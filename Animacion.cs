using System;
using System.Windows;

namespace NubeBar;

public enum NivelAnimacion { Completo, Equilibrado, Reducido }

// Cuánto se mueve la isla.
//
// El truco está en que no hay que repasar las treinta animaciones del proyecto: todas
// pasan por los dos ayudantes de MainWindow (Kf para las de números y Ms para las de
// color), así que basta con escalar el tiempo ahí. Y funciona porque el código ya
// cumple la regla de la casa: el valor de verdad de cada propiedad se fija ANTES de
// animar, y la animación solo dibuja el camino. Con el factor a cero no hay camino
// que dibujar y todo aparece ya en su sitio, sin saltos ni restos.
//
// Los adornos puros —el icono que se levanta al pasar el ratón, el latido de una app
// que arranca, el destello del borde— no tienen estado que conservar, así que en
// "reducido" ni se lanzan: se preguntan por Adorno.
public static class Animacion
{
    public static NivelAnimacion Nivel { get; private set; } = NivelAnimacion.Completo;

    // Equilibrado no quita animaciones, las acorta: se nota igual de vivo y responde
    // antes. Reducido las deja en cero.
    public static double Factor => Nivel switch
    {
        NivelAnimacion.Completo => 1.0,
        NivelAnimacion.Equilibrado => 0.6,
        _ => 0.0
    };

    // ¿Se lanzan los adornos que solo son adorno?
    public static bool Adorno => Nivel == NivelAnimacion.Completo;

    // ¿Hay movimiento, del tipo que sea?
    public static bool Hay => Factor > 0;

    public static double Escalar(double ms) => ms * Factor;

    // Lo que diga el config. "auto" pregunta a Windows: quien ya ha pedido menos
    // movimiento en Accesibilidad > Efectos visuales no tiene que volver a pedirlo
    // aquí, que es justo lo que pide el documento sobre respetar la reducción de
    // movimiento del sistema.
    public static void Elegir(string? valor)
    {
        var antes = Nivel;
        Nivel = (valor ?? "auto").Trim().ToLowerInvariant() switch
        {
            "completo" => NivelAnimacion.Completo,
            "equilibrado" => NivelAnimacion.Equilibrado,
            "reducido" => NivelAnimacion.Reducido,
            "auto" => DesdeWindows(),
            _ => Avisar(valor)
        };
        if (Nivel != antes) Log.Info($"nivel de animación: {Nivel.ToString().ToLowerInvariant()} (config: \"{valor}\")");
    }

    static NivelAnimacion DesdeWindows()
    {
        try
        {
            // False = el usuario ha apagado los efectos de animación en Windows.
            return SystemParameters.ClientAreaAnimation ? NivelAnimacion.Completo : NivelAnimacion.Reducido;
        }
        catch (Exception ex)
        {
            Log.Error("leer los efectos de animación de Windows", ex);
            return NivelAnimacion.Completo;
        }
    }

    static NivelAnimacion Avisar(string? valor)
    {
        Log.Info($"nivel de animación \"{valor}\" desconocido: se usa auto " +
                 "(valores: auto, completo, equilibrado, reducido)");
        return DesdeWindows();
    }
}
