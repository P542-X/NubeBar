using System;
using System.Windows;

namespace NubeBar;

// El runner de `--guia`. Ver DiagGuia.cs para qué garantiza y por qué.
public partial class MainWindow
{
    async System.Threading.Tasks.Task CorrerGuiaManual()
    {
        try
        {
            Apunta("=== modo de demostración de la guía de NubeHypr ===");
            Apunta($"ejecutable: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta($"NubeHypr en memoria: activo={_cfg.NubeHypr.Activo} modo={_cfg.NubeHypr.Modo}");
            Apunta("");
            Apunta("Lo que este modo NO puede hacer, y por qué:");
            Apunta("  · escribir config.json  -> es una bandera, y `ConfigArbol.Guardar` se niega");
            Apunta("  · migrar el esquema     -> `Config.Cargar` solo migra si NO es solo lectura");
            Apunta("  · encender NubeHypr     -> `DiagGuia.Apagar` lo fuerza al cargar");
            Apunta("  · coger atajos globales -> `AplicarAtajosDeHypr` sale antes de registrar");
            Apunta("  · mover ventanas        -> nada de esto llama al adaptador");
            Apunta("");

            // ---------- La comprobación, no la promesa ----------
            //
            // Lo de arriba es lo que el código pretende. Esto es lo que se puede medir desde
            // aquí: si alguna sale mal, se dice y el modo no se abre. Un modo seguro que se
            // abre igual cuando sus garantías no se cumplen es peor que no tenerlo, porque
            // invita a tocar lo que dice que protege.
            var fallos = 0;
            if (!Config.SoloLectura) { Apunta("[GUÍA] FALLA: la configuración NO está en solo lectura"); fallos++; }
            if (_cfg.NubeHypr.Activo) { Apunta("[GUÍA] FALLA: NubeHypr ha quedado activo"); fallos++; }
            if (!string.Equals(_cfg.NubeHypr.Modo, "simulacion", StringComparison.OrdinalIgnoreCase))
            { Apunta($"[GUÍA] FALLA: el modo es «{_cfg.NubeHypr.Modo}», no simulación"); fallos++; }

            var huellaAntes = HuellaDelConfig();
            Apunta($"[GUÍA] huella de tu config.json al empezar: {huellaAntes}");

            if (fallos > 0)
            {
                Apunta($"[GUÍA] {fallos} garantía(s) sin cumplir: NO se abre la guía.");
                Application.Current.Shutdown(3);
                return;
            }

            // ---------- Y se abre ----------
            await System.Threading.Tasks.Task.Delay(1500);

            VentanaAjustes? ajustes = null;
            try
            {
                ajustes = new VentanaAjustes();
                ajustes.Show();
                ajustes.Activate();
                ajustes.Ir("nubehypr");
            }
            catch (Exception ex) { Log.Error("abrir Ajustes en el modo de demostración", ex); }

            if (ajustes == null)
            {
                Apunta("[GUÍA] no se ha podido abrir Ajustes");
                Application.Current.Shutdown(4);
                return;
            }

            Apunta("[GUÍA] la guía está abierta en Ajustes → NubeHypr.");
            Apunta("[GUÍA] Cierra la ventana de Ajustes cuando termines: NubeBar se cerrará");
            Apunta("       solo y volverá a dejarte tu barra de siempre.");

            // Al cerrar Ajustes se termina. Así no queda una segunda barra encendida que
            // haya que ir a apagar, que es justo el accidente que esto viene a evitar.
            ajustes.Closed += (_, _) =>
            {
                var huellaDespues = HuellaDelConfig();
                Apunta($"[GUÍA] huella de tu config.json al salir:  {huellaDespues}");
                Apunta(huellaAntes == huellaDespues
                    ? "[GUÍA] tu configuración está intacta, byte a byte."
                    : "[GUÍA] ¡OJO! la configuración ha cambiado. No debería: revísala.");
                Application.Current.Shutdown(0);
            };
        }
        catch (Exception ex)
        {
            Apunta($"[GUÍA] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el modo de demostración de la guía", ex);
            Application.Current.Shutdown(1);
        }
    }

    // La huella del fichero del usuario, para poder decir al salir si se ha tocado. Es la
    // única prueba que vale: lo demás son intenciones del código.
    static string HuellaDelConfig()
    {
        try
        {
            if (!System.IO.File.Exists(Config.Ruta)) return "(no existe)";
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var f = System.IO.File.OpenRead(Config.Ruta);
            return Convert.ToHexString(sha.ComputeHash(f))[..16].ToLowerInvariant();
        }
        catch (Exception ex) { Log.Error("huella del config", ex); return "(no se pudo leer)"; }
    }
}
