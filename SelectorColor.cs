using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// Selector de color propio: cuadrado de saturación/valor, barra de tono, barra de
// opacidad opcional y campo hexadecimal. Nada de diálogos del sistema, para que
// tenga el mismo aire que el resto de la nube.
public sealed class SelectorColor : StackPanel
{
    readonly Border _cuadro = new() { Height = 120, CornerRadius = new CornerRadius(6), ClipToBounds = true };
    readonly Canvas _marcaCuadro = new();
    readonly System.Windows.Shapes.Ellipse _puntero = Puntero();
    readonly Border _tono = new() { Height = 14, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 8, 0, 0) };
    // La barra de opacidad lleva borde: con un color oscuro, el degradado de
    // transparente a negro sobre fondo negro no se vería y parecería que no está
    readonly Border _alfa = new()
    {
        Height = 14,
        CornerRadius = new CornerRadius(7),
        Margin = new Thickness(0, 8, 0, 0),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 255, 255, 255))
    };
    readonly TextBox _hex = new() { Margin = new Thickness(0, 8, 0, 0), Height = 26, BorderThickness = new Thickness(0) };
    readonly Border _muestra = new() { Width = 26, Height = 26, CornerRadius = new CornerRadius(6), Margin = new Thickness(8, 8, 0, 0) };

    double _h, _s = 1, _v = 1;
    byte _a = 0xE6;
    bool _silencio;

    public bool ConOpacidad { get; init; } = true;
    public event Action<Color>? Cambiado;

    public Color Valor
    {
        get => DesdeHsv(_h, _s, _v, _a);
        set
        {
            _a = value.A;
            AHsv(value, out _h, out _s, out _v);
            Refrescar();
        }
    }

    // Pequeño círculo que marca dónde estás dentro del cuadrado
    static System.Windows.Shapes.Ellipse Puntero() => new()
    {
        Width = 12,
        Height = 12,
        Stroke = Brushes.White,
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8, Color = Colors.Black }
    };

    readonly Border _capaColor = new();
    readonly Border _capaSombra = new()
    {
        Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90)
    };

    public SelectorColor()
    {
        // Las tres capas del cuadrado se montan UNA vez. Rehacerlas en cada refresco
        // explotaba: el puntero seguía colgando de la rejilla anterior.
        _marcaCuadro.Children.Add(_puntero);
        var capas = new Grid();
        capas.Children.Add(_capaColor);
        capas.Children.Add(_capaSombra);
        capas.Children.Add(_marcaCuadro);
        _cuadro.Child = capas;
        Children.Add(_cuadro);

        _tono.Background = PincelTono();
        Children.Add(_tono);

        if (ConOpacidad) { /* se añade en Montar, cuando ya se conoce la propiedad */ }

        var filaHex = new Grid();
        filaHex.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filaHex.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_hex, 0);
        Grid.SetColumn(_muestra, 1);
        filaHex.Children.Add(_hex);
        filaHex.Children.Add(_muestra);
        Children.Add(filaHex);

        _cuadro.MouseLeftButtonDown += (_, e) => { _cuadro.CaptureMouse(); TomarCuadro(e.GetPosition(_cuadro)); };
        _cuadro.MouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) TomarCuadro(e.GetPosition(_cuadro)); };
        _cuadro.MouseLeftButtonUp += (_, _) => _cuadro.ReleaseMouseCapture();

        _tono.MouseLeftButtonDown += (_, e) => { _tono.CaptureMouse(); TomarTono(e.GetPosition(_tono)); };
        _tono.MouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) TomarTono(e.GetPosition(_tono)); };
        _tono.MouseLeftButtonUp += (_, _) => _tono.ReleaseMouseCapture();

        _alfa.MouseLeftButtonDown += (_, e) => { _alfa.CaptureMouse(); TomarAlfa(e.GetPosition(_alfa)); };
        _alfa.MouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) TomarAlfa(e.GetPosition(_alfa)); };
        _alfa.MouseLeftButtonUp += (_, _) => _alfa.ReleaseMouseCapture();

        _hex.LostFocus += (_, _) => DesdeHex();
        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) DesdeHex(); };

        SizeChanged += (_, _) => ColocarPuntero();
    }

    // Se llama después de construir para respetar ConOpacidad (que es init)
    public SelectorColor Montar()
    {
        if (ConOpacidad) Children.Insert(2, _alfa);
        Refrescar();
        return this;
    }

    // ---------- Interacción ----------
    void TomarCuadro(Point p)
    {
        _s = Math.Clamp(p.X / Math.Max(1, _cuadro.ActualWidth), 0, 1);
        _v = 1 - Math.Clamp(p.Y / Math.Max(1, _cuadro.ActualHeight), 0, 1);
        Refrescar();
    }

    void TomarTono(Point p)
    {
        _h = Math.Clamp(p.X / Math.Max(1, _tono.ActualWidth), 0, 1) * 360;
        Refrescar();
    }

    void TomarAlfa(Point p)
    {
        _a = (byte)Math.Round(Math.Clamp(p.X / Math.Max(1, _alfa.ActualWidth), 0, 1) * 255);
        Refrescar();
    }

    void DesdeHex()
    {
        try
        {
            var t = _hex.Text.Trim();
            if (!t.StartsWith("#")) t = "#" + t;
            var c = (Color)ColorConverter.ConvertFromString(t);
            Valor = c;
            Avisar();
        }
        catch { Refrescar(); }
    }

    // ---------- Pintado ----------
    void Refrescar()
    {
        var puro = DesdeHsv(_h, 1, 1, 255);
        _capaColor.Background = new LinearGradientBrush(Colors.White, puro, 0);

        var c = Valor;
        _alfa.Background = new LinearGradientBrush(Color.FromArgb(0, c.R, c.G, c.B),
                                                   Color.FromArgb(255, c.R, c.G, c.B), 0);
        _muestra.Background = new SolidColorBrush(c);
        _muestra.BorderBrush = Brushes.White;
        _muestra.BorderThickness = new Thickness(1);

        _silencio = true;
        _hex.Text = ConOpacidad
            ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
            : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        _silencio = false;

        ColocarPuntero();
        Avisar();
    }

    void ColocarPuntero()
    {
        Canvas.SetLeft(_puntero, _s * Math.Max(0, _cuadro.ActualWidth) - 6);
        Canvas.SetTop(_puntero, (1 - _v) * Math.Max(0, _cuadro.ActualHeight) - 6);
    }

    void Avisar()
    {
        if (_silencio) return;
        Cambiado?.Invoke(Valor);
    }

    static LinearGradientBrush PincelTono()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++)
            b.GradientStops.Add(new GradientStop(DesdeHsv(i * 60, 1, 1, 255), i / 6.0));
        return b;
    }

    // ---------- HSV ----------
    public static Color DesdeHsv(double h, double s, double v, byte a)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return Color.FromArgb(a, (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static void AHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        v = max;
        s = max == 0 ? 0 : d / max;
        if (d == 0) { h = 0; return; }
        h = max == r ? 60 * (((g - b) / d) % 6)
          : max == g ? 60 * ((b - r) / d + 2)
                     : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
    }

    public static string AHex(Color c, bool conAlfa) =>
        conAlfa ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
