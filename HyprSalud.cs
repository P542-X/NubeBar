using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D7: el supervisor interno y el modo seguro.
//
// ---------- La regla que gobierna este archivo ----------
//
// El documento la escribe como riesgo de la fase: *"una recuperacion automatica demasiado
// agresiva puede empeorar el problema. Preferir no hacer nada"*. Así que esto no es un
// sistema que arregle cosas: es uno que **deja de tocar** cuando algo no cuadra.
//
// Tres estados y tres acciones, y la acción más fuerte que existe es apagar la capa. No hay
// "reintentar moviendo ventanas", no hay "recolocar todo por si acaso", y no hay nada que
// escriba la configuración: entrar en modo seguro no puede dejarte la configuración tocada,
// porque entonces un fallo de una tarde se convertiría en un cambio permanente.
//
// ---------- Y qué NO es el modo seguro ----------
//
// No es apagar NubeBar. La isla, los paneles, el carrusel, la bandeja, el reloj y el
// buscador siguen exactamente igual: lo único que se apaga es **organizar ventanas**. Eso
// es lo que pide el documento —"desactivar organizacion y conservar isla y paneles"— y es
// también lo honesto: un fallo en la capa de ventanas no es motivo para quitarle al usuario
// la barra de tareas.
public static class HyprSalud
{
    // ================= Lo que el supervisor mira =================
    //
    // Cuatro señales, y ninguna es "¿funciona?". Son cosas medibles desde dentro:
    //
    //  - La edad de la instantánea: `GestorVentanas` se refresca con eventos de Windows. Si
    //    lleva mucho sin refrescarse habiendo actividad, el gancho se ha caído.
    //  - Los atajos registrados: si la capa está encendida y hay cero, `RegisterHotKey` ha
    //    fallado o alguien los ha soltado por detrás.
    //  - Los fallos seguidos al aplicar: una ventana que no se deja mover es normal; diez
    //    seguidas es que el adaptador no está haciendo nada útil.
    //  - Las excepciones seguidas: lo mismo, un nivel más arriba.
    public sealed record Señales(
        TimeSpan EdadDeLaInstantanea,
        int AtajosVivos,
        int AtajosEsperados,
        int FallosSeguidos,
        int ExcepcionesSeguidas,
        bool CapaEncendida)
    {
        public static readonly Señales Sana =
            new(TimeSpan.Zero, 0, 0, 0, 0, false);
    }

    public enum Estado { Sano, Sospechoso, Detenido }

    // Qué se hace. Nótese lo que falta: no hay `Reorganizar`.
    public enum Accion { Nada, Mirar, ModoSeguro }

    // ---------- Los umbrales ----------
    //
    // Generosos a propósito. Un supervisor nervioso que apaga la capa cada vez que una
    // ventana tarda es peor que no tener supervisor: el usuario no sabría nunca por qué se
    // apaga, y acabaría dejándolo apagado.
    public static readonly TimeSpan InstantaneaVieja = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan InstantaneaMuerta = TimeSpan.FromMinutes(3);
    public const int FallosParaSospechar = 5;
    public const int FallosParaParar = 15;
    public const int ExcepcionesParaParar = 5;

    public sealed record Diagnostico(Estado Como, Accion Que, string Porque)
    {
        public override string ToString() => $"{Como} -> {Que}: {Porque}";
    }

    public static Diagnostico Mirar(Señales s)
    {
        // Con la capa apagada no hay nada que supervisar, y desde luego nada que apagar.
        // Esto va primero: sin él, una instantánea vieja con la capa apagada —que es lo
        // normal cuando el equipo está en reposo— pediría modo seguro sin motivo.
        if (!s.CapaEncendida)
            return new(Estado.Sano, Accion.Nada, "la capa está apagada: nada que supervisar");

        if (s.ExcepcionesSeguidas >= ExcepcionesParaParar)
            return new(Estado.Detenido, Accion.ModoSeguro,
                       $"{s.ExcepcionesSeguidas} excepciones seguidas en el adaptador");

        if (s.FallosSeguidos >= FallosParaParar)
            return new(Estado.Detenido, Accion.ModoSeguro,
                       $"{s.FallosSeguidos} movimientos seguidos sin conseguirse");

        if (s.EdadDeLaInstantanea >= InstantaneaMuerta)
            return new(Estado.Detenido, Accion.ModoSeguro,
                       $"la instantánea lleva {s.EdadDeLaInstantanea.TotalMinutes:0.#} min sin refrescarse: " +
                       "el gancho de ventanas no está vivo");

        // ---------- Sospechoso: se apunta y no se toca nada ----------
        //
        // Es la mitad del valor de esto. "Sospechoso" existe para que el log diga qué
        // estaba pasando ANTES de que se apagara, en vez de tener un salto de "todo bien" a
        // "modo seguro" sin nada en medio.
        if (s.FallosSeguidos >= FallosParaSospechar)
            return new(Estado.Sospechoso, Accion.Mirar,
                       $"{s.FallosSeguidos} movimientos seguidos sin conseguirse");

        if (s.EdadDeLaInstantanea >= InstantaneaVieja)
            return new(Estado.Sospechoso, Accion.Mirar,
                       $"la instantánea lleva {s.EdadDeLaInstantanea.TotalSeconds:0} s sin refrescarse");

        if (s.AtajosEsperados > 0 && s.AtajosVivos == 0)
            return new(Estado.Sospechoso, Accion.Mirar,
                       $"se esperaban {s.AtajosEsperados} atajos y no hay ninguno vivo");

        return new(Estado.Sano, Accion.Nada, "todo en orden");
    }

    // ================= El modo seguro =================
    //
    // Es un estado de EJECUCIÓN, no una clave de configuración. A propósito: si se guardara,
    // un fallo de una tarde dejaría NubeHypr apagado para siempre sin que el usuario
    // supiera por qué, y tendría que ir a buscar la clave para volver. Reiniciando NubeBar
    // se sale del modo seguro — y si el problema sigue, el supervisor lo vuelve a poner.
    public static bool Seguro { get; private set; }
    public static string RazonDelSeguro { get; private set; } = "";
    public static DateTime DesdeCuando { get; private set; }
    public static int VecesQueSeHaEntrado { get; private set; }

    public static bool Entrar(string razon)
    {
        if (Seguro) return false;                // idempotente: entrar dos veces no es entrar
        Seguro = true;
        RazonDelSeguro = razon;
        DesdeCuando = DateTime.Now;
        VecesQueSeHaEntrado++;
        // El renglón de estado lo escribe quien llama, no esto. Aquí no se sabe en qué
        // versión está el config del usuario, y un `config-version=` inventado es peor que
        // ninguno: la primera vez que se probó esto, el registro decía v4 en una línea y v3
        // en la siguiente, y las dos hablaban del mismo fichero.
        Log.Info($"[D7]  MODO SEGURO: {razon}. NubeHypr deja de organizar; la isla y los " +
                 "paneles siguen funcionando igual.");
        return true;
    }

    // Salir es SIEMPRE a mano. El supervisor no sale solo de modo seguro aunque las señales
    // vuelvan a estar bien: si el problema va y viene, saldría y entraría en bucle, y cada
    // entrada apaga la capa de golpe mientras el usuario la está usando.
    public static bool Salir()
    {
        if (!Seguro) return false;
        Log.Info($"[D7]  se sale del modo seguro a mano (estuvo {(DateTime.Now - DesdeCuando).TotalSeconds:0} s, " +
                 $"motivo: {RazonDelSeguro})");
        Seguro = false;
        RazonDelSeguro = "";
        return true;
    }

    internal static void Olvidar()
    {
        Seguro = false; RazonDelSeguro = ""; VecesQueSeHaEntrado = 0;
        DesdeCuando = default;
    }

    // ---------- El modo efectivo, con el modo seguro por encima de todo ----------
    //
    // El orden importa y es el mismo de D0 ampliado: el modo seguro gana a la capa
    // encendida, que gana al modo escrito en la configuración. Así, encendido o no, en
    // simulación o en aplicar, **en modo seguro no se organiza nada**.
    public static NubeHypr.Modo ModoConSeguro(bool activo, string? modo) =>
        Seguro ? NubeHypr.Modo.Apagado : NubeHypr.ModoEfectivo(activo, modo);

    // ================= El renglón de estado =================
    //
    // Con el formato que pide el documento: `[D7]  safe-mode=false adapter=ready
    // config-version=7`. En castellano el resto del log, pero estas claves en inglés porque
    // son las del documento y así un log se puede comparar con él sin traducir.
    // `version` es la del CONFIG DEL USUARIO, no la del esquema que trae el código. Cuando
    // no se sabe se escribe `?`, y eso es información: significa "nadie me ha dicho de qué
    // fichero hablamos". Poner `Esquema.Actual` como respaldo era lo que hacía que el
    // registro dijera v4 y v3 del mismo fichero en dos líneas seguidas.
    public static string Linea(string adaptador = "ready", int version = 0) =>
        $"[D7]  safe-mode={(Seguro ? "true" : "false")} adapter={adaptador} " +
        $"config-version={(version > 0 ? version.ToString() : "?")}" +
        (Seguro ? $" reason=\"{RazonDelSeguro}\"" : "");

    // ================= Qué hay que soltar al cerrar =================
    //
    // ---------- Por qué es una LISTA y no solo código de cierre ----------
    //
    // El cierre de NubeBar suelta siete cosas de NubeHypr, repartidas por tres archivos. La
    // forma de que una se quede sin soltar no es que alguien la escriba mal: es que se añada
    // la octava y nadie se acuerde del cierre. Con la lista aquí, una prueba puede exigir
    // que lo que se declara se suelte, y la octava se nota el día que se añade.
    public static readonly string[] HayQueSoltar =
    {
        "atajos globales (RegisterHotKey)",
        "la suscripción de reglas a GestorVentanas.Cambio",
        "la suscripción de la vista general a GestorVentanas.Cambio",
        "la previsualización (su ventana superpuesta)",
        "la memoria de posiciones anteriores",
        "la memoria de ventanas ya vistas por las reglas",
        "el gesto en curso, si había uno",
        // D9.1: las dos memorias nuevas. No atrapan nada del sistema —son diccionarios—
        // pero entran en el inventario por la misma razón que las otras dos: lo que no se
        // mide, no se sabe. Y un inventario que dice "limpio" sin mirarlas mentiría.
        "las marcas de gesto (qué ventanas has movido tú)",
        "los sitios recordados de las aplicaciones que se fueron",
    };

    // El inventario de lo que está vivo AHORA. Se mide, no se supone: cada número sale de
    // quien lo tiene, y un cierre limpio los deja todos a cero.
    public sealed record Inventario(
        int Atajos, bool ReglasEscuchando, bool VistaEscuchando,
        bool PreviaViva, int PosicionesRecordadas, int VentanasVistas, bool GestoEnCurso,
        int MarcasDeGesto, int SitiosRecordados)
    {
        public int Total => Atajos + (ReglasEscuchando ? 1 : 0) + (VistaEscuchando ? 1 : 0) +
                            (PreviaViva ? 1 : 0) + PosicionesRecordadas + VentanasVistas +
                            (GestoEnCurso ? 1 : 0) + MarcasDeGesto + SitiosRecordados;

        public bool Limpio => Total == 0;

        public override string ToString() =>
            $"atajos={Atajos} reglas={(ReglasEscuchando ? "sí" : "no")} " +
            $"vista={(VistaEscuchando ? "sí" : "no")} previa={(PreviaViva ? "sí" : "no")} " +
            $"posiciones={PosicionesRecordadas} vistas={VentanasVistas} " +
            $"gesto={(GestoEnCurso ? "sí" : "no")} marcas={MarcasDeGesto} " +
            $"sitios={SitiosRecordados} -> {(Limpio ? "LIMPIO" : "quedan " + Total)}";
    }

    // ================= Resistencia: los contadores del adaptador =================
    //
    // Los fallos seguidos se cuentan aquí y no en el adaptador porque el supervisor es quien
    // los interpreta, y porque así se pueden poner a cero sin tocar el adaptador.
    public static int FallosSeguidos { get; private set; }
    public static int ExcepcionesSeguidas { get; private set; }
    public static long TotalFallos { get; private set; }
    public static long TotalExcepciones { get; private set; }

    public static void SalioBien()
    {
        // Un acierto borra la racha. Si no la borrara, cinco fallos repartidos en una
        // semana apagarían la capa igual que cinco seguidos, y no es lo mismo.
        FallosSeguidos = 0;
        ExcepcionesSeguidas = 0;
    }

    public static void SalioMal() { FallosSeguidos++; TotalFallos++; }
    public static void Reventó() { ExcepcionesSeguidas++; TotalExcepciones++; }

    internal static void OlvidarContadores()
    {
        FallosSeguidos = ExcepcionesSeguidas = 0;
        TotalFallos = TotalExcepciones = 0;
    }
}
