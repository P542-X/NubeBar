using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Las operaciones sobre las zonas de un panel: qué hay puesto, dónde, y cómo
// añadir, ocultar, mostrar, quitar y mover.
//
// Vive aparte porque tiene DOS clientes que deben comportarse igual: la Biblioteca
// (que trabaja por id, porque enseña un catálogo) y el modo edición de la isla (que
// trabaja por posición, porque tienes el bloque delante y puede haber dos iguales).
// Si cada uno llevara su copia de las reglas, el día que cambie una se cambiaría en
// un sitio y no en el otro.
//
// Es lógica pura sobre el árbol del config: ni una línea de interfaz. Por eso se
// puede comprobar entera sin abrir una ventana.
public sealed class ZonasWidgets
{
    public static readonly string[] Nombres = { "Izquierda", "Centro", "Derecha" };

    readonly ConfigArbol _arbol;

    public ZonasWidgets(ConfigArbol arbol) => _arbol = arbol;

    static bool Igual(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
    static bool EsZona(string z) => Nombres.Any(n => Igual(n, z));

    // ---------- Lectura ----------
    public List<string> De(string zona) => _arbol.Lista("Modulos", zona);
    public List<string> Ocultos() => _arbol.Lista("Modulos", "Ocultos");

    public bool EstaOculto(string id) => Ocultos().Any(o => Igual(o, id));

    public string? DondeEsta(string id)
    {
        foreach (var z in Nombres) if (De(z).Any(m => Igual(m, id))) return z;
        return null;
    }

    public int CuantosHay(string id) => Nombres.Sum(z => De(z).Count(m => Igual(m, id)));

    // ---------- Añadir ----------
    // Devuelve false si el widget no es repetible y ya estaba: la regla del contrato
    // vive aquí y no en el botón, para que valga igual desde la biblioteca y desde la
    // isla.
    // "aZona" la pone quien sabe dónde quiere el widget: el "+" de una zona concreta
    // en el modo edición. Desde la biblioteca de Ajustes no hay zona en la mano, así
    // que se usa la preferida del descriptor.
    public bool Anadir(DescriptorWidget d, string? aZona = null)
    {
        if (!d.Repetible && CuantosHay(d.Id) > 0)
        {
            Log.Info($"el widget \"{d.Id}\" no admite más de uno: no se añade otro");
            return false;
        }
        var zona = aZona != null && EsZona(aZona) ? aZona
                 : EsZona(d.ZonaPreferida) ? d.ZonaPreferida : "Derecha";
        var lista = De(zona);
        lista.Add(d.Id);
        _arbol.PonerLista("Modulos", zona, lista);
        Mostrar(d.Id);                       // si estaba oculto, lo lógico es verlo
        Log.Info($"widget \"{d.Id}\" añadido a {zona}");
        return true;
    }

    // ---------- Quitar ----------
    // Por id: quita la ÚLTIMA instancia, que es la que uno espera deshacer.
    public bool Quitar(string id)
    {
        foreach (var z in Nombres)
        {
            var lista = De(z);
            int i = lista.FindLastIndex(m => Igual(m, id));
            if (i < 0) continue;
            return Quitar(z, i);
        }
        return false;
    }

    // Por posición: la instancia exacta que tienes delante en el modo edición.
    public bool Quitar(string zona, int indice)
    {
        if (!EsZona(zona)) return false;
        var lista = De(zona);
        if (indice < 0 || indice >= lista.Count) return false;
        var id = lista[indice];
        lista.RemoveAt(indice);
        _arbol.PonerLista("Modulos", zona, lista);
        // Si era la última, que deje de figurar como oculto: si no, al volver a
        // añadirlo aparecería invisible y nadie entendería por qué.
        if (CuantosHay(id) == 0) Mostrar(id);
        Log.Info($"widget \"{id}\" quitado de {zona}");
        return true;
    }

    // ---------- Ocultar y mostrar ----------
    // Ocultar NO toca la zona: el id se queda donde está, con su sitio. Por eso al
    // mostrarlo reaparece en su posición y no al final.
    public void Ocultar(string id)
    {
        var o = Ocultos();
        if (o.Any(x => Igual(x, id))) return;
        o.Add(id);
        _arbol.PonerLista("Modulos", "Ocultos", o);
        Log.Info($"widget \"{id}\" oculto (conserva su sitio)");
    }

    public void Mostrar(string id)
    {
        var o = Ocultos();
        if (o.RemoveAll(x => Igual(x, id)) == 0) return;
        _arbol.PonerLista("Modulos", "Ocultos", o);
        Log.Info($"widget \"{id}\" vuelve a verse");
    }

    public void Alternar(string id)
    {
        if (EstaOculto(id)) Mostrar(id); else Ocultar(id);
    }

    // ---------- Mover ----------
    // De una posición a otra, dentro de la misma zona o entre zonas. El índice de
    // destino se interpreta sobre la lista YA SIN el elemento movido, que es lo que
    // hace que arrastrar "una a la derecha" funcione como uno espera.
    //
    // Devuelve false sin tocar nada si algo no cuadra: el modo edición cancela un
    // arrastre soltándolo fuera, y entonces nadie debe escribir en el config.
    public bool Mover(string zonaOrigen, int indice, string zonaDestino, int destino)
    {
        if (!EsZona(zonaOrigen) || !EsZona(zonaDestino)) return false;

        var origen = De(zonaOrigen);
        if (indice < 0 || indice >= origen.Count) return false;
        var id = origen[indice];

        if (Igual(zonaOrigen, zonaDestino))
        {
            origen.RemoveAt(indice);
            destino = Math.Clamp(destino, 0, origen.Count);
            if (destino == indice) { return false; }      // no se ha movido: no se escribe
            origen.Insert(destino, id);
            _arbol.PonerLista("Modulos", zonaOrigen, origen);
        }
        else
        {
            origen.RemoveAt(indice);
            var llegada = De(zonaDestino);
            destino = Math.Clamp(destino, 0, llegada.Count);
            llegada.Insert(destino, id);
            _arbol.PonerLista("Modulos", zonaOrigen, origen);
            _arbol.PonerLista("Modulos", zonaDestino, llegada);
        }
        Log.Info($"widget \"{id}\" movido de {zonaOrigen}[{indice}] a {zonaDestino}[{destino}]");
        return true;
    }

    // ---------- Qué se dibuja en una zona ----------
    //
    // La regla, en un solo sitio y sin interfaz de por medio, para poder comprobarla:
    //
    //   · en modo normal se dibuja todo menos los ocultos;
    //   · editando se dibuja TODO, y los ocultos salen atenuados — si desaparecieran
    //     no habría forma de volver a mostrarlos desde la propia isla;
    //   · entrar o salir de edición NO cambia el orden ni el contenido de la zona.
    //
    // De aquí sale la garantía que se pide: un widget del Centro que no esté oculto se
    // dibuja siempre, en los dos modos.
    // Estática y sin dependencias: la usan tanto quien tiene el árbol del config
    // (la biblioteca, las pruebas) como quien tiene el panel ya cargado (la isla).
    // Una sola regla, un solo sitio.
    public static List<(string Id, bool Oculto)> ADibujar(
        IEnumerable<string> enLaZona, IEnumerable<string> ocultos, bool editando)
    {
        var escondidos = new HashSet<string>(ocultos, StringComparer.OrdinalIgnoreCase);
        var res = new List<(string, bool)>();
        foreach (var id in enLaZona)
        {
            bool oculto = escondidos.Contains(id);
            if (oculto && !editando) continue;
            res.Add((id, oculto));
        }
        return res;
    }

    public List<(string Id, bool Oculto)> ADibujar(string zona, bool editando) =>
        ADibujar(De(zona), Ocultos(), editando);

    // Los ids puestos que el registro no conoce. Pasa con un config escrito por una
    // NubeBar más nueva; la biblioteca ofrece limpiarlos.
    public IEnumerable<string> Desconocidos() =>
        Nombres.SelectMany(De)
               .Where(id => !RegistroWidgets.Existe(id))
               .Distinct(StringComparer.OrdinalIgnoreCase);
}
