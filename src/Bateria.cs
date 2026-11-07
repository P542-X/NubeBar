using System;
using System.Runtime.InteropServices;

namespace NubeBar;

// La batería y el modo de energía de Windows (el deslizador "Eficiencia /
// Equilibrado / Rendimiento" de Configuración > Sistema > Energía).
static class Bateria
{
    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS { public byte AC, Flag, Percent, Saver; public int Life, FullLife; }
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

    // Segundos = -1 cuando Windows aún no lo sabe (recién desenchufado) o está enchufado
    public sealed record Estado(bool HayBateria, bool Enchufado, bool Cargando, int PorCiento, bool Ahorro, int Segundos);

    public static Estado Leer()
    {
        if (!GetSystemPowerStatus(out var s)) return new Estado(false, true, false, 0, false, -1);
        bool hay = s.Flag != 128 && s.Percent != 255;
        return new Estado(hay, s.AC == 1, (s.Flag & 8) != 0, Math.Min((int)s.Percent, 100), s.Saver == 1, s.Life);
    }

    // Icono de la pila con su nivel (de 10 en 10) y el rayo si está cargando.
    // Son los glifos "MobBattery" de Segoe MDL2/Fluent: EBA0..EBAA y, cargando, EBAB..EBB5.
    public static string Glifo(Estado e)
    {
        int nivel = Math.Clamp((int)Math.Round(e.PorCiento / 10.0), 0, 10);
        return ((char)((e.Cargando || (e.Enchufado && e.PorCiento < 100) ? 0xEBAB : 0xEBA0) + nivel)).ToString();
    }

    public static string Texto(Estado e)
    {
        if (!e.HayBateria) return "Sin batería";
        if (e.Cargando) return "Cargando";
        if (e.Enchufado) return e.PorCiento >= 100 ? "Enchufado · cargada" : "Enchufado · sin cargar";
        if (e.Segundos < 0) return "Con batería · calculando lo que queda…";
        var t = TimeSpan.FromSeconds(e.Segundos);
        return t.TotalHours >= 1 ? $"Con batería · quedan {(int)t.TotalHours} h {t.Minutes} min"
                                 : $"Con batería · quedan {t.Minutes} min";
    }

    // ---------- Modo de energía ----------
    // Son los "overlay" de energía de Windows 10/11. powrprof los exporta aunque no
    // salgan en la documentación principal; son los que usa la propia Configuración.
    public enum Modo { Eficiencia, Equilibrado, Rendimiento }

    static readonly Guid Eficiente = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    static readonly Guid Rapido = new("ded574b5-45a0-4f42-8737-46345c09c238");

    [DllImport("powrprof.dll")] static extern uint PowerGetEffectiveOverlayScheme(out Guid g);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveOverlayScheme(Guid g);

    public static Modo? ModoActual()
    {
        try
        {
            if (PowerGetEffectiveOverlayScheme(out var g) != 0) return null;
            return g == Eficiente ? Modo.Eficiencia : g == Rapido ? Modo.Rendimiento : Modo.Equilibrado;
        }
        catch { return null; }    // Windows sin overlays: el selector no sale
    }

    public static bool PonerModo(Modo m)
    {
        try
        {
            var g = m switch { Modo.Eficiencia => Eficiente, Modo.Rendimiento => Rapido, _ => Guid.Empty };
            uint r = PowerSetActiveOverlayScheme(g);
            Log.Info($"modo de energía: {m} (resultado {r})");
            return r == 0;
        }
        catch (Exception ex) { Log.Error("cambiar el modo de energía", ex); return false; }
    }
}
