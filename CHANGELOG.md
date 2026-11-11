# Notas de versión

Las versiones publicadas están en [Releases](../../releases). Cada una trae el
instalador (`NubeBar-Setup.exe`) y el ejecutable suelto (`NubeBar.exe`), los dos
construidos por el workflow de Release a partir de la etiqueta.

---

## v1.1.0

### Añadido

- **Modo notch.** Un tercer modo de nube, además de isla y barra: pegada al borde sin
  margen, redondeada solo por el lado que da a la pantalla y con el fondo opaco, como la
  muesca de un MacBook. Hace todo lo que hace la isla —se centra, se abre al pasar el ratón,
  los paneles salen de dentro— porque **es** una isla: lo único que cambia es la silueta.

  El radio sigue saliendo del tema, que es la regla de `Forma.cs`. Con Cyberpunk, que va a
  radio 0, la muesca sale rectangular, y eso no es un fallo: es Cyberpunk.

  Lo que lo ha hecho barato cabe en una línea: `EsIsla` ha pasado de preguntar
  `Modo == "isla"` a preguntar `!EsBarra`. Con la forma de antes, los diecisiete sitios que
  preguntan `EsIsla` —el ancho fijado a mano, la alineación centrada, `Expandir`, la
  edición, el margen— se habrían ido todos por la rama de la barra sin dar un solo error, y
  habría salido una barra de lado a lado donde se pidió una muesca. Preguntando por el
  negativo, la notch hereda los diecisiete.

- **Lo que suena, en la isla.** Un módulo nuevo, `spotify`: carátula, canción y artista, y
  al pulsarlo un panel con la carátula, el avance y los tiempos.

  Funciona sin configurar nada y **sin que salga nada de tu equipo**: todo viene de la
  sesión de medios de Windows, la misma de la que viven los módulos `musica` de siempre. Así
  que vale para Spotify, para el navegador y para cualquier reproductor. Y con
  `Musica.SoloSpotify` se calla con todo lo que no sea Spotify, que tampoco necesita cuenta:
  quién reproduce lo dice la propia sesión.

  **Solo existe en modo isla y notch**, y lo dice la biblioteca antes de que lo añadas: vive
  del ancho elástico de la isla, y en una barra de lado a lado el título se estira de punta
  a punta de la pantalla.

- **Y dos capas que se construyeron y se quitaron**, antes de publicar ninguna de las dos.
  Se apuntan porque las dos se van a volver a pedir:

  **La API de Spotify**, con inicio de sesión por OAuth, que añadía el nombre del álbum, la
  carátula a 640 px y el corazón de «Me gusta». Spotify dejó de aceptar URL de retorno por
  HTTP en loopback (`http://127.0.0.1:PUERTO`) para aplicaciones registradas desde abril de
  2025, aunque su documentación siga diciendo que valen. Lo que quedaba era HTTPS en
  loopback con un certificado autofirmado —con su pantalla roja de aviso en el navegador,
  delante del usuario— o un esquema propio que su documentación de la Web API no contempla.
  Tres adornos no valen eso.

  **La letra sincronizada**, desde LRCLIB. Esa funcionaba: traía la letra y seguía el verso
  al milisegundo sin una consulta más de las que ya se hacían, extrapolando la posición en
  local entre lecturas de la sesión de medios. Se quitó por cómo quedaba: el verso vivía en
  una línea de la isla que crece y se encoge con cada palabra, así que la barra respiraba al
  ritmo de la letra. Una barra que se mueve mientras la lees no se puede leer, y eso no se
  arregla afinando el ancho — la forma buena pide una superficie de alto fijo que no mueva
  la isla, y eso es otro trabajo.

  De todo aquello quedan dos cosas, que son la parte que interesa: `EsSpotify` bajó a
  `Musica.cs` —saber QUIÉN reproduce nunca necesitó cuenta, es una cadena que la sesión de
  medios ya nos da— y al quitar las capas de arriba no se movió nada de abajo. Que es como
  se descubre que las capas estaban bien puestas.

### Y la casa, más ordenada

- Un widget puede declarar **en qué modos existe** (`DescriptorWidget.SoloEnModos`), y la
  biblioteca lo cuenta antes de que lo añadas en vez de dejarte un hueco sin explicación.
  Es el mismo razonamiento que `PuedeNoVerse`, aplicado a algo que se sabe sin montar nada:
  las dos frases salen ahora juntas por `Descriptor.Avisos`, que es lo que leen la
  biblioteca, el editor, Ajustes y `--diagmodulos`.
- La validación del modo en los perfiles y el desplegable de Ajustes han dejado de llevar la
  lista de modos escrita a mano: preguntan a `PanelNube.Modos`. Las dos copias se habrían
  quedado atrás el día de la notch, y el fallo es del silencioso: un perfil con un modo
  bueno rechazado por un sitio que nadie mira.
- `--diagpaneles` mide la notch y `--capturas` la fotografía arriba y abajo, que es la única
  forma de comprobar una silueta.

---

## v1.0.1

### Arreglado

- **El pulso de audio se quedaba con la forma del tema anterior.** Con Cyberpunk, que va a
  radio 0, la isla es un rectángulo — pero al sonar música el filo que late salía
  redondeado y asomaba por las cuatro esquinas.

  El filo se creaba copiando el radio de la isla, y se crea **una sola vez y tarde**: la
  primera que suena algo. Bastaba con haber tenido música en un tema redondo para que
  naciera con aquel radio y se quedara con él. Ahora va atado al de la isla, que no es un
  apaño para que se refresque: la forma del pulso no es una decisión suya, es la de la
  isla, y copiarla era dejar que dos copias de lo mismo se contradijeran.

  Los colores del pulso tenían el mismo fallo, menos visible: nacían con los del tema de
  aquel momento. Ahora se refrescan al cambiar de tema.

### Y la casa, más ordenada

- `ARCHITECTURE.md`: el programa explicado por partes, en inglés. Qué es cada pieza, dónde
  vive y por qué está hecha así, con una tabla final que es el mapa del repositorio.
- Fuera los documentos de fase, que eran hojas de ruta de cosas ya hechas.

---

## v1.0.0 — la primera pública

NubeBar sale de casa. Es la misma barra que llevaba meses funcionando en una sola
máquina, con la diferencia de que ahora se puede instalar sin conocer a quien la
escribió: licencia, instalador firmado por el workflow a partir de la etiqueta,
README en español y en inglés, y la interfaz entera en los dos idiomas con una
pregunta la primera vez que la abres.

Lo que trae, en una línea: una barra para Windows 11 con la forma de la Dynamic
Island, que sustituye a la de Windows, se configura con un solo `config.json` y
trae su propio menú Inicio, centro de control, bandeja, vista general de
ventanas, nueve temas, dock, paneles laterales, fondos de escritorio, perfiles,
cambio de entrada de vídeo por DDC/CI y un organizador de ventanas al estilo de
los gestores de mosaico de Linux.

Y un guardián que te devuelve la barra de Windows si algo va mal, que es lo
mínimo que se le puede pedir a un programa que te la esconde.

---

Lo de aquí abajo es el historial de desarrollo, de antes de publicarlo. Se queda
porque explica por qué las cosas son como son, no porque haga falta leerlo.

---

## v1.0.9

### Cambiado

- **Ya no llueve nada en el menú Inicio.** Siete de los nueve temas de serie soltaban
  partículas cayendo por dentro del menú: pétalos en Sakura, pétalos rojos en Samurái,
  copos en Invierno, hojas de arce en Otoño, lluvia en Sad, lluvia de neón en Cyberpunk y
  píxeles subiendo en Videojuegos. Fuera los siete. Los temas se quedan con la misma
  animación que Profesional —el halo y el destello del filo, que ya se pintan con el color
  de cada uno— más lo suyo que no cae: la mascota, la marca de agua del Samurái, el glitch
  del reloj y las líneas de barrido de Cyberpunk.

  El motor sigue estando: si quieres partículas en un tema **tuyo**, el argumento
  `particulas:` se sigue leyendo. Lo que no las lleva es ninguno de los de serie.

- **Un clic fuera cierra cualquier panel, no cuatro.** Esto era una lista de altas —red,
  bandeja, batería, notificaciones— y los otros seis se quedaban abiertos hasta volver a
  pulsar su icono. El problema de una lista así es que cada panel nuevo nace fuera sin que
  nadie lo decida, y desde fuera no se ven cuatro paneles con una regla y seis sin ella: se
  ve una barra que unas veces se cierra y otras no.

  Ahora cierran todos, y la excepción es una sola: el menú Inicio, que ya tiene su propio
  vigilante con las mismas reglas. Y al cerrarse así, la isla se recoge si el ratón sigue
  fuera — antes se quedaba desplegada hasta que volvías a pasar por encima y salir.

### Arreglado

- **El círculo de los deslizadores se cortaba por la mitad al 100 %.** El brillo, el
  volumen, el mezclador por aplicación y los deslizadores de los paneles laterales: todos
  salen del mismo componente. El círculo mide 14 px y se colocaba centrado sobre el punto
  del porcentaje, así que al 100 % su centro caía en el filo de la barra y la mitad derecha
  quedaba fuera, donde la recortan. El extremo izquierdo no fallaba porque tenía un tope
  puesto a mano — un lado sujeto y el otro suelto.

  Ahora lo que el círculo recorre no es la barra entera sino la barra menos su propio
  diámetro, que es como funciona cualquier deslizador: al 0 % empieza en el filo, al 100 %
  termina en el filo, y al 50 % su centro sigue cayendo en la mitad exacta.

- **Las filas de redes wifi salían redondeadas por la derecha y cuadradas por la izquierda.**
  La fila llevaba un margen de −6 px para que el fondo del ratón llegara al filo de la
  tarjeta, y eso la sacaba por fuera del `ScrollViewer` de la lista, que recorta. Lo primero
  que se pierde de una caja redondeada al recortarla son sus esquinas. El desplazamiento se
  ha movido a la lista entera: se ve en el mismo sitio y con las cuatro esquinas.

- **Los separadores ya casi no tiemblan.** Medido con capturas cada segundo sobre la
  pantalla vertical, las rayas se movían en 5 de cada 14 muestras; ahora, en 1. El ancho de
  la isla sube a múltiplos de cuatro, así que un respiro de uno, dos o tres píxeles cae en
  el mismo escalón y la isla ni se entera — y de paso el centrado deja de caer a medio
  píxel, que era lo que además las desdibujaba.

  **No está cerrado del todo.** Lo que queda no es la isla: son dos módulos que respiran un
  píxel por su cuenta —`fijadas` y el grupo `batería · control · monitores`— y como la zona
  central va centrada, esos 2 px se reparten uno a cada lado. Se arreglará donde está, en
  el módulo.

---

## v1.0.8

### Cambiado

- **La nube puede ocupar su sitio también en modo isla.** «Reservar espacio en pantalla»
  existía desde hace versiones, pero solo hacía algo en modo barra: en modo isla lo
  encendías, Ajustes lo guardaba en el `config.json` y no pasaba nada. Ahora el interruptor
  vale en los dos modos y en los cuatro bordes.

  Apagado —como viene de serie— la nube **flota**: las ventanas pasan por debajo y una
  ventana maximizada la tapa. Encendido, Windows le guarda su franja igual que a la barra de
  tareas y las ventanas maximizadas llegan solo hasta donde empieza la nube. Ya no hay que
  cambiar de modo para elegir una cosa o la otra, que era la única forma que había.

  Una isla, aunque sea estrecha y esté centrada, reserva la franja **entera** de su borde:
  una appbar de Windows solo entiende de bordes de pantalla, no de trozos. Y lo que se
  reserva es el grosor de la nube **recogida**, así que expandirse al pasar el ratón no mueve
  las ventanas de todo el escritorio. Con «ocultar automáticamente» no se reserva nada,
  aunque el interruptor esté encendido.

  Y el interruptor **se ha mudado a donde se busca**: estaba en Sistema, dentro del grupo
  «Barra de tareas de Windows» —donde se leía como si fuese cosa de la barra de Windows—, y
  ahora está en **Apariencia → Forma**, pegado a «Modo» y «Posición», que es donde se decide
  la geometría de la nube. En Sistema queda una línea que dice dónde encontrarlo.

### Arreglado

- **La entrada de vídeo desaparecía de la barra con dos monitores.** `Entradas.Disponible()`
  guardaba la respuesta en un campo y después la **releía** para devolverla. Con dos
  pantallas hay dos islas preguntando a la vez, y un cambio de monitores llama a
  `Replantear()`, que pone ese campo a nulo: si caía justo en medio saltaba
  `InvalidOperationException: Nullable object must have a value`, el trabajador se moría a
  media comprobación y el módulo «Monitores» no se ofrecía — aunque los monitores sí
  contestaran al DDC/CI. Ahora la respuesta de cada llamada va en un local y el campo es
  solo la caché.

- **Una app podía quedarse con su inicial en vez de su icono.** Abrías Discord y en la
  barra salía una «D». El icono de una app abierta se le pide a su VENTANA
  (`WM_GETICON`, con 50 ms de plazo), y una ventana recién creada todavía no lo ha puesto
  —ni una app ocupada contesta a tiempo, por ejemplo con un juego comiéndose la máquina—.
  Encima, la caché de iconos guardaba también los FALLOS, y para siempre: un tropiezo de
  medio segundo condenaba a esa app a su letra hasta reiniciar NubeBar.

  Ahora, si la ventana no contesta, el icono se le pide al shell a partir del ejecutable
  —el mismo sitio del que ya salía el de una app fijada y cerrada—, y en la caché solo
  entran los aciertos: los fallos se apuntan con la hora y se reintentan a los cinco
  segundos. En cuanto la ventana conteste, se queda con el suyo.

- **Los separadores parpadeaban y se movían un par de píxeles.** Se veía sobre todo
  jugando. `RefrescarApps` vive en los actualizadores, así que el ancho de la isla se
  repreguntaba **cada segundo**, y el listón para rehacerlo era medio píxel: como medir
  texto da decimales, el reloj pasando de minuto bastaba para lanzar una animación de
  280 ms de la isla entera. Y la isla va centrada, así que todo lo de dentro se corría esa
  pizca. En una raya de 1 px eso se ve como un parpadeo y un salto de lado a lado.

  Ahora **crecer se hace siempre** —quedarse corto recorta un icono o la hora— y **encoger
  solo si sobran 3 px o más**, que es el lado barato: que sobre un pelo de isla no se ve, y
  moverla entera para quitarlo sí. Además el ancho se redondea a píxel entero y la fila de
  módulos cae en la rejilla de píxeles, que es lo que quita el parpadeo de las rayas de
  1 px.

- **NubeHypr dejaba hueco para una barra que no estaba.** Con «reservar espacio» encendido y
  la visibilidad en «ocultar automáticamente», Windows no guardaba la franja (eso ya estaba
  bien) pero el reparto de ventanas sí la descontaba, y los paneles laterales se metían
  debajo de la nube. Eran dos respuestas distintas para la misma pregunta, leídas de dos
  sitios; ahora las dos salen de `ReservaHueco`.

---

## v1.0.7

### Añadido

- **Cambiar la entrada de vídeo del monitor desde la barra:** un módulo nuevo, «Monitores»,
  que habla con el propio monitor por el cable (DDC/CI, VCP `0x60`) y le dice a cuál de sus
  conectores ponerse. Pasar el monitor a HDMI para jugar en la consola y devolverlo a
  DisplayPort, sin agacharse a buscar el botón de debajo de la pantalla.

  Por cada monitor enseña su modelo de verdad —leído de su EDID, no el «Generic PnP Monitor»
  que devuelve Windows—, si DDC/CI responde, en qué entrada está, y un botón por cada entrada
  que **el monitor anuncia**: si no las anuncia, no se inventan botones y queda el campo de
  código manual. Después de mandar el cambio vuelve a leer el `0x60` para comprobarlo, y
  distingue las tres respuestas que no son lo mismo: cambió, lo aceptó y sigue donde estaba, o
  lo aceptó y dejó de contestar (lo normal al quitarle la señal a la pantalla donde vives).

  Se le puede poner un mote a cada monitor («PS5», «Monitor principal»), guardado contra su
  identidad del EDID y no contra `DISPLAY1`, que Windows reasigna al reiniciar.

  El icono solo sale si algún monitor del equipo gestiona su entrada. Nada de esto ocurre en
  el hilo de la interfaz, y NubeBar nunca cambia una entrada por su cuenta: solo cuando lo
  pulsas.

  **No es una capturadora.** Cambia el canal del monitor; no trae a Windows la imagen que
  llega por HDMI. La consola se sigue viendo en el monitor, no en una ventana.

- **`NubeBar.exe --diagmonitores`:** qué monitores hay, quiénes son según su EDID y qué
  entradas gestionan, preguntado por el cable. De solo lectura: para mandar un cambio hay que
  pedirlo con `--cambiar <clave|nº> <código hex>`, y entonces además comprueba que ningún otro
  monitor se haya movido.

### Arreglado

- **Pulsar una app en la barra no siempre la traía delante.** Había que ir a «ver todas las
  ventanas», clic derecho y colocarla en un sitio para que apareciera — y ese rodeo era la
  pista: colocar usa `SetWindowPos`, que no pide permiso, mientras que traer al frente usa
  `SetForegroundWindow`, que sí. Windows niega ese permiso a quien no acaba de recibir
  entrada del usuario, y las ventanas de NubeBar no toman el foco al pulsarlas, así que para
  Windows el proceso que lo pedía era uno que no estabas tocando. No se notaba porque
  `SetForegroundWindow` devuelve «hecho» aunque solo parpadee el botón en la barra de tareas,
  y eso se daba por bueno. Ahora se comprueba el resultado y, si hace falta, se insiste
  enganchándose a la cola de entrada de la ventana que tiene el foco; si aun así Windows no
  lo suelta, la ventana se sube en el orden Z para que al menos se vea.

- **El menú del clic derecho de «ver todas las ventanas» no salía donde pulsabas y aparecía
  cortado.** Lo primero, porque se colocaba al lado de la TARJETA y no del cursor. Lo segundo,
  porque sus catorce opciones miden unos 488 px y el panel unos 320: solo se topaba el borde
  de arriba, así que el menú empezaba dentro y se iba por abajo, y las dos últimas opciones
  —«Cerrar» y «Forzar el cierre»— quedaban fuera y no se podían pulsar. Ahora sale en la punta
  del cursor, se vuelca al otro lado cuando no cabe y se desplaza por dentro en vez de
  cortarse. Con la tecla de menú contextual sigue saliendo al lado de la tarjeta, sin taparla:
  por teclado no hay cursor que seguir.

- **En el panel de red, la fila bajo el cursor daba un brinco.** El icono del wifi crecía un
  8 % al pasar por encima. En una lista eso mueve el punto que el ojo usa para seguir dónde
  está, y se lee como un fallo de dibujo. Ahora la fila solo se ilumina, que es lo que ya
  hacía la plantilla por su cuenta.

---

## v1.0.6

### Arreglado

- **El neón de los paneles laterales salía con las esquinas en ángulo recto:** un
  rectángulo de luz alrededor de un panel con las puntas redondas. El halo estaba
  enganchado a la tarjeta del panel, y la tarjeta recorta su contenido para que no asome
  por las esquinas; WPF recorta el resultado del efecto con ese mismo recorte, que es el
  RECTÁNGULO de la caja. Ahora el halo sale de una pieza aparte detrás del panel, con su
  misma silueta y sin recortar, así que sigue las curvas. Es el mismo arreglo que ya
  llevaban la isla y las tarjetas de dentro de los paneles.

---

## v1.0.5

### Añadido

- **Cerrar ventanas una a una desde la nube:** en la mini lista que sale sobre un programa
  con varias ventanas (por ejemplo, varias carpetas abiertas), cada fila lleva una ✕ que
  cierra solo esa ventana, sin tener que traerla delante primero. Se cierra por las
  buenas: si hay algo sin guardar, el programa lo pregunta.

---

## v1.0.4

### Arreglado

- **La isla parpadeaba al abrir y cerrar paneles con la barra abajo:** la ventana tenía
  que subir su filo de arriba y crecer a la vez, y al ser transparente Windows enseñaba un
  fotograma el dibujo viejo en el sitio nuevo. Ahora, con la barra abajo, la ventana mide
  siempre lo que mediría con el panel más alto y abrir o cerrar ya no la mueve.
- **Tirones al abrir y cerrar paneles** (red, Inicio, audio…): los halos se recalculaban
  en cada fotograma de la animación. Ahora se quitan mientras dura y vuelven al terminar.
  Medido: del 55 % de fotogramas lentos al 5 %.
- **Paneles borrosos:** el halo de las tarjetas pintaba también su texto en una imagen
  intermedia, y eso lo suavizaba. Ahora el halo sale de una pieza aparte detrás del
  contenido y el texto se ve nítido.

---

## v1.0.3

### Cambiado

- **Rediseño visual:** el tema deja de ser tres colores y pasa a ser uno de los nueve de
  serie, que pinta todas las superficies (isla, paneles y Ajustes) y decide también la
  forma. Fuera los estilos Normal, Nube y Neón. Ver `docs/REDISENO-VISUAL.md`.
- Los gestos de borde funcionan también dentro de un juego.

### Arreglado

- **Confirmación de apagar/reiniciar cortada:** salía en una fila nueva debajo de los
  botones de energía, que son lo último del Inicio, y quedaba por debajo del borde del
  panel. Ahora ocupa el sitio de los botones, y vuelven al cancelar.
- Ajustes > Colores ya no es un muro de opciones, y el escritorio actual vuelve a su azul
  con el número legible.
- El halo cuadrado que asomaba por las puntas de la isla.
- La nube de la escena de entrada ya no se deforma al crecer.
- El diagnóstico de paneles ya no avisa de un desplazamiento que no existe.

---

## v1.0.2

### Cambiado

- **Menú Inicio (tecla Windows), solo lo que se usa:** el saludo con la fecha y la hora, el
  buscador, la música, el idioma del teclado y la energía (bloquear, suspender, reiniciar,
  apagar). Fuera las acciones rápidas, CPU/RAM, el fondo, la conectividad y las fijadas.

### Arreglado

- **Inicio cortado por la derecha:** con el ancla pegada al filo de la isla, el contenido
  no podía correrse a la izquierda y sus últimos 8 px quedaban fuera de la tarjeta. Vale
  para cualquier panel colgado de un icono del borde.
- **Hueco vacío al final del Inicio:** reservaba alto para 9 resultados y el buscador solo
  enseña 6. Ahora mide lo que pide su contenido.

---

## v1.0.1

### Arreglado

- **Cuelgue al cerrar y abrir la tapa del portátil.** Al volver de la suspensión, el hilo
  de .NET que reparte los avisos del sistema esperaba a la interfaz, y la interfaz le
  preguntaba el título a la ventana oculta de ese mismo hilo: cada uno esperando al otro,
  la isla congelada hasta que Windows la cerraba. Ahora los títulos se leen sin preguntarle
  nada a la ventana (`InternalGetWindowText`), como hacen las barras de tareas. Encontrado
  con la foto del vigía de cuelgues y comprobado provocándolo a propósito.

---

## v1.0.0

**La numeración empieza de cero.** Esta 1.0.0 es el mismo NubeBar que se publicó como
1.4.2 el 28/10/2026: todo lo que cuentan las notas de abajo está dentro. Las Releases y
etiquetas de la numeración anterior (de la v1.0.0 a la v1.4.2) se retiraron de GitHub; sus
notas se quedan aquí como historial.

---

# Historial — numeración anterior

Las versiones de aquí abajo ya no están publicadas como Release. Se conservan las notas para
saber de dónde viene cada cosa.

---

## v1.4.2

### Nuevo

- **Barra de desplazamiento propia** en toda la app (paneles, laterales, Ajustes): una
  pastilla fina del color del texto, sin flechas, que engorda al pasar el ratón y se pinta
  del acento al arrastrarla. Sigue el tema.

### Arreglado

- **Arranque:** la isla aparece ya completa. Antes salía a medias y la hora se quedaba
  cortada un segundo hasta que la isla se ponía al día. Ahora el ancho se mide después del
  layout y la primera pasada no espera al reloj de un segundo.
- **Dock:** ya no se ve «con zoom» ni con los iconos cortados en pantallas al 125 %; se
  coloca en píxeles reales y se compacta con la pantalla como el resto.

---

## v1.4.1

Arreglos de paneles, pantallas pequeñas y dos monitores.

### Arreglado

- **Panel de batería:** ya no se estira a toda la isla («zoom») al pulsar un modo de
  energía. Lo mismo para notificaciones, Inicio y la vista general al refrescarse.
- **Salida de audio:** al elegir otra, el panel marca «Activa» en la nueva sin cerrarlo.
- **Paneles y menús en pantallas pequeñas:** se escalan con el tamaño de la pantalla (alto
  y ancho). En un 1920x1080 al 125 % salen al ~86 %; nunca por debajo del 80 %. Se puede
  fijar a mano con `Barra.EscalaPaneles`.
- **Paneles laterales:** ya no se cortan por abajo al 125 % ni se salen al otro monitor
  con dos pantallas; también se compactan con la pantalla.
- **Iconos de la nube:** ya no se cortan por arriba al levantarse con el ratón.
- **Iconos oscuros** (ChatGPT y similares): llevan un halo claro para verse sobre la isla.
- **Inicio:** los botones de bloquear, suspender, reiniciar y apagar salían dos veces.

### Nuevo

- Clic derecho en una app abierta de la nube → **«Abrir otra ventana de …»**.
- **Vigía de cuelgues:** si la interfaz deja de responder 5 s, se apunta en el log y se
  guarda una foto del proceso en `%APPDATA%\NubeBar\cuelgues\` para poder ver dónde se
  quedó atascada.

---

## v1.3.0 — sin publicar

**Esta versión NO está publicada.** Está preparada: compilada, probada y empaquetada en
carpetas de fase. Lo que falta es la tanda de pruebas que necesitan ojos, ratón y
teclado.

NubeHypr: una capa de organización de ventanas dentro de NubeBar. **Nace apagada, y
encendida nace en simulación**: calcula, explica en el registro lo que haría y no mueve
ninguna ventana. Pasar a mover de verdad es una decisión explícita del usuario.

### Nuevo

**NubeHypr — el motor (fases D0 a D5.1)**

- Modelo puro de layouts (flotante, horizontal, vertical, principal) y 19 comandos
  semánticos, con el reparto calculado sobre bordes en vez de tamaños: sin costuras de 1 px
  ni desbordes.
- Motor real de posicionamiento con el orden seguro: destino válido → simular y volver →
  comprobar que la ventana existe → mover → **confirmar** comparando lo que se pidió con lo
  que Windows hizo.
- Lista protegida: el shell, los diálogos del sistema y nuestras propias ventanas quedan
  fuera. Las maximizadas y minimizadas también, salvo que se pida restaurarlas.
- Escritorios virtuales: leer, renombrar, reordenar y enviar ventanas, con un modo de un
  solo escritorio si la API no responde.
- Reglas por proceso, clase, título y ruta, con prioridad, especificidad y desempate por
  orden; y una **explicación por regla** de por qué coincide o se descarta.
- Las reglas conectadas al momento en que aparece una ventana, en simulación.
- Editor de atajos en Ajustes: ver los ocupados, reasignar, detectar conflictos antes de
  guardar y restaurar los de serie.

**NubeHypr — la integración visual (D6)**

- **Indicador de escritorio** con nombre, número y estado de ventanas ("2/4 · Trabajo"),
  con cuatro tallas según el sitio disponible y el estado por tamaño de punto, no por color.
- **Vista general rediseñada**: la ventana activa del sistema y la elegida con el teclado
  son dos cosas distintas y se ven a la vez (filo de acento y halo de foco). Teclado
  completo: flechas, Inicio, Fin, Entrar, Supr y la tecla de menú.
- El **menú de una tarjeta ya no la tapa**: se coloca en el lado con hueco.
- **Página de NubeHypr en Ajustes**: búsqueda, grupos, estado activo y la explicación de
  qué decidiría cada ventana abierta, con el aviso de simulación siempre a la vista.
- **Previsualización del ajuste con filo doble**: una línea oscura y una de color, para que
  se vea sobre un fondo claro, uno oscuro o un vídeo de Wallpaper Engine. Sigue sin recibir
  clics ni foco.
- Azul para foco y acciones principales, coral para avisos y acciones destructivas, en los
  siete temas y con contraste medido.
- La nube de la escena de inicio como identidad, con topes de tamaño para que no se coma la
  pantalla.

**NubeHypr — fiabilidad (D7)**

- **Modo seguro**: deja de organizar ventanas y **conserva la isla, los paneles, el
  carrusel y la bandeja**. Es estado de ejecución, no configuración: reiniciando se sale.
- **Supervisor interno** que vigila la edad de la instantánea, los atajos vivos y las rachas
  de fallos. Su acción más fuerte es apagar la capa: no existe ninguna que reorganice.
- **Restauración de posiciones** cuando una aplicación se reinicia, descartando las que
  caerían en un monitor que ya no está.
- **Rescate de la configuración**: si `config.json` no se puede leer, se usa la copia más
  reciente que sirva (el `.tmp` a medias, el `.bak` o un respaldo de migración) en vez de
  arrancar con la configuración de fábrica. El fichero roto no se borra ni se sobrescribe.
- Inventario de cierre que se **mide**: atajos, suscripciones, previsualización y memorias.

**NubeHypr — preguntar antes de mover**

- **`ConfirmarMovimientos` por fin hace algo.** La clave estaba en la configuración desde la
  primera versión de NubeHypr, puesta a `true`, escrita en el `config.json` de cada usuario…
  y no la leía nadie. Era una promesa de seguridad sin cumplir: el fichero decía que NubeHypr
  pregunta antes de tocar ventanas, y no preguntaba.
- La pregunta sale en la isla, con «Mover» y «Cancelar», y **no contestar es no mover**: no
  hay plazo que venza aplicando ni confirmación por omisión.
- **Solo para lo automático**: la regla que salta al abrir una ventana y el reparto por
  layout. Un atajo que el usuario acaba de pulsar no pregunta, porque pulsarlo ya es la
  respuesta.
- La propuesta **caduca a los 30 segundos** —los destinos se calcularon con las ventanas de
  entonces— y al contestar se vuelve a comprobar que la pregunta siga en pie, que no haya
  caducado y que la capa siga en «aplicar».
- Interruptor en Ajustes → NubeHypr. Apagarlo es legítimo: es decir «muévelas sin preguntar».

**D9.6 probado contra Windows — y arreglado, porque no funcionaba**

- **`--arneslayout`**: crea un escritorio virtual, lanza tres ventanas propias dentro, aplica
  una regla con layout y mide dónde acaban. La prueba que «no se podía hacer sin recolocar
  las ventanas del usuario» sí se podía: en un escritorio recién creado no hay ninguna.
- **Y prueba las dos mitades sobre las mismas ventanas**: en simulación las tres se quedan
  donde Windows las puso, con los contadores de reparto y de movimientos reales sin tocar
  —«en simulación no se mueve nada» estaba comprobado solo en lógica—; después, en «aplicar»,
  una ventana nueva dispara el reparto y las cuatro acaban enlosadas, incluidas las tres que
  ninguna regla nombró.
- **Y encontró que D9.6 no llegaba a ejecutarse.** Una regla que pedía **solo** un layout
  —la forma natural de escribirla— volvía antes de repartir, porque la condición de salida
  miraba si la regla decía algo de *esa* ventana. Son dos decisiones distintas: una sobre la
  ventana y otra sobre el escritorio. Ahora se comprueban por separado.
- Contador propio para el reparto (`RepartidasPorRegla`): el de colocar cuenta ventanas una a
  una, y un layout no coloca una a una.
- **Y una mentira en el registro:** la línea de cada ventana decía `modo=simulacion` fijo
  desde antes de que NubeHypr moviera nada, así que llevaba desde D9.5 diciéndolo mientras las
  ventanas se movían. Ahora dice el modo de verdad.

**Paneles: superficie propia para cuatro que no la tenían**

- Bandeja, batería, notificaciones y el menú contextual usan ya la misma concha que audio, red
  y el centro de control. No es estética: da superficie **opaca**, y antes se apoyaban en el
  fondo translúcido de la isla, con el escritorio transparentándose detrás del texto.
- **Y destapó que tres paneles se dibujaban de punta a punta de la isla** con su contenido
  centrado dentro, porque no pasaban el ancla a la que debían colgarse. Ya cuelgan de su
  icono. No se veía porque sin filo, un contenedor de ancho completo y uno que abraza su
  contenido se dibujan igual.
- El menú contextual se cierra al salir el ratón, y ese gesto se movió a la superficie nueva:
  si no, moverse hacia el filo del propio menú lo habría cerrado en la cara.
- Una notificación con dos acciones del mismo texto —Defender manda dos «Ver menos
  notificaciones»— ya no pinta dos botones idénticos.

**Pruebas que antes eran manuales y ahora se miden**

- `--diagrejilla`: «Ver todas las ventanas» con ventanas de verdad, midiendo las tarjetas
  **dibujadas** y no las pedidas.
- El panel de red abierto y cerrado diez veces, comprobando que las cinco acciones están las
  diez. El fallo que eso busca sale una vez de cada tantas.
- Ajustes encogida a su tamaño mínimo: las trece páginas caben a 720x460.
- **Y el fallo original, probado por el botón de verdad**: `--diagmodulos` abre Ajustes,
  pulsa «Añadir Escritorios a Centro», comprueba que aparece en la barra, pulsa **Guardar**,
  **cierra la ventana** y comprueba que sigue ahí. Ese último paso es el que importa: Ajustes
  guarda en vivo para que veas el resultado, pero cerrar sin Guardar revierte.
  No es inyección de entrada: se levanta el `Click` de WPF, que llama al manejador del botón.
- Elegir una ventana en «Ver todas las ventanas» y comprobar que viene al frente, sobre una
  ventana propia.

**Ajustes que existían y no se podían tocar**

- **«Mover ventanas de verdad»**, el interruptor que faltaba. `NubeHypr.Modo` decide si la
  capa mueve ventanas o solo apunta lo que haría, tiene 175 usos en el producto y **no tenía
  ningún control en Ajustes**: el modo «aplicar» solo se alcanzaba editando el `config.json`
  a mano. Con él, todo lo que mueve ventanas era inalcanzable desde la interfaz.
- Ese interruptor y el de la confirmación salen **atenuados** mientras NubeHypr está apagado,
  con el motivo en la ayuda emergente y en el nombre para el lector de pantalla: apagada la
  capa no significan nada, y un control que parece disponible y no responde engaña más que
  uno que se ofrece de más.
- **Hueco entre ventanas** (`NubeHypr.Gap`): lo usan el reparto, la vista general y las
  reglas, y la única forma de cambiarlo era editar el `config.json` a mano. Ahora es un
  deslizador en Ajustes → NubeHypr.
- Los dos salieron del mismo barrido mecánico —qué claves de la configuración no escribe
  nadie desde la interfaz—, que es el que habría encontrado el dock sin interruptor mucho
  antes de que lo encontrara un usuario.
- **Y tres que no leía nadie dejan de escribirse** (`EspacioInicial`,
  `MostrarPrevisualizacion`, `Espacios`): escribir en el fichero de alguien una clave que el
  programa ignora no es inofensivo, porque quien lo abre entiende que significa algo. Lo que
  ya esté escrito no se toca; solo se deja de añadirlas donde no estén.

### Cambiado

- `Contraste` sabe de tonos (`Tono`), y el extractor del fondo guarda también el color medio
  sin oscurecer, que es lo que de verdad hay detrás de una ventana.
- La previsualización del ajuste a bordes y la de NubeHypr comparten cuerpo: eran dos copias
  del mismo rectángulo.
- El módulo de escritorios repinta comparando una huella, así que renombrar un escritorio o
  mover una ventana se nota — antes solo se notaba al cambiar el número de escritorios.
- `Config.MiraYNoToca` decide qué banderas no pueden escribir la configuración, y es una
  función pura: antes era un `is ... or ...` dentro del arranque que no se podía comprobar.

### Corregido

- Un valor no esperado en `NubeHypr.Reglas` (por ejemplo cadenas de un borrador antiguo)
  tumbaba la carga entera de `config.json` y arrancaba con la configuración de fábrica.
  Ahora cada entrada ilegible queda inerte y el resto de la configuración se conserva.
- El registro decía `adapter=stopped` incluso con el motor montado, y daba dos versiones de
  configuración distintas para el mismo fichero en líneas seguidas.
- **El prefijo de los atajos de NubeHypr pasa de `Win+Alt` a `Win+Ctrl+Alt`.** Con el
  anterior solo entraban 10 de los 19: el Game Bar de Xbox y Teams se quedan con varios
  `Win+Alt`. Con el nuevo entran **los 19**, medido con `--diagprefijos`. El prefijo es
  ahora una constante, así que cambiarlo es una edición y no reescribir veinte cadenas.
- **`--salir` migraba la configuración** al esquema del binario que lo lanzaba. Su proceso
  solo manda la señal de salida, pero al irse cargaba la configuración, y cargarla ejecuta
  las migraciones.
- **Una opción desconocida arrancaba la barra entera.** Un `--loquesea` se caía por todas
  las ramas del arranque y terminaba montando NubeBar, con lo que eso implica para la
  configuración. Ahora se dice en el registro y se sale con código 2.

### Diagnósticos nuevos

`--diagreglas`, `--diagd6`, `--diagd7` y `--diagprefijos`, todos de solo lectura. Ninguno
mueve ventanas ni escribe la configuración.

---


## v1.2.0

Primera versión de la serie 1.2. La 1.1.x cerró con una barra que funcionaba; esta
es la que convierte la barra en un entorno: widgets que se colocan donde quieras,
superficies rediseñadas, el escritorio (fondos, perfiles, escritorios virtuales) y
una bandeja que ya no depende de lo que hubiera al arrancar.

### Nuevo

**La isla y sus widgets**

- Modo edición: los widgets se arrastran, se esconden, se quitan y se añaden desde
  una biblioteca, con tres zonas (izquierda, centro, derecha).
- Barra **vertical** de verdad en los bordes izquierdo y derecho, con su propia
  ventana para los paneles, que salen al lado y no dentro de la franja.
- `PanelNube` es la autoridad única de borde, posición y visibilidad.
- Roles de color, cuatro niveles de animación (completo, equilibrado, reducido,
  desactivado) y siete temas.

**Las superficies**

- **Inicio** es una sola superficie: saludo y hora, buscador con su lista, lo que
  suena, acciones rápidas, CPU y RAM, fondo, conectividad, teclado, apps fijadas,
  energía y acciones del sistema. Se abre desde el botón Inicio y cuelga de él.
- **Centro de control**: sus tarjetas viven en esa misma superficie. No hay un
  segundo panel con lo mismo ni una segunda entrada.
- **Audio** aparte, abierto solo desde el altavoz: volumen, salida, volumen por
  aplicación y brillo.
- **Red y Ethernet** rediseñados, con redes, conexión y medición de velocidad.
- **Vista de ventanas** con miniaturas estables y su atajo global.
- **Ajustes** rediseñado: navegación lateral, cabecera, tarjetas por intención,
  una sola barra de acciones y búsqueda que ignora acentos.
- Lenguaje visual común (`PanelShell`, `Card`, `Slider`, `Toggle`, `MetricCard`,
  `QuickAction`, `MediaCard`, `StatusBadge`, `EmptyState`…) con superficies opacas
  derivadas del tema.

**El escritorio**

- Gestor de fondos: color sólido, degradado, imagen, presentación, con ajuste por
  monitor y respaldo si falta la imagen.
- Perfiles de escritorio versionados, con aplicación transaccional y deshacer.
- Esquema de configuración versionado, con migraciones en orden, copia previa y
  recuperación del `.bak` si el fichero está roto.
- Escritorios virtuales.
- Ajuste de ventanas a bordes y esquinas arrastrándolas.
- Integración opcional con **Wallpaper Engine**: control de su arranque y
  coordinación con la escena de la nube.
- Escena de arranque: la nube se despierta y se abre, esperando al fondo real.

**El buscador y las notificaciones**

- Resultados por peldaños (aplicaciones, archivos, ajustes, ventanas, cálculos,
  comandos, web), con menú de clic derecho por resultado: abrir, ejecutar como
  administrador, abrir ubicación, propiedades y fijar.
- Centro de notificaciones con historial, no leídas y «no molestar».

### Correcciones

- **La bandeja se actualiza en caliente.** Las aplicaciones que arrancan después de
  NubeBar aparecen sin reiniciarla, y al cerrarlas desaparecen. Medido con apps
  reales: `añadidos=1` al abrir una, `eliminados=1` al cerrarla, tres a la vez sin
  duplicados y sin que el orden se mueva.
- **Iconos fantasma al reiniciar Explorer.** Al volver Explorer, las apps registran
  su icono con una ventana nueva y el registro viejo se quedaba: la bandeja llegaba
  a enseñar la misma aplicación dos veces, y una no respondía a los clics. Ahora se
  filtran los iconos cuya ventana ya no existe, con una sola definición que usan la
  tira, la cuenta del «^» y la rejilla de ocultos. Lo mismo cubre a una aplicación
  que se muere de golpe sin retirar su icono.
- **Los deslizadores no se quedan agarrados.** El fin del gesto colgaba de
  `MouseLeftButtonUp`, que en una ventana `WS_EX_NOACTIVATE` por capas se pierde.
  Ahora lo decide el botón del ratón una vez por fotograma, con una máquina de
  estados y un solo camino de salida idempotente, y seis formas de terminar.
- **Los controles ya no arrastran la isla.** El arrastre se engancha en un evento
  de túnel, que viaja de la raíz a la hoja, así que `e.Handled` en el control
  llegaba tarde por definición. Ahora el gesto es de quien nace el clic.
- **Abrir Inicio no detiene la música.** Montar un panel es una operación de
  lectura: mientras se construye, las puertas que escriben audio y multimedia están
  cerradas y cualquier intento queda apuntado con su nombre. Medido con música
  sonando: 30 segundos con la superficie abierta y el progreso avanzando.
- **Los paneles ya no se despliegan de punta a punta.** Cuelgan de su botón y su
  contenido mide lo que mide su tarjeta; antes se estiraba al ancho de la isla y el
  recorte solo lo tapaba, de ahí las tarjetas cortadas.
- **Y ya no se les pone un segundo scroll.** Un panel que se desplaza por dentro no
  se envuelve en otro `ScrollViewer`: eso dejaba su barra a la vista, quitaba a
  `PanelShell` de la raíz y dejaba la fila elástica sin acotar.
- **Altura controlada:** el alto pedido era un mínimo y el contenido podía subirlo
  hasta comerse la pantalla. Ahora hay tope y lo que sobra se desplaza por dentro.
- **Esc cierra los paneles** que no piden teclado. No faltaba un manejador: la isla
  no tiene el foco, así que su `KeyDown` no se disparaba nunca. El aviso llega del
  hook, y la tecla no se traga.
- La vista general ya no parpadea: se encontró un bucle en el que recolocar la isla
  disparaba un evento sobre nuestra propia ventana que pedía refrescar.
- La barra vertical ya no recorta el texto y flota sobre las ventanas en los cuatro
  bordes por defecto.

### Diagnóstico

Tres modos que escriben en `%APPDATA%\NubeBar\nubebar.log` y se cierran solos:

- `NubeBar.exe --diagbandeja [segundos]` — vigila la bandeja y apunta
  `enumerados / añadidos / eliminados / duplicados / fantasmas` y el refresco al
  abrir el desplegable.
- `NubeBar.exe --diaginicio [segundos]` — abre la superficie de Inicio, mide lo que
  se ha pintado de verdad (raíz visual, `PanelShell`, tarjetas visibles y con área,
  viewport, extent, offset) y vigila la música mientras está abierta.
- `NubeBar.exe --diagvista [segundos]` — cuenta cuántas veces se rehace la vista de
  ventanas en reposo.
- `NubeBar.exe --ventanas` — qué ventanas se ven y por qué se descartan las demás.
- `NubeBar.exe --pruebas` — la tanda de pruebas, dentro del propio ejecutable.

### Limitaciones conocidas

- **Iconos de bandeja que no llegan.** Un icono solo aparece si su aplicación se
  vuelve a registrar al recibir `TaskbarCreated`. Alguna no lo hace y sigue hablando
  con la bandeja de Explorer: su icono no llega, y no hay forma de traerlo.
- **Iconos que no aceptan clics.** Los de procesos como administrador o SYSTEM se
  ven, pero Windows no deja mandarles mensajes. Queda apuntado en el log.
- Un icono cuya aplicación conserva la ventana pero deja de usarlo sin retirarlo
  seguiría apareciendo. No hay nada que lo detecte.
- El desplegable de iconos ocultos enumera **al abrirse**, no mientras está abierto.
- «No molestar» no se puede cambiar desde fuera en Windows: NubeBar abre su ajuste.
- Verificación visual, varios monitores y DPI mixto se comprueban a mano.

### Instalación

Desde la [Release](../../releases/latest):

- **`NubeBar-Setup.exe`** — recomendado. Se instala para tu usuario, sin pedir
  administrador, y deja NubeBar lista para arrancar con Windows. Se quita desde
  «Aplicaciones instaladas».
- **`NubeBar.exe`** — el ejecutable suelto. Muévelo a una carpeta fija antes de
  abrirlo: desde la carpeta de descargas, Windows puede borrarla sola.

Windows 11 x64. No hace falta instalar .NET.

### Actualizar desde una versión anterior

Ejecuta el instalador nuevo encima: cierra NubeBar con su propia salida ordenada
—que devuelve la barra de tareas de Windows a su sitio— reemplaza el ejecutable y
mantiene el acceso de inicio.

> **Tu configuración no se toca.** Vive en `%APPDATA%\NubeBar\config.json` y el
> instalador no la sobrescribe, ni al actualizar ni al desinstalar. Tampoco se
> tocan las copias de seguridad que NubeBar guarda a su lado.
