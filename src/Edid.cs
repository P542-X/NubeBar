using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace NubeBar;

// Quién es ese monitor DE VERDAD: fabricante, modelo y número de serie, leídos de su EDID.
//
// ---------- Por qué no vale lo que ya teníamos ----------
//
// `Monitores.Info.Dispositivo` da `\\.\DISPLAY1`, y eso NO es el monitor: es la ranura. Windows
// reasigna esos números al reiniciar, al cambiar de cable o al encender las pantallas en otro
// orden, así que un alias guardado contra `DISPLAY2` acaba puesto en la pantalla equivocada.
//
// Y `szDescription`, lo único que devuelve dxva2, en la mitad de los monitores del mundo dice
// «Generic PnP Monitor». Con dos pantallas eso son dos filas idénticas y ninguna forma de
// saber cuál es cuál.
//
// El EDID sí lo sabe: lo graba el fabricante en el propio monitor y viaja por el cable. De los
// dos AOC de la máquina de desarrollo sale «AOC Q27G3G3R3» con código de producto B309 y
// «AOC 25G3ZM» con B322, que es exactamente lo que hace falta para distinguirlos.
//
// ---------- Por qué del registro y no por WMI ----------
//
// `WmiMonitorID` da lo mismo pero obliga a levantar una consulta WMI (~200 ms la primera vez,
// y en esta máquina ya hay precedentes de WMI tardando segundos). El EDID en bruto está en el
// registro, es una lectura de un `byte[]`, y además trae los bloques de texto del fabricante,
// que es de donde sale el nombre bonito del modelo.
//
// Todo lo de aquí FALLA HACIA null. Un monitor sin EDID legible (sesión remota, driver básico,
// una tele por un conversor) sigue saliendo en la lista: lo que pierde es el nombre bonito, no
// su sitio. Ver `Entradas.Clave` para el plan B de la identidad.
static class Edid
{
    // Lo que el EDID sabe decir de sí mismo. `Nombre` y `Serie` pueden faltar: los bloques de
    // texto son opcionales en la norma y hay monitores que no traen ninguno.
    public sealed record Datos(
        string Fabricante,      // los tres caracteres del PnP ID: "AOC", "DEL", "SAM"…
        ushort Codigo,          // el código de producto: 0xB309 para el Q27G3G3R3
        uint NumeroDeSerie,     // el serial numérico, 0 si el monitor no lo pone
        string? Nombre,         // el modelo en letra, del descriptor 0xFC: "Q27G3G3R3"
        string? Serie)          // el serial en letra, del descriptor 0xFF
    {
        // Cómo se le llama en la interfaz. El fabricante delante porque «Q27G3G3R3» a secas no
        // le dice nada a nadie, y «AOC Q27G3G3R3» es como está escrito en la caja.
        public string Rotulo =>
            string.IsNullOrWhiteSpace(Nombre) ? $"{Fabricante} {Codigo:X4}" : $"{Fabricante} {Nombre}";

        // La identidad estable, y en este orden a propósito:
        //
        //   fabricante + código de producto  → siempre están (son campos obligatorios del EDID)
        //   + serie                          → desempata dos monitores del MISMO modelo
        //
        // Sin la serie, dos AOC Q27G3G3R3 iguales compartirían clave y el alias de uno saldría
        // en el otro. Con ella no, salvo que el fabricante haya grabado la misma serie en los
        // dos — pasa, y para eso está el desempate por ranura que añade `Entradas`.
        public string Clave
        {
            get
            {
                var s = !string.IsNullOrWhiteSpace(Serie) ? Serie!.Trim()
                      : NumeroDeSerie != 0 ? NumeroDeSerie.ToString("X8")
                      : "";
                return s.Length > 0 ? $"{Fabricante}{Codigo:X4}-{s}" : $"{Fabricante}{Codigo:X4}";
            }
        }
    }

    // ---------- Descifrar los 128 bytes ----------
    //
    // Pura y `internal` para poder probarla con un EDID escrito a mano, que es la única forma
    // razonable de comprobar un descifrador de formato binario: enchufar un monitor no es una
    // prueba que se pueda correr.
    //
    // Devuelve null si no es un EDID. La cabecera es fija (00 FF FF FF FF FF FF 00) y
    // comprobarla no es ceremonia: en el registro hay claves de monitores desconectados con
    // valores a medias, y un bloque de basura descifrado «con éxito» saldría en la interfaz
    // como un fabricante de tres caracteres aleatorios.
    internal static Datos? Parsear(byte[]? e)
    {
        if (e is null || e.Length < 128) return null;

        ReadOnlySpan<byte> cabecera = stackalloc byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        for (int i = 0; i < 8; i++) if (e[i] != cabecera[i]) return null;

        // Bytes 8-9: el PnP ID del fabricante. Tres letras de CINCO bits metidas en 16, en
        // big-endian y con 1 = 'A'. "AOC" sale como 0x0503.
        int id = (e[8] << 8) | e[9];
        var letras = new char[3];
        for (int i = 0; i < 3; i++)
        {
            int v = (id >> (10 - i * 5)) & 0x1F;
            // 0 no es letra. Un EDID con un hueco aquí está roto, y un fabricante con un '@'
            // en medio es peor que no tener fabricante.
            if (v is < 1 or > 26) return null;
            letras[i] = (char)('A' + v - 1);
        }

        // Bytes 10-11: código de producto, little-endian. Bytes 12-15: serie, little-endian.
        ushort codigo = (ushort)(e[10] | (e[11] << 8));
        uint serie = (uint)(e[12] | (e[13] << 8) | (e[14] << 16) | (e[15] << 24));

        string? nombre = null, serieTexto = null;

        // Los cuatro descriptores de 18 bytes. Un descriptor de TEXTO empieza por 00 00 00,
        // lleva su etiqueta en el byte 3 y el texto en los 13 últimos, terminado en 0x0A y
        // rellenado con espacios.
        for (int d = 54; d + 18 <= Math.Min(e.Length, 128); d += 18)
        {
            if (e[d] != 0 || e[d + 1] != 0 || e[d + 2] != 0) continue;   // es un modo de vídeo
            string texto = TextoDescriptor(e, d + 5);
            if (texto.Length == 0) continue;
            switch (e[d + 3])
            {
                case 0xFC: nombre = nombre is null ? texto : nombre + texto; break;  // nombre del modelo
                case 0xFF: serieTexto ??= texto; break;                              // serie en letra
            }
        }

        return new Datos(new string(letras), codigo, serie, nombre?.Trim(), serieTexto?.Trim());
    }

    // El nombre del modelo puede venir PARTIDO en dos descriptores 0xFC cuando no cabe en 13
    // caracteres, y entonces el primer trozo NO acaba en 0x0A — por eso arriba se concatena en
    // vez de quedarse con el primero.
    static string TextoDescriptor(byte[] e, int desde)
    {
        var sb = new StringBuilder(13);
        for (int i = desde; i < desde + 13 && i < e.Length; i++)
        {
            if (e[i] == 0x0A) break;            // fin de cadena
            if (e[i] is < 0x20 or > 0x7E) continue;
            sb.Append((char)e[i]);
        }
        return sb.ToString().TrimEnd();
    }

    // ---------- Y de dónde salen esos bytes ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string? dispositivo, uint indice, ref DISPLAY_DEVICE dd, uint banderas);

    // Con esta bandera, `DeviceID` del MONITOR trae el nombre de interfaz de dispositivo
    // (`\\?\DISPLAY#AOC2432#5&1a2b3c4d&0&UID4353#{e6f07b5f-…}`), que es lo que se puede
    // convertir en ruta del registro. Sin ella viene el PnP ID a secas y no hay instancia.
    const uint EDD_GET_DEVICE_INTERFACE_NAME = 1;
    const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

    // Los EDID de una pantalla lógica (`\\.\DISPLAY1`), en el orden en que Windows enumera sus
    // monitores. Una pantalla puede tener más de uno (un divisor, una cadena DisplayPort), y
    // por eso esto devuelve lista y no un solo valor.
    //
    // El orden es el que importa: `Entradas` empareja el monitor físico número N de esa
    // pantalla con el EDID número N. No es una garantía de la API —y está dicho en el plan que
    // no se puede asumir— pero es la única correspondencia que Windows ofrece, y cuando falla
    // lo que se pierde es el rótulo bonito, nunca el control del monitor.
    public static List<Datos> DePantalla(string dispositivo)
    {
        var lista = new List<Datos>();
        if (string.IsNullOrWhiteSpace(dispositivo)) return lista;

        try
        {
            for (uint i = 0; ; i++)
            {
                var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(dispositivo, i, ref mon, EDD_GET_DEVICE_INTERFACE_NAME)) break;
                if ((mon.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;
                if (Parsear(LeerDelRegistro(mon.DeviceID)) is { } d) lista.Add(d);
            }
        }
        catch (Exception ex) { Log.Error($"leer el EDID de {dispositivo}", ex); }

        return lista;
    }

    // De `\\?\DISPLAY#AOC2432#5&1a2b3c4d&0&UID4353#{guid}` a
    // `SYSTEM\CurrentControlSet\Enum\DISPLAY\AOC2432\5&1a2b3c4d&0&UID4353\Device Parameters`.
    //
    // `internal` porque la conversión es puro manejo de cadenas y se prueba sin registro y sin
    // monitores: es la parte que se rompe sola el día que alguien «limpie» el formato.
    internal static string? RutaDelRegistro(string? idDeInterfaz)
    {
        if (string.IsNullOrWhiteSpace(idDeInterfaz)) return null;

        var s = idDeInterfaz.Trim();
        if (s.StartsWith(@"\\?\", StringComparison.Ordinal)) s = s.Substring(4);
        else if (s.StartsWith(@"\\.\", StringComparison.Ordinal)) s = s.Substring(4);

        // El GUID de la clase de interfaz va al final y no forma parte de la ruta.
        int llave = s.IndexOf('{');
        if (llave > 0) s = s.Substring(0, llave).TrimEnd('#');

        var trozos = s.Split('#', StringSplitOptions.RemoveEmptyEntries);
        // DISPLAY + hardware id + instancia. Con menos de tres no hay instancia que abrir.
        if (trozos.Length < 3) return null;

        return @"SYSTEM\CurrentControlSet\Enum\" +
               string.Join(@"\", trozos.Take(3)) + @"\Device Parameters";
    }

    static byte[]? LeerDelRegistro(string? idDeInterfaz)
    {
        var ruta = RutaDelRegistro(idDeInterfaz);
        if (ruta is null) return null;
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(ruta);
            return k?.GetValue("EDID") as byte[];
        }
        catch { return null; }      // sin permiso o clave a medias: se queda sin rótulo, y punto
    }
}
