using System;

namespace NubeBar;

// `NubeBar.exe --arneslayout`: cierra **D9.6** —una regla que fija el layout del escritorio
// mueve ventanas que ninguna regla ha nombrado— probándolo contra Windows de verdad.
//
// ---------- Por qué esto faltaba, y por qué ahora se puede ----------
//
// D9.6 llevaba desde que se escribió marcado así en `PRUEBAS-PARA-PAULO.md`:
//
//   «⚠️ esto NO lo he probado moviendo ventanas. Es la única parte que mueve ventanas que
//    ninguna regla ha nombrado (repartir un escritorio es, por definición, moverlas todas),
//    y comprobarlo de verdad significaba recolocar tus ventanas abiertas.»
//
// Eso era cierto **en el escritorio del usuario**. Pero `--arnesespacios` lleva desde D4
// creando su propio escritorio virtual, usándolo y borrándolo, con reglas estrictas y
// pasando en verde en cada regresión. En un escritorio recién creado no hay ni una ventana
// del usuario: repartirlo entero no le toca nada a nadie.
//
// Así que la prueba que no se podía hacer sí se puede, cambiando dónde se hace. Esa es toda
// la idea de este arnés.
//
// ---------- Las reglas, que no son recomendaciones ----------
//
//  1. **Ninguna ventana del usuario.** Las tres ventanas las crea este arnés —copias
//     renombradas del propio ejecutable, en `--ventanita`—, las usa y las cierra. No se
//     mueve, no se mira y no se enumera ninguna otra.
//  2. **Ningún escritorio del usuario.** Se crea uno, se va a él, se reparte DENTRO y se
//     borra. Al terminar se comprueba que la secuencia de los tuyos es exactamente la de
//     antes, igual que hace `--arnesespacios`.
//  3. **Se vuelve a tu escritorio** pase lo que pase, desde el `finally`.
//  4. **Cero inyección de entrada.** Ni `SendKeys`, ni `keybd_event`, ni `SendInput`, ni un
//     clic. Las ventanas nacen por `Process.Start` y el reparto lo dispara el mismo camino
//     que en el producto: la regla al aparecer una ventana.
//  5. **No escribe la configuración.** La capa se enciende EN MEMORIA y se devuelve a como
//     estaba; además `Config.SoloLectura` lo impide.
//  6. **Las ventanas se cierran ANTES de borrar el escritorio.** Borrar un escritorio con
//     ventanas dentro las manda a otro, y la limpieza dejaría tres ventanas sueltas en un
//     escritorio tuyo. Es la misma lección que ya aprendió `--arnesespacios`.
//
// ---------- Por qué TRES ventanas ----------
//
// Con una no hay reparto: un layout de una ventana es la ventana a pantalla completa, y eso
// no distingue «ha repartido» de «la ha maximizado». Con dos, un reparto correcto y uno que
// se equivoque de mitad dan rectángulos simétricos y cuesta ver la diferencia. Con tres, el
// reparto tiene una forma que solo sale si la cuenta está bien: tres columnas que no se
// solapan, que cubren el ancho y que caben en el área útil.
public static class ArnesLayout
{
    internal const string Bandera = "--arneslayout";

    public static bool Encendido;

    // El nombre del escritorio de prueba. Reconocible a simple vista por si una limpieza
    // fallara y quedara uno suelto: así sabes de dónde salió y que lo puedes borrar.
    public const string NombreDePrueba = "NubeBar · prueba de layout (borrar si la ves)";

    // Cuántas ventanas. Ver la nota de arriba sobre por qué tres.
    public const int Cuantas = 3;

    // Lo que puede separarse el reparto medido del calculado, en píxeles. No es cero porque
    // Windows redondea y algunas ventanas tienen tamaño mínimo propio; es pequeño porque un
    // margen generoso aquí dejaría pasar un reparto equivocado.
    public const int Tolerancia = 16;

    public static void Leer(string[] args)
    {
        if (args.Length > 0 && args[0] == Bandera) Encendido = true;
    }

    // ---------- Lo que se le exige al reparto ----------
    //
    // Puro para poder exigirlo sin ventanas delante, y porque la pregunta «¿esto es un
    // reparto o un amontonamiento?» tiene respuesta geométrica y no de opinión.
    public sealed record Veredicto(bool Bien, string Porque);

    public static Veredicto MiraElReparto(System.Collections.Generic.IReadOnlyList<NubeHypr.Rect> sitios,
                                          NubeHypr.Rect area, int tolerancia)
    {
        if (sitios.Count < 2) return new Veredicto(false, $"solo hay {sitios.Count} ventana(s)");

        // 1. Todas dentro del área útil. Una ventana repartida fuera de la pantalla es el
        //    fallo más caro de todos: no se ve y no se recupera sin teclado.
        for (int i = 0; i < sitios.Count; i++)
        {
            var s = sitios[i];
            if (s.X < area.X - tolerancia || s.Y < area.Y - tolerancia ||
                s.X + s.Ancho > area.X + area.Ancho + tolerancia ||
                s.Y + s.Alto > area.Y + area.Alto + tolerancia)
                return new Veredicto(false, $"la ventana {i + 1} se sale del área útil: {s} fuera de {area}");
        }

        // 2. Ninguna encima de otra. Es LA diferencia entre repartir y amontonar, y es la
        //    comprobación que un «se movieron 3 ventanas» no hace: tres ventanas pueden
        //    moverse las tres al mismo sitio.
        for (int i = 0; i < sitios.Count; i++)
            for (int j = i + 1; j < sitios.Count; j++)
                if (SeSolapanDeVerdad(sitios[i], sitios[j], tolerancia))
                    return new Veredicto(false,
                        $"las ventanas {i + 1} y {j + 1} se pisan: {sitios[i]} contra {sitios[j]}");

        // 3. Y que de verdad hayan ocupado la pantalla, no que estén las tres apiladas en una
        //    esquina sin tocarse. Un reparto que deja media pantalla vacía con tres ventanas
        //    dentro no es un reparto.
        double suma = 0;
        foreach (var s in sitios) suma += s.Ancho * s.Alto;
        double deLaPantalla = area.Ancho * area.Alto;
        if (deLaPantalla <= 0) return new Veredicto(false, "el área útil es cero");

        double parte = suma / deLaPantalla;
        if (parte < 0.5)
            return new Veredicto(false,
                $"entre las {sitios.Count} solo ocupan el {parte * 100:0}% de la pantalla: eso no es repartir");

        return new Veredicto(true,
            $"{sitios.Count} ventanas, ninguna fuera, ninguna encima de otra, " +
            $"ocupan el {parte * 100:0}% del área útil");
    }

    // Solaparse "de verdad": compartir más que el borde. Dos ventanas pegadas comparten una
    // línea de píxeles cuando el hueco es 0, y llamar a eso solaparse daría rojo a un
    // reparto perfecto.
    static bool SeSolapanDeVerdad(NubeHypr.Rect a, NubeHypr.Rect b, int tolerancia) =>
        a.X + a.Ancho - tolerancia > b.X && b.X + b.Ancho - tolerancia > a.X &&
        a.Y + a.Alto - tolerancia > b.Y && b.Y + b.Alto - tolerancia > a.Y;
}
