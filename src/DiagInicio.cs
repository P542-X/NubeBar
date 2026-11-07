using System;

namespace NubeBar;

// `NubeBar.exe --diaginicio`: arranca NubeBar de verdad, abre el panel de Inicio, mide
// lo que ha salido en pantalla, lo apunta y se cierra.
//
// ---------- Por qué hace falta ----------
//
// Porque "el panel ocupa casi todo el ancho" no se puede resolver leyendo el código. El
// código dice que pide 660; lo que hay que saber es cuánto mide **lo que se ha pintado**,
// si la raíz es una superficie opaca y si el cajón se ha recortado a la tarjeta o se ha
// desplegado de punta a punta de la isla. Las tres cosas solo existen después de que WPF
// haya hecho el layout.
//
// Y porque sin esto la única forma de comprobarlo es que una persona abra el panel y lo
// mire — que es lo que hizo falta para descubrir que lo que se estaba ejecutando era un
// binario de hace una hora.
//
// Mide, además, que Inicio y Audio siguen siendo paneles distintos: abrirlos uno detrás
// de otro y ver que el nombre y el ancho cambian.
public static class DiagInicio
{
    internal const string Bandera = "--diaginicio";

    public static bool Encendido;

    // "--diaginicio 35": deja Inicio abierto esos segundos en vez de medir y cerrar. Es
    // lo que permite comprobar que la música sigue sonando con la superficie abierta.
    public static int Segundos;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 300) Segundos = s;
    }
}
