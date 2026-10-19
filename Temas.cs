using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NubeBar;

public sealed record TemaGuardado(string Nombre, string Fondo, string Texto, string Acento, double Radio);

// Temas listos para usar y guardado/carga de los tuyos en %APPDATA%\NubeBar\temas
public static class Temas
{
    public static string Carpeta => Path.Combine(Config.Carpeta, "temas");

    public static readonly TemaGuardado[] Predefinidos =
    {
        new("Negro iPhone",      "#E6000000", "#FFFFFF", "#7AA2F7", 20),
        new("Tokyo Night",       "#E61A1B26", "#C0CAF5", "#7AA2F7", 18),
        new("Catppuccin Mocha",  "#E61E1E2E", "#CDD6F4", "#CBA6F7", 20),
        new("Nord",              "#E62E3440", "#ECEFF4", "#88C0D0", 16),
        new("Dracula",           "#E6282A36", "#F8F8F2", "#BD93F9", 18),
        new("Gruvbox",           "#E6282828", "#EBDBB2", "#FABD2F", 14)
    };

    public static void Exportar(TemaGuardado tema, string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, JsonSerializer.Serialize(tema, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static TemaGuardado? Importar(string ruta)
    {
        try { return JsonSerializer.Deserialize<TemaGuardado>(File.ReadAllText(ruta)); }
        catch (Exception ex) { Log.Error("importar tema", ex); return null; }
    }

    public static List<TemaGuardado> Mios()
    {
        var lista = new List<TemaGuardado>();
        try
        {
            if (!Directory.Exists(Carpeta)) return lista;
            foreach (var f in Directory.EnumerateFiles(Carpeta, "*.json"))
                if (Importar(f) is { } t) lista.Add(t);
        }
        catch { }
        return lista;
    }
}
