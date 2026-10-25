using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NubeBar;

// El robot de la escena de entrada, dibujado con primitivas.
//
// Es original a propósito: nada de parecerse a una mascota conocida ni a la marca de
// nadie. La idea es "una nubecita con cuerpo de máquina": el cuerpo es literalmente la
// silueta de una nube —tres lóbulos y una base recta—, la cara es una visera oscura con
// dos ojos del color de acento del usuario, y en vez de patas tiene un propulsor,
// porque una nube no anda.
//
// Se dibuja a partir de UN número, el alto, y todo lo demás sale en proporción: así
// cabe igual en un portátil de 768 que en un monitor de 1440, y al 150 % de DPI no hay
// que tocar nada.
//
// El acento entra por parámetro para que el robot salga pintado de la nube DEL USUARIO.
//
// El pincel va a su IZQUIERDA, no a su derecha. Parece un detalle y no lo es: el robot
// cruza hacia la derecha y la pintura queda detrás, así que el pincel tiene que ir del
// lado de lo ya pintado. Con el pincel delante parecía que pintaba por donde todavía no
// había pasado.
static class Robot
{
    // Dónde cae la punta del pincel dentro del lienzo, en centésimas del alto. La
    // escena lo necesita para dejar la punta justo en el filo de la pintura.
    public const double PuntaX = 12, PuntaY = 116;

    public static Size Medida(double alto) => new(alto * 1.62, alto * 1.30);

    public static UIElement Dibujar(double alto, Color acento)
    {
        double u = alto / 100.0;
        var m = Medida(alto);
        var lienzo = new Canvas { Width = m.Width, Height = m.Height, IsHitTestVisible = false };

        var chapa = new LinearGradientBrush(
            Color.FromRgb(0xF2, 0xF6, 0xFC), Color.FromRgb(0xC2, 0xCF, 0xE2), 70);
        var sombra = new SolidColorBrush(Color.FromRgb(0x9A, 0xA8, 0xBE));
        var visera = new SolidColorBrush(Color.FromRgb(0x1B, 0x22, 0x30));
        var luz = new SolidColorBrush(acento);
        var luzSuave = new SolidColorBrush(Color.FromArgb(0x55, acento.R, acento.G, acento.B));

        void Poner(UIElement e, double x, double y) { Canvas.SetLeft(e, x * u); Canvas.SetTop(e, y * u); lienzo.Children.Add(e); }

        // El cuerpo va desplazado a la derecha: la franja de la izquierda es para el
        // brazo y el pincel.
        const double dx = 42;

        // ---------- El propulsor ----------
        Poner(new Ellipse
        {
            Width = 54 * u, Height = 14 * u, Fill = luzSuave,
            Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 * u }
        }, dx + 20, 96);

        Poner(new Path
        {
            Fill = new LinearGradientBrush(
                Color.FromArgb(0xCC, acento.R, acento.G, acento.B), Color.FromArgb(0x00, acento.R, acento.G, acento.B), 90),
            Data = Geometry.Parse(Escalar("M 36,0 L 60,0 L 70,26 L 26,26 Z", u))
        }, dx, 76);

        // ---------- La antena ----------
        // Antes que el cuerpo, para que el palo le salga por detrás de la nube.
        Poner(new Rectangle { Width = 2.4 * u, Height = 18 * u, Fill = sombra, RadiusX = 1.2 * u, RadiusY = 1.2 * u }, dx + 56, 16);

        // ---------- El cuerpo: la silueta de una nube ----------
        Poner(new Path
        {
            Fill = chapa,
            Stroke = sombra,
            StrokeThickness = 1.6 * u,
            Data = Geometry.Parse(Escalar(
                "M 18,78 " +
                "A 17,17 0 0 1 18,44 " +
                "A 23,23 0 0 1 60,30 " +
                "A 19,19 0 0 1 92,50 " +
                "A 15,15 0 0 1 92,78 " +
                "Z", u))
        }, dx, 0);

        // La bombilla de la antena va después del cuerpo: se ve entera
        Poner(new Ellipse
        {
            Width = 11 * u, Height = 11 * u, Fill = luz,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { Color = acento, BlurRadius = 14 * u, ShadowDepth = 0, Opacity = 0.9 }
        }, dx + 51, 8);

        // ---------- La visera y los ojos ----------
        Poner(new Border
        {
            Width = 50 * u, Height = 24 * u,
            CornerRadius = new CornerRadius(12 * u),
            Background = visera
        }, dx + 30, 46);

        Poner(new Ellipse { Width = 9 * u, Height = 9 * u, Fill = luz }, dx + 41, 53);
        Poner(new Ellipse { Width = 9 * u, Height = 9 * u, Fill = luz }, dx + 62, 53);
        Poner(new Ellipse { Width = 3 * u, Height = 3 * u, Fill = Brushes.White }, dx + 43, 55);
        Poner(new Ellipse { Width = 3 * u, Height = 3 * u, Fill = Brushes.White }, dx + 64, 55);

        // ---------- El brazo y el pincel, a la izquierda ----------
        // El brazo sale del cuerpo y baja en diagonal hasta el mango; sin el codo, el
        // pincel parecía pegado al aire.
        Poner(new Path
        {
            Stroke = chapa, StrokeThickness = 7 * u,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse(Escalar("M 40,0 L 18,6 L 6,22", u))
        }, 0, 62);

        // La mano: un anillo que agarra el mango
        Poner(new Ellipse
        {
            Width = 15 * u, Height = 15 * u,
            Fill = Brushes.Transparent, Stroke = sombra, StrokeThickness = 3.5 * u
        }, 0, 78);

        // Mango, virola y cerdas, en vertical y con la punta abajo
        Poner(new Rectangle
        {
            Width = 7 * u, Height = 30 * u, RadiusX = 3 * u, RadiusY = 3 * u,
            Fill = new SolidColorBrush(Color.FromRgb(0xB8, 0x7A, 0x3E))
        }, 4, 60);
        Poner(new Rectangle { Width = 11 * u, Height = 9 * u, Fill = sombra, RadiusX = 1.5 * u, RadiusY = 1.5 * u }, 2, 89);
        Poner(new Path
        {
            Fill = Brushes.Black,
            Data = Geometry.Parse(Escalar("M 0,0 L 15,0 L 12,22 Q 7.5,27 3,22 Z", u))
        }, 0, 96);

        return lienzo;
    }

    // Las rutas se escriben en centésimas del alto y se escalan aquí: así el dibujo se
    // lee en el código con números redondos y sale al tamaño que haga falta.
    static string Escalar(string ruta, double u)
    {
        var partes = ruta.Split(' ');
        for (int i = 0; i < partes.Length; i++)
        {
            if (!partes[i].Contains(',')) continue;
            var xy = partes[i].Split(',');
            if (xy.Length != 2) continue;
            if (double.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
                partes[i] = (x * u).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                            (y * u).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return string.Join(" ", partes);
    }
}
