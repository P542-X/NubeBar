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

    public static void Info(string mensaje)
    {
        try { File.AppendAllText(Ruta, $"{DateTime.Now:HH:mm:ss.fff} {mensaje}{Environment.NewLine}"); }
        catch { }
    }

    public static void Error(string donde, object error) => Info($"ERROR en {donde}: {error}");
}
