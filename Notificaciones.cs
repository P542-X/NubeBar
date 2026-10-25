using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace NubeBar;

// Las notificaciones de Windows (Discord, descargas, correo...).
//
// La vía "oficial" es UserNotificationListener, pero exige identidad de paquete:
// habría que empaquetar NubeBar en un MSIX disperso y firmarlo con un certificado
// instalado en el equipo. Eso no se hace a espaldas de nadie, así que aquí se lee
// el mismo sitio donde Windows las guarda: su base de datos de notificaciones.
//
// Se lee SIN tocar la original. Windows la tiene abierta en modo WAL, así que se
// copian los tres archivos (db, -wal y -shm) a una carpeta temporal y se abre esa
// copia en solo lectura. Nunca se escribe, nunca se bloquea y nunca se borra nada
// del original: en el peor caso, la copia sale un poco antigua y ya está.
public static class Notificaciones
{
    // Una acción rápida de la notificación. `Uri` solo viene cuando se puede lanzar de
    // verdad —un protocolo: http, ms-settings, el que sea—; las acciones que solo sabe
    // atender la app dueña a través de la plataforma de Windows llegan con Uri vacío y
    // el centro no las enseña. Ver el comentario de Acciones().
    public sealed record AccionRapida(string Texto, string Uri);

    public sealed record Aviso(long Id, string App, string Titulo, string Texto, DateTime Cuando,
                               string AumId, IReadOnlyList<AccionRapida>? Acciones = null);

    // Solo lo cambian las pruebas, para apuntar a una base sintética.
    // NUBEBAR_NOTIS_PRUEBA=<carpeta> hace lo mismo desde fuera: es la única forma
    // de ver el aviso en la isla en un equipo donde Windows tiene las
    // notificaciones apagadas y por tanto no guarda ninguna.
    static string? _carpetaPrueba = Environment.GetEnvironmentVariable("NUBEBAR_NOTIS_PRUEBA");

    static string Carpeta =>
        _carpetaPrueba ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Microsoft", "Windows", "Notifications");

    static string Original => Path.Combine(Carpeta, "wpndatabase.db");
    static string CarpetaCopia => Path.Combine(Path.GetTempPath(), "NubeBar-wpn");
    static string Copia => Path.Combine(CarpetaCopia, "wpndatabase.db");

    static long _ultimaVista = -1;
    static DateTime _ultimaEscritura = DateTime.MinValue;

    public static bool Disponible => File.Exists(Original);

    // Al arrancar se apunta cuál es la última notificación que YA existía, para no
    // soltar de golpe en la isla todas las de los últimos días.
    public static void Arrancar()
    {
        try
        {
            var todas = Leer(1);
            _ultimaVista = todas.Count > 0 ? todas[0].Id : 0;
            Log.Info("notificaciones: base en " + Carpeta + (Disponible ? "" : " (NO EXISTE)"));
            Log.Info($"notificaciones: leyendo la base de Windows, la última es la {_ultimaVista}");
        }
        catch (Exception ex) { Log.Error("arrancar las notificaciones", ex); }
    }

    // La copia de trabajo se borra al cerrar: es la base de notificaciones de
    // Windows (con los textos de tus mensajes) y no tiene por qué quedarse en el
    // temporal cuando NubeBar ya no está.
    public static void Limpiar()
    {
        try { if (Directory.Exists(CarpetaCopia)) Directory.Delete(CarpetaCopia, true); }
        catch (Exception ex) { Log.Info("no se pudo borrar la copia de las notificaciones: " + ex.Message); }
    }

    // Las que han llegado desde la última vez que se preguntó
    // Si Arrancar() falla (la base con otro esquema, el temporal lleno, un antivirus
    // de por medio), antes se reintentaba en CADA vuelta de 3 segundos, y cada
    // reintento copiaba la base entera de Windows: disco a tope para siempre. Ahora
    // se espera un minuto entre intentos y a los 5 fallos se deja estar.
    static DateTime _proximoIntento = DateTime.MinValue;
    static int _fallos;

    public static List<Aviso> Nuevas()
    {
        if (_ultimaVista < 0)
        {
            if (_fallos >= 5 || DateTime.Now < _proximoIntento) return new List<Aviso>();
            _proximoIntento = DateTime.Now.AddMinutes(1);
            _fallos++;
            Arrancar();
            if (_ultimaVista < 0 && _fallos >= 5)
                Log.Info("notificaciones: 5 intentos fallidos, se deja de probar hasta el próximo arranque");
            return new List<Aviso>();
        }

        try
        {
            // Si Windows no ha escrito nada, no hay nada nuevo y nos ahorramos la copia
            var escrita = UltimaEscritura();
            if (escrita <= _ultimaEscritura) return new List<Aviso>();
            _ultimaEscritura = escrita;

            var nuevas = Leer(20).Where(a => a.Id > _ultimaVista).OrderBy(a => a.Id).ToList();
            if (nuevas.Count > 0) _ultimaVista = nuevas[^1].Id;
            return nuevas;
        }
        catch (Exception ex) { Log.Error("leer notificaciones nuevas", ex); return new List<Aviso>(); }
    }

    static DateTime UltimaEscritura()
    {
        var ultima = DateTime.MinValue;
        foreach (var n in new[] { "wpndatabase.db", "wpndatabase.db-wal" })
        {
            var f = Path.Combine(Carpeta, n);
            if (File.Exists(f)) { var t = File.GetLastWriteTimeUtc(f); if (t > ultima) ultima = t; }
        }
        return ultima;
    }

    static void Copiar()
    {
        Directory.CreateDirectory(CarpetaCopia);
        foreach (var n in new[] { "wpndatabase.db", "wpndatabase.db-wal", "wpndatabase.db-shm" })
        {
            var origen = Path.Combine(Carpeta, n);
            var destino = Path.Combine(CarpetaCopia, n);
            if (File.Exists(origen))
            {
                // FileShare.ReadWrite: Windows la tiene abierta, hay que pedir
                // permiso para leerla mientras él sigue escribiendo
                using var e = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var s = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None);
                e.CopyTo(s);
            }
            else if (File.Exists(destino)) File.Delete(destino);
        }
    }

    static List<Aviso> Leer(int cuantas)
    {
        var lista = new List<Aviso>();
        if (!Disponible) return lista;
        Copiar();

        // Pooling apagado a propósito: con el grupo de conexiones encendido, SQLite
        // se queda el archivo abierto después del using y la copia SIGUIENTE falla
        // al intentar sobrescribirlo. Sin esto, las notificaciones funcionaban una
        // sola vez y después nunca más.
        var cadena = new SqliteConnectionStringBuilder
        {
            DataSource = Copia,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using var con = new SqliteConnection(cadena);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT n.""Order"", h.PrimaryId, n.Payload, n.ArrivalTime
            FROM Notification n
            JOIN NotificationHandler h ON h.RecordId = n.HandlerId
            WHERE n.Type = 'toast'
            ORDER BY n.""Order"" DESC
            LIMIT $cuantas;";
        cmd.Parameters.AddWithValue("$cuantas", cuantas);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            string aumid = r.IsDBNull(1) ? "" : r.GetString(1);
            string xml = LeerTexto(r, 2);
            DateTime cuando;
            try { cuando = DateTime.FromFileTimeUtc(r.GetInt64(3)).ToLocalTime(); }
            catch { cuando = DateTime.Now; }

            var (titulo, texto) = Partir(xml);
            if (titulo.Length == 0 && texto.Length == 0) continue;
            lista.Add(new Aviso(id, NombreBonito(aumid), titulo, texto, cuando, aumid, Acciones(xml)));
        }
        return lista;
    }

    // El payload a veces viene como texto y a veces como BLOB con el XML dentro
    static string LeerTexto(SqliteDataReader r, int col)
    {
        if (r.IsDBNull(col)) return "";
        try { return r.GetString(col); }
        catch
        {
            try
            {
                using var s = r.GetStream(col);
                using var m = new MemoryStream();
                s.CopyTo(m);
                var b = m.ToArray();
                // Si empieza por BOM de UTF-16 o por '<' en UTF-16, se decodifica así
                return b.Length > 1 && b[1] == 0 ? System.Text.Encoding.Unicode.GetString(b)
                                                 : System.Text.Encoding.UTF8.GetString(b);
            }
            catch { return ""; }
        }
    }

    static (string titulo, string texto) Partir(string xml)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(xml)) return ("", "");
            var doc = XDocument.Parse(xml);
            var textos = doc.Descendants()
                            .Where(e => e.Name.LocalName == "text")
                            .Select(e => e.Value.Trim())
                            .Where(t => t.Length > 0)
                            .ToList();
            if (textos.Count == 0) return ("", "");
            string titulo = textos[0];
            string cuerpo = textos.Count > 1 ? string.Join("  ·  ", textos.Skip(1)) : "";
            return (titulo, cuerpo);
        }
        catch { return ("", ""); }
    }

    // El identificador de app es feísimo ("Microsoft.WindowsStore_8wekyb3d8bbwe!App").
    // Se le quita la parafernalia para enseñar algo legible.
    static string NombreBonito(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return "Windows";
        var trozo = aumid.Split('!')[0];
        trozo = trozo.Split('_')[0];
        int punto = trozo.LastIndexOf('.');
        if (punto >= 0 && punto < trozo.Length - 1) trozo = trozo[(punto + 1)..];
        return trozo.Length == 0 ? aumid : trozo;
    }


    // ---------- Las acciones rápidas de una notificación ----------
    //
    // Un toast puede traer botones ("Responder", "Marcar como leído", "Ver pedido").
    // Vienen en el XML como <action content="..." arguments="..." activationType="...">.
    //
    // Y aquí hay un límite que conviene dejar escrito en vez de disimular: **solo se
    // pueden pulsar las de tipo `protocol`**. Esas son una URI y lanzarlas es abrirla.
    // Las de tipo `foreground`/`background` —las de responder a un mensaje, las que de
    // verdad querrías— no son una URI: son un argumento que Windows le entrega a la app
    // dueña a través de la plataforma de notificaciones, y para hablar con esa
    // plataforma hace falta identidad de paquete, que es justo lo que NubeBar no tiene
    // (ver la cabecera de este archivo).
    //
    // Fingirlo sería lo peor de los dos mundos: un botón "Responder" que en realidad
    // abre la app y pierde lo que ibas a responder. Así que esas no se enseñan, y
    // pulsar la tarjeta —que abre la app dueña— sigue siendo lo que hay.
    static IReadOnlyList<AccionRapida>? Acciones(string xml)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            var doc = XDocument.Parse(xml);
            var lista = new List<AccionRapida>();

            foreach (var a in doc.Descendants().Where(e => e.Name.LocalName == "action"))
            {
                var texto = (a.Attribute("content")?.Value ?? "").Trim();
                var argumento = (a.Attribute("arguments")?.Value ?? "").Trim();
                var tipo = (a.Attribute("activationType")?.Value ?? "").Trim();
                if (texto.Length == 0) continue;
                if (!tipo.Equals("protocol", StringComparison.OrdinalIgnoreCase)) continue;
                if (!EsUriLanzable(argumento)) continue;
                lista.Add(new AccionRapida(texto, argumento));
                if (lista.Count == 3) break;      // tres botones llenan la tarjeta
            }
            return lista.Count == 0 ? null : lista;
        }
        catch { return null; }
    }

    // Solo esquemas, y solo los que abren algo. Nada de file: ni de javascript:: lo que
    // llega aquí viene de una app cualquiera y acaba en ShellExecute.
    internal static bool EsUriLanzable(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return false;
        int dosPuntos = uri.IndexOf(':');
        if (dosPuntos <= 0) return false;
        var esquema = uri[..dosPuntos].ToLowerInvariant();
        if (esquema is "file" or "javascript" or "vbscript" or "data" or "shell") return false;
        foreach (var c in esquema)
            if (!char.IsLetterOrDigit(c) && c != '+' && c != '-' && c != '.') return false;
        return true;
    }

    // Lanza una acción rápida. Pasa por el shell igual que un enlace del navegador.
    public static void Lanzar(AccionRapida a)
    {
        if (!EsUriLanzable(a.Uri)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = a.Uri, UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex) { Log.Error("lanzar una acción de notificación", ex); }
    }

    // El historial que ya había cuando NubeBar arrancó. `Nuevas()` solo trae lo que ha
    // llegado desde entonces —para no soltar de golpe en la isla las de los últimos
    // días— pero el centro de notificaciones sí tiene que enseñarlas: es su historial.
    // No toca `_ultimaVista`, así que llamarlo no se come ningún aviso pendiente.
    public static List<Aviso> Historial(int cuantas)
    {
        try { return Leer(Math.Clamp(cuantas, 1, 200)); }
        catch (Exception ex) { Log.Error("leer el historial de notificaciones", ex); return new List<Aviso>(); }
    }
    // Pulsar el aviso abre la app dueña
    public static void Abrir(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{aumid}",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Log.Error("abrir la app de una notificación", ex); }
    }
}
