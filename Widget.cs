using System;
using System.Windows;
using System.Windows.Controls;

namespace NubeBar;

// En qué cajón de la biblioteca sale el widget.
public enum CategoriaWidget
{
    Reloj,      // reloj, fecha, calendario
    Apps,       // carrusel, fijadas, escritorios
    Sistema,    // CPU, RAM, batería, red, bandeja, centro de control
    Medios,     // música, carátula, barras
    Aspecto,    // wallpaper, iconos del escritorio
    Util        // separador y los que no son de ningún sitio
}

// La ficha de un widget: lo que hay que saber de él SIN montarlo. La biblioteca de
// widgets, la ventana de ajustes y el log se apañan solo con esto, así que enseñar la
// lista de widgets disponibles no construye ni uno.
public sealed record DescriptorWidget(
    string Id,                      // lo que se escribe en el config: "reloj"
    string Nombre,                  // lo que lee el usuario: "Reloj"
    string Descripcion,             // una línea para la biblioteca
    string Glifo,                   // icono (Segoe Fluent Icons) para la biblioteca
    CategoriaWidget Categoria,
    string ZonaPreferida = "Derecha",   // dónde cae si lo añades sin decir dónde
    bool Repetible = false);            // ¿tiene sentido tener dos? (el separador, sí)

// Lo que un widget puede pedirle a la isla que lo aloja.
//
// Es a propósito un contrato de WPF y no una abstracción de mentira: los widgets se
// dibujan con WPF, y fingir que no se sabe habría obligado a inventar una capa de
// envoltorios que no sirve para nada. Lo que sí hace es que un widget no vea a
// MainWindow entera: pide colores, nivel de animación, un refresco por segundo y los
// cuatro ayudantes de construcción, y nada más.
public interface IAnfitrionWidget
{
    // Los colores del tema por rol, ya resueltos (con el wallpaper aplicado y el
    // acento corregido). Nunca es null.
    Roles Roles { get; }

    // Para que un widget que anima por su cuenta respete lo que ha pedido el usuario.
    NivelAnimacion Animacion { get; }

    // Alto de la fila de widgets, en píxeles de diseño.
    double Grosor { get; }

    // Un refresco que se llama cada segundo. El anfitrión lo olvida solo cuando se
    // remonta el panel, así que el widget no tiene que darse de baja.
    void CadaTick(Action refresco);

    // ---------- Ayudantes de construcción ----------
    // Son los mismos que usan los módulos de casa, para que un widget de fuera salga
    // con el mismo aire: el botón que se levanta al pasar el ratón, el glifo con la
    // fuente de iconos, y la etiqueta que reserva el ancho de su valor más largo para
    // que la isla no respire cada segundo.
    Button Boton(object contenido, string tip, Action accion);
    TextBlock Glifo(string codigo);
    TextBlock Etiqueta(Func<string> valor, bool animar = false, string? muestra = null);
    TextBlock Texto(string t, double tamano);
}

// Un widget.
public interface IWidget
{
    DescriptorWidget Descriptor { get; }

    // Monta su interfaz. Puede devolver cualquier UIElement. Si revienta, el anfitrión
    // lo apunta y deja el hueco vacío: un widget roto no tumba la barra.
    UIElement Crear(IAnfitrionWidget anfitrion);
}

// Los widgets de casa: los dieciséis módulos que ya existían. Cada uno envuelve la
// expresión que tenía en el switch de CrearModulo, sin tocarla, que es lo que
// garantiza que siguen haciendo exactamente lo mismo.
//
// Necesitan la isla entera y no solo el anfitrión (el carrusel habla con los grupos de
// ventanas, la bandeja con el servicio de iconos), así que la lambda recibe MainWindow.
// Un widget de fuera no podrá hacer eso, y es justo la diferencia que esta fase deja
// marcada en vez de esconder.
public sealed class WidgetDeCasa : IWidget
{
    readonly Func<MainWindow, UIElement> _crear;

    public DescriptorWidget Descriptor { get; }

    public WidgetDeCasa(DescriptorWidget descriptor, Func<MainWindow, UIElement> crear)
    {
        Descriptor = descriptor;
        _crear = crear;
    }

    public UIElement Crear(IAnfitrionWidget anfitrion)
    {
        if (anfitrion is MainWindow isla) return _crear(isla);

        // No debería pasar nunca: hoy el único anfitrión es la isla. Si algún día hay
        // otro (el escritorio, por ejemplo), esto dice qué widget no vale ahí todavía
        // en vez de reventar con un cast.
        Log.Info($"el widget de casa \"{Descriptor.Id}\" necesita la isla y el anfitrión " +
                 $"es {anfitrion.GetType().Name}: se deja el hueco vacío");
        return new Border { Visibility = Visibility.Collapsed };
    }
}
