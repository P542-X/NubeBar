using System;

namespace NubeBar;

// `NubeBar.exe --diagmotor [segundos] [hwnd]`: el motor de D2, en simulación.
//
// ---------- Qué hace y qué NO ----------
//
// Observa, calcula el reparto, enseña la previsualización del destino y apunta destino por
// destino. **No mueve nada**, porque el modo por defecto es simulación.
//
// Para mover de verdad hay que pedirlo con `--diagmotor <segundos> <hwnd>`: entonces se
// aplica a UNA sola ventana, la que se le diga por su handle, y a ninguna más. Es lo que
// pide la especificación —"empezar con una sola ventana elegida"— y lo que evita descubrir
// un efecto raro de `SetWindowPos` con doce ventanas a la vez.
//
// Sin hwnd no se mueve ni una. Y nunca escribe la configuración: ver `Config.SoloLectura`.
public static class DiagMotor
{
    internal const string Bandera = "--diagmotor";

    public static bool Encendido;
    public static int Segundos = 15;

    // El handle de la ÚNICA ventana a la que se le permite aplicar de verdad. Cero
    // significa simulación pura, y es el valor por defecto.
    public static IntPtr SoloEsta = IntPtr.Zero;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 900) Segundos = s;

        // El hwnd se acepta en decimal o en hexadecimal con 0x: un log lo escribe en hex
        // y copiarlo de ahí tiene que funcionar.
        if (args.Length > 2)
        {
            string h = args[2].Trim();
            bool hex = h.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (hex) h = h[2..];
            if (long.TryParse(h, hex ? System.Globalization.NumberStyles.HexNumber
                                     : System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out long v) && v != 0)
                SoloEsta = (IntPtr)v;
        }
    }
}
