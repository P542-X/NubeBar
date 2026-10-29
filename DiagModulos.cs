using System;
using System.IO;

namespace NubeBar;

// `NubeBar.exe --diagmodulos`: añadir un módulo y comprobar que SALE.
//
// ---------- Por qué hace falta ----------
//
// El fallo que se persigue es «añado «escritorios» desde la barra de edición, la interfaz
// dice que sí, y el módulo no aparece». Tiene una propiedad desagradable: todo lo que se
// puede comprobar con una prueba pura pasa. El catálogo lo conoce. `ZonasWidgets.Anadir` lo
// añade. El árbol lo escribe. El fichero lo guarda. `Config.Cargar` lo lee. Y en pantalla no
// está.
//
// Lo que falla vive en la juntura, y la juntura es el camino entero:
//
//     editar → guardar → FileSystemWatcher → recarga en caliente → `Aplicar`
//           → `RemontarZonas` → `LlenarZonas` → el StackPanel de la zona
//
// Seis saltos, tres de ellos asíncronos y uno a través del disco. No se recorre sin una
// barra de verdad corriendo, y por eso esto es un diagnóstico y no una prueba.
//
// ---------- Qué comprueba, y contra qué ----------
//
// Contra **el árbol visual**, nunca contra el modelo. El modelo siempre va a decir que el
// módulo está puesto: el modelo es exactamente lo que se acaba de editar; preguntarle es
// preguntarle a la respuesta. La pregunta de verdad —¿llegó a la pantalla?— solo la contesta
// la pantalla, y la contesta desde que cada bloque montado lleva su id en
// `AutomationProperties.AutomationId` (ver `MainWindow.IdsMontadosEn`).
//
// Y comprueba la cadena entera para cada módulo del catálogo:
//
//   1. añadir a una zona      → sale, en esa zona, al final
//   2. mover a otra zona      → sale en la nueva y ya no en la vieja
//   3. mover dentro de la zona→ cambia de sitio, y el orden es el que se pidió
//   4. ocultar / mostrar      → desaparece y vuelve A SU SITIO, no al final
//   5. quitar                 → desaparece de las tres
//   6. recargar desde cero    → lo de arriba sobrevive a releer el fichero
//
// En horizontal y en vertical, que es donde se pidió que «escritorios» se dibuje bien.
//
// ---------- Y por qué no toca nada tuyo ----------
//
// Reproducir esto EXIGE escribir el config: si no se guarda, no salta la recarga en caliente
// y no se recorre el camino que falla. Así que el diagnóstico se monta un arenero:
//
//   · crea una carpeta temporal propia y apunta `Config.CarpetaDeArena` ahí ANTES de que
//     nadie lea la configuración;
//   · escribe un `config.json` de fábrica dentro, no una copia del tuyo;
//   · `ConfigArbol.NoSeDebeGuardar` sigue protegiendo `Config.RutaDelUsuario`, que no se
//     mueve con la redirección: ni aunque algo se equivocara podría escribir tu fichero;
//   · al terminar toma la huella de tu config y la compara con la de antes de empezar, y
//     lo dice en el informe;
//   · y borra el arenero.
//
// No mueve ventanas, no registra atajos, no migra nada tuyo.
public static class DiagModulos
{
    internal const string Bandera = "--diagmodulos";

    public static bool Encendido;

    // La huella del config del usuario al arrancar, para poder jurar al final que sigue igual.
    public static string HuellaDeEntrada = "";

    public static string Arenero = "";

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], Bandera, StringComparison.OrdinalIgnoreCase))
            return;
        Encendido = true;

        HuellaDeEntrada = Huella(Config.RutaDelUsuario);

        // El arenero se monta AQUÍ, antes de que nada lea la configuración: si se montara
        // más tarde, el arranque ya habría leído (y podría haber migrado) el fichero bueno.
        try
        {
            Arenero = Path.Combine(Path.GetTempPath(),
                                   "nubebar-arenero-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(Arenero);
            Config.CarpetaDeArena = Arenero;
            File.WriteAllText(Path.Combine(Arenero, "config.json"), ConfigDeFabrica);
        }
        catch (Exception ex)
        {
            // Sin arenero no se hace el diagnóstico: lo que NO se hace es seguir adelante
            // escribiendo en el config de verdad.
            Encendido = false;
            Config.CarpetaDeArena = null;
            Log.Error("preparar el arenero de --diagmodulos (no se ejecuta)", ex);
        }
    }

    // Un config mínimo y reconocible. A propósito con las tres zonas pobladas y con los
    // "de serie" ya marcados: así `ModulosDeSerie` no inyecta nada y lo que se mide es lo
    // que hace el diagnóstico, no lo que hace la migración.
    const string ConfigDeFabrica = """
    {
      "Modulos": {
        "Izquierda": [ "inicio" ],
        "Centro": [ "fijadas", "apps", "reloj" ],
        "Derecha": [ "cpu", "ram", "bandeja", "control", "fecha" ],
        "Ocultos": [],
        "ControlDeSerie": true,
        "BandejaDeSerie": true,
        "FijadasDeSerie": true,
        "AppsAlCentro": true,
        "NotificacionesDeSerie": true,
        "CentroALaIzquierda": true,
        "CentroRetirado": true
      },
      "Paneles": [
        { "Id": "principal", "Activo": true, "Modo": "barra", "Posicion": "arriba",
          "Grosor": 38, "Margen": 8, "Visibilidad": "siempre", "Monitores": "principal" }
      ],
      "Barra": {
        "OcultarBarraWindows": false,
        "AppsFijadas": [ "C:\\Windows\\explorer.exe" ]
      },
      "Wallpaper": { "Integrar": false, "IniciarConNubeBar": false },
      "Animaciones": { "RobotVisto": true, "Nivel": "reducido", "Robot": "nunca" },
      "Version": 3
    }
    """;

    // Tres decisiones de ese config que no son estéticas:
    //
    //  · `OcultarBarraWindows: false` — el diagnóstico no esconde la barra de Windows. Si
    //    reventara a la mitad, el usuario se quedaría sin ninguna barra, que es exactamente
    //    el accidente del que salió la guarda de `Fase`.
    //  · `Robot: "nunca"` — la escena de entrada tapa la barra unos segundos y aquí se mide
    //    lo que hay en la barra.
    //  · `Wallpaper` apagado — no se arranca Wallpaper Engine para esto.
    //  · una app anclada (`explorer.exe`, que está en todos los Windows) — sin ninguna, el
    //    módulo "fijadas" mide cero a propósito, para no descentrar el reloj, y entonces el
    //    diagnóstico no comprueba nada de él. Con una, se comprueba que dibuja su icono.

    public static string Huella(string ruta)
    {
        try
        {
            if (!File.Exists(ruta)) return "(no existe)";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(File.ReadAllBytes(ruta));
            return Convert.ToHexString(bytes)[..16] + $" ({new FileInfo(ruta).Length} bytes)";
        }
        catch (Exception ex) { return "(no se puede leer: " + ex.GetType().Name + ")"; }
    }

    public static void RecogerElArenero()
    {
        try
        {
            if (Arenero.Length > 0 && Directory.Exists(Arenero)) Directory.Delete(Arenero, true);
        }
        catch (Exception ex) { Log.Error("borrar el arenero de --diagmodulos", ex); }
    }
}
