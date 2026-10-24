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

    // Y al revés: lo más ancha que puede ser una tarjeta sin salirse de la pantalla.
    // Es la inversa exacta de AnchoConTarjeta, y hace falta porque el panel Inicio pide
    // 660 px de ancho: en un portátil de 1366 con una barra de 56 eso cabe, pero en una
    // pantalla estrecha o con un grosor grande no, y la tarjeta se quedaba cortada por
    // el filo de la ventana (no por un recorte: simplemente fuera de ella).
    internal static double AnchoMaximoDeTarjeta(double anchoPantalla, double grosor,
                                                double margen, double separacion) =>
        Math.Max(120, anchoPantalla - (margen + grosor + margen + separacion + margen));

    // Lo más alta que puede ser sin salirse por arriba o por abajo
    internal static double AltoMaximoDeTarjeta(double altoPantalla, double margen) =>
        Math.Max(140, altoPantalla - margen * 2);

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


    // ---------- Los paneles, en una ventana superpuesta al lado ----------
    //
    // En horizontal el panel es el "cajón": la isla crece de alto y el panel se
    // despliega desde dentro, como una sola superficie. En vertical eso no se puede: la
    // barra ya va de arriba abajo y no tiene hacia dónde crecer. Sale al lado.
    //
    // Y sale en SU PROPIA VENTANA, no en un trozo de la ventana de la barra. El por qué
    // —con los dos fallos que lo obligaron— está escrito en VentanaSuperpuesta.cs.
    //
    // `scrollPropio` lo pasan los paneles que ya se desplazan por dentro: el menú Inicio
    // tiene su propio ScrollViewer para la lista. A esos se les recorta el alto y su
    // fila elástica se encarga; envolverlos en otro scroll sería lo peor de los dos
    // mundos (la caja de búsqueda subiéndose al desplazar, y dos barras).
    VentanaSuperpuesta? _superpuesta;

    public bool PanelSuperpuestoAbierto => _superpuesta is { IsVisible: true };

    VentanaSuperpuesta Superpuesta() => _superpuesta ??= new VentanaSuperpuesta(this);

    void AbrirPanelFlotante(string nombre, FrameworkElement contenido, double ancho, double alto,
                            bool teclado, FrameworkElement? ancla, bool scrollPropio = false)
    {
        var v = Superpuesta();

        // ---- 1. Lo que cabe, en unidades de WPF ----
        double anchoPantalla = _monitor.Ancho / _monitor.Escala;
        double altoPantalla = _monitor.Alto / _monitor.Escala;
        double topeAncho = AnchoMaximoDeTarjeta(anchoPantalla, Grosor, _cfg.Barra.Margen, SeparacionPanel);
        double topeAlto = AltoMaximoDeTarjeta(altoPantalla, _cfg.Barra.Margen);

        if (ancho > topeAncho)
        {
            Log.Info($"panel {nombre} (vertical): pedía {ancho:0} de ancho y al lado de la barra " +
                     $"caben {topeAncho:0}; se estrecha");
            ancho = topeAncho;
        }

        _panelAbierto = nombre;
        _panelAncho = ancho;
        _anclaFlotante = ancla;
        _panelScrollPropio = scrollPropio;

        // ---- 2. Vestir y medir ----
        VestirPanelFlotante();
        v.Cajon.Content = contenido;

        double bordes = v.Tarjeta.BorderThickness.Top + v.Tarjeta.BorderThickness.Bottom;
        try
        {
            contenido.Measure(new Size(ancho, double.PositiveInfinity));
            double pide = contenido.DesiredSize.Height + bordes;

            if (pide > topeAlto)
            {
                if (scrollPropio)
                    Log.Info($"panel {nombre} (vertical): pedía {pide:0} de alto y caben {topeAlto:0}; " +
                             "se recorta y se desplaza con su propio scroll");
                else
                {
                    Log.Info($"panel {nombre} (vertical): necesita {pide:0} y caben {topeAlto:0}; se desplaza por dentro");
                    v.Cajon.Content = null;
                    v.Cajon.Content = EnvolverConScroll(contenido, topeAlto - bordes);
                    contenido = (FrameworkElement)v.Cajon.Content;
                }
                pide = topeAlto;
            }
            if (pide > alto) alto = pide;
            if (alto > topeAlto) alto = topeAlto;
        }
        catch (Exception ex) { Log.Error("medir el panel superpuesto", ex); }

        _panelAlto = alto;

        // ---- 3. Colocar en coordenadas de PANTALLA y elevar ----
        // Se muestra antes de colocarla para que tenga hwnd; nace fuera de la vista
        // (-32000) a propósito, así no se ve el salto.
        if (!v.IsVisible) v.Show();
        ColocarPanelFlotante(ancla);
        v.Elevar();
        if (teclado) { _panelConTeclado = true; v.Teclado(true); }

        // La barra ya no cambia de tamaño al abrir un panel, porque la tarjeta no vive
        // dentro de su ventana. Se recoloca con su propio grosor por si venía de una
        // vez en la que sí se ensanchó.
        ColocarEnMonitorVertical(Grosor + _cfg.Barra.Margen * 2);

        // ---- 4. Entrar ----
        // Solo fundido: con una ventana del tamaño exacto de la tarjeta, un
        // desplazamiento la recortaría contra su propio filo.
        v.Tarjeta.BeginAnimation(OpacityProperty, null);
        v.Tarjeta.Opacity = 1;
        v.Tarjeta.BeginAnimation(OpacityProperty, Kf(0, 1, 180));

        var r = v.Rectangulo();
        Log.Info($"panel abierto al lado: {nombre} ({ancho:0}x{alto:0}, teclado={teclado}) " +
                 $"en ({r.Izq},{r.Arr})-({r.Der},{r.Aba})");
    }

    void CerrarPanelFlotante()
    {
        if (_panelAbierto == null) return;
        var nombre = _panelAbierto;
        _panelAbierto = null;
        _panelAlto = _panelAncho = 0;
        _anclaFlotante = null;

        if (_panelConTeclado) { _panelConTeclado = false; _superpuesta?.Teclado(false); }

        if (_superpuesta is not { } v) return;
        if (v.Cajon.Content is FrameworkElement viejo) viejo.IsHitTestVisible = false;

        var fundido = Kf(v.Tarjeta.Opacity, 0, 200, 0, Comoda);
        fundido.Completed += (_, _) =>
        {
            if (_panelAbierto != null) return;      // se abrió otro por el camino
            // Esconder la ventana es lo que devuelve el orden Z: deja de estar en la
            // pila, y el de la barra no se ha tocado en ningún momento.
            QuitarPanelFlotante();
        };
        v.Tarjeta.Opacity = 0;
        v.Tarjeta.BeginAnimation(OpacityProperty, fundido);

        Log.Info($"panel cerrado al lado: {nombre}");
    }

    // Los paneles que se rellenan por detrás (la lista de wifis, el test de velocidad)
    // se midieron cuando aún estaban a medias: aquí se vuelve a medir y la tarjeta
    // crece o encoge a lo que ahora necesita, sin moverse de sitio.
    void ReajustarPanelFlotante(string nombre)
    {
        if (_panelAbierto != nombre || _superpuesta is not { } v) return;
        if (v.Cajon.Content is not FrameworkElement contenido) return;
        try
        {
            double bordes = v.Tarjeta.BorderThickness.Top + v.Tarjeta.BorderThickness.Bottom;
            double tope = AltoMaximoDeTarjeta(_monitor.Alto / _monitor.Escala, _cfg.Barra.Margen);
            contenido.Measure(new Size(_panelAncho, double.PositiveInfinity));
            double alto = contenido.DesiredSize.Height + bordes;

            if (alto > tope)
            {
                if (_panelScrollPropio) { /* se desplaza él: solo se le recorta el alto */ }
                else if (contenido is ScrollViewer { Tag: TopeDePanel } ya) ya.MaxHeight = tope - bordes;
                else
                {
                    Log.Info($"panel {nombre} (vertical): ha crecido hasta {alto:0} y caben {tope:0}; se desplaza por dentro");
                    v.Cajon.Content = null;
                    v.Cajon.Content = EnvolverConScroll(contenido, tope - bordes);
                }
                alto = tope;
            }
            if (Math.Abs(alto - _panelAlto) < 1) return;

            _panelAlto = alto;
            ColocarPanelFlotante(_anclaFlotante);
        }
        catch (Exception ex) { Log.Error("reajustar el panel superpuesto", ex); }
    }

    FrameworkElement? _anclaFlotante;
    bool _panelScrollPropio;

    // ---------- El rectángulo, en coordenadas de pantalla ----------
    //
    // Esta es la diferencia de fondo con lo que había: ya no se pone un Margin dentro de
    // otra ventana confiando en que la ventana sea bastante grande. Se calcula el
    // rectángulo final en PÍXELES DE PANTALLA, se recorta contra el área útil del
    // monitor y se coloca la ventana ahí. Así no hay ningún contenedor que pueda
    // cortarlo, y no depende del tamaño de la ventana de la barra.
    void ColocarPanelFlotante(FrameworkElement? ancla)
    {
        if (_superpuesta is not { } v) return;

        var m = _monitor;
        double escala = m.Escala;
        double altoPantalla = m.Alto / escala;

        // El centro del widget que lo ha abierto, en unidades de pantalla
        double? centro = null;
        try
        {
            if (ancla is { IsVisible: true } a && a.ActualHeight > 0)
            {
                var p = a.PointToScreen(new Point(0, a.ActualHeight / 2));
                centro = (p.Y - m.Y) / escala;
            }
        }
        catch { }

        var (desplazado, y) = SitioDeLaTarjeta(_cfg.Barra.Margen, Grosor, SeparacionPanel,
                                               altoPantalla, _panelAlto, centro);

        // A la derecha de la barra si está a la izquierda, y a su izquierda si está a la
        // derecha: el panel sale siempre hacia DENTRO de la pantalla.
        int anchoPx = Math.Max(1, (int)Math.Round(_panelAncho * escala));
        int altoPx = Math.Max(1, (int)Math.Round(_panelAlto * escala));
        int despPx = (int)Math.Round(desplazado * escala);
        int x = PegadaIzquierda ? m.X + despPx : m.Derecha - despPx - anchoPx;
        int yPx = m.Y + (int)Math.Round(y * escala);

        // Y recortado al área útil, que es lo que impide que asome por un filo. Si
        // viniera degenerada (pasa mientras el shell recalcula el área de trabajo),
        // manda el monitor entero antes que colocar la ventana en cualquier sitio.
        int utilX = m.TrabajoAncho > 0 ? m.TrabajoX : m.X;
        int utilY = m.TrabajoAlto > 0 ? m.TrabajoY : m.Y;
        int utilDer = m.TrabajoAncho > 0 ? m.TrabajoX + m.TrabajoAncho : m.Derecha;
        int utilAba = m.TrabajoAlto > 0 ? m.TrabajoY + m.TrabajoAlto : m.Abajo;
        // El área útil puede excluir nuestra propia franja reservada, y el panel tiene
        // derecho a ocuparla: para el recorte se cuenta el monitor por el lado de la
        // barra y el área útil por los otros tres.
        if (PegadaIzquierda) utilX = Math.Min(utilX, m.X); else utilDer = Math.Max(utilDer, m.Derecha);

        x = Math.Clamp(x, utilX, Math.Max(utilX, utilDer - anchoPx));
        yPx = Math.Clamp(yPx, utilY, Math.Max(utilY, utilAba - altoPx));

        v.Colocar(x, yPx, anchoPx, altoPx);
    }

    // La tarjeta se viste como la isla: mismo fondo, mismo filo de acento, mismas
    // esquinas. Los pinceles son fijos y animados (ver AnimarColor), así que basta
    // asignarlos y ya cambian de color solos al cambiar de tema.
    void VestirPanelFlotante()
    {
        if (_superpuesta is not { } v) return;
        v.Tarjeta.Background = _fondo;
        v.Tarjeta.BorderBrush = _acento;
        v.Tarjeta.BorderThickness = Isla.BorderThickness;
        v.Tarjeta.CornerRadius = Isla.CornerRadius;
        v.Tarjeta.Padding = new Thickness(0);
    }

    // Al recargar el config, al cambiar de orientación o al terminar de cerrarse: la
    // ventana se esconde —eso devuelve el orden Z— y se vacía.
    void QuitarPanelFlotante()
    {
        if (_superpuesta is not { } v) return;
        if (_panelConTeclado) { _panelConTeclado = false; v.Teclado(false); }
        v.Tarjeta.BeginAnimation(OpacityProperty, null);
        v.Tarjeta.Opacity = 1;
        v.Cajon.Content = null;
        if (v.IsVisible) v.Hide();
        _anclaFlotante = null;
        _panelScrollPropio = false;
    }

    // ---------- El resumen para el log ----------
    string ResumenVertical() =>
        !Vertical ? "" : $" | vertical {(PegadaIzquierda ? "izquierda" : "derecha")} " +
                         $"ventana {ActualWidth:0}x{ActualHeight:0} barra {Isla.ActualWidth:0}x{Isla.ActualHeight:0} " +
                         $"zonas={Izq.Children.Count}/{Cen.Children.Count}/{Der.Children.Count}";
}
