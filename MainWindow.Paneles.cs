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

    // ---------- Abrir y cerrar ----------

    // Abre un panel dentro de la isla. Si ya había otro, lo sustituye con transición suave.
    // Devuelve false si el panel pedido ya estaba abierto (y entonces lo cierra: alternar).
    bool AlternarPanel(string nombre, Func<FrameworkElement> crear, double ancho, double alto, bool teclado = false)
    {
        if (_panelAbierto == nombre) { CerrarPanel(); return false; }
        AbrirPanel(nombre, crear(), ancho, alto, teclado);
        return true;
    }

    void AbrirPanel(string nombre, FrameworkElement contenido, double ancho, double alto,
                    bool teclado = false, bool encoger = false)
    {
        bool habiaOtro = _panelAbierto != null;
        _panelAbierto = nombre;
        _panelAlto = alto;
        _panelAncho = ancho;

        // Si la isla estaba escondida por un juego o una ventana maximizada, aquí la
        // traemos de vuelta nosotros mismos. Esperar al vigilante de 150 ms y a que
        // el primer plano cambie añadía casi medio segundo al abrir el menú con la
        // tecla Windows, y el juego puede no soltar el primer plano nunca.
        if (_escondidaPorPantalla && !_asomada)
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
        contenido.RenderTransform = new TranslateTransform { Y = Arriba ? -12 : 12 };
        Cajon.Content = contenido;

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
            if (pide > alto)
            {
                Log.Info($"panel {nombre}: pedía {alto:0} de alto pero necesita {pide:0}; se le da el que necesita");
                alto = pide;
                _panelAlto = alto;
            }
        }
        catch (Exception ex) { Log.Error("medir el panel", ex); }

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
            AjustarVentana(altoIsla);
            if (!Isla.IsMouseOver) Expandir(false);     // si el ratón ya no está, recogerse
        });
        AnimarAncho(MedirAncho(), ms, Comoda);
        MedirFotogramas($"cerrar panel {nombre}");
        Log.Info($"panel cerrado: {nombre}");
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
