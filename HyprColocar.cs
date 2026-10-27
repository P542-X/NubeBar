using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.5: la decisión de una regla, convertida en efectos.
//
// ---------- El eslabón que faltaba ----------
//
// Desde D5.1, cuando aparece una ventana se resuelven las reglas, se apunta la decisión y
// **no se mueve nada**. Estaba bien hecho a propósito: «conectarlo y aplicarlo a la vez
// sería meter dos cambios en uno y no saber cuál rompió qué». Esto es la otra mitad.
//
// Lo que había en medio era un hueco de verdad, no una formalidad: `HyprReglas` produce una
// `Decision` —espacio, monitor, flotante, tamaño, foco— y `HyprAdaptador` consume
// `EfectoVentana`. Nadie traducía de una a otro. La guía de reglas lleva desde D9.4
// prometiendo «cuando aparezca Discord, enviarlo al Escritorio 2 y dejarlo flotante», y esa
// frase no tenía ningún código detrás que la cumpliera.
//
// ---------- La regla de oro de este fichero ----------
//
// **Un campo del que ninguna regla opinó no produce ningún efecto.**
//
// Es lo que separa «NubeHypr coloca lo que le has pedido» de «NubeHypr te recoloca las
// ventanas». Si una regla dice solo «al espacio 2», la ventana se va al espacio 2 y conserva
// su tamaño, su monitor y su sitio: nadie ha pedido lo demás. `HyprVigilante` ya calcula
// esa lista de campos sin opinión; aquí se respeta.
//
// Y por eso esto es lógica pura: la diferencia entre mover lo pedido y mover de más se
// decide aquí, y tiene que poder comprobarse sin ventanas de verdad delante.
public static class HyprColocar
{
    // ---------- Qué sabemos del sitio ----------
    //
    // Lo mínimo para calcular un destino, y nada de Win32: el área útil de cada monitor ya
    // descontada (la barra, el dock, lo que haya), en el mismo orden en que las reglas
    // numeran los monitores.
    public readonly record struct Sitio(IReadOnlyList<NubeHypr.Rect> Monitores, int CuantosEspacios);

    // ---------- Lo que la decisión implica ----------
    public sealed record Plan(
        IntPtr Hwnd,
        int? AlEspacio,            // índice desde 1, como se escribe en una regla
        NubeHypr.Rect? Destino,    // null = no se toca el sitio
        bool Enfocar,
        IReadOnlyList<string> Pegas)   // por qué algún campo no se pudo cumplir
    {
        public bool HaceAlgo => AlEspacio != null || Destino != null || Enfocar;
    }

    // ---------- Y la traducción ----------
    //
    // `v` es la ventana tal y como está AHORA. Su rectángulo actual es el punto de partida:
    // lo que la decisión no diga, se queda como está.
    public static Plan Planear(VentanaModelo v, HyprReglas.Decision d, Sitio sitio)
    {
        var pegas = new List<string>();

        // ---------- El espacio ----------
        int? espacio = null;
        if (d.Espacio is { } e)
        {
            if (e < 1 || e > Math.Max(0, sitio.CuantosEspacios))
                pegas.Add($"la regla pide el escritorio {e} y hay {sitio.CuantosEspacios}");
            else if (e - 1 == v.Escritorio)
                pegas.Add($"ya está en el escritorio {e}");
            else espacio = e;
        }

        // ---------- El monitor y el tamaño ----------
        //
        // Van juntos porque los dos son «dónde cae el rectángulo», y calcularlos por
        // separado daría dos destinos para la misma ventana.
        NubeHypr.Rect? destino = null;
        bool tocaElSitio = d.Monitor != null || d.Ancho != null || d.Alto != null ||
                           d.Flotante == true;

        if (tocaElSitio)
        {
            int m = d.Monitor ?? (v.Monitor + 1);
            if (m < 1 || m > sitio.Monitores.Count)
            {
                pegas.Add($"la regla pide el monitor {m} y hay {sitio.Monitores.Count}");
            }
            else
            {
                var area = sitio.Monitores[m - 1];

                // El tamaño: una fracción del área útil (0,5 = la mitad) o, sin decir nada,
                // lo que la ventana mide ahora. Fracción y no píxeles porque una regla
                // escrita en un monitor de 4K no puede dejar una ventana de 3000 px en un
                // portátil.
                double ancho = d.Ancho is { } fa ? area.Ancho * Limitar(fa) : v.Rectangulo.Ancho;
                double alto = d.Alto is { } fl ? area.Alto * Limitar(fl) : v.Rectangulo.Alto;

                // Nunca más grande que su monitor ni tan pequeña que no se pueda agarrar.
                ancho = Math.Clamp(ancho, MinimoLado, area.Ancho);
                alto = Math.Clamp(alto, MinimoLado, area.Alto);

                // Dónde: centrada en el área del monitor. Es lo que se espera de «mándalo
                // al monitor 2 y déjalo flotante», y no depende de dónde estuviera antes,
                // que es justo lo que no sirve cuando cambia de pantalla.
                double x = area.X + (area.Ancho - ancho) / 2;
                double y = area.Y + (area.Alto - alto) / 2;

                destino = new NubeHypr.Rect(Math.Round(x), Math.Round(y),
                                            Math.Round(ancho), Math.Round(alto));
            }
        }

        // ---------- El foco ----------
        //
        // Solo si la regla dice `Foco = true`. Un `false` explícito no significa «quítale
        // el foco a esta ventana»: significa «no te ocupes del foco», y quitárselo a la
        // ventana que el usuario acaba de abrir sería de las cosas más molestas que puede
        // hacer un gestor.
        bool enfocar = d.Foco == true;

        return new Plan(v.Hwnd, espacio, destino, enfocar, pegas);
    }

    // Una ventana por debajo de esto no se puede agarrar con el ratón.
    public const double MinimoLado = 120;

    // Las fracciones de las reglas se escriben entre 0 y 1. Fuera de ahí es un error de
    // quien la escribió, y lo razonable es acotar y seguir, no dejar la ventana de un píxel.
    static double Limitar(double fraccion) => Math.Clamp(fraccion, 0.05, 1.0);

    // ---------- Y los efectos ----------
    //
    // El ORDEN importa y no es cosmético: primero el espacio, después el sitio, el foco al
    // final.
    //
    //  · Colocar antes de mandar al otro escritorio deja a Windows recolocando una ventana
    //    que se está yendo, y el resultado depende de quién gane la carrera.
    //  · Y enfocar antes de las dos haría saltar la pantalla al escritorio de origen justo
    //    antes de sacarla de ahí.
    public static List<EfectoVentana> Efectos(Plan p, string porque)
    {
        var efectos = new List<EfectoVentana>();
        if (p.AlEspacio is { } e)
            efectos.Add(new EfectoVentana(TipoEfecto.AEspacio, p.Hwnd, Espacio: e, Porque: porque));
        if (p.Destino is { } r)
            efectos.Add(new EfectoVentana(TipoEfecto.Colocar, p.Hwnd, Destino: r, Porque: porque));
        if (p.Enfocar)
            efectos.Add(new EfectoVentana(TipoEfecto.Enfocar, p.Hwnd, Porque: porque));
        return efectos;
    }

    // ================= La puerta =================
    //
    // Si una decisión de regla puede llegar a mover algo, y si no, por qué no. Devuelve
    // null cuando sí se puede.
    //
    // Está aparte y es pura porque es la frase que hay que poder leer en una prueba: son
    // las condiciones que impiden que NubeHypr te toque una ventana sin haberlo pedido, y
    // vale más tenerlas en un sitio que repartidas por tres `if` de la interfaz.
    public static string? PorQueNoSeAplica(bool capaActiva, NubeHypr.Modo modo,
                                           bool algunaReglaCoincidio, bool laDecisionDiceAlgo)
    {
        if (!capaActiva) return "NubeHypr está apagado";
        if (!algunaReglaCoincidio) return "ninguna regla coincide con esta ventana";
        if (!laDecisionDiceAlgo) return "las reglas que coinciden no deciden nada";
        if (modo != NubeHypr.Modo.Aplicar) return "el modo es simulación: se calcula y no se mueve";
        return null;
    }

    public static bool SeAplica(bool capaActiva, NubeHypr.Modo modo,
                                bool algunaReglaCoincidio, bool laDecisionDiceAlgo) =>
        PorQueNoSeAplica(capaActiva, modo, algunaReglaCoincidio, laDecisionDiceAlgo) == null;

    // ---------- Cuánto se espera antes de colocar ----------
    //
    // Una ventana recién abierta todavía se está colocando: muchas aplicaciones hacen su
    // `SetWindowPos` unos milisegundos después de ser visibles —restaurar el sitio de la
    // sesión anterior, centrarse, ajustarse al DPI—. Colocarla en ese momento es entrar en
    // una pelea que se ve: la ventana salta dos veces y a veces gana la aplicación.
    //
    // 350 ms es el margen que deja pasar esa recolocación inicial sin que la espera se note
    // como un tirón. No es un número medido en este equipo: es un punto de partida, y está
    // aquí con nombre para poder cambiarlo en un sitio si hace falta.
    public static readonly TimeSpan LoQueTardaEnAsentarse = TimeSpan.FromMilliseconds(350);

    // ================= D9.6: el layout, que es otra cosa =================
    //
    // Los cinco campos de arriba —espacio, monitor, flotante, tamaño, foco— tocan **la
    // ventana que la regla ha nombrado**. `Layout` no: dice cómo se reparte el ESPACIO
    // entero, y repartirlo mueve a todas las demás, que son ventanas que ninguna regla ha
    // mencionado.
    //
    // Es la única cosa de NubeHypr que hace eso, y por eso va aparte, con su propia
    // pregunta y su propia línea en el registro. No es una excepción a la regla de oro de
    // este fichero: es que un layout es, por definición, una decisión sobre el conjunto.
    // Lo que no puede pasar es que ocurra sin que se vea venir.
    //
    // Las salvaguardas no son nuevas y por eso esto es poco código: el reparto sale por la
    // misma puerta (`AplicarConRedes`), así que el respeto a tu gesto deja fuera las
    // ventanas que has colocado a mano, el tope de ritmo corta las ráfagas, y el lote
    // entero queda apuntado para deshacerlo de una vez.
    public static bool LaReglaRepiteElEspacio(HyprReglas.Decision d) => d.Layout != null;

    // Y el aviso, con el número delante. «Van a moverse 6 ventanas» es lo que uno quiere
    // leer antes de que se muevan, no después.
    public static string LineaDelLayout(NubeHypr.Layout layout, int cuantas, bool seAplica) =>
        $"[D9.6] la regla pide el layout «{layout}» para este escritorio: " +
        (seAplica
            ? $"se reparten {cuantas} ventana(s), incluidas las que ninguna regla nombra"
            : $"NO se aplica (serían {cuantas} ventana(s))");

    // ---------- Lo que se escribe en el registro ----------
    public static List<string> Lineas(VentanaModelo v, Plan p, string? porQueNo)
    {
        var lineas = new List<string>
        {
            $"[D9.5] regla sobre hwnd=0x{v.Hwnd.ToInt64():X6} proceso={v.Proceso}",
        };

        if (porQueNo != null) { lineas.Add($"[D9.5]   no se aplica: {porQueNo}"); return lineas; }

        if (p.AlEspacio is { } e) lineas.Add($"[D9.5]   al escritorio {e}");
        if (p.Destino is { } r) lineas.Add($"[D9.5]   colocar en {r}");
        if (p.Enfocar) lineas.Add("[D9.5]   y enfocarla");
        foreach (var pega in p.Pegas) lineas.Add($"[D9.5]   pega: {pega}");
        if (!p.HaceAlgo) lineas.Add("[D9.5]   no queda nada que hacer");
        return lineas;
    }
}
