using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NubeBar;

// Los accesorios de la Nubecita: el hachimaki de Samurái, la bufanda de Otoño, el gorro de
// Invierno, los auriculares de Videojuegos, el visor de Cyberpunk, los coloretes de Sakura y
// la lágrima de Sad.
//
// ---------- Por qué accesorios y no siete nubes ----------
//
// La tentación es dibujar una nube por tema. Entonces hay siete siluetas que mantener, seis de
// ellas se quedan sin actualizar la primera vez que la nube cambie, y el programa tiene siete
// identidades en vez de una con disfraces.
//
// `Nubecita.cs` ya dice esto mismo de su silueta: «Y de verdad una sola. La misma silueta hace
// de mascota pequeña, de ventana del tamaño de la pantalla…». Aquí se sigue: la nube es la que
// es, y encima se le pone algo.
//
// ---------- El sistema de coordenadas ----------
//
// Todo se dibuja en una caja de 100×100 y luego se escala al tamaño real con un `Viewbox`. Así
// un accesorio se diseña una vez y vale igual en la nube de 72 px del panel lateral que en la
// de 20 del botón Inicio, sin números repetidos por cada sitio donde salga.
//
// La nube, dentro de esa caja, ocupa aproximadamente de x=8 a x=92 y de y=30 a y=78.
public static class Accesorios
{
    // Qué lleva cada uno, para poder preguntarlo sin montar nada.
    public static bool Existe(string? cual) =>
        !string.IsNullOrWhiteSpace(cual) && Conocidos.Contains(cual.Trim().ToLowerInvariant());

    static readonly HashSet<string> Conocidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "hachimaki", "bufanda", "gorro", "auriculares", "visor", "coloretes", "triste-con-lagrima",
    };

    // ---------- El accesorio que le toca a un tema, o null ----------
    //
    // Null cuando el tema no lleva mascota, o cuando lleva una que esta versión no sabe
    // dibujar. Lo segundo pasa de verdad: un `.nubetema` de la comunidad puede pedir
    // `mascota:sombrero`, y lo correcto es no pintar nada y seguir, no inventarse un sombrero.
    public static UIElement? Para(Pintura p, double lado)
    {
        var cual = p.Tema.Argumento(Decoraciones.Mascota);
        if (!Existe(cual)) return null;

        var lienzo = new Canvas { Width = 100, Height = 100, IsHitTestVisible = false };
        Pintar(lienzo, cual!.Trim().ToLowerInvariant(), p);
        if (lienzo.Children.Count == 0) return null;

        return new Viewbox
        {
            Child = lienzo,
            Width = lado,
            Height = lado,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
        };
    }

    static void Pintar(Canvas c, string cual, Pintura p)
    {
        var acento = p.Color(ClavesBase.Acento);
        var sec = p.Color(ClavesBase.Sec1);
        var tinta = p.Color(ClavesBase.Tinta);
        var peligro = p.Color(ClavesBase.Peligro);

        switch (cual)
        {
            // La cinta en la frente y sus dos colas al viento. Va POR DELANTE de la nube y
            // cruzándola entera: una cinta que se queda en el borde se lee como un arañazo.
            case "hachimaki":
                Trazo(c, "M 6,40 L 94,36 L 94,47 L 6,51 Z", acento);
                Trazo(c, "M 90,40 L 112,30 L 108,44 L 118,46 L 90,52 Z", acento);
                Trazo(c, "M 42,40 L 58,39 L 58,48 L 42,49 Z", sec);   // el nudo, al medio
                break;

            // Por debajo, cruzando el cuerpo, con la punta colgando a un lado.
            case "bufanda":
                Trazo(c, "M 10,64 C 32,76 68,76 90,64 L 92,76 C 68,88 32,88 8,76 Z", acento);
                Trazo(c, "M 72,74 L 86,74 L 82,98 L 68,96 Z", sec);
                break;

            // Gorro de lana con su vuelta y su pompón.
            case "gorro":
                Trazo(c, "M 20,36 C 24,8 76,8 80,36 Z", peligro);
                Trazo(c, "M 14,34 L 86,34 L 86,46 L 14,46 Z", p.Color(ClavesBase.Tinta));
                Circulo(c, 50, 8, 9, peligro);
                break;

            // Diadema y dos cascos. La diadema por encima, los cascos a los lados y bien
            // pegados: separados parecen dos ruedas y no unos auriculares.
            case "auriculares":
                Trazo(c, "M 14,44 C 14,14 86,14 86,44 L 78,44 C 78,24 22,24 22,44 Z", tinta);
                Trazo(c, "M 6,42 L 24,42 L 24,66 L 6,66 Z", acento);
                Trazo(c, "M 76,42 L 94,42 L 94,66 L 76,66 Z", acento);
                break;

            // Una banda horizontal encendida a la altura de los ojos.
            case "visor":
                Trazo(c, "M 12,46 L 88,42 L 88,58 L 12,60 Z", acento);
                Trazo(c, "M 16,49 L 50,47 L 50,52 L 16,54 Z",
                      Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));   // el reflejo del cristal
                break;

            // Dos óvalos en los mofletes, por debajo de donde van los ojos.
            case "coloretes":
                Elipse(c, 28, 60, 11, 7, Color.FromArgb(0x7A, acento.R, acento.G, acento.B));
                Elipse(c, 72, 60, 11, 7, Color.FromArgb(0x7A, acento.R, acento.G, acento.B));
                break;

            // Una gota cayendo por el lado. Pequeña: una lágrima grande deja de dar pena y
            // pasa a dar risa.
            case "triste-con-lagrima":
                Trazo(c, "M 33,62 C 37,70 39,76 36,80 C 33,84 27,82 27,76 C 27,72 30,67 33,62 Z",
                      p.Color(ClavesBase.Sec2));
                break;
        }
    }

    // ---------- Ayudantes ----------
    //
    // `Canvas.SetLeft/Top` no hace falta: los trazados vienen ya en coordenadas de la caja de
    // 100×100, así que se colocan solos. Es lo que permite copiar un trazado de un editor
    // vectorial y pegarlo aquí sin traducir nada.
    static void Trazo(Canvas c, string datos, Color color)
    {
        var g = Geometry.Parse(datos);
        g.Freeze();
        c.Children.Add(new Path { Data = g, Fill = new SolidColorBrush(color), IsHitTestVisible = false });
    }

    static void Circulo(Canvas c, double cx, double cy, double r, Color color) =>
        Elipse(c, cx, cy, r, r, color);

    static void Elipse(Canvas c, double cx, double cy, double rx, double ry, Color color)
    {
        var e = new Ellipse
        {
            Width = rx * 2, Height = ry * 2,
            Fill = new SolidColorBrush(color),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(e, cx - rx);
        Canvas.SetTop(e, cy - ry);
        c.Children.Add(e);
    }
}
