using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NubeBar;

// La nube de la escena de entrada: el personaje entero.
//
// Antes esto era un robot con cabeza de nube, torso, dos brazos articulados, manos,
// propulsores y una estela. Funcionaba, estaba medido y aun así se tiró: un humanoide
// pide una animación de humanoide —gestos, posturas, anatomía— y para contar "el
// escritorio se enciende" eso es ruido. Lo que queda es **una sola figura**: la nube.
//
// Y de verdad una sola. La misma silueta hace de mascota pequeña, de ventana del tamaño
// de la pantalla y de icono en la barra; lo único que cambia entre esos tres papeles es
// una escala, una posición y un giro. No hay una nube pequeña y otra grande, no hay un
// dibujo para revelar y otro para el final: hay un `Geometry` y tres transformaciones.
//
// Por eso vive aquí y no dentro de la escena: la escena la mueve, pero quién es la nube
// —su forma y su cara— se decide en un sitio.
//
// Es geometría vectorial pura: cuatro arcos y una base recta. Ni un mapa de bits, así
// que se ve igual de limpia a 30 píxeles de ancho que a 4000, y al 100, 125, 150 o
// 200 % de DPI no hay nada que tocar.
static class Nubecita
{
    // La silueta. Las coordenadas dan igual —se normaliza nada más leerla— pero se
    // escriben en una caja de 100 porque así se lee el dibujo.
    const string Ruta = "M 18,78 A 17,17 0 0 1 18,44 A 23,23 0 0 1 60,30 " +
                        "A 19,19 0 0 1 92,50 A 15,15 0 0 1 92,78 Z";

    // La nube normalizada: **ancho 1 y centrada en (0,0)**. Que el ancho sea exactamente
    // 1 no es cosmético: así "la escala" ES el ancho de la nube en píxeles, y todas las
    // cuentas de la escena se leen en píxeles sin conversiones.
    static TransformGroup Normalizar(Rect b) => new()
    {
        Children =
        {
            new TranslateTransform(-(b.X + b.Width / 2), -(b.Y + b.Height / 2)),
            new ScaleTransform(1 / b.Width, 1 / b.Width),
        }
    };

    // El molde: una copia congelada, sin animar, para preguntarle cosas. La escena la
    // usa para averiguar cuánto hay que agrandar la nube para que tape una pantalla.
    internal static Geometry Molde()
    {
        // `Geometry.Parse` devuelve una figura CONGELADA —y además la misma para la misma
        // cadena—, así que hay que clonarla antes de ponerle transformaciones propias.
        var g = Geometry.Parse(Ruta).Clone();
        g.Transform = Normalizar(g.Bounds);
        g.Freeze();
        return g;
    }

    internal static double Proporcion()
    {
        var b = Geometry.Parse(Ruta).Bounds;
        return b.Height / b.Width;
    }

    // El contorno de la nube normalizada, ya aplanado a polígono.
    //
    // Existe por velocidad: para saber cuánto hay que agrandar la nube hasta que tape
    // una pantalla hay que preguntar muchas veces "¿está este punto dentro?", y
    // `Geometry.FillContains` aplana la figura **en cada llamada**. Con el polígono
    // calculado una vez, la misma pregunta es un bucle de treinta sumas.
    internal static Point[] Perfil()
    {
        var plano = Molde().GetFlattenedPathGeometry(0.0006, ToleranceType.Absolute);
        var puntos = new List<Point>(128);
        foreach (var f in plano.Figures)
        {
            puntos.Add(f.StartPoint);
            foreach (var seg in f.Segments)
                switch (seg)
                {
                    case PolyLineSegment poli: puntos.AddRange(poli.Points); break;
                    case LineSegment linea: puntos.Add(linea.Point); break;
                }
        }
        return puntos.ToArray();
    }

    // ¿Cae el punto dentro del contorno? Lanzamiento de rayo de toda la vida.
    internal static bool Dentro(Point[] perfil, double x, double y)
    {
        bool dentro = false;
        for (int i = 0, j = perfil.Length - 1; i < perfil.Length; j = i++)
        {
            if ((perfil[i].Y > y) == (perfil[j].Y > y)) continue;
            double corte = (perfil[j].X - perfil[i].X) * (y - perfil[i].Y)
                           / (perfil[j].Y - perfil[i].Y) + perfil[i].X;
            if (x < corte) dentro = !dentro;
        }
        return dentro;
    }

    // La nube que se mueve. Las tres transformaciones van DENTRO de la geometría, no en
    // los `Path` que la dibujan: así todos los que la comparten —la ventana negra, el
    // relleno, el halo y el filo— siguen la misma figura sin que haya que acordarse de
    // moverlos a la vez. Es lo que hace imposible que se desincronicen.
    internal sealed record Forma(Geometry Trazo, ScaleTransform Tamano, RotateTransform Ladeo, TranslateTransform Centro);

    internal static Forma Nueva()
    {
        var g = Geometry.Parse(Ruta).Clone();
        var tamano = new ScaleTransform(1, 1);
        var ladeo = new RotateTransform(0);
        var centro = new TranslateTransform();
        var grupo = Normalizar(g.Bounds);
        grupo.Children.Add(tamano);
        grupo.Children.Add(ladeo);
        grupo.Children.Add(centro);
        g.Transform = grupo;
        return new Forma(g, tamano, ladeo, centro);
    }

    // ---------- La cara ----------
    //
    // Dos ojos y nada más. Sin boca y sin cejas: a 30 píxeles de ancho cualquier otra
    // cosa se convierte en suciedad, y lo que tiene que sobrevivir al tamaño de icono es
    // "esto es una nube y está despierta".
    //
    // Va en las mismas unidades normalizadas que la silueta, así que se coloca con la
    // misma escala y la misma posición que ella.
    internal sealed record Rostro(UIElement Visual, ScaleTransform Parpadeo, ScaleTransform Tamano, TranslateTransform Centro);

    internal static Rostro Ojos(Color acento)
    {
        const double sep = 0.108, linea = -0.012, r = 0.050;

        var lienzo = new Canvas { IsHitTestVisible = false };
        var tinta = new SolidColorBrush(acento); tinta.Freeze();
        var blanco = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)); blanco.Freeze();

        void Ovalo(double x, double y, double radio, Brush relleno)
        {
            var e = new Ellipse { Width = radio * 2, Height = radio * 2, Fill = relleno };
            Canvas.SetLeft(e, x - radio); Canvas.SetTop(e, y - radio);
            lienzo.Children.Add(e);
        }

        foreach (double lado in new[] { -1.0, 1.0 })
        {
            Ovalo(lado * sep, linea, r, tinta);
            Ovalo(lado * sep - r * 0.30, linea - r * 0.32, r * 0.30, blanco);
        }

        // El parpadeo aplasta los ojos contra SU línea, no contra el centro de la nube:
        // si no, al cerrarse se deslizarían hacia abajo.
        var parpadeo = new ScaleTransform(1, 1, 0, linea);
        var tamano = new ScaleTransform(1, 1);
        var centro = new TranslateTransform();
        lienzo.RenderTransform = new TransformGroup { Children = { parpadeo, tamano, centro } };

        return new Rostro(lienzo, parpadeo, tamano, centro);
    }
}
