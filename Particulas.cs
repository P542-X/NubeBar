using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NubeBar;

// Las partículas de un tema: pétalos, copos, hojas, gotas y píxeles.
//
// ---------- Dónde caen, y dónde NO ----------
//
// Solo **dentro** de una superficie de NubeBar —el menú Inicio, un panel—, nunca sobre el
// escritorio. Es una regla del plan y no es estética: un escritorio con pétalos encima es una
// capa a pantalla completa que recibe (o no recibe) el ratón, que tapa iconos, que sale en
// todas las capturas que haga el usuario y que gasta batería mientras no la miras.
//
// ---------- Por qué un bucle y no una animación por partícula ----------
//
// Treinta `Storyboard` sincronizados son treinta relojes que WPF mantiene, interpola y
// compone por fotograma, y pararlos todos al cerrar el panel es treinta oportunidades de
// dejarse uno vivo. Aquí hay **un** `CompositionTarget.Rendering` que mueve las treinta y se
// desengancha de golpe: si la capa no está en pantalla, no hay nada corriendo.
//
// ---------- Y el tope ----------
//
// `Tope` no se puede subir desde un tema ni desde el Estudio. El plan lo pide así («un tope de
// partículas que no se puede saltar») y es lo correcto: una decoración que puede dejar la
// barra a trompicones no es una decoración, es un interruptor de rendimiento disfrazado.
public sealed class Particulas : Canvas
{
    public const int Tope = 26;

    // ---------- Las formas ----------
    //
    // Trazados pequeños, de unos 10 px, que luego se escalan. Son los que la maqueta nombra
    // por su argumento en `temas-de-serie.json` (`particulas:petalo`, `particulas:copo`…).
    static readonly Dictionary<string, string> Formas = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pétalo de sakura: una gota con la punta hacia arriba y la base redonda.
        ["petalo"] = "M 5,0 C 8.6,2.8 9.4,7.6 5,11 C 0.6,7.6 1.4,2.8 5,0 Z",
        ["petalo-rojo"] = "M 5,0 C 8.6,2.8 9.4,7.6 5,11 C 0.6,7.6 1.4,2.8 5,0 Z",
        // Hoja de arce: aproximada con tres lóbulos y su rabillo.
        ["hoja-de-arce"] = "M 5,0 L 6.6,3.2 L 9.8,2.6 L 7.8,5.4 L 10,7.4 L 6.4,7.8 L 5.6,11 " +
                           "L 5,9.2 L 4.4,11 L 3.6,7.8 L 0,7.4 L 2.2,5.4 L 0.2,2.6 L 3.4,3.2 Z",
        // Copo: seis brazos cortos desde el centro.
        ["copo"] = "M 5,0 L 5.7,4.3 L 10,5 L 5.7,5.7 L 5,10 L 4.3,5.7 L 0,5 L 4.3,4.3 Z",
        // Gota de lluvia: una raya fina y alargada.
        ["lluvia"] = "M 0,0 L 1.3,0 L 1.3,11 L 0,11 Z",
        ["lluvia-neon"] = "M 0,0 L 1.3,0 L 1.3,13 L 0,13 Z",
        // Píxel: un cuadrado, que es lo que pide Videojuegos.
        ["pixel-sube"] = "M 0,0 L 4.4,0 L 4.4,4.4 L 0,4.4 Z",
    };

    // Las que suben en vez de caer. Solo los píxeles, de momento: es lo que convierte una
    // rejilla retro en algo que parece encendido por dentro.
    static bool Sube(string forma) => forma.Equals("pixel-sube", StringComparison.OrdinalIgnoreCase);

    sealed class Mota
    {
        public Path Visual = null!;
        public TranslateTransform Donde = null!;
        public RotateTransform Giro = null!;
        public double X, Y, Vel, Fase, Vaiven, VelGiro, Escala;
    }

    readonly List<Mota> _motas = new();
    readonly Random _azar = new();
    string _forma = "";
    Color _color;
    bool _corriendo;
    DateTime _ultimo = DateTime.UtcNow;

    Particulas()
    {
        IsHitTestVisible = false;     // una decoración jamás se come un clic
        ClipToBounds = true;
        Background = Brushes.Transparent;

        // ---------- Los enganches van AQUÍ, no en EndInit ----------
        //
        // Estuvieron en `EndInit()`, y no corrían nunca: WPF llama a `EndInit` cuando el
        // elemento se construye parseando XAML, y esta capa se crea desde código. Resultado:
        // las partículas se montaban, se sembraban… y nadie arrancaba el bucle. Las fotos del
        // Inicio en los nueve temas no enseñaban ni un pétalo, y por eso existen esas fotos.
        //
        // `IsVisibleChanged` es lo que para el bucle al cerrar el panel. Sin él habría que
        // acordarse de pararlo en los seis sitios que cierran uno, y el día que aparezca el
        // séptimo nadie se acordará.
        IsVisibleChanged += (_, _) => { if (IsVisible) Arrancar(); else Parar(); };
        Loaded += (_, _) => { if (IsVisible) Arrancar(); };
        Unloaded += (_, _) => Parar();
        SizeChanged += (_, _) => Encajar();
    }

    // ---------- La capa que le toca a un tema, o null ----------
    //
    // Null cuando el tema no lleva partículas, cuando su forma no se conoce, o cuando el nivel
    // de animación dice que no. Devolver null y no una capa vacía es a propósito: quien la
    // pide puede no añadirla al árbol, y entonces no hay nada que costar.
    public static Particulas? Para(Pintura p)
    {
        if (!Animacion.Adorno) return null;
        var forma = p.Tema.Argumento(Decoraciones.Particulas);
        if (string.IsNullOrWhiteSpace(forma) || !Formas.ContainsKey(forma)) return null;

        var color = p.Color("particula.color");
        if (color.A == 0) return null;

        return new Particulas { _forma = forma, _color = color };
    }

    // ---------- Arrancar y parar ----------
    //
    // Enganchado a `IsVisibleChanged`: el panel se cierra, la capa deja de estar en pantalla y
    // el bucle se desengancha solo. Sin esto habría que acordarse de pararlo en los seis
    // sitios que cierran un panel, y el día que aparezca el séptimo nadie se acordará.
    public void Arrancar()
    {
        if (_corriendo) return;
        _corriendo = true;
        _ultimo = DateTime.UtcNow;
        // Al reabrir el panel, las de la vez anterior están donde las dejó el cierre. Se
        // reparten de nuevo: un panel que se abre con todos los pétalos en la esquina de abajo
        // se ve como lo que es, una animación en pausa.
        _repartirOtraVez = true;
        Sembrar();
        Encajar();
        CompositionTarget.Rendering += Latido;
    }

    public void Parar()
    {
        if (!_corriendo) return;
        _corriendo = false;
        CompositionTarget.Rendering -= Latido;
    }

    // ---------- Y cuando la capa cambia de tamaño ----------
    //
    // Sembrar y no volver a mirar tenía un fallo que solo se ve en la foto: la capa se mide
    // ANTES de que el panel tenga su alto final, así que las partículas se repartían sobre
    // los 40 px que medía en ese momento y se quedaban todas en una franja, cayendo a la
    // velocidad de un panel de 40 px. Salió en la captura del Inicio de Sakura: una tira de
    // pétalos a media altura en vez de lluvia.
    //
    // Al crecer se reparten otra vez por todo el alto y se les recalcula la velocidad, que va
    // con el recorrido. Al encoger, basta con meterlas dentro.
    void Encajar()
    {
        if (ActualWidth < 20 || ActualHeight < 20) return;
        if (_motas.Count == 0) { Sembrar(); return; }

        // ---------- Por qué se mira el alto CON EL QUE SE SEMBRARON ----------
        //
        // Mirar solo si cada Y cabe en el alto nuevo no basta, y la foto lo enseñó dos veces:
        // sembradas sobre 40 px, todas las Y valen menos de 40, así que al crecer el panel a
        // 380 todas siguen «cabiendo» y se quedan en una tira a media altura. Lo que hay que
        // comparar es el alto de entonces con el de ahora.
        bool creció = ActualHeight > _altoSembrado * 1.25;
        bool sube = Sube(_forma);
        foreach (var m in _motas)
        {
            m.Vel = (sube ? -1 : 1) * ActualHeight / (9 + _azar.NextDouble() * 5);
            if (creció || _repartirOtraVez || m.Y > ActualHeight || m.Y < 0)
            {
                m.Y = _azar.NextDouble() * ActualHeight;
                m.X = _azar.NextDouble() * ActualWidth;
            }
        }
        _altoSembrado = ActualHeight;
        _repartirOtraVez = false;
    }

    bool _repartirOtraVez;
    double _altoSembrado;

    void Sembrar()
    {
        if (ActualWidth < 20 || ActualHeight < 20) return;
        if (_motas.Count > 0) return;

        var geo = Geometry.Parse(Formas[_forma]);
        geo.Freeze();
        bool sube = Sube(_forma);

        for (int i = 0; i < Tope; i++)
        {
            var giro = new RotateTransform(_azar.NextDouble() * 360, 5, 5);
            var donde = new TranslateTransform();
            var grupo = new TransformGroup();
            grupo.Children.Add(giro);
            grupo.Children.Add(donde);

            var p = new Path
            {
                Data = geo,
                Fill = new SolidColorBrush(_color) { Opacity = 0.22 + _azar.NextDouble() * 0.45 },
                RenderTransform = grupo,
                IsHitTestVisible = false,
            };
            Children.Add(p);

            var m = new Mota
            {
                Visual = p,
                Donde = donde,
                Giro = giro,
                X = _azar.NextDouble() * ActualWidth,
                // Repartidas por todo el alto desde el principio: sembrando todas arriba, el
                // panel se abre vacío y la primera tanda tarda en entrar.
                Y = _azar.NextDouble() * ActualHeight,
                // 9 a 14 s de recorrido, como pide la maqueta, convertidos a px/s.
                Vel = (sube ? -1 : 1) * ActualHeight / (9 + _azar.NextDouble() * 5),
                Fase = _azar.NextDouble() * Math.PI * 2,
                Vaiven = 6 + _azar.NextDouble() * 14,
                VelGiro = (_azar.NextDouble() - 0.5) * 36,
                Escala = 0.7 + _azar.NextDouble() * 0.8,
            };
            var escala = new ScaleTransform(m.Escala, m.Escala, 5, 5);
            grupo.Children.Insert(0, escala);
            _motas.Add(m);
        }
        _altoSembrado = ActualHeight;
    }

    void Latido(object? s, EventArgs e)
    {
        var ahora = DateTime.UtcNow;
        double dt = (ahora - _ultimo).TotalSeconds;
        _ultimo = ahora;
        // Un salto de más de un cuarto de segundo es que la máquina estuvo ocupada o suspendida.
        // Avanzar ese hueco de golpe teletransporta las partículas; se descarta el fotograma.
        if (dt <= 0 || dt > 0.25) return;

        double w = ActualWidth, h = ActualHeight;
        if (w < 20 || h < 20) return;

        foreach (var m in _motas)
        {
            m.Y += m.Vel * dt;
            m.Fase += dt * 0.9;
            m.Giro.Angle += m.VelGiro * dt;

            if (m.Vel > 0 && m.Y > h + 14) { m.Y = -14; m.X = _azar.NextDouble() * w; }
            else if (m.Vel < 0 && m.Y < -14) { m.Y = h + 14; m.X = _azar.NextDouble() * w; }

            m.Donde.X = m.X + Math.Sin(m.Fase) * m.Vaiven;
            m.Donde.Y = m.Y;
        }
    }
}
