using System;
using System.Collections.ObjectModel;
using System.Linq;
using ManagedShell;
using ManagedShell.WindowsTray;

namespace NubeBar;

// La bandeja del sistema (Discord, Steam, OneDrive, el antivirus...).
//
// Al esconder la barra de tareas de Windows esos iconos se vuelven inalcanzables,
// así que NubeBar tiene que recibirlos ella. Eso no se puede hacer con un par de
// llamadas: hay que registrarse como la bandeja del shell, recibir los mensajes
// NOTIFYICONDATA de cada app y reenviarles los clics. Lo hace ManagedShell (MIT,
// la librería de Cairo Shell, la misma que usa RetroBar).
//
// Es un servicio de todo el proceso, no de cada isla: hay una sola bandeja aunque
// haya dos pantallas, igual que en Windows.
static class Bandeja
{
    static ShellManager? _shell;
    static System.Threading.Timer? _avisoVacia;

    public static bool Encendida => _shell != null;

    // Si la librería no consigue registrarse (otra barra de terceros ya lo hizo,
    // una directiva de grupo, lo que sea) esto queda en true y el módulo no se pinta
    public static bool Fallo { get; private set; }

    public static ObservableCollection<NotifyIcon>? Iconos => _shell?.NotificationArea?.TrayIcons;

    public static event Action? Cambio;

    // ---------- Los que siguen vivos ----------
    //
    // ---------- El fallo que esto arregla ----------
    //
    // Al reiniciar Explorer, las apps vuelven a registrar su icono —eso es lo que
    // garantiza el mensaje "TaskbarCreated"— pero lo hacen con una VENTANA NUEVA. El
    // registro viejo se queda en la colección, porque nadie ha mandado el NIM_DELETE
    // correspondiente: la ventana que lo habría mandado ya no existe.
    //
    // Resultado medido con Explorer reiniciado a mitad de sesión:
    //
    //   añadidos=1 → Wallpaper Engine [hwnd:91689058/uid:1000]
    //   ...
    //     · Wallpaper Engine [hwnd:91557986/uid:1000]   ← el viejo, con la ventana muerta
    //     · Wallpaper Engine [hwnd:91689058/uid:1000]   ← el nuevo
    //
    // Y comprobando esas dos ventanas: `IsWindow(91557986) = False`. O sea, la bandeja
    // enseñaba Wallpaper Engine dos veces, y una de las dos no respondía a los clics.
    //
    // ---------- Por qué se filtra y no se borra ----------
    //
    // La colección es de ManagedShell y la mantiene él con los mensajes de las apps.
    // Quitarle entradas por detrás es meterle la mano a su estado; filtrar al mirar es
    // barato (una llamada a `IsWindow` por icono), no pelea con la librería y da el mismo
    // resultado a la vista.
    //
    // Y se filtra AQUÍ, en un solo sitio, para que la tira, la cuenta del "^" y la
    // rejilla de los ocultos no puedan discrepar: con la cuenta sin filtrar el "^" decía
    // "otros 3" y en la rejilla salían 2.
    public static System.Collections.Generic.List<NotifyIcon> Vivos()
    {
        var lista = new System.Collections.Generic.List<NotifyIcon>();
        try
        {
            foreach (var i in Iconos ?? new ObservableCollection<NotifyIcon>())
                if (EstaVivo(i)) lista.Add(i);
        }
        catch (Exception ex) { Log.Error("filtrar los iconos vivos de la bandeja", ex); }
        return lista;
    }

    // Un icono está vivo si la ventana que lo registró sigue existiendo. Los que se
    // registran con GUID y sin ventana (hay alguno) se dan por vivos: no hay nada que
    // comprobar y descartarlos sería esconder un icono bueno.
    public static bool EstaVivo(NotifyIcon i)
    {
        try { return i.HWnd == IntPtr.Zero || Nativo.Existe(i.HWnd); }
        catch { return true; }
    }

    // Cuántos de la colección son fantasmas. Solo para el log y el diagnóstico.
    public static int Fantasmas()
    {
        try { return (Iconos?.Count ?? 0) - Vivos().Count; }
        catch { return 0; }
    }

    public static void Encender()
    {
        if (_shell != null) return;
        try
        {
            // Montaje calcado al de RetroBar, que es el que funciona en Windows 11 con
            // Explorer vivo. Lo que hace la magia está dentro de NotificationArea.
            // Initialize(): crea una ventana de clase Shell_TrayWnd, manda la de
            // Explorer al FONDO del orden Z, pone la nuestra arriba del todo y repite
            // esa comprobación cada 100 ms. Como FindWindow devuelve la primera en
            // orden Z, los Shell_NotifyIcon de las apps acaban llegándonos a nosotros.
            // Después difunde "TaskbarCreated" para que todas vuelvan a registrarse.
            ManagedShell.Common.Helpers.EnvironmentHelper.IsAppRunningAsShell =
                Nativo.VentanaDelShell() == IntPtr.Zero;

            var cfg = ShellManager.DefaultShellConfig;
            cfg.EnableTasksService = false;       // la lista de ventanas ya la llevamos nosotros
            cfg.AutoStartTasksService = false;
            _shell = new ShellManager(cfg);       // con AutoStartTrayService, ya llama a Initialize()

            if (_shell.NotificationArea == null || _shell.NotificationArea.IsFailed)
            {
                Fallo = true;
                Log.Info("bandeja: ManagedShell no pudo registrarse como bandeja del shell");
                Apagar();
                return;
            }

            _shell.NotificationArea.TrayIcons.CollectionChanged += (_, _) => Cambio?.Invoke();
            Fallo = false;
            Cambio?.Invoke();           // los módulos ya montados se enteran y se rellenan
            Log.Info($"bandeja encendida: {_shell.NotificationArea.TrayIcons.Count} iconos de salida");

            // Aviso honesto: si en 8 segundos no ha llegado ni un icono es que las apps
            // siguen hablando con la bandeja de Explorer y este módulo no sirve de nada.
            // TrayIcons es una colección de WPF: solo se puede mirar desde el hilo de
            // la interfaz, así que el temporizador salta allí antes de contar.
            var interfaz = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            _avisoVacia = new System.Threading.Timer(_ => interfaz.BeginInvoke(new Action(() =>
            {
                int cuantos = _shell?.NotificationArea?.TrayIcons.Count ?? 0;
                var nuestra = _shell?.NotificationArea?.Handle ?? IntPtr.Zero;
                var primera = Nativo.PrimeraBandeja();
                Log.Info($"bandeja a los 8 s: {cuantos} iconos | nuestra ventana={nuestra} | " +
                         $"la que encuentra Windows={primera} | ¿ganamos?={nuestra == primera}");
                foreach (var linea in Ficha()) Log.Info("  bandeja: " + linea);
            })), null, 8000, System.Threading.Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Fallo = true;
            Log.Error("encender la bandeja", ex);
            _shell = null;
        }
    }

    public static void Apagar()
    {
        if (_shell == null) return;
        _avisoVacia?.Dispose();
        _avisoVacia = null;
        try
        {
            // Al soltarse, ManagedShell vuelve a difundir "TaskbarCreated" y las apps
            // registran sus iconos otra vez con la bandeja de Explorer: así no te
            // quedas sin iconos en la barra de Windows cuando NubeBar se cierra.
            _shell.Dispose();
            Log.Info("bandeja apagada y devuelta a Explorer");
        }
        catch (Exception ex) { Log.Error("apagar la bandeja", ex); }
        _shell = null;
    }

    // Lo que el icono necesita saber para colocar su menú: dónde está en pantalla
    public static void FijarSitio(NotifyIcon icono, double x, double y, double ancho, double alto)
    {
        try
        {
            icono.Placement = new ManagedShell.Interop.NativeMethods.Rect
                { Left = (int)x, Top = (int)y, Right = (int)(x + ancho), Bottom = (int)(y + alto) };
        }
        catch { }
    }

    // Una línea por icono con lo que hace falta para entender por qué uno no
    // responde a los clics: de qué proceso es, si podemos alcanzarlo y con qué
    // versión del protocolo de bandeja se registró.
    public static System.Collections.Generic.List<string> Ficha()
    {
        var filas = new System.Collections.Generic.List<string>();
        try
        {
            foreach (var i in _shell?.NotificationArea?.TrayIcons ?? new ObservableCollection<NotifyIcon>())
            {
                int pid = Nativo.PidDe(i.HWnd);
                bool alcanzable = pid != 0 && Nativo.ProcesoAlcanzable(pid);
                string nombre = (i.Title ?? "").Split('\n')[0];
                if (nombre.Length == 0) nombre = "(sin nombre)";
                filas.Add($"{nombre} · pid {pid} · {(alcanzable ? "alcanzable" : "INALCANZABLE (admin o SYSTEM)")}" +
                          $" · v{i.Version} · cb=0x{i.CallbackMessage:X} · {System.IO.Path.GetFileName(i.Path ?? "")}" +
                          $" · guid={i.GUID}");
            }
        }
        catch (Exception ex) { Log.Error("fichar los iconos de bandeja", ex); }
        return filas;
    }

    public static string Resumen() =>
        _shell?.NotificationArea == null
            ? "apagada"
            : string.Join(", ", _shell.NotificationArea.TrayIcons.Select(i => i.Title?.Split('\n')[0] ?? "?"));
}
