using System;
using System.Linq;
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

        if (e.Args.Length > 0 && e.Args[0] == Guardian.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Guardian.Vigilar(e.Args);      // bloquea hasta que haya que actuar
            Shutdown(0);
            return;
        }

        MainWindow = NubeBar.MainWindow.Arrancar();
    }
}
