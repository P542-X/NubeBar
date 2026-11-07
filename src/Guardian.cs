using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace NubeBar;

// El guardián: el mismo ejecutable lanzado con --guardian, sin ninguna ventana.
//
// Por qué existe: NubeBar toca cosas del shell que son de TODO el sistema (esconde
// las barras de tareas, las pone en "ocultar automáticamente" y, a partir de la
// fase 3, esconde el aviso de volumen de Windows). Si NubeBar se cierra bien, las
// devuelve ella misma. Pero si la matas desde el Administrador de tareas, o se
// cuelga, o se va la luz al proceso, nadie las devuelve y te quedas sin barra.
//
// Guardar el estado en disco no basta: ese apaño solo arregla las cosas la próxima
// vez que arrancas NubeBar, y si no vuelves a abrirla te quedas igual. Por eso hace
// falta un proceso aparte que esté esperando a que el principal muera.
//
// Cómo sabe si fue un cierre bueno o malo: NubeBar crea un evento con nombre antes
// de lanzarlo. Al cerrarse bien lo señala ("ya está, puedes irte"). El guardián
// espera a lo que pase primero, ese evento o la muerte del proceso; si gana la
// muerte, restaura. Así un cierre limpio no hace trabajo doble y un cierre feo
// queda cubierto.
static class Guardian
{
    public const string Bandera = "--guardian";

    static string NombreEvento(int pid) => $@"Local\NubeBar.Guardian.{pid}";

    static EventWaitHandle? _fin;
    static Process? _proceso;

    // ---------- Lado de NubeBar ----------
    public static void Lanzar()
    {
        if (_fin != null) return;                       // ya hay uno
        try
        {
            int pid = Environment.ProcessId;
            // El evento se crea AQUÍ, antes de lanzarlo: si lo creara el guardián
            // habría un hueco en el que un cierre limpio no tendría a quién avisar.
            _fin = new EventWaitHandle(false, EventResetMode.ManualReset, NombreEvento(pid));

            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) { Log.Info("guardián: no sé cuál es mi ejecutable"); return; }

            _proceso = Process.Start(new ProcessStartInfo(exe, $"{Bandera} {pid}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Log.Info($"guardián lanzado (pid {_proceso?.Id}) vigilando a {pid}");
        }
        catch (Exception ex) { Log.Error("lanzar el guardián", ex); }
    }

    // Cierre limpio: NubeBar ya ha devuelto todo, el guardián sobra
    public static void Despedir()
    {
        try { _fin?.Set(); } catch { }
    }

    // ---------- Lado del guardián ----------
    public static void Vigilar(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int pid)) return;

        EventWaitHandle fin;
        try { fin = EventWaitHandle.OpenExisting(NombreEvento(pid)); }
        catch { fin = new EventWaitHandle(false, EventResetMode.ManualReset, NombreEvento(pid)); }

        Process padre;
        try { padre = Process.GetProcessById(pid); }
        catch { return; }                                // ya no estaba: nada que vigilar

        Log.Info($"guardián en marcha, vigilando el pid {pid}");

        // Esperar a los dos a la vez. El handle del proceso se señala al morir;
        // envolviéndolo en un WaitHandle se puede esperar junto al evento.
        using var muerte = new ManualResetEvent(false);
        muerte.SafeWaitHandle = new SafeWaitHandle(padre.Handle, false);

        int cual;
        try { cual = WaitHandle.WaitAny(new WaitHandle[] { fin, muerte }); }
        catch (Exception ex) { Log.Error("guardián esperando", ex); return; }
        // El SafeWaitHandle se creó con ownsHandle:false, así que no es dueño del
        // handle: si el GC finaliza "padre" durante la espera, Process lo cierra y el
        // WaitAny se queda esperando sobre un handle muerto. Justo el guardián, que
        // existe para devolverte la barra de tareas, es el que no debe fallar.
        GC.KeepAlive(padre);

        // Si los dos están señalados gana el evento, pero se vuelve a mirar por si
        // la señal llegó justo entre medias: un cierre limpio no debe tocar nada.
        if (cual == 0 || fin.WaitOne(0))
        {
            Log.Info("guardián: cierre limpio, me voy sin tocar nada");
            return;
        }

        Log.Info("guardián: NubeBar ha muerto sin avisar, devuelvo el shell");
        RestaurarElShell();
    }

    // Todo lo que NubeBar le cambia al sistema y hay que devolver sí o sí.
    // El orden importa: primero que las barras se VEAN (es instantáneo) y después
    // el "ocultar automáticamente", que tarda más porque Explorer recalcula el área
    // de trabajo y se lo cuenta a todas las ventanas.
    public static void RestaurarElShell()
    {
        var reloj = Stopwatch.StartNew();
        try { Nativo.MostrarBarraWindows(true); }
        catch (Exception ex) { Log.Error("guardián devolviendo las barras", ex); }

        // Fase 3: aquí irá también devolver el aviso de volumen de Windows.

        Log.Info($"guardián: shell devuelto en {reloj.ElapsedMilliseconds} ms · " +
                 $"barra visible={Nativo.BarraWindowsVisible()}");
    }
}
