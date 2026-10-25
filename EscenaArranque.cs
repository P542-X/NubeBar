using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Threading;
using System.Windows.Threading;

namespace NubeBar;

// La escena de entrada: **la nube despierta**.
//
// La pantalla empieza entera a negro. Una nubecita aparece en el centro, da un par de
// botes con su pulso de brillo y su parpadeo —eso es el saludo, y no hace falta nada
// más—, se enciende desde dentro y **se abre**: crece hasta ocupar la pantalla, y por
// dentro de ella va apareciendo el escritorio de verdad, la barra y los widgets. Cuando
// ya está todo a la vista, se recoge, viaja hasta donde está la barra y se funde con
// ella como su icono.
//
// Lo que resuelve no es decorativo. Arrancar NubeBar es feo: Explorer recalcula el área
// de trabajo, la barra de Windows se esconde (~0,7 s), las islas se montan, el carrusel
// se llena a trompicones. Eso dura un segundo largo y se ve. El negro tapa justo ese
// rato, y lo que la nube va descubriendo al abrirse es el escritorio ya terminado.
//
// Y una cosa que condiciona TODO el diseño: **la nube es la única fuente de verdad del
// revelado**. La capa negra no es una máscara aparte que se anima en paralelo: es un
// rectángulo del tamaño de la pantalla con un agujero, y el agujero es la MISMA
// geometría que dibuja la nube, con las mismas transformaciones. No hay dos relojes que
// se puedan separar, y el escritorio no puede asomar donde la nube no ha llegado porque
// lo que lo destapa es el agujero de la nube.
//
// Aquí hubo antes un robot humanoide con brazos, manos, torso, propulsores y estela. Se
// tiró entero: un humanoide pide una animación de humanoide —gestos, posturas,
// anatomía— y para contar "el escritorio se enciende" eso era ruido, no personaje.
//
// ---------- Las reglas que no se negocian ----------
//
//  1. **Nunca dejar la pantalla negra.** Hay dos redes: la construcción arranca con un
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
    //
    // La escena vive en SU PROPIO HILO DE INTERFAZ, con su propio Dispatcher. No es una
    // florituura: medido, era la diferencia entre 3 y 118 fotogramas por segundo.
    //
    // Justo detrás de crear la escena, App llama a MainWindow.Arrancar(), que se queda
    // con el hilo de interfaz casi un segundo montando las islas, la appbar, la bandeja
    // y el índice del buscador. `CompositionTarget.Rendering` solo se dispara cuando el
    // Dispatcher está libre, así que la animación se quedaba sin fotogramas justo
    // mientras tenía que moverse: **5 fotogramas en 1,3 s**, uno de ellos de un segundo.
    // La misma escena, sola, daba 137 fotogramas en 1,15 s.
    //
    // Con un hilo propio, que la nube esté ocupada deja de importar. Y de paso la hace
    // más robusta: aunque el arranque se atascara del todo, la escena sigue animando y
    // sigue cerrándose sola por su tope de seguridad.
    static EscenaArranque? _actual;
    static Dispatcher? _suyo;
    static readonly object _candado = new();

    public static bool EnMarcha { get { lock (_candado) return _actual != null; } }

    // Se llama una vez, antes de crear las islas. Decide si toca y la muestra.
    public static void Arrancar()
    {
        try
        {
            lock (_candado) if (_actual != null) return;

            var arbol = ConfigArbol.Cargar();
            Animacion.Elegir(arbol.Texto("Animaciones", "Nivel", "auto"));

            var modo = (arbol.Texto("Animaciones", "Robot", "primera-vez") ?? "").Trim().ToLowerInvariant();
            bool vista = arbol.Bandera("Animaciones", "RobotVisto", false);

            if (!TocaMostrar(modo, vista, out string porque))
            {
                Log.Info($"escena de entrada: no se muestra ({porque})");
                return;
            }

            Log.Info($"escena de entrada: {porque} (nivel {Animacion.Nivel.ToString().ToLowerInvariant()})");
            EnSuHilo(LeerColor(arbol.Texto("Tema", "Acento", "#7AA2F7")), LeerBarra(arbol),
                     apuntarVista: !vista, yaLista: false);
        }
        catch (Exception ex)
        {
            // Que la escena falle no puede impedir que arranque la nube
            Log.Error("arrancar la escena de entrada", ex);
            Cerrar("falló al montarse");
        }
    }

    // Monta la ventana en un hilo nuevo y espera lo justo a que esté en pantalla: si se
    // siguiera sin esperar, los primeros fotogramas del arranque quedarían al aire, que
    // es precisamente lo que la escena viene a tapar.
    static void EnSuHilo(Color acento, Barra barra, bool apuntarVista, bool yaLista)
    {
        var monitor = Monitores.Principal();
        var enPantalla = new ManualResetEventSlim(false);

        var hilo = new Thread(() =>
        {
            try
            {
                var e = new EscenaArranque(monitor, acento, barra, apuntarVista);
                lock (_candado) { _actual = e; _suyo = Dispatcher.CurrentDispatcher; }
                if (yaLista) e._listo = true;
                // Al cerrarse la ventana se acaba el hilo: sin esto, el Dispatcher.Run
                // de abajo se quedaría girando para siempre.
                e.Closed += (_, _) => Dispatcher.CurrentDispatcher.InvokeShutdown();
                e.Show();
                enPantalla.Set();
                Dispatcher.Run();
            }
            catch (Exception ex) { Log.Error("hilo de la escena de entrada", ex); }
            finally
            {
                lock (_candado) { _actual = null; _suyo = null; }
                enPantalla.Set();
            }
        })
        {
            // De fondo y con su propio apartamento: WPF exige STA, y de fondo para que
            // este hilo no pueda impedir nunca que el proceso termine.
            IsBackground = true,
            Name = "escena-arranque",
            // Un pelo por encima: lo que hace por fotograma es mover dos números, pero
            // compite con un arranque que satura la máquina. Sin esto se queda sin
            // turno justo en los fotogramas que más se notan.
            Priority = ThreadPriority.AboveNormal
        };
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        enPantalla.Wait(800);
    }

    // Todo lo que llega de fuera pasa por aquí: la escena solo se toca desde su hilo.
    static void EnLaEscena(Action<EscenaArranque> que)
    {
        EscenaArranque? e; Dispatcher? d;
        lock (_candado) { e = _actual; d = _suyo; }
        if (e == null || d == null) return;
        try
        {
            if (d.CheckAccess()) que(e);
            else d.BeginInvoke(que, e);
        }
        catch (Exception ex) { Log.Error("hablar con la escena de entrada", ex); }
    }

    // El acento del usuario. La nube ya no lo usa —es la marca, va en azul nube— pero se
    // sigue leyendo porque la escena lo recibe y puede volver a hacer falta.
    static Color LeerColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0x7A, 0xA2, 0xF7); }
    }

    // Lo ÚNICO que la escena sabe de la nube de verdad: por qué borde sale y cuánto
    // ocupa. Con eso deja su franja para el final de la construcción y hace que los
    // nube aterrice donde de verdad va a estar la barra. Cualquier cosa rara en el
    // config y se asume "arriba": una escena bonita en el sitio equivocado es mejor que
    // ninguna escena.
    static Barra LeerBarra(ConfigArbol arbol)
    {
        try
        {
            string borde = (arbol.TextoDelPanel("principal", "Posicion", "arriba") ?? "arriba")
                           .Trim().ToLowerInvariant();
            if (!PanelNube.PosicionValida(borde)) borde = "arriba";
            double grosor = arbol.NumeroDelPanel("principal", "Grosor", 38);
            double margen = arbol.NumeroDelPanel("principal", "Margen", 8);
            return new Barra(borde, Math.Clamp(grosor + margen * 2, 24, 240));
        }
        catch { return new Barra("arriba", 54); }
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
            lock (_candado) if (_actual != null) return;
            if (Animacion.Nivel == NivelAnimacion.Desactivado)
            {
                Log.Info("vista previa de la escena: el movimiento está desactivado, no hay nada que ver");
                return;
            }
            Log.Info("vista previa de la escena de entrada");
            var arbol = ConfigArbol.Cargar();
            // "yaLista" porque aquí no hay ningún arranque que esperar
            EnSuHilo(LeerColor(arbol.Texto("Tema", "Acento", "#7AA2F7")), LeerBarra(arbol),
                     apuntarVista: false, yaLista: true);
        }
        catch (Exception ex)
        {
            Log.Error("vista previa de la escena", ex);
            Cerrar("falló la vista previa");
        }
    }

    // La interfaz real ya está montada: se puede levantar la pintura.
    public static void InterfazLista() => EnLaEscena(e =>
    {
        e._listo = true;
        Log.Info($"escena de entrada: la interfaz avisa de que está lista ({e._reloj.ElapsedMilliseconds} ms)");
    });

    // Cierre inmediato y sin ceremonia: --salir, un error, o el tope de seguridad.
    public static void Cerrar(string motivo) => EnLaEscena(e =>
    {
        lock (_candado) { if (_actual != e) return; _actual = null; }
        try
        {
            Log.Info($"escena de entrada: se cierra ({motivo})");
            e._pulso.Stop();
            e._tope.Stop();
            e.DejarDeDibujar();
            e.Close();      // y al cerrarse, el hilo termina solo (ver EnSuHilo)
        }
        catch (Exception ex) { Log.Error("cerrar la escena de entrada", ex); }
    });

    // ---------- El guion ----------
    //
    // Cada etapa tiene su duración y **ninguna depende de los hercios**. El compositor
    // decide cuántos fotogramas caben; el guion decide cuánto dura cada cosa. A 240 Hz
    // hay el doble de fotogramas que a 120, no la mitad de escena.
    //
    // Que no se confundan las dos ideas es justo lo que hace `Avanzar(ms)`: recibe
    // milisegundos transcurridos y devuelve la escena en ese instante. Si un fotograma
    // llega tarde, se dibuja el instante que toca, no el siguiente paso de una cuenta.

    // ---------- Un solo progreso, y una sola figura ----------
    //
    // La escena entera es `Avanzar(ms)` y una nube. Cinco etapas en fila, cada una con
    // su duración, y dentro de cada una se calculan diez números: el tamaño de la nube,
    // dónde está, cuánto se ladea y las opacidades de lo poco que se dibuja.
    //
    // Lo importante no es que sea corto, es **de dónde sale lo que se ve**: la capa
    // negra que tapa el escritorio es un rectángulo con un agujero **con la forma de la
    // nube**, hecho con la MISMA geometría que dibuja la nube. No hay una animación para
    // la nube y otra para el revelado; hay una figura. Por construcción, el escritorio
    // no puede asomar donde no esté la nube, porque lo que lo destapa es el agujero.
    //
    // Queda UNA espera, y solo como red: si cuando la nube va a abrirse la interfaz
    // todavía no ha avisado de que está montada, el reloj de la escena se queda quieto
    // **antes de destapar nada**. Esperar con la nubecita flotando es para lo que existe
    // la escena; esperar a mitad del revelado enseñaría un escritorio a medio montar.
    internal readonly record struct Guion(
        double Entrada, double Saludo, double Despierta, double Crece, double Llega, int Pulsos)
    {
        public double HastaSaludo => Entrada + Saludo;
        public double HastaDespierta => HastaSaludo + Despierta;
        public double HastaCrece => HastaDespierta + Crece;
        public double Total => HastaCrece + Llega;
    }

    internal static Guion GuionDe(NivelAnimacion nivel) => nivel switch
    {
        //                               entra saluda despierta crece llega  pulsos
        NivelAnimacion.Completo    => new(340,  820,    260,    1000,  780,   2),  // 3200 ms
        NivelAnimacion.Equilibrado => new(220,  480,    170,     680,  520,   1),  // 2070 ms
        // "Reducido" no quita etapas, las acorta: aparición directa, un pulso, la
        // expansión justa y la integración rápida. Es el nivel de quien ha pedido menos
        // movimiento en Windows, así que se le cuenta lo mismo en menos tiempo, no media
        // historia.
        _                          => new(110,  300,    110,     420,  320,   1),  // 1260 ms
    };

    internal static Guion Tiempos() => GuionDe(Animacion.Nivel);

    // Dónde está la barra, para que la nube acabe integrada en ella. Es lo único que la
    // escena sabe de la nube de verdad, y a propósito: con el borde y el grosor basta
    // para saber a qué punto de la pantalla tiene que viajar al final. Si el config trae
    // cualquier cosa rara, "arriba" y 54 px.
    internal readonly record struct Barra(string Borde, double Franja);

    // "Estampa" deja leer dónde está cada cosa en un instante dado. Lo usa la prueba que
    // demuestra lo único que de verdad importa aquí: que la escena es la MISMA a 60 y a
    // 240 Hz, y que el escritorio nunca asoma fuera de la nube.
    internal readonly record struct Estampa(
        double Escala,          // el ancho de la nube, en píxeles
        double X, double Y,     // su centro
        double Ladeo,           // grados
        double Relleno,         // opacidad del cuerpo de la nube
        double Tapa,            // opacidad de la capa negra
        double Borde, double Halo, double Nucleo, double Cara,
        double Ojos,            // 1 abiertos, 0 cerrados
        double Llena);          // el tamaño al que la nube tapa la pantalla entera

    internal sealed record Composicion(Canvas Lienzo, Action<double> Avanzar, double Total, Func<Estampa> Estado);

    // ---------- La paleta ----------
    // Azul nube para todo; coral solo para el latido de energía. Sin acento del usuario
    // en la figura: la nube es la marca, no la decoración.
    static readonly Color AzulNube = Color.FromRgb(0x52, 0xAE, 0xF0);
    static readonly Color AzulClaro = Color.FromRgb(0xDC, 0xF1, 0xFF);
    static readonly Color Coral = Color.FromRgb(0xFF, 0x7A, 0x5E);

    static SolidColorBrush Pincelada(Color c, byte alfa = 0xFF)
    {
        var b = new SolidColorBrush(Color.FromArgb(alfa, c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    // Un brillo suave sin `BlurEffect`: un desenfoque hay que recalcularlo cada vez que
    // el elemento cambia de tamaño, y este cambia en cada fotograma.
    static RadialGradientBrush Resplandor(Color centro, Color borde, byte alfaCentro, byte alfaBorde)
    {
        var b = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(alfaCentro, centro.R, centro.G, centro.B), 0),
                new GradientStop(Color.FromArgb((byte)((alfaCentro + alfaBorde) / 2), centro.R, centro.G, centro.B), 0.55),
                new GradientStop(Color.FromArgb(alfaBorde, borde.R, borde.G, borde.B), 1),
            }
        };
        b.Freeze();
        return b;
    }

    // ---------- Cuánto hay que agrandar la nube para que tape la pantalla ----------
    //
    // No se puede poner a ojo: la nube no es un rectángulo, tiene la base recta, los
    // lados redondos y la parte de arriba con bultos, así que lo que tarda en taparse
    // son las esquinas. Y tampoco se puede deducir del ancho y el alto, porque depende
    // de la forma.
    //
    // Se busca por bisección preguntándole al propio molde si contiene el borde de la
    // pantalla. Es la misma figura que después se dibuja, así que la respuesta vale
    // para cualquier pantalla sin tener que repasar nada a mano.
    static double EscalaQueTapa(Point[] perfil, double w, double h)
    {
        var bordes = new List<Point>();
        for (int i = 0; i <= 8; i++)
        {
            double t = i / 8.0;
            bordes.Add(new Point(t * w, 0));
            bordes.Add(new Point(t * w, h));
            bordes.Add(new Point(0, t * h));
            bordes.Add(new Point(w, t * h));
        }

        bool Tapa(double e)
        {
            foreach (var p in bordes)
                if (!Nubecita.Dentro(perfil, (p.X - w / 2) / e, (p.Y - h / 2) / e)) return false;
            return true;
        }

        double poco = 1, mucho = Math.Max(w, h) * 6;
        if (!Tapa(mucho)) return mucho;                 // red: nunca debería pasar
        for (int i = 0; i < 34 && mucho - poco > 0.25; i++)
        {
            double medio = (poco + mucho) / 2;
            if (Tapa(medio)) mucho = medio; else poco = medio;
        }
        return mucho;
    }

    // ---------- La escena ----------
    //
    // Se construye todo de antemano y se devuelve una función que la pone en el instante
    // que se le pida. Por fotograma no se crea ni se mide nada: se escriben una escala,
    // una posición, un giro y seis opacidades.
    internal static Composicion Componer(double w, double h, Color acento, Guion g, Barra barra)
    {
        var lienzo = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
        double cx = w / 2, cy = h / 2;

        var perfil = Nubecita.Perfil();
        var nube = Nubecita.Nueva();

        // Los tres tamaños de la nube, y los tres son la misma nube.
        double escalaMini = Math.Max(80, Math.Min(260, Math.Min(w, h) * 0.17));
        double escalaLlena = EscalaQueTapa(perfil, w, h);
        double escalaTope = escalaLlena * 1.06;     // un pelo más, para que no se vea el filo
        double franja = Math.Min(barra.Franja, (barra.Borde is "arriba" or "abajo" ? h : w) * 0.33);
        double escalaIcono = Math.Clamp(franja * 0.58, 20, 90);

        // Y a dónde viaja al final: al centro de la franja de la barra. En modo isla la
        // nube de verdad está justo ahí, y en modo barra el centro es donde más cosas
        // hay, así que en los dos casos aterriza encima de la interfaz.
        (double X, double Y) destino = barra.Borde switch
        {
            "abajo" => (cx, h - franja / 2),
            "izquierda" => (franja / 2, cy),
            "derecha" => (w - franja / 2, cy),
            _ => (cx, franja / 2),
        };

        // ---------- La capa negra, que es la nube en negativo ----------
        //
        // Un rectángulo del tamaño de la pantalla y la nube, los dos en el mismo grupo
        // con regla PAR-IMPAR: lo que está dentro de los dos —la nube— se queda fuera
        // del relleno. O sea, un agujero con forma de nube, que crece cuando crece la
        // nube porque ES la nube.
        //
        // No hay máscara que mantener ni segunda animación que sincronizar, y el
        // escritorio no puede aparecer donde la nube no ha llegado.
        var hueco = new GeometryGroup { FillRule = FillRule.EvenOdd };
        hueco.Children.Add(new RectangleGeometry(new Rect(0, 0, w, h)));
        hueco.Children.Add(nube.Trazo);
        var tapa = new Path { Fill = Brushes.Black, Data = hueco, IsHitTestVisible = false };
        lienzo.Children.Add(tapa);

        // El cuerpo de la nube, que tapa su propio agujero mientras es una mascota y se
        // abre cuando se convierte en ventana.
        var relleno = new Path
        {
            Fill = Resplandor(AzulClaro, AzulNube, 0xFF, 0xFF),
            Data = nube.Trazo, IsHitTestVisible = false
        };
        lienzo.Children.Add(relleno);

        // El latido coral: se enciende desde el centro al despertar y da el último
        // chispazo al integrarse en la barra.
        var nucleo = new Ellipse
        {
            Fill = Resplandor(Coral, Coral, 0xCC, 0x00), Opacity = 0, IsHitTestVisible = false
        };
        var centroNucleo = new TranslateTransform();
        nucleo.RenderTransform = centroNucleo;
        lienzo.Children.Add(nucleo);

        // El halo y el filo: el borde luminoso de la ventana. Misma geometría otra vez,
        // así que no hay forma de que se separen de la nube.
        var halo = new Path
        {
            Stroke = Pincelada(AzulNube, 0x5C), Fill = null, Data = nube.Trazo,
            Opacity = 0, IsHitTestVisible = false
        };
        lienzo.Children.Add(halo);
        var filo = new Path
        {
            Stroke = Pincelada(AzulClaro, 0xDD), Fill = null, Data = nube.Trazo,
            Opacity = 0, IsHitTestVisible = false
        };
        lienzo.Children.Add(filo);

        var cara = Nubecita.Ojos(Color.FromRgb(0x17, 0x3A, 0x5C));
        cara.Visual.Opacity = 0;
        lienzo.Children.Add(cara.Visual);

        var suave = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var frena = new CubicEase { EasingMode = EasingMode.EaseOut };

        static void Opaco(UIElement e, double v)
        {
            if (Math.Abs(e.Opacity - v) > 0.003) e.Opacity = v;
        }

        double escala = escalaMini, x = cx, y = cy, ladeo = 0;
        double opRelleno = 1, opTapa = 1, opFilo = 0, opHalo = 0, opNucleo = 0, opCara = 0, ojos = 1;

        void Avanzar(double ms)
        {
            ladeo = 0; ojos = 1; opNucleo = 0; opRelleno = 1; opTapa = 1;
            x = cx; y = cy;

            // ---- 1. aparece ----
            // Crece desde un punto, no se funde desde transparente: si apareciera por
            // opacidad, su agujero en la capa negra ya estaría abierto y se vería el
            // escritorio asomando dentro de una nube a medio aparecer.
            if (ms < g.Entrada)
            {
                double k = frena.Ease(Math.Clamp(ms / g.Entrada, 0, 1));
                escala = escalaMini * (0.16 + 0.84 * k);
                opFilo = k; opHalo = k * 0.45;
            }
            // ---- 2. saluda ----
            // Sin brazos: un rebote, un ladeo que acompaña al rebote y un pulso de
            // brillo por cada bote, más un parpadeo en lo alto del primero. Eso es
            // suficiente para que se lea "hola" — de hecho se lee mejor que una mano.
            else if (ms < g.HastaSaludo)
            {
                double k = (ms - g.Entrada) / g.Saludo;
                double fase = k * g.Pulsos;
                double bote = Math.Abs(Math.Sin(fase * Math.PI));

                escala = escalaMini * (1 + 0.05 * bote);
                y = cy - escalaMini * 0.26 * bote;
                ladeo = -5.5 * Math.Sin(fase * 2 * Math.PI);
                opFilo = 1;
                opHalo = 0.30 + 0.45 * bote;
                opNucleo = 0.18 * bote;

                // El parpadeo, en lo alto del primer bote
                double d = Math.Abs(fase - 0.5);
                if (d < 0.11) ojos = 0.10 + 0.90 * (d / 0.11);
            }
            // ---- 3. despierta ----
            // Se enciende desde dentro y toma aire antes de abrirse.
            else if (ms < g.HastaDespierta)
            {
                double k = Math.Clamp((ms - g.HastaSaludo) / Math.Max(1, g.Despierta), 0, 1);
                escala = escalaMini * (1 + 0.11 * suave.Ease(k));
                opFilo = 1;
                opHalo = 0.30 + 0.60 * k;
                opNucleo = 0.85 * Math.Sin(Math.PI * k) + 0.15 * k;
            }
            // ---- 4. crece: la nube ES la ventana ----
            //
            // El cuerpo se apaga enseguida y lo que queda es el agujero: a partir de ahí
            // la nube no es una figura encima del escritorio, es por donde se ve.
            else if (ms < g.HastaCrece)
            {
                double k = Math.Clamp((ms - g.HastaDespierta) / g.Crece, 0, 1);
                // La escala se interpola por el ÁREA, no por el ancho. Lo que se ve
                // aparecer es superficie, y la superficie va con el cuadrado del ancho:
                // interpolando el ancho a secas, a mitad de la expansión solo se había
                // destapado una cuarta parte de la pantalla y el resto salía de golpe al
                // final. Medido antes: del 3 % al 69 % en 320 ms. Con la raíz, el
                // escritorio aparece a ritmo parejo de principio a fin.
                double e = k * 0.18 + suave.Ease(k) * 0.82;
                double desde = escalaMini * 1.11;
                escala = Math.Sqrt(desde * desde + (escalaTope * escalaTope - desde * desde) * e);
                opRelleno = 1 - Math.Clamp(k / 0.16, 0, 1);
                opFilo = 1;
                opHalo = 0.90;
                opNucleo = Math.Max(0, 0.15 - k * 0.6);

                // La capa negra solo empieza a irse cuando la nube YA tapa la pantalla
                // entera. Mientras eso no pasa vale 1, así que no hay un solo instante
                // en el que el escritorio pueda asomar por fuera de la nube.
                opTapa = 1 - Math.Clamp((escala - escalaLlena) / (escalaLlena * 0.05), 0, 1);
            }
            // ---- 5. llega: se recoge y se integra en la barra ----
            //
            // Aquí ya no hay nada tapado —el escritorio está entero a la vista— así que
            // esto no es una pausa, es la nube volviendo a su tamaño de icono y
            // viajando a donde está la barra de verdad. Y se funde con ella en vez de
            // desaparecer: lo que hay debajo cuando se apaga es la barra, en su sitio.
            else
            {
                double l = Math.Clamp((ms - g.HastaCrece) / g.Llega, 0, 1);
                double q = suave.Ease(l);
                opTapa = 0;

                // Al encoger, la escala va en PROPORCIÓN y no en píxeles: de 2700 px a
                // 30 en línea recta, la nube se pasaría media etapa siendo enorme y la
                // otra media encogiendo un pelo. En proporción —misma razón en cada
                // tramo de tiempo— llega a tamaño de mascota a mitad de camino, que es
                // cuando tiene que empezar a parecer otra vez un personaje.
                escala = escalaTope * Math.Pow(escalaIcono / escalaTope, q);

                // Y el viaje empieza cuando ya se ha hecho pequeña: arrastrar una nube
                // del tamaño de la pantalla hacia una esquina no se lee como viajar.
                double viaje = suave.Ease(Math.Clamp((q - 0.32) / 0.68, 0, 1));
                x = cx + (destino.X - cx) * viaje;
                y = cy + (destino.Y - cy) * viaje;

                // El cuerpo se vuelve a condensar según se encoge, y eso se mide por el
                // TAMAÑO, no por el tiempo: encenderlo mientras la nube todavía ocupa la
                // pantalla la convertía en una cortina azul y tapaba el escritorio que
                // acababa de destapar. Medido: al 25 % de esta etapa el escritorio se
                // iba del 100 % al 26 %.
                opRelleno = Math.Clamp((escalaMini * 2.4 - escala) / (escalaMini * 1.6), 0, 1);
                opFilo = 1;
                opHalo = 0.45 + 0.30 * Math.Sin(Math.PI * l);
                opNucleo = 0.65 * Math.Sin(Math.PI * Math.Clamp((l - 0.72) / 0.28, 0, 1));

                // Y el último tramo, ya parada encima de la barra, se funde con ella
                double fundido = 1 - Math.Clamp((l - 0.86) / 0.14, 0, 1);
                opRelleno *= fundido; opFilo *= fundido; opHalo *= fundido; opNucleo *= fundido;
            }

            // ---------- La cara, por una sola regla ----------
            // Los ojos son del CUERPO de la nube, así que valen lo que valga el cuerpo;
            // y además se van cuando la nube deja de tener tamaño de personaje. Una
            // ventana del tamaño de la pantalla no tiene cara.
            //
            // Las dos condiciones salen del tamaño y del relleno, que ya están
            // calculados, así que la cara vuelve sola al final —cuando la nube se recoge
            // para meterse en la barra— sin que haya que acordarse de encenderla.
            opCara = Math.Min(Math.Clamp((escalaMini * 1.95 - escala) / (escalaMini * 0.80), 0, 1), opRelleno);

            // ---------- Y ahora, a escribirlo ----------
            nube.Tamano.ScaleX = nube.Tamano.ScaleY = escala;
            nube.Ladeo.Angle = ladeo;
            nube.Centro.X = x; nube.Centro.Y = y;

            Opaco(tapa, opTapa);
            Opaco(relleno, opRelleno);
            Opaco(filo, opFilo);
            Opaco(halo, opHalo);
            Opaco(nucleo, opNucleo);
            Opaco(cara.Visual, opCara);

            // El grosor del halo y del filo va en píxeles de pantalla, no en unidades de
            // la nube: si fuera proporcional, al tamaño de ventana el borde sería una
            // franja de 200 px y al tamaño de icono no se vería.
            filo.StrokeThickness = Math.Clamp(escala * 0.014, 1.5, 9);
            halo.StrokeThickness = Math.Clamp(escala * 0.060, 7, 56);

            double rNucleo = escala * 0.40;
            nucleo.Width = nucleo.Height = rNucleo * 2;
            centroNucleo.X = x - rNucleo; centroNucleo.Y = y - rNucleo;

            cara.Tamano.ScaleX = cara.Tamano.ScaleY = Math.Min(escala, escalaMini * 2.0);
            cara.Centro.X = x; cara.Centro.Y = y;
            cara.Parpadeo.ScaleY = ojos;
        }

        Avanzar(0);
        return new Composicion(lienzo, Avanzar, g.Total,
            () => new Estampa(escala, x, y, ladeo, opRelleno, opTapa, opFilo, opHalo,
                              opNucleo, opCara, ojos, escalaLlena));
    }

    // Tope duro: pase lo que pase, a los 9 s no hay escena. Es la última red contra
    // quedarse con la pantalla negra si la interfaz nunca avisa.
    const double MsTope = 9000;

    readonly Guion _guion;
    readonly Monitores.Info _monitor;
    readonly Color _acento;
    readonly Barra _barra;              // dónde va la barra, para construirla la última
    readonly bool _apuntarVista;        // hay que escribir RobotVisto al terminar

    // El pulso NO anima nada: solo mira si has pulsado Esc o el botón. La animación va
    // por fotograma, en CompositionTarget.Rendering.
    readonly DispatcherTimer _pulso = new() { Interval = TimeSpan.FromMilliseconds(60) };
    readonly DispatcherTimer _tope = new() { Interval = TimeSpan.FromMilliseconds(MsTope) };
    readonly Stopwatch _reloj = Stopwatch.StartNew();

    bool _listo;                        // la interfaz ha avisado
    bool _revelando;                    // la brocha ya está destapando la pantalla
    double _esperandoDesde;             // cuándo empezó la espera a la nube (0 = no hizo falta)

    // ---------- El reloj de la escena ----------
    // Normalmente es el reloj de verdad. Se separa de él en dos casos, los dos a
    // propósito: cuando la escena espera a la nube antes de destapar (se queda quieto)
    // y cuando te la saltas (corre más). Todo lo demás —las cinco etapas y el recorrido
    // de la brocha— sale solo de este número, así que no hay nada que sincronizar.
    double _enEscena;                   // instante de la escena en el último ajuste
    double _origen;                     // instante real de ese mismo ajuste
    double _prisa = 1;                  // >1 solo si te la has saltado

    Composicion? _escena;
    EventHandler? _porFotograma;
    IntPtr _self;

    // ---------- Medición ----------
    // Un resumen de una línea al terminar. No es diagnóstico de usar y tirar: si algún
    // día esto vuelve a ir a tirones, lo primero que hace falta es saber cuántos
    // fotogramas salieron y contra qué refresco.
    readonly List<double> _frames = new(1024);
    double _ultimoFrame;

    EscenaArranque(Monitores.Info monitor, Color acento, Barra barra, bool apuntarVista)
    {
        _monitor = monitor;
        _acento = acento;
        _barra = barra;
        _apuntarVista = apuntarVista;
        _guion = Tiempos();

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

        // Calidad: nada de bajar el listón para ganar fotogramas. Se deja el suavizado
        // en su sitio y se pide el mejor escalado, porque lo que se arregló fue de
        // dónde salían los fotogramas, no cuánto se dibuja en cada uno.
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        UseLayoutRounding = false;
        SnapsToDevicePixels = false;

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
        // Colgarlo de ContentRendered costaba segundo y medio: justo detrás de esto,
        // App llama a MainWindow.Arrancar(). Las medidas salen del monitor en unidades
        // de WPF, que es lo que WPF le va a dar a esta ventana ahí.
        Montar();

        _pulso.Tick += (_, _) =>
        {
            // Mientras la escena está tapando, se reclama el primer plano. La nube se
            // crea DESPUÉS que la escena y también es Topmost, así que sin esto la barra
            // de verdad aparecería encima del negro a mitad de la construcción y se
            // vería el truco. En cuanto empieza a destaparse se deja de reclamar, que es
            // justo cuando la nube tiene que verse.
            if (!_revelando && _self != IntPtr.Zero) Nativo.SiempreEncima(_self);
            if (Saltar()) Saltar("te la has saltado");
        };
        _tope.Tick += (_, _) => Cerrar("tope de seguridad de 9 s");
        _pulso.Start();
        _tope.Start();
    }

    void Montar()
    {
        if (_escena != null) return;
        try
        {
            double w = _monitor.Ancho / _monitor.Escala, h = _monitor.Alto / _monitor.Escala;
            _escena = Componer(w, h, _acento, _guion, _barra);
            ((Grid)Content).Children.Add(_escena.Lienzo);

            _porFotograma = (_, _) => Fotograma();
            CompositionTarget.Rendering += _porFotograma;
            Log.Info($"escena de entrada: {_guion.Total:0} ms de guion " +
                     $"(entra {_guion.Entrada:0} · saluda {_guion.Saludo:0} x{_guion.Pulsos} · " +
                     $"despierta {_guion.Despierta:0} · crece {_guion.Crece:0} · " +
                     $"llega {_guion.Llega:0}) · barra {_barra.Borde} " +
                     $"de {_barra.Franja:0} px · pantalla {_monitor.Dispositivo} " +
                     $"a {Monitores.Hercios(_monitor.Dispositivo):0.##} Hz");
        }
        catch (Exception ex)
        {
            Log.Error("montar la escena de entrada", ex);
            Cerrar("falló al dibujarse");
        }
    }

    // ---------- Un fotograma ----------
    //
    // Esto es lo único que corre por fotograma, y lo hace **por tiempo transcurrido**.
    // Ni cuenta fotogramas ni suma un paso fijo: pregunta qué hora es y dibuja la
    // escena en ese instante. Por eso dura lo mismo a 60 que a 240 Hz, y a 240 lo que
    // hay es el doble de fotogramas, no media escena.
    void Fotograma()
    {
        try
        {
            if (_escena is not { } e) return;

            double bruto = _reloj.Elapsed.TotalMilliseconds;
            if (_ultimoFrame > 0) _frames.Add(bruto - _ultimoFrame);
            double delta = _ultimoFrame > 0 ? bruto - _ultimoFrame : 0;
            _ultimoFrame = bruto;

            double t = Instante(bruto);

            // La ÚNICA espera que queda, y solo como red: si cuando la nubecita va a
            // abrirse la interfaz todavía no ha avisado de que está montada, el reloj de
            // la escena se queda quieto —no se alarga una etapa, que descuadraría las
            // demás— hasta que avise o hasta el tope.
            //
            // Y espera AQUÍ, con la nube todavía cerrada y antes de destapar nada: al
            // abrirse va enseñando el escritorio, así que esperar a mitad dejaría media
            // pantalla con la interfaz a medio montar, que es exactamente lo que esta
            // escena viene a tapar. Medido en esta máquina no llega a ocurrir: la nube
            // avisa sobre los 600 ms y no se abre hasta los ~1420.
            //
            // Si te la has saltado NO se espera a nadie: has pedido verlo ya.
            if (!_revelando && _prisa == 1 && t >= _guion.HastaDespierta && !_listo && bruto < MsTope * 0.6)
            {
                if (_esperandoDesde == 0) _esperandoDesde = bruto;
                _origen += delta;
                t = Instante(bruto);
            }

            if (!_revelando && t >= _guion.HastaDespierta)
            {
                _revelando = true;
                if (_apuntarVista) ApuntarQueSeHaVisto();
                string espera = _esperandoDesde > 0
                    ? $"tras esperar {bruto - _esperandoDesde:0} ms a la nube"
                    : "sin esperar nada: retardo cero";
                Log.Info($"escena de entrada: la nube se abre a los {bruto:0} ms, {espera}" +
                         (_listo ? "" : " (la interfaz no avisó: se abre igual)"));
            }

            e.Avanzar(t);

            if (t >= e.Total) Cerrar("revelada");
        }
        catch (Exception ex)
        {
            Log.Error("fotograma de la escena de entrada", ex);
            Cerrar("falló un fotograma");
        }
    }

    // Esc o un clic. La ventana no recibe entrada a propósito (los clics la
    // atraviesan), así que se pregunta al sistema, igual que hace el arrastre.
    static bool Saltar()
    {
        try { return Nativo.TeclaPulsada(0x1B) || Nativo.BotonIzquierdoPulsado(); }
        catch { return false; }
    }

    // Saltarse la escena no la corta de golpe. Pone el reloj en el instante en que la
    // nube se abre —si no había llegado— y lo hace correr al cuádruple: se ve lo mismo,
    // la nube abriéndose, destapando el escritorio y metiéndose en la barra, pero en
    // 445 ms en vez de 1780. Cortar en seco dejaría un salto de negro a escritorio, y lo
    // que hay debajo del negro es justo lo que se quiere ver. Es una cuenta aparte para
    // poder probarla sin abrir ninguna ventana.
    internal static (double EnEscena, double Prisa) AlSaltar(double t, Guion g) => (Math.Max(t, g.HastaDespierta), 4);

    double Instante(double bruto) => _enEscena + (bruto - _origen) * _prisa;

    void Saltar(string motivo)
    {
        if (_prisa > 1) return;
        double bruto = _reloj.Elapsed.TotalMilliseconds;
        (_enEscena, _prisa) = AlSaltar(Instante(bruto), _guion);
        _origen = bruto;
        _pulso.Stop();
        Log.Info($"escena de entrada: {motivo}, la construcción termina a toda prisa");
    }

    void DejarDeDibujar()
    {
        if (_porFotograma == null) return;
        CompositionTarget.Rendering -= _porFotograma;
        _porFotograma = null;
        ApuntarLosFotogramas();
    }

    void ApuntarLosFotogramas()
    {
        if (_frames.Count < 2) return;
        try
        {
            double hz = Monitores.Hercios(_monitor.Dispositivo);
            double periodo = hz > 1 ? 1000.0 / hz : 16.67;
            var orden = _frames.OrderBy(x => x).ToList();
            double total = _frames.Sum();
            int conTiron = _frames.Count(x => x > periodo * 1.5);
            double saltadas = _frames.Where(x => x > periodo * 1.5).Sum(x => Math.Round(x / periodo) - 1);

            Log.Info($"escena de entrada: {_frames.Count + 1} fotogramas en {total:0} ms · " +
                     $"{1000.0 * _frames.Count / total:0.0} FPS efectivos de {hz:0.##} Hz posibles · " +
                     $"frame p50={orden[orden.Count / 2]:0.00} ms p95={orden[(int)(orden.Count * 0.95)]:0.00} " +
                     $"peor={orden[^1]:0.00} · con tirón {conTiron} ({100.0 * conTiron / _frames.Count:0.0}%) · " +
                     $"presentaciones saltadas {saltadas:0}");
        }
        catch (Exception ex) { Log.Error("apuntar los fotogramas", ex); }
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
