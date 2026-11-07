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

    // ================= Y el menú contextual, en el cursor =================
    //
    // ---------- Por qué esto no usa `DondeCabe` ----------
    //
    // `DondeCabe` coloca al LADO de la tarjeta, y existe por una razón buena: la
    // previsualización no puede tapar la ventana que estás colocando. Para el menú del clic
    // derecho esa regla resultó ser la equivocada, y lo dijo el usuario: «el menú no sale
    // donde se realiza el click».
    //
    // Tiene razón y es lo que hace todo lo demás. Un menú contextual sale en la punta del
    // cursor —en Windows, en el escritorio, en el navegador— porque el cursor es donde está
    // mirando el usuario y donde ya tiene la mano. Sacarlo a 300 px de allí obliga a buscarlo
    // y a cruzar media rejilla para llegar a la primera opción.
    //
    // `DondeCabe` se queda para cuando NO hay cursor: con la tecla de menú contextual o
    // Mayús+F10 no hay punto donde abrirlo, y entonces sí vale la regla vieja de ponerlo al
    // lado de la tarjeta sin taparla.
    //
    // ---------- Y lo otro que fallaba: salía cortado ----------
    //
    // El menú de una ventana tiene catorce opciones: unos 488 px de alto. El panel de la
    // vista mide ~320. No cabía por ningún sitio, y el código solo topaba el borde de
    // ARRIBA (`Math.Max(2, …)`), así que el menú empezaba dentro y se iba por abajo: las
    // últimas opciones —«cerrar» y «forzar el cierre»— quedaban fuera del recorte y no había
    // forma de llegar a ellas. Por eso había que «adaptarlo» cada vez.
    //
    // Así que esto devuelve también el alto que de verdad cabe y si hay que desplazar por
    // dentro. Un menú que no cabe se desplaza; lo que no hace nunca es cortarse.
    public sealed record SitioMenu(NubeHypr.Rect Donde, bool ConScroll);

    // El aire que se le deja al borde del área. Más pequeño que `HuecoMinimo`: aquí no se
    // trata de no tapar nada, sino de que el filo del menú no se pegue al del panel.
    public const double HuecoMenu = 4;

    public static SitioMenu MenuEnElPunto(double clicX, double clicY, NubeHypr.Rect area,
                                          double ancho, double alto)
    {
        ancho = Math.Max(1, ancho);
        alto = Math.Max(1, alto);

        // Lo que de verdad hay sitio para dibujar. Si el menú es más alto que el área, se
        // queda con el área y se desplaza por dentro: es la diferencia entre «no cabe» y
        // «no se puede usar».
        double cabeAncho = Math.Max(1, area.Ancho - HuecoMenu * 2);
        double cabeAlto = Math.Max(1, area.Alto - HuecoMenu * 2);

        bool conScroll = alto > cabeAlto;
        double anchoReal = Math.Min(ancho, cabeAncho);
        double altoReal = Math.Min(alto, cabeAlto);

        // Hacia la derecha y hacia abajo del cursor, que es la esquina por la que se lee un
        // menú. Si por ese lado no cabe, se vuelca al otro —como hace Windows— en vez de
        // arrastrarse contra el borde, que dejaría el cursor en mitad del menú y la primera
        // opción debajo del puntero, lista para pulsarse sin querer.
        double x = clicX;
        if (x + anchoReal > area.X + area.Ancho - HuecoMenu) x = clicX - anchoReal;

        double y = clicY;
        if (y + altoReal > area.Y + area.Alto - HuecoMenu) y = clicY - altoReal;

        // Y pase lo que pase, dentro. Los dos bordes, no solo el de arriba: ese era el fallo.
        x = Dentro(x, area.X, area.Ancho, anchoReal);
        y = Dentro(y, area.Y, area.Alto, altoReal);

        return new SitioMenu(new NubeHypr.Rect(x, y, anchoReal, altoReal), conScroll);
    }

    // Mete un trozo dentro del área por los DOS lados. El `Math.Max` del tope no es
    // prolijidad: con un área más pequeña que el trozo, el mínimo saldría mayor que el
    // máximo y `Math.Clamp` lanza.
    static double Dentro(double v, double inicio, double total, double mio)
    {
        double min = inicio + HuecoMenu;
        double max = inicio + total - mio - HuecoMenu;
        return Math.Clamp(v, min, Math.Max(min, max));
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
