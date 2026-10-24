using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace NubeBar;

// La ventana del panel cuando la nube es una barra VERTICAL.
//
// ---------- Por qué hace falta una ventana ----------
//
// En B6 la tarjeta del panel era un hermano de la isla DENTRO de la misma ventana, y la
// ventana se ensanchaba de 54 a 730 px para dejarle sitio. Parecía razonable —una
// ventana menos que mantener— y se medía bien en frío, pero hacía que ver el panel
// dependiera de dos cosas frágiles:
//
//  1. **Que la ventana siguiera ancha.** Cualquiera que recoloque la ventana con el
//     grosor de la BARRA (no el del panel) la devuelve a 54 px, y entonces la tarjeta
//     no se recorta: se queda fuera de la ventana y desaparece de golpe. En modo barra
//     la ventana es además una **appbar registrada**, y el shell se cree con derecho a
//     recolocarla cuando recalcula el área de trabajo — lo que pasa justo al cambiar de
//     posición, al esconder la barra de Windows o al salir de pantalla completa.
//
//  2. **Que el orden Z de la BARRA valiera para el panel.** Son cosas distintas: la
//     barra puede estar en "ventanas-encima" o "por-debajo" a propósito, y un lanzador
//     abierto tiene que verse por encima de todo igual. Metidos en la misma ventana, no
//     hay forma de que uno esté encima y el otro no.
//
// Con una ventana propia las dos cosas desaparecen: el rectángulo se calcula en
// coordenadas de PANTALLA, se eleva mientras está abierto y al cerrarse deja el orden Z
// como estaba, sin tocar el de la barra.
//
// ---------- Y por qué no un Popup ----------
//
// Un Popup de WPF es una ventana al fin y al cabo, pero con el tamaño y la colocación
// gestionados por WPF en unidades suyas, que es justo lo que no sirve con dos monitores
// a distinto DPI (el mismo motivo por el que la isla se coloca con SetWindowPos). Y su
// orden Z lo decide él. Aquí hace falta mandar en los dos.
public sealed class VentanaSuperpuesta : Window
{
    // La tarjeta se estira para llenar la ventana: **el rectángulo en píxeles es la
    // autoridad** y el contenido se adapta a la escala que WPF le dé a esta ventana en
    // el monitor en el que esté. Al revés —fijar el tamaño del contenido en unidades de
    // WPF— es lo que se descuadra al cambiar de pantalla.
    public readonly Border Tarjeta;
    public readonly ContentPresenter Cajon;

    IntPtr _self;
    IntPtr _aDondeVolver;          // quién tenía el foco antes de que lo pidiéramos

    public IntPtr Self => _self;

    public VentanaSuperpuesta(Window dueña)
    {
        // Dueña de verdad, y no por estética: una ventana suelta más mantendría viva la
        // aplicación cuando la isla se cierre (ShutdownMode es OnLastWindowClose) y
        // NubeBar no terminaría nunca. Siendo de la isla, se cierra con ella.
        Owner = dueña;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;          // aparecer no roba el foco
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        Topmost = true;
        Focusable = false;

        // Nace fuera de la vista: entre crearla y colocarla hay un fotograma, y sin
        // esto se vería un parpadeo en la esquina de arriba a la izquierda.
        Left = -32000; Top = -32000; Width = 10; Height = 10;

        Cajon = new ContentPresenter();
        Tarjeta = new Border
        {
            Child = Cajon,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Content = Tarjeta;

        SourceInitialized += (_, _) =>
        {
            _self = new WindowInteropHelper(this).Handle;
            // Mismo trato que la isla: ni roba el foco ni sale en Alt+Tab. Cuando el
            // panel necesite escribir se le quita el NOACTIVATE un rato (ver Teclado).
            Nativo.NoActivable(_self);
        };
    }

    // ---------- Colocar, en píxeles de pantalla ----------
    public void Colocar(int x, int y, int ancho, int alto)
    {
        if (_self == IntPtr.Zero)
        {
            // Todavía no hay hwnd (no se ha mostrado): se deja en unidades de WPF y al
            // mostrarse se vuelve a colocar en píxeles.
            Left = x; Top = y; Width = ancho; Height = alto;
            return;
        }
        Nativo.Colocar(_self, x, y, ancho, alto);
        // Y soltar Width/Height como hace la isla: si WPF los gestiona, al primer
        // WM_DPICHANGED los reimpone y la ventana se encoge.
        Width = double.NaN;
        Height = double.NaN;
    }

    // Mientras está abierto, por encima de las aplicaciones normales. Topmost solo no
    // basta: si otra ventana topmost se puso después, estaría por delante.
    public void Elevar()
    {
        if (_self == IntPtr.Zero) return;
        if (!Topmost) Topmost = true;
        Nativo.SiempreEncima(_self);
    }

    // ---------- Teclado ----------
    //
    // El buscador vive DENTRO de esta ventana, así que es esta la que tiene que poder
    // recibir teclas, no la isla. Al cerrar se devuelve el foco a donde estaba.
    public void Teclado(bool activar)
    {
        if (_self == IntPtr.Zero) return;
        if (activar)
        {
            var antes = Nativo.Activa();
            if (antes != IntPtr.Zero && !Nativo.EsNuestra(antes)) _aDondeVolver = antes;
            Nativo.Activable(_self);
            bool alFrente = Nativo.ForzarPrimerPlano(_self);
            Activate();
            Log.Info($"panel superpuesto: teclado activado (primer plano={alFrente})");
        }
        else
        {
            Nativo.NoActivable(_self);
            if (_aDondeVolver != IntPtr.Zero) Nativo.Activar(_aDondeVolver);
            _aDondeVolver = IntPtr.Zero;
            Log.Info("panel superpuesto: teclado devuelto");
        }
    }

    // ---------- El rectángulo que ocupa, en pantalla ----------
    internal Nativo.RECT Rectangulo() => _self == IntPtr.Zero ? default : Nativo.Rectangulo(_self);

    public bool Contiene(int x, int y)
    {
        if (_self == IntPtr.Zero || !IsVisible) return false;
        var r = Rectangulo();
        return x >= r.Izq && x < r.Der && y >= r.Arr && y < r.Aba;
    }
}
