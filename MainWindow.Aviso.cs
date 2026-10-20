using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// El aviso de volumen y de brillo: la isla se estira un momento, enseña el icono
// y una barra que se anima hasta el valor nuevo, y vuelve a su forma a los 1,5 s.
public partial class MainWindow : Window
{
    const string PanelAviso = "aviso";
    const double AltoAviso = 44;

    static int _volumenVisto = -1;
    static bool _mudoVisto;
    static DispatcherTimer? _cierraAviso;

    Border? _barraAviso;
    TextBlock? _glifoAviso, _cifraAviso;

    // Se mira a la vez que la pantalla completa, con el mismo temporizador de 150 ms:
    // así el aviso sale a la vez que el de Windows y no hace falta otro reloj.
    void VigilarVolumen()
    {
        if (!_esJefa) return;
        try
        {
            int v = Audio.Volumen();
            if (v < 0) return;
            bool mudo = Audio.Silenciado;

            if (_volumenVisto < 0) { _volumenVisto = v; _mudoVisto = mudo; return; }   // primera lectura
            if (v == _volumenVisto && mudo == _mudoVisto) return;

            _volumenVisto = v;
            _mudoVisto = mudo;
            IslaDelRaton().MostrarAviso(mudo || v == 0 ? "" : v < 33 ? "" : v < 66 ? "" : "",
                                        mudo ? 0 : v, mudo ? "Silencio" : "Volumen");
        }
        catch { }
    }

    // El brillo no se puede vigilar igual: preguntárselo al monitor por DDC/CI es un
    // viaje por el cable de 50 ms largos, y hacerlo diez veces por segundo sería un
    // disparate. Por eso el aviso de brillo lo dispara quien lo cambia.
    public void AvisoDeBrillo(int porCiento) =>
        MostrarAviso("", porCiento, "Brillo");

    public void MostrarAviso(string glifo, int porCiento, string etiqueta)
    {
        try
        {
            // Si ya hay un aviso puesto, se reaprovecha: así subir el volumen cinco
            // veces seguidas no abre y cierra el panel cinco veces
            if (_panelAbierto == PanelAviso && _barraAviso != null && _glifoAviso != null)
            {
                PintarAviso(glifo, porCiento, etiqueta);
                RearmarCierre();
                return;
            }

            // Un aviso no debe tragarse el menú Inicio ni la vista general si están abiertos
            if (_panelAbierto != null && _panelAbierto != PanelAviso) return;

            AbrirPanel(PanelAviso, CrearAviso(glifo, porCiento, etiqueta), 300, AltoAviso);
            DestellarBorde();
            RearmarCierre();
        }
        catch (Exception ex) { Log.Error("aviso de volumen", ex); }
    }

    FrameworkElement CrearAviso(string glifo, int porCiento, string etiqueta)
    {
        var rejilla = new Grid { Margin = new Thickness(14, 0, 14, 10), Height = AltoAviso - 12 };
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

        _glifoAviso = Glifo(glifo);
        _glifoAviso.FontSize = 16;
        Grid.SetColumn(_glifoAviso, 0);
        rejilla.Children.Add(_glifoAviso);

        // El canal de la barra, y dentro la parte llena, que es la que se anima
        var canal = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(0x33, _cTexto.R, _cTexto.G, _cTexto.B)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 0),
            ClipToBounds = true
        };
        _barraAviso = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = _acento,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0
        };
        canal.Child = _barraAviso;
        Grid.SetColumn(canal, 1);
        rejilla.Children.Add(canal);

        _cifraAviso = new TextBlock
        {
            Foreground = _texto, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12
        };
        Grid.SetColumn(_cifraAviso, 2);
        rejilla.Children.Add(_cifraAviso);

        // El ancho de la barra depende del ancho ya medido del canal, que todavía no
        // existe: se pinta cuando el layout lo haya colocado
        canal.SizeChanged += (_, _) => PintarAviso(glifo, porCiento, etiqueta);
        rejilla.Loaded += (_, _) => PintarAviso(glifo, porCiento, etiqueta);
        return rejilla;
    }

    void PintarAviso(string glifo, int porCiento, string etiqueta)
    {
        if (_barraAviso == null || _glifoAviso == null || _cifraAviso == null) return;

        _glifoAviso.Text = glifo;
        _cifraAviso.Text = $"{porCiento}%";
        _cifraAviso.ToolTip = etiqueta;

        if (_barraAviso.Parent is not Border canal || canal.ActualWidth <= 0) return;

        double destino = canal.ActualWidth * Math.Clamp(porCiento, 0, 100) / 100.0;
        double desde = _barraAviso.ActualWidth;

        // Como siempre: el valor de verdad es el final y la animación solo lo recorre
        _barraAviso.BeginAnimation(WidthProperty, null);
        _barraAviso.Width = destino;
        _barraAviso.BeginAnimation(WidthProperty, Kf(desde, destino, 180, 0, Suave));
    }

    void RearmarCierre()
    {
        _cierraAviso ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _cierraAviso.Stop();
        _cierraAviso.Tick -= CerrarAvisoTick;
        _cierraAviso.Tick += CerrarAvisoTick;
        _cierraAviso.Start();
    }

    static void CerrarAvisoTick(object? s, EventArgs e)
    {
        _cierraAviso?.Stop();
        foreach (var i in Islas.Where(i => i._panelAbierto == PanelAviso).ToList()) i.CerrarPanel();
    }
}
