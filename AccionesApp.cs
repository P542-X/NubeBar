using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NubeBar;

// Lo que se puede hacer con una aplicación de la lista de resultados aparte de
// abrirla: lanzarla como administrador, enseñar dónde vive y abrir sus propiedades.
//
// Todo pasa por el shell de Windows, igual que si hicieras clic derecho en el
// Explorador. En particular, **la elevación la pide Windows**: se usa el verbo "runas",
// que es lo que saca el UAC. Aquí no hay —ni va a haber— ningún atajo para saltárselo:
// si el usuario dice que no, la acción no se hace y se queda escrito en el log.
static class AccionesApp
{
    // ---------- Qué clase de cosa es esto ----------

    // Las apps de la Store no son un fichero suelto: viven en shell:AppsFolder y lo
    // único que se puede hacer con ellas es abrirlas.
    public static bool EsDeLaStore(string? ruta) =>
        ruta != null && ruta.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);

    // Un fichero de verdad, que está donde dice que está
    public static bool EsFichero(string? ruta) =>
        !string.IsNullOrWhiteSpace(ruta) && !EsDeLaStore(ruta) && File.Exists(ruta);

    // Lo que tiene sentido lanzar elevado. Un .txt o una carpeta no: "ejecutar como
    // administrador" un documento no significa nada, y ofrecerlo sería mentir.
    static readonly string[] Ejecutables = { ".exe", ".lnk", ".bat", ".cmd", ".com", ".msi", ".msc", ".ps1" };

    public static bool PuedeElevar(string? ruta)
    {
        if (!EsFichero(ruta)) return false;
        var ext = Path.GetExtension(ruta!);
        foreach (var e in Ejecutables)
            if (ext.Equals(e, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool TieneUbicacion(string? ruta) => EsFichero(ruta);
    public static bool TienePropiedades(string? ruta) => EsFichero(ruta);

    // ---------- Accesos directos ----------
    //
    // ShellExecute con el verbo "runas" sobre un .lnk no eleva: el shell abre el
    // acceso directo y el proceso de destino nace sin elevar, así que el UAC no sale y
    // parece que la opción no hace nada. Hay que resolver el .lnk y elevar su DESTINO.
    //
    // Se resuelve por COM tardío con Shell.Application, que es lo que ya usa el índice
    // de aplicaciones para enumerar la carpeta de la Store: nada de declarar IShellLink
    // ni de añadir una referencia a Windows Script Host solo para esto.
    // Se devuelven los dos de una vez —destino y argumentos— porque hacen el mismo
    // viaje al shell. Los argumentos hay que conservarlos al elevar: sin ellos, un .lnk
    // con parámetros (un "chrome.exe --profile-directory=…", por ejemplo) arrancaría
    // otra cosa distinta de la que pulsaste.
    public static (string? Destino, string? Argumentos) LeerAcceso(string lnk)
    {
        try
        {
            var tipo = Type.GetTypeFromProgID("Shell.Application");
            if (tipo == null) return (null, null);
            dynamic? shell = null;
            try
            {
                shell = Activator.CreateInstance(tipo);
                if (shell == null) return (null, null);

                dynamic? carpeta = shell.NameSpace(Path.GetDirectoryName(lnk) ?? "");
                if (carpeta == null) return (null, null);
                dynamic? item = carpeta!.ParseName(Path.GetFileName(lnk));
                if (item == null || !(bool)item!.IsLink) return (null, null);

                dynamic enlace = item!.GetLink;
                string destino = enlace.Path;
                string args = enlace.Arguments;
                return (string.IsNullOrWhiteSpace(destino) ? null : destino,
                        string.IsNullOrWhiteSpace(args) ? null : args);
            }
            finally { if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); }
        }
        catch (Exception ex)
        {
            Log.Error("resolver el acceso directo " + lnk, ex);
            return (null, null);
        }
    }

    // ---------- Las tres acciones ----------

    // Como administrador, por el camino normal de Windows: el verbo "runas" es el que
    // saca el UAC. Si el usuario lo cancela, Windows devuelve el error 1223 y eso NO es
    // un fallo: es una respuesta, y se apunta como tal.
    public static bool ComoAdministrador(string ruta)
    {
        string destino = ruta;
        string? argumentos = null;

        if (Path.GetExtension(ruta).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var (d, args) = LeerAcceso(ruta);
            if (d != null && File.Exists(d))
            {
                destino = d;
                argumentos = args;
            }
            else
            {
                Log.Info($"no se pudo resolver el acceso directo \"{ruta}\": " +
                         "se eleva el .lnk tal cual y puede que Windows lo abra sin elevar");
            }
        }

        try
        {
            var inicio = new ProcessStartInfo(destino)
            {
                UseShellExecute = true,          // imprescindible: el verbo lo aplica el shell
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(destino) ?? ""
            };
            if (argumentos != null) inicio.Arguments = argumentos;
            Process.Start(inicio)?.Dispose();
            Log.Info($"lanzado como administrador: {destino}" +
                     (argumentos != null ? $" {argumentos}" : ""));
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED: has dicho "No" en el UAC. Ni se reintenta ni se busca
            // otra forma de elevar: no hacerlo es la respuesta correcta.
            Log.Info($"elevación cancelada en el UAC: {destino}");
            return false;
        }
        catch (Exception ex) { Log.Error("ejecutar como administrador " + destino, ex); return false; }
    }

    // El Explorador abierto en su carpeta y con el fichero ya seleccionado
    public static void AbrirUbicacion(string ruta)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ruta}\"")
            { UseShellExecute = true })?.Dispose();
            Log.Info($"ubicación abierta: {ruta}");
        }
        catch (Exception ex) { Log.Error("abrir la ubicación de " + ruta, ex); }
    }

    // ---------- Propiedades ----------
    //
    // Esta no se puede hacer con Process.Start. El verbo "properties" necesita que el
    // shell tenga la LISTA DE IDs del elemento (SEE_MASK_INVOKEIDLIST): sin ella,
    // ShellExecute no encuentra el verbo y no pasa nada. Por eso hay que bajar a
    // ShellExecuteEx.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ShellExecuteExW(ref SHELLEXECUTEINFO info);

    const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    const int SW_SHOW = 5;

    public static void Propiedades(string ruta)
    {
        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            fMask = SEE_MASK_INVOKEIDLIST,
            // Sin dueño a propósito: la nube vive como WS_EX_NOACTIVATE y un diálogo
            // colgado de una ventana que no se activa puede quedarse detrás de todo.
            hwnd = IntPtr.Zero,
            lpVerb = "properties",
            lpFile = ruta,
            lpDirectory = Path.GetDirectoryName(ruta),
            nShow = SW_SHOW
        };
        try
        {
            if (ShellExecuteExW(ref info)) { Log.Info($"propiedades abiertas: {ruta}"); return; }
            Log.Info($"el shell no ha abierto las propiedades de \"{ruta}\" " +
                     $"(error {Marshal.GetLastWin32Error()})");
        }
        catch (Exception ex) { Log.Error("abrir las propiedades de " + ruta, ex); }
    }
}
