using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace NubeBar;

// D7: recuperar la configuración cuando el fichero se ha quedado a medias.
//
// ---------- El fallo que esto cubre ----------
//
// El documento lo pide con esas palabras: *"recuperar config desde copia despues de una
// escritura interrumpida"*. `ConfigArbol.EscribirAtomico` ya escribe a un `.tmp` y hace
// `File.Replace`, así que una escritura interrumpida **normalmente** no deja el config a
// medias. Pero "normalmente" no es "nunca":
//
//  - Un corte de luz entre `WriteAllText` y `File.Replace` deja un `.tmp` huérfano.
//  - Un disco lleno puede dejar el `.tmp` truncado y el `Replace` escribirlo encima.
//  - Y hay más de un camino que escribe el config: el modelo tipado, el árbol, y las
//    migraciones. No todos pasan por el mismo sitio.
//
// Hoy, si el config no se puede leer, `Config.Cargar()` devuelve null y NubeBar arranca
// **con la configuración de serie**: tema por defecto, módulos por defecto, el trabajo del
// usuario aparentemente perdido. El fichero sigue ahí y los respaldos también, pero el
// usuario no lo sabe y lo que ve es su escritorio en blanco.
//
// ---------- Y lo que NO hace ----------
//
// No sobrescribe nada por su cuenta. Elige el mejor candidato y lo DEVUELVE; quién lo usa y
// si lo guarda es decisión de quien llama. Un rescate que escribe solo es un rescate que
// puede estropear el fichero bueno.
public static class HyprRescate
{
    // ================= ¿Está sano este JSON? =================
    //
    // "Se puede parsear" no es suficiente: `{}` se parsea perfectamente y no es una
    // configuración. Lo que hace que un árbol valga es que sea un objeto y que tenga
    // `Version`, que es lo que necesitan las migraciones para saber de dónde parte.
    public enum Estado { Bien, NoExiste, Vacio, NoEsJson, SinVersion }

    public sealed record Juicio(Estado Como, int Version, string Porque)
    {
        public bool Sirve => Como == Estado.Bien;
        public override string ToString() => Sirve ? $"bien (v{Version})" : $"{Como}: {Porque}";
    }

    public static Juicio Juzgar(string? texto)
    {
        if (texto == null) return new(Estado.NoExiste, 0, "no hay fichero");
        if (texto.Trim().Length == 0) return new(Estado.Vacio, 0, "el fichero está vacío");

        JsonNode? nodo;
        try { nodo = JsonNode.Parse(texto); }
        catch (Exception ex) { return new(Estado.NoEsJson, 0, "no es JSON válido: " + ex.GetType().Name); }

        if (nodo is not JsonObject obj)
            return new(Estado.NoEsJson, 0, "el JSON no es un objeto");

        // `Version` puede faltar legítimamente en un config muy viejo: las migraciones lo
        // tratan como v1. Lo que NO puede es estar y no ser un número.
        if (!obj.ContainsKey("Version"))
            return new(Estado.Bien, 1, "sin Version: se trata como v1, que es lo que hacen las migraciones");

        try
        {
            int v = obj["Version"]!.GetValue<int>();
            if (v <= 0) return new(Estado.SinVersion, 0, $"Version={v} no tiene sentido");
            return new(Estado.Bien, v, "");
        }
        catch { return new(Estado.SinVersion, 0, "Version no es un número"); }
    }

    public static Juicio JuzgarFichero(string ruta)
    {
        try { return Juzgar(File.Exists(ruta) ? File.ReadAllText(ruta) : null); }
        catch (Exception ex) { return new(Estado.NoExiste, 0, "no se pudo leer: " + ex.GetType().Name); }
    }

    // ================= Los candidatos a rescate =================
    //
    // Por orden de preferencia, y el orden es una decisión:
    //
    //  1. El `.tmp` huérfano. Es lo MÁS NUEVO que escribió el usuario: si existe, la
    //     escritura se cortó justo después de dejarlo listo y antes de sustituir. Si está
    //     sano, es exactamente lo que el usuario quería guardar.
    //  2. El `.bak`, que es la copia que deja `File.Replace` en cada guardado: el config
    //     anterior, de hace segundos o minutos.
    //  3. Los respaldos de migración `antes-de-vN`, del más nuevo al más viejo. Son de una
    //     versión anterior del esquema, así que hay que migrarlos — pero el contenido del
    //     usuario está entero.
    //
    // Un candidato que no pasa el juicio se salta, no se usa a medias.
    public sealed record Candidato(string Ruta, string Que, Juicio Juicio, DateTime Cuando);

    public static List<Candidato> Candidatos(string config)
    {
        var lista = new List<Candidato>();

        void Mirar(string ruta, string que)
        {
            if (!File.Exists(ruta)) return;
            DateTime cuando;
            try { cuando = File.GetLastWriteTime(ruta); } catch { cuando = DateTime.MinValue; }
            lista.Add(new Candidato(ruta, que, JuzgarFichero(ruta), cuando));
        }

        Mirar(config + ".tmp", "la escritura que se quedó a medias");
        Mirar(config + ".bak", "el config anterior");

        // Los respaldos de migración, del esquema más nuevo al más viejo.
        for (int v = Esquema.Actual; v >= 1; v--)
            Mirar(Esquema.RutaRespaldo(config, v), $"el respaldo de antes de la v{v}");

        return lista;
    }

    // El mejor: el primero de la lista que sirva. El orden de `Candidatos` ES la
    // preferencia, así que aquí no se vuelve a decidir — si se decidiera en dos sitios, un
    // día dirían cosas distintas.
    public static Candidato? Mejor(string config) =>
        Candidatos(config).FirstOrDefault(c => c.Juicio.Sirve);

    // ================= El informe =================
    //
    // Lo que se apunta cuando el config no se puede leer. Va completo a propósito: el
    // usuario tiene que poder ver qué había, qué se ha usado y qué se ha dejado quieto, sin
    // tener que fiarse de una línea que diga "recuperado".
    public static List<string> Informe(string config)
    {
        var lineas = new List<string>();
        var juicio = JuzgarFichero(config);
        lineas.Add($"[D7]  config={config}");
        lineas.Add($"[D7]  estado del config: {juicio}");

        if (juicio.Sirve)
        {
            lineas.Add("[D7]  no hay nada que rescatar: el config se lee bien");
            return lineas;
        }

        var todos = Candidatos(config);
        if (todos.Count == 0)
        {
            lineas.Add("[D7]  no hay ningún respaldo: NubeBar arrancará con la configuración de serie");
            return lineas;
        }

        lineas.Add($"[D7]  {todos.Count} candidato(s) a rescate:");
        foreach (var c in todos)
            lineas.Add($"        {c.Que}: {c.Juicio}  ({c.Cuando:yyyy-MM-dd HH:mm:ss})  {c.Ruta}");

        var mejor = todos.FirstOrDefault(c => c.Juicio.Sirve);
        lineas.Add(mejor == null
            ? "[D7]  ninguno sirve: ni uno pasa el juicio, así que no se toca nada"
            : $"[D7]  se usaría «{mejor.Que}» (v{mejor.Juicio.Version}). " +
              "El fichero roto NO se borra: se queda para que puedas mirarlo.");
        return lineas;
    }

    // ================= Cargar con rescate =================
    //
    // Devuelve el árbol que se puede usar y de dónde salió. Si el config está bien, de él;
    // si no, del mejor candidato; y si no hay ninguno, null — y entonces quien llama hace lo
    // que hacía antes, que es arrancar de serie.
    //
    // No escribe. Que el rescate se guarde o no es decisión de quien llama, y en un
    // diagnóstico la respuesta es que no.
    public sealed record Rescatado(JsonObject Arbol, string DeDonde, int Version);

    public static Rescatado? Cargar(string config)
    {
        var juicio = JuzgarFichero(config);
        if (juicio.Sirve)
        {
            try
            {
                var txt = File.ReadAllText(config);
                if (JsonNode.Parse(txt) is JsonObject obj)
                    return new Rescatado(obj, "el config", juicio.Version);
            }
            catch (Exception ex) { Log.Error("leer el config que parecía sano", ex); }
        }

        var mejor = Mejor(config);
        if (mejor == null) return null;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(mejor.Ruta)) is JsonObject obj)
                return new Rescatado(obj, mejor.Que, mejor.Juicio.Version);
        }
        catch (Exception ex) { Log.Error($"leer el respaldo {mejor.Ruta}", ex); }
        return null;
    }
}
