using System;
using System.Runtime.InteropServices;

namespace NubeBar;

// Reserva una franja de pantalla como hace la barra de Windows (o polybar con su "strut")
static class AppBar
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string? t);

    const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABM_GETSTATE = 4, ABM_SETSTATE = 10;
    const int ABS_AUTOHIDE = 1;
    static int? _estadoOriginal;
    static bool _registrada;

    static APPBARDATA Datos(IntPtr h) => new() { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = h };

    public static void Registrar(IntPtr h, bool arriba, int altoPx)
    {
        var d = Datos(h);
        if (!_registrada)
        {
            d.uCallbackMessage = RegisterWindowMessage("NubeBarAppBar");
            SHAppBarMessage(ABM_NEW, ref d);
            _registrada = true;
        }

        int ancho = GetSystemMetrics(0), alto = GetSystemMetrics(1); // pantalla principal en píxeles
        d.uEdge = arriba ? 1u : 3u;                                  // ABE_TOP / ABE_BOTTOM
        d.rc = new RECT
        {
            Left = 0, Right = ancho,
            Top = arriba ? 0 : alto - altoPx,
            Bottom = arriba ? altoPx : alto
        };
        SHAppBarMessage(ABM_QUERYPOS, ref d);
        if (arriba) d.rc.Bottom = d.rc.Top + altoPx; else d.rc.Top = d.rc.Bottom - altoPx;
        SHAppBarMessage(ABM_SETPOS, ref d);
    }

    public static void Quitar(IntPtr h)
    {
        if (!_registrada) return;
        var d = Datos(h);
        SHAppBarMessage(ABM_REMOVE, ref d);
        _registrada = false;
    }

    // Pone la barra de Windows en "ocultar automáticamente" para que libere su hueco
    // (si solo se esconde, Windows sigue reservando ese espacio abajo)
    // El estado de la barra se guarda también en disco. Si matan el proceso (o se
    // cuelga) sin pasar por aquí, la barra se queda en "ocultar automáticamente", y el
    // siguiente arranque tomaría ESE como el estado original y lo dejaría así para
    // siempre. Con el fichero, el primer cierre limpio la devuelve a como estaba.
    static string RutaEstado => System.IO.Path.Combine(Config.Carpeta, "barra-estado.txt");

    public static void AutoOcultarBarraWindows(bool autoOcultar)
    {
        var d = Datos(FindWindow("Shell_TrayWnd", null));

        if (_estadoOriginal == null)
        {
            _estadoOriginal = LeerEstadoGuardado() ?? (int)SHAppBarMessage(ABM_GETSTATE, ref d);
        }

        if (autoOcultar) GuardarEstado(_estadoOriginal.Value);

        d.lParam = (IntPtr)(autoOcultar ? (_estadoOriginal.Value | ABS_AUTOHIDE) : _estadoOriginal.Value);
        SHAppBarMessage(ABM_SETSTATE, ref d);

        if (!autoOcultar) BorrarEstadoGuardado();
    }

    static int? LeerEstadoGuardado()
    {
        try
        {
            if (System.IO.File.Exists(RutaEstado) &&
                int.TryParse(System.IO.File.ReadAllText(RutaEstado).Trim(), out int v)) return v;
        }
        catch { }
        return null;
    }

    static void GuardarEstado(int estado)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Config.Carpeta);
            if (!System.IO.File.Exists(RutaEstado))
                System.IO.File.WriteAllText(RutaEstado, estado.ToString());
        }
        catch { }
    }

    static void BorrarEstadoGuardado()
    {
        try { if (System.IO.File.Exists(RutaEstado)) System.IO.File.Delete(RutaEstado); }
        catch { }
    }
}
