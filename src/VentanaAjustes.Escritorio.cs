using System;
using System.Windows;
using System.Windows.Controls;

namespace NubeBar;

// La página «Escritorio»: el dock y los paneles laterales de la macrofase E.
//
// ---------- Por qué existe esta página ----------
//
// La macrofase E construyó el dock (E2) y los dos paneles laterales (E3 y E4). Están
// escritos, tienen pruebas, salen en las capturas… y el usuario no los ha visto nunca,
// porque **no había forma de encenderlos**:
//
//   · `Barra.Dock` viene en «nunca» y la única manera de cambiarlo era editar el
//     `config.json` a mano. En todo el programa, el único sitio que escribía esa clave era
//     el diagnóstico de capturas, para poder fotografiar el dock.
//   · `AbrirLateral` solo lo llamaba ese mismo diagnóstico. Los paneles no tenían ni
//     interruptor ni botón ni atajo.
//
// Una función que existe y no tiene interruptor es una función que no tiene nadie. Esto es
// el interruptor.
//
// ---------- Y lo que sigue apagado ----------
//
// Todo, de serie. Encender el dock es una decisión del usuario, igual que NubeHypr: una
// actualización no puede cambiarle a nadie la forma del escritorio sin que lo haya pedido.
public partial class VentanaAjustes
{
    // Los tres valores que entiende `Dock.Leer`, con el nombre que se lee en pantalla.
    // El mapa va en los dos sentidos y en un solo sitio: si la lista y el lector
    // discreparan, elegir una opción no haría nada y nadie sabría por qué.
    static readonly (string Config, string Visible)[] CuandoElDock =
    {
        ("nunca", "Nunca"),
        ("alacercarse", "Al acercar el ratón al borde"),
        ("siempre", "Siempre a la vista"),
    };

    // Tres aberturas para la prueba. Lo que se quiere garantizar es que esta lista y lo que
    // `Dock.Leer` entiende no se separen nunca: una opción que el lector no reconozca se
    // convertiría en «siempre» sin que nadie lo hubiera pedido.
    internal static (string Config, string Visible)[] OpcionesDelDockParaPruebas => CuandoElDock;
    internal static string DockVisibleDeParaPruebas(string? c) => DockVisibleDe(c);
    internal static string DockConfigDeParaPruebas(string v) => DockConfigDe(v);

    static string DockVisibleDe(string? config)
    {
        string c = (config ?? "nunca").Trim().ToLowerInvariant();
        foreach (var (k, v) in CuandoElDock) if (k == c) return v;
        // `Dock.Leer` convierte lo desconocido en «siempre»; aquí se enseña lo mismo que
        // va a pasar, no lo que está escrito en el fichero.
        return CuandoElDock[2].Visible;
    }

    static string DockConfigDe(string visible)
    {
        foreach (var (k, v) in CuandoElDock) if (v == visible) return k;
        return "nunca";
    }

    UIElement SeccionEscritorio()
    {
        var p = NuevaPagina("escritorio");

        p.Grupo("Dock", IconoDeGrupo("Dock"));
        p.Mas(new TextBlock
        {
            Text = "Una fila de iconos flotando sobre el escritorio, con las apps fijadas y " +
                   "las abiertas. Es independiente de la barra: puedes tener las dos cosas, " +
                   "o solo una.",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var valores = new string[CuandoElDock.Length];
        for (int i = 0; i < CuandoElDock.Length; i++) valores[i] = CuandoElDock[i].Visible;

        p.Mas(Opciones("Cuándo se ve", valores,
                       DockVisibleDe(_arbol.Texto("Barra", "Dock", "nunca")),
                       v => _arbol.Poner("Barra", "Dock", DockConfigDe(v))));

        p.Mas(new TextBlock
        {
            Text = "«Al acercar el ratón» lo deja escondido hasta que bajas al borde, y no " +
                   "se esconde mientras tengas el ratón encima: si lo hiciera, desaparecería " +
                   "justo cuando vas a pulsar un icono.",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
        });

        // ---------- Los paneles laterales ----------
        //
        // Se nombran aquí aunque todavía no tengan interruptor, y eso es a propósito: están
        // construidos y no se pueden abrir, y callárselo sería dejar al usuario sin saber
        // que existen. Decir «esto está hecho y le falta la puerta» es más útil que una
        // página que no los menciona.
        p.Grupo("Paneles laterales", IconoDeGrupo("Paneles laterales"));
        p.Mas(new TextBlock
        {
            Text = "El panel izquierdo trae tu perfil, las acciones rápidas, volumen, brillo, " +
                   "carpetas y el estado de la máquina. El derecho, las notificaciones, el " +
                   "calendario y el escritorio actual.",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });

        // Las mismas tres opciones que el dock, y a propósito: es el mismo gesto de borde y
        // el mismo lector (`Dock.Leer`). Dos vocabularios para lo mismo serían dos cosas que
        // aprender para un solo concepto.
        p.Mas(Opciones("Cuándo se abren", valores,
                       DockVisibleDe(_arbol.Texto("Barra", "Laterales", "nunca")),
                       v => _arbol.Poner("Barra", "Laterales", DockConfigDe(v))));

        p.Mas(new TextBlock
        {
            Text = "Con «al acercar el ratón», se abren al llevar el ratón al filo izquierdo " +
                   "o derecho de la pantalla, y se quedan mientras lo tengas dentro. El borde " +
                   "donde esté la barra no cuenta: si tienes la barra en vertical a la " +
                   "izquierda, acercarse ahí es para usar la barra, no para abrir un panel.\n\n" +
                   "Vienen apagados. Un panel de 340 px que aparece solo al rozar el borde " +
                   "estorba si no lo has pedido.",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
        });

        // Y qué lleva cada panel, que es cosa suya: esta página ya se llama «El dock y los
        // paneles laterales». Ver `VentanaAjustes.Laterales.cs`.
        GruposDeLosLaterales(p);

        return p.Fin();
    }
}
