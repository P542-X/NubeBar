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

public static class Nativo
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
    // Las ventanas del usuario. Quién cuenta como tal lo decide GestorVentanas con una
    // función pura sobre los datos de la ventana, y es la MISMA regla que usa la vista
    // general: antes había dos parecidas pero no iguales, y la de aquí vivía dentro de
    // una lambda donde no se podía comprobar.
    public static List<IntPtr> VentanasAbiertas(IntPtr self)
    {
        var lista = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            if (h != self && GestorVentanas.EsDelUsuario(Ficha(h), out string _)) lista.Add(h);
            return true;
        }, IntPtr.Zero);
        return lista;
    }

    public static IntPtr Activa() => GetForegroundWindow();

    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();

    // Si no hay ventana de shell, es que nadie hace de escritorio: ManagedShell
    // necesita saberlo para decidir si difunde "TaskbarCreated" al arrancar.
    public static IntPtr VentanaDelShell() => GetShellWindow();

    // La bandeja que encontraría Windows al repartir los Shell_NotifyIcon: la
    // primera Shell_TrayWnd en orden Z. Sirve para comprobar si ganamos nosotros.
    // Con "" se exige que el título sea exactamente vacío; con null vale cualquiera.
    // Una ventana por su título exacto, buscando entre las de nivel superior. La usa el
    // arnés de D9.5 para encontrar la ventana que ha abierto en otro proceso: su hwnd no lo
    // sabe nadie de este lado, y el título es único porque lleva la hora con milésimas.
    public static IntPtr PorTitulo(string titulo) => FindWindow(null!, titulo);

    public static IntPtr PrimeraBandeja() => FindWindow("Shell_TrayWnd", null);

    // La Shell_TrayWnd de Explorer, la de verdad. Con la bandeja encendida hay dos y
    // la nuestra (la de ManagedShell) va primera en orden Z: FindWindow devolvería la
    // nuestra, y entonces se escondía una ventana que ya está escondida mientras la
    // de Explorer, cuando volvía a salir (al abrir un Excel, por ejemplo), se quedaba
    // abajo a la vista sin que nadie la quitara.
    public static IntPtr BarraDeExplorer()
    {
        var h = IntPtr.Zero;
        while ((h = FindWindowEx(IntPtr.Zero, h, "Shell_TrayWnd", null)) != IntPtr.Zero)
            if (!EsNuestra(h)) return h;
        return IntPtr.Zero;
    }

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

    // ---------- Enseñar una ventana ESCONDIDA ----------
    //
    // `Activar` sirve para una ventana que está ahí, aunque esté minimizada. No sirve para
    // una que la aplicación ha **escondido**: `SW_RESTORE` sobre algo invisible no lo hace
    // visible, y `SetForegroundWindow` sobre lo invisible no hace nada.
    //
    // Y hace falta de verdad: Discord, Steam, Telegram y casi todo lo que vive en la bandeja
    // no se minimizan al pulsar la X ni al minimizar — **se esconden**. La ventana sigue
    // existiendo, pero deja de estar visible, así que sale de la lista de ventanas y NubeBar
    // creía que la app estaba cerrada.
    //
    // `SW_SHOW` primero y después el camino de siempre. Es lo que hace la barra de tareas de
    // Windows cuando pulsas el icono de una app escondida en la bandeja.
    // ---------- La memoria INSTALADA, no la que ve Windows ----------
    //
    // `TotalPhysicalMemory` da lo que el sistema puede usar, que es algo menos que lo que hay
    // puesto porque el hardware se reserva un trozo: con 32 GB instalados sale 31,8, y nadie
    // llama a eso «32 GB». Esta API da lo de la caja, en kilobytes.
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetPhysicallyInstalledSystemMemory(out long memoriaEnKB);

    public static bool MemoriaInstaladaKB(out long kb)
    {
        kb = 0;
        try { return GetPhysicallyInstalledSystemMemory(out kb); }
        catch { return false; }
    }

    // Esconder una ventana, igual que hacen las apps de bandeja. Existe para poder
    // REPRODUCIR ese caso con una ventana nuestra en un diagnóstico: sin esto, el arreglo de
    // `EscondidaDe` solo se podría comprobar teniendo Discord delante.
    public static void Esconder(IntPtr h) => ShowWindow(h, 0);   // SW_HIDE

    public static void MostrarYActivar(IntPtr h)
    {
        ShowWindow(h, 5);                   // SW_SHOW
        if (IsIconic(h)) ShowWindow(h, 9);  // SW_RESTORE, por si además estaba minimizada
        Activar(h);
    }


    [DllImport("user32.dll")] static extern int GetDoubleClickTime();

    public static int TiempoDobleClic => GetDoubleClickTime();

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    // El estado real del botón izquierdo, esté el puntero donde esté. Hace falta
    // para arrastrar la isla sin capturar el ratón.
    public static bool BotonIzquierdoPulsado() => (GetAsyncKeyState(0x01) & 0x8000) != 0;

    // El bit de abajo dice "se ha pulsado desde la última vez que preguntaste". Hace
    // falta para no perderse un clic corto entre dos vueltas del vigilante.
    public static bool HuboClicIzquierdo() => (GetAsyncKeyState(0x01) & 1) != 0;

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

    // ¿Podemos siquiera mirar ese proceso? Si ni abrirlo con el permiso más flojo
    // se puede, es de otro usuario (SYSTEM) o va elevado, y entonces Windows (UIPI)
    // tampoco nos deja mandarle mensajes de ventana: sus iconos de bandeja no
    // responden a nuestros clics y no hay forma de saberlo salvo preguntando esto.
    public static bool ProcesoAlcanzable(int pid)
    {
        const uint QueryLimited = 0x1000;
        var p = OpenProcess(QueryLimited, false, (uint)pid);
        if (p == IntPtr.Zero) return false;
        CloseHandle(p);
        return true;
    }

    // Dejar que la app dueña de un icono pueda ponerse delante. Sin esto, cuando
    // responde al clic con un menú o abriendo su ventana, Windows le niega el
    // SetForegroundWindow y no se ve nada.
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);

    public static bool PermitirPrimerPlano(int pid)
    {
        try { return AllowSetForegroundWindow(pid); } catch { return false; }
    }

    // Los clics la atraviesan y van a lo que haya debajo. Lo usa la escena de entrada:
    // mientras dura, el escritorio responde igual que si no estuviera.
    public static void Atravesable(IntPtr h) =>
        SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x20);   // WS_EX_TRANSPARENT

    // ¿Está pulsada esa tecla ahora mismo? Para una ventana que no recibe entrada a
    // propósito —la escena de entrada— es la única forma de atender a Esc.
    public static bool TeclaPulsada(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

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

    // ---------- Orden Z, para los modos de visibilidad del panel ----------
    // El Topmost de WPF solo sabe "arriba del todo" o "como una ventana normal". Para
    // "por-debajo" hay que mandarla al fondo a mano.
    static readonly IntPtr HWND_BOTTOM = new(1);
    static readonly IntPtr HWND_NOTOPMOST = new(-2);
    const uint SWP_QUIETO = 0x0001 | 0x0002 | 0x0010;   // NOSIZE | NOMOVE | NOACTIVATE

    static readonly IntPtr HWND_TOPMOST = new(-1);

    public static void AlFondo(IntPtr h) => SetWindowPos(h, HWND_BOTTOM, 0, 0, 0, 0, SWP_QUIETO);

    public static void NoSiempreEncima(IntPtr h) => SetWindowPos(h, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_QUIETO);

    // Arriba del todo, y además por delante de las demás ventanas que también sean
    // "siempre encima". Lo necesita el panel superpuesto de la barra vertical: ponerle
    // Topmost no basta si otra ventana topmost se puso después, y un lanzador abierto
    // tiene que verse sí o sí.
    public static void SiempreEncima(IntPtr h) => SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_QUIETO);

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
        // Nada de atajos por número: si se cierra una ventana y se abre otra entre dos
        // vueltas, los contadores coinciden y la nueva heredaría el exe cacheado de la
        // muerta (el icono se agrupaba con el programa equivocado).
        foreach (var muerta in _exes.Keys.Where(k => !vivas.Contains(k)).ToList()) _exes.Remove(muerta);
    }

    // ---------- Cerrar una ventana con educación ----------
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    // WM_CLOSE: la app decide (puede preguntar si guardas). Nunca se mata el proceso.
    // Cerrar por las buenas: se le PIDE a la ventana que se cierre, igual que al
    // pulsar su aspa. La app puede preguntarte si quieres guardar, y debe poder.
    public static void CerrarVentana(IntPtr h) => PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);

    public static int PidDe(IntPtr h)
    {
        GetWindowThreadProcessId(h, out int pid);
        return pid;
    }

    // Cerrar por las malas, como "Finalizar tarea" del Administrador de tareas:
    // el proceso muere sin poder guardar nada. Devuelve el porqué si no se pudo,
    // que casi siempre es que el proceso va como administrador y nosotros no.
    public static string? MatarProceso(int pid)
    {
        if (pid <= 0) return "no sé de qué proceso es esa ventana";
        if (pid == Environment.ProcessId) return "esa es la propia NubeBar";
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            string nombre = p.ProcessName;
            p.Kill(entireProcessTree: true);
            Log.Info($"proceso {nombre} ({pid}) finalizado a la fuerza");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"finalizar el proceso {pid}", ex);
            return ex is System.ComponentModel.Win32Exception
                ? "no tengo permiso: seguramente va como administrador"
                : ex.Message;
        }
    }

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
                        // ---------- Las flechas y compañía (D3) ----------
                        //
                        // Faltaban, y hacen falta: el foco por dirección se ata a las
                        // flechas porque es lo natural, y sin esto un atajo con flecha
                        // se quedaba en "no entiendo la combinación" sin decir cuál era
                        // el trozo que no se entendía.
                        "left" or "izquierda"  => 0x25,
                        "up" or "arriba"       => 0x26,
                        "right" or "derecha"   => 0x27,
                        "down" or "abajo"      => 0x28,
                        "oemplus" or "plus" or "mas"    => 0xBB,
                        "oemminus" or "minus" or "menos" => 0xBD,
                        "home" or "inicio"     => 0x24,
                        "end" or "fin"         => 0x23,
                        "pageup" or "pgup"     => 0x21,
                        "pagedown" or "pgdn"   => 0x22,
                        "delete" or "supr"     => 0x2E,
                        "back" or "retroceso"  => 0x08,
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
        var principal = BarraDeExplorer();
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

    // La medida es diferencial: cada llamada compara con la anterior. Si se llama dos
    // veces seguidas (el módulo de la isla, la gráfica del menú y el historial lo
    // hacían en el mismo tick), la segunda no tiene tiempo que comparar y devuelve 0:
    // por eso la isla marcaba "CPU 0%" casi siempre. Ahora la lectura real se hace
    // como mucho cada medio segundo y las demás reciben la misma cifra.
    static int _ultimoCpu;
    static long _marcaCpu;

    public static int Cpu()
    {
        long ahora = Environment.TickCount64;
        if (_cpuMedida && ahora - _marcaCpu < 500) return _ultimoCpu;
        _marcaCpu = ahora;

        GetSystemTimes(out var i, out var k, out var u);
        long di = i - _pi, dt = (k - _pk) + (u - _pu); // kernel incluye idle
        _pi = i; _pk = k; _pu = u;
        if (!_cpuMedida) { _cpuMedida = true; return 0; }  // la primera lectura no tiene con qué compararse
        _ultimoCpu = dt <= 0 ? _ultimoCpu : (int)Math.Round(100.0 * (dt - di) / dt);
        return _ultimoCpu;
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

    // ================= C4: ventanas reales =================

    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);

    public static bool Existe(IntPtr h) => h != IntPtr.Zero && IsWindow(h);
    public static bool EstaMaximizada(IntPtr h) => IsZoomed(h);

    public static IntPtr MonitorDe(IntPtr h) => MonitorFromWindow(h, 2);   // NEAREST

    // Los hechos de una ventana, leídos de golpe.
    //
    // Se saca TODO de una pasada y se devuelve como datos planos a propósito: así quien
    // decide si una ventana es del usuario no necesita que la ventana exista —se le
    // puede dar una ficha inventada— y esa decisión se puede comprobar sin abrir nada.
    // Era el trozo de la barra de tareas que más se equivocaba y el único que no se
    // podía mirar.
    public readonly record struct FichaVentana(
        IntPtr Hwnd,
        bool Visible,
        int LargoTitulo,
        bool TieneDueno,
        int EstiloEx,
        int Estilo,
        bool Tapada,            // DWM la tiene "cloaked": otro escritorio virtual, o suspendida
        bool EsNuestra,
        string Clase,
        int Pid);

    public static FichaVentana Ficha(IntPtr h)
    {
        int tapada = 0;
        try { DwmGetWindowAttribute(h, 14, out tapada, sizeof(int)); } catch { }
        GetWindowThreadProcessId(h, out int pid);
        return new FichaVentana(
            h,
            IsWindowVisible(h),
            GetWindowTextLength(h),
            GetWindow(h, 4) != IntPtr.Zero,          // GW_OWNER
            GetWindowLong(h, -20),                   // GWL_EXSTYLE
            GetWindowLong(h, -16),                   // GWL_STYLE
            tapada != 0,
            EsNuestra(h),
            Clase(h),
            pid);
    }

    // Todas las ventanas de nivel superior, sin filtrar. Quien filtra es GestorVentanas.
    public static List<IntPtr> TodasLasDeNivelSuperior()
    {
        var lista = new List<IntPtr>();
        EnumWindows((h, _) => { lista.Add(h); return true; }, IntPtr.Zero);
        return lista;
    }

    // ---------- Colocar una ventana ----------
    //
    // Restaurar ANTES de mover no es un detalle: a una ventana maximizada Windows le
    // ignora el SetWindowPos y se queda donde estaba, y a una minimizada la mueve sin
    // enseñarla. Las dos cosas se leen como "no ha pasado nada".
    public static bool ColocarVentanaDelUsuario(IntPtr h, int x, int y, int ancho, int alto)
    {
        if (!Existe(h)) return false;
        try
        {
            if (IsIconic(h) || IsZoomed(h)) ShowWindow(h, 9);    // SW_RESTORE
            // SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED
            return SetWindowPos(h, IntPtr.Zero, x, y, ancho, alto, 0x0004 | 0x0010 | 0x0020);
        }
        catch (Exception ex) { Log.Error("colocar una ventana", ex); return false; }
    }

    public static void Maximizar(IntPtr h)
    {
        if (!Existe(h)) return;
        const uint WM_SYSCOMMAND = 0x0112;
        const int SC_MAXIMIZE = 0xF030;
        PostMessage(h, WM_SYSCOMMAND, (IntPtr)SC_MAXIMIZE, IntPtr.Zero);
    }

    public static void Restaurar(IntPtr h)
    {
        if (!Existe(h)) return;
        const uint WM_SYSCOMMAND = 0x0112;
        const int SC_RESTORE = 0xF120;
        PostMessage(h, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
    }

    // ---------- Enterarse de lo que hacen las ventanas ----------
    //
    // `SetWinEventHook` fuera de contexto: Windows avisa a NUESTRO proceso cuando pasa
    // algo, sin inyectar ninguna DLL en los demás y sin procesos residentes. Las
    // llamadas llegan al hilo que instaló el gancho y solo mientras ese hilo bombee
    // mensajes, así que instalándolo desde el hilo de interfaz las notificaciones ya
    // vienen en el hilo correcto.
    //
    // OJO con el delegado: si se deja que lo recoja el recolector de basura, la
    // siguiente llamada de Windows salta a memoria liberada y tira el proceso. Por eso
    // se guarda en un campo estático. Es el error clásico de esta API.
    public delegate void GanchoEventos(IntPtr gancho, uint evento, IntPtr hwnd,
                                       int objeto, int hijo, uint hilo, uint cuando);

    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr dll, GanchoEventos cb,
                                         uint pid, uint hilo, uint flags);

    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr gancho);

    public const uint EvtPrimerPlano = 0x0003;     // EVENT_SYSTEM_FOREGROUND
    public const uint EvtMoverEmpieza = 0x000A;    // EVENT_SYSTEM_MOVESIZESTART
    public const uint EvtMoverTermina = 0x000B;    // EVENT_SYSTEM_MOVESIZEEND
    public const uint EvtMinimizar = 0x0016;       // EVENT_SYSTEM_MINIMIZESTART
    public const uint EvtRestaurar = 0x0017;       // EVENT_SYSTEM_MINIMIZEEND
    public const uint EvtCrear = 0x8000;           // EVENT_OBJECT_CREATE
    public const uint EvtDestruir = 0x8001;        // EVENT_OBJECT_DESTROY
    public const uint EvtMostrar = 0x8002;         // EVENT_OBJECT_SHOW
    public const uint EvtEsconder = 0x8003;        // EVENT_OBJECT_HIDE
    public const uint EvtSitio = 0x800B;           // EVENT_OBJECT_LOCATIONCHANGE
    public const uint EvtNombre = 0x800C;          // EVENT_OBJECT_NAMECHANGE

    static readonly List<GanchoEventos> _delegadosVivos = new();
    static readonly List<IntPtr> _ganchosVivos = new();

    public static IntPtr EscucharEventos(uint desde, uint hasta, GanchoEventos cb)
    {
        try
        {
            // WINEVENT_OUTOFCONTEXT (0) | WINEVENT_SKIPOWNPROCESS (2): nuestras propias
            // ventanas no tienen por qué avisarnos de nada.
            var g = SetWinEventHook(desde, hasta, IntPtr.Zero, cb, 0, 0, 0 | 2);
            if (g == IntPtr.Zero) { Log.Info($"no se pudo escuchar los eventos {desde:X}-{hasta:X}"); return IntPtr.Zero; }
            _delegadosVivos.Add(cb);      // que no lo recoja el GC mientras Windows lo tenga
            _ganchosVivos.Add(g);
            return g;
        }
        catch (Exception ex) { Log.Error("escuchar eventos de ventanas", ex); return IntPtr.Zero; }
    }

    // Se llama al cerrar NubeBar. Si se olvidara, los ganchos mueren igualmente con el
    // proceso, pero dejarlo explícito es lo que permite decir que no hay fugas.
    public static void DejarDeEscuchar()
    {
        foreach (var g in _ganchosVivos)
            try { UnhookWinEvent(g); } catch { }
        Log.Info($"ganchos de ventanas liberados: {_ganchosVivos.Count}");
        _ganchosVivos.Clear();
        _delegadosVivos.Clear();
    }

    // ---------- Una foto de la ventana ----------
    //
    // `PrintWindow` con PW_RENDERFULLCONTENT (2) le pide a la ventana que se dibuje en
    // un lienzo nuestro. Es la única vía que sirve aquí: las miniaturas de DWM no se
    // componen dentro de ventanas por capas, y TODAS las de NubeBar lo son (la isla es
    // redondeada y translúcida). Ver DECISIONES.md.
    //
    // Devuelve null si no se puede, y quien llama enseña la tarjeta con icono y título.
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);

    // ---------- Y una copia de LA PANTALLA ----------
    //
    // `PrintWindow` le pide a la ventana que se redibuje en un lienzo nuestro. Eso es lo que
    // hace falta para las miniaturas, pero **no es lo que el usuario ve**: la ventana se
    // dibuja a sí misma, sin lo que hay detrás y sin cómo el compositor la mezcla. En una
    // ventana por capas —y todas las de NubeBar lo son— las dos cosas no tienen por qué
    // coincidir, y conviene poder comprobar cuál es cuál.
    //
    // Esto copia los píxeles de la pantalla, que es lo que ven los ojos. Solo para
    // diagnóstico: no vale para las miniaturas de la vista general, porque ahí las ventanas
    // están tapadas unas por otras y lo que se copiaría sería la de encima.
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr destino, int x, int y, int w, int alt,
                                                       IntPtr origen, int xo, int yo, uint rop);
    const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;

    public static System.Windows.Media.Imaging.BitmapSource? FotoDeLaPantalla(
        int x, int y, int w, int alt)
    {
        if (w <= 0 || alt <= 0 || w > 20000 || alt > 20000) return null;

        IntPtr dcPantalla = IntPtr.Zero, dcMemoria = IntPtr.Zero, mapa = IntPtr.Zero, antiguo = IntPtr.Zero;
        try
        {
            dcPantalla = GetDC(IntPtr.Zero);
            if (dcPantalla == IntPtr.Zero) return null;
            dcMemoria = CreateCompatibleDC(dcPantalla);
            mapa = CreateCompatibleBitmap(dcPantalla, w, alt);
            if (dcMemoria == IntPtr.Zero || mapa == IntPtr.Zero) return null;
            antiguo = SelectObject(dcMemoria, mapa);

            // CAPTUREBLT es lo que incluye las ventanas por capas. Sin él, la isla y los
            // paneles —que son justo lo que hay que fotografiar— salen como un agujero.
            if (!BitBlt(dcMemoria, 0, 0, w, alt, dcPantalla, x, y, SRCCOPY | CAPTUREBLT))
                return null;

            var fuente = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                mapa, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            fuente.Freeze();
            return fuente;
        }
        catch { return null; }
        finally
        {
            if (antiguo != IntPtr.Zero) SelectObject(dcMemoria, antiguo);
            if (mapa != IntPtr.Zero) DeleteObject(mapa);
            if (dcMemoria != IntPtr.Zero) DeleteDC(dcMemoria);
            if (dcPantalla != IntPtr.Zero) ReleaseDC(IntPtr.Zero, dcPantalla);
        }
    }

    // ---------- ¿Está lisa? ----------
    //
    // Veinticinco puntos repartidos por toda la imagen, bordes incluidos. Si los
    // veinticinco son el mismo color, ahí no hay una ventana dibujada.
    //
    // Se muestrea y no se recorre entera a propósito: esto corre por cada ventana cada vez
    // que se abre la vista, y leer treinta megapíxeles para contestar «¿hay algo?» costaría
    // más que sacar la foto.
    //
    // La decisión de si una serie de muestras es lisa vive aparte y tiene pruebas
    // (`Miniaturas.TodasIguales`): lo que no se puede comprobar sin ventanas de verdad es
    // el muestreo, no el criterio.
    internal static bool FotoLisa(System.Windows.Media.Imaging.BitmapSource fuente)
    {
        try
        {
            int ancho = fuente.PixelWidth, alto = fuente.PixelHeight;
            if (ancho < 4 || alto < 4) return false;      // tan pequeña que no se puede juzgar

            var muestras = new List<uint>(25);
            var pixel = new byte[4];
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    int x = Math.Min(ancho - 1, (int)(i * (ancho - 1) / 4.0));
                    int y = Math.Min(alto - 1, (int)(j * (alto - 1) / 4.0));
                    fuente.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
                    muestras.Add(BitConverter.ToUInt32(pixel, 0));
                }
            return Miniaturas.TodasIguales(muestras);
        }
        catch
        {
            // Si no se puede mirar, se da por buena: quitarle la miniatura a una ventana
            // por no haber podido comprobarla sería cambiar un fallo por otro.
            return false;
        }
    }

    // `rechazarLisa` solo lo pide quien va a enseñar la foto como MINIATURA de una ventana
    // (ver `Miniaturas`). Y es opcional por un fallo que duró media hora: se puso dentro de
    // `Foto` para todo el mundo, y las capturas de diagnóstico de los paneles dejaron de
    // salir —«PrintWindow no devolvió nada»— porque la isla es una ventana en capas de
    // 2560 px de ancho casi toda transparente: veinticinco puntos repartidos por ahí caen
    // todos en el mismo vacío y la foto, que era perfectamente buena, se tiraba.
    //
    // La lección: «aquí no hay nada dibujado» depende de PARA QUÉ se quiere la foto. En una
    // tarjeta de 300 px que enseña una ventana, un rectángulo liso es un fallo; en la
    // captura de una superficie flotante, es el fondo.
    public static System.Windows.Media.Imaging.BitmapSource? Foto(IntPtr h, int anchoMax,
                                                                  bool rechazarLisa = false)
    {
        if (!Existe(h) || IsIconic(h)) return null;
        var r = Rectangulo(h);
        int w = r.Der - r.Izq, alt = r.Aba - r.Arr;
        if (w <= 0 || alt <= 0 || w > 20000 || alt > 20000) return null;

        IntPtr dcVentana = IntPtr.Zero, dcMemoria = IntPtr.Zero, mapa = IntPtr.Zero, antiguo = IntPtr.Zero;
        try
        {
            dcVentana = GetWindowDC(h);
            if (dcVentana == IntPtr.Zero) return null;
            dcMemoria = CreateCompatibleDC(dcVentana);
            mapa = CreateCompatibleBitmap(dcVentana, w, alt);
            if (dcMemoria == IntPtr.Zero || mapa == IntPtr.Zero) return null;
            antiguo = SelectObject(dcMemoria, mapa);

            if (!PrintWindow(h, dcMemoria, 2)) return null;

            var fuente = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                mapa, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            // ---------- Una foto lisa no es una foto ----------
            //
            // `PrintWindow` devuelve `true` y deja el mapa entero de un color con las
            // aplicaciones que se dibujan con la GPU por su cuenta —Electron y Chromium,
            // que es media barra de tareas de cualquiera: Discord, ChatGPT, VS Code—.
            //
            // El código de antes se fiaba del `true`, ponía ese rectángulo liso en la
            // tarjeta y escondía el icono de respaldo. En pantalla salían tarjetas grises
            // vacías: es la diferencia entre un centro de ventanas y una cuadrícula de
            // huecos. Devolviendo null, la tarjeta se queda con el icono de la aplicación,
            // que dice bastante más que un rectángulo gris.
            if (rechazarLisa && FotoLisa(fuente)) return null;

            // Se guarda ya encogida: una tarjeta mide 200 px y quedarse la foto entera
            // de un 4K son 33 MB por ventana.
            double escala = Math.Min(1.0, anchoMax / (double)w);
            var pequena = new System.Windows.Media.Imaging.TransformedBitmap(
                fuente, new System.Windows.Media.ScaleTransform(escala, escala));
            pequena.Freeze();
            return pequena;
        }
        catch { return null; }
        finally
        {
            if (antiguo != IntPtr.Zero) SelectObject(dcMemoria, antiguo);
            if (mapa != IntPtr.Zero) DeleteObject(mapa);
            if (dcMemoria != IntPtr.Zero) DeleteDC(dcMemoria);
            if (dcVentana != IntPtr.Zero) ReleaseDC(h, dcVentana);
        }
    }
}
