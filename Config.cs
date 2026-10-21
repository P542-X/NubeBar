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
    // Una ventana maximizada también la echa. Va activado de serie: quien juega o ve
    // un vídeo en ventana maximizada no tiene por qué ver la isla encima. A cambio,
    // se aparta también al maximizar el navegador; se quita en Ajustes > Isla.
    public bool OcultarConMaximizada { get; set; } = true;
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
    public List<string> Izquierda { get; set; } = new() { "inicio", "apps" };
    public List<string> Centro { get; set; } = new() { "reloj" };
    public List<string> Derecha { get; set; } = new() { "cpu", "ram", "bateria", "bandeja", "control", "fecha" };
    // Ya se pusieron "control" y "bandeja" una vez en una config de antes (ver Config.ModulosDeSerie)
    public bool ControlDeSerie { get; set; } = true;
    public bool BandejaDeSerie { get; set; } = true;
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

            if (tocado) arbol.Guardar();
        }
        catch (Exception ex) { Log.Error("añadir los módulos de serie", ex); }
    }

    // Lo mete en la derecha delante de "antesDe" (o al final) si no está en ninguna
    // zona. Devuelve si lo ha añadido.
    static bool AnadirUnaVez(Config c, ConfigArbol arbol, string modulo, string antesDe)
    {
        bool yaEsta = c.Modulos.Izquierda.Concat(c.Modulos.Centro).Concat(c.Modulos.Derecha)
                       .Any(m => m.Equals(modulo, StringComparison.OrdinalIgnoreCase));
        if (yaEsta) return false;
        var der = c.Modulos.Derecha;
        int donde = der.FindIndex(m => m.Equals(antesDe, StringComparison.OrdinalIgnoreCase));
        der.Insert(donde < 0 ? der.Count : donde, modulo);
        arbol.PonerLista("Modulos", "Derecha", der);
        return true;
    }
}
