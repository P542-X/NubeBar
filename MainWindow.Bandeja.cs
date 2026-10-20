using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using ManagedShell.Common.Helpers;
using ManagedShell.WindowsTray;

namespace NubeBar;

// El módulo "bandeja": los iconos de las apps que viven junto al reloj.
public partial class MainWindow : Window
{
    StackPanel? _bandeja;
    Button? _botonMas;
    readonly Dictionary<NotifyIcon, Button> _botonesBandeja = new();

    const string PanelBandeja = "bandeja";
    const double AnchoIconoBandeja = 26;

    bool HayModuloBandeja() =>
        _cfg.Modulos.Izquierda.Concat(_cfg.Modulos.Centro).Concat(_cfg.Modulos.Derecha)
                     .Any(m => m.Equals("bandeja", StringComparison.OrdinalIgnoreCase));

    UIElement ModuloBandeja()
    {
        _bandeja = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _botonesBandeja.Clear();
        // El botón "^" se vuelve a crear en cada Aplicar; si se queda el viejo, sigue
        // teniendo por padre el panel anterior y añadirlo al nuevo lanza "ya es hijo
        // de otro elemento", que abortaba Aplicar() a medias y dejaba la isla coja.
        _botonMas = null;

        // Suscribirse siempre, aunque la bandeja todavía no esté en marcha: los
        // módulos se montan en Aplicar(), que corre ANTES de encenderla, y si aquí
        // nos rindiéramos el panel se quedaría vacío para siempre. Cuando arranque
        // (o cuando llegue un icono nuevo) avisará y esto se rellena solo.
        Bandeja.Cambio -= RefrescarBandeja;
        Bandeja.Cambio += RefrescarBandeja;
        RefrescarBandeja();
        return _bandeja;
    }

    void RefrescarBandeja()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(RefrescarBandeja)); return; }
        if (_bandeja == null || Bandeja.Iconos is not { } iconos) return;

        int tope = Math.Max(1, _cfg.Barra.MaxBandeja);
        // Los que no caben se van al desplegable "^", igual que en Windows
        var dentro = iconos.Where(i => !i.IsHidden).Take(tope).ToList();

        // Los que ya no están: se encogen y se van
        foreach (var par in _botonesBandeja.Where(p => !dentro.Contains(p.Key)).ToList())
        {
            _botonesBandeja.Remove(par.Key);
            AnimarSalida(par.Value, () => _bandeja.Children.Remove(par.Value));
        }

        // Los nuevos, en su sitio y creciendo
        for (int i = 0; i < dentro.Count; i++)
        {
            var icono = dentro[i];
            if (_botonesBandeja.TryGetValue(icono, out var ya))
            {
                int donde = _bandeja.Children.IndexOf(ya);
                if (donde != i && donde >= 0 && i < _bandeja.Children.Count)
                {
                    _bandeja.Children.Remove(ya);
                    _bandeja.Children.Insert(Math.Min(i, _bandeja.Children.Count), ya);
                }
                ya.ToolTip = TipDe(icono);
                continue;
            }
            var b = BotonBandeja(icono);
            _botonesBandeja[icono] = b;
            _bandeja.Children.Insert(Math.Min(i, _bandeja.Children.Count), b);
            AnimarEntrada(b, 0);
        }

        // El "^" solo aparece si de verdad sobra algo
        int sobran = iconos.Count(i => !i.IsHidden) - dentro.Count;
        if (sobran > 0)
        {
            if (_botonMas == null)
            {
                _botonMas = Boton(Glifo(""), "Más iconos", AlternarBandejaExtra);
                _botonMas.Width = AnchoIconoBandeja;
                _bandeja.Children.Add(_botonMas);
                AnimarEntrada(_botonMas, 0);
            }
            else if (!_bandeja.Children.Contains(_botonMas)) _bandeja.Children.Add(_botonMas);
            _botonMas.ToolTip = $"Otros {sobran} iconos";
        }
        else if (_botonMas != null && _bandeja.Children.Contains(_botonMas))
        {
            var viejo = _botonMas;
            _botonMas = null;
            AnimarSalida(viejo, () => _bandeja.Children.Remove(viejo));
        }

        AjustarAncho();
    }

    static string TipDe(NotifyIcon icono) =>
        string.IsNullOrWhiteSpace(icono.Title) ? "(sin nombre)" : icono.Title;

    Button BotonBandeja(NotifyIcon icono)
    {
        var img = new Image
        {
            Source = icono.Icon,
            Width = 16, Height = 16,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

        var b = new Button
        {
            Content = img,
            ToolTip = TipDe(icono),
            Width = AnchoIconoBandeja,
            Height = Math.Max(20, _cfg.Barra.Alto - 8),
            Padding = new Thickness(0)
        };

        // El icono cambia solo (Discord con mensajes, el antivirus escaneando...)
        icono.PropertyChanged += (_, e) =>
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => ActualizarIcono(img, b, icono, e.PropertyName))); return; }
            ActualizarIcono(img, b, icono, e.PropertyName);
        };

        // Los clics se le reenvían tal cual a la app dueña del icono: ella decide
        // si abre su ventana, su menú o lo que sea. Hay que decirle antes DÓNDE está
        // el icono en pantalla o su menú saldría en una esquina cualquiera.
        b.PreviewMouseDown += (_, e) => { SitioDelIcono(b, icono); Reenviar(icono, e.ChangedButton, abajo: true); e.Handled = true; };
        b.PreviewMouseUp += (_, e) => { SitioDelIcono(b, icono); Reenviar(icono, e.ChangedButton, abajo: false); e.Handled = true; };
        b.MouseEnter += (_, _) => { try { icono.IconMouseEnter(MouseHelper.GetCursorPositionParam()); } catch { } };
        b.MouseLeave += (_, _) => { try { icono.IconMouseLeave(MouseHelper.GetCursorPositionParam()); } catch { } };

        return b;
    }

    void ActualizarIcono(Image img, Button b, NotifyIcon icono, string? propiedad)
    {
        try
        {
            if (propiedad is null or "Icon") img.Source = icono.Icon;
            if (propiedad is null or "Title") b.ToolTip = TipDe(icono);
            if (propiedad == "IsHidden") RefrescarBandeja();
        }
        catch (Exception ex) { Log.Error("refrescar icono de bandeja", ex); }
    }

    void SitioDelIcono(Button b, NotifyIcon icono)
    {
        try
        {
            var p = b.PointToScreen(new Point(0, 0));
            Bandeja.FijarSitio(icono, p.X, p.Y, b.ActualWidth * _monitor.Escala, b.ActualHeight * _monitor.Escala);
        }
        catch { }
    }

    void Reenviar(NotifyIcon icono, MouseButton boton, bool abajo)
    {
        try
        {
            uint raton = MouseHelper.GetCursorPositionParam();
            int dobleClic = Nativo.TiempoDobleClic;
            if (abajo) icono.IconMouseDown(boton, raton, dobleClic);
            else icono.IconMouseUp(boton, raton, dobleClic);
        }
        catch (Exception ex) { Log.Error("reenviar clic a la bandeja", ex); }
    }

    // ---------- El desplegable "^" ----------
    void AlternarBandejaExtra()
    {
        if (Bandeja.Iconos is not { } iconos) return;
        int tope = Math.Max(1, _cfg.Barra.MaxBandeja);
        var resto = iconos.Where(i => !i.IsHidden).Skip(tope).ToList();
        if (resto.Count == 0) return;

        int porFila = Math.Min(6, resto.Count);
        int filas = (int)Math.Ceiling(resto.Count / (double)porFila);

        AlternarPanel(PanelBandeja, () => RejillaBandeja(resto, porFila),
                      porFila * 40 + 20, filas * 40 + 20);
    }

    FrameworkElement RejillaBandeja(List<NotifyIcon> iconos, int porFila)
    {
        var rejilla = new UniformGrid
        {
            Columns = porFila,
            Background = Brushes.Transparent,   // sin fondo, un Panel no recibe el ratón en los huecos
            Margin = new Thickness(10, 4, 10, 10)
        };
        foreach (var i in iconos)
        {
            var b = BotonBandeja(i);
            b.Width = 36;
            b.Height = 36;
            rejilla.Children.Add(b);
        }
        return rejilla;
    }
}
