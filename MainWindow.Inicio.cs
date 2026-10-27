using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// Menú Inicio propio: una caja de búsqueda y una lista de resultados que salen
// de varias fuentes a la vez (apps, ventanas abiertas, ajustes, archivos, cálculos,
// comandos y web). Lo rápido se calcula al teclear; los archivos van por detrás.
public partial class MainWindow
{
    internal const string PanelInicio = "inicio";
    const double AnchoMenu = 660, AltoFila = 44, FilasVisibles = 9;
    // El mismo número, visible para las pruebas: el ancho de la superficie de Inicio.
    internal const double AnchoDeInicio = AnchoMenu;
    // Seis y se acabó. Veinte filas era una lista para recorrer con la rueda, y un
    // buscador no es para recorrerlo: o lo que quieres está arriba del todo o la
    // puntuación está mal. Con seis, lo que sale tiene que ser lo bueno.
    const int MaxFilas = 6;

    TextBox? _caja;
    StackPanel? _lista;
    Border? _marcaSeleccion;
    ScrollViewer? _scroll;
    TextBlock? _pista;

    List<Resultado> _resultados = new();
    int _seleccion;
    string _ultimaConsulta = "";
    Point _ratonAlPintar = new(double.NaN, double.NaN);
    Categoria? _filtro;                 // Tab va rotando entre las categorías que hay
    CancellationTokenSource? _buscaArchivos;
    readonly Relevo _relevo = new();      // descarta los archivos que llegan tarde
    DispatcherTimer? _esperaArchivos;

    // Con la portada (lo que suena, accesos, gráficas, wallpaper, fijadas y energía)
    // hace falta bastante más alto que con la lista de resultados a secas
    double AltoCabecera => _cfg.Inicio.Saludo ? 58 : 0;
    double AltoMenu => AltoCabecera + 8 + 38 + 8 + AltoFila * FilasVisibles + 10;

    // ---------- Y lo que de verdad cabe (C1) ----------
    //
    // En horizontal el menú se despliega desde la isla hacia el centro de la pantalla y
    // tiene todo el ancho del monitor para él: 660 px siempre caben. Al lado de una
    // barra VERTICAL no: hay que descontar la franja de la barra, su margen y la
    // separación, y en una pantalla estrecha —o con un grosor grande— 660 ya no entran.
    // Una tarjeta más ancha que ese hueco no se recorta: se queda fuera de la ventana y
    // simplemente no se ve.
    //
    // Estas dos propiedades son lo único que cambia de medidas entre las dos
    // orientaciones; el panel de dentro es el mismo y no se entera.
    double AnchoMenuEfectivo => !Vertical ? AnchoMenu
        : Math.Min(AnchoMenu, AnchoMaximoDeTarjeta(_monitor.Ancho / _monitor.Escala,
                                                   Grosor, _cfg.Barra.Margen, SeparacionPanel));

    double AltoMenuEfectivo => !Vertical ? AltoMenu
        : Math.Min(AltoMenu, AltoMaximoDeTarjeta(_monitor.Alto / _monitor.Escala, _cfg.Barra.Margen));

    // ---------- El tope de alto ----------
    //
    // `AltoMenu` es lo que mide la LISTA de resultados, y es un mínimo: `AbrirPanel` le
    // da más si el contenido lo pide. Con la portada —multimedia, acciones rápidas,
    // CPU/RAM, fondo, conectividad, fijadas, energía y sistema— el contenido pide mucho
    // más, y sin tope el panel se comería la pantalla: con el centro de control pasó
    // exactamente eso, medido ("pedía 560 de alto pero necesita 1076; se le da el que
    // necesita"). El tope es el criterio del resto de los paneles: nunca más del 62 % del
    // monitor, y la portada se desplaza por dentro con su propio scroll.
    internal static double TopeDeInicio(double altoPantalla) =>
        Math.Max(360, Math.Min(960, altoPantalla * 0.62));

    double AltoMaximoDeInicio() => Vertical
        ? Math.Min(TopeDeInicio(_monitor.Alto / _monitor.Escala),
                   AltoMaximoDeTarjeta(_monitor.Alto / _monitor.Escala, _cfg.Barra.Margen))
        : TopeDeInicio(_monitor.Alto / _monitor.Escala);

    // ================= Tecla Windows =================
    // Hay un solo hook de teclado para todas las islas: lo monta la jefa y lo
    // comparten todas (viven en el mismo hilo de interfaz, así que no hay carrera)
    static GanchoTeclas? _gancho;

    void MontarTeclaWindows()
    {
        if (_gancho == null)
        {
            _gancho = new GanchoTeclas();
            // El hook avisa desde SU hilo: hay que saltar al de la interfaz.
            // El menú sale en la isla del monitor donde tengas el ratón.
            _gancho.AbrirMenu += () => Dispatcher.BeginInvoke(new Action(() => IslaDelRaton().MenuDesdeTecla()));
            _gancho.TeclaVolumen += paso => Dispatcher.BeginInvoke(new Action(() => TeclaVolumen(paso)));
            _gancho.Instalar();
        }
        _gancho.Activo = _cfg.Inicio.TeclaWindows;
        _gancho.Volumen = _cfg.Barra.TeclasVolumen;
    }

    // Lo mismo que haría Windows con la tecla (de 2 en 2, y subir quita el silencio),
    // pero sin su aviso: el de la isla lo saca VigilarVolumen al ver el cambio
    static void TeclaVolumen(int paso)
    {
        try
        {
            if (paso == 0) { Audio.Silenciar(!Audio.Silenciado); return; }
            int v = Audio.Volumen();
            if (v < 0) return;
            Audio.PonerVolumen(v + paso * 2);
            if (paso > 0 && Audio.Silenciado) Audio.Silenciar(false);
        }
        catch (Exception ex) { Log.Error("tecla de volumen", ex); }
    }

    // Win sola (o Ctrl+Esc): si el menú está abierto se cierra, y si no se abre.
    // Con varias islas, el menú abierto en OTRA pantalla también cuenta como abierto:
    // pulsar Win se lo lleva, no abre un segundo menú.
    void MenuDesdeTecla()
    {
        // Escondida a mano: la tecla solo la trae de vuelta, no abre el menú encima
        if (_escondidaAMano)
        {
            _gancho!.Capturando = false;
            VaciarCola(descartar: true);
            EsconderAMano(false);
            return;
        }
        foreach (var otra in Islas.Where(i => i != this && i._panelAbierto == PanelInicio).ToList())
        {
            _gancho!.Capturando = false;
            otra.VaciarCola(descartar: true);
            otra.CerrarMenu();
            return;
        }
        if (_panelAbierto == PanelInicio)
        {
            _gancho!.Capturando = false;
            VaciarCola(descartar: true);
            CerrarMenu();
            return;
        }
        ApuntarBoton("tecla Windows");   // TEMPORAL: diagnóstico del flujo
        AbrirMenuInicio();
    }

    // Traduce lo que el hook guardó mientras la caja no tenía el foco y lo mete
    // en orden. Se vacía dos veces: entre la primera y soltar la captura todavía
    // puede entrar alguna tecla más.
    void VaciarCola(bool descartar = false)
    {
        if (_gancho == null) return;

        for (int vuelta = 0; vuelta < 2; vuelta++)
        {
            while (_gancho.Cola.TryDequeue(out var p))
            {
                if (descartar || _caja == null) continue;
                AplicarPulsacion(p);
            }
            if (vuelta == 0) _gancho.Capturando = false;
        }
    }

    void AplicarPulsacion(GanchoTeclas.Pulsacion p)
    {
        if (_caja == null) return;
        switch (p.Vk)
        {
            case 0x08:                                   // retroceso
                if (_caja.Text.Length > 0) _caja.Text = _caja.Text[..^1];
                break;
            case 0x1B:                                   // escape
                CerrarMenu();
                break;
            case 0x0D:                                   // intro
                EjecutarSeleccion(Visibles, p.Ctrl);
                break;
            case 0x26: case 0x28:                        // flechas arriba/abajo
                var lista = Visibles;
                if (lista.Count > 0)
                {
                    _seleccion = p.Vk == 0x28
                        ? (_seleccion + 1) % lista.Count
                        : (_seleccion - 1 + lista.Count) % lista.Count;
                    MoverMarca();
                }
                break;
            default:
                var texto = Tipografia.Traducir(p);
                if (texto.Length > 0) _caja.Text += texto;
                break;
        }
        _caja.CaretIndex = _caja.Text.Length;
    }

    // ================= Abrir y cerrar =================
    // El botón "⊞" de la zona, para que la tarjeta salga a su lado y no en el medio de
    // la pantalla. Si el módulo "inicio" no está puesto en ninguna zona, se queda en
    // null y la tarjeta se centra, que es la salida razonable.
    Button? _botonInicio;

    internal UIElement ModuloInicio() => _botonInicio =
        Boton(Texto("⊞", 16), "Inicio", () => { ApuntarBoton("⊞ lanzador"); AbrirMenuInicio(); });

    void AbrirMenuInicio()
    {
        // ---------- La vigilancia de lo que suena ----------
        //
        // Abrir Inicio no puede parar la música. Eso está medido —la reproducción sigue y
        // el progreso avanza con la superficie abierta— y los guardias de `Musica` y
        // `Audio` lo garantizan mientras se monta.
        //
        // Este muestreo es el INSTRUMENTO que lo demostró, y se queda: apagado en el uso
        // normal (cuesta cuatro enumeraciones de dispositivos y tres tareas por apertura,
        // y eso no se le cobra a nadie por si acaso) y encendido con `--diaginicio`, que
        // es cuando se quiere volver a comprobar.
        long mutAntes = Musica.Mutaciones;
        int volAntes = 0; bool mudoAntes = false; string salidaAntes = "";
        if (DiagInicio.Encendido)
        {
            volAntes = Audio.Volumen();
            mudoAntes = Audio.Silenciado;
            salidaAntes = SalidaActual();
            ApuntarMultimedia("antes de abrir Inicio", mutAntes, volAntes, mudoAntes, salidaAntes);
        }

        // ---------- Anclado a su botón, en las dos orientaciones ----------
        //
        // En horizontal NO se pasaba ancla, y sin ancla el cajón de la isla se despliega
        // de punta a punta: 1163 px en una pantalla de 2560, mientras el panel pide 660.
        // El contenido se estira a ese hueco —de ahí las tarjetas estiradas— y el trozo
        // de isla que queda sin tarjeta deja ver el escritorio de detrás. Medido:
        // "ancho pedido: 660 / ancho real: 1131 / recorte del cajón: NO".
        //
        // Era deliberado ("igual que siempre", decía el comentario), pero ese "siempre"
        // es justo lo que no se aguanta mirando. Con ancla, el cajón se recorta como una
        // tarjeta del ancho pedido colgada del botón Inicio, que es su entrada.
        //
        // El ancla es la referencia REAL del botón, no una coordenada: `XdeTarjeta` le
        // pregunta dónde está en cada fotograma —así la sigue mientras la isla crece o
        // cambia de borde— y en vertical `ColocarPanelFlotante` usa su centro en
        // coordenadas de pantalla. De ahí salen los cuatro bordes sin un número a mano.
        //
        // Si quitas el módulo de Inicio de la isla, `_botonInicio` es null y se ancla a
        // la isla: la tarjeta sale centrada, pero RECORTADA, que es lo que importa.
        if (!AlternarPanel(PanelInicio, CrearMenuInicio, AnchoMenuEfectivo, AltoMenuEfectivo,
                           teclado: true, ancla: (FrameworkElement?)_botonInicio ?? Isla,
                           scrollPropio: true, topeDuro: AltoMaximoDeInicio())) return;
        _filtro = null;
        CapturarClicsFuera(true);

        // Los 30 segundos muestreados, solo con el diagnóstico encendido (ver arriba).
        // Si la reproducción se corta, el log dice en qué momento y si alguien pidió el
        // cambio.
        if (DiagInicio.Encendido)
            Dispatcher.InvokeAsync(async () =>
            {
                foreach (int s in new[] { 2, 10, 30 })
                {
                    await Task.Delay(s == 2 ? 2000 : s == 10 ? 8000 : 20000);
                    if (_panelAbierto != PanelInicio)
                    {
                        Log.Info($"multimedia: Inicio ya estaba cerrado a los {s} s");
                        return;
                    }
                    ApuntarMultimedia($"{s} s con Inicio abierto", mutAntes, volAntes, mudoAntes, salidaAntes);
                }
            }, DispatcherPriority.Background);
        Dispatcher.InvokeAsync(() =>
        {
            _caja?.Focus();
            Keyboard.Focus(_caja);
            Buscar("");
            // Ya hay dónde escribir: se sueltan las teclas que el hook guardó
            // mientras se montaba el panel, en el mismo orden en que se pulsaron
            VaciarCola();
        }, DispatcherPriority.Input);
    }

    void CerrarMenu()
    {
        if (_panelAbierto != PanelInicio) return;
        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") == "1")
            Log.Info("DIAG cerrar menú desde: " +
                     string.Join(" <- ", new System.Diagnostics.StackTrace(1, false).GetFrames()
                        .Take(5).Select(f => f.GetMethod()?.Name ?? "?")));
        CapturarClicsFuera(false);
        _buscaArchivos?.Cancel();
        _relevo.Olvidar();
        _esperaArchivos?.Stop();
        OlvidarMenuDeApp();
        CerrarPanel();
    }

    // El menú NO se cierra por perder el foco. Wallpaper Engine se lo devuelve a sí
    // mismo a los pocos segundos y cerraba el menú solo mientras escribías. El criterio
    // bueno es el que pediste: un clic de verdad fuera. Se consigue capturando el ratón
    // en el subárbol de la isla, que es como se comportan los menús de Windows.
    // La captura del ratón es de la isla, y solo sirve cuando el panel vive DENTRO de
    // ella: es el caso horizontal. Con la barra vertical el menú está en otra ventana, y
    // la captura de WPF no cruza ventanas — capturarla en la isla desviaba allí los
    // clics de la tarjeta (la caja de búsqueda y las filas no recibían ninguno) y encima
    // los contaba como "clic fuera", así que el primer clic en el buscador lo cerraba.
    //
    // Para ese caso no se captura nada: el clic de fuera lo caza igual el vigilante de
    // 150 ms, que trabaja en coordenadas de PANTALLA y ya sabía apañárselas cuando
    // Windows nos quita la captura (ver VigilarClicFueraDelMenu).
    FrameworkElement? _capturada;

    void CapturarClicsFuera(bool activar)
    {
        if (activar)
        {
            if (Vertical) return;
            _capturada = Isla;
            Isla.PreviewMouseDown += ClicQuizaFuera;
            Isla.PreviewMouseUp += RearmarCaptura;
            Isla.LostMouseCapture += RecuperarCaptura;
            Mouse.Capture(Isla, CaptureMode.SubTree);
        }
        else
        {
            // Se suelta de DONDE se cogió y no de donde toque ahora: si el config
            // cambiara de orientación con el menú abierto, quitar los manejadores de la
            // otra superficie los dejaría puestos para siempre.
            if (_capturada is not { } sobre) return;
            sobre.PreviewMouseDown -= ClicQuizaFuera;
            sobre.PreviewMouseUp -= RearmarCaptura;
            sobre.LostMouseCapture -= RecuperarCaptura;
            if (ReferenceEquals(Mouse.Captured, sobre)) Mouse.Capture(null);
            _capturada = null;
        }
    }

    // ¿Este punto cae dentro del menú? Con la barra vertical el menú son DOS
    // superficies: la barra y su tarjeta. InputHitTest y no un rectángulo porque
    // acierta hasta en las esquinas redondeadas.
    bool DentroDelMenu(Point enPantalla)
    {
        if (Toca(Isla, enPantalla)) return true;
        // Con la barra vertical el menú vive en su propia ventana superpuesta: lo que
        // cuenta es su rectángulo de PANTALLA, no un hit-test dentro de la isla.
        if (_superpuesta is { } sup && sup.Contiene((int)enPantalla.X, (int)enPantalla.Y)) return true;
        return false;

        static bool Toca(FrameworkElement e, Point pantalla)
        {
            try
            {
                if (e.ActualWidth <= 0 || e.ActualHeight <= 0) return false;
                var p = e.PointFromScreen(pantalla);
                if (p.X < 0 || p.Y < 0 || p.X > e.ActualWidth || p.Y > e.ActualHeight) return false;
                return e.InputHitTest(p) != null;
            }
            catch { return false; }
        }
    }

    void ClicQuizaFuera(object remitente, MouseButtonEventArgs e)
    {
        if (_panelAbierto != PanelInicio) return;
        var sobre = _capturada ?? Isla;
        if (DentroDelMenu(sobre.PointToScreen(e.GetPosition(sobre)))) return;
        CerrarMenu();
        e.Handled = true;
    }

    // La captura del ratón no basta cuando hay un juego delante: en cuanto otra
    // aplicación se activa, Windows nos quita la captura y el clic de fuera ya no
    // llega. Así que el clic fuera se mira también a pelo, cada 150 ms, preguntando
    // por el botón y por dónde está el puntero. InputHitTest acierta hasta en las
    // esquinas redondeadas, que es más de lo que daría comparar rectángulos.
    bool _clicContado;
    bool _tuvoFoco;        // el menú ha llegado a tener el primer plano alguna vez

    void VigilarClicFueraDelMenu()
    {
        if (_panelAbierto != PanelInicio) { _clicContado = false; _tuvoFoco = false; return; }

        // 1) ¿Se ha llevado el primer plano una aplicación de verdad? Entonces has
        //    hecho clic en ella y el menú sobra. Esto va aquí, cada 150 ms, y no en
        //    el tick de un segundo: tardaba dos segundos largos en enterarse.
        //    Ojo: solo si el menú LLEGÓ a tener el foco. Con un juego a pantalla
        //    completa puede que no nos lo suelte nunca, y entonces cerrar aquí sería
        //    cerrar el menú nada más abrirlo.
        var otra = Nativo.Activa();
        if (otra == _self || Nativo.EsNuestra(otra)) { _tuvoFoco = true; return; }

        // Hacer una captura del menú abierto tiene que poder hacerse. El recortador
        // se pone delante y te hace arrastrar un recuadro por encima de la isla: con
        // la regla normal, eso es "otra aplicación al frente" y un clic fuera, o sea
        // dos motivos para cerrar el menú justo mientras lo estabas fotografiando.
        if (EsRecortador(otra)) return;

        if (_tuvoFoco && !SeLoDevuelveSolo(otra))
        {
            Log.Info($"el primer plano se ha ido a \"{Nativo.Titulo(otra)}\": el menú se cierra");
            CerrarMenu();
            return;
        }

        // 2) Y un clic fuera aunque nadie se lleve el foco (el escritorio, por
        //    ejemplo). HuboClicIzquierdo no se pierde los clics cortos.
        bool clic = Nativo.BotonIzquierdoPulsado() || Nativo.HuboClicIzquierdo();
        if (!clic) { _clicContado = false; return; }
        if (_clicContado) return;
        _clicContado = true;
        try
        {
            var r = Monitores.Raton();
            // Las dos superficies, no solo la isla: con la barra vertical, un clic en
            // la tarjeta del menú estaba "fuera de la isla" y lo cerraba.
            if (DentroDelMenu(new Point(r.X, r.Y))) return;
            Log.Info("clic fuera del menú: se cierra");
            CerrarMenu();
        }
        catch { }
    }

    int _intentosFoco;

    // Quien se lleva el primer plano puede ser una aplicación de verdad (has hecho
    // clic en ella) o uno de esos programas que se lo devuelven a sí mismos cada
    // pocos segundos: Wallpaper Engine y el escritorio. Con los primeros no hay nada
    // que pelear, el menú sobra. Con los segundos sí, que si no te quedas escribiendo
    // en el vacío. Antes se peleaba con todos, y con un juego delante era imposible
    // cerrar el menú con un clic fuera: lo cerrabas y el foco volvía solo.
    static readonly string[] SeDevuelvenElFoco =
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "SHELLDLL_DefView", "SysListView32"
    };

    // Los que salen a pantalla completa a recortar: mientras uno de estos está
    // delante, el menú no se cierra ni se pelea por el primer plano
    static readonly string[] Recortadores =
    {
        "snippingtool", "screenclippinghost", "screensketch", "gamebar",
        "sharex", "greenshot", "snagit", "lightshot", "picpick", "flameshot"
    };

    static bool EsRecortador(IntPtr h)
    {
        if (h == IntPtr.Zero) return false;
        var exe = Nativo.Exe(h);
        if (exe.Length == 0) return false;
        var nombre = Path.GetFileNameWithoutExtension(exe);
        return Recortadores.Any(r => nombre.Contains(r, StringComparison.OrdinalIgnoreCase));
    }

    static bool SeLoDevuelveSolo(IntPtr h)
    {
        if (h == IntPtr.Zero) return true;                       // nadie al frente: no es un clic tuyo
        if (SeDevuelvenElFoco.Contains(Nativo.Clase(h))) return true;
        var exe = Nativo.Exe(h);
        return exe.Length > 0 &&
               Path.GetFileNameWithoutExtension(exe).StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase);
    }

    void VigilarFocoDelMenu()
    {
        if (_panelAbierto != PanelInicio) { _intentosFoco = 0; return; }
        var otra = Nativo.Activa();
        if (otra == _self || Nativo.EsNuestra(otra)) { _intentosFoco = 0; return; }

        // Si se lo ha llevado una aplicación de verdad no se toca nada: ya lo cierra
        // el vigilante de 150 ms. Pelearse aquí le robaba el primer plano al juego
        // durante un instante antes de cerrarse.
        if (!SeLoDevuelveSolo(otra)) return;
        if (EsRecortador(otra)) return;      // y al recortador tampoco se le quita

        if (++_intentosFoco > 3)
        {
            Log.Info("el menú no consigue mantener el foco: se cierra");
            CerrarMenu();
            return;
        }
        Log.Info($"el menú ha perdido el foco frente a \"{Nativo.Titulo(otra)}\": recuperándolo ({_intentosFoco})");
        Nativo.ForzarPrimerPlano(_self);
        Dispatcher.InvokeAsync(() => { _caja?.Focus(); Keyboard.Focus(_caja); }, DispatcherPriority.Input);
    }

    // Un Button captura el ratón al pulsarlo y lo suelta al soltarlo: así sabe si
    // levantaste el dedo encima. Si se la quitamos en ese momento, nunca llega a su
    // Click. Por eso solo se recupera cuando la captura no la tiene NADIE.
    void RecuperarCaptura(object remitente, MouseEventArgs e) => ArmarCaptura();

    // Y al soltar el botón se vuelve a armar, que es cuando la suelta él
    void RearmarCaptura(object remitente, MouseButtonEventArgs e) => ArmarCaptura();

    void ArmarCaptura()
    {
        if (_panelAbierto != PanelInicio) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (_panelAbierto != PanelInicio) return;
            if (Mouse.Captured != null) return;      // se la ha llevado algo de dentro
            // Y si el primer plano NO es nuestro, la captura no se recupera. Windows
            // nos la quita cuando se activa otra aplicación, y volver a cogerla le
            // robaba el ratón a quien esté delante: con el menú abierto, el recortador
            // de pantalla (Win+Shift+S, Impr Pant) salía pero no te dejaba arrastrar
            // el recuadro. El clic de fuera lo sigue cazando el vigilante de 150 ms.
            if (!Nativo.EsNuestra(Nativo.Activa())) return;
            // Solo se recupera si se cogió: en vertical no se captura nada (ver
            // CapturarClicsFuera), y volver a capturar aquí reintroduciría el fallo.
            if (_capturada is { } sobre) Mouse.Capture(sobre, CaptureMode.SubTree);
        }, DispatcherPriority.Input);
    }

    // ================= Montaje =================
    // Los actualizadores del menú viven aparte de los de la isla: se tiran a la
    // basura al cerrarlo, porque sus controles ya no existen
    readonly List<Action> _actualizadoresMenu = new();

    FrameworkElement CrearMenuInicio()
    {
        _actualizadoresMenu.Clear();

        var raiz = new Grid { Margin = new Thickness(6, 8, 6, 10) };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // cabecera
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // caja de búsqueda
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _caja = new TextBox
        {
            Padding = new Thickness(9, 0, 12, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            // Transparente a propósito: el fondo y las esquinas los pone el marco de más
            // abajo. Pintados aquí daban lo que se veía en la captura — un rectángulo gris
            // de esquinas rectas, de filo a filo, entre tarjetas oscuras redondeadas: la
            // pieza que peor encajaba de todo el lanzador.
            Background = Brushes.Transparent,
            Foreground = _texto,
            CaretBrush = _acentoLegible,
            BorderThickness = new Thickness(0),
            FontSize = FontSize + 3,
            Tag = "caja"
        };
        // ---------- Y una pista dentro de la caja ----------
        //
        // No la había: el lanzador se abría con una franja vacía y un cursor parpadeando.
        // Quien no sepa que ahí se busca —y que se buscan aplicaciones, archivos Y la web—
        // no tiene de dónde deducirlo. Se vio mirando una captura del menú entero, no el
        // código: en el código la caja parece perfectamente normal.
        //
        // Un TextBlock detrás, que no recoge clics y se esconde en cuanto escribes. Es el
        // mismo patrón que el buscador de la biblioteca; no se toca la plantilla del
        // TextBox porque eso serían dos formas de poner una pista en la misma aplicación.
        var pistaDeLaCaja = new TextBlock
        {
            Text = "Buscar aplicaciones, archivos y en la web…",
            Foreground = _texto,
            Opacity = 0.45,
            FontSize = FontSize + 3,
            Margin = new Thickness(10, 0, 13, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };

        _caja.TextChanged += (_, _) =>
        {
            pistaDeLaCaja.Visibility = _caja!.Text.Length == 0
                                     ? Visibility.Visible : Visibility.Collapsed;
            Buscar(_caja.Text);
        };

        // ---------- El marco: lupa dentro y esquinas redondas ----------
        //
        // Es la misma construcción que `VentanaAjustes.CajaDeBusqueda`: un borde que pone el
        // fondo y el radio, una lupa delante y el campo transparente encima. No se toca la
        // plantilla del TextBox, que es justo lo que pide el comentario de la pista — dos
        // formas de hacer un buscador en la misma aplicación es lo que había que evitar.
        //
        // Y la lupa no es adorno: era una franja gris con un cursor parpadeando. Quien no
        // supiera ya que ahí se busca no tenía de dónde deducirlo. La pista de texto lo dice
        // y desaparece al escribir; la lupa se queda.
        var filaDeLaCaja = new Grid();
        filaDeLaCaja.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filaDeLaCaja.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lupa = Glifo(char.ConvertFromUtf32(0xE721));   // Search
        lupa.FontSize = 14;
        lupa.Opacity = 0.5;
        lupa.Margin = new Thickness(13, 0, 0, 0);
        lupa.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(lupa, 0);
        filaDeLaCaja.Children.Add(lupa);

        Grid.SetColumn(_caja, 1);
        filaDeLaCaja.Children.Add(_caja);
        Grid.SetColumn(pistaDeLaCaja, 1);
        filaDeLaCaja.Children.Add(pistaDeLaCaja);   // después: por encima del campo

        var marcoDeLaCaja = new Border
        {
            Height = 38,
            Background = _superficie,
            CornerRadius = new CornerRadius(10),
            Child = filaDeLaCaja,
        };
        Grid.SetRow(marcoDeLaCaja, 1);
        raiz.Children.Add(marcoDeLaCaja);

        // Y el nombre, para quien no puede ver la pista.
        System.Windows.Automation.AutomationProperties.SetName(
            _caja, "Buscar aplicaciones, archivos y en la web");

        // El teclado se escucha en la RAÍZ del panel, no solo en la caja: así las
        // flechas, Intro, Tab y Escape siguen funcionando aunque el foco se haya ido a
        // un botón de la portada o a una fila. Antes, en cuanto tocabas algo del panel,
        // dejabas de poder bajar con las flechas hasta volver a hacer clic en la caja.
        raiz.PreviewKeyDown += CajaTecla;

        if (_cfg.Inicio.Saludo)
        {
            var cabecera = CrearCabecera();
            Grid.SetRow(cabecera, 0);
            raiz.Children.Add(cabecera);
        }

        // La marca de selección va por detrás de las filas y se desliza entre ellas.
        // Con el acento LEGIBLE, no con el de verdad: si eliges un acento negro (o lo
        // saca así el extractor del wallpaper) la marca quedaba negra sobre fondo negro
        // y no se veía NADA de lo que estaba seleccionado, ni moverse con las flechas.
        _marcaSeleccion = new Border
        {
            Height = AltoFila - 4,
            CornerRadius = new CornerRadius(8),
            Background = _elegido,
            BorderBrush = _acentoLegible,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2, 0, 2, 0),
            Visibility = Visibility.Collapsed,
            RenderTransform = new TranslateTransform()
        };

        _lista = new StackPanel();
        var capa = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        capa.Children.Add(_marcaSeleccion);
        capa.Children.Add(_lista);

        _scroll = new ScrollViewer
        {
            Content = capa,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(_scroll, 2);
        raiz.Children.Add(_scroll);

        // Sin nada escrito no se ven resultados, sino el panel de siempre: lo que
        // suena, los accesos rápidos, las gráficas, el wallpaper y las fijadas
        _portada = CrearPortada();
        Grid.SetRow(_portada, 2);
        raiz.Children.Add(_portada);

        _pista = new TextBlock
        {
            Foreground = _texto,
            Opacity = 0.4,
            Margin = new Thickness(12, 14, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Text = "Sin resultados"
        };
        Grid.SetRow(_pista, 2);
        raiz.Children.Add(_pista);

        // La capa del menú del clic derecho, la última de todas: tiene que quedar por
        // encima de la lista y de la portada (ver MainWindow.MenuApp.cs). Vive
        // colapsada y no estorba hasta que se pide.
        var capaMenu = CrearCapaMenuApp();
        Grid.SetRow(capaMenu, 0);
        Grid.SetRowSpan(capaMenu, 3);
        raiz.Children.Add(capaMenu);

        _raizMenu = raiz;

        // ---------- La superficie opaca ----------
        //
        // `PanelShell` existía desde el rediseño y esta superficie —la que de verdad se
        // abre con el botón Inicio y con la tecla Windows— nunca lo consumía: la raíz era
        // este `Grid`, que no pinta nada. El contenido se apoyaba directamente en la isla,
        // que es translúcida, y por eso se leía el texto del escritorio de detrás. La
        // traza lo decía con todas las letras: "shell opaco: NO (Grid)".
        //
        // Con margen 0 porque la raíz ya trae el suyo (6, 8, 6, 10) y dos acolchados
        // seguidos dejan el panel con un marco que no pega.
        return PanelShell(raiz, 0);
    }

    ScrollViewer? _portada;

    // Entra escalonada: cada tarjeta un pelín después que la anterior
    ScrollViewer CrearPortada()
    {
        // ---------- Montar es LEER ----------
        //
        // Mientras se construye la portada, las puertas que escriben audio y multimedia
        // están cerradas: si alguna tarjeta intentara pausar, cambiar el volumen o el
        // dispositivo de salida, no pasa y queda apuntado con su nombre en el log. Ver
        // `Musica.Permitido` y `Audio.Permitido`.
        //
        // El `finally` no es adorno: si una tarjeta lanzara y el guardia se quedara
        // puesto, el siguiente clic del usuario en pausa no haría nada.
        Musica.Construyendo = true;
        Audio.Construyendo = true;
        try { return MontarPortada(); }
        finally
        {
            Musica.Construyendo = false;
            Audio.Construyendo = false;
        }
    }

    ScrollViewer MontarPortada()
    {
        var pila = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        // Las tarjetas son las del diseño aprobado (`Card`, opacas y con su filo), las
        // mismas que se montaron para el centro de control. No hay dos pantallas con lo
        // mismo: el centro de control ES esta portada, y su único acceso es el botón
        // Inicio. Las piezas propias del lanzador —las fijadas y la energía— se quedan:
        // una superficie unificada no es una superficie con menos cosas.
        var piezas = new UIElement[]
        {
            CardMultimedia(),
            CardAccionesRapidas(),
            CardMetricas(),
            CardFondo(),
            CardConectividad(),
            CrearFijadas(),
            CrearEnergia(),
            CardAccionesDelSistema(),
        };

        for (int i = 0; i < piezas.Length; i++)
        {
            pila.Children.Add(piezas[i]);
            if (piezas[i] is not FrameworkElement fe) continue;
            var mover = new TranslateTransform { Y = 14 };
            fe.RenderTransform = mover;
            double retardo = 90 + i * 45;
            fe.BeginAnimation(OpacityProperty, Kf(0, 1, 240, retardo));
            mover.BeginAnimation(TranslateTransform.YProperty, Kf(14, 0, 420, retardo, Muelle));
            mover.Y = 0;
        }
        // El idioma del teclado solo sale si tienes más de una distribución puesta: la
        // propia tarjeta lo decide, y por eso se añade aparte del bucle de entrada.
        CardTeclado(pila);


        return new ScrollViewer
        {
            Content = pila,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    // ================= Búsqueda =================
    void Buscar(string consulta)
    {
        if (_lista == null) return;
        var limpia = consulta.Trim();
        var norm = Parecido.Normalizar(limpia);

        // Al cambiar lo escrito se vuelve a la primera fila: lo que teclees manda sobre
        // donde estuviera la marca (una flecha de antes, o el ratón quieto encima de la
        // lista). Así Intro abre siempre la primera opción, que es lo que esperas.
        if (limpia != _ultimaConsulta) { _ultimaConsulta = limpia; _seleccion = 0; }

        var reloj = Stopwatch.StartNew();
        var encontrados = new List<Resultado>();

        if (limpia.Length > 0)
        {
            if (Fuentes.Comando(limpia) is { } c) encontrados.Add(c);
            else
            {
                if (Fuentes.Calculo(limpia) is { } calc) encontrados.Add(calc);
                encontrados.AddRange(Fuentes.Apps(norm, 40));
                encontrados.AddRange(Fuentes.Ordenes(norm));
                encontrados.AddRange(VentanasAbiertas(norm));
                encontrados.AddRange(Fuentes.Ajustes(norm));
                if (AjustesDeLaNube(norm) is { } aj) encontrados.Add(aj);
                encontrados.Add(Fuentes.Web(limpia, _cfg.Inicio.BuscadorWeb));
            }
        }

        foreach (var r in encontrados) r.Puntos = Ranking.ConCostumbre(r.Puntos, Uso.Empujon(r.Clave), r.Nivel);
        _resultados = Ranking.Ordenar(encontrados);
        Pintar();
        reloj.Stop();

        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") == "1")
            Log.Info($"DIAG buscar \"{limpia}\": {_resultados.Count} resultados en {reloj.Elapsed.TotalMilliseconds:0.0} ms");

        ProgramarArchivos(limpia);
    }

    // La propia ventana de ajustes, buscable como cualquier otra cosa
    Resultado? AjustesDeLaNube(string norm)
    {
        var m = Parecido.Medir(norm, "Ajustes de la nube");
        var m2 = Parecido.Medir(norm, "NubeBar configuración");
        if (m2.Puntos > m.Puntos) m = m2;
        if (m.Puntos < 0.5) return null;
        return new Resultado
        {
            Categoria = Categoria.Ajuste,
            Titulo = "Ajustes de la nube",
            Detalle = "Cambiar colores, módulos y comportamiento",
            Clave = "ajuste:nubebar",
            Puntos = m.Puntos,
            Nivel = m.Nivel,
            Abrir = AbrirAjustes
        };
    }

    // Las ventanas abiertas salen de los grupos que ya mantiene el carrusel
    IEnumerable<Resultado> VentanasAbiertas(string norm)
    {
        foreach (var g in _grupos.Values)
            foreach (var h in g.Ventanas)
            {
                var titulo = Nativo.Titulo(h);
                var porTitulo = Parecido.Medir(norm, titulo);
                var porExe = Parecido.Medir(norm, Path.GetFileNameWithoutExtension(g.Exe));
                // El nombre del ejecutable vale un poco menos que el título de la
                // ventana: el título es lo que estás viendo en la barra de tareas.
                bool ganaTitulo = porTitulo.Puntos >= porExe.Puntos * 0.9;
                double mejor = ganaTitulo ? porTitulo.Puntos : porExe.Puntos * 0.9;
                var nivelV = ganaTitulo ? porTitulo.Nivel : porExe.Nivel;
                if (mejor < 0.5) continue;
                var v = h;
                yield return new Resultado
                {
                    Categoria = Categoria.Ventana,
                    Titulo = titulo,
                    Detalle = Path.GetFileName(g.Exe),
                    Clave = "ventana:" + Path.GetFileName(g.Exe),
                    IconoRuta = g.Exe.StartsWith("hwnd:") ? null : g.Exe,
                    Puntos = Math.Min(1.0, mejor * 1.02),   // saltar a algo abierto suele ser lo que quieres
                    Nivel = nivelV,
                    Abrir = () => Nativo.Activar(v)
                };
            }
    }

    // Los archivos tardan: se piden un poco después y se mezclan cuando llegan
    void ProgramarArchivos(string consulta)
    {
        _buscaArchivos?.Cancel();
        _relevo.Olvidar();
        _esperaArchivos?.Stop();
        if (!_cfg.Inicio.BuscarArchivos || consulta.Length < 3 || consulta.StartsWith(">")) return;

        _esperaArchivos ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _esperaArchivos.Tick -= LanzarArchivos;
        _esperaArchivos.Tick += LanzarArchivos;
        _esperaArchivos.Start();
    }

    void LanzarArchivos(object? remitente, EventArgs e)
    {
        _esperaArchivos?.Stop();
        if (_panelAbierto != PanelInicio || _caja == null) return;
        var consulta = _caja.Text.Trim();
        if (consulta.Length < 3) return;

        // Dos cerrojos, y hacen falta los dos. El token corta la consulta a Windows
        // Search en cuanto sigues escribiendo; el turno del relevo descarta lo que ya
        // venía de vuelta cuando se canceló —una tarea puede haber terminado justo
        // antes y estar ya en la cola del hilo de interfaz, y entonces el token no
        // dice nada—. Sin el turno salían archivos de "doc" debajo de "documento".
        var cts = new CancellationTokenSource();
        var anterior = _buscaArchivos;
        _buscaArchivos = cts;
        anterior?.Cancel();
        anterior?.Dispose();
        long turno = _relevo.Pedir();

        Task.Run(() => Fuentes.Archivos(consulta, 8, cts.Token), cts.Token)
            .ContinueWith(t =>
            {
                if (!_relevo.SigueSiendoLaUltima(turno)) return;
                if (cts.IsCancellationRequested || !t.IsCompletedSuccessfully) return;
                if (_panelAbierto != PanelInicio || _caja == null) return;
                if (_caja.Text.Trim() != consulta) return;   // ya has seguido escribiendo
                foreach (var r in t.Result) r.Puntos = Ranking.ConCostumbre(r.Puntos, Uso.Empujon(r.Clave), r.Nivel);
                _resultados = Ranking.Ordenar(_resultados.Concat(t.Result));
                Pintar();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ================= Pintado =================
    void Pintar()
    {
        if (_lista == null || _pista == null) return;

        var mostrar = _filtro is { } f ? _resultados.Where(r => r.Categoria == f).ToList() : _resultados;
        bool escribiendo = _caja?.Text.Trim().Length > 0;

        // Dónde está el ratón ahora, antes de crear las filas: las de abajo comparan
        // con esto para saber si un MouseEnter es tuyo o del repintado
        _ratonAlPintar = Mouse.GetPosition(Isla);

        // Con la caja vacía manda la portada; en cuanto escribes, los resultados
        if (_portada != null) _portada.Visibility = escribiendo ? Visibility.Collapsed : Visibility.Visible;
        if (_scroll != null) _scroll.Visibility = escribiendo ? Visibility.Visible : Visibility.Collapsed;
        _pista.Visibility = escribiendo && mostrar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _lista.Children.Clear();
        // Más filas de las que caben no aportan nada y cada una cuesta un icono
        foreach (var r in mostrar.Take(MaxFilas)) _lista.Children.Add(CrearFila(r));

        _seleccion = Math.Min(_seleccion, Math.Max(0, Math.Min(mostrar.Count, MaxFilas) - 1));
        if (mostrar.Count == 0) _seleccion = 0;
        MoverMarca(inmediato: true);
    }

    List<Resultado> Visibles =>
        _filtro is { } f ? _resultados.Where(r => r.Categoria == f).Take(MaxFilas).ToList() : _resultados.Take(MaxFilas).ToList();

    UIElement CrearFila(Resultado r)
    {
        var fila = new Grid { Height = AltoFila, Margin = new Thickness(10, 0, 10, 0) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (r.IconoRuta is { } ruta)
        {
            var img = new Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetColumn(img, 0);
            fila.Children.Add(img);
            // Sacar el icono es una llamada COM al shell y la primera vez cuesta.
            // Se pide en segundo plano para que teclear no se quede esperando.
            Dispatcher.InvokeAsync(() => img.Source = Iconos.De(ruta, 32), DispatcherPriority.Background);
        }

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = r.Titulo,
            Foreground = _texto,
            FontSize = FontSize + 1,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (r.Detalle.Length > 0)
            textos.Children.Add(new TextBlock
            {
                Text = r.Detalle,
                Foreground = _texto,
                Opacity = 0.45,
                FontSize = FontSize - 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        Grid.SetColumn(textos, 1);
        fila.Children.Add(textos);

        var etiqueta = new TextBlock
        {
            Text = r.Etiqueta,
            Foreground = _acentoLegible,
            Opacity = 0.7,
            FontSize = FontSize - 3,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(etiqueta, 2);
        fila.Children.Add(etiqueta);

        var zona = new Border { Background = Brushes.Transparent, Child = fila, Cursor = Cursors.Hand };
        zona.MouseEnter += (_, _) =>
        {
            if (!RatonSeHaMovido()) return;
            int i = Visibles.FindIndex(x => ReferenceEquals(x, r));
            if (i >= 0) { _seleccion = i; MoverMarca(); }
        };
        zona.MouseLeftButtonUp += (_, _) => Ejecutar(r, false);

        // Clic derecho: el menú de ESTA aplicación. Se marca como manejado para que no
        // suba hasta la isla y abra el suyo —el de cerrar aplicaciones y salir de
        // NubeBar—, que no tiene nada que ver con lo que tienes bajo el puntero. El
        // clic izquierdo sigue abriendo, igual que antes.
        if (TieneMenuDeApp(r))
            zona.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                int i = Visibles.FindIndex(x => ReferenceEquals(x, r));
                if (i >= 0) { _seleccion = i; MoverMarca(); }
                AbrirMenuDeApp(r, e.GetPosition(_raizMenu ?? (IInputElement)zona));
            };
        else
            // Sin menú propio, pero tampoco el de la isla: un cálculo o una búsqueda
            // en la web no tienen nada que ofrecer, y abrir ahí "Salir de NubeBar" es
            // la clase de sorpresa que hace que no te fíes de un clic derecho.
            zona.MouseRightButtonUp += (_, e) => e.Handled = true;

        return zona;
    }

    // Al repintar la lista las filas nuevas nacen debajo del puntero y disparan su
    // MouseEnter sin que hayas movido el ratón: eso le robaba la marca a cada tecla, y
    // entonces Intro abría lo que hubiera bajo el cursor en vez de la primera opción.
    // Solo cuenta como hover si el puntero está en otro sitio que cuando se pintó.
    bool RatonSeHaMovido()
    {
        if (double.IsNaN(_ratonAlPintar.X)) return true;
        return (Mouse.GetPosition(Isla) - _ratonAlPintar).Length > 2;
    }

    // La marca de selección se desliza hasta la fila elegida en vez de saltar
    void MoverMarca(bool inmediato = false)
    {
        if (_marcaSeleccion == null || _lista == null) return;
        var cuantos = Visibles.Count;
        if (cuantos == 0) { _marcaSeleccion.Visibility = Visibility.Collapsed; return; }

        _marcaSeleccion.Visibility = Visibility.Visible;
        _marcaSeleccion.Width = Math.Max(0, (_scroll?.ActualWidth ?? AnchoMenu) - 8);

        double destino = _seleccion * AltoFila + 2;
        if (_marcaSeleccion.RenderTransform is not TranslateTransform t)
            _marcaSeleccion.RenderTransform = t = new TranslateTransform();

        double ahora = t.Y;
        t.BeginAnimation(TranslateTransform.YProperty, null);
        t.Y = destino;
        if (!inmediato && Math.Abs(ahora - destino) > 0.5)
            t.BeginAnimation(TranslateTransform.YProperty, Kf(ahora, destino, 180, 0, Suave));

        // La fila elegida a pleno color y las demás un poco apagadas: con la marca a
        // secas no se distinguía bien cuál era, y menos al bajar con las flechas
        for (int i = 0; i < _lista.Children.Count; i++)
            if (_lista.Children[i] is UIElement fila)
                fila.Opacity = i == _seleccion ? 1 : 0.65;

        // Y un destello al cambiar de fila, para que el salto se note además de verse
        if (!inmediato && Math.Abs(ahora - destino) > 0.5)
            _marcaSeleccion.BeginAnimation(OpacityProperty, Kf(0.45, 1, 220, 0, Suave));

        // Mantener la selección a la vista
        _scroll?.ScrollToVerticalOffset(Math.Max(0, destino - (FilasVisibles - 1) * AltoFila));
    }

    // ================= Teclado =================
    void CajaTecla(object remitente, KeyEventArgs e)
    {
        // Con el menú de una aplicación abierto, las teclas son suyas: Esc lo cierra a
        // él y no el buscador entero, y las flechas no mueven una marca que ahora mismo
        // está detrás de una tarjeta.
        if (MenuDeAppAbierto)
        {
            if (e.Key == Key.Escape) { CerrarMenuDeApp(); e.Handled = true; }
            // Las flechas, Intro y Tab se comen: actuarían sobre una lista que ahora
            // está detrás de la tarjeta y no se ve. Lo que se escriba sigue yendo a la
            // caja, que es inofensivo y evita tener que acordarse de volver a ella.
            else if (e.Key is Key.Down or Key.Up or Key.Enter or Key.Tab) e.Handled = true;
            return;
        }

        var visibles = Visibles;
        switch (e.Key)
        {
            case Key.Escape:
                CerrarMenu();
                e.Handled = true;
                break;

            case Key.Down:
                if (visibles.Count > 0) { _seleccion = (_seleccion + 1) % visibles.Count; MoverMarca(); }
                VolverALaCaja();
                e.Handled = true;
                break;

            case Key.Up:
                if (visibles.Count > 0) { _seleccion = (_seleccion - 1 + visibles.Count) % visibles.Count; MoverMarca(); }
                VolverALaCaja();
                e.Handled = true;
                break;

            case Key.Enter:
                EjecutarSeleccion(visibles, Teclado.Control);
                e.Handled = true;
                break;

            case Key.Tab:
                RotarFiltro();
                e.Handled = true;
                break;
        }
    }

    // Si el foco se había ido a otro trasto del panel, al navegar vuelve a la caja,
    // para que puedas seguir escribiendo sin tener que hacer clic en ella
    void VolverALaCaja()
    {
        if (_caja == null || _caja.IsKeyboardFocused) return;
        _caja.Focus();
        Keyboard.Focus(_caja);
        _caja.CaretIndex = _caja.Text.Length;
    }

    // Intro abre la fila marcada; si la marca se ha quedado fuera de sitio, la primera
    void EjecutarSeleccion(List<Resultado> visibles, bool ctrl)
    {
        if (visibles.Count == 0) return;
        Ejecutar(visibles[_seleccion >= 0 && _seleccion < visibles.Count ? _seleccion : 0], ctrl);
    }

    // Tab rota entre las categorías que hay ahora mismo, y vuelve a "todas"
    void RotarFiltro()
    {
        var categorias = _resultados.Select(r => r.Categoria).Distinct().OrderBy(c => c).ToList();
        if (categorias.Count == 0) return;

        if (_filtro is not { } actual) _filtro = categorias[0];
        else
        {
            int i = categorias.IndexOf(actual);
            _filtro = (i < 0 || i == categorias.Count - 1) ? null : categorias[i + 1];
        }
        _seleccion = 0;
        Pintar();
    }

    void Ejecutar(Resultado r, bool abrirUbicacion)
    {
        Uso.Apuntar(r.Clave);
        if (abrirUbicacion && r.Carpeta is { } carpeta && Directory.Exists(carpeta))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{r.IconoRuta ?? carpeta}\"")
                { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex) { Log.Error("abrir ubicación", ex); }
        }
        else
        {
            try { r.Abrir(); } catch (Exception ex) { Log.Error("abrir resultado", ex); }
        }
        // El cálculo se copia y el menú se queda, que es lo cómodo para encadenar cuentas
        if (r.Categoria != Categoria.Calculo) CerrarMenu();
    }
}

// Azúcar para leer el modificador sin arrastrar using por todo el archivo
static class Teclado
{
    public static bool Control => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
}
