using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace NubeBar;

// El runner de `--vigilia`. Ver DiagVigilia.cs para qué mide y por qué es una bandera.
public partial class MainWindow
{
    async System.Threading.Tasks.Task CorrerVigilia()
    {
        try { await Vigilar(); }
        catch (Exception ex)
        {
            Apunta($"[VIG] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("la vigilia", ex);
        }
        finally
        {
            // Se cierra sola. Una medida que te deja una segunda barra encendida es una
            // medida que tienes que ir a apagar, y ya pasó una vez: un binario de fase que se
            // quedó corriendo diez minutos como barra normal.
            Application.Current.Shutdown(0);
        }
    }

    async System.Threading.Tasks.Task Vigilar()
    {
        var muestras = new List<DiagVigilia.Muestra>();
        var proceso = Process.GetCurrentProcess();
        var reloj = Stopwatch.StartNew();

        Apunta("=== vigilia: la barra funcionando, midiéndose ===");
        Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
        Apunta($"configuración en solo lectura={Config.SoloLectura}");
        Apunta($"duración: {DiagVigilia.Minutos:0.#} min, una muestra cada " +
               $"{DiagVigilia.Cada.TotalSeconds:0} s");
        Apunta($"ventanas ahora: {GestorVentanas.Cuantas}  escritorios: {Escritorios.Espacios().Count}");
        Apunta($"NubeHypr: activo={_cfg.NubeHypr.Activo} modo={_cfg.NubeHypr.Modo}");
        Apunta("");

        // Un respiro antes de la primera muestra: durante el arranque se llenan las cachés de
        // iconos y se montan las zonas, y medir ahí daría una línea base que no es la de
        // régimen. No se descarta el dato —se apunta igual— pero el veredicto arranca más
        // tarde a propósito.
        await System.Threading.Tasks.Task.Delay(5000);

        var fin = TimeSpan.FromMinutes(DiagVigilia.Minutos);
        while (reloj.Elapsed < fin)
        {
            proceso.Refresh();
            var m = new DiagVigilia.Muestra(
                reloj.Elapsed,
                proceso.PrivateMemorySize64,
                GC.GetTotalMemory(false),
                proceso.TotalProcessorTime,
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                proceso.HandleCount,
                RemontajesDeZonas);

            muestras.Add(m);
            Apunta("[VIG] " + m);

            var queda = fin - reloj.Elapsed;
            if (queda <= TimeSpan.Zero) break;
            await System.Threading.Tasks.Task.Delay(
                queda < DiagVigilia.Cada ? queda : DiagVigilia.Cada);
        }

        Apunta("");
        Apunta("[VIG] " + DiagVigilia.Veredicto(muestras));

        // ---------- Y los remontajes, que es lo que de verdad se vigila ----------
        //
        // El número en sí no dice nada: al arrancar se remonta, y cada cambio de tema o de
        // módulos remonta otra vez, y eso está bien. Lo que importa es que **no suba solo**
        // mientras la barra está quieta.
        if (muestras.Count >= 2)
        {
            long subida = muestras[^1].Repintados - muestras[0].Repintados;
            Apunta(subida == 0
                ? $"[VIG] zonas remontadas durante la vigilia: 0 (quedan en " +
                  $"{muestras[^1].Repintados} desde que arrancó). Es lo que tiene que pasar " +
                  "con la barra quieta."
                : $"[VIG] ¡OJO! las zonas se han remontado {subida} vez/veces con la barra " +
                  "quieta. Remontar es tirar los widgets y rehacerlos: si pasa solo, algo " +
                  "está reconstruyendo en bucle.");
        }

        Apunta("[VIG] lo que esto NO mide: con TUS cien ventanas abiertas y la CPU a tope. " +
               "Esta medida es con lo que haya ahora en la máquina.");
        Apunta("[VIG] la configuración no se ha tocado: es una bandera, y con bandera " +
               "`Config.PuedeEscribir` es falso.");
    }
}
