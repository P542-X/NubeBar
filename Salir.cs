using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace NubeBar;

// "NubeBar.exe --salir": cerrar la NubeBar que esté en marcha, desde fuera.
//
// Lo pide el instalador antes de reemplazar el exe o de desinstalarlo, y vale
// igual para un atajo o un script.
//
// Lo importante no es matar el proceso, es que se cierre BIEN: NubeBar esconde la
// barra de tareas de Windows mientras vive y la devuelve en su cierre. Por eso esto
// no es "taskkill con otro nombre" sino tres intentos, de más fino a más bruto, y
// el último se encarga a mano de devolver las barras.
static class Salir
{
    public const string Bandera = "--salir";

    // Lo que tarda en cerrarse del todo: su cierre espera hasta 5 s a que la cola
    // del shell termine de devolver las barras, así que hay que darle más que eso
    static readonly TimeSpan Paciencia = TimeSpan.FromSeconds(8);

    public static void Pedir()
    {
        // 1. Por las buenas: el evento con nombre que escucha la que está en marcha
        if (!Instancia.PedirSalida())
        {
            Log.Info("--salir: no hay ninguna NubeBar escuchando");
            // Aun así puede haber quedado una a medias de una versión vieja, o las
            // barras escondidas de un cierre feo: se comprueba y se arregla
            Rematar();
            return;
        }

        Log.Info("--salir: pedida la salida, esperando a que se cierre");
        if (Esperar())
        {
            Log.Info("--salir: cerrada por las buenas");
            return;
        }

        Log.Info($"--salir: sigue viva después de {Paciencia.TotalSeconds:0} s, se fuerza");
        Rematar();
    }

    // ¿Se han ido ya todas las NubeBar menos esta?
    static bool Esperar()
    {
        var hasta = DateTime.UtcNow + Paciencia;
        while (DateTime.UtcNow < hasta)
        {
            if (Otras().Length == 0) return true;
            Thread.Sleep(150);
        }
        return Otras().Length == 0;
    }

    // Las demás NubeBar: la principal y su guardián, que es el mismo ejecutable.
    // Esta de "--salir" también lo es, así que se quita de la lista.
    static Process[] Otras()
    {
        var yo = Environment.ProcessId;
        try
        {
            var nombre = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "NubeBar";
            return Process.GetProcessesByName(nombre).Where(p => p.Id != yo).ToArray();
        }
        catch (Exception ex) { Log.Error("buscar las otras NubeBar", ex); return Array.Empty<Process>(); }
    }

    // A lo bruto, y recogiendo lo que se rompa.
    //
    // El guardián se mata DESPUÉS y aparte a propósito: es quien devuelve las barras
    // cuando la principal muere de mala manera, y matándolos a la vez (un taskkill
    // /F /IM NubeBar.exe se lleva a los dos) se pierde esa red justo cuando hacía
    // falta. Pasó de verdad: el instalador mataba a los dos y la barra de Windows no
    // volvía. Aun así, al final se comprueba y se devuelven aquí mismo si hace falta,
    // que para eso esto es el último recurso.
    static void Rematar()
    {
        foreach (var p in Otras())
        {
            using (p)
            {
                try
                {
                    Log.Info($"--salir: matando el pid {p.Id}");
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch (Exception ex) { Log.Error($"matar el pid {p.Id}", ex); }
            }
            // Un respiro entre uno y otro: si el primero era la principal, el
            // guardián tiene ahora su momento de devolver las barras
            Thread.Sleep(1200);
        }

        // Y la comprobación que de verdad importa, pase lo que pase
        try
        {
            if (Nativo.BarraWindowsVisible())
            {
                Log.Info("--salir: la barra de Windows está en su sitio");
                return;
            }
            Log.Info("--salir: la barra de Windows seguía escondida, se devuelve");
            Nativo.MostrarBarraWindows(true);
        }
        catch (Exception ex) { Log.Error("devolver la barra de Windows", ex); }
    }
}
