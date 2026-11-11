using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NubeBar;

public sealed record EstadoMusica(
    string Titulo,
    string Artista,
    bool Sonando,
    TimeSpan Posicion,
    TimeSpan Duracion,
    string AppId)
{
    public double Avance => Duracion.TotalSeconds > 0
        ? Math.Clamp(Posicion.TotalSeconds / Duracion.TotalSeconds, 0, 1)
        : 0;
}

// Lo que esté sonando en el sistema, venga de donde venga (Spotify, el navegador,
// un reproductor...). Windows lo expone en una sola sesión "la que manda".
public static class Musica
{
    static GlobalSystemMediaTransportControlsSessionManager? _gestor;
    static bool _intentado;

    // ================= ¿Lo que suena es Spotify? =================
    //
    // La sesión dice quién reproduce en `SourceAppUserModelId`. Para la aplicación de
    // escritorio es `Spotify.exe`; para la de la Microsoft Store, un identificador largo que
    // empieza por `SpotifyAB.SpotifyMusic`. Las dos llevan «spotify» dentro, así que se mira
    // por ahí en vez de escribir las dos cadenas y quedarse corto con la tercera.
    //
    // Y el navegador NO cuenta: `open.spotify.com` en Edge se anuncia como `msedge.exe`,
    // porque quien reproduce es el navegador. Que Spotify en el navegador no entre es
    // correcto y conviene que esté escrito, porque parece un fallo.
    //
    // Vivía en `Spotify.cs`, que era el fichero de su API. Esa API ya no está y esto sí:
    // no necesita cuenta, ni permiso, ni red — es una cadena que ya tenemos en la mano.
    public static bool EsSpotify(string? appId) =>
        appId != null && appId.Contains("spotify", StringComparison.OrdinalIgnoreCase);

    // La carátula se cachea por título: decodificarla en cada tick sería absurdo
    static string _claveCaratula = "";
    static BitmapImage? _caratula;

    public static BitmapImage? Caratula => _caratula;

    static async Task<GlobalSystemMediaTransportControlsSessionManager?> Gestor()
    {
        if (_gestor != null) return _gestor;
        if (_intentado) return null;
        _intentado = true;
        try { _gestor = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
        catch (Exception ex) { Log.Error("sesión de medios", ex); }
        return _gestor;
    }

    static GlobalSystemMediaTransportControlsSession? Sesion() => _gestor?.GetCurrentSession();

    // Devuelve null si no hay nada sonando ni en pausa
    public static async Task<EstadoMusica?> Leer()
    {
        try
        {
            var g = await Gestor();
            var s = g?.GetCurrentSession();
            if (s == null) return null;

            var info = s.GetPlaybackInfo();
            if (info == null) return null;

            var props = await s.TryGetMediaPropertiesAsync();
            if (props == null || string.IsNullOrWhiteSpace(props.Title)) return null;

            var tl = s.GetTimelineProperties();
            bool sonando = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            await CargarCaratula(props);

            return new EstadoMusica(
                props.Title ?? "",
                props.Artist ?? "",
                sonando,
                tl?.Position ?? TimeSpan.Zero,
                tl != null && tl.EndTime > tl.StartTime ? tl.EndTime - tl.StartTime : TimeSpan.Zero,
                s.SourceAppUserModelId ?? "");
        }
        catch (Exception ex)
        {
            Log.Error("leer la música", ex);
            return null;
        }
    }

    static async Task CargarCaratula(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        var clave = (props.Title ?? "") + "|" + (props.Artist ?? "");
        if (clave == _claveCaratula) return;
        _claveCaratula = clave;
        _caratula = null;

        if (props.Thumbnail == null) return;
        try
        {
            using var flujo = await props.Thumbnail.OpenReadAsync();
            using var memoria = new MemoryStream();
            await flujo.AsStreamForRead().CopyToAsync(memoria);
            memoria.Position = 0;

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = memoria;
            img.DecodePixelWidth = 160;
            img.EndInit();
            img.Freeze();
            _caratula = img;
        }
        catch { _caratula = null; }
    }


    // ================= El contador de mutaciones =================
    //
    // ---------- Para qué ----------
    //
    // "Al abrir el Centro de control se detiene la reproducción". Hay dos explicaciones
    // posibles y se arreglan de formas opuestas:
    //
    //  1. NubeBar PIDE una pausa en el camino de abrir el panel (un manejador que se
    //     dispara al construir, un `Toggle` que se invoca al pintarse, una tecla
    //     multimedia inyectada). Se arregla quitando esa llamada.
    //  2. NubeBar no pide nada y la reproducción se corta porque el reproductor reacciona
    //     a la VENTANA (pierde el primer plano, se queda tapado, sale de pantalla
    //     completa). Se arregla cambiando la ventana, no el código del panel.
    //
    // Sin esto no se puede distinguir, y las dos se parecen muchísimo desde el sofá. Con
    // esto: si la reproducción se para y el contador NO ha subido, es el caso 2.
    //
    // Cuenta las TRES únicas puertas por las que NubeBar puede cambiar lo que suena.
    public static long Mutaciones;
    public static string UltimaMutacion = "ninguna";


    // ================= El guardia: construir un panel no cambia nada =================
    //
    // ---------- La regla ----------
    //
    // Montar un panel es una operación de LECTURA. Lo único que puede cambiar lo que
    // suena es que tú pulses un mando. Esto lo convierte en algo que se cumple de verdad
    // en vez de en una buena intención: mientras `Construyendo` está puesto, las tres
    // puertas que mutan se cierran, se cuentan y se apuntan con su nombre.
    //
    // ---------- Por qué un guardia y no una revisión ----------
    //
    // Porque la revisión ya la hice —`CrearControl` y `MediaCard` solo leen, los mandos
    // son lambdas que nadie invoca al construir— y el fallo seguía descrito. Un guardia
    // sirve para las dos salidas posibles: si algo llama a pausa por un camino que no vi,
    // lo BLOQUEA y deja su nombre en el log; y si nunca se dispara, eso es la prueba de
    // que la pausa no sale de NubeBar y hay que mirar la ventana.
    //
    // No es un `catch` vacío: no se tapa nada, se apunta todo.
    public static bool Construyendo;
    public static long Rechazadas;
    public static string UltimaRechazada = "ninguna";

    internal static bool Permitido(string que)
    {
        if (!Construyendo) return true;
        Rechazadas++;
        UltimaRechazada = $"{DateTime.Now:HH:mm:ss.fff} {que}";
        Log.Info($"multimedia: se IGNORA «{que}»: se está montando un panel, " +
                 $"y montar solo lee (rechazo nº {Rechazadas})");
        return false;
    }
    static void Apuntar(string que)
    {
        Mutaciones++;
        UltimaMutacion = $"{DateTime.Now:HH:mm:ss.fff} {que}";
        Log.Info($"multimedia: NubeBar pide «{que}» (mutación nº {Mutaciones})");
    }
    // ---------- Mandos ----------
    public static async void Anterior()
    {
        if (!Permitido("anterior")) return;
        Apuntar("anterior");
        try { await Gestor(); if (Sesion() is { } s) await s.TrySkipPreviousAsync(); }
        catch (Exception ex) { Log.Error("canción anterior", ex); }
    }

    public static async void Siguiente()
    {
        if (!Permitido("siguiente")) return;
        Apuntar("siguiente");
        try { await Gestor(); if (Sesion() is { } s) await s.TrySkipNextAsync(); }
        catch (Exception ex) { Log.Error("canción siguiente", ex); }
    }

    public static async void PlayPausa()
    {
        if (!Permitido("play/pausa")) return;
        Apuntar("play/pausa");
        try { await Gestor(); if (Sesion() is { } s) await s.TryTogglePlayPauseAsync(); }
        catch (Exception ex) { Log.Error("play/pausa", ex); }
    }
}
