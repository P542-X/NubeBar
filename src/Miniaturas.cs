using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace NubeBar;

// Las fotos de las ventanas para la vista general.
//
// ---------- Por qué no son miniaturas de DWM ----------
//
// `DwmRegisterThumbnail` es la vía oficial y la que usa la barra de tareas, y **aquí no
// sirve**: una miniatura de DWM se compone encima de la ventana de destino, y no se
// compone dentro de una ventana POR CAPAS. Todas las de NubeBar lo son —la isla es
// redondeada y translúcida, y eso en WPF es `AllowsTransparency`, que es
// `WS_EX_LAYERED`—. Usarlas significaría renunciar a la isla.
//
// Así que se le pide a la ventana que se dibuje: `PrintWindow` con
// PW_RENDERFULLCONTENT. Sale un mapa de bits de verdad que va DENTRO del WPF, se
// recorta con las esquinas redondeadas como cualquier otra imagen y se desplaza con el
// panel. Lo que no da es vídeo en directo, y para una vista general no hace falta.
//
// ---------- Y por qué por detrás ----------
//
// Cada foto cuesta entre 5 y 20 ms y hay que hacer una por ventana. Veinte ventanas son
// cuatro décimas de segundo, y hacerlo en el hilo de interfaz sería abrir el panel y
// esperar. Se piden todas a la vez por detrás y cada tarjeta se rellena cuando la suya
// llega: mientras tanto enseña su icono, que es lo que se ve si la foto no sale.
public static class Miniaturas
{
    // Cuánto vale una foto antes de volver a hacerla. Lo justo para que abrir, cerrar y
    // volver a abrir no cueste nada, y poco para que no enseñe algo de hace un rato.
    static readonly TimeSpan Caduca = TimeSpan.FromSeconds(4);

    sealed class Guardada
    {
        public ImageSource? Foto;
        public DateTime Cuando;
        public bool NoSePudo;      // se intentó y no salió: no se insiste cada vez
    }

    static readonly Dictionary<IntPtr, Guardada> _cache = new();

    public static ImageSource? Cacheada(IntPtr h) =>
        _cache.TryGetValue(h, out var g) && DateTime.Now - g.Cuando < Caduca ? g.Foto : null;

    // Pide la foto de una ventana. Si ya la hay y es reciente, llama a `cuandoEste` al
    // momento; si no, la saca por detrás y llama después, SIEMPRE en el hilo de interfaz.
    //
    // `sigueValiendo` lo pregunta justo antes de entregar: si el panel ya se cerró o la
    // tarjeta ya no existe, no se toca nada.
    public static void Pedir(IntPtr h, int anchoMax, Dispatcher interfaz,
                             Func<bool> sigueValiendo, Action<ImageSource?> cuandoEste)
    {
        if (_cache.TryGetValue(h, out var ya))
        {
            if (ya.NoSePudo) { cuandoEste(null); return; }
            if (DateTime.Now - ya.Cuando < Caduca) { cuandoEste(ya.Foto); return; }
        }

        Task.Run(() =>
        {
            ImageSource? foto = null;
            // `rechazarLisa: true` solo aquí: esta foto va a una tarjeta de la vista
            // general, y ahí un rectángulo de un solo color es un fallo que se ve —las
            // aplicaciones Electron devuelven eso— y no el fondo de nada.
            try { foto = Nativo.Foto(h, anchoMax, rechazarLisa: true); }
            catch (Exception ex) { Log.Error("sacar la foto de una ventana", ex); }

            interfaz.BeginInvoke(new Action(() =>
            {
                try
                {
                    _cache[h] = new Guardada { Foto = foto, Cuando = DateTime.Now, NoSePudo = foto == null };
                    if (sigueValiendo()) cuandoEste(foto);
                }
                catch (Exception ex) { Log.Error("entregar la foto de una ventana", ex); }
            }), DispatcherPriority.Background);
        });
    }

    // Las ventanas que ya no existen no tienen por qué seguir ocupando memoria. Se llama
    // al cerrar la vista, que es cuando no estorba.
    public static void Limpiar(ICollection<IntPtr> vivas)
    {
        if (_cache.Count < 60) return;
        foreach (var muerta in new List<IntPtr>(_cache.Keys))
            if (!vivas.Contains(muerta)) _cache.Remove(muerta);
    }

    // ---------- El criterio de «aquí no hay nada dibujado» ----------
    //
    // Separado del muestreo para poder comprobarlo: lo que necesita una ventana de verdad
    // es sacar los píxeles, no decidir qué significan.
    //
    // Exacto y no por parecido, a propósito. Una ventana legítimamente casi lisa —un editor
    // en blanco, una terminal negra— tiene marco, borde o cursor, y veinticinco puntos
    // repartidos por toda la imagen no salen EXACTAMENTE iguales. Con tolerancia se
    // empezarían a tirar miniaturas buenas, y eso es peor: una tarjeta gris se ve y se
    // entiende, pero una ventana que pierde su miniatura sin motivo, no.
    internal static bool TodasIguales(IReadOnlyList<uint> muestras)
    {
        if (muestras.Count < 4) return false;        // con tan pocas no se puede juzgar
        uint primera = muestras[0];
        for (int i = 1; i < muestras.Count; i++) if (muestras[i] != primera) return false;
        return true;
    }

    public static void Olvidar(IntPtr h) => _cache.Remove(h);

    internal static void Vaciar() => _cache.Clear();
}
