using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.5: que una regla coloque la ventana, y solo lo que la regla pide.
//
// Lo que se protege aquí es la frase que separa un gestor de ventanas de una molestia:
// **un campo del que ninguna regla opinó no produce ningún efecto**. Si una regla dice solo
// «al escritorio 2», la ventana se va al escritorio 2 y conserva su tamaño, su monitor y su
// sitio. Es lógica pura justamente para poder exigirlo sin ventanas delante.
public static partial class Pruebas
{
    internal static void CorrerColocar()
    {
        ColocarSoloLoQueLaReglaPide();
        ColocarElDestinoEsRazonable();
        ColocarLaPuertaYSusCuatroCandados();
        ColocarElOrdenDeLosEfectos();
        ColocarElLayoutEsOtraCosa();
    }

    // ================= D9.6: el layout mueve a las demás =================
    static void ColocarElLayoutEsOtraCosa()
    {
        Grupo("D9.6: un layout es una decisión sobre el escritorio, no sobre una ventana");

        var v = Ventana();

        // Los cinco campos de la ventana NO disparan un reparto: mover la ventana que la
        // regla nombra es una cosa, recolocar las otras seis es otra muy distinta.
        foreach (var d in new[]
        {
            new HyprReglas.Decision { Espacio = 2 },
            new HyprReglas.Decision { Monitor = 1 },
            new HyprReglas.Decision { Flotante = true },
            new HyprReglas.Decision { Ancho = 0.5, Alto = 0.5 },
            new HyprReglas.Decision { Foco = true },
            new HyprReglas.Decision(),
        })
            Vale(!HyprColocar.LaReglaRepiteElEspacio(d),
                 "una regla sin layout no reparte el escritorio");

        // Y con layout, sí — incluido «Flotante», que es un layout de pleno derecho aunque
        // luego no mueva a nadie. La diferencia la hace quien reparte, no esta pregunta.
        foreach (var l in Enum.GetValues<NubeHypr.Layout>())
            Vale(HyprColocar.LaReglaRepiteElEspacio(new HyprReglas.Decision { Layout = l }),
                 $"una regla con layout «{l}» sí lo reparte");

        // El aviso lleva el NÚMERO delante: «van a moverse 6 ventanas» es lo que hay que
        // poder leer antes de que se muevan, no después.
        string aviso = HyprColocar.LineaDelLayout(NubeHypr.Layout.Horizontal, 6, seAplica: true);
        Vale(aviso.Contains("6"), $"el aviso dice cuántas se mueven ({aviso})");
        Vale(aviso.Contains("ninguna regla nombra"),
             "y avisa de que se mueven ventanas que ninguna regla menciona");
        Vale(aviso.Contains("Horizontal"), "y qué layout se pide");

        string noVa = HyprColocar.LineaDelLayout(NubeHypr.Layout.Vertical, 4, seAplica: false);
        Vale(noVa.Contains("NO se aplica"), "y cuando no se aplica, lo dice");
        Vale(noVa.Contains("4"), "con el número que habría movido");

        // Y lo de siempre: con la capa apagada o en simulación, nada de esto ocurre. El
        // layout no es una puerta trasera que se salte el modo.
        Vale(!HyprColocar.SeAplica(true, NubeHypr.Modo.Simulacion, true, true),
             "en simulación tampoco se reparte");
        Vale(!HyprColocar.SeAplica(false, NubeHypr.Modo.Aplicar, true, true),
             "ni con la capa apagada");
    }

    static readonly HyprColocar.Sitio DosMonitores = new(
        new[]
        {
            new NubeHypr.Rect(0, 38, 2560, 1402),     // principal, con la barra descontada
            new NubeHypr.Rect(2560, 0, 1080, 1920),   // el vertical de al lado
        },
        CuantosEspacios: 3);

    static VentanaModelo Ventana(int espacio = 0, int monitor = 0) =>
        new((IntPtr)0x1234, "discord.exe", "Discord",
            new NubeHypr.Rect(300, 200, 900, 600),
            Escritorio: espacio, Monitor: monitor);

    // ================= Solo lo que la regla pide =================
    static void ColocarSoloLoQueLaReglaPide()
    {
        Grupo("D9.5: una regla mueve lo que dice y nada más");

        var v = Ventana();

        // ---------- Solo el espacio ----------
        var soloEspacio = HyprColocar.Planear(v, new HyprReglas.Decision { Espacio = 2 }, DosMonitores);
        Igual(2, soloEspacio.AlEspacio ?? 0, "«al escritorio 2» manda al escritorio 2");
        Vale(soloEspacio.Destino == null,
             "y NO toca el sitio: nadie ha pedido moverla de donde está");
        Vale(!soloEspacio.Enfocar, "ni el foco");

        // ---------- Solo el foco ----------
        var soloFoco = HyprColocar.Planear(v, new HyprReglas.Decision { Foco = true }, DosMonitores);
        Vale(soloFoco.Enfocar, "«dale el foco» da el foco");
        Vale(soloFoco.Destino == null && soloFoco.AlEspacio == null, "y nada más");

        // Un `Foco = false` NO es «quítale el foco»: es «no te ocupes del foco». Quitárselo
        // a la ventana que el usuario acaba de abrir sería de lo más molesto que puede
        // hacer un gestor.
        var focoNo = HyprColocar.Planear(v, new HyprReglas.Decision { Foco = false }, DosMonitores);
        Vale(!focoNo.Enfocar, "y un «Foco=false» no quita el foco: no hace nada");
        Vale(!focoNo.HaceAlgo, "esa decisión no produce ningún efecto");

        // ---------- Una decisión vacía ----------
        var nada = HyprColocar.Planear(v, new HyprReglas.Decision(), DosMonitores);
        Vale(!nada.HaceAlgo, "una decisión sin campos no hace nada");
        Igual(0, HyprColocar.Efectos(nada, "x").Count, "y no produce efectos");

        // ---------- Flotante y tamaño ----------
        var flotante = HyprColocar.Planear(
            v, new HyprReglas.Decision { Flotante = true, Ancho = 0.5, Alto = 0.5 }, DosMonitores);
        Vale(flotante.Destino != null, "«flotante a media pantalla» sí coloca");
        Vale(flotante.AlEspacio == null, "y no la manda a otro escritorio");
        var r = flotante.Destino!.Value;
        Igual(1280.0, r.Ancho, "la mitad del ancho útil del monitor principal");
        Igual(701.0, r.Alto, "y la mitad del alto útil");

        // ---------- Lo que ya está donde se pide ----------
        var yaEsta = HyprColocar.Planear(Ventana(espacio: 1),
                                         new HyprReglas.Decision { Espacio = 2 }, DosMonitores);
        Vale(yaEsta.AlEspacio == null, "si ya está en el escritorio pedido, no se mueve");
        Vale(yaEsta.Pegas.Any(p => p.Contains("ya está")), "y se dice por qué");
    }

    // ================= El destino es razonable =================
    static void ColocarElDestinoEsRazonable()
    {
        Grupo("D9.5: el destino cae dentro del monitor y se puede agarrar");

        foreach (int mon in new[] { 1, 2 })
            foreach (double frac in new[] { 0.05, 0.25, 0.5, 0.75, 1.0 })
            {
                var p = HyprColocar.Planear(
                    Ventana(), new HyprReglas.Decision { Monitor = mon, Ancho = frac, Alto = frac },
                    DosMonitores);

                Vale(p.Destino != null, $"[monitor {mon}, {frac:0.00}] hay destino");
                if (p.Destino is not { } d) continue;
                var area = DosMonitores.Monitores[mon - 1];

                // Dentro de su monitor, entero. Una ventana medio fuera de la pantalla es
                // peor que no haberla movido.
                Vale(d.X >= area.X - 1 && d.Y >= area.Y - 1,
                     $"[monitor {mon}, {frac:0.00}] no se sale por arriba ni por la izquierda");
                Vale(d.X + d.Ancho <= area.X + area.Ancho + 1 &&
                     d.Y + d.Alto <= area.Y + area.Alto + 1,
                     $"[monitor {mon}, {frac:0.00}] ni por abajo ni por la derecha");

                // Y agarrable.
                Vale(d.Ancho >= HyprColocar.MinimoLado && d.Alto >= HyprColocar.MinimoLado,
                     $"[monitor {mon}, {frac:0.00}] se puede coger con el ratón ({d.Ancho:0}x{d.Alto:0})");
            }

        // Fracciones imposibles: se acotan y se sigue, no se deja una ventana de un píxel
        // ni una de diez mil.
        var enorme = HyprColocar.Planear(Ventana(),
            new HyprReglas.Decision { Ancho = 40, Alto = 40 }, DosMonitores).Destino!.Value;
        Vale(enorme.Ancho <= 2560 && enorme.Alto <= 1402, "una fracción de 40 se acota al monitor");

        var minusculo = HyprColocar.Planear(Ventana(),
            new HyprReglas.Decision { Ancho = -3, Alto = 0.0001 }, DosMonitores).Destino!.Value;
        Vale(minusculo.Ancho >= HyprColocar.MinimoLado && minusculo.Alto >= HyprColocar.MinimoLado,
             "y una negativa o ridícula no deja una ventana inagarrable");

        // Un monitor que no existe: se dice y no se inventa un destino.
        var sinMonitor = HyprColocar.Planear(Ventana(),
            new HyprReglas.Decision { Monitor = 9 }, DosMonitores);
        Vale(sinMonitor.Destino == null, "un monitor que no existe no produce destino");
        Vale(sinMonitor.Pegas.Any(p => p.Contains("monitor 9")), "y la pega lo nombra");

        // Lo mismo con un escritorio que no existe.
        var sinEspacio = HyprColocar.Planear(Ventana(),
            new HyprReglas.Decision { Espacio = 7 }, DosMonitores);
        Vale(sinEspacio.AlEspacio == null, "un escritorio que no existe no se intenta");
        Vale(sinEspacio.Pegas.Any(p => p.Contains("7")), "y se dice cuántos hay");

        // Sin monitores en absoluto no se revienta.
        var vacio = new HyprColocar.Sitio(Array.Empty<NubeHypr.Rect>(), 0);
        var p0 = HyprColocar.Planear(Ventana(), new HyprReglas.Decision { Monitor = 1, Espacio = 1 }, vacio);
        Vale(!p0.HaceAlgo, "sin monitores ni escritorios no se hace nada");
        Vale(p0.Pegas.Count == 2, "y se explican las dos pegas");
    }

    // ================= La puerta =================
    static void ColocarLaPuertaYSusCuatroCandados()
    {
        Grupo("D9.5: las cuatro condiciones para que una regla llegue a mover algo");

        // Con las cuatro bien, se aplica.
        Vale(HyprColocar.SeAplica(true, NubeHypr.Modo.Aplicar, true, true),
             "capa encendida, modo aplicar, regla que coincide y decisión con contenido: se aplica");

        // Y quitando cada una, no — y cada «no» viene con su motivo, que es lo que se
        // escribe en el registro cuando el usuario pregunta por qué no se movió su ventana.
        var casos = new (string Que, bool Capa, NubeHypr.Modo Modo, bool Coincide, bool Dice)[]
        {
            ("la capa está apagada",      false, NubeHypr.Modo.Aplicar,    true,  true),
            ("el modo es simulación",     true,  NubeHypr.Modo.Simulacion, true,  true),
            ("el modo es apagado",        true,  NubeHypr.Modo.Apagado,    true,  true),
            ("ninguna regla coincide",    true,  NubeHypr.Modo.Aplicar,    false, true),
            ("la decisión está vacía",    true,  NubeHypr.Modo.Aplicar,    true,  false),
        };

        foreach (var (que, capa, modo, coincide, dice) in casos)
        {
            Vale(!HyprColocar.SeAplica(capa, modo, coincide, dice), $"no se aplica si {que}");
            string? motivo = HyprColocar.PorQueNoSeAplica(capa, modo, coincide, dice);
            Vale(motivo != null && motivo.Length > 10, $"y lo explica: {motivo}");
        }

        // La que más importa de las cinco: **simulación nunca mueve**. Es el valor de serie
        // y lo que el usuario tiene puesto.
        Vale(!HyprColocar.SeAplica(true, NubeHypr.Modo.Simulacion, true, true),
             "en simulación no se mueve ni con todo lo demás a favor");
        Vale(HyprColocar.PorQueNoSeAplica(true, NubeHypr.Modo.Simulacion, true, true)!
                        .Contains("simulación"),
             "y el motivo dice que es la simulación");

        // Y el margen de espera, que existe para no pelearse con la aplicación.
        Vale(HyprColocar.LoQueTardaEnAsentarse > TimeSpan.Zero,
             "se espera algo antes de colocar una ventana recién abierta");
        Vale(HyprColocar.LoQueTardaEnAsentarse < TimeSpan.FromSeconds(2),
             "pero no tanto como para que se note el salto");
    }

    // ================= El orden de los efectos =================
    static void ColocarElOrdenDeLosEfectos()
    {
        Grupo("D9.5: primero el escritorio, después el sitio, el foco al final");

        var p = HyprColocar.Planear(
            Ventana(),
            new HyprReglas.Decision { Espacio = 2, Monitor = 2, Ancho = 0.5, Alto = 0.5, Foco = true },
            DosMonitores);

        var efectos = HyprColocar.Efectos(p, "regla «Discord»");
        Igual(3, efectos.Count, "tres efectos");
        Igual(TipoEfecto.AEspacio, efectos[0].Tipo, "primero el escritorio");
        Igual(TipoEfecto.Colocar, efectos[1].Tipo, "después el sitio");
        Igual(TipoEfecto.Enfocar, efectos[2].Tipo, "y el foco al final");

        // El orden no es estético: colocar antes de mandar al otro escritorio deja a
        // Windows recolocando una ventana que se está yendo, y enfocar antes de las dos
        // haría saltar la pantalla al escritorio de origen justo antes de sacarla de ahí.
        Vale(efectos.All(e => e.Porque.Contains("Discord")),
             "y todos llevan de dónde vienen, para el registro");
        Vale(efectos.All(e => e.Hwnd == (IntPtr)0x1234), "y la misma ventana");

        // Las líneas del registro dicen lo que va a pasar, o por qué no.
        var lineas = HyprColocar.Lineas(Ventana(), p, null);
        Vale(lineas.Any(l => l.Contains("escritorio 2")), "el registro nombra el escritorio");
        Vale(lineas.Any(l => l.Contains("colocar en")), "y el destino");
        Vale(lineas.Any(l => l.Contains("enfocar")), "y el foco");

        var negadas = HyprColocar.Lineas(Ventana(), p, "el modo es simulación: se calcula y no se mueve");
        Vale(negadas.Any(l => l.Contains("no se aplica")), "y cuando no se aplica, lo dice");
        Vale(negadas.Count == 2, "sin prometer movimientos que no van a ocurrir");
    }
}
