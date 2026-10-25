using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NubeBar;

// Usa los "Command Line Controls" oficiales: wallpaper64.exe -control <comando>
static class WallpaperEngine
{
    static string? _exe;
    public static string? RutaManual { get; set; }

    public static string? Exe => _exe != null && File.Exists(_exe) ? _exe : (_exe = Localizar());
    public static string? Carpeta => Exe is { } e ? Path.GetDirectoryName(e) : null;

    public static bool Abierto
    {
        get
        {
            foreach (var n in new[] { "wallpaper64", "wallpaper32" })
            {
                var ps = Process.GetProcessesByName(n);
                foreach (var p in ps) p.Dispose();
                if (ps.Length > 0) return true;
            }
            return false;
        }
    }

    // ---------- Encontrar el exe ----------
    static string? Localizar()
    {
        // 1. Si ya está abierto, su propio exe
        foreach (var n in new[] { "wallpaper64", "wallpaper32" })
            foreach (var p in Process.GetProcessesByName(n))
            {
                try { if (p.MainModule?.FileName is { } f) return f; }
                catch { }
                finally { p.Dispose(); }
            }

        // 2. Ruta puesta a mano en config.json
        if (!string.IsNullOrWhiteSpace(RutaManual))
        {
            if (File.Exists(RutaManual)) return RutaManual;
            if (EnCarpeta(RutaManual) is { } m) return m;
        }

        // 3. Todas las bibliotecas de Steam (también las de otros discos)
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is not string steam)
            return null;
        var bibliotecas = new List<string> { steam };
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match mt in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                bibliotecas.Add(mt.Groups[1].Value.Replace(@"\\", @"\"));

        foreach (var b in bibliotecas)
            if (EnCarpeta(Path.Combine(b, "steamapps", "common", "wallpaper_engine")) is { } e) return e;
        return null;
    }

    // wallpaper64 primero SI el sistema es de 64 bits; en un Windows de 32 no sirve de
    // nada intentarlo. "Compatible" es exactamente eso, y no una preferencia estética.
    static string? EnCarpeta(string carpeta)
    {
        var orden = Environment.Is64BitOperatingSystem
            ? new[] { "wallpaper64.exe", "wallpaper32.exe" }
            : new[] { "wallpaper32.exe" };
        foreach (var n in orden)
        {
            var r = Path.Combine(carpeta, n);
            if (File.Exists(r)) return r;
        }
        return null;
    }

    // ---------- Enviar comandos ----------
    //
    // Siempre con `ArgumentList`: cada argumento va por separado y lo escapa el runtime.
    // Construir una cadena de shell con una ruta del usuario dentro es la forma clásica
    // de que un wallpaper llamado  a" -control closeWallpaper "  haga algo que nadie pidió.
    static ProcessStartInfo Crear(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-control");
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    // Solo si está abierto: si no, lanzar el exe abriría Wallpaper Engine
    public static void Enviar(params string[] args)
    {
        if (!Abierto || Exe is not { } exe) return;
        try { Process.Start(Crear(exe, args))?.Dispose(); } catch { }
    }

    // ---------- Los cinco comandos con nombre ----------
    //
    // Los que documenta Wallpaper Engine. Están juntos para que quien los lea vea de una
    // vez qué le decimos al programa de otro: poco, y siempre lo mismo.
    public static bool AbrirWallpaper(string ruta, string? monitor) =>
        Mandar("openWallpaper", "-file", ruta, monitor);

    public static bool AbrirPlaylist(string nombre, string? monitor) =>
        Mandar("openPlaylist", "-playlist", nombre, monitor);

    public static bool Reproducir(string? monitor) => Mandar("play", null, null, monitor);
    public static bool Pausar(string? monitor) => Mandar("pause", null, null, monitor);
    public static bool CerrarWallpaper(string? monitor) => Mandar("closeWallpaper", null, null, monitor);

    static bool Mandar(string comando, string? bandera, string? valor, string? monitor)
    {
        if (!Abierto || Exe is not { } exe) return false;
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("-control");
            psi.ArgumentList.Add(comando);
            if (bandera != null && valor != null) { psi.ArgumentList.Add(bandera); psi.ArgumentList.Add(valor); }
            // Sin -monitor, Wallpaper Engine lo aplica a todos. "todos" es nuestra forma
            // de decir eso mismo sin que la config tenga un valor vacío con significado.
            if (!string.IsNullOrWhiteSpace(monitor) && monitor != Integracion.TodosLosMonitores)
            {
                psi.ArgumentList.Add("-monitor");
                psi.ArgumentList.Add(monitor);
            }
            using var p = Process.Start(psi);
            Log.Info($"Wallpaper Engine: -control {comando}" +
                     (valor != null ? $" {bandera} \"{valor}\"" : "") +
                     (!string.IsNullOrWhiteSpace(monitor) && monitor != Integracion.TodosLosMonitores
                        ? $" -monitor {monitor}" : " (todos los monitores)"));
            return true;
        }
        catch (Exception ex) { Log.Error($"mandar -control {comando} a Wallpaper Engine", ex); return false; }
    }

    // ---------- Abrirlo ----------
    //
    // Sin `-control`: lanzar el exe a secas es lo que abre el programa. Devuelve false si
    // ya estaba abierto, y eso NO es un fallo: es la respuesta a "lo has abierto tú?".
    public static bool Abrir(out string porque)
    {
        if (Abierto) { porque = "ya estaba abierto"; return false; }
        if (Exe is not { } exe) { porque = "no se encuentra el ejecutable"; return false; }
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                // Desde su propia carpeta: busca sus assets en relativo y lanzado desde
                // otro directorio de trabajo arranca sin ellos.
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            };
            using var p = Process.Start(psi);
            porque = p == null ? "Process.Start no devolvió proceso" : "abierto por NubeBar";
            return p != null;
        }
        catch (Exception ex)
        {
            Log.Error("abrir Wallpaper Engine", ex);
            porque = "falló al abrirlo: " + ex.Message;
            return false;
        }
    }

    public static async Task<string?> WallpaperActual(int monitor)
    {
        if (!Abierto || Exe is not { } exe) return null;

        // Su config.json PRIMERO. Esto se pregunta cada 15 segundos, y preguntárselo
        // al cliente significa arrancar wallpaper32.exe (más su conhost) otras tantas
        // veces: cuatro procesos por minuto, para siempre, y con el parpadeo de una
        // ventanita que aparece y se va. Leer un fichero no abre nada y contesta lo
        // mismo. Lanzar el cliente se queda de reserva para la instalación en la que
        // ese config no se pueda leer.
        if (DesdeSuConfig(monitor) is { } delConfig) return delConfig;

        var psi = Crear(exe, "getWallpaper", "-monitor", monitor.ToString());
        psi.RedirectStandardOutput = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var p = Process.Start(psi);
            if (p != null)
            {
                var salida = await p.StandardOutput.ReadToEndAsync(cts.Token);
                foreach (var linea in salida.Split('\n'))
                {
                    var r = linea.Trim().Trim('"');
                    if (r.Length > 0) return r;
                }
            }
        }
        catch { }

        // En Wallpaper Engine 2.8 "getWallpaper" tampoco escribe nada por la salida
        // estándar: el exe que lanzamos es solo un cliente que reenvía el comando a la
        // instancia ya abierta. O sea que por este camino no se saca nada y encima se
        // paga un proceso — por eso el config va antes.
        return null;
    }

    static string? DesdeSuConfig(int monitor)
    {
        try
        {
            if (Carpeta is not { } dir) return null;
            var conf = Path.Combine(dir, "config.json");
            if (!File.Exists(conf)) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(conf));
            if (Buscar(doc.RootElement, "selectedwallpapers") is not { } sel) return null;
            if (!sel.TryGetProperty("Monitor" + monitor, out var m)) return null;
            if (!m.TryGetProperty("file", out var f) || f.GetString() is not { } ruta) return null;
            return ruta.Replace('/', '\\');
        }
        catch { return null; }
    }

    // Busca una propiedad por nombre a cualquier profundidad: así no nos la juega
    // que Wallpaper Engine cambie de sitio su estructura entre versiones
    static JsonElement? Buscar(JsonElement nodo, string nombre)
    {
        if (nodo.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in nodo.EnumerateObject())
        {
            if (p.NameEquals(nombre) && p.Value.ValueKind == JsonValueKind.Object) return p.Value;
            if (Buscar(p.Value, nombre) is { } hallado) return hallado;
        }
        return null;
    }

    // ---------- Datos del wallpaper ----------
    public static string Titulo(string ruta) =>
        LeerProyecto(ruta, "title") ?? Path.GetFileNameWithoutExtension(ruta);

    public static string? Preview(string ruta)
    {
        if (EsImagen(ruta) && File.Exists(ruta)) return ruta;   // wallpaper que es una imagen suelta
        var dir = Dir(ruta);
        if (dir == null || !Directory.Exists(dir)) return null;
        if (LeerProyecto(ruta, "preview") is { } pv && File.Exists(Path.Combine(dir, pv)))
            return Path.Combine(dir, pv);
        return Directory.EnumerateFiles(dir, "preview.*").FirstOrDefault();
    }

    static string? Dir(string ruta) => Directory.Exists(ruta) ? ruta : Path.GetDirectoryName(ruta);

    static bool EsImagen(string r) =>
        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Any(x => r.EndsWith(x, StringComparison.OrdinalIgnoreCase));

    static string? LeerProyecto(string ruta, string campo)
    {
        try
        {
            if (Dir(ruta) is not { } dir) return null;
            var proj = Path.Combine(dir, "project.json");
            if (!File.Exists(proj)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(proj));
            return doc.RootElement.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;
        }
        catch { return null; }
    }
}
