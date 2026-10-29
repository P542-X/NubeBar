using System;

namespace NubeBar;

// `NubeBar.exe --diagespacios [segundos]`: lee los escritorios virtuales y los enseña.
//
// Solo lectura, y a propósito: crear escritorios o renombrarlos en el equipo de alguien
// para ver si la API responde es pasarse. Lo que se comprueba es que la API contesta, que
// la política ordena y nombra bien lo que contesta, que el mapa de ventana a espacio se
// rellena, y que un cambio hecho a mano durante la medida se detecta.
public static class DiagEspacios
{
    internal const string Bandera = "--diagespacios";

    public static bool Encendido;
    public static int Segundos = 12;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 300) Segundos = s;
    }
}
