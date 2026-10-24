using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NubeBar;

// La barra VERTICAL: pegada a la izquierda o a la derecha, de arriba abajo de la
// pantalla, con las zonas apiladas y los paneles saliendo flotando al lado.
//
// Hasta B5 un borde lateral en modo barra se avisaba en el log y se trataba como
// "arriba", porque media barra vertical es peor que ninguna. Esto es la otra mitad.
//
// La idea que lo hace manejable: la nube ya sabía vivir en una franja fija (el modo
// barra horizontal). Lo único que de verdad cambia es **qué eje es el grosor y qué eje
// es el largo**. Por eso aquí no hay una segunda nube: hay una docena de sitios donde
// se pregunta `Vertical` y se cambian las dos medidas de sitio.
//
// Lo que NO se ha intentado: el menú Inicio de 860 px rehecho para una columna. Sale
// como tarjeta flotante al lado, con su ancho de siempre, y se desplaza por dentro si
// no cabe de alto. Un menú pensado para vertical es trabajo posterior.
public partial class MainWindow : Window
{
    // ---------- ¿Estoy en vertical? ----------
    public bool Vertical => PanelNube.EsVertical(PanelPrincipal.Modo, PanelPrincipal.Posicion);

    bool PegadaIzquierda => PanelNube.PegadaAlaIzquierda(PosicionEfectiva);

    // El grosor de la franja, que en vertical es su ANCHO. Es el mismo número que en
    // horizontal era el alto (`Barra.Alto`, derivado de `PanelNube.Grosor`): el panel
    // tiene un grosor, y lo que cambia es contra qué eje se mide.
    double Grosor => _cfg.Barra.Alto;

    // Lo que mide la ventana en el eje del grosor, ahora mismo. En horizontal es su
    // alto y en vertical su ancho, y en los dos casos hay que sumarle el panel abierto.
    double GrosorVentanaAhora()
    {
        double propio = Grosor + _cfg.Barra.Margen * 2;
        if (_panelAbierto == null) return propio;
        return Vertical ? AnchoConTarjeta(Grosor, _cfg.Barra.Margen, SeparacionPanel, _panelAncho)
                        : propio + _panelAlto;
    }

    // ---------- La aritmética de la tarjeta, aparte y pura ----------
    //
    // Está fuera de la ventana para poder probarla sin abrir nada: el clic que abre un
    // panel no se puede automatizar sin riesgo de escribir en la ventana de otro, así
    // que lo que se comprueba a mano es que la tarjeta aparece, y lo que se comprueba
    // en frío es que aparece DONDE toca.

    // Lo que tiene que medir la ventana de ancho para que quepan la barra y la tarjeta
    internal static double AnchoConTarjeta(double grosor, double margen, double separacion, double anchoTarjeta) =>
        margen + grosor + margen + separacion + anchoTarjeta + margen;

    // Dónde va la tarjeta dentro de esa ventana:
    //  - "Desplazado" es lo que se aparta del lado de la barra (el Margin que le toca
    //    por su lado, que es el izquierdo si la barra está a la izquierda y el derecho
    //    si está a la derecha);
    //  - "Y" es su borde de arriba, alineado con el widget que la abrió y sin salirse
    //    nunca de la pantalla.
    internal static (double Desplazado, double Y) SitioDeLaTarjeta(
        double margen, double grosor, double separacion,
        double altoPantalla, double altoTarjeta, double? centroDelAncla)
    {
        double desplazado = grosor + margen + separacion;
        double y = centroDelAncla is { } c ? c - altoTarjeta / 2 : (altoPantalla - altoTarjeta) / 2;
        y = Math.Clamp(y, margen, Math.Max(margen, altoPantalla - altoTarjeta - margen));
        return (desplazado, y);
    }

    // Hueco entre la barra y la tarjeta del panel. El mismo margen que la barra usa
    // contra el filo de la pantalla: así los tres huecos se ven iguales.
    double SeparacionPanel => Math.Max(6, _cfg.Barra.Margen);

    // ---------- Colocar la ventana ----------
    // En vertical la ventana es una columna del alto del monitor, pegada a su lado. El
    // ancho crece cuando se abre un panel, porque la tarjeta vive DENTRO de la ventana
    // (si no, los clics del panel caerían en el escritorio).
    //
    // Va en píxeles y no en Left/Top/Width/Height por lo mismo que la horizontal: con
    // dos monitores a distinto DPI las unidades de WPF no significan lo mismo en cada
    // uno (ver ColocarEnMonitor).
    void ColocarEnMonitorVertical(double anchoVentanaDip)
    {
        var m = _monitor;
        double escala = _self != IntPtr.Zero ? EscalaDeWpf() : m.Escala;
        int anchoPx = Math.Max(1, (int)Math.Round(anchoVentanaDip * escala));
        int x = PegadaIzquierda ? m.X : m.Derecha - anchoPx;

        if (_self == IntPtr.Zero)
        {
            Left = x; Top = m.Y; Width = anchoVentanaDip; Height = m.Alto / escala;
            return;
        }

        Nativo.Colocar(_self, x, m.Y, anchoPx, m.Alto);
        // Igual que en horizontal: el tamaño lo pone SetWindowPos en píxeles y hay que
        // soltar Width/Height de WPF, o al primer WM_DPICHANGED los reimpone y la
        // ventana se encoge.
        Width = double.NaN;
        Height = double.NaN;
    }

    // ---------- Las zonas, apiladas ----------
    //
    // El Grid de las zonas pasa de tres columnas (* Auto *) a tres FILAS con las mismas
    // proporciones, y los StackPanel de horizontal a vertical. Las zonas conservan su
    // significado: Izquierda es "al principio" (arriba), Centro es el medio y Derecha
    // es "al final" (abajo). Es lo que hace Plasma, y así los widgets, la biblioteca y
    // el modo edición siguen hablando de las mismas tres zonas sin enterarse.
    void OrientarZonas()
    {
        bool v = Vertical;

        Contenido.ColumnDefinitions.Clear();
        Contenido.RowDefinitions.Clear();
        if (v)
        {
            Contenido.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Contenido.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Contenido.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        else
        {
            Contenido.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Contenido.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Contenido.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // El sitio de cada zona en el Grid. Se pone aquí y no en el XAML para que no
        // dependa de cómo WPF recorta un Grid.Row que no existe: en horizontal las tres
        // van en la fila 0 y lo que manda es su columna, y en vertical al revés.
        var zonas = new[] { Izq, Cen, Der };
        for (int i = 0; i < zonas.Length; i++)
        {
            Grid.SetColumn(zonas[i], v ? 0 : i);
            Grid.SetRow(zonas[i], v ? i : 0);
        }

        var orientacion = v ? Orientation.Vertical : Orientation.Horizontal;
        Izq.Orientation = Cen.Orientation = Der.Orientation = orientacion;

        Izq.HorizontalAlignment = v ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Izq.VerticalAlignment = v ? VerticalAlignment.Top : VerticalAlignment.Center;
        Cen.HorizontalAlignment = HorizontalAlignment.Center;
        Cen.VerticalAlignment = VerticalAlignment.Center;
        Cen.Margin = v ? new Thickness(0, 8, 0, 8) : new Thickness(8, 0, 8, 0);
        Der.HorizontalAlignment = v ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        Der.VerticalAlignment = v ? VerticalAlignment.Bottom : VerticalAlignment.Center;
    }

    // ---------- La forma de la barra ----------
    // El equivalente vertical de ColocarCajon: qué mide la isla, dónde se pega y qué
    // pasa con el cajón de dentro (nada: en vertical no se usa).
    void ColocarBarraVertical()
    {
        FilaIsla.Height = new GridLength(1, GridUnitType.Star);
        FilaCajon.Height = new GridLength(0);
        Grid.SetRow(Contenido, 0);
        Grid.SetRow(Cajon, 1);
        Cajon.Visibility = Visibility.Collapsed;

        Isla.VerticalAlignment = VerticalAlignment.Stretch;
        Isla.HorizontalAlignment = PegadaIzquierda ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Isla.Padding = new Thickness(0, 10, 0, 10);
        Isla.MinWidth = 0;
        Isla.Margin = new Thickness(_cfg.Barra.Margen, _cfg.Barra.Margen,
                                    _cfg.Barra.Margen, _cfg.Barra.Margen);

        Isla.BeginAnimation(WidthProperty, null);
        Isla.BeginAnimation(HeightProperty, null);
        Isla.Width = Grosor;
        Isla.Height = double.NaN;      // la estira su alineación, no un número
    }

    // Y deshacerlo al volver a horizontal. Sin esto, cambiar de lateral a "arriba" en
    // caliente dejaba la isla con MinWidth=0, el padding girado y el cajón oculto.
    void ColocarBarraHorizontal()
    {
        Cajon.Visibility = Visibility.Visible;
        Isla.Padding = new Thickness(10, 0, 10, 0);
        Isla.MinWidth = 120;
        Isla.BeginAnimation(HeightProperty, null);
        Isla.Height = double.NaN;
    }

    // ---------- Los paneles, flotando al lado ----------
    //
    // En horizontal el panel es el "cajón": la isla crece de alto y el panel se
    // despliega desde dentro, como una sola superficie. En vertical eso no se puede:
    // la barra ya va de arriba abajo y no tiene hacia dónde crecer. Así que el panel
    // sale como una TARJETA aparte, pegada al lado de la barra, que es exactamente lo
    // que hace Plasma — y encima es más sencillo que el recorte de la horizontal.
    //
    // La tarjeta vive dentro de la MISMA ventana (que se ensancha para dejarle sitio):
    // una ventana nueva tendría que resolver otra vez el no robar foco, el orden Z, el
    // DPI y el guardián, y ya está todo resuelto aquí.
    void AbrirPanelFlotante(string nombre, FrameworkElement contenido, double ancho, double alto,
                            bool teclado, FrameworkElement? ancla)
    {
        _panelAbierto = nombre;
        _panelAncho = ancho;
        _anclaFlotante = ancla;

        VestirPanelFlotante();
        CajonFlotante.Content = contenido;
        PanelFlotante.Visibility = Visibility.Visible;

        // Medir de verdad: el alto que pide quien abre el panel es una estimación, y en
        // vertical el tope es la pantalla entera menos los márgenes.
        double bordes = PanelFlotante.BorderThickness.Top + PanelFlotante.BorderThickness.Bottom;
        double tope = Math.Max(200, _monitor.Alto / _monitor.Escala - _cfg.Barra.Margen * 2);
        try
        {
            contenido.Measure(new Size(ancho, double.PositiveInfinity));
            double pide = contenido.DesiredSize.Height + bordes;
            if (pide > tope)
            {
                Log.Info($"panel {nombre} (vertical): necesita {pide:0} y caben {tope:0}; se desplaza por dentro");
                CajonFlotante.Content = null;
                CajonFlotante.Content = EnvolverConScroll(contenido, tope - bordes);
                contenido = (FrameworkElement)CajonFlotante.Content;
                pide = tope;
            }
            if (pide > alto) alto = pide;
        }
        catch (Exception ex) { Log.Error("medir el panel flotante", ex); }

        _panelAlto = alto;
        PanelFlotante.Width = ancho;
        PanelFlotante.Height = alto;

        // La ventana primero, que es la que tiene que dar sitio a la tarjeta
        ColocarEnMonitorVertical(GrosorVentanaAhora());
        ColocarPanelFlotante(ancla);
        if (teclado) ActivarTeclado(true);

        // Entra deslizándose desde la barra, en la dirección en la que se abre
        double desde = PegadaIzquierda ? -18 : 18;
        var mover = new TranslateTransform { X = desde };
        PanelFlotante.RenderTransform = mover;
        PanelFlotante.Opacity = 1;
        CajonFlotante.Opacity = 1;
        mover.BeginAnimation(TranslateTransform.XProperty, Kf(desde, 0, 300, 0, Muelle));
        mover.X = 0;
        PanelFlotante.BeginAnimation(OpacityProperty, Kf(0, 1, 180));

        Log.Info($"panel abierto al lado: {nombre} ({ancho:0}x{alto:0}, teclado={teclado})");
    }

    void CerrarPanelFlotante()
    {
        if (_panelAbierto == null) return;
        var nombre = _panelAbierto;
        _panelAbierto = null;
        _panelAlto = _panelAncho = 0;
        if (_panelConTeclado) ActivarTeclado(false);

        _anclaFlotante = null;
        if (CajonFlotante.Content is FrameworkElement viejo) viejo.IsHitTestVisible = false;

        double hasta = PegadaIzquierda ? -14 : 14;
        if (PanelFlotante.RenderTransform is TranslateTransform t)
        {
            t.BeginAnimation(TranslateTransform.XProperty, Kf(t.X, hasta, 220, 0, Comoda));
            t.X = hasta;
        }
        var fundido = Kf(PanelFlotante.Opacity, 0, 200, 0, Comoda);
        fundido.Completed += (_, _) =>
        {
            if (_panelAbierto != null) return;      // se abrió otro por el camino
            PanelFlotante.Visibility = Visibility.Collapsed;
            CajonFlotante.Content = null;
            if (Vertical) ColocarEnMonitorVertical(GrosorVentanaAhora());
        };
        PanelFlotante.Opacity = 0;
        PanelFlotante.BeginAnimation(OpacityProperty, fundido);

        Log.Info($"panel cerrado al lado: {nombre}");
    }

    // Los paneles que se rellenan por detrás (la lista de wifis, el test de velocidad)
    // se midieron cuando aún estaban a medias: aquí se vuelve a medir y la tarjeta
    // crece o encoge a lo que ahora necesita, sin moverse de sitio.
    void ReajustarPanelFlotante(string nombre)
    {
        if (_panelAbierto != nombre || CajonFlotante.Content is not FrameworkElement contenido) return;
        try
        {
            double bordes = PanelFlotante.BorderThickness.Top + PanelFlotante.BorderThickness.Bottom;
            double tope = Math.Max(200, _monitor.Alto / _monitor.Escala - _cfg.Barra.Margen * 2);
            contenido.Measure(new Size(_panelAncho, double.PositiveInfinity));
            double alto = contenido.DesiredSize.Height + bordes;

            if (alto > tope)
            {
                if (contenido is ScrollViewer { Tag: TopeDePanel } ya) ya.MaxHeight = tope - bordes;
                else
                {
                    Log.Info($"panel {nombre} (vertical): ha crecido hasta {alto:0} y caben {tope:0}; se desplaza por dentro");
                    CajonFlotante.Content = null;
                    CajonFlotante.Content = EnvolverConScroll(contenido, tope - bordes);
                }
                alto = tope;
            }
            if (Math.Abs(alto - _panelAlto) < 1) return;

            double antes = _panelAlto;
            _panelAlto = alto;
            PanelFlotante.BeginAnimation(HeightProperty, null);
            PanelFlotante.Height = alto;
            PanelFlotante.BeginAnimation(HeightProperty, Kf(antes, alto, 220, 0, Suave));
            ColocarPanelFlotante(_anclaFlotante);
        }
        catch (Exception ex) { Log.Error("reajustar el panel flotante", ex); }
    }

    FrameworkElement? _anclaFlotante;

    // La tarjeta se pega al lado de la barra y se alinea con el widget que la ha
    // abierto, sin salirse nunca de la pantalla. Si no hay widget (el menú Inicio con
    // la tecla Windows, por ejemplo), se centra.
    void ColocarPanelFlotante(FrameworkElement? ancla)
    {
        double altoPantalla = _monitor.Alto / _monitor.Escala;

        double? centro = null;
        try
        {
            if (ancla is { IsVisible: true } a && a.ActualHeight > 0)
                centro = a.TransformToAncestor(Raiz).Transform(new Point(0, 0)).Y + a.ActualHeight / 2;
        }
        catch { }

        var (desplazado, y) = SitioDeLaTarjeta(_cfg.Barra.Margen, Grosor, SeparacionPanel,
                                               altoPantalla, _panelAlto, centro);

        // La ventana crece hacia DENTRO de la pantalla: con la barra a la izquierda la
        // tarjeta va después de la franja; con la barra a la derecha, la ventana se ha
        // ensanchado hacia la izquierda y la tarjeta ocupa ese trozo nuevo. Por eso se
        // ancla a SU lado y lo que se desplaza es su propio margen.
        PanelFlotante.HorizontalAlignment = PegadaIzquierda ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        PanelFlotante.VerticalAlignment = VerticalAlignment.Top;
        PanelFlotante.Margin = PegadaIzquierda
            ? new Thickness(desplazado, y, 0, 0)
            : new Thickness(0, y, desplazado, 0);
    }

    // La tarjeta se viste como la isla: mismo fondo, mismo filo de acento, mismas
    // esquinas. Se hace aquí y no en AplicarTema porque los pinceles son fijos y
    // animados (ver AnimarColor): basta asignarlos una vez y ya cambian de color solos.
    void VestirPanelFlotante()
    {
        PanelFlotante.Background = _fondo;
        PanelFlotante.BorderBrush = _acento;
        PanelFlotante.BorderThickness = Isla.BorderThickness;
        PanelFlotante.CornerRadius = Isla.CornerRadius;
        PanelFlotante.Padding = new Thickness(0);
    }

    // Al recargar el config o al cambiar de orientación, la tarjeta se va sin
    // animación: sus medidas ya no valen.
    void QuitarPanelFlotante()
    {
        PanelFlotante.BeginAnimation(OpacityProperty, null);
        PanelFlotante.Visibility = Visibility.Collapsed;
        PanelFlotante.Opacity = 1;
        PanelFlotante.RenderTransform = null;
        PanelFlotante.BeginAnimation(HeightProperty, null);
        CajonFlotante.Content = null;
        _anclaFlotante = null;
    }

    // ---------- El resumen para el log ----------
    string ResumenVertical() =>
        !Vertical ? "" : $" | vertical {(PegadaIzquierda ? "izquierda" : "derecha")} " +
                         $"ventana {ActualWidth:0}x{ActualHeight:0} barra {Isla.ActualWidth:0}x{Isla.ActualHeight:0} " +
                         $"zonas={Izq.Children.Count}/{Cen.Children.Count}/{Der.Children.Count}";
}
