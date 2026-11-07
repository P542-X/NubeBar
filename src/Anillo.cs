using System;

namespace NubeBar;

// El anillo de progreso: la geometría, sin WPF delante.
//
// ---------- Por qué un anillo y no una barra ----------
//
// Lo pide la maqueta, y tiene su razón: tres cuadros con un anillo cada uno se leen de un
// vistazo —lleno, medio, vacío— sin comparar longitudes. Tres barras horizontales de
// distinta longitud obligan a mirar dónde acaba cada una.
//
// ---------- Por qué la aritmética vive aquí ----------
//
// Dibujar un arco en WPF es un `ArcSegment` con un punto final, un radio, y **dos banderas**
// que deciden si el arco va por el lado corto o por el largo y en qué sentido. Equivocarse en
// cualquiera de las dos da un arco que se ve bien al 30 % y se vuelve del revés al 60 %, que
// es un fallo que solo aparece cuando la máquina va cargada.
//
// Así que el cálculo se hace aquí, donde se puede exigir en una prueba sin pintar nada.
public static class Anillo
{
    // El punto del borde del anillo para una fracción de 0 a 1, con el cero **arriba** y
    // girando en el sentido de las agujas del reloj, que es como lee todo el mundo un
    // progreso circular.
    //
    // `cx`, `cy` el centro; `r` el radio de la línea media del trazo.
    public static (double X, double Y) Punto(double cx, double cy, double r, double parte)
    {
        double a = Math.Clamp(parte, 0, 1) * 2 * Math.PI - Math.PI / 2;   // -90° = arriba
        return (cx + r * Math.Cos(a), cy + r * Math.Sin(a));
    }

    // ¿El arco va por el lado largo? Sí a partir de la mitad. Es la bandera `IsLargeArc` de
    // `ArcSegment`, y ponerla mal hace que el anillo se dibuje por fuera a partir del 50 %.
    public static bool PorElLadoLargo(double parte) => Math.Clamp(parte, 0, 1) > 0.5;

    // ---------- Los dos casos que no son un arco ----------
    //
    // Con 0 no hay nada que dibujar, y con 1 el punto final coincide con el inicial: un arco
    // de 360° es ambiguo para `ArcSegment` —no sabe por dónde ir— y WPF no pinta nada, que es
    // justo lo contrario de lo que uno quiere ver con el disco lleno.
    //
    // Quien dibuja mira esto y, con el anillo lleno, pinta una circunferencia entera en vez
    // de un arco.
    public static bool Vacio(double parte) => Math.Clamp(parte, 0, 1) <= 0.0005;
    public static bool Lleno(double parte) => Math.Clamp(parte, 0, 1) >= 0.9995;

    // De un porcentaje de 0 a 100 a la fracción, aguantando lo que venga: un contador de
    // memoria que devuelve 101 por redondeo no puede dar un anillo de más de una vuelta.
    public static double Parte(double porCiento) => Math.Clamp(porCiento / 100.0, 0, 1);
}
