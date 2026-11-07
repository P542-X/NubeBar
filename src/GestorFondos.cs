using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NubeBar;

// El gestor de fondos: lo que de verdad pone el fondo del escritorio.
//
// `Fondos.cs` es el modelo y las cuentas puras —qué tipo, qué ajuste, qué imagen toca,
// qué monitor es cuál—. Esto es el runtime: dibuja lo que haya que dibujar, aplica, lleva
// el reloj de la presentación y sabe volver atrás.
//
// ---------- Lo que Windows no sabe hacer ----------
//
// Windows solo sabe de ficheros y de un color plano. No sabe poner un degradado. Así que
// el sólido va por `SetBackgroundColor` —que no gasta disco— y el degradado **se dibuja**
// a un PNG en `%LOCALAPPDATA%\NubeBar\fondos` y se pone como imagen.
//
// Ese PNG es un intermedio, no configuración: no va a config.json, se puede borrar sin
// perder nada y se vuelve a dibujar. Y el nombre lleva la huella de sus parámetros, así
// que cambiar el degradado no reescribe el de antes: escribe otro y el viejo se recoge
// solo cuando sobra.
//
// ---------- Y el reloj de la presentación ----------
//
// **Uno solo, de la aplicación, y no de la ventana de ajustes.** Esto importa: si el reloj
// naciera al abrir Ajustes, abrir y cerrar Ajustes cinco veces dejaría cinco relojes
// cambiando el fondo a destiempo. Vive aquí, se arranca una vez y se para cuando el tipo
// de fondo deja de ser una presentación.
public static class GestorFondos
{
    // ================= El dibujo, en cuentas puras =================
    //
    // Los píxeles de un degradado lineal. Aparte y pura a propósito: así se puede
    // comprobar que el degradado va de un color al otro y que el ángulo hace algo, sin
    // necesidad de un hilo de interfaz ni de mirar una imagen.
    //
    // Formato Bgra32, que es lo que quiere `BitmapSource.Create` sin conversiones.
    public static byte[] PixelesDeDegradado(int ancho, int alto, Color a, Color b, double grados)
    {
        if (ancho <= 0 || alto <= 0) return Array.Empty<byte>();
        var px = new byte[ancho * alto * 4];

        // El eje del degradado. 0° = de izquierda a derecha; 90° = de arriba abajo.
        double rad = grados * Math.PI / 180.0;
        double ex = Math.Cos(rad), ey = Math.Sin(rad);

        // Se proyecta cada punto sobre el eje y se normaliza con la proyección de las
        // cuatro esquinas: así el degradado cubre exactamente la imagen para cualquier
        // ángulo, en vez de cortarse en diagonal.
        double[] esquinas =
        {
            0 * ex + 0 * ey,
            (ancho - 1) * ex + 0 * ey,
            0 * ex + (alto - 1) * ey,
            (ancho - 1) * ex + (alto - 1) * ey,
        };
        double min = esquinas.Min(), max = esquinas.Max();
        double rango = max - min;
        if (Math.Abs(rango) < 1e-9) rango = 1;

        for (int y = 0; y < alto; y++)
        {
            double py = y * ey;
            int fila = y * ancho * 4;
            for (int x = 0; x < ancho; x++)
            {
                double t = (x * ex + py - min) / rango;
                if (t < 0) t = 0; else if (t > 1) t = 1;
                int i = fila + x * 4;
                px[i + 0] = (byte)Math.Round(a.B + (b.B - a.B) * t);
                px[i + 1] = (byte)Math.Round(a.G + (b.G - a.G) * t);
                px[i + 2] = (byte)Math.Round(a.R + (b.R - a.R) * t);
                px[i + 3] = 255;
            }
        }
        return px;
    }

    // El nombre del intermedio, a partir de sus parámetros. Determinista: los mismos
    // parámetros dan el mismo fichero, así que no se redibuja lo que ya está.
    public static string NombreDeIntermedio(string tipo, string c1, string c2, double angulo, int w, int h)
    {
        unchecked
        {
            int hash = 17;
            foreach (var s in new[] { tipo, c1, c2, angulo.ToString("0.##"), w + "x" + h })
                foreach (var ch in s) hash = hash * 31 + ch;
            return $"{tipo}-{w}x{h}-{(uint)hash:X8}.png";
        }
    }

    // Dibuja (o reutiliza) el PNG de un sólido o un degradado. Devuelve la ruta.
    public static string? Dibujar(string tipo, string color1, string color2, double angulo, int ancho, int alto)
    {
        try
        {
            // Un fondo plano o un degradado no necesitan la resolución real: se escalan sin
            // perder nada. Se acota para no escribir un PNG de 8K por gusto.
            ancho = Math.Clamp(ancho, 16, 3840);
            alto = Math.Clamp(alto, 16, 2160);

            Directory.CreateDirectory(Fondos.CarpetaIntermedios);
            var nombre = NombreDeIntermedio(tipo, color1, color2, angulo, ancho, alto);
            var ruta = Path.Combine(Fondos.CarpetaIntermedios, nombre);
            if (File.Exists(ruta)) return ruta;

            var a = Paleta.Leer(color1, Colors.Black);
            var b = tipo == "solido" ? a : Paleta.Leer(color2, a);
            var px = PixelesDeDegradado(ancho, alto, a, b, angulo);
            if (px.Length == 0) return null;

            var mapa = BitmapSource.Create(ancho, alto, 96, 96, PixelFormats.Bgra32, null, px, ancho * 4);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(mapa));

            // A un temporal y luego al sitio: si el proceso muere a mitad, no queda un PNG
            // roto con el nombre bueno que luego se daría por válido y no se redibujaría.
            var tmp = ruta + ".tmp";
            using (var f = File.Create(tmp)) enc.Save(f);
            if (File.Exists(ruta)) File.Delete(ruta);
            File.Move(tmp, ruta);
            Log.Info($"fondo: {tipo} dibujado en {nombre}");
            return ruta;
        }
        catch (Exception ex) { Log.Error($"dibujar el fondo {tipo}", ex); return null; }
    }

    // ================= La instantánea, para poder volver =================

    public static string RutaInstantanea =>
        Path.Combine(Fondos.CarpetaIntermedios, "fondo-anterior.json");

    sealed class Anterior
    {
        public Dictionary<string, string> PorMonitor { get; set; } = new();
        public string? Color { get; set; }
        public string? Cuando { get; set; }
    }

    // Guarda lo que hay puesto AHORA. Se llama antes de aplicar, una vez: si ya hay una
    // instantánea no se sobrescribe, porque la que vale es la de antes de que NubeBar
    // empezara a tocar el fondo, no la del último cambio.
    public static bool GuardarInstantanea(bool soloSiNoHay = true)
    {
        try
        {
            Directory.CreateDirectory(Fondos.CarpetaIntermedios);
            if (soloSiNoHay && File.Exists(RutaInstantanea)) return true;
            var a = new Anterior
            {
                PorMonitor = Fondos.LoQueHayPuesto(),
                Color = Fondos.ColorQueHayPuesto(),
                Cuando = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            ConfigArbol.EscribirAtomico(RutaInstantanea,
                JsonSerializer.Serialize(a, new JsonSerializerOptions { WriteIndented = true }));
            Log.Info($"fondo: instantánea de lo que había ({a.PorMonitor.Count} pantallas)");
            return true;
        }
        catch (Exception ex) { Log.Error("guardar la instantánea del fondo", ex); return false; }
    }

    public static bool HayInstantanea => File.Exists(RutaInstantanea);

    // Vuelve al fondo que había antes de que NubeBar lo tocara.
    public static bool RestaurarAnterior()
    {
        try
        {
            if (!File.Exists(RutaInstantanea)) { Log.Info("fondo: no hay instantánea que restaurar"); return false; }
            var a = JsonSerializer.Deserialize<Anterior>(File.ReadAllText(RutaInstantanea));
            if (a == null) return false;

            var rutas = a.PorMonitor.Where(p => !string.IsNullOrWhiteSpace(p.Value))
                                    .ToDictionary(p => p.Key, p => p.Value);
            var r = Fondos.Poner(rutas, new Dictionary<string, string>(), a.Color);
            Log.Info($"fondo: restaurado el anterior de {a.Cuando} — {r.Detalle}");
            _huella = "";          // la próxima aplicación no puede creerse que ya está puesta
            return r.Ok || rutas.Count == 0;
        }
        catch (Exception ex) { Log.Error("restaurar el fondo anterior", ex); return false; }
    }

    // ================= Aplicar =================

    static string _huella = "";
    static string _huellaReloj = "";
    static readonly Dictionary<string, string> _actualPorClave = new();   // la imagen puesta en cada pantalla

    public static string HuellaPuesta => _huella;

    public sealed record Resultado(bool Ok, bool Cambio, string Detalle);

    // Aplica la configuración de fondo. Asíncrono de verdad: lo que cuesta —leer el
    // directorio de la presentación, dibujar un degradado de 4K, comprobar si una imagen
    // existe en un disco de red— se hace fuera del hilo de interfaz.
    //
    // `forzar` lo usa el botón Aplicar de Ajustes: sin él, pedir el mismo fondo dos veces
    // no haría nada y parecería que el botón está roto.
    public static async Task<Resultado> AplicarAsync(Fondo cfg, bool forzar = false)
    {
        if (cfg == null) return new Resultado(false, false, "sin configuración");

        var tipo = Fondos.TipoEfectivo(cfg.Tipo, Fondos.AnimadoDisponible);

        // ---------- Wallpaper Engine no se gestiona aquí ----------
        //
        // Su ciclo de vida, su detección, su timeout y su coordinación con la nube están en
        // `Integracion.cs` desde a918e6c, probados. Duplicar aquí cualquier parte de eso
        // sería tener dos formas de arrancarlo y dos sitios donde apagarlo.
        if (tipo == "animado")
            return new Resultado(true, false,
                "tipo \"animado\": lo lleva la integración con Wallpaper Engine, no el gestor de fondos");

        if (tipo == "ninguno")
        {
            PararPresentacion();
            return new Resultado(true, false, "tipo \"ninguno\": NubeBar no gestiona el fondo");
        }

        // ---------- ¿Hace falta tocar algo? ----------
        var huella = Fondos.Huella(cfg);
        bool mismaHuella = huella == _huella;
        if (mismaHuella && !forzar && tipo != "presentacion")
            return new Resultado(true, false, "la configuración no ha cambiado: no se recarga");

        try
        {
            var trabajo = await Task.Run(() => Preparar(cfg, tipo)).ConfigureAwait(false);
            if (trabajo.Rutas.Count == 0 && trabajo.Color == null)
                return new Resultado(false, false, trabajo.Detalle);

            var r = Fondos.Poner(trabajo.Rutas, trabajo.Ajustes, trabajo.Color);
            if (r.Ok)
            {
                _huella = huella;
                foreach (var (k, v) in trabajo.Rutas) _actualPorClave[k] = v;
            }

            // El reloj solo se reprograma si cambió lo del reloj, no en cada aplicación.
            var hr = Fondos.HuellaDelReloj(cfg);
            if (tipo == "presentacion")
            {
                if (hr != _huellaReloj || !Corriendo) ProgramarPresentacion(cfg);
                _huellaReloj = hr;
            }
            else PararPresentacion();

            return new Resultado(r.Ok, true, r.Detalle + (trabajo.Aviso.Length > 0 ? " · " + trabajo.Aviso : ""));
        }
        catch (Exception ex)
        {
            Log.Error("aplicar el fondo", ex);
            return new Resultado(false, false, "falló: " + ex.Message);
        }
    }

    sealed record Trabajo(Dictionary<string, string> Rutas, Dictionary<string, string> Ajustes,
                          string? Color, string Detalle, string Aviso);

    // Lo que hay que poner en cada pantalla. Fuera del hilo de interfaz.
    static Trabajo Preparar(Fondo cfg, string tipo)
    {
        var rutas = new Dictionary<string, string>();
        var ajustes = new Dictionary<string, string>();
        var avisos = new List<string>();
        string? color = null;

        var pantallas = Monitores.Todos();
        if (pantallas.Count == 0)
            return new Trabajo(rutas, ajustes, null, "no hay pantallas", "");

        foreach (var m in pantallas)
        {
            var ef = Fondos.Efectivo(cfg, m.Clave);
            var tipoM = Fondos.TipoEfectivo(ef.Tipo, Fondos.AnimadoDisponible);
            if (tipoM is "ninguno" or "animado") continue;

            ajustes[m.Clave] = Fondos.AjusteValido(ef.Ajuste) ? ef.Ajuste! : "rellenar";

            switch (tipoM)
            {
                case "solido":
                    // El color plano es de TODO el escritorio, no por pantalla: es lo único
                    // que `IDesktopWallpaper` no sabe separar. Manda el del monitor
                    // principal, y se dice si los demás pedían otro.
                    color ??= ef.Color1;
                    if (m.EsPrincipal) color = ef.Color1;
                    // Y además se dibuja, porque con solo el color de fondo Windows deja la
                    // imagen anterior puesta encima.
                    var plano = Dibujar("solido", ef.Color1 ?? "#000000", ef.Color1 ?? "#000000", 0,
                                        m.Ancho, m.Alto);
                    if (plano != null) rutas[m.Clave] = plano;
                    break;

                case "degradado":
                    var deg = Dibujar("degradado", ef.Color1 ?? "#000000", ef.Color2 ?? "#FFFFFF",
                                      cfg.Angulo, m.Ancho, m.Alto);
                    if (deg != null) rutas[m.Clave] = deg;
                    else avisos.Add($"no se pudo dibujar el degradado de {m.Dispositivo}");
                    break;

                case "imagen":
                {
                    var res = Fondos.Resolver(ef.Ruta, Array.Empty<string>());
                    if (res.Ruta != null) rutas[m.Clave] = res.Ruta;
                    else avisos.Add(res.Porque);
                    break;
                }

                case "presentacion":
                {
                    var imagenes = Fondos.ImagenesDe(ef.CarpetaPresentacion);
                    _actualPorClave.TryGetValue(m.Clave, out var puesta);
                    // Al preparar NO se avanza: se resuelve la que toca ahora. Avanzar es
                    // del reloj, y mezclarlo aquí hacía que abrir Ajustes cambiara la foto.
                    var pedida = puesta != null && imagenes.Contains(puesta, StringComparer.OrdinalIgnoreCase)
                        ? puesta
                        : Fondos.Siguiente(imagenes, null, cfg.Aleatorio, Environment.TickCount);
                    var res = Fondos.Resolver(pedida, imagenes);
                    if (res.Ruta != null) rutas[m.Clave] = res.Ruta;
                    if (res.Falta) avisos.Add(res.Porque);
                    break;
                }
            }
        }

        return new Trabajo(rutas, ajustes, color,
                           $"{rutas.Count} de {pantallas.Count} pantallas",
                           string.Join("; ", avisos.Distinct()));
    }

    // ================= La presentación =================
    //
    // Un reloj, de la aplicación. Ni uno por ventana de ajustes ni uno por monitor.
    static DispatcherTimer? _reloj;
    static Dispatcher? _interfaz;
    static Fondo? _cfgReloj;
    static int _vuelta;

    public static bool Corriendo => _reloj is { IsEnabled: true };

    // Se llama una vez, al arrancar NubeBar.
    public static void Arrancar(Dispatcher interfaz)
    {
        _interfaz = interfaz;
        Log.Info("gestor de fondos en marcha");
    }

    static void ProgramarPresentacion(Fondo cfg)
    {
        var d = _interfaz;
        if (d == null) { Log.Info("fondo: no hay hilo de interfaz; la presentación no se programa"); return; }

        double seg = Math.Max(Fondos.MinSegundos, cfg.SegundosPresentacion);
        _cfgReloj = cfg;

        if (_reloj == null)
        {
            // Una sola vez en toda la vida del proceso. El Tick se suscribe aquí y no se
            // vuelve a suscribir nunca: reprogramar cambia el Interval, no el oyente.
            _reloj = new DispatcherTimer(DispatcherPriority.Background, d);
            _reloj.Tick += (_, _) => _ = Avanzar();
        }
        _reloj.Stop();
        _reloj.Interval = TimeSpan.FromSeconds(seg);
        _reloj.Start();
        Log.Info($"presentación de fondos: cada {seg:0} s" + (cfg.Aleatorio ? ", aleatoria" : ", en orden"));
    }

    public static void PararPresentacion()
    {
        if (_reloj == null) return;
        if (_reloj.IsEnabled) Log.Info("presentación de fondos: parada");
        _reloj.Stop();
        _cfgReloj = null;
        _huellaReloj = "";
    }

    // Suelta el reloj del todo. Al cerrar NubeBar: un DispatcherTimer vivo mantiene
    // referencias y sigue encolando en un Dispatcher que se está apagando.
    public static void Apagar()
    {
        PararPresentacion();
        if (_reloj != null)
        {
            _reloj = null;      // sin oyentes que lo resuciten: el Tick era una lambda suya
            Log.Info("gestor de fondos: reloj soltado");
        }
        _interfaz = null;
    }

    // Un paso de la presentación. Pública para poder dispararla desde una prueba o desde
    // un botón sin esperar el intervalo.
    public static async Task<bool> Avanzar()
    {
        var cfg = _cfgReloj;
        if (cfg == null) return false;
        try
        {
            var hecho = await Task.Run(() =>
            {
                var rutas = new Dictionary<string, string>();
                var ajustes = new Dictionary<string, string>();
                bool algo = false;
                foreach (var m in Monitores.Todos())
                {
                    var ef = Fondos.Efectivo(cfg, m.Clave);
                    if (Fondos.TipoEfectivo(ef.Tipo, Fondos.AnimadoDisponible) != "presentacion") continue;

                    var imagenes = Fondos.ImagenesDe(ef.CarpetaPresentacion);
                    if (imagenes.Count == 0)
                    {
                        // La carpeta se quedó vacía: no se borra el fondo, se deja el que
                        // hay. Un escritorio en negro porque alguien movió una carpeta es
                        // peor que un fondo de hace un rato.
                        Log.Info($"presentación: la carpeta de {m.Dispositivo} no tiene imágenes; se deja el fondo");
                        continue;
                    }
                    _actualPorClave.TryGetValue(m.Clave, out var puesta);
                    var pedida = Fondos.Siguiente(imagenes, puesta, cfg.Aleatorio, unchecked(Environment.TickCount + _vuelta++));
                    var res = Fondos.Resolver(pedida, imagenes);
                    if (res.Ruta == null) continue;
                    if (res.Falta) Log.Info("presentación: " + res.Porque);
                    rutas[m.Clave] = res.Ruta;
                    ajustes[m.Clave] = Fondos.AjusteValido(ef.Ajuste) ? ef.Ajuste! : "rellenar";
                    algo = true;
                }
                if (!algo) return false;
                var r = Fondos.Poner(rutas, ajustes, null);
                if (r.Ok) foreach (var (k, v) in rutas) _actualPorClave[k] = v;
                return r.Ok;
            }).ConfigureAwait(false);
            return hecho;
        }
        catch (Exception ex) { Log.Error("avanzar la presentación", ex); return false; }
    }
}
