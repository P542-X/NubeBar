using System;
using System.Linq;

namespace NubeBar;

// E3: las pruebas de los paneles laterales.
//
// Puras: rectángulos de pantalla inventados y cifras inventadas. Así se puede comprobar lo que
// no se puede provocar a mano — un portátil de 1024 px con los dos paneles abiertos, un panel
// que pide más alto que la pantalla, un disco lleno al 99 %.
public static partial class Pruebas
{
    internal static void CorrerE3()
    {
        E3DondeSeColocanLosPaneles();
        E3LosDosALaVez();
        E3LasSecciones();
        E3LasCifras();
    }

    // ================= Colocación =================
    static void E3DondeSeColocanLosPaneles()
    {
        Grupo("E3: dónde se coloca un panel lateral");

        // Un monitor de 2560×1440 con la barra arriba de 38: el área útil empieza en 38.
        var util = new NubeHypr.Rect(0, 38, 2560, 1402);

        var izq = Lateral.Sitio(util, Lateral.Lado.Izquierda, 600);
        var der = Lateral.Sitio(util, Lateral.Lado.Derecha, 600);

        Igual(Lateral.Ancho, izq.Ancho, "el ancho es fijo");
        Igual(Lateral.Ancho, der.Ancho, "en los dos lados");
        Vale(izq.X >= util.X, "el izquierdo no se sale por la izquierda");
        Vale(der.X + der.Ancho <= util.X + util.Ancho, "ni el derecho por la derecha");

        // ---------- Nunca debajo de la barra ----------
        //
        // El área útil ya descuenta la barra cuando reserva espacio. Un panel que empezara en
        // Y=0 se metería debajo de ella.
        Vale(izq.Y >= util.Y, "no empieza por encima del área útil");
        Vale(izq.Y + izq.Alto <= util.Y + util.Alto, "ni termina por debajo");

        // ---------- Centrado en vertical ----------
        Vale(Math.Abs((izq.Y + izq.Alto / 2) - (util.Y + util.Alto / 2)) < 1,
             "está centrado: pegado arriba colgaría de la barra, pegado abajo parecería caído");

        // ---------- Más alto que la pantalla ----------
        //
        // Un panel con muchas secciones. Se recorta y se desplaza por dentro, no se sale.
        foreach (double pide in new[] { 2000.0, 5000, 100000 })
        {
            var r = Lateral.Sitio(util, Lateral.Lado.Izquierda, pide);
            Vale(r.Alto <= util.Alto, $"pidiendo {pide:0} de alto, no se pasa de la pantalla");
            Vale(r.Y >= util.Y && r.Y + r.Alto <= util.Y + util.Alto,
                 $"pidiendo {pide:0}, sigue dentro");
        }

        // ---------- Y más bajo que el mínimo ----------
        var minimo = Lateral.Sitio(util, Lateral.Lado.Izquierda, 10);
        Vale(minimo.Alto >= 120, "un panel casi vacío tiene un alto mínimo, no mide 10 px");

        // ---------- Una pantalla estrecha ----------
        var portatil = new NubeHypr.Rect(0, 0, 1024, 768);
        var p = Lateral.Sitio(portatil, Lateral.Lado.Derecha, 600);
        Vale(p.X >= 0 && p.X + p.Ancho <= 1024, "en 1024 px el panel sigue dentro");

        // Y una pantalla ridícula: el ancho se encoge en vez de salirse.
        var mini = new NubeHypr.Rect(0, 0, 300, 400);
        var m = Lateral.Sitio(mini, Lateral.Lado.Izquierda, 300);
        Vale(m.Ancho <= 300, "en 300 px de ancho, el panel se estrecha");
        Vale(m.X >= 0 && m.X + m.Ancho <= 300, "y no se sale");

        // ---------- El segundo monitor ----------
        //
        // Empieza donde acaba el primero. Un panel colocado en coordenadas del monitor en vez
        // de del escritorio saldría en la pantalla de al lado.
        var segundo = new NubeHypr.Rect(2560, 0, 1080, 1920);
        var s = Lateral.Sitio(segundo, Lateral.Lado.Izquierda, 800);
        Vale(s.X >= 2560, "en el segundo monitor sale en el segundo monitor");
        Vale(s.X + s.Ancho <= 2560 + 1080, "y no se escapa");
    }

    // ================= Los dos a la vez =================
    static void E3LosDosALaVez()
    {
        Grupo("E3: los dos paneles abiertos no se pisan");

        // Era el objetivo de E0: que dos superficies de contenido convivan.
        var ancha = new NubeHypr.Rect(0, 0, 2560, 1440);
        var a = Lateral.Sitio(ancha, Lateral.Lado.Izquierda, 700);
        var b = Lateral.Sitio(ancha, Lateral.Lado.Derecha, 700);

        Vale(!Lateral.SePisan(a, b), "en 2560 px los dos caben sin tocarse");
        Vale(Lateral.CabenLosDos(ancha), "y se sabe antes de abrirlos");

        // El hueco de escritorio que queda en medio es lo que importa: dos paneles pegados
        // sin escritorio entre ellos no son una composición, son una pantalla partida.
        double hueco = (b.X) - (a.X + a.Ancho);
        Vale(hueco >= Lateral.HuecoMinimoDeEscritorio,
             $"queda escritorio en medio ({hueco:0} px)");

        // ---------- Donde NO caben ----------
        var estrecha = new NubeHypr.Rect(0, 0, 800, 600);
        Vale(!Lateral.CabenLosDos(estrecha),
             "en 800 px no caben los dos con escritorio en medio");

        // Y la comprobación es coherente con la colocación: si dice que no caben, es porque
        // de verdad se pisarían o no dejarían hueco.
        foreach (double ancho in new[] { 600.0, 700, 800, 900, 1000, 1200, 1500, 2560 })
        {
            var u = new NubeHypr.Rect(0, 0, ancho, 800);
            var i = Lateral.Sitio(u, Lateral.Lado.Izquierda, 500);
            var d = Lateral.Sitio(u, Lateral.Lado.Derecha, 500);
            bool dice = Lateral.CabenLosDos(u);
            bool deVerdad = !Lateral.SePisan(i, d) &&
                            (d.X - (i.X + i.Ancho)) >= Lateral.HuecoMinimoDeEscritorio;
            Igual(dice, deVerdad, $"a {ancho:0} px, lo que dice y lo que pasa coinciden");
        }

        // Un panel no se pisa consigo mismo… pero el solape sí se detecta cuando lo hay.
        Vale(Lateral.SePisan(a, a), "un rectángulo se pisa consigo mismo");
        Vale(!Lateral.SePisan(a, new NubeHypr.Rect(5000, 0, 10, 10)), "y dos lejanos no");
    }

    // ================= Las secciones =================
    static void E3LasSecciones()
    {
        Grupo("E3: el catálogo de secciones");

        Vale(Lateral.Secciones.Length > 0, "hay secciones");

        // Ids únicos: dos con el mismo id serían dos sitios que enseñan lo mismo, y el que
        // llegara segundo taparía al primero.
        Igual(Lateral.Secciones.Length,
              Lateral.Secciones.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
              "ningún id repetido");

        foreach (var s in Lateral.Secciones)
        {
            Vale(s.Id.Length > 0, "cada sección tiene id");
            Vale(s.Titulo.Length > 0, $"«{s.Id}» tiene título");
            Vale(s.Que.Length > 10, $"«{s.Id}» explica para qué es");
        }

        // ---------- El reparto ----------
        //
        // Izquierda lo que controlas, derecha lo que pasa. Si una sección estuviera en los
        // dos, habría dos sitios para lo mismo.
        var izq = Lateral.De(Lateral.Lado.Izquierda);
        var der = Lateral.De(Lateral.Lado.Derecha);
        Vale(izq.Count > 0 && der.Count > 0, "los dos paneles tienen contenido");
        Igual(Lateral.Secciones.Length, izq.Count + der.Count, "y toda sección está en uno de los dos");
        Vale(!izq.Select(s => s.Id).Intersect(der.Select(s => s.Id)).Any(),
             "ninguna sección está en los dos lados");

        // Lo que E3 pide que lleve el izquierdo.
        //
        // `volumen` y `brillo` eran dos secciones y ahora son una, `audio`: dos cabeceras,
        // dos marcos y dos flechas para dos controles de una línea cada uno era irse en
        // cromo, y la maqueta del usuario los pone juntos. Lo que importa es que los dos
        // controles sigan estando, no cómo se llame la tarjeta — y que de verdad se dibujen
        // lo comprueba `--diagpaneles`, que los monta.
        foreach (var id in new[] { "perfil", "rapidas", "audio", "carpetas",
                                   "maquina", "ajustes" })
            Vale(izq.Any(s => s.Id == id), $"el panel izquierdo lleva «{id}»");

        // Y que no quede una sección huérfana del renombrado: un id viejo que ya no dibuja
        // nadie sale como un hueco en el panel, y eso no lo nota nadie hasta que lo ve.
        Vale(!Lateral.Secciones.Any(s => s.Id is "volumen" or "brillo"),
             "y no quedan las dos secciones viejas que se fusionaron");

        // ---------- Las acciones rápidas ----------
        //
        // Pocas: la referencia tiene cuatro. Un panel lleno de conmutadores es un panel que
        // nadie lee.
        Vale(Lateral.Rapidas.Length is > 0 and <= 6, "las acciones rápidas son pocas");
        Igual(Lateral.Rapidas.Length, Lateral.Rapidas.Select(a => a.Que).Distinct().Count(),
              "y ninguna repetida");

        foreach (var a in Lateral.Rapidas)
        {
            Vale(a.Titulo.Length > 0, $"«{a.Que}» tiene título");
            Vale(a.Apagada.Length > 0 && a.Encendida.Length > 0,
                 $"«{a.Que}» dice qué significa encendida y apagada");
            Vale(a.Apagada != a.Encendida,
                 $"«{a.Que}»: y las dos frases son distintas, o el conmutador no informa");
            Igual(a.Encendida, Lateral.Estado(a, true), $"«{a.Que}» encendida");
            Igual(a.Apagada, Lateral.Estado(a, false), $"«{a.Que}» apagada");
        }
    }

    // ================= Las cifras =================
    static void E3LasCifras()
    {
        Grupo("E3: cómo se escriben las cifras del panel");

        // ---------- El almacenamiento ----------
        //
        // «234 GB libres de 931 GB» contesta la pregunta que uno tiene; «697 GB usados»
        // obliga a restar.
        long gb = 1024L * 1024 * 1024;
        var t = Lateral.Almacen(234 * gb, 931 * gb);
        Vale(t.Contains("libres"), "se dice lo que queda libre");
        Vale(t.Contains("234") && t.Contains("931"), "con las dos cifras");

        Vale(Lateral.Tamano(2048L * gb).Contains("TB"), "a partir de un tera se escribe en TB");
        Vale(Lateral.Tamano(500 * gb).Contains("GB"), "y por debajo en GB");
        Vale(Lateral.Tamano(300L * 1024 * 1024).Contains("MB"), "y lo pequeño en MB");
        Igual("—", Lateral.Tamano(-1), "un tamaño negativo no se inventa");
        Igual("sin datos", Lateral.Almacen(0, 0), "un disco que no se puede leer lo dice");

        // ---------- La proporción ----------
        Igual(0.0, Lateral.UsadoDe(100, 100), "un disco vacío está al 0");
        Igual(1.0, Lateral.UsadoDe(0, 100), "uno lleno, al 1");
        Vale(Math.Abs(Lateral.UsadoDe(25, 100) - 0.75) < 0.001, "y en medio, lo que toca");
        Igual(0.0, Lateral.UsadoDe(0, 0), "y con el total a cero no revienta ni sale lleno");

        // Nunca fuera de 0..1, pase lo que pase: una barra al 150 % se sale de su canal.
        foreach (var (libres, total) in new (long, long)[] { (200, 100), (-50, 100), (0, -1) })
        {
            double u = Lateral.UsadoDe(libres, total);
            Vale(u is >= 0 and <= 1, $"con ({libres},{total}) la proporción sigue entre 0 y 1");
        }

        // ---------- El aviso ----------
        Vale(Lateral.QuedaPocoSitio(5 * gb, 500 * gb), "un disco al 99 % avisa");
        Vale(!Lateral.QuedaPocoSitio(250 * gb, 500 * gb), "uno a la mitad, no");
        Vale(!Lateral.QuedaPocoSitio(0, 0), "y uno ilegible tampoco: no se sabe");
    }
}
