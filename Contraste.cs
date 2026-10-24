using System;
using System.Windows.Media;

namespace NubeBar;

// Que un color se lea siempre sobre el fondo de la isla.
//
// El color de acento lo eliges tú, o lo saca el extractor del wallpaper. Nada
// impide que salga uno oscuro, y entonces el texto que lo usa (el nombre de la
// ventana activa en la vista general, la app de una notificación, los rótulos
// del centro de control) queda negro sobre negro y no se lee.
//
// En vez de prohibir colores oscuros, aquí se aclara u oscurece el que sea
// hasta que contrasta lo suficiente, conservando su tono. Se usa la fórmula de
// luminancia relativa de la WCAG, que es la que de verdad se corresponde con
// lo que el ojo distingue, y no la media de R, G y B.
static class Contraste
{
    // Mínimo razonable para texto mediano. 4.5 sería el nivel AA estricto, pero
    // aquí hablamos de un acento decorativo sobre un fondo muy oscuro: con 3.5
    // se lee de sobra y no se destiñe el color que elegiste.
    public const double Minimo = 3.5;

    static double Canal(byte v)
    {
        double c = v / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    public static double Luminancia(Color c) =>
        0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);

    public static double Entre(Color a, Color b)
    {
        double la = Luminancia(a), lb = Luminancia(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // Pública porque Roles la usa para derivar las superficies y el texto atenuado
    // a partir del fondo y del texto del tema.
    public static Color Mezclar(Color a, Color b, double k) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * k),
        (byte)Math.Round(a.G + (b.G - a.G) * k),
        (byte)Math.Round(a.B + (b.B - a.B) * k));

    // Saturación de 0 a 1: 0 es un gris puro (R=G=B) y 1 un color a tope.
    public static double Saturacion(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        return max == 0 ? 0 : (max - min) / max;
    }

    // Devuelve el color tal cual si ya se lee; si no, el mismo tono acercado al
    // blanco (sobre fondo oscuro) o al negro (sobre fondo claro) lo justo.
    public static Color Legible(Color color, Color fondo, double minimo = Minimo)
    {
        if (Entre(color, fondo) >= minimo) return color;

        var hacia = Luminancia(fondo) < 0.5 ? Colors.White : Colors.Black;
        for (int i = 1; i <= 20; i++)
        {
            var c = Mezclar(color, hacia, i / 20.0);
            if (Entre(c, fondo) >= minimo) return c;
        }
        return hacia;
    }
}
