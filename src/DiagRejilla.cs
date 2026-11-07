using System;

namespace NubeBar;

// `NubeBar.exe --diagrejilla`: «Ver todas las ventanas» con 2, 5, 7 y 12 ventanas **de
// verdad**, midiendo la cuadrícula que sale.
//
// ---------- Por qué esto no estaba ----------
//
// `VistaReparto` es puro y está probado para muchos números de ventanas: cuántas columnas,
// qué tamaño de tarjeta, cómo se reparte la última fila. Eso cubre la DECISIÓN.
//
// Lo que no cubre es que WPF haga lo que la decisión dice. Y ahí es donde se escondían las
// tarjetas de distinto tamaño: el reparto pedía doce iguales y la última fila salía con dos
// estiradas, porque un `UniformGrid` con hueco sobrante lo reparte a su manera. La
// diferencia entre «lo que se pidió» y «lo que se dibujó» no se ve leyendo el código.
//
// Y tampoco se ve en una captura: mirando una imagen se discute si una tarjeta «parece» más
// pequeña. Midiéndola, no se discute.
//
// ---------- Por qué 2, 5, 7 y 12 ----------
//
// No son números redondos a propósito, son los cuatro casos donde se rompe:
//
//  - **2**: el mínimo con el que existe una cuadrícula. Con una no hay reparto.
//  - **5**: primo, así que NINGUNA cuadrícula lo llena — siempre sobra hueco en la última
//    fila. Es el caso que enseña si el reparto equilibra o deja un agujero.
//  - **7**: primo también, y además cae entre dos cuadrículas razonables (3x3 deja dos
//    huecos, 4x2 deja uno). Es donde se ve si la elección mira la forma de la pantalla.
//  - **12**: el primero que llena varias cuadrículas distintas (3x4, 4x3, 6x2) y donde una
//    elección mala se nota a simple vista porque hay tarjetas de sobra para compararlas.
//
// ---------- Las reglas ----------
//
// Las ventanas las crea este diagnóstico —copias renombradas del ejecutable, en
// `--ventanita`—, las usa y las cierra. Ninguna tuya se abre, se mira ni se mueve, y nada
// se escribe en tu configuración.
public static class DiagRejilla
{
    internal const string Bandera = "--diagrejilla";

    public static bool Encendido;

    // Los cuatro casos. Ver arriba por qué estos.
    public static readonly int[] Cuantas = { 2, 5, 7, 12 };

    public static void Leer(string[] args)
    {
        if (args.Length > 0 && args[0] == Bandera) Encendido = true;
    }

    // ---------- Cuándo una última fila deja un agujero ----------
    //
    // Puro, porque «¿esto es un agujero grande?» tiene respuesta aritmética. El hueco de la
    // última fila es `columnas - (n % columnas)` cuando `n` no es múltiplo de `columnas`.
    //
    // Lo que se exige: que sobre **menos de la mitad de una fila**. Con 7 ventanas en 3
    // columnas la última fila lleva 1 y faltan 2 — eso son dos tercios de fila vacía, y se
    // ve. Con 7 en 4 columnas la última lleva 3 y falta 1, que es lo razonable.
    public static int HuecoDeLaUltimaFila(int n, int columnas)
    {
        if (columnas <= 0 || n <= 0) return 0;
        int resto = n % columnas;
        return resto == 0 ? 0 : columnas - resto;
    }

    public static bool LaUltimaFilaEstaBien(int n, int columnas) =>
        columnas > 0 && HuecoDeLaUltimaFila(n, columnas) * 2 < columnas;
}
