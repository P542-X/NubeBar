using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NubeBar;

// Un tema guardado. Los cuatro primeros campos son los de siempre; los roles van
// detrás y son opcionales, así que un .json de tema exportado con una NubeBar
// anterior se sigue leyendo igual (las claves que faltan quedan en null y Roles las
// deriva).
public sealed record TemaGuardado(
    string Nombre, string Fondo, string Texto, string Acento, double Radio,
    string? Superficie = null, string? SuperficieAlta = null, string? Borde = null,
    string? TextoAtenuado = null, string? Secundario = null, string? Sistema = null,
    string? Positivo = null, string? Aviso = null, string? Negativo = null);

// Temas listos para usar y guardado/carga de los tuyos en %APPDATA%\NubeBar\temas
public static class Temas
{
    public static string Carpeta => Path.Combine(Config.Carpeta, "temas");

    // El tema de la especificación: coral, naranja y azul nube sobre azul profundo.
    // Es el que se aplica en una instalación NUEVA (ver Config.Cargar); a quien ya
    // tiene su config.json no se le toca nada.
    //
    // Dos correcciones sobre la paleta del documento, las dos de contraste:
    //
    //  1. El documento da "#172033" como texto principal y "#101A2B" como fondo
    //     profundo. Uno sobre otro dan 1,1:1, que es invisible. El #172033 es el
    //     texto de las SUPERFICIES CLARAS (#F4F7FA), no el de la isla. Como la isla
    //     es oscura, aquí el texto es el #F4F7FA.
    //  2. El coral #E87562 queda en 5,9:1 sobre el fondo profundo, así que sirve de
    //     tinta tal cual. Sobre una superficie clara se cae a 2,7:1, y por eso
    //     Roles.TintaSobre lo profundiza cuando hace de texto ahí. Como relleno,
    //     filo y rayita de la app activa se usa el coral de verdad.
    public static readonly TemaGuardado Nube = new(
        Nombre: "Nube",
        Fondo:  "#E6101A2B",     // "Fondo profundo" con la opacidad de la isla
        Texto:  "#F4F7FA",       // corrección 1: la "superficie clara" hace de tinta
        Acento: "#E87562",       // coral de acción
        Radio:  18,
        Superficie:     "#1B2740",   // el azul profundo, un paso levantado
        SuperficieAlta: "#24324F",
        Borde:          "#33415C",
        TextoAtenuado:  "#A9B6CA",
        Secundario:     "#F29C65",   // naranja secundario: detalles cálidos
        Sistema:        "#65B8D6");  // azul nube: red, volumen, batería, reloj

    public static readonly TemaGuardado[] Predefinidos =
    {
        Nube,
        new("Negro iPhone",      "#E6000000", "#FFFFFF", "#7AA2F7", 20),
        new("Tokyo Night",       "#E61A1B26", "#C0CAF5", "#7AA2F7", 18),
        new("Catppuccin Mocha",  "#E61E1E2E", "#CDD6F4", "#CBA6F7", 20),
        new("Nord",              "#E62E3440", "#ECEFF4", "#88C0D0", 16),
        new("Dracula",           "#E6282A36", "#F8F8F2", "#BD93F9", 18),
        new("Gruvbox",           "#E6282828", "#EBDBB2", "#FABD2F", 14)
    };

    // Volcar un tema guardado sobre el Tema del config. Los roles que el tema no
    // trae se ponen a null a propósito: null significa "derívalo", y si se dejara el
    // valor del tema anterior, cambiar de Nube a Gruvbox dejaría el naranja coral
    // puesto en Secundario para siempre.
    public static void Aplicar(TemaGuardado t, Tema destino)
    {
        destino.Fondo = t.Fondo;
        destino.Texto = t.Texto;
        destino.Acento = t.Acento;
        destino.Radio = t.Radio;
        destino.Superficie = t.Superficie;
        destino.SuperficieAlta = t.SuperficieAlta;
        destino.Borde = t.Borde;
        destino.TextoAtenuado = t.TextoAtenuado;
        destino.Secundario = t.Secundario;
        destino.Sistema = t.Sistema;
        destino.Positivo = t.Positivo;
        destino.Aviso = t.Aviso;
        destino.Negativo = t.Negativo;
    }

    public static void Exportar(TemaGuardado tema, string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        ConfigArbol.EscribirAtomico(ruta, JsonSerializer.Serialize(tema, new JsonSerializerOptions { WriteIndented = true }));
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
