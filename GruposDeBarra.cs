using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Los grupos visuales de la barra: qué módulos van juntos dentro de una misma pastilla.
//
// ---------- El problema ----------
//
// La barra montaba sus módulos uno detrás de otro, sin nada entre ellos. Con siete en la zona
// derecha —CPU, memoria, batería, notificaciones, bandeja, control, fecha— el resultado es una
// tira de iconos pegados donde no se distingue dónde acaba una cosa y empieza otra. Dicho por
// el usuario mirándolo: «poca jerarquía, pocos iconos, sin el acabado de las referencias».
//
// ---------- Por qué por CATEGORÍA y no por una lista escrita a mano ----------
//
// Porque los módulos los coloca el usuario. Una lista fija de «estos tres van juntos» deja de
// valer en cuanto alguien mueve uno, y entonces el agrupado dice una mentira: enseña como
// relacionadas cosas que el usuario separó a propósito.
//
// `RegistroWidgets` ya sabe de qué es cada módulo (`CategoriaWidget`), así que el grupo sale
// de ahí: **tramos seguidos de la misma categoría**. Si pones CPU y memoria juntos, salen en
// una pastilla; si metes el reloj entre medias, se parten en dos, que es lo correcto porque
// eso es lo que has pedido al colocarlos así.
//
// ---------- Lo que NO agrupa ----------
//
// Un tramo de uno solo no lleva pastilla. Un icono suelto dentro de su propia cápsula es más
// ruido que agrupación — la pastilla existe para decir «estos van juntos», y eso no se puede
// decir de uno.
//
// Y el separador (`Util`) nunca entra en una pastilla: es precisamente lo que el usuario pone
// para separar, así que meterlo dentro de un grupo sería anularlo.
public static class GruposDeBarra
{
    // Un tramo: los ids que van juntos y de qué categoría son.
    public sealed record Tramo(IReadOnlyList<string> Ids, CategoriaWidget Que)
    {
        // Un tramo se dibuja con pastilla solo si agrupa de verdad a más de uno.
        public bool ConPastilla => Ids.Count > 1 && Que != CategoriaWidget.Util;
    }

    // `categoriaDe` se pasa en vez de leerla aquí para poder probar esto sin registro: lo que
    // se comprueba es el agrupado, no el catálogo.
    public static List<Tramo> Partir(IEnumerable<string> ids,
                                     System.Func<string, CategoriaWidget> categoriaDe)
    {
        var tramos = new List<Tramo>();
        List<string>? actual = null;
        CategoriaWidget cat = CategoriaWidget.Util;

        foreach (var id in ids)
        {
            var c = categoriaDe(id);

            // El separador corta siempre y va solo: es lo que el usuario pone para separar.
            if (c == CategoriaWidget.Util)
            {
                if (actual is { Count: > 0 }) tramos.Add(new Tramo(actual, cat));
                tramos.Add(new Tramo(new[] { id }, c));
                actual = null;
                continue;
            }

            if (actual == null || c != cat)
            {
                if (actual is { Count: > 0 }) tramos.Add(new Tramo(actual, cat));
                actual = new List<string>();
                cat = c;
            }
            actual.Add(id);
        }

        if (actual is { Count: > 0 }) tramos.Add(new Tramo(actual, cat));
        return tramos;
    }

    // ---------- ¿Va una raya entre estos dos tramos? ----------
    //
    // La maqueta pide «separadores verticales finos entre grupos. No pastillas de fondo: una
    // línea». La regla de cuándo toca vive aquí, y no en un `if` del montaje de la barra, por
    // lo de siempre: una decisión metida en el código que pinta no se puede exigir en una
    // prueba — y esta tiene un caso que es fácil equivocar.
    //
    // Ese caso: nunca se pone raya al lado del separador que coloca el USUARIO (`Util`). Él
    // ya ha dicho ahí dónde quiere separar. Añadirle otra al lado da dos rayas juntas y deja
    // el módulo que puso pareciendo un fallo de dibujo.
    public static bool RayaEntre(Tramo antes, Tramo despues) =>
        antes.Que != CategoriaWidget.Util && despues.Que != CategoriaWidget.Util;

    // Para el registro y los diagnósticos: cómo ha quedado el reparto, en una línea.
    public static string Linea(IEnumerable<Tramo> tramos) =>
        string.Join(" · ", tramos.Select(t =>
            (t.ConPastilla ? "[" : "") +
            string.Join(",", t.Ids) +
            (t.ConPastilla ? "]" : "")));
}
