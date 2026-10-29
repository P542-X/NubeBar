using System;

namespace NubeBar;

// `NubeBar.exe --diagprefijos`: ¿qué prefijo de atajos está libre en ESTE equipo?
//
// ---------- Para qué ----------
//
// El prefijo de los 19 atajos de NubeHypr se eligió a ojo (`Win+Alt`) y la medición de D3
// dijo que era mala elección: 9 de los 19 los tenía cogidos otra aplicación. Cambiarlo a
// otro elegido también a ojo sería repetir el error con otro nombre.
//
// Esto registra de verdad cada candidato con las 19 teclas, cuenta cuántos entran, dice qué
// pasa con cada uno que no entra, y **los suelta todos**. Lo que queda es una tabla con la
// que decidir.
//
// ---------- Qué hace y qué no ----------
//
// `RegisterHotKey` + `UnregisterHotKey`, nada más. No inyecta pulsaciones, no mueve
// ventanas, no escribe la configuración (`Config.SoloLectura`) y suelta todo lo que
// registra, también si algo falla por el camino.
//
// El único efecto observable mientras corre es que durante unos milisegundos esas
// combinaciones son de NubeBar. Si pulsaras una justo en ese momento no pasaría nada: el
// diagnóstico no atiende los avisos, solo mira si el registro se concede.
public static class DiagPrefijos
{
    internal const string Bandera = "--diagprefijos";

    public static bool Encendido;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
    }
}
