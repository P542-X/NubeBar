using System;

namespace NubeBar;

// `NubeBar.exe --ventanita "<título>" [segundos]`: una ventana pelada que se cierra sola.
//
// ---------- Para qué existe ----------
//
// Para poder comprobar, de extremo a extremo y sin tocar una sola ventana del usuario, que
// una regla coloca una ventana cuando aparece (D9.5).
//
// El arnés lo intentó primero con una ventana suya, en su propio proceso, y no funcionó por
// un motivo que es correcto: `HyprAdaptador.Observar` descarta las ventanas de NubeBar
// —`Nativo.EsNuestra`, que compara el **PID**—, porque lo último que puede hacer un gestor
// de ventanas es recolocarse a sí mismo la barra. Así que el arnés movía su ventana a mano
// y daba verde, pero el camino automático no se llegaba a recorrer nunca.
//
// Un proceso aparte tiene otro PID. Es nuestro código, es nuestra ventana, se abre y se
// cierra sola, y para el observador es una ventana normal: justo lo que hacía falta.
//
// ---------- Lo que NO hace ----------
//
// No arranca la barra, no lee ni escribe la configuración, no toca la bandeja ni los
// atajos. Abre un `Window`, espera y se va. Y como es una bandera, `Config.PuedeEscribir`
// es falso: ni queriendo podría tocar el config.
public static class DiagVentanita
{
    internal const string Bandera = "--ventanita";

    public static bool Encendido;
    public static string Titulo = "NubeBar · ventanita";
    public static TimeSpan Cuanto = TimeSpan.FromSeconds(8);

    // Tope por si alguien la lanza a mano y se olvida: una ventana de pruebas que se queda
    // en pantalla para siempre es basura en el escritorio de alguien.
    public static readonly TimeSpan Maximo = TimeSpan.FromMinutes(2);

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], Bandera, StringComparison.OrdinalIgnoreCase))
            return;
        Encendido = true;
        if (args.Length > 1 && args[1].Length > 0) Titulo = args[1];
        if (args.Length > 2 && double.TryParse(args[2], System.Globalization.NumberStyles.Any,
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               out double s) && s > 0)
            Cuanto = TimeSpan.FromSeconds(s);
        if (Cuanto > Maximo) Cuanto = Maximo;
    }
}
