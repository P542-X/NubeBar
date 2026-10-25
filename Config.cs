using System;
using System.Collections.Generic;
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

public class OpcionesWallpaper
{
    public string? Ruta { get; set; }        // vacío = detectar automáticamente desde Steam
    public int Monitor { get; set; } = 0;    // monitor del que se leen los colores
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
    // Ctrl+Alt+Space estaba ocupado en el equipo de desarrollo y el registro fallaba
    // con el error 1409. Ctrl+Alt+W ("W de ventanas") sí está libre, y no choca con
    // Ctrl+Shift+Space, que en Visual Studio es la ayuda de parámetros.
    public string AtajoVistaGeneral { get; set; } = "Ctrl+Alt+W";       // abre la cuadrícula de ventanas
    public string Monitores { get; set; } = "todos";   // "todos" | "principal" (DERIVADA de Paneles, ver arriba)
    public bool AppsPorMonitor { get; set; } = true;   // cada isla enseña las apps de SU monitor
    public bool OcultarEnPantallaCompleta { get; set; } = true;
    // Una ventana maximizada también la echa. Ya no viene de serie: la isla iba y
    // venía cada vez que pasabas de un Excel maximizado a otra cosa. Para quitarla
    // de en medio está "Esconder la isla" en el clic derecho (vuelve con Win).
    public bool OcultarConMaximizada { get; set; } = false;
    public bool Bandeja { get; set; } = true;        // iconos de la bandeja del sistema en la isla
    public int MaxBandeja { get; set; } = 0;         // cuántos se ven sin abrir el desplegable "^" (0 = solo el "^")
    public bool Notificaciones { get; set; } = true; // las notificaciones asoman en la isla
    public bool AppsDelEscritorio { get; set; } = false;   // el carrusel, solo con las del escritorio virtual actual
    public List<string> AppsFijadas { get; set; } = new();  // apps siempre presentes en el carrusel
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

public class Config
{
    public Tema Tema { get; set; } = new();
    public Barra Barra { get; set; } = new();
    public Modulos Modulos { get; set; } = new();
    public OpcionesWallpaper Wallpaper { get; set; } = new();
    public Inicio Inicio { get; set; } = new();
    public Animaciones Animaciones { get; set; } = new();
    public List<PanelNube> Paneles { get; set; } = new();

    public static string Carpeta =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NubeBar");
    public static string Ruta => Path.Combine(Carpeta, "config.json");

    static readonly JsonSerializerOptions Opc = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
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
            return c;
        }
        catch (Exception ex)
        {
            // Sin esto, un solo valor con el tipo equivocado ("Alto": "38") dejaba la
            // isla con el tema de fábrica y ni una palabra en el log
            Log.Error("leer config.json (se usa la configuración de fábrica)", ex);
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
