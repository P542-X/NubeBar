using System;
using System.Threading;

namespace NubeBar;

// Una sola NubeBar a la vez.
//
// Sin esto, abrir el ejecutable dos veces daba dos juegos de islas, dos ganchos
// de teclado peleándose por la tecla Windows, dos servicios de bandeja
// disputándose el orden Z y dos procesos escondiendo y mostrando la barra de
// tareas a la vez. El desenlace típico era quedarse sin barra.
//
// El cerrojo es un mutex con nombre, que Windows limpia solo aunque el proceso
// muera a lo bruto: no hay forma de quedarse bloqueado por un cierre feo.
// Lleva "Local\" delante a propósito: el ámbito es la sesión, así que dos
// usuarios distintos del mismo equipo sí pueden tener cada uno la suya.
static class Instancia
{
    const string Nombre = @"Local\NubeBar.UnaSola";

    // "Cierra, por favor": un evento con nombre que la NubeBar en marcha está
    // escuchando. Lo usa "NubeBar.exe --salir", que es como el instalador la quita
    // de en medio antes de tocar su exe.
    //
    // Por qué hace falta y no basta con matarla: NubeBar esconde la barra de tareas
    // de Windows mientras vive y la devuelve AL CERRARSE. Un taskkill /F deja al
    // usuario sin ninguna barra (de eso va el guardián). Y un taskkill sin /F no
    // vale: manda WM_CLOSE a las ventanas "normales" del proceso, y las islas son
    // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, así que no encuentra ninguna y no cierra
    // nada. Comprobado: el instalador se quedaba sin cerrarla y la barra de Windows
    // no volvía.
    const string NombreSalida = @"Local\NubeBar.Salir";

    static Mutex? _cerrojo;
    static EventWaitHandle? _salida;
    static RegisteredWaitHandle? _espera;

    public static bool SoyLaPrimera()
    {
        try
        {
            _cerrojo = new Mutex(true, Nombre, out bool creado);

            // Si otra la creó pero murió sin soltarla, el mutex queda "abandonado"
            // y WaitOne lo avisa con esa excepción: ahí el dueño somos nosotros.
            if (!creado)
            {
                try { creado = _cerrojo.WaitOne(TimeSpan.Zero); }
                catch (AbandonedMutexException) { creado = true; }
            }
            return creado;
        }
        catch (Exception ex)
        {
            // Si el cerrojo falla por lo que sea, más vale arrancar que no arrancar
            Log.Error("comprobar si ya hay otra NubeBar", ex);
            return true;
        }
    }

    // La NubeBar de verdad se queda escuchando: cuando alguien señale el evento,
    // "cerrar" se ejecuta en el hilo que se le diga (el de la interfaz).
    public static void EscucharSalida(Action cerrar)
    {
        try
        {
            _salida = new EventWaitHandle(false, EventResetMode.ManualReset, NombreSalida);
            _espera = ThreadPool.RegisterWaitForSingleObject(
                _salida, (_, _) => cerrar(), null, Timeout.Infinite, executeOnlyOnce: true);
        }
        catch (Exception ex) { Log.Error("escuchar la petición de salida", ex); }
    }

    // Avisa a la que esté en marcha. false = no había ninguna escuchando.
    public static bool PedirSalida()
    {
        try
        {
            using var e = EventWaitHandle.OpenExisting(NombreSalida);
            e.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
        catch (Exception ex) { Log.Error("pedir la salida", ex); return false; }
    }

    public static void Soltar()
    {
        try { _espera?.Unregister(null); } catch { }
        _espera = null;
        _salida?.Dispose();
        _salida = null;
        try { _cerrojo?.ReleaseMutex(); } catch { }
        _cerrojo?.Dispose();
        _cerrojo = null;
    }
}
