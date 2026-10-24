using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NubeBar;

// El estilo de los botones de la isla.
//
// Antes vivía en MainWindow.xaml, con sus dos storyboards escritos a mano: el fundido
// del fondo al pasar el ratón y el encogido al 82 % al pulsar. El problema es que un
// storyboard del XAML tiene su Duration congelada dentro de la plantilla, así que no
// había manera de que el nivel de animación lo tocara: en "reducido" el botón seguía
// encogiéndose igual. Era el cabo suelto que quedó apuntado al cerrar la Fase A.
//
// Aquí se construye lo mismo, pero decidiendo según el nivel. Se vuelve a montar en
// cada AplicarTema, que es también cuando se vuelven a crear los botones, así que
// cambiar el nivel en caliente se nota en el acto.
public partial class MainWindow : Window
{
    const double EncogidoAlPulsar = 0.82;

    Style EstiloBoton()
    {
        var plantilla = new ControlTemplate(typeof(Button));

        var raiz = new FrameworkElementFactory(typeof(Grid));
        raiz.SetValue(Panel.BackgroundProperty, Brushes.Transparent);

        // El fondo que aparece bajo el ratón. Su color es el rol SuperficieAlta, que
        // llega por el recurso "Hover" (ver AplicarTema).
        var fondo = new FrameworkElementFactory(typeof(Border), "bg");
        fondo.SetResourceReference(Border.BackgroundProperty, "Hover");
        fondo.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        fondo.SetValue(UIElement.OpacityProperty, 0.0);
        raiz.AppendChild(fondo);

        var contenido = new FrameworkElementFactory(typeof(ContentPresenter), "cp");
        contenido.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
        contenido.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contenido.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        contenido.SetValue(FrameworkElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
        contenido.SetValue(UIElement.RenderTransformProperty, new ScaleTransform());
        raiz.AppendChild(contenido);

        plantilla.VisualTree = raiz;
        plantilla.Triggers.Add(DisparadorRaton());
        if (Animacion.Encogido) plantilla.Triggers.Add(DisparadorPulsado());

        var estilo = new Style(typeof(Button));
        estilo.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 0)));
        estilo.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 0, 6, 0)));
        estilo.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        estilo.Setters.Add(new Setter(Control.TemplateProperty, plantilla));
        return estilo;
    }

    // ---------- El ratón por encima ----------
    // Con fundido mientras haya fundidos que valgan la pena. En "desactivado" no se
    // usa un storyboard de cero milisegundos sino un Setter de verdad: así no queda
    // ninguna transición que mirar, que es lo que se ha pedido.
    Trigger DisparadorRaton()
    {
        var t = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };

        if (!Animacion.HayFundido)
        {
            t.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "bg"));
            return t;
        }

        t.EnterActions.Add(Guion("bg", UIElement.OpacityProperty, 1.0, Animacion.EscalarFundido(150)));
        t.ExitActions.Add(Guion("bg", UIElement.OpacityProperty, 0.0, Animacion.EscalarFundido(300)));
        return t;
    }

    // ---------- El encogido al pulsar ----------
    // El rebote al soltar (BackEase) es lo que más se nota, así que es lo primero que
    // se va: en "equilibrado" el botón encoge y vuelve, pero sin pasarse de largo.
    Trigger DisparadorPulsado()
    {
        var t = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        var vuelta = Animacion.Rebote ? new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut } : null;

        foreach (var prop in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            var ruta = new PropertyPath("(UIElement.RenderTransform).(ScaleTransform." + prop.Name + ")");
            t.EnterActions.Add(Guion("cp", ruta, EncogidoAlPulsar, Animacion.Escalar(80)));
            t.ExitActions.Add(Guion("cp", ruta, 1.0, Animacion.Escalar(350), vuelta));
        }
        return t;
    }

    // Un storyboard de una sola animación sobre un elemento con nombre de la plantilla.
    static BeginStoryboard Guion(string destino, DependencyProperty prop, double hasta, double ms,
                                 IEasingFunction? curva = null)
        => Guion(destino, new PropertyPath(prop), hasta, ms, curva);

    static BeginStoryboard Guion(string destino, PropertyPath ruta, double hasta, double ms,
                                 IEasingFunction? curva = null)
    {
        var a = new DoubleAnimation
        {
            To = hasta,
            Duration = new Duration(TimeSpan.FromMilliseconds(Math.Max(0, ms))),
            EasingFunction = curva,
            // La regla de la casa: pase lo que pase con la animación, la propiedad se
            // queda en el valor final y no vuelve sola al de la plantilla.
            FillBehavior = FillBehavior.HoldEnd
        };
        Storyboard.SetTargetName(a, destino);
        Storyboard.SetTargetProperty(a, ruta);

        var guion = new Storyboard();
        guion.Children.Add(a);
        return new BeginStoryboard { Storyboard = guion };
    }
}
