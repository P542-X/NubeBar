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

    // ---------- Con qué se pinta antes de que haya tema ----------
    //
    // Eran negro, blanco y el azul aciano de WPF, escritos aquí. Duran lo que tarda
    // `AplicarTema` en correr, pero eran cinco colores de la interfaz fuera del catálogo, y
    // ninguno de ellos es de ningún tema: `CornflowerBlue` no lo eligió nadie.
    //
    // Ahora el estado inicial es el tema de serie. Si algo se pinta antes de aplicar el tema
    // —o si aplicar el tema falla— lo que se ve es Profesional, no tres colores de nadie.
    static readonly Roles DeSerie = Roles.PorDefecto();

    // Pinceles fijos: cuando cambia el tema se anima su color, y todo lo que los usa cambia suave
    readonly SolidColorBrush _fondo = new(DeSerie.Fondo), _texto = new(DeSerie.Texto),
                             _acento = new(DeSerie.Acento), _hover = new(DeSerie.SuperficieAlta);
    // El acento "legible" es el mismo color corregido para que se lea sobre el
    // fondo. Se usa en TEXTO; para bordes y barras sigue valiendo el de verdad.
    readonly SolidColorBrush _acentoLegible = new(DeSerie.AcentoTinta);
    Color _cTexto = DeSerie.Texto, _cAcento = DeSerie.Acento, _cAcentoLegible = DeSerie.AcentoTinta;

    // Los colores del tema por rol, para que un widget pueda preguntar "¿de qué color
    // pinto una fila?" sin mezclar a ojo. Se recalcula en AplicarTema.
    Roles _roles = DeSerie;

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

    internal const string FuenteIconos = "Segoe Fluent Icons, Segoe MDL2 Assets";

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
            // ---------- Todo fallo pasa por `Apuntar` ----------
            //
            // Antes cada manejador hacía lo suyo: la interfaz apuntaba y perdonaba, la tarea
            // apuntaba, y el fatal enseñaba la barra de Windows y ya. Ese último dejaba
            // NubeBar a medio morir — con la bandeja secuestrada, los ganchos puestos y la
            // reserva de pantalla sin soltar: barra de Windows visible y NubeBar zombi.
            //
            // Ahora los tres apuntan en el mismo sitio y `Panico` decide. Perdonar un fallo
            // de pintado sigue siendo lo correcto —más vale un módulo tonto que quedarte sin
            // barra—, pero el MISMO fallo cinco veces en veinte segundos ya no se perdona:
            // eso no es un módulo, es una barra que no se puede dibujar.
            Application.Current.DispatcherUnhandledException += (_, e) =>
            {
                Apuntar(Panico.Clase.Interfaz, DondeFallo(e.Exception), e.Exception);
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Apuntar(Panico.Clase.Tarea, DondeFallo(e.Exception), e.Exception);
                e.SetObserved();
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                // Esta mata el proceso quiera uno o no, así que `Panico` la da por grave de
                // entrada: hay que soltar el escritorio ANTES de irnos.
                Apuntar(Panico.Clase.Fondo, DondeFallo(e.ExceptionObject as Exception),
                        e.ExceptionObject);
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
        // ---------- Lo que vive dentro de un panel nunca arrastra la isla ----------
        //
        // Se marca el CAJÓN, que es donde `AbrirPanel` mete el contenido de todos los
        // paneles. Marcarlo una vez aquí cubre audio, el centro de control, red, batería,
        // la vista de ventanas y lo que venga: ninguno tiene que acordarse de protegerse.
        //
        // Marcar cada control por su cuenta era la otra opción y es la que se olvida: el
        // deslizador de volumen no estaba marcado y la nube se iba detrás del ratón.
        SinArrastre(Cajon);
        if (_superpuesta is { } sup) SinArrastre(sup.Cajon);

                MontarArrastre();
                Aplicar();
                if (_esJefa) RegistrarAtajos();
            ApuntarSaludDeHypr();
            }
            catch (Exception ex) { Log.Error("arranque", ex); }

            // Fuera del try: la isla arranca invisible, así que pase lo que pase
            // en Aplicar hay que hacerla aparecer o no se vería nada
            AnimarAparicion();

            // Y aquí ya hay nube que enseñar: la escena de entrada puede levantar la
            // pintura. Lo avisa la JEFA y una sola vez; las demás islas no mandan en
            // esto. Si no había escena, no hace nada (ver EscenaArranque).
            if (_esJefa) EscenaArranque.InterfazLista();

            try
            {
                if (_esJefa)
                {
                    // Un diagnóstico no lleva vigilante: es otro proceso, no ve la bandera de
                    // solo lectura y acabaría reescribiendo el config que la medida no debe
                    // tocar. Y un vigilante que resucite la app a mitad de una medida la
                    // invalida de todas formas.
                    if (!Config.SoloLectura)
                    {
                        Guardian.Lanzar();      // por si me matan: alguien tiene que devolver el shell
                        Vigia.Empezar(Dispatcher);   // y si me cuelgo, que quede la foto
                    }
                    // Encenderla hace que las apps nos manden sus iconos a NOSOTROS en
                    // vez de a Explorer. Si el módulo no está puesto en ninguna zona,
                    // los iconos desaparecerían de la barra de Windows sin aparecer en
                    // ningún sitio, así que hace falta la clave Y el módulo.
                    if (_cfg.Barra.Bandeja && HayModuloBandeja()) Bandeja.Encender();
                    VigilarConfig();
                    Uso.Cargar();
                    _ = Fuentes.CargarApps();   // el índice del buscador se arma por detrás
                    MontarTeclaWindows();
                    // El gestor de ventanas: una instantánea para toda la aplicación, al día
                    // por los eventos de Windows en vez de por un sondeo.
                    GestorVentanas.Arrancar(Dispatcher);
                    // El gestor de fondos: un reloj para toda la aplicación, no uno por
                    // ventana de ajustes. Ver GestorFondos.
                    GestorFondos.Arrancar(Dispatcher);
                    _ = GestorFondos.AplicarAsync(_cfg.Fondo);
                    MontarAjusteDeBordes();
                    if (_cfg.Barra.Notificaciones && Notificaciones.Disponible)
                    {
                        Notificaciones.Arrancar();
                        Centro.Arrancar(_cfg.Barra.NotificacionesSilenciadas, Dispatcher);
                    }
                    // El clima: solo la jefa. Con tres monitores habría tres islas, y cada
                    // una pidiendo el tiempo son tres peticiones por cuarto de hora para
                    // enseñar el mismo número. `Clima` guarda la lectura en estático, así
                    // que los paneles de las otras islas la leen sin pedir nada.
                    ArrancarClima();
                }
                VigilarWallpaperEngine();
                MontarPulsoAudio();
                _tick.Tick += (_, _) => Tick();
                _tick.Start();
                // Y la primera pasada YA, no dentro de un segundo: el reloj espera su primer
                // intervalo antes de saltar, y hasta entonces la isla salía a medias —sin las
                // apps, sin el «ES» del teclado— y se completaba de golpe un segundo después.
                // Todo lo que el tick necesita ya está montado aquí (el gestor de ventanas
                // hace su primera lectura al arrancar).
                Tick();
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

            // El modo diagnóstico: solo la jefa, y después de que todo esté en marcha.
            if (DiagVista.Encendido && _esJefa) await CorrerDiagnosticoDeLaVista();
            if (DiagInicio.Encendido && _esJefa) await CorrerDiagnosticoDeInicio();
            if (DiagBandeja.Encendido && _esJefa) await CorrerDiagnosticoDeLaBandeja();
            if (DiagHypr.Encendido && _esJefa) await CorrerDiagnosticoDeHypr();
            if (DiagMotor.Encendido && _esJefa) await CorrerDiagnosticoDelMotor();
            if (DiagEspacios.Encendido && _esJefa) await CorrerDiagnosticoDeEspacios();
            if (DiagAtajos.Encendido && _esJefa) await CorrerDiagnosticoDeAtajos();
            if (DiagReglas.Encendido && _esJefa) await CorrerDiagnosticoDeReglas();
            if (DiagD6.Encendido && _esJefa) await CorrerDiagnosticoD6();
            if (DiagD7.Encendido && _esJefa) await CorrerDiagnosticoD7();
            if (DiagPrefijos.Encendido && _esJefa) await CorrerDiagnosticoDePrefijos();
            if (ArnesEspacios.Encendido && _esJefa) await CorrerArnesDeEspacios();
            if (ArnesAplicar.Encendido && _esJefa) await CorrerArnesDeAplicar();
            if (ArnesLayout.Encendido && _esJefa) await CorrerArnesDeLayout();
            if (DiagRejilla.Encendido && _esJefa) await CorrerDiagnosticoDeRejilla();
            if (DiagCapturas.Encendido && _esJefa) await CorrerCapturas();
            if (DiagNeon.Encendido && _esJefa) await CorrerDiagnosticoDelNeon();
            if (DiagVigilia.Encendido && _esJefa) await CorrerVigilia();
            if (DiagGuia.Encendido && _esJefa) await CorrerGuiaManual();
            if (DiagE0.Encendido && _esJefa) await CorrerDiagnosticoE0();
            if (DiagModulos.Encendido && _esJefa) await CorrerDiagnosticoModulos();
            if (DiagPaneles.Encendido && _esJefa) await CorrerDiagnosticoDePaneles();
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
            // D7: todo lo de NubeHypr se suelta en un solo sitio, con inventario antes y
            // después. Antes eran dos líneas aquí y el resto repartido; la forma de que una
            // se quedara sin soltar no era escribirla mal, era añadir la octava y no
            // acordarse de este cierre. Ver MainWindow.Salud.cs y HyprSalud.HayQueSoltar.
            SoltarTodoLoDeHypr();
            SoltarElDock();           // E2: y la superficie del dock, que el tablero quede vacío
            SoltarTodosLosLaterales();   // E3: los paneles laterales y sus deslizadores
            DesmontarAjusteDeBordes();                 // antes de Parar(): el reloj del ratón es nuestro
            DejarDeEscucharEscape();                   // y el Esc de los paneles se da de baja
            GestorFondos.Apagar();                     // y el reloj de la presentación se suelta
            GestorVentanas.Parar();                    // y los ganchos de ventanas se sueltan
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
            // Y si el centro de control está abierto, sus dos métricas se repintan solas.
            // Va aquí, en el Tick que ya corre una vez por segundo, en vez de un reloj
            // nuevo: dos relojes para lo mismo es lo que acaba descompasado.
            RefrescarMetricasDelControl();
        }
        if (_esJefa) VigilarNotificaciones();
        // D7: el supervisor de NubeHypr. Cuelga de este reloj y no de uno propio —dos
        // relojes para cosas parecidas es lo que acaba descompasado—, mira una vez cada
        // cinco segundos y solo apunta cuando el estado cambia. Ver MainWindow.Salud.cs.
        VigilarLaSaludDeHypr();
        NubeAlRecuperarse();      // E6: un destello al salir del modo seguro

        // E2: el dock. Se refresca con el pulso de siempre y NO reconstruye nada si lo que
        // dibujaría es idéntico a lo que hay: lo decide su huella.
        RefrescarDock();
        // E3/E4: y los paneles laterales, con el mismo pulso y el mismo gesto de borde.
        // Apagados de serie: ver MainWindow.LateralesAlBorde.cs.
        RefrescarLateralesAlBorde();
        // ---------- Los fantasmas de la bandeja ----------
        //
        // Una app que se cierra bien manda su NIM_DELETE y la colección avisa sola. Una
        // que se muere de golpe —o cuyo registro queda huérfano al reiniciar Explorer—
        // no manda nada, y nadie avisaría nunca.
        //
        // Así que se CUENTAN los fantasmas una vez por segundo (una llamada a `IsWindow`
        // por icono, nada) y solo se repinta si la cuenta ha cambiado. No es reconstruir
        // la bandeja en cada fotograma: es mirar un número y no tocar nada mientras sea
        // el mismo. Es la misma regla que el vigilante de ventanas, que se encontró con
        // el parpadeo por no tenerla.
        if (_esJefa && Bandeja.Encendida)
        {
            int fantasmas = Bandeja.Fantasmas();
            if (fantasmas != _fantasmasBandeja)
            {
                Log.Info($"bandeja: fantasmas={fantasmas} (eran {_fantasmasBandeja}): " +
                         "iconos cuya ventana ya no existe; se repinta una vez");
                _fantasmasBandeja = fantasmas;
                RefrescarBandeja();
            }
        }
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
        AjustarAnchoPendiente();
    }

    void AplicarTema()
    {
        var t = _cfg.Tema;
        var b = _cfg.Barra;
        WallpaperEngine.RutaManual = _cfg.Wallpaper.Ruta;

        // Antes de montar nada: el nivel de animación lo leen Kf y Ms, por los que
        // pasa todo lo que se mueve, así que tiene que estar puesto ya.
        Animacion.Elegir(_cfg.Animaciones.Nivel);
        // El idioma, antes de montar nada: lo que se construya a partir de aquí ya se traduce
        // al pintarse. Cambiarlo en Ajustes recarga el config y vuelve a pasar por aquí, así
        // que no hace falta reiniciar.
        Textos.Elegir(_cfg.Tema.Idioma);

        // Y con él, el estilo de los botones, que también depende del nivel. Va aquí y
        // no en el constructor porque los botones se vuelven a crear unas líneas más
        // abajo (LlenarZonas): así cambiar el nivel en caliente se ve en el acto.
        Resources[typeof(Button)] = EstiloBoton();

        // ---------- De dónde sale el color desde el rediseño ----------
        //
        // Del tema puesto (`Tema.Id`, uno de los nueve de serie) pasado por la cascada, con
        // encima lo que el usuario le haya cambiado (`Tema.Colores`). Antes salía de tres
        // claves del config; ahora esas tres son solo dos entradas más de la cascada, y la
        // migración v4→v5 ya las movió ahí.
        var tema = TemasDeSerie.De(t.Id);
        var pintura = new Pintura(tema, t.Colores, t.Radios, t.Letras);

        Color fondo = pintura.Color(ClavesBase.Superficie),
              texto = pintura.Color(ClavesBase.Tinta),
              acento = pintura.Color(ClavesBase.Acento);
        Color fondoDelTema = fondo, textoDelTema = texto, acentoDelTema = acento;
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
            var porDefecto = DeSerie.Acento;
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

        // ---------- Y la pintura se rehace con lo que de verdad se va a pintar ----------
        //
        // El wallpaper y las tres correcciones de arriba pueden haber movido el fondo, el
        // texto o el acento. Si lo han hecho, los tres entran como personalizaciones DE ESTA
        // SESIÓN —encima de las del usuario, sin tocar el config— y todo lo que se deriva de
        // ellos se recalcula con los valores de verdad.
        //
        // Antes esto era lo mismo pero dicho de otra forma: «los roles se recalculan con los
        // colores ya resueltos, no con los del config en bruto». Un widget que pregunte por
        // `tarjeta.fondo` tiene que recibir algo que pegue con lo que está pintado.
        if (fondo != fondoDelTema || texto != textoDelTema || acento != acentoDelTema)
        {
            var deLaSesion = new Dictionary<string, string>(t.Colores, StringComparer.OrdinalIgnoreCase)
            {
                [ClavesBase.Superficie] = Relleno.Hex(fondo),
                [ClavesBase.Tinta] = Relleno.Hex(texto),
                [ClavesBase.Acento] = Relleno.Hex(acento),
            };
            pintura = new Pintura(tema, deLaSesion, t.Radios, t.Letras);
        }

        _roles = Roles.Desde(pintura);
        _forma = Formas.Del(pintura);

        // La barra de desplazamiento propia, para TODA la aplicación: se pone en los
        // recursos de la app y no en los de esta ventana para que llegue también a Ajustes
        // y a las ventanas flotantes. Ver `BarraDeScroll`.
        try
        {
            Application.Current.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] =
                BarraDeScroll.Estilo(texto, _cAcentoLegible);
        }
        catch (Exception ex) { Log.Error("montar la barra de desplazamiento", ex); }

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

        // El radio sale del tema (o de lo que el usuario haya puesto encima), no de una clave
        // suelta del config: es lo que permite que Videojuegos y Cyberpunk vayan a radio 0 y
        // que Sakura vaya a 25.
        Isla.CornerRadius = new CornerRadius(pintura.Radio(Piezas.Isla));
        // ---------- La letra sale del tema ----------
        //
        // Leía `Tema.Fuente` del config, o sea una sola letra para todo y elegida aparte. Pero
        // la letra ES del tema —Cyberpunk pide Orbitron en los títulos y Rajdhani en el resto,
        // Sakura una redondeada, Samurái una de pincel— y hasta ahora el tema la declaraba, se
        // guardaba, se probaba y no la usaba nadie.
        FontFamily = LetraDe(pintura.Fuente(Letras.Texto));
        FontSize = t.TamanoFuente;

        // ---------- El filo de la isla, y la luz que lo recorre ----------
        //
        // El filo ya no es «el acento si BordeAcento está puesto»: es la clave `isla.filo` del
        // tema, que es lo que la maqueta fija (1 px, blanco al 9 % en Profesional).
        //
        // Y encima, si el tema lleva la decoración `destello-filo`, ese filo pasa a ser un
        // degradado con una franja estrecha de luz que da la vuelta al contorno. `BordeAcento`
        // se queda como el interruptor de quien no lo quiera.
        PonerFiloDeLaIsla(pintura, t.BordeAcento);

        // ---------- Y el halo, en la envoltura ----------
        PonerAuraDeLaIsla(pintura);
        AjustarBrillo(t.BordeAcento, acento);

        // ---------- Y el halo del neón, también en la BARRA ----------
        //
        // Esto faltaba y es lo que hacía que el estilo pareciera no funcionar. El halo se le
        // puso a `PanelShell`, a `Card` y a la tarjeta de los laterales... y la isla no pasa
        // por ninguno de los tres: se viste aquí, a mano, desde la fase A.
        //
        // O sea que elegías «Neón» y la barra —que es lo que tienes delante todo el día—
        // seguía exactamente igual. El usuario lo dijo así: «creo que lo del neón no
        // funciona». Y desde donde él mira, no funcionaba.
        //
        // Es el fallo de siempre en este proyecto: la pieza buena existe y hay una superficie
        // que no la llama. Van ya unas cuantas.
        //
        // ---------- Y hay que convivir con el brillo que YA tenía ----------
        //
        // `AjustarBrillo` lleva desde la fase A gestionando `Isla.Effect`: es el brillo de
        // acento que se enciende con `Tema.BordeAcento` y que `AnimarBrillo` sube y baja al
        // expandirse la isla. Poner el halo encima sin más lo sustituye, y entonces `_brillo`
        // se queda apuntando a un efecto que ya no está en la isla — la animación de
        // abrir/cerrar seguiría corriendo sobre un objeto suelto y no se vería nada.
        //
        // ---------- Y por qué el halo de la ISLA está apagado ----------
        //
        // Aquí se llamaba a `PonerResplandor(Isla, …)` cuando la forma brillaba, y con el
        // estilo «Neón» funcionaba porque casi nadie lo tenía puesto. Al pasar el halo a ser
        // una decoración del tema, ocho de los nueve lo encienden — y entonces se vio lo que
        // llevaba ahí desde el principio.
        //
        // `Isla` tiene `ClipToBounds="True"` (lo necesita: es lo que recorta el cajón al
        // crecer). Un `DropShadowEffect` sobre un elemento recortado se recorta con él, y el
        // recorte es el RECTÁNGULO de la caja, no la forma redondeada. Resultado: un halo
        // cuadrado alrededor de una isla con las puntas redondas, que asoma justo por donde
        // la curva se separa de la esquina. El usuario lo vio al primer vistazo y lo dijo:
        // «sobresale un rectángulo de la nube, queda feísimo». Medido: #13182E en la esquina
        // contra #0C0E17 del interior.
        //
        // El halo de la isla es de la FASE 2 del rediseño («La isla: destello del filo, halo,
        // reloj con brillo…»), y hacerlo bien es ponerlo en una pieza DETRÁS de la isla, con
        // su misma forma y sin recorte. Encenderlo aquí ahora no es adelantarlo: es enseñar
        // mal el de la fase 2 antes de construirlo.
        //
        // Los paneles lo llevan así desde C5 (ver `CapaDelHalo`). Los laterales NO lo llevaban:
        // aquí se escribió «y los laterales también, sus conchas no recortan», y era media
        // verdad —la concha de un lateral es `VentanaSuperpuesta.Tarjeta`, que sí recorta—, así
        // que su halo salió cuadrado en la v1.0.5. Ya tienen su aura detrás; ver
        // `VentanaSuperpuesta.Envoltura`, que es donde está escrita la regla para esta isla.
        //
        //   if (EstiloDeAhora.Brilla) { PonerResplandor(Isla, EstiloDeAhora, Realce.Ambiente); _brillo = null; }

        // La orientación va ANTES de montar las zonas: los widgets se construyen según
        // ella (el separador es una raya a lo ancho o a lo alto, el carrusel se apila o
        // se tumba), y montarlos primero habría dejado la mitad girada.
        OrientarZonas();

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

        double grosorVentana = b.Alto + b.Margen * 2;

        if (Vertical)
        {
            // La barra vertical no tiene nada que medir ni que expandir: su grosor lo
            // fija el config y su largo es la pantalla. Todo lo de la isla horizontal
            // (MedirAncho, Expandir, el cajón de dentro) se queda quieto, que ya está
            // apagado en modo barra.
            Contenido.Height = double.NaN;
            Contenido.Width = double.NaN;
            ColocarBarraVertical();
            _expandida = false;
            FijarLados(true);
            ColocarEnMonitorVertical(grosorVentana);
        }
        else
        {
            ColocarBarraHorizontal();
            Contenido.Height = b.Alto;      // la fila de los módulos no cambia aunque crezca el cajón
            // Que lo de dentro caiga en píxeles enteros. El separador ya lo pedía para sí
            // (`SnapsToDevicePixels`), pero eso solo decide cómo se PINTA: dónde se COLOCA
            // lo dice su padre, y sin esto el padre lo dejaba a medio píxel. Una raya de
            // 1 px a medio píxel es media raya en dos columnas: más clara, y parpadeando
            // en cuanto algo se mueve.
            Contenido.UseLayoutRounding = true;
            Contenido.Width = double.NaN;
            Isla.BeginAnimation(HeightProperty, null);
            Isla.Height = b.Alto;
            Isla.BeginAnimation(WidthProperty, null);
            Isla.HorizontalAlignment = AlineacionDeLaIsla();
            ColocarCajon();                 // alineación vertical, márgenes y orden de las filas

            _expandida = false;
            FijarLados(!EsIsla);
            // En modo isla el ancho siempre va fijado a mano (ver AnimarAncho); en modo barra, automático
            Isla.Width = EsIsla ? MedirAncho() : double.NaN;

            // La ventana es una franja transparente de todo el ancho de SU monitor
            ColocarEnMonitor(grosorVentana);
        }

        // Esto va en el hilo de la ventana porque la AppBar se registra con nuestro propio hwnd.
        // El borde lo decide PanelNube.BordeAppBar: con la barra vertical es ABE_LEFT o
        // ABE_RIGHT y lo que se reserva es ANCHO, no alto.
        if (ReservaHueco)
            AppBar.Registrar(_self, PanelNube.BordeAppBar(b.Modo, PosicionEfectiva),
                             (int)Math.Round(grosorVentana * _monitor.Escala),
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
        AplicarAtajosDeHypr();   // y los de NubeHypr: `Registrar` suelta antes, no se duplican
        AplicarReglasDeHypr();   // y la escucha de reglas se engancha o se suelta según el modo
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
        var mover = new TranslateTransform();            // base 0 = posición final
        Isla.RenderTransform = mover;
        _mover = mover;
        Isla.BeginAnimation(OpacityProperty, Kf(0, 1, 400));

        // Entra por su propio borde: la horizontal cae de arriba (o sube de abajo) y la
        // vertical entra desde su lado.
        bool v = Vertical;
        var eje = v ? TranslateTransform.XProperty : TranslateTransform.YProperty;
        double desde = (v ? PegadaIzquierda : Arriba) ? -distancia : distancia;

        // Con el autoocultar encendido su sitio es FUERA de la pantalla, no dentro
        // (lo decide AplicarVisibilidad, que ya ha corrido en Aplicar). Caer con
        // rebote y recogerse acto seguido se vería como un tic, y además la caída
        // dejaría el transform en 0 y la isla se quedaría a la vista marcada como
        // recogida: entonces no volvería a esconderse hasta el siguiente cambio.
        if (_recogida && !_asomada) { if (v) mover.X = desde; else mover.Y = desde; return; }

        mover.BeginAnimation(eje, Kf(desde, 0, 700, 0, Muelle));
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

        // ---------- Y se redondea ARRIBA, a píxel entero ----------
        //
        // Medir texto da decimales, y la isla va centrada: un ancho de 580,4 pone todo lo
        // de dentro a medio píxel, y una raya de 1 px —los separadores— se reparte entre
        // dos columnas de píxeles y se ve más clara. Al siguiente minuto el reloj mide
        // 580,9, todo se corre esa pizca y la raya parpadea y salta de sitio. Lo contó el
        // usuario: «la línea de los separadores parpadean y se mueven 1 o 2 px de lado a
        // lado».
        //
        // Arriba y no al más cercano: redondear hacia abajo recortaría el último píxel de
        // lo que acabamos de medir, y de eso ya hay media docena de párrafos en este
        // fichero.
        return Math.Ceiling(Math.Clamp(ancho, Isla.MinWidth, Math.Max(Isla.MinWidth, maximo)));
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
    //
    // ---------- Se mide DESPUÉS del layout, no en caliente ----------
    //
    // Aquí estaba por qué la hora salía cortada un segundo al arrancar. Lo contó el
    // usuario: «cuando se inicia la app, la zona donde está la hora tarda un poco más en
    // iniciarse». Medido en el log: en el primer tick se meten las apps (la isla mide 584)
    // y en ese MISMO tick se hace visible el «ES» del teclado; la medida de detrás seguía
    // diciendo 584, porque un cambio de visibilidad tres niveles más abajo no marca la
    // zona como sucia hasta que pasa el layout, y `Measure` devuelve lo que tenía guardado.
    // Hasta el tick siguiente —un segundo— no salía 612, y entretanto la hora quedaba
    // fuera de la isla.
    //
    // Así que pedir un ajuste ya no mide: programa UNA medida para cuando WPF haya hecho
    // su pasada de layout (prioridad Loaded, que va detrás de la de pintar). Varias
    // peticiones seguidas —la bandeja manda una por icono al arrancar— se juntan en una.
    // Y si toca medir en mitad de otra animación de ancho, se repite al terminar esta en
    // vez de esperar al siguiente tick.
    bool _anchoProgramado, _anchoTrasAnimar;

    void AjustarAncho()
    {
        if (_anchoProgramado) return;
        _anchoProgramado = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _anchoProgramado = false;
            AjustarAnchoYa();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    void AjustarAnchoYa()
    {
        if (!EsIsla) return;
        if (_aplicando || _animandoAncho) { _anchoTrasAnimar = true; return; }
        double hasta = Math.Max(MedirAncho(), _panelAncho);   // con panel abierto manda el panel
        if (!HayQueReajustarElAncho(Isla.ActualWidth, hasta)) return;
        AnimarAncho(hasta, 280, Suave);
    }

    // ---------- Crecer siempre; encoger, solo si merece la pena ----------
    //
    // `RefrescarApps` está en los actualizadores, así que esto se pregunta CADA SEGUNDO.
    // Con el listón en medio píxel, cualquier respiro de la medida —el reloj cambiando de
    // minuto, un punto más en un grupo, una ventana de overlay que aparece y se va—
    // lanzaba una animación de 280 ms de la isla entera. Jugando eso es un baile continuo.
    //
    // Las dos direcciones no valen lo mismo, y ahí está el arreglo:
    //
    //   CRECER   hay que hacerlo siempre. Lo que se ha medido es lo que el contenido
    //            necesita; quedarse corto es recortar un icono o la hora.
    //   ENCOGER  puede esperar. Sobrar tres píxeles de isla no se ve; moverla entera
    //            para quitarlos, sí.
    //
    // No es un número mágico con histéresis por si acaso: es que un fallo (recortar) es
    // peor que el otro (sobrar un poco), así que el listón solo se levanta del lado
    // barato. Y con el ancho ya redondeado a píxel entero (ver MedirAncho), lo que llega
    // aquí son saltos de verdad y no decimales.
    internal const double MargenParaEncoger = 3;

    internal static bool HayQueReajustarElAncho(double ahora, double pedido)
    {
        if (ahora <= 0) return true;                       // aún no hay isla: ponle la medida
        if (pedido > ahora + 0.5) return true;             // crecer: siempre
        return pedido < ahora - MargenParaEncoger;         // encoger: solo si sobra de verdad
    }

    // Lo que se quedó sin medir mientras la isla se desplegaba o se aplicaba el config.
    void AjustarAnchoPendiente()
    {
        if (!_anchoTrasAnimar) return;
        _anchoTrasAnimar = false;
        AjustarAncho();
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
            AjustarAnchoPendiente();
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

        // Y el fondo. Va aquí y no en `Aplicar()` por dos razones: `Aplicar()` corre una
        // vez por isla —con dos pantallas se pondría el fondo dos veces— y el fondo no es
        // de una isla, es del escritorio.
        //
        // `AplicarAsync` no hace nada si la huella no cambió, así que recargar el config
        // por cualquier otro motivo no vuelve a leer una imagen de 20 MB. Eso es lo que
        // hace que aplicar un perfil con fondo —"Noche"— cambie el fondo de verdad, y que
        // guardar cualquier otro ajuste no lo toque.
        _ = GestorFondos.AplicarAsync(nueva.Fondo);

        // ---------- Y el clima, si lo acabas de encender ----------
        //
        // Esto faltaba, y se nota en cuanto lo usas: enciendes el clima, escribes tu ciudad,
        // y el panel sigue diciendo «el tiempo está apagado» hasta el siguiente tick... que
        // es de QUINCE MINUTOS. Lo contó el usuario: «pongo clima Barcelona y no sale».
        //
        // El fallo de fondo es de los de siempre en este proyecto: `ArrancarClima` pide el
        // tiempo al arrancar, el reloj lo pide cada cuarto de hora, y nadie lo pedía al
        // cambiar el ajuste — que es justo el instante en que el usuario está mirando.
        //
        // Lo pide la jefa, no las tres islas: `Clima` guarda la lectura en estático.
        try { Islas.FirstOrDefault(i => i._esJefa)?.PedirElClima(); } catch { }

        // Y los paneles laterales que estén abiertos. Son ventanas aparte, así que `Aplicar()`
        // —que repinta la isla— no les llega: sin esto se quedan con los colores de antes
        // hasta que los cierras y los vuelves a abrir. Ver `RepintarLateralesAbiertos`.
        foreach (var i in Islas.ToList())
        {
            try { i.RepintarLateralesAbiertos(); } catch { }
        }
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

    // `topeAlto` solo lo usa el menú de la vista general, que es el único que se abre en una
    // CAPA y no por `AbrirPanel`: los que van por el panel ya reciben de él su recorte y su
    // desplazamiento, y meterles otro aquí dejaría dos scrolls encajados.
    //
    // Cuando llega, la lista se desplaza por dentro en vez de crecer. Es lo que convierte un
    // menú de catorce opciones en un panel de 320 px de «cortado» en «desplazable».
    FrameworkElement CrearMenuContextual((string Texto, Action Accion)[] opciones, double ancho, double altoFila,
                                         double topeAlto = double.PositiveInfinity)
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

        // ---------- Con la concha de C5, como el resto de los paneles ----------
        //
        // Un menú contextual sin superficie propia es el único del programa que se apoyaba
        // entero en el fondo de la isla: sin filo que dijera dónde acaba, y con el escritorio
        // transparentándose al 10 % detrás de cada opción.
        // La barra que sale es la de la casa: `BarraDeScroll` es el estilo implícito de
        // `ScrollBar` en toda la aplicación (ver `AplicarTema`), así que no hay que pedirla.
        // Y visible, no escondida: en un menú recortado, una barra que no se ve es un menú
        // al que le faltan opciones sin decirlo.
        UIElement dentro = pila;
        if (!double.IsPositiveInfinity(topeAlto))
            dentro = new ScrollViewer
            {
                Content = pila,
                MaxHeight = Math.Max(altoFila, topeAlto),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };

        var concha = PanelShell(dentro, 0);

        // ---------- Y el gesto va en la CONCHA, no en la pila ----------
        //
        // Esto no es un detalle de implementación, es el fallo que habría metido al añadir la
        // concha sin pensarlo: el menú se cierra al salir el ratón, y si el oyente siguiera
        // en la pila, moverse de la pila al filo de la concha —que es DENTRO del menú, a la
        // vista— lo cerraría en la cara del usuario.
        //
        // De paso arregla el caso que ya existía antes de la concha: la pila no llegaba al
        // borde de la isla, así que acercarse a su filo ya cerraba el menú.
        bool tocado = false;
        concha.MouseEnter += (_, _) => tocado = true;
        concha.MouseLeave += (_, _) => { if (tocado && _panelAbierto == PanelMenu) CerrarPanel(); };
        return concha;
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
    //
    // "vertical" es cómo se escribe el mismo dato cuando la barra es una COLUMNA: la
    // hora en dos líneas, "CPU" sobre su porcentaje. Lo dice cada widget porque por
    // dónde se parte un texto depende de lo que significa, y adivinarlo en un sitio
    // común (¿por el espacio?, ¿por los dos puntos?) sale mal al primer caso raro.
    TextBlock Etiqueta(Func<string> valor, bool animar = false, string? muestra = null,
                       Func<string>? vertical = null, bool brillo = false)
    {
        bool v = Vertical;
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        // Cifras de ancho fijo: si no, un "1" ocupa menos que un "8" y el texto baila
        Typography.SetNumeralAlignment(tb, FontNumeralAlignment.Tabular);

        if (v)
        {
            // El margen va en el eje en el que se separan los módulos. Puesto en
            // horizontal (6 a cada lado) se comía 12 px de una columna de 38, que es
            // exactamente lo que cortaba la hora: "00:05" mide 34 y le quedaban 36.
            tb.Margin = new Thickness(0, 6, 0, 6);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.TextAlignment = TextAlignment.Center;
            // Las tres redes que impiden que se recorte: parte por donde pueda, nunca
            // mide más que la columna, y si aun así no cabe, se encoge.
            tb.TextWrapping = TextWrapping.Wrap;
            tb.MaxWidth = AnchoUtilDeLaColumna;
            // Nada de MinWidth: lo que no sobra en una columna es justamente ancho.
        }
        else
        {
            tb.Margin = new Thickness(6, 0, 6, 0);
            if (muestra != null) tb.MinWidth = AnchoDeTexto(muestra);
        }

        void Act()
        {
            var nuevo = (v && vertical != null ? vertical() : valor()) ?? "";
            if (nuevo == tb.Text) return;
            bool habia = tb.Text.Length > 0;
            tb.Text = nuevo;
            tb.Visibility = nuevo.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (v) EncajarEnLaColumna(tb);
            if (animar && habia) DeslizarTexto(tb);   // el minuto entra desde abajo
        }
        Act();
        _actualizadores.Add(Act);
        // El brillo va DESPUÉS de que `Act()` haya puesto el texto: el pincel se mapea sobre
        // la caja del TextBlock, y con la caja vacía el degradado no tendría dónde recorrer.
        if (brillo) PonerBrilloDeTexto(tb);
        return tb;
    }

    // Lo que de verdad le queda a un widget a lo ancho de la columna: el grosor menos
    // el filo de acento por los dos lados.
    double AnchoUtilDeLaColumna => Math.Max(16, _cfg.Barra.Alto - Isla.BorderThickness.Left * 2);

    // La última red contra el recorte: si una línea sigue sin caber —"100%" no se puede
    // partir, y con el grosor al mínimo tampoco cabe entero— se baja el tamaño de letra
    // hasta que quepa. Encoger se lee; recortar, no.
    void EncajarEnLaColumna(TextBlock tb)
    {
        try
        {
            tb.FontSize = FontSize;                 // siempre desde el tamaño del tema
            if (tb.Text.Length == 0) return;

            double disponible = AnchoUtilDeLaColumna;
            double mayor = 0;
            foreach (var linea in tb.Text.Split('\n'))
                mayor = Math.Max(mayor, AnchoDeTexto(linea, tb.FontSize));

            if (mayor <= disponible || mayor <= 0) return;
            tb.FontSize = Math.Max(7, Math.Floor(tb.FontSize * disponible / mayor));
        }
        catch { }
    }

    // Ancho que ocuparía ese texto con la fuente y el tamaño actuales del tema
    double AnchoDeTexto(string texto) => AnchoDeTexto(texto, FontSize);

    // Y con un tamaño concreto, que es lo que hace falta para encogerlo hasta que quepa
    double AnchoDeTexto(string texto, double tamano)
    {
        try
        {
            double ppp = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var tipo = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            // El pincel no se pinta: `FormattedText` lo pide para poder dibujar, y aquí solo
            // se le pregunta el ancho. Da igual cuál sea.
            return new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                     tipo, tamano, Brushes.Black, ppp).Width;   // color literal a propósito
        }
        catch (Exception ex)
        {
            Log.Error("medir texto", ex);
            return texto.Length * tamano * 0.6;   // aproximación, mejor eso que nada
        }
    }

    // ---------- Aquí se traduce ----------
    //
    // Y no en las ~1900 llamadas. Las cadenas de interfaz de este proyecto se pasan como
    // argumento a un puñado de ayudantes, así que traduciendo DENTRO de ellos se cubren
    // cientos de sitios tocando diez funciones — y sin riesgo de romper una llamada.
    //
    // `Textos.T` devuelve la cadena tal cual si no está traducida, así que lo que falte sale
    // en español: nunca peor que antes. Ver `Textos.cs`.
    TextBlock Texto(string t, double tamano) => new()
    {
        Text = Textos.T(t), FontSize = tamano, Foreground = _texto,
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
            // El tooltip también: es texto que el usuario lee, y en media interfaz es lo
            // único que explica qué hace un icono.
            Content = contenido, ToolTip = Textos.T(tip), Foreground = _texto,
            Height = Math.Max(20, _cfg.Barra.Alto - 8)
        };

        // ---------- Y su nombre para el lector de pantalla ----------
        //
        // Una línea con mucho alcance: por aquí pasan casi todos los botones de la barra y
        // de sus paneles, y ninguno tenía nombre. Medido con `--diagpaneles`: diecisiete de
        // diecisiete en audio, veintitrés de veintitrés en Inicio, catorce de catorce en la
        // bandeja… El contenido suele ser un glifo del área privada o un `Grid`, así que un
        // lector leía un carácter sin significado, o nada.
        //
        // La ayuda emergente ya decía la frase correcta —«Cerrar esta ventana», «Abrir
        // Energía y batería»— pero un ToolTip NO es el nombre de UIA: es otra propiedad, y
        // los lectores no la usan como nombre. Estaba el texto bueno escrito y puesto en el
        // sitio que no era.
        if (!string.IsNullOrWhiteSpace(tip))
            System.Windows.Automation.AutomationProperties.SetName(b, tip);

        // Soltar algo que venías arrastrando no es pulsar este botón (ver ClicDeArrastre)
        b.Click += (_, _) => { if (ClicDeArrastre()) return; accion(); Tick(); };
        if (levantar) ConLevantado(b);
        return b;
    }
}
