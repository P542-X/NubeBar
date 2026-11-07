using System;

namespace NubeBar;

// `NubeBar.exe --arnesaplicar`: el camino completo de aplicar, sobre una ventana NUESTRA.
//
// ---------- Qué cierra ----------
//
// Hasta aquí, `HyprAdaptador.Aplicar` en modo "aplicar" no se había ejecutado nunca: todas
// las fases iban en simulación, y el único `SetWindowPos` real que se ha hecho en todo
// NubeHypr fue el de D2, sobre una sola ventana elegida a mano con su hwnd.
//
// Eso deja una pregunta sin contestar que no se puede contestar con pruebas puras: **¿el
// camino entero funciona?** Regla → decisión → efecto → `Nativo.Colocar` → confirmar
// geometría → contar. Y es la pregunta que hay que tener contestada ANTES de que aplicar de
// verdad pueda encenderse para las ventanas del usuario.
//
// ---------- Las condiciones, que son las mismas que las de D4 ----------
//
// El arnés de escritorios de D4 resolvió el mismo problema con las mismas reglas, y
// funcionaron. Aquí se repiten tal cual:
//
//  - **Ninguna ventana del usuario.** El arnés crea la suya, la usa y la cierra. No enumera
//    las del usuario y no las toca ni para leerlas.
//  - **Ni teclado ni ratón sintéticos.** Nada de `SendKeys`, `SendInput` ni `keybd_event`.
//  - **Se guarda la posición de antes y se devuelve** al terminar.
//  - **Limpieza en `try/finally`**, y si la limpieza falla, se dice y se para.
//  - **No se escribe la configuración** (`Config.SoloLectura`).
//  - La capa se enciende **solo en memoria** y solo durante el arnés.
//
// Lo que este arnés NO hace, y es la mitad de su valor: no conecta las reglas a la
// aplicación automática. Prueba que el camino funciona; encenderlo para las ventanas del
// usuario sigue siendo una decisión que no es mía.
public static class ArnesAplicar
{
    internal const string Bandera = "--arnesaplicar";

    public static bool Encendido;

    // El título de la ventana del arnés. Lleva la hora para que, si alguna vez quedara una
    // suelta, se sepa de qué ejecución fue.
    public static string TituloDePrueba =>
        "NubeHypr: ventana del arnés de aplicar " + DateTime.Now.ToString("HHmmss");

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
    }
}
