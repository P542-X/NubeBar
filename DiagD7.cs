using System;

namespace NubeBar;

// `NubeBar.exe --diagd7`: el supervisor, el modo seguro y el cierre limpio, de verdad.
//
// ---------- Qué hace ----------
//
// Enciende la capa **solo en memoria** y en simulación, registra los atajos de verdad,
// engancha la escucha de reglas, y entonces fuerza el modo seguro para comprobar lo que
// importa: que al entrar en modo seguro se sueltan los atajos y la escucha, y que la isla,
// los paneles y la bandeja **siguen funcionando**. Después sale del modo seguro, lo vuelve a
// montar, lo suelta todo y enseña el inventario.
//
// ---------- Qué NO hace ----------
//
// No mueve ninguna ventana (la capa va en simulación). No toca los escritorios. No escribe
// la configuración (`Config.SoloLectura`), ni siquiera para encender la capa: eso vive en
// memoria y se va con el proceso. Y no mata ni relanza nada: "matar el adaptador" y
// "desconectar un monitor" son pruebas de las tuyas, no de este diagnóstico.
public static class DiagD7
{
    internal const string Bandera = "--diagd7";

    public static bool Encendido;
    public static int Segundos = 10;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 900) Segundos = s;
    }
}
