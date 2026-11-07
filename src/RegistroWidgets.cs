using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Quién hay. Sustituye al switch de dieciséis casos: para añadir un widget se
// registra, y a partir de ahí sale en la isla, en la biblioteca y en la lista de
// módulos de Ajustes sin tocar ninguno de los tres.
//
// Es estático porque la lista de widgets disponibles es una del programa, no una por
// isla: con dos monitores hay dos MainWindow y los dos ofrecen lo mismo.
public static class RegistroWidgets
{
    static readonly Dictionary<string, IWidget> _porId = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<IWidget> _orden = new();

    // En el orden en que se registraron, que es el que se enseña en la biblioteca.
    public static IReadOnlyList<IWidget> Todos => _orden;

    public static IEnumerable<string> Ids => _orden.Select(w => w.Descriptor.Id);

    // Quién sabe rellenar el registro, para que quien lo USA no tenga que saberlo.
    //
    // Hasta ahora la biblioteca llamaba a MainWindow.AsegurarWidgetsRegistrados(), y
    // eso la ataba a la isla: no se podía construir sin levantar media aplicación. Con
    // esto, MainWindow deja aquí su iniciador al arrancar y los demás solo piden
    // Asegurar(). El registro deja de depender de quién lo llena.
    static Action? _iniciador;

    public static void AlIniciar(Action iniciador) => _iniciador = iniciador;

    public static void Asegurar()
    {
        var i = _iniciador;
        if (i == null) return;
        _iniciador = null;      // una sola vez, y sin reentrar si el iniciador registra
        i();
    }

    public static void Registrar(IWidget w)
    {
        var id = w.Descriptor.Id;
        if (_porId.ContainsKey(id))
        {
            // Registrar dos veces el mismo id sería un fallo de programación nuestro,
            // o dos paquetes de widgets peleándose por un nombre. Gana el primero y se
            // apunta, que es menos sorpresa que que el segundo tape al primero.
            Log.Info($"el widget \"{id}\" ya estaba registrado: se queda el primero");
            return;
        }
        _porId[id] = w;
        _orden.Add(w);
    }

    public static IWidget? Buscar(string id) =>
        id != null && _porId.TryGetValue(id, out var w) ? w : null;

    public static bool Existe(string id) => id != null && _porId.ContainsKey(id);

    public static IEnumerable<IWidget> DeCategoria(CategoriaWidget c) =>
        _orden.Where(w => w.Descriptor.Categoria == c);

    // Los ids agrupados por categoría y en orden, para la biblioteca y para Ajustes.
    public static IEnumerable<IGrouping<CategoriaWidget, IWidget>> PorCategoria() =>
        _orden.GroupBy(w => w.Descriptor.Categoria);
}
