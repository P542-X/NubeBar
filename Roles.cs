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
    public Color Fondo { get; init; }            // la isla o el panel, con su transparencia
    public Color FondoOpaco { get; init; }        // el mismo sin alfa: para medir contraste
    public Color Superficie { get; init; }        // una tarjeta o una fila DENTRO de un panel
    public Color SuperficieAlta { get; init; }    // el elemento bajo el ratón o el elegido
    public Color Borde { get; init; }             // separadores y filos

    // ---------- Tinta ----------
    public Color Texto { get; init; }
    public Color TextoAtenuado { get; init; }     // segundo renglón, unidades, pistas

    // ---------- Acentos ----------
    public Color Acento { get; init; }            // relleno, filo, la rayita de la app activa
    public Color AcentoTinta { get; init; }       // el acento cuando hace de TEXTO
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

        // Las superficies se sacan acercando el fondo al texto, no aclarando a ciegas:
        // así funciona igual en un tema oscuro (sube) y en uno claro (baja).
        var superficie     = Leer(t.Superficie)     ?? Contraste.Mezclar(fondoOpaco, texto, 0.07);
        var superficieAlta = Leer(t.SuperficieAlta) ?? Contraste.Mezclar(fondoOpaco, texto, 0.14);
        var borde          = Leer(t.Borde)          ?? Contraste.Mezclar(fondoOpaco, texto, 0.22);
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
            SuperficieAlta = superficieAlta,
            Borde = borde,

            Texto = texto,
            TextoAtenuado = atenuado,

            Acento = acento,
            AcentoTinta = Contraste.Legible(acento, fondoOpaco),
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
