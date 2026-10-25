using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NubeBar;

// El lenguaje visual de los paneles: «el centro de control de NubeBar».
//
// ---------- Por qué existe ----------
//
// Cada panel se dibujaba por su cuenta, y por eso parecían de programas distintos: el de
// red con sus filas, el centro de control con sus botoncitos, el de batería con otra
// medida de margen. Aquí están las piezas, una vez, y todos los paneles las usan.
//
// ---------- Y por qué las superficies son OPACAS ----------
//
// Esto es el arreglo de la queja de fondo. `Roles.Superficie` es un velo translúcido del
// color de texto —12 %— y encima la isla pinta con `Tema.Fondo`, que de serie es
// `#E6000000`: un 90 % de negro. Noventa por ciento suena a mucho y no lo es: por el 10 %
// que queda se lee perfectamente el texto de un terminal, y un panel de ajustes con el
// escritorio asomando por debajo no se lee.
//
// `Roles` ya daba la solución y nadie la usaba: `SuperficieSolida` y
// `SuperficieAltaSolida` son esas mismas superficies **aplanadas sobre el fondo opaco del
// tema**. Mismo color a la vista, sin nada detrás. Se derivan del tema, así que los siete
// temas siguen mandando y no hay ni una mezcla a mano.
//
// La isla sigue siendo translúcida: eso es su identidad. Lo que se vuelve opaco es la
// superficie de las TARJETAS, que es donde hay que leer.
public partial class MainWindow
{
    // ---------- Las medidas, en un sitio ----------
    //
    // Si cada panel elige sus números, cada panel mide distinto. Estos son los de todos.
    internal const double RadioCard = 12;
    internal const double RadioShell = 14;
    internal const double HuecoCard = 10;        // entre tarjetas
    internal const double DentroCard = 13;       // del filo al contenido
    internal const double AltoTocable = 34;      // mínimo cómodo para pulsar con el ratón

    SolidColorBrush PincelSolido(Color c) => new(c);

    // ================= PanelShell =================
    //
    // El contenedor de un panel. Lo que aporta: superficie opaca, filo sutil, esquinas y
    // un margen interior consistente. No decide el tamaño — eso es de `AbrirPanel`, que
    // mide y coloca, y cuya lógica no se toca.
    internal Border PanelShell(UIElement contenido, double margen = 12)
    {
        // Todo el panel va marcado: la red de seguridad por si una pieza nueva se olvida
        // de marcarse. Un panel es, por definición, una superficie con la que se interactúa.
        return SinArrastre(new Border
        {
            Background = PincelSolido(_roles.SuperficieSolida),
            BorderBrush = PincelSolido(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(RadioShell),
            Padding = new Thickness(margen),
            Child = contenido,
        });
    }

    // ================= Card =================
    //
    // La tarjeta. `alta` la pinta con la superficie de "elegido o bajo el ratón", para
    // poder anidar una dentro de otra sin que se pierda el borde.
    internal Border Card(UIElement contenido, bool alta = false, double dentro = DentroCard)
    {
        return new Border
        {
            Background = PincelSolido(alta ? _roles.SuperficieAltaSolida : _roles.SuperficieSolida),
            BorderBrush = PincelSolido(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(RadioCard),
            Padding = new Thickness(dentro),
            Margin = new Thickness(0, 0, 0, HuecoCard),
            Child = contenido,
        };
    }

    // ================= SectionHeader =================
    //
    // El rótulo de una sección dentro de un panel: icono, título y, a la derecha, lo que
    // haga falta (un porcentaje, un botón, una insignia).
    internal UIElement SectionHeader(int icono, string titulo, UIElement? derecha = null)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 13;
        ic.Foreground = _acentoLegible;
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(ic, 0);
        g.Children.Add(ic);

        var t = new TextBlock
        {
            Text = titulo,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(t, 1);
        g.Children.Add(t);

        if (derecha != null)
        {
            Grid.SetColumn(derecha, 2);
            g.Children.Add(derecha);
        }
        return g;
    }

    // ================= Toggle =================
    //
    // Un interruptor de verdad: una pista y un botoncito que se va de un lado al otro.
    // Sustituye al `CheckBox` del sistema, que en un panel oscuro sale como una caja
    // blanca y no se parece a nada de NubeBar.
    //
    // La animación respeta el nivel: en "Desactivado" el botoncito cambia de sitio y ya.
    internal UIElement Toggle(string etiqueta, bool valor, Action<bool> alCambiar, string? ayuda = null)
    {
        bool estado = valor;

        var pista = new Border
        {
            Width = 38, Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = PincelSolido(estado ? _roles.Acento : _roles.SuperficieAltaSolida),
            BorderBrush = PincelSolido(_roles.Borde),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var bolita = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = PincelSolido(estado ? _roles.FondoOpaco : _roles.Texto),
            HorizontalAlignment = estado ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0),
        };
        pista.Child = bolita;

        void Pintar()
        {
            pista.Background = PincelSolido(estado ? _roles.Acento : _roles.SuperficieAltaSolida);
            bolita.Fill = PincelSolido(estado ? _roles.FondoOpaco : _roles.Texto);
            bolita.HorizontalAlignment = estado ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = etiqueta, FontSize = 12, Foreground = _texto, TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(ayuda))
            textos.Children.Add(new TextBlock
            {
                Text = ayuda, FontSize = 10.5, Foreground = _textoAtenuado,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0),
            });

        var g = new Grid { MinHeight = AltoTocable, Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(textos, 0);
        g.Children.Add(textos);
        Grid.SetColumn(pista, 1);
        g.Children.Add(pista);

        // Todo el ancho es pulsable, no solo el interruptor: en un panel, acertar en 38 px
        // con el ratón es peor que acertar en la fila entera.
        var boton = new Button
        {
            Content = g,
            Template = PlantillaDeFila(),
            Cursor = Cursors.Hand,
            Focusable = true,
            ToolTip = ayuda ?? etiqueta,
        };
        boton.Click += (_, _) => { estado = !estado; Pintar(); alCambiar(estado); };
        return boton;
    }

    // ================= Slider =================
    //
    // Un deslizador con su valor a la vista, hecho con piezas propias. El del sistema es
    // una línea de 1 px con un cuadradito: es lo que se ve como "un control sin estilo".
    //
    // `alArrastrar` se llama mientras se mueve (respuesta inmediata, que es lo que se
    // pidió) y `alSoltar` al terminar, para lo que cueste caro.
    internal sealed class MandoSlider
    {
        public FrameworkElement Visual = null!;
        public Action<double>? Poner;
        public GestoDeslizador? Gesto;      // para poder comprobar que no queda nada agarrado
    }

    internal MandoSlider Slider(double valor, double min, double max,
                                Action<double> alArrastrar, Action<double>? alSoltar = null,
                                bool apagado = false)
    {
        double v = Math.Clamp(valor, min, max);
        var mando = new MandoSlider();

        var fondo = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var lleno = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = PincelSolido(apagado ? _roles.TextoAtenuado : _roles.Acento),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var agarre = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = PincelSolido(apagado ? _roles.TextoAtenuado : _roles.Acento),
            Stroke = PincelSolido(_roles.FondoOpaco),
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // La capa va marcada: su gesto es suyo y no de la isla. Ver MainWindow.Gesto.cs.
        var capa = SinArrastre(new Grid
        {
            Height = AltoTocable,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
        });
        capa.Children.Add(fondo);
        capa.Children.Add(lleno);
        capa.Children.Add(agarre);

        void Pintar()
        {
            double w = capa.ActualWidth;
            if (w <= 1) return;
            double k = (v - min) / Math.Max(0.0001, max - min);
            lleno.Width = Math.Max(0, w * k);
            agarre.Margin = new Thickness(Math.Max(0, w * k - 7), 0, 0, 0);
        }
        capa.SizeChanged += (_, _) => Pintar();
        mando.Poner = nv => { v = Math.Clamp(nv, min, max); Pintar(); };

        // ================= El gesto =================
        //
        // ---------- Por qué NO cuelga de MouseLeftButtonUp ----------
        //
        // Porque en una ventana `WS_EX_NOACTIVATE` y por capas ese evento se pierde: al
        // soltar fuera, al cambiar de ventana, al perder la captura. Si no llega, nadie
        // limpia y el deslizador se queda agarrado moviéndose con el ratón. Pasó.
        //
        // La verdad la dice `Nativo.BotonIzquierdoPulsado()` una vez por fotograma, que es
        // el patrón de la casa desde B2 —el mismo con el que la isla se deja arrastrar— y
        // funciona esté el puntero donde esté. `MouseLeftButtonUp` se mantiene como una
        // vía más, no como la única.
        var gesto = new GestoDeslizador();
        EventHandler? porFotograma = null;

        // La posición se lee del ratón de VERDAD, no del evento: así seguir el arrastre
        // fuera del control sigue funcionando.
        double DeLaPantalla()
        {
            try
            {
                var p = Monitores.Raton();
                var local = capa.PointFromScreen(new Point(p.X, p.Y));
                return min + (max - min) * Math.Clamp(local.X / Math.Max(1, capa.ActualWidth), 0, 1);
            }
            catch { return v; }
        }

        gesto.Apuntar = m => Log.Info(m);
        gesto.SoltarCaptura = () => { try { if (capa.IsMouseCaptured) capa.ReleaseMouseCapture(); } catch { } };
        gesto.DejarDeSeguir = () =>
        {
            if (porFotograma == null) return;
            CompositionTarget.Rendering -= porFotograma;
            porFotograma = null;
            // La apariencia vuelve a la normal: el agarre deja de estar "cogido".
            agarre.Width = agarre.Height = 14;
            alSoltar?.Invoke(v);
        };

        capa.MouseLeftButtonDown += (_, e) =>
        {
            gesto.Empezar();
            capa.CaptureMouse();
            agarre.Width = agarre.Height = 17;       // se nota que lo tienes cogido
            v = DeLaPantalla();
            Pintar();
            alArrastrar(v);

            porFotograma = (_, _) =>
            {
                // Lo único que decide si el gesto sigue vivo.
                if (!gesto.Fotograma(Nativo.BotonIzquierdoPulsado())) return;
                double nv = DeLaPantalla();
                if (Math.Abs(nv - v) < 0.001) return;    // sin cambio, sin trabajo
                v = nv;
                Pintar();
                alArrastrar(v);
            };
            CompositionTarget.Rendering += porFotograma;
            e.Handled = true;
        };

        // Las otras cinco salidas. Todas van al mismo sitio y limpiar es idempotente.
        capa.MouseLeftButtonUp += (_, _) => gesto.Terminar("soltado");
        capa.LostMouseCapture += (_, _) => gesto.Terminar("se perdió la captura");
        capa.Unloaded += (_, _) => gesto.Cancelar("se cerró el panel");
        capa.KeyDown += (_, e) => { if (e.Key == Key.Escape) gesto.Cancelar("Esc"); };

        // Y si la ventana pierde el foco. Se desuscribe al descargarse la capa para no
        // dejar un oyente colgado de la ventana por cada deslizador que se haya creado.
        void AlPerderFoco(object? s, EventArgs e) => gesto.Cancelar("la ventana perdió el foco");
        Deactivated += AlPerderFoco;
        capa.Unloaded += (_, _) => Deactivated -= AlPerderFoco;

        // Flechas y foco: un panel que solo se puede usar con el ratón no es accesible.
        var boton = new Button
        {
            Content = capa,
            Template = PlantillaDeFila(),
            Focusable = true,
            Padding = new Thickness(0),
        };
        boton.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { gesto.Cancelar("Esc"); return; }
            double paso = (max - min) / 20;
            double nv = e.Key switch
            {
                Key.Left or Key.Down => v - paso,
                Key.Right or Key.Up => v + paso,
                Key.Home => min,
                Key.End => max,
                _ => v,
            };
            if (Math.Abs(nv - v) < 0.0001) return;
            v = Math.Clamp(nv, min, max);
            Pintar();
            alArrastrar(v);
            alSoltar?.Invoke(v);
            e.Handled = true;
        };

        mando.Visual = boton;
        mando.Gesto = gesto;
        return mando;
    }

    // ================= MetricCard =================
    //
    // Una métrica: el número grande, su etiqueta y una tira con el histórico. El estado
    // sale del propio valor, no de un color escrito a mano: normal, alto y crítico usan
    // los roles semánticos, que es lo que hace que un "crítico" siga siendo rojo en
    // Gruvbox.
    internal enum EstadoMetrica { Normal, Alto, Critico }

    internal static EstadoMetrica EstadoDe(double porCiento) =>
        porCiento >= 90 ? EstadoMetrica.Critico
        : porCiento >= 70 ? EstadoMetrica.Alto
        : EstadoMetrica.Normal;

    Color ColorDeEstado(EstadoMetrica e) => e switch
    {
        EstadoMetrica.Critico => _roles.Negativo,
        EstadoMetrica.Alto => _roles.Aviso,
        _ => _roles.Acento,
    };

    internal sealed class MandoMetrica
    {
        public FrameworkElement Visual = null!;
        public Action<double, IReadOnlyList<double>>? Poner;
    }

    internal MandoMetrica MetricCard(string etiqueta, int icono, double porCiento,
                                     IReadOnlyList<double> historico, string? detalle = null)
    {
        var mando = new MandoMetrica();
        var estado = EstadoDe(porCiento);

        var numero = new TextBlock
        {
            Text = $"{Math.Round(porCiento)}%",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Foreground = PincelSolido(ColorDeEstado(estado)),
        };
        var rotulo = new TextBlock
        {
            Text = etiqueta, FontSize = 11, Foreground = _textoAtenuado,
            Margin = new Thickness(0, -2, 0, 0),
        };
        var sub = new TextBlock
        {
            Text = detalle ?? "", FontSize = 10, Foreground = _textoAtenuado,
            Opacity = 0.75, TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var grafica = new Polyline
        {
            Stroke = PincelSolido(ColorDeEstado(estado)),
            StrokeThickness = 1.6,
            Height = 26,
            StrokeLineJoin = PenLineJoin.Round,
        };
        var hueco = new Grid { Height = 26, Margin = new Thickness(0, 6, 0, 0), ClipToBounds = true };
        hueco.Children.Add(grafica);

        void Dibujar(double pc, IReadOnlyList<double> hist)
        {
            var est = EstadoDe(pc);
            var color = PincelSolido(ColorDeEstado(est));
            numero.Text = $"{Math.Round(pc)}%";
            numero.Foreground = color;
            grafica.Stroke = color;

            // La gráfica se redibuja SOLO aquí, con los puntos que le dan. No hay reloj
            // propio ni se repinta el panel entero: eso es lo que hacía que parpadeara.
            double w = Math.Max(10, hueco.ActualWidth > 1 ? hueco.ActualWidth : 120);
            var pts = new PointCollection();
            int n = hist.Count;
            if (n >= 2)
                for (int i = 0; i < n; i++)
                {
                    double x = w * i / (n - 1.0);
                    double y = 24 - 22 * Math.Clamp(hist[i], 0, 100) / 100.0;
                    pts.Add(new Point(x, y));
                }
            grafica.Points = pts;
        }
        hueco.SizeChanged += (_, _) => Dibujar(porCiento, historico);
        mando.Poner = (pc, hist) => Dibujar(pc, hist);

        var col = new StackPanel();
        col.Children.Add(SectionHeader(icono, etiqueta));
        col.Children.Add(numero);
        col.Children.Add(rotulo);
        if (detalle != null) col.Children.Add(sub);
        col.Children.Add(hueco);

        var card = Card(col);
        Dibujar(porCiento, historico);
        mando.Visual = card;
        return mando;
    }

    // ================= QuickAction =================
    //
    // Un botón de la cuadrícula de acciones rápidas: icono arriba, etiqueta abajo, y el
    // estado puesto con el velo de selección y el filo del acento — nunca solo con color,
    // que es la regla de accesibilidad que ya seguían los semánticos.
    internal Button QuickAction(int icono, string etiqueta, bool activo, Action alPulsar,
                                string? ayuda = null)
    {
        var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 17;
        ic.Foreground = activo ? _acentoLegible : _texto;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        col.Children.Add(ic);
        col.Children.Add(new TextBlock
        {
            Text = etiqueta,
            FontSize = 10,
            Foreground = activo ? _acentoLegible : _textoAtenuado,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
        });

        var b = new Button
        {
            Content = col,
            Template = PlantillaDeAccion(activo),
            MinHeight = 60,
            Margin = new Thickness(3),
            Cursor = Cursors.Hand,
            ToolTip = ayuda ?? etiqueta,
        };
        b.Click += (_, _) => alPulsar();
        return b;
    }

    // Una acción delicada: igual que `QuickAction` pero con el filo y el icono del rol
    // `Negativo`. Del TEMA, no un rojo escrito a mano — así sigue siendo rojo en Gruvbox
    // y no choca en un tema claro.
    internal Button AccionDelicada(int icono, string etiqueta, Action alPulsar, string? ayuda = null)
    {
        var b = QuickAction(icono, etiqueta, false, alPulsar, ayuda);
        var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 17;
        ic.Foreground = PincelSolido(_roles.NegativoTinta);
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        col.Children.Add(ic);
        col.Children.Add(new TextBlock
        {
            Text = etiqueta, FontSize = 10,
            Foreground = PincelSolido(_roles.NegativoTinta),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            TextAlignment = TextAlignment.Center,
        });
        b.Content = col;
        return b;
    }

    // La cuadrícula que las agrupa. Columnas fijas para que no bailen de ancho al cambiar
    // cuántas hay.
    internal UIElement RejillaDeAcciones(IEnumerable<Button> acciones, int columnas = 4)
    {
        var r = new UniformGrid { Columns = Math.Max(1, columnas) };
        foreach (var a in acciones) r.Children.Add(a);
        return r;
    }

    // ================= StatusBadge =================
    //
    // Una insignia de estado: un punto y una palabra. Sin el punto, un estado dicho solo
    // con color no lo distingue quien no distingue ese color.
    internal UIElement StatusBadge(string texto, bool bien, bool neutro = false)
    {
        var color = neutro ? _roles.TextoAtenuado : bien ? _roles.Positivo : _roles.Aviso;
        var fila = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fila.Children.Add(new Ellipse
        {
            Width = 7, Height = 7,
            Fill = PincelSolido(color),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        fila.Children.Add(new TextBlock
        {
            Text = texto, FontSize = 10.5, Foreground = _textoAtenuado,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return new Border
        {
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 9, 3),
            Child = fila,
        };
    }

    // ================= EmptyState =================
    //
    // El hueco cuando no hay nada. Con icono y una frase que diga qué hacer: un panel
    // vacío sin explicación se lee como un panel roto.
    internal UIElement EmptyState(int icono, string titulo, string? queHacer = null)
    {
        var col = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 18),
        };
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 24;
        ic.Foreground = _textoAtenuado;
        ic.Opacity = 0.65;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        col.Children.Add(ic);
        col.Children.Add(new TextBlock
        {
            Text = titulo, FontSize = 12, Foreground = _texto,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0), TextAlignment = TextAlignment.Center,
        });
        if (!string.IsNullOrWhiteSpace(queHacer))
            col.Children.Add(new TextBlock
            {
                Text = queHacer, FontSize = 10.5, Foreground = _textoAtenuado,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Opacity = 0.8,
            });
        return col;
    }

    // ================= MediaCard =================
    //
    // El reproductor. La portada va recortada en redondo y con `UniformToFill`, que es lo
    // que evita que una carátula cuadrada salga estirada.
    internal UIElement MediaCard(ImageSource? portada, string titulo, string? artista,
                                 string? origen, double progreso,
                                 Action anterior, Action reproducir, Action siguiente,
                                 bool sonando)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            return Card(EmptyState(0xE8D6, "No hay nada sonando",
                                   "Pon música y aparecerá aquí con sus controles"));

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var hueco = new Border
        {
            Width = 52, Height = 52,
            CornerRadius = new CornerRadius(9),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 11, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (portada != null)
            hueco.Child = new Image { Source = portada, Stretch = Stretch.UniformToFill };
        else
        {
            var ic = Glifo(char.ConvertFromUtf32(0xE8D6));
            ic.FontSize = 20;
            ic.Foreground = _textoAtenuado;
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            hueco.Child = ic;
        }
        Grid.SetColumn(hueco, 0);
        g.Children.Add(hueco);

        var col = new StackPanel();
        col.Children.Add(new TextBlock
        {
            Text = titulo, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            Foreground = _texto, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(artista))
            col.Children.Add(new TextBlock
            {
                Text = artista!, FontSize = 11, Foreground = _textoAtenuado,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        if (!string.IsNullOrWhiteSpace(origen))
            col.Children.Add(new TextBlock
            {
                Text = origen!, FontSize = 9.5, Foreground = _textoAtenuado,
                Opacity = 0.7, Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

        // El progreso: una tira, no un control. Solo informa.
        var pista = new Border
        {
            Height = 3, CornerRadius = new CornerRadius(2),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            Margin = new Thickness(0, 7, 0, 0),
        };
        var lleno = new Border
        {
            Height = 3, CornerRadius = new CornerRadius(2),
            Background = PincelSolido(_roles.Acento),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var pilaProg = new Grid();
        pilaProg.Children.Add(pista);
        pilaProg.Children.Add(lleno);
        pilaProg.SizeChanged += (_, _) =>
            lleno.Width = Math.Max(0, pilaProg.ActualWidth * Math.Clamp(progreso, 0, 1));
        col.Children.Add(pilaProg);

        var mandos = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(-6, 6, 0, 0),
        };
        mandos.Children.Add(BotonIcono(0xE892, "Anterior", anterior));
        mandos.Children.Add(BotonIcono(sonando ? 0xE769 : 0xE768, sonando ? "Pausar" : "Reproducir", reproducir));
        mandos.Children.Add(BotonIcono(0xE893, "Siguiente", siguiente));
        col.Children.Add(mandos);

        Grid.SetColumn(col, 1);
        g.Children.Add(col);
        return Card(g);
    }

    // Un botón que es solo un icono, con su zona pulsable cómoda.
    internal Button BotonIcono(int icono, string ayuda, Action alPulsar, bool activo = false)
    {
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 14;
        ic.Foreground = activo ? _acentoLegible : _texto;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        ic.VerticalAlignment = VerticalAlignment.Center;

        var b = new Button
        {
            Content = ic,
            Template = PlantillaDeAccion(activo),
            Width = AltoTocable, Height = AltoTocable,
            Margin = new Thickness(3, 0, 3, 0),
            Cursor = Cursors.Hand,
            ToolTip = ayuda,
        };
        b.Click += (_, _) => alPulsar();
        return b;
    }


    // ================= SearchBox =================
    //
    // La caja de búsqueda: lupa, campo y aspa para limpiar, en una superficie propia con
    // filo. El `TextBox` se le pasa de fuera porque el que ya existe lleva sus oyentes de
    // teclado y su lógica de búsqueda: aquí solo se le pone el marco.
    internal Border SearchBox(TextBox campo, string ayuda, Action? alLimpiar = null)
    {
        campo.Background = Brushes.Transparent;
        campo.Foreground = _texto;
        campo.CaretBrush = _acentoLegible;
        campo.BorderThickness = new Thickness(0);
        campo.VerticalContentAlignment = VerticalAlignment.Center;
        campo.ToolTip = ayuda;

        var lupa = Glifo(char.ConvertFromUtf32(0xE721));
        lupa.FontSize = 13;
        lupa.Foreground = _textoAtenuado;
        lupa.VerticalAlignment = VerticalAlignment.Center;
        lupa.Margin = new Thickness(2, 0, 9, 0);

        var aspa = BotonIcono(0xE711, "Limpiar la búsqueda", () =>
        {
            campo.Text = "";
            campo.Focus();
            alLimpiar?.Invoke();
        });
        aspa.Width = aspa.Height = 26;
        aspa.Visibility = campo.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        campo.TextChanged += (_, _) =>
            aspa.Visibility = campo.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(lupa, 0);
        g.Children.Add(lupa);
        Grid.SetColumn(campo, 1);
        g.Children.Add(campo);
        Grid.SetColumn(aspa, 2);
        g.Children.Add(aspa);

        var marco = SinArrastre(new Border
        {
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            BorderBrush = PincelSolido(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 3, 5, 3),
            Child = g,
        });

        // El filo se enciende con el acento cuando el campo tiene el foco: es el
        // indicador de "aquí va lo que escribas".
        campo.GotKeyboardFocus += (_, _) => marco.BorderBrush = PincelSolido(_roles.Acento);
        campo.LostKeyboardFocus += (_, _) => marco.BorderBrush = PincelSolido(_roles.Borde);
        return marco;
    }

    // ================= FilterChip =================
    //
    // Una pastilla de filtro. Elegida lleva el velo de selección Y el filo del acento:
    // nunca solo color, que es la regla de accesibilidad de los semánticos.
    internal Button FilterChip(string texto, bool elegida, Action alPulsar, int? cuantas = null)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(new TextBlock
        {
            Text = texto,
            FontSize = 11,
            Foreground = elegida ? _acentoLegible : _texto,
            FontWeight = elegida ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (cuantas is { } n)
            fila.Children.Add(new TextBlock
            {
                Text = "  " + n,
                FontSize = 10,
                Foreground = _textoAtenuado,
                VerticalAlignment = VerticalAlignment.Center,
            });

        var b = new Button
        {
            Content = fila,
            Template = PlantillaDeChip(elegida),
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(11, 4, 11, 4),
            MinHeight = 26,
            Cursor = Cursors.Hand,
            ToolTip = elegida ? $"{texto} (puesto) — vuelve a pulsar para quitarlo" : "Ver solo " + texto,
        };
        b.Click += (_, _) => alPulsar();
        return b;
    }

    ControlTemplate PlantillaDeChip(bool elegida)
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty,
            PincelSolido(elegida ? _roles.SeleccionSolida : _roles.SuperficieAltaSolida));
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        marco.SetValue(Border.BorderBrushProperty,
            elegida ? PincelSolido(_roles.Acento) : PincelSolido(_roles.Borde));
        marco.SetBinding(Border.PaddingProperty,
            new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        marco.AppendChild(cp);
        p.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(encima);

        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }

    // ================= Las plantillas =================
    //
    // Dos, y las dos con la misma idea: el filo SIEMPRE puesto y lo que cambia es su
    // color. Con el grosor variable, el contenido se mueve al pasar el ratón.
    ControlTemplate PlantillaDeFila()
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        marco.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        marco.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        marco.AppendChild(cp);
        p.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BackgroundProperty, PincelSolido(_roles.SuperficieAltaSolida), "marco"));
        p.Triggers.Add(encima);

        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }

    ControlTemplate PlantillaDeAccion(bool activo)
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty,
            PincelSolido(activo ? _roles.SeleccionSolida : _roles.SuperficieAltaSolida));
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        marco.SetValue(Border.BorderBrushProperty,
            activo ? PincelSolido(_roles.Acento) : PincelSolido(_roles.Borde));
        marco.SetValue(Border.PaddingProperty, new Thickness(6));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        marco.AppendChild(cp);
        p.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(encima);

        var pulsado = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pulsado.Setters.Add(new Setter(Border.BackgroundProperty, PincelSolido(_roles.SeleccionSolida), "marco"));
        p.Triggers.Add(pulsado);

        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }
}
