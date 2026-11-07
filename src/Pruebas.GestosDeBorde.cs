using System;

namespace NubeBar;

// La guardia de los gestos de borde: que un juego no te abra los paneles.
//
// ---------- Por qué esto se prueba y no se mira a ojo ----------
//
// Porque el fallo que arregla solo se ve jugando. Alguien tiene que estar en mitad de una
// partida, con los laterales en «al acercarse», girando rápido, para que se note. Eso no es
// una comprobación que se pueda repetir antes de cada versión, así que o está aquí o no está.
//
// Y es frágil de la manera peor: las tres entradas son booleanos y la regla que los combina
// cabe en una línea. Una negación de menos y vuelve el fallo; una de más y los gestos de
// borde se apagan para todo el mundo y nadie se entera hasta que alguien se queja de que el
// dock ya no sale. Las dos direcciones están abajo.
public static partial class Pruebas
{
    static void CorrerGestosDeBorde()
    {
        QuienLlevaElRaton();
        ElReposoEnElFilo();
    }

    static void QuienLlevaElRaton()
    {
        // El caso normal: nadie te ha tocado el ratón, el gesto vale.
        Vale(GestosDeBorde.EsTuGesto(false, false, false),
             "ratón libre y visible: el gesto de borde es tuyo");

        // ---------- Acotado: basta solo ----------
        //
        // Nadie encierra tu puntero en un rectángulo si no lo está usando. No hace falta
        // comprobar nada más, y de hecho exigir además pantalla completa fue la primera
        // versión y dejaba fuera a los juegos en ventana sin bordes que no declaran
        // pantalla completa pero sí acotan el ratón.
        Vale(!GestosDeBorde.EsTuGesto(true, false, false),
             "ratón acotado: no es tu gesto, aunque no haya nada a pantalla completa");
        Vale(!GestosDeBorde.EsTuGesto(true, true, true),
             "ratón acotado y además escondido a pantalla completa: menos todavía");

        // ---------- Escondido: necesita testigo ----------
        //
        // Un reproductor de vídeo esconde el puntero cuando llevas un rato quieto, y ahí sí
        // quieres poder asomar la barra. Así que escondido a secas NO apaga nada.
        Vale(GestosDeBorde.EsTuGesto(false, true, false),
             "ratón escondido pero sin pantalla completa: el gesto sigue siendo tuyo");
        Vale(!GestosDeBorde.EsTuGesto(false, true, true),
             "ratón escondido y algo a pantalla completa: es un juego, no es tu gesto");

        // Y pantalla completa sola tampoco apaga nada: un vídeo a pantalla completa con el
        // puntero a la vista es exactamente cuando quieres poder asomar la isla para ver la
        // hora. Esta es la prueba que impide «arreglarlo» apagando los gestos en cuanto haya
        // algo a pantalla completa, que es lo primero que apetece hacer.
        Vale(GestosDeBorde.EsTuGesto(false, false, true),
             "algo a pantalla completa pero el puntero a la vista: el gesto es tuyo");
    }

    static void ElReposoEnElFilo()
    {
        var r = new GestosDeBorde.Reposo();
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0);

        // Llegar al filo no es un gesto todavía: es el primer muestreo que lo ve.
        Vale(!r.Mirar(NubeHypr.Borde.Izquierda, true, t0),
             "el primer muestreo en el filo no cuenta: acabas de llegar");
        Vale(!r.Mirar(NubeHypr.Borde.Izquierda, true, t0.AddMilliseconds(150)),
             "a los 150 ms tampoco: aún no son los 300");
        Vale(r.Mirar(NubeHypr.Borde.Izquierda, true, t0.AddMilliseconds(300)),
             "a los 300 ms sí: has dejado el puntero ahí");

        // Y cruzarlo lo borra. Esto es el gesto que fallaba: en el escritorio, pasar el ratón
        // de un lado a otro de la pantalla ya no abre nada por el camino.
        Vale(!r.Mirar(NubeHypr.Borde.Izquierda, false, t0.AddMilliseconds(450)),
             "sales del filo y se olvida");
        Vale(!r.Mirar(NubeHypr.Borde.Izquierda, true, t0.AddMilliseconds(600)),
             "y al volver se cuenta de cero, no se reanuda");

        // ---------- Un borde no habla por otro ----------
        //
        // Con el dock abajo y un panel a la izquierda, una sola marca para todos haría que
        // dejar el puntero abajo abriese además el panel de la izquierda. Es el mismo error
        // que `_ratonDentroDe` ya tuvo con una bandera para los dos laterales.
        var r2 = new GestosDeBorde.Reposo();
        r2.Mirar(NubeHypr.Borde.Abajo, true, t0);
        Vale(r2.Mirar(NubeHypr.Borde.Abajo, true, t0.AddMilliseconds(400)),
             "abajo lleva su cuenta");
        Vale(!r2.Quieto(NubeHypr.Borde.Izquierda, t0.AddMilliseconds(400)),
             "y la izquierda no se entera: cada borde lleva la suya");

        // `Quieto` es lo que consulta quien actúa con otro pulso (el dock y los laterales van
        // al de un segundo). Tiene que decir lo mismo que dijo el último muestreo.
        Vale(r2.Quieto(NubeHypr.Borde.Abajo, t0.AddMilliseconds(400)),
             "`Quieto` repite lo que vio el muestreo, sin volver a medir");

        // Y olvidarlo todo de golpe, que es lo que pasa al salir de un juego.
        r2.Olvidar();
        Vale(!r2.Quieto(NubeHypr.Borde.Abajo, t0.AddMilliseconds(400)),
             "al cambiar de manos el ratón, el reposo medido antes no vale");
    }
}
