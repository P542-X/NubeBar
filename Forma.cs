using System;

namespace NubeBar;

// La FORMA de las superficies: radios, filos, huecos y resplandor.
//
// ---------- De tres estilos a un valor del tema ----------
//
// Esto era `EstiloVisual`: un `enum` de tres casos —Normal, Nube y Neón— con sus números
// escritos en C#. Nació bien, y el motivo está en el comentario que llevaba: la forma estaba
// en cuatro constantes dentro del código y cambiarla era recompilar, así que se sacó a un
// ajuste. Las palabras del usuario eran «lo que buscamos ahora es decoración».
//
// Lo que no resolvía es que seguían siendo TRES, y los tres los elegí yo. Añadir un cuarto
// era recompilar igual, y el usuario no podía inventarse ninguno.
//
// El rediseño lo cierra por el otro lado (`docs/REDISENO-VISUAL.md`, decisión A): la forma es
// un valor del TEMA, como los colores. «Un tema es un paquete de valores, no código aparte».
// Así que aquí ya no hay tres casos: hay una función del tema a sus medidas.
//
// ---------- Lo que una forma NO puede cambiar ----------
//
// Esto se queda tal cual estaba, porque sigue siendo verdad: nada que afecte a lo que el
// programa HACE. Una forma mueve radios, filos, huecos y resplandor; no esconde controles, no
// cambia qué dice un panel ni quita una función. Si una forma pudiera quitar algo, elegirla
// sería una trampa: estarías perdiendo programa a cambio de una decoración.
public sealed record Forma(
    double RadioCard,
    double RadioShell,
    double Hueco,          // entre tarjetas
    double Dentro,         // del filo al contenido
    double GrosorFilo,
    double Resplandor,     // 0 = sin resplandor; >0 = radio del difuminado en reposo
    // ---------- El halo, latiendo ----------
    //
    // Hasta dónde crece el difuminado al respirar. Un letrero de neón de verdad no está
    // quieto, y el usuario lo dijo mirándolo: «tiene que estar animado de neón, no solo los
    // bordes». Con `ResplandorAlto <= Resplandor` no late.
    double ResplandorAlto = 0,
    // ---------- Y que no se quede en el filo ----------
    //
    // Encendido, las cabeceras de sección toman el color de acento y su icono brilla: la
    // decoración entra DENTRO de la tarjeta en vez de dibujarle un marco de colores alrededor.
    // El cuerpo del texto no se toca — un párrafo entero en color de acento se lee peor, y una
    // decoración no puede cobrarse la legibilidad.
    bool TintaDeAcento = false)
{
    public bool Brilla => Resplandor > 0;
    public bool Late => ResplandorAlto > Resplandor;
}

// ================= Cuánto brilla cada cosa =================
//
// ---------- El fallo que arregla ----------
//
// El estilo tenía una bandera, `FiloDeAcento`, y `Card` la obedecía: **todas** las tarjetas se
// pintaban el filo con el color de acento. El resultado es el que describió el usuario mirando
// la captura: «un borde azul fijo alrededor de todas las tarjetas; parece un contorno
// seleccionado repetido en todos los elementos».
//
// Y tenía razón en el diagnóstico, no solo en el gusto: si TODO lleva el color de lo activo,
// el color de lo activo deja de significar nada. Una tarjeta en reposo no puede verse igual
// que una seleccionada.
//
// ---------- La regla que lo sustituye ----------
//
// El brillo deja de ser una propiedad de la forma y pasa a ser una propiedad de **qué es esa
// superficie**. La forma dice cuánta luz hay en total; el realce, cómo se reparte.
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
    //
    // ---------- El ambiente subió de 0,40 a 0,95, y no por gusto ----------
    //
    // Con 0,40 la opacidad del halo del panel salía 0,20. Se midió sobre las capturas: el azul
    // del aire que rodea al panel pasaba de 10,866 a 11,307 sobre 255 entre el punto bajo y el
    // alto del ciclo. O sea que el halo **estaba ahí y respiraba de verdad** —la medida es
    // consistente en las seis fotos— pero 0,44 de 255 no lo ve nadie.
    //
    // Un efecto que solo detecta un medidor no cumple «debe verse claramente sobre un wallpaper
    // oscuro». Ahora el máximo queda en ~0,52 y el mínimo en ~0,23: se ve, y se ve que cambia.
    public static (double Difuminado, double Intensidad) De(Realce r) => r switch
    {
        Realce.Ambiente => (1.70, 0.95),
        Realce.Reposo => (0.45, 0.22),
        Realce.Activo => (1.00, 1.00),
        Realce.Principal => (1.25, 1.20),
        _ => (1.00, 1.00),
    };

    // Una tarjeta en reposo NO puede parecer seleccionada. Es la regla que se rompió y la que
    // conviene poder exigir en una prueba, así que está escrita como función y no como un
    // comentario de buenas intenciones.
    public static bool SeDistingue(Realce reposo, Realce activo) =>
        De(activo).Intensidad >= De(reposo).Intensidad * 2;
}

public static class Formas
{
    // ---------- Los valores que un tema no dice ----------
    //
    // Son EXACTAMENTE los que tenía el estilo «Normal», que a su vez eran los que estaban
    // escritos en el código antes de que los estilos existieran (`HuecoCard = 10`,
    // `DentroCard = 13`, filo de 1 px). Es a propósito: ninguno de los nueve temas de serie
    // declara todavía su hueco ni su relleno —la maqueta no los fija— y lo que no se decide
    // no puede cambiar de aspecto por accidente.
    //
    // El día que la fase 1 afine Profesional, los declarará y esto dejará de aplicársele.
    public const double HuecoDeSerie = 10, DentroDeSerie = 13, FiloDeSerie = 1;

    // ---------- El resplandor ----------
    //
    // Era la seña de identidad del estilo «Neón», y el plan lo dice claro: «Lo útil de Neón
    // (el halo con DropShadowEffect que respira) pasa a ser una decoración más».
    //
    // Así que ya no hay un estilo que brille: hay temas que llevan encendida la decoración
    // `halo`, y esos brillan. Ocho de los nueve la llevan; Oficina no, y además declara su
    // halo `transparent` — un tema Fluent claro con un resplandor de neón alrededor de cada
    // tarjeta sería otra cosa, no Oficina. Las dos condiciones se comprueban: una decoración
    // encendida cuyo color es invisible no enciende nada.
    public const double ResplandorDeSerie = 10, ResplandorAltoDeSerie = 26;

    public static Forma Del(Pintura p)
    {
        bool halo = p.Tema.Lleva(Decoraciones.Halo) && !p.R(ClavesBase.Halo).EsInvisible;

        return new Forma(
            RadioCard: p.Radio(Piezas.Tarjeta),
            RadioShell: p.Radio(Piezas.Panel),
            Hueco: p.Tema.Medida(Medidas.Hueco, HuecoDeSerie),
            Dentro: p.Tema.Medida(Medidas.Dentro, DentroDeSerie),
            GrosorFilo: p.Tema.Medida(Medidas.GrosorFilo, FiloDeSerie),
            Resplandor: halo ? ResplandorDeSerie : 0,
            ResplandorAlto: halo ? ResplandorAltoDeSerie : 0,
            TintaDeAcento: p.Tema.Lleva(Decoraciones.TintaDeAcento));
    }
}

// Los nombres de las decoraciones que el código consulta. Como constantes y no como cadenas
// sueltas por lo mismo que `ClavesBase`: una errata en `"halo"` no da error de compilación, da
// un tema que no brilla y un rato perdido.
public static class Decoraciones
{
    public const string Halo = "halo";
    public const string DestelloFilo = "destello-filo";
    public const string ModoRgb = "modo-rgb";
    public const string RelojGlitch = "reloj-glitch";
    public const string LineasDeBarrido = "lineas-de-barrido";
    public const string Particulas = "particulas";
    public const string Mascota = "mascota";
    public const string MarcaDeAgua = "marca-de-agua";
    public const string DestellosReloj = "destellos-reloj";

    // Las cabeceras de sección en color de acento. La traía «Neón» encendida y ningún tema de
    // serie la lleva; se queda porque un tema del usuario puede quererla y porque el código
    // que la pinta ya existe y funciona.
    public const string TintaDeAcento = "tinta-de-acento";
}

// Las medidas de forma que un tema puede declarar, además de los radios.
public static class Medidas
{
    public const string Hueco = "hueco";
    public const string Dentro = "dentro";
    public const string GrosorFilo = "grosor-filo";
}
