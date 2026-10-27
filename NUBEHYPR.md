# NubeHypr — D0 a D9.4 (+ D6.1, D6.2, D6.3)

Fase D0 del plan maestro. **No organiza ninguna ventana.** Lo que deja escrito es el
punto de partida, el interruptor (apagado), lo que no se puede tocar y las trazas con las
que se diagnosticará el motor cuando exista.

---

## 1. Mapa: superficies y puntos de montaje

La isla es una ventana por monitor (`MainWindow`, una por pantalla; la del monitor
principal es *la jefa* y lleva lo que debe existir una sola vez). Los paneles no son
ventanas nuevas en horizontal: la isla crece de alto y el panel se despliega en su cajón,
recortado como una tarjeta colgada de su botón. En vertical sí hay una ventana aparte
(`_superpuesta`), porque una columna no tiene hacia dónde crecer.

| Superficie | Clave | Se abre desde | Teclado | Scroll propio |
|---|---|---|---|---|
| Inicio (buscador + portada del centro de control) | `inicio` | botón Inicio / tecla Windows | sí | sí |
| Audio | `audio` | altavoz de la isla | no | no |
| Red | `red` | icono de red | no | no |
| Batería | `bateria` | icono de batería | no | no |
| Vista de ventanas | `vista` | cuadrícula / `Ctrl+Alt+W` | sí | sí |
| Notificaciones | `notificaciones` | campana | no | no |
| Bandeja (iconos ocultos) | `bandeja` | `^` de la bandeja | no | no |
| Mini lista de ventanas | `contextual` | icono del carrusel | no | no |
| Biblioteca de widgets | `biblioteca` | modo edición | no | sí |

Módulos registrados en la isla (15): `reloj fecha inicio apps fijadas escritorios cpu ram
bateria bandeja control notificaciones musica wallpaper iconos separador`, repartidos en
tres zonas (izquierda, centro, derecha) y colocables desde el modo edición.

**El snapshot de ventanas ya existe y es el que D1/D2 deben consumir:**
`GestorVentanas` mantiene `Todas` (lista de `VentanaReal` con `Hwnd`, `Titulo`, `Exe`,
`App`, `Pid`, `Monitor`, `Rect` en píxeles de pantalla, `Estado`, `Activa`, `Visible`,
`SePuedeActivar`, `UltimaVez`), avisa con `event Cambio(Cambios, Motivo)` y lleva
contadores (`VecesRefrescado`, `VecesAvisado`, `VecesSinCambio`, `EventosNuestros`,
`EventosDeSitio`). Funciona por eventos de Windows, sin sondeo. **No hay que escribir otro
enumerador.**

## 2. Matriz de modos

Todo lo que sigue ya funciona y D0 no lo cambia.

| Eje | Valores |
|---|---|
| Modo | `isla` (flotante, con márgenes) · `barra` (de punta a punta) |
| Posición | `arriba` · `abajo` · `izquierda` · `derecha` |
| Orientación | horizontal (arriba/abajo, y los laterales en modo isla) · **vertical** (modo barra + lateral) |
| Monitores | una isla por pantalla; la del principal es la jefa |
| DPI | coordenadas en píxeles de pantalla; conversión explícita a unidades de WPF |
| Visibilidad | siempre · auto-ocultar · esconder a mano |

Medido en este equipo con `--diaginicio`, con la superficie de Inicio abierta:

| Posición | Ventana | Raíz visual | PanelShell |
|---|---|---|---|
| arriba | 2560×947 | `Border` | 660×893, raíz, opaco |
| abajo | 2560×947 | `Border` | 660×893, raíz, opaco |
| barra izquierda (vertical) | 54×1440 | `Border` | 660×893, raíz, opaco |
| barra derecha (vertical) | 54×1440 | `Border` | 660×893, raíz, opaco |

## 3. El interruptor

Sección `NubeHypr` del `config.json`, declarada por la migración **v3 → v4** y **apagada**:

```json
"NubeHypr": {
  "Activo": false, "Modo": "simulacion", "Gap": 8, "Margen": 12,
  "Layout": "principal", "EspacioInicial": "actual",
  "ConfirmarMovimientos": true, "MostrarPrevisualizacion": true,
  "Atajos": {}, "Reglas": [], "Espacios": []
}
```

Tres reglas de lectura, todas comprobadas:

1. **`Activo: false` manda sobre todo lo demás.** Una configuración vieja, editada a mano
   o traída de otro equipo no puede encender esta capa, y menos ponerla a mover ventanas.
2. **Un modo desconocido cae en simulación, nunca en aplicar.** Si el fichero dice algo
   que no entendemos, lo que toca es calcular y apuntar.
3. **Ninguna migración la enciende.** `Esquema.ActivoAntes` permite afirmarlo y no
   suponerlo; y si el usuario la tenía encendida, no se la apagamos: era su decisión.

`null` significa *no elegido*, no un valor mágico escondido.

### Y una cuarta regla, que faltaba: que se pueda tocar

Durante toda la fase D, **ninguna de estas claves tenía control en Ajustes salvo `Activo`**.
`Modo` —la que decide si la capa mueve ventanas o solo lo apunta, con 175 usos en el
producto— solo se podía cambiar editando este fichero a mano, lo que dejaba D9.5, D9.6 y
todo lo que mueve ventanas **inalcanzable desde la interfaz**.

Ahora la página tiene los que hacían falta, en este orden y leyéndose como una escalera:

| Interruptor | Clave | Qué decide |
|---|---|---|
| Activar NubeHypr | `Activo` | si la capa existe |
| Mover ventanas de verdad | `Modo` | `aplicar` o `simulacion` |
| Preguntar antes de mover ventanas | `ConfirmarMovimientos` | si lo automático se confirma |
| Hueco entre ventanas | `Gap` | los píxeles entre ventana y ventana |

Los dos de en medio salen **atenuados** con la capa apagada, y también en modo de
demostración —donde `DiagGuia.Apagar` clava el modo en simulación aunque el fichero diga
«aplicar»—, con el motivo en la ayuda emergente y en el nombre de UIA.

**Tres que ya no se escriben:** `EspacioInicial`, `MostrarPrevisualizacion` y `Espacios`
estaban en el fichero y no las consultaba nadie. Escribir una clave que el programa ignora no
es inofensivo —quien abre el fichero entiende que significa algo, y `ConfirmarMovimientos` era
exactamente eso—, así que se dejan de prometer en vez de implementarlas inventándose qué
deberían hacer. **Lo ya escrito no se toca**: solo se deja de añadirlas donde no estén, y
`ConfigArbol` conserva las que no conoce.

Al comprobar la última, cuidado: `HyprLayout` lee un `Espacios` que **no es ése** —es
`EstadoNubeHypr.Espacios`, el del estado en marcha—, así que un `grep` del nombre a secas dice
que se usa. Lo que sale a cero es `_cfg.NubeHypr.Espacios`.

### D9.7 — preguntar antes de mover

`HyprConfirmar.cs` (puro: cuándo se pregunta, qué caduca, cómo se cuenta) y
`MainWindow.ConfirmarHypr.cs` (la pregunta en pantalla). La puerta está en
`AplicarConRedes`, después del filtro y **antes** del apunte para deshacer: si se apuntara
antes, una pregunta sin contestar dejaría en la pila un lote que nunca se aplicó, y
«deshacer» devolvería ventanas que nadie movió.

- `HaceFalta(modo, opcion, automatico, yaConcedida)`: las cuatro condiciones.
- **`automatico` es el diseño, no un matiz.** Un atajo recién pulsado no pregunta: pulsarlo
  ya es la respuesta, y preguntar ahí haría que el usuario apagara la opción.
- **No contestar es no mover.** Un fallo aquí solo puede mover de menos.
- Caducidad de 30 s, y al contestar se revisan tres cosas: que la pregunta siga en pie, que
  no haya caducado y que la capa siga en «aplicar».
- Probado de los **dos** lados contra Windows en `--arnesaplicar`: con la opción puesta la
  ventana no se mueve y el contador de movimientos reales no se toca; al aceptar, se mueve a
  donde se pedía. Probar solo una mitad daría verde a un gate que bloquea siempre.

### D9.6 — probado contra Windows, y arreglado

`--arneslayout` crea un escritorio virtual, lanza tres ventanas propias dentro, pone una regla
con layout «Horizontal», mide dónde acaban y lo recoge todo. La prueba que «no se podía hacer
sin recolocar las ventanas del usuario» sí se podía: en un escritorio recién creado no hay
ninguna. Mismo patrón y mismas reglas que `--arnesespacios`, que lleva desde D4 haciéndolo.

Tres ventanas, no una ni dos: con una no se distingue repartir de maximizar, y con dos un
reparto correcto y uno equivocado dan rectángulos simétricos.

**Y a la primera dio rojo.** La causa, en `ColocarPorRegla`:

```csharp
if (porQueNo != null || !plan.HaceAlgo) return;   // ← aquí
```

`plan.HaceAlgo` mira `AlEspacio`, `Destino` y `Enfocar`: lo que la regla dice de **esa**
ventana. Una regla que pide solo un layout no dice nada de ella, así que volvía antes de
llegar a `RepartirPorRegla`. **D9.6 era inalcanzable por su caso principal** — «cuando aparezca
el navegador, enlosa esta pantalla» no dice nada del navegador. Ahora son dos condiciones
separadas, porque son dos decisiones distintas: una sobre la ventana y otra sobre el
escritorio.

El veredicto no es «se movieron tres». Tres ventanas pueden moverse las tres al mismo sitio y
el contador diría lo mismo. `ArnesLayout.MiraElReparto` exige la **forma**: ninguna fuera del
área útil, ninguna encima de otra, y que entre todas ocupen más de la mitad de la pantalla.
Medido: tres columnas de 848 px, 99 % del área.

## 4. Lo que no se toca

`GestorVentanas.EsDelUsuario` ya descarta lo que no es una ventana de usuario. Esta es
otra red y va además: hay ventanas que **sí** son del usuario y que aun así no se mueven.
`NubeHypr.SePuedeOrganizar` devuelve el motivo escrito, para que el log diga por qué se
descartó cada una.

| Motivo | Qué cubre |
|---|---|
| `es de NubeBar` | la isla, sus paneles, la ventana superpuesta, Ajustes |
| `es del sistema` | `explorer`, menú Inicio, búsqueda, `consent` (UAC), pantalla de bloqueo, `dwm`, teclado en pantalla, lupa |
| `pinta el fondo del escritorio` | `wallpaper32/64` — moverlo deja el escritorio negro |
| `va elevada y Windows no nos deja` | procesos como administrador o SYSTEM |
| `no tiene un rectángulo válido` | ventanas sin geometría utilizable |

Clases protegidas además: `Progman`, `WorkerW`, `Shell_TrayWnd`,
`Shell_SecondaryTrayWnd`, `MultitaskingViewFrame`, `TaskSwitcherWnd`,
`XamlExplorerHostIslandWindow`, `ForegroundStaging`, `Windows.UI.Core.CoreWindow`.

**Sin datos no se adivina:** muchas ventanas no dejan leer su ejecutable, y eso no las
convierte en protegidas — se juzgan por lo demás. Negarse a mover todo lo que no se puede
identificar dejaría la capa sin hacer nada en media sesión.

## 5. Las trazas

Formato de la especificación, al pie de la letra, para que un log se pueda leer con el
documento al lado. Un ciclo lleva un número (`NubeHypr.SiguienteVersion`) que ata las
cuatro líneas; `EsVieja` es la regla que impide que una notificación atrasada sobrescriba
un snapshot más nuevo — el mismo fallo que hizo parpadear la vista general, escrito esta
vez **antes** de que ocurra.

```
[D0]  nubehypr activo=no modo=apagado gap=8 margen=12 layout=principal reglas=0 atajos=0
[D7]  safe-mode=false adapter=stopped config-version=4
[D2]  snapshot=184 ventanas=12 foco=0x000A92
[D2]  policy=split-main target=monitor:1 rect=1280,0,1280,720
[D2]  apply hwnd=0x000B15 result=ok elapsed=3.4ms
[D2]  confirm version=184 moved=1 skipped=2 stale=0
[D3]  gesture=resize state=clean reason=button-released
```

`adapter=stopped` mientras no exista el motor (D2). Es la verdad; dejarlo en `ready` por
adelantado sería mentir en el log desde el primer día. El decimal va con punto y no con la
coma del sistema: con la coma española, un log sería ilegible para cualquier herramienta.

## 6. Un diagnóstico mira y no toca

`Config.SoloLectura` se pone con cualquier `--diag*` y entonces **no se escribe el
config**: ni la migración del esquema, ni los módulos de serie, ni el orden de las apps. Y
no se lanza el guardián, que es otro proceso, no ve la bandera y resucitaría la app a mitad
de una medida.

Esto no es precaución teórica. Pasó: ejecutar un binario de desarrollo migró el config real
a la v4, y la NubeBar instalada —v1.2.0, esquema v3— ya no sabía migrarlo y apuntaba un
error en cada arranque. Y no lo hizo la migración: lo hizo un guardado rutinario del
arranque, porque el modelo tipado lleva `Version = Esquema.Actual` y cualquier escritura
sube la versión del fichero.

## 7. Deudas registradas, no arregladas

D0 no arregla nada fuera de alcance. Lo que queda apuntado:

- **`NU1903`** — `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 tiene una vulnerabilidad conocida de
  gravedad alta; entra como dependencia transitiva de `Microsoft.Data.Sqlite`.
- **`WFO0003`** — el DPI alto se declara en `app.manifest` en vez de por propiedad del
  proyecto.
- Iconos de bandeja de apps que no se vuelven a registrar al recibir `TaskbarCreated`: no
  llegan, y no hay forma de traerlos.
- El desplegable de iconos ocultos enumera al abrirse, no mientras está abierto.
- **`HyprGesto` escrito, probado y sin instanciar.** Revisado a fondo y **cerrado como
  deuda que no se paga**: está escrito para una tecla MANTENIDA, y los atajos van por
  `RegisterHotKey`, donde `WM_HOTKEY` solo llega al pulsar — no existe el de soltar.
  Detectarlo pediría un gancho global de teclado, demasiado riesgo para esto en un
  reemplazo de shell. Y «cancelar de verdad» —devolver la ventana a donde estaba— ya existe:
  es el comando 20, `DeshacerLoUltimo`. Se queda como pieza que la realidad dejó sin sitio,
  con la nota en `MainWindow.Salud.cs` para que nadie la vuelva a recorrer entera.
- **Tres claves de `NubeHypr` que nadie lee** (`EspacioInicial`, `MostrarPrevisualizacion`,
  `Espacios`): ver la sección 3. No se implementan inventándose qué deberían hacer.

## 8. Lo que falta por fases

| Fase | Resultado | Estado |
|---|---|---|
| D0 | Diagnóstico y contrato de seguridad | **hecha** |
| D1 | Modelo puro de layouts y comandos | **hecha** |
| D2 | Motor real de posicionamiento | **hecha** |
| D3 | Foco, teclado y gestos | **hecha** — atajos probados a mano por el usuario |
| D4 | Escritorios virtuales | **hecha** — arnés aislado pasado |
| D5 | Reglas, perfiles y persistencia | **hecha** — editor validado a mano por el usuario |
| D5.1 | Reglas conectadas a la ventana nueva | **hecha** — solo simulación, no mueve nada |
| D6 | Integración visual | **hecha** — pendiente de prueba visual |
| D7 | Fiabilidad, recuperación y modo seguro | **hecha** — pendiente lo que necesita tus manos |
| D8 | Estabilización final y entrega | **hecha** — sin publicar, pendiente la tanda manual |
| D8.1 | El prefijo de los atajos, medido | **hecha** — 19/19 con `Win+Ctrl+Alt` |
| D9.0 | Arnés de aplicar (la puerta de D9) | **hecha** — camino confirmado al píxel |
| D8.2 | Pruebas visuales por captura | **hecha** — 2 fallos encontrados y corregidos |
| D9.1 | Respeto al gesto, ritmo y deshacer | **hecha** — probadas y en el arnés |
| D9.2 | La puerta única y el comando 20 | **hecha** — nadie mueve sin pasar por las redes |
| D9.3 | Editor de reglas en Ajustes | **hecha** — pendiente de usarlo con el ratón |
| D9.4 | Atajos por la puerta única | **hecha** |
| D9.5 | Una regla coloca la ventana nueva | **hecha** — probada contra Windows (`--arnesaplicar`) |
| D9.6 | Una regla fija el layout del escritorio | **hecha** — probada contra Windows (`--arneslayout`), y **estaba rota**: ver la sección de D9.6 |
| D9.7 | Preguntar antes de mover | **hecha** — probada de los dos lados contra Windows |

> Esta tabla decía «D9: sin empezar — hace falta tu permiso» mucho después de que D9.5 y D9.6
> estuvieran escritas y funcionando. Una tabla de estado que miente es peor que no tenerla:
> quien la lee cree que falta trabajo hecho, o que está hecho lo que falta.

---

# D1 — Modelo puro de layouts y comandos

Ni una llamada a Win32, ni una línea de interfaz. Recibe datos y devuelve decisiones.

## Tipos

`VentanaModelo` (hwnd, proceso, título, rectángulo **en píxeles de pantalla**, visible,
minimizada, maximizada, flotante, escritorio, monitor) · `EspacioTrabajo` · `ReglaVentana`
(definida; aplicarlas es D5) · `EstadoNubeHypr` (**inmutable**: los comandos devuelven un
estado nuevo) · `EfectoVentana` con cuatro tipos: `Colocar`, `Enfocar`, `AEspacio` y
`SeQueda` — este último con el motivo escrito.

Que las que no se mueven **salgan en la lista** con su motivo, en vez de desaparecer de
ella, es lo que permite que el log conteste a «¿por qué no se movió esa ventana?», que es
la pregunta que se hace siempre.

## Los cuatro layouts

| Layout | Reparto |
|---|---|
| `Flotante` | nadie se mueve: manda el usuario |
| `Horizontal` | columnas, de izquierda a derecha |
| `Vertical` | filas, de arriba abajo |
| `Principal` | una grande a un lado con su proporción (acotada a 0,2–0,8) y el resto apiladas al otro |

Con una sola ventana, los tres que colocan le dan el área entera. `Principal` se cae a
`Vertical` cuando no caben dos columnas del mínimo (120 px): un «principal» de 60 px no es
un layout.

## El área y el reparto

`AreaUtil(monitor, borde, reserva, margen)` descuenta la barra **solo por su borde** y
después el margen por los cuatro lados, en ese orden: medido contra la pantalla en vez de
contra el hueco, el lado de la barra acaba con el doble de aire y se nota. Un área que
saldría negativa se devuelve vacía, no «casi válida» — un rectángulo de ancho negativo
pasado a Windows es un cuelgue.

**El reparto no divide tamaños, reparte bordes.** `ancho / n` colocado en `i * trozo` falla
de dos formas a la vez en cuanto hay redondeo: deja rayas de un píxel entre ventanas y la
última se pasa del área. Repartiendo bordes, los bordes coinciden por construcción y el
último es exactamente el final del área.

Y antes de dar ventanas de 20 px, no se organizan las que no caben: `CuantasCaben` corta y
las sobrantes salen como `SeQueda` con el motivo.

## Los comandos

`(estado, comando) → (estado, efectos, porqué)`. Un comando que no se puede cumplir
devuelve **el mismo estado** y ningún efecto.

| Comando | Qué hace | En el límite |
|---|---|---|
| `EnfocarSiguiente/Anterior` | rota entre las visibles del espacio | da la vuelta; n vueltas te dejan donde empezaste |
| `EnfocarHacia(dirección)` | la más cercana **que esté de verdad hacia ese lado** | si no hay ninguna, no hace nada |
| `MoverEnElOrden(adelante)` | intercambia con la vecina | **no** da la vuelta: en los extremos se queda quieta |
| `Redimensionar(delta)` | mueve la frontera de `Principal` | acotada a 0,2–0,8 |
| `AlternarFlotante` | entra y sale del reparto | dos veces vuelve al estado original |
| `CambiarEspacio(n, llevar)` | cambia de espacio, con o sin la ventana | un espacio que no existe no hace nada |
| `PonerLayout(cual)` | cambia el layout | el que ya era no hace nada |

`MoverEnElOrden` no da la vuelta a propósito: mantener pulsado el atajo mandaría la ventana
de un extremo al otro sin parar, que es la deriva que la especificación pide evitar.

## Insertar y quitar

La regla es aburrida y es la que importa: **insertar añade al final y quitar no recoloca a
nadie**. Cualquier otra —por título, por proceso, por uso reciente— hace que abrir el
navegador te reorganice la pantalla. La misma ventana insertada dos veces no se duplica: se
actualiza *en su sitio*. Y si se cierra la que tenía el foco, el foco se suelta — dejarlo
apuntando a una ventana muerta es como se cuelan los `enfocar 0x0` en el log, y en D2 sería
una llamada a Win32 con un hwnd que ya no existe.

## Riesgo de migración

**Ninguno nuevo.** D1 no añade ni cambia una sola clave de configuración: el modelo vive en
memoria y se construye desde el snapshot. `Esquema.Actual` sigue en 4, con la sección
`NubeHypr` que declaró D0.

El único riesgo sigue siendo el de D0 y conviene repetirlo: un binario de D0/D1 arrancado
**en modo normal** migra el config a la v4, y una NubeBar más antigua (la v1.2.0 publicada,
esquema v3) no la migra hacia atrás — la lee y funciona, pero apunta un error en cada
arranque. Las herramientas (`--pruebas`, `--ventanas`, `--diag*`) no escriben nada.

---

# D2 — Motor real de posicionamiento

Observar primero, mover después, confirmar al final. Y **nunca** calcular con un monitor
para aplicar con otro implícito.

`HyprAdaptador` es lo único de NubeHypr que habla con Windows. Si ahí aparece una decisión
de layout, está en el sitio equivocado.

## Observar

El snapshot **no se reescribe**: es el de `GestorVentanas` —eventos de Windows, sin
sondeo, con sus contadores—. Un segundo enumerador serían dos verdades sobre las mismas
ventanas, y la que se quedara atrás sería la que se usara para mover.

Lo que el adaptador sí hace es el filtro: la lista protegida de D0 con los datos reales
(clase de ventana, si es nuestra, si Windows nos deja). Las descartadas salen con su
motivo, una línea por ventana.

`DeRect` es la **única** conversión `Nativo.RECT` → `NubeHypr.Rect`: el primero lleva los
cuatro bordes y el segundo origen y tamaño. Hacer esa resta a mano en cada sitio es como
se cuela un ancho que en realidad era un borde, y el síntoma es una ventana del doble de
ancha, no un error.

## Aplicar — el orden seguro

| | Paso | Si falla |
|---|---|---|
| 1 | destino válido (puro) | no se aplica nada |
| 2 | **si es simulación: apuntar y acabar** | — |
| 3 | el hwnd sigue existiendo | se descarta solo esa ventana |
| 4 | mover, midiendo | se registra y se continúa |
| 5 | confirmar con un snapshot nuevo | se apunta la divergencia |

**El orden de 2 y 3 importa, y lo descubrió una prueba.** Con la comprobación del hwnd por
delante, simular llamaba a Win32 y dejaba de ser algo que se puede razonar y comprobar en
frío — que es justo para lo que existe. Ahora simular es un cálculo puro.

Al mover **nunca** se activa la ventana ni se le cambia el orden Z (`SWP_NOACTIVATE |
SWP_NOZORDER`). Organizar que te robe el foco o te reordene las ventanas por detrás es lo
que convierte un gestor de ventanas en una molestia.

Un reparto de una versión vieja se descarta **entero**, no a medias: aplicar la mitad de un
reparto viejo deja la pantalla peor que no tocarla.

Y confirmar no es ceremonia: Windows puede no obedecer —una app con tamaño mínimo propio,
una que se recoloca sola— y sin comparar, el log diría «ok» de algo que no pasó. La
tolerancia es de 8 px a propósito: muchas ventanas tienen bordes invisibles de
redimensionado y su rectángulo real no es el que se ve.

## La previsualización

Reutiliza la ventana de C4 (`VentanaSuperpuesta` + `HacerseAtravesable`), que ya resuelve
las cuatro piezas: `WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`, `HTTRANSPARENT` ante
`WM_NCHITTEST` y `IsHitTestVisible = false` en el contenido. Una segunda ventana de
previsualización sería una segunda ocasión de olvidarse de una de esas cuatro. Lo único
nuevo es poder pedirla con un **rectángulo** en vez de con una zona de ajuste.

Medido: `previa: ¿tiene el foco?=no`.

## `--diagmotor [segundos] [hwnd]`

Sin hwnd: observa, calcula los cuatro layouts, enseña la previsualización y **no mueve
nada**. Con hwnd: aplica a **esa sola ventana**, calculando el reparto completo —para que
el destino sea el de verdad y no el de una ventana sola— y la devuelve a su sitio al
terminar. Nunca escribe la configuración.

Medido sobre una ventana de prueba propia:

```
[D2]  modo=aplicar (se aplicará SOLO a 0x5850AC4)
[D2]  previa en 12,12,1517,1416 (atravesable, sin foco)
[D2]  antes  0x5850AC4 en 300,300,500,400
[D2]  apply hwnd=0x5850AC4 result=ok elapsed=1.2ms
[D2]  después 0x5850AC4 en 12,12,1517,1416
[D2]  ¿se movió?=sí divergió=no
[D2]  devuelta a 300,300,500,400  ¿igual que al empezar?=sí
```

## Hallazgo: casi todo está maximizado

En simulación sobre un escritorio real, **5 de 6 ventanas se saltaron por estar maximizadas
o minimizadas**, y los cuatro layouts dieron el mismo resultado porque solo quedaba una
ventana que repartir.

No es un fallo del motor: es cómo usa la gente Windows. Pero significa que un organizador
que se niegue a tocar ventanas maximizadas no organizará nada. Desmaximizar antes de
enlosar es una acción **visible** y no entra en D2; es una decisión de D3/D6 y debe ser
una opción con su camino de vuelta.

## Riesgo de migración

**Ninguno nuevo.** D2 no añade ni cambia una sola clave; `Esquema.Actual` sigue en 4. Sigue
en pie el riesgo de D0: un binario de D0–D2 arrancado en modo normal migra el config a la
v4, y la v1.2.0 publicada (esquema v3) la lee y funciona pero apunta un error en cada
arranque. Las herramientas no escriben nada.

---

# D3 — Foco, teclado y gestos

## El mapa guarda un nombre, no una acción

Un atajo apunta a `foco.siguiente`, no a «llama a `SetForegroundWindow` con este hwnd». El
fichero se puede leer (`"Win+Alt+Right": "foco.derecha"`), lo que hace un comando puede
cambiar sin romper la configuración de nadie, y un nombre que no existe se **detecta y se
dice** en vez de quedarse como un atajo muerto que el usuario pulsa sin entender nada.

19 nombres: foco (siguiente, anterior, y las cuatro direcciones), mover en el orden,
tamaño, flotante, los cuatro layouts y los espacios (declarados; los aplica D4).

**Las combinaciones se normalizan** a `Win, Ctrl, Alt, Shift` + tecla. `ctrl+alt+w`,
`Alt+Ctrl+W` y `CTRL + ALT + W` son el mismo atajo; sin normalizar, el usuario tendría
tres atajos donde quería uno y ninguno detectado como conflicto.

Se exige un modificador: un atajo global de una sola tecla te la quita en **todo el
sistema**.

**Los conflictos se devuelven, no se lanzan ni se tragan.** Un atajo mal escrito no puede
tumbar el arranque ni quedarse muerto en silencio. Tres clases: combinación que Windows no
entiende, comando que no existe, y combinación repetida o ya cogida —contra el atajo de la
vista general, por ejemplo—. En una repetición se queda **el primero**, para que el orden
del fichero mande y sea predecible.

De serie, todo con `Win+Alt`: `Win+flechas` es el ajuste de Windows y `Win+Ctrl+flechas`
los escritorios virtuales. Pisarlos sería empezar con una pelea.

## Encontrado al probar: faltaban las flechas

`Nativo.TraducirAtajo` —el parseador que ya registra el atajo de la vista general— soportaba
`space`, `tab`, `enter`, `esc`, una letra y F1–F24. **Sin flechas.** Y el foco por dirección
se ata a las flechas porque es lo natural: el mapa de serie entero era inválido y el log
solo decía «no entiendo la combinación», sin decir qué trozo.

Se extendió **ahí**, no en un parseador nuevo: flechas, `+`/`-`, inicio, fin, página arriba
y abajo, suprimir y retroceso, cada una con su nombre en inglés y en español.

## Pasos de mover y redimensionar

Para las ventanas **flotantes**: en un layout enlosado no se mueve una ventana, se mueve su
sitio en el orden o la frontera. 32 px por pulsación —con 8 hay que machacar la tecla, con
100 no se coloca nada con precisión— y 5 % para la proporción.

Las dos funciones acotan al área. Una ventana que se va de la pantalla con el teclado es
imposible de recuperar sin el ratón, y es el tipo de fallo que obliga a reiniciar la sesión.
Cincuenta pasos seguidos la dejan pegada al borde y el 51 ya no la mueve: no hay deriva.

## El gesto reutiliza la máquina de los deslizadores

Mantener pulsado `Win+Alt+→` es un **gesto**: principio, repetición y final, y el final
puede llegar de varias formas. Es el mismo problema que el deslizador de volumen, y ese ya
costó una corrección entera. `HyprGesto` **envuelve** `GestoDeslizador` —un solo camino de
salida, idempotente, seis formas de terminar— y le añade lo único que le faltaba para
ventanas: **qué había antes**.

**Cancelar no es «parar de mover»: es devolver la ventana a donde estaba.** Un Esc que la
deja a medio camino es peor que no tener Esc, porque el usuario ya no sabe dónde la tenía.
Sin pasos dados no se coloca nada —pedirle a Windows que ponga una ventana donde ya está es
una llamada de más y un parpadeo posible—. Y el final lo decide el estado real de la tecla
una vez por fotograma, no un evento que puede perderse.

## El hallazgo de D2, resuelto con dos puertas

Maximizadas y minimizadas **se quedan fuera por defecto**. Sacar una ventana de maximizada
es visible y sorprendente: el usuario la dejó así a propósito. Para que ocurra hacen falta
**dos** decisiones suyas:

1. `RestaurarAlOrganizar`, que **nace apagada**. Apagada, el comportamiento es el de D2:
   fuera del reparto, con su motivo.
2. Y aun encendida, lo que se calcula es un **efecto** `Restaurar`, no una llamada. En
   simulación se apunta y no pasa nada; mover de verdad necesita el modo «aplicar».

**Y lo que se toca se puede devolver.** Antes de restaurar se guarda qué era *y dónde
estaba* —el rectángulo hace falta: una ventana maximizada tiene además un «sitio de cuando
no lo está»—. `Devolver` la deja como se la encontró y `DevolverTodo` es el deshacer de la
capa: lo que permite apagar NubeHypr y que el escritorio quede como estaba.

Dos reglas de la memoria que importan: se recuerda **una** vez (la primera, que es la del
usuario — recordar dos veces machacaría el original con el intermedio y «devolver» dejaría
la ventana donde la puso NubeHypr), y se olvida al devolver. Las flotantes no se restauran:
una flotante maximizada está así porque su dueño quiso.

## Riesgo de migración

`RestaurarAlOrganizar` es una clave **nueva y aditiva**, escrita como `false` por la
migración v3→v4. **No hace falta una v5**: un fichero que ya esté en v4 sin la clave la lee
como `false` por el valor por defecto del modelo, que es el seguro. Lo que no puede pasar es
que aparezca encendida, y por eso se escribe explícita.

`Esquema.Actual` sigue en 4. Sigue en pie el riesgo de D0: un binario de D0–D3 arrancado en
modo normal migra el config a la v4, y la v1.2.0 publicada la lee pero apunta un error en
cada arranque.

## D3: lo que falta — **no está usable todavía**

D3 dejó el mapa de atajos, la validación, la detección de conflictos, los comandos
semánticos, los pasos y el gesto cancelable. **Lo que no dejó es que un atajo funcione.**

Pendiente, y hasta que esté hecho D3 no se puede presentar como usable:

- **`RegisterHotKey` no está conectado.** Los 19 nombres se resuelven a comandos y las
  combinaciones se parsean y se validan, pero nadie registra ni un atajo en Windows. Hoy,
  pulsar `Win+Alt+→` no hace absolutamente nada.
- **Los conflictos reales no se han detectado.** El mapa caza los suyos —repetidos, nombres
  que no existen, combinaciones inválidas, y choques con el atajo de la vista general— pero
  eso es aritmética sobre una tabla. Quién tiene cogido de verdad `Win+Alt+→` en este
  equipo solo lo dice `RegisterHotKey` devolviendo `ERROR_HOTKEY_ALREADY_REGISTERED`
  (1409), y eso no se ha ejecutado ni una vez.
- **Nada se ha pulsado.** Faltan, como mínimo: `Win+Alt+` las cuatro flechas, `Esc` en
  mitad de un gesto, y **`Ctrl+Alt+W`**, que es el atajo de la vista general de NubeBar y
  pasa por el mismo parseador que D3 extendió.
- **El indicador breve de comando no existe.** No hay en la isla un aviso breve que
  reutilizar; montarlo con identidad de nube y sin tapar la ventana activa es trabajo
  visual y encaja en D6.

Y el riesgo que va con esto: `Nativo.TraducirAtajo` es **código compartido** con el atajo de
la vista general. El cambio de D3 es aditivo y ninguna tecla que antes funcionaba cambió de
significado, pero eso está razonado, no pulsado.

---

# D4 — Escritorios virtuales

La API de escritorios de Windows **no es pública**: Microsoft le cambia los CLSID entre
builds de Windows 11, y `Escritorios.cs` ya vivía envuelto en try/catch por eso. Así que
D4 se parte en dos: la **política** en `HyprEspacios.cs`, que decide y se puede comprobar,
y el **adaptador** en `Escritorios.cs`, que llama.

No se puede pedirle a la API cinco escritorios, ni un renombrado externo, ni que no
conteste a mitad de un cambio. Por eso lo que decide está aparte.

## Lo que ya había y lo que faltaba

`Escritorios.cs` tenía `Disponible`, `Todos`, `Actual`, `Ir`, `Mover`, `Crear` y
`EstaEnElActual`. D4 añadió: **`Renombrar`** (`Name` es escribible), **`Ordenar`**
(`Move(int)`), **`Enviar`** una ventana (`MoveToDesktop`), **`EspacioDe`** —el id, que
`EstaEnElActual` no da y el mapa necesita— y **`Espacios()`**, el puente que devuelve el
modo de uno si la API no contesta, para que nadie más tenga que preguntar si hay API.

Enviar una ventana **no** cambia de escritorio detrás de ella: son dos cosas distintas y
mezclarlas quita el control. Las junta el usuario, no nosotros.

## Nombrar, sin repetir

Tres casos reales: un escritorio sin nombre (Windows los crea así), dos con el mismo
nombre (Windows deja ponerlo), y un renombrado desde fuera. Los tres se arreglan **al
presentar**, no al guardar: lo que Windows tenga guardado es suyo, y reescribirle el nombre
a un escritorio por nuestra cuenta sería tocarle la configuración sin que lo pidiera.

Sin nombre sale «Escritorio 3», que es lo que hace Windows. Un repetido sale «Trabajo (2)».
El choque se detecta **sin distinguir mayúsculas** —dos rótulos que se leen igual son el
mismo— pero **se respeta la capitalización que escribió el usuario**: cambiarle «trabajo»
por «Trabajo» sería corregirle lo que tecleó. Límite de 40 caracteres, porque el indicador
tiene un ancho y un nombre de doscientos no se corta solo: desmonta la fila.

## El vecino no da la vuelta

En el último escritorio, «siguiente» no hace nada. Es lo que hace Windows con
`Win+Ctrl+→`, y es la misma deriva que ya se evitó en `MoverEnElOrden`: mantener pulsado el
atajo te llevaría del último al primero sin parar. `darLaVuelta` existe para quien lo
quiera, pero no de serie.

Y «ve al escritorio 7» cuando hay tres **no** es «ve al 3»: no se hace nada. El más cercano
no es lo que pidió nadie.

## Modo de un solo escritorio

Si la API no está o no contesta, la política devuelve **un** espacio en vez de ninguno. Así
el indicador enseña algo, los comandos de cambiar no encuentran vecino y no hacen nada, y
las ventanas se cuentan todas en el único espacio que hay. Ningún `if (hayApi)` repartido
por el código y ningún camino muerto.

Se distingue del equipo que de verdad tiene un escritorio por su id reservado.

## El mapa de ventana a espacio

**No hay un mapa propio persistente** — la especificación lo prohíbe y tiene razón: el
escritorio de una ventana lo sabe Windows, y guardarlo nosotros crea dos verdades que se
separan en cuanto el usuario mueve algo con `Win+Ctrl+→`.

Lo que hay es una **caché de lo último que contestó la API, con su marca de tiempo**.
Preguntar justo durante un cambio devuelve nada, el escritorio de antes, o una excepción; y
la respuesta correcta a eso no es «no lo sé y la escondo», es «lo último que me dijiste», y
se devuelve además si es reciente para que quien lo use decida. Si nunca contestó, se
supone el actual: más vale enseñar una ventana de más que esconder una que existe.

Caducidad de 2 s: un cambio de escritorio tarda décimas, así que algo de hace dos segundos
vale y algo de hace un minuto ya no dice nada.

## Recuperarse de un cambio de fuera

`Comparar` dice **qué** cambió, no solo que algo cambió: con «algo cambió» la única
reacción posible es rehacerlo todo, y rehacerlo todo es como se llega al parpadeo.

| Cambio | ¿Toca rehacer el reparto? |
|---|---|
| cambió el escritorio actual | **sí** — son otras ventanas |
| desapareció un escritorio | **sí** — sus ventanas han ido a alguna parte |
| apareció uno nuevo | no — el actual sigue siendo el mismo |
| renombrado | no — un nombre no mueve ninguna ventana |
| reordenados | no |

El orden solo se compara cuando el conjunto es el mismo: si entró o salió alguno, que «el
orden cambie» es obvio y no es información.

## Medido contra la API real, en lectura

```
[D4]  api disponible=sí
[D4]  espacios=2 (de la API)
  · 1. Escritorio 1 (aquí)  id=cd89790b-…
  · 2. Escritorio 2         id=eafca010-…
[D4]  siguiente=Escritorio 2
[D4]  anterior=ninguno (estás en el primero)
[D4]  mapa: contestó por 6 ventana(s), no contestó por 0
[D4]  nada se creó, se renombró, se reordenó ni se movió
```

`--diagespacios [s]` es **solo lectura** a propósito: crear o renombrar escritorios en el
equipo de alguien para ver si la API responde es pasarse.

## Riesgo de migración

**Ninguno.** D4 no añade ni cambia una sola clave: los espacios los sabe Windows y el mapa
es una caché en memoria. `Esquema.Actual` sigue en 4.

---

# D3 cerrada — los atajos registrados de verdad

`HyprTeclas` registra, clasifica, suelta y vuelve a registrar.

## Las cuatro cosas que no se pueden estropear

**No duplicar al recargar.** Registrar **siempre** empieza por soltar. Sin eso, cambiar la
configuración dejaría vivos atajos que ya no están en el mapa. Medido: registrar dos veces
seguidas da `registrados=10, vivos=10` y `¿se duplicó algo?=no`.

**Soltarlos todos al cerrar.** Se guarda qué ids se registraron —no se supone, porque si la
configuración cambió entre registrar y soltar, recalcular soltaría los nuevos y dejaría
vivos los viejos—. Medido: `soltados=10, vivos ahora=0, ¿queda alguno?=no`, y soltar otra
vez devuelve 0: es idempotente, que es lo que permite ponerlo en el cierre sin preguntar.

**`Ctrl+Alt+W` intacto.** Entra como reservado al construir el mapa, así que un atajo que lo
pise se rechaza **ahí**, con su problema explicado, y no en `RegisterHotKey` con un 1409 que
parecería culpa de otra aplicación. Y los ids viven en otro rango —`0xB100+` frente a
`0xB0CA`— así que ni por id ni por combinación se cruzan. Medido en arranque normal:
`atajo de la vista general "Ctrl+Alt+W": registrado`.

**Nada con la capa apagada.** NubeHypr nace apagada y quedarse con 19 atajos globales del
usuario «por si acaso» sería lo contrario de apagada. Medido: con `activo=no`, el arranque
normal no registra ni uno, y si quedaba algo lo suelta.

## Lo que midió el registro real: 9 de 19 ocupados

```
[D3]  mapa: 19 atajos, 0 problema(s)
[D3]  registrados=10 ocupados(1409)=9 otros=0 de 19
```

Ocupados en este equipo: `Win+Alt+` las cuatro flechas, `Tab`, `K`, `F`, `P` y
`Win+Alt+Shift+Tab`. **Esto no lo podía saber el mapa**: su detección de conflictos es
aritmética sobre una tabla —repetidos, nombres inexistentes, choque con la vista general—.
Quién tiene cogida de verdad una combinación solo lo dice Windows con
`ERROR_HOTKEY_ALREADY_REGISTERED`.

Y 1409 se clasifica aparte de un error genérico a propósito: «mal escrito» y «ocupado» son
dos problemas con dos soluciones distintas, y un log que los confunda no sirve.

**Qué proceso los tiene cogidos no es averiguable**: Windows no expone el dueño de un
atajo. Lo que queda es que el usuario los cambie, y para eso hace falta que el log le diga
cuáles fallaron — que es lo que hace.

**El mapa de serie necesita revisión.** Más de la mitad choca en este equipo. No lo rediseño
sobre una sola máquina, pero queda registrado: `Win+Alt` no es el prefijo libre que parecía.

## Lo que un atajo hace hoy

Resuelve el nombre, lo convierte en comando y lo ejecuta contra el estado observado —y con
la capa en **simulación**, que es lo de serie, calcula y apunta sin mover nada. Un atajo no
puede ser la puerta trasera por la que se mueven ventanas sin que el usuario haya puesto el
modo en «aplicar». Los de espacios se resuelven contra la lista real y se apuntan; aplicarlos
de verdad espera al arnés aislado de D4.

## Lo que sigue pendiente de D3

- **Pulsar los atajos.** Los 10 que registran están vivos durante `--diagatajos [s]` y cada
  pulsación sale en el log como `[D3] atajo «...»`. **No lo he comprobado yo**: probarlo
  requiere pulsar teclas, y eso lo hace el usuario.
- **El indicador breve de comando** no existe. Encaja en D6.

---

# D4 cerrada — el arnés aislado

`--arnesespacios` prueba las tres operaciones que **escriben** —renombrar, reordenar y
enviar una ventana— sin tocar nada del usuario.

## Las seis reglas

1. **Ninguna ventana del usuario.** El arnés crea la suya, la usa y la cierra. No se mira
   ni se enumera ninguna otra.
2. **Cero inyección de entrada.** Ni `SendKeys`, ni `keybd_event`, ni `SendInput`, ni un
   clic. Todo por la API, que es lo que se quiere probar.
3. **Se guarda el escritorio actual antes de empezar**, y se vuelve a él si algo lo cambió.
4. **De los escritorios del usuario solo cambia uno: el que se crea y se borra.** El
   reordenado mueve el de prueba y lo devuelve al final.
5. **La limpieza va en `finally`**, con las variables declaradas fuera del `try` para que
   pueda limpiar lo que se creara aunque la excepción saltara justo después de crearlo.
6. **No escribe la configuración.**

Y el orden importa: **la ventana se cierra antes de borrar el escritorio.** Borrar un
escritorio con ventanas dentro las manda a otro, y entonces la limpieza dejaría una ventana
suelta en un escritorio del usuario.

La garantía que de verdad se comprueba al final no es «hay el mismo número de escritorios»:
es que **la secuencia de ids de los del usuario es exactamente la de antes**.

## Lo medido

```
[D4]  ANTES: 2 escritorio(s)
  · 1. Escritorio 1 (aquí)  id=cd89790b-…
  · 2. Escritorio 2         id=eafca010-…
[D4]  creado  id=96c6de1c-…  escritorios=3   ¿crear me movió de escritorio?=no
[D4]  el de prueba nace en la posición 2 con el nombre «Escritorio 3»
[D4]  RENOMBRAR -> «NubeHypr PRUEBA 225936»: llamada=ok, leído igual, ¿llegó?=sí
[D4]  REORDENAR -> posición 1: llamada=ok, leída=1, ¿llegó?=sí
[D4]  devuelto al final (posición 2)
[D4]  ventana temporal: hwnd=0x410016, nace en cd89790b-…
[D4]  ENVIAR -> 96c6de1c-…: llamada=ok, leído igual, ¿llegó?=sí
[D4]  comprobación cruzada: ¿sigue en tu escritorio?=no (se fue, como se pidió)
--- limpieza ---
[D4]  ventana temporal cerrada: ¿sigue existiendo?=no
[D4]  escritorio de prueba borrado: ¿sigue en la lista?=no
[D4]  DESPUÉS: 2 escritorio(s) — los mismos ids, en el mismo orden
[D4]  limpieza=COMPLETA
```

Las tres operaciones se comprueban **leyendo el resultado**, no solo por el valor que
devuelve la llamada: `Renombrar` puede devolver `ok` y el nombre no haber llegado. Y
`Enviar` se comprueba por dos vías distintas —`EspacioDe` y `EstaEnElActual`, que es la que
usa el carrusel— porque dos caminos que coinciden descartan que uno de los dos mienta.

## Límites de la API, encontrados aquí

- **Reordenar no se puede probar sin mover transitoriamente los del usuario.** No hay forma
  de mover un escritorio a una posición sin desplazar a los demás. Se mitiga devolviéndolo
  al final y comprobando la secuencia, pero durante unos cientos de milisegundos el último
  escritorio del usuario está un puesto más abajo. Si alguien mirase en ese instante, lo
  vería.
- **`Crear` no cambia de escritorio** — comprobado, no supuesto: si fuera falso, el arnés
  habría movido al usuario de escritorio.
- **Borrar arrastra las ventanas** que haya dentro a otro escritorio. De ahí el orden de la
  limpieza.
- **Quién tiene cogido un atajo global no es averiguable** (de D3, y sigue siendo verdad).
- **No sé forzar que la librería falle** sin romper algo, así que el modo de un solo
  escritorio está probado en frío y no provocado de verdad.

`--diagespacios` sigue siendo **solo lectura** para el uso normal. Escribir solo lo hace
`--arnesespacios`, y solo sobre lo que él mismo crea.

---

# D5 — Reglas, perfiles y persistencia

## La decisión que lo gobierna todo

**Una regla sin criterios no coincide con nada.** Lo contrario —que coincida con todo— es
el valor por defecto que parece cómodo y es el que convierte un error de tecleo en «todas
mis ventanas se han ido al escritorio 3». Una regla vacía está a medio escribir, y a medio
escribir no se aplica ni se puede guardar.

## Criterios y acciones

| Criterio | Cómo compara |
|---|---|
| `Proceso` | exacto, sin mayúsculas (`firefox`, no `fire`) |
| `Clase` | exacto, sin mayúsculas |
| `Titulo` | **contiene**, sin acentos ni mayúsculas |
| `Ruta` | **contiene**, sobre la ruta completa |

Se combinan con **Y**: los que estén puestos tienen que cumplirse. Sin acentos porque quien
escribe una regla para «Configuración» no tiene por qué acordarse de la tilde — misma norma
que el buscador, con `Parecido.Normalizar`.

Acciones: espacio, monitor, layout, flotante, ancho, alto y foco. **`null` significa "esta
regla no opina de eso"**, no «ponlo a cero». Esa distinción es la que permite que dos reglas
se sumen: una dice el espacio y otra dice que sea flotante, y la ventana acaba flotante en
ese espacio.

## Prioridad, especificidad, posición

Tres niveles, en este orden:

1. **Prioridad explícita** — es lo que el usuario puso para resolver un empate a mano.
2. **Especificidad** — con la misma prioridad gana la que tiene más criterios, porque se
   escribió pensando en un caso concreto; la de un solo criterio es la general.
3. **Posición en la lista** — estable y predecible: el orden del fichero manda.

Las reglas se recorren de más peso a menos y **cada campo lo pone la primera que opine**.
Una de prioridad alta no se ve pisada por una general que viene después, que es lo que la
gente espera de una prioridad.

## Cada regla explica lo que hace

«Mi navegador aparece en un sitio raro» no se investiga leyendo el código: se investiga
sabiendo qué regla coincidió y por qué las demás no. `Resolver` devuelve **una línea por
regla**:

```
  [editor] COINCIDE -> espacio=2 layout=Principal
  [navegador] no: el proceso es «code» y la regla pide «firefox»
  [apagada] no: está desactivada
  [baja] COINCIDE -> no aportó nada nuevo
```

Esa última línea es información, no ruido: explica por qué una regla «que coincide» no se
nota. Y la decisión guarda **quién** puso cada campo — sin eso, «¿por qué esta ventana es
flotante?» no tiene respuesta.

## Deshacer y rollback

Cada regla tiene **id**: quitar «la tercera» se rompe en cuanto el usuario reordena.
`Quitar` por id, `Alternar` para desactivar sin perder lo escrito (borrar no es reversible),
`Poner` actualiza **en su sitio** sin moverla en el orden, `Instantanea` + `Comparar` para
el rollback — que así no es un acto de fe: se ve qué entró, qué salió y qué cambió.

Un cambio de valor se detecta como **cambio**, no como quitar y poner.

## Las reglas en el config

`NubeHypr.Reglas` pasa de `List<string>` a `List<Regla>` tipada. **No hace falta migración**:
un config en v4 trae `"Reglas": []`, que deserializa en una lista vacía.

**Lo que esta versión no conoce se conserva** — requisito del documento, cumplido porque las
migraciones trabajan sobre el **árbol** JSON: una clave desconocida dentro de `NubeHypr`, y
una sección entera desconocida, sobreviven a migrar. Probado. Y el validador no rechaza un
árbol por llevar claves que no entiende.

Una regla da la vuelta por JSON sin perder nada, y **los nulos siguen siendo nulos**: «no
opino» no se convierte en un cero.

# El editor de atajos en Ajustes

## Por qué hace falta

Midiendo el registro real salieron **9 de 19 combinaciones ya cogidas** por otras
aplicaciones en este equipo. Unos atajos que solo se cambian abriendo el config a mano no
son usables: el usuario no sabe cuáles están ocupados, no sabe qué nombres existen, y cada
intento es un reinicio.

## Lo que hace

Una fila por cada uno de los 19 comandos, con su rótulo, su combinación, su estado y un
botón «De serie». Abajo: **Comprobar cuáles están cogidos**, **Todos de serie** y **Guardar
atajos**.

- **Capturar**: se pulsa la combinación en el campo. Modificadores solos no cuentan, y se
  exige al menos uno — un atajo global de una sola tecla se la quita al usuario en todo el
  sistema. `Supr` lo borra.
- **Conflictos antes de guardar**: se construye el mapa con lo que hay en los campos y se
  enseñan sus problemas —repetidos, nombres inexistentes, y el choque con el atajo de la
  vista general, que entra como reservado—. **Con conflictos no se guarda.**
- **Comprobar** registra de verdad un instante y suelta, con `ComprobarAtajo`. Es lo único
  que dice quién tiene cogida una combinación.
- **Guardar** escribe solo `Atajos` con `PonerAtajos`, **no la sección entera**: las claves
  de `NubeHypr` que esta versión no conozca se conservan. Y los hooks se rehacen al aplicar
  —`HyprTeclas.Registrar` suelta antes de registrar—, así que guardar veinte veces no deja
  veinte atajos.

## La limitación que manda en el diseño

**Una combinación que ya tiene otra aplicación no se puede teclear en el campo**: Windows se
la entrega a esa aplicación y nunca llega. De ahí el botón «Comprobar», que es la vía
fiable. Y **qué aplicación la tiene cogida no se puede saber**: Windows no lo dice.

## Comprobación de humo

El código de la página solo corre cuando se abre Ajustes y se entra en la pestaña, así que
una excepción ahí no la ve nadie hasta que el usuario pulsa. `--diagatajos` construye la
ventana, entra en la página, cuenta y cierra:

```
[D5]  editor de atajos: la página se construyó. campos de texto=21 botones=36
[D5]  ¿hay una fila por comando?=sí
[D5]  Ajustes cerrada
```

No es «se ve bien» —eso hay que mirarlo— es «no revienta y tiene filas».

---

# D5.1 — Las reglas, conectadas a que aparezca una ventana

Hasta aquí las reglas eran una función que nadie llamaba: `HyprReglas.Resolver` existía,
estaba probada, y no se ejecutaba nunca en una sesión real. D5.1 la conecta al momento en
que aparece una ventana **y a nada más**: resuelve, apunta y se calla.

**No llama a `SetWindowPos` ni una vez, en ningún modo.** Ni con la capa en "aplicar".
Conectar y aplicar a la vez habría metido dos cambios en uno, y el primer efecto raro no
se habría podido atribuir a ninguno de los dos.

## Dónde se engancha, y por qué ahí

`GestorVentanas.Cambio` ya avisa de cinco cosas distintas: la lista, la activa, los
títulos, los estados y los sitios. Las reglas escuchan **solo la lista** (`Cambios.Lista`):

- Una ventana se coloca cuando nace. Un cambio de título no es un sitio nuevo.
- Resolver en cada cambio sería recalcular cuarenta veces por minuto para no hacer nada —
  y ese es exactamente el camino por el que se llegó al parpadeo de C4.1.

La suscripción es **aparte** de la de la vista general. `EscucharCambios` solo escucha
mientras la vista está abierta, porque solo entonces hay rejilla que rehacer; las reglas
tienen que oír la ventana que aparece aunque no haya ningún panel abierto, que es el caso
normal. Y escucha **solo la isla jefa**: con dos monitores hay dos `MainWindow`, y sin eso
cada ventana nueva se resolvería dos veces y saldría dos veces en el log.

Con la capa apagada no hay ni un delegado colgado del evento: `AplicarReglasDeHypr` se
engancha o se suelta según el modo, se llama desde el mismo sitio que los atajos (así
recargar la configuración rehace las dos cosas) y `Closed` la suelta.

## Una ventana se resuelve una sola vez

`HyprVigilante` recuerda los handles que ya vio. Sin eso, una ventana que cambia de título
volvería a "aparecer" para las reglas cada vez. Y las que se van se olvidan: si no, una
sesión larga acumularía handles muertos y un hwnd reutilizado por Windows se daría por ya
visto — la ventana nueva no se resolvería nunca.

## Qué se apunta

| Línea | Qué dice |
|---|---|
| `ventana nueva hwnd= proceso= titulo= modo=simulacion` | de qué ventana se habla, y el modo |
| `decision:` | cada campo con su valor **y con la regla que lo puso** |
| `puestos:` | los campos que alguien decidió |
| `sin opinion:` | los campos de los que **nadie** opinó |
| `[id] COINCIDE -> …` / `[id] no: …` | una línea por regla, con el motivo exacto |
| `NO se ha movido nada` | la promesa de D5.1, escrita donde se lee |

La lista de **campos sin opinión** no es relleno: "mi navegador no se va al espacio 2" se
contesta viendo que ninguna regla opinó del espacio, y sin esa lista eso no se distingue
de "se intentó y falló". Y una regla que coincide pero no puede poner nada sale como
*"no aportó nada nuevo"* en vez de desaparecer del log.

Para una ventana que no encaja en ninguna regla el log son **dos líneas**: la ventana y
«ninguna regla coincide». Seis líneas de "no opinó nadie" por cada ventana que abres no se
pueden leer.

## `--diagreglas`

`NubeBar.exe --diagreglas [segundos]`. Resuelve contra las ventanas que hay abiertas, se
engancha al evento y espera a que aparezca alguna. No mueve nada y no escribe la
configuración (`Config.SoloLectura`). Si tu configuración no tiene reglas —lo normal: la
lista nace vacía— usa un juego de ejemplo **solo en memoria**, porque un diagnóstico de
reglas sin reglas no mide nada; y lo pone también en la configuración en memoria durante
la escucha, o la fase en vivo contaría cada ventana nueva como "sin reglas".

Termina imprimiendo `movimientos aplicados por este diagnóstico: 0`, leído del contador
`HyprAdaptador.VecesMovida`, que sube una vez por cada `Nativo.Colocar` que de verdad se
llama. Es la diferencia entre afirmar que no se movió nada y suponerlo.

### Lo medido

| Qué | Resultado |
|---|---|
| Ventanas organizables | 6 (0 descartadas por la lista protegida) |
| Resueltas en la pasada inicial | 6: 4 con alguna regla, 2 sin ninguna |
| Idempotencia sobre una ventana real | la misma decisión las dos veces |
| Ventana nueva durante la escucha | `Notepad` «Bloc de notas», resuelta **una** vez |
| Resueltas en vivo | 7 de 7, `sinReglas=0` |
| Movimientos aplicados | **0** |
| Configuración escrita | ninguna (`SoloLectura=True`), v3 byte a byte igual |

## Compatibilidad: `Reglas` como lista de cadenas

`NubeHypr.Reglas` nació como `List<string>` en un borrador. El riesgo no era perder una
clave: era perder **toda** la configuración. Con cadenas ahí, `JsonSerializer` lanza,
`Config.Cargar` devuelve null y NubeBar arranca de serie — el usuario pierde su
configuración entera por una clave.

`HyprReglas.LectorTolerante` lo evita. Una cadena se conserva como `Id` y queda **inerte**
(sin criterios no coincide con nada, que es la regla de oro de D5); un número, un booleano
o un `null` quedan como `entrada-no-valida`; un objeto que no se entiende, como
`regla-ilegible`. Todo se apunta en el log, y la lista sigue.

Dos cosas que una prueba encontró y el código no tenía:

- **`HandleNull => true`.** Sin eso, `System.Text.Json` resuelve el `null` por su cuenta y
  mete un `null` en la lista sin llamar al convertidor. Después, cualquier recorrido de las
  reglas revienta — y ese recorrido ahora pasa cada vez que aparece una ventana.
- **Consumir el objeto entero antes de intentar entenderlo.** El `try/catch` alrededor de
  `Deserialize<Regla>(ref lector, …)` no protegía nada: si falla a mitad del objeto, el
  lector se queda dentro y `System.Text.Json` aborta la carga completa con «el convertidor
  leyó de más». O sea, tumbaba la configuración igual, solo con otro mensaje.
  `JsonDocument.ParseValue` consume el elemento completo —válido o no— y deja el lector
  donde debe; después ya se intenta convertir.

Las claves desconocidas siguen conservándose, porque las migraciones trabajan sobre el
**árbol**: lo que no se toca sigue ahí. Probado con una clave y una sección enteras que
esta versión no conoce.

## Reglas y atajos en los perfiles

`Perfiles.Claves` acepta `NubeHypr.Reglas` y `NubeHypr.Atajos`. Lo que **no** acepta
importa más: `NubeHypr.Activo`, `NubeHypr.Modo` y `NubeHypr.RestaurarAlOrganizar` no son
claves de perfil. Un perfil cambia la configuración; si pudiera encender la capa —o
ponerla en "aplicar"— aplicar un perfil te empezaría a mover ventanas sin haberlo pedido,
y desmaximizar las tuyas de paso.

Un perfil con reglas produce **escrituras de configuración**, no movimientos: la lista de
reglas viaja entera como un solo valor y nadie toca una ventana al aplicarlo.

## Pendiente de prueba a mano

Dos cosas de D5 siguen **sin probar a mano**, y no se deben dar por buenas:

1. **El editor visual de atajos** en Ajustes. La comprobación de humo lo construye, cuenta
   19 filas y lo cierra: eso dice que no revienta y que tiene filas, no que se use bien.
   Reasignar, ver un conflicto y guardar está sin tocar con el ratón.
2. **Los atajos reales.** Están registrados de verdad (9 de 19 los tiene cogidos otra
   aplicación, medido), pero pulsarlos está sin verificar: `Win+Alt+flechas`, `Esc` y
   `Ctrl+Alt+W`.

Y lo de siempre: **las reglas no se aplican todavía**. Deciden en el log y nada más.

---

# D6 — Integración visual

El objetivo del documento es que NubeHypr no parezca "tarjetas encima de un motor", sino
que **el mismo lenguaje de nube explique foco, espacios, ajuste y recuperación**. Lo que
eso pedía en concreto eran seis cosas, y las seis tienen ahora un sitio donde se deciden
—no en un manejador— y una prueba que las comprueba sin abrir una ventana.

## Dos colores, no un acento

NubeHypr dice dos cosas y tiene que decirlas igual en los siete temas: **dónde estás o qué
va a pasar** (azul) y **cuidado con esto** (coral). El acento del tema no sirve: en Gruvbox
es amarillo, en Catppuccin morado y en Nube justamente el coral. Si "va a pasar" y "cuidado"
salieran los dos del acento serían el mismo color, y el filo de la previsualización se
leería igual que el botón de quitar una regla.

`HyprPinta` resuelve eso derivando del tema **cuando el tema tiene algo parecido**:

| Papel | De dónde sale | Si el tema no lo tiene |
|---|---|---|
| Azul (foco, acción principal) | `Roles.Sistema`, si de verdad es azul (tono 165–265°) | el azul de Nube `#65B8D6` |
| Coral (aviso, acción destructiva) | `Roles.Negativo`, si está en la familia rojo-naranja | el coral de Nube `#E87562` |

Y todo pasa por `Contraste`, así que no hay "un color que se ve mal en el tema claro": se ve
o se corrige. Medido en los siete temas: azul y coral siempre ≥ 4,5:1 como texto sobre la
superficie de un panel, y siempre a más de 60° de tono el uno del otro.

En el equipo de pruebas, con el tema **Negro iPhone** puesto: azul `#7AA2F7` a **8,34:1**
contra el fondo y 6,62:1 como tinta; coral `#DA4453` a 4,93:1 y 4,81:1 como tinta;
separación de tono **133°**.

## El filo de la previsualización: dos líneas, no una

El problema real no es elegir un color bonito. Es que la previsualización se dibuja **encima
del escritorio del usuario** y no se sabe qué hay debajo: un fondo oscuro, una foto
clarísima, o Wallpaper Engine moviendo un vídeo cuya luminancia cambia cada fotograma. Un
filo de un solo color siempre tiene un fondo que lo borra — el amarillo de Gruvbox sobre una
playa no es un borde, es nada.

La solución no es adivinar el fondo: es un **filo doble**. Una línea exterior oscura (clara
si se sabe que el fondo es muy oscuro) y, dentro, la línea azul. Sobre claro se ve la
exterior; sobre oscuro, la azul; sobre un vídeo que cambia, siempre una de las dos. Medido:
21:1 sobre blanco, 21:1 sobre negro, 3,95:1 sobre un gris medio — el mínimo exigido es 2,2.

El relleno sigue siendo un velo flojo (alfa 48/255) **a propósito**: si tapara, la
previsualización dejaría de enseñar dónde va a caer la ventana y sería una mancha.

Y los cuatro seguros de "los clics la atraviesan" no se han tocado: `IsHitTestVisible=false`
en cada trozo, más `WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE` y `HTTRANSPARENT` que pone
`HacerseAtravesable`. Las pruebas `PreviaNoRecibeClics` y `PreviaNoRobaFoco` siguen verdes.

Además, el cuerpo de la previsualización de ajuste y el del destino de NubeHypr eran **dos
copias** del mismo `Border`; con el filo doble serían dos copias de algo con cuatro números
que hay que mantener iguales, y la que se quedara atrás sería la que se ve en el gesto que
menos se prueba. Ahora es uno.

## La vista general: la activa y la elegida son dos cosas

Antes de D6 el acento marcaba la ventana **activa del sistema**, y el teclado solo servía
para cerrar con Esc. Para elegir había que coger el ratón. Eso no es "teclado completo", y
además no había dónde enseñar la elección.

Ahora hay tres niveles y pueden verse a la vez:

- **Activa del sistema**: filo de acento.
- **Elegida con el teclado**: un halo azul **por fuera** del marco.
- **Minimizada**: atenuada, y lo dice con la palabra "Minimizada".

El halo va por fuera para que encenderlo **no mueva ni un píxel** de lo de dentro. Y el
grosor del filo es el mismo en todos los niveles: hay una prueba que lo exige, porque con el
grosor cambiando entre 0 y 1,4 según el foco, marcar una tarjeta movía su contenido y eso se
lee como que las tarjetas tiemblan.

Teclado: flechas, Inicio, Fin, Entrar/Espacio para activar, Supr para cerrar (con la
confirmación de siempre) y la tecla de menú contextual. Las flechas **no dan la vuelta**: con
vuelta, mantener pulsada una flecha recorre la rejilla en bucle y nunca se para en un borde,
así que no hay forma de saber que has llegado al final sin mirar.

La elección sobrevive a que la lista cambie: si la ventana elegida sigue abierta, la elección
se queda en ella aunque se haya movido de sitio; si se cerró, se queda **en su hueco**, que es
lo que uno espera al ir cerrando tarjetas en fila.

Y el estado va también en palabras, por `AutomationProperties`: el color nunca puede ser el
único portador de la información.

## El menú ya no tapa la ventana elegida

El menú de una tarjeta tiene catorce opciones: casi 500 px de alto. Se colocaba en la esquina
de la tarjeta + 12 px, así que **tapaba la tarjeta** — elegías "mitad izquierda" sin ver de
qué ventana. Es justo lo que el documento no quiere.

`HyprVista.DondeCabe` elige el sitio: de los cuatro lados de la elegida, el que tenga hueco, y
entre los que tengan, el que más. Si no cabe por ninguno (una ventana que ocupa casi toda la
pantalla) devuelve el mejor sitio **y avisa de que solapa**, para que quien pinte baje la
opacidad en vez de tapar a ciegas. Probado en los cuatro bordes de la isla, y medido en los
dos monitores de este equipo: uno horizontal de 2560×1440 (el menú se va a la derecha) y uno
**vertical** de 1080×1920 (se va abajo). En los dos, dentro del área útil y sin solapar.

## El indicador de escritorio: nombre, número y estado

El módulo pintaba los números y marcaba el actual. Eso dice en qué escritorio estás y cuántos
hay, y nada más — ni cómo se llama, ni si los demás tienen algo dentro. Y "¿en cuál tenía yo
el correo?" es exactamente la pregunta que un indicador de escritorios existe para contestar.

Ahora: un rótulo "2/4 · Trabajo" con la nube delante, y un puntito de estado por escritorio.
El estado va por **tamaño** del punto y no por color, porque el color ya lo ocupa "cuál es el
actual" y dos informaciones en el mismo canal no se distinguen.

Cuatro tallas, y lo que se cae lo decide el sitio disponible: primero el total, luego el
nombre, y el número **no se cae nunca**. En vertical no cabe el nombre por mucho alto que
haya: una columna de 30 px no tiene dónde ponerlo.

Un detalle que se escapa fácil: `HyprEspacios.NombreVisible` nunca devuelve vacío — un
escritorio sin nombre sale como "Escritorio 3". Si "tiene nombre" mirara solo si la cadena
está vacía, el rótulo completo diría "3/4 · Escritorio 3": el número tres veces y ninguna
información nueva. Hay prueba.

## El panel de reglas y atajos

Página propia en Ajustes (Sistema → NubeHypr), porque son dos preguntas distintas: "¿qué tecla
hace esto?" es la de Atajos, y "¿por qué mi navegador no se va al espacio 2?" es esta.

- **Búsqueda** sobre todo: id, proceso, clase, título, ruta, y también sobre lo que la regla
  *hace* ("espacio 2", "flotante", "principal"). Con el mismo normalizador de acentos del
  buscador de aplicaciones — dos normalizadores distintos serían la forma de que "Música" se
  encuentre en un sitio y no en el otro.
- **Grupos** por lo que la regla *mira*, que es la pregunta que uno tiene al abrir el panel.
  Dentro de cada grupo, el mismo orden con el que el motor evalúa: si el panel las ordenara de
  otra forma, la de arriba no sería la que gana. El grupo de las que no miran nada va al final,
  porque es una bandeja de problemas y no un grupo más.
- **Estado activo** con filo azul, apagado atenuado, y las que no se pueden aplicar en coral —
  y siempre con la palabra, no solo con el color. Que una regla esté apagada y no se vea es la
  causa número uno de "mi regla no funciona".
- **Explicación de la regla aplicada**: por cada ventana abierta, qué campo pone qué regla,
  qué campos quedaron sin opinión y por qué se descartó cada una. Se construye desde el **mismo
  `HyprVigilante.Apunte`** que se escribe en el registro, para que el panel y el log no puedan
  decir cosas distintas.
- **El aviso de simulación**, arriba y repetido al pie de las decisiones. Un panel que enseña
  decisiones sin aclarar que no se ejecutan es un panel que miente.

## La nube, protagonista sin ser una mascota

La nube de `Nubecita` aparece en el indicador, en la cabecera del panel y en los vacíos. El
riesgo de una mascota es que crezca, así que el tamaño **no se escribe en cada sitio**: se pide
a `HyprPinta.TamanoDeLaNube`, con mínimo 16 px, máximo 72 px y nunca más del 34 % del lado
disponible. Medido en este equipo: 16 px en la isla, 47,6 px en una cabecera de Ajustes, 64,6
px en un panel vacío.

Y los vacíos llevan nube **y una frase de qué es esto**: un panel que solo dice "no hay nada"
es una caja genérica, que es lo que el documento pide no enseñar.

## Animaciones pegadas al estado, y nada de repintar en bucle

`HyprPinta.TocaAnimar` exige dos cosas para animar: que el estado haya cambiado de verdad y que
el nivel del usuario lo permita. Relanzar un fundido con el mismo origen y destino es
exactamente lo que se ve como parpadeo — es lo que pasó en la vista general en C4.1. Y la
duración sale siempre de `Animacion`, nunca de un número escrito a mano: con "desactivado" vale
cero y no se lanza nada.

Para CPU alta y muchas ventanas: la vista ya reconcilia en vez de rehacerse, el indicador
compara una **huella** antes de repintar, y mover la elección repinta **dos tarjetas**, no la
rejilla. La huella es la pieza delicada: tiene que llevar lo que decide el dibujo (nombre,
número, estado) y **no** lo que no (qué ventana concreta es, el título), porque con el título
dentro cada pulsación de tecla en el navegador repintaría la isla. Hay pruebas de las dos
direcciones.

## El indicador breve de un comando

Era el último hueco de D6: pulsas `Win+Ctrl+Alt+→` y, con la capa en simulación, **no se mueve
nada**. La respuesta honesta es "ha decidido, no ha movido", y sin indicador eso solo se sabía
abriendo el registro. Un atajo que no da señal es un atajo que se pulsa dos veces.

`HyprAviso` decide **qué se escribe, de qué tono y cuánto dura**; dónde se dibuja es de la isla.
No se construyó una segunda superficie flotante: se extendió el aviso de volumen, que ya
resuelve lo difícil —se reaprovecha en vez de reabrirse, se cierra solo y **no se traga el menú
Inicio ni la vista general**—. Una superficie nueva habría sido una segunda ocasión de
equivocarse en esas tres cosas.

Lo que dice, en los cinco casos:

| Caso | Qué sale | Tono |
|---|---|---|
| Comando con efectos, en simulación | `Principal · simulado, no se ha movido nada` | coral |
| Comando con efectos, aplicando | `Principal · 4 ventanas` | azul |
| Comando sin efectos | `Siguiente: no hay otra ventana` (el motivo del motor) | coral |
| Escritorio | `Espacio →: 2 · Trabajo (simulado, no se ha cambiado)` | coral |
| Deshacer sin nada que deshacer | `Deshacer: no hay nada que deshacer` | coral |
| Capa apagada / modo seguro | dos frases distintas, nunca la misma | coral |
| El atajo revienta | `…: ha fallado (está en el registro)` — sin volcar la excepción | coral |

Tres decisiones que no son obvias:

- **"Simulado" se dice siempre.** Es la diferencia entre "lo he hecho" y "lo haría". Si se
  ocultara, el usuario vería `Principal · 4 ventanas`, miraría la pantalla sin un solo cambio y
  concluiría que NubeHypr está roto — cuando está haciendo exactamente lo que se le pidió.
- **Sin efectos se enseña el motivo, no el rótulo.** `Foco →` a secas, sin que pase nada,
  parece un atajo averiado; `no hay otra ventana` se entiende a la primera.
- **La duración depende del texto** (1,2 s base, 180 ms por palabra, tope 3 s). Fija, o no da
  tiempo a leer el mensaje largo o el corto estorba.

Y el rótulo del escritorio sale de la **misma `HyprIndicador.Ficha`** que usa el indicador de la
barra: dos formas de nombrar el mismo escritorio en la misma pantalla es una manera barata de
confundir.

### El fallo de compartir panel con el aviso de volumen

Reaprovechar el panel del volumen era la decisión correcta, pero trajo un fallo propio. Cada
aviso guardaba sus controles en sus campos y decidía *«¿ya está puesto lo mío?»* mirando si esos
campos eran distintos de `null`. Eso falla en esta secuencia:

1. Sale un mensaje de texto → `_textoDelAviso` apunta a su `TextBlock`.
2. Cambias el volumen → `AbrirPanel` **sustituye el contenido**, pero `_textoDelAviso` sigue
   apuntando al de antes, ya desenganchado del árbol visual.
3. Sale otro mensaje → cree que su panel está puesto, pinta en un elemento que no está en
   pantalla: **no se ve nada**, y encima acorta el aviso de volumen.

El arreglo no es añadir más comprobaciones de `null`: es que **lo que hay dentro se apunta en
vez de deducirse** (`Dentro.Barra` / `Dentro.Texto`), y lo pone quien crea el contenido. Cada
creador suelta además los controles del otro, que desde ese momento están fuera del árbol.

Comprobado mirando, no razonando: las capturas `10e` y `10f` son justo esa secuencia — barra de
brillo y, después, texto. Si `10f` enseñara la barra o un panel vacío, el fallo habría vuelto.

### El fallo que encontró la captura y no la prueba

La primera tabla de glifos puso `0xE7E8` para los escritorios. Es un **botón de encendido**. La
prueba de glifos lo dio por bueno porque comprobaba que el punto de código estuviera en el
bloque de iconos —y lo estaba—; se vio en una captura, con un icono de apagar al lado de
`Espacio →`. Los siete se redibujaron con la fuente de verdad y se eligieron mirándolos.

De ahí salieron dos cambios, no uno:

1. El mapa correcto (`0xE737` una ventana para foco, `0xE7C4` las ventanas superpuestas para
   escritorios, `0xE7A7` la flecha de volver para deshacer).
2. La prueba de glifos ahora pregunta a la **fuente** si tiene el dibujo
   (`GlyphTypeface.CharacterToGlyphMap`), no solo si el número cae en el rango. Eso no detecta
   un icono *inadecuado* —para eso hacen falta ojos— pero sí uno que saldría como una caja
   vacía. Si la fuente no estuviera instalada, la prueba se salta y lo dice en vez de fingir
   que ha pasado.

## `--diagd6`

Lo que una prueba pura no puede contestar porque depende del equipo: monitores, DPI, borde y
orientación de la isla, alto contraste de Windows, contraste real del azul y el coral contra el
tema **que el usuario tiene puesto** (no los siete de serie), el filo medido sobre blanco,
negro y gris, dónde caería el menú en el área útil de cada monitor, los tamaños de la nube y el
nivel de animación. Solo lectura: no mueve ventanas, no abre paneles y no escribe la
configuración.

### Lo medido en este equipo

| | |
|---|---|
| Monitores | 2 — 2560×1440 (principal) y **1080×1920 vertical**, los dos a 100 % |
| Isla | modo isla, borde arriba, horizontal, alto 38 |
| Indicador | 563 px de sitio → talla Completo |
| Tema puesto | Negro iPhone, fondo `#000000`, superficie `#1E1E1E` |
| Azul / coral | 8,34:1 y 4,93:1 contra el fondo; 133° de separación |
| Filo sobre blanco / negro / gris | 21:1 · 21:1 · 3,95:1 (mínimo 2,2) |
| Menú en los dos monitores | dentro del área útil y sin solapar |
| Nube | 16 / 47,6 / 64,6 px |
| Configuración escrita | ninguna (`SoloLectura=True`) |

## Pendiente de prueba manual

Nada de esto está marcado como verificado: solo se ha comprobado la lógica.

1. **Que el filo se vea sobre un vídeo de Wallpaper Engine.** Medido contra colores fijos;
   un vídeo cambia cada fotograma y eso necesita ojos.
2. **Que no haya glifos en caja** en la página nueva de Ajustes ni en el indicador.
3. **Que no haya parpadeos** abriendo y cerrando paneles rápido, con la vista general abierta
   y cambiando de aplicación por fuera.
4. **Que el halo del teclado se distinga del filo de la ventana activa** de un vistazo.
5. **El teclado de la vista general**: flechas, Inicio, Fin, Entrar, Supr y la tecla de menú.
6. **Los cuatro bordes y la barra vertical** con los ojos: la lógica dice que el menú cae
   dentro del área útil, no que se vea bien.
7. **Alto contraste de Windows encendido**: la rama está probada en lógica (velos opacos, filo
   más gordo, sin relleno), no vista.
8. **Un tema claro** escrito a mano por el usuario.
9. **El indicador breve, pulsando de verdad.** Las tres caras están fotografiadas
   (`10b`, `10c`, `10d`) con mensajes construidos a mano, que es otra cosa: queda por ver que
   al pulsar `Win+Ctrl+Alt+→` salga el mensaje correcto, que pulsar cinco veces seguidas
   **actualice el texto en vez de parpadear**, y que con el menú Inicio abierto el indicador
   no se lo coma.

---

# D7 — Fiabilidad, recuperación y modo seguro

El documento lo plantea como convertir una demostración convincente en algo que pueda estar
encendido días. Y pone el riesgo de la fase por escrito: *"una recuperacion automatica
demasiado agresiva puede empeorar el problema. Preferir no hacer nada"*. Esa frase gobierna
todo este archivo.

## El supervisor no arregla nada: deja de tocar

`HyprSalud` mira cuatro señales medibles desde dentro —ninguna es "¿funciona?":

| Señal | Qué detecta |
|---|---|
| Edad de la instantánea | el gancho de ventanas se ha caído; sin esto, "no llega ningún evento" es indistinguible de "no pasa nada" |
| Atajos vivos frente a esperados | `RegisterHotKey` falló o alguien los soltó por detrás |
| Fallos seguidos al aplicar | el adaptador no está consiguiendo mover nada |
| Excepciones seguidas | lo mismo, un nivel más arriba |

Tres estados —**Sano**, **Sospechoso**, **Detenido**— y tres acciones: **nada**, **mirar** y
**modo seguro**. Y eso es todo: *no existe ninguna acción que reorganice*. Hay una prueba
que lo exige, porque es el otro lado de la misma regla.

"Sospechoso" existe para que el registro diga qué pasaba **antes** de apagar, en vez de
saltar de "todo bien" a "modo seguro" sin nada en medio.

Los umbrales son generosos a propósito (5 fallos para sospechar, 15 para parar; 30 s de
instantánea vieja, 3 min de muerta). Un supervisor nervioso que apaga la capa cada vez que
una ventana tarda es peor que no tener supervisor: el usuario no sabría por qué se apaga y
acabaría dejándolo apagado.

Un **acierto borra la racha**. Si no la borrara, cinco fallos repartidos en una semana
apagarían la capa igual que cinco seguidos, y no es lo mismo.

Y si el propio supervisor revienta, lo que **no** se hace es apagar la capa por ello: el
supervisor es lo accesorio y la capa es lo que el usuario pidió.

## El modo seguro apaga organizar ventanas, no NubeBar

Es lo que pide el documento —"desactivar organizacion y conservar isla y paneles"— y es lo
honesto: un fallo en la capa de ventanas no es motivo para quitarle al usuario la barra de
tareas.

`HyprSalud.ModoConSeguro` se pone **por encima de todo**: encendida o no, en simulación o en
aplicar, en modo seguro el modo efectivo es `Apagado`. Las seis llamadas que leían el modo
pasan por ahí ahora.

Y es **estado de ejecución, no configuración**. Si se guardara, un fallo de una tarde
dejaría NubeHypr apagado para siempre sin que el usuario supiera por qué, y tendría que ir a
buscar la clave para volver. Reiniciando NubeBar se sale; y si el problema sigue, el
supervisor lo vuelve a poner.

**No se sale solo** aunque las señales vuelvan a estar bien: si el problema va y viene,
entraría y saldría en bucle, y cada entrada apaga la capa de golpe mientras la estás usando.

### Lo medido con `--diagd7`

| | |
|---|---|
| Atajos al entrar en modo seguro | **10 → 0** |
| Escucha de reglas | `True → False` |
| Modo efectivo con la configuración en "aplicar" | **Apagado** |
| La isla | 15 botones antes y después: **intacta** |
| Bandeja | 14 iconos, siguen |
| Atajo de la vista general (`Ctrl+Alt+W`) | sigue: no es de NubeHypr |
| Al salir | **0 → 10** atajos, reglas `True` otra vez |
| Tras soltar todo | inventario **LIMPIO** |

## El cierre no deja nada vivo, y se mide

Siete cosas se sueltan al cerrar, y antes estaban repartidas por tres archivos. La forma de
que una se quede sin soltar no es que alguien la escriba mal: es que se añada la octava y
nadie se acuerde del cierre.

Ahora están en un solo sitio (`SoltarTodoLoDeHypr`), declaradas en `HyprSalud.HayQueSoltar`,
y hay un **inventario que se mide** —no se supone— antes y después. Una prueba exige que la
lista declarada y los campos del inventario sean exactamente los mismos siete, así que el día
que haya una octava cosa, la prueba falla.

Y si queda algo, el registro lo dice **por nombre**: "quedan 2" es inútil, "atajos=2" se
arregla.

## Una aplicación que se reinicia vuelve a su sitio

El caso real: tienes el editor colocado a la izquierda, se actualiza y se reinicia solo, y
vuelve donde le dé a Windows. La memoria de D3 no sirve porque está indexada por `hwnd`, y el
de la ventana nueva es otro.

`HyprRegreso` recuerda por **ejecutable + clase de ventana**, no por título: un navegador
cambia de título con cada pestaña, y entonces cada pestaña sería otra aplicación. La clase sí
entra, porque la ventana principal de Explorer y su diálogo de copiar archivos no deberían
compartir sitio recordado.

Se apunta cuando la ventana **desaparece** (es cuando se sabe dónde estaba por última vez) y
se **gasta** al usarse: si no se gastara, cada ventana nueva del programa iría al mismo sitio
y acabarían todas apiladas.

Y **no mueve nada**: decide y devuelve la decisión. En simulación —que es lo de serie— se
apunta en el registro y se queda ahí. Misma disciplina que D5.1.

Los casos en que no se toca, cada uno con su motivo en el registro: la capa apagada, la
ventana minimizada, ya está donde estaba (±8 px), no hay nada recordado, y el que de verdad
importa:

### Un monitor que ya no está

Tenías el editor en el monitor de la derecha, lo cierras, desconectas ese monitor y lo
vuelves a abrir. La posición recordada está en coordenadas que ya no existen: la ventana se
restauraría **fuera de la pantalla**, y tendrías una ventana abierta que no puedes ver ni
alcanzar con el ratón.

Por eso un sitio recordado solo vale si sigue cayendo dentro de algún monitor, y no
"tocando": hacen falta 120 px dentro para poder cogerla por la barra de título. Asomar 20 px
no es caber.

Topes de la memoria: 2 horas de caducidad y 64 sitios. Sin ellos es una fuga, y encima
acabaría restaurando posiciones de hace una semana. Dos horas es "lo que se cerró hace un
rato", no "ayer trabajé así".

## Rescatar la configuración

`EscribirAtomico` ya escribe a un `.tmp` y hace `File.Replace`, así que una escritura
interrumpida **normalmente** no deja el config a medias. Pero "normalmente" no es "nunca": un
corte de luz entre las dos operaciones deja un `.tmp` huérfano, y un disco lleno puede dejar
el `.tmp` truncado.

Hasta D7, un config ilegible significaba arrancar **con la configuración de serie**: tema de
fábrica, módulos de fábrica, y el trabajo del usuario aparentemente perdido. El fichero
seguía ahí y los respaldos también, pero el usuario no lo sabía — lo que veía era su
escritorio en blanco.

`HyprRescate` elige el mejor candidato por orden de preferencia, y el orden es una decisión:

1. **El `.tmp` huérfano.** Es lo más nuevo que el usuario quiso guardar.
2. **El `.bak`**, la copia que deja `File.Replace` en cada guardado.
3. **Los respaldos de migración** `antes-de-vN`, del más nuevo al más viejo.

Y el juicio es más estricto que "se parsea": `{}` se parsea perfectamente y no es una
configuración. Hace falta que sea un objeto y que su `Version`, si está, sea un número que
tenga sentido.

Tres cosas que hace y una que no hace:

- Migra el rescate **en memoria**, sobre el árbol JSON, así que las claves que esta versión no
  conoce sobreviven. Hay prueba con una sección entera desconocida.
- Lo cuenta todo en el registro: qué le pasa al config, qué candidatos hay, cuál se usaría.
- Deja NubeHypr apagado: recuperarse de un fallo no puede encender nada.
- **No sobrescribe ni borra el fichero roto.** Se queda para que se pueda mirar. Un rescate
  que escribe por su cuenta puede estropear el fichero bueno.

## Dos mentiras del registro, encontradas por el propio diagnóstico

El primer `--diagd7` dejó dos líneas que no se sostenían, y las dos están corregidas:

1. **`adapter=stopped` fijo.** En D0 era la verdad porque no había motor. Desde D2 lo hay, así
   que pasó de ser honesto a ser una mentira al revés. Ahora dice `off` cuando la capa está
   apagada y `ready` cuando está montada: "apagado" y "roto" no son lo mismo.
2. **`config-version=4` y `config-version=3` del mismo fichero**, en dos líneas seguidas.
   `Linea()` usaba `Esquema.Actual` —la versión del esquema que trae el código— como respaldo
   cuando nadie le decía la del usuario. Ahora escribe `?`, que es información: significa
   "nadie me ha dicho de qué fichero hablamos".

## Resistencia

Lo que se puede medir sin ventanas de verdad, medido:

| Prueba | Resultado |
|---|---|
| 500 ventanas × 5 reglas, resueltas | bajo 3 s, y en la práctica inmediato |
| Segunda pasada sobre las mismas 500 | **0 resueltas**, bajo 500 ms: no se recalcula nada |
| Repartir 500 tramos de layout | bajo 200 ms, sin solapes |
| Memoria de regresos con 500 ventanas | se queda en 64, y conserva lo reciente |
| El supervisor con 500 ventanas | `Nada`: muchas no es lo mismo que mal |

## Pendiente de prueba manual

Lo que necesita tus manos y no se puede simular desde dentro sin tocar tu escritorio:

1. **Reiniciar NubeBar con ventanas organizadas** y ver que vuelve al modo normal.
2. **Matar y relanzar una aplicación** y ver la línea `[D7] regreso` en el registro (con la
   capa encendida en simulación).
3. **Desconectar y reconectar un monitor.** La lógica del descarte está probada; que la isla y
   los paneles se recoloquen bien, no.
4. **Cambiar el DPI o la escala y volver.**
5. **Abrir y cerrar 100 ventanas** en una sesión.
6. **Comprobar a mano que en modo seguro la isla, los paneles y la bandeja responden igual.**
   El diagnóstico mide que los botones siguen en el árbol visual; que respondan al ratón es
   otra cosa.
7. **Que no quede ningún proceso huérfano** tras cerrar (el guardián incluido).

---

# D8 — Estabilización, regresión y preparación de entrega

Sin funciones nuevas: lo de esta fase es endurecer, corregir y documentar. Lo que ha
aparecido es lo que suele aparecer cuando se prueba lo que se daba por sabido.

## El fallo que encontró D8, y era mío

**`--salir` migró la configuración real del usuario de la v3 a la v4.**

El razonamiento original para dejar `--salir` fuera de la lista de "mira y no toca" estaba
escrito y parecía bueno: *"cierra la instancia que hay, y la que se cierra sí puede querer
guardar su estado"*. Confundía dos procesos. El que guarda su estado es **el que se cierra**;
el de `--salir` solo manda la señal y se va — y en ese "se va" pasa por `OnExit` →
`CerrarIntegracionDeFondo` → `Config.Cargar()`, que ejecuta las migraciones del esquema.

Es exactamente el fallo que `Config.SoloLectura` existía para evitar, por una puerta que
nadie había mirado. Y el agravante: la NubeBar instalada del usuario es más antigua, así que
un config en v4 le da un error en cada arranque.

Lo encontré leyendo el registro de un arranque que decía `config-version=4` cuando el
fichero tenía que estar en v3. Quedó corregido (`--salir` está ahora en `BanderasQueMiran`),
hay prueba que lo exige, y la configuración del usuario está restaurada **byte a byte** desde
`config.json.antes-de-v3.json`, con los cinco respaldos intactos. La copia en v4 que llegó a
escribirse está guardada fuera del árbol, por si se quiere mirar.

Lo que hizo falta para encontrarlo no fue una idea: fue convertir la decisión en una función
pura (`Config.MiraYNoToca`) y poder preguntarle bandera por bandera.

## Lo que la regresión midió

### Las pruebas puras

**4290 comprobaciones, 0 fallos.** El total no es constante entre ejecuciones: algunos grupos
recorren las aplicaciones abiertas de la máquina.

Grupos nuevos de D8, todos sobre cosas que ya existían y no estaban probadas:

| Grupo | Qué exige |
|---|---|
| Cadena completa de migraciones | de la v1 a la v4 conservando lo del usuario **y** las claves desconocidas; e idempotencia (migrar dos veces no cambia un byte) |
| Banderas | ninguna repetida, todas empiezan por `--`, y cada constante del código está en la lista |
| Ninguna escribe el config | cada bandera comparada con la **decisión del programa**, no con el valor escrito al lado |
| Secciones de Ajustes | la página nueva de NubeHypr no pisa ningún id, subtítulo ni glifo |
| Glifos | cada punto de código está en el bloque de iconos (U+E000–U+F8FF): un número inventado no falla, sale una caja |
| NubeHypr sigue apagado | después de ocho fases, y ningún modo raro se interpreta como "aplicar" |
| Catálogo de fases | las 20 piezas de D0 a D7 están y responden |

La primera versión de la prueba de banderas comparaba un booleano consigo mismo y pasaba
siempre. Al arreglarla encontró dos cosas a la primera: que la auditoría se llama
`--ventanas` y no `--auditoria`, y la lista con un elemento de más.

### La barrida de diagnósticos

Los diez diagnósticos ejecutados seguidos sobre el binario de D8, cada uno arrancando y
cerrando NubeBar entera:

| | |
|---|---|
| Diagnósticos ejecutados | 10, todos con código de salida 0 |
| Líneas de registro producidas | 1082 |
| Errores, excepciones o avisos de "ATENCIÓN" | **0** |
| Reconstrucciones completas de la vista general | **0** (cualquier número > 0 es una anomalía) |
| Veces que se remidió el panel | **0** |
| Arrastres de la isla con la vista abierta | 0 (cualquier número > 0 es un fallo) |
| Procesos NubeBar restantes al terminar | **0** |
| Carrera de la bandeja contra Explorer | ganada, 14 iconos |
| Superficie de Inicio | `PanelShell` 660×893, raíz, opaca, 31 tarjetas visibles, sin barra blanca |

### Arranque y cierre

Del registro del último arranque: de `ejecutable:` a la isla montada con sus zonas, los
widgets registrados, el tema aplicado y el atajo global registrado — **223 ms**.

Diez ciclos de arranque y cierre en la barrida, sin un proceso huérfano y sin dejar la barra
de Windows escondida.

### Uso prolongado

`--diagbandeja 900` mantiene NubeBar entera en marcha en modo solo lectura. Muestreo de
memoria de trabajo cada minuto:

| Minuto | Memoria de trabajo | CPU acumulada |
|---|---|---|
| 0:25 | 238,8 MB | 5 s |
| 1:25 | 248,3 MB | 7 s |
| 2:25 | 245,6 MB | 8 s |
| 3:25 | 250,7 MB | 9 s |
| 4:25 | 246,3 MB | 10 s |
| 5:25 | 247,0 MB | 12 s |
| 6:25 | 254,8 MB | 13 s |
| 7:25 | 248,3 MB | 14 s |
| 8:25 | 248,5 MB | 15 s |
| 9:25 | 249,3 MB | 16 s |
| 10:25 | 254,0 MB | 17 s |
| 11:25 | 254,5 MB | 18 s |
| 12:25 | 261,2 MB | 19 s |
| 13:25 | 254,5 MB | 20 s |

**Memoria:** arranca en 239 MB y se queda oscilando entre 245 y 261 MB. No sube en escalera,
que es lo que delataría una fuga: baja varias veces (en el minuto 2, el 4, el 7 y el 13), que
es el recolector haciendo su trabajo. Trece minutos no demuestran que no haya una fuga lenta;
sí demuestran que no hay una rápida.

**CPU:** 20 segundos acumulados en 830 de reloj — un **2,4 % de un núcleo**, con la bandeja
viva, el reloj de un segundo, las métricas de CPU y RAM y el supervisor de NubeHypr
mirando cada cinco segundos.

**Durante esos 13,5 minutos:** 73 refrescos de la bandeja, **0 iconos fantasma**, 14 iconos
estables, y ni un error en el registro.

**Y el cierre, a los 900 s, por sí solo:** inventario de NubeHypr vacío (`LIMPIO`), los dos
ganchos de ventanas liberados, el hook de teclado retirado, la bandeja devuelta a Explorer,
la barra de Windows visible otra vez y **0 procesos**.

### Instalación, actualización y desinstalación

En una carpeta temporal, con el paquete real de un solo fichero (81 MB, el mismo que compila
el workflow de Release):

1. **Instalar**: copiar el ejecutable a la carpeta y ejecutarlo desde ahí — las 4290
   comprobaciones pasan desde la ruta instalada, no solo desde el árbol de compilación.
2. **Actualizar**: `--salir` y sustituir el ejecutable — se sustituye sin bloqueo de fichero.
3. **Desinstalar**: borrar la carpeta — no queda nada ni ningún proceso.

**Limitación:** Inno Setup (`ISCC.exe`) no está instalado en este equipo, así que
`NubeBar-Setup.exe` no se ha podido construir ni probar aquí. Lo construye el workflow de
Release a partir de la etiqueta. Lo que sí se ha revisado es el script: instala por usuario
en `%LOCALAPPDATA%\Programs\NubeBar` sin UAC, cierra NubeBar con su propio código (no con
AppMutex, que solo sabe plantarse) y espera un único `dist\NubeBar.exe`, que es el que
produce el comando de un solo fichero.

## Lo que queda pendiente de tus ojos

Nada de lo de abajo está marcado como verificado. La lógica de todo ello está probada; que se
vea bien, no.

### Superficies de siempre (regresión visual)

- [ ] **La nube** de la escena de entrada: que entre y salga sin saltos.
- [ ] **El lanzador / Inicio**: buscador, categorías, fijadas, recientes, clic derecho, abrir
      como administrador, propiedades y navegación con teclado.
- [ ] **El centro de control** y sus accesos rápidos.
- [ ] **Audio**: el deslizador no arrastra la isla y no se queda agarrado.
- [ ] **Red y Ethernet**.
- [ ] **La bandeja y los iconos ocultos**: que aparezca el icono de una aplicación lanzada
      después de NubeBar, sin reiniciar nada.
- [ ] **Los paneles**: abrir y cerrar rápido, Esc, clic fuera, cambio de foco.
- [ ] **Los escritorios virtuales** y el carrusel.

### Lo nuevo de D6 y D7

- [ ] **Indicador de escritorio**: "2/4 · Trabajo" con su nube, y el puntito de estado.
- [ ] **Vista general**: que el halo azul del teclado se distinga del filo de la ventana
      activa de un vistazo, y el teclado completo (flechas, Inicio, Fin, Entrar, Supr, menú).
- [ ] **El menú de una tarjeta** no tapa la tarjeta.
- [ ] **Página de NubeHypr en Ajustes**: búsqueda, grupos, el aviso de simulación y el botón
      "Calcular ahora".
- [ ] **Previsualización del ajuste**: que el filo doble se vea sobre un vídeo de Wallpaper
      Engine (medido solo contra colores fijos).
- [ ] **Modo seguro**: que la isla, los paneles y la bandeja respondan igual al ratón.

### Entornos

- [ ] Tema **claro** escrito a mano, y **alto contraste** de Windows encendido.
- [ ] Los **cuatro bordes** y la **barra vertical**.
- [ ] **DPI** al 125 % y al 150 %, y cambiarlo en caliente.
- [ ] El **segundo monitor** (aquí es vertical, 1080×1920): isla, paneles y previsualización.

### Y lo que no se puede simular desde dentro

- [ ] Reiniciar NubeBar con ventanas organizadas.
- [ ] Matar y relanzar una aplicación, y ver la línea `[D7] regreso`.
- [ ] Desconectar y reconectar un monitor.
- [ ] Abrir y cerrar 100 ventanas en una sesión.

### Qué buscar en todas ellas

Parpadeos, superficies transparentes donde debería haber fondo, glifos en caja, controles que
se quedan agarrados al ratón y cambios de tamaño que no has pedido.

## Riesgos conocidos

- **El prefijo de los atajos.** De los 19, **10 se registran y 9 los tiene cogidos otra
  aplicación** en este equipo. `Win+Alt+…` no fue una buena elección y conviene cambiarla
  antes de que alguien se acostumbre.
- **`HyprGesto` sigue sin instanciar.** Es una deuda de D3: el arrastre de verdad lo lleva la
  maquinaria de C4, que funciona. La clase está escrita y probada y no la usa nadie.
- **Dos verdades sobre "el siguiente escritorio"**: `Escritorios.Mover` y
  `HyprEspacios.Vecino` no están unificadas.
- **`NU1903`**: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 tiene una vulnerabilidad conocida. Es la
  dependencia del historial de notificaciones y viene de antes de NubeHypr.
- Las reglas **no se aplican**. Deciden y lo apuntan. Conectarlas de verdad es la fase que
  viene, y pide su propio arnés.

## Pasos de instalación (cuando se decida publicar)

Nada de esto se ha ejecutado: no hay commit, no hay etiqueta y no hay publicación.

1. **Probar a mano** la lista de arriba. Mientras quede una casilla sin marcar, esto no sale.
2. **Commit** del trabajo de D6, D7 y D8 (sigue sin hacerse, a propósito).
3. **Etiqueta** `v1.3.0`. El workflow de Release construye con la versión de la etiqueta:
   `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true
   -p:Version=1.3.0`, y después `ISCC /DAppVersion=1.3.0 /O"dist" installer\NubeBar.iss`.
4. **Comprobar que el instalador y el ejecutable salen del MISMO publish**, comparando el
   hash del `NubeBar.exe` que se sube con el que empaquetó el instalador.
5. **Instalar en limpio** en un equipo o usuario sin configuración previa, y comprobar que
   entra con el tema Nube y NubeHypr apagado.
6. **Actualizar desde la v1.2.0 instalada** y comprobar que la configuración se migra de la
   v3 a la v4 **una sola vez**, que queda su respaldo `config.json.antes-de-v3.json` y que
   las claves que la v1.2.0 no conocía siguen ahí.
7. **Desinstalar** y comprobar que no queda ni el acceso directo de arranque ni ningún
   proceso.

El instalador se instala por usuario en `%LOCALAPPDATA%\Programs\NubeBar`, sin UAC, y cierra
NubeBar con su propio código antes de tocar el ejecutable.

## Las compilaciones

Las tres carpetas de fase se han vuelto a compilar **desde el código final**, así que las
tres son el mismo binario. No son fotos de cada fase, y es a propósito: la de D7 se había
compilado antes de arreglar `--salir`, y dejar por ahí un ejecutable capaz de migrar la
configuración del usuario al pasarle `--salir` sería irresponsable. Quien quiera el binario,
cualquiera de las tres sirve; `dist-d8` es el nombre que tiene sentido usar.

| Carpeta | Qué es | SHA-256 |
|---|---|---|
| `dist-d6/`, `dist-d7/`, `dist-d8/` | `NubeBar.dll` (carpeta, no un solo fichero) | `223d6ca805c939a6b2cf01a568d09a2e1a64a6b863d4218aec550afb26c057a3` |
| `dist-d8-unico/` | `NubeBar.exe` de un solo fichero, 81 MB — el formato que empaqueta el instalador | `dc4687cf05dfb36545a218bdc56a35501fed693a0a169ba7d6959fb419e825a0` |

En las carpetas `dist-d6/d7/d8` el `NubeBar.exe` es solo el lanzador y su hash no cambia con
el código; el que identifica la compilación es el de `NubeBar.dll`.

La versión **estable v1.2.0 (`dist-nuevo/`) no se ha tocado** y es la que está en marcha.

## Verificación del arreglo de `--salir`

Medido después de corregirlo, con la barra en marcha:

```
config antes:  41033936e49e...0ca43c
config después: 41033936e49e...0ca43c   -> IGUAL
"Version": 3
01:03:07  salida pedida desde fuera (--salir)
01:03:08  --salir: cerrada por las buenas
```

Ni una línea de `esquema:`. Antes del arreglo, ese mismo comando dejaba
`esquema: migrado v3 → v4`.

---

# D8.1 — El prefijo de los atajos, medido

El riesgo que D8 dejó anotado era este: de los 19 atajos, **9 los tenía cogidos otra
aplicación**. `Win+Alt` se lo reparten el Game Bar de Xbox y Teams, y sustituirlo por otro
prefijo elegido también a ojo habría sido repetir el error con otro nombre.

## Primero, que el prefijo sea UNA constante

Las diecinueve combinaciones estaban escritas enteras, con `Win+Alt` repetido veinte veces.
**Por eso no se había cambiado en cinco fases**: cambiarlo era reescribir las veinte y no
equivocarse en ninguna. Ahora hay `HyprAtajos.Prefijo`, una tabla de teclas
(`Teclas[]`: la pulsación, el comando y si va en el segundo nivel) y `ConPrefijo(...)` que
las compone. Cambiar de prefijo es una edición.

## Después, `--diagprefijos`

Registra de verdad cada candidato con las 19 teclas, cuenta cuántos entran, dice qué pasa
con cada uno que no, y **los suelta todos** (también en `finally`, para no dejar
combinaciones del usuario cogidas por un proceso que ya se fue). No inyecta pulsaciones, no
mueve ventanas y no escribe la configuración.

### Lo medido en este equipo (Windows 11, con Game Bar, Teams, Steam y Wallpaper Engine)

| Prefijo | Segundo nivel | Entran |
|---|---|---|
| **`Win+Ctrl+Alt`** | `Win+Ctrl+Alt+Shift` | **19/19** |
| `Ctrl+Alt+Shift` | `Win+Ctrl+Alt+Shift` | 18/19 |
| `Win+Ctrl+Shift` | `Win+Ctrl+Alt+Shift` | 18/19 |
| `Ctrl+Alt` | `Win+Ctrl+Alt` | 18/19 |
| `Win+Alt` (el de antes) | `Win+Ctrl+Alt` | **11/19** |

Y con el mapa de producción, `--diagatajos`: **19 registrados, 0 ocupados, 0 problemas**, y
recargar la configuración no duplica ninguno.

`Win+Ctrl+Alt` además se lee como lo que es: el prefijo "de escritorios" de Windows
(`Win+Ctrl+flechas` cambia de escritorio) con un modificador más.

## Dos fallos míos que salieron de aquí

1. **`SegundoDe` repetía un modificador.** Con `Win+Ctrl+Shift` devolvía
   `Win+Ctrl+Shift+Ctrl`, así que las cinco combinaciones del segundo nivel colapsaban
   sobre las del primero y el mapa se quedaba en **14 comandos de 19** sin que nada
   avisara. Lo destapó el propio diagnóstico al enseñar "13/14 entran".
2. **`Replace("+A", "")` se comía el "+A" de "+Alt".** El arreglo de lo anterior usaba ese
   truco y `Win+Ctrl+Alt+Shift` salía como `Win+Ctrllt+Shift`. Lo pilló la prueba nueva en
   su primera ejecución, antes de ejecutar nada más.

Las dos están corregidas y las dos tienen prueba: todo candidato tiene que dar **19
combinaciones con 19 comandos distintos**, y el segundo nivel no puede repetir ni perder
ningún modificador.

## Y una protección que faltaba, pagada con un accidente

Un argumento que empezara por `--` y que esa versión no conociera **se caía por todos los
`if` y terminaba en el arranque normal**. O sea: pedirle a un binario un diagnóstico que
todavía no tenía montaba la barra entera y, con ella, migraba el config del usuario al
esquema de ese binario.

Me pasó dos veces en la misma sesión. La segunda fue un error de mi comando de shell: un
`grep -c` que devuelve 1 al no encontrar nada hizo que el `&&` saltara el publish, así que
`dist-d8` seguía siendo el binario anterior, no conocía `--diagprefijos` y arrancó como
NubeBar normal durante diez minutos.

Ahora una bandera desconocida **no arranca nada**: se dice en el registro, se listan las que
hay y se sale con código 2. `App.Banderas` es la lista, `Config.BanderasQueMiran` es el
subconjunto que además no puede escribir, y hay prueba de que la segunda está contenida en
la primera — para que no se pueda añadir un diagnóstico y olvidarse de protegerlo.

La configuración del usuario quedó restaurada byte a byte desde `config.json.antes-de-v3.json`
las dos veces, con los cinco respaldos intactos.

## Pendiente de tus manos

- [ ] **Pulsar unos cuantos atajos con `Win+Ctrl+Alt`.** Que Windows conceda el registro no
      significa que la combinación sea cómoda ni que tu teclado la dé con una mano. Si no lo
      es, `Ctrl+Alt+Shift` quedó a 18/19 y se cambia con una edición de una línea.
- [ ] Comprobar que `Ctrl+Alt+W` (la vista general) sigue respondiendo.

## La regla, al revés: el tercer accidente

El guardia de la bandera desconocida cayó en el mismo agujero que arreglaba. Rechaza
arrancar la barra, pero el `Shutdown(2)` pasa por `OnExit` → `CerrarIntegracionDeFondo` →
`Config.Cargar()`, y cargar la configuración ejecuta las migraciones. Tu config volvió a la
v4 por la puerta que yo acababa de poner.

Tres accidentes, uno detrás de otro, y el mismo patrón:

1. `--salir` no estaba en la lista de "mira y no toca".
2. Una bandera desconocida se caía por todas las ramas y arrancaba la barra entera.
3. El guardia nuevo salía por `OnExit`, que lee la configuración.

El agujero no era ninguno de los tres: **era la forma de la regla**. Una lista de lo que no
puede escribir hay que acordarse de ampliarla, y cualquier camino que no sea el arranque
normal acaba en `OnExit`.

Así que la pregunta correcta no es *"¿esta bandera mira?"* sino *"¿es esto la barra?"*:

```csharp
public static bool PuedeEscribir(string[]? args) =>
    args is not { Length: > 0 } ||
    string.Equals(args[0], Guardian.Bandera, StringComparison.OrdinalIgnoreCase);
```

Solo escriben dos cosas: la barra sin argumentos y el guardián. Invertida, la regla **se
mantiene sola**: un diagnóstico nuevo nace protegido sin que nadie tenga que acordarse de
nada. `Config.BanderasQueMiran` se queda, pero ya no decide: solo documenta y sirve para que
las pruebas comprueben que cada bandera se comporta como dice.

### Verificado sobre el config de verdad

Los cuatro caminos, uno detrás de otro, comparando el hash del fichero:

```
inicio:         41033936e49e25a  v3
bandera rara:   41033936e49e25a
--salir:        41033936e49e25a
--pruebas:      41033936e49e25a
--diagprefijos: 41033936e49e25a
version final:  v3   -> INTACTO
```

Y tu configuración quedó restaurada byte a byte desde `config.json.antes-de-v3.json` las
tres veces, con los cinco respaldos intactos.

---

# D9.0 — La puerta de D9: el arnés de aplicar

D9 es aplicar las reglas de verdad. Eso no se ha hecho, porque hay una instrucción expresa
del usuario: *"No actives todavía movimientos reales automáticos por reglas"*. Lo que sí se
puede cerrar antes es la pregunta que D9 tiene detrás y que ninguna prueba pura puede
contestar: **¿funciona el camino entero?**

Hasta aquí, `HyprAdaptador.Aplicar` en modo "aplicar" no se había ejecutado **nunca**. Todas
las fases fueron en simulación, y el único `SetWindowPos` real de todo NubeHypr fue el de D2
sobre una ventana elegida a mano por su hwnd. El camino reglas → decisión → efecto →
`Nativo.Colocar` → confirmar → contar estaba escrito, probado por partes y sin recorrer.

## Las condiciones, que son las de D4

El arnés de escritorios resolvió el mismo problema con las mismas reglas y funcionaron, así
que se repiten tal cual:

- **Ninguna ventana del usuario.** El arnés crea la suya, la usa y la cierra. No enumera las
  del usuario ni para leerlas.
- La regla del arnés coincide **por título**, y el título lleva la hora: no hay otra ventana
  en el equipo que pueda coincidir ni por accidente.
- **Ni teclado ni ratón sintéticos.**
- Se guarda la posición de antes y **se devuelve por el mismo camino** — no con un
  `Left`/`Top` de WPF, porque entonces no se probaría que el motor sabe devolver.
- Limpieza en `try/finally`, y la capa vuelve a su valor **antes** de cerrar la ventana: si
  el cierre fallara, lo que no puede quedar es la capa en "aplicar".
- La configuración no se toca (`Config.SoloLectura`); la capa se enciende solo en memoria.

## Lo medido

```
ventana del arnés: hwnd=0x72E0C90  nació en 120,120,420,300
capa en memoria: activo=sí modo=aplicar -> efectivo=Aplicar
área útil del monitor: 0,0,2560,1440
destino calculado: 640,360,1280,720
OK   el destino es válido
OK   y cae dentro del área útil
resultado: movidas=1 saltadas=0 viejas=0 simulado=False divergió=False
OK   NO fue una simulación: se aplicó de verdad
OK   se movió exactamente una ventana
OK   y el contador de movimientos reales subió una vez (0 -> 1)
leído de Windows después de mover: 640,360,1280,720
OK   la ventana está donde se le pidió (±10 px)
OK   y ya no está donde nació
OK   el adaptador no reportó divergencia
OK   un movimiento que sale bien borra la racha de fallos
leído al final: 120,120,420,300
OK   ha vuelto a donde nació
capa devuelta: activo=False modo=simulacion (el fichero no se tocó)
ventana del arnés cerrada: ¿sigue existiendo?=no
movimientos reales de todo el arnés: 2 (uno para colocar, uno para devolver)
limpieza=COMPLETA
```

**Al píxel**, ida y vuelta. Y el contador de `HyprAdaptador.VecesMovida` pasó de 0 a 2: dos
movimientos reales en toda la vida del proceso, los dos del arnés.

Se comprueba además lo que D7 dejó a medias: que aplicar alimenta los contadores del
supervisor. Si no lo hiciera, el supervisor nunca sabría que algo va mal al mover.

## Lo que esto NO es

No es D9. **Las reglas siguen sin aplicarse a las ventanas del usuario**: nada se ha
conectado, el modo de serie sigue siendo simulación y la capa sigue naciendo apagada. Lo que
este arnés demuestra es que, el día que se encienda, el camino por el que pasará está
recorrido y confirmado.

## Lo que falta para D9, cuando se decida

1. **Permiso explícito** para que las reglas muevan ventanas de verdad.
2. Decidir qué pasa con una ventana que la regla coloca y el usuario mueve a mano después:
   ¿se respeta su gesto o la regla vuelve a colocarla? (La respuesta correcta es respetar el
   gesto, y eso necesita recordar qué ventanas ha tocado el usuario.)
3. Un tope de ritmo: una aplicación que abre seis ventanas en dos segundos no puede
   convertirse en seis `SetWindowPos` encadenados.
4. Y un "deshacer lo último": si una regla coloca mal diez ventanas, tiene que haber una
   tecla que las devuelva. `HyprRestaurar` ya guarda el estado anterior; falta la tecla.

---

# D8.2 — Las pruebas visuales, hechas mirando

El usuario pidió que hiciera yo las pruebas «si puedo». Pulsar atajos no puedo: eso es
inyección de teclado y está prohibido. Pero **mirar sí**, y resulta que se puede de verdad:
`Nativo.Foto` (`PrintWindow` con PW_RENDERFULLCONTENT) ya existía para las miniaturas de la
vista general, y sirve igual para retratar cada superficie y luego abrir el PNG.

## `--capturas`

Abre cada superficie por el camino del usuario —Inicio por la vía de la tecla Windows, los
paneles por sus botones— espera a que el diseño asiente y la fotografía. Dieciséis imágenes
en una carpeta temporal, con su índice. No mueve ventanas, no toca escritorios y no escribe
la configuración.

Dos detalles que costaron una tanda cada uno:

- Las dos primeras capturas salían con **la isla a medio montar**, sin su zona derecha. No
  era un fallo del producto: era mi diagnóstico disparando a los 2,5 s. Ahora espera 6.
- Las tarjetas de la vista general salían **sin foto**: las miniaturas se piden en segundo
  plano y tardan más que la captura. Ahora espera 3,5 s.

Los dos son el mismo error por mi parte: **confundir una carrera con un defecto**. Lo
anoto porque es exactamente lo que un informe de pruebas no debe hacer.

Y una pieza no salía en ninguna foto porque este equipo no la tiene puesta: el módulo
«escritorios». Se añade **solo en memoria** durante la captura y se quita después.

## Lo verificado mirando

| Superficie | Qué se ve |
|---|---|
| **Indicador de escritorio** (D6) | nube + «1/2», el actual en azul con su punto de estado, el otro atenuado y sin punto, y el `+`. Funciona |
| **Vista general** (D6) | el filo de la activa y el **halo** de la elegida, a la vez y en tarjetas distintas; las minimizadas atenuadas **con el título legible** |
| **Previsualización** (D6) | filo doble —línea oscura fuera, azul dentro— y la chapa del rótulo sobre fondo propio |
| **Ajustes → NubeHypr** (D6) | nube en la navegación, estado en azul, aviso de simulación con su filo, buscador, tarjeta de reglas |
| **Inicio** | saludo, reloj, buscador, tarjeta de multimedia, acciones rápidas, CPU/RAM con sus gráficas, fondo y conectividad. Opaco, sin escritorio detrás |
| Audio, Red, Batería, Bandeja, Notificaciones | abren y se dibujan |
| Ajustes: Apariencia, Colores, Módulos, Atajos | se dibujan |
| **Glifos** | ni una caja en ninguna de las dieciséis |

## Dos fallos que solo se veían mirando

### 1. Ajustes enseñaba un atajo que no era el tuyo

La página leía `AtajoVistaGeneral` del árbol con **`"Ctrl+Alt+Space"`** de respaldo —el valor
viejo— mientras el modelo tipado usaba **`"Ctrl+Alt+W"`**. Con la clave ausente del fichero,
que es el caso de cualquier instalación que no la haya tocado, el campo enseñaba
`Ctrl+Alt+Space` con un error en rojo debajo, y el atajo que de verdad estaba registrado y
funcionando era otro.

Lo grave no era enseñarlo mal: **pulsar Guardar habría escrito el valor equivocado encima de
un atajo que funcionaba**, dejando al usuario sin vista general.

Arreglado con una constante única, `Barra.AtajoDeSerie`, usada en los cuatro sitios. Y con
una prueba que exige que el modelo y el árbol digan lo mismo cuando la clave no está.

### 2. Y después decía que tu propio atajo estaba cogido

Con el valor ya correcto, el mensaje en rojo seguía: «Esa combinación ya la tiene cogida otra
aplicación». Y era verdad a medias — la tenía cogida NubeBar.

`RegisterHotKey` devuelve 1409 igual si el atajo lo tiene otra aplicación que si lo tiene el
proceso que pregunta, y Ajustes vive en el proceso de la isla, que lo registró al arrancar.
Así que la página marcaba en rojo el atajo del usuario, que estaba funcionando.

Ahora se suelta el nuestro antes de preguntar y se vuelve a poner en un `finally`. La isla se
queda sin él unos microsegundos. Verificado en una captura nueva: **«La combinación guardada
está libre»**, en verde.

## Lo que una imagen quieta NO puede contestar

Y por tanto sigue pendiente de tus ojos y tus manos:

- Parpadeos al abrir y cerrar paneles rápido.
- Controles que se quedan agarrados al ratón.
- Animaciones descompasadas del estado real.
- Pulsar los atajos (no puedo: es inyección de teclado).
- Tema claro y alto contraste de Windows.
- Los cuatro bordes y la barra vertical.
- DPI al 125 % y al 150 %.
- Desconectar y reconectar un monitor.

---

# D9.1 — Las tres redes que tienen que existir antes de aplicar

En el informe anterior dije qué faltaba para poder encender D9, y esto es eso, construido.
Las tres son lógica pura con el reloj inyectado, así que se comprueban sin esperar, sin
abrir ventanas y sin mover nada — y después se ejercitan contra Windows en el arnés.

## 1. Tu gesto manda — `HyprRespeto`

Es lo primero que rompe un gestor de ventanas automático y lo que hace que la gente los
desinstale el primer día: colocas una ventana donde la quieres, la regla te la devuelve a su
sitio, y la pelea la gana siempre el programa.

La regla aquí es la contraria: **una ventana que has tocado a mano queda marcada y las reglas
dejan de opinar sobre ella** hasta que se cierre.

**Cómo se sabe que la moviste tú:** por eliminación, que es la única forma honesta. NubeHypr
sabe exactamente dónde pidió que estuviera cada ventana que colocó. Si aparece en otro sitio,
ese cambio no es nuestro. Puede ser tuyo o de la propia aplicación —un editor que recuerda su
tamaño, un reproductor que se redimensiona al cargar un vídeo— y las dos merecen la misma
respuesta: **no insistir**. No se intenta distinguirlas: no se puede sin espiar el ratón, y
acertar a medias sería peor que la regla simple.

La tolerancia es 8 px, la misma que usa `Confirmar`. Si fuera igualdad exacta, nuestro propio
movimiento se leería como un gesto tuyo y **el motor se desactivaría a sí mismo en la primera
ventana que colocara**. Hay prueba de las dos direcciones.

**No caduca**, a propósito: si caducara, la regla volvería a colocarte la ventana al rato y
vivirías la misma pelea, solo más espaciada —y encima impredecible—. Soltar la marca es un
acto explícito. Y las marcas de ventanas cerradas se sueltan, porque un `hwnd` que Windows
reutilice nacería marcado.

## 2. Un tope de ritmo — `HyprRitmo`

Abres un proyecto y el editor abre seis ventanas en dos segundos. Sin tope, son seis
`SetWindowPos` inmediatos: la pantalla baila, el equipo se atasca justo en el momento de más
trabajo, y si algo va mal son diez movimientos equivocados en vez de uno.

Dos límites, porque uno solo no cubre los dos casos:

| Límite | Valor | Qué corta |
|---|---|---|
| Por ventana | 500 ms | el bucle: una app que se recoloca sola y la regla que insiste |
| En total | 4 por segundo | la ráfaga del editor que abre seis |
| De seguridad | 60 por minuto | **se deja de aplicar**: eso ya no es una ráfaga, es algo peleándose |

Preguntar **no gasta cupo**: si lo gastara, dos consultas seguidas bloquearían una ventana que
no se ha movido nunca.

## 3. Deshacer lo último — `HyprDeshacer`

Es un requisito, no una comodidad. El día que las reglas coloquen de verdad, la primera vez
que una esté mal escrita va a recolocar diez ventanas a la vez. Sin vuelta atrás, el usuario
recoloca diez ventanas a mano mientras la regla se lo deshace. Con una tecla, es un susto de
dos segundos.

Guarda los **cinco últimos lotes** (no uno: una ráfaga puede producir tres seguidos y deshacer
solo el último te deja a medias) con **cinco minutos** de caducidad (pasado ese rato, "lo de
antes" ya no es lo de antes). Lo que estaba maximizado se **restaura**, no se coloca con su
rectángulo — colocarlo lo dejaría del tamaño de antes de maximizar pero sin estar maximizado.
Y las ventanas que ya se cerraron se filtran: devolver una ventana cerrada es nada, y hacerlo
saltar convertiría "una se cerró" en "no se puede deshacer".

## Conectado, observando y sin mover

El detector de gestos está **enganchado de verdad**: el manejador que ya escuchaba los cambios
de lista ahora escucha también los de sitio, y compara rectángulos — no recalcula reglas, que
sería el camino al parpadeo de C4.1.

Y las tres cosas que hay que mirar cuando cambian las ventanas —las que se van, las que llegan
y las que se mueven— se hacen en **un solo recorrido**, con la observación ya hecha. El orden
importa: comparar antes de actualizar `_ultimoSitio`. Hacerlo al revés sería comparar cada
ventana consigo misma y no detectar un gesto jamás.

## Lo medido en el arnés, contra Windows

```
OK   ritmo: la primera colocación se permite
OK   ritmo: 80 ms después se corta (DemasiadoPronto)
     ritmo hwnd=0x17C0C8A: esta ventana se colocó hace 80 ms (hacen falta 500)
OK   ritmo: pasada la espera, vuelve a permitirse
OK   respeto: antes del gesto, las reglas pueden colocarla
     gesto hwnd=0x17C0C8A de=DeFuera antes=640,360,1280,720 ahora=840,480,1280,720
     -> marcada: las reglas la dejan en paz
OK   respeto: el movimiento a mano se lee como de fuera
OK   respeto: y a partir de ahí las reglas no la tocan
OK   respeto: lo que colocamos nosotros NO se lee como un gesto tuyo
     deshacer 1 ventana(s) de «el arnés de aplicar» -> 1 efecto(s)
OK   deshacer: un lote con un efecto
OK   deshacer: el lote se consume, no se repite
```

Todo sobre la ventana del arnés. Ninguna tuya se tocó.

## El inventario de cierre pasa de siete a nueve

Las dos memorias nuevas entran. No atrapan nada del sistema —son diccionarios— pero entran por
la misma razón que las otras: lo que no se mide, no se sabe, y un inventario que dice "limpio"
sin mirarlas mentiría. La prueba que exige que la lista declarada y los campos del inventario
coincidan sigue ahí, ahora en nueve.

## Lo que sigue faltando para D9

1. **Tu permiso.** Sigue siendo lo primero.
2. Un **atajo** para deshacer. La lógica está; falta el comando número 20 y su tecla, y eso
   toca las pruebas que hoy exigen 19 — es una decisión, no un descuido.
3. Conectar las tres redes al camino de aplicar de verdad, que hoy solo recorre el arnés.

---

# D9.2 — La puerta única, y el comando número 20

## El problema de tener redes que nadie consulta

D9.1 dejó tres redes construidas y probadas —respetar tu gesto, el tope de ritmo, el
deshacer— **y no las miraba nadie**. Un cinturón de seguridad en el maletero.

El error fácil habría sido consultarlas en cada sitio que mueve algo: el atajo, la regla, el
gesto de la vista general, el arnés. Cuatro sitios que hay que acordarse de actualizar, y el
día que aparezca el quinto se moverían ventanas sin pasar por ninguna red.

Así que hay **un solo camino**: nadie llama a `HyprAdaptador.Aplicar` directamente. Se pasa
por `AplicarConRedes`, que consulta, apunta lo que había y delega. El arnés también —si el
arnés tomara un atajo, estaría probando un camino que el producto no usa.

## El fallo de diseño que destapó una prueba

La primera versión del tope de ritmo contaba **ventanas**. Escribí la prueba «de diez de
golpe no pasan las diez», la ejecuté, y pasaron las diez. Al mirar por qué, el fallo no era
el que yo creía que estaba probando:

**Un reparto en mosaico de diez ventanas es UNA decisión.** Con el tope contando ventanas,
ese reparto se habría quedado en cuatro colocadas y seis donde estaban. Media pantalla
ordenada y la otra media no, sin forma de saber por qué. **Medio mosaico es peor que
ninguno.**

La unidad correcta del tope en total es la **tanda**: un reparto, una regla que dispara, un
atajo. Seis ventanas que abre un editor son seis tandas; diez ventanas de un mosaico son
una. Lo que sigue siendo por ventana es la espera de medio segundo, que protege de otra cosa
distinta: la pelea entre una aplicación que se recoloca sola y una regla que insiste.

Ahora son dos preguntas, `PreguntaLote` y `PreguntaVentana`, y hay una prueba que exige que
una tanda de diez pase entera — para que no se pueda volver al diseño de antes sin que algo
falle.

## El orden de las comprobaciones, y por qué

1. **¿Cabe la tanda?** Una vez, antes de mirar ventana por ventana. Si no cabe, se descartan
   todas con el mismo motivo: nadie se queda a medias y el registro dice una razón en vez de
   diez iguales.
2. **Lo que no mueve, pasa sin preguntar.** Enfocar una ventana no es moverla; si contara,
   darle el foco la dejaría «recién movida» para el ritmo.
3. **El respeto antes que la espera.** Una ventana que tocaste no se mueve hoy ni dentro de
   una hora, así que preguntarle al ritmo por ella sería gastar cupo en algo que no va a
   pasar nunca.

Y después de aplicar, dos apuntes. El del ritmo (una tanda, un apunte — si fuese uno por
ventana, un reparto de diez se comería el cupo del segundo él solo) y el del respeto: **qué
se pidió para cada ventana**. Sin ese segundo, el propio movimiento se leería luego como un
gesto tuyo y el motor se desactivaría solo en la primera ventana que colocara.

## El comando número 20: deshacer

Romper el «son 19» fue deliberado. Sin una tecla que deshaga, encender el modo «aplicar»
significa que la primera regla mal escrita te recoloca diez ventanas y te deja
recolocándolas a mano mientras la regla te las vuelve a mover.

`Win+Ctrl+Alt+Z`, en el primer nivel, donde la gente busca un deshacer sin pensarlo. El mapa
de serie pasa a 20 combinaciones y las pruebas que decían 19 dicen 20 — a mano, para que el
cambio sea una decisión y no un número que se ajusta solo.

**Deshacer no pasa por las redes**, y es a propósito: el ritmo cortaría justamente lo que
acabas de pedir (deshacer viene siempre pegado a lo que se quiere deshacer) y el respeto
descartaría las ventanas cuya marca se acaba de soltar. Deshacer es una orden explícita; las
redes están para lo automático.

## Lo medido en el arnés

```
[D9]  aplicar «arnés de aplicar»: 1 efecto(s), ninguno descartado, modo=aplicar
[D9]  apuntado para deshacer: 1 ventana(s) de «arnés de aplicar»
[D9]  resultado: movidas=1 saltadas=0 simulado=False divergió=False
      leído de Windows: 640,360,1280,720  <- al píxel
OK    ritmo: la primera tanda se permite / y esta ventana también
OK    ritmo: 80 ms después se corta (DemasiadoPronto)
OK    respeto: el gesto a mano se lee como de fuera -> marcada
OK    respeto: lo que colocamos nosotros NO se lee como un gesto tuyo
OK    deshacer: un lote con un efecto, y se consume
```

## Y lo que sigue sin pasar

Las reglas **no mueven tus ventanas**. La capa nace apagada, el modo de serie es simulación,
y el único sitio donde el camino de aplicar se recorre entero es el arnés, sobre su propia
ventana. Lo que D9.2 cambia es que, el día que se encienda, ya no hay forma de mover una
ventana sin pasar por las tres redes.

Para D9 de verdad sigue faltando lo mismo: **tu permiso**.

---

# D9.3 — El editor de reglas

D6 dejó el panel: se podían ver, buscar y agrupar. **Crear una exigía abrir `config.json` y
escribir un objeto a mano**, acertando con los nombres de los campos. Eso no es una función
a medias: es una función que solo puede usar quien escribió el formato.

## Dos decisiones del diseño

**Se edita una copia.** Lo que se toca vive en memoria hasta que pulsas Guardar. Escribir
cada tecla en el árbol significaría que cancelar no cancela nada y que una regla a medio
escribir queda guardada si cierras la ventana.

**Se dice a cuántas ventanas afecta, en el momento.** Es la pieza que lo hace usable: el
error más común al escribir una regla es que no coincida con nada —«chrome» cuando el
proceso se llama «chrome.exe», o al revés— y sin esto no te enteras hasta que abres una
ventana y no pasa nada. El contador usa el **mismo `Coincide`** que el motor: si se
recalculara aquí, el panel diría una cosa y el motor haría otra.

## Los campos, y por qué tres estados

Criterios arriba («a qué ventanas»), acciones abajo («qué hacer con ellas»). El orden no es
estético: es el orden en que se piensa una regla, y poner las acciones primero invita a
escribir una regla que hace algo sin saber todavía a quién.

Flotante y Foco no son casillas, son **tres opciones**: Sí, No y **No opina**. Una casilla no
puede decir «no opino», y esa es justamente la diferencia entre «esta regla la pone flotante»
y «esta regla no habla de flotar» — que es lo que deja decidir a otra regla.

Los números se escriben con coma o con punto, los dos: en español se escribe 0,5 y en el
fichero vive como 0.5, y rechazar una de las dos formas sería rechazar a medio mundo. Lo que
no es un número se descarta y el campo vuelve a lo que había.

Y los campos escriben **al perder el foco**, no en cada tecla: letra a letra, «fire» quedaría
guardado un instante como una regla distinta de «firefox» y el contador de coincidencias
parpadearía con cada pulsación.

Una regla nueva nace **apagada y sin criterios**. Apagada porque está a medias por
definición, y encenderla sola significaría que pulsar «Añadir» ya decide sobre tus ventanas.

## Escribir sin perder nada

`ConfigArbol.PonerReglas` sustituye **solo** la clave `Reglas`. Las demás de la sección
—incluidas las que esta versión no conozca— se quedan. Y cada regla se escribe sin sus
nulos: un `"Clase": null` se lee igual que si no estuviera, pero al abrir el fichero a mano
parece una decisión en vez de un hueco.

Borrar la última regla deja `"Reglas": []`, no quita la clave: un hueco donde había una lista
es una invitación a que algo la dé por no migrada. Hay prueba de las dos cosas, sobre un
fichero temporal con una clave del futuro dentro.

## Un fallo que encontró la captura

El editor se ve bien, pero la foto enseñaba una regla con **ancho 0,5** cuyo resumen decía
**`tamaño=1x0`**. Dos fallos en la misma línea:

- El formato era `0`, que redondea: **media pantalla se enseñaba como una pantalla entera**.
  Un ancho es una fracción y una fracción necesita decimales.
- Y un alto que **nadie había puesto** salía como `0`, que no es lo que significa: cero sería
  una ventana sin altura. Lo que hay es un hueco, y un hueco se escribe «—».

Estaba repetido en tres sitios y ahora se escribe en uno (`HyprReglas.Tamano`), con prueba.
Verificado mirando otra vez: **`tamaño=0.5x—`**.

## Pendiente de tus manos

El editor está probado en lógica y visto en una captura. Lo que falta es usarlo con el ratón:
escribir en los campos, ver cambiar el contador de coincidencias, apagar, quitar, añadir,
cancelar y comprobar que cancelar no guarda nada.

---

# D9.4 — El atajo pasa por la puerta

Hasta aquí el portón de D9.2 estaba montado, probado… y no lo cruzaba ningún atajo. Pulsar
`Win+Ctrl+Alt+→` calculaba los efectos, los escribía en el registro y ahí se quedaban. Era lo
correcto mientras no había puerta —aplicar desde un manejador de teclas, sin respeto a tu gesto
ni tope de ritmo ni forma de deshacer, es justo lo que D9.1 existe para impedir— pero con la
puerta puesta era un cinturón en el maletero.

Ahora `AtajoDeHypr` llama a `AplicarConRedes` como todo lo demás, con el motivo
`atajo «…»` en el registro.

## Lo que NO cambia: quién decide si se mueve algo

El modo. En **simulación** —que es lo de serie, y lo que tiene la configuración real— la puerta
calcula, consulta las tres redes, apunta y **no toca una sola ventana**. Pulsar una tecla no es
la puerta trasera por la que se activa nada: para que un atajo mueva ventanas hay que poner el
modo en `aplicar` a mano, en el fichero de configuración.

Dos cosas que convenía comprobar y están comprobadas:

- En simulación **no se gasta cupo de ritmo**. `HyprAplicador.Filtrar` solo *lee* los
  contadores; quien los escribe es `Salio`, y `Salio` solo se llama en modo `aplicar`. Si no
  fuera así, un rato probando atajos en simulación dejaría al tope de ritmo creyendo que acaba
  de mover veinte ventanas.
- En simulación **no se apunta nada para deshacer**, por lo mismo: no hay nada que deshacer.

## Propuesto y salido son dos números

El motor puede proponer cuatro ventanas y el respeto a tu gesto dejar pasar una, porque las
otras tres las colocaste tú. Decir «4 ventanas» sería un número que se desmiente mirando la
pantalla, así que el indicador cuenta **lo que salió por la puerta**.

Y el caso en que no sale ninguna necesita su propia frase. «No había nada que hacer» sería
falso —había cuatro— y dejaría al usuario pensando que el comando está roto, cuando lo que pasa
es que NubeHypr está respetando lo que él hizo a mano. Sale: **«esas ventanas las has colocado
tú»**.

La decisión vive en `HyprAviso.DelComandoAplicado`, que es puro y tiene pruebas, no en el
manejador de teclas.

## El estado, en Ajustes, dice lo que hace

«Aplicando» en coral avisaba de que hay cuidado, pero no de **qué** cuidado. Ahora bajo el
estado hay una frase que lo dice en los tres casos, y la de `aplicar` distingue las dos mitades
que de verdad están en sitios distintos: *«Los atajos mueven ventanas de verdad. Las reglas,
todavía no.»*

## Pendiente de tus manos

1. **Pulsar los atajos en simulación** y comprobar en el registro que la puerta se cruza
   (`atajo «…»`) y que no se mueve nada en pantalla.
2. **Si alguna vez pones el modo en `aplicar`**: pulsar un atajo, ver moverse las ventanas, y
   `Win+Ctrl+Alt+Shift+Z` para deshacerlo. Eso sigue sin probarse fuera del arnés.
3. Mover una ventana a mano, pulsar el atajo y comprobar que sale **«esas ventanas las has
   colocado tú»** en vez de moverla.

---

# D6.1 — La barra vertical, vista

D6 dejó los cuatro bordes y las dos orientaciones **probados en lógica**: que el menú cae
dentro del área útil, que el indicador elige la talla que cabe. Lo que nadie había mirado es
cómo se ve. Con la barra vertical los paneles no se despliegan dentro de la isla —no tiene
hacia dónde crecer—: salen **flotando al lado**, por otra ventana y otro camino de código.

`--capturas` ahora gira la barra **en memoria** (`barra` + `izquierda`), retrata la barra, el
indicador breve y la vista general, y la devuelve en un `finally`: si el diagnóstico revienta,
la barra no se queda de lado. El fichero no se toca.

## Lo que se vio: todo más claro de lo que debía

La vista general con la barra vertical salía con las tarjetas sobre un fondo casi blanco, con
el tema negro puesto. Medido: una tarjeta minimizada daba `#BDC0E9` en vertical contra
`#646570` en horizontal.

El camino hasta encontrarlo tiene dos lecciones y una equivocación mía:

1. **Primera hipótesis, equivocada:** el fondo de la tarjeta flotante usaba `_fondo`, que trae
   alfa (`#E6000000`). Lo puse opaco… y **los píxeles no se movieron ni uno**. Esa es la
   prueba de que la hipótesis era falsa, y por eso se revirtió: la tarjeta flotante hace de
   isla cuando la barra es vertical, y la isla es translúcida a propósito.
2. **¿Y si mentía la captura?** `PrintWindow` le pide a la ventana que se redibuje, que no es
   lo que ve un ojo. Se añadió `Nativo.FotoDeLaPantalla` (BitBlt con `CAPTUREBLT`) y se
   retrató lo mismo por los dos caminos: **idénticos**. La captura no mentía — lo cual importa
   más allá de este fallo, porque es la herramienta con la que se ha verificado todo D6.
3. **Lo que faltaba por mirar era lo de EN MEDIO.** Un volcado de la cadena de capas por
   encima de una tarjeta, en las dos orientaciones, puso el dedo encima:

   ```
   horizontal: Border[tarjeta] < Grid[#00FFFFFF]      < Button[#FFDDDDDD] < UniformGrid < …
   vertical:   Border[tarjeta] < Border[#FFDDDDDD]    < Button[#FFDDDDDD] < UniformGrid < …
   ```

   El mismo botón, dos plantillas. `#FFDDDDDD` no está escrito en ninguna parte del proyecto:
   es la plantilla **de serie de WPF**, que pinta el `Background` del control en un `Border`.

## La causa: los estilos no cruzan de ventana

`Resources[typeof(Button)] = EstiloBoton()` se instala en `MainWindow`. Los estilos implícitos
se buscan subiendo por el árbol hasta **la ventana que contiene al elemento** y de ahí a la
aplicación; nunca pasan por MainWindow. La ventana superpuesta no tenía recursos propios, así
que todos sus botones caían a la plantilla de serie — y lo mismo le pasaba al recurso `Hover`.

No afectaba solo a la vista general: afectaba a **todos los botones de todos los paneles**
cuando la barra está en vertical.

El arreglo es una línea, y va en el **constructor** de `VentanaSuperpuesta`, no donde se viste
el panel: esa clase se usa para dos cosas —los paneles al lado y la previsualización del
ajuste— y la previsualización no pasa por ahí. Un arreglo que cubra uno de los dos caminos es
medio arreglo, y el que faltaría volvería a aparecer el día que la previsualización lleve un
control con estilo.

Se comparte el **mismo** diccionario, no una copia, porque `AplicarTema` lo reescribe en cada
cambio de tema y de nivel de animación y una copia se quedaría atrás.

### Dónde más podía pasar, comprobado

La misma pregunta para el resto de ventanas propias:

- **`VentanaAjustes`** — sana. Sus cuatro botones sin plantilla propia fijan `Background`
  explícitamente, que es lo que la plantilla de serie pinta; y los de navegación traen su
  `Template`. Por eso las capturas de Ajustes siempre salieron bien.
- **`EscenaArranque`** — sana: no usa botones ni recursos dinámicos.
- **La previsualización del ajuste** — es una `VentanaSuperpuesta`, así que estaba expuesta al
  mismo fallo aunque hoy no lleve ningún control con estilo. Queda cubierta por el arreglo del
  constructor.

## Y la sonda se queda, convertida en comprobación

El volcado de capas no se borra: se ejecuta en las dos orientaciones y **las compara solo**,
hasta la rejilla —lo que vive dentro del panel—, porque de ahí hacia arriba los recipientes son
distintos a propósito. Si vuelven a separarse, `--capturas` lo dice con un `¡OJO!` en vez de
dejar una imagen rara que haya que mirar con suerte.

## Pendiente de tus manos

Con la barra puesta en vertical de verdad (no en memoria): abrir los paneles, pulsar botones y
comprobar el resaltado al pasar el ratón, que es lo otro que dependía del recurso `Hover`.

---

# Pasada de regresión (26 oct)

Con la barra parada, los diez diagnósticos de solo lectura y los dos arneses, seguidos:

| | |
|---|---|
| `--diaghypr` `--diagmotor` `--diagespacios` `--diagatajos` `--diagreglas` | 0 errores |
| `--diaginicio` `--diagbandeja` `--diagd6` `--diagd7` `--diagprefijos` | 0 errores |
| `--arnesaplicar` | aplica, verifica píxel a píxel, ejercita las tres redes, devuelve la ventana a donde nació (`120,120,420,300` contra `120,120,420,300`) y `limpieza=COMPLETA` |
| `--arnesespacios` | crea, renombra, reordena, envía una ventana, borra — y deja tus dos escritorios iguales y en el mismo orden, contigo en el de siempre |
| `config.json` | byte a byte idéntico antes y después de las doce ejecuciones |

Un aviso sobre cómo se lee esto: al filtrar la salida con un `grep` por «error|excepción»,
`--diagd7` parecía dar dos. No daba ninguno: las líneas que casaban eran
`totalExcepciones=0` y `5 excepciones seguidas -> ModoSeguro`, que son **etiquetas de lo que
el diagnóstico comprueba**, no fallos. Conviene mirar la línea antes de contarla.

---

# D6.2 — Los cuatro bordes y el alto contraste, vistos

Quedaban dos puntos de D6 marcados como «probados en lógica, no vistos». Ya están vistos.

## Los ocho casos de borde

No son cuatro, son ocho, porque el **modo** cambia lo que significa el borde: con `isla` en un
lateral la isla se **ancla** a ese lado y sigue horizontal; con `barra` en un lateral es una
barra **vertical** de verdad. `--capturas` recorre los ocho en memoria y los devuelve al final.

Medido en cada uno, además de la foto, porque una isla negra sobre un escritorio negro no
distingue «barra de lado a lado» de «isla centrada»:

| | `EsIsla` | `Vertical` | ventana | isla |
|---|---|---|---|---|
| isla / los cuatro bordes | sí | no | 2560×54 | 352×38 |
| barra / arriba y abajo | no | no | 2560×54 | **2544×38** |
| barra / izquierda y derecha | no | **sí** | 54×1440 | 38×1424 |

### Un fallo del diagnóstico que casi cuento como fallo del producto

En la primera pasada, `barra/arriba` y `barra/abajo` salían como una isla centrada:
`EsIsla=True` con el modo puesto a `barra`. Parecía que el modo no se aplicaba.

No era el producto. `Barra.Modo` **no es otra opción**: es un derivado del panel que
`Config.Cargar` recalcula al leer el fichero («ahora al revés que antes: Barra se deriva del
panel»). El diagnóstico cambiaba el panel en memoria, que es lo que lee `Vertical` —por eso la
barra sí giraba—, pero no `Barra.Modo`, que es lo que lee `EsIsla`. Cuando el usuario lo cambia
en Ajustes se guarda y se recarga, y al recargar se deriva: ahí no hay medias tintas.

Lo apunto porque es el tipo de cosa que se cuenta mal con facilidad: *una herramienta de
diagnóstico que aplica la mitad de un cambio produce síntomas idénticos a los de un fallo de
verdad*. La forma de distinguirlos fue imprimir los números, no mirar la foto.

## El alto contraste, con su límite dicho

`HyprPinta.Forzado = true` enciende la rama de alto contraste **de NubeHypr** —velos opacos,
filo más grueso, sin relleno— sin tocar la configuración de Windows. Eso es lo que se
fotografía, y se ve la diferencia: la previsualización normal tiene relleno azulado y la de
alto contraste **no tiene relleno** y el filo es más gordo.

Lo que esto **no** es: Windows en modo alto contraste. Eso cambia los colores de todo el
sistema y hay que encenderlo en la configuración de Windows, que no se toca. Así que de las dos
mitades, nuestra rama queda verificada y la del sistema sigue siendo prueba manual. El
`finally` devuelve `Forzado` a `null` pase lo que pase: si se quedara encendido, el resto de
capturas de la tanda mentirían sin avisar.

## Pendiente de tus manos

1. Encender el alto contraste **de Windows** y mirar la isla, los paneles y la vista general.
2. Con la barra en vertical de verdad, el resaltado al pasar el ratón por los botones.
3. Los bordes con el ratón: que la isla se asome y se recoja bien en abajo, izquierda y
   derecha, que es lo que una imagen quieta no enseña.

---

# D6.3 — Un tema claro, medido

El último punto de D6 que seguía sin ver: **los siete temas de serie son oscuros**. Azul y
coral sobre un fondo claro es otra rama de `Roles` —la que oscurece la tinta para que se lea—
probada en lógica y nunca vista.

El tema claro se inventa para la captura, con la paleta clara del propio documento (`#F4F7FA`
de fondo, `#172033` de texto). **No es un tema tuyo**: que funcione con éste no demuestra que
funcione con el tuyo. Lo que demuestra es que la rama clara dibuja, y con cuánto contraste.

Y por eso lo que importa aquí no son las fotos, son los números:

| sobre fondo claro `#F4F7FA` | |
|---|---|
| azul **crudo** contra el fondo | 2,08:1 |
| azul **como tinta** sobre superficie | **4,72:1** (mínimo 4,5) |
| coral crudo contra el fondo | 3,96:1 |
| coral **como tinta** sobre superficie | **4,56:1** (mínimo 4,5) |
| filo sobre blanco / negro / gris | 21,00 · 21,00 · 3,95 (mínimo 2,2) |

Esa es la pieza funcionando: el azul crudo da 2,08:1, que sería ilegible como texto, y
`TintaSobre` lo oscurece hasta 4,72:1 cuando hace de letra. El margen es fino —4,72 y 4,56
contra un mínimo de 4,5— porque el algoritmo apunta justo al mínimo, no por suerte.

## Una observación, no un fallo

Con un acento coral, el filo de la ventana **activa** sale coral, porque ese filo usa el acento
del tema y no la paleta de NubeHypr. Visto de golpe choca con la regla de D6 («azul para foco,
coral para avisos»), pero esa regla es de las superficies de NubeHypr —el halo de la elegida,
el filo del ajuste—, que siguen usando `HyprPinta.Azul`/`Coral` con sus tonos fijos. El filo de
la activa es de la barra y es anterior. Si alguna vez eliges un acento coralino, conviene
saber que las dos cosas se parecerán.

## Y una captura que enseña de más

La previsualización del ajuste tiene el relleno semitransparente a propósito (alfa 0x30, para
que se siga viendo la ventana de debajo). `PrintWindow` la dibuja **sobre nada**, así que esa
foto enseñaba el relleno sobre negro, que no es lo que ve nadie. Ahora se retrata también con
`FotoEnPantalla`, y ahí sí: se ve el filo doble, la chapa legible y la ventana de debajo
entera a través del relleno.

Con una consecuencia que hay que decir: **copiar de la pantalla copia lo que haya en la
pantalla**. La primera vez salió un correo abierto detrás, con nombres, direcciones y una firma
con teléfono. No se tapa ni se difumina —eso sería mentir sobre lo que la captura enseña—: el
diagnóstico lo avisa en cada foto de pantalla, para que quien mire las imágenes sepa qué está
mirando antes de mandárselas a nadie.

---

# `--vigilia` — la barra funcionando, midiéndose

## Por qué es una bandera y no «arrancar un binario y mirarlo»

Lo que D6 y D7 prometen —que con la CPU alta y muchas ventanas **no se reconstruyan los
paneles continuamente**, y que un proceso encendido todo el día no se hinche— no lo contesta
una foto ni una prueba pura. Lo contesta el tiempo.

Hasta ahora esa medida se hacía arrancando un binario de fase como barra normal y mirándolo un
rato. Eso tiene un problema que ya mordió: **una barra arrancada sin banderas puede escribir la
configuración**, y una versión nueva se la migra sin preguntar. Pasó: un `dist-d8` olvidado
corrió diez minutos como barra normal y migró el `config.json`.

Por eso esto es una bandera. `Config.PuedeEscribir` es falso en cuanto hay una bandera
conocida, así que la vigilia **no puede** tocar el fichero ni queriendo: la garantía es
estructural, no un cuidado que haya que acordarse de tener. Y se cierra sola al terminar, para
que no quede una segunda barra encendida que haya que ir a apagar.

```
NubeBar.exe --vigilia [minutos]      # por defecto 15; una muestra cada 30 s
```

## Qué mide

Memoria privada y gestionada, CPU acumulada, recolecciones por generación, manejadores
abiertos, y lo que es propio de NubeBar: **cuántas veces se han remontado las zonas**.

Ese último es EL número de «no se reconstruyen los paneles continuamente». Remontar es tirar
los widgets y rehacerlos, y `RemontarZonas` es el único sitio desde el que se hace. Con la
barra quieta tiene que quedarse parado; si sube solo, algo está remontando en bucle.

## Dos cuidados en cómo se juzga

1. **La pendiente se mide desde el primer tercio, no desde el arranque.** Los primeros minutos
   suben siempre porque se llenan las cachés de iconos y de miniaturas. Contar esa subida como
   fuga sería acusar a algo que está haciendo bien su trabajo.
2. **Por debajo de cinco minutos no se da veredicto de memoria.** La primera prueba corta, de
   minuto y medio, dijo «+3,16 MB/min SUBE» — y no era una fuga, era el calentamiento. Ahora
   con menos de cinco minutos dice que las muestras valen y la pendiente no, en vez de
   inventarse una conclusión que los datos no sostienen.

Y lo que no mide, dicho en la propia salida: con **tus** cien ventanas abiertas y la CPU a
tope. Mide con lo que haya en la máquina en ese momento.

## La prueba que cazó la bandera a medio registrar

Añadir `--vigilia` son cuatro sitios: la clase, el registro en el arranque (`App.Banderas`), el
lanzador en `MainWindow`, y la lista de las que **miran y no tocan**
(`Config.BanderasQueMiran`). Puse tres y me dejé la cuarta.

Saltó la prueba de D8, que es justo para eso. Pero decía esto y nada más:

```
esperado 18, real 17
```

Con eso se sabe que falta una bandera en una de dos listas — no cuál, ni en cuál — y toca
contar a mano en dos ficheros. Tardé bastante más en encontrarla que en arreglarla, y parte
del rato lo gasté persiguiendo una hipótesis falsa (que el binario estuviera viejo), porque el
mensaje no daba por dónde mirar.

Así que ahora las dos comparaciones de listas dicen **los nombres** que faltan y los que
sobran, en los dos sentidos. No hace la prueba más estricta: hace que cuando salte, sirva.

> Nota de método: lo que acabó señalando el sitio no fue contar mejor, fue darse cuenta de que
> el texto del fallo decía «la lista **del programa**» y el de la comprobación que yo miraba
> decía «del arranque». Eran dos comprobaciones distintas. Que cada mensaje de error sea
> literalmente distinto del de al lado no es cosmética.

## Carpetas de salida

`dist-d6`, `dist-d7`, `dist-d8` y `dist-d8-unico` están en `.gitignore`; `dist-nuevo` es la
versión estable y **no se sobreescribe nunca**. Durante esta sesión usé además una carpeta
`dist-cap` para correr diagnósticos, y eso estuvo mal por dos motivos pequeños pero reales: no
estaba en `.gitignore` —así que aparecía como basura en `git status`— y era un nombre más que
recordar sin aportar nada. Los diagnósticos se corren desde `dist-d8`, que es el mismo binario.

---

# La guía de reglas: «Qué puedes hacer»

## El problema que quedaba

D9.3 puso un editor con todos los campos, y eso quitó la necesidad de escribir JSON a mano.
No quitó la otra: **saber qué se le puede pedir**. Delante de un formulario con «Clase de
ventana», «Ruta contiene» y «Prioridad», quien no haya leído este documento no sabe si
NubeHypr puede mandar Discord al escritorio 2 — ni cómo se escribe eso.

Un formulario completo y un usuario que no sabe qué pedirle sigue siendo una función que solo
puede usar quien la escribió.

## Ocho ejemplos, antes de la lista de reglas

`HyprGuia` es puro: el catálogo, la traducción de una regla a una frase y los avisos. Dibujarlo
es de `VentanaAjustes.Guia.cs`. La sección va **encima** del editor, porque la pregunta se hace
al abrir la página, no después de pelearse con once campos.

Cada tarjeta enseña cuatro cosas en este orden: qué consigues, **qué campos se rellenan**, el
ejemplo escrito tal cual (`Proceso = Discord`, no `Discord.exe`), y la frase legible. Y un
botón que lo pone en el formulario.

Todos usan aplicaciones y nombres de proceso de verdad. Un ejemplo con «MiAplicacion» obliga a
traducir, y traducir es justo el paso en el que la gente se equivoca.

## Lo que «Usar este ejemplo» hace y lo que no

Rellena el formulario. La regla nace **apagada**, se abre su editor, y **no se guarda**: es el
mismo camino que «Añadir una regla». Si cierras Ajustes sin pulsar Guardar, no queda rastro.
Abrir la guía tampoco escribe nada.

## La frase, en un solo sitio

`HyprGuia.EnPalabras` la usan la tarjeta del catálogo **y** la vista previa del formulario. Si
cada una la escribiera por su lado, el día que alguien tocara una habría dos verdades.

Con un matiz que se vio en la primera captura: en el catálogo la frase se cuenta como si la
regla estuviera encendida. Los ejemplos nacen apagados, pero una tarjeta que termina en
«(ahora mismo está apagada)» habla del estado de una regla que todavía no existe, y repetido
ocho veces es ruido. En el formulario sí se dice, porque allí la regla ya es tuya.

## «Probar regla»

Evalúa las ventanas abiertas y dice qué le haría a cada una. **No mueve ninguna**, y el aviso
de lo que de verdad va a ocurrir sale siempre debajo del resultado: sin él, «coincide con 3
ventanas» se lee como «va a mover 3 ventanas».

Dos decisiones:

- **Se prueba como si estuviera encendida.** Una regla recién creada nace apagada; si se
  evaluara tal cual, daría siempre cero y el botón no serviría hasta encenderla — que es justo
  lo que no queremos obligar a hacer para mirar.
- **Con un problema grave no se evalúa.** Una regla sin criterios «coincide» con cero ventanas,
  y enseñar «0 ventanas» sin decir por qué haría pensar que el filtro está bien escrito y que
  no hay nada abierto.

## Un fallo que encontró la captura

El contador «a cuántas ventanas afecta» decía **«no coincide con ninguna ventana abierta»** en
una regla de Discord con Discord delante. La causa: `HyprReglas.Coincide` descarta de entrada
las reglas apagadas, y las recién creadas nacen apagadas.

Quien lo lee concluye que el nombre del proceso está mal y se pone a cambiarlo — el error
contrario al que ese contador existe para evitar. Ahora cuenta como si estuviera encendida, y
que esté apagada se dice en su propio aviso, que es donde corresponde.

## Los avisos, todos a la vez y en castellano

`HyprReglas.Valida` da un motivo y para. El editor los enseña todos: arreglar uno, guardar y
encontrarse con el siguiente es lo que hace que un editor parezca que va a trompicones.

Y separa lo grave de lo que solo es raro. «Flotante + layout» no impide guardar: puede ser a
propósito, y un editor que se niega a guardar lo legal acaba peleándose con quien sabe lo que
hace. Lo que sí se dice siempre:

| Caso | Qué sale |
|---|---|
| Sin criterios | «No has dicho a qué ventanas se aplica…» (grave) |
| Sin acciones | «No has dicho qué hacer con ellas…» (grave) |
| `Espacio = 0` | «Los escritorios se cuentan desde 1» — el error de cálculo más fácil, porque los monitores sí empiezan en 0 |
| `Proceso = Discord.exe` | «Pon «Discord» en vez de «Discord.exe»», con el nombre ya corregido |
| Una ruta en «Proceso» | manda al campo «Ruta contiene» |
| Apagada | se dice, sin tratarlo como un error |

## Probado con los ocho ejemplos, sin tocar el ratón

`--capturas` recorre **el mismo camino de código que el botón**, ejemplo por ejemplo, y prueba
cada regla resultante. Nada de inyección de teclado ni de ratón: se llama al manejador, que es
lo que el clic acaba llamando. Si una tarjeta o un formulario revienta, sale con su número.

Resultado: `los 8 ejemplos de la guía se usan y se prueban sin reventar`.

---

# `--guia` / `--sololectura` — el modo de demostración

## Por qué hace falta

La guía se puede probar en lógica y fotografiar, pero lo que falta es **usarla**: escribir en
los campos, pulsar los ocho ejemplos, probar reglas, ver los tooltips. Eso necesita manos.

Y hacerlo con una barra normal abre la puerta a todo lo que una barra normal puede hacer:
escribir el `config.json`, migrarlo a un esquema nuevo, registrar diecinueve atajos globales y
—si se toca el interruptor— encender NubeHypr de verdad.

No es hipotético. El 26 de octubre el config del usuario apareció migrado a v4 y con
`Activo: true` después de que un binario de fase corriera como barra normal. Se restauró byte
a byte desde el respaldo, pero el susto estaba servido.

```
NubeBar.exe --guia          (o --sololectura, el mismo modo)
```

Dos nombres a propósito: «guía» dice para qué es y «solo lectura» dice qué garantiza. Según lo
que uno tenga en la cabeza busca una o la otra.

## Las cinco garantías, y de dónde sale cada una

| | Cómo se garantiza |
|---|---|
| No escribe `config.json` | Es una bandera ⇒ `Config.PuedeEscribir` es falso ⇒ `ConfigArbol.Guardar` se niega |
| No migra el esquema | `Config.Cargar` solo llama a `Esquema.MigrarFichero` si NO es solo lectura. Un v3 sigue siendo v3 |
| NubeHypr queda apagado | `DiagGuia.Apagar` lo fuerza **dentro de `Config.Cargar`** |
| No registra atajos globales | `AplicarAtajosDeHypr` sale antes de registrar |
| No mueve ventanas | Nada de este camino llama al adaptador |

Ninguna de estas es un cuidado que haya que recordar: son estructurales.

### Dónde va el apagado, y por qué importa

`DiagGuia.Apagar` se llama desde `Config.Cargar`, no desde la interfaz. Hay media docena de
sitios que cargan la configuración —la isla, Ajustes, los monitores, el wallpaper— y si cada
uno tuviera que acordarse de apagar la capa, el que se olvidara sería el que la encendiera. Y
sería el único que importa.

Con el mismo cuidado al revés: hay una prueba de que **sin** la bandera no se toca nada. Esa
función corre en todos los arranques; si apagara NubeHypr sin la bandera, habría roto la capa
para el uso normal.

## Comprueba sus propias garantías antes de abrir

El modo no se fía de su propia documentación. Al arrancar mide que la configuración esté en
solo lectura, que NubeHypr haya quedado apagado y que el modo sea simulación; **si alguna
falla, no abre la guía**. Un modo seguro que se abre igual cuando sus garantías no se cumplen
es peor que no tenerlo, porque invita a tocar justo lo que dice proteger.

Y toma la huella de tu `config.json` al empezar y al salir, para poder decirte si se ha tocado.
Lo demás son intenciones del código; eso es la única prueba.

## Lo que se ve

El rótulo va lo primero de la página, en coral, y dice las dos mitades: lo que **no** va a
pasar y lo que **sí** se puede hacer. Un aviso que solo prohíbe deja al usuario sin saber para
qué está ahí, y entonces no toca nada — que es lo contrario de para lo que se hizo.

Al cerrar la ventana de Ajustes, NubeBar se cierra solo. Así no queda una segunda barra
encendida que haya que ir a apagar, que es exactamente el accidente que este modo viene a
evitar.

---

# El binario de entrega, fuera de alcance

`dist-d8-unico/NubeBar.exe` ya no existe. El candidato de entrega se publica ahora en:

```
experimental-NO-EJECUTAR/NubeBar-EXPERIMENTAL-no-ejecutar.exe
```

con un `LEEME.txt` que explica por qué, y la carpeta añadida a `.gitignore`.

El motivo es el de arriba: es un binario que conoce un esquema **más nuevo** que el instalado,
y una barra sin banderas puede escribir el config — correctamente, porque eso es lo que una
barra hace. El nombre y la carpeta son la diferencia entre «un ejecutable que invita a un doble
clic» y «un artefacto que hay que querer abrir».

Lo que sí se puede ejecutar sin riesgo es todo lo que lleva bandera, desde `dist-d8`, que es el
mismo código.
