using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NubeBar;

// Los monitores del sistema, en píxeles de verdad. Todo lo que posicione ventanas
// trabaja aquí en píxeles físicos y deja que WPF haga la conversión a su escala:
// con monitores de distinto DPI, mezclar unidades es lo que lo rompe todo.
public static class Monitores
{
    public sealed record Info(
        IntPtr Handle,
        string Dispositivo,
        int X, int Y, int Ancho, int Alto,     // pantalla completa, en píxeles
        int TrabajoX, int TrabajoY, int TrabajoAncho, int TrabajoAlto,
        bool EsPrincipal,
        uint Dpi)
    {
        public double Escala => Dpi / 96.0;
        public int Derecha => X + Ancho;
        public int Abajo => Y + Alto;
        public bool Contiene(int px, int py) => px >= X && px < Derecha && py >= Y && py < Abajo;

        // Identidad estable entre recolocaciones: el handle cambia al reconfigurar
        public string Clave => $"{Dispositivo}|{X},{Y},{Ancho}x{Alto}";
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    delegate bool EnumProc(IntPtr monitor, IntPtr dc, ref RECT r, IntPtr datos);

    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr recorte, EnumProc cb, IntPtr datos);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFOEX mi);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr h, int tipo, out uint x, out uint y);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    const uint MONITOR_DEFAULTTONEAREST = 2;
    const int MDT_EFFECTIVE_DPI = 0;

    static List<Info> _acumulador = new();

    static bool Uno(IntPtr h, IntPtr dc, ref RECT r, IntPtr datos)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(h, ref mi)) return true;

        uint dpi = 96;
        try { if (GetDpiForMonitor(h, MDT_EFFECTIVE_DPI, out uint dx, out _) == 0) dpi = dx; }
        catch { }

        _acumulador.Add(new Info(
            h, mi.szDevice,
            mi.rcMonitor.Left, mi.rcMonitor.Top,
            mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top,
            mi.rcWork.Left, mi.rcWork.Top,
            mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top,
            (mi.dwFlags & 1) != 0, dpi));
        return true;
    }

    public static List<Info> Todos()
    {
        _acumulador = new List<Info>();
        try { EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Uno, IntPtr.Zero); }
        catch (Exception ex) { Log.Error("enumerar monitores", ex); }

        if (_acumulador.Count == 0)
            _acumulador.Add(new Info(IntPtr.Zero, "?", 0, 0, 1920, 1080, 0, 0, 1920, 1040, true, 96));

        // El principal primero, y el resto de izquierda a derecha: así el orden
        // no baila entre arranques
        return _acumulador
            .OrderByDescending(m => m.EsPrincipal)
            .ThenBy(m => m.X)
            .ToList();
    }

    public static Info Principal() => Todos().FirstOrDefault(m => m.EsPrincipal) ?? Todos()[0];

    public static Info? DeVentana(IntPtr ventana)
    {
        var h = HandleDe(ventana);
        return h == IntPtr.Zero ? null : Todos().FirstOrDefault(m => m.Handle == h);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public POINT ptMinPosition, ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT wp);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

    // El monitor de una ventana, sin recorrer la lista entera: se usa una vez por
    // ventana y por segundo, así que tiene que ser barato.
    // Una ventana minimizada vive en (-32000,-32000), que caería siempre en el monitor
    // de más a la izquierda; para esas se mira dónde volverá al restaurarse.
    public static IntPtr HandleDe(IntPtr ventana)
    {
        if (ventana == IntPtr.Zero) return IntPtr.Zero;
        if (IsIconic(ventana))
        {
            var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            if (GetWindowPlacement(ventana, ref wp))
            {
                var centro = new POINT
                {
                    X = (wp.rcNormalPosition.Left + wp.rcNormalPosition.Right) / 2,
                    Y = (wp.rcNormalPosition.Top + wp.rcNormalPosition.Bottom) / 2
                };
                return MonitorFromPoint(centro, MONITOR_DEFAULTTONEAREST);
            }
        }
        return MonitorFromWindow(ventana, MONITOR_DEFAULTTONEAREST);
    }

    public static Info? DelRaton()
    {
        if (!GetCursorPos(out var p)) return null;
        var h = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
        return Todos().FirstOrDefault(m => m.Handle == h);
    }

    public static POINT Raton()
    {
        GetCursorPos(out var p);
        return p;
    }

    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);

    // La ventana que hay bajo el puntero (la de más arriba)
    public static IntPtr VentanaBajoElRaton() =>
        GetCursorPos(out var p) ? WindowFromPoint(p) : IntPtr.Zero;
}
