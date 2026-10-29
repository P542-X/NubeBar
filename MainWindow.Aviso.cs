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
//
// ---------- Por qué este NO lleva la concha de C5 ----------
//
// Al darle superficie propia a los paneles que no la tenían —bandeja, batería,
// notificaciones y el menú contextual— este se quedó fuera **a propósito**, y conviene que
// quede escrito para que no parezca un olvido.
//
// Esto no es un panel: es la isla **estirándose** un instante. No se abre ni se cierra, no
// se navega y no tiene contenido que leer — un icono y una barra durante segundo y medio.
// Una tarjeta con filo y esquinas dentro de la isla convertiría un gesto en una ventanita, y
// aquí se quiere justo lo contrario: que parezca la propia barra reaccionando.
//
// La vista general queda fuera por el motivo opuesto: cada tarjeta de ventana ya trae su
// superficie, y una concha alrededor sería un marco de pantalla completa rodeando a otros
// marcos.
public partial class MainWindow : Window
{
    const string PanelAviso = "aviso";
    const double AltoAviso = 44;

    static int _volumenVisto = -1;
    static bool _mudoVisto;
    static DispatcherTimer? _cierraAviso;

    Border? _barraAviso;
    TextBlock? _glifoAviso, _cifraAviso;

    // ---------- Qué hay DENTRO del panel de aviso ----------
    //
    // El panel es uno y lo usan dos cosas: la barra del volumen y el texto del indicador de
    // NubeHypr. Cada una guarda sus propios controles, y la primera versión de esto decidía
    // "¿ya está puesto lo mío?" mirando si sus campos eran distintos de null.
    //
    // Eso está mal y falla así: sale un mensaje de texto (`_textoDelAviso` queda apuntando a
    // su TextBlock), luego subes el volumen y `AbrirPanel` sustituye el contenido — pero
    // `_textoDelAviso` sigue apuntando al de antes, ya desenganchado del árbol visual. El
    // siguiente mensaje cree que su panel está puesto, pinta en un elemento que no está en
    // pantalla y **no se ve nada**, además de acortar el aviso de volumen.
    //
    // Así que lo que hay dentro se APUNTA, en vez de deducirse. Un campo, dos valores, y
    // quien crea el contenido es quien lo pone.
    enum Dentro { Nada, Barra, Texto }
    Dentro _dentroDelAviso = Dentro.Nada;

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
            // Si ya hay un aviso puesto Y lo que hay dentro es la barra, se reaprovecha: así
            // subir el volumen cinco veces seguidas no abre y cierra el panel cinco veces.
            // Si dentro hay un texto, no se reaprovecha: se sustituye el contenido.
            if (_panelAbierto == PanelAviso && _dentroDelAviso == Dentro.Barra
                && _barraAviso != null && _glifoAviso != null)
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
        // Lo que hay dentro, apuntado; y los controles del texto a null, que a partir de aquí
        // apuntarían a elementos desenganchados del árbol.
        _dentroDelAviso = Dentro.Barra;
        _textoDelAviso = null; _glifoDelAviso = null; _ultimoMensaje = null;

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
            Background = _superficieAlta,
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

    void RearmarCierre(TimeSpan? cuanto = null)
    {
        _cierraAviso ??= new DispatcherTimer();
        _cierraAviso.Interval = cuanto ?? TimeSpan.FromMilliseconds(1500);
        _cierraAviso.Stop();
        _cierraAviso.Tick -= CerrarAvisoTick;
        _cierraAviso.Tick += CerrarAvisoTick;
        _cierraAviso.Start();
    }

    // ================= D6: el indicador breve de un comando =================
    //
    // ---------- Por qué va EN EL MISMO panel que el volumen ----------
    //
    // Porque el del volumen ya resuelve lo difícil, y lo resuelve bien: se reaprovecha si ya
    // está puesto (subir el volumen cinco veces no abre y cierra el panel cinco veces), se
    // cierra solo, y **no se traga el menú Inicio ni la vista general** si están abiertos.
    // Una segunda superficie flotante para esto sería una segunda ocasión de equivocarse en
    // esas tres cosas.
    //
    // Lo único que cambia es el cuerpo: donde el volumen pinta una barra y un porcentaje,
    // esto pinta un glifo y una frase.
    HyprAviso.Mensaje? _ultimoMensaje;
    TextBlock? _textoDelAviso, _glifoDelAviso;

    // El atajo lo atiende la isla que tiene el gancho —la jefa—, pero el indicador tiene que
    // salir donde el usuario está mirando: en la pantalla del ratón, como la vista general
    // cuando se abre con el teclado.
    //
    // Con un matiz: si esa isla tiene abierto el menú Inicio o la vista general, el aviso no
    // se los come (`MostrarMensaje` se niega), así que se busca otra isla libre. Y si no hay
    // ninguna, no sale — y está bien que no salga: con la vista general delante, lo que el
    // comando ha hecho se ve en las propias tarjetas.
    public static void Avisar(HyprAviso.Mensaje m)
    {
        var raton = IslaDelRaton();
        if (raton != null && (raton._panelAbierto == null || raton._panelAbierto == PanelAviso))
        {
            raton.MostrarMensaje(m);
            return;
        }
        Islas.FirstOrDefault(i => i._panelAbierto == null || i._panelAbierto == PanelAviso)
             ?.MostrarMensaje(m);
    }

    public void MostrarMensaje(HyprAviso.Mensaje m)
    {
        try
        {
            bool yaEsta = _panelAbierto == PanelAviso && _dentroDelAviso == Dentro.Texto
                          && _textoDelAviso != null;

            // El mismo mensaje otra vez: solo se alarga el tiempo. Repintar el mismo texto
            // no se ve como una actualización, se ve como un tirón.
            if (yaEsta && _ultimoMensaje is { } antes && HyprAviso.NiSiquieraEso(true, antes, m))
            {
                RearmarCierre(HyprAviso.Duracion(m.Texto));
                return;
            }

            _ultimoMensaje = m;

            if (yaEsta)
            {
                PintarMensaje(m);
                RearmarCierre(HyprAviso.Duracion(m.Texto));
                return;
            }

            // Igual que el aviso de volumen: si hay otro panel abierto, el indicador no se
            // lo come. Lo que has abierto a propósito manda sobre un aviso de un segundo.
            if (_panelAbierto != null && _panelAbierto != PanelAviso) return;

            AbrirPanel(PanelAviso, CrearMensaje(m), AnchoDelMensaje(m.Texto), AltoAviso);
            RearmarCierre(HyprAviso.Duracion(m.Texto));
        }
        catch (Exception ex) { Log.Error("el indicador breve de NubeHypr", ex); }
    }

    // Un ancho que dependa del texto, con topes. Fijo se queda corto con «Espacio →: no hay
    // más escritorios» y ridículamente ancho con «Flotante».
    static double AnchoDelMensaje(string texto) =>
        Math.Clamp(110 + (texto ?? "").Length * 7.2, 240, 560);

    FrameworkElement CrearMensaje(HyprAviso.Mensaje m)
    {
        // Simétrico a `CrearAviso`: se apunta qué hay dentro y se sueltan los controles de la
        // barra, que desde aquí quedan fuera del árbol visual.
        _dentroDelAviso = Dentro.Texto;
        _barraAviso = null; _glifoAviso = null; _cifraAviso = null;

        var fila = new Grid { Margin = new Thickness(14, 0, 14, 10), Height = AltoAviso - 12 };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _glifoDelAviso = Glifo(m.Glifo);
        _glifoDelAviso.FontSize = 16;
        _glifoDelAviso.VerticalAlignment = VerticalAlignment.Center;
        fila.Children.Add(_glifoDelAviso);

        _textoDelAviso = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(_textoDelAviso, 1);
        fila.Children.Add(_textoDelAviso);

        PintarMensaje(m);
        return fila;
    }

    void PintarMensaje(HyprAviso.Mensaje m)
    {
        if (_textoDelAviso == null || _glifoDelAviso == null) return;

        // Azul para "esto ha pasado", coral para "ojo". Los mismos dos papeles de D6, y por
        // el mismo motivo: en un tema con el acento amarillo, usar el acento para las dos
        // cosas las haría indistinguibles.
        var color = m.Como == HyprAviso.Tono.Aviso
            ? HyprPinta.CoralTinta(_roles, _roles.FondoOpaco)
            : HyprPinta.AzulTinta(_roles, _roles.FondoOpaco);

        _glifoDelAviso.Text = m.Glifo;
        _glifoDelAviso.Foreground = new SolidColorBrush(color);
        _textoDelAviso.Text = m.Texto;
        _textoDelAviso.Foreground = _texto;

        System.Windows.Automation.AutomationProperties.SetName(_textoDelAviso, m.Texto);
    }

    static void CerrarAvisoTick(object? s, EventArgs e)
    {
        _cierraAviso?.Stop();
        foreach (var i in Islas.Where(i => i._panelAbierto == PanelAviso).ToList())
        {
            i.CerrarPanel();
            // Ya no hay nada dentro. La comprobación de "¿está puesto lo mío?" exige además
            // que el panel abierto sea este, así que esto no es lo que impide el fallo —
            // es dejar el campo diciendo la verdad en vez de un valor de hace un rato.
            i._dentroDelAviso = Dentro.Nada;
        }
    }
}
