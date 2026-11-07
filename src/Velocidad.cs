using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NubeBar;

// Test de velocidad contra speed.cloudflare.com, el mismo servidor que usa la web
// de Cloudflare: no pide clave ni cuenta, y sirve desde el nodo más cercano.
//
// Ping: la mejor de 8 peticiones vacías (la mejor y no la media, porque la primera
// abre la conexión TLS y alguna más cae en un mal momento). Bajada y subida: 4
// conexiones en paralelo durante unos segundos, que es lo que hace falta para
// llenar una fibra; con una sola, una línea de 600 Mb salía en 200. El primer
// segundo no cuenta: es el arranque de TCP.
//
// Es estático y sigue corriendo aunque cierres el panel: al volver a abrirlo se
// ve por dónde va o lo que salió.
static class Velocidad
{
    public enum Fase { Parado, Ping, Bajada, Subida }

    public sealed record Resultado(double PingMs, double BajadaMbps, double SubidaMbps, DateTime Cuando, string? Error);

    public static Fase FaseActual { get; private set; }
    public static double PingMs { get; private set; } = -1;
    public static double BajadaMbps { get; private set; } = -1;
    public static double SubidaMbps { get; private set; } = -1;
    public static Resultado? Ultimo { get; private set; }
    public static bool EnCurso => FaseActual != Fase.Parado;

    // Salta (desde un hilo de fondo) cada vez que cambia una cifra o la fase
    public static event Action? Cambio;

    const string Servidor = "https://speed.cloudflare.com/";
    const int Conexiones = 4;
    static readonly TimeSpan Duracion = TimeSpan.FromSeconds(7);
    static readonly TimeSpan Arranque = TimeSpan.FromSeconds(1);

    static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        // Una conexión TCP por petición en paralelo, no todas metidas en una HTTP/2
        MaxConnectionsPerServer = Conexiones * 2,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
    };

    static void Avisar() { try { Cambio?.Invoke(); } catch { } }

    public static async Task Medir()
    {
        if (EnCurso) return;
        PingMs = BajadaMbps = SubidaMbps = -1;
        string? error = null;
        try
        {
            FaseActual = Fase.Ping; Avisar();
            PingMs = await MedirPing();
            Avisar();

            FaseActual = Fase.Bajada; Avisar();
            BajadaMbps = await MedirCaudal(Bajar);
            Avisar();

            FaseActual = Fase.Subida; Avisar();
            SubidaMbps = await MedirCaudal(Subir);
        }
        catch (Exception ex)
        {
            error = ex is HttpRequestException or TaskCanceledException or IOException
                ? "No se llega al servidor del test"
                : "El test ha fallado";
            Log.Error("test de velocidad", ex);
        }
        FaseActual = Fase.Parado;
        Ultimo = new Resultado(PingMs, BajadaMbps, SubidaMbps, DateTime.Now, error);
        Log.Info($"test de velocidad: ping {PingMs:0} ms, bajada {BajadaMbps:0.0} Mb/s, subida {SubidaMbps:0.0} Mb/s{(error != null ? " · " + error : "")}");
        Avisar();
    }

    static async Task<double> MedirPing()
    {
        double mejor = double.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var reloj = Stopwatch.StartNew();
            using var r = await _http.GetAsync(Servidor + "__down?bytes=0", HttpCompletionOption.ResponseHeadersRead, cts.Token);
            reloj.Stop();
            if (i > 0) mejor = Math.Min(mejor, reloj.Elapsed.TotalMilliseconds);   // la 1ª abre TLS
            PingMs = mejor == double.MaxValue ? -1 : mejor;
        }
        return mejor;
    }

    // Lanza N trabajadores que van sumando bytes a un contador común durante
    // "Duracion", y va publicando la cifra en vivo cada cuarto de segundo
    static async Task<double> MedirCaudal(Func<CancellationToken, Action<long>, Task> trabajador)
    {
        long bytes = 0;
        using var fin = new CancellationTokenSource(Duracion);
        var reloj = Stopwatch.StartNew();
        long bytesAlArrancar = -1;
        double mbps = 0;

        void Sumar(long n) => Interlocked.Add(ref bytes, n);

        async Task Bucle()
        {
            while (!fin.IsCancellationRequested)
            {
                try { await trabajador(fin.Token, Sumar); }
                catch (OperationCanceledException) { return; }
                // Al cortar a la hora, el socket a medias a veces sale como IOException
                catch when (fin.IsCancellationRequested) { return; }
            }
        }

        var tareas = Enumerable.Range(0, Conexiones).Select(_ => Task.Run(Bucle)).ToList();
        double Calcular()
        {
            long ahora = Interlocked.Read(ref bytes);
            var t = reloj.Elapsed;
            if (t < Arranque) return ahora * 8 / t.TotalSeconds / 1e6;
            if (bytesAlArrancar < 0) bytesAlArrancar = ahora;
            double seg = (t - Arranque).TotalSeconds;
            return seg <= 0.05 ? mbps : (ahora - bytesAlArrancar) * 8 / seg / 1e6;
        }

        while (!tareas.All(t => t.IsCompleted))
        {
            await Task.WhenAny(Task.WhenAll(tareas), Task.Delay(250));
            if (reloj.Elapsed >= Arranque && bytesAlArrancar < 0) bytesAlArrancar = Interlocked.Read(ref bytes);
            mbps = Calcular();
            if (FaseActual == Fase.Bajada) BajadaMbps = mbps; else SubidaMbps = mbps;
            Avisar();
        }
        // Si no ha pasado ni un byte, es que algo ha fallado de verdad: que se vea el error
        if (Interlocked.Read(ref bytes) == 0) await Task.WhenAll(tareas);
        return mbps;
    }

    static async Task Bajar(CancellationToken ct, Action<long> sumar)
    {
        using var r = await _http.GetAsync(Servidor + "__down?bytes=50000000", HttpCompletionOption.ResponseHeadersRead, ct);
        r.EnsureSuccessStatusCode();
        await using var s = await r.Content.ReadAsStreamAsync(ct);
        var buf = new byte[64 * 1024];
        int n;
        while ((n = await s.ReadAsync(buf, ct)) > 0) sumar(n);
    }

    static async Task Subir(CancellationToken ct, Action<long> sumar)
    {
        using var r = await _http.PostAsync(Servidor + "__up", new Contado(10_000_000, sumar), ct);
    }

    // Un cuerpo de subida que va contando lo que de verdad ya ha salido por el
    // socket, para que la cifra se mueva en vivo y no a saltos de 10 MB
    sealed class Contado(long total, Action<long> sumar) : HttpContent
    {
        static readonly byte[] Trozo = new byte[64 * 1024];

        protected override async Task SerializeToStreamAsync(Stream s, TransportContext? c, CancellationToken ct)
        {
            long falta = total;
            while (falta > 0)
            {
                int n = (int)Math.Min(Trozo.Length, falta);
                await s.WriteAsync(Trozo.AsMemory(0, n), ct);
                sumar(n);
                falta -= n;
            }
        }

        protected override Task SerializeToStreamAsync(Stream s, TransportContext? c) =>
            SerializeToStreamAsync(s, c, CancellationToken.None);

        protected override bool TryComputeLength(out long largo) { largo = total; return true; }
    }
}
