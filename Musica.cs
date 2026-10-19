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

    // ---------- Mandos ----------
    public static async void Anterior()
    {
        try { await Gestor(); if (Sesion() is { } s) await s.TrySkipPreviousAsync(); }
        catch (Exception ex) { Log.Error("canción anterior", ex); }
    }

    public static async void Siguiente()
    {
        try { await Gestor(); if (Sesion() is { } s) await s.TrySkipNextAsync(); }
        catch (Exception ex) { Log.Error("canción siguiente", ex); }
    }

    public static async void PlayPausa()
    {
        try { await Gestor(); if (Sesion() is { } s) await s.TryTogglePlayPauseAsync(); }
        catch (Exception ex) { Log.Error("play/pausa", ex); }
    }
}
