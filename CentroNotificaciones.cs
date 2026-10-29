using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NubeBar;

// Una notificación tal y como la guarda el centro: lo que leyó `Notificaciones` de la
// base de Windows, más lo nuestro — si la has leído y si la has descartado.
public sealed class Anotacion
{
    public long Id;
    public string App = "";
    public string AumId = "";
    public string Titulo = "";
    public string Texto = "";
    public DateTime Cuando;
    public bool Leida;
    public IReadOnlyList<Notificaciones.AccionRapida> Acciones = Array.Empty<Notificaciones.AccionRapida>();

    // "ahora", "hace 5 min", "13:42", "ayer 09:10". Lo que pondría una persona.
    public string Momento(DateTime ahora)
    {
        var va = ahora - Cuando;
        if (va < TimeSpan.Zero) va = TimeSpan.Zero;
        if (va.TotalMinutes < 1) return "ahora";
        if (va.TotalMinutes < 60) return $"hace {(int)va.TotalMinutes} min";
        if (Cuando.Date == ahora.Date) return Cuando.ToString("HH:mm");
        if (Cuando.Date == ahora.Date.AddDays(-1)) return "ayer " + Cuando.ToString("HH:mm");
        return Cuando.ToString("d MMM HH:mm");
    }
}

// Las notificaciones de una app, juntas.
public sealed record GrupoAviso(string App, string AumId, IReadOnlyList<Anotacion> Avisos)
{
    public int NoLeidas => Avisos.Count(a => !a.Leida);
    public DateTime Ultima => Avisos.Count == 0 ? DateTime.MinValue : Avisos.Max(a => a.Cuando);
}

// El centro de notificaciones: el historial y lo que se puede hacer con él.
//
// Es una CLASE y no un puñado de estáticos, al contrario que `Notificaciones` o `Uso`,
// y la diferencia importa: así una prueba puede crear el suyo, meterle avisos de
// mentira y comprobar el contador de no leídas sin tocar el del usuario ni su disco.
// La aplicación usa uno solo, `Centro.Actual`.
//
// Lo que NO hace: hablar con Windows. Quien lee la base es `Notificaciones`; aquí solo
// entran avisos por `Recibir`. Eso es lo que lo deja comprobable.
public sealed class CentroNotificaciones
{
    // Cuántas se guardan. Doscientas son varios días de uso normal y caben de sobra en
    // el desplazamiento del panel; más allá, lo que hay es ruido.
    public const int Tope = 200;

    readonly List<Anotacion> _avisos = new();
    readonly HashSet<long> _descartadas = new();
    readonly HashSet<string> _silenciadas = new(StringComparer.OrdinalIgnoreCase);

    public bool NoMolestar { get; set; }

    // Avisa de que algo ha cambiado: lo usa la isla para repintar la chapita sin
    // preguntar cada segundo.
    public event Action? Cambio;

    public IReadOnlyList<Anotacion> Todas => _avisos;
    public int Cuantas => _avisos.Count;
    public int NoLeidas => _avisos.Count(a => !a.Leida);
    public bool Vacio => _avisos.Count == 0;

    public IReadOnlyCollection<string> Silenciadas => _silenciadas;

    // ---------- Entrada ----------

    // Mete avisos nuevos. Devuelve los que de verdad se quedaron, que es lo que la isla
    // tiene que enseñar como aviso emergente: los descartados, los repetidos y los de
    // una app silenciada no vuelven.
    //
    // El "No molestar" NO los tira: los guarda sin enseñarlos. Es la diferencia entre
    // "ahora no me interrumpas" y "no quiero saber nada", y la primera es la que pide
    // un centro de notificaciones — si no, el modo silencio se come tu historial.
    public List<Anotacion> Recibir(IEnumerable<Notificaciones.Aviso> llegan)
    {
        var nuevas = new List<Anotacion>();
        foreach (var a in llegan)
        {
            if (_descartadas.Contains(a.Id)) continue;
            if (_avisos.Any(x => x.Id == a.Id)) continue;
            if (EstaSilenciada(a.AumId)) continue;

            var n = new Anotacion
            {
                Id = a.Id,
                App = a.App,
                AumId = a.AumId,
                Titulo = a.Titulo,
                Texto = a.Texto,
                Cuando = a.Cuando,
                Acciones = a.Acciones ?? Array.Empty<Notificaciones.AccionRapida>()
            };
            _avisos.Add(n);
            nuevas.Add(n);
        }

        if (nuevas.Count == 0) return nuevas;

        // Las más nuevas arriba, y se recorta por abajo
        _avisos.Sort((x, y) => y.Cuando.CompareTo(x.Cuando));
        if (_avisos.Count > Tope) _avisos.RemoveRange(Tope, _avisos.Count - Tope);

        Cambio?.Invoke();
        return nuevas;
    }

    // ---------- Lo que se puede hacer con una ----------

    public bool MarcarLeida(long id)
    {
        var a = _avisos.FirstOrDefault(x => x.Id == id);
        if (a == null || a.Leida) return false;
        a.Leida = true;
        Cambio?.Invoke();
        return true;
    }

    public int MarcarTodasLeidas()
    {
        int n = 0;
        foreach (var a in _avisos) if (!a.Leida) { a.Leida = true; n++; }
        if (n > 0) Cambio?.Invoke();
        return n;
    }

    // Descartar es para siempre: se apunta el id para que no vuelva cuando se relea la
    // base de Windows, que sigue teniéndola.
    public bool Descartar(long id)
    {
        int i = _avisos.FindIndex(x => x.Id == id);
        if (i < 0) return false;
        _avisos.RemoveAt(i);
        Recordar(id);
        Cambio?.Invoke();
        return true;
    }

    public int BorrarTodo()
    {
        int n = _avisos.Count;
        foreach (var a in _avisos) Recordar(a.Id);
        _avisos.Clear();
        if (n > 0) Cambio?.Invoke();
        return n;
    }

    // Los ids descartados también tienen tope: son números, pero el archivo es del
    // usuario y no tiene por qué crecer sin fin. Se quedan los más altos, que son los
    // más recientes — el "Order" de Windows solo sube.
    void Recordar(long id)
    {
        _descartadas.Add(id);
        if (_descartadas.Count <= Tope * 4) return;
        var quedan = _descartadas.OrderByDescending(x => x).Take(Tope * 2).ToList();
        _descartadas.Clear();
        foreach (var x in quedan) _descartadas.Add(x);
    }

    // ---------- Silenciar una app ----------
    //
    // Se compara por AumId y por el nombre bonito: el usuario silencia "Discord" desde
    // una notificación suya, y lo que se guarda es su AumId entero. Pero el config
    // antiguo (`Barra.NotificacionesSilenciadas`) guardaba trozos sueltos, así que la
    // comparación es "contiene", igual que antes.
    public bool EstaSilenciada(string aumId) =>
        !string.IsNullOrEmpty(aumId) &&
        _silenciadas.Any(s => s.Length > 0 && aumId.Contains(s, StringComparison.OrdinalIgnoreCase));

    public void Silenciar(string aumId, bool si)
    {
        if (string.IsNullOrWhiteSpace(aumId)) return;
        bool cambio = si ? _silenciadas.Add(aumId) : _silenciadas.Remove(aumId);
        // Silenciar no borra lo que ya estaba: lo que no quieres es que te interrumpa
        // otra vez, no perder el historial de lo que ya te avisó.
        if (cambio) Cambio?.Invoke();
    }

    // ---------- Agrupar ----------
    //
    // Por app, con las apps ordenadas por lo reciente que es su última notificación, y
    // dentro de cada app las más nuevas arriba. Es el orden de Plasma y es el único que
    // no te obliga a buscar: lo que acaba de pasar, lo primero.
    public List<GrupoAviso> PorApp()
    {
        return _avisos
            .GroupBy(a => a.AumId.Length > 0 ? a.AumId : a.App, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GrupoAviso(
                g.First().App,
                g.First().AumId,
                g.OrderByDescending(a => a.Cuando).ThenByDescending(a => a.Id).ToList()))
            .OrderByDescending(g => g.Ultima)
            .ThenBy(g => g.App, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---------- Guardar lo nuestro ----------
    //
    // En su propio archivo, al lado de `uso.json` y NO dentro del config del usuario.
    // Dos razones: el config lo edita la gente a mano y no tiene por qué llenarse de
    // identificadores, y guardarlo ahí dispararía la recarga en caliente cada vez que
    // marcas una notificación como leída.
    sealed class EnDisco
    {
        public bool NoMolestar { get; set; }
        public List<string> Silenciadas { get; set; } = new();
        public List<long> Descartadas { get; set; } = new();
        public List<long> Leidas { get; set; } = new();
    }

    public static string Ruta => Path.Combine(Config.Carpeta, "notificaciones.json");

    public void Cargar(string? ruta = null)
    {
        try
        {
            var r = ruta ?? Ruta;
            if (!File.Exists(r)) return;
            var d = JsonSerializer.Deserialize<EnDisco>(File.ReadAllText(r));
            if (d == null) return;

            NoMolestar = d.NoMolestar;
            _silenciadas.Clear();
            foreach (var s in d.Silenciadas) if (!string.IsNullOrWhiteSpace(s)) _silenciadas.Add(s);
            _descartadas.Clear();
            foreach (var x in d.Descartadas) _descartadas.Add(x);

            // Las leídas se aplican sobre lo que haya cargado ya, y se recuerdan para
            // las que lleguen después: al releer la base de Windows vuelven las mismas.
            _leidasRecordadas.Clear();
            foreach (var x in d.Leidas) _leidasRecordadas.Add(x);
            foreach (var a in _avisos) if (_leidasRecordadas.Contains(a.Id)) a.Leida = true;
        }
        catch (Exception ex) { Log.Error("leer notificaciones.json", ex); }
    }

    readonly HashSet<long> _leidasRecordadas = new();

    public void Guardar(string? ruta = null)
    {
        try
        {
            var r = ruta ?? Ruta;
            Directory.CreateDirectory(Path.GetDirectoryName(r)!);
            var d = new EnDisco
            {
                NoMolestar = NoMolestar,
                Silenciadas = _silenciadas.ToList(),
                Descartadas = _descartadas.OrderByDescending(x => x).Take(Tope * 2).ToList(),
                Leidas = _avisos.Where(a => a.Leida).Select(a => a.Id)
                                .Concat(_leidasRecordadas)
                                .Distinct().OrderByDescending(x => x).Take(Tope * 2).ToList()
            };
            ConfigArbol.EscribirAtomico(r, JsonSerializer.Serialize(d, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            }));
        }
        catch (Exception ex) { Log.Error("guardar notificaciones.json", ex); }
    }

    // Al cargar avisos después de haber leído el archivo, hay que volver a aplicar lo
    // que se sabía: `Recibir` crea anotaciones nuevas y no sabe nada de lo de antes.
    public void AplicarLoGuardado()
    {
        foreach (var a in _avisos) if (_leidasRecordadas.Contains(a.Id)) a.Leida = true;
    }

    // Solo para las pruebas: deja el centro como recién hecho.
    internal void Vaciar()
    {
        _avisos.Clear();
        _descartadas.Clear();
        _silenciadas.Clear();
        _leidasRecordadas.Clear();
        NoMolestar = false;
    }
}

// El centro de la aplicación. Uno solo, como `Uso`: dos islas en dos monitores
// comparten historial, porque las notificaciones son del equipo y no de la pantalla.
public static class Centro
{
    public static readonly CentroNotificaciones Actual = new();

    // Se llama una vez al arrancar, detrás de Notificaciones.Arrancar().
    //
    // Lo pequeño se hace aquí y lo caro por detrás. Leer el historial copia la base de
    // notificaciones de Windows y parsea doscientos XML, y esto corre en el hilo que
    // está montando la isla: es exactamente el tipo de cosa que en C2 costó segundo y
    // medio de arranque. Así que el archivo nuestro —que son cuatro listas— se lee aquí
    // mismo, y el historial llega cuando llegue.
    //
    // La tarea de fondo NO toca el centro: devuelve lo leído y quien lo mete es el hilo
    // de interfaz. El centro no es seguro entre hilos a propósito, porque no tiene por
    // qué serlo si solo lo toca uno.
    public static void Arrancar(IEnumerable<string>? silenciadasDelConfig = null,
                                System.Windows.Threading.Dispatcher? interfaz = null)
    {
        try
        {
            Actual.Cargar();
            // Lo que ya estuviera silenciado en `Barra.NotificacionesSilenciadas` sigue
            // valiendo: era la única forma de silenciar una app antes de que existiera
            // el centro, y quien la usó no tiene por qué volver a hacerlo.
            if (silenciadasDelConfig != null)
                foreach (var s in silenciadasDelConfig) Actual.Silenciar(s, true);

            var ui = interfaz ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            System.Threading.Tasks.Task.Run(() =>
            {
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                var historial = Notificaciones.Historial(CentroNotificaciones.Tope);
                reloj.Stop();
                ui.BeginInvoke(new Action(() =>
                {
                    Actual.Recibir(historial);
                    Actual.AplicarLoGuardado();
                    Log.Info($"centro de notificaciones: {Actual.Cuantas} en el historial, " +
                             $"{Actual.NoLeidas} sin leer, no molestar={Actual.NoMolestar} " +
                             $"(leerlo costó {reloj.Elapsed.TotalMilliseconds:0} ms, fuera del arranque)");
                }));
            });
        }
        catch (Exception ex) { Log.Error("arrancar el centro de notificaciones", ex); }
    }
}
