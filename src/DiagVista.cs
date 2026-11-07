using System;
using System.Text;

namespace NubeBar;

// `NubeBar.exe --diagvista [segundos]`: abre la vista general, la deja quieta y cuenta
// qué le pasa.
//
// ---------- Por qué hace falta ----------
//
// "Las tarjetas parpadean" no se puede investigar leyendo el código, y tampoco mirándolo
// a ojo: lo que hay que saber es **cuántas veces** se rehace la cuadrícula en treinta
// segundos sin tocar nada, y **por culpa de qué evento**. Sin esto, lo único que se puede
// hacer es cambiar cosas y volver a mirar, que es exactamente como se arreglan los
// síntomas en vez de las causas.
//
// La prueba es que esta medida es la que encontró el bucle: la vista se rehacía entera
// unas once veces por segundo porque recolocar la ventana de la isla disparaba un
// `EVENT_OBJECT_LOCATIONCHANGE` sobre nuestra propia ventana, que pedía refrescar, que
// avisaba de un cambio, que rehacía la vista, que volvía a recolocar la ventana.
//
// Abre una NubeBar de verdad —con sus ganchos, sus animaciones y su isla— porque el bucle
// solo existe con todo en marcha. Al terminar escribe el recuento y se cierra.
public static class DiagVista
{
    internal const string Bandera = "--diagvista";

    public static bool Encendido;
    public static int Segundos = 30;

    // Lo que se cuenta. Son los campos que se pidieron: cuántas veces se reconcilió,
    // cuántas se reconstruyó (que es la anomalía), qué se creó, se actualizó y se quitó,
    // cuántas miniaturas se reemplazaron y cuántas veces se remidió el panel.
    public static long Reconciliaciones, Reconstrucciones, Aplazados;
    public static long Creadas, Actualizadas, Eliminadas;
    public static long MiniaturasPuestas, PanelRemedido;
    public static long ArrastresIsla, ArrastresIslaConVistaAbierta;
    public static long ArrastresTarjeta, ArrastresVentana, AvisosAjenos;

    public static void Reiniciar()
    {
        Reconciliaciones = Reconstrucciones = Aplazados = 0;
        Creadas = Actualizadas = Eliminadas = 0;
        MiniaturasPuestas = PanelRemedido = 0;
        ArrastresIsla = ArrastresIslaConVistaAbierta = 0;
        ArrastresTarjeta = ArrastresVentana = AvisosAjenos = 0;
        GestorVentanas.OlvidarContadores();
    }

    public static string Informe(int segundos, int tarjetas)
    {
        var sb = new StringBuilder();
        void Di(string s) => sb.AppendLine(s);

        Di("=== Diagnóstico de la vista general ===");
        Di($"la vista estuvo abierta y quieta: {segundos} s");
        Di($"tarjetas en pantalla: {tarjetas}");
        Di("");
        Di("--- la cuadrícula ---");
        Di($"  reconciliaciones ................. {Reconciliaciones}");
        Di($"  RECONSTRUCCIONES COMPLETAS ....... {Reconstrucciones}   <- cualquier número > 0 es una anomalía");
        Di($"  avisos aplazados (menú/arrastre) .. {Aplazados}");
        Di($"  tarjetas creadas ................. {Creadas}");
        Di($"  tarjetas actualizadas en su sitio  {Actualizadas}");
        Di($"  tarjetas eliminadas .............. {Eliminadas}");
        Di($"  veces que se remidió el panel .... {PanelRemedido}   <- cada una mueve la ventana de la isla");
        Di($"  miniaturas reemplazadas .......... {MiniaturasPuestas}");
        Di("");
        Di("--- de dónde vienen los avisos ---");
        Di("  " + GestorVentanas.Contadores());
        Di("");
        Di("--- los arrastres ---");
        Di($"  arrastres de la isla ............. {ArrastresIsla}");
        Di($"  ...con la vista abierta .......... {ArrastresIslaConVistaAbierta}   <- cualquier número > 0 es un FALLO");
        Di($"  arrastres de tarjeta ............. {ArrastresTarjeta}");
        Di($"  arrastres de ventana real ........ {ArrastresVentana}");
        Di($"  avisos de fin de OTRA ventana .... {AvisosAjenos}   (ignorados, no cancelan el gesto)");
        Di("");

        // El veredicto, dicho sin rodeos: con la vista quieta y nadie tocando nada, lo
        // único que puede pasar es nada.
        bool limpio = Reconstrucciones == 0 && Creadas == 0 && Eliminadas == 0 &&
                      PanelRemedido == 0 && ArrastresIslaConVistaAbierta == 0;
        Di(limpio
            ? $"VEREDICTO: la vista aguantó {segundos} s sin reconstruirse, sin crear ni quitar " +
              "tarjetas y sin remedir el panel. Estable."
            : "VEREDICTO: la vista se movió estando quieta. Mirar los contadores de arriba " +
              "que no sean cero.");
        return sb.ToString();
    }

    // Lee los argumentos. Devuelve si la bandera estaba puesta.
    public static bool Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return false;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s > 0) Segundos = Math.Min(300, s);
        return true;
    }
}
