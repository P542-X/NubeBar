# Rediseño visual de NubeBar

Plan para llevar a la app el aspecto de la maqueta aprobada el 28/10/2026. Está escrito
para que una sesión nueva pueda empezar por aquí sin más contexto: qué se quiere, qué se
puede y qué no, dónde tocar, en qué orden y cómo comprobarlo.

> **Estado: cerrado y aprobado (28/10/2026).** El diseño está decidido; lo que queda es
> construirlo. **Para empezar una sesión nueva:** «Lee `docs/REDISENO-VISUAL.md` y empieza
> por la fase 0». Cada fase se publica como versión antes de pasar a la siguiente.
>
> Decisiones tomadas, por orden de importancia:
>
> 1. **Todos los colores de todo, editables** por el usuario (decisión B).
> 2. **Fuera Normal, Nube y Neón**; en su lugar, **9 temas de serie** completos (decisión A):
>    Profesional, Oficina, Videojuegos, Invierno, Cyberpunk, Sakura, Samurái, Otoño y Sad.
> 3. El tema pinta **toda la app**, y nunca puede dejar Ajustes ilegible («Alcance»).
> 4. El usuario **crea sus propios temas** en el Estudio de temas, con muchísimas
>    herramientas (decisión C).
> 5. **Nada que la app no pueda hacer de verdad** («La regla que manda»).

**La maqueta:** <https://claude.ai/artifact/EPD2FVAWaDM8eeFbzKRcEF> (privada: solo la abre el
dueño de la cuenta). Once pantallas, por filas:

1. «Isla e Inicio» y «Laterales y dock», en Profesional.
2. «Paneles que cuelgan de la isla»: red, audio, batería, notificaciones y bandeja, en
   Profesional.
3. «Temas anime»: Sakura, Samurái, Otoño y Sad, con su selector.
4. «Más temas»: Oficina, Videojuegos, Invierno y Cyberpunk, con su selector; y el «Estudio
   de temas», funcional.
5. «Paneles en cada tema»: el lateral izquierdo y todos los paneles a la vez en cualquiera de
   los 9 temas, para ver que cada superficie cambia entera con el tema.

**Copia en el repositorio:** `docs/maqueta-rediseno/` guarda el código de las once pantallas
y **`temas.json` con los valores exactos de los 9 temas** (colores, radios, esquinas, letras
y decoraciones). Si la maqueta no se puede abrir, todo lo necesario está ahí.

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

## Dos decisiones que mandan sobre todo lo demás

### A. Temas completos que elige el usuario (fuera Normal, Nube y Neón)

Los estilos actuales —**Normal, Nube y Neón** (`EstiloVisual.cs`, `Neon.cs`, `Tema.Estilo`)—
**se quitan**. En su lugar hay **temas completos**: cada uno trae colores, forma (radios,
grosores), letra, decoraciones y animaciones propias, y el usuario elige el que más le
gusta en Ajustes, con una vista previa de cada uno.

| Tema | Idea | Estado |
|---|---|---|
| **Profesional** | Gris carbón, azul acero, plata. Sobrio | Maqueta: «Isla e Inicio», «Laterales y dock» y los paneles |
| **Anime · Sakura** | Pastel claro (sakura, lavanda, cielo, menta), letra redondeada, Nubecita sonriente, pétalos rosas | Maqueta: «Temas anime» |
| **Anime · Samurái** | Noche de tinta: laca negra, bermellón `#D7263D` y oro `#C9A55C`; títulos con letra de pincel (Shippori Mincho), radios pequeños; Nubecita con *hachimaki*; pétalos rojo oscuro; fondo con sol rojo, montañas y el kanji 侍 | Maqueta: «Temas anime» |
| **Anime · Otoño** | Crema cálido, naranja arce `#E8743B`, ámbar y granate; hojas de arce cayendo; Nubecita con bufanda; fondo de atardecer con colinas | Maqueta: «Temas anime» |
| **Anime · Sad** | Noche lluviosa azul grisácea, acento `#8FA8D8`; lluvia cayendo en el Inicio; Nubecita gris con lágrima y boca triste; saludo con puntos suspensivos; fondo de ciudad con ventanas encendidas | Maqueta: «Temas anime» |
| **Oficina** | Claro y neutro tipo Fluent: blanco, grises, azul `#2F6FDB`, letra de Windows, radios pequeños. Sin mascota, sin partículas, sin halo | Maqueta: «Más temas» |
| **Videojuegos** | Casi negro con verde neón `#39FF88` y magenta `#FF3DCB`, esquinas en chaflán, letra Rajdhani, filo en **modo RGB** que rota de color, píxeles que suben en el Inicio, Nubecita con auriculares, fondo con rejilla en perspectiva | Maqueta: «Más temas» |
| **Invierno** | Azul hielo y blanco, letra Nunito, **nieve cayendo**, Nubecita con gorro rojo de lana, fondo de colinas nevadas con pinos | Maqueta: «Más temas» |
| **Cyberpunk** | Negro violáceo con amarillo neón `#FCEE0A`, cian `#00F0FF` y magenta `#FF2A6D`; esquinas en chaflán; Orbitron en títulos y Rajdhani en el resto; **reloj con efecto glitch** (capas de color desplazadas que parpadean); **líneas de barrido** sobre los paneles; lluvia de neón; Nubecita con visor; fondo de ciudad con letreros de neón | Maqueta: «Más temas» |

**Se entregan exactamente estos 9 temas construidos** (Profesional, Oficina, Videojuegos,
Invierno, Cyberpunk y los cuatro Anime). **Los valores exactos de los 9** están en
`docs/maqueta-rediseno/temas.json`: 19 colores por tema con su papel, letra y letra de
títulos, radios, tipo de esquina y la lista de decoraciones que lleva encendidas. Es el
fichero del que salen los temas de serie en la fase 0. Todo lo demás lo crea el usuario con el Estudio de temas
(decisión C). Muchos más temas de serie no aportan: cuesta mantenerlos y el usuario ya
puede hacerse los que quiera.

Reglas de los temas:

- **Un tema es un paquete de valores, no código aparte.** Todos los temas pintan las mismas
  piezas con los mismos componentes; lo que cambia son los valores de sus colores (B), sus
  medidas (radio, grosor del filo, separaciones), su fuente y qué decoraciones lleva
  encendidas (pétalos, destellos, halo, mascota…). Así un tema nuevo es un fichero de
  valores y no una rama del código.
- **Las decoraciones propias de un tema** (pétalos de Anime, destellos, Nubecita) son
  piezas opcionales que cualquier tema puede encender, con su interruptor en Ajustes.
- **Migración del config** (`Esquema.cs`, con su copia de seguridad): `Tema.Estilo`
  desaparece. `normal` y `neon` pasan a Profesional; `nube` pasa a Anime si el usuario no
  ha tocado colores, y si no, a Profesional conservando sus colores como personalizaciones
  (B). La migración se prueba como las demás (`Pruebas`, grupo de migraciones).
- **Al quitar los estilos**, `Pruebas.Confirmar.cs` (31 usos), `MainWindow.DiagNeon.cs` y la
  parte de `VentanaAjustes.cs` que los elige se rehacen para los temas; nada de dejar código
  muerto.

### B. Todos los colores de todo, editables por el usuario

**Cualquier color de cualquier elemento de la app se puede cambiar.** Sin excepciones: la
isla, su filo y su destello, cada icono de sistema, los puntos de las apps, el reloj, la
insignia, cada parte de cada panel, los deslizadores, los botones (en reposo, al pasar el
ratón, pulsados, desactivados), las barras de desplazamiento, el dock, los laterales, los
avisos, la ventana de Ajustes…

Cómo se hace:

1. **Un catálogo de colores con nombre** (ampliar `Roles.cs` hasta cubrir cada elemento).
   Cada color de la interfaz tiene una clave estable y legible, agrupada por zona:
   `isla.fondo`, `isla.filo`, `isla.destello`, `isla.halo`, `app.punto`,
   `app.punto.activo`, `reloj.texto`, `insignia.fondo`, `panel.fondo`, `tarjeta.fondo`,
   `tarjeta.filo`, `boton.fondo`, `boton.fondo.raton`, `boton.texto.desactivado`,
   `deslizador.lleno`, `deslizador.pomo`, `scroll.pastilla`, `energia.apagar`…
2. **Cascada**: el valor de una clave sale de (1) lo que haya puesto el usuario; si no, (2)
   lo que diga el tema; si no, (3) se **deriva** de los colores básicos del tema (fondo,
   texto, acento), como ya hace `Roles` con superficie o texto atenuado. Así un tema puede
   definir solo cuatro colores y todo sale coherente, y el usuario puede afinar cualquiera.
3. **Nada de colores escritos a mano en la interfaz.** Hoy hay **53** (`Color.FromRgb`,
   `Brushes.Gold`, `Brushes.Tomato`… en `MainWindow*.cs`, `VentanaAjustes*.cs` y
   `Biblioteca.cs`): cada uno pasa a ser una clave del catálogo. Una **prueba** lo vigila:
   recorre el código y falla si aparece un color literal fuera del catálogo y de los temas.
   Otra prueba exige que todas las claves tengan valor en todos los temas (o se deriven).
4. **Guardado**: en `config.json`, `Tema.Colores` es un diccionario `clave → #AARRGGBB` solo
   con lo que el usuario ha cambiado. Cambiar de tema conserva o descarta esas
   personalizaciones, a elección del usuario (se pregunta).
5. **Ajustes > Colores**: la lista de todas las claves agrupadas por zona (Isla, Inicio,
   Paneles, Laterales, Dock, Avisos, Ajustes), cada una con su muestra, el selector propio
   (`SelectorColor.cs`, con opacidad), **«Restablecer»** por color y **«Restablecer todo»**,
   un buscador («reloj», «apagar»…) y un modo **«Elegir en pantalla»**: se pulsa cualquier
   elemento de la barra y se abre su color. Los cambios se ven en vivo.
6. **Contraste**: al elegir un color de texto o de icono, si no se lee sobre su fondo
   (`Contraste.Entre` por debajo de 4,5:1 en texto, 3:1 en iconos), se avisa en el propio
   selector. Se avisa, no se impide: manda el usuario.
7. **Exportar e importar** un tema personalizado como fichero (los colores, las medidas y
   las decoraciones encendidas), para poder compartirlo.

### Alcance: el tema pinta TODA la app

Un tema no es «la isla y el Inicio»: **cada superficie** de NubeBar toma sus colores, formas,
letra y decoraciones del tema, y cambia con él. Las maquetas solo dibujan algunas en cada
tema; las demás llevan las mismas piezas y cambian igual. La lista completa:

- **La barra**: la isla (recogida y desplegada), el modo barra de lado a lado, la barra
  vertical (`MainWindow.Vertical.cs`) y sus paneles flotantes.
- **Paneles de la isla**: Inicio (portada y lista de resultados), audio, red, batería,
  centro de notificaciones, bandeja.
- **Avisos que asoman**: notificaciones en la isla, volumen, brillo, teclas.
- **Vista general** (`MainWindow.Vista.cs`): tarjetas de ventanas, su menú y la mini lista de
  ventanas de un grupo.
- **Menús y diálogos**: clic derecho en apps y en la isla, menú de los resultados del Inicio
  (`MainWindow.MenuApp.cs`), confirmaciones (cerrar, forzar, apagar).
- **Laterales** izquierdo y derecho, y el **dock**.
- **Ajuste de ventanas**: la previsualización de zonas al arrastrar (`MainWindow.Previa.cs`) y
  los indicadores y avisos de NubeHypr.
- **La ventana de Ajustes entera**: todas sus páginas, el editor de módulos, la biblioteca de
  widgets, la guía y los formularios de reglas.
- **El Estudio de temas** (con su vista previa pintando el tema que se edita).
- **La escena de arranque**, las barras de desplazamiento y las ayudas emergentes (tooltips).

**Regla de seguridad: un tema nunca puede dejar Ajustes ilegible.** Como el usuario puede
cambiarlo todo, puede hacer un tema en el que no se lea nada. Por eso:

- Si el texto de Ajustes o del Estudio no llega a 4,5:1 sobre su fondo con el tema puesto,
  **esas dos ventanas se pintan con Profesional** y avisan arriba de por qué.
- Siempre hay una salida: **«Volver al tema de serie»** en el menú de clic derecho de la isla,
  y el modo seguro que ya existe (`Panico.cs`) arranca con Profesional.
- Un `.nubetema` importado que no pase la validación no se aplica.

### C. El Estudio de temas: el usuario crea los suyos, con muchísimas herramientas

Maqueta funcional: pantalla «Estudio de temas» (`docs/maqueta-rediseno/Estudio.dc.html`), con los 10
apartados, la vista previa en vivo, el contraste calculado, la simulación de daltonismo,
antes/después, deshacer y rehacer y el medidor de consumo.

Una ventana propia, **Estudio de temas**, donde el usuario parte de cualquiera de los 9
temas (o de cero), lo cambia todo y lo guarda como suyo. Tiene que dar **muchísimas**
herramientas, sin que haga falta saber de diseño. Todas son posibles en WPF. Va por
apartados:

**1. Punto de partida**
- Duplicar cualquier tema (de serie o propio) o empezar de cero; nombre, autor, familia
  (Sobrios, Anime, Estaciones, Fiestas, Mis temas…), claro u oscuro.
- **Generador rápido**: eliges 1 a 3 colores y propone el tema entero derivado (decisión B).
- **Desde una imagen o el fondo de escritorio**: saca la paleta de una foto
  (reutilizar `Paleta.cs` y el modo «Desde wallpaper» que ya existen).
- **Armonías**: complementaria, análoga, tríada, monocromática… a partir del acento.
- **«Sorpréndeme»**: un tema aleatorio coherente (que pase el contraste), y otra vez.

**2. Colores** (la decisión B entera)
- Todas las claves del catálogo, por zona, con buscador.
- **Cuentagotas en pantalla**: coge el color de cualquier punto del escritorio.
- **«Elegir en pantalla»**: pulsas un elemento de la barra y se abre su color.
- Cada relleno puede ser **color liso o degradado lineal** (paradas, ángulo, opacidad).
- Opacidad por color; paletas guardadas; colores recientes; copiar y pegar hexadecimal.
- **Contraste en vivo** (aviso por debajo de 4,5:1 en texto, 3:1 en iconos) y **simulación
  de daltonismo** (protanopia, deuteranopia, tritanopia) sobre la vista previa.

**3. Forma**
- Radios por pieza (isla, paneles, tarjetas, botones, deslizadores) y tipo de esquina
  (redonda o en chaflán).
- Grosor y estilo del filo (liso o degradado), separaciones, relleno de las tarjetas, alto de
  la isla, tamaño de iconos.
- Sombras: color, difuminado, desplazamiento y opacidad.
- Transparencia de cada superficie.

**4. Letra**
- Elegir entre las fuentes instaladas o **importar** un `.ttf` u `.otf` (viaja con el tema).
- Letra distinta para títulos y para el resto; tamaño base y escala; peso; mayúsculas y
  espaciado de los títulos de sección; cifras tabulares en relojes.

**5. Decoraciones**, un catálogo de piezas con sus parámetros, combinables
- **Partículas**: forma (pétalo, hoja, gota, copo, estrella, burbuja, corazón, confeti… o una
  **forma propia** desde un trazado SVG o una imagen PNG), cantidad, tamaño, colores,
  velocidad, dirección (caer, subir, de lado), giro y vaivén, y **dónde** (Inicio,
  laterales, paneles; nunca sobre el escritorio).
- **Destellos**: dónde (junto a la hora, en las tarjetas…), cuántos, color y ritmo.
- **Halo** de la isla: color, tamaño y respiración.
- **Destello del filo**: colores, anchura y velocidad, o apagado; y el modo **RGB** con
  colores que rotan.
- **Mascota Nubecita** (`Nubecita.cs`): cara (alegre, seria, triste, dormida, guiño…),
  accesorios (*hachimaki*, bufanda, gorro, gafas de sol, auriculares…), colores, dónde sale,
  o **sustituirla por una imagen propia**.
- **Marca de agua**: un texto, un kanji o una imagen tras las tarjetas, con opacidad y sitio.
- **Fondo de escritorio del tema** (opcional): color, degradado, imagen o pase de fotos
  (`GestorFondos`).

**6. Movimiento**
- Velocidad general y tipo de curva (con rebote o suave); cómo entran los paneles (escala,
  deslizar o fundido) y el escalonado; cuánto se levantan los iconos; la lupa del dock.
- Siempre por debajo del nivel de animaciones del sistema: si está en «reducido», manda.

**7. Textos y detalles**
- Saludo con plantilla (`{saludo}`, `{nombre}`, `{hora}`…), la pista del buscador, el formato
  de fecha y hora.
- Iconos de sistema: contorno o relleno, color y tamaño; halo para iconos oscuros sí o no.

**8. Vista previa y prueba**
- Vista previa en vivo de **todas** las superficies (isla, Inicio, cada panel, laterales,
  dock, avisos) con datos de ejemplo; **antes y después**; probar el tema en la barra real
  con un botón de **volver** que deshace en un clic.
- **Deshacer y rehacer** ilimitado, e historial de versiones del tema.
- **Medidor de consumo** del tema: estima el coste de sus decoraciones (partículas,
  brillos) y avisa si va a gastar batería; con un tope de partículas que no se puede
  saltar.

**9. Automático**
- Tema distinto según la hora (claro de día, oscuro de noche), según la fecha (fiestas),
  según el escritorio virtual o según el monitor.

**10. Compartir**
- Exportar e importar como un único fichero `.nubetema` (un zip con el JSON de valores, las
  fuentes y las imágenes). **Solo datos, nunca código**: al importarlo se valida todo, lo que
  no se entiende se ignora y se avisa, y un tema roto no puede tumbar la barra (vuelve al
  último tema bueno y lo apunta en el log).
- Más adelante, una galería de temas de la comunidad.

Encaje en el código: el Estudio es otra ventana como `VentanaAjustes` (reutiliza
`SelectorColor.cs` y los controles de Ajustes); los temas de serie y los del usuario usan
**el mismo formato** de fichero, así que «duplicar un tema de serie» es copiar su fichero.
Las pruebas exigen que los 9 de serie pasen el contraste y que un `.nubetema` mal formado se
rechace sin romper nada.

---

## Tema Profesional: los colores

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

Encaje en el código: estos son los valores del tema Profesional dentro del catálogo de la
decisión B; lo que no se fije aquí lo deriva `Roles` como ya hace. Comprobar contraste con
`Contraste.Entre` (las pruebas de E8 ya miden texto sobre superficies).

## Temas Anime: los colores y lo suyo

Pantalla «Temas anime» de la maqueta: **una sola pantalla con un selector abajo** para pasar
entre Sakura, Samurái, Otoño y Sad en vivo, que es justo como debe sentirse el selector de
temas en Ajustes. Las cuatro variantes usan **las mismas piezas**; cambian sus valores
(colores, letra, radios), la mascota (accesorio y cara), la partícula que cae en el Inicio
(pétalo, hoja o gota) y el fondo de escritorio. Los valores exactos de cada una están en el
código de la maqueta (`docs/maqueta-rediseno/Anime.dc.html`, objeto `TEMAS`) y, en limpio,
en `docs/maqueta-rediseno/temas.json`.

Letras de las variantes, todas con licencia OFL e incrustables: M PLUS Rounded 1c (Sakura),
Shippori Mincho para títulos y Zen Maru Gothic para el resto (Samurái), Zen Maru Gothic
(Otoño y Sad).

Lo que sigue es la variante **Sakura**, que es un tema **claro**.

| Papel | Valor |
|---|---|
| Superficie de isla y paneles | `#FFFBFD` al 95 % (paneles con degradado `#FFF7FB → #FBF7FF`) |
| Tarjeta | `#FFFFFF`, filo `#C2477A` al 14 %, sombra rosada suave |
| Texto | `#4A3B5C` (ciruela) |
| Texto tenue | `#75628C` (≥ 4,5:1 sobre blanco) |
| Acento | sakura `#FF8FB8` (configurable) |
| Acento para texto y enlaces | `#C2477A` |
| Secundarios | lavanda `#B8A4F4`, cielo `#7EC8F5`, menta `#7FD8B5` |
| Fondo de escritorio | degradado `#FFE4EF → #F1E6FF → #DCEEFF` con nubes blancas, quieto |

Forma y letra: radios grandes (isla 25, tarjetas 20, botones redondos o en píldora), filo
de la isla y de los paneles en **degradado sakura → lavanda** de 1,5 px, separadores como
rayitas lavanda redondeadas. Letra **M PLUS Rounded 1c** (Google Fonts, licencia OFL),
**incrustada** en el ejecutable como recurso de WPF.

Decoraciones (cada una con su interruptor):

- **Nubecita** (`Nubecita.cs`, ya existe con su cara y su parpadeo) en la cabecera del Inicio,
  con coloretes y flotando arriba y abajo 4 px en 3,2 s; y en pequeño como botón Inicio.
- **Pétalos de sakura** cayendo **solo dentro del menú Inicio**, nunca sobre el escritorio:
  7 formas vectoriales en un `Canvas`, cada una con su caída, giro y vaivén (9–14 s),
  semitransparentes y sin recibir el ratón. Se paran con el panel cerrado.
- **Destellos** de cuatro puntas que titilan junto a la hora (2,6 s, desfasados).
- Ecualizador con una barra de cada color pastel; play como círculo sakura relleno; el
  teclado activo como píldora en degradado sakura → lavanda; «Apagar» rosa que al pasar el
  ratón se rellena de sakura.

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
- Los niveles de animación (`Animacion.Elegir`, `Animacion.Adorno`).
- `Nubecita.cs` para la mascota del tema Anime, y `SelectorColor.cs` para Ajustes > Colores.
- **No** se reutilizan los estilos Normal, Nube y Neón: se quitan (decisión A). Lo útil de
  Neón (el halo con `DropShadowEffect` que respira) pasa a ser una decoración más.

---

## Orden de trabajo

Cada fase se publica como versión para probarla antes de seguir.

0. **Los cimientos: catálogo de colores y temas** (decisiones A y B). El catálogo con todas
   las claves y su cascada; los 53 colores escritos a mano pasados al catálogo; la prueba
   que lo vigila; los temas como paquetes de valores; quitar Normal, Nube y Neón con su
   migración del config; y Ajustes > Colores con todo editable. **Va primero**: si se pinta
   la isla nueva antes de esto, habría que rehacerla para que sus colores fueran editables.
1. **Tema Profesional**: sus valores, filos, tarjetas, tipografía, títulos de sección.
2. **La isla**: destello del filo, halo, reloj con brillo, insignia con latido, puntos de
   las apps.
3. **El menú Inicio**: entrada y escalonado, buscador con franja, música (carátula,
   progreso, ecualizador por pico), teclado y energía.
4. **Paneles de la isla**: red, audio, batería, notificaciones y bandeja.
5. **Laterales y dock**: entradas, secciones, deslizadores, anillos y lupa del dock.
6. **Temas Anime** (Sakura, Samurái, Otoño, Sad): sus valores, las fuentes incrustadas y las
   decoraciones que estrenan (partículas, destellos, mascota con accesorios, marca de agua).
7. **Temas Oficina, Videojuegos, Invierno y Cyberpunk**: sus valores y lo que estrenan
   (esquinas en chaflán, modo RGB, glitch, líneas de barrido, nieve, píxeles).
8. **Selector de temas** en Ajustes, agrupado por familias y con vista previa de cada uno.
9. **Estudio de temas** (decisión C), por orden: colores y forma; letra; decoraciones;
   movimiento y textos; vista previa, deshacer y medidor de consumo; automático; exportar e
   importar `.nubetema`.

### Dónde va el rediseño

| Fase | Qué | Estado |
|---|---|---|
| **0** | Cimientos: catálogo, cascada, los 9 temas, migración, fuera los 3 estilos | ✅ |
| **1** | Tema Profesional: panel y tarjeta separados, títulos de sección, la letra del tema | ✅ |
| **2** | La isla: halo (en `Aura`, sin recorte), destello del filo, reloj con brillo, insignia con latido | ✅ |
| **3** | El menú Inicio: franja del buscador, brillo del progreso, escalonado 50 ms / 10 px | ✅ |
| **4** | Paneles de la isla: ancla marcada, batería que se llena, bandeja con rebote, buscar que gira | ✅ |
| **5** | Laterales y dock: entrada desde el borde, secciones escalonadas, lupa con vecinos | ✅ |
| **6** | Temas Anime: partículas, marca de agua, las seis letras incrustadas, la mascota con accesorios | ✅ |
| **7** | Oficina, Videojuegos, Invierno, Cyberpunk: chaflán, modo RGB, glitch, barrido | ✅ |
| **8** | Selector de temas por familias | ✅ (vino con la fase 0) |
| **9** | El Estudio de temas | ◐ el esqueleto: guardar, listar, ponerse y borrar temas tuyos |

**La mascota lleva accesorios.** `Accesorios.cs` dibuja los siete que los temas nombran
—hachimaki, bufanda, gorro, auriculares, visor, coloretes y la lágrima— en una caja de 100×100
que un `Viewbox` escala al tamaño que haga falta. Una sola nube con disfraces, no siete nubes:
siete siluetas serían seis que se quedan sin actualizar el día que la nube cambie. Un accesorio
que esta versión no sepa dibujar (un `.nubetema` de la comunidad pidiendo `mascota:sombrero`)
no pinta nada y sigue, que es lo correcto: no se inventa un sombrero.

**Y de la fase 9 está el esqueleto, no el Estudio.** Lo que hay: ponerle nombre a lo que has
afinado, que salga en una lista con su tira de colores y de qué tema de serie sale, ponértelo
y borrarlo (con dos pulsaciones, dentro de la página, sin un `MessageBox` que este programa no
tiene en ciento cincuenta ficheros). Un tema tuyo guarda el **id del tema de partida** más lo
que le cambiaste —colores, radios y letras—, no los ~130 colores resueltos: así el día que
Sakura mejore su rosa, tu tema mejora contigo, y el fichero se lee de un vistazo.

Lo que NO está, y conviene no dar por hecho: la vista previa en vivo, deshacer y rehacer, la
simulación de daltonismo, el medidor de consumo y el `.nubetema` empaquetado con las fuentes y
las imágenes dentro. Eso es el Estudio de verdad y sigue pendiente.

**Los gestos de borde ya no se disparan dentro de un juego.** No es del plan, es un fallo que
salió jugando: a pantalla completa, girar rápido la cámara abría los paneles laterales y hacía
asomar la isla. La cuenta del borde estaba bien; la pregunta no. Ahora, antes de mirar si el
puntero está en el filo, se mira **de quién es el puntero** (`GestosDeBorde.cs`): acotado con
`ClipCursor` significa que lo lleva otro, y escondido **más** algo a pantalla completa, también.
Y además se le pide reposo: cruzar un filo ya no abre nada, hay que dejar el puntero ahí 300 ms,
lo mismo que la isla pedía desde siempre.

**Las letras ya viajan dentro.** Las seis OFL están en `Fuentes/` con sus licencias y
declaradas como `Resource` en el csproj; `MainWindow.LetraDe` las busca en el programa ANTES
que en el sistema, a propósito: si alguien tiene instalada otra versión de Rajdhani, el tema
tiene que verse como se diseñó y no como lo tenga esa máquina.

**Y pesan 2,9 MB en vez de 38,8.** Las tres japonesas —M PLUS Rounded 1c, Zen Maru Gothic y
Shippori Mincho— traían el juego CJK entero: miles de kanji que NubeBar no escribe nunca. Van
recortadas con `fonttools` al latín, la puntuación, los símbolos que la interfaz usa y los
pocos kanji que nombran los temas. Shippori Mincho sola bajó de 8,5 MB a 327 KB. Si algún día
un tema necesita más kanji, el recorte está en el guion y se vuelve a pasar.

**Cómo se comprueba una decoración:** `NubeBar.exe --capturas <carpeta>` fotografía el panel
lateral **y el menú Inicio** en los nueve temas (`35-tema-*.png` y `36-inicio-*.png`). Las
decoraciones viven en el Inicio, no en el lateral: sin esas fotos, las fases 6 y 7 se
comprueban mirando el tema equivocado. Las tres veces que las partículas estuvieron mal
—sin arrancar, invisibles en Invierno, amontonadas en una franja— salieron ahí y en ningún
otro sitio.

### Fase 0: lo que ya está en pie

Construido y con las pruebas en verde (`NubeBar.exe --pruebas`: 10 583 comprobaciones, 0 mal):

| Pieza | Fichero | Qué hace |
|---|---|---|
| El valor «color» | `Relleno.cs` | Un color liso o un degradado lineal, en el formato de la maqueta (`rgba()`, `linear-gradient()`). Se lee, se escribe, se mide y se convierte en pincel de WPF |
| El tema como paquete | `TemaBase.cs` | Un tema son datos: 19 colores, letra, radios, esquinas y decoraciones. Ni un `switch` por tema |
| Los nueve de serie | `temas-de-serie.json` | Copia del objeto `temas` de la maqueta, incrustada en el exe. Una sola desviación, apuntada dentro del propio fichero |
| El catálogo | `Catalogo.cs` | ~130 claves con nombre, zona, papel y de dónde salen. Las 19 base son claves como las demás |
| La cascada | `Pintura.cs` | Usuario → tema → derivación, con caché, corte de ciclos, medida de contraste y la regla de seguridad de Ajustes (`ParaAjustes()`) |
| Las pruebas | `Pruebas.Temas.cs` | Que los nueve temas declaren las 19 claves, que las ~130 resuelvan en todos, que ninguna derivación dé vueltas y que todo lo que hace de texto o de icono se lea |

También construido:

| Pieza | Fichero | Qué hace |
|---|---|---|
| La fachada | `Roles.cs` | `Roles` ya no deriva nada: cada rol con nombre viejo es una clave del catálogo. Los widgets, `HyprPinta`, `Tonos` y los paneles no se tocaron, y su color ya sale del catálogo |
| El puente | `TemaBase.DeTemaViejo` | Los ocho temas de antes entran a la MISMA cascada como paquetes de valores. Una sola cascada, no dos en paralelo |
| El centinela | `Pruebas.SinColoresAMano.cs` | Lee `MainWindow*.cs`, `VentanaAjustes*.cs` y `Biblioteca.cs` y falla si la interfaz decide un color por su cuenta |

Los colores escritos a mano se pasaron a claves: `Brushes.Gold` → `bateria.ahorro`,
`Brushes.Tomato` → `bateria.bajo` / `boton.destructivo.texto` / `ajustes.aviso.mal`,
`Brushes.YellowGreen` → `ajustes.aviso.bien`, los dos hexadecimales del pulso de audio →
`pulso.frio` / `pulso.calido`, los cuatro velos del editor de la barra → `editor.zona.*`, y
los colores de «antes de que haya tema» (negro, blanco y `CornflowerBlue`) → el tema de serie.

El centinela deja pasar tres cosas, y ninguna es un color: `Brushes.Transparent` (estructural
— sin fondo, un `Panel` no recibe el ratón en los huecos), `Color.FromArgb(0x2A, c.R, c.G,
c.B)` (una opacidad sobre un color que ya venía del tema) y lo que lleve el comentario
`// color literal a propósito`, que hoy son las máscaras de opacidad del carrusel y el pincel
que `FormattedText` pide para medir un ancho y nunca pinta.

Y el cambio de fuente de verdad, que fue un solo paso porque no se podía partir:

| Pieza | Fichero | Qué hace |
|---|---|---|
| El config | `Config.cs` | `Tema.Id` (cuál de los nueve) + `Tema.Colores` y `Tema.Radios` (lo que le has cambiado). Las trece claves de color de antes se quitaron de la clase; del fichero de nadie se borran |
| La migración | `Esquema.cs`, v4→v5 | `normal` y `neon` → Profesional; `nube` → Sakura si no tocaste colores. Lo que elegiste se conserva clave por clave; lo que era el valor por defecto no se arrastra |
| La barra | `MainWindow.xaml.cs` | El color sale de `Tema.Id` por la cascada; el wallpaper y las correcciones entran como personalizaciones de la sesión |
| Ajustes | `VentanaAjustes.Colores.cs` | El selector de los nueve por familias, la lista de las ~130 claves por zonas con buscador, contraste en vivo, «Restablecer» y «Restablecer todo» |
| Exportar | `Temas.cs` | Un tema del usuario es su tema de partida y lo que le cambió. El `.nubetema` con fuentes e imágenes es la fase 9 |
| Las pruebas | `Pruebas.MigracionTema.cs` | La migración, caso por caso: los dos estilos, Nube tocado y sin tocar, un Gruvbox entero, los radios, que no borra nada y que migrar dos veces no pisa nada |

Con eso se retiraron los ocho temas de antes (`Temas.Predefinidos`), el puente
(`TemaBase.DeTemaViejo`) y `Roles.Desde(Tema, …)`. Las pruebas que iban «en los siete temas»
van ahora en los nueve.

Y por último, fuera Normal, Nube y Neón:

| Pieza | Fichero | Qué hace |
|---|---|---|
| La forma | `Forma.cs` | Radios, hueco, relleno, grosor del filo y resplandor salen del TEMA. `EstiloVisual.cs` se borra entero |
| Las decoraciones | `Decoraciones`, `Medidas` | Los nombres que el código consulta, como constantes: una errata en `"halo"` daba un tema que no brilla y un rato perdido |
| El resplandor | `Formas.Del` | Ya no hay un estilo que brille: hay temas que llevan la decoración `halo` encendida **y** con color visible. Ocho de nueve |
| Ajustes | `VentanaAjustes.cs` | El selector de estilo desaparece: la decisión era el tema, y está en Colores. Queda el radio, encima del tema |
| Las capturas | `MainWindow.DiagCapturas.cs` | Fotografía los nueve temas en vez de los tres estilos — la pantalla «Paneles en cada tema» de la maqueta |

Mirar los nueve destapó un fallo viejo: **la concha de un panel lateral se pintaba una sola
vez, al crear la ventana**, así que cambiar de tema con el panel cerrado dejaba sus tarjetas
del tema nuevo sobre una concha del viejo. Medido en Sakura: concha `#0F0F16` con tarjetas
`#FFFFFF`. Arreglado, y comprobado midiendo el píxel de la concha en los nueve.

**La fase 0 está cerrada.** Lo que queda pendiente y pasa a las fases siguientes:

1. **La fachada `Roles`** se va quedando sin consumidores a medida que las fases 1 a 8 pasen
   cada pieza a preguntar por su clave. Mientras tenga alguno, ahí está.
2. **De Ajustes > Colores**: editar degradados y «Elegir en pantalla» (ver
   para por qué los dos esperan al Estudio y a las fases 1-8).
3. ~~**El chaflán** de Videojuegos y Cyberpunk es hoy un radio 0, o sea una esquina recta.~~
   **Cerrado en la v1.1.2, y por la otra punta.** La fase 7 cortó la esquina en diagonal en
   los paneles y en las tarjetas (`ChaflanSiToca`), pero no en la **isla**, que se viste a
   mano en `AplicarTema` y solo entiende de radios. O sea que lo que quedaba a la vista todo
   el día era una isla de cuatro esquinas rectas con paneles cortados colgando de ella: lo
   peor de las dos formas. Lo zanjó el usuario en una frase —«no quiero que ningún tema tenga
   forma de rectángulo»— y los dos temas pasan a redondos (isla 20, panel 18, tarjeta 14,
   botón 10). El chaflán se queda como clave del tema para quien se haga el suyo; la regla
   está escrita en `NingunTemaEsUnRectangulo`.
4. **El halo de la isla está apagado a propósito**, y conviene leer esto antes de encenderlo
   en la fase 2. `Isla` tiene `ClipToBounds="True"` —lo necesita: es lo que recorta el cajón
   al crecer— y un `DropShadowEffect` sobre un elemento recortado se recorta con él, al
   RECTÁNGULO de la caja y no a la forma redondeada. Sale un halo cuadrado alrededor de una
   isla con las puntas redondas, que asoma justo por donde la curva se separa de la esquina
   (medido: `#13182E` en la esquina contra `#0C0E17` del interior). Hacerlo bien es ponerlo en
   una pieza DETRÁS de la isla, con su misma forma y sin recorte. Es lo que se hizo con los
   paneles laterales, y por el mismo motivo: aquí se escribió que ellos «sí lo llevan y se
   ve bien, sus conchas no recortan», y era media verdad. La concha de `PanelShell` no
   recorta, pero la de un panel lateral no es ésa —es `VentanaSuperpuesta.Tarjeta`, que sí
   recorta— y su halo salió cuadrado en la v1.0.5. Ahora cada una de las dos lleva su aura
   detrás; la de los laterales, en `VentanaSuperpuesta.Envoltura`, con la regla exigida en
   la prueba `ElHaloDelPanelLateralNoVaEnLoRecortado`.

Todo lo que se construya en las fases 1 a 8 toma sus colores, medidas y decoraciones **del
tema**, nunca de un valor escrito en el código: es lo que hace posible que el Estudio pueda
cambiarlo todo.

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
