using System;
using System.Linq;
using System.Windows;

namespace NubeBar;

// El runner de `--diage0`. Ver DiagE0.cs para qué comprueba y por qué.
public partial class MainWindow
{
    async System.Threading.Tasks.Task CorrerDiagnosticoE0()
    {
        int fallos = 0;
        try
        {
            Apunta("=== E0: el tablero de superficies contra la realidad ===");
            Apunta($"ejecutable: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta($"dueño de esta isla: {DuenoDeLaMesa}  ·  vertical={Vertical}");
            Apunta($"modo clásico (composición E apagada): {_mesa.ModoClasico}");
            Apunta("");

            await System.Threading.Tasks.Task.Delay(5000);

            // ---------- La barra, que no la había mirado nadie ----------
            //
            // Los paneles ya se miran uno a uno más abajo. La isla en sí —el lanzador, los
            // escritorios, las apps fijadas, el carrusel, el reloj, la bandeja— no se había
            // medido nunca, y es lo que está siempre en pantalla.
            //
            // Y DESPLEGADA. Con la isla recogida, las zonas izquierda y derecha no están
            // visibles, y la medida —que salta lo invisible, y hace bien— decía «sin
            // botones» de dos tercios de la barra. Una medida que mira donde no hay nada da
            // verde siempre.
            Expandir(true, forzar: true);
            await System.Threading.Tasks.Task.Delay(900);

            foreach (var (nombre, destino) in ZonasDibujables())
            {
                if (destino == null) continue;
                var a = MirarElTecladoDe(destino);
                if (a.Botones == 0) { Apunta($"[A11Y] zona {nombre}: sin botones"); continue; }
                Apunta(a.SinNombre > 0
                    ? $"[A11Y] zona {nombre}: {a.SinNombre} de {a.Botones} sin nombre: {a.Ejemplo}"
                    : $"[A11Y] zona {nombre}: {a.Botones} botones, todos con nombre");
            }
            Apunta("");

            // ---------- De entrada, vacío ----------
            fallos += Cuadran("al arrancar, sin paneles");

            // ---------- Panel por panel ----------
            //
            // Por el mismo camino que los abre el usuario, no llamando a `AbrirPanel` a pelo:
            // un diagnóstico que usa una puerta distinta de la del usuario comprueba otra
            // cosa.
            var puertas = new (string Que, Action Abrir)[]
            {
                ("audio", () => AbrirAudio()),
                ("red", () => AbrirRed()),
                ("bateria", () => AbrirBateria()),
                ("bandeja", () => AlternarBandejaExtra()),
                ("notificaciones", () => AbrirNotificaciones()),
                ("vista", () => AlternarVista()),
            };

            foreach (var (que, abrir) in puertas)
            {
                try
                {
                    abrir();
                    await System.Threading.Tasks.Task.Delay(1100);
                    fallos += Cuadran($"con «{que}» abierto");
                    Apunta($"   tablero: {_mesa.Linea()}");

                    // Y de paso, lo que oiría un lector de pantalla. Va aquí además de en
                    // `--diagpaneles` por una razón práctica: aquel recorre cinco
                    // colocaciones y dos monitores y tarda doce minutos, y para perseguir
                    // botones mudos de uno en uno hace falta una vuelta corta.
                    if (RaizDelPanelAbierto() is { } raizA)
                    {
                        var a = MirarElTecladoDe(raizA);
                        Apunta(a.SinNombre > 0
                            ? $"   [A11Y] {a.SinNombre} de {a.Botones} sin nombre: {a.Ejemplo}"
                            : $"   [A11Y] {a.Botones} botones, todos con nombre");
                    }

                    CerrarPanel();
                    await System.Threading.Tasks.Task.Delay(700);
                    fallos += Cuadran($"tras cerrar «{que}»");
                }
                catch (Exception ex)
                {
                    Apunta($"[E0] «{que}» REVENTÓ: {ex.GetType().Name}: {ex.Message}");
                    Log.Error($"diagnóstico E0, panel {que}", ex);
                    fallos++;
                }
            }

            // ---------- Uno detrás de otro, sin cerrar ----------
            //
            // El caso que de verdad ejercita el modo clásico: abrir el segundo tiene que
            // echar al primero, y el tablero tiene que enterarse. Si se quedara con los dos,
            // al encender la composición E aparecería un panel fantasma.
            Apunta("");
            Apunta("[E0] tres paneles seguidos, sin cerrar entre medias:");
            foreach (var (que, abrir) in puertas.Take(3))
            {
                abrir();
                await System.Threading.Tasks.Task.Delay(900);
                int paneles = _mesa.DeClase(Superficies.Clase.Panel).Count;
                Apunta($"   tras abrir «{que}»: {paneles} panel(es) en el tablero  ·  " +
                       $"isla={_panelAbierto ?? "ninguno"}");
                if (paneles != 1)
                {
                    Apunta($"[E0] FALLA: en modo clásico tiene que haber 1 panel, hay {paneles}");
                    fallos++;
                }
                fallos += Cuadran($"con «{que}» encadenado");
            }

            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(700);
            fallos += Cuadran("tras cerrar el último");

            // ---------- Y la limpieza ----------
            int quedan = _mesa.Cuantas;
            Apunta("");
            Apunta($"[E0] superficies en el tablero al terminar: {quedan} ({_mesa.Linea()})");
            if (quedan != 0)
            {
                Apunta("[E0] FALLA: tendría que estar vacío");
                fallos++;
            }

            Apunta("");
            Apunta(fallos == 0
                ? "[E0] el tablero y la isla dicen lo mismo en todos los casos."
                : $"[E0] ¡OJO! {fallos} discrepancia(s): el tablero y la isla NO cuadran.");
            Apunta("[E0] no se ha movido ninguna ventana y no se ha escrito la configuración.");
        }
        catch (Exception ex)
        {
            Apunta($"[E0] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico E0", ex);
        }
        finally
        {
            try { CerrarPanel(); } catch { }
            Application.Current.Shutdown(fallos == 0 ? 0 : 5);
        }
    }

    // ---------- La comprobación ----------
    //
    // Las dos fuentes de verdad tienen que decir lo mismo: si la isla cree que hay un panel
    // abierto, el tablero tiene que tener ese panel y no otro; y si la isla no tiene ninguno,
    // el tablero tampoco.
    int Cuadran(string cuando)
    {
        var enLaMesa = _mesa.DeClase(Superficies.Clase.Panel)
                            .Where(s => s.Dueño == DuenoDeLaMesa)
                            .Select(s => s.Id)
                            .ToList();

        string isla = _panelAbierto ?? "";
        bool bien = isla.Length == 0
            ? enLaMesa.Count == 0
            : enLaMesa.Count == 1 && string.Equals(enLaMesa[0], isla, StringComparison.OrdinalIgnoreCase);

        if (!bien)
            Apunta($"[E0] FALLA {cuando}: la isla dice «{(isla.Length == 0 ? "ninguno" : isla)}» " +
                   $"y el tablero «{(enLaMesa.Count == 0 ? "ninguno" : string.Join(",", enLaMesa))}»");

        return bien ? 0 : 1;
    }
}
