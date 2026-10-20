using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// El módulo "escritorios": los numeritos de los workspaces, como en Linux.
public partial class MainWindow : Window
{
    StackPanel? _escritorios;

    UIElement ModuloEscritorios()
    {
        _escritorios = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent     // sin fondo no llega la rueda a los huecos
        };

        if (!Escritorios.Disponible) return _escritorios;   // Windows que no reconoce: ni se pinta

        // La rueda pasa al siguiente o al anterior, como en los workspaces de Linux
        _escritorios.MouseWheel += (_, e) =>
        {
            Escritorios.Mover(e.Delta < 0);
            e.Handled = true;
            Dispatcher.BeginInvoke(new Action(RefrescarEscritorios),
                                   System.Windows.Threading.DispatcherPriority.Background);
        };

        RefrescarEscritorios();
        _actualizadores.Add(RefrescarEscritorios);
        return _escritorios;
    }

    int _escritoriosPintados = -1;
    int _actualPintado = -1;

    void RefrescarEscritorios()
    {
        if (_escritorios == null || !Escritorios.Disponible) return;
        try
        {
            var todos = Escritorios.Todos();
            int actual = todos.FirstOrDefault(d => d.EsElActual)?.Numero ?? 1;

            // Repintar entero cada segundo haría parpadear el hover: solo cuando
            // de verdad cambia el número de escritorios o cuál es el actual
            if (todos.Count == _escritoriosPintados && actual == _actualPintado) return;
            _escritoriosPintados = todos.Count;
            _actualPintado = actual;

            _escritorios.Children.Clear();
            foreach (var d in todos)
            {
                var b = Boton(NumeroEscritorio(d), d.Nombre, () => Escritorios.Ir(d.Id));
                b.Width = 22;
                b.Padding = new Thickness(0);
                _escritorios.Children.Add(b);
            }

            var mas = Boton(Texto("+", 13), "Escritorio nuevo", () =>
            {
                Escritorios.Crear();
                RefrescarEscritorios();
            });
            mas.Width = 22;
            mas.Padding = new Thickness(0);
            mas.Opacity = 0.5;
            _escritorios.Children.Add(mas);

            AjustarAncho();
        }
        catch (Exception ex) { Log.Error("pintar los escritorios", ex); }
    }

    // El actual va en color de acento y con un puntito debajo; los demás, tenues
    UIElement NumeroEscritorio(Escritorios.Info d)
    {
        var pila = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        pila.Children.Add(new TextBlock
        {
            Text = d.Numero.ToString(),
            Foreground = d.EsElActual ? _acentoLegible : _texto,
            Opacity = d.EsElActual ? 1 : 0.45,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        pila.Children.Add(new Border
        {
            Width = 10, Height = 2, CornerRadius = new CornerRadius(1),
            Background = _acento,
            Opacity = d.EsElActual ? 1 : 0,
            Margin = new Thickness(0, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        return pila;
    }
}
