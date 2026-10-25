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
            // Lo silenciado en el config de siempre sigue valiendo, y además lo que
            // hayas silenciado desde el propio centro. Las dos listas se juntan al
            // arrancar (ver EncajarSilenciadasDelConfig).
            var centro = Centro.Actual;

            // El centro se queda TODO lo que llega —es su historial— y devuelve solo lo
            // que además hay que enseñar: ni lo descartado, ni lo repetido, ni lo de una
            // app silenciada.
            var paraLaIsla = centro.Recibir(Notificaciones.Nuevas());
            if (paraLaIsla.Count > 0) centro.Guardar();

            foreach (var a in paraLaIsla)
            {
                // "No molestar" solo impide que se ASOME: en el centro se queda. Antes
                // se descartaban aquí mismo y el modo silencio se comía el historial.
                if (centro.NoMolestar || NoMolestar.Activo) continue;
                _cola.Enqueue(new Notificaciones.Aviso(a.Id, a.App, a.Titulo, a.Texto,
                                                       a.Cuando, a.AumId, a.Acciones));
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
