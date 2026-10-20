# Decisiones

Lo que fui decidiendo por mi cuenta, con el motivo. Cada entrada es algo que no
estaba previsto en el encargo o que cambió sobre la marcha.

## Dependencias y sus licencias

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| System.Data.OleDb | 9.0.0 | MIT | Índice de Windows Search en el buscador del menú Inicio |

## Fase 1 · La bandeja del sistema (resuelto en la segunda pasada)

**Qué pasa.** ManagedShell 0.0.374 se registra bien como bandeja del shell: su
ventana `Shell_TrayWnd` se crea y queda *antes* que la de Explorer, así que un
`FindWindow("Shell_TrayWnd")` nos encuentra a nosotros. Pero no llega **ni un
solo icono**: medido dos veces, con `AutoStartTrayService` y llamando a
`NotificationArea.Initialize()` a mano (que es quien difunde `TaskbarCreated`).

**Por qué.** En Windows 11 la bandeja de Explorer ya no es el `ToolbarWindow32`
clásico; es XAML. Dos consecuencias: `ExplorerTrayService`, que servía para leer
los iconos que ya estaban registrados antes de arrancar nosotros, devuelve cero
(comprobado: `TB_BUTTONCOUNT` sobre `TrayNotifyWnd/SysPager/ToolbarWindow32` ni
siquiera encuentra la ventana), y las apps que ya tenían su icono puesto no se
vuelven a registrar con nadie mientras Explorer siga vivo y atendiéndolas.
Funcionaría de verdad sustituyendo a Explorer como shell, que es lo que hacen
Cairo Shell y RetroBar, y eso ni se plantea aquí.

**Qué hice.** El módulo queda escrito y funcionando (iconos con tooltip, clic
izquierdo, derecho y doble, animación de entrada y salida y desplegable "^"),
pero:
- `Barra.Bandeja` por defecto **false**.
- El servicio solo se enciende si además pones el módulo `bandeja` en alguna
  zona. Si se encendiera sin el módulo, las apps que sí nos hicieran caso
  dejarían de salir en la barra de Windows sin aparecer en ningún sitio.
- Si lo enciendes y a los 8 segundos no ha llegado ningún icono, lo dice en el log.
- Interruptor en Ajustes → Sistema con la explicación al lado.

**Lo más seguro y reversible**, que es la regla de esta noche: no tocar la
bandeja de Explorer salvo que lo pidas a propósito.

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| ManagedShell | 0.0.374 | MIT (cairoshell) | Recibir los iconos de la bandeja del sistema |

## Fase 2 · Un fallo de COM que escondía todas las sesiones de audio

`IAudioSessionControl2.IsSystemSoundsSession()` no lleva parámetros de salida.
En una interfaz `ComImport` sin `PreserveSig`, el marshaller de .NET interpreta
el `int` declarado como el valor de retorno lógico y no como el HRESULT, así que
devolvía **0 siempre**, o sea "sí, son los sonidos del sistema", y el filtro se
comía las tres sesiones. Se distingue por PID, que para esto vale igual: los
sonidos del sistema son el pid 0.

## Fase 2 · Lo que NO se probó a propósito

Cambiar la salida de audio, conectarse a una wifi y encender o apagar el
bluetooth están implementados pero sin probar: tocarlos de verdad cambiaría la
configuración del equipo. `IPolicyConfig` sí se comprueba (responde en este
Windows), y si no respondiera la opción no aparecería. Los pasos para probarlo
a mano están en PRUEBAS-PARA-PAULO.md.

### Fase 1 (corregido) · Sí funciona: faltaba el truco del orden Z

La conclusión de arriba era **equivocada**, y lo demostró RetroBar: ejecutado un
minuto en este mismo PC, con Explorer vivo, enseña los 16 iconos de la bandeja.
Leyendo su código y el de ManagedShell apareció lo que faltaba.

`NotificationArea.Initialize()` llama a `TrayService.Run()`, y ahí está la magia:

```
Resume()  ->  SetWindowsTrayBottommost()   // la Shell_TrayWnd de Explorer, al FONDO
              MakeTrayTopmost()            // la nuestra, arriba del todo
              trayMonitor.Start()          // y lo revisa cada 100 ms
SendTaskbarCreated()                       // "volved a registrar vuestros iconos"
```

Como `FindWindow` devuelve la primera ventana en orden Z, con eso los
`Shell_NotifyIcon` de las apps nos llegan a nosotros. Mis dos intentos fallaron
por montar el `ShellConfig` a mano; usando `ShellManager.DefaultShellConfig` y
fijando `EnvironmentHelper.IsAppRunningAsShell` (que es lo que hace RetroBar)
entran los 16 iconos a los 8 segundos.

**Resultado:** `Barra.Bandeja` pasa a **true** por defecto. Sigue haciendo falta
poner el módulo `bandeja` en alguna zona para que el servicio arranque, por la
misma razón de antes: encenderlo sin enseñarlo dejaría los iconos en tierra de
nadie. Comprobado que al cerrar NubeBar los iconos vuelven a la bandeja de
Windows (ManagedShell vuelve a difundir `TaskbarCreated` al soltarse).

**Aviso sobre RetroBar:** al cerrarlo dejó la barra de tareas de Windows en
"ocultar automáticamente". Se devolvió a su estado con `ABM_SETSTATE`.

## Fase 3 · El aviso de volumen de Windows NO se esconde

La regla era: esconderlo solo si puedo garantizar que vuelve aunque maten
NubeBar. **No puedo**, así que se queda visible.

El motivo no es el guardián, es que en este Windows 11 (26200) el aviso de
volumen ya no es una ventana que se pueda esconder. Comprobado enumerando todas
las ventanas de nivel superior justo antes y justo después de pulsar la tecla de
volumen: no aparece ninguna nueva, ni visible ni oculta, y `ShellExperienceHost`
y `ShellHost` solo tienen un `DesktopWindowXamlSource` de 1x1 px. Lo pinta el
compositor, no una `NativeHWNDHost` como en Windows 10, así que no hay ningún
`ShowWindow(SW_HIDE)` que aplicar.

Consecuencia: al cambiar el volumen se ven los dos avisos, el de Windows y el
de la isla. El hueco para devolverlo sigue marcado en `Guardian.RestaurarElShell()`
por si en una versión futura vuelve a ser una ventana.

## Fase 3 · El brillo no se vigila, se avisa al cambiarlo

Preguntarle el brillo a un monitor por DDC/CI es un viaje de ida y vuelta por el
cable de unos 50 ms. Sondearlo diez veces por segundo como el volumen sería un
disparate, así que el aviso de brillo lo dispara quien lo cambia (el deslizador
del centro de control). Si lo cambias desde el menú del propio monitor, NubeBar
no se entera: no hay forma barata de enterarse.
