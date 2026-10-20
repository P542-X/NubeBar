using System;
using System.IO;

namespace NubeBar;

// Escribe en %APPDATA%\NubeBar\nubebar.log lo que pasa al arrancar y cualquier error
static class Log
{
    public const string Version = "NubeBar v5";
    static string Ruta => Path.Combine(Config.Carpeta, "nubebar.log");

    public static void Nuevo()
    {
        try
        {
            Directory.CreateDirectory(Config.Carpeta);
            Rotar();
        }
        catch { }
        Info("==== " + Version + " ====");
    }

    // Antes solo se miraba el tamaño al arrancar: en una sesión larga (NubeBar
    // arranca con Windows y no se cierra en días) el log crecía sin tope. Y al rotar
    // se borraba, justo cuando hace falta para investigar lo que acaba de pasar:
    // ahora la vuelta anterior se conserva en .1.
    const long Tope = 512_000;
    static int _desdeQueMire;

    static void Rotar()
    {
        if (!File.Exists(Ruta) || new FileInfo(Ruta).Length <= Tope) return;
        var viejo = Ruta + ".1";
        if (File.Exists(viejo)) File.Delete(viejo);
        File.Move(Ruta, viejo);
    }

    // El guardián es otro proceso y escribe en este mismo archivo, así que de vez en
    // cuando los dos lo abren a la vez. Se reintenta un par de veces en lugar de
    // perder la línea: justo las del guardián son las que hacen falta para saber
    // qué pasó cuando NubeBar murió.
    public static void Info(string mensaje)
    {
        var linea = $"{DateTime.Now:HH:mm:ss.fff} {mensaje}{Environment.NewLine}";
        // Mirar el tamaño en cada línea costaría un FileInfo por línea; cada 200 basta
        if (++_desdeQueMire >= 200)
        {
            _desdeQueMire = 0;
            try { Rotar(); } catch { }
        }
        for (int intento = 0; intento < 8; intento++)
        {
            try { File.AppendAllText(Ruta, linea); return; }
            catch (IOException) { System.Threading.Thread.Sleep(10 * (intento + 1)); }
            catch { return; }
        }
    }


    public static void Error(string donde, object error) => Info($"ERROR en {donde}: {error}");
}
