# Notas de versión

Las versiones publicadas están en [Releases](../../releases). Cada una trae el
instalador (`NubeBar-Setup.exe`) y el ejecutable suelto (`NubeBar.exe`), los dos
construidos por el workflow de Release a partir de la etiqueta.

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
