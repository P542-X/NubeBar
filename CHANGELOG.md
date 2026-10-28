# Notas de versión

Las versiones publicadas están en [Releases](../../releases). Cada una trae el
instalador (`NubeBar-Setup.exe`) y el ejecutable suelto (`NubeBar.exe`), los dos
construidos por el workflow de Release a partir de la etiqueta.

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
carpetas de fase, con los hashes en `NUBEHYPR.md`. Lo que falta es la tanda de pruebas que
necesitan ojos, ratón y teclado, enumerada en `NUBEHYPR.md`.

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
