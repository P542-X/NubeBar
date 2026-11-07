using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// La guía de reglas: qué se puede hacer, dicho en castellano y con ejemplos que se pueden
// usar de un clic.
//
// ---------- El problema que resuelve ----------
//
// D9.3 puso un editor con todos los campos. Eso quita la necesidad de escribir JSON a mano,
// pero no la de **saber qué se puede pedir**. Delante de un formulario con «Clase de ventana»,
// «Ruta contiene» y «Prioridad», quien no haya leído el documento técnico no sabe si NubeHypr
// puede mandar Discord al escritorio 2 — ni cómo se escribe eso.
//
// Un formulario completo y un usuario que no sabe qué pedirle es una función que solo puede
// usar quien la escribió. Otra vez.
//
// ---------- Qué hay aquí y qué no ----------
//
// Aquí está lo que se puede decidir sin abrir una ventana: el catálogo de ejemplos, la
// traducción de una regla a una frase, y los avisos de cuándo una regla no va a funcionar.
// Todo puro y con pruebas. Dibujarlo es de `VentanaAjustes.Guia.cs`.
//
// Y una cosa que NO hace: ejecutar nada. Un ejemplo es una regla rellenada en el formulario,
// apagada, sin guardar y sin mover una sola ventana.
public static class HyprGuia
{
    // ================= Un ejemplo =================
    //
    // El `Que` es el titular —lo que quieres conseguir—, y la `Regla` es cómo se escribe. Los
    // `Campos` son los que el ejemplo rellena, para poder decirlo antes de pulsar nada: quien
    // mira la lista tiene que poder aprender qué campo sirve para qué sin probar los ocho.
    public sealed record Ejemplo(
        string Que,
        string Explicacion,
        HyprReglas.Regla Regla,
        string? Ojo = null)
    {
        public IReadOnlyList<string> Campos => CamposDe(Regla);

        // La frase legible, la misma función que usa la vista previa del formulario. Un
        // ejemplo cuya descripción se escribiera a mano acabaría diciendo algo distinto de lo
        // que hace su regla el día que alguien tocara una de las dos.
        //
        // Con un matiz que se vio en la primera captura: la frase se cuenta COMO SI estuviera
        // encendida. Los ejemplos nacen apagados —usar uno no decide nada— pero una tarjeta
        // de catálogo que termina en «(ahora mismo está apagada)» habla del estado de una
        // regla que todavía no existe, y repetido ocho veces es puro ruido. Ahí la tarjeta
        // explica una capacidad; el aviso de apagada es del formulario, donde la regla ya es
        // tuya y su estado importa.
        public string Frase => EnPalabras(Regla with { Activa = true });
    }

    // ================= Qué campos toca una regla =================
    public static IReadOnlyList<string> CamposDe(HyprReglas.Regla r)
    {
        var l = new List<string>();
        if (r.Proceso != null) l.Add("Proceso");
        if (r.Clase != null) l.Add("Clase de ventana");
        if (r.Titulo != null) l.Add("Título contiene");
        if (r.Ruta != null) l.Add("Ruta contiene");
        if (r.Espacio != null) l.Add("Espacio");
        if (r.Monitor != null) l.Add("Monitor");
        if (r.Layout != null) l.Add("Layout");
        if (r.Ancho != null || r.Alto != null) l.Add("Tamaño");
        if (r.Flotante != null) l.Add("Flotante");
        if (r.Foco != null) l.Add("Darle el foco");
        if (r.Prioridad != 0) l.Add("Prioridad");
        return l;
    }

    // ================= La regla, en castellano =================
    //
    // «Cuando aparezca Discord, enviarlo a Escritorio 2 y dejarlo flotante.»
    //
    // Es la pieza que convierte el formulario en algo que se puede revisar. Un campo relleno
    // no dice si has entendido bien lo que hace; una frase entera sí, porque se lee y se
    // compara con lo que querías.
    // ---------- Y por qué esto va por PLANTILLAS y no por trozos ----------
    //
    // La frase se arma juntando pedazos, y mientras solo hubo castellano eso daba igual:
    // «Cuando aparezca » + «una ventana de Discord». En inglés el verbo va detrás —«When a
    // Discord window appears»— así que pegar trozos traducidos uno a uno da una frase que
    // no es inglés, es castellano con palabras inglesas.
    //
    // Así que lo que se traduce es la PLANTILLA entera, con su hueco: `T("Cuando aparezca
    // {0}")` da `"When {0} appears"` y el orden lo decide cada idioma. Lo mismo con «una
    // ventana de {0}», que en inglés es «a {0} window»: el dato va en otro sitio.
    //
    // En castellano `T()` devuelve la clave tal cual, así que `string.Format` reconstruye
    // exactamente la frase de antes. Hay pruebas que la comparan letra a letra.
    static string F(string plantilla, params object[] datos) =>
        string.Format(Textos.T(plantilla), datos);

    public static string EnPalabras(HyprReglas.Regla r)
    {
        string cuando = Cuando(r);
        var hace = Acciones(r);

        if (hace.Count == 0)
            return F("{0}, no hacer nada todavía: falta decir qué quieres que pase.", cuando);

        return F("{0}, {1}.", cuando, Juntar(hace)) +
               (r.Activa ? "" : Textos.T(" (Ahora mismo está apagada, así que no se evalúa.)"));
    }

    // ---------- La mitad del «cuándo» ----------
    static string Cuando(HyprReglas.Regla r)
    {
        var trozos = new List<string>();
        if (r.Proceso is { Length: > 0 } p) trozos.Add(F("una ventana de {0}", p));
        if (r.Clase is { Length: > 0 } c) trozos.Add(F("cuya clase sea «{0}»", c));
        if (r.Titulo is { Length: > 0 } t) trozos.Add(F("cuyo título contenga «{0}»", t));
        if (r.Ruta is { Length: > 0 } ru) trozos.Add(F("cuya ruta contenga «{0}»", ru));

        if (trozos.Count == 0)
            return Textos.T("Esta regla no dice a qué ventanas se aplica, así que nunca se activará");

        // Si no hay proceso, la frase empieza por el criterio que haya, y hay que darle un
        // sujeto: «cuyo título contenga X» a secas no es una frase.
        if (r.Proceso == null) trozos[0] = F("una ventana {0}", trozos[0]);

        return F("Cuando aparezca {0}", Juntar(trozos));
    }

    // ---------- La mitad del «qué hace» ----------
    static List<string> Acciones(HyprReglas.Regla r)
    {
        var l = new List<string>();
        if (r.Espacio is { } e) l.Add(F("enviarla al Escritorio {0}", e));
        if (r.Monitor is { } m)
            l.Add(F("llevarla al monitor {0}", m) + (m == 0 ? Textos.T(" (el principal)") : ""));
        if (r.Flotante == true) l.Add(Textos.T("dejarla flotante, fuera del reparto"));
        if (r.Flotante == false) l.Add(Textos.T("meterla en el reparto aunque flotara"));
        if (r.Layout is { } la) l.Add(F("repartir la pantalla en {0}", NombreDeLayout(la)));
        if (r.Ancho != null || r.Alto != null) l.Add(F("darle el tamaño {0}", Tamano(r.Ancho, r.Alto)));
        if (r.Foco == true) l.Add(Textos.T("traerla delante"));
        if (r.Foco == false) l.Add(Textos.T("dejarla detrás, sin robar el foco"));
        return l;
    }

    // El tamaño, en palabras y no en fracciones. «0,5x—» es exacto y no se entiende; «media
    // pantalla de ancho, el alto como estuviera» sí.
    public static string Tamano(double? ancho, double? alto)
    {
        string? a = Fraccion(ancho), b = Fraccion(alto);
        if (a != null && b != null) return F("{0} de ancho y {1} de alto", a, b);
        if (a != null) return F("{0} de ancho (el alto, como estuviera)", a);
        if (b != null) return F("{0} de alto (el ancho, como estuviera)", b);
        return Textos.T("el de siempre");
    }

    static string? Fraccion(double? v) => v switch
    {
        null => null,
        <= 0.3 => Textos.T("un tercio de pantalla o menos"),
        <= 0.55 => Textos.T("media pantalla"),
        <= 0.8 => Textos.T("unos dos tercios de pantalla"),
        _ => Textos.T("casi toda la pantalla"),
    };

    // ---------- Cuidado con los dos nombres que engañan ----------
    //
    // En el modelo, `Horizontal` reparte en COLUMNAS (de izquierda a derecha) y `Vertical` en
    // FILAS (de arriba abajo). El nombre se refiere a la dirección en la que se recorre la
    // pantalla, no a la forma de las ventanas, y es justo al revés de lo que mucha gente
    // imagina al leerlo. Aquí se dice lo que hace, no solo cómo se llama.
    public static string NombreDeLayout(NubeHypr.Layout l) => l switch
    {
        NubeHypr.Layout.Principal => Textos.T("principal: una grande a un lado y las demás apiladas al otro"),
        NubeHypr.Layout.Horizontal => Textos.T("horizontal: columnas una al lado de otra, de izquierda a derecha"),
        NubeHypr.Layout.Vertical => Textos.T("vertical: filas una encima de otra, de arriba abajo"),
        NubeHypr.Layout.Flotante => Textos.T("flotante: no se reparte nada, cada ventana donde esté"),
        _ => l.ToString(),
    };

    // La «y» final también se traduce, y va como plantilla y no como palabra suelta: una
    // conjunción con sus espacios alrededor es una clave frágil, y además hay idiomas
    // donde la coma de antes cambia.
    static string Juntar(IReadOnlyList<string> trozos) => trozos.Count switch
    {
        0 => "",
        1 => trozos[0],
        2 => F("{0} y {1}", trozos[0], trozos[1]),
        _ => F("{0} y {1}", string.Join(", ", trozos.Take(trozos.Count - 1)), trozos[^1]),
    };

    // ================= Qué va mal, dicho en claro =================
    //
    // `HyprReglas.Valida` da UN motivo y para. Para el editor hacen falta todos a la vez: si
    // una regla no tiene criterios Y no tiene acciones, enseñar solo lo primero hace que el
    // usuario lo arregle, vuelva a guardar y se encuentre con lo segundo.
    //
    // Y en castellano, no en jerga del formato: «no has dicho a qué ventanas se aplica», no
    // «SinCriterios».
    public sealed record Problema(string Texto, bool Grave);

    public static List<Problema> Problemas(HyprReglas.Regla r)
    {
        var l = new List<Problema>();

        if (r.SinCriterios)
            l.Add(new("No has dicho a qué ventanas se aplica, así que no coincidirá con ninguna. " +
                      "Rellena al menos Proceso, Título, Clase o Ruta.", true));

        if (r.SinAcciones)
            l.Add(new("No has dicho qué hacer con ellas. Rellena al menos Espacio, Monitor, " +
                      "Layout, Tamaño, Flotante o Foco.", true));

        if (r.Espacio is < 0) l.Add(new("El escritorio no puede ser negativo.", true));
        if (r.Monitor is < 0) l.Add(new("El monitor no puede ser negativo.", true));
        if (r.Espacio == 0)
            l.Add(new("Los escritorios se cuentan desde 1: el primero es el 1, no el 0.", true));

        // ---------- Lo que no es inválido pero no va a hacer lo que esperas ----------
        //
        // Estos NO impiden guardar. Una regla puede ser rara a propósito, y un editor que se
        // niega a guardar lo que es legal acaba peleándose con quien sabe lo que hace.
        if (r.Flotante == true && (r.Layout != null || r.Ancho != null || r.Alto != null))
            l.Add(new("Una ventana flotante queda fuera del reparto, así que el layout y el " +
                      "tamaño que has puesto no se le aplicarán.", false));

        if (r.Proceso is { Length: > 0 } p &&
            p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            l.Add(new($"El proceso se escribe sin «.exe»: pon «{p[..^4]}» en vez de «{p}».", false));

        if (r.Proceso is { Length: > 0 } p2 && (p2.Contains('\\') || p2.Contains('/')))
            l.Add(new("El proceso es solo el nombre, sin ruta. Para filtrar por carpeta, " +
                      "usa «Ruta contiene».", false));

        if (!r.Activa)
            l.Add(new("Está apagada: se guarda, pero no se evalúa hasta que la enciendas.", false));

        return l;
    }

    // ================= Qué pasa si dos reglas coinciden =================
    //
    // La pregunta que se hace todo el mundo en cuanto tiene tres reglas, y la que no se puede
    // contestar mirando el formulario. Se explica con el mismo orden que usa el motor
    // (`HyprReglas.Ordenar`), porque si aquí se contara otra cosa el panel estaría enseñando
    // una prioridad que no es la de verdad.
    public const string ComoSeResuelvenLosEmpates =
        "Las reglas no se pisan entre sí: se COMBINAN campo a campo.\n\n" +
        "Cada regla que coincida pone los campos que haya rellenado y deja en paz los que no. " +
        "Una regla que solo dice «Escritorio 2» y otra que solo dice «flotante» se suman, " +
        "aunque las hayas escrito por separado.\n\n" +
        "Solo hay conflicto cuando dos reglas rellenan EL MISMO campo con valores distintos. " +
        "Entonces manda:\n\n" +
        "  1. La de PRIORIDAD más alta.\n" +
        "  2. Con la misma prioridad, la más CONCRETA: la que mira más cosas " +
        "(proceso + título gana a solo proceso).\n" +
        "  3. Y si siguen empatadas, la primera de la lista.\n\n" +
        "En «Qué pasaría con las ventanas de ahora» se ve, para cada ventana, qué regla puso " +
        "cada campo y por qué se descartaron las demás.";

    // ================= Los ejemplos =================
    //
    // Ocho, y en este orden a propósito: de lo que se pide más —mandar una aplicación a un
    // escritorio— a lo que hay que entender antes de escribir la tercera regla —qué pasa
    // cuando dos coinciden—.
    //
    // Todos usan aplicaciones de verdad y nombres de proceso de verdad, porque un ejemplo con
    // «MiAplicacion» obliga a traducir y es justo el paso en el que la gente se equivoca.
    //
    // Y todos nacen APAGADOS (`Activa: false`). Usar un ejemplo rellena el formulario; no
    // decide nada sobre tus ventanas hasta que lo revisas, lo enciendes y lo guardas.
    public static readonly Ejemplo[] Ejemplos =
    {
        new("Enviar una aplicación a un escritorio",
            "Lo más pedido: que el chat viva siempre en su escritorio y no se mezcle con el " +
            "trabajo. Basta con el nombre del proceso y el número del escritorio.",
            new HyprReglas.Regla("ejemplo-escritorio", Proceso: "Discord", Espacio: 2,
                                 Activa: false)),

        new("Que una aplicación salga flotante",
            "Una calculadora o un reproductor pequeño no tienen por qué entrar en el reparto " +
            "de la pantalla. Flotante significa: déjala donde Windows la ponga y no la " +
            "coloques con las demás.",
            new HyprReglas.Regla("ejemplo-flotante", Proceso: "CalculatorApp", Flotante: true,
                                 Activa: false)),

        new("Elegir el monitor",
            "Con dos pantallas, que el correo se abra siempre en la de al lado. Los monitores " +
            "se cuentan desde 0, y el 0 es el principal.",
            new HyprReglas.Regla("ejemplo-monitor", Proceso: "olk", Monitor: 1, Activa: false),
            Ojo: "Si desconectas ese monitor, la regla deja de tener dónde poner la ventana y " +
                 "no se aplica. No se pierde: vuelve a funcionar al reconectarlo."),

        new("Aplicar la regla solo a UNA ventana concreta, por el título",
            "El mismo programa puede tener veinte ventanas. El título distingue una: así la " +
            "regla toca la pestaña de WhatsApp del navegador y no todo el navegador.",
            new HyprReglas.Regla("ejemplo-titulo", Proceso: "opera", Titulo: "WhatsApp",
                                 Espacio: 2, Activa: false),
            Ojo: "El título se compara por «contiene», sin distinguir mayúsculas ni acentos. " +
                 "Ojo: muchos programas cambian el título solo (el nombre del documento " +
                 "abierto), así que elige un trozo que no cambie."),

        new("Elegir el layout: principal, horizontal o vertical",
            "Cómo se reparte la pantalla entre las ventanas que NO son flotantes.",
            new HyprReglas.Regla("ejemplo-layout", Proceso: "Code",
                                 Layout: NubeHypr.Layout.Principal, Activa: false),
            Ojo: "Los nombres engañan y conviene saberlo: «horizontal» reparte en COLUMNAS, " +
                 "de izquierda a derecha, y «vertical» en FILAS, de arriba abajo. El nombre " +
                 "es la dirección en que se recorre la pantalla, no la forma de las ventanas."),

        new("Definir el tamaño inicial",
            "El tamaño va en fracción de la pantalla, no en píxeles: 0,5 es la mitad. Así la " +
            "misma regla vale en un portátil y en un monitor de 4K.",
            new HyprReglas.Regla("ejemplo-tamano", Proceso: "notepad", Ancho: 0.5, Alto: 0.6,
                                 Activa: false),
            Ojo: "Puedes poner solo el ancho o solo el alto. Lo que dejes en blanco se queda " +
                 "como estuviera."),

        new("Combinar varias cosas en una regla",
            "Discord en el Escritorio 2 Y flotante. No hacen falta dos reglas: una misma " +
            "regla puede rellenar todos los campos que quieras.",
            new HyprReglas.Regla("ejemplo-combinado", Proceso: "Discord", Espacio: 2,
                                 Flotante: true, Foco: false, Activa: false),
            Ojo: "También funcionaría con dos reglas separadas, una por campo: NubeHypr las " +
                 "combina. Una sola es más fácil de leer; dos permiten apagar una mitad."),

        new("Qué pasa si dos reglas coinciden",
            "Esta no es una regla para usar, es la que explica el resto. Pruébala junto a la " +
            "de Discord de arriba: las dos coinciden con Discord y cada una rellena un campo " +
            "distinto, así que se suman en vez de pisarse.",
            new HyprReglas.Regla("ejemplo-empate", Prioridad: 10, Proceso: "Discord",
                                 Monitor: 0, Activa: false),
            Ojo: ComoSeResuelvenLosEmpates),
    };

    // ================= Probar una regla =================
    //
    // «Probar» aquí significa **evaluar y contar**, nunca mover. Es la diferencia entre un
    // botón que resuelve una duda y un botón que te reorganiza el escritorio mientras
    // escribes una regla a medias.
    //
    // Recibe las ventanas ya leídas en vez de leerlas por su cuenta: así esto es puro y se
    // puede probar con ventanas inventadas, incluidas las que no se pueden provocar a mano
    // (cien ventanas, un título con acentos, un proceso que no existe).
    public sealed record Candidata(string Nombre, string Proceso, string Titulo);

    public sealed record Prueba(
        bool PuedeAplicarse,
        string Veredicto,
        IReadOnlyList<Candidata> Coinciden,
        IReadOnlyList<string> Problemas);

    public static Prueba Probar(HyprReglas.Regla r, IEnumerable<Candidata> ventanas,
                                Func<Candidata, bool> coincide)
    {
        var problemas = Problemas(r);
        var graves = problemas.Where(p => p.Grave).Select(p => p.Texto).ToList();
        var todos = problemas.Select(p => p.Texto).ToList();

        // Con un problema grave no se evalúa: una regla sin criterios «coincide» con cero
        // ventanas, y enseñar «0 ventanas» sin decir por qué haría pensar que el filtro está
        // bien escrito y que simplemente no hay nada abierto.
        if (graves.Count > 0)
            return new(false, "Esta regla todavía no se puede aplicar.",
                       Array.Empty<Candidata>(), todos);

        var casan = ventanas.Where(v => { try { return coincide(v); } catch { return false; } })
                            .ToList();

        string veredicto = casan.Count switch
        {
            0 => "No coincide con ninguna ventana abierta ahora mismo. " +
                 "Puede estar bien —quizá esa aplicación no está abierta— o puede que el " +
                 "nombre del proceso no sea el que has escrito.",
            1 => "Coincide con 1 ventana abierta.",
            _ => $"Coincide con {casan.Count} ventanas abiertas.",
        };

        return new(true, veredicto, casan, todos);
    }

    // Lo que la prueba DICE que haría, ventana por ventana. Sigue sin mover nada: es la
    // frase de la regla aplicada a un nombre concreto, que es lo que convierte «coincide con
    // 3 ventanas» en algo que se puede revisar.
    public static string QueLeHariaA(HyprReglas.Regla r, Candidata v)
    {
        var hace = Acciones(r);
        return hace.Count == 0
            ? $"{v.Nombre}: nada, la regla no dice qué hacer."
            : $"{v.Nombre}: {Juntar(hace)}.";
    }

    // El recordatorio que acompaña a toda prueba, y que no puede faltar mientras el modo sea
    // simulación. Sin esto, «coincide con 3 ventanas» se lee como «va a mover 3 ventanas».
    public static string LoQuePasaDeVerdad(NubeHypr.Modo modo) => modo switch
    {
        NubeHypr.Modo.Apagado =>
            "NubeHypr está apagado: esto es solo un cálculo. No se evalúa ni se mueve nada.",
        NubeHypr.Modo.Aplicar =>
            "NubeHypr está en modo APLICAR: las reglas todavía no mueven ventanas por su " +
            "cuenta, pero los atajos sí. Probar aquí sigue sin mover nada.",
        _ => "Modo simulación: probar calcula y lo apunta en el registro, pero NO mueve " +
             "ninguna ventana. Tampoco lo hará al guardar la regla.",
    };

    // ---------- Un ejemplo, con un id que no choque ----------
    //
    // Los ids del catálogo son fijos para poder nombrarlos en las pruebas, pero dos clics en
    // el mismo ejemplo no pueden crear dos reglas con el mismo id: la segunda machacaría a la
    // primera y el usuario vería desaparecer lo que acababa de escribir.
    public static HyprReglas.Regla ParaUsar(Ejemplo e, IEnumerable<HyprReglas.Regla> yaHay)
    {
        var id = HyprReglas.IdLibre(yaHay.ToList(), e.Regla.Id);
        // Apagada, siempre, aunque el catálogo cambiara: usar un ejemplo rellena un
        // formulario, no toma una decisión sobre las ventanas de nadie.
        return e.Regla with { Id = id, Activa = false };
    }
}
