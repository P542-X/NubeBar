using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace NubeBar;

// Fase 0 del rediseño: el catálogo de colores y los nueve temas de serie.
//
// Lo que se vigila aquí son las dos reglas que hacen posible todo lo demás
// (`docs/REDISENO-VISUAL.md`, decisiones A y B):
//
//   - **Todas las claves tienen valor en todos los temas** (o se derivan). Si una clave se
//     queda sin contestar, en pantalla no sale un error: sale un hueco transparente, que es
//     exactamente la clase de fallo que nadie encuentra mirando.
//   - **Los nueve temas de serie pasan el contraste.** Un tema de serie ilegible no es un
//     gusto discutible, es un fallo: el usuario no eligió los valores, los elegí yo.
public static partial class Pruebas
{
    internal static void CorrerTemas()
    {
        RellenosSeLeen();
        TemasDeSerieCargan();
        CatalogoEntero();
        CascadaDeColores();
        ContrasteDeLosNueve();
        AjustesNuncaIlegible();
    }

    // ================= Relleno: leer lo que escribió la maqueta =================
    static void RellenosSeLeen()
    {
        Grupo("fase 0: colores y degradados en texto");

        // Lo liso, en los formatos que hay escritos por ahí.
        Igual((byte)0xFF, Relleno.Leer("#8DA9E8")!.Color.A, "un #RRGGBB sale opaco");
        Igual((byte)0x8D, Relleno.Leer("#8DA9E8")!.Color.R, "y con su rojo");
        Igual((byte)0xE6, Relleno.Leer("#E6101A2B")!.Color.A, "un #AARRGGBB conserva su alfa");
        Vale(Relleno.Leer("transparent")!.EsInvisible, "«transparent» es invisible");

        // `rgba()`, que es como viene la maqueta. El `.94` sin cero delante incluido.
        var rgba = Relleno.Leer("rgba(12,14,20,.94)")!;
        Igual((byte)12, rgba.Color.R, "rgba: el rojo");
        Igual((byte)240, rgba.Color.A, "rgba: el alfa .94 son 240 de 255");
        Igual((byte)9, Relleno.Leer("rgba(255,255,255,.035)")!.Color.A, "rgba: un alfa muy bajo no se pierde");

        // Un degradado de verdad, de los de los temas.
        var g = Relleno.Leer("linear-gradient(90deg,#FF2A6D,#FCEE0A,#00F0FF)")!;
        Vale(g.EsDegradado, "linear-gradient se lee como degradado");
        Igual(3, g.Paradas.Count, "con sus tres paradas");
        Igual(90.0, g.Angulo, "y su ángulo");
        Vale(Math.Abs(g.Paradas[1].Sitio - 0.5) < 0.001, "la parada de en medio se reparte al 50 %");
        Igual(0.0, g.Paradas[0].Sitio, "la primera va al principio");
        Igual(1.0, g.Paradas[^1].Sitio, "y la última al final");

        // Con por cientos escritos a mano.
        var pct = Relleno.Leer("linear-gradient(160deg,#1b2130 0%,#10141d 45%,#0a0c12 100%)")!;
        Vale(Math.Abs(pct.Paradas[1].Sitio - 0.45) < 0.001, "un «45%» se respeta");

        // El «+ rejilla en perspectiva verde» de los fondos de escritorio: se lee el
        // degradado y se ignora la prosa. Sin esto, el fondo de Videojuegos no se entendería
        // y el tema se quedaría sin fondo.
        var conProsa = Relleno.Leer("linear-gradient(180deg,#0B0B14,#12091A 55%,#05050A) + rejilla en perspectiva verde");
        Vale(conProsa is { EsDegradado: true }, "un fondo con «+ decoración» detrás se lee igual");

        // Lo que no se entiende es null, NUNCA una excepción: un color con una errata en el
        // config no puede tumbar el arranque, tiene que caer en lo derivado.
        Vale(Relleno.Leer("no soy un color") is null, "lo que no se entiende es null");
        Vale(Relleno.Leer("") is null, "una cadena vacía es null");
        Vale(Relleno.Leer(null) is null, "null es null");
        Vale(Relleno.Leer("linear-gradient(90deg,#FF2A6D,ni idea)") is null,
             "un degradado con una parada mala es null, no un degradado a medias");

        // Ida y vuelta: lo que se escribe se vuelve a leer igual. Hace falta para que el
        // Estudio pueda guardar un degradado en el config y recuperarlo.
        var vuelta = Relleno.Leer(g.Escribir())!;
        Igual(g.Paradas.Count, vuelta.Paradas.Count, "un degradado escrito se relee con sus paradas");
        Igual(g.Angulo, vuelta.Angulo, "y con su ángulo");
        Igual(g.Color, vuelta.Color, "y con el mismo color medio");

        // El ángulo de CSS contra los puntos de WPF. 180° es hacia abajo, que es el valor por
        // omisión y el que más se usa en los temas.
        var abajo = Relleno.Extremo(180, true);
        Vale(Math.Abs(abajo.X - 0.5) < 0.001 && Math.Abs(abajo.Y - 1) < 0.001,
             "180deg acaba abajo del todo");
        var derecha = Relleno.Extremo(90, true);
        Vale(Math.Abs(derecha.X - 1) < 0.001 && Math.Abs(derecha.Y - 0.5) < 0.001,
             "90deg acaba a la derecha");

        // La opacidad multiplica, no sustituye.
        Igual((byte)0x80, Relleno.Liso(Color.FromArgb(0xFF, 1, 2, 3)).ConOpacidad(0.5).Color.A,
              "la opacidad multiplica el alfa");
    }

    // ================= Los nueve de serie =================
    static void TemasDeSerieCargan()
    {
        Grupo("fase 0: los nueve temas de serie");

        var todos = TemasDeSerie.Todos;
        Igual(9, todos.Count, "son nueve temas");

        foreach (var id in new[] { "profesional", "oficina", "videojuegos", "invierno",
                                   "cyberpunk", "sakura", "samurai", "otono", "sad" })
            Vale(todos.ContainsKey(id), $"está el tema «{id}»");

        // Las 19 claves base, en los nueve. Es la exigencia del plan: «todas las claves tengan
        // valor en todos los temas». Para los de serie no vale derivarlas: sus valores son los
        // de la maqueta aprobada, y una que falte significa que se perdió al copiarla.
        foreach (var t in todos.Values)
            foreach (var clave in ClavesBase.Todas)
                Vale(t.Declarado(clave) is not null, $"[{t.Id}] declara «{clave}»");

        // Valores concretos de la maqueta, como centinelas de que el fichero es el bueno y no
        // una copia vieja.
        var pro = TemasDeSerie.De("profesional");
        Igual("#FF8DA9E8", Relleno.Hex(pro.Declarado(ClavesBase.Acento)!.Color), "Profesional: el acento es #8DA9E8");
        Vale(!pro.Claro, "Profesional es oscuro");
        Igual(23.0, pro.Radio(Piezas.Isla, 0), "Profesional: la isla tiene radio 23");
        Vale(pro.Declarado(ClavesBase.FiloPanel)!.EsDegradado, "Profesional: el filo del panel es un degradado");

        var juegos = TemasDeSerie.De("videojuegos");
        Igual(Esquinas.Chaflan, juegos.Esquinas, "Videojuegos va en chaflán");
        Igual(0.0, juegos.Radio(Piezas.Isla, 99), "y con radio 0");
        Vale(juegos.Lleva("modo-rgb"), "Videojuegos lleva el modo RGB");
        Igual("pixel-sube", juegos.Argumento("particulas"), "y sus partículas son píxeles que suben");
        Igual("Rajdhani", juegos.Fuente, "con la letra Rajdhani");

        var cyber = TemasDeSerie.De("cyberpunk");
        Igual("Orbitron", cyber.FuenteTitulos, "Cyberpunk: títulos en Orbitron");
        Igual("Rajdhani", cyber.Fuente, "y el resto en Rajdhani");
        Vale(cyber.Lleva("reloj-glitch") && cyber.Lleva("lineas-de-barrido"),
             "Cyberpunk lleva el glitch del reloj y las líneas de barrido");

        var samurai = TemasDeSerie.De("samurai");
        Igual("侍", samurai.Argumento("marca-de-agua"), "Samurái lleva el kanji de marca de agua");

        var sad = TemasDeSerie.De("sad");
        Vale(sad.Texto("saludo")?.EndsWith("…") == true, "Sad: el saludo acaba en puntos suspensivos");
        Igual("lluvia", sad.Argumento("particulas"), "y le llueve");

        // ---------- La única desviación respecto a la maqueta ----------
        //
        // Sakura ponía blanco sobre su acento rosa: 2,13:1, por debajo incluso del 3:1 que se
        // le exige a la etiqueta de un control relleno. Pasó a su propia tinta ciruela, que da
        // 4,77:1 y ya estaba en su paleta. Esta prueba existe para que nadie «restaure el
        // valor de la maqueta» dentro de seis meses sin saber por qué se cambió.
        Igual("#FF4A3B5C", Relleno.Hex(TemasDeSerie.De("sakura").Declarado(ClavesBase.SobreAcento)!.Color),
              "Sakura escribe en ciruela encima de su acento, no en blanco (2,13:1 era ilegible)");

        var oficina = TemasDeSerie.De("oficina");
        Vale(oficina.Claro, "Oficina es claro");
        Igual(0, oficina.Decoraciones.Count, "y no lleva ninguna decoración: sin mascota ni partículas");
        Vale(oficina.Declarado(ClavesBase.Halo)!.EsInvisible, "su halo está apagado a propósito");

        // El fondo con prosa detrás se guarda sin perderla.
        Vale(juegos.FondoExtra?.Contains("rejilla") == true,
             "la decoración del fondo de Videojuegos («rejilla») no se tira al leer el degradado");

        // Un id que no existe cae en Profesional, nunca en el más llamativo. Misma regla que
        // `Estilos.Leer` y que `NubeHypr.ModoEfectivo`: un config con una errata no puede
        // dejarte el escritorio lleno de neón.
        Igual("profesional", TemasDeSerie.De("no existe").Id, "un tema desconocido cae en Profesional");
        Igual("profesional", TemasDeSerie.De(null).Id, "y sin tema, también");
        Igual("sakura", TemasDeSerie.De("  SAKURA  ").Id, "el id no distingue mayúsculas ni se queja de espacios");
    }

    // ================= El catálogo =================
    static void CatalogoEntero()
    {
        Grupo("fase 0: el catálogo de colores");

        var todas = Catalogo.Todas;
        Vale(todas.Count >= 100, $"el catálogo cubre la interfaz ({todas.Count} claves)");

        // Claves repetidas. Con una tabla escrita a mano es el fallo más fácil de cometer, y
        // el más silencioso: la segunda gana y la primera deja de poder editarse.
        var repes = todas.GroupBy(c => c.Clave, StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Vale(repes.Count == 0, $"no hay claves repetidas ({string.Join(", ", repes)})");

        // El nombre de una clave es parte de la configuración del usuario: se escribe en su
        // config.json y en los temas que exporta. Así que tiene que ser escribible a mano.
        foreach (var c in todas)
            Vale(c.Clave.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch == '.' || ch == '-'),
                 $"«{c.Clave}» se escribe en minúsculas, puntos y guiones");

        // Todo `Sobre` apunta a una clave que existe: si no, el contraste se mediría contra
        // transparente y el aviso del selector diría cualquier cosa.
        foreach (var c in todas.Where(c => c.Sobre is not null))
            Vale(Catalogo.Existe(c.Sobre), $"«{c.Clave}» se pinta sobre «{c.Sobre}», que existe");

        // Un texto o un icono SIN fondo declarado no se puede medir, así que su aviso de
        // contraste nunca saltaría: es una clave que miente por omisión.
        foreach (var c in todas.Where(c => c.Papel is Papel.Texto or Papel.Icono))
            Vale(c.Sobre is not null, $"«{c.Clave}» hace de {c.Papel.ToString().ToLowerInvariant()} y dice sobre qué");

        // Todas las claves base están en el catálogo con su nombre exacto: es lo que hace que
        // el tema y el catálogo compartan un solo juego de nombres.
        foreach (var clave in ClavesBase.Todas)
            Vale(Catalogo.Existe(clave), $"la clave base «{clave}» está en el catálogo");

        // Y todas las zonas tienen rótulo, que es lo que agrupa la lista de Ajustes.
        foreach (ZonaColor z in Enum.GetValues<ZonaColor>())
        {
            Vale(Catalogo.RotuloDeZona(z) != z.ToString(), $"la zona {z} tiene rótulo");
            Vale(Catalogo.Todas.Any(c => c.Zona == z), $"y al menos una clave");
        }
    }

    // ================= La cascada =================
    static void CascadaDeColores()
    {
        Grupo("fase 0: la cascada usuario → tema → derivado");

        var pro = new Pintura(TemasDeSerie.De("profesional"));

        // Escalón 2: lo que dice el tema.
        Igual("#FF8DA9E8", Relleno.Hex(pro.Color(ClavesBase.Acento)), "el acento sale del tema");

        // Escalón 3: lo derivado. `musica.progreso` no está en ningún tema; sale del acento.
        Igual(pro.Color(ClavesBase.Acento), pro.Color("musica.progreso"),
              "la barra de progreso se deriva del acento");

        // Escalón 1: el usuario manda sobre los dos.
        var mio = new Pintura(TemasDeSerie.De("profesional"),
                              new Dictionary<string, string> { ["acento"] = "#FF0000" });
        Igual("#FFFF0000", Relleno.Hex(mio.Color(ClavesBase.Acento)), "lo que pone el usuario manda");
        Igual("#FFFF0000", Relleno.Hex(mio.Color("musica.progreso")),
              "y arrastra con él todo lo que se deriva del acento");
        Igual("#FFFF0000", Relleno.Hex(mio.Color("ecualizador.barra")),
              "y el ecualizador también");
        Vale(mio.Personalizado("acento") && !mio.Personalizado("tinta"),
             "se sabe qué claves ha tocado el usuario y cuáles no");

        // Un color del usuario mal escrito se ignora y se sigue con el del tema: no puede
        // dejar la barra sin pintar.
        var roto = new Pintura(TemasDeSerie.De("profesional"),
                               new Dictionary<string, string> { ["acento"] = "#no" });
        Igual("#FF8DA9E8", Relleno.Hex(roto.Color(ClavesBase.Acento)),
              "un color del usuario ilegible cae en el del tema");

        // Una clave que no existe no lanza: transparente y apuntada.
        Vale(pro.R("me.la.acabo.de.inventar").EsInvisible, "una clave desconocida sale transparente");

        // Un tema que solo declara cuatro colores: todo lo demás tiene que salir coherente.
        // Es la promesa de la decisión B, y si se rompe, los temas del usuario salen a medias.
        var cuatro = new TemaBase
        {
            Id = "cuatro", Nombre = "Cuatro colores", Claro = false,
            Colores = new Dictionary<string, Relleno>(StringComparer.OrdinalIgnoreCase)
            {
                [ClavesBase.Superficie] = Relleno.Leer("rgba(20,10,30,.95)")!,
                [ClavesBase.Tinta] = Relleno.Leer("#F0EAFF")!,
                [ClavesBase.Acento] = Relleno.Leer("#9B5DE5")!,
                [ClavesBase.Peligro] = Relleno.Leer("#FF5C8A")!,
            },
        };
        var p4 = new Pintura(cuatro);
        var sinValor = Catalogo.Todas.Where(c => p4.R(c.Clave).EsInvisible).Select(c => c.Clave).ToList();
        Vale(sinValor.Count == 0,
             $"[cuatro colores] ninguna clave se queda sin valor ({string.Join(", ", sinValor)})");
        Igual(0, p4.Ciclos, "y ninguna derivación da vueltas sobre sí misma");

        // Las derivaciones encadenan de verdad: tocar `tarjeta` mueve el botón.
        var conTarjeta = new Pintura(TemasDeSerie.De("profesional"),
                                     new Dictionary<string, string> { ["tarjeta"] = "#FF102030" });
        Igual("#FF102030", Relleno.Hex(conTarjeta.Color("boton.fondo")),
              "cambiar el fondo de las tarjetas cambia el de los botones");

        // Y el radio sale del tema, con un valor razonable si el tema calla.
        Igual(23.0, pro.Radio(Piezas.Isla), "el radio de la isla sale del tema");
        Vale(new Pintura(cuatro).Radio(Piezas.Isla) > 0, "y un tema sin radios tiene uno igualmente");
    }

    // ================= El contraste de los nueve =================
    static void ContrasteDeLosNueve()
    {
        Grupo("fase 0: los nueve temas de serie se leen");

        foreach (var t in TemasDeSerie.Lista)
        {
            var p = new Pintura(t);

            // Todas las claves resuelven y ninguna derivación se muerde la cola.
            foreach (var c in Catalogo.Todas) p.R(c.Clave);
            Igual(0, p.Ciclos, $"[{t.Id}] ninguna derivación circular");

            // Y lo que hace de texto o de icono se lee sobre su fondo. Es lo que el plan
            // exige de los temas de serie: «las pruebas exigen que los 9 de serie pasen el
            // contraste».
            foreach (var c in Catalogo.Todas.Where(c => c.SeMide))
            {
                double r = p.ContrasteDe(c.Clave);
                Vale(r >= c.MinimoContraste,
                     $"[{t.Id}] «{c.Clave}» sobre «{c.Sobre}»: {r:0.00}:1 (mínimo {c.MinimoContraste:0.0})");
            }
        }
    }

    // ================= La regla de seguridad =================
    static void AjustesNuncaIlegible()
    {
        Grupo("fase 0: un tema nunca deja Ajustes ilegible");

        foreach (var t in TemasDeSerie.Lista)
            Vale(new Pintura(t).AjustesSeLee, $"[{t.Id}] Ajustes se lee con el tema puesto");

        // El caso que la regla existe para atajar: un tema del usuario con el texto del color
        // del fondo. Ajustes tiene que pintarse con Profesional, no quedarse en blanco sobre
        // blanco — si no, se ha quedado sin la ventana desde la que arreglarlo.
        var suicida = new Pintura(TemasDeSerie.De("profesional"), new Dictionary<string, string>
        {
            ["tinta"] = "#0C0E14",          // el texto, del color exacto de la superficie
            ["tenue"] = "#0C0E14",
        });
        Vale(!suicida.AjustesSeLee, "un tema con el texto del color del fondo no pasa");
        Igual("profesional", suicida.ParaAjustes().Tema.Id, "y Ajustes se pinta con Profesional");
        Igual(0, suicida.ParaAjustes().DelUsuario.Count,
              "sin arrastrar las personalizaciones que lo rompieron");
        Vale(suicida.Ilegibles().Count > 0, "y se puede decir qué claves no se leen");

        // La pintura de un tema sano es ella misma, no una copia: Ajustes con el tema del
        // usuario puesto es el caso normal.
        var sana = new Pintura(TemasDeSerie.De("sakura"));
        Vale(ReferenceEquals(sana, sana.ParaAjustes()), "con un tema sano, Ajustes usa el tema");
    }
}
