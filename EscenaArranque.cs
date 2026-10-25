using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NubeBar;

// La escena de entrada: un robot cruza la pantalla con un pincel gordo, la deja negra
// de una pasada, y cuando se levanta la pintura ya está debajo la nube montada.
//
// Lo que resuelve no es decorativo. Arrancar NubeBar es feo: Explorer recalcula el área
// de trabajo, la barra de Windows se esconde (~0,7 s), las islas se montan, el carrusel
// se llena a trompicones. Eso dura un segundo largo y se ve. La pintura tapa justo ese
// rato y lo que se descubre al quitarla es el escritorio ya terminado.
//
// ---------- Las reglas que no se negocian ----------
//
//  1. **Nunca dejar la pantalla negra.** Hay dos redes: el revelado se dispara con un
//     tope de tiempo aunque la interfaz no avise de que está lista, y encima hay un
//     temporizador que cierra la ventana pase lo que pase. Si algo falla, se cae del
//     lado de "no hay escena", nunca del lado de "pantalla negra".
//  2. **Ni foco ni clics.** La ventana nace WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW y
//     WS_EX_TRANSPARENT: no se activa, no sale en Alt+Tab y los clics la atraviesan.
//     Mientras dura, el escritorio responde exactamente igual que sin ella.
//  3. **Se puede saltar.** Esc o un clic la revelan al instante. Como la ventana no
//     recibe entrada (punto 2), se mira el teclado y el botón a pelo cada 60 ms, que
//     es el mismo truco que usa el arrastre de la isla.
//  4. **Una sola vez.** Se crea en App.OnStartup y en ningún otro sitio, así que
//     recargar el tema, los paneles o los widgets no la vuelve a sacar.
public sealed class EscenaArranque : Window
{
    // ---------- Quién manda desde fuera ----------
    static EscenaArranque? _actual;

    public static bool EnMarcha => _actual != null;

    // Se llama una vez, antes de crear las islas. Decide si toca y la muestra.
    public static void Arrancar()
    {
        try
        {
            if (_actual != null) return;

            var arbol = ConfigArbol.Cargar();
            Animacion.Elegir(arbol.Texto("Animaciones", "Nivel", "auto"));

            var modo = (arbol.Texto("Animaciones", "Robot", "primera-vez") ?? "").Trim().ToLowerInvariant();
            bool vista = arbol.Bandera("Animaciones", "RobotVisto", false);

            if (!TocaMostrar(modo, vista, out string porque))
            {
                Log.Info($"escena de entrada: no se muestra ({porque})");
                return;
            }

            var acento = LeerColor(arbol.Texto("Tema", "Acento", "#7AA2F7"));
            _actual = new EscenaArranque(Monitores.Principal(), acento, !vista);
            _actual.Show();
            Log.Info($"escena de entrada: {porque} (nivel {Animacion.Nivel.ToString().ToLowerInvariant()})");
        }
        catch (Exception ex)
        {
            // Que la escena falle no puede impedir que arranque la nube
            Log.Error("arrancar la escena de entrada", ex);
            Cerrar("falló al montarse");
        }
    }

    // El acento del usuario, para que el robot salga pintado de SU nube. Si el config
    // trae algo que no es un color, se usa el azul de serie antes que no salir.
    static Color LeerColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0x7A, 0xA2, 0xF7); }
    }

    internal static bool TocaMostrar(string modo, bool vista, out string porque)
    {
        if (Animacion.Nivel == NivelAnimacion.Desactivado)
        {
            porque = "el movimiento está desactivado";
            return false;
        }
        switch (modo)
        {
            case "nunca":
                porque = "la tienes puesta en \"nunca\"";
                return false;
            case "cada-inicio":
                porque = "en cada inicio";
                return true;
            case "primera-vez":
            case "al-cambiar-tema":   // el disparo por tema es otra cosa; al arrancar, la primera vez
                porque = vista ? "ya la has visto" : "es la primera vez";
                return !vista;
            default:
                Log.Info($"escena de entrada \"{modo}\" desconocida: se trata como \"primera-vez\" " +
                         "(valores: primera-vez, cada-inicio, al-cambiar-tema, nunca)");
                porque = vista ? "ya la has visto" : "es la primera vez";
                return !vista;
        }
    }

    // "Vista previa ahora", desde Ajustes. Se salta la decisión —la quieres ver, da
    // igual si ya la viste— y no apunta nada: una vista previa no gasta la primera vez.
    // Y se da por lista enseguida, porque aquí no hay ningún arranque que esperar.
    public static void VistaPrevia()
    {
        try
        {
            if (_actual != null) return;
            if (Animacion.Nivel == NivelAnimacion.Desactivado)
            {
                Log.Info("vista previa de la escena: el movimiento está desactivado, no hay nada que ver");
                return;
            }
            var arbol = ConfigArbol.Cargar();
            _actual = new EscenaArranque(Monitores.Principal(), LeerColor(arbol.Texto("Tema", "Acento", "#7AA2F7")),
                                         apuntarVista: false);
            _actual.Show();
            _actual._listo = true;
            Log.Info("vista previa de la escena de entrada");
        }
        catch (Exception ex)
        {
            Log.Error("vista previa de la escena", ex);
            Cerrar("falló la vista previa");
        }
    }

    // La interfaz real ya está montada: se puede levantar la pintura.
    public static void InterfazLista()
    {
        if (_actual is not { } e) return;
        e._listo = true;
        Log.Info($"escena de entrada: la interfaz avisa de que está lista ({e._reloj.ElapsedMilliseconds} ms)");
    }

    // Cierre inmediato y sin ceremonia: --salir, un error, o el tope de seguridad.
    public static void Cerrar(string motivo)
    {
        if (_actual is not { } e) return;
        _actual = null;
        try
        {
            Log.Info($"escena de entrada: se cierra ({motivo})");
            e._pulso.Stop();
            e._tope.Stop();
            e.SoltarElPincel();
            e.Close();
        }
        catch (Exception ex) { Log.Error("cerrar la escena de entrada", ex); }
    }

    // ---------- Duraciones ----------
    //
    // El nivel de animación no se escala con Animacion.Escalar porque esto no es una
    // animación de la isla: es una escena con tres tiempos que tienen que guardar una
    // proporción entre ellos. Lo que se hace es tener un juego de tiempos por nivel.
    //
    //   Completo     robot, pincelada larga, el segundo de negro y revelado con calma
    //   Equilibrado  lo mismo más corto
    //   Reducido     sin robot: un fundido a negro muy breve y a revelar
    //   Desactivado  no se llega aquí (ni se crea la escena)
    internal readonly record struct Duraciones(double Pintar, double Negro, double Revelar, bool ConRobot);

    internal static Duraciones Tiempos() => Animacion.Nivel switch
    {
        NivelAnimacion.Completo    => new(1150, 1000, 460, true),
        NivelAnimacion.Equilibrado => new(700, 620, 280, true),
        _                          => new(200, 260, 180, false),
    };

    readonly double _msPintar, _msNegro, _msRevelar;
    readonly bool _conRobot;

    // Tope duro: pase lo que pase, a los 9 s no hay escena. Es la última red contra
    // quedarse con la pantalla negra si la interfaz nunca avisa.
    const double MsTope = 9000;

    readonly Monitores.Info _monitor;
    readonly Color _acento;
    readonly bool _apuntarVista;        // hay que escribir RobotVisto al terminar

    readonly DispatcherTimer _pulso = new() { Interval = TimeSpan.FromMilliseconds(60) };
    readonly DispatcherTimer _tope = new() { Interval = TimeSpan.FromMilliseconds(MsTope) };
    readonly Stopwatch _reloj = Stopwatch.StartNew();

    bool _listo;                        // la interfaz ha avisado
    bool _pintado;                      // la pincelada ha terminado
    bool _revelando;
    double _msPintadoEn;                // cuándo terminó de pintar

    Canvas? _lienzo;
    IntPtr _self;
    EventHandler? _avanzar;        // el que dibuja la pincelada, fotograma a fotograma

    // Se suelta en cuanto el trazo llega al final y, pase lo que pase, al cerrar: un
    // manejador de CompositionTarget.Rendering olvidado corre para siempre.
    void SoltarElPincel()
    {
        if (_avanzar == null) return;
        CompositionTarget.Rendering -= _avanzar;
        _avanzar = null;
    }

    EscenaArranque(Monitores.Info monitor, Color acento, bool apuntarVista)
    {
        _monitor = monitor;
        _acento = acento;
        _apuntarVista = apuntarVista;

        var t = Tiempos();
        _msPintar = t.Pintar; _msNegro = t.Negro; _msRevelar = t.Revelar; _conRobot = t.ConRobot;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        Topmost = true;
        Focusable = false;
        Title = "NubeBar";

        // En unidades de WPF para el primer fotograma; en cuanto hay hwnd se coloca en
        // píxeles de verdad, que es lo único que vale con dos monitores a distinto DPI.
        Left = monitor.X; Top = monitor.Y;
        Width = monitor.Ancho / monitor.Escala;
        Height = monitor.Alto / monitor.Escala;

        Content = new Grid { ClipToBounds = true };

        SourceInitialized += (_, _) =>
        {
            _self = new WindowInteropHelper(this).Handle;
            Nativo.NoActivable(_self);
            Nativo.Atravesable(_self);      // los clics pasan de largo
            Nativo.Colocar(_self, monitor.X, monitor.Y, monitor.Ancho, monitor.Alto);
            Width = double.NaN; Height = double.NaN;
        };

        // El contenido se monta YA, no cuando la ventana dé su primer fotograma.
        //
        // Colgarlo de ContentRendered parecía más correcto —así se mide la ventana de
        // verdad— y costaba segundo y medio: justo detrás de esto, App llama a
        // MainWindow.Arrancar(), que monopoliza el hilo de interfaz hasta que termina.
        // Hasta entonces no hay ni un fotograma, así que la escena empezaba tarde y se
        // perdía justamente el rato que venía a tapar.
        //
        // Las medidas salen del monitor en unidades de WPF (píxeles entre su escala),
        // que es exactamente lo que WPF le va a dar a esta ventana ahí: no hace falta
        // esperar a nadie para saberlo.
        Montar();

        _pulso.Tick += (_, _) => Pulso();
        _tope.Tick += (_, _) => Cerrar("tope de seguridad de 9 s");
        _pulso.Start();
        _tope.Start();
    }

    // ---------- La escena ----------
    //
    // La composición está aparte y devuelve, además del lienzo, una forma de ponerla en
    // un punto cualquiera del recorrido sin animar nada. Eso permite dibujar la escena
    // fuera de pantalla y mirarla fotograma a fotograma, que es la única manera de
    // revisar un dibujo sin tener que sacarlo delante del usuario.
    internal sealed record Composicion(Canvas Lienzo, Action<double> Avanzar,
                                       LineGeometry Trazo, LineGeometry[] Cerdas,
                                       TranslateTransform? Robot, double Desde, double Hasta);

    internal static Composicion Componer(double w, double h, Color acento, bool conRobot)
    {
        var lienzo = new Canvas { Width = w, Height = h, IsHitTestVisible = false };

        // La pincelada: una línea de grosor mayor que la pantalla, con los extremos
        // redondeados, que cruza de un lado a otro. De una pasada, como se pidió: no son
        // varias brochas ni un barrido rectangular, es UN trazo.
        //
        // El detalle que hay que tener en la cabeza todo el rato: **un extremo redondeado
        // sobresale medio grosor del punto final**. El filo que se ve no está en
        // EndPoint.X, está en EndPoint.X + radio. Sin contar con eso, el trazo ya nace
        // tapando media pantalla y el robot va setecientos píxeles por detrás de su
        // propia pintura.
        double y = h * 0.52;
        double grosor = h * 1.35, radio = grosor / 2;
        double desde = -radio - w * 0.06;          // el casquete entero, fuera de la pantalla
        double hasta = w + radio * 0.25;           // y al final sobra pintura por la derecha

        var trazo = new LineGeometry(new Point(desde, y), new Point(desde, y));
        lienzo.Children.Add(new Path
        {
            Data = trazo,
            Stroke = Brushes.Black,
            StrokeThickness = grosor,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });

        // Cerdas: dos trazos más, del MISMO negro, uno adelantado y otro atrasado. En
        // gris se leían como dos barras cruzando la pantalla; en negro no se ven como
        // líneas, se ven como que el filo de la pintura no es limpio — que es
        // justamente lo que distingue un pincel de un barrido.
        var forma = new (double Desvio, double Grosor, double Adelanto)[]
        {
            (-h * 0.40, h * 0.46,  0.045),
            ( h * 0.36, h * 0.38, -0.035),
        };
        var cerdas = new LineGeometry[forma.Length];
        foreach (var (desvio, gr, _) in forma)
        {
            var lg = new LineGeometry(new Point(desde, y + desvio), new Point(desde, y + desvio));
            cerdas[Array.FindIndex(forma, f => f.Desvio == desvio)] = lg;
            lienzo.Children.Add(new Path
            {
                Data = lg,
                Stroke = Brushes.Black,
                StrokeThickness = gr,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        // El robot va justo DELANTE del filo, con su pincel apoyado en él: la pintura
        // queda detrás, que es por donde ya ha pasado.
        TranslateTransform? llevar = null;
        double alto = 0;
        if (conRobot)
        {
            alto = Math.Max(150, Math.Min(320, h * 0.26));
            var robot = Robot.Dibujar(alto, acento);
            llevar = new TranslateTransform(0, y - alto * Robot.PuntaY / 100.0);
            robot.RenderTransform = llevar;
            lienzo.Children.Add(robot);
        }

        void Avanzar(double t)
        {
            t = Math.Clamp(t, 0, 1);
            double x = desde + (hasta - desde) * t;
            trazo.EndPoint = new Point(x, y);
            for (int i = 0; i < cerdas.Length; i++)
            {
                double ti = Math.Clamp(t + forma[i].Adelanto, 0, 1);
                cerdas[i].EndPoint = new Point(desde + (hasta - desde) * ti, y + forma[i].Desvio);
            }
            // La punta del pincel, en el filo que se VE (el del casquete redondeado)
            if (llevar != null) llevar.X = x + radio - alto * Robot.PuntaX / 100.0;
        }

        Avanzar(0);
        return new Composicion(lienzo, Avanzar, trazo, cerdas, llevar, desde, hasta);
    }

    void Montar()
    {
        if (_lienzo != null) return;
        try
        {
            double w = _monitor.Ancho / _monitor.Escala, h = _monitor.Alto / _monitor.Escala;

            var c = Componer(w, h, _acento, _conRobot);
            _lienzo = c.Lienzo;
            ((Grid)Content).Children.Add(_lienzo);

            // En "reducido" no hay pincelada que dibujar: se pone el negro de una vez y
            // lo que queda de escena es el fundido de entrada y el de salida, que es la
            // versión "muy breve y simplificada" del nivel.
            if (!Animacion.Hay)
            {
                c.Avanzar(1);
                _lienzo.Opacity = 0;
                _lienzo.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_msPintar)));
                _lienzo.Opacity = 1;
                _msPintadoEn = _reloj.ElapsedMilliseconds + _msPintar;
                return;
            }

            // La pincelada se dibuja fotograma a fotograma y no con tres animaciones en
            // paralelo. El motivo es sincronía: el robot y el trazo tienen que ir
            // pegados, y tres relojes distintos se separan en cuanto el sistema va
            // cargado —que al arrancar NubeBar es exactamente lo que pasa—. Con un solo
            // Avanzar(t) por fotograma no hay forma de que se desincronicen.
            var curva = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var reloj = Stopwatch.StartNew();
            _avanzar = (_, _) =>
            {
                double t = Math.Clamp(reloj.Elapsed.TotalMilliseconds / _msPintar, 0, 1);
                c.Avanzar(curva.Ease(t));
                if (t >= 1) SoltarElPincel();
            };
            CompositionTarget.Rendering += _avanzar;

            // El vaivén sí es una animación suelta: va en el otro eje y nadie más lo toca
            if (c.Robot is { } llevar)
                llevar.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(llevar.Y - 9, llevar.Y + 9, TimeSpan.FromMilliseconds(_msPintar / 2.6))
                    {
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    });

            // Cuándo acaba de pintar se cuelga de un reloj y no del Completed de la
            // animación: si el sistema va cargado, el Completed puede llegar tarde, y
            // de ese instante depende que la escena termine.
            _msPintadoEn = _reloj.ElapsedMilliseconds + _msPintar;
        }
        catch (Exception ex)
        {
            Log.Error("montar la escena de entrada", ex);
            Cerrar("falló al dibujarse");
        }
    }

    // ---------- El pulso ----------
    //
    // Un solo temporizador lleva las tres decisiones: ¿has pulsado para saltártela?,
    // ¿ha terminado de pintar?, ¿toca ya revelar?
    void Pulso()
    {
        try
        {
            if (_revelando) return;

            if (Saltar()) { Revelar("te la has saltado"); return; }

            double ahora = _reloj.ElapsedMilliseconds;
            if (!_pintado)
            {
                if (ahora < _msPintadoEn || _lienzo == null) return;
                _pintado = true;
                return;
            }

            // Negro: se aguanta lo que diga el nivel Y, además, hasta que la interfaz
            // avise. Lo segundo es lo que hace que un arranque lento no enseñe una nube
            // a medio montar — y el tope de seguridad impide que se eternice.
            double desdeElNegro = ahora - _msPintadoEn;
            bool cumplido = desdeElNegro >= _msNegro;
            bool pasado = desdeElNegro >= MsTope * 0.6;

            if (cumplido && (_listo || pasado))
                Revelar(_listo ? "la interfaz está lista" : "la interfaz no avisó: se revela igual");
        }
        catch (Exception ex)
        {
            Log.Error("pulso de la escena de entrada", ex);
            Cerrar("falló el pulso");
        }
    }

    // Esc o un clic. La ventana no recibe entrada a propósito (los clics la
    // atraviesan), así que se pregunta al sistema, igual que hace el arrastre.
    static bool Saltar()
    {
        try { return Nativo.TeclaPulsada(0x1B) || Nativo.BotonIzquierdoPulsado(); }
        catch { return false; }
    }

    void Revelar(string motivo)
    {
        if (_revelando) return;
        _revelando = true;
        _pulso.Stop();
        Log.Info($"escena de entrada: revelando a los {_reloj.ElapsedMilliseconds} ms ({motivo})");

        try
        {
            if (_apuntarVista) ApuntarQueSeHaVisto();

            var fundido = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(_msRevelar))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fundido.Completed += (_, _) => Cerrar("revelada");
            BeginAnimation(OpacityProperty, fundido);
        }
        catch (Exception ex)
        {
            Log.Error("revelar la escena de entrada", ex);
            Cerrar("falló el revelado");
        }
    }

    // Se apunta DESPUÉS de verla, no antes: si la escena reventara a mitad, la próxima
    // vez vuelve a intentarlo en vez de quedarse sin estrenar para siempre.
    static void ApuntarQueSeHaVisto()
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            arbol.Poner("Animaciones", "RobotVisto", true);
            arbol.Guardar();
            Log.Info("escena de entrada: apuntada como vista");
        }
        catch (Exception ex) { Log.Error("apuntar la escena como vista", ex); }
    }
}
