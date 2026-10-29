using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NubeBar;

// `Media` es el color medio de la imagen TAL CUAL, sin oscurecer. `Fondo` no sirve para
// eso: lleva un ×0,25 para que la isla quede oscura por encima de cualquier foto, así que
// preguntarle "¿es claro el escritorio?" contesta que no siempre.
//
// Lo necesita D6: la previsualización del ajuste se dibuja encima del escritorio, y para
// elegir un filo que se vea hay que saber si lo de debajo es claro u oscuro. Es opcional
// —`MediaReal` cae en `Fondo` si no está— para que nadie que construya una paleta a mano
// tenga que inventarse un cuarto color.
public record Paleta(Color Fondo, Color Texto, Color Acento, Color? Media = null)
{
    public Color MediaReal => Media ?? Fondo;

    // Es esto un color? Hacía falta en un sitio concreto: validar un perfil o un fondo
    // que viene de un fichero importado, donde "Acento": "azulito" tiene que poder
    // rechazarse ANTES de escribir nada. El try/catch estaba repetido suelto en cinco
    // archivos y ninguno se podía comprobar.
    public static bool EsColor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        try { return ColorConverter.ConvertFromString(texto) is Color; }
        catch { return false; }
    }

    // Y leerlo con un valor de respaldo, que es lo que hacían esas cinco copias.
    public static Color Leer(string? texto, Color siNo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return siNo;
        try { return (Color)ColorConverter.ConvertFromString(texto); }
        catch { return siNo; }
    }
}

// Saca los colores de una imagen, al estilo de pywal en Linux
static class Extractor
{
    public static Paleta? DesdeImagen(string ruta)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(ruta);
            bmp.DecodePixelWidth = 64;                    // reducida: rápido y suficiente
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();

            var img = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            int stride = img.PixelWidth * 4;
            var px = new byte[img.PixelHeight * stride];
            img.CopyPixels(px, stride, 0);

            // Color medio (para el fondo) + el tono vivo dominante (para el acento)
            double mr = 0, mg = 0, mb = 0; int n = 0;
            var peso = new double[12]; var sr = new double[12]; var sg = new double[12]; var sb = new double[12];

            for (int i = 0; i + 3 < px.Length; i += 4)
            {
                double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
                mr += r; mg += g; mb += b; n++;

                double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                double sat = max == 0 ? 0 : (max - min) / max;
                if (sat < 0.25 || max < 0.25) continue;      // ignorar grises y negros

                int k = (int)(Tono(r, g, b, max, min) / 30) % 12;
                double w = sat * max;
                peso[k] += w; sr[k] += r * w; sg[k] += g * w; sb[k] += b * w;
            }
            if (n == 0) return null;

            int mejor = 0;
            for (int k = 1; k < 12; k++) if (peso[k] > peso[mejor]) mejor = k;

            var acento = peso[mejor] > 0
                ? Rgb(sr[mejor] / peso[mejor], sg[mejor] / peso[mejor], sb[mejor] / peso[mejor])
                : Colors.CornflowerBlue;
            acento = Aclarar(acento, 0.8);                   // que se vea bien sobre fondo oscuro

            var fondo = Rgb(mr / n * 0.25, mg / n * 0.25, mb / n * 0.25);
            var texto = Mezclar(Colors.White, acento, 0.85); // blanco con un toque del acento

            // El medio sin oscurecer: es lo que de verdad hay detrás de una ventana, y lo
            // que la previsualización del ajuste necesita para elegir un filo que se vea.
            var media = Rgb(mr / n, mg / n, mb / n);
            return new Paleta(fondo, texto, acento, media);
        }
        catch { return null; }
    }

    static double Tono(double r, double g, double b, double max, double min)
    {
        double d = max - min;
        if (d == 0) return 0;
        double h = max == r ? ((g - b) / d) % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        h *= 60;
        return h < 0 ? h + 360 : h;
    }

    static Color Rgb(double r, double g, double b) =>
        Color.FromRgb((byte)Math.Clamp(r * 255, 0, 255), (byte)Math.Clamp(g * 255, 0, 255), (byte)Math.Clamp(b * 255, 0, 255));

    static Color Aclarar(Color c, double minimo)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0;
        if (max >= minimo || max == 0) return c;
        double f = minimo / max;
        return Rgb(c.R / 255.0 * f, c.G / 255.0 * f, c.B / 255.0 * f);
    }

    static Color Mezclar(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R * t + b.R * (1 - t)), (byte)(a.G * t + b.G * (1 - t)), (byte)(a.B * t + b.B * (1 - t)));
}
