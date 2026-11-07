using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// La pregunta de la primera vez: ¿en qué idioma quieres NubeBar?
//
// ---------- Por qué se pregunta en vez de adivinarlo ----------
//
// `Tema.Idioma` vale "auto" de serie y `Textos.DeWindows()` lo resuelve mirando la
// cultura de Windows: español si el sistema está en español, inglés en cualquier otro
// caso. Eso acierta casi siempre y no se toca — sigue siendo lo que pasa si cierras esta
// ventana sin elegir.
//
// Lo que no cubría es a quien tiene Windows en un idioma y quiere el programa en otro,
// que es más común de lo que parece: mucha gente usa Windows en inglés por costumbre y
// prefiere su escritorio en su lengua. Hasta ahora eso estaba a tres clics dentro de
// Ajustes, y había que saber que existía.
//
// ---------- Y por qué hace falta una bandera aparte ----------
//
// Porque "auto" no distingue «he elegido automático» de «nadie me ha preguntado». Es
// exactamente el tri-estado que hizo falta en `ReservarEspacio`, y por el mismo motivo:
// un valor por defecto no puede hacer de respuesta. Si se mirara solo `Idioma == "auto"`,
// al que elige automático a propósito se le volvería a preguntar en cada arranque.
//
// Así que la pregunta se hace una vez, se apunta `Tema.IdiomaPreguntado`, y no se vuelve
// a molestar. Se puede cambiar siempre en Ajustes → Apariencia → Idioma.
//
// ---------- Qué NO hace ----------
//
// No se dibuja con el tema. A esta altura del arranque el tema todavía no está montado
// —va antes que la escena de entrada y que las islas— y arrastrar `Roles` hasta aquí
// sería montar media interfaz para una ventana de ocho segundos. Lleva sus cuatro colores
// escritos a mano, y por eso el fichero está fuera de la lista que vigila la guarda de
// colores (ver Pruebas.SinColoresAMano: solo mira MainWindow*, VentanaAjustes* y
// Biblioteca).
static class ElegirIdioma
{
    // Bilingüe a propósito: es la única ventana del programa que se lee antes de saber en
    // qué idioma quiere leer quien la mira.
    const string Titulo = "¿En qué idioma quieres NubeBar?";
    const string TituloEn = "Which language do you want NubeBar in?";
    const string Pista = "Se puede cambiar cuando quieras en Ajustes → Apariencia → Idioma.";
    const string PistaEn = "You can change this any time in Settings → Appearance → Language.";

    public static bool HayQuePreguntar(Config c) =>
        c?.Tema != null && !c.Tema.IdiomaPreguntado;

    // Abre la ventana y devuelve la clave elegida ("es" / "en" / "auto"), o null si se
    // cerró sin elegir. La escritura la hace `Preguntar`, que es quien sabe del config.
    public static void Preguntar(Config c)
    {
        string? elegido = null;
        try
        {
            elegido = Pedir();
        }
        catch (Exception ex) { Log.Error("preguntar el idioma", ex); }

        // Se apunta SIEMPRE, se haya elegido o no. Si la ventana falló o la cerraste sin
        // tocar nada, la respuesta es «lo que diga Windows» y no hay por qué volver a
        // preguntar en el siguiente arranque: una pregunta que reaparece es peor que una
        // que no se hizo.
        try
        {
            var arbol = ConfigArbol.Cargar();
            if (elegido != null) arbol.Poner("Tema", "Idioma", elegido);
            arbol.Poner("Tema", "IdiomaPreguntado", true);
            arbol.Guardar();
        }
        catch (Exception ex) { Log.Error("guardar el idioma elegido", ex); }

        if (elegido != null)
        {
            c.Tema.Idioma = elegido;
            Textos.Elegir(elegido);
        }
        c.Tema.IdiomaPreguntado = true;
        Log.Info($"idioma de la primera vez: {(elegido ?? "no eligió, se queda en automático")} " +
                 $"(en uso: {Textos.Clave(Textos.EnUso)})");
    }

    static readonly Color Fondo = Color.FromRgb(0x0C, 0x0E, 0x17);
    static readonly Color Tinta = Color.FromRgb(0xFF, 0xFF, 0xFF);
    static readonly Color Acento = Color.FromRgb(0x8D, 0xA9, 0xE8);
    static readonly Color Superficie = Color.FromRgb(0x16, 0x1A, 0x26);

    static string? Pedir()
    {
        string? elegido = null;

        var v = new Window
        {
            Title = "NubeBar",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ShowInTaskbar = false,
        };

        var pila = new StackPanel { Margin = new Thickness(34, 28, 34, 26) };

        pila.Children.Add(new TextBlock
        {
            Text = Titulo,
            Foreground = new SolidColorBrush(Tinta),
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        pila.Children.Add(new TextBlock
        {
            Text = TituloEn,
            Foreground = new SolidColorBrush(Tinta),
            Opacity = 0.55,
            FontSize = 14,
            Margin = new Thickness(0, 4, 0, 22),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var fila = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        fila.Children.Add(Boton("Español", "Castellano", () => { elegido = "es"; v.Close(); }));
        fila.Children.Add(Boton("English", "British / US", () => { elegido = "en"; v.Close(); }));
        pila.Children.Add(fila);

        pila.Children.Add(new TextBlock
        {
            Text = Pista + "\n" + PistaEn,
            Foreground = new SolidColorBrush(Tinta),
            Opacity = 0.4,
            FontSize = 11.5,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
        });

        v.Content = new Border
        {
            Background = new SolidColorBrush(Fondo),
            BorderBrush = new SolidColorBrush(Acento) { Opacity = 0.35 },
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = pila,
        };

        // Arrastrable por cualquier sitio: no tiene barra de título que agarrar.
        v.MouseLeftButtonDown += (_, _) => { try { v.DragMove(); } catch { } };
        // Escape = no elijo, que Windows decida. Es la misma respuesta que cerrarla.
        v.KeyDown += (_, e) => { if (e.Key == Key.Escape) v.Close(); };

        v.ShowDialog();
        return elegido;
    }

    static Button Boton(string titulo, string pie, Action alPulsar)
    {
        var dentro = new StackPanel();
        dentro.Children.Add(new TextBlock
        {
            Text = titulo, FontSize = 16, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Tinta), HorizontalAlignment = HorizontalAlignment.Center,
        });
        dentro.Children.Add(new TextBlock
        {
            Text = pie, FontSize = 11, Opacity = 0.5,
            Foreground = new SolidColorBrush(Tinta), HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var caja = new Border
        {
            Background = new SolidColorBrush(Superficie),
            BorderBrush = new SolidColorBrush(Acento) { Opacity = 0.0 },
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(26, 14, 26, 14),
            Margin = new Thickness(7, 0, 7, 0),
            Child = dentro,
            Cursor = Cursors.Hand,
        };

        var b = new Button { Content = caja, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        b.Template = SinCromo();
        b.MouseEnter += (_, _) => ((SolidColorBrush)caja.BorderBrush).Opacity = 0.9;
        b.MouseLeave += (_, _) => ((SolidColorBrush)caja.BorderBrush).Opacity = 0.0;
        b.Click += (_, _) => alPulsar();
        System.Windows.Automation.AutomationProperties.SetName(b, titulo);
        return b;
    }

    // Un botón sin su cromo de Windows: aquí el aspecto lo pone el Border de dentro.
    static ControlTemplate SinCromo()
    {
        var raiz = new FrameworkElementFactory(typeof(ContentPresenter));
        return new ControlTemplate(typeof(Button)) { VisualTree = raiz };
    }
}
