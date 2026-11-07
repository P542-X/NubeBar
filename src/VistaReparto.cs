using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Cómo se reparte la cuadrícula de «Ver todas las ventanas».
//
// ---------- Qué había antes ----------
//
//     static int Columnas(int cuantas) => clamp(ceil(sqrt(cuantas)), 1, 4);
//     const double AnchoTarjeta = 236, AltoTarjeta = 158;
//
// Dos líneas que no miran la pantalla. De ahí salen, una por una, las cosas que se ven mal:
//
//  · **Tarjetas desproporcionadas.** 236×158 es un tamaño fijo. En un monitor 4K la
//    cuadrícula es un sello en mitad de la pantalla; en un portátil de 1366 px, cuatro
//    columnas miden 944 px y la vista ocupa casi todo el ancho.
//  · **Huecos enormes.** Con cinco ventanas, `ceil(sqrt(5))` = 3 columnas: dos filas, y la
//    segunda con dos tarjetas y un agujero del tamaño de una tercera. Con siete, dos
//    agujeros. Es el caso normal, no uno raro.
//  · **Tope de cuatro columnas.** Con dieciséis ventanas salen cuatro filas de cuatro, se
//    pasa del alto máximo y aparece la rueda — en un monitor donde cabrían seis de ancho
//    sin esfuerzo.
//  · **Nada se reordena al cambiar el número de ventanas**, porque el número de columnas
//    solo se recalcula al abrir.
//
// ---------- Qué hace esto ----------
//
// Una función: cuántas ventanas hay y cuánto sitio hay → cuántas columnas, de qué tamaño y
// con cuánto aire. Elige la repartición MENOS DESIGUAL de las que caben, y escala la
// tarjeta para llenar el ancho que le toca en vez de dejarlo en blanco.
//
// Es pura a propósito: así los casos que de verdad importan —cero ventanas, una, dieciocho,
// un 4K al 175 %, un portátil apaisado de 768 px, una pantalla vertical— se comprueban sin
// abrir una ventana ni tener esos monitores delante.
internal static class VistaReparto
{
    // ---------- Los límites de una tarjeta ----------
    //
    // Mínimo: por debajo de 168 px la miniatura deja de reconocerse y el título no cabe en
    // una línea útil. Máximo: por encima de 340 una ventana sola ocuparía media pantalla
    // para enseñar una miniatura, que no es lo que uno quiere de un conmutador.
    public const double AnchoMinimo = 168;
    public const double AnchoMaximo = 340;

    // La miniatura es 16:10 y debajo va el pie con el icono, el título y el aspa.
    public const double ProporcionMiniatura = 10.0 / 16.0;
    public const double AltoDelPie = 34;

    // El aire entre tarjetas y el margen de la cuadrícula. Proporcionales al tamaño de la
    // tarjeta: con tarjetas pequeñas, 12 px de hueco es un abismo.
    public const double HuecoMinimo = 8;

    public static double HuecoPara(double anchoTarjeta) =>
        Math.Round(Math.Clamp(anchoTarjeta * 0.055, HuecoMinimo, 16));

    // El sitio del que se dispone, en píxeles de diseño (ya divididos por la escala).
    public readonly record struct Sitio(double Ancho, double Alto, double Escala, bool Vertical);

    public readonly record struct Reparto(
        int Columnas,
        int Filas,
        double AnchoTarjeta,
        double AltoTarjeta,
        double Hueco,
        double AnchoTotal,
        double AltoTotal,
        bool ConRueda,          // ¿no caben todas y hay que desplazar?
        int FilasALaVista);     // cuántas filas se ven sin mover la rueda

    // Cuánto del ancho y del alto de la pantalla puede llegar a ocupar la vista. Es un
    // conmutador de ventanas, no una aplicación a pantalla completa: si lo tapa todo, deja
    // de servir para ver dónde estabas.
    public const double ParteDelAncho = 0.82;
    public const double ParteDelAlto = 0.74;

    // El alto que se come la cabecera (título + filtro de apps) y los márgenes.
    public const double Cabecera = 46;
    public const double Margen = 14;

    public static Reparto Para(int cuantas, Sitio sitio)
    {
        double anchoUtil = Math.Max(AnchoMinimo + Margen * 2, sitio.Ancho * ParteDelAncho);
        double altoUtil = Math.Max(160, sitio.Alto * ParteDelAlto) - Cabecera - Margen * 2;

        // ---------- Sin ventanas ----------
        //
        // Un reparto de verdad, no un cero: la vista vacía tiene su propio cartel y hay que
        // darle una caja con una medida razonable, ni un hilo ni media pantalla.
        if (cuantas <= 0)
        {
            double a = Math.Clamp(anchoUtil * 0.6, 280, 420);
            return new Reparto(0, 0, a, 0, HuecoPara(a), a + Margen * 2,
                               Cabecera + 150 + Margen * 2, false, 0);
        }

        // ---------- Cuántas columnas caben ----------
        int maxPorAncho = Math.Max(1, (int)((anchoUtil - Margen * 2 + HuecoMinimo) /
                                            (AnchoMinimo + HuecoMinimo)));
        int tope = Math.Min(cuantas, maxPorAncho);

        int mejor = 1;
        double mejorNota = double.MaxValue;
        for (int c = 1; c <= tope; c++)
        {
            double nota = Nota(cuantas, c, anchoUtil, altoUtil);
            // `<` y no `<=`: en empate gana el número de columnas MENOR, que da tarjetas
            // más grandes. Con cuatro ventanas, 2×2 y 4×1 empatan en desigualdad, y 2×2 es
            // lo que uno espera ver.
            if (nota < mejorNota) { mejorNota = nota; mejor = c; }
        }

        return Armar(cuantas, mejor, anchoUtil, altoUtil);
    }

    // Lo mismo pero con el número de columnas ya decidido. Público porque las pruebas
    // tienen que poder comparar la repartición elegida con las que NO se eligieron: la
    // propiedad que de verdad se quiere garantizar no es «nunca hay huecos» —a veces no
    // existe ninguna repartición sin huecos— sino «no había otra mejor sin obligar a la
    // rueda», y eso solo se puede comprobar mirando también las descartadas.
    public static Reparto ConColumnas(int cuantas, int columnas, Sitio sitio)
    {
        double anchoUtil = Math.Max(AnchoMinimo + Margen * 2, sitio.Ancho * ParteDelAncho);
        double altoUtil = Math.Max(160, sitio.Alto * ParteDelAlto) - Cabecera - Margen * 2;
        return Armar(cuantas, Math.Max(1, columnas), anchoUtil, altoUtil);
    }

    static Reparto Armar(int cuantas, int columnas, double anchoUtil, double altoUtil)
    {
        int filas = (int)Math.Ceiling(cuantas / (double)columnas);

        // ---------- De qué tamaño ----------
        //
        // La tarjeta crece hasta llenar el ancho que le toca. Esto es lo que quita los
        // huecos: el espacio sobrante no se queda en blanco entre columnas, se reparte
        // dentro de las tarjetas.
        double ancho = RepartirElAncho(columnas, anchoUtil);
        double hueco = HuecoPara(ancho);
        ancho = RepartirElAncho(columnas, anchoUtil, hueco);
        double alto = Math.Round(ancho * ProporcionMiniatura) + AltoDelPie;

        // ¿Caben todas las filas? Si no, se desplaza — y las tarjetas NO se encogen por
        // debajo del mínimo para que quepan: una cuadrícula de sellos ilegibles es peor
        // que una rueda.
        int filasQueCaben = Math.Max(1, (int)((altoUtil + hueco) / (alto + hueco)));
        bool conRueda = filas > filasQueCaben;
        int aLaVista = Math.Min(filas, filasQueCaben);

        return new Reparto(
            columnas, filas, ancho, alto, hueco,
            Math.Round(columnas * ancho + (columnas - 1) * hueco + Margen * 2),
            Math.Round(Cabecera + aLaVista * alto + (aLaVista - 1) * hueco + Margen * 2),
            conRueda, aLaVista);
    }

    // Cuántas columnas se han llegado a considerar para ese número de ventanas. Lo usa la
    // prueba para recorrer las descartadas.
    public static int ColumnasPosibles(int cuantas, Sitio sitio)
    {
        double anchoUtil = Math.Max(AnchoMinimo + Margen * 2, sitio.Ancho * ParteDelAncho);
        int maxPorAncho = Math.Max(1, (int)((anchoUtil - Margen * 2 + HuecoMinimo) /
                                            (AnchoMinimo + HuecoMinimo)));
        return Math.Max(1, Math.Min(Math.Max(1, cuantas), maxPorAncho));
    }

    static double RepartirElAncho(int columnas, double anchoUtil, double hueco = HuecoMinimo)
    {
        double libre = anchoUtil - Margen * 2 - hueco * (columnas - 1);
        return Math.Round(Math.Clamp(libre / columnas, AnchoMinimo, AnchoMaximo));
    }

    // ---------- La nota de una repartición: cuanto más baja, mejor ----------
    //
    // Tres cosas, y en este orden de importancia:
    //
    //  1. **Los huecos de la última fila.** Es lo que se ve como «cuadrícula provisional»:
    //     cinco ventanas en tres columnas dejan un agujero del tamaño de una tarjeta. Pesa
    //     mucho, y se mide en huecos, no en proporción: un hueco molesta lo mismo con
    //     cinco ventanas que con quince.
    //  2. **La forma del bloque.** Entre dos reparticiones sin huecos, la que se acerca a
    //     un rectángulo ancho —como se mira una pantalla— gana a la columna larga.
    //  3. **Desaprovechar el ancho.** Pocas columnas con mucho sitio al lado dejan la vista
    //     flaca en medio de un monitor grande.
    internal static double Nota(int cuantas, int columnas, double anchoUtil, double altoUtil)
    {
        int filas = (int)Math.Ceiling(cuantas / (double)columnas);
        int huecos = columnas * filas - cuantas;

        double ancho = RepartirElAncho(columnas, anchoUtil);
        double alto = ancho * ProporcionMiniatura + AltoDelPie;

        // La proporción del bloque. 1.6 es un rectángulo cómodo de mirar; alejarse mucho
        // —hacia la fila larguísima o hacia la columna— penaliza.
        double proporcion = (columnas * ancho) / Math.Max(1, filas * alto);
        double formaMala = Math.Abs(Math.Log(proporcion / 1.6));

        // Lo que sobra de ancho a la derecha, en tarjetas.
        double sobra = Math.Max(0, anchoUtil - Margen * 2 - (columnas * ancho + (columnas - 1) * HuecoMinimo))
                     / Math.Max(1, ancho);

        // Pasarse de alto es peor que quedarse corto: obliga a la rueda, y una cuadrícula
        // con rueda deja de ser una ojeada. Pesa más que cualquier hueco.
        double seSale = (filas * alto > altoUtil) ? 2.5 : 0;

        // Los pesos. Salieron de probar los casos de verdad, no de la intuición, y cada
        // uno sujeta un caso concreto:
        //
        //  · hueco 1.8 > forma 1.6 — porque la queja era de huecos. Con 1.0 el término de
        //    forma ganaba y cinco ventanas en un portátil salían 3x2 (un hueco) en vez de
        //    5x1 (ninguno).
        //  · forma 1.6 y no menos — con 1.2, once ventanas en un 1440p salían en una tira
        //    de once tarjetas de 181 px: cero huecos y un listón ilegible. Ahora salen 4x3.
        //  · rueda 2.5 — con 0.6, trece ventanas en un monitor vertical elegían 2x7, que
        //    deja un hueco menos y no cabe de alto. Cambiar una ojeada por un desplazamiento
        //    para ahorrar un hueco es mal negocio.
        return huecos * 1.8 + formaMala * 1.6 + sobra * 0.5 + seSale;
    }

    // ---------- Y dónde cae cada tarjeta ----------
    //
    // Fila y columna de un índice. Trivial, y aquí porque la prueba de «al cambiar el
    // número de ventanas se reordenan» necesita poder preguntarlo sin una cuadrícula de
    // WPF delante.
    public static (int Fila, int Columna) Sitio2D(int indice, int columnas) =>
        columnas <= 0 ? (0, 0) : (indice / columnas, indice % columnas);

    // Si pasar de `antes` ventanas a `ahora` cambia la forma de la cuadrícula. Lo usa la
    // vista para saber si le basta con quitar una tarjeta o tiene que recolocar.
    public static bool CambiaLaForma(int antes, int ahora, Sitio sitio) =>
        Para(antes, sitio).Columnas != Para(ahora, sitio).Columnas;
}
