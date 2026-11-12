using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace NubeBar;

// Que un panel cabe en la pantalla, o se desplaza por dentro. Nunca ni lo uno ni lo otro.
//
// ---------- De dónde sale esto ----------
//
// El panel «Red e Internet» salía con la tarjeta de acciones cortada por abajo: los cinco
// botones existían, estaban bien colocados dentro del panel, y ciento once píxeles de ellos
// caían por debajo del borde de la isla sin ninguna barra de desplazamiento que los
// alcanzara. Un botón que no se puede pulsar.
//
// La causa no se podía comprobar con una prueba —era `DesiredSize` contestándose a sí mismo
// bajo una fila `Star`, y eso necesita WPF midiendo de verdad—. Lo que SÍ se puede fijar
// aquí es la regla que el arreglo tiene que cumplir, en todas las pantallas:
//
//     o el panel cabe entero, o se enciende el desplazamiento interno;
//     y el alto que se le da NUNCA pasa del tope de la pantalla.
//
// Mientras esas dos se cumplan, no puede quedar un control por debajo del borde: o está
// dentro, o se llega a él con la rueda.
public static partial class Pruebas
{
    internal static void CorrerPaneles()
    {
        PanelesElTopeMiraLaPantalla();
        PanelesOCabeOSeDesplaza();
        PanelesEnCadaPantallaYOrientacion();
        RespaldoDeMigracionNuncaSePisa();
        PanelesSeEscalanConLaPantalla();
        IconosOscurosSeReconocen();
        LaBarraDeScrollEsLaNuestra();
        ElHaloDelPanelLateralNoVaEnLoRecortado();
        LaReservaNoDependeDelModo();
        LaNotchEsUnaIsla();
        LaNotchNoPierdeSusEsquinasAlAbrirUnPanel();
        ElPulsoDeAudioNoMueveLoQueEstasLeyendo();
        LaIslaCreceEnseguidaYEncogeConCalma();
        LoQueSeCentraMidePar();
        ElCirculoDelDeslizadorNoSeSale();
        LosModulosSeSeparanEnElEjeQueSeApilan();
        ElIdiomaSePreguntaUnaVezYSoloUna();
    }

    // ================= La pregunta del idioma =================
    //
    // «Cuando ejecuten el exe que les dé a elegir si inglés o español.»
    //
    // Lo que se fija aquí es lo que convierte una buena idea en una molestia si se hace
    // mal: que se pregunte UNA vez. `Tema.Idioma` vale "auto" de serie y no puede hacer
    // de respuesta —no distingue «he elegido automático» de «nadie me ha preguntado»—,
    // así que la memoria es una bandera aparte. Es el mismo tri-estado disfrazado que
    // hizo falta en ReservarEspacio esta misma versión.
    static void ElIdiomaSePreguntaUnaVezYSoloUna()
    {
        Grupo("el idioma de la primera vez");

        var recien = new Config();
        Vale(ElegirIdioma.HayQuePreguntar(recien), "config recién nacido: se pregunta");
        Igual("auto", recien.Tema.Idioma, "y hasta que conteste, manda lo que diga Windows");

        // Elija lo que elija, incluido «automático», no se vuelve a preguntar
        foreach (var respuesta in new[] { "es", "en", "auto" })
        {
            var c = new Config();
            c.Tema.Idioma = respuesta;
            c.Tema.IdiomaPreguntado = true;
            Vale(!ElegirIdioma.HayQuePreguntar(c), $"tras contestar «{respuesta}», no se vuelve a preguntar");
        }

        // Y el caso que una bandera mal pensada se habría comido: quien elige automático
        // a propósito. Sin la bandera, su config es idéntico al de alguien a quien nadie
        // ha preguntado, y le saldría la ventana en cada arranque.
        var automatico = new Config();
        automatico.Tema.Idioma = "auto";
        automatico.Tema.IdiomaPreguntado = true;
        Vale(!ElegirIdioma.HayQuePreguntar(automatico),
             "elegir «automático» es una respuesta, no la falta de una");

        // Lo que se guarda se lee igual: las tres claves van y vuelven
        var antes = Textos.Elegida;
        try
        {
            foreach (var (clave, esperado) in new[]
                     { ("es", Textos.Lengua.Espanol), ("en", Textos.Lengua.Ingles), ("auto", Textos.Lengua.Auto) })
            {
                Textos.Elegir(clave);
                Igual(esperado, Textos.Elegida, $"«{clave}» se guarda y se lee igual");
            }
        }
        finally { Textos.Elegir(Textos.Clave(antes)); }
    }

    // ================= La separación de un módulo, en los dos lados =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «Cuando está sonando la música hay huecos que no se completan, como que no van
    // juntos» — con la barra a la izquierda y en modo barra, o sea en vertical.
    //
    // Los dos módulos de música llevaban su separación escrita a pelo como
    // `Thickness(4, 0, 4, 0)`: izquierda y derecha. En horizontal es lo correcto y en
    // vertical es el eje equivocado, así que gastaba ancho en una columna estrecha y
    // dejaba a los módulos pegados unos a otros.
    //
    // Lo que se exige aquí es que la separación caiga SIEMPRE en el eje en que se apila,
    // y que valga lo mismo en los dos: un módulo no se separa más ni menos por que la
    // nube esté de pie.
    static void LosModulosSeSeparanEnElEjeQueSeApilan()
    {
        Grupo("la separación de un módulo");

        var h = MainWindow.SeparacionDeModulo(false, 4, 4);
        var v = MainWindow.SeparacionDeModulo(true, 4, 4);

        // Horizontal: los vecinos están a los lados
        Igual(4.0, h.Left, "horizontal: separa por la izquierda");
        Igual(4.0, h.Right, "horizontal: y por la derecha");
        Igual(0.0, h.Top + h.Bottom, "horizontal: y nada arriba ni abajo");

        // Vertical: los vecinos están arriba y abajo
        Igual(4.0, v.Top, "vertical: separa por arriba");
        Igual(4.0, v.Bottom, "vertical: y por abajo");
        Igual(0.0, v.Left + v.Right, "vertical: y nada a los lados — ahí no hay nadie");

        // Lo mismo en los dos: la nube de pie no aprieta ni suelta los módulos
        foreach (var (antes, despues) in new[] { (4.0, 4.0), (0.0, 7.0), (6.0, 6.0), (0.0, 0.0) })
        {
            var a = MainWindow.SeparacionDeModulo(false, antes, despues);
            var b = MainWindow.SeparacionDeModulo(true, antes, despues);
            Igual(a.Left + a.Right, b.Top + b.Bottom,
                  $"({antes:0},{despues:0}) separa lo mismo apilando en un eje que en el otro");
            Igual(0.0, a.Top + a.Bottom + b.Left + b.Right,
                  $"({antes:0},{despues:0}) y nada en el eje que no toca");
        }

        // El orden importa: «antes» es el vecino de arriba en vertical y el de la
        // izquierda en horizontal. Un módulo con 0 delante y 7 detrás tiene que quedar
        // pegado a lo anterior en los dos casos, no al revés en uno de ellos.
        var r = MainWindow.SeparacionDeModulo(true, 0, 7);
        Igual(0.0, r.Top, "con (0,7) en vertical no se separa de lo de arriba");
        Igual(7.0, r.Bottom, "y sí de lo de abajo");
    }

    // ================= Lo que se centra mide par =================
    //
    // ---------- De dónde sale esto ----------
    //
    // El píxel que quedaba del temblor de los separadores, después de poner el ancho de la
    // isla a múltiplos de cuatro. La zona central va centrada, así que su filo izquierdo
    // cae en `(hueco - ancho) / 2`. Con el ancho fraccionario —medir texto siempre lo es—
    // ese filo cae a medio píxel y el entero al que se ajusta cambia en cuanto la medida
    // se mueve una décima.
    //
    // Se midió con capturas cada segundo: 391 530 627 794 -> 390 530 627 795. La de la
    // izquierda para un lado y la de la derecha para el otro, que es la firma de un
    // centrado y no de un módulo creciendo.
    static void LoQueSeCentraMidePar()
    {
        Grupo("lo que se centra mide par");

        // Nunca por debajo de lo medido: recortar es el error caro
        for (double w = 0; w < 400; w += 0.5)
            Vale(MainWindow.APar(w) >= w, $"{w:0.#} no se recorta al subir al par");

        // Y siempre par, que es lo único que hace entera la mitad
        foreach (double w in new[] { 0.0, 0.1, 1.0, 1.9, 2.0, 2.1, 579.4, 580.0, 580.6 })
            Vale(MainWindow.APar(w) % 2 == 0, $"{w:0.#} sube a un ancho par");

        // Lo que de verdad se exige: centrado en un hueco par, el filo cae en un entero.
        // El hueco es el interior de la isla, que ya es par (ver ARejilla).
        foreach (double hueco in new[] { 560.0, 580.0, 996.0, 2560.0 })
            for (double w = 100; w < 500; w += 3.7)
            {
                double filo = (hueco - MainWindow.APar(w)) / 2;
                Vale(filo % 1 == 0, $"zona de {MainWindow.APar(w):0} en hueco de {hueco:0}: filo entero");
            }

        // Y el caso que lo explica: dos medidas que solo se diferencian en una décima
        // tienen que dar el MISMO ancho, o el centro vuelve a saltar.
        Igual(MainWindow.APar(579.4), MainWindow.APar(579.9), "579,4 y 579,9 dan el mismo ancho");
        Igual(MainWindow.APar(580.0), MainWindow.APar(579.9), "y 580,0 también");
    }

    // ================= El círculo del deslizador =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «En la barra donde se puede cambiar el brillo y el sonido, el circulito que
    // representa la posición del porcentaje, cuando está completo al cien por cien, es
    // como si se cortara por la mitad.»
    //
    // Estaba puesto en `w * k - 7`, o sea centrado sobre el punto del porcentaje. Al 100 %
    // eso deja su centro en el filo de la barra y la mitad del círculo fuera, donde la
    // recortan. El extremo izquierdo no fallaba porque tenía un `Math.Max(0, …)` sujetando
    // el borde — un tope a mano en un lado y nada en el otro.
    //
    // Lo que se fija aquí es que el círculo QUEPA SIEMPRE, en los dos extremos y con
    // cualquier ancho de barra, y que a media barra siga cayendo en el medio: si se
    // corrigiera el desbordamiento empujando el recorrido sin más, el 50 % dejaría de
    // coincidir con la mitad y eso se ve a simple vista.
    static void ElCirculoDelDeslizadorNoSeSale()
    {
        Grupo("el círculo del deslizador");

        const double d = 14;     // el diámetro del agarre

        foreach (double w in new[] { 60.0, 120.0, 180.0, 240.0, 317.0 })
        {
            double izq0 = MainWindow.IzquierdaDelAgarre(w, d, 0);
            double izq1 = MainWindow.IzquierdaDelAgarre(w, d, 1);

            Igual(0.0, izq0, $"barra de {w:0}: al 0 % el círculo arranca en el filo");
            Igual(w, izq1 + d, $"barra de {w:0}: al 100 % el círculo TERMINA en el filo");

            // Y el medio sigue siendo el medio: centro = izquierda + radio
            double centroMedio = MainWindow.IzquierdaDelAgarre(w, d, 0.5) + d / 2;
            Igual(w / 2, centroMedio, $"barra de {w:0}: al 50 % el centro cae en la mitad");

            // Lo que no puede pasar nunca, en todo el recorrido
            for (double k = 0; k <= 1.0001; k += 0.05)
            {
                double izq = MainWindow.IzquierdaDelAgarre(w, d, k);
                Vale(izq >= 0 && izq + d <= w + 0.001,
                     $"barra de {w:0} al {k * 100:0} %: el círculo cabe entero");
            }
        }

        // Un valor fuera de rango no puede sacarlo de la barra (el gesto lo acota, pero
        // esta función no puede depender de que alguien se acuerde)
        Igual(0.0, MainWindow.IzquierdaDelAgarre(120, d, -3), "un porcentaje negativo se queda en el filo");
        Igual(106.0, MainWindow.IzquierdaDelAgarre(120, d, 9), "y uno pasado, en el otro filo");

        // Una barra más estrecha que el propio círculo no puede dar un margen negativo
        Igual(0.0, MainWindow.IzquierdaDelAgarre(10, d, 1), "barra más estrecha que el círculo: sin margen negativo");
    }

    // ================= El baile de los separadores =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «Hay veces que los separadores se mueven mientras estoy jugando; la línea de los
    // separadores parpadean y se mueven 1 o 2 px de lado a lado.»
    //
    // `RefrescarApps` vive en los actualizadores, o sea que el ancho se repregunta cada
    // segundo, y el listón para rehacerlo era medio píxel. Medir texto da decimales, así
    // que el reloj pasando de minuto bastaba para lanzar una animación de 280 ms de la
    // isla entera; y como la isla va centrada, todo lo de dentro se corría esa pizca. En
    // una raya de 1 px eso se ve como un parpadeo y un salto.
    //
    // Lo que se fija aquí es la asimetría, que es el arreglo entero: crecer no puede
    // esperar —quedarse corto RECORTA el contenido— y encoger sí, porque sobrar tres
    // píxeles no lo ve nadie y mover la isla para quitarlos lo ve todo el mundo.
    static void LaIslaCreceEnseguidaYEncogeConCalma()
    {
        Grupo("el ancho de la isla: crecer y encoger");

        // ---------- El escalón: lo que de verdad paró el temblor ----------
        //
        // El listón de arriba frena los encogimientos pequeños, pero CRECER se hace
        // siempre —lo medido es lo que el contenido necesita— así que un respiro de dos
        // píxeles seguía moviendo la isla entera. Medido en la pantalla vertical del
        // usuario, con capturas cada segundo, las rayas iban 391 → 390 → 391.
        //
        // Con el ancho subido a múltiplos de cuatro, uno, dos y tres píxeles de respiro
        // caen en el mismo escalón y no pasa nada. Eso es lo que se exige aquí.
        Igual(584.0, MainWindow.ARejilla(581), "581 sube al escalón de 584");
        Igual(584.0, MainWindow.ARejilla(582), "582 también");
        Igual(584.0, MainWindow.ARejilla(583), "y 583");
        Igual(584.0, MainWindow.ARejilla(584), "584 ya está en el escalón y no se mueve");
        Igual(588.0, MainWindow.ARejilla(584.1), "y una décima más ya salta al siguiente");

        // Nunca hacia abajo: recortar el contenido es el error caro
        for (double w = 100; w < 400; w += 0.5)
            Vale(MainWindow.ARejilla(w) >= w, $"{w:0.#} no se recorta al subir a la rejilla");

        // Y el escalón es par, que es lo que mantiene el centrado en píxel entero:
        // la isla va centrada, y (ventana - isla) / 2 con los dos pares da un entero.
        Vale(MainWindow.RejillaDelAncho % 2 == 0, "el escalón es par: el centrado no cae a medio píxel");
        foreach (double ventana in new[] { 1080.0, 1920.0, 2560.0, 3440.0 })
            for (double w = 200; w < 900; w += 7)
            {
                double isla = MainWindow.ARejilla(w);
                Vale((ventana - isla) / 2 % 1 == 0,
                     $"isla de {isla:0} centrada en {ventana:0}: filo en píxel entero");
            }

        // Crecer: siempre, hasta por poco. Lo medido es lo que el contenido necesita.
        Vale(MainWindow.HayQueReajustarElAncho(580, 581), "un píxel más de contenido: se crece");
        Vale(MainWindow.HayQueReajustarElAncho(580, 640), "y mucho más, también");

        // Encoger: solo si sobra de verdad
        Vale(!MainWindow.HayQueReajustarElAncho(580, 579), "uno menos: no se mueve la isla");
        Vale(!MainWindow.HayQueReajustarElAncho(580, 578), "ni dos");
        Vale(!MainWindow.HayQueReajustarElAncho(580, 577), "ni tres");
        Vale(MainWindow.HayQueReajustarElAncho(580, 570), "diez menos sí: eso ya es hueco");

        // Quieto es quieto: el caso de cada segundo cuando no ha pasado nada
        Vale(!MainWindow.HayQueReajustarElAncho(580, 580), "sin cambios, no se toca nada");

        // Y la primera vez hay que ponerle la medida sí o sí
        Vale(MainWindow.HayQueReajustarElAncho(0, 580), "la isla aún sin medir: se ajusta");

        // ---------- Lo que no puede volver a pasar ----------
        //
        // Un ciclo: que al encoger se cruce el listón, y al medir otra vez haya que
        // crecer, y vuelta a empezar. Si eso fuera posible, la isla se movería sola para
        // siempre — que es justo el síntoma del que venimos.
        for (double ancho = 560; ancho <= 600; ancho += 1)
        {
            double pedido = ancho - MainWindow.MargenParaEncoger;   // justo en el filo
            Vale(!MainWindow.HayQueReajustarElAncho(ancho, pedido),
                 $"en el filo ({ancho:0} -> {pedido:0}) la isla se queda quieta");
        }
    }

    // ================= La notch es una isla =================
    //
    // ---------- Qué se fija aquí, y por qué por el negativo ----------
    //
    // La notch entró sin escribir un solo caso nuevo en los diecisiete sitios que preguntan
    // `EsIsla`, y eso se sostiene en UNA decisión: `EsIsla` es `!EsBarra`. El día que alguien
    // la vuelva a escribir como `== "isla"` —que es lo natural de leer y lo que estaba— la
    // notch se queda sin ancho fijado a mano, sin centrarse, sin `Expandir` y sin margen de
    // isla. Y no da ningún error: sale una barra de lado a lado donde se pidió una muesca.
    //
    // De ahí que la primera comprobación sea que la notch ES una isla, y no que "isla" lo es.
    static void LaNotchEsUnaIsla()
    {
        Grupo("notch");

        Igual(true, PanelNube.EsIsla("isla"), "la isla es una isla");
        Igual(true, PanelNube.EsIsla("notch"), "y la notch TAMBIÉN: hereda los diecisiete sitios");
        Igual(false, PanelNube.EsIsla("barra"), "la barra no");
        Igual(false, PanelNube.EsBarra("notch"), "y una notch no es una barra");
        Igual(false, PanelNube.EsVertical("notch", "izquierda"),
              "una notch no se pone vertical ni pedida en un lateral");

        // Los tres modos se reconocen, con mayúsculas, como el resto de los valores del config
        foreach (var m in PanelNube.Modos)
            Vale(PanelNube.ModoValido(m.ToUpperInvariant()), $"«{m}» se reconoce en mayúsculas");
        Igual(false, PanelNube.ModoValido("muesca"), "lo que no es un modo no cuela");
        Igual(false, PanelNube.ModoValido(null), "ni null");
        Igual(3, PanelNube.Modos.Length, "y son tres");

        // ---------- El borde ----------
        //
        // Una muesca en un lateral se dibuja arriba. En isla y en barra el lateral se respeta,
        // porque allí significa algo: anclarse a ese lado, o barra vertical.
        Igual("arriba", PanelNube.BordeEfectivo("notch", "izquierda"), "notch a la izquierda: arriba");
        Igual("arriba", PanelNube.BordeEfectivo("notch", "derecha"), "notch a la derecha: arriba");
        Igual("abajo", PanelNube.BordeEfectivo("notch", "abajo"), "notch abajo: abajo, que es el espejo");
        Igual("izquierda", PanelNube.BordeEfectivo("isla", "izquierda"), "la isla sí se ancla al lado");
        Igual("izquierda", PanelNube.BordeEfectivo("barra", "izquierda"), "y la barra se pone vertical");
        Igual(false, PanelNube.CuelgaDeArriba("notch", "abajo"), "y la de abajo no cuelga de arriba");
        Igual(true, PanelNube.CuelgaDeArriba("notch", "izquierda"), "la del lateral sí, porque está arriba");

        // ---------- Las cuatro esquinas ----------
        //
        // Lo que la hace leerse como un recorte del marco. En el orden de WPF:
        // arriba-izquierda, arriba-derecha, abajo-derecha, abajo-izquierda.
        Igual((20.0, 20.0, 20.0, 20.0), PanelNube.Esquinas("isla", "arriba", 20),
              "la isla redondea las cuatro");
        Igual((20.0, 20.0, 20.0, 20.0), PanelNube.Esquinas("barra", "arriba", 20),
              "y la barra también");
        Igual((0.0, 0.0, 20.0, 20.0), PanelNube.Esquinas("notch", "arriba", 20),
              "la notch de arriba se redondea solo por abajo");
        Igual((20.0, 20.0, 0.0, 0.0), PanelNube.Esquinas("notch", "abajo", 20),
              "y la de abajo, al contrario");
        Igual((0.0, 0.0, 20.0, 20.0), PanelNube.Esquinas("notch", "derecha", 20),
              "y una pedida en un lateral se redondea como la de arriba, porque ES la de arriba");

        // El radio sigue saliendo del TEMA, que es la regla de Forma.cs: una muesca
        // rectangular no es un fallo, es Cyberpunk.
        Igual((0.0, 0.0, 0.0, 0.0), PanelNube.Esquinas("notch", "arriba", 0),
              "con el tema a radio 0, la muesca es un rectángulo");

        // ---------- Y el margen ----------
        //
        // La isla flota y el margen la separa del borde. La notch finge ser el marco, y ocho
        // píxeles de aire entre el borde y la muesca la delatan del todo.
        Igual(0.0, PanelNube.MargenEfectivo("notch", 8), "la notch no flota: margen 0");
        Igual(8.0, PanelNube.MargenEfectivo("isla", 8), "la isla sí, y se queda el que tenga");
        Igual(8.0, PanelNube.MargenEfectivo("barra", 8), "y la barra igual");
    }

    // ================= Y el recorte tiene que llevarlas también =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «Cuando activamos el modo notch y desplegamos un panel, los bordes se vuelven
    // rectangulares.»
    //
    // Las esquinas estaban bien decididas (`PanelNube.Esquinas`, arriba) y bien puestas en el
    // Border (`Isla.CornerRadius`). Lo que no las llevaba era el RECORTE del cajón, que se
    // construía con `RectangleGeometry(rect, radio, radio)`: un solo radio para las cuatro, y
    // el que se le pasaba era `CornerRadius.TopLeft`, que en una notch colgando de arriba vale
    // CERO. Así que al abrir un panel el recorte pasaba a ser un rectángulo, y el recorte
    // manda sobre el `CornerRadius` del Border.
    //
    // Por eso solo se veía con el panel abierto: en reposo no hay recorte.
    static void LaNotchNoPierdeSusEsquinasAlAbrirUnPanel()
    {
        Grupo("notch: el recorte del cajón");

        var r = new Rect(0, 0, 600, 40);
        const double radio = 20;

        // Con las cuatro al radio del tema —isla y barra— las cuatro esquinas quedan fuera.
        var isla = MainWindow.FormaRedondeada(r, new CornerRadius(radio));
        Igual(false, isla.FillContains(new Point(2, 2)), "isla: arriba-izquierda queda fuera de la forma");
        Igual(false, isla.FillContains(new Point(598, 2)), "isla: y arriba-derecha");
        Igual(false, isla.FillContains(new Point(2, 38)), "isla: y abajo-izquierda");
        Vale(isla.FillContains(new Point(300, 20)), "y el centro sí está dentro, no vaya a ser");

        // La notch colgando de arriba: arriba a escuadra —es el filo de la pantalla— y abajo
        // redondo. Es lo que la hace leerse como un recorte del marco.
        var e = PanelNube.Esquinas("notch", "arriba", radio);
        var notch = MainWindow.FormaRedondeada(r, new CornerRadius(e.AI, e.AD, e.BD, e.BI));
        Vale(notch.FillContains(new Point(0.5, 0.5)), "notch: arriba-izquierda SÍ es parte de la forma");
        Vale(notch.FillContains(new Point(599.5, 0.5)), "notch: y arriba-derecha");
        Igual(false, notch.FillContains(new Point(2, 38)), "notch: y las de abajo siguen redondas");
        Igual(false, notch.FillContains(new Point(598, 38)), "notch: las dos");

        // Y la de abajo, al revés, que es el espejo.
        var ab = PanelNube.Esquinas("notch", "abajo", radio);
        var abajo = MainWindow.FormaRedondeada(r, new CornerRadius(ab.AI, ab.AD, ab.BD, ab.BI));
        Vale(abajo.FillContains(new Point(0.5, 39.5)), "notch de abajo: abajo-izquierda es parte de la forma");
        Igual(false, abajo.FillContains(new Point(2, 2)), "notch de abajo: y arriba queda redondo");

        // ---------- Lo que fallaba, escrito ----------
        //
        // Con un radio solo, y encima el de arriba-izquierda, la notch perdía las DOS de abajo.
        var comoEstaba = new RectangleGeometry(r, e.AI, e.AI);
        Vale(comoEstaba.FillContains(new Point(2, 38)),
             "con un radio solo —el de arriba-izquierda— la esquina de abajo salía cuadrada");

        // Un tema a radio 0 da un rectángulo limpio, y eso no es un fallo: es Cyberpunk.
        var cero = MainWindow.FormaRedondeada(r, new CornerRadius(0));
        Vale(cero.FillContains(new Point(0.5, 0.5)), "con el tema a radio 0, la esquina está dentro");
        Vale(cero.FillContains(new Point(599.5, 39.5)), "y la de la otra punta también");

        // Y un radio imposible se topa a la mitad del lado corto en vez de dar la vuelta.
        var gordo = MainWindow.FormaRedondeada(r, new CornerRadius(500)).Bounds;
        Vale(Math.Abs(gordo.Width - r.Width) < 0.01 && Math.Abs(gordo.Height - r.Height) < 0.01,
             "un radio imposible no saca la forma de su rectángulo");

        // ---------- Y ahora la silueta de verdad, con el panel abierto ----------
        //
        // Lo de arriba fija la forma; esto fija que el recorte la USE, que es donde estaba el
        // fallo. La isla mide 300 de alto porque hay un panel desplegado, y la tarjeta cuelga
        // del icono que lo abrió (x=100, ancho 200).
        var conPanel = MainWindow.SiluetaDelCajon(600, 300, 40, arriba: true,
                                                  new CornerRadius(e.AI, e.AD, e.BD, e.BI), 100, 200);
        Vale(conPanel.FillContains(new Point(0.5, 0.5)),
             "notch con un panel abierto: la esquina de arriba sigue a escuadra");
        Vale(conPanel.FillContains(new Point(599.5, 0.5)), "y la otra de arriba también");
        Igual(false, conPanel.FillContains(new Point(2, 38)),
              "y la de abajo de la fila sigue redonda, donde la tarjeta no tapa");
        Vale(conPanel.FillContains(new Point(200, 250)), "la tarjeta cuelga y es parte de la silueta");
        Igual(false, conPanel.FillContains(new Point(50, 100)),
              "y a su lado no hay isla: el cajón solo se ve por donde cuelga la tarjeta");

        // La misma, en isla: ahí las cuatro son redondas también con el panel abierto.
        var islaConPanel = MainWindow.SiluetaDelCajon(600, 300, 40, arriba: true,
                                                      new CornerRadius(radio), 100, 200);
        Igual(false, islaConPanel.FillContains(new Point(0.5, 0.5)),
              "isla con un panel abierto: la esquina de arriba sigue redonda");

        // Y sin panel —el cajón a cero— la silueta es la fila y nada más.
        var recogida = MainWindow.SiluetaDelCajon(600, 40, 40, arriba: true,
                                                  new CornerRadius(e.AI, e.AD, e.BD, e.BI), 100, 200);
        Vale(recogida.FillContains(new Point(0.5, 0.5)), "recogida: arriba a escuadra");
        Igual(false, recogida.FillContains(new Point(2, 38)), "recogida: y abajo redondo");
    }

    // ================= Y la música no puede mover lo que estás leyendo =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «Cuando hay música sonando y se abre un panel se cortan por abajo los iconos, porque
    // la música lo que hace es mover el panel y la nube.»
    //
    // El pulso de audio achata la isla un 2,2 % con el golpe. La escala iba en `Cuerpo`, que
    // con un panel abierto mide la isla ENTERA, y el origen de la escala es su centro: la fila
    // de los módulos, que está en una punta, se iba cuatro píxeles para abajo y se salía del
    // recorte —que no se mueve, porque un Clip se aplica antes del RenderTransform—. De ahí
    // los iconos cortados al ritmo de la canción.
    //
    // Esto NO era del modo notch: pasaba en los tres. Con la nube abajo se cortan por arriba,
    // que es el mismo fallo del revés.
    //
    // Lo que se fija aquí es la regla: lo que se escala tiene que ser la fila de módulos y
    // nada más, para que el alto del panel no entre en la cuenta.
    static void ElPulsoDeAudioNoMueveLoQueEstasLeyendo()
    {
        Grupo("pulso de audio");

        const double barra = 40;
        double esc = MainWindow.EscalaYdelPulso(1);          // a tope de golpe

        Vale(esc < 1 && esc > 0.9, "el pulso achata, y poco: la escala vertical se queda cerca de 1");
        Igual(1.0, MainWindow.EscalaYdelPulso(0), "en silencio no achata nada");
        Vale(MainWindow.EscalaXdelPulso(1) > 1, "y ensancha a la vez, que es la «presión»");

        // Lo que se escala ahora: la fila. No se sale ni a tope de golpe.
        Igual(0.0, MainWindow.SeSaleDelRecorte(barra, barra, esc),
              "escalando la fila de módulos no se sale un solo píxel del recorte");
        Igual(0.0, MainWindow.SeSaleDelRecorte(barra, barra, MainWindow.EscalaYdelPulso(0.5)),
              "ni a medio golpe");

        // Y pase lo que pase con el panel: la fila mide lo que mide, y el cajón no entra en
        // la cuenta. Es toda la diferencia entre el fallo y el arreglo.
        foreach (var altoDelPanel in new[] { 0.0, 120, 300, 520, 900 })
            Igual(0.0, MainWindow.SeSaleDelRecorte(barra, barra, esc),
                  $"con un panel de {altoDelPanel:0} px, la fila sigue sin salirse");

        // Y lo de antes: escalando algo del alto de la isla ENTERA, se sale. Y cuanto más
        // alto el panel, más — que es exactamente lo que se veía.
        double con400 = MainWindow.SeSaleDelRecorte(400, barra, esc);
        double con800 = MainWindow.SeSaleDelRecorte(800, barra, esc);
        Vale(con400 > 3, $"escalando la isla entera con un panel de 400 px se salían {con400:0.0} px de icono");
        Vale(con800 > con400, "y con un panel más alto, más todavía");

        // Con la isla RECOGIDA el cajón mide 0, así que las dos cosas son la misma y el efecto
        // de siempre no cambia. Es lo que hace que el arreglo no se cobre nada.
        Igual(0.0, MainWindow.SeSaleDelRecorte(barra, barra, esc),
              "y con la isla recogida da igual qué se escale: el cajón mide 0");
    }

    // ================= Reservar espacio: una elección, no un modo =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «Hay que hacer una opción para que la nube ocupe un espacio como barra de tareas
    // normal, y opción como ahora, que sea una nube que flote.» La opción ya existía
    // —`Paneles[].ReservarEspacio`, con su interruptor en Ajustes y su tri-estado— y en
    // modo isla no hacía nada: `ReservaHueco` llevaba un `!EsIsla` delante. La
    // encendías, se guardaba en el config, el log decía `reserva=no`, y desde donde el
    // usuario mira eso es que no funciona.
    //
    // Lo que se fija aquí es que la respuesta dependa SOLO de lo que has elegido y del
    // autoocultar. El modo no entra, y la firma de `ReservaLaFranja` es la prueba de que
    // no puede entrar: no recibe ningún modo. Esto no deja volver el candado a
    // escondidas, que es lo que pasó la primera vez.
    static void LaReservaNoDependeDelModo()
    {
        Grupo("reservar espacio");

        // El tri-estado, tal cual
        Igual(false, PanelNube.ReservaEfectiva(null), "sin elegir: la nube flota");
        Igual(true, PanelNube.ReservaEfectiva(true), "elegido sí: se reserva");
        Igual(false, PanelNube.ReservaEfectiva(false), "elegido no: no se reserva");
        Igual(false, PanelNube.ReservaPorDefecto, "el defecto es flotar");

        // Y la regla final: lo elegido manda, el autoocultar manda sobre lo elegido
        Igual(false, PanelNube.ReservaLaFranja(null, autoOculta: false), "sin elegir, visible: no");
        Igual(true, PanelNube.ReservaLaFranja(true, autoOculta: false), "elegido sí, visible: sí");
        Igual(false, PanelNube.ReservaLaFranja(true, autoOculta: true), "elegido sí, pero se autooculta: no");
        Igual(false, PanelNube.ReservaLaFranja(false, autoOculta: false), "elegido no: no");

        // La firma no tiene por dónde mirar el modo. Si alguien añade un parámetro para
        // volver a distinguir isla de barra, esto se cae y hay que leer el comentario de
        // arriba antes de seguir.
        var f = typeof(PanelNube).GetMethod(nameof(PanelNube.ReservaLaFranja));
        Vale(f != null, "ReservaLaFranja es pública y se puede probar en frío");
        var nombres = f?.GetParameters().Select(x => x.Name).ToArray() ?? Array.Empty<string?>();
        Igual(2, nombres.Length, "la reserva se decide con dos cosas y no con tres");
        Vale(!nombres.Any(n => (n ?? "").Contains("modo", StringComparison.OrdinalIgnoreCase)),
             "la reserva no mira el modo: la isla también puede ocupar su sitio");

        // El borde que se le pide a Windows sí depende del modo, y eso está bien: una
        // isla anclada a un lateral sigue siendo horizontal, así que reserva por arriba
        // (ABE_TOP=1) y no por el lado. Una barra en ese mismo borde es vertical de
        // verdad y reserva por la izquierda (ABE_LEFT=0).
        Igual(1u, PanelNube.BordeAppBar("isla", "izquierda"), "isla a la izquierda: reserva por arriba");
        Igual(0u, PanelNube.BordeAppBar("barra", "izquierda"), "barra a la izquierda: reserva por el lado");
        Igual(1u, PanelNube.BordeAppBar("isla", "arriba"), "isla arriba: ABE_TOP");
        Igual(3u, PanelNube.BordeAppBar("isla", "abajo"), "isla abajo: ABE_BOTTOM");
    }

    // ================= El halo del panel lateral =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «En los paneles laterales los bordes redondeados del neón no se ven redondeados, se
    // ven rectangulares» (v1.0.5). El halo estaba enganchado a `VentanaSuperpuesta.Tarjeta`,
    // que tiene `ClipToBounds` —lo necesita: es lo que impide que el contenido asome por las
    // esquinas—, y WPF recorta el resultado del efecto con ese recorte: al RECTÁNGULO de la
    // caja. Un rectángulo de luz alrededor de un panel con las puntas redondas.
    //
    // Es la SEGUNDA vez que pasa. La primera fue la isla, y al arreglarla allí se escribió
    // «los paneles y los laterales sí lo llevan, y ahí se ve bien: sus conchas no recortan»
    // — media verdad, porque la concha de los laterales no es la de `PanelShell`, es esta
    // tarjeta, y sí recorta. Un comentario no impidió la repetición; esta prueba sí.
    //
    // Lo que se exige es la FORMA de la envoltura, no el color de un píxel: que exista una
    // pieza sin recortar donde colgar el efecto, que esté DETRÁS de la tarjeta y no
    // envolviéndola —envolver mete el texto dentro del efecto otra vez, y eso es la
    // borrosidad que se midió en `CapaDelHalo`— y que la tarjeta siga recortando.
    //
    // Va sobre `Envoltura()` y no sobre una ventana de verdad porque `Owner` obliga a que la
    // dueña ya se haya mostrado, y una prueba no abre ventanas.
    static void ElHaloDelPanelLateralNoVaEnLoRecortado()
    {
        Grupo("panel lateral: el halo va en una pieza sin recortar, y detrás");

        var (raiz, aura, tarjeta, cajon) = VentanaSuperpuesta.Envoltura();

        // La regla que se incumplió: el halo necesita algo que NO se recorte.
        Vale(!aura.ClipToBounds, "el aura no recorta: es quien puede llevar el halo");

        // Y la que no se puede perder al arreglarla: el contenido no asoma por las esquinas.
        Vale(tarjeta.ClipToBounds, "la tarjeta sigue recortando su contenido");

        // Detrás, no alrededor. Si el aura envolviera a la tarjeta, el efecto volvería a
        // pintar el texto en una imagen intermedia y los paneles volverían a verse borrosos.
        Vale(!ReferenceEquals(aura.Child, tarjeta), "el aura no envuelve a la tarjeta");
        Vale(aura.Child == null, "el aura no lleva nada escrito dentro que difuminar");
        Igual(2, raiz.Children.Count, "la raíz tiene las dos piezas");
        Igual(0, raiz.Children.IndexOf(aura), "el aura va primero: se pinta detrás");
        Igual(1, raiz.Children.IndexOf(tarjeta), "y la tarjeta encima, opaca y nítida");

        // Y el cajón, donde siempre: dentro de la tarjeta.
        Vale(ReferenceEquals(tarjeta.Child, cajon), "el cajón va dentro de la tarjeta");

        // Un halo no puede comerse los clics del panel que adorna.
        Vale(!aura.IsHitTestVisible, "el aura no recibe ratón");

        // La silueta tiene que seguir a la tarjeta sola. El margen lo mueve `ColocarLateral`
        // en cada colocación —es el aire donde cabe el halo— y las esquinas las cambia el
        // tema; una copia que hay que acordarse de actualizar es una copia que se queda atrás.
        foreach (var (prop, como) in new[]
        {
            ((System.Windows.DependencyProperty)System.Windows.FrameworkElement.MarginProperty, "el margen"),
            (System.Windows.Controls.Border.CornerRadiusProperty, "las esquinas"),
        })
        {
            var enlace = System.Windows.Data.BindingOperations.GetBinding(aura, prop);
            Vale(enlace != null && ReferenceEquals(enlace.Source, tarjeta),
                 $"{como} del aura sigue a la tarjeta por enlace");
        }

        // El fondo NO va enlazado, y es a propósito: el dock y la previsualización usan esta
        // misma clase sin halo, y un relleno heredado les saldría detrás como un rectángulo
        // de su propio color. Lo pone `PintarConchaLateral`, y solo si el estilo brilla.
        Vale(aura.Background == null, "el aura nace sin fondo: lo pone quien quiera halo");
        Vale(System.Windows.Data.BindingOperations.GetBinding(
                 aura, System.Windows.Controls.Border.BackgroundProperty) == null,
             "y el fondo no va enlazado");
    }

    // ================= La barra de desplazamiento propia =================
    //
    // Va en XAML dentro de una cadena: una errata no la ve el compilador, revienta al
    // aplicar el tema y deja la barra de Windows. Aquí se monta de verdad, en los dos
    // sentidos y con temas claros y oscuros.
    static void LaBarraDeScrollEsLaNuestra()
    {
        Grupo("barra de desplazamiento: la nuestra, en vertical y en horizontal");

        foreach (var (tinta, acento) in new[]
        {
            (System.Windows.Media.Colors.White, System.Windows.Media.Color.FromRgb(0x7A, 0xA2, 0xF7)),
            (System.Windows.Media.Colors.Black, System.Windows.Media.Colors.OrangeRed),
        })
        {
            System.Windows.Style? estilo = null;
            try { estilo = BarraDeScroll.Estilo(tinta, acento); }
            catch (Exception ex) { Vale(false, $"el XAML de la barra no se lee: {ex.Message}"); continue; }

            Vale(estilo.TargetType == typeof(System.Windows.Controls.Primitives.ScrollBar),
                 "es un estilo de ScrollBar");

            foreach (var o in new[] { System.Windows.Controls.Orientation.Vertical,
                                      System.Windows.Controls.Orientation.Horizontal })
            {
                var barra = new System.Windows.Controls.Primitives.ScrollBar { Orientation = o, Style = estilo };
                bool montada = false;
                try { montada = barra.ApplyTemplate(); }
                catch (Exception ex) { Vale(false, $"[{o}] la plantilla revienta: {ex.Message}"); continue; }
                Vale(montada && barra.Track != null, $"[{o}] monta su plantilla y tiene carril");
                Vale(barra.Track?.Thumb != null, $"[{o}] y pastilla");
                Igual(o == System.Windows.Controls.Orientation.Vertical, barra.Track?.IsDirectionReversed == true,
                      $"[{o}] y el sentido bueno (arriba es el principio)");
            }
        }
    }

    // ================= El tamaño de los paneles según la pantalla =================
    //
    // En un 1920x1080 al 125 % (864 de alto en unidades de WPF) los paneles salían con las
    // medidas de un 2560x1440 y ocupaban media pantalla.
    static void PanelesSeEscalanConLaPantalla()
    {
        Grupo("paneles: se escalan con el alto de la pantalla");

        Igual(1.0, MainWindow.EscalaDePanelesPura(1440, 2560, 0), "en un 1440 al 100 % salen tal cual");
        Igual(1.0, MainWindow.EscalaDePanelesPura(1080, 1920, 0), "en un 1080 al 100 % también");
        double portatil = MainWindow.EscalaDePanelesPura(864, 1536, 0);
        Vale(portatil < 0.9 && portatil >= MainWindow.EscalaMinima,
             $"en un 1080 al 125 % encogen ({portatil:0.00})");
        double vertical = MainWindow.EscalaDePanelesPura(1920, 1080, 0);
        Vale(vertical < 1.0, $"en un monitor vertical, por estrecho, también ({vertical:0.00})");
        Igual(MainWindow.EscalaMinima, MainWindow.EscalaDePanelesPura(600, 1024, 0),
              "y nunca por debajo del mínimo, que el texto se tiene que leer");
        Igual(0.9, MainWindow.EscalaDePanelesPura(864, 1536, 0.9), "el config.json manda si lo pones");
        Igual(1.5, MainWindow.EscalaDePanelesPura(864, 1536, 7), "pero sin disparates");
    }

    static void IconosOscurosSeReconocen()
    {
        Grupo("iconos: los oscuros se reconocen y los demás no");

        static byte[] Lleno(byte r, byte g, byte b, byte a) =>
            Enumerable.Range(0, 64).SelectMany(_ => new[] { b, g, r, a }).ToArray();

        Vale(Iconos.LuminanciaMedia(Lleno(10, 10, 10, 255)) < Iconos.LuminanciaOscura,
             "un trazo negro es oscuro");
        Vale(Iconos.LuminanciaMedia(Lleno(0, 120, 215, 255)) > Iconos.LuminanciaOscura,
             "el azul de Windows no lo es");
        Vale(Iconos.LuminanciaMedia(Lleno(255, 255, 255, 255)) > Iconos.LuminanciaOscura,
             "el blanco tampoco");
        Vale(Iconos.LuminanciaMedia(Lleno(0, 0, 0, 0)) == null,
             "un icono transparente no se juzga");
    }

    // ================= Y la red de las migraciones =================
    //
    // No va de paneles, pero va de lo mismo: una red de seguridad que existía, se creía
    // puesta y no estaba.
    //
    // Antes de migrar se guarda una copia con la versión de origen en el nombre. Si ese
    // nombre ya estaba cogido —y basta con restaurar una copia vieja y volver a migrar;
    // en una sola sesión pasó tres veces con la misma v3— NO se escribía nada y el registro
    // decía «ya había copia de v3, no se sobrescribe». Suena a que estás cubierto, y lo que
    // tienes es un fichero de otro día con otro contenido.
    static void RespaldoDeMigracionNuncaSePisa()
    {
        Grupo("esquema: cada migración deja su propia copia, y ninguna pisa a la anterior");

        const string cfg = @"C:\x\config.json";
        var ahora = new DateTime(2026, 10, 27, 0, 25, 13);

        // La primera vez, el nombre limpio: es el que se busca a mano cuando hace falta.
        string primera = Esquema.RutaRespaldoQueNoPisa(cfg, 3, _ => false, ahora);
        Igual(@"C:\x\config.json.antes-de-v3.json", primera, "la primera copia lleva el nombre limpio");
        Igual(Esquema.RutaRespaldo(cfg, 3), primera, "que es el de siempre");

        // La segunda, con fecha, y DISTINTA de la primera: eso es lo que antes no pasaba.
        string segunda = Esquema.RutaRespaldoQueNoPisa(cfg, 3, r => r == primera, ahora);
        Vale(segunda != primera, "la segunda no pisa a la primera");
        Vale(segunda.Contains("antes-de-v3-"), $"y lleva la versión de origen ({segunda})");
        Vale(segunda.Contains("20261027"), "y la fecha, para saber de cuándo es");
        Vale(segunda.EndsWith(".json"), "y sigue siendo un .json");

        // Cada versión de origen tiene su serie: migrar de 1 a 4 deja rastro de cada paso.
        foreach (int v in new[] { 1, 2, 3, 4, 17 })
        {
            string r = Esquema.RutaRespaldoQueNoPisa(cfg, v, _ => false, ahora);
            Vale(r.Contains($"antes-de-v{v}."), $"la copia de v{v} se llama por su versión");
        }

        // Y dos migraciones en el mismo segundo desde versiones distintas no chocan.
        string deV2 = Esquema.RutaRespaldoQueNoPisa(cfg, 2, _ => true, ahora);
        string deV3 = Esquema.RutaRespaldoQueNoPisa(cfg, 3, _ => true, ahora);
        Vale(deV2 != deV3, "dos versiones de origen distintas no comparten nombre");
    }

    // Las pantallas de verdad, con su escala. El alto va SIEMPRE en píxeles de diseño: es
    // en lo que mide WPF, y pasarle los físicos daría el doble de sitio del que hay en un
    // monitor al 200 %.
    static readonly (string Nombre, double AltoDip, double Escala)[] PantallasDePanel =
    {
        ("1366x768",            768,  1.0),
        ("1920x1080",          1080,  1.0),
        ("2560x1440",          1440,  1.0),
        ("4K al 150%",         1440,  1.5),   // 2160 físicos
        ("4K al 200%",         1080,  2.0),
        ("1366x768 al 150%",    512,  1.5),   // el caso apretado de verdad
        ("vertical 1080x1920", 1920,  1.0),
        ("vertical al 125%",   1536,  1.25),
    };

    // ================= El tope mira la pantalla =================
    static void PanelesElTopeMiraLaPantalla()
    {
        Grupo("paneles: el tope sale del monitor, la barra y su margen");

        foreach (var (nombre, alto, _) in PantallasDePanel)
            foreach (double barra in new double[] { 30, 38, 44, 56 })
                foreach (double margen in new double[] { 0, 8, 16 })
                {
                    double tope = MainWindow.AltoMaximoDePanel(alto, barra, margen);

                    // Nunca más que la pantalla menos lo que ocupa la barra: si el tope
                    // fuera mayor, la isla crecería fuera del monitor y lo de abajo sería
                    // inalcanzable con el ratón.
                    Vale(tope <= alto - barra || tope == 200,
                         $"[{nombre} barra={barra} margen={margen}] el tope ({tope:0}) " +
                         $"no pasa de la pantalla menos la barra ({alto - barra:0})");

                    // Y nunca cero o negativo: en una pantalla ridícula vale más un panel
                    // que se desplaza que uno de altura imposible.
                    Vale(tope >= 200, $"[{nombre}] y nunca baja de 200 ({tope:0})");

                    // Una barra más gruesa deja MENOS sitio, no más. Parece obvio y es la
                    // forma en que un signo cambiado se escapa.
                    double conBarraGorda = MainWindow.AltoMaximoDePanel(alto, barra + 20, margen);
                    Vale(conBarraGorda <= tope || tope == 200,
                         $"[{nombre}] una barra más gruesa deja menos sitio");
                }

        // El DPI entra por el alto en píxeles de diseño, que es lo correcto: la misma
        // pantalla física al 100 % y al 200 % NO da el mismo tope, porque en píxeles de
        // diseño es media pantalla.
        double cien = MainWindow.AltoMaximoDePanel(2160, 38, 8);
        double doscientos = MainWindow.AltoMaximoDePanel(1080, 38, 8);
        Vale(doscientos < cien,
             $"un 4K al 200 % deja menos sitio de diseño que al 100 % ({doscientos:0} < {cien:0})");
    }

    // ================= O cabe, o se desplaza =================
    static void PanelesOCabeOSeDesplaza()
    {
        Grupo("paneles: o cabe entero o se desplaza por dentro, nunca se corta");

        foreach (double tope in new double[] { 200, 434, 690, 1026, 1362 })
            foreach (double pide in new double[] { 0, 44, 161, 492, 608, 788, 893, 1236, 4000 })
            {
                var (alto, scroll) = MainWindow.ComoCabeElPanel(pide, tope);

                // LA REGLA. Si no se cumple, hay un control por debajo del borde.
                Vale(alto <= tope + 0.001,
                     $"[tope {tope:0}, pide {pide:0}] el alto que se da ({alto:0}) no pasa del tope");
                Vale(scroll || alto >= pide - 0.001,
                     $"[tope {tope:0}, pide {pide:0}] si no hay desplazamiento, cabe entero");
                Vale(!scroll || Math.Abs(alto - tope) < 0.001,
                     $"[tope {tope:0}, pide {pide:0}] y si hay desplazamiento, se usa el tope entero");

                // Y lo que de verdad se quiere decir con las tres de arriba: nada queda
                // fuera sin forma de llegar a ello.
                bool nadaSeQuedaFuera = pide <= alto + 0.001 || scroll;
                Vale(nadaSeQuedaFuera,
                     $"[tope {tope:0}, pide {pide:0}] nada se queda fuera sin desplazamiento");
            }

        // Los casos de los extremos.
        Vale(!MainWindow.ComoCabeElPanel(0, 500).ConScroll, "un panel vacío no pide desplazamiento");
        Vale(!MainWindow.ComoCabeElPanel(500, 500).ConScroll, "justo en el tope, tampoco");
        Vale(MainWindow.ComoCabeElPanel(501, 500).ConScroll, "un píxel más, sí");
        Igual(500.0, MainWindow.ComoCabeElPanel(9999, 500).Alto, "y por mucho que pida, se queda en el tope");
        Igual(0.0, MainWindow.ComoCabeElPanel(-5, 500).Alto, "un alto negativo se queda en cero");
    }

    // ================= En cada pantalla y orientación =================
    static void PanelesEnCadaPantallaYOrientacion()
    {
        Grupo("paneles: los de verdad, en cada pantalla, DPI y posición de barra");

        // Lo que miden los paneles reales, tomado del registro de NubeBar con el panel ya
        // asentado. No son números inventados: son los que salen al abrirlos.
        var paneles = new (string Nombre, double Pide)[]
        {
            ("aviso", 44),
            ("bandeja", 140),
            ("bateria", 161),
            // 401 con DOS monitores físicos, que es lo que hay en la máquina de desarrollo:
            // una tarjeta por monitor. Con una pantalla pide ~243 y con tres ~559, así que es
            // el único panel de esta tabla cuyo alto depende del equipo — y por eso entra
            // también en la ronda de `--diagpaneles`, que lo mide donde se ejecute.
            ("monitores", 401),
            ("notificaciones", 154),
            ("vista", 320),
            ("red", 608),          // el del recorte: 4 tarjetas, con las wifis dentro
            ("audio", 865),
            ("inicio", 1236),      // el más alto, y el que SIEMPRE se desplaza
        };

        foreach (var (pantalla, altoDip, _) in PantallasDePanel)
            foreach (double barra in new double[] { 38, 56 })
                foreach (var (panel, pide) in paneles)
                {
                    double tope = MainWindow.AltoMaximoDePanel(altoDip, barra, 8);
                    var (alto, scroll) = MainWindow.ComoCabeElPanel(pide, tope);

                    // La isla entera —barra más panel— tiene que caber en el monitor. Es la
                    // comprobación que de verdad contesta «¿queda algo fuera de la ventana?».
                    double isla = barra + alto;
                    Vale(isla <= altoDip || tope == 200,
                         $"[{pantalla} barra={barra}] «{panel}» deja la isla en {isla:0} " +
                         $"y la pantalla mide {altoDip:0}");

                    Vale(pide <= alto + 0.001 || scroll,
                         $"[{pantalla} barra={barra}] «{panel}» pide {pide:0}, se le dan {alto:0}" +
                         (scroll ? " y se desplaza" : " SIN desplazamiento"));
                }

        // El panel de red, que es el del fallo, en las pantallas que se pidieron comprobar.
        foreach (var (pantalla, altoDip, escala) in PantallasDePanel)
        {
            double tope = MainWindow.AltoMaximoDePanel(altoDip, 38, 8);
            var (alto, scroll) = MainWindow.ComoCabeElPanel(608, tope);
            Vale(608 <= alto + 0.001 || scroll,
                 $"[{pantalla} al {escala:0.##}x] el panel de red cabe ({alto:0}) o se desplaza ({scroll})");
        }

        // Y en una pantalla pequeña con la barra gruesa, que es donde el tope muerde: tiene
        // que DESPLAZARSE, no cortarse.
        double apretado = MainWindow.AltoMaximoDePanel(512, 56, 8);
        var red = MainWindow.ComoCabeElPanel(608, apretado);
        Vale(red.ConScroll,
             $"en 1366x768 al 150 % con barra de 56, el panel de red se desplaza (tope {apretado:0})");
        Vale(56 + red.Alto <= 512 || apretado == 200,
             "y la isla sigue cabiendo en la pantalla");
    }
}
