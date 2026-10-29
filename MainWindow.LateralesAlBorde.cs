using System;
using System.Linq;

namespace NubeBar;

// La puerta de los paneles laterales: el ratón contra el borde de la pantalla.
//
// ---------- Por qué hacía falta ----------
//
// E3 y E4 construyeron los dos paneles. Están escritos, tienen pruebas, salen en las
// capturas… y no había forma de abrirlos: `AbrirLateral` solo lo llamaba el diagnóstico de
// capturas. Ni botón, ni atajo, ni gesto. Una superficie entera que nadie podía ver.
//
// ---------- Y por qué este gesto ----------
//
// Porque es el mismo que ya usa el dock y no inventa nada: `Dock.RatonCerca` es genérico
// para cualquier borde, así que un panel que se abre al llegar al filo izquierdo usa la
// misma cuenta que el dock al llegar al de abajo. Un segundo detector de bordes sería la
// segunda forma de medir lo mismo.
//
// Apagado de serie. Y eso no es prudencia de más: un panel de 340 px que aparece solo
// cuando rozas el borde de la pantalla es intrusivo si no lo has pedido, y a quien no lo
// quiera no se le puede imponer por haber actualizado.
public partial class MainWindow
{
    // Si el ratón está dentro de alguno de los dos paneles. Sin esto, el panel se cerraría
    // justo cuando mueves el ratón hacia dentro para usarlo — el mismo fallo que el dock ya
    // tenía resuelto con `_ratonEnElDock`.
    // Por LADO, no una bandera para los dos. Con los dos paneles abiertos, una sola bandera
    // decía «el ratón está en un lateral» y los mantenía abiertos los dos: te ibas del
    // izquierdo y el derecho seguía creyendo que lo estabas usando.
    readonly HashSet<Lateral.Lado> _ratonDentroDe = new();

    internal void RatonEnLateral(Lateral.Lado l, bool si)
    {
        if (si) _ratonDentroDe.Add(l); else _ratonDentroDe.Remove(l);
    }

    // ---------- Solo se cierra lo que este gesto ha abierto ----------
    //
    // La primera versión cerraba cualquier panel abierto en cuanto la opción estuviera en
    // «nunca». Parecía lo lógico y rompió algo en la primera prueba: el diagnóstico de
    // capturas abre los paneles a mano con `AbrirLateral`, y al pulso siguiente se los
    // encontraba cerrados. «izquierdo abierto · tablero: sin superficies».
    //
    // Y el fallo de verdad no es ese, es el que venía detrás: el día que un botón o un
    // atajo abra un panel, este pulso se lo cerraría en menos de un segundo. Un gesto no
    // puede tener la última palabra sobre superficies que no ha abierto él.
    readonly HashSet<Lateral.Lado> _lateralesDelBorde = new();

    // ================= El muestreo de los bordes =================
    //
    // Por qué existe esto y no se mira el ratón en el sitio donde se decide: porque el dock y
    // los laterales se deciden en el pulso de un segundo, y un segundo no distingue «has
    // dejado el puntero en el filo» de «lo has cruzado». Con el pulso fino de 150 ms sí se
    // distingue, así que el filo se MUESTREA ahí y aquí solo se consulta lo muestreado.
    //
    // El porqué de cada señal está en `GestosDeBorde.cs`. Esto es el cableado.
    readonly GestosDeBorde.Reposo _reposoDeBordes = new();

    bool _ratonEsTuyo = true;

    // Si el ratón lo lleva otro —un juego a pantalla completa, básicamente—, los gestos de
    // borde se apagan: ni dock, ni laterales, ni isla asomando. Público porque los tres lo
    // preguntan y uno de ellos vive en otro fichero.
    internal bool RatonEsTuyo => _ratonEsTuyo;

    internal void MuestrearBordes()
    {
        try
        {
            // `RatonEscondido` es una llamada; `HayPantallaCompleta` recorre ventanas. Así que
            // la segunda solo se pregunta cuando la primera ha dicho algo: fuera de un juego,
            // este muestreo cuesta dos llamadas a user32 y nada más.
            bool acotado = Monitores.RatonAcotado();
            bool escondido = Monitores.RatonEscondido();
            bool pc = escondido && HayPantallaCompleta();

            bool tuyo = GestosDeBorde.EsTuGesto(acotado, escondido, pc);
            if (tuyo != _ratonEsTuyo)
            {
                _ratonEsTuyo = tuyo;
                // Al cambiar de manos se olvida lo medido. Si no, al salir del juego quedaría
                // un reposo de hace rato y el panel se abriría solo al volver al escritorio.
                _reposoDeBordes.Olvidar();
                Log.Info(tuyo
                    ? "el ratón vuelve a ser tuyo: los gestos de borde se encienden"
                    : $"el ratón lo lleva otro ({(acotado ? "acotado" : "escondido a pantalla completa")}): " +
                      "se apagan los gestos de borde");
            }
            if (!tuyo) return;

            var util = UtilDeEstaPantalla();
            var r = PuntoRaton();
            var ahora = DateTime.Now;
            foreach (var b in new[] { NubeHypr.Borde.Arriba, NubeHypr.Borde.Abajo,
                                      NubeHypr.Borde.Izquierda, NubeHypr.Borde.Derecha })
                _reposoDeBordes.Mirar(b, Dock.RatonCerca(util, b, r.X, r.Y), ahora);
        }
        catch (Exception ex) { Log.Error("muestrear los bordes de la pantalla", ex); }
    }

    // ¿Cuenta el filo `b` como gesto tuyo, ahora mismo y de verdad?
    internal bool GestoEnElBorde(NubeHypr.Borde b) =>
        _ratonEsTuyo && _reposoDeBordes.Quieto(b, DateTime.Now);

    void RefrescarLateralesAlBorde()
    {
        try
        {
            if (!_esJefa) return;      // con dos pantallas, una sola isla manda aquí

            var cuando = Dock.Leer(_cfg.Barra.Laterales);
            if (cuando == Dock.Cuando.Nunca)
            {
                // Apagado: se recogen los que ABRIÓ ESTE GESTO y nada más. Los que haya
                // abierto otro no son asunto suyo.
                foreach (var l in _lateralesDelBorde.ToList())
                {
                    if (LateralAbierto(l)) CerrarLateral(l);
                    _lateralesDelBorde.Remove(l);
                }
                return;
            }

            var util = UtilDeEstaPantalla();
            var raton = PuntoRaton();

            foreach (var lado in new[] { Lateral.Lado.Izquierda, Lateral.Lado.Derecha })
            {
                var borde = lado == Lateral.Lado.Izquierda
                          ? NubeHypr.Borde.Izquierda : NubeHypr.Borde.Derecha;

                // ---------- Y el borde donde vive la barra no cuenta ----------
                //
                // Con la barra vertical pegada a la izquierda, acercarse a ese filo es lo
                // que se hace para usar la barra. Abrir ahí un panel de 340 px encima sería
                // castigar el gesto más normal que hay.
                if (Vertical && BordeDeLaBarraEs(borde))
                {
                    if (_lateralesDelBorde.Remove(lado)) CerrarLateral(lado);
                    continue;
                }

                // El filo, pero con la guardia delante: el puntero tiene que ser tuyo y
                // tiene que llevar ahí quieto sus 300 ms. Cruzarlo no abre nada, y dentro de
                // una partida no abre nada tampoco.
                bool cerca = Dock.RatonCerca(util, borde, raton.X, raton.Y) && GestoEnElBorde(borde);
                bool abierto = LateralAbierto(lado);
                bool mio = _lateralesDelBorde.Contains(lado);
                bool debeVerse = Dock.SeVe(cuando, cerca, abierto && _ratonDentroDe.Contains(lado));

                // Solo se actúa cuando CAMBIA. Abrir un panel que ya está abierto lo
                // reconstruiría en cada pulso, que es un segundo de parpadeo por segundo.
                if (debeVerse && !abierto) { AbrirLateral(lado); _lateralesDelBorde.Add(lado); }
                // Y cerrar, solo el que abrió este gesto.
                else if (!debeVerse && abierto && mio) { CerrarLateral(lado); _lateralesDelBorde.Remove(lado); }
                else if (!abierto) _lateralesDelBorde.Remove(lado);   // lo cerró otro
            }
        }
        catch (Exception ex) { Log.Error("mirar si toca abrir un panel lateral", ex); }
    }

    // ¿Es ese el borde donde está la barra ahora mismo?
    bool BordeDeLaBarraEs(NubeHypr.Borde b) =>
        EsElBordeDeLaBarra(PanelPrincipal.Posicion, b);

    // ---------- La decisión, separada de la ventana ----------
    //
    // Sacada aquí para poder exigirla sin una barra delante. No es celo: de esta función
    // depende que acercarse al filo donde **vive** la barra no te abra encima un panel de
    // 340 px, y era la única pieza de la puerta de los laterales sin una sola prueba.
    //
    // Que eso importe está demostrado en este mismo fichero: `RatonEnLateral` existía, no la
    // llamaba nadie, y la puerta habría salido inusable a la primera. Lo cazó un `grep`, no
    // una prueba. Esta vez la prueba va delante.
    //
    // El `_ => arriba` del final no es pereza: la posición se puede escribir a mano en el
    // config, y lo que no se entiende se trata como «arriba», igual que en el resto del
    // programa. Devolver `false` para lo desconocido haría que una posición mal escrita
    // abriera paneles en los dos filos a la vez.
    internal static bool EsElBordeDeLaBarra(string? posicion, NubeHypr.Borde b) =>
        (posicion ?? "arriba").Trim().ToLowerInvariant() switch
        {
            "izquierda" => b == NubeHypr.Borde.Izquierda,
            "derecha" => b == NubeHypr.Borde.Derecha,
            "abajo" => b == NubeHypr.Borde.Abajo,
            _ => b == NubeHypr.Borde.Arriba,
        };
}
