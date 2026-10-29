using System;
using System.Windows;
using System.Windows.Media;

namespace NubeBar;

// D6: con qué se pintan las superficies de NubeHypr.
//
// ---------- Por qué una paleta propia y no el acento del tema ----------
//
// NubeHypr dice dos cosas y tiene que decirlas igual en los siete temas: **dónde estás o
// qué va a pasar** (azul) y **cuidado con esto** (coral). El acento del tema no sirve para
// eso: en Gruvbox es amarillo, en Catppuccin morado y en Nube justamente el coral. Si "va
// a pasar" y "cuidado" salieran los dos del acento, serían el mismo color — y entonces el
// filo de la previsualización y el botón de quitar una regla se leerían igual.
//
// Así que son dos papeles, no dos colores: azul para foco y acciones principales, coral
// para avisos y acciones destructivas. Y cada uno se **deriva del tema cuando el tema
// tiene algo parecido**, para no pegar un azul ajeno encima de Gruvbox:
//
//  - Azul: `Roles.Sistema` si de verdad es azul (es el "azul nube" del tema Nube, y el que
//    ya usan red, volumen y batería). Si el tema no lo declara, `Sistema` ES el acento, y
//    un acento amarillo no puede hacer de azul: entonces se usa el azul de Nube.
//  - Coral: el coral de Nube, salvo que el `Negativo` del tema ya esté en esa familia de
//    tono, y entonces manda el del tema.
//
// Todo lo que sale de aquí pasa por `Contraste`, así que no hay un color que "se vea mal
// en el tema claro": se ve o se corrige.
//
// ---------- Y el alto contraste ----------
//
// Con el alto contraste de Windows encendido, los velos translúcidos son lo primero que
// hay que tirar: un relleno al 20 % sobre un fondo que el usuario ha pedido plano es
// exactamente lo que ese ajuste existe para quitar. Aquí eso se decide una vez
// (`AltoContraste`) y lo consultan todas las superficies.
public static class HyprPinta
{
    // ---------- Los dos colores de referencia ----------
    //
    // Son los del tema Nube, que es la identidad de la casa: el azul nube y el coral de
    // acción. No se eligen aquí por gusto; se repiten porque ya están en `Temas.Nube`.
    public static readonly Color AzulNube = Color.FromRgb(0x65, 0xB8, 0xD6);
    public static readonly Color CoralNube = Color.FromRgb(0xE8, 0x75, 0x62);

    // Las dos ventanas de tono, en grados. Azul: del cian al azul-violeta. Coral: del rojo
    // al naranja. Un color fuera de su ventana no puede hacer de ese papel.
    public const double TonoAzulMin = 165, TonoAzulMax = 265;
    public const double TonoCoralMin = 340, TonoCoralMax = 45;   // cruza el 0: es rojo-naranja

    // Por debajo de esto un color es un gris y no tiene tono del que hablar.
    public const double SaturacionMinima = 0.15;

    public static bool EsAzul(Color c) =>
        Contraste.Saturacion(c) >= SaturacionMinima &&
        Contraste.Tono(c) >= TonoAzulMin && Contraste.Tono(c) <= TonoAzulMax;

    public static bool EsCoral(Color c)
    {
        if (Contraste.Saturacion(c) < SaturacionMinima) return false;
        double t = Contraste.Tono(c);
        return t >= TonoCoralMin || t <= TonoCoralMax;   // la ventana cruza el 0
    }

    // ---------- Azul: foco y acción principal ----------
    public static Color Azul(Roles r) => EsAzul(r.Sistema) ? r.Sistema : AzulNube;

    // ---------- Coral: aviso y acción destructiva ----------
    public static Color Coral(Roles r) => EsCoral(r.Negativo) ? r.Negativo : CoralNube;

    // Y las mismas como TINTA, que no es lo mismo: un relleno y un texto no se miden
    // contra lo mismo. `superficie` es lo que de verdad hay detrás (las "Solidas" de
    // `Roles`), porque un color con alfa no tiene luminancia propia.
    public static Color AzulTinta(Roles r, Color superficie) => Roles.TintaSobre(Azul(r), superficie);
    public static Color CoralTinta(Roles r, Color superficie) => Roles.TintaSobre(Coral(r), superficie);

    // ================= Alto contraste =================
    //
    // Se lee de Windows, con una puerta para las pruebas: `--pruebas` no puede depender de
    // cómo tenga el usuario su sistema, y a la vez hay que poder comprobar las dos ramas.
    public static bool? Forzado;

    public static bool AltoContraste =>
        Forzado ?? SystemParameters.HighContrast;

    // El alfa de un velo. Con alto contraste **no hay velo**: opaco o nada, porque un
    // translúcido es justo lo que ese ajuste pide quitar.
    public static byte Velo(byte normal) => AltoContraste ? (byte)0xFF : normal;

    // ================= El filo de la previsualización =================
    //
    // ---------- El problema de verdad ----------
    //
    // La previsualización se pinta ENCIMA del escritorio del usuario, y no sabemos qué hay
    // debajo: un fondo oscuro, una foto clarísima, o Wallpaper Engine moviendo un vídeo que
    // cambia de luminancia cada fotograma. Un filo de un solo color siempre tiene un fondo
    // que lo borra.
    //
    // La solución no es adivinar el fondo: es un **filo doble**. Una línea exterior oscura
    // y translúcida y, dentro, la línea de color. Sobre claro se ve la exterior; sobre
    // oscuro se ve la de color; sobre un vídeo que cambia, siempre una de las dos. Es lo
    // que hace que "el filo visible" no dependa de una medición imposible.
    //
    // Si SÍ se conoce lo que hay detrás —el color medio que ya saca el extractor del
    // fondo— se usa además para elegir la versión del color que más contraste da.
    public sealed record Filo(Color Exterior, Color Interior, double GrosorExterior, double GrosorInterior)
    {
        public double Total => GrosorExterior + GrosorInterior;
    }

    public const double GrosorFiloExterior = 1.0;
    public const double GrosorFiloInterior = 2.0;

    // Mínimo de contraste que se le exige al filo contra lo que haya detrás. Es más bajo
    // que el de un texto a propósito: un filo de 2 px no se "lee", se ve.
    public const double MinimoFilo = 2.2;

    public static Filo FiloDelDestino(Roles r, Color? detras = null)
    {
        var color = Azul(r);

        // Si se conoce el fondo, se corrige el color hasta que destaque sobre él. Y si no,
        // se deja el azul tal cual: la línea exterior es la que responde por él.
        if (detras is Color fondo && Contraste.Entre(color, fondo) < MinimoFilo)
            color = Contraste.Legible(color, fondo, MinimoFilo);

        // La exterior es negra sobre fondo claro y blanca sobre fondo oscuro cuando se
        // sabe; sin saberlo, negra translúcida, que es la que funciona sobre más fondos.
        Color exterior = detras is Color f && Contraste.Luminancia(f) < 0.22
            ? Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0xB0, 0x00, 0x00, 0x00);

        double grueso = AltoContraste ? 2.0 : GrosorFiloExterior;
        double fino = AltoContraste ? 3.0 : GrosorFiloInterior;
        return new Filo(exterior, color, grueso, fino);
    }

    // El relleno del destino: un velo del azul, **lo bastante flojo para que se siga
    // viendo la ventana que hay debajo**. Si tapara, la previsualización dejaría de
    // enseñar dónde va a caer la ventana y se convertiría en una mancha.
    public const byte VeloDestino = 0x30;        // ~19 %

    public static Color RellenoDelDestino(Roles r)
    {
        var a = Azul(r);
        // Con alto contraste tampoco se rellena: solo el filo. Un relleno opaco taparía la
        // ventana, y eso es peor que no rellenar.
        return AltoContraste ? Color.FromArgb(0x00, a.R, a.G, a.B)
                             : Color.FromArgb(VeloDestino, a.R, a.G, a.B);
    }

    // ================= La nube, de protagonista sin ser una mascota =================
    //
    // La nube de `Nubecita` es la identidad, y en D6 aparece en las superficies de
    // NubeHypr: el indicador de escritorio, el vacío de la vista general, la cabecera del
    // panel de reglas. El riesgo es el de siempre con una mascota: que crezca.
    //
    // Por eso el tamaño no se escribe a mano en cada sitio, se pide aquí. Dos topes:
    // nunca menos de `MinNube` (por debajo es una manchita) y nunca más de `MaxNube` ni
    // de una fracción del lado disponible. Con eso, "la nube es protagonista" no puede
    // convertirse en "la nube tapa el escritorio".
    public const double MinNube = 16, MaxNube = 72, FraccionMaxima = 0.34;

    public static double TamanoDeLaNube(double ladoDisponible)
    {
        if (double.IsNaN(ladoDisponible) || ladoDisponible <= 0) return MinNube;
        double quiero = ladoDisponible * FraccionMaxima;
        return Math.Clamp(quiero, MinNube, MaxNube);
    }

    // ================= Animar solo cuando el estado cambia de verdad =================
    //
    // La regla de la casa es que el valor final se fija ANTES de animar y la animación
    // solo dibuja el camino. Lo que falta es no dibujar el camino cuando no hay camino:
    // relanzar un fundido con el mismo estado de origen y destino es exactamente lo que se
    // ve como parpadeo, y es lo que pasó en la vista general en C4.1.
    //
    // Así que una animación necesita dos cosas: que el estado haya cambiado, y que el
    // nivel de animación del usuario la permita.
    public static bool TocaAnimar<T>(T antes, T ahora) where T : IEquatable<T> =>
        !antes.Equals(ahora) && Animacion.HayFundido;

    // Y la duración sale del nivel, nunca de un número escrito a mano: con "desactivado"
    // devuelve 0, y entonces no se lanza nada.
    public static TimeSpan Duracion(double ms) =>
        TimeSpan.FromMilliseconds(Animacion.EscalarFundido(ms));
}
