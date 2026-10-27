using System;

namespace NubeBar;

// `NubeBar.exe --diage0`: el tablero de superficies, contra la realidad.
//
// ---------- Para qué ----------
//
// E0 mete un gestor de superficies que, de momento, va **en paralelo** al `_panelAbierto` de
// siempre: no manda sobre nada, se le cuenta lo que pasa. Eso tiene una ventaja —entra sin
// cambiar lo que el usuario ve— y un riesgo: dos fuentes de verdad que pueden separarse sin
// que nadie se entere, hasta el día que una empiece a mandar.
//
// Este diagnóstico abre los paneles de verdad, uno por uno, y después de cada uno comprueba
// que el tablero dice exactamente lo mismo que la isla. Si se separan, sale aquí.
//
// Solo lectura, como todos: abre paneles y los cierra, no mueve ventanas y no escribe la
// configuración.
public static class DiagE0
{
    internal const string Bandera = "--diage0";

    public static bool Encendido;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
    }
}
