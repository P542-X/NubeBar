using System;

namespace NubeBar;

// El estilo visual: la FORMA de las superficies, elegida por el usuario.
//
// ---------- Por qué esto es un ajuste y no una decisión mía ----------
//
// El tema ya decide los colores, y lleva siete variantes. Lo que no se podía elegir era la
// forma: cuánto se redondea una tarjeta, cuánto filo tiene, cuánto aire lleva dentro y si
// brilla. Eso estaba escrito en cuatro constantes dentro del código, y cambiarlo significaba
// recompilar.
//
// Y es justo lo que el usuario quiere decidir. Sus palabras: «lo que buscamos ahora es
// decoración». La decoración es suya.
//
// ---------- Lo que un estilo NO puede cambiar ----------
//
// Nada que afecte a lo que el programa hace. Un estilo mueve radios, filos, huecos y
// resplandor; no esconde controles, no cambia qué dice un panel ni quita una función. Si un
// estilo pudiera quitar algo, elegirlo sería una trampa: estarías perdiendo programa a cambio
// de una decoración.
//
// Por eso esto es un puñado de números y dos banderas. Si algún día uno de ellos quisiera
// decidir qué se enseña, el sitio es otro.
public enum EstiloVisual
{
    Normal,     // el de siempre: sobrio, filo discreto
    Nube,       // el de la maqueta: más redondo, más aire, filo algo más presente
    Neon,       // filos de acento y resplandor
}

public sealed record Estilo(
    EstiloVisual Que,
    string Nombre,
    string Que_Hace,
    double RadioCard,
    double RadioShell,
    double Hueco,          // entre tarjetas
    double Dentro,         // del filo al contenido
    double GrosorFilo,
    double Resplandor,     // 0 = sin resplandor; >0 = radio del difuminado en reposo
    // ---------- El neón, latiendo ----------
    //
    // Hasta dónde crece el difuminado al respirar. Un letrero de neón de verdad no está
    // quieto, y el usuario lo dijo mirándolo: «tiene que estar animado de neón, no solo los
    // bordes». Con `ResplandorAlto <= Resplandor` no late — así «Normal» y «Nube» no heredan
    // un latido que nadie les ha pedido.
    double ResplandorAlto = 0,
    // ---------- Y que no se quede en el filo ----------
    //
    // La otra mitad de lo mismo. Encendido, las cabeceras de sección toman el color de acento
    // y su icono brilla: el estilo entra DENTRO de la tarjeta en vez de dibujarle un marco de
    // colores alrededor. El cuerpo del texto no se toca — un párrafo entero en color de
    // acento se lee peor, y una decoración no puede cobrarse la legibilidad.
    bool TintaDeAcento = false)
{
    public bool Brilla => Resplandor > 0;
    public bool Late => ResplandorAlto > Resplandor;
}

// ================= Cuánto brilla cada cosa =================
//
// ---------- El fallo que arregla ----------
//
// El estilo tenía una bandera, `FiloDeAcento`, y `Card` la obedecía: **todas** las tarjetas
// se pintaban el filo con el color de acento. El resultado es el que describió el usuario
// mirando la captura: «un borde azul fijo alrededor de todas las tarjetas; parece un contorno
// seleccionado repetido en todos los elementos».
//
// Y tenía razón en el diagnóstico, no solo en el gusto: si TODO lleva el color de lo activo,
// el color de lo activo deja de significar nada. Una tarjeta en reposo no puede verse igual
// que una seleccionada.
//
// ---------- La regla que lo sustituye ----------
//
// El brillo deja de ser una propiedad del estilo y pasa a ser una propiedad de **qué es esa
// superficie**. El estilo dice cuánta luz hay en total; el realce, cómo se reparte.
public enum Realce
{
    Ambiente,    // la concha del panel: un halo ancho y muy flojo, como luz de fondo
    Reposo,      // una tarjeta normal: filo oscuro y un brillo que casi no se nota
    Activo,      // lo encendido, elegido, bajo el ratón o con el foco: se ve
    Principal,   // la acción importante de una superficie: lo que más luz tiene
}

public static class Realces
{
    // Multiplicadores sobre el difuminado y sobre la intensidad. Separados porque no van
    // juntos: el halo de ambiente es MUY ancho y MUY flojo —así se lee como luz de la
    // habitación— mientras que el de un elemento activo es más corto y más fuerte, que es
    // como se lee «esto está encendido».
    // ---------- El ambiente subió de 0,40 a 0,95, y no por gusto ----------
    //
    // Con 0,40 la opacidad del halo del panel salía 0,20. Se midió sobre las capturas: el
    // azul del aire que rodea al panel pasaba de 10,866 a 11,307 sobre 255 entre el punto
    // bajo y el alto del ciclo. O sea que el halo **estaba ahí y respiraba de verdad** —la
    // medida es consistente en las seis fotos— pero 0,44 de 255 no lo ve nadie.
    //
    // Un efecto que solo detecta un medidor no cumple «debe verse claramente sobre un
    // wallpaper oscuro». Ahora el máximo queda en ~0,52 y el mínimo en ~0,23: se ve, y se ve
    // que cambia.
    public static (double Difuminado, double Intensidad) De(Realce r) => r switch
    {
        Realce.Ambiente => (1.70, 0.95),
        Realce.Reposo => (0.45, 0.22),
        Realce.Activo => (1.00, 1.00),
        Realce.Principal => (1.25, 1.20),
        _ => (1.00, 1.00),
    };

    // Una tarjeta en reposo NO puede parecer seleccionada. Es la regla que se rompió y la
    // que conviene poder exigir en una prueba, así que está escrita como función y no como
    // un comentario de buenas intenciones.
    public static bool SeDistingue(Realce reposo, Realce activo) =>
        De(activo).Intensidad >= De(reposo).Intensidad * 2;
}

public static class Estilos
{
    // ---------- Los tres ----------
    //
    // Las medidas de `Normal` son EXACTAMENTE las que había escritas en el código
    // (`RadioCard = 12`, `RadioShell = 14`, `HuecoCard = 10`, `DentroCard = 13`). Es a
    // propósito: quien no toque nada tiene que ver lo mismo que veía, al píxel. Un ajuste
    // nuevo que cambia el aspecto por defecto no es un ajuste, es un cambio con un
    // interruptor encima.
    public static readonly Estilo Normal = new(
        EstiloVisual.Normal, "Normal",
        "Sobrio, con el filo justo. Lo de siempre.",
        RadioCard: 12, RadioShell: 14, Hueco: 10, Dentro: 13,
        GrosorFilo: 1, Resplandor: 0);

    // El de la maqueta: lo mismo pero más redondo y con más aire. La diferencia no está en
    // añadir cosas, está en dejar respirar las que hay.
    public static readonly Estilo Nube = new(
        EstiloVisual.Nube, "Nube",
        "Más redondo y con más aire. El de las tarjetas grandes.",
        RadioCard: 15, RadioShell: 18, Hueco: 12, Dentro: 15,
        GrosorFilo: 1, Resplandor: 0);

    // ---------- Neón ----------
    //
    // Dos versiones anteriores y las dos las devolvió el usuario, con razón las dos veces:
    //
    //  1. Solo pintaba el filo de acento. «Un marco de color no es neón.»
    //  2. Se le añadió el latido, pero el filo de acento seguía en TODAS las tarjetas: «un
    //     borde azul fijo alrededor de todas; parece un contorno seleccionado repetido».
    //
    // El filo de acento ya no existe (ver `Realce`): el filo es oscuro y discreto, y la luz
    // viene de fuera —un halo difuminado que respira— y no de una línea dibujada.
    //
    // `GrosorFilo` baja de 1,4 a 1: un filo grueso Y un halo compiten, y el que gana es el
    // filo, que es justo el que no queremos ver.
    public static readonly Estilo Neon = new(
        EstiloVisual.Neon, "Neón",
        "Filo discreto y un halo de color que respira desde fuera.",
        RadioCard: 12, RadioShell: 16, Hueco: 11, Dentro: 13,
        GrosorFilo: 1, Resplandor: 10,
        ResplandorAlto: 26, TintaDeAcento: true);

    public static readonly Estilo[] Todos = { Normal, Nube, Neon };

    // Lo que se guarda en el fichero. Minúsculas y sin acentos: una clave de configuración no
    // es un rótulo, y «Neón» con tilde en un fichero que alguien edita a mano es una fuente de
    // sorpresas.
    public static string Clave(EstiloVisual e) => e switch
    {
        EstiloVisual.Nube => "nube",
        EstiloVisual.Neon => "neon",
        _ => "normal",
    };

    // ---------- Y lo que no se entiende es «normal» ----------
    //
    // Nunca se cae en el más llamativo. Un fichero con una errata no puede dejarte el
    // escritorio lleno de resplandores sin que tú lo hayas pedido: es la misma regla que
    // `NubeHypr.ModoEfectivo`, que convierte lo desconocido en simulación y jamás en aplicar.
    public static Estilo Leer(string? clave) => (clave ?? "").Trim().ToLowerInvariant() switch
    {
        "nube" => Nube,
        "neon" or "neón" => Neon,
        _ => Normal,
    };

    public static Estilo De(EstiloVisual e) => Leer(Clave(e));
}
