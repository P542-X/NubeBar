using System;

namespace NubeBar;

// `NubeBar.exe --diagatajos [segundos]`: registra los 19 atajos de verdad, dice qué pasó
// con cada uno, los suelta y comprueba que no queda ninguno.
//
// ---------- Por qué hace falta registrarlos de verdad ----------
//
// El mapa detecta sus propios conflictos —repetidos, nombres que no existen, choque con el
// atajo de la vista general— pero eso es aritmética sobre una tabla. **Quién tiene cogida
// de verdad `Win+Alt+→` en este equipo solo lo dice `RegisterHotKey` devolviendo 1409.**
//
// Así que este modo los registra, recoge los códigos reales de Windows, y los suelta. Y
// durante los segundos que se le digan los deja vivos, para poder pulsarlos y ver en el
// log si llegan.
//
// Enciende la capa **solo en memoria** y en modo simulación: no escribe la configuración
// —ver `Config.SoloLectura`— y no mueve ninguna ventana.
public static class DiagAtajos
{
    internal const string Bandera = "--diagatajos";

    public static bool Encendido;
    public static int Segundos = 20;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 300) Segundos = s;
    }
}
