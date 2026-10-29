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

    // Con varios monitores hay una isla (y por tanto una appbar) por pantalla, cada una
    // con su hwnd. Se lleva la cuenta por ventana: si no, la segunda isla no llegaría
    // a registrarse y al cerrar la primera se daría de baja un hwnd que no era el suyo.
    static readonly System.Collections.Generic.HashSet<IntPtr> _registradas = new();

    static APPBARDATA Datos(IntPtr h) => new() { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = h };

    public const uint ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    // El rectángulo que se pide es el del MONITOR de esa isla, en píxeles de pantalla:
    // así Windows reserva el hueco en la pantalla correcta y no siempre en la principal.
    //
    // `borde` es el de Windows (lo decide PanelNube.BordeAppBar) y `grosorPx` es lo que
    // se reserva en la dirección que toque: el alto si la franja es horizontal, el
    // ancho si es vertical. El ABM_QUERYPOS puede mover el filo que no fijamos nosotros
    // —si ya hay otra appbar en ese lado, Windows nos aparta—, así que al volver se
    // vuelve a clavar el grosor contra el filo exterior y solo entonces se asienta.
    public static void Registrar(IntPtr h, uint borde, int grosorPx, int mx, int my, int mAncho, int mAlto)
    {
        var d = Datos(h);
        if (_registradas.Add(h))
        {
            d.uCallbackMessage = RegisterWindowMessage("NubeBarAppBar");
            SHAppBarMessage(ABM_NEW, ref d);
        }

        if (mAncho <= 0 || mAlto <= 0) { mx = 0; my = 0; mAncho = GetSystemMetrics(0); mAlto = GetSystemMetrics(1); }

        d.uEdge = borde;
        d.rc = borde switch
        {
            ABE_LEFT   => new RECT { Left = mx,                      Right = mx + grosorPx, Top = my,                     Bottom = my + mAlto },
            ABE_RIGHT  => new RECT { Left = mx + mAncho - grosorPx,   Right = mx + mAncho,   Top = my,                     Bottom = my + mAlto },
            ABE_BOTTOM => new RECT { Left = mx,                      Right = mx + mAncho,   Top = my + mAlto - grosorPx,  Bottom = my + mAlto },
            _          => new RECT { Left = mx,                      Right = mx + mAncho,   Top = my,                     Bottom = my + grosorPx },
        };
        SHAppBarMessage(ABM_QUERYPOS, ref d);
        switch (borde)
        {
            case ABE_LEFT:   d.rc.Right = d.rc.Left + grosorPx; break;
            case ABE_RIGHT:  d.rc.Left = d.rc.Right - grosorPx; break;
            case ABE_BOTTOM: d.rc.Top = d.rc.Bottom - grosorPx; break;
            default:         d.rc.Bottom = d.rc.Top + grosorPx; break;
        }
        SHAppBarMessage(ABM_SETPOS, ref d);
    }

    public static void Quitar(IntPtr h)
    {
        if (!_registradas.Remove(h)) return;
        var d = Datos(h);
        SHAppBarMessage(ABM_REMOVE, ref d);
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
        var d = Datos(Nativo.BarraDeExplorer());

        if (_estadoOriginal == null)
        {
            // Si no hay fichero (no se pudo escribir, o lo borró alguien) se pregunta
            // a Windows. Pero en ese momento el "ocultar automáticamente" puede ser el
            // que pusimos nosotros: si se guardara tal cual, la barra se quedaría
            // oculta para siempre. Por eso se quita ese bit del estado de respaldo:
            // vale más dejarla visible de más que dejar al usuario sin barra.
            var guardado = LeerEstadoGuardado();
            _estadoOriginal = guardado ?? ((int)SHAppBarMessage(ABM_GETSTATE, ref d) & ~ABS_AUTOHIDE);
            if (guardado == null)
                Log.Info($"no había estado guardado de la barra: se toma {_estadoOriginal} (sin auto-ocultar)");
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
                ConfigArbol.EscribirAtomico(RutaEstado, estado.ToString());
        }
        // Que esto falle en silencio era peligroso: sin el fichero, un cierre feo
        // deja la barra en auto-ocultar y nadie sabe por qué
        catch (Exception ex) { Log.Error("guardar el estado de la barra de Windows", ex); }
    }

    static void BorrarEstadoGuardado()
    {
        try { if (System.IO.File.Exists(RutaEstado)) System.IO.File.Delete(RutaEstado); }
        catch { }
    }
}
