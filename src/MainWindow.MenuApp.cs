using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NubeBar;

// El menú del clic derecho sobre un resultado de la búsqueda.
//
// Hasta ahora el clic derecho en una fila subía hasta la isla y abría SU menú —el de
// cerrar aplicaciones, editar la nube y salir de NubeBar—, que no tiene nada que ver
// con la aplicación que tenías debajo del puntero. Ahora cada resultado saca el suyo:
// abrir, abrir como administrador, enseñar dónde vive y sus propiedades.
//
// Dos decisiones que explican cómo está hecho:
//
//  1. **No es un ContextMenu de Windows.** Sería una cajita blanca del sistema que no
//     pega con nada y, en una ventana WS_EX_NOACTIVATE, con el foco siempre a medias.
//     Es una tarjeta más, dibujada DENTRO del propio panel del menú Inicio.
//
//  2. **No es un panel de la isla.** Los paneles son de uno en uno: abrir uno cerraría
//     la búsqueda, que es justo lo que estabas usando. Así que es una capa por encima
//     de la lista, con su propio fondo que se come los clics de fuera.
public partial class MainWindow
{
    Grid? _raizMenu;            // la raíz del panel del menú Inicio, para medir y colocar
    Grid? _capaMenuApp;         // la capa que tapa el panel cuando hay menú
    Canvas? _lienzoMenuApp;     // dentro de ella, donde se coloca la tarjeta

    public bool MenuDeAppAbierto => _capaMenuApp?.Visibility == Visibility.Visible;

    // La capa se monta una vez con el panel y vive colapsada hasta que hace falta.
    UIElement CrearCapaMenuApp()
    {
        _lienzoMenuApp = new Canvas();
        _capaMenuApp = new Grid
        {
            Visibility = Visibility.Collapsed,
            // Transparente pero NO invisible al ratón: es lo que recoge el clic de
            // fuera. Un Background nulo dejaría pasar los clics a la lista de debajo y
            // el menú no se cerraría al hacer clic al lado.
            Background = Brushes.Transparent,
            Children = { _lienzoMenuApp }
        };
        _capaMenuApp.MouseLeftButtonDown += (_, e) => { e.Handled = true; CerrarMenuDeApp(); };
        _capaMenuApp.MouseRightButtonUp += (_, e) => { e.Handled = true; CerrarMenuDeApp(); };
        return _capaMenuApp;
    }

    void CerrarMenuDeApp()
    {
        if (_capaMenuApp == null) return;
        _capaMenuApp.Visibility = Visibility.Collapsed;
        _lienzoMenuApp?.Children.Clear();
    }

    // Al cerrarse el buscador, su panel entero se desmonta: estas tres referencias
    // apuntarían a elementos que ya no están en ningún árbol, y MenuDeAppAbierto
    // seguiría diciendo "sí" si el menú estaba abierto al cerrar.
    void OlvidarMenuDeApp()
    {
        CerrarMenuDeApp();
        _capaMenuApp = null;
        _lienzoMenuApp = null;
        _raizMenu = null;
    }

    // ---------- Abrir ----------
    void AbrirMenuDeApp(Resultado r, Point enRaiz)
    {
        if (_capaMenuApp == null || _lienzoMenuApp == null || _raizMenu == null) return;

        _lienzoMenuApp.Children.Clear();
        var tarjeta = TarjetaMenuDeApp(r);
        _lienzoMenuApp.Children.Add(tarjeta);
        _capaMenuApp.Visibility = Visibility.Visible;

        // Medir para poder encajarla: si se coloca a pelo donde has pulsado, un clic
        // derecho en la última fila deja medio menú fuera del panel y el recorte del
        // cajón se come las opciones de abajo.
        tarjeta.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var pide = tarjeta.DesiredSize;
        double x = Math.Clamp(enRaiz.X, 0, Math.Max(0, _raizMenu.ActualWidth - pide.Width));
        double y = Math.Clamp(enRaiz.Y, 0, Math.Max(0, _raizMenu.ActualHeight - pide.Height));
        Canvas.SetLeft(tarjeta, x);
        Canvas.SetTop(tarjeta, y);

        // Entra como todo lo demás: el valor real es el final y la animación solo
        // dibuja el camino (ver la regla de la casa en MainWindow.Animaciones.cs).
        var mover = new TranslateTransform { Y = -6 };
        tarjeta.RenderTransform = mover;
        tarjeta.BeginAnimation(OpacityProperty, Kf(0, 1, 140));
        mover.BeginAnimation(TranslateTransform.YProperty, Kf(-6, 0, 220, 0, Muelle));
        mover.Y = 0;

        Log.Info($"menú de aplicación: \"{r.Titulo}\" ({r.Etiqueta}) ruta={r.IconoRuta ?? "(ninguna)"}");
    }

    // ---------- La tarjeta ----------
    Border TarjetaMenuDeApp(Resultado r)
    {
        string? ruta = r.IconoRuta;

        var pila = new StackPanel { Margin = new Thickness(0, 5, 0, 7) };

        // El título, para que no haya duda de sobre qué app es este menú: con seis
        // filas parecidas, un menú sin nombre se abre sobre la que no querías.
        pila.Children.Add(new TextBlock
        {
            Text = r.Titulo,
            Foreground = _texto,
            Opacity = 0.55,
            FontSize = FontSize - 2,
            Margin = new Thickness(12, 2, 12, 6),
            MaxWidth = 260,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        // Las opciones y POR QUÉ cada una está o no disponible. Lo que no se puede
        // hacer sale apagado en vez de desaparecer: así el menú no cambia de forma
        // según la app y se ve que la opción existe pero esta no la admite.
        // Las tres acciones de verdad cierran también el buscador, como hace el menú
        // Inicio de Windows: has decidido qué hacer y ya no estás buscando.
        void Actuar(Action a) { CerrarMenu(); Uso.Apuntar(r.Clave); a(); }

        var opciones = new (string Texto, string? Pega, Action? Accion)[]
        {
            ("Abrir", null, () => Ejecutar(r, false)),

            ("Ejecutar como administrador",
             AccionesApp.EsDeLaStore(ruta) ? "las aplicaciones de la Store no se elevan"
             : !AccionesApp.EsFichero(ruta) ? "no se encuentra el fichero"
             : !AccionesApp.PuedeElevar(ruta) ? "no es un ejecutable"
             : null,
             AccionesApp.PuedeElevar(ruta) ? () => Actuar(() => AccionesApp.ComoAdministrador(ruta!)) : null),

            ("Abrir ubicación del archivo",
             AccionesApp.TieneUbicacion(ruta) ? null
             : AccionesApp.EsDeLaStore(ruta) ? "no tiene carpeta" : "no se encuentra el fichero",
             AccionesApp.TieneUbicacion(ruta) ? () => Actuar(() => AccionesApp.AbrirUbicacion(ruta!)) : null),

            ("Propiedades",
             AccionesApp.TienePropiedades(ruta) ? null : "el origen no lo permite",
             AccionesApp.TienePropiedades(ruta) ? () => Actuar(() => AccionesApp.Propiedades(ruta!)) : null),
        };

        double anchoTexto = 0;
        foreach (var o in opciones) anchoTexto = Math.Max(anchoTexto, AnchoDeTexto(o.Texto));
        anchoTexto = Math.Max(anchoTexto, AnchoDeTexto("Cancelar"));

        foreach (var (texto, pega, accion) in opciones)
            pila.Children.Add(FilaMenuDeApp(texto, pega, accion, anchoTexto, r));

        pila.Children.Add(new Border
        {
            Height = 1,
            Background = _borde,
            Margin = new Thickness(10, 5, 10, 5),
            SnapsToDevicePixels = true
        });
        // Siempre hay una salida que no hace nada, y se puede pulsar: cerrar un menú
        // con Esc o con un clic fuera es de quien ya sabe que puede.
        pila.Children.Add(FilaMenuDeApp("Cancelar", null, () => { }, anchoTexto, r));

        // Fondo propio: la tarjeta tapa lo que hay debajo. El pincel es el de la isla,
        // que ya va encima del fondo de la isla, así que las dos capas juntas son
        // opacas de hecho — y al cambiar de tema se tiñe sola, como todo lo demás.
        return new Border
        {
            Child = pila,
            Background = _fondo,
            BorderBrush = _borde,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            MinWidth = 210,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    // Una opción. Con acción es un botón normal; sin acción es un rótulo apagado con
    // el motivo en el tooltip, porque "está en gris" sin decir por qué solo frustra.
    UIElement FilaMenuDeApp(string texto, string? pega, Action? accion, double anchoTexto, Resultado r)
    {
        var etiqueta = new TextBlock
        {
            Text = texto,
            Foreground = _texto,
            Width = anchoTexto,
            TextAlignment = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (accion == null)
        {
            etiqueta.Opacity = 0.32;
            return new Border
            {
                Child = etiqueta,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Background = Brushes.Transparent,
                ToolTip = pega is { } p ? $"{texto}: {p}" : null
            };
        }

        var b = Boton(etiqueta, null!, () =>
        {
            CerrarMenuDeApp();
            try { accion(); }
            catch (Exception ex) { Log.Error($"opción \"{texto}\" de {r.Titulo}", ex); }
        });
        b.ToolTip = null;
        b.Height = 32;
        b.HorizontalContentAlignment = HorizontalAlignment.Left;
        b.Padding = new Thickness(12, 0, 12, 0);
        return b;
    }

    // ---------- Quién tiene menú ----------
    //
    // Solo lo que es una aplicación o un fichero. Un cálculo, una búsqueda en la web o
    // un ajuste de Windows no tienen "abrir ubicación" ni propiedades que enseñar, y
    // sacarles un menú de cuatro opciones apagadas no ayuda a nadie.
    //
    // Las ventanas ya abiertas tampoco: esas tienen el suyo en su icono del carrusel
    // (MenuDeGrupo), con cerrar, forzar el cierre y fijar, que es lo que se espera de
    // algo que ya está en marcha.
    internal static bool TieneMenuDeApp(Resultado r) =>
        r.Categoria is Categoria.App or Categoria.Archivo;
}
