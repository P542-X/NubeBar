using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace NubeBar;

// El centro de control: red, volumen y batería en la isla, y al pulsarlos un panel
// con los mandos de verdad, al estilo del centro de control del iPhone.
public partial class MainWindow : Window
{
    // ---------- Una sola superficie ----------
    //
    // Las tarjetas del centro de control viven en la portada de INICIO, que es la que se
    // abre con el botón Inicio y con la tecla Windows. Hubo un rato en que eran un panel
    // propio ("control") con su propio botón en la isla, y eran dos pantallas con lo
    // mismo: una sola fuente de apertura y una sola superficie.
    //
    // La constante se queda con nombre propio —y no se usa `PanelInicio` a pelo— para que
    // al leer un refresco parcial se vea de qué superficie se está hablando.
    internal const string PanelDelCentro = PanelInicio;

    // ---------- Quién abre qué ----------
    //
    // Una sola tabla, y los botones de este módulo la usan de verdad para despachar: si
    // "altavoz" dejara de apuntar a `PanelAudio`, el altavoz abriría otra cosa. Por eso
    // la prueba que mira esta tabla está mirando el comportamiento, no una copia.
    //
    // Las dos últimas filas son las que NO se tocan —la cuadrícula abre la vista general
    // y el `⊞` abre el lanzador, cada uno con su cableado de siempre— y están aquí para
    // dejar escrito el reparto: seis botones, seis paneles distintos. Es lo que impide
    // que un botón se cuele en la superficie de otro, que es justo lo que pasó al
    // confundir "Inicio" con el lanzador.
    internal const string BotonAltavoz = "altavoz",
                          BotonRed = "red", BotonBateria = "bateria",
                          BotonCuadricula = "cuadricula", BotonLanzador = "lanzador";

    internal static readonly string[] BotonesDeIsla =
        { BotonAltavoz, BotonRed, BotonBateria, BotonCuadricula, BotonLanzador };

    internal static string PanelDeBoton(string boton) => boton switch
    {

        BotonAltavoz    => PanelAudio,
        BotonRed        => PanelRed,
        BotonBateria    => PanelBateria,
        BotonCuadricula => PanelVista,      // no se toca
        BotonLanzador   => PanelInicio,     // no se toca
        _ => "",
    };

    // El despacho de los cuatro de este módulo. Los otros dos conservan su cableado.
    void AbrirDesde(string boton)
    {
        ApuntarBoton(boton);          // TEMPORAL: diagnóstico del flujo
        switch (PanelDeBoton(boton))
        {

            case PanelAudio:   AbrirAudio();   break;
            case PanelRed:     AbrirRed();     break;
            case PanelBateria: AbrirBateria(); break;
            default: Log.Info($"isla: botón sin panel en este módulo ({boton})"); break;
        }
    }

    // ---------- Los iconos de la isla ----------
    UIElement ModuloControl()
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var red = Glifo("");
        var vol = Glifo("");
        var bat = Glifo("");

        // El idioma del teclado: solo sale si tienes más de una distribución puesta.
        // Clic para pasar a la siguiente (como Win+Espacio), rueda para ir y volver.
        var idioma = new TextBlock { Foreground = _texto, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var bIdioma = Boton(idioma, "Idioma del teclado", () => Idioma.Siguiente());
        bIdioma.MouseWheel += (_, e) => { Idioma.Siguiente(atras: e.Delta > 0); Tick(); e.Handled = true; };
        bIdioma.Visibility = Visibility.Collapsed;

        var bRed = Boton(red, "Red", () => AbrirDesde(BotonRed));
        var bVol = Boton(vol, "Volumen y sonido", () => AbrirDesde(BotonAltavoz));
        var bBat = Boton(bat, "Batería", () => AbrirDesde(BotonBateria));

        // ---------- El ancla ----------
        //
        // Aquí estaba media causa de que el panel "ocupara demasiado ancho". Sin `ancla`,
        // `AbrirPanel` dibuja el cajón de punta a punta de la isla —1169 px en una pantalla
        // de 2560— y las tarjetas se estiran a ese ancho: de ahí el espacio vacío, las
        // tarjetas estiradas y CPU/RAM con sitio de sobra.
        //
        // Con el ancla, el cajón se recorta como una tarjeta del ancho pedido, colgada de
        // su icono. El mecanismo existía desde C1 y estos paneles no lo usaban.

        _anclaVolumen = bVol;
        _anclaRed = bRed;
        _anclaBateria = bBat;

        // La rueda sobre el altavoz sube y baja el volumen de 2 en 2, sin abrir nada.
        // El aviso de la isla sale solo: VigilarVolumen ve el cambio.
        bVol.MouseWheel += (_, e) =>
        {
            int v = Audio.Volumen();
            if (v >= 0) Audio.PonerVolumen(v + (e.Delta > 0 ? 2 : -2));
            if (Audio.Silenciado && e.Delta > 0) Audio.Silenciar(false);
            e.Handled = true;
        };

        fila.Children.Add(bIdioma);

        fila.Children.Add(bRed);
        fila.Children.Add(bVol);
        fila.Children.Add(bBat);

        _actualizadores.Add(() =>
        {
            try
            {
                var distribuciones = Idioma.Lista();
                if (distribuciones.Count > 1)
                {
                    var actual = distribuciones.FirstOrDefault(d => d.Hkl == Idioma.Actual());
                    idioma.Text = actual?.Corto ?? "··";
                    bIdioma.ToolTip = $"Teclado: {actual?.Nombre ?? "?"}\nClic o rueda para cambiar";
                    bIdioma.Visibility = Visibility.Visible;
                }
                else bIdioma.Visibility = Visibility.Collapsed;

                red.Text = Red.Estado() switch
                {
                    Red.Tipo.Cable => "",     // cable de red
                    Red.Tipo.Wifi => "",      // ondas
                    _ => ""                   // sin conexión
                };
                int v = Audio.Volumen();
                bool mudo = Audio.Silenciado;
                vol.Text = mudo || v == 0 ? "" : v < 33 ? "" : v < 66 ? "" : "";
                bVol.ToolTip = mudo ? "Volumen: silenciado" : $"Volumen: {v}%";

                // La pila enseña su nivel de 10 en 10 y el rayo si está cargando
                var energia = Bateria.Leer();
                bBat.Visibility = energia.HayBateria ? Visibility.Visible : Visibility.Collapsed;
                if (energia.HayBateria)
                {
                    bat.Text = Bateria.Glifo(energia);
                    bat.Foreground = energia.Ahorro ? Brushes.Gold
                                   : energia.PorCiento <= 15 && !energia.Enchufado ? Brushes.Tomato : _texto;
                    bBat.ToolTip = $"Batería: {energia.PorCiento}% · {Bateria.Texto(energia)}";
                }
            }
            catch { }
        });

        return fila;
    }

    // ---------- Lo que se actualiza sin tocar el resto ----------
    //
    // `RefrescarControl` hacía `CerrarPanel()` y volvía a abrir el panel entero, así que
    // pulsar silencio lo destruía y lo reanimaba de alto. Guardando los controles que
    // cambian se actualiza UNO: el panel no se cierra, no se remonta, no cambia de tamaño
    // y no pierde el foco ni el scroll.
    MandoSlider? _ccVolumen;
    TextBlock? _ccCifraVolumen;
    Button? _ccMudo;
    StackPanel? _ccBluetooth;
    MandoMetrica? _ccCpu, _ccRam;
    Border? _ccMediaHueco;
    UniformGrid? _ccAcciones;

    internal const string PanelAudio = "audio";

    // Los iconos de los que cuelgan los paneles. Se guardan al montar la isla.
    FrameworkElement? _anclaVolumen, _anclaRed, _anclaBateria;

    // El icono del centro de control y su botón, para poder marcarlo activo mientras el
    // panel está abierto.

    // ================= Dos paneles, no uno =================
    //
    // ---------- El fallo que esto arregla ----------
    //
    // La primera versión metió TODO en un solo panel: cabecera, multimedia, acciones,
    // sonido, por aplicación, salida, brillo, teclado, CPU/RAM, conectividad y sistema.
    // Once tarjetas. El log lo dijo con número:
    //
    //     panel control: necesita 1710 de alto y en la pantalla caben 1362
    //     panel abierto: control (380x1362)
    //
    // Mil setecientos diez píxeles de contenido para un panel colgado de un icono de la
    // isla. `AbrirPanel` hizo lo correcto —topar y envolver en scroll— y el resultado fue
    // una superficie negra de casi toda la pantalla con una barra al lado.
    //
    // Se pidieron DOS superficies desde el principio y yo las junté. El altavoz abre el
    // panel de AUDIO; el centro de control es otra cosa y se abre por su lado.
    //
    // La regla que queda escrita: **un panel colgado de la isla no pasa de `TopeDeAlto`**.
    // Si el contenido se pasa, sobra contenido, no falta pantalla.
    internal const double TopeDeAlto = 560;
    internal const double AnchoDePanel = 380;

    // El centro de control lleva más cosas que el de audio: rejilla de acciones de cuatro
    // columnas, dos métricas lado a lado y la tarjeta de fondo. 460 es el ancho donde eso
    // cabe sin estirarse y sin pasar del medio rango que se pidió (420..560).
    internal const double AnchoDelCentro = 460;

    // Cuánto mide de verdad el contenido que se le va a dar a un panel. Pública para
    // poder comprobar en una prueba que ninguno se pasa del tope.
    internal static double AltoQuePide(FrameworkElement contenido, double ancho)
    {
        contenido.Measure(new Size(ancho, double.PositiveInfinity));
        return contenido.DesiredSize.Height;
    }

    // ---------- El panel de audio ----------

    void AbrirAudio()
    {
        int cuantas = 0;
        if (Brillo.Disponible)
        {
            var p = Brillo.Pantallas();
            cuantas = p.Count;
            Brillo.Soltar(p);
        }
        double alto = Math.Min(TopeDeAlto, 300 + cuantas * 52);
        AlternarPanel(PanelAudio, CrearAudio, AnchoDePanel, alto, ancla: _anclaVolumen);
    }

    FrameworkElement CrearAudio()
    {
        var columna = new StackPanel { Margin = new Thickness(12, 4, 12, 8) };
        columna.Children.Add(CardVolumen());
        CardSalida(columna);
        CardPorAplicacion(columna);
        CardBrillo(columna);
        ApuntarPanel(PanelAudio, columna, AnchoDePanel, TopeDeAlto);
        // Envuelto en PanelShell: la superficie OPACA. Sin esto el contenido se apoya en el
        // fondo translúcido de la isla y por los huecos entre tarjetas se lee el escritorio,
        // que es lo que se veía. PanelShell existía desde el primer día y no se usaba.
        return PanelShell(columna, 0);
    }

    // El nombre del dispositivo de salida de ahora mismo, para poder demostrar que no
    // cambia. Solo lee.
    static string SalidaActual()
    {
        try
        {
            var s = Audio.Salidas().FirstOrDefault(x => x.EsLaActual);
            return s?.Nombre ?? "(ninguna)";
        }
        catch { return "(no se pudo leer)"; }
    }

    void ApuntarMultimedia(string cuando, long mutAntes, int volAntes, bool mudoAntes, string salidaAntes)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var e = await Musica.Leer();
                long mut = Musica.Mutaciones;
                int vol = Audio.Volumen();
                bool mudo = Audio.Silenciado;
                string salida = SalidaActual();

                Log.Info($"multimedia {cuando}: " +
                         $"sonando={(e?.Sonando == true ? "sí" : "no")} " +
                         $"titulo=\"{e?.Titulo ?? ""}\" app={e?.AppId ?? ""} " +
                         $"avance={(e?.Avance ?? 0) * 100:0}% " +
                         $"volumen={vol} mudo={(mudo ? "sí" : "no")} salida=\"{salida}\" " +
                         $"mutaciones={mut} (eran {mutAntes}; última: {Musica.UltimaMutacion})");

                if (mut != mutAntes)
                    Log.Info($"multimedia: ATENCIÓN, abrir el centro ha pedido {mut - mutAntes} cambio(s): {Musica.UltimaMutacion}");
                if (vol != volAntes || mudo != mudoAntes)
                    Log.Info($"multimedia: ATENCIÓN, el volumen ha cambiado ({volAntes}/{mudoAntes} → {vol}/{mudo})");
                if (salida != salidaAntes)
                    Log.Info($"multimedia: ATENCIÓN, la salida ha cambiado (\"{salidaAntes}\" → \"{salida}\")");
            }
            catch (Exception ex) { Log.Error("apuntar el estado multimedia", ex); }
        }, DispatcherPriority.Background);
    }

    // ---------- La traza que se pidió ----------
    //
    // Lo que de verdad hacía falta para encontrar esto: qué mide el contenido ANTES de
    // que el panel decida. Sin esta línea hubo que deducirlo del aviso de "se desplaza
    // por dentro", que es un síntoma y no la medida.
    // Medía SIEMPRE con `AnchoDePanel` (380) y `TopeDeAlto`, estuviera abriendo el panel
    // que estuviera: para el centro de control, que mide 460, la traza decía "ancho=380"
    // y un "pide" calculado a un ancho que no era el suyo. Ahora se le pasan los dos.
    void ApuntarPanel(string nombre, FrameworkElement contenido, double ancho, double tope)
    {
        try
        {
            int cuantas = contenido is Panel p ? p.Children.Count : 1;
            double pide = AltoQuePide(contenido, ancho);
            bool scroll = pide > tope;
            Log.Info($"panel abierto: {nombre} ancho={ancho:0} alto={Math.Min(pide, tope):0} " +
                     $"contenido={cuantas} tarjeta(s), pide={pide:0} scroll={(scroll ? "sí" : "no")}" +
                     (scroll ? $" — se pasa del tope ({tope:0}): se desplaza por dentro" : ""));
        }
        catch (Exception ex) { Log.Error($"medir el panel {nombre}", ex); }
    }

    // ---------- Cabecera: saludo, hora y fecha ----------
    UIElement CardCabeceraDelControl()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var izq = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        int h = DateTime.Now.Hour;
        var saludo = h < 6 ? "Buenas noches" : h < 13 ? "Buenos días" : h < 21 ? "Buenas tardes" : "Buenas noches";
        izq.Children.Add(new TextBlock
        {
            Text = saludo, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = _texto,
        });
        izq.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("dddd, d 'de' MMMM"),
            FontSize = 10.5, Foreground = _textoAtenuado, Margin = new Thickness(0, 1, 0, 0),
        });
        var perfil = Perfiles.LeerEstado().Activo;
        if (!string.IsNullOrWhiteSpace(perfil))
        {
            var p = Perfiles.Todos().FirstOrDefault(x => x.Id == perfil);
            izq.Children.Add(new TextBlock
            {
                Text = "Perfil: " + (p?.Nombre ?? perfil!),
                FontSize = 10, Foreground = _acentoLegible, Opacity = 0.9,
                Margin = new Thickness(0, 3, 0, 0),
            });
        }
        Grid.SetColumn(izq, 0);
        g.Children.Add(izq);

        var der = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var hora = new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm"),
            FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = _texto,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Typography.SetNumeralAlignment(hora, FontNumeralAlignment.Tabular);
        der.Children.Add(hora);
        var ajustes = BotonIcono(0xE713, "Abrir los ajustes de la nube", AbrirAjustes);
        ajustes.HorizontalAlignment = HorizontalAlignment.Right;
        ajustes.Margin = new Thickness(0, 4, -3, 0);
        der.Children.Add(ajustes);
        Grid.SetColumn(der, 1);
        g.Children.Add(der);

        return Card(g);
    }

    // ---------- Multimedia ----------
    //
    // El hueco se guarda y su CONTENIDO se cambia: así cuando la canción cambie se
    // sustituye la tarjeta de dentro y no el panel.
    UIElement CardMultimedia()
    {
        _ccMediaHueco = new Border { Margin = new Thickness(0, 0, 0, 0) };
        PintarMultimedia(null);
        // Leer el estado tarda (es una API asíncrona de Windows): el panel se abre ya y
        // esto lo rellena cuando llegue.
        Dispatcher.InvokeAsync(async () =>
        {
            try { PintarMultimedia(await Musica.Leer()); }
            catch (Exception ex) { Log.Error("leer la música para el centro de control", ex); }
        }, DispatcherPriority.Background);
        return _ccMediaHueco;
    }

    void PintarMultimedia(EstadoMusica? e)
    {
        if (_ccMediaHueco == null) return;
        _ccMediaHueco.Child = MediaCard(
            Musica.Caratula,
            e?.Titulo ?? "",
            e?.Artista,
            Notificaciones.NombreBonito(e?.AppId ?? ""),
            e?.Avance ?? 0,
            () => { Musica.Anterior(); RefrescarMultimedia(); },
            () => { Musica.PlayPausa(); RefrescarMultimedia(); },
            () => { Musica.Siguiente(); RefrescarMultimedia(); },
            e?.Sonando ?? false);
    }

    void RefrescarMultimedia() => Dispatcher.InvokeAsync(async () =>
    {
        // Un momento: el reproductor tarda en reflejar el cambio, y preguntarle al
        // instante devuelve el estado de antes.
        await Task.Delay(350);
        try { PintarMultimedia(await Musica.Leer()); } catch { }
    }, DispatcherPriority.Background);

    // ---------- Acciones rápidas ----------
    UIElement CardAccionesRapidas()
    {
        _ccAcciones = new UniformGrid { Columns = 4 };
        PintarAccionesRapidas();
        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xE8A9, "Acciones rápidas"));
        col.Children.Add(_ccAcciones);
        return Card(col);
    }

    void PintarAccionesRapidas()
    {
        if (_ccAcciones == null) return;
        _ccAcciones.Children.Clear();

        // "Sonido" lleva al panel de audio, que es donde vive el volumen. El altavoz de la
        // isla abre ese panel directamente; esto es el atajo desde aquí.
        _ccAcciones.Children.Add(QuickAction(0xE767, "Sonido", false,
            () => { CerrarPanel(); AbrirDesde(BotonAltavoz); }, "Volumen, salida, por aplicación y brillo"));

        bool mudo = Audio.Silenciado;
        _ccAcciones.Children.Add(QuickAction(mudo ? 0xE74F : 0xE767, "Silencio", mudo, () =>
        {
            Audio.Silenciar(!Audio.Silenciado);
            RefrescarVolumen();          // solo el volumen y estas pastillas
            PintarAccionesRapidas();
        }, mudo ? "Quitar el silencio" : "Silenciar todo"));

        bool nm = Centro.Actual.NoMolestar;
        _ccAcciones.Children.Add(QuickAction(nm ? 0xE7ED : 0xEA8F, "No molestar", nm, () =>
        {
            Centro.Actual.NoMolestar = !Centro.Actual.NoMolestar;
            Centro.Actual.Guardar();
            PintarAccionesRapidas();
        }, "Las notificaciones se siguen guardando, pero no asoman"));

        _ccAcciones.Children.Add(QuickAction(0xE91B, "Fondo", false,
            () => { CerrarPanel(); AbrirAjustes(); }, "Cambiar el fondo del escritorio"));

        _ccAcciones.Children.Add(QuickAction(0xE774, "Red", Red.Estado() != Red.Tipo.Sin,
            () => { CerrarPanel(); AbrirRed(); }, "Redes, velocidad y ajustes de red"));

        if (WallpaperEngine.Exe != null)
            _ccAcciones.Children.Add(QuickAction(0xE786, "Wallpaper", WallpaperEngine.Abierto,
                () => { CerrarPanel(); AbrirAjustes(); }, "Wallpaper Engine"));

        _ccAcciones.Children.Add(QuickAction(0xE7F4, "Ventanas", false,
            () => { CerrarPanel(); AbrirVista(); }, "Ver todas las ventanas abiertas"));


        // ---------- Las dos que solo estaban en la portada vieja ----------
        //
        // Pausar la escena de Wallpaper Engine y esconder los iconos del escritorio eran
        // accesos de la portada anterior y NO estaban aquí. Al unificar las dos
        // superficies se habrían perdido sin que se notara, que es justo lo que no puede
        // pasar: unificar no es quedarse con menos cosas.
        if (WallpaperEngine.Exe != null)
        {
            _ccAcciones.Children.Add(QuickAction(_wpPausado ? 0xE768 : 0xE769,
                "Pausar fondo", _wpPausado, () =>
                {
                    _wpPausado = !_wpPausado;
                    WallpaperEngine.Enviar(_wpPausado ? "pause" : "play");
                    PintarAccionesRapidas();
                }, _wpPausado ? "Reanudar la escena del fondo" : "Pausar la escena del fondo"));

            _ccAcciones.Children.Add(QuickAction(_iconosOcultos ? 0xE7B3 : 0xE8B7,
                "Iconos", _iconosOcultos, () =>
                {
                    _iconosOcultos = !_iconosOcultos;
                    WallpaperEngine.Enviar(_iconosOcultos ? "hideIcons" : "showIcons");
                    PintarAccionesRapidas();
                }, _iconosOcultos ? "Volver a enseñar los iconos del escritorio"
                                  : "Esconder los iconos del escritorio"));
        }
        if (Escritorios.Disponible)
            _ccAcciones.Children.Add(QuickAction(0xE7C4, "Escritorios", false,
                () => { CerrarPanel(); Escritorios.Crear(); }, "Crear un escritorio virtual"));
    }

    // ---------- Volumen ----------
    UIElement CardVolumen()
    {
        bool mudo = Audio.Silenciado;
        int v = Math.Max(0, Audio.Volumen());

        _ccCifraVolumen = new TextBlock
        {
            Text = mudo ? "Silenciado" : $"{v}%",
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = mudo ? _textoAtenuado : _acentoLegible,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetNumeralAlignment(_ccCifraVolumen, FontNumeralAlignment.Tabular);

        _ccMudo = BotonIcono(mudo ? 0xE74F : 0xE767, mudo ? "Quitar el silencio" : "Silenciar", () =>
        {
            Audio.Silenciar(!Audio.Silenciado);
            RefrescarVolumen();
            PintarAccionesRapidas();
        }, mudo);

        var cabecera = new StackPanel { Orientation = Orientation.Horizontal };
        cabecera.Children.Add(_ccCifraVolumen);
        var derecha = new StackPanel { Orientation = Orientation.Horizontal };
        derecha.Children.Add(_ccMudo);

        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xE767, "Sonido", derecha));

        // La salida actual, como subtítulo de la tarjeta: es el dato que falta cuando
        // subes el volumen y no suena nada porque va a otro sitio.
        var salidaActual = Audio.Salidas().FirstOrDefault(s => s.EsLaActual);
        if (salidaActual != null)
            col.Children.Add(new TextBlock
            {
                Text = salidaActual.Nombre, FontSize = 10.5, Foreground = _textoAtenuado,
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, -4, 0, 6),
            });

        var filaCifra = new Grid();
        filaCifra.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filaCifra.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filaCifra.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var menos = BotonIcono(0xE949, "Bajar 5", () => PasoDeVolumen(-5));
        Grid.SetColumn(menos, 0);
        filaCifra.Children.Add(menos);

        _ccVolumen = Slider(v, 0, 100,
            nv => { Audio.PonerVolumen((int)Math.Round(nv)); PintarCifraVolumen((int)Math.Round(nv)); },
            nv => { if (nv > 0 && Audio.Silenciado) { Audio.Silenciar(false); RefrescarVolumen(); PintarAccionesRapidas(); } },
            apagado: mudo, nombre: "Volumen general");
        _ccVolumen.Visual.Margin = new Thickness(2, 0, 2, 0);
        Grid.SetColumn(_ccVolumen.Visual, 1);
        filaCifra.Children.Add(_ccVolumen.Visual);

        var mas = BotonIcono(0xE948, "Subir 5", () => PasoDeVolumen(5));
        Grid.SetColumn(mas, 2);
        filaCifra.Children.Add(mas);

        // La rueda encima de la tarjeta, de 2 en 2, como siempre
        filaCifra.PreviewMouseWheel += (_, e) =>
        {
            PasoDeVolumen(e.Delta > 0 ? 2 : -2);
            e.Handled = true;
        };

        col.Children.Add(filaCifra);
        return Card(col);
    }

    void PasoDeVolumen(int paso)
    {
        int v = Math.Clamp(Math.Max(0, Audio.Volumen()) + paso, 0, 100);
        Audio.PonerVolumen(v);
        if (paso > 0 && Audio.Silenciado) Audio.Silenciar(false);
        RefrescarVolumen();
        PintarAccionesRapidas();
    }

    void PintarCifraVolumen(int v)
    {
        if (_ccCifraVolumen == null) return;
        bool mudo = Audio.Silenciado;
        _ccCifraVolumen.Text = mudo ? "Silenciado" : $"{v}%";
        _ccCifraVolumen.Foreground = mudo ? _textoAtenuado : _acentoLegible;
    }

    // Solo el volumen: ni se cierra el panel ni se remonta nada.
    void RefrescarVolumen()
    {
        int v = Math.Max(0, Audio.Volumen());
        _ccVolumen?.Poner?.Invoke(v);
        PintarCifraVolumen(v);
        if (_ccMudo != null)
        {
            bool mudo = Audio.Silenciado;
            _ccMudo.Content = IconoDe(mudo ? 0xE74F : 0xE767, mudo);
            _ccMudo.ToolTip = mudo ? "Quitar el silencio" : "Silenciar";
        }
    }

    TextBlock IconoDe(int codigo, bool activo)
    {
        var ic = Glifo(char.ConvertFromUtf32(codigo));
        ic.FontSize = 14;
        ic.Foreground = activo ? _acentoLegible : _texto;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        ic.VerticalAlignment = VerticalAlignment.Center;
        return ic;
    }

    // ---------- Volumen por aplicación ----------
    void CardPorAplicacion(StackPanel columna)
    {
        var sesiones = Audio.Sesiones().Where(s => s.Pid != Environment.ProcessId).ToList();
        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xECAA, "Por aplicación",
            new TextBlock
            {
                Text = sesiones.Count == 0 ? "" : $"{sesiones.Count}",
                FontSize = 11, Foreground = _textoAtenuado, VerticalAlignment = VerticalAlignment.Center,
            }));

        if (sesiones.Count == 0)
        {
            col.Children.Add(EmptyState(0xE767, "Nada reproduciendo sonido",
                                        "Las aplicaciones aparecerán aquí con su propio volumen"));
            columna.Children.Add(Card(col));
            return;
        }

        var pila = new StackPanel();
        foreach (var s in sesiones) pila.Children.Add(FilaDeAplicacion(s));

        // Con muchas, scroll dentro: el panel no crece hasta el infinito.
        if (sesiones.Count > 4)
            col.Children.Add(new ScrollViewer
            {
                Content = pila,
                MaxHeight = 168,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            });
        else col.Children.Add(pila);

        columna.Children.Add(Card(col));
    }

    UIElement FilaDeAplicacion(Audio.Sesion s)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // El icono de la aplicación, si se puede sacar de su ejecutable.
        var hueco = new Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(5),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        ImageSource? icono = null;
        // El icono sale de la ventana de ese mismo proceso, que ya lo tiene cacheado el
        // gestor de ventanas. `Audio.Sesion` no trae icono y no voy a añadirle uno: eso
        // sería tocar la lógica de audio en una fase de presentación.
        try { icono = GestorVentanas.Todas.FirstOrDefault(v => v.Pid == s.Pid)?.Icono; } catch { }
        if (icono != null) hueco.Child = new Image { Source = icono, Stretch = Stretch.Uniform };
        else
        {
            var ic = Glifo(char.ConvertFromUtf32(0xECAA));
            ic.FontSize = 11;
            ic.Foreground = _textoAtenuado;
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            hueco.Child = ic;
        }
        Grid.SetColumn(hueco, 0);
        g.Children.Add(hueco);

        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var cifra = new TextBlock
        {
            Text = $"{s.Volumen}%", FontSize = 9.5, Foreground = _textoAtenuado,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nombre = new TextBlock
        {
            Text = s.Nombre, FontSize = 11.5, Foreground = _texto,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = s.Silenciada ? 0.45 : 1,
        };
        fila.Children.Add(nombre);
        Grid.SetColumn(cifra, 1);
        fila.Children.Add(cifra);
        col.Children.Add(fila);

        var mando = Slider(s.Volumen, 0, 100,
            nv => { Audio.PonerVolumenSesion(s, (int)Math.Round(nv)); cifra.Text = $"{(int)Math.Round(nv)}%"; },
            apagado: s.Silenciada, nombre: $"Volumen de {s.Nombre}");
        col.Children.Add(mando.Visual);
        Grid.SetColumn(col, 1);
        g.Children.Add(col);

        var mudo = BotonIcono(s.Silenciada ? 0xE74F : 0xE767,
                              s.Silenciada ? "Quitar el silencio de " + s.Nombre : "Silenciar " + s.Nombre,
                              () =>
                              {
                                  Audio.SilenciarSesion(s, !s.Silenciada);
                                  // Solo esta fila: el resto del panel no se entera.
                                  nombre.Opacity = s.Silenciada ? 0.45 : 1;
                              }, s.Silenciada);
        Grid.SetColumn(mudo, 2);
        g.Children.Add(mudo);

        return g;
    }

    // ---------- Salida ----------
    void CardSalida(StackPanel columna)
    {
        var salidas = Audio.Salidas();
        if (salidas.Count == 0 || !Audio.PuedeCambiarSalida) return;

        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xE7F5, "Salida de audio"));
        foreach (var s in salidas)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ic = Glifo(char.ConvertFromUtf32(0xE7F5));
            ic.FontSize = 13;
            ic.Foreground = s.EsLaActual ? _acentoLegible : _texto;
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 9, 0);
            Grid.SetColumn(ic, 0);
            g.Children.Add(ic);

            var t = new TextBlock
            {
                Text = s.Nombre, FontSize = 11.5,
                Foreground = s.EsLaActual ? _acentoLegible : _texto,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(t, 1);
            g.Children.Add(t);

            if (s.EsLaActual)
            {
                var badge = StatusBadge("Activa", true);
                Grid.SetColumn(badge, 2);
                g.Children.Add(badge);
            }

            var elegida = s;
            var boton = new Button
            {
                Content = g,
                Template = PlantillaDeFila(),
                MinHeight = AltoTocable,
                Cursor = Cursors.Hand,
                ToolTip = s.EsLaActual ? "Ya es la salida actual" : "Usar " + s.Nombre,
                IsEnabled = !s.EsLaActual,
            };
            // El contenido es un `Grid` con el icono y el nombre, así que para UIA no tiene
            // nombre. El del dispositivo es lo único que distingue una fila de otra.
            System.Windows.Automation.AutomationProperties.SetName(
                boton, s.EsLaActual ? $"{s.Nombre} (salida actual)" : $"Usar {s.Nombre}");
            boton.Click += (_, _) => CambiarLaSalidaYEsperarla(elegida.Id);
            col.Children.Add(boton);
        }
        columna.Children.Add(Card(col));
    }

    // ---------- Cambiar de salida y ESPERAR a que sea verdad ----------
    //
    // Esto hacía `CambiarSalida(...)` y repintaba en la línea siguiente. El sonido cambiaba
    // de altavoz, pero el panel seguía marcando «Activa» en el de antes hasta que lo cerrabas
    // y lo abrías. Lo contó el usuario: «cuando cambio el audio y pongo otro se aplica, pero
    // en el panel no pone activo el nuevo, tengo que recargar».
    //
    // El motivo no es que falte repintar — repintaba. Es que `SetDefaultEndpoint` vuelve
    // antes de que Windows haya terminado de mover el dispositivo predeterminado: al
    // preguntar inmediatamente después, el sistema todavía contesta el viejo. Se repintaba
    // bien un dato que aún era el anterior.
    //
    // Así que se pregunta hasta que la respuesta cambie, con tope. Y si no llega a cambiar,
    // se repinta igual: entonces lo que el panel enseña es la verdad —no se movió—, que es
    // mejor que dejarlo marcando una salida que tampoco es.
    //
    // Nada de bloquear esperando: esto cuelga de un clic y el hilo que lo atiende es el que
    // pinta. Un `Sleep` aquí congela la barra entera.
    void CambiarLaSalidaYEsperarla(string id)
    {
        try
        {
            if (!Audio.CambiarSalida(id))
            {
                // No se pudo ni intentar (Windows que no lo permite, o modo de solo lectura).
                // Se repinta para que el panel diga lo que de verdad hay.
                RefrescarControlEntero("no se pudo cambiar la salida de audio");
                return;
            }

            int intentos = 0;
            var reloj = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            reloj.Tick += (_, _) =>
            {
                // Si has cerrado el panel mientras tanto, se deja de preguntar. Seguir sería
                // enumerar los dispositivos de audio dieciséis veces para repintar algo que
                // ya no está en pantalla.
                if (_panelAbierto != PanelDelCentro) { reloj.Stop(); return; }

                bool yaEs;
                try { yaEs = Audio.Salidas().Any(s => s.EsLaActual && s.Id == id); }
                catch { yaEs = false; }

                // Hasta ~1 segundo. Pasado eso no es que tarde: es que no va a cambiar.
                if (!yaEs && ++intentos < 16) return;

                reloj.Stop();
                RefrescarControlEntero(yaEs
                    ? "cambió la salida de audio"
                    : "la salida de audio no llegó a cambiar");
            };
            reloj.Start();
        }
        catch (Exception ex) { Log.Error("cambiar la salida de audio", ex); }
    }

    // ---------- Brillo ----------
    void CardBrillo(StackPanel columna)
    {
        var pantallas = Brillo.Pantallas();
        if (pantallas.Count == 0) return;

        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xE706, "Brillo"));
        foreach (var p in pantallas)
        {
            var cifra = new TextBlock
            {
                Text = $"{p.PorCiento}%", FontSize = 10, Foreground = _textoAtenuado,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            if (pantallas.Count > 1)
                col.Children.Add(new TextBlock
                {
                    Text = p.Nombre, FontSize = 10.5, Foreground = _textoAtenuado,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            var fila = new Grid();
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            var pant = p;
            var mando = Slider(p.PorCiento, 0, 100,
                nv => cifra.Text = $"{(int)Math.Round(nv)}%",
                nv => { Brillo.Poner(pant, (int)Math.Round(nv)); AvisoDeBrillo((int)Math.Round(nv)); },
                nombre: $"Brillo de {pant.Nombre}");
            fila.Children.Add(mando.Visual);
            Grid.SetColumn(cifra, 1);
            fila.Children.Add(cifra);
            col.Children.Add(fila);
        }
        columna.Children.Add(Card(col));
    }

    // ---------- Teclado ----------
    void CardTeclado(StackPanel columna)
    {
        var distribuciones = Idioma.Lista();
        if (distribuciones.Count <= 1) return;

        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xE765, "Teclado"));
        var rejilla = new UniformGrid { Columns = Math.Min(4, distribuciones.Count) };
        var actual = Idioma.Actual();
        foreach (var d in distribuciones)
        {
            var dd = d;
            rejilla.Children.Add(QuickAction(0xE765, d.Corto, d.Hkl == actual,
                () => { Idioma.Poner(dd); RefrescarControlEntero("cambió el teclado"); }, d.Nombre));
        }
        col.Children.Add(rejilla);
        columna.Children.Add(Card(col));
    }

    // ---------- CPU y RAM ----------
    //
    // Dos tarjetas de métrica lado a lado. Se actualizan con `Poner`, que repinta el
    // número y la polilínea y **nada más**: ni el panel ni la tarjeta se reconstruyen.
    UIElement CardMetricas()
    {
        var cpu = Nativo.Cpu();
        var ram = Nativo.Ram();
        _ccCpu = MetricCard("CPU", 0xE950, cpu, Historial.Cpu.ToList());
        _ccRam = MetricCard("RAM", 0xE964, ram, Historial.Ram.ToList());

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _ccCpu.Visual.Margin = new Thickness(0, 0, 5, HuecoCard);
        _ccRam.Visual.Margin = new Thickness(5, 0, 0, HuecoCard);
        Grid.SetColumn(_ccCpu.Visual, 0);
        g.Children.Add(_ccCpu.Visual);
        Grid.SetColumn(_ccRam.Visual, 1);
        g.Children.Add(_ccRam.Visual);
        return g;
    }

    // Lo llama el Tick de la isla, que ya corre una vez por segundo. No hay reloj nuevo.
    void RefrescarMetricasDelControl()
    {
        if (_panelAbierto != PanelDelCentro) return;
        try
        {
            _ccCpu?.Poner?.Invoke(Nativo.Cpu(), Historial.Cpu.ToList());
            _ccRam?.Poner?.Invoke(Nativo.Ram(), Historial.Ram.ToList());
        }
        catch { }
    }

    // ---------- Conectividad ----------
    UIElement CardConectividad()
    {
        var col = new StackPanel();
        var tipo = Red.Estado();
        var texto = tipo switch
        {
            Red.Tipo.Cable => "Conectado por cable",
            Red.Tipo.Wifi => "Wi-Fi",
            _ => "Sin conexión",
        };
        col.Children.Add(SectionHeader(tipo == Red.Tipo.Cable ? 0xE839 : tipo == Red.Tipo.Wifi ? 0xE701 : 0xEB55,
                                       "Conectividad", StatusBadge(texto, tipo != Red.Tipo.Sin, tipo == Red.Tipo.Sin)));

        var irRed = new Button
        {
            Content = FilaConFlecha(0xE774, "Redes y velocidad", "Wi-Fi, conectar y medir"),
            Template = PlantillaDeFila(),
            MinHeight = AltoTocable,
            Cursor = Cursors.Hand,
            ToolTip = "Ver las redes wifi, conectarse y medir la velocidad",
        };
        System.Windows.Automation.AutomationProperties.SetName(irRed, "Redes y velocidad");
        irRed.Click += (_, _) => { CerrarPanel(); AbrirRed(); };
        col.Children.Add(irRed);

        // Bluetooth: se rellena por detrás porque preguntar tarda, y cuando llega se
        // cambia SOLO esta pila.
        _ccBluetooth = new StackPanel();
        _ccBluetooth.Children.Add(new TextBlock
        {
            Text = "Bluetooth: consultando…", FontSize = 11, Foreground = _textoAtenuado,
            Margin = new Thickness(2, 6, 0, 0),
        });
        col.Children.Add(_ccBluetooth);
        PedirBluetooth();

        return Card(col);
    }

    UIElement FilaConFlecha(int icono, string titulo, string? sub)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 13;
        ic.Foreground = _texto;
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 9, 0);
        Grid.SetColumn(ic, 0);
        g.Children.Add(ic);

        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        col.Children.Add(new TextBlock { Text = titulo, FontSize = 11.5, Foreground = _texto });
        if (!string.IsNullOrWhiteSpace(sub))
            col.Children.Add(new TextBlock { Text = sub!, FontSize = 10, Foreground = _textoAtenuado });
        Grid.SetColumn(col, 1);
        g.Children.Add(col);

        var flecha = Glifo(char.ConvertFromUtf32(0xE76C));
        flecha.FontSize = 11;
        flecha.Foreground = _textoAtenuado;
        flecha.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(flecha, 2);
        g.Children.Add(flecha);
        return g;
    }

    void PedirBluetooth() => Dispatcher.InvokeAsync(async () =>
    {
        try
        {
            var est = await Bluetooth.Estado();
            if (_ccBluetooth == null || _panelAbierto != PanelDelCentro) return;
            _ccBluetooth.Children.Clear();

            string dice = est switch
            {
                Bluetooth.EstadoRadio.Encendida => "Encendido",
                Bluetooth.EstadoRadio.Apagada => "Apagado",
                Bluetooth.EstadoRadio.SinPermiso => "Sin permiso",
                _ => "No hay",
            };

            if (est is Bluetooth.EstadoRadio.Encendida or Bluetooth.EstadoRadio.Apagada)
            {
                bool on = est == Bluetooth.EstadoRadio.Encendida;
                _ccBluetooth.Children.Add(Toggle("Bluetooth", on, async _ =>
                {
                    await Bluetooth.Encender(!on);
                    // Solo el bluetooth: el panel se queda donde está, con su tamaño,
                    // su scroll y su foco. Antes esto cerraba y reabría el panel entero.
                    PedirBluetooth();
                    PintarAccionesRapidas();
                }, dice));

                foreach (var a in (await Bluetooth.Emparejados()).Take(5))
                {
                    var g = new Grid { Margin = new Thickness(2, 2, 0, 0) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.Children.Add(new TextBlock
                    {
                        Text = a.Nombre, FontSize = 11, Foreground = _texto,
                        Opacity = a.Conectado ? 1 : 0.65,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                    if (a.Conectado)
                    {
                        var b = StatusBadge("Conectado", true);
                        Grid.SetColumn(b, 1);
                        g.Children.Add(b);
                    }
                    _ccBluetooth.Children.Add(g);
                }
            }
            else
                _ccBluetooth.Children.Add(new TextBlock
                {
                    Text = "Bluetooth: " + dice.ToLowerInvariant(),
                    FontSize = 11, Foreground = _textoAtenuado, Margin = new Thickness(2, 6, 0, 0),
                });
        }
        catch (Exception ex) { Log.Error("rellenar el bluetooth del centro de control", ex); }
    }, DispatcherPriority.Background);

    // ---------- El fondo actual ----------
    //
    // Miniatura, nombre, tipo y monitor. La miniatura sale de lo que el gestor de fondos
    // tenga puesto de verdad; si no hay nada que enseñar, un hueco con su explicación en
    // vez de un recuadro gris que parece un fallo.
    UIElement CardFondo()
    {
        var col = new StackPanel();
        var cfg = _cfg.Fondo;
        var tipo = Fondos.TipoEfectivo(cfg?.Tipo, Fondos.AnimadoDisponible);

        string comoSeLlama = tipo switch
        {
            "solido" => "Color sólido",
            "degradado" => "Degradado",
            "imagen" => "Imagen",
            "presentacion" => "Presentación",
            "animado" => "Wallpaper Engine",
            _ => "No gestionado por NubeBar",
        };

        col.Children.Add(SectionHeader(0xE91B, "Fondo", StatusBadge(comoSeLlama, tipo != "ninguno", tipo == "ninguno")));

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // La miniatura: 60x38, con las esquinas redondeadas y sin deformar.
        var hueco = new Border
        {
            Width = 60, Height = 38,
            CornerRadius = new CornerRadius(7),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 11, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        string? ruta = null;
        try
        {
            var m = Monitores.Principal();
            var ef = Fondos.Efectivo(cfg ?? new Fondo(), m.Clave);
            // Para una imagen o una presentación, lo que haya puesto; para un sólido o un
            // degradado no hace falta fichero: se pinta el color directamente.
            if (tipo is "imagen" or "presentacion") ruta = ef.Ruta ?? ef.CarpetaPresentacion;
        }
        catch { }

        if (tipo == "solido" && cfg != null)
            hueco.Background = PincelSolido(Paleta.Leer(cfg.Color1, _roles.SuperficieAltaSolida));
        else if (tipo == "degradado" && cfg != null)
        {
            double rad = cfg.Angulo * Math.PI / 180.0;
            hueco.Background = new LinearGradientBrush(
                Paleta.Leer(cfg.Color1, Colors.Black),
                Paleta.Leer(cfg.Color2, Colors.White),
                new Point(0.5 - Math.Cos(rad) / 2, 0.5 - Math.Sin(rad) / 2),
                new Point(0.5 + Math.Cos(rad) / 2, 0.5 + Math.Sin(rad) / 2));
        }
        else if (ruta != null && System.IO.File.Exists(ruta))
        {
            try
            {
                // `DecodePixelWidth` para no leer 20 MB para una tira de 60 px, y `OnLoad`
                // para no quedarse el fichero tomado.
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(ruta);
                bmp.DecodePixelWidth = 160;
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                hueco.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
            }
            catch (Exception ex) { Log.Error("miniatura del fondo en el centro de control", ex); }
        }

        if (hueco.Child == null && tipo is "ninguno" or "imagen" or "presentacion")
        {
            var ic = Glifo(char.ConvertFromUtf32(0xE91B));
            ic.FontSize = 15;
            ic.Foreground = _textoAtenuado;
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            hueco.Child = ic;
        }
        Grid.SetColumn(hueco, 0);
        g.Children.Add(hueco);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = tipo == "animado" && WallpaperEngine.Abierto
                ? "Wallpaper Engine en marcha"
                : ruta != null ? System.IO.Path.GetFileName(ruta) : comoSeLlama,
            FontSize = 11.5, Foreground = _texto, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var cuantas = Monitores.Todos().Count;
        textos.Children.Add(new TextBlock
        {
            Text = cuantas > 1 ? $"{cuantas} pantallas" : "1 pantalla",
            FontSize = 10, Foreground = _textoAtenuado,
        });
        Grid.SetColumn(textos, 1);
        g.Children.Add(textos);

        var personalizar = BotonIcono(0xE713, "Abrir los ajustes del fondo",
                                      () => { CerrarPanel(); AbrirAjustes(); });
        Grid.SetColumn(personalizar, 2);
        g.Children.Add(personalizar);

        col.Children.Add(g);
        return Card(col);
    }

    // ---------- Acciones del sistema ----------
    //
    // Con etiqueta, no cuatro iconos sueltos: un icono de apagado sin rótulo se pulsa por
    // error una vez y ya no se vuelve a abrir el panel con confianza.
    UIElement CardAccionesDelSistema()
    {
        var col = new StackPanel();
        col.Children.Add(SectionHeader(0xE713, "Sistema"));
        var rejilla = new UniformGrid { Columns = 4 };
        rejilla.Children.Add(QuickAction(0xE72E, "Bloquear", false,
            () => { CerrarPanel(); Energia.Bloquear(); }, "Bloquear la sesión"));
        rejilla.Children.Add(QuickAction(0xE708, "Suspender", false,
            () => { CerrarPanel(); Energia.Suspender(); }, "Suspender el equipo"));
        rejilla.Children.Add(QuickAction(0xE777, "Reiniciar", false,
            () => { CerrarPanel(); Energia.Reiniciar(); }, "Reiniciar el equipo"));
        rejilla.Children.Add(AccionDelicada(0xE7E8, "Apagar",
            () => { CerrarPanel(); Energia.Apagar(); }, "Apagar el equipo"));
        col.Children.Add(rejilla);
        return Card(col);
    }

    // ---------- Refrescar ----------
    //
    // Esto es lo único que queda de reconstruir el panel entero, y solo se llama cuando
    // cambia una LISTA: otra salida de audio elegida, otro teclado puesto. Lo demás se
    // actualiza en su sitio.
    //
    // Y cuando toca, se reconstruye el CONTENIDO sin cerrar el panel: así no hay
    // animación de alto ni salto de posición.
    void RefrescarControlEntero(string porque)
    {
        if (_panelAbierto != PanelDelCentro) return;
        Log.Info("centro de control: se rehace el contenido porque " + porque);
        // La superficie es la de Inicio: se rehace SU contenido, con su buscador, su lista
        // y su portada. Antes esto montaba un panel "control" aparte, que era la segunda
        // pantalla con lo mismo.
        var contenido = CrearMenuInicio();
        if (PanelSuperpuestoAbierto && _superpuesta is { } v) v.Cajon.Content = contenido;
        else Cajon.Content = contenido;
        ReajustarPanel(PanelDelCentro);
    }

    // Se conserva el nombre de siempre para quien lo llamara, pero ya NO cierra el panel.
    void RefrescarControl() => RefrescarControlEntero("lo pidió un control");

    TextBlock Titulito(string texto) => new()
    {
        Text = texto, Foreground = _acentoLegible, FontSize = 11, Opacity = 0.9,
        Margin = new Thickness(0, 8, 0, 2)
    };

    Slider Deslizador(double valor, Action<double> alSoltar)
    {
        var s = new Slider
        {
            Minimum = 0, Maximum = 100, Value = Math.Clamp(valor, 0, 100),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = _acento,
            IsMoveToPointEnabled = true
        };
        // Se aplica al soltar y no en cada píxel: con el volumen por app, mandar
        // cincuenta cambios por segundo a Core Audio hace que crepite
        s.AddHandler(System.Windows.Input.Mouse.PreviewMouseUpEvent,
                     new System.Windows.Input.MouseButtonEventHandler((_, _) => alSoltar(s.Value)), true);
        s.KeyUp += (_, _) => alSoltar(s.Value);
        return s;
    }
}
