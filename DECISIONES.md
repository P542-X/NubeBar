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

## Fase 4 · Notificaciones: se lee la base de Windows, sin paquetes ni certificados

Tal como pediste, nada de MSIX disperso ni de instalar certificados. Se lee
`%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db`, que es donde
Windows las guarda, **sin tocarla**: se copian los tres archivos (db, -wal y
-shm) a una carpeta temporal con `FileShare.ReadWrite` y se abre la copia en
solo lectura. Nunca se escribe ni se borra nada del original.

Dos fallos encontrados por el camino:

- **El archivo copiado se quedaba bloqueado.** Microsoft.Data.Sqlite mantiene la
  conexión en un grupo y no suelta el archivo al salir del `using`, así que la
  copia siguiente fallaba: las notificaciones habrían funcionado una sola vez y
  nunca más. Se arregla con `Pooling = false`.
- **"No molestar" salía encendido siempre.** `SHQueryUserNotificationState`
  devuelve 5 para *acepta notificaciones*, y el 5 estaba en la lista de estados
  que cuentan como "no molestar". Los buenos son 2, 3, 4 y 6. Venía de la fase 5
  del bloque anterior.

### Aviso: dependencia con una vulnerabilidad conocida

`Microsoft.Data.Sqlite` 10.0.0 arrastra `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, que
tiene el aviso **GHSA-2m69-gcr7-jv3q** (gravedad alta) y **no hay versión
parcheada** a día de hoy. Es un fallo de corrupción de memoria de SQLite cuando
el número de términos de agregación supera al de columnas, o sea: hace falta
poder ejecutar SQL hecho a medida. Aquí la consulta es fija, la base la genera
el propio Windows en local y no entra nada de fuera, así que no es explotable en
este uso. Lo dejo escrito para que lo sepas y decidas; si prefieres no tener esa
dependencia, dímelo y quito la fase entera.

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| Microsoft.Data.Sqlite | 10.0.0 | MIT | Leer la base de notificaciones de Windows |

### Lo que no se pudo probar con notificaciones de verdad

Tienes las notificaciones **apagadas en Windows** (`ToastEnabled = 0` en
`HKCU\...\PushNotifications`), así que la base real no guarda ningún toast: hay
2 filas y las dos son de tipo `badge`. Lanzar un toast de prueba desde
PowerShell no cambia nada porque Windows lo descarta antes de guardarlo.

No te toco ese ajuste. Para poder probarlo igualmente hay una variable de
entorno, `NUBEBAR_NOTIS_PRUEBA=<carpeta>`, que apunta el lector a una base
sintética con el mismo esquema. Con ella sí se ha probado el camino entero:
3 toasts leídos y el `badge` descartado, el XML partido en título y texto, el
identificador de app convertido en algo legible, solo las nuevas desde el
arranque, y el aviso saliendo en la isla con su icono, la app en color de
acento, el título y el texto.

## Fase 5 · Escritorios virtuales: te dejo los 2 que ya tenías

El encargo decía "al acabar, un solo escritorio virtual". Resulta que **ya
tenías 2** antes de que yo empezara, así que dejarte uno habría significado
borrarte uno tuyo. Lo correcto es dejarlo como estaba: 2 escritorios y en el
mismo en el que estabas. Comprobado al terminar.

La librería es `Slions.VirtualDesktop` 6.9.2 (MIT), el fork mantenido del
clásico de Grabacr07. Funciona en tu build (Windows 10.0.26200.9711). Como esta
API no es pública y Microsoft la cambia entre versiones, todo va dentro de
try/catch y el módulo se esconde si deja de reconocer el sistema, en vez de
tumbar NubeBar.

Probado crear, cambiar y borrar con un escritorio **de prueba** creado por mí
(nunca sobre los tuyos): 2 → crea el 3 → va al 3 → vuelve al 1 → borra el 3 →
2 escritorios y en el 1, igual que al empezar.

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| Slions.VirtualDesktop | 6.9.2 | MIT | Listar, cambiar y crear escritorios virtuales |

## Solo esconder la barra de Windows donde hay isla

Con `Barra.Monitores = "principal"`, NubeBar seguía escondiendo **todas** las
barras de tareas, incluidas las secundarias, aunque en esas pantallas no pusiera
ninguna isla. Resultado: el monitor secundario se quedaba sin nada, ni la barra
de Windows ni la nuestra.

Ahora solo se esconden las barras de los monitores donde de verdad hay isla
(`MainWindow.MonitoresConIsla()`). Al devolverlas se muestran **todas**, sin
filtrar, que es lo seguro.

Queda una limitación que no tiene arreglo por API: el "ocultar automáticamente"
de la barra de tareas es un ajuste **global** de Windows, no hay uno por
pantalla. Como en el monitor principal hace falta para que la barra libere su
hueco, la barra del secundario también queda en modo "ocultar
automáticamente": está, se usa, pero hay que llevar el ratón al borde para que
asome. La alternativa sería no esconder la barra principal, que es lo contrario
de lo que hace NubeBar.

## Una sola NubeBar, y la isla centrada pase lo que pase

Dos cosas que salieron al probar el exe en otro equipo.

**1. Se podían abrir dos NubeBar.** No había nada que lo impidiera. Con dos,
hay dos juegos de islas, dos ganchos de teclado peleándose por la tecla
Windows, dos servicios de bandeja disputándose el orden Z y dos procesos
escondiendo y mostrando la barra de tareas: lo normal es acabar sin barra.
Ahora hay un mutex con nombre (`Local\NubeBar.UnaSola`); la segunda lo ve, lo
apunta en el log y se cierra sin tocar nada. El guardián queda fuera de la
comprobación a propósito: es el mismo ejecutable y tiene que poder convivir.
Windows limpia el mutex aunque el proceso muera a lo bruto, y si aun así
quedara abandonado, `WaitOne` lo avisa y lo tomamos nosotros: no hay forma de
quedarse bloqueado sin poder arrancar.

**2. La isla podía quedarse descentrada.** El XAML fijaba `Width="10"` y esa
propiedad no se actualizaba nunca, porque la ventana se dimensiona con
`SetWindowPos` en píxeles. Mientras nadie la mira da igual, pero al llegar un
`WM_DPICHANGED` —arrancar en un monitor al 125 %, o mover la ventana entre
pantallas con distinto escalado— WPF **reimpone** esa anchura y encoge la
ventana. Como la isla va centrada *dentro* de la ventana, una ventana de 10 px
la deja pegada al borde izquierdo en vez de en medio de la pantalla.

Arreglado poniendo `Width`/`Height` a `NaN` después de colocar, que es como se
le dice a WPF "el tamaño no lo gestionas tú", y atendiendo `WM_DPICHANGED` para
releer el monitor y recolocar. Así la isla queda centrada en 720p, 1080p, 2K o
4K y con cualquier escalado.

De paso, las pantallas se filtran por handle único (`DistinctBy`), por si
`EnumDisplayMonitors` devolviera dos veces la misma en algún clonado.

## Los paneles miden su contenido en vez de fiarse de un alto a mano

Cada sitio que abre un panel le pasaba un alto calculado a mano (la vista
general, por ejemplo, `filas * 46 + 16`). Basta con que esa cuenta se quede
corta por un píxel para que la última fila salga recortada, porque el cajón
recorta a sus bordes. Y hay dos cosas que la cuenta no tenía en cuenta: el filo
de acento de la isla, que se come su grosor por arriba y por abajo, y que los
márgenes y tamaños de dentro dependen del tema y de `Barra.Alto`.

Ahora `AbrirPanel` mide el contenido con `Measure` y, si lo que necesita es más
que lo pedido, le da lo que necesita y lo apunta en el log. El alto que pasa
quien abre el panel pasa a ser un mínimo, no un máximo.

**No pude reproducir el recorte en este equipo**: la vista general con 8
ventanas sale en 3 filas y le sobran 12 px. El arreglo es preventivo pero ataca
la causa real; si sigue pasando, en el log habrá una línea "pedía X de alto
pero necesita Y".

## El acento es tinta, y la tinta no lleva transparencia

El color de acento pinta **texto, filos y gráficas**: el reloj grande del menú,
los porcentajes de CPU y RAM, el filo de la isla, la ventana activa de la vista
general. Si ese color viene con alfa (un `#80FFFFFF`, por ejemplo), todo eso se
dibuja a medias y queda gris, y da la sensación de que la isla "no cambia de
color" por mucho que toques el tema. La transparencia de la isla la pone
`Tema.Fondo`, que es donde tiene sentido; en el acento solo estorba, así que al
aplicar el tema se ignora el alfa y se deja constancia en el log.

Y un acento que resulta ser **el mismo gris que el texto** no es un acento: no
hay nada que distinguir. En ese caso (saturación < 8% y contraste con el texto
< 1.5) se vuelve al de por defecto `#7AA2F7` y se avisa en el log, con el
porqué. Un gris que sí se distingue del texto (un `#808080` sobre texto blanco,
por ejemplo) se respeta tal cual: eso es una decisión de diseño, no un error.

Para que esto no vuelva a ser un misterio, al arrancar y en cada cambio de tema
se escribe una línea con lo que de verdad se está pintando:
`tema aplicado: fondo=... texto=... acento=... (origen: config | wallpaper)`.

## Maximizada no es pantalla completa, pero ahora puedes decidirlo tú

La isla se aparta cuando algo ocupa la pantalla entera, y una ventana
**maximizada** no contaba: deja ver la isla y el borde de Windows, y si contara
desaparecería cada vez que maximizas el navegador, que es casi siempre. El
problema es que un juego o un vídeo en ventana maximizada tampoco la echaban,
y ahí sí estorba.

Clave `Barra.OcultarConMaximizada`, con su interruptor en Ajustes > Isla.
Nació en `false` para no cambiarle el comportamiento a nadie, pero Paulo
pidió que fuera `true` de serie con un argumento que es el bueno: quien está
jugando o viendo un vídeo a pantalla completa no tiene por qué ver la isla
encima, y eso no se descubre si hay que ir a buscarlo a los ajustes. Es la
primera clave nueva que SÍ cambia el comportamiento de una config vieja: al
actualizar, la isla se aparta también al maximizar el navegador. Quien no lo
quiera lo apaga en Ajustes. Cuando está activa se mira el **rectángulo**
contra el área de trabajo del monitor, no si Windows marca la ventana como
maximizada: así también cuenta una ventana estirada a mano hasta ocupar lo
mismo, y se evita el lío de F11 (un navegador maximizado que pasa a pantalla
completa sigue marcado como maximizado).

Escondida por maximizada se comporta como escondida por pantalla completa:
asoma si dejas el ratón pegado al borde 300 ms.

## La isla no le roba el primer plano a nadie al arrancar

Probando con VALORANT delante salió un fallo de verdad: al arrancar, NubeBar se
ponía ella misma en primer plano y sacaba del juego. Y de rebote la isla se
quedaba encima, porque "¿hay algo a pantalla completa?" mira la ventana activa,
y la activa éramos nosotros (que no contamos, claro).

La ventana lleva `WS_EX_NOACTIVATE` desde siempre, pero ese estilo se pone en
`Loaded`, o sea **después** de que WPF haya hecho el `Show()` y la haya
activado. La cura es una propiedad en el XAML, `ShowActivated="False"`: así no
se activa ni en ese primer instante. Solo afecta al primer `Show()`; el menú
Inicio sigue cogiendo el teclado con su mecanismo de siempre (comprobado:
tecla Windows, escribir "calc", sale la Calculadora).
