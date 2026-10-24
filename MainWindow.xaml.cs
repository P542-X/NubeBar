using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace NubeBar;

public partial class MainWindow : Window
{
    Config _cfg = Config.Cargar() ?? new Config();
    IntPtr _self, _ultimaActiva;

    // Pinceles fijos: cuando cambia el tema se anima su color, y todo lo que los usa cambia suave
    readonly SolidColorBrush _fondo = new(Colors.Black), _texto = new(Colors.White),
                             _acento = new(Colors.CornflowerBlue), _hover = new(Color.FromArgb(0x30, 255, 255, 255));
    // El acento "legible" es el mismo color corregido para que se lea sobre el
    // fondo. Se usa en TEXTO; para bordes y barras sigue valiendo el de verdad.
    readonly SolidColorBrush _acentoLegible = new(Colors.CornflowerBlue);
    Color _cTexto = Colors.White, _cAcento = Colors.CornflowerBlue, _cAcentoLegible = Colors.CornflowerBlue;

    // Los colores del tema por rol, para que un widget pueda preguntar "¿de qué color
    // pinto una fila?" sin mezclar a ojo. Se recalcula en AplicarTema.
    Roles _roles = Roles.PorDefecto();

    // Y sus pinceles, con el mismo truco que los de arriba: el pincel es fijo y lo que
    // cambia es su Color, animado. Por eso al cambiar de tema TODO lo que los use se
    // tiñe solo y en suave, sin volver a montar nada.
    //
    // Antes esto no existía y cada sitio se hacía su propio
    // `new SolidColorBrush(FromArgb(0x33, _cTexto…))`: veinticinco mezclas a ojo, cada
    // una con su alfa, y ninguna cambiaba de color hasta que se remontaba el panel.
    readonly SolidColorBrush _superficie = new(), _superficieAlta = new(), _borde = new(), _elegido = new(),
                             _textoAtenuado = new(), _secundario = new(), _sistema = new();

    readonly List<Action> _actualizadores = new();
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _recarga = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly DispatcherTimer _wpTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    FileSystemWatcher? _watcher, _wpWatcher;
    bool _expandida;
    bool _panelEncogido;  // el panel abierto quiere la isla recogida: el ratón no la vuelve a abrir
    bool _aplicando;      // dentro de Aplicar(): no tocar el ancho, aún se está montando todo
    bool _animandoAncho;  // hay una expansión o contracción en curso, no interrumpirla
    bool _cerrando;       // ya se está cerrando: nadie puede volver a esconder la barra
    DropShadowEffect? _brillo;   // halo del color de acento alrededor de la isla

    // Las operaciones sobre la barra de Windows tardan ~0,7 s y van en un hilo aparte,
    // así que se encolan en fila india: sin esto, un "esconder" lanzado antes podría
    // ejecutarse después del "mostrar" del cierre y dejarte sin barra de tareas.
    readonly object _candadoShell = new();
    Task _colaShell = Task.CompletedTask;

    // Estado de Wallpaper Engine
    string? _wpRuta, _wpTitulo;
    Paleta? _paleta;
    bool _wpPausado, _wpMuteado, _iconosOcultos, _wpOcupado;
    DateTime _wpUltimo;
    string _ultimoColorEnviado = "";

    const string FuenteIconos = "Segoe Fluent Icons, Segoe MDL2 Assets";

    // Curvas: "Muelle" se pasa un pelín y vuelve (como la isla del iPhone), "Suave" frena sin rebote
    static readonly IEasingFunction Muelle = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
    static readonly IEasingFunction Suave = new CubicEase { EasingMode = EasingMode.EaseOut };
    // Para CERRAR. Suave es EaseOut: arranca de golpe y se arrastra al final, que es
    // perfecto para abrir (lo ves aparecer ya) y malísimo para cerrar: el panel se
    // plegaba casi entero en los primeros 80 ms y parecía un corte, no un cierre.
    // Comoda arranca despacio y acelera, que es como se dobla algo de verdad.
    static readonly IEasingFunction Comoda = new CubicEase { EasingMode = EasingMode.EaseInOut };
    // El otro embudo: las animaciones de COLOR piden una Duration, no keyframes. Se
    // escala aquí por el mismo motivo que en Kf.
    static Duration Ms(double ms) => new(TimeSpan.FromMilliseconds(Animacion.Escalar(ms)));

    bool EsIsla => _cfg.Barra.Modo.Equals("isla", StringComparison.OrdinalIgnoreCase);
    // Lo consulta medio proyecto. Pasa por PosicionEfectiva para que los bordes
    // laterales (que cuelgan de la franja de arriba) no se tomen por "abajo".
    bool Arriba => ArribaEfectivo;

    // ================= Una isla por monitor =================
    // Hay una ventana de estas por cada pantalla. La del monitor principal es la
    // "jefa": lleva lo que debe existir una sola vez (el hook de teclado, el atajo
    // global, el índice del buscador, la música, las lecturas de CPU y el trato con
    // las barras de Windows). Las demás solo se dibujan y atienden a sus apps.
    public static readonly List<MainWindow> Islas = new();

    Monitores.Info _monitor;
    readonly bool _esJefa;

    public Monitores.Info Monitor => _monitor;

    public MainWindow() : this(Monitores.Principal(), true) { }

    public MainWindow(Monitores.Info monitor, bool esJefa)
    {
        _monitor = monitor;
        _esJefa = esJefa;

        if (_esJefa)
        {
            Log.Nuevo();
            // Ningún error tumba la app: se apunta en el log y se sigue
            Application.Current.DispatcherUnhandledException += (_, e) => { Log.Error("interfaz", e.Exception); e.Handled = true; };
            TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("tarea", e.Exception); e.SetObserved(); };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Log.Error("FATAL", e.ExceptionObject);
                Nativo.MostrarBarraWindows(true);   // nunca dejarte sin barra de tareas
            };
        }
        Islas.Add(this);

        InitializeComponent();
        Resources["Hover"] = _hover;
        Isla.Background = _fondo;
        Foreground = _texto;
        // Mientras un panel se dibuja como tarjeta, el recorte se rehace con cada
        // medida nueva: así la tarjeta crece y se pliega pegada a la animación
        Isla.SizeChanged += (_, _) => AplicarRecorteDelCajon();


        Loaded += async (_, _) =>
        {
            try
            {
                _self = new WindowInteropHelper(this).Handle;
                Nativo.NoActivable(_self);
                VigilarCambiosDePantalla();
                MontarArrastre();
                Aplicar();
                if (_esJefa) RegistrarAtajos();
            }
            catch (Exception ex) { Log.Error("arranque", ex); }

            // Fuera del try: la isla arranca invisible, así que pase lo que pase
            // en Aplicar hay que hacerla aparecer o no se vería nada
            AnimarAparicion();

            try
            {
                if (_esJefa)
                {
                    Guardian.Lanzar();          // por si me matan: alguien tiene que devolver el shell
                    // Encenderla hace que las apps nos manden sus iconos a NOSOTROS en
                    // vez de a Explorer. Si el módulo no está puesto en ninguna zona,
                    // los iconos desaparecerían de la barra de Windows sin aparecer en
                    // ningún sitio, así que hace falta la clave Y el módulo.
                    if (_cfg.Barra.Bandeja && HayModuloBandeja()) Bandeja.Encender();
                    VigilarConfig();
                    Uso.Cargar();
                    _ = Fuentes.CargarApps();   // el índice del buscador se arma por detrás
                    MontarTeclaWindows();
                    if (_cfg.Barra.Notificaciones && Notificaciones.Disponible) Notificaciones.Arrancar();
                }
                VigilarWallpaperEngine();
                MontarPulsoAudio();
                _tick.Tick += (_, _) => Tick();
                _tick.Start();
                VigilarPantallaCompleta();
                await ActualizarWallpaper(true);
            }
            catch (Exception ex) { Log.Error("arranque", ex); }

            await Task.Delay(2000);
            if (Energia.Simulando)
                Log.Info("ATENCIÓN: modo simulación de energía ACTIVO (NUBEBAR_SIMULAR_ENERGIA=1): " +
                         "bloquear, suspender, reiniciar y apagar solo se apuntan en el log");
            Log.Info($"Isla[{_monitor.Dispositivo}{(_esJefa ? " jefa" : "")}] " +
                     $"ventana L={Left} T={Top} {ActualWidth}x{ActualHeight} visible={IsVisible} | " +
                     $"isla {Isla.ActualWidth:0}x{Isla.ActualHeight:0} opacidad={Isla.Opacity:0.##} | " +
                     $"monitor ({_monitor.X},{_monitor.Y}) {_monitor.Ancho}x{_monitor.Alto} dpi={_monitor.Dpi} | " +
                     $"MaxApps={MaxAppsEfectivo} | {ResumenDelPanel()} | " +
                     $"Wallpaper Engine={WallpaperEngine.Exe ?? "no encontrado"}");
        };
        Closed += (_, _) =>
        {
            // Primero callar a todo lo que podría volver a esconder la barra
            _cerrando = true;
            _tick.Stop();
            PararPulsoAudio();
            _recarga.Stop();
            _wpTimer.Stop();
            if (_watcher is { } w) w.EnableRaisingEvents = false;
            if (_wpWatcher is { } ww) ww.EnableRaisingEvents = false;

            Islas.Remove(this);
            AppBar.Quitar(_self);                      // cada isla suelta SU reserva de espacio
            if (!_esJefa) return;                      // lo global lo desmonta solo la jefa

            Nativo.QuitarAtajo(_self, IdAtajoVista);   // el atajo vuelve a quien lo quiera
            _gancho?.Dispose();                        // y la tecla Windows vuelve a la normalidad
            Bandeja.Apagar();                          // los iconos vuelven a la bandeja de Explorer
            Notificaciones.Limpiar();                  // y la copia de su base se borra del temporal
            Guardian.Despedir();                       // cierre limpio: el guardián sobra

            // Se encola detrás de lo que hubiera pendiente y se espera: el proceso no
            // puede terminar hasta que las barras de Windows estén de vuelta
            if (!EncolarShell(() => Nativo.MostrarBarraWindows(true)).Wait(TimeSpan.FromSeconds(5)))
            {
                Log.Info("la cola del shell no terminó a tiempo: restaurando las barras aquí mismo");
                Nativo.MostrarBarraWindows(true);
            }
            Log.Info($"cerrado. Barra de Windows visible={Nativo.BarraWindowsVisible()}");
        };
    }

    // Encadena el trabajo con el shell para que se ejecute en orden y nunca a la vez
    Task EncolarShell(Action accion)
    {
        lock (_candadoShell)
            return _colaShell = _colaShell.ContinueWith(_ =>
            {
                try { accion(); }
                catch (Exception ex) { Log.Error("barra de Windows", ex); }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    void Tick()
    {
        // Explorer a veces vuelve a mostrar su barra: se vuelve a esconder
        // Nativo.Cpu() mide la diferencia con la lectura anterior: si lo llamasen dos
        // islas en el mismo segundo, las cifras saldrían disparatadas. Lo de una sola
        // vez por tick (y el trato con las barras de Windows) es cosa de la jefa.
        if (_esJefa)
        {
            // Lo de esconder la barra ya NO va aquí: con un tick de un segundo,
            // cuando algo la volvía a mostrar (una captura de pantalla, por
            // ejemplo) se quedaba a la vista hasta un segundo entero y se veía
            // el parpadeo. Ahora lo hace el vigilante de 150 ms.
            Historial.Anotar(Nativo.Cpu(), Nativo.Ram());
        }
        if (_esJefa) VigilarNotificaciones();
        VigilarVuelta();
        RefrescarMusica();
        VigilarPulsoAudio();   // si ha empezado a sonar algo, despierta al pulso
        // Cada actualizador con su red: uno que falle no debe llevarse por delante el
        // resto del tick (el ancho de la isla, el vigilante del foco, el menú...)
        foreach (var a in _actualizadores.ToArray())
        {
            try { a(); }
            catch (Exception ex) { Log.Error("refrescar un módulo", ex); }
        }
        AjustarAncho();   // el texto de CPU/RAM/hora cambia de ancho: que se deslice
        VigilarFocoDelMenu();
        // Lo del menú solo se refresca si el menú está delante
        if (_panelAbierto == PanelInicio)
            foreach (var a in _actualizadoresMenu.ToArray())
            {
                try { a(); } catch (Exception ex) { Log.Error("refrescar el menú", ex); }
            }
        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") == "1")
            Log.Info($"DIAG tick [{_monitor.Dispositivo}] isla={Isla.ActualWidth:0.00} exp={_expandida} " +
                     $"oculta={_recogida} asomada={_asomada} | {ResumenGrupos()}");
    }

    // ================= Tema y posición =================
    // La bandera _aplicando bloquea el ajuste de ancho mientras se monta todo. Si una
    // excepción se escapaba por el medio, se quedaba puesta para siempre y la isla ya
    // no volvía a ajustar su ancho en toda la sesión. Por eso el try/finally.
    void Aplicar()
    {
        if (_cerrando) return;
        _aplicando = true;
        try { AplicarTema(); }
        finally { _aplicando = false; }
    }

    void AplicarTema()
    {
        var t = _cfg.Tema;
        var b = _cfg.Barra;
        WallpaperEngine.RutaManual = _cfg.Wallpaper.Ruta;

        // Antes de montar nada: el nivel de animación lo leen Kf y Ms, por los que
        // pasa todo lo que se mueve, así que tiene que estar puesto ya.
        Animacion.Elegir(_cfg.Animaciones.Nivel);

        // Y con él, el estilo de los botones, que también depende del nivel. Va aquí y
        // no en el constructor porque los botones se vuelven a crear unas líneas más
        // abajo (LlenarZonas): así cambiar el nivel en caliente se ve en el acto.
        Resources[typeof(Button)] = EstiloBoton();

        Color fondo = Hex(t.Fondo, Colors.Black), texto = Hex(t.Texto, Colors.White), acento = Hex(t.Acento, Colors.CornflowerBlue);
        if (t.DesdeWallpaper && _paleta is { } p)
        {
            fondo = Color.FromArgb(fondo.A, p.Fondo.R, p.Fondo.G, p.Fondo.B);  // se respeta tu opacidad
            texto = p.Texto;
            acento = p.Acento;
        }
        else if (t.EnviarColorAlWallpaper && _esJefa) EnviarColor(acento);   // un solo emisor, no dos peleándose

        // El acento es TINTA: pinta texto, filos y gráficas. Si viene con alfa
        // (un "#80FFFFFF", por ejemplo), todo eso se dibuja a medias y sale gris:
        // el reloj del menú, los porcentajes y el filo de la isla parecen
        // apagados y da la sensación de que la isla no tiene color. La
        // transparencia de la isla la pone Tema.Fondo, no el acento.
        if (acento.A != 0xFF)
        {
            Log.Info($"el acento {acento} viene con transparencia: la tinta se pinta opaca " +
                     "(la transparencia de la isla es cosa de Tema.Fondo)");
            acento = Color.FromRgb(acento.R, acento.G, acento.B);
        }

        // Y un acento que es el mismo gris que el texto no es un acento: no hay
        // nada que distinguir, así que se vuelve al de por defecto y se avisa.
        if (Contraste.Saturacion(acento) < 0.08 && Contraste.Entre(acento, texto) < 1.5)
        {
            var porDefecto = Color.FromRgb(0x7A, 0xA2, 0xF7);
            Log.Info($"el acento {acento} es un gris igual que el texto {texto}: no se distinguiría " +
                     $"de nada, se usa {porDefecto} (cámbialo en Ajustes > Colores)");
            acento = porDefecto;
        }

        // El acento para TEXTO se corrige si no se lee sobre el fondo de la isla.
        // Un acento oscuro sobre un fondo negro dejaba, por ejemplo, el nombre de
        // la ventana activa en la vista general en negro sobre negro: invisible.
        // Se conserva el tono y solo se acerca al blanco lo justo.
        var fondoOpaco = Color.FromRgb(fondo.R, fondo.G, fondo.B);
        _cAcentoLegible = Contraste.Legible(acento, fondoOpaco);
        if (_cAcentoLegible != acento)
            Log.Info($"el acento {acento} no contrasta con el fondo {fondoOpaco} " +
                     $"(ratio {Contraste.Entre(acento, fondoOpaco):0.0}); para texto se usa {_cAcentoLegible}");

        // Una línea con lo que de verdad se pinta: si alguna vez "no cambia de
        // color", esto dice en un vistazo qué acento se está usando y de dónde sale
        if (_esJefa && (_cAcento != acento || _cTexto != texto))
            Log.Info($"tema aplicado: fondo={fondo} texto={texto} acento={acento} " +
                     $"(origen: {(t.DesdeWallpaper && _paleta != null ? "wallpaper" : "config")})");

        _cTexto = texto; _cAcento = acento;

        // Los roles se recalculan con los colores YA resueltos (con el wallpaper
        // aplicado y el acento corregido), no con los del config en bruto: un widget
        // que pregunte por Roles.Superficie tiene que recibir algo que pegue con lo
        // que está pintado de verdad.
        _roles = Roles.Desde(t, fondo, texto, acento);

        AnimarColor(_fondo, fondo);
        AnimarColor(_texto, texto);
        AnimarColor(_acento, acento);
        AnimarColor(_acentoLegible, _cAcentoLegible);

        // El hover es, conceptualmente, la superficie elevada: lo que hay bajo el
        // ratón. Antes era un 0x30 del texto escrito aquí a mano; ahora lo decide el
        // tema, así que Nube puede tener un hover azulado en vez de un gris del texto.
        AnimarColor(_hover, _roles.SuperficieAlta);

        AnimarColor(_superficie, _roles.Superficie);
        AnimarColor(_elegido, _roles.Seleccion);
        AnimarColor(_superficieAlta, _roles.SuperficieAlta);
        AnimarColor(_borde, _roles.Borde);
        AnimarColor(_textoAtenuado, _roles.TextoAtenuado);
        AnimarColor(_secundario, _roles.SecundarioTinta);
        AnimarColor(_sistema, _roles.SistemaTinta);

        Isla.CornerRadius = new CornerRadius(t.Radio);
        FontFamily = new FontFamily(t.Fuente);
        FontSize = t.TamanoFuente;

        // Filo y brillo de acento: es lo que hace que el color del wallpaper se note,
        // porque si no el acento solo pinta la rayita de 3 px bajo la app activa
        Isla.BorderBrush = _acento;
        Isla.BorderThickness = new Thickness(t.BordeAcento ? 1 : 0);
        AjustarBrillo(t.BordeAcento, acento);

        // Montar las zonas es más que rellenarlas: ver RemontarZonas, que es el ÚNICO
        // sitio desde el que se hace. El modo edición llamaba antes a LlenarZonas a
        // secas y se saltaba este preámbulo, lo que hacía desaparecer las fijadas.
        RemontarZonas();

        // Primero el diseño de la isla y después el tamaño de la ventana: asignar
        // Window.Width/Height hace un SetWindowPos que bombea mensajes y provoca un
        // layout intermedio. Si el diseño ya está puesto, ese layout ya es el definitivo
        // y no se llega a dibujar la isla estirada de lado a lado.
        // Recargar el config con un panel abierto lo cierra: sus medidas ya no valen
        CerrarPanelAlRecargar();

        Contenido.Height = b.Alto;          // la fila de los módulos no cambia aunque crezca el cajón
        Isla.BeginAnimation(HeightProperty, null);
        Isla.Height = b.Alto;
        Isla.BeginAnimation(WidthProperty, null);
        Isla.HorizontalAlignment = AlineacionDeLaIsla();
        ColocarCajon();                     // alineación vertical, márgenes y orden de las filas

        _expandida = false;
        FijarLados(!EsIsla);
        // En modo isla el ancho siempre va fijado a mano (ver AnimarAncho); en modo barra, automático
        Isla.Width = EsIsla ? MedirAncho() : double.NaN;

        // La ventana es una franja transparente de todo el ancho de SU monitor
        double altoVentana = b.Alto + b.Margen * 2;
        ColocarEnMonitor(altoVentana);

        // Solo el modo barra reserva espacio; la isla flota encima de las ventanas, como en el iPhone.
        // Y una barra que se autooculta tampoco lo reserva: reservar un hueco para algo
        // que no está es justo lo que la gente quiere evitar al encender el autoocultar
        // (en Plasma pasa lo mismo).
        // Esto va en el hilo de la ventana porque la AppBar se registra con nuestro propio hwnd.
        if (!EsIsla && b.ReservarEspacio && !PanelAutoOculta)
            AppBar.Registrar(_self, Arriba, (int)Math.Round(altoVentana * _monitor.Escala),
                             _monitor.X, _monitor.Y, _monitor.Ancho, _monitor.Alto);
        else AppBar.Quitar(_self);

        // Esconder la barra de Windows tarda ~0,7 s: Explorer recalcula el área de trabajo y
        // se lo notifica a todas las ventanas. Si se hace aquí, el hilo de interfaz se queda
        // clavado y la animación de entrada se congela a medias. Va en la cola del shell:
        // en otro hilo, pero en orden y sin pisarse con el "mostrar" del cierre.
        bool mostrar = !b.OcultarBarraWindows;
        var conIsla = MonitoresConIsla();
        if (!_cerrando) EncolarShell(() => Nativo.MostrarBarraWindows(mostrar, conIsla));

        AplicarAtajo();   // por si has cambiado AtajoVistaGeneral en caliente
        if (_gancho != null)
        {
            _gancho.Activo = _cfg.Inicio.TeclaWindows;
            _gancho.Volumen = _cfg.Barra.TeclasVolumen;
        }

        // Lo último: aquí arriba la isla se ha dejado recogida. Si se está editando,
        // hay que volver a abrirla, o cada acción del modo edición (que guarda, y
        // guardar recarga) cerraría la isla en las narices de quien está editando.
        RestaurarEdicion();

        // Y la visibilidad del panel: orden Z y autoocultar. Va al final porque
        // depende de que la ventana ya esté colocada y la bandeja montada.
        AplicarVisibilidad();
    }

    int Px(double dip) =>
        (int)Math.Round(dip * (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1.0));

    static Color Hex(string hex, Color porDefecto)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return porDefecto; }
    }

    // ================= Animaciones =================
    // Regla: el valor "real" de cada propiedad es siempre el estado final, y la animación
    // solo dibuja el camino hasta él. Si una animación fallase, todo se vería igual, sin saltos.

    // Animación con espera inicial: se queda en "desde" durante "retardo" y luego va a "hasta"
    //
    // Por aquí pasan casi todas las animaciones de números de la isla, así que es el
    // sitio donde se aplica el nivel de animación (ver Animacion.cs). Con el nivel en
    // "reducido" el factor es 0 y se devuelve una animación de un solo fotograma en el
    // valor final: nada se mueve, pero nadie se queda a medias.
    static DoubleAnimationUsingKeyFrames Kf(double desde, double hasta, double ms, double retardo = 0, IEasingFunction? curva = null)
    {
        var a = new DoubleAnimationUsingKeyFrames();

        if (!Animacion.Hay)
        {
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(hasta, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            return a;
        }

        ms = Animacion.Escalar(ms);
        retardo = Animacion.Escalar(retardo);

        a.KeyFrames.Add(new DiscreteDoubleKeyFrame(desde, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (retardo > 0) a.KeyFrames.Add(new DiscreteDoubleKeyFrame(desde, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(retardo))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(hasta, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(retardo + ms)), curva));
        return a;
    }

    // El brillo vive apagado cuando la isla está recogida y sube un poco al expandirse.
    // Se intensifica con la opacidad y no con el radio: cambiar el radio obliga a
    // recalcular el núcleo del desenfoque en cada fotograma, y la opacidad no.
    const double BrilloRecogida = 0.30, BrilloAbierta = 0.60, BrilloRadio = 8;

    void AjustarBrillo(bool activo, Color color)
    {
        if (!activo)
        {
            Isla.Effect = null;
            _brillo = null;
            return;
        }
        if (_brillo == null)
        {
            _brillo = new DropShadowEffect
            {
                ShadowDepth = 0,
                BlurRadius = BrilloRadio,
                Opacity = _expandida ? BrilloAbierta : BrilloRecogida
            };
            Isla.Effect = _brillo;
        }
        var actual = _brillo.Color;
        _brillo.BeginAnimation(DropShadowEffect.ColorProperty, null);
        _brillo.Color = color;
        _brillo.BeginAnimation(DropShadowEffect.ColorProperty,
                               new ColorAnimation(actual, color, Ms(700)) { EasingFunction = Suave });
    }

    void AnimarBrillo(bool abrir)
    {
        if (_brillo == null) return;
        double ahora = _brillo.Opacity;
        double hasta = abrir ? BrilloAbierta : BrilloRecogida;
        _brillo.BeginAnimation(DropShadowEffect.OpacityProperty, null);
        _brillo.Opacity = hasta;
        _brillo.BeginAnimation(DropShadowEffect.OpacityProperty, Kf(ahora, hasta, abrir ? 420 : 320, 0, Suave));
    }

    static void AnimarColor(SolidColorBrush pincel, Color destino, double ms = 700)
    {
        var actual = pincel.Color;                       // color que se ve ahora mismo
        pincel.BeginAnimation(SolidColorBrush.ColorProperty, null);
        pincel.Color = destino;                          // valor final real
        pincel.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(actual, destino, Ms(ms)) { EasingFunction = Suave });
    }

    // Al arrancar la isla cae desde fuera de la pantalla con rebote
    void AnimarAparicion()
    {
        double distancia = _cfg.Barra.Alto + _cfg.Barra.Margen * 2;
        var mover = new TranslateTransform();            // base Y = 0 (posición final)
        Isla.RenderTransform = mover;
        _mover = mover;
        Isla.BeginAnimation(OpacityProperty, Kf(0, 1, 400));

        // Con el autoocultar encendido su sitio es FUERA de la pantalla, no dentro
        // (lo decide AplicarVisibilidad, que ya ha corrido en Aplicar). Caer con
        // rebote y recogerse acto seguido se vería como un tic, y además la caída
        // dejaría _mover.Y en 0 y la isla se quedaría a la vista marcada como
        // recogida: entonces no volvería a esconderse hasta el siguiente cambio.
        if (_recogida && !_asomada) { mover.Y = Arriba ? -distancia : distancia; return; }

        mover.BeginAnimation(TranslateTransform.YProperty, Kf(Arriba ? -distancia : distancia, 0, 700, 0, Muelle));
    }

    // Ancho natural del contenido, medido sin límite para que las columnas "*" del
    // Grid no se lleven todo el hueco disponible. Se deja el layout invalidado para
    // que la pasada real vuelva a medir con su propia restricción.
    double MedirAncho()
    {
        var sinLimite = new Size(double.PositiveInfinity, double.PositiveInfinity);

        // El Grid de dentro tiene columnas *, Auto, *. Midiendo con ancho infinito las
        // dos estrellas se dimensionan a su contenido, pero al colocar se reparten el
        // hueco a partes IGUALES: si un lado tiene más cosas que el otro, se recorta.
        // Por eso el ancho bueno es 2 × el lado más ancho + el centro, y no la suma.
        Izq.Measure(sinLimite);
        Cen.Measure(sinLimite);
        Der.Measure(sinLimite);
        double lados = Math.Max(Izq.DesiredSize.Width, Der.DesiredSize.Width);
        double ancho = lados * 2 + Cen.DesiredSize.Width
                       + Isla.Padding.Left + Isla.Padding.Right
                       + Isla.BorderThickness.Left + Isla.BorderThickness.Right;

        Contenido.InvalidateMeasure();
        double maximo = ActualWidth > 0 ? ActualWidth : _monitor.Ancho / _monitor.Escala;
        return Math.Clamp(ancho, Isla.MinWidth, Math.Max(Isla.MinWidth, maximo));
    }

    // El ancho real queda siempre fijado al valor final y la animación solo dibuja el
    // camino: así nunca hay un salto al terminar (antes volvía a "auto" y pegaba un tirón).
    void AnimarAncho(double hasta, double ms, IEasingFunction curva, Action? alTerminar = null)
    {
        double desde = Isla.ActualWidth > 0 ? Isla.ActualWidth : hasta;
        Isla.BeginAnimation(WidthProperty, null);
        Isla.Width = hasta;

        if (Math.Abs(desde - hasta) < 0.5) { alTerminar?.Invoke(); return; }

        var ancho = Kf(desde, hasta, ms, 0, curva);
        if (alTerminar != null) ancho.Completed += (_, _) => alTerminar();
        Isla.BeginAnimation(WidthProperty, ancho);
    }

    // El contenido cambia solo (minuto, CPU, RAM, apps que abres o cierras): la isla
    // se adapta deslizándose en vez de dar el salto de un fotograma
    void AjustarAncho()
    {
        if (!EsIsla || _aplicando || _animandoAncho) return;
        double hasta = Math.Max(MedirAncho(), _panelAncho);   // con panel abierto manda el panel
        if (Math.Abs(hasta - Isla.ActualWidth) < 0.5) return;
        AnimarAncho(hasta, 280, Suave);
    }

    // Expandir / contraer la isla: el ancho se anima con muelle y los laterales
    // aparecen con fundido y escalonados, de dentro hacia fuera
    // Ancho que tendría la isla con los módulos de los lados a la vista, los
    // tenga ahora o no. Hace falta para decidir si un panel cabe sin desplegarla.
    double AnchoConModulos()
    {
        var antes = Izq.Visibility;
        Izq.Visibility = Der.Visibility = Visibility.Visible;
        double w = MedirAncho();
        Izq.Visibility = Der.Visibility = antes;
        return w;
    }

    void Expandir(bool abrir, bool forzar = false)
    {
        if (!EsIsla || abrir == _expandida) return;
        // Editando, la isla se queda abierta pase lo que pase: si se recogiera a
        // mitad de un arrastre no habría dónde soltar el widget. Solo Editar(false),
        // que pasa forzar, puede cerrarla.
        if (!abrir && EdicionAnclada && !forzar) return;
        if (abrir && _sinExpandir) return;     // la tienes cogida: no se abre mientras la llevas
        // Con un panel estrecho abierto, volver a pasar el ratón NO la re-expande:
        // la isla crecería y el panel se quedaría descuadrado dentro, con el
        // resaltado de cada fila a todo lo ancho y el texto en medio.
        if (abrir && _panelEncogido) return;
        // Con un panel abierto la isla se queda desplegada, salvo que el propio
        // panel pida lo contrario porque es estrecho y la quiere recogida
        if (!abrir && HayPanel && !forzar) return;
        _expandida = abrir;

        // Medir el destino con la visibilidad que tendrá al final...
        Izq.Visibility = Der.Visibility = abrir ? Visibility.Visible : Visibility.Collapsed;
        double hasta = MedirAncho();
        // ...pero dejarlos visibles mientras dura la animación
        Izq.Visibility = Der.Visibility = Visibility.Visible;

        MedirFotogramas($"{(abrir ? "expandir" : "contraer")} isla (brillo={_brillo != null})", 700);
        _animandoAncho = true;
        // Recogerse con Comoda y no con Suave, por lo mismo que al cerrar un panel:
        // con EaseOut la isla se estrechaba casi del todo en los primeros 90 ms y el
        // resto de la animación no se veía, así que parecía un recorte
        AnimarAncho(hasta, abrir ? 480 : 340, abrir ? Muelle : Comoda, () =>
        {
            _animandoAncho = false;
            if (_expandida != abrir) return;   // la interrumpió otra animación
            if (!abrir) Izq.Visibility = Der.Visibility = Visibility.Collapsed;
        });

        foreach (var lado in new Panel[] { Izq, Der })
        {
            double ahora = lado.Opacity;
            lado.BeginAnimation(OpacityProperty, null);
            lado.Opacity = abrir ? 1 : 0;                // valor final real
            // Y los módulos se van en 220 ms, no en 120: desaparecían antes de que la
            // isla hubiera empezado a estrecharse de verdad
            lado.BeginAnimation(OpacityProperty, abrir ? Kf(0, 1, 260, 140) : Kf(ahora, 0, 220, 0, Comoda));
            Escalonar(lado, abrir);
        }

        AnimarBrillo(abrir);
    }

    // Cada módulo del lateral entra un poco desplazado hacia fuera y con retardo
    // creciente: es lo que le da el aire de la Dynamic Island al abrirse
    void Escalonar(Panel lado, bool abrir)
    {
        var hijos = lado.Children.OfType<UIElement>().ToList();
        double signo = lado == Izq ? -1 : 1;

        for (int i = 0; i < hijos.Count; i++)
        {
            var hijo = hijos[i];
            int orden = lado == Izq ? hijos.Count - 1 - i : i;   // del centro hacia fuera
            if (hijo.RenderTransform is not TranslateTransform mover)
                hijo.RenderTransform = mover = new TranslateTransform();

            double desde = abrir ? signo * 18 : mover.X;
            mover.BeginAnimation(TranslateTransform.XProperty, null);
            mover.X = 0;                                         // valor final real
            if (!abrir) continue;                                // al cerrar basta el fundido
            mover.BeginAnimation(TranslateTransform.XProperty, Kf(desde, 0, 420, 120 + orden * 30, Muelle));
        }
    }

    void FijarLados(bool visibles)
    {
        foreach (var lado in new Panel[] { Izq, Der })
        {
            lado.BeginAnimation(OpacityProperty, null);
            lado.Opacity = visibles ? 1 : 0;
            lado.Visibility = visibles ? Visibility.Visible : Visibility.Collapsed;
            foreach (var hijo in lado.Children.OfType<UIElement>())
            {
                if (hijo.RenderTransform is not TranslateTransform mover) continue;
                mover.BeginAnimation(TranslateTransform.XProperty, null);
                mover.X = 0;
            }
        }
    }

    // Iconos de apps: crecen desde 0 al abrirse y se encogen al cerrarse.
    // Con RenderTransform (y no LayoutTransform) la escala va en la composición y no
    // obliga a un measure+arrange por fotograma; el hueco lo abre y lo cierra la
    // animación de ancho de la isla, que es la que se ve.
    static void AnimarEntrada(FrameworkElement e, double retardoMs = 0)
    {
        var escala = new ScaleTransform(1, 1);           // base: tamaño normal
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = escala;
        escala.BeginAnimation(ScaleTransform.ScaleXProperty, Kf(0, 1, 450, retardoMs, Muelle));
        escala.BeginAnimation(ScaleTransform.ScaleYProperty, Kf(0, 1, 450, retardoMs, Muelle));
        e.BeginAnimation(OpacityProperty, Kf(0, 1, 250, retardoMs));
    }

    static void AnimarSalida(FrameworkElement e, Action alTerminar)
    {
        e.IsHitTestVisible = false;
        var escala = new ScaleTransform(1, 1);
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = escala;
        var encoger = Kf(1, 0, 220, 0, Suave);
        encoger.Completed += (_, _) => alTerminar();
        escala.BeginAnimation(ScaleTransform.ScaleXProperty, encoger);
        escala.BeginAnimation(ScaleTransform.ScaleYProperty, Kf(1, 0, 220, 0, Suave));
        e.BeginAnimation(OpacityProperty, Kf(1, 0, 150));
    }

    static void CambiarGlifo(TextBlock tb, string nuevo)
    {
        tb.Text = nuevo;
        tb.BeginAnimation(OpacityProperty, Kf(0, 1, 220));
    }

    // ================= Recarga en caliente =================
    void VigilarConfig()
    {
        _watcher = new FileSystemWatcher(Config.Carpeta, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) => ProgramarRecarga();
        _watcher.Created += (_, _) => ProgramarRecarga();
        _watcher.Renamed += (_, _) => ProgramarRecarga();

        _recarga.Tick += (_, _) =>
        {
            _recarga.Stop();
            RecargarTodas();
        };
    }

    // El config lo vigila solo la jefa, pero el cambio lo tienen que ver todas las islas.
    // Si además has tocado "Monitores", puede que ahora sobren o falten islas.
    static void RecargarTodas()
    {
        if (Config.Cargar() is not { } nueva) return;
        string antes = Islas.FirstOrDefault()?._cfg.Barra.Monitores ?? "todos";
        foreach (var i in Islas.ToList()) i._cfg = nueva;

        if (!antes.Equals(nueva.Barra.Monitores, StringComparison.OrdinalIgnoreCase)) Reorganizar();
        else foreach (var i in Islas.ToList()) i.Aplicar();
    }

    void ProgramarRecarga() => Dispatcher.InvokeAsync(() => { _recarga.Stop(); _recarga.Start(); });

    // ================= Wallpaper Engine =================
    void VigilarWallpaperEngine()
    {
        _wpTimer.Tick += async (_, _) => await ActualizarWallpaper();
        _wpTimer.Start();

        if (WallpaperEngine.Carpeta is { } dir)
        {
            _wpWatcher = new FileSystemWatcher(dir, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _wpWatcher.Changed += (_, _) => Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(500);
                await ActualizarWallpaper();
            });
        }
    }

    async Task ActualizarWallpaper(bool forzar = false)
    {
        if (_wpOcupado || (!forzar && DateTime.Now - _wpUltimo < TimeSpan.FromSeconds(2))) return;
        _wpOcupado = true;
        _wpUltimo = DateTime.Now;
        try
        {
            var ruta = await WallpaperEngine.WallpaperActual(IndiceWallpaper);
            if (ruta == null || ruta == _wpRuta) return;

            _wpRuta = ruta;
            _wpTitulo = WallpaperEngine.Titulo(ruta);
            var prev = WallpaperEngine.Preview(ruta);
            _paleta = prev is not null ? Extractor.DesdeImagen(prev) : null;
            _ultimoColorEnviado = "";
            Log.Info($"wallpaper: \"{_wpTitulo}\" | {ruta} | preview={prev ?? "ninguna"} | " +
                     $"paleta={(_paleta is { } pl ? $"fondo={pl.Fondo} texto={pl.Texto} acento={pl.Acento}" : "no extraída")}");

            if (_cfg.Tema.DesdeWallpaper || _cfg.Tema.EnviarColorAlWallpaper) Aplicar();
            else Tick();
        }
        finally { _wpOcupado = false; }
    }

    void EnviarColor(Color col)
    {
        var rgb = string.Format(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###}",
                                col.R / 255.0, col.G / 255.0, col.B / 255.0);
        if (rgb == _ultimoColorEnviado) return;
        _ultimoColorEnviado = rgb;
        WallpaperEngine.Enviar("applyProperties", "-properties", $"RAW~({{\"schemecolor\":\"{rgb}\"}})~END");
    }

    UIElement ModuloWallpaper()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var play = Glifo(_wpPausado ? "\uE768" : "\uE769");
        panel.Children.Add(Boton(play, "Pausar / reanudar wallpaper", () =>
        {
            _wpPausado = !_wpPausado;
            WallpaperEngine.Enviar(_wpPausado ? "pause" : "play");
            CambiarGlifo(play, _wpPausado ? "\uE768" : "\uE769");
        }));

        panel.Children.Add(Boton(Glifo("\uE893"), "Siguiente wallpaper (playlist)", () =>
        {
            WallpaperEngine.Enviar("nextWallpaper");
            Dispatcher.InvokeAsync(async () => { await Task.Delay(1500); await ActualizarWallpaper(true); });
        }));

        var sonido = Glifo(_wpMuteado ? "\uE74F" : "\uE767");
        panel.Children.Add(Boton(sonido, "Silenciar / activar sonido", () =>
        {
            _wpMuteado = !_wpMuteado;
            WallpaperEngine.Enviar(_wpMuteado ? "mute" : "unmute");
            CambiarGlifo(sonido, _wpMuteado ? "\uE74F" : "\uE767");
        }));

        return panel;
    }

    // ================= Ratón =================
    void Isla_MouseEnter(object s, MouseEventArgs e) => Expandir(true);
    void Isla_MouseLeave(object s, MouseEventArgs e) { CerrarMiniLista(); Expandir(false); }
    // El clic derecho ya no cierra de golpe: antes había que acertar y no había
    // vuelta atrás. Ahora saca un menú con el mismo aire que la isla.
    void Isla_Salir(object s, MouseButtonEventArgs e)
    {
        e.Handled = true;
        AbrirMenuContextual();
    }

    const string PanelMenu = "contextual";

    // Se usa el mismo cajón que los demás paneles en vez de un ContextMenu de
    // Windows: así sale de la isla, con sus colores y su animación, en lugar de
    // una cajita blanca del sistema que no pega con nada.
    void AbrirMenuContextual()
    {
        if (_panelAbierto == PanelMenu) { CerrarPanel(); return; }

        const double ancho = 250, altoFila = 34;
        var opciones = new List<(string Texto, Action Accion)>();
        opciones.AddRange(OpcionesDeCerrarTodas());
        // Solo si la tecla Windows es nuestra: es lo único que la trae de vuelta
        if (_cfg.Inicio.TeclaWindows) opciones.Add(("Esconder la isla", () => EsconderAMano(true)));
        opciones.Add((_editando ? "Salir de la edición" : "Editar la nube", AlternarEdicion));
        opciones.Add(("Ajustes…", AbrirAjustes));
        opciones.Add(("Recargar el config", () =>
        {
            RecargarTodas(); Log.Info("config recargado a mano");
        }));
        opciones.Add(("Salir de NubeBar", Close));

        AbrirPanel(PanelMenu, CrearMenuContextual(opciones.ToArray(), ancho, altoFila),
                   ancho, opciones.Count * altoFila + 12, encoger: true);
    }

    FrameworkElement CrearMenuContextual((string Texto, Action Accion)[] opciones, double ancho, double altoFila)
    {
        // Nada de anchos fijos aquí. La isla tiene su Padding y su filo, así que
        // una pila de "ancho" exacto se sale por los lados y el recorte del cajón
        // se come el principio de cada texto. Estirándose, el ancho lo decide la
        // isla y todo queda alineado.
        var pila = new StackPanel
        {
            Margin = new Thickness(0, 4, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        // La plantilla de los botones centra su contenido, así que para que todas
        // las opciones empiecen en la misma columna se les da a todas el ancho de
        // la más larga. Medido, no a ojo: así se adapta al tema y a la tipografía.
        double anchoTexto = opciones.Length == 0 ? 0 : opciones.Max(o => AnchoDeTexto(o.Texto));

        foreach (var (texto, accion) in opciones)
        {
            var etiqueta = new TextBlock
            {
                Text = texto,
                Foreground = _texto,
                Width = anchoTexto,
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            // Sin tooltip: el texto ya está delante y el globo de Windows salía
            // tapando la propia opción que estabas leyendo
            var b = Boton(etiqueta, null!, () => { CerrarPanel(); accion(); });
            b.ToolTip = null;
            b.Height = altoFila - 2;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Padding = new Thickness(12, 0, 12, 0);
            pila.Children.Add(b);
        }

        bool tocado = false;
        pila.MouseEnter += (_, _) => tocado = true;
        pila.MouseLeave += (_, _) => { if (tocado && _panelAbierto == PanelMenu) CerrarPanel(); };
        return pila;
    }

    public void AbrirAjustes()
    {
        if (VentanaAjustes.Abierta is { } ya) { ya.Activate(); return; }
        try
        {
            var v = new VentanaAjustes();
            v.Show();
            v.Activate();
        }
        catch (Exception ex) { Log.Error("abrir ajustes", ex); }
    }

    void Isla_MouseDown(object s, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        try { Process.Start(new ProcessStartInfo(Config.Ruta) { UseShellExecute = true }); }
        catch { Process.Start("notepad.exe", Config.Ruta); }
    }

    // ================= Módulos =================
    // El switch de dieciséis casos que había aquí se ha mudado a
    // MainWindow.Widgets.cs, donde cada caso es ahora el cuerpo de un widget
    // registrado. Las expresiones son las mismas, una a una; lo que cambia es que el
    // reloj de casa y un widget de fuera se montan por la misma puerta (CrearWidget),
    // y que un widget que reviente ya no se lleva la isla por delante.
    //
    // "muestra", en las etiquetas, reserva el ancho del valor más largo para que la
    // isla no respire al pasar de "CPU 8%" a "CPU 11%" cada segundo.

    // Un nombre de módulo que esta versión no conoce. Pasa de verdad: el config se
    // migra solo y le añade módulos nuevos, así que un exe viejo se encuentra con
    // "fijadas" puesto en el centro y antes lo escribía TAL CUAL, con lo que se leía
    // la palabra "fijadas" suelta entre los iconos de la isla. Se apunta una vez en
    // el log y se deja el hueco vacío, que es lo que menos estorba.
    static readonly HashSet<string> _modulosDesconocidos = new(StringComparer.OrdinalIgnoreCase);

    static UIElement ModuloDesconocido(string nombre)
    {
        if (_modulosDesconocidos.Add(nombre))
            Log.Info($"módulo \"{nombre}\" desconocido en esta versión: se ignora " +
                     "(el config lo habrá escrito una NubeBar más nueva)");
        return new Border { Visibility = Visibility.Collapsed };
    }


    // "muestra" = el texto más largo que llegará a mostrar este módulo. Se reserva su
    // ancho para que el contenido cambiante no haga respirar a la isla cada segundo.
    TextBlock Etiqueta(Func<string> valor, bool animar = false, string? muestra = null)
    {
        var tb = new TextBlock
        {
            Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        // Cifras de ancho fijo: si no, un "1" ocupa menos que un "8" y el texto baila
        Typography.SetNumeralAlignment(tb, FontNumeralAlignment.Tabular);
        if (muestra != null) tb.MinWidth = AnchoDeTexto(muestra);

        void Act()
        {
            var nuevo = valor();
            if (nuevo == tb.Text) return;
            bool habia = tb.Text.Length > 0;
            tb.Text = nuevo;
            tb.Visibility = nuevo.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (animar && habia) DeslizarTexto(tb);   // el minuto entra desde abajo
        }
        Act();
        _actualizadores.Add(Act);
        return tb;
    }

    // Ancho que ocuparía ese texto con la fuente y el tamaño actuales del tema
    double AnchoDeTexto(string texto)
    {
        try
        {
            double ppp = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var tipo = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            return new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                     tipo, FontSize, Brushes.Black, ppp).Width;
        }
        catch (Exception ex)
        {
            Log.Error("medir texto", ex);
            return texto.Length * FontSize * 0.6;   // aproximación, mejor eso que nada
        }
    }

    TextBlock Texto(string t, double tamano) => new()
    {
        Text = t, FontSize = tamano, Foreground = _texto,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

    TextBlock Glifo(string codigo) => new()
    {
        Text = codigo, FontFamily = new FontFamily(FuenteIconos), FontSize = 14, Foreground = _texto,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

    // "levantar" = se levanta y crece un 8 % al pasar el ratón. En un botón ancho
    // como una fila entera eso son 15 px por lado y se sale de la isla: esos lo
    // apagan y levantan solo su icono (ver FilaWifi)
    Button Boton(object contenido, string tip, Action accion, bool levantar = true)
    {
        var b = new Button
        {
            Content = contenido, ToolTip = tip, Foreground = _texto,
            Height = Math.Max(20, _cfg.Barra.Alto - 8)
        };
        // Soltar algo que venías arrastrando no es pulsar este botón (ver ClicDeArrastre)
        b.Click += (_, _) => { if (ClicDeArrastre()) return; accion(); Tick(); };
        if (levantar) ConLevantado(b);
        return b;
    }
}
