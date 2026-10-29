using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace NubeBar;

// Animaciones de adorno. Todas siguen la misma regla que el resto de la isla:
// el valor de verdad de cada propiedad es siempre el estado FINAL, y la
// animación solo dibuja el camino hasta él. Si una fallase o se interrumpiera,
// todo se quedaría en su sitio, sin saltos ni restos.
public partial class MainWindow : Window
{
    // ---------- Levantar el icono al pasar por encima ----------
    // Un empujoncito de 2 px y un 8 % de tamaño. Es lo que más se nota al usar la
    // barra y lo que hace que los iconos se sientan "vivos" bajo el ratón.
    const double LevantaPx = 2, LevantaEscala = 1.08;

    static void Levantar(FrameworkElement e, bool arriba)
    {
        // Con el movimiento reducido el icono no se levanta en absoluto: no hay estado
        // que mantener (el fondo del hover ya avisa de que estás encima), así que esto
        // se salta del todo en vez de hacerlo instantáneo, que marearía igual.
        if (!Animacion.Adorno) return;

        // El transform se monta una sola vez y se reutiliza: crearlo en cada
        // MouseEnter dejaría animaciones viejas colgando del objeto anterior.
        if (e.RenderTransform is not TransformGroup g || g.Children.Count != 2)
        {
            g = new TransformGroup();
            g.Children.Add(new ScaleTransform(1, 1));
            g.Children.Add(new TranslateTransform(0, 0));
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            e.RenderTransform = g;
        }
        var escala = (ScaleTransform)g.Children[0];
        var mueve = (TranslateTransform)g.Children[1];

        double hastaE = arriba ? LevantaEscala : 1;
        double hastaY = arriba ? -LevantaPx : 0;
        double ms = arriba ? 220 : 320;

        Animar(escala, ScaleTransform.ScaleXProperty, escala.ScaleX, hastaE, ms, arriba ? Muelle : Suave);
        Animar(escala, ScaleTransform.ScaleYProperty, escala.ScaleY, hastaE, ms, arriba ? Muelle : Suave);
        Animar(mueve, TranslateTransform.YProperty, mueve.Y, hastaY, ms, arriba ? Muelle : Suave);
    }

    // Fija el valor final y después anima desde donde estuviera
    static void Animar(Animatable obj, DependencyProperty prop, double desde, double hasta,
                       double ms, IEasingFunction curva)
    {
        obj.BeginAnimation(prop, null);
        obj.SetValue(prop, hasta);
        obj.BeginAnimation(prop, Kf(desde, hasta, ms, 0, curva));
    }

    // Se engancha a cualquier botón de la isla
    void ConLevantado(FrameworkElement b)
    {
        b.MouseEnter += (_, _) => Levantar(b, true);
        b.MouseLeave += (_, _) => Levantar(b, false);
    }

    // ---------- Latido al abrir una app fijada ----------
    // Pulsas un icono apagado y la app tarda un segundo largo en aparecer. Sin
    // esto no hay forma de saber si el clic ha entrado.
    static void Latir(FrameworkElement e)
    {
        if (!Animacion.Adorno) return;
        if (e.RenderTransform is not TransformGroup g || g.Children.Count != 2) return;
        var escala = (ScaleTransform)g.Children[0];

        var a = new DoubleAnimationUsingKeyFrames { RepeatBehavior = new RepeatBehavior(3) };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), Suave));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)), Suave));
        escala.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        escala.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    public void LatirIcono(string claveGrupo)
    {
        if (_grupos.TryGetValue(claveGrupo, out var g) && g.Boton is { } b) Latir(b);
    }

    // ---------- Destello del borde de acento ----------
    // Para avisar sin ruido: una notificación, un cambio de volumen. El halo sube
    // y baja una vez. Si no hay borde de acento puesto, no hace nada.
    void DestellarBorde()
    {
        if (!Animacion.Adorno) return;
        if (_brillo == null) return;
        double vuelta = _expandida ? BrilloAbierta : BrilloRecogida;

        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new EasingDoubleKeyFrame(Math.Min(1, vuelta + 0.45),
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), Suave));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(vuelta,
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)), Suave));

        _brillo.BeginAnimation(DropShadowEffect.OpacityProperty, null);
        _brillo.Opacity = vuelta;                 // el valor real vuelve a ser el de siempre
        _brillo.BeginAnimation(DropShadowEffect.OpacityProperty, a);
    }

    // ---------- El reloj pasa de minuto deslizándose ----------
    // Sustituye al destello de opacidad: la cifra vieja sube y se va, la nueva
    // entra desde abajo. Es el detalle que más recuerda a la isla del iPhone.
    static void DeslizarTexto(TextBlock tb)
    {
        if (tb.RenderTransform is not TranslateTransform t)
        {
            t = new TranslateTransform();
            tb.RenderTransform = t;
        }
        t.BeginAnimation(TranslateTransform.YProperty, null);
        t.Y = 0;
        t.BeginAnimation(TranslateTransform.YProperty, Kf(9, 0, 420, 0, Muelle));
        tb.BeginAnimation(OpacityProperty, Kf(0, 1, 260));
    }

    // ---------- Entrada escalonada de los módulos al expandirse ----------
    // Los iconos de un lado no aparecen todos a la vez, sino uno detrás de otro
    // con 25 ms de diferencia. Cuesta cero y se nota mucho.
    static void EntradaEscalonada(Panel lado)
    {
        int i = 0;
        foreach (FrameworkElement hijo in lado.Children.OfType<FrameworkElement>())
        {
            double retardo = i++ * 25;
            hijo.BeginAnimation(OpacityProperty, Kf(0, 1, 220, retardo));
        }
    }
}
