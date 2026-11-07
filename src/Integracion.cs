using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NubeBar;

// La integración con Wallpaper Engine: abrirlo, cargar el fondo y coordinarlo con la
// escena de la nube.
//
// ---------- La regla de propiedad, que es toda la ética de esto ----------
//
// Wallpaper Engine es un programa de otro, que el usuario tenía abierto antes de que
// NubeBar existiera y que seguirá abierto después. De ahí la única regla de la que cuelga
// todo lo demás:
//
//     **NubeBar solo puede cerrar o pausar lo que NubeBar haya abierto.**
//
// Si ya estaba abierto, no es nuestro: no se cierra al salir, no se le cambia su
// configuración y no se le descarga el wallpaper que el usuario tenía puesto. Esto no es
// cortesía, es que lo contrario es destructivo y silencioso: alguien que use Wallpaper
// Engine con su playlist montada no puede descubrir que instalar una barra de tareas se
// la apaga al cerrarla.
//
// Y nunca `Kill`. Si no se va con `closeWallpaper`, se queda: matarlo le deja los ajustes
// a medio escribir y la próxima vez abre sin wallpaper.
//
// ---------- Por qué no se adivinan rutas ----------
//
// `WallpaperEngine.Localizar()` sabe rastrear las bibliotecas de Steam, y se sigue usando
// como SUGERENCIA. Pero lo que manda es la ruta que el usuario haya guardado: una
// instalación portable, un disco que hoy no está montado o dos copias de Steam hacen que
// adivinar acierte casi siempre y falle justo en la máquina de quien se queja. La ruta
// elegida se guarda, y si deja de existir se dice — no se vuelve a adivinar por detrás.
public static class Integracion
{
    public const string TodosLosMonitores = "todos";

    // ---------- Qué pasó con el proceso ----------
    //
    // Los cinco estados que se pidieron registrar, más el de "no tocado". Es un enum y no
    // un par de bool porque son excluyentes y porque el de salida depende de cuál sea:
    // `AlSalir` mira esto para decidir si tiene derecho a cerrar nada.
    public enum Estado
    {
        NoTocado,           // la integración está desactivada: no se ha mirado ni el proceso
        YaEstaba,           // estaba abierto antes que nosotros. NO es nuestro.
        LoAbrimosNosotros,  // lo abrimos y cargó su fondo
        AbiertoSinFondo,    // lo abrimos (o estaba) pero el fondo no llegó a tiempo
        FalloAlAbrir,       // no se pudo abrir: ruta mala, permisos, o no contestó
        CerradoPorNosotros, // lo cerramos o pausamos al salir
    }

    static Estado _estado = Estado.NoTocado;
    public static Estado Ahora => _estado;

    // Lo abrimos nosotros: es lo único que da derecho a cerrarlo o pausarlo al salir.
    // Lo lee `QueHacerAlSalir`, que es donde está la regla y donde tiene prueba.
    static bool _loAbrimos;

    // Qué monitores recibieron un wallpaper nuestro, para no descargar los que no.
    static readonly List<string> _nuestrosMonitores = new();

    internal static void Reiniciar()
    {
        _estado = Estado.NoTocado;
        _loAbrimos = false;
        _nuestrosMonitores.Clear();
    }

    // ================= La decisión, aparte y pura =================
    //
    // Qué hay que hacer antes de nada. Se saca del método asíncrono a propósito: es la
    // parte que tiene reglas y la que hay que poder comprobar sin abrir el programa de
    // nadie ni esperar ocho segundos.
    public enum Paso { NoHacerNada, SoloCargar, AbrirYCargar, NoSePuede }

    public static Paso QueHacer(bool integrar, bool iniciarConNubeBar, bool yaAbierto, bool hayExe)
    {
        if (!integrar) return Paso.NoHacerNada;
        if (yaAbierto) return Paso.SoloCargar;              // está abierto: solo se le pide el fondo
        if (!iniciarConNubeBar) return Paso.NoHacerNada;    // no está y no se nos autoriza a abrirlo
        if (!hayExe) return Paso.NoSePuede;
        return Paso.AbrirYCargar;
    }

    // Y qué se le dice al salir. Igual: una tabla, no un if enterrado en el cierre.
    public enum Salida { NoTocar, Pausar, Cerrar }

    public static Salida QueHacerAlSalir(string? alSalir, bool loAbrimosNosotros)
    {
        // Si no lo abrimos nosotros, NO se toca. Pase lo que diga la opción: la opción
        // dice qué hacer con lo nuestro, no con lo de los demás.
        if (!loAbrimosNosotros) return Salida.NoTocar;
        return (alSalir ?? "nada").Trim().ToLowerInvariant() switch
        {
            "pausar" => Salida.Pausar,
            "cerrar" => Salida.Cerrar,
            _ => Salida.NoTocar,
        };
    }

    // El tope de espera, acotado. Un 0 deja la escena esperando a nada y un 120 la deja
    // esperando más de lo que la propia escena aguanta abierta.
    public const double MinEspera = 1, MaxEspera = 30;

    public static double EsperaValida(double segundos) =>
        double.IsNaN(segundos) ? 8 : Math.Clamp(segundos, MinEspera, MaxEspera);

    // ---------- A qué monitor va cada cosa ----------
    //
    // Wallpaper Engine numera sus monitores por índice, y ese índice no tiene por qué
    // coincidir con el orden de `Monitores.Todos()`. Lo que se hace es traducir, y si el
    // monitor elegido ya no está —desconectado, cambiado de sitio— se cae a "todos" en
    // vez de mandar un índice que señala a otra pantalla. Mandar el índice equivocado
    // pondría el fondo en el monitor de al lado, que es peor que ponerlo en los dos.
    public static string MonitorParaWE(string? elegido, int cuantosHay, out string aviso)
    {
        aviso = "";
        var e = (elegido ?? TodosLosMonitores).Trim();
        if (e.Length == 0 || e.Equals(TodosLosMonitores, StringComparison.OrdinalIgnoreCase))
            return TodosLosMonitores;
        if (!int.TryParse(e, out int i) || i < 0)
        {
            aviso = $"\"{e}\" no es un número de monitor; se usan todos";
            return TodosLosMonitores;
        }
        if (i >= cuantosHay)
        {
            aviso = $"el monitor {i} ya no está (hay {cuantosHay}); se usan todos";
            return TodosLosMonitores;
        }
        return i.ToString();
    }

    // ================= Preparar =================

    public sealed record Resultado(Estado Estado, bool FondoListo, long Ms, string Detalle);

    // Abre Wallpaper Engine si toca, le pide el fondo y espera a que esté disponible.
    //
    // **No bloquea nada**: es async de verdad y la escena sigue animando en su hilo
    // mientras esto espera. Y tiene tope: si no contesta, se sigue adelante con lo que
    // haya. La pantalla no se queda negra nunca — eso lo garantiza la escena, que abre
    // igual al llegar a su propio tope de seguridad.
    public static async Task<Resultado> PrepararAsync(OpcionesWallpaper o, CancellationToken ct)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        Reiniciar();

        if (!string.IsNullOrWhiteSpace(o.Ruta)) WallpaperEngine.RutaManual = o.Ruta;

        bool yaAbierto = WallpaperEngine.Abierto;
        bool hayExe = WallpaperEngine.Exe != null;
        var paso = QueHacer(o.Integrar, o.IniciarConNubeBar, yaAbierto, hayExe);

        switch (paso)
        {
            case Paso.NoHacerNada:
                _estado = Estado.NoTocado;
                Log.Info("Wallpaper Engine: integración " +
                         (!o.Integrar ? "desactivada" : "sin autorización para abrirlo") + ", no se toca nada");
                return new Resultado(_estado, false, reloj.ElapsedMilliseconds, "no se hizo nada");

            case Paso.NoSePuede:
                _estado = Estado.FalloAlAbrir;
                Log.Info("Wallpaper Engine: la integración está activada pero no se encuentra el ejecutable. " +
                         "Elige la ruta en Ajustes > Fondo; no se adivina.");
                return new Resultado(_estado, false, reloj.ElapsedMilliseconds, "sin ejecutable");

            case Paso.SoloCargar:
                _estado = Estado.YaEstaba;
                _loAbrimos = false;
                Log.Info("Wallpaper Engine: ya estaba abierto. No es nuestro: no se cerrará al salir.");
                break;

            case Paso.AbrirYCargar:
                if (!WallpaperEngine.Abrir(out string porque))
                {
                    _estado = Estado.FalloAlAbrir;
                    Log.Info("Wallpaper Engine: no se pudo abrir (" + porque + ")");
                    return new Resultado(_estado, false, reloj.ElapsedMilliseconds, porque);
                }
                _loAbrimos = true;
                _estado = Estado.LoAbrimosNosotros;
                Log.Info("Wallpaper Engine: " + porque);
                // Un momento para que el proceso levante su canal de control: mandarle un
                // -control al milisegundo cero se pierde sin error.
                await EsperarAQueResponda(TimeSpan.FromSeconds(Math.Min(4, EsperaValida(o.SegundosEspera))), ct);
                break;
        }

        // ---------- Cargar lo que toque ----------
        int cuantos = Monitores.Todos().Count;
        var monitor = MonitorParaWE(o.MonitorDestino, cuantos, out string aviso);
        if (aviso.Length > 0) Log.Info("Wallpaper Engine: " + aviso);

        bool pedido = Cargar(o, monitor);
        if (pedido && !_nuestrosMonitores.Contains(monitor)) _nuestrosMonitores.Add(monitor);

        // ---------- Esperar a que el fondo esté ----------
        double tope = EsperaValida(o.SegundosEspera);
        bool listo = await EsperarFondo(o, tope, ct);

        if (!listo)
        {
            _estado = _loAbrimos ? Estado.AbiertoSinFondo : _estado;
            Log.Info($"Wallpaper Engine: el fondo no estaba listo en {tope:0.#} s. " +
                     "Se sigue: la escena revela el escritorio que haya.");
        }
        else _estado = _loAbrimos ? Estado.LoAbrimosNosotros : Estado.YaEstaba;

        return new Resultado(_estado, listo, reloj.ElapsedMilliseconds,
                             listo ? "fondo disponible" : "se agotó la espera");
    }

    static bool Cargar(OpcionesWallpaper o, string monitor)
    {
        if (!string.IsNullOrWhiteSpace(o.PlaylistElegida))
            return WallpaperEngine.AbrirPlaylist(o.PlaylistElegida!, monitor);
        if (!string.IsNullOrWhiteSpace(o.WallpaperElegido))
            return WallpaperEngine.AbrirWallpaper(o.WallpaperElegido!, monitor);
        Log.Info("Wallpaper Engine: no hay wallpaper ni playlist elegidos; se deja el que tenga puesto");
        return false;
    }

    static async Task EsperarAQueResponda(TimeSpan tope, CancellationToken ct)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        while (reloj.Elapsed < tope && !ct.IsCancellationRequested)
        {
            if (WallpaperEngine.Abierto) { await Task.Delay(250, ct).ConfigureAwait(false); return; }
            try { await Task.Delay(120, ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
        }
    }

    // ¿Hay fondo? Se pregunta a su propio config, que es lo que ya hacía NubeBar para la
    // paleta y lo que no cuesta un proceso por pregunta.
    static async Task<bool> EsperarFondo(OpcionesWallpaper o, double segundos, CancellationToken ct)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var tope = TimeSpan.FromSeconds(segundos);
        int monitorParaLeer = int.TryParse(o.MonitorDestino, out int mi) ? mi : o.Monitor;

        while (reloj.Elapsed < tope)
        {
            if (ct.IsCancellationRequested) return false;
            if (!WallpaperEngine.Abierto) { await Pausa(200, ct); continue; }
            try
            {
                var actual = await WallpaperEngine.WallpaperActual(monitorParaLeer).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(actual))
                {
                    Log.Info($"Wallpaper Engine: fondo disponible en {reloj.ElapsedMilliseconds} ms " +
                             $"(\"{WallpaperEngine.Titulo(actual!)}\")");
                    return true;
                }
            }
            catch (Exception ex) { Log.Error("preguntar por el wallpaper actual", ex); }
            if (!await Pausa(250, ct)) return false;
        }
        return false;
    }

    static async Task<bool> Pausa(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct).ConfigureAwait(false); return true; }
        catch (OperationCanceledException) { return false; }
    }

    // ================= Al salir =================
    //
    // Se llama desde el cierre de NubeBar. Solo toca lo nuestro.
    public static void AlSalir(OpcionesWallpaper o)
    {
        try
        {
            var que = QueHacerAlSalir(o.AlSalir, _loAbrimos);
            if (que == Salida.NoTocar)
            {
                Log.Info("Wallpaper Engine: no se toca al salir" +
                         (_loAbrimos ? " (así está configurado)" : " (no lo abrimos nosotros)"));
                return;
            }

            // Solo los monitores a los que NOSOTROS le pusimos algo.
            var destinos = _nuestrosMonitores.Count > 0 ? _nuestrosMonitores : new List<string> { TodosLosMonitores };
            foreach (var m in destinos)
            {
                if (que == Salida.Pausar) WallpaperEngine.Pausar(m);
                else WallpaperEngine.CerrarWallpaper(m);
            }

            _estado = Estado.CerradoPorNosotros;
            Log.Info($"Wallpaper Engine: {(que == Salida.Pausar ? "pausado" : "wallpaper cerrado")} al salir " +
                     $"en {destinos.Count} destino(s). El proceso NO se mata.");
        }
        catch (Exception ex) { Log.Error("Wallpaper Engine al salir", ex); }
    }

    // ================= Probar, desde Ajustes =================

    public sealed record Prueba(bool Ok, string Mensaje);

    public static Prueba Probar(OpcionesWallpaper o)
    {
        if (!string.IsNullOrWhiteSpace(o.Ruta)) WallpaperEngine.RutaManual = o.Ruta;

        if (WallpaperEngine.Exe is not { } exe)
            return new Prueba(false, "No encuentro wallpaper64.exe ni wallpaper32.exe. " +
                                     "Elige la ruta del ejecutable aquí abajo.");
        if (!WallpaperEngine.Abierto)
        {
            if (!o.IniciarConNubeBar)
                return new Prueba(false, "Wallpaper Engine no está abierto y no tengo permiso para abrirlo. " +
                                         "Marca «Iniciar Wallpaper Engine con NubeBar» o ábrelo tú.");
            if (!WallpaperEngine.Abrir(out string porque))
                return new Prueba(false, "No se pudo abrir: " + porque);
            _loAbrimos = true;
            _estado = Estado.LoAbrimosNosotros;
        }

        int cuantos = Monitores.Todos().Count;
        var monitor = MonitorParaWE(o.MonitorDestino, cuantos, out string aviso);

        if (string.IsNullOrWhiteSpace(o.PlaylistElegida) && string.IsNullOrWhiteSpace(o.WallpaperElegido))
            return new Prueba(false, "No has elegido wallpaper ni playlist.");

        bool ok = Cargar(o, monitor);
        var donde = monitor == TodosLosMonitores ? "todos los monitores" : "el monitor " + monitor;
        return new Prueba(ok, ok
            ? $"Orden enviada a {donde}." + (aviso.Length > 0 ? " " + aviso : "") +
              $" Ejecutable: {System.IO.Path.GetFileName(exe)}."
            : "Wallpaper Engine no aceptó la orden. Mira el log.");
    }
}
