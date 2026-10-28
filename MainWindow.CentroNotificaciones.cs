using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// El centro de notificaciones: la campana de la isla y su panel.
//
// ---------- Por qué no hay una ventana nueva ----------
//
// Porque ya hay dos y las dos saben lo que hace falta. Esto es un panel más —como el de
// la red, el de la batería o el menú Inicio— y por eso sale solo en los cuatro bordes:
// con la isla horizontal se despliega el cajón, y con la barra vertical `AbrirPanel`
// deriva a la ventana superpuesta de C1, que ya calcula el rectángulo en coordenadas de
// pantalla, se eleva por encima de las aplicaciones y devuelve el orden Z al cerrarse.
//
// Inventar aquí una segunda arquitectura habría significado repetir los dos problemas
// que esa ventana resolvió (ver la cabecera de VentanaSuperpuesta.cs) y resolverlos
// peor.
//
// ---------- Y por qué no pide el teclado ----------
//
// El menú Inicio sí lo pide, porque tiene una caja donde escribes. Este no: si lo
// pidiera, abrir las notificaciones te sacaría de lo que estabas haciendo, y eso es
// exactamente lo que un centro de notificaciones no debe hacer. Esc se mira preguntando
// al sistema cada 60 ms mientras ESTE panel está abierto —el mismo truco que usa la
// escena de entrada— y se deja de mirar en cuanto se cierra. No hay gancho, no se
// captura nada y ninguna otra ventana se entera.
public partial class MainWindow : Window
{
    const string PanelNotas = "notificaciones";

    static readonly string[] SinNadaQueContar =
    {
        "No hay nada pendiente",
        "Aquí aparecerán los avisos de tus aplicaciones",
    };

    // ---------- El widget de la isla ----------

    UIElement ModuloNotificaciones()
    {
        var caja = new Grid { VerticalAlignment = VerticalAlignment.Center };

        var campana = Glifo("");
        campana.FontSize = 15;
        caja.Children.Add(campana);

        // La chapita de no leídas: un círculo del acento con el número dentro, pegado
        // arriba a la derecha de la campana. Con más de nueve pone "9+", que es lo que
        // cabe sin que el círculo se convierta en una pastilla.
        var chapa = new Border
        {
            CornerRadius = new CornerRadius(7),
            Background = _acento,
            MinWidth = 14, Height = 14,
            Padding = new Thickness(3, 0, 3, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -7, 0),
            Visibility = Visibility.Collapsed
        };
        var cifra = new TextBlock
        {
            FontSize = 9, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        chapa.Child = cifra;
        caja.Children.Add(chapa);

        void Repintar()
        {
            int n = Centro.Actual.NoLeidas;
            bool mudo = Centro.Actual.NoMolestar || NoMolestar.Activo;
            campana.Text = mudo ? "" : "";          // campana tachada / campana
            campana.Foreground = n > 0 && !mudo ? _acentoLegible : _texto;
            campana.Opacity = mudo ? 0.6 : 1;
            chapa.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
            chapa.Background = mudo ? _textoAtenuado : _acento;
            cifra.Text = n > 9 ? "9+" : n.ToString();
            // El número va sobre el acento, así que su tinta es la que se lee AHÍ y no
            // la del texto de la isla. Es la misma regla de los roles: un color de
            // relleno y un color de texto no son lo mismo.
            cifra.Foreground = new SolidColorBrush(Contraste.TintaSobre(_roles.Acento));
            var tip = n == 0 ? "No hay notificaciones sin leer"
                             : n == 1 ? "1 notificación sin leer"
                                      : $"{n} notificaciones sin leer";
            if (mudo) tip += " · no molestar";
            caja.ToolTip = tip;
        }

        Repintar();
        // La campana se repinta cuando cambia el centro, no cada segundo: la cuenta
        // solo se mueve cuando llega algo o cuando tocas una, y las dos cosas avisan.
        Centro.Actual.Cambio += Repintar;
        caja.Unloaded += (_, _) => Centro.Actual.Cambio -= Repintar;

        var boton = Boton(caja, "Notificaciones", AbrirNotificaciones);
        // Se guarda para que el panel cuelgue de la campana, como el de audio cuelga del
        // altavoz. Ver `AbrirNotificaciones`.
        _anclaNotas = boton;
        return boton;
    }

    // El botón de la campana, para anclar el panel a él.
    FrameworkElement? _anclaNotas;

    // ---------- Abrir y cerrar ----------

    void AbrirNotificaciones()
    {
        // Anclado a la campana, por el mismo motivo que la batería y la bandeja: sin ancla
        // el panel se dibuja de punta a punta de la isla con 420 px de contenido centrados
        // dentro. Con tres paneles iguales en esto, no era un olvido suelto: era que sin
        // superficie propia no se distinguía, y por eso duró tanto.
        bool abierto = AlternarPanel(PanelNotas, CrearNotificaciones, 420, AltoDeNotas(),
                                     teclado: false, ancla: _anclaNotas, scrollPropio: true);
        if (abierto) MirarEscDelPanel(true);
    }

    // El alto se ajusta a lo que hay, con tope: con dos avisos no tiene sentido una
    // tarjeta de 460 px medio vacía, y con cuarenta tampoco tiene sentido una que no
    // cabe en la pantalla.
    //
    // La cuenta va aparte y es estática porque es la que decide si la tarjeta cabe en
    // un portátil de 768 px lleno de notificaciones, y ese es justo el equipo que no se
    // tiene a mano para mirarlo.
    internal static double AltoDelCentro(int cuantas, double altoPantalla)
    {
        const double cabecera = 76;                 // título + fila de botones
        const double porAviso = 62;
        double alto = cuantas == 0 ? cabecera + 96
                                   : cabecera + Math.Min(cuantas, 8) * porAviso;
        double tope = Math.Max(240, altoPantalla * 0.62);
        return Math.Min(alto, tope);
    }

    double AltoDeNotas() =>
        AltoDelCentro(Centro.Actual.Cuantas, _monitor.Alto / Math.Max(1, _monitor.Escala));

    void RefrescarNotificaciones()
    {
        if (_panelAbierto != PanelNotas) return;
        // Se sustituye el contenido sin cerrar: así no hay que replegar y desplegar
        // cada vez que descartas una. Vale igual para el cajón de la isla y para la
        // tarjeta flotante de la barra vertical.
        CambiarContenidoDelPanel(CrearNotificaciones());
        ReajustarPanel(PanelNotas);
    }

    // ---------- Esc ----------
    //
    // Se mira a pelo, sin gancho ni foco, y SOLO mientras este panel está abierto.
    bool _mirandoEsc;

    void MirarEscDelPanel(bool si)
    {
        _mirandoEsc = si;
        _escPulsadaAntes = false;
    }

    bool _escPulsadaAntes;

    void VigilarEscDelPanel()
    {
        if (!_mirandoEsc) return;
        if (_panelAbierto != PanelNotas) { MirarEscDelPanel(false); return; }

        bool pulsada;
        try { pulsada = Nativo.TeclaPulsada(0x1B); } catch { return; }
        if (!pulsada) { _escPulsadaAntes = false; return; }
        if (_escPulsadaAntes) return;         // mantenerla pulsada cuenta una vez
        _escPulsadaAntes = true;

        Log.Info("notificaciones: cerradas con Esc");
        MirarEscDelPanel(false);
        CerrarPanel();
    }

    // ---------- El panel ----------

    FrameworkElement CrearNotificaciones()
    {
        var centro = Centro.Actual;
        var columna = new StackPanel { Margin = new Thickness(14, 2, 14, 12) };

        columna.Children.Add(CabeceraNotas(centro));

        // Las dos salidas pasan por la concha de C5 — la de «no hay nada» también. Es el
        // detalle que se olvida al añadir una superficie: el camino corto se queda sin ella
        // y el panel vacío se ve distinto del lleno sin que nadie lo haya decidido.
        if (centro.Vacio) { columna.Children.Add(NadaQueContar()); return PanelShell(columna, 0); }

        var lista = new StackPanel();
        foreach (var g in centro.PorApp()) lista.Children.Add(GrupoDeApp(g));

        var desplaza = new ScrollViewer
        {
            Content = lista,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(120, AltoDeNotas() - 86),
            Margin = new Thickness(0, 4, -8, 0),
            Padding = new Thickness(0, 0, 8, 0)
        };
        columna.Children.Add(desplaza);

        // Y aquí la razón de más peso para darle concha a éste: son notificaciones, o sea
        // texto pequeño y mucho. Apoyado en el fondo translúcido de la isla, el escritorio se
        // transparenta detrás de cada línea.
        return PanelShell(columna, 0);
    }

    FrameworkElement CabeceraNotas(CentroNotificaciones centro)
    {
        var fila = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int n = centro.NoLeidas;
        var titulo = new TextBlock
        {
            Text = n == 0 ? "Notificaciones" : $"Notificaciones · {n} sin leer",
            Foreground = _texto, FontSize = 13, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        fila.Children.Add(titulo);

        // No molestar: el de NubeBar. Si además Windows está en horas de silencio, el
        // botón lo dice pero no finge poder apagarlo — eso se cambia en Configuración.
        bool mudoWindows = NoMolestar.Activo;
        var glifoMudo = Glifo(centro.NoMolestar || mudoWindows ? "" : "");
        glifoMudo.FontSize = 13;
        glifoMudo.Foreground = centro.NoMolestar ? _acentoLegible : _texto;
        var bMudo = Boton(glifoMudo,
            mudoWindows && !centro.NoMolestar
                ? "Windows está en horas de silencio; aquí puedes activarlo también para NubeBar"
                : centro.NoMolestar ? "No molestar: activado. Se siguen guardando, no se asoman"
                                    : "No molestar: no se asomarán a la isla, pero se guardan",
            () =>
            {
                centro.NoMolestar = !centro.NoMolestar;
                centro.Guardar();
                Log.Info($"notificaciones: no molestar = {centro.NoMolestar}");
                RefrescarNotificaciones();
                Tick();
            });
        bMudo.Width = 30;
        Grid.SetColumn(bMudo, 1);
        fila.Children.Add(bMudo);

        var bBorrar = Boton(new TextBlock
        {
            Text = "Borrar todo", Foreground = _acentoLegible, FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        }, "Quitar todas las notificaciones del centro", () =>
        {
            int cuantas = centro.BorrarTodo();
            centro.Guardar();
            Log.Info($"notificaciones: borradas {cuantas}");
            RefrescarNotificaciones();
        });
        bBorrar.IsEnabled = !centro.Vacio;
        bBorrar.Opacity = centro.Vacio ? 0.45 : 1;
        Grid.SetColumn(bBorrar, 2);
        fila.Children.Add(bBorrar);

        return fila;
    }

    // El estado vacío. No es un hueco: dice qué va a aparecer ahí y por qué ahora no
    // hay nada, que es lo que uno quiere saber cuando abre y no encuentra nada.
    FrameworkElement NadaQueContar()
    {
        var caja = new StackPanel
        {
            Margin = new Thickness(0, 22, 0, 18),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var g = Glifo("");
        g.FontSize = 30;
        g.Foreground = _textoAtenuado;
        g.Opacity = 0.7;
        g.HorizontalAlignment = HorizontalAlignment.Center;
        caja.Children.Add(g);
        caja.Children.Add(new TextBlock
        {
            Text = SinNadaQueContar[0], Foreground = _texto, FontSize = 13,
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        caja.Children.Add(new TextBlock
        {
            Text = !_cfg.Barra.Notificaciones
                   ? "Las notificaciones están apagadas en Ajustes de la nube"
                   : Notificaciones.Disponible
                     ? SinNadaQueContar[1]
                     : "Windows no guarda notificaciones en este equipo",
            Foreground = _textoAtenuado, FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0),
            TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        return caja;
    }

    // ---------- Un grupo: una app y sus avisos ----------

    FrameworkElement GrupoDeApp(GrupoAviso g)
    {
        var caja = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

        var cabecera = new Grid();
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nombre = new TextBlock
        {
            Text = g.NoLeidas > 0 ? $"{g.App}  ·  {g.NoLeidas}" : g.App,
            Foreground = _acentoLegible, FontSize = 11,
            FontWeight = g.NoLeidas > 0 ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        cabecera.Children.Add(nombre);

        var silencio = Glifo("");
        silencio.FontSize = 11;
        silencio.Foreground = _textoAtenuado;
        var bSilencio = Boton(silencio, $"Silenciar las notificaciones de {g.App}", () =>
        {
            var centro = Centro.Actual;
            centro.Silenciar(g.AumId.Length > 0 ? g.AumId : g.App, true);
            centro.Guardar();
            Log.Info($"notificaciones: silenciada {g.App}");
            RefrescarNotificaciones();
            Tick();
        });
        bSilencio.Width = 26;
        bSilencio.Height = 20;
        Grid.SetColumn(bSilencio, 1);
        cabecera.Children.Add(bSilencio);
        caja.Children.Add(cabecera);

        foreach (var a in g.Avisos) caja.Children.Add(FilaDeAviso(a));
        return caja;
    }

    FrameworkElement FilaDeAviso(Anotacion a)
    {
        var marco = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = a.Leida ? _superficie : _elegido,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(10, 8, 6, 8),
            Cursor = Cursors.Hand
        };

        var rejilla = new Grid();
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // El punto de "sin leer". No está solo el color diciéndolo: el fondo también
        // cambia y el texto va en negrita, que es la misma regla de los roles — un
        // estado no se comunica solo con color.
        var punto = new Border
        {
            Width = 7, Height = 7, CornerRadius = new CornerRadius(4),
            Background = _acento,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 9, 0),
            Visibility = a.Leida ? Visibility.Hidden : Visibility.Visible
        };
        rejilla.Children.Add(punto);

        var pila = new StackPanel();
        var arriba = new Grid();
        arriba.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        arriba.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        arriba.Children.Add(new TextBlock
        {
            Text = a.Titulo.Length > 0 ? a.Titulo : a.App,
            Foreground = _texto, FontSize = 12,
            FontWeight = a.Leida ? FontWeights.Normal : FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var hora = new TextBlock
        {
            Text = a.Momento(DateTime.Now), Foreground = _textoAtenuado, FontSize = 10,
            Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(hora, 1);
        arriba.Children.Add(hora);
        pila.Children.Add(arriba);

        if (a.Texto.Length > 0)
            pila.Children.Add(new TextBlock
            {
                Text = a.Texto, Foreground = _textoAtenuado, FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            });

        // Las acciones que de verdad se pueden pulsar (ver Notificaciones.Acciones)
        if (a.Acciones.Count > 0)
        {
            var botones = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-6, 4, 0, 0) };
            foreach (var acc in a.Acciones)
            {
                var acci = acc;
                var b = Boton(new TextBlock { Text = acci.Texto, Foreground = _acentoLegible, FontSize = 11 },
                              acci.Uri, () =>
                              {
                                  Notificaciones.Lanzar(acci);
                                  Centro.Actual.MarcarLeida(a.Id);
                                  Centro.Actual.Guardar();
                                  CerrarPanel();
                              });
                b.Height = 22;
                botones.Children.Add(b);
            }
            pila.Children.Add(botones);
        }

        Grid.SetColumn(pila, 1);
        rejilla.Children.Add(pila);

        var aspa = Glifo("");
        aspa.FontSize = 11;
        aspa.Foreground = _textoAtenuado;
        var bAspa = Boton(aspa, "Descartar esta notificación", () =>
        {
            Centro.Actual.Descartar(a.Id);
            Centro.Actual.Guardar();
            RefrescarNotificaciones();
            Tick();
        });
        bAspa.Width = 24;
        bAspa.Height = 22;
        bAspa.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(bAspa, 2);
        rejilla.Children.Add(bAspa);

        marco.Child = rejilla;

        // Pulsar la tarjeta: la marca como leída y abre la app dueña. Las dos cosas,
        // porque es lo que esperas de un aviso que te interesa; si solo quieres
        // quitártelo de encima, está el aspa.
        marco.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && EstaDentroDeUnBoton(d)) return;
            Centro.Actual.MarcarLeida(a.Id);
            Centro.Actual.Guardar();
            Notificaciones.Abrir(a.AumId);
            CerrarPanel();
        };
        // Pasar el ratón por encima ya la da por vista: es el gesto de leerla.
        marco.MouseEnter += (_, _) =>
        {
            if (a.Leida) return;
            if (!Centro.Actual.MarcarLeida(a.Id)) return;
            Centro.Actual.Guardar();
            marco.Background = _superficie;
            punto.Visibility = Visibility.Hidden;
            Tick();
        };
        return marco;
    }

    // Un clic sobre el aspa o sobre una acción no es un clic en la tarjeta
    static bool EstaDentroDeUnBoton(DependencyObject d)
    {
        for (var p = d; p != null; p = VisualTreeHelper.GetParent(p))
            if (p is Button) return true;
        return false;
    }
}
