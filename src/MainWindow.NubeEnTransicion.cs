using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NubeBar;

// E6: la nube en las transiciones.
//
// ---------- Dónde sale y dónde no ----------
//
// Lo decide `Transiciones.ConNube`, que es puro y tiene pruebas. Resumido: sale donde el
// cambio es **de NubeBar o de su estado** —cambiar de escritorio, abrir una superficie,
// recuperarse de un error, arrancar— y no sale al cerrar ni al cambiar el foco de una ventana
// a otra, que son cosas tuyas y del sistema.
//
// Si saliera en todas, aparecería ocho veces por minuto y dejaría de significar nada: sería
// una mascota, que es exactamente lo que el documento de E prohíbe.
//
// ---------- El tamaño ----------
//
// Con los topes de D6 (`HyprPinta.TamanoDeLaNube`), que E6 **no levanta**. Una nube de
// transición es pequeña: marca el cambio, no lo celebra.
public partial class MainWindow
{
    // La nube del cambio de escritorio vive sobre el indicador de escritorios, no en medio de
    // la pantalla. Un destello en el centro del escritorio taparía justo lo que acabas de ir
    // a ver.
    System.Windows.Shapes.Path? _nubeDelCambio;
    int _escritorioQueVi = -1;

    // ---------- El cambio de escritorio ----------
    //
    // Se llama desde el refresco de los escritorios, que ya corre con el pulso. Solo hace algo
    // cuando el número del escritorio actual **ha cambiado de verdad**, que es la regla de D6
    // que E6 hereda.
    void NubeAlCambiarDeEscritorio(int actual)
    {
        try
        {
            int antes = _escritorioQueVi;
            _escritorioQueVi = actual;

            // La primera lectura no es un cambio: es enterarse de dónde estabas.
            if (antes < 0) return;
            if (!Transiciones.TocaAnimar(Transiciones.Que.CambioDeEscritorio, antes, actual))
                return;
            if (!Transiciones.ConNube(Transiciones.Que.CambioDeEscritorio)) return;
            if (_escritorios == null) return;

            DestelloDeNube(_escritorios,
                           Transiciones.Duracion(Transiciones.Que.CambioDeEscritorio),
                           HyprPinta.Azul(_roles));
        }
        catch (Exception ex) { Log.Error("la nube del cambio de escritorio", ex); }
    }

    // ---------- La recuperación ----------
    //
    // Salir del modo seguro es una buena noticia y conviene verla: hasta ahora solo se
    // apuntaba en el registro, y un registro no lo mira nadie mientras trabaja.
    bool _estabaEnModoSeguro;

    void NubeAlRecuperarse()
    {
        try
        {
            bool ahora = HyprSalud.Seguro;
            bool antes = _estabaEnModoSeguro;
            _estabaEnModoSeguro = ahora;

            // Solo al SALIR. Entrar en modo seguro ya se cuenta con el color de la nube de la
            // barra, que se pone coral; un destello al entrar sería celebrar un problema.
            if (!antes || ahora) return;
            if (!Transiciones.TocaAnimar(Transiciones.Que.Recuperacion, antes, ahora)) return;
            if (_escritorios == null && _tira == null) return;

            var donde = (FrameworkElement?)_escritorios ?? _tira;
            if (donde == null) return;

            DestelloDeNube(donde, Transiciones.Duracion(Transiciones.Que.Recuperacion),
                           HyprPinta.Azul(_roles));
            Log.Info("[E6] NubeHypr ha salido del modo seguro: destello de recuperación");
        }
        catch (Exception ex) { Log.Error("la nube de la recuperación", ex); }
    }

    // ---------- El destello ----------
    //
    // Una nube que aparece y se va encima del elemento que ha cambiado. Se monta en la capa
    // de adornos de la isla, no dentro del módulo: meterla dentro cambiaría el tamaño del
    // módulo a mitad de animación y movería los de al lado.
    //
    // **Cancelable**: si llega otro cambio antes de que termine, el anterior se corta y
    // empieza el nuevo. Dos destellos encadenados sobre el mismo sitio se ven como un
    // parpadeo, no como dos cambios.
    void DestelloDeNube(FrameworkElement sobre, TimeSpan cuanto, Color color)
    {
        if (cuanto <= TimeSpan.Zero) return;        // animaciones apagadas: no se lanza nada

        try
        {
            // La capa: `Raiz` es el Grid de la ventana y, al contrario que `Isla`, no
            // recorta. Un destello dentro de la isla se cortaría contra su filo justo en el
            // momento en que crece.

            // El tamaño sale del elemento, con los topes de D6. Nunca una mascota.
            double lado = Transiciones.TamanoDeLaNube(
                Math.Max(16, Math.Min(sobre.ActualWidth, sobre.ActualHeight) * 1.6));
            if (lado <= 0) return;

            // Se reaprovecha la misma nube: crear una por cambio dejaría una docena de
            // formas muertas en el árbol visual en cinco minutos de uso.
            if (_nubeDelCambio == null)
            {
                _nubeDelCambio = new System.Windows.Shapes.Path
                {
                    Data = Nubecita.Molde(),
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false,       // no se puede pulsar: es un adorno
                    Opacity = 0,
                };
                Raiz.Children.Add(_nubeDelCambio);
            }

            var n = _nubeDelCambio;
            n.Width = n.Height = lado;
            n.Fill = new SolidColorBrush(color);

            // Centrada sobre el elemento, en coordenadas de la capa de adornos.
            var p = sobre.TranslatePoint(
                new Point(sobre.ActualWidth / 2, sobre.ActualHeight / 2), Raiz);
            n.Margin = new Thickness(p.X - lado / 2, p.Y - lado / 2, 0, 0);
            n.HorizontalAlignment = HorizontalAlignment.Left;
            n.VerticalAlignment = VerticalAlignment.Top;

            // Cancelar lo que hubiera: `BeginAnimation(null)` corta en seco, que es justo lo
            // que «cancelable» significa aquí.
            n.BeginAnimation(OpacityProperty, null);
            n.Opacity = 0;

            // Entra deprisa y se va: la mitad del tiempo subiendo y la otra bajando.
            var sube = new DoubleAnimation(0, 0.9, new Duration(cuanto * 0.35));
            var baja = new DoubleAnimation(0.9, 0, new Duration(cuanto * 0.65))
            {
                BeginTime = cuanto * 0.35,
            };
            var guion = new Storyboard();
            Storyboard.SetTarget(sube, n);
            Storyboard.SetTargetProperty(sube, new PropertyPath(OpacityProperty));
            Storyboard.SetTarget(baja, n);
            Storyboard.SetTargetProperty(baja, new PropertyPath(OpacityProperty));
            guion.Children.Add(sube);
            guion.Children.Add(baja);
            guion.Begin();
        }
        catch (Exception ex) { Log.Error("el destello de la nube", ex); }
    }
}
