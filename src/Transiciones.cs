using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E6: las transiciones de la nube.
//
// ---------- La regla que gobierna todo esto ----------
//
// **Una animación representa un cambio de estado real.** No decora, no entretiene y no se
// lanza «porque queda bien». Si el estado no ha cambiado, no hay transición; si ha cambiado,
// la transición dura lo que tarda en entenderse y ni un milisegundo más.
//
// Es la misma regla de D6 (`HyprPinta.TocaAnimar`), subida un nivel: allí era «no relanzar un
// fundido con el mismo origen y destino», aquí es «qué cambios merecen una transición y
// cuánto dura cada uno».
//
// ---------- Por qué puro ----------
//
// Porque «breve y cancelable» es una promesa que se puede comprobar. Que ninguna transición
// pase de un tope, que todas se puedan cancelar, que con el nivel de animación en
// «desactivado» duren cero — eso se mira sin abrir una ventana, y mirarlo a ojo en una
// captura es imposible: una animación de 900 ms y una de 180 se parecen en una imagen quieta.
public static class Transiciones
{
    // ---------- Qué cambios tienen transición ----------
    //
    // Cada una nombra un cambio de estado REAL. Si alguna vez hiciera falta una que no
    // responda a un cambio, no va aquí: va fuera, y entonces el nombre lo dirá.
    public enum Que
    {
        CambioDeEscritorio,   // te has ido al escritorio 2
        PanelEntra,           // se abre un panel o una superficie
        PanelSale,            // se cierra
        CambioDeFoco,         // otra ventana ha pasado al frente
        PreviaDeAjuste,       // aparece el hueco al que iría la ventana
        Recuperacion,         // NubeHypr ha salido de un error o del modo seguro
        ComposicionEntra,     // arranca el escritorio
        ComposicionSale,      // se cierra
    }

    // ---------- Una transición ----------
    //
    // `Ms` es la duración **antes** de aplicarle el nivel de animación. `Cancelable` dice si
    // se puede cortar a medias: casi todas sí, y las que no lo son hay que justificarlas.
    public sealed record Paso(Que Cual, double Ms, bool Cancelable, bool ConNube, string Porque);

    // ---------- El tope ----------
    //
    // Nada de la capa de escritorio puede durar más de esto. Una transición de medio segundo
    // se siente como un programa que va lento, y la diferencia entre «elegante» y «lento» es
    // justo este número.
    public const double TopeMs = 320;

    public static readonly Paso[] Todas =
    {
        new(Que.CambioDeEscritorio, 220, true, true,
            "Cambiar de escritorio es el salto más grande que das sin cambiar de ventana: " +
            "sin transición no se sabe si se ha cambiado o si la pantalla se ha quedado quieta."),

        new(Que.PanelEntra, 160, true, true,
            "Un panel que aparece de golpe parece un fallo de pintado. Con fundido se lee " +
            "como que ha salido de la barra."),

        new(Que.PanelSale, 120, true, false,
            "Salir va más rápido que entrar: lo que se va ya no tienes que mirarlo. Y sin " +
            "nube, porque la nube es la marca de lo que llega, no de lo que se marcha."),

        new(Que.CambioDeFoco, 140, true, false,
            "El filo de la ventana activa cambia de sitio. Sin transición, el filo parpadea " +
            "de una ventana a otra y cuesta seguirlo."),

        new(Que.PreviaDeAjuste, 120, true, false,
            "La previsualización tiene que aparecer YA: llega mientras arrastras, y una " +
            "entrada lenta la enseña cuando ya has soltado."),

        new(Que.Recuperacion, 260, true, true,
            "Salir del modo seguro es una buena noticia y conviene verla. Con nube, porque " +
            "es el estado de NubeHypr el que ha cambiado."),

        new(Que.ComposicionEntra, 300, true, true,
            "El arranque del escritorio. Es lo único que puede permitirse acercarse al tope: " +
            "pasa una vez por sesión."),

        new(Que.ComposicionSale, 180, false, false,
            "Al cerrar NO es cancelable a propósito: si se pudiera cortar, cerrar NubeBar " +
            "dejaría superficies a medio desmontar en la pantalla."),
    };

    public static Paso De(Que q) => Todas.First(p => p.Cual == q);

    // ---------- La duración de verdad ----------
    //
    // Sale del nivel de animación, nunca de un número escrito a mano. Con «desactivado» vale
    // cero y entonces no se lanza nada: es lo que hace que apagar las animaciones las apague
    // de verdad y no las deje «rápidas».
    public static TimeSpan Duracion(Que q) => HyprPinta.Duracion(De(q).Ms);

    // ¿Hay que animar este cambio? Dos condiciones, las dos necesarias: que el estado haya
    // cambiado de verdad, y que el nivel lo permita.
    public static bool TocaAnimar<T>(Que q, T antes, T ahora) where T : IEquatable<T> =>
        HyprPinta.TocaAnimar(antes, ahora) && Duracion(q) > TimeSpan.Zero;

    // ---------- Y la nube ----------
    //
    // No sale en todas. Sale donde el cambio es **de NubeBar o de su estado**: cambiar de
    // escritorio, abrir una superficie, recuperarse de un error, arrancar. No sale al cerrar
    // ni al cambiar el foco de una ventana a otra, que son cosas tuyas y del sistema.
    //
    // Que salga en todas sería una mascota: la nube aparecería ocho veces por minuto y
    // dejaría de significar nada.
    public static bool ConNube(Que q) => De(q).ConNube;

    // El tamaño de la nube en una transición, con los topes de D6. Nunca una mascota gigante
    // que tape el escritorio: ese tope lleva puesto desde D6 y aquí no se levanta.
    public static double TamanoDeLaNube(double disponible) =>
        HyprPinta.TamanoDeLaNube(disponible);

    // ---------- El resumen, para el registro ----------
    public static string Linea(Que q)
    {
        var p = De(q);
        return $"{q}: {Duracion(q).TotalMilliseconds:0} ms" +
               $"{(p.Cancelable ? "" : ", NO cancelable")}" +
               $"{(p.ConNube ? ", con nube" : "")}";
    }
}
