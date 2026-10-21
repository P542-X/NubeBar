using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        // El idioma del teclado: solo sale si tienes más de una distribución puesta.
        // Clic para pasar a la siguiente (como Win+Espacio), rueda para ir y volver.
        var idioma = new TextBlock { Foreground = _texto, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var bIdioma = Boton(idioma, "Idioma del teclado", () => Idioma.Siguiente());
        bIdioma.MouseWheel += (_, e) => { Idioma.Siguiente(atras: e.Delta > 0); Tick(); e.Handled = true; };
        bIdioma.Visibility = Visibility.Collapsed;

        var bRed = Boton(red, "Red", AbrirRed);
        var bVol = Boton(vol, "Volumen", AbrirControl);
        var bBat = Boton(bat, "Batería", AbrirControl);

        // La rueda sobre el altavoz sube y baja el volumen de 2 en 2, sin abrir nada.
        // El aviso de la isla sale solo: VigilarVolumen ve el cambio.
        bVol.MouseWheel += (_, e) =>
        {
            int v = Audio.Volumen();
            if (v >= 0) Audio.PonerVolumen(v + (e.Delta > 0 ? 2 : -2));
            if (Audio.Silenciado && e.Delta > 0) Audio.Silenciar(false);
            e.Handled = true;
        };

        fila.Children.Add(bIdioma);
        fila.Children.Add(bRed);
        fila.Children.Add(bVol);
        fila.Children.Add(bBat);

        _actualizadores.Add(() =>
        {
            try
            {
                var distribuciones = Idioma.Lista();
                if (distribuciones.Count > 1)
                {
                    var actual = distribuciones.FirstOrDefault(d => d.Hkl == Idioma.Actual());
                    idioma.Text = actual?.Corto ?? "··";
                    bIdioma.ToolTip = $"Teclado: {actual?.Nombre ?? "?"}\nClic o rueda para cambiar";
                    bIdioma.Visibility = Visibility.Visible;
                }
                else bIdioma.Visibility = Visibility.Collapsed;

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
        // Solo para medir el alto del panel: se abren los monitores físicos y se
        // devuelven en el acto. Antes se quedaban abiertos y se fugaba un handle del
        // driver de vídeo por monitor cada vez que abrías el centro de control.
        int cuantas = 0;
        if (hayBrillo)
        {
            var p = Brillo.Pantallas();
            cuantas = p.Count;
            Brillo.Soltar(p);
        }
        // Es un mínimo: AbrirPanel mide, y lo que llega tarde (bluetooth) lo reajusta
        double alto = 300 + cuantas * 34;
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
        filaVol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filaVol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filaVol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filaVol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        var botonMudo = Boton(Glifo(mudo ? "" : ""), "Silenciar", () =>
        {
            Audio.Silenciar(!Audio.Silenciado);
            RefrescarControl();
        });
        Grid.SetColumn(botonMudo, 0);
        filaVol.Children.Add(botonMudo);

        var cifraVol = new TextBlock
        {
            Foreground = _texto, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, Text = $"{Math.Max(0, Audio.Volumen())}%"
        };
        Typography.SetNumeralAlignment(cifraVol, FontNumeralAlignment.Tabular);

        var desVol = Deslizador(Audio.Volumen(), v => Audio.PonerVolumen((int)v));
        desVol.Margin = new Thickness(4, 0, 4, 0);
        // La cifra sigue al deslizador mientras lo arrastras, aunque el volumen
        // de verdad solo se aplique al soltar (ver Deslizador)
        desVol.ValueChanged += (_, e) => cifraVol.Text = $"{(int)Math.Round(e.NewValue)}%";
        // La rueda encima del deslizador también vale, de 2 en 2
        desVol.PreviewMouseWheel += (_, e) =>
        {
            desVol.Value = Math.Clamp(desVol.Value + (e.Delta > 0 ? 2 : -2), 0, 100);
            Audio.PonerVolumen((int)desVol.Value);
            e.Handled = true;
        };
        Grid.SetColumn(desVol, 2);
        filaVol.Children.Add(desVol);

        // Bajar y subir de 5 en 5, para afinar sin tener que acertar con el ratón
        Button Paso(string glifo, string tip, int paso)
        {
            var b = Boton(Glifo(glifo), tip, () =>
            {
                int v = Math.Clamp((int)Math.Round(desVol.Value) + paso, 0, 100);
                desVol.Value = v;
                Audio.PonerVolumen(v);
                if (paso > 0 && Audio.Silenciado) Audio.Silenciar(false);
            });
            b.Height = 26;
            return b;
        }
        var menos = Paso("", "Bajar el volumen", -5);
        Grid.SetColumn(menos, 1);
        filaVol.Children.Add(menos);
        var mas = Paso("", "Subir el volumen", 5);
        Grid.SetColumn(mas, 3);
        filaVol.Children.Add(mas);

        Grid.SetColumn(cifraVol, 4);
        filaVol.Children.Add(cifraVol);
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

        // --- Teclado ---
        var distribuciones = Idioma.Lista();
        if (distribuciones.Count > 1)
        {
            columna.Children.Add(Titulito("Teclado"));
            var actual = Idioma.Actual();
            var filaTeclado = new WrapPanel();
            foreach (var d in distribuciones)
            {
                bool es = d.Hkl == actual;
                var b = Boton(new TextBlock
                {
                    Text = d.Corto, Foreground = es ? _acentoLegible : _texto,
                    FontWeight = es ? FontWeights.Bold : FontWeights.Normal
                }, d.Nombre, () => { Idioma.Poner(d); Dispatcher.BeginInvoke(new Action(RefrescarControl), DispatcherPriority.Background); });
                b.Height = 26;
                b.Margin = new Thickness(-4, 0, 6, 0);
                filaTeclado.Children.Add(b);
            }
            columna.Children.Add(filaTeclado);
        }

        // --- Red y Bluetooth ---
        // Lo de la red, entero (wifis, conectar, test de velocidad), vive en su
        // propio panel; aquí solo el resumen y la puerta para ir
        columna.Children.Add(Titulito("Red"));
        var estadoRed = new TextBlock { Foreground = _texto, VerticalAlignment = VerticalAlignment.Center };
        estadoRed.Text = Red.Estado() switch
        {
            Red.Tipo.Cable => "Conectado por cable",
            Red.Tipo.Wifi => "Wi-Fi",
            _ => "Sin conexión"
        };
        var irRed = Boton(new TextBlock { Text = "Redes y velocidad ›", Foreground = _acentoLegible, FontSize = 11 },
                          "Ver las redes wifi, conectarse y medir la velocidad", AbrirRed);
        irRed.Height = 24;
        var filaRed = new Grid();
        filaRed.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filaRed.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filaRed.Children.Add(estadoRed);
        Grid.SetColumn(irRed, 1);
        filaRed.Children.Add(irRed);
        columna.Children.Add(filaRed);

        var estadoBt = new TextBlock { Foreground = _texto, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0), Text = "Bluetooth: …" };
        columna.Children.Add(estadoBt);
        var listaBt = new StackPanel();
        columna.Children.Add(listaBt);

        // Preguntar al bluetooth tarda: se hace por detrás para que el panel se abra
        // al instante y se vaya rellenando
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
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
            ReajustarPanel(PanelControl);
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
