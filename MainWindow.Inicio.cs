using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// Menú Inicio propio. En esta fase solo está el armazón (cabecera, caja de búsqueda y
// zona de resultados) para validar el mecanismo de paneles: el buscador llega en la fase 4.
public partial class MainWindow
{
    const string PanelInicio = "inicio";
    TextBox? _caja;

    void AbrirMenuInicio()
    {
        if (!AlternarPanel(PanelInicio, CrearMenuInicio, 560, 320, teclado: true)) return;
        // El cursor tiene que estar ya dentro de la caja: se escribe sin tocar nada más
        Dispatcher.InvokeAsync(() => _caja?.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    FrameworkElement CrearMenuInicio()
    {
        var raiz = new Grid { Margin = new Thickness(4, 10, 4, 12) };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _caja = new TextBox
        {
            Height = 38,
            Padding = new Thickness(12, 0, 12, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(0x28, _cTexto.R, _cTexto.G, _cTexto.B)),
            Foreground = _texto,
            CaretBrush = _acento,
            BorderThickness = new Thickness(0),
            FontSize = FontSize + 2
        };
        _caja.KeyDown += (_, e) => { if (e.Key == Key.Escape) { CerrarPanel(); e.Handled = true; } };
        Grid.SetRow(_caja, 0);
        raiz.Children.Add(_caja);

        var aviso = new TextBlock
        {
            Text = "Escribe para buscar  ·  Esc para cerrar",
            Foreground = _texto,
            Opacity = 0.45,
            Margin = new Thickness(12, 16, 12, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(aviso, 1);
        raiz.Children.Add(aviso);

        // Eco de lo que se escribe: sirve para comprobar que el teclado llega de verdad
        var eco = new TextBlock
        {
            Foreground = _acento,
            Margin = new Thickness(12, 42, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            FontSize = FontSize + 4
        };
        _caja.TextChanged += (_, _) => eco.Text = _caja.Text;
        Grid.SetRow(eco, 1);
        raiz.Children.Add(eco);

        return raiz;
    }
}
