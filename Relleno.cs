using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace NubeBar;

// Un relleno: un color liso O un degradado lineal.
//
// ---------- Por qué un tipo propio y no un Color ----------
//
// Hasta ahora todo color del tema era un `Color`, y bastaba: un tema eran tres colores
// lisos. El rediseño (`docs/REDISENO-VISUAL.md`, decisión B) pide otra cosa: «cada relleno
// puede ser color liso o degradado lineal (paradas, ángulo, opacidad)». Y los nueve temas de
// serie ya lo usan — el filo de la isla de Cyberpunk es
// `linear-gradient(90deg,#FF2A6D,#FCEE0A,#00F0FF)`, que en un `Color` no cabe.
//
// La alternativa era guardar `Brush` en el catálogo. No vale por dos razones: un `Brush` no
// se puede MEDIR (hace falta un color para `Contraste.Entre`) y un `Brush` de WPF no es
// serializable a una línea de config que un humano pueda leer y editar.
//
// Así que `Relleno` es el valor —datos, comparable, serializable— y `Pincel()` es lo que se
// pinta. Un degradado además sabe dar su `Color` representativo, que es el que se usa para
// medir contraste: no es exacto (un degradado no tiene un contraste, tiene un rango) pero es
// la media honrada, y evita que un texto quede ilegible sobre la mitad oscura.
//
// ---------- Y por qué el formato de texto es el de CSS ----------
//
// Porque es el que ya está escrito. Los valores de los nueve temas vienen de la maqueta
// aprobada (`docs/maqueta-rediseno/temas.json`) en `rgba(...)` y `linear-gradient(...)`.
// Inventar aquí una sintaxis propia obligaría a traducir a mano 9 temas × 19 colores, que es
// exactamente la clase de trabajo donde se cuela una errata que nadie vuelve a ver.
public readonly record struct Parada(Color Color, double Sitio)
{
    // El sitio va de 0 a 1. Se guarda normalizado y no en por ciento para no tener que
    // dividir en cada pintada.
    public Parada Con(double sitio) => new(Color, sitio);
}

public sealed record Relleno
{
    public Color Color { get; }                      // liso: el color. Degradado: su media
    public IReadOnlyList<Parada> Paradas { get; }    // vacío si es liso
    public double Angulo { get; }                    // grados CSS: 0 arriba, 90 derecha, 180 abajo

    Relleno(Color color, IReadOnlyList<Parada> paradas, double angulo)
    {
        Color = color; Paradas = paradas; Angulo = angulo;
    }

    public bool EsDegradado => Paradas.Count > 1;
    public bool EsInvisible => !EsDegradado && Color.A == 0;

    static readonly Parada[] Ninguna = Array.Empty<Parada>();

    public static Relleno Liso(Color c) => new(c, Ninguna, 180);

    public static readonly Relleno Nada = Liso(Colors.Transparent);

    public static Relleno Degradado(double angulo, IReadOnlyList<Parada> paradas)
    {
        if (paradas.Count == 0) return Nada;
        if (paradas.Count == 1) return Liso(paradas[0].Color);
        return new Relleno(Media(paradas), paradas, angulo);
    }

    // ---------- El color que representa a un degradado ----------
    //
    // La media aritmética de sus paradas, canal a canal y alfa incluida. No es "el color del
    // degradado" —no existe tal cosa— sino el único número con el que se puede contestar a
    // «¿se lee este texto encima?» sin pintar nada.
    //
    // Se pondera por el TRAMO que ocupa cada parada, no por cuántas hay: en
    // `linear-gradient(180deg,#FFF 0%,#000 10%,#000 100%)` el blanco manda sobre el 5 % del
    // alto, y una media sin ponderar diría que la mitad es blanca.
    static Color Media(IReadOnlyList<Parada> paradas)
    {
        double r = 0, g = 0, b = 0, a = 0, total = 0;
        for (int i = 0; i < paradas.Count; i++)
        {
            // El peso de una parada es la mitad de lo que la separa de sus vecinas: su
            // "zona de influencia". Las de los extremos solo tienen un lado.
            double antes = i == 0 ? paradas[i].Sitio : (paradas[i].Sitio - paradas[i - 1].Sitio) / 2;
            double luego = i == paradas.Count - 1 ? 1 - paradas[i].Sitio
                                                  : (paradas[i + 1].Sitio - paradas[i].Sitio) / 2;
            double peso = Math.Max(antes + luego, 0.0001);
            var c = paradas[i].Color;
            r += c.R * peso; g += c.G * peso; b += c.B * peso; a += c.A * peso;
            total += peso;
        }
        if (total <= 0) return Colors.Transparent;
        return Color.FromArgb(Ocho(a / total), Ocho(r / total), Ocho(g / total), Ocho(b / total));
    }

    static byte Ocho(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    // ---------- Opacidad ----------
    //
    // Multiplica el alfa de todo. Hace falta para la «opacidad por color» del Estudio
    // (apartado 2) y para los velos derivados del catálogo, que son el mismo color del tema
    // con menos alfa.
    public Relleno ConOpacidad(double k)
    {
        k = Math.Clamp(k, 0, 1);
        if (k >= 1) return this;
        if (!EsDegradado) return Liso(ConAlfa(Color, k));
        return Degradado(Angulo, Paradas.Select(p => p with { Color = ConAlfa(p.Color, k) }).ToArray());
    }

    static Color ConAlfa(Color c, double k) => Color.FromArgb(Ocho(c.A * k), c.R, c.G, c.B);

    // ================= Lo que se pinta =================

    // El pincel, congelado: un `Brush` de WPF que no cambia se puede compartir entre hilos y
    // entre elementos, y congelarlo le ahorra a WPF mantenerle notificaciones de cambio. Los
    // pinceles del catálogo se reparten por toda la interfaz, así que esto se nota.
    public Brush Pincel()
    {
        if (!EsDegradado)
        {
            var liso = new SolidColorBrush(Color);
            liso.Freeze();
            return liso;
        }

        var p = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            StartPoint = Extremo(Angulo, false),
            EndPoint = Extremo(Angulo, true),
        };
        foreach (var parada in Paradas)
            p.GradientStops.Add(new GradientStop(parada.Color, parada.Sitio));
        p.Freeze();
        return p;
    }

    // ---------- De un ángulo CSS a los dos puntos de WPF ----------
    //
    // CSS cuenta los grados en el sentido del reloj desde «hacia arriba»: 0 arriba, 90 a la
    // derecha, 180 abajo, 270 a la izquierda. WPF no pide un ángulo, pide de dónde a dónde va
    // el degradado dentro de la caja (0,0 arriba a la izquierda; 1,1 abajo a la derecha).
    //
    // El vector que apunta «hacia donde va» es (sen θ, −cos θ), con la y creciendo hacia
    // abajo: en 180° sale (0, +1), o sea hacia abajo, que es lo que `180deg` significa.
    // Luego los dos extremos son el centro ± la mitad del vector.
    //
    // No se replica el escalado de las esquinas que hace CSS en los ángulos oblicuos (CSS
    // estira el degradado hasta que la primera y la última parada caen en las esquinas). En
    // los ángulos rectos —que son los que usan los nueve temas— el resultado es idéntico; en
    // uno oblicuo la franja queda algo más corta, y eso en un filo de 1 px no se ve.
    internal static Point Extremo(double anguloGrados, bool final)
    {
        double r = anguloGrados * Math.PI / 180;
        double dx = Math.Sin(r), dy = -Math.Cos(r);
        double signo = final ? 0.5 : -0.5;
        return new Point(0.5 + dx * signo, 0.5 + dy * signo);
    }

    // ================= Leer y escribir =================

    // Lo que se guarda en el config y en los ficheros de tema. Para un liso, el
    // `#AARRGGBB` de siempre, que es lo que ya hay escrito en los config de los usuarios.
    public string Escribir()
    {
        if (!EsDegradado) return Hex(Color);

        var sb = new StringBuilder("linear-gradient(");
        sb.Append(Angulo.ToString("0.##", CultureInfo.InvariantCulture)).Append("deg");
        foreach (var p in Paradas)
        {
            sb.Append(',').Append(Hex(p.Color));
            sb.Append(' ').Append((p.Sitio * 100).ToString("0.##", CultureInfo.InvariantCulture)).Append('%');
        }
        return sb.Append(')').ToString();
    }

    public static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public override string ToString() => Escribir();

    // ---------- Leer ----------
    //
    // Devuelve null si no se entiende. NUNCA lanza: un color mal escrito en un config o en un
    // `.nubetema` importado no puede tumbar el arranque, tiene que caer en lo derivado y
    // quedar apuntado. Es la misma regla que ya cumplía `Roles.Leer`.
    public static Relleno? Leer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var s = texto.Trim();

        // ---------- El "+ lo que sea" de los fondos de escritorio ----------
        //
        // `temas.json` describe algunos fondos como «linear-gradient(...) + rejilla en
        // perspectiva verde»: el degradado y, detrás, una decoración que se construye en la
        // fase 7. Aquí se lee el degradado y se ignora la prosa; quien quiera la decoración la
        // tiene en `TemaBase.FondoExtra`.
        int mas = s.IndexOf(") +", StringComparison.Ordinal);
        if (mas > 0) s = s[..(mas + 1)];

        if (s.StartsWith("linear-gradient(", StringComparison.OrdinalIgnoreCase))
            return LeerDegradado(s);

        var c = LeerColor(s);
        return c is null ? null : Liso(c.Value);
    }

    // Un color suelto: `#RGB`, `#RRGGBB`, `#AARRGGBB`, `rgb(...)`, `rgba(...)`,
    // `transparent` o un nombre de los de WPF (`Tomato`, `Gold`…).
    public static Color? LeerColor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var s = texto.Trim();

        if (s.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return Colors.Transparent;

        if (s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            int ab = s.IndexOf('('), ce = s.LastIndexOf(')');
            if (ab < 0 || ce <= ab) return null;
            var partes = s[(ab + 1)..ce].Split(',', StringSplitOptions.TrimEntries);
            if (partes.Length is < 3 or > 4) return null;
            if (!Num(partes[0], out var r) || !Num(partes[1], out var g) || !Num(partes[2], out var b))
                return null;
            double a = 1;
            if (partes.Length == 4 && !Num(partes[3], out a)) return null;
            // `rgba()` da el alfa de 0 a 1 y los canales de 0 a 255. Los `.25` sin cero
            // delante que trae la maqueta los acepta `double.TryParse` con InvariantCulture.
            return Color.FromArgb(Ocho(a * 255), Ocho(r), Ocho(g), Ocho(b));
        }

        // WPF entiende `#RGB`, `#RRGGBB`, `#AARRGGBB` y los nombres. Lo que no entienda,
        // excepción; de ahí el try: aquí un color ilegible es null, no un cierre.
        try { return (Color)ColorConverter.ConvertFromString(s); }
        catch { return null; }
    }

    static Relleno? LeerDegradado(string s)
    {
        int ab = s.IndexOf('('), ce = s.LastIndexOf(')');
        if (ab < 0 || ce <= ab) return null;

        var trozos = PartirPorComas(s[(ab + 1)..ce]);
        if (trozos.Count == 0) return null;

        double angulo = 180;                 // el de CSS cuando no se dice: hacia abajo
        int primera = 0;
        if (LeerAngulo(trozos[0]) is { } a) { angulo = a; primera = 1; }

        var paradas = new List<Parada>();
        for (int i = primera; i < trozos.Count; i++)
        {
            var t = trozos[i].Trim();
            if (t.Length == 0) continue;

            // Una parada es «color» o «color 55%». El sitio va al final, así que se corta por
            // el último hueco que no esté dentro de un paréntesis de `rgba(...)`.
            double? sitio = null;
            int hueco = t.LastIndexOf(' ');
            if (hueco > 0 && t.IndexOf(')', hueco) < 0)
            {
                var cola = t[(hueco + 1)..];
                if (cola.EndsWith('%') && Num(cola[..^1], out var pct)) { sitio = pct / 100.0; t = t[..hueco].TrimEnd(); }
            }

            if (LeerColor(t) is not { } c) return null;
            paradas.Add(new Parada(c, sitio ?? -1));
        }

        if (paradas.Count == 0) return null;
        return Degradado(angulo, Repartir(paradas));
    }

    // ---------- Las paradas sin por ciento ----------
    //
    // En CSS, una parada sin sitio se reparte a partes iguales entre las que sí lo tienen.
    // Aquí se hace la versión simple y suficiente: las que no lo traen se reparten
    // uniformemente entre la anterior fijada y la siguiente fijada. Con los nueve temas, que
    // o fijan todas o ninguna, es exactamente lo de CSS.
    static List<Parada> Repartir(List<Parada> paradas)
    {
        if (paradas.Count == 1) return new List<Parada> { paradas[0].Con(0) };

        // Primera y última siempre fijadas: 0 y 1, como CSS.
        if (paradas[0].Sitio < 0) paradas[0] = paradas[0].Con(0);
        if (paradas[^1].Sitio < 0) paradas[^1] = paradas[^1].Con(1);

        int i = 0;
        while (i < paradas.Count)
        {
            if (paradas[i].Sitio >= 0) { i++; continue; }
            int j = i;
            while (j < paradas.Count && paradas[j].Sitio < 0) j++;
            double desde = paradas[i - 1].Sitio, hasta = paradas[j].Sitio;
            int cuantas = j - i;
            for (int k = 0; k < cuantas; k++)
                paradas[i + k] = paradas[i + k].Con(desde + (hasta - desde) * (k + 1) / (cuantas + 1.0));
            i = j;
        }

        // Monótonas: una parada nunca puede ir por detrás de la anterior, o WPF pinta
        // franjas invertidas. CSS hace lo mismo (las sube al máximo visto).
        for (int k = 1; k < paradas.Count; k++)
            if (paradas[k].Sitio < paradas[k - 1].Sitio)
                paradas[k] = paradas[k].Con(paradas[k - 1].Sitio);

        return paradas;
    }

    static double? LeerAngulo(string trozo)
    {
        var t = trozo.Trim();
        if (t.EndsWith("deg", StringComparison.OrdinalIgnoreCase) && Num(t[..^3], out var g)) return g;

        // `to bottom`, `to right`… Los nueve temas no los usan, pero los escribe cualquiera
        // que copie un degradado de la web, y traducirlos cuesta cuatro casos.
        return t.ToLowerInvariant().Replace("  ", " ") switch
        {
            "to top" => 0.0,
            "to right" => 90.0,
            "to bottom" => 180.0,
            "to left" => 270.0,
            "to top right" => 45.0,
            "to bottom right" => 135.0,
            "to bottom left" => 225.0,
            "to top left" => 315.0,
            _ => null,
        };
    }

    // Partir por comas de PRIMER nivel: las de dentro de `rgba(12,14,20,.94)` no cuentan.
    static List<string> PartirPorComas(string s)
    {
        var fuera = new List<string>();
        int hondo = 0, desde = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') hondo++;
            else if (s[i] == ')') hondo = Math.Max(0, hondo - 1);
            else if (s[i] == ',' && hondo == 0) { fuera.Add(s[desde..i]); desde = i + 1; }
        }
        fuera.Add(s[desde..]);
        return fuera;
    }

    // Siempre con InvariantCulture: los ficheros de tema no cambian de coma decimal según
    // dónde viva el usuario. `.25` sin cero delante entra bien con AllowDecimalPoint.
    static bool Num(string s, out double v) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
