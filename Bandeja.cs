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

    public static string Resumen() =>
        _shell?.NotificationArea == null
            ? "apagada"
            : string.Join(", ", _shell.NotificationArea.TrayIcons.Select(i => i.Title?.Split('\n')[0] ?? "?"));
}
