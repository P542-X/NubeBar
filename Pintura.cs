using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace NubeBar;

// Quien contesta «¿de qué color pinto esto?».
//
// ---------- La cascada de la decisión B ----------
//
// Para cada clave del catálogo, por orden:
//
//   1. lo que haya puesto el usuario (`Tema.Colores` del config: solo lo que ha cambiado);
//   2. lo que diga el tema (los 9 de serie declaran 19 claves; uno del usuario, las que quiera);
//   3. la derivación escrita en `Catalogo`.
//
// Y nada más. No hay un cuarto escalón «y si no, este hexadecimal que escribí aquí»: una
// clave que no sepa derivarse es un hueco en el catálogo, y la prueba `CatalogoEntero` lo
// caza antes de publicar.
//
// ---------- Por qué es un objeto y no funciones estáticas ----------
//
// Porque hay DOS pinturas vivas a la vez. La normal, con el tema del usuario, y la de la
// ventana de Ajustes, que puede ser otra: «si el texto de Ajustes o del Estudio no llega a
// 4,5:1 sobre su fondo con el tema puesto, esas dos ventanas se pintan con Profesional». Con
// estáticas eso no se puede tener, y el Estudio además necesita una tercera —la del tema que
// se está editando— para su vista previa en vivo.
//
// ---------- El caché ----------
//
// Una pintura es INMUTABLE: se construye al aplicar el tema y se tira cuando el tema cambia.
// Por eso puede cachear sin invalidar nada. Importa: `boton.texto.desactivado` encadena cuatro
// derivaciones con dos cuentas de contraste, y se pide una vez por botón.
//
// Se usa desde el hilo de la interfaz. El caché no está protegido a propósito: ponerle un
// cerrojo costaría en cada pintada para defenderse de algo que no pasa.
public sealed class Pintura
{
    public TemaBase Tema { get; }

    // Solo lo que el usuario ha cambiado, clave → el texto tal cual está en el config. Se
    // guarda en crudo y se interpreta al pedirlo: así un color mal escrito a mano en el
    // config.json no impide construir la pintura, solo se ignora esa clave.
    public IReadOnlyDictionary<string, string> DelUsuario { get; }

    readonly Dictionary<string, Relleno> _cache = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _enCurso = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Brush> _pinceles = new(StringComparer.OrdinalIgnoreCase);

    public Pintura(TemaBase tema, IReadOnlyDictionary<string, string>? delUsuario = null)
    {
        Tema = tema;
        DelUsuario = delUsuario is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(delUsuario, StringComparer.OrdinalIgnoreCase);
    }

    public static Pintura PorDefecto() => new(TemasDeSerie.De(TemasDeSerie.Predeterminado));

    // ================= Preguntar =================

    // El relleno de una clave. Nunca lanza y nunca devuelve null: una clave desconocida da
    // transparente y queda apuntada. Un widget de tercero que pregunte por una clave que se
    // inventó no puede dejar la barra sin pintar.
    public Relleno R(string clave)
    {
        if (_cache.TryGetValue(clave, out var hecho)) return hecho;

        var r = Resolver(clave);
        _cache[clave] = r;
        return r;
    }

    public Color Color(string clave) => R(clave).Color;

    // El pincel, cacheado y congelado. Dos tarjetas con el mismo fondo comparten el objeto.
    public Brush Pincel(string clave)
    {
        if (_pinceles.TryGetValue(clave, out var p)) return p;
        return _pinceles[clave] = R(clave).Pincel();
    }

    // ---------- Lo que se VE de verdad ----------
    //
    // Un color con alfa no tiene luminancia propia: `#1EFFFFFF` no es claro ni oscuro hasta
    // que se sabe qué hay detrás. Esto lo compone sobre su fondo —el de la clave `Sobre` del
    // catálogo, recursivamente, hasta llegar a la superficie de la isla— y devuelve el color
    // opaco resultante. Es el único con el que se puede medir contraste.
    public Color ColorOpaco(string clave) => Opaco(clave, 0);

    Color Opaco(string clave, int hondo)
    {
        var c = Color(clave);
        if (c.A == 0xFF || hondo > 8) return System.Windows.Media.Color.FromRgb(c.R, c.G, c.B);

        var detras = Detras(clave, hondo);
        return Contraste.Mezclar(detras, System.Windows.Media.Color.FromRgb(c.R, c.G, c.B), c.A / 255.0);
    }

    Color Detras(string clave, int hondo)
    {
        if (Catalogo.Por(clave)?.Sobre is { } sobre &&
            !sobre.Equals(clave, StringComparison.OrdinalIgnoreCase))
            return Opaco(sobre, hondo + 1);

        // El suelo de la pila: la superficie de la isla sin su transparencia. Es lo que había
        // antes (`Roles.FondoOpaco`) y sigue siendo la respuesta correcta para una clave que
        // no dice sobre qué se pinta.
        var s = Color(ClavesBase.Superficie);
        return System.Windows.Media.Color.FromRgb(s.R, s.G, s.B);
    }

    public double Radio(string pieza) => Tema.Radio(pieza, pieza switch
    {
        Piezas.Isla => 20,
        Piezas.Panel => 18,
        Piezas.Tarjeta => 12,
        _ => 10,
    });

    public bool Personalizado(string clave) => DelUsuario.ContainsKey(clave);

    // Cuántas veces se ha cortado una derivación circular. Debe quedarse en 0 con el catálogo
    // de serie, y la prueba `CatalogoSinCiclos` lo exige: un ciclo no rompe nada visible —se
    // corta y se pinta la tinta— así que sin contador se colaría sin que nadie se enterase.
    public int Ciclos { get; private set; }

    // ================= La cascada =================

    Relleno Resolver(string clave)
    {
        var ficha = Catalogo.Por(clave);

        // ---------- 1. El usuario ----------
        if (DelUsuario.TryGetValue(clave, out var mio))
        {
            if (Relleno.Leer(mio) is { } r) return r;
            Log.Info($"el color \"{clave}\" = \"{mio}\" del config no se entiende: se usa el del tema");
        }

        // ---------- 2. El tema ----------
        if (Tema.Color(clave) is { } delTema) return delTema;

        // ---------- 3. La derivación ----------
        if (ficha is null)
        {
            Log.Info($"color \"{clave}\": no está en el catálogo; se pinta transparente");
            return Relleno.Nada;
        }

        // Una derivación que, dando vueltas, vuelve a pedir su propia clave. No puede pasar
        // con el catálogo de serie —la prueba `CatalogoSinCiclos` lo comprueba— pero sí con un
        // `.nubetema` importado que declare claves raras, y colgar la barra en un bucle
        // infinito es peor que pintar algo flojo.
        if (!_enCurso.Add(clave))
        {
            Ciclos++;
            Log.Info($"color \"{clave}\": la derivación da vueltas sobre sí misma; se corta");
            return Relleno.Liso(Color(ClavesBase.Tinta));
        }

        try { return ficha.Derivar(this); }
        catch (Exception ex)
        {
            Log.Error($"derivar el color \"{clave}\"", ex);
            return Relleno.Nada;
        }
        finally { _enCurso.Remove(clave); }
    }

    // ================= Contraste =================

    // El contraste real de una clave de texto o de icono sobre su fondo, o 0 si a esa clave no
    // se le mide nada. Lo usan el aviso del selector de color (apartado 2 del Estudio) y las
    // pruebas de los nueve temas.
    public double ContrasteDe(string clave)
    {
        var ficha = Catalogo.Por(clave);
        if (ficha is null || !ficha.SeMide) return 0;
        return Contraste.Entre(ColorOpaco(clave), ColorOpaco(ficha.Sobre!));
    }

    public bool Legible(string clave)
    {
        var ficha = Catalogo.Por(clave);
        if (ficha is null || !ficha.SeMide) return true;
        return ContrasteDe(clave) >= ficha.MinimoContraste;
    }

    // Las claves que NO se leen con este tema. La lista, no un booleano: el aviso de arriba de
    // Ajustes tiene que poder decir cuál, y la prueba tiene que poder decir cuáles fallan.
    public IReadOnlyList<ClaveColor> Ilegibles() =>
        Catalogo.Todas.Where(c => c.SeMide && !Legible(c.Clave)).ToList();

    // ---------- La regla de seguridad ----------
    //
    // «Un tema nunca puede dejar Ajustes ilegible». El usuario puede cambiarlo todo, así que
    // puede hacer un tema en el que no se lea nada — y si lo que no se lee es Ajustes, se ha
    // quedado sin la ventana desde la que arreglarlo.
    //
    // Por eso Ajustes y el Estudio no preguntan a la pintura del tema: preguntan a esta. Si
    // las claves de la zona Ajustes se leen, es la misma; si no, es Profesional, y la ventana
    // avisa arriba de por qué (`PorQueProfesional`).
    public bool AjustesSeLee => !Catalogo.Todas
        .Any(c => c.Zona == ZonaColor.Ajustes && c.SeMide && !Legible(c.Clave));

    public Pintura ParaAjustes()
    {
        if (AjustesSeLee) return this;

        var malas = Catalogo.Todas
            .Where(c => c.Zona == ZonaColor.Ajustes && c.SeMide && !Legible(c.Clave))
            .Select(c => c.Clave);
        Log.Info($"el tema \"{Tema.Id}\" deja Ajustes ilegible ({string.Join(", ", malas)}): " +
                 $"esa ventana se pinta con {TemasDeSerie.Predeterminado}");

        // Sin las personalizaciones del usuario: si lo que rompe Ajustes es un color que ha
        // puesto él, arrastrarlo aquí rompería también el repuesto.
        return new Pintura(TemasDeSerie.De(TemasDeSerie.Predeterminado));
    }

    public override string ToString() =>
        $"Pintura({Tema.Id}, {DelUsuario.Count} personalizados)";
}
