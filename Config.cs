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
}

public class Modulos
{
    public List<string> Izquierda { get; set; } = new() { "inicio", "apps" };
    public List<string> Centro { get; set; } = new() { "reloj" };
    public List<string> Derecha { get; set; } = new() { "cpu", "ram", "bateria", "fecha" };
}

public class Config
{
    public Tema Tema { get; set; } = new();
    public Barra Barra { get; set; } = new();
    public Modulos Modulos { get; set; } = new();
    public OpcionesWallpaper Wallpaper { get; set; } = new();

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
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(Ruta), Opc);
        }
        catch { return null; }
    }
}
