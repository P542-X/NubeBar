using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NubeBar;

// El clima: lo que se puede decidir sin red delante.
//
// ---------- Por qué no estaba ----------
//
// Durante toda la fase E esto se dejó fuera a propósito, y el motivo escrito era bueno:
// «NubeBar no habla con ningún servicio meteorológico; inventar una tarjeta con un sol
// dibujado sería una maqueta». Seguía valiendo mientras no hubiera proveedor.
//
// Ahora lo hay, y el usuario ha pedido que se ponga.
//
// ---------- Qué sale de este equipo, exactamente ----------
//
// Una petición a `api.open-meteo.com` con **unas coordenadas y nada más**. Sin clave, sin
// registro, sin identificador de usuario, sin cookies. Open-Meteo es gratuito para uso no
// comercial y no pide cuenta.
//
// Y las coordenadas salen de una ciudad que **escribe el usuario**, no de su IP. Esa es la
// diferencia que importa: deducirla de la IP significaría mandar su dirección a un segundo
// servicio sin que él lo haya dicho, y además acertando a medias. Escribiendo «Madrid» se
// habla con un solo sitio, se acierta, y hasta que no lo escriba no sale **nada** de aquí.
//
// ---------- Y por qué la mitad de este fichero no toca la red ----------
//
// Traducir un código de la OMM a «Nubes dispersas», decidir cuándo toca volver a preguntar y
// escribir una temperatura son decisiones, y las decisiones se prueban sin red. Lo único que
// necesita internet es la llamada, y está sola al final.
public static class Clima
{
    // ---------- Cada cuánto se pregunta ----------
    //
    // El tiempo no cambia en un minuto, y el panel derecho se reconstruye cada vez que lo
    // abres o que pulsas «siguiente canción». Sin este freno, abrir y cerrar el panel diez
    // veces serían diez peticiones a un servicio gratuito, que es la forma de que lo dejen
    // de ser.
    public static readonly TimeSpan CadaCuanto = TimeSpan.FromMinutes(15);

    // Lo que se guarda de una lectura. `Cuando` es cuándo se pidió, para saber si caducó.
    public sealed record Lectura(double Grados, string Que, int Glifo, string Ciudad, DateTime Cuando);

    public static bool Caducada(Lectura? l, DateTime ahora) =>
        l == null || ahora - l.Cuando >= CadaCuanto;

    // ---------- Los códigos de la OMM ----------
    //
    // Open-Meteo devuelve un `weather_code` del estándar WMO 4677. No es una escala: son
    // grupos sueltos —61 es lluvia débil, 71 nieve débil, 95 tormenta— y hay huecos sin
    // asignar. Por eso es un `switch` por rangos y no una tabla.
    //
    // Lo que NO se hace es inventar: un código que no esté en la lista sale como «Sin datos»
    // en vez de caer en «Despejado», que sería enseñar sol con una tormenta fuera.
    //
    // ---------- Y los glifos están MIRADOS, no recordados ----------
    //
    // La primera versión de esta tabla llevaba 0xE9C4 para la lluvia y 0xE9C8 para la nieve,
    // elegidos de memoria. Ninguno de los dos existe en la fuente: habrían salido como dos
    // rectángulos vacíos el primer día de lluvia. Los cazó `GlifosQueExistenDeVerdad`, que se
    // escribió a la vez que esto precisamente para eso.
    //
    // Los de ahora se eligieron pintando la fuente entera en una hoja y mirándola. Conviene
    // saber por qué hizo falta: el candidato que «sonaba» a sol y nubes, 0xEA79, resultó ser
    // **una tortuga**. Existe en la fuente, así que la prueba lo habría dado por bueno; solo
    // se cae mirándolo. De ahí que estos comentarios digan la forma y no el nombre.
    public static (string Que, int Glifo) Tiempo(int codigo) => codigo switch
    {
        0 => ("Despejado", 0xE706),                      // un sol con rayos
        1 => ("Casi despejado", 0xE706),
        2 => ("Nubes dispersas", 0xE753),                // una nube
        3 => ("Nublado", 0xE753),
        45 or 48 => ("Niebla", 0xEDA8),                  // líneas onduladas horizontales
        >= 51 and <= 57 => ("Llovizna", 0xEA91),         // una nube con gotas debajo
        >= 61 and <= 67 => ("Lluvia", 0xEA91),
        >= 71 and <= 77 => ("Nieve", 0xEDAD),            // un copo de seis puntas
        >= 80 and <= 82 => ("Chubascos", 0xEA91),
        85 or 86 => ("Nevadas", 0xEDAD),
        >= 95 and <= 99 => ("Tormenta", 0xE945),         // un rayo
        _ => ("Sin datos", 0xE753),
    };

    // La temperatura como se dice: sin decimales. «18°», no «17,8 °C» — el decimal de una
    // temperatura del aire no informa de nada y ocupa un tercio de la línea.
    public static string Grados(double c) => $"{Math.Round(c)}°";

    // ---------- La ciudad, antes de preguntar ----------
    //
    // Una ciudad vacía o con un espacio no se manda: sería una petición segura de fallar, y
    // además el único caso en que no debe salir nada de este equipo es justamente cuando el
    // usuario no ha dicho dónde está.
    public static bool CiudadValida(string? c) =>
        !string.IsNullOrWhiteSpace(c) && c.Trim().Length >= 2;

    // ================= De aquí para abajo sí hay red =================
    //
    // Todo lo de arriba se prueba sin tocar internet. Lo que sigue es la llamada, y está sola
    // y al final a propósito: así una prueba que importe `Clima` no abre un socket por error,
    // y se ve de un vistazo qué parte del fichero puede fallar porque el wifi se cayó.

    public static Lectura? Ultima { get; private set; }
    public static string? Error { get; private set; }
    public static bool EnCurso { get; private set; }

    // Salta desde un hilo de fondo cuando hay dato nuevo o error nuevo. Quien lo escuche
    // tiene que volver al hilo de la interfaz él mismo (ver `MainWindow.Clima.cs`).
    public static event Action? Cambio;
    static void Avisar() { try { Cambio?.Invoke(); } catch { } }

    // Ocho segundos. Es un adorno de un panel: si el servicio tarda más, lo que toca es
    // enseñar el dato de antes y callarse, no dejar una petición colgada.
    static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    // ---------- Buscar la ciudad ----------
    //
    // Devuelve null si no se encuentra o si no hay red. Se llama **una vez** por ciudad: el
    // resultado se guarda como latitud y longitud en el config, y a partir de ahí solo se
    // pide el tiempo. Madrid no se muda.
    public static async Task<(double Lat, double Lon, string Nombre)?> BuscarCiudad(string ciudad)
    {
        if (!CiudadValida(ciudad)) return null;
        try
        {
            var url = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=es&format=json"
                    + "&name=" + Uri.EscapeDataString(ciudad.Trim());
            // `ConfigureAwait(false)`: ver la nota de `Refrescar`. Esto no toca la interfaz y
            // no debe exigir volver a su hilo para continuar.
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("results", out var r) ||
                r.ValueKind != JsonValueKind.Array || r.GetArrayLength() == 0) return null;

            var p = r[0];
            // El nombre lo devuelve el servicio, y es el que se guarda: así quien escribe
            // «madrid» acaba viendo «Madrid», y quien escribe algo ambiguo ve qué entendió.
            return (p.GetProperty("latitude").GetDouble(),
                    p.GetProperty("longitude").GetDouble(),
                    p.TryGetProperty("name", out var n) ? n.GetString() ?? ciudad : ciudad);
        }
        catch { return null; }
    }

    // ---------- Dónde estás, sin escribirlo ----------
    //
    // Pedido así: «estaría bien que detectase por IP». Se hace, pero NO con un servicio de
    // geolocalización por IP, y el motivo no es puntilloso:
    //
    // Un servicio de IP significa mandar tu dirección a un tercero más —uno que no tiene nada
    // que ver con el tiempo— solo para que te devuelva una ciudad. El de Windows ya lo tienes:
    // es del sistema, sale en Configuración → Privacidad → Ubicación, ahí puedes ver qué
    // aplicaciones lo usan y quitárselo, y en un equipo sin GPS resuelve por wifi e IP de
    // todas formas. Mismo resultado, un tercero menos, y bajo un interruptor que ya sabes
    // dónde está.
    //
    // Devuelve null si lo tienes desactivado, si lo deniegas o si falla. Null no es un error
    // que haya que esconder: es «no se ha podido, escribe la ciudad», y quien llama lo dice.
    public static async Task<(double Lat, double Lon, string Nombre)?> DondeEstoy()
    {
        try
        {
            var permiso = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
            if (permiso != Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            {
                Error = "Windows no da permiso de ubicación";
                return null;
            }

            // Precisión baja a propósito: para saber qué tiempo hace sobra con el municipio,
            // y pedir precisión alta enciende el GPS y gasta batería para afinar metros que
            // aquí no sirven de nada.
            var geo = new Windows.Devices.Geolocation.Geolocator
            {
                DesiredAccuracy = Windows.Devices.Geolocation.PositionAccuracy.Default,
            };
            var pos = await geo.GetGeopositionAsync();
            var p = pos.Coordinate.Point.Position;

            // El nombre, si Windows lo sabe. Muchas veces no, y entonces no se inventa: se
            // dice «Mi ubicación», que es verdad, en vez de poner una ciudad a ojo.
            string nombre = pos.CivicAddress?.City is { Length: > 0 } c ? c : "Mi ubicación";
            return (p.Latitude, p.Longitude, nombre);
        }
        catch (Exception ex)
        {
            Error = "No se ha podido leer la ubicación de Windows";
            Log.Info($"clima: ubicación no disponible ({ex.GetType().Name})");
            return null;
        }
    }

    // ---------- Pedir el tiempo ----------
    //
    // `aLaFuerza` se salta el freno de los 15 minutos; es para el botón de recargar, no para
    // el repintado del panel.
    public static async Task Refrescar(double lat, double lon, string ciudad, bool aLaFuerza = false)
    {
        if (EnCurso) return;
        if (!aLaFuerza && !Caducada(Ultima, DateTime.Now)) return;

        EnCurso = true; Avisar();
        try
        {
            var url = "https://api.open-meteo.com/v1/forecast?current=temperature_2m,weather_code"
                    + "&latitude=" + lat.ToString("0.####", CultureInfo.InvariantCulture)
                    + "&longitude=" + lon.ToString("0.####", CultureInfo.InvariantCulture);

            // ---------- `ConfigureAwait(false)`, y no es ceremonia ----------
            //
            // Sin esto, la continuación de este `await` exige volver al hilo que llamó. Si ese
            // hilo es el de la interfaz **y está bloqueado esperando a esta tarea**, los dos se
            // quedan esperándose: la petición no puede continuar porque el hilo está ocupado, y
            // el hilo no se libera porque espera a la petición.
            //
            // No es teórico: pasó la primera vez que se ejecutó `--diagclima`, que llama desde
            // `OnStartup` con un `GetResult()`. Se quedó colgado sin imprimir una línea.
            // `Clima` no toca la interfaz en ningún punto, así que no tiene por qué pedir su
            // hilo de vuelta — y no pidiéndolo, deja de poder provocar el abrazo.
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url).ConfigureAwait(false));
            var c = doc.RootElement.GetProperty("current");
            var (que, glifo) = Tiempo(c.GetProperty("weather_code").GetInt32());

            Ultima = new Lectura(c.GetProperty("temperature_2m").GetDouble(), que, glifo,
                                 ciudad, DateTime.Now);
            Error = null;
        }
        catch (Exception ex)
        {
            // El dato viejo NO se borra. Un corte de wifi de un minuto no es motivo para
            // dejar el hueco vacío: «18° · hace 20 min» informa más que nada.
            Error = ex is HttpRequestException or TaskCanceledException
                ? "No se llega al servicio del tiempo"
                : "No se entiende la respuesta del tiempo";
            Log.Info($"clima: {Error} ({ex.GetType().Name})");
        }
        finally { EnCurso = false; Avisar(); }
    }
}
