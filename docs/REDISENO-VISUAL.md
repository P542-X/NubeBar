# Rediseño visual de NubeBar

Plan para llevar a la app el aspecto de la maqueta aprobada el 28/10/2026. Está escrito
para que una sesión nueva pueda empezar por aquí sin más contexto: qué se quiere, qué se
puede y qué no, dónde tocar, en qué orden y cómo comprobarlo.

**La maqueta:** <https://claude.ai/artifact/EPD2FVAWaDM8eeFbzKRcEF> (privada: solo la abre el
dueño de la cuenta). Siete pantallas: «Isla e Inicio», «Laterales y dock» y, debajo, los
paneles que cuelgan de la isla: red, audio, batería, notificaciones y bandeja.

---

## La regla que manda

**Nada que la app no pueda hacer de verdad.** La maqueta ya se rehízo con esa regla, así que
lo que se ve en ella es el objetivo literal. Lo que se descartó, y por qué:

| Descartado | Por qué | Lo que se hace en su lugar |
|---|---|---|
| Cristal esmerilado (desenfocar lo de detrás) | El Acrylic/Mica de Windows no va fiable en ventanas con `AllowsTransparency` y forma libre, que es lo que es la isla | Superficies oscuras con alfa (`rgba(12,14,20,.94)`), filo fino y sombra |
| Degradados cónicos | WPF no tiene `ConicGradientBrush` | `LinearGradientBrush` con un `RotateTransform` animado en `RelativeTransform`: una franja de luz que da la vuelta al borde |
| Fondo de escritorio animado | No es de la barra, y gastaría batería siempre | Un degradado quieto, que `GestorFondos` ya sabe poner |
| Aviso flotante suelto | En NubeBar las notificaciones asoman **dentro** de la isla | Mantener ese comportamiento con el estilo nuevo |
| Ecualizador por frecuencias | `Audio` mide el **pico** (`IAudioMeterInformation.GetPeakValue`): un número, no un espectro | Barras que siguen ese nivel, cada una con su fase |

---

## Los colores

Una sola familia fría y sobria. El coral es **solo** una señal (aviso sin leer, «Apagar» al
pasar el ratón), nunca decoración.

| Papel | Valor | Uso |
|---|---|---|
| Superficie | `#0C0E14` al 94 % | Isla, paneles, dock |
| Tarjeta | blanco al 3,5 % sobre la superficie | Cada bloque dentro de un panel |
| Filo | blanco al 9 % | Bordes de 1 px |
| Texto | `#E6E9F2` | |
| Texto tenue | `#9AA3B8` | Subtítulos, estados (contraste ≥ 4,5:1 sobre la superficie) |
| Plata | `#C9D2E6` | Iconos de sistema, glifos |
| Acero | `#5E6E8F` | Degradados de relleno (carátula vacía, fondo) |
| Acento | `#8DA9E8` (configurable) | Activo, foco, progreso, punto de la app activa |
| Coral | `#D9826E` | Solo señales |
| Fondo de escritorio | degradado `#1B2130 → #10141D → #0A0C12` a 160° | Opcional, vía `GestorFondos` |

Acentos alternativos sobrios que la maqueta ofrece: `#9FB4C7`, `#7FA8B8`, `#A9A3D0`.

Tipografía: **Segoe UI Variable** (la de Windows 11). Títulos de sección en mayúsculas
pequeñas, 11,5 px, espaciado 0,06 em, color tenue.

Encaje en el código: los colores salen de `Roles.Desde(...)` (`Roles.cs`) a partir de
`Tema.Fondo/Texto/Acento`. Lo limpio es un **tema nuevo** en `Temas.cs` con estos valores, y
que `Roles` derive superficie, tarjeta, filo y tenue como ya hace. Comprobar contraste con
`Contraste.Entre` (las pruebas de E8 ya miden texto sobre superficies).

---

## Lo que hay que construir, pieza a pieza

Medidas de la maqueta en unidades de WPF, antes de la escala de paneles
(`EscalaDePaneles`, ver `MainWindow.Paneles.cs`).

### 1. La isla

- **Superficie** sólida con alfa, radio de la isla, **filo de 1 px** blanco al 9 %.
- **Destello que recorre el filo:** detrás del filo, un `Border` con un
  `LinearGradientBrush` (transparente → plata 60 % → acento → transparente, en una franja
  estrecha) y un `RotateTransform` en su `RelativeTransform` animado 0→360° en **9 s**,
  lineal, infinito. Se ve como una luz que da la vuelta al contorno. Recortado por la forma
  de la isla.
- **Halo:** `DropShadowEffect` del color de acento, `ShadowDepth 0`, radio ~26, con la
  opacidad respirando entre 0,08 y 0,2 en 6 s. Ya existe algo parecido en el estilo Neón
  (`Neon.cs`, `PonerResplandor`): reutilizarlo, no duplicarlo.
- **Iconos de apps:** los reales (ya los da `Iconos`/`Nativo.Icono`), 22 px, sin ficha
  detrás. Al pasar el ratón: sube 2 px, escala 1,08 con muelle y un fondo blanco al 6 % de
  radio 8 (`MainWindow.Animaciones.cs`, `Levantar`; ojo con el recorte del carrusel, ver
  «Lecciones»). Iconos oscuros con halo claro: ya hecho (`Legible`, `Iconos.EsOscuro`).
- **Punto bajo la app:** 4×3 px tenue; la activa, 12×3 px del acento.
- **Reloj:** degradado lineal de texto (texto → blanco → texto) cuyo desplazamiento se
  anima en 7 s: un brillo que lo recorre. En WPF, `Foreground` = `LinearGradientBrush` con
  `TranslateTransform` animado en `RelativeTransform`.
- **«ES»** en una pastilla blanca al 6 %, radio 6, color plata.
- **Insignia de avisos:** pastilla coral con el número y un latido (anillo coral que crece y
  se desvanece en 2,4 s).

### 2. El menú Inicio

- **Cuelga del botón Inicio y se funde con la isla:** esquinas de arriba rectas, de abajo
  redondeadas (20). Eso ya lo hace el recorte de la tarjeta (`AplicarRecorteDelCajon`).
- **Entrada:** escala vertical 0,6 → 1 desde arriba más un fundido (0,55 s, curva con un
  pelín de rebote), y **cada tarjeta escalonada** 50 ms más tarde que la anterior (sube 10 px
  y aparece). `MontarPortada` ya escalona: afinar tiempos y curva.
- **Orden:** saludo (nombre, fecha, reloj grande en degradado blanco → plata → acero),
  buscador, música, teclado, energía.
- **Buscador:** filo de 1 px. Al pasar el ratón o con foco, **una franja del acento recorre
  el filo** (misma técnica que la isla, 4 s). Lupa a la izquierda, pista de texto tenue.
- **Música:**
  - Carátula real 72 px, radio 12 (`Musica.Caratula`); sin carátula, un degradado acero.
  - Título, artista · app.
  - Barra de progreso de 4 px del acento con el `Avance` del estado que devuelve
    `Musica.Leer()`, y un brillo blanco que la recorre (2,8 s).
  - Controles: anterior y siguiente en plata; play/pausa en un círculo blanco de 42 px.
  - **Ecualizador:** 7 barras de 3 px del acento que siguen el **pico** de audio (ver la
    regla), cada una con su fase.
- **Teclado:** rejilla de 3; la activa con el borde del acento y fondo acento al 10 %.
- **Energía:** fila de 4 botones de 42 px a la derecha. Al pasar el ratón suben 2 px.
  «Apagar» con glifo coral y, al pasar el ratón, fondo coral al 12 % y borde coral al 45 %.
  Se mantiene la confirmación de reiniciar y apagar.

### 3. Los paneles laterales

- Pegados a los bordes, centrados en vertical y fuera del hueco de la barra y del dock (eso
  ya está: `ColocarLateral`, `EstorbosParaLosLaterales`).
- **Entrada:** deslizan 24 px desde su borde y aparecen, y sus secciones escalonadas.
- **Superficie** como la isla, con un filo que se aclara arriba (degradado plata 22 % →
  filo).
- **Secciones:** tarjetas con cabecera (icono del acento, título, flecha `›` a la derecha).
- **Acciones rápidas** encendidas: fondo acento al 12 % y borde del acento.
- **Deslizadores:** pista de 4 px, la parte llena del acento, pomo blanco de 14 px con un
  anillo del acento al 25 % que crece al pasar el ratón.
- **Sistema:** tres anillos (CPU, RAM, disco) del acento, que pasan a ámbar `#D8C08F` al
  pasar del 75 %.
- **Calendario:** hoy con borde del acento y fondo acento al 16 %.
- **Desplazamiento:** la barra propia (`BarraDeScroll.cs`), que ya existe.

### 4. El dock

- Iconos reales de 38 px sobre la superficie, radio 22, filo y sombra.
- **Lupa:** el icono bajo el ratón escala 1,32 y sube 6 px; **sus dos vecinos** escalan 1,12
  y suben 2 px. Con muelle. En WPF: en `MouseEnter`/`MouseLeave` de cada icono, animar el
  suyo y los de al lado.
- Puntos de apps abiertas igual que en la isla.
- **Mide en píxeles reales**: usar `SitioDelDock` (ver «Lecciones»).

### 5. Los paneles que cuelgan de la isla

Pantallas «Panel de red», «Panel de audio», «Panel de batería», «Notificaciones» y
«Bandeja» de la maqueta. Comparten la forma del Inicio: **salen de su icono fundidos con la
isla** (esquinas de arriba rectas), entran con la escala vertical y sus bloques escalonados.
El icono del que cuelgan se marca con fondo acento al 18 % mientras están abiertos.

- **Red** (`MainWindow.Red.cs`): estado con pastilla «Conectado» (punto verde `#8FC1A0`),
  la red actual con su señal, velocidad y adaptador y un botón «Desconectar»; lista de
  redes con su barra propia, filas que se iluminan y la elegida con borde del acento; el
  botón de volver a buscar **gira** mientras busca; velocidad con tres cifras y, midiendo,
  una franja del acento que recorre una pista fina; rejilla de acciones (la destructiva con
  coral al pasar el ratón, las que no aplican atenuadas).
- **Audio** (`AbrirAudio` en `MainWindow.Control.cs`): volumen general con `−`, deslizador,
  `+` y cifra; silencio que pinta el botón en coral y apaga el deslizador; salida activa con
  borde del acento y pastilla «Activa»; **por aplicación** con el icono real de cada app
  (`Nativo.Icono` de la ventana del proceso de la sesión), su deslizador y su silencio.
- **Batería** (`MainWindow.Bateria.cs`): icono grande con el rayo si carga, porcentaje en
  grande, barra que se **llena al abrir** (de 0 a su valor, 1 s); modo de energía como
  **control segmentado** de tres (el activo con fondo y borde del acento); ahorro y el enlace
  a Configuración.
- **Notificaciones** (`MainWindow.CentroNotificaciones.cs`): cabecera con «No molestar»
  (encendido en el acento) y «Borrar todo»; agrupadas por app con el nombre en el acento;
  cada aviso con icono, título, hora y texto; la `✕` aparece al pasar el ratón; vacío, un
  «Todo al día» con una marca.
- **Bandeja** (`MainWindow.Bandeja.cs`): rejilla de 5 columnas de 44 px; los iconos **entran
  con un pequeño rebote escalonado**; al pasar el ratón crecen y sale su nombre en una
  etiqueta propia (en WPF, un `ToolTip` con estilo, no el de serie).

### 6. Lo que ya existe y se reutiliza

- La barra de desplazamiento propia (`BarraDeScroll.cs`).
- El halo claro para iconos oscuros (`Legible`).
- La escala de paneles según la pantalla (`EscalaDePaneles`, `Barra.EscalaPaneles`).
- Los niveles de animación (`Animacion.Elegir`, `Animacion.Adorno`) y los estilos visuales
  (`EstiloVisual`: Normal, Nube, Neón).

---

## Orden de trabajo

Cada fase se publica como versión para probarla antes de seguir.

1. **Tema nuevo**: colores, filos, tarjetas, tipografía, títulos de sección. Lo que más
   cambia la imagen y lo menos arriesgado.
2. **La isla**: destello del filo, halo, reloj con brillo, insignia con latido, puntos de
   las apps.
3. **El menú Inicio**: entrada y escalonado, buscador con franja, música (carátula,
   progreso, ecualizador por pico), teclado y energía.
4. **Paneles de la isla**: red, audio, batería, notificaciones y bandeja.
5. **Laterales y dock**: entradas, secciones, deslizadores, anillos y lupa del dock.

Con todo lo decorativo:

- **Respeta los niveles de animación.** En «reducido» o «desactivado» nada se mueve que no
  haga falta (`Animacion.Adorno`); los fundidos se quedan.
- **Lo pesado solo cuando se ve.** El destello del filo y el halo se paran con la isla
  escondida (pantalla completa) y no corren si la ventana no es visible.
- **Medir el consumo** con la barra en reposo antes y después de cada fase (CPU del proceso
  y tiempo de fotograma; `MedirFotogramas` ya apunta FPS en el log). Si una fase sube el
  reposo de forma apreciable, se recorta antes de publicar.

---

## Cómo se comprueba

- **Pruebas de lógica:** `NubeBar.exe --pruebas informe.txt` (sin ventanas). A día de hoy
  falla **una** que ya fallaba antes: «fondo: no recargar si no ha cambiado nada». Cualquier
  otro fallo es nuevo.
- **Fotos de cada superficie:** `NubeBar.exe --capturas carpeta` abre cada panel y lo
  fotografía (`02-inicio.png`, `31-dock.png`, `32-panel-izquierdo.png`,
  `33-panel-derecho.png`…). Comparar con la maqueta.
- **Que nada se corte:** `NubeBar.exe --diagpaneles` (paneles en las cuatro posiciones de la
  barra) y `--diaginicio`. Al abrir un panel el log apunta además «de lado: contenido …
  tarjeta … dentro/SE SALE» (`ApuntarSiSeSaleDeLado`) y, en los laterales, «según WPF: …»
  con su tamaño, escala y scroll.
- **Los diagnósticos se lanzan con la barra cerrada** (`NubeBar.exe --salir`) y luego hay
  que volver a abrirla. Un binario en una carpeta `dist-*` se niega a arrancar como barra
  normal (es a propósito, para no migrar la configuración); para usarlo de barra, copiarlo a
  `%LOCALAPPDATA%\Programs\NubeBar`.

---

## Lecciones de esta sesión que afectan al rediseño

- **Píxeles contra unidades de WPF.** `Monitores.Info`, el área de trabajo y
  `VentanaSuperpuesta.Colocar` van en **píxeles**; las medidas de diseño (anchos de panel,
  iconos, márgenes) van en **unidades de WPF**. Al 125 % mezclarlas recorta o agranda.
  Convertir siempre con la escala del monitor (`ColocarLateral`, `SitioDelDock`).
- **`VentanaSuperpuesta` crea su hwnd antes de colocarse** (`EnsureHandle`); sin eso, la
  primera colocación iba en las unidades equivocadas.
- **Los paneles se escalan con `LayoutTransform`.** `Width` va por dentro de la escala y el
  margen por fuera (ver `AplicarRecorteDelCajon`). El margen puede ser negativo cuando el
  ancla está en el filo de la isla.
- **Medir anchos después del layout.** `AjustarAncho` programa la medida con prioridad
  `Loaded`; medir en caliente devuelve tamaños viejos.
- **El carrusel recorta.** Cualquier cosa que crezca o suba al pasar el ratón necesita el
  hueco que el carrusel ya deja (`ModuloApps`, «holgura»).
- **No preguntar a ventanas desde la interfaz.** Los títulos se leen con
  `InternalGetWindowText`: `GetWindowTextLength` sobre una ventana del propio proceso se
  interbloqueaba con el hilo de `SystemEvents` al volver de la suspensión.
- **Vigía de cuelgues** (`Vigia.cs`): si la interfaz no responde 5 s guarda un volcado en
  `%APPDATA%\NubeBar\cuelgues\`. Se lee con `dotnet-dump analyze <fichero>` → `clrthreads`,
  `setthread 0`, `clrstack`.

---

## Publicar

1. Commit en `fase-b-widgets` y push. En PowerShell 5.1, el mensaje con `git commit -F
   fichero.txt`, no con una cadena en línea: una vez se subió la etiqueta sin el commit.
2. Comprobar que el commit está en GitHub (`git ls-remote origin refs/heads/fase-b-widgets`)
   **antes** de etiquetar.
3. `git tag -a vX.Y.Z -m "…"` y `git push origin vX.Y.Z`. El workflow (`release.yml`)
   compila el exe autónomo y el instalador y publica la Release.
4. Entrada en `CHANGELOG.md` con cada versión.

Numeración: empezó de cero en la v1.0.0 el 28/10/2026; la última publicada antes de este
plan es la **v1.0.2**.
