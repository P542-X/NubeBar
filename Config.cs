using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
}

public class OpcionesWallpaper
{
    public string? Ruta { get; set; }        // vacío = detectar automáticamente desde Steam
    public int Monitor { get; set; } = 0;    // monitor del que se leen los colores
}

public class Barra
{
    public string Modo { get; set; } = "isla";        // "isla" | "barra"
    public string Posicion { get; set; } = "arriba";  // "arriba" | "abajo"
    public double Alto { get; set; } = 38;
    public double Margen { get; set; } = 8;
    public bool OcultarBarraWindows { get; set; } = true;
    public bool ReservarEspacio { get; set; } = true;  // las ventanas maximizadas no la tapan
    public int MaxApps { get; set; } = 10;             // iconos de apps visibles a la vez; el resto, con la rueda
    public bool AgruparApps { get; set; } = true;      // las ventanas del mismo programa, en un solo icono
    // Ctrl+Alt+Space estaba ocupado en el equipo de desarrollo y el registro fallaba
    // con el error 1409. Ctrl+Alt+W ("W de ventanas") sí está libre, y no choca con
    // Ctrl+Shift+Space, que en Visual Studio es la ayuda de parámetros.
    public string AtajoVistaGeneral { get; set; } = "Ctrl+Alt+W";       // abre la cuadrícula de ventanas
    public string Monitores { get; set; } = "todos";   // "todos" | "principal"
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
                File.WriteAllText(Ruta, JsonSerializer.Serialize(new Config(), Opc));
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
            ModulosDeSerie(c);
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
