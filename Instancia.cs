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

    static Mutex? _cerrojo;

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

    public static void Soltar()
    {
        try { _cerrojo?.ReleaseMutex(); } catch { }
        _cerrojo?.Dispose();
        _cerrojo = null;
    }
}
