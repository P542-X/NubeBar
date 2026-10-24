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
