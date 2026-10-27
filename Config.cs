using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NubeBar;

public class Tema
{
    public string Fondo { get; set; } = "#E6000000";   // ARGB: los 2 primeros dígitos son la opacidad
    public string Texto { get; set; } = "#FFFFFF";
    public string Acento { get; set; } = "#7AA2F7";
    public double Radio { get; set; } = 20;
    public string Fuente { get; set; } = "Segoe UI";
    public double TamanoFuente { get; set; } = 13;
    public bool DesdeWallpaper { get; set; } = false;          // colores sacados del wallpaper actual (tipo pywal)
    public bool EnviarColorAlWallpaper { get; set; } = false;  // manda tu Acento como "schemecolor" al wallpaper
    public bool BordeAcento { get; set; } = true;              // filo de 1 px y brillo suave del color de acento

    // ---------- El estilo visual ----------
    //
    // La FORMA de las superficies: cuánto se redondea una tarjeta, cuánto filo tiene, cuánto
    // aire lleva dentro y si brilla. El tema decide los COLORES; esto decide la forma.
    //
    // Estaba escrito en cuatro constantes dentro del código, así que cambiarlo era
    // recompilar — y es justo lo que el usuario quiere poder elegir.
    //
    // «normal» es lo de siempre al píxel: quien no toque nada no nota que esto existe. Lo que
    // no se entienda cae en «normal» y nunca en el más llamativo (ver `Estilos.Leer`).
    public string Estilo { get; set; } = "normal";

    // ---------- Roles de color (todos opcionales) ----------
    // Un tema puede seguir siendo tres colores: lo que no se ponga aquí, Roles lo
    // deriva del fondo y del texto. Así los seis temas de serie y los que ya tenga
    // guardados el usuario pintan exactamente igual que antes de existir esto.
    // Se ponen cuando el tema quiere mandar, como hace "Nube" con su naranja y su
    // azul, que no se pueden sacar del fondo ni del acento.
    public string? Superficie { get; set; }       // tarjetas y filas dentro de un panel
    public string? SuperficieAlta { get; set; }   // lo que está bajo el ratón o elegido
    public string? Borde { get; set; }            // separadores y filos
    public string? TextoAtenuado { get; set; }    // segundo renglón, unidades, pistas
    public string? Secundario { get; set; }       // detalles cálidos
    public string? Sistema { get; set; }          // red, volumen, batería
    public string? Positivo { get; set; }
    public string? Aviso { get; set; }
    public string? Negativo { get; set; }
}

// Los mandos del halo del estilo «Neón».
//
// ---------- Por qué es una sección propia y no va dentro de `Tema` ----------
//
// Conceptualmente pertenece al tema. Pero `ConfigArbol.Poner(seccion, clave, valor)` escribe
// UN nivel: `raiz[seccion][clave]`. Metido en `Tema`, Ajustes habría tenido que escribir
// claves llamadas literalmente «Neon.Intensidad» dentro de `Tema`, que el modelo tipado no
// lee — se guardarían los ajustes y no haría ninguno efecto, en silencio.
//
// Así que va al lado de `Clima` y `NubeHypr`, que es donde el árbol sabe escribir.
//
// ---------- Por qué son ajustes y no números míos ----------
//
// «Cuánto tiene que brillar» no tiene respuesta correcta: depende del monitor, del fondo de
// pantalla y de a quién le canse antes. Lo que para uno es un halo bonito, para otro es una
// luz de discoteca. Así que se elige, como el estilo.
//
// ---------- Y qué NO se puede elegir ----------
//
// Que el halo desaparezca del todo o que tape el contenido. `Intensidad` y `Radio` llegan
// topados (ver `Neon.Limpia`): un ajuste puede decorar, no puede dejar la interfaz rota ni
// ilegible. Es la misma regla que ya cumple `EstiloVisual`.
public sealed class OpcionesNeon
{
    // Multiplica la opacidad del halo. 1 = lo pensado; 0,3 = casi nada; 2 = fuerte.
    public double Intensidad { get; set; } = 1.0;

    // Hasta dónde crece el difuminado en el punto más alto de la respiración, en píxeles.
    public double Radio { get; set; } = 26;

    // Lo que tarda un ciclo entero (crecer y volver), en milisegundos.
    public int Velocidad { get; set; } = 2600;

    // Apagarlo deja el halo FIJO, no lo quita. Es la diferencia entre «no quiero movimiento»
    // y «no quiero estilo»: lo segundo ya se consigue eligiendo otro estilo.
    public bool Respira { get; set; } = true;

    // Vacío = el color de acento del tema. Puesto, manda este. Dos colores separados por
    // coma = degradado (el primero es el del filo; el segundo, el del halo).
    public string Color { get; set; } = "";
}

// Qué secciones lleva cada panel lateral y en qué orden.
//
// Vacías = el reparto de serie (ver `Lateral.Repartidas`). Un config que no las tenga —todos
// los que existen hoy— sigue viendo exactamente lo de siempre.
//
// Van como listas de ids y no como objetos: es lo mismo que `ZonasPanel` hace con los módulos
// de la barra, y por la misma razón — mover una cosa de sitio tiene que ser mover una cadena
// de sitio, no reescribir una estructura.
public sealed class OpcionesLaterales
{
    public List<string> Izquierda { get; set; } = new();
    public List<string> Derecha { get; set; } = new();
}

public class OpcionesWallpaper
{
    public string? Ruta { get; set; }        // ejecutable de Wallpaper Engine; vacío = buscarlo en Steam
    public int Monitor { get; set; } = 0;    // monitor del que se leen los colores

    // ---------- La integración (opcional, y desactivada de serie) ----------
    //
    // Desactivada a propósito: activarla hace que NubeBar abra un programa de otro y le
    // dé órdenes. Eso se pide, no se hereda al actualizar.
    public bool Integrar { get; set; } = false;
    public bool IniciarConNubeBar { get; set; } = false;

    // Lo que se le pide cargar. La playlist manda si están las dos: es lo más específico
    // que el usuario puede haber elegido.
    public string? WallpaperElegido { get; set; }
    public string? PlaylistElegida { get; set; }

    // "todos" | "0" | "1" ... El índice es el de Wallpaper Engine.
    public string MonitorDestino { get; set; } = "todos";

    // "nada" | "pausar" | "cerrar". Solo se aplica a lo que NubeBar haya abierto:
    // ver Integracion.QueHacerAlSalir.
    public string AlSalir { get; set; } = "nada";

    // Cuánto espera la escena de la nube a que el fondo esté disponible.
    public double SegundosEspera { get; set; } = 8;
}

public class Barra
{
    // ---------- Geometría: DERIVADA, no editable ----------
    // Desde B5 la autoridad de estas seis es Paneles[principal] (ver PanelNube y
    // Config.MigrarYDerivarPaneles): se rellenan al cargar a partir del panel y no se
    // escriben nunca en el fichero. Siguen aquí porque las leen sesenta y pico sitios
    // y cambiarlos todos a la vez no habría comprado nada; lo que NO hay que hacer es
    // guardarlas: se perderían al siguiente arranque.
    public string Modo { get; set; } = "isla";        // "isla" | "barra"
    public string Posicion { get; set; } = "arriba";  // los cuatro bordes: ver PanelNube.Posiciones
    public double Alto { get; set; } = 38;            // el "Grosor" del panel
    public double Margen { get; set; } = 8;
    public bool ReservarEspacio { get; set; } = true;  // las ventanas maximizadas no la tapan
    // ---------- Y de aquí abajo, lo que sí es suyo ----------
    public bool OcultarBarraWindows { get; set; } = true;
    public int MaxApps { get; set; } = 10;             // iconos de apps visibles a la vez; el resto, con la rueda
    public bool AgruparApps { get; set; } = true;      // las ventanas del mismo programa, en un solo icono
    // ---------- El atajo de la vista general ----------
    //
    // Ctrl+Alt+Space estaba ocupado en el equipo de desarrollo y el registro fallaba con el
    // error 1409. Ctrl+Alt+W ("W de ventanas") sí está libre, y no choca con
    // Ctrl+Shift+Space, que en Visual Studio es la ayuda de parámetros.
    //
    // El valor de serie es una CONSTANTE y no un literal repetido, y eso salió de un fallo
    // que se vio mirando una captura de la ventana de Ajustes: la página leía el atajo del
    // árbol con `"Ctrl+Alt+Space"` de respaldo —el valor viejo— mientras el modelo tipado
    // usaba `"Ctrl+Alt+W"`. Con la clave ausente del fichero, que es el caso de cualquier
    // instalación que no la haya tocado, Ajustes enseñaba un atajo que NO era el registrado,
    // con un error en rojo debajo; y pulsar Guardar habría escrito el valor equivocado
    // encima de uno que funcionaba.
    public const string AtajoDeSerie = "Ctrl+Alt+W";

    public string AtajoVistaGeneral { get; set; } = AtajoDeSerie;       // abre la cuadrícula de ventanas

    // Arrastrar una ventana a un borde la ajusta a media pantalla, a un cuarto o la
    // maximiza, con una previsualización antes de soltar. Se puede apagar: quien ya
    // usa el ajuste de Windows o una herramienta propia no quiere dos a la vez.
    public bool AjustarAlArrastrar { get; set; } = true;
    public string Monitores { get; set; } = "todos";   // "todos" | "principal" (DERIVADA de Paneles, ver arriba)
    public bool AppsPorMonitor { get; set; } = true;   // cada isla enseña las apps de SU monitor
    public bool OcultarEnPantallaCompleta { get; set; } = true;
    // Una ventana maximizada también la echa. Ya no viene de serie: la isla iba y
    // venía cada vez que pasabas de un Excel maximizado a otra cosa. Para quitarla
    // de en medio está "Esconder la isla" en el clic derecho (vuelve con Win).
    public bool OcultarConMaximizada { get; set; } = false;
    public bool Bandeja { get; set; } = true;        // iconos de la bandeja del sistema en la isla

    // ---------- Las rayitas entre grupos ----------
    //
    // La barra dibuja una raya fina entre tramos de módulos de distinta categoría (ver
    // `GruposDeBarra.RayaEntre`). No son módulos, así que **no salen en el editor de
    // módulos** — y el usuario se encontró dos rayas en su barra que no podía ni encontrar ni
    // quitar: «en la nube hay 2 separadores que no se ven en los ajustes».
    //
    // Tenía razón en el fondo: algo que se ve en la barra tiene que poder apagarse desde
    // algún sitio.
    //
    // ---------- Y nace APAGADO ----------
    //
    // Primero se puso encendido, que era lo que había. El usuario lo vio y dijo «quita los
    // separadores automáticos», así que se invierte el valor de serie en vez de dejárselo
    // apagado solo a él: lo que molesta a quien lo prueba molestará a quien lo instale.
    //
    // El objetivo visual pedía «separadores verticales finos entre grupos», y esto lo
    // contradice a propósito — con la barra delante no funcionan, y lo que se ve manda sobre
    // lo que se escribió antes de verlo. El interruptor sigue ahí para quien los quiera.
    //
    // Apagados, los grupos se distinguen por el hueco, que es como lo hace cualquier barra
    // bien hecha y es lo que ya había antes de añadir las rayas.
    public bool SeparadoresDeGrupo { get; set; } = false;
    public int MaxBandeja { get; set; } = 0;         // cuántos se ven sin abrir el desplegable "^" (0 = solo el "^")
    public bool Notificaciones { get; set; } = true; // las notificaciones asoman en la isla
    public bool AppsDelEscritorio { get; set; } = false;   // el carrusel, solo con las del escritorio virtual actual
    public List<string> AppsFijadas { get; set; } = new();  // apps siempre presentes en el carrusel

    // ---------- E2: el dock ----------
    //
    // "siempre" | "alacercarse" | "nunca". De serie **"nunca"**, y no por timidez: el dock es
    // una superficie nueva que aparece en el escritorio, y una actualización de NubeBar no
    // puede cambiarle a nadie la forma del escritorio sin que lo haya pedido. Es la misma
    // regla que con NubeHypr.
    public string Dock { get; set; } = "nunca";

    // E3/E4: cuándo se abren los paneles laterales. «nunca» o «alacercarse».
    //
    // Apagado de serie, como el dock y por lo mismo: una actualización no puede cambiarle a
    // nadie la forma del escritorio sin que lo haya pedido. Los paneles están construidos
    // desde la macrofase E y hasta ahora no tenían NINGUNA forma de abrirse —solo los
    // llamaba el diagnóstico de capturas—, así que esto es su puerta.
    public string Laterales { get; set; } = "nunca";
    public bool PulsoAudio { get; set; } = true;            // la isla respira con lo que suena (y aberra un poco)
    public bool TeclasVolumen { get; set; } = true;         // las teclas de volumen las atiende la isla, sin el aviso de Windows
    public bool Arrastrable { get; set; } = true;           // se puede apartar con el ratón
    public double SegundosParaVolver { get; set; } = 1;     // tras dejarla, cuánto tarda en volver
    public List<string> OrdenApps { get; set; } = new();    // el orden que les has dado tú arrastrando
    public List<string> NotificacionesSilenciadas { get; set; } = new();   // trozos de AppUserModelId
}

public class Modulos
{
    public List<string> Izquierda { get; set; } = new() { "inicio" };
    public List<string> Centro { get; set; } = new() { "fijadas", "apps", "reloj" };
    public List<string> Derecha { get; set; } = new() { "cpu", "ram", "bateria", "bandeja", "control", "fecha" };
    // Ya se tocaron una vez en una config de antes (ver Config.ModulosDeSerie)
    public bool ControlDeSerie { get; set; } = true;
    public bool BandejaDeSerie { get; set; } = true;
    public bool FijadasDeSerie { get; set; } = true;
    public bool AppsAlCentro { get; set; } = true;
    public bool NotificacionesDeSerie { get; set; } = true;
    public bool CentroALaIzquierda { get; set; } = true;
    public bool CentroRetirado { get; set; } = true;

    // Widgets puestos en una zona pero que NO se dibujan.
    //
    // Ocultar y quitar no son lo mismo, y por eso hacen falta dos sitios: quitar un
    // widget lo saca de su zona y pierde dónde estaba; ocultarlo lo deja en su sitio
    // y con su configuración, y al volver a mostrarlo reaparece donde estaba, no al
    // final. Es la diferencia entre "esto hoy no lo quiero ver" y "esto fuera".
    public List<string> Ocultos { get; set; } = new();
}

// Nivel de animación y la escena de entrada.
public class Animaciones
{
    // "auto" sigue a Windows (Accesibilidad > Efectos visuales > Efectos de
    // animación): quien ha pedido menos movimiento en el sistema no tiene que
    // volver a pedirlo aquí. "completo", "equilibrado" y "reducido" mandan sobre él.
    public string Nivel { get; set; } = "auto";

    // Cuándo se ve la escena de entrada —la nube que despierta y construye el
    // escritorio—. La clave se sigue llamando "Robot" porque está escrita en el config
    // de la gente desde la Fase A: renombrarla les dejaría la escena en su valor de
    // serie sin avisar, y lo que cambió fue el personaje, no el ajuste.
    // "primera-vez" | "cada-inicio" | "al-cambiar-tema" | "nunca"
    public string Robot { get; set; } = "primera-vez";
    public bool RobotVisto { get; set; } = false;        // lo escribe la propia escena al terminar
    public bool RobotAlCambiarTema { get; set; } = true; // además, la primera vez que cambias de tema
}

// Un panel de la nube, y **la autoridad** de su geometría: borde, posición, grosor,
// margen, visibilidad y en qué monitores sale. Es lo que vive en el config y lo que
// reciben las escrituras; `Barra` se deriva de aquí al cargar (ver
// Config.MigrarYDerivarPaneles) para que los sesenta y pico lectores de siempre no
// tengan que cambiar.
//
// Es una lista porque el modelo admite varios paneles, aunque hoy solo se dibuje el
// "principal": añadir un segundo no obligará a cambiar el config de nadie.
public class PanelNube
{
    public string Id { get; set; } = "principal";
    public bool Activo { get; set; } = true;
    public string Modo { get; set; } = "isla";          // "isla" (flotante) | "barra" (de lado a lado)

    // Los cuatro bordes, válidos los cuatro en los dos modos. Lo que cambia no es si
    // valen, sino qué significan:
    //
    //   modo isla  + lateral → la isla se ANCLA a ese lado, horizontal como siempre
    //   modo barra + lateral → barra VERTICAL de verdad, con las zonas apiladas (B6)
    public string Posicion { get; set; } = "arriba";

    public double Grosor { get; set; } = 38;            // alto si es horizontal, ancho si es vertical
    public double Margen { get; set; } = 8;

    public static readonly string[] Posiciones = { "arriba", "abajo", "izquierda", "derecha" };
    public static readonly string[] Visibilidades = { "siempre", "autoocultar", "ventanas-encima", "por-debajo" };

    public static bool PosicionValida(string? p) =>
        p != null && Posiciones.Any(x => x.Equals(p, StringComparison.OrdinalIgnoreCase));

    public static bool VisibilidadValida(string? v) =>
        v != null && Visibilidades.Any(x => x.Equals(v, StringComparison.OrdinalIgnoreCase));

    // ¿El borde es uno de los laterales?
    public static bool EsLateral(string? p) =>
        p != null && (p.Equals("izquierda", StringComparison.OrdinalIgnoreCase)
                   || p.Equals("derecha", StringComparison.OrdinalIgnoreCase));

    // Los cuatro modos de Plasma, y lo que significa cada uno en el orden Z.
    public string Visibilidad { get; set; } = "siempre";  // "siempre" | "autoocultar" | "ventanas-encima" | "por-debajo"

    // Las decisiones de visibilidad están aquí, fuera de la ventana, por dos razones:
    // se pueden probar sin abrir nada, y así hay UN sitio donde está escrito qué hace
    // cada modo en vez de cuatro ifes repartidos.
    //
    //   siempre          → por encima de todo
    //   autoocultar      → por encima, pero recogida hasta que acercas el ratón
    //   ventanas-encima  → una ventana normal: las demás pueden taparla
    //   por-debajo       → al fondo del todo, como un widget del escritorio
    static string Vis(string? v) => VisibilidadValida(v) ? v!.ToLowerInvariant() : "siempre";

    public static bool PorEncima(string? v) => Vis(v) is "siempre" or "autoocultar";
    public static bool AutoOculta(string? v) => Vis(v) == "autoocultar";
    public static bool AlFondoDelTodo(string? v) => Vis(v) == "por-debajo";

    // ¿Toca estar recogida (fuera de la pantalla, asomando con el ratón al borde)?
    // Dos motivos distintos que acaban en el mismo deslizamiento: el autoocultar del
    // panel, que se recoge siempre, y la pantalla completa de toda la vida.
    //
    // Lo pregunta MainWindow.RevisarPantallaCompleta, que no necesita saber cuál de
    // los dos ha sido: le basta con si toca o no. El motivo solo va al log.
    public static (bool Toca, string Motivo) TocaRecogerse(
        string? visibilidad, bool ocultarEnPantallaCompleta, bool hayPantallaCompleta)
    {
        if (AutoOculta(visibilidad)) return (true, "autoocultar");
        if (ocultarEnPantallaCompleta && hayPantallaCompleta) return (true, "pantalla completa");
        return (false, "");
    }

    // ¿Puede esta nube recogerse alguna vez? Si no, no hay ni que mirar quién está a
    // pantalla completa: se fuerza visible y se sale.
    public static bool PuedeRecogerse(string? visibilidad, bool ocultarEnPantallaCompleta) =>
        AutoOculta(visibilidad) || ocultarEnPantallaCompleta;

    // ---------- Orientación ----------
    //
    // Desde B6 los cuatro bordes se dibujan de verdad, así que esto ya no recorta
    // nada: solo pone el borde en minúsculas y cambia por "arriba" lo que no conozca.
    public static string BordeEfectivo(string? posicion) =>
        PosicionValida(posicion) ? posicion!.ToLowerInvariant() : "arriba";

    public static bool EsBarra(string? modo) =>
        modo != null && modo.Equals("barra", StringComparison.OrdinalIgnoreCase);

    // **La pregunta de B6.** Una barra pegada a un lateral es una barra VERTICAL: la
    // ventana es una columna de alto completo, las zonas se apilan y los paneles salen
    // flotando al lado. Una ISLA pegada a un lateral sigue siendo horizontal y solo
    // se ancla a ese lado (eso es B5 y no cambia).
    public static bool EsVertical(string? modo, string? posicion) =>
        EsBarra(modo) && EsLateral(BordeEfectivo(posicion));

    public static bool PegadaAlaIzquierda(string? posicion) =>
        BordeEfectivo(posicion).Equals("izquierda", StringComparison.OrdinalIgnoreCase);

    // ¿Cuelga del borde de arriba? Es la pregunta de los sesenta lectores de siempre y
    // solo tiene sentido en horizontal. En vertical la ventana ocupa la pantalla de
    // arriba abajo, así que la respuesta da igual y se queda en "sí".
    public static bool CuelgaDeArriba(string? posicion) =>
        BordeEfectivo(posicion) != "abajo";

    // El borde de la appbar (ABE_LEFT=0, ABE_TOP=1, ABE_RIGHT=2, ABE_BOTTOM=3). Es el
    // mismo número que usa Windows, y aquí sale del borde del panel y del modo: una
    // isla anclada a la izquierda reserva —si reservara— por arriba, no por el lado.
    public static uint BordeAppBar(string? modo, string? posicion)
    {
        if (EsVertical(modo, posicion)) return PegadaAlaIzquierda(posicion) ? 0u : 2u;
        return CuelgaDeArriba(posicion) ? 1u : 3u;
    }

    // ¿Le pedimos a Windows que nos guarde la franja (appbar)? Son TRES estados y no
    // dos, y la diferencia importa:
    //
    //   null  → no lo has elegido: la nube flota, en los cuatro bordes
    //   true  → lo has elegido: resérvala, como la barra de tareas
    //   false → lo has elegido: no la reserves, que las ventanas pasen por debajo
    //
    // Hacía falta el tercer estado porque el valor heredado era `true` y en modo ISLA
    // no hacía nada: llevaba ahí sin efecto desde siempre, y al pasar a barra se
    // activó solo y convirtió la nube en una barra de tareas que nadie había pedido.
    // Un bool no sabe distinguir "lo quiero" de "venía puesto".
    public bool? ReservarEspacio { get; set; }

    // Y el valor de serie es NO RESERVAR, en los cuatro bordes.
    //
    // Primero se intentó que dependiera de la orientación —horizontal reserva, vertical
    // no—, copiando lo que hace Plasma. Pero esta nube no es un panel de Plasma: es una
    // isla que flota, y eso vale igual estirada de lado a lado. Reservar hueco empuja
    // las ventanas de todo el escritorio, que es una decisión demasiado grande para
    // tomarla por el usuario solo porque haya elegido el borde de arriba.
    //
    // Ya no depende de nada, y por eso no recibe nada: una función con dos parámetros
    // que no mira invita a pensar que aún hay una regla escondida.
    public const bool ReservaPorDefecto = false;

    public static bool ReservaEfectiva(bool? elegido) => elegido ?? ReservaPorDefecto;

    public string Monitores { get; set; } = "todos";      // "todos" | "principal"

    // Las zonas del panel NO se guardan aquí: siguen viviendo en "Modulos", que es lo
    // que edita la biblioteca y lo que el usuario reconoce en su config. Esto es una
    // VISTA de aquellas listas (las mismas instancias, no copias), que rellena
    // MigrarYDerivarPaneles al cargar.
    //
    // Sin el JsonIgnore se escribían las tres listas también dentro de Paneles, y al
    // primer cambio en la biblioteca las dos copias dejaban de coincidir: justo el
    // problema por el que se ha quitado la geometría de "Barra".
    [JsonIgnore]
    public ZonasPanel Zonas { get; set; } = new();
}

// Las zonas de un panel, como colección y no como tres campos sueltos: así el bucle
// que lo rellena no sabe cuántas hay ni cómo se llaman.
public class ZonasPanel
{
    public List<string> Izquierda { get; set; } = new();
    public List<string> Centro { get; set; } = new();
    public List<string> Derecha { get; set; } = new();

    // Los que están puestos pero no se dibujan (ver Modulos.Ocultos). Van aparte de
    // las zonas a propósito: el widget conserva su sitio mientras está oculto.
    public List<string> Ocultos { get; set; } = new();

    public bool Oculto(string id) =>
        Ocultos.Any(o => o.Equals(id, StringComparison.OrdinalIgnoreCase));

    // En el orden en que se pintan, con su nombre. Lo usa MainWindow.Llenar.
    public IEnumerable<(string Nombre, List<string> Widgets)> Todas()
    {
        yield return ("Izquierda", Izquierda);
        yield return ("Centro", Centro);
        yield return ("Derecha", Derecha);
    }
}

public class Inicio
{
    public List<string> Fijadas { get; set; } = new();              // apps ancladas en el menú
    public bool TeclaWindows { get; set; } = true;                  // la tecla Windows sola abre este menú (fase 6)
    public bool BuscarArchivos { get; set; } = true;                // usar el índice de Windows Search
    public string BuscadorWeb { get; set; } = "https://www.google.com/search?q=";
    public bool Saludo { get; set; } = true;                        // cabecera con "Buenas tardes, ..."
}


// Las opciones de la capa de organización de ventanas. Nace apagada y así se queda:
// quien la enciende es el usuario, en Ajustes o en el fichero, nunca una migración.
//
// Los valores son los de la especificación. `Modo` se lee con `NubeHypr.ModoEfectivo`,
// que convierte lo desconocido en simulación y nunca en "aplicar".
public sealed class OpcionesHypr
{
    public bool Activo { get; set; }                        // false de serie
    public string Modo { get; set; } = "simulacion";
    public int Gap { get; set; } = 8;
    public int Margen { get; set; } = 12;
    public string Layout { get; set; } = "principal";
    public string EspacioInicial { get; set; } = "actual";
    public bool ConfirmarMovimientos { get; set; } = true;
    public bool MostrarPrevisualizacion { get; set; } = true;

    // ---------- El hallazgo de D2 ----------
    //
    // Nace APAGADA. Apagada, las ventanas maximizadas y minimizadas se quedan fuera del
    // reparto, con su motivo. Encendida, se sacan de ese estado para poder enlosarlas —y
    // se guarda qué eran, para poder devolverlas.
    //
    // Sacar una ventana de maximizada es una acción visible y sorprendente: el usuario la
    // dejó a pantalla completa a propósito. Así que hace falta esto Y el modo "aplicar".
    public bool RestaurarAlOrganizar { get; set; }

    // Vacíos a propósito: una lista vacía es "no has puesto ninguna", no un hueco al que
    // haya que inventarle contenido.
    public Dictionary<string, string> Atajos { get; set; } = new();

    // Las reglas de D5. Tipadas: un `List<string>` obligaba a parsear a mano y a que cada
    // sitio se inventara su formato. Un config antiguo trae `"Reglas": []`, que deserializa
    // en una lista vacía sin migración ninguna.
    public List<HyprReglas.Regla> Reglas { get; set; } = new();
    public List<string> Espacios { get; set; } = new();
}

// El clima. Cuatro claves, y la primera manda sobre las otras tres.
//
// ---------- Por qué nace apagado ----------
//
// Es la única parte de NubeBar que saldría a un servidor de otro **por su cuenta**. El test de
// velocidad también sale a internet, pero porque el usuario pulsa un botón que dice «medir»;
// esto se refrescaría solo cada quince minutos. Eso se pide, no se asume — y pedirlo cuesta
// un interruptor en Ajustes y escribir una ciudad.
//
// ---------- Por qué se guardan las coordenadas ----------
//
// `Ciudad` es lo que escribe el usuario; `Latitud` y `Longitud` es lo que contestó el
// buscador. Guardarlas convierte la búsqueda en algo de una sola vez: al arrancar se pide el
// tiempo directamente, sin volver a pasar por el geocodificador. Si el usuario cambia de
// ciudad se ponen a cero y se busca otra vez (ver `MainWindow.Clima.cs`).
//
// Cero es «sin resolver». Las coordenadas 0,0 caen en el Golfo de Guinea, a 500 km de la
// costa, donde no vive nadie; el centinela honrado —`double?`— habría metido dos nulls en el
// JSON de todo el mundo para distinguir un caso que no existe.
public sealed class OpcionesClima
{
    public bool Activo { get; set; }                     // apagado de serie, a propósito
    public string Ciudad { get; set; } = "";
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}

public class Config
{
    // La versión del esquema de este fichero. Ver Esquema.cs: lo que la mantiene al día
    // son las migraciones, no esta propiedad — aquí está para que el modelo tipado no se
    // coma la clave y para poder preguntarla sin volver a abrir el árbol.
    public int Version { get; set; } = Esquema.Actual;

    public Tema Tema { get; set; } = new();
    public Barra Barra { get; set; } = new();
    public Modulos Modulos { get; set; } = new();
    public OpcionesWallpaper Wallpaper { get; set; } = new();
    public Inicio Inicio { get; set; } = new();
    public Animaciones Animaciones { get; set; } = new();
    public List<PanelNube> Paneles { get; set; } = new();
    public Fondo Fondo { get; set; } = new();
    public OpcionesHypr NubeHypr { get; set; } = new();
    public OpcionesClima Clima { get; set; } = new();
    public OpcionesNeon Neon { get; set; } = new();
    public OpcionesLaterales Laterales { get; set; } = new();

    // ---------- Un diagnóstico no escribe en los datos de nadie ----------
    //
    // Mientras corre un `--diag*`, la migración se hace EN MEMORIA y no se guarda. El
    // motivo es concreto y ya mordió: ejecutar un binario de desarrollo con un esquema
    // más nuevo migró el config real del usuario a una versión que su NubeBar instalada
    // —la estable, más antigua— ya no sabe migrar, y le dejaba un error en cada arranque.
    //
    // Migrar al actualizar es correcto y así se queda. Lo que no es correcto es que lo
    // haga una herramienta de medida, que por definición solo debería mirar. Con esto, no
    // depende de que nadie se acuerde de restaurar nada.
    public static bool SoloLectura;

    // ---------- D8: quién mira y no toca ----------
    //
    // Pura sobre los argumentos, para que la protección se pueda COMPROBAR. Antes esto era
    // un `is ... or ...` de seis líneas dentro de `App.OnStartup`, y la única forma de
    // probarlo era arrancar un binario de desarrollo y ver si el config del usuario
    // sobrevivía — que es justamente lo que no se puede hacer.
    //
    // ---------- La lista, ahora solo informativa ----------
    //
    // Desde el tercer accidente la decisión NO sale de esta lista, sale de `PuedeEscribir`
    // (más abajo) y está al revés: solo escriben la barra sin argumentos y el guardián.
    // Esta lista se queda porque sirve para leerla y para que las pruebas comprueben que
    // cada bandera documentada se comporta como dice — pero ya no es la que decide, y por
    // eso ya no se puede olvidar de nadie.
    //
    // La historia de por qué, que vale más que la lista:
    //
    // ---------- Por qué `--salir` está aquí ----------
    //
    // Antes NO estaba, con este razonamiento: "cierra la instancia que hay, y la que se
    // cierra sí puede querer guardar su estado". El razonamiento confundía dos procesos.
    // El que guarda su estado es el que SE CIERRA; el de `--salir` solo manda la señal y se
    // va. Y en ese "se va" pasa por `OnExit` → `CerrarIntegracionDeFondo` →
    // `Config.Cargar()`, que ejecuta las migraciones del esquema.
    //
    // Resultado medido durante D8: un `--salir` lanzado desde un binario de desarrollo
    // migró el config del usuario de la v3 a la v4 — la versión instalada, más antigua, ya
    // no sabría leerlo. Exactamente el fallo que `SoloLectura` existía para evitar, por una
    // puerta que nadie había mirado.
    //
    // `--salir` no necesita escribir nada: lee la configuración para saber qué hacer con
    // Wallpaper Engine y nada más. El guardián sí se queda fuera: es el vigilante y tiene
    // que poder trabajar.
    //
    // Y solo cuenta el PRIMER argumento, igual que el resto del arranque: un `--salir
    // --pruebas` no es una herramienta de medida, es un `--salir` con ruido detrás.
    public static readonly string[] BanderasQueMiran =
    {
        Pruebas.Bandera, Auditoria.Bandera,
        DiagVista.Bandera, DiagInicio.Bandera, DiagBandeja.Bandera, DiagHypr.Bandera,
        DiagMotor.Bandera, DiagEspacios.Bandera, DiagAtajos.Bandera, DiagReglas.Bandera,
        DiagD6.Bandera, DiagD7.Bandera, DiagPrefijos.Bandera,
        ArnesEspacios.Bandera, ArnesAplicar.Bandera, ArnesLayout.Bandera, DiagCapturas.Bandera,
        DiagVigilia.Bandera, DiagGuia.Bandera, DiagGuia.BanderaAlias, DiagE0.Bandera,
        // El de módulos SÍ escribe, pero en un arenero temporal suyo y nunca en el config
        // del usuario: lo impide `ConfigArbol.NoSeDebeGuardar`, que protege
        // `Config.RutaDelUsuario` y no se mueve con la redirección de la carpeta.
        DiagModulos.Bandera,
        // La rejilla abre ventanas suyas y mide la cuadrícula: no escribe nada.
        DiagRejilla.Bandera,
        // La ventanita abre un `Window` y se va; no lee ni escribe la configuración.
        DiagVentanita.Bandera,
        // Abre cada panel en cada posición de barra y mira si algo queda fuera. La
        // orientación la cambia en memoria y la devuelve; el fichero no se toca.
        DiagPaneles.Bandera,
        // El clima contra el servicio de verdad. Es el único que sale a internet, y aun así
        // no lee nada tuyo: la ciudad se la pasas tú o usa Madrid. El config, ni mirarlo.
        DiagClima.Bandera,
        // Fotografía el halo del neón a lo largo de dos ciclos. Fuerza el estilo en memoria
        // y lo devuelve; el fichero, como todas, ni lo toca.
        DiagNeon.Bandera,
        Salir.Bandera,
    };

    // ---------- Y la regla, al revés: solo la barra de verdad escribe ----------
    //
    // La primera versión de esto era una LISTA de banderas que no pueden escribir. Falló
    // tres veces en la misma sesión, y las tres por la misma razón: la lista hay que
    // acordarse de ampliarla.
    //
    //  1. `--salir` no estaba en la lista.
    //  2. Una bandera desconocida se caía por todas las ramas y arrancaba la barra entera.
    //  3. Al cerrar ese caso, el propio guardia pasaba por `OnExit` →
    //     `CerrarIntegracionDeFondo` → `Config.Cargar()`, y eso migra.
    //
    // El agujero no era ninguna de las tres: era la forma de la regla. **Cualquier** camino
    // que no sea el arranque normal acaba en `OnExit`, y `OnExit` lee la configuración. Así
    // que la pregunta correcta no es "¿esta bandera mira?" sino "¿es esto la barra?".
    //
    // Invertida, se mantiene sola: un diagnóstico nuevo nace protegido sin que nadie tenga
    // que acordarse de nada. Solo escriben dos cosas: la barra sin argumentos y el guardián.
    public static bool PuedeEscribir(string[]? args) =>
        args is not { Length: > 0 } ||
        string.Equals(args[0], Guardian.Bandera, StringComparison.OrdinalIgnoreCase);

    public static bool MiraYNoToca(string[]? args) => !PuedeEscribir(args);

    // ---------- Dónde vive la configuración ----------
    //
    // `CarpetaDelUsuario` es la de verdad y no la cambia nadie. `Carpeta` es la que usa
    // el programa, y normalmente son la misma.
    //
    // Se separan por un motivo concreto. Hay fallos —«añado un módulo y no aparece»—
    // que solo se reproducen recorriendo el camino entero: editar, GUARDAR, que salte la
    // recarga en caliente, remontar las zonas y mirar la pantalla. Un diagnóstico no
    // puede hacer eso contra el config del usuario, y sin escribir no reproduce nada.
    //
    // Así que un diagnóstico puede apuntar `Carpeta` a una carpeta temporal suya, y
    // entonces escribe de verdad —en su arenero—. La garantía que importa no se mueve:
    // `NoSeDebeGuardar` protege `RutaDelUsuario`, que es independiente de esto, así que
    // redirigir no abre ninguna puerta al fichero del usuario.
    internal static string? CarpetaDeArena;

    public static string CarpetaDelUsuario =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NubeBar");

    public static string RutaDelUsuario => Path.Combine(CarpetaDelUsuario, "config.json");

    public static string Carpeta => CarpetaDeArena ?? CarpetaDelUsuario;
    public static string Ruta => Path.Combine(Carpeta, "config.json");

    static readonly JsonSerializerOptions Opc = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,

        // Una entrada de `NubeHypr.Reglas` que no sea un objeto no puede tumbar la carga
        // del config entero. `Reglas` era `List<string>`: si alguien tiene cadenas ahí,
        // sin esto `Cargar` devolvería null y NubeBar arrancaría de serie — el usuario
        // perdería su configuración entera por una clave. Ver HyprReglas.LectorTolerante.
        Converters = { new HyprReglas.LectorTolerante() },
    };

    // Devuelve null si el JSON está mal escrito, así se mantiene el tema anterior
    public static Config? Cargar()
    {
        try
        {
            if (!File.Exists(Ruta))
            {
                Directory.CreateDirectory(Carpeta);
                // Instalación nueva: entra con el tema Nube puesto. A quien ya tiene
                // config.json no se le toca el tema, que es suyo.
                var recien = new Config();
                Temas.Aplicar(Temas.Nube, recien.Tema);
                File.WriteAllText(Ruta, JsonSerializer.Serialize(recien, Opc));
                Log.Info("config nuevo creado con el tema Nube");
            }

            // ---------- Las migraciones, antes de deserializar ----------
            //
            // Van aquí y no después por un motivo concreto: el modelo tipado solo ve las
            // claves que conoce, así que migrar sobre el objeto deserializado no podría
            // ni leer ni conservar lo que esta versión no entiende. `Esquema` trabaja
            // sobre el árbol JSON, que lo conserva todo.
            //
            // Solo escribe si de verdad hay algo que migrar, y si falla no escribe nada:
            // entonces se sigue adelante con el fichero tal cual, que es lo que había
            // antes de existir esto y funciona. Ver Esquema.MigrarFichero.
            if (!SoloLectura) Esquema.MigrarFichero(Ruta);
            var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(Ruta), Opc);
            if (c == null) return null;

            // Un "Barra": null explícito en el JSON SÍ deja la propiedad a null: los
            // inicializadores = new() solo valen cuando la clave no está. Y como el
            // resto del programa da por hecho que estas secciones existen, un null
            // aquí mataba el arranque antes incluso de que hubiera log. Se normaliza.
            c.Tema ??= new();
            c.Barra ??= new();
            c.Modulos ??= new();
            c.Wallpaper ??= new();
            c.Fondo ??= new();
            c.Fondo.PorMonitor ??= new();
            c.Clima ??= new();
            c.Clima.Ciudad ??= "";
            c.Neon ??= new();
            c.Neon.Color ??= "";
            c.Laterales ??= new();
            c.Laterales.Izquierda ??= new();
            c.Laterales.Derecha ??= new();
            if (c.Version < 1) c.Version = Esquema.Actual;
            c.Inicio ??= new();
            c.Barra.AppsFijadas ??= new();
            c.Barra.OrdenApps ??= new();
            c.Barra.NotificacionesSilenciadas ??= new();
            c.Modulos.Izquierda ??= new();
            c.Modulos.Centro ??= new();
            c.Modulos.Derecha ??= new();
            c.Inicio.Fijadas ??= new();
            c.Modulos.Ocultos ??= new();
            c.Animaciones ??= new();
            c.Paneles ??= new();
            ModulosDeSerie(c);
            if (MigrarYDerivarPaneles(c)) GuardarPanelesMigrados(c);

            // ---------- El modo de demostración, aquí y en ningún otro sitio ----------
            //
            // Hay media docena de sitios que llaman a `Cargar()`: la isla, Ajustes, los
            // monitores, el wallpaper. Si cada uno tuviera que acordarse de apagar NubeHypr
            // en modo demostración, el que se olvidara sería el que lo encendiera — y sería
            // el único que importa. Puesto aquí, no hay forma de saltárselo.
            DiagGuia.Apagar(c);
            return c;
        }
        catch (Exception ex)
        {
            // Sin esto, un solo valor con el tipo equivocado ("Alto": "38") dejaba la
            // isla con el tema de fábrica y ni una palabra en el log
            Log.Error("leer config.json (se intenta rescatar de una copia)", ex);
            return Rescatar();
        }

    }

    // ---------- D7: el rescate ----------
    //
    // Antes, un config ilegible significaba arrancar con la configuración de serie: tema de
    // fábrica, módulos de fábrica, y el trabajo del usuario aparentemente perdido. El
    // fichero seguía ahí y los respaldos también, pero el usuario no lo sabía — lo que veía
    // era su escritorio en blanco.
    //
    // Ahora se busca la copia más reciente que se pueda leer y se usa ESA. El fichero roto
    // **no se borra y no se sobrescribe**: se queda para que se pueda mirar, y lo rescatado
    // vive solo en memoria hasta que el usuario guarde algo. Un rescate que escribe por su
    // cuenta puede estropear el fichero bueno.
    //
    // Ver HyprRescate.cs.
    static Config? Rescatar()
    {
        try
        {
            foreach (var linea in HyprRescate.Informe(Ruta)) Log.Info(linea);

            var r = HyprRescate.Cargar(Ruta);
            if (r == null)
            {
                Log.Info("[D7]  no se pudo rescatar nada: se usa la configuración de serie");
                return null;
            }

            // El respaldo puede ser de un esquema anterior, así que se migra **en memoria**.
            // `Esquema.Migrar` trabaja sobre el árbol, que conserva las claves que esta
            // versión no conoce: rescatar no puede ser una excusa para perderlas.
            var resultado = Esquema.Migrar(r.Arbol);
            var c = JsonSerializer.Deserialize<Config>(r.Arbol.ToJsonString(), Opc);
            if (c == null)
            {
                Log.Info("[D7]  el respaldo tampoco se deja leer como configuración");
                return null;
            }

            Log.Info($"[D7]  rescatado de {r.DeDonde} (v{r.Version} -> v{c.Version}, " +
                     $"{resultado.Resumen}, en memoria). " +
                     "El fichero roto se queda como está.");
            // También por el camino del rescate: un config roto no puede ser la rendija por
            // la que la demostración arranca con NubeHypr encendido.
            DiagGuia.Apagar(c);
            return c;
        }
        catch (Exception ex)
        {
            Log.Error("rescatar el config de una copia", ex);
            return null;
        }
    }

    // ================= Paneles: la autoridad =================
    //
    // Se le ha dado la vuelta a lo que decidió la fase A. Ahora **manda Paneles**: es
    // lo que vive en el fichero, lo que reciben las escrituras y lo que define bordes,
    // posición, grosor, visibilidad y en qué monitores sale la nube.
    //
    // Pero `Barra` no desaparece: se convierte en una VISTA DERIVADA en memoria, que
    // se rehace en cada carga a partir de Paneles[0]. El motivo es práctico y vale la
    // pena escribirlo: hay 64 lecturas de la geometría de Barra repartidas por once
    // ficheros (EsIsla, Arriba, _cfg.Barra.Alto, la appbar, el arrastre, la pantalla
    // completa…). Reescribirlas todas no habría dado ni una garantía nueva y habría
    // puesto en riesgo cosas que llevan meses funcionando. Invertir la autoridad en el
    // LÍMITE DE CARGA consigue lo mismo sin tocar a ningún lector.
    //
    // Lo que sí queda en Barra como suyo son los ajustes que no son del panel:
    // MaxApps, AgruparApps, Bandeja, Notificaciones, AppsFijadas, PulsoAudio…
    //
    // Devuelve true si ha habido que MIGRAR (no existía Paneles): entonces hay que
    // escribirlo en el fichero, que es lo que hace GuardarPanelesMigrados.
    internal static bool MigrarYDerivarPaneles(Config c)
    {
        bool migrado = false;
        try
        {
            var p = c.Paneles.FirstOrDefault(x => x.Id == "principal");
            if (p == null)
            {
                // Primera vez con esta versión: el panel nace de lo que decía Barra,
                // así que nadie nota el cambio.
                p = new PanelNube
                {
                    Id = "principal",
                    Activo = true,
                    Modo = c.Barra.Modo,
                    Posicion = c.Barra.Posicion,
                    Grosor = c.Barra.Alto,
                    Margen = c.Barra.Margen,
                    // Sin elegir, la nube flota (ver PanelNube.ReservaPorDefecto).
                    ReservarEspacio = null,
                    Monitores = c.Barra.Monitores,
                    Visibilidad = "siempre"
                };
                c.Paneles.Insert(0, p);
                migrado = true;
                Log.Info("Paneles no existía: migrado desde Barra " +
                         $"(modo={p.Modo} posicion={p.Posicion} grosor={p.Grosor})");
            }

            p.Activo = true;
            if (!PanelNube.PosicionValida(p.Posicion))
            {
                Log.Info($"posición \"{p.Posicion}\" desconocida: se usa arriba");
                p.Posicion = "arriba";
            }
            if (!PanelNube.VisibilidadValida(p.Visibilidad))
            {
                Log.Info($"visibilidad \"{p.Visibilidad}\" desconocida: se usa siempre");
                p.Visibilidad = "siempre";
            }

            // Las zonas siguen viviendo en Modulos: son del usuario y las edita la
            // biblioteca. El panel las referencia, no las copia.
            p.Zonas = new ZonasPanel
            {
                Izquierda = c.Modulos.Izquierda,
                Centro = c.Modulos.Centro,
                Derecha = c.Modulos.Derecha,
                Ocultos = c.Modulos.Ocultos
            };

            // Y ahora al revés que antes: Barra se deriva del panel.
            c.Barra.Modo = p.Modo;
            c.Barra.Posicion = p.Posicion;
            c.Barra.Alto = p.Grosor;
            c.Barra.Margen = p.Margen;
            // Barra.ReservarEspacio es un bool de toda la vida y lo leen otros sitios:
            // aquí se le da la respuesta YA RESUELTA, con el defecto de la orientación
            // aplicado si el usuario no ha elegido.
            c.Barra.ReservarEspacio = PanelNube.ReservaEfectiva(p.ReservarEspacio);
            c.Barra.Monitores = p.Monitores;
        }
        catch (Exception ex) { Log.Error("migrar y derivar Paneles", ex); }
        return migrado;
    }

    // Escribe Paneles en el fichero y borra de Barra las claves que se ha llevado.
    // Borrarlas es lo que evita tener la misma información en dos sitios: con las dos,
    // el primero que editara "Barra.Alto" a mano no entendería por qué no pasa nada.
    static void GuardarPanelesMigrados(Config c)
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            arbol.PonerPaneles(c.Paneles);
            foreach (var clave in new[] { "Modo", "Posicion", "Alto", "Margen", "ReservarEspacio", "Monitores" })
                arbol.Quitar("Barra", clave);
            arbol.Guardar();
            Log.Info("Paneles guardado en el config; las claves de geometría salen de Barra");
        }
        catch (Exception ex) { Log.Error("guardar Paneles migrados", ex); }
    }

    // Los módulos "control" (red, volumen, idioma, batería) y "bandeja" (las apps en
    // segundo plano, tras el "^") vienen de serie. Las configs de antes tienen la
    // lista de la derecha escrita entera, así que el valor por defecto no les llega
    // nunca: se añade cada uno UNA vez y se apunta. Si luego lo quitas en Ajustes,
    // no vuelve a aparecer.
    //
    // Se escribe con ConfigArbol para no perder las claves que NubeBar no conoce.
    // Guardar dispara la recarga en caliente, que lee ya la bandera puesta: no hay bucle.
    static void ModulosDeSerie(Config c)
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            bool tocado = false;

            // "control" delante de la fecha
            if (!arbol.Bandera("Modulos", "ControlDeSerie", false))
            {
                if (AnadirUnaVez(c, arbol, "control", antesDe: "fecha"))
                    Log.Info("módulo control añadido a la derecha (viene de serie)");
                arbol.Poner("Modulos", "ControlDeSerie", true);
                c.Modulos.ControlDeSerie = tocado = true;
            }

            // "bandeja" delante de "control". Quien no la tenía puesta nunca ha
            // elegido cuántos iconos ver, así que empieza como los iconos ocultos de
            // Windows: solo el "^", y dentro todas las apps en segundo plano
            if (!arbol.Bandera("Modulos", "BandejaDeSerie", false))
            {
                if (AnadirUnaVez(c, arbol, "bandeja", antesDe: "control"))
                {
                    arbol.Poner("Barra", "MaxBandeja", 0);
                    c.Barra.MaxBandeja = 0;
                    Log.Info("módulo bandeja añadido a la derecha (viene de serie), solo con el \"^\"");
                }
                arbol.Poner("Modulos", "BandejaDeSerie", true);
                c.Modulos.BandejaDeSerie = tocado = true;
            }

            // "fijadas" (los iconos de las apps que has fijado) va en el CENTRO, delante
            // del reloj, y no a la izquierda con el carrusel: el centro es la única zona
            // que no se mueve al expandirse la isla, así que el icono se queda en el
            // mismo sitio y se puede pulsar sin que se escape (ver MainWindow.Fijadas.cs)
            if (!arbol.Bandera("Modulos", "FijadasDeSerie", false))
            {
                if (AnadirUnaVez(c, arbol, "fijadas", antesDe: "reloj", zona: "Centro"))
                    Log.Info("módulo fijadas añadido al centro (viene de serie)");
                arbol.Poner("Modulos", "FijadasDeSerie", true);
                c.Modulos.FijadasDeSerie = tocado = true;
            }

            // Y el carrusel de apps abiertas se muda al centro por el mismo motivo que
            // las fijadas: en la izquierda desaparecía con la isla recogida y había que
            // desplegarla para ver qué tienes abierto. Detrás de las fijadas, como en la
            // barra de tareas. Si lo devuelves a la izquierda, se queda donde lo pongas.
            if (!arbol.Bandera("Modulos", "AppsAlCentro", false))
            {
                if (MoverUnaVez(c, arbol, "apps", zona: "Centro", antesDe: "reloj"))
                    Log.Info("módulo apps movido al centro (así se ve con la isla recogida)");
                arbol.Poner("Modulos", "AppsAlCentro", true);
                c.Modulos.AppsAlCentro = tocado = true;
            }

            // La campana de notificaciones, delante de la bandeja: el mismo sitio que
            // ocupa en Plasma y en Windows, y el orden en que se miran las cosas
            // —primero si hay algo que leer, después los iconos de segundo plano—.
            // Si la quitas de la isla, la bandera se queda puesta y no vuelve.
            if (!arbol.Bandera("Modulos", "NotificacionesDeSerie", false))
            {
                if (AnadirUnaVez(c, arbol, "notificaciones", antesDe: "bandeja"))
                    Log.Info("módulo notificaciones añadido a la derecha (viene de serie)");
                arbol.Poner("Modulos", "NotificacionesDeSerie", true);
                c.Modulos.NotificacionesDeSerie = tocado = true;
            }

            // ---------- El botón del centro de control, a la izquierda ----------
            //
            // Nació dentro del módulo "control", que vive en la zona DERECHA con la red,
            // el volumen y la batería. Y el panel cuelga de su botón, así que salía
            // desplegado en la mitad derecha de la pantalla. Es su propio módulo y va a
            // la izquierda, al lado del lanzador, que es de donde se espera que salga el
            // centro de control: se abre hacia dentro de la pantalla desde la esquina.
            //
            // Si lo quitas de la isla, la bandera se queda puesta y no vuelve solo.
            if (!arbol.Bandera("Modulos", "CentroALaIzquierda", false))
            {
                if (AnadirUnaVez(c, arbol, "centro", antesDe: "", zona: "Izquierda"))
                    Log.Info("módulo centro añadido a la izquierda (al lado del lanzador)");
                arbol.Poner("Modulos", "CentroALaIzquierda", true);
                c.Modulos.CentroALaIzquierda = tocado = true;
            }

            // ---------- Y se retira ----------
            //
            // El botón propio del centro de control existió un rato y se retiró: su
            // superficie es la portada de Inicio y se abre desde el botón Inicio, así que
            // un segundo botón para lo mismo era una entrada duplicada. Si se queda en el
            // config, la isla dibuja un hueco de "módulo desconocido".
            if (!arbol.Bandera("Modulos", "CentroRetirado", false))
            {
                if (QuitarUnaVez(c, arbol, "centro"))
                    Log.Info("módulo centro retirado: su superficie es la portada de Inicio");
                arbol.Poner("Modulos", "CentroRetirado", true);
                c.Modulos.CentroRetirado = tocado = true;
            }

            if (tocado) arbol.Guardar();
        }
        catch (Exception ex) { Log.Error("añadir los módulos de serie", ex); }
    }

    // Lo mete en la zona que se le diga, delante de "antesDe" (o al final), si no está
    // ya en ninguna de las tres. Devuelve si lo ha añadido.
    static bool AnadirUnaVez(Config c, ConfigArbol arbol, string modulo, string antesDe, string zona = "Derecha")
    {
        bool yaEsta = c.Modulos.Izquierda.Concat(c.Modulos.Centro).Concat(c.Modulos.Derecha)
                       .Any(m => m.Equals(modulo, StringComparison.OrdinalIgnoreCase));
        if (yaEsta) return false;
        var lista = zona switch
        {
            "Izquierda" => c.Modulos.Izquierda,
            "Centro"    => c.Modulos.Centro,
            _           => c.Modulos.Derecha
        };
        int donde = lista.FindIndex(m => m.Equals(antesDe, StringComparison.OrdinalIgnoreCase));
        lista.Insert(donde < 0 ? lista.Count : donde, modulo);
        arbol.PonerLista("Modulos", zona, lista);
        return true;
    }


    // Lo quita de donde esté, una vez. Hace falta para retirar limpiamente un módulo que
    // se añadió de serie y luego se retiró del programa: si se deja en el config, la isla
    // dibuja un hueco de "módulo desconocido" donde no hay nada que dibujar.
    static bool QuitarUnaVez(Config c, ConfigArbol arbol, string modulo)
    {
        bool tocado = false;
        foreach (var (nombre, lista) in new (string, List<string>)[]
        {
            ("Izquierda", c.Modulos.Izquierda),
            ("Centro",    c.Modulos.Centro),
            ("Derecha",   c.Modulos.Derecha),
        })
        {
            if (lista.RemoveAll(m => m.Equals(modulo, StringComparison.OrdinalIgnoreCase)) == 0) continue;
            arbol.PonerLista("Modulos", nombre, lista);
            tocado = true;
        }
        return tocado;
    }
    // Lo cambia de zona, una vez. A diferencia de AnadirUnaVez, si no lo tienes puesto
    // en ningún sitio NO se mete: lo habrás quitado tú y no es cosa nuestra devolverlo.
    static bool MoverUnaVez(Config c, ConfigArbol arbol, string modulo, string zona, string antesDe)
    {
        var zonas = new (string Nombre, List<string> Lista)[]
        {
            ("Izquierda", c.Modulos.Izquierda),
            ("Centro",    c.Modulos.Centro),
            ("Derecha",   c.Modulos.Derecha)
        };
        var destino = zonas.First(z => z.Nombre == zona).Lista;

        bool estaba = false;
        foreach (var z in zonas)
        {
            if (ReferenceEquals(z.Lista, destino)) continue;
            int i = z.Lista.FindIndex(m => m.Equals(modulo, StringComparison.OrdinalIgnoreCase));
            if (i < 0) continue;
            z.Lista.RemoveAt(i);
            arbol.PonerLista("Modulos", z.Nombre, z.Lista);
            estaba = true;
        }
        if (!estaba) return false;      // o no lo tienes, o ya estaba donde queremos

        int donde = destino.FindIndex(m => m.Equals(antesDe, StringComparison.OrdinalIgnoreCase));
        destino.Insert(donde < 0 ? destino.Count : donde, modulo);
        arbol.PonerLista("Modulos", zona, destino);
        return true;
    }
}
