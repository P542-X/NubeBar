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
    //
    // ---------- Roles ya no deriva nada: pregunta al catálogo ----------
    //
    // Antes esto tenía sus propias cuentas (el velo de la tarjeta, el texto atenuado, la
    // selección) y el resto de la interfaz tenía las suyas escritas a mano. Dos sitios donde
    // decidir el mismo color es un sitio de más, y el rediseño lo prohíbe de frente: «nada de
    // colores escritos a mano en la interfaz» (decisión B).
    //
    // Así que la cascada está ahora en un solo sitio, `Pintura`, y Roles es la FACHADA: cada
    // rol es una clave del catálogo con un nombre viejo. Lo que esto compra es que los
    // widgets, `HyprPinta`, `Tonos` y los paneles sigan pintando igual —no se toca ni una
    // línea de ellos— mientras el color de verdad ya sale del catálogo y el usuario ya puede
    // cambiarlo.
    //
    // Y lo que esto es: temporal. A medida que las fases 1 a 8 pasen cada pieza a preguntar
    // por su clave (`deslizador.pomo` en vez de `Roles.Acento`), esta fachada se queda sin
    // consumidores y desaparece. Mientras tenga alguno, aquí está.
    public Pintura Pintura { get; init; } = null!;

    // Atajos para preguntar por CLAVE desde donde ya hay un `_roles` a mano. Es el camino por
    // el que la interfaz va dejando de tener colores escritos: `_roles.Pincel("bateria.ahorro")`
    // en vez de `Brushes.Gold`. Cuando no quede ningún rol con nombre viejo, estos dos son lo
    // único que seguirá hablando, y entonces lo que sobra es la fachada, no ellos.
    // `ColorDe` y no `Color`: un miembro llamado `Color` dentro de esta clase tapa al tipo
    // `System.Windows.Media.Color` y obliga a cualificarlo en cada línea de las de abajo.
    public Brush Pincel(string clave) => Pintura.Pincel(clave);
    public Color ColorDe(string clave) => Pintura.Color(clave);

    public static Roles Desde(Pintura p)
    {
        var fondo = p.Color("isla.fondo");
        var fondoOpaco = Color.FromRgb(fondo.R, fondo.G, fondo.B);

        var texto = p.Color(ClavesBase.Tinta);
        var acento = p.Color(ClavesBase.Acento);
        var superficie = p.Color(ClavesBase.Tarjeta);
        var superficieAlta = p.Color("tarjeta.fondo.raton");
        var secundario = p.Color(ClavesBase.Sec1);
        var sistema = p.Color(ClavesBase.Sec2);
        var positivo = p.Color(ClavesBase.Ok);
        var aviso = p.Color("aviso");
        var negativo = p.Color(ClavesBase.Peligro);

        // La selección se calcula aquí y no se lee de `resultado.fondo.elegido` a propósito.
        // El catálogo trae el valor de la MAQUETA (acento al 18 %) y este rol llevaba el de
        // antes (0x33, o sea 20 %). Son casi iguales pero no iguales, y mientras la fachada
        // exista su trabajo es que nada cambie de aspecto sin que se haya decidido. Cuando el
        // menú y los paneles pasen a preguntar por su clave, se irán al 18 % de la maqueta.
        var seleccion = Velo(acento, VeloSeleccion);

        return new Roles
        {
            Pintura = p,

            Fondo = fondo,
            FondoOpaco = fondoOpaco,
            Superficie = superficie,
            SuperficieSolida = Aplanar(superficie, fondoOpaco),
            SuperficieAlta = superficieAlta,
            SuperficieAltaSolida = Aplanar(superficieAlta, fondoOpaco),
            Borde = p.Color(ClavesBase.Filo),

            Texto = texto,
            TextoAtenuado = p.Color(ClavesBase.Tenue),

            Acento = acento,
            AcentoTinta = Contraste.Legible(acento, fondoOpaco),
            Seleccion = seleccion,
            SeleccionSolida = Aplanar(seleccion, fondoOpaco),
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

            Radio = p.Radio(Piezas.Isla),
        };
    }

    // ---------- Y la entrada de los temas de antes ----------
    //
    // La firma de siempre, para no tocar los veinte sitios que la llaman. Por dentro ya no
    // deriva: construye el paquete de valores equivalente (`TemaBase.DeTemaViejo`, que es
    // donde viven ahora las cuentas de antes) y lo pasa por la misma cascada que los nueve
    // temas nuevos.
    public static Roles Desde(Tema t, Color fondo, Color texto, Color acento) =>
        Desde(new Pintura(TemaBase.DeTemaViejo(t, fondo, texto, acento)));

    // Los alfas de los velos derivados. No son números redondos por capricho: son los
    // que el código ya usaba a mano (0x14 y 0x22 para una fila, 0x33 para la fila bajo
    // el ratón), promediados, para que los temas de siempre no cambien de aspecto.
    const byte VeloSeleccion = 0x33;

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

    // Roles de emergencia, para antes de que haya tema aplicado (y para los tests de
    // un widget suelto). Nunca se devuelve null: un widget siempre tiene de qué pintar.
    public static Roles PorDefecto() => Desde(Pintura.PorDefecto());
}
