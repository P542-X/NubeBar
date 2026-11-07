namespace NubeBar;

// `NubeBar.exe --diagneon [carpeta]`: el halo del neón, fotografiado a lo largo de un ciclo.
//
// ---------- Por qué hace falta un diagnóstico entero para esto ----------
//
// Porque una foto suelta no puede contestar la pregunta. El usuario devolvió dos versiones
// del neón con el mismo reproche, y la segunda vez lo dijo exactamente así: «si en una
// captura solo se ve un borde azul fijo, el neón no está terminado».
//
// Una captura no distingue «brillo fijo» de «brillo que está pasando por su punto medio».
// Para eso hacen falta varias, a tiempos conocidos del ciclo, y mirarlas juntas: si el halo
// crece y se aviva entre la primera y la tercera, respira; si las cuatro salen idénticas, es
// una línea dibujada y no hay más que hablar.
//
// ---------- Lo que mide ----------
//
// Abre el panel izquierdo, lo deja quieto y dispara seis fotos repartidas por DOS ciclos
// completos: así se ve que vuelve a empezar y no que creció una vez y se quedó.
//
// Y apunta el instante de cada una respecto al ciclo, para que al mirarlas se sepa cuál
// debería ser el mínimo y cuál el máximo en vez de adivinarlo.
public static class DiagNeon
{
    internal const string Bandera = "--diagneon";

    public static bool Encendido;
    public static string? Carpeta;

    // Seis disparos. Con el ciclo de serie (2,6 s) salen a 0 s, 0,65 s, 1,3 s, 1,95 s, 2,6 s
    // y 3,9 s: mínimo, subida, máximo, bajada, mínimo otra vez y el máximo del ciclo
    // siguiente. Las fracciones son del ciclo, no milisegundos fijos, para que sigan cayendo
    // donde toca si cambias la velocidad en Ajustes.
    public static readonly double[] Momentos = { 0, 0.25, 0.5, 0.75, 1.0, 1.5 };

    public static readonly string[] QueTocaVer =
    {
        "mínimo (arranque del ciclo)",
        "creciendo",
        "MÁXIMO del primer ciclo",
        "contrayéndose",
        "mínimo otra vez (fin del ciclo)",
        "MÁXIMO del segundo ciclo",
    };

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && args[1].Length > 2) Carpeta = args[1];
    }
}
