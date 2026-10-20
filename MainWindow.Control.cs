using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NubeBar;

// El centro de control: red, volumen y batería en la isla, y al pulsarlos un panel
// con los mandos de verdad, al estilo del centro de control del iPhone.
public partial class MainWindow : Window
{
    const string PanelControl = "control";

    // ---------- Los iconos de la isla ----------
    UIElement ModuloControl()
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var red = Glifo("");
        var vol = Glifo("");
        var bat = Glifo("");

        var bRed = Boton(red, "Red", AbrirControl);
        var bVol = Boton(vol, "Volumen", AbrirControl);
        var bBat = Boton(bat, "Batería", AbrirControl);

        fila.Children.Add(bRed);
        fila.Children.Add(bVol);
        fila.Children.Add(bBat);

        _actualizadores.Add(() =>
        {
            try
            {
                red.Text = Red.Estado() switch
                {
                    Red.Tipo.Cable => "",     // cable de red
                    Red.Tipo.Wifi => "",      // ondas
                    _ => ""                   // sin conexión
                };
                int v = Audio.Volumen();
                bool mudo = Audio.Silenciado;
                vol.Text = mudo || v == 0 ? "" : v < 33 ? "" : v < 66 ? "" : "";
                bVol.ToolTip = mudo ? "Volumen: silenciado" : $"Volumen: {v}%";

                string bateria = Nativo.Bateria();
                bool hayBateria = !string.IsNullOrWhiteSpace(bateria) && bateria != "—";
                bBat.Visibility = hayBateria ? Visibility.Visible : Visibility.Collapsed;
                bBat.ToolTip = hayBateria ? $"Batería: {bateria}" : null;
            }
            catch { }
        });

        return fila;
    }

    void AbrirControl()
    {
        bool hayBrillo = Brillo.Disponible;
        double alto = 420 + (hayBrillo ? Brillo.Pantallas().Count * 34 : 0);
        AlternarPanel(PanelControl, CrearControl, 460, alto);
    }

    // ---------- El panel ----------
    FrameworkElement CrearControl()
    {
        var columna = new StackPanel { Margin = new Thickness(14, 2, 14, 12) };

        // --- Volumen general ---
        columna.Children.Add(Titulito("Volumen"));
        var mudo = Audio.Silenciado;
        var filaVol = new Grid();
        filaVol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filaVol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var botonMudo = Boton(Glifo(mudo ? "" : ""), "Silenciar", () =>
        {
            Audio.Silenciar(!Audio.Silenciado);
            RefrescarControl();
        });
        Grid.SetColumn(botonMudo, 0);
        filaVol.Children.Add(botonMudo);

        var desVol = Deslizador(Audio.Volumen(), v => Audio.PonerVolumen((int)v));
        Grid.SetColumn(desVol, 1);
        filaVol.Children.Add(desVol);
        columna.Children.Add(filaVol);

        // --- Volumen por aplicación ---
        var sesiones = Audio.Sesiones().Where(s => s.Pid != Environment.ProcessId).ToList();
        if (sesiones.Count > 0)
        {
            columna.Children.Add(Titulito("Por aplicación"));
            var caja = new ScrollViewer
            {
                MaxHeight = 110,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var pila = new StackPanel();
            foreach (var s in sesiones)
            {
                var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var nombre = new TextBlock
                {
                    Text = s.Nombre, Foreground = _texto, VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis, Opacity = s.Silenciada ? 0.45 : 1
                };
                Grid.SetColumn(nombre, 0);
                g.Children.Add(nombre);
                var d = Deslizador(s.Volumen, v => Audio.PonerVolumenSesion(s, (int)v));
                Grid.SetColumn(d, 1);
                g.Children.Add(d);
                pila.Children.Add(g);
            }
            caja.Content = pila;
            columna.Children.Add(caja);
        }

        // --- Salida de audio ---
        var salidas = Audio.Salidas();
        if (salidas.Count > 0 && Audio.PuedeCambiarSalida)
        {
            columna.Children.Add(Titulito("Salida"));
            var combo = new ComboBox { Foreground = Brushes.Black, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var s in salidas) combo.Items.Add(s.Nombre);
            combo.SelectedIndex = salidas.FindIndex(s => s.EsLaActual);
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex < 0 || combo.SelectedIndex >= salidas.Count) return;
                var elegida = salidas[combo.SelectedIndex];
                if (elegida.EsLaActual) return;
                Audio.CambiarSalida(elegida.Id);
            };
            columna.Children.Add(combo);
        }

        // --- Brillo ---
        var pantallas = Brillo.Pantallas();
        if (pantallas.Count > 0)
        {
            columna.Children.Add(Titulito("Brillo"));
            foreach (var p in pantallas)
                columna.Children.Add(Deslizador(p.PorCiento, v => { Brillo.Poner(p, (int)v); AvisoDeBrillo((int)v); }));
        }

        // --- Red y Bluetooth ---
        columna.Children.Add(Titulito("Red"));
        var estadoRed = new TextBlock { Foreground = _texto, Opacity = 0.7 };
        estadoRed.Text = Red.Estado() switch
        {
            Red.Tipo.Cable => "Conectado por cable",
            Red.Tipo.Wifi => "Wi-Fi",
            _ => "Sin conexión"
        };
        columna.Children.Add(estadoRed);

        var listaWifi = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        columna.Children.Add(listaWifi);

        var estadoBt = new TextBlock { Foreground = _texto, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0), Text = "Bluetooth: …" };
        columna.Children.Add(estadoBt);
        var listaBt = new StackPanel();
        columna.Children.Add(listaBt);

        // Escanear wifis y preguntar al bluetooth tarda: se hace por detrás para que
        // el panel se abra al instante y se vaya rellenando
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var redes = await Task.Run(Red.Wifis);
                foreach (var w in redes.Where(w => w.Senal > 0 || w.Conectada).Take(5))
                    listaWifi.Children.Add(new TextBlock
                    {
                        Text = $"{(w.Conectada ? "● " : "   ")}{w.Nombre}   {w.Senal}%",
                        Foreground = _texto, Opacity = w.Conectada ? 1 : 0.6
                    });
                if (!redes.Any(w => w.Senal > 0 || w.Conectada))
                    listaWifi.Children.Add(new TextBlock { Text = "   (sin redes a la vista)", Foreground = _texto, Opacity = 0.45 });

                var est = await Bluetooth.Estado();
                estadoBt.Text = est switch
                {
                    Bluetooth.EstadoRadio.Encendida => "Bluetooth: encendido",
                    Bluetooth.EstadoRadio.Apagada => "Bluetooth: apagado",
                    Bluetooth.EstadoRadio.SinPermiso => "Bluetooth: sin permiso",
                    _ => "Bluetooth: no hay"
                };
                if (est is Bluetooth.EstadoRadio.Encendida or Bluetooth.EstadoRadio.Apagada)
                {
                    var interruptor = Boton(Glifo(est == Bluetooth.EstadoRadio.Encendida ? "" : ""),
                                            est == Bluetooth.EstadoRadio.Encendida ? "Apagar bluetooth" : "Encender bluetooth",
                                            async () => { await Bluetooth.Encender(est != Bluetooth.EstadoRadio.Encendida); RefrescarControl(); });
                    listaBt.Children.Add(interruptor);
                    foreach (var a in (await Bluetooth.Emparejados()).Take(5))
                        listaBt.Children.Add(new TextBlock
                        {
                            Text = $"{(a.Conectado ? "● " : "   ")}{a.Nombre}",
                            Foreground = _texto, Opacity = a.Conectado ? 1 : 0.6
                        });
                }
            }
            catch (Exception ex) { Log.Error("rellenar el centro de control", ex); }
        }, DispatcherPriority.Background);

        return columna;
    }

    void RefrescarControl()
    {
        if (_panelAbierto != PanelControl) return;
        CerrarPanel();
        Dispatcher.BeginInvoke(new Action(AbrirControl), DispatcherPriority.Background);
    }

    TextBlock Titulito(string texto) => new()
    {
        Text = texto, Foreground = _acentoLegible, FontSize = 11, Opacity = 0.9,
        Margin = new Thickness(0, 8, 0, 2)
    };

    Slider Deslizador(double valor, Action<double> alSoltar)
    {
        var s = new Slider
        {
            Minimum = 0, Maximum = 100, Value = Math.Clamp(valor, 0, 100),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = _acento,
            IsMoveToPointEnabled = true
        };
        // Se aplica al soltar y no en cada píxel: con el volumen por app, mandar
        // cincuenta cambios por segundo a Core Audio hace que crepite
        s.AddHandler(System.Windows.Input.Mouse.PreviewMouseUpEvent,
                     new System.Windows.Input.MouseButtonEventHandler((_, _) => alSoltar(s.Value)), true);
        s.KeyUp += (_, _) => alSoltar(s.Value);
        return s;
    }
}
