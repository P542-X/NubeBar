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

    // Sobre qué fichero trabaja este árbol. Casi siempre es el config de verdad, pero
    // no tiene por qué: así se puede abrir un paquete de tema, o un config de prueba,
    // sin tocar el del usuario. Antes la ruta estaba escrita dentro de cada método y
    // no había manera de apuntar a otro sitio.
    readonly string _ruta;

    public string Ruta => _ruta;
    public string RutaCopia => _ruta + ".bak";

    ConfigArbol(JsonObject raiz, string original, string ruta)
    {
        _raiz = raiz;
        _original = original;
        _ruta = ruta;
    }

    public static ConfigArbol Cargar(string? ruta = null)
    {
        ruta ??= Config.Ruta;
        string copiaDe = ruta + ".bak";
        string texto = "";
        try { if (File.Exists(ruta)) texto = File.ReadAllText(ruta); }
        catch (Exception ex) { Log.Error("leer config para ajustes", ex); }

        JsonObject? raiz = null;
        try
        {
            raiz = JsonNode.Parse(texto, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) as JsonObject;
        }
        catch (Exception ex) { Log.Error("el config no se puede leer como JSON", ex); }

        // Antes, si el fichero estaba roto se empezaba con un árbol VACÍO y el primer
        // guardado desde ajustes borraba toda la configuración del usuario. Ahora el
        // roto se aparta con su fecha y se intenta recuperar la copia .bak.
        if (raiz == null && texto.Length > 0)
        {
            try
            {
                var apartado = ruta + ".roto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(ruta, apartado, true);
                Log.Info("config ilegible: se guarda tal cual en " + apartado);
            }
            catch (Exception ex) { Log.Error("apartar el config roto", ex); }

            try
            {
                if (File.Exists(copiaDe))
                {
                    var copia = File.ReadAllText(copiaDe);
                    if (JsonNode.Parse(copia) is JsonObject buena)
                    {
                        Log.Info("se recupera la copia de seguridad " + copiaDe);
                        return new ConfigArbol(buena, copia, ruta);
                    }
                }
            }
            catch (Exception ex) { Log.Error("recuperar la copia del config", ex); }
        }

        return new ConfigArbol(raiz ?? new JsonObject(), texto, ruta);

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

    // Borrar una clave, que no es lo mismo que ponerla a null.
    //
    // Hace falta para los roles de color: un tema que no trae Secundario quiere que se
    // DERIVE, y eso se dice quitando la clave. Si al cambiar de Nube a Gruvbox se
    // dejara el "Secundario": "#F29C65" puesto, Gruvbox se quedaría con el naranja de
    // Nube para siempre y nadie entendería de dónde sale.
    public void Quitar(string seccion, string clave)
    {
        try { Seccion(seccion).Remove(clave); }
        catch (Exception ex) { Log.Error($"quitar {seccion}.{clave} del config", ex); }
    }

    // Pone el valor, o quita la clave si el valor viene a null.
    public void PonerOQuitar(string seccion, string clave, string? valor)
    {
        if (valor == null) Quitar(seccion, clave);
        else Poner(seccion, clave, valor);
    }

    // Los paneles, que son objetos y no claves sueltas. Se serializan enteros y se
    // ponen en la raíz: es la única sección del config que no es un diccionario plano.
    public void PonerPaneles(IEnumerable<PanelNube> paneles)
    {
        try
        {
            // Lo que no se ha elegido NO se escribe: `ReservarEspacio` es un bool? y un
            // null en el fichero se leería igual, pero dejarlo escrito convierte "no lo
            // he elegido" en algo que parece una decisión al abrir el config a mano.
            _raiz["Paneles"] = JsonSerializer.SerializeToNode(paneles, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (Exception ex) { Log.Error("escribir Paneles en el config", ex); }
    }

    // Una clave de un panel por su id, sin tocar el resto. Lo usa la ventana de
    // ajustes: escribir el panel entero desde ahí pisaría lo que no conoce.
    public void PonerDelPanel(string id, string clave, JsonNode? valor)
    {
        try
        {
            if (_raiz["Paneles"] is not JsonArray lista)
            {
                lista = new JsonArray();
                _raiz["Paneles"] = lista;
            }
            var panel = lista.OfType<JsonObject>()
                             .FirstOrDefault(p => p["Id"]?.GetValue<string>() == id);
            if (panel == null)
            {
                panel = new JsonObject { ["Id"] = id };
                lista.Add(panel);
            }
            panel[clave] = valor;
        }
        catch (Exception ex) { Log.Error($"escribir Paneles[{id}].{clave}", ex); }
    }

    public void PonerDelPanel(string id, string clave, string valor) => PonerDelPanel(id, clave, JsonValue.Create(valor));
    public void PonerDelPanel(string id, string clave, double valor) => PonerDelPanel(id, clave, JsonValue.Create(valor));
    public void PonerDelPanel(string id, string clave, bool valor) => PonerDelPanel(id, clave, JsonValue.Create(valor));

    // Y leerla, con su valor por defecto si el panel o la clave no están.
    public string TextoDelPanel(string id, string clave, string porDefecto)
    {
        try
        {
            if (_raiz["Paneles"] is JsonArray lista)
            {
                var p = lista.OfType<JsonObject>().FirstOrDefault(x => x["Id"]?.GetValue<string>() == id);
                if (p?[clave] is { } v) return v.GetValue<string>();
            }
        }
        catch { }
        return porDefecto;
    }

    public double NumeroDelPanel(string id, string clave, double porDefecto)
    {
        try
        {
            if (_raiz["Paneles"] is JsonArray lista)
            {
                var p = lista.OfType<JsonObject>().FirstOrDefault(x => x["Id"]?.GetValue<string>() == id);
                if (p?[clave] is { } v) return v.GetValue<double>();
            }
        }
        catch { }
        return porDefecto;
    }

    public bool BanderaDelPanel(string id, string clave, bool porDefecto)
    {
        try
        {
            if (_raiz["Paneles"] is JsonArray lista)
            {
                var p = lista.OfType<JsonObject>().FirstOrDefault(x => x["Id"]?.GetValue<string>() == id);
                if (p?[clave] is { } v) return v.GetValue<bool>();
            }
        }
        catch { }
        return porDefecto;
    }

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

    // Escribir un fichero sin dejarlo a medias. File.WriteAllText trunca y luego
    // escribe: si el proceso muere ahí (o se va la luz), el config queda vacío. Con
    // un temporal y File.Replace el cambio es atómico, y de paso Windows deja la
    // versión anterior en el .bak, que así está siempre al día en vez de ser una
    // foto de la primera vez que se abrieron los ajustes.
    public static void EscribirAtomico(string ruta, string texto, string? copia = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var tmp = ruta + ".tmp";
        File.WriteAllText(tmp, texto);
        if (File.Exists(ruta))
        {
            if (copia != null) File.Replace(tmp, ruta, copia, true);
            else File.Replace(tmp, ruta, null, true);
        }
        else File.Move(tmp, ruta);
    }

    public string Serializar() => _raiz.ToJsonString(Formato);

    public void Guardar()
    {
        try
        {
            EscribirAtomico(_ruta, Serializar(), RutaCopia);
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
            if (_original.Length > 0) EscribirAtomico(_ruta, _original, RutaCopia);
        }
        catch (Exception ex) { Log.Error("revertir config", ex); }
    }

    public bool HaCambiado() => Serializar() != _original;
}
