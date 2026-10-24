using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NubeBar;

// Las notificaciones asoman en la isla unos segundos y se van.
public partial class MainWindow : Window
{
    const string PanelNota = "nota";
    const double AltoNota = 62;

    static readonly Queue<Notificaciones.Aviso> _cola = new();
    static DispatcherTimer? _cierraNota;
    static int _tickDeNotas;

    // Se mira cada 3 segundos, no cada uno: copiar la base y abrirla cuesta unos
    // milisegundos y no hay ninguna prisa por enseñar un aviso dos segundos antes.
    void VigilarNotificaciones()
    {
        if (!_esJefa || !_cfg.Barra.Notificaciones) return;
        if (++_tickDeNotas % 3 != 0) return;

        try
        {
            var silenciadas = _cfg.Barra.NotificacionesSilenciadas ?? new List<string>();
            foreach (var a in Notificaciones.Nuevas())
            {
                // "No molestar" manda: se descartan, no se encolan para después
                if (NoMolestar.Activo) continue;
                if (silenciadas.Any(s => a.AumId.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
                _cola.Enqueue(a);
            }
            SiguienteNota();
        }
        catch (Exception ex) { Log.Error("vigilar notificaciones", ex); }
    }

    void SiguienteNota()
    {
        if (_cola.Count == 0) return;

        var isla = IslaDelRaton();
        // Si hay algo abierto (el menú, la vista, un aviso de volumen) se espera:
        // una notificación no debe echarte de lo que estabas haciendo
        if (isla._panelAbierto != null && isla._panelAbierto != PanelNota) return;

        var a = _cola.Dequeue();
        isla.AbrirPanel(PanelNota, isla.CrearNota(a), 420, AltoNota);
        isla.DestellarBorde();
        Log.Info($"notificación en la isla: {a.App}");

        _cierraNota ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _cierraNota.Stop();
        _cierraNota.Tick -= CerrarNotaTick;
        _cierraNota.Tick += CerrarNotaTick;
        _cierraNota.Start();
    }

    static void CerrarNotaTick(object? s, EventArgs e)
    {
        _cierraNota?.Stop();
        foreach (var i in Islas.Where(i => i._panelAbierto == PanelNota).ToList()) i.CerrarPanel();
        // Si se han amontonado, la siguiente entra en cuanto la anterior sale
        if (_cola.Count > 0)
            Islas.FirstOrDefault(i => i._esJefa)?.Dispatcher
                 .BeginInvoke(new Action(() => Islas.FirstOrDefault(i => i._esJefa)?.SiguienteNota()),
                              DispatcherPriority.Background);
    }

    FrameworkElement CrearNota(Notificaciones.Aviso a)
    {
        var rejilla = new Grid { Margin = new Thickness(14, 0, 14, 10) };
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var campana = Glifo("");
        campana.FontSize = 18;
        campana.Margin = new Thickness(0, 0, 10, 0);
        Grid.SetColumn(campana, 0);
        rejilla.Children.Add(campana);

        var pila = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        pila.Children.Add(new TextBlock
        {
            Text = a.App, Foreground = _acentoLegible, FontSize = 10, Opacity = 0.9
        });
        pila.Children.Add(new TextBlock
        {
            Text = a.Titulo, Foreground = _texto, FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (a.Texto.Length > 0)
            pila.Children.Add(new TextBlock
            {
                Text = a.Texto, Foreground = _textoAtenuado, FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        Grid.SetColumn(pila, 1);
        rejilla.Children.Add(pila);

        // Pulsar abre la app dueña y cierra el aviso
        rejilla.Background = Brushes.Transparent;   // sin fondo, un Panel no recibe el ratón
        rejilla.Cursor = System.Windows.Input.Cursors.Hand;
        rejilla.MouseLeftButtonUp += (_, _) =>
        {
            Notificaciones.Abrir(a.AumId);
            CerrarPanel();
        };
        return rejilla;
    }
}
