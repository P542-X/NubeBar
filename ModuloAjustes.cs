using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace NubeBar;

// La ficha de un módulo de ajustes: lo que hay que saber de él sin construirlo.
//
// PalabrasClave existe para el buscador de ajustes: en Plasma puedes escribir "ratón"
// y te lleva al módulo, aunque el módulo se llame "Dispositivos de entrada". Aquí
// todavía no hay buscador, pero las palabras se escriben ya, que es la parte que se
// olvida cuando se añade un módulo meses después.
public sealed record DescriptorAjustes(
    string Id,                  // "colores"
    string Nombre,              // "Colores"
    string Categoria,           // agrupación para la barra lateral que vendrá
    params string[] PalabrasClave);

public interface IModuloAjustes
{
    DescriptorAjustes Descriptor { get; }

    // Monta la página. Se llama cada vez que hay que (re)construirla, así que puede
    // leer el config en ese momento.
    UIElement Construir();
}

// Un módulo que envuelve una de las secciones que ya existían en VentanaAjustes.
public sealed class ModuloAjustesDeCasa : IModuloAjustes
{
    readonly Func<UIElement> _construir;

    public DescriptorAjustes Descriptor { get; }

    public ModuloAjustesDeCasa(DescriptorAjustes descriptor, Func<UIElement> construir)
    {
        Descriptor = descriptor;
        _construir = construir;
    }

    public UIElement Construir() => _construir();
}

// Los módulos de la ventana de ajustes.
//
// No es estático, a diferencia de RegistroWidgets: las secciones de ajustes cierran
// sobre el ConfigArbol de SU ventana, así que el registro vive con la ventana. Si
// fuera estático, abrir ajustes dos veces dejaría lambdas apuntando a un árbol viejo
// y lo que se escribiera en la segunda ventana se perdería.
public sealed class RegistroAjustes
{
    readonly List<IModuloAjustes> _orden = new();
    readonly Dictionary<string, IModuloAjustes> _porId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IModuloAjustes> Todos => _orden;

    public void Registrar(string id, string nombre, string categoria, Func<UIElement> construir,
                          params string[] palabrasClave)
        => Registrar(new ModuloAjustesDeCasa(new DescriptorAjustes(id, nombre, categoria, palabrasClave), construir));

    public void Registrar(IModuloAjustes m)
    {
        var id = m.Descriptor.Id;
        if (_porId.ContainsKey(id))
        {
            Log.Info($"el módulo de ajustes \"{id}\" ya estaba registrado: se queda el primero");
            return;
        }
        _porId[id] = m;
        _orden.Add(m);
    }

    public IModuloAjustes? Buscar(string id) =>
        id != null && _porId.TryGetValue(id, out var m) ? m : null;

    // Su sitio en la lista, o -1. Lo usa la ventana para recargar una página concreta
    // sin depender de un número escrito a mano.
    public int Indice(string id) =>
        _orden.FindIndex(m => m.Descriptor.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    // Los módulos cuyo nombre, categoría o palabras clave contienen el texto; todos
    // si el texto está vacío. Para el buscador de ajustes que vendrá en la Fase B.
    //
    // Se llama Filtrar y no Buscar porque devuelve VARIOS y no uno: el nombre tiene
    // que decir qué hace sin mirar la firma.
    public IEnumerable<IModuloAjustes> Filtrar(string texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? _orden
            : _orden.Where(m =>
                  m.Descriptor.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                  m.Descriptor.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                  m.Descriptor.PalabrasClave.Any(p => p.Contains(texto, StringComparison.OrdinalIgnoreCase)));
}
