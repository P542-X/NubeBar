using System;
using System.Windows;
using System.Windows.Controls;
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

    void SinRecorteDelCajon()
    {
        if (_tarjetaAncla == null) return;
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
    double XdeTarjeta()
    {
        double w = Math.Min(_tarjetaAncho, Isla.ActualWidth);
        double centro = Isla.ActualWidth / 2;
        try
        {
            if (_tarjetaAncla is { IsVisible: true } ancla && ancla.ActualWidth > 0)
                centro = ancla.TransformToAncestor(Isla).Transform(new Point(0, 0)).X + ancla.ActualWidth / 2;
        }
        catch { }
        double borde = Math.Max(2, Isla.BorderThickness.Left);
        return Math.Clamp(centro - w / 2, borde, Math.Max(borde, Isla.ActualWidth - w - borde));
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
            // del padding de la isla, así que lo que se le pone de margen es el resto
            if (_tarjetaContenido != null)
            {
                var m = _tarjetaContenido.Margin;
                double izq = Math.Max(0, x - Isla.Padding.Left - Isla.BorderThickness.Left);
                if (Math.Abs(m.Left - izq) > 0.5)
                    _tarjetaContenido.Margin = new Thickness(izq, m.Top, m.Right, m.Bottom);
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
                       bool teclado = false, FrameworkElement? ancla = null, bool scrollPropio = false)
    {
        if (_panelAbierto == nombre) { CerrarPanel(); return false; }
        AbrirPanel(nombre, crear(), ancho, alto, teclado, ancla: ancla, scrollPropio: scrollPropio);
        return true;
    }

    // Con "ancla" (el icono del que cuelga el panel) el cajón no se dibuja de punta a
    // punta, sino como una tarjeta de "ancho" pegada a ese icono. Ver RecortarCajon.
    //
    // "scrollPropio" solo lo mira el camino VERTICAL: dice que el panel ya se desplaza
    // por dentro y que, si no cabe, hay que recortarle el alto en vez de envolverlo en
    // otro scroll. El camino horizontal lo ignora a propósito, para que el menú Inicio
    // de siempre se siga comportando exactamente igual que antes de C1.
    void AbrirPanel(string nombre, FrameworkElement contenido, double ancho, double alto,
                    bool teclado = false, bool encoger = false, FrameworkElement? ancla = null,
                    bool scrollPropio = false)
    {
        // En vertical el cajón no vale: la barra ya ocupa la pantalla de arriba abajo y
        // no tiene hacia dónde crecer. El panel sale flotando al lado (ver
        // MainWindow.Vertical.cs). Todo lo de aquí abajo —el recorte en tarjeta, el
        // encogerse, la animación de alto— es de la isla horizontal.
        if (Vertical)
        {
            if (_panelAbierto != null) QuitarPanelFlotante();
            AbrirPanelFlotante(nombre, contenido, ancho, alto, teclado, ancla, scrollPropio);
            return;
        }

        bool habiaOtro = _panelAbierto != null;
        _panelAbierto = nombre;
        _panelAlto = alto;
        _panelAncho = ancho;

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
            contenido.Measure(new Size(ancho, double.PositiveInfinity));
            double pide = contenido.DesiredSize.Height + bordes;

            // Y por mucho que necesite, la isla no puede crecer más de lo que cabe en
            // la pantalla: lo que sobre se recorre por dentro con la rueda
            double tope = AltoMaximoPanel();
            if (pide > tope)
            {
                Log.Info($"panel {nombre}: necesita {pide:0} de alto y en la pantalla caben {tope:0}; se desplaza por dentro");
                Cajon.Content = null;               // para poder cambiarle el padre
                Cajon.Content = EnvolverConScroll(contenido, tope - bordes);
                contenido = (FrameworkElement)Cajon.Content;
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
    }

    void CerrarPanel()
    {
        if (_panelAbierto == null) return;
        // La tarjeta se cierra sola, con su propia animación: se mira si está a la
        // vista y no si estamos en vertical, para que un cambio de orientación con un
        // panel abierto no deje la tarjeta colgada.
        if (PanelSuperpuestoAbierto) { CerrarPanelFlotante(); return; }

        var nombre = _panelAbierto;
        bool pequeno = _panelAlto < 220;
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
    static readonly HashSet<string> SeCierranConClicFuera = new() { PanelRed, PanelControl, PanelBandeja, PanelBateria };
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

    // Para los paneles que se rellenan por detrás (la lista de wifis, el test de
    // velocidad, la caja de la contraseña): AbrirPanel midió el contenido cuando aún
    // estaba a medias, así que se vuelve a medir y la isla crece o encoge a lo que
    // ahora necesita. Sin esto lo que llegaba tarde salía recortado por abajo.
    void ReajustarPanel(string nombre)
    {
        if (_panelAbierto != nombre) return;
        if (PanelSuperpuestoAbierto) { ReajustarPanelFlotante(nombre); return; }
        if (Cajon.Content is not FrameworkElement contenido) return;
        try
        {
            double bordes = Isla.BorderThickness.Top + Isla.BorderThickness.Bottom;
            contenido.Measure(new Size(_panelAncho, double.PositiveInfinity));
            double alto = contenido.DesiredSize.Height + bordes;

            // Lo que llega tarde (las wifis, el test de velocidad) puede pasarse del
            // alto de la pantalla: mismo trato que al abrir, se desplaza por dentro
            double tope = AltoMaximoPanel();
            if (alto > tope)
            {
                if (contenido is ScrollViewer { Tag: TopeDePanel } ya) ya.MaxHeight = tope - bordes;
                else
                {
                    Log.Info($"panel {nombre}: ha crecido hasta {alto:0} y caben {tope:0}; se desplaza por dentro");
                    Cajon.Content = null;
                    Cajon.Content = EnvolverConScroll(contenido, tope - bordes);
                }
                alto = tope;
            }

            if (Math.Abs(alto - _panelAlto) < 1) return;

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
    double AltoMaximoPanel()
    {
        double pantalla = _monitor.Alto / _monitor.Escala;
        return Math.Max(200, pantalla - _cfg.Barra.Alto - _cfg.Barra.Margen * 2 - 24);
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
