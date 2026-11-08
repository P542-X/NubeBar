using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace NubeBar;

// La isla reacciona a lo que suena.
//
// Cada 40 ms se mira el pico de la salida (0 a 1, del medidor de Windows, no de la
// música concreta: vale para Spotify, para un vídeo o para el sonido de un juego) y
// con él se mueven tres cosas a la vez:
//
//   · la isla se comprime un pelín, como si el sonido le hiciera presión;
//   · el halo del acento crece, que es lo que de verdad se ve de lejos;
//   · y dos filos de color, uno frío y uno cálido, se separan hacia los lados:
//     aberración cromática. Suave, de adorno, no un karaoke.
//
// Nada de esto toca el layout: son RenderTransform y el radio del halo, así que no
// provoca ni una medida de más. Y el temporizador rápido solo corre mientras hay
// sonido: en silencio se para solo y lo vuelve a arrancar el tick de un segundo.
public partial class MainWindow
{
    const double PulsoMs = 40;              // 25 veces por segundo, de sobra para un adorno
    const int TicksParaDormir = 50;         // 2 s sin sonido y se para

    DispatcherTimer? _pulso;
    ScaleTransform? _pulsoEscala;
    Border? _filoFrio, _filoCalido;
    TranslateTransform? _mueveFrio, _mueveCalido;
    double _nivel;                          // nivel ya suavizado, 0 a 1
    int _ticksEnSilencio;

    bool PulsoPedido => _cfg.Barra.PulsoAudio;

    // ---------- Montaje ----------

    void MontarPulsoAudio()
    {
        if (!PulsoPedido || _pulso != null) return;

        // La escala va en Cuerpo y no en Isla: la isla ya tiene su alto y su ancho
        // animados, y su RenderTransform lo usan el arrastre y el deslizar para
        // esconderse. Cuerpo está libre, y escalarlo no mueve ni un píxel de layout.
        if (Cuerpo.RenderTransform is not ScaleTransform)
        {
            _pulsoEscala = new ScaleTransform(1, 1);
            Cuerpo.RenderTransformOrigin = new Point(0.5, 0.5);
            Cuerpo.RenderTransform = _pulsoEscala;
        }

        // Los filos van DENTRO de Cuerpo, con el margen negativo que cancela el
        // padding de la isla, para que caigan justo en su borde. La isla recorta a
        // sus bordes (ClipToBounds), así que al separarse se ve el filo de color
        // asomando por un lado y el otro por el otro, que es el efecto que buscas.
        if (_filoFrio == null)
        {
            _filoFrio = CrearFilo(_roles.ColorDe("pulso.frio"));
            _filoCalido = CrearFilo(_roles.ColorDe("pulso.calido"));
            _mueveFrio = (TranslateTransform)_filoFrio.RenderTransform;
            _mueveCalido = (TranslateTransform)_filoCalido!.RenderTransform;
        }

        _pulso = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(PulsoMs)
        };
        _pulso.Tick += (_, _) => PasoDelPulso();
    }

    // ---------- Por qué el radio va ATADO y no copiado ----------
    //
    // Esto ponía `CornerRadius = Isla.CornerRadius`, o sea una foto del radio en el
    // momento de crear el filo. Y el pulso se monta tarde y una sola vez: la primera vez
    // que suena algo. Cambiar de tema después le cambia la forma a la isla y no al pulso.
    //
    // Lo contó el usuario con Cyberpunk, que va a radio 0: «el tema tiene los bordes
    // rectangulares, que está bien; el problema es que cuando se escucha música el bombeo
    // es redondeado y se sale de la zona rectangular». Justo eso: una isla cuadrada con un
    // filo de radio 23 heredado del tema anterior, asomando por las cuatro esquinas.
    //
    // Atado con un Binding no puede quedarse viejo. Y no es un apaño: dice lo que de
    // verdad pasa, que la forma del pulso NO es una decisión suya, es la de la isla.
    // Copiarla era afirmar dos veces lo mismo y dejar que se contradijeran.
    Border CrearFilo(Color color)
    {
        var filo = new Border
        {
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(-10, 0, -10, 0),   // justo el padding de la isla
            Background = null,
            IsHitTestVisible = false,
            Opacity = 0,
            RenderTransform = new TranslateTransform()
        };
        filo.SetBinding(Border.CornerRadiusProperty,
                        new System.Windows.Data.Binding(nameof(Border.CornerRadius)) { Source = Isla });
        Grid.SetRow(filo, 0);
        Grid.SetRowSpan(filo, 2);
        Cuerpo.Children.Add(filo);
        return filo;
    }

    // ---------- Y los colores, que se copiaban igual ----------
    //
    // Mismo fallo y menos visible: los dos filos nacen con el color que el tema tuviera en
    // ese momento, así que al cambiar de tema el pulso seguía latiendo con los colores del
    // anterior. El radio se arregla con un Binding porque es una propiedad de dependencia;
    // estos salen de los roles resueltos y no hay nada a lo que atarlos, así que se
    // refrescan desde donde se aplica el tema (ver AplicarTema).
    internal void RefrescarColoresDelPulso()
    {
        if (_filoFrio == null && _filoCalido == null) return;   // aún no ha sonado nada
        try
        {
            if (_filoFrio != null) _filoFrio.BorderBrush = new SolidColorBrush(_roles.ColorDe("pulso.frio"));
            if (_filoCalido != null) _filoCalido.BorderBrush = new SolidColorBrush(_roles.ColorDe("pulso.calido"));
        }
        catch (Exception ex) { Log.Error("refrescar los colores del pulso de audio", ex); }
    }

    // ---------- Arranque y parada ----------

    // Lo llama el tick de un segundo: si hay sonido y el pulso está dormido, lo
    // despierta. Preguntar el pico una vez por segundo no se nota en ningún sitio.
    void VigilarPulsoAudio()
    {
        // El config se recarga en caliente, así que apagarlo o encenderlo tiene que
        // notarse sin reiniciar
        if (!PulsoPedido)
        {
            if (_pulso is { IsEnabled: true }) PararPulsoAudio();
            return;
        }
        if (_pulso == null) MontarPulsoAudio();
        if (_pulso == null || _pulso.IsEnabled) return;
        if (Audio.PicoSalida() < 0.01) return;
        _ticksEnSilencio = 0;
        _pulso.Start();
    }

    void PasoDelPulso()
    {
        double pico = Audio.PicoSalida();

        // Lineal y con poca ganancia a propósito. Medido en este equipo con un aviso
        // corto del sistema, el pico de la música real va de 0,06 a 0,81: comprimirlo
        // con una curva dejaba el efecto casi a tope todo el rato, que es justo lo que
        // lo vuelve cansino. Así se mueve con la canción y solo llega al máximo en los
        // golpes. Sube rápido y baja despacio, como un vúmetro.
        double objetivo = Math.Min(1, pico * 1.25);
        _nivel += (objetivo - _nivel) * (objetivo > _nivel ? 0.55 : 0.12);

        Aplicar(_nivel);

        if (pico < 0.005)
        {
            if (++_ticksEnSilencio >= TicksParaDormir)
            {
                _nivel = 0;
                Aplicar(0);
                _pulso?.Stop();
            }
        }
        else _ticksEnSilencio = 0;
    }

    // Lo que se mueve. Todo son valores finales directos (no animaciones): ya es una
    // animación, 25 fotogramas por segundo, y encima dibujada por el suavizado.
    void Aplicar(double n)
    {
        // Presión: se ensancha un pelín y se achata. Son décimas de píxel en la isla
        // recogida y sin embargo es lo que la hace parecer viva.
        if (_pulsoEscala != null)
        {
            _pulsoEscala.ScaleX = 1 + 0.016 * n;
            _pulsoEscala.ScaleY = 1 - 0.022 * n;
        }

        // El halo. Solo el radio: la opacidad la animan el expandirse y el destello
        // de los avisos, y pelearse con ellas dejaría parpadeos.
        if (_brillo != null)
            _brillo.BlurRadius = BrilloRadio + 14 * n;

        // Y la aberración: en silencio los filos están pegados y a cero de opacidad,
        // o sea invisibles; con la música se separan y asoman.
        double aparte = 0.4 + 2.2 * n;
        if (_mueveFrio != null) _mueveFrio.X = -aparte;
        if (_mueveCalido != null) _mueveCalido.X = aparte;
        if (_filoFrio != null) _filoFrio.Opacity = 0.45 * n;
        if (_filoCalido != null) _filoCalido.Opacity = 0.45 * n;
    }

    // Al cerrar la isla, o al recargar el config con el pulso apagado, hay que dejar
    // todo como estaba: el valor de verdad de cada propiedad es el de reposo.
    void PararPulsoAudio()
    {
        _pulso?.Stop();
        _nivel = 0;
        Aplicar(0);
        if (_brillo != null) _brillo.BlurRadius = BrilloRadio;
    }
}
