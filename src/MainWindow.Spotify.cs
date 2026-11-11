using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// Lo que suena, visto desde la isla: la carátula, la canción y el artista, y un panel con el
// avance.
//
// Todo sale de la sesión de medios de Windows (`Musica.cs`), así que funciona con Spotify, con
// el navegador y con cualquier reproductor, sin cuenta, sin claves y sin tocar la red.
//
// ---------- Lo que este fichero tuvo y ya no tiene ----------
//
// Dos capas más, las dos construidas y las dos retiradas. Se cuentan aquí porque las dos se
// van a volver a pedir, y conviene que vuelvan sabiendo lo que ya se sabe:
//
// **La API de Spotify**, con inicio de sesión por OAuth, que añadía el nombre del álbum, la
// carátula a 640 px y el corazón de «Me gusta». Spotify dejó de aceptar URL de retorno por
// HTTP en loopback para aplicaciones registradas desde abril de 2025, y lo que quedaba era un
// certificado autofirmado —con su pantalla roja de aviso en el navegador, delante del
// usuario— o un esquema propio que su documentación de la Web API no contempla. Tres adornos
// no valen eso.
//
// **La letra sincronizada**, desde LRCLIB. Esa funcionaba: traía la letra y seguía el verso al
// milisegundo sin una consulta más de las que ya se hacen. Se quitó por decisión del usuario:
// «no va bien y no queda bien». Y lo segundo era verdad: el verso vivía en una línea de la
// isla que crece y se encoge con cada palabra, así que la barra respiraba al ritmo de la
// letra. Eso no se arregla afinando el ancho — es la forma equivocada de enseñar una letra, y
// la forma buena (una superficie de alto fijo, que no mueva la isla) era otro trabajo.
public partial class MainWindow
{
    internal const string PanelSpotify = "spotify";

    FrameworkElement? _anclaSpotify;

    // ¿Hay algo que enseñar ahora mismo? Las dos condiciones son del usuario: que suene algo,
    // y que sea Spotify si ha pedido que solo sea Spotify.
    bool HayCancionQueEnsenar =>
        _musica != null && (_cfg?.Musica?.SoloSpotify != true || Musica.EsSpotify(_musica.AppId));

    // ---------- El módulo: carátula, canción y artista ----------
    //
    // El título va en un `TextBlock` con tope de ancho y recorte, y NO con `Etiqueta`.
    // Etiqueta reserva el ancho de su valor más largo para que la isla no respire, y aquí eso
    // no sirve: los títulos de canción no tienen un largo máximo razonable («Everything In Its
    // Right Place»), así que reservar el del más largo que haya sonado dejaría un agujero
    // permanente en la isla. Se recorta con puntos suspensivos y se acabó.
    internal UIElement ModuloSpotify()
    {
        var fila = EnLaZona(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        }, 4, 4);

        var marco = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
            Background = _superficieAlta,
        };
        var portada = new Image { Stretch = Stretch.UniformToFill };
        marco.Child = portada;

        var titulo = new TextBlock
        {
            Foreground = _texto,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 170,
        };
        var artista = new TextBlock
        {
            Foreground = _textoAtenuado,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 130,
            Margin = new Thickness(7, 0, 0, 0),
        };

        var textos = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(titulo);
        textos.Children.Add(artista);

        fila.Children.Add(marco);
        fila.Children.Add(textos);

        var boton = Boton(fila, "Lo que suena", AbrirSpotify);
        _anclaSpotify = boton;

        _actualizadores.Add(() =>
        {
            bool hay = HayCancionQueEnsenar;
            var quiero = hay ? Visibility.Visible : Visibility.Collapsed;
            if (fila.Visibility != quiero) fila.Visibility = quiero;
            if (!hay) return;

            if (!ReferenceEquals(portada.Source, Musica.Caratula))
            {
                portada.Source = Musica.Caratula;
                if (Musica.Caratula != null) portada.BeginAnimation(OpacityProperty, Kf(0, 1, 260));
            }
            marco.Visibility = Musica.Caratula != null ? Visibility.Visible : Visibility.Collapsed;

            if (titulo.Text != _musica!.Titulo) titulo.Text = _musica.Titulo;
            var quien = _musica.Artista ?? "";
            if (artista.Text != quien) artista.Text = quien;
            // Sin artista no se deja el separador de aire colgando
            artista.Visibility = quien.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        });

        return boton;
    }

    // ================= El panel =================

    void AbrirSpotify() => AlternarPanel(PanelSpotify, CrearPanelSpotify, 340, 170,
                                         teclado: false, ancla: _anclaSpotify);

    FrameworkElement CrearPanelSpotify()
    {
        var columna = new StackPanel { Margin = new Thickness(14, 6, 14, 12) };

        if (!HayCancionQueEnsenar)
        {
            columna.Children.Add(Tenue(_cfg?.Musica?.SoloSpotify == true
                ? "No hay nada sonando en Spotify."
                : "No hay nada sonando."));
            return columna;
        }

        var m = _musica!;

        // ---------- La cabecera: carátula, canción y artista ----------
        var cabecera = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var marco = new Border
        {
            Width = 76, Height = 76,
            CornerRadius = new CornerRadius(_forma.RadioCard),
            ClipToBounds = true,
            Background = _superficieAlta,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        marco.Child = new Image { Stretch = Stretch.UniformToFill, Source = Musica.Caratula };
        cabecera.Children.Add(marco);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = m.Titulo, Foreground = _texto, FontSize = 16, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, MaxHeight = 44, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(m.Artista)) textos.Children.Add(Tenue(m.Artista));
        Grid.SetColumn(textos, 1);
        cabecera.Children.Add(textos);

        columna.Children.Add(cabecera);

        // ---------- El avance ----------
        //
        // Es una foto del momento en que se abre, como el panel de la batería: el panel se
        // monta al pulsar y no se reconstruye solo. Una barra que avanzara en vivo pediría un
        // temporizador propio, y para eso ya está la barra del propio reproductor.
        var canal = new Border
        {
            Height = 4, CornerRadius = new CornerRadius(2), Background = _superficieAlta,
            Margin = new Thickness(0, 0, 0, 4),
        };
        var lleno = new Border
        {
            Height = 4, CornerRadius = new CornerRadius(2), Background = _acento,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        canal.Child = lleno;
        canal.SizeChanged += (_, _) => lleno.Width = Math.Max(0, canal.ActualWidth * m.Avance);
        columna.Children.Add(canal);

        var tiempos = new Grid();
        tiempos.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tiempos.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ahora = Tenue(Reloj(m.Posicion));
        var total = Tenue(Reloj(m.Duracion));
        Grid.SetColumn(total, 1);
        tiempos.Children.Add(ahora);
        tiempos.Children.Add(total);
        columna.Children.Add(tiempos);

        return columna;
    }

    // «2:14», no «134 s». Es el mismo criterio que la batería: se escribe como lo diría una
    // persona. Y pasa de la hora porque hay canciones de hora y pico y un «74:09» se lee mal.
    static string Reloj(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                          : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
