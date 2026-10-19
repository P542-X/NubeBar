using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NubeBar;

// Silenciar el audio del sistema. Se usa Core Audio y no la tecla de volumen
// porque así además se puede LEER si está silenciado y pintar el estado.
public static class Audio
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class EnumeradorDispositivos { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int flujo, int estado, out IntPtr coleccion);
        int GetDefaultAudioEndpoint(int flujo, int rol, out IMMDevice dispositivo);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int contexto, IntPtr parametros,
                     [MarshalAs(UnmanagedType.IUnknown)] out object interfaz);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegistrarNotificacion(IntPtr n);
        int QuitarNotificacion(IntPtr n);
        int GetChannelCount(out uint n);
        int SetMasterVolumeLevel(float nivel, ref Guid evento);
        int SetMasterVolumeLevelScalar(float nivel, ref Guid evento);
        int GetMasterVolumeLevel(out float nivel);
        int GetMasterVolumeLevelScalar(out float nivel);
        int SetChannelVolumeLevel(uint canal, float nivel, ref Guid evento);
        int SetChannelVolumeLevelScalar(uint canal, float nivel, ref Guid evento);
        int GetChannelVolumeLevel(uint canal, out float nivel);
        int GetChannelVolumeLevelScalar(uint canal, out float nivel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool silencio, ref Guid evento);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool silencio);
    }

    static IAudioEndpointVolume? Volumen()
    {
        try
        {
            var enumerador = (IMMDeviceEnumerator)new EnumeradorDispositivos();
            if (enumerador.GetDefaultAudioEndpoint(0, 1, out var disp) != 0) return null;   // eRender, eMultimedia
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (disp.Activate(ref iid, 1, IntPtr.Zero, out object obj) != 0) return null;   // CLSCTX_INPROC_SERVER
            return obj as IAudioEndpointVolume;
        }
        catch { return null; }
    }

    public static bool Silenciado
    {
        get
        {
            try { if (Volumen() is { } v && v.GetMute(out bool m) == 0) return m; }
            catch { }
            return false;
        }
    }

    public static void Silenciar(bool silencio)
    {
        try
        {
            var vacio = Guid.Empty;
            Volumen()?.SetMute(silencio, ref vacio);
        }
        catch (Exception ex) { Log.Error("silenciar", ex); }
    }
}

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
            try { return SHQueryUserNotificationState(out int e) == 0 && e is 2 or 3 or 4 or 5; }
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
