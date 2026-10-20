using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NubeBar;

// Estado de la red y lista de wifis, con la API nativa de Windows (wlanapi).
// Solo lectura y escaneo: conectarse a una red se deja para cuando lo pidas tú.
static class Red
{
    public sealed record Wifi(string Nombre, int Senal, bool Conectada, bool Segura);

    public enum Tipo { Sin, Cable, Wifi }

    public static Tipo Estado()
    {
        try
        {
            var vivas = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();
            if (vivas.Count == 0) return Tipo.Sin;
            if (vivas.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                               n.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)) return Tipo.Cable;
            if (vivas.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)) return Tipo.Wifi;
            return Tipo.Cable;
        }
        catch { return Tipo.Sin; }
    }

    // ---------- wlanapi ----------
    [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint version, IntPtr res, out uint negociada, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr res);
    [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr res, out IntPtr lista);
    [DllImport("wlanapi.dll")] static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid iface, uint flags, IntPtr res, out IntPtr lista);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

    [StructLayout(LayoutKind.Sequential)]
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

    [StructLayout(LayoutKind.Sequential)]
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
        public uint dwFlags;                                 // bit 0 = es la conectada
        public uint dwReserved;
    }

    public static List<Wifi> Wifis()
    {
        var lista = new List<Wifi>();
        IntPtr handle = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out handle) != 0) return lista;
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out IntPtr pLista) != 0) return lista;

            int cuantas = Marshal.ReadInt32(pLista);
            IntPtr pIface = pLista + 8;                      // dwNumberOfItems + dwIndex
            int tamIface = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

            for (int i = 0; i < cuantas; i++)
            {
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(pIface + i * tamIface);
                var guid = info.InterfaceGuid;
                if (WlanGetAvailableNetworkList(handle, ref guid, 3, IntPtr.Zero, out IntPtr pRedes) != 0) continue;

                int nRedes = Marshal.ReadInt32(pRedes);
                IntPtr pRed = pRedes + 8;
                int tamRed = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
                for (int r = 0; r < nRedes; r++)
                {
                    var red = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(pRed + r * tamRed);
                    string ssid = red.dot11Ssid.uSSIDLength == 0
                        ? "(red oculta)"
                        : System.Text.Encoding.UTF8.GetString(red.dot11Ssid.ucSSID, 0, (int)red.dot11Ssid.uSSIDLength);
                    lista.Add(new Wifi(ssid, (int)red.wlanSignalQuality, (red.dwFlags & 1) != 0, red.bSecurityEnabled));
                }
                WlanFreeMemory(pRedes);
            }
            WlanFreeMemory(pLista);
        }
        catch (Exception ex) { Log.Error("listar wifis", ex); }
        finally { if (handle != IntPtr.Zero) WlanCloseHandle(handle, IntPtr.Zero); }

        // Una misma red sale varias veces (una por banda): se queda la de mejor señal
        return lista.GroupBy(w => w.Nombre)
                    .Select(g => g.OrderByDescending(w => w.Conectada).ThenByDescending(w => w.Senal).First())
                    .OrderByDescending(w => w.Conectada).ThenByDescending(w => w.Senal)
                    .ToList();
    }

    public static string NombreDeLaConectada() =>
        Wifis().FirstOrDefault(w => w.Conectada)?.Nombre ?? "";
}
