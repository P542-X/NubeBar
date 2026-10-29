using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// `VentanaAjustes` ya tiene un método suyo llamado `Lateral()`, y dentro de la clase ese
// nombre tapa al del catálogo: `CatLat.Lado` se lee como «la propiedad Lado del método
// Lateral» y no compila. El alias deshace la ambigüedad sin renombrar un método que usan
// otros ficheros.
using CatLat = NubeBar.Lateral;

namespace NubeBar;

// Mover las secciones de los paneles laterales: de panel, y de sitio dentro del panel.
//
// ---------- Por qué existe ----------
//
// Los módulos de la barra se mueven desde la fase A. Las secciones de los paneles no: su
// panel y su orden estaban escritos en el catálogo, o sea decididos por mí. El usuario lo
// pidió sin rodeos: «quiero que todo sea editable y movible; si quiero que el ethernet se
// mueva y se ponga en el medio que pueda hacerlo, si quiero que el tiempo se ponga arriba
// que pueda hacerlo».
//
// ---------- Por qué con botones y no arrastrando ----------
//
// Arrastrar es mejor cuando funciona y un pozo cuando no: hace falta sitio para soltar,
// desplazamiento automático al llegar al borde, una marca de dónde va a caer, y que todo eso
// funcione también con el teclado. La barra lo tiene porque sus módulos se ven mientras los
// arrastras; aquí estaríamos arrastrando en una ventana que NO es el panel que se edita.
//
// Tres botones por fila —subir, bajar y cambiar de panel— hacen lo mismo, funcionan con el
// tabulador y con un lector de pantalla, y no pueden dejar una sección «caída» en ningún
// sitio raro.
public partial class VentanaAjustes
{
    // Va dentro de la página «Escritorio», que ya se llama «El dock y los paneles laterales»:
    // ahí es donde alguien busca esto. Una página nueva para dos listas habría repartido el
    // mismo tema en dos sitios.
    void GruposDeLosLaterales(Pagina p)
    {
        p.Grupo("Qué lleva cada panel", IconoDeGrupo("panel"),
                "Mueve cualquier sección de un panel al otro, o súbela y bájala dentro del " +
                "suyo. El orden que dejes aquí es el que verás al abrirlos.");

        p.Mas(ListaDeLado(CatLat.Lado.Izquierda, "Panel izquierdo"));
        p.Mas(ListaDeLado(CatLat.Lado.Derecha, "Panel derecho"));

        p.Mas(BotonPlano("Volver al reparto de serie", () =>
        {
            _arbol.PonerLista("Laterales", "Izquierda", new List<string>());
            _arbol.PonerLista("Laterales", "Derecha", new List<string>());
            Cambio();
            RehacerPagina("escritorio", SeccionEscritorio);
        }, false));

        p.Pista("Dos listas vacías significan «el reparto de serie», no «paneles vacíos»: por " +
                "eso volver atrás es seguro y no te deja sin nada.");
        p.Pista("Una sección que llegue en una actualización futura aparece sola al final de " +
                "su panel, no se queda invisible porque tú ya tuvieras un orden guardado.");
    }

    // El reparto tal y como está AHORA, leído del árbol. No del modelo tipado: aquí hay que
    // enseñar lo que el usuario acaba de mover, y el modelo se recarga al guardar.
    (List<string> Izq, List<string> Der) RepartoDeAhora() =>
        (_arbol.Lista("Laterales", "Izquierda").ToList(),
         _arbol.Lista("Laterales", "Derecha").ToList());

    IReadOnlyList<CatLat.Seccion> LoQueLleva(CatLat.Lado l)
    {
        var (izq, der) = RepartoDeAhora();
        return CatLat.Repartidas(l, izq, der);
    }

    // ---------- Guardar los DOS lados a la vez ----------
    //
    // Siempre los dos, aunque solo se haya tocado uno. Con una sola lista guardada, la otra
    // se queda vacía y `Repartidas` la trata como «la de serie» — y entonces una sección que
    // acabas de mover saldría en los dos paneles a la vez. Pasa en cuanto mueves la primera.
    void GuardarReparto(List<string> izq, List<string> der)
    {
        _arbol.PonerLista("Laterales", "Izquierda", izq);
        _arbol.PonerLista("Laterales", "Derecha", der);
        Cambio();
        RehacerPagina("escritorio", SeccionEscritorio);
    }

    UIElement ListaDeLado(CatLat.Lado lado, string titulo)
    {
        var col = new StackPanel { Margin = new Thickness(0, 10, 0, 4) };
        col.Children.Add(new TextBlock
        {
            Text = titulo, Foreground = _texto, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        });

        var ss = LoQueLleva(lado);
        if (ss.Count == 0)
        {
            col.Children.Add(Rotulo("Este panel está vacío. Mueve alguna sección desde el otro."));
            return col;
        }

        for (int i = 0; i < ss.Count; i++) col.Children.Add(FilaDeSeccion(lado, ss, i));
        return col;
    }

    FrameworkElement FilaDeSeccion(CatLat.Lado lado, IReadOnlyList<CatLat.Seccion> ss, int i)
    {
        var s = ss[i];
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var ic = new TextBlock
        {
            Text = char.ConvertFromUtf32(s.Icono),
            FontFamily = new FontFamily(FuenteDeIconos),
            FontSize = 14, Foreground = _acento,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Grid.SetColumn(ic, 0);
        g.Children.Add(ic);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock { Text = s.Titulo, Foreground = _texto, FontSize = 12.5 });
        textos.Children.Add(new TextBlock
        {
            Text = s.Que, Foreground = _textoAtenuado, FontSize = 10.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(textos, 1);
        g.Children.Add(textos);

        var mandos = new StackPanel { Orientation = Orientation.Horizontal };

        // ---------- Los glifos van por CÓDIGO ----------
        //
        // No escritos dentro de una cadena: viven en el área privada de la fuente de iconos y,
        // metidos en texto normal, salen como una barra vertical. Esta misma sesión ya me
        // costó una flecha de cabecera, y la primera versión de este fichero cayó otra vez.
        //
        // Subir y bajar: el primero no puede subir y el último no puede bajar. El botón se
        // atenúa en vez de desaparecer, para que la fila no cambie de forma según dónde esté.
        mandos.Children.Add(BotonDeMover(0xE70E, "Subir", i > 0,                 // ChevronUp
                                         () => Mover(lado, ss, i, -1)));
        mandos.Children.Add(BotonDeMover(0xE70D, "Bajar", i < ss.Count - 1,      // ChevronDown
                                         () => Mover(lado, ss, i, +1)));

        // Y el que de verdad pedía el usuario: llevarla al otro panel.
        bool aLaDerecha = lado == CatLat.Lado.Izquierda;
        mandos.Children.Add(BotonDeMover(aLaDerecha ? 0xE72A : 0xE72B,           // Forward / Back
                                         aLaDerecha ? "Llevar al panel derecho"
                                                    : "Llevar al panel izquierdo",
                                         true, () => Cambiar(lado, s)));

        Grid.SetColumn(mandos, 2);
        g.Children.Add(mandos);
        return g;
    }

    Button BotonDeMover(int glifo, string tip, bool puede, Action accion)
    {
        var b = new Button
        {
            Content = new TextBlock
            {
                Text = char.ConvertFromUtf32(glifo),
                FontFamily = new FontFamily(FuenteDeIconos),
                FontSize = 12, Foreground = _texto,
            },
            ToolTip = tip,
            IsEnabled = puede,
            Opacity = puede ? 1 : 0.3,
            Width = 30, Height = 28,
            Margin = new Thickness(3, 0, 0, 0),
            Background = _superficieAlta,
            BorderThickness = new Thickness(0),
        };
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => accion();
        return b;
    }

    void Mover(CatLat.Lado lado, IReadOnlyList<CatLat.Seccion> ss, int i, int cuanto)
    {
        int j = i + cuanto;
        if (j < 0 || j >= ss.Count) return;

        var ids = CatLat.IdsDe(ss);
        (ids[i], ids[j]) = (ids[j], ids[i]);

        // El otro lado se guarda tal y como está AHORA —resuelto, no la lista cruda—, porque
        // puede estar vacío por no haberse tocado nunca. Guardar un vacío ahí lo convertiría
        // en «el de serie» y desharía lo que ya se hubiera movido.
        var otras = CatLat.IdsDe(LoQueLleva(Otro(lado)));
        if (lado == CatLat.Lado.Izquierda) GuardarReparto(ids, otras);
        else GuardarReparto(otras, ids);
    }

    void Cambiar(CatLat.Lado lado, CatLat.Seccion s)
    {
        var mias = CatLat.IdsDe(LoQueLleva(lado)).Where(id => id != s.Id).ToList();
        var otras = CatLat.IdsDe(LoQueLleva(Otro(lado)));
        otras.Add(s.Id);     // al final del panel de destino: subir y bajar hacen el resto

        if (lado == CatLat.Lado.Izquierda) GuardarReparto(mias, otras);
        else GuardarReparto(otras, mias);
    }

    static CatLat.Lado Otro(CatLat.Lado l) =>
        l == CatLat.Lado.Izquierda ? CatLat.Lado.Derecha : CatLat.Lado.Izquierda;
}
