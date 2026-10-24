using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// La biblioteca de widgets: qué hay, qué tienes puesto y qué puedes hacer con ello.
//
// Vive aparte de VentanaAjustes a propósito. Hoy la abre la ventana de ajustes, pero
// el modo edición de la isla va a querer exactamente lo mismo, y entonces el
// anfitrión es otro. Por eso no sabe nada de dónde se la dibuja: recibe el árbol del
// config, los colores por rol y un aviso para cuando algo cambia.
//
// Tres estados por widget, y conviene no confundirlos:
//
//   no puesto  → no está en ninguna zona. Se puede AÑADIR.
//   puesto     → está en una zona y se dibuja. Se puede OCULTAR o QUITAR.
//   oculto     → está en una zona, con su sitio, pero no se dibuja. Se puede MOSTRAR
//                o QUITAR.
//
// Ocultar conserva el sitio; quitar lo pierde y devuelve el widget a la biblioteca.
public sealed class Biblioteca
{
    readonly ConfigArbol _arbol;
    readonly Roles _roles;
    readonly Action _alCambiar;          // guardar y repintar
    readonly RegistroAjustes? _ajustes;  // para el "¿buscabas un ajuste?" del estado vacío

    string _busqueda = "";
    CategoriaWidget? _categoria;         // null = todas

    readonly StackPanel _lista = new();
    readonly Panel _raiz = new StackPanel();

    public Biblioteca(ConfigArbol arbol, Roles roles, Action alCambiar, RegistroAjustes? ajustes = null)
    {
        _arbol = arbol;
        _roles = roles;
        _alCambiar = alCambiar;
        _ajustes = ajustes;
        RegistroWidgets.Asegurar();
    }

    // ================= Pinceles =================
    SolidColorBrush P(Color c) => new(c);
    SolidColorBrush Texto => P(_roles.Texto);
    SolidColorBrush Atenuado => P(_roles.TextoAtenuado);
    SolidColorBrush Acento => P(_roles.AcentoTinta);
    SolidColorBrush Superficie => P(_roles.Superficie);
    SolidColorBrush Alta => P(_roles.SuperficieAlta);
    SolidColorBrush Borde => P(_roles.Borde);

    // ================= Lo que hay puesto =================
    static readonly string[] Zonas = { "Izquierda", "Centro", "Derecha" };

    List<string> EnZona(string zona) => _arbol.Lista("Modulos", zona);
    List<string> Ocultos() => _arbol.Lista("Modulos", "Ocultos");

    bool EstaOculto(string id) => Ocultos().Any(o => o.Equals(id, StringComparison.OrdinalIgnoreCase));

    // En qué zona está, o null. Si está repetido devuelve la primera.
    string? DondeEsta(string id)
    {
        foreach (var z in Zonas)
            if (EnZona(z).Any(m => m.Equals(id, StringComparison.OrdinalIgnoreCase))) return z;
        return null;
    }

    int CuantosHay(string id) =>
        Zonas.Sum(z => EnZona(z).Count(m => m.Equals(id, StringComparison.OrdinalIgnoreCase)));

    // ================= Acciones =================

    void Anadir(IWidget w)
    {
        var d = w.Descriptor;

        // La regla del contrato: si no es repetible y ya está, no se añade otro. El
        // botón ya sale apagado, pero se comprueba igual aquí porque esto también lo
        // llamará el modo edición y no quiero que la regla viva en la interfaz.
        if (!d.Repetible && CuantosHay(d.Id) > 0)
        {
            Log.Info($"el widget \"{d.Id}\" no admite más de uno: no se añade otro");
            return;
        }

        var zona = Zonas.Contains(d.ZonaPreferida) ? d.ZonaPreferida : "Derecha";
        var lista = EnZona(zona);
        lista.Add(d.Id);
        _arbol.PonerLista("Modulos", zona, lista);

        // Si se vuelve a añadir algo que estaba oculto, lo lógico es verlo.
        Mostrar(d.Id, guardar: false);

        Log.Info($"widget \"{d.Id}\" añadido a {zona}");
        Cambiado();
    }

    void Quitar(string id)
    {
        foreach (var z in Zonas)
        {
            var lista = EnZona(z);
            // La ÚLTIMA, no la primera: con un widget repetible (el separador), quitar
            // debe deshacer el último que pusiste, que es lo que uno espera.
            int i = lista.FindLastIndex(m => m.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (i < 0) continue;
            lista.RemoveAt(i);
            _arbol.PonerLista("Modulos", z, lista);
            Log.Info($"widget \"{id}\" quitado de {z}");
            break;
        }
        // Si era el último, ya no tiene sentido que siga en la lista de ocultos
        if (CuantosHay(id) == 0) Mostrar(id, guardar: false);
        Cambiado();
    }

    void Ocultar(string id)
    {
        var o = Ocultos();
        if (!o.Any(x => x.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            o.Add(id);
            _arbol.PonerLista("Modulos", "Ocultos", o);
            Log.Info($"widget \"{id}\" oculto (conserva su sitio)");
        }
        Cambiado();
    }

    void Mostrar(string id, bool guardar = true)
    {
        var o = Ocultos();
        int antes = o.Count;
        o.RemoveAll(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (o.Count != antes)
        {
            _arbol.PonerLista("Modulos", "Ocultos", o);
            Log.Info($"widget \"{id}\" vuelve a verse");
        }
        if (guardar) Cambiado();
    }

    void Cambiado()
    {
        _alCambiar();
        Refrescar();
    }

    // ================= Interfaz =================

    public UIElement Construir()
    {
        _raiz.Children.Clear();
        _raiz.Margin = new Thickness(4);

        _raiz.Children.Add(Buscador());
        _raiz.Children.Add(Categorias());
        _raiz.Children.Add(new ScrollViewer
        {
            Content = _lista,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 320,
            Margin = new Thickness(0, 10, 0, 0)
        });

        Refrescar();
        return _raiz;
    }

    UIElement Buscador()
    {
        var caja = new TextBox
        {
            Background = Superficie,
            Foreground = Texto,
            CaretBrush = Texto,
            BorderBrush = Borde,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 10)
        };
        // Una pista dentro de la caja, sin plantillas: un texto detrás que se esconde
        // en cuanto escribes.
        var pista = new TextBlock
        {
            Text = "Buscar un widget…",
            Foreground = Atenuado,
            Margin = new Thickness(10, 6, 0, 0),
            IsHitTestVisible = false
        };
        caja.TextChanged += (_, _) =>
        {
            _busqueda = caja.Text ?? "";
            pista.Visibility = _busqueda.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Refrescar();
        };
        return new Grid { Children = { caja, pista }, Margin = new Thickness(0, 0, 0, 2) };
    }

    UIElement Categorias()
    {
        var fila = new WrapPanel();

        void Chip(string texto, CategoriaWidget? cat)
        {
            bool elegida = _categoria.HasValue == cat.HasValue && _categoria == cat;
            var b = new Border
            {
                Background = elegida ? P(_roles.Seleccion) : Superficie,
                BorderBrush = elegida ? P(_roles.Acento) : Borde,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = texto, Foreground = elegida ? Acento : Texto, FontSize = 12 }
            };
            b.MouseLeftButtonUp += (_, _) => { _categoria = cat; Construir(); };
            fila.Children.Add(b);
        }

        Chip("Todas", null);
        // Solo las categorías que de verdad tienen algo: PorCategoria agrupa el
        // registro, así que una categoría vacía no sale.
        foreach (var g in RegistroWidgets.PorCategoria())
            Chip($"{Nombre(g.Key)} ({g.Count()})", g.Key);

        return fila;
    }

    static string Nombre(CategoriaWidget c) => c switch
    {
        CategoriaWidget.Reloj => "Reloj",
        CategoriaWidget.Apps => "Apps",
        CategoriaWidget.Sistema => "Sistema",
        CategoriaWidget.Medios => "Medios",
        CategoriaWidget.Aspecto => "Aspecto",
        _ => "Útiles"
    };

    // ================= La lista =================

    IEnumerable<IWidget> Resultados()
    {
        var fuente = _categoria.HasValue
            ? RegistroWidgets.DeCategoria(_categoria.Value)
            : RegistroWidgets.Todos;

        if (string.IsNullOrWhiteSpace(_busqueda)) return fuente;

        var q = _busqueda.Trim();
        return fuente.Where(w =>
            w.Descriptor.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            w.Descriptor.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            w.Descriptor.Descripcion.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    void Refrescar()
    {
        _lista.Children.Clear();

        foreach (var w in Resultados()) _lista.Children.Add(Ficha(w));

        // Los ids que hay en el config y que el registro no conoce. Pasa de verdad:
        // un config escrito por una NubeBar más nueva deja nombres que esta no sabe
        // montar. Antes solo se apuntaban en el log y no había forma de limpiarlos.
        foreach (var id in Desconocidos()) _lista.Children.Add(FichaDesconocido(id));

        if (_lista.Children.Count == 0) _lista.Children.Add(Vacio());
    }

    IEnumerable<string> Desconocidos() =>
        Zonas.SelectMany(EnZona)
             .Where(id => !RegistroWidgets.Existe(id))
             .Distinct(StringComparer.OrdinalIgnoreCase);

    Border Ficha(IWidget w)
    {
        var d = w.Descriptor;
        var zona = DondeEsta(d.Id);
        bool puesto = zona != null;
        bool oculto = puesto && EstaOculto(d.Id);
        int cuantos = CuantosHay(d.Id);

        var marco = new Border
        {
            Background = Superficie,
            BorderBrush = Borde,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6)
        };

        var rejilla = new Grid();
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icono = new TextBlock
        {
            Text = d.Glifo,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 18,
            Foreground = oculto ? Atenuado : Acento,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 12, 0)
        };
        Grid.SetColumn(icono, 0);
        rejilla.Children.Add(icono);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titulo = new StackPanel { Orientation = Orientation.Horizontal };
        titulo.Children.Add(new TextBlock
        {
            Text = d.Nombre,
            Foreground = oculto ? Atenuado : Texto,
            FontWeight = FontWeights.SemiBold
        });
        titulo.Children.Add(Etiqueta(
            oculto ? "oculto" : puesto ? $"en {zona}" + (cuantos > 1 ? $" ×{cuantos}" : "") : "no puesto",
            oculto ? _roles.Aviso : puesto ? _roles.Positivo : _roles.TextoAtenuado));
        if (d.Repetible) titulo.Children.Add(Etiqueta("repetible", _roles.Sistema));
        textos.Children.Add(titulo);
        textos.Children.Add(new TextBlock
        {
            Text = d.Descripcion,
            Foreground = Atenuado,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 8, 0)
        });
        Grid.SetColumn(textos, 1);
        rejilla.Children.Add(textos);

        var botones = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!puesto)
        {
            botones.Children.Add(Boton("Añadir", () => Anadir(w)));
        }
        else
        {
            // Un no repetible ya puesto: el botón sale, pero apagado y diciendo por qué.
            if (d.Repetible) botones.Children.Add(Boton("Añadir otro", () => Anadir(w)));
            else botones.Children.Add(Boton("Añadir", null, "Este widget no admite más de uno"));

            botones.Children.Add(oculto
                ? Boton("Mostrar", () => Mostrar(d.Id))
                : Boton("Ocultar", () => Ocultar(d.Id)));
            botones.Children.Add(Boton("Quitar", () => Quitar(d.Id)));
        }
        Grid.SetColumn(botones, 2);
        rejilla.Children.Add(botones);

        marco.Child = rejilla;
        return marco;
    }

    Border FichaDesconocido(string id)
    {
        var marco = new Border
        {
            Background = Superficie,
            BorderBrush = P(_roles.Aviso),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textos = new StackPanel();
        textos.Children.Add(new TextBlock { Text = id, Foreground = Texto, FontWeight = FontWeights.SemiBold });
        textos.Children.Add(new TextBlock
        {
            Text = "Esta versión no conoce este widget. Lo habrá escrito una NubeBar más nueva.",
            Foreground = Atenuado, FontSize = 12, TextWrapping = TextWrapping.Wrap
        });
        fila.Children.Add(textos);

        var quitar = Boton("Quitar", () => Quitar(id));
        Grid.SetColumn(quitar, 1);
        fila.Children.Add(quitar);

        marco.Child = fila;
        return marco;
    }

    // ================= El estado vacío =================

    UIElement Vacio()
    {
        var pila = new StackPanel { Margin = new Thickness(0, 24, 0, 24), HorizontalAlignment = HorizontalAlignment.Center };
        pila.Children.Add(new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 28,
            Foreground = Atenuado,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        pila.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_busqueda)
                ? "No hay widgets en esta categoría."
                : $"Ningún widget coincide con «{_busqueda}».",
            Foreground = Atenuado,
            Margin = new Thickness(0, 8, 0, 0),
            TextAlignment = TextAlignment.Center
        });

        // Y si lo que buscaba era un ajuste y no un widget, se lo decimos. Esto es lo
        // que le da por fin un consumidor a RegistroAjustes.Filtrar.
        if (_ajustes != null && !string.IsNullOrWhiteSpace(_busqueda))
        {
            var ajustes = _ajustes.Filtrar(_busqueda).ToList();
            if (ajustes.Count > 0)
            {
                pila.Children.Add(new TextBlock
                {
                    Text = "Pero sí hay ajustes que se llaman así: " +
                           string.Join(", ", ajustes.Select(m => m.Descriptor.Nombre)),
                    Foreground = Acento,
                    FontSize = 12,
                    Margin = new Thickness(0, 10, 0, 0),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 380
                });
            }
        }
        return pila;
    }

    // ================= Piezas =================

    UIElement Etiqueta(string texto, Color color) => new Border
    {
        Background = P(Color.FromArgb(0x2A, color.R, color.G, color.B)),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(5, 1, 5, 2),
        Margin = new Thickness(8, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = texto, Foreground = P(Contraste.Legible(color, _roles.FondoOpaco)), FontSize = 10 }
    };

    // accion == null deja el botón apagado, y el motivo sale como tooltip.
    Button Boton(string texto, Action? accion, string? motivo = null)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = texto, FontSize = 12 },
            Foreground = accion == null ? Atenuado : Texto,
            Background = accion == null ? Superficie : Alta,
            BorderBrush = Borde,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 4, 10, 5),
            Margin = new Thickness(6, 0, 0, 0),
            IsEnabled = accion != null,
            Cursor = accion == null ? Cursors.No : Cursors.Hand,
            ToolTip = motivo
        };
        if (accion != null) b.Click += (_, _) => accion();
        return b;
    }
}
