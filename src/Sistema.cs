using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NubeBar;


// "No molestar" (Asistente de concentración). Windows deja LEER el estado, pero no
// hay API pública para cambiarlo: al pulsar se abre su página de Configuración.
public static class NoMolestar
{
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int estado);

    // 1 = pantalla no disponible, 2 = app a pantalla completa, 3 = presentando,
    // 4 = modo silencioso, 5 = correo, 6 = aceptando notificaciones
    public static bool Activo
    {
        get
        {
            // OJO con el 5: es QUNS_ACCEPTS_NOTIFICATIONS, o sea justo lo contrario.
            // Estaba en la lista por error y el botón salía encendido siempre.
            // Molestan: 2 (app a pantalla completa), 3 (juego en D3D), 4 (presentando)
            // y 6 (horas de silencio).
            try { return SHQueryUserNotificationState(out int e) == 0 && e is 2 or 3 or 4 or 6; }
            catch { return false; }
        }
    }

    public static bool SePuedeCambiar => false;

    public static void AbrirAjustes()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:quiethours") { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { Log.Error("abrir no molestar", ex); }
    }
}

// Bloquear, suspender, reiniciar y apagar.
// Con NUBEBAR_SIMULAR_ENERGIA=1 no se ejecuta nada: solo se apunta en el log.
// Es lo que permite probar los botones sin apagarle el ordenador a nadie.
public static class Energia
{
    [DllImport("user32.dll")] static extern bool LockWorkStation();
    [DllImport("powrprof.dll", SetLastError = true)]
    static extern bool SetSuspendState(bool hibernar, bool forzar, bool desactivarEventos);

    public static bool Simulando =>
        Environment.GetEnvironmentVariable("NUBEBAR_SIMULAR_ENERGIA") == "1";

    public static void Bloquear() => Hacer("bloquear", () => LockWorkStation());
    public static void Suspender() => Hacer("suspender", () => SetSuspendState(false, false, false));
    public static void Reiniciar() => Hacer("reiniciar", () => Ejecutar("shutdown.exe", "/r /t 0"));
    public static void Apagar() => Hacer("apagar", () => Ejecutar("shutdown.exe", "/s /t 0"));

    static void Hacer(string que, Action accion)
    {
        if (Simulando)
        {
            Log.Info($"SIMULADO energía: se habría llamado a «{que}»");
            return;
        }
        Log.Info("energía: " + que);
        try { accion(); }
        catch (Exception ex) { Log.Error("energía " + que, ex); }
    }

    static void Ejecutar(string exe, string argumentos)
    {
        Process.Start(new ProcessStartInfo(exe, argumentos)
        { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
    }
}
