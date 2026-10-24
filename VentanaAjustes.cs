using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// Ventana de ajustes. Es una ventana normal (se activa y se escribe en ella) pero
// con el aire de la nube. Cada cambio se escribe en el config y la isla lo recoge
// con su recarga en caliente: por eso la vista previa es en vivo y gratis.
public sealed class VentanaAjustes : Window
{
    readonly ConfigArbol _arbol;
    readonly DispatcherTimer _escritura = new() { Interval = TimeSpan.FromMilliseconds(120) };
    // La lista sale del registro de widgets, no escrita a mano: antes había que
    // acordarse de añadir aquí cada módulo nuevo, y "musica-caratula" y
    // "musica-barras" existían en la isla pero no se podían elegir desde aquí porque
    // se olvidaron. Ahora registrar un widget basta para que salga en los tres sitios
    // (la isla, esta lista y la biblioteca).
    readonly List<string> _modulosDisponibles = ModulosDisponibles();

    static List<string> ModulosDisponibles()
    {
        MainWindow.AsegurarWidgetsRegistrados();
        return RegistroWidgets.Ids.ToList();
    }

    SolidColorBrush _fondo = new(Color.FromRgb(0x16, 0x16, 0x1A));
    SolidColorBrush _texto = new(Colors.White);
    SolidColorBrush _acento = new(Color.FromRgb(0x7A, 0xA2, 0xF7));
    SolidColorBrush _superficie = new(Color.FromArgb(0x1E, 255, 255, 255));
    SolidColorBrush _superficieAlta = new(Color.FromArgb(0x33, 255, 255, 255));
    SolidColorBrush _textoAtenuado = new(Color.FromRgb(0xA0, 0xA0, 0xA0));
    Roles _roles = Roles.PorDefecto();

    TabControl _pestanas = new();
    readonly RegistroAjustes _modulos = new();

    // El panel cuya geometria se edita aqui. Hoy solo hay uno; cuando haya varios,
    // esta constante se convertira en el que este seleccionado.
    const string Principal = "principal";
    Action? _revisarAtajo;        // comprobar el atajo guardado, cuando ya haya handle
    bool _guardado;

    public static VentanaAjustes? Abierta { get; private set; }

    public VentanaAjustes()
    {
        _arbol = ConfigArbol.Cargar();
        LeerColoresDelTema();

        Title = "Ajustes de la nube";
        Width = 760;
        Height = 620;
        // Centrada en la pantalla donde tengas el ratón, no siempre en la principal
        WindowStartupLocation = WindowStartupLocation.Manual;
        var m = Monitores.DelRaton() ?? Monitores.Principal();
        Left = (m.TrabajoX + (m.TrabajoAncho - Width * m.Escala) / 2) / m.Escala;
        Top = (m.TrabajoY + (m.TrabajoAlto - Height * m.Escala) / 2) / m.Escala;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        FontFamily = new FontFamily(_arbol.Texto("Tema", "Fuente", "Segoe UI"));
        FontSize = 13;

        Content = Montar();

        _escritura.Tick += (_, _) => { _escritura.Stop(); _arbol.Guardar(); };

        Loaded += (_, _) =>
        {
            Abierta = this;
            var mover = new TranslateTransform { Y = 18 };
            ((FrameworkElement)Content).RenderTransform = mover;
            mover.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(340))
                { EasingFunction = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut } });
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            _revisarAtajo?.Invoke();
        };
        Closed += (_, _) =>
        {
            Abierta = null;
            // Cerrar con la X equivale a cancelar: el config vuelve a como estaba
            if (!_guardado) { _escritura.Stop(); _arbol.Revertir(); }
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Cancelar(); };
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    void LeerColoresDelTema()
    {
        // El fondo de esta ventana es OPACO a propósito, aunque el tema traiga alfa:
        // es una ventana normal, no la isla.
        var fondo  = Opaco(Hex(_arbol.Texto("Tema", "Fondo", "#E6000000"), Color.FromRgb(0x16, 0x16, 0x1A)));
        var texto  = Hex(_arbol.Texto("Tema", "Texto", "#FFFFFF"), Colors.White);
        var acento = Hex(_arbol.Texto("Tema", "Acento", "#7AA2F7"), Color.FromRgb(0x7A, 0xA2, 0xF7));

        _fondo = new SolidColorBrush(fondo);
        _texto = new SolidColorBrush(texto);
        _acento = new SolidColorBrush(acento);

        // Los mismos roles que la isla, calculados aquí porque esta ventana lee el
        // árbol del config y no el Config ya cargado (así la vista previa es en vivo).
        //
        // Antes, los fondos de esta ventana eran blancos fijos
        // —`FromArgb(0x18, 255, 255, 255)`— escritos ocho veces. Con un tema claro eso
        // es un velo blanco sobre blanco: invisible. Ahora salen del color de texto
        // del tema, así que funcionan igual en claro y en oscuro.
        _roles = Roles.Desde(TemaDelArbol(), fondo, texto, acento);
        _superficie = new SolidColorBrush(_roles.Superficie);
        _superficieAlta = new SolidColorBrush(_roles.SuperficieAlta);
        _textoAtenuado = new SolidColorBrush(_roles.TextoAtenuado);
    }

    // Un Tema con lo que haya en el árbol, para poder pedirle los roles. Las claves de
    // rol que no estén quedan vacías, y Roles las deriva.
    Tema TemaDelArbol()
    {
        string? Opcional(string clave)
        {
            var v = _arbol.Texto("Tema", clave, "");
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }
        return new Tema
        {
            Fondo = _arbol.Texto("Tema", "Fondo", "#E6000000"),
            Texto = _arbol.Texto("Tema", "Texto", "#FFFFFF"),
            Acento = _arbol.Texto("Tema", "Acento", "#7AA2F7"),
            Radio = _arbol.Numero("Tema", "Radio", 20),
            Superficie = Opcional("Superficie"),
            SuperficieAlta = Opcional("SuperficieAlta"),
            Borde = Opcional("Borde"),
            TextoAtenuado = Opcional("TextoAtenuado"),
            Secundario = Opcional("Secundario"),
            Sistema = Opcional("Sistema"),
            Positivo = Opcional("Positivo"),
            Aviso = Opcional("Aviso"),
            Negativo = Opcional("Negativo")
        };
    }

    static Color Opaco(Color c) => Color.FromRgb(c.R, c.G, c.B);

    static Color Hex(string s, Color porDefecto)
    {
        try { return (Color)ColorConverter.ConvertFromString(s); } catch { return porDefecto; }
    }

    // ================= Esqueleto =================
    UIElement Montar()
    {
        var marco = new Border
        {
            Background = _fondo,
            CornerRadius = new CornerRadius(16),
            BorderBrush = _acento,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Black }
        };

        var rejilla = new Grid { Margin = new Thickness(18) };
        rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rejilla.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titulo = new TextBlock
        {
            Text = "Ajustes de la nube",
            FontSize = 20,
            Foreground = _texto,
            Margin = new Thickness(4, 0, 0, 12)
        };
        Grid.SetRow(titulo, 0);
        rejilla.Children.Add(titulo);

        _pestanas = new TabControl
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = _texto
        };
        // Sin esto las pestañas salen con el estilo de Windows: blancas sobre blanco
        // y prácticamente ilegibles encima de un fondo oscuro
        _pestanas.ItemContainerStyle = EstiloPestana();

        // Las ocho secciones, por el registro. Siguen siendo los mismos métodos
        // Seccion*(): lo que cambia es que ahora tienen id y palabras clave, que la
        // ventana no sabe cuántas hay, y que recargar una no depende de acordarse de
        // su número (ver AplicarTema, que antes usaba Items[1] a pelo).
        _modulos.Registrar("apariencia", "Apariencia", "Aspecto", SeccionApariencia,
                           "tema", "radio", "fuente", "tamaño", "opacidad", "animaciones", "movimiento");
        _modulos.Registrar("colores", "Colores", "Aspecto", SeccionColores,
                           "acento", "fondo", "texto", "paleta", "hsv", "wallpaper", "roles", "contraste");
        _modulos.Registrar("modulos", "Módulos", "Panel", SeccionModulos,
                           "widgets", "zonas", "izquierda", "centro", "derecha", "reloj", "bandeja");
        _modulos.Registrar("biblioteca", "Biblioteca", "Panel", SeccionBiblioteca,
                           "widgets", "añadir", "ocultar", "quitar", "catálogo", "buscar");
        _modulos.Registrar("apps", "Apps", "Panel", SeccionApps,
                           "carrusel", "fijadas", "agrupar", "orden", "ventanas");
        _modulos.Registrar("atajos", "Atajos", "Sistema", SeccionAtajos,
                           "teclado", "combinación", "vista general", "teclas");
        _modulos.Registrar("inicio", "Inicio", "Sistema", SeccionInicio,
                           "menú", "buscador", "archivos", "web", "saludo", "tecla windows");
        _modulos.Registrar("wallpaper", "Wallpaper", "Aspecto", SeccionWallpaper,
                           "fondo", "wallpaper engine", "monitor", "paleta");
        _modulos.Registrar("sistema", "Sistema", "Sistema", SeccionSistema,
                           "arrancar", "windows", "notificaciones", "monitores", "diagnóstico");

        foreach (var m in _modulos.Todos)
            _pestanas.Items.Add(Pestana(m.Descriptor.Nombre, m.Construir()));
        Grid.SetRow(_pestanas, 1);
        rejilla.Children.Add(_pestanas);

        var botones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        botones.Children.Add(BotonPlano("Cancelar", Cancelar, false));
        botones.Children.Add(BotonPlano("Guardar", Guardar, true));
        Grid.SetRow(botones, 2);
        rejilla.Children.Add(botones);

        marco.Child = rejilla;
        return marco;
    }

    // Pestaña plana: texto tenue, y la elegida con el color de acento y su subrayado
    Style EstiloPestana()
    {
        var plantilla = new ControlTemplate(typeof(TabItem));

        var raiz = new FrameworkElementFactory(typeof(Border), "marco");
        raiz.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        raiz.SetValue(Border.PaddingProperty, new Thickness(12, 7, 12, 7));
        raiz.SetValue(Border.MarginProperty, new Thickness(0, 0, 2, 0));
        raiz.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        raiz.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

        var texto = new FrameworkElementFactory(typeof(ContentPresenter));
        texto.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        texto.SetValue(TextBlock.ForegroundProperty, _texto);
        raiz.AppendChild(texto);
        plantilla.VisualTree = raiz;

        var elegida = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        elegida.Setters.Add(new Setter(Border.BorderBrushProperty, _acento, "marco"));
        elegida.Setters.Add(new Setter(TextBlock.ForegroundProperty, _acento));
        plantilla.Triggers.Add(elegida);

        var encima = new Trigger { Property = TabItem.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BackgroundProperty,
                                      _superficie, "marco"));
        plantilla.Triggers.Add(encima);

        var estilo = new Style(typeof(TabItem));
        estilo.Setters.Add(new Setter(Control.TemplateProperty, plantilla));
        estilo.Setters.Add(new Setter(Control.ForegroundProperty, _texto));
        estilo.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        return estilo;
    }

    TabItem Pestana(string nombre, UIElement contenido) => new()
    {
        Header = nombre,
        Foreground = _texto,
        Content = new ScrollViewer
        {
            Content = contenido,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(8, 12, 8, 8)
        }
    };

    // ================= Piezas sueltas =================
    void Cambio() { _escritura.Stop(); _escritura.Start(); }

    StackPanel Columna() => new() { Margin = new Thickness(4) };

    TextBlock Rotulo(string t, bool titulo = false) => new()
    {
        Text = t,
        Foreground = _texto,
        Opacity = titulo ? 1 : 0.75,
        FontSize = titulo ? 15 : 13,
        Margin = new Thickness(0, titulo ? 14 : 10, 0, 4)
    };

    Button BotonPlano(string texto, Action accion, bool destacado)
    {
        var b = new Button
        {
            Content = texto,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(18, 7, 18, 7),
            Foreground = destacado ? Brushes.Black : _texto,
            Background = destacado ? _acento : _superficieAlta,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        b.Click += (_, _) => accion();
        return b;
    }

    // Deslizador con su número al lado
    UIElement Deslizador(string etiqueta, double min, double max, double valor, Action<double> alCambiar, double paso = 1)
    {
        var fila = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

        var rot = new TextBlock { Text = etiqueta, Foreground = _texto, VerticalAlignment = VerticalAlignment.Center };
        var desl = new Slider
        {
            Minimum = min, Maximum = max, Value = valor,
            TickFrequency = paso, IsSnapToTickEnabled = paso >= 1,
            IsMoveToPointEnabled = true,          // pulsar en la barra lleva el mando ahí
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _acento
        };
        var num = new TextBlock
        {
            Text = valor.ToString("0.##"),
            Foreground = _acento,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        desl.ValueChanged += (_, e) =>
        {
            num.Text = e.NewValue.ToString("0.##");
            alCambiar(e.NewValue);
            Cambio();
        };

        Grid.SetColumn(rot, 0); Grid.SetColumn(desl, 1); Grid.SetColumn(num, 2);
        fila.Children.Add(rot); fila.Children.Add(desl); fila.Children.Add(num);
        return fila;
    }

    UIElement Interruptor(string etiqueta, bool valor, Action<bool> alCambiar)
    {
        var c = new CheckBox
        {
            Content = etiqueta,
            IsChecked = valor,
            Foreground = _texto,
            Margin = new Thickness(0, 6, 0, 6)
        };
        c.Checked += (_, _) => { alCambiar(true); Cambio(); };
        c.Unchecked += (_, _) => { alCambiar(false); Cambio(); };
        return c;
    }

    UIElement Opciones(string etiqueta, string[] valores, string actual, Action<string> alCambiar)
    {
        var fila = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rot = new TextBlock { Text = etiqueta, Foreground = _texto, VerticalAlignment = VerticalAlignment.Center };
        var combo = new ComboBox { ItemsSource = valores, SelectedItem = actual, Foreground = Brushes.Black };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string s) { alCambiar(s); Cambio(); }
        };
        Grid.SetColumn(rot, 0); Grid.SetColumn(combo, 1);
        fila.Children.Add(rot); fila.Children.Add(combo);
        return fila;
    }

    // ================= Secciones =================
    UIElement SeccionApariencia()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Forma", true));
        // La geometría se escribe en Paneles[principal], que es la autoridad desde B5.
        // Las claves equivalentes de "Barra" ya no se leen: se derivan al cargar.
        c.Children.Add(Opciones("Modo", new[] { "isla", "barra" }, _arbol.TextoDelPanel(Principal, "Modo", "isla"),
                                v => _arbol.PonerDelPanel(Principal, "Modo", v)));
        c.Children.Add(Opciones("Posición", PanelNube.Posiciones, _arbol.TextoDelPanel(Principal, "Posicion", "arriba"),
                                v => _arbol.PonerDelPanel(Principal, "Posicion", v)));
        // Los cuatro bordes valen en los dos modos, pero no significan lo mismo, y eso
        // no se adivina de un desplegable con cuatro palabras.
        var pista = Rotulo("En «izquierda» y «derecha», el modo barra se vuelve una barra vertical " +
                           "con las zonas apiladas; el modo isla solo se ancla a ese lado.");
        pista.TextWrapping = TextWrapping.Wrap;
        pista.Opacity = 0.55;
        pista.Margin = new Thickness(0, 0, 0, 4);
        c.Children.Add(pista);
        c.Children.Add(Opciones("Visibilidad", PanelNube.Visibilidades,
                                _arbol.TextoDelPanel(Principal, "Visibilidad", "siempre"),
                                v => _arbol.PonerDelPanel(Principal, "Visibilidad", v)));
        c.Children.Add(Deslizador("Grosor", 24, 72, _arbol.NumeroDelPanel(Principal, "Grosor", 38),
                                  v => _arbol.PonerDelPanel(Principal, "Grosor", v)));
        c.Children.Add(Deslizador("Margen", 0, 32, _arbol.NumeroDelPanel(Principal, "Margen", 8),
                                  v => _arbol.PonerDelPanel(Principal, "Margen", v)));
        c.Children.Add(Deslizador("Radio de esquinas", 0, 36, _arbol.Numero("Tema", "Radio", 20),
                                  v => _arbol.Poner("Tema", "Radio", v)));

        c.Children.Add(Rotulo("Texto", true));
        var fuentes = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        c.Children.Add(Opciones("Fuente", fuentes, _arbol.Texto("Tema", "Fuente", "Segoe UI"),
                                v => _arbol.Poner("Tema", "Fuente", v)));
        c.Children.Add(Deslizador("Tamaño", 9, 22, _arbol.Numero("Tema", "TamanoFuente", 13),
                                  v => _arbol.Poner("Tema", "TamanoFuente", v)));

        c.Children.Add(Rotulo("Movimiento", true));
        // "auto" sigue a Windows (Accesibilidad > Efectos visuales > Efectos de
        // animación): quien ya ha pedido menos movimiento en el sistema no tiene que
        // volver a pedirlo aquí. Equilibrado no quita animaciones, las acorta.
        c.Children.Add(Opciones("Animaciones", new[] { "auto", "completo", "equilibrado", "reducido", "desactivado" },
                                _arbol.Texto("Animaciones", "Nivel", "auto"),
                                v => _arbol.Poner("Animaciones", "Nivel", v)));
        // La escena del robot todavía no existe (llega en la fase C). La opción se
        // guarda ya para que quien la cambie no la pierda al actualizar, pero aquí no
        // hay botón de vista previa: no habría nada que previsualizar.
        c.Children.Add(Opciones("Escena de entrada", new[] { "primera-vez", "cada-inicio", "al-cambiar-tema", "nunca" },
                                _arbol.Texto("Animaciones", "Robot", "primera-vez"),
                                v => _arbol.Poner("Animaciones", "Robot", v)));

        c.Children.Add(Rotulo("Reacción al sonido", true));
        c.Children.Add(Interruptor("La isla respira con lo que suena",
                                   _arbol.Bandera("Barra", "PulsoAudio", true),
                                   v => _arbol.Poner("Barra", "PulsoAudio", v)));
        c.Children.Add(Interruptor("Las teclas de volumen enseñan el aviso de la isla, no el de Windows",
                                   _arbol.Bandera("Barra", "TeclasVolumen", true),
                                   v => _arbol.Poner("Barra", "TeclasVolumen", v)));

        c.Children.Add(Rotulo("Mover la isla", true));
        c.Children.Add(Interruptor("Se puede apartar arrastrándola",
                                   _arbol.Bandera("Barra", "Arrastrable", true),
                                   v => _arbol.Poner("Barra", "Arrastrable", v)));
        c.Children.Add(Deslizador("Segundos antes de volver a su sitio", 0.5, 10,
                                  _arbol.Numero("Barra", "SegundosParaVolver", 2),
                                  v => _arbol.Poner("Barra", "SegundosParaVolver", Math.Round(v, 1))));

        c.Children.Add(Rotulo("Pantallas", true));
        c.Children.Add(Opciones("Monitores", new[] { "todos", "principal" },
                                _arbol.TextoDelPanel(Principal, "Monitores", "todos"),
                                v => _arbol.PonerDelPanel(Principal, "Monitores", v)));
        c.Children.Add(Interruptor("Cada isla enseña las apps de su pantalla",
                                   _arbol.Bandera("Barra", "AppsPorMonitor", true),
                                   v => _arbol.Poner("Barra", "AppsPorMonitor", v)));
        c.Children.Add(Interruptor("Esconderse cuando algo ocupa la pantalla entera",
                                   _arbol.Bandera("Barra", "OcultarEnPantallaCompleta", true),
                                   v => _arbol.Poner("Barra", "OcultarEnPantallaCompleta", v)));
        c.Children.Add(Interruptor("Esconderse también con una ventana maximizada (juegos, vídeos)",
                                   _arbol.Bandera("Barra", "OcultarConMaximizada", false),
                                   v => _arbol.Poner("Barra", "OcultarConMaximizada", v)));
        return c;
    }

    UIElement SeccionColores()
    {
        var c = Columna();

        c.Children.Add(Rotulo("Temas listos", true));
        var muestras = new WrapPanel();
        foreach (var t in Temas.Predefinidos) muestras.Children.Add(MuestraTema(t));
        c.Children.Add(muestras);

        c.Children.Add(Rotulo("Fondo (con opacidad)", true));
        var selFondo = new SelectorColor { ConOpacidad = true }.Montar();
        selFondo.Valor = Hex(_arbol.Texto("Tema", "Fondo", "#E6000000"), Colors.Black);
        selFondo.Cambiado += v => { _arbol.Poner("Tema", "Fondo", SelectorColor.AHex(v, true)); Cambio(); };
        c.Children.Add(selFondo);

        c.Children.Add(Rotulo("Texto", true));
        var selTexto = new SelectorColor { ConOpacidad = false }.Montar();
        selTexto.Valor = Hex(_arbol.Texto("Tema", "Texto", "#FFFFFF"), Colors.White);
        selTexto.Cambiado += v => { _arbol.Poner("Tema", "Texto", SelectorColor.AHex(v, false)); Cambio(); };
        c.Children.Add(selTexto);

        c.Children.Add(Rotulo("Acento", true));
        var selAcento = new SelectorColor { ConOpacidad = false }.Montar();
        selAcento.Valor = Hex(_arbol.Texto("Tema", "Acento", "#7AA2F7"), Colors.CornflowerBlue);
        selAcento.Cambiado += v => { _arbol.Poner("Tema", "Acento", SelectorColor.AHex(v, false)); Cambio(); };
        c.Children.Add(selAcento);

        c.Children.Add(Interruptor("Colores desde el wallpaper", _arbol.Bandera("Tema", "DesdeWallpaper", false),
                                   v => _arbol.Poner("Tema", "DesdeWallpaper", v)));
        c.Children.Add(Interruptor("Borde de acento alrededor de la isla", _arbol.Bandera("Tema", "BordeAcento", true),
                                   v => _arbol.Poner("Tema", "BordeAcento", v)));

        c.Children.Add(Rotulo("Mis temas", true));
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(BotonPlano("Exportar", ExportarTema, false));
        fila.Children.Add(BotonPlano("Importar", () => ImportarTema(selFondo, selTexto, selAcento), false));
        fila.Children.Add(BotonPlano("Abrir carpeta", () =>
        {
            Directory.CreateDirectory(Temas.Carpeta);
            Process.Start(new ProcessStartInfo(Temas.Carpeta) { UseShellExecute = true })?.Dispose();
        }, false));
        c.Children.Add(fila);
        return c;
    }

    UIElement MuestraTema(TemaGuardado t)
    {
        var tira = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var hex in new[] { t.Fondo, t.Texto, t.Acento })
            tira.Children.Add(new Border
            {
                Width = 18, Height = 10,
                Background = new SolidColorBrush(Opaco(Hex(hex, Colors.Gray))),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 3, 0)
            });

        var caja = new StackPanel();
        caja.Children.Add(new TextBlock { Text = t.Nombre, Foreground = _texto, FontSize = 12 });
        caja.Children.Add(tira);

        var b = new Button
        {
            Content = caja,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(10, 8, 10, 8),
            Background = _superficie,
            BorderBrush = _acento,
            BorderThickness = new Thickness(1),
            Foreground = _texto,
            Cursor = Cursors.Hand
        };
        b.Click += (_, _) => AplicarTema(t);
        return b;
    }

    void AplicarTema(TemaGuardado t)
    {
        _arbol.Poner("Tema", "Fondo", t.Fondo);
        _arbol.Poner("Tema", "Texto", t.Texto);
        _arbol.Poner("Tema", "Acento", t.Acento);
        _arbol.Poner("Tema", "Radio", t.Radio);

        // Y los roles de color. Van con PonerOQuitar porque un tema que NO trae un rol
        // quiere que se derive, y eso se dice borrando la clave: si solo se escribieran
        // los que vienen, pasar de Nube a Gruvbox dejaría el naranja y el azul nube de
        // Nube puestos encima de Gruvbox para siempre.
        _arbol.PonerOQuitar("Tema", "Superficie", t.Superficie);
        _arbol.PonerOQuitar("Tema", "SuperficieAlta", t.SuperficieAlta);
        _arbol.PonerOQuitar("Tema", "Borde", t.Borde);
        _arbol.PonerOQuitar("Tema", "TextoAtenuado", t.TextoAtenuado);
        _arbol.PonerOQuitar("Tema", "Secundario", t.Secundario);
        _arbol.PonerOQuitar("Tema", "Sistema", t.Sistema);
        _arbol.PonerOQuitar("Tema", "Positivo", t.Positivo);
        _arbol.PonerOQuitar("Tema", "Aviso", t.Aviso);
        _arbol.PonerOQuitar("Tema", "Negativo", t.Negativo);

        _arbol.Guardar();

        // Los selectores se quedan con los valores viejos: se recarga la página de
        // colores. Por id y no por Items[1]: con el registro, el orden de las páginas
        // puede cambiar y un número escrito a mano recargaría la equivocada.
        int donde = _modulos.Indice("colores");
        if (donde < 0 || donde >= _pestanas.Items.Count)
        {
            Log.Info("no encuentro la página de colores para recargarla");
            return;
        }
        var i = _pestanas.SelectedIndex;
        ((TabItem)_pestanas.Items[donde]).Content = new ScrollViewer
        {
            Content = SeccionColores(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8, 12, 8, 8)
        };
        _pestanas.SelectedIndex = i;
    }

    void ExportarTema()
    {
        var t = new TemaGuardado(
            "Mi tema " + DateTime.Now.ToString("yyyy-MM-dd HHmm"),
            _arbol.Texto("Tema", "Fondo", "#E6000000"),
            _arbol.Texto("Tema", "Texto", "#FFFFFF"),
            _arbol.Texto("Tema", "Acento", "#7AA2F7"),
            _arbol.Numero("Tema", "Radio", 20));
        var ruta = Path.Combine(Temas.Carpeta, t.Nombre.Replace(':', '-') + ".json");
        try
        {
            Temas.Exportar(t, ruta);
            Avisar("Tema guardado en " + ruta, false);
        }
        catch (Exception ex) { Avisar("No se pudo exportar: " + ex.Message, true); }
    }

    void ImportarTema(SelectorColor f, SelectorColor t, SelectorColor a)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Temas de la nube (*.json)|*.json",
            InitialDirectory = Directory.Exists(Temas.Carpeta) ? Temas.Carpeta : Config.Carpeta
        };
        if (dlg.ShowDialog() != true) return;
        if (Temas.Importar(dlg.FileName) is not { } tema) { Avisar("Ese archivo no es un tema", true); return; }
        AplicarTema(tema);
        Avisar("Tema importado: " + tema.Nombre, false);
    }

    // La biblioteca de widgets. La construye Biblioteca.cs, que no sabe nada de esta
    // ventana: así el modo edición de la isla podrá usar la misma sin tocarla.
    //
    // Cada acción escribe en el árbol y guarda en el acto (no con el temporizador de
    // 120 ms de los deslizadores): añadir o quitar un widget no es arrastrar un valor,
    // es una decisión, y quien la toma espera verla en la isla ya. La recarga en
    // caliente hace el resto.
    UIElement SeccionBiblioteca()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Biblioteca de widgets", true));
        c.Children.Add(new TextBlock
        {
            Text = "Lo que puedes poner en la nube. Ocultar conserva el sitio del widget; " +
                   "quitar lo devuelve aquí.",
            Foreground = _textoAtenuado,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var biblioteca = new Biblioteca(_arbol, _roles, () => { _escritura.Stop(); _arbol.Guardar(); }, _modulos);
        c.Children.Add(biblioteca.Construir());
        return c;
    }

    UIElement SeccionModulos()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Arrastra los módulos entre columnas para colocarlos", true));

        var rejilla = new Grid();
        for (int i = 0; i < 3; i++) rejilla.ColumnDefinitions.Add(new ColumnDefinition());

        var izq = ListaModulos("Izquierda", _arbol.Lista("Modulos", "Izquierda", "inicio"));
        var cen = ListaModulos("Centro", _arbol.Lista("Modulos", "Centro", "fijadas", "apps", "reloj"));
        var der = ListaModulos("Derecha", _arbol.Lista("Modulos", "Derecha", "cpu", "ram", "bateria", "bandeja", "control", "fecha"));

        Grid.SetColumn(izq.Caja, 0); Grid.SetColumn(cen.Caja, 1); Grid.SetColumn(der.Caja, 2);
        rejilla.Children.Add(izq.Caja); rejilla.Children.Add(cen.Caja); rejilla.Children.Add(der.Caja);
        c.Children.Add(rejilla);

        void Volcar()
        {
            _arbol.PonerLista("Modulos", "Izquierda", izq.Lista.Items.Cast<string>());
            _arbol.PonerLista("Modulos", "Centro", cen.Lista.Items.Cast<string>());
            _arbol.PonerLista("Modulos", "Derecha", der.Lista.Items.Cast<string>());
            Cambio();
        }
        foreach (var l in new[] { izq, cen, der }) l.AlCambiar = Volcar;

        c.Children.Add(Rotulo("Disponibles (doble clic para añadir a la izquierda)", true));
        var disponibles = new WrapPanel();
        foreach (var m in _modulosDisponibles)
        {
            var etiqueta = m;
            var b = new Button
            {
                Content = etiqueta,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 4, 10, 4),
                Background = _superficie,
                Foreground = _texto,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            b.Click += (_, _) => { izq.Lista.Items.Add(etiqueta); Volcar(); };
            disponibles.Children.Add(b);
        }
        c.Children.Add(disponibles);
        c.Children.Add(new TextBlock
        {
            Text = "Doble clic en un módulo de una columna para quitarlo.",
            Foreground = _textoAtenuado, Margin = new Thickness(0, 6, 0, 0)
        });
        return c;
    }

    sealed class CajaModulos
    {
        public Border Caja = new();
        public ListBox Lista = new();
        public Action? AlCambiar;
    }

    CajaModulos ListaModulos(string titulo, List<string> valores)
    {
        var r = new CajaModulos();
        r.Lista = new ListBox
        {
            Height = 190,
            Background = _superficie,
            Foreground = _texto,
            BorderThickness = new Thickness(0),
            AllowDrop = true
        };
        foreach (var v in valores) r.Lista.Items.Add(v);

        // Arrastrar y soltar entre columnas y dentro de la misma
        r.Lista.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (r.Lista.SelectedItem is string sel && e.ClickCount == 1)
                r.Lista.Tag = sel;
        };
        r.Lista.MouseMove += (s, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (r.Lista.Tag is not string sel) return;
            DragDrop.DoDragDrop(r.Lista, new DataObject("modulo", new object[] { sel, r }), DragDropEffects.Move);
            r.Lista.Tag = null;
        };
        r.Lista.DragOver += (s, e) =>
        {
            e.Effects = e.Data.GetDataPresent("modulo") ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        r.Lista.Drop += (s, e) =>
        {
            if (e.Data.GetData("modulo") is not object[] datos) return;
            var texto = (string)datos[0];
            var origen = (CajaModulos)datos[1];
            origen.Lista.Items.Remove(texto);

            int destino = IndiceBajoElRaton(r.Lista, e.GetPosition(r.Lista));
            if (destino < 0 || destino > r.Lista.Items.Count) destino = r.Lista.Items.Count;
            r.Lista.Items.Insert(destino, texto);

            origen.AlCambiar?.Invoke();
            r.AlCambiar?.Invoke();
            e.Handled = true;
        };
        r.Lista.MouseDoubleClick += (s, e) =>
        {
            if (r.Lista.SelectedItem is string sel) { r.Lista.Items.Remove(sel); r.AlCambiar?.Invoke(); }
        };

        var pila = new StackPanel { Margin = new Thickness(4) };
        pila.Children.Add(new TextBlock { Text = titulo, Foreground = _acento, Margin = new Thickness(2, 0, 0, 4) });
        pila.Children.Add(r.Lista);
        r.Caja = new Border { Child = pila };
        return r;
    }

    static int IndiceBajoElRaton(ListBox lista, Point p)
    {
        for (int i = 0; i < lista.Items.Count; i++)
        {
            if (lista.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement fe) continue;
            var sup = fe.TranslatePoint(new Point(0, 0), lista);
            if (p.Y < sup.Y + fe.ActualHeight / 2) return i;
        }
        return lista.Items.Count;
    }

    // Lista editable con subir y bajar: el ORDEN importa en las apps fijadas
    UIElement ListaEditable(String seccion, String clave, String ayuda)
    {
        var c = Columna();
        var lista = new ListBox
        {
            Height = 120,
            Background = _superficie,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        foreach (var f in _arbol.Lista(seccion, clave)) lista.Items.Add(f);

        var nueva = new TextBox
        {
            Height = 28, Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        void Volcar() { _arbol.PonerLista(seccion, clave, lista.Items.Cast<string>()); Cambio(); }

        void Mover(int paso)
        {
            int i = lista.SelectedIndex;
            int j = i + paso;
            if (i < 0 || j < 0 || j >= lista.Items.Count) return;
            var s = lista.Items[i];
            lista.Items.RemoveAt(i);
            lista.Items.Insert(j, s);
            lista.SelectedIndex = j;
            Volcar();
        }

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        fila.Children.Add(BotonPlano("Añadir", () =>
        {
            if (nueva.Text.Trim().Length == 0) return;
            lista.Items.Add(nueva.Text.Trim());
            nueva.Text = "";
            Volcar();
        }, false));
        fila.Children.Add(BotonPlano("Quitar", () =>
        {
            if (lista.SelectedItem is string s) { lista.Items.Remove(s); Volcar(); }
        }, false));
        fila.Children.Add(BotonPlano("Subir", () => Mover(-1), false));
        fila.Children.Add(BotonPlano("Bajar", () => Mover(1), false));

        c.Children.Add(lista);
        c.Children.Add(nueva);
        c.Children.Add(fila);
        c.Children.Add(new TextBlock
        {
            Text = ayuda, Foreground = _textoAtenuado,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });
        return c;
    }

    UIElement SeccionApps()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Iconos de aplicaciones", true));
        c.Children.Add(Deslizador("Máximo visibles", 3, 30, _arbol.Numero("Barra", "MaxApps", 10),
                                  v => _arbol.Poner("Barra", "MaxApps", Math.Round(v))));
        c.Children.Add(Interruptor("Enseñar solo las apps del escritorio virtual actual",
                                   _arbol.Bandera("Barra", "AppsDelEscritorio", false),
                                   v => _arbol.Poner("Barra", "AppsDelEscritorio", v)));

        c.Children.Add(Rotulo("Apps fijadas en la barra", true));
        c.Children.Add(ListaEditable("Barra", "AppsFijadas",
            "Siempre a la vista, abiertas o no, y en este orden. Con el módulo \"fijadas\" puesto " +
            "(viene de serie, junto al reloj) se ven también con la isla recogida, sin acercar el ratón; " +
            "si lo quitas, vuelven al carrusel. Una ruta de ejecutable por línea, o " +
            "shell:AppsFolder\\<id> para las de la Store. Más cómodo: clic derecho sobre el icono " +
            "de una app en la isla."));
        c.Children.Add(Interruptor("Agrupar las ventanas del mismo programa",
                                   _arbol.Bandera("Barra", "AgruparApps", true),
                                   v => _arbol.Poner("Barra", "AgruparApps", v)));
        return c;
    }

    UIElement SeccionAtajos()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Vista general de ventanas", true));

        var aviso = new TextBlock { Foreground = Brushes.Tomato, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        var campo = new TextBox
        {
            Text = _arbol.Texto("Barra", "AtajoVistaGeneral", "Ctrl+Alt+Space"),
            IsReadOnly = true,
            Height = 30,
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        // Nota: NO se pisa el aviso al coger el foco. El resultado de la comprobación
        // es más útil que un "pulsa algo", y al entrar en la pestaña el campo recibe
        // el foco solo, con lo que el mensaje bueno se perdía.
        campo.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            var tecla = e.Key == Key.System ? e.SystemKey : e.Key;
            if (tecla is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                      or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

            var partes = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) partes.Add("Ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) partes.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) partes.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) partes.Add("Win");
            if (partes.Count == 0) { aviso.Text = "Añade al menos Ctrl, Alt, Shift o Win."; return; }

            partes.Add(tecla == Key.Space ? "Space" : tecla.ToString());
            var combo = string.Join("+", partes);
            campo.Text = combo;

            if (ComprobarAtajo(combo, out string problema))
            {
                aviso.Foreground = Brushes.YellowGreen;
                aviso.Text = "Libre: se puede usar.";
                _arbol.Poner("Barra", "AtajoVistaGeneral", combo);
                Cambio();
            }
            else
            {
                aviso.Foreground = Brushes.Tomato;
                aviso.Text = problema;
            }
        };
        c.Children.Add(campo);
        c.Children.Add(aviso);
        c.Children.Add(new TextBlock
        {
            Text = "Aviso: una combinación que YA tenga otra aplicación no se puede teclear aquí, " +
                   "porque Windows se la entrega a esa aplicación y nunca llega a este campo. " +
                   "Por eso la que está guardada se comprueba sola al entrar en esta pestaña.",
            Foreground = _textoAtenuado, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap
        });

        // Comprobar la que ya está guardada es justamente donde avisar sirve de algo,
        // porque esa combinación no se podría volver a teclear. Se deja apuntado para
        // llamarlo cuando la ventana tenga handle: antes RegisterHotKey no vale.
        _revisarAtajo = () =>
        {
            if (ComprobarAtajo(campo.Text, out string problema))
            {
                aviso.Foreground = Brushes.YellowGreen;
                aviso.Text = "La combinación guardada está libre.";
            }
            else
            {
                aviso.Foreground = Brushes.Tomato;
                aviso.Text = problema;
            }
        };

        return c;
    }

    // Se registra de verdad y se suelta: es la única forma de saber si está cogida
    bool ComprobarAtajo(string combo, out string problema)
    {
        problema = "";
        var h = new WindowInteropHelper(this).Handle;
        if (h == IntPtr.Zero) { problema = "aún no se puede comprobar"; return false; }

        const int idPrueba = 0xB0CB;
        if (!Nativo.RegistrarAtajo(h, idPrueba, combo))
        {
            problema = Nativo.UltimoErrorAtajo == 1409
                ? "Esa combinación ya la tiene cogida otra aplicación."
                : $"No se puede registrar (error {Nativo.UltimoErrorAtajo}).";
            return false;
        }
        Nativo.QuitarAtajo(h, idPrueba);
        return true;
    }

    UIElement SeccionInicio()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Menú Inicio", true));
        c.Children.Add(Interruptor("La tecla Windows abre el menú de la nube",
                                   _arbol.Bandera("Inicio", "TeclaWindows", true),
                                   v => _arbol.Poner("Inicio", "TeclaWindows", v)));
        c.Children.Add(Interruptor("Buscar también en archivos (índice de Windows)",
                                   _arbol.Bandera("Inicio", "BuscarArchivos", true),
                                   v => _arbol.Poner("Inicio", "BuscarArchivos", v)));
        c.Children.Add(Interruptor("Saludo en la cabecera",
                                   _arbol.Bandera("Inicio", "Saludo", true),
                                   v => _arbol.Poner("Inicio", "Saludo", v)));

        c.Children.Add(Rotulo("Buscador web", true));
        var web = new TextBox
        {
            Text = _arbol.Texto("Inicio", "BuscadorWeb", "https://www.google.com/search?q="),
            Height = 28, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        web.TextChanged += (_, _) => { _arbol.Poner("Inicio", "BuscadorWeb", web.Text); Cambio(); };
        c.Children.Add(web);

        c.Children.Add(Rotulo("Apps fijadas", true));
        var lista = new ListBox
        {
            Height = 130,
            Background = _superficie,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        foreach (var f in _arbol.Lista("Inicio", "Fijadas")) lista.Items.Add(f);

        var nueva = new TextBox
        {
            Height = 28, Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        void Volcar() { _arbol.PonerLista("Inicio", "Fijadas", lista.Items.Cast<string>()); Cambio(); }

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        fila.Children.Add(BotonPlano("Añadir", () =>
        {
            if (nueva.Text.Trim().Length == 0) return;
            lista.Items.Add(nueva.Text.Trim());
            nueva.Text = "";
            Volcar();
        }, false));
        fila.Children.Add(BotonPlano("Quitar", () =>
        {
            if (lista.SelectedItem is string s) { lista.Items.Remove(s); Volcar(); }
        }, false));

        c.Children.Add(lista);
        c.Children.Add(nueva);
        c.Children.Add(fila);
        return c;
    }

    UIElement SeccionWallpaper()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Wallpaper Engine", true));

        var ruta = new TextBox
        {
            Text = _arbol.Texto("Wallpaper", "Ruta", "") ?? "",
            Height = 28, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        ruta.TextChanged += (_, _) => { _arbol.Poner("Wallpaper", "Ruta", ruta.Text); Cambio(); };
        c.Children.Add(ruta);

        var detectada = WallpaperEngine.Exe;
        c.Children.Add(new TextBlock
        {
            Text = detectada != null ? "Detectado solo en: " + detectada : "No se ha detectado Wallpaper Engine",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        if (detectada != null)
            c.Children.Add(BotonPlano("Usar la ruta detectada", () => ruta.Text = detectada, false));

        c.Children.Add(Deslizador("Monitor", 0, 3, _arbol.Numero("Wallpaper", "Monitor", 0),
                                  v => _arbol.Poner("Wallpaper", "Monitor", Math.Round(v))));
        c.Children.Add(Interruptor("Enviar mi color de acento al wallpaper",
                                   _arbol.Bandera("Tema", "EnviarColorAlWallpaper", false),
                                   v => _arbol.Poner("Tema", "EnviarColorAlWallpaper", v)));
        return c;
    }

    UIElement SeccionSistema()
    {
        var c = Columna();
        c.Children.Add(Rotulo("Arranque", true));

        var estado = new TextBlock { Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        estado.Text = Arranque.Activo ? "Ahora mismo sí arranca con Windows." : "Ahora mismo no arranca con Windows.";

        c.Children.Add(Interruptor("Arrancar con Windows", Arranque.Activo, v =>
        {
            bool ok = Arranque.Poner(v, out string msg);
            estado.Foreground = ok ? _texto : Brushes.Tomato;
            estado.Opacity = ok ? 0.6 : 1;
            estado.Text = (ok ? "Hecho: " : "No se pudo: ") + msg;
        }));
        c.Children.Add(estado);
        c.Children.Add(new TextBlock
        {
            Text = "Crea o quita un acceso directo en la carpeta Inicio de Windows hacia " + Arranque.ExeRecomendado(),
            Foreground = _texto, Opacity = 0.45, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });

        c.Children.Add(Rotulo("Bandeja del sistema", true));
        c.Children.Add(Interruptor("Traer los iconos de la bandeja a la isla",
                                   _arbol.Bandera("Barra", "Bandeja", true),
                                   v => _arbol.Poner("Barra", "Bandeja", v)));
        c.Children.Add(Deslizador("Iconos visibles antes del desplegable (0 = solo el \"^\")", 0, 15,
                                  _arbol.Numero("Barra", "MaxBandeja", 0),
                                  v => _arbol.Poner("Barra", "MaxBandeja", Math.Round(v))));
        c.Children.Add(new TextBlock
        {
            Text = "Para que se encienda hay que añadir además el módulo \"bandeja\" en Módulos: " +
                   "si no estuviera puesto, los iconos desaparecerían de la barra de Windows sin " +
                   "salir en ningún sitio. Al cerrar NubeBar vuelven solos a la bandeja de Windows.",
            Foreground = _texto, Opacity = 0.45, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });

        c.Children.Add(Rotulo("Notificaciones", true));
        c.Children.Add(Interruptor("Enseñar las notificaciones en la isla",
                                   _arbol.Bandera("Barra", "Notificaciones", true),
                                   v => _arbol.Poner("Barra", "Notificaciones", v)));
        var silenciadas = _arbol.Lista("Barra", "NotificacionesSilenciadas");
        var caja = new TextBox
        {
            Text = string.Join(Environment.NewLine, silenciadas),
            AcceptsReturn = true, MinHeight = 56, MaxHeight = 110,
            Background = _superficieAlta,
            Foreground = _texto, BorderBrush = _acento, BorderThickness = new Thickness(1),
            Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        caja.TextChanged += (_, _) => _arbol.PonerLista("Barra", "NotificacionesSilenciadas",
            caja.Text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList());
        c.Children.Add(caja);
        c.Children.Add(new TextBlock
        {
            Text = "Una app por línea para silenciarla: basta un trozo de su identificador " +
                   "(por ejemplo \"Discord\" o \"WindowsStore\"). \"No molestar\" se respeta siempre.",
            Foreground = _texto, Opacity = 0.45, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });

        c.Children.Add(Rotulo("Barra de tareas de Windows", true));
        c.Children.Add(Interruptor("Ocultarla mientras la nube esté abierta",
                                   _arbol.Bandera("Barra", "OcultarBarraWindows", true),
                                   v => _arbol.Poner("Barra", "OcultarBarraWindows", v)));
        // El valor de serie depende de la ORIENTACIÓN: una barra horizontal reserva su
        // franja como cualquier barra, y una vertical no, porque lo que se espera de
        // una barra lateral es que flote por encima de las ventanas. Tocar este
        // interruptor lo convierte en una elección tuya y manda sobre el defecto.
        string modoAhora = _arbol.TextoDelPanel(Principal, "Modo", "isla");
        string bordeAhora = _arbol.TextoDelPanel(Principal, "Posicion", "arriba");
        bool reservaDeSerie = PanelNube.ReservaPorDefecto(modoAhora, bordeAhora);
        c.Children.Add(Interruptor("Reservar espacio en pantalla (solo en modo barra)",
                                   _arbol.BanderaDelPanel(Principal, "ReservarEspacio", reservaDeSerie),
                                   v => _arbol.PonerDelPanel(Principal, "ReservarEspacio", v)));
        var pistaReserva = Rotulo(PanelNube.EsVertical(modoAhora, bordeAhora)
            ? "Con la barra vertical viene apagado: la nube flota por encima de las ventanas. " +
              "Enciéndelo si prefieres que Windows le guarde la franja, como a la barra de tareas."
            : "Con la barra horizontal viene encendido: Windows le guarda su franja y las " +
              "ventanas maximizadas no la tapan.");
        pistaReserva.TextWrapping = TextWrapping.Wrap;
        pistaReserva.Opacity = 0.55;
        c.Children.Add(pistaReserva);

        c.Children.Add(Rotulo("Archivos", true));
        c.Children.Add(BotonPlano("Abrir la carpeta del config", () =>
            Process.Start(new ProcessStartInfo(Config.Carpeta) { UseShellExecute = true })?.Dispose(), false));
        return c;
    }

    // ================= Guardar / cancelar =================
    void Avisar(string texto, bool malo)
    {
        Title = texto;
        Log.Info("ajustes: " + texto);
        if (malo) SystemSounds2.Error();
    }

    void Guardar()
    {
        _escritura.Stop();
        _arbol.HacerCopiaUnaVez();
        _arbol.Guardar();
        _guardado = true;
        Close();
    }

    void Cancelar()
    {
        _escritura.Stop();
        _arbol.Revertir();
        _guardado = true;        // ya se ha dejado el fichero como estaba
        Close();
    }
}

static class SystemSounds2
{
    public static void Error()
    {
        try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
    }
}
