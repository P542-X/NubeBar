using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NubeBar;

// NubeHypr: la capa de organización de ventanas.
//
// ---------- Qué es esto y qué NO es ----------
//
// Esto es la fase D0: el contrato de seguridad. No organiza ninguna ventana todavía y no
// cambia nada de lo que ves. Lo que deja es el interruptor (apagado), la lectura segura
// de su configuración, la lista de lo que no se puede tocar y las trazas con las que se
// podrá diagnosticar el motor cuando exista.
//
// La separación es obligatoria y no estética: aquí dentro NO se llama a Win32 ni se toca
// la interfaz. Se reciben datos y se devuelven decisiones. Es lo que permite comprobar
// cientos de casos sin abrir una ventana, y es lo que evita que un `callback` de WPF
// esconda una decisión importante — que es exactamente cómo se colaron el parpadeo de la
// vista general y el deslizador que se quedaba agarrado.
public static partial class NubeHypr
{
    // ================= El modo =================
    //
    // Tres estados y un orden de seguridad: apagado no hace nada, simulación calcula y
    // apunta sin mover, y aplicar mueve ventanas de verdad.
    public enum Modo
    {
        Apagado,        // la capa no existe: NubeBar se comporta como siempre
        Simulacion,     // calcula el destino, lo apunta, NO lo aplica
        Aplicar,        // mueve ventanas de verdad
    }

    // ---------- Cómo se lee el modo ----------
    //
    // Pura a propósito. Dos reglas que no son un detalle:
    //
    //  1. `Activo = false` manda sobre todo lo demás. Una configuración vieja —o editada
    //     a mano, o traída de otro equipo— no puede encender esta capa por accidente, y
    //     menos ponerla a mover ventanas.
    //
    //  2. Un modo que no se reconoce cae en SIMULACIÓN, no en aplicar. Si el fichero dice
    //     algo que no entendemos, lo que toca es calcular y apuntar, nunca empezar a
    //     mover las ventanas de alguien por si acaso.
    //
    // Y null significa "no elegido": se trata igual que un valor desconocido, no se le
    // esconde un valor mágico por detrás.
    public static Modo ModoEfectivo(bool activo, string? modo)
    {
        if (!activo) return Modo.Apagado;
        return (modo ?? "").Trim().ToLowerInvariant() switch
        {
            "apagado" => Modo.Apagado,
            "aplicar" => Modo.Aplicar,
            _ => Modo.Simulacion,
        };
    }

    // ¿Lo que dice el fichero es un modo que entendemos? Sirve para avisar en el log en
    // vez de aceptar un error en silencio.
    public static bool ModoConocido(string? modo) =>
        (modo ?? "").Trim().ToLowerInvariant() is "apagado" or "simulacion" or "aplicar";

    // ================= Lo que no se toca =================
    //
    // ---------- Por qué una lista y no "lo que parezca una ventana normal" ----------
    //
    // Mover la ventana equivocada no es un fallo visual: es dejar al usuario sin
    // escritorio, sin barra de tareas o sin el cuadro de UAC que estaba esperando una
    // respuesta. Así que la pregunta se invierte: en vez de "¿puedo mover esto?", se
    // pregunta "¿hay alguna razón para NO moverlo?", y la razón se devuelve escrita para
    // que el log diga por qué se descartó cada ventana.
    //
    // `GestorVentanas.EsDelUsuario` ya descarta lo que no es una ventana de usuario (sin
    // título, propietaria, tool window, clases del shell). Esto es otra cosa y va además:
    // hay ventanas que SÍ son del usuario y que aun así no se pueden mover.
    public const string PorqueNuestra = "es de NubeBar";
    public const string PorqueSistema = "es del sistema";
    public const string PorqueFondo = "pinta el fondo del escritorio";
    public const string PorqueElevada = "va elevada y Windows no nos deja";
    public const string PorqueSinSitio = "no tiene un rectángulo válido";

    // Procesos que nunca se organizan. Se compara por nombre de ejecutable, sin ruta y
    // sin distinguir mayúsculas.
    static readonly HashSet<string> ProcesosProtegidos = new(StringComparer.OrdinalIgnoreCase)
    {
        // Los nuestros
        "NubeBar",
        // El shell: el escritorio, la barra, el menú Inicio, la búsqueda, el conmutador
        "explorer", "StartMenuExperienceHost", "SearchHost", "SearchApp",
        "ShellExperienceHost", "ApplicationFrameHost.Shell", "TextInputHost",
        // Los fondos animados: mover su ventana deja el escritorio en negro
        "wallpaper32", "wallpaper64", "wallpaperservice32_c", "wallpaperservice64_c",
        // Lo que no se puede ni se debe tocar
        "consent",              // el cuadro de UAC
        "LockApp",              // la pantalla de bloqueo
        "LogonUI",
        "osk", "TabTip",        // el teclado en pantalla
        "Magnify",              // la lupa
        "PickerHost", "CredentialUIBroker",
        "dwm",                  // el compositor
    };

    // Clases de ventana que nunca se organizan, además de las que ya descarta el gestor.
    static readonly HashSet<string> ClasesProtegidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW",                       // el escritorio y la capa del fondo
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",  // las barras de Windows
        "Windows.UI.Core.CoreWindow",
        "MultitaskingViewFrame", "TaskSwitcherWnd", "TaskSwitcherOverlayWnd",
        "XamlExplorerHostIslandWindow",             // la vista de tareas de Windows 11
        "ForegroundStaging",
        "Shell_InputSwitchTopLevelWindow",
        "#32770.UAC",
    };

    // ---------- La decisión, pura ----------
    //
    // Se le dan los datos de una ventana y dice si se puede organizar. `nuestra` lo
    // decide quien llama (en la aplicación real, `Nativo.EsNuestra`), porque esta capa no
    // llama a Win32.
    public static bool SePuedeOrganizar(string? exe, string? clase, bool nuestra,
                                        bool sePuedeActivar, bool rectValido,
                                        out string porque)
    {
        if (nuestra) { porque = PorqueNuestra; return false; }

        string proceso = NombreDeProceso(exe);
        if (proceso.Length > 0 && ProcesosProtegidos.Contains(proceso))
        {
            // El fondo animado se nombra aparte porque el síntoma es muy distinto: no es
            // "una ventana del sistema", es "el escritorio se queda negro".
            porque = proceso.StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase)
                ? PorqueFondo : PorqueSistema;
            return false;
        }

        if (clase != null && ClasesProtegidas.Contains(clase)) { porque = PorqueSistema; return false; }
        if (!sePuedeActivar) { porque = PorqueElevada; return false; }
        if (!rectValido) { porque = PorqueSinSitio; return false; }

        porque = "";
        return true;
    }

    // "C:\Program Files\...\wallpaper64.exe" -> "wallpaper64". Sin ruta, sin extensión y
    // tolerante a que no haya ruta: muchas ventanas no dejan leer su ejecutable.
    public static string NombreDeProceso(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return "";
        try
        {
            var nombre = System.IO.Path.GetFileNameWithoutExtension(exe.Trim());
            return nombre ?? "";
        }
        catch { return ""; }
    }

    public static IReadOnlyCollection<string> Protegidos => ProcesosProtegidos;
    public static IReadOnlyCollection<string> ClasesProtegidasVisibles => ClasesProtegidas;

    // ================= El ciclo y sus trazas =================
    //
    // ---------- Para qué sirve numerar un ciclo ----------
    //
    // Un movimiento de ventana pasa por cuatro pasos —mirar, decidir, aplicar,
    // confirmar— y entre el primero y el último el mundo cambia: una ventana se cierra,
    // Windows manda un evento de hace 200 ms, el usuario suelta el ratón. Sin un número
    // que ate las cuatro líneas del log, un informe de "se movió la ventana que no era"
    // no se puede reconstruir.
    //
    // Y el número sirve para algo más que leer: `EsVieja` es la regla que impide que una
    // notificación atrasada sobrescriba un snapshot más nuevo. Es el mismo fallo que hizo
    // parpadear la vista general —recolocar la isla disparaba un evento que pedía
    // refrescar, que recolocaba la isla— y esta vez está escrito antes de que ocurra.
    static long _ultimaVersion;

    public static long SiguienteVersion() => ++_ultimaVersion;
    public static long UltimaVersion => _ultimaVersion;

    // Una actualización es vieja si su versión no es la última. Se descarta sin tocar
    // nada: no se intenta adivinar si "todavía vale".
    public static bool EsVieja(long version) => version != _ultimaVersion;

    // Solo para las pruebas: deja el contador como estaba.
    internal static void OlvidarVersion() => _ultimaVersion = 0;

    // ---------- Las cuatro líneas ----------
    //
    // El formato es el de la especificación, al pie de la letra, para que un log de una
    // sesión de un usuario se pueda leer con el documento al lado.
    public static string LineaSnapshot(long version, int ventanas, IntPtr foco) =>
        $"[D2]  snapshot={version} ventanas={ventanas} foco=0x{foco.ToInt64():X6}";

    public static string LineaPolitica(string layout, int monitor, Rect destino) =>
        $"[D2]  policy={layout} target=monitor:{monitor} " +
        $"rect={F(destino.X)},{F(destino.Y)},{F(destino.Ancho)},{F(destino.Alto)}";

    public static string LineaAplicar(IntPtr hwnd, string resultado, double ms) =>
        $"[D2]  apply hwnd=0x{hwnd.ToInt64():X6} result={resultado} " +
        $"elapsed={ms.ToString("0.0", CultureInfo.InvariantCulture)}ms";

    public static string LineaConfirmar(long version, int movidas, int saltadas, int viejas) =>
        $"[D2]  confirm version={version} moved={movidas} skipped={saltadas} stale={viejas}";

    public static string LineaGesto(string gesto, string estado, string razon) =>
        $"[D3]  gesture={gesto} state={estado} reason={razon}";

    public static string LineaSalud(bool modoSeguro, bool adaptadorListo, int versionConfig) =>
        $"[D7]  safe-mode={Si(modoSeguro)} adapter={(adaptadorListo ? "ready" : "stopped")} " +
        $"config-version={versionConfig}";

    static string Si(bool b) => b ? "true" : "false";
    static string F(double v) => Math.Round(v).ToString(CultureInfo.InvariantCulture);

    // ================= Geometría declarada =================
    //
    // ---------- Por qué un rectángulo propio ----------
    //
    // La especificación lo pide con todas las letras: "todas las coordenadas deben
    // declararse en un sistema y convertirse explícitamente". En NubeBar conviven los
    // píxeles de pantalla de `Nativo.RECT` y las unidades de WPF, y la mezcla silenciosa
    // de los dos es de donde salen los ajustes desplazados en un monitor al 150 %.
    //
    // Este `Rect` es de la capa de modelo y vive en PÍXELES DE PANTALLA, declarado aquí y
    // no en un comentario a medio camino. La conversión a unidades de WPF la hace el
    // adaptador, a la vista, y nunca al revés.
    public readonly record struct Rect(double X, double Y, double Ancho, double Alto)
    {
        public double Derecha => X + Ancho;
        public double Abajo => Y + Alto;
        public bool Valido => Ancho > 0 && Alto > 0;

        public static readonly Rect Nada = new(0, 0, 0, 0);

        // ¿Está dentro de otro, con una tolerancia de un píxel? La tolerancia no es
        // pereza: redondear un reparto de columnas deja diferencias de medio píxel y una
        // comprobación exacta las convertiría en falsos fallos.
        public bool DentroDe(Rect area, double tolerancia = 1.0) =>
            X >= area.X - tolerancia && Y >= area.Y - tolerancia &&
            Derecha <= area.Derecha + tolerancia && Abajo <= area.Abajo + tolerancia;

        public bool CasiIgual(Rect otro, double tolerancia = 1.0) =>
            Math.Abs(X - otro.X) <= tolerancia && Math.Abs(Y - otro.Y) <= tolerancia &&
            Math.Abs(Ancho - otro.Ancho) <= tolerancia && Math.Abs(Alto - otro.Alto) <= tolerancia;

        public override string ToString() => $"{F(X)},{F(Y)},{F(Ancho)},{F(Alto)}";
    }

    // ================= La comparativa de "apagado no cambia nada" =================
    //
    // ---------- La prueba que pide D0 ----------
    //
    // "Comparativa de geometría antes y después en modo apagado". Dicho de otro modo: con
    // el interruptor puesto pero el modo apagado, NINGUNA ventana puede haberse movido.
    //
    // Se compara por identidad de ventana, no por posición en la lista: una ventana que
    // se abre o se cierra entre las dos fotos no es una regresión, y confundir las dos
    // cosas haría que la prueba fallara sola.
    public readonly record struct Divergencia(IntPtr Hwnd, string App, Rect Antes, Rect Despues)
    {
        public override string ToString() => $"0x{Hwnd.ToInt64():X6} {App}: {Antes} -> {Despues}";
    }

    public static List<Divergencia> Comparar(
        IReadOnlyList<(IntPtr Hwnd, string App, Rect Rect)> antes,
        IReadOnlyList<(IntPtr Hwnd, string App, Rect Rect)> despues,
        double tolerancia = 1.0)
    {
        var mapa = despues.ToDictionary(v => v.Hwnd, v => v);
        var fuera = new List<Divergencia>();
        foreach (var a in antes)
        {
            if (!mapa.TryGetValue(a.Hwnd, out var d)) continue;   // se cerró: no es una regresión
            if (!a.Rect.CasiIgual(d.Rect, tolerancia))
                fuera.Add(new Divergencia(a.Hwnd, a.App, a.Rect, d.Rect));
        }
        return fuera;
    }
}
