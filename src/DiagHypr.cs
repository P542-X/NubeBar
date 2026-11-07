using System;

namespace NubeBar;

// `NubeBar.exe --diaghypr [segundos]`: la línea base de D0.
//
// ---------- Qué contesta ----------
//
// La prueba que pide D0 es "comparativa de geometría antes y después en modo apagado".
// Dicho sin rodeos: con la capa de ventanas presente pero apagada, ninguna ventana del
// usuario puede haberse movido ni un píxel.
//
// Eso no se puede comprobar leyendo el código —la capa no mueve nada *todavía*, así que
// cualquier lectura diría que está bien— ni mirando la pantalla, porque un
// desplazamiento de veinte píxeles en una ventana de atrás no se ve. Se comprueba
// midiendo: una foto de la geometría de todas las ventanas, el rato que se pida, y otra
// foto al final.
//
// Y de paso deja escrito el inventario que D0 pide: qué ventanas se podrían organizar
// cuando el motor exista, y cuáles no y por qué. Es la lista de procesos protegidos
// comprobada contra las ventanas reales de este equipo, no contra una suposición.
//
// No toca nada: ni mueve, ni activa, ni escribe en el config.
public static class DiagHypr
{
    internal const string Bandera = "--diaghypr";

    public static bool Encendido;
    public static int Segundos = 20;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 900) Segundos = s;
    }
}
