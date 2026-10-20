using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NubeBar;

static class Nativo
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int idx, int val);
    [DllImport("user32.dll")] static extern IntPtr GetClassLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string? t);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr padre, IntPtr tras, string? clase, string? titulo);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);

    // ---------- Ventanas ----------
    public static List<IntPtr> VentanasAbiertas(IntPtr self)
    {
        var lista = new List<IntPtr>();
        var escritorio = FindWindow("Progman", null);
        EnumWindows((h, _) =>
        {
            if (h != self && h != escritorio && EsVentanaDeBarra(h)) lista.Add(h);
            return true;
        }, IntPtr.Zero);
        return lista;
    }

    static bool EsVentanaDeBarra(IntPtr h)
    {
        if (!IsWindowVisible(h) || GetWindowTextLength(h) == 0) return false;
        if (GetWindow(h, 4) != IntPtr.Zero) return false;            // GW_OWNER
        if ((GetWindowLong(h, -20) & 0x80) != 0) return false;        // WS_EX_TOOLWINDOW
        DwmGetWindowAttribute(h, 14, out int cloaked, sizeof(int));   // DWMWA_CLOAKED
        return cloaked == 0;
    }

    public static IntPtr Activa() => GetForegroundWindow();

    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();

    // Si no hay ventana de shell, es que nadie hace de escritorio: ManagedShell
    // necesita saberlo para decidir si difunde "TaskbarCreated" al arrancar.
    public static IntPtr VentanaDelShell() => GetShellWindow();

    // La bandeja que encontraría Windows al repartir los Shell_NotifyIcon: la
    // primera Shell_TrayWnd en orden Z. Sirve para comprobar si ganamos nosotros.
    public static IntPtr PrimeraBandeja() => FindWindow("Shell_TrayWnd", "");

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Izq, Arr, Der, Aba; }

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);

    public static RECT Rectangulo(IntPtr h) => GetWindowRect(h, out var r) ? r : default;

    public static string Clase(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string Titulo(IntPtr h)
    {
        var sb = new StringBuilder(GetWindowTextLength(h) + 1);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static ImageSource? Icono(IntPtr h)
    {
        SendMessageTimeout(h, 0x7F, (IntPtr)2, IntPtr.Zero, 2, 50, out var hIcon); // WM_GETICON
        if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtr(h, -34);                 // GCLP_HICONSM
        if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtr(h, -14);                 // GCLP_HICON
        if (hIcon == IntPtr.Zero) return null;
        try { return Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); }
        catch { return null; }
    }

    public static void Activar(IntPtr h)
    {
        if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
        if (SetForegroundWindow(h)) return;
        keybd_event(0x12, 0, 0, UIntPtr.Zero); // truco de Alt para que Windows permita cambiar el foco
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetForegroundWindow(h);
    }

    [DllImport("user32.dll")] static extern int GetDoubleClickTime();

    public static int TiempoDobleClic => GetDoubleClickTime();

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    // El estado real del botón izquierdo, esté el puntero donde esté. Hace falta
    // para arrastrar la isla sin capturar el ratón.
    public static bool BotonIzquierdoPulsado() => (GetAsyncKeyState(0x01) & 0x8000) != 0;

    public static bool EstaMinimizada(IntPtr h) => IsIconic(h);

    // Minimizar como lo hace la barra de tareas: se le MANDA la orden a la ventana
    // en vez de imponérsela. Así cada app hace su animación y su guardado de estado,
    // y funciona con las de pantalla completa sin bordes y con las de la Store.
    // ShowWindow queda de respaldo por si alguna no atiende el mensaje.
    public static void Minimizar(IntPtr h)
    {
        const uint WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020;
        PostMessage(h, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
    }

    public static void MinimizarAlaFuerza(IntPtr h) => ShowWindow(h, 6);   // SW_MINIMIZE

    // ¿Esta ventana es de NubeBar? Hace falta para no confundir nuestros propios
    // paneles (el menú Inicio y los ajustes sí cogen el foco) con "la app activa"
    static int _nuestroPid;

    public static bool EsNuestra(IntPtr h)
    {
        if (_nuestroPid == 0) _nuestroPid = Environment.ProcessId;
        GetWindowThreadProcessId(h, out int pid);
        return pid == _nuestroPid;
    }

    // La barra nunca roba el foco ni sale en Alt+Tab, así Wallpaper Engine
    // no la cuenta como "otra aplicación enfocada" y no se pausa al usarla
    public static void NoActivable(IntPtr h) =>
        SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x08000000 | 0x80); // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr h, IntPtr tras, int x, int y, int cx, int cy, uint flags);

    // Lo contrario: hace falta mientras un panel necesita escribir (el buscador del menú).
    // Se mantiene WS_EX_TOOLWINDOW para seguir fuera de Alt+Tab.
    // No basta con cambiar el estilo: hasta que no se avisa con SWP_FRAMECHANGED,
    // Windows sigue tratando la ventana como no activable y no le da el foco.
    public static void Activable(IntPtr h)
    {
        SetWindowLong(h, -20, (GetWindowLong(h, -20) & ~0x08000000) | 0x80);
        // SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE
        SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0004 | 0x0020 | 0x0010);
    }

    // Colocar en píxeles de pantalla de verdad. Con monitores de distinto DPI las
    // unidades de WPF no sirven para posicionar entre pantallas: hay que ir en crudo
    // y dejar que WPF convierta a su escala el contenido de dentro.
    public static void Colocar(IntPtr h, int x, int y, int ancho, int alto) =>
        SetWindowPos(h, IntPtr.Zero, x, y, ancho, alto, 0x0010 | 0x0004);   // SWP_NOACTIVATE | SWP_NOZORDER

    // Traer al frente de verdad. Windows bloquea el cambio de primer plano a procesos
    // que no acaban de recibir entrada del usuario; el toque de Alt levanta ese bloqueo.
    public static bool ForzarPrimerPlano(IntPtr h)
    {
        if (SetForegroundWindow(h) && GetForegroundWindow() == h) return true;
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetForegroundWindow(h);
        return GetForegroundWindow() == h;
    }

    // ---------- Ejecutable de cada ventana (para agrupar) ----------
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint acceso, bool heredar, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr proceso, uint flags, StringBuilder ruta, ref int n);

    // Preguntar la ruta abre y cierra un handle de proceso: se guarda por ventana
    static readonly Dictionary<IntPtr, string> _exes = new();

    // ---------- Identidad de las apps de la Store ----------
    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Sequential)]
    struct PROPVARIANT
    {
        public ushort vt;
        ushort r1, r2, r3;
        public IntPtr valor;
        public IntPtr relleno;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        int GetCount(out uint cuantas);
        int GetAt(uint i, out PROPERTYKEY clave);
        int GetValue(ref PROPERTYKEY clave, out PROPVARIANT valor);
        int SetValue(ref PROPERTYKEY clave, ref PROPVARIANT valor);
        int Commit();
    }

    [DllImport("shell32.dll")]
    static extern int SHGetPropertyStoreForWindow(IntPtr h, ref Guid iid,
                                                  [MarshalAs(UnmanagedType.Interface)] out object almacen);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

    const string AnfitrionStore = "ApplicationFrameHost.exe";
    const ushort VT_LPWSTR = 31;

    // El identificador con el que Windows distingue las apps empaquetadas, por
    // ejemplo "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App". Es lo mismo que usa
    // la barra de tareas de verdad para decidir qué va junto y qué no.
    static string? IdDeAplicacion(IntPtr h)
    {
        object? almacen = null;
        var pv = new PROPVARIANT();
        try
        {
            var iid = typeof(IPropertyStore).GUID;
            if (SHGetPropertyStoreForWindow(h, ref iid, out almacen) != 0) return null;
            if (almacen is not IPropertyStore ps) return null;

            // PKEY_AppUserModel_ID
            var clave = new PROPERTYKEY
            {
                fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                pid = 5
            };
            if (ps.GetValue(ref clave, out pv) != 0) return null;
            if (pv.vt != VT_LPWSTR || pv.valor == IntPtr.Zero) return null;
            return Marshal.PtrToStringUni(pv.valor);
        }
        catch { return null; }
        finally
        {
            PropVariantClear(ref pv);
            if (almacen != null && Marshal.IsComObject(almacen)) Marshal.ReleaseComObject(almacen);
        }
    }

    public static string Exe(IntPtr h)
    {
        if (_exes.TryGetValue(h, out var guardado)) return guardado;

        var ruta = RutaDelProceso(h);

        // Las apps de la Store (Calculadora, Configuración, Fotos...) son todas
        // ventanas de ApplicationFrameHost.exe, así que agrupando por ejecutable
        // acabarían en un solo icono. Se las distingue por su AppUserModelId.
        if (TerminaEn(ruta, AnfitrionStore) && IdDeAplicacion(h) is { Length: > 0 } id)
            ruta = id;

        _exes[h] = ruta;
        return ruta;
    }

    static bool TerminaEn(string ruta, string nombre) =>
        ruta.EndsWith("\\" + nombre, StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals(nombre, StringComparison.OrdinalIgnoreCase);

    static string RutaDelProceso(IntPtr h)
    {
        GetWindowThreadProcessId(h, out int pid);
        if (pid == 0) return "";
        var proceso = OpenProcess(0x1000, false, (uint)pid);   // PROCESS_QUERY_LIMITED_INFORMATION
        if (proceso == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(512);
            int n = sb.Capacity;
            return QueryFullProcessImageName(proceso, 0, sb, ref n) ? sb.ToString() : "";
        }
        finally { CloseHandle(proceso); }
    }

    // Windows reaprovecha los hwnd, así que hay que soltar los de ventanas que ya no existen
    public static void OlvidarExes(ICollection<IntPtr> vivas)
    {
        if (_exes.Count <= vivas.Count) return;
        foreach (var muerta in _exes.Keys.Where(k => !vivas.Contains(k)).ToList()) _exes.Remove(muerta);
    }

    // ---------- Cerrar una ventana con educación ----------
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    // WM_CLOSE: la app decide (puede preguntar si guardas). Nunca se mata el proceso.
    public static void CerrarVentana(IntPtr h) => PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);

    // ---------- Atajo global ----------
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    public static int UltimoErrorAtajo { get; private set; }

    public const int WM_HOTKEY = 0x0312;

    // "Ctrl+Alt+Space" -> modificadores + tecla. Devuelve false si no se entiende.
    public static bool TraducirAtajo(string atajo, out uint mod, out uint vk)
    {
        mod = 0; vk = 0;
        foreach (var trozo in atajo.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (trozo.Trim().ToLowerInvariant())
            {
                case "ctrl" or "control": mod |= 0x0002; break;   // MOD_CONTROL
                case "alt":               mod |= 0x0001; break;   // MOD_ALT
                case "shift":             mod |= 0x0004; break;   // MOD_SHIFT
                case "win":               mod |= 0x0008; break;   // MOD_WIN
                default:
                    var t = trozo.Trim();
                    vk = t.ToLowerInvariant() switch
                    {
                        "space" or "espacio" => 0x20,
                        "tab"                => 0x09,
                        "enter" or "intro"   => 0x0D,
                        "esc"                => 0x1B,
                        _ when t.Length == 1 => (uint)char.ToUpperInvariant(t[0]),
                        _ when t.Length > 1 && t[0] is 'F' or 'f' && int.TryParse(t[1..], out int n) && n is >= 1 and <= 24
                                             => (uint)(0x70 + n - 1),
                        _ => 0
                    };
                    break;
            }
        }
        return vk != 0;
    }

    public static bool RegistrarAtajo(IntPtr h, int id, string atajo)
    {
        UltimoErrorAtajo = 0;
        if (!TraducirAtajo(atajo, out uint mod, out uint vk)) { UltimoErrorAtajo = -1; return false; }
        UnregisterHotKey(h, id);                 // por si ya estaba
        bool ok = RegisterHotKey(h, id, mod | 0x4000, vk);   // MOD_NOREPEAT
        if (!ok) UltimoErrorAtajo = Marshal.GetLastWin32Error();
        return ok;
    }

    public static void QuitarAtajo(IntPtr h, int id) => UnregisterHotKey(h, id);

    public static void AbrirInicio()
    {
        keybd_event(0x5B, 0, 0, UIntPtr.Zero);
        keybd_event(0x5B, 0, 2, UIntPtr.Zero);
    }

    // Con varios monitores hay una barra principal y una secundaria por cada uno
    // de los demás. Todas se esconden y todas se devuelven.
    public static List<IntPtr> BarrasDeWindows()
    {
        var lista = new List<IntPtr>();
        var principal = FindWindow("Shell_TrayWnd", null);
        if (principal != IntPtr.Zero) lista.Add(principal);

        var h = IntPtr.Zero;
        while ((h = FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            lista.Add(h);
        return lista;
    }

    // Solo se esconden las barras de las pantallas donde NubeBar pone isla. Si
    // tienes Barra.Monitores="principal", el monitor secundario se quedaría sin
    // ninguna barra: ni la suya ni la nuestra. Con null se tocan todas, que es lo
    // que hace falta al devolverlas.
    static List<IntPtr> BarrasEn(HashSet<IntPtr>? monitores)
    {
        var todas = BarrasDeWindows();
        if (monitores == null) return todas;
        return todas.Where(b => monitores.Contains(Monitores.HandleDe(b))).ToList();
    }

    // El orden importa, y es distinto en cada sentido. Al devolverlas, primero que
    // se VEAN (ShowWindow es instantáneo) y después el "ocultar automáticamente",
    // que tarda porque Explorer recalcula el área de trabajo y avisa a todas las
    // ventanas: así, si alguien está mirando, la barra reaparece ya. Al esconderlas,
    // al revés, para que no se vea el hueco saltar.
    public static void MostrarBarraWindows(bool mostrar, HashSet<IntPtr>? monitores = null)
    {
        if (mostrar)
        {
            foreach (var b in BarrasDeWindows()) ShowWindow(b, 5);   // al devolver, TODAS
            AppBar.AutoOcultarBarraWindows(false);
        }
        else
        {
            AppBar.AutoOcultarBarraWindows(true);
            foreach (var b in BarrasEn(monitores)) ShowWindow(b, 0);
        }
    }

    // Solo esconder (sin tocar el auto-ocultar), para cuando Explorer las vuelve a mostrar
    public static void EsconderBarraWindows(HashSet<IntPtr>? monitores = null)
    {
        foreach (var b in BarrasEn(monitores)) ShowWindow(b, 0);
    }

    public static bool BarraWindowsVisible(HashSet<IntPtr>? monitores = null) =>
        BarrasEn(monitores).Any(IsWindowVisible);

    // ---------- Sistema ----------
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    static long _pi, _pk, _pu;
    static bool _cpuMedida;

    public static int Cpu()
    {
        GetSystemTimes(out var i, out var k, out var u);
        long di = i - _pi, dt = (k - _pk) + (u - _pu); // kernel incluye idle
        _pi = i; _pk = k; _pu = u;
        if (!_cpuMedida) { _cpuMedida = true; return 0; }  // la primera lectura no tiene con qué compararse
        return dt <= 0 ? 0 : (int)Math.Round(100.0 * (dt - di) / dt);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong total, avail, totalPage, availPage, totalVirtual, availVirtual, availExtended;
    }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    public static int Ram()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref m);
        return (int)m.dwMemoryLoad;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS { public byte AC, Flag, Percent, Saver; public int Life, FullLife; }
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

    // "" si es un sobremesa sin batería (el módulo se oculta solo)
    public static string Bateria()
    {
        GetSystemPowerStatus(out var s);
        if (s.Flag == 128 || s.Percent == 255) return "";
        return $"{(s.AC == 1 ? "AC" : "BAT")} {s.Percent}%";
    }
}
