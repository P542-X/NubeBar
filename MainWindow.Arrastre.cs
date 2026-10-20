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
        Isla.PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (!_cfg.Barra.Arrastrable || _siguiendoRaton != null) return;

            _raizRaton = PuntoRaton();
            var r = Nativo.Rectangulo(_self);
            _raizX = r.Izq;
            _raizY = r.Arr;
            _huboArrastre = false;

            _siguiendoRaton = (_, _) => SeguirRaton();
            CompositionTarget.Rendering += _siguiendoRaton;
        };
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
                Isla.Cursor = null;
                ProgramarVuelta();
                Log.Info($"isla soltada en {_ahoraX},{_ahoraY}; vuelve en {_cfg.Barra.SegundosParaVolver:0.#} s");
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
            Isla.Cursor = Cursors.SizeAll;
            Log.Info("arrastrando la isla");
        }

        MoverVentanaA(_raizX + (int)Math.Round(dx), _raizY + (int)Math.Round(dy));
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

    void AlTocarVolver(object? s, EventArgs e)
    {
        _vuelta?.Stop();
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

        int ancho = r.Der - r.Izq, alto = r.Aba - r.Arr;
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        const double ms = 520;

        // Muelle: se pasa un pelín y vuelve, como la isla del iPhone al encajar
        var curva = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };

        _animandoVuelta = (_, _) =>
        {
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
