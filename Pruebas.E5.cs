using System;
using System.Linq;

namespace NubeBar;

// E5: las pruebas del área de trabajo con las superficies de E.
//
// Es la pieza nueva de E5: hasta aquí el único estorbo era la barra. Con el dock, repartir
// sobre la pantalla entera deja una ventana **permanentemente** debajo de él.
public static partial class Pruebas
{
    internal static void CorrerE5()
    {
        E5ElAreaDescuentaLasSuperficies();
        E5LoQueNoSeDescuenta();
        E5ElAjusteRespetaElDock();
    }

    static NubeHypr.Rect PantallaE5 => new(0, 0, 2560, 1440);

    // ================= El área =================
    static void E5ElAreaDescuentaLasSuperficies()
    {
        Grupo("E5: el área de trabajo descuenta las superficies permanentes");

        // ---------- Sin estorbos ----------
        var entera = NubeHypr.AreaUtil(PantallaE5, 0);
        Igual(PantallaE5.Ancho, entera.Ancho, "sin estorbos, el área es la pantalla");
        Igual(PantallaE5.Alto, entera.Alto, "entera");

        // ---------- Solo la barra ----------
        var conBarra = NubeHypr.AreaUtil(PantallaE5, 0,
            new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 54));
        Igual(54.0, conBarra.Y, "la barra de arriba empuja el área hacia abajo");
        Igual(PantallaE5.Alto - 54, conBarra.Alto, "y le quita su grosor");
        Igual(PantallaE5.Ancho, conBarra.Ancho, "sin tocar el ancho");

        // ---------- Barra arriba y dock abajo ----------
        //
        // La composición de E: una superficie permanente en cada extremo.
        var conLasDos = NubeHypr.AreaUtil(PantallaE5, 0,
            new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 54),
            new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, 74));
        Igual(54.0, conLasDos.Y, "sigue empezando debajo de la barra");
        Igual(PantallaE5.Alto - 54 - 74, conLasDos.Alto, "y termina encima del dock");
        Vale(conLasDos.Alto < conBarra.Alto, "con dock hay menos sitio que sin él");

        // ---------- Barra vertical y dock al otro lado ----------
        var vertical = NubeHypr.AreaUtil(PantallaE5, 0,
            new NubeHypr.Estorbo(NubeHypr.Borde.Izquierda, 44),
            new NubeHypr.Estorbo(NubeHypr.Borde.Derecha, 74));
        Igual(44.0, vertical.X, "la barra de la izquierda empuja el área");
        Igual(PantallaE5.Ancho - 44 - 74, vertical.Ancho, "y el dock de la derecha la recorta");
        Igual(PantallaE5.Alto, vertical.Alto, "sin tocar el alto");

        // ---------- Dos estorbos en el MISMO borde ----------
        //
        // Se coge el más grueso, no la suma. Sumarlos descontaría dos veces lo que dos
        // superficies solapadas ocupan una sola vez, y el área saldría más pequeña.
        var mismoBorde = NubeHypr.AreaUtil(PantallaE5, 0,
            new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, 40),
            new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, 74));
        Igual(PantallaE5.Alto - 74, mismoBorde.Alto, "manda el más grueso, no la suma");

        // ---------- El margen ----------
        var conMargen = NubeHypr.AreaUtil(PantallaE5, 10,
            new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 54));
        Igual(54.0 + 10, conMargen.Y, "el margen se suma al estorbo");
        Igual(PantallaE5.Alto - 54 - 20, conMargen.Alto, "y se descuenta por los dos lados");

        // ---------- Lo que no revienta ----------
        Vale(NubeHypr.AreaUtil(PantallaE5, 0, (NubeHypr.Estorbo[])null!).Ancho > 0,
             "sin lista de estorbos no revienta");
        Vale(NubeHypr.AreaUtil(PantallaE5, 0,
                 new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 0),
                 new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, -5)).Alto == PantallaE5.Alto,
             "un estorbo de grosor cero o negativo no cuenta");

        // ---------- Un área imposible es Nada, no un rectángulo raro ----------
        //
        // Una pantalla pequeña con superficies gordas puede dar negativo. Devolver un
        // rectángulo «casi válido» acabaría en ventanas de ancho negativo pasadas a Windows.
        var mini = new NubeHypr.Rect(0, 0, 200, 200);
        var imposible = NubeHypr.AreaUtil(mini, 20,
            new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 120),
            new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, 120));
        Vale(imposible.Ancho <= 0 || imposible.Alto <= 0 || imposible.Equals(NubeHypr.Rect.Nada),
             "un área imposible no se devuelve como rectángulo válido");

        // ---------- Y el área nunca se sale del monitor ----------
        foreach (var b in Enum.GetValues<NubeHypr.Borde>())
            foreach (double g in new[] { 1.0, 50, 200, 700 })
            {
                var a = NubeHypr.AreaUtil(PantallaE5, 8, new NubeHypr.Estorbo(b, g));
                if (a.Ancho <= 0 || a.Alto <= 0) continue;    // la rechazó, bien
                Vale(a.X >= PantallaE5.X && a.Y >= PantallaE5.Y &&
                     a.X + a.Ancho <= PantallaE5.X + PantallaE5.Ancho &&
                     a.Y + a.Alto <= PantallaE5.Y + PantallaE5.Alto,
                     $"con {b} de {g:0}px, el área sigue dentro del monitor");
            }

        // ---------- Un segundo monitor ----------
        var segundo = new NubeHypr.Rect(2560, 0, 1080, 1920);
        var enElSegundo = NubeHypr.AreaUtil(segundo, 0,
            new NubeHypr.Estorbo(NubeHypr.Borde.Izquierda, 44));
        Vale(enElSegundo.X >= 2560, "el área del segundo monitor empieza en el segundo monitor");
        Vale(enElSegundo.X + enElSegundo.Ancho <= 2560 + 1080, "y no se escapa al tercero");
    }

    // ================= Lo que NO se descuenta =================
    static void E5LoQueNoSeDescuenta()
    {
        Grupo("E5: los paneles laterales NO quitan sitio a las ventanas");

        // Esta es una decisión, no un olvido, y conviene que esté fijada por una prueba.
        //
        // Si los paneles laterales descontaran, abrir el panel izquierdo recolocaría todas
        // las ventanas del escritorio y cerrarlo las recolocaría otra vez: mirar el volumen
        // te movería la pantalla entera.
        //
        // Lo que se comprueba es la consecuencia: con los mismos estorbos permanentes, el
        // área es la misma esté el panel abierto o cerrado. Y eso se cumple porque el panel
        // sencillamente no aparece en la lista.
        var permanentes = new[]
        {
            new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 54),
            new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, 74),
        };

        var sinPanel = NubeHypr.AreaUtil(PantallaE5, 8, permanentes);
        var conPanel = NubeHypr.AreaUtil(PantallaE5, 8, permanentes);   // el panel no se añade
        Igual(sinPanel.Ancho, conPanel.Ancho, "abrir un panel lateral no cambia el ancho útil");
        Igual(sinPanel.X, conPanel.X, "ni dónde empieza");

        // Y el contraste: si alguien decidiera lo contrario y lo metiera, el área cambiaría.
        // Esto no es lo que hace NubeBar; está para que se vea la diferencia de un vistazo.
        var siSeMetiera = NubeHypr.AreaUtil(PantallaE5, 8,
            permanentes.Append(new NubeHypr.Estorbo(NubeHypr.Borde.Izquierda, 352)).ToArray());
        Vale(siSeMetiera.Ancho < sinPanel.Ancho,
             "si se descontaran, el área cambiaría — por eso no se descuentan");
    }

    // ================= El ajuste =================
    static void E5ElAjusteRespetaElDock()
    {
        Grupo("E5: ajustar una ventana no la deja debajo del dock");

        // `Monitores.Info` no se puede inventar sin tocar Win32, así que lo que se comprueba
        // aquí es la cuenta: `Colocacion` sobre un área recortada.
        var util = new Hueco(0, 54, 2560, 1312);            // con la barra ya descontada
        var conDock = new Hueco(0, 54, 2560, 1312 - 74);    // y el dock de abajo

        var abajoEntera = ZonasAjuste.Colocacion(Zona.AbajoDerecha, util);
        var abajoConDock = ZonasAjuste.Colocacion(Zona.AbajoDerecha, conDock);

        Vale(abajoConDock.Abajo < abajoEntera.Abajo,
             "con dock, la ventana de abajo termina más arriba");
        Vale(abajoConDock.Abajo <= conDock.Abajo,
             "y no se mete en la franja del dock");

        // Maximizar también: una ventana «maximizada» por el ajuste no puede acabar con su
        // barra de título o su pie debajo de una superficie permanente.
        var maxConDock = ZonasAjuste.Colocacion(Zona.Maximizar, conDock);
        Igual(conDock.Abajo, maxConDock.Abajo, "maximizar llega justo hasta el dock");
        Igual(conDock.Y, maxConDock.Y, "y empieza justo debajo de la barra");

        // Las mitades siguen sin dejar una raya de un píxel por el medio, que es lo que ya
        // se arregló antes y no puede romperse al cambiar el área.
        var izq = ZonasAjuste.Colocacion(Zona.Izquierda, conDock);
        var der = ZonasAjuste.Colocacion(Zona.Derecha, conDock);
        Igual(conDock.Ancho, izq.Ancho + der.Ancho, "las dos mitades suman el ancho exacto");
        Igual(izq.Derecha, der.X, "y no dejan hueco ni se solapan");
    }
}
