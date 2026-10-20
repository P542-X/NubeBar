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

        MainWindow = NubeBar.MainWindow.Arrancar();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Instancia.Soltar();
        base.OnExit(e);
    }
}
