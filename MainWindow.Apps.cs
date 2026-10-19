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
        public Button? Boton;
        public StackPanel? Puntos;
        public ImageSource? Icono;
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

    double _anchoTira;                     // ancho natural de la fila entera, medido

    double AnchoVisible => Math.Max(AnchoIcono, _cfg.Barra.MaxApps * AnchoIcono);
    double DesplazamientoMaximo => Math.Max(0, _anchoTira - AnchoVisible);

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
        return $"tira={_anchoTira:0} visible={_carrusel?.Width:0} x0={x0:0} " +
               $"desp={_desplazamiento?.X:0} [{string.Join(" ", nombres)}]{detalle}";
    }

    // ================= Construcción =================
    UIElement ModuloApps()
    {
        double altoBoton = Math.Max(20, _cfg.Barra.Alto - 8);

        _tira = new StackPanel { Orientation = Orientation.Horizontal, Height = altoBoton };
        _desplazamiento = new TranslateTransform();
        _tira.RenderTransform = _desplazamiento;

        // La tira va dentro de un Canvas y no directamente en el Border. Si no, WPF le
        // aplica el recorte de layout: como la tira mide más que el hueco que se le da,
        // la recorta EN SUS PROPIAS COORDENADAS, y al desplazarla solo quedaba visible
        // el trocito final. El Canvas da sitio infinito a sus hijos y no los recorta;
        // el recorte bueno, el de la ventanita que se ve, lo hace el Border.
        var lienzo = new Canvas { Height = altoBoton };
        lienzo.Children.Add(_tira);

        _carrusel = new Border
        {
            ClipToBounds = true,
            Child = lienzo,
            Height = altoBoton,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent     // para que la rueda llegue aunque sobre hueco
        };
        _carrusel.MouseWheel += Carrusel_Rueda;
        _carrusel.SizeChanged += (_, _) => PintarDesvanecido();

        _grupos.Clear();
        _activaPrevia = IntPtr.Zero;
        _primeraCarga = true;

        RefrescarApps();
        _actualizadores.Add(RefrescarApps);
        return _carrusel;
    }

    // ================= Refresco =================
    void RefrescarApps()
    {
        if (_tira == null || _carrusel == null) return;

        var activa = Nativo.Activa();
        if (activa == _self) activa = _ultimaActiva; else if (activa != IntPtr.Zero) _ultimaActiva = activa;

        var abiertas = Nativo.VentanasAbiertas(_self);
        var vivas = new HashSet<IntPtr>(abiertas);
        Nativo.OlvidarExes(vivas);

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

        // Grupos que ya no existen: se encogen y se van
        foreach (var clave in _grupos.Keys.Where(k => !ahora.ContainsKey(k)).ToList())
        {
            var g = _grupos[clave];
            _grupos.Remove(clave);
            if (g.Boton is { } b)
                AnimarSalida(b, () => { _tira.Children.Remove(b); TrasCambiarApps(); });
        }

        // Grupos nuevos y actualización de los que siguen.
        // En orden de ejecutable: los botones se añaden al final según van apareciendo,
        // así que sin ordenar aquí los iconos salían en distinto sitio en cada arranque.
        int orden = 0;
        foreach (var (clave, ventanas) in ahora.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
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
                ActualizarGrupo(g, activa);
                continue;
            }
            g = new Grupo { Exe = clave, Ventanas = ventanas, Ultima = ventanas[0] };
            _grupos[clave] = g;
            CrearBotonGrupo(g);
            _tira.Children.Add(g.Boton);
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

    // Tras añadir o quitar iconos hay que recalcular el ancho visible y el degradado
    void TrasCambiarApps()
    {
        if (_carrusel == null || _tira == null) return;
        // Medir sin límite (no UpdateLayout, que fuerza una pasada entera cada segundo)
        _tira.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _anchoTira = _tira.DesiredSize.Width;
        _tira.InvalidateMeasure();
        _carrusel.Width = Math.Min(_anchoTira, AnchoVisible);
        LimitarDesplazamiento();
        PintarDesvanecido();
        AjustarAncho();
    }

    void CrearBotonGrupo(Grupo g)
    {
        var titulo = Nativo.Titulo(g.Ventanas[0]);
        g.Icono = Nativo.Icono(g.Ventanas[0]);
        UIElement visual = g.Icono != null
            ? new Image { Source = g.Icono, Width = 18, Height = 18 }
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
        g.Boton.MouseEnter += (_, _) => SenalarGrupo(g);
        g.Boton.MouseLeave += (_, _) => OlvidarGrupo(g);
    }

    // Estado visual del grupo: cuántos puntos y de qué color
    void ActualizarGrupo(Grupo g, IntPtr activa, bool inmediato = false)
    {
        if (g.Boton == null || g.Puntos == null) return;

        bool tieneLaActiva = g.Ventanas.Contains(activa);
        int cuantos = Math.Min(3, g.Ventanas.Count);

        // Con una sola ventana, tooltip normal. Con varias no, porque el que manda
        // es la mini lista y el tooltip se le pondría encima tapando la primera fila.
        g.Boton.ToolTip = g.Ventanas.Count == 1 ? Nativo.Titulo(g.Ventanas[0]) : null;

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
    void PulsarGrupo(Grupo g)
    {
        if (g.Ventanas.Count == 0) return;

        var activa = Nativo.Activa();
        int i = g.Ventanas.IndexOf(activa);

        // Si ya estás en una ventana del grupo, pasa a la siguiente; si no, a la última usada
        var destino = i >= 0
            ? g.Ventanas[(i + 1) % g.Ventanas.Count]
            : (g.Ventanas.Contains(g.Ultima) ? g.Ultima : g.Ventanas[0]);

        g.Ultima = destino;
        Nativo.Activar(destino);
        CerrarMiniLista();
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
        double ahora = _desplazamiento.X;
        _desplazamiento.BeginAnimation(TranslateTransform.XProperty, null);
        _desplazamiento.X = destino;
        if (Math.Abs(ahora - destino) < 0.5) { PintarDesvanecido(); return; }

        var anim = Kf(ahora, destino, ms, 0, Suave);
        anim.Completed += (_, _) => PintarDesvanecido();
        _desplazamiento.BeginAnimation(TranslateTransform.XProperty, anim);
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

        double izquierda = i * AnchoIcono;
        double derecha = izquierda + AnchoIcono;
        double visibleIzq = -_objetivoDesplazamiento;
        double visibleDer = visibleIzq + _carrusel.Width;

        double destino = _objetivoDesplazamiento;
        if (izquierda < visibleIzq) destino = -izquierda;
        else if (derecha > visibleDer) destino = -(derecha - _carrusel.Width);
        else return;

        _objetivoDesplazamiento = destino;
        LimitarDesplazamiento();
        DeslizarA(_objetivoDesplazamiento, 520);
    }

    // Los extremos se difuminan solo por el lado donde quedan apps sin ver
    void PintarDesvanecido()
    {
        if (_carrusel == null || _carrusel.Width <= 0 || double.IsNaN(_carrusel.Width)) return;

        bool hayIzquierda = _desplazamiento != null && _desplazamiento.X < -0.5;
        bool hayDerecha = _desplazamiento != null && _desplazamiento.X > -DesplazamientoMaximo + 0.5;

        if (Environment.GetEnvironmentVariable("NUBEBAR_SINMASCARA") == "1") { _carrusel.OpacityMask = null; return; }
        if (!hayIzquierda && !hayDerecha) { _carrusel.OpacityMask = null; return; }

        double borde = Math.Min(DesvanecidoBorde / _carrusel.Width, 0.45);
        var mascara = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        mascara.GradientStops.Add(new GradientStop(hayIzquierda ? Colors.Transparent : Colors.Black, 0));
        mascara.GradientStops.Add(new GradientStop(Colors.Black, borde));
        mascara.GradientStops.Add(new GradientStop(Colors.Black, 1 - borde));
        mascara.GradientStops.Add(new GradientStop(hayDerecha ? Colors.Transparent : Colors.Black, 1));
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

        var activa = Nativo.Activa();
        int filas = Math.Min(MaxFilasMiniLista, g.Ventanas.Count);
        double ancho = 320, alto = filas * 30 + 12;

        AbrirPanel("grupo:" + g.Exe, CrearMiniLista(g, activa), ancho, alto);
    }

    const int MaxFilasMiniLista = 8;

    FrameworkElement CrearMiniLista(Grupo g, IntPtr activa)
    {
        // La lista cuelga justo debajo de su icono, no del borde de la isla
        double x = 0;
        try
        {
            if (g.Boton != null)
                x = g.Boton.TransformToAncestor(Isla).Transform(new Point(0, 0)).X - Isla.Padding.Left;
        }
        catch { }
        x = Math.Max(0, Math.Min(x, Math.Max(0, Isla.ActualWidth - 320 - Isla.Padding.Left * 2)));

        var lista = new StackPanel
        {
            Margin = new Thickness(x, 4, 0, 6),
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var h in g.Ventanas.Take(MaxFilasMiniLista))
        {
            var titulo = Nativo.Titulo(h);
            var fila = new TextBlock
            {
                Text = titulo.Length > 48 ? titulo[..47] + "…" : titulo,
                Foreground = h == activa ? _acento : _texto,
                Opacity = h == activa ? 1 : 0.8,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = 286
            };
            var boton = Boton(fila, titulo, () => { g.Ultima = h; Nativo.Activar(h); CerrarMiniLista(); });
            boton.Height = 28;
            boton.HorizontalContentAlignment = HorizontalAlignment.Left;
            lista.Children.Add(boton);
        }
        return lista;
    }

    void CerrarMiniLista()
    {
        _tiempoMiniLista?.Stop();
        if (_panelAbierto?.StartsWith("grupo:") == true) CerrarPanel();
    }
}
