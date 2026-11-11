using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NubeBar;

// Perfiles de escritorio: un conjunto de ajustes con nombre que se aplica de golpe.
//
// ---------- Qué es un perfil, exactamente ----------
//
// **Un trozo de config.json con nombre.** No una copia del config entero, y la
// diferencia es toda la seguridad de esto: un perfil solo puede tocar las claves de una
// lista blanca (`Claves`), así que aplicarlo no puede llevarse por delante tu atajo de
// teclado, tus apps fijadas, el orden del carrusel ni nada que no sea suyo. Y como se
// escribe con `ConfigArbol` —que es el config visto como árbol JSON— lo que el perfil no
// menciona se queda exactamente como estaba, incluso lo que esta versión de NubeBar no
// conoce.
//
// ---------- Por qué no guarda el config entero ----------
//
// Porque sería un perfil que no se puede compartir ni importar. Un config lleva cosas de
// la MÁQUINA: los ids de los paneles, qué monitores hay, rutas de ejecutables, las apps
// de la bandeja. Un perfil que las arrastrara, al importarlo en otro equipo, pondría
// paneles en monitores que no existen. Por eso el perfil habla de `Panel` en singular y
// sin id: al aplicarlo se traduce al panel principal de ESTA máquina.
//
// ---------- Y por qué el "No molestar" va aparte ----------
//
// Porque no vive en config.json: vive en el centro de notificaciones, con su propio
// fichero. Es un `bool?` a propósito: `null` significa "este perfil no opina", que no es
// lo mismo que "este perfil lo apaga". Sin el tri-estado, cualquier perfil que no lo
// mencionara te apagaría el No molestar al aplicarlo.
public sealed class Perfil
{
    // El id no cambia nunca, ni al renombrar. Es lo que permite saber qué perfil está
    // puesto después de que le cambies el nombre, y lo que evita que dos perfiles
    // importados con el mismo nombre se pisen.
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";

    // Los cuatro de serie no se borran. Sí se pueden duplicar, renombrar y modificar: lo
    // que no se puede es dejar a alguien sin ninguno.
    public bool Protegido { get; set; }

    public int Version { get; set; } = Perfiles.VersionEsquema;

    // El trozo de config, con la misma forma que config.json: { "Tema": {...}, ... }
    public JsonObject Ajustes { get; set; } = new();

    // null = no opina
    public bool? NoMolestar { get; set; }

    public Perfil Copia(string nuevoId, string nuevoNombre) => new()
    {
        Id = nuevoId,
        Nombre = nuevoNombre,
        Descripcion = Descripcion,
        Protegido = false,            // una copia nunca hereda la protección
        Version = Version,
        Ajustes = Ajustes.DeepClone()?.AsObject() ?? new JsonObject(),
        NoMolestar = NoMolestar,
    };
}

public static class Perfiles
{
    // ---------- La versión del esquema de un PERFIL ----------
    //
    // Es la del fichero de perfil, no la del config. Sube cuando cambia la forma de un
    // perfil, y un perfil de una versión más NUEVA se rechaza al importar en vez de
    // leerse a medias: no se puede adivinar qué significa una clave que no existía.
    public const int VersionEsquema = 1;

    // ---------- Dónde viven los perfiles ----------
    //
    // Al lado del config al que pertenecen, y no en una ruta global. Parece un detalle y
    // no lo es: con la ruta global, aplicar un perfil sobre un config de prueba escribía
    // el "perfil activo" del usuario de verdad. Lo descubrió una prueba, que dejó un
    // estado.json en el AppData real.
    //
    // Un config lleva sus perfiles, su estado y su instantánea de deshacer. Así una
    // configuración de prueba es de verdad una configuración aparte.
    public static string CarpetaDe(string rutaConfig) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rutaConfig)) ?? ".", "perfiles");

    public static string Carpeta => CarpetaDe(Config.Ruta);
    public static string RutaDe(string id) => Path.Combine(Carpeta, id + ".json");
    public static string RutaDeEn(string rutaConfig, string id) => Path.Combine(CarpetaDe(rutaConfig), id + ".json");

    // Donde se guarda la copia del estado de antes de aplicar, para poder deshacer.
    public static string RutaDeshacerDe(string rutaConfig) =>
        Path.Combine(CarpetaDe(rutaConfig), "antes-de-aplicar.json");
    public static string RutaDeshacer => RutaDeshacerDe(Config.Ruta);

    public static string RutaEstadoDe(string rutaConfig) =>
        Path.Combine(CarpetaDe(rutaConfig), "estado.json");
    public static string RutaEstado => RutaEstadoDe(Config.Ruta);

    // ================= La lista blanca =================
    //
    // Lo que un perfil puede tocar. Está aquí, en una tabla y no repartida por el
    // código, porque es la frontera de seguridad de todo esto: lo que no esté en esta
    // lista no se escribe aunque venga en el fichero, y lo que esté y no venga no se
    // borra. Un `*` significa "toda la sección".
    //
    // Se corresponde una a una con lo que se pidió que un perfil guardara: tema, nivel
    // de animación, posición y modo de los paneles, widgets visibles/ocultos/ordenados,
    // notificaciones, fondo y las preferencias visuales de alrededor.
    public static readonly string[] Claves =
    {
        "Tema.*",                               // los tres colores, el radio, la fuente y los nueve roles
        "Animaciones.Nivel",                    // Completo | Equilibrado | Reducido | Desactivado | auto
        "Modulos.Izquierda",                    // widgets, en orden
        "Modulos.Centro",
        "Modulos.Derecha",
        "Modulos.Ocultos",                      // puestos pero no dibujados
        "Barra.Notificaciones",
        "Barra.NotificacionesSilenciadas",
        "Barra.OcultarEnPantallaCompleta",
        "Barra.OcultarConMaximizada",
        "Barra.PulsoAudio",
        "Barra.AppsDelEscritorio",
        "Fondo.*",                              // el gestor de fondos (Parte 3)
        "Panel.Modo",                           // se traduce al panel principal de esta máquina
        "Panel.Posicion",
        "Panel.Grosor",
        "Panel.Margen",
        "Panel.Visibilidad",
        "Panel.ReservarEspacio",

        // ---------- D5.1: las reglas y los atajos de NubeHypr ----------
        //
        // Un perfil puede traer un juego de reglas y de atajos: "Trabajo" con el editor
        // en el espacio 2 y el navegador flotante, "Juego" sin ninguna. Es exactamente lo
        // que pide la especificación de D5.
        //
        // Y lo que NO está aquí importa más que lo que está: **`NubeHypr.Activo` y
        // `NubeHypr.Modo` no son claves de perfil**. Un perfil cambia la configuración,
        // así que si pudiera encender la capa —o ponerla en "aplicar"— aplicar un perfil
        // te empezaría a mover ventanas sin haberlo pedido. Encender la capa es una
        // decisión aparte y se queda aparte.
        "NubeHypr.Reglas",
        "NubeHypr.Atajos",
    };

    // ¿Puede un perfil escribir esta clave? Pura y pública para poder comprobarla con
    // claves inventadas, incluidas las maliciosas de un fichero importado.
    // ---------- Las claves de `Tema` que ya no pintan nada ----------
    //
    // `Tema.*` deja entrar cualquier clave de la sección, y eso estaba bien cuando los colores
    // del tema eran estas trece. Desde el rediseño el color sale de `Tema.Id` y `Tema.Colores`
    // (ver `Catalogo`), y estas trece **no las lee nadie**.
    //
    // Si se dejaran pasar, un perfil del usuario con `Tema.Acento` se aplicaría «bien» y no
    // cambiaría ningún color: un perfil que miente. Así que se declaran retiradas y se
    // informan como ignoradas, que es el canal que ya existe para esto («válido, N clave(s)
    // ignoradas»). El perfil sigue aplicándose; lo que no hace es prometer.
    public static readonly string[] ClavesRetiradas =
    {
        "Tema.Fondo", "Tema.Texto", "Tema.Acento", "Tema.Radio", "Tema.Superficie",
        "Tema.SuperficieAlta", "Tema.Borde", "Tema.TextoAtenuado", "Tema.Secundario",
        "Tema.Sistema", "Tema.Positivo", "Tema.Aviso", "Tema.Negativo", "Tema.Estilo",
    };

    public static bool Retirada(string seccion, string clave) =>
        ClavesRetiradas.Any(c => c.Equals($"{seccion}.{clave}", StringComparison.OrdinalIgnoreCase));

    public static bool Permitida(string seccion, string clave)
    {
        if (string.IsNullOrWhiteSpace(seccion) || string.IsNullOrWhiteSpace(clave)) return false;
        if (Retirada(seccion, clave)) return false;
        foreach (var c in Claves)
        {
            int p = c.IndexOf('.');
            if (p <= 0) continue;
            var s = c[..p];
            var k = c[(p + 1)..];
            if (!s.Equals(seccion, StringComparison.OrdinalIgnoreCase)) continue;
            if (k == "*" || k.Equals(clave, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // ================= Validar =================
    //
    // Se valida ANTES de escribir nada, y entero. Un perfil con una clave mala no se
    // aplica "hasta donde llegue": no se aplica. Eso es lo que significa no dejar
    // configuraciones a medias.
    public sealed record Veredicto(bool Vale, List<string> Problemas, List<string> Ignoradas)
    {
        public static Veredicto Bien() => new(true, new(), new());
        public string Resumen => Vale
            ? (Ignoradas.Count == 0 ? "válido" : $"válido, {Ignoradas.Count} clave(s) ignoradas")
            : string.Join("; ", Problemas);
    }

    public static Veredicto Validar(Perfil? p)
    {
        var problemas = new List<string>();
        var ignoradas = new List<string>();

        if (p == null) return new Veredicto(false, new() { "el perfil no se pudo leer" }, ignoradas);
        if (string.IsNullOrWhiteSpace(p.Id)) problemas.Add("no tiene id");
        if (string.IsNullOrWhiteSpace(p.Nombre)) problemas.Add("no tiene nombre");
        if (p.Nombre.Length > 60) problemas.Add("el nombre pasa de 60 caracteres");
        if (p.Version <= 0) problemas.Add("la versión no es válida");
        if (p.Version > VersionEsquema)
            problemas.Add($"es de una versión más nueva ({p.Version} > {VersionEsquema}): " +
                          "actualiza NubeBar en vez de leerlo a medias");
        if (!IdValido(p.Id)) problemas.Add("el id tiene caracteres que no valen para un nombre de fichero");
        if (p.Ajustes == null) problemas.Add("no trae ajustes");

        if (p.Ajustes != null)
        {
            foreach (var (seccion, nodo) in p.Ajustes)
            {
                if (nodo is not JsonObject obj)
                {
                    problemas.Add($"la sección \"{seccion}\" no es un objeto");
                    continue;
                }
                foreach (var (clave, valor) in obj)
                {
                    if (!Permitida(seccion, clave)) { ignoradas.Add($"{seccion}.{clave}"); continue; }
                    var mal = ValorMal(seccion, clave, valor);
                    if (mal != null) problemas.Add($"{seccion}.{clave}: {mal}");
                }
            }
        }

        return new Veredicto(problemas.Count == 0, problemas, ignoradas);
    }

    public static bool IdValido(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 64 &&
        id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    // Los valores que tienen un conjunto cerrado se comprueban de verdad. Los demás solo
    // tienen que ser del tipo que toca: un perfil no es sitio para inventar validaciones
    // que el resto del programa no hace.
    static string? ValorMal(string seccion, string clave, JsonNode? valor)
    {
        if (valor == null) return null;            // null = "deriva este rol", es legítimo
        try
        {
            if (seccion.Equals("Panel", StringComparison.OrdinalIgnoreCase))
            {
                if (clave.Equals("Posicion", StringComparison.OrdinalIgnoreCase))
                    return PanelNube.PosicionValida(valor.GetValue<string>())
                        ? null : "no es arriba, abajo, izquierda ni derecha";
                if (clave.Equals("Visibilidad", StringComparison.OrdinalIgnoreCase))
                    return PanelNube.VisibilidadValida(valor.GetValue<string>())
                        ? null : "no es una visibilidad conocida";
                if (clave.Equals("Modo", StringComparison.OrdinalIgnoreCase))
                {
                    // Pregunta al modelo en vez de llevar la lista escrita aquí. Esta copia
                    // se habría quedado atrás el día de la notch, y el fallo es silencioso:
                    // un perfil con un modo bueno rechazado por un sitio que nadie mira.
                    var m = valor.GetValue<string>();
                    return PanelNube.ModoValido(m)
                        ? null : "no es ninguno de " + string.Join(", ", PanelNube.Modos);
                }
                if (clave is "Grosor" or "Margen")
                {
                    double d = valor.GetValue<double>();
                    return d >= 0 && d <= 400 ? null : "fuera de 0..400";
                }
            }

            if (seccion.Equals("Animaciones", StringComparison.OrdinalIgnoreCase) &&
                clave.Equals("Nivel", StringComparison.OrdinalIgnoreCase))
            {
                var n = (valor.GetValue<string>() ?? "").Trim().ToLowerInvariant();
                return n is "auto" or "completo" or "equilibrado" or "reducido" or "desactivado"
                    ? null : "no es un nivel de animación conocido";
            }

            if (seccion.Equals("Tema", StringComparison.OrdinalIgnoreCase))
            {
                if (clave is "TamanoFuente")
                {
                    double d = valor.GetValue<double>();
                    return d >= 0 && d <= 200 ? null : "fuera de 0..200";
                }

                // ---------- Los colores del tema, que ahora viven DENTRO de una clave ----------
                //
                // Antes eran trece claves de la sección y se validaba una a una. Ahora son un
                // diccionario (`Tema.Colores`), así que se valida su contenido: cada clave tiene
                // que estar en el catálogo y cada valor tiene que entenderse. Sin esto, un perfil
                // podría traer `{"no.existe": "azulito"}` y aplicarse «bien».
                //
                // Un null dentro vale: significa «quita esta personalización», o sea «que lo
                // ponga el tema». Es la misma idea que el null de antes («deriva este rol»).
                if (clave.Equals("Colores", StringComparison.OrdinalIgnoreCase))
                {
                    if (valor is not JsonObject co) return "Colores tiene que ser un objeto";
                    foreach (var (k, v) in co)
                    {
                        if (v == null) continue;
                        if (!Catalogo.Existe(k)) return $"\"{k}\" no es un color del catálogo";
                        var s = v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() ?? "" : "";
                        if (Relleno.Leer(s) is null) return $"\"{s}\" no es un color";
                    }
                    return null;
                }

                if (clave.Equals("Radios", StringComparison.OrdinalIgnoreCase))
                {
                    if (valor is not JsonObject ra) return "Radios tiene que ser un objeto";
                    foreach (var (k, v) in ra)
                    {
                        if (v == null) continue;
                        if (v.GetValueKind() != JsonValueKind.Number) return $"el radio de \"{k}\" no es un número";
                        double d = v.GetValue<double>();
                        if (d < 0 || d > 200) return $"el radio de \"{k}\" se sale de 0..200";
                    }
                    return null;
                }

                // El id de un tema que no existe NO es un error: cae en Profesional y queda
                // apuntado (`TemasDeSerie.De`). Rechazar el perfil entero por eso sería más duro
                // de lo que es el resto del programa con la misma situación.
            }

            if (seccion.Equals("Fondo", StringComparison.OrdinalIgnoreCase))
                return Fondos.ValorMal(clave, valor);
        }
        catch (Exception ex) { return "el tipo no es el que toca (" + ex.GetType().Name + ")"; }
        return null;
    }

    static bool EsNombreDeColor(string clave) => clave is
        "Fondo" or "Texto" or "Acento" or "Superficie" or "SuperficieAlta" or "Borde" or
        "TextoAtenuado" or "Secundario" or "Sistema" or "Positivo" or "Aviso" or "Negativo";

    // ================= Disco =================

    static readonly JsonSerializerOptions Formato = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static List<Perfil> Todos()
    {
        var lista = new List<Perfil>();
        try
        {
            Directory.CreateDirectory(Carpeta);
            foreach (var f in Directory.GetFiles(Carpeta, "*.json").OrderBy(x => x))
            {
                if (Path.GetFileName(f) == Path.GetFileName(RutaDeshacer)) continue;
                if (Leer(f) is { } p) lista.Add(p);
            }
        }
        catch (Exception ex) { Log.Error("listar los perfiles", ex); }
        return lista.OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static Perfil? Leer(string ruta)
    {
        try
        {
            var p = JsonSerializer.Deserialize<Perfil>(File.ReadAllText(ruta), Formato);
            if (p == null) return null;
            p.Ajustes ??= new JsonObject();
            // El id manda el nombre del fichero: así un perfil copiado a mano con otro
            // nombre no se queda con el id del original y pisándolo.
            var deFichero = Path.GetFileNameWithoutExtension(ruta);
            if (IdValido(deFichero)) p.Id = deFichero;
            return p;
        }
        catch (Exception ex) { Log.Error("leer el perfil " + Path.GetFileName(ruta), ex); return null; }
    }

    public static bool Guardar(Perfil p)
    {
        var v = Validar(p);
        if (!v.Vale) { Log.Error("guardar un perfil inválido: " + v.Resumen, new InvalidOperationException(v.Resumen)); return false; }
        try
        {
            Directory.CreateDirectory(Carpeta);
            ConfigArbol.EscribirAtomico(RutaDe(p.Id), JsonSerializer.Serialize(p, Formato));
            Log.Info($"perfil guardado: \"{p.Nombre}\" ({p.Id})");
            return true;
        }
        catch (Exception ex) { Log.Error("guardar el perfil " + p.Id, ex); return false; }
    }

    public static bool Borrar(Perfil p)
    {
        if (p.Protegido) { Log.Info($"el perfil \"{p.Nombre}\" está protegido: no se borra"); return false; }
        try
        {
            var r = RutaDe(p.Id);
            if (File.Exists(r)) File.Delete(r);
            Log.Info($"perfil borrado: \"{p.Nombre}\" ({p.Id})");
            return true;
        }
        catch (Exception ex) { Log.Error("borrar el perfil " + p.Id, ex); return false; }
    }

    // Un id libre a partir de un nombre. "Noche" -> "noche", y si ya está, "noche-2".
    //
    // Los acentos se pliegan: `char.IsLetterOrDigit('ó')` es true, así que "Concentración"
    // daba el id "concentración". Es un nombre de fichero legal en Windows, pero un id con
    // acentos viaja mal — un perfil exportado y reimportado en una máquina con otra página
    // de códigos deja de encontrar su propio fichero. El id es una llave, no un rótulo:
    // el nombre bonito ya lo lleva `Nombre`.
    public static string IdLibre(string nombre, IEnumerable<string> ocupados)
    {
        var plegado = SinAcentos((nombre ?? "").Trim().ToLowerInvariant());
        var limpio = new string(plegado
            .Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-').ToArray()).Trim('-');
        while (limpio.Contains("--")) limpio = limpio.Replace("--", "-");
        if (limpio.Length == 0) limpio = "perfil";
        if (limpio.Length > 48) limpio = limpio[..48];
        var usados = new HashSet<string>(ocupados, StringComparer.OrdinalIgnoreCase);
        if (!usados.Contains(limpio)) return limpio;
        for (int i = 2; i < 1000; i++)
            if (!usados.Contains($"{limpio}-{i}")) return $"{limpio}-{i}";
        return limpio + "-" + Guid.NewGuid().ToString("N")[..6];
    }

    // "Concentración" -> "concentracion". Se descompone en letra + tilde y se tiran las
    // tildes, que es lo que hace cualquier generador de slugs y no hace falta inventar.
    static string SinAcentos(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    // Un nombre libre, para duplicar: "Noche" -> "Noche (copia)" -> "Noche (copia 2)".
    public static string NombreLibre(string nombre, IEnumerable<string> ocupados)
    {
        var usados = new HashSet<string>(ocupados, StringComparer.CurrentCultureIgnoreCase);
        var basico = $"{nombre} (copia)";
        if (!usados.Contains(basico)) return basico;
        for (int i = 2; i < 1000; i++)
            if (!usados.Contains($"{nombre} (copia {i})")) return $"{nombre} (copia {i})";
        return basico;
    }

    // ================= Exportar e importar =================

    public static bool Exportar(Perfil p, string ruta)
    {
        try
        {
            ConfigArbol.EscribirAtomico(ruta, JsonSerializer.Serialize(p, Formato));
            Log.Info($"perfil \"{p.Nombre}\" exportado a {ruta}");
            return true;
        }
        catch (Exception ex) { Log.Error("exportar el perfil " + p.Id, ex); return false; }
    }

    // Importar NO es "copiar el fichero": es leerlo, validarlo entero, darle un id y un
    // nombre que no choquen con los de aquí, y guardarlo. Si algo falla no se escribe
    // nada, así que el estado anterior sigue intacto por construcción.
    public sealed record Importado(Perfil? Perfil, Veredicto Veredicto);

    public static Importado Importar(string ruta, IEnumerable<Perfil> yaHay)
    {
        var p = Leer(ruta);
        if (p == null)
            return new Importado(null, new Veredicto(false, new() { "el fichero no es un perfil legible" }, new()));

        // El id del fichero importado no vale aquí: puede chocar con uno nuestro.
        var lista = yaHay.ToList();
        p.Protegido = false;                  // nada importado llega protegido
        p.Nombre = string.IsNullOrWhiteSpace(p.Nombre) ? "Importado" : p.Nombre.Trim();
        if (lista.Any(x => x.Nombre.Equals(p.Nombre, StringComparison.CurrentCultureIgnoreCase)))
            p.Nombre = NombreLibre(p.Nombre, lista.Select(x => x.Nombre));
        p.Id = IdLibre(p.Nombre, lista.Select(x => x.Id));

        var v = Validar(p);
        if (!v.Vale) return new Importado(null, v);

        // Las claves que no están en la lista blanca se tiran AQUÍ, no al aplicar: así lo
        // que queda guardado es exactamente lo que se va a poder aplicar.
        Podar(p);
        return new Importado(p, v);
    }

    // Quita del perfil lo que no puede tocar. Devuelve cuántas quitó.
    public static int Podar(Perfil p)
    {
        int fuera = 0;
        try
        {
            foreach (var seccion in p.Ajustes.Select(x => x.Key).ToList())
            {
                if (p.Ajustes[seccion] is not JsonObject obj) { p.Ajustes.Remove(seccion); fuera++; continue; }
                foreach (var clave in obj.Select(x => x.Key).ToList())
                    if (!Permitida(seccion, clave)) { obj.Remove(clave); fuera++; }
                if (obj.Count == 0) p.Ajustes.Remove(seccion);
            }
        }
        catch (Exception ex) { Log.Error("podar un perfil", ex); }
        return fuera;
    }

    // ================= Traducir un perfil a claves de config =================
    //
    // Un perfil habla de `Panel` en singular y sin id; esta máquina tiene paneles con id.
    // Aquí se traduce, y es lo que hace que un perfil sea portable entre equipos.
    public const string PanelPrincipal = "principal";

    // Qué escrituras hace falta hacer para aplicar este perfil. Devolverlas como DATOS en
    // vez de escribirlas directamente es lo que permite comprobar la traducción sin tocar
    // ningún config, y lo que permite validarlas todas antes de que la primera toque el
    // disco.
    public sealed record Escritura(string Seccion, string Clave, JsonNode? Valor, bool EsDelPanel);

    public static List<Escritura> Escrituras(Perfil p)
    {
        var lista = new List<Escritura>();
        if (p?.Ajustes == null) return lista;
        foreach (var (seccion, nodo) in p.Ajustes)
        {
            if (nodo is not JsonObject obj) continue;
            foreach (var (clave, valor) in obj)
            {
                if (!Permitida(seccion, clave)) continue;
                bool delPanel = seccion.Equals("Panel", StringComparison.OrdinalIgnoreCase);
                lista.Add(new Escritura(seccion, clave, valor?.DeepClone(), delPanel));
            }
        }
        return lista;
    }

    // ================= El estado: qué perfil está puesto =================
    //
    // En su propio fichero y no en config.json, por dos razones. Una: config.json es lo
    // que el perfil MODIFICA, así que guardar ahí cuál está puesto mezcla el contenido con
    // la etiqueta. Y dos, más práctica: escribir en config.json dispara el remonte de la
    // interfaz, y marcar el activo no tiene por qué repintar nada.
    public sealed class EstadoPerfiles
    {
        public int Version { get; set; } = VersionEsquema;
        public string? Activo { get; set; }
        // La huella de los valores tal y como quedaron al aplicar. Sirve para saber si el
        // usuario ha tocado algo a mano desde entonces.
        public string? HuellaAlAplicar { get; set; }
    }


    public static EstadoPerfiles LeerEstado(string? rutaConfig = null)
    {
        try
        {
            var r = RutaEstadoDe(rutaConfig ?? Config.Ruta);
            if (!File.Exists(r)) return new EstadoPerfiles();
            return JsonSerializer.Deserialize<EstadoPerfiles>(File.ReadAllText(r), Formato)
                   ?? new EstadoPerfiles();
        }
        catch (Exception ex) { Log.Error("leer el estado de los perfiles", ex); return new EstadoPerfiles(); }
    }

    public static void GuardarEstado(EstadoPerfiles e, string? rutaConfig = null)
    {
        try
        {
            var r = RutaEstadoDe(rutaConfig ?? Config.Ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(r)!);
            ConfigArbol.EscribirAtomico(r, JsonSerializer.Serialize(e, Formato));
        }
        catch (Exception ex) { Log.Error("guardar el estado de los perfiles", ex); }
    }

    // ================= La huella: saber si lo han tocado a mano =================
    //
    // "Detección de configuración personalizada". Se lee el valor ACTUAL de cada clave que
    // el perfil toca y se arma una huella. Si no coincide con la de cuando se aplicó, el
    // usuario ha cambiado algo desde entonces y la tarjeta lo dice: "Noche (modificado)".
    //
    // Es una función de los valores, no del fichero entero: cambiar algo que el perfil no
    // toca —un atajo, una app fijada— NO cuenta como haber modificado el perfil.
    public static string Huella(Perfil p, ConfigArbol arbol)
    {
        var partes = new List<string>();
        foreach (var e in Escrituras(p).OrderBy(x => x.Seccion + "." + x.Clave, StringComparer.Ordinal))
        {
            string actual = e.EsDelPanel
                ? LeerDelPanelComoTexto(arbol, e.Clave)
                : LeerComoTexto(arbol, e.Seccion, e.Clave);
            partes.Add($"{e.Seccion}.{e.Clave}={actual}");
        }
        return string.Join(";", partes);
    }

    // ---------- Leer un valor como texto, para la huella ----------
    //
    // Sin centinela de "no está". La primera versión usaba uno y resultó ser un byte NUL
    // literal metido en el fuente: compilaba y funcionaba, pero un NUL dentro de un .cs es
    // una mina — los editores y los diffs lo tratan como binario, y es justo la clase de
    // carácter invisible que ya se perdió una vez con el glifo de la cuadrícula en C4.1.
    //
    // No hace falta saber de qué tipo es el valor: para una huella basta que sea ESTABLE y
    // que cambie cuando el valor cambie. Se componen los tres tipos posibles y listo.
    static string LeerComoTexto(ConfigArbol a, string seccion, string clave) =>
        a.Texto(seccion, clave, "") + "|" +
        a.Numero(seccion, clave, 0).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "|" +
        a.Bandera(seccion, clave, false);

    static string LeerDelPanelComoTexto(ConfigArbol a, string clave) =>
        a.TextoDelPanel(PanelPrincipal, clave, "") + "|" +
        a.NumeroDelPanel(PanelPrincipal, clave, 0).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "|" +
        a.BanderaDelPanel(PanelPrincipal, clave, false);

    // ¿Está el perfil activo tal y como se aplicó, o lo han tocado?
    public static bool Modificado(string? huellaGuardada, string huellaAhora) =>
        huellaGuardada != null && huellaGuardada != huellaAhora;

    // ================= Aplicar, como una transacción =================

    public sealed record Aplicacion(bool Ok, Perfil? Perfil, int Escritas, string Detalle)
    {
        public static Aplicacion Mal(string porque) => new(false, null, 0, porque);
    }

    // El orden es el que se pidió, y ninguno de los pasos se puede adelantar:
    //
    //  1. INSTANTÁNEA del estado actual, a un fichero aparte.
    //  2. VALIDAR el perfil entero. Si algo no vale, no se escribe nada.
    //  3. Aplicar todos los valores **en memoria**, sobre el árbol.
    //  4. Validar el árbol resultante y GUARDAR UNA SOLA VEZ.
    //  5. El remonte de la interfaz lo hace el vigilante del config, que ya existe y ya
    //     agrupa los cambios con 250 ms de poso: una escritura, un remonte.
    //  6. Si algo falla por el camino, se restaura la instantánea.
    //
    // Lo que NO se hace: escribir clave a clave. Eso dejaría la interfaz remontando a
    // mitad de la aplicación y, si fallara la séptima clave, un escritorio mitad de un
    // perfil y mitad del otro.
    public static Aplicacion Aplicar(Perfil perfil, string? rutaConfig = null)
    {
        rutaConfig ??= Config.Ruta;

        // ---------- 2. Validar antes de nada ----------
        var v = Validar(perfil);
        if (!v.Vale)
        {
            Log.Error($"perfil \"{perfil?.Nombre}\": no se aplica, no es válido", new InvalidOperationException(v.Resumen));
            return Aplicacion.Mal("el perfil no es válido: " + v.Resumen);
        }

        // ---------- 1. La instantánea ----------
        if (!GuardarInstantanea(rutaConfig))
            return Aplicacion.Mal("no se pudo guardar la instantánea de antes; no se aplica nada");

        try
        {
            // ---------- 3. Todo en memoria ----------
            var arbol = ConfigArbol.Cargar(rutaConfig);
            var escrituras = Escrituras(perfil);
            foreach (var e in escrituras)
            {
                if (e.EsDelPanel) arbol.PonerDelPanel(PanelPrincipal, e.Clave, e.Valor);
                else PonerNodo(arbol, e.Seccion, e.Clave, e.Valor);
            }

            // ---------- 4. Validar el resultado y guardar UNA vez ----------
            var texto = arbol.Serializar();
            if (JsonNode.Parse(texto) is not JsonObject resultado)
                throw new InvalidOperationException("el config resultante no es un objeto JSON");
            if (Esquema.Mal(resultado, 0) is { } mal)
                throw new InvalidOperationException("el config resultante no pasa la validación: " + mal);

            arbol.Guardar();

            // ---------- El No molestar, que no vive en config.json ----------
            if (perfil.NoMolestar is { } nm)
            {
                try { Centro.Actual.NoMolestar = nm; Centro.Actual.Guardar(); }
                catch (Exception ex) { Log.Error("aplicar el No molestar del perfil", ex); }
            }

            // ---------- Y marcar el activo ----------
            GuardarEstado(new EstadoPerfiles
            {
                Activo = perfil.Id,
                HuellaAlAplicar = Huella(perfil, ConfigArbol.Cargar(rutaConfig)),
            }, rutaConfig);

            Log.Info($"perfil \"{perfil.Nombre}\" aplicado: {escrituras.Count} claves, una sola escritura" +
                     (perfil.NoMolestar is { } x ? $", No molestar={x}" : ""));
            return new Aplicacion(true, perfil, escrituras.Count,
                                  $"{escrituras.Count} claves aplicadas");
        }
        catch (Exception ex)
        {
            Log.Error($"aplicar el perfil \"{perfil.Nombre}\": se deshace", ex);
            bool vuelto = Deshacer(rutaConfig);
            return Aplicacion.Mal($"falló al aplicar ({ex.Message}); " +
                                  (vuelto ? "se ha restaurado lo de antes" : "NO se pudo restaurar"));
        }
    }

    static void PonerNodo(ConfigArbol a, string seccion, string clave, JsonNode? valor)
    {
        // `null` significa "quita la clave", que para los roles de color es "derívalo".
        if (valor == null) { a.Quitar(seccion, clave); return; }
        switch (valor.GetValueKind())
        {
            case JsonValueKind.String: a.Poner(seccion, clave, valor.GetValue<string>()); break;
            case JsonValueKind.Number: a.Poner(seccion, clave, valor.GetValue<double>()); break;
            case JsonValueKind.True:
            case JsonValueKind.False: a.Poner(seccion, clave, valor.GetValue<bool>()); break;
            case JsonValueKind.Array:
                a.PonerLista(seccion, clave,
                    valor.AsArray().Select(n => n?.GetValue<string>() ?? "").Where(s => s.Length > 0));
                break;
            default:
                // Un objeto dentro de una sección plana no lo sabemos escribir sin
                // inventar. Se deja la clave como estaba.
                Log.Info($"perfil: {seccion}.{clave} es un {valor.GetValueKind()} y no se sabe escribir; se ignora");
                break;
        }
    }

    // ================= Deshacer =================

    public static bool HayQueDeshacer => File.Exists(RutaDeshacer);
    public static bool HayQueDeshacerEn(string rutaConfig) => File.Exists(RutaDeshacerDe(rutaConfig));

    static bool GuardarInstantanea(string rutaConfig)
    {
        try
        {
            Directory.CreateDirectory(CarpetaDe(rutaConfig));
            if (!File.Exists(rutaConfig))
            {
                // Sin config no hay nada que fotografiar, pero hay que poder deshacer
                // igual: se deja una marca vacía para que Deshacer sepa qué hacer.
                File.WriteAllText(RutaDeshacerDe(rutaConfig), "");
                return true;
            }
            File.Copy(rutaConfig, RutaDeshacerDe(rutaConfig), true);
            return true;
        }
        catch (Exception ex) { Log.Error("guardar la instantánea de antes de aplicar", ex); return false; }
    }

    // Vuelve al estado de antes de la última aplicación. Una escritura atómica: igual que
    // aplicar, deshacer no puede dejar la configuración a medias.
    public static bool Deshacer(string? rutaConfig = null)
    {
        rutaConfig ??= Config.Ruta;
        try
        {
            if (!File.Exists(RutaDeshacerDe(rutaConfig)))
            {
                Log.Info("deshacer: no hay instantánea que restaurar");
                return false;
            }
            var texto = File.ReadAllText(RutaDeshacerDe(rutaConfig));
            if (texto.Length == 0)
            {
                Log.Info("deshacer: la instantánea está vacía (no había config antes)");
                return false;
            }
            if (JsonNode.Parse(texto) is not JsonObject)
            {
                Log.Error("deshacer: la instantánea no es JSON válido; no se restaura",
                          new InvalidOperationException("instantánea ilegible"));
                return false;
            }

            ConfigArbol.EscribirAtomico(rutaConfig, texto, rutaConfig + ".bak");
            // El perfil deja de estar activo: lo que hay ahora es lo de antes.
            var e = LeerEstado(rutaConfig);
            Log.Info($"deshecho: el config vuelve a como estaba antes de aplicar \"{e.Activo}\"");
            GuardarEstado(new EstadoPerfiles { Activo = null, HuellaAlAplicar = null }, rutaConfig);
            try { File.Delete(RutaDeshacerDe(rutaConfig)); } catch { }
            return true;
        }
        catch (Exception ex) { Log.Error("deshacer la aplicación del perfil", ex); return false; }
    }

    // ================= Los cuatro de serie =================
    //
    // Lo que guarda cada uno es DELIBERADAMENTE poco: solo lo que define ese modo de
    // trabajar. Un perfil que lo fije todo es un perfil que, al aplicarlo, te quita cosas
    // que no tenían nada que ver con lo que pediste.
    public static List<Perfil> DeSerie() => new()
    {
        new Perfil
        {
            Id = "concentracion",
            Nombre = "Concentración",
            Descripcion = "Lo justo a la vista y nada que se mueva. Sin notificaciones y con " +
                          "No molestar puesto: la barra deja de pedirte atención.",
            Protegido = true,
            NoMolestar = true,
            Ajustes = new JsonObject
            {
                ["Animaciones"] = new JsonObject { ["Nivel"] = "reducido" },
                ["Barra"] = new JsonObject
                {
                    ["Notificaciones"] = false,
                    ["PulsoAudio"] = false,
                    ["OcultarEnPantallaCompleta"] = true,
                    ["OcultarConMaximizada"] = true,
                },
                ["Modulos"] = new JsonObject
                {
                    ["Izquierda"] = new JsonArray("inicio"),
                    ["Centro"] = new JsonArray("apps", "reloj"),
                    ["Derecha"] = new JsonArray("control", "fecha"),
                },
            },
        },
        new Perfil
        {
            Id = "creativo",
            Nombre = "Creativo",
            Descripcion = "Todo a mano y todo vivo: el pulso del audio, las gráficas y la " +
                          "animación completa. Para cuando la máquina es la herramienta.",
            Protegido = true,
            NoMolestar = false,
            Ajustes = new JsonObject
            {
                ["Animaciones"] = new JsonObject { ["Nivel"] = "completo" },
                ["Barra"] = new JsonObject
                {
                    ["Notificaciones"] = true,
                    ["PulsoAudio"] = true,
                    ["OcultarConMaximizada"] = false,
                },
                ["Modulos"] = new JsonObject
                {
                    ["Izquierda"] = new JsonArray("inicio"),
                    ["Centro"] = new JsonArray("fijadas", "apps", "reloj"),
                    ["Derecha"] = new JsonArray("cpu", "ram", "bateria", "bandeja", "control", "fecha"),
                },
            },
        },
        new Perfil
        {
            Id = "noche",
            Nombre = "Noche",
            Descripcion = "Oscuro y tranquilo. Menos movimiento, sin pulso de audio y el " +
                          "No molestar puesto para que nada te despierte la pantalla.",
            Protegido = true,
            NoMolestar = true,
            Ajustes = new JsonObject
            {
                ["Animaciones"] = new JsonObject { ["Nivel"] = "equilibrado" },
                ["Barra"] = new JsonObject { ["PulsoAudio"] = false, ["Notificaciones"] = true },
                ["Fondo"] = new JsonObject
                {
                    ["Tipo"] = "degradado",
                    ["Color1"] = "#0B0D14",
                    ["Color2"] = "#1A1B26",
                    ["Angulo"] = 160.0,
                },
            },
        },
        new Perfil
        {
            Id = "minimal",
            Nombre = "Minimal",
            Descripcion = "La nube y la hora. Nada más: ni gráficas, ni bandeja, ni fijadas. " +
                          "Sin animación, para que aparezca y desaparezca sin ceremonia.",
            Protegido = true,
            Ajustes = new JsonObject
            {
                ["Animaciones"] = new JsonObject { ["Nivel"] = "desactivado" },
                ["Barra"] = new JsonObject { ["PulsoAudio"] = false },
                ["Modulos"] = new JsonObject
                {
                    ["Izquierda"] = new JsonArray("inicio"),
                    ["Centro"] = new JsonArray("reloj"),
                    ["Derecha"] = new JsonArray("control"),
                    ["Ocultos"] = new JsonArray(),
                },
                ["Panel"] = new JsonObject { ["Modo"] = "isla", ["Posicion"] = "arriba" },
            },
        },
    };

    // Deja los cuatro en disco si no están. No sobrescribe los que ya existan: si los has
    // modificado, son tuyos.
    public static int CrearLosDeSerie()
    {
        int creados = 0;
        try
        {
            Directory.CreateDirectory(Carpeta);
            foreach (var p in DeSerie())
            {
                if (File.Exists(RutaDe(p.Id))) continue;
                if (Guardar(p)) creados++;
            }
            if (creados > 0) Log.Info($"perfiles de serie: {creados} creados");
        }
        catch (Exception ex) { Log.Error("crear los perfiles de serie", ex); }
        return creados;
    }

    // Devolver un perfil de serie a sus valores de fábrica. Solo vale para los de serie:
    // un perfil tuyo no tiene "valores de fábrica" a los que volver.
    public static Perfil? Restaurar(string id)
    {
        var deSerie = DeSerie().FirstOrDefault(x => x.Id == id);
        if (deSerie == null)
        {
            Log.Info($"restaurar: \"{id}\" no es un perfil de serie, no hay valores de fábrica");
            return null;
        }
        return Guardar(deSerie) ? deSerie : null;
    }
}
