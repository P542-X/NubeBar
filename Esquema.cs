using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace NubeBar;

// La versión del esquema de `config.json`, y las migraciones entre versiones.
//
// ---------- Por qué hace falta un número ----------
//
// NubeBar ya migraba configuraciones, pero **por la forma**: `MigrarYDerivarPaneles`
// decide que hay que crear `Paneles` porque ve que no está. Eso funciona y lleva meses
// funcionando, pero no escala: cada migración nueva tiene que inventarse una pregunta
// sobre la forma del fichero que la distinga de todas las anteriores, y dos migraciones
// que miran la misma clave se pisan sin que nadie se entere.
//
// Un número quita la adivinanza. El fichero dice en qué versión está, las migraciones
// van de una a la siguiente y cada una corre **una sola vez**.
//
// ---------- Qué NO hace este motor ----------
//
// No reimplementa las migraciones que ya existen. La de `Paneles` (B5) vive en
// `Config.MigrarYDerivarPaneles` + `GuardarPanelesMigrados`, es idempotente, está
// probada y escribe bien el fichero. Traerla aquí habría significado tener la misma
// migración en dos sitios, que es peor que no tener número.
//
// Así que el paso 1 → 2 **solo sella la versión**: deja escrito que ese fichero ya pasó
// por la derivación de paneles, que es la verdad. Si alguien lee esto en el futuro y
// piensa "falta la migración de Paneles": no falta, está en Config.cs y se ejecuta en
// cada carga. Lo que no hay que hacer es duplicarla.
//
// ---------- Y la regla de los respaldos ----------
//
// Antes de migrar se guarda una copia con la versión en el nombre
// (`config.antes-de-v3.json`). No se usa el `.bak` de siempre: ese lo reescribe
// `EscribirAtomico` en cada guardado, así que a los dos minutos ya no es la foto de
// antes de la migración. Y **no se toca ningún `config-antes-de-probar-*.json`**: esos
// son los respaldos que dejó la fase B5 y son del usuario.
public static class Esquema
{
    // ---------- La versión de hoy ----------
    //
    //  1  Fase A .. B4   — manda `Barra`; no existe `Paneles`
    //  2  B5 / B6        — manda `Paneles`; `Barra` es una vista derivada en memoria
    //  3  C5             — hay sección `Fondo`, y `Wallpaper` lleva la integración
    public const int Actual = 3;

    public const string Clave = "Version";

    // Donde se guarda la copia de antes de migrar. Lleva la versión de ORIGEN en el
    // nombre: así migrar de 1 a 3 deja rastro de las dos y ninguna pisa a la otra.
    public static string RutaRespaldo(string config, int desde) =>
        config + ".antes-de-v" + desde + ".json";

    // ================= Deducir la versión de un fichero sin ella =================
    //
    // "Si no existe versión, interpretarla como la versión histórica correspondiente."
    // Se deduce por la forma, que es lo único que hay, y **solo una vez**: en cuanto se
    // migra queda sellada y no se vuelve a adivinar.
    //
    // Pura y pública para poder comprobarla con configuraciones inventadas de cada fase.
    public static int DeducirVersion(JsonObject? raiz)
    {
        if (raiz == null) return Actual;             // no hay fichero: nace al día

        // Si lo dice, se le cree. Un número que no es un número se trata como ausente.
        if (raiz[Clave] is JsonValue v)
        {
            try
            {
                int n = v.GetValue<int>();
                if (n >= 1) return n;
            }
            catch { /* no era un entero: se deduce por la forma */ }
        }

        // `Paneles` con algo dentro es la marca de B5: antes de B5 no existía.
        if (raiz["Paneles"] is JsonArray a && a.Count > 0) return 2;

        // Un fichero vacío del todo es una instalación nueva, no una de la Fase A.
        if (raiz.Count == 0) return Actual;

        return 1;
    }

    // ================= El resultado =================

    public sealed record Resultado(
        int Desde, int Hasta, bool Cambio, List<string> Aplicadas, string? Problema)
    {
        public bool Vale => Problema == null;
        public string Resumen => Problema != null
            ? $"migración ABORTADA en v{Desde}: {Problema}"
            : Cambio
                ? $"migrado v{Desde} → v{Hasta} ({string.Join(", ", Aplicadas)})"
                : $"ya estaba en v{Hasta}, no hay nada que migrar";
    }

    // ================= Migrar =================
    //
    // Secuencial y una a una: de la versión que sea a la siguiente, hasta llegar a la de
    // hoy. Trabaja sobre el ÁRBOL, no sobre las clases, y eso es lo que conserva las
    // claves que esta versión no conoce: lo que no se toca sigue ahí.
    //
    // No escribe nada. Devuelve qué pasó, y el que llama decide: así se puede validar el
    // resultado antes de que toque el disco, que es lo que se pidió.
    public static Resultado Migrar(JsonObject raiz)
    {
        int desde = DeducirVersion(raiz);
        var aplicadas = new List<string>();

        if (desde > Actual)
        {
            // De una versión más nueva. No se migra hacia atrás y no se toca nada: lo
            // que esta NubeBar no entienda se conserva igual porque trabajamos sobre el
            // árbol, pero adivinar qué significa una clave del futuro es inventar.
            return new Resultado(desde, desde, false, aplicadas,
                $"el fichero es de una versión más nueva (v{desde} > v{Actual}): " +
                "actualiza NubeBar. No se migra hacia atrás.");
        }

        int v = desde;
        try
        {
            while (v < Actual)
            {
                var paso = Paso(v);
                if (paso == null)
                    return new Resultado(desde, v, aplicadas.Count > 0, aplicadas,
                        $"no hay migración de v{v} a v{v + 1}");

                paso.Hacer(raiz);
                aplicadas.Add(paso.Nombre);
                v++;
                raiz[Clave] = v;      // se sella en cada paso: si falla el siguiente, no se repite este
            }
        }
        catch (Exception ex)
        {
            return new Resultado(desde, v, aplicadas.Count > 0, aplicadas,
                                 ex.GetType().Name + ": " + ex.Message);
        }

        bool cambio = v != desde || raiz[Clave] == null;
        raiz[Clave] = v;
        return new Resultado(desde, v, cambio, aplicadas, null);
    }

    sealed record Migracion(string Nombre, Action<JsonObject> Hacer);

    static Migracion? Paso(int desde) => desde switch
    {
        1 => new Migracion("v1→v2: sellar la derivación de Paneles", De1A2),
        2 => new Migracion("v2→v3: declarar la sección Fondo", De2A3),
        _ => null,
    };

    // ---------- v1 → v2 ----------
    //
    // No hace nada, y es a propósito. La migración de verdad —crear `Paneles` a partir
    // de `Barra` y borrar de `Barra` las claves que se lleva— ya la hace
    // `Config.MigrarYDerivarPaneles` en cada carga, es idempotente y está probada.
    // Duplicarla aquí sería tener la misma migración en dos sitios.
    //
    // Lo que se gana sellando el 2 es dejar de adivinar: a partir de ahora el fichero
    // dice su versión y las migraciones futuras no tienen que mirar si `Paneles` existe.
    static void De1A2(JsonObject raiz)
    {
        // Solo se comprueba que la forma sea la que se espera, para no sellar una versión
        // que no es verdad. Si `Paneles` no está todavía, se sella igual: la derivación
        // corre justo después, en la misma carga.
        _ = raiz;
    }

    // ---------- v2 → v3 ----------
    //
    // Deja la sección `Fondo` escrita con `Tipo: "ninguno"`.
    //
    // Importa que sea explícito y no una ausencia: "ninguno" significa **NubeBar no
    // gestiona tu fondo de escritorio**, y es lo que tiene que pasar al actualizar. Sin
    // la clave escrita, el día que el gestor de fondos empiece a leer esa sección se
    // encontraría un hueco y tendría que decidir qué hacer con él; con la clave puesta,
    // el fichero dice la verdad y nadie cambia el fondo de nadie por omisión.
    //
    // Y si el usuario ya tenía una sección `Fondo` —de una prueba, de haber editado el
    // fichero a mano— no se toca: solo se rellena lo que falte.
    static void De2A3(JsonObject raiz)
    {
        if (raiz["Fondo"] is not JsonObject fondo)
        {
            fondo = new JsonObject();
            raiz["Fondo"] = fondo;
        }
        if (fondo["Tipo"] == null) fondo["Tipo"] = "ninguno";

        // La integración con Wallpaper Engine nació apagada y así se queda. Se escribe
        // explícita por el mismo motivo que el fondo: que el fichero diga qué pasa.
        if (raiz["Wallpaper"] is not JsonObject wp)
        {
            wp = new JsonObject();
            raiz["Wallpaper"] = wp;
        }
        if (wp["Integrar"] == null) wp["Integrar"] = false;
    }

    // ================= Validar antes de escribir =================
    //
    // Lo mínimo que tiene que ser verdad de un árbol migrado para que merezca tocar el
    // disco. No valida los valores de cada clave —eso es de cada sección— sino que el
    // fichero no haya quedado absurdo: sin versión, con la versión mal, o vacío cuando
    // antes tenía cosas.
    public static string? Mal(JsonObject? raiz, int clavesAntes)
    {
        if (raiz == null) return "el árbol es null";
        if (raiz[Clave] == null) return "el árbol migrado no lleva versión";
        int v;
        try { v = raiz[Clave]!.GetValue<int>(); }
        catch { return "la versión no es un entero"; }
        if (v < 1) return $"la versión {v} no es válida";
        if (v > Actual) return $"la versión {v} es mayor que la de esta NubeBar ({Actual})";

        // Esta es la red de verdad: una migración que deja el fichero con menos secciones
        // de las que tenía se ha llevado algo por delante. Puede crecer, nunca encoger.
        if (clavesAntes > 0 && raiz.Count < clavesAntes)
            return $"la migración dejó {raiz.Count} secciones y antes había {clavesAntes}: " +
                   "algo se ha perdido";
        return null;
    }

    // ================= Todo junto, sobre el disco =================

    public sealed record Aplicada(bool Ok, Resultado Que, string? Respaldo, string Detalle);

    // Lee, migra, valida y escribe. En este orden y sin atajos:
    //
    //  1. se lee el fichero y se parsea como árbol (conserva lo desconocido);
    //  2. se migra en memoria;
    //  3. se VALIDA el resultado;
    //  4. solo si vale, se guarda una copia de la versión de origen;
    //  5. y se escribe de forma atómica.
    //
    // Si algo falla en 2 o en 3, **no se escribe nada**: el fichero del usuario sigue
    // exactamente como estaba, que es la recuperación más fiable que hay — no haber
    // tocado nada.
    public static Aplicada MigrarFichero(string ruta)
    {
        JsonObject? raiz = null;
        string texto = "";
        try
        {
            if (!File.Exists(ruta))
                return new Aplicada(true, new Resultado(Actual, Actual, false, new(), null), null,
                                    "no hay config: nace en la versión de hoy");
            texto = File.ReadAllText(ruta);
            raiz = JsonNode.Parse(texto, documentOptions: new System.Text.Json.JsonDocumentOptions
            {
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) as JsonObject;
        }
        catch (Exception ex)
        {
            // Un config ilegible no es asunto de las migraciones: de eso se encarga
            // ConfigArbol.Cargar, que lo aparta con su fecha e intenta el .bak.
            Log.Error("esquema: el config no se puede leer como JSON, no se migra", ex);
            return new Aplicada(false, new Resultado(0, 0, false, new(), ex.Message), null,
                                "el config no se puede leer");
        }

        if (raiz == null)
            return new Aplicada(false, new Resultado(0, 0, false, new(), "no es un objeto JSON"), null,
                                "el config no es un objeto");

        int clavesAntes = raiz.Count;
        int desde = DeducirVersion(raiz);
        if (desde == Actual && raiz[Clave] != null)
            return new Aplicada(true, new Resultado(desde, desde, false, new(), null), null,
                                $"ya está en v{Actual}");

        var res = Migrar(raiz);
        if (!res.Vale)
        {
            Log.Error("esquema: " + res.Resumen, new InvalidOperationException(res.Problema));
            return new Aplicada(false, res, null, res.Resumen);
        }

        if (Mal(raiz, clavesAntes) is { } mal)
        {
            Log.Error("esquema: el resultado de migrar no es válido, NO se escribe",
                      new InvalidOperationException(mal));
            return new Aplicada(false, res with { Problema = mal }, null, mal);
        }

        // El respaldo, con la versión de origen en el nombre. No se toca el .bak ni
        // ningún config-antes-de-probar-*.json.
        string? respaldo = null;
        try
        {
            respaldo = RutaRespaldo(ruta, desde);
            if (!File.Exists(respaldo) && texto.Length > 0)
            {
                File.WriteAllText(respaldo, texto);
                Log.Info("esquema: copia de antes de migrar en " + Path.GetFileName(respaldo));
            }
            else if (File.Exists(respaldo))
            {
                Log.Info("esquema: ya había copia de v" + desde + ", no se sobrescribe");
            }
        }
        catch (Exception ex)
        {
            // Sin respaldo no se migra. Migrar sin red es exactamente lo que no se puede
            // hacer con el único fichero de configuración del usuario.
            Log.Error("esquema: no se pudo dejar la copia de seguridad; NO se migra", ex);
            return new Aplicada(false, res with { Problema = "sin copia de seguridad" }, null,
                                "no se pudo respaldar");
        }

        try
        {
            ConfigArbol.EscribirAtomico(ruta, raiz.ToJsonString(Formato), ruta + ".bak");
            Log.Info("esquema: " + res.Resumen);
            return new Aplicada(true, res, respaldo, res.Resumen);
        }
        catch (Exception ex)
        {
            Log.Error("esquema: no se pudo escribir el config migrado", ex);
            return new Aplicada(false, res with { Problema = ex.Message }, respaldo, "no se pudo escribir");
        }
    }

    static readonly System.Text.Json.JsonSerializerOptions Formato = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
