using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NubeBar;

// Brillo del monitor.
//
// En un portátil se hace por WMI, pero en un monitor de sobremesa hay que hablarle
// al propio monitor por el cable con DDC/CI (dxva2.dll). No todos lo admiten, ni
// todos los que lo admiten lo tienen activado en su menú de pantalla: por eso todo
// esto se prueba al arrancar y, si el monitor no contesta, la opción no se ofrece.
static class Brillo
{
    // CharSet.Unicode NO es decorativo: szDescription es WCHAR[128] en el nativo.
    // Sin él, ByValTStr se marshala como 128 BYTES y la estructura mide 136 en vez de
    // 264: dxva2 escribe 264 por monitor sobre un hueco de 136 y machaca el montón.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szDescription;
    }

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint cuantos);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint tam, [Out] PHYSICAL_MONITOR[] monitores);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool DestroyPhysicalMonitors(uint tam, [In] PHYSICAL_MONITOR[] monitores);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimo, out uint actual, out uint maximo);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool SetMonitorBrightness(IntPtr monitor, uint nuevo);

    public sealed record Pantalla(string Nombre, IntPtr Fisico, uint Minimo, uint Actual, uint Maximo)
    {
        public int PorCiento => Maximo > Minimo ? (int)Math.Round((Actual - Minimo) * 100.0 / (Maximo - Minimo)) : 0;
    }

    // Se abren los monitores físicos de cada pantalla y se pregunta el brillo.
    // Los que no contestan se descartan en silencio: es lo normal en monitores
    // conectados por DisplayPort con DDC/CI apagado de fábrica.
    // Los handles de monitor físico hay que devolverlos. Antes solo se devolvían
    // cuando NINGUNO contestaba, así que cada vez que se abría el panel de control
    // (que llama a Pantallas() dos veces) se fugaban tantos handles del driver de
    // vídeo como monitores con DDC/CI. Ahora se devuelven todos al terminar.
    public static void Soltar(List<Pantalla> pantallas)
    {
        foreach (var p in pantallas)
        {
            try { DestroyPhysicalMonitors(1, new[] { new PHYSICAL_MONITOR { hPhysicalMonitor = p.Fisico, szDescription = "" } }); }
            catch { }
        }
    }

    public static List<Pantalla> Pantallas()

    {
        var lista = new List<Pantalla>();
        foreach (var m in Monitores.Todos())
        {
            try
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(m.Handle, out uint n) || n == 0) continue;
                var fisicos = new PHYSICAL_MONITOR[n];
                if (!GetPhysicalMonitorsFromHMONITOR(m.Handle, n, fisicos)) continue;

                bool alguno = false;
                foreach (var f in fisicos)
                {
                    if (GetMonitorBrightness(f.hPhysicalMonitor, out uint min, out uint act, out uint max))
                    {
                        lista.Add(new Pantalla(string.IsNullOrWhiteSpace(f.szDescription) ? m.Dispositivo : f.szDescription,
                                               f.hPhysicalMonitor, min, act, max));
                        alguno = true;
                    }
                }
                // Si ninguno contestó, se devuelven ya: no vamos a usarlos
                if (!alguno) DestroyPhysicalMonitors(n, fisicos);
            }
            catch (Exception ex) { Log.Error("leer el brillo", ex); }
        }
        return lista;
    }

    public static bool Poner(Pantalla p, int porCiento)
    {
        try
        {
            uint valor = (uint)Math.Round(p.Minimo + (p.Maximo - p.Minimo) * Math.Clamp(porCiento, 0, 100) / 100.0);
            return SetMonitorBrightness(p.Fisico, valor);
        }
        catch (Exception ex) { Log.Error("poner el brillo", ex); return false; }
    }

    static bool? _hay;

    public static bool Disponible
    {
        get
        {
            if (_hay is { } ya) return ya;
            var p = Pantallas();
            _hay = p.Count > 0;
            Log.Info($"brillo por DDC/CI: {(p.Count > 0 ? $"{p.Count} pantalla(s) responden" : "ningún monitor responde, la opción no se ofrece")}");
            return _hay.Value;
        }
    }
}
