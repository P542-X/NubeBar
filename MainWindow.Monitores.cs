using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace NubeBar;

// Todo lo que tiene que ver con repartir las islas entre las pantallas.
public partial class MainWindow : Window
{
    // ================= Colocación en píxeles de verdad =================
    // La ventana se coloca con SetWindowPos y no con Left/Top/Width/Height: las
    // propiedades de WPF van en sus unidades, y cuando dos monitores tienen DPI
    // distinto esas unidades no significan lo mismo en cada uno. En píxeles, sí.
    // La escala que usa WPF PARA ESTA VENTANA, que no siempre es la del monitor.
    // La ventana nace en el monitor principal y después la movemos al suyo; hasta
    // que Windows manda el WM_DPICHANGED y WPF se entera, su escala es la vieja.
    // Si dimensionáramos con la del monitor, el alto en píxeles y el que WPF cree
    // tener no cuadrarían, y la isla saldría más alta que su ventana: recortada,
    // viéndose solo la mitad de arriba.
    double EscalaDeWpf()
    {
        try
        {
            double s = VisualTreeHelper.GetDpi(this).DpiScaleY;
            return s > 0 ? s : _monitor.Escala;
        }
        catch { return _monitor.Escala; }
    }

    // El número que recibe es el GROSOR de la franja con sus márgenes: en horizontal es
    // el alto de la ventana y en vertical su ancho. Es el mismo dato —el grosor del
    // panel— y lo único que cambia es contra qué eje se mide, así que los que ya
    // llamaban a esto (abrir un panel, un cambio de DPI, recargar el config) siguen
    // llamando igual y la barra vertical sale colocada sin tocarlos.
    void ColocarEnMonitor(double altoVentanaDip)
    {
        if (Vertical) { ColocarEnMonitorVertical(altoVentanaDip); return; }
        altoVentanaDip = AltoDeVentana(altoVentanaDip);

        var m = _monitor;
        double escala = _self != IntPtr.Zero ? EscalaDeWpf() : m.Escala;
        int altoPx = Math.Max(1, (int)Math.Round(altoVentanaDip * escala));
        int y = Arriba ? m.Y : m.Abajo - altoPx;

        if (_self == IntPtr.Zero)
        {
            Left = m.X; Top = y; Width = m.Ancho; Height = altoVentanaDip;
            return;
        }

        // Mientras la estás arrastrando manda el ratón, y mientras siga fuera de
        // sitio manda donde la dejaste: aquí solo se ajusta el TAMAÑO, nunca la
        // posición.
        //
        // Lo segundo no estaba y es lo que la teletransportaba: cualquier cosa que
        // recoloque la ventana (abrir o cerrar un panel, recargar el config) la
        // devolvía de un salto a su sitio, sin la animación de vuelta. Y pasaba
        // constantemente, porque al soltarla cogida de un icono se disparaba el
        // clic de ese botón y lo que abría ya recolocaba la ventana.
        //
        // Ahora la isla solo vuelve a casa por un sitio: AnimarVuelta.
        if (_arrastrando || _fueraDeSitio || _animandoVuelta != null)
        {
            var r = Nativo.Rectangulo(_self);
            // Con la isla abajo, el filo que no se puede mover es el de abajo: si
            // se respetara el de arriba, al abrir un panel la isla bajaría sola
            int yy = Arriba ? r.Arr : r.Aba - altoPx;
            Nativo.Colocar(_self, r.Izq, yy, m.Ancho, altoPx);
            Width = double.NaN;
            Height = double.NaN;
            return;
        }

        Nativo.Colocar(_self, m.X, y, m.Ancho, altoPx);

        // Y ahora lo importante: soltar Width/Height de WPF.
        //
        // El tamaño lo pone SetWindowPos en píxeles, pero Window.Width seguía
        // valiendo lo que dijera el XAML. Mientras nadie la mire da igual; en
        // cuanto Windows manda un WM_DPICHANGED (al mover la ventana a un monitor
        // con otro escalado, o al arrancar en uno al 125%), WPF REIMPONE esa
        // anchura y la ventana se encoge. La isla va centrada dentro de la
        // ventana, así que una ventana encogida la deja pegada al borde izquierdo
        // en vez de en medio de la pantalla.
        //
        // Con NaN, WPF entiende "no la gestiono yo" y respeta el tamaño del hwnd.
        Width = double.NaN;
        Height = double.NaN;
    }

    // ---------- Con la isla abajo, la ventana no cambia de alto ----------
    //
    // Arriba, abrir un panel solo baja el filo de abajo de la ventana: lo ya pintado se
    // queda donde estaba. Abajo hay que SUBIR el filo de arriba, y la ventana es en capas
    // (AllowsTransparency): Windows enseña un fotograma el dibujo viejo pegado al filo
    // nuevo, la isla salta para arriba y vuelve —al cerrar, al revés, y desaparece un
    // instante—. Era el parpadeo al abrir paneles con la isla abajo.
    //
    // Así que abajo la ventana mide siempre lo que mediría con el panel más alto que cabe,
    // y abrir o cerrar ya no la mueve: solo cambia lo que hay dentro. Lo de encima de la
    // isla es transparente del todo, y en una ventana en capas eso deja pasar el ratón.
    double AltoDeVentana(double pedido) =>
        Arriba || Vertical ? pedido
                           : Math.Max(pedido, _cfg.Barra.Alto + AltoMaximoPanel() + _cfg.Barra.Margen * 2);

    // ================= Cuántas apps caben de verdad =================
    // Tu MaxApps manda, pero en un monitor estrecho (el vertical, por ejemplo) una
    // tira de 10 iconos dejaría la isla más ancha que la pantalla. Aquí se recorta
    // a lo que cabe: la isla mide 2 × el lado más ancho + el centro, así que el
    // carrusel no puede pasar de la mitad del monitor menos lo que ocupa el resto.
    const double EspacioOtrosModulos = 150;   // reloj, fecha, cpu, ram... medido en la práctica

    int MaxAppsEfectivo
    {
        get
        {
            int pedidas = Math.Max(1, _cfg.Barra.MaxApps);

            // En vertical la tira se apila, así que lo que la limita es el ALTO de la
            // pantalla, y no hay que repartir el hueco entre dos lados: la zona del
            // medio tiene la columna entera menos lo que ocupan las otras dos.
            if (Vertical)
            {
                double altoDip = _monitor.Alto / _monitor.Escala - _cfg.Barra.Margen * 2;
                int apilados = (int)Math.Floor((altoDip - EspacioOtrosModulos * 2 - AnchoIcono) / AnchoIcono);
                return Math.Max(1, Math.Min(pedidas, apilados));
            }

            double anchoDip = _monitor.Ancho / _monitor.Escala - _cfg.Barra.Margen * 2;
            // (ancho - centro) / 2 es lo que le toca a cada lado; menos el botón de Inicio
            int caben = (int)Math.Floor(((anchoDip - EspacioOtrosModulos) / 2 - AnchoIcono) / AnchoIcono);
            return Math.Max(1, Math.Min(pedidas, caben));
        }
    }

    // ================= Qué apps enseña esta isla =================
    List<IntPtr> SoloDeMiMonitor(List<IntPtr> ventanas)
    {
        if (!_cfg.Barra.AppsPorMonitor || Islas.Count < 2) return ventanas;
        return ventanas.Where(h => Monitores.HandleDe(h) == _monitor.Handle).ToList();
    }

    // Las pantallas donde de verdad hay isla. Solo en esas se esconde la barra de
    // Windows: si no, con Monitores="principal" el secundario se quedaría pelado.
    public static HashSet<IntPtr> MonitoresConIsla() =>
        Islas.Select(i => i._monitor.Handle).ToHashSet();

    // ================= Qué wallpaper mira cada isla =================
    // Wallpaper Engine guarda un fondo por pantalla ("Monitor0", "Monitor1"...) en el
    // mismo orden que Windows numera los DISPLAY. Con una sola isla se respeta tu clave
    // Wallpaper.Monitor de siempre; con varias, cada una lee el de su propia pantalla.
    int IndiceWallpaper
    {
        get
        {
            if (Islas.Count < 2) return _cfg.Wallpaper.Monitor;
            var d = _monitor.Dispositivo;                      // \\.\DISPLAY2
            int i = d.LastIndexOf("DISPLAY", StringComparison.OrdinalIgnoreCase);
            return i >= 0 && int.TryParse(d[(i + 7)..], out int n) && n > 0 ? n - 1 : _cfg.Wallpaper.Monitor;
        }
    }

    // ================= La isla que tiene el ratón encima =================
    // El menú Inicio, la vista general y los ajustes salen donde estás mirando.
    public static MainWindow IslaDelRaton()
    {
        var jefa = Islas.FirstOrDefault(i => i._esJefa) ?? Islas.FirstOrDefault();
        if (Islas.Count < 2 || jefa == null) return jefa!;

        var p = Monitores.Raton();
        return Islas.FirstOrDefault(i => i._monitor.Contiene(p.X, p.Y)) ?? jefa;
    }

    // ================= Crear y rehacer el juego de islas =================
    static List<Monitores.Info> Deseados(Config cfg)
    {
        var todos = Monitores.Todos().DistinctBy(m => m.Handle).ToList();
        return cfg.Barra.Monitores.Equals("principal", StringComparison.OrdinalIgnoreCase)
            ? todos.Take(1).ToList()     // Todos() ya pone el principal el primero
            : todos;
    }

    // Se llama una vez al arrancar: la primera isla es la jefa y es la MainWindow
    public static MainWindow Arrancar()
    {
        var cfg = Config.Cargar() ?? new Config();
        var ms = Deseados(cfg);

        var jefa = new MainWindow(ms[0], true);
        jefa.Show();
        // Una isla por monitor y ni una más: si EnumDisplayMonitors devolviera dos
        // veces la misma pantalla (pasa con algunos clonados), saldrían dos islas
        // superpuestas en ella.
        foreach (var m in ms.Skip(1).DistinctBy(m => m.Handle)) new MainWindow(m, false).Show();
        Log.Info($"islas creadas: {ms.Count} ({string.Join(", ", ms.Select(m => m.Dispositivo))})");
        return jefa;
    }

    // Enchufar o quitar un monitor, girarlo o cambiarle la resolución llega aquí.
    // La jefa nunca se cierra (es la dueña del hook de teclado y de la MainWindow):
    // se le reasigna el monitor principal y son las demás las que van y vienen.
    static void Reorganizar()
    {
        var jefa = Islas.FirstOrDefault(i => i._esJefa);
        if (jefa == null) return;

        var ms = Deseados(jefa._cfg);
        var antes = string.Join(", ", Islas.Select(i => i._monitor.Clave));

        // Windows manda WM_SETTINGCHANGE por mil motivos que no son las pantallas
        // (cambiar el área de trabajo, por ejemplo, que es lo que hacemos nosotros al
        // esconder la barra). Si no ha cambiado nada de verdad, no se toca nada.
        if (antes == string.Join(", ", ms.Select(m => m.Clave)) && Islas.Count == ms.Count) return;

        jefa._monitor = ms[0];

        var libres = Islas.Where(i => !i._esJefa).ToList();
        var usadas = new List<MainWindow>();
        foreach (var m in ms.Skip(1))
        {
            var isla = libres.FirstOrDefault(i => i._monitor.Dispositivo == m.Dispositivo && !usadas.Contains(i))
                    ?? libres.FirstOrDefault(i => !usadas.Contains(i));
            if (isla != null) { isla._monitor = m; usadas.Add(isla); }
            else new MainWindow(m, false).Show();
        }
        foreach (var sobra in libres.Except(usadas).ToList()) sobra.Close();

        foreach (var i in Islas.ToList())
        {
            try { i.Aplicar(); i.RefrescarApps(); }
            catch (Exception ex) { Log.Error("recolocar isla", ex); }
        }
        Log.Info($"pantallas cambiadas: [{antes}] -> [{string.Join(", ", Islas.Select(i => i._monitor.Dispositivo))}]");
    }

    // ================= Avisos de Windows =================
    const int WM_DISPLAYCHANGE = 0x007E;
    const int WM_DPICHANGED = 0x02E0;
    const int WM_SETTINGCHANGE = 0x001A;

    static DispatcherTimer? _reposo;

    void VigilarCambiosDePantalla()
    {
        HwndSource.FromHwnd(_self)?.AddHook(MensajePantalla);

        // Cuando WPF ya se ha enterado del cambio de escala, se rehace la
        // geometría con la escala nueva. Sin esto, al arrastrar la ventana a un
        // monitor con otro escalado la isla queda recortada o descentrada.
        DpiChanged += (_, e) =>
        {
            // Recolocar la ventana puede provocar otro DpiChanged, y ese otro
            // recoloca y vuelve a provocarlo: el log se llenaba de "cambio de escala
            // 1 -> 1" decenas de veces por segundo. Si la escala no ha cambiado de
            // verdad no hay nada que hacer.
            if (Math.Abs(e.OldDpi.DpiScaleY - e.NewDpi.DpiScaleY) < 0.001 &&
                Math.Abs(e.OldDpi.DpiScaleX - e.NewDpi.DpiScaleX) < 0.001) return;

            if (Monitores.DeVentana(_self) is { } m) _monitor = m;
            ColocarEnMonitor(AltoVentanaAhora());
            AjustarAncho();
            Log.Info($"cambio de escala en {_monitor.Dispositivo}: " +
                     $"{e.OldDpi.DpiScaleY:0.##} -> {e.NewDpi.DpiScaleY:0.##}, isla recolocada");
        };

    }

    // El grosor que debe tener la ventana ahora mismo, con el panel abierto si lo hay.
    // Lo decide GrosorVentanaAhora, que sabe si el panel crece hacia dentro (el cajón
    // de la horizontal) o hacia el lado (la tarjeta de la vertical).
    double AltoVentanaAhora() => GrosorVentanaAhora();

    IntPtr MensajePantalla(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool manejado)
    {
        if (msg != WM_DISPLAYCHANGE && msg != WM_DPICHANGED && msg != WM_SETTINGCHANGE) return IntPtr.Zero;

        // El WM_DPICHANGED crudo no se atiende aquí: WPF todavía no ha actualizado
        // su propia escala cuando llega, así que recolocar ahora usaría la vieja.
        // Se hace en el evento DpiChanged de WPF (ver VigilarCambiosDePantalla),
        // que salta DESPUÉS de que se entere.
        if (msg == WM_DPICHANGED) return IntPtr.Zero;

        if (!_esJefa) return IntPtr.Zero;   // el aviso llega a todas las ventanas; basta con una

        // Windows manda varios avisos seguidos mientras recoloca las pantallas:
        // se espera medio segundo de calma y se hace el trabajo una sola vez.
        _reposo ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _reposo.Stop();
        _reposo.Tick -= TrasLaCalma;
        _reposo.Tick += TrasLaCalma;
        _reposo.Start();
        return IntPtr.Zero;
    }

    static void TrasLaCalma(object? s, EventArgs e)
    {
        _reposo?.Stop();
        try { Reorganizar(); }
        catch (Exception ex) { Log.Error("reorganizar pantallas", ex); }

        // Y lo que sabíamos de los monitores por DDC/CI deja de valer: han podido entrar o salir
        // pantallas, y las capacidades guardadas eran de otras. Esto solo OLVIDA —no pregunta
        // nada, que sería hablar por el cable desde el hilo de la interfaz—; el panel vuelve a
        // preguntar la próxima vez que se abra.
        try { Entradas.Replantear(); }
        catch (Exception ex) { Log.Error("replantear los monitores DDC/CI", ex); }
    }
}
