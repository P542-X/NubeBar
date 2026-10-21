using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace NubeBar;

// Estado de la red y wifis, con la API nativa de Windows (wlanapi): listar, escanear,
// conectarse (con el perfil guardado o creando uno con la contraseña) y desconectarse.
static class Red
{
    // Cómo se entra en una red. "Empresa" es 802.1X (usuario y contraseña de la
    // universidad o del trabajo): eso no se monta aquí, se manda a Windows.
    public enum Seguridad { Abierta, Wpa, Wpa2, Wpa3, Empresa, Otra }

    public sealed record Wifi(string Nombre, int Senal, bool Conectada, bool Segura,
                              Guid Interfaz, string Perfil, byte[] Ssid, Seguridad Tipo, bool Tkip);

    public enum Tipo { Sin, Cable, Wifi }

    // Las interfaces que de verdad sacan a internet. Las virtuales (Hyper-V, WSL,
    // VirtualBox) también están "Up" y son Ethernet, pero no tienen puerta de
    // enlace: sin este filtro el icono decía "cable" estando por wifi.
    static List<NetworkInterface> Vivas()
    {
        var vivas = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();
        var conSalida = vivas.Where(n => n.GetIPProperties().GatewayAddresses
                                          .Any(g => !g.Address.Equals(System.Net.IPAddress.Any))).ToList();
        return conSalida.Count > 0 ? conSalida : vivas;
    }

    public static Tipo Estado()
    {
        try
        {
            var vivas = Vivas();
            if (vivas.Count == 0) return Tipo.Sin;
            if (vivas.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                               n.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)) return Tipo.Cable;
            if (vivas.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)) return Tipo.Wifi;
            return Tipo.Cable;
        }
        catch { return Tipo.Sin; }
    }

    // ---------- Lo que hay conectado ahora ----------
    public sealed record Conexion(string Nombre, Tipo Tipo, string Adaptador, string Ip,
                                  string PuertaEnlace, long BitsPorSegundo, int Senal);

    public static List<Conexion> Conexiones()
    {
        var lista = new List<Conexion>();
        try
        {
            var wifis = Wifis().Where(w => w.Conectada).ToList();
            foreach (var n in Vivas())
            {
                var ip = n.GetIPProperties();
                string v4 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                              ?.Address.ToString() ?? "";
                string gw = ip.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                              ?.Address.ToString() ?? "";
                bool esWifi = n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                var wifi = esWifi ? wifis.FirstOrDefault() : null;
                lista.Add(new Conexion(
                    wifi?.Nombre ?? n.Name,
                    esWifi ? Tipo.Wifi : Tipo.Cable,
                    n.Description, v4, gw, n.Speed, wifi?.Senal ?? -1));
            }
        }
        catch (Exception ex) { Log.Error("leer las conexiones", ex); }
        return lista;
    }

    // ---------- wlanapi ----------
    [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint version, IntPtr res, out uint negociada, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr res);
    [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr res, out IntPtr lista);
    [DllImport("wlanapi.dll")] static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid iface, uint flags, IntPtr res, out IntPtr lista);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);
    [DllImport("wlanapi.dll")] static extern int WlanScan(IntPtr handle, ref Guid iface, IntPtr ssid, IntPtr ie, IntPtr res);
    [DllImport("wlanapi.dll")] static extern int WlanConnect(IntPtr handle, ref Guid iface, ref WLAN_CONNECTION_PARAMETERS p, IntPtr res);
    [DllImport("wlanapi.dll")] static extern int WlanDisconnect(IntPtr handle, ref Guid iface, IntPtr res);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    static extern int WlanSetProfile(IntPtr handle, ref Guid iface, uint flags, string xml, string? seguridad,
                                     [MarshalAs(UnmanagedType.Bool)] bool sobrescribir, IntPtr res, out uint motivo);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    static extern int WlanDeleteProfile(IntPtr handle, ref Guid iface, string nombre, IntPtr res);

    // Igual que en Brillo: strInterfaceDescription es WCHAR[256]. Sin CharSet.Unicode
    // la estructura mide 276 en vez de 532 y, a partir del segundo adaptador, el GUID
    // que se lee es basura.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strInterfaceDescription;
        public int isState;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] ucSSID;
    }

    // strProfileName es WCHAR[256]: sin CharSet.Unicode la estructura mide 372 en vez
    // de 628 y, de la segunda red en adelante, el puntero cae a mitad de estructura
    // (SSID, señal y banderas basura, y uSSIDLength disparatado reventaba el GetString)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strProfileName;
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;
        public uint uNumberOfBssids;
        [MarshalAs(UnmanagedType.Bool)] public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] dot11PhyTypes;
        [MarshalAs(UnmanagedType.Bool)] public bool bMorePhyTypes;
        public uint wlanSignalQuality;                       // 0..100
        [MarshalAs(UnmanagedType.Bool)] public bool bSecurityEnabled;
        public uint dot11DefaultAuthAlgorithm;
        public uint dot11DefaultCipherAlgorithm;
        public uint dwFlags;                                 // bit 0 = es la conectada, bit 1 = tiene perfil
        public uint dwReserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_CONNECTION_PARAMETERS
    {
        public int wlanConnectionMode;                       // 0 = con perfil
        [MarshalAs(UnmanagedType.LPWStr)] public string strProfile;
        public IntPtr pDot11Ssid;
        public IntPtr pDesiredBssidList;
        public int dot11BssType;                             // 1 = infraestructura
        public uint dwFlags;
    }

    // DOT11_AUTH_ALGORITHM: 1 abierta, 2 WEP compartida, 3 WPA, 4 WPA-PSK, 6 RSNA
    // (WPA2-Enterprise), 7 RSNA-PSK (WPA2), 8 y 11 WPA3-Enterprise, 9 SAE (WPA3), 10 OWE
    static Seguridad Clasificar(uint auth, uint cifrado) => auth switch
    {
        1 when cifrado == 0 => Seguridad.Abierta,
        10 => Seguridad.Abierta,
        4 => Seguridad.Wpa,
        7 => Seguridad.Wpa2,
        9 => Seguridad.Wpa3,
        3 or 6 or 8 or 11 => Seguridad.Empresa,
        _ => Seguridad.Otra                                  // WEP: en 2026, no
    };

    // Abre el handle de wlanapi, recorre las interfaces wifi y lo cierra pase lo que pase
    static void ConCadaInterfaz(Action<IntPtr, Guid> hacer)
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out handle) != 0) return;
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out IntPtr pLista) != 0) return;
            try
            {
                int cuantas = Marshal.ReadInt32(pLista);
                IntPtr pIface = pLista + 8;                  // dwNumberOfItems + dwIndex
                int tamIface = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                for (int i = 0; i < cuantas; i++)
                    hacer(handle, Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(pIface + i * tamIface).InterfaceGuid);
            }
            finally { WlanFreeMemory(pLista); }
        }
        finally { if (handle != IntPtr.Zero) WlanCloseHandle(handle, IntPtr.Zero); }
    }

    public static List<Wifi> Wifis()
    {
        var lista = new List<Wifi>();
        try
        {
            ConCadaInterfaz((handle, guid) =>
            {
                if (WlanGetAvailableNetworkList(handle, ref guid, 3, IntPtr.Zero, out IntPtr pRedes) != 0) return;
                try
                {
                    int nRedes = Marshal.ReadInt32(pRedes);
                    IntPtr pRed = pRedes + 8;
                    int tamRed = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
                    for (int r = 0; r < nRedes; r++)
                    {
                        var red = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(pRed + r * tamRed);
                        int largo = (int)Math.Min(red.dot11Ssid.uSSIDLength, 32);
                        var bytes = red.dot11Ssid.ucSSID.Take(largo).ToArray();
                        string ssid = largo == 0 ? "(red oculta)" : System.Text.Encoding.UTF8.GetString(bytes);
                        lista.Add(new Wifi(ssid, (int)red.wlanSignalQuality, (red.dwFlags & 1) != 0, red.bSecurityEnabled,
                                           guid, red.strProfileName ?? "", bytes,
                                           Clasificar(red.dot11DefaultAuthAlgorithm, red.dot11DefaultCipherAlgorithm),
                                           red.dot11DefaultCipherAlgorithm == 2));
                    }
                }
                finally { WlanFreeMemory(pRedes); }
            });
        }
        catch (Exception ex) { Log.Error("listar wifis", ex); }

        // Una misma red sale varias veces (una por banda, y otra más si tiene perfil):
        // se queda la conectada, luego la que tiene perfil, luego la de mejor señal
        return lista.Where(w => w.Ssid.Length > 0)
                    .GroupBy(w => w.Nombre)
                    .Select(g => g.OrderByDescending(w => w.Conectada)
                                  .ThenByDescending(w => w.Perfil.Length > 0)
                                  .ThenByDescending(w => w.Senal).First() with { Senal = g.Max(w => w.Senal) })
                    .OrderByDescending(w => w.Conectada).ThenByDescending(w => w.Senal)
                    .ToList();
    }

    public static string NombreDeLaConectada() =>
        Wifis().FirstOrDefault(w => w.Conectada)?.Nombre ?? "";

    // Pide a la tarjeta que vuelva a mirar qué redes hay. Vuelve al momento; Windows
    // tarda unos 4 s en tener la lista nueva (es lo que exige la propia API).
    public static void Escanear()
    {
        try { ConCadaInterfaz((h, g) => WlanScan(h, ref g, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)); }
        catch (Exception ex) { Log.Error("escanear wifis", ex); }
    }

    public static void Desconectar(Wifi w)
    {
        try
        {
            ConCadaInterfaz((h, g) => { if (g == w.Interfaz) WlanDisconnect(h, ref g, IntPtr.Zero); });
            Log.Info($"wifi: desconectado de {w.Nombre}");
        }
        catch (Exception ex) { Log.Error("desconectar wifi", ex); }
    }

    // ¿Hace falta preguntar contraseña para entrar en esta red?
    public static bool PideClave(Wifi w) =>
        w.Perfil.Length == 0 && w.Tipo is Seguridad.Wpa or Seguridad.Wpa2 or Seguridad.Wpa3;

    // ¿Se puede entrar desde la isla, o hay que mandar a los ajustes de Windows?
    public static bool SePuedeDesdeAqui(Wifi w) =>
        w.Perfil.Length > 0 || w.Tipo is Seguridad.Abierta or Seguridad.Wpa or Seguridad.Wpa2 or Seguridad.Wpa3;

    // Se conecta y espera a que Windows diga que está dentro (o a que se rinda).
    // Devuelve null si ha ido bien, o el motivo en castellano si no.
    //
    // Si la red no tenía perfil, se crea uno con la contraseña. Y si con esa
    // contraseña no entra, el perfil se BORRA: si no, Windows se quedaría con una
    // clave mala guardada y reintentaría con ella solo cada vez que la viera.
    public static async Task<string?> Conectar(Wifi w, string? clave, CancellationToken ct = default)
    {
        bool creado = false;
        string perfil = w.Perfil;
        string? fallo = null;

        ConCadaInterfaz((h, g) =>
        {
            if (g != w.Interfaz) return;
            if (perfil.Length == 0)
            {
                if (w.Tipo != Seguridad.Abierta && string.IsNullOrEmpty(clave)) { fallo = "Hace falta la contraseña"; return; }
                perfil = w.Nombre;
                string xml = PerfilXml(w, clave);
                int r = WlanSetProfile(h, ref g, 0, xml, null, true, IntPtr.Zero, out uint motivo);
                if (r != 0) { fallo = $"Windows no aceptó el perfil (error {r}, motivo {motivo})"; return; }
                creado = true;
            }
            var p = new WLAN_CONNECTION_PARAMETERS { wlanConnectionMode = 0, strProfile = perfil, dot11BssType = 1 };
            int c = WlanConnect(h, ref g, ref p, IntPtr.Zero);
            if (c != 0) fallo = $"No se pudo pedir la conexión (error {c})";
        });

        if (fallo == null)
        {
            Log.Info($"wifi: conectando a {w.Nombre} ({w.Tipo}, perfil {(creado ? "nuevo" : "guardado")})");
            // Una conexión WPA2 normal entra en 2-5 s; con 15 sobra
            var limite = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < limite)
            {
                await Task.Delay(700, ct);
                if (Wifis().Any(x => x.Conectada && x.Nombre == w.Nombre)) { Log.Info($"wifi: dentro de {w.Nombre}"); return null; }
            }
            fallo = creado ? "No entra: ¿la contraseña está bien?" : "No se pudo conectar";
        }

        if (creado)
            try { ConCadaInterfaz((h, g) => { if (g == w.Interfaz) WlanDeleteProfile(h, ref g, perfil, IntPtr.Zero); }); }
            catch { }
        Log.Info($"wifi: {w.Nombre}: {fallo}");
        return fallo;
    }

    static string PerfilXml(Wifi w, string? clave)
    {
        string nombre = SecurityElement.Escape(w.Nombre);
        string hex = Convert.ToHexString(w.Ssid);
        string seguridad = w.Tipo switch
        {
            Seguridad.Abierta => """
                <authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption>
                """,
            _ => $"""
                <authEncryption><authentication>{w.Tipo switch { Seguridad.Wpa => "WPAPSK", Seguridad.Wpa3 => "WPA3SAE", _ => "WPA2PSK" }}</authentication><encryption>{(w.Tkip ? "TKIP" : "AES")}</encryption><useOneX>false</useOneX></authEncryption>
                <sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{SecurityElement.Escape(clave ?? "")}</keyMaterial></sharedKey>
                """
        };
        return $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{nombre}</name>
              <SSIDConfig><SSID><hex>{hex}</hex><name>{nombre}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>auto</connectionMode>
              <MSM><security>
                {seguridad}
              </security></MSM>
            </WLANProfile>
            """;
    }
}
