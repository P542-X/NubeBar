using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NubeBar;

// Todo lo que tiene que ver con repartir las islas entre las pantallas.
public partial class MainWindow : Window
{
    // ================= Colocación en píxeles de verdad =================
    // La ventana se coloca con SetWindowPos y no con Left/Top/Width/Height: las
    // propiedades de WPF van en sus unidades, y cuando dos monitores tienen DPI
    // distinto esas unidades no significan lo mismo en cada uno. En píxeles, sí.
    void ColocarEnMonitor(double altoVentanaDip)
    {
        var m = _monitor;
        int altoPx = Math.Max(1, (int)Math.Round(altoVentanaDip * m.Escala));
        int y = Arriba ? m.Y : m.Abajo - altoPx;

        if (_self != IntPtr.Zero) Nativo.Colocar(_self, m.X, y, m.Ancho, altoPx);
        else { Left = m.X; Top = y; Width = m.Ancho; Height = altoVentanaDip; }
    }

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
        var todos = Monitores.Todos();
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
        foreach (var m in ms.Skip(1)) new MainWindow(m, false).Show();
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
    }

    IntPtr MensajePantalla(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool manejado)
    {
        if (msg != WM_DISPLAYCHANGE && msg != WM_DPICHANGED && msg != WM_SETTINGCHANGE) return IntPtr.Zero;
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
    }
}
