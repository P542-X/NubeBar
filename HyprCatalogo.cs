using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D6: el panel de reglas y atajos — búsqueda, grupos, estado y explicación.
//
// ---------- El problema que resuelve ----------
//
// D5 dejó reglas con prioridad, especificidad y una explicación por regla, y un editor de
// atajos con 19 filas. Con tres reglas y 19 atajos eso se lee de un vistazo. Con treinta
// reglas, no: hace falta buscar, agrupar y ver de un golpe cuáles están apagadas. Y hace
// falta lo que de verdad pide el documento — "explicacion de la regla aplicada" — que no
// es la lista de reglas, es: *a esta ventana que tengo delante, qué regla le toca y por
// qué*.
//
// Todo lo de aquí es puro: se busca y se agrupa sobre listas, sin tocar la configuración y
// sin abrir una ventana.
public static class HyprCatalogo
{
    // ================= Buscar =================
    //
    // Sobre todos los campos de la regla, no solo el id: lo que uno recuerda es "la del
    // navegador", y el id puede ser "regla-4". Insensible a acentos y a mayúsculas por
    // `Parecido.Normalizar`, que es el mismo normalizador del buscador de aplicaciones —
    // dos normalizadores distintos en el mismo programa es la forma de que "Música" se
    // encuentre en un sitio y no en el otro.
    public static bool Coincide(HyprReglas.Regla r, string busqueda)
    {
        var q = Parecido.Normalizar(busqueda ?? "");
        if (q.Length == 0) return true;                 // sin búsqueda, todo vale

        foreach (var campo in Campos(r))
            if (Parecido.Normalizar(campo).Contains(q, StringComparison.Ordinal)) return true;
        return false;
    }

    // Los campos por los que se busca. El layout y el espacio también: "espacio 2" es una
    // forma legítima de buscar una regla, y "principal" otra.
    static IEnumerable<string> Campos(HyprReglas.Regla r)
    {
        yield return r.Id;
        if (r.Proceso != null) yield return r.Proceso;
        if (r.Clase != null) yield return r.Clase;
        if (r.Titulo != null) yield return r.Titulo;
        if (r.Ruta != null) yield return r.Ruta;
        if (r.Layout != null) yield return r.Layout.ToString()!;
        if (r.Espacio != null) yield return "espacio " + r.Espacio;
        if (r.Monitor != null) yield return "monitor " + r.Monitor;
        if (r.Flotante == true) yield return "flotante";
        if (r.Foco == true) yield return "foco";
    }

    public static List<HyprReglas.Regla> Buscar(IEnumerable<HyprReglas.Regla> reglas,
                                                string busqueda, bool soloActivas = false)
    {
        return reglas.Where(r => (!soloActivas || r.Activa) && Coincide(r, busqueda)).ToList();
    }

    // ================= Agrupar =================
    //
    // Por lo que la regla MIRA, no por lo que hace. Es la pregunta que uno se hace al
    // abrir el panel: "¿qué reglas afectan a Firefox?". Agrupar por acción —"las que
    // mueven de espacio"— contesta una pregunta que nadie tiene.
    //
    // El grupo es el proceso si lo hay; si no, la clase; si no, el título; si no, la ruta;
    // y si no mira nada, el grupo de las que no se aplican nunca, que conviene ver juntas
    // porque suelen ser un error o una regla a medio escribir.
    public const string GrupoSinCriterios = "Sin criterios (no se aplican)";

    public static string GrupoDe(HyprReglas.Regla r)
    {
        if (r.Proceso is { Length: > 0 } p) return p;
        if (r.Clase is { Length: > 0 } c) return "clase: " + c;
        if (r.Titulo is { Length: > 0 } t) return "título: " + t;
        if (r.Ruta is { Length: > 0 } ru) return "ruta: " + ru;
        return GrupoSinCriterios;
    }

    public sealed record Grupo(string Nombre, List<HyprReglas.Regla> Reglas)
    {
        public int Activas => Reglas.Count(r => r.Activa);
        public int Apagadas => Reglas.Count - Activas;

        // El renglón del grupo. Dice el número de reglas y, si hay alguna apagada, cuántas:
        // una regla apagada que no se ve es la causa número uno de "mi regla no funciona".
        public string Resumen => Apagadas == 0
            ? $"{Reglas.Count} regla{(Reglas.Count == 1 ? "" : "s")}"
            : $"{Reglas.Count} regla{(Reglas.Count == 1 ? "" : "s")}, {Apagadas} apagada{(Apagadas == 1 ? "" : "s")}";
    }

    public static List<Grupo> Agrupar(IEnumerable<HyprReglas.Regla> reglas)
    {
        // Dentro de cada grupo, el mismo orden con el que el motor las evalúa. Si el panel
        // las ordenara de otra forma, la de arriba no sería la que gana y el panel estaría
        // enseñando una prioridad que no es la de verdad.
        var grupos = reglas
            .GroupBy(GrupoDe, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Grupo(g.Key, HyprReglas.Ordenar(g)))
            .ToList();

        // Los grupos: por nombre, salvo el de "sin criterios", que va al final porque es
        // una bandeja de problemas y no un grupo más.
        return grupos
            .OrderBy(g => g.Nombre == GrupoSinCriterios ? 1 : 0)
            .ThenBy(g => g.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // ================= La explicación de la regla aplicada =================
    //
    // Esto es lo que el documento pide y lo que no existía: delante de una ventana
    // concreta, qué decidió el motor y por qué. No es la lista de reglas: es la decisión.
    //
    // Se construye desde el mismo `HyprVigilante.Apunte` que se escribe en el log, así que
    // **el panel y el log no pueden decir cosas distintas**. Si se recalculara aquí, el
    // día que una cambiara el usuario tendría dos verdades y ninguna forma de saber cuál
    // es la que se aplica.
    public sealed record Renglon(string Texto, Tipo Que);

    public enum Tipo { Cabecera, Puesto, SinOpinion, Coincide, NoCoincide, Nada }

    public static List<Renglon> Explicar(HyprVigilante.Apunte a)
    {
        var filas = new List<Renglon>
        {
            new($"{a.Proceso} — {Corto(a.Titulo)}", Tipo.Cabecera),
        };

        if (!a.AlgunaCoincidio)
        {
            filas.Add(new("Ninguna regla coincide: la ventana se queda donde la ponga Windows",
                          Tipo.Nada));
            foreach (var e in a.Explicaciones)
                filas.Add(new($"{e.Id}: {e.Porque}", Tipo.NoCoincide));
            return filas;
        }

        // Primero lo que se decidió, con el dueño de cada campo. Es la respuesta a "¿por
        // qué esta ventana sale flotando?" sin releer la configuración.
        foreach (var campo in a.CamposPuestos)
            filas.Add(new($"{campo}: {Valor(a, campo)}  ·  lo pone «{a.Decision.Quien.GetValueOrDefault(campo, "?")}»",
                          Tipo.Puesto));

        if (a.CamposSinOpinion.Count > 0)
            filas.Add(new("Sin opinión: " + string.Join(", ", a.CamposSinOpinion), Tipo.SinOpinion));

        // Y después las reglas, las que coinciden y las que no, con el motivo. Las que no
        // coinciden son la mitad útil: una regla que no dispara se depura leyendo por qué.
        foreach (var e in a.Explicaciones)
            filas.Add(e.Coincidio
                ? new($"{e.Id}: {(e.Aplico.Length > 0 ? e.Aplico : "coincide, pero no aportó nada nuevo")}", Tipo.Coincide)
                : new($"{e.Id}: {e.Porque}", Tipo.NoCoincide));

        return filas;
    }

    static string Valor(HyprVigilante.Apunte a, string campo) => campo switch
    {
        "Espacio" => a.Decision.Espacio?.ToString() ?? "?",
        "Monitor" => a.Decision.Monitor?.ToString() ?? "?",
        "Layout" => a.Decision.Layout?.ToString() ?? "?",
        "Flotante" => a.Decision.Flotante == true ? "sí" : "no",
        // El mismo escritor que usan las reglas: un alto que nadie ha puesto se escribe «—»
        // y no «0», que querría decir una ventana sin altura.
        "Tamano" => HyprReglas.Tamano(a.Decision.Ancho, a.Decision.Alto),
        "Foco" => a.Decision.Foco == true ? "sí" : "no",
        _ => "?",
    };

    static string Corto(string s) => s.Length <= 52 ? s : s[..49] + "…";

    // ---------- Y el aviso que tiene que estar en el panel ----------
    //
    // Mientras las reglas no se apliquen de verdad, el panel **tiene que decirlo**. Un
    // panel que enseña decisiones sin aclarar que no se ejecutan es un panel que miente, y
    // el usuario acabaría pensando que las reglas están roto cuando están desconectadas a
    // propósito.
    public static string AvisoDeSimulacion(NubeHypr.Modo modo) => modo switch
    {
        NubeHypr.Modo.Apagado => "NubeHypr está apagado: las reglas no se evalúan.",
        _ => "Modo simulación: las reglas deciden y se apuntan, pero NO se mueve ninguna ventana.",
    };

    // ================= Los atajos, con la misma búsqueda =================
    //
    // El editor de D5 enseña las 19 filas siempre. Con una búsqueda, "¿qué tecla era la de
    // flotante?" se contesta escribiendo "flot".
    public static List<string> BuscarAtajos(IEnumerable<string> nombres, string busqueda,
                                            Func<string, string> rotulo,
                                            Func<string, string?> combinacion)
    {
        var q = Parecido.Normalizar(busqueda ?? "");
        if (q.Length == 0) return nombres.ToList();

        return nombres.Where(n =>
            Parecido.Normalizar(n).Contains(q, StringComparison.Ordinal) ||
            Parecido.Normalizar(rotulo(n)).Contains(q, StringComparison.Ordinal) ||
            Parecido.Normalizar(combinacion(n) ?? "").Contains(q, StringComparison.Ordinal)
        ).ToList();
    }

    // Los atajos agrupados por familia. El prefijo del nombre semántico ya es la familia
    // ("ciclo.siguiente", "espacio.3"), así que no hace falta una tabla aparte que se
    // quede desincronizada el día que se añada un comando.
    public static List<Grupo2> AgruparAtajos(IEnumerable<string> nombres)
    {
        return nombres
            .GroupBy(n => n.Contains('.') ? n[..n.IndexOf('.')] : "otros")
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new Grupo2(g.Key, g.ToList()))
            .ToList();
    }

    public sealed record Grupo2(string Familia, List<string> Nombres);
}
