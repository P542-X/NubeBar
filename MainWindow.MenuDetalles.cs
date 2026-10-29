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

// Lo que lleva la superficie de Inicio además del buscador y de las tarjetas del centro
// de control: la cabecera con el saludo, las apps fijadas y los botones de energía.
//
// Lo que suena, los accesos rápidos, las gráficas de CPU/RAM y el wallpaper vivían aquí
// con tarjetas hechas a mano; ahora son las tarjetas aprobadas de MainWindow.Control.cs
// (CardMultimedia, CardAccionesRapidas, CardMetricas, CardFondo), que es lo que hace que
// haya UNA superficie y no dos con lo mismo.

public partial class MainWindow
{

    StackPanel? _confirmacion;
    StackPanel? _filaEnergia;

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
        // En tarjeta, como el resto de la portada: suelto, con todo lo de al lado dentro
        // de una tarjeta opaca, se veía como un bloque de botones flotando.
        return Tarjeta(caja);
    }

    static string Saludo(DateTime t) => t.Hour switch
    {
        >= 6 and < 13 => "Buenos días",
        >= 13 and < 21 => "Buenas tardes",
        _ => "Buenas noches"
    };

    static string Mayuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

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
        _filaEnergia = fila;

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
        // En tarjeta, como el resto de la portada: suelto, con todo lo de al lado dentro
        // de una tarjeta opaca, se veía como un bloque de botones flotando.
        return Tarjeta(caja);
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

    // La confirmación sale dentro del propio panel, no en un cuadro de diálogo.
    // Ocupa el sitio de la fila de botones en vez de ir debajo: la energía es lo
    // último de la portada y una fila más se quedaba cortada por el borde del panel
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
            OcultarConfirmacion();
            accion();
            CerrarMenu();
        });
        var no = Boton(new TextBlock { Text = "Cancelar", Foreground = _texto }, "Cancelar", () =>
        {
            Log.Info($"energía: cancelado «{que}»");
            OcultarConfirmacion();
        });
        _confirmacion.Children.Add(si);
        _confirmacion.Children.Add(no);

        if (_filaEnergia != null) _filaEnergia.Visibility = Visibility.Collapsed;
        _confirmacion.Visibility = Visibility.Visible;
        _confirmacion.BeginAnimation(OpacityProperty, Kf(0, 1, 220));
        _confirmacion.BringIntoView();
    }

    void OcultarConfirmacion()
    {
        if (_confirmacion != null) _confirmacion.Visibility = Visibility.Collapsed;
        if (_filaEnergia != null) _filaEnergia.Visibility = Visibility.Visible;
    }

    // ================= Utilidades =================
    // La tarjeta de la portada. Antes se pintaba a mano con `_superficie`, que lleva
    // alfa: apoyada sobre una isla translúcida, el escritorio se leía a través de dos
    // capas transparentes seguidas. Ahora es la MISMA tarjeta del resto de los paneles
    // —`Card`, opaca y con su filo— para que no haya dos definiciones de lo mismo que se
    // separen con el tiempo, y para que las piezas propias del lanzador (las fijadas, la
    // energía) se vean igual que las del centro de control, que están al lado. El
    // acolchado se mantiene en 10 para no cambiar el aire que ya tenía la portada.
    Border Tarjeta(UIElement dentro) => Card(dentro, dentro: 10);
}
