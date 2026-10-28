using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NubeBar;

// Los paneles que nacen de la isla (mini lista de grupos, vista general, menú Inicio).
// No son ventanas nuevas: la isla es una superficie de dos filas y lo único que pasa es
// que crece de alto, así que el panel se despliega desde dentro de ella.
public partial class MainWindow
{
    string? _panelAbierto;          // nombre del panel que hay ahora mismo, o null
    bool _panelConTeclado;          // le quitamos WS_EX_NOACTIVATE mientras está abierto
    double _panelAlto, _panelAncho; // medidas del panel actual

    // ---------- E0: el tablero de superficies ----------
    //
    // Uno por isla, porque cada pantalla tiene las suyas y cerrar el panel de una no puede
    // cerrar el de la otra.
    //
    // De momento va **en paralelo** a `_panelAbierto` y en modo clásico: no manda sobre nada,
    // se le cuenta lo que pasa. Es a propósito y no es código muerto — es la forma de que el
    // tablero esté vivo, con datos de verdad y mirable desde un diagnóstico, antes de que
    // nada dependa de él. Cuando la composición E se encienda, el tablero pasa a mandar y
    // `_panelAbierto` se queda siendo lo que siempre fue: el panel clásico de la isla.
    //
    // Y mientras tanto, si los dos dijeran cosas distintas sería un fallo. `--diage0` lo
    // comprueba en vez de confiar en que no pase.
    internal readonly Superficies.Tablero _mesa = new() { ModoClasico = true };

    internal string DuenoDeLaMesa => _esJefa ? "principal" : $"monitor-{_monitor.Handle}";

    // Un solo sitio donde contárselo, para no tener que acordarse en los veinte que abren.
    void ApuntarPanelEnLaMesa(string nombre, double ancho, double alto, bool teclado)
    {
        try
        {
            _mesa.Abrir(Superficies.Nueva(nombre, Superficies.Clase.Panel, DuenoDeLaMesa) with
            {
                Ancho = ancho,
                Alto = alto,
                PideTeclado = teclado,
            });
        }
        catch (Exception ex) { Log.Error("apuntar el panel en el tablero de superficies", ex); }
    }

    void OlvidarPanelEnLaMesa(string? nombre)
    {
        try { if (nombre is { Length: > 0 }) _mesa.Cerrar(nombre); }
        catch (Exception ex) { Log.Error("olvidar el panel en el tablero", ex); }
    }

    public bool HayPanel => _panelAbierto != null;

    // ---------- Colocación ----------

    // Con la barra arriba el cajón va debajo de los módulos; con la barra abajo, encima,
    // para que la isla crezca siempre hacia el centro de la pantalla
    void ColocarCajon()
    {
        Grid.SetRow(Contenido, Arriba ? 0 : 1);
        Grid.SetRow(Cajon, Arriba ? 1 : 0);
        FilaIsla.Height = GridLength.Auto;
        FilaCajon.Height = new GridLength(1, GridUnitType.Star);
        if (!Arriba) { FilaIsla.Height = new GridLength(1, GridUnitType.Star); FilaCajon.Height = GridLength.Auto; }

        Isla.VerticalAlignment = Arriba ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        Isla.Margin = EsIsla
            ? new Thickness(0, Arriba ? _cfg.Barra.Margen : 0, 0, Arriba ? 0 : _cfg.Barra.Margen)
            : new Thickness(_cfg.Barra.Margen, Arriba ? _cfg.Barra.Margen : 0,
                            _cfg.Barra.Margen, Arriba ? 0 : _cfg.Barra.Margen);
    }

    // Alto que necesita la ventana para que quepa la isla con su cajón
    void AjustarVentana(double altoIsla)
    {
        ColocarEnMonitor(altoIsla + _cfg.Barra.Margen * 2);
    }

    // ---------- El cajón en forma de tarjeta ----------
    //
    // Un panel que cuelga de un icono del carrusel (la mini lista de ventanas de
    // un programa con varias abiertas) no puede pedir "encoger": al estrecharse la
    // isla, el icono se mueve de debajo del cursor, la isla cree que te has ido y
    // cierra el panel justo cuando ibas a usarlo. Pero dejar el cajón a lo ancho de
    // la isla pintaba una losa negra de punta a punta de la pantalla para escribir
    // dos títulos de ventana, y quedaba fatal.
    //
    // La salida es no tocar las MEDIDAS y tocar solo lo que se dibuja: la isla sigue
    // midiendo lo mismo —y por eso el icono no se mueve de debajo del cursor— pero un
    // recorte deja a la vista, debajo de la barra, únicamente una tarjeta redondeada
    // del ancho del panel y pegada a su icono. La tarjeta se mete por debajo del filo
    // de la barra tanto como el radio de la isla, para que la unión de las dos formas
    // no deje muescas y parezca una sola superficie con un saliente.
    //
    // El recorte también quita el ratón de lo que ya no se ve, y eso viene bien: el
    // hueco negro de los lados dejaba la lista abierta aunque te hubieras ido lejos.
    //
    // El recorte se rehace en cada fotograma (Isla.SizeChanged salta con la
    // animación de alto), así que la tarjeta se despliega y se repliega con la isla.
    const double RadioTarjeta = 12;
    FrameworkElement? _tarjetaAncla;        // el icono del que cuelga la tarjeta
    FrameworkElement? _tarjetaContenido;    // lo que va dentro, para moverlo con ella
    double _tarjetaAncho;

    void RecortarCajon(FrameworkElement ancla, double ancho)
    {
        _tarjetaAncla = ancla;
        _tarjetaAncho = ancho;
        AplicarRecorteDelCajon();
    }

    // ---------- El tamaño de los paneles según la pantalla ----------
    //
    // Los paneles tienen medidas fijas —380 el de audio, 660 el Inicio, 1260 la vista
    // general— pensadas en una pantalla de 2560x1440 al 100 %, que da 1440 de alto en
    // unidades de WPF. En un 1920x1080 al 125 %, que es lo normal en un portátil, quedan
    // 864: el panel de audio ocupaba dos tercios de la pantalla y la vista general casi
    // todo el ancho. Lo contó el usuario: «la app no está preparada para 1920x1080, los
    // menús se agrandan y son demasiado grandes».
    //
    // Así que el panel entero se escala —texto, márgenes y tarjetas a la vez, con un
    // `LayoutTransform`, que es lo que hace que también MIDA menos— en proporción al alto
    // de la pantalla, sin bajar de `EscalaMinima` para que el texto se siga leyendo.
    // `Barra.EscalaPaneles` en el config.json lo fija a mano.
    //
    // Y mira también el ANCHO, no solo el alto: un monitor vertical de 1080 de ancho, o un
    // portátil estrecho, se quedan cortos de lado aunque tengan alto de sobra.
    internal const double AltoDeReferencia = 1000, AnchoDeReferencia = 1280, EscalaMinima = 0.8;

    internal static double EscalaDePanelesPura(double altoPantallaDip, double anchoPantallaDip,
                                               double forzada) =>
        forzada > 0
            ? Math.Clamp(forzada, 0.5, 1.5)
            : Math.Clamp(Math.Min(altoPantallaDip / AltoDeReferencia,
                                  anchoPantallaDip / AnchoDeReferencia), EscalaMinima, 1.0);

    double EscalaDePaneles()
    {
        double s = Math.Max(0.1, _monitor.Escala);
        return EscalaDePanelesPura(_monitor.Alto / s, _monitor.Ancho / s, _cfg.Barra.EscalaPaneles);
    }

    static void Escalar(FrameworkElement contenido, double f)
    {
        if (Math.Abs(EscalaDe(contenido) - f) < 0.001) return;
        contenido.LayoutTransform = Math.Abs(f - 1) < 0.001 ? Transform.Identity : new ScaleTransform(f, f);
    }

    static double EscalaDe(FrameworkElement e) =>
        e.LayoutTransform is ScaleTransform s && s.ScaleX > 0 ? s.ScaleX : 1.0;

    // ---------- Cambiar lo que enseña un panel ya abierto ----------
    //
    // Varios paneles se repintan poniendo un contenido nuevo en el cajón sin cerrarlo. Si
    // el panel cuelga de un icono, el ancho y el margen de la tarjeta se los pone
    // `AplicarRecorteDelCajon` al contenido que ESTABA, y el nuevo llegaba sin ellos: se
    // estiraba a la isla entera mientras el recorte seguía enseñando el trozo de la
    // tarjeta. Visto desde fuera, el panel «se hacía zoom» al pulsar cualquier botón. Lo
    // contó el usuario con el de batería al cambiar de modo de energía.
    void CambiarContenidoDelPanel(FrameworkElement contenido)
    {
        Escalar(contenido, EscalaDePaneles());
        if (PanelSuperpuestoAbierto && _superpuesta is { } v) { v.Cajon.Content = contenido; return; }
        Cajon.Content = contenido;
        if (_tarjetaAncla != null) { _tarjetaContenido = contenido; AplicarRecorteDelCajon(); }
    }

    void SinRecorteDelCajon()
    {
        if (_tarjetaAncla == null) return;
        // Se le devuelve el ancho libre: si el siguiente panel se abre SIN ancla, tiene
        // que poder ocupar el cajón entero y no quedarse con el ancho del anterior.
        if (_tarjetaContenido != null)
        {
            _tarjetaContenido.Width = double.NaN;
            _tarjetaContenido.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        _tarjetaAncla = null;
        _tarjetaContenido = null;
        _tarjetaAncho = 0;
        Isla.Clip = null;
    }
    // Dónde empieza la tarjeta, en coordenadas de la isla: centrada bajo su icono y
    // sin salirse por ningún lado. Se recalcula en cada fotograma y no se apunta al
    // abrir, porque la isla se expande DESDE EL CENTRO: mientras crece, el icono se
    // mueve dentro de ella y una x de antes dejaba la tarjeta a medio camino. Con la
    // isla recogida y más estrecha que el propio panel, directamente en la esquina.
    // La cuenta, aparte del árbol visual: dado el ancho de la isla, el del panel y el
    // centro del icono que lo abre, dónde empieza la tarjeta. Está fuera para poder
    // comprobarla con la isla arriba, abajo y recogida sin abrir ninguna ventana.
    internal static double XdeTarjetaPura(double anchoIsla, double anchoPanel,
                                          double centroAncla, double borde)
    {
        double w = Math.Min(anchoPanel, anchoIsla);
        borde = Math.Max(2, borde);
        return Math.Clamp(centroAncla - w / 2, borde, Math.Max(borde, anchoIsla - w - borde));
    }

    double XdeTarjeta()
    {
        double centro = Isla.ActualWidth / 2;
        try
        {
            if (_tarjetaAncla is { IsVisible: true } ancla && ancla.ActualWidth > 0)
                centro = ancla.TransformToAncestor(Isla).Transform(new Point(0, 0)).X + ancla.ActualWidth / 2;
        }
        catch { }
        return XdeTarjetaPura(Isla.ActualWidth, _tarjetaAncho, centro, Isla.BorderThickness.Left);
    }

    void AplicarRecorteDelCajon()
    {
        if (_tarjetaAncla == null) { Isla.Clip = null; return; }
        try
        {
            double ancho = Isla.ActualWidth, alto = Isla.ActualHeight;
            if (ancho <= 0 || alto <= 0) return;

            double radio = Isla.CornerRadius.TopLeft;
            double barra = Math.Min(_cfg.Barra.Alto, alto);
            double cajon = Math.Max(0, alto - barra);

            // La barra de siempre, con sus esquinas de siempre
            var fila = new RectangleGeometry(new Rect(0, Arriba ? 0 : cajon, ancho, barra), radio, radio);
            if (cajon < 1) { Isla.Clip = fila; return; }     // aún no ha crecido: solo la barra

            double w = Math.Min(_tarjetaAncho, ancho);
            double x = XdeTarjeta();

            // El contenido viaja con su tarjeta: el cajón empieza después del filo y
            // del padding de la isla, así que lo que se le pone de margen es el resto.
            //
            // ---------- Y mide lo que mide la tarjeta ----------
            //
            // Aquí estaba el fallo que el recorte TAPABA. El cajón ocupa la isla entera y
            // el contenido se estira a él: con la isla de 1163 px y un panel de 460, el
            // contenido medía 1143 y el recorte dejaba ver solo los 460 de la izquierda.
            // Resultado: las tarjetas cortadas por la mitad, no ajustadas.
            //
            // Y pasaba desapercibido porque los paneles anclados a un icono de la DERECHA
            // salían bien por casualidad: el margen izquierdo que se les pone aquí les
            // comía el ancho sobrante y acababan midiendo, sin pretenderlo, lo suyo. En
            // cuanto el ancla se mueve a la izquierda el margen es 0 y el contenido se
            // desparrama. La medida lo cantó: ancho pedido 460, ancho real 1143.
            //
            // Con el ancho puesto, el contenido mide SIEMPRE lo que mide su tarjeta,
            // esté el ancla donde esté, y el recorte vuelve a ser solo lo que dice ser:
            // un recorte.
            if (_tarjetaContenido != null)
            {
                var m = _tarjetaContenido.Margin;
                double izq = Math.Max(0, x - Isla.Padding.Left - Isla.BorderThickness.Left);
                if (Math.Abs(m.Left - izq) > 0.5)
                    _tarjetaContenido.Margin = new Thickness(izq, m.Top, m.Right, m.Bottom);

                _tarjetaContenido.HorizontalAlignment = HorizontalAlignment.Left;
                // `Width` va por DENTRO de la escala del panel y el margen por fuera: con el
                // panel al 85 %, para ocupar `w` en pantalla el contenido mide w / 0,85.
                double wDentro = w / EscalaDe(_tarjetaContenido);
                if (double.IsNaN(_tarjetaContenido.Width) || Math.Abs(_tarjetaContenido.Width - wDentro) > 0.5)
                    _tarjetaContenido.Width = wDentro;
            }

            // Lo que la tarjeta se mete en la barra. Nunca menos que su propio radio:
            // si no, sus esquinas de arriba se quedan fuera de la fila y la unión deja
            // dos muescas justo donde tiene que parecer que la tarjeta nace de la isla.
            double solape = Math.Min(barra, Math.Max(RadioTarjeta, radio));
            var tarjeta = new RectangleGeometry(
                new Rect(x, Arriba ? barra - solape : 0, w, cajon + solape),
                RadioTarjeta, RadioTarjeta);

            var union = new CombinedGeometry(GeometryCombineMode.Union, fila, tarjeta);
            union.Freeze();                                  // se rehace en cada fotograma
            Isla.Clip = union;
        }
        catch (Exception ex) { Log.Error("recortar el cajón", ex); }
    }

    // ---------- Abrir y cerrar ----------

    // Abre un panel dentro de la isla. Si ya había otro, lo sustituye con transición suave.
    // Devuelve false si el panel pedido ya estaba abierto (y entonces lo cierra: alternar).
    bool AlternarPanel(string nombre, Func<FrameworkElement> crear, double ancho, double alto,
                       bool teclado = false, FrameworkElement? ancla = null, bool scrollPropio = false,
                       double? topeDuro = null)
    {
        if (_panelAbierto == nombre) { CerrarPanel(); return false; }
        AbrirPanel(nombre, crear(), ancho, alto, teclado, ancla: ancla, scrollPropio: scrollPropio,
                   topeDuro: topeDuro);
        return true;
    }

    // Con "ancla" (el icono del que cuelga el panel) el cajón no se dibuja de punta a
    // punta, sino como una tarjeta de "ancho" pegada a ese icono. Ver RecortarCajon.
    //
    // "scrollPropio" dice que el panel ya se desplaza por dentro, y entonces NO se le
    // pone otro scroll encima: si no cabe, se le recorta el alto y su propio scroll hace
    // su trabajo. Lo miran los DOS caminos, el vertical y el horizontal. El horizontal lo
    // ignoraba, y eso es lo que dejó la superficie de Inicio con un scroll de más, su
    // barra a la vista y la fila `Star` sin acotar.

    // ================= Diagnóstico temporal del flujo de apertura =================
    //
    // Sirve para distinguir, sin adivinar, por qué lo que se ve no coincide con lo que
    // se ha escrito: binario antiguo, botón equivocado, ancho ignorado por `AbrirPanel`,
    // raíz visual que se estira, o `PanelShell` sin consumir.
    //
    // TEMPORAL: quitar cuando la superficie de Inicio esté aprobada.
    internal static string _ultimoBoton = "ninguno";
    internal static void ApuntarBoton(string boton)
    {
        _ultimoBoton = boton;
        Log.Info($"botón pulsado: {boton}");
    }

    // ¿La raíz que se mete en el cajón es una superficie opaca de verdad? Es la
    // pregunta que separa "PanelShell existe" de "PanelShell se está usando".
    //
    // Mira a través del scroll: cuando el contenido no cabe, `AbrirPanel` lo envuelve en
    // un `ScrollViewer`, y entonces la raíz del cajón es ese scroll —transparente— con la
    // superficie opaca DENTRO. La traza decía "shell opaco: NO (ScrollViewer)" y era un
    // falso negativo: el panel sí estaba tapado.
    internal static bool EsShellOpaco(object? raiz)
    {
        for (int vuelta = 0; vuelta < 4 && raiz != null; vuelta++)
        {
            if (raiz is Border b && b.Background is SolidColorBrush s && s.Color.A == 255) return true;
            raiz = raiz switch
            {
                ScrollViewer sv => sv.Content,
                Border bo => bo.Child,
                _ => null,
            };
        }
        return false;
    }

    void ApuntarFlujo(string nombre, FrameworkElement raiz, double anchoPedido,
                      FrameworkElement? ancla)
    {
        try
        {
            string queAncla = ancla == null
                ? "NINGUNA -> el cajón se despliega de punta a punta de la isla"
                : $"{ancla.GetType().Name} \"{ancla.ToolTip}\"";
            Log.Info($"panel solicitado: {nombre}");
            Log.Info($"ancla: {queAncla}");
            Log.Info($"ancho pedido: {anchoPedido:0}");
            Log.Info($"shell opaco: {(EsShellOpaco(raiz) ? "sí" : $"NO ({raiz.GetType().Name})")}");

            // El tamaño real solo existe cuando el layout ha pasado: medirlo aquí daría
            // 0 y no diría nada. Se apunta en cuanto WPF ha colocado la raíz.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                Log.Info($"ancho real: {raiz.ActualWidth:0}");
                Log.Info($"alto real: {raiz.ActualHeight:0}");
                Log.Info($"isla real: {Isla.ActualWidth:0}x{Isla.ActualHeight:0}");
            }));

            // ---------- Y el recorte, cuando el panel ya está QUIETO ----------
            //
            // Esto estuvo mal medido y conviene que quede escrito, porque la medida decía
            // justo lo contrario de la verdad. Puesto en el mismo `Loaded` de arriba, el
            // contenido ya está colocado pero la isla todavía no ha crecido: se la ve de
            // 46 px de alto con un panel de 490 dentro, y el diagnóstico gritaba «SE SALE
            // 427 px» en paneles que acababan perfectamente.
            //
            // La isla crece animada hasta 460 ms (ver `AnimarAlto`), así que se mira
            // después. Un diagnóstico que mide a destiempo no es medio diagnóstico: es uno
            // que manda a investigar donde no hay nada.
            var cuando = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700),
            };
            cuando.Tick += (s, _) =>
            {
                cuando.Stop();
                if (_panelAbierto == nombre) ApuntarSiAlgoSeSale(nombre, raiz);
            };
            cuando.Start();
        }
        catch (Exception ex) { Log.Error("apuntar el flujo del panel", ex); }
    }

    // ---------- ¿Se sale algo por abajo? ----------
    //
    // La pregunta que no se podía contestar. El registro decía el alto del panel y el de
    // la isla, y de ahí no se deduce si el ÚLTIMO BOTÓN está dentro: puede estar fuera por
    // veinte píxeles y los dos números parecer razonables.
    //
    // Esto mide el borde inferior del último elemento que de verdad se dibuja, en
    // coordenadas de la isla, y lo compara con el borde de la isla. Si sobresale, lo dice
    // con el número. Y dice también si hay un scroll que lo rescate: un panel cuyo
    // contenido se sale PERO se puede desplazar es correcto; uno que se sale sin scroll
    // tiene un botón que nadie puede pulsar.
    // Lo que sale de mirar un panel. `Bien` es la regla entera: o cabe, o hay rueda.
    internal readonly record struct Recorte(bool Bien, double Sobra, string Quien, bool HayRueda)
    {
        public override string ToString() =>
            Bien ? (Sobra > 1 ? $"se sale {Sobra:0} px pero hay rueda" : "todo dentro")
                 : $"SE SALE {Sobra:0} px sin rueda ({Quien})";
    }

    // ---------- Y lo que un lector de pantalla oye de un panel ----------
    //
    // La misma medida que se hizo con Ajustes, donde encontró cincuenta y un botones mudos
    // —los trece de la navegación, los cuarenta y cuatro de la biblioteca, los siete temas—
    // que ninguna captura podía enseñar: un botón con nombre y uno sin él se dibujan igual.
    //
    // Los de dentro de una plantilla de WPF no cuentan: las flechas de un `ScrollViewer`
    // son suyas, y un lector entiende la barra entera por su patrón, no botón a botón.
    internal static (int Botones, int SinNombre, string Ejemplo) MirarElTecladoDe(FrameworkElement raiz)
    {
        int botones = 0, sinNombre = 0;
        string ejemplo = "";
        try
        {
            foreach (var d in Descendientes(raiz))
            {
                if (d is not ButtonBase b || !b.IsVisible) continue;
                if (b.TemplatedParent != null) continue;
                botones++;
                var n = System.Windows.Automation.AutomationProperties.GetName(b);
                if (!string.IsNullOrWhiteSpace(n) || b.Content is string) continue;
                sinNombre++;
                // Todos, no uno de ejemplo: con un solo nombre se arregla ese y vuelven a
                // salir cinco iguales en la siguiente pasada. Con la lista se ven de golpe
                // y se sabe de qué trozo de interfaz son.
                if (sinNombre <= 6)
                    ejemplo += (ejemplo.Length > 0 ? " | " : "") +
                               (b.ToolTip?.ToString() ?? b.Content?.GetType().Name ?? "(sin pista)");
            }
        }
        catch { }
        return (botones, sinNombre, ejemplo);
    }

    Recorte ApuntarSiAlgoSeSale(string nombre, FrameworkElement raiz)
    {
        try
        {
            double bordeIsla = Isla.ActualHeight - Isla.BorderThickness.Bottom;
            double masAbajo = double.NegativeInfinity;
            string quien = "(nada)";

            foreach (var d in Descendientes(raiz))
            {
                if (d is not FrameworkElement fe || !fe.IsVisible) continue;
                if (fe.ActualHeight <= 0 || fe.ActualWidth <= 0) continue;
                // Solo lo que se PULSA o se lee: medir contenedores da el mismo número y
                // no dice qué control se queda fuera.
                if (fe is not (ButtonBase or TextBlock)) continue;
                try
                {
                    double y = fe.TransformToAncestor(Isla)
                                 .Transform(new Point(0, fe.ActualHeight)).Y;
                    if (y > masAbajo)
                    {
                        masAbajo = y;
                        quien = fe is ButtonBase b
                              ? $"botón «{System.Windows.Automation.AutomationProperties.GetName(b)}{b.ToolTip}»"
                              : $"texto «{((TextBlock)fe).Text}»";
                    }
                }
                catch { }
            }

            // Ni un botón ni un texto: no hay nada que pueda quedarse fuera.
            if (double.IsNegativeInfinity(masAbajo))
                return new Recorte(true, 0, "(sin controles que medir)", false);

            var scroll = Descendientes(raiz).OfType<ScrollViewer>()
                                            .FirstOrDefault(s => s.ScrollableHeight > 0);
            double sobra = masAbajo - bordeIsla;
            bool bien = sobra <= 1 || scroll != null;

            Log.Info($"panel {nombre}: lo más abajo es {quien} en y={masAbajo:0}; " +
                     $"la isla acaba en {bordeIsla:0} · " +
                     (sobra > 1
                        ? (scroll != null
                            ? $"se sale {sobra:0} px PERO hay desplazamiento ({scroll.ScrollableHeight:0} px)"
                            : $"SE SALE {sobra:0} px Y NO HAY DESPLAZAMIENTO: ese control no se puede pulsar")
                        : "todo dentro"));

            // ---------- Y el detalle, SOLO cuando algo va mal ----------
            //
            // Esto se escribía siempre, y esta función corre cada vez que abres un panel —
            // también en uso normal, no solo en un diagnóstico—. Son tres o cuatro líneas
            // de aritmética por cada apertura, en el registro que uno abre cuando algo
            // falla. Un registro que lo cuenta todo tapa lo que importa igual de bien que
            // uno que no cuenta nada.
            //
            // Callado cuando está bien; con todos los números cuando no, que es cuando
            // sirven: la cadena de alturas dice QUIÉN impone el recorte —la ventana, la
            // fila del cajón o la medida— y las tarjetas dicen en cuál está la diferencia.
            if (!bien)
            {
                Log.Info($"   alturas de {nombre}: ventana {ActualHeight:0} · isla {Isla.ActualHeight:0} " +
                         $"· barra {_cfg.Barra.Alto:0} · cajón {Cajon.ActualHeight:0} " +
                         $"· raíz [pide {raiz.DesiredSize.Height:0}, ocupa {raiz.ActualHeight:0}] " +
                         $"· _panelAlto {_panelAlto:0} · tope {AltoMaximoPanel():0}");
                try
                {
                    var pila = Descendientes(raiz).OfType<StackPanel>().FirstOrDefault();
                    if (pila != null)
                        Log.Info($"   tarjetas de {nombre}: " + string.Join("  ", pila.Children
                            .OfType<FrameworkElement>()
                            .Select(c => $"[pide {c.DesiredSize.Height:0}, ocupa {c.ActualHeight:0}]")));
                }
                catch { }
            }

            // La regla, en una sola frase: o cabe, o hay rueda que lo alcance.
            return new Recorte(bien, Math.Max(0, sobra), quien, scroll != null);
        }
        catch (Exception ex)
        {
            Log.Error($"medir si algo se sale del panel {nombre}", ex);
            // Si no se pudo mirar, no se inventa un veredicto: se da por bueno y se dice.
            return new Recorte(true, 0, "(no se pudo medir)", false);
        }
    }

    void AbrirPanel(string nombre, FrameworkElement contenido, double ancho, double alto,
                    bool teclado = false, bool encoger = false, FrameworkElement? ancla = null,
                    bool scrollPropio = false, double? topeDuro = null)
    {
        // Los tamaños que llegan son de diseño; a partir de aquí todo va en lo que de
        // verdad ocupa el panel en esta pantalla (ver `EscalaDePaneles`).
        double f = EscalaDePaneles();
        Escalar(contenido, f);
        ancho *= f;
        alto *= f;
        if (topeDuro is { } td) topeDuro = td * f;

        ApuntarFlujo(nombre, contenido, ancho, ancla);
        // En vertical el cajón no vale: la barra ya ocupa la pantalla de arriba abajo y
        // no tiene hacia dónde crecer. El panel sale flotando al lado (ver
        // MainWindow.Vertical.cs). Todo lo de aquí abajo —el recorte en tarjeta, el
        // encogerse, la animación de alto— es de la isla horizontal.
        if (Vertical)
        {
            if (_panelAbierto != null) QuitarPanelFlotante();
            AbrirPanelFlotante(nombre, contenido, ancho, alto, teclado, ancla, scrollPropio, topeDuro);
            return;
        }

        bool habiaOtro = _panelAbierto != null;
        if (habiaOtro) OlvidarPanelEnLaMesa(_panelAbierto);
        _panelAbierto = nombre;
        _panelAlto = alto;
        _panelAncho = ancho;
        ApuntarPanelEnLaMesa(nombre, ancho, alto, teclado);

        if (ancla != null) RecortarCajon(ancla, ancho); else SinRecorteDelCajon();

        // Si la isla estaba escondida por un juego o una ventana maximizada, aquí la
        // traemos de vuelta nosotros mismos. Esperar al vigilante de 150 ms y a que
        // el primer plano cambie añadía casi medio segundo al abrir el menú con la
        // tecla Windows, y el juego puede no soltar el primer plano nunca.
        if (_recogida && !_asomada)
        {
            _asomada = true;
            _ratonEnElBorde = DateTime.MaxValue;
            Deslizar(false);
        }

        // ¿Se despliega la isla o se queda recogida?
        //
        // Un panel estrecho dentro de una isla tan ancha como la fila entera de
        // módulos deja dos vacíos negros enormes a los lados y queda raro, así que
        // puede encogerse hasta la anchura DEL PANEL, como hace la isla del iPhone.
        //
        // Pero eso SOLO vale para los paneles que no cuelgan de un módulo. Si el
        // panel lo ha abierto un icono del carrusel (su mini lista, su menú), al
        // encoger la isla el icono desaparece de debajo del cursor, la isla cree
        // que te has ido y cierra el panel justo cuando ibas a usarlo. Por eso
        // "encoger" es opt-in y no automático.
        if (EsIsla)
        {
            bool estrecho = encoger && ancho + 40 < AnchoConModulos();
            _panelEncogido = estrecho;
            Expandir(!estrecho, forzar: true);
        }

        contenido.Opacity = 0;
        Cajon.Content = contenido;      // atado ya al árbol: así mide con la fuente de la isla

        // El alto que pide quien abre el panel es una estimación hecha a mano, y
        // basta con que se quede corta por un píxel para que la última fila salga
        // recortada: el cajón recorta a sus bordes. Así que se MIDE el contenido y
        // se usa lo que de verdad necesita, si es más. Hay que sumarle el filo de
        // la isla, que se come su grosor por arriba y por abajo.
        double bordes = Isla.BorderThickness.Top + Isla.BorderThickness.Bottom;
        try
        {
            // Por `AltoQueDeVerdadPide` y no por `Measure` a secas: al abrir, el cajón
            // todavía no se ha medido y la raíz contesta bien, pero eso es suerte de
            // cronometraje. Un panel que naciera ya lleno —la lista de wifis en caché, el
            // centro de notificaciones con cosas sin leer— se encontraría la misma fila
            // `Star` contestando por él. Una sola forma de medir en los dos sitios.
            double pide = AltoQueDeVerdadPide(contenido, ancho) + bordes;

            // Y por mucho que necesite, la isla no puede crecer más de lo que cabe en
            // la pantalla: lo que sobre se recorre por dentro con la rueda
            //
            // ---------- El alto pedido era un MÍNIMO, no un tope ----------
            //
            // Aquí estaba el segundo fallo. `alto` solo servía para abajo: si el
            // contenido pedía más, se le daba más (ver el `if (pide > alto)` de abajo).
            // El centro de control pedía 560 y salía de 1076 px, tres cuartos de una
            // pantalla de 1440, porque sus ocho tarjetas medían eso. "Altura máxima
            // controlada" no era controlada por nadie.
            //
            // Con `topeDuro` el que abre puede decir "de aquí no pasas"; lo que sobre se
            // desplaza por dentro, que es exactamente lo que ya se hacía con el tope de
            // la pantalla. Sin `topeDuro` el comportamiento es el de siempre, para no
            // tocarle el alto a los paneles que crecen a propósito (la lista de wifis, el
            // centro de notificaciones).
            double tope = Math.Min(AltoMaximoPanel(), topeDuro ?? double.PositiveInfinity);
            if (pide > tope)
            {
                // ---------- Y "scrollPropio" también aquí ----------
                //
                // Aquí estaba el fallo del panel negro con la franja blanca. Este camino
                // envolvía SIEMPRE en un `ScrollViewer`, aunque el panel ya tuviera el
                // suyo. Antes no se notaba porque la superficie de Inicio nunca pasaba del
                // tope; en cuanto le entró la portada entera, empezó a pasar, y el
                // resultado medido fue:
                //
                //   tipo de raíz visual: ScrollViewer
                //   cadena: ScrollViewer(660x893) > Border(643x1256) > Grid(629x1236)
                //   viewport 643x893  extent 643x1256  barra visible: SÍ
                //
                // Tres consecuencias, las tres visibles: (1) la barra de ese scroll de más
                // es la FRANJA BLANCA VERTICAL; (2) `PanelShell` deja de ser la raíz y sus
                // 17 px de ancho que la barra le come dejan a la vista el fondo del
                // scroll, que no es opaco; (3) dentro de un scroll el alto disponible es
                // INFINITO, así que la fila `Star` del Inicio deja de estar acotada, su
                // portada se estira a 1256 px y 363 se quedan fuera del viewport.
                //
                // El camino vertical ya lo hacía bien: si el panel se desplaza por dentro,
                // no se le pone otro scroll encima; se le recorta el alto y su propio
                // scroll hace su trabajo. Esto es lo mismo.
                if (scrollPropio)
                    Log.Info($"panel {nombre}: necesita {pide:0} de alto y el tope es {tope:0}; " +
                             "se recorta y se desplaza con su propio scroll");
                else
                {
                    Log.Info($"panel {nombre}: necesita {pide:0} de alto y el tope es {tope:0}; se desplaza por dentro");
                    Cajon.Content = null;               // para poder cambiarle el padre
                    Cajon.Content = EnvolverConScroll(contenido, tope - bordes);
                    contenido = (FrameworkElement)Cajon.Content;
                }
                pide = tope;
            }

            if (pide > alto)
            {
                Log.Info($"panel {nombre}: pedía {alto:0} de alto pero necesita {pide:0}; se le da el que necesita");
                alto = pide;
                _panelAlto = alto;
            }
        }
        catch (Exception ex) { Log.Error("medir el panel", ex); }

        // Ya sabemos qué elemento hay DE VERDAD en el cajón (puede haber acabado
        // envuelto en un scroll): es el que la tarjeta tiene que llevar consigo
        if (ancla != null) { _tarjetaContenido = contenido; AplicarRecorteDelCajon(); }

        contenido.Opacity = 0;
        contenido.RenderTransform = new TranslateTransform { Y = Arriba ? -12 : 12 };

        double altoIsla = _cfg.Barra.Alto + alto;
        double anchoIsla = Math.Max(ancho, MedirAncho());

        // La ventana primero: si no, el recorte de la isla se come el panel al crecer
        AjustarVentana(altoIsla);
        if (teclado) ActivarTeclado(true);

        // Un panel pequeño no necesita el mismo recorrido que el menú Inicio: con
        // la duración larga, un menú de cuatro líneas tardaba medio segundo en
        // poder leerse y daba sensación de lento. Se escala con lo que crece.
        bool pequeno = alto < 220;
        double msAncho = habiaOtro ? 240 : pequeno ? 280 : 420;
        double msAlto = habiaOtro ? 240 : pequeno ? 300 : 460;
        AnimarAncho(anchoIsla, msAncho, habiaOtro ? Suave : Muelle);
        AnimarAlto(altoIsla, msAlto, habiaOtro ? Suave : Muelle);

        // El contenido entra un pelín después, cuando ya hay hueco donde verlo
        double retardo = habiaOtro ? 30 : pequeno ? 55 : 110;
        contenido.BeginAnimation(OpacityProperty, Kf(0, 1, pequeno ? 150 : 220, retardo));
        contenido.Opacity = 1;
        if (contenido.RenderTransform is TranslateTransform t)
        {
            t.BeginAnimation(TranslateTransform.YProperty,
                             Kf(Arriba ? -12 : 12, 0, pequeno ? 260 : 380, retardo, Muelle));
            t.Y = 0;
        }
        MedirFotogramas($"abrir panel {nombre}");
        // Cuánto ha tardado el menú desde que soltaste la tecla Windows: hasta que
        // la ventana está colocada y el contenido montado, sin contar la animación
        var tecla = GanchoTeclas.MarcaTecla;
        GanchoTeclas.MarcaTecla = 0;
        string desdeTecla = tecla == 0 ? "" :
            $", {System.Diagnostics.Stopwatch.GetElapsedTime(tecla).TotalMilliseconds:0} ms desde la tecla";
        Log.Info($"panel abierto: {nombre} ({ancho:0}x{alto:0}, teclado={teclado}){desdeTecla}");
        // Y si se pasa de un panel sin teclado a uno que SÍ lo pide, hay que darse de
        // baja: cambiar de panel no pasa por `CerrarPanel`, y el lanzador se habría
        // encontrado con dos Esc —el suyo, que vacía la búsqueda, y el del hook.
        if (teclado) DejarDeEscucharEscape(); else EscucharEscape();
    }

    // ================= Esc cierra el panel =================
    //
    // Los paneles de centro de control, audio, red y batería NO piden teclado: la isla es
    // WS_EX_NOACTIVATE y nunca tiene el foco, así que un `KeyDown` suyo no se dispara
    // nunca. Por eso Esc no los cerraba: no faltaba un manejador, faltaba alguien a
    // quien avisar. El aviso llega del hook de teclado, que sí ve la tecla, y la tecla
    // no se traga.
    //
    // Los que sí piden teclado —el lanzador y la vista general— siguen con su propio Esc
    // intacto: el del lanzador primero vacía la búsqueda, y meterse por medio le habría
    // cambiado el comportamiento.
    Action? _escDelPanel;

    void EscucharEscape()
    {
        if (_gancho == null || _escDelPanel != null) return;
        _escDelPanel = () => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_panelAbierto == null) return;
            Log.Info($"Esc con el panel {_panelAbierto} abierto: se cierra");
            CerrarPanel();
        }));
        _gancho.Escape += _escDelPanel;
        _gancho.HayPanel = true;
    }

    void DejarDeEscucharEscape()
    {
        if (_escDelPanel == null) return;
        if (_gancho != null) { _gancho.Escape -= _escDelPanel; _gancho.HayPanel = false; }
        _escDelPanel = null;
    }

    void CerrarPanel()
    {
        if (_panelAbierto == null) return;
        DejarDeEscucharEscape();
        // La tarjeta se cierra sola, con su propia animación: se mira si está a la
        // vista y no si estamos en vertical, para que un cambio de orientación con un
        // panel abierto no deje la tarjeta colgada.
        if (PanelSuperpuestoAbierto) { CerrarPanelFlotante(); return; }

        var nombre = _panelAbierto;
        bool pequeno = _panelAlto < 220;
        OlvidarPanelEnLaMesa(nombre);
        _panelAbierto = null;
        _panelAlto = _panelAncho = 0;
        _panelEncogido = false;

        if (_panelConTeclado) ActivarTeclado(false);

        // Cerrar tiene que durar lo que duró abrir, o se siente un portazo. Y el
        // contenido no se puede ir en 120 ms mientras la isla tarda el triple en
        // plegarse: lo que veías era una isla VACÍA encogiéndose. Ahora se va con la
        // isla, metiéndose hacia ella, y termina un poco antes de que acabe el pliegue.
        double ms = pequeno ? 260 : 380;
        if (Cajon.Content is FrameworkElement viejo)
        {
            viejo.IsHitTestVisible = false;
            viejo.BeginAnimation(OpacityProperty, Kf(viejo.Opacity, 0, ms * 0.75, 0, Comoda));
            viejo.Opacity = 0;
            if (viejo.RenderTransform is TranslateTransform t)
            {
                double hasta = Arriba ? -10 : 10;       // hacia la isla, no hacia fuera
                t.BeginAnimation(TranslateTransform.YProperty, Kf(t.Y, hasta, ms * 0.85, 0, Comoda));
                t.Y = hasta;
            }
        }

        // La isla vuelve a su alto normal y solo entonces se encoge la ventana
        double altoIsla = _cfg.Barra.Alto;
        AnimarAlto(altoIsla, ms, Comoda, () =>
        {
            if (_panelAbierto != null) return;          // ya se abrió otro por el camino
            Cajon.Content = null;
            // El recorte se quita AQUÍ y no al empezar a cerrar: si no, la tarjeta
            // desaparecía de golpe y lo que se plegaba era otra vez la losa entera
            SinRecorteDelCajon();
            AjustarVentana(altoIsla);
            if (!Isla.IsMouseOver) Expandir(false);     // si el ratón ya no está, recogerse
        });
        AnimarAncho(MedirAncho(), ms, Comoda);
        MedirFotogramas($"cerrar panel {nombre}");
        Log.Info($"panel cerrado: {nombre}");
    }

    // ---------- Clic fuera ----------
    // Los paneles que se abren desde un icono (red, centro de control, bandeja) se
    // cierran con un clic fuera de la isla, como los menús de Windows. Antes solo lo
    // hacía el menú Inicio y estos se quedaban abiertos hasta volver a pulsar el icono.
    // Se mira igual que en el menú (VigilarClicFueraDelMenu): a pelo cada 150 ms, que
    // funciona aunque el clic caiga en otra aplicación y no nos llegue.
    static readonly HashSet<string> SeCierranConClicFuera = new()
        { PanelRed, PanelBandeja, PanelBateria, PanelNotas };
    bool _clicFueraContado;

    void VigilarClicFueraDelPanel()
    {
        if (_panelAbierto == null || !SeCierranConClicFuera.Contains(_panelAbierto)) { _clicFueraContado = false; return; }

        bool clic = Nativo.BotonIzquierdoPulsado() || Nativo.HuboClicIzquierdo();
        if (!clic) { _clicFueraContado = false; return; }
        if (_clicFueraContado) return;      // un clic largo cuenta una vez
        _clicFueraContado = true;
        try
        {
            var r = Monitores.Raton();
            if (Isla.InputHitTest(Isla.PointFromScreen(new Point(r.X, r.Y))) != null) return;
            // Con la barra vertical el panel vive en SU PROPIA VENTANA, fuera de la
            // isla: sin esto, el primer clic dentro del propio panel contaba como "clic
            // fuera" y lo cerraba justo cuando ibas a usarlo. Se pregunta por el
            // rectángulo de pantalla de esa ventana, no por un hit-test de la isla.
            if (_superpuesta is { } sup && sup.Contiene(r.X, r.Y)) return;
            // Un desplegable (la salida de audio), un tooltip o la ventana de ajustes
            // son ventanas NUESTRAS aparte de la isla: elegir algo en ellos no es "fuera"
            var bajo = Monitores.VentanaBajoElRaton();
            if (bajo != IntPtr.Zero && bajo != _self && Nativo.EsNuestra(bajo)) return;
            Log.Info($"clic fuera del panel {_panelAbierto}: se cierra");
            CerrarPanel();
        }
        catch { }
    }

    // ---------- Cuánto pide un panel DE VERDAD ----------
    //
    // Aquí estaba el recorte del panel de red, y es una trampa que merece quedar escrita
    // porque no se ve leyendo el código.
    //
    // El cajón vive en una fila `Star` (`FilaCajon.Height = Star`), así que el Grid le
    // concede la altura que sobra en la isla y mide a su contenido CON ESA altura. El
    // `DesiredSize` de la raíz del panel no puede entonces pasar de lo que ya se le ha
    // dado: si la isla mide 530 y la barra 38, la raíz dirá 492 tenga dentro lo que tenga.
    //
    // Y `ReajustarPanel` preguntaba justo a esa raíz. La conversación era circular:
    //
    //     — ¿Cuánto necesitas?   — 492.
    //     — ¿Y por qué 492?      — Porque es lo que me has dado.
    //
    // Medido: la raíz decía 492 mientras su contenido pedía 638. Las cuatro tarjetas del
    // panel de red suman 626, el último botón acababa en y=642 y la isla terminaba en 530:
    // 111 píxeles de «Ajustes de Windows», «Medir velocidad» y compañía por debajo del
    // borde, sin desplazamiento que los rescatara. No era una altura fija, ni el recorte en
    // tarjeta, ni el DPI, ni el área de trabajo: era la medida, que se contestaba a sí misma.
    //
    // La salida es no preguntarle al contenedor sino a lo que lleva dentro, que sí puede
    // medirse suelto. Se le descuenta el filo y el relleno del contenedor para medirlo con
    // el ancho que le toca, y se le vuelven a sumar después.
    //
    // Se devuelve el MÁXIMO de las dos medidas a propósito: así esto solo puede hacer que
    // un panel crezca cuando hacía falta, nunca encogerlo por debajo de lo que el código de
    // antes le daba. Un arreglo de recortes no puede introducir otro.
    internal static double AltoQueDeVerdadPide(FrameworkElement raiz, double ancho)
    {
        double deLaRaiz = 0;
        try
        {
            raiz.InvalidateMeasure();
            raiz.Measure(new Size(ancho, double.PositiveInfinity));
            deLaRaiz = raiz.DesiredSize.Height;
        }
        catch { }

        try
        {
            // El envoltorio puede ser el `PanelShell` (un Border con filo y relleno) o el
            // `ScrollViewer` que `EnvolverConScroll` pone cuando el panel ya no cabía. En
            // el segundo caso no hay nada que arreglar: su contenido SÍ se desplaza.
            if (raiz is ScrollViewer) return deLaRaiz;

            if (raiz is not Border marco || marco.Child is not FrameworkElement dentro)
                return deLaRaiz;

            double aLosLados = marco.Padding.Left + marco.Padding.Right
                             + marco.BorderThickness.Left + marco.BorderThickness.Right;
            double arribaYAbajo = marco.Padding.Top + marco.Padding.Bottom
                                + marco.BorderThickness.Top + marco.BorderThickness.Bottom;

            // El hijo se mide por dentro de la escala del panel: se le da el ancho de
            // dentro y lo que pide se pasa a lo que ocupa en pantalla.
            double f = EscalaDe(raiz);
            dentro.InvalidateMeasure();
            dentro.Measure(new Size(Math.Max(1, ancho / f - aLosLados), double.PositiveInfinity));
            return Math.Max(deLaRaiz, (dentro.DesiredSize.Height + arribaYAbajo) * f);
        }
        catch { return deLaRaiz; }
    }

    // Para los paneles que se rellenan por detrás (la lista de wifis, el test de
    // velocidad, la caja de la contraseña): AbrirPanel midió el contenido cuando aún
    // estaba a medias, así que se vuelve a medir y la isla crece o encoge a lo que
    // ahora necesita. Sin esto lo que llegaba tarde salía recortado por abajo.
    // ---------- Y se mide cuando el layout ya ha pasado ----------
    //
    // Medir en el mismo instante en que se añade el contenido es medir a medias, y eso
    // costó la mitad del recorte del panel de red: el único reajuste que llegaba a correr
    // lo hacía 90 ms después de abrir, con las wifis todavía entrando, y se quedaba con
    // aquel alto para siempre.
    //
    // `DispatcherPriority.Loaded` corre después de que WPF haya medido y colocado, así que
    // lo que se mide es lo que hay. El contador evita que una ráfaga de cambios —tres
    // repasos de red seguidos— encole tres reajustes: solo cuenta el último.
    int _reajustesPendientes;

    void ReajustarPanel(string nombre)
    {
        if (_panelAbierto != nombre) return;
        _reajustesPendientes++;
        Dispatcher.InvokeAsync(() =>
        {
            _reajustesPendientes--;
            if (_reajustesPendientes > 0) return;      // ya viene otro detrás
            ReajustarPanelYa(nombre);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    void ReajustarPanelYa(string nombre)
    {
        if (_panelAbierto != nombre) return;
        if (PanelSuperpuestoAbierto) { ReajustarPanelFlotante(nombre); return; }
        if (Cajon.Content is not FrameworkElement contenido) return;
        try
        {
            double bordes = Isla.BorderThickness.Top + Isla.BorderThickness.Bottom;

            // ---------- Hay que INVALIDAR antes de medir ----------
            //
            // `Measure` no vuelve a medir por pedírselo: si el elemento no está marcado
            // como sucio, devuelve el `DesiredSize` que ya tenía guardado. Y aquí no
            // siempre está sucio: el contenido que acaba de cambiar está varios niveles más
            // abajo —dentro del ScrollViewer de las wifis, dentro de su tarjeta— y ese
            // ScrollViewer tiene `MaxHeight`, así que absorbe el cambio y no propaga
            // ninguna invalidación hacia arriba.
            //
            // Resultado: se medía el alto de antes, salía igual que `_panelAlto`, y la
            // comparación de abajo decidía que no había nada que hacer. La isla se quedaba
            // con el alto de cuando el panel estaba vacío y lo que llegó después quedaba
            // por debajo del borde.
            double alto = AltoQueDeVerdadPide(contenido, _panelAncho) + bordes;

            // Lo que llega tarde (las wifis, el test de velocidad) puede pasarse del
            // alto de la pantalla: mismo trato que al abrir, se desplaza por dentro
            double tope = AltoMaximoPanel();
            var (cabe, conScroll) = ComoCabeElPanel(alto, tope);
            if (conScroll)
            {
                if (contenido is ScrollViewer { Tag: TopeDePanel } ya) ya.MaxHeight = tope - bordes;
                else
                {
                    Log.Info($"panel {nombre}: ha crecido hasta {alto:0} y caben {tope:0}; se desplaza por dentro");
                    Cajon.Content = null;
                    var envuelto = EnvolverConScroll(contenido, tope - bordes);
                    Cajon.Content = envuelto;
                    // Si era la tarjeta, ahora la tarjeta es el scroll: el contenido le
                    // devuelve su ancho y su margen, y es el scroll el que se coloca.
                    if (_tarjetaContenido == contenido)
                    {
                        var m = contenido.Margin;
                        contenido.Margin = new Thickness(0, m.Top, m.Right, m.Bottom);
                        contenido.Width = double.NaN;
                        contenido.HorizontalAlignment = HorizontalAlignment.Stretch;
                        _tarjetaContenido = envuelto;
                        AplicarRecorteDelCajon();
                    }
                }
            }
            alto = cabe;

            if (Math.Abs(alto - _panelAlto) < 1) return;

            Log.Info($"reajuste {nombre}: {_panelAlto:0} → {alto:0} " +
                     $"(pide {AltoQueDeVerdadPide(contenido, _panelAncho) + bordes:0}, tope {tope:0}, " +
                     $"desplazamiento {(conScroll ? "sí" : "no")})");

            bool crece = alto > _panelAlto;
            _panelAlto = alto;
            double altoIsla = _cfg.Barra.Alto + alto;
            // Igual que al abrir: si crece, la ventana primero; si encoge, al final
            if (crece) AjustarVentana(altoIsla);
            AnimarAlto(altoIsla, 220, Suave, crece ? null : () =>
            {
                if (_panelAbierto == nombre) AjustarVentana(_cfg.Barra.Alto + _panelAlto);
            });
        }
        catch (Exception ex) { Log.Error("reajustar el panel", ex); }
    }

    // ---------- El tope de la pantalla ----------

    // Lo máximo que puede medir un panel sin que la isla se salga del monitor. Sin
    // esto, un panel largo (quince wifis, un menú Inicio en un portátil de 768 px)
    // estiraba la isla hasta fuera de la pantalla y lo de abajo era inalcanzable.
    double AltoMaximoPanel() =>
        AltoMaximoDePanel(_monitor.Alto / Math.Max(0.1, _monitor.Escala),
                          _cfg.Barra.Alto, _cfg.Barra.Margen);

    // ---------- La misma cuenta, pura ----------
    //
    // Fuera de la ventana para poder comprobarla en pantallas que no tengo: un portátil de
    // 1366x768, un 4K al 150 %, un monitor vertical. La altura va siempre en píxeles de
    // DISEÑO —la pantalla física dividida por la escala—, que es en lo que mide WPF; pasarle
    // píxeles físicos daría el doble de sitio del que hay en un monitor al 200 %.
    //
    // El 24 son los dos márgenes de aire que la isla deja contra el borde de la pantalla, y
    // el mínimo de 200 es la red de seguridad: en una pantalla ridícula vale más un panel
    // que se desplaza por dentro que uno de altura negativa.
    internal static double AltoMaximoDePanel(double altoPantallaDip, double altoBarra, double margen) =>
        Math.Max(200, altoPantallaDip - altoBarra - margen * 2 - 24);

    // ---------- Y la decisión: ¿cabe o se desplaza? ----------
    //
    // Lo que hacen `AbrirPanel` y `ReajustarPanel` con esos dos números, en una función que
    // se puede comprobar sin abrir una ventana. Devuelve el alto que se le da al panel y si
    // hace falta desplazamiento por dentro.
    //
    // La regla que importa y que se comprueba: **el alto que se da nunca pasa del tope**, y
    // cuando lo que se pide no cabe, el desplazamiento se enciende. Las dos cosas juntas son
    // lo que garantiza que no queda un botón por debajo del borde: o cabe, o se llega a él
    // con la rueda.
    internal static (double Alto, bool ConScroll) ComoCabeElPanel(double pide, double tope)
    {
        if (pide <= tope) return (Math.Max(0, pide), false);
        return (tope, true);
    }

    const string TopeDePanel = "tope";

    // El contenido que no cabe se mete en un ScrollViewer. La rueda se atiende a mano
    // por lo mismo que en la lista de wifis: dentro hay botones y paneles de sobra como
    // para que alguien se quede el evento y la lista no se moviera.
    FrameworkElement EnvolverConScroll(FrameworkElement dentro, double alto)
    {
        dentro.RenderTransform = null;          // el que se anima ahora es el de fuera
        var scroll = new ScrollViewer
        {
            MaxHeight = alto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = dentro,
            Tag = TopeDePanel
        };
        scroll.PreviewMouseWheel += (_, e) =>
        {
            if (scroll.ScrollableHeight <= 0) return;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset - Math.Sign(e.Delta) * 60);
            e.Handled = true;
        };
        return scroll;
    }

    // Al recargar el config en caliente cambian alto, posición y modo, así que las
    // medidas del panel abierto dejan de valer: se cierra sin animación y a empezar
    void CerrarPanelAlRecargar()
    {
        if (_panelAbierto == null) return;
        Log.Info($"panel {_panelAbierto} cerrado por recarga del config");
        if (_panelConTeclado) ActivarTeclado(false);
        OlvidarPanelEnLaMesa(_panelAbierto);
        _panelAbierto = null;
        _panelEncogido = false;
        _panelAlto = _panelAncho = 0;
        Cajon.Content = null;
        SinRecorteDelCajon();
        QuitarPanelFlotante();
    }

    // ---------- Animación de alto ----------

    // Mismo criterio que AnimarAncho: el valor real es siempre el final y la animación
    // solo dibuja el camino, así nunca hay saltos al terminar
    void AnimarAlto(double hasta, double ms, IEasingFunction curva, Action? alTerminar = null)
    {
        double desde = Isla.ActualHeight > 0 ? Isla.ActualHeight : hasta;
        Isla.BeginAnimation(HeightProperty, null);
        Isla.Height = hasta;

        if (Math.Abs(desde - hasta) < 0.5) { alTerminar?.Invoke(); return; }

        var alto = Kf(desde, hasta, ms, 0, curva);
        if (alTerminar != null) alto.Completed += (_, _) => alTerminar();
        Isla.BeginAnimation(HeightProperty, alto);
    }

    // ---------- Medir fluidez ----------

    // Con NUBEBAR_DIAG=1 apunta alto y ancho de la isla en cada fotograma compuesto.
    // Sirve para comprobar que las animaciones nuevas van al refresco del monitor.
    void MedirFotogramas(string que, int ms = 900)
    {
        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") != "1") return;
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var filas = new System.Collections.Generic.List<string>();
        EventHandler? mano = null;
        mano = (_, _) =>
        {
            filas.Add($"{reloj.ElapsedMilliseconds}:{Isla.ActualHeight:0.0}x{Isla.ActualWidth:0.0}");
            if (reloj.ElapsedMilliseconds <= ms) return;
            CompositionTarget.Rendering -= mano;
            Log.Info($"DIAG {que} ({filas.Count} fotogramas en {reloj.ElapsedMilliseconds} ms) " +
                     string.Join(" ", filas));
        };
        CompositionTarget.Rendering += mano;
    }

    // ---------- Teclado ----------

    // La ventana vive como WS_EX_NOACTIVATE para no robar el foco ni tapar clics.
    // Para escribir en el buscador hay que quitárselo un rato y devolvérselo al cerrar.
    void ActivarTeclado(bool activar)
    {
        _panelConTeclado = activar;
        if (activar)
        {
            var antes = Nativo.Activa();         // a dónde volver al cerrar
            // Cualquier ventana NUESTRA (la de ajustes, la otra isla) no cuenta: al
            // cerrar el panel devolveríamos el foco a nosotros mismos.
            if (antes != IntPtr.Zero && !Nativo.EsNuestra(antes)) _ultimaActiva = antes;
            Nativo.Activable(_self);
            bool alFrente = Nativo.ForzarPrimerPlano(_self);
            Activate();
            Focus();
            Log.Info($"teclado activado (primer plano={alFrente})");
        }
        else
        {
            Nativo.NoActivable(_self);
            if (_ultimaActiva != IntPtr.Zero) Nativo.Activar(_ultimaActiva);
            Log.Info("teclado devuelto");
        }
    }
}
