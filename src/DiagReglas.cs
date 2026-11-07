using System;

namespace NubeBar;

// `NubeBar.exe --diagreglas [segundos]`: las reglas de D5 contra las ventanas de verdad.
//
// ---------- Qué hace y qué NO ----------
//
// Resuelve las reglas contra las ventanas que hay abiertas ahora mismo y contra las que
// aparezcan durante la espera, y apunta de cada una: la decisión, qué regla puso cada
// campo, por qué las demás no coincidieron, y qué campos se quedaron sin opinión.
//
// **No mueve ninguna ventana, en ningún modo.** No es que esté "en simulación": es que
// este diagnóstico no llama a `SetWindowPos` ni existiendo la posibilidad. D5.1 conecta
// las reglas al momento de aparecer una ventana y nada más; aplicarlas es otra fase.
//
// Y no escribe la configuración: ver `Config.SoloLectura`. Las reglas que se usan son las
// del fichero del usuario si tiene alguna; si no tiene ninguna —que es lo normal, porque
// la lista nace vacía— se usa un juego de ejemplo **solo en memoria**, porque un
// diagnóstico de reglas sin reglas no mide nada.
public static class DiagReglas
{
    internal const string Bandera = "--diagreglas";

    public static bool Encendido;
    public static int Segundos = 20;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 900) Segundos = s;
    }
}
