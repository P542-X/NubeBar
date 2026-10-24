using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NubeBar;

// El panel de red: lo que tienes conectado (con su IP y su velocidad de enlace), las
// wifis que hay a la vista para entrar en ellas desde la isla, y un test de velocidad.
public partial class MainWindow : Window
{
    const string PanelRed = "red";
    const double AnchoRed = 420;
    const double AltoFilaWifi = 32;
    const int FilasWifiALaVista = 5;      // las demás, con la rueda

    // Cuántas wifis se ven de golpe. Con quince redes de vecinos el panel crecía hasta
    // medir media pantalla, y la isla pasaba de ser una isla a ser un muro: la lista
    // tiene tope, y en una pantalla pequeña todavía menos. El resto está a una rueda.
    double AltoListaWifi()
    {
        double pantalla = _monitor.Alto / _monitor.Escala;
        // Lo que ocupa el panel SIN la lista (lo conectado, las cabeceras, el test de
        // velocidad y el enlace a Windows), más la fila de módulos de la isla
        double resto = 320 + _cfg.Barra.Alto + _cfg.Barra.Margen * 2;
        double cabe = Math.Floor((pantalla - resto) / AltoFilaWifi) * AltoFilaWifi;
        return Math.Clamp(cabe, AltoFilaWifi * 2, AltoFilaWifi * FilasWifiALaVista);
    }

    StackPanel? _redConectadas, _redLista;
    ScrollViewer? _redScroll;        // la ventanita por la que se ven las wifis
    TextBlock? _redTitulo;           // "Redes wifi · 12"
    TextBlock? _redEstado;
    Red.Wifi? _redConClave;          // la red cuya fila tiene la caja de contraseña abierta
    bool _redConectando;

    // Cifras del test de velocidad del panel abierto
    TextBlock? _velPing, _velBajada, _velSubida, _velNota;
    Button? _velBoton;

    void AbrirRed()
    {
        _redConClave = null;
        AlternarPanel(PanelRed, CrearRed, AnchoRed, 200);
    }

    FrameworkElement CrearRed()
    {
        var columna = new StackPanel { Margin = new Thickness(14, 2, 14, 12) };

        // --- Conectado ahora ---
        columna.Children.Add(Titulito("Conectado"));
        _redConectadas = new StackPanel();
        _redConectadas.Children.Add(Tenue("Mirando…"));
        columna.Children.Add(_redConectadas);

        // --- Wifis a la vista ---
        var cabecera = new Grid { Margin = new Thickness(0, 8, 0, 2) };
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _redTitulo = Titulito("Redes wifi");
        _redTitulo.Margin = new Thickness(0);
        _redTitulo.VerticalAlignment = VerticalAlignment.Center;
        cabecera.Children.Add(_redTitulo);
        var buscar = Boton(Glifo(""), "Volver a buscar redes", () =>
        {
            Red.Escanear();
            if (_redEstado != null) _redEstado.Text = "Buscando redes…";
            // wlanapi necesita unos 4 s para tener la lista nueva tras un escaneo
            Dispatcher.InvokeAsync(async () => { await Task.Delay(4000); await RellenarRed(); });
        });
        buscar.Height = 24;
        Grid.SetColumn(buscar, 1);
        cabecera.Children.Add(buscar);
        columna.Children.Add(cabecera);

        _redEstado = Tenue("");
        _redEstado.Visibility = Visibility.Collapsed;
        columna.Children.Add(_redEstado);

        _redLista = new StackPanel();
        _redLista.SizeChanged += (_, e) => { if (e.WidthChanged) AjustarFilasWifi(); };
        _redScroll = new ScrollViewer
        {
            MaxHeight = AltoListaWifi(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _redLista
        };
        // La rueda se atiende a mano. El ScrollViewer ya lo haría solo, pero cada fila
        // es un Button dentro de un panel que la isla también vigila, y bastaba con que
        // alguien marcara el evento como manejado para que la lista no se moviera y
        // hubiera que arrastrar la barra a pulso. Así baja seguro, y de fila en fila.
        _redScroll.PreviewMouseWheel += (_, e) =>
        {
            if (_redScroll.ScrollableHeight <= 0) return;      // cabe entera: que la rueda siga su camino
            _redScroll.ScrollToVerticalOffset(_redScroll.VerticalOffset - Math.Sign(e.Delta) * AltoFilaWifi * 2);
            e.Handled = true;
        };
        columna.Children.Add(_redScroll);

        // --- Test de velocidad ---
        columna.Children.Add(Titulito("Velocidad"));
        columna.Children.Add(CrearVelocidad());

        // --- A Windows, para lo que no se hace aquí ---
        var ajustes = Boton(new TextBlock { Text = "Ajustes de red de Windows", Foreground = _acentoLegible, FontSize = 11 },
                            "Abrir la configuración de red de Windows", () => { AbrirAjustesRed("ms-settings:network"); });
        ajustes.HorizontalAlignment = HorizontalAlignment.Left;
        ajustes.Margin = new Thickness(-6, 8, 0, 0);
        ajustes.Height = 22;
        columna.Children.Add(ajustes);

        Velocidad.Cambio += VelocidadCambio;
        columna.Unloaded += (_, _) => Velocidad.Cambio -= VelocidadCambio;

        // El panel se abre ya y se rellena por detrás
        Dispatcher.InvokeAsync(RellenarYEscanear, DispatcherPriority.Background);
        return columna;
    }

    // Listar las wifis es instantáneo (28 ms medidos), pero lo que da Windows es su
    // lista en caché, y solo la renueva cuando escanea, más o menos una vez por
    // minuto. Por eso a veces salía una sola red hasta pulsar "buscar". Ahora se
    // enseña la caché al momento, se pide un escaneo y se va repasando unos segundos:
    // las redes nuevas aparecen según Windows las va viendo.
    async Task RellenarYEscanear()
    {
        await RellenarRed();
        if (_panelAbierto != PanelRed) return;
        await Task.Run(Red.Escanear);
        foreach (int ms in new[] { 1500, 2000, 2500 })
        {
            await Task.Delay(ms);
            if (_panelAbierto != PanelRed) return;
            await RellenarRed(soloSiCambia: true);
        }
    }

    string _firmaRedes = "";

    TextBlock Tenue(string texto) => new()
    {
        Text = texto, Foreground = _textoAtenuado, FontSize = 11,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    void AbrirAjustesRed(string uri) => AbrirAjustes(uri);

    // soloSiCambia: para los repasos automáticos. Rehacer la lista cuando no ha
    // cambiado nada la haría parpadear y te devolvería el scroll arriba, y con la caja
    // de la contraseña abierta o conectando no se toca: perderías lo escrito.
    async Task RellenarRed(bool soloSiCambia = false)
    {
        if (_panelAbierto != PanelRed || _redConectadas == null || _redLista == null) return;
        if (soloSiCambia && (_redConClave != null || _redConectando)) return;
        try
        {
            var conexiones = await Task.Run(Red.Conexiones);
            var wifis = await Task.Run(Red.Wifis);
            if (_panelAbierto != PanelRed) return;

            string firma = string.Join("|", wifis.Select(w => $"{w.Nombre}:{w.Conectada}:{w.Senal / 10}"));
            if (soloSiCambia && firma == _firmaRedes) return;
            _firmaRedes = firma;

            // Conectado
            _redConectadas.Children.Clear();
            if (conexiones.Count == 0) _redConectadas.Children.Add(Tenue("Sin conexión"));
            foreach (var c in conexiones) _redConectadas.Children.Add(FilaConexion(c, wifis.FirstOrDefault(w => w.Conectada)));

            // A la vista. El orden que da Windows es el suyo y tu red se perdía entre
            // las de los vecinos: primero las que ya conoces (tienen perfil guardado) y
            // dentro de cada grupo, la que mejor llega.
            _redLista.Children.Clear();
            var visibles = wifis.Where(w => !w.Conectada && w.Senal > 0)
                                .OrderByDescending(w => w.Perfil.Length > 0)
                                .ThenByDescending(w => w.Senal)
                                .ToList();
            if (_redTitulo != null)
                _redTitulo.Text = visibles.Count > FilasWifiALaVista
                    ? $"Redes wifi · {visibles.Count}"      // que se vea que hay más abajo
                    : "Redes wifi";
            if (visibles.Count == 0)
                _redLista.Children.Add(Tenue(wifis.Count == 0 && conexiones.All(c => c.Tipo != Red.Tipo.Wifi)
                    ? "No hay tarjeta wifi, o está apagada"
                    : "No hay más redes a la vista"));
            foreach (var w in visibles) _redLista.Children.Add(FilaWifi(w));

            if (_redEstado != null && !_redConectando && _redEstado.Text == "Buscando redes…")
                _redEstado.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { Log.Error("rellenar el panel de red", ex); }
        ReajustarPanel(PanelRed);
    }

    static string Glifowifi(int senal) => senal switch
    {
        >= 75 => "",
        >= 50 => "",
        >= 25 => "",
        _ => ""
    };

    static string Enlace(long bps) => bps <= 0 ? "" :
        bps >= 1_000_000_000 ? $"{bps / 1e9:0.#} Gb/s" : $"{bps / 1e6:0} Mb/s";

    FrameworkElement FilaConexion(Red.Conexion c, Red.Wifi? wifi)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icono = Glifo(c.Tipo == Red.Tipo.Wifi ? Glifowifi(c.Senal) : "");
        icono.Foreground = _acentoLegible;
        icono.HorizontalAlignment = HorizontalAlignment.Left;
        g.Children.Add(icono);

        var textos = new StackPanel();
        textos.Children.Add(new TextBlock
        {
            Text = c.Tipo == Red.Tipo.Wifi && c.Senal >= 0 ? $"{c.Nombre}   {c.Senal}%" : c.Nombre,
            Foreground = _texto, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis
        });
        var detalle = new[] { c.Ip, Enlace(c.BitsPorSegundo), c.Adaptador }.Where(s => s.Length > 0);
        var linea = Tenue(string.Join("  ·  ", detalle));
        linea.ToolTip = $"IP {c.Ip}\nPuerta de enlace {c.PuertaEnlace}\nEnlace {Enlace(c.BitsPorSegundo)}\n{c.Adaptador}";
        textos.Children.Add(linea);
        Grid.SetColumn(textos, 1);
        g.Children.Add(textos);

        if (wifi != null && c.Tipo == Red.Tipo.Wifi)
        {
            var salir = Boton(new TextBlock { Text = "Desconectar", Foreground = _texto, FontSize = 11, Opacity = 0.8 },
                              $"Desconectarse de {wifi.Nombre}", () =>
            {
                Red.Desconectar(wifi);
                Dispatcher.InvokeAsync(async () => { await Task.Delay(1500); await RellenarRed(); });
            });
            salir.Height = 24;
            Grid.SetColumn(salir, 2);
            g.Children.Add(salir);
        }
        return g;
    }

    FrameworkElement FilaWifi(Red.Wifi w)
    {
        var pila = new StackPanel();
        // El botón centra su contenido, así que la fila no se estira sola: se le da el
        // ancho de la lista (en una isla ancha el panel mide más que AnchoRed)
        var g = new Grid { Width = AnchoFilaWifi(), Tag = FilaWifiTag };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

        var icono = Glifo(Glifowifi(w.Senal));
        icono.HorizontalAlignment = HorizontalAlignment.Left;
        g.Children.Add(icono);

        var nombre = new TextBlock
        {
            Text = w.Nombre, Foreground = _texto, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        // Las que ya conoces van primero y además en negrita: con la lista llena de
        // redes de vecinos, la tuya se encuentra de un vistazo
        if (w.Perfil.Length > 0)
        {
            nombre.FontWeight = FontWeights.SemiBold;
            nombre.ToolTip = "Ya te has conectado antes";
        }
        Grid.SetColumn(nombre, 1);
        g.Children.Add(nombre);

        if (w.Tipo != Red.Seguridad.Abierta)
        {
            var candado = Glifo("");
            candado.FontSize = 11;
            candado.Opacity = 0.6;
            Grid.SetColumn(candado, 2);
            g.Children.Add(candado);
        }

        var senal = new TextBlock
        {
            Text = $"{w.Senal}%", Foreground = _textoAtenuado, FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(senal, 3);
        g.Children.Add(senal);

        string tip = w.Tipo switch
        {
            Red.Seguridad.Empresa => "Red de empresa: se abre en los ajustes de Windows",
            Red.Seguridad.Otra => "Seguridad antigua (WEP): se abre en los ajustes de Windows",
            _ when Red.PideClave(w) => "Conectar (pide contraseña)",
            _ => "Conectar"
        };
        // La fila entera no crece al pasar por encima (se salía de la isla por los
        // lados): crece solo el icono del wifi, que tiene sitio en su columna
        var fila = Boton(g, tip, () => PulsarWifi(w), levantar: false);
        fila.MouseEnter += (_, _) => Levantar(icono, true);
        fila.MouseLeave += (_, _) => Levantar(icono, false);
        fila.Height = AltoFilaWifi;
        fila.Margin = new Thickness(-6, 0, 0, 0);
        fila.HorizontalAlignment = HorizontalAlignment.Left;
        pila.Children.Add(fila);

        if (_redConClave?.Nombre == w.Nombre) pila.Children.Add(CajaClave(w));
        return pila;
    }

    const string FilaWifiTag = "filawifi";

    // Lo que mide la lista menos el hueco de la barra de desplazamiento
    double AnchoFilaWifi() =>
        Math.Max(AnchoRed - 42, (_redLista?.ActualWidth ?? 0) - 22);

    void AjustarFilasWifi()
    {
        if (_redLista == null) return;
        foreach (var pila in _redLista.Children.OfType<StackPanel>())
            foreach (var b in pila.Children.OfType<Button>())
                if (b.Content is Grid { Tag: FilaWifiTag } g) g.Width = AnchoFilaWifi();
    }

    void PulsarWifi(Red.Wifi w)
    {
        if (_redConectando) return;
        if (!Red.SePuedeDesdeAqui(w)) { AbrirAjustesRed("ms-availablenetworks:"); return; }
        if (Red.PideClave(w))
        {
            // Abrir o cerrar la caja de esa fila; solo una a la vez
            _redConClave = _redConClave?.Nombre == w.Nombre ? null : w;
            Dispatcher.InvokeAsync(() => RellenarRed());
            return;
        }
        _ = ConectarWifi(w, null);
    }

    FrameworkElement CajaClave(Red.Wifi w)
    {
        var g = new Grid { Margin = new Thickness(26, 2, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var caja = new PasswordBox
        {
            Height = 28,
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficie,
            Foreground = _texto,
            CaretBrush = _acentoLegible,
            BorderThickness = new Thickness(0),
            ToolTip = $"Contraseña de {w.Nombre}"
        };
        g.Children.Add(caja);

        void Entrar()
        {
            if (caja.Password.Length < 8) { MostrarEstadoRed("La contraseña wifi tiene al menos 8 caracteres"); return; }
            _ = ConectarWifi(w, caja.Password);
        }
        caja.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Entrar(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _redConClave = null; Dispatcher.InvokeAsync(() => RellenarRed()); e.Handled = true; }
        };

        var boton = Boton(new TextBlock { Text = "Conectar", Foreground = _acentoLegible }, $"Conectar a {w.Nombre}", Entrar);
        boton.Height = 28;
        boton.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(boton, 1);
        g.Children.Add(boton);

        // Para escribir hace falta el teclado: la isla deja de ser "no activable" mientras
        // el panel siga abierto, como el buscador del menú Inicio. Al cerrarlo, se devuelve.
        caja.Loaded += (_, _) =>
        {
            if (!_panelConTeclado) ActivarTeclado(true);
            caja.Focus();
            Keyboard.Focus(caja);
        };
        return g;
    }

    void MostrarEstadoRed(string texto)
    {
        if (_redEstado == null) return;
        _redEstado.Text = texto;
        _redEstado.Visibility = texto.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ReajustarPanel(PanelRed);
    }

    async Task ConectarWifi(Red.Wifi w, string? clave)
    {
        _redConectando = true;
        MostrarEstadoRed($"Conectando a {w.Nombre}…");
        string? fallo;
        try { fallo = await Red.Conectar(w, clave); }
        catch (Exception ex) { Log.Error("conectar wifi", ex); fallo = "No se pudo conectar"; }
        _redConectando = false;

        if (_panelAbierto != PanelRed) return;
        if (fallo == null) _redConClave = null;
        MostrarEstadoRed(fallo == null ? $"Conectado a {w.Nombre}" : fallo);
        await RellenarRed();
    }

    // ---------- Test de velocidad ----------
    FrameworkElement CrearVelocidad()
    {
        var pila = new StackPanel();
        var cifras = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        for (int i = 0; i < 3; i++) cifras.ColumnDefinitions.Add(new ColumnDefinition());

        TextBlock Columna(int col, string titulo, string glifo)
        {
            var c = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
            var cab = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0.6 };
            var g = Glifo(glifo);
            g.FontSize = 10;
            g.Margin = new Thickness(0, 0, 4, 0);
            cab.Children.Add(g);
            cab.Children.Add(new TextBlock { Text = titulo, Foreground = _texto, FontSize = 11 });
            c.Children.Add(cab);
            var valor = new TextBlock { Text = "—", Foreground = _texto, FontSize = 20, FontWeight = FontWeights.SemiBold };
            Typography.SetNumeralAlignment(valor, FontNumeralAlignment.Tabular);
            c.Children.Add(valor);
            Grid.SetColumn(c, col);
            cifras.Children.Add(c);
            return valor;
        }

        _velPing = Columna(0, "Ping", "");
        _velBajada = Columna(1, "Bajada", "");
        _velSubida = Columna(2, "Subida", "");
        pila.Children.Add(cifras);

        var abajo = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        abajo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        abajo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _velNota = Tenue("");
        _velNota.VerticalAlignment = VerticalAlignment.Center;
        abajo.Children.Add(_velNota);
        _velBoton = Boton(new TextBlock { Text = "Medir", Foreground = _acentoLegible }, "Hacer un test de velocidad (speed.cloudflare.com)",
                          () => { _ = Velocidad.Medir(); PintarVelocidad(); });
        _velBoton.Height = 26;
        Grid.SetColumn(_velBoton, 1);
        abajo.Children.Add(_velBoton);
        pila.Children.Add(abajo);

        PintarVelocidad();
        return pila;
    }

    void VelocidadCambio() => Dispatcher.BeginInvoke(new Action(PintarVelocidad), DispatcherPriority.Background);

    void PintarVelocidad()
    {
        if (_velPing == null || _velBajada == null || _velSubida == null || _velNota == null || _velBoton == null) return;

        static string Mb(double v) => v < 0 ? "—" : v >= 100 ? $"{v:0}" : $"{v:0.0}";
        bool enCurso = Velocidad.EnCurso;
        var ultimo = Velocidad.Ultimo;

        // Con el test parado se enseña el último resultado; durante el test, las cifras vivas
        double ping = enCurso || ultimo == null ? Velocidad.PingMs : ultimo.PingMs;
        double bajada = enCurso || ultimo == null ? Velocidad.BajadaMbps : ultimo.BajadaMbps;
        double subida = enCurso || ultimo == null ? Velocidad.SubidaMbps : ultimo.SubidaMbps;
        _velPing.Text = ping < 0 ? "—" : $"{ping:0} ms";
        _velBajada.Text = bajada < 0 ? "—" : $"{Mb(bajada)} Mb/s";
        _velSubida.Text = subida < 0 ? "—" : $"{Mb(subida)} Mb/s";

        // La cifra que se está midiendo, en color de acento
        _velPing.Foreground = Velocidad.FaseActual == Velocidad.Fase.Ping ? _acentoLegible : _texto;
        _velBajada.Foreground = Velocidad.FaseActual == Velocidad.Fase.Bajada ? _acentoLegible : _texto;
        _velSubida.Foreground = Velocidad.FaseActual == Velocidad.Fase.Subida ? _acentoLegible : _texto;

        _velBoton.IsEnabled = !enCurso;
        _velBoton.Opacity = enCurso ? 0.4 : 1;
        _velNota.Text = Velocidad.FaseActual switch
        {
            Velocidad.Fase.Ping => "Midiendo el ping…",
            Velocidad.Fase.Bajada => "Midiendo la bajada…",
            Velocidad.Fase.Subida => "Midiendo la subida…",
            _ when ultimo?.Error != null => ultimo.Error,
            _ when ultimo != null => $"Medido a las {ultimo.Cuando:HH:mm} · Cloudflare",
            _ => "Tarda unos 15 s y gasta datos"
        };
    }
}
