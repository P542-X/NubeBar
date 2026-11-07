using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D5: las reglas. "Esta aplicación, ahí."
//
// ---------- La decisión que lo gobierna todo ----------
//
// **Una regla sin criterios no coincide con nada.** Lo contrario —que coincida con todo—
// es el valor por defecto que parece cómodo y es el que convierte un error de tecleo en
// "todas mis ventanas se han ido al escritorio 3". Una regla vacía es una regla a medio
// escribir, y a medio escribir no se aplica.
//
// ---------- Y por qué cada regla explica lo que hace ----------
//
// "Mi navegador aparece en un sitio raro" no se investiga leyendo el código: se investiga
// sabiendo QUÉ regla coincidió y por qué las demás no. Así que `Resolver` no devuelve solo
// la decisión: devuelve una línea por regla, con el motivo. Es lo que pide la
// especificación —"vista de diagnóstico: regla coincidente, acción aplicada y motivo de
// descarte"— y es lo que convierte una sorpresa en un dato.
public static class HyprReglas
{
    // ================= Una regla =================
    //
    // Los criterios son todos opcionales y se combinan con Y: los que estén puestos tienen
    // que cumplirse. Las acciones también son opcionales, y `null` significa **esta regla
    // no opina de eso** — no "ponlo a cero".
    //
    // Esa distinción es la que permite que dos reglas se sumen: una dice el espacio y otra
    // dice que sea flotante, y la ventana acaba flotante en ese espacio.
    public sealed record Regla(
        string Id,
        int Prioridad = 0,

        // ---------- Criterios ----------
        string? Proceso = null,     // "firefox" — exacto, sin ruta ni extensión
        string? Clase = null,       // la clase de ventana — exacta
        string? Titulo = null,      // contiene, sin distinguir acentos ni mayúsculas
        string? Ruta = null,        // contiene, sobre la ruta completa del ejecutable

        // ---------- Acciones ----------
        int? Espacio = null,
        int? Monitor = null,
        NubeHypr.Layout? Layout = null,
        bool? Flotante = null,
        double? Ancho = null,
        double? Alto = null,
        bool? Foco = null,

        bool Activa = true)
    {
        // Cuántos criterios tiene puestos. Es el desempate cuando dos reglas tienen la
        // misma prioridad: la más específica gana, porque es la que el usuario escribió
        // pensando en un caso concreto.
        public int Especificidad =>
            (Proceso != null ? 1 : 0) + (Clase != null ? 1 : 0) +
            (Titulo != null ? 1 : 0) + (Ruta != null ? 1 : 0);

        public bool SinCriterios => Especificidad == 0;

        // ¿Dice algo esta regla? Una sin acciones no hace nada, y eso también es una regla
        // a medio escribir.
        public bool SinAcciones =>
            Espacio == null && Monitor == null && Layout == null && Flotante == null &&
            Ancho == null && Alto == null && Foco == null;

        public override string ToString()
        {
            var crit = new List<string>();
            if (Proceso != null) crit.Add($"proceso={Proceso}");
            if (Clase != null) crit.Add($"clase={Clase}");
            if (Titulo != null) crit.Add($"título~{Titulo}");
            if (Ruta != null) crit.Add($"ruta~{Ruta}");

            var acc = new List<string>();
            if (Espacio != null) acc.Add($"espacio={Espacio}");
            if (Monitor != null) acc.Add($"monitor={Monitor}");
            if (Layout != null) acc.Add($"layout={Layout}");
            if (Flotante != null) acc.Add($"flotante={(Flotante.Value ? "sí" : "no")}");
            if (Ancho != null || Alto != null) acc.Add($"tamaño={Tamano(Ancho, Alto)}");
            if (Foco != null) acc.Add($"foco={(Foco.Value ? "sí" : "no")}");

            return $"[{Id}] p{Prioridad} {(crit.Count > 0 ? string.Join(" ", crit) : "SIN CRITERIOS")}" +
                   $" -> {(acc.Count > 0 ? string.Join(" ", acc) : "SIN ACCIONES")}" +
                   (Activa ? "" : " (desactivada)");
        }
    }

    // ---------- Cómo se escribe un tamaño ----------
    //
    // Dos fallos en una sola línea, y los dos se vieron en una captura del editor: una regla
    // con ancho 0,5 y sin alto salía como **«tamaño=1x0»**.
    //
    //  - El formato era `0`, que redondea: media pantalla se enseñaba como una pantalla
    //    entera. Un ancho es una fracción, y una fracción necesita decimales.
    //  - Y un alto que NADIE ha puesto salía como `0`, que no es lo que significa: cero
    //    sería una ventana sin altura. Lo que hay es un hueco, y un hueco se escribe «—».
    //
    // Se escribe en un sitio porque estaba repetido en tres.
    public static string Tamano(double? ancho, double? alto) =>
        $"{Trozo(ancho)}x{Trozo(alto)}";

    static string Trozo(double? v) =>
        v == null ? "—" : v.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // ================= Validar =================
    //
    // Antes de guardar, no después. Una regla inválida en el fichero es una sorpresa cada
    // vez que se abre una ventana.
    public static bool Valida(Regla r, out string porque)
    {
        if (string.IsNullOrWhiteSpace(r.Id)) { porque = "una regla sin id no se puede deshacer por separado"; return false; }
        if (r.SinCriterios) { porque = "sin criterios no coincidiría con nada: está a medio escribir"; return false; }
        if (r.SinAcciones) { porque = "sin acciones no haría nada"; return false; }
        if (r.Espacio is < 0) { porque = "un espacio no puede ser negativo"; return false; }
        if (r.Monitor is < 0) { porque = "un monitor no puede ser negativo"; return false; }
        if (r.Ancho is <= 0 || r.Alto is <= 0) { porque = "un tamaño tiene que ser positivo"; return false; }
        if (r.Ancho is > 20000 || r.Alto is > 20000) { porque = "ese tamaño no cabe en ninguna pantalla"; return false; }
        porque = "";
        return true;
    }

    // ================= Coincidir =================
    //
    // Proceso y clase, exactos y sin distinguir mayúsculas. Título y ruta, "contiene" y
    // además **sin distinguir acentos**: quien escribe una regla para "Configuración" no
    // tiene por qué acordarse de la tilde, y `Parecido.Normalizar` ya hace eso en el
    // buscador — misma regla en los dos sitios.
    public static bool Coincide(Regla r, VentanaModelo v, string clase, string ruta, out string porque)
    {
        if (!r.Activa) { porque = "está desactivada"; return false; }
        if (r.SinCriterios) { porque = "no tiene criterios: no coincide con nada"; return false; }

        if (r.Proceso != null &&
            !string.Equals(r.Proceso.Trim(), v.Proceso, StringComparison.OrdinalIgnoreCase))
        {
            porque = $"el proceso es «{v.Proceso}» y la regla pide «{r.Proceso}»";
            return false;
        }

        if (r.Clase != null &&
            !string.Equals(r.Clase.Trim(), clase, StringComparison.OrdinalIgnoreCase))
        {
            porque = $"la clase es «{clase}» y la regla pide «{r.Clase}»";
            return false;
        }

        if (r.Titulo != null && !Contiene(v.Titulo, r.Titulo))
        {
            porque = $"el título «{Corto(v.Titulo)}» no contiene «{r.Titulo}»";
            return false;
        }

        if (r.Ruta != null && !Contiene(ruta, r.Ruta))
        {
            porque = $"la ruta no contiene «{r.Ruta}»";
            return false;
        }

        porque = "";
        return true;
    }

    static bool Contiene(string? donde, string que)
    {
        if (string.IsNullOrEmpty(que)) return true;
        var a = Parecido.Normalizar(donde ?? "");
        var b = Parecido.Normalizar(que);
        return b.Length == 0 || a.Contains(b, StringComparison.Ordinal);
    }

    static string Corto(string s) => s.Length <= 40 ? s : s[..37] + "...";

    // ================= El orden: prioridad, especificidad, posición =================
    //
    // ---------- Por qué tres niveles y no uno ----------
    //
    // La prioridad explícita manda, porque es lo que el usuario puso para resolver un
    // empate a mano. Pero si dos reglas tienen la misma prioridad —y lo normal es que
    // todas tengan 0— hace falta una regla que no sea "la primera que pillemos": gana la
    // **más específica**, porque una regla con proceso y título se escribió pensando en un
    // caso concreto, y una con solo el proceso es la general.
    //
    // Y si siguen empatadas, gana **la que está antes en la lista**. Es estable y
    // predecible: el orden del fichero manda y el usuario puede verlo.
    public static List<Regla> Ordenar(IEnumerable<Regla> reglas)
    {
        return reglas
            .Select((r, i) => (r, i))
            .OrderByDescending(x => x.r.Prioridad)
            .ThenByDescending(x => x.r.Especificidad)
            .ThenBy(x => x.i)
            .Select(x => x.r)
            .ToList();
    }

    // ================= La decisión =================
    //
    // Lo que sale de aplicar todas las reglas a una ventana. Cada campo lo pone la regla
    // de más peso que opine de él, y se guarda **quién** lo puso: sin eso, "¿por qué esta
    // ventana es flotante?" no tiene respuesta.
    public sealed record Decision
    {
        public int? Espacio { get; init; }
        public int? Monitor { get; init; }
        public NubeHypr.Layout? Layout { get; init; }
        public bool? Flotante { get; init; }
        public double? Ancho { get; init; }
        public double? Alto { get; init; }
        public bool? Foco { get; init; }

        // Campo -> id de la regla que lo decidió
        public Dictionary<string, string> Quien { get; init; } = new();

        public bool DiceAlgo => Espacio != null || Monitor != null || Layout != null ||
                                Flotante != null || Ancho != null || Alto != null || Foco != null;

        public override string ToString()
        {
            if (!DiceAlgo) return "ninguna regla opina";
            var partes = new List<string>();
            if (Espacio != null) partes.Add($"espacio={Espacio} ({Quien.GetValueOrDefault("Espacio", "?")})");
            if (Monitor != null) partes.Add($"monitor={Monitor} ({Quien.GetValueOrDefault("Monitor", "?")})");
            if (Layout != null) partes.Add($"layout={Layout} ({Quien.GetValueOrDefault("Layout", "?")})");
            if (Flotante != null) partes.Add($"flotante={(Flotante.Value ? "sí" : "no")} ({Quien.GetValueOrDefault("Flotante", "?")})");
            if (Ancho != null || Alto != null) partes.Add($"tamaño={Tamano(Ancho, Alto)} ({Quien.GetValueOrDefault("Tamano", "?")})");
            if (Foco != null) partes.Add($"foco={(Foco.Value ? "sí" : "no")} ({Quien.GetValueOrDefault("Foco", "?")})");
            return string.Join(", ", partes);
        }
    }

    // Una línea por regla: coincidió o no, y por qué. Es la vista de diagnóstico que pide
    // la especificación.
    public readonly record struct Explicacion(string Id, bool Coincidio, string Porque, string Aplico)
    {
        public override string ToString() =>
            Coincidio
                ? $"  [{Id}] COINCIDE -> {(Aplico.Length > 0 ? Aplico : "no aportó nada nuevo")}"
                : $"  [{Id}] no: {Porque}";
    }

    public sealed record Resultado(Decision Decision, List<Explicacion> Explicaciones)
    {
        public int Coincidencias => Explicaciones.Count(e => e.Coincidio);
    }

    // ---------- Resolver ----------
    //
    // Las reglas se recorren **de más peso a menos**, y cada campo lo pone la primera que
    // opine. Así una regla de prioridad alta no se ve pisada por una general que viene
    // después, que es justo lo que la gente espera de una prioridad.
    public static Resultado Resolver(VentanaModelo v, IEnumerable<Regla> reglas,
                                      string clase = "", string ruta = "")
    {
        var explicaciones = new List<Explicacion>();
        int? espacio = null, monitor = null;
        NubeHypr.Layout? layout = null;
        bool? flotante = null, foco = null;
        double? ancho = null, alto = null;
        var quien = new Dictionary<string, string>();

        foreach (var r in Ordenar(reglas))
        {
            if (!Coincide(r, v, clase, ruta, out string porque))
            {
                explicaciones.Add(new Explicacion(r.Id, false, porque, ""));
                continue;
            }

            // Lo que esta regla aporta de NUEVO. Si otra de más peso ya lo dijo, esta no
            // lo pisa — y se dice que no aportó nada, que es información: explica por qué
            // una regla "que coincide" no se nota.
            var aporta = new List<string>();
            if (r.Espacio != null && espacio == null) { espacio = r.Espacio; quien["Espacio"] = r.Id; aporta.Add($"espacio={r.Espacio}"); }
            if (r.Monitor != null && monitor == null) { monitor = r.Monitor; quien["Monitor"] = r.Id; aporta.Add($"monitor={r.Monitor}"); }
            if (r.Layout != null && layout == null) { layout = r.Layout; quien["Layout"] = r.Id; aporta.Add($"layout={r.Layout}"); }
            if (r.Flotante != null && flotante == null) { flotante = r.Flotante; quien["Flotante"] = r.Id; aporta.Add($"flotante={(r.Flotante.Value ? "sí" : "no")}"); }
            if (r.Foco != null && foco == null) { foco = r.Foco; quien["Foco"] = r.Id; aporta.Add($"foco={(r.Foco.Value ? "sí" : "no")}"); }
            if ((r.Ancho != null || r.Alto != null) && ancho == null && alto == null)
            {
                ancho = r.Ancho; alto = r.Alto; quien["Tamano"] = r.Id;
                aporta.Add($"tamaño={Tamano(r.Ancho, r.Alto)}");
            }

            explicaciones.Add(new Explicacion(r.Id, true, "", string.Join(" ", aporta)));
        }

        var d = new Decision
        {
            Espacio = espacio, Monitor = monitor, Layout = layout, Flotante = flotante,
            Ancho = ancho, Alto = alto, Foco = foco, Quien = quien,
        };
        return new Resultado(d, explicaciones);
    }

    // ================= Guardar, deshacer y rollback =================
    //
    // ---------- Deshacer UNA regla sin tocar las demás ----------
    //
    // Es lo que pide la especificación, y la forma de hacerlo bien es que cada regla tenga
    // un id: quitar "la tercera" se rompe en cuanto el usuario reordena.
    public static List<Regla> Quitar(IEnumerable<Regla> reglas, string id) =>
        reglas.Where(r => !string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();

    // Desactivar en vez de borrar: el usuario puede querer apagar una regla un rato sin
    // perder lo que escribió. Y es reversible, que es más de lo que se puede decir de
    // borrarla.
    public static List<Regla> Alternar(IEnumerable<Regla> reglas, string id) =>
        reglas.Select(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)
                           ? r with { Activa = !r.Activa } : r).ToList();

    public static List<Regla> Poner(IEnumerable<Regla> reglas, Regla nueva)
    {
        var lista = reglas.ToList();
        int i = lista.FindIndex(r => string.Equals(r.Id, nueva.Id, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) lista[i] = nueva;      // se actualiza en su sitio: no se mueve en el orden
        else lista.Add(nueva);
        return lista;
    }

    // Un id libre, con el mismo criterio que `Perfiles.IdLibre`: a partir de lo que el
    // usuario escribió, sin acentos, y con un número si ya existe.
    public static string IdLibre(IEnumerable<Regla> reglas, string? propuesto)
    {
        var usados = new HashSet<string>(reglas.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var baseId = Parecido.Normalizar(propuesto ?? "").Replace(' ', '-');
        if (baseId.Length == 0) baseId = "regla";
        if (!usados.Contains(baseId)) return baseId;
        for (int n = 2; n < 1000; n++)
            if (!usados.Contains($"{baseId}-{n}")) return $"{baseId}-{n}";
        return baseId + "-" + Guid.NewGuid().ToString("N")[..4];
    }

    // ---------- La instantánea, para el rollback ----------
    //
    // Guardar la lista entera antes de tocarla y poder volver. Es lo mismo que hacen los
    // perfiles de C5 con las claves del config, y por el mismo motivo: aplicar algo que
    // cambia varias cosas a la vez tiene que poder deshacerse de una pieza.
    public static List<Regla> Instantanea(IEnumerable<Regla> reglas) => reglas.ToList();

    // ¿Qué cambió entre dos listas? Para el log, y para que "rollback" no sea un acto de fe.
    public static (List<string> Puestas, List<string> Quitadas, List<string> Cambiadas)
        Comparar(IReadOnlyList<Regla> antes, IReadOnlyList<Regla> ahora)
    {
        var idsA = antes.Select(r => r.Id).ToList();
        var idsB = ahora.Select(r => r.Id).ToList();
        var puestas = idsB.Where(id => !idsA.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
        var quitadas = idsA.Where(id => !idsB.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
        var cambiadas = new List<string>();
        foreach (var a in antes)
        {
            var b = ahora.FirstOrDefault(x => string.Equals(x.Id, a.Id, StringComparison.OrdinalIgnoreCase));
            if (b != null && b != a) cambiadas.Add(a.Id);
        }
        return (puestas, quitadas, cambiadas);
    }

    // ================= Leer una regla sin reventar =================
    //
    // ---------- El riesgo que esto cierra ----------
    //
    // `NubeHypr.Reglas` era `List<string>` y ahora es `List<Regla>`. Si alguien tuviera
    // cadenas ahí —de una versión anterior, o de haber editado el fichero a mano—
    // `JsonSerializer` lanzaría, `Config.Cargar` devolvería null y NubeBar arrancaría con
    // la configuración de serie: **el usuario perdería su config entera por una clave**.
    //
    // Eso no es un riesgo teórico de un tipo raro: es el caso normal de un cambio de tipo.
    //
    // ---------- Y por qué tolerar en vez de migrar ----------
    //
    // Una migración arregla los ficheros que pasan por ella. Esto arregla *cualquier*
    // fichero, en cualquier orden de versiones, incluido el que alguien edite mañana. Y no
    // hace falta subir el esquema otra vez.
    //
    // Lo que entra mal **no se borra y no se inventa**: se convierte en una regla sin
    // criterios y sin acciones, con el texto original como id. Una regla así no coincide
    // con nada —esa es la regla de oro de D5— así que es inofensiva, y además **se ve** en
    // el editor como lo que es: algo que hay que arreglar o quitar.
    public sealed class LectorTolerante : System.Text.Json.Serialization.JsonConverter<Regla>
    {
        // ---------- Y `null` también pasa por aquí ----------
        //
        // Sin esto, `System.Text.Json` resuelve el `null` por su cuenta y mete un `null`
        // en la lista sin llamar a `Read`. Entonces cualquier recorrido de las reglas
        // revienta con `NullReferenceException` —y el recorrido ahora pasa cada vez que
        // aparece una ventana—. Lo encontró una prueba con `[1,true,null,"x"]`.
        public override bool HandleNull => true;


        public override Regla Read(ref System.Text.Json.Utf8JsonReader lector, Type tipo,
                                    System.Text.Json.JsonSerializerOptions opc)
        {
            // Una cadena donde se esperaba un objeto: se conserva como id y se deja inerte.
            if (lector.TokenType == System.Text.Json.JsonTokenType.String)
            {
                var texto = lector.GetString() ?? "";
                Log.Info($"reglas: «{texto}» no es una regla (es texto de una versión " +
                         "anterior); se conserva como regla vacía, que no coincide con nada");
                return new Regla(texto.Length > 0 ? texto : "sin-nombre");
            }

            // Un número, un booleano o null: lo mismo, sin perder la pista de que estaba.
            if (lector.TokenType is System.Text.Json.JsonTokenType.Number
                                  or System.Text.Json.JsonTokenType.True
                                  or System.Text.Json.JsonTokenType.False
                                  or System.Text.Json.JsonTokenType.Null)
            {
                // Nada que consumir: el lector ya está sobre el valor. Adelantarlo aquí
                // hace que `System.Text.Json` se queje de que el convertidor "leyó de más".
                Log.Info("reglas: una entrada que no es un objeto; se conserva como regla vacía");
                return new Regla("entrada-no-valida");
            }

            // Lo normal: un objeto. Se lee con el serializador de siempre, pero sin este
            // convertidor, o se llamaría a sí mismo para siempre.
            var sinMi = new System.Text.Json.JsonSerializerOptions(opc);
            for (int i = sinMi.Converters.Count - 1; i >= 0; i--)
                if (sinMi.Converters[i] is LectorTolerante) sinMi.Converters.RemoveAt(i);

            // ---------- Y el objeto se consume ENTERO antes de intentar entenderlo ----------
            //
            // Leer directamente con `Deserialize<Regla>(ref lector, ...)` y recoger la
            // excepción no sirve: si falla a mitad del objeto, el lector se queda dentro y
            // `System.Text.Json` aborta la carga completa con «el convertidor leyó de más».
            // O sea: el `try/catch` que parecía proteger la configuración la tumbaba igual,
            // solo con otro mensaje. `JsonDocument.ParseValue` consume el elemento completo
            // —válido o no— y deja el lector justo donde tiene que estar; después ya se
            // intenta convertir, y si no se puede, la regla queda inerte y la LISTA SIGUE.
            using var trozo = System.Text.Json.JsonDocument.ParseValue(ref lector);

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<Regla>(trozo.RootElement.GetRawText(), sinMi) ?? new Regla("regla-vacia");
            }
            catch (Exception ex)
            {
                // Un objeto con un campo del tipo equivocado. Se apunta y se deja inerte:
                // una regla que no se entiende no puede aplicarse a medias.
                Log.Info($"reglas: una entrada no se pudo leer ({ex.GetType().Name}); " +
                         "se conserva como regla vacía");
                return new Regla("regla-ilegible");
            }
        }

        public override void Write(System.Text.Json.Utf8JsonWriter escritor, Regla valor,
                                   System.Text.Json.JsonSerializerOptions opc)
        {
            var sinMi = new System.Text.Json.JsonSerializerOptions(opc);
            for (int i = sinMi.Converters.Count - 1; i >= 0; i--)
                if (sinMi.Converters[i] is LectorTolerante) sinMi.Converters.RemoveAt(i);
            System.Text.Json.JsonSerializer.Serialize(escritor, valor, sinMi);
        }
    }
}
