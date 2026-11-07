using System;
using System.IO;

namespace NubeBar;

// `NubeBar.exe --capturas [carpeta]`: una foto de cada superficie, para poder MIRARLAS.
//
// ---------- Para qué ----------
//
// La lista de "pendiente de tus ojos" de D8 tiene quince casillas, y casi todas se pueden
// contestar mirando una imagen: si un glifo sale como una caja, si una superficie que
// debería ser opaca deja ver el escritorio, si un panel se sale de su sitio, si el texto se
// lee sobre su fondo. Lo que NO se puede ver en una imagen quieta es el parpadeo y el
// movimiento, y eso se queda en la lista manual.
//
// Así que esto abre cada superficie de verdad —con la isla montada, su tema y sus datos— y
// la fotografía con `Nativo.Foto`, que es `PrintWindow` con PW_RENDERFULLCONTENT: el mismo
// camino que usan las miniaturas de la vista general, y el único que funciona con ventanas
// por capas como las nuestras.
//
// ---------- Qué hace y qué no ----------
//
// Abre paneles y los cierra. No mueve ninguna ventana del usuario, no toca los escritorios y
// no escribe la configuración (`Config.SoloLectura`). Las imágenes van a una carpeta
// temporal, no al árbol del proyecto.
public static class DiagCapturas
{
    internal const string Bandera = "--capturas";

    public static bool Encendido;

    // Por defecto, una carpeta con la hora dentro del temporal del sistema: dos ejecuciones
    // seguidas no se pisan, y nada de esto acaba en el repositorio.
    public static string Carpeta =
        Path.Combine(Path.GetTempPath(), "nubebar-capturas",
                     DateTime.Now.ToString("yyyyMMdd-HHmmss"));

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && args[1].Length > 2) Carpeta = args[1];
    }
}
