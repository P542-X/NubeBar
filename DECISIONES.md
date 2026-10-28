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

## Con un juego delante, el menú no se dejaba cerrar

El menú Inicio se cerraba con un clic fuera gracias a la captura del ratón
(`Mouse.Capture` en modo subárbol, como hacen los menús de Windows). Eso se cae
en cuanto **otra aplicación se activa**: Windows nos quita la captura y el clic
ya no nos llega. Con VALORANT delante pasaba justo eso, y encima el vigilante
del foco creía que se lo había robado Wallpaper Engine y nos devolvía el primer
plano: el menú parecía reabrirse solo y solo se podía cerrar con Esc.

Dos cambios:

- El vigilante del foco distingue a quién se lo ha llevado. Si es una
  aplicación de verdad (has hecho clic en ella), el menú **se cierra**. Solo se
  pelea con los que se devuelven el foco solos: Wallpaper Engine y las ventanas
  del escritorio.
- El clic fuera se mira además **a pelo cada 150 ms** (botón + posición del
  puntero + `InputHitTest`, que acierta en las esquinas redondeadas), sin
  depender de la captura. Se usa el bit de "pulsado desde la última vez" de
  `GetAsyncKeyState` para no perderse los clics cortos.

Medido: del clic fuera al menú cerrado, 180-220 ms. Y el vigilante de un
segundo ya ni lo intenta con una aplicación de verdad: hacerlo le robaba el
primer plano al juego durante un instante antes de cerrarse. Y escribiendo durante 10
segundos seguidos el menú aguanta abierto, que era lo que protegía la captura.

## El filo de la ventana activa salía cortado

En la vista general, la celda de la ventana activa lleva un filo redondeado del
color de acento. Pedía más alto del que tenía: el marco medía 38 px y llevaba
3 px de margen arriba y abajo, o sea 44, dentro de un botón de 42. WPF recorta
lo que sobra y se comía el filo de abajo.

Quitarle la altura tampoco valía: el botón centra su contenido, así que el
borde se encogía al tamaño del texto (19 px medidos) y parecía que faltaba el
filo de abajo. Las cuentas que sí salen: marco de 36 + 3+3 de margen = 42
dentro de un botón de 44, y el botón entra en la fila de 46. Medido contando
píxeles azules en la captura: filo arriba en y=104 y abajo en y=139, 35 px.

Y una lección de método: `dotnet build` con NubeBar abierta falla al copiar el
exe con MSB3027, no con un "error CS". Filtrar el log del build por "error CS"
me hizo creer dos veces que había compilado cuando no. Se filtra por "error" a
secas, o se cierra NubeBar antes.

## El menú con un juego delante: salir rápido y no cerrarse solo

Dos cosas más que solo se ven con un juego o una ventana maximizada delante,
o sea con la isla escondida:

- **Tardaba en salir.** Al abrir el menú, la isla seguía deslizada fuera y
  esperaba a que el vigilante de 150 ms viera que el primer plano había
  cambiado. Y si el juego no soltaba el primer plano, no cambiaba nunca y el
  menú se quedaba invisible hasta que movías el ratón al borde. Ahora `AbrirPanel`
  saca la isla él mismo, sin depender de nadie. Medido: de la tecla al panel
  colocado, 25-42 ms en caliente y 108-116 ms la primera vez; el resto de lo que
  se ve es la animación de apertura.
- **Se cerraba solo.** La regla nueva de "si el primer plano se lo lleva una
  aplicación de verdad, cierro el menú" cerraba el menú recién abierto cuando el
  juego no nos cedía el primer plano. Ahora solo cierra si el menú **llegó a
  tener** el foco y lo perdió después. El clic fuera lo sigue cerrando igual,
  porque eso se mira aparte.

Y con un panel abierto la isla ya no se vuelve a esconder al apartar el ratón:
estarías usando el menú con el teclado y se te iba debajo.

## Al cerrar un panel la isla desaparecía bajo el puntero

Con una ventana maximizada detrás: abres la vista general desde la isla, la
cierras con el mismo botón, y al cerrarse el panel el primer plano vuelve a la
ventana maximizada. Como ahora eso esconde la isla, se iba justo debajo del
puntero, donde acababas de hacer clic. Parecía que cerrar el panel cerraba la
isla entera.

Regla nueva: cuando toca esconderse, si tienes el ratón dentro de la franja de
la isla, se queda **asomada** en vez de irse, y se va cuando apartas el ratón,
igual que cuando asoma por el borde. La franja se mide con el alto de la barra
y no con `ActualHeight`, que con un panel abierto es el del panel entero.

## La caída del buscador, y por qué una excepción suelta se lleva la aplicación

Caída real, con su rastro en el log:

```
ERROR en FATAL: System.IO.FileNotFoundException: 'System.Data.OleDb ...'
   at NubeBar.Fuentes.ConsultarIndice(...)
   at System.Threading.Thread.StartHelper.Callback(...)
guardián: NubeBar ha muerto sin avisar, devuelvo el shell
```

Dos lecciones. La primera: `ConsultarIndice` **sí** tenía su `try/catch`, pero no
sirvió de nada, porque el fallo es al **cargar el ensamblado**, y eso pasa al
entrar en el método, antes de que su `try` exista. Un `catch` solo protege de lo
que ocurre dentro; para esto tiene que estar en quien llama.

La segunda: una excepción sin capturar en un hilo de fondo **no la ve nadie** y
termina el proceso. Ahora el cuerpo de ese hilo va envuelto, la búsqueda de
archivos se apaga sola si falla (y lo dice en el log, una vez), y en `App` hay
tres redes: `AppDomain.UnhandledException` (solo puede dejar constancia),
`TaskScheduler.UnobservedTaskException` y `DispatcherUnhandledException`, esta
última con `Handled = true`: vale más una isla con un módulo tonto que dejarte
sin barra de tareas.

## Lo que encontraron tres auditorías en paralelo

Con el equipo parado una hora, tres agentes revisaron a la vez la interoperación
con Win32/COM, la configuración y el disco, y la interfaz. Lo que de verdad
estaba roto:

**Corrupción de memoria** (tres sitios, todos por lo mismo): `ByValTStr` sin
`CharSet.Unicode` cuenta BYTES donde el nativo cuenta WCHAR, así que la
estructura mide la mitad y el API escribe fuera. `PHYSICAL_MONITOR` (136 contra
264, y eso corre en **cada arranque** al probar el brillo), `WLAN_INTERFACE_INFO`
y `WLAN_AVAILABLE_NETWORK` (372 contra 628, de ahí que la lista de wifis saliera
corta o vacía). Y el `PROPVARIANT` de `Audio.cs` declaraba 16 bytes cuando en x64
son 24: machacaba la pila cada vez que se leía el nombre de una salida. En
`Nativo.cs` ya estaba bien declarado; ahora los dos son iguales.

**Arranque y configuración**: un `"Barra": null` explícito en el JSON deja la
propiedad a null (los inicializadores `= new()` solo valen si falta la clave) y
mataba el arranque **antes de que existiera el log**: ni ventana, ni error, nada.
Si el config no parseaba, la ventana de ajustes empezaba con un árbol vacío y el
primer guardado borraba toda la configuración. Y ninguna escritura era atómica.

**Seguridad de la barra de tareas**: si no se podía guardar `barra-estado.txt`,
el respaldo preguntaba a Windows el estado actual, que ya incluía el
auto-ocultar puesto por nosotros, y la barra se quedaba oculta para siempre. Y
el guardián podía quedarse esperando sobre un handle que el GC había cerrado.

**Cerrar todas las apps** incluía nuestra propia ventana de ajustes, así que
"forzar el cierre" podía matar a NubeBar.

**El `CPU 0%`**: `Nativo.Cpu()` es una lectura diferencial y se llamaba dos o
tres veces por tick (el historial, el módulo de la isla y la gráfica del menú);
la segunda no tenía tiempo que comparar y devolvía 0. Ahora la medida real se
toma como mucho cada medio segundo y todos reciben la misma cifra.

## "Un solo fichero" no era un solo fichero

Lo que de verdad provocó la caída de hoy. `PublishSingleFile` **no** mete las DLL
nativas en el exe salvo que se le diga: `IncludeNativeLibrariesForSelfExtract`
vale `false` por defecto. El `dist\` local tenía esto al lado del ejecutable:

```
PresentationNative_cor3.dll  wpfgfx_cor3.dll  D3DCompiler_47_cor3.dll
PenImc_cor3.dll  vcruntime140_cor3.dll  e_sqlite3.dll
```

El exe funciona mientras esos seis ficheros estén a su lado. Copiado solo a otra
carpeta, muere nada más arrancar con `System.DllNotFoundException` en
`SetWindowLongPtrWndProc` (código de salida `0xC000041D`), y sin escribir **ni
una línea** en el log, porque se cae antes de llegar a él.

Y de ahí el enredo entero: cuando yo republicaba `dist` con NubeBar abierta
desde ahí, el proceso vivo se quedaba sin sus piezas y cualquier ensamblado que
cargara a partir de ese momento reventaba. Eso explica el
`System.Data.OleDb` del buscador, el `UIAutomationProvider` al mover el foco y
hasta un `System.Linq` imposible. No era un fallo del buscador: era el exe
desapareciendo bajo sus pies.

Dos conclusiones:

1. `IncludeNativeLibrariesForSelfExtract=true` en el .csproj. El workflow de la
   Release ya publicaba bien (su exe **sí** arranca solo en una carpeta vacía,
   comprobado), así que esto arregla el publish local, que es el que va a
   `dist\`. El exe pasa de 65 a 69,6 MB, que es exactamente lo que pesa el de
   GitHub.
2. Nunca republicar `dist` con una NubeBar corriendo desde ahí. Para probar, una
   copia en otra carpeta; para usar a diario, el exe de la Release.

El arreglo de las excepciones en hilos de fondo (la red de seguridad que se
añadió el mismo día) sigue siendo bueno por su cuenta, pero no era la causa.

## El exe desapareciendo bajo sus pies, parte 2: la barra de descargas

**Qué pasa.** Con la Release v1.1.24 ya instalada, abrir la caja del buscador
cerraba NubeBar de golpe. En el log, al enfocar:

```
TypeInitializationException: AutomationPeer
 ---> FileNotFoundException: 'UIAutomationProvider, Version=10.0.0.0...'
   at ButtonBase.OnMouseLeftButtonDown
```

y tres segundos después, en el guardián, un `System.Linq` imposible. Los mismos
dos síntomas de la vez anterior, pero esta vez nadie estaba republicando nada.

**Por qué.** El exe que se estaba ejecutando no era el de `Descargas`:

```
pid 35732  C:\...\AppData\Local\Temp\scoped_dir2664_1428973678\NubeBar.exe
```

`scoped_dir…` es la carpeta temporal de Chrome. Al abrir la descarga desde la
barra del navegador, Chrome no ejecuta el fichero de `Descargas`: lo copia a una
carpeta suya, lo lanza desde ahí y la borra a los pocos segundos. Esa carpeta se
quedó vacía con el proceso vivo (y su guardián siguió en marcha apuntando a un
exe que ya no existía).

Con `IncludeNativeLibrariesForSelfExtract`, los ensamblados **gestionados** no se
extraen: se leen del propio exe a medida que hacen falta. Mientras solo cargues
cosas al arrancar no se nota; la primera carga tardía después del borrado
revienta. Y la accesibilidad del foco (`UIAutomationProvider`, que WPF pide la
primera vez que algo recibe el foco) es justo una carga tardía. De ahí que
crasheara al abrir el buscador y no antes.

Medido con una prueba de consola aparte, publicada de las dos formas, borrando el
exe en marcha:

| Publicación | Carga tardía con el exe ya borrado |
|---|---|
| `IncludeNativeLibrariesForSelfExtract` | `FileNotFoundException` |
| `IncludeAllContentForSelfExtract` | OK |

**Decisión.** `IncludeAllContentForSelfExtract=true` en el .csproj y en el
workflow. Extrae los 247 ficheros a `%TEMP%\.net\NubeBar\<id>` **antes** de que
arranque el runtime, así que el exe no vuelve a hacer falta en toda la sesión.
Cuesta medio segundo en el primer arranque de cada versión (1,1 s contra 0,6 s
medidos con el exe de verdad); los siguientes, 90 ms. Incluye las nativas, así
que sustituye a la propiedad anterior. Efecto colateral a recordar:
`AppContext.BaseDirectory` pasa a ser la carpeta de extracción, no la del exe.

Y dos cosas más, que son la misma historia por otro lado:

1. El log dice desde dónde se está ejecutando, y avisa si es una carpeta
   temporal. Media investigación de hoy era averiguar precisamente eso.
2. "Arrancar con Windows" se niega a crear el acceso directo si el exe está en
   una carpeta temporal: apuntaría a la nada en el siguiente arranque. Antes lo
   creaba tan contento.

## El buscador: lo que no se veía y las capturas de pantalla

**La marca de selección era invisible.** El menú Inicio pintaba la marca, su borde,
la etiqueta de categoría y el cursor de la caja con el acento **de verdad**, no con
el corregido. Con un acento negro —el de la configuración de aquí, y también lo que
puede sacar el extractor del wallpaper— eso es negro sobre fondo negro: las flechas
movían la selección y no se veía absolutamente nada. Ya existía `_cAcentoLegible`
(el mismo tono aclarado hasta que contrasta, ver `Contraste.cs`) y se usa en media
interfaz; el buscador se había quedado fuera. Ahora lo usa, la fila elegida va a
pleno color con las demás al 65 %, y la marca da un destello al saltar de fila.

**Las flechas se escuchan en la raíz del panel**, no en la caja. En cuanto pulsabas
cualquier cosa del panel (una tarjeta de la portada, una fila), el foco se iba de la
caja y las flechas dejaban de bajar hasta que volvías a hacer clic en ella. Al
navegar, el foco vuelve solo a la caja para poder seguir escribiendo.

**Y no se podían hacer capturas con el menú abierto**, por dos motivos distintos:

1. En modo captura, el hook se tragaba *todas* las teclas durante hasta 1,5 s, Impr
   Pant incluida. Ahora deja pasar Impr Pant y cualquier combinación con Win o Alt:
   son del sistema, no de la caja.
2. El recortador sale a pantalla completa y te hace arrastrar un recuadro por encima
   de la isla. Para NubeBar eso era "otra aplicación al frente" *y* "un clic fuera",
   o sea dos motivos para cerrar el menú, y además `RecuperarCaptura` le volvía a
   quitar el ratón, con lo que no dejaba ni arrastrar el recuadro. Ahora la captura
   del ratón no se recupera si el primer plano no es nuestro, y mientras delante haya
   un recortador conocido (Recortes, Game Bar, ShareX, Greenshot, Snagit, Lightshot,
   PicPick, Flameshot) el menú no se cierra ni pelea por el foco.

## La isla respira con lo que suena, y cerrar deja de ser un portazo

**El pulso de audio.** El medidor de la salida (`IAudioMeterInformation`, el mismo que
usa el mezclador de Windows) da el pico de lo que está sonando AHORA, de 0 a 1, sin
importar de qué programa venga: vale para Spotify, para un vídeo o para un juego. Se
pregunta 25 veces por segundo y con eso se mueven tres cosas:

- la isla se ensancha un 1,6 % y se achata un 2,2 %, como si el sonido le hiciera presión;
- el halo del acento crece de 8 a 22 de radio, que es lo que se ve de lejos;
- y dos filos de color, uno frío y uno cálido, se separan hasta 2,6 px: aberración cromática.

Tres decisiones dentro de esto:

1. **La escala va en `Cuerpo`, no en `Isla`.** La isla ya tiene alto y ancho animados, y
   su `RenderTransform` lo usan el arrastre y el deslizar para esconderse. `Cuerpo`
   estaba libre y escalarlo no provoca ni una medida de layout.
2. **Del halo solo se toca el radio.** La opacidad la animan el expandirse y el destello
   de los avisos; pelearse con ellas dejaba parpadeos.
3. **La curva es lineal y con poca ganancia** (`pico × 1,25`). Medido con el medidor de
   verdad, la música real da picos de 0,06 a 0,81; al comprimirlos con una curva el
   efecto se quedaba casi a tope todo el rato, que es lo que lo vuelve cansino. Sube
   rápido y baja despacio, como un vúmetro.

El temporizador rápido solo corre mientras hay sonido: en silencio se para solo a los
2 s y lo despierta el tick de un segundo. Se apaga en Ajustes > Isla.

**Y el cierre.** `Suave` es `CubicEase EaseOut`: arranca de golpe y se arrastra al
final. Para abrir es perfecto (ves el panel aparecer ya), pero para cerrar es malísimo,
y se usaba para las dos cosas: el panel se plegaba casi entero en los primeros 80 ms de
los 300 y el resto no se veía, así que parecía un corte y no un cierre. Peor aún, el
contenido se desvanecía en 120 ms mientras la isla tardaba el triple, o sea que lo que
veías era una isla VACÍA encogiéndose.

Ahora hay una curva `Comoda` (`EaseInOut`) para cerrar, el cierre dura lo que duró
abrir (380 ms el menú Inicio, 260 ms los paneles pequeños), y el contenido se va con la
isla metiéndose hacia ella, acabando un pelín antes que el pliegue. Lo mismo al
recogerse la isla: antes se estrechaba del todo en 90 ms y los módulos desaparecían en
120 ms.

## Red, volumen a mano, idioma del teclado y test de velocidad

**La red tiene su propio panel.** Meter las wifis con su contraseña y un test de
velocidad dentro del centro de control lo hacía larguísimo, así que el icono de
red abre un panel suyo y el centro de control se queda con un resumen y un
enlace "Redes y velocidad ›".

**Conectarse a una wifi.** Con perfil guardado, `WlanConnect` con ese perfil. Sin
perfil, se crea uno (WPA, WPA2 o WPA3-SAE con la contraseña, o abierta) con
`WlanSetProfile` y se espera hasta 15 s a que Windows diga que está dentro. Si no
entra, el perfil recién creado **se borra**: si no, Windows se quedaría con una
clave mala y reintentaría con ella solo cada vez que viera la red. Las redes
802.1X y las WEP no se montan aquí: se abren los ajustes de Windows.

**Para escribir la contraseña la isla coge el teclado**, igual que el buscador del
menú Inicio, y lo devuelve al cerrar el panel.

**El icono de red ya no dice "cable" estando por wifi** cuando hay adaptadores
virtuales (Hyper-V, WSL, VirtualBox): esos son Ethernet y están "Up", pero no
tienen puerta de enlace. Ahora cuentan solo las interfaces con salida.

**Idioma del teclado.** Es de cada hilo, y el que importa es el de la ventana en
la que escribes: la isla nunca se activa, así que se le pregunta a la ventana en
primer plano y se le manda `WM_INPUTLANGCHANGEREQUEST`, lo mismo que Win+Espacio.
Con dos entradas del mismo idioma (aquí: inglés con teclado de EE. UU. e inglés con
teclado español) el código solo no basta, y salen como `EN·US` y `EN·ES`.

**Test de velocidad** contra speed.cloudflare.com: sin clave ni cuenta, y sirve
desde el nodo más cercano. Ping = la mejor de 7 peticiones vacías (la primera abre
TLS y no cuenta). Bajada y subida con 4 conexiones en paralelo durante 7 s, sin
contar el primer segundo (arranque de TCP). Medido aquí: 33 ms, 216 y 191 Mb/s.
Gasta datos de verdad: en una fibra de 1 Gb, del orden de 1 GB por test.

**Lo que no se probó a propósito**: conectarse a otra wifi y desconectarse,
porque te habría dejado sin red a media sesión. Listar, escanear, las conexiones,
el idioma y el test de velocidad sí se probaron contra este equipo.

## Las teclas de volumen, por fin sin el aviso de Windows

Lo de la fase 3 sigue siendo verdad: en Windows 11 el aviso de volumen del sistema
no es una ventana y no se puede esconder. Pero sí se puede evitar que salga: el
hook de teclado de bajo nivel ya existía (tecla Windows), así que ahora también se
queda las teclas de volumen (`VK_VOLUME_UP/DOWN/MUTE`) y no las deja llegar a
Windows. El volumen lo cambiamos nosotros, de 2 en 2 como Windows, subir quita el
silencio, y el aviso de la isla sale solo porque `VigilarVolumen` ve el cambio.

Se traga también la de soltar, para que Windows no reciba un "soltar" sin su
"pulsar". Es reversible por construcción: si NubeBar se cierra o muere, el hook
muere con ella y las teclas vuelven a lo de siempre, aviso de Windows incluido.

Límite: los botones de volumen de algunos auriculares (Jabra, por ejemplo) no van
por el teclado sino por HID directo a Windows; esos seguirán sacando su aviso.

Clave `Barra.TeclasVolumen` (true de serie), con interruptor en Ajustes > Isla.
Probado con la tecla simulada: sale la barra en la isla y abajo no aparece nada.
## Batería, apps en segundo plano y redes que tardaban

**Las redes que "tardaban".** No era la app: listar las wifis cuesta 28 ms medidos.
Lo que da Windows es su lista en caché, y solo la renueva cuando escanea, más o
menos una vez por minuto; a veces traía una sola red hasta pulsar "buscar".
Ahora el panel enseña la caché al momento, pide un escaneo y repasa a los 1,5,
3,5 y 6 s, rehaciendo la lista solo si algo ha cambiado (y nunca con la caja de
la contraseña abierta, que perderías lo escrito).

**El icono de la batería abría el centro de control.** No tenía panel propio y
reaprovechaba el del volumen. Ahora tiene el suyo: porcentaje, si carga, lo que
queda, el ahorro de batería (solo se lee: no hay API pública para encenderlo, se
manda a Configuración) y el modo de energía Eficiencia / Equilibrado /
Rendimiento con `PowerSetActiveOverlayScheme`, que es lo que usa la propia
Configuración. El icono de la pila enseña su nivel y el rayo al cargar.

**Las apps en segundo plano.** El módulo `bandeja` ya existía pero no venía
puesto. Ahora viene de serie, igual que `control` (se añade una vez a las configs
de antes, con su bandera `Modulos.BandejaDeSerie`), y `Barra.MaxBandeja` admite 0:
solo el `^`, que despliega todas, como los iconos ocultos de Windows. A quien se le
añade se le pone a 0; quien ya tenía la bandeja conserva lo suyo.
## La barra de Windows volvía a salir abajo (por la bandeja de serie)

Al poner la bandeja de serie, ManagedShell crea su propia `Shell_TrayWnd` y se
pone la primera en orden Z (es justo lo que hace que los iconos nos lleguen a
nosotros). Pero `BarrasDeWindows` y el auto-ocultar buscaban la barra con
`FindWindow("Shell_TrayWnd")`, que ahora devuelve LA NUESTRA. El vigilante de
150 ms miraba una ventana que ya estaba escondida, y cuando Explorer sacaba la
suya (abriendo un Calc, por ejemplo) se quedaba abajo a la vista. Ahora
`Nativo.BarraDeExplorer()` recorre todas las `Shell_TrayWnd` y se salta las de
nuestro proceso. Probado: se fuerza la de Explorer a visible y a los 400 ms ya
está escondida otra vez.

Y de paso el error del arranque: el `FullScreenHelper` de ManagedShell (lo monta
la bandeja) usa `System.Windows.Forms.Screen`, y sin `UseWindowsForms` esa DLL
no viene en el paquete: `FileNotFoundException` a los 3 s de arrancar. Se activa
WinForms en el proyecto y se le quitan los `using` globales para que no choquen
con los de WPF.

## Siempre a la vista, y esconderla a mano con el clic derecho

Con `OcultarConMaximizada` de serie la isla iba y venía cada vez que pasabas de
una ventana maximizada (un Calc, el navegador) a otra cosa: en el log salían
decenas de "se esconde / vuelve" en pocos minutos. Ahora viene apagado y la isla
se queda siempre. La pantalla completa de verdad (F11, un vídeo, un juego) la
sigue apartando, como antes.

Para quitarla de en medio cuando estorba: clic derecho → «Esconder la isla». Se
va de todas las pantallas a la vez (`_escondidaAMano` es estático) y solo vuelve
con la tecla Windows, que esa vez no abre el menú: solo la devuelve. Mientras
está escondida a mano, ni la pantalla completa ni el ratón en el borde la tocan.
La opción solo sale si la tecla Windows es nuestra (`Inicio.TeclaWindows`): sin
el hook no habría forma de recuperarla salvo cerrando NubeBar.

Límite conocido: con VMware Workstation delante, la tecla Windows no nos llega
(abre el Inicio de Windows) y tampoco devuelve la isla. VMware se queda la tecla
antes que nadie. Se probó a reenganchar nuestro hook en cada cambio de primer
plano para ponernos los primeros en la cadena, y no cambió nada: no es cuestión
del orden de los hooks. Se deja así; basta con cambiar a otra ventana.

## Las apps fijadas, a la vista con la isla recogida

Fijar una app ya servía para tenerla siempre en el carrusel, pero el carrusel vive
en la zona izquierda y esa zona **no existe con la isla recogida**: para llegar a tu
app había que acercar el ratón, esperar a que se desplegara y entonces pulsar. Tres
pasos para lo que en la barra de Windows es uno.

Ahora las fijadas tienen su propio módulo, `fijadas`, que viene de serie y se ve
siempre, recogida o no.

**Va en el centro, pegado al reloj, y no a la izquierda con el carrusel.** Es
geometría, no gusto: la isla crece y se encoge desde el centro, así que al
desplegarse la zona izquierda se desplaza unos 350 px (medido en una captura con la
config de aquí: la isla pasa de 120 px de ancho recogida a unos 850 expandida, y el
borde izquierdo se va de 1220 a 865). El icono se escapaba de debajo del cursor
justo cuando ibas a pulsarlo, que es la peor sensación posible. La columna del
centro es `Auto` entre dos estrellas iguales, o sea que está centrada pase lo que
pase: los iconos fijos no se mueven ni un píxel entre un estado y otro. Comprobado
con capturas de los dos estados.

**Un icono y no dos.** Con el módulo puesto, las fijadas salen del carrusel y viven
en la tira nueva; si lo quitas de Ajustes > Módulos, vuelven al carrusel como antes.
El botón es exactamente el mismo (`CrearBotonGrupo`), así que conserva los puntitos
por ventana, la rayita de la activa, la mini lista y el menú del clic derecho. Lo
único que no se hereda es el arrastrar para reordenar: en la tira fija el orden es
el de `Barra.AppsFijadas`.

**Cuidado con el orden de construcción:** los módulos se montan por zonas, izquierda
primero, y el carrusel reparte los botones en cuanto nace. Si la tira de las fijas
no existiera todavía, se los quedaría todos él. Por eso `PrepararFijas()` se llama
antes que `Llenar()`, y por eso `_tira`/`_carrusel` se ponen a null ahí mismo: si
quitas el módulo `apps`, el carrusel de la vez anterior ya no está en la isla y
seguir apuntando a él dejaba los iconos en un panel huérfano.

## Quince wifis no pueden convertir la isla en un muro

Tres cosas distintas, las tres arregladas:

**La isla se hacía enorme.** El panel mide lo que pide su contenido y nadie miraba
el tamaño de la pantalla. Ahora hay un tope, `AltoMaximoPanel()` (la pantalla menos
la isla y sus márgenes): lo que no quepa se mete en un `ScrollViewer` y se recorre
por dentro, en vez de estirar la isla hasta salirse del monitor. Vale para todos los
paneles, no solo el de red, y también para lo que llega tarde y lo hace crecer
(`ReajustarPanel`). Y la lista de wifis, aparte, enseña cinco de golpe y guarda el
resto para la rueda.

**La rueda no movía la lista.** La atendemos a mano (`PreviewMouseWheel` en el
propio `ScrollViewer`, marcando el evento) en vez de confiar en que llegue sola:
cada fila es un `Button` dentro de paneles que la isla también vigila, y basta con
que alguien se quede el evento para que no quede más remedio que arrastrar la barra
a pulso. Si la lista cabe entera no se toca el evento, que siga su camino.

**Tu red se perdía entre las de los vecinos.** El orden que da Windows es el suyo.
Ahora salen primero las que ya conoces (tienen perfil guardado, y van en negrita) y
dentro de cada grupo la que mejor llega. El título pasa a "Redes wifi · 12" cuando
hay más de las que caben, para que se note que hay que bajar.

Probado en este equipo con 4 redes a la vista y conexión por cable: el panel pide
217 px de alto y las redes salen ordenadas (la conocida primero aunque llegue al
27 %, y detrás 87 %, 36 % y 12 %). Lo que **no** se ha podido probar aquí es el
desplazamiento con más de cinco redes ni el tope de pantalla: no hay tantas redes
al alcance, y ningún panel llega a pedir los 1.362 px que caben en este monitor.

## Y las abiertas, igual que las fijadas

Mismo problema que con las fijadas, y la misma solución: el carrusel vivía en la
izquierda, que no existe con la isla recogida, así que para ver qué tienes abierto
había que acercar el ratón. Ahora el módulo `apps` se muda al centro, detrás de las
fijadas, y se ve siempre.

No hizo falta código nuevo: la zona del centro ya era la que no se esconde (es la
columna `Auto` del Grid, entre dos estrellas iguales), así que basta con mover el
módulo de zona. Eso es lo bueno de que esto sean módulos colocables. La migración
`AppsAlCentro` lo mueve **una vez** y respeta lo que decidas después; y si lo habías
quitado del todo, no te lo devuelve: `MoverUnaVez` solo mueve lo que ya está puesto.

El carrusel se lleva consigo todo lo suyo: `MaxApps`, la rueda con inercia, el
degradado de los extremos y el botón de la vista general con su "+N". O sea que
tener veinte ventanas abiertas no estira la isla sin fin: a partir de diez iconos,
rueda.

Lo que cambia, y hay que saberlo: **la isla en reposo ya no es pequeña**. Con dos
fijadas y una app abierta mide 218 px de ancho (antes 120, el mínimo), y respira
cada vez que abres o cierras algo, aunque con la misma animación de ancho de
siempre. Quien prefiera la isla mínima solo tiene que devolver `apps` (y `fijadas`)
a la izquierda desde Ajustes > Módulos.

Probado abriendo y cerrando el Bloc de notas con la isla recogida: el icono entra y
sale él solo. Ojo con el Bloc de notas de Windows 11 para probar estas cosas:
`Start-Process notepad` lanza un puente que se muere enseguida y deja el programa de
verdad corriendo como `Notepad` con otro pid, así que parecía que el icono se
quedaba colgado cuando lo que pasaba es que la ventana seguía abierta.

## Soltar la isla no es pulsar lo que tenías debajo

Síntoma: cogiéndola de ciertos sitios (el icono de la vista general, un icono de
app) la isla no hacía la animación de volver, se plantaba en su sitio de un salto.

Eran **dos fallos encadenados**, y el log los enseña seguidos:

```
21:01:52.764 vista general: 3 ventanas…
21:01:52.773 isla soltada en 54,643; se queda mientras sigas encima
21:01:52.784 panel abierto: vista (436x108)
```

**Uno:** al soltar, WPF le manda su `Click` al botón que tenías debajo del dedo
cuando empezaste. Cogerla por la vista general la abría, por un icono de app te
cambiaba a esa app (o la lanzaba, si estaba fijada y cerrada). La isla se mueve con
el cursor, así que el botón sigue debajo al soltar y el clic es "legítimo" para WPF.

**Dos:** abrir un panel recoloca la ventana (`AjustarVentana` → `ColocarEnMonitor`),
y ahí la posición solo se respetaba mientras `_arrastrando`. En cuanto soltabas,
cualquier recolocación —un panel, cerrar un panel, una recarga del config— la
devolvía a casa de un salto. El primer fallo disparaba el segundo todo el rato.

Arreglo:
- `ClicDeArrastre()`: un `Click` que es el final de un arrastre se ignora. Mira las
  dos banderas (`_huboArrastre`, `_llevaIcono`) y además una marca de tiempo con
  250 ms de margen, porque el arrastre se entera de que has soltado en el siguiente
  fotograma compuesto, que puede llegar *después* del Click. Vale también para el
  arrastre de iconos: después de reordenar tampoco quieres acabar en esa app. Y las
  banderas se limpian al empezar cada pulsación, no al acabarla, para no comerse el
  clic siguiente.
- `ColocarEnMonitor` respeta la posición mientras la isla esté fuera de sitio o
  volviendo, no solo mientras la llevas cogida: ahora la isla vuelve a casa por un
  único sitio, `AnimarVuelta`. Con la barra abajo se conserva el filo de abajo, que
  es el que no se puede mover (si no, abrir un panel la hacía bajar sola).
- `AnimarVuelta` vuelve a leer el tamaño en cada fotograma en vez de guardárselo al
  empezar: si por el camino se abre o se cierra un panel, ya no la estira o la
  encoge a la medida vieja.

Probado: se arrastra cogida del botón de la vista general y al soltar **no se abre
nada** (antes salía `panel abierto: vista` en el log). La vuelta se ve en
fotogramas intermedios (x≈1085 y x≈1175 camino de 1280), o sea que se anima de
verdad. Y con la isla fuera de sitio se abre un panel y se queda donde la dejaste
en vez de saltar a su sitio.

## Los iconos de bandeja que no hacían nada

Síntoma: algunos iconos de los ocultos ("^") no responden ni al clic ni al clic
derecho. Otros sí. La primera sospecha era de permisos (una app como administrador,
a la que Windows no nos deja mandar mensajes), y era **falsa**: se añadió una ficha
al log con el dueño de cada icono y los trece eran alcanzables.

Lo que lo delató fue otra columna de esa misma ficha:

```
Altavoces: 36%                 · pid 25192 · v4 · cb=0x0   · explorer.exe
Seguridad de Windows           · pid 37368 · v4 · cb=0x0   · SecurityHealthSystray.exe
NordVPN desconectado           · pid 12572 · v4 · cb=0x400 · NordVPN.exe
Dispositivos Bluetooth         · pid 25192 · v4 · cb=0x403 · explorer.exe
```

**`cb` es el mensaje con el que se le habla a la app dueña del icono, y en dos de
ellos vale 0.** Esos no son de ninguna app: son los iconos del sistema (el volumen,
la red, la batería, el reloj, el escudo de Seguridad), que la barra de tareas de
Windows dibuja y atiende por dentro; lo que se registra es solo el dibujo. Así que
no había a quién reenviarle el clic: ManagedShell mandaba el mensaje 0 (WM_NULL),
que es exactamente no hacer nada. Comprobado uno por uno: los de `cb` distinto de
cero abren su menú (HyperX, NordVPN, Steam, Bluetooth), y los dos de `cb=0` no
hacían nada, ni con un botón ni con el otro.

Se reconocen por su GUID, que no cambia con el idioma de Windows (aquí salen como
"Altavoces" y "Seguridad de Windows"), y ahora cada uno hace lo suyo: el volumen
abre nuestro centro de control, la red nuestro panel de red, la batería el suyo, el
escudo abre Seguridad de Windows y el reloj, la configuración de fecha y hora. Para
uno desconocido con `cb=0`, se lanza su ejecutable, que es lo que haría el clic
izquierdo de casi todos.

De paso, al reenviar un clic ahora se llama a `AllowSetForegroundWindow` también
con el botón derecho. ManagedShell solo lo hace con el izquierdo y el central, y
una app que contesta con su menú llama antes a `SetForegroundWindow` (es lo que
manda Microsoft para los menús de bandeja): sin ese permiso, Windows se lo niega y
el menú no sale.

## El buscador daba rutas que no existen

En el log aparecían siete intentos de abrir cosas que acababan en "el sistema no
puede encontrar el archivo", y todas tenían la misma pinta:

```
ERROR en abrir C:\Usuarios\Spcar\Documentos\ola\IP Switcher Setup 1.0.0.exe
ERROR en abrir C:\ProgramData\Microsoft\Windows\Menú Inicio\Programas\…
```

`C:\Usuarios`, `Documentos`, `Menú Inicio`: son los nombres **traducidos** que
enseña el Explorador. En disco esas carpetas se llaman `Users`, `Documents` y
`Start Menu`, siempre, sea cual sea el idioma de Windows. La consulta al índice
pedía `System.ItemPathDisplay`, que es justo eso: la ruta para enseñar.

La buena es `System.ItemUrl`. Comprobado contra el índice de este equipo:

```
mostrada : C:\Usuarios\Spcar\Documentos\ola\IP Switcher Setup 1.0.0.exe   (no existe)
itemurl  : file:C:/Users/Spcar/Documents/ola/IP Switcher Setup 1.0.0.exe  (sí existe)
```

Ahora se piden las dos: la traducida se sigue enseñando debajo del resultado
(es la que verías en el Explorador, y queda mejor), y la de verdad es la que se
usa para abrir, para sacar el icono y para "abrir la ubicación" — que por lo mismo
tampoco funcionaba: `Directory.Exists` decía que no sobre la ruta traducida y la
opción se iba por el desagüe sin decir nada.

`System.ItemUrl` viene como `file:C:/…` (y en un recurso de red, `file://servidor/…`),
y lo que no sea `file:` no es un archivo: el índice también guarda correo y
contactos, y ahí nos quedamos con lo que hubiera.

## La palabra "fijadas" escrita en la isla

El exe de la release v1.1.36 enseñaba la palabra `fijadas` suelta, en texto, justo
antes de los iconos de las apps fijadas.

No era un cartel ni un aviso: era el **nombre del módulo**. El selector de módulos
tenía de caso por defecto `_ => Etiqueta(() => nombre)`, o sea "si no sé lo que es,
lo escribo". Y pasó lo que tenía que pasar en cuanto el config y el exe se
desincronizaron:

- `Config.cs` migra el config solo y le añade `"fijadas"` al centro.
- La release v1.1.36 es el commit `112fc61`, **anterior** al `cfecbe7` que añadió
  el módulo `fijadas` al selector.

Resultado: un config escrito por una NubeBar nueva y un exe que no conoce la
palabra. El exe hacía lo que le habían dicho y la escribía.

Ahora un módulo desconocido **no se escribe**: se apunta una vez en el log y se
deja el hueco vacío. Escribir el nombre nunca fue una buena idea — no es un
mensaje para nadie, y un exe viejo con un config nuevo no es un caso raro, es
exactamente lo que pasa cada vez que alguien actualiza el config antes que el exe.

## Escribir "cmd" no encontraba la consola

Daba esto, del log de verdad:

```
ERROR en abrir C:\Usuarios\Spcar\Documentos\p5\node_modules\.bin\semver.cmd
```

Tres cosas a la vez:

1. **El menú Inicio no tiene la consola.** Y lo que sí tiene se llama en cada
   idioma de una manera: "Símbolo del sistema" en español, "Command Prompt" en
   inglés. En `simbolo del sistema` no hay ni una `c`, así que el parecido con
   `cmd` era cero; con `command prompt` salía 0.70 de subsecuencia.
2. **Los archivos ganaban.** `semver.cmd` *contiene* `cmd` → 0.9 × 0.8 = **0.72**.
   Más que la consola. Y la consulta al índice pedía las N más **recientes**, no
   las que más se parecen, así que lo que salía era el último `.cmd` que alguien
   hubiera tocado en un `node_modules`.
3. **Veinte filas.** Con veinte huecos que rellenar daba igual lo flojo que fuera
   un parecido: entraba.

Las tres, arregladas:

- Una tabla de **herramientas de Windows con alias** (`Fuentes.Ordenes`): cmd,
  powershell, taskmgr, regedit, services.msc… cada una con lo que uno teclea de
  verdad para buscarla. Escribir un alias **entero** vale 1.3, por encima del 1.0
  de un parecido perfecto: pedir algo por su nombre no es parecerse, es pedirlo.
  Solo entran las que existen en ese Windows (`gpedit.msc` no está en Home), y los
  accesos directos del menú Inicio que son la misma herramienta con otro nombre se
  quitan del índice, que si no salen dos consolas en seis filas.
- El índice de archivos se consulta pidiendo **seis veces** lo que cabe y se ordena
  aquí por parecido; se tiran las carpetas trastero (`node_modules`, temporales,
  papelera, cachés) y los archivos pesan 0.72, que casi siempre lo que buscas es
  el programa y no un fichero suelto.
- **Seis filas**, y con listón relativo: se tira lo que no llegue al 60 % del mejor
  resultado. Bajar el listón absoluto no valía, porque es el mismo que aguanta las
  erratas (`fotosop` → Photoshop, 0.52). Pero en relación al mejor sí se nota: con
  `cmd` quedan la consola y Git CMD, y se van las cuatro cosas que solo tenían una
  `c`, una `m` y una `d` desperdigadas por el nombre.

## La ventanita blanca que parpadeaba cada tanto

Aparecía una ventana pequeña un instante y se iba. Cazada con un vigilante que
enumeraba las ventanas visibles cada 15 ms y apuntaba las que nacían y morían:

```
20:03:33.224   29 ms   clase=ConsoleWindowClass
               titulo='KristjanSkutta.WallpaperEngine'
```

Una **consola** del cliente de Wallpaper Engine, 29 ms de vida, una cada **15
segundos**. El reloj `_wpTimer` pregunta cada 15 s por el wallpaper actual, y
`WallpaperActual` arrancaba `wallpaper32.exe -control getWallpaper` para
preguntárselo. Cuatro procesos por minuto, para siempre.

Y para nada: ese comando **no contesta** por la salida estándar (ya estaba escrito
dos líneas más abajo, en el comentario del apaño), porque el exe que se lanza es
solo un cliente que reenvía la orden a la instancia abierta. O sea que el proceso
se arrancaba, no devolvía nada, y la respuesta acababa saliendo igualmente del
`config.json` de Wallpaper Engine — el mismo fichero que ya vigila un
`FileSystemWatcher` para enterarse de los cambios.

`CreateNoWindow = true` no lo evitaba: eso suprime la consola que crea *Windows*
al arrancar el proceso, no la que el propio programa se monta por su cuenta.

Ahora se lee el `config.json` **primero**. Lanzar el cliente se queda de reserva
para la instalación donde ese fichero no se pueda leer, y en la práctica no se
lanza nunca. Comprobado: 75 segundos (cinco vueltas del reloj) sin un solo
`wallpaper32` ni `conhost`, y el título y la paleta del wallpaper se siguen
leyendo igual.

La lección no es sobre Wallpaper Engine: es que un sondeo periódico que arranca un
proceso se paga cada vez, y aquí el apaño que ya existía (leer el config) estaba
puesto DESPUÉS del camino caro en vez de antes.

## Un instalador, y lo que costó cerrar NubeBar desde fuera

El instalador (Inno Setup, `installer/NubeBar.iss`) es casi trivial: envuelve el
exe autónomo, lo pone en `%LOCALAPPDATA%\Programs\NubeBar` y, como `app.manifest`
no pide administrador, **no sale ninguna ventana de UAC**. Pedir admin para luego
no usarlo solo sirve para asustar.

Lo que no fue trivial fue cerrar NubeBar antes de tocar su exe. Tres intentos:

**1. `AppMutex`.** Parecía lo elegante: reusar el mutex que ya tiene
`Instancia.cs`. Pero `AppMutex` solo sabe PLANTARSE. Desinstalar con NubeBar
abierta daba código de salida 1 y no desinstalaba nada:

```
codigo de salida del desinstalador: 1
carpeta: True   lnk de arranque: True   procesos NubeBar vivos: 2
```

**2. `taskkill` desde el instalador.** Peor, y de dos maneras a la vez:

- Sin `/F` manda WM_CLOSE a "la ventana principal" del proceso. Las islas son
  `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`: no son la ventana principal de nadie, no
  las encuentra, **no cierra nada**. En el log no aparecía ni una línea de cierre.
- Con `/F` sí muere, pero `taskkill /IM NubeBar.exe` se lleva por delante también
  **al guardián**, que es el mismo ejecutable con otro papel — y era justo quien
  tenía que devolver la barra de tareas al morir la principal. Resultado medido:

```
barra de Windows -> visible=False autohide=True
```

Ni barra de NubeBar ni barra de Windows. El peor de los finales posibles, y por
un instalador.

**3. `NubeBar.exe --salir`** (`Salir.cs`), que es lo que hay. Quien sabe cerrar
NubeBar es NubeBar:

- Un evento con nombre (`Local\NubeBar.Salir`) que la que está en marcha escucha,
  y que cierra todas las islas con la jefa la última, porque es la que desmonta lo
  global. Que sean todas importa con dos pantallas: cerrando solo la jefa, la otra
  mantiene vivo el proceso (`ShutdownMode` es `OnLastWindowClose`).
- Si en 8 segundos no se ha ido, se mata — pero **de uno en uno y con un respiro
  entre medias**, para que al morir la principal el guardián tenga su momento de
  restaurar.
- Y pase lo que pase, al final comprueba `Nativo.BarraWindowsVisible()` y la
  devuelve él mismo si hace falta. Es el último recurso: no puede quedar en manos
  de que alguien más se acuerde.

Medido, con la barra de Windows escondida y NubeBar en marcha:

```
--salir: pedida la salida, esperando a que se cierre
guardián: cierre limpio, me voy sin tocar nada
cerrado. Barra de Windows visible=True
--salir: cerrada por las buenas          (1 segundo, 0 procesos vivos)
```

El instalador solo llama a eso. El `taskkill` se queda únicamente para actualizar
desde una versión anterior a `--salir`, y ni siquiera se fía de que `Exec` haya
ido bien: un exe viejo no entiende la bandera, ve que ya hay una NubeBar y se
cierra él sin tocar nada, que también es "ir bien". Quien dice la verdad es
`CheckForMutexes('Local\NubeBar.UnaSola')`: mientras el cerrojo exista, hay una
NubeBar viva.

## La mini lista de ventanas ya no es una losa de punta a punta

Pasar el ratón por un programa con varias ventanas abiertas (el Explorador, sin ir
más lejos) abría su mini lista, y lo que salía debajo de la barra era **un bloque
negro de lado a lado de la pantalla** para escribir dos títulos de ventana. Lo
mismo con el menú del clic derecho sobre un icono.

El motivo estaba escrito desde hacía tiempo en `AbrirPanel`: el cajón mide lo que
mide la isla, y un panel puede pedir `encoger` para que la isla se estreche hasta
su propio ancho, como hace el menú del clic derecho sobre la isla. Pero eso **no
vale para un panel que cuelga de un icono del carrusel**: al encoger, la isla se
recoge desde el centro, el icono se va de debajo del cursor, la isla cree que te
has ido y cierra el panel justo cuando ibas a usarlo.

Así que no se tocan las medidas, se toca lo que se DIBUJA. La isla sigue midiendo
igual — el icono no se mueve ni un píxel — y un `Isla.Clip` deja a la vista, por
debajo de la barra, solo una tarjeta redondeada:

```
union( fila de la barra entera , tarjeta del ancho del panel bajo su icono )
```

Tres detalles que no son opcionales:

- **La tarjeta se mete dentro de la barra** tanto como el mayor entre el radio de
  la isla y el suyo propio. Si solo se pegara por debajo, sus dos esquinas
  superiores redondeadas dejarían una muesca justo en la unión, que es donde tiene
  que parecer que la tarjeta nace de la isla.
- **La x se recalcula en cada fotograma, no se apunta al abrir.** La isla crece
  desde el centro: mientras se expande, el icono SE MUEVE dentro de ella. Con una x
  de antes, la tarjeta se quedaba a medio camino — y si la isla estaba recogida y
  era más estrecha que el propio panel, en la esquina. El `Isla.SizeChanged` dispara
  el recálculo, así que la tarjeta se despliega y se pliega pegada a su icono.
- **El recorte se quita al terminar de cerrar, no al empezar.** Quitándolo antes,
  la tarjeta desaparecía de golpe y lo que se veía plegarse era otra vez la losa.

De paso, el ancho deja de ser fijo (320 px para todo) y lo decide el título más
largo, entre 180 y 460. Y el recorte quita el ratón de lo que ya no se ve, que
también estaba mal: el hueco negro de los lados mantenía la lista abierta aunque
te hubieras ido con el cursor al otro extremo de la pantalla.

## Fase A · El reloj deja de ser un `case`

El encargo era acercar NubeBar a KDE Plasma. La pieza que lo bloqueaba todo no era
ninguna función que faltara, sino un `switch` de dieciséis casos en
`MainWindow.xaml.cs` que decidía qué es un módulo. Mientras el reloj fuera un `case`
y un widget de usuario fuera otra cosa, habría dos castas de widget y la de usuario
sería siempre la pobre: no se puede escribir un modo edición que mueva "cosas que
están en un switch".

Así que esta fase no añade nada que se vea. Cambia por dónde entran los módulos.

**Lo que se ha hecho.** Un contrato (`Widget.cs`), un registro (`RegistroWidgets.cs`)
y los dieciocho módulos de casa registrados como adaptadores en
`MainWindow.Widgets.cs`. Cada adaptador envuelve **la misma expresión** que tenía en
el `switch`, sin tocarla: el reloj sigue siendo `Etiqueta(() =>
DateTime.Now.ToString("HH:mm"), animar: true)`. Eso es lo que permite afirmar que no
hay cambio de comportamiento.

**Por qué el contrato expone tipos de WPF.** `IAnfitrionWidget` devuelve `Button` y
`TextBlock` a secas. Envolverlos en una abstracción propia habría sido una capa de
mentira: los widgets se dibujan con WPF y no hay un segundo backend al que mudarse.
Lo que sí hace el contrato es que un widget **no vea a `MainWindow` entera**: pide
colores, nivel de animación, un refresco por segundo y los cuatro ayudantes de
construcción, y nada más.

**Y por qué los de casa reciben `MainWindow` y no el anfitrión.** El carrusel habla
con los grupos de ventanas y la bandeja con el servicio de iconos: eso no cabe en el
contrato y no tiene sentido que quepa. `WidgetDeCasa` recibe la isla entera, y un
widget de fuera no podrá. Esa diferencia se deja **marcada** en el código en vez de
disimulada, porque es la que dice qué falta para que un widget de terceros pueda
hacer lo que hace el carrusel.

**Un widget roto ya no tumba la barra.** `CrearWidget` monta dentro de un `try`: si
uno revienta, se apunta y se deja el hueco vacío. Antes no hacía falta porque los
dieciséis módulos eran nuestros y un fallo ahí era un fallo que había que arreglar.
Con código que no escribimos nosotros, que un widget malo deje al usuario sin barra
de tareas no es una opción.

**Los glifos de los descriptores están comprobados contra la fuente**, no elegidos a
ojo. Los dieciocho (`U+E823` el reloj, `U+E787` la fecha, `U+E80F` el inicio… hasta
`U+E76F` el separador) existen en `SegoeIcons.ttf` (Segoe Fluent Icons, 2033
caracteres) **y también** en `segmdl2.ttf` (Segoe MDL2 Assets, 1833), que es el
respaldo que ya declara `MainWindow.FuenteIconos`. Medido con
`GlyphTypeface.CharacterToGlyphMap`, no supuesto.

Como ninguno falla, **no se ha añadido código de respaldo**: haría falta el día que un
widget de FUERA declare un glifo inventado, y el sitio de esa comprobación es quien lo
dibuje —la biblioteca de la Fase B—, no el descriptor. Meter hoy un `GlifoSeguro()`
que nadie llama sería la misma clase de código muerto que el `bool` sobrante de
`RegistroAjustes`.

Ojo: estos glifos **todavía no se pintan en ningún sitio**, son para la biblioteca. El
único que sí se dibuja hoy es el `U+E890` del botón de iconos del escritorio, que es
literalmente el que tenía el `switch`.

## Fase A · Tres colores no son un tema

Un tema eran `Fondo`, `Texto` y `Acento`. Con eso, cada trozo de la isla se apañaba:
el centro de control mezclaba a ojo para sus filas, la vista general aclaraba el
fondo por su cuenta y "atenuado" era un `Opacity = 0.6` escrito a mano en cada sitio.
Un widget de fuera no puede hacer eso: necesita preguntar "¿de qué color pinto una
fila?" y recibir una respuesta que valga en los seis temas de serie.

`Roles.cs` define los roles y, lo importante, **deriva los que el tema no dé**: las
superficies se sacan acercando el fondo al texto, y el texto atenuado acercando el
texto al fondo. Por eso los seis temas que ya existían siguen pintando exactamente
igual — no se les ha tocado ni un hex — y ganan roles gratis.

`Secundario` y `Sistema`, si el tema calla, **son el propio acento** y no un naranja
inventado: un tema que solo trae tres colores no debe estrenar colores que su autor
no eligió. Los semánticos (positivo, aviso, negativo) sí tienen color propio aunque
el tema calle, porque un "batería baja" tiene que seguir siendo rojo en Gruvbox.

**La regla de las tintas.** Un color de relleno y un color de texto no son lo mismo.
Cada acento tiene su pareja `…Tinta`, que es el mismo color pasado por
`Contraste.Legible` contra el fondo de verdad. Esta idea ya estaba en el código
(el `_cAcentoLegible` de `MainWindow`); aquí solo se ha generalizado.

## Fase A · Las dos erratas de la paleta del documento

La especificación da una paleta y pide, en el mismo documento, "contraste suficiente
en temas claro y oscuro". Las dos cosas no se pueden cumplir a la vez tal cual:

1. **"Texto principal `#172033`" sobre "fondo profundo `#101A2B`" da 1,1:1.** Es
   invisible. El `#172033` es el texto de las superficies CLARAS (`#F4F7FA`), no el de
   la isla, que es oscura. El tema `Nube` usa `#F4F7FA` como tinta, y el `#172033` se
   queda para cuando haya superficies claras de verdad.
2. **El coral `#E87562` sobre `#F4F7FA` da 2,7:1.** No sirve para texto. Sobre el
   fondo profundo da 5,9:1 y ahí sí. Así que el coral se usa como **acento** —relleno,
   filo, la rayita de la app activa— y cuando hace de texto sobre una superficie clara
   pasa por `Roles.TintaSobre`, que lo profundiza hasta 4,5:1.

Las dos quedan apuntadas aquí porque son correcciones **sobre el encargo**, no sobre
el código, y conviene que se sepa que son deliberadas.

## Fase A · `Paneles[]` se deriva, no se guarda

> **Esto ya no es así.** En B5 la autoridad se invirtió: manda `Paneles[0]` y `Barra` es
> la vista derivada. Ver «`PanelNube` manda de verdad» al final del documento.

El modelo nuevo es una lista de paneles, con los cuatro bordes y los cuatro modos de
visibilidad de Plasma. Pero en esta fase **`Barra` y `Modulos` siguen siendo la
verdad** y `Paneles[0]` se rellena a partir de ellos al cargar
(`Config.SincronizarPaneles`).

No se escribe en disco a propósito: es una vista, no un dato. Si se guardara, el
config tendría la misma información en dos sitios y acabarían discrepando — y el
primero que editara `config.json` a mano se encontraría con que cambiar `Barra.Alto`
no hace nada porque manda `Paneles[0].Grosor`.

Lo nuevo (el bucle que monta los widgets, y mañana el modo edición) lee de `Paneles`.
Cuando existan paneles de verdad se le da la vuelta a la autoridad y es `Barra` la que
se deriva, sin tener que tocar a quien lee.

**Los laterales están en el modelo y no se dibujan todavía.** `Posicion` acepta
`"izquierda"` y `"derecha"` porque el documento los pide, pero la isla los trata como
`"arriba"`. Mejor eso que colocar la barra mal y en silencio.

**Y los tres `StackPanel` del XAML siguen ahí.** De ellos cuelgan el ancho de la isla,
la expansión y la entrada escalonada. Cambiarlos por un contenedor genérico en la
misma tanda que el contrato de widget era meter dos refactores en uno. Lo que ya es
colección es el RECORRIDO: quien rellena no sabe cuántas zonas hay ni cómo se llaman.

## Fase A · El nivel de animación se aplica en dos sitios, no en treinta

Hay unas treinta animaciones repartidas por el proyecto. Para que "completo,
equilibrado y reducido" funcione no hace falta repasarlas: todas pasan por `Kf` (las
de números) y por `Ms` (las de color), así que el tiempo se escala ahí.

Funciona porque el código ya cumplía la regla de la casa: **el valor de verdad de
cada propiedad se fija ANTES de animar, y la animación solo dibuja el camino**. Con
el factor a cero no hay camino que dibujar y todo aparece ya en su sitio, sin saltos
ni restos. Esa invariante, que estaba escrita para que un fallo no dejara la isla a
medias, es la que hace que el modo reducido salga casi gratis.

Los adornos puros —el icono que se levanta, el latido de una app que arranca, el
destello del borde— no tienen estado que conservar, así que en "reducido" **ni se
lanzan** (`Animacion.Adorno`), en vez de hacerlos instantáneos, que marearía igual.

**`auto` viene de serie y pregunta a Windows** (`SystemParameters.ClientAreaAnimation`):
quien ya ha pedido menos movimiento en Accesibilidad no tiene que volver a pedirlo
aquí, que es lo que pide el documento sobre respetar la reducción de movimiento.

**Lo que NO cubre.** Las animaciones de la plantilla del botón viven en el XAML
(`MainWindow.xaml`): el fundido del hover y el encogido del 82 % al pulsar. No pasan
por `Kf`, así que el nivel reducido no las apaga. El fundido es opacidad y no molesta;
el encogido al pulsar sí es movimiento y queda pendiente.

## Fase A · Cambiar de tema solo escribía cuatro claves

Encontrado al meter los roles: `VentanaAjustes.AplicarTema` escribía `Fondo`, `Texto`,
`Acento` y `Radio`, y nada más. Con roles eso tiene dos consecuencias malas: elegir
`Nube` no traería su naranja ni su azul, y salir de `Nube` hacia Gruvbox **dejaría el
naranja coral puesto para siempre**, sin que nadie entendiera de dónde sale.

Arreglado con `ConfigArbol.PonerOQuitar`: un rol que el tema no trae se escribe
**borrando la clave**, porque ausente significa "derívalo" y null no.

De paso, la recarga de la página de colores usaba `_pestanas.Items[1]` —un número
escrito a mano—. Ahora pregunta al registro por el id `"colores"`: con las páginas en
un registro, el orden puede cambiar y ese `1` habría recargado la equivocada.

## Fase A · La lista de módulos de Ajustes estaba escrita dos veces

`VentanaAjustes` tenía su propia lista de dieciséis nombres a mano. Consecuencia real:
`musica-caratula` y `musica-barras` funcionaban en la isla y estaban documentados en
el README, pero no se podían elegir desde Ajustes porque se olvidaron en esa lista.

Ahora sale de `RegistroWidgets.Ids`. Registrar un widget basta para que aparezca en
los tres sitios: la isla, la lista de Ajustes y la biblioteca que vendrá.

## Fase B · El 82 % que no obedecía, y en qué se diferencian Reducido y Desactivado

La Fase A dejó apuntado un cabo suelto: el encogido al 82 % al pulsar un botón, y el
fundido del fondo al pasar el ratón, vivían en los storyboards de `MainWindow.xaml` y
no pasaban por `Kf`, así que el nivel de animación no los tocaba. En "reducido" el
botón seguía encogiéndose igual.

**Por qué no bastaba con escalar la Duration.** Un storyboard escrito dentro de un
`ControlTemplate` se congela cuando la plantilla se aplica: su `Duration` ya no se
puede cambiar, ni con `DynamicResource`. La única salida era construir el estilo desde
código, que es lo que hace ahora `MainWindow.Botones.cs`. Se vuelve a montar en cada
`AplicarTema`, que es también cuando se vuelven a crear los botones, así que cambiar el
nivel en caliente se ve en el acto. Hay precedente en la casa: `VentanaAjustes` ya
construía su `EstiloPestana()` igual.

**Geometría y fundidos son cosas distintas.** Mover, crecer y encoger obliga a medir y
redibujar en cada fotograma; un cambio de opacidad solo mezcla. Por eso ahora hay dos
factores en vez de uno:

| Nivel | Geometría | Fundidos | Encogido al pulsar | Rebote |
|---|---|---|---|---|
| Completo | ×1,0 | ×1,0 | sí | sí |
| Equilibrado | ×0,6 | ×0,6 | sí | **no** |
| Reducido | **×0** | ×0,25 | **no** | no |
| Desactivado | ×0 | **×0** | no | no |

Y aquí está lo que de otro modo habría sido un fallo: **si "reducido" y "desactivado"
compartieran factor, serían el mismo nivel con dos nombres.** La diferencia real es
que Reducido conserva el fundido del hover, muy corto (37 ms), para que al pasar el
ratón por un botón siga habiendo respuesta sin que nada se mueva; y Desactivado no
tiene ninguno. Más aún: en Desactivado el disparador del ratón **no usa un storyboard
de cero milisegundos**, usa un `Setter`. Así no queda ninguna transición que mirar, que
es literalmente lo que se ha pedido, y además se puede demostrar leyendo el árbol de
disparadores en vez de cronometrando.

El rebote (`BackEase`) se va ya en Equilibrado, antes que el encogido: es la parte que
más se nota de las dos, porque el botón se pasa de largo y vuelve.

**La regla de la casa se mantiene**, y aquí hizo falta decirla explícitamente: las
animaciones del estilo llevan `FillBehavior.HoldEnd` para que, pase lo que pase, la
propiedad se quede en su valor final y no vuelva sola al de la plantilla.

## Fase B · El separador, que era el widget más tonto y tenía tres fallos

Una rayita vertical entre grupos. No hace nada más, y precisamente por eso nadie lo
había mirado: estaba escrito en una línea dentro del registro de widgets y tenía tres
cosas mal que solo salen cuando alguien lo pone de verdad.

**1. No decía cómo alinearse.** Un `Border` nace con `VerticalAlignment.Stretch`, y con
un `Height` fijo eso deja la posición en manos de cómo WPF resuelve un «estírate» que
no se puede cumplir. Funcionaba de casualidad mientras todos sus vecinos de zona
midieran lo mismo. Ahora lo dice: `Center`.

**2. Su alto era un `16` escrito a mano** que no sabía nada de `Barra.Alto`. Con la
barra fina la raya ocupaba casi toda la altura; con la barra gruesa era un muñón. Y lo
peor: si algún vecino hacía la fila más alta que `Barra.Alto`, la raya se centraba en
la FILA y no en la isla, y el `ClipToBounds` de la isla se le comía un trozo. Ahora
sale de la altura de la barra (`Alto × 0,42`, acotado), con `MaxHeight` además de
`Height` para que no pueda pasarse aunque alguien ponga un `Alto` raro.

Los números: 38 → 16 px (exactamente lo de siempre, así que a quien ya lo tuviera
puesto no le cambia nada), 24 → 10 px, 56 → 24 px.

**3. Su color era `_texto` con `Opacity = 0.3`**, otra mezcla a ojo de las que quitó la
fase B1. Un separador es un borde: le toca el rol `Borde`. De paso se vuelve temable —
en Nube es azulado (`#33415C`) en vez de un gris del texto.

Y `SnapsToDevicePixels` + `UseLayoutRounding`, porque una línea de 1 px que cae entre
píxeles con DPI no entero sale gris y borrosa en vez de nítida.

**Medido, no supuesto.** Con la barra en modo barra y dos separadores puestos a la vez,
midiendo los píxeles de la captura:

| `Barra.Alto` | isla | raya | centro raya | desvío |
|---|---|---|---|---|
| 24 | y 8..31, centro 19,5 | 10 px | 19,5 | 0 |
| 38 | y 8..45, centro 26,5 | 16 px | 26,5 | 0 |
| 56 | y 8..63, centro 35,5 | 24 px | 35,5 | 0 |

Las dos rayas en cada caso, dentro de la isla y sin recorte. Y lo mismo con los cuatro
niveles de animación y con los temas negro, Nube, Gruvbox y Nord: dos rayas de 16 px
centradas en 26,5 en los siete casos.

**Lo que no pude reproducir.** El informe original decía «demasiado abajo y parcialmente
cortado». Con `Alto = 38` en modo barra la raya sale centrada y entera, también antes
del arreglo. Lo que sí es cierto es que la alineación dependía de la casualidad y el
alto no respetaba la barra, que son las dos condiciones que producen exactamente ese
síntoma en cuanto una zona crece más que la fila. Queda arreglado el mecanismo aunque
no haya podido ver el caso concreto.

## Fase B · El modo edición, y las tres cosas que me enseñó esta isla

El modo edición de la isla: entras desde el clic derecho, la isla se queda abierta,
cada widget se ve como un bloque y se puede ocultar, quitar, mover entre zonas y
añadir desde la biblioteca. Al salir, todo vuelve a ser exactamente lo de antes.

Lo interesante no es el modo. Son los tres fallos que salieron por el camino, porque
los tres son del tipo que se repite.

### 1. Las apps fijadas desaparecían: la tira huérfana

**Qué pasaba.** A veces, al entrar o salir del modo edición, las apps fijadas del
Centro se esfumaban.

**Por qué.** `ModuloFijas()` no construye nada: devuelve una instancia cacheada,
`_tiraFijas`, y se protege de que el módulo esté puesto en dos zonas a la vez así:

```csharp
if (_tiraFijas!.Parent != null) { /* "puesto más de una vez" */ return new StackPanel(); }
```

El cromo del modo edición mete cada widget dentro de un `Grid`. Al remontar las
zonas, `destino.Children.Clear()` quita el `Border` del cromo de la zona — **pero ese
`Grid` sigue existiendo, huérfano, y sigue siendo el `Parent` de la tira**. La guarda
lo tomaba por un duplicado y devolvía un panel vacío.

**La raíz, que era mía.** `Editar()` llamaba a `LlenarZonas` a secas. En el camino
normal, `AplicarTema` hace antes un preámbulo que yo me salté, y que importa:
`PrepararFijas()` crea una tira NUEVA (con lo que el `Parent` huérfano deja de
existir), `_tira` y `_carrusel` se ponen a null, y `_actualizadores` se vacía.

**El arreglo.** Un único punto de montaje, `RemontarZonas()`, con el preámbulo
completo, llamado tanto por `AplicarTema` como por `Editar`. **Nadie llama ya a
`LlenarZonas` por su cuenta**, y ese es el punto: el fallo no fue olvidar una línea,
fue que había dos formas de montar las zonas y solo una era correcta.

De paso se cerró una fuga que nadie había visto: `_actualizadores` no se limpiaba al
entrar y salir de edición, así que cada ciclo dejaba otra tanda de refrescos
apuntando a elementos muertos.

Y la regla de qué se dibuja se sacó a `ZonasWidgets.ADibujar(ids, ocultos, editando)`:
estática, compartida y sin interfaz, para poder comprobarla sin abrir una ventana.
Son las veinte comprobaciones de las seis reglas (entrar no toca el config, los no
ocultos se dibujan siempre, los fijados mantienen su sitio, 25 ciclos sin perder
nada, ids duplicados).

### 2. La captura de ratón no sirve en esta ventana

El arrastre de widgets no empezaba. El log lo dejó claro: llegaban el `down` y los
`move`, **el `up` no llegaba nunca**.

La isla vive con `WS_EX_NOACTIVATE` y no toma el foco jamás. En esas condiciones la
captura de WPF se pierde en silencio y el enrutado del `MouseLeftButtonUp` no es de
fiar.

Y la respuesta estaba en el fichero de al lado: **`MainWindow.Arrastre.cs` —el
arrastre de la isla, que funciona desde hace meses— no usa captura ni eventos de
soltar.** Lee el botón con `Nativo.BotonIzquierdoPulsado()` desde un bucle de
`CompositionTarget.Rendering`. El arrastre de widgets hace ahora lo mismo.

La lección no es sobre WPF: **antes de inventar un mecanismo, mirar si el proyecto ya
resolvió ese problema.** Lo había resuelto, y mejor.

Hubo además una regresión propia de esto: un manejador de `LostMouseCapture` que
llamaba a limpiar el estado. Como la captura se toma en la fase TÚNEL y cualquier
`ButtonBase` bajo el puntero toma la suya en la fase BURBUJA —justo después—, el
estado se borraba entre el `down` y el primer `move`. Ya no hay captura que perder.

### 3. Un Border con Opacity 0 sigue recibiendo clics

El velo del cromo —el fondo que se enciende al pasar el ratón— cubría el bloque
entero con `Opacity = 0`. En WPF eso **no** lo hace transparente al ratón: seguía
comiéndose los clics, y el único sitio por donde agarrar un widget era el filo del
borde. Lleva `IsHitTestVisible = false`.

Lo mismo con los botones de acción: estaban **centrados**, justo donde uno agarra el
bloque. Ahora van al borde derecho y solo reciben clics cuando el ratón está sobre su
bloque.

### Lo que el modo edición NO hace

- **No llama a `Aplicar()`**: eso rehace la appbar, la barra de Windows y el atajo
  global, y entrar a colocar widgets no tiene por qué tocar nada de eso.
- **No guarda si no cambia nada**: soltar un widget donde ya estaba devuelve `false`
  en `Mover()` y no se reescribe el fichero. Sin eso, un arrastre sin efecto
  reescribía el config, disparaba la recarga en caliente y la isla parpadeaba entera.
- **No pide el teclado para la biblioteca**: el modo ya lo tiene activo, y si el panel
  lo pidiera por su cuenta, `CerrarPanelAlRecargar` lo apagaría al cerrarse y Esc
  dejaría de sacarte del modo edición.

### Las zonas se eligen por columna, no por contenido

El Grid de la isla es `* | Auto | *`, pero `Izq` se alinea a la izquierda y `Der` a la
derecha: **cada StackPanel mide lo que miden sus widgets, no lo que mide su columna**.
Decidir la zona de destino por el contenido dejaba la mitad de la isla sin dueño, y
soltar ahí cancelaba el arrastre. Ahora manda la columna, que sí llena la isla — y de
paso una zona vacía acepta igual que una llena.

La marca de destino tampoco es hija del `StackPanel`: se pinta en un `Canvas` por
encima. Siendo hija empujaba a los demás widgets, que al moverse cambiaban los puntos
medios, que cambiaban el índice: temblaba. Nada se reordena hasta que sueltas.


## Fase B · `PanelNube` manda de verdad, y la autoridad se invierte en el límite de carga

En la Fase A `Paneles[]` se escribió como una **vista derivada** de `Barra`: nacía de
ella en cada carga y no se guardaba. Era un modelo de mentira — el config seguía
siendo `Barra` y el panel, un adorno. B5 le da la vuelta.

### Por qué en el límite de carga y no sitio por sitio

`Barra.Modo`, `Posicion`, `Alto`, `Margen`, `ReservarEspacio` y `Monitores` se leen en
**64 sitios repartidos por 11 ficheros**: `EsIsla`, `Arriba`, el alto de la isla, la
appbar, el arrastre, la pantalla completa, el recorte del cajón, las animaciones de
entrada, la colocación en cada monitor… Cambiarlos todos a `PanelPrincipal.X` en la
misma tanda que el modelo nuevo era pedir dos refactores a la vez, y el segundo tocaba
cosas que llevan meses funcionando.

Así que la autoridad se invierte **en el único sitio por el que pasa todo**:

```csharp
internal static bool MigrarYDerivarPaneles(Config c)   // Paneles[0] manda
{
    var p = c.Paneles.FirstOrDefault(x => x.Id == "principal");
    if (p == null) { p = new PanelNube { … desde c.Barra … }; migrado = true; }
    …validar Posicion y Visibilidad…
    c.Barra.Modo = p.Modo;   c.Barra.Posicion = p.Posicion;
    c.Barra.Alto = p.Grosor; c.Barra.Margen  = p.Margen;   // y las otras dos
}
```

Los 64 lectores no se han tocado ni uno. Lo que leen sigue llamándose `_cfg.Barra.Alto`
y vale lo mismo, solo que ahora **viene** del panel. Y las seis claves se **borran** de
`Barra` en el fichero (`GuardarPanelesMigrados`): con las dos puestas, el primero que
editase `Barra.Alto` a mano no entendería por qué no pasa nada.

### La misma información en dos sitios, otra vez

Al probar la migración sobre el config de verdad apareció el error clásico: `PanelNube`
tiene una propiedad `Zonas`, y al serializar el panel se escribían **las tres listas de
widgets también dentro de `Paneles`**, duplicando `Modulos`. Al primer cambio en la
Biblioteca las dos copias dejaban de coincidir — exactamente el problema por el que se
estaba quitando la geometría de `Barra`.

`[JsonIgnore]` en `PanelNube.Zonas`. Las zonas siguen viviendo en `Modulos`, que es lo
que edita la Biblioteca y lo que el usuario reconoce en su config; `Zonas` es una
**vista** de aquellas listas —las mismas instancias, no copias— que rellena
`MigrarYDerivarPaneles`. Hay una prueba que lo comprueba con `ReferenceEquals`.

### Los cuatro bordes: lo que se puede hacer hoy y lo que no

> **B6 cambió esto.** La barra vertical ya existe: un lateral en modo barra es una barra
> vertical de verdad, no un «se coloca arriba». Ver «La barra vertical» al final.

`arriba` y `abajo` ya estaban y valen en los dos modos. Los laterales **anclan la isla**
a ese lado sin dejar de ser horizontal: es una colocación, no un giro.

Una **barra vertical de verdad** —zonas apiladas, el cajón saliendo de lado, el menú
Inicio de 860 px cabiendo ahí, la appbar con `ABE_LEFT`— es otra fase. Hacerla a medias
dejaría la nube rota, así que en modo barra los laterales se avisan en el log una vez y
se tratan como `arriba`. Es feo, pero es honesto y queda escrito en el log de arranque:

```
panel[principal] modo=barra borde=arriba (pedías izquierda) … alineación=Stretch
```

### Los cuatro modos de visibilidad, y el que no existía

`siempre` y `ventanas-encima`/`por-debajo` son orden Z y se resuelven con `Topmost` y
dos llamadas nuevas (`Nativo.AlFondo` → `HWND_BOTTOM`, `Nativo.NoSiempreEncima` →
`HWND_NOTOPMOST`).

`autoocultar` era el interesante: **no estaba implementado**. La primera versión se
limitaba a llamar a `RevisarPantallaCompleta()`, que solo esconde la isla si hay algo a
pantalla completa — o sea, no hacía nada. La maquinaria de recogerse y asomar por el
borde ya existía y está probada; lo que faltaba era que tuviera **dos motivos** en vez
de uno:

```csharp
public static (bool Toca, string Motivo) TocaRecogerse(
    string? visibilidad, bool ocultarEnPantallaCompleta, bool hayPantallaCompleta)
{
    if (AutoOculta(visibilidad)) return (true, "autoocultar");
    if (ocultarEnPantallaCompleta && hayPantallaCompleta) return (true, "pantalla completa");
    return (false, "");
}
```

Lo de abajo no distingue cuál ha sido: le basta saber si toca estar recogida. Así el
asomar con el ratón en el borde, el no esconderse con un panel abierto y el quedarse
asomada si tienes el puntero encima valen igual para el modo nuevo **sin repetir una
línea**. El flag pasó a llamarse `_recogida` porque ya no es "escondida por pantalla
completa". El motivo solo va al log.

### Dos cosas que solo se ven al probarlo

- **Reservar hueco para algo que no está.** Una barra con autoocultar registraba la
  appbar igual, así que Windows le guardaba su franja y las ventanas maximizadas no la
  usaban: justo lo que uno quiere evitar al encender el autoocultar. Ahora
  `!EsIsla && ReservarEspacio && !PanelAutoOculta`. En Plasma pasa lo mismo.
- **La animación de entrada deshacía la recogida.** `AnimarAparicion` hace caer la isla
  desde fuera con rebote y **deja `_mover.Y` en 0**. Con autoocultar encendido,
  `AplicarVisibilidad` ya la había recogido antes, y la caída la devolvía a la vista
  marcada como recogida: se quedaba visible y no volvía a esconderse hasta el siguiente
  cambio. Ahora, si nace recogida, su sitio es fuera y no se anima la entrada.

### Las decisiones viven en `PanelNube`, no en la ventana

`BordeEfectivo`, `CuelgaDeArriba`, `PorEncima`, `AutoOculta`, `AlFondoDelTodo`,
`TocaRecogerse` y `PuedeRecogerse` son estáticas y puras. `MainWindow.Visibilidad.cs`
solo las traduce a Win32 y a WPF. Dos ventajas: la tabla entera (4 bordes × 2 modos y 4
visibilidades × pantalla completa) se prueba **sin abrir una ventana**, y hay un único
sitio donde está escrito qué significa cada modo, en vez de cuatro ifes repartidos.

### Lo que queda fuera

- Barra **vertical** de verdad (zonas apiladas): es B6.
- `ventanas-encima` y `por-debajo` **se pelean con el servicio de bandeja**, que manda
  la nube por encima cada 100 ms. Se avisa en el log una vez y no se apaga la bandeja
  por la espalda, que sería peor sorpresa.
- Varios paneles a la vez: el modelo es una lista y `PanelPrincipal` ya es una
  propiedad de la isla, pero hoy todas dibujan el mismo `"principal"`.


## Fase B · La barra vertical, o cómo girar una nube sin escribir una segunda

En B5 un borde lateral en modo barra se avisaba en el log y se trataba como «arriba»,
porque media barra vertical es peor que ninguna. B6 es la otra mitad.

### La idea que lo hizo manejable

La tentación era escribir una barra vertical: otro XAML, otra colocación, otro cajón,
otro carrusel. Eso son dos nubes que hay que arreglar dos veces.

Lo que de verdad cambia entre las dos es **qué eje es el grosor y qué eje es el largo**.
La nube ya sabía vivir en una franja fija —eso es el modo barra horizontal— y ya sabía
que un panel tiene un grosor. Así que no hay una segunda nube: hay una docena de sitios
donde se pregunta `Vertical` y se cambian las dos medidas de sitio.

El caso más claro es `ColocarEnMonitor`. Recibía «el alto de la ventana». Hoy recibe **el
grosor de la franja con sus márgenes**, que es el mismo número; lo que cambia es contra
qué eje se mide. Los que ya llamaban —abrir un panel, un cambio de DPI, recargar el
config— siguen llamando igual y la barra vertical sale colocada sin tocarlos.

### Las zonas, apiladas, siguen siendo las mismas tres

El Grid de las zonas pasa de tres columnas (`* Auto *`) a tres **filas** con las mismas
proporciones, y los `StackPanel` de horizontal a vertical (`OrientarZonas`). Las zonas
conservan su significado: Izquierda es «al principio» —arriba—, Centro es el medio y
Derecha es «al final» —abajo—. Es lo que hace Plasma, y así los widgets, la Biblioteca y
el modo edición siguen hablando de las mismas tres zonas sin enterarse de nada.

`OrientarZonas` va **antes** de `RemontarZonas`, porque los widgets se construyen según
la orientación: el separador es una raya a lo ancho o a lo alto, el carrusel se apila o
se tumba. Montarlos primero dejaba la mitad girada.

El sitio de cada zona en el Grid se pone en código y no en el XAML. Con `Grid.Row`
escrito en el XAML, en horizontal las tres zonas pedían filas que no existen y lo que
las salvaba era que WPF recorta un índice fuera de rango. Funcionaba por casualidad.

### Tres cosas que tienen eje propio

- **El separador.** Separa en la dirección en la que se apilan los widgets, así que en
  vertical la rayita va tumbada: las dos medidas y los dos márgenes cambian de sitio.
- **El carrusel.** Es una tira más larga que su hueco, desplazándose por dentro de una
  ventanita. Cinco accesos (`EjeTira`, `Desplazado`, `PonerDesplazado`, `LargoCarrusel`,
  `PonerLargoCarrusel`) son los únicos sitios que saben de qué eje hablan; la rueda, la
  inercia, el desvanecido de los extremos y el traer a la vista la app que se acaba de
  activar están escritos **una sola vez** y valen para los dos.
- **El arrastre entre zonas del modo edición.** `ZonaEn` decidía por columna del Grid;
  ahora por columna o por fila. El punto medio de cada bloque, la marca de destino y el
  fondo de cada zona se miden con `ALoLargo`/`ALoAncho`, que es la misma cuenta con los
  ejes cambiados. Sin esto, editar una barra vertical habría puesto cada widget en la
  zona equivocada.

### Los paneles no caben dentro: salen al lado

En horizontal el panel es el «cajón»: la isla crece de alto y el panel se despliega
desde dentro, como una sola superficie con un saliente. En vertical eso **no se puede**:
la barra ya va de arriba abajo y no tiene hacia dónde crecer.

Así que el panel sale como una **tarjeta aparte**, pegada al lado de la barra, que es
exactamente lo que hace Plasma — y encima es más sencillo que el recorte con
`CombinedGeometry` de la horizontal.

La tarjeta vive dentro de la **misma ventana**, que se ensancha para dejarle sitio. Una
ventana nueva tendría que resolver otra vez el no robar el foco, el orden Z, el DPI y el
guardián, y todo eso ya está resuelto aquí. Lo que sí hubo que añadir es que un clic
dentro de la tarjeta no cuenta como «clic fuera»: `VigilarClicFueraDelPanel` solo miraba
la isla, y el primer clic dentro del propio panel lo cerraba.

Su aritmética (`AnchoConTarjeta`, `SitioDeLaTarjeta`) está **fuera de la ventana y es
pura**, por un motivo concreto: el clic que abre un panel no se puede automatizar sin
riesgo de escribir en la ventana de otro programa. Lo que se comprueba a mano es que la
tarjeta aparece; lo que se comprueba en frío es que aparece **donde** toca, incluidos los
casos de borde (un widget de arriba, uno de abajo, una tarjeta más alta que la pantalla).

### La appbar, con los cuatro bordes

`AppBar.Registrar` recibía un `bool arriba`. Ahora recibe el borde de Windows
(`ABE_LEFT=0`, `ABE_TOP=1`, `ABE_RIGHT=2`, `ABE_BOTTOM=3`), que decide
`PanelNube.BordeAppBar` a partir del modo y del borde del panel, y `grosorPx` es lo que
se reserva en la dirección que toque.

Un detalle que no es obvio: el `ABM_QUERYPOS` puede mover el filo que **no** fijamos
nosotros —si ya hay otra appbar en ese lado, Windows nos aparta—, así que al volver hay
que clavar otra vez el grosor contra el filo exterior y solo entonces hacer `ABM_SETPOS`.
Eso ya estaba bien para arriba y abajo; ahora está escrito para los cuatro.

Y hay una asimetría a propósito: una **isla** anclada a un lateral, si reservara hueco,
lo reservaría por su franja horizontal, no por el lado. Estar pegada a un lado no la
gira. (Hoy no reserva ninguna isla, pero la regla está escrita donde se decide.)

### Las cuatro visibilidades, sin una línea de más

`Deslizar` movía la isla en Y. Ahora mueve en X o en Y según la orientación, y lo que la
aparta es **su propio grosor** y no el alto de la ventana (con un panel abierto la
ventana es mucho más grande que la franja). `RatonEnLaFranja` y el nuevo `RatonEnElFilo`
son la misma cuenta con los ejes cambiados.

Con eso, `autoocultar` y la pantalla completa funcionan en los cuatro bordes sin que
quien los decide (`PanelNube.TocaRecogerse`, de B5) sepa nada de la orientación. La
visibilidad y el borde son independientes a propósito, y hay ocho pruebas que lo fijan.

Al cambiar de orientación en caliente, el eje que ya no se usa vuelve a cero. Sin eso, la
barra se quedaba apartada en una dirección que nadie iba a deshacer.

### Lo que NO hace, y por qué

- **El menú Inicio de 860 px no está rehecho para una columna.** Sale como tarjeta
  flotante al lado, con su ancho de siempre, y se desplaza por dentro si no cabe de
  alto. Un menú pensado para vertical —la lista de apps en una columna, el buscador
  arriba, las secciones apiladas— es trabajo posterior y se decidió dejarlo fuera.
- **La barra vertical no se aparta con el ratón.** No es un olvido: es una franja
  reservada pegada a su lado, como la barra de Windows, y toda la maquinaria de
  apartarla y devolverla (`CasaDeLaVentana`, `AnimarVuelta`, el filo que no se puede
  mover) está escrita en el eje de la horizontal. Media función se vería como un fallo;
  esto se ve como una decisión.
- **Un widget más ancho que el grosor se recorta.** La barra recorta a sus bordes
  (`ClipToBounds`), y con un grosor de 38 px un texto largo no cabe. Lo que se ve en una
  columna estrecha son iconos y cifras cortas; los widgets de texto largo piden un
  grosor mayor. Formatos específicos para vertical —el reloj en dos líneas, por
  ejemplo— son trabajo posterior.
- **Un solo panel.** `Paneles` es una lista desde B5 y `PanelPrincipal` ya es una
  propiedad de cada isla, pero hoy todas dibujan el mismo `"principal"`: no se puede
  tener una barra vertical a la izquierda y una isla arriba a la vez.


## El clic derecho en un resultado de la búsqueda

Hasta ahora el clic derecho sobre una fila del buscador subía hasta la isla y abría **su**
menú: cerrar aplicaciones, editar la nube, salir de NubeBar. Nada de eso tiene que ver
con la aplicación que tenías debajo del puntero, y «Salir de NubeBar» a dos píxeles de
donde esperabas «Abrir ubicación» es la clase de sorpresa que hace que dejes de fiarte
de un clic derecho.

Ahora cada resultado saca el suyo: **Abrir**, **Ejecutar como administrador**, **Abrir
ubicación del archivo**, **Propiedades** y **Cancelar**.

### Por qué no es un `ContextMenu` ni un panel

Un `ContextMenu` de WPF sería una cajita blanca del sistema que no pega con nada, y en
una ventana `WS_EX_NOACTIVATE` con el foco siempre a medias.

Y no puede ser un panel de la isla, que es como están hechos los otros menús: los
paneles son de uno en uno, así que abrirlo **cerraría la búsqueda**, que es justo lo que
estabas usando. Por eso es una capa dentro del propio panel del menú Inicio, por encima
de la lista y de la portada, con un fondo transparente que se come los clics de fuera
(un `Background` nulo los dejaría pasar a la lista y el menú no se cerraría al hacer clic
al lado).

La tarjeta se **mide antes de colocarse**: puesta a pelo donde pulsaste, un clic derecho
en la última fila deja medio menú fuera del panel y el recorte del cajón se come las
opciones de abajo.

### Lo que no se puede hacer sale apagado, y dice por qué

El menú no cambia de forma según la aplicación: las cuatro opciones están siempre, y las
que no aplican salen en gris con el motivo en el tooltip. «Está en gris» sin decir por
qué solo frustra.

| | se activa cuando |
|---|---|
| Abrir | siempre |
| Ejecutar como administrador | es un fichero **y** es ejecutable (`.exe .lnk .bat .cmd .com .msi .msc .ps1`) |
| Abrir ubicación | es un fichero que está donde dice |
| Propiedades | igual |

Una app de la Store (`shell:AppsFolder\…`) no es un fichero suelto: lo único que se puede
hacer con ella es abrirla, y las otras tres salen apagadas. Un `.txt` del índice de
archivos tiene ubicación y propiedades pero no se eleva: «ejecutar como administrador» un
documento no significa nada, y ofrecerlo sería mentir.

### La elevación la pide Windows, y punto

`Ejecutar como administrador` usa `UseShellExecute = true` con `Verb = "runas"`, que es
exactamente lo que hace el Explorador: **el que saca el UAC es Windows**. Aquí no hay —ni
va a haber— ningún atajo para saltárselo.

Si el usuario dice que no, Windows devuelve `ERROR_CANCELLED` (1223). Eso **no es un
fallo**: es una respuesta. Se apunta en el log como «elevación cancelada en el UAC» y no
se reintenta ni se busca otra vía.

### El detalle que habría dejado la opción muerta: los accesos directos

`ShellExecute` con `runas` **sobre un `.lnk` no eleva**. El shell abre el acceso directo
y el proceso de destino nace sin elevar, así que el UAC no sale y la opción parece no
hacer nada. Y el índice de aplicaciones está lleno de `.lnk`: es lo que hay en el menú
Inicio de Windows.

Así que un `.lnk` se resuelve primero y lo que se eleva es su **destino**, con sus
**argumentos**. Sin los argumentos, un acceso directo con parámetros —un
`chrome.exe --profile-directory=…`— arrancaría otra cosa distinta de la que pulsaste.

Se resuelve por COM tardío con `Shell.Application`, que es lo que ya usa el índice para
enumerar la carpeta de la Store: nada de declarar `IShellLink` ni de añadir una
referencia a Windows Script Host solo para esto. Si no se puede resolver, se avisa en el
log y se intenta con el `.lnk` tal cual, que es lo peor que puede pasar: que Windows lo
abra sin elevar.

### Propiedades necesita bajar a `ShellExecuteEx`

Esta no se puede hacer con `Process.Start`. El verbo `properties` necesita que el shell
tenga la lista de IDs del elemento (`SEE_MASK_INVOKEIDLIST`); sin ella, `ShellExecute` no
encuentra el verbo y no pasa nada en absoluto.

El diálogo se abre **sin dueño** a propósito: la nube vive como `WS_EX_NOACTIVATE`, y un
diálogo colgado de una ventana que no se activa puede quedarse detrás de todo.

### Lo que sigue igual

- **El clic izquierdo abre, como siempre.**
- **El menú de la isla no se ha tocado**, y ya no lo abre un clic derecho en una fila:
  el manejador marca el evento como atendido y no deja que suba.
- **Las ventanas ya abiertas tampoco cambian**: esas tienen su menú en su icono del
  carrusel (`MenuDeGrupo`), con cerrar, forzar el cierre y fijar, que es lo que se
  espera de algo que ya está en marcha.
- Un cálculo, un comando, un ajuste de Windows o la búsqueda en la web **no sacan menú**
  —no tienen nada que ofrecer— pero tampoco dejan subir el clic a la isla.
- Con el menú abierto, `Esc` lo cierra a **él** y no el buscador entero; las flechas,
  `Intro` y `Tab` se comen, que actuarían sobre una lista que está detrás de la tarjeta.
- Al cerrarse el buscador se olvidan las tres referencias de la capa: si no,
  apuntarían a elementos que ya no están en ningún árbol y `MenuDeAppAbierto` seguiría
  diciendo «sí».


## Fase C · El menú Inicio al lado de una barra vertical

B6 dejó la barra vertical con sus paneles saliendo en tarjeta al lado. El menú Inicio
—que es el panel más grande y el único con foco de teclado, lista desplazable y su
propio menú contextual dentro— entraba por ese mismo camino, y por eso parecía que ya
funcionaba. No funcionaba: tenía un fallo que lo habría dejado inusable y dos medidas
que no se habían pensado para un hueco estrecho.

### El fallo: la captura del ratón solo conocía la isla

El menú Inicio no se cierra al perder el foco —Wallpaper Engine se lo devuelve a sí
mismo cada pocos segundos y lo cerraba mientras escribías—, sino con **un clic de verdad
fuera**. Eso se consigue capturando el ratón en el subárbol de la isla, que es como se
comportan los menús de Windows:

```csharp
Mouse.Capture(Isla, CaptureMode.SubTree);
```

En horizontal es correcto: el panel **es** el cajón de la isla y vive dentro de ella. En
vertical el panel es una tarjeta **hermana** de la isla, y eso rompía el menú de dos
formas a la vez:

1. la captura en subárbol **desvía a la isla** los clics que caen fuera de su subárbol,
   así que la caja de búsqueda y las filas de resultados no recibían ni uno;
2. y además el comprobador los contaba como «clic fuera», porque medía contra el
   rectángulo de `Isla`. O sea: **el primer clic en el buscador lo cerraba.**

Se captura en `Raiz`, que es el ancestro común de las dos superficies, y el «¿estoy
dentro?» pasó a ser una sola función que pregunta por las dos:

```csharp
FrameworkElement SuperficieDeClics => Vertical ? Raiz : Isla;

bool DentroDelMenu(Point enPantalla)   // la barra Y su tarjeta
```

Había **tres** sitios que preguntaban solo por la isla: el manejador de la captura, el
vigilante de 150 ms que caza el clic cuando Windows nos quita la captura, y el rearmado
que la recupera (ese volvía a capturar en `Isla` aunque se hubiera cogido en `Raiz`, con
lo que el fallo reaparecía al primer clic). Los tres pasan por la misma función.

Y la captura se **suelta de donde se cogió**, no de donde toque ahora: si la orientación
cambiara con el menú abierto, quitar los manejadores de la otra superficie los habría
dejado puestos para siempre.

### Las medidas: 660 px no caben en todas partes

En horizontal el menú tiene el ancho entero del monitor para él y 660 px siempre caben.
Al lado de una barra vertical hay que descontar la franja de la barra, su margen y la
separación — y una tarjeta más ancha que ese hueco **no se recorta**: se queda fuera de
la ventana y simplemente no se ve, porque la tarjeta vive dentro de la misma ventana que
la barra.

Así que `AnchoMaximoDeTarjeta` es la inversa exacta de `AnchoConTarjeta` (hay una prueba
que las compone y comprueba que vuelve a salir el ancho de la pantalla), y el menú pide
`Math.Min(660, lo que quepa)`. En un 2560 con barra de 38 quedan 2490 y pide 660; en un
1024 al 150 % con barra de 72 quedan 579 y se estrecha a 579.

Esto hay que mirarlo **en unidades de WPF y no en píxeles**: ese mismo 1024 al 150 % son
683 de WPF. Hecha la cuenta en píxeles, los 660 habrían «cabido» y la tarjeta habría
salido media pantalla fuera.

El alto, igual: `AltoMaximoDeTarjeta` descuenta los dos márgenes, y las dos cuentas salen
del monitor **de esa isla**, así que un portátil al lado de un 2560 da dos tarjetas
distintas sin que ninguna mire la del otro.

### Recortar no es envolver

`AbrirPanelFlotante` ya sabía qué hacer con un panel que no cabe: envolverlo en un
`ScrollViewer`. Para el menú Inicio eso es lo peor de los dos mundos — la caja de
búsqueda se iría hacia arriba al desplazar la lista, y habría **dos** barras de
desplazamiento, la de fuera y la de la lista.

Pero el panel ya se desplaza por dentro: su raíz es un Grid de tres filas con la lista en
la fila elástica. Lo que necesita es que le **recorten el alto**; de lo demás se encarga
él. De ahí el `scrollPropio` de `AbrirPanel`, que solo mira el camino vertical: el
horizontal lo ignora a propósito, para que el menú de siempre se comporte exactamente
igual que antes de C1.

### La tarjeta sale donde está el botón

El menú salía centrado en la pantalla. Con una barra vertical y el botón «⊞» arriba
—zona Izquierda es arriba— lo natural es que la tarjeta salga a su lado, como el lanzador
de Plasma. Para eso hacía falta una referencia al botón, que se creaba en una lambda
anónima del registro de widgets; ahora es `ModuloInicio()` y se guarda.

Si el módulo «inicio» no está puesto en ninguna zona, la referencia se queda en null y la
tarjeta se centra, que es la salida razonable. Y un botón arriba deja la tarjeta pegada
al margen de arriba, no medio fuera: eso ya lo hacía el recorte de `SitioDeLaTarjeta`.

### Lo que no se ha tocado

- **El menú Inicio horizontal**, ni una línea de su comportamiento: mismas medidas
  (`AnchoMenuEfectivo` devuelve 660 tal cual), mismo cajón, misma captura en la isla,
  mismo envolver-en-scroll si no cabe.
- **La búsqueda, las categorías, las fijadas y el menú contextual de aplicaciones** no
  se enteran de la orientación: viven dentro del panel y el panel es el mismo objeto.
  El menú contextual se coloca contra la raíz del panel, que en vertical es la tarjeta,
  así que se recorta contra ella sin saberlo.
- **El sistema de panel único**: la tarjeta sigue siendo *el* panel abierto. No hay dos.
- Y sigue pendiente, a propósito, **rehacer el menú de 860 px para una columna**: hoy
  sale como tarjeta ancha al lado, con su diseño horizontal de siempre.


### Corrección de C1 · el panel pasa a ser una ventana superpuesta

La prueba visual de C1 encontró dos cosas en modo barra vertical: el panel Inicio salía
**cortado** y quedaba **por detrás de la aplicación activa**. Las dos tienen la misma
raíz, y la raíz es de diseño, no un despiste.

#### Lo que estaba mal de fondo

La tarjeta del panel era un hermano de la isla **dentro de la misma ventana**, y la
ventana se ensanchaba de 54 a 730 px para dejarle sitio. Eso ataba el panel a dos cosas
que no son suyas:

1. **A que la ventana siguiera ancha.** Cualquiera que recoloque la ventana con el grosor
   de la BARRA —no el del panel— la devuelve a 54 px, y entonces la tarjeta no se
   recorta: se queda fuera de la ventana y desaparece. Y en modo barra esa ventana es
   además una **appbar registrada**: el shell se cree con derecho a recolocarla cuando
   recalcula el área de trabajo, que es justo lo que pasa al cambiar de posición, al
   esconder la barra de Windows o al salir de pantalla completa.

2. **A que el orden Z de la barra le valiera.** Y no le vale: la barra puede estar en
   `ventanas-encima` o `por-debajo` **a propósito**, y un lanzador abierto tiene que
   verse por encima de todo igual. Dentro de la misma ventana no hay forma de que uno
   esté encima y el otro no — eso es, literalmente, el segundo fallo.

#### Lo que se ha hecho

El panel vive en su propia ventana (`VentanaSuperpuesta`), sin borde, transparente,
fuera de Alt+Tab, **dueña la isla** —si fuera suelta mantendría viva la aplicación al
cerrar la isla, porque `ShutdownMode` es `OnLastWindowClose`, y NubeBar no terminaría
nunca—. Y:

- **el rectángulo se calcula en coordenadas de pantalla**, en píxeles, y se recorta
  contra el área útil del monitor. Ya no se pone un `Margin` dentro de otra ventana
  confiando en que la ventana sea bastante grande;
- **la tarjeta se estira para llenar la ventana**: el rectángulo en píxeles es la
  autoridad y el contenido se adapta a la escala que WPF le dé en ese monitor. Al revés
  —fijar el contenido en unidades de WPF— es lo que se descuadra al cambiar de pantalla;
- **se eleva mientras está abierto** (`SetWindowPos` con `HWND_TOPMOST`; `Topmost` solo
  no basta si otra ventana topmost se puso después) y al esconderse deja el orden Z como
  estaba, **sin tocar el de la barra**;
- **la barra ya no cambia de tamaño** al abrir un panel. Esa es la parte que mata el
  primer fallo de raíz: no hay ningún ancho que se pueda perder;
- el **teclado** lo pide la ventana del panel, no la isla: el buscador vive dentro de
  ella. Al cerrar devuelve el foco a donde estaba.

Y no es un `Popup` de WPF porque un Popup coloca y dimensiona en unidades de WPF —lo que
no sirve con dos monitores a distinto DPI, el mismo motivo por el que la isla se coloca
con `SetWindowPos`— y su orden Z lo decide él. Aquí hay que mandar en los dos.

#### La captura del ratón ya no cruza ventanas

En C1 se capturaba en la raíz para que el clic de la tarjeta llegase. Con la tarjeta en
otra ventana eso **no puede funcionar**: la captura de WPF no cruza ventanas. Así que en
vertical no se captura nada y el clic de fuera lo caza el vigilante de 150 ms, que
trabaja en coordenadas de pantalla y ya sabía apañárselas cuando Windows nos quita la
captura. El «¿estoy dentro?» pregunta por el **rectángulo de pantalla** de la ventana del
panel, no por un hit-test dentro de la isla.

#### Lo que medí, y lo que no pude medir

Honestamente: **no conseguí reproducir los dos síntomas con instrumentación.** Puse una
terminal maximizada delante, le di el primer plano y abrí el lanzador desde el código,
apuntando los rectángulos reales y preguntando a `WindowFromPoint` quién gana cada
píxel; con el C1 de entonces la tarjeta salía en su sitio y ganaba el píxel del centro.

De paso aprendí que **mi propia sonda era mala**: probé las cuatro esquinas, y las
esquinas de una tarjeta redondeada en una ventana por capas son píxeles transparentes,
así que `WindowFromPoint` devuelve —con razón— lo que hay detrás. Daba «tapada» siempre.
Con cinco sondas por dentro (centro y 20/80 % de cada eje) el resultado es inequívoco.

Lo que sí quedó medido, y es lo que importa: con la barra en `por-debajo` o
`ventanas-encima` —es decir, con la ventana de la barra **sin** TOPMOST— el panel gana
sus cinco sondas por encima de un `cmd` maximizado. **Eso antes era imposible por
construcción**, y encaja con lo que viste: durante la sesión estuviste cambiando de modo
de visibilidad, y en cualquiera de esos dos el panel quedaba debajo de la terminal, que
además lo tapaba en parte.


### B6 · El texto en una columna, y quién decide si se reserva hueco

Dos cosas que solo se ven usando la barra vertical de verdad: la hora salía cortada, y
la barra se comportaba como una barra de tareas en vez de flotar sobre las ventanas.

#### 1. El margen estaba en el eje equivocado

`Etiqueta()` le ponía a todo texto `Margin(6,0,6,0)`. En una barra horizontal eso es la
separación entre módulos y está bien. En una columna de 38 px se come **12 px de los
38**, y la cuenta sale justo al revés de lo que hace falta:

```
grosor 38 → útil 36   "00:05" mide 34 + 12 de margen = 46   → cortado
grosor 48 → útil 46                                     46   → justo al límite, cortado igual
grosor 56 → útil 54                                     46   → cabe, por los pelos
```

Por eso subir el grosor no lo arreglaba: hasta ~56 no empezaba a caber, y `RAM 7,8 GB`
no cabía ni a 72. Lo que lo cortaba era el `ClipToBounds` de la barra, que es el peor
resultado posible: no avisa, no se puede leer y parece que falta un dígito.

Ahora el margen va en el eje en el que se separan los módulos (`0,6,0,6` en vertical) y
eso solo ya libera los 12 px.

#### 2. Partir un texto es cosa de quien sabe lo que significa

Con el margen arreglado, `00:05` cabe en 38 por 2 px. Eso no es un diseño, es suerte.

Así que cada etiqueta declara **cómo se escribe apilada**, y lo dice el widget y no un
sitio común: por dónde se parte un texto depende de lo que significa, y adivinarlo
(¿por el espacio?, ¿por los dos puntos?) sale mal al primer caso raro.

| | horizontal | vertical |
|---|---|---|
| reloj | `00:05` | `00` / `05` — como el panel de Plasma |
| fecha | `dom 25 oct` | `dom` / `25 oct` |
| CPU | `CPU 19%` | `CPU` / `19%` |
| RAM | `RAM 56%` | `RAM` / `56%` |
| batería | `BAT 85%` | `BAT` / `85%` (parte por el espacio más centrado) |

Y para lo que no tiene formato propio —el título del wallpaper, que es texto libre— hay
tres redes puestas en la propia etiqueta: **parte** por donde pueda (`TextWrapping`),
**nunca mide más** que la columna (`MaxWidth`), y si aun así no cabe —`100%` no se puede
partir— **se encoge** la letra hasta que quepa. Encoger se lee; recortar, no.

El `muestra` que reserva el ancho del texto más largo solo se aplica en horizontal: en
una columna, lo que no sobra es justamente ancho.

#### 3. Un bool no sabía distinguir "lo quiero" de "venía puesto"

`ReservarEspacio` valía `true` de serie, y **en modo isla no hace nada** (la isla nunca
reserva). Llevaba ahí sin efecto desde siempre. Al pasar a barra vertical se activó solo
y convirtió la nube en una barra de tareas que nadie había pedido: Windows le guardaba
la franja y las ventanas se apartaban en vez de pasar por debajo.

El problema no era el valor, era que **no se podía saber si alguien lo había elegido**.
Ahora son tres estados:

```
null  → no lo has elegido: decide la orientación
true  → lo has elegido: resérvala, como la barra de tareas
false → lo has elegido: no la reserves, que las ventanas pasen por debajo
```

Y el defecto por orientación es el que se espera de cada forma: una barra **horizontal**
reserva su franja como cualquier barra; una **vertical** no, porque lo que se espera de
una barra lateral es que flote por encima. La migración desde un config viejo escribe
`null` a propósito: aquel `true` era el valor de serie, no una decisión.

Lo que no se ha elegido **no se escribe** en el fichero. Un `null` se leería igual, pero
dejarlo escrito convierte "no lo he elegido" en algo que parece una decisión al abrir el
config a mano. Tocar el interruptor en Ajustes es lo que lo convierte en elección, y
entonces manda sobre el defecto — en los dos sentidos.

#### Medido

Los once textos de la barra, en los cuatro grosores (38, 48, 56, 72) y en los dos lados:
**0 se salen** en los ocho casos. Y el cuadro de la reserva, leyendo el área de trabajo
que Windows publica:

```
isla/arriba                         (0,0)-(2560,1440)   no reserva
barra/arriba        (sin elegir)    (0,54)-(2560,1440)  reserva   ← horizontal intacto
barra/izquierda     (sin elegir)    (0,0)-(2560,1440)   no reserva ← las apps pasan por debajo
barra/izquierda + ReservarEspacio:true  (54,0)-(2560,1440)  reserva ← la opción sigue estando
barra/izquierda + autoocultar       (0,0)-(2560,1440)   no reserva
```

#### Y el defecto acabó siendo "no reservar" en los cuatro bordes

La primera versión del tri-estado hacía que el defecto dependiera de la orientación:
horizontal reserva, vertical no. Era copiar lo que hace Plasma, donde un panel reserva
su franja de serie.

Está mal para esta nube, y se ve en cuanto la usas con la barra arriba: las ventanas se
apartan y queda una barra de tareas más. **Esto no es un panel de Plasma, es una isla que
flota**, y eso vale igual estirada de lado a lado que recogida en el centro. Reservar
hueco empuja las ventanas de todo el escritorio, que es una decisión demasiado grande
para tomarla por el usuario solo porque haya elegido el borde de arriba.

Así que el defecto es **no reservar, en los cuatro bordes**. El tri-estado no cambia:

```
null  → no lo has elegido: la nube flota, siempre
true  → lo has elegido: resérvala, como la barra de tareas
false → lo has elegido: no la reserves
```

`ReservaPorDefecto` dejó de ser una función con dos parámetros y pasó a ser una
constante. No es cosmético: una función que recibe el modo y el borde y no los mira
invita a pensar que todavía hay una regla escondida ahí dentro.

Dos reglas que siguen por encima de tu elección, y las dos son de antes:

- **El autoocultar nunca reserva**, aunque lo hayas encendido: guardar sitio para algo
  que no está es justo lo que se quiere evitar al autoocultar.
- **La isla no reserva nunca**, la elijas o no. Flota encima de las ventanas, que es lo
  que es.

Medido, los doce casos del cuadro más los cuatro de las excepciones:

```
null   arriba/abajo/izquierda/derecha   área (0,0)-(2560,1440)   libre
true   arriba                           área (0,54)-(2560,1440)
true   abajo                            área (0,0)-(2560,1338)
true   izquierda                        área (54,0)-(2560,1440)
true   derecha                          área (0,0)-(2506,1440)
false  los cuatro                       área (0,0)-(2560,1440)   libre
true + autoocultar                      área (0,0)-(2560,1440)   libre
true + modo isla                        área (0,0)-(2560,1440)   libre
```

Los cuatro bordes de la appbar quedan así probados contra el área de trabajo que publica
Windows, cada uno recortando por su lado.


## Fase C2 · La escena de entrada

La Fase A dejó escritas en el config cuatro claves —`Animaciones.Robot`, `RobotVisto`,
`RobotAlCambiarTema`— y un desplegable en Ajustes con un comentario honesto al lado:
«la escena del robot todavía no existe». C2 es escribirla.

### Lo que resuelve, que no es decorativo

Arrancar NubeBar es feo durante segundo y medio: Explorer recalcula el área de trabajo,
la barra de Windows se esconde (tarda unos 0,7 s), las islas se montan, el carrusel se
llena a trompicones. La pintura tapa exactamente ese rato, y lo que se descubre al
levantarla es el escritorio ya terminado. Por eso la escena **espera a que la interfaz
avise** en vez de durar un tiempo fijo: si el arranque se alarga, el negro se alarga con
él.

### El robot

Dibujado con primitivas y original a propósito: nada que se parezca a una mascota
conocida ni a la marca de nadie. Es **una nube con cuerpo de máquina** — el contorno del
cuerpo es literalmente la silueta de una nube, la cara es una visera oscura con dos ojos
del **color de acento del usuario**, lleva una antena con su bombilla encendida y, en
vez de patas, un propulsor, porque una nube no anda.

Se dibuja a partir de un solo número, el alto, y todo lo demás sale en proporción: cabe
igual en un portátil de 768 que en un 4K, y al 150 % de DPI no hay nada que tocar.

El pincel va a su **izquierda**, no a su derecha. Parece un detalle tonto y no lo es: el
robot cruza hacia la derecha y la pintura queda detrás, así que el pincel tiene que estar
del lado de lo ya pintado. Con el pincel delante parecía que pintaba por donde todavía no
había pasado.

### Dos cosas que solo se ven mirando el dibujo

Esta fase se pudo revisar sin sacar nada por pantalla: la composición está en un método
aparte que devuelve el lienzo y un `Avanzar(t)`, así que se puede dibujar **fuera de
pantalla** y guardar fotogramas en PNG. Mirándolos aparecieron dos fallos que ninguna
prueba de lógica habría cazado:

1. **Un extremo redondeado sobresale medio grosor del punto final.** El trazo mide
   1,35 × el alto de la pantalla, o sea que su casquete asoma ~730 px por delante de
   donde cree el código que está el filo. Consecuencia: el trazo nacía ya tapando media
   pantalla, y el robot iba setecientos píxeles por detrás de su propia pintura.
2. **Las "cerdas" en gris se leían como dos barras** cruzando la pantalla, no como
   textura. En negro —del mismo negro— no se ven como líneas: se ven como que el filo de
   la pintura no es limpio, que es lo que distingue un pincel de un barrido.

### Por qué se dibuja fotograma a fotograma

El trazo, las dos cerdas y el robot podrían ser cuatro animaciones de WPF en paralelo. No
lo son: van de un solo `Avanzar(t)` colgado de `CompositionTarget.Rendering`. El motivo
es la sincronía. Cuatro relojes independientes se separan en cuanto el sistema va
cargado — y «el sistema cargado» es, literalmente, el momento en que esto se ejecuta.
Con un solo avance por fotograma no hay forma de que el pincel se despegue de la pintura.

### El segundo y medio que se perdía

La primera versión montaba la escena en `ContentRendered`, que parecía lo correcto: así
se mide la ventana de verdad. Costaba **segundo y medio**. Justo detrás de crear la
escena, `App` llama a `MainWindow.Arrancar()`, que monopoliza el hilo de interfaz hasta
que termina; hasta entonces no hay ni un fotograma. La escena empezaba tarde y se perdía
justamente el rato que venía a tapar: medido, revelaba a los 3645 ms en vez de a los
~2400 de diseño.

Se monta en el constructor, con las medidas del monitor en unidades de WPF (píxeles entre
su escala), que es exactamente lo que WPF le va a dar a esa ventana ahí. No hace falta
esperar a nadie para saberlo.

### Las reglas que no se negocian

- **Nunca dejar la pantalla negra.** Tres redes: el revelado se dispara aunque la
  interfaz no avise (a los ~5,4 s; hoy lo que arranca igual es la pasada, ver más
  abajo), un temporizador cierra la ventana pase lo que pase a
  los 9 s, y `App` la cierra si `MainWindow.Arrancar()` revienta o si salta una excepción
  sin capturar. Si algo falla, se cae del lado de «no hay escena», nunca del de «pantalla
  negra».
- **Ni foco ni clics.** `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT`: no se
  activa, no sale en Alt+Tab y **los clics la atraviesan**. Mientras dura, el escritorio
  responde igual que sin ella.
- **Se puede saltar.** Esc o un clic la revelan al instante. Como la ventana no recibe
  entrada —por el punto anterior—, se mira el teclado y el botón a pelo cada 60 ms, que
  es el mismo truco que usa el arrastre de la isla.
- **Una sola vez.** Se crea en `App.OnStartup` y en ningún otro sitio: recargar el tema,
  los paneles o los widgets no la vuelve a sacar. Medido: tres remontajes de zonas, una
  sola escena.
- **`--salir` la cierra primero**, antes que las islas. Si se cerrara la última isla con
  la escena abierta, se quedaría sola en pantalla y en negro.
- **Se apunta como vista DESPUÉS de verla**, no antes: si reventara a mitad, la próxima
  vez vuelve a intentarlo en vez de quedarse sin estrenar para siempre.

### Los cuatro niveles

> Estos tiempos son los del primer diseño. La sección «La escena, tres veces del revés»
> —más abajo— cuenta en qué acabaron y por qué; la tabla de ahí es la buena.

| | robot | pintar | negro | revelar |
|---|---|---|---|---|
| Completo | sí | 1150 ms | 1000 ms | 460 ms |
| Equilibrado | sí | 700 | 620 | 280 |
| Reducido | **no** | 200 | 260 | 180 |
| Desactivado | — | no se crea la escena |

Los tiempos no pasan por `Animacion.Escalar` como el resto del proyecto, y es a
propósito: esto no es una animación de la isla, es una escena con tres tiempos que tienen
que guardar una proporción entre ellos. Un factor único los estiraría mal.

### Lo que falta

El modo `al-cambiar-tema` del desplegable **se comporta hoy como `primera-vez`**: sale al
arrancar si no la has visto. El disparo por cambio de tema necesita un gancho en
`AplicarTema` que distinga un cambio de color de verdad de una recarga cualquiera del
config, y meterlo en esta tanda habría tocado código ya verificado por un extra.

## Fase C2 · La escena, tres veces del revés

> La composición que se describe aquí —la brocha— **ya no es la que hay**: se sustituyó
> entera por «La construcción de la nube», más abajo. Las lecciones de esta sección
> siguen valiendo enteras; la brocha no.

La escena de arriba funcionaba y se veía mal. Lo que vino después fueron tres tandas
seguidas sobre el mismo archivo —el tirón, la secuencia, la composición— y cada una tiró
abajo una suposición de la anterior. Vale la pena dejarlas escritas porque las tres
fallaban por el mismo motivo de fondo: **confundir lo que se dibuja con cuándo se
dibuja**.

### 1. Iba a 3 FPS, y no era el dibujo

Síntoma: entrecortada. Primera hipótesis mía, y era **falsa**: el trazo se rehacía en
cada fotograma —una línea de 1458 px de grosor a la que se le movía el punto final— y
rasterizar eso entero sobre toda la pantalla tenía que ser lo caro. Se reescribió como
una figura estática movida con un solo `TranslateTransform`. Medido antes y después:
**exactamente los mismos 5 fotogramas**. La hipótesis no valía nada.

Lo que sí era: **`CompositionTarget.Rendering` solo salta cuando su `Dispatcher` está
libre**. Y justo detrás de crear la escena, `App` llama a `MainWindow.Arrancar()`, que se
queda con el hilo de interfaz durante todo el arranque. La escena no iba lenta: **no se
dibujaba**. Cinco fotogramas en 1339 ms, o sea 3,0 FPS, y los cinco eran los huecos que
dejaba el montaje de la nube.

El arreglo es dónde vive, no qué dibuja: la escena se abre en **su propio hilo de
interfaz** (STA, `IsBackground`, su `Dispatcher.Run()`, `InvokeShutdown()` al cerrarse).
Su `Dispatcher` no tiene nada más que hacer, así que `Rendering` salta en cada
presentación aunque el hilo principal esté hasta arriba.

Medido después, en una pantalla de 165 Hz (periodo 6,06 ms):

| | antes | después |
|---|---|---|
| fotogramas | 5 en 1339 ms | 371 en 3111 ms |
| FPS efectivos | **3,0** | **118,9** |
| frame p50 | — | 6,27 ms |
| frame p95 | — | 14,47 ms |
| con tirón (> 1,5 periodos) | — | 26,2 % |
| presentaciones saltadas | — | 147 |

**No son 165 FPS sostenidos y no conviene decir que lo sean.** La mediana va clavada al
refresco (6,27 contra 6,06), pero uno de cada cuatro fotogramas pierde al menos una
presentación, y eso es esperable: esto corre *mientras* arranca el escritorio entero. El
resumen de una línea que escribe cada ejecución en el log —fotogramas, FPS efectivos
contra los Hz de verdad, p50/p95/peor, cuántos con tirón y cuántas presentaciones
saltadas— se ha dejado permanente justo por esto: si algún día vuelve a ir a tirones, lo
primero que hace falta es el número, no una impresión.

Dos cosas más que se midieron y **no** se quedaron: `BitmapCache` sobre el grupo de la
pintura salió neutro (4018 × 1458 = 23 MB de mapa de bits para rellenar negro, que es lo
más barato que hay) y se quitó; sobre el robot sí se queda, que lleva dos desenfoques y
se mueve entero en cada fotograma.

### 2. Fluidez, velocidad y calidad son tres cosas

La corrección anterior destapó una confusión que merece su párrafo, porque es la que
lleva a «animaciones a 60 FPS» mal hechas:

- **fluidez** = cuántos fotogramas salen. La pone la pantalla.
- **velocidad** = cuánto dura la escena. La pone el guion.
- **calidad** = cuánto se dibuja en cada fotograma. La pone el dibujo.

A 240 Hz tiene que haber **más fotogramas suaves, no una escena más rápida**. Eso sale
gratis si el fotograma no *suma* un paso sino que *coloca*: `Avanzar(ms)` recibe los
milisegundos transcurridos y pone la escena en ese instante. No hay `Thread.Sleep`, ni
`DispatcherTimer` lento, ni tope de FPS en ningún sitio; los Hz reales se leen con
`EnumDisplaySettings` (y `DwmGetCompositionTimingInfo` de reserva) **solo para el
informe**, nunca para animar.

Comprobado a 60, 75, 120, 144, 165 y 240 Hz: el estado de la escena en un instante dado
es idéntico en los seis casos, **diferencia máxima 0,000000**, y lo único que cambia son
los fotogramas (179 a 60 Hz, 716 a 240, ×4,0 para ×4 de refresco).

Y la calidad no se tocó para ganar fotogramas: `EdgeMode.Unspecified`,
`BitmapScalingMode.HighQuality`, sin `UseLayoutRounding` ni `SnapsToDevicePixels`. Lo que
se arregló fue de dónde salían los fotogramas, no cuánto se dibuja en cada uno.

### 3. El saludo, y dos brazos en vez de uno

La secuencia pasó a ser: aparece, **saluda de forma reconocible**, hace una pausa
expresiva, baja la brocha y da la pasada. «Reconocible» obligó a cambiar el robot: ahora
tiene **dos brazos con su propio giro**, el derecho para saludar y el izquierdo para la
brocha, y la mano es una palma con **tres dedos que asoman por encima** más el pulgar.
Con las rayas dentro de un disco —la primera versión— parecía una pelota con marcas; lo
que tiene que leerse de un vistazo es «esto es una mano y está saludando».

El vaivén es un seno, no una ida y vuelta por tramos: `-(38 + 16·sin(k·2π·n))`, con `n`
saludos según el nivel. Un seno no tiene esquinas, así que no hay tirón en los extremos.

### 4. Las dos pausas negras, que eran dos cosas distintas

El guion original dejaba la pantalla **un segundo en negro** entre el final de la
pincelada y el revelado. Se quitó por decisión de UX: la pasada ya cubre la pantalla
mientras avanza, así que al llegar al final no hay nada nuevo que enseñar — solo un
segundo de pantalla apagada que alarga el arranque.

Al quitarla apareció **otra pausa de 708 ms** que no era una etapa, era geometría. El
extremo del trazo era un casquete redondo de 0,675 × el alto de la pantalla, así que
**tapaba la pantalla entera mucho antes de que su centro llegara al borde derecho**: el
pincel seguía viajando por fuera, invisible, y lo que se veía era una pausa. El último
píxel en taparse es el de la esquina más lejana del eje del trazo, y a esa altura el
casquete solo alcanza `sqrt(radio² − distancia²)`. Con esa cuenta, la cola acortada y la
pasada casi lineal, los 708 + 1000 ms se quedaron en **64 ms**.

Esta es la lección que se repite en toda la fase: *un tiempo muerto que parece una etapa
suele ser geometría mal contada*.

### 5. La composición, al revés

Y aun así la escena estaba montada del revés. Tapaba primero y destapaba después, con lo
que el revelado era un **segundo barrido** detrás del primero. Lo que se pidió —y lo que
tiene sentido— es que la brocha vaya **destapando a su paso**: lo que ha pasado está
visible, lo que no, sigue tapado.

La versión final no sincroniza dos cosas: **no hay dos cosas**. El negro está desde el
primer fotograma, lo que tapa el escritorio es la propia mancha, y el filo de la mancha
**es** donde está la brocha. No pueden desincronizarse ni puede asomar nada por delante,
porque no hay nada que coordinar. Un progreso temporal, una figura.

Con el negro a la derecha del filo, la forma de la mancha deja de ser libre:

- **El filo es una elipse ancha y poco profunda** (semieje horizontal 0,10 × el alto),
  no media circunferencia. Y no es estética: *lo que se curva el filo es lo que el robot
  tiene que estar fuera de cuadro*. El filo más adelantado está a la altura del eje y el
  más retrasado en las esquinas; si el robot no va con el retrasado, parte del borde se
  destapa por delante de la brocha. Con el casquete de 0,675 × alto de la versión
  anterior eran **245 px** fuera de la pantalla: el robot empezaba la pasada cortado por
  el borde y volvía a entrar. Con la elipse plana son **39 px**. Que además es la forma
  que tiene una brocha plana de verdad.
- **El robot va con la punta más retrasada del filo**, `X − alcance`. A la altura del
  eje eso deja lo destapado unos 39 px por detrás del canto de la brocha: el retraso que
  pide el efecto para leerse, en el sentido correcto. La revelación nunca se adelanta.
- **La pasada acaba en el píxel exacto**, `w + lo que más asoma`. Y ahí se acaba también
  el robot sin inventarle una salida: como va del lado todavía tapado, cuando el filo
  llega al borde derecho su cuerpo ya está fuera de cuadro. *Destapar del todo y quedarse
  sin robot son el mismo fotograma.* La primera versión de esto se dejaba 290 ms de
  pantalla quieta al final — no negra, pero quieta.
- **Los pelos de la brocha son casi negros, no negros.** Van cargados de la misma pintura
  que tapa la pantalla, así que sobre ella un negro puro desaparecía y la brocha parecía
  acabarse en la virola.

Dos consecuencias en la máquina de la escena:

- **La espera a la nube se mudó.** Si al terminar de bajar la brocha la nube todavía no
  ha avisado de que está montada, el reloj de la escena se queda quieto **antes** de
  destapar, no a mitad del revelado. Esperar a mitad enseñaría medio escritorio a medio
  montar, que es justo lo que esta escena viene a tapar. Medido en esta máquina no llega
  a ocurrir: la nube avisa sobre los 600 ms y la brocha no arranca hasta los ~1660.
- **Saltársela ya no corta, acelera.** Esc o un clic colocan el reloj al principio de la
  pasada —si no había llegado— y lo ponen al cuádruple: se ve la misma brocha destapando,
  en 330 ms en vez de 1320. Cortar en seco dejaría un salto de negro a escritorio, y lo
  que hay debajo del negro es justo lo que se quiere ver. Y si te la saltas **no se
  espera a nadie**: has pedido verlo ya.

### Los cuatro niveles, como quedaron

| | entra | saluda | pausa | baja la brocha | pasada = revelado | total |
|---|---|---|---|---|---|---|
| Completo | 320 ms | 820 (×3) | 220 | 300 | 1320 | **2980 ms** |
| Equilibrado | 200 | 520 (×2) | 140 | 190 | 900 | **1950 ms** |
| Reducido | 140 | 360 (×1) | 80 | 120 | 560 | **1260 ms** |
| Desactivado | — | — | — | — | — | no se crea la escena |

«Reducido» **conserva el saludo**, al contrario que en el primer diseño: uno solo y todo
lo demás al mínimo. Es el nivel de quien ha pedido menos movimiento en Windows, así que
se le enseña la escena entera sin florituras ni esperas largas, no media escena.

No hay etapa detrás de la pasada: `Total == HastaPasada`.

### Lo que se midió de la versión final

Fuera de pantalla, dibujando la escena sobre un fondo magenta puro —que no usa ni el
robot ni la pintura, así que «magenta = destapado»— y contando píxeles, en seis pantallas
(1366×768, 1080p, 1440p, la de esta máquina, 4K al 225 % y una 21:9) y en los tres
niveles:

- **tapado al 100 % desde el primer fotograma** hasta que arranca la pasada (medido 1664
  ms contra los 1660 del guion, que es el paso del muestreo);
- destapado **progresivo y monótono**: 0 % → 8 % → 29 % → 59 % → 81 % → 100 %, sin que
  vuelva a taparse nada en ningún instante;
- **cero píxeles destapados por delante del filo** en ninguna fila, comparando el mapa de
  bits contra el límite calculado fila a fila (la elipse del filo más los dos mechones);
- **cero filas con huecos**: lo destapado es siempre un bloque pegado al borde izquierdo,
  nunca islas;
- **todo destapado exactamente al acabar**: cola de 0–2 ms, y 0 ms de pausa negra
  después;
- el robot, **fuera de cuadro en ese mismo fotograma**.

Lo que esta prueba **no** puede ver —y por eso hace falta mirarla— es si el ritmo gusta.
Los números dicen que no hay parpadeos, zonas sin destapar, elementos que asomen antes de
tiempo ni pausa negra final; no dicen si 1320 ms de pasada se hacen largos.

## Fase C2 · La construcción de la nube

> **Superada.** Tanto esta sección como «El asistente, y marcharse andando» describen la
> escena del robot humanoide, que se tiró entera: ver «La nube despierta», al final. Las
> lecciones de rendimiento y de método siguen valiendo; el personaje y la cuadrícula no.

La escena de la brocha funcionaba —medida, verificada y aceptada— y aun así se cambió
entera. El motivo no fue técnico: **la idea era genérica**. Una brocha que destapa el
escritorio es el mismo efecto de cortinilla que hace cualquier cosa, y lo que arranca
aquí no es una cortina, es un entorno de escritorio montándose. Lo que queda de aquella
tanda son las lecciones, que se aplican enteras a esta; lo que se fue es la brocha.

La escena de ahora: la pantalla empieza negra, el robot aparece **en el centro**,
saluda, y saca de la mano una **semilla de nube** —un núcleo azul con detalles coral—
que lleva al centro y activa. Desde ahí el núcleo echa líneas a los bordes, las líneas
cuajan en una **cuadrícula holográfica**, y la cuadrícula se convierte en escritorio por
zonas: primero el fondo, después la franja de la barra —del centro hacia los extremos,
como la barra de verdad— y encima los **fragmentos de luz** que encajan donde van los
widgets. Cuando está todo, el robot da el visto bueno y se apaga.

### Lo único que de verdad importa: una sola lista de números

Toda la escena sale de **una lista de piezas, cada una con un número**, su `orden`, y de
un único `Avanzar(ms)`. Esa pieza hace cuatro cosas distintas a lo largo de la escena
—dibujar su contorno en la cuadrícula, destaparse, apagar el contorno y soltar su
fragmento— y las cuatro se calculan del mismo número. No hay dos animaciones que
sincronizar porque no hay dos animaciones.

Esto no es elegancia: es la lección de las tres tandas anteriores. Dos relojes que
tienen que ir a la par se separan en cuanto la máquina va cargada, y «la máquina
cargada» es, literalmente, el instante en que esto se ejecuta.

```
contorno (etapa malla) = (malla − orden·0,72) / 0,28
q        (etapa obra)  = (obra  − orden·0,78) / 0,22
tapa                   = 1 − q/0,60          ← la tapa se va primero
contorno (etapa obra)  = 1 − (q−0,25)/0,75   ← y el contorno después
```

Ese desfase entre las dos últimas líneas es el efecto entero: durante un instante se ve
el escritorio de verdad **con la retícula todavía encima**, que es lo que hace que
parezca construido en vez de descubierto.

### El orden de construcción no es la distancia al centro

La cuenta obvia —ordenar las casillas por lo lejos que están del centro— sale mal, y no
por poco. El área de un anillo de radio r crece con r, así que con el orden puesto a la
distancia el escritorio aparece despacio al principio y de golpe al final. Medido:
**10 % construido a tres cuartos de la obra y 71 % trescientos milisegundos después**.

Lo que se quiere es que aparezca siempre al mismo ritmo, y para eso el orden no es la
distancia sino el **puesto en la lista ordenada por distancia**: la casilla número n de
N se destapa en n/N. Así el ritmo es el mismo en 16:9, en 21:9 y en vertical, sin
depender de la forma de la pantalla. Medido después: 29 % → 75 % → 100 % en los mismos
tres cuartos, y **ningún salto mayor del 4 % entre muestras consecutivas**.

El ruido que rompe la circunferencia perfecta del frente se suma **antes** de ordenar, no
después: así desordena de verdad el borde sin alterar el ritmo. Y es un ruido
determinista escrito a mano, no un `Random`: la escena tiene que ser una función pura del
tiempo para que la prueba pueda compararla consigo misma.

### Siete filas eran un damero

La primera rejilla tenía siete filas —casillas de 150 px— y se veía el tablero: cuadrados
grandes apagándose enteros. Con diez filas la casilla baja a unos 100 px y el frente se
lee como una zona que se construye. Más de diez y las líneas de la cuadrícula empiezan a
ensuciar en vez de estructurar.

Y un detalle que no es obvio: las tapas llevan `EdgeMode.Aliased` y un píxel de solape.
Son ciento y pico rectángulos negros pegados, y si WPF suaviza sus cantos queda una
costura clarita en cada junta. En un rectángulo recto el suavizado no aporta nada, así
que se quita **solo ahí** — el robot, los contornos y el núcleo lo conservan.

### La barra se construye la última, y por separado

La franja de la barra no son «las casillas que la tocan»: una casilla mide 100 px y la
barra 54, así que eso se llevaría por delante una décima parte de la pantalla. Son
**piezas propias, por encima de las de la cuadrícula**, con su propio orden en el último
tramo (0,80–0,98) y del centro de la barra hacia sus extremos.

Que quede negra mientras el resto ya está construido es a propósito, pero sola parecía un
trozo que faltaba. Por eso su contorno va en **coral** y no en azul: marca el hueco como
reservado, no como roto.

Los fragmentos de los widgets salen de **esa misma cuenta**: el momento en que vuela cada
uno se calcula del orden de su trozo de barra, así que por construcción no pueden
adelantarse a la barra que los sostiene. Vienen desde dentro hacia fuera, encajan y se
apagan: cero opacidad en los dos extremos, para que al final no quede ni uno encendido.

Esto es, de paso, lo **único** que la escena sabe de la nube de verdad: por qué borde
sale y cuánto ocupa, leído de `Paneles[principal]`. Con cualquier cosa rara en el config
asume «arriba»: una escena bonita en el sitio equivocado es mejor que ninguna escena.

### La nube de verdad se colaba por encima

Un problema que la escena anterior escondía sin saberlo. Las islas se crean **después**
que la escena y también son `Topmost`, así que Windows las pone por encima: la barra de
verdad aparecía sobre el negro a mitad de escena. Con la brocha apenas se notaba —la
pantalla ya estaba casi destapada cuando ocurría—; con una construcción que enseña el
escritorio por zonas, ver la barra entera aparecer de golpe a los 600 ms rompe el efecto.

El pulso de 60 ms —el que ya miraba Esc y el botón del ratón— reclama ahora también el
primer plano mientras la escena está tapando, y deja de hacerlo en cuanto empieza a
destaparse, que es justo cuando la nube tiene que verse.

### El robot ya no lleva brocha

Lleva una **mano abierta con la palma hacia arriba** en el brazo izquierdo: es la que
ofrece la semilla. El derecho sigue siendo el del saludo, y es también el del gesto de
aprobación del final. Los dos gestos van en brazos distintos a propósito: si el mismo
brazo saludara y ofreciera, el saludo parecería parte del lanzamiento.

Dos cosas pequeñas que costaron un fotograma cada una:

- **La semilla se dibuja por encima del robot y los rayos por debajo.** Con el núcleo
  debajo, el robot tapaba la semilla justo cuando la tenía en la mano; con los rayos
  encima, le cruzaban la cara.
- **El brazo se mueve después de soltar.** Mientras la semilla está en la mano el brazo
  se queda quieto, y así la semilla puede ir en su sitio sin tener que girarla con el
  brazo: una cuenta menos que cuadrar, y una cuenta menos que pueda desincronizarse.
- **El escalado del anillo de activación va centrado en el propio anillo**, no en el
  origen del lienzo. Un óvalo colocado en (−r, −r) y escalado desde su esquina se abre
  hacia abajo y a la derecha: el anillo salía descentrado del núcleo.

Y el tamaño del robot sale del alto de la pantalla **limitado también por el ancho**: el
robot mide casi dos altos de ancho, y en una pantalla vertical de 1080×1920 un robot «de
1920» se salía 108 px por la izquierda al apartarse.

### Los cuatro niveles

| | entra | saluda | pausa | semilla | rayos | malla | obra | total |
|---|---|---|---|---|---|---|---|---|
| Completo | 300 ms | 760 (×3) | 200 | 380 | 260 | 220 | 860 | **2980 ms** |
| Equilibrado | 200 | 480 (×2) | 130 | 250 | 170 | 150 | 570 | **1950 ms** |
| Reducido | 140 | 330 (×1) | 80 | 160 | 110 | 100 | 340 | **1260 ms** |
| Desactivado | — | — | — | — | — | — | — | no se crea la escena |

Los totales son **los mismos que tenía la escena de la brocha**: lo que cambió es el
relato, no cuánto dura. «Reducido» conserva las seis etapas, simplificadas; es el nivel
de quien ha pedido menos movimiento en Windows, así que se le enseña la escena entera sin
esperas largas, no media escena.

La espera a la nube se mudó a **antes de la obra**, con la cuadrícula ya dibujada:
esperar a mitad de la construcción dejaría media pantalla con la nube a medio montar, que
es justo lo que esto viene a tapar. Medido en esta máquina no llega a ocurrir — la nube
avisa sobre los 600 ms y la obra no empieza hasta los ~2120.

Y saltársela no corta: coloca el reloj al principio de la obra y lo pone al cuádruple, así
que se ve la misma construcción, con sus zonas y sus fragmentos, en 215 ms en vez de 860.
Esa cuenta vive en un método aparte (`AlSaltar`) para poder probarla desde cada etapa sin
abrir ninguna ventana.

### Lo que se midió

Fuera de pantalla, dibujando la escena sobre un fondo magenta puro —que no usa ningún
elemento del dibujo— y contando píxeles. Una nota de método que costó un falso fallo: hay
que medir **dos** cosas, porque una tapa a medio desvanecer no está terminada pero
tampoco es negra. Con un solo umbral estricto la prueba cantaba «165 ms de negro al
empezar la obra» cuando lo que había era la primera tanda de casillas desvaneciéndose a
la vista.

- **El saludo va primero**: durante toda su etapa y la pausa no hay semilla (0,0000), ni
  estructura, ni un solo píxel destapado. El brazo llega a 54°.
- **La semilla llega al centro** con menos de 2 px de error, y los rayos no salen hasta
  que está activada.
- **Se construye por zonas**: 0 % → 29 % → 75 % → 100 %, y el mayor salto entre dos
  muestras consecutivas (de 150) es del **4,0 %**. No aparece nada de golpe.
- **La barra nunca va por delante del fondo**, comprobado muestra a muestra.
- **Sin negro después de empezar**: la pantalla deja de ser negra 26 ms (Completo),
  11 ms (Equilibrado) y 12 ms (Reducido) después de arrancar la obra.
- **Sin restos al final**: 100 % construido, y tapas, núcleo, fragmentos y robot a cero.
- **7 resoluciones × 4 bordes de barra** —incluida una pantalla vertical y una 21:9—:
  tapadas al empezar, monótonas, sin restos, y el robot cabe entero en todas.
- **Esc y clic en las siete etapas** y en los tres niveles: nunca retrocede, nunca deja
  restos, y termina en 215 / 143 / 85 ms.
- **60, 75, 120, 144, 165 y 240 Hz**: el estado en un instante dado es idéntico en los
  seis; lo que cambia son los fotogramas (179 a 60 Hz, 716 a 240).
- **Decidir un fotograma cuesta 0,019 ms** en la pantalla de esta máquina — un 0,5 % del
  presupuesto de 240 Hz. Las seiscientas piezas no son un problema de CPU, y además casi
  ninguna cambia en un fotograma dado: solo las que están dentro del frente.

Lo que esta prueba **no** puede ver —y por eso hace falta mirarla— es si el ritmo gusta.

## Fase C2 · El asistente, y marcharse andando

> **Superada.** El robot humanoide está borrado: ver «La nube despierta», al final.

Dos cosas que faltaban para que la escena dejara de parecer una demo: el robot se
**apagaba** al terminar la construcción, y era un monigote hecho de prisa. Ninguna de las
dos se arregla con parámetros.

### El robot se va por su propio pie

Antes, al acabar la obra, la opacidad del robot bajaba a cero en 190 ms y ya está. Eso no
es irse: es que alguien lo borra. Ahora hay una **séptima etapa** detrás de la obra —con
el escritorio entero ya construido y a la vista, así que no es ninguna pausa negra— en la
que revisa la nube, asiente, saluda por última vez y **cruza el borde izquierdo volando**.

Por la izquierda y no por otro sitio: el robot nace en el centro y lo primero que hace es
apartarse a la izquierda para dejarle el centro a la semilla. Ese es su sentido de marcha
durante toda la escena, y salir por donde vino es lo único que no se lee como un salto.

Lo que hace que se lea como marcharse y no como desaparecer:

- **El apagado se decide por la POSICIÓN, no por el tiempo.** Mientras le quede un píxel
  dentro de la pantalla está dibujado al 100 %; en cuanto su lienzo entero cruza el
  borde, deja de dibujarse. Para entonces no hay nada visible que apagar, así que el
  cambio no se ve — y como la condición es geométrica, vale igual en 1366×768 que en una
  pantalla vertical. La prueba comprueba exactamente eso: en 3 niveles × 8 pantallas,
  **nunca se apaga estando dentro**.
- **La curva de salida es una mezcla, no una `EaseInOut` pelada.** Con la curva limpia se
  pasaba el primer tercio prácticamente quieto y después cruzaba la pantalla en 170 ms,
  que se lee como un tirón. Un 35 % de recta le da empuje desde el principio y el 65 % de
  curva deja la suavidad en los dos extremos. Medido: empieza a moverse en el 58 % de la
  etapa y está fuera del todo en el 91 %.
- **Flota mientras se va**: un vaivén en vertical que no para y que se marca más según
  coge velocidad. Es lo que lo separa de una caja deslizándose.
- **Se inclina hacia donde va**, como quien echa el cuerpo al arrancar, y lleva un
  **aura** azul nube bajo los propulsores que se enciende con la aceleración.
- **Deja una estela** de catorce chispas azul nube y coral. Nacen de la MISMA curva de
  salida —no de una animación aparte—, así que caen sobre su camino de verdad; y la
  última se apaga en el 98 % de la etapa, así que no queda ni una encendida.
- Y el saludo de despedida **se solapa un 10 % con la marcha**: empieza a irse mientras
  termina de saludar. Sin solape eran dos cosas en fila; con él, un gesto seguido.

Esc o un clic aceleran también esto: la escena entera corre al cuádruple, así que la
salida se ve igual —con su estela y su aceleración— en 68 ms en vez de 270. Lo que no
hace nunca es cortarla.

### De monigote a asistente

El robot anterior era una nube con visera, dos palos por brazos y un cono por propulsor.
Funcionaba de lejos y no aguantaba una mirada. El de ahora conserva **exactamente la
identidad** —la nube con el visor cruzándola, que es la silueta que hay que reconocer— y
reparte el resto como es debido:

- la nube pasa a ser **la cabeza**, no la cabeza y el cuerpo a la vez (por eso parecía un
  sello y no un personaje);
- debajo hay un **torso compacto y redondeado** con el **emblema de la nube en el pecho**
  en azul nube y una chispa coral. La nube grande es quién es; la del pecho, para quién
  trabaja;
- los **brazos tienen codo**: hombro, antebrazo y mano, con la línea de la junta marcada
  encima del trazo grueso. Solo con el trazo parecía un tubo doblado;
- **manos abiertas** de palma y cuatro dedos. La izquierda presenta con la palma hacia
  arriba —ahí se apoya la semilla—, la derecha saluda;
- **no tiene piernas, tiene flotación**: anillo, chorro y halo bajo el torso. Una nube no
  anda, y unas piernas colgando lo dirían todo al revés;
- y **cara con expresión**: las pupilas se mueven (mira hacia donde ha quedado la barra,
  leído del config) y los ojos se entrecierran. En una cara sin boca, entornar los ojos es
  la única sonrisa posible, y es lo que hace legible el gesto de aprobación.

Tres cosas de dibujo que costaron un fotograma cada una:

1. **El brazo que saluda va por DELANTE de la cabeza.** La nube es ancha, y con el brazo
   detrás la mano levantada asomaba por el borde del cabezón en vez de saludar. El otro
   brazo sigue detrás del torso, que es lo que hace que el hombro se lea como una
   articulación y no como un palo pegado.
2. **El tamaño sale del alto de la pantalla pero limitado por el ancho.** El robot mide
   1,6 altos de ancho, y en una pantalla vertical de 1080×1920 un robot «de 1920» se
   salía 108 px por la izquierda al apartarse.
3. **Dos lienzos, uno dentro del otro.** El de fuera lo mueve la escena por la pantalla y
   el de dentro es el que se inclina al asentir. Con uno solo, las dos transformaciones
   se pisan.

Y una decisión de rendimiento al revés que antes: **se quitó el `BitmapCache`** del
robot. Cachear sale a cuenta cuando lo único que cambia es la posición; aquí cambian los
brazos, las pupilas, los ojos y la inclinación en cada fotograma, así que el mapa de bits
habría que rehacerlo igualmente. Y ya no hay nada que lo justifique: los brillos pasaron
de `BlurEffect` a degradados radiales, que se calculan una vez y después se mueven y
escalan gratis. Todo el robot es geometría vectorial, así que al 100, 125, 150 o 200 % de
DPI no hay nada que tocar y no hay nada que pueda salir borroso.

### Los cuatro niveles, otra vez

| | entra | saluda | pausa | semilla | rayos | malla | obra | salida | total |
|---|---|---|---|---|---|---|---|---|---|
| Completo | 300 ms | 760 (×3) | 200 | 380 | 260 | 220 | 860 | 900 | **3880 ms** |
| Equilibrado | 200 | 480 (×2) | 130 | 250 | 170 | 150 | 570 | 600 | **2550 ms** |
| Reducido | 140 | 330 (×1) | 80 | 160 | 110 | 100 | 340 | 380 | **1640 ms** |

La construcción no se tocó: dura exactamente lo que duraba. Lo que se añade es la
despedida, y el total sigue dentro de los 2,5–4,5 s acordados.

Los gestos de aprobación y despedida **se mudaron de la obra a la salida**. Antes el
visto bueno iba repartido por encima de la construcción y se quedaba a medias; además
hacía que el escritorio y el robot compitieran por la atención en el mismo momento.
Durante la obra el robot ahora solo mira.

### Lo que se midió

Mismo método de siempre: la escena dibujada fuera de pantalla sobre magenta puro y
contada píxel a píxel. Con una corrección de método que costó un falso fallo: **los
píxeles del robot hay que descontarlos**. Con la escena entera construida y el robot
todavía encima, la pantalla daba 97 % y la prueba cantaba un 3 % sin construir que era,
literalmente, el robot.

- **El orden**: durante toda la obra, cero saludos, cero estela y el robot al 100 %. Los
  gestos empiezan cuando la construcción termina, no antes.
- **La salida**, en 3 niveles × 8 pantallas (incluidas una vertical, una 21:9 y las
  equivalencias de 125, 150, 200 y 225 % de DPI): nunca se apaga estando dentro, nunca
  retrocede, siempre sale entero, siempre con aceleración (la velocidad punta cae entre
  el 70 % y el 95 % de la etapa) y sin restos al final.
- **El escritorio durante la salida**: nunca baja del **100,0 %** construido.
- **Al final**: tapado 0, núcleo 0, estela 0, aura 0, robot sin dibujar.
- **Esc y clic en las ocho etapas** × 3 niveles: nunca retrocede, nunca apaga al robot
  dentro de la pantalla, siempre termina con él fuera y sin restos.
- **60–240 Hz**: estado idéntico en el mismo instante; 233 fotogramas a 60 Hz y 932 a 240.
- **Decidir un fotograma: 0,020 ms** en la pantalla de esta máquina, un 0,5 % del
  presupuesto de 240 Hz.

## Fase C2 · La nube despierta

El robot humanoide está borrado. No fallaba nada medible —tenía su verificación entera
en verde— pero visualmente no funcionaba, y el motivo es de concepto, no de parámetros:
**un humanoide pide una animación de humanoide**. Brazos que se levantan, posturas,
anatomía que leer. Para contar «el escritorio se enciende», todo eso es ruido compitiendo
con lo único que importa. Lo que queda es la nube, y ya está.

La escena de ahora: la pantalla empieza negra, una nubecita aparece en el centro, da un
par de botes con su pulso de brillo y su parpadeo —eso es el saludo—, se enciende desde
dentro y **se abre**: crece hasta ocupar la pantalla y por dentro de ella va apareciendo
el escritorio de verdad. Cuando está todo a la vista se recoge, viaja a donde está la
barra y se funde con ella como su icono.

### El agujero con forma de nube

La regla que el usuario puso por delante de todo —«la nube debe ser la única fuente de
verdad de la revelación»— tiene una respuesta exacta en WPF, y no es una máscara animada
en paralelo:

```csharp
var hueco = new GeometryGroup { FillRule = FillRule.EvenOdd };
hueco.Children.Add(new RectangleGeometry(new Rect(0, 0, w, h)));
hueco.Children.Add(nube.Trazo);           // ← la MISMA geometría que se dibuja
var tapa = new Path { Fill = Brushes.Black, Data = hueco };
```

Un rectángulo del tamaño de la pantalla y la nube, los dos en el mismo grupo con regla
par-impar: lo que está dentro de los dos se queda fuera del relleno. O sea, **un agujero
con forma de nube**, que crece cuando crece la nube porque *es* la nube. El mismo
`Geometry` lo comparten el agujero, el cuerpo, el halo y el filo, y las tres
transformaciones —escala, giro, posición— viven **dentro de la geometría**, no en los
`Path`. Así no hay nada que acordarse de mover a la vez.

No hay dos relojes que se puedan separar, y el escritorio no puede asomar donde la nube
no ha llegado. No es una propiedad que haya que vigilar: es la construcción.

Y la capa negra solo empieza a irse cuando la nube **ya tapa la pantalla entera**:

```csharp
opTapa = 1 - clamp((escala - escalaLlena) / (escalaLlena * 0.05));
```

Mientras eso no pasa vale 1, así que no hay un solo instante en que se pueda ver
escritorio por fuera de la nube. Medido píxel a píxel, en los tres niveles: **cero**.

### Cuánto hay que agrandar la nube

`escalaLlena` no se puede poner a ojo ni deducir del ancho y el alto: la nube tiene la
base recta, los lados redondos y arriba bultos, así que lo que tarda en taparse son las
esquinas y eso depende de la forma. Se busca por bisección preguntándole al propio
contorno si contiene el borde de la pantalla — la misma figura que luego se dibuja, así
que la respuesta vale para cualquier resolución sin repasar nada a mano.

La primera versión preguntaba con `Geometry.FillContains`, y eso **aplana la figura en
cada llamada**: la prueba se fue a más de dos minutos. Con el contorno aplanado una vez a
polígono y un lanzamiento de rayo de toda la vida, montar la escena cuesta **0,4 ms**.

### Dos cuentas que había que hacer al cuadrado, y no lineales

Las dos salieron de medir, no de mirar:

1. **Al crecer, la escala se interpola por el ÁREA.** Lo que se ve aparecer es
   superficie, y la superficie va con el cuadrado del ancho. Interpolando el ancho a
   secas, a mitad de la expansión solo se había destapado una cuarta parte de la pantalla
   y el resto salía de golpe: medido, **del 3 % al 69 % en 320 ms**. Con
   `escala = sqrt(lerp(a₀², a₁², t))` el escritorio aparece a ritmo parejo y el mayor
   salto entre muestras baja al 9,8 %.

2. **Al encoger, la escala va en PROPORCIÓN.** De 3360 px a 34 en línea recta, la nube se
   pasaría media etapa siendo enorme y la otra media encogiendo un pelo. Con
   `escala = tope · (icono/tope)^q` —misma razón en cada tramo de tiempo— llega a tamaño
   de mascota a mitad de camino, que es cuando tiene que volver a parecer un personaje.

### El cuerpo se enciende por tamaño, no por tiempo

Un fallo que la prueba cazó y que a ojo habría parecido «un fogonazo raro»: al recogerse,
el cuerpo de la nube vuelve a ser opaco. Atado al tiempo, se encendía cuando la nube
todavía ocupaba la pantalla entera y la convertía en una cortina azul que tapaba el
escritorio que acababa de destapar. Medido: **al 25 % de la etapa de llegada, el
escritorio se iba del 100 % al 26 %**.

Atado al tamaño —`opRelleno = clamp((escalaMini·2,4 − escala) / (escalaMini·1,6))`— no
puede pasar: mientras sea grande es una ventana, y solo al volver a ser pequeña es un
cuerpo.

Lo mismo con la cara, y de ahí sale gratis un detalle bonito: los ojos valen lo que valga
el cuerpo y además se van cuando la nube deja de tener tamaño de personaje. Una ventana
del tamaño de la pantalla no tiene cara; una nube de 34 px en la barra, sí. Nadie tiene
que acordarse de encenderlos al final — vuelven solos.

### El saludo sin brazos

Cuatro cosas a la vez y se lee mejor que una mano: **rebote** vertical (48 px, con `|sin|`
para que haya un bote por pulso), **ladeo** de 5,5° que acompaña el bote, **pulso de
brillo** en el halo, y **un parpadeo** en lo alto del primer bote. El número de botes y
pulsos lo pone el guion: dos en Completo, uno en los otros dos.

### Los cuatro niveles

| | aparece | saluda | despierta | crece | llega | total |
|---|---|---|---|---|---|---|
| Completo | 340 ms | 820 (×2 pulsos) | 260 | 1000 | 780 | **3200 ms** |
| Equilibrado | 220 | 480 (×1) | 170 | 680 | 520 | **2070 ms** |
| Reducido | 110 | 300 (×1) | 110 | 420 | 320 | **1260 ms** |
| Desactivado | — | — | — | — | — | no se crea la escena |

«Reducido» no quita etapas, las acorta: se cuenta lo mismo en menos tiempo, no media
historia.

### Detalles que no son obvios

- **La nube aparece creciendo desde un punto, no fundiéndose desde transparente.** Si
  apareciera por opacidad, su agujero en la capa negra ya estaría abierto y se vería el
  escritorio asomando dentro de una nube a medio aparecer. Mientras es una mascota, el
  cuerpo opaco tapa su propio agujero.
- **`Geometry.Parse` devuelve una figura congelada**, y además la misma instancia para la
  misma cadena. Hay que clonarla antes de ponerle transformaciones propias o la segunda
  llamada revienta.
- **El grosor del halo y del filo va en píxeles de pantalla**, no en unidades de la nube:
  si fuera proporcional, al tamaño de ventana el borde sería una franja de 200 px y al
  tamaño de icono no se vería.
- **La clave del config se sigue llamando `Animaciones.Robot`.** Está escrita en el
  config de la gente desde la Fase A; renombrarla les dejaría la escena en su valor de
  serie sin avisar. Lo que cambió fue el personaje, no el ajuste.

### Lo que costaba y lo que cuesta

| | robot humanoide | la nube |
|---|---|---|
| elementos en la escena | ~700 | 6 |
| decidir un fotograma (esta máquina) | 0,020 ms | **0,006 ms** |
| montar la escena | — | 0,4 ms |

Seiscientos elementos menos, y lo que queda es una figura con tres transformaciones.

### Lo que se midió

- **El saludo**: rebote de 48 px, ladeo de 5,5°, el número de pulsos que dice el guion y
  parpadeo hasta el 0,10 de apertura. Y la pantalla, negra del todo hasta que se abre.
- **Cero píxeles de escritorio fuera de la nube** en los tres niveles, contrastando cada
  fotograma contra la propia figura dibujada aparte.
- **La nube no se deforma**: su proporción se mantiene dentro del ±2,7 % medido sobre el
  mapa de bits (lo que queda es el suavizado del borde).
- **El revelado es monótono** mientras se abre, con un salto máximo del 9,8 % entre
  muestras, y llega al 100 %.
- **Durante la llegada el escritorio nunca baja del 97,3 %**: lo que falta es la propia
  nube y su halo volando hacia la barra, que es lo que se quiere ver.
- **3 niveles × 8 pantallas × 4 bordes** —incluidas una vertical, una 21:9 y las
  equivalencias de 125, 150, 200 y 225 % de DPI—: empiezan tapadas, la capa negra solo se
  abre cuando la nube ya tapa la pantalla, la nube llega a la franja de la barra con su
  cara puesta, se funde sin cortes y no quedan restos.
- **Esc y clic en las cinco etapas** × 3 niveles: nunca retrocede, nunca abre la capa
  negra con la nube pequeña, siempre acaba en la barra y sin restos. Entre 445 y 32 ms
  según desde dónde se salte.
- **60–240 Hz**: el estado en un instante dado es idéntico en los seis.

## Fase C3 · El lanzador y el centro de notificaciones

Dos cosas que no se parecen en nada y que comparten una idea: **lo que decide lo que ves
tiene que poder comprobarse sin abrir una ventana**. El ranking del buscador y el
contador de no leídas son exactamente el tipo de código que se rompe sin dar error —
simplemente ordena mal, o cuenta de más— y hasta ahora ninguno de los dos se podía mirar.

### Los cuatro peldaños

El parecido devolvía un número suelto que mezclaba prefijo, subsecuencia y distancia de
edición. Funcionaba, pero la cifra era la única explicación de por qué una cosa salía
antes que otra, y no se podía razonar sobre ella: un 0,88 podía ser «las iniciales
encajan» o «se parece bastante escrito mal», y esas dos cosas no deben valer lo mismo.

Ahora cada criterio cae en su **franja**, y la franja es el peldaño:

| | | |
|---|---|---|
| Exacta | 1,00 | lo escrito ES el nombre |
| Prefijo | 0,90 – 0,99 | el nombre empieza por lo escrito |
| Palabra | 0,78 – 0,89 | una palabra empieza así, las iniciales, o aparece por el medio |
| Aproximada | 0,01 – 0,76 | subsecuencia o erratas |

Las franjas no se solapan, y eso es el invariante del que vive todo lo demás: **ordenar
por puntos y ordenar por peldaño dan lo mismo**, así que no hay dos verdades que
mantener sincronizadas. Dentro de cada franja se afina con lo que cubre la consulta —
escribir «note» para «Notepad» vale más que para «Notepad++ plugin manager»— y eso es lo
que desempata de verdad, no el orden en que la fuente devolvió las cosas.

### El empujón por costumbre, y lo que enseñó la prueba

Las apps que más usas suben en la lista. Eso se sumaba tal cual, hasta +0,35, y con el
ranking nuevo eso es un error: basta para que una **errata** de una app muy usada
adelante a un nombre que empieza por lo escrito. El peldaño dice cómo de bien encaja lo
que has escrito, y eso no lo arregla la costumbre.

El primer arreglo fue repartir el empujón sobre lo que falta para el 1,0. Parecía
suficiente y **la prueba lo tiró en el primer intento**: con 0,70 y el empujón al máximo
salía 0,805, que ya es franja de palabra. Lo que hay que mirar no es el 1,0 sino el
**techo de su propia franja**:

```csharp
public static double ConCostumbre(double puntos, double empujon, Nivel nivel)
{
    double tope = TopeDe(nivel);
    if (puntos >= tope) return puntos;      // la fuente le puso un extra a propósito
    return Math.Min(tope, puntos + empujon * (tope - puntos));
}
```

Así la costumbre desempata dentro del peldaño —que es lo que debe hacer— y no puede
subirte uno. Comprobado con el empujón disparado a 1,0 desde el suelo de cada franja.

Vale la pena dejarlo escrito porque es el patrón: **las dos veces que la prueba encontró
algo en esta fase, lo encontró en código que yo acababa de escribir y daba por bueno.**

### Orden estable de verdad

`OrderByDescending` de LINQ ya es estable, pero «estable» ahí significa «en el orden en
que llegaron», y el orden en que llegan las apps depende de cómo recorrió Windows el
menú Inicio. Eso no es estabilidad, es azar repetible. El orden definitivo desempata por
puntos, peldaño, categoría, título y clave — siempre el mismo, vengan como vengan.

Y los repetidos se funden quedándose con el de más puntos. La identidad es lo que **se
ve** —categoría, título y detalle— y no la clave: la clave de dos ventanas del mismo
programa es la misma a propósito (sirve para contar usos), y colapsarlas escondería
ventanas de verdad. Hay una prueba para justo eso.

### Cancelar lo que llega tarde

Los archivos los busca Windows Search en otro hilo. Cancelar el token no basta: una tarea
puede haber terminado justo antes de la cancelación y estar ya en la cola del hilo de
interfaz, y entonces el token no dice nada. Por eso cada petición se lleva un número
—`Relevo`— y al volver se pregunta si sigue siendo la última. Son tres líneas, y son las
que deciden si ves archivos de «doc» debajo de «documento».

---

### El centro de notificaciones no trae una ventana nueva

Es un panel más, como el de la red o el de la batería. Eso no es pereza: significa que
sale solo en los cuatro bordes, porque con la isla horizontal se despliega el cajón y con
la barra vertical `AbrirPanel` deriva a la ventana superpuesta de C1, que ya calcula su
rectángulo en coordenadas de pantalla, se eleva por encima de las aplicaciones y devuelve
el orden Z al cerrarse. Inventar aquí una segunda arquitectura habría significado repetir
los dos problemas que esa ventana resolvió y resolverlos peor.

### Y no pide el teclado

El menú Inicio sí lo pide, porque tiene una caja donde escribes. Este no: si lo pidiera,
abrir las notificaciones te sacaría de lo que estabas haciendo, y eso es exactamente lo
que un centro de notificaciones no debe hacer.

Esc se mira preguntando al sistema cada 60 ms **mientras este panel está abierto** — el
mismo truco que usa la escena de entrada y que usa el arrastre de la isla. No hay gancho,
no se captura nada, ninguna otra ventana se entera, y se deja de mirar en cuanto se
cierra. El clic fuera lo da el vigilante que ya existía: el panel solo tuvo que apuntarse
a la lista.

### «No molestar» no se come tu historial

El código anterior descartaba las notificaciones que llegaban con el modo silencio
puesto: `if (NoMolestar.Activo) continue;` antes de encolarlas. Con una isla que solo
asoma avisos eso era defendible. Con un centro que guarda historial es un fallo: el modo
silencio te borraría lo que ha pasado mientras no mirabas.

Ahora el centro **se queda todo lo que llega** y devuelve solo lo que además hay que
asomar. «No molestar» impide lo segundo y no lo primero. Es la diferencia entre «ahora no
me interrumpas» y «no quiero saber nada», y un centro de notificaciones necesita la
primera.

Son dos interruptores distintos y se enseñan como tales: el de NubeBar, que se puede
cambiar desde el panel, y las horas de silencio de Windows, que son de solo lectura
(`SHQueryUserNotificationState`) y que el botón dice pero no finge poder apagar.

### Las acciones rápidas, y hasta dónde llegan

Un toast puede traer botones. Vienen en el XML como `<action content="…"
arguments="…" activationType="…">` y **solo se pueden pulsar las de tipo `protocol`**:
esas son una URI y lanzarlas es abrirla. Las de tipo `foreground`/`background` —las de
responder a un mensaje, las que de verdad querrías— no son una URI: son un argumento que
Windows le entrega a la app dueña a través de la plataforma de notificaciones, y para
hablar con esa plataforma hace falta identidad de paquete, que es justo lo que NubeBar no
tiene (el mismo motivo por el que las notificaciones se leen de la base de datos).

Fingirlo sería lo peor de los dos mundos: un botón «Responder» que en realidad abre la
app y pierde lo que ibas a responder. Así que **esas no se enseñan**, y pulsar la tarjeta
—que abre la app dueña— sigue siendo lo que hay. Las URI, además, se filtran antes de
llegar al shell: fuera `file:`, `javascript:`, `vbscript:`, `data:` y `shell:`.

### Lo nuestro va en su propio archivo

Si has leído una notificación y cuál has descartado se guarda en
`notificaciones.json`, al lado de `uso.json` y **no dentro del config**. Dos razones: el
config lo edita la gente a mano y no tiene por qué llenarse de identificadores, y
guardarlo ahí dispararía la recarga en caliente cada vez que marcas una como leída.

Descartar es para siempre: se apunta el id para que no vuelva cuando se relea la base de
Windows, que sigue teniéndola. Los ids descartados también tienen tope — son números,
pero el archivo es del usuario y no tiene por qué crecer sin fin.

Lo que ya estuviera silenciado en `Barra.NotificacionesSilenciadas` se sigue respetando:
era la única forma de silenciar una app antes de que existiera el centro, y quien la usó
no tiene por qué volver a hacerlo.

### La campana viene de serie, y se puede quitar

Usa el mecanismo que ya existía para `control`, `bandeja` y `fijadas`: una bandera
`Modulos.NotificacionesDeSerie` que mete el módulo **una vez** delante de la bandeja. Si
lo quitas de la isla, la bandera se queda puesta y no vuelve.

Es un cambio en el config del usuario y conviene decirlo: añade un módulo a
`Modulos.Derecha` y una bandera. Es reversible y es el convenio de la casa desde la
Fase A, pero no es invisible.

### La cifra de la chapita, y el segundo fallo que cazó la prueba

El número de no leídas va **dentro** de un círculo del acento, así que su tinta es la que
se lee ahí y no la del texto de la isla. Se puso `Contraste.Legible(blanco, acento)`, que
es lo que hace el resto del proyecto… y la prueba de los siete temas lo tiró: salía entre
**2,0 y 2,9 de contraste en seis de los siete**.

El motivo es que `Legible` aclara el color hacia el blanco cuando el fondo es oscuro, y
partiendo del blanco no tiene a dónde ir. Para un relleno de tono medio hay que **elegir**
entre blanco y negro, no aclarar. De ahí `Contraste.TintaSobre`, que es ahora una regla
general del proyecto: con ella la cifra no baja de 4,5 en ninguno de los siete.

### Las pruebas se quedan

Las fases anteriores comprobaban cada tanda con un arnés temporal que se borraba al
terminar. Para medir una animación eso está bien —lo que se mide es irrepetible— pero
aquí no: son reglas que tienen que seguir siendo verdad dentro de seis meses.

`NubeBar.exe --pruebas [informe.txt]` corre **180 comprobaciones** y devuelve 1 si algo
falla. Es un `if` en `App.OnStartup`, como `--salir` y `--guardian`: no añade nada al
arranque normal, no abre ninguna ventana, no toca el config y no deja ningún proceso
detrás. El centro se prueba con instancias propias y la persistencia en una carpeta
temporal que se borra.

Cubren: los cuatro peldaños y que sus franjas no se solapan, erratas, orden estable,
repetidos, búsquedas vacías, el empujón por costumbre, el relevo de las búsquedas que
tardan, agrupación, contador de no leídas, marcar como leída, descartar, borrar todo,
silenciar, no molestar, persistencia, el alto de la tarjeta en cinco pantallas, el
contraste del panel en los siete temas, los cuatro niveles de animación y abrir y cerrar
en bucle.

### Lo que no cubren

Que sea bonito. El contraste se mide; la composición hay que mirarla. Y la agrupación
visual en la barra vertical depende de la ventana superpuesta de C1, que ya tenía su
propia verificación pero no con este contenido dentro.

## Fase C4.1 · Ventanas reales

Hasta aquí, «qué ventanas hay» era una pregunta que cada sitio contestaba por su cuenta:
el carrusel enumeraba y agrupaba, la vista general reusaba los grupos del carrusel, y el
buscador miraba otra vez. Tres recorridos por segundo y tres criterios parecidos pero no
iguales.

Y lo peor: el criterio —qué ventana es «del usuario»— vivía dentro de una lambda de
`EnumWindows`. Para comprobarlo había que abrir ventanas de verdad, que es justo lo que
una prueba no debe hacer en el escritorio de alguien.

### La decisión, en una función pura

`GestorVentanas.EsDelUsuario` recibe una **ficha de datos planos** —visible, largo del
título, si tiene dueño, los estilos, si está tapada, la clase, el pid— y devuelve si
cuenta, y por qué no si no cuenta. Eso permite probarla con fichas inventadas, y es toda
la diferencia entre tener pruebas de filtrado y no tenerlas.

Las reglas, en orden, porque el orden importa:

1. no visible, nuestra, o **tapada** (`DWMWA_CLOAKED`: otro escritorio virtual, o una
   app de la Store suspendida);
2. `WS_EX_TOOLWINDOW` — por definición no sale en la barra de tareas;
3. `WS_EX_NOACTIVATE` — pulsarla no haría nada, así que en una vista de ventanas sobra;
4. tiene dueño **y** no pide `WS_EX_APPWINDOW` — es un diálogo o una paleta. La
   excepción es la forma documentada de decir «aunque cuelgue, trátame como aplicación»,
   y sin ella se perdían ventanas de verdad;
5. clase del sistema (`Progman`, `WorkerW`, `Shell_TrayWnd`, `Windows.UI.Core.CoreWindow`,
   `MultitaskingViewFrame`…);
6. y solo al final, el título vacío.

**El título ya no es la primera regla ni la única.** Antes lo era, y por eso dejaba pasar
el escritorio y la barra de tareas, que tienen título y son visibles.

`Nativo.VentanasAbiertas` ahora delega en esta misma función: una regla, un sitio.

### Windows avisa, no hace falta preguntar

El carrusel sondeaba cada segundo. `SetWinEventHook` fuera de contexto dice cuándo se
crea, se destruye, se activa, se minimiza, se restaura o cambia de nombre una ventana,
**sin inyectar ninguna DLL en otros procesos y sin un proceso residente**.

Dos cosas que hay que hacer bien o no funciona:

- **El delegado se guarda en un campo estático.** Si se deja que lo recoja el recolector
  de basura, la siguiente llamada de Windows salta a memoria liberada y tira el proceso.
  Es el error clásico de esta API y no da ningún aviso hasta que pasa.
- **Nada de tocar la interfaz en el callback.** Lo único que hace es encender un
  temporizador del `Dispatcher`. Los eventos llegan a ráfagas —abrir el Explorador
  dispara crear, mostrar, nombre y primer plano en pocos milisegundos— así que se marca
  «hay que mirar» y se mira una vez, 90 ms después. Y mientras el usuario arrastra una
  ventana, `EVENT_OBJECT_LOCATIONCHANGE` se ignora entero: son cientos de avisos por
  segundo y lo que interesa es dónde la suelta.

Se sueltan con `UnhookWinEvent` al cerrar. Morirían igualmente con el proceso, pero
dejarlo explícito es lo que permite decir que no hay fugas.

### Las miniaturas no son de DWM, y no es por pereza

`DwmRegisterThumbnail` es la vía oficial y la que usa la barra de tareas. **Aquí no
sirve**: una miniatura de DWM se compone encima de la ventana de destino y **no se
compone dentro de una ventana por capas**. Todas las de NubeBar lo son — la isla es
redondeada y translúcida, y eso en WPF es `AllowsTransparency`, que es `WS_EX_LAYERED`.
Usarlas significaría renunciar a la isla.

Así que se le pide a la ventana que se dibuje: `PrintWindow` con `PW_RENDERFULLCONTENT`.
Sale un mapa de bits de verdad que va **dentro** del WPF, se recorta con las esquinas
redondeadas como cualquier imagen y se desplaza con el panel. Lo que no da es vídeo en
directo, y para una vista general no hace falta.

Cada foto cuesta entre 5 y 20 ms, así que se sacan por detrás y cada tarjeta se rellena
cuando llega la suya. Mientras tanto enseña su icono, que es también lo que queda si la
foto no sale (juegos, algunas ventanas aceleradas por hardware). Y una ventana minimizada
no se fotografía porque no tiene nada que dibujar: la tarjeta lo dice en vez de dejar un
hueco gris que parece un fallo.

### La vista general es un panel, no una ventana nueva

Misma decisión que el centro de notificaciones de C3, por el mismo motivo: con la isla
horizontal se despliega el cajón y con la barra vertical `AbrirPanel` deriva a la ventana
superpuesta de C1. Los cuatro bordes salen gratis.

Lo que sí cambió es **qué enseña**: antes solo las ventanas de ESTE monitor, agrupadas
por ejecutable y recortadas por lo que cupiera; ahora todas, con su monitor escrito en la
tarjeta. Y se entera sola de que una ventana desapareció mientras está abierta, porque
escucha a `GestorVentanas.Cambio` y se da de baja al cerrarse.

El menú de cada ventana es el de la casa —el mismo `CrearMenuContextual` del carrusel— y
no un `ContextMenu` de WPF: eso sería una cajita blanca del sistema que no pega con nada
y, en una ventana `WS_EX_NOACTIVATE`, con el foco siempre a medias. Abrirlo cierra la
vista, porque los paneles son de uno en uno; es un clic más y a cambio no hay una segunda
arquitectura de menús.

Y un detalle que importa: **al activar una ventana se cierra el panel ANTES**. La vista
pide el teclado, y cerrarla devuelve el foco a quien lo tenía; si se activara primero la
ventana elegida, el cierre se lo quitaría para dárselo a la de antes.

### El ajuste a los bordes, sin capturar nada

`EVENT_SYSTEM_MOVESIZESTART` y `...MOVESIZEEND` dicen cuándo el usuario empieza y termina
de mover una ventana. Esos avisos ya los recibe el gestor, así que el ajuste no instala
ningún gancho más: escucha lo que él publica.

Mientras dura el arrastre se pregunta dónde está el puntero cada 60 ms con `GetCursorPos`.
**Eso no es capturar el ratón**: no hay gancho de entrada, no se intercepta nada, nadie
deja de recibir sus clics, y se deja de preguntar en cuanto se suelta. Es la misma
técnica con la que la isla se deja arrastrar desde B2.

La previsualización es una `VentanaSuperpuesta` —la de los paneles verticales— con un
rectángulo del color de acento dentro. Hacía falta algo que se dibuje encima de todo, que
no coja el foco y que deje pasar los clics, y eso ya existía. Montar una segunda ventana
flotante con reglas propias habría sido exactamente lo que esta fase no debía hacer.

Una ventana maximizada no se ajusta: lo que hace Windows al arrastrarla es restaurarla, y
meterse ahí sería pelearse con el gestor del sistema.

Y hay una red: si el botón del ratón ya no está pulsado pero el aviso de Windows no ha
llegado —pasa al arrastrar fuera de la pantalla—, el ajuste se cierra solo.

### Qué es «el área útil»

La del monitor menos lo que otros han reservado, que es lo que Windows llama área de
trabajo. **Si NubeBar está registrada como AppBar, su franja ya está descontada ahí**: no
hay que restarla otra vez ni saber dónde está.

Y si NubeBar está flotando —lo de serie desde B6— la franja no se descuenta, y eso
también es correcto: flotar significa precisamente ponerse encima de las ventanas. Quien
quiera lo contrario tiene «Reservar espacio» en Ajustes, que es el interruptor que ya
existe y que significa justo esto. Inventar aquí una tercera regla habría hecho que la
misma opción quisiera decir dos cosas distintas según quién preguntara.

Las mitades se calculan como «la mitad» y «lo que queda»: con `Ancho / 2` en las dos, un
ancho impar deja una raya de un píxel por el medio, y eso se ve. Hay una prueba para eso.


### Los cuatro fallos de la prueba visual

C4.1 compilaba, pasaba sus 315 comprobaciones y tenía cuatro fallos que solo se ven
usándola. Vale la pena anotar qué tenían en común: **los cuatro eran decisiones, no
dibujo** —quién se arrastra, qué cierra Esc, qué pasa al soltar, en qué orden se ajusta—
y ninguno se podía comprobar sin abrir ventanas de verdad. Por eso ninguno tenía prueba.

El arreglo no fue solo corregirlos: fue **sacar la decisión a una función pura** en cada
caso, igual que se hizo con el filtro de ventanas. Ahora los cuatro tienen prueba.

**1. La ventana ajustada se quedaba detrás.** Se colocaba bien y no se veía. Mover una
ventana no la activa —ni debe, o cualquier recolocación de fondo te robaría el foco— así
que elegir una zona desde la vista tiene que activarla aparte. Y el orden importa:

```
Restaurar  →  Colocar  →  Activar
```

Restaurar primero porque a una maximizada Windows le ignora el `SetWindowPos` y a una
minimizada la mueve sin enseñarla. Activar al final porque activar antes de colocar deja
un parpadeo en el sitio viejo. Eso es `ZonasAjuste.Plan(zona, estado, activar)`, que
devuelve los pasos y se puede comprobar sin tocar la ventana de nadie; `Ajustar` se
limita a ejecutarlos. El `activar` es un parámetro porque no toda colocación es una
elección del usuario.

**2. El icono de la cuadrícula desapareció.** El glifo estaba escrito como **carácter
literal de la zona de uso privado de Unicode** (U+E8A9, "GridView"). Al reescribir el
archivo se perdió y quedó una cadena vacía: el botón seguía funcionando y no se veía
nada — el peor tipo de fallo, porque no da error.

La corrección de fondo es no volver a escribirlo así:

```csharp
internal const int CodigoCuadricula = 0xE8A9;
internal static readonly string GlifoCuadricula = char.ConvertFromUtf32(CodigoCuadricula);
```

Construido desde el número no se puede perder, y además se puede comprobar: hay una
prueba que verifica que mide un carácter, que es el 0xE8A9 y que cae en la zona de uso
privado — fuera de ahí la fuente de iconos no lo tendría y saldría una caja.

**3. El menú contextual cerraba la vista.** Se abría como panel, y los paneles de la
isla son de uno en uno: abrir el menú cerraba la cuadrícula. Ahora se dibuja **dentro**
de la vista, en una capa por encima, exactamente como el menú de aplicaciones del
buscador resolvió lo mismo en C1. La rejilla se queda detrás, visible y en su sitio; el
clic de fuera cierra el menú sin cerrar la vista.

Dos detalles que hacían falta: cada opción cierra el menú antes de hacer lo suyo (las que
solo colocan no cierran la vista, y el menú se quedaría puesto encima), y mientras el
menú está abierto la vista **no se repinta** — bastaría con que otra ventana cambiara de
título para destruir el menú justo mientras lo estás leyendo.

Y Esc va por capas: `EscCierra(vistaAbierta, menuAbierto)` devuelve menú, vista o nada.
Si cerrara siempre la vista, abrir un menú sin querer costaría volver a abrirlo todo.

**4. Arrastrar una tarjeta movía la nube.** Faltaba la regla: con la vista abierta el
gesto es suyo. `ArrastraAlPulsar(vistaAbierta, enBoton, menuAbierto, islaArrastrable)`
decide quién se mueve, y la isla ya no empieza su arrastre si el panel abierto es la
vista.

El arrastre de tarjeta usa el patrón de la casa desde B2: `CompositionTarget.Rendering`
una vez por fotograma preguntando `Nativo.BotonIzquierdoPulsado()` y dónde está el
puntero. **No se depende de `MouseLeftButtonUp`**: si sueltas fuera del panel ese evento
no llega nunca y el arrastre se quedaría colgado con la previsualización puesta. La
previsualización es la misma que la del arrastre de ventanas de verdad.

Distinguir el aspa del cuerpo de la tarjeta es contar botones: los dos son `Button`, y lo
que caracteriza al aspa es estar **dentro** — dos botones por encima en vez de uno.

Soltar fuera de una zona no cambia nada, y un clic sin mover tampoco: eso es pulsar la
tarjeta, que es otra cosa. `AjustaAlSoltar(seMovio, zona)`.

Las comprobaciones pasan de 315 a **445**.
### Lo que queda para C4.2

- **Zonas personalizadas** al estilo de las plantillas de Plasma o FancyZones: el modelo
  (`Hueco`, `Zona`, `Colocacion`) está preparado para que una plantilla sea una lista de
  huecos, pero no hay editor ni formato guardado.
- **Dividir con el teclado** (Win+flechas propio). Hoy el ajuste es con el ratón o desde
  el menú de la vista general.
- **Recordar la pareja**: en Plasma, al ajustar una ventana a la izquierda, la otra mitad
  ofrece las candidatas. Eso necesita un estado de «sesión de división» que no existe.
- **Arrastrar entre monitores con DPI distinto**: funciona, pero la previsualización se
  calcula con el monitor del puntero y en el salto justo entre dos pantallas puede
  parpadear una vez.
- **Reordenar ventanas arrastrando dentro de la vista general.**

### Lo que se midió

`--pruebas` pasa de 180 a **445 comprobaciones**. Las nuevas, todas con fichas inventadas
salvo las que leen la lista real en modo solo lectura:

- el filtro, regla por regla, incluida la excepción `WS_EX_APPWINDOW` y las siete clases
  del sistema;
- el orden: la activa primero, después por uso reciente, y a igualdad por aplicación,
  título y handle — comprobado que no depende de cómo llegaron;
- estados, nombres de aplicación y rutas imposibles;
- las zonas: que las dos mitades suman el ancho exacto (también con ancho impar) y que
  los cuatro cuartos cubren la pantalla sin solaparse;
- qué zona pide cada punto, incluido un segundo monitor que no empieza en 0,0 y una
  pantalla diminuta;
- el área de trabajo con la barra **arriba, abajo, izquierda y derecha**, comprobando que
  lo ajustado no se mete debajo;
- DPI al 100, 125, 150 y 200 %, y dos monitores con DPI distinto;
- el alto de la vista en cuatro pantallas;
- lo que pasa cuando algo falla: handle muerto, foto imposible, icono imposible;
- y abrir y cerrar en bucle, comprobando que no quedan oyentes colgados, que no hay
  ventanas repetidas, que todas las de la lista existen y que ninguna es nuestra.


## Fase C4.1c · El parpadeo de la vista

Un fallo que solo se veía usándola y que ningún parche superficial iba a arreglar,
porque era estructural. Lo que sigue es el diagnóstico, no el remiendo.

### La vista se rehacía sola unas once veces por segundo

El síntoma era "las aplicaciones parpadean y parece que se recolocan". Las dos mitades de
la causa:

**Mitad A — se avisaba de un cambio que no había habido.** `GestorVentanas.Refrescar`
terminaba siempre en `Cambio?.Invoke()`, sin comparar nada. Y el refresco lo pedía
cualquier evento de Windows, con 90 ms de poso.

**Mitad B — al avisar se tiraba la vista entera.** `RepintarVista` hacía literalmente
`Cajon.Content = CrearVista()`: todas las tarjetas nuevas, todos los `Image` nuevos, un
`ScrollViewer` nuevo —así que el desplazamiento volvía arriba— y el orden recalculado.

Juntas ya serían malas. Lo que las convierte en un parpadeo continuo es que **se
alimentan solas**:

```
la isla se mueve o se redimensiona  (animación, abrir un panel, volver a su sitio)
      └─> SetWindowPos sobre NUESTRA ventana
            └─> EVENT_OBJECT_LOCATIONCHANGE   (hwnd de NubeBar)
                  └─> HayQueMirar()  →  90 ms  →  Refrescar()
                        └─> Cambio?.Invoke()   (aunque no haya cambiado nada)
                              └─> RepintarVista()  →  CrearVista()  →  ReajustarPanel()
                                    └─> mueve y redimensiona la ventana de la isla
                                          └─> otro LOCATIONCHANGE …
```

El gancho de eventos cubre el tramo `EVENT_OBJECT_CREATE (0x8000)` a
`EVENT_OBJECT_NAMECHANGE (0x800C)`, y **`EVENT_OBJECT_LOCATIONCHANGE` es 0x800B**: cae
dentro. El callback filtraba por `objeto != 0` —lo de dentro de una ventana— pero **no
filtraba nuestras propias ventanas**. Una ventana de NubeBar nunca entra en la lista
(`EsDelUsuario` la descarta por `EsNuestra`), así que enterarse de lo que le pasa no podía
cambiar nada de lo que se enseña: solo costaba un refresco y un repintado completo.

Y encima el orden: la instantánea ordena con **la activa primero**, así que cambiar de
aplicación desde fuera no solo repintaba, **recolocaba**. Eso es la segunda mitad de "se
mueven solas", y pasaba aunque no hubiera bucle.

#### Lo que se hizo

**En `GestorVentanas`:**

1. **Lo nuestro no se escucha.** `if (Nativo.EsNuestra(hwnd)) return;` como primera
   pregunta del callback. Esto corta el bucle en la raíz.
2. **`LOCATIONCHANGE` no refresca la lista.** Mover o redimensionar una ventana no la
   añade, no la quita y no la renombra. Se cuenta, para poder verlo, y no se refresca.
3. **Solo se avisa si cambió algo**, y se dice **qué**: cuatro huellas separadas (lista,
   títulos, estados, sitios) más la activa, y el aviso lleva `Cambios` y `Motivo`.

**En la vista:** la identidad de una tarjeta es su **HWND**, y hay reconciliación de
verdad. `ConstruirVista` corre al abrir y nada más; después corre `ReconciliarVista`, que
crea solo las que faltan, quita solo las muertas y actualiza el resto **en su sitio**.

La regla que decide qué merece tocar la rejilla está aparte y es pura, porque era una
decisión metida en un manejador —el mismo error que produjo los cuatro fallos de C4.1 y
los tres de C4.2:

```csharp
QueHacer(cambios, vistaAbierta, menuAbierto, arrastrandoTarjeta)
  → TocarLaRejilla    // SOLO si cambió la LISTA
  → SoloActualizar    // activa, título, estado, sitio
  → Aplazar           // con el menú abierto o una tarjeta cogida
  → Nada
```

Y el orden también es una función pura, `OrdenReconciliado`: **el congelado**, menos las
que ya no existen, más las nuevas **al final**. Congelado al abrir, nunca reordenado. Una
que se va deja su hueco y nadie se mueve.

Tres detalles que se ven si no se hacen:

- **El filo de la tarjeta activa va siempre puesto** y lo que cambia es su color. Con el
  grosor a 0 y a 1,4 según el foco, marcar la activa movía el contenido un píxel y medio,
  y con el foco cambiando solo eso se lee como que las tarjetas tiemblan.
- **El panel solo se remide si cambió el número de FILAS.** `ReajustarPanel` mide y, si el
  alto cambia, mueve la ventana de la isla. Llamarlo por nada era pedir un `SetWindowPos`
  gratis, y con la isla de por medio eso se ve.
- **El filtro de aplicaciones solo se vuelve a montar si cambió el conjunto de apps.**

**Las miniaturas**, con freno: se conserva la anterior mientras llega la nueva, no se
sustituye si la captura falla (`if (img == null) return`), no se toca si es el mismo
`ImageSource`, y hay un mínimo de **5 segundos** por ventana —cinco y no tres porque la
caché de `Miniaturas` vale cuatro, y por debajo de eso una petición solo da un viaje a la
caché para devolver la misma imagen—. Y lo más importante: **una activación no fotografía
nada**. Antes, cada Alt+Tab disparaba una captura por tarjeta.

### `--ventanas`: qué ve NubeBar

Aparte, una herramienta de diagnóstico: `NubeBar.exe --ventanas [informe.txt]` enumera
las ventanas de nivel superior, dice cuántas cuentan como del usuario y **agrupa las
descartadas por el motivo** que dio `GestorVentanas.EsDelUsuario`, con un ejemplo de cada
uno; después las agrupa por programa con su estado, su pantalla y su clase.

Existe porque «me faltan aplicaciones» es una queja que no se puede investigar leyendo el
código: hay que ver qué devuelve `EnumWindows` **en esa máquina** y qué regla se come cada
ventana. Sin esto lo único que se puede hacer es adivinar y cambiar el filtro a ciegas,
que es exactamente como se rompen estas cosas. Es de solo lectura: enumera, pregunta y
escribe un informe; no activa, no mueve, no cierra ni cambia el estado de nada.

En esta máquina: 422 ventanas de nivel superior, 11 del usuario, y las 411 descartadas
reparten en tres motivos (391 «no está visible», 14 «está tapada», 6 «es una ventana de
herramientas»). El filtro está sano.

### El diagnóstico, para poder volver a medirlo

Esto es lo que faltaba de verdad: **el código viejo no registraba nada al repintar**. En el
log de la versión con el fallo, la vista estuvo abierta treinta y ocho segundos
(`16:28:41` → `16:29:19`) y en ese tramo **no hay una sola línea**. Por eso no se podía
investigar sin cambiar cosas a ciegas.

Ahora cada reconciliación escribe una línea con todo lo que se pidió:

```
vista gen=3 motivo=Crear cambios=Lista, Titulos tarjetas 11->12 creadas=1
  actualizadas=11 eliminadas=0 orden=crece o encoge por los extremos
  rejilla=tocada reconstruida=no filas 3->3
vista gen=3 tarjeta CREADA hwnd=0x00060D40
vista gen=3 miniatura REEMPLAZADA hwnd=0x00060D40 motivo=Crear
```

Una reconstrucción completa sale como `reconstruida A PETICION` (el filtro, que es una
elección del usuario) o como `ANOMALIA` en el log de errores. Las dos cosas que no pueden
pasar nunca llevan su palabra: `ERROR: arrastre isla INICIADO CON LA VISTA ABIERTA` y
`ANOMALIA`.

Y los arrastres se distinguen uno a uno:

```
arrastre tarjeta INICIADO hwnd=0x...  /  destino=Mitad derecha pantalla=2 (...)  /  TERMINADO
arrastre ventana INICIADO hwnd=0x...  /  destino=...  /  TERMINADO
arrastre ventana: aviso AJENO hwnd=0x..., el gesto sigue siendo de 0x...
arrastre isla NO EMPIEZA: el gesto es de la vista general (el clic sale de la vista)
previsualización del ajuste: atravesable (WS_EX_TRANSPARENT + HTTRANSPARENT)
```

**`NubeBar.exe --diagvista [segundos]`** abre la vista, la deja quieta y cuenta:
reconciliaciones, reconstrucciones, tarjetas creadas/actualizadas/eliminadas, miniaturas
reemplazadas, veces que se remidió el panel, los eventos por tipo —y cuántos eran
nuestros— y los arrastres de cada clase. Termina con un veredicto: con la vista quieta y
nadie tocando nada, lo único que puede pasar es nada.

Arranca una NubeBar **de verdad**, con su isla, sus animaciones y sus ganchos, porque el
bucle solo existe con todo en marcha: medirlo con un arnés aparte habría medido otra cosa.
Y como solo puede haber una NubeBar a la vez, hay que parar la que esté puesta antes.

### Lo que se midió

**Treinta y seis comprobaciones nuevas**, y son justamente las reglas que antes vivían
dentro de los manejadores:

- `QueHacer`: que **nada** que no sea un cambio de lista pueda acabar tocando la rejilla,
  comprobado para todas las combinaciones de banderas; y que con el menú abierto o una
  tarjeta cogida se aplace.
- `TocaFoto`: que una activación no fotografíe nada, que una minimizada no se fotografíe
  nunca, y que cincuenta avisos en dos segundos no pidan ni una foto de más.
- `OrdenReconciliado`: que la instantánea **sí** reordene al cambiar la activa y que la
  vista **no**; que la que desaparece deje su hueco y que la nueva vaya al final.
- `Filas`, que es lo único que justifica remedir el panel, incluido no dividir por cero.
- El glifo del aspa, por su número (0xE711) y no como carácter suelto, igual que el de la
  cuadrícula en C4.1. Estaba bien escrito, pero escrito de la forma que ya se perdió una
  vez.
- Y el contrato nuevo del gestor: ocho refrescos seguidos sin tocar nada no dan ocho
  avisos.

#### Y una advertencia sobre el total

**El número que imprime `--pruebas` no es una constante.** Unas pocas comprobaciones
recorren las aplicaciones abiertas de la máquina —`la aplicación "X" de la lista de
filtros existe`, y las que leen la lista real en modo solo lectura—, así que el total sube
y baja con lo que haya abierto en ese momento. Con once aplicaciones abiertas dio 519; con
siete, 479.

Lo que tiene que ser **cero** es la cuenta de fallos. Citar el total como si fuera fijo es
cómodo y engaña: si mañana sale un número distinto, lo primero que hay que mirar es
cuántas aplicaciones hay abiertas, no el código.

### Lo que queda por comprobar a mano

La parte que no se puede automatizar, porque hace falta arrastrar ventanas de verdad en un
escritorio de verdad: la vista quieta treinta segundos con muchas aplicaciones, cambiar el
foco desde fuera, arrastrar una tarjeta a la mitad derecha y a una esquina, y arrastrar el
Bloc de notas por su barra de título a los cuatro bordes y cruzando de monitor. El
`--diagvista` cubre la primera con números; las demás dejan su rastro en el log.

## Fase C4.2 · Ajustar al arrastrar

Lo primero de la lista de C4.1, y lo que separa «tengo una vista general» de «tengo un
gestor de ventanas»: arrastrar una ventana a un borde y que se coloque, con una
previsualización antes de soltar.

Las cuentas ya estaban hechas. `ZonasAjuste` sabe desde C4.1 a dónde va cada zona, qué
zona pide un punto y en qué orden hay que restaurar, colocar y activar. Lo que faltaba
era enterarse del arrastre y dibujar la promesa.

### Enterarse sin capturar nada

Windows avisa de que alguien ha empezado a mover una ventana
(`EVENT_SYSTEM_MOVESIZESTART`) y de que la ha soltado (`...MOVESIZEEND`), y esos avisos
ya los recibe `GestorVentanas`. Así que aquí **no se instala ningún gancho más**: se
escucha lo que él publica.

Mientras dura el arrastre se pregunta dónde está el puntero cada 60 ms con
`GetCursorPos`. Eso **no es capturar el ratón**: no hay gancho de entrada, nadie deja de
recibir sus clics y se deja de preguntar en cuanto se suelta. Es la misma técnica con la
que la isla se deja arrastrar desde B2, y la razón de no usar `MouseMove` es la de
siempre: el ratón lo tiene capturado la ventana que se está moviendo, no nosotros.

La previsualización es una `VentanaSuperpuesta`, la de los paneles de la barra vertical:
hacía falta algo que se dibuje encima de todo, que no coja el foco y que deje pasar los
clics, y eso ya existía. Montar una segunda ventana flotante con reglas propias habría
sido exactamente la deuda que la Fase 7 quiere no tener.

Y solo la isla **jefa** vigila. Con dos monitores hay dos `MainWindow`, el arrastre es
uno solo, y sin esto se pelearían por enseñar dos previsualizaciones de lo mismo.

### Los tres fallos de escribirlo dentro del manejador

La primera versión compilaba y no tenía ni una prueba, por un motivo concreto: las
decisiones estaban **dentro de los manejadores de eventos**, donde no hay forma de
comprobarlas sin arrastrar ventanas de verdad en el escritorio de alguien. Es literalmente
el mismo error que produjo los cuatro fallos de C4.1, cometido otra vez en la fase
siguiente. Sacarlas fuera encontró tres:

**1. El aviso de otra ventana cancelaba el arrastre.** `MoverSeTermina` salta por
*cualquier* ventana que termine de moverse, no solo por la que se arrastra. El manejador
paraba el reloj, borraba el estado y escondía la previsualización **antes** de comprobar
si la ventana era la suya, así que una recolocación de fondo en mitad del gesto —otra
aplicación acomodando una ventana, el propio Windows— dejaba el arrastre sin ajustar y la
previsualización apagada. Ahora la primera pregunta es de quién es el aviso:

```csharp
AlTerminar(enCurso, soltada, zona)
  → NoEsNuestro       // no es la que vigilábamos: no se toca nada
  → SoltarSinAjustar  // es la nuestra, pero no acabó en ninguna zona
  → Ajustar
```

**2. La previsualización prometía una pantalla y el ajuste caía en otra.** El rectángulo
se dibuja con el monitor **del puntero** (`Monitores.DelRaton`), pero al soltar se llamaba
a `ZonasAjuste.Ajustar(ventana, zona)` sin pasarlo, y ahí el monitor se deduce de la
ventana con `MonitorFromWindow`. En el salto entre dos pantallas esas dos cosas no son la
misma: el cuerpo de la ventana sigue en la de origen mientras el ratón ya empuja el borde
de la otra. Se veía la mitad izquierda de la segunda pantalla y la ventana se colocaba en
la primera.

El arreglo es guardar el monitor con el que se dibujó y pasarlo: **la previsualización es
una promesa y tiene que decidir ella**. Hay prueba de que lo colocado en la segunda
pantalla cae dentro de la segunda pantalla, para las siete zonas.

**3. Los clics no la atravesaban, aunque el comentario dijera que sí.**
`ShowActivated = false` solo evita que robe el foco al aparecer; para que el ratón ni la
vea hace falta `WS_EX_TRANSPARENT`, que es `Nativo.Atravesable` y ya existía para la
escena de arranque. Durante el arrastre no se nota —el ratón lo tiene capturado la
ventana que se mueve— y se nota justo al terminar, con la previsualización todavía puesta
mientras Windows reacomoda. El estilo necesita el hwnd, que no existe hasta el primer
`Show()`, así que se pone ahí y una sola vez.

Y de paso, el reloj del ratón se para al cerrar: cerrar NubeBar en mitad de un arrastre
dejaba un `DispatcherTimer` preguntando por el puntero. `DesmontarAjusteDeBordes` va
**antes** de `GestorVentanas.Parar()`, porque los oyentes son nuestros y se quitan en el
orden inverso al que se pusieron.

### Lo que no se toca

**Una maximizada no se ajusta.** Lo que hace Windows al arrastrar una maximizada es
restaurarla; meterse ahí sería pelearse con el gestor del sistema por el mismo gesto.

**No se activa al soltar.** `ZonasAjuste.Ajustar` sabe activar la ventana si la
colocación es una elección del usuario, y aquí lo es, pero se le pasa `activar: false` a
propósito: la ventana que se arrastra **ya es la del frente**, así que activarla es un
paso de más, y si `SetForegroundWindow` fallara, `Nativo.Activar` haría el truco del Alt
sintético para nada. Desde la vista general sí se activa, porque allí la ventana elegida
puede estar detrás — que es el fallo 1 de C4.1.

**Y se puede apagar.** `Barra.AjustarAlArrastrar`, de serie puesto. Quien ya use el
ajuste de Windows o FancyZones no quiere dos cosas peleándose por el mismo borde.

### Lo que se midió

`--pruebas` pasa de 446 a **482 comprobaciones**. Las nuevas son las dos decisiones y la
promesa de la previsualización:

- cuándo se vigila un arrastre, regla por regla, y que apagarlo en Ajustes gana sobre
  cualquier combinación de las otras tres;
- qué se hace al soltar, incluido que el aviso de otra ventana no es asunto nuestro —
  comprobado para las ocho zonas, ninguna puede terminar en «Ajustar» con la ventana
  equivocada;
- y que lo prometido en la segunda pantalla cae dentro de la segunda pantalla, con la
  mitad de **su** ancho y empezando donde empieza ella.

### Lo que queda

Sigue pendiente todo lo de la lista de C4.1 —zonas personalizadas, dividir con el
teclado, recordar la pareja, reordenar dentro de la vista general— y una cosa nueva: el
**margen de 12 px es el de Windows, no el del usuario**. Si se quiere más tolerancia hay
que sacar `ZonasAjuste.Margen` a la configuración, y eso se hará cuando sea un ajuste de
widget de la Fase 7 y no una casilla más en la ventana de siempre.


## Fase C4.2b · Los tres arrastres

El otro fallo que salió al probarla a mano: el arrastre acababa en quien no debía, y a
veces lo que se movía era la nube. Tres causas distintas, una por cada forma de confundir
los gestos.

**2a. La isla decidía antes de que la tarjeta pudiera hablar.** El arrastre de la isla se
engancha en `Isla.PreviewMouseLeftButtonDown`, y los eventos **Preview van de la raíz a la
hoja**: cuando la tarjeta se entera del clic, la isla ya ha empezado su arrastre. Poner
`e.Handled` en la tarjeta llega tarde **por definición**. La única comprobación que había
era `_panelAbierto == PanelVista`, que depende de un campo de estado y no del gesto.

Ahora son tres, y cada una tapa un agujero distinto: el panel abierto, **que el clic salga
del árbol visual de la vista** (`EnLaVista`, subiendo por el árbol desde
`e.OriginalSource`) y que no haya ya un ajuste en marcha. Y si el arrastre de la isla
llegara a empezar con la vista abierta, el log lo dice con la palabra `ERROR`.

**2b. Los dos ajustes compartían estado.** El arrastre de tarjeta y el de ventana real
usaban los mismos `_zonaPrevia`, `_monitorPrevio` y `_previa`. Cerrar la vista en mitad del
arrastre de una ventana real llamaba a `PararArrastreDeTarjeta`, que borraba la zona y
escondía la previsualización; al soltar ya no había zona y **el gesto se perdía en
silencio**. La previsualización tiene ahora **dueño** (`DuenoPrevia.Tarjeta` o `.Ventana`):
quien no la tiene, no la puede apagar.

**2c. La previsualización podía comerse el clic.** El comentario decía que los clics la
atravesaban y no era verdad. Hacen falta las tres cosas:

1. `IsHitTestVisible = false` en el contenido — la mitad de WPF. No basta: un `Background`
   puesto, **aunque sea `Transparent`**, hace que la ventana cuente para el ratón.
2. `WS_EX_TRANSPARENT` — la mitad de Win32. Tampoco basta sola: hay rutas
   (`WindowFromPoint`, un bucle de arrastre que pregunta por su cuenta) que le mandan el
   `WM_NCHITTEST` igual.
3. **Contestar `HTTRANSPARENT` al `WM_NCHITTEST`** — decir explícitamente "yo no estoy
   aquí, pregunta a la de debajo". Es lo único que cierra el caso, y es lo que impide que
   la previsualización se convierta en la ventana bajo el puntero.

Es **opt-in** (`VentanaSuperpuesta.HacerseAtravesable`): la misma clase es la de los
paneles de la barra vertical, que sí tienen que recibir clics y escribir en el buscador.

Y de C4.2 siguen puestas las dos reglas que ya se arreglaron: el aviso de fin de **otra**
ventana no cancela el gesto, y el monitor del puntero se mantiene explícito desde la
previsualización hasta el ajuste — ahora también en el arrastre de tarjeta, que lo pasa a
`Ajustar(h, z, monitor)`.


### Lo que se midió

**Treinta y seis comprobaciones** para el ajuste al arrastrar, las del grupo
`ajuste: arrastrar una ventana a un borde`: cuándo se vigila un arrastre regla por regla,
que apagarlo en Ajustes gane sobre cualquier combinación de las otras tres, qué se hace al
soltar —incluido que el aviso de **otra** ventana no es asunto nuestro, comprobado para
las ocho zonas— y que lo prometido en la segunda pantalla caiga dentro de la segunda
pantalla, con la mitad de **su** ancho.

Sobre el total que imprime `--pruebas`, ver la advertencia de C4.1c: no es una constante,
porque unas pocas comprobaciones recorren las aplicaciones abiertas de la máquina. Con
siete abiertas, el árbol completo da 515 y ninguna mal.

## Rediseño, fase 0 · El catálogo de colores y los nueve temas

El plan es `docs/REDISENO-VISUAL.md`. Lo que decidí yo al construir sus cimientos:

**Los temas y el catálogo comparten UN juego de nombres.** `temas-de-serie.json` declara 19
claves (`acento`, `superficie`, `filo-isla`…) y el catálogo tiene ~130, las 19 incluidas con
ese mismo nombre. Así `Pintura` resuelve cualquier clave con la misma cascada —usuario, tema,
derivación— sin preguntar si la clave es «base» o «de la interfaz», y un tema del usuario
puede declarar `deslizador.pomo` si le da la gana sin que haya que inventar un segundo
mecanismo. La alternativa (dos espacios de nombres, uno para el tema y otro para la interfaz)
obligaba a mapear uno sobre otro, y ese mapa es exactamente donde se pierden las claves.

**Las derivaciones preguntan a `Pintura`, no al tema en bruto.** `boton.fondo` sale de
`tarjeta`, que sale del tema. Si cada derivación mirara el tema directamente, cambiar
`tarjeta` en el Estudio no movería los botones, y el usuario vería que un selector de color
no hace lo que dice. El precio es que una derivación puede dar vueltas sobre sí misma: hay un
corte con contador (`Pintura.Ciclos`) y una prueba que exige que sea 0 en los nueve temas.

**El formato de color es el de CSS.** `rgba(12,14,20,.94)` y
`linear-gradient(90deg,#FF2A6D,#FCEE0A,#00F0FF)` se leen tal cual, porque es como están
escritos los 9 × 19 valores de la maqueta aprobada. Inventar una sintaxis propia habría
significado traducir a mano 171 valores, que es donde se cuela la errata que nadie vuelve a
ver. `Relleno` es el tipo: liso o degradado, serializable y medible (un degradado sabe dar su
color medio ponderado por tramos, que es con lo que se mide contraste).

### Las dos correcciones de contraste

Las pruebas de los nueve temas encontraron **33 claves ilegibles**, todas en los cuatro temas
CLAROS. Treinta eran fallos de mis derivaciones, no de la maqueta, y el patrón era siempre el
mismo: *un velo del color del texto no sirve para apagar nada en un tema claro*. En un tema
oscuro la tinta es clara y un velo al 45 % sobre fondo negro se ve; en un tema claro la tinta
es oscura y el mismo velo sobre fondo blanco da un gris a 2,3:1. Lo que había:

- el punto de una app abierta era `tinta` al 45 % → es `tenue` corregida a 3:1;
- el punto de la app activa era el acento crudo → es `acento-tinta`, que es para lo que la
  maqueta lo trae («el acento cuando hace de texto o icono»);
- el pomo del interruptor era blanco, sobre una pista gris clara: 1,5:1, un interruptor sin
  pomo → es el blanco o el negro que más se lea sobre la pista apagada (en claro sale oscuro,
  que es lo que hacen los interruptores claros de Windows);
- «desactivado» y «días de otro mes» se apagaban un 35-40 % hacia el fondo y se caían por
  debajo de 3:1 → se apagan igual y luego se les devuelve lo justo para llegar al mínimo. Un
  botón desactivado tiene que ser no pulsable, no invisible.

Las otras tres eran de la maqueta: Sakura (2,13:1), Otoño (3,00) e Invierno (3,45) escriben en
**blanco sobre su acento**. Resuelto en dos mitades, consultado antes de tocar nada:

1. **A `sobre-acento` se le exige 3:1 y no 4,5.** Rotula un control relleno —el glifo de play
   en su círculo, la cifra de la insignia, la palabra de un segmento activo—, no un párrafo.
   Con 4,5 el aviso saltaba en Otoño y en Invierno, que se ven perfectamente.
2. **Sakura cambia su blanco por su propia tinta ciruela `#4A3B5C`** (4,77:1). A 3:1 seguía sin
   pasar, y el color ya estaba en su paleta, así que el tema sigue siendo Sakura. Es la **única**
   desviación respecto a `docs/maqueta-rediseno/temas.json`; está apuntada en el propio
   `temas-de-serie.json` («correcciones») y la vigila una prueba con nombre, para que nadie
   «restaure el valor de la maqueta» dentro de seis meses sin saber por qué se cambió.

La regla se ajustó donde era injusta y el tema se corrigió donde estaba mal; no al revés.

## Rediseño, fase 0 · Una sola cascada, y el centinela

**`Roles` pasa a ser una fachada, no a desaparecer.** El plan decía «ampliar `Roles.cs` hasta
cubrir cada elemento». No se puede: `Roles` es una lista fija de propiedades (`Superficie`,
`AcentoTinta`…) y lo que hace falta son ~130 claves con zona, papel y derivación, buscables
por nombre desde una lista en Ajustes. Eso es un diccionario, no un objeto con campos.

Así que el catálogo es nuevo (`Catalogo` + `Pintura`) y `Roles` se queda como **fachada**:
cada rol con nombre viejo es ahora una clave del catálogo. Lo que esto compra es que los
widgets, `HyprPinta`, `Tonos` y los paneles no se toquen —ni una línea— mientras su color ya
sale del catálogo y el usuario ya puede cambiarlo. Y lo que esto es: temporal. A medida que las
fases 1 a 8 pasen cada pieza a preguntar por su clave, la fachada se queda sin consumidores.

**Los temas de antes entran por un puente, no por una segunda cascada.** Los ocho de siempre
(Nube, Claro, Tokyo Night, Gruvbox…) son tres colores más diez roles opcionales; los nuevos son
paquetes de 19 valores. Mientras convivan había que elegir: dos cascadas en paralelo, o una y un
puente. Dos cascadas es incumplir «no hay colores escritos a mano» desde el primer día, con dos
sitios donde arreglar cada cosa. El puente (`TemaBase.DeTemaViejo`) **declara** lo que el tema
viejo callaba, con la cuenta que se le aplicaba antes (0x1E para una tarjeta, 0x33 para la de
debajo del ratón), para que los ocho temas no cambien de aspecto el día que se enchufó el
catálogo. Cambiarles el tema a quien no ha pedido nada no es migrar: es estropearlo.

### El centinela, y lo que decide que es un color

`Pruebas.SinColoresAMano.cs` lee `MainWindow*.cs`, `VentanaAjustes*.cs` y `Biblioteca.cs` y
falla si la interfaz decide un color por su cuenta. Hace falta porque un color escrito a mano no
da error, no se ve raro y es lo más cómodo del mundo de escribir: se quitaron veinticinco, y el
siguiente entra el día que tenga prisa y ponga un `Brushes.Tomato` porque «es solo un aviso».

Lo difícil no fue escribirlo, fue decidir qué cuenta. Tres cosas que parecen colores y no lo son:

- **`Color.FromArgb(0x2A, color.R, color.G, color.B)`** no es un color: es una **opacidad**
  aplicada a un color que ya venía del tema. La regla del centinela es exactamente esa — si
  todos los argumentos son números, es un color decidido aquí; si alguno viene de una variable,
  es un velo.
- **`Brushes.Transparent`** es estructural. Sin `Background` puesto, un `Panel` de WPF no recibe
  el ratón en sus huecos: no se pinta nada, se declara que ahí se puede pulsar.
- **Las máscaras de opacidad** del carrusel. En una `OpacityMask`, negro significa «opaco» y
  transparente «no se ve»: son un uno y un cero, no paleta. Cambiarlos por un color del tema
  rompería el desvanecido de los bordes.

La salida de emergencia es el comentario `// color literal a propósito`. No es un agujero: es la
obligación de escribir por qué. Hoy lo llevan las cuatro máscaras del carrusel y el pincel que
`FormattedText` pide para poder dibujar cuando solo se le pregunta el ancho de un texto.

Y lo que el centinela **no** puede hacer: leerse el código desde un exe publicado, porque allí
no hay código. Busca las fuentes hacia arriba desde el ejecutable y, si no las encuentra, lo
escribe en el informe y no falla. Donde importa —esta máquina, donde se escribe cada cambio y se
pasa `--pruebas` antes de publicar— están siempre. La alternativa era un paso de MSBuild que
mirara los fuentes en cada compilación; se descartó porque un centinela que puede romper la
compilación de un release por una falsa alarma cuesta más de lo que cuida.

### Por qué el cambio de fuente de verdad no se partió en dos

`Tema.Id` y `Tema.Colores` no entraron en esta tanda, y no por falta de sitio. Hoy el selector
de temas de Ajustes escribe `Tema.Fondo`, `Tema.Texto` y `Tema.Acento`. Si `Tema.Id` empieza a
mandar antes de que Ajustes escriba `Id`, elegir Gruvbox deja de hacer nada; y si no manda, hay
dos fuentes de verdad para el mismo color, que es el problema que todo esto venía a quitar.

Así que el config, la migración, el selector de los nueve temas y Ajustes > Colores son **un
solo paso** y van en la misma versión. Partirlo deja la barra en un estado en el que un ajuste
que existe no hace nada, y eso es peor que no haberlo empezado.

## Rediseño, fase 0 · El cambio de fuente de verdad

Lo que pintaba la barra eran `Tema.Fondo`, `Tema.Texto` y `Tema.Acento` más diez roles.
Ahora es **`Tema.Id`** (uno de los nueve temas) más **`Tema.Colores`** y **`Tema.Radios`** (lo
que el usuario le haya cambiado), resuelto por el catálogo.

**Fue un solo paso y no cuatro, a propósito.** El selector de temas de Ajustes escribía
`Tema.Fondo`. Si `Tema.Id` empieza a mandar antes de que Ajustes escriba `Id`, elegir un tema
deja de hacer nada; y si no manda, hay dos fuentes de verdad para el mismo color, que es el
problema que todo esto venía a quitar. Así que el config, la migración, el selector de los
nueve temas, Ajustes > Colores y la retirada de los ocho temas de antes entran juntos.

### La migración, clave por clave

El plan fija el tema: `normal` y `neon` → Profesional; `nube` → Anime (Sakura) si no se
tocaron colores. Lo que el plan no cerraba es qué pasa con los colores de los otros dos casos,
y la respuesta es la única defendible: **se conservan siempre que el usuario los haya
elegido**. Tirarle los colores a quien lleva meses con Gruvbox porque su estilo era «normal»
sería estropearle la configuración, no migrarla.

Y al revés: **lo que nadie eligió no se arrastra.** Un `#E6000000` que está ahí porque es el
valor por defecto no es una decisión. Si se arrastrara, todo el mundo se quedaría con la isla
negra de siempre y no vería nunca el tema nuevo — la migración habría «funcionado» y el
rediseño no se vería en ninguna máquina. Se mira clave por clave, así que quien solo cambió el
acento conserva el acento y estrena el resto.

**No se borra nada.** `Tema.Estilo` «desaparece» de donde importa —nadie lo lee para pintar—
pero del fichero no se toca, y las trece claves de color se quedan donde están. Sí se han
quitado de la clase `Tema`: trece propiedades que nadie lee son código muerto. El fichero las
conserva porque `ConfigArbol` conserva lo que no conoce; borrar claves del config de alguien es
lo que este proyecto lleva varios accidentes aprendiendo a no hacer.

### Tres cosas que habrían quedado mintiendo

Al cambiar de dónde sale el color, tres sitios se quedaban prometiendo algo que ya no hacían.
Ninguno daba error:

- **El deslizador «Radio de esquinas»** escribía `Tema.Radio`, y el radio pasó a ser un valor
  del tema (23 en Profesional, 0 en Videojuegos y Cyberpunk, 25 en Sakura). Habría quedado un
  deslizador que se mueve y no mueve nada. Se arregló con la misma idea que los colores: una
  capa de personalización (`Tema.Radios`, por pieza) que va ENCIMA del tema, y un botón para
  devolverle el radio al tema.
- **Los perfiles** pueden escribir `Tema.*`, así que un perfil con `Tema.Acento` se habría
  aplicado «bien» sin cambiar ningún color. Esas trece claves están ahora declaradas retiradas
  y se informan como ignoradas, que es el canal que ya existía («válido, N clave(s)
  ignoradas»). Y como los colores viven dentro de `Tema.Colores`, se valida su contenido: cada
  clave tiene que estar en el catálogo y cada valor tiene que entenderse.
- **La escena de arranque** leía `Tema.Acento` del árbol. Ahora pide el acento a la cascada,
  igual que la barra, así que la escena entra del color con el que la barra va a salir — que es
  lo que esa escena existe para anunciar.

### «Se pregunta», sin un cuadro de diálogo

El plan dice que al cambiar de tema se pregunta si conservar las personalizaciones. Se pregunta
**dentro de la página**, con dos botones y un «Dejarlo», y no con un `MessageBox`. No por gusto:
este producto no tiene ni un cuadro de diálogo del sistema en más de doscientos ficheros —cerrar
una app, apagar y probar un perfil se confirman dentro de su propia superficie— y éste habría
sido el primero, además del que menos falta hace: la pregunta cabe donde está la decisión.

### Lo que Ajustes > Colores todavía no hace

- **Los degradados se enseñan pero no se editan.** `SelectorColor` da un color; un degradado son
  paradas, ángulo y opacidad. Es el apartado 3 del Estudio de temas (fase 9). Mientras tanto una
  clave con degradado se ve, se puede restablecer y se puede sustituir por un color liso.
- **«Elegir en pantalla»** (pulsar un elemento de la barra y que se abra su color) pide que cada
  cosa pintada sepa de qué clave salió. Eso llega cuando las fases 1 a 8 pasen cada pieza a
  preguntar por su clave; hacerlo antes sería mantener a mano un segundo mapa «elemento →
  clave», y ese mapa se desincroniza el primer día.
- **La lista va en desplegables por zona**, con «Colores base» abierto y el contenido de cada
  zona construido la primera vez que se abre. Ciento treinta filas con su selector de color de
  golpe es una página que tarda en abrirse y en la que no se encuentra nada. El buscador pasa a
  una lista plana de coincidencias de todas las zonas, para poder buscar «apagar» sin saber en
  qué zona vive.
