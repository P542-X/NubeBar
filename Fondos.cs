using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace NubeBar;

// El fondo del escritorio, gestionado por NubeBar.
//
// ---------- Por qué no basta con SystemParametersInfo ----------
//
// `SPI_SETDESKWALLPAPER` es la API de toda la vida y pone UN fondo para TODO el
// escritorio: no sabe de monitores, y el "ajuste" (rellenar, ajustar, centrar) hay que
// escribirlo a mano en el registro y rezar. Desde Windows 8 existe `IDesktopWallpaper`,
// que es COM y sí sabe de monitores: cada pantalla con su imagen, su posición y su color
// de relleno. Es lo que usa la propia Configuración de Windows.
//
// Se usa `IDesktopWallpaper` y se cae a `SystemParametersInfo` si no está, que es el caso
// de un Windows 7 o de una sesión donde el shell no lo publique. El de respaldo pierde el
// por-monitor; lo dice en el log y pone el fondo del monitor principal en todos.
//
// ---------- Y los tipos que no son una imagen ----------
//
// Windows no sabe poner un degradado: solo sabe de ficheros y de un color plano. Así que
// el color sólido va por su camino (`SetBackgroundColor`, que no gasta disco) y el
// degradado se **dibuja** a un PNG en `%LOCALAPPDATA%\NubeBar\fondos` y se pone como
// imagen. Ese PNG es un intermedio, no configuración: no va a config.json y se puede
// borrar sin perder nada, se vuelve a dibujar.
//
// ---------- La regla de no recargar por nada ----------
//
// Cambiar el intervalo de la presentación no tiene que volver a leer una imagen de 20 MB
// del disco. Por eso lo que decide si hay que tocar el escritorio es una **huella** de
// las claves que de verdad lo cambian (`Huella`), no el hecho de que el config se haya
// guardado. Y por eso la huella es una función pura: se puede comprobar que mover el
// intervalo no la cambia.
public class Fondo
{
    // "solido" | "degradado" | "imagen" | "presentacion" | "animado" | "ninguno"
    //
    // "ninguno" es importante y no es lo mismo que no tener la sección: significa "no
    // toques el fondo del escritorio", que es lo que tiene que pasar de serie para no
    // cambiarle el fondo a nadie el día que actualice.
    public string Tipo { get; set; } = "ninguno";

    public string Color1 { get; set; } = "#1A1B26";
    public string Color2 { get; set; } = "#7AA2F7";
    public double Angulo { get; set; } = 135;              // grados, para el degradado

    public string? Ruta { get; set; }                       // imagen estática
    public string? CarpetaPresentacion { get; set; }        // presentación
    public double SegundosPresentacion { get; set; } = 300;
    public bool Aleatorio { get; set; }

    // "rellenar" | "ajustar" | "centrar" | "extender" | "mosaico"
    public string Ajuste { get; set; } = "rellenar";

    // Fondo distinto por monitor. La clave es la de `Monitores.Info.Clave`, que es
    // estable entre recolocaciones (el handle no lo es). Lo que no esté aquí usa lo de
    // arriba, así que un monitor nuevo no se queda sin fondo.
    public Dictionary<string, FondoDeMonitor> PorMonitor { get; set; } = new();

}

// Lo que un monitor puede tener distinto. Todo opcional: lo que esté a null hereda.
public class FondoDeMonitor
{
    public string? Tipo { get; set; }
    public string? Color1 { get; set; }
    public string? Color2 { get; set; }
    public string? Ruta { get; set; }
    public string? CarpetaPresentacion { get; set; }
    public string? Ajuste { get; set; }
}

public static class Fondos
{
    public static readonly string[] Tipos = { "ninguno", "solido", "degradado", "imagen", "presentacion", "animado" };
    public static readonly string[] Ajustes = { "rellenar", "ajustar", "centrar", "extender", "mosaico" };

    public static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".jfif" };

    // Donde se dibujan los degradados y los sólidos cuando hace falta un fichero. Es
    // caché, no configuración: va en LocalAppData y se puede borrar.
    public static string CarpetaIntermedios =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NubeBar", "fondos");

    public static bool TipoValido(string? t) =>
        t != null && Tipos.Contains(t, StringComparer.OrdinalIgnoreCase);

    public static bool AjusteValido(string? a) =>
        a != null && Ajustes.Contains(a, StringComparer.OrdinalIgnoreCase);

    // ================= Validación, para los perfiles importados =================
    //
    // La llama `Perfiles.ValorMal`. Devuelve null si el valor vale.
    public static string? ValorMal(string clave, JsonNode? valor)
    {
        if (valor == null) return null;
        try
        {
            switch (clave.ToLowerInvariant())
            {
                case "tipo":
                    return TipoValido(valor.GetValue<string>()) ? null
                        : "no es uno de " + string.Join(", ", Tipos);
                case "ajuste":
                    return AjusteValido(valor.GetValue<string>()) ? null
                        : "no es uno de " + string.Join(", ", Ajustes);
                case "color1":
                case "color2":
                    var c = valor.GetValue<string>() ?? "";
                    return Paleta.EsColor(c) ? null : $"\"{c}\" no es un color";
                case "angulo":
                    double g = valor.GetValue<double>();
                    return g >= 0 && g <= 360 ? null : "fuera de 0..360";
                case "segundospresentacion":
                    double s = valor.GetValue<double>();
                    return s >= MinSegundos && s <= 86400 ? null : $"fuera de {MinSegundos}..86400";
                case "aleatorio":
                    _ = valor.GetValue<bool>();
                    return null;
                case "ruta":
                case "carpetapresentacion":
                    var r = valor.GetValue<string>() ?? "";
                    // Que no exista NO es motivo para rechazar el perfil: la imagen puede
                    // estar en el otro equipo, o en un disco que hoy no está puesto. Lo
                    // que se comprueba es que sea una ruta y no un intento de otra cosa.
                    return RutaRazonable(r) ? null : "no parece una ruta de fichero";
                case "pormonitor":
                    return valor is JsonObject ? null : "tiene que ser un objeto por clave de monitor";
                default:
                    return null;        // una clave que no conocemos la poda la lista blanca
            }
        }
        catch (Exception ex) { return "el tipo no es el que toca (" + ex.GetType().Name + ")"; }
    }

    // Un intervalo por debajo de esto no es una presentación, es un parpadeo. Y leer una
    // imagen grande cada dos segundos deja el disco ocupado para nada.
    public const double MinSegundos = 5;

    public static bool RutaRazonable(string? r)
    {
        if (string.IsNullOrWhiteSpace(r)) return true;      // vacío = "no hay", es válido
        if (r.Length > 32000) return false;
        if (r.IndexOfAny(new[] { '\0', '\n', '\r' }) >= 0) return false;
        // Nada de URLs: esto se le pasa a Windows como ruta de fichero
        if (r.Contains("://", StringComparison.Ordinal)) return false;
        return true;
    }

    // ================= Lo que de verdad cambia el escritorio =================
    //
    // La huella. Si no cambia, no se toca el fondo: eso es lo que impide que mover el
    // intervalo de la presentación, o marcar "extraer paleta", releaa una imagen de 20 MB.
    //
    // Fíjate en lo que NO entra: `SegundosPresentacion` ni `Aleatorio`.
    // Los dos primeros solo cambian el reloj; el tercero, el tema.
    public static string Huella(Fondo f)
    {
        if (f == null) return "";
        var partes = new List<string>
        {
            (f.Tipo ?? "").ToLowerInvariant(),
            (f.Ajuste ?? "").ToLowerInvariant(),
            f.Color1 ?? "", f.Color2 ?? "",
            f.Angulo.ToString("0.##"),
            f.Ruta ?? "", f.CarpetaPresentacion ?? "",
        };
        foreach (var (clave, m) in (f.PorMonitor ?? new()).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            partes.Add($"{clave}|{m.Tipo}|{m.Color1}|{m.Color2}|{m.Ruta}|{m.CarpetaPresentacion}|{m.Ajuste}");
        return string.Join(";", partes);
    }

    // Y la del reloj, aparte: si cambia esta y no la otra, lo único que hay que hacer es
    // reprogramar el temporizador de la presentación.
    public static string HuellaDelReloj(Fondo f) =>
        f == null ? "" : $"{f.SegundosPresentacion:0.##}|{f.Aleatorio}";

    // ================= Resolver: qué le toca a este monitor =================
    //
    // Devuelve el fondo efectivo de un monitor: lo suyo si lo tiene, y lo general si no.
    // Pura, para poder comprobar la herencia sin tocar ninguna pantalla.
    public static FondoDeMonitor Efectivo(Fondo f, string? claveMonitor)
    {
        var baseF = new FondoDeMonitor
        {
            Tipo = f.Tipo, Color1 = f.Color1, Color2 = f.Color2,
            Ruta = f.Ruta, CarpetaPresentacion = f.CarpetaPresentacion, Ajuste = f.Ajuste,
        };
        if (claveMonitor == null || f.PorMonitor == null) return baseF;
        if (!f.PorMonitor.TryGetValue(claveMonitor, out var suyo) || suyo == null) return baseF;
        return new FondoDeMonitor
        {
            Tipo = suyo.Tipo ?? baseF.Tipo,
            Color1 = suyo.Color1 ?? baseF.Color1,
            Color2 = suyo.Color2 ?? baseF.Color2,
            Ruta = suyo.Ruta ?? baseF.Ruta,
            CarpetaPresentacion = suyo.CarpetaPresentacion ?? baseF.CarpetaPresentacion,
            Ajuste = suyo.Ajuste ?? baseF.Ajuste,
        };
    }

    // ================= La presentación =================

    // Las imágenes de una carpeta, en orden estable. Estable importa: sin ordenar, el
    // orden lo decide el sistema de ficheros y la presentación "aleatoria" apagada
    // saltaría igual.
    public static List<string> ImagenesDe(string? carpeta)
    {
        var lista = new List<string>();
        if (string.IsNullOrWhiteSpace(carpeta)) return lista;
        try
        {
            if (!Directory.Exists(carpeta)) return lista;
            foreach (var f in Directory.EnumerateFiles(carpeta))
                if (Extensiones.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    lista.Add(f);
        }
        catch (Exception ex) { Log.Error("listar las imágenes de " + carpeta, ex); }
        lista.Sort(StringComparer.OrdinalIgnoreCase);
        return lista;
    }

    // Cuál va después. Pura y con la semilla por parámetro para poder comprobarla.
    //
    // En aleatorio **no se repite la que está puesta** mientras haya más de una: un
    // "aleatorio" que a veces no cambia nada se lee como que la presentación está rota.
    public static string? Siguiente(IReadOnlyList<string> imagenes, string? actual, bool aleatorio, int semilla)
    {
        if (imagenes == null || imagenes.Count == 0) return null;
        if (imagenes.Count == 1) return imagenes[0];

        if (!aleatorio)
        {
            int i = actual == null ? -1 : IndiceDe(imagenes, actual);
            return imagenes[(i + 1) % imagenes.Count];
        }

        int actualIdx = actual == null ? -1 : IndiceDe(imagenes, actual);
        // Se elige entre las OTRAS: así nunca sale la misma dos veces seguidas.
        int cuantas = actualIdx >= 0 ? imagenes.Count - 1 : imagenes.Count;
        int elegido = Math.Abs(semilla) % cuantas;
        if (actualIdx >= 0 && elegido >= actualIdx) elegido++;
        return imagenes[elegido];
    }

    static int IndiceDe(IReadOnlyList<string> lista, string ruta)
    {
        for (int i = 0; i < lista.Count; i++)
            if (string.Equals(lista[i], ruta, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // ================= El respaldo cuando una imagen desaparece =================
    //
    // Pasa constantemente: la carpeta estaba en un USB, la imagen se renombró, OneDrive
    // la movió a la nube. La regla, y es pura:
    //
    //  1. Si la que toca existe, esa.
    //  2. Si no, la siguiente de su carpeta que sí exista.
    //  3. Si no queda ninguna, NO se borra el fondo: se deja el que haya puesto y se
    //     dice en el log. Dejar el escritorio en negro porque falta un fichero es peor
    //     que dejar el fondo de ayer.
    public sealed record Resuelto(string? Ruta, bool Falta, string Porque);

    public static Resuelto Resolver(string? pedida, IReadOnlyList<string> disponibles)
    {
        bool hay = !string.IsNullOrWhiteSpace(pedida);
        if (hay && Existe(pedida!)) return new Resuelto(pedida, false, "");

        var viva = disponibles?.FirstOrDefault(Existe);
        if (viva != null)
            return new Resuelto(viva, true,
                hay ? $"\"{Path.GetFileName(pedida)}\" ya no está; se usa \"{Path.GetFileName(viva)}\""
                    : "no había ninguna puesta; se usa la primera de la carpeta");

        return new Resuelto(null, true,
            hay ? $"\"{Path.GetFileName(pedida)}\" ya no está y la carpeta no tiene ninguna; se deja el fondo actual"
                : "no hay ninguna imagen; se deja el fondo actual");
    }

    // Separada para poder sustituirla en las pruebas sin tocar el disco.
    internal static Func<string, bool> Existe = r =>
    {
        try { return !string.IsNullOrWhiteSpace(r) && File.Exists(r); }
        catch { return false; }
    };

    // ================= Wallpaper Engine =================

    public static bool AnimadoDisponible => WallpaperEngine.Exe != null;

    // Qué hacer si el perfil o la config piden "animado" y Wallpaper Engine no está.
    // No se cambia el tipo guardado —el usuario no ha cambiado de opinión, es que hoy no
    // está el programa— y se pinta lo que se pueda.
    public static string TipoEfectivo(string? pedido, bool weDisponible)
    {
        if (!TipoValido(pedido)) return "ninguno";
        var t = pedido!.ToLowerInvariant();
        if (t == "animado" && !weDisponible) return "ninguno";
        return t;
    }

    // ================= Aplicar de verdad =================

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID,
                          [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out Nativo.RECT rect);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
        void SetSlideshow(IntPtr items);
        void GetSlideshow(out IntPtr items);
        void SetSlideshowOptions(int options, uint slideshowTick);
        void GetSlideshowOptions(out int options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, int direction);
        int GetStatus();
        void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
    }

    // DESKTOP_WALLPAPER_POSITION
    internal static int PosicionCom(string? ajuste) => (ajuste ?? "").ToLowerInvariant() switch
    {
        "centrar" => 0,     // DWPOS_CENTER
        "mosaico" => 1,     // DWPOS_TILE
        "ajustar" => 2,     // DWPOS_STRETCH  (deformaría; ver nota)
        "rellenar" => 4,    // DWPOS_FILL     (recorta, no deforma)
        "extender" => 5,    // DWPOS_SPAN     (una imagen para todas las pantallas)
        _ => 4,
    };

    // Ojo con "ajustar": Windows llama STRETCH a lo que deforma y FIT a lo que respeta la
    // proporción. Lo que un usuario entiende por "ajustar" es FIT (=3), no STRETCH.
    internal static int PosicionDeAjustar => 3;      // DWPOS_FIT

    internal static int Posicion(string? ajuste) =>
        (ajuste ?? "").Equals("ajustar", StringComparison.OrdinalIgnoreCase)
            ? PosicionDeAjustar : PosicionCom(ajuste);

    static IDesktopWallpaper? Com()
    {
        try
        {
            var t = Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD"));
            if (t == null) return null;
            return Activator.CreateInstance(t) as IDesktopWallpaper;
        }
        catch (Exception ex)
        {
            Log.Info("IDesktopWallpaper no está disponible (" + ex.GetType().Name + "): se usa el respaldo");
            return null;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SystemParametersInfoW(uint accion, uint param, string? valor, uint aviso);

    // El respaldo de toda la vida. Pierde el por-monitor.
    static bool PonerConSPI(string ruta)
    {
        try
        {
            const uint SPI_SETDESKWALLPAPER = 0x0014;
            const uint SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02;
            return SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, ruta, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
        catch (Exception ex) { Log.Error("poner el fondo con SystemParametersInfo", ex); return false; }
    }

    // ---------- La API que usa el resto de NubeBar ----------

    public sealed record Aplicado(bool Ok, int Monitores, bool PorMonitor, string Detalle);

    // ================= Emparejar monitores DE VERDAD =================
    //
    // `IDesktopWallpaper` identifica cada pantalla con una ruta de dispositivo
    // (`\\?\DISPLAY#...`), y ese identificador no tiene nada que ver con el orden de
    // `Monitores.Todos()`. La primera versión los emparejaba **por índice** y lo dejaba
    // anotado como riesgo; aquí está resuelto, porque emparejar por índice es exactamente
    // cómo se le pone el fondo a la pantalla de al lado.
    //
    // Se emparejan por **rectángulo**: se le pregunta a COM dónde está cada una de sus
    // pantallas (`GetMonitorRECT`) y se busca la de `Monitores.Todos()` que ocupe ese
    // mismo sitio. El rectángulo es identidad real: dos monitores no pueden estar en las
    // mismas coordenadas a la vez.
    //
    // Y si no se puede emparejar —un monitor que COM ve y Windows ya no, o al revés— NO
    // se adivina: esa pantalla se queda sin tocar y queda en el log. Poner el fondo en la
    // pantalla equivocada es peor que no ponerlo.
    public sealed record ParMonitor(string IdCom, string? Clave, int X, int Y, int Ancho, int Alto);

    // Pura: recibe lo que COM dice y lo que Windows dice, y empareja. Así se puede
    // comprobar con pantallas inventadas, incluidas las que no coinciden.
    public static List<ParMonitor> Emparejar(
        IReadOnlyList<(string Id, int X, int Y, int Ancho, int Alto)> deCom,
        IReadOnlyList<Monitores.Info> deWindows)
    {
        var pares = new List<ParMonitor>();
        var usadas = new HashSet<string>();
        foreach (var c in deCom)
        {
            // Exacto primero: es lo normal y no hay que tolerar nada.
            var m = deWindows.FirstOrDefault(w =>
                !usadas.Contains(w.Clave) &&
                w.X == c.X && w.Y == c.Y && w.Ancho == c.Ancho && w.Alto == c.Alto);

            // Y si no, por la esquina: al cambiar de resolución las coordenadas de origen
            // se conservan y el tamaño no. Es la única tolerancia, y sigue siendo
            // identidad: dos pantallas no comparten esquina superior izquierda.
            m ??= deWindows.FirstOrDefault(w =>
                !usadas.Contains(w.Clave) && w.X == c.X && w.Y == c.Y);

            if (m != null) usadas.Add(m.Clave);
            pares.Add(new ParMonitor(c.Id, m?.Clave, c.X, c.Y, c.Ancho, c.Alto));
        }
        return pares;
    }

    // Lo mismo, preguntándole a COM de verdad.
    static List<ParMonitor> EmparejarConCom(IDesktopWallpaper com)
    {
        var deCom = new List<(string, int, int, int, int)>();
        uint n = com.GetMonitorDevicePathCount();
        for (uint i = 0; i < n; i++)
        {
            string id;
            try { id = com.GetMonitorDevicePathAt(i); }
            catch { continue; }
            if (string.IsNullOrEmpty(id)) continue;
            try
            {
                com.GetMonitorRECT(id, out var r);
                deCom.Add((id, r.Izq, r.Arr, r.Der - r.Izq, r.Aba - r.Arr));
            }
            catch
            {
                // Una pantalla que COM enumera pero de la que no sabe decir dónde está:
                // se enumera igual, sin rectángulo, y el emparejamiento la descartará.
                deCom.Add((id, int.MinValue, int.MinValue, 0, 0));
            }
        }
        return Emparejar(deCom, Monitores.Todos());
    }

    // Pone el fondo. `rutaPorClave` es lo que haya resuelto el gestor (la presentación ya
    // eligió su imagen, el degradado ya está dibujado): aquí solo se coloca, para que todo
    // lo que decide sea comprobable sin tocar el escritorio de nadie.
    public static Aplicado Poner(Dictionary<string, string> rutaPorClave,
                                 Dictionary<string, string> ajustePorClave,
                                 string? colorPlano)
    {
        var com = Com();
        if (com == null)
        {
            // Sin COM no hay por-monitor. Se pone una y se dice: es un Windows donde el
            // shell no publica IDesktopWallpaper.
            var una = rutaPorClave.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (una == null) return new Aplicado(false, 0, false, "sin COM y sin imagen que poner");
            bool ok = PonerConSPI(una);
            Log.Info("fondo: IDesktopWallpaper no está; respaldo SystemParametersInfo (sin por-monitor)");
            return new Aplicado(ok, 1, false, "respaldo SystemParametersInfo (sin por-monitor)");
        }

        int puestos = 0, sinEmparejar = 0;
        try
        {
            if (colorPlano != null)
            {
                var c = Paleta.Leer(colorPlano, System.Windows.Media.Colors.Black);
                // COLORREF es 0x00BBGGRR, al revés que un #RRGGBB
                uint bgr = (uint)(c.R | (c.G << 8) | (c.B << 16));
                com.SetBackgroundColor(bgr);
            }

            var pares = EmparejarConCom(com);
            foreach (var p in pares)
            {
                if (p.Clave == null)
                {
                    sinEmparejar++;
                    Log.Info($"fondo: la pantalla de COM en ({p.X},{p.Y}) {p.Ancho}x{p.Alto} no se " +
                             "corresponde con ninguna de Windows; se deja sin tocar");
                    continue;
                }
                if (!rutaPorClave.TryGetValue(p.Clave, out var ruta) || string.IsNullOrWhiteSpace(ruta))
                    continue;
                if (ajustePorClave.TryGetValue(p.Clave, out var aj)) com.SetPosition(Posicion(aj));
                com.SetWallpaper(p.IdCom, ruta);
                puestos++;
            }

            var detalle = $"IDesktopWallpaper, {puestos} de {pares.Count} pantallas" +
                          (sinEmparejar > 0 ? $" ({sinEmparejar} sin emparejar)" : "");
            return new Aplicado(puestos > 0, puestos, true, detalle);
        }
        catch (Exception ex)
        {
            Log.Error("poner el fondo con IDesktopWallpaper", ex);
            var una = rutaPorClave.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (una != null && PonerConSPI(una))
                return new Aplicado(true, 1, false, "IDesktopWallpaper falló; respaldo SystemParametersInfo");
            return new Aplicado(false, puestos, true, "no se pudo poner el fondo");
        }
    }

    // ---------- Lo que hay puesto ahora, para poder volver ----------
    public static Dictionary<string, string> LoQueHayPuesto()
    {
        var mapa = new Dictionary<string, string>();
        try
        {
            var com = Com();
            if (com == null) return mapa;
            foreach (var p in EmparejarConCom(com))
            {
                if (p.Clave == null) continue;
                try { mapa[p.Clave] = com.GetWallpaper(p.IdCom) ?? ""; } catch { }
            }
        }
        catch (Exception ex) { Log.Error("leer el fondo que hay puesto", ex); }
        return mapa;
    }

    public static string? ColorQueHayPuesto()
    {
        try
        {
            var com = Com();
            if (com == null) return null;
            uint bgr = com.GetBackgroundColor();
            byte r = (byte)(bgr & 0xFF), g = (byte)((bgr >> 8) & 0xFF), b = (byte)((bgr >> 16) & 0xFF);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch { return null; }
    }
}
