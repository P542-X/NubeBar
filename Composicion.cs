using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E1: la composición de la barra, como objeto.
//
// ---------- Por qué un objeto y no una lista escrita a mano ----------
//
// La barra de la macrofase E no necesita módulos nuevos —están los quince de siempre, más el
// de la nube— sino una **distribución** concreta: qué va en cada zona, con qué grosor y en qué
// borde, para que salga la composición de la referencia y no un amontonamiento.
//
// Escribir esa distribución en el código de la barra la haría imposible de probar y de
// cambiar. Aquí es un dato: se puede comprobar que no repite módulos, que no pide ninguno que
// no existe, que cabe en vertical, y que aplicarla y deshacerla devuelve exactamente lo que
// había.
//
// ---------- Lo que NO hace ----------
//
// No escribe la configuración. Una composición se **prueba en memoria** y solo se guarda si el
// usuario lo pide, por el mismo motivo que todo lo demás de este proyecto: la distribución de
// módulos es trabajo del usuario y cambiarla sin preguntar es perder su trabajo.
public static class Composicion
{
    // ---------- Una composición ----------
    //
    // Las tres zonas son las de siempre, y conservan su significado en las dos orientaciones:
    // «Izquierda» es *al principio* (arriba, en vertical), «Centro» es el medio y «Derecha»
    // es *al final* (abajo). Es lo que hace Plasma y lo que ya hace `OrientarZonas`, así que
    // una composición no tiene que saber si la barra está tumbada o de pie.
    public sealed record Reparto(
        string Nombre,
        string Que,
        IReadOnlyList<string> Izquierda,
        IReadOnlyList<string> Centro,
        IReadOnlyList<string> Derecha,
        string Modo = "barra",
        string Posicion = "arriba",
        double Grosor = 38)
    {
        public IEnumerable<string> Todos => Izquierda.Concat(Centro).Concat(Derecha);
        public int Cuantos => Todos.Count();

        public override string ToString() =>
            $"{Nombre} [{Modo}/{Posicion} {Grosor:0}px] " +
            $"{string.Join(",", Izquierda)} | {string.Join(",", Centro)} | {string.Join(",", Derecha)}";
    }

    // ================= Las composiciones =================
    //
    // Dos, y la diferencia entre ellas no es estética: es **cuánto texto cabe**.
    //
    // En horizontal hay sitio de sobra y las etiquetas van enteras («CPU 12%», «lun 26 oct»).
    // En vertical, una columna de 38 px no admite ni «CPU 12%» ni una fecha, así que la
    // composición vertical lleva menos módulos y los que lleva son los que saben apilarse.
    // Meter los mismos quince en una columna no da una barra vertical: da una columna de
    // texto cortado.

    // ---------- Horizontal: la referencia secundaria ----------
    //
    // Escritorios y apps al principio, el reloj en el centro, y el estado al final. Es la
    // distribución de la captura horizontal y la de casi cualquier escritorio de Linux:
    // lo que identifica *dónde estás* a un lado, lo que te dice *cómo va la máquina* al otro.
    public static readonly Reparto Horizontal = new(
        Nombre: "escritorio-horizontal",
        Que: "La composición de E con la barra arriba: escritorios y apps a la izquierda, " +
             "reloj en el centro, estado y nube a la derecha.",
        Izquierda: new[] { "inicio", "escritorios", "fijadas", "apps" },
        Centro: new[] { "reloj", "fecha" },
        // Ojo: **no hay un módulo «red»**. El estado de la conexión vive dentro del centro de
        // control y en la bandeja, que es donde Windows lo pone. La primera versión de esta
        // composición pedía uno y la revisión la rechazó entera, que es exactamente para lo
        // que `Revisar` existe: antes de aplicar, no después.
        Derecha: new[] { "musica", "cpu", "ram", "bandeja", "notificaciones",
                         "bateria", "nube", "control" },
        Modo: "barra", Posicion: "arriba", Grosor: 38);

    // ---------- Vertical: la referencia principal ----------
    //
    // Una columna estrecha de iconos. Sin fecha, sin CPU, sin RAM: son etiquetas de texto y
    // en 44 px de ancho no caben de forma legible ni partidas. El reloj sí, porque sabe
    // escribirse en dos líneas (las horas sobre los minutos), y eso ya existe desde B6.
    //
    // El grosor sube de 38 a 44: en vertical el grosor es el ANCHO, y 38 px de ancho deja los
    // iconos pegados a los dos filos.
    public static readonly Reparto Vertical = new(
        Nombre: "escritorio-vertical",
        Que: "La composición de E con la barra en un lateral: una columna estrecha de iconos, " +
             "sin las etiquetas de texto que no caben en 44 px.",
        Izquierda: new[] { "inicio", "escritorios", "apps" },
        Centro: Array.Empty<string>(),
        Derecha: new[] { "bandeja", "notificaciones", "bateria", "nube", "reloj", "control" },
        Modo: "barra", Posicion: "izquierda", Grosor: 44);

    public static readonly Reparto[] Todas = { Horizontal, Vertical };

    public static Reparto? Buscar(string? nombre) =>
        Todas.FirstOrDefault(c => string.Equals(c.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    // ---------- La que toca según el borde ----------
    //
    // Que la composición dependa del borde y no de un ajuste aparte es lo que hace que mover
    // la barra a un lateral no deje una fila de etiquetas cortadas.
    public static Reparto ParaElBorde(string? modo, string? posicion) =>
        PanelNube.EsVertical(modo, posicion) ? Vertical : Horizontal;

    // ================= Comprobar antes de aplicar =================
    //
    // Una composición rota no se aplica a medias: se rechaza y se dice por qué. Aplicar media
    // composición deja la barra con la mitad de los módulos y sin forma de saber qué pasó.
    public sealed record Queja(string Texto, bool Grave);

    public static List<Queja> Revisar(Reparto c, Func<string, bool> existe)
    {
        var quejas = new List<Queja>();
        if (c == null) return new() { new("No hay composición que revisar.", true) };

        // ---------- Módulos que no existen ----------
        foreach (var m in c.Todos)
            if (!existe(m))
                quejas.Add(new($"El módulo «{m}» no está registrado: la barra no sabría qué " +
                               "pintar ahí.", true));

        // ---------- Repetidos ----------
        //
        // Casi todos los módulos NO son repetibles —hay un reloj, una bandeja, una nube— y
        // ponerlos dos veces da dos copias peleándose por el mismo dato. El separador sí se
        // repite, y por eso esto mira duplicados y deja que el que llama decida.
        foreach (var g in c.Todos.GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                                 .Where(g => g.Count() > 1))
            quejas.Add(new($"«{g.Key}» sale {g.Count()} veces.", false));

        // ---------- Vacía ----------
        if (c.Cuantos == 0)
            quejas.Add(new("La composición no tiene ni un módulo: sería una barra vacía.", true));

        // ---------- Grosor ----------
        //
        // Los topes son los de la barra de siempre. Por debajo de 24 no cabe un icono con su
        // aire; por encima de 120 deja de ser una barra fina, que es lo que pide E.
        if (c.Grosor is < 24 or > 120)
            quejas.Add(new($"Un grosor de {c.Grosor:0} px no es una barra fina (24 a 120).", true));

        // ---------- Y el aviso que importa en vertical ----------
        //
        // Los módulos de etiqueta larga no caben en una columna. No es grave —se pintan, y se
        // parten como pueden— pero sale cortado y conviene decirlo en vez de dejar que se
        // descubra mirando.
        if (PanelNube.EsVertical(c.Modo, c.Posicion))
        {
            foreach (var m in c.Todos.Where(x => DeTextoLargo.Contains(x, StringComparer.OrdinalIgnoreCase)))
                quejas.Add(new($"«{m}» es una etiqueta de texto y en una columna de " +
                               $"{c.Grosor:0} px sale apretada.", false));
        }

        return quejas;
    }

    // Los que son texto y no icono. Se parten en dos líneas desde B6, pero aun así son los
    // primeros que sobran cuando la barra se pone de pie.
    public static readonly string[] DeTextoLargo = { "fecha", "cpu", "ram" };

    public static bool SePuedeAplicar(Reparto c, Func<string, bool> existe) =>
        !Revisar(c, existe).Any(q => q.Grave);

    // ================= Lo que había, para poder volver =================
    //
    // Aplicar una composición sin poder deshacerla es pedirle al usuario que se juegue su
    // distribución de módulos por mirar. Esto guarda las tres zonas y la geometría, y
    // `Devolver` las repone tal cual.
    public sealed record LoQueHabia(
        IReadOnlyList<string> Izquierda,
        IReadOnlyList<string> Centro,
        IReadOnlyList<string> Derecha,
        string Modo,
        string Posicion,
        double Grosor);

    public static LoQueHabia Apuntar(Modulos m, string modo, string posicion, double grosor) =>
        new(m.Izquierda.ToList(), m.Centro.ToList(), m.Derecha.ToList(), modo, posicion, grosor);

    // ---------- Se MODIFICAN las listas, no se sustituyen ----------
    //
    // Esto parece un detalle y es el fallo que tuvo la primera versión. `PanelPrincipal.Zonas`
    // no copia las listas de `Modulos`: **las referencia** (ver `MigrarYDerivarPaneles`: «las
    // zonas siguen viviendo en Modulos… el panel las referencia, no las copia»).
    //
    // Así que asignar `m.Izquierda = otra lista` deja al panel apuntando a la lista vieja: el
    // modelo cambia, la barra no. Se aplicaba la composición, el registro decía que se había
    // aplicado, y la captura enseñaba la barra de siempre.
    //
    // Vaciando y rellenando la MISMA lista, quien la referencie ve el cambio.
    static void Poner(List<string> destino, IReadOnlyList<string> origen)
    {
        destino.Clear();
        destino.AddRange(origen);
    }

    // Aplicar y devolver son simétricos a propósito: los dos escriben las mismas tres listas.
    // Si uno tocara algo que el otro no, probar una composición dejaría rastro.
    public static void Aplicar(Reparto c, Modulos m)
    {
        Poner(m.Izquierda, c.Izquierda);
        Poner(m.Centro, c.Centro);
        Poner(m.Derecha, c.Derecha);
    }

    public static void Devolver(LoQueHabia h, Modulos m)
    {
        Poner(m.Izquierda, h.Izquierda);
        Poner(m.Centro, h.Centro);
        Poner(m.Derecha, h.Derecha);
    }
}
