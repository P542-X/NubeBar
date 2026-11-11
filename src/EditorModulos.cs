using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Lo que el editor de módulos sabe, sin saber dibujarlo.
//
// ---------- Qué había antes ----------
//
// La página «Módulos» de Ajustes eran tres `ListBox` con los IDS en crudo —"fijadas",
// "musica-caratula"— y debajo una fila de botones de texto. De ahí salían las cosas que se
// pidieron arreglar, y cada una tiene su causa concreta:
//
//  · **No se sabe qué está puesto ni dónde.** El catálogo de abajo enseñaba los dieciocho
//    ids siempre iguales, estuvieran puestos o no. Para saber si «escritorios» ya estaba
//    había que leer las tres listas.
//  · **No se sabe qué se puede repetir.** Pulsar dos veces en un módulo no repetible
//    parecía funcionar —el botón respondía— y luego no pasaba nada.
//  · **El botón mentía.** El rótulo decía «doble clic para añadir a la izquierda» y el
//    manejador era un `Click` sencillo. Y añadía SIEMPRE a la izquierda, dijeras lo que
//    dijeras.
//  · **Mover era arrastrar o nada.** Sin alternativa de teclado ni de botón, y arrastrar
//    dentro de un `ListBox` dentro de una ventana de ajustes falla justo en las pantallas
//    pequeñas, que es donde más falta hace.
//  · **Soltar fuera perdía el módulo.** El `Drop` lo quitaba del origen antes de saber si
//    había destino.
//
// ---------- Qué hay aquí ----------
//
// El estado de cada módulo (puesto, dónde, oculto, repetible, si puede no verse), qué
// acciones admite y qué pasa al soltarlo en un sitio o fuera de todos. Sin una línea de
// WPF, para poder comprobarlo entero.
//
// Las REGLAS no se reimplementan: añadir, quitar, mover y ocultar siguen siendo de
// `ZonasWidgets`, que es lo que usa también el modo edición de la isla. Esto es la capa de
// arriba: qué se le enseña al usuario y qué se le deja hacer.
internal static class EditorModulos
{
    // ---------- El estado de un módulo, para enseñarlo ----------
    public readonly record struct Ficha(
        string Id,
        string Nombre,
        string Glifo,
        CategoriaWidget Categoria,
        bool Puesto,
        string Zona,            // "" si no está puesto
        int Posicion,           // -1 si no está puesto
        bool Oculto,
        bool Repetible,
        int Cuantos,            // cuántas veces está puesto
        string? PuedeNoVerse);

    // Una frase corta con el estado, para la chapita del chip. Nunca vacía: un chip sin
    // estado obliga a deducirlo de la posición, que es justo lo que no se entendía.
    // Con un módulo repetible puesto varias veces se dice CUÁL de ellos es. «3 veces» no
    // sirve cuando tienes tres chips iguales delante y quieres quitar el del medio.
    public static string Estado(Ficha f) =>
        !f.Puesto ? "Sin poner"
      : f.Oculto ? $"Oculto · {f.Zona}"
      : f.Cuantos > 1 ? $"{f.Zona} · {f.Posicion + 1}.º de {f.Cuantos}"
      : f.Zona;

    // Y la explicación larga, para la ayuda emergente.
    public static string Explicar(Ficha f)
    {
        var partes = new List<string>();
        partes.Add(f.Puesto
            ? $"Puesto en {f.Zona}, en la posición {f.Posicion + 1}."
            : "No está puesto en ninguna zona.");
        if (f.Oculto) partes.Add("Está oculto: conserva su sitio, pero no se dibuja.");
        if (!f.Repetible) partes.Add("Solo puede haber uno.");
        else partes.Add("Se puede poner más de una vez.");
        if (f.PuedeNoVerse != null) partes.Add("Aviso: " + f.PuedeNoVerse + ".");
        return string.Join(" ", partes);
    }

    // ---------- Lo que se puede hacer con un chip ----------
    //
    // Existe para que haya una alternativa de BOTÓN a cada arrastre. Arrastrar sigue
    // estando, pero no puede ser la única forma: no funciona con el teclado, falla en
    // ventanas pequeñas y con mucho DPI, y en una barra que es el shell del escritorio un
    // arrastre fallido arrastra la propia barra.
    public enum Accion { Anadir, Quitar, ALaIzquierda, ALaDerecha, Subir, Bajar, Ocultar, Mostrar }

    public static string Nombre(Accion a) => a switch
    {
        Accion.Anadir => "Añadir",
        Accion.Quitar => "Quitar",
        Accion.ALaIzquierda => "Mover a la zona anterior",
        Accion.ALaDerecha => "Mover a la zona siguiente",
        Accion.Subir => "Subir en la zona",
        Accion.Bajar => "Bajar en la zona",
        Accion.Ocultar => "Ocultar",
        _ => "Mostrar",
    };

    public static string Glifo(Accion a) => a switch
    {
        Accion.Anadir => "",          // Add
        Accion.Quitar => "",          // Cancel
        Accion.ALaIzquierda => "",    // ChevronLeft
        Accion.ALaDerecha => "",      // ChevronRight
        Accion.Subir => "",           // ChevronUp
        Accion.Bajar => "",           // ChevronDown
        Accion.Ocultar => "",         // Hide
        _ => "",                      // View
    };

    // null = se puede. Si no, el motivo — y se enseña, en vez de esconder el botón. Es la
    // misma regla que en el panel de red y por el mismo motivo: un botón que desaparece no
    // dice si la acción no existe, está en otro sitio o el programa está roto.
    public static string? PorQueNo(Accion a, Ficha f)
    {
        int zona = Array.IndexOf(ZonasWidgets.Nombres, f.Zona);
        return a switch
        {
            Accion.Anadir => f.Puesto && !f.Repetible
                           ? "Este módulo solo puede estar una vez, y ya está puesto" : null,
            Accion.Quitar => f.Puesto ? null : "No está puesto",
            Accion.Ocultar => !f.Puesto ? "No está puesto"
                            : f.Oculto ? "Ya está oculto" : null,
            Accion.Mostrar => !f.Puesto ? "No está puesto"
                            : !f.Oculto ? "Ya se ve" : null,
            Accion.ALaIzquierda => !f.Puesto ? "No está puesto"
                                 : zona <= 0 ? "Ya está en la primera zona" : null,
            Accion.ALaDerecha => !f.Puesto ? "No está puesto"
                               : zona < 0 || zona >= ZonasWidgets.Nombres.Length - 1
                                 ? "Ya está en la última zona" : null,
            Accion.Subir => !f.Puesto ? "No está puesto"
                          : f.Posicion <= 0 ? "Ya es el primero de su zona" : null,
            _ => !f.Puesto ? "No está puesto" : null,   // Bajar: el tope lo sabe quien tiene la lista
        };
    }

    public static bool SePuede(Accion a, Ficha f) => PorQueNo(a, f) == null;

    // La zona de al lado, en el orden en que se dibujan. Devuelve null si no hay.
    public static string? ZonaVecina(string zona, bool haciaLaDerecha)
    {
        int i = Array.IndexOf(ZonasWidgets.Nombres, zona);
        if (i < 0) return null;
        int j = i + (haciaLaDerecha ? 1 : -1);
        return j >= 0 && j < ZonasWidgets.Nombres.Length ? ZonasWidgets.Nombres[j] : null;
    }

    // ---------- Dónde cae lo que estás arrastrando ----------
    //
    // La vista previa del destino: entre qué dos chips se va a meter. Es un índice de
    // INSERCIÓN, de 0 a cuantos (incluido), que es lo que hace que soltar al final del todo
    // funcione igual que soltar en medio.
    //
    // `altoChip` incluye su separación; `y` es la posición del ratón dentro de la columna.
    public static int DondeCae(double y, double altoChip, int cuantos)
    {
        if (altoChip <= 0 || cuantos <= 0) return 0;
        int i = (int)Math.Floor((y + altoChip / 2) / altoChip);
        return Math.Clamp(i, 0, cuantos);
    }

    // Soltar FUERA de toda zona. La primera versión quitaba del origen y después buscaba
    // destino, así que un arrastre que se pasaba de largo se comía el módulo. Esta función
    // existe para que la respuesta esté escrita y comprobada: soltar fuera **cancela**.
    public static bool SoltarFueraCancela => true;

    // ---------- Y el resultado de un arrastre, decidido antes de tocar nada ----------
    public readonly record struct Suelta(bool Vale, string Zona, int Donde, string Porque);

    public static Suelta AlSoltar(string? zonaDestino, double y, double altoChip, int cuantosEnDestino)
    {
        if (zonaDestino == null || !ZonasWidgets.Nombres.Contains(zonaDestino))
            return new Suelta(false, "", -1, "Soltado fuera de una zona: no se mueve nada");

        return new Suelta(true, zonaDestino, DondeCae(y, altoChip, cuantosEnDestino), "");
    }

    // ---------- La lista de fichas ----------
    //
    // Todo el catálogo, con el estado de cada uno. Los puestos primero, en el orden en que
    // se dibujan; después los que quedan por poner. Así la página contesta de un vistazo
    // las dos preguntas que no contestaba: qué tengo puesto y dónde, y qué me queda.
    // Una ficha por INSTANCIA puesta, más una por cada id que no lo está.
    //
    // La primera versión hacía una ficha por id del catálogo, y con eso un separador
    // puesto tres veces salía como UN chip. En la barra había tres rayitas y en Ajustes una
    // sola fila: no se veía cuántos había, no se podía mover el de en medio y el botón de
    // quitar actuaba siempre sobre el primero, porque `DondeEsta` devuelve el primero.
    //
    // Es el mismo razonamiento que ya estaba escrito en `ZonasWidgets.Quitar(zona, indice)`
    // —«la instancia exacta que tienes delante en el modo edición»— y que el editor de
    // Ajustes no seguía: el modelo sabía contar repetidos y la interfaz no.
    public static List<Ficha> Fichas(ZonasWidgets z)
    {
        RegistroWidgets.Asegurar();
        var res = new List<Ficha>();

        // 1. Lo que está puesto, en su orden y una ficha por instancia.
        foreach (var zona in ZonasWidgets.Nombres)
        {
            var enLaZona = z.De(zona);
            for (int i = 0; i < enLaZona.Count; i++)
            {
                string id = enLaZona[i];
                var d = RegistroWidgets.Buscar(id)?.Descriptor;
                if (d == null) continue;     // los desconocidos van por su lado (`Desconocidos`)
                res.Add(new Ficha(id, d.Nombre, d.Glifo, d.Categoria,
                                  true, zona, i, z.EstaOculto(id),
                                  d.Repetible, z.CuantosHay(id), d.Avisos));
            }
        }

        // 2. Y lo que no está puesto, para poder añadirlo.
        foreach (var id in RegistroWidgets.Ids)
        {
            if (z.CuantosHay(id) > 0) continue;
            var d = RegistroWidgets.Buscar(id)?.Descriptor;
            if (d == null) continue;
            res.Add(new Ficha(id, d.Nombre, d.Glifo, d.Categoria,
                              false, "", -1, false, d.Repetible, 0, d.Avisos));
        }
        return res;
    }

    // Los ids puestos que el registro no conoce: un config escrito por una NubeBar más
    // nueva, o un módulo retirado. Se enseñan para poder quitarlos, porque si no la barra
    // dibuja un hueco de «módulo desconocido» y no hay forma de limpiarlo desde aquí.
    public static List<string> Desconocidos(ZonasWidgets z) => z.Desconocidos().ToList();
}
