using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NubeBar;

// Core Audio: volumen general, volumen por aplicación y lista de salidas.
//
// Todo esto es COM sin envoltorio en .NET, así que hay que declarar las interfaces
// a mano con sus Guid y respetar el orden exacto de los métodos en la vtable: si
// una firma se sale de sitio, la llamada se va a otro método y revienta.
public static class Audio
{
    // ---------- Fontanería COM ----------
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class EnumeradorDispositivos { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int flujo, int estado, out IMMDeviceCollection dispositivos);
        int GetDefaultAudioEndpoint(int flujo, int papel, out IMMDevice dispositivo);
        int GetDevice(string id, out IMMDevice dispositivo);
        int RegisterEndpointNotificationCallback(IntPtr cliente);
        int UnregisterEndpointNotificationCallback(IntPtr cliente);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        int GetCount(out int cuantos);
        int Item(int indice, out IMMDevice dispositivo);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int contexto, IntPtr parametros, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        int OpenPropertyStore(int acceso, out IPropertyStore propiedades);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int estado);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        int GetCount(out int cuantas);
        int GetAt(int indice, out PROPERTYKEY clave);
        int GetValue(ref PROPERTYKEY clave, out PROPVARIANT valor);
        int SetValue(ref PROPERTYKEY clave, ref PROPVARIANT valor);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public int pid; }

    [StructLayout(LayoutKind.Explicit)]
    struct PROPVARIANT
    {
        [FieldOffset(0)] public short vt;
        [FieldOffset(8)] public IntPtr punteroValor;
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr cb);
        int UnregisterControlChangeNotify(IntPtr cb);
        int GetChannelCount(out int canales);
        int SetMasterVolumeLevel(float nivelDb, ref Guid contexto);
        int SetMasterVolumeLevelScalar(float nivel, ref Guid contexto);
        int GetMasterVolumeLevel(out float nivelDb);
        int GetMasterVolumeLevelScalar(out float nivel);
        int SetChannelVolumeLevel(int canal, float db, ref Guid contexto);
        int SetChannelVolumeLevelScalar(int canal, float nivel, ref Guid contexto);
        int GetChannelVolumeLevel(int canal, out float db);
        int GetChannelVolumeLevelScalar(int canal, out float nivel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool silencio, ref Guid contexto);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool silencio);
        int GetVolumeStepInfo(out int paso, out int pasos);
        int VolumeStepUp(ref Guid contexto);
        int VolumeStepDown(ref Guid contexto);
        int QueryHardwareSupport(out int soporte);
        int GetVolumeRange(out float minDb, out float maxDb, out float incrementoDb);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        int GetAudioSessionControl(IntPtr idSesion, int flujo, out IntPtr sesion);
        int GetSimpleAudioVolume(IntPtr idSesion, int transversal, out IntPtr volumen);
        int GetSessionEnumerator(out IAudioSessionEnumerator enumerador);
        int RegisterSessionNotification(IntPtr cb);
        int UnregisterSessionNotification(IntPtr cb);
        int RegisterDuckNotification(string id, IntPtr cb);
        int UnregisterDuckNotification(IntPtr cb);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        int GetCount(out int cuantas);
        int GetSession(int indice, out IAudioSessionControl sesion);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl
    {
        int GetState(out int estado);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string nombre);
        int SetDisplayName(string nombre, ref Guid contexto);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string ruta);
        int SetIconPath(string ruta, ref Guid contexto);
        int GetGroupingParam(out Guid grupo);
        int SetGroupingParam(ref Guid grupo, ref Guid contexto);
        int RegisterAudioSessionNotification(IntPtr cb);
        int UnregisterAudioSessionNotification(IntPtr cb);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // Hereda de IAudioSessionControl: hay que repetir sus métodos en orden
        int GetState(out int estado);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string nombre);
        int SetDisplayName(string nombre, ref Guid contexto);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string ruta);
        int SetIconPath(string ruta, ref Guid contexto);
        int GetGroupingParam(out Guid grupo);
        int SetGroupingParam(ref Guid grupo, ref Guid contexto);
        int RegisterAudioSessionNotification(IntPtr cb);
        int UnregisterAudioSessionNotification(IntPtr cb);
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetProcessId(out int pid);
        int IsSystemSoundsSession();
        int SetDuckingPreference(bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        int SetMasterVolume(float nivel, ref Guid contexto);
        int GetMasterVolume(out float nivel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool silencio, ref Guid contexto);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool silencio);
    }

    const int eRender = 0, eConsole = 0, DEVICE_STATE_ACTIVE = 1, CLSCTX_ALL = 23;
    static Guid _sinContexto = Guid.Empty;

    // PKEY_Device_FriendlyName
    static PROPERTYKEY ClaveNombre => new()
    { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };

    static IMMDeviceEnumerator NuevoEnumerador() =>
        (IMMDeviceEnumerator)new EnumeradorDispositivos();

    // ---------- Volumen general ----------
    static IAudioEndpointVolume? VolumenDelDefecto()
    {
        var enumerador = NuevoEnumerador();
        if (enumerador.GetDefaultAudioEndpoint(eRender, eConsole, out var disp) != 0 || disp == null) return null;
        var iid = typeof(IAudioEndpointVolume).GUID;
        if (disp.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object o) != 0) return null;
        return o as IAudioEndpointVolume;
    }

    public static int Volumen()
    {
        try
        {
            if (VolumenDelDefecto() is not { } v) return -1;
            v.GetMasterVolumeLevelScalar(out float nivel);
            return (int)Math.Round(nivel * 100);
        }
        catch (Exception ex) { Log.Error("leer el volumen", ex); return -1; }
    }

    public static void PonerVolumen(int porCiento)
    {
        try
        {
            if (VolumenDelDefecto() is not { } v) return;
            v.SetMasterVolumeLevelScalar(Math.Clamp(porCiento, 0, 100) / 100f, ref _sinContexto);
        }
        catch (Exception ex) { Log.Error("poner el volumen", ex); }
    }

    public static bool Silenciado
    {
        get
        {
            try
            {
                if (VolumenDelDefecto() is not { } v) return false;
                v.GetMute(out bool m);
                return m;
            }
            catch { return false; }
        }
    }

    public static void Silenciar(bool si)
    {
        try { VolumenDelDefecto()?.SetMute(si, ref _sinContexto); }
        catch (Exception ex) { Log.Error("silenciar", ex); }
    }

    // ---------- Salidas disponibles ----------
    public sealed record Salida(string Id, string Nombre, bool EsLaActual);

    public static List<Salida> Salidas()
    {
        var lista = new List<Salida>();
        try
        {
            var enumerador = NuevoEnumerador();
            string actual = "";
            if (enumerador.GetDefaultAudioEndpoint(eRender, eConsole, out var def) == 0 && def != null)
                def.GetId(out actual);

            if (enumerador.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out var col) != 0 || col == null) return lista;
            col.GetCount(out int n);
            for (int i = 0; i < n; i++)
            {
                if (col.Item(i, out var d) != 0 || d == null) continue;
                d.GetId(out string id);
                lista.Add(new Salida(id, NombreDe(d) ?? id, id == actual));
            }
        }
        catch (Exception ex) { Log.Error("listar salidas de audio", ex); }
        return lista;
    }

    static string? NombreDe(IMMDevice d)
    {
        try
        {
            if (d.OpenPropertyStore(0, out var props) != 0 || props == null) return null;
            var clave = ClaveNombre;
            if (props.GetValue(ref clave, out var v) != 0) return null;
            return v.punteroValor == IntPtr.Zero ? null : Marshal.PtrToStringUni(v.punteroValor);
        }
        catch { return null; }
    }

    // ---------- Volumen por aplicación ----------
    public sealed record Sesion(int Pid, string Nombre, int Volumen, bool Silenciada, object Control);

    public static List<Sesion> Sesiones()
    {
        var lista = new List<Sesion>();
        try
        {
            var enumerador = NuevoEnumerador();
            if (enumerador.GetDefaultAudioEndpoint(eRender, eConsole, out var disp) != 0 || disp == null) return lista;

            var iid = typeof(IAudioSessionManager2).GUID;
            if (disp.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object o) != 0 || o is not IAudioSessionManager2 gestor)
                return lista;

            if (gestor.GetSessionEnumerator(out var ses) != 0 || ses == null) return lista;
            ses.GetCount(out int n);

            for (int i = 0; i < n; i++)
            {
                if (ses.GetSession(i, out var ctrl) != 0 || ctrl == null) continue;
                if (ctrl is not IAudioSessionControl2 c2) continue;

                c2.GetState(out int estado);
                if (estado == 2) continue;                      // AudioSessionStateExpired

                // No se usa IsSystemSoundsSession: al no llevar parámetros de salida,
                // el marshaller de COM no deja ver su HRESULT y siempre contesta 0, o
                // sea "sí lo es", y se filtraban TODAS las sesiones. El pid distingue
                // igual de bien: los sonidos del sistema son el pid 0.
                c2.GetProcessId(out int pid);

                if (ctrl is not ISimpleAudioVolume vol) continue;
                vol.GetMasterVolume(out float nivel);
                vol.GetMute(out bool mute);
                lista.Add(new Sesion(pid, NombreDeProceso(pid), (int)Math.Round(nivel * 100), mute, vol));
            }
        }
        catch (Exception ex) { Log.Error("listar sesiones de audio", ex); }
        return lista;
    }

    public static void PonerVolumenSesion(Sesion s, int porCiento)
    {
        try
        {
            if (s.Control is ISimpleAudioVolume v)
                v.SetMasterVolume(Math.Clamp(porCiento, 0, 100) / 100f, ref _sinContexto);
        }
        catch (Exception ex) { Log.Error("poner volumen de una app", ex); }
    }

    public static void SilenciarSesion(Sesion s, bool si)
    {
        try { if (s.Control is ISimpleAudioVolume v) v.SetMute(si, ref _sinContexto); }
        catch (Exception ex) { Log.Error("silenciar una app", ex); }
    }

    static string NombreDeProceso(int pid)
    {
        if (pid == 0) return "Sonidos del sistema";
        try { return System.Diagnostics.Process.GetProcessById(pid).ProcessName; }
        catch { return $"pid {pid}"; }
    }

    // ---------- Cambiar la salida por defecto ----------
    // IPolicyConfig no está documentada por Microsoft y su CLSID cambia entre
    // versiones de Windows. Si no responde, la opción simplemente no se ofrece.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] class ConfigDirectiva { }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        int GetMixFormat(string id, out IntPtr formato);
        int GetDeviceFormat(string id, bool predeterminado, out IntPtr formato);
        int ResetDeviceFormat(string id);
        int SetDeviceFormat(string id, IntPtr fin, IntPtr mix);
        int GetProcessingPeriod(string id, bool predeterminado, out long p1, out long p2);
        int SetProcessingPeriod(string id, ref long periodo);
        int GetShareMode(string id, out IntPtr modo);
        int SetShareMode(string id, IntPtr modo);
        int GetPropertyValue(string id, ref PROPERTYKEY clave, out PROPVARIANT valor);
        int SetPropertyValue(string id, ref PROPERTYKEY clave, ref PROPVARIANT valor);
        int SetDefaultEndpoint(string id, int papel);
        int SetEndpointVisibility(string id, bool visible);
    }

    static bool? _puedeCambiarSalida;

    // Se comprueba una sola vez y sin cambiar nada: solo que la interfaz exista
    public static bool PuedeCambiarSalida
    {
        get
        {
            if (_puedeCambiarSalida is { } ya) return ya;
            try
            {
                var c = new ConfigDirectiva();
                bool ok = c is IPolicyConfig;
                Marshal.ReleaseComObject(c);
                _puedeCambiarSalida = ok;
            }
            catch (Exception ex)
            {
                Log.Info($"cambiar la salida de audio no está disponible en este Windows: {ex.GetType().Name}");
                _puedeCambiarSalida = false;
            }
            return _puedeCambiarSalida.Value;
        }
    }

    public static bool CambiarSalida(string idDispositivo)
    {
        if (!PuedeCambiarSalida) return false;
        try
        {
            var cfg = (IPolicyConfig)new ConfigDirectiva();
            for (int papel = 0; papel <= 2; papel++) cfg.SetDefaultEndpoint(idDispositivo, papel);
            Log.Info($"salida de audio cambiada a {idDispositivo}");
            return true;
        }
        catch (Exception ex) { Log.Error("cambiar la salida de audio", ex); return false; }
    }
}
