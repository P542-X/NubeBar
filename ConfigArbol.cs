using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NubeBar;

// El config visto como árbol, no como clases. La ventana de ajustes solo toca las
// claves que conoce, así que cualquier cosa que hayas añadido a mano (o que venga
// de una versión más nueva) sigue ahí cuando se guarda.
public sealed class ConfigArbol
{
    JsonObject _raiz;
    readonly string _original;

    public static string RutaCopia => Config.Ruta + ".bak";

    ConfigArbol(JsonObject raiz, string original)
    {
        _raiz = raiz;
        _original = original;
    }

    public static ConfigArbol Cargar()
    {
        string texto = "";
        try { if (File.Exists(Config.Ruta)) texto = File.ReadAllText(Config.Ruta); }
        catch (Exception ex) { Log.Error("leer config para ajustes", ex); }

        JsonObject raiz;
        try
        {
            raiz = (JsonNode.Parse(texto, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) as JsonObject) ?? new JsonObject();
        }
        catch { raiz = new JsonObject(); }

        return new ConfigArbol(raiz, texto);
    }

    JsonObject Seccion(string nombre)
    {
        if (_raiz[nombre] is JsonObject o) return o;
        var nueva = new JsonObject();
        _raiz[nombre] = nueva;
        return nueva;
    }

    // ---------- Lectura ----------
    public string Texto(string seccion, string clave, string porDefecto)
    {
        try { return Seccion(seccion)[clave]?.GetValue<string>() ?? porDefecto; }
        catch { return porDefecto; }
    }

    public double Numero(string seccion, string clave, double porDefecto)
    {
        try { return Seccion(seccion)[clave]?.GetValue<double>() ?? porDefecto; }
        catch { return porDefecto; }
    }

    public bool Bandera(string seccion, string clave, bool porDefecto)
    {
        try { return Seccion(seccion)[clave]?.GetValue<bool>() ?? porDefecto; }
        catch { return porDefecto; }
    }

    public List<string> Lista(string seccion, string clave, params string[] porDefecto)
    {
        try
        {
            if (Seccion(seccion)[clave] is JsonArray a)
                return a.Select(n => n?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList();
        }
        catch { }
        return porDefecto.ToList();
    }

    // ---------- Escritura ----------
    public void Poner(string seccion, string clave, string valor) => Seccion(seccion)[clave] = valor;
    public void Poner(string seccion, string clave, double valor) => Seccion(seccion)[clave] = valor;
    public void Poner(string seccion, string clave, bool valor) => Seccion(seccion)[clave] = valor;

    public void PonerLista(string seccion, string clave, IEnumerable<string> valores)
    {
        var a = new JsonArray();
        foreach (var v in valores) a.Add(v);
        Seccion(seccion)[clave] = a;
    }

    // ---------- Disco ----------
    // Encoder relajado: si no, un "Ctrl+Alt+F9" se guarda como "Ctrl+Alt+F9".
    // Es JSON válido, pero este fichero se edita a mano y así queda legible.
    static readonly JsonSerializerOptions Formato = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Serializar() => _raiz.ToJsonString(Formato);

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Config.Carpeta);
            File.WriteAllText(Config.Ruta, Serializar());
        }
        catch (Exception ex) { Log.Error("guardar config", ex); }
    }

    // Antes de tocar nada por primera vez se deja una copia a mano
    public void HacerCopiaUnaVez()
    {
        try
        {
            if (File.Exists(RutaCopia)) return;
            if (_original.Length == 0) return;
            File.WriteAllText(RutaCopia, _original);
            Log.Info("copia de seguridad del config en " + RutaCopia);
        }
        catch (Exception ex) { Log.Error("copia del config", ex); }
    }

    // Cancelar: el fichero vuelve exactamente a como estaba, byte a byte
    public void Revertir()
    {
        try
        {
            if (_original.Length > 0) File.WriteAllText(Config.Ruta, _original);
        }
        catch (Exception ex) { Log.Error("revertir config", ex); }
    }

    public bool HaCambiado() => Serializar() != _original;
}
