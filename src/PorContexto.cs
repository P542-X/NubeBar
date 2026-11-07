using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E7: configuración por monitor, escritorio y orientación.
//
// ---------- El problema ----------
//
// «Quiero el dock solo en el monitor grande.» «En el escritorio de juego, la barra fina y sin
// módulos de sistema.» «En vertical, el panel izquierdo más estrecho.»
//
// Son tres preguntas distintas con la misma forma: **el mismo ajuste vale una cosa u otra
// según dónde estés**. Resolverlas con tres mecanismos separados daría tres sitios donde
// equivocarse; resolverlas con uno obliga a decidir qué pasa cuando dos encajan a la vez.
//
// ---------- Por qué se parece tanto a las reglas ----------
//
// Porque es el mismo problema: varios candidatos que encajan y hay que elegir. Así que usa el
// **mismo criterio de desempate que `HyprReglas`**: primero la prioridad, luego la más
// concreta —la que mira más cosas—, y si siguen empatadas, la primera de la lista.
//
// Usar un criterio distinto aquí sería pedirle al usuario que aprenda dos sistemas de
// prioridad en el mismo programa.
public static class PorContexto
{
    // ---------- Dónde estás ----------
    //
    // El monitor es el ÍNDICE, no el handle: un handle cambia entre arranques y una
    // configuración guardada con handles no sobreviviría a reiniciar.
    public readonly record struct Contexto(int Monitor, int Escritorio, bool Vertical)
    {
        public override string ToString() =>
            $"monitor {Monitor}, escritorio {Escritorio}, {(Vertical ? "vertical" : "horizontal")}";
    }

    // ---------- Un ajuste que solo vale en cierto sitio ----------
    //
    // Los tres filtros son opcionales. Lo que se deja en blanco **no se mira**, que es la
    // misma regla que en las reglas de ventanas: un ajuste sin filtros vale siempre y es el
    // valor de serie.
    public sealed record Ajuste<T>(
        T Valor,
        int? Monitor = null,
        int? Escritorio = null,
        bool? Vertical = null,
        int Prioridad = 0,
        string Id = "")
    {
        // Cuántas cosas mira. Es el desempate cuando dos tienen la misma prioridad: la más
        // concreta gana, porque es la que el usuario escribió pensando en un caso.
        public int Especificidad =>
            (Monitor != null ? 1 : 0) + (Escritorio != null ? 1 : 0) + (Vertical != null ? 1 : 0);

        public bool EsElDeSerie => Especificidad == 0;

        public bool Encaja(Contexto c) =>
            (Monitor == null || Monitor == c.Monitor) &&
            (Escritorio == null || Escritorio == c.Escritorio) &&
            (Vertical == null || Vertical == c.Vertical);

        public string Donde
        {
            get
            {
                if (EsElDeSerie) return "siempre";
                var t = new List<string>();
                if (Monitor is { } m) t.Add($"monitor {m}");
                if (Escritorio is { } e) t.Add($"escritorio {e}");
                if (Vertical is { } v) t.Add(v ? "en vertical" : "en horizontal");
                return string.Join(" y ", t);
            }
        }

        public override string ToString() =>
            $"{(Id.Length > 0 ? Id + ": " : "")}{Valor} ({Donde}" +
            (Prioridad != 0 ? $", prioridad {Prioridad}" : "") + ")";
    }

    // ---------- Elegir ----------
    //
    // Devuelve el que manda, o `null` si ninguno encaja —que pasa cuando no hay uno de serie
    // y el contexto no cumple ninguno—. Quien llame decide qué hacer con eso; inventarse un
    // valor aquí sería decidir por él.
    public static Ajuste<T>? Elegir<T>(IEnumerable<Ajuste<T>> ajustes, Contexto c)
    {
        if (ajustes == null) return null;

        return Ordenar(ajustes.Where(a => a != null && a.Encaja(c))).FirstOrDefault();
    }

    public static T? Valor<T>(IEnumerable<Ajuste<T>> ajustes, Contexto c, T? siNoHay = default) =>
        Elegir(ajustes, c) is { } a ? a.Valor : siNoHay;

    // El orden en que se evalúan: el que manda primero. Es público porque un panel que
    // enseñe los ajustes tiene que poder enseñarlos en el orden de VERDAD; si los ordenara
    // de otra forma, el de arriba no sería el que gana.
    public static List<Ajuste<T>> Ordenar<T>(IEnumerable<Ajuste<T>> ajustes)
    {
        var lista = (ajustes ?? Array.Empty<Ajuste<T>>()).Where(a => a != null).ToList();

        // `OrderByDescending` es estable en .NET, así que los empates conservan el orden de
        // la lista — que es el tercer criterio. No hace falta un índice explícito.
        return lista
            .OrderByDescending(a => a.Prioridad)
            .ThenByDescending(a => a.Especificidad)
            .ToList();
    }

    // ================= Por qué ha ganado ese =================
    //
    // La misma idea que la explicación de las reglas: delante de un ajuste que no hace lo que
    // esperabas, lo que hace falta no es la lista, es **cuál ganó y por qué se descartaron los
    // demás**.
    public sealed record Linea(string Texto, bool Gano, bool Encaja);

    public static List<Linea> Explicar<T>(IEnumerable<Ajuste<T>> ajustes, Contexto c)
    {
        var filas = new List<Linea>();
        var ordenados = Ordenar(ajustes);
        var ganador = ordenados.FirstOrDefault(a => a.Encaja(c));

        filas.Add(new($"Estás en: {c}", false, false));

        if (ganador == null)
        {
            filas.Add(new("Ningún ajuste encaja aquí, y no hay uno de serie: " +
                          "no se aplica ninguno.", false, false));
        }

        foreach (var a in ordenados)
        {
            bool encaja = a.Encaja(c);
            bool gano = ReferenceEquals(a, ganador);

            string texto = gano
                ? $"{a} — MANDA" + (a.EsElDeSerie ? " (es el de serie)" : "")
                : encaja
                    ? $"{a} — encaja, pero gana otro " +
                      (ganador!.Prioridad > a.Prioridad
                          ? "con más prioridad"
                          : ganador.Especificidad > a.Especificidad
                              ? "más concreto"
                              : "que está antes en la lista")
                    : $"{a} — no encaja: {PorQueNo(a, c)}";

            filas.Add(new(texto, gano, encaja));
        }
        return filas;
    }

    static string PorQueNo<T>(Ajuste<T> a, Contexto c)
    {
        if (a.Monitor != null && a.Monitor != c.Monitor)
            return $"pide el monitor {a.Monitor} y estás en el {c.Monitor}";
        if (a.Escritorio != null && a.Escritorio != c.Escritorio)
            return $"pide el escritorio {a.Escritorio} y estás en el {c.Escritorio}";
        if (a.Vertical != null && a.Vertical != c.Vertical)
            return $"pide {(a.Vertical.Value ? "vertical" : "horizontal")} y la barra está " +
                   $"{(c.Vertical ? "vertical" : "horizontal")}";
        return "no encaja";
    }

    // ================= Lo que está mal escrito =================
    //
    // Como en las reglas: todos los problemas a la vez, y separando lo que impide usar el
    // ajuste de lo que solo es raro.
    public sealed record Queja(string Texto, bool Grave);

    public static List<Queja> Revisar<T>(IEnumerable<Ajuste<T>> ajustes, int monitores, int escritorios)
    {
        var l = new List<Queja>();
        var lista = (ajustes ?? Array.Empty<Ajuste<T>>()).Where(a => a != null).ToList();

        foreach (var a in lista)
        {
            if (a.Monitor is < 0)
                l.Add(new($"«{a}»: un monitor no puede ser negativo.", true));
            else if (a.Monitor is { } m && monitores > 0 && m >= monitores)
                // No es grave: puedes tener un ajuste para un monitor que ahora está
                // desconectado, y borrarlo al desconectarlo sería perder tu configuración.
                l.Add(new($"«{a}»: ahora mismo no hay monitor {m} (hay {monitores}). " +
                          "El ajuste se guarda y volverá a valer al conectarlo.", false));

            if (a.Escritorio is < 1)
                l.Add(new($"«{a}»: los escritorios se cuentan desde 1.", true));
            else if (a.Escritorio is { } e && escritorios > 0 && e > escritorios)
                l.Add(new($"«{a}»: ahora mismo no hay escritorio {e} (hay {escritorios}).", false));
        }

        // ---------- Dos que tapan al otro ----------
        //
        // Dos ajustes idénticos en filtros y prioridad: el segundo no se aplicará nunca, y
        // eso es casi siempre un descuido al duplicar uno para cambiarle un valor.
        foreach (var g in lista.GroupBy(a => (a.Monitor, a.Escritorio, a.Vertical, a.Prioridad))
                               .Where(g => g.Count() > 1))
            l.Add(new($"Hay {g.Count()} ajustes con los mismos filtros y la misma prioridad " +
                      $"({g.First().Donde}): solo se aplicará el primero.", false));

        // ---------- Más de uno «de serie» ----------
        var deSerie = lista.Count(a => a.EsElDeSerie);
        if (deSerie > 1)
            l.Add(new($"Hay {deSerie} ajustes sin filtros. Solo el primero hará de valor " +
                      "de serie; los demás no se usarán nunca.", false));

        return l;
    }
}
