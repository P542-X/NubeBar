using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.Generic;

namespace NubeBar;

// El panel de red: lo que tienes conectado (con su IP y su velocidad de enlace), las
// wifis que hay a la vista para entrar en ellas desde la isla, y un test de velocidad.
public partial class MainWindow : Window
{
    internal const string PanelRed = "red";
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

    // ---------- El estado del panel, en un solo sitio ----------
    //
    // La decisión vive en `RedEstado` (lógica pura, con sus pruebas) y aquí solo se pinta.
    // Antes cada trozo del panel miraba los datos crudos por su cuenta y sacaba su propia
    // conclusión, y de ahí salían los botones que aparecían y desaparecían.
    readonly RedEstado.Memoria _redMemoria = new();
    readonly RedEstado.ListaEstable<Red.Wifi> _redVisibles = new();

    // La cabecera, que ahora se ACTUALIZA. Era el caso más fácil de ver: se construía con
    // `Red.Estado()` al abrir el panel y no se volvía a tocar, así que con el panel abierto
    // y el cable fuera seguía diciendo «Conectado» hasta cerrarlo y volver a abrirlo.
    TextBlock? _redCabeceraTitulo;
    Border? _redCabeceraInsignia;
    Ellipse? _redCabeceraPunto;
    TextBlock? _redCabeceraExplica;
    StackPanel? _redSeccionWifi;

    // Las cinco acciones, creadas una vez y guardadas por su nombre. Ninguna se quita
    // nunca del panel: lo que cambia es `IsEnabled` y la ayuda emergente.
    readonly Dictionary<RedEstado.Accion, Button> _redBotones = new();

    bool _redMidiendo;
    FrameworkElement? _redBotonBuscar;

    // ---------- Buscar redes, por una sola puerta ----------
    //
    // Lo hacían dos sitios con el mismo código copiado: el botón de la cabecera y la acción
    // «Actualizar» de la rejilla. Dos caminos para lo mismo es como uno se queda con las
    // reglas viejas el día que el otro cambie — y además el giro habría que acordarse de
    // ponerlo en los dos.
    //
    // ---------- Y el giro ----------
    //
    // La maqueta: «el botón de volver a buscar GIRA mientras busca». No es decoración: wlanapi
    // tarda unos cuatro segundos en tener la lista nueva, y cuatro segundos sin que pase nada
    // es tiempo de sobra para pensar que el botón no ha funcionado y volver a pulsarlo.
    void EscanearRedes()
    {
        Red.Escanear();
        if (_redEstado != null)
        {
            _redEstado.Text = "Buscando redes…";
            _redEstado.Visibility = Visibility.Visible;
        }

        RotateTransform? giro = null;
        if (_redBotonBuscar is { } b && Animacion.Adorno)
        {
            b.RenderTransformOrigin = new Point(0.5, 0.5);
            giro = new RotateTransform();
            b.RenderTransform = giro;
            var a = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            a.Freeze();
            giro.BeginAnimation(RotateTransform.AngleProperty, a);
        }

        // wlanapi necesita unos 4 s para tener la lista nueva tras un escaneo
        Dispatcher.InvokeAsync(async () =>
        {
            try { await Task.Delay(4000); await RellenarRed(); }
            finally
            {
                // El giro se para pase lo que pase: un botón girando para siempre porque el
                // escaneo falló diría que sigue buscando cuando ya no busca nadie.
                giro?.BeginAnimation(RotateTransform.AngleProperty, null);
                if (_redBotonBuscar != null) _redBotonBuscar.RenderTransform = Transform.Identity;
            }
        });
    }
    Red.Wifi? _redElegida;

    // Cifras del test de velocidad del panel abierto
    TextBlock? _velPing, _velBajada, _velSubida, _velNota;
    Button? _velBoton;

    void AbrirRed()
    {
        _redConClave = null;
        AlternarPanel(PanelRed, CrearRed, AnchoRed, 200, ancla: _anclaRed);
    }

    FrameworkElement CrearRed()
    {
        var columna = new StackPanel { Margin = new Thickness(12, 4, 12, 8) };

        // ---------- Qué cambia aquí, y qué NO ----------
        //
        // Cambia el envoltorio: lo que antes eran rótulos y filas sueltas sobre el fondo
        // ahora son tarjetas con superficie opaca. **Los contenedores son los mismos**
        // —`_redConectadas`, `_redLista`, `_redScroll`, los cuatro TextBlock de
        // velocidad—, así que `RellenarRed`, `AjustarFilasWifi` y el refresco parcial
        // siguen funcionando sin tocar una línea: vacían y rellenan SU pila, no el panel.
        //
        // Y la lógica de `Red.cs` no se toca en absoluto.

        // ---------- Conexión actual ----------
        //
        // La insignia y la explicación se guardan en campos y se ACTUALIZAN en cada
        // lectura. Antes se construían aquí con `Red.Estado()` y se quedaban congeladas:
        // con el panel abierto y el cable fuera seguían diciendo «Conectado».
        _redMemoria.Olvidar();
        _redVisibles.Olvidar();
        _redElegida = null;
        _redBotones.Clear();

        var colCon = new StackPanel();
        colCon.Children.Add(SectionHeader(0xEB55, "Red e Internet", CabeceraDeRed()));

        _redCabeceraExplica = Tenue(RedEstado.Explicacion(RedEstado.Que.Cargando));
        _redCabeceraExplica.TextWrapping = TextWrapping.Wrap;
        _redCabeceraExplica.Margin = new Thickness(0, 4, 0, 0);
        colCon.Children.Add(_redCabeceraExplica);

        _redConectadas = new StackPanel();
        colCon.Children.Add(_redConectadas);
        columna.Children.Add(Card(colCon));

        // ---------- Redes wifi ----------
        //
        // La sección entera se guarda en un campo: hay estados —sin tarjeta wifi, sin
        // adaptadores— en los que no tiene sentido enseñarla, y entonces se colapsa de una
        // pieza en vez de quedarse como una cabecera con un hueco debajo.
        var colWifi = _redSeccionWifi = new StackPanel();
        var buscar = _redBotonBuscar = BotonIcono(0xE72C, "Volver a buscar redes", () => EscanearRedes());
        _redTitulo = Titulito("Redes wifi");
        colWifi.Children.Add(SectionHeader(0xE701, "Redes Wi-Fi", buscar));

        _redEstado = Tenue("");
        _redEstado.Visibility = Visibility.Collapsed;
        colWifi.Children.Add(_redEstado);

        _redLista = new StackPanel();
        _redLista.SizeChanged += (_, e) => { if (e.WidthChanged) AjustarFilasWifi(); };
        _redScroll = new ScrollViewer
        {
            MaxHeight = AltoListaWifi(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _redLista,
            // Los 6 px que antes llevaba cada fila. Aquí el que se corre es el contenedor,
            // así que las filas empiezan en su filo y conservan sus esquinas (ver FilaWifi).
            Margin = new Thickness(-SangradoDeLaLista, 0, 0, 0),
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
        colWifi.Children.Add(_redScroll);
        columna.Children.Add(Card(colWifi));

        // ---------- Velocidad, como tarjeta de estadísticas ----------
        var colVel = new StackPanel();
        colVel.Children.Add(SectionHeader(0xE9D9, "Velocidad"));
        colVel.Children.Add(CrearVelocidad());
        columna.Children.Add(Card(colVel));

        // ---------- Acciones de abajo ----------
        //
        // Las CINCO, siempre, en el mismo orden, en los siete estados. Lo que cambia es si
        // se pueden pulsar, y la que no se puede dice por qué al pasar el ratón.
        //
        // Antes había dos, y las demás vivían dentro de las filas de conexión y de wifi —
        // así que desaparecían con su fila. El usuario lo veía como «los botones a veces
        // están y a veces no», y tenía razón: un botón dentro de una lista que se vacía es
        // un botón que se va.
        columna.Children.Add(Card(BarraDeAcciones()));

        Velocidad.Cambio += VelocidadCambio;
        columna.Unloaded += (_, _) => Velocidad.Cambio -= VelocidadCambio;

        ApuntarPanel(PanelRed, columna, AnchoRed, TopeDeAlto);

        // El panel se abre ya y se rellena por detrás
        Dispatcher.InvokeAsync(RellenarYEscanear, DispatcherPriority.Background);
        return PanelShell(columna, 0);
    }

    // ================= La cabecera, que se actualiza =================
    //
    // Se construye una vez con los dos trozos guardados en campos —el punto de color y el
    // texto—, y después solo se les cambia el contenido. Rehacerla entera en cada lectura
    // la haría parpadear, y además movería de sitio lo que hay debajo.
    UIElement CabeceraDeRed()
    {
        _redCabeceraPunto = new Ellipse
        {
            Width = 7, Height = 7,
            Fill = PincelSolido(_roles.TextoAtenuado),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        _redCabeceraTitulo = new TextBlock
        {
            Text = RedEstado.Titulo(RedEstado.Que.Cargando),
            FontSize = 10.5, Foreground = _textoAtenuado,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var fila = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fila.Children.Add(_redCabeceraPunto);
        fila.Children.Add(_redCabeceraTitulo);
        return _redCabeceraInsignia = new Border
        {
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 9, 3),
            Child = fila,
        };
    }

    // ================= Las cinco acciones =================
    UIElement BarraDeAcciones()
    {
        var rejilla = new UniformGrid { Columns = 3 };
        foreach (var a in RedEstado.Todas)
        {
            var accion = a;
            var b = QuickAction(IconoDeAccionDeRed(accion), RedEstado.Nombre(accion), false,
                                () => HacerAccionDeRed(accion));
            _redBotones[accion] = b;
            rejilla.Children.Add(b);
        }
        // Con cinco en una rejilla de tres, el hueco de la sexta celda queda vacío y la
        // última fila se descuadra. Un relleno invisible la cuadra sin inventarse un botón.
        rejilla.Children.Add(new Border { Visibility = Visibility.Hidden });
        AplicarEstadoALosBotones();
        return rejilla;
    }

    static int IconoDeAccionDeRed(RedEstado.Accion a) => a switch
    {
        RedEstado.Accion.Actualizar => 0xE72C,
        RedEstado.Accion.Conectar => 0xE703,
        RedEstado.Accion.Desconectar => 0xE711,
        RedEstado.Accion.MedirVelocidad => 0xE9D9,
        _ => 0xE713,
    };

    void HacerAccionDeRed(RedEstado.Accion a)
    {
        switch (a)
        {
            case RedEstado.Accion.Actualizar:
                EscanearRedes();
                break;

            case RedEstado.Accion.Conectar:
                // Por la MISMA puerta que pulsar la fila, no por una copia suya: si fueran
                // dos caminos, el día que uno cambie el otro se queda con las reglas viejas.
                if (_redElegida is { } w) PulsarWifi(w);
                break;

            case RedEstado.Accion.Desconectar:
                foreach (var c in _redVisibles.Ultima.Where(x => x.Conectada)) Red.Desconectar(c);
                Dispatcher.InvokeAsync(async () => { await Task.Delay(1200); await RellenarRed(); });
                break;

            case RedEstado.Accion.MedirVelocidad:
                _redMidiendo = true;
                AplicarEstadoALosBotones();
                _ = Velocidad.Medir();
                PintarVelocidad();
                break;

            default:
                AbrirAjustesRed("ms-settings:network");
                break;
        }
    }

    // Pintar el estado en los botones: habilitado o no, y por qué no.
    //
    // Es LO ÚNICO que decide si un botón se puede pulsar, y por eso no hay forma de que un
    // trozo del panel llegue a otra conclusión por su cuenta.
    void AplicarEstadoALosBotones()
    {
        var q = _redMemoria.Ahora;
        foreach (var (accion, boton) in _redBotones)
        {
            string? no = RedEstado.PorQueNo(accion, q, _redElegida != null, _redMidiendo);
            boton.IsEnabled = no == null;
            boton.Opacity = no == null ? 1.0 : 0.45;
            boton.ToolTip = no ?? RedEstado.Nombre(accion);
            // Y para quien no usa el ratón: el motivo tiene que llegar igual.
            System.Windows.Automation.AutomationProperties.SetHelpText(boton, no ?? "");
            System.Windows.Automation.AutomationProperties.SetName(
                boton, no == null ? RedEstado.Nombre(accion)
                                  : $"{RedEstado.Nombre(accion)} (no disponible: {no})");
        }
    }

    // Y en la cabecera.
    void AplicarEstadoALaCabecera()
    {
        var q = _redMemoria.Ahora;
        if (_redCabeceraTitulo != null) _redCabeceraTitulo.Text = RedEstado.Titulo(q);
        if (_redCabeceraPunto != null)
            _redCabeceraPunto.Fill = PincelSolido(q switch
            {
                RedEstado.Que.Conectado => _roles.Positivo,
                RedEstado.Que.Cargando => _roles.TextoAtenuado,
                _ => _roles.Aviso,
            });
        if (_redCabeceraExplica != null)
        {
            string texto = RedEstado.Explicacion(q);
            _redCabeceraExplica.Text = texto;
            _redCabeceraExplica.Visibility = texto.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        if (_redSeccionWifi?.Parent is UIElement tarjeta)
            tarjeta.Visibility = RedEstado.HayQueEnseñarLasWifis(q)
                               ? Visibility.Visible : Visibility.Collapsed;
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
            // ---------- El reajuste tiene que pasar SIEMPRE ----------
            //
            // Y esta es la segunda mitad del recorte del panel de red.
            //
            // `ReajustarPanel` estaba al final del método, detrás de tres `return`
            // tempranos: «no ha cambiado la firma», «hay una contraseña a medias», «se está
            // conectando». En los repasos automáticos —que es cuando la lista YA está
            // llena— se salía por el primero y el reajuste no llegaba a ejecutarse nunca.
            //
            // Así que la única vez que el panel se remedía era la primera, a los 90 ms de
            // abrirse, con las wifis todavía llegando: medía un panel a medio llenar, se
            // quedaba con ese alto, y lo que entraba después caía por debajo del borde.
            //
            // En un `finally` no hay camino que se lo salte. Y no es «reconstruir el panel
            // en cada actualización»: no toca ni un control, solo comprueba si la isla
            // tiene que medir otra cosa, y si no, se va sin hacer nada.
            var conexiones = await Task.Run(Red.Conexiones);
            var wifis = await Task.Run(Red.Wifis);
            var comoFue = Red.UltimaLectura;        // justo después de leer, antes de nada más
            if (_panelAbierto != PanelRed) return;

            int adaptadores = await Task.Run(Red.CuantosAdaptadores);

            // ---------- El estado, por la máquina y con histéresis ----------
            //
            // Aquí es donde una lectura vacía deja de poder borrar el panel: `Encaja` se
            // queda con el estado bueno hasta que DOS lecturas seguidas digan lo mismo.
            _redMemoria.Encaja(new RedEstado.Lectura(conexiones.Count, wifis.Count,
                                                     adaptadores, comoFue));
            AplicarEstadoALaCabecera();
            AplicarEstadoALosBotones();

            string firma = string.Join("|", wifis.Select(w => $"{w.Nombre}:{w.Conectada}:{w.Senal / 10}"));
            if (soloSiCambia && firma == _firmaRedes) return;
            _firmaRedes = firma;

            // ---------- Conectado ----------
            //
            // Y aquí lo mismo un escalón más abajo: si la lectura viene vacía pero el
            // estado sigue siendo «Conectado», NO se vacía la tarjeta. Vaciarla era lo
            // que se llevaba por delante los botones que vivían dentro de la fila.
            if (conexiones.Count > 0)
            {
                _redConectadas.Children.Clear();
                foreach (var c in conexiones)
                    _redConectadas.Children.Add(FilaConexion(c, wifis.FirstOrDefault(w => w.Conectada)));
            }
            else if (_redMemoria.Ahora != RedEstado.Que.Conectado)
            {
                // Ya no es un parpadeo: la máquina lo ha confirmado. Un hueco con nombre,
                // no un panel en blanco.
                _redConectadas.Children.Clear();
                _redConectadas.Children.Add(Tenue(RedEstado.Titulo(_redMemoria.Ahora)));
            }
            // else: lectura vacía con el estado todavía en Conectado. Se deja lo de antes.

            // ---------- A la vista ----------
            //
            // El orden que da Windows es el suyo y tu red se perdía entre las de los
            // vecinos: primero las que ya conoces (tienen perfil guardado) y dentro de cada
            // grupo, la que mejor llega.
            var recien = wifis.Where(w => !w.Conectada && w.Senal > 0)
                              .OrderByDescending(w => w.Perfil.Length > 0)
                              .ThenByDescending(w => w.Senal)
                              .ToList();

            // Escanear vacía la caché de Windows un instante, y sin esto la lista entera
            // desaparecía y volvía medio segundo después: el salto por el que pulsabas en
            // una red y acababas en otra.
            var visibles = _redVisibles.Encaja(recien);

            _redLista.Children.Clear();
            if (_redTitulo != null)
                _redTitulo.Text = visibles.Count > FilasWifiALaVista
                    ? $"Redes wifi · {visibles.Count}"      // que se vea que hay más abajo
                    : "Redes wifi";
            if (visibles.Count == 0)
                _redLista.Children.Add(Tenue(MensajeDeListaVacia()));
            foreach (var w in visibles) _redLista.Children.Add(FilaWifi(w));

            if (_redEstado != null && !_redConectando)
            {
                // Mientras se enseña la lista de antes se dice, en vez de dejar creer que
                // lo que hay en pantalla es de ahora mismo.
                if (_redVisibles.EnsenandoLoDeAntes)
                {
                    _redEstado.Text = "Buscando redes… (se enseñan las de antes)";
                    _redEstado.Visibility = Visibility.Visible;
                }
                else if (_redEstado.Text.StartsWith("Buscando redes…", StringComparison.Ordinal))
                    _redEstado.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            // Un fallo al leer es un ESTADO, no un silencio. Antes se registraba y el panel
            // se quedaba con lo que hubiera, sin decir nada.
            Log.Error("rellenar el panel de red", ex);
            _redMemoria.Encaja(RedEstado.Que.Error);
            AplicarEstadoALaCabecera();
            AplicarEstadoALosBotones();
        }
        finally
        {
            // Pase lo que pase y se haya salido por donde se haya salido.
            ReajustarPanel(PanelRed);
        }
    }

    // Qué poner donde iría la lista de wifis cuando no hay ninguna. Sale del estado, que
    // es lo que distingue «no se ve ninguna» de «no se han podido pedir».
    string MensajeDeListaVacia() => _redMemoria.Ahora switch
    {
        RedEstado.Que.Cargando => "Buscando redes…",
        RedEstado.Que.SinPermisos => RedEstado.Explicacion(RedEstado.Que.SinPermisos),
        RedEstado.Que.SinTarjetaWifi => "Este equipo no tiene Wi-Fi, o está apagada",
        RedEstado.Que.Error => "No se han podido pedir las redes. Prueba a actualizar.",
        _ => "No hay más redes a la vista",
    };

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
        // El ancho va a mano, y no por gusto: la plantilla del botón CENTRA su contenido y
        // no respeta `HorizontalContentAlignment`, así que una rejilla estirada se queda del
        // tamaño de su contenido y sale centrada en la tarjeta. Se probó.
        //
        // Lo que sí estaba mal era el número. Era `max(AnchoRed - 42, lista - 22)`, donde 42
        // es una estimación del cromo que el panel se come por los lados; el cromo de verdad
        // son 52 (filo 2 + margen de la columna 24 + relleno de la tarjeta 26). La fila
        // salía de 378 dentro de un hueco de 368 y diez píxeles de candado y de porcentaje
        // quedaban cortados contra el borde derecho.
        //
        // Y el `Math.Max` remataba: cuando la lista ya sabía su ancho real —que es el dato
        // bueno— se quedaba igualmente con la estimación por ser el número mayor.
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
        // ---------- Una fila de la lista no se levanta: se ilumina ----------
        //
        // `levantar: false` ya estaba, y por su motivo: la fila entera creciendo un 8 % son
        // quince píxeles por lado y se salía de la isla.
        //
        // Lo que se hizo entonces fue levantar SOLO el icono del wifi, que tenía sitio en su
        // columna. Y eso es lo que había que quitar ahora: en una LISTA, el elemento bajo el
        // cursor se señala iluminando su fila entera, no dando un brinco a un trozo de ella.
        // El icono saltando a destiempo dentro de una fila quieta se lee como un fallo de
        // dibujo, y además mueve el único punto de la fila que el ojo usa para seguir dónde
        // está mientras recorre la lista.
        //
        // No hace falta poner nada a cambio: la plantilla de `Boton` ya pinta el fondo de
        // `SuperficieAlta` bajo el ratón (ver `MainWindow.Botones.cs`, elemento «bg»), que es
        // exactamente el gris que señala la fila. Lo decía el propio comentario de `Levantar`:
        // «el fondo del hover ya avisa de que estás encima».
        var fila = Boton(g, tip, () => PulsarWifi(w), levantar: false);
        fila.Height = AltoFilaWifi;
        // ---------- Y el desplazamiento de 6 px NO va aquí ----------
        //
        // Estaba puesto en la fila (`Margin = -6,0,0,0`) para que el fondo del ratón
        // llegara al filo de la tarjeta en vez de empezar sangrado. Hacía su trabajo, y de
        // paso uno que no se pretendía: sacaba la fila 6 px por fuera del `ScrollViewer`
        // de la lista, que RECORTA. Lo que se quedaba fuera del recorte eran justo las
        // esquinas redondeadas del lado izquierdo del fondo —radio 8, ver la plantilla de
        // `Boton`—, así que la fila salía redondeada por la derecha y cuadrada por la
        // izquierda. El usuario lo vio: «en la selección de redes wifi los bordes derechos
        // están redondeados pero los izquierdos no».
        //
        // El desplazamiento se mueve a la LISTA entera (ver `_redScroll`): el conjunto se
        // corre los mismos 6 px, cada fila empieza exactamente en el filo de su viewport y
        // ya no hay nada que recortar. Se ve igual que antes y con las cuatro esquinas.
        //
        // La regla, por si vuelve a aparecer: lo que asoma por fuera de un contenedor que
        // recorta no se dibuja «un poco peor», se dibuja CORTADO — y lo primero que se
        // pierde de una caja redondeada son sus esquinas.
        // Estirada, y con su contenido estirado también: `Boton` centra lo que le pones, y
        // era por eso por lo que la fila necesitaba un ancho a mano. Con las dos cosas
        // estiradas, la rejilla recibe el ancho que haya y cada columna cae donde debe.
        fila.HorizontalAlignment = HorizontalAlignment.Left;
        pila.Children.Add(fila);

        if (_redConClave?.Nombre == w.Nombre) pila.Children.Add(CajaClave(w));
        return pila;
    }

    const string FilaWifiTag = "filawifi";

    // Lo que la lista de redes se corre hacia la izquierda para que el fondo del ratón
    // llegue al filo de la tarjeta. Vivía en cada fila y recortaba sus esquinas.
    const double SangradoDeLaLista = 6;

    // ---------- Lo que mide una fila de wifi ----------
    //
    // El dato bueno es el ancho REAL de la lista: ya descuenta el filo del panel, el margen
    // de la columna y el relleno de la tarjeta, sin que nadie tenga que sumarlos a mano. Se
    // usa en cuanto existe.
    //
    // La estimación solo vale para la primera vez, antes de que la lista se haya colocado,
    // y ahora descuenta el cromo de verdad: filo 2 + margen de columna 24 + relleno de
    // tarjeta 26 = 52, más los 14 del hueco de la barra de desplazamiento.
    //
    // Y se coge el ancho real cuando lo hay, en vez del mayor de los dos: quedarse con el
    // número más grande «por si acaso» es exactamente lo que cortaba las filas.
    const double CromoDelPanelDeRed = 52;
    const double HuecoDeLaBarraDeDesplazamiento = 14;

    double AnchoFilaWifi()
    {
        double real = _redLista?.ActualWidth ?? 0;
        return real > 1
            ? Math.Max(80, real - HuecoDeLaBarraDeDesplazamiento)
            : Math.Max(80, AnchoRed - CromoDelPanelDeRed - HuecoDeLaBarraDeDesplazamiento);
    }

    // Cuando la lista cambia de ancho —la isla se expande o se recoge, cambia el DPI— las
    // filas ya creadas tienen que seguirla. Es lo que llama el `SizeChanged` de la lista.
    void AjustarFilasWifi()
    {
        if (_redLista == null) return;
        double ancho = AnchoFilaWifi();
        foreach (var pila in _redLista.Children.OfType<StackPanel>())
            foreach (var b in pila.Children.OfType<Button>())
                if (b.Content is Grid { Tag: FilaWifiTag } g) g.Width = ancho;
    }

    void PulsarWifi(Red.Wifi w)
    {
        // Pulsar una fila también la ELIGE, y eso es lo que habilita el botón «Conectar»
        // de la barra de acciones. Sin esto, el botón estaría siempre deshabilitado y
        // diciendo «elige antes una red», que es peor que no estar.
        _redElegida = w;
        AplicarEstadoALosBotones();

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
        // "Medir" con aspecto de botón secundario, no de texto azul suelto: usa la misma
        // plantilla que el resto de los botones de los paneles.
        _velBoton = new Button
        {
            Content = new TextBlock
            {
                Text = "Medir", FontSize = 11.5, Foreground = _texto,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Template = PlantillaDeAccion(false),
            MinHeight = AltoTocable,
            Padding = new Thickness(14, 4, 14, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Hacer un test de velocidad (speed.cloudflare.com)",
        };
        System.Windows.Automation.AutomationProperties.SetName(_velBoton, "Medir la velocidad");
        _velBoton.Click += (_, _) => { _ = Velocidad.Medir(); PintarVelocidad(); };
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

        // «Medir» de la barra de acciones se deshabilita mientras se mide y se vuelve a
        // habilitar al acabar. La verdad es `Velocidad.EnCurso`, no una bandera nuestra:
        // el test también se puede lanzar desde el botón de la tarjeta.
        if (_redMidiendo != enCurso) { _redMidiendo = enCurso; AplicarEstadoALosBotones(); }

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
