using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// El modo edición de la isla.
//
// Mientras está encendido la isla se queda abierta, cada widget se ve como un bloque
// con su contorno y se puede mover, ocultar o quitar. Al salir, la isla vuelve a ser
// exactamente la de siempre: todo el cromo que añade este fichero existe SOLO mientras
// _editando está puesto, y la forma de garantizarlo es que se monta al rellenar las
// zonas y desaparece al volver a rellenarlas.
//
// Tres cosas que compiten con él y que hay que apartar mientras dura:
//
//   1. La isla se recoge al salir el ratón. Editando no puede: si se recogiera a mitad
//      de un arrastre no habría dónde soltar. Expandir() consulta EdicionAnclada.
//   2. La isla se arrastra para apartarla (Barra.Arrastrable). Si siguiera activo,
//      coger un widget movería la barra entera. Cede mientras se edita.
//   3. Los widgets tienen su propia interacción, y el carrusel hasta su propio
//      arrastre de iconos. Editando quedan inertes: el bloque es lo que se coge.
public partial class MainWindow : Window
{
    bool _editando;

    // Lo consulta Expandir() para no recogerse, y el arrastre de la isla para ceder.
    public bool EdicionAnclada => _editando;

    void AlternarEdicion() => Editar(!_editando);

    void Editar(bool encender)
    {
        if (_editando == encender) return;
        _editando = encender;

        try
        {
            if (encender)
            {
                CerrarPanel();                 // nada de paneles abiertos por debajo
                Expandir(true, forzar: true);  // y la isla abierta de par en par
                ActivarTeclado(true);          // para que Esc salga
                PreviewKeyDown += EscaparDeEdicion;
            }
            else
            {
                PreviewKeyDown -= EscaparDeEdicion;
                ActivarTeclado(false);
            }

            // Volver a montar las zonas es lo que pone y quita el cromo. No se llama a
            // Aplicar(): eso rehace también la appbar, la barra de Windows y el
            // atajo global, y entrar en modo edición no tiene por qué tocar nada de eso.
            //
            // Pero sí el remonte COMPLETO, no LlenarZonas a secas: ahí estaba el fallo
            // de las fijadas desapareciendo (ver RemontarZonas).
            RemontarZonas();
            AjustarAnchoSiHaceFalta();

            if (!encender) Expandir(false, forzar: true);
            Log.Info(encender ? "modo edición encendido" : "modo edición apagado");
        }
        catch (Exception ex) { Log.Error("cambiar de modo edición", ex); }
    }

    // Escape sale del modo. Es PreviewKeyDown y no KeyDown para llegar antes que
    // cualquier control que tuviera el foco dentro de la isla.
    void EscaparDeEdicion(object remitente, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        e.Handled = true;
        Editar(false);
    }

    // El ancho de la isla cambia al añadir los contornos. En modo barra lo decide WPF.
    void AjustarAnchoSiHaceFalta()
    {
        if (!EsIsla) return;
        Isla.BeginAnimation(WidthProperty, null);
        Isla.Width = MedirAncho();
    }

    // ---------- El cromo de cada widget ----------
    //
    // Un contorno fino alrededor del widget y, encima, una capa transparente que se
    // come los clics. Esa capa es la que deja el widget inerte sin tener que ir
    // apagando a mano los manejadores de cada uno de los dieciocho.
    UIElement ConChromeDeEdicion(UIElement widget, string id, string zona, int indice, bool oculto)
    {
        var capa = new Grid();
        capa.Children.Add(widget);

        // La tapa: transparente pero NO nula, para que reciba los clics. Es lo que
        // deja el widget inerte sin tener que apagar a mano los manejadores de los
        // dieciocho, y lo que impide que un clic lo atraviese y active el de debajo.
        capa.Children.Add(new Border { Background = Brushes.Transparent, ToolTip = id });

        // Velo + acciones, apagados hasta que el ratón entra en ESTE bloque. Con la
        // fila de 38 px no caben debajo del widget, así que van encima.
        // El velo NO es hit-testable: un Border con Opacity 0 sigue recibiendo clics en
        // WPF, y este cubre el bloque entero. Si los recibiera, el único sitio por
        // donde agarrar el widget sería el milímetro del borde.
        var velo = new Border
        {
            Background = _superficieAlta,
            CornerRadius = new CornerRadius(5),
            Opacity = 0,
            IsHitTestVisible = false
        };

        // Las acciones van pegadas al borde DERECHO, no centradas: centradas caían
        // justo donde uno agarra el bloque para arrastrarlo, y se comían el gesto.
        // Así el resto del bloque queda libre como asa.
        var acciones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            IsHitTestVisible = false        // se enciende con el ratón encima (ver abajo)
        };
        acciones.Children.Add(BotonEdicion(
            oculto ? "" : "",
            oculto ? $"Mostrar «{id}»" : $"Ocultar «{id}» (conserva su sitio)",
            () => EditarNube(z => z.Alternar(id))));
        acciones.Children.Add(BotonEdicion(
            "", $"Quitar «{id}» de la nube",
            () => EditarNube(z => z.Quitar(zona, indice))));

        capa.Children.Add(velo);
        capa.Children.Add(acciones);

        var marco = new Border
        {
            BorderBrush = oculto ? _textoAtenuado : _borde,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(2, 0, 2, 0),
            Child = capa
        };
        // Con el ratón encima se encienden Y pasan a recibir clics. Apagadas no los
        // reciben, así que nunca estorban al arrastre.
        marco.MouseEnter += (_, _) =>
        {
            velo.Opacity = 0.80; acciones.Opacity = 1; acciones.IsHitTestVisible = true;
        };
        marco.MouseLeave += (_, _) =>
        {
            velo.Opacity = 0; acciones.Opacity = 0; acciones.IsHitTestVisible = false;
        };
        marco.Cursor = System.Windows.Input.Cursors.SizeAll;
        ConArrastreDeEdicion(marco, zona, indice);
        return marco;
    }

    Button BotonEdicion(string glifo, string tip, Action accion)
    {
        var b = new Button
        {
            Content = new TextBlock
            {
                Text = glifo,
                FontFamily = new FontFamily(FuenteIconos),
                FontSize = 13,
                Foreground = _texto
            },
            ToolTip = tip,
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(1, 0, 1, 0),
            Height = Math.Max(18, _cfg.Barra.Alto - 16)
        };
        b.Click += (_, e) => { e.Handled = true; accion(); };
        return b;
    }

    // Toda escritura del modo edición pasa por aquí: se abre el árbol, se aplica la
    // operación y se guarda. Guardar dispara la recarga en caliente, que vuelve a
    // montar las zonas — y como _editando sigue puesto, vuelven con su cromo.
    //
    // Se usa ConfigArbol y no el Config ya cargado para no perder las claves que esta
    // versión no conoce, igual que hace la ventana de ajustes.
    void EditarNube(Action<ZonasWidgets> operacion)
        => EditarNube(z => { operacion(z); return true; });

    // La sobrecarga que decide: solo se guarda si la operación dice que ha cambiado
    // algo. Soltar un widget donde ya estaba devuelve false, y entonces no se
    // reescribe el fichero ni salta la recarga en caliente — que era justo lo que
    // hacía que un arrastre sin efecto pareciera un parpadeo.
    void EditarNube(Func<ZonasWidgets, bool> operacion)
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            if (operacion(new ZonasWidgets(arbol))) arbol.Guardar();
            else Log.Info("la operación no cambia nada: no se guarda");
        }
        catch (Exception ex) { Log.Error("editar la nube", ex); }
    }

    // La recarga en caliente pasa por AplicarTema, que deja la isla recogida. Si
    // estamos editando hay que volver a abrirla, o cada acción cerraría la isla en
    // las narices de quien está editando.
    void RestaurarEdicion()
    {
        if (!_editando) return;
        Expandir(true, forzar: true);
        AjustarAnchoSiHaceFalta();
    }

    // ================= Añadir widgets desde la isla =================
    //
    // En B3 quedó escrito que la biblioteca se abriría «desde Ajustes o desde el modo
    // edición». Esto es la segunda mitad.
    //
    // Se abre como PANEL DE LA ISLA, no como la ventana de ajustes: así no pierdes el
    // contexto —sigues editando, la isla sigue anclada— y no hay otra ventana que
    // robe el foco. Es exactamente para esto que la Biblioteca se escribió sin saber
    // quién la aloja: recibe el árbol, los roles y un aviso de cambio, y le da igual
    // estar en una pestaña o en el cajón.
    //
    // Hay un "+" por zona, y lo que añadas desde él cae EN ESA ZONA, no en la
    // preferida del widget. Teniendo el hueco delante, es lo que uno espera.
    const string PanelBiblioteca = "biblioteca";

    UIElement BotonAnadirWidgets(string zona)
    {
        var b = new Button
        {
            Content = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily(FuenteIconos),
                FontSize = 14,
                Foreground = _acentoLegible
            },
            ToolTip = $"Añadir widgets a {zona}",
            Padding = new Thickness(7, 0, 7, 0),
            Height = Math.Max(20, _cfg.Barra.Alto - 12)
        };
        b.Click += (_, e) => { e.Handled = true; AbrirBiblioteca(zona); };

        return new Border
        {
            BorderBrush = _borde,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(1, 0, 1, 0),
            Child = b
        };
    }

    void AbrirBiblioteca(string zona)
    {
        try
        {
            if (_panelAbierto == PanelBiblioteca) { CerrarPanel(); return; }

            var arbol = ConfigArbol.Cargar();
            var biblioteca = new Biblioteca(arbol, _roles, () => arbol.Guardar(), null, zona);
            var contenido = biblioteca.Construir() as FrameworkElement;
            if (contenido == null) return;

            contenido.Margin = new Thickness(10, 6, 10, 10);

            // teclado: false a propósito. El modo edición YA ha llamado a
            // ActivarTeclado(true); si el panel lo pidiera por su cuenta, la recarga
            // del config lo apagaría al cerrarse (ver CerrarPanelAlRecargar) y Esc
            // dejaría de sacarte del modo edición.
            AbrirPanel(PanelBiblioteca, contenido, 460, 380);
            Log.Info($"edición: biblioteca abierta para la zona {zona}");
        }
        catch (Exception ex) { Log.Error("abrir la biblioteca desde la isla", ex); }
    }

    // ================= Arrastrar un widget =================
    //
    // La primera versión se sentía mal, y no por pulido: tenía cuatro cosas mal de
    // raíz. Quedan escritas porque las cuatro se repetirán el día que alguien haga
    // otro arrastre en este proyecto.
    //
    //  1. **La zona de destino era el contenido, no la columna.** El Grid de la isla
    //     es "* | Auto | *", pero Izq se alinea a la izquierda y Der a la derecha, así
    //     que cada StackPanel ocupa SOLO lo que miden sus widgets. Todo el hueco entre
    //     ellos no pertenecía a ninguna zona: soltar ahí —que es la mitad de la
    //     isla— cancelaba el arrastre. Ahora el destino se decide por COLUMNA del
    //     Grid, que sí llena la isla de lado a lado.
    //
    //  2. **La marca de destino era un hijo del StackPanel.** Insertarla empujaba a
    //     los demás widgets, que se movían mientras arrastrabas, lo que a su vez movía
    //     la marca: temblaba y "saltaba". Ahora se dibuja en un Canvas por encima, que
    //     no participa en la medida. Nada se reordena hasta que sueltas.
    //
    //  3. **El índice se calculaba con la marca anterior todavía puesta**, así que los
    //     puntos medios estaban desplazados y el índice bailaba entre dos valores.
    //
    //  4. **Soltar sin mover guardaba igual.** Mover() devolvía false, pero se
    //     guardaba de todos modos: el fichero se reescribía, saltaba la recarga en
    //     caliente y la isla se remontaba entera para nada.

    string? _zonaOrigen;
    int _indiceOrigen = -1;
    Border? _bloqueLlevado;
    Point _puntoPresion;
    bool _arrastrandoWidget;
    string? _zonaResaltada;

    // La capa donde se pinta la marca, por encima de todo y sin recibir el ratón.
    Canvas? _capaMarca;
    Border? _marcaDestino;

    // NADA de captura de ratón ni de eventos de soltar.
    //
    // El log lo dejó claro: el "down" y los "move" llegaban al bloque, pero el "up"
    // no llegaba nunca. Y la explicación estaba en el propio proyecto: el arrastre de
    // la isla (MainWindow.Arrastre.cs) tampoco usa captura — lee el botón con
    // Nativo.BotonIzquierdoPulsado() desde un bucle de CompositionTarget.Rendering.
    // En una ventana WS_EX_NOACTIVATE, que nunca toma el foco, la captura de WPF y el
    // enrutado del "up" no son de fiar. Aquí se hace lo mismo que ya funcionaba.
    EventHandler? _siguiendoWidget;

    void ConArrastreDeEdicion(Border marco, string zona, int indice)
    {
        marco.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (DentroDeUnBoton(e.OriginalSource as DependencyObject)) return;
            if (_siguiendoWidget != null) return;              // ya hay uno en marcha

            _zonaOrigen = zona;
            _indiceOrigen = indice;
            _bloqueLlevado = marco;
            _puntoPresion = PuntoEnContenido();
            _arrastrandoWidget = false;

            _siguiendoWidget = (_, _) => SeguirWidget();
            CompositionTarget.Rendering += _siguiendoWidget;
            Log.Info($"edición: cogido \"{zona}[{indice}]\"");
            // Sin e.Handled: los botones de acción tienen que seguir pulsándose.
        };
    }

    // Un fotograma del arrastre: dónde está el ratón y si sigue pulsado.
    void SeguirWidget()
    {
        if (_bloqueLlevado is not Border marco) { DejarDeSeguirWidget(); return; }

        if (!Nativo.BotonIzquierdoPulsado())
        {
            // Soltado: aquí se decide, y aquí se escribe (una sola vez)
            var destino = _arrastrandoWidget ? Destino(PuntoEnContenido()) : null;
            marco.Opacity = 1;

            if (_arrastrandoWidget && _zonaOrigen is { } origen)
            {
                if (destino == null) Log.Info("edición: soltado fuera de las zonas, el config no se toca");
                else
                {
                    var (zonaDestino, indiceDestino) = destino.Value;
                    int i = _indiceOrigen;
                    // Guardar SOLO si Mover cambia algo: soltar donde ya estaba no
                    // reescribe el fichero ni dispara la recarga en caliente.
                    EditarNube(z => z.Mover(origen, i, zonaDestino, indiceDestino));
                }
            }
            DejarDeSeguirWidget();
            return;
        }

        var ahora = PuntoEnContenido();
        if (!_arrastrandoWidget)
        {
            if (Math.Abs(ahora.X - _puntoPresion.X) < 6) return;   // umbral: no es un arrastre
            _arrastrandoWidget = true;
            marco.Opacity = 0.45;
            Log.Info("edición: arrastre iniciado");
        }
        SeguirConLaMarca(ahora);
    }

    void DejarDeSeguirWidget()
    {
        if (_siguiendoWidget != null)
        {
            CompositionTarget.Rendering -= _siguiendoWidget;
            _siguiendoWidget = null;
        }
        LimpiarArrastre();
    }

    // El ratón, en coordenadas de Contenido. Se pregunta a Windows en vez de usar el
    // evento: durante el bucle no hay evento del que sacarlo.
    Point PuntoEnContenido()
    {
        try
        {
            var p = System.Windows.Forms.Control.MousePosition;
            return Contenido.PointFromScreen(new Point(p.X, p.Y));
        }
        catch { return new Point(double.NaN, double.NaN); }
    }

    void LimpiarArrastre()
    {
        QuitarMarca();
        ResaltarZona(null);
        _bloqueLlevado = null; _zonaOrigen = null; _indiceOrigen = -1; _arrastrandoWidget = false;
    }

    static bool DentroDeUnBoton(DependencyObject? o)
    {
        while (o != null)
        {
            if (o is System.Windows.Controls.Primitives.ButtonBase) return true;
            o = VisualTreeHelper.GetParent(o);
        }
        return false;
    }

    // ---------- Qué zona hay bajo el puntero ----------
    //
    // Por COLUMNA del Grid, no por el contenido de cada zona: así la superficie de
    // destino llena la isla y una zona vacía acepta igual que una llena.
    // Con la barra VERTICAL las zonas son filas y se mide en Y. Es la misma cuenta con
    // los ejes cambiados de sitio, así que el arrastre entre zonas, la marca de destino
    // y el resaltado valen igual en las dos orientaciones sin duplicar nada.
    static readonly string[] NombresDeZona = { "Izquierda", "Centro", "Derecha" };

    // "A lo largo" es el eje en el que se apilan los widgets; "a lo ancho", el otro.
    double ALoLargo(Point p) => Vertical ? p.Y : p.X;
    double ALoAncho(Point p) => Vertical ? p.X : p.Y;
    double LargoContenido => Vertical ? Contenido.ActualHeight : Contenido.ActualWidth;
    double AnchoContenido => Vertical ? Contenido.ActualWidth : Contenido.ActualHeight;
    // Lo que mide un bloque en el eje en el que se apila
    double LargoDel(FrameworkElement e) => Vertical ? e.ActualHeight : e.ActualWidth;

    string? ZonaEn(Point enContenido)
    {
        if (ALoAncho(enContenido) < -16 || ALoAncho(enContenido) > AnchoContenido + 16) return null;
        double a = ALoLargo(enContenido);
        if (a < 0 || a > LargoContenido) return null;

        double acumulado = 0;
        int tramos = Vertical ? Contenido.RowDefinitions.Count : Contenido.ColumnDefinitions.Count;
        for (int c = 0; c < tramos && c < NombresDeZona.Length; c++)
        {
            double largo = Vertical ? Contenido.RowDefinitions[c].ActualHeight
                                    : Contenido.ColumnDefinitions[c].ActualWidth;
            if (a >= acumulado && a < acumulado + largo) return NombresDeZona[c];
            acumulado += largo;
        }
        return NombresDeZona[^1];     // el filo del final cuenta como la última zona
    }

    // ---------- Dónde caería dentro de esa zona ----------
    //
    // Con las posiciones REALES de los widgets: el punto medio de cada bloque,
    // medido en coordenadas de Contenido, saltándose el que llevas en la mano.
    (string Zona, int Indice)? Destino(Point enContenido)
    {
        if (double.IsNaN(enContenido.X) || double.IsNaN(enContenido.Y)) return null;
        var zona = ZonaEn(enContenido);
        if (zona == null) return null;

        var panel = ZonasDibujables().FirstOrDefault(z => string.Equals(z.Nombre, zona, StringComparison.OrdinalIgnoreCase)).Destino;
        if (panel == null) return (zona, 0);      // zona vacía o sin dibujar: al principio

        int i = 0;
        foreach (var hijo in panel.Children.OfType<FrameworkElement>())
        {
            if (ReferenceEquals(hijo, _bloqueLlevado)) continue;   // el que llevas no cuenta
            double largo = LargoDel(hijo);
            if (largo <= 0) continue;
            var centro = Vertical ? new Point(0, largo / 2) : new Point(largo / 2, 0);
            double medio = ALoLargo(hijo.TranslatePoint(centro, Contenido));
            if (ALoLargo(enContenido) < medio) break;
            i++;
        }
        return (zona, i);
    }

    // ---------- La marca, por encima y sin empujar a nadie ----------
    void SeguirConLaMarca(Point enContenido)
    {
        var d = Destino(enContenido);
        ResaltarZona(d?.Zona);
        if (d == null) { QuitarMarca(); return; }

        double a = XdeLaMarca(d.Value.Zona, d.Value.Indice);
        if (double.IsNaN(a)) { QuitarMarca(); return; }

        if (_capaMarca == null) return;
        bool v = Vertical;
        if (_marcaDestino == null)
        {
            // En vertical la marca va tumbada: separa lo que se apila, igual que el
            // separador. Y se centra en el otro eje.
            double largo = Math.Max(12, _cfg.Barra.Alto - 12);
            _marcaDestino = new Border
            {
                Width = v ? largo : 2,
                Height = v ? 2 : largo,
                Background = _acento,
                CornerRadius = new CornerRadius(1)
            };
            _capaMarca.Children.Add(_marcaDestino);
        }
        if (v)
        {
            Canvas.SetLeft(_marcaDestino, Math.Max(0, (Contenido.ActualWidth - _marcaDestino.Width) / 2));
            Canvas.SetTop(_marcaDestino, a - 1);
        }
        else
        {
            Canvas.SetTop(_marcaDestino, Math.Max(0, (Contenido.ActualHeight - _marcaDestino.Height) / 2));
            Canvas.SetLeft(_marcaDestino, a - 1);
        }
    }

    // Dónde debe caer la marca EN EL EJE EN QUE SE APILAN los widgets: el filo de
    // entrada del bloque que ocuparía esa posición, o el de salida del último si va al
    // final. NaN si no se puede saber.
    double XdeLaMarca(string zona, int indice)
    {
        var panel = ZonasDibujables().FirstOrDefault(z => string.Equals(z.Nombre, zona, StringComparison.OrdinalIgnoreCase)).Destino;
        if (panel == null) return double.NaN;

        var bloques = panel.Children.OfType<FrameworkElement>()
                           .Where(h => !ReferenceEquals(h, _bloqueLlevado) && LargoDel(h) > 0)
                           .ToList();

        // Zona vacía: la marca va al principio de su columna (o de su fila)
        if (bloques.Count == 0)
        {
            try { return ALoLargo(panel.TranslatePoint(new Point(0, 0), Contenido)); }
            catch { return double.NaN; }
        }

        try
        {
            if (indice <= 0) return ALoLargo(bloques[0].TranslatePoint(new Point(0, 0), Contenido));
            if (indice >= bloques.Count)
            {
                var ultimo = bloques[^1];
                double largo = LargoDel(ultimo);
                var filo = Vertical ? new Point(0, largo) : new Point(largo, 0);
                return ALoLargo(ultimo.TranslatePoint(filo, Contenido));
            }
            return ALoLargo(bloques[indice].TranslatePoint(new Point(0, 0), Contenido));
        }
        catch { return double.NaN; }
    }

    void QuitarMarca()
    {
        if (_marcaDestino == null) return;
        _capaMarca?.Children.Remove(_marcaDestino);
        _marcaDestino = null;
    }

    // ---------- Las superficies de las zonas ----------
    //
    // Antes el tinte se ponía en el propio StackPanel, y ahí está el error: Izq se
    // alinea a la izquierda y Der a la derecha, así que el panel mide lo que miden
    // sus widgets, no lo que mide su columna. El tinte solo cubría los widgets y el
    // hueco de en medio no era de nadie — ni para verlo ni para soltar en él.
    //
    // Ahora cada zona tiene un fondo que ocupa SU COLUMNA entera, detrás de los
    // widgets, y encima de todo un lienzo donde se pinta la marca de destino sin
    // empujar a nadie. Los dos existen solo mientras se edita.
    readonly System.Collections.Generic.Dictionary<string, Border> _fondosZona = new();

    void MarcarZonas(bool editando)
    {
        // Quitar lo de la vez anterior: montar es siempre desde cero
        foreach (var f in _fondosZona.Values) Contenido.Children.Remove(f);
        _fondosZona.Clear();
        if (_capaMarca != null) { Contenido.Children.Remove(_capaMarca); _capaMarca = null; }
        _marcaDestino = null;

        if (!editando) return;

        // Con la barra vertical las zonas son filas: el fondo ocupa SU FILA entera.
        bool v = Vertical;
        var nombres = NombresDeZona;
        int tramos = v ? Contenido.RowDefinitions.Count : Contenido.ColumnDefinitions.Count;
        for (int c = 0; c < nombres.Length && c < tramos; c++)
        {
            var fondo = new Border
            {
                Background = _roles.Pincel("editor.zona.fondo"),
                BorderBrush = _roles.Pincel("editor.zona.filo"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(1, 1, 1, 1),
                IsHitTestVisible = false,          // decide ZonaEn(), no el hit-test
                ToolTip = nombres[c]
            };
            if (v) Grid.SetRow(fondo, c); else Grid.SetColumn(fondo, c);
            Contenido.Children.Insert(0, fondo);   // al fondo, detrás de los widgets
            _fondosZona[nombres[c]] = fondo;
        }

        _capaMarca = new Canvas { IsHitTestVisible = false };
        if (v)
        {
            Grid.SetRow(_capaMarca, 0);
            Grid.SetRowSpan(_capaMarca, Math.Max(1, tramos));
        }
        else
        {
            Grid.SetColumn(_capaMarca, 0);
            Grid.SetColumnSpan(_capaMarca, Math.Max(1, tramos));
        }
        Contenido.Children.Add(_capaMarca);        // el último: por encima de todo
    }

    // La zona bajo el puntero se enciende. Sin animación: seguir al ratón con un
    // fundido de 200 ms es justo lo que hace que un arrastre se sienta pastoso.
    void ResaltarZona(string? zona)
    {
        if (_zonaResaltada == zona) return;
        _zonaResaltada = zona;
        foreach (var (nombre, fondo) in _fondosZona)
        {
            bool activa = zona != null && string.Equals(nombre, zona, StringComparison.OrdinalIgnoreCase);
            fondo.Background = _roles.Pincel(activa ? "editor.zona.fondo.activa" : "editor.zona.fondo");
            fondo.BorderBrush = _roles.Pincel(activa ? "editor.zona.filo.activa" : "editor.zona.filo");
        }
    }
}
