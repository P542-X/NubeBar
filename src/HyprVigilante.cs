using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D5.1: cuando aparece una ventana, ¿qué dirían las reglas?
//
// ---------- Qué hace y qué NO hace ----------
//
// Mira. Resuelve las reglas contra la ventana que acaba de aparecer, apunta la decisión, la
// regla que coincidió, los campos que aportó y los que se descartaron, y **no llama a
// `SetWindowPos` ni una vez**. Ni en modo "aplicar": D5.1 es el eslabón que conecta las
// reglas con la realidad, y conectarlo y aplicarlo a la vez sería meter dos cambios en uno
// y no saber cuál rompió qué.
//
// ---------- Por qué "aparecer" y no "cada cambio" ----------
//
// `GestorVentanas.Cambio` avisa de cinco cosas distintas: la lista, la activa, los títulos,
// los estados y los sitios. Las reglas solo tienen sentido al APARECER una ventana: es
// cuando se decide dónde va. Resolverlas en cada cambio de título sería recalcular cuarenta
// veces por minuto para no hacer nada — y es exactamente el camino por el que se llegó al
// parpadeo de la vista general.
//
// Y una ventana se resuelve **una sola vez**: se recuerda cuáles ya se vieron. Sin eso, una
// ventana que cambia de título volvería a "aparecer" para las reglas cada vez.
public static class HyprVigilante
{
    // Las que ya se resolvieron. No es una caché de la decisión: es la memoria de "a esta
    // ya le tocó", que es lo que hace que la conexión sea idempotente.
    static readonly HashSet<IntPtr> _yaVistas = new();

    public static int Vistas => _yaVistas.Count;
    public static long Resueltas { get; private set; }
    public static long SinReglas { get; private set; }

    internal static void Olvidar() { _yaVistas.Clear(); Resueltas = 0; SinReglas = 0; }

    // Las ventanas que han aparecido desde la última vez. Pura sobre los dos conjuntos:
    // así se puede comprobar sin ventanas.
    public static List<VentanaModelo> Nuevas(IEnumerable<VentanaModelo> ahora,
                                              ISet<IntPtr> yaVistas)
    {
        return ahora.Where(v => !yaVistas.Contains(v.Hwnd)).ToList();
    }

    // ---------- Lo que se apunta de una ventana ----------
    //
    // La decisión, la regla de cada campo, y **los campos que no se pusieron**. Esto
    // último no es relleno: "mi navegador no se va al espacio 2" se contesta viendo que
    // ninguna regla opinó del espacio, y sin la lista de descartados eso no se distingue
    // de "se intentó y falló".
    public sealed record Apunte(
        IntPtr Hwnd, string Proceso, string Titulo,
        HyprReglas.Decision Decision,
        List<HyprReglas.Explicacion> Explicaciones,
        List<string> CamposPuestos,
        List<string> CamposSinOpinion)
    {
        public bool AlgunaCoincidio => Explicaciones.Any(e => e.Coincidio);
    }

    static readonly string[] TodosLosCampos =
        { "Espacio", "Monitor", "Layout", "Flotante", "Tamano", "Foco" };

    public static Apunte Examinar(VentanaModelo v, IEnumerable<HyprReglas.Regla> reglas,
                                   string clase = "", string ruta = "")
    {
        var r = HyprReglas.Resolver(v, reglas, clase, ruta);

        var puestos = new List<string>();
        if (r.Decision.Espacio != null) puestos.Add("Espacio");
        if (r.Decision.Monitor != null) puestos.Add("Monitor");
        if (r.Decision.Layout != null) puestos.Add("Layout");
        if (r.Decision.Flotante != null) puestos.Add("Flotante");
        if (r.Decision.Ancho != null || r.Decision.Alto != null) puestos.Add("Tamano");
        if (r.Decision.Foco != null) puestos.Add("Foco");

        var sinOpinion = TodosLosCampos.Where(c => !puestos.Contains(c)).ToList();

        return new Apunte(v.Hwnd, v.Proceso, v.Titulo, r.Decision, r.Explicaciones,
                          puestos, sinOpinion);
    }

    // ---------- Las líneas del log ----------
    //
    // Con la etiqueta de la fase, como el resto, para que un log se pueda leer con el
    // documento al lado.
    //
    // ---------- El modo ya no se puede dar por supuesto ----------
    //
    // Aquí iba `modo=simulacion` **fijo**, con un comentario que decía «va siempre: que quede
    // escrito que no se movió nada». Era verdad cuando se escribió: hasta D5.1 el vigilante
    // resolvía y apuntaba, y se acabó.
    //
    // Desde D9.5 sí mueve, y desde D9.6 reparte escritorios enteros — así que esta línea
    // llevaba desde entonces diciendo «simulacion» mientras las ventanas se movían de verdad.
    // Un registro que miente sobre si algo se aplicó es peor que uno que no lo diga: apareció
    // investigando por qué D9.6 no repartía, y costó un rato descartarlo como causa.
    //
    // Ahora se pasa el modo. `null` es para quien solo resuelve sin intención de aplicar —el
    // editor de reglas, la guía, un arnés que solo quiere ver la decisión—, y entonces se
    // dice eso mismo en vez de inventarse un modo.
    public static List<string> Lineas(Apunte a, NubeHypr.Modo? modo = null)
    {
        var lineas = new List<string>
        {
            $"[D5]  ventana nueva hwnd=0x{a.Hwnd.ToInt64():X6} proceso={a.Proceso} " +
            $"titulo=\"{Corto(a.Titulo)}\" " +
            $"modo={(modo is { } m ? m.ToString().ToLowerInvariant() : "solo se resuelve")}",
        };

        if (!a.AlgunaCoincidio)
        {
            lineas.Add("[D5]    ninguna regla coincide: la ventana se queda como la ponga Windows");
            return lineas;
        }

        lineas.Add($"[D5]    decision: {a.Decision}");
        lineas.Add($"[D5]    puestos: {(a.CamposPuestos.Count > 0 ? string.Join(", ", a.CamposPuestos) : "ninguno")}");
        lineas.Add($"[D5]    sin opinion: {(a.CamposSinOpinion.Count > 0 ? string.Join(", ", a.CamposSinOpinion) : "ninguno")}");
        foreach (var e in a.Explicaciones) lineas.Add("[D5]  " + e);

        // Y la misma corrección que la del modo: esta línea decía siempre «NO se ha movido
        // nada», que dejó de ser cierto en D9.5. Ahora dice lo que va a pasar de verdad, y
        // quien lea el registro puede fiarse de ella.
        lineas.Add(modo switch
        {
            NubeHypr.Modo.Aplicar => "[D5]    y con el modo en «aplicar», esto SÍ se ejecuta " +
                                     "(ver las líneas [D9.5] y [D9.6] que vienen detrás)",
            NubeHypr.Modo.Simulacion => "[D5]    NO se mueve nada: la capa está en simulación",
            NubeHypr.Modo.Apagado => "[D5]    NO se mueve nada: la capa está apagada",
            _ => "[D5]    aquí solo se resuelve: mover es cosa de quien llame a esto",
        });
        return lineas;
    }

    static string Corto(string s) => s.Length <= 48 ? s : s[..45] + "...";

    // ================= La conexión =================
    //
    // Se llama cuando `GestorVentanas` avisa de que la LISTA cambió. Devuelve los apuntes
    // de las ventanas nuevas, y marca esas ventanas como vistas.
    //
    // Si la capa está apagada no hace nada: ni resuelve ni recuerda. Con la capa apagada
    // NubeBar se comporta como siempre, y eso incluye no gastar tiempo en esto.
    public static List<Apunte> AlCambiarLaLista(
        NubeHypr.Modo modo,
        IReadOnlyList<VentanaModelo> ventanas,
        IEnumerable<HyprReglas.Regla> reglas,
        Func<IntPtr, string>? claseDe = null,
        Func<IntPtr, string>? rutaDe = null)
    {
        var apuntes = new List<Apunte>();
        if (modo == NubeHypr.Modo.Apagado) return apuntes;

        // Las que se fueron se olvidan: si no, una sesión larga acumula handles muertos y
        // un hwnd reutilizado por Windows se daría por ya visto.
        var vivas = new HashSet<IntPtr>(ventanas.Select(v => v.Hwnd));
        _yaVistas.RemoveWhere(h => !vivas.Contains(h));

        var reglasFirmes = reglas.ToList();
        foreach (var v in Nuevas(ventanas, _yaVistas))
        {
            _yaVistas.Add(v.Hwnd);
            if (reglasFirmes.Count == 0) { SinReglas++; continue; }

            var a = Examinar(v, reglasFirmes,
                             claseDe?.Invoke(v.Hwnd) ?? "", rutaDe?.Invoke(v.Hwnd) ?? "");
            Resueltas++;
            apuntes.Add(a);
        }
        return apuntes;
    }
}
