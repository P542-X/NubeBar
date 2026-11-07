using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D4: los espacios de trabajo, sin tocar la API.
//
// ---------- Por qué la política va aparte del adaptador ----------
//
// La API de escritorios virtuales de Windows **no es pública**: Microsoft le cambia los
// CLSID entre builds de Windows 11 y `Escritorios.cs` ya vive envuelto en try/catch por
// eso. Una capa que dependa de ella para *decidir* algo no se puede comprobar: no se puede
// provocar un renombrado externo, ni cinco escritorios, ni que la API devuelva nada a
// mitad de un cambio.
//
// Así que aquí está lo que decide —ordenar, nombrar sin repetir, elegir el vecino,
// reconciliar un cambio que vino de fuera, y el modo de un solo escritorio— y en
// `Escritorios.cs` lo que llama. La especificación lo pide con todas las letras: "usar la
// API disponible o una abstracción aislada; no inventar un mapa paralelo que se
// desincronice".
//
// ---------- Y por qué NO hay un mapa propio persistente ----------
//
// La especificación lo prohíbe explícitamente, y tiene razón: el escritorio de una ventana
// lo sabe Windows, y guardarlo nosotros crea dos verdades que se separan en cuanto el
// usuario mueve algo con `Win+Ctrl+→`. Lo que sí hay es una CACHÉ de lo último que
// contestó la API, con su marca de tiempo, para sobrevivir a que no contestara. Una caché
// que se sabe caché no es un mapa paralelo.
public static class HyprEspacios
{
    // ---------- Un espacio ----------
    //
    // `Orden` es el sitio que ocupa, empezando en 0. `Numero` es lo que se le enseña al
    // usuario, empezando en 1, porque nadie llama "escritorio 0" al primero.
    public readonly record struct Espacio(Guid Id, string Nombre, int Orden, bool EsElActual)
    {
        public int Numero => Orden + 1;
        public override string ToString() => $"{Numero}. {Nombre}{(EsElActual ? " (aquí)" : "")}";
    }

    // ================= Nombrar =================
    //
    // ---------- Los tres casos que hay que resolver ----------
    //
    // Un escritorio sin nombre (Windows los crea así), dos con el mismo nombre (el usuario
    // puede ponerlo, y Windows le deja), y un nombre puesto desde fuera mientras NubeBar
    // mira. Los tres se arreglan al presentar, no al guardar: lo que Windows tenga guardado
    // es suyo.
    public const string SinNombre = "Escritorio";

    // El nombre que se enseña. Un escritorio sin nombre no sale en blanco: sale como
    // "Escritorio 3", que es lo que hace Windows y lo que el usuario espera.
    public static string NombreVisible(string? nombre, int orden) =>
        string.IsNullOrWhiteSpace(nombre) ? $"{SinNombre} {orden + 1}" : nombre.Trim();

    // Un nombre que no choque con los que ya hay. Se le añade un número, como hace el
    // explorador con los archivos: "Trabajo", "Trabajo (2)", "Trabajo (3)".
    //
    // No es un capricho: dos espacios con el mismo rótulo son indistinguibles en el
    // indicador, y entonces "llevar esta ventana a Trabajo" no tiene una respuesta.
    public static string NombreLibre(IEnumerable<string> usados, string? propuesto)
    {
        var limpio = (propuesto ?? "").Trim();
        if (limpio.Length == 0) limpio = SinNombre;

        var tomados = new HashSet<string>(usados.Select(u => (u ?? "").Trim()),
                                          StringComparer.OrdinalIgnoreCase);
        if (!tomados.Contains(limpio)) return limpio;

        for (int n = 2; n < 1000; n++)
        {
            var intento = $"{limpio} ({n})";
            if (!tomados.Contains(intento)) return intento;
        }
        return limpio + " " + Guid.NewGuid().ToString("N")[..4];
    }

    // ---------- Validar un nombre que escribe el usuario ----------
    //
    // Se recorta y se limita. El límite no es estética: el indicador y el panel tienen un
    // ancho, y un nombre de doscientos caracteres no se corta solo, desmonta la fila.
    public const int MaxNombre = 40;

    public static bool NombreValido(string? nombre, out string limpio, out string porque)
    {
        limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0) { porque = "un nombre vacío no distingue nada"; return false; }
        if (limpio.Length > MaxNombre) { porque = $"más de {MaxNombre} caracteres no cabe"; return false; }
        if (limpio.Any(char.IsControl)) { porque = "no puede llevar saltos de línea"; return false; }
        porque = "";
        return true;
    }

    // ================= Ordenar y presentar =================
    //
    // Pone los espacios en su orden, les rellena el nombre y desambigua los repetidos.
    // Entra lo que diga la API —en el orden que lo diga— y sale lo que se puede enseñar.
    public static List<Espacio> Presentar(IEnumerable<(Guid Id, string? Nombre, bool EsElActual)> crudos)
    {
        var lista = new List<Espacio>();
        var usados = new List<string>();
        int i = 0;
        foreach (var (id, nombre, actual) in crudos)
        {
            var visible = NombreVisible(nombre, i);
            // Desambiguar al presentar y no al guardar: lo que Windows tenga guardado es
            // suyo, y reescribirle el nombre a un escritorio por nuestra cuenta sería
            // tocarle la configuración al usuario sin que lo pidiera.
            visible = NombreLibre(usados, visible);
            usados.Add(visible);
            lista.Add(new Espacio(id, visible, i, actual));
            i++;
        }
        return lista;
    }

    // ================= El vecino =================
    //
    // ---------- Por qué NO da la vuelta ----------
    //
    // En el último escritorio, "siguiente" no hace nada. Es lo que hace Windows con
    // `Win+Ctrl+→`, y cambiar ese comportamiento sorprendería: mantener pulsado el atajo
    // te llevaría del último al primero sin parar, que es la misma deriva que ya se evitó
    // en `MoverEnElOrden`.
    //
    // `darLaVuelta` existe para quien lo quiera —es una preferencia legítima— pero no es
    // el valor por defecto.
    public static Espacio? Vecino(IReadOnlyList<Espacio> espacios, bool siguiente,
                                   bool darLaVuelta = false)
    {
        if (espacios.Count == 0) return null;
        int i = IndiceDelActual(espacios);
        if (i < 0) return espacios[0];      // nadie se declara actual: el primero

        int j = i + (siguiente ? 1 : -1);
        if (j < 0 || j >= espacios.Count)
        {
            if (!darLaVuelta) return null;
            j = (j + espacios.Count) % espacios.Count;
        }
        if (j == i) return null;            // un solo espacio: no hay vecino
        return espacios[j];
    }

    public static int IndiceDelActual(IReadOnlyList<Espacio> espacios)
    {
        for (int i = 0; i < espacios.Count; i++) if (espacios[i].EsElActual) return i;
        return -1;
    }

    // El espacio número N, como lo cuenta el usuario (empezando en 1). Fuera de rango da
    // null en vez del más cercano: "ve al escritorio 7" cuando hay tres no es "ve al 3".
    public static Espacio? PorNumero(IReadOnlyList<Espacio> espacios, int numero) =>
        numero >= 1 && numero <= espacios.Count ? espacios[numero - 1] : null;

    // ================= Modo de un solo escritorio =================
    //
    // ---------- Qué pasa si la API no está ----------
    //
    // La especificación lo pide: "mantener un modo de un solo escritorio si falla". Y la
    // forma de hacerlo sin que el resto del sistema se llene de `if (hayApi)` es que la
    // política devuelva **un** espacio en vez de ninguno.
    //
    // Así el indicador enseña "1. Escritorio 1", los comandos de cambiar de espacio no
    // encuentran vecino y no hacen nada, y las ventanas se cuentan todas en el único
    // espacio que hay. Nada que programar dos veces, y ningún camino muerto.
    public static readonly Guid IdUnico = new("00000000-0000-0000-0000-000000000001");

    public static List<Espacio> UnSoloEscritorio() =>
        new() { new Espacio(IdUnico, $"{SinNombre} 1", 0, true) };

    public static bool EsElModoDeUno(IReadOnlyList<Espacio> espacios) =>
        espacios.Count == 1 && espacios[0].Id == IdUnico;

    // ================= El mapa de ventanas =================
    //
    // ---------- Tolerante a los retrasos del sistema ----------
    //
    // Preguntar "¿en qué escritorio está esta ventana?" justo durante un cambio devuelve
    // cosas raras: nada, el escritorio de antes, o una excepción. La respuesta correcta a
    // eso NO es "no lo sé y la escondo": es "lo último que me dijiste", con su marca de
    // cuándo.
    //
    // Y si nunca ha contestado, se da por que está en el actual. Más vale enseñar una
    // ventana de más que esconder una que existe — es la misma regla que ya tenía
    // `Escritorios.EstaEnElActual`.
    public sealed class Mapa
    {
        readonly Dictionary<IntPtr, (Guid Espacio, DateTime Cuando)> _ultimo = new();

        public int Cuantas => _ultimo.Count;

        // La API contestó: se apunta.
        public void Apuntar(IntPtr hwnd, Guid espacio)
        {
            if (hwnd == IntPtr.Zero || espacio == Guid.Empty) return;
            _ultimo[hwnd] = (espacio, DateTime.UtcNow);
        }

        // La API no contestó: se usa lo último, y se dice que es de hace rato.
        public (Guid Espacio, bool EsReciente, bool EsSabido) Consultar(
            IntPtr hwnd, Guid actual, TimeSpan caduca)
        {
            if (!_ultimo.TryGetValue(hwnd, out var q)) return (actual, false, false);
            bool reciente = DateTime.UtcNow - q.Cuando <= caduca;
            return (q.Espacio, reciente, true);
        }

        public void Olvidar(IntPtr hwnd) => _ultimo.Remove(hwnd);

        // Las ventanas que ya no existen no pueden estar en ningún escritorio. Sin esto,
        // una sesión larga acumula handles muertos.
        public int Limpiar(IEnumerable<IntPtr> vivas)
        {
            var hay = new HashSet<IntPtr>(vivas);
            var muertas = _ultimo.Keys.Where(h => !hay.Contains(h)).ToList();
            foreach (var h in muertas) _ultimo.Remove(h);
            return muertas.Count;
        }

        public void Vaciar() => _ultimo.Clear();
    }

    // Cuánto se fía de una respuesta vieja. Dos segundos: un cambio de escritorio tarda
    // unas décimas, así que algo de hace dos segundos sigue valiendo, y algo de hace un
    // minuto ya no dice nada.
    public static readonly TimeSpan Caducidad = TimeSpan.FromSeconds(2);

    // ================= Recuperarse de un cambio de fuera =================
    //
    // ---------- El caso que importa ----------
    //
    // El usuario pulsa `Win+Ctrl+→`, o crea un escritorio desde la vista de tareas, o le
    // cambia el nombre. NubeHypr no se ha enterado y su idea del mundo es de antes.
    //
    // Esto compara las dos fotos y dice **qué** cambió, no solo que algo cambió: con
    // "algo cambió" la única reacción posible es rehacerlo todo, y rehacerlo todo es como
    // se llega al parpadeo.
    public readonly record struct Cambio(
        bool CambioElActual, List<Guid> Nuevos, List<Guid> Quitados, List<Guid> Renombrados,
        bool CambioElOrden)
    {
        public bool Nada => !CambioElActual && Nuevos.Count == 0 && Quitados.Count == 0 &&
                            Renombrados.Count == 0 && !CambioElOrden;

        // ¿Hace falta rehacer el reparto? Solo si cambió el escritorio en el que estamos o
        // si desapareció alguno. Un renombrado no mueve ninguna ventana, y repintar el
        // reparto por un nombre nuevo es trabajo tirado.
        public bool TocaRepartir => CambioElActual || Quitados.Count > 0;
    }

    public static Cambio Comparar(IReadOnlyList<Espacio> antes, IReadOnlyList<Espacio> ahora)
    {
        var idsAntes = antes.Select(e => e.Id).ToList();
        var idsAhora = ahora.Select(e => e.Id).ToList();

        var nuevos = idsAhora.Where(id => !idsAntes.Contains(id)).ToList();
        var quitados = idsAntes.Where(id => !idsAhora.Contains(id)).ToList();

        var renombrados = new List<Guid>();
        foreach (var a in antes)
        {
            var b = ahora.FirstOrDefault(x => x.Id == a.Id);
            if (b.Id != Guid.Empty && b.Nombre != a.Nombre) renombrados.Add(a.Id);
        }

        var actualAntes = antes.FirstOrDefault(e => e.EsElActual).Id;
        var actualAhora = ahora.FirstOrDefault(e => e.EsElActual).Id;
        bool cambioElActual = actualAntes != actualAhora;

        // El orden de los que siguen estando. Si entró o salió alguno, que el orden
        // "cambie" es obvio y no es información: solo se mira cuando el conjunto es el
        // mismo.
        bool cambioElOrden = false;
        if (nuevos.Count == 0 && quitados.Count == 0)
            cambioElOrden = !idsAntes.SequenceEqual(idsAhora);

        return new Cambio(cambioElActual, nuevos, quitados, renombrados, cambioElOrden);
    }

    // ================= Resolver un comando de espacio =================
    //
    // ---------- Por qué hace falta un paso más ----------
    //
    // `AlVecino` dice la intención y no el destino: cuál es "el siguiente" depende de
    // cuántos escritorios haya AHORA, y eso lo sabe la API, no el atajo. Esto es el paso
    // que traduce la intención en un escritorio concreto, con la lista de verdad delante.
    //
    // Devuelve null cuando no se puede: en el último escritorio no hay siguiente, y un
    // número que no existe no es el más cercano. Null significa "no se hace nada", no
    // "haz lo que puedas".
    public readonly record struct Destino(Espacio Espacio, bool LlevarLaVentana, string Porque);

    public static Destino? Resolver(IReadOnlyList<Espacio> espacios, Comando comando,
                                    bool darLaVuelta = false)
    {
        switch (comando)
        {
            case Comando.AlVecino v:
            {
                // En el modo de un solo escritorio no hay vecino, y eso no es un fallo: es
                // la respuesta correcta cuando la API no está o hay un solo escritorio.
                var vecino = Vecino(espacios, v.Siguiente, darLaVuelta);
                if (vecino == null)
                    return null;
                return new Destino(vecino.Value, v.LlevarLaVentana,
                                   v.Siguiente ? "al siguiente" : "al anterior");
            }
            case Comando.AlNumero n:
            {
                var e = PorNumero(espacios, n.Numero);
                if (e == null) return null;
                if (e.Value.EsElActual) return null;     // ya estabas ahí
                return new Destino(e.Value, n.LlevarLaVentana, $"al número {n.Numero}");
            }
            case Comando.CambiarEspacio c:
            {
                // El comando de D1 cuenta desde 0; el usuario desde 1. La conversión va
                // aquí, en un solo sitio, y no en cada sitio que lo llame.
                var e = PorNumero(espacios, c.Espacio + 1);
                if (e == null) return null;
                if (e.Value.EsElActual) return null;
                return new Destino(e.Value, c.LlevarLaVentana, $"al índice {c.Espacio}");
            }
            default:
                return null;
        }
    }
}
