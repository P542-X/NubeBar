using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NubeBar;

public partial class App : Application
{
    // El mismo ejecutable hace dos papeles. Con --guardian no abre ninguna ventana:
    // se queda esperando a que el NubeBar de verdad muera para devolver el shell
    // (ver Guardian.cs). Sin argumentos, crea una isla por monitor y la del monitor
    // principal es la MainWindow, la que manda y la que cierra la aplicación.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Redes de seguridad. Una excepción suelta en un hilo de fondo no la caza
        // nadie y se lleva por delante el proceso entero (le pasó al buscador con
        // System.Data.OleDb). Aquí no se puede evitar el cierre, pero al menos queda
        // escrito en el log qué fue. Las de la interfaz sí se pueden perdonar: más
        // vale una isla con un módulo tonto que quedarte sin barra de tareas.
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            var ex = a.ExceptionObject as Exception;
            Log.Error("excepción sin capturar en un hilo de fondo", ex ?? new Exception(a.ExceptionObject?.ToString() ?? "desconocida"));
            // Nos vamos a pique, pero no con la pantalla pintada de negro
            EscenaArranque.Cerrar("excepción sin capturar");
        };
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Log.Error("tarea que falló y nadie miró", a.Exception);
            a.SetObserved();
        };
        DispatcherUnhandledException += (_, a) =>
        {
            Log.Error("excepción en la interfaz (no se cierra la aplicación)", a.Exception);
            a.Handled = true;
        };

        // "NubeBar.exe --salir": cierra la que esté en marcha y se va. Es como lo
        // quita de en medio el instalador antes de tocar su exe, y sirve igual para
        // un atajo o un script. No abre ninguna ventana.
        if (e.Args.Length > 0 && e.Args[0] == Salir.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Salir.Pedir();
            Shutdown(0);
            return;
        }

        // "NubeBar.exe --pruebas [informe.txt]": corre las comprobaciones de lógica y se
        // va. No abre ninguna ventana, no toca el config y devuelve 1 si algo falla,
        // para poder encadenarlo.
        if (e.Args.Length > 0 && e.Args[0] == Pruebas.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(Pruebas.Correr(e.Args));
            return;
        }

        if (e.Args.Length > 0 && e.Args[0] == Guardian.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Guardian.Vigilar(e.Args);      // bloquea hasta que haya que actuar
            Shutdown(0);
            return;
        }

        // Una sola NubeBar a la vez. El guardián queda fuera de esta comprobación
        // a propósito: es el mismo ejecutable y tiene que poder convivir con ella.
        if (!Instancia.SoyLaPrimera())
        {
            Log.Info("ya hay otra NubeBar abierta: esta se cierra sin tocar nada");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(0);
            return;
        }

        // La escena de entrada va ANTES de montar las islas: lo que tapa es justamente
        // el rato feo del arranque (Explorer recalculando el área de trabajo, la barra
        // de Windows escondiéndose, el carrusel llenándose). Si no toca mostrarla, no
        // crea nada y esto no cuesta nada. Ver EscenaArranque.
        EscenaArranque.Arrancar();

        try
        {
            MainWindow = NubeBar.MainWindow.Arrancar();
        }
        catch
        {
            // Si la nube no llega a montarse, lo último que puede pasar es dejar al
            // usuario con la pantalla negra de la escena delante.
            EscenaArranque.Cerrar("la nube no pudo arrancar");
            throw;
        }

        // Y a la escucha de "NubeBar.exe --salir". Se cierran TODAS las islas, no
        // solo la jefa: con dos pantallas, cerrar la jefa deja viva la otra y el
        // proceso no termina (ShutdownMode es OnLastWindowClose). La jefa va la
        // última porque es la que desmonta lo global y devuelve las barras.
        Instancia.EscucharSalida(() => Dispatcher.BeginInvoke(new Action(() =>
        {
            Log.Info("salida pedida desde fuera (--salir)");
            // La escena primero: es una ventana más y, si se cerrara la última isla
            // con ella abierta, se quedaría sola en pantalla y en negro.
            EscenaArranque.Cerrar("--salir");
            foreach (var isla in NubeBar.MainWindow.Islas.ToList().Where(i => i != MainWindow))
                isla.Close();
            (MainWindow as Window)?.Close();
        })));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Instancia.Soltar();
        base.OnExit(e);
    }
}
