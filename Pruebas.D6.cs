using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace NubeBar;

// D6: las pruebas de la integración visual.
//
// Están en su propio archivo porque `Pruebas.cs` pasó de 7.000 líneas y seguir metiendo
// grupos ahí hace que una edición con `sed` o un patch mal cerrado se lleve por delante
// todo lo demás — ya pasó una vez en esta misma sesión. Un `partial` por fase es más
// barato de revisar y de recuperar.
//
// Todas son PURAS: ni un color se mide sobre un píxel real, ni se abre una ventana, ni se
// mueve nada. Lo que no se puede comprobar así —que el filo se vea de verdad sobre un
// vídeo de Wallpaper Engine— queda anotado como pendiente de prueba manual, no colado como
// verificado.
public static partial class Pruebas
{
    internal static void CorrerD6()
    {
        D6AzulYCoralEnLosSieteTemas();
        D6ContrasteEnClaroOscuroYAltoContraste();
        D6ElFiloDelDestinoSeVeSobreCualquierFondo();
        D6LaNubeNoPuedeCrecerSinLimite();
        D6AnimarSoloConCambioDeEstado();
        D6JerarquiaDeLaVista();
        D6TecladoPorLaRejilla();
        D6LaEleccionSobreviveALosCambios();
        D6LaPreviaNoTapaLaElegida();
        D6CuatroBordesYOrientaciones();
        D6NoRepintarSinMotivo();
        D6IndicadorDeEscritorio();
        D6BuscarYAgruparReglas();
        D6ExplicarLaReglaAplicada();
        D6BuscarYAgruparAtajos();
        D6ElIndicadorBreve();
    }

    // ================= La paleta =================

    static Roles RolesDe(TemaBase t) => Roles.Desde(new Pintura(t));

    static void D6AzulYCoralEnLosSieteTemas()
    {
        Grupo("D6: el azul de foco y el coral de aviso, en los nueve temas");

        HyprPinta.Forzado = false;      // sin alto contraste, para medir la rama normal

        foreach (var t in TemasDeSerie.Lista)
        {
            var r = RolesDe(t);
            var azul = HyprPinta.Azul(r);
            var coral = HyprPinta.Coral(r);

            Vale(HyprPinta.EsAzul(azul), $"{t.Nombre}: el azul de foco es azul de verdad");
            Vale(HyprPinta.EsCoral(coral), $"{t.Nombre}: el coral es coral de verdad");

            // Y lo que de verdad importa: que no se confundan. Si "va a pasar" y "cuidado"
            // salieran del mismo tono, el filo de la previsualización y el botón de quitar
            // una regla se leerían igual.
            double dt = Math.Abs(Contraste.Tono(azul) - Contraste.Tono(coral));
            dt = Math.Min(dt, 360 - dt);
            Vale(dt > 60, $"{t.Nombre}: el azul y el coral no se parecen (separación {dt:0}°)");

            // Como TINTA, los dos tienen que leerse sobre la superficie del panel.
            Vale(Contraste.Entre(HyprPinta.AzulTinta(r, r.SuperficieSolida), r.SuperficieSolida)
                 >= Roles.MinimoSuperficie, $"{t.Nombre}: el azul se lee como texto en un panel");
            Vale(Contraste.Entre(HyprPinta.CoralTinta(r, r.SuperficieSolida), r.SuperficieSolida)
                 >= Roles.MinimoSuperficie, $"{t.Nombre}: y el coral también");
        }

        // ---------- La regla, en vez de dos temas con nombre ----------
        //
        // Esto nombraba a «Nube» (cuyo azul de sistema ES el azul de foco) y a «Gruvbox»
        // (cuyo amarillo no puede hacer de azul, así que cae en el de la casa). Los dos
        // desaparecieron con el rediseño, y reescribir la prueba con dos de los nueve dejaría
        // el mismo problema para la próxima vez.
        //
        // Así que se comprueba la REGLA, que no depende de ningún tema: si el color de
        // sistema del tema es azul de verdad, ése es el azul de foco; si no, el de la casa.
        foreach (var t in TemasDeSerie.Lista)
        {
            var r = RolesDe(t);
            if (HyprPinta.EsAzul(r.Sistema))
                Igual(r.Sistema, HyprPinta.Azul(r), $"{t.Nombre}: su color de sistema es azul, y es el azul de foco");
            else
                Igual(HyprPinta.AzulNube, HyprPinta.Azul(r), $"{t.Nombre}: su color de sistema no es azul, así que se usa el de la casa");
        }

        // Y que las dos ramas se recorren de verdad: con nueve temas que todos cayeran del
        // mismo lado dejaría media regla sin probar y nadie se enteraría.
        Vale(TemasDeSerie.Lista.Any(t => HyprPinta.EsAzul(RolesDe(t).Sistema)),
             "al menos un tema de serie trae un color de sistema azul");
        Vale(TemasDeSerie.Lista.Any(t => !HyprPinta.EsAzul(RolesDe(t).Sistema)),
             "y al menos uno no, que es el que prueba el respaldo");

        HyprPinta.Forzado = null;
    }

    static void D6ContrasteEnClaroOscuroYAltoContraste()
    {
        Grupo("D6: contraste con tema claro, oscuro y alto contraste");

        // ---------- Un tema CLARO ----------
        //
        // Esto se escribía a mano porque ninguno de los temas de antes era claro. Con el
        // rediseño hay cuatro de serie (Oficina, Invierno, Sakura y Otoño), así que se mide
        // sobre uno de verdad: Oficina, que es el claro sobrio.
        //
        // Lo que se comprueba es lo mismo: sobre un fondo casi blanco un azul claro deja de
        // leerse, y la corrección tiene que OSCURECERLO en vez de aclararlo más.
        var temaClaro = TemasDeSerie.De("oficina");
        Vale(temaClaro.Claro, "Oficina es un tema claro, que es lo que esta prueba necesita");
        var rClaro = Roles.Desde(new Pintura(temaClaro));

        var azulClaro = HyprPinta.AzulTinta(rClaro, rClaro.SuperficieSolida);
        Vale(Contraste.Entre(azulClaro, rClaro.SuperficieSolida) >= Roles.MinimoSuperficie,
             "sobre un tema claro, el azul se corrige hasta leerse");
        Vale(Contraste.Luminancia(azulClaro) < Contraste.Luminancia(HyprPinta.AzulNube),
             "y se corrige OSCURECIENDO, que es lo que hace falta sobre claro");

        var coralClaro = HyprPinta.CoralTinta(rClaro, rClaro.SuperficieSolida);
        Vale(Contraste.Entre(coralClaro, rClaro.SuperficieSolida) >= Roles.MinimoSuperficie,
             "y el coral igual");

        // ---------- Alto contraste ----------
        //
        // Lo primero que se cae son los velos: un relleno al 19 % sobre un fondo que el
        // usuario ha pedido plano es exactamente lo que ese ajuste existe para quitar.
        HyprPinta.Forzado = true;
        Igual((byte)0xFF, HyprPinta.Velo(0x30), "con alto contraste un velo se vuelve opaco");
        Igual((byte)0x00, HyprPinta.RellenoDelDestino(rClaro).A,
              "y la previsualización no se rellena: taparía la ventana, que es peor");
        var filoAC = HyprPinta.FiloDelDestino(rClaro, null);
        Vale(filoAC.Total >= 5, $"y el filo engorda (total {filoAC.Total})");

        HyprPinta.Forzado = false;
        Igual((byte)0x30, HyprPinta.Velo(0x30), "sin alto contraste el velo se respeta");
        Vale(HyprPinta.RellenoDelDestino(rClaro).A > 0, "y la previsualización sí se rellena");
        Vale(HyprPinta.RellenoDelDestino(rClaro).A < 0x60,
             "pero flojo: si tapara, dejaría de enseñar dónde va a caer la ventana");

        HyprPinta.Forzado = null;
    }

    static void D6ElFiloDelDestinoSeVeSobreCualquierFondo()
    {
        Grupo("D6: el filo doble se ve sobre fondo claro, oscuro y desconocido");

        HyprPinta.Forzado = false;
        var r = RolesDe(TemasDeSerie.De(TemasDeSerie.Predeterminado));

        // ---------- Sin saber qué hay detrás ----------
        var ciego = HyprPinta.FiloDelDestino(r, null);
        Vale(ciego.GrosorExterior > 0 && ciego.GrosorInterior > 0,
             "el filo tiene dos líneas, no una");
        Vale(ciego.Exterior.A > 0x80, "la exterior es bien opaca: es la que responde sobre claro");

        // ---------- Sobre blanco ----------
        //
        // La clave: una de las dos líneas SIEMPRE contrasta. Es lo que hace que no haya que
        // medir un fondo que con Wallpaper Engine cambia cada fotograma.
        foreach (var fondo in new[] { Colors.White, Colors.Black,
                                      Color.FromRgb(0x80, 0x80, 0x80),
                                      Color.FromRgb(0xF0, 0xE8, 0xD0),
                                      Color.FromRgb(0x10, 0x14, 0x20) })
        {
            var f = HyprPinta.FiloDelDestino(r, fondo);
            double mejor = Math.Max(Contraste.Entre(Aplanado(f.Exterior, fondo), fondo),
                                    Contraste.Entre(f.Interior, fondo));
            Vale(mejor >= HyprPinta.MinimoFilo,
                 $"sobre {fondo}: alguna de las dos líneas se ve (contraste {mejor:0.0})");
        }

        // Sobre un fondo muy oscuro la línea exterior se vuelve CLARA: una línea negra
        // sobre negro no es un borde.
        var sobreNegro = HyprPinta.FiloDelDestino(r, Colors.Black);
        Vale(sobreNegro.Exterior.R > 0x80, "sobre negro, la línea exterior es clara");
        var sobreBlanco = HyprPinta.FiloDelDestino(r, Colors.White);
        Vale(sobreBlanco.Exterior.R < 0x80, "y sobre blanco, oscura");

        HyprPinta.Forzado = null;
    }

    static Color Aplanado(Color c, Color fondo) =>
        c.A == 0xFF ? c : Contraste.Mezclar(fondo, Color.FromRgb(c.R, c.G, c.B), c.A / 255.0);

    static void D6LaNubeNoPuedeCrecerSinLimite()
    {
        Grupo("D6: la nube es protagonista pero no una mascota gigante");

        // El riesgo de una mascota es que crezca. Por eso el tamaño se pide a `HyprPinta`
        // en vez de escribirse en cada sitio, y por eso tiene dos topes.
        Igual(HyprPinta.MinNube, HyprPinta.TamanoDeLaNube(10), "en un hueco minúsculo, el mínimo");
        Igual(HyprPinta.MaxNube, HyprPinta.TamanoDeLaNube(4000),
              "y en una pantalla entera, el máximo: no se come el escritorio");
        Vale(HyprPinta.TamanoDeLaNube(200) <= 200 * HyprPinta.FraccionMaxima + 0.001,
              "en un hueco mediano, una fracción del lado");
        Igual(HyprPinta.MinNube, HyprPinta.TamanoDeLaNube(0), "y un hueco de cero no revienta");
        Igual(HyprPinta.MinNube, HyprPinta.TamanoDeLaNube(double.NaN), "ni un NaN");

        Vale(HyprPinta.MaxNube < 100, "el tope máximo es de verdad un tope, no un adorno");
        Vale(Nubecita.Proporcion() is > 0.3 and < 1.0,
             "y la nube sigue siendo más ancha que alta: la proporción no se ha tocado");
    }

    static void D6AnimarSoloConCambioDeEstado()
    {
        Grupo("D6: una animación necesita un cambio de estado de verdad");

        // Relanzar un fundido con el mismo estado de origen y destino es exactamente lo que
        // se ve como parpadeo, y es lo que pasó en la vista general en C4.1.
        Vale(!HyprPinta.TocaAnimar(3, 3), "mismo estado: no se anima");
        Vale(HyprPinta.TocaAnimar(3, 4), "estado distinto: se anima");
        Vale(!HyprPinta.TocaAnimar("a", "a"), "y con cadenas, igual");

        // Y la duración sale del nivel del usuario, nunca de un número escrito a mano.
        Vale(HyprPinta.Duracion(200).TotalMilliseconds > 0,
             "con animaciones puestas, la duración es mayor que cero");
        Igual(Animacion.EscalarFundido(200), HyprPinta.Duracion(200).TotalMilliseconds,
              "y es exactamente la que dice `Animacion`, no otra");
    }

    // ================= La vista general =================

    static void D6JerarquiaDeLaVista()
    {
        Grupo("D6: la activa del sistema y la elegida con el teclado son dos cosas");

        Igual(HyprVista.Nivel.Activa, HyprVista.NivelDe(true, false, false), "la activa");
        Igual(HyprVista.Nivel.Elegida, HyprVista.NivelDe(false, true, false), "la elegida");
        Igual(HyprVista.Nivel.ActivaYElegida, HyprVista.NivelDe(true, true, false),
              "y las dos a la vez, que es un estado propio");
        Igual(HyprVista.Nivel.Apagada, HyprVista.NivelDe(false, false, true), "la minimizada");
        Igual(HyprVista.Nivel.Normal, HyprVista.NivelDe(false, false, false), "y una cualquiera");

        // Elegida manda sobre minimizada: puedes recorrer con las flechas una minimizada, y
        // si al elegirla se quedara apagada, el cursor del teclado desaparecería.
        Igual(HyprVista.Nivel.Elegida, HyprVista.NivelDe(false, true, true),
              "una minimizada elegida se ve como elegida");

        // ---------- El grosor NO cambia nunca ----------
        //
        // Es la prueba que impide que vuelva el temblor: con el grosor cambiando entre 0 y
        // 1,4 según el foco, marcar una tarjeta movía su contenido un píxel.
        var grosores = Enum.GetValues<HyprVista.Nivel>()
                           .Select(n => HyprVista.PintaDe(n).Grosor).Distinct().ToList();
        Igual(1, grosores.Count, "todos los niveles tienen el MISMO grosor de filo");
        Igual(HyprVista.GrosorDelFilo, grosores[0], "y es el de siempre");

        // El halo solo lo lleva la elegida, y el filo de acento solo la activa.
        Vale(HyprVista.PintaDe(HyprVista.Nivel.Elegida).Halo, "la elegida lleva halo");
        Vale(!HyprVista.PintaDe(HyprVista.Nivel.Elegida).FiloDeAcento, "y no filo de acento");
        Vale(HyprVista.PintaDe(HyprVista.Nivel.Activa).FiloDeAcento, "la activa lleva filo");
        Vale(!HyprVista.PintaDe(HyprVista.Nivel.Activa).Halo, "y no halo");
        Vale(HyprVista.PintaDe(HyprVista.Nivel.ActivaYElegida) is { Halo: true, FiloDeAcento: true },
             "y la que es las dos cosas, las dos");

        // ---------- La atenuación de una minimizada ----------
        //
        // Solo la minimizada se atenúa, y no hasta desaparecer: se aplica al HUECO de la
        // foto y no al marco entero, porque atenuar el marco baja también el título y un
        // 0,62 sobre texto se cae por debajo del contraste mínimo. Lo que tiene que verse
        // apagado es la imagen, no el nombre de la ventana.
        Igual(1.0, HyprVista.PintaDe(HyprVista.Nivel.Normal).Opacidad, "una normal no se atenúa");
        Igual(1.0, HyprVista.PintaDe(HyprVista.Nivel.Activa).Opacidad, "ni la activa");
        Igual(1.0, HyprVista.PintaDe(HyprVista.Nivel.Elegida).Opacidad, "ni la elegida");
        var apagada = HyprVista.PintaDe(HyprVista.Nivel.Apagada);
        Vale(apagada.Opacidad < 1, "la minimizada sí");
        Vale(apagada.Opacidad >= 0.5,
             $"pero no tanto que no se vea ({apagada.Opacidad:0.##}): sigue siendo una ventana tuya");

        // Y el estado en palabras: el color no puede ser el único portador.
        Igual("activa", HyprVista.PintaDe(HyprVista.Nivel.Activa).Estado, "se dice en palabras");
        Igual("elegida", HyprVista.PintaDe(HyprVista.Nivel.Elegida).Estado, "y la elegida también");
        Igual("minimizada", HyprVista.PintaDe(HyprVista.Nivel.Apagada).Estado, "y la minimizada");
        Igual("", HyprVista.PintaDe(HyprVista.Nivel.Normal).Estado, "una normal no dice nada");
    }

    static void D6TecladoPorLaRejilla()
    {
        Grupo("D6: las flechas recorren la rejilla y no dan la vuelta");

        // Rejilla de 3 columnas y 7 tarjetas: dos filas llenas y una corta.
        const int n = 7, c = 3;

        Igual(1, HyprVista.Siguiente(0, n, c, HyprVista.Mover.Derecha), "derecha avanza");
        Igual(0, HyprVista.Siguiente(1, n, c, HyprVista.Mover.Izquierda), "izquierda retrocede");
        Igual(3, HyprVista.Siguiente(0, n, c, HyprVista.Mover.Abajo), "abajo baja una fila");
        Igual(0, HyprVista.Siguiente(3, n, c, HyprVista.Mover.Arriba), "arriba sube una fila");

        // ---------- Los bordes: se queda, no da la vuelta ----------
        //
        // Con vuelta, mantener pulsada una flecha recorre la rejilla en bucle y nunca se
        // para en un borde: no hay forma de saber que has llegado al final sin mirar.
        Igual(0, HyprVista.Siguiente(0, n, c, HyprVista.Mover.Izquierda),
              "en la primera columna, izquierda se queda");
        Igual(2, HyprVista.Siguiente(2, n, c, HyprVista.Mover.Derecha),
              "en la última columna, derecha se queda");
        Igual(1, HyprVista.Siguiente(1, n, c, HyprVista.Mover.Arriba),
              "en la primera fila, arriba se queda");
        Igual(6, HyprVista.Siguiente(6, n, c, HyprVista.Mover.Abajo),
              "en la última, abajo se queda");

        // La fila corta: abajo desde el 4 saldría al 7, que no existe.
        Igual(4, HyprVista.Siguiente(4, n, c, HyprVista.Mover.Abajo),
              "abajo no se sale por la fila corta");
        Igual(6, HyprVista.Siguiente(3, n, c, HyprVista.Mover.Abajo),
              "pero sí baja cuando hay sitio");
        Igual(6, HyprVista.Siguiente(6, n, c, HyprVista.Mover.Derecha),
              "y derecha tampoco se sale al final de la fila corta");

        Igual(0, HyprVista.Siguiente(5, n, c, HyprVista.Mover.Principio), "Inicio va al primero");
        Igual(n - 1, HyprVista.Siguiente(0, n, c, HyprVista.Mover.Final), "Fin va al último");

        // ---------- Sin nada elegido, la primera flecha elige ----------
        //
        // Media accesibilidad está aquí: si la primera pulsación se perdiera, habría que
        // pulsar dos veces para empezar y la vista parecería que no responde al teclado.
        Igual(0, HyprVista.Siguiente(-1, n, c, HyprVista.Mover.Derecha), "derecha elige la primera");
        Igual(0, HyprVista.Siguiente(-1, n, c, HyprVista.Mover.Abajo), "abajo también");
        Igual(n - 1, HyprVista.Siguiente(-1, n, c, HyprVista.Mover.Izquierda),
              "e izquierda elige la última, que es lo que uno espera");
        Igual(n - 1, HyprVista.Siguiente(-1, n, c, HyprVista.Mover.Arriba), "y arriba igual");

        // ---------- Degenerados ----------
        Igual(-1, HyprVista.Siguiente(0, 0, c, HyprVista.Mover.Derecha), "sin tarjetas, nada");
        Igual(0, HyprVista.Siguiente(0, 1, 1, HyprVista.Mover.Derecha), "con una sola, se queda");
        Igual(0, HyprVista.Siguiente(0, 3, 0, HyprVista.Mover.Derecha),
              "cero columnas no revienta: se trata como una");

        // Y nunca se sale del rango, para cualquier combinación
        for (int i = 0; i < n; i++)
            foreach (var m in Enum.GetValues<HyprVista.Mover>())
            {
                int d = HyprVista.Siguiente(i, n, c, m);
                Vale(d >= 0 && d < n, $"desde {i} con {m} se queda dentro (dio {d})");
            }
    }

    static void D6LaEleccionSobreviveALosCambios()
    {
        Grupo("D6: la elección sobrevive a que la lista cambie debajo");

        var a = (IntPtr)1; var b = (IntPtr)2; var c = (IntPtr)3;
        var antes = new List<IntPtr> { a, b, c };

        // Sigue viva, aunque se haya movido de sitio
        Igual(0, HyprVista.Recolocar(b, antes, new List<IntPtr> { b, a, c }),
              "si la elegida sigue abierta, la elección se queda en ELLA");

        // Se cerró: la elección se queda en su hueco, no salta al principio. Es lo que uno
        // espera al ir cerrando tarjetas en fila.
        Igual(1, HyprVista.Recolocar(b, antes, new List<IntPtr> { a, c }),
              "si se cerró, la elección se queda en su hueco");

        // Se cerró la última: se queda en la nueva última, no fuera de rango
        Igual(1, HyprVista.Recolocar(c, antes, new List<IntPtr> { a, b }),
              "y si era la última, en la nueva última");

        Igual(-1, HyprVista.Recolocar(b, antes, new List<IntPtr>()),
              "sin ventanas no hay elección");
        Igual(0, HyprVista.Recolocar(IntPtr.Zero, antes, new List<IntPtr> { a, b }),
              "sin elección previa, la primera");
    }

    static void D6LaPreviaNoTapaLaElegida()
    {
        Grupo("D6: la previsualización y el menú no tapan la ventana elegida");

        var area = new NubeHypr.Rect(0, 0, 1920, 1040);

        // ---------- Una ventana a la izquierda: el menú se va a la derecha ----------
        var izq = new NubeHypr.Rect(40, 100, 600, 500);
        var s = HyprVista.DondeCabe(izq, area, 300, 480);
        Vale(!s.Solapa, "cabe sin solapar");
        Vale(!HyprVista.Solapan(s.Donde, izq), "y de hecho no solapa, medido");
        Igual("derecha", s.Lado, "se va al lado que tiene hueco");

        // ---------- Y al contrario ----------
        var der = new NubeHypr.Rect(1300, 100, 580, 500);
        var s2 = HyprVista.DondeCabe(der, area, 300, 480);
        Vale(!HyprVista.Solapan(s2.Donde, der), "con la ventana a la derecha, tampoco solapa");
        Igual("izquierda", s2.Lado, "y se va a la izquierda");

        // ---------- Dentro del área, siempre ----------
        foreach (var caso in new[] { izq, der, new NubeHypr.Rect(700, 300, 500, 400) })
        {
            var r = HyprVista.DondeCabe(caso, area, 300, 480).Donde;
            Vale(r.X >= area.X - 0.001 && r.Y >= area.Y - 0.001 &&
                 r.X + r.Ancho <= area.X + area.Ancho + 0.001 &&
                 r.Y + r.Alto <= area.Y + area.Alto + 0.001,
                 $"el sitio elegido cae dentro del área ({r.X:0},{r.Y:0} {r.Ancho:0}x{r.Alto:0})");
        }

        // ---------- Cuando no cabe: se dice, no se miente ----------
        //
        // Una ventana que ocupa casi toda la pantalla no deja hueco por ningún lado. Lo
        // honesto es colocar en el mejor sitio y AVISAR de que solapa, para que quien pinte
        // pueda bajar la opacidad en vez de tapar a ciegas.
        var casiTodo = new NubeHypr.Rect(20, 20, 1880, 1000);
        var apretado = HyprVista.DondeCabe(casiTodo, area, 300, 480);
        Vale(apretado.Solapa, "si no cabe por ningún lado, se avisa de que solapa");
        Vale(apretado.Lado.Length > 0, "y aun así se devuelve un lado, no una excepción");

        // ---------- Degenerados ----------
        var cero = HyprVista.DondeCabe(izq, area, 0, 0);
        Vale(cero.Donde.Ancho >= 1 && cero.Donde.Alto >= 1, "un tamaño cero se sube a uno");
    }

    static void D6CuatroBordesYOrientaciones()
    {
        Grupo("D6: los cuatro bordes y las dos orientaciones");

        // El área útil cambia según dónde esté la isla. Lo que se comprueba aquí es que la
        // colocación del menú y de la previsualización sigue cayendo dentro del área ÚTIL
        // en los cuatro casos: con la isla arriba, el menú no puede salir por encima.
        const double pantallaW = 1920, pantallaH = 1080, barra = 48;

        var areas = new (string Borde, NubeHypr.Rect Area)[]
        {
            ("arriba",    new NubeHypr.Rect(0, barra, pantallaW, pantallaH - barra)),
            ("abajo",     new NubeHypr.Rect(0, 0, pantallaW, pantallaH - barra)),
            ("izquierda", new NubeHypr.Rect(barra, 0, pantallaW - barra, pantallaH)),
            ("derecha",   new NubeHypr.Rect(0, 0, pantallaW - barra, pantallaH)),
        };

        foreach (var (borde, area) in areas)
        {
            var elegida = new NubeHypr.Rect(area.X + 60, area.Y + 80, 520, 420);
            var s = HyprVista.DondeCabe(elegida, area, 300, 460);

            Vale(s.Donde.Y >= area.Y - 0.001,
                 $"isla {borde}: el menú no se sale por arriba del área útil");
            Vale(s.Donde.Y + s.Donde.Alto <= area.Y + area.Alto + 0.001,
                 $"isla {borde}: ni por abajo");
            Vale(s.Donde.X >= area.X - 0.001, $"isla {borde}: ni por la izquierda");
            Vale(s.Donde.X + s.Donde.Ancho <= area.X + area.Ancho + 0.001,
                 $"isla {borde}: ni por la derecha");
            Vale(!HyprVista.Solapan(s.Donde, elegida), $"isla {borde}: y no tapa la elegida");
        }

        // ---------- El indicador: en vertical no cabe el rótulo ----------
        //
        // Una columna de 30 px no tiene dónde poner "2/4 · Trabajo". El rótulo se cae solo,
        // y eso se decide aquí y no en el pintado, para que no haya dos verdades.
        Igual(HyprIndicador.Talla.Minimo, HyprIndicador.TallaQueCabe(30, vertical: true),
              "en una barra vertical estrecha, solo el número");
        Igual(HyprIndicador.Talla.Corto, HyprIndicador.TallaQueCabe(60, vertical: true),
              "y en una vertical ancha, el número y el total");
        Vale(HyprIndicador.TallaQueCabe(2000, vertical: true) != HyprIndicador.Talla.Completo,
             "en vertical NUNCA cabe el nombre, por mucho alto que haya");

        Igual(HyprIndicador.Talla.Completo, HyprIndicador.TallaQueCabe(400, vertical: false),
              "en horizontal con sitio, todo");
        Igual(HyprIndicador.Talla.Medio, HyprIndicador.TallaQueCabe(130, vertical: false),
              "con menos sitio, se cae el total antes que el nombre");
        Igual(HyprIndicador.Talla.Minimo, HyprIndicador.TallaQueCabe(20, vertical: false),
              "y sin sitio, el número y nada más");

        // El orden de lo que se cae es una decisión, no un efecto: primero el total, luego
        // el nombre, y el número nunca.
        var f = new HyprIndicador.Ficha(2, "Trabajo", true, 3);
        foreach (var t in Enum.GetValues<HyprIndicador.Talla>())
            Vale(HyprIndicador.Rotulo(f, 4, t).Contains('2'),
                 $"con la talla {t} el número sigue estando");
    }

    static void D6NoRepintarSinMotivo()
    {
        Grupo("D6: con CPU alta y muchas ventanas no se repinta en bucle");

        Vale(!HyprVista.TocaRepintar("abc", "abc"), "la misma huella no repinta");
        Vale(HyprVista.TocaRepintar("abc", "abd"), "una distinta sí");

        // El acelerador: treinta avisos en un segundo se convierten en un repintado.
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0);
        Vale(!HyprVista.YaTocaOtraVez(t0, t0.AddMilliseconds(10)),
             "a los 10 ms todavía no toca");
        Vale(HyprVista.YaTocaOtraVez(t0, t0.AddMilliseconds(HyprVista.MsEntreRepintados)),
             "y al llegar al umbral, sí");
        Vale(HyprVista.MsEntreRepintados >= 60 && HyprVista.MsEntreRepintados <= 300,
             "el umbral es un valor razonable, ni un fotograma ni medio segundo");

        // Y la huella de una lista larga sigue siendo determinista: el mismo estado da la
        // misma cadena, que es lo que hace que la comparación sirva para algo.
        var muchas = Enumerable.Range(0, 200).Select(i => "v" + i).ToList();
        Igual(HyprVista.Huella(muchas), HyprVista.Huella(muchas),
              "la huella de 200 ventanas es estable");
        Vale(HyprVista.Huella(muchas) != HyprVista.Huella(muchas.Take(199)),
             "y cambia si cambia la lista");
    }

    // ================= El indicador de escritorio =================

    static void D6IndicadorDeEscritorio()
    {
        Grupo("D6: el indicador de escritorio dice nombre, número y estado");

        var conNombre = new HyprIndicador.Ficha(2, "Trabajo", true, 5);
        var sinNombre = new HyprIndicador.Ficha(3, "Escritorio 3", false, 0);

        Vale(conNombre.TieneNombre, "un nombre propio se reconoce");

        // ---------- El caso que se escapa fácil ----------
        //
        // `HyprEspacios.NombreVisible` nunca devuelve vacío: un escritorio sin nombre sale
        // como "Escritorio 3". Si "tiene nombre" mirara solo si la cadena está vacía, el
        // rótulo diría "3/4 · Escritorio 3" — el número tres veces y nada nuevo.
        Vale(!sinNombre.TieneNombre, "y el nombre por defecto NO cuenta como nombre propio");
        Vale(!new HyprIndicador.Ficha(1, "escritorio 1", true, 0).TieneNombre,
             "sin distinguir mayúsculas");
        Vale(new HyprIndicador.Ficha(1, "Escritorio 7", true, 0).TieneNombre,
             "pero «Escritorio 7» en el escritorio 1 sí es un nombre que alguien puso");

        Igual("2/4 · Trabajo", HyprIndicador.Rotulo(conNombre, 4, HyprIndicador.Talla.Completo),
              "el rótulo completo");
        Igual("2 · Trabajo", HyprIndicador.Rotulo(conNombre, 4, HyprIndicador.Talla.Medio),
              "el medio se queda con el nombre y suelta el total");
        Igual("2/4", HyprIndicador.Rotulo(conNombre, 4, HyprIndicador.Talla.Corto), "el corto");
        Igual("2", HyprIndicador.Rotulo(conNombre, 4, HyprIndicador.Talla.Minimo), "y el mínimo");
        Igual("3/4", HyprIndicador.Rotulo(sinNombre, 4, HyprIndicador.Talla.Completo),
              "sin nombre propio, el completo no repite el número");

        // Un nombre larguísimo se recorta, no se come la isla
        var largo = new HyprIndicador.Ficha(1, new string('x', 80), true, 1);
        var rot = HyprIndicador.Rotulo(largo, 2, HyprIndicador.Talla.Completo);
        Vale(rot.Length < 30, $"un nombre de 80 letras se recorta ({rot.Length} caracteres)");
        Vale(rot.EndsWith('…'), "y se ve que está recortado");

        // ---------- Los tres estados ----------
        Igual(HyprIndicador.Estado.Vacio, sinNombre.Como, "cero ventanas: vacío");
        Igual(HyprIndicador.Estado.ConAlgo, new HyprIndicador.Ficha(1, "a", false, 2).Como,
              "dos ventanas: con algo");
        Igual(HyprIndicador.Estado.Lleno, conNombre.Como, "cinco: lleno");
        Igual(0.0, HyprIndicador.PuntoDe(HyprIndicador.Estado.Vacio),
              "el vacío no pinta puntito");
        Vale(HyprIndicador.PuntoDe(HyprIndicador.Estado.Lleno) >
             HyprIndicador.PuntoDe(HyprIndicador.Estado.ConAlgo),
             "y el estado va por TAMAÑO: el color ya lo ocupa «cuál es el actual»");

        // ---------- Lo que oye un lector de pantalla ----------
        var leido = HyprIndicador.ParaLeer(conNombre, 4);
        Vale(leido.Contains("2 de 4"), "se lee el número y el total");
        Vale(leido.Contains("Trabajo"), "y el nombre");
        Vale(leido.Contains("el actual"), "y que es el actual");
        Vale(leido.Contains("5 ventanas"), "y cuántas ventanas tiene, en palabras");
        Vale(HyprIndicador.ParaLeer(sinNombre, 4).Contains("vacío"), "y «vacío» cuando lo está");
        Vale(HyprIndicador.ParaLeer(new HyprIndicador.Ficha(1, "a", false, 1), 2).Contains("1 ventana"),
             "una sola ventana se dice en singular");

        // ---------- Contar las ventanas de cada escritorio ----------
        var espacios = new List<HyprEspacios.Espacio>
        {
            new(Guid.NewGuid(), "Casa", 0, true),
            new(Guid.NewGuid(), "Escritorio 2", 1, false),
        };
        var mapa = new Dictionary<IntPtr, int>
        {
            [(IntPtr)1] = 1, [(IntPtr)2] = 1, [(IntPtr)3] = 2,
        };
        var fichas = HyprIndicador.Fichas(espacios, mapa);
        Igual(2, fichas.Count, "una ficha por escritorio");
        Igual(2, fichas[0].Ventanas, "el primero tiene dos ventanas");
        Igual(1, fichas[1].Ventanas, "y el segundo una");
        Vale(fichas[0].EsElActual, "y se sabe cuál es el actual");

        // Una ventana en un escritorio que no está en la lista no se cuenta en ninguno: un
        // número de más sería peor que un número de menos.
        var raras = HyprIndicador.Fichas(espacios, new Dictionary<IntPtr, int> { [(IntPtr)9] = 44 });
        Igual(0, raras.Sum(f => f.Ventanas), "una ventana de un escritorio desconocido no se cuenta");

        // ---------- La huella: qué entra y qué no ----------
        var h1 = HyprIndicador.Huella(fichas, HyprIndicador.Talla.Completo);
        Igual(h1, HyprIndicador.Huella(fichas, HyprIndicador.Talla.Completo), "estable");
        Vale(h1 != HyprIndicador.Huella(fichas, HyprIndicador.Talla.Corto),
             "cambiar de talla repinta: el rótulo es otro");

        var renombrado = HyprIndicador.Fichas(
            new List<HyprEspacios.Espacio>
            {
                new(espacios[0].Id, "Oficina", 0, true),
                espacios[1],
            }, mapa);
        Vale(h1 != HyprIndicador.Huella(renombrado, HyprIndicador.Talla.Completo),
             "renombrar un escritorio SÍ repinta, que es lo que antes no pasaba");

        // Cruzar un UMBRAL de estado sí repinta: el puntito cambia de tamaño. De 1 a 2
        // ventanas no, porque las dos son "con algo"; de 3 a 4 sí, porque ahí empieza
        // "lleno". Es la diferencia entre repintar por un dato nuevo y repintar por ruido.
        var sigueConAlgo = HyprIndicador.Fichas(espacios, new Dictionary<IntPtr, int>
        {
            [(IntPtr)1] = 1, [(IntPtr)2] = 1, [(IntPtr)3] = 2, [(IntPtr)4] = 2,
        });
        Igual(h1, HyprIndicador.Huella(sigueConAlgo, HyprIndicador.Talla.Completo),
              "de 1 a 2 ventanas no repinta: las dos son «con algo»");

        var llenoDeVerdad = HyprIndicador.Fichas(espacios, new Dictionary<IntPtr, int>
        {
            [(IntPtr)1] = 1, [(IntPtr)2] = 1, [(IntPtr)3] = 2, [(IntPtr)4] = 2,
            [(IntPtr)5] = 2, [(IntPtr)6] = 2,
        });
        Igual(HyprIndicador.Estado.Lleno, llenoDeVerdad[1].Como, "cuatro ventanas es «lleno»");
        Vale(h1 != HyprIndicador.Huella(llenoDeVerdad, HyprIndicador.Talla.Completo),
             "y pasar de «con algo» a «lleno» SÍ repinta");

        // Y lo que NO tiene que repintar: una ventana más dentro del mismo tramo de estado.
        var unaMas = HyprIndicador.Fichas(espacios, new Dictionary<IntPtr, int>
        {
            [(IntPtr)1] = 1, [(IntPtr)2] = 1, [(IntPtr)5] = 1, [(IntPtr)3] = 2,
        });
        Igual(HyprIndicador.Huella(unaMas, HyprIndicador.Talla.Completo),
              HyprIndicador.Huella(
                  HyprIndicador.Fichas(espacios, new Dictionary<IntPtr, int>
                  {
                      [(IntPtr)1] = 1, [(IntPtr)2] = 1, [(IntPtr)6] = 1, [(IntPtr)3] = 2,
                  }), HyprIndicador.Talla.Completo),
              "cambiar QUÉ ventana es, sin cambiar el estado, no repinta");
    }

    // ================= El panel de reglas y atajos =================

    static void D6BuscarYAgruparReglas()
    {
        Grupo("D6: buscar y agrupar reglas");

        var reglas = new List<HyprReglas.Regla>
        {
            R("nav-espacio", prio: 5, proc: "firefox", espacio: 2),
            R("nav-tamano", proc: "firefox", ancho: 0.8),
            R("editor", proc: "code", layout: NubeHypr.Layout.Principal),
            R("por-clase", clase: "CabinetWClass", flotante: true),
            R("apagada", proc: "steam", espacio: 4, activa: false),
            R("vacia"),
        };

        // ---------- Buscar ----------
        Igual(6, HyprCatalogo.Buscar(reglas, "").Count, "sin búsqueda, todas");
        Igual(2, HyprCatalogo.Buscar(reglas, "firefox").Count, "por proceso");
        Igual(1, HyprCatalogo.Buscar(reglas, "cabinet").Count, "por clase, sin mayúsculas");
        Igual(1, HyprCatalogo.Buscar(reglas, "espacio 2").Count,
              "y por lo que la regla HACE, no solo por sus criterios");
        Igual(1, HyprCatalogo.Buscar(reglas, "principal").Count, "por layout");
        Igual(1, HyprCatalogo.Buscar(reglas, "flotante").Count, "por flotante");
        Igual(0, HyprCatalogo.Buscar(reglas, "nohaynada").Count, "y lo que no está, no sale");

        // Acentos: el mismo normalizador que el buscador de aplicaciones. Dos
        // normalizadores distintos serían la forma de que algo se encuentre en un sitio y
        // no en el otro.
        var conAcento = new List<HyprReglas.Regla> { R("musica", titulo: "Música") };
        Igual(1, HyprCatalogo.Buscar(conAcento, "musica").Count, "«musica» encuentra «Música»");
        Igual(1, HyprCatalogo.Buscar(conAcento, "MÚSICA").Count, "y al revés");

        Igual(5, HyprCatalogo.Buscar(reglas, "", soloActivas: true).Count,
              "el filtro de solo activas deja fuera la apagada");

        // ---------- Agrupar ----------
        var grupos = HyprCatalogo.Agrupar(reglas);
        Vale(grupos.Any(g => g.Nombre == "firefox"), "hay un grupo por proceso");
        Igual(2, grupos.First(g => g.Nombre == "firefox").Reglas.Count, "con sus dos reglas");
        Vale(grupos.Any(g => g.Nombre == "clase: CabinetWClass"), "y uno por clase");

        // El grupo de las que no miran nada va al final: es una bandeja de problemas, no un
        // grupo más. Una regla sin criterios no se aplica nunca y suele ser un olvido.
        Igual(HyprCatalogo.GrupoSinCriterios, grupos[^1].Nombre,
              "el grupo «sin criterios» va el último");

        // Dentro de un grupo, el MISMO orden con el que el motor evalúa. Si el panel las
        // ordenara de otra forma, la de arriba no sería la que gana.
        var fx = grupos.First(g => g.Nombre == "firefox");
        Igual("nav-espacio", fx.Reglas[0].Id, "dentro del grupo, primero la de más prioridad");

        // El resumen dice cuántas hay y, si alguna está apagada, cuántas: una regla apagada
        // que no se ve es la causa número uno de "mi regla no funciona".
        var steam = HyprCatalogo.Agrupar(reglas).First(g => g.Nombre == "steam");
        Vale(steam.Resumen.Contains("apagada"), "el resumen avisa de las apagadas");
        Igual(0, steam.Activas, "y las cuenta bien");
        Vale(!fx.Resumen.Contains("apagada"), "y no lo dice cuando no hay ninguna");

        Igual(0, HyprCatalogo.Agrupar(new List<HyprReglas.Regla>()).Count,
              "sin reglas no hay grupos");
    }

    static void D6ExplicarLaReglaAplicada()
    {
        Grupo("D6: la explicación de la regla aplicada");

        var reglas = new List<HyprReglas.Regla>
        {
            R("espacio", prio: 3, proc: "firefox", espacio: 2),
            R("flota", prio: 1, proc: "firefox", flotante: true),
            R("otra", proc: "code", monitor: 1),
        };
        var a = HyprVigilante.Examinar(Vt(1, "firefox", "GitHub"), reglas);
        var filas = HyprCatalogo.Explicar(a);

        Igual(HyprCatalogo.Tipo.Cabecera, filas[0].Que, "lo primero, de qué ventana se habla");
        Vale(filas[0].Texto.Contains("firefox"), "con su proceso");

        // Lo que se decidió, con el dueño de cada campo: es la respuesta a "¿por qué esta
        // ventana sale flotando?" sin releer la configuración entera.
        var puestos = filas.Where(f => f.Que == HyprCatalogo.Tipo.Puesto).ToList();
        Igual(2, puestos.Count, "dos campos puestos");
        Vale(puestos.Any(f => f.Texto.Contains("Espacio") && f.Texto.Contains("espacio")),
             "el espacio, con el id de la regla que lo pone");
        Vale(puestos.Any(f => f.Texto.Contains("Flotante") && f.Texto.Contains("flota")),
             "y lo de flotar, con el suyo");

        Vale(filas.Any(f => f.Que == HyprCatalogo.Tipo.SinOpinion && f.Texto.Contains("Monitor")),
             "los campos sin opinión se listan");
        Vale(filas.Any(f => f.Que == HyprCatalogo.Tipo.NoCoincide && f.Texto.Contains("otra")),
             "y las reglas que no coinciden, con su motivo: es la mitad útil");

        // ---------- Una ventana que no encaja en nada ----------
        var nada = HyprCatalogo.Explicar(HyprVigilante.Examinar(Vt(2, "calc", "Calculadora"), reglas));
        Vale(nada.Any(f => f.Que == HyprCatalogo.Tipo.Nada), "se dice que no coincide ninguna");
        Vale(!nada.Any(f => f.Que == HyprCatalogo.Tipo.Puesto), "y no hay nada puesto");

        // ---------- El aviso: el panel no puede mentir ----------
        //
        // Mientras las reglas no se apliquen, el panel TIENE que decirlo. Un panel que
        // enseña decisiones sin aclarar que no se ejecutan hace pensar que están roto.
        Vale(HyprCatalogo.AvisoDeSimulacion(NubeHypr.Modo.Simulacion).Contains("NO se mueve"),
             "en simulación, el aviso lo dice con mayúsculas");
        Vale(HyprCatalogo.AvisoDeSimulacion(NubeHypr.Modo.Apagado).Contains("apagado"),
             "y apagado, que ni se evalúan");
        foreach (var m in Enum.GetValues<NubeHypr.Modo>())
            Vale(HyprCatalogo.AvisoDeSimulacion(m).Length > 10,
                 $"el modo {m} tiene su aviso: ninguno se queda en blanco");
    }

    static void D6BuscarYAgruparAtajos()
    {
        Grupo("D6: buscar y agrupar los atajos");

        var mapa = HyprAtajos.DeSerie();
        var porNombre = new Dictionary<string, string>();
        foreach (var (combo, nombre) in mapa) porNombre[nombre] = combo;

        string Rot(string n) => HyprAtajos.Rotulo(n);
        string? Combo(string n) => porNombre.GetValueOrDefault(n);

        Igual(HyprAtajos.Nombres.Length,
              HyprCatalogo.BuscarAtajos(HyprAtajos.Nombres, "", Rot, Combo).Count,
              "sin búsqueda, los 19");

        var flot = HyprCatalogo.BuscarAtajos(HyprAtajos.Nombres, "flot", Rot, Combo);
        Vale(flot.Count > 0 && flot.Count < HyprAtajos.Nombres.Length,
             "«flot» encuentra algo y no todo");

        // Por la combinación también: "¿qué hacía Win+Alt+flecha derecha?"
        var porTecla = HyprCatalogo.BuscarAtajos(HyprAtajos.Nombres, "RIGHT", Rot, Combo);
        Vale(porTecla.Count > 0, "se puede buscar por la tecla");

        Igual(0, HyprCatalogo.BuscarAtajos(HyprAtajos.Nombres, "zzzz", Rot, Combo).Count,
              "y lo que no está, no sale");

        // ---------- Agrupar por familia ----------
        //
        // El prefijo del nombre semántico ya es la familia, así que no hace falta una tabla
        // aparte que se quede desincronizada el día que se añada un comando.
        var grupos = HyprCatalogo.AgruparAtajos(HyprAtajos.Nombres);
        Vale(grupos.Count > 1 && grupos.Count < HyprAtajos.Nombres.Length,
             "hay familias, y menos que comandos");
        Igual(HyprAtajos.Nombres.Length, grupos.Sum(g => g.Nombres.Count),
              "y no se pierde ni se duplica ningún comando");
        Vale(grupos.All(g => g.Familia.Length > 0), "toda familia tiene nombre");

        // Cada comando tiene rótulo: sin rótulo, el panel enseñaría "espacio.3" al usuario.
        foreach (var n in HyprAtajos.Nombres)
            Vale(HyprAtajos.Rotulo(n).Length > 0, $"«{n}» tiene rótulo legible");
    }

    // ================= El indicador breve de un comando =================
    //
    // Lo último que le faltaba a D6. Aquí se comprueba lo que se puede comprobar sin
    // pantalla: QUÉ dice, de qué tono y cuánto dura. Que se vea bien encima de un vídeo de
    // Wallpaper Engine sigue siendo prueba manual, y así queda anotado.
    static void D6ElIndicadorBreve()
    {
        Grupo("D6: el indicador breve de un comando");

        // ---------- Lo que NO puede pasar: decir «hecho» cuando no se ha hecho ----------
        //
        // Es la prueba importante del grupo. La capa está en simulación de serie, así que el
        // mensaje de un comando con efectos tiene que decirlo SIEMPRE. Si no, el usuario ve
        // «Principal · 4 ventanas», mira la pantalla, no ha cambiado nada, y concluye que
        // NubeHypr está roto — cuando está haciendo exactamente lo que se le pidió.
        foreach (var n in HyprAtajos.Nombres)
        {
            var sim = HyprAviso.DelComando(n, 4, simulado: true);
            Vale(sim.Texto.Contains("simulado", StringComparison.OrdinalIgnoreCase),
                 $"«{n}» en simulación lo dice");
            Igual(HyprAviso.Tono.Aviso, sim.Como, $"«{n}» en simulación va en coral");
        }

        var real = HyprAviso.DelComando("layout.principal", 4, simulado: false);
        Vale(!real.Texto.Contains("simulado"), "aplicando de verdad no dice «simulado»");
        Vale(real.Texto.Contains("4 ventanas"), "y dice cuántas ventanas");
        Igual(HyprAviso.Tono.Normal, real.Como, "aplicando de verdad va en azul");

        // Singular y plural. Un «1 ventanas» en un indicador de dos segundos es justo el
        // tipo de detalle que hace que un programa parezca de juguete.
        Vale(HyprAviso.DelComando("foco.siguiente", 1, false).Texto.Contains("1 ventana")
             && !HyprAviso.DelComando("foco.siguiente", 1, false).Texto.Contains("1 ventanas"),
             "una ventana en singular");

        // ---------- Cero efectos: el motivo, no el rótulo ----------
        //
        // «Foco →» a secas, sin que pase nada, parece un atajo roto. El motivo del motor es
        // lo único que distingue "no hay nada que enfocar" de "esto no funciona".
        var nada = HyprAviso.DelComando("foco.siguiente", 0, true, "no hay otra ventana");
        Vale(nada.Texto.Contains("no hay otra ventana"), "sin efectos se enseña el motivo");
        Igual(HyprAviso.Tono.Aviso, nada.Como, "sin efectos, coral");
        Igual(HyprAviso.GlifoNada, nada.Glifo, "sin efectos, el glifo de «nada»");

        // Y sin motivo, algo que se entienda igual: nunca una frase a medias.
        var sinMotivo = HyprAviso.DelComando("foco.siguiente", 0, true);
        Vale(sinMotivo.Texto.Length > HyprAtajos.Rotulo("foco.siguiente").Length + 2,
             "sin motivo también se dice algo");

        // ---------- D9.4: propuesto y salido son dos números ----------
        //
        // El motor propone cuatro y el respeto a tu gesto deja pasar una. Decir «4 ventanas»
        // es un número que se desmiente mirando la pantalla.
        var unaDeCuatro = HyprAviso.DelComandoAplicado("layout.principal", 4, 1, simulado: false);
        Vale(unaDeCuatro.Texto.Contains("1 ventana") && !unaDeCuatro.Texto.Contains("4"),
             "se cuenta lo que salió, no lo que se propuso");

        // Y si no sale ninguna, la frase no puede ser «no había nada que hacer»: había cuatro.
        var ninguna = HyprAviso.DelComandoAplicado("layout.principal", 4, 0, simulado: false);
        Vale(ninguna.Texto.Contains("colocado tú"),
             "si las redes lo descartan todo, se dice que fuiste tú");
        Vale(!ninguna.Texto.Contains("no había nada"),
             "y NO se dice que no había nada que hacer, porque sí lo había");
        Igual(HyprAviso.Tono.Aviso, ninguna.Como, "y va en coral");

        // En simulación el número honesto es lo que SE HARÍA: no hay "salieron" porque no se
        // mueve nada. Si se contara lo salido, en simulación siempre diría cero.
        var simulada = HyprAviso.DelComandoAplicado("layout.principal", 4, 0, simulado: true);
        Vale(simulada.Texto.Contains("simulado"), "en simulación se dice");
        Vale(!simulada.Texto.Contains("colocado tú"),
             "y no se confunde «no se movió porque es simulación» con «lo colocaste tú»");

        // Cero propuestas sigue siendo cero propuestas, no un descarte de las redes.
        Vale(!HyprAviso.DelComandoAplicado("foco.siguiente", 0, 0, false).Texto.Contains("colocado tú"),
             "sin propuestas no se culpa al usuario");

        // ---------- Los escritorios, con el mismo rótulo que la barra ----------
        var esp = HyprAviso.DelEspacio("espacio.siguiente", "2 · Trabajo", null);
        Vale(esp.Texto.Contains("2 · Trabajo"), "el destino sale con su rótulo");
        Vale(esp.Texto.Contains("simulado"), "y que no se ha cambiado de verdad");

        var sinDestino = HyprAviso.DelEspacio("espacio.siguiente", null, "no hay escritorio al que ir");
        Vale(sinDestino.Texto.Contains("no hay escritorio"), "sin destino, el motivo");
        Igual(HyprAviso.Tono.Aviso, sinDestino.Como, "sin destino, coral");
        // Cadena vacía cuenta como "no hay destino": un rótulo vacío daría «Espacio → ·  ».
        Igual(sinDestino.Texto, HyprAviso.DelEspacio("espacio.siguiente", "", "no hay escritorio al que ir").Texto,
              "un rótulo vacío se trata como sin destino");

        // ---------- Deshacer ----------
        Vale(HyprAviso.DelDeshacer(0, true).Texto.Contains("no hay nada"),
             "deshacer sin nada lo dice");
        Igual(HyprAviso.Tono.Aviso, HyprAviso.DelDeshacer(0, true).Como, "y en coral");
        Igual(HyprAviso.Tono.Normal, HyprAviso.DelDeshacer(3, false).Como,
              "deshacer de verdad, en azul");
        Vale(HyprAviso.DelDeshacer(3, false).Texto.Contains("3 ventanas"),
             "deshacer dice cuántas");

        // ---------- Apagado y modo seguro, que no son lo mismo ----------
        //
        // «Apagado» lo has puesto tú; «modo seguro» lo ha puesto NubeHypr solo porque algo
        // iba mal. Confundirlos deja al usuario buscando un interruptor que no apagó.
        Vale(HyprAviso.Apagado(false).Texto != HyprAviso.Apagado(true).Texto,
             "apagado y modo seguro dicen cosas distintas");
        Vale(HyprAviso.Apagado(true).Texto.Contains("seguro"), "el modo seguro se nombra");
        Igual(HyprAviso.Tono.Aviso, HyprAviso.Apagado(false).Como, "apagado, en coral");

        // Y el fallo: se dice que ha fallado, y NO se escupe la excepción en pantalla.
        var pum = HyprAviso.Reventado("layout.principal");
        Vale(pum.Texto.Contains("fallado"), "un atajo que revienta lo dice");
        Vale(pum.Texto.Contains("registro"), "y manda al registro, que es donde está la traza");

        // ---------- Los glifos ----------
        //
        // Un punto por familia, no veinte iconos distintos. Y ninguno puede salir vacío: un
        // glifo vacío deja el indicador con un hueco y parece un fallo de pintado.
        foreach (var n in HyprAtajos.Nombres)
            Vale(HyprAviso.GlifoDe(n).Length > 0, $"«{n}» tiene glifo");

        Igual(HyprAviso.GlifoEspacio, HyprAviso.GlifoDe("espacio.3"), "los de espacio comparten glifo");
        Igual(HyprAviso.GlifoEspacio, HyprAviso.GlifoDe("espacio.siguiente"), "y entre ellos también");
        Igual(HyprAviso.GlifoFoco, HyprAviso.GlifoDe("foco.izquierda"), "los de foco, el suyo");
        Igual(HyprAviso.GlifoNada, HyprAviso.GlifoDe("inventado.que.no.existe"),
              "un nombre desconocido no revienta: cae en el de «nada»");
        Igual(HyprAviso.GlifoNada, HyprAviso.GlifoDe(""), "y una cadena vacía tampoco");

        // Menos glifos que comandos: si hubiera uno por comando, el indicador sería un
        // catálogo de iconos en vez de una señal.
        Vale(HyprAtajos.Nombres.Select(HyprAviso.GlifoDe).Distinct().Count() < HyprAtajos.Nombres.Length,
             "hay menos glifos que comandos");

        // ---------- Cuánto dura ----------
        //
        // Ni tan corto que no dé tiempo a leerlo ni tan largo que estorbe. Y el texto largo
        // tiene que durar MÁS que el corto, que es el motivo de que la duración no sea fija.
        Vale(HyprAviso.Duracion("Foco →") >= HyprAviso.Minimo, "nunca por debajo del mínimo");
        Vale(HyprAviso.Duracion(new string('x', 400).Replace("x", "palabra ")) <= HyprAviso.Maximo,
             "nunca por encima del máximo");
        Vale(HyprAviso.Duracion("Espacio →: no hay más escritorios a la derecha")
             > HyprAviso.Duracion("Flotante"), "el texto largo dura más");
        Vale(HyprAviso.Duracion("") >= HyprAviso.Minimo, "y un texto vacío no deja duración cero");

        // ---------- Reaprovechar, no reabrir ----------
        //
        // Pulsar la misma tecla cinco veces tiene que actualizar el texto, no abrir y cerrar
        // el panel cinco veces: la diferencia entre un indicador y un parpadeo.
        var a = HyprAviso.DelComando("foco.siguiente", 1, false);
        var b = HyprAviso.DelComando("foco.izquierda", 1, false);
        var aOtraVez = HyprAviso.DelComando("foco.siguiente", 1, false);

        Vale(HyprAviso.SoloCambiarElTexto(true, a, b), "con el panel puesto y otro texto, se repinta");
        Vale(!HyprAviso.SoloCambiarElTexto(true, a, aOtraVez), "el mismo mensaje no se repinta");
        Vale(HyprAviso.NiSiquieraEso(true, a, aOtraVez), "el mismo mensaje solo alarga el tiempo");
        Vale(!HyprAviso.NiSiquieraEso(true, a, b), "otro mensaje sí toca el panel");
        Vale(!HyprAviso.SoloCambiarElTexto(false, a, b) && !HyprAviso.NiSiquieraEso(false, a, a),
             "sin panel puesto, ninguna de las dos aplica");

        // Mismo texto pero distinto tono también cuenta como cambio: el color es la mitad
        // del mensaje, y repintarlo es justo lo que hace falta.
        var mismoTextoOtroTono = a with { Como = HyprAviso.Tono.Aviso };
        Vale(HyprAviso.SoloCambiarElTexto(true, a, mismoTextoOtroTono),
             "cambiar solo el tono también repinta");
    }
}
