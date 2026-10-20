using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NubeBar;

// Bluetooth: encenderlo o apagarlo y ver los dispositivos emparejados.
// Va por WinRT (Windows.Devices.Radios y Windows.Devices.Enumeration), que ya
// está disponible gracias al TFM net10.0-windows10.0.19041.0.
static class Bluetooth
{
    public sealed record Aparato(string Nombre, bool Conectado);

    public enum EstadoRadio { NoHay, Encendida, Apagada, SinPermiso }

    static Windows.Devices.Radios.Radio? _radio;

    static async Task<Windows.Devices.Radios.Radio?> Radio()
    {
        if (_radio != null) return _radio;
        try
        {
            var acceso = await Windows.Devices.Radios.Radio.RequestAccessAsync();
            if (acceso != Windows.Devices.Radios.RadioAccessStatus.Allowed) return null;
            var todas = await Windows.Devices.Radios.Radio.GetRadiosAsync();
            _radio = todas.FirstOrDefault(r => r.Kind == Windows.Devices.Radios.RadioKind.Bluetooth);
        }
        catch (Exception ex) { Log.Error("buscar la radio bluetooth", ex); }
        return _radio;
    }

    public static async Task<EstadoRadio> Estado()
    {
        try
        {
            var r = await Radio();
            if (r == null) return EstadoRadio.NoHay;
            return r.State switch
            {
                Windows.Devices.Radios.RadioState.On => EstadoRadio.Encendida,
                Windows.Devices.Radios.RadioState.Off => EstadoRadio.Apagada,
                _ => EstadoRadio.NoHay
            };
        }
        catch { return EstadoRadio.NoHay; }
    }

    public static async Task<bool> Encender(bool si)
    {
        try
        {
            var r = await Radio();
            if (r == null) return false;
            var estado = si ? Windows.Devices.Radios.RadioState.On : Windows.Devices.Radios.RadioState.Off;
            var res = await r.SetStateAsync(estado);
            bool ok = res == Windows.Devices.Radios.RadioAccessStatus.Allowed;
            Log.Info($"bluetooth {(si ? "encendido" : "apagado")}: {(ok ? "hecho" : res.ToString())}");
            return ok;
        }
        catch (Exception ex) { Log.Error("cambiar el bluetooth", ex); return false; }
    }

    // Solo los ya emparejados: no se hace búsqueda de nuevos, que enciende la radio
    // y hace ruido en el sistema sin que nadie lo haya pedido.
    public static async Task<List<Aparato>> Emparejados()
    {
        var lista = new List<Aparato>();
        try
        {
            string filtro = Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var encontrados = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(filtro);
            foreach (var d in encontrados)
            {
                bool conectado = false;
                try
                {
                    var bd = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(d.Id);
                    conectado = bd?.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected;
                }
                catch { }
                lista.Add(new Aparato(d.Name, conectado));
            }

            // Los auriculares y mandos modernos son LE y salen por otro selector
            string filtroLe = Windows.Devices.Bluetooth.BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            foreach (var d in await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(filtroLe))
                if (!lista.Any(a => a.Nombre == d.Name)) lista.Add(new Aparato(d.Name, false));
        }
        catch (Exception ex) { Log.Error("listar dispositivos bluetooth", ex); }
        return lista.OrderByDescending(a => a.Conectado).ThenBy(a => a.Nombre).ToList();
    }
}
