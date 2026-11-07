using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E5: el área donde NubeHypr reparte las ventanas, con las superficies de E descontadas.
//
// ---------- Por qué hace falta ahora ----------
//
// Hasta E, la única superficie que estorbaba era la barra, y `NubeHypr.AreaUtil` con un solo
// estorbo bastaba. Con el dock encendido, repartir sobre la pantalla entera deja una ventana
// **permanentemente** debajo de él.
//
// ---------- Un solo sitio ----------
//
// Esto lo calcula una vez y lo usan el motor, la previsualización y los diagnósticos. Si cada
// uno hiciera su cuenta, el día que una cambiara la previsualización enseñaría un hueco y el
// motor colocaría en otro.
public partial class MainWindow
{
    // El borde de la barra en el lenguaje de NubeHypr ya lo da `BordeDeLaBarra` (E2).

    // Lo que la barra se queda, en píxeles. Cero cuando flota: entonces las ventanas pasan
    // por debajo de la isla a propósito, que es como ha funcionado siempre.
    //
    // La pregunta es la MISMA que la de la appbar, así que se lee del mismo sitio
    // (`ReservaHueco`) y no del interruptor en crudo. En crudo había dos respuestas para
    // una sola cosa: con el autoocultar encendido, Windows no guardaba la franja —eso ya
    // lo descartaba `ReservaHueco`— pero NubeHypr sí la descontaba, y repartía las
    // ventanas dejando un hueco para una barra que no estaba.
    double ReservaDeLaBarraPx => ReservaHueco ? (Grosor + _cfg.Barra.Margen * 2) * _monitor.Escala : 0;

    // ---------- Y los estorbos para los PANELES LATERALES ----------
    //
    // Casi los mismos, con una diferencia que importa: la barra estorba **aunque flote**.
    //
    // Para las ventanas de otros programas, una barra que no reserva espacio no es un
    // estorbo: pasan por debajo a propósito, y así ha funcionado desde el primer día. Pero
    // un panel de NubeBar metiéndose debajo de la barra de NubeBar no es una decisión de
    // diseño: son dos cosas nuestras pisándose.
    //
    // Hizo falta en cuanto el dock y los laterales se pudieron encender a la vez. El
    // lateral se colocaba en el área de trabajo cruda, centrado, con 12 px de margen arriba
    // y abajo —menos que los 38 de la barra—, así que se metía debajo de la isla; y con
    // bastantes apps fijadas, también debajo del dock.
    internal List<NubeHypr.Estorbo> EstorbosParaLosLaterales()
    {
        var l = EstorbosDeAhora();
        if (!ReservaHueco)
            l.Add(new NubeHypr.Estorbo(BordeDeLaBarra,
                                       (Grosor + _cfg.Barra.Margen * 2) * _monitor.Escala));
        return l;
    }

    // ---------- Los estorbos ----------
    //
    // Solo lo PERMANENTE. Los paneles laterales no entran aunque estén abiertos: si entraran,
    // abrir el panel izquierdo recolocaría todas las ventanas y cerrarlo las recolocaría otra
    // vez, y mirar el volumen te movería la pantalla entera.
    internal List<NubeHypr.Estorbo> EstorbosDeAhora()
    {
        var l = new List<NubeHypr.Estorbo>
        {
            new(BordeDeLaBarra, ReservaDeLaBarraPx),
        };

        // El dock, solo si está puesto Y visible. Con «al acercarse» aparece y desaparece,
        // así que lo que cuenta es si está ahora: igual que la barra que no reserva, un dock
        // escondido no quita sitio.
        try
        {
            if (_dock is { IsVisible: true })
            {
                // El mismo rectángulo que se le da al dock, en píxeles: los estorbos van en
                // píxeles (ver `SitioDelDock`).
                var r = SitioDelDock(_dockFila?.Children.Count ?? 0);
                double grosor = Dock.EsVertical(BordeDelDock) ? r.Ancho : r.Alto;
                // Más la separación del filo, que es hueco que el dock también ocupa.
                l.Add(new NubeHypr.Estorbo(BordeDelDock, grosor + Dock.DelFilo * Math.Max(0.1, _monitor.Escala)));
            }
        }
        catch (Exception ex) { Log.Error("medir el dock para el área de trabajo", ex); }

        return l;
    }

    // ---------- El área ----------
    internal NubeHypr.Rect AreaDeTrabajo()
    {
        var m = _monitor;
        var monitor = new NubeHypr.Rect(m.X, m.Y, m.Ancho, m.Alto);
        return NubeHypr.AreaUtil(monitor, _cfg.NubeHypr.Margen * m.Escala,
                                 EstorbosDeAhora().ToArray());
    }

    // Para el registro y los diagnósticos: qué estorba y cuánto.
    internal string LineaDelArea()
    {
        var e = EstorbosDeAhora();
        var a = AreaDeTrabajo();
        return $"área={a} · estorbos: " +
               string.Join(" · ", e.Where(x => x.Cuenta).Select(x => $"{x.Donde} {x.Grosor:0}px")) +
               (e.All(x => !x.Cuenta) ? "ninguno (la barra flota y no hay dock)" : "");
    }
}
