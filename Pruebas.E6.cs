using System;
using System.Linq;

namespace NubeBar;

// E6: las pruebas de las transiciones.
//
// «Breves y cancelables» es una promesa, y una promesa se comprueba. En una captura una
// animación de 900 ms y una de 180 se parecen; aquí no.
public static partial class Pruebas
{
    internal static void CorrerE6()
    {
        E6TodasSonBrevesYRepresentanAlgo();
        E6ElNivelDeAnimacionManda();
        E6LaNubeNoSaleEnTodas();
    }

    static void E6TodasSonBrevesYRepresentanAlgo()
    {
        Grupo("E6: las transiciones son breves y representan un cambio real");

        // Todas las del enum tienen su paso: una transición sin definir se usaría con una
        // duración inventada en el sitio donde se llame.
        foreach (var q in Enum.GetValues<Transiciones.Que>())
        {
            var p = Transiciones.De(q);
            Vale(p != null, $"{q} está definida");
            Vale(p!.Ms > 0, $"{q} dura algo");
            Vale(p.Ms <= Transiciones.TopeMs,
                 $"{q} no pasa del tope de {Transiciones.TopeMs:0} ms ({p.Ms:0})");
            Vale(p.Porque.Length > 40, $"{q} explica POR QUÉ existe y por qué dura eso");
        }

        Igual(Enum.GetValues<Transiciones.Que>().Length, Transiciones.Todas.Length,
              "no hay pasos de más ni de menos");
        Igual(Transiciones.Todas.Length, Transiciones.Todas.Select(p => p.Cual).Distinct().Count(),
              "y ninguno repetido");

        // ---------- Salir es más rápido que entrar ----------
        //
        // Lo que se va ya no tienes que mirarlo.
        Vale(Transiciones.De(Transiciones.Que.PanelSale).Ms <
             Transiciones.De(Transiciones.Que.PanelEntra).Ms,
             "cerrar un panel es más rápido que abrirlo");

        // ---------- La previsualización, la más rápida de las que importan ----------
        //
        // Llega mientras arrastras: una entrada lenta la enseña cuando ya has soltado.
        Vale(Transiciones.De(Transiciones.Que.PreviaDeAjuste).Ms <=
             Transiciones.De(Transiciones.Que.CambioDeEscritorio).Ms,
             "la previsualización entra antes que un cambio de escritorio");

        // ---------- Cancelables ----------
        //
        // Casi todas. La de cerrar la composición no lo es a propósito, y esa excepción
        // tiene que seguir siendo UNA.
        var noCancelables = Transiciones.Todas.Where(p => !p.Cancelable).ToList();
        Igual(1, noCancelables.Count, "solo hay una transición no cancelable");
        Igual(Transiciones.Que.ComposicionSale, noCancelables[0].Cual,
              "y es la de cerrar: cortarla dejaría superficies a medio desmontar");

        foreach (var q in new[] { Transiciones.Que.PanelEntra, Transiciones.Que.CambioDeEscritorio,
                                  Transiciones.Que.PreviaDeAjuste, Transiciones.Que.CambioDeFoco })
            Vale(Transiciones.De(q).Cancelable, $"{q} se puede cancelar a medias");

        // La línea del registro nombra la transición y su duración.
        foreach (var q in Enum.GetValues<Transiciones.Que>())
            Vale(Transiciones.Linea(q).Contains(q.ToString()), $"la línea de {q} la nombra");
    }

    static void E6ElNivelDeAnimacionManda()
    {
        Grupo("E6: apagar las animaciones las apaga de verdad");

        var antes = Animacion.Nivel.ToString().ToLowerInvariant();
        try
        {
            // ---------- Desactivado: cero, no «rápido» ----------
            //
            // Es la diferencia entre respetar la preferencia y hacer como que se respeta.
            Animacion.Elegir("desactivado");
            foreach (var q in Enum.GetValues<Transiciones.Que>())
                Igual(TimeSpan.Zero, Transiciones.Duracion(q),
                      $"con las animaciones apagadas, {q} dura cero");

            // Y entonces no se anima nada, aunque el estado haya cambiado.
            Vale(!Transiciones.TocaAnimar(Transiciones.Que.PanelEntra, 1, 2),
                 "con cero de duración no se anima ni un cambio real");

            // ---------- Completo ----------
            Animacion.Elegir("completo");
            foreach (var q in Enum.GetValues<Transiciones.Que>())
            {
                var d = Transiciones.Duracion(q);
                Vale(d > TimeSpan.Zero, $"con animaciones completas, {q} dura algo");
                Vale(d.TotalMilliseconds <= Transiciones.TopeMs + 1,
                     $"y {q} sigue sin pasar del tope ({d.TotalMilliseconds:0} ms)");
            }

            // ---------- Reducido: menos, pero no cero ----------
            Animacion.Elegir("reducido");
            foreach (var q in Enum.GetValues<Transiciones.Que>())
            {
                var d = Transiciones.Duracion(q);
                Vale(d.TotalMilliseconds <= Transiciones.TopeMs + 1,
                     $"en reducido, {q} tampoco pasa del tope");
            }

            // ---------- Y el estado tiene que haber cambiado ----------
            //
            // La regla de D6, que E6 hereda: relanzar un fundido con el mismo origen y
            // destino es lo que se ve como parpadeo.
            Animacion.Elegir("completo");
            Vale(!Transiciones.TocaAnimar(Transiciones.Que.CambioDeFoco, 7, 7),
                 "sin cambio de estado no se anima");
            Vale(Transiciones.TocaAnimar(Transiciones.Que.CambioDeFoco, 7, 8),
                 "con cambio de estado sí");
        }
        finally { Animacion.Elegir(antes); }
    }

    static void E6LaNubeNoSaleEnTodas()
    {
        Grupo("E6: la nube sale donde significa algo, no en todas");

        // Si saliera en todas, aparecería ocho veces por minuto y dejaría de significar
        // nada: sería una mascota, que es lo que el documento de E prohíbe.
        var conNube = Transiciones.Todas.Where(p => p.ConNube).ToList();
        Vale(conNube.Count > 0, "la nube sale en algunas");
        Vale(conNube.Count < Transiciones.Todas.Length, "pero NO en todas");

        // Donde sí: cambios de NubeBar o de su estado.
        foreach (var q in new[] { Transiciones.Que.CambioDeEscritorio, Transiciones.Que.PanelEntra,
                                  Transiciones.Que.Recuperacion, Transiciones.Que.ComposicionEntra })
            Vale(Transiciones.ConNube(q), $"{q} lleva nube: es un cambio de NubeBar");

        // Donde no: cosas tuyas y del sistema, o lo que se va.
        foreach (var q in new[] { Transiciones.Que.CambioDeFoco, Transiciones.Que.PreviaDeAjuste,
                                  Transiciones.Que.PanelSale, Transiciones.Que.ComposicionSale })
            Vale(!Transiciones.ConNube(q), $"{q} NO lleva nube");

        // ---------- Y nunca una mascota gigante ----------
        //
        // El tope de D6 sigue puesto y E6 no lo levanta. Con una pantalla de 4K disponible, la
        // nube de una transición sigue siendo pequeña.
        foreach (double disponible in new[] { 40.0, 140, 400, 2160 })
        {
            double lado = Transiciones.TamanoDeLaNube(disponible);
            Vale(lado > 0, $"con {disponible:0} px disponibles, la nube mide algo");
            Vale(lado <= disponible, $"y nunca más que el hueco ({lado:0} de {disponible:0})");
            Vale(lado <= 200, $"ni más de 200 px: no es una mascota ({lado:0})");
        }
    }
}
