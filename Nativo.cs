using System;
using System.Collections.Generic;
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

    // La barra nunca roba el foco ni sale en Alt+Tab, así Wallpaper Engine
    // no la cuenta como "otra aplicación enfocada" y no se pausa al usarla
    public static void NoActivable(IntPtr h) =>
        SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x08000000 | 0x80); // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW

    // Lo contrario: hace falta mientras un panel necesita escribir (el buscador del menú).
    // Se mantiene WS_EX_TOOLWINDOW para seguir fuera de Alt+Tab.
    public static void Activable(IntPtr h) =>
        SetWindowLong(h, -20, (GetWindowLong(h, -20) & ~0x08000000) | 0x80);

    public static void AbrirInicio()
    {
        keybd_event(0x5B, 0, 0, UIntPtr.Zero);
        keybd_event(0x5B, 0, 2, UIntPtr.Zero);
    }

    public static void MostrarBarraWindows(bool mostrar)
    {
        AppBar.AutoOcultarBarraWindows(!mostrar);
        ShowWindow(FindWindow("Shell_TrayWnd", null), mostrar ? 5 : 0);
    }

    // Solo esconder (sin tocar el auto-ocultar), para cuando Explorer la vuelve a mostrar
    public static void EsconderBarraWindows() => ShowWindow(FindWindow("Shell_TrayWnd", null), 0);

    public static bool BarraWindowsVisible() => IsWindowVisible(FindWindow("Shell_TrayWnd", null));

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
