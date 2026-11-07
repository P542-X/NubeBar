# NubeBar

Una barra para Windows 11 con la forma y el genio de la Dynamic Island del iPhone:
una nube negra que vive arriba del todo, se expande cuando le pasas el ratón por
encima y despliega paneles desde dentro de sí misma. Sustituye a la barra de tareas
de Windows y se configura con un único `config.json`, al estilo de un *rice* de Linux.

![La isla recogida](docs/isla-recogida.png)

Recogida solo enseña el reloj. Al acercar el ratón se abre:

![La isla abierta](docs/isla-abierta.png)

## Qué hace

**La barra**
- Modo *isla* (flotando, centrada) o modo *barra* (de lado a lado), arriba o abajo.
- Esconde la barra de tareas de Windows, la principal y las secundarias, y las
  devuelve intactas al cerrarse.
- **Flota o se queda su sitio, tú eliges.** De serie la nube flota y las ventanas
  pasan por debajo. Con «reservar espacio en pantalla» Windows le guarda su franja
  como a la barra de tareas y las ventanas maximizadas llegan solo hasta donde
  empieza la nube — una appbar de verdad, y vale igual en modo isla que en modo
  barra.
- Módulos colocables en tres zonas (izquierda, centro, derecha): inicio, apps,
  reloj, fecha, CPU, RAM, batería, música, bandeja, control, escritorios,
  wallpaper, iconos, separador.
- Borde y halo del color de acento, con animaciones de muelle a la velocidad de
  refresco del monitor.
- **Se aparta cuando molesta**: la coges y la arrastras donde quieras. Mientras
  la tengas cogida no se abre, y se queda donde la dejes todo el tiempo que
  sigas con ella; en cuanto le quitas el ratón de encima, vuelve sola a su
  sitio con un muelle.
- La isla **toma la forma de lo que enseña**: con un panel estrecho se queda
  recogida en vez de estirarse de lado a lado.

**Las apps**
- Carrusel con las ventanas abiertas, agrupadas por programa, con rueda para
  desplazarse y un límite configurable de iconos visibles.
- Clic para activar, clic en la que ya está delante para minimizarla, y una
  mini lista para elegir entre las ventanas de un mismo programa. Sale como una
  **tarjeta pegada a su icono**, del ancho del título más largo, no como una
  franja de punta a punta de la pantalla.
- **Apps fijadas**: clic derecho sobre un icono para anclarlo. Se quedan
  siempre, abiertas o no; cerradas se ven atenuadas y al pulsarlas se abren.
- **Se reordenan arrastrándolas**: coges un icono dentro de la isla, lo llevas
  donde quieras y los demás se apartan. El orden se guarda y aguanta reinicios.
- **Cerrar desde la isla**: clic derecho en un icono para cerrar esa app, o
  **forzar su cierre** como con «Finalizar tarea». Clic derecho en la isla para
  cerrarlas todas. Las tres piden confirmación antes de hacer nada.
- **Esconder la isla**: clic derecho en la isla → «Esconder la isla» y se va de
  todas las pantallas. Vuelve al pulsar la tecla Windows.
- Vista general tipo Exposé con todas las ventanas, con atajo global.
- **Ajustar ventanas a los bordes**: arrastra una ventana al borde izquierdo o
  derecho y se queda en media pantalla; a una esquina, en un cuarto; arriba,
  maximizada. Antes de soltar se ve dónde va a caer, y lo que se ve es lo que
  pasa: el ajuste cae en la pantalla del puntero, no en la de origen. Respeta el
  área de trabajo, así que con «Reservar espacio» puesto no se mete debajo de la
  isla. Se puede apagar en Ajustes.
- `NubeBar.exe --ventanas [informe.txt]` cuenta qué ventanas ve y por qué
  descarta las demás, agrupadas por programa. Es de solo lectura —no activa, no
  mueve, no cierra nada— y está para que «me faltan aplicaciones» se pueda
  investigar con los datos de la máquina en vez de cambiando el filtro a ciegas.
- `NubeBar.exe --diagvista [segundos]` abre la vista general, la deja quieta y cuenta
  qué le pasa: reconciliaciones, reconstrucciones completas (que son una anomalía),
  tarjetas creadas o eliminadas, miniaturas reemplazadas y de qué tipo de evento de
  Windows viene cada aviso. Hay que parar antes la NubeBar que esté puesta, porque
  arranca una de verdad: el parpadeo que vino a medir solo existía con la isla y sus
  animaciones en marcha.

**El menú Inicio propio**
- Se abre con la tecla Windows (interceptada sin romper ninguna combinación `Win+…`).
- Buscador de aplicaciones, de ventanas abiertas, de archivos (índice de Windows
  Search), calculadora y búsqueda web.
- Apps fijadas, saludo, reproductor de música, gráficas de CPU y RAM, silencio,
  No molestar y botones de energía.

![El menú Inicio](docs/menu-inicio.png)

**Varias pantallas**
- Una isla en cada monitor, cada una con su propio DPI y su propio ancho.
- Cada isla enseña solo las apps de su pantalla; al mover una ventana de monitor,
  su icono viaja de una isla a la otra con animación.
- El menú Inicio, la vista general y los ajustes se abren en la pantalla donde
  tengas el ratón.
- Enchufar, quitar o girar un monitor recoloca las islas sin reiniciar nada.

**Pantalla completa**
- Si algo ocupa una pantalla entera (F11, un vídeo, un juego), la isla de ESA
  pantalla se desliza hacia arriba y desaparece; la otra sigue en su sitio.
- Con el ratón pegado al borde superior unos 300 ms, asoma.
- Una ventana simplemente maximizada no la esconde.

**Bandeja del sistema**
- Los iconos de la bandeja (Discord, Steam, OneDrive, el antivirus…) con su
  tooltip, su clic izquierdo, su menú de clic derecho y su doble clic.
- Se actualizan en vivo y entran y salen con animación; si hay muchos, un
  desplegable `^` que nace de la isla.
- Al cerrar NubeBar los iconos vuelven solos a la bandeja de Windows.

**Centro de control**
- Iconos de idioma del teclado, red, volumen y batería; al pulsarlos, un panel al
  estilo del centro de control del iPhone.
- Volumen general (deslizador con su cifra, botones `−` / `+` de 5 en 5 y rueda) y
  **por aplicación**, selector de salida de audio, Bluetooth con sus dispositivos
  emparejados y brillo por DDC/CI.
- **Rueda sobre el altavoz** de la isla: sube y baja el volumen sin abrir nada.
- **Teclas de volumen del teclado**: el aviso sale en la isla y no el de Windows
  (`Barra.TeclasVolumen`, se apaga en Ajustes).
- **Idioma del teclado**: si tienes más de una distribución puesta en Windows, sale
  su código (`ES`, `EN`…). Clic para pasar a la siguiente, como Win+Espacio, o
  rueda para ir y volver; en el centro de control, todas para elegir.

**Batería**
- Al pulsar la pila: porcentaje, si está cargando y lo que queda, el ahorro de
  batería y el **modo de energía** (Eficiencia / Equilibrado / Rendimiento).

**Apps en segundo plano**
- Un botón `^` junto a la hora que despliega los iconos de las apps en segundo
  plano, como los iconos ocultos de Windows. Viene de serie (módulo `bandeja`).

**Red**
- Al pulsar el icono de red: lo que tienes conectado (wifi o cable) con su IP, su
  velocidad de enlace y el adaptador.
- Las **wifis a la vista** con su señal y candado. Clic para conectarte: si ya
  habías entrado antes, directo; si no, pide la contraseña ahí mismo. Si la
  contraseña no vale, no se queda guardada. Las redes de empresa (802.1X) se
  abren en los ajustes de Windows. Botón para desconectarse y para volver a buscar.
- Primero salen **las que ya conoces** (en negrita) y después las demás por señal,
  para que la tuya no se pierda entre las de los vecinos. Se ven cinco de golpe
  y el resto con la rueda: así la isla no se convierte en un muro aunque haya
  quince redes alrededor. El título dice cuántas hay en total.
- **Test de velocidad** integrado: ping, bajada y subida contra
  speed.cloudflare.com, con las cifras moviéndose en vivo. Tarda unos 15 s.
- Lo que tu equipo no admite no aparece: sin batería no hay icono de batería, y
  si el monitor no contesta a DDC/CI no hay deslizador de brillo.

![El centro de control](docs/centro-control.png)

**Avisos en la isla**
- Al cambiar el volumen, la isla se estira y enseña una barra animada 1,5 s.
- Las notificaciones de Windows asoman unos segundos con el nombre de la app,
  el título y el texto; al pulsarlas se abre la app. Respeta «No molestar» y
  puedes silenciar apps concretas. Si te salen repetidas (la de Windows y la de la
  isla), mira [Si las notificaciones salen dos veces](#si-las-notificaciones-salen-dos-veces).

**Escritorios virtuales**
- Numeritos tipo *workspaces* de Linux, con el actual en color de acento.
- Clic para cambiar, rueda para ir al siguiente o al anterior y `+` para crear.
- Opcionalmente, el carrusel enseña solo las apps del escritorio actual.

**Si la matan, el shell vuelve igual**
- Un proceso guardián (el mismo ejecutable con `--guardian`) vigila a NubeBar.
  Si la cierras desde el Administrador de tareas, o se cuelga, devuelve las dos
  barras de tareas y el «ocultar automáticamente» en menos de medio segundo,
  sin reiniciar el Explorador.

**Wallpaper Engine**
- Detecta el fondo actual y puede sacar de él la paleta de colores (tipo pywal).
- Pausa, silencio y ocultar iconos desde la isla.
- Puede mandarle tu color de acento al propio wallpaper.

**Ventana de ajustes**
- Todo lo anterior se configura con ratón y con vista previa en vivo.
- Temas listos, selector de color HSV, exportar e importar temas.

![La ventana de ajustes](docs/ajustes.png)

## Instalar

De la [última Release](../../releases/latest), con instalador o sin él:

**Con instalador** — descarga `NubeBar-Setup.exe` y ábrelo. Se instala para tu
usuario en `%LOCALAPPDATA%\Programs\NubeBar`, **sin pedir administrador**, y
deja marcado que arranque con Windows (si no, te quedas sin barra en el
siguiente reinicio). Se quita desde "Aplicaciones instaladas" como cualquier
otro programa.

**Sin instalador** — descarga `NubeBar.exe`, muévelo a una carpeta fija y ábrelo.
No necesita .NET: lleva todo dentro. Ojo con abrirlo desde la descarga del
navegador: Chrome y compañía lo ejecutan desde una carpeta temporal suya y la
borran a los pocos segundos.

En los dos casos, la primera vez se crea `%APPDATA%\NubeBar\config.json` con los
valores por defecto, y el instalador no lo toca ni al actualizar ni al
desinstalar.

### Windows va a avisarte, y conviene saber por qué

La primera vez saldrá una pantalla azul a pantalla completa:

> **Windows protegió su PC**
> Microsoft Defender SmartScreen impidió el inicio de una aplicación no reconocida.

No es un virus ni un error: es que el instalador **no lleva firma digital**. Una
firma es un certificado que hay que comprarle a una autoridad cada año, y este
proyecto no tiene ninguna. Sin ella, Windows no sabe quién hizo el programa, y a
todo lo que no reconoce le pone ese cartel — le pasa a cualquier programa
pequeño el día que sale.

Para continuar: **Más información** → **Ejecutar de todas formas**. El botón de
«Más información» es el que hace aparecer el de ejecutar; si no lo pulsas, solo
verás «No ejecutar».

Y ya que estás confiando en un programa que **esconde tu barra de tareas**, lo
honesto es decirte cómo comprobarlo tú en vez de pedirte que te fíes:

- El código está entero en este repositorio. Ningún binario se sube a mano: el
  `NubeBar.exe` y el `NubeBar-Setup.exe` de cada Release los compila
  [el workflow de GitHub](.github/workflows/release.yml) a partir de la etiqueta,
  en una máquina limpia, y se puede ver el registro de cada compilación.
- Puedes compilarlo tú (ver [Compilar](#compilar)) y usar tu propio ejecutable.
- NubeBar no se instala como administrador, no toca el registro de Windows más
  allá de su propia desinstalación, y lo único que escribe fuera de su carpeta
  es `%APPDATA%\NubeBar`.
- Si algo va mal y te quedas sin barra, lee
  [Si te quedas sin barra de tareas](#si-te-quedas-sin-barra-de-tareas): hay un
  vigilante que te la devuelve, y una forma de recuperarla a mano.

Para que arranque con Windows (o para que deje de hacerlo):
**Ajustes → Sistema → Arrancar con Windows**.

## Compilar

Hace falta el SDK de .NET 10.

```powershell
git clone https://github.com/P542-X/NubeBar.git
cd NubeBar
dotnet build
```

Para generar el ejecutable autónomo de un solo archivo:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
               -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
               -o dist
```

Variables de entorno útiles para depurar:

| Variable | Qué hace |
|---|---|
| `NUBEBAR_DIAG=1` | Apunta en el log el ancho de la isla y los grupos de apps en cada tick |
| `NUBEBAR_DIAG2=1` | Añade el detalle del carrusel |
| `NUBEBAR_SINMASCARA=1` | Desactiva el truco de la tecla máscara del gancho de teclado |
| `NUBEBAR_SIMULAR_ENERGIA=1` | Bloquear, suspender, reiniciar y apagar solo se apuntan en el log |
| `NUBEBAR_NOTIS_PRUEBA=<carpeta>` | Lee las notificaciones de esa carpeta en vez de las de Windows |

El log vive en `%APPDATA%\NubeBar\nubebar.log`.

## Configurar

El archivo es `%APPDATA%\NubeBar\config.json` y se recarga en caliente: al guardarlo,
la isla se adapta sola. Las claves que no reconoce se conservan tal cual.

### Tema

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Tema.Fondo` | `"#E6000000"` | Color del fondo en ARGB; los dos primeros dígitos son la opacidad |
| `Tema.Texto` | `"#FFFFFF"` | Color del texto |
| `Tema.Acento` | `"#7AA2F7"` | Color de acento: borde, halo y la rayita de la app activa |
| `Tema.Radio` | `20` | Radio de las esquinas |
| `Tema.Fuente` | `"Segoe UI"` | Tipografía |
| `Tema.TamanoFuente` | `13` | Tamaño del texto |
| `Tema.DesdeWallpaper` | `false` | Saca los colores del wallpaper actual |
| `Tema.EnviarColorAlWallpaper` | `false` | Manda tu acento a Wallpaper Engine como `schemecolor` |
| `Tema.BordeAcento` | `true` | Filo de 1 px y halo suave del color de acento |

En una instalación nueva el tema que entra es **Nube** (coral, naranja y azul nube
sobre azul profundo). Si ya tienes `config.json`, tu tema no se toca.

#### Roles de color

Todos opcionales: lo que no pongas se **deriva** del fondo y del texto, así que un
tema puede seguir siendo tres colores y nada cambia. Se ponen cuando el tema quiere
mandar, como hace Nube con su naranja y su azul, que no se pueden sacar del acento.

| Clave | Qué hace |
|---|---|
| `Tema.Superficie` | Tarjetas y filas dentro de un panel |
| `Tema.SuperficieAlta` | Lo que está bajo el ratón o elegido |
| `Tema.Borde` | Separadores y filos |
| `Tema.TextoAtenuado` | Segundo renglón, unidades, pistas |
| `Tema.Secundario` | Detalles cálidos; si falta, el propio acento |
| `Tema.Sistema` | Red, volumen, batería; si falta, el propio acento |
| `Tema.Positivo` | Verde de "todo bien" |
| `Tema.Aviso` | Ámbar de "ojo" |
| `Tema.Negativo` | Rojo de "esto va mal" |

Cada acento tiene además su versión para **texto**, que no se configura: es el mismo
color corregido para que se lea sobre el fondo de verdad (WCAG, 3,5:1 sobre la isla y
4,5:1 sobre una superficie clara). Por eso un coral que vale de relleno se profundiza
solo cuando hace de letra.

### Animaciones

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Animaciones.Nivel` | `"auto"` | `"auto"` (sigue a Windows), `"completo"`, `"equilibrado"` (las acorta un 40 % y quita el rebote), `"reducido"` (sin movimiento, pero con fundidos muy cortos) o `"desactivado"` (sin nada: ni movimiento ni fundidos) |
| `Animaciones.Robot` | `"primera-vez"` | Cuándo se ve la escena de entrada: `"primera-vez"`, `"cada-inicio"`, `"al-cambiar-tema"` o `"nunca"`. La escena todavía no está implementada |

Con `"auto"` se lee **Accesibilidad → Efectos visuales → Efectos de animación** de
Windows: quien ya ha pedido menos movimiento ahí no tiene que volver a pedirlo aquí.

### Barra

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Barra.Modo` | `"isla"` | `"isla"` (flotante) o `"barra"` (de lado a lado) |
| `Barra.Posicion` | `"arriba"` | `"arriba"` o `"abajo"` |
| `Barra.Alto` | `38` | Alto de la fila de módulos |
| `Barra.Margen` | `8` | Separación con el borde de la pantalla |
| `Barra.OcultarBarraWindows` | `true` | Esconde la barra de tareas de Windows mientras NubeBar esté abierta |
| `Paneles[].ReservarEspacio` | sin elegir (flota) | Reserva su franja, como la barra de tareas, para que no la tapen las ventanas maximizadas. Vale en los dos modos; con `autoocultar` no se reserva |
| `Barra.MaxApps` | `10` | Iconos de apps visibles a la vez; el resto, con la rueda |
| `Barra.AgruparApps` | `true` | Las ventanas del mismo programa, en un solo icono |
| `Barra.AtajoVistaGeneral` | `"Ctrl+Alt+W"` | Atajo global de la vista general |
| `Barra.AjustarAlArrastrar` | `true` | Arrastrar una ventana a un borde la ajusta ahí, con previsualización |
| `Barra.Monitores` | `"todos"` | `"todos"` (una isla por pantalla) o `"principal"` |
| `Barra.AppsPorMonitor` | `true` | Cada isla enseña solo las apps de su pantalla |
| `Barra.OcultarEnPantallaCompleta` | `true` | La isla se esconde si algo ocupa su pantalla entera |
| `Barra.OcultarConMaximizada` | `false` | Se esconde también con una ventana maximizada (juegos, vídeos) |
| `Barra.Bandeja` | `true` | Traer los iconos de la bandeja del sistema (hace falta el módulo `bandeja`) |
| `Barra.MaxBandeja` | `0` | Iconos de bandeja visibles antes del desplegable `^`; con `0`, solo el `^` (como los iconos ocultos de Windows) |
| `Barra.Notificaciones` | `true` | Las notificaciones de Windows asoman en la isla |
| `Barra.NotificacionesSilenciadas` | `[]` | Trozos de identificador de app que no avisan (p. ej. `"Discord"`) |
| `Barra.AppsDelEscritorio` | `false` | El carrusel, solo con las apps del escritorio virtual actual |
| `Barra.AppsFijadas` | `[]` | Apps siempre a la vista, en este orden. Con el módulo `fijadas` puesto se ven también con la isla recogida; si lo quitas, van en el carrusel. Rutas de ejecutable, o `shell:AppsFolder\<id>` para las de la Store |
| `Barra.OrdenApps` | `[]` | El orden que les has dado arrastrando los iconos; se escribe solo |
| `Barra.Arrastrable` | `true` | La isla se aparta arrastrándola, y los iconos se reordenan igual |
| `Barra.SegundosParaVolver` | `1` | Tras quitarle el ratón de encima, cuánto tarda en volver a su sitio |
| `Barra.EscalaPaneles` | `0` | Tamaño de paneles y menús. `0` = según la pantalla (en un 1920x1080 al 125 % salen al ~86 %); `0.8` más pequeños; `1` tal cual |

### Módulos

Listas de nombres; el orden es el orden en que salen.

```json
"Modulos": {
  "Izquierda": ["inicio"],
  "Centro":    ["fijadas", "apps", "reloj"],
  "Derecha":   ["cpu", "ram", "bateria", "bandeja", "control", "fecha"]
}
```

Disponibles: `inicio`, `apps`, `fijadas`, `reloj`, `fecha`, `cpu`, `ram`, `bateria`,
`musica`, `musica-caratula`, `musica-barras`, `bandeja`, `control`, `escritorios`,
`wallpaper`, `wallpaper-titulo`, `iconos`, `separador`.

La lista sale del registro de widgets, así que es la misma en el config, en
**Ajustes → Módulos** y en el log. `musica-caratula` y `musica-barras` son la carátula
y las barras por separado, para quien no quiera las dos juntas.

Hay además una clave `Paneles`, y desde B5 es **la autoridad** de la geometría de la
nube: borde, modo, grosor, margen, visibilidad, en qué monitores sale y si reserva
espacio. Nace sola la primera vez a partir de `Barra` —nadie nota el cambio— y a
partir de ahí `Barra.Modo`, `Posicion`, `Alto`, `Margen`, `ReservarEspacio` y
`Monitores` se *derivan* de ella al cargar y no se escriben nunca en el fichero: están
en la tabla de arriba porque las leen sesenta y pico sitios del código. Si editas esas
claves a mano no pasará nada; lo que manda es `Paneles`. Existe en plural para que
añadir un segundo panel, o uno lateral, no obligue a cambiar el `config.json` de
nadie.

La zona del **centro se ve siempre**, con la isla recogida o desplegada; la
izquierda y la derecha solo al desplegarse. Por eso ahí viven las apps: `fijadas`
(los iconos de `Barra.AppsFijadas`, estén abiertas o no) y `apps` (las que tienes
abiertas ahora mismo). Están a un clic sin acercar el ratón, y no se mueven de
sitio al desplegarse la isla, porque el centro es la única zona que no se desplaza.
Si prefieres la isla pequeña en reposo, muévelos a la izquierda en Ajustes > Módulos.

### Menú Inicio

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Inicio.Fijadas` | `[]` | Rutas de las apps ancladas |
| `Inicio.TeclaWindows` | `true` | La tecla Windows abre este menú en vez del de Windows |
| `Inicio.BuscarArchivos` | `true` | Busca también archivos con el índice de Windows Search |
| `Inicio.BuscadorWeb` | Google | Prefijo de la búsqueda web |
| `Inicio.Saludo` | `true` | Cabecera con «Buenas tardes, …», la fecha y la hora |

### Wallpaper

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Wallpaper.Ruta` | `null` | Ruta de Wallpaper Engine; vacío = detectarla desde Steam |
| `Wallpaper.Monitor` | `0` | Monitor del que se leen los colores (si solo hay una isla) |

## Si te quedas sin barra de tareas

NubeBar esconde la barra de Windows y la pone en «ocultar automáticamente» mientras
está en marcha, y la devuelve a como estaba al cerrarse. Si la matas de golpe (o se
cuelga) puede quedarse escondida. Para recuperarla:

1. **Lo primero, prueba a abrir NubeBar y cerrarla bien** (clic derecho en la isla →
   Salir). Al cerrarse limpiamente devuelve la barra y el estado guardado en
   `%APPDATA%\NubeBar\barra-estado.txt`.

2. Si no, reinicia el Explorador de Windows:

   ```powershell
   Stop-Process -Name explorer -Force
   ```

   Windows lo vuelve a abrir solo con su barra de tareas de siempre.

3. Si la barra vuelve pero sigue en «ocultar automáticamente», quítalo en
   **Configuración → Personalización → Barra de tareas → Comportamientos de la barra
   de tareas → Ocultar automáticamente**.

**La tecla Windows** también vuelve sola: el gancho de teclado se desinstala al cerrar
y, si el proceso muere, Windows lo descarta por su cuenta. Si aun así no responde,
reinicia el Explorador con el comando de arriba.

## Si las notificaciones salen dos veces

Es lo esperado con la configuración de serie de Windows. NubeBar no recibe las
notificaciones directamente: las lee de la base de datos donde Windows las guarda (la vía
oficial exigiría empaquetarla y firmarla). Así que se entera **después** de que Windows
haya enseñado su propio aviso, y no puede impedirlo. Salen las dos.

Para quedarte con una sola:

**Solo las de la isla (recomendado).** Quita el aviso emergente de Windows, pero deja que
las notificaciones se sigan guardando:

1. **Configuración → Sistema → Notificaciones**.
2. Entra en cada app que te avise (Discord, correo, WhatsApp…).
3. Desactiva **Mostrar banners de notificación**.
4. **No** desactives *Mostrar notificaciones en el centro de notificaciones*: es de ahí de
   donde las lee NubeBar, y sin eso dejaría de verlas también.

Windows 11 no tiene un interruptor general para los banners; hay que hacerlo app por app.

**Solo las de Windows.** Pon `Barra.Notificaciones` a `false` (o apágalo en Ajustes).
NubeBar deja de leerlas del todo: ni asoman en la isla ni llegan a su centro de
notificaciones.

> **No uses «No molestar» para esto:** NubeBar lo respeta, así que callaría los avisos de
> los dos (las de la isla se siguen guardando en su centro de notificaciones).

## Licencia

**NubeBar es gratis, pero no es de código abierto.** El texto completo está en
[LICENSE](LICENSE); en corto:

| | |
|---|---|
| ✅ Usarlo gratis | en los equipos que quieras, en casa o en el trabajo |
| ✅ Copiarlo y compartirlo | tal cual viene, sin cobrar |
| ✅ Leer el código y compilarlo | para ti |
| ❌ Venderlo | ni suelto, ni dentro de un pack o un servicio de pago |
| ❌ Publicar versiones modificadas | puedes trastear en tu máquina; distribuir el resultado, no |
| ❌ Quitarle la autoría | el nombre y los avisos viajan con el programa |

El código está a la vista por un motivo concreto, y es el de la sección de
arriba: un programa que sustituye a tu barra de tareas tiene que poder
auditarse. Que se pueda leer no significa que se pueda revender ni republicar
con cambios.

Para cualquier otro uso, pídelo abriendo una issue.

### Lo que viene de fuera

Esa licencia cubre el código de NubeBar. Los componentes de terceros conservan
la suya, más permisiva, y puedes seguir usándolos por tu cuenta bajo sus propios
términos:

| Componente | Licencia |
|---|---|
| [ManagedShell](https://github.com/cairoshell/ManagedShell) | Apache-2.0 |
| Microsoft.Data.Sqlite | MIT |
| [Slions.VirtualDesktop](https://github.com/Slion/VirtualDesktop) | MIT |
| System.Data.OleDb | MIT |
| M PLUS Rounded 1c · Nunito · Orbitron · Rajdhani · Shippori Mincho · Zen Maru Gothic | SIL Open Font License 1.1 |

Las tipografías viajan con su licencia al lado, en `Fuentes/OFL-*.txt`, que es
lo que la OFL pide.
