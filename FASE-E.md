# Macrofase E — La capa de escritorio

> **Estado: E0 a E8 hechos. Quedan las comprobaciones que necesitan ojos y manos.**
> Las fases A–D están construidas y D6–D8 se conservan como fases de estabilización de
> NubeHypr (ver `NUBEHYPR.md`).

---

## 1. El objetivo, en una frase

Que NubeBar deje de ser *una barra con paneles* y pase a ser **la capa de escritorio**: un
conjunto de superficies independientes —barra, panel izquierdo, panel derecho, dock,
notificaciones— sobre un escritorio amplio y limpio, con ventanas reales de Windows y el
fondo de Wallpaper Engine a la vista.

Es el paso de «KDE Plasma en Windows» de la intención a la composición.

---

## 2. Las referencias visuales

Hay dos, y la segunda manda.

### Referencia principal — composición vertical

Una captura de un escritorio Hyprland con:

- **Barra lateral estrecha y permanente** en el borde izquierdo, de iconos, de arriba abajo.
- **Panel de control desplegable** pegado a esa barra: conmutadores (Wi-Fi, Bluetooth, modo
  noche, cafeína), deslizadores de volumen y micrófono, lista de notificaciones y un
  reproductor multimedia, apilados en una sola columna.
- **Dock lateral de aplicaciones** en el borde opuesto, también vertical.
- **Ventanas reales en el centro**: una terminal, una calculadora.
- **Notificación flotante** en la esquina superior derecha, pequeña y con su aspa.
- **Fondo visible** ocupando todo lo que las superficies no tapan.
- **Mucho espacio libre.** Las superficies ocupan los bordes; el centro es del usuario.

### Referencia secundaria — composición horizontal

La primera captura: barra superior compacta con selector de escritorios y apps a la
izquierda, estado del sistema y reloj a la derecha, panel lateral de ajustes, panel derecho
con una aplicación, dock inferior centrado, notificación arriba a la derecha.

### Por qué se conservan las dos

**Son la misma arquitectura girada.** Una tiene la barra arriba y el dock abajo; la otra, la
barra a la izquierda y el dock a la derecha. Lo que define el objetivo no es «arriba» o «a la
izquierda»: es que haya **una barra fina y permanente, dos paneles laterales desplegables, un
dock y unas notificaciones**, cada uno independiente, con el escritorio respirando en medio.

NubeBar ya soporta los cuatro bordes y las dos orientaciones (D6.2 las fotografió las ocho).
Así que el objetivo no es elegir una composición: es que la arquitectura dé las dos sin
reescribirse. La referencia principal es la vertical porque es la más exigente —paneles
flotantes al lado, no desplegados dentro de la barra— y lo que funciona ahí funciona girado.

### Lo que NO se copia

Ni las aplicaciones, ni los colores, ni los personajes, ni el estilo. De las imágenes se toma
**composición, proporciones, jerarquía y sensación de calidad**. Nada más.

En concreto, lo que se descarta a propósito:

| De la referencia | Por qué no |
|---|---|
| El verde menta de los conmutadores | La paleta es de NubeBar: azul para foco, coral para avisos |
| El personaje del fondo | El fondo es tuyo, el que ponga Wallpaper Engine |
| Discord, Nucleus, Spotify, la mascota del `fastfetch` | Son las apps de otro; aquí salen las tuyas, de verdad |
| El estilo *cyberpunk* / neón | Superficies oscuras, opacas y sobrias |

---

## 3. La composición objetivo

```
┌──┬────────────────────────────────────────────────────────┬──┐
│  │                                                        │  │
│B │   ┌──────────────┐                    ┌─────────────┐  │D │
│A │   │  PANEL       │   ventanas         │ notificación│  │O │
│R │   │  IZQUIERDO   │   reales de        └─────────────┘  │C │
│R │   │  ajustes,    │   Windows                           │K │
│A │   │  controles,  │   (tiling o flotantes)              │  │
│  │   │  accesos     │                 ┌────────────────┐  │  │
│  │   └──────────────┘                 │ PANEL DERECHO  │  │  │
│  │                                    │ apps, medios,  │  │  │
│  │        escritorio y Wallpaper      │ contexto       │  │  │
│  │        Engine, a la vista          └────────────────┘  │  │
└──┴────────────────────────────────────────────────────────┴──┘
```

Las siete superficies, y qué es cada una:

| Superficie | Qué es | Siempre visible |
|---|---|---|
| **Barra** | fina, permanente. Escritorios y apps activas a un lado; estado, audio, red, batería, notificaciones y reloj al otro | sí |
| **Panel izquierdo** | ajustes, accesos, controles, configuración | no: se abre y se cierra |
| **Panel derecho** | aplicaciones, notificaciones, centro multimedia, información contextual | no |
| **Dock** | favoritas + abiertas, en el borde opuesto a la barra | sí, o al acercarse |
| **Notificaciones** | pequeñas, discretas, en una esquina | solo cuando las hay |
| **Escritorio** | amplio, limpio, con el fondo real | siempre: es el protagonista |
| **Ventanas** | las de Windows, organizadas por NubeHypr o flotando | las que tengas |

**La regla que gobierna todo:** el escritorio es lo que más superficie ocupa. Si una
composición de E acaba con más píxeles de NubeBar que de escritorio, está mal.

---

## 4. La identidad

- **La nube de `Nubecita.cs`** como elemento de marca en la barra, en los paneles y en las
  transiciones. Protagonista visual, **sutil y funcional**: nunca una mascota gigante que tape
  el escritorio. `HyprPinta.TamanoDeLaNube` ya impone los topes y seguirá imponiéndolos.
- **Azul** para foco, navegación y acciones principales. **Coral** para avisos y acciones
  destructivas. Las dos salen de los roles del tema, no de un color escrito a mano, que es lo
  que las hace funcionar en los siete temas y en claro.
- **La geometría de Nubecita** en bordes y transiciones: los redondeos y las curvas salen de
  la misma forma, no de un radio cualquiera.
- Paleta profesional y coherente. Nada de colores aleatorios ni de neón.
- **Ni una tarjeta de más.** La referencia tiene cuatro conmutadores y dos deslizadores en el
  panel de control, no veinte. Un panel lleno de tarjetas es un panel que nadie lee.

---

## 5. El comportamiento, que es la mitad del objetivo

Esto no es decoración: es lo que separa una capa de escritorio de una maqueta.

1. **La barra no mueve ni reconstruye las ventanas.** Reserva su hueco como appbar y ya está.
2. **Los paneles respetan foco, teclado y clic fuera.** Abrirlos no roba el foco a la ventana
   con la que estás trabajando salvo que el panel necesite escribir.
3. **El audio sigue sonando al abrir cualquier panel.** Ya es así y tiene que seguir siéndolo.
4. **Los deslizadores no se quedan agarrados** al ratón. Es un fallo que ya se arregló una vez.
5. **Las ventanas reales siguen siendo ventanas de Windows.** Nada se dibuja «como si» fuera
   una ventana: se colocan las de verdad.
6. **La previsualización es atravesable** (`WS_EX_TRANSPARENT`, `HTTRANSPARENT`) y no roba
   foco.
7. **Cada superficie es independiente.** Abrir el panel derecho no reconstruye la barra, ni
   cierra el izquierdo, ni toca el dock.
8. **Se adapta** a horizontal, vertical, los cuatro bordes, varios monitores y distintos DPI.

---

## 6. El cambio de arquitectura que esto exige

Aquí está el trabajo de verdad, y conviene decirlo antes de empezar:

**Hoy NubeBar tiene UN panel abierto a la vez.** `_panelAbierto` es un solo campo; abrir
cualquier panel cierra el anterior, y hay una regla explícita de que un aviso no se trague el
menú Inicio. Eso fue una decisión buena para una isla con paneles, y es **incompatible** con
la composición objetivo, donde el panel izquierdo, el derecho, el dock y una notificación
pueden estar los cuatro a la vez.

Ese es el núcleo de E0: pasar de *una isla con un panel* a *varias superficies independientes*,
sin romper nada de lo que ya funciona.

### Qué se reutiliza y qué es nuevo

| Pieza | Estado | Comentario |
|---|---|---|
| Ventana por capas, sin foco, sin Alt+Tab | **existe** | `VentanaSuperpuesta` ya es exactamente eso: ventana aparte, `NoActivable`, colocada en píxeles. Es el cimiento de las superficies nuevas |
| Barra en los cuatro bordes, isla y barra, vertical | **existe** | D6.2 lo fotografió en los ocho casos |
| Módulos en zonas | **existe** | 15 módulos registrados; la barra de E es una redistribución, no un motor nuevo |
| Paneles: audio, red, batería, bandeja, notificaciones, Inicio, control, biblioteca | **existe** | Son el contenido de los paneles laterales. No hay que inventarlos, hay que recolocarlos |
| Vista general de ventanas | **existe** | C4 + D6 |
| Notificaciones | **existe** | Falta la presentación pequeña y discreta en esquina |
| Wallpaper Engine | **existe** | Integración y colores |
| Nubecita, roles, azul/coral, contraste | **existe** | A + D6 |
| NubeHypr: tiling, flotantes, reglas, escritorios, foco, deshacer | **existe** | D0–D9.4 |
| **Varias superficies a la vez** | **NUEVO** | El cambio de fondo. Hoy es una y solo una |
| **Dock** | **NUEVO** | No existe nada |
| **Paneles laterales como superficie propia** | **NUEVO** | Hoy un panel vive dentro de la isla (horizontal) o al lado (vertical) |
| **Composición del escritorio como un todo** | **NUEVO** | Quién ocupa qué borde, qué reserva espacio y qué no |

---

## 7. Las fases

### E0 — Composición global y arquitectura del shell

El reparto de superficies y el motor que las sostiene.

- Un modelo de **superficies**: qué hay, en qué borde, qué tamaño, cuáles reservan espacio de
  trabajo y cuáles flotan por encima.
- **Varias abiertas a la vez**, cada una con su ciclo de vida. Sustituir el `_panelAbierto`
  único por algo que admita N, sin romper la regla de que un aviso no tape lo que abriste a
  propósito.
- Reglas de colocación para los cuatro bordes, varios monitores y DPI distintos.
- Qué pasa cuando dos superficies piden el mismo borde.

*Entregable:* la arquitectura y la distribución, con las superficies vacías pero colocándose
de verdad. Sin contenido nuevo.

### E1 — Barra y selector de escritorios

- Barra fina y permanente, en el borde elegido, en las dos orientaciones.
- Escritorios virtuales **reales** y apps activas a un lado.
- Estado del sistema, audio, red, batería, notificaciones y reloj al otro.
- La nube como marca, pequeña.

*Reutiliza* los 15 módulos, el indicador de escritorios de D6 y los escritorios reales de D4.

### E2 — Dock

- Favoritas y abiertas, en el borde opuesto a la barra.
- Clic para activar, clic en la activa para minimizar, mini lista para elegir ventana.
- Conectado a las ventanas de verdad: `GestorVentanas`, no una lista inventada.

*Nuevo*, pero el comportamiento de las apps ya está resuelto en el carrusel.

### E3 — Panel izquierdo: ajustes y controles

- Conmutadores, deslizadores, accesos, configuración.
- Pocos y grandes, no veinte tarjetas.
- Abre y cierra sin reconstruir nada más.

*Reutiliza* el centro de control, audio, red y batería.

### E4 — Panel derecho: contextual, multimedia y notificaciones

- Reproductor multimedia integrado, conectado al audio real.
- Lista de notificaciones, conectada a las de verdad.
- Información contextual de lo que tengas delante.

*Reutiliza* el módulo de música, `Audio` y el centro de notificaciones.

### E5 — Ventanas reales: tiling, flotantes y foco

- Tiling y flotantes de NubeHypr aplicados al escritorio completo.
- Foco, reglas, previsualización atravesable.
- **Aquí es donde las reglas pasarían a mover ventanas de verdad**, y eso necesita permiso
  explícito. Hasta entonces, simulación.

*Reutiliza* D0–D9.4 entero.

### E6 — Nube, transiciones y Wallpaper Engine

- La nube en transiciones de apertura y cierre de superficies.
- Geometría de Nubecita en bordes.
- Fondo real visible y colores derivados del wallpaper.

### E7 — Perfiles, reglas y persistencia

- Guardar y cambiar composiciones enteras.
- Reglas por perfil.
- Migración del esquema, con el cuidado de siempre: nunca encender nada al actualizar.

### E8 — Rendimiento, accesibilidad, temas y acabado

- Sin repintados en bucle (`RemontajesDeZonas` ya lo mide).
- Teclado completo, foco visible, contraste suficiente, lectores de pantalla.
- Los siete temas, claro, oscuro y alto contraste.
- DPI, multimonitor, desconexión de pantallas.

---

## 8. Lo que este documento prohíbe

- **Nada de maqueta estática.** Cada superficie se conecta con lo real: escritorios virtuales
  de Windows, ventanas de Windows, el audio del sistema, las notificaciones de verdad, el
  Wallpaper Engine instalado. Un dock con iconos de mentira no es E2 a medias: no es E2.
- **No convertirlo en un *dashboard*.** El escritorio es el protagonista.
- **No romper lo que funciona.** A–D están probadas (4961 comprobaciones). E no empieza
  tirando nada.
- **No encender NubeHypr por el camino.** Sigue apagado de serie y en simulación.

---

## 9. Por dónde se empieza

Por **E0 y solo E0**: la arquitectura del shell y la distribución de superficies, dejando por
escrito qué se reutiliza y qué es nuevo —que es lo que hace la tabla de §6—.

No se implementa la composición entera de golpe. El motivo es el de siempre: una capa de
escritorio a medias es un escritorio que no se puede usar, y esta máquina es la de trabajo de
alguien.

---

# E0 — Hecho: el gestor de superficies

## Qué se ha construido

`Superficies.cs`: un gestor **puro** —ni una ventana, ni un píxel— que decide qué superficies
hay abiertas, en qué orden se pintan, cuál se cierra con un clic fuera, quién reserva área de
trabajo y quién manda en el teclado.

Ocho clases: `Barra`, `Izquierda`, `Derecha`, `Dock`, `Notificaciones`, `Multimedia`,
`Overlay` y `Panel` (los clásicos de la isla).

### Las decisiones que no son obvias

**El orden de pintado sale de la clase, no del orden de apertura.** Quien abra antes no puede
decidir quién tapa a quién. Y las notificaciones van **por encima** de los paneles a propósito:
un aviso detrás del panel que tienes abierto es un aviso que no has visto.

**A igual Z desempata el orden de llegada, y eso es estable.** El panel izquierdo y el derecho
empatan; si el desempate dependiera del orden interno del diccionario, dos arranques podrían
pintar distinto y nadie sabría por qué.

**Actualizar no reordena.** Cambiar el tamaño de un panel conserva su sitio en la pila. Si
reordenara, mover un deslizador pondría el panel izquierdo encima del derecho y el usuario
vería saltar la composición por haber tocado un control.

**El clic fuera tiene tres respuestas, no dos.** «Se queda» para la barra y el dock, que son
permanentes. «Se cierra» para un panel que abriste a propósito. Y «se cierra si no la usas»
para una notificación: un clic en el escritorio la descarta, pero pulsar el dock no, porque
estás usando el escritorio, no diciéndole al aviso que se vaya.

**El modo clásico es por pantalla.** Con dos monitores, abrir Inicio en uno no puede cerrar el
panel del otro: son dos islas y puedes estar usando las dos.

**Solo la barra reserva espacio.** El dock flota por encima en vez de robar área de trabajo, y
eso es lo que deja el escritorio amplio. Dos superficies reservando en el mismo monitor
descontarían dos veces y dejarían un hueco muerto.

**Se desmonta de arriba abajo.** `CerrarTodo` devuelve los ids en orden inverso al de pintado:
cerrar la barra antes que el panel que vive encima deja un instante con el panel colgando de
nada, y eso en ventanas de verdad es un parpadeo.

## La compatibilidad, que era el requisito difícil

El tablero va **en paralelo** al `_panelAbierto` de siempre y en modo clásico. No manda sobre
nada todavía: se le cuenta lo que pasa.

Eso no es código muerto. Es la forma de que el tablero esté vivo, con datos de verdad y
mirable, **antes** de que nada dependa de él. Cuando la composición E se encienda, el tablero
pasa a mandar y `_panelAbierto` se queda siendo lo que siempre fue.

Y porque dos fuentes de verdad se separan sin que nadie se entere, hay un diagnóstico que lo
comprueba en vez de confiar: `--diage0` abre los paneles **por la misma puerta que el usuario**
—no llamando a `AbrirPanel` a pelo, que comprobaría otra cosa— y compara los dos estados tras
cada apertura y cada cierre.

## Lo medido

```
--diage0 en la barra real:
  audio          -> audio[Panel principal z=40 380x404]
  red            -> red[Panel principal z=40 420x200]
  bateria        -> bateria[Panel principal z=40 340x150]
  bandeja        -> bandeja[Panel principal z=40 260x140]
  notificaciones -> notificaciones[Panel principal z=40 420x138]
  vista          -> vista[Panel principal z=40 736x378 teclado]

  tres paneles encadenados sin cerrar: 1 panel en el tablero cada vez
  al terminar: 0 superficies
  el tablero y la isla dicen lo mismo en todos los casos
```

La vista general aparece con `teclado`, que es correcto: es el único de los seis que pide el
foco del teclado.

## Pruebas

210 comprobaciones nuevas (`Pruebas.E0.cs`), todas puras: abrir, cerrar, actualizar y ordenar
**de dos a siete** superficies simultáneas, el clic fuera, el modo clásico, dos monitores y la
limpieza al cerrar. Incluye que un tablero cerrado del todo **vuelve a servir** —al recargar el
tema se desmonta y se vuelve a montar; si `CerrarTodo` lo dejara inservible, cambiar de tema
apagaría el escritorio—.

## Lo que E0 NO hace

No dibuja nada. No hay todavía barra nueva, ni dock, ni paneles laterales: hay el motor que
los va a sostener y la garantía de que lo de hoy sigue funcionando igual. La composición E
sigue apagada.

---

# E1 — Hecho: la barra como composición

## La idea: una composición es un dato, no código

E1 no necesitaba módulos nuevos —están los quince de siempre— sino una **distribución**
concreta que dé la barra de la referencia. Escribir esa distribución dentro del código de la
barra la habría hecho imposible de probar y de cambiar.

`Composicion.cs` la convierte en un dato: qué módulos en cada zona, con qué grosor y en qué
borde. Así se puede comprobar que no repite, que no pide módulos que no existen, que cabe, y
que **aplicarla y deshacerla devuelve exactamente lo que había**.

## Dos composiciones, y la diferencia no es estética

| | Horizontal | Vertical |
|---|---|---|
| Borde | arriba | izquierda |
| Grosor | 38 px | **44 px** |
| Inicio | inicio, escritorios, fijadas, apps | inicio, escritorios, apps |
| Centro | reloj, fecha | — |
| Final | musica, cpu, ram, bandeja, notificaciones, bateria, nube, control | bandeja, notificaciones, bateria, nube, reloj, control |

La diferencia es **cuánto texto cabe**. Una columna de 44 px no admite «CPU 12%» ni una fecha
de forma legible: meterlos no da una barra vertical, da una columna de texto cortado. El reloj
sí va, porque sabe escribirse en dos líneas desde B6.

Y el grosor sube de 38 a 44 porque en vertical el grosor es el **ancho**, y 38 px deja los
iconos pegados a los dos filos.

**La composición sale del borde**, no de un ajuste aparte: así mover la barra a un lateral no
deja una fila de etiquetas cortadas. Con un matiz que ya existía y había que respetar: «isla»
en un lateral **no** es una barra vertical, es una isla anclada que sigue siendo horizontal.

## El módulo que faltaba: el estado de la nube

La barra enseñaba el estado de todo —red, audio, batería, notificaciones— menos el de la pieza
que organiza tus ventanas. Saber si NubeHypr está apagado, simulando o aplicando obligaba a
abrir Ajustes, y es justo el dato que conviene tener delante.

Es la nube de `Nubecita` —geometría, no una fuente: no se convierte en una caja el día que
falte un tipo de letra— y el estado se cuenta **con su color**, no con un punto al lado:

| Estado | Color | Por qué |
|---|---|---|
| Apagado | texto atenuado, opacidad 0,55 | estar apagada no es un aviso, es lo normal |
| Simulación | azul | «esto está funcionando» |
| Aplicando | coral | el único estado que de verdad toca tus ventanas |
| Modo seguro | coral | algo ha ido mal |

Clic abre Ajustes **directamente en NubeHypr**: un icono de estado que abre la primera página
obliga a buscar lo que el icono estaba contando.

## Dos fallos, y los dos los cazó mirar

### 1. Un módulo que no existe

La primera composición pedía un módulo `"red"`. **No existe**: el estado de la conexión vive
dentro del centro de control y en la bandeja. `Revisar` lo rechazó entero antes de aplicarlo,
que es exactamente para lo que existe.

Lo grave no es el error, es que **la prueba no lo cazó**: comprobaba contra una lista de ids
escrita a mano por mí. Una prueba que compara una suposición consigo misma no comprueba nada.
Ahora pregunta al registro de widgets, que es quien decide.

### 2. La composición se aplicaba al modelo y no a la pantalla

La captura de la composición horizontal enseñaba **la barra del usuario**. El registro decía
que se había aplicado.

La causa: `PanelPrincipal.Zonas` no copia las listas de `Modulos`, **las referencia**. Asignar
`m.Izquierda = otra lista` deja al panel apuntando a la lista vieja. Vaciando y rellenando la
misma lista, quien la referencie ve el cambio.

Hay prueba, y mira la **identidad** de la lista (`ReferenceEquals`), no su contenido: las
pruebas que había miraban el contenido y por eso pasaban todas.

### 3. Los escritorios se salían de la columna

Con la barra vertical, el indicador de escritorios ponía los números uno al lado de otro: con
dos escritorios, el «2» se dibujaba **fuera de la superficie de la barra**, sobre el
escritorio. La orientación estaba escrita a mano en horizontal.

No lo había cazado nadie porque esta configuración no lleva ese módulo, y la única captura
vertical anterior lo añadía estando en horizontal. Arreglado y vuelto a fotografiar: el «1» y
el «2» se apilan dentro de la columna.

## Medido

```
escritorio-horizontal [barra/arriba 38px]  EsIsla=False Vertical=False  isla=2544x38
escritorio-vertical   [barra/izquierda 44px] EsIsla=False Vertical=True  isla=44x1424
tu barra, devuelta: isla/arriba 38px · inicio | fijadas,apps,reloj | cpu,ram,bateria,…
```

Las dos se aplican **en memoria** y se devuelven en un `finally`: si el diagnóstico revienta,
la barra no se queda con una distribución que el usuario no ha pedido. El `config.json` no se
toca.

## Lo que queda para tus ojos

La composición vertical con la barra puesta de verdad: el resaltado al pasar el ratón, que los
iconos del carrusel no se aprieten con muchas ventanas, y si 44 px es el grosor que te gusta.

---

# E2 — Hecho: el dock

## Ventana propia, no un módulo más

El documento pide que el dock **se pueda esconder y mostrar sin reconstruir el resto del
escritorio**. Un módulo dentro de la barra comparte su ciclo de vida: cada cambio del dock
haría que `RemontarZonas` tirase los quince widgets y los volviera a construir.

Siendo ventana propia —una `VentanaSuperpuesta`, que ya era exactamente lo que hacía falta—
el dock se refresca solo. **Medido: 20 refrescos del dock, 0 remontajes de las zonas.**

## Lo que decide la lógica pura

`Dock.cs`: qué elementos, en qué orden, dónde van los separadores, cuál está activo y **en qué
rectángulo de la pantalla se coloca**. Que la colocación sea pura permite comprobar «monitor de
1024 px, cuarenta iconos: ¿cabe?» sin tener ese monitor.

- **Las fijadas primero, lo abierto después, con un separador en medio** — y el separador solo
  si hay las dos cosas: uno al principio o al final es una raya que no separa nada.
- **Una app fijada y además abierta sale UNA vez**, en su sitio de fijada y con su indicador.
  Dos iconos del mismo programa es el fallo clásico de las barras de tareas.
- **El dock va en el borde opuesto a la barra.** Dos superficies permanentes en el mismo borde
  se pisan y hay que decidir cuál cede.
- **No reserva área de trabajo**: flota por encima, que es lo que deja el escritorio amplio.
- **Tope de elementos.** Con cuarenta ventanas, un dock de cuarenta iconos ocupa la pantalla y
  deja de ser un dock. Las fijadas no se caen nunca: son las que tú elegiste.
- **Con «al acercarse», sigue visible mientras el ratón esté encima.** Si solo mirara la franja
  del borde, se escondería justo cuando vas a pulsar un icono.

El menú contextual también es puro: a una app cerrada no se le ofrece «Cerrar», a una fijada no
se le ofrece «Anclar», y «Cerrar» va la última y en coral porque es la destructiva.

**De serie el dock viene apagado** (`Barra.Dock = "nunca"`). Una actualización de NubeBar no
puede cambiarle a nadie la forma del escritorio sin que lo haya pedido.

## Tres fallos, y los tres se encontraron midiendo, no mirando

**1. Las fijadas vacías se colaban.** Filtraba por longitud **antes** de normalizar, así que
`"  "` pasaba y se convertía en una clave vacía: un elemento sin nombre y sin icono. Lo cazó la
prueba de entradas raras.

**2. El margen iba en los cuatro lados.** Cada botón ocupaba 50 px de grueso en una tarjeta de
44, y los iconos se recortaban. El hueco va solo en el eje largo.

**3. El indicador de «abierta» no se dibujaba.** `Boton` fija la altura al grosor de la
**barra** (`Barra.Alto - 8` = 30 px), que es correcto para un módulo de la barra e incorrecto
en el dock, cuya celda es de 44. El indicador quedaba fuera del botón.

Este último es el que importa como método: **en la captura no se veía la diferencia entre una
rayita recortada y una rayita que no existe**. Lo que lo resolvió fue imprimir el tamaño del
botón (`44x30`) y muestrear los píxeles de la franja, no mirar la imagen con más atención.

Verificado después midiendo otra vez: tres píxeles de alto en `y=51..53`, azul (B=247) bajo la
ventana de delante.

## Pendiente de tus manos

Encender el dock (`Barra.Dock` a `"siempre"` o `"alacercarse"`) y usarlo: el menú contextual
con el ratón, anclar y soltar, el modo «al acercarse» —que no se escape al ir a pulsar—, y con
la barra en un lateral, que el dock se ponga de pie en el borde opuesto.

---

# E3 — Hecho: el panel lateral izquierdo

## Lo que de verdad demuestra E3

No es el contenido: es que **dos superficies de contenido conviven**. Era el objetivo de E0 y
hasta aquí nada lo había ejercitado. Medido en la barra real:

```
izquierdo abierto  -> 1 superficie: izquierda[Izquierda principal z=30]
los dos abiertos   -> 2 superficies: izquierda · derecha
cerrar el izquierdo -> el derecho sigue abierto
abrir los dos      -> 0 remontajes de las zonas de la barra
los dos cerrados   -> sin superficies
```

## Un archivo para los dos lados

`MainWindow.Laterales.cs` es la superficie —ventana aparte, colocación, abrir y cerrar,
apuntarse en el tablero— y es **la misma para los dos lados**. Lo que cambia son las
secciones. Escribirlos por separado habría dado dos sitios donde arreglar el mismo fallo de
colocación, que es exactamente lo que pasó entre el aviso de volumen y el indicador de D6.

## El reparto: lo que tocas y lo que pasa

Izquierda lo que **controlas** —perfil y nube, acciones rápidas, volumen, brillo, carpetas, la
máquina, ajustes—. Derecha lo que **pasa**. Hay prueba de que ninguna sección está en los dos
lados: dos sitios para lo mismo es la forma de que un día digan cosas distintas.

## Las decisiones que no son obvias

**El ancho es fijo (340 px), no un porcentaje.** Un panel de controles que en un 4K midiera
900 px sería una columna con cuatro conmutadores perdidos en medio.

**Si no caben los dos, no se abren los dos.** En una pantalla estrecha, dos columnas de 340
no dejan escritorio en medio, y un panel que tapa lo que hay debajo es peor que uno que no se
abre. Se comprueba antes (`CabenLosDos`) y hay prueba de que lo que dice y lo que pasa
coinciden a todos los anchos.

**Los deslizadores usan la máquina de siempre** (`Slider` + `GestoDeslizador`), y al cerrar el
panel se les llama `Terminar`. Cerrar con un deslizador cogido dejaba el ratón capturado y el
siguiente clic iba a un control que ya no existe. Es un fallo que ya se arregló una vez en el
centro de control y no puede volver por la puerta de atrás.

**El brillo se escribe al soltar, no al arrastrar.** Cada escritura por DDC/CI son 50 ms
largos; mandarlas sesenta veces por segundo deja el deslizador a trompicones. Y los handles
del driver se devuelven al cerrar: no hacerlo los fuga, y ya pasó una vez.

**Una sección sin datos no se pinta.** Sin dispositivo de audio no hay volumen; con DDC/CI
apagado de fábrica —lo normal en DisplayPort— no hay brillo. Mejor que un deslizador que no
hace nada.

**Una sección que revienta no se lleva el panel.** Se dice en su sitio, en coral, y las demás
siguen.

**Dos conmutadores abren Ajustes en vez de fingir.** «Modo nocturno» y «Tema claro» todavía no
tienen una función detrás; abren donde sí se cambian. Un conmutador que no conmuta se pulsa y
se cree que ha funcionado.

## El fallo que encontró la captura

Los accesos a carpetas salían **centrados**, cada uno en una posición distinta según su
longitud. Una lista así no se lee, se descifra.

`EstiloBoton` centra su contenido —correcto para un icono de la barra— y
`HorizontalContentAlignment` no sirve porque la plantilla no lo mira. Lo que funciona es darle
al contenido el ancho de la fila, para que no quede hueco que centrar.

## Pendiente de tus manos

Abrir el panel y usarlo: arrastrar los deslizadores y comprobar que no se quedan agarrados al
soltar fuera, que el volumen responde al momento y el brillo al soltar, y que cerrar el panel
con un deslizador cogido no deja el ratón capturado. Y los dos paneles abiertos a la vez en
una pantalla estrecha.

---

# E4 — Hecho: el panel lateral derecho

## Lo que lleva, y todo con datos de verdad

Reproduciendo, notificaciones, calendario, este escritorio, conexión y batería. Medido en la
máquina, no inventado: el título de Prime Video que sonaba, un aviso real de Defender,
«Escritorio 1 de 3, el actual, 5 ventanas», «Cable».

Reutiliza la superficie de E3 entera: lo único nuevo es el contenido y el calendario.

## La música no se para al abrirlo

Es lo que E4 prohíbe explícitamente, y se comprueba en vez de prometerlo: se apunta lo que
suena **antes** de abrir y se compara después.

```
[E4] la música sigue sonando tras abrir el panel: «Prime Video: El Mentalista…»
```

El centro multimedia **lee** la sesión de Windows; no la manda. Y si no hubiera nada sonando,
el diagnóstico lo dice y deja la comprobación pendiente en vez de darla por buena.

## El calendario, que es la pieza nueva

Un calendario es aritmética con trampas: meses de 28 a 31 días, bisiestos, y —la que más se
falla— **qué día empieza la semana**. En España el lunes, en Estados Unidos el domingo; un
calendario que empiece el día equivocado coloca mal las cinco semanas enteras y no se nota
hasta que buscas una fecha.

Siendo puro se comprueba sin esperar a esas fechas:

- Febrero de 2024 (29) contra el de 2025 (28). **2000 bisiesto y 1900 no**, que es la regla de
  los siglos y la que más se falla.
- **132 meses × 2 culturas**: en todos, la rejilla contiene exactamente los días del mes, sin
  perder ni repetir ninguno, y siempre en semanas completas.
- Las semanas son **las que hagan falta**, 4, 5 o 6. Seis fijas dejan una fila vacía casi
  siempre y descolocan el panel al cambiar de mes; cinco se comen días en un mes de 31 que
  empieza el último día de la semana.
- Las cabeceras siguen el mismo criterio que la rejilla, o el calendario diría una cosa y
  pintaría otra.
- Hoy se marca **una sola vez**, y mirando otro mes no se marca nada.

## El clima: no está, y es a propósito

El documento pedía clima «si existe proveedor real». **No existe**: NubeBar no habla con
ningún servicio meteorológico. Una tarjeta con un sol dibujado sería una maqueta, que es lo que
este documento prohíbe en su propia sección 8. Hay una prueba que lo fija: si algún día hay
proveedor, esa prueba es la que habrá que cambiar.

Por lo mismo no hay un reloj aparte: ya está en la barra y en el calendario, y dos sitios
enseñando la hora en la misma pantalla es ruido.

## Un detalle de saturación

La primera versión ponía «5 ventanas» en una línea y «5 ventanas en total» justo debajo. Dos
veces el mismo número no informa de nada. Ahora la segunda línea cuenta **lo que la primera no
dice**: lo que hay en los demás escritorios.

## Pendiente de tus manos

Los botones de transporte con el ratón —que pulsar «siguiente» refresque solo el panel derecho
y no toque el izquierdo— y el calendario a primeros y últimos de mes, donde se ven los días de
relleno.

---

# E5 — Hecho (la parte que se podía hacer): el área con las superficies de E

## Lo que E5 necesitaba de verdad

Casi todo lo que pide E5 —tiling, flotantes, foco por teclado, previsualización, reglas por
proceso/título/clase/ruta, escritorios virtuales, restauración segura— **ya existe** desde
D0–D9.4. Lo que no existía es la consecuencia de E: ahora hay más superficies, y el sitio
donde se reparten las ventanas tiene que descontarlas.

Sin eso, con el dock encendido, una ventana acaba **permanentemente** debajo de él. No tapada
a ratos: tapada siempre, porque el dock es permanente.

## Qué se descuenta y qué no

| | ¿Descuenta? | Por qué |
|---|---|---|
| Barra (si reserva) | sí | es permanente y Windows ya la conoce como appbar |
| Dock | **sí** | es permanente y Windows **no** lo conoce: flota |
| Paneles laterales | **no** | se abren y se cierran |

Lo de los paneles es una decisión, no un olvido, y está fijada por una prueba. **Si
descontaran, abrir el panel izquierdo recolocaría todas las ventanas del escritorio y cerrarlo
las recolocaría otra vez: mirar el volumen te movería la pantalla entera.** Un panel que se
abre y se cierra pasa por delante; no cambia la forma del escritorio.

Es la misma distinción que hace el tablero de E0 entre «reserva espacio» y «flota».

## Por borde, el más grueso — no la suma

Sumar dos estorbos del mismo borde descontaría dos veces lo que dos superficies solapadas
ocupan una sola vez, y el área saldría más pequeña de lo que es.

## La previsualización y el ajuste, por la misma cuenta

El área de trabajo de Windows conoce la barra pero **no el dock**. Ajustar una ventana a la
mitad inferior la dejaba con su borde debajo de él.

Ahora `ZonasAjuste.AreaUtilCon` descuenta los estorbos, y lo usan **la previsualización y el
ajuste de verdad**. Si uno calculara con el área de Windows y el otro con el dock descontado,
la ventana acabaría en un sitio distinto del que se le prometió al usuario — y eso es
exactamente el tipo de fallo que no se ve hasta que molesta.

## Medido en la barra real

```
[E5] sin dock: área=12,12,2536,1416 · estorbos: ninguno (la barra flota y no hay dock)
[E5] con dock: área=12,12,2536,1342 · estorbos: Abajo 74px
```

Los 74 px exactos que ocupa el dock. Y el mensaje «la barra flota y no hay dock» es correcto y
honesto: tu barra no reserva espacio, así que las ventanas pasan por debajo de ella a
propósito, como siempre.

## Lo que E5 NO hace, y por qué

**Las reglas siguen sin mover tus ventanas.** El modo sigue siendo simulación, que es el valor
seguro que pediste, y conectar el movimiento automático necesita tu permiso explícito. Todo lo
demás de E5 —el área, la previsualización, el ajuste, el foco, las reglas, los escritorios—
está integrado y funcionando con las superficies nuevas.

## Pendiente de tus manos

Con el dock encendido: arrastrar una ventana al borde de abajo y comprobar que la
previsualización enseña el hueco **por encima** del dock, y que al soltar la ventana acaba
justo ahí. Y lo mismo con la barra en un lateral y el dock en el opuesto.

---

# E6 — Hecho: la nube y las transiciones

## La regla que gobierna todo esto

**Una animación representa un cambio de estado real.** No decora, no entretiene y no se lanza
«porque queda bien». Si el estado no ha cambiado, no hay transición; si ha cambiado, dura lo
que tarda en entenderse y ni un milisegundo más.

Es la regla de D6 (`HyprPinta.TocaAnimar`) subida un nivel: allí era «no relanzar un fundido
con el mismo origen y destino»; aquí es **qué cambios merecen transición y cuánto dura cada
uno**.

## Un catálogo, no números sueltos

`Transiciones.cs` es puro y lleva las ocho: cambio de escritorio, panel que entra, panel que
sale, cambio de foco, previsualización, recuperación, arranque y cierre. Cada una con su
duración, si es cancelable, si lleva nube y **por qué**.

Lo importante de que sea un catálogo: el dock y el panel lateral tenían cada uno su número
escrito a mano (150 y 160 ms). Dos sitios con dos números es como se acaba con un panel que
entra más despacio que el dock sin que nadie lo haya decidido. Ahora los dos piden
`Transiciones.Duracion(PanelEntra)`.

## Lo que las pruebas fijan

«Breve y cancelable» es una promesa, y en una captura **una animación de 900 ms y una de 180
se parecen**. Por eso se comprueba sin mirar:

- Ninguna pasa de **320 ms**. Ese número es la diferencia entre «elegante» y «lento».
- **Salir es más rápido que entrar**: lo que se va ya no tienes que mirarlo.
- La previsualización es de las más rápidas: llega mientras arrastras, y una entrada lenta la
  enseña cuando ya has soltado.
- **Solo una no es cancelable**, la de cerrar la composición — cortarla dejaría superficies a
  medio desmontar en la pantalla. Que siga siendo una sola está fijado por una prueba.
- Con las animaciones **desactivadas duran cero**, no «rápido». Es la diferencia entre respetar
  la preferencia y hacer como que se respeta.
- Cada transición explica en su propio registro por qué existe y por qué dura eso.

## La nube no sale en todas

Sale donde el cambio es **de NubeBar o de su estado**: cambiar de escritorio, abrir una
superficie, recuperarse de un error, arrancar. No sale al cerrar ni al cambiar el foco de una
ventana a otra, que son cosas tuyas y del sistema.

Si saliera en todas aparecería ocho veces por minuto y dejaría de significar nada: sería una
mascota, que es lo que el documento de E prohíbe. Y el tope de tamaño de D6 **no se levanta**:
con una pantalla 4K disponible, la nube de una transición sigue siendo pequeña. Hay prueba.

## Lo que se ha conectado

Dos transiciones que el documento pedía y no existían:

- **Cambio de escritorio.** Un destello de nube sobre el indicador de escritorios —no en medio
  de la pantalla, que taparía justo lo que acabas de ir a ver—. Salta después de la huella, así
  que solo cuando lo dibujado ha cambiado de verdad.
- **Recuperación.** Salir del modo seguro solo se apuntaba en el registro, y un registro no lo
  mira nadie mientras trabaja. Ahora hay un destello. **Solo al salir**: un destello al entrar
  sería celebrar un problema, y entrar ya se cuenta con el color coral de la nube de la barra.

El destello se monta en `Raiz` y no dentro del módulo: dentro cambiaría el tamaño del módulo a
mitad de animación y movería los de al lado. Y se reaprovecha la misma forma, porque crear una
por cambio dejaría una docena de formas muertas en el árbol visual en cinco minutos.

**Cancelable de verdad**: si llega otro cambio antes de terminar, el anterior se corta. Dos
destellos encadenados sobre el mismo sitio se ven como un parpadeo, no como dos cambios.

## Pendiente de tus manos

Esto es lo que menos se puede verificar sin ojos: **una captura no enseña una animación**.
Cambiar de escritorio y ver el destello, comprobar que cambiar dos veces seguidas no parpadea,
y que con las animaciones en «desactivado» no se mueve nada en absoluto.

---

# E7 — Hecho: lo que faltaba de reglas, perfiles y persistencia

## Lo que ya estaba, dicho con honestidad

E7 pedía nueve cosas y **siete ya existían** desde D5–D9.3. Conviene decirlo en vez de
reescribirlas para que la fase parezca más grande:

| Lo que pedía E7 | Dónde está |
|---|---|
| Reglas al aparecer una ventana | `HyprVigilante` (D5.1), en simulación |
| Editor visual de reglas | D9.3 + la guía |
| Editor de atajos | D5 |
| Detección de conflictos | D5.1 |
| Explicación de coincidencias y descartes | `HyprCatalogo.Explicar` (D6) |
| Migraciones seguras + rollback | `Esquema` + los respaldos `antes-de-vN` |
| Conservación de claves desconocidas | el árbol JSON, desde el principio |
| **Perfiles con reglas y atajos** | D5.1 — y con una garantía que conviene repetir |
| **Configuración por monitor, escritorio y orientación** | **lo nuevo de E7** |

### La garantía de los perfiles, repetida porque importa

Un perfil puede traer `NubeHypr.Reglas` y `NubeHypr.Atajos`. **No puede traer `Activo` ni
`Modo`.** Si pudiera, aplicar un perfil te empezaría a mover ventanas sin haberlo pedido.
Encender la capa es una decisión aparte y se queda aparte — y ahora hay prueba que lo fija, por
si alguien añade claves a esa lista sin pensarlo.

## Lo nuevo: configuración por contexto

«El dock solo en el monitor grande.» «En el escritorio de juego, la barra sin módulos de
sistema.» «En vertical, el panel más estrecho.» Son tres preguntas con la misma forma: **el
mismo ajuste vale una cosa u otra según dónde estés**.

`PorContexto.cs` las resuelve con un mecanismo, no con tres. Un ajuste lleva filtros opcionales
—monitor, escritorio, orientación— y **lo que se deja en blanco no se mira**, igual que en las
reglas de ventanas.

### El desempate es el MISMO que el de las reglas

1. La **prioridad** más alta.
2. A igual prioridad, el más **concreto** (el que mira más cosas).
3. Y si siguen empatados, el primero de la lista.

Usar un criterio distinto aquí sería pedirle al usuario que aprenda dos sistemas de prioridad
en el mismo programa. Hay prueba de los tres niveles, y de que el orden es **estable**: si
dependiera de algo interno, dos arranques elegirían distinto y nadie sabría por qué.

Y una regla que parece obvia hasta que falla: **la prioridad no sirve de nada si el ajuste no
encaja**. Un ajuste del monitor 5 con prioridad 100 pierde contra el de serie cuando estás en
el monitor 0.

### Por qué ha ganado ese

La misma idea que la explicación de las reglas: delante de un ajuste que no hace lo que
esperabas, lo que hace falta no es la lista, es **cuál ganó y por qué se descartaron los
demás**. Y de los que no encajan se dice el motivo concreto: «pide el monitor 1 y estás en el
0».

### Un monitor desconectado no es un error

Un ajuste para el monitor del trabajo avisa cuando no está, pero **no se rechaza ni se borra**:
borrarlo al desconectarlo sería perder tu configuración por llevarte el portátil a casa. El
aviso dice literalmente que volverá a valer al reconectarlo.

Lo que sí es grave: un monitor negativo, o el escritorio 0 —se cuentan desde 1, y ese es el
error de cálculo más fácil porque los monitores sí empiezan en 0—.

## Pendiente

`PorContexto` es el mecanismo y está probado; **falta conectarlo a ajustes concretos** (qué
ajustes admiten contexto y su interfaz en Ajustes). Eso es trabajo de interfaz y va después de
E8, no antes: meter un selector de contexto en cada ajuste antes de estabilizar sería añadir
superficie justo cuando toca consolidarla.

---

# E8 — Estabilización final

## La regresión completa

| | Resultado |
|---|---|
| Pruebas puras | **5874 comprobaciones, 0 fallos** |
| Los once `--diag*` (incluido `--diage0`) | **0 fallos reales** |
| `--arnesaplicar` | vuelve a `120,120,420,300` exacto · `limpieza=COMPLETA` |
| `--arnesespacios` | tus escritorios, los mismos y en el mismo orden · `limpieza=COMPLETA` |
| `--capturas` | **55 imágenes**, todas con contenido |
| Instalación / actualización / desinstalación | en carpeta temporal, limpias |
| Arranque y cierre, 3 ciclos | **0 procesos** al terminar cada uno |
| Ventanas huérfanas | ninguna |
| `config.json` | byte a byte idéntico tras las ~20 ejecuciones |

## Dos falsas alarmas, y las dos eran mías

### 1. Una captura «casi vacía»

El repaso automático marcó `16-isla-izquierda.png` con 0,9 % de contenido. No era un defecto:
mi rejilla de muestreo era gruesa. Medido bien, las cuatro islas tienen contenido equivalente
**y en el sitio correcto**:

| captura | contenido | x de … a |
|---|---|---|
| isla arriba / abajo | 1,6 / 2,4 % | centrada (1122–1438) |
| isla izquierda | 1,5 % | pegada al filo (26–292) |
| isla derecha | 1,6 % | pegada al otro (2256–2534) |
| barra arriba | 2,8 % | de lado a lado (28–2536) |

Eso verifica los cuatro bordes **midiendo**, no mirando.

### 2. Una prueba que medía donde no era

La prueba de los siete temas falló en cuatro: «la activa se distingue de una abierta
(1,09:1)». Parecía un problema de contraste del dock.

No lo era: **la prueba comparaba los dos colores crudos ignorando la opacidad que el dock sí
aplica**. Lo que el ojo ve no son los dos colores, son los dos colores ya mezclados con el
fondo. Una prueba así da un resultado que no se corresponde con nada de lo que pasa en
pantalla.

El arreglo no fue bajar el umbral —eso habría sido acallar la prueba— sino **mover la decisión
a la capa pura**. `Dock.MarcaDe` devuelve color, opacidad y longitud, y `Dock.SeDistinguen`
exige que al menos **dos de los tres canales** difieran:

> Dos colores pueden tener casi la misma luminancia y distinguirse por el tono —azul contra
> gris lo hacen— pero apoyar la diferencia SOLO en el tono deja fuera a quien no distingue
> bien los colores.

Y el dibujo del dock pasó a pedirle la decisión a esa función, en vez de tomarla otra vez por
su cuenta: dos sitios decidiendo lo mismo es como acaban diciendo cosas distintas.

## Accesibilidad: un hueco real encontrado

Los botones de transporte del panel derecho tienen por contenido un **glifo**. Un lector de
pantalla leería el carácter de la zona de uso privado, que no significa nada — y el tooltip no
vale como nombre accesible, porque UIA no lo usa. Arreglado con `AutomationProperties.SetName`,
y lo mismo para los dos deslizadores, que sin nombre se anuncian como «control».

## Lo transversal, fijado por pruebas

- **La nube nunca es una mascota**: con 2160 px de hueco disponible sigue siendo pequeña.
- **Solo la barra reserva espacio**; las siete clases restantes flotan. Dos reservas en el
  mismo monitor descontarían dos veces del área de trabajo.
- **Ninguna transición pasa de 320 ms.**
- **La composición E viene apagada de serie** (`Barra.Dock = "nunca"`), y NubeHypr sigue
  apagado y en simulación. Una actualización no puede cambiarle a nadie la forma del
  escritorio sin que lo haya pedido.

## Lo que E8 ha encontrado y NO ha tocado

### Seis paneles sin superficie opaca

El propio diagnóstico de NubeBar lo dice en cada apertura. Contado sobre el registro:

```
41 × "shell opaco: sí"
27 × "shell opaco: NO (Grid)"
26 × "shell opaco: NO (StackPanel)"
21 × "shell opaco: NO (UniformGrid)"
```

Emparejado con el panel que se abría: **bandeja, vista, batería, aviso, notificaciones y
contextual** no envuelven su contenido en `PanelShell`. Audio, Inicio y Red sí.

Es **anterior a E** —viene de C5.9, que es cuando se añadió esa traza precisamente para
cazarlo— y significa que esos seis se apoyan en el fondo de la isla, que es translúcido al 90 %
a propósito. No es necesariamente un defecto: una bandeja de iconos sobre un fondo al 90 % se
lee bien. Pero la regla que C5.9 fijó dice lo contrario, y una regla que seis de nueve paneles
no cumplen es una regla a medias.

**No se ha tocado**, y el motivo es de método: cambiar el fondo de seis paneles es una
modificación visible de la barra que el usuario usa a diario, y E8 es la fase de estabilizar,
no de rediseñar. Queda anotado para decidirlo con los ojos delante.

### La vigilia limpia se quedó en 2,1 minutos

Y no por un fallo: el registro dice que a los 2,1 minutos se abrió el menú contextual y un
segundo después la barra se cerró **limpiamente** —inventario LIMPIO, ganchos liberados,
bandeja devuelta a Explorer, barra de Windows visible—. Es decir, **la cerró el usuario**, que
es lo razonable cuando aparece una barra que no has arrancado.

Así que el dato de estabilidad prolongada de esta tanda es **el de la vigilia anterior**, con
su matiz: plana en ~200 MB hasta el minuto 13, un pico a 333 MB al abrirse el menú Inicio, y
vuelta a 280. Eso es trabajo, no fuga — y ahora el veredicto sabe distinguirlos.

~~**Queda pendiente una vigilia larga en reposo de verdad.**~~ **Hecha**, con la máquina
libre: 15 minutos, memoria **plana** —213 a 217 MB, sin tendencia—, CPU 0,7 % de un núcleo y
**0 remontajes de zonas**, que es lo que tiene que pasar con la barra quieta. Los manejadores
suben de 997 a 1047 y **bajan a 984** tras un GC de generación 2: eso es el recolector
soltándolos, no una fuga.

Lo que esa medida sigue sin decir: cómo se porta con cien ventanas abiertas y la CPU a tope.
Para eso hay que correrla mientras se trabaja, y ahí la decisión es del usuario.
