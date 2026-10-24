using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NubeBar;

// Lo que lleva el menú Inicio además del buscador: cabecera con saludo, lo que
// suena, accesos rápidos, gráficas, el wallpaper, las apps fijadas y energía.
public partial class MainWindow
{
    Grafica? _graficaCpu, _graficaRam;
    Border? _tarjetaMusica;
    StackPanel? _confirmacion;

    // ================= Cabecera =================
    UIElement CrearCabecera()
    {
        var caja = new Grid { Margin = new Thickness(12, 2, 12, 10) };
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var saludo = new TextBlock
        {
            Foreground = _texto,
            FontSize = FontSize + 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        var fecha = new TextBlock
        {
            Foreground = _textoAtenuado,
            FontSize = FontSize - 1,
            Margin = new Thickness(0, 2, 0, 0)
        };
        var izquierda = new StackPanel();
        izquierda.Children.Add(saludo);
        izquierda.Children.Add(fecha);
        Grid.SetColumn(izquierda, 0);
        caja.Children.Add(izquierda);

        var reloj = new TextBlock
        {
            Foreground = _acento,
            FontSize = FontSize + 20,
            VerticalAlignment = VerticalAlignment.Center
        };
        Typography.SetNumeralAlignment(reloj, FontNumeralAlignment.Tabular);
        Grid.SetColumn(reloj, 1);
        caja.Children.Add(reloj);

        void Act()
        {
            var ahora = DateTime.Now;
            saludo.Text = $"{Saludo(ahora)}, {Environment.UserName}";
            fecha.Text = Mayuscula(ahora.ToString("dddd, d 'de' MMMM", CultureInfo.CurrentCulture));
            reloj.Text = ahora.ToString("HH:mm");
        }
        Act();
        _actualizadoresMenu.Add(Act);
        return caja;
    }

    static string Saludo(DateTime t) => t.Hour switch
    {
        >= 6 and < 13 => "Buenos días",
        >= 13 and < 21 => "Buenas tardes",
        _ => "Buenas noches"
    };

    static string Mayuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    // ================= Lo que suena =================
    UIElement CrearTarjetaMusica()
    {
        var caratula = new Border
        {
            Width = 52, Height = 52,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 12, 0),
            Background = _superficie
        };
        var img = new Image { Stretch = Stretch.UniformToFill };
        caratula.Child = img;

        var titulo = new TextBlock { Foreground = _texto, FontSize = FontSize + 1, TextTrimming = TextTrimming.CharacterEllipsis };
        var artista = new TextBlock { Foreground = _textoAtenuado, FontSize = FontSize - 1, TextTrimming = TextTrimming.CharacterEllipsis };

        var barra = new Border
        {
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = _superficieAlta,
            Margin = new Thickness(0, 7, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var avance = new Border
        {
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = _acento,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0
        };
        var pistaBarra = new Grid();
        pistaBarra.Children.Add(barra);
        pistaBarra.Children.Add(avance);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(titulo);
        textos.Children.Add(artista);
        textos.Children.Add(pistaBarra);

        var anterior = Boton(Glifo(""), "Anterior", Musica.Anterior);
        var playPausa = Boton(Glifo(""), "Pausa o reanudar", Musica.PlayPausa);
        var siguiente = Boton(Glifo(""), "Siguiente", Musica.Siguiente);
        var mandos = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Children = { anterior, playPausa, siguiente }
        };

        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(caratula, 0); Grid.SetColumn(textos, 1); Grid.SetColumn(mandos, 2);
        fila.Children.Add(caratula); fila.Children.Add(textos); fila.Children.Add(mandos);

        _tarjetaMusica = Tarjeta(fila);
        _tarjetaMusica.Visibility = Visibility.Collapsed;

        void Act()
        {
            if (_musica is not { } m)
            {
                _tarjetaMusica.Visibility = Visibility.Collapsed;
                return;
            }
            _tarjetaMusica.Visibility = Visibility.Visible;
            titulo.Text = m.Titulo;
            artista.Text = m.Artista.Length > 0 ? m.Artista : m.AppId;
            if (!ReferenceEquals(img.Source, Musica.Caratula)) img.Source = Musica.Caratula;
            if (playPausa.Content is TextBlock g) g.Text = m.Sonando ? "" : "";
            avance.Width = Math.Max(0, pistaBarra.ActualWidth * m.Avance);
        }
        Act();
        _actualizadoresMenu.Add(Act);
        return _tarjetaMusica;
    }

    // ================= Accesos rápidos =================
    UIElement CrearAccesosRapidos()
    {
        var fila = new UniformGrid { Columns = 4, Margin = new Thickness(0, 2, 0, 0) };

        fila.Children.Add(Acceso("", "Wallpaper", () => _wpPausado,
            () => { _wpPausado = !_wpPausado; WallpaperEngine.Enviar(_wpPausado ? "pause" : "play"); },
            encendidoEs: "pausado"));

        fila.Children.Add(Acceso("", "Iconos", () => _iconosOcultos,
            () => { _iconosOcultos = !_iconosOcultos; WallpaperEngine.Enviar(_iconosOcultos ? "hideIcons" : "showIcons"); },
            encendidoEs: "ocultos"));

        fila.Children.Add(Acceso("", "Silencio", () => Audio.Silenciado,
            () => Audio.Silenciar(!Audio.Silenciado),
            encendidoEs: "silenciado"));

        fila.Children.Add(Acceso("", "No molestar", () => NoMolestar.Activo,
            NoMolestar.AbrirAjustes,
            encendidoEs: "activo",
            nota: "Windows no deja cambiarlo desde fuera: se abre su ajuste"));

        return Tarjeta(fila);
    }

    // Cada acceso se pinta con el acento cuando está encendido
    UIElement Acceso(string glifo, string nombre, Func<bool> encendido, Action pulsar,
                     string encendidoEs, string? nota = null)
    {
        var icono = new TextBlock
        {
            Text = glifo,
            FontFamily = new FontFamily(FuenteIconos),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var etiqueta = new TextBlock
        {
            Text = nombre,
            FontSize = FontSize - 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var dentro = new StackPanel { Children = { icono, etiqueta } };
        var marco = new Border
        {
            Child = dentro,
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(4),
            Padding = new Thickness(0, 9, 0, 8),
            BorderThickness = new Thickness(1)
        };

        var boton = Boton(marco, nota ?? nombre, () => { pulsar(); Tick(); });
        boton.Height = double.NaN;
        boton.Padding = new Thickness(0);

        void Act()
        {
            bool on = false;
            try { on = encendido(); } catch { }
            marco.Background = on
                ? _elegido
                : _superficie;
            marco.BorderBrush = on ? _acento : Brushes.Transparent;
            icono.Foreground = on ? _acento : _texto;
            etiqueta.Foreground = on ? _acento : _texto;
            etiqueta.Opacity = on ? 1 : 0.6;
            boton.ToolTip = nota ?? $"{nombre}: {(on ? encendidoEs : "no")}";
        }
        Act();
        _actualizadoresMenu.Add(Act);
        return boton;
    }

    // ================= Gráficas =================
    UIElement CrearGraficas()
    {
        _graficaCpu = new Grafica();
        _graficaRam = new Grafica();
        _graficaCpu.Colorear(_cAcento);
        _graficaRam.Colorear(_cAcento);
        _graficaCpu.Rellenar(Historial.Cpu);
        _graficaRam.Rellenar(Historial.Ram);

        var cpu = ConGrafica("CPU", _graficaCpu, () => $"{Nativo.Cpu()}%");
        var ram = ConGrafica("RAM", _graficaRam, () => $"{Nativo.Ram()}%");

        var fila = new UniformGrid { Columns = 2 };
        fila.Children.Add(cpu);
        fila.Children.Add(ram);
        return Tarjeta(fila);
    }

    UIElement ConGrafica(string nombre, Grafica g, Func<string> valor)
    {
        var titulo = new TextBlock { Text = nombre, Foreground = _textoAtenuado, FontSize = FontSize - 2 };
        var numero = new TextBlock { Foreground = _acento, FontSize = FontSize + 2, HorizontalAlignment = HorizontalAlignment.Right };
        Typography.SetNumeralAlignment(numero, FontNumeralAlignment.Tabular);

        var arriba = new Grid();
        arriba.Children.Add(titulo);
        arriba.Children.Add(numero);

        var pila = new StackPanel { Margin = new Thickness(8, 2, 8, 2) };
        pila.Children.Add(arriba);
        pila.Children.Add(g);

        void Act()
        {
            numero.Text = valor();
            g.Rellenar(nombre == "CPU" ? Historial.Cpu : Historial.Ram);
        }
        Act();
        _actualizadoresMenu.Add(Act);
        return pila;
    }

    // ================= Wallpaper =================
    UIElement CrearTarjetaWallpaper()
    {
        var mini = new Border
        {
            Width = 72, Height = 40,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 12, 0),
            Background = _superficie
        };
        var img = new Image { Stretch = Stretch.UniformToFill };
        mini.Child = img;

        var titulo = new TextBlock { Foreground = _texto, TextTrimming = TextTrimming.CharacterEllipsis };
        var pie = new TextBlock { Text = "Wallpaper actual", Foreground = _textoAtenuado, FontSize = FontSize - 2 };
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(titulo);
        textos.Children.Add(pie);

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Children = { mini, textos } };
        var tarjeta = Tarjeta(fila);

        string? ultimaPreview = null;
        void Act()
        {
            titulo.Text = _wpTitulo ?? "ninguno";
            var prev = _wpRuta != null ? WallpaperEngine.Preview(_wpRuta) : null;
            if (prev != ultimaPreview)
            {
                ultimaPreview = prev;
                img.Source = null;
                // Decodificar el JPEG cuesta unos 30 ms: fuera del camino de la
                // animación de apertura, que si no se come un par de fotogramas
                if (prev != null && File.Exists(prev))
                {
                    var ruta = prev;
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (ultimaPreview != ruta) return;
                        try
                        {
                            var b = new BitmapImage();
                            b.BeginInit();
                            b.UriSource = new Uri(ruta);
                            b.DecodePixelWidth = 160;
                            b.CacheOption = BitmapCacheOption.OnLoad;
                            b.EndInit();
                            b.Freeze();
                            img.Source = b;
                            img.BeginAnimation(OpacityProperty, Kf(0, 1, 240));
                        }
                        catch { }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            tarjeta.Visibility = _wpTitulo != null ? Visibility.Visible : Visibility.Collapsed;
        }
        Act();
        _actualizadoresMenu.Add(Act);
        return tarjeta;
    }

    // ================= Apps fijadas =================
    UIElement CrearFijadas()
    {
        var fijadas = _cfg.Inicio.Fijadas;
        if (fijadas.Count == 0) return new Border { Visibility = Visibility.Collapsed };

        var rejilla = new UniformGrid { Columns = Math.Min(6, Math.Max(1, fijadas.Count)) };
        foreach (var f in fijadas)
        {
            var ruta = f;
            var icono = new Image { Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Center };
            Dispatcher.InvokeAsync(() => icono.Source = Iconos.De(ruta, 48),
                                   System.Windows.Threading.DispatcherPriority.Background);

            var nombre = new TextBlock
            {
                Text = Path.GetFileNameWithoutExtension(ruta),
                Foreground = _textoAtenuado,
                FontSize = FontSize - 3,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 80,
                Margin = new Thickness(0, 4, 0, 0)
            };
            var dentro = new StackPanel { Children = { icono, nombre } };
            var b = Boton(dentro, ruta, () => { Uso.Apuntar("fijada:" + ruta); Fuentes.Lanzar(ruta); CerrarMenu(); });
            b.Height = double.NaN;
            b.Padding = new Thickness(4, 8, 4, 8);
            rejilla.Children.Add(b);
        }
        return Tarjeta(rejilla);
    }

    // ================= Energía =================
    UIElement CrearEnergia()
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        fila.Children.Add(BotonEnergia("", "Bloquear", Energia.Bloquear, false));
        fila.Children.Add(BotonEnergia("", "Suspender", Energia.Suspender, false));
        fila.Children.Add(BotonEnergia("", "Reiniciar", Energia.Reiniciar, true));
        fila.Children.Add(BotonEnergia("", "Apagar", Energia.Apagar, true));

        _confirmacion = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed
        };

        var caja = new StackPanel();
        caja.Children.Add(fila);
        caja.Children.Add(_confirmacion);
        return caja;
    }

    UIElement BotonEnergia(string glifo, string nombre, Action accion, bool confirmar)
    {
        var icono = new TextBlock
        {
            Text = glifo,
            FontFamily = new FontFamily(FuenteIconos),
            FontSize = 15,
            Foreground = _texto
        };
        var b = Boton(icono, nombre, () =>
        {
            if (confirmar) PedirConfirmacion(nombre, accion);
            else { Log.Info($"energía: pulsado «{nombre}»"); accion(); CerrarMenu(); }
        });
        b.Width = 40;
        return b;
    }

    // La confirmación sale dentro del propio panel, no en un cuadro de diálogo
    void PedirConfirmacion(string que, Action accion)
    {
        if (_confirmacion == null) return;
        Log.Info($"energía: pidiendo confirmación para «{que}»");

        _confirmacion.Children.Clear();
        _confirmacion.Children.Add(new TextBlock
        {
            Text = $"¿Seguro que quieres {que.ToLowerInvariant()}?",
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var si = Boton(new TextBlock { Text = "Sí, " + que.ToLowerInvariant(), Foreground = _acento }, que, () =>
        {
            Log.Info($"energía: confirmado «{que}»");
            _confirmacion.Visibility = Visibility.Collapsed;
            accion();
            CerrarMenu();
        });
        var no = Boton(new TextBlock { Text = "Cancelar", Foreground = _texto }, "Cancelar", () =>
        {
            Log.Info($"energía: cancelado «{que}»");
            _confirmacion.Visibility = Visibility.Collapsed;
        });
        _confirmacion.Children.Add(si);
        _confirmacion.Children.Add(no);

        _confirmacion.Visibility = Visibility.Visible;
        _confirmacion.BeginAnimation(OpacityProperty, Kf(0, 1, 220));
    }

    // ================= Utilidades =================
    Border Tarjeta(UIElement dentro) => new()
    {
        Child = dentro,
        CornerRadius = new CornerRadius(12),
        Background = _superficie,
        Padding = new Thickness(10, 9, 10, 9),
        Margin = new Thickness(0, 0, 0, 8)
    };
}
