using System;
using System.Windows.Media;

namespace NubeBar;

// Los colores del tema, por ROL y no por nombre de color.
//
// Hasta ahora un tema eran tres colores (Fondo, Texto, Acento) y cada trozo de la
// isla se los apañaba: el centro de control mezclaba a ojo para sus filas, la vista
// general aclaraba el fondo por su cuenta, y "atenuado" era un Opacity=0.6 puesto a
// mano en cada sitio. Mientras los módulos fueran dieciséis casos de un switch eso
// se aguantaba. Con widgets que no escribimos nosotros, no: un widget necesita poder
// preguntar "¿de qué color pinto una fila?" y recibir una respuesta que valga en los
// seis temas de serie y en el que se invente el usuario.
//
// De ahí los roles. Un tema puede dar solo los tres colores de siempre —y entonces
// el resto se DERIVA, así que los seis temas que ya existen siguen pintando
// exactamente igual que antes— o puede dar los que quiera y mandar sobre lo derivado.
//
// Regla de las tintas: un color de relleno y un color de texto no son lo mismo. El
// coral del tema Nube se lee bien sobre el azul profundo (5,9:1) y mal sobre una
// superficie clara (2,7:1). Por eso cada acento tiene su pareja "Tinta", que es el
// mismo color pasado por Contraste.Legible contra el fondo de verdad. Esta idea ya
// estaba en el código (el _cAcentoLegible de MainWindow); aquí se generaliza.
public sealed class Roles
{
    // ---------- Superficies ----------
    // Superficie y SuperficieAlta se PINTAN y pueden venir translúcidas (es lo normal
    // cuando se derivan). Sus parejas "Solida" son lo que se VE una vez compuestas
    // sobre el fondo de la isla, y son las que hay que usar para MEDIR contraste: un
    // color con alfa no tiene luminancia propia hasta que se sabe qué hay detrás.
    public Color Fondo { get; init; }            // la isla o el panel, con su transparencia
    public Color FondoOpaco { get; init; }        // el mismo sin alfa: para medir contraste
    public Color Superficie { get; init; }        // una tarjeta o una fila DENTRO de un panel
    public Color SuperficieSolida { get; init; }
    public Color SuperficieAlta { get; init; }    // el elemento bajo el ratón o el elegido
    public Color SuperficieAltaSolida { get; init; }
    public Color Borde { get; init; }             // separadores y filos

    // ---------- Tinta ----------
    public Color Texto { get; init; }
    public Color TextoAtenuado { get; init; }     // segundo renglón, unidades, pistas

    // ---------- Acentos ----------
    public Color Acento { get; init; }            // relleno, filo, la rayita de la app activa
    public Color AcentoTinta { get; init; }       // el acento cuando hace de TEXTO

    // El fondo de lo que está ELEGIDO: la wifi a la que estás conectado, la app
    // marcada en el menú. Es un velo del acento, no el acento a secas, porque encima
    // tiene que poder leerse texto.
    public Color Seleccion { get; init; }
    public Color SeleccionSolida { get; init; }
    public Color Secundario { get; init; }        // el naranja del tema Nube: detalles cálidos
    public Color SecundarioTinta { get; init; }
    public Color Sistema { get; init; }           // el azul nube: red, volumen, batería
    public Color SistemaTinta { get; init; }

    // ---------- Semánticos ----------
    // El documento pide "diferenciar informacion, aviso y accion requerida", y pide
    // no comunicar un estado solo con color. Estos tres son para el color; el icono
    // y el texto los pone quien los use.
    public Color Positivo { get; init; }
    public Color PositivoTinta { get; init; }
    public Color Aviso { get; init; }
    public Color AvisoTinta { get; init; }
    public Color Negativo { get; init; }
    public Color NegativoTinta { get; init; }

    // ---------- Forma ----------
    public double Radio { get; init; }

    // Mismo criterio que Contraste.Minimo, pero el de las superficies claras tiene que
    // ser más exigente: ahí es donde el coral #E87562 se cae a 2,7:1 y hay que
    // profundizarlo. 4.5 es el AA de la WCAG para texto normal.
    public const double MinimoSuperficie = 4.5;

    // Un color legible sobre una superficie cualquiera, no solo sobre el fondo de la
    // isla. La usarán los paneles que se pintan sobre Superficie en vez de sobre Fondo.
    //
    // Estática porque no mira ningún rol de esta instancia: le das los dos colores y
    // te devuelve el corregido. Así se puede comprobar un par de colores sin tener un
    // tema montado.
    public static Color TintaSobre(Color color, Color superficie) =>
        Contraste.Legible(color, superficie, MinimoSuperficie);

    // ================= Construcción =================

    // De un Tema del config a los roles. Lo que el tema no diga se deriva del fondo y
    // del texto, que es lo que mantiene a los temas viejos pintando igual.
    public static Roles Desde(Tema t, Color fondo, Color texto, Color acento)
    {
        var fondoOpaco = Color.FromRgb(fondo.R, fondo.G, fondo.B);

        // Las superficies derivadas son un VELO TRANSLÚCIDO del color de texto, no un
        // color opaco. Es exactamente lo que el código venía haciendo a mano por todas
        // partes —`FromArgb(0x14 / 0x22 / 0x33, _cTexto…)`— y es lo que hace que una
        // fila de un panel deje pasar un poco el fondo de pantalla, igual que la isla.
        //
        // Si se derivaran opacas, los seis temas de siempre cambiarían de aspecto el
        // día que empezaran a usar roles, que es justo lo que no puede pasar. Un tema
        // que las declare manda, y puede darlas opacas: el tema Nube lo hace.
        var superficie     = Leer(t.Superficie)     ?? Velo(texto, VeloSuperficie);
        var superficieAlta = Leer(t.SuperficieAlta) ?? Velo(texto, VeloAlta);
        var borde          = Leer(t.Borde)          ?? Velo(texto, VeloBorde);

        // El texto atenuado sí es opaco: es tinta, y bajarle el alfa lo dejaría
        // dependiendo del fondo de pantalla para poder leerse.
        var atenuado       = Leer(t.TextoAtenuado)  ?? Contraste.Mezclar(texto, fondoOpaco, 0.35);

        // Secundario y Sistema, si el tema no los da, son el propio acento: así un tema
        // que solo trae tres colores NO se inventa un naranja que su autor no eligió.
        var secundario = Leer(t.Secundario) ?? acento;
        var sistema    = Leer(t.Sistema)    ?? acento;

        // Los semánticos sí tienen color propio aunque el tema calle: un "batería baja"
        // en rojo tiene que seguir siendo rojo en Gruvbox.
        var positivo = Leer(t.Positivo) ?? Color.FromRgb(0x2E, 0xA0, 0x72);
        var aviso    = Leer(t.Aviso)    ?? Color.FromRgb(0xE8, 0x9C, 0x3C);
        var negativo = Leer(t.Negativo) ?? Color.FromRgb(0xDA, 0x44, 0x53);

        return new Roles
        {
            Fondo = fondo,
            FondoOpaco = fondoOpaco,
            Superficie = superficie,
            SuperficieSolida = Aplanar(superficie, fondoOpaco),
            SuperficieAlta = superficieAlta,
            SuperficieAltaSolida = Aplanar(superficieAlta, fondoOpaco),
            Borde = borde,

            Texto = texto,
            TextoAtenuado = atenuado,

            Acento = acento,
            AcentoTinta = Contraste.Legible(acento, fondoOpaco),
            Seleccion = Velo(acento, VeloSeleccion),
            SeleccionSolida = Aplanar(Velo(acento, VeloSeleccion), fondoOpaco),
            Secundario = secundario,
            SecundarioTinta = Contraste.Legible(secundario, fondoOpaco),
            Sistema = sistema,
            SistemaTinta = Contraste.Legible(sistema, fondoOpaco),

            Positivo = positivo,
            PositivoTinta = Contraste.Legible(positivo, fondoOpaco),
            Aviso = aviso,
            AvisoTinta = Contraste.Legible(aviso, fondoOpaco),
            Negativo = negativo,
            NegativoTinta = Contraste.Legible(negativo, fondoOpaco),

            Radio = t.Radio
        };
    }

    // Los alfas de los velos derivados. No son números redondos por capricho: son los
    // que el código ya usaba a mano (0x14 y 0x22 para una fila, 0x33 para la fila bajo
    // el ratón), promediados, para que los temas de siempre no cambien de aspecto.
    const byte VeloSuperficie = 0x1E, VeloAlta = 0x33, VeloBorde = 0x3A, VeloSeleccion = 0x33;

    static Color Velo(Color sobre, byte alfa) => Color.FromArgb(alfa, sobre.R, sobre.G, sobre.B);

    // Lo que se ve de verdad cuando ese color se pinta encima del fondo: si es opaco,
    // él mismo; si trae alfa, el resultado de componerlo. Hace falta para medir
    // contraste, porque un #1EFFFFFF no se "lee" claro ni oscuro por sí solo.
    //
    // `internal` desde que existen los azulejos de los paneles: su relleno es el color del
    // icono rebajado, y para saber qué tinta poner ENCIMA hay que saber antes de qué color
    // queda ese relleno una vez compuesto. Es justo la pregunta que contesta.
    internal static Color Aplanar(Color c, Color fondo) =>
        c.A == 0xFF ? c : Contraste.Mezclar(fondo, Color.FromRgb(c.R, c.G, c.B), c.A / 255.0);

    // Un hex del config, o null si no está puesto o está mal escrito. Un color mal
    // escrito no puede tumbar el arranque: se deriva y se apunta.
    static Color? Leer(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch
        {
            Log.Info($"el color \"{hex}\" del tema no se entiende: se usa el derivado");
            return null;
        }
    }

    // Roles de emergencia, para antes de que haya tema aplicado (y para los tests de
    // un widget suelto). Nunca se devuelve null: un widget siempre tiene de qué pintar.
    public static Roles PorDefecto() =>
        Desde(new Tema(), Color.FromArgb(0xE6, 0, 0, 0), Colors.White, Color.FromRgb(0x7A, 0xA2, 0xF7));
}
