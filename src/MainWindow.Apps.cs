using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// El carrusel de apps abiertas: agrupa las ventanas del mismo programa, limita
// cuántos iconos se ven a la vez y deja desplazarse con la rueda.
public partial class MainWindow
{
    // Un programa con todas sus ventanas abiertas
    sealed class Grupo
    {
        public string Exe = "";
        public List<IntPtr> Ventanas = new();
        public IntPtr Ultima;              // la última de este grupo que estuvo activa
        public IntPtr InicioCiclo;         // por dónde empezó la vuelta actual al grupo
        public bool Fijada;                // está anclada: se ve aunque no esté abierta
        public string Lanzar = "";         // qué abrir si está fijada y cerrada
        public Button? Boton;
        public StackPanel? Puntos;
        public ImageSource? Icono;
    }

    // La ventana activa DE VERDAD. No vale GetForegroundWindow a secas: el menú
    // Inicio y la ventana de ajustes son nuestras y sí cogen el foco, así que
    // taparían a la app que el usuario tenía delante.
    IntPtr ActivaAjena()
    {
        var f = Nativo.Activa();
        if (f != IntPtr.Zero && !Nativo.EsNuestra(f)) _ultimaActiva = f;
        return _ultimaActiva;
    }

    // Quién mandaba justo ANTES de pulsar. Se apunta en el mouse-down, no en el
    // Click: si no, cualquier cambio de foco por el camino decidiría por nosotros.
    IntPtr _activaAlPulsar;

    void ApuntarActivaAlPulsar() => _activaAlPulsar = ActivaAjena();

    // ================= La ventana que conocíamos y se ha escondido =================
    //
    // Discord, Steam, Telegram y casi todo lo que vive en la bandeja no se minimizan al
    // pulsar minimizar o la X: **se esconden**. La ventana sigue existiendo pero deja de ser
    // visible, así que sale de la lista y el grupo se queda con cero ventanas. Y entonces
    // NubeBar hacía lo peor posible: relanzar el ejecutable, que no hace nada porque ya hay
    // un proceso corriendo. Resultado: pulsar el icono no producía ningún efecto.
    //
    // ---------- Por qué se RECUERDA en vez de buscarla ----------
    //
    // La primera versión de esto recorría todas las ventanas de nivel superior invisibles
    // buscando una «que parezca la principal» de ese ejecutable. Falló dos veces seguidas en
    // el diagnóstico: encontraba otra ventana del mismo proceso. Y tiene sentido — un proceso
    // cualquiera tiene varias ventanas invisibles con título, y **adivinar cuál es la buena
    // es adivinar**. Enseñar la equivocada sería peor que no hacer nada.
    //
    // Pero no hace falta adivinar: si el usuario ha usado esa app, su ventana estuvo en la
    // lista y `g.Ultima` la guarda. Así que se comprueba esa, y solo esa.
    //
    // Lo que este camino NO cubre: una app que ya estaba escondida antes de arrancar NubeBar.
    // Ahí no hay nada recordado, y pulsar el icono la lanza como siempre.
    IntPtr EscondidaRecordada(Grupo g)
    {
        var h = g.Ultima;
        if (h == IntPtr.Zero || !Nativo.Existe(h)) return IntPtr.Zero;
        return Nativo.Ficha(h).Visible ? IntPtr.Zero : h;
    }

    // Lo mismo para el dock, que no tiene el grupo a mano sino su clave.
    internal IntPtr EscondidaRecordadaPorClave(string clave)
    {
        foreach (var g in _grupos.Values)
            if (string.Equals(Dock.Clave(g.Exe), clave, StringComparison.OrdinalIgnoreCase))
                return EscondidaRecordada(g);
        return IntPtr.Zero;
    }

    // Minimizar y, si la app no atiende el mensaje, insistir por las malas
    void MinimizarVentana(IntPtr h)
    {
        Log.Info($"minimizando \"{Nativo.Titulo(h)}\"");
        Nativo.Minimizar(h);
        Dispatcher.InvokeAsync(async () =>
        {
            await System.Threading.Tasks.Task.Delay(400);
            if (!Nativo.EstaMinimizada(h))
            {
                Log.Info("no atendió a WM_SYSCOMMAND: se minimiza con ShowWindow");
                Nativo.MinimizarAlaFuerza(h);
            }
            // La rayita se va a la que haya quedado delante
            RefrescarApps();
        });
    }

    const double AnchoIcono = 34;          // lo que ocupa cada botón de app, incluidos márgenes
    const double DesvanecidoBorde = 18;    // ancho del degradado de los extremos

    readonly Dictionary<string, Grupo> _grupos = new(StringComparer.OrdinalIgnoreCase);
    Border? _carrusel;                     // recorta y desvanece
    StackPanel? _tira;                     // la fila de botones que se desplaza
    TranslateTransform? _desplazamiento;
    double _objetivoDesplazamiento;
    IntPtr _activaPrevia;
    bool _primeraCarga = true;
    DispatcherTimer? _tiempoMiniLista;
    Grupo? _grupoSenalado;

    double _anchoTira;                     // largo natural de la tira entera, medido

    double AnchoVisible => Math.Max(AnchoIcono, MaxAppsEfectivo * AnchoIcono);
    double DesplazamientoMaximo => Math.Max(0, _anchoTira - AnchoVisible);

    // ---------- El eje de la tira ----------
    //
    // El carrusel es una tira más larga que el hueco que se le da, desplazándose por
    // dentro de una ventanita. En la barra horizontal eso pasa en X y lo que se mide es
    // el ancho; en la vertical, en Y y lo que se mide es el alto.
    //
    // Estos cinco accesos son los ÚNICOS sitios que lo saben. El resto del carrusel
    // —la rueda, la inercia, el desvanecido de los extremos, traer a la vista la app
    // que se acaba de activar— está escrito una sola vez y vale para los dos.
    DependencyProperty EjeTira => Vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;

    double Desplazado => _desplazamiento == null ? 0
                       : Vertical ? _desplazamiento.Y : _desplazamiento.X;

    void PonerDesplazado(double v)
    {
        if (_desplazamiento == null) return;
        if (Vertical) _desplazamiento.Y = v; else _desplazamiento.X = v;
    }

    // El largo de la ventanita por la que se ve la tira
    double LargoCarrusel => _carrusel == null ? 0 : Vertical ? _carrusel.Height : _carrusel.Width;

    void PonerLargoCarrusel(double v)
    {
        if (_carrusel == null) return;
        if (Vertical) _carrusel.Height = v; else _carrusel.Width = v;
    }

    // Para el log de diagnóstico: qué grupos hay, en qué orden y con cuántas ventanas
    string ResumenGrupos()
    {
        if (_tira == null) return "sin carrusel";
        var nombres = _tira.Children.OfType<Button>()
            .Select(b => _grupos.Values.FirstOrDefault(g => g.Boton == b))
            .Where(g => g != null)
            .Select(g => $"{System.IO.Path.GetFileName(g!.Exe)}x{g.Ventanas.Count}");
        var detalle = "";
        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG2") == "1" && _carrusel != null)
            detalle = " {" + string.Join(" ", _tira.Children.OfType<Button>().Select(b =>
            {
                try
                {
                    var p = b.TransformToAncestor(_carrusel).Transform(new Point(0, 0));
                    return $"{p.X:0}/{b.ActualWidth:0}/op{b.Opacity:0.0}";
                }
                catch { return "?"; }
            })) + "}";
        double x0 = -1;
        try { if (_carrusel != null) x0 = Left + _carrusel.TransformToAncestor(this).Transform(new Point(0, 0)).X; }
        catch { }
        return $"tira={_anchoTira:0} visible={LargoCarrusel:0} x0={x0:0} " +
               $"desp={Desplazado:0} [{string.Join(" ", nombres)}]{detalle}";
    }

    // ================= Construcción =================
    UIElement ModuloApps()
    {
        bool v = Vertical;
        double grosorBoton = Math.Max(20, _cfg.Barra.Alto - 8);

        // En vertical la tira se apila y lo que se fija es su ANCHO; en horizontal va
        // tumbada y lo que se fija es su alto. De ahí para abajo, lo mismo.
        _tira = new StackPanel { Orientation = v ? Orientation.Vertical : Orientation.Horizontal };
        if (v) _tira.Width = grosorBoton; else _tira.Height = grosorBoton;
        _desplazamiento = new TranslateTransform();
        _tira.RenderTransform = _desplazamiento;

        // La tira va dentro de un Canvas y no directamente en el Border. Si no, WPF le
        // aplica el recorte de layout: como la tira mide más que el hueco que se le da,
        // la recorta EN SUS PROPIAS COORDENADAS, y al desplazarla solo quedaba visible
        // el trocito final. El Canvas da sitio infinito a sus hijos y no los recorta;
        // el recorte bueno, el de la ventanita que se ve, lo hace el Border.
        // ---------- Y sitio para que un icono se levante ----------
        //
        // El recorte medía justo lo que los botones, y al pasar el ratón el icono sube 2 px
        // y crece un 8 % con rebote: lo que asomaba por encima se cortaba. Lo contó el
        // usuario: «cuando me pongo encima de una app hay veces que se cortan por arriba».
        // Se le da al recorte el hueco que deja la barra a cada lado (4 px con el grosor
        // de serie) y la tira se baja eso mismo: los iconos siguen exactamente donde estaban.
        double holgura = Math.Max(0, (_cfg.Barra.Alto - grosorBoton) / 2);
        double grosorRecorte = grosorBoton + holgura * 2;

        var lienzo = new Canvas();
        if (v) lienzo.Width = grosorRecorte; else lienzo.Height = grosorRecorte;
        if (v) Canvas.SetLeft(_tira, holgura); else Canvas.SetTop(_tira, holgura);
        lienzo.Children.Add(_tira);

        _carrusel = new Border
        {
            ClipToBounds = true,
            Child = lienzo,
            HorizontalAlignment = v ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = v ? VerticalAlignment.Top : VerticalAlignment.Center,
            Background = Brushes.Transparent     // para que la rueda llegue aunque sobre hueco
        };
        if (v) _carrusel.Width = grosorRecorte; else _carrusel.Height = grosorRecorte;
        _carrusel.MouseWheel += Carrusel_Rueda;
        _carrusel.SizeChanged += (_, _) => PintarDesvanecido();

        _grupos.Clear();
        _activaPrevia = IntPtr.Zero;
        _primeraCarga = true;

        RefrescarApps();
        _actualizadores.Add(RefrescarApps);

        // El botón de la vista general va detrás del carrusel pero FUERA de él:
        // dentro se desplazaría con la rueda y dejaría de estar a mano
        return new StackPanel
        {
            Orientation = v ? Orientation.Vertical : Orientation.Horizontal,
            Children = { _carrusel, CrearBotonVista() }
        };
    }

    // ================= Refresco =================
    void RefrescarApps()
    {
        // Sin carrusel todavía se puede estar enseñando algo: las fijadas tienen su
        // propia tira (módulo "fijadas") y viven aunque "apps" no esté puesto
        if ((_tira == null || _carrusel == null) && !FijasAparte) return;

        var activa = ActivaAjena();

        var abiertas = Nativo.VentanasAbiertas(_self);
        // Nuestras propias ventanas (la de ajustes, sobre todo) NO son apps del
        // carrusel: si no, salía un icono de NubeBar y "cerrar todas" o "forzar el
        // cierre" podían acabar matando a la propia NubeBar.
        abiertas.RemoveAll(Nativo.EsNuestra);
        // La caché de ejecutables se limpia con TODAS las ventanas, no solo con las
        // de este monitor: si no, cada isla le borraría a la otra lo que sí sigue vivo
        Nativo.OlvidarExes(new HashSet<IntPtr>(abiertas));

        // Cada isla enseña las apps de su pantalla. Al mover una ventana de monitor
        // su grupo desaparece de una isla y aparece en la otra, y eso ya tiene
        // animación de salida y de entrada más abajo: el icono "viaja" solo.
        abiertas = SoloDeMiMonitor(abiertas);

        // Y, si lo pides, solo las del escritorio virtual en el que estás
        if (_cfg.Barra.AppsDelEscritorio && Escritorios.Disponible)
            abiertas = abiertas.Where(Escritorios.EstaEnElActual).ToList();

        // Repartir las ventanas en grupos. Sin agrupar, cada ventana es su propio grupo.
        var ahora = new Dictionary<string, List<IntPtr>>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in abiertas)
        {
            var clave = _cfg.Barra.AgruparApps
                ? (Nativo.Exe(h) is { Length: > 0 } e ? e : "hwnd:" + h)
                : "hwnd:" + h;
            if (!ahora.TryGetValue(clave, out var lista)) ahora[clave] = lista = new List<IntPtr>();
            lista.Add(h);
        }

        // Las fijadas entran aunque no estén abiertas, con la lista de ventanas
        // vacía. Si ya estaban abiertas no se duplican: comparten clave con su
        // grupo, así que el icono es el mismo y solo se le enciende la marca.
        var fijadas = Fijadas.Select(ClaveDeFijada).ToList();
        foreach (var f in fijadas)
            if (!ahora.ContainsKey(f)) ahora[f] = new List<IntPtr>();

        // Sin módulo "apps" no hay dónde poner las que no están fijadas
        if (_tira == null)
            foreach (var clave in ahora.Keys.Where(k => !EstaFijada(k)).ToList())
                ahora.Remove(clave);

        // Grupos que ya no existen: se encogen y se van
        foreach (var clave in _grupos.Keys.Where(k => !ahora.ContainsKey(k)).ToList())
        {
            var g = _grupos[clave];
            _grupos.Remove(clave);
            if (g.Boton is { } b)
                AnimarSalida(b, () => { (b.Parent as Panel)?.Children.Remove(b); TrasCambiarApps(); });
        }

        // Grupos nuevos y actualización de los que siguen.
        // Primero las fijadas, en el orden en que las pusiste tú, y detrás las demás
        // por nombre de ejecutable: los botones se añaden al final según van
        // apareciendo, así que sin ordenar aquí saldrían en distinto sitio cada vez.
        // Si has reordenado a mano arrastrando, mandas tú. Lo que no esté en tu
        // orden va detrás: primero las fijadas y después el resto por nombre.
        int orden = 0;
        var tuyo = _cfg.Barra.OrdenApps ?? new List<string>();
        var enOrden = ahora
            .OrderBy(p => Indice(tuyo, p.Key) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(p => Indice(fijadas, p.Key) is var j && j >= 0 ? j : int.MaxValue)
            .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        static int Indice(List<string> lista, string clave) =>
            lista.FindIndex(x => x.Equals(clave, StringComparison.OrdinalIgnoreCase));

        foreach (var (clave, ventanas) in enOrden)
        {
            if (_grupos.TryGetValue(clave, out var g))
            {
                // Conservar el orden que ya tenía y añadir las nuevas al final.
                // VentanasAbiertas devuelve orden Z, que cambia cada vez que activas una:
                // si nos fiáramos de él, ciclar por el grupo iría y vendría entre dos.
                var conocidas = new HashSet<IntPtr>(ventanas);
                g.Ventanas = g.Ventanas.Where(conocidas.Contains)
                                       .Concat(ventanas.Where(h => !g.Ventanas.Contains(h)))
                                       .ToList();
                g.Fijada = EstaFijada(clave);
                // La acabas de fijar (o de quitar): su botón se cambia de tira sin
                // perderlo, que es lo que pasaría si esperásemos a la recarga del config
                if (g.Boton is { } bot && TiraDe(g) is { } suya && !ReferenceEquals(bot.Parent, suya))
                {
                    (bot.Parent as Panel)?.Children.Remove(bot);
                    suya.Children.Add(bot);
                }
                ActualizarGrupo(g, activa);
                continue;
            }
            g = new Grupo
            {
                Exe = clave,
                Ventanas = ventanas,
                Ultima = ventanas.Count > 0 ? ventanas[0] : IntPtr.Zero,
                Fijada = EstaFijada(clave),
                Lanzar = FijadaDeClave(clave)
            };
            _grupos[clave] = g;
            var tira = TiraDe(g);
            if (tira == null) { _grupos.Remove(clave); continue; }
            CrearBotonGrupo(g, enElCarrusel: ReferenceEquals(tira, _tira));
            tira.Children.Add(g.Boton);
            AnimarEntrada(g.Boton!, _primeraCarga ? orden++ * 45 : 0);
            ActualizarGrupo(g, activa, inmediato: true);
        }
        _primeraCarga = false;

        // La ventana activa ha cambiado de sitio: mover la marca y traerla a la vista
        if (activa != _activaPrevia)
        {
            _activaPrevia = activa;
            foreach (var g in _grupos.Values)
            {
                if (g.Ventanas.Contains(activa)) g.Ultima = activa;
                ActualizarGrupo(g, activa);
            }
            MostrarGrupoDe(activa);
        }

        TrasCambiarApps();
    }

    // ¿En qué tira va este grupo? Las fijadas, en la que se ve también con la isla
    // recogida (módulo "fijadas"); las demás, en el carrusel.
    Panel? TiraDe(Grupo g) => g.Fijada && _tiraFijas != null ? _tiraFijas : _tira;

    // Tras añadir o quitar iconos hay que recalcular el ancho visible y el degradado
    void TrasCambiarApps()
    {
        OrdenarFijas();
        if (_carrusel == null || _tira == null) { AjustarAncho(); return; }
        // Medir sin límite (no UpdateLayout, que fuerza una pasada entera cada segundo)
        _tira.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _anchoTira = Vertical ? _tira.DesiredSize.Height : _tira.DesiredSize.Width;
        _tira.InvalidateMeasure();
        PonerLargoCarrusel(Math.Min(_anchoTira, AnchoVisible));
        LimitarDesplazamiento();
        PintarDesvanecido();
        ActualizarBotonVista();
        AjustarAncho();
    }

    // ---------- Que un icono negro se vea sobre la isla negra ----------
    //
    // Un icono que es un trazo oscuro (ChatGPT, por ejemplo) desaparecía sobre el fondo de
    // la isla. No se le cambia el color —es el icono de la app y se tiene que reconocer—:
    // se le pone detrás un halo claro, que le dibuja el contorno. Solo con fondo oscuro:
    // en un tema claro el negro ya se lee.
    internal const double FondoClaro = 0.4;

    Image Legible(Image img)
    {
        try
        {
            if (Contraste.Luminancia(_roles.FondoOpaco) < FondoClaro && Iconos.EsOscuro(img.Source))
                img.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = _roles.ColorDe("icono.halo"), ShadowDepth = 0, BlurRadius = 6, Opacity = 0.85,
                };
        }
        catch { }
        return img;
    }

    void CrearBotonGrupo(Grupo g, bool enElCarrusel = true)
    {
        // Una app fijada y cerrada no tiene ventana de la que sacar el icono:
        // se lo pedimos al shell a partir de su ruta o de su AppUserModelId.
        bool cerrada = g.Ventanas.Count == 0;
        var titulo = cerrada ? NombreDeClave(g.Exe) : Nativo.Titulo(g.Ventanas[0]);

        // ---------- Y si la ventana no contesta, se le pregunta al shell ----------
        //
        // El icono de una app abierta sale de su VENTANA (`WM_GETICON`, con 50 ms de
        // plazo), que es lo correcto: es el icono que la app ha elegido enseñar ahora, y
        // en las que cambian de icono —un reproductor, un instalador— es el bueno.
        //
        // Pero una ventana recién creada todavía no lo ha puesto, y una app ocupada no
        // contesta en 50 ms. Entonces esto devolvía null y el botón se quedaba con la
        // INICIAL del título: la «D» de Discord. Para el usuario eso no es «aún no sé el
        // icono», es «NubeBar no reconoce mi app».
        //
        // El respaldo es el mismo sitio del que ya salía el icono de una app fijada y
        // cerrada: el shell, a partir del ejecutable. Nunca está peor que una letra, y
        // llega siempre. Y como `RefrescarApps` vuelve a pasar cada segundo, en cuanto la
        // ventana conteste se queda con el suyo.
        g.Icono = cerrada
            ? Iconos.De(g.Lanzar, 32)
            : Nativo.Icono(g.Ventanas[0]) ?? Iconos.De(g.Lanzar, 32) ?? Iconos.De(g.Exe, 32);
        UIElement visual = g.Icono != null
            ? Legible(new Image { Source = g.Icono, Width = 18, Height = 18 })
            : Texto(titulo.Length > 0 ? titulo[..1] : "?", 14);

        // Puntitos: uno por ventana del grupo, con un máximo de tres
        g.Puntos = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        };

        var contenido = new StackPanel { Children = { visual, g.Puntos } };
        g.Boton = Boton(contenido, titulo, () => PulsarGrupo(g));
        // Se apunta quién mandaba ANTES del clic: para cuando salte el Click ya
        // podría haber cambiado el foco y nunca se minimizaría nada
        g.Boton.PreviewMouseLeftButtonDown += (_, _) => ApuntarActivaAlPulsar();
        g.Boton.MouseEnter += (_, _) => SenalarGrupo(g);
        g.Boton.MouseLeave += (_, _) => OlvidarGrupo(g);

        // Clic derecho: fijar o quitar de la nube. Se marca como manejado para que
        // no suba hasta la isla y abra SU menú contextual en vez de este.
        g.Boton.MouseRightButtonUp += (_, e) => { e.Handled = true; MenuDeGrupo(g); };

        // Y se puede coger y arrastrar para cambiarla de sitio en el carrusel. En la
        // tira de las fijas no: ahí el orden es el de Barra.AppsFijadas (Ajustes > Apps)
        if (enElCarrusel) ConReordenado(g.Boton, g);
    }

    // Estado visual del grupo: cuántos puntos y de qué color
    void ActualizarGrupo(Grupo g, IntPtr activa, bool inmediato = false)
    {
        if (g.Boton == null || g.Puntos == null) return;

        bool tieneLaActiva = g.Ventanas.Contains(activa);
        int cuantos = Math.Min(3, g.Ventanas.Count);
        bool cerrada = g.Ventanas.Count == 0;

        // Una app fijada pero cerrada se ve apagada y sin puntitos: se nota de un
        // vistazo que está ahí para abrirla, no para volver a ella.
        double opacidad = cerrada ? 0.45 : 1;
        if (Math.Abs(g.Boton.Opacity - opacidad) > 0.01)
        {
            double antes = g.Boton.Opacity;
            g.Boton.BeginAnimation(OpacityProperty, null);
            g.Boton.Opacity = opacidad;
            if (!inmediato) g.Boton.BeginAnimation(OpacityProperty, Kf(antes, opacidad, 260));
        }

        // Con una sola ventana, tooltip normal. Con varias no, porque el que manda
        // es la mini lista y el tooltip se le pondría encima tapando la primera fila.
        g.Boton.ToolTip = cerrada ? NombreDeClave(g.Exe)
                        : g.Ventanas.Count == 1 ? Nativo.Titulo(g.Ventanas[0]) : null;

        // Crear o quitar puntos hasta llegar al número que toca
        while (g.Puntos.Children.Count > cuantos) g.Puntos.Children.RemoveAt(g.Puntos.Children.Count - 1);
        while (g.Puntos.Children.Count < cuantos)
            g.Puntos.Children.Add(new Border
            {
                Height = 3,
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(1.5, 0, 1.5, 0),
                Background = new SolidColorBrush(_cTexto)
            });

        double ms = inmediato ? 0 : 380;
        foreach (Border punto in g.Puntos.Children)
        {
            // El grupo activo: puntos del color de acento y a plena opacidad
            double hasta = tieneLaActiva ? 1 : 0.45;
            double ahora = punto.Opacity;
            punto.BeginAnimation(OpacityProperty, null);
            punto.Opacity = hasta;
            AnimarColor((SolidColorBrush)punto.Background, tieneLaActiva ? _cAcento : _cTexto, ms);
            if (!inmediato) punto.BeginAnimation(OpacityProperty, Kf(ahora, hasta, ms));
        }

        // Un solo punto en el grupo activo se estira como la rayita de antes
        if (g.Puntos.Children.Count == 1 && g.Puntos.Children[0] is Border unico)
        {
            double hasta = tieneLaActiva ? 16 : 5;
            double ahora = unico.ActualWidth > 0 ? unico.ActualWidth : unico.Width;
            unico.BeginAnimation(WidthProperty, null);
            unico.Width = hasta;
            if (!inmediato) unico.BeginAnimation(WidthProperty, Kf(ahora, hasta, ms, 0, Muelle));
        }
    }

    // ================= Pulsar =================
    // Como en la barra de tareas de Windows: si pulsas la app que ya tienes
    // delante, se minimiza. Con varias ventanas se va pasando de una a otra y,
    // al dar la vuelta completa, se minimiza el grupo entero.
    void PulsarGrupo(Grupo g)
    {
        // Fijada y cerrada: se abre, que es para lo que está ahí
        if (g.Ventanas.Count == 0)
        {
            CerrarMiniLista();
            if (!g.Fijada) return;

            // ---------- ¿Cerrada, o solo escondida? ----------
            //
            // No es lo mismo, y aquí se confundían. Discord, Steam, Telegram y casi todo lo
            // que vive en la bandeja no se minimizan: **se esconden**. La ventana sigue
            // existiendo pero deja de ser visible, así que sale de la lista de ventanas y
            // este código daba la app por cerrada.
            //
            // Y entonces hacía lo peor posible: volver a lanzar el ejecutable. El segundo
            // proceso ve que ya hay uno corriendo y se va sin hacer nada, así que **pulsar el
            // icono no producía ningún efecto** y la app parecía irrecuperable. Es el fallo
            // que contó el usuario: «clico dos veces y ya no me deja volver a abrirla».
            //
            // Ahora se busca primero la ventana escondida de ese mismo ejecutable y se
            // enseña, que es lo que hace la barra de tareas de Windows. Si no hay ninguna se
            // lanza como siempre: en el peor caso, el comportamiento de antes.
            var escondida = EscondidaRecordada(g);
            if (escondida != IntPtr.Zero)
            {
                Log.Info($"fijada {System.IO.Path.GetFileName(g.Exe)}: no estaba cerrada, estaba " +
                         $"ESCONDIDA en 0x{escondida.ToInt64():X6} (\"{Nativo.Titulo(escondida)}\"); " +
                         "se enseña en vez de relanzarla");
                Nativo.MostrarYActivar(escondida);
                return;
            }

            Log.Info($"abriendo la app fijada {g.Lanzar}");
            if (g.Boton != null) LatirIcono(g.Exe);   // late mientras la app arranca
            Uso.Apuntar("fijada:" + g.Lanzar);
            Fuentes.Lanzar(g.Lanzar);
            return;
        }
        CerrarMiniLista();

        var activa = _activaAlPulsar != IntPtr.Zero ? _activaAlPulsar : ActivaAjena();
        int i = g.Ventanas.IndexOf(activa);

        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") == "1")
            Log.Info($"DIAG pulsar {System.IO.Path.GetFileName(g.Exe)}: alPulsar={_activaAlPulsar} " +
                     $"ajena={ActivaAjena()} activa={activa} i={i} ciclo={g.InicioCiclo} " +
                     $"ventanas=[{string.Join(",", g.Ventanas)}]");

        // Ninguna del grupo delante: se trae la última que usaste y empieza la vuelta
        if (i < 0)
        {
            var destino = g.Ventanas.Contains(g.Ultima) ? g.Ultima : g.Ventanas[0];
            g.Ultima = destino;
            g.InicioCiclo = destino;
            Log.Info($"grupo {System.IO.Path.GetFileName(g.Exe)}: activando \"{Nativo.Titulo(destino)}\"");
            Nativo.Activar(destino);
            return;
        }

        // Una del grupo está delante: la siguiente
        var siguiente = g.Ventanas[(i + 1) % g.Ventanas.Count];

        // ¿Hemos vuelto al punto de partida? Entonces toca esconder el grupo.
        // Con una sola ventana esto pasa al primer clic, que es justo lo que
        // hace la barra de tareas: pulsas la app que tienes delante y se va.
        if (g.InicioCiclo == IntPtr.Zero) g.InicioCiclo = activa;
        if (siguiente == g.InicioCiclo)
        {
            Log.Info($"grupo {System.IO.Path.GetFileName(g.Exe)}: vuelta completa, se minimizan sus {g.Ventanas.Count} ventanas");
            g.InicioCiclo = IntPtr.Zero;
            foreach (var v in g.Ventanas) MinimizarVentana(v);
            return;
        }

        g.Ultima = siguiente;
        Log.Info($"grupo {System.IO.Path.GetFileName(g.Exe)}: pasando a \"{Nativo.Titulo(siguiente)}\"");
        Nativo.Activar(siguiente);
    }

    // Clic sobre una ventana concreta (mini lista o vista general): si ya es la
    // que está delante, se minimiza; si no, se trae
    void PulsarVentana(IntPtr h, Grupo? g = null)
    {
        var activa = _activaAlPulsar != IntPtr.Zero ? _activaAlPulsar : ActivaAjena();
        if (h == activa && !Nativo.EstaMinimizada(h))
        {
            MinimizarVentana(h);
            return;
        }
        if (g != null) { g.Ultima = h; g.InicioCiclo = h; }
        Nativo.Activar(h);
    }

    // ================= Desplazamiento =================
    void Carrusel_Rueda(object remitente, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (DesplazamientoMaximo <= 0) return;
        MedirFotogramas("rueda del carrusel", 600);

        // Una muesca de rueda ≈ dos iconos; el resto lo pone la frenada de la animación
        _objetivoDesplazamiento += Math.Sign(e.Delta) * AnchoIcono * 2;
        LimitarDesplazamiento();
        DeslizarA(_objetivoDesplazamiento, 420);
    }

    void LimitarDesplazamiento() =>
        _objetivoDesplazamiento = Math.Clamp(_objetivoDesplazamiento, -DesplazamientoMaximo, 0);

    // La frenada larga con CubicEase es lo que da la sensación de inercia: cada muesca
    // nueva reanuda desde donde va el dibujo, así que empujar varias veces acelera
    void DeslizarA(double destino, double ms)
    {
        if (_desplazamiento == null) return;
        var eje = EjeTira;
        double ahora = Desplazado;
        _desplazamiento.BeginAnimation(eje, null);
        PonerDesplazado(destino);
        if (Math.Abs(ahora - destino) < 0.5) { PintarDesvanecido(); return; }

        var anim = Kf(ahora, destino, ms, 0, Suave);
        anim.Completed += (_, _) => PintarDesvanecido();
        _desplazamiento.BeginAnimation(eje, anim);
        PintarDesvanecido();
    }

    // Si la app que se acaba de activar está fuera de la vista, traerla
    void MostrarGrupoDe(IntPtr ventana)
    {
        if (_tira == null || _carrusel == null) return;
        var g = _grupos.Values.FirstOrDefault(x => x.Ventanas.Contains(ventana));
        if (g?.Boton == null || DesplazamientoMaximo <= 0) return;

        int i = _tira.Children.IndexOf(g.Boton);
        if (i < 0) return;

        // "Antes" y "después" en el eje de la tira: a la izquierda y a la derecha si
        // está tumbada, arriba y abajo si está apilada. La cuenta es la misma.
        double antes = i * AnchoIcono;
        double despues = antes + AnchoIcono;
        double visibleDesde = -_objetivoDesplazamiento;
        double visibleHasta = visibleDesde + LargoCarrusel;

        double destino = _objetivoDesplazamiento;
        if (antes < visibleDesde) destino = -antes;
        else if (despues > visibleHasta) destino = -(despues - LargoCarrusel);
        else return;

        _objetivoDesplazamiento = destino;
        LimitarDesplazamiento();
        DeslizarA(_objetivoDesplazamiento, 520);
    }

    // Los extremos se difuminan solo por el lado donde quedan apps sin ver
    void PintarDesvanecido()
    {
        double largo = LargoCarrusel;
        if (_carrusel == null || largo <= 0 || double.IsNaN(largo)) return;

        bool hayAntes = _desplazamiento != null && Desplazado < -0.5;
        bool hayDespues = _desplazamiento != null && Desplazado > -DesplazamientoMaximo + 0.5;

        if (Environment.GetEnvironmentVariable("NUBEBAR_SINMASCARA") == "1") { _carrusel.OpacityMask = null; return; }
        if (!hayAntes && !hayDespues) { _carrusel.OpacityMask = null; return; }

        // El degradado va en el eje de la tira: de izquierda a derecha si está tumbada,
        // de arriba abajo si está apilada.
        double borde = Math.Min(DesvanecidoBorde / largo, 0.45);
        var mascara = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = Vertical ? new Point(0, 1) : new Point(1, 0)
        };
        // Esto es una MÁSCARA de opacidad, no pintura: en una `OpacityMask` de WPF el negro
        // significa «opaco» y el transparente «no se ve». No son colores, son un uno y un
        // cero, y cambiarlos por un color del tema rompería el desvanecido del carrusel.
        mascara.GradientStops.Add(new GradientStop(hayAntes ? Colors.Transparent : Colors.Black, 0));   // color literal a propósito
        mascara.GradientStops.Add(new GradientStop(Colors.Black, borde));                               // color literal a propósito
        mascara.GradientStops.Add(new GradientStop(Colors.Black, 1 - borde));                           // color literal a propósito
        mascara.GradientStops.Add(new GradientStop(hayDespues ? Colors.Transparent : Colors.Black, 1)); // color literal a propósito
        mascara.Freeze();
        _carrusel.OpacityMask = mascara;
    }

    // ================= Mini lista de ventanas del grupo =================
    void SenalarGrupo(Grupo g)
    {
        _grupoSenalado = g;
        if (g.Ventanas.Count < 2) { CerrarMiniLista(); return; }

        // Pequeña espera: si solo estás pasando el ratón por encima, no aparece
        if (_tiempoMiniLista == null)
        {
            _tiempoMiniLista = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _tiempoMiniLista.Tick += AbrirMiniLista;
        }
        _tiempoMiniLista.Stop();
        _tiempoMiniLista.Start();
    }

    void OlvidarGrupo(Grupo g)
    {
        if (_grupoSenalado == g) _tiempoMiniLista?.Stop();
    }

    void AbrirMiniLista(object? remitente, EventArgs e)
    {
        _tiempoMiniLista?.Stop();
        if (_grupoSenalado is not { } g || g.Ventanas.Count < 2) return;
        if (g.Boton?.IsMouseOver != true) return;   // ya no está el ratón encima

        // Con el menú del clic derecho abierto, la mini lista se lo comería: el
        // ratón sigue encima del icono y el temporizador salta igual.
        if (_panelAbierto == PanelMenu) return;

        var activa = ActivaAjena();
        int filas = Math.Min(MaxFilasMiniLista, g.Ventanas.Count);
        double ancho = AnchoMiniLista(g), alto = filas * 30 + 12;

        // Con "ancla", el cajón no se dibuja de punta a punta: solo la tarjeta de la
        // lista, pegada a su icono. Sin esto, dos títulos de ventana abrían una losa
        // negra de lado a lado de la pantalla, que es justo lo que no tiene que
        // parecer un menú de dos líneas.
        AbrirPanel("grupo:" + g.Exe, CrearMiniLista(g, activa, ancho), ancho, alto, ancla: g.Boton);
    }

    const int MaxFilasMiniLista = 8;
    const double MargenMiniLista = 10;     // hueco del texto a cada lado de la tarjeta

    // Lo ancha que tiene que ser la tarjeta: lo que pida el título más largo, ni más
    // ni menos. Un ancho fijo dejaba una tarjeta medio vacía con títulos cortos y
    // recortaba los largos a la misma altura de siempre.
    double AnchoMiniLista(Grupo g)
    {
        double texto = 0;
        foreach (var h in g.Ventanas.Take(MaxFilasMiniLista))
            texto = Math.Max(texto, AnchoDeTexto(TituloDeFila(Nativo.Titulo(h))));
        return Math.Clamp(Math.Ceiling(texto) + MargenMiniLista * 2 + 24, 180, 460);
    }

    static string TituloDeFila(string titulo) => titulo.Length > 48 ? titulo[..47] + "…" : titulo;

    FrameworkElement CrearMiniLista(Grupo g, IntPtr activa, double ancho)
    {
        // El margen de la izquierda no se pone aquí: lo lleva el recorte del cajón,
        // que coloca la tarjeta bajo su icono en cada fotograma (ver XdeTarjeta)
        var lista = new StackPanel
        {
            Margin = new Thickness(0, 4, 0, 6),
            Width = ancho,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var h in g.Ventanas.Take(MaxFilasMiniLista))
        {
            var titulo = Nativo.Titulo(h);
            var fila = new TextBlock
            {
                Text = TituloDeFila(titulo),
                Foreground = h == activa ? _acentoLegible : _texto,
                Opacity = h == activa ? 1 : 0.8,
                Margin = new Thickness(MargenMiniLista, 0, MargenMiniLista, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = Math.Max(40, ancho - MargenMiniLista * 2 - 24)
            };
            var boton = Boton(fila, titulo, () => { CerrarMiniLista(); PulsarVentana(h, g); });
            boton.ToolTip = null;   // el título ya está escrito en la propia fila
            boton.Height = 28;
            boton.PreviewMouseLeftButtonDown += (_, _) => ApuntarActivaAlPulsar();
            boton.HorizontalContentAlignment = HorizontalAlignment.Left;

            // La ✕ de cada fila: cerrar una carpeta de diez sin tener que traerla delante
            // primero. Va en los 24 px que `AnchoMiniLista` ya reservaba a la derecha.
            var renglon = new Grid();
            renglon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            renglon.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            renglon.Children.Add(boton);
            var aspa = Glifo(GlifoAspa);
            aspa.FontSize = 10;
            aspa.Foreground = _textoAtenuado;
            var cerrar = Boton(aspa, "Cerrar esta ventana", () => CerrarDesdeMiniLista(h, g, lista, renglon));
            cerrar.Width = 24;
            cerrar.Height = 28;
            Grid.SetColumn(cerrar, 1);
            renglon.Children.Add(cerrar);
            lista.Children.Add(renglon);
        }
        return lista;
    }

    // Por las buenas (WM_CLOSE, como la vista general): si la app pregunta si guardas, te
    // lo pregunta. La fila se quita ya y la lista se encoge; con una sola ventana la mini
    // lista ya no tiene sentido y se cierra, como cuando no llega a abrirse.
    void CerrarDesdeMiniLista(IntPtr h, Grupo g, StackPanel lista, UIElement renglon)
    {
        CerrarLaVentana(h, "mini lista");
        lista.Children.Remove(renglon);
        if (lista.Children.Count < 2) { CerrarMiniLista(); return; }
        ReajustarPanel("grupo:" + g.Exe);
    }

    void CerrarMiniLista()
    {
        _tiempoMiniLista?.Stop();
        if (_panelAbierto?.StartsWith("grupo:") == true) CerrarPanel();
    }
}
