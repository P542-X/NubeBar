using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E7: las pruebas de la configuración por contexto.
//
// Puras. Lo que se comprueba es el desempate —qué ajuste gana cuando encajan varios— y que
// ese desempate es **el mismo** que usan las reglas de ventanas. Dos sistemas de prioridad
// distintos en el mismo programa es pedirle al usuario que aprenda dos cosas donde había una.
public static partial class Pruebas
{
    internal static void CorrerE7()
    {
        E7LoQueEncajaYLoQueNo();
        E7ElDesempate();
        E7LaExplicacion();
        E7LoQueEstaMalEscrito();
        E7LosPerfilesNoEnciendenNada();
    }

    static PorContexto.Ajuste<string> A(string v, int? mon = null, int? esc = null,
                                        bool? vert = null, int prio = 0, string id = "") =>
        new(v, mon, esc, vert, prio, id);

    // ================= Encajar =================
    static void E7LoQueEncajaYLoQueNo()
    {
        Grupo("E7: un ajuste solo vale donde dice");

        var aqui = new PorContexto.Contexto(Monitor: 0, Escritorio: 1, Vertical: false);

        // Sin filtros: vale siempre. Es el valor de serie.
        var serie = A("de serie");
        Vale(serie.EsElDeSerie, "un ajuste sin filtros es el de serie");
        Vale(serie.Encaja(aqui), "y encaja aquí");
        Vale(serie.Encaja(new PorContexto.Contexto(9, 9, true)), "y en cualquier otro sitio");
        Igual("siempre", serie.Donde, "y se dice que vale siempre");

        // Con un filtro.
        Vale(A("x", mon: 0).Encaja(aqui), "el monitor 0 encaja en el monitor 0");
        Vale(!A("x", mon: 1).Encaja(aqui), "el monitor 1 no");
        Vale(A("x", esc: 1).Encaja(aqui), "el escritorio 1 encaja");
        Vale(!A("x", esc: 2).Encaja(aqui), "el 2 no");
        Vale(A("x", vert: false).Encaja(aqui), "«en horizontal» encaja con la barra horizontal");
        Vale(!A("x", vert: true).Encaja(aqui), "«en vertical» no");

        // Con varios: tienen que cumplirse TODOS, como en las reglas de ventanas.
        Vale(A("x", mon: 0, esc: 1, vert: false).Encaja(aqui), "los tres a la vez encajan");
        Vale(!A("x", mon: 0, esc: 2, vert: false).Encaja(aqui),
             "si falla uno, no encaja: se cumplen todos o ninguno");

        // La especificidad cuenta los filtros puestos.
        Igual(0, A("x").Especificidad, "sin filtros, especificidad 0");
        Igual(3, A("x", 0, 1, false).Especificidad, "con los tres, 3");

        // Y se puede contar dónde vale, para enseñarlo.
        Vale(A("x", mon: 1).Donde.Contains("monitor 1"), "se dice en qué monitor vale");
        Vale(A("x", vert: true).Donde.Contains("vertical"), "y en qué orientación");
    }

    // ================= El desempate =================
    static void E7ElDesempate()
    {
        Grupo("E7: cuando encajan varios, gana el mismo que ganaría una regla");

        var aqui = new PorContexto.Contexto(0, 1, false);

        // ---------- 1. La prioridad manda ----------
        var porPrioridad = new[]
        {
            A("concreto", mon: 0, esc: 1, vert: false, prio: 0, id: "a"),
            A("prioritario", prio: 10, id: "b"),
        };
        Igual("prioritario", PorContexto.Valor(porPrioridad, aqui),
              "la prioridad manda sobre la concreción, como en las reglas");

        // ---------- 2. A igual prioridad, el más concreto ----------
        var porConcrecion = new[]
        {
            A("de serie", id: "a"),
            A("de este monitor", mon: 0, id: "b"),
            A("de este monitor y escritorio", mon: 0, esc: 1, id: "c"),
        };
        Igual("de este monitor y escritorio", PorContexto.Valor(porConcrecion, aqui),
              "a igual prioridad, gana el que mira más cosas");

        // ---------- 3. Y si siguen empatados, el primero de la lista ----------
        var empate = new[]
        {
            A("el primero", mon: 0, id: "a"),
            A("el segundo", mon: 0, id: "b"),
        };
        Igual("el primero", PorContexto.Valor(empate, aqui),
              "empatados del todo, manda el primero de la lista");

        // Y ese orden es estable: si dependiera de algo interno, dos arranques elegirían
        // distinto y nadie sabría por qué.
        for (int i = 0; i < 20; i++)
            Igual("el primero", PorContexto.Valor(empate, aqui), "y no cambia al repetirlo");

        // ---------- Lo que no encaja no gana, por mucha prioridad que tenga ----------
        var altaPeroLejos = new[]
        {
            A("del otro monitor", mon: 5, prio: 100, id: "a"),
            A("de aquí", id: "b"),
        };
        Igual("de aquí", PorContexto.Valor(altaPeroLejos, aqui),
              "la prioridad no sirve de nada si el ajuste no encaja");

        // ---------- Sin nada que encaje ----------
        var ninguno = new[] { A("de otro sitio", mon: 7) };
        Vale(PorContexto.Elegir(ninguno, aqui) == null, "si no encaja ninguno, no hay ganador");
        Igual("el de siempre", PorContexto.Valor(ninguno, aqui, "el de siempre"),
              "y quien llama decide qué poner en su lugar");

        // ---------- Lo que no revienta ----------
        Vale(PorContexto.Elegir<string>(null!, aqui) == null, "sin lista no revienta");
        Igual(0, PorContexto.Ordenar<string>(null!).Count, "ni al ordenar nada");
        Vale(PorContexto.Elegir(Array.Empty<PorContexto.Ajuste<string>>(), aqui) == null,
             "ni con una lista vacía");

        // ---------- El orden que se enseña es el orden de verdad ----------
        //
        // Si un panel los ordenara de otra forma, el de arriba no sería el que gana y el
        // panel estaría enseñando una prioridad que no es la del programa.
        var orden = PorContexto.Ordenar(porConcrecion);
        Igual("de este monitor y escritorio", orden[0].Valor, "el primero de la lista es el que manda");
    }

    // ================= La explicación =================
    static void E7LaExplicacion()
    {
        Grupo("E7: por qué ha ganado ese ajuste");

        var aqui = new PorContexto.Contexto(0, 2, true);
        var ajustes = new[]
        {
            A("serie", id: "base"),
            A("solo en el monitor 1", mon: 1, id: "otro-monitor"),
            A("en vertical", vert: true, id: "vert"),
            A("en el escritorio 9", esc: 9, id: "esc9"),
        };

        var filas = PorContexto.Explicar(ajustes, aqui);
        Vale(filas.Count > 0, "la explicación tiene renglones");
        Vale(filas[0].Texto.Contains("Estás en"), "empieza diciendo dónde estás");
        Vale(filas[0].Texto.Contains("vertical"), "con la orientación incluida");

        // Uno y solo uno manda.
        Igual(1, filas.Count(f => f.Gano), "manda exactamente uno");
        Vale(filas.Single(f => f.Gano).Texto.Contains("en vertical"),
             "y es el más concreto que encaja");

        // ---------- Y de los demás se dice POR QUÉ no ----------
        //
        // Es la mitad útil: un ajuste que no se aplica se depura leyendo el motivo, no
        // mirando la lista.
        var noEncajan = filas.Where(f => !f.Gano && !f.Encaja && !f.Texto.StartsWith("Estás")).ToList();
        Vale(noEncajan.Count >= 2, "los que no encajan salen también");
        Vale(noEncajan.Any(f => f.Texto.Contains("monitor 1") && f.Texto.Contains("estás en el 0")),
             "y se dice qué monitor pide y en cuál estás");
        Vale(noEncajan.Any(f => f.Texto.Contains("escritorio 9")),
             "lo mismo con el escritorio");

        // El que encaja pero pierde dice por qué pierde.
        var pierde = filas.FirstOrDefault(f => !f.Gano && f.Encaja);
        Vale(pierde != null && (pierde.Texto.Contains("concreto") ||
                                pierde.Texto.Contains("prioridad") ||
                                pierde.Texto.Contains("antes en la lista")),
             "el que encaja y pierde dice por qué pierde");

        // ---------- Sin ninguno que encaje ----------
        var vacio = PorContexto.Explicar(new[] { A("lejos", mon: 8) }, aqui);
        Vale(vacio.Any(f => f.Texto.Contains("Ningún ajuste encaja")),
             "si no encaja ninguno, se dice claramente");
    }

    // ================= Lo que está mal escrito =================
    static void E7LoQueEstaMalEscrito()
    {
        Grupo("E7: los ajustes mal escritos se dicen");

        // ---------- Grave ----------
        Vale(PorContexto.Revisar(new[] { A("x", mon: -1) }, 2, 3).Any(q => q.Grave),
             "un monitor negativo es grave");
        Vale(PorContexto.Revisar(new[] { A("x", esc: 0) }, 2, 3)
                        .Any(q => q.Grave && q.Texto.Contains("desde 1")),
             "el escritorio 0 es grave: se cuentan desde 1");

        // ---------- NO grave: un monitor desconectado ----------
        //
        // Puedes tener un ajuste para el monitor del trabajo. Borrarlo al desconectarlo sería
        // perder tu configuración por llevarte el portátil a casa.
        var desconectado = PorContexto.Revisar(new[] { A("x", mon: 3) }, 2, 3);
        Vale(desconectado.Any(q => !q.Grave && q.Texto.Contains("volverá a valer")),
             "un monitor que ahora no está se avisa, no se rechaza");

        // ---------- Duplicados ----------
        var dobles = PorContexto.Revisar(new[] { A("a", mon: 0), A("b", mon: 0) }, 2, 3);
        Vale(dobles.Any(q => q.Texto.Contains("solo se aplicará el primero")),
             "dos ajustes idénticos avisan de que el segundo no se usará");

        // ---------- Dos «de serie» ----------
        var dosSerie = PorContexto.Revisar(new[] { A("a"), A("b") }, 2, 3);
        Vale(dosSerie.Any(q => q.Texto.Contains("sin filtros")),
             "dos ajustes sin filtros avisan");

        // ---------- Uno bien escrito no inventa quejas ----------
        Igual(0, PorContexto.Revisar(new[] { A("x", mon: 0, esc: 1) }, 2, 3).Count,
              "un ajuste correcto no tiene nada que avisar");
        Igual(0, PorContexto.Revisar<string>(null!, 2, 3).Count, "y revisar nada no revienta");
    }

    // ================= Los perfiles =================
    static void E7LosPerfilesNoEnciendenNada()
    {
        Grupo("E7: un perfil puede traer reglas y atajos, pero no encender la capa");

        // Esto ya venía de D5.1 y es lo que E7 pedía de los perfiles. Se comprueba aquí
        // porque es la garantía que no puede romperse al tocar la lista de claves.
        Vale(Perfiles.Permitida("NubeHypr", "Reglas"), "un perfil puede traer reglas");
        Vale(Perfiles.Permitida("NubeHypr", "Atajos"), "y atajos");

        // Y lo que NO puede, que importa más: si un perfil pudiera encender la capa o
        // ponerla en «aplicar», aplicar un perfil te empezaría a mover ventanas sin pedirlo.
        Vale(!Perfiles.Permitida("NubeHypr", "Activo"),
             "pero NO puede encender NubeHypr");
        Vale(!Perfiles.Permitida("NubeHypr", "Modo"),
             "ni cambiar su modo a «aplicar»");

        // Ni tocar cosas que no son suyas.
        foreach (var (s, c) in new[] { ("Barra", "AtajoVistaGeneral"), ("Wallpaper", "Integrar") })
            Vale(!Perfiles.Permitida(s, c), $"ni «{s}.{c}»");
    }
}
