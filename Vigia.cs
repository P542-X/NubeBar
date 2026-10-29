using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace NubeBar;

// El vigía de cuelgues: si la interfaz deja de responder, lo apunta y guarda una foto.
//
// ---------- De dónde sale ----------
//
// El 28/10 NubeBar se colgó: Windows la dio por «no responde» y la cerró. El guardián
// devolvió la barra, como debe, pero de lo que pasó no quedó NADA: ni una línea en el log
// —el hilo que escribe es el que estaba colgado— ni una excepción, porque un cuelgue no
// lanza ninguna. El informe de Windows solo dice «AppHangB1». Sin saber en qué se quedó
// esperando, arreglarlo era adivinar.
//
// ---------- Cómo ----------
//
// Un hilo aparte le manda un recado al hilo de la interfaz cada segundo. Si en
// `Umbral` no lo ha atendido, la interfaz está colgada: se apunta en el log y se guarda un
// volcado de este proceso (ver `Fotografiar`). Del volcado se saca después
// la pila exacta del hilo colgado (`dotnet-dump analyze` → `clrstack`). Si al rato vuelve
// a responder, también se apunta: un tirón de seis segundos es otra cosa que un cuelgue.
//
// Una foto por cuelgue, no una por segundo, y solo se guarda la última: pesa lo que la
// memoria del proceso.
static class Vigia
{
    public static readonly TimeSpan Umbral = TimeSpan.FromSeconds(5);
    const int FotosQueSeGuardan = 1;

    static Dispatcher? _ui;
    static long _ultimaRespuesta;        // Stopwatch.GetTimestamp() del último recado atendido
    static Thread? _hilo;

    public static string Carpeta => Path.Combine(Config.Carpeta, "cuelgues");

    public static void Empezar(Dispatcher ui)
    {
        if (_hilo != null) return;
        _ui = ui;
        _ultimaRespuesta = Stopwatch.GetTimestamp();
        _hilo = new Thread(Vigilar) { IsBackground = true, Name = "NubeBar vigía de cuelgues" };
        _hilo.Start();
        Log.Info($"vigía de cuelgues en marcha (umbral {Umbral.TotalSeconds:0} s)");
    }

    static void Vigilar()
    {
        bool colgada = false;
        while (true)
        {
            try
            {
                long antes = Stopwatch.GetTimestamp();
                Thread.Sleep(1000);
                var ui = _ui;
                if (ui == null || ui.HasShutdownStarted) return;

                // Si el propio vigía ha dormido mucho más de un segundo, el que estaba parado
                // era el sistema entero —suspensión, hibernación—, no la interfaz. Se empieza
                // a contar de nuevo en vez de dar por colgado lo que solo estaba dormido.
                if (Stopwatch.GetElapsedTime(antes) > TimeSpan.FromSeconds(3))
                {
                    Interlocked.Exchange(ref _ultimaRespuesta, Stopwatch.GetTimestamp());
                    continue;
                }

                // Prioridad Normal: por debajo de lo que pinta y de la entrada, así que si
                // esto no se atiende es que de verdad no se atiende nada.
                ui.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    Interlocked.Exchange(ref _ultimaRespuesta, Stopwatch.GetTimestamp())));

                var sin = Stopwatch.GetElapsedTime(Interlocked.Read(ref _ultimaRespuesta));
                if (!colgada && sin >= Umbral)
                {
                    colgada = true;
                    Log.Info($"VIGÍA: la interfaz lleva {sin.TotalSeconds:0.0} s sin responder; se guarda una foto del proceso");
                    Fotografiar();
                }
                else if (colgada && sin < TimeSpan.FromSeconds(2))
                {
                    colgada = false;
                    Log.Info("VIGÍA: la interfaz ha vuelto a responder");
                }
            }
            catch (Exception ex)
            {
                try { Log.Error("vigía de cuelgues", ex); } catch { }
            }
        }
    }

    // ---------- La foto, con la API de Windows ----------
    //
    // Y no con el `createdump` del runtime: el exe de la Release es autónomo y de un solo
    // archivo, y ese formato no lo trae. `MiniDumpWriteDump` está en todos los Windows.
    //
    // Con la memoria ENTERA. Probado contra NubeBar en marcha: con solo la memoria privada
    // —la mitad de tamaño— Windows contesta ERROR_PARTIAL_COPY y deja un fichero a medias,
    // también pidiéndole que se salte lo ilegible. Entera sale bien en unos 3 s y pesa lo
    // que el proceso (unos 740 MB), por eso se guarda solo la última.
    [DllImport("dbghelp.dll", SetLastError = true)]
    static extern bool MiniDumpWriteDump(IntPtr proceso, int pid, Microsoft.Win32.SafeHandles.SafeFileHandle fichero,
                                         uint tipo, IntPtr excepcion, IntPtr usuario, IntPtr retrollamada);

    const uint ConMemoriaEntera = 0x2, ConHandles = 0x4, ConModulosDescargados = 0x20,
               ConMapaDeMemoria = 0x800, ConHilos = 0x1000;

    static void Fotografiar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            foreach (var vieja in new DirectoryInfo(Carpeta).GetFiles("cuelgue-*.dmp")
                         .OrderByDescending(f => f.CreationTimeUtc).Skip(FotosQueSeGuardan - 1))
            {
                try { vieja.Delete(); } catch { }
            }

            var ruta = Path.Combine(Carpeta, $"cuelgue-{DateTime.Now:yyyyMMdd-HHmmss}.dmp");
            bool ok;
            using (var yo = Process.GetCurrentProcess())
            using (var fs = new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                ok = MiniDumpWriteDump(yo.Handle, yo.Id, fs.SafeFileHandle,
                    ConMemoriaEntera | ConHandles | ConModulosDescargados |
                    ConMapaDeMemoria | ConHilos,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            if (ok)
                Log.Info($"VIGÍA: foto guardada en {ruta} ({new FileInfo(ruta).Length / 1048576} MB)");
            else
            {
                Log.Info($"VIGÍA: no se pudo guardar la foto (error {Marshal.GetLastWin32Error()})");
                try { File.Delete(ruta); } catch { }
            }
        }
        catch (Exception ex) { Log.Error("guardar la foto del cuelgue", ex); }
    }
}
