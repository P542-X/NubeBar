using System;
using System.Linq;
using System.Windows.Media;

namespace NubeBar;

// E8: las pruebas de estabilización de la macrofase E.
//
// Lo que se comprueba aquí es lo TRANSVERSAL: que las superficies nuevas funcionan en los
// siete temas, en claro y en alto contraste, y que las piezas de E no se han saltado las
// reglas que A–D ya habían fijado.
public static partial class Pruebas
{
    internal static void CorrerE8()
    {
        E8LasSuperficiesEnLosSieteTemas();
        E8LasPiezasDeESiguenLasReglas();
        CorrerFase();       // la guarda del binario de fase (Pruebas.Fase.cs)
    }

    // ================= Los siete temas =================
    static void E8LasSuperficiesEnLosSieteTemas()
    {
        Grupo("E8: las superficies de E se leen en los siete temas");

        foreach (var t in NubeBar.Temas.Predefinidos)
        {
            var tema = new Tema();
            NubeBar.Temas.Aplicar(t, tema);
            var fondo = (Color)ColorConverter.ConvertFromString(tema.Fondo)!;
            var texto = (Color)ColorConverter.ConvertFromString(tema.Texto)!;
            var acento = (Color)ColorConverter.ConvertFromString(tema.Acento)!;
            var r = Roles.Desde(tema, fondo, texto, acento);

            // ---------- El fondo de un panel lateral ----------
            //
            // Es opaco de verdad, o el texto pequeño de los controles se vuelve ilegible con
            // el escritorio transparentándose detrás.
            Igual(255, (int)r.FondoOpaco.A, $"[{t.Nombre}] el fondo del panel lateral es opaco");

            // ---------- El indicador del dock ----------
            //
            // Azul para la ventana de delante, atenuado para las demás. Los dos tienen que
            // distinguirse del fondo del dock Y entre sí, o el indicador no indica.
            var azul = HyprPinta.Azul(r);
            double contrasteAzul = Contraste.Entre(azul, r.FondoOpaco);
            Vale(contrasteAzul >= 1.6,
                 $"[{t.Nombre}] el indicador azul se ve sobre el dock ({contrasteAzul:0.00}:1)");

            // ---------- Y que los dos estados se distinguen ----------
            //
            // NO por la luminancia de los dos colores: el dock los pinta con opacidades
            // distintas, así que comparar los colores crudos mide algo que no pasa en
            // pantalla. Lo que se comprueba es la decisión de verdad, que vive en
            // `Dock.MarcaDe` y cuenta por tres canales: color, opacidad y longitud.
            var activa = Dock.MarcaDe(new Dock.Elemento("a", "A", false, Ventanas: 1, Activa: true));
            var abierta = Dock.MarcaDe(new Dock.Elemento("b", "B", false, Ventanas: 1, Activa: false));
            Vale(Dock.SeDistinguen(activa, abierta),
                 $"[{t.Nombre}] la de delante se distingue de una abierta por varios canales");
            // ---------- El texto del panel ----------
            Vale(Contraste.Entre(r.Texto, r.FondoOpaco) >= 4.5,
                 $"[{t.Nombre}] el texto del panel cumple el mínimo de lectura");

            // ---------- El día de hoy en el calendario ----------
            //
            // Va en azul sobre la superficie del panel: si no se distinguiera, «hoy» dejaría
            // de estar marcado.
            var tinta = HyprPinta.AzulTinta(r, r.FondoOpaco);
            Vale(Contraste.Entre(tinta, r.FondoOpaco) >= Roles.MinimoSuperficie - 0.1,
                 $"[{t.Nombre}] «hoy» se lee en el calendario");

            // ---------- Y el coral sigue siendo otra cosa ----------
            //
            // Azul y coral tienen que seguir separándose: si convergieran en algún tema,
            // «foco» y «aviso» serían el mismo color.
            double sep = Math.Abs(Contraste.Tono(azul) - Contraste.Tono(HyprPinta.Coral(r)));
            sep = Math.Min(sep, 360 - sep);
            Vale(sep > 60, $"[{t.Nombre}] azul y coral siguen a más de 60° ({sep:0}°)");
        }
    }

    // ================= Las reglas de siempre =================
    static void E8LasPiezasDeESiguenLasReglas()
    {
        Grupo("E8: las piezas de E no se saltan lo que A–D fijó");

        // ---------- La nube nunca es una mascota ----------
        //
        // El tope de D6 sigue puesto en todo lo que E dibuja nubes: el módulo de la barra, el
        // perfil del panel y el destello de las transiciones.
        foreach (double hueco in new[] { 16.0, 38, 64, 140, 400, 2160 })
        {
            double lado = HyprPinta.TamanoDeLaNube(hueco);
            Vale(lado <= 200, $"con {hueco:0} px de hueco, la nube sigue siendo pequeña ({lado:0})");
        }

        // ---------- Ninguna superficie nueva reserva espacio salvo la barra ----------
        //
        // Dos reservas en el mismo monitor descontarían dos veces del área de trabajo.
        foreach (var c in Enum.GetValues<Superficies.Clase>())
        {
            var s = Superficies.Nueva("x", c);
            if (c == Superficies.Clase.Barra)
                Vale(s.ReservaEspacio, "la barra reserva espacio");
            else
                Vale(!s.ReservaEspacio, $"{c} NO reserva espacio: flota");
        }

        // ---------- Ninguna transición pasa del tope ----------
        foreach (var q in Enum.GetValues<Transiciones.Que>())
            Vale(Transiciones.De(q).Ms <= Transiciones.TopeMs,
                 $"la transición {q} sigue dentro del tope");

        // ---------- Y la composición E sigue apagada de serie ----------
        //
        // Lo mismo que con NubeHypr: una actualización no puede cambiarle a nadie la forma
        // del escritorio sin que lo haya pedido.
        Igual("nunca", new Barra().Dock, "el dock viene apagado de serie");
        Vale(!new Config().NubeHypr.Activo, "y NubeHypr sigue apagado");
        Igual("simulacion", new Config().NubeHypr.Modo, "y en simulación");
    }
}

