using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace NubeBar;

// Mini gráfica de los últimos N valores. Se dibuja a mano en OnRender porque con
// formas de WPF serían 60 elementos por gráfica y dos gráficas en el menú.
public sealed class Grafica : FrameworkElement
{
    readonly Queue<double> _valores = new();
    readonly int _cuantos;
    Brush _trazo = Brushes.CornflowerBlue;
    Brush _relleno = Brushes.Transparent;

    public string Titulo { get; set; } = "";
    public Func<string> Etiqueta { get; set; } = () => "";

    public Grafica(int cuantos = 60)
    {
        _cuantos = cuantos;
        Height = 46;
    }

    public void Colorear(Color acento)
    {
        _trazo = new SolidColorBrush(acento);
        var degradado = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        degradado.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, acento.R, acento.G, acento.B), 0));
        degradado.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, acento.R, acento.G, acento.B), 1));
        degradado.Freeze();
        _relleno = degradado;
        InvalidateVisual();
    }

    public void Anotar(double valor)
    {
        _valores.Enqueue(Math.Clamp(valor, 0, 100));
        while (_valores.Count > _cuantos) _valores.Dequeue();
        InvalidateVisual();
    }

    public void Rellenar(IEnumerable<double> historial)
    {
        _valores.Clear();
        foreach (var v in historial.TakeLast(_cuantos)) _valores.Enqueue(Math.Clamp(v, 0, 100));
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 1 || h <= 1 || _valores.Count < 2) return;

        var datos = _valores.ToArray();
        // Siempre se reparte sobre los N huecos, aunque haya menos datos: así la
        // línea crece desde la derecha en vez de estirarse al llenarse
        double paso = w / Math.Max(1, _cuantos - 1);
        double x0 = w - (datos.Length - 1) * paso;

        var figura = new PathFigure { StartPoint = new Point(x0, h - datos[0] / 100 * h) };
        for (int i = 1; i < datos.Length; i++)
            figura.Segments.Add(new LineSegment(new Point(x0 + i * paso, h - datos[i] / 100 * h), true));

        var linea = new PathGeometry(new[] { figura });

        // El relleno es la misma línea cerrada contra la base
        var bajo = figura.Clone();
        bajo.Segments.Add(new LineSegment(new Point(x0 + (datos.Length - 1) * paso, h), true));
        bajo.Segments.Add(new LineSegment(new Point(x0, h), true));
        bajo.IsClosed = true;
        dc.DrawGeometry(_relleno, null, new PathGeometry(new[] { bajo }));

        dc.DrawGeometry(null, new Pen(_trazo, 1.6) { LineJoin = PenLineJoin.Round }, linea);
    }
}

// Guarda los últimos 60 segundos de CPU y RAM para que las gráficas tengan
// historia aunque el menú se acabe de abrir
public static class Historial
{
    const int Cuantos = 60;
    static readonly Queue<double> _cpu = new();
    static readonly Queue<double> _ram = new();

    public static IEnumerable<double> Cpu => _cpu;
    public static IEnumerable<double> Ram => _ram;

    public static void Anotar(double cpu, double ram)
    {
        _cpu.Enqueue(cpu); while (_cpu.Count > Cuantos) _cpu.Dequeue();
        _ram.Enqueue(ram); while (_ram.Count > Cuantos) _ram.Dequeue();
    }
}
