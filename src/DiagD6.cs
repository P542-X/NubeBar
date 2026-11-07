using System;

namespace NubeBar;

// `NubeBar.exe --diagd6`: la integración visual medida sobre esta máquina.
//
// ---------- Qué contesta ----------
//
// Las preguntas de D6 que no se pueden contestar con una prueba pura porque dependen del
// equipo: cuántos monitores hay, a qué escala está cada uno, en qué borde está la isla, si
// Windows tiene el alto contraste puesto, y qué contraste real dan el azul y el coral
// contra el tema que el usuario tiene AHORA (no contra los siete de serie).
//
// Es de solo lectura: no mueve ventanas, no abre paneles, no escribe la configuración
// (`Config.SoloLectura`) y no toca los escritorios. Imprime y se cierra.
//
// Lo que este diagnóstico NO puede decir es si se ve bien. Eso necesita ojos, y queda en la
// lista de pruebas manuales pendientes.
public static class DiagD6
{
    internal const string Bandera = "--diagd6";

    public static bool Encendido;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
    }
}
