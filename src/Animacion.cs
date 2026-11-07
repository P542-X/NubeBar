using System;
using System.Windows;

namespace NubeBar;

// El orden importa: de más a menos movimiento, y se compara con <= en varios sitios.
public enum NivelAnimacion { Completo, Equilibrado, Reducido, Desactivado }

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

    // ---------- Geometría: lo que se mueve, crece o encoge ----------
    // Es lo caro: obliga a medir y a redibujar en cada fotograma. Equilibrado no la
    // quita, la acorta. Reducido y Desactivado la dejan en cero, y entonces el valor
    // final se fija igual pero sin recorrer el camino.
    public static double Factor => Nivel switch
    {
        NivelAnimacion.Completo => 1.0,
        NivelAnimacion.Equilibrado => 0.6,
        _ => 0.0
    };

    public static bool Hay => Factor > 0;
    public static double Escalar(double ms) => ms * Factor;

    // ---------- Fundidos: los cambios de opacidad ----------
    // No son geometría: no miden ni mueven nada, solo mezclan. Por eso Reducido SÍ los
    // conserva, muy cortos, para que al pasar el ratón por un botón siga habiendo una
    // respuesta; y Desactivado no, porque ahí lo que se pide es que todo aparezca ya
    // en su estado final.
    //
    // Esta es la diferencia de verdad entre Reducido y Desactivado: sin ella los dos
    // niveles serían el mismo botón con dos nombres.
    public static double FactorFundido => Nivel switch
    {
        NivelAnimacion.Completo => 1.0,
        NivelAnimacion.Equilibrado => 0.6,
        NivelAnimacion.Reducido => 0.25,
        _ => 0.0
    };

    public static bool HayFundido => FactorFundido > 0;
    public static double EscalarFundido(double ms) => ms * FactorFundido;

    // ---------- Adornos ----------
    // El icono que se levanta, el latido de una app que arranca, el destello del
    // borde: no tienen estado que conservar, así que fuera de Completo ni se lanzan.
    public static bool Adorno => Nivel == NivelAnimacion.Completo;

    // ¿El botón se encoge al pulsarlo? Es geometría, y en Equilibrado va más corto y
    // sin el rebote, que es la parte que más se nota.
    public static bool Encogido => Nivel <= NivelAnimacion.Equilibrado;
    public static bool Rebote => Nivel == NivelAnimacion.Completo;

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
            "desactivado" => NivelAnimacion.Desactivado,
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
                 "(valores: auto, completo, equilibrado, reducido, desactivado)");
        return DesdeWindows();
    }
}
