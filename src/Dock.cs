using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E2: el dock.
//
// ---------- Qué decide este archivo ----------
//
// Qué elementos lleva el dock, en qué orden, dónde van los separadores, cuál está activo y
// **en qué rectángulo de la pantalla se coloca**. Todo puro: recibe las ventanas abiertas y
// las fijadas, y devuelve una lista y unas coordenadas. No abre ventanas ni llama a Win32.
//
// Que la colocación sea pura es lo que permite comprobar «barra arriba de 38 px, monitor de
// 2560×1440, ocho elementos: ¿dónde cae el dock?» sin tener dos monitores delante.
//
// ---------- Las dos decisiones de diseño ----------
//
// 1. **Las fijadas primero, las demás después, con un separador en medio.** Es el orden de
//    cualquier dock: lo que tú has elegido que esté siempre, y detrás lo que hay abierto
//    ahora. Sin el separador, una app abierta se confunde con una anclada y el dock cambia de
//    forma cada vez que abres algo.
//
// 2. **Una app fijada que además está abierta NO sale dos veces.** Sale una, en su sitio de
//    fijada, con su indicador de abierta. Dos iconos del mismo programa en el mismo dock es
//    el fallo clásico de las barras de tareas y confunde a cualquiera.
public static class Dock
{
    // ---------- Un elemento ----------
    //
    // `Clave` es con lo que se compara: el nombre del programa, no la ruta. Dos ventanas del
    // mismo navegador son un elemento con dos ventanas, no dos elementos.
    public sealed record Elemento(
        string Clave,
        string Nombre,
        bool Fijada,
        int Ventanas = 0,
        bool Activa = false,
        bool EsSeparador = false)
    {
        public bool Abierta => Ventanas > 0;

        // Lo que se le dice a un lector de pantalla. El dock es iconos: sin esto, quien use
        // un lector oye «botón» nueve veces.
        public string ParaLeer => EsSeparador
            ? "separador"
            : $"{Nombre}" +
              (Fijada ? ", anclada" : "") +
              (Ventanas switch { 0 => ", no abierta", 1 => ", 1 ventana", var n => $", {n} ventanas" }) +
              (Activa ? ", es la de delante" : "");

        public override string ToString() =>
            EsSeparador ? "|" :
            $"{Clave}{(Fijada ? "*" : "")}{(Ventanas > 0 ? $"({Ventanas})" : "")}{(Activa ? "<" : "")}";
    }

    public static Elemento Separador => new("", "", false, EsSeparador: true);

    // ================= Montar la lista =================
    //
    // `fijadas` llega en el orden que el usuario les dio en Ajustes, y se respeta: es su
    // orden, no uno alfabético nuestro.
    public static List<Elemento> Montar(IEnumerable<string> fijadas,
                                        IEnumerable<VentanaReal> ventanas,
                                        int tope = 24)
    {
        // Se filtra DESPUÉS de normalizar, no antes. Filtrar antes dejaba pasar `"  "`
        // —tiene longitud 2— y `Clave` lo convertía en una clave vacía: un elemento sin
        // nombre y sin icono en el dock. Lo cazó la prueba de entradas raras.
        var fijas = (fijadas ?? Array.Empty<string>())
            .Select(Clave)
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Las ventanas, agrupadas por programa. Una app con cinco ventanas es un elemento.
        var abiertas = (ventanas ?? Array.Empty<VentanaReal>())
            .Where(v => v != null)
            .GroupBy(v => Clave(v.App.Length > 0 ? v.App : v.Exe), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key,
                          g => (Cuantas: g.Count(), Activa: g.Any(v => v.Activa),
                                Nombre: g.First().App.Length > 0 ? g.First().App : g.Key),
                          StringComparer.OrdinalIgnoreCase);

        var lista = new List<Elemento>();

        // ---------- 1. Las fijadas, en su orden ----------
        foreach (var f in fijas)
        {
            abiertas.TryGetValue(f, out var datos);
            lista.Add(new Elemento(f, datos.Nombre is { Length: > 0 } ? datos.Nombre : f,
                                   Fijada: true, Ventanas: datos.Cuantas, Activa: datos.Activa));
        }

        // ---------- 2. Lo abierto que NO está fijado ----------
        var sueltas = abiertas
            .Where(kv => !fijas.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Value.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // El separador solo si hay las dos cosas. Un separador al principio o al final es una
        // raya suelta que no separa nada.
        if (fijas.Count > 0 && sueltas.Count > 0) lista.Add(Separador);

        foreach (var kv in sueltas)
            lista.Add(new Elemento(kv.Key, kv.Value.Nombre, Fijada: false,
                                   Ventanas: kv.Value.Cuantas, Activa: kv.Value.Activa));

        // ---------- 3. El tope ----------
        //
        // Con cuarenta ventanas abiertas, un dock de cuarenta iconos ocupa la pantalla entera
        // y deja de ser un dock. Se corta, y las fijadas nunca se caen: son las que el
        // usuario ha elegido que estén siempre.
        if (lista.Count > tope)
        {
            var recortada = lista.Take(tope).ToList();
            // Si el corte deja un separador al final, se va con él.
            while (recortada.Count > 0 && recortada[^1].EsSeparador)
                recortada.RemoveAt(recortada.Count - 1);
            return recortada;
        }

        return lista;
    }

    // La clave de comparación: el nombre del programa sin ruta ni extensión, en minúsculas.
    // Se usa el mismo normalizador que NubeHypr para que «Discord.exe», «discord» y
    // «C:\…\Discord.exe» sean lo mismo en los dos sitios.
    public static string Clave(string? s) =>
        s is not { Length: > 0 } ? "" : NubeHypr.NombreDeProceso(s).ToLowerInvariant();

    // ================= Dónde se coloca =================
    //
    // El dock va en el borde OPUESTO a la barra. Es lo que da la composición de la referencia
    // —barra a un lado, dock al otro— y además evita el problema de verdad: dos superficies
    // permanentes en el mismo borde se pisan y hay que decidir cuál cede.
    public static NubeHypr.Borde BordeDelDock(NubeHypr.Borde barra) => barra switch
    {
        NubeHypr.Borde.Arriba => NubeHypr.Borde.Abajo,
        NubeHypr.Borde.Abajo => NubeHypr.Borde.Arriba,
        NubeHypr.Borde.Izquierda => NubeHypr.Borde.Derecha,
        _ => NubeHypr.Borde.Izquierda,
    };

    public static bool EsVertical(NubeHypr.Borde b) =>
        b is NubeHypr.Borde.Izquierda or NubeHypr.Borde.Derecha;

    // ---------- El rectángulo ----------
    //
    // Centrado en el eje largo y pegado a su borde con un margen. Devuelve píxeles de
    // pantalla, como todo lo que coloca ventanas en este proyecto: las unidades de WPF no
    // significan lo mismo en dos monitores con DPI distinto.
    //
    // `util` es el área de trabajo, no el monitor entero: así el dock no se mete debajo de la
    // barra de tareas de Windows cuando el usuario la conserva.
    public const double Lado = 44;        // el lado de un icono
    public const double Hueco = 6;        // entre iconos
    public const double Aire = 10;        // el relleno de la tarjeta
    public const double DelFilo = 10;     // separación del borde de la pantalla

    public static NubeHypr.Rect Sitio(NubeHypr.Rect util, NubeHypr.Borde borde, int cuantos,
                                      double lado = Lado, double hueco = Hueco,
                                      double aire = Aire, double delFilo = DelFilo)
    {
        cuantos = Math.Max(0, cuantos);

        // El largo: los iconos, los huecos entre ellos y el aire de los dos extremos.
        double largo = cuantos == 0 ? aire * 2
                                    : cuantos * lado + (cuantos - 1) * hueco + aire * 2;
        double grueso = lado + aire * 2;

        if (EsVertical(borde))
        {
            // No puede ser más largo que la pantalla: con treinta iconos en un portátil, un
            // dock más alto que el monitor se sale por los dos extremos a la vez.
            largo = Math.Min(largo, Math.Max(grueso, util.Alto - delFilo * 2));
            double y = util.Y + (util.Alto - largo) / 2;
            double x = borde == NubeHypr.Borde.Izquierda
                ? util.X + delFilo
                : util.X + util.Ancho - grueso - delFilo;
            return new NubeHypr.Rect(x, y, grueso, largo);
        }
        else
        {
            largo = Math.Min(largo, Math.Max(grueso, util.Ancho - delFilo * 2));
            double x = util.X + (util.Ancho - largo) / 2;
            double y = borde == NubeHypr.Borde.Arriba
                ? util.Y + delFilo
                : util.Y + util.Alto - grueso - delFilo;
            return new NubeHypr.Rect(x, y, largo, grueso);
        }
    }

    // ================= El indicador de un icono =================
    //
    // ---------- Por qué vive aquí y no en el dibujo ----------
    //
    // Porque «la ventana de delante se distingue de una abierta» es una promesa que hay que
    // poder comprobar, y comprobarla sobre los colores crudos **mide lo que no es**: el dock
    // pinta el indicador con una opacidad distinta en cada estado, así que lo que el ojo ve
    // no son los dos colores, son los dos colores YA MEZCLADOS con el fondo.
    //
    // Una prueba que compare el azul contra el gris sin esa mezcla da un resultado que no se
    // corresponde con nada de lo que pasa en pantalla. Pasó: cuatro de los siete temas
    // «fallaban» una comprobación que estaba mirando el sitio equivocado.
    //
    // ---------- Y por qué no basta con el color ----------
    //
    // Porque dos colores pueden tener casi la misma luminancia y distinguirse perfectamente
    // por el tono —azul contra gris lo hacen— pero apoyar la diferencia SOLO en el tono deja
    // fuera a quien no distingue bien los colores. Por eso el estado se cuenta por **tres
    // canales a la vez**: color, opacidad y longitud de la rayita.
    public readonly record struct Marca(bool Activa, double Opacidad, double Largo)
    {
        public bool EsLaDeDelante => Activa;
    }

    public const double LargoNormal = 10;
    public const double LargoVarias = 16;

    public static Marca MarcaDe(Elemento e) => new(
        Activa: e.Activa,
        // La de delante, a plena opacidad; una abierta cualquiera, atenuada. Esta es la
        // diferencia que funciona aunque el color no se distinga.
        Opacidad: e.Activa ? 1.0 : 0.7,
        // Y la longitud cuenta otra cosa: si tiene una ventana o varias.
        Largo: e.Ventanas > 1 ? LargoVarias : LargoNormal);

    // ¿Se distinguen de verdad los dos estados? Las tres diferencias, y basta con que haya
    // dos: con solo una, un usuario que no perciba ese canal se queda sin la información.
    public static bool SeDistinguen(Marca activa, Marca abierta)
    {
        int canales = 0;
        if (activa.Activa != abierta.Activa) canales++;                 // el color
        if (Math.Abs(activa.Opacidad - abierta.Opacidad) >= 0.15) canales++;
        if (Math.Abs(activa.Largo - abierta.Largo) >= 4) canales++;
        return canales >= 2;
    }

    // ================= Cuándo se ve =================
    //
    // Tres políticas, y la de serie es «siempre». El dock de la referencia es permanente, y
    // un dock que se esconde obliga a buscarlo.
    public enum Cuando { Siempre, AlAcercarse, Nunca }

    public static Cuando Leer(string? s) => (s ?? "").Trim().ToLowerInvariant() switch
    {
        "nunca" or "no" or "apagado" => Cuando.Nunca,
        "alacercarse" or "acercarse" or "auto" => Cuando.AlAcercarse,
        _ => Cuando.Siempre,
    };

    // ¿Se ve ahora? Con `AlAcercarse`, solo si el ratón está cerca de su borde o si el propio
    // dock tiene el ratón encima —si no, se escondería justo cuando vas a pulsar un icono—.
    public static bool SeVe(Cuando cuando, bool ratonCerca, bool ratonEncima) => cuando switch
    {
        Cuando.Nunca => false,
        Cuando.AlAcercarse => ratonCerca || ratonEncima,
        _ => true,
    };

    // La franja sensible: la del borde del dock, de este grosor. Más fina y hay que afinar
    // con el ratón; más gorda y el dock aparece sin querer.
    public const double FranjaSensible = 6;

    public static bool RatonCerca(NubeHypr.Rect util, NubeHypr.Borde borde, double x, double y,
                                  double franja = FranjaSensible)
    {
        // Fuera del monitor, no. Con dos pantallas, acercarse al filo de una no puede sacar
        // el dock de la otra.
        if (x < util.X || x > util.X + util.Ancho || y < util.Y || y > util.Y + util.Alto)
            return false;

        return borde switch
        {
            NubeHypr.Borde.Arriba => y <= util.Y + franja,
            NubeHypr.Borde.Abajo => y >= util.Y + util.Alto - franja,
            NubeHypr.Borde.Izquierda => x <= util.X + franja,
            _ => x >= util.X + util.Ancho - franja,
        };
    }

    // ================= El menú contextual =================
    //
    // Lo que se puede hacer con un elemento depende de si está fijado y de si está abierto.
    // Decidirlo aquí —y no al construir el menú— permite comprobar que a una app cerrada no
    // se le ofrece «Cerrar», que es la clase de detalle que se cuela al escribirlo a mano.
    public enum Accion { Abrir, Activar, Cerrar, Fijar, Soltar, VerVentanas }

    public static List<Accion> MenuDe(Elemento e)
    {
        var l = new List<Accion>();
        if (e.EsSeparador) return l;

        if (e.Abierta)
        {
            l.Add(Accion.Activar);
            if (e.Ventanas > 1) l.Add(Accion.VerVentanas);
        }
        else l.Add(Accion.Abrir);

        l.Add(e.Fijada ? Accion.Soltar : Accion.Fijar);

        // Cerrar va al final y solo si hay algo que cerrar. Es la destructiva, y en el menú
        // se pinta en coral por la misma regla de D6.
        if (e.Abierta) l.Add(Accion.Cerrar);
        return l;
    }

    public static string Rotulo(Accion a, Elemento e) => a switch
    {
        Accion.Abrir => "Abrir",
        Accion.Activar => "Traer al frente",
        Accion.Cerrar => e.Ventanas > 1 ? $"Cerrar las {e.Ventanas} ventanas" : "Cerrar",
        Accion.Fijar => "Anclar al dock",
        Accion.Soltar => "Quitar del dock",
        Accion.VerVentanas => $"Ver sus {e.Ventanas} ventanas",
        _ => a.ToString(),
    };

    public static bool EsDestructiva(Accion a) => a == Accion.Cerrar;
}
