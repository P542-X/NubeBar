using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace NubeBar;

// Las distribuciones de teclado que tienes puestas en Windows y cuál está activa.
//
// El idioma de entrada es de cada hilo, y el que importa es el de la ventana en la
// que escribes, no el nuestro: la isla nunca se activa (WS_EX_NOACTIVATE), así que
// la ventana en primer plano sigue siendo la tuya. Se le pregunta a ella, y para
// cambiarlo se le manda WM_INPUTLANGCHANGEREQUEST, que es lo mismo que hace Windows
// con Win+Espacio. Con la opción por defecto de Windows 8 en adelante ("usar la
// misma para todas las apps") el cambio vale para todas.
static class Idioma
{
    public sealed record Distribucion(IntPtr Hkl, string Corto, string Nombre);

    [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int n, [Out] IntPtr[]? lista);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint hilo);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();

    const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    // El HKL lleva el idioma en la palabra baja y la distribución física en la alta
    // (0409 = EE. UU., 040A = español...). Con dos entradas del mismo idioma, como
    // "inglés con teclado español", el idioma solo no basta para distinguirlas.
    public static List<Distribucion> Lista()
    {
        var lista = new List<Distribucion>();
        try
        {
            int n = GetKeyboardLayoutList(0, null);
            if (n <= 0) return lista;
            var hkls = new IntPtr[n];
            n = GetKeyboardLayoutList(n, hkls);

            var crudas = hkls.Take(n).Select(h =>
            {
                long v = h.ToInt64();
                var idioma = Cultura((int)(v & 0xFFFF));
                int fisica = (int)((v >> 16) & 0xFFFF);
                // 0xFxxx son distribuciones "especiales" (Dvorak, variantes): no son una cultura
                var cultFisica = (fisica & 0xF000) == 0xF000 ? null : Cultura(fisica);
                return (h, idioma, cultFisica);
            }).ToList();

            foreach (var (h, idioma, fisica) in crudas)
            {
                string lengua = idioma?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "??";
                bool repetida = crudas.Count(c => c.idioma?.LCID == idioma?.LCID) > 1;
                string region = fisica != null ? Region(fisica) : "";
                string corto = repetida && region.Length > 0 ? $"{lengua}·{region}" : lengua;
                string nombre = idioma != null ? Mayuscula(idioma.NativeName) : "Desconocido";
                // "Inglés con teclado español" sí se dice; "español con teclado español
                // tradicional" (0C0A con 040A) es lo mismo para quien escribe, y no
                string lenguaFisica = fisica?.TwoLetterISOLanguageName ?? "";
                if (fisica != null && lenguaFisica != idioma?.TwoLetterISOLanguageName)
                    nombre += $" · teclado {CultureInfo.GetCultureInfo(lenguaFisica).NativeName}";
                lista.Add(new Distribucion(h, corto, nombre));
            }
        }
        catch (Exception ex) { Log.Error("listar distribuciones de teclado", ex); }
        return lista;
    }

    static CultureInfo? Cultura(int lcid)
    {
        try { return CultureInfo.GetCultureInfo(lcid); } catch { return null; }
    }

    static string Region(CultureInfo c)
    {
        try { return new RegionInfo(c.Name).TwoLetterISORegionName; } catch { return ""; }
    }

    static string Mayuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    static IntPtr Destino()
    {
        var fg = GetForegroundWindow();
        return fg == IntPtr.Zero || Nativo.EsNuestra(fg) ? GetShellWindow() : fg;
    }

    public static IntPtr Actual()
    {
        try { return GetKeyboardLayout(GetWindowThreadProcessId(Destino(), out _)); }
        catch { return IntPtr.Zero; }
    }

    public static void Poner(Distribucion d)
    {
        try
        {
            PostMessage(Destino(), WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, d.Hkl);
            Log.Info($"teclado: {d.Nombre} ({d.Hkl.ToInt64():X8})");
        }
        catch (Exception ex) { Log.Error("cambiar el teclado", ex); }
    }

    // La siguiente de la lista, como Win+Espacio. "atras" para la rueda hacia arriba.
    public static void Siguiente(bool atras = false)
    {
        var lista = Lista();
        if (lista.Count < 2) return;
        int i = lista.FindIndex(d => d.Hkl == Actual());
        int j = ((i < 0 ? 0 : i) + (atras ? -1 : 1) + lista.Count) % lista.Count;
        Poner(lista[j]);
    }
}
