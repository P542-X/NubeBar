using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D6: las decisiones de la vista general rediseñada, fuera del manejador.
//
// ---------- Por qué están aquí y no en MainWindow.Vista.cs ----------
//
// Porque son decisiones, no pintura, y las decisiones metidas en un manejador son el
// patrón que ya produjo los cuatro fallos de C4.1 y los tres de C4.2. Lo que hay aquí
// —jerarquía, selección, movimiento con el teclado y dónde cabe la previsualización— se
// puede comprobar sin abrir una ventana, sin un monitor y sin un ratón. Lo que queda en
// `MainWindow.Vista.cs` es poner colores y tamaños a lo que esto decide.
public static class HyprVista
{
    // ================= Jerarquía =================
    //
    // Tres cosas distintas que la vista tiene que distinguir **a la vez**, y que antes de
    // D6 se mezclaban en una sola idea de "la activa":
    //
    //  - La ventana ACTIVA del sistema: la que tiene el foco de Windows ahora mismo.
    //  - La tarjeta ELEGIDA: dónde está el cursor del teclado. Puede no ser la activa, y
    //    esa es justamente la situación normal mientras navegas con las flechas.
    //  - Las APAGADAS: minimizadas. Siguen estando, pero no se pueden fotografiar y no
    //    compiten por la atención.
    //
    // Sin separar las dos primeras no hay navegación con teclado posible: "la elegida" no
    // tendría dónde vivir, y marcar con el acento la activa dejaría al teclado sin ningún
    // sitio donde enseñar por dónde va.
    public enum Nivel { Normal, Apagada, Elegida, Activa, ActivaYElegida }

    public static Nivel NivelDe(bool activa, bool elegida, bool minimizada) =>
        activa && elegida ? Nivel.ActivaYElegida
        : activa ? Nivel.Activa
        : elegida ? Nivel.Elegida
        : minimizada ? Nivel.Apagada
        : Nivel.Normal;

    // ---------- La pinta de cada nivel ----------
    //
    // `Grosor` es SIEMPRE el mismo. No es un descuido: con el grosor cambiando entre 0 y
    // 1,4 según el foco, marcar una tarjeta movía su contenido un píxel, y con el foco
    // cambiando solo eso se lee como que las tarjetas tiemblan. Lo que cambia es el color
    // del filo y, en la elegida, un segundo filo por fuera.
    //
    // `Halo` es ese segundo filo: el foco visible que pide la accesibilidad. Va por fuera
    // del marco, así que tampoco mueve nada de dentro.
    public const double GrosorDelFilo = 1.4;
    public const double GrosorDelHalo = 2.0;

    public sealed record Pinta(
        double Grosor,
        bool FiloDeAcento,     // el filo coloreado: la activa del sistema
        bool Halo,             // el anillo de foco del teclado: la elegida
        double Opacidad,
        bool Negrita)
    {
        // Lo que se le dice a un lector de pantalla. El color nunca es el único portador
        // del estado: eso es la regla de accesibilidad del documento.
        public string Estado =>
            FiloDeAcento && Halo ? "activa y elegida"
            : FiloDeAcento ? "activa"
            : Halo ? "elegida"
            : Opacidad < 1 ? "minimizada"
            : "";
    }

    public static Pinta PintaDe(Nivel n) => n switch
    {
        Nivel.ActivaYElegida => new Pinta(GrosorDelFilo, true, true, 1.0, true),
        Nivel.Activa => new Pinta(GrosorDelFilo, true, false, 1.0, true),
        Nivel.Elegida => new Pinta(GrosorDelFilo, false, true, 1.0, false),
        Nivel.Apagada => new Pinta(GrosorDelFilo, false, false, 0.62, false),
        _ => new Pinta(GrosorDelFilo, false, false, 1.0, false),
    };

    // ================= Moverse con el teclado =================
    //
    // La rejilla es uniforme: `columnas` por fila, la última fila puede ir corta. Las
    // flechas se mueven por la rejilla y **no dan la vuelta**: en la primera columna,
    // izquierda no salta al final de la fila anterior.
    //
    // Por qué sin vuelta: con vuelta, mantener pulsada una flecha recorre la rejilla en
    // bucle y nunca se para en un borde, así que no hay forma de saber que has llegado al
    // final sin mirar. Inicio y Fin sí existen para eso.
    public enum Mover { Izquierda, Derecha, Arriba, Abajo, Principio, Final }

    public static int Siguiente(int actual, int cuantas, int columnas, Mover a)
    {
        if (cuantas <= 0) return -1;
        columnas = Math.Max(1, columnas);

        // Sin nada elegido, la primera flecha elige: la primera con Derecha/Abajo/Inicio y
        // la última con Izquierda/Arriba/Fin. Así la vista se puede usar con el teclado sin
        // tocar antes el ratón, que es media accesibilidad.
        if (actual < 0 || actual >= cuantas)
            return a is Mover.Izquierda or Mover.Arriba or Mover.Final ? cuantas - 1 : 0;

        switch (a)
        {
            case Mover.Principio: return 0;
            case Mover.Final: return cuantas - 1;
            case Mover.Izquierda: return actual % columnas == 0 ? actual : actual - 1;
            case Mover.Derecha: return (actual + 1) % columnas == 0 || actual + 1 >= cuantas
                                       ? actual : actual + 1;
            case Mover.Arriba: return actual - columnas < 0 ? actual : actual - columnas;
            case Mover.Abajo: return actual + columnas >= cuantas ? actual : actual + columnas;
            default: return actual;
        }
    }

    // Cuando la lista cambia debajo de la selección hay que recolocarla. La regla es
    // conservar la MISMA VENTANA si sigue abierta; y si se cerró, quedarse en su hueco
    // —no saltar al principio—, que es lo que uno espera al cerrar tarjetas en fila.
    public static int Recolocar(IntPtr elegida, IReadOnlyList<IntPtr> antes,
                                IReadOnlyList<IntPtr> ahora)
    {
        if (ahora.Count == 0) return -1;

        int sigue = IndiceDe(ahora, elegida);
        if (sigue >= 0) return sigue;

        int donde = IndiceDe(antes, elegida);
        if (donde < 0) return 0;
        return Math.Min(donde, ahora.Count - 1);
    }

    static int IndiceDe(IReadOnlyList<IntPtr> lista, IntPtr h)
    {
        for (int i = 0; i < lista.Count; i++) if (lista[i] == h) return i;
        return -1;
    }

    // ================= La previsualización no tapa la ventana elegida =================
    //
    // ---------- El fallo que esto evita ----------
    //
    // La vista general enseña dónde va a caer una ventana. Si ese rectángulo se dibuja
    // encima de la ventana elegida, el usuario pierde de vista justo lo que está colocando:
    // se queda mirando una mancha de color sin saber qué hay debajo. El documento lo pide
    // con esas palabras — "previsualizacion y menu sin tapar la ventana elegida".
    //
    // Así que el sitio se ELIGE: de los cuatro lados de la ventana elegida, el que tenga
    // hueco; y entre los que tengan hueco, el que más. Si no cabe por ninguno —la elegida
    // ocupa casi toda la pantalla— se devuelve el mejor sitio posible y se dice que hay
    // solape, para que quien pinte pueda bajar la opacidad en vez de tapar a ciegas.
    public sealed record Sitio(NubeHypr.Rect Donde, string Lado, bool Solapa);

    public const double HuecoMinimo = 8;

    public static Sitio DondeCabe(NubeHypr.Rect elegida, NubeHypr.Rect area,
                                  double ancho, double alto)
    {
        ancho = Math.Max(1, ancho); alto = Math.Max(1, alto);

        // Cuánto queda libre a cada lado de la elegida dentro del área. Se mide contra el
        // área útil, no contra la pantalla: la isla ya ha reservado su franja y meter la
        // previsualización debajo de la barra sería no enseñarla.
        double aIzq = elegida.X - area.X;
        double aDer = area.X + area.Ancho - (elegida.X + elegida.Ancho);
        double arriba = elegida.Y - area.Y;
        double abajo = area.Y + area.Alto - (elegida.Y + elegida.Alto);

        var lados = new (string Lado, double Libre, Func<NubeHypr.Rect> Donde)[]
        {
            ("derecha",  aDer,    () => new NubeHypr.Rect(area.X + area.Ancho - ancho - HuecoMinimo, Centrado(elegida.Y, elegida.Alto, alto, area.Y, area.Alto), ancho, alto)),
            ("izquierda", aIzq,   () => new NubeHypr.Rect(area.X + HuecoMinimo, Centrado(elegida.Y, elegida.Alto, alto, area.Y, area.Alto), ancho, alto)),
            ("abajo",    abajo,   () => new NubeHypr.Rect(Centrado(elegida.X, elegida.Ancho, ancho, area.X, area.Ancho), area.Y + area.Alto - alto - HuecoMinimo, ancho, alto)),
            ("arriba",   arriba,  () => new NubeHypr.Rect(Centrado(elegida.X, elegida.Ancho, ancho, area.X, area.Ancho), area.Y + HuecoMinimo, ancho, alto)),
        };

        // Primero los que de verdad caben, de más hueco a menos. El orden de la lista
        // rompe los empates, y es deliberado: derecha antes que izquierda, y los lados
        // antes que arriba y abajo, porque una ventana de escritorio es más ancha que alta
        // y el hueco de los lados es el que suele existir.
        foreach (var l in lados.Where(l => l.Libre >= ancho + HuecoMinimo * 2 ||
                                           (l.Lado is "arriba" or "abajo" && l.Libre >= alto + HuecoMinimo * 2))
                               .OrderByDescending(l => l.Libre))
        {
            var donde = l.Donde();
            if (!Solapan(donde, elegida)) return new Sitio(donde, l.Lado, false);
        }

        // No cabe por ningún lado: el mejor hueco, y dicho.
        var mejor = lados.OrderByDescending(l => l.Libre).First();
        return new Sitio(mejor.Donde(), mejor.Lado, true);
    }

    // Centrar un trozo frente a otro sin salirse del área.
    static double Centrado(double inicio, double largo, double mio, double limite, double total)
    {
        double x = inicio + (largo - mio) / 2;
        return Math.Clamp(x, limite, Math.Max(limite, limite + total - mio));
    }

    public static bool Solapan(NubeHypr.Rect a, NubeHypr.Rect b) =>
        a.X < b.X + b.Ancho && b.X < a.X + a.Ancho &&
        a.Y < b.Y + b.Alto && b.Y < a.Y + a.Alto;

    // ================= No repintar sin motivo =================
    //
    // La vista ya reconcilia en vez de rehacerse (C4.1), y el indicador de escritorios ya
    // compara antes de repintar. Lo que faltaba era poder **comprobar** esa decisión en vez
    // de confiarla a un `if` dentro del manejador.
    //
    // Una huella es una cadena corta con lo que de verdad decide el dibujo. Si no cambia,
    // no se toca nada: ni un `Children.Clear()`, ni una animación, ni un `Measure`.
    public static string Huella(IEnumerable<string> trozos) => string.Join("", trozos);

    public static bool TocaRepintar(string antes, string ahora) => antes != ahora;

    // Y el caso que de verdad importa con CPU alta: si llegan treinta avisos en un
    // segundo, el repintado se hace UNA vez. `Aplazador` no es un reloj nuevo: dice si
    // toca ya o si hay que esperar, y quien lo usa ya tiene su propio reloj.
    public const double MsEntreRepintados = 120;

    public static bool YaTocaOtraVez(DateTime ultimo, DateTime ahora) =>
        (ahora - ultimo).TotalMilliseconds >= MsEntreRepintados;
}
