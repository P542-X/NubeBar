using System;

namespace NubeBar;

// `NubeBar.exe --arnesespacios`: cierra D4 probando las tres operaciones que escriben
// —renombrar, reordenar y enviar una ventana— **sin tocar nada del usuario**.
//
// ---------- Las reglas del arnés ----------
//
// Esto ejecuta operaciones destructivas de verdad, así que las reglas no son
// recomendaciones:
//
//  1. **Ninguna ventana del usuario.** El arnés crea la suya, la usa y la cierra. No se
//     mira, no se mueve y no se enumera ninguna otra.
//  2. **Cero inyección de entrada.** Ni `SendKeys`, ni `keybd_event`, ni `SendInput`, ni
//     un clic. Todo se hace llamando a la API, que es lo que se quiere probar.
//  3. **Se guarda el escritorio actual antes de empezar**, y se vuelve a él si algo lo
//     cambió.
//  4. **De los escritorios del usuario solo cambia uno: el que se crea y se borra.** El
//     reordenado mueve el de prueba y lo devuelve al final; al terminar se comprueba que
//     la secuencia de los del usuario es **exactamente** la de antes.
//  5. **La limpieza va en `finally`** y se ejecuta aunque todo lo demás falle. Si la
//     limpieza falla, se restaura el escritorio original y se para.
//  6. **No escribe la configuración** — ver `Config.SoloLectura`.
//
// Y el orden importa: la ventana se cierra ANTES de borrar el escritorio. Borrar un
// escritorio con ventanas dentro las manda a otro, y entonces la limpieza dejaría una
// ventana suelta en un escritorio del usuario.
public static class ArnesEspacios
{
    internal const string Bandera = "--arnesespacios";

    public static bool Encendido;

    // El nombre del escritorio de prueba. Con marca de tiempo para que, si algo saliera
    // tan mal que quedara uno, se sepa de dónde vino y de cuándo.
    public static string NombreDePrueba =>
        "NubeHypr PRUEBA " + DateTime.Now.ToString("HHmmss");

    public static void Leer(string[] args)
    {
        if (args.Length > 0 && args[0] == Bandera) Encendido = true;
    }
}
