using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace NubeBar;

// Vista general tipo Exposé: todas las ventanas abiertas en una cuadrícula que
// sale de la isla, con atajo global para abrirla sin tocar el ratón.
public partial class MainWindow
{
    const string PanelVista = "vista";
    const int IdAtajoVista = 0xB0CA;
    const int MaxEnVista = 24;
    const double AnchoCelda = 210, AltoCelda = 46;

    Button? _botonVista;
    TextBlock? _textoVista;
    bool _vistaTocada;          // el ratón ha llegado a entrar en el panel
    HwndSource? _fuente;

    // ================= Atajo global =================
    void RegistrarAtajos()
    {
        _fuente = HwndSource.FromHwnd(_self);
        _fuente?.AddHook(Mensaje);
        AplicarAtajo();
    }

    void AplicarAtajo()
    {
        if (_self == IntPtr.Zero || !_esJefa) return;   // un solo dueño del atajo global
        var atajo = _cfg.Barra.AtajoVistaGeneral;
        if (string.IsNullOrWhiteSpace(atajo)) { Nativo.QuitarAtajo(_self, IdAtajoVista); return; }

        bool ok = Nativo.RegistrarAtajo(_self, IdAtajoVista, atajo);
        string motivo = Nativo.UltimoErrorAtajo switch
        {
            0 => "",
            -1 => " (no entiendo la combinación)",
            1409 => " (ya lo tiene cogido otra aplicación)",
            _ => $" (error {Nativo.UltimoErrorAtajo})"
        };
        Log.Info($"atajo de la vista general \"{atajo}\": {(ok ? "registrado" : "NO registrado" + motivo)}");
    }

    IntPtr Mensaje(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool manejado)
    {
        if (msg != Nativo.WM_HOTKEY || wParam.ToInt32() != IdAtajoVista) return IntPtr.Zero;
        manejado = true;
        // La vista general se abre en la pantalla donde tengas el ratón.
        // Si estaba abierta en otra, esa se cierra y no se abre una segunda.
        var abierta = Islas.FirstOrDefault(i => i._panelAbierto == PanelVista);
        (abierta ?? IslaDelRaton()).AlternarVista();
        return IntPtr.Zero;
    }

    // ================= Botón "+N" =================
    UIElement CrearBotonVista()
    {
        _textoVista = new TextBlock
        {
            FontFamily = new FontFamily(FuenteIconos),
            FontSize = 14,
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _botonVista = Boton(_textoVista, "Ver todas las ventanas", AlternarVista);
        ActualizarBotonVista();
        return _botonVista;
    }

    // Glifo de cuadrícula, y "+N" si hay grupos que no caben en el carrusel
    void ActualizarBotonVista()
    {
        if (_textoVista == null || _botonVista == null) return;
        int ocultos = _tira == null ? 0 : Math.Max(0, _tira.Children.Count - MaxAppsEfectivo);
        _textoVista.Text = ocultos > 0 ? $" +{ocultos}" : "";
        _textoVista.FontFamily = ocultos > 0
            ? new FontFamily($"{FuenteIconos}, {_cfg.Tema.Fuente}")
            : new FontFamily(FuenteIconos);
        int ventanas = _grupos.Values.Sum(g => g.Ventanas.Count);
        _botonVista.ToolTip = $"Ver todas las ventanas ({ventanas})";
    }

    // ================= Abrir y cerrar =================
    void AlternarVista()
    {
        if (_panelAbierto == PanelVista) { CerrarPanel(); return; }
        AbrirVista();
    }

    void AbrirVista()
    {
        var ventanas = _grupos.Values
            .SelectMany(g => g.Ventanas.Select(h => (Grupo: g, Ventana: h)))
            .Take(MaxEnVista)
            .ToList();
        if (ventanas.Count == 0) return;

        int columnas = Math.Max(1, Math.Min(4, (int)Math.Ceiling(Math.Sqrt(ventanas.Count))));
        int filas = (int)Math.Ceiling(ventanas.Count / (double)columnas);

        _vistaTocada = false;
        AbrirPanel(PanelVista, CrearRejilla(ventanas, columnas),
                   columnas * AnchoCelda + 16, filas * AltoCelda + 16);
    }

    FrameworkElement CrearRejilla(List<(Grupo Grupo, IntPtr Ventana)> ventanas, int columnas)
    {
        // Nada de GetForegroundWindow a secas: cuando la vista se abre con el atajo
        // global, Windows devuelve 0 mientras procesa la combinación de teclas y no
        // se resaltaba ninguna ventana. ActivaAjena recuerda la última de verdad.
        var activa = ActivaAjena();

        var rejilla = new UniformGrid
        {
            Columns = columnas,
            Margin = new Thickness(6, 6, 6, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            // Sin fondo, un Panel no recibe ratón en los huecos entre celdas y el
            // "cerrar al salir" no se enteraba de nada
            Background = Brushes.Transparent
        };

        foreach (var (grupo, h) in ventanas)
            rejilla.Children.Add(CrearCelda(grupo, h, h == activa));

        var resaltada = ventanas.FirstOrDefault(v => v.Ventana == activa).Ventana;
        Log.Info($"vista general: {ventanas.Count} ventanas, foreground 0x{Nativo.Activa().ToInt64():X} ultima 0x{_ultimaActiva.ToInt64():X} = " +
                 (resaltada == IntPtr.Zero ? "ninguna de la rejilla" : Nativo.Titulo(resaltada)));

        // Solo se cierra al salir si antes ha entrado: si se abrió con el atajo,
        // el ratón puede estar en cualquier sitio y se cerraría al primer movimiento
        rejilla.MouseEnter += (_, _) => _vistaTocada = true;
        rejilla.MouseLeave += (_, _) => { if (_vistaTocada) CerrarVista(); };
        return rejilla;
    }

    UIElement CrearCelda(Grupo grupo, IntPtr h, bool esActiva)
    {
        var titulo = Nativo.Titulo(h);
        var icono = Nativo.Icono(h) ?? grupo.Icono;

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0) };
        if (icono != null)
            fila.Children.Add(new Image { Source = icono, Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0) });
        fila.Children.Add(new TextBlock
        {
            Text = titulo,
            Foreground = esActiva ? _acentoLegible : _texto,
            Opacity = esActiva ? 1 : 0.85,
            MaxWidth = AnchoCelda - 60,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        // La activa se resalta con un filo del color de acento. Las cuentas importan:
        // el marco pedía 38 de alto más 3+3 de margen, o sea 44, dentro de un botón
        // de 42, y WPF recortaba la diferencia por abajo. Quitarle la altura tampoco
        // vale: el borde se encoge al tamaño del texto (19 px) porque el botón centra
        // su contenido. Así que marco de 36 + 6 de margen = 42 dentro de un botón de
        // 44, con un pelín de aire y sin recortes.
        var marco = new Border
        {
            Child = fila,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(3),
            Height = AltoCelda - 10,
            BorderThickness = new Thickness(esActiva ? 1 : 0),
            BorderBrush = _acentoLegible,
            Background = esActiva
                ? new SolidColorBrush(Color.FromArgb(0x22, _cAcentoLegible.R, _cAcentoLegible.G, _cAcentoLegible.B))
                : Brushes.Transparent
        };

        var boton = Boton(marco, titulo, () => { PulsarVentana(h, grupo); CerrarVista(); });
        boton.PreviewMouseLeftButtonDown += (_, _) => ApuntarActivaAlPulsar();
        boton.Height = AltoCelda - 2;
        boton.Width = AnchoCelda - 6;
        boton.Padding = new Thickness(0);

        // Clic central: cerrar esa ventana, con su animación de salida
        boton.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            Nativo.CerrarVentana(h);
            AnimarSalida(boton, () =>
            {
                if (boton.Parent is Panel p) p.Children.Remove(boton);
            });
        };
        return boton;
    }

    void CerrarVista()
    {
        if (_panelAbierto == PanelVista) CerrarPanel();
    }
}
