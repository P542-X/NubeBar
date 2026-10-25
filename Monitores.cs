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

    // ---------- A cuántos hercios va esa pantalla ----------
    //
    // No se usa para marcar el ritmo —de eso se encarga el compositor, y la escena de
    // entrada avanza por tiempo transcurrido, no por fotogramas contados— sino para
    // saber contra qué comparar: sin el refresco real no hay forma de decir si 97
    // fotogramas por segundo están bien o están mal.
    //
    // Primero el modo de pantalla, que es el dato exacto de ese monitor. Si no se puede
    // (sesión remota, driver raro), la cadencia de presentación que publica DWM, que es
    // la que de verdad manda. Y si tampoco, 60, que es lo más probable.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string? dispositivo, int modo, ref DEVMODE dm);

    [DllImport("dwmapi.dll")] static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref DWM_TIMING_INFO info);
    [DllImport("kernel32.dll")] static extern bool QueryPerformanceFrequency(out long f);

    // Solo hacen falta los tres primeros campos, pero la estructura va entera: DWM
    // comprueba cbSize y rechaza la llamada si no cuadra.
    [StructLayout(LayoutKind.Sequential)]
    struct DWM_TIMING_INFO
    {
        public int cbSize;
        public double rateRefreshNum, rateRefreshDen;
        public long qpcRefreshPeriod;
        public double rateCompose1, rateCompose2;
        public long qpcVBlank, cRefresh; public uint cDXRefresh;
        public long qpcCompose, cFrame; public uint cDXPresent;
        public long cRefreshFrame, cFrameSubmitted; public uint cDXPresentSubmitted;
        public long cFrameConfirmed; public uint cDXPresentConfirmed;
        public long cRefreshConfirmed; public uint cDXRefreshConfirmed;
        public long cFramesLate; public uint cFramesOutstanding;
        public long cFrameDisplayed, qpcFrameDisplayed, cRefreshFrameDisplayed;
        public long cFrameComplete, qpcFrameComplete, cFramePending, qpcFramePending;
        public long cFramesDisplayed, cFramesComplete, cFramesPending, cFramesAvailable;
        public long cFramesDropped, cFramesMissed, cRefreshNextDisplayed, cRefreshNextPresented;
        public long cRefreshesDisplayed, cRefreshesPresented, cRefreshStarted;
        public long cPixelsReceived, cPixelsDrawn, cBuffersEmpty;
    }

    public static double Hercios(string dispositivo)
    {
        try
        {
            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (EnumDisplaySettings(dispositivo, -1, ref dm) && dm.dmDisplayFrequency > 1)
                return dm.dmDisplayFrequency;
        }
        catch { }
        try
        {
            var info = new DWM_TIMING_INFO { cbSize = Marshal.SizeOf<DWM_TIMING_INFO>() };
            if (DwmGetCompositionTimingInfo(IntPtr.Zero, ref info) == 0 && info.qpcRefreshPeriod > 0 &&
                QueryPerformanceFrequency(out long f))
                return (double)f / info.qpcRefreshPeriod;
        }
        catch { }
        return 60;
    }
}
