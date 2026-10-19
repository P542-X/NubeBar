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
            if (File.Exists(Ruta) && new FileInfo(Ruta).Length > 512_000) File.Delete(Ruta);
        }
        catch { }
        Info("==== " + Version + " ====");
    }

    // El guardián es otro proceso y escribe en este mismo archivo, así que de vez en
    // cuando los dos lo abren a la vez. Se reintenta un par de veces en lugar de
    // perder la línea: justo las del guardián son las que hacen falta para saber
    // qué pasó cuando NubeBar murió.
    public static void Info(string mensaje)
    {
        var linea = $"{DateTime.Now:HH:mm:ss.fff} {mensaje}{Environment.NewLine}";
        for (int intento = 0; intento < 3; intento++)
        {
            try { File.AppendAllText(Ruta, linea); return; }
            catch (IOException) { System.Threading.Thread.Sleep(15); }
            catch { return; }
        }
    }

    public static void Error(string donde, object error) => Info($"ERROR en {donde}: {error}");
}
