using System;
using System.Linq;

namespace NubeBar;

// La cuadrícula de «Ver todas las ventanas», en pantallas que no tengo delante.
//
// El reparto es pura aritmética (`VistaReparto`), así que un 4K al 175 %, un portátil de
// 768 px y un monitor vertical se comprueban igual de bien que el de esta mesa. Que es la
// razón de haberlo sacado de la interfaz: antes vivía en dos líneas dentro de
// `MainWindow.Vista.cs` y no había forma de mirarlo sin el monitor correspondiente.
public static partial class Pruebas
{
    internal static void CorrerVista()
    {
        VistaLosNumerosDeVentanasQueImportan();
        VistaEnCadaPantalla();
        VistaSinHuecosFeos();
        VistaSeReordenaAlCambiar();
        VistaUnaFotoLisaNoEsUnaFoto();
    }

    // ================= Una miniatura en blanco no vale =================
    static void VistaUnaFotoLisaNoEsUnaFoto()
    {
        Grupo("vista: una miniatura lisa se descarta y queda el icono");

        // Veinticinco puntos iguales = `PrintWindow` ha devuelto «bien» y un rectángulo
        // vacío, que es lo que hacen Discord, ChatGPT y cualquier Electron. Se veía como
        // tarjetas grises sin nada dentro.
        var lisa = Enumerable.Repeat(0xFF1E1E1Eu, 25).ToList();
        Vale(Miniaturas.TodasIguales(lisa), "veinticinco puntos iguales: no hay ventana ahí");

        var negra = Enumerable.Repeat(0u, 25).ToList();
        Vale(Miniaturas.TodasIguales(negra), "y un mapa entero en negro, tampoco");

        // Un solo punto distinto basta para que sea una ventana de verdad: con el marco,
        // el borde o el cursor ya no es lisa.
        var casiLisa = Enumerable.Repeat(0xFF1E1E1Eu, 25).ToList();
        casiLisa[13] = 0xFF1E1E1Fu;                       // un bit de diferencia
        Vale(!Miniaturas.TodasIguales(casiLisa),
             "con un solo punto distinto ya es una ventana: no se tira la miniatura");

        // Y lo que no se puede juzgar, no se juzga: quitarle la miniatura a una ventana
        // por no haber podido mirarla bien sería cambiar un fallo por otro.
        Vale(!Miniaturas.TodasIguales(new List<uint> { 7, 7, 7 }),
             "con menos de cuatro muestras no se decide nada");
        Vale(!Miniaturas.TodasIguales(new List<uint>()), "ni con ninguna");
    }

    // Las pantallas de verdad, en píxeles de diseño (ya divididos por la escala).
    static readonly (string Nombre, VistaReparto.Sitio Sitio)[] Pantallas =
    {
        ("portátil 1366x768",      new(1366, 768, 1.0, false)),
        ("1080p",                  new(1920, 1080, 1.0, false)),
        ("1440p",                  new(2560, 1440, 1.0, false)),
        ("4K al 150%",             new(2560, 1440, 1.5, false)),   // 3840x2160 físicos
        ("4K al 175%",             new(2194, 1234, 1.75, false)),
        ("vertical 1080x1920",     new(1080, 1920, 1.0, true)),
        ("vertical al 125%",       new(864, 1536, 1.25, true)),
        ("pequeña 1280x720",       new(1280, 720, 1.0, false)),
    };

    // Los números de ventanas que pidió el usuario, más los que rompen cosas.
    static readonly int[] Cuantas = { 0, 1, 2, 3, 4, 5, 6, 7, 9, 12, 16, 18, 24, 40 };

    // ================= Cero, una, dos, seis, doce y muchas =================
    static void VistaLosNumerosDeVentanasQueImportan()
    {
        Grupo("vista: el reparto con 0, 1, 2, 6, 12 y muchas ventanas");

        var p = new VistaReparto.Sitio(1920, 1080, 1.0, false);

        var vacia = VistaReparto.Para(0, p);
        Igual(0, vacia.Columnas, "sin ventanas no hay columnas");
        Vale(vacia.AnchoTotal >= 280 && vacia.AnchoTotal <= 460,
             $"pero la vista vacía tiene una caja razonable ({vacia.AnchoTotal:0} px)");
        Vale(!vacia.ConRueda, "y no pide rueda");

        var una = VistaReparto.Para(1, p);
        Igual(1, una.Columnas, "una ventana, una columna");
        Igual(1, una.Filas, "y una fila");
        Vale(una.AnchoTarjeta <= VistaReparto.AnchoMaximo,
             "y la tarjeta no se come media pantalla por estar sola");

        Igual(2, VistaReparto.Para(2, p).Columnas, "dos ventanas, dos columnas en fila");
        Igual(1, VistaReparto.Para(2, p).Filas, "y una sola fila");

        var seis = VistaReparto.Para(6, p);
        Igual(0, seis.Columnas * seis.Filas - 6, "seis ventanas se reparten sin dejar huecos");

        var doce = VistaReparto.Para(12, p);
        Igual(0, doce.Columnas * doce.Filas - 12, "y doce, tampoco");

        // Muchas: lo que no puede pasar es que la tarjeta se encoja hasta no verse.
        var muchas = VistaReparto.Para(40, p);
        Vale(muchas.AnchoTarjeta >= VistaReparto.AnchoMinimo,
             $"con cuarenta ventanas la tarjeta sigue siendo legible ({muchas.AnchoTarjeta:0} px)");
        Vale(muchas.ConRueda, "y lo que no cabe se desplaza, en vez de encogerse");
        Vale(muchas.FilasALaVista >= 2, "con al menos dos filas a la vista");
    }

    // ================= En cada pantalla =================
    static void VistaEnCadaPantalla()
    {
        Grupo("vista: en cada pantalla, DPI y orientación");

        foreach (var (nombre, sitio) in Pantallas)
            foreach (int n in Cuantas)
            {
                var r = VistaReparto.Para(n, sitio);

                // ---------- No se sale de la pantalla ----------
                //
                // Lo que de verdad rompe la vista en un portátil pequeño. Y se comprueba
                // contra el sitio de VERDAD, no contra el que la vista cree tener.
                Vale(r.AnchoTotal <= sitio.Ancho,
                     $"[{nombre}, {n}] la vista cabe de ancho ({r.AnchoTotal:0} de {sitio.Ancho:0})");
                Vale(r.AltoTotal <= sitio.Alto,
                     $"[{nombre}, {n}] y de alto ({r.AltoTotal:0} de {sitio.Alto:0})");

                if (n == 0) continue;

                // ---------- Las tarjetas son legibles y proporcionadas ----------
                Vale(r.AnchoTarjeta >= VistaReparto.AnchoMinimo,
                     $"[{nombre}, {n}] la tarjeta no baja del mínimo ({r.AnchoTarjeta:0})");
                Vale(r.AnchoTarjeta <= VistaReparto.AnchoMaximo,
                     $"[{nombre}, {n}] ni pasa del máximo ({r.AnchoTarjeta:0})");

                double proporcion = r.AnchoTarjeta / Math.Max(1, r.AltoTarjeta);
                Vale(proporcion > 1.0 && proporcion < 2.2,
                     $"[{nombre}, {n}] y tiene forma de ventana, no de tira ({proporcion:0.00})");

                // ---------- Caben todas ----------
                Vale(r.Columnas * r.Filas >= n,
                     $"[{nombre}, {n}] hay sitio para las {n}");

                // ---------- Y el hueco es aire, no un abismo ----------
                Vale(r.Hueco >= VistaReparto.HuecoMinimo && r.Hueco <= 16,
                     $"[{nombre}, {n}] el hueco entre tarjetas es {r.Hueco:0} px");
            }

        // La misma pantalla a distinta escala da tarjetas del mismo tamaño EN PÍXELES DE
        // DISEÑO: es lo que hace que se vean igual de grandes en un 4K al 150 % que en un
        // 1440p, que es lo que uno espera de un escalado que funciona.
        var a = VistaReparto.Para(9, new VistaReparto.Sitio(2560, 1440, 1.0, false));
        var b = VistaReparto.Para(9, new VistaReparto.Sitio(2560, 1440, 1.5, false));
        Igual(a.AnchoTarjeta, b.AnchoTarjeta, "la escala no cambia el tamaño en píxeles de diseño");
        Igual(a.Columnas, b.Columnas, "ni el número de columnas");
    }

    // ================= Sin huecos feos =================
    static void VistaSinHuecosFeos()
    {
        Grupo("vista: la última fila no se queda medio vacía");

        // EL CASO. Cinco ventanas con el reparto viejo —`ceil(sqrt(5))` = 3 columnas— daban
        // dos filas y un agujero del tamaño de una tarjeta en la esquina. Con siete, dos
        // agujeros. No era un caso raro: es el número de ventanas que tiene cualquiera.
        //
        // La propiedad que se comprueba NO es «nunca hay huecos»: a veces no existe
        // ninguna repartición sin huecos que quepa en la pantalla —trece ventanas en un
        // monitor vertical estrecho, por ejemplo: las reparticiones con menos huecos son
        // columnas larguísimas que no caben de alto—. Lo que sí se puede exigir, y es lo
        // que de verdad se quería, es que **no hubiera una mejor**: si existía una forma de
        // dejar la última fila más llena sin obligar a la rueda, hay que haberla elegido.
        foreach (var (nombre, sitio) in Pantallas)
            foreach (int n in new[] { 3, 5, 7, 10, 11, 13, 14 })
            {
                var r = VistaReparto.Para(n, sitio);
                int huecos = r.Columnas * r.Filas - n;

                // La exigencia: la última fila está al menos medio llena.
                bool mediaLlena = huecos <= r.Columnas / 2;

                // Y la salida honesta para las geometrías donde eso no se puede: que no
                // hubiera otra repartición con menos huecos que también cupiera.
                var mejorQueCabe = Enumerable.Range(1, VistaReparto.ColumnasPosibles(n, sitio))
                    .Select(c => VistaReparto.ConColumnas(n, c, sitio))
                    .Where(x => !x.ConRueda)
                    .Select(x => x.Columnas * x.Filas - n)
                    .DefaultIfEmpty(huecos)
                    .Min();

                Vale(mediaLlena || huecos <= mejorQueCabe,
                     $"[{nombre}, {n}] {r.Columnas}x{r.Filas} deja {huecos} hueco(s); " +
                     $"media fila serían {r.Columnas / 2} y sin rueda no se baja de {mejorQueCabe}");
            }

        // Y en concreto, el que se quejaba.
        var cinco = VistaReparto.Para(5, new VistaReparto.Sitio(1920, 1080, 1.0, false));
        Vale(cinco.Columnas * cinco.Filas - 5 <= 1,
             $"cinco ventanas ya no dejan un agujero grande ({cinco.Columnas}x{cinco.Filas})");

        // Cuatro ventanas: 2x2, no 4x1. Empatan en huecos y gana la forma.
        var cuatro = VistaReparto.Para(4, new VistaReparto.Sitio(1920, 1080, 1.0, false));
        Igual(2, cuatro.Columnas, "cuatro ventanas salen en 2x2");
        Igual(2, cuatro.Filas, "con dos filas");
    }

    // ================= Se reordena al cambiar =================
    static void VistaSeReordenaAlCambiar()
    {
        Grupo("vista: al cambiar el número de ventanas, la cuadrícula se recoloca");

        var p = new VistaReparto.Sitio(1920, 1080, 1.0, false);

        // Cerrar una ventana puede cambiar la forma, y entonces hay que recolocar y no
        // solo quitar una tarjeta. El reparto viejo solo se calculaba AL ABRIR, así que
        // cerrar la cuarta de cuatro dejaba tres tarjetas en una rejilla de dos columnas
        // con un agujero donde estaba la cerrada.
        Vale(VistaReparto.CambiaLaForma(4, 3, p), "de cuatro a tres cambia la forma");
        Vale(VistaReparto.CambiaLaForma(2, 3, p), "y de dos a tres también");
        Vale(!VistaReparto.CambiaLaForma(6, 6, p), "y el mismo número no cambia nada");

        // Y los sitios salen en el orden de lectura: izquierda a derecha, arriba abajo.
        var r = VistaReparto.Para(7, p);
        Igual((0, 0), VistaReparto.Sitio2D(0, r.Columnas), "la primera va arriba a la izquierda");
        Igual((0, 1), VistaReparto.Sitio2D(1, r.Columnas), "la segunda, a su derecha");
        Igual((1, 0), VistaReparto.Sitio2D(r.Columnas, r.Columnas),
              "y la de después de la última de la fila baja a la siguiente");

        // Ninguna tarjeta cae fuera de la cuadrícula.
        for (int i = 0; i < 7; i++)
        {
            var (fila, col) = VistaReparto.Sitio2D(i, r.Columnas);
            Vale(fila < r.Filas && col < r.Columnas, $"la tarjeta {i} cae dentro ({fila},{col})");
        }
    }
}
