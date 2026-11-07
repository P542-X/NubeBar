using System;
using System.Collections.Generic;

namespace NubeBar;

// Cuándo un ratón en el filo de la pantalla es un GESTO TUYO, y cuándo no lo es.
//
// ---------- El fallo que trae esto aquí ----------
//
// Jugando a pantalla completa, con los paneles laterales en «al acercarse»: giras la cámara
// rápido hacia un lado y se te abre un panel encima de la partida. Mirando rápido hacia
// arriba, asoma la isla. El dock hace lo mismo por abajo.
//
// Y no es un fallo de la cuenta del borde: `Dock.RatonCerca` está haciendo exactamente lo que
// se le pidió. El problema es ANTES, en la pregunta. Se estaba preguntando «¿está el puntero
// en el filo?» cuando lo que había que preguntar es «¿has llevado tú el puntero al filo?».
//
// En un juego no lo has llevado tú. El juego lee el ratón en crudo (`WM_INPUT`): mueve la
// cámara con el desplazamiento del sensor y el puntero del escritorio no le sirve de nada, así
// que lo deja donde cae. Y donde cae, girando deprisa, es contra un tope: Windows recorta el
// puntero al rectángulo que el juego haya pedido y ahí se queda clavado mientras sigas
// girando. Muchos además lo recolocan en el centro en cada fotograma, y entre el recorte y la
// recolocación el puntero aparece pegado a un filo —no siempre al que mirabas, que es por lo
// que mirar a la derecha abría el panel de la izquierda—.
//
// Nada de eso es un gesto. Es la sombra de un ratón que ahora mismo lleva otro.
//
// ---------- Las dos señales ----------
//
// **Acotado.** `GetClipCursor` dice a qué rectángulo está confinado el puntero. Si no es el
// escritorio entero, alguien lo ha encerrado — y nadie encierra tu ratón si no lo está usando.
// Esta señal basta sola: no hace falta que haya nada a pantalla completa.
//
// **Escondido.** `GetCursorInfo` dice si hay puntero dibujado. Un juego lo esconde. Pero un
// reproductor de vídeo a pantalla completa también lo esconde cuando llevas un rato quieto,
// y ahí sí quieres poder asomar la barra para ver la hora. Así que esta señal SOLO cuenta si
// además hay algo a pantalla completa en esa pantalla, que es el caso del juego; y en el del
// vídeo, mover el ratón hacia el filo hace que el reproductor lo vuelva a dibujar antes de que
// llegues, con lo que el gesto sigue funcionando.
//
// La diferencia entre las dos no es capricho: una es «alguien se ha quedado el ratón» y la
// otra es «puede que alguien se haya quedado el ratón». A la primera se le cree sola; a la
// segunda se le pide un testigo.
//
// ---------- Y el reposo ----------
//
// Aparte de quién lleva el ratón, hay una segunda cosa que faltaba: el dock y los laterales
// abrían con el puntero DE PASO. La isla ya exigía 300 ms clavada en el filo
// (`MainWindow.PantallaCompleta.cs`, `MsAsomar`) y por eso era la que menos se abría sola.
// Aquí se le pide lo mismo a los otros dos: cruzar un filo no abre nada; dejar el puntero
// ahí, sí.
//
// Nada de esto se mide con un reloj propio. Lo muestrea el vigilante de 150 ms que ya existe,
// que es el único con el pulso fino para distinguir un roce de una intención.
public static class GestosDeBorde
{
    // Cuánto hay que dejar el puntero en el filo. El mismo número que la isla, y por el mismo
    // motivo: por debajo de 300 ms no se distingue de un barrido, y por encima de medio
    // segundo el panel se siente lento.
    public const double MsQuieto = 300;

    // ---------- La decisión, sin Windows delante ----------
    //
    // Separada para poder exigirla en las pruebas. Las tres entradas son hechos que alguien
    // mira por ti; esto solo los combina, y combinarlos mal es exactamente el fallo que
    // estamos arreglando.
    public static bool EsTuGesto(bool ratonAcotado, bool ratonEscondido, bool pantallaCompleta) =>
        !ratonAcotado && !(ratonEscondido && pantallaCompleta);

    // ---------- El reposo, por borde ----------
    //
    // Por BORDE y no una marca para todos: con el dock abajo y un panel a la izquierda, una
    // sola marca haría que dejar el puntero abajo contase como reposo en la izquierda. Es el
    // mismo error que `_ratonDentroDe` ya tuvo con una bandera para los dos laterales.
    public sealed class Reposo
    {
        readonly Dictionary<NubeHypr.Borde, DateTime> _desde = new();

        // Se llama en cada muestreo. Devuelve si ese borde cuenta YA como reposo.
        //
        // `ahora` entra como parámetro en vez de leer el reloj dentro porque si no, esto no se
        // puede probar sin esperar de verdad 300 ms, y una prueba que duerme es una prueba que
        // acaba desactivada.
        public bool Mirar(NubeHypr.Borde borde, bool cerca, DateTime ahora, double ms = MsQuieto)
        {
            if (!cerca) { _desde.Remove(borde); return false; }
            if (!_desde.TryGetValue(borde, out var desde)) { _desde[borde] = ahora; return false; }
            return (ahora - desde).TotalMilliseconds >= ms;
        }

        // Lo que vio el último muestreo, para que quien actúa con otro pulso —el dock y los
        // laterales van al de un segundo— no tenga que volver a medir.
        public bool Quieto(NubeHypr.Borde borde, DateTime ahora, double ms = MsQuieto) =>
            _desde.TryGetValue(borde, out var desde) && (ahora - desde).TotalMilliseconds >= ms;

        // Olvidarlo todo. Hace falta al cambiar de pantalla completa o al volver de estar
        // escondida: si no, se arrastra un reposo medido cuando el ratón era de otro.
        public void Olvidar() => _desde.Clear();
    }
}
