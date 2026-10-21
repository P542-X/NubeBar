using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace NubeBar;

// Las distribuciones de teclado que tienes puestas en Windows y cuál está activa.
//
// El idioma de entrada es de cada hilo, y el que importa es el de la ventana en la
// que escribes, no el nuestro. Para cambiarlo se hacen DOS cosas:
//
// - WM_INPUTLANGCHANGEREQUEST a la ventana en primer plano, lo mismo que hace
//   Windows con Win+Espacio. Con un clic normal la isla no se activa
//   (WS_EX_NOACTIVATE), así que esa ventana es la tuya y cambia en el acto.
// - El gestor de perfiles de TSF con TF_IPPMF_FORSESSION, que lo cambia para toda
//   la sesión. Hace falta para cuando la ventana delante somos NOSOTROS (con la caja
//   de la contraseña abierta, o pulsando desde la accesibilidad): el mensaje solo
//   llegaba al escritorio y el idioma no cambiaba. Comprobado probando la v1.1.29.
//
// Para saber cuál está puesto se mira la ventana de delante si es de otro, y si es
// nuestra, el perfil activo de TSF, que es el de la sesión.
static class Idioma
{
    public sealed record Distribucion(IntPtr Hkl, string Corto, string Nombre);

    [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int n, [Out] IntPtr[]? lista);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint hilo);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

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

    // La ventana de otro que está delante, o Zero si delante no hay nada o somos nosotros
    static IntPtr Ajena()
    {
        var fg = GetForegroundWindow();
        return fg == IntPtr.Zero || Nativo.EsNuestra(fg) ? IntPtr.Zero : fg;
    }

    public static IntPtr Actual()
    {
        try
        {
            var fg = Ajena();
            if (fg != IntPtr.Zero) return GetKeyboardLayout(GetWindowThreadProcessId(fg, out _));
            var kb = GUID_TFCAT_TIP_KEYBOARD;
            if (Perfiles() is { } m && m.GetActiveProfile(ref kb, out var p) == 0 && p.hkl != IntPtr.Zero) return p.hkl;
            return GetKeyboardLayout(0);
        }
        catch { return IntPtr.Zero; }
    }

    public static void Poner(Distribucion d)
    {
        try
        {
            var fg = Ajena();
            if (fg != IntPtr.Zero) PostMessage(fg, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, d.Hkl);

            int hr = -1;
            if (Perfiles() is { } m)
            {
                var nada = Guid.Empty;
                hr = m.ActivateProfile(TF_PROFILETYPE_KEYBOARDLAYOUT, (ushort)(d.Hkl.ToInt64() & 0xFFFF),
                                       ref nada, ref nada, d.Hkl, TF_IPPMF_FORPROCESS | TF_IPPMF_FORSESSION);
            }
            Log.Info($"teclado: {d.Nombre} ({d.Hkl.ToInt64():X8}) · ventana {(fg != IntPtr.Zero ? "avisada" : "nuestra")} · TSF hr={hr:X}");
        }
        catch (Exception ex) { Log.Error("cambiar el teclado", ex); }
    }

    // ---------- TSF ----------
    const uint TF_PROFILETYPE_KEYBOARDLAYOUT = 2;
    const uint TF_IPPMF_FORPROCESS = 0x10000000, TF_IPPMF_FORSESSION = 0x20000000;
    static readonly Guid GUID_TFCAT_TIP_KEYBOARD = new("34745C63-B2F0-4784-8B67-5E12C8701A31");

    // Es COM de apartamento: se crea una vez en el hilo de la interfaz, que es
    // desde donde se llama siempre (el tick y los clics)
    [ThreadStatic] static ITfInputProcessorProfileMgr? _perfiles;
    [ThreadStatic] static bool _sinTsf;

    static ITfInputProcessorProfileMgr? Perfiles()
    {
        if (_perfiles != null || _sinTsf) return _perfiles;
        try
        {
            var tipo = Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED"));
            _perfiles = tipo != null ? Activator.CreateInstance(tipo) as ITfInputProcessorProfileMgr : null;
        }
        catch (Exception ex) { Log.Error("abrir TSF", ex); }
        _sinTsf = _perfiles == null;
        return _perfiles;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TF_INPUTPROCESSORPROFILE
    {
        public uint dwProfileType;
        public ushort langid;
        public Guid clsid, guidProfile, catid;
        public IntPtr hklSubstitute;
        public uint dwCaps;
        public IntPtr hkl;
        public uint dwFlags;
    }

    // Solo se usan ActivateProfile y GetActiveProfile, pero en una interfaz COM el
    // orden de la vtable manda: los del medio tienen que estar aunque no se llamen
    [ComImport, Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITfInputProcessorProfileMgr
    {
        [PreserveSig] int ActivateProfile(uint tipo, ushort langid, ref Guid clsid, ref Guid perfil, IntPtr hkl, uint flags);
        [PreserveSig] int DeactivateProfile(uint tipo, ushort langid, ref Guid clsid, ref Guid perfil, IntPtr hkl, uint flags);
        [PreserveSig] int GetProfile(uint tipo, ushort langid, ref Guid clsid, ref Guid perfil, IntPtr hkl, out TF_INPUTPROCESSORPROFILE p);
        [PreserveSig] int EnumProfiles(ushort langid, out IntPtr e);
        [PreserveSig] int ReleaseInputProcessor(ref Guid clsid, uint flags);
        [PreserveSig] int RegisterProfile(ref Guid a, ushort b, ref Guid c, IntPtr d, uint e, IntPtr f, uint g, IntPtr h, uint i, uint j, IntPtr k, uint l, int m);
        [PreserveSig] int UnregisterProfile(ref Guid a, ushort b, ref Guid c, uint d);
        [PreserveSig] int GetActiveProfile(ref Guid catid, out TF_INPUTPROCESSORPROFILE p);
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
