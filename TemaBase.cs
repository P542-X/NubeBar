using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;

namespace NubeBar;

// Un tema, como PAQUETE DE VALORES.
//
// ---------- La regla que esto hace cumplir ----------
//
// «Un tema es un paquete de valores, no código aparte» (`docs/REDISENO-VISUAL.md`,
// decisión A). Todos los temas pintan las mismas piezas con los mismos componentes; lo que
// cambia son sus colores, sus medidas, su letra y qué decoraciones lleva encendidas. Por eso
// aquí no hay ni un `switch` por tema: un tema nuevo es un fichero, no una rama.
//
// Es lo contrario de lo que había. `EstiloVisual` era un `enum` de tres casos y cada caso
// traía sus números escritos en C#: añadir un cuarto estilo era recompilar, y el usuario no
// podía inventarse ninguno. Los nueve temas de serie salen de `temas-de-serie.json`, y los
// del usuario del mismo formato — así «duplicar un tema de serie» es copiar su fichero.
//
// ---------- Qué declara un tema y qué NO ----------
//
// Declara los 19 colores base, la letra, los radios, el tipo de esquina y la lista de
// decoraciones. NO declara los ~120 colores de cada elemento de la interfaz: esos los pone
// `Catalogo`, derivándolos de estos 19 (y el tema puede mandar sobre cualquiera si quiere,
// porque `Color()` busca por clave sin preguntar si es base o no).
//
// Así un tema puede traer cuatro colores y que todo salga coherente, que es justo lo que pide
// la cascada de la decisión B.

public enum Esquinas { Redondas, Chaflan }

// Las claves de los 19 colores base. Como constantes y no como cadenas sueltas porque una
// errata en `"filo-isla"` no da error de compilación: da un tema sin filo y un rato perdido.
public static class ClavesBase
{
    public const string Acento = "acento";
    public const string AcentoTinta = "acento-tinta";       // el acento cuando hace de texto
    public const string SobreAcento = "sobre-acento";       // lo que se escribe ENCIMA del acento
    public const string Sec1 = "sec1";
    public const string Sec2 = "sec2";
    public const string Tinta = "tinta";
    public const string Tenue = "tenue";
    public const string Superficie = "superficie";          // el fondo de la isla
    public const string Tarjeta = "tarjeta";
    public const string PanelFondo = "panel-fondo";
    public const string Filo = "filo";
    public const string FiloIsla = "filo-isla";
    public const string FiloPanel = "filo-panel";
    public const string Sombra = "sombra";
    public const string Halo = "halo";
    public const string Destello = "destello";
    public const string Ok = "ok";
    public const string Peligro = "peligro";
    public const string FondoEscritorio = "fondo-escritorio";

    public static readonly string[] Todas =
    {
        Acento, AcentoTinta, SobreAcento, Sec1, Sec2, Tinta, Tenue, Superficie, Tarjeta,
        PanelFondo, Filo, FiloIsla, FiloPanel, Sombra, Halo, Destello, Ok, Peligro,
        FondoEscritorio,
    };
}

// Las piezas que tienen radio propio.
public static class Piezas
{
    public const string Isla = "isla";
    public const string Panel = "panel";
    public const string Tarjeta = "tarjeta";
    public const string Boton = "boton";
}

public sealed class TemaBase
{
    public string Id { get; init; } = "profesional";
    public string Nombre { get; init; } = "Profesional";
    public string Familia { get; init; } = "Sobrios";
    public bool Claro { get; init; }
    public string Fuente { get; init; } = "Segoe UI Variable";
    public string FuenteTitulos { get; init; } = "Segoe UI Variable";
    public Esquinas Esquinas { get; init; } = Esquinas.Redondas;

    // Lo que en `temas.json` viene detrás del «+» del fondo de escritorio: «rejilla en
    // perspectiva verde», «ciudad con letreros de neón»… Es la descripción de una decoración
    // que se construye en la fase 7; aquí se conserva tal cual para no perderla, y nadie la
    // interpreta todavía.
    public string? FondoExtra { get; init; }

    public IReadOnlyDictionary<string, Relleno> Colores { get; init; } =
        new Dictionary<string, Relleno>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, double> Radios { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    // «halo», «destello-filo», «modo-rgb», «particulas:petalo», «mascota:hachimaki»,
    // «marca-de-agua:侍»… El nombre y, tras los dos puntos, su argumento.
    public IReadOnlyList<string> Decoraciones { get; init; } = Array.Empty<string>();

    // Los textos que un tema cambia: el saludo de Sad acaba en puntos suspensivos y su
    // buscador dice «Busca algo… lo que sea…». Plantillas, no frases hechas (ver el
    // apartado 7 del Estudio).
    public IReadOnlyDictionary<string, string> Textos { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Lo que el tema DECLARA para esa clave, o null si calla. Null significa «derívalo»,
    // nunca «transparente»: un tema que quiera apagar algo escribe `transparent`, que sí es
    // un valor (es lo que hacen Oficina con el halo y el destello).
    // Se llama «Declarado» y no «Color» porque es exactamente eso —lo que el tema dice, no lo
    // que acaba pintándose, que lo decide la cascada— y porque un miembro llamado `Color`
    // dentro de esta clase tapa a `System.Windows.Media.Color` y obliga a cualificar el tipo
    // en cada línea.
    public Relleno? Declarado(string clave) =>
        Colores.TryGetValue(clave, out var r) ? r : null;

    public double Radio(string pieza, double siNoLoDice) =>
        Radios.TryGetValue(pieza, out var v) ? v : siNoLoDice;

    public bool Lleva(string decoracion) =>
        Decoraciones.Any(d => d.Equals(decoracion, StringComparison.OrdinalIgnoreCase) ||
                              d.StartsWith(decoracion + ":", StringComparison.OrdinalIgnoreCase));

    // El argumento de una decoración: `Argumento("particulas")` sobre Sakura da «petalo».
    // Null si el tema no la lleva; cadena vacía si la lleva sin argumento.
    public string? Argumento(string decoracion)
    {
        foreach (var d in Decoraciones)
        {
            if (d.Equals(decoracion, StringComparison.OrdinalIgnoreCase)) return "";
            if (d.StartsWith(decoracion + ":", StringComparison.OrdinalIgnoreCase))
                return d[(decoracion.Length + 1)..];
        }
        return null;
    }

    public string? Texto(string clave) => Textos.TryGetValue(clave, out var t) ? t : null;

    public override string ToString() => $"{Nombre} ({Id})";

    // ================= El puente con los temas de antes del rediseño =================
    //
    // ---------- Por qué existe y cuándo se va ----------
    //
    // Los temas de antes (`Temas.Predefinidos`: Nube, Claro, Tokyo Night, Gruvbox…) son tres
    // colores más diez roles opcionales dentro de `Tema`, y los deriva `Roles` con sus propias
    // cuentas. Los nuevos son paquetes de 19 valores que resuelve el catálogo.
    //
    // Mientras los dos convivan hay que elegir: o dos cascadas en paralelo, o una sola y un
    // puente. Dos cascadas es la respuesta equivocada —es exactamente el «no hay colores
    // escritos a mano» incumplido desde el primer día, con dos sitios donde arreglar cada
    // cosa— así que: una sola cascada, y los temas viejos entran por aquí.
    //
    // ---------- Y por qué repite las derivaciones de antes ----------
    //
    // A propósito, y es lo único feo de este fichero. Las derivaciones del catálogo son las de
    // la MAQUETA (la tarjeta al 12 %, el elegido al 18 %); las de antes eran otras (0x1E,
    // 0x33). Si un tema viejo entrara sin sus valores, los ocho temas de siempre cambiarían de
    // aspecto el día que se enchufó el catálogo — y eso no es «migrar al rediseño», es
    // estropearle el tema a quien no ha pedido nada.
    //
    // Así que el puente DECLARA lo que el tema viejo callaba, con la cuenta que se le aplicaba
    // antes. El tema viejo deja de tener derivaciones propias: pasa a ser un paquete de
    // valores, como los nuevos. Cuando los ocho temas de antes se retiren (fase 0, último
    // paso), este método se va con ellos.
    public static TemaBase DeTemaViejo(Tema t, Color fondo, Color texto, Color acento)
    {
        var fondoOpaco = Color.FromRgb(fondo.R, fondo.G, fondo.B);
        var colores = new Dictionary<string, Relleno>(StringComparer.OrdinalIgnoreCase);

        void Poner(string clave, Color c) => colores[clave] = Relleno.Liso(c);
        Color Velo(Color sobre, byte alfa) => Color.FromArgb(alfa, sobre.R, sobre.G, sobre.B);
        Color? Leido(string? hex) => Relleno.LeerColor(hex);

        // Los tres de siempre, ya resueltos por quien llama (con el wallpaper aplicado y el
        // acento corregido): no se vuelven a leer del config.
        Poner(ClavesBase.Superficie, fondo);
        Poner(ClavesBase.Tinta, texto);
        Poner(ClavesBase.Acento, acento);

        // Y los diez roles opcionales, con la derivación de antes cuando el tema calla. Los
        // alfas son los que tenía escritos `Roles` (0x1E para una tarjeta, 0x33 para la de
        // debajo del ratón, 0x3A para un filo), que a su vez eran los que el código usaba a
        // mano antes de que los roles existieran.
        Poner(ClavesBase.Tarjeta, Leido(t.Superficie) ?? Velo(texto, 0x1E));
        Poner("tarjeta.fondo.raton", Leido(t.SuperficieAlta) ?? Velo(texto, 0x33));
        Poner(ClavesBase.Filo, Leido(t.Borde) ?? Velo(texto, 0x3A));
        Poner(ClavesBase.Tenue, Leido(t.TextoAtenuado) ?? Contraste.Mezclar(texto, fondoOpaco, 0.35));

        // `Secundario` y `Sistema`, si el tema no los da, son el propio acento: un tema de
        // tres colores NO se inventa un naranja que su autor no eligió. Era la regla de
        // `Roles` y sigue siendo la correcta.
        Poner(ClavesBase.Sec1, Leido(t.Secundario) ?? acento);
        Poner(ClavesBase.Sec2, Leido(t.Sistema) ?? acento);

        // Los semánticos sí tienen color propio aunque el tema calle: un «batería baja» en
        // rojo tiene que seguir siendo rojo en Gruvbox.
        Poner(ClavesBase.Ok, Leido(t.Positivo) ?? Color.FromRgb(0x2E, 0xA0, 0x72));
        Poner("aviso", Leido(t.Aviso) ?? Color.FromRgb(0xE8, 0x9C, 0x3C));
        Poner(ClavesBase.Peligro, Leido(t.Negativo) ?? Color.FromRgb(0xDA, 0x44, 0x53));

        // El panel se pintaba con el mismo fondo de la isla, y el filo de la isla y el del
        // panel eran el mismo filo. Los temas nuevos los separan; los viejos no los tenían.
        Poner(ClavesBase.PanelFondo, fondo);
        colores[ClavesBase.FiloIsla] = colores[ClavesBase.Filo];
        colores[ClavesBase.FiloPanel] = colores[ClavesBase.Filo];

        return new TemaBase
        {
            Id = "(tema anterior)",
            Nombre = "Personalizado",
            Familia = "Mis temas",
            // Claro u oscuro se MIDE, no se declara: los temas de antes no lo decían, y de
            // esto dependen las derivaciones que el puente no cubre (las ~110 claves de la
            // interfaz, que sí salen del catálogo).
            Claro = Contraste.Luminancia(fondoOpaco) >= 0.5,
            Fuente = string.IsNullOrWhiteSpace(t.Fuente) ? "Segoe UI" : t.Fuente,
            FuenteTitulos = string.IsNullOrWhiteSpace(t.Fuente) ? "Segoe UI" : t.Fuente,
            Colores = colores,
            Radios = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [Piezas.Isla] = t.Radio,
                [Piezas.Panel] = t.Radio,
            },
        };
    }
}

// ================= Los nueve de serie =================
//
// Salen de `temas-de-serie.json`, que es copia literal del objeto `temas` de
// `docs/maqueta-rediseno/temas.json` (la maqueta aprobada). Va como recurso incrustado y no
// como fichero al lado del exe: el exe es de un solo archivo y autocontenido, y un tema de
// serie que se pueda borrar por accidente no es de serie.
public static class TemasDeSerie
{
    public const string Predeterminado = "profesional";

    static Dictionary<string, TemaBase>? _todos;
    static readonly object _cerrojo = new();

    public static IReadOnlyDictionary<string, TemaBase> Todos
    {
        get
        {
            if (_todos != null) return _todos;
            lock (_cerrojo) return _todos ??= Cargar();
        }
    }

    // En el orden del fichero, que es el del selector de Ajustes: los sobrios primero.
    public static IReadOnlyList<TemaBase> Lista => Todos.Values.ToList();

    // Nunca devuelve null. Un id que no se conoce cae en Profesional, no en el más
    // llamativo: misma regla que cumplía `Estilos.Leer` y que cumple `NubeHypr.ModoEfectivo`.
    // Un config con una errata no puede dejarte el escritorio lleno de neón.
    public static TemaBase De(string? id)
    {
        var clave = (id ?? "").Trim().ToLowerInvariant();
        if (clave.Length > 0 && Todos.TryGetValue(clave, out var t)) return t;
        if (clave.Length > 0)
            Log.Info($"el tema \"{id}\" no existe: se usa {Predeterminado}");
        return Todos.TryGetValue(Predeterminado, out var p) ? p : new TemaBase();
    }

    public static bool Existe(string? id) =>
        !string.IsNullOrWhiteSpace(id) && Todos.ContainsKey(id.Trim().ToLowerInvariant());

    static Dictionary<string, TemaBase> Cargar()
    {
        var fuera = new Dictionary<string, TemaBase>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var s = Recurso() ?? throw new FileNotFoundException("temas-de-serie.json no está en el ensamblado");
            using var doc = JsonDocument.Parse(s);
            if (!doc.RootElement.TryGetProperty("temas", out var temas))
                throw new InvalidDataException("temas-de-serie.json no trae el objeto \"temas\"");

            foreach (var t in temas.EnumerateObject())
                if (Leer(t.Name, t.Value) is { } tema) fuera[tema.Id] = tema;

            Log.Info($"temas de serie: {fuera.Count} ({string.Join(", ", fuera.Keys)})");
        }
        catch (Exception ex)
        {
            // Sin temas de serie no hay con qué pintar, así que se deja uno mínimo y se
            // apunta. Es un fallo de compilación disfrazado —el recurso no viaja— y la
            // prueba `TemasDeSerieCargan` lo coge antes de publicar.
            Log.Error("cargar los temas de serie", ex);
        }
        if (fuera.Count == 0) fuera[Predeterminado] = new TemaBase();
        return fuera;
    }

    static Stream? Recurso()
    {
        var asm = Assembly.GetExecutingAssembly();
        var nombre = asm.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("temas-de-serie.json", StringComparison.OrdinalIgnoreCase));
        return nombre is null ? null : asm.GetManifestResourceStream(nombre);
    }

    // ---------- De JSON a TemaBase ----------
    //
    // Tolerante a propósito: lo que no se entiende se ignora y se apunta, y el tema sigue
    // cargando con lo demás. Es la misma regla que tendrá que cumplir un `.nubetema` de la
    // comunidad (apartado 10 del Estudio), así que el camino es el mismo desde ya.
    internal static TemaBase? Leer(string id, JsonElement j)
    {
        try
        {
            var colores = new Dictionary<string, Relleno>(StringComparer.OrdinalIgnoreCase);
            string? fondoExtra = null;

            if (j.TryGetProperty("colores", out var jc) && jc.ValueKind == JsonValueKind.Object)
                foreach (var c in jc.EnumerateObject())
                {
                    var crudo = c.Value.GetString();
                    if (Relleno.Leer(crudo) is { } r) colores[c.Name] = r;
                    else Log.Info($"tema {id}: el color \"{c.Name}\" = \"{crudo}\" no se entiende; se derivará");

                    // El «+ rejilla en perspectiva verde» del fondo de escritorio.
                    if (c.Name.Equals(ClavesBase.FondoEscritorio, StringComparison.OrdinalIgnoreCase) &&
                        crudo is not null)
                    {
                        int mas = crudo.IndexOf(") +", StringComparison.Ordinal);
                        if (mas > 0) fondoExtra = crudo[(mas + 3)..].Trim();
                    }
                }

            var radios = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (j.TryGetProperty("radios", out var jr) && jr.ValueKind == JsonValueKind.Object)
                foreach (var r in jr.EnumerateObject())
                    if (r.Value.TryGetDouble(out var v)) radios[r.Name] = v;

            var decoraciones = new List<string>();
            if (j.TryGetProperty("decoraciones", out var jd) && jd.ValueKind == JsonValueKind.Array)
                foreach (var d in jd.EnumerateArray())
                    if (d.GetString() is { Length: > 0 } s) decoraciones.Add(s);

            var textos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (j.TryGetProperty("textos", out var jt) && jt.ValueKind == JsonValueKind.Object)
                foreach (var t in jt.EnumerateObject())
                    if (t.Value.GetString() is { } s) textos[t.Name] = s;

            return new TemaBase
            {
                Id = id.Trim().ToLowerInvariant(),
                Nombre = Cadena(j, "nombre") ?? id,
                Familia = Cadena(j, "familia") ?? "Mis temas",
                Claro = j.TryGetProperty("claro", out var jcl) && jcl.ValueKind == JsonValueKind.True,
                Fuente = Cadena(j, "fuente") ?? "Segoe UI Variable",
                FuenteTitulos = Cadena(j, "fuente-titulos") ?? Cadena(j, "fuente") ?? "Segoe UI Variable",
                Esquinas = (Cadena(j, "esquinas") ?? "").Trim().ToLowerInvariant() is "chaflan" or "chaflán"
                    ? Esquinas.Chaflan : Esquinas.Redondas,
                FondoExtra = fondoExtra,
                Colores = colores,
                Radios = radios,
                Decoraciones = decoraciones,
                Textos = textos,
            };
        }
        catch (Exception ex) { Log.Error($"leer el tema {id}", ex); return null; }
    }

    static string? Cadena(JsonElement j, string clave) =>
        j.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
