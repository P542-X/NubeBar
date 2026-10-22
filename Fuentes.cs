using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NubeBar;

// De dónde salen los resultados del buscador. El índice de aplicaciones se arma
// una vez en segundo plano y vive en memoria, para que teclear no toque el disco.
public static class Fuentes
{
    public sealed record Entrada(string Nombre, string Ruta, string Detalle);

    static List<Entrada> _apps = new();
    public static bool AppsListas { get; private set; }

    // ---------- Índice de aplicaciones ----------
    public static Task CargarApps() => Task.Run(() =>
    {
        var encontradas = new Dictionary<string, Entrada>(StringComparer.OrdinalIgnoreCase);

        // IgnoreInaccessible: en el menú Inicio común hay carpetas sin permiso de lectura
        // y sin esto la primera que aparece aborta el recorrido entero.
        // Cada raíz va en su propio try por lo mismo: que una no se lleve a la otra.
        var opciones = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        foreach (var raiz in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        })
        {
            try
            {
                if (!Directory.Exists(raiz)) continue;
                foreach (var f in Directory.EnumerateFiles(raiz, "*.*", opciones))
                {
                    var ext = Path.GetExtension(f);
                    if (!ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".url", StringComparison.OrdinalIgnoreCase)) continue;
                    var nombre = Path.GetFileNameWithoutExtension(f);
                    if (nombre.Length == 0) continue;
                    encontradas[nombre] = new Entrada(nombre, f, "Acceso directo");
                }
            }
            catch (Exception ex) { Log.Error("índice de accesos directos en " + raiz, ex); }
        }

        // Apps de la Store: viven en shell:AppsFolder y no tienen fichero suelto
        try
        {
            foreach (var (nombre, id) in AppsDeLaStore())
                if (!encontradas.ContainsKey(nombre))
                    encontradas[nombre] = new Entrada(nombre, "shell:AppsFolder\\" + id, "Aplicación de Windows");
        }
        catch (Exception ex) { Log.Error("índice de apps de la Store", ex); }

        _apps = encontradas.Values.OrderBy(e => e.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
        AppsListas = true;
        Log.Info($"índice del buscador: {_apps.Count} aplicaciones");
    });

    // Enumera shell:AppsFolder por COM tardío, sin referencias extra
    static IEnumerable<(string Nombre, string Id)> AppsDeLaStore()
    {
        var salida = new List<(string, string)>();
        var tipo = Type.GetTypeFromProgID("Shell.Application");
        if (tipo == null) return salida;
        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(tipo);
            if (shell == null) return salida;
            // GUID de la carpeta virtual de aplicaciones
            dynamic carpeta = shell.NameSpace("shell:::{4234d49b-0245-4df3-b780-3893943456e1}");
            if (carpeta == null) return salida;
            foreach (dynamic item in carpeta.Items())
            {
                try
                {
                    string nombre = item.Name;
                    string id = item.Path;
                    if (!string.IsNullOrWhiteSpace(nombre) && !string.IsNullOrWhiteSpace(id))
                        salida.Add((nombre, id));
                }
                catch { }
            }
        }
        catch { }
        finally { if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell)) System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); }
        return salida;
    }

    public static IEnumerable<Resultado> Apps(string consultaNorm, int tope)
    {
        foreach (var a in _apps)
        {
            double p = Parecido.Puntuar(consultaNorm, a.Nombre);
            if (p < 0.45) continue;
            var entrada = a;
            yield return new Resultado
            {
                Categoria = Categoria.App,
                Titulo = entrada.Nombre,
                Detalle = entrada.Detalle,
                Clave = "app:" + entrada.Nombre,
                IconoRuta = entrada.Ruta,
                Carpeta = entrada.Ruta.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                          ? null : Path.GetDirectoryName(entrada.Ruta),
                Puntos = p,
                Abrir = () => Lanzar(entrada.Ruta)
            };
        }
    }

    public static void Lanzar(string ruta)
    {
        try
        {
            if (ruta.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ruta}\"") { UseShellExecute = true })?.Dispose();
            else
                Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) { Log.Error("abrir " + ruta, ex); }
    }

    // ---------- Ajustes de Windows ----------
    static readonly (string Nombre, string Pagina)[] _ajustes =
    {
        ("Wifi",                      "ms-settings:network-wifi"),
        ("Bluetooth",                 "ms-settings:bluetooth"),
        ("Pantalla",                  "ms-settings:display"),
        ("Sonido",                    "ms-settings:sound"),
        ("Notificaciones",            "ms-settings:notifications"),
        ("Energía y batería",         "ms-settings:powersleep"),
        ("Almacenamiento",            "ms-settings:storagesense"),
        ("Fondo de escritorio",       "ms-settings:personalization-background"),
        ("Colores",                   "ms-settings:colors"),
        ("Temas",                     "ms-settings:themes"),
        ("Barra de tareas",           "ms-settings:taskbar"),
        ("Aplicaciones instaladas",   "ms-settings:appsfeatures"),
        ("Aplicaciones predeterminadas", "ms-settings:defaultapps"),
        ("Inicio automático",         "ms-settings:startupapps"),
        ("Cuentas",                   "ms-settings:yourinfo"),
        ("Idioma y región",           "ms-settings:regionlanguage"),
        ("Fecha y hora",              "ms-settings:dateandtime"),
        ("Windows Update",            "ms-settings:windowsupdate"),
        ("Seguridad de Windows",      "ms-settings:windowsdefender"),
        ("Impresoras y escáneres",    "ms-settings:printers"),
        ("Mouse",                     "ms-settings:mousetouchpad"),
        ("Teclado",                   "ms-settings:keyboard"),
        ("Red e Internet",            "ms-settings:network"),
        ("Privacidad",                "ms-settings:privacy"),
        ("Accesibilidad",             "ms-settings:easeofaccess"),
        ("Multitarea",                "ms-settings:multitasking"),
        ("Monitores múltiples",       "ms-settings:display-advanced"),
        ("Juegos y Game Bar",         "ms-settings:gaming-gamebar"),
        ("Variables de entorno",      "ms-settings:about")
    };

    public static IEnumerable<Resultado> Ajustes(string consultaNorm)
    {
        foreach (var (nombre, pagina) in _ajustes)
        {
            double p = Parecido.Puntuar(consultaNorm, nombre);
            if (p < 0.5) continue;
            var pg = pagina;
            yield return new Resultado
            {
                Categoria = Categoria.Ajuste,
                Titulo = nombre,
                Detalle = pg,
                Clave = "ajuste:" + pg,
                Puntos = p * 0.95,
                Abrir = () => Lanzar(pg)
            };
        }
    }

    // ---------- Calculadora ----------
    public static Resultado? Calculo(string consulta)
    {
        var limpia = consulta.Trim();
        if (limpia.Length < 3) return null;
        if (!limpia.Any(char.IsDigit)) return null;
        if (!limpia.All(c => char.IsDigit(c) || " +-*/().,^%".Contains(c))) return null;
        if (!limpia.Any(c => "+-*/^%".Contains(c))) return null;

        try
        {
            double v = Calculadora.Evaluar(limpia);
            if (double.IsNaN(v) || double.IsInfinity(v)) return null;
            string texto = v.ToString("0.##########", CultureInfo.CurrentCulture);
            return new Resultado
            {
                Categoria = Categoria.Calculo,
                Titulo = texto,
                Detalle = limpia + "  ·  Enter lo copia",
                Clave = "calc",
                Puntos = 10,          // siempre el primero
                Abrir = () => { try { System.Windows.Clipboard.SetText(texto); } catch { } }
            };
        }
        catch { return null; }
    }

    // ---------- Comandos tipo rofi ----------
    public static Resultado? Comando(string consulta)
    {
        if (!consulta.StartsWith(">")) return null;
        var orden = consulta[1..].Trim();
        if (orden.Length == 0) return null;
        return new Resultado
        {
            Categoria = Categoria.Comando,
            Titulo = orden,
            Detalle = "Ejecutar como comando",
            Clave = "cmd:" + orden,
            Puntos = 10,
            Abrir = () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("cmd.exe", "/c " + orden)
                    { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })?.Dispose();
                }
                catch (Exception ex) { Log.Error("ejecutar " + orden, ex); }
            }
        };
    }

    // ---------- Web ----------
    public static Resultado Web(string consulta, string buscador) => new()
    {
        Categoria = Categoria.Web,
        Titulo = "Buscar en la web: " + consulta,
        Detalle = buscador,
        Clave = "web",
        Puntos = 0.01,          // siempre el último
        Abrir = () => Lanzar(buscador + Uri.EscapeDataString(consulta))
    };

    // ---------- Archivos: índice de Windows Search ----------
    // Si el servicio no está o tarda, se devuelve vacío y no pasa nada.
    // El proveedor Search.CollatorDSO quiere un hilo STA: desde un hilo del pool
    // (que es MTA) devuelve 0x80041607 y parece que el índice no está disponible.
    // Si el índice no se puede usar (el servicio parado, el ensamblado de OleDb que
    // no carga...), se apunta UNA vez y no se vuelve a intentar en toda la sesión.
    static bool _sinIndice;

    public static List<Resultado> Archivos(string consulta, int tope, CancellationToken ct)
    {
        var salida = new List<Resultado>();
        if (consulta.Length < 3 || _sinIndice) return salida;

        // El cuerpo del hilo va envuelto SIEMPRE. Una excepción suelta en un hilo de
        // fondo no la caza nadie y se lleva por delante el proceso entero: pasó de
        // verdad, con un FileNotFoundException de System.Data.OleDb al ENTRAR en
        // ConsultarIndice (el fallo es al cargar el ensamblado, antes de que exista
        // su propio try, así que el catch tiene que estar aquí fuera).
        var hilo = new Thread(() =>
        {
            try { salida = ConsultarIndice(consulta, tope, ct); }
            catch (Exception ex)
            {
                _sinIndice = true;
                Log.Error("buscar archivos en el índice de Windows (se desactiva)", ex);
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.IsBackground = true;
        hilo.Start();
        if (!hilo.Join(TimeSpan.FromSeconds(3))) Log.Info("Windows Search tardó demasiado: se deja estar");
        return salida;
    }


    // System.ItemUrl viene como "file:C:/Users/…/cosa.txt" (y en un recurso de red,
    // "file://servidor/sitio/…"). Se devuelve la ruta de Windows de toda la vida. Si
    // el índice no da url (pasa con cosas que no son archivos: correo, contactos…),
    // nos quedamos con la que había, que al menos sirve para enseñarla.
    static string RutaDeItemUrl(string? url, string mostrada)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return mostrada;
        var resto = url[5..];
        if (resto.StartsWith("//")) return @"\\" + resto[2..].Replace('/', '\\');
        return resto.TrimStart('/').Replace('/', '\\');
    }

    static List<Resultado> ConsultarIndice(string consulta, int tope, CancellationToken ct)
    {
        var salida = new List<Resultado>();
        try
        {
            var cadena = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows'";
            var escapada = consulta.Replace("'", "''");
            // OJO con las columnas: System.ItemPathDisplay es la ruta PARA ENSEÑAR, y
            // viene traducida al idioma de Windows ("C:\Usuarios\Spcar\Documentos\…",
            // "…\Menú Inicio\Programas\…"). Esos nombres son de cara a la galería: en
            // disco las carpetas se llaman Users, Documents y Start Menu, así que
            // abrir con esa ruta fallaba con "no se encuentra el archivo". La ruta de
            // verdad es System.ItemUrl.
            var sql = "SELECT TOP " + tope + " System.ItemPathDisplay, System.ItemNameDisplay, " +
                      "System.ItemType, System.ItemUrl " +
                      "FROM SystemIndex WHERE System.ItemNameDisplay LIKE '%" + escapada + "%' " +
                      "ORDER BY System.DateModified DESC";

            using var cn = new System.Data.OleDb.OleDbConnection(cadena);
            try { cn.Open(); }
            catch (Exception ex) { Log.Info($"Windows Search: falla al conectar ({ex.GetType().Name}) {ex.Message}"); return salida; }

            using var cmd = new System.Data.OleDb.OleDbCommand(sql, cn);
            using var lector = cmd.ExecuteReader();
            while (lector.Read())
            {
                if (ct.IsCancellationRequested) break;
                var mostrada = lector[0] as string ?? "";
                var nombre = lector[1] as string ?? Path.GetFileName(mostrada);
                var r = RutaDeItemUrl(lector[3] as string, mostrada);
                if (r.Length == 0) continue;
                salida.Add(new Resultado
                {
                    Categoria = Categoria.Archivo,
                    Titulo = nombre,
                    // La de enseñar sí es la traducida: es la que ves en el Explorador
                    Detalle = Path.GetDirectoryName(mostrada.Length > 0 ? mostrada : r) ?? "",
                    Clave = "archivo:" + r,
                    IconoRuta = r,
                    Carpeta = Path.GetDirectoryName(r),
                    Puntos = Parecido.Puntuar(Parecido.Normalizar(consulta), nombre) * 0.8,
                    Abrir = () => Lanzar(r)
                });
            }
        }
        catch (Exception ex)
        {
            Log.Info($"Windows Search no disponible: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
        }
        return salida;
    }
}

// Lo que más abres sube. Se guarda en %APPDATA%\NubeBar\uso.json
public static class Uso
{
    static Dictionary<string, int> _veces = new();
    static string Ruta => Path.Combine(Config.Carpeta, "uso.json");

    public static void Cargar()
    {
        try
        {
            if (File.Exists(Ruta))
                _veces = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Ruta)) ?? new();
        }
        catch { _veces = new(); }
    }

    public static void Apuntar(string clave)
    {
        if (string.IsNullOrEmpty(clave)) return;
        _veces[clave] = _veces.TryGetValue(clave, out int n) ? n + 1 : 1;
        try
        {
            Directory.CreateDirectory(Config.Carpeta);
            ConfigArbol.EscribirAtomico(Ruta, JsonSerializer.Serialize(_veces, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Error("guardar uso.json", ex); }
    }

    // Empujón por costumbre: crece despacio para que no aplaste al parecido
    public static double Empujon(string clave) =>
        _veces.TryGetValue(clave, out int n) ? Math.Min(0.35, Math.Log(1 + n) * 0.09) : 0;
}
