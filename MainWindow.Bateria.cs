using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// El panel de la batería: cuánto queda, si carga, el ahorro de batería y el modo de
// energía. Antes el icono de la pila abría el centro de control (el del volumen),
// que no tiene nada de batería.
public partial class MainWindow : Window
{
    internal const string PanelBateria = "bateria";

    // Con su ancla, como audio y red. `_anclaBateria` se guardaba en `MainWindow.Control.cs`
    // desde que existe el botón… y no la pasaba nadie: el panel se dibujaba de punta a punta
    // de la isla con 340 px de contenido centrados dentro.
    //
    // No se veía porque este panel tampoco tenía superficie propia: sin filo, un contenedor
    // de ancho completo y uno que abraza el contenido se dibujan igual. Al darle la concha de
    // C5 quedó a la vista en la primera captura.
    void AbrirBateria() => AlternarPanel(PanelBateria, CrearBateria, 340, 150,
                                         teclado: false, ancla: _anclaBateria);

    FrameworkElement CrearBateria()
    {
        var e = Bateria.Leer();
        var columna = new StackPanel { Margin = new Thickness(14, 4, 14, 12) };

        // --- Nivel ---
        var cabecera = new Grid();
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cabecera.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var pila = Glifo(Bateria.Glifo(e));
        pila.FontSize = 26;
        pila.Foreground = e.Ahorro ? _roles.Pincel("bateria.ahorro") : _acentoLegible;
        pila.Margin = new Thickness(0, 0, 12, 0);
        cabecera.Children.Add(pila);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock { Text = $"{e.PorCiento}%", Foreground = _texto, FontSize = 22, FontWeight = FontWeights.SemiBold });
        textos.Children.Add(Tenue(Bateria.Texto(e)));
        Grid.SetColumn(textos, 1);
        cabecera.Children.Add(textos);
        columna.Children.Add(cabecera);

        // La barra, del color de acento (dorada con el ahorro puesto, como en Windows)
        var canal = new Border
        {
            Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 10, 0, 0),
            Background = _superficieAlta
        };
        var lleno = new Border
        {
            Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left,
            Background = e.Ahorro ? _roles.Pincel("bateria.ahorro")
                      : e.PorCiento <= 15 && !e.Enchufado ? _roles.Pincel("bateria.bajo") : _acento
        };
        canal.Child = lleno;
        // ---------- Se llena al abrir, no aparece llena ----------
        //
        // La maqueta: «barra que se llena al abrir (de 0 a su valor, 1 s)». Lo que gana no es
        // vistosidad: una barra que crece te dice CUÁNTO ha crecido, así que el 74 % se lee
        // sin tener que mirar la cifra. Apareciendo ya llena, la barra es un adorno al lado
        // del número.
        canal.SizeChanged += (_, _) =>
        {
            double destino = canal.ActualWidth * e.PorCiento / 100.0;
            if (!Animacion.Adorno) { lleno.Width = destino; return; }
            lleno.Width = 0;
            lleno.BeginAnimation(WidthProperty, Kf(0, destino, 1000, 0, Suave));
        };
        columna.Children.Add(canal);

        // --- Modo de energía ---
        if (Bateria.ModoActual() is { } modo)
        {
            columna.Children.Add(Titulito("Modo de energía"));
            var fila = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(-6, 0, -6, 0) };
            foreach (var (m, nombre, glifo) in new[]
            {
                (Bateria.Modo.Eficiencia, "Eficiencia", ""),
                (Bateria.Modo.Equilibrado, "Equilibrado", ""),
                (Bateria.Modo.Rendimiento, "Rendimiento", "")
            })
            {
                bool es = m == modo;
                var dentro = new StackPanel { Orientation = Orientation.Horizontal };
                var g = Glifo(glifo);
                g.FontSize = 12;
                g.Margin = new Thickness(0, 0, 6, 0);
                g.Foreground = es ? _acentoLegible : _texto;
                dentro.Children.Add(g);
                dentro.Children.Add(new TextBlock
                {
                    Text = nombre, Foreground = es ? _acentoLegible : _texto,
                    FontWeight = es ? FontWeights.SemiBold : FontWeights.Normal, FontSize = 12
                });
                var b = Boton(dentro, es ? $"{nombre} (puesto)" : $"Pasar a {nombre.ToLowerInvariant()}", () =>
                {
                    if (Bateria.PonerModo(m)) RefrescarBateria();
                });
                b.Height = 28;
                fila.Children.Add(b);
            }
            columna.Children.Add(fila);
        }

        // --- Ahorro y ajustes ---
        var abajo = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        abajo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        abajo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        // El ahorro de batería no tiene API pública para encenderlo: se dice cómo
        // está y se manda a su página de Configuración
        var ahorro = Boton(new TextBlock
        {
            Text = e.Ahorro ? "Ahorro de batería: activado" : "Ahorro de batería: desactivado",
            Foreground = e.Ahorro ? _roles.Pincel("bateria.ahorro") : _texto, FontSize = 11, Opacity = e.Ahorro ? 1 : 0.7
        }, "Abrir el ahorro de batería en Configuración", () => AbrirAjustes("ms-settings:batterysaver"));
        ahorro.HorizontalAlignment = HorizontalAlignment.Left;
        ahorro.Margin = new Thickness(-6, 0, 0, 0);
        ahorro.Height = 22;
        abajo.Children.Add(ahorro);
        var ajustes = Boton(new TextBlock { Text = "Energía y batería ›", Foreground = _acentoLegible, FontSize = 11 },
                            "Abrir Energía y batería en Configuración", () => AbrirAjustes("ms-settings:powersleep"));
        ajustes.Height = 22;
        Grid.SetColumn(ajustes, 1);
        abajo.Children.Add(ajustes);
        columna.Children.Add(abajo);

        // Con la concha de C5, como audio, red y el centro de control. Hasta aquí este panel
        // se apoyaba en el fondo de la isla, que es translúcido: el escritorio se
        // transparentaba detrás del porcentaje y del tiempo restante. Es el mismo
        // razonamiento que `MainWindow.Laterales.cs` lleva escrito desde E3 —«un panel de
        // controles con el escritorio transparentándose detrás hace ilegible cualquier texto
        // pequeño»— aplicado donde faltaba.
        //
        // `margen: 0` porque el contenido ya trae el suyo. La concha pondría un segundo
        // margen encima y el panel engordaría sin que nadie lo hubiera pedido.
        return PanelShell(columna, 0);
    }

    void RefrescarBateria()
    {
        if (_panelAbierto != PanelBateria) return;
        // Se sustituye el contenido sin cerrar: así no hay que replegar y desplegar
        CambiarContenidoDelPanel(CrearBateria());
        ReajustarPanel(PanelBateria);
    }

    // Cierra el panel y abre una página de Configuración (ms-settings:...)
    void AbrirAjustes(string uri)
    {
        CerrarPanel();
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error($"abrir {uri}", ex); }
    }
}
