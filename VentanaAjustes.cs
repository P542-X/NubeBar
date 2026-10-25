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
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
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
    // La misma fuente de iconos que la nube, y por el mismo motivo: si no está, Windows
    // sustituye por cajas. Se nombra una vez aquí en vez de repetirla en cada glifo.
    const string FuenteDeIconos = MainWindow.FuenteIconos;


    SolidColorBrush _fondo = new(Color.FromRgb(0x16, 0x16, 0x1A));
    SolidColorBrush _texto = new(Colors.White);
    SolidColorBrush _acento = new(Color.FromRgb(0x7A, 0xA2, 0xF7));
    SolidColorBrush _superficie = new(Color.FromArgb(0x1E, 255, 255, 255));
    SolidColorBrush _superficieAlta = new(Color.FromArgb(0x33, 255, 255, 255));
    SolidColorBrush _textoAtenuado = new(Color.FromRgb(0xA0, 0xA0, 0xA0));
    // El fondo de lo ELEGIDO y el acento cuando hace de texto. Salen de los roles, no de
    // una mezcla a mano: es la regla desde la Fase A y lo que hace que funcionen igual en
    // los siete temas, en claro y en oscuro.
    SolidColorBrush _elegido = new(Color.FromArgb(0x33, 0x7A, 0xA2, 0xF7));
    SolidColorBrush _acentoLegible = new(Color.FromRgb(0x7A, 0xA2, 0xF7));
    internal Roles _roles = Roles.PorDefecto();

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
        // Más ancha que antes porque ahora hay navegación lateral: con 760 el contenido se
        // quedaba en 570 y las tarjetas con dos columnas no caben. Y con mínimos, para que
        // una ventana estrecha no deje el pie encima del contenido ni recorte la lista.
        Width = 940;
        Height = 660;
        MinWidth = 720;
        MinHeight = 460;
        // Y nunca más grande que la pantalla donde se abre: a 150 % de PPP una ventana de
        // 940x660 en un portátil de 1366x768 nacía con el pie fuera.
        var pant = Monitores.DelRaton() ?? Monitores.Principal();
        double anchoUtil = pant.TrabajoAncho / pant.Escala, altoUtil = pant.TrabajoAlto / pant.Escala;
        Width = Math.Min(Width, Math.Max(MinWidth, anchoUtil - 40));
        Height = Math.Min(Height, Math.Max(MinHeight, altoUtil - 40));
        // Centrada en la pantalla donde tengas el ratón, no siempre en la principal
        WindowStartupLocation = WindowStartupLocation.Manual;
        var m = pant;
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
        _elegido = new SolidColorBrush(_roles.Seleccion);
        _acentoLegible = new SolidColorBrush(_roles.AcentoTinta);
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
    // ================= El marco de la ventana =================
    //
    // ---------- Qué cambia y qué NO ----------
    //
    // Cambia el marco: una cabecera que se queda quieta, navegación lateral agrupada en
    // tres categorías, el contenido en tarjetas y UNA barra de acciones al pie. No cambia
    // ni una sección: siguen siendo los mismos `Seccion*()`, con los mismos ids y el mismo
    // registro, porque el registro ya las guardaba como `Func<UIElement>`.
    //
    // Y el `TabControl` se queda. Parece raro si no se ve la navegación encima, y es
    // deliberado: `RehacerPagina(id, ...)`, `_modulos.Indice(id)` y el botón de la sección
    // Fondo que salta a Wallpaper trabajan contra él. Lo que se le quita es la tira de
    // pestañas —con una plantilla que solo pinta el contenido elegido— y lo que se le pone
    // es la lista lateral mandándole el `SelectedIndex`. Sustituirlo por otro anfitrión
    // habría significado reescribir esas tres cosas para no ganar nada.

    // ---------- Los iconos ----------
    //
    // Por su número y nunca como carácter literal. Un glifo de la zona de uso privado
    // escrito a pelo se pierde al reescribir el archivo y el botón se queda funcionando
    // pero invisible: pasó con el de la cuadrícula en C4.1 y no se repite.
    static readonly Dictionary<string, int> Iconos = new()
    {
        ["apariencia"] = 0xE771,   // Personalize
        ["colores"] = 0xE790,      // Color
        ["fondo"] = 0xE91B,        // Photo
        ["wallpaper"] = 0xE786,    // Slideshow
        ["perfiles"] = 0xE77B,     // Contact2
        ["modulos"] = 0xE8A9,      // GridView
        ["biblioteca"] = 0xE8F1,   // Library
        ["apps"] = 0xECAA,         // AppIconDefault
        ["atajos"] = 0xE765,       // KeyboardClassic
        ["inicio"] = 0xE80F,       // Home
        ["sistema"] = 0xE713,      // Settings
    };

    const int CodigoNube = 0xE753;      // Cloud

    static string Glifo(int codigo) => char.ConvertFromUtf32(codigo);

    internal static string SubtituloDe(string id) =>
        Subtitulos.TryGetValue(id, out var s) ? s : "";

    internal static string GlifoDeSeccion(string id) =>
        Glifo(Iconos.TryGetValue(id, out int c) ? c : 0xE713);

    // ---------- Las categorías ----------
    //
    // El registro ya agrupa bien: "Aspecto" son exactamente las cinco de personalización,
    // "Panel" las tres de NubeBar y "Sistema" las tres del sistema. Así que esto es solo
    // cómo se LLAMAN en pantalla; no se toca ninguna registración ni ningún id.
    static readonly (string Id, string Nombre, string Sub)[] Categorias =
    {
        ("Aspecto", "Personalización", "Personaliza el aspecto de tu escritorio"),
        ("Panel", "NubeBar", "Organiza los widgets y módulos"),
        ("Sistema", "Sistema", "Controla ventanas, atajos y comportamiento"),
    };

    static readonly Dictionary<string, string> Subtitulos = new()
    {
        ["apariencia"] = "El tema, la forma y cuánto se mueve todo",
        ["colores"] = "Los colores del tema y sus roles",
        ["fondo"] = "El fondo del escritorio, por tipo y por monitor",
        ["wallpaper"] = "Wallpaper Engine y su integración con la escena de inicio",
        ["perfiles"] = "Configura tu perfil actual",
        ["modulos"] = "Qué widgets hay en cada zona de la nube",
        ["biblioteca"] = "Añade, oculta o quita widgets",
        ["apps"] = "El carrusel de aplicaciones y las fijadas",
        ["atajos"] = "Las combinaciones de teclado",
        ["inicio"] = "El menú Inicio propio y su buscador",
        ["sistema"] = "Arranque, notificaciones y diagnóstico",
    };

    // ---------- Estado del marco ----------
    TextBlock? _subtitulo;
    TextBlock? _sinGuardar;
    Button? _botonGuardar;
    TextBox? _busqueda;
    StackPanel? _lateral;
    readonly List<(string Id, Button Boton)> _navegacion = new();
    readonly DispatcherTimer _vigilaCambios = new() { Interval = TimeSpan.FromMilliseconds(400) };

    UIElement Montar()
    {
        var marco = new Border
        {
            Background = _fondo,
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Black }
        };

        var rejilla = new Grid();
        rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                  // cabecera
        rejilla.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                  // acciones

        // ================= Las secciones, por el registro =================
        RegistrarSecciones();

        // ================= Cabecera =================
        var cabecera = Cabecera();
        Grid.SetRow(cabecera, 0);
        rejilla.Children.Add(cabecera);

        // ================= Cuerpo: lateral + contenido =================
        var cuerpo = new Grid();
        // El lateral con ancho fijo: con Auto, un nombre largo lo ensanchaba y el contenido
        // se encogía al cambiar de sección, que se lee como que la ventana salta.
        cuerpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196) });
        cuerpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lateral = Lateral();
        Grid.SetColumn(lateral, 0);
        cuerpo.Children.Add(lateral);

        _pestanas = new TabControl
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = _texto,
            // La plantilla pinta SOLO el contenido elegido: la tira de pestañas desaparece
            // y la navegación la lleva la lista lateral.
            Template = PlantillaSinPestanas(),
        };
        foreach (var m in _modulos.Todos)
            _pestanas.Items.Add(Pestana(m.Descriptor.Nombre, m.Construir()));
        _pestanas.SelectionChanged += (_, e) => { if (e.OriginalSource == _pestanas) AlCambiarDePagina(); };
        Grid.SetColumn(_pestanas, 1);
        cuerpo.Children.Add(_pestanas);

        Grid.SetRow(cuerpo, 1);
        rejilla.Children.Add(cuerpo);

        // ================= Barra de acciones, una sola =================
        var pie = Pie();
        Grid.SetRow(pie, 2);
        rejilla.Children.Add(pie);

        marco.Child = rejilla;

        // El indicador de cambios sin guardar se mira con un reloj corto en vez de
        // engancharse a cada control: hay ciento y pico, y `HaCambiado()` es comparar dos
        // cadenas. Enganchar ciento y pico oyentes para esto sería pagar de más.
        _vigilaCambios.Tick += (_, _) => RefrescarCambios();
        _vigilaCambios.Start();
        Closed += (_, _) => _vigilaCambios.Stop();

        AlCambiarDePagina();
        return marco;
    }

    void RegistrarSecciones()
    {
        // Los mismos ids, los mismos nombres y las mismas palabras clave de siempre: la
        // navegación nueva se monta ENCIMA del registro, no en su lugar.
        _modulos.Registrar("apariencia", "Apariencia", "Aspecto", SeccionApariencia,
                           "tema", "radio", "fuente", "tamaño", "opacidad", "animaciones", "movimiento");
        _modulos.Registrar("colores", "Colores", "Aspecto", SeccionColores,
                           "acento", "fondo", "texto", "paleta", "hsv", "wallpaper", "roles", "contraste");
        _modulos.Registrar("fondo", "Fondo", "Aspecto", SeccionFondo,
                           "fondo", "escritorio", "degradado", "color sólido", "imagen",
                           "presentación", "wallpaper", "monitor", "rellenar", "ajustar");
        _modulos.Registrar("wallpaper", "Wallpaper", "Aspecto", SeccionWallpaper,
                           "fondo", "wallpaper engine", "monitor", "paleta", "integración",
                           "playlist", "escena", "inicio", "espera", "animado");
        _modulos.Registrar("perfiles", "Perfiles", "Aspecto", SeccionPerfiles,
                           "perfil", "concentración", "creativo", "noche", "minimal",
                           "aplicar", "deshacer", "exportar", "importar", "duplicar");
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
        _modulos.Registrar("sistema", "Sistema", "Sistema", SeccionSistema,
                           "arrancar", "windows", "notificaciones", "monitores", "diagnóstico");
    }

    // ---------- La cabecera ----------
    UIElement Cabecera()
    {
        var marco = new Border
        {
            Background = new SolidColorBrush(_roles.Superficie),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 16, 20, 14),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
        };

        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // La nube
        var nube = new TextBlock
        {
            Text = Glifo(CodigoNube),
            FontFamily = new FontFamily(FuenteDeIconos),
            FontSize = 26,
            Foreground = _acentoLegible,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        };
        Grid.SetColumn(nube, 0);
        fila.Children.Add(nube);

        // Título y subtítulo
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = "Ajustes de la nube",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = _texto,
        });
        _subtitulo = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Foreground = _textoAtenuado,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        textos.Children.Add(_subtitulo);
        Grid.SetColumn(textos, 1);
        fila.Children.Add(textos);

        // Buscador, perfil activo y cambios pendientes
        var derecha = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _sinGuardar = new TextBlock
        {
            Text = "",
            FontSize = 11.5,
            Foreground = _acentoLegible,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        derecha.Children.Add(_sinGuardar);

        var perfil = Perfiles.LeerEstado().Activo;
        if (!string.IsNullOrWhiteSpace(perfil))
        {
            var p = Perfiles.Todos().FirstOrDefault(x => x.Id == perfil);
            derecha.Children.Add(new Border
            {
                Background = new SolidColorBrush(_roles.Seleccion),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = p?.Nombre ?? perfil!,
                    FontSize = 11.5,
                    Foreground = _acentoLegible,
                },
                ToolTip = "El perfil que está puesto",
            });
        }

        derecha.Children.Add(CajaDeBusqueda());
        Grid.SetColumn(derecha, 2);
        fila.Children.Add(derecha);

        marco.Child = fila;
        return marco;
    }

    UIElement CajaDeBusqueda()
    {
        var caja = new Border
        {
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(new TextBlock
        {
            Text = Glifo(0xE721),       // Search
            FontFamily = new FontFamily(FuenteDeIconos),
            FontSize = 12,
            Foreground = _textoAtenuado,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });

        _busqueda = new TextBox
        {
            Width = 170, Height = 26,
            Background = Brushes.Transparent,
            Foreground = _texto,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Buscar en los ajustes (por nombre, descripción o categoría)",
        };
        _busqueda.TextChanged += (_, _) => Filtrar(_busqueda!.Text);
        _busqueda.KeyDown += (_, e) =>
        {
            // Esc limpia la búsqueda en vez de cerrar la ventana: cerrarla con una
            // búsqueda escrita a medias se lee como perder el trabajo.
            if (e.Key == Key.Escape && _busqueda!.Text.Length > 0)
            {
                _busqueda.Text = "";
                e.Handled = true;
            }
        };
        fila.Children.Add(_busqueda);

        var limpiar = new Button
        {
            Content = Glifo(0xE711),    // Cancel
            FontFamily = new FontFamily(FuenteDeIconos),
            FontSize = 10,
            Width = 22, Height = 22,
            Background = Brushes.Transparent,
            Foreground = _textoAtenuado,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "Limpiar la búsqueda",
            VerticalAlignment = VerticalAlignment.Center,
        };
        limpiar.Click += (_, _) => { if (_busqueda != null) _busqueda.Text = ""; };
        fila.Children.Add(limpiar);

        caja.Child = fila;
        return caja;
    }

    // ---------- La navegación lateral ----------
    UIElement Lateral()
    {
        var marco = new Border
        {
            // Un fondo distinto del contenido, para que se lean como dos zonas. Sale del
            // rol de superficie, no de una mezcla a mano.
            Background = new SolidColorBrush(_roles.Superficie),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(10, 12, 10, 12),
        };

        _lateral = new StackPanel();
        _navegacion.Clear();

        foreach (var (catId, catNombre, _) in Categorias)
        {
            var delGrupo = _modulos.Todos
                .Where(m => m.Descriptor.Categoria.Equals(catId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (delGrupo.Count == 0) continue;

            _lateral.Children.Add(new TextBlock
            {
                Text = catNombre.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = _textoAtenuado,
                Opacity = 0.75,
                Margin = new Thickness(10, 12, 0, 6),
            });

            foreach (var m in delGrupo)
                _lateral.Children.Add(BotonDeNavegacion(m.Descriptor));
        }

        marco.Child = new ScrollViewer
        {
            Content = _lateral,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        return marco;
    }

    Button BotonDeNavegacion(DescriptorAjustes d)
    {
        var contenido = new StackPanel { Orientation = Orientation.Horizontal };
        contenido.Children.Add(new TextBlock
        {
            Text = GlifoDeSeccion(d.Id),
            FontFamily = new FontFamily(FuenteDeIconos),
            FontSize = 15,
            Width = 22,
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
        });
        contenido.Children.Add(new TextBlock
        {
            Text = d.Nombre,
            FontSize = 13,
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });

        var b = new Button
        {
            Content = contenido,
            // El filo va SIEMPRE puesto y lo que cambia es su color: con el grosor a 0 y a
            // 1 según la selección, el texto se movía un píxel al cambiar de página.
            Template = PlantillaDeNavegacion(),
            Padding = new Thickness(10, 8, 12, 8),
            Margin = new Thickness(0, 1, 0, 1),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Tag = d.Id,
            ToolTip = Subtitulos.TryGetValue(d.Id, out var s) ? s : d.Nombre,
        };
        b.Click += (_, _) => Ir(d.Id);
        b.KeyDown += (_, e) =>
        {
            // Flechas para moverse por la lista, como en cualquier panel de ajustes.
            if (e.Key is not (Key.Up or Key.Down)) return;
            var vivos = _navegacion.Where(x => x.Boton.Visibility == Visibility.Visible).ToList();
            int i = vivos.FindIndex(x => x.Id == d.Id);
            if (i < 0) return;
            int j = e.Key == Key.Down ? i + 1 : i - 1;
            if (j < 0 || j >= vivos.Count) return;
            Ir(vivos[j].Id);
            vivos[j].Boton.Focus();
            e.Handled = true;
        };

        _navegacion.Add((d.Id, b));
        return b;
    }

    // Plantilla del botón de navegación: superficie al pasar por encima, selección con el
    // velo del acento y una barrita del acento a la izquierda. Todo de los roles.
    ControlTemplate PlantillaDeNavegacion()
    {
        var plantilla = new ControlTemplate(typeof(Button));

        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(3, 0, 0, 0));
        marco.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        marco.SetBinding(Border.PaddingProperty,
            new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

        var presentador = new FrameworkElementFactory(typeof(ContentPresenter));
        presentador.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        presentador.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        marco.AppendChild(presentador);
        plantilla.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(_roles.SuperficieAlta), "marco"));
        plantilla.Triggers.Add(encima);

        // El foco de teclado se ve: es lo que hace la ventana usable con Tab.
        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(_roles.AcentoTinta), "marco"));
        plantilla.Triggers.Add(foco);

        return plantilla;
    }

    // El TabControl sin su tira de pestañas: solo el contenido elegido.
    ControlTemplate PlantillaSinPestanas()
    {
        var plantilla = new ControlTemplate(typeof(TabControl));
        var presentador = new FrameworkElementFactory(typeof(ContentPresenter));
        presentador.SetBinding(ContentPresenter.ContentProperty,
            new System.Windows.Data.Binding("SelectedContent") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        plantilla.VisualTree = presentador;
        return plantilla;
    }

    // ---------- Ir a una sección ----------
    internal void Ir(string id)
    {
        int donde = _modulos.Indice(id);
        if (donde < 0 || donde >= _pestanas.Items.Count) return;
        _pestanas.SelectedIndex = donde;
        AlCambiarDePagina();
    }

    // ---------- La transición al cambiar de página ----------
    //
    // Un fundido corto, y respetando el nivel de animación: `Animacion.EscalarFundido`
    // devuelve 0 en "Desactivado", y entonces **no se anima nada** — la página cambia de
    // golpe, que es lo que se pidió. No hay rebote, ni desplazamiento, ni nada que se
    // mueva: un panel de ajustes que se menea cansa a la tercera vez.
    void FundirPagina()
    {
        if (_pestanas.SelectedIndex < 0) return;
        double ms = Animacion.EscalarFundido(110);
        if (ms <= 0) { _pestanas.Opacity = 1; return; }
        _pestanas.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.55,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(ms),
        });
    }

    void AlCambiarDePagina()
    {
        FundirPagina();
        var id = IdDeLaPaginaActual();
        if (_subtitulo != null)
            _subtitulo.Text = Subtitulos.TryGetValue(id, out var s)
                ? s
                : Categorias.FirstOrDefault(c => c.Id == CategoriaDe(id)).Sub ?? "";

        foreach (var (nid, b) in _navegacion)
        {
            bool elegido = nid == id;
            if (b.Template.FindName("marco", b) is not Border marco) continue;
            marco.Background = elegido ? new SolidColorBrush(_roles.Seleccion) : Brushes.Transparent;
            marco.BorderBrush = elegido ? new SolidColorBrush(_roles.Acento) : Brushes.Transparent;
            if (b.Content is StackPanel sp)
                foreach (var hijo in sp.Children.OfType<TextBlock>())
                {
                    hijo.Foreground = elegido ? _acentoLegible : _texto;
                    hijo.FontWeight = elegido ? FontWeights.SemiBold : FontWeights.Normal;
                }
        }
    }

    string IdDeLaPaginaActual()
    {
        int i = _pestanas.SelectedIndex;
        return i >= 0 && i < _modulos.Todos.Count ? _modulos.Todos[i].Descriptor.Id : "";
    }

    string CategoriaDe(string id) => _modulos.Buscar(id)?.Descriptor.Categoria ?? "";

    // ---------- La búsqueda ----------
    //
    // Filtra la LISTA, no destruye la página: lo que estás viendo sigue ahí mientras
    // buscas. Reutiliza las palabras clave que el registro ya guardaba desde la Fase A,
    // que se escribieron precisamente para esto.
    internal static bool Coincide(DescriptorAjustes d, string texto, string categoriaVisible)
    {
        if (string.IsNullOrWhiteSpace(texto)) return true;
        var q = texto.Trim();

        // ---------- Sin acentos y sin mayúsculas ----------
        //
        // `Contains(q, CurrentCultureIgnoreCase)` ignora las mayúsculas pero NO los
        // acentos, y eso se nota en castellano a la primera: buscar "animación" no
        // encontraba la palabra clave "animaciones", y buscar "presentacion" sin tilde no
        // encontraba "presentación". Quien busca no sabe cómo lo escribimos nosotros.
        //
        // `CompareInfo.IndexOf` con `IgnoreNonSpace` compara plegando los diacríticos, que
        // es lo que hace cualquier buscador decente y no hay que inventar.
        var ci = System.Globalization.CultureInfo.CurrentCulture.CompareInfo;
        const System.Globalization.CompareOptions como =
            System.Globalization.CompareOptions.IgnoreCase |
            System.Globalization.CompareOptions.IgnoreNonSpace;

        bool En(string? s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            try { return ci.IndexOf(s, q, como) >= 0; }
            catch { return s.Contains(q, StringComparison.OrdinalIgnoreCase); }
        }

        if (En(d.Nombre) || En(d.Id) || En(categoriaVisible)) return true;
        return d.PalabrasClave.Any(En);
    }

    void Filtrar(string texto)
    {
        int visibles = 0;
        foreach (var (id, b) in _navegacion)
        {
            var d = _modulos.Buscar(id)?.Descriptor;
            bool ok = d != null && Coincide(d, texto, NombreDeCategoriaVisible(d.Categoria));
            b.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
            if (ok) visibles++;
        }

        // Los rótulos de categoría se esconden si no les queda nada debajo: una categoría
        // vacía con su título puesto parece un fallo.
        if (_lateral != null)
        {
            string? cat = null;
            var porRotulo = new Dictionary<TextBlock, List<Button>>();
            TextBlock? ultimo = null;
            foreach (var hijo in _lateral.Children)
            {
                if (hijo is TextBlock t) { ultimo = t; porRotulo[t] = new List<Button>(); cat = t.Text; }
                else if (hijo is Button b && ultimo != null) porRotulo[ultimo].Add(b);
            }
            foreach (var (rotulo, botones) in porRotulo)
                rotulo.Visibility = botones.Any(x => x.Visibility == Visibility.Visible)
                    ? Visibility.Visible : Visibility.Collapsed;
            _ = cat;
        }

        if (_subtitulo != null && texto.Trim().Length > 0)
            _subtitulo.Text = visibles == 0
                ? $"Nada coincide con «{texto.Trim()}». Prueba con «tema», «fondo», «widget» o «ventana»."
                : $"{visibles} sección(es) coinciden con «{texto.Trim()}»";
        else AlCambiarDePagina();

        // Con una sola coincidencia se salta sola: es lo que uno espera al escribir.
        if (visibles == 1 && texto.Trim().Length >= 3)
        {
            var unica = _navegacion.FirstOrDefault(x => x.Boton.Visibility == Visibility.Visible);
            if (unica.Id != null && unica.Id != IdDeLaPaginaActual())
            {
                int donde = _modulos.Indice(unica.Id);
                if (donde >= 0) _pestanas.SelectedIndex = donde;
            }
        }
    }

    internal static string NombreDeCategoriaVisible(string catId) =>
        Categorias.FirstOrDefault(c => c.Id.Equals(catId, StringComparison.OrdinalIgnoreCase)).Nombre ?? catId;

    // ---------- La barra de acciones, UNA ----------
    //
    // Antes había Guardar/Cancelar abajo y además Aplicar/Cancelar/Restaurar dentro de la
    // página de Fondo, que es el tipo de duplicado que hace dudar de qué botón manda. Lo
    // global vive aquí y solo aquí; lo de una página se queda en su tarjeta pero con un
    // nombre que diga qué hace de verdad ("Aplicar el fondo ahora", no "Aplicar").
    UIElement Pie()
    {
        var marco = new Border
        {
            Background = new SolidColorBrush(_roles.Superficie),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 12, 20, 12),
            CornerRadius = new CornerRadius(0, 0, 16, 16),
        };

        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ---------- Por qué NO hay un "Restaurar" global ----------
        //
        // Se pidió, y no existe en el código: no hay ninguna operación de "volver a los
        // valores de fábrica", y añadirla en una fase de presentación habría sido meter una
        // acción destructiva nueva de paso. Lo que sí existe vive donde tiene sentido y con
        // un nombre que dice qué hace: "Restaurar" en una tarjeta de perfil devuelve ESE
        // perfil a fábrica, y "Restaurar el fondo anterior" en la página de Fondo devuelve
        // el fondo que había. Eso no es un duplicado del pie: son otras cosas.
        //
        // Aquí quedan las dos globales, una vez cada una.
        var izquierda = new TextBlock
        {
            Text = "Los cambios se guardan al pulsar Guardar",
            FontSize = 11,
            Foreground = _textoAtenuado,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(izquierda, 0);
        fila.Children.Add(izquierda);

        var derecha = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        derecha.Children.Add(BotonPlano("Cancelar", Cancelar, false));
        _botonGuardar = BotonPlano("Guardar", Guardar, true);
        derecha.Children.Add(_botonGuardar);
        Grid.SetColumn(derecha, 2);
        fila.Children.Add(derecha);

        marco.Child = fila;
        return marco;
    }


    // ---------- La tarjeta ----------
    //
    // Un bloque con título, icono, descripción breve y sus controles dentro. Lo que evita
    // es la pantalla de controles sueltos sobre el fondo plano: con la tarjeta, lo que va
    // junto se ve junto.
    //
    // El radio y la separación salen de aquí y no de cada sitio, para que todas midan lo
    // mismo. Y el filo va siempre puesto cambiando solo de color, por lo de siempre: con
    // el grosor variable, el contenido se mueve al cambiar de estado.
    const double RadioTarjeta = 12;

    internal Border Tarjeta(string titulo, int icono, string? descripcion, UIElement contenido)
    {
        var col = new StackPanel();

        var cab = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        cab.Children.Add(new TextBlock
        {
            Text = Glifo(icono),
            FontFamily = new FontFamily(FuenteDeIconos),
            FontSize = 14,
            Foreground = _acentoLegible,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0),
        });
        cab.Children.Add(new TextBlock
        {
            Text = titulo,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
        });
        col.Children.Add(cab);

        if (!string.IsNullOrWhiteSpace(descripcion))
            col.Children.Add(new TextBlock
            {
                Text = descripcion,
                FontSize = 11.5,
                Foreground = _textoAtenuado,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(23, 0, 0, 10),
            });
        else
            col.Children.Add(new Border { Height = 8 });

        col.Children.Add(contenido);

        return new Border
        {
            Background = new SolidColorBrush(_roles.Superficie),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(RadioTarjeta),
            Padding = new Thickness(16, 14, 16, 16),
            Margin = new Thickness(0, 0, 0, 14),
            Child = col,
        };
    }

    // El encabezado de una página: título grande y una línea de qué es. Va dentro del
    // contenido y no en la cabecera de la ventana, que es de la ventana.
    internal UIElement TituloDePagina(string titulo, string descripcion)
    {
        var col = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        col.Children.Add(new TextBlock
        {
            Text = titulo,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = _texto,
        });
        if (descripcion.Length > 0)
            col.Children.Add(new TextBlock
            {
                Text = descripcion,
                FontSize = 12,
                Foreground = _textoAtenuado,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        return col;
    }


    // ================= El constructor de páginas =================
    //
    // Las once secciones estaban escritas igual: una columna, y dentro una tirada de
    // `Rotulo("X", true)` separando grupos de controles. O sea que la estructura YA estaba
    // —los grupos existían— y lo único que faltaba era dibujarla.
    //
    // Esto es lo que la dibuja. Cada `Grupo(...)` cierra la tarjeta anterior y abre otra, y
    // `Mas(...)` echa controles en la que esté abierta. Así migrar una sección es cambiar
    // `Rotulo(x, true)` por `Grupo(x, icono)` y `c.Children.Add` por `p.Mas`: ni un control
    // se toca, ni una lambda cambia, ni un valor se mueve de sitio.
    //
    // Y existe en vez de repetir la subcolumna a mano en once sitios porque a la tercera
    // sección ya se te olvida cerrar una.
    sealed class Pagina
    {
        readonly VentanaAjustes _v;
        readonly StackPanel _raiz = new();
        StackPanel? _dentro;            // el contenido de la tarjeta abierta

        public Pagina(VentanaAjustes v, string titulo, string descripcion)
        {
            _v = v;
            _raiz.Margin = new Thickness(22, 20, 22, 20);
            _raiz.Children.Add(_v.TituloDePagina(titulo, descripcion));
        }

        // Abre una tarjeta. La anterior se queda cerrada con lo que se le echó.
        public Pagina Grupo(string titulo, int icono, string? descripcion = null)
        {
            _dentro = new StackPanel();
            _raiz.Children.Add(_v.Tarjeta(titulo, icono, descripcion, _dentro));
            return this;
        }

        // Un control. Si nadie abrió tarjeta, se abre una sin título: más vale una tarjeta
        // sin rótulo que un control suelto sobre el fondo.
        public Pagina Mas(UIElement e)
        {
            if (_dentro == null) Grupo("", 0xE713);
            _dentro!.Children.Add(e);
            return this;
        }

        // Ayuda contextual: dentro de la tarjeta, atenuada y con sangría. Es lo que
        // sustituye a los párrafos sueltos.
        public Pagina Pista(string texto)
        {
            var t = _v.Rotulo(texto);
            t.TextWrapping = TextWrapping.Wrap;
            t.FontSize = 11;
            t.Opacity = 0.6;
            t.Margin = new Thickness(0, 8, 0, 0);
            return Mas(t);
        }

        // Un aviso: lo mismo pero con filo, para lo que el usuario tiene que ver de verdad.
        // Nunca con rojo a mano: el rol `Aviso` es el que sabe de qué color va en cada tema.
        public Pagina Aviso(string texto)
        {
            var t = _v.Rotulo(texto);
            t.TextWrapping = TextWrapping.Wrap;
            t.FontSize = 11.5;
            var caja = new Border
            {
                Background = new SolidColorBrush(_v._roles.SuperficieAlta),
                BorderBrush = new SolidColorBrush(_v._roles.Aviso),
                BorderThickness = new Thickness(0, 0, 0, 0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 10, 0, 0),
                Child = t,
            };
            caja.BorderThickness = new Thickness(3, 0, 0, 0);
            return Mas(caja);
        }

        public UIElement Fin() => _raiz;
    }

    Pagina NuevaPagina(string id) =>
        new(this, _modulos.Buscar(id)?.Descriptor.Nombre ?? id, SubtituloDe(id));


    // ---------- Los iconos de las tarjetas ----------
    //
    // Un juego corto y vetado, con nombre. Corto a propósito: cuarenta tarjetas con
    // cuarenta iconos distintos es ruido, y además cada glifo nuevo es un riesgo de que la
    // fuente no lo tenga y salga una caja. Los de la navegación lateral sí son únicos —hay
    // una prueba—; los de las tarjetas se repiten entre páginas, que está bien: dentro de
    // una página lo que distingue es el título.
    //
    // Todos son nombres de Segoe MDL2 de los de siempre, no rarezas de una versión.
    internal static class Ico
    {
        public const int Ajustes = 0xE713;      // Settings
        public const int Forma = 0xE771;        // Personalize
        public const int Color = 0xE790;        // Color
        public const int Imagen = 0xE91B;       // Photo
        public const int Presentacion = 0xE786; // Slideshow
        public const int Zonas = 0xE8A9;        // GridView
        public const int Biblioteca = 0xE8F1;   // Library
        public const int Apps = 0xECAA;         // AppIconDefault
        public const int Teclado = 0xE765;      // KeyboardClassic
        public const int Inicio = 0xE80F;       // Home
        public const int Perfil = 0xE77B;       // Contact2
        public const int Sonido = 0xE767;       // Volume
        public const int Buscar = 0xE721;       // Search
        public const int Info = 0xE946;         // Info
        public const int Restaurar = 0xE72C;    // Refresh
        public const int Guardar = 0xE74E;      // Save
        public const int Pantallas = 0xE740;    // FullScreen
        public const int Mover = 0xE70F;        // Edit
        public const int Nube = 0xE753;         // Cloud

        // Todos, para poder comprobarlos de una vez.
        public static readonly int[] Todos =
        {
            Ajustes, Forma, Color, Imagen, Presentacion, Zonas, Biblioteca, Apps,
            Teclado, Inicio, Perfil, Sonido, Buscar, Info, Restaurar, Guardar,
            Pantallas, Mover, Nube,
        };
    }

    // Qué icono le toca a una tarjeta según su título. Una tabla y no una elección a mano
    // en cada sitio: cuarenta elecciones a mano salen distintas entre sí sin motivo, y esto
    // además se puede comprobar.
    internal static int IconoDeGrupo(string titulo, int siNoSe = Ico.Ajustes)
    {
        var t = (titulo ?? "").ToLowerInvariant();
        bool Hay(params string[] cs) => cs.Any(x => t.Contains(x, StringComparison.Ordinal));

        // ---------- Las ACCIONES van primero ----------
        //
        // El orden de estas preguntas no es decorativo. "Restaurar el tema" contiene
        // "tema", así que con el color antes se llevaba el icono de color y no el de
        // recargar — y lo que la tarjeta hace es restaurar, no elegir un color. Lo pilló
        // una prueba. Regla: lo que la tarjeta HACE manda sobre de qué TRATA.
        if (Hay("restaur", "volver", "deshacer")) return Ico.Restaurar;
        if (Hay("export", "import", "guardar", "probar", "aplicar")) return Ico.Guardar;

        if (Hay("forma", "posición", "posicion", "modo")) return Ico.Forma;
        if (Hay("color", "tema", "contraste", "paleta")) return Ico.Color;
        if (Hay("fondo", "imagen", "degradado")) return Ico.Imagen;
        if (Hay("presentación", "presentacion", "wallpaper", "animado")) return Ico.Presentacion;
        if (Hay("zona", "widget", "módulo", "modulo", "orden")) return Ico.Zonas;
        if (Hay("bibliotec", "catálogo", "catalogo")) return Ico.Biblioteca;
        if (Hay("app", "carrusel", "fijad")) return Ico.Apps;
        if (Hay("atajo", "tecla", "combinaci")) return Ico.Teclado;
        if (Hay("inicio", "menú", "menu", "arranque", "saludo")) return Ico.Inicio;
        if (Hay("perfil")) return Ico.Perfil;
        if (Hay("sonido", "audio", "volumen")) return Ico.Sonido;
        if (Hay("buscar", "búsqueda", "busqueda", "buscador")) return Ico.Buscar;
        if (Hay("estado", "diagnóstico", "diagnostico", "aviso", "notificaci")) return Ico.Info;
        if (Hay("pantalla", "monitor")) return Ico.Pantallas;
        if (Hay("mover", "arrastr", "visibilidad", "grosor", "margen", "texto", "fuente")) return Ico.Mover;
        if (Hay("movimiento", "animaci", "escena")) return Ico.Forma;
        return siNoSe;
    }

    void RefrescarCambios()
    {
        try
        {
            bool hay = _arbol.HaCambiado();
            if (_sinGuardar != null)
                _sinGuardar.Text = hay ? "Cambios sin guardar" : "";
            if (_botonGuardar != null)
            {
                _botonGuardar.IsEnabled = hay;
                _botonGuardar.Opacity = hay ? 1 : 0.5;
                _botonGuardar.ToolTip = hay ? "Guardar y cerrar" : "No hay nada que guardar";
            }
        }
        catch (Exception ex) { Log.Error("mirar si hay cambios sin guardar", ex); }
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

    internal TextBlock Rotulo(string t, bool titulo = false) => new()
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
        var p = NuevaPagina("apariencia");
        p.Grupo("Forma", IconoDeGrupo("Forma"));
        // La geometría se escribe en Paneles[principal], que es la autoridad desde B5.
        // Las claves equivalentes de "Barra" ya no se leen: se derivan al cargar.
        p.Mas(Opciones("Modo", new[] { "isla", "barra" }, _arbol.TextoDelPanel(Principal, "Modo", "isla"),
                                v => _arbol.PonerDelPanel(Principal, "Modo", v)));
        p.Mas(Opciones("Posición", PanelNube.Posiciones, _arbol.TextoDelPanel(Principal, "Posicion", "arriba"),
                                v => _arbol.PonerDelPanel(Principal, "Posicion", v)));
        // Los cuatro bordes valen en los dos modos, pero no significan lo mismo, y eso
        // no se adivina de un desplegable con cuatro palabras.
        var pista = Rotulo("En «izquierda» y «derecha», el modo barra se vuelve una barra vertical " +
                           "con las zonas apiladas; el modo isla solo se ancla a ese lado.");
        pista.TextWrapping = TextWrapping.Wrap;
        pista.Opacity = 0.55;
        pista.Margin = new Thickness(0, 0, 0, 4);
        p.Mas(pista);
        p.Mas(Opciones("Visibilidad", PanelNube.Visibilidades,
                                _arbol.TextoDelPanel(Principal, "Visibilidad", "siempre"),
                                v => _arbol.PonerDelPanel(Principal, "Visibilidad", v)));
        p.Mas(Deslizador("Grosor", 24, 72, _arbol.NumeroDelPanel(Principal, "Grosor", 38),
                                  v => _arbol.PonerDelPanel(Principal, "Grosor", v)));
        p.Mas(Deslizador("Margen", 0, 32, _arbol.NumeroDelPanel(Principal, "Margen", 8),
                                  v => _arbol.PonerDelPanel(Principal, "Margen", v)));
        p.Mas(Deslizador("Radio de esquinas", 0, 36, _arbol.Numero("Tema", "Radio", 20),
                                  v => _arbol.Poner("Tema", "Radio", v)));

        p.Grupo("Texto", IconoDeGrupo("Texto"));
        var fuentes = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        p.Mas(Opciones("Fuente", fuentes, _arbol.Texto("Tema", "Fuente", "Segoe UI"),
                                v => _arbol.Poner("Tema", "Fuente", v)));
        p.Mas(Deslizador("Tamaño", 9, 22, _arbol.Numero("Tema", "TamanoFuente", 13),
                                  v => _arbol.Poner("Tema", "TamanoFuente", v)));

        p.Grupo("Movimiento", IconoDeGrupo("Movimiento"));
        // "auto" sigue a Windows (Accesibilidad > Efectos visuales > Efectos de
        // animación): quien ya ha pedido menos movimiento en el sistema no tiene que
        // volver a pedirlo aquí. Equilibrado no quita animaciones, las acorta.
        p.Mas(Opciones("Animaciones", new[] { "auto", "completo", "equilibrado", "reducido", "desactivado" },
                                _arbol.Texto("Animaciones", "Nivel", "auto"),
                                v => _arbol.Poner("Animaciones", "Nivel", v)));
        p.Mas(Opciones("Escena de entrada", new[] { "primera-vez", "cada-inicio", "al-cambiar-tema", "nunca" },
                                _arbol.Texto("Animaciones", "Robot", "primera-vez"),
                                v => _arbol.Poner("Animaciones", "Robot", v)));
        // Verla sin tener que reiniciar la nube. No gasta la "primera vez": una vista
        // previa es para decidir si la quieres, no para estrenarla.
        p.Mas(BotonPlano("Ver la escena ahora", () => EscenaArranque.VistaPrevia(), false));
        var pistaEscena = Rotulo("Una nubecita aparece en el centro, saluda y se abre: por dentro de " +
                                 "ella va apareciendo el escritorio ya montado. Al final se recoge y se " +
                                 "mete en la barra. Se salta con Esc o con un clic, y con el movimiento " +
                                 "desactivado no aparece.");
        pistaEscena.TextWrapping = TextWrapping.Wrap;
        pistaEscena.Opacity = 0.55;
        p.Mas(pistaEscena);

        p.Grupo("Reacción al sonido", IconoDeGrupo("Reacción al sonido"));
        p.Mas(Interruptor("La isla respira con lo que suena",
                                   _arbol.Bandera("Barra", "PulsoAudio", true),
                                   v => _arbol.Poner("Barra", "PulsoAudio", v)));
        p.Mas(Interruptor("Las teclas de volumen enseñan el aviso de la isla, no el de Windows",
                                   _arbol.Bandera("Barra", "TeclasVolumen", true),
                                   v => _arbol.Poner("Barra", "TeclasVolumen", v)));

        p.Grupo("Mover la isla", IconoDeGrupo("Mover la isla"));
        p.Mas(Interruptor("Se puede apartar arrastrándola",
                                   _arbol.Bandera("Barra", "Arrastrable", true),
                                   v => _arbol.Poner("Barra", "Arrastrable", v)));
        p.Mas(Deslizador("Segundos antes de volver a su sitio", 0.5, 10,
                                  _arbol.Numero("Barra", "SegundosParaVolver", 2),
                                  v => _arbol.Poner("Barra", "SegundosParaVolver", Math.Round(v, 1))));

        p.Grupo("Pantallas", IconoDeGrupo("Pantallas"));
        p.Mas(Opciones("Monitores", new[] { "todos", "principal" },
                                _arbol.TextoDelPanel(Principal, "Monitores", "todos"),
                                v => _arbol.PonerDelPanel(Principal, "Monitores", v)));
        p.Mas(Interruptor("Cada isla enseña las apps de su pantalla",
                                   _arbol.Bandera("Barra", "AppsPorMonitor", true),
                                   v => _arbol.Poner("Barra", "AppsPorMonitor", v)));
        p.Mas(Interruptor("Esconderse cuando algo ocupa la pantalla entera",
                                   _arbol.Bandera("Barra", "OcultarEnPantallaCompleta", true),
                                   v => _arbol.Poner("Barra", "OcultarEnPantallaCompleta", v)));
        p.Mas(Interruptor("Esconderse también con una ventana maximizada (juegos, vídeos)",
                                   _arbol.Bandera("Barra", "OcultarConMaximizada", false),
                                   v => _arbol.Poner("Barra", "OcultarConMaximizada", v)));
        return p.Fin();
    }

    UIElement SeccionColores()
    {
        var p = NuevaPagina("colores");

        p.Grupo("Temas listos", IconoDeGrupo("Temas listos"));
        var muestras = new WrapPanel();
        foreach (var t in Temas.Predefinidos) muestras.Children.Add(MuestraTema(t));
        p.Mas(muestras);

        p.Grupo("Fondo (con opacidad)", IconoDeGrupo("Fondo (con opacidad)"));
        var selFondo = new SelectorColor { ConOpacidad = true }.Montar();
        selFondo.Valor = Hex(_arbol.Texto("Tema", "Fondo", "#E6000000"), Colors.Black);
        selFondo.Cambiado += v => { _arbol.Poner("Tema", "Fondo", SelectorColor.AHex(v, true)); Cambio(); };
        p.Mas(selFondo);

        p.Grupo("Texto", IconoDeGrupo("Texto"));
        var selTexto = new SelectorColor { ConOpacidad = false }.Montar();
        selTexto.Valor = Hex(_arbol.Texto("Tema", "Texto", "#FFFFFF"), Colors.White);
        selTexto.Cambiado += v => { _arbol.Poner("Tema", "Texto", SelectorColor.AHex(v, false)); Cambio(); };
        p.Mas(selTexto);

        p.Grupo("Acento", IconoDeGrupo("Acento"));
        var selAcento = new SelectorColor { ConOpacidad = false }.Montar();
        selAcento.Valor = Hex(_arbol.Texto("Tema", "Acento", "#7AA2F7"), Colors.CornflowerBlue);
        selAcento.Cambiado += v => { _arbol.Poner("Tema", "Acento", SelectorColor.AHex(v, false)); Cambio(); };
        p.Mas(selAcento);

        p.Mas(Interruptor("Colores desde el wallpaper", _arbol.Bandera("Tema", "DesdeWallpaper", false),
                                   v => _arbol.Poner("Tema", "DesdeWallpaper", v)));
        p.Mas(Interruptor("Borde de acento alrededor de la isla", _arbol.Bandera("Tema", "BordeAcento", true),
                                   v => _arbol.Poner("Tema", "BordeAcento", v)));

        p.Grupo("Mis temas", IconoDeGrupo("Mis temas"));
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(BotonPlano("Exportar", ExportarTema, false));
        fila.Children.Add(BotonPlano("Importar", () => ImportarTema(selFondo, selTexto, selAcento), false));
        fila.Children.Add(BotonPlano("Abrir carpeta", () =>
        {
            Directory.CreateDirectory(Temas.Carpeta);
            Process.Start(new ProcessStartInfo(Temas.Carpeta) { UseShellExecute = true })?.Dispose();
        }, false));
        p.Mas(fila);
        return p.Fin();
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


    // ---------- Volver a construir una página ----------
    //
    // Por id y no por índice: con el registro de módulos el orden de las páginas puede
    // cambiar, y un número escrito a mano recargaría la equivocada. Se conserva la
    // pestaña elegida para que recargar no te saque de donde estabas.
    void RehacerPagina(string id, Func<UIElement> construir)
    {
        int donde = _modulos.Indice(id);
        if (donde < 0 || donde >= _pestanas.Items.Count)
        {
            Log.Info($"no encuentro la página \"{id}\" para recargarla");
            return;
        }
        var i = _pestanas.SelectedIndex;
        ((TabItem)_pestanas.Items[donde]).Content = new ScrollViewer
        {
            Content = construir(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8, 12, 8, 8)
        };
        _pestanas.SelectedIndex = i;
    }

    // La de perfiles se recarga a cada acción: aplicar, duplicar, renombrar o borrar
    // cambia qué tarjetas hay y cuál va marcada como puesta.
    void Rehacer() => RehacerPagina("perfiles", SeccionPerfiles);

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
        var p = NuevaPagina("biblioteca");
        p.Grupo("Biblioteca de widgets", IconoDeGrupo("Biblioteca de widgets"));
        p.Mas(new TextBlock
        {
            Text = "Lo que puedes poner en la nube. Ocultar conserva el sitio del widget; " +
                   "quitar lo devuelve aquí.",
            Foreground = _textoAtenuado,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var biblioteca = new Biblioteca(_arbol, _roles, () => { _escritura.Stop(); _arbol.Guardar(); }, _modulos);
        p.Mas(biblioteca.Construir());
        return p.Fin();
    }

    UIElement SeccionModulos()
    {
        var p = NuevaPagina("modulos");
        p.Grupo("Arrastra los módulos entre columnas para colocarlos", IconoDeGrupo("Arrastra los módulos entre columnas para colocarlos"));

        var rejilla = new Grid();
        for (int i = 0; i < 3; i++) rejilla.ColumnDefinitions.Add(new ColumnDefinition());

        var izq = ListaModulos("Izquierda", _arbol.Lista("Modulos", "Izquierda", "inicio"));
        var cen = ListaModulos("Centro", _arbol.Lista("Modulos", "Centro", "fijadas", "apps", "reloj"));
        var der = ListaModulos("Derecha", _arbol.Lista("Modulos", "Derecha", "cpu", "ram", "bateria", "bandeja", "control", "fecha"));

        Grid.SetColumn(izq.Caja, 0); Grid.SetColumn(cen.Caja, 1); Grid.SetColumn(der.Caja, 2);
        rejilla.Children.Add(izq.Caja); rejilla.Children.Add(cen.Caja); rejilla.Children.Add(der.Caja);
        p.Mas(rejilla);

        void Volcar()
        {
            _arbol.PonerLista("Modulos", "Izquierda", izq.Lista.Items.Cast<string>());
            _arbol.PonerLista("Modulos", "Centro", cen.Lista.Items.Cast<string>());
            _arbol.PonerLista("Modulos", "Derecha", der.Lista.Items.Cast<string>());
            Cambio();
        }
        foreach (var l in new[] { izq, cen, der }) l.AlCambiar = Volcar;

        p.Grupo("Disponibles (doble clic para añadir a la izquierda)", IconoDeGrupo("Disponibles (doble clic para añadir a la izquierda)"));
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
        p.Mas(disponibles);
        p.Mas(new TextBlock
        {
            Text = "Doble clic en un módulo de una columna para quitarlo.",
            Foreground = _textoAtenuado, Margin = new Thickness(0, 6, 0, 0)
        });
        return p.Fin();
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
        var p = NuevaPagina("apps");
        p.Grupo("Iconos de aplicaciones", IconoDeGrupo("Iconos de aplicaciones"));
        p.Mas(Deslizador("Máximo visibles", 3, 30, _arbol.Numero("Barra", "MaxApps", 10),
                                  v => _arbol.Poner("Barra", "MaxApps", Math.Round(v))));
        p.Mas(Interruptor("Enseñar solo las apps del escritorio virtual actual",
                                   _arbol.Bandera("Barra", "AppsDelEscritorio", false),
                                   v => _arbol.Poner("Barra", "AppsDelEscritorio", v)));

        p.Grupo("Apps fijadas en la barra", IconoDeGrupo("Apps fijadas en la barra"));
        p.Mas(ListaEditable("Barra", "AppsFijadas",
            "Siempre a la vista, abiertas o no, y en este orden. Con el módulo \"fijadas\" puesto " +
            "(viene de serie, junto al reloj) se ven también con la isla recogida, sin acercar el ratón; " +
            "si lo quitas, vuelven al carrusel. Una ruta de ejecutable por línea, o " +
            "shell:AppsFolder\\<id> para las de la Store. Más cómodo: clic derecho sobre el icono " +
            "de una app en la isla."));
        p.Grupo("Agrupar", Ico.Zonas, "Varias ventanas del mismo programa, en un solo icono");
        p.Grupo("Agrupar", Ico.Zonas, "Varias ventanas del mismo programa, en un solo icono");
        p.Mas(Interruptor("Agrupar las ventanas del mismo programa",
                                   _arbol.Bandera("Barra", "AgruparApps", true),
                                   v => _arbol.Poner("Barra", "AgruparApps", v)));
        return p.Fin();
    }

    UIElement SeccionAtajos()
    {
        var p = NuevaPagina("atajos");
        p.Grupo("Vista general de ventanas", IconoDeGrupo("Vista general de ventanas"));

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
        p.Mas(campo);
        p.Mas(aviso);
        p.Grupo("Ajustar ventanas a los bordes", IconoDeGrupo("Ajustar ventanas a los bordes"));
        p.Mas(Interruptor("Arrastrar una ventana a un borde la coloca ahí",
                                   _arbol.Bandera("Barra", "AjustarAlArrastrar", true),
                                   v => _arbol.Poner("Barra", "AjustarAlArrastrar", v)));
        var pistaAjuste = Rotulo("Al borde izquierdo o derecho, media pantalla; a una esquina, un cuarto; " +
                                 "arriba, maximizar. Antes de soltar se ve dónde va a caer. Respeta el área " +
                                 "de trabajo, así que con «Reservar espacio» puesto no se mete debajo de la nube.");
        pistaAjuste.TextWrapping = TextWrapping.Wrap;
        pistaAjuste.Opacity = 0.55;
        p.Mas(pistaAjuste);

        p.Mas(new TextBlock
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

        return p.Fin();
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
        var p = NuevaPagina("inicio");
        p.Grupo("Menú Inicio", IconoDeGrupo("Menú Inicio"));
        p.Mas(Interruptor("La tecla Windows abre el menú de la nube",
                                   _arbol.Bandera("Inicio", "TeclaWindows", true),
                                   v => _arbol.Poner("Inicio", "TeclaWindows", v)));
        p.Mas(Interruptor("Buscar también en archivos (índice de Windows)",
                                   _arbol.Bandera("Inicio", "BuscarArchivos", true),
                                   v => _arbol.Poner("Inicio", "BuscarArchivos", v)));
        p.Mas(Interruptor("Saludo en la cabecera",
                                   _arbol.Bandera("Inicio", "Saludo", true),
                                   v => _arbol.Poner("Inicio", "Saludo", v)));

        p.Grupo("Buscador web", IconoDeGrupo("Buscador web"));
        var web = new TextBox
        {
            Text = _arbol.Texto("Inicio", "BuscadorWeb", "https://www.google.com/search?q="),
            Height = 28, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        web.TextChanged += (_, _) => { _arbol.Poner("Inicio", "BuscadorWeb", web.Text); Cambio(); };
        p.Mas(web);

        p.Grupo("Apps fijadas", IconoDeGrupo("Apps fijadas"));
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

        p.Mas(lista);
        p.Mas(nueva);
        p.Mas(fila);
        return p.Fin();
    }

    UIElement SeccionWallpaper()
    {
        var p = NuevaPagina("wallpaper");
        p.Grupo("Wallpaper Engine", IconoDeGrupo("Wallpaper Engine"));

        var ruta = new TextBox
        {
            Text = _arbol.Texto("Wallpaper", "Ruta", "") ?? "",
            Height = 28, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta,
            Foreground = _texto, BorderThickness = new Thickness(0)
        };
        ruta.TextChanged += (_, _) => { _arbol.Poner("Wallpaper", "Ruta", ruta.Text); Cambio(); };
        p.Mas(ruta);

        var detectada = WallpaperEngine.Exe;
        p.Mas(new TextBlock
        {
            Text = detectada != null ? "Detectado solo en: " + detectada : "No se ha detectado Wallpaper Engine",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        if (detectada != null)
            p.Mas(BotonPlano("Usar la ruta detectada", () => ruta.Text = detectada, false));
        p.Mas(BotonPlano("Elegir el ejecutable…", () =>
        {
            var d = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Dónde está Wallpaper Engine",
                Filter = "Wallpaper Engine (wallpaper64.exe;wallpaper32.exe)|wallpaper64.exe;wallpaper32.exe|Programas (*.exe)|*.exe",
                CheckFileExists = true,
            };
            if (d.ShowDialog() == true) ruta.Text = d.FileName;
        }, false));

        p.Grupo("Paleta y monitor", Ico.Color, "De qué pantalla se leen los colores del wallpaper");
        p.Mas(Deslizador("Monitor del que se leen los colores", 0, 3,
                                  _arbol.Numero("Wallpaper", "Monitor", 0),
                                  v => _arbol.Poner("Wallpaper", "Monitor", Math.Round(v))));
        p.Mas(Interruptor("Enviar mi color de acento al wallpaper",
                                   _arbol.Bandera("Tema", "EnviarColorAlWallpaper", false),
                                   v => _arbol.Poner("Tema", "EnviarColorAlWallpaper", v)));

        // ================= La integración =================
        p.Grupo("Integración con Wallpaper Engine", IconoDeGrupo("Integración con Wallpaper Engine"));

        var pista = Rotulo("Con esto puesto, NubeBar puede abrir Wallpaper Engine al arrancar, cargar el " +
                           "wallpaper que elijas y esperar a que esté listo antes de que la nube se abra: " +
                           "así la escena de inicio va descubriendo el escritorio con el fondo ya puesto. " +
                           "Está desactivada de serie porque significa abrir y dar órdenes a un programa que " +
                           "no es NubeBar.");
        pista.TextWrapping = TextWrapping.Wrap;
        pista.Opacity = 0.6;
        p.Mas(pista);

        p.Mas(Interruptor("Activar la integración",
                                   _arbol.Bandera("Wallpaper", "Integrar", false),
                                   v => _arbol.Poner("Wallpaper", "Integrar", v)));
        p.Mas(Interruptor("Iniciar Wallpaper Engine con NubeBar",
                                   _arbol.Bandera("Wallpaper", "IniciarConNubeBar", false),
                                   v => _arbol.Poner("Wallpaper", "IniciarConNubeBar", v)));

        // ---------- Qué cargar ----------
        p.Grupo("Qué cargar", Ico.Presentacion, "El wallpaper o la playlist que NubeBar le pedirá");
        p.Mas(Rotulo("Wallpaper o playlist"));

        var elegido = new TextBox
        {
            Text = _arbol.Texto("Wallpaper", "WallpaperElegido", "") ?? "",
            Height = 28, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta, Foreground = _texto, BorderThickness = new Thickness(0)
        };
        elegido.TextChanged += (_, _) => { _arbol.Poner("Wallpaper", "WallpaperElegido", elegido.Text); Cambio(); };
        p.Mas(elegido);
        p.Mas(BotonPlano("Elegir el wallpaper…", () =>
        {
            // Se abre en la carpeta de wallpapers de Wallpaper Engine si se sabe dónde
            // está, pero no se adivina el fichero: lo elige el usuario.
            var inicio = WallpaperEngine.Carpeta is { } dir
                ? System.IO.Path.Combine(dir, "projects", "myprojects") : null;
            var d = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Qué wallpaper cargar",
                Filter = "Wallpaper (project.json;*.pkg)|project.json;*.pkg|Todos|*.*",
                InitialDirectory = inicio != null && System.IO.Directory.Exists(inicio) ? inicio : "",
            };
            if (d.ShowDialog() == true) elegido.Text = d.FileName;
        }, false));

        var playlist = new TextBox
        {
            Text = _arbol.Texto("Wallpaper", "PlaylistElegida", "") ?? "",
            Height = 28, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta, Foreground = _texto, BorderThickness = new Thickness(0)
        };
        playlist.TextChanged += (_, _) => { _arbol.Poner("Wallpaper", "PlaylistElegida", playlist.Text); Cambio(); };
        p.Mas(Rotulo("O el nombre de una playlist (manda sobre el wallpaper)"));
        p.Mas(playlist);

        // ---------- Dónde ----------
        var cuantos = Math.Max(1, Monitores.Todos().Count);
        var destinos = new List<string> { Integracion.TodosLosMonitores };
        for (int i = 0; i < cuantos; i++) destinos.Add(i.ToString());
        p.Grupo("Dónde y hasta cuándo", Ico.Pantallas, "El monitor de destino, qué hacer al salir y cuánto espera la nube");
        p.Mas(Opciones("Monitor de destino", destinos.ToArray(),
                                _arbol.Texto("Wallpaper", "MonitorDestino", Integracion.TodosLosMonitores),
                                v => _arbol.Poner("Wallpaper", "MonitorDestino", v)));

        // ---------- Al salir ----------
        p.Mas(Opciones("Al salir de NubeBar", new[] { "nada", "pausar", "cerrar" },
                                _arbol.Texto("Wallpaper", "AlSalir", "nada"),
                                v => _arbol.Poner("Wallpaper", "AlSalir", v)));
        var pistaSalir = Rotulo("Solo se aplica a lo que haya abierto NubeBar. Si Wallpaper Engine ya estaba " +
                                "abierto cuando arrancaste, no se toca al salir: no es nuestro.");
        pistaSalir.TextWrapping = TextWrapping.Wrap;
        pistaSalir.Opacity = 0.55;
        p.Mas(pistaSalir);

        // ---------- La espera ----------
        p.Mas(Deslizador($"Esperar al fondo (segundos)",
                                  Integracion.MinEspera, Integracion.MaxEspera,
                                  _arbol.Numero("Wallpaper", "SegundosEspera", 8),
                                  v => _arbol.Poner("Wallpaper", "SegundosEspera", Math.Round(v))));
        var pistaEspera = Rotulo("Lo que la nube espera, como mucho, antes de abrirse. Si se agota, la escena " +
                                 "se abre igual con el escritorio que haya: la pantalla no se queda en negro.");
        pistaEspera.TextWrapping = TextWrapping.Wrap;
        pistaEspera.Opacity = 0.55;
        p.Mas(pistaEspera);

        // ---------- Los dos botones ----------
        var resultado = new TextBlock
        {
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        p.Grupo("Probar", Ico.Guardar, "Prueba con lo que hay escrito ahora, sin cerrar los ajustes");
        p.Mas(BotonPlano("Probar wallpaper", () =>
        {
            // Se prueba con lo que hay ESCRITO ahora, no con lo guardado: si no, habría
            // que cerrar los ajustes para poder probar un cambio.
            var o = new OpcionesWallpaper
            {
                Ruta = ruta.Text,
                Integrar = true,
                IniciarConNubeBar = _arbol.Bandera("Wallpaper", "IniciarConNubeBar", false),
                WallpaperElegido = elegido.Text,
                PlaylistElegida = playlist.Text,
                MonitorDestino = _arbol.Texto("Wallpaper", "MonitorDestino", Integracion.TodosLosMonitores),
                SegundosEspera = _arbol.Numero("Wallpaper", "SegundosEspera", 8),
            };
            var p = Integracion.Probar(o);
            resultado.Text = (p.Ok ? "✓ " : "✗ ") + p.Mensaje;
            resultado.Foreground = p.Ok ? _texto : _textoAtenuado;
        }, false));

        p.Mas(BotonPlano("Ver escena de inicio", () =>
        {
            // La vista previa no espera a nadie: es para ver la escena, no para medir la
            // integración. Si quieres probar la espera, reinicia NubeBar.
            EscenaArranque.VistaPrevia();
            resultado.Text = Animacion.Nivel == NivelAnimacion.Desactivado
                ? "El movimiento está desactivado, así que no hay escena que ver."
                : "Escena lanzada. Para probar la espera al fondo de verdad, reinicia NubeBar.";
        }, false));

        p.Mas(resultado);

        // ---------- El estado, que es lo que hace falta para diagnosticar ----------
        var estadoWE = new TextBlock
        {
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0), Opacity = 0.8
        };
        estadoWE.Text =
            $"Ahora mismo: {(WallpaperEngine.Abierto ? "Wallpaper Engine está abierto" : "Wallpaper Engine no está abierto")}. " +
            $"Última vez que NubeBar lo tocó: {Integracion.Ahora}. " +
            $"Pantallas detectadas: {Monitores.Todos().Count}.";
        p.Grupo("Estado de la integración", Ico.Info);
        p.Mas(estadoWE);

        return p.Fin();
    }

    // ================= Perfiles de escritorio =================
    //
    // Una tarjeta por perfil, con lo que cambia y los botones. Todo pinta con los roles
    // del tema (`_superficie`, `_elegido`, `_acento`, `_textoAtenuado`): ni una mezcla de
    // color a mano, que es la regla desde la Fase A.
    UIElement SeccionPerfiles()
    {
        var pag = NuevaPagina("perfiles");
        Perfiles.CrearLosDeSerie();

        var estado = Perfiles.LeerEstado();
        var todos = Perfiles.Todos();
        var arbol = ConfigArbol.Cargar();

        pag.Grupo("Perfiles de escritorio", IconoDeGrupo("Perfiles de escritorio"));
        var intro = Rotulo("Un perfil guarda el tema, el nivel de animación, el panel, los widgets, las " +
                           "notificaciones, el No molestar y el fondo. Aplicarlo lo cambia todo de una vez " +
                           "y con una sola escritura; lo que el perfil no menciona se queda como lo tengas.");
        intro.TextWrapping = TextWrapping.Wrap;
        intro.Opacity = 0.6;
        pag.Mas(intro);

        // ---------- El aviso de deshacer, arriba y solo si hay algo que deshacer ----------
        if (Perfiles.HayQueDeshacer)
        {
            var aviso = new Border
            {
                Background = _elegido,
                BorderBrush = _acento,
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 8, 0, 4),
            };
            var fila = new StackPanel();
            var t = Rotulo("Hay un perfil aplicado hace poco. Puedes volver a como estaba antes.");
            t.TextWrapping = TextWrapping.Wrap;
            fila.Children.Add(t);
            var bots = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            bots.Children.Add(BotonPlano("Deshacer", () =>
            {
                if (Perfiles.Deshacer()) Rehacer();
            }, true));
            fila.Children.Add(bots);
            aviso.Child = fila;
            pag.Mas(aviso);
        }

        if (todos.Count == 0)
            pag.Mas(Rotulo("No hay perfiles. Importa uno o duplica alguno de los de serie."));

        pag.Grupo("Tus perfiles", Ico.Perfil,
                  "Cada tarjeta es un perfil: lo que cambia y qué puedes hacer con él");
        foreach (var p in todos)
            pag.Mas(TarjetaDePerfil(p, estado, arbol));

        // ---------- Importar ----------
        pag.Grupo("Importar", IconoDeGrupo("Importar"));
        var resultado = Rotulo("");
        resultado.TextWrapping = TextWrapping.Wrap;

        var filaImp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        filaImp.Children.Add(BotonPlano("Importar un perfil…", () =>
        {
            var d = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Qué perfil importar",
                Filter = "Perfil de NubeBar (*.json)|*.json|Todos|*.*",
                CheckFileExists = true,
            };
            if (d.ShowDialog() != true) return;

            var imp = Perfiles.Importar(d.FileName, Perfiles.Todos());
            if (imp.Perfil == null)
            {
                resultado.Text = "✗ No se pudo importar: " + imp.Veredicto.Resumen;
                return;
            }
            if (!Perfiles.Guardar(imp.Perfil))
            {
                resultado.Text = "✗ Se validó pero no se pudo guardar. Mira el log.";
                return;
            }
            resultado.Text = $"✓ Importado como «{imp.Perfil.Nombre}»." +
                             (imp.Veredicto.Ignoradas.Count > 0
                                ? $" Se ignoraron {imp.Veredicto.Ignoradas.Count} clave(s) que un perfil no puede tocar: " +
                                  string.Join(", ", imp.Veredicto.Ignoradas)
                                : "");
            Rehacer();
        }, false));
        pag.Mas(filaImp);
        pag.Mas(resultado);

        var nota = Rotulo("Los perfiles se guardan en %APPDATA%\\NubeBar\\perfiles, uno por fichero y con su " +
                          "versión. Un perfil de una versión más nueva se rechaza en vez de leerse a medias.");
        nota.TextWrapping = TextWrapping.Wrap;
        nota.Opacity = 0.5;
        nota.Margin = new Thickness(0, 10, 0, 0);
        pag.Grupo("Dónde se guardan", Ico.Info);
        pag.Mas(nota);
        return pag.Fin();
    }

    UIElement TarjetaDePerfil(Perfil p, Perfiles.EstadoPerfiles estado, ConfigArbol arbol)
    {
        bool activo = estado.Activo == p.Id;
        bool modificado = activo && Perfiles.Modificado(estado.HuellaAlAplicar, Perfiles.Huella(p, arbol));

        var marco = new Border
        {
            // El filo va SIEMPRE puesto y lo que cambia es su color, igual que en las
            // tarjetas de la vista general: con el grosor a 0 y a 1,4 según el estado, el
            // contenido se movía un píxel y medio al cambiar de perfil activo.
            Background = activo ? _elegido : _superficie,
            BorderBrush = activo ? _acento : Brushes.Transparent,
            BorderThickness = new Thickness(1.4),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 8, 0, 0),
        };

        var col = new StackPanel();

        // ---------- Cabecera: nombre y si está puesto ----------
        var cab = new StackPanel { Orientation = Orientation.Horizontal };
        cab.Children.Add(new TextBlock
        {
            Text = p.Nombre,
            Foreground = activo ? _acentoLegible : _texto,
            FontSize = 14,
            FontWeight = activo ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (activo)
            cab.Children.Add(new TextBlock
            {
                Text = modificado ? "  ·  puesto (modificado)" : "  ·  puesto",
                Foreground = _textoAtenuado, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            });
        if (p.Protegido)
            cab.Children.Add(new TextBlock
            {
                Text = "  ·  de serie", Foreground = _textoAtenuado, FontSize = 11,
                Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center,
            });
        col.Children.Add(cab);

        // ---------- Descripción ----------
        if (p.Descripcion.Length > 0)
        {
            var d = new TextBlock
            {
                Text = p.Descripcion,
                Foreground = _textoAtenuado, FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0),
            };
            col.Children.Add(d);
        }

        // ---------- Qué cambia ----------
        var que = ResumenDelPerfil(p);
        if (que.Length > 0)
            col.Children.Add(new TextBlock
            {
                Text = que,
                Foreground = _textoAtenuado, FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
                Margin = new Thickness(0, 7, 0, 0),
            });

        // ---------- Los botones ----------
        var b1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-8, 11, 0, 0) };
        b1.Children.Add(BotonPlano(activo ? "Volver a aplicar" : "Aplicar", () =>
        {
            var r = Perfiles.Aplicar(p);
            Rehacer();
            if (!r.Ok) Log.Info("perfiles: no se aplicó — " + r.Detalle);
        }, !activo));

        // "Vista previa" es aplicar de verdad y poder volver: un perfil cambia el tema, el
        // panel y los widgets de TODA la interfaz, así que una vista previa que no aplique
        // tendría que simular el escritorio entero. Aplicar con deshacer a un clic es más
        // honesto y es lo que el usuario quiere comprobar.
        b1.Children.Add(BotonPlano("Vista previa", () =>
        {
            var r = Perfiles.Aplicar(p);
            Rehacer();
            if (!r.Ok) Log.Info("perfiles: la vista previa no se aplicó — " + r.Detalle);
        }, false));

        b1.Children.Add(BotonPlano("Duplicar", () =>
        {
            var todos = Perfiles.Todos();
            var copia = p.Copia(Perfiles.IdLibre(p.Nombre, todos.Select(x => x.Id)),
                                Perfiles.NombreLibre(p.Nombre, todos.Select(x => x.Nombre)));
            if (Perfiles.Guardar(copia)) Rehacer();
        }, false));
        col.Children.Add(b1);

        // ---------- Renombrar, en su propia fila para que quepa el campo ----------
        var b2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-8, 6, 0, 0) };

        var nuevoNombre = new TextBox
        {
            Text = p.Nombre,
            Width = 170, Height = 28,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta, Foreground = _texto, BorderThickness = new Thickness(0),
        };
        b2.Children.Add(nuevoNombre);
        b2.Children.Add(BotonPlano("Renombrar", () =>
        {
            var n = (nuevoNombre.Text ?? "").Trim();
            if (n.Length == 0 || n == p.Nombre) return;
            // El id NO cambia al renombrar: es lo que permite saber qué perfil está puesto
            // después de cambiarle el nombre.
            p.Nombre = n;
            if (Perfiles.Guardar(p)) Rehacer();
        }, false));

        b2.Children.Add(BotonPlano("Exportar…", () =>
        {
            var d = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Dónde guardar el perfil",
                Filter = "Perfil de NubeBar (*.json)|*.json",
                FileName = p.Id + ".json",
            };
            if (d.ShowDialog() == true) Perfiles.Exportar(p, d.FileName);
        }, false));

        if (p.Protegido)
            b2.Children.Add(BotonPlano("Restaurar", () =>
            {
                if (Perfiles.Restaurar(p.Id) != null) Rehacer();
            }, false));
        else
            b2.Children.Add(BotonPlano("Eliminar", () =>
            {
                if (Perfiles.Borrar(p)) Rehacer();
            }, false));

        col.Children.Add(b2);

        marco.Child = col;
        return marco;
    }

    // Una línea con lo que el perfil toca. Se saca del propio perfil y no de una lista
    // escrita a mano: así un perfil importado dice la verdad de lo que lleva.
    static string ResumenDelPerfil(Perfil p)
    {
        var trozos = new List<string>();
        var a = p.Ajustes;

        if (a["Tema"] is JsonObject tema && tema.Count > 0)
            trozos.Add($"tema ({tema.Count} valores)");
        if (a["Animaciones"]?["Nivel"] is { } niv)
            trozos.Add("animación: " + niv.GetValue<string>());
        if (a["Panel"] is JsonObject panel && panel.Count > 0)
        {
            var modo = panel["Modo"]?.GetValue<string>();
            var pos = panel["Posicion"]?.GetValue<string>();
            trozos.Add("panel" + (modo != null ? " " + modo : "") + (pos != null ? " " + pos : ""));
        }
        if (a["Modulos"] is JsonObject mods && mods.Count > 0)
        {
            int cuantos = 0;
            foreach (var zona in new[] { "Izquierda", "Centro", "Derecha" })
                if (mods[zona] is JsonArray arr) cuantos += arr.Count;
            int ocultos = mods["Ocultos"] is JsonArray oc ? oc.Count : 0;
            trozos.Add($"widgets ({cuantos}" + (ocultos > 0 ? $", {ocultos} ocultos" : "") + ")");
        }
        if (a["Fondo"]?["Tipo"] is { } tipo)
            trozos.Add("fondo: " + tipo.GetValue<string>());
        if (a["Barra"] is JsonObject barra && barra.Count > 0)
        {
            if (barra["Notificaciones"] is { } n)
                trozos.Add("notificaciones: " + (n.GetValue<bool>() ? "sí" : "no"));
            if (barra["PulsoAudio"] is { } pu)
                trozos.Add("pulso de audio: " + (pu.GetValue<bool>() ? "sí" : "no"));
        }
        if (p.NoMolestar is { } nm)
            trozos.Add("No molestar: " + (nm ? "puesto" : "quitado"));

        return trozos.Count == 0 ? "" : "Cambia " + string.Join(" · ", trozos) + ".";
    }

    // ================= Fondo de escritorio =================
    //
    // Todo pinta con los roles del tema. El selector de monitor trabaja con la CLAVE del
    // monitor (`Monitores.Info.Clave`), no con su índice: un índice cambia al reconectar
    // una pantalla y acabarías editando el fondo de la otra.
    string _monitorDelFondo = "";          // "" = todos los monitores

    UIElement SeccionFondo()
    {
        var p = NuevaPagina("fondo");
        p.Grupo("Monitor", Ico.Pantallas, "A qué pantalla se refiere todo lo de abajo");

        var pantallas = Monitores.Todos();
        bool porMonitor = _monitorDelFondo.Length > 0;

        // ---------- A qué pantalla se refiere lo de abajo ----------
        var nombres = new List<string> { "Todos los monitores" };
        var claves = new List<string> { "" };
        for (int i = 0; i < pantallas.Count; i++)
        {
            var m = pantallas[i];
            bool vertical = m.Alto > m.Ancho;
            nombres.Add($"Pantalla {i + 1}{(m.EsPrincipal ? " (principal)" : "")} · " +
                        $"{m.Ancho}x{m.Alto}{(vertical ? " vertical" : "")} · {m.Dpi} ppp");
            claves.Add(m.Clave);
        }
        int elegida = Math.Max(0, claves.IndexOf(_monitorDelFondo));
        p.Mas(Opciones("Editar el fondo de", nombres.ToArray(), nombres[elegida], v =>
        {
            int i = nombres.IndexOf(v);
            _monitorDelFondo = i >= 0 ? claves[i] : "";
            RehacerFondo();
        }));

        if (porMonitor)
        {
            var pista = Rotulo("Lo que no pongas aquí lo hereda del fondo de todos los monitores. " +
                               "«Usar este fondo en todos los monitores» borra lo propio de esta pantalla.");
            pista.TextWrapping = TextWrapping.Wrap;
            pista.Opacity = 0.55;
            p.Mas(pista);
            p.Mas(BotonPlano("Usar este fondo en todos los monitores", () =>
            {
                QuitarFondoDeMonitor(_monitorDelFondo);
                _monitorDelFondo = "";
                RehacerFondo();
            }, false));
        }

        // ---------- El tipo ----------
        string tipo = LeerFondo("Tipo", "ninguno");
        var etiquetas = new Dictionary<string, string>
        {
            ["ninguno"] = "No gestionar el fondo",
            ["solido"] = "Color sólido",
            ["degradado"] = "Degradado",
            ["imagen"] = "Imagen",
            ["presentacion"] = "Presentación de imágenes",
            ["animado"] = "Wallpaper Engine",
        };
        var visibles = Fondos.Tipos.Select(t => etiquetas[t]).ToArray();
        var actual = etiquetas.TryGetValue(tipo, out var e) ? e : etiquetas["ninguno"];
        p.Grupo("Tipo de fondo", Ico.Imagen, "Qué clase de fondo pone NubeBar");
        p.Mas(Opciones("Tipo", visibles, actual, v =>
        {
            var clave = etiquetas.First(p => p.Value == v).Key;
            EscribirFondo("Tipo", clave);
            RehacerFondo();
        }));

        // ---------- Vista previa ----------
        p.Grupo("Vista previa", Ico.Buscar, "Lo que se va a ver en el escritorio");
        p.Mas(VistaPreviaDelFondo(tipo));

        // ---------- Lo propio de cada tipo ----------
        p.Grupo("Ajustes de este tipo", Ico.Forma, "Los controles propios del fondo elegido");
        switch (tipo)
        {
            case "solido":
                p.Mas(Rotulo("Color"));
                p.Mas(CampoDeColorDelFondo("Color1", "#1A1B26"));
                break;

            case "degradado":
                p.Mas(Rotulo("De este color"));
                p.Mas(CampoDeColorDelFondo("Color1", "#0B0D14"));
                p.Mas(Rotulo("A este otro"));
                p.Mas(CampoDeColorDelFondo("Color2", "#7AA2F7"));
                p.Mas(Deslizador("Ángulo", 0, 360, LeerNumFondo("Angulo", 135),
                                          v => { EscribirFondoNum("Angulo", Math.Round(v)); }, 5));
                break;

            case "imagen":
                p.Mas(Rotulo("Imagen"));
                p.Mas(CampoDeRutaDelFondo("Ruta", false));
                break;

            case "presentacion":
                p.Mas(Rotulo("Carpeta con las imágenes"));
                p.Mas(CampoDeRutaDelFondo("CarpetaPresentacion", true));
                var cuantas = Fondos.ImagenesDe(LeerFondo("CarpetaPresentacion", "")).Count;
                var info = Rotulo(cuantas == 0
                    ? "No encuentro imágenes en esa carpeta."
                    : $"{cuantas} imagen(es) encontradas.");
                info.Opacity = 0.6;
                p.Mas(info);
                p.Mas(Deslizador("Cambiar cada (segundos)", Fondos.MinSegundos, 3600,
                                          LeerNumFondo("SegundosPresentacion", 300),
                                          v => EscribirFondoNum("SegundosPresentacion", Math.Round(v)), 5));
                p.Mas(Interruptor("Orden aleatorio", LeerBanderaFondo("Aleatorio", false),
                                           v => EscribirFondoBool("Aleatorio", v)));
                p.Mas(BotonPlano("Pasar a la siguiente ahora", () => _ = GestorFondos.Avanzar(), false));
                break;

            case "animado":
                var we = Rotulo(Fondos.AnimadoDisponible
                    ? "El fondo animado lo gestiona la integración con Wallpaper Engine, que está en su " +
                      "propia sección: ahí se elige el wallpaper, el monitor y qué hacer al salir. Aquí no " +
                      "se duplica nada de eso."
                    : "No encuentro Wallpaper Engine. Los demás tipos de fondo siguen funcionando; para " +
                      "usar este, instálalo o indica su ruta en la sección Wallpaper.");
                we.TextWrapping = TextWrapping.Wrap;
                p.Mas(we);
                p.Mas(BotonPlano("Ir a los ajustes de Wallpaper Engine", () =>
                {
                    int donde = _modulos.Indice("wallpaper");
                    if (donde >= 0 && donde < _pestanas.Items.Count) _pestanas.SelectedIndex = donde;
                }, false));
                break;
        }

        // ---------- El ajuste, para los tipos que son una imagen ----------
        if (tipo is "imagen" or "presentacion")
        {
            var ajustes = new Dictionary<string, string>
            {
                ["rellenar"] = "Rellenar (recorta, no deforma)",
                ["ajustar"] = "Ajustar (cabe entera, no deforma)",
                ["centrar"] = "Centrar (tamaño original)",
                ["extender"] = "Extender (una imagen en todas las pantallas)",
                ["mosaico"] = "Mosaico (se repite)",
            };
            var aj = LeerFondo("Ajuste", "rellenar");
            p.Grupo("Cómo encaja la imagen", Ico.Pantallas, "Rellenar recorta; ajustar deja la imagen entera. Ninguno deforma.");
            p.Mas(Opciones("Cómo encaja", ajustes.Values.ToArray(),
                ajustes.TryGetValue(aj, out var va) ? va : ajustes["rellenar"],
                v => EscribirFondo("Ajuste", ajustes.First(p => p.Value == v).Key)));
        }

        // ---------- Los botones ----------
        var estado = new TextBlock
        {
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-8, 12, 0, 0) };
        fila.Children.Add(BotonPlano("Aplicar el fondo ahora", () =>
        {
            // Se guarda primero —una escritura atómica— y se aplica después desde lo
            // guardado: así lo que se ve es exactamente lo que queda en el config.
            _arbol.Guardar();
            var cfg = Config.Cargar()?.Fondo;
            if (cfg == null) { estado.Text = "✗ No pude leer la configuración."; return; }
            GestorFondos.GuardarInstantanea();
            var r = GestorFondos.AplicarAsync(cfg, forzar: true).GetAwaiter().GetResult();
            estado.Text = (r.Ok ? "✓ " : "✗ ") + r.Detalle;
        }, true));

        fila.Children.Add(BotonPlano("Volver al fondo anterior", () =>
        {
            estado.Text = GestorFondos.RestaurarAnterior()
                ? "✓ Puesto el fondo que había antes de que NubeBar lo tocara."
                : "No hay fondo anterior guardado (NubeBar no ha cambiado el tuyo todavía).";
        }, false));
        p.Grupo("Aplicar", Ico.Guardar, "Estos botones son de esta página: el fondo se pone al momento");
        p.Mas(fila);
        p.Mas(estado);

        // ---------- Qué hay puesto ahora mismo ----------
        var ahora = new TextBlock
        {
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0), Opacity = 0.75
        };
        var puestas = Fondos.LoQueHayPuesto();
        ahora.Text =
            $"Estado: tipo «{(etiquetas.TryGetValue(tipo, out var et) ? et : tipo)}». " +
            $"{pantallas.Count} pantalla(s) detectada(s). " +
            (puestas.Count > 0
                ? "Windows tiene fondo en " + puestas.Count + " de ellas. "
                : "No he podido leer el fondo actual de Windows. ") +
            (GestorFondos.Corriendo ? "La presentación está en marcha. " : "") +
            (GestorFondos.HayInstantanea ? "Hay un fondo anterior guardado." : "");
        p.Grupo("Estado", Ico.Info);
        p.Mas(ahora);
        return p.Fin();
    }

    void RehacerFondo() => RehacerPagina("fondo", SeccionFondo);

    // ---------- Leer y escribir, con la herencia del monitor ----------
    //
    // Si estás editando una pantalla concreta, se lee lo suyo y si no lo tiene, lo
    // general. Escribir siempre escribe en lo que estés editando.
    string LeerFondo(string clave, string porDefecto)
    {
        if (_monitorDelFondo.Length > 0 &&
            _arbol.TextoDeFondoDeMonitor(_monitorDelFondo, clave) is { } suyo) return suyo;
        return _arbol.Texto("Fondo", clave, porDefecto);
    }

    double LeerNumFondo(string clave, double porDefecto) => _arbol.Numero("Fondo", clave, porDefecto);
    bool LeerBanderaFondo(string clave, bool porDefecto) => _arbol.Bandera("Fondo", clave, porDefecto);

    void EscribirFondo(string clave, string valor)
    {
        if (_monitorDelFondo.Length > 0) _arbol.PonerEnFondoDeMonitor(_monitorDelFondo, clave, valor);
        else _arbol.Poner("Fondo", clave, valor);
        Cambio();
    }

    // El ángulo, el intervalo, el aleatorio y la paleta son del fondo entero y no de una
    // pantalla: un degradado con un ángulo por monitor no es una función, es un enredo.
    void EscribirFondoNum(string clave, double valor) { _arbol.Poner("Fondo", clave, valor); Cambio(); }
    void EscribirFondoBool(string clave, bool valor) { _arbol.Poner("Fondo", clave, valor); Cambio(); }

    void QuitarFondoDeMonitor(string clave) { _arbol.QuitarFondoDeMonitor(clave); Cambio(); }

    // ---------- Los campos ----------
    //
    // El selector de color es el MISMO que el de la sección Colores (`SelectorColor`
    // montado en línea, no un diálogo), para que elegir un color se haga igual en toda la
    // ventana. Lo que se guarda va sin opacidad: un fondo de escritorio translúcido no
    // significa nada, debajo no hay nada.
    UIElement CampoDeColorDelFondo(string clave, string porDefecto)
    {
        var fila = new StackPanel();
        var sel = new SelectorColor { ConOpacidad = false }.Montar();
        sel.Valor = Paleta.Leer(LeerFondo(clave, porDefecto), Colors.Black);
        sel.Cambiado += v => EscribirFondo(clave, SelectorColor.AHex(v, false));
        fila.Children.Add(sel);
        return fila;
    }

    UIElement CampoDeRutaDelFondo(string clave, bool carpeta)
    {
        var fila = new StackPanel();
        var caja = new TextBox
        {
            Text = LeerFondo(clave, ""), Height = 28,
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _superficieAlta, Foreground = _texto, BorderThickness = new Thickness(0),
        };
        caja.TextChanged += (_, _) => EscribirFondo(clave, caja.Text);
        fila.Children.Add(caja);

        var aviso = Rotulo("");
        aviso.Opacity = 0.6;
        void Revisar()
        {
            var t = caja.Text;
            if (string.IsNullOrWhiteSpace(t)) { aviso.Text = ""; return; }
            bool hay = carpeta ? Directory.Exists(t) : File.Exists(t);
            aviso.Text = hay ? "" : (carpeta ? "Esa carpeta no existe ahora mismo."
                                             : "Ese archivo no existe ahora mismo. El fondo actual se queda puesto.");
        }
        Revisar();
        caja.TextChanged += (_, _) => Revisar();
        fila.Children.Add(aviso);

        var bots = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-8, 6, 0, 0) };
        bots.Children.Add(BotonPlano(carpeta ? "Elegir carpeta…" : "Elegir imagen…", () =>
        {
            if (carpeta)
            {
                var d = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta con las imágenes" };
                if (d.ShowDialog() == true) caja.Text = d.FolderName;
            }
            else
            {
                var d = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Qué imagen poner de fondo",
                    Filter = "Imágenes|" + string.Join(";", Fondos.Extensiones.Select(x => "*" + x)) + "|Todos|*.*",
                    CheckFileExists = true,
                };
                if (d.ShowDialog() == true) caja.Text = d.FileName;
            }
        }, false));
        fila.Children.Add(bots);
        return fila;
    }

    // ---------- La vista previa ----------
    //
    // Se dibuja con los mismos pinceles que el fondo de verdad, no con una aproximación:
    // el degradado usa `GestorFondos.PixelesDeDegradado` a través de un `LinearGradientBrush`
    // con los mismos dos colores y el mismo ángulo, así que lo que se ve es lo que saldrá.
    UIElement VistaPreviaDelFondo(string tipo)
    {
        var marco = new Border
        {
            Height = 118,
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            Background = _superficieAlta,
            Margin = new Thickness(0, 8, 0, 4),
            ClipToBounds = true,
        };

        try
        {
            switch (tipo)
            {
                case "solido":
                    marco.Background = new SolidColorBrush(Paleta.Leer(LeerFondo("Color1", "#1A1B26"), Colors.Black));
                    break;

                case "degradado":
                {
                    double g = LeerNumFondo("Angulo", 135);
                    double rad = g * Math.PI / 180.0;
                    marco.Background = new LinearGradientBrush(
                        Paleta.Leer(LeerFondo("Color1", "#0B0D14"), Colors.Black),
                        Paleta.Leer(LeerFondo("Color2", "#7AA2F7"), Colors.White),
                        new Point(0.5 - Math.Cos(rad) / 2, 0.5 - Math.Sin(rad) / 2),
                        new Point(0.5 + Math.Cos(rad) / 2, 0.5 + Math.Sin(rad) / 2));
                    break;
                }

                case "imagen":
                case "presentacion":
                {
                    var ruta = tipo == "imagen"
                        ? LeerFondo("Ruta", "")
                        : Fondos.ImagenesDe(LeerFondo("CarpetaPresentacion", "")).FirstOrDefault() ?? "";
                    if (ruta.Length > 0 && File.Exists(ruta))
                    {
                        // `DecodePixelWidth` es lo que evita cargar 20 MB para una tira de
                        // 118 px de alto, y `OnLoad` suelta el fichero: sin eso, la imagen
                        // se queda bloqueada y no se puede borrar ni mover.
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(ruta);
                        bmp.DecodePixelWidth = 640;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bmp.EndInit();
                        marco.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
                    }
                    else marco.Child = CentradoAtenuado(ruta.Length == 0
                        ? "Elige una imagen para verla aquí"
                        : "Esa imagen no está ahora mismo");
                    break;
                }

                case "animado":
                    marco.Child = CentradoAtenuado(Fondos.AnimadoDisponible
                        ? "Lo dibuja Wallpaper Engine" : "Wallpaper Engine no está");
                    break;

                default:
                    marco.Child = CentradoAtenuado("NubeBar no gestiona el fondo");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("dibujar la vista previa del fondo", ex);
            marco.Child = CentradoAtenuado("No se pudo dibujar la vista previa");
        }
        return marco;
    }

    TextBlock CentradoAtenuado(string texto) => new()
    {
        Text = texto, Foreground = _textoAtenuado, FontSize = 11.5,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        Opacity = 0.8,
    };

    UIElement SeccionSistema()
    {
        var p = NuevaPagina("sistema");
        p.Grupo("Arranque", IconoDeGrupo("Arranque"));

        var estado = new TextBlock { Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        estado.Text = Arranque.Activo ? "Ahora mismo sí arranca con Windows." : "Ahora mismo no arranca con Windows.";

        p.Mas(Interruptor("Arrancar con Windows", Arranque.Activo, v =>
        {
            bool ok = Arranque.Poner(v, out string msg);
            estado.Foreground = ok ? _texto : Brushes.Tomato;
            estado.Opacity = ok ? 0.6 : 1;
            estado.Text = (ok ? "Hecho: " : "No se pudo: ") + msg;
        }));
        p.Mas(estado);
        p.Mas(new TextBlock
        {
            Text = "Crea o quita un acceso directo en la carpeta Inicio de Windows hacia " + Arranque.ExeRecomendado(),
            Foreground = _texto, Opacity = 0.45, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });

        p.Grupo("Bandeja del sistema", IconoDeGrupo("Bandeja del sistema"));
        p.Mas(Interruptor("Traer los iconos de la bandeja a la isla",
                                   _arbol.Bandera("Barra", "Bandeja", true),
                                   v => _arbol.Poner("Barra", "Bandeja", v)));
        p.Mas(Deslizador("Iconos visibles antes del desplegable (0 = solo el \"^\")", 0, 15,
                                  _arbol.Numero("Barra", "MaxBandeja", 0),
                                  v => _arbol.Poner("Barra", "MaxBandeja", Math.Round(v))));
        p.Mas(new TextBlock
        {
            Text = "Para que se encienda hay que añadir además el módulo \"bandeja\" en Módulos: " +
                   "si no estuviera puesto, los iconos desaparecerían de la barra de Windows sin " +
                   "salir en ningún sitio. Al cerrar NubeBar vuelven solos a la bandeja de Windows.",
            Foreground = _texto, Opacity = 0.45, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });

        p.Grupo("Notificaciones", IconoDeGrupo("Notificaciones"));
        p.Mas(Interruptor("Enseñar las notificaciones en la isla",
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
        p.Mas(caja);
        p.Mas(new TextBlock
        {
            Text = "Una app por línea para silenciarla: basta un trozo de su identificador " +
                   "(por ejemplo \"Discord\" o \"WindowsStore\"). \"No molestar\" se respeta siempre.",
            Foreground = _texto, Opacity = 0.45, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });

        p.Grupo("Barra de tareas de Windows", IconoDeGrupo("Barra de tareas de Windows"));
        p.Mas(Interruptor("Ocultarla mientras la nube esté abierta",
                                   _arbol.Bandera("Barra", "OcultarBarraWindows", true),
                                   v => _arbol.Poner("Barra", "OcultarBarraWindows", v)));
        // De serie viene apagado en los cuatro bordes: la nube flota. Tocar este
        // interruptor lo convierte en una elección tuya y manda sobre el defecto.
        p.Mas(Interruptor("Reservar espacio en pantalla (solo en modo barra)",
                                   _arbol.BanderaDelPanel(Principal, "ReservarEspacio", PanelNube.ReservaPorDefecto),
                                   v => _arbol.PonerDelPanel(Principal, "ReservarEspacio", v)));
        var pistaReserva = Rotulo(
            "De serie viene apagado en los cuatro bordes: la nube flota por encima de las " +
            "ventanas. Enciéndelo si prefieres que Windows le guarde su franja y las ventanas " +
            "maximizadas no la tapen, como pasa con la barra de tareas.");
        pistaReserva.TextWrapping = TextWrapping.Wrap;
        pistaReserva.Opacity = 0.55;
        p.Mas(pistaReserva);

        p.Grupo("Archivos", IconoDeGrupo("Archivos"));
        p.Mas(BotonPlano("Abrir la carpeta del config", () =>
            Process.Start(new ProcessStartInfo(Config.Carpeta) { UseShellExecute = true })?.Dispose(), false));
        return p.Fin();
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
