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

    void AbrirPanel(string nombre, FrameworkElement contenido, double ancho, double alto, bool teclado = false)
    {
        bool habiaOtro = _panelAbierto != null;
        _panelAbierto = nombre;
        _panelAlto = alto;
        _panelAncho = ancho;

        // Los módulos laterales se quedan desplegados mientras haya panel
        if (EsIsla && !_expandida) Expandir(true);

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

        AnimarAncho(anchoIsla, habiaOtro ? 260 : 420, habiaOtro ? Suave : Muelle);
        AnimarAlto(altoIsla, habiaOtro ? 260 : 460, habiaOtro ? Suave : Muelle);

        // El contenido entra un pelín después, cuando ya hay hueco donde verlo
        double retardo = habiaOtro ? 40 : 110;
        contenido.BeginAnimation(OpacityProperty, Kf(0, 1, 220, retardo));
        contenido.Opacity = 1;
        if (contenido.RenderTransform is TranslateTransform t)
        {
            t.BeginAnimation(TranslateTransform.YProperty, Kf(Arriba ? -12 : 12, 0, 380, retardo, Muelle));
            t.Y = 0;
        }
        MedirFotogramas($"abrir panel {nombre}");
        Log.Info($"panel abierto: {nombre} ({ancho:0}x{alto:0}, teclado={teclado})");
    }

    void CerrarPanel()
    {
        if (_panelAbierto == null) return;
        var nombre = _panelAbierto;
        _panelAbierto = null;
        _panelAlto = _panelAncho = 0;

        if (_panelConTeclado) ActivarTeclado(false);

        if (Cajon.Content is FrameworkElement viejo)
        {
            viejo.IsHitTestVisible = false;
            viejo.BeginAnimation(OpacityProperty, Kf(viejo.Opacity, 0, 120));
            viejo.Opacity = 0;
        }

        // La isla vuelve a su alto normal y solo entonces se encoge la ventana
        double altoIsla = _cfg.Barra.Alto;
        AnimarAlto(altoIsla, 300, Suave, () =>
        {
            if (_panelAbierto != null) return;          // ya se abrió otro por el camino
            Cajon.Content = null;
            AjustarVentana(altoIsla);
            if (!Isla.IsMouseOver) Expandir(false);     // si el ratón ya no está, recogerse
        });
        AnimarAncho(MedirAncho(), 300, Suave);
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
            if (antes != IntPtr.Zero && antes != _self) _ultimaActiva = antes;
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
