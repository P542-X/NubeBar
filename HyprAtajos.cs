using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D3: los atajos, con nombres y no con acciones.
//
// ---------- Por qué el mapa guarda un NOMBRE y no un comando ----------
//
// Un atajo no apunta a "llama a SetForegroundWindow con este hwnd": apunta a
// `foco.siguiente`. Tres razones, y ninguna es estética:
//
//  1. El fichero de configuración se puede escribir y leer: `"Win+Alt+Right":
//     "foco.derecha"` se entiende sin saber qué es un hwnd.
//  2. Lo que hace un comando puede cambiar sin romper la configuración de nadie.
//  3. Se puede detectar un nombre que no existe y DECIRLO, en vez de dejar un atajo
//     muerto que el usuario pulsa sin entender por qué no pasa nada.
//
// El parseo de la combinación lo hace `Nativo.TraducirAtajo`, que ya existe y ya registra
// el atajo de la vista general. Escribir aquí un segundo parseador sería tener dos
// gramáticas para lo mismo y que una se quedara atrás.
public static class HyprAtajos
{
    // ---------- Los nombres ----------
    //
    // Todo lo que un atajo puede pedir. Si un nombre no está en esta lista, no existe, y
    // el mapa lo rechaza al cargarse en vez de en mitad de una pulsación.
    public const string FocoSiguiente = "foco.siguiente";
    public const string FocoAnterior = "foco.anterior";
    public const string FocoIzquierda = "foco.izquierda";
    public const string FocoDerecha = "foco.derecha";
    public const string FocoArriba = "foco.arriba";
    public const string FocoAbajo = "foco.abajo";
    public const string MoverAdelante = "mover.adelante";
    public const string MoverAtras = "mover.atras";
    public const string MasAncho = "tamano.mas";
    public const string MasEstrecho = "tamano.menos";
    public const string Flotante = "ventana.flotante";
    public const string LayoutPrincipal = "layout.principal";
    public const string LayoutHorizontal = "layout.horizontal";
    public const string LayoutVertical = "layout.vertical";
    public const string LayoutFlotante = "layout.flotante";
    public const string EspacioSiguiente = "espacio.siguiente";
    public const string EspacioAnterior = "espacio.anterior";
    public const string LlevarAlSiguiente = "espacio.llevar.siguiente";
    public const string LlevarAlAnterior = "espacio.llevar.anterior";

    // D9.1: el vigésimo. Ver `Comando.Deshacer` para por qué se rompió el "son 19".
    public const string DeshacerUltimo = "deshacer.ultimo";

    public static readonly string[] Nombres =
    {
        FocoSiguiente, FocoAnterior, FocoIzquierda, FocoDerecha, FocoArriba, FocoAbajo,
        MoverAdelante, MoverAtras, MasAncho, MasEstrecho, Flotante,
        LayoutPrincipal, LayoutHorizontal, LayoutVertical, LayoutFlotante,
        EspacioSiguiente, EspacioAnterior, LlevarAlSiguiente, LlevarAlAnterior,
        DeshacerUltimo,
    };

    public static bool EsNombre(string? n) =>
        n != null && Nombres.Contains(n.Trim(), StringComparer.OrdinalIgnoreCase);

    // ---------- El paso de un comando de tamaño ----------
    //
    // Un 5 % por pulsación. Con menos no se nota y hay que machacar la tecla; con más, dos
    // pulsaciones te llevan del 60 % al tope y se pierde el control fino que es justo lo
    // que se busca.
    public const double PasoDeTamano = 0.05;

    // ---------- De nombre a comando ----------
    //
    // Devuelve null para un nombre que no existe, en vez de un comando inventado: un
    // atajo mal escrito no puede acabar haciendo algo distinto de lo que dice.
    public static Comando? Comando(string? nombre) => (nombre ?? "").Trim().ToLowerInvariant() switch
    {
        FocoSiguiente => new NubeBar.Comando.EnfocarSiguiente(),
        FocoAnterior => new NubeBar.Comando.EnfocarAnterior(),
        FocoIzquierda => new NubeBar.Comando.EnfocarHacia(Direccion.Izquierda),
        FocoDerecha => new NubeBar.Comando.EnfocarHacia(Direccion.Derecha),
        FocoArriba => new NubeBar.Comando.EnfocarHacia(Direccion.Arriba),
        FocoAbajo => new NubeBar.Comando.EnfocarHacia(Direccion.Abajo),
        MoverAdelante => new NubeBar.Comando.MoverEnElOrden(true),
        MoverAtras => new NubeBar.Comando.MoverEnElOrden(false),
        MasAncho => new NubeBar.Comando.Redimensionar(PasoDeTamano),
        MasEstrecho => new NubeBar.Comando.Redimensionar(-PasoDeTamano),
        Flotante => new NubeBar.Comando.AlternarFlotante(),
        LayoutPrincipal => new NubeBar.Comando.PonerLayout(NubeHypr.Layout.Principal),
        LayoutHorizontal => new NubeBar.Comando.PonerLayout(NubeHypr.Layout.Horizontal),
        LayoutVertical => new NubeBar.Comando.PonerLayout(NubeHypr.Layout.Vertical),
        LayoutFlotante => new NubeBar.Comando.PonerLayout(NubeHypr.Layout.Flotante),
        EspacioSiguiente => new NubeBar.Comando.AlVecino(true, false),
        EspacioAnterior => new NubeBar.Comando.AlVecino(false, false),
        LlevarAlSiguiente => new NubeBar.Comando.AlVecino(true, true),
        LlevarAlAnterior => new NubeBar.Comando.AlVecino(false, true),
        DeshacerUltimo => new NubeBar.Comando.Deshacer(),
        _ => null,
    };

    // Lo que se le enseña al usuario. Corto a propósito: va en el indicador breve, que no
    // puede tapar la ventana activa para explicarse.
    public static string Rotulo(string nombre) => (nombre ?? "").Trim().ToLowerInvariant() switch
    {
        FocoSiguiente => "Siguiente",
        FocoAnterior => "Anterior",
        FocoIzquierda => "Foco ←",
        FocoDerecha => "Foco →",
        FocoArriba => "Foco ↑",
        FocoAbajo => "Foco ↓",
        MoverAdelante => "Mover adelante",
        MoverAtras => "Mover atrás",
        MasAncho => "Más ancho",
        MasEstrecho => "Más estrecho",
        Flotante => "Flotante",
        LayoutPrincipal => "Principal",
        LayoutHorizontal => "Columnas",
        LayoutVertical => "Filas",
        LayoutFlotante => "Libre",
        EspacioSiguiente => "Espacio →",
        EspacioAnterior => "Espacio ←",
        LlevarAlSiguiente => "Llevar →",
        LlevarAlAnterior => "Llevar ←",
        DeshacerUltimo => "Deshacer",
        _ => nombre ?? "",
    };

    // ================= La combinación =================
    //
    // ---------- Por qué se normaliza ----------
    //
    // "ctrl+alt+w", "Alt+Ctrl+W" y "CTRL + ALT + W" son el mismo atajo, y si el mapa las
    // guarda como tres claves distintas, el usuario se encuentra tres atajos donde quería
    // uno y ninguno de los tres detectado como conflicto. Se ordena por un orden fijo
    // —Win, Ctrl, Alt, Shift— y se escribe la tecla al final.
    public static string Normalizar(string? combinacion)
    {
        if (string.IsNullOrWhiteSpace(combinacion)) return "";
        var trozos = combinacion.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).ToList();
        if (trozos.Count == 0) return "";

        bool ctrl = trozos.Any(t => t is "ctrl" or "control");
        bool alt = trozos.Contains("alt");
        bool shift = trozos.Contains("shift");
        bool win = trozos.Contains("win");
        var tecla = trozos.LastOrDefault(t => t is not ("ctrl" or "control" or "alt" or "shift" or "win"));
        if (tecla == null) return "";        // solo modificadores no es un atajo

        // Win primero, que es como se escriben los atajos de Windows y como se leen en
        // voz alta: "Win+Alt+Derecha". El orden es fijo, cualquiera sirve, pero tiene que
        // coincidir con el que espera el que lea el fichero.
        var partes = new List<string>();
        if (win) partes.Add("Win");
        if (ctrl) partes.Add("Ctrl");
        if (alt) partes.Add("Alt");
        if (shift) partes.Add("Shift");
        partes.Add(tecla.ToUpperInvariant());
        return string.Join("+", partes);
    }

    // ¿Windows sabría registrarlo? Se le pregunta al que ya lo hace, no a una tabla
    // paralela. Y se exige al menos un modificador: un atajo global de una sola tecla te
    // deja sin esa tecla en todo el sistema.
    public static bool Valida(string? combinacion)
    {
        var n = Normalizar(combinacion);
        if (n.Length == 0 || !n.Contains('+')) return false;
        return Nativo.TraducirAtajo(n, out uint mod, out uint vk) && mod != 0 && vk != 0;
    }

    // ================= El mapa =================
    //
    // De combinación normalizada a nombre de comando.
    public sealed record Mapa(IReadOnlyDictionary<string, string> Atajos,
                              IReadOnlyList<string> Problemas)
    {
        public string? Nombre(string combinacion) =>
            Atajos.TryGetValue(Normalizar(combinacion), out var n) ? n : null;

        public Comando? Resolver(string combinacion) => HyprAtajos.Comando(Nombre(combinacion) ?? "");
        public bool Bien => Problemas.Count == 0;
    }

    // ---------- El prefijo, en UN sitio ----------
    //
    // Hasta aquí las diecinueve combinaciones estaban escritas enteras, con `Win+Alt`
    // repetido veinte veces. Cambiar de prefijo era reescribir las veinte sin equivocarse en
    // ninguna — y por eso no se había cambiado nunca, aunque la medición de D3 decía que
    // había que hacerlo: con `Win+Alt`, **9 de los 19 los tenía cogidos otra aplicación** en
    // este equipo. El Game Bar de Xbox y Teams se quedan con varios `Win+Alt`.
    //
    // Ahora el prefijo es una constante y las teclas una tabla. Cambiarlo es una edición. Y
    // el de serie sale de lo que mide `--diagprefijos`, no de una preferencia: registrar de
    // verdad cada candidato y contar cuántos entran. Lo medido en este equipo (Windows 11,
    // con Game Bar, Teams, Steam y Wallpaper Engine instalados):
    //
    //     Win+Ctrl+Alt      19/19   <- el elegido
    //     Ctrl+Alt+Shift    18/19   (1 ocupado)
    //     Ctrl+Alt          17/19   (2 ocupados)
    //     Win+Alt           10/19   (9 ocupados)  <- el de antes
    //
    // `Win+Ctrl+Alt` tiene además la ventaja de leerse como lo que es: el prefijo "de
    // escritorios" de Windows (`Win+Ctrl+flechas` cambia de escritorio) con un modificador
    // más. Lo que no quita que haya que pulsarlo a mano para saber si es cómodo.
    public const string Prefijo = "Win+Ctrl+Alt";
    public const string PrefijoSegundo = "Win+Ctrl+Alt+Shift";

    // Los candidatos que se miden. En el orden en que se considerarían: primero los que
    // menos se cruzan con lo que Windows y las aplicaciones de siempre se reservan.
    public static readonly string[] Candidatos =
    {
        "Ctrl+Alt+Shift",
        "Win+Ctrl+Alt",
        "Win+Alt",
        "Win+Ctrl+Shift",
        "Ctrl+Alt",
    };

    // ---------- Qué tecla hace qué ----------
    //
    // La tabla de verdad: la tecla sin prefijo, y si va en el segundo nivel. Separar el
    // "qué" del "con qué" es lo que permite cambiar el prefijo sin tocar un solo nombre de
    // comando, y lo que permite comprobar que los 19 están cubiertos sin leer veinte
    // cadenas a mano.
    public sealed record Tecla(string Pulsacion, string Comando, bool SegundoNivel = false);

    public static readonly Tecla[] Teclas =
    {
        new("Right", FocoDerecha),
        new("Left", FocoIzquierda),
        new("Up", FocoArriba),
        new("Down", FocoAbajo),
        new("Tab", FocoSiguiente),
        new("J", MoverAtras),
        new("K", MoverAdelante),
        new("Oemplus", MasAncho),
        new("Oemminus", MasEstrecho),
        new("F", Flotante),
        new("P", LayoutPrincipal),
        new("H", LayoutHorizontal),
        new("V", LayoutVertical),
        new("L", LayoutFlotante),

        // Los cinco que faltaban para cubrir los 19 comandos, en el segundo nivel. Los de
        // espacios ahí porque es como los agrupa Windows: un modificador más significa "y
        // llévate la ventana".
        new("Tab", FocoAnterior, SegundoNivel: true),
        new("Right", EspacioSiguiente, SegundoNivel: true),
        new("Left", EspacioAnterior, SegundoNivel: true),
        new("K", LlevarAlSiguiente, SegundoNivel: true),
        new("J", LlevarAlAnterior, SegundoNivel: true),

        // D9.1: deshacer. En el primer nivel y con Z, que es donde la gente busca un
        // deshacer sin pensarlo. Si alguna vez deja de estar libre, se cambia aquí.
        new("Z", DeshacerUltimo),
    };

    // El segundo nivel de un prefijo: el prefijo más **el primer modificador que no lleve
    // ya**. Si repitiera uno, el segundo nivel sería la misma combinación con otro nombre:
    // las cinco claves del segundo nivel colapsarían sobre las del primero y cinco comandos
    // se perderían sin que nada avisara.
    //
    // Y no es teoría: la primera versión de esto encadenaba condiciones a mano y con
    // `Win+Ctrl+Shift` devolvía `Win+Ctrl+Shift+Ctrl`. `--diagprefijos` lo destapó al
    // enseñar **14 combinaciones en vez de 19** para ese candidato. El orden es el de
    // `Normalizar` (Win, Ctrl, Alt, Shift) para que la combinación resultante se escriba
    // como se escribe todo lo demás.
    static readonly string[] Modificadores = { "Win", "Ctrl", "Alt", "Shift" };

    public static string SegundoDe(string prefijo)
    {
        // Qué modificadores lleva ya, comparando TROZO a trozo. No con `Contains` sobre la
        // cadena entera: "Alt" está dentro de "Win+Ctrl+Alt" y también dentro de nada más,
        // pero el día que haya un modificador cuyo nombre sea prefijo de otro, un
        // `Contains` de cadena acierta por casualidad.
        var trozos = (prefijo ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim()).ToList();
        bool Lleva(string m) => trozos.Any(t => t.Equals(m, StringComparison.OrdinalIgnoreCase));

        var falta = Modificadores.FirstOrDefault(m => !Lleva(m));

        // Los cuatro puestos: no hay segundo nivel posible. No se inventa uno repetido —se
        // devuelve el mismo, y `ConPrefijo` se queda con 14 comandos, que es la verdad.
        if (falta == null) return string.Join("+", Modificadores.Where(Lleva));

        // Y se escribe en el orden de siempre (Win, Ctrl, Alt, Shift), construyéndolo en
        // vez de recomponer la cadena del prefijo: la primera versión de esto hacía
        // `Normalizar(prefijo + "+" + m + "+A").Replace("+A", "")` y el `Replace` se comía
        // también el "+A" de "+Alt" — `Win+Ctrl+Alt+Shift` salía como `Win+Ctrllt+Shift`.
        // Lo pilló la prueba de este mismo grupo en la primera ejecución.
        return string.Join("+", Modificadores.Where(m => Lleva(m) || m == falta));
    }

    // El mapa con un prefijo cualquiera. `DeSerie()` es este con el de serie, y
    // `--diagprefijos` lo usa con cada candidato para medirlos todos con la misma vara.
    public static Dictionary<string, string> ConPrefijo(string prefijo, string? segundo = null)
    {
        segundo ??= SegundoDe(prefijo);

        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Teclas)
            mapa[Normalizar($"{(t.SegundoNivel ? segundo : prefijo)}+{t.Pulsacion}")] = t.Comando;
        return mapa;
    }

    public static Dictionary<string, string> DeSerie() => ConPrefijo(Prefijo, PrefijoSegundo);

    // ---------- Construir y validar ----------
    //
    // Los problemas se DEVUELVEN, no se lanzan ni se tragan: un atajo mal escrito no
    // puede tumbar el arranque, y tampoco puede quedarse muerto en silencio. El que lo
    // reciba lo apunta en el log y lo enseña en Ajustes.
    //
    // `reservados` son los atajos que NubeBar ya tiene cogidos —el de la vista general— y
    // los del sistema que no se deben pisar. Un conflicto con esos no se resuelve por
    // orden de llegada: se rechaza el nuevo y se dice contra qué chocaba.
    public static Mapa Construir(IReadOnlyDictionary<string, string>? crudo,
                                 IEnumerable<string>? reservados = null)
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var problemas = new List<string>();
        var tomados = new HashSet<string>(
            (reservados ?? Array.Empty<string>()).Select(Normalizar).Where(s => s.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (combinacion, nombre) in crudo ?? new Dictionary<string, string>())
        {
            var n = Normalizar(combinacion);
            if (n.Length == 0) { problemas.Add($"«{combinacion}» no es una combinación"); continue; }
            if (!Valida(n)) { problemas.Add($"«{combinacion}»: Windows no la entiende o le falta un modificador"); continue; }
            if (!EsNombre(nombre)) { problemas.Add($"«{combinacion}» -> «{nombre}»: ese comando no existe"); continue; }
            if (tomados.Contains(n)) { problemas.Add($"«{n}» ya está cogido: no se asigna a «{nombre}»"); continue; }
            if (mapa.TryGetValue(n, out var ya))
            {
                problemas.Add($"«{n}» está dos veces: «{ya}» y «{nombre}». Se queda el primero");
                continue;
            }
            mapa[n] = nombre.Trim().ToLowerInvariant();
        }
        return new Mapa(mapa, problemas);
    }
}
