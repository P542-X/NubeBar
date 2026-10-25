using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// Apartar la isla de en medio: la coges, la arrastras donde quieras y a los dos
// segundos de soltarla vuelve sola a su sitio.
//
// Se mueve la VENTANA, no la isla dentro de ella: la isla va centrada en una
// franja del ancho del monitor, así que moviendo la franja se mueve la isla y no
// hay nada que se recorte. Y todo en píxeles de pantalla, que es lo único que
// significa lo mismo en todos los monitores.
public partial class MainWindow : Window
{
    bool _arrastrando;                 // el ratón manda: nadie más toca la geometría
    bool _huboArrastre;                // se movió de verdad, no fue un clic
    bool _sinExpandir;                 // mientras la llevas cogida no se abre
    bool _fueraDeSitio;                // está donde la dejaste, pendiente de volver
    Point _raizRaton;                  // dónde estaba el ratón al empezar
    int _raizX, _raizY;                // dónde estaba la ventana al empezar
    int _ahoraX, _ahoraY;              // dónde está ahora
    DispatcherTimer? _vuelta;
    EventHandler? _animandoVuelta;

    // Un clic normal no puede convertirse en arrastre al primer píxel: hasta que
    // no te separas de aquí, el botón de debajo sigue siendo el que manda.
    const double UmbralArrastre = 6;

    EventHandler? _siguiendoRaton;

    void MontarArrastre()
    {
        // Solo hace falta engancharse al botón abajo. El seguimiento NO va por
        // eventos de ratón de WPF: en cuanto el puntero sale de la isla, los
        // WM_MOUSEMOVE se los lleva la ventana de debajo y dejaríamos de verlos
        // justo al empezar a arrastrar. Y capturar el ratón desde el principio
        // tampoco vale: los botones de la isla dejarían de recibir sus clics.
        //
        // Así que se sondea el ratón una vez por fotograma con GetCursorPos y
        // GetAsyncKeyState, que funcionan esté el puntero donde esté.
        Isla.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // Cada pulsación empieza limpia, pase lo que pase después: si esto se
            // quedara puesto de un arrastre anterior, el siguiente clic bueno
            // sobre un botón se lo comería ClicDeArrastre()
            _huboArrastre = false;
            _marcaArrastre = 0;

            if (!_cfg.Barra.Arrastrable || _siguiendoRaton != null) return;

            // La barra vertical no se aparta con el ratón. No es un olvido: es una
            // franja reservada pegada a su lado, como la barra de Windows, y toda la
            // maquinaria de apartarla y devolverla (CasaDeLaVentana, AnimarVuelta, el
            // filo que no se puede mover) está escrita en el eje de la horizontal.
            // Media función aquí se vería como un fallo; esto se ve como una decisión.
            if (Vertical) return;

            // Editando, lo que se coge es un widget, no la isla. Si no cediera aqui,
            // arrastrar un bloque moveria la barra entera por la pantalla.
            if (EdicionAnclada) return;

            // Si has cogido un icono del carrusel, lo que se mueve es el icono,
            // no la isla entera. Cada arrastre con lo suyo.
            if (EsDelCarrusel(e.OriginalSource)) return;

            // ---------- Y si el clic nace sobre un control, el gesto es suyo ----------
            //
            // Esto es lo que faltaba. La guarda de abajo solo conocía la vista general, así
            // que con el panel de audio abierto `_panelAbierto` valía "audio" y la isla se
            // iba detrás del ratón mientras arrastrabas el deslizador de volumen.
            //
            // No se puede resolver con `e.Handled` en el control: este manejador va en un
            // `PreviewMouseLeftButtonDown` de un ANTEPASADO y los eventos Preview van de la
            // raíz a la hoja. Cuando el deslizador se entera, la isla ya ha empezado.
            //
            // Se mira el ORIGEN y se sube por el árbol. Ver MainWindow.Gesto.cs.
            if (GestoDeUnControl(e.OriginalSource, Isla))
            {
                _huboArrastre = false;
                return;
            }

            // ---------- Con la vista general abierta, el gesto NO es de la isla ----------
            //
            // Lo que se arrastra es una ventana del escritorio, no la nube. Tres
            // comprobaciones y no una, porque cada una tapa un agujero distinto:
            //
            //  1. **El panel abierto es la vista.** Lo normal.
            //  2. **El clic sale del árbol visual de la vista.** Esto es lo que faltaba:
            //     no vale con que la tarjeta ponga `e.Handled`, porque este manejador va
            //     en un `PreviewMouseLeftButtonDown` de un ANTEPASADO, y los eventos
            //     Preview van de la raíz a la hoja — cuando la tarjeta se entera, la isla
            //     ya ha empezado su arrastre.
            //  3. **Ya hay un ajuste en marcha**, de una tarjeta o de una ventana real.
            //     Dos gestos a la vez con un solo botón no existen; si el estado dice que
            //     sí, el que no empieza es este.
            if (_panelAbierto == PanelVista || EnLaVista(e.OriginalSource) || AjustandoAlgo)
            {
                Log.Info("arrastre isla NO EMPIEZA: el gesto es de la vista general" +
                         (_panelAbierto == PanelVista ? " (panel=vista)" : "") +
                         (EnLaVista(e.OriginalSource) ? " (el clic sale de la vista)" : "") +
                         (AjustandoAlgo ? " (ya hay un ajuste en marcha)" : ""));
                return;
            }

            _raizRaton = PuntoRaton();
            var r = Nativo.Rectangulo(_self);
            _raizX = r.Izq;
            _raizY = r.Arr;
            _huboArrastre = false;

            _siguiendoRaton = (_, _) => SeguirRaton();
            CompositionTarget.Rendering += _siguiendoRaton;
        };

        // La vuelta a su sitio la manda el ratón, no un reloj: mientras sigas
        // con ella encima se queda donde la pusiste, y en cuanto la dejas
        // empieza la cuenta atrás.
        Isla.MouseEnter += (_, _) => { if (_fueraDeSitio) PararVuelta(); };
        Isla.MouseLeave += (_, _) => { if (_fueraDeSitio) ProgramarVuelta(); };
    }

    void SeguirRaton()
    {
        // Botón soltado: se acabó
        if (!Nativo.BotonIzquierdoPulsado())
        {
            DejarDeSeguir();
            if (_huboArrastre)
            {
                _arrastrando = false;
                _sinExpandir = false;
                _fueraDeSitio = true;
                Isla.Cursor = null;
                ApuntarArrastre();      // que el botón de debajo no se tome esto por un clic

                // Si ya no tienes el ratón encima, empieza la cuenta atrás. Si lo
                // tienes, se espera a que la sueltes de verdad: no va a volverse
                // a su sitio mientras sigues con ella.
                if (!Isla.IsMouseOver) ProgramarVuelta();
                Log.Info($"isla soltada en {_ahoraX},{_ahoraY}" +
                         (Isla.IsMouseOver ? "; se queda mientras sigas encima" : "; vuelve enseguida"));
            }
            return;
        }

        var p = PuntoRaton();
        double dx = p.X - _raizRaton.X, dy = p.Y - _raizRaton.Y;

        if (!_arrastrando)
        {
            // Hasta que no te separas de aquí sigue siendo un clic normal
            if (Math.Abs(dx) < UmbralArrastre && Math.Abs(dy) < UmbralArrastre) return;
            PararVuelta();
            _arrastrando = true;
            _huboArrastre = true;

            // Mientras la llevas NO se abre: la tienes cogida, no la estás usando.
            // Se recoge y se queda recogida hasta que la sueltes.
            _sinExpandir = true;
            Expandir(false);

            Isla.Cursor = Cursors.SizeAll;
            // Si esto sale con la vista abierta es un FALLO, no una curiosidad: significa
            // que las tres comprobaciones de arriba no han bastado.
            DiagVista.ArrastresIsla++;
            if (_panelAbierto == PanelVista) DiagVista.ArrastresIslaConVistaAbierta++;
            Log.Info((_panelAbierto == PanelVista ? "ERROR: " : "") + "arrastre isla INICIADO" +
                     (_panelAbierto == PanelVista ? " CON LA VISTA ABIERTA" : ""));
        }

        MoverVentanaA(_raizX + (int)Math.Round(dx), _raizY + (int)Math.Round(dy));
    }

    // ---------- Soltar no es pulsar ----------
    //
    // Al soltar la isla, WPF le manda su Click al botón que había debajo del dedo
    // cuando empezaste: cogerla por el icono de la vista general la abría, por el
    // de una app te cambiaba a esa app, y por una app fijada y cerrada te la
    // lanzaba. Encima, lo que se abría recolocaba la ventana y la isla se plantaba
    // en su sitio de un salto, sin la animación de vuelta.
    //
    // Vale tanto para el arrastre de la isla como para el de un icono del carrusel:
    // después de reordenar tampoco quieres acabar en esa app.
    long _marcaArrastre;

    void ApuntarArrastre() => _marcaArrastre = System.Diagnostics.Stopwatch.GetTimestamp();

    bool ClicDeArrastre()
    {
        // El arrastre de la isla y el del icono se enteran de que has soltado en el
        // siguiente fotograma, que puede llegar después del Click: por eso, además
        // de las banderas, hay una marca de tiempo con un margen corto.
        if (_huboArrastre || _llevaIcono != null) return true;
        if (_marcaArrastre == 0) return false;
        if (System.Diagnostics.Stopwatch.GetElapsedTime(_marcaArrastre).TotalMilliseconds < 250) return true;
        _marcaArrastre = 0;
        return false;
    }

    void DejarDeSeguir()
    {
        if (_siguiendoRaton == null) return;
        CompositionTarget.Rendering -= _siguiendoRaton;
        _siguiendoRaton = null;
    }

    static Point PuntoRaton()
    {
        var p = Monitores.Raton();
        return new Point(p.X, p.Y);
    }

    void MoverVentanaA(int x, int y)
    {
        var r = Nativo.Rectangulo(_self);
        _ahoraX = x;
        _ahoraY = y;
        Nativo.Colocar(_self, x, y, r.Der - r.Izq, r.Aba - r.Arr);
    }

    // ---------- La vuelta ----------
    void ProgramarVuelta()
    {
        _vuelta ??= new DispatcherTimer();
        _vuelta.Interval = TimeSpan.FromSeconds(Math.Max(0.2, _cfg.Barra.SegundosParaVolver));
        _vuelta.Stop();
        _vuelta.Tick -= AlTocarVolver;
        _vuelta.Tick += AlTocarVolver;
        _vuelta.Start();
    }

    // Red de seguridad, una vez por segundo: si la isla está fuera de su sitio,
    // no tienes el ratón encima y no hay ninguna cuenta atrás en marcha, se
    // programa. Hace falta porque MouseLeave no llega si el cursor se teletransporta
    // (lo hace algún programa, o al saltar de monitor muy rápido), y sin esto la
    // isla se quedaría descolocada para siempre.
    void VigilarVuelta()
    {
        if (!_fueraDeSitio || _arrastrando) return;
        if (Isla.IsMouseOver || _panelAbierto != null) return;
        if (_vuelta is { IsEnabled: true }) return;
        ProgramarVuelta();
    }

    void AlTocarVolver(object? s, EventArgs e)
    {
        _vuelta?.Stop();

        // Si has vuelto a ponerte encima o has abierto algo, no se mueve: se
        // espera otra vez. Volver a su sitio con el menú abierto sería lo peor.
        if (Isla.IsMouseOver || _panelAbierto != null) { ProgramarVuelta(); return; }

        _fueraDeSitio = false;
        AnimarVuelta();
    }

    void PararVuelta()
    {
        _vuelta?.Stop();
        if (_animandoVuelta != null)
        {
            CompositionTarget.Rendering -= _animandoVuelta;
            _animandoVuelta = null;
        }
    }

    // La posición de casa: la que le toca por su monitor y su configuración
    (int x, int y) SuSitio()
    {
        double altoDip = AltoVentanaAhora();
        int altoPx = Math.Max(1, (int)Math.Round(altoDip * EscalaDeWpf()));
        return (_monitor.X, Arriba ? _monitor.Y : _monitor.Abajo - altoPx);
    }

    // No se puede animar un SetWindowPos con una Storyboard de WPF, así que se
    // interpola a mano una vez por fotograma compuesto. Así va al refresco del
    // monitor, igual que el resto, en vez de a saltos de temporizador.
    void AnimarVuelta()
    {
        PararVuelta();

        var (destinoX, destinoY) = SuSitio();
        var r = Nativo.Rectangulo(_self);
        int desdeX = r.Izq, desdeY = r.Arr;
        if (desdeX == destinoX && desdeY == destinoY) return;

        var reloj = System.Diagnostics.Stopwatch.StartNew();
        const double ms = 520;

        // Muelle: se pasa un pelín y vuelve, como la isla del iPhone al encajar
        var curva = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };

        _animandoVuelta = (_, _) =>
        {
            // El tamaño se vuelve a leer en cada fotograma y no se guarda del
            // principio: por el camino puede abrirse o cerrarse un panel, y con la
            // medida vieja la volveríamos a estirar o a encoger a cada paso
            var ahora = Nativo.Rectangulo(_self);
            int ancho = ahora.Der - ahora.Izq, alto = ahora.Aba - ahora.Arr;

            double t = Math.Min(1, reloj.Elapsed.TotalMilliseconds / ms);
            double k = curva.Ease(t);
            int x = (int)Math.Round(desdeX + (destinoX - desdeX) * k);
            int y = (int)Math.Round(desdeY + (destinoY - desdeY) * k);
            Nativo.Colocar(_self, x, y, ancho, alto);

            if (t < 1) return;
            PararVuelta();
            // El valor de verdad es el final: se deja clavado, pase lo que pase
            Nativo.Colocar(_self, destinoX, destinoY, ancho, alto);
        };
        CompositionTarget.Rendering += _animandoVuelta;
    }
}
