using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// Reordenar las apps del carrusel cogiéndolas y arrastrándolas.
//
// El orden que resulta se guarda en Barra.OrdenApps, así que sobrevive a cerrar
// NubeBar. Los iconos miden todos lo mismo, así que no hace falta nada
// sofisticado: cuando el que llevas pasa de la mitad de su vecino, se
// intercambian y listo.
public partial class MainWindow : Window
{
    Button? _iconoArrastrado;
    Grupo? _grupoArrastrado;
    TranslateTransform? _llevaIcono;
    double _raizRatonIcono;              // X del ratón al empezar, en píxeles
    int _zIndexAntes;
    EventHandler? _siguiendoIcono;

    const double UmbralIcono = 5;

    // Se engancha desde CrearBotonGrupo
    void ConReordenado(Button b, Grupo g)
    {
        b.PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (!_cfg.Barra.Arrastrable || _siguiendoIcono != null) return;
            _iconoArrastrado = b;
            _grupoArrastrado = g;
            _raizRatonIcono = Monitores.Raton().X;
            _siguiendoIcono = (_, _) => SeguirIcono();
            CompositionTarget.Rendering += _siguiendoIcono;
        };
    }

    void SeguirIcono()
    {
        if (_iconoArrastrado is not { } b || _tira == null) { SoltarIcono(); return; }

        if (!Nativo.BotonIzquierdoPulsado()) { SoltarIcono(); return; }

        double dxPx = Monitores.Raton().X - _raizRatonIcono;
        double dx = dxPx / EscalaDeWpf();            // el ratón va en píxeles, el layout en unidades de WPF

        if (_llevaIcono == null)
        {
            if (Math.Abs(dx) < UmbralIcono) return;

            // Empieza el arrastre: se levanta y se pone por encima de los demás
            _llevaIcono = new TranslateTransform();
            b.RenderTransform = _llevaIcono;
            _zIndexAntes = Panel.GetZIndex(b);
            Panel.SetZIndex(b, 99);
            b.Opacity = 0.9;
            b.Cursor = Cursors.SizeWE;
            Log.Info($"reordenando {System.IO.Path.GetFileName(_grupoArrastrado?.Exe)}");
        }

        int i = _tira.Children.IndexOf(b);
        if (i < 0) { SoltarIcono(); return; }

        // ¿Ha pasado de la mitad del vecino? Entonces se cambian el sitio y se
        // descuenta su ancho del arrastre, para que el icono siga bajo el cursor.
        if (dx > 0 && i + 1 < _tira.Children.Count && _tira.Children[i + 1] is FrameworkElement der)
        {
            double mitad = (der.ActualWidth > 0 ? der.ActualWidth : AnchoIcono) / 2;
            if (dx > mitad)
            {
                Intercambiar(i, i + 1);
                _raizRatonIcono += (der.ActualWidth > 0 ? der.ActualWidth : AnchoIcono) * EscalaDeWpf();
                dx = (Monitores.Raton().X - _raizRatonIcono) / EscalaDeWpf();
            }
        }
        else if (dx < 0 && i - 1 >= 0 && _tira.Children[i - 1] is FrameworkElement izq)
        {
            double mitad = (izq.ActualWidth > 0 ? izq.ActualWidth : AnchoIcono) / 2;
            if (-dx > mitad)
            {
                Intercambiar(i, i - 1);
                _raizRatonIcono -= (izq.ActualWidth > 0 ? izq.ActualWidth : AnchoIcono) * EscalaDeWpf();
                dx = (Monitores.Raton().X - _raizRatonIcono) / EscalaDeWpf();
            }
        }

        _llevaIcono.X = dx;
    }

    // El vecino se desliza al hueco que deja el que llevas: sin esto el cambio
    // es un salto seco y no se entiende qué ha pasado.
    void Intercambiar(int desde, int hasta)
    {
        if (_tira == null) return;
        var b = _tira.Children[desde];
        var vecino = _tira.Children[hasta] as FrameworkElement;
        _tira.Children.RemoveAt(desde);
        _tira.Children.Insert(hasta, b);

        if (vecino == null) return;
        double ancho = vecino.ActualWidth > 0 ? vecino.ActualWidth : AnchoIcono;
        double desdeX = hasta > desde ? ancho : -ancho;
        if (vecino.RenderTransform is not TranslateTransform t)
        {
            t = new TranslateTransform();
            vecino.RenderTransform = t;
        }
        t.BeginAnimation(TranslateTransform.XProperty, null);
        t.X = 0;
        t.BeginAnimation(TranslateTransform.XProperty, Kf(desdeX, 0, 220, 0, Suave));
    }

    void SoltarIcono()
    {
        if (_siguiendoIcono != null)
        {
            CompositionTarget.Rendering -= _siguiendoIcono;
            _siguiendoIcono = null;
        }

        if (_iconoArrastrado is { } b && _llevaIcono is { } t)
        {
            // Vuelve a su casilla con muelle, y el valor real queda en 0
            double desde = t.X;
            t.BeginAnimation(TranslateTransform.XProperty, null);
            t.X = 0;
            t.BeginAnimation(TranslateTransform.XProperty, Kf(desde, 0, 320, 0, Muelle));
            b.Opacity = 1;
            b.Cursor = null;
            Panel.SetZIndex(b, _zIndexAntes);
            ApuntarArrastre();      // soltar el icono aquí no es pulsarlo
            GuardarOrden();
        }

        _iconoArrastrado = null;
        _grupoArrastrado = null;
        _llevaIcono = null;
    }

    // El orden que se ve ahora mismo, de izquierda a derecha
    List<string> OrdenVisible()
    {
        if (_tira == null) return new List<string>();
        return _tira.Children.OfType<Button>()
            .Select(b => _grupos.FirstOrDefault(p => p.Value.Boton == b).Key)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();
    }

    void GuardarOrden()
    {
        try
        {
            var orden = OrdenVisible();
            if (orden.Count == 0) return;

            var arbol = ConfigArbol.Cargar();
            arbol.PonerLista("Barra", "OrdenApps", orden);
            arbol.Guardar();
            Log.Info($"orden de las apps guardado: {string.Join(" ", orden.Select(System.IO.Path.GetFileName))}");
        }
        catch (Exception ex) { Log.Error("guardar el orden de las apps", ex); }
    }

    // ¿El ratón se posó sobre el carrusel? Entonces estás moviendo un icono, no
    // la isla entera, y el arrastre de la isla tiene que quedarse quieto.
    bool EsDelCarrusel(object? origen)
    {
        if (_tira == null || origen is not DependencyObject d) return false;
        while (d != null)
        {
            if (ReferenceEquals(d, _tira)) return true;
            d = VisualTreeHelper.GetParent(d) ?? (d as FrameworkElement)?.Parent!;
        }
        return false;
    }
}
