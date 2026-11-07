using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;

namespace NubeBar;

// El lado que actúa de `Panico`: apuntar los fallos y, si se pone feo, salir de verdad.
//
// Ver `Panico.cs` para cuándo se considera grave. Aquí solo está lo que no se puede probar
// sin una ventana delante: cerrar la aplicación y la red de seguridad por si cerrarla falla.
public partial class MainWindow
{
    static readonly List<Panico.Apunte> _fallos = new();
    static readonly object _candado = new();
    static int _rindiendose;     // 0/1 con Interlocked: dos hilos pueden fallar a la vez

    // ---------- Apuntar un fallo ----------
    //
    // `donde` es la etiqueta que agrupa: el nombre del módulo, de la sección o de la
    // operación. NO el mensaje de la excepción — ése cambia con los datos, y entonces cinco
    // repeticiones del mismo fallo parecerían cinco fallos distintos y no saltaría nunca.
    internal static void Apuntar(Panico.Clase que, string donde, object? error)
    {
        try
        {
            Log.Error($"[{que}] {donde}", error as Exception
                      ?? new Exception(error?.ToString() ?? "desconocido"));

            bool rendirse;
            lock (_candado)
            {
                _fallos.Add(new Panico.Apunte(que, donde, DateTime.Now));
                // La lista no crece para siempre: una barra encendida toda la semana con un
                // módulo tonto acumularía miles de apuntes que ya no decide nada.
                if (_fallos.Count > 200) _fallos.RemoveRange(0, _fallos.Count - 200);
                rendirse = Panico.HayQueRendirse(_fallos, DateTime.Now);
            }

            if (rendirse) Rendirse(Panico.Motivo(SnapshotDeFallos(), DateTime.Now));
        }
        catch
        {
            // Si hasta apuntar falla, lo último que se puede hacer por el usuario es
            // devolverle la barra. Sin log: el log es justo lo que podría estar roto.
            try { Nativo.MostrarBarraWindows(true); } catch { }
        }
    }

    // ---------- De dónde viene el fallo ----------
    //
    // La etiqueta que agrupa las repeticiones. Se saca del primer marco de la pila que sea
    // NUESTRO: el mensaje de la excepción no sirve —cambia con los datos, y entonces cinco
    // caídas del mismo sitio parecerían cinco sitios— y el tipo tampoco, porque media
    // aplicación lanza `NullReferenceException`.
    //
    // Si no hay pila que leer, queda el tipo. Peor agrupación, pero mejor que nada: con la
    // cadena vacía `HayQueRendirse` ni siquiera contaría las repeticiones.
    internal static string DondeFallo(Exception? ex)
    {
        try
        {
            if (ex == null) return "desconocido";
            var traza = new System.Diagnostics.StackTrace(ex, false);
            for (int i = 0; i < traza.FrameCount; i++)
            {
                var m = traza.GetFrame(i)?.GetMethod();
                if (m?.DeclaringType?.Namespace?.StartsWith("NubeBar") != true) continue;
                return $"{m.DeclaringType.Name}.{m.Name}";
            }
            return ex.GetType().Name;
        }
        catch { return "desconocido"; }
    }

    static List<Panico.Apunte> SnapshotDeFallos()
    {
        lock (_candado) return new List<Panico.Apunte>(_fallos);
    }

    // ---------- Rendirse ----------
    //
    // Lo que pidió el usuario, con sus palabras: «que estos activen un exit que haga que al
    // usuario se le active la barra de tareas normal, igual que salirse de NubeBar».
    //
    // O sea: NO basta con enseñar la barra de Windows, que es lo que hacía el manejador de
    // antes. Eso deja NubeBar a medio morir — con la bandeja secuestrada, los ganchos de
    // ventanas puestos y la reserva de pantalla sin soltar. Hay que salir de verdad, que es
    // lo que suelta todo eso en `Closed` y en `OnExit`.
    //
    // ---------- Y con red, porque la salida limpia también puede fallar ----------
    //
    // Estamos aquí precisamente porque algo está roto, así que `Shutdown()` puede colgarse.
    // Un hilo aparte vigila: si en cuatro segundos no hemos terminado, enseña la barra de
    // Windows a la fuerza y mata el proceso.
    //
    // El hilo es de FONDO (`IsBackground`) para no impedir él mismo que el proceso termine, y
    // va por `Thread` y no por `Task`: si el problema fuese el planificador de tareas, una
    // tarea podría no llegar a ejecutarse nunca.
    internal static void Rendirse(string motivo)
    {
        if (Interlocked.Exchange(ref _rindiendose, 1) != 0) return;   // una sola vez

        try { Log.Info($"PÁNICO: {motivo}. Se cierra NubeBar y se devuelve la barra de Windows."); }
        catch { }

        new Thread(() =>
        {
            Thread.Sleep(Panico.EsperaDeLaSalidaLimpia);
            try
            {
                Log.Info("PÁNICO: la salida limpia no terminó a tiempo; se fuerza.");
                Nativo.MostrarBarraWindows(true);
            }
            catch { }
            // `Kill` sobre el propio proceso: `Environment.Exit` pasa por `OnExit`, que es
            // justo lo que no ha terminado. Dar otra vuelta por ahí volvería a colgarse.
            try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { }
        })
        { IsBackground = true, Name = "panico-tope" }.Start();

        try
        {
            // La salida de siempre, en el hilo de la interfaz. `Shutdown` dispara `Closed` en
            // cada isla —que para los relojes, suelta los ganchos, devuelve la bandeja y
            // libera la reserva— y después `OnExit`.
            var app = Application.Current;
            if (app == null) { Nativo.MostrarBarraWindows(true); return; }
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { app.Shutdown(0); }
                catch (Exception ex)
                {
                    try { Log.Error("PÁNICO: fallo al cerrar", ex); } catch { }
                    try { Nativo.MostrarBarraWindows(true); } catch { }
                }
            }));
        }
        catch
        {
            try { Nativo.MostrarBarraWindows(true); } catch { }
        }
    }
}
