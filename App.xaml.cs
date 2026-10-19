using System.Windows;

namespace NubeBar;

public partial class App : Application
{
    // Ya no hay una sola ventana: se crea una isla por monitor y la del monitor
    // principal es la MainWindow, la que manda y la que cierra la aplicación.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow = NubeBar.MainWindow.Arrancar();
    }
}
