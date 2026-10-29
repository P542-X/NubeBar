using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E3 y E4: la lógica de los paneles laterales.
//
// ---------- Qué decide ----------
//
// Dónde se colocan, qué secciones llevan y cómo se escriben las cifras que enseñan. Puro:
// recibe el área útil y unos números, y devuelve un rectángulo y unas cadenas.
//
// Que la colocación sea pura permite comprobar lo que no se puede provocar a mano: un panel
// más alto que la pantalla, dos paneles abiertos a la vez sin pisarse, un monitor vertical de
// 1080×1920, la barra en cada uno de los cuatro bordes.
//
// ---------- La regla que gobierna la colocación ----------
//
// **Un panel lateral nunca se mete debajo de la barra.** Se coloca dentro del área útil, que
// ya descuenta la barra cuando esta reserva espacio. Y cuando la barra está en el mismo lado
// que el panel —barra a la izquierda, panel izquierdo— el panel se aparta lo que haga falta,
// porque si no saldría encima de ella.
public static class Lateral
{
    public enum Lado { Izquierda, Derecha }

    // ---------- Medidas ----------
    //
    // El ancho es fijo y no un porcentaje: un panel de controles que en un 4K midiera 900 px
    // sería una columna con cuatro conmutadores perdidos en medio. Lo que cambia con la
    // pantalla es el alto, que se recorta si no cabe.
    public const double Ancho = 340;

    // ---------- El aire para el halo ----------
    //
    // Cuánto se agranda la VENTANA del panel por cada lado cuando el estilo brilla, para que
    // el resplandor tenga dónde dibujarse. Una sombra difuminada se pinta fuera del elemento;
    // con la ventana pegada a la tarjeta, se recorta y no se ve nada — que es exactamente lo
    // que pasaba.
    //
    // 30 y no 26 (el radio máximo del halo): un difuminado gaussiano no acaba de golpe en su
    // radio nominal, y cortarlo justo ahí deja un canto recto en el degradado que se nota más
    // que el propio halo.
    //
    // No cambia el panel: la tarjeta lleva este mismo margen hacia dentro, así que lo que se
    // ve sigue midiendo `Ancho`.
    public const double AireDelHalo = 30;

    // Cuánto aire cabe de verdad por cada lado sin salirse del hueco útil. Es puro para
    // poder exigirlo en una prueba: la primera versión sumaba el aire a los cuatro lados sin
    // mirar y sacó cuatro paneles fuera de la pantalla — lo cazó `--diagpaneles`, pero una
    // cuenta de geometría tiene que poder comprobarse sin abrir una ventana.
    public readonly record struct Aire(double Izq, double Arr, double Der, double Aba);

    public static Aire AireQueCabe(NubeHypr.Rect hueco, NubeHypr.Rect panel, double quiero)
    {
        if (quiero <= 0) return new Aire(0, 0, 0, 0);
        double q = Math.Max(0, quiero);
        return new Aire(
            Math.Clamp(panel.X - hueco.X, 0, q),
            Math.Clamp(panel.Y - hueco.Y, 0, q),
            Math.Clamp(hueco.X + hueco.Ancho - (panel.X + panel.Ancho), 0, q),
            Math.Clamp(hueco.Y + hueco.Alto - (panel.Y + panel.Alto), 0, q));
    }
    public const double DelFilo = 12;        // separación del borde de la pantalla
    public const double DelOtroLado = 12;    // y del borde de arriba y de abajo

    public static NubeHypr.Rect Sitio(NubeHypr.Rect util, Lado lado, double altoQuePide,
                                      double ancho = Ancho, double delFilo = DelFilo,
                                      double delOtroLado = DelOtroLado)
    {
        // No puede ser más ancho que la pantalla. En un portátil de 1024 con dos paneles
        // abiertos, dos columnas de 340 dejan 344 px de escritorio: estrecho, pero visible.
        ancho = Math.Min(ancho, Math.Max(160, util.Ancho - delFilo * 2));

        double maxAlto = Math.Max(120, util.Alto - delOtroLado * 2);
        double alto = Math.Clamp(altoQuePide, 120, maxAlto);

        double x = lado == Lado.Izquierda
            ? util.X + delFilo
            : util.X + util.Ancho - ancho - delFilo;

        // Centrado en vertical. Pegado arriba quedaría colgando de la barra y pegado abajo
        // parecería caído; centrado es lo que hace la referencia y lo que deja el mismo aire
        // por los dos lados.
        double y = util.Y + (util.Alto - alto) / 2;

        return new NubeHypr.Rect(x, y, ancho, alto);
    }

    // ---------- ¿Se pisan dos paneles? ----------
    //
    // Con los dos abiertos en una pantalla estrecha. Si se pisaran, lo que hay debajo sería
    // inalcanzable, y eso es peor que no abrir el segundo.
    public static bool SePisan(NubeHypr.Rect a, NubeHypr.Rect b) =>
        a.X < b.X + b.Ancho && b.X < a.X + a.Ancho &&
        a.Y < b.Y + b.Alto && b.Y < a.Y + a.Alto;

    // Lo más ancho que puede ser cada panel para que quepan los dos con un hueco de
    // escritorio en medio. Debajo de esto, el segundo panel no se abre.
    public const double HuecoMinimoDeEscritorio = 120;

    public static bool CabenLosDos(NubeHypr.Rect util, double ancho = Ancho,
                                   double delFilo = DelFilo) =>
        util.Ancho - (ancho + delFilo) * 2 >= HuecoMinimoDeEscritorio;

    // ================= Las secciones =================
    //
    // El catálogo, con su orden. Está aquí y no repartido por el código de la ventana para
    // poder comprobar que no hay dos con el mismo id, que todas tienen título, y que el panel
    // izquierdo y el derecho no se solapan en contenido — que es la forma de acabar con dos
    // sitios que enseñan lo mismo.
    // ---------- `Icono` y `Suelta` ----------
    //
    // `Icono` es el glifo de la cabecera. No es adorno: siete rótulos de texto en columna, sin
    // nada a la izquierda, se leen como una lista de la compra — que es exactamente el aspecto
    // que tenían estos paneles. El icono es lo que deja saltar de una sección a otra sin leer.
    //
    // `Suelta` dice que la sección **no** va dentro de una tarjeta, porque su contenido ya son
    // tarjetas. Meter tarjetas dentro de una tarjeta da un marco rodeando otros marcos, que es
    // ruido. Lo usan las notificaciones y el reproductor.
    // `SinCabecera`: la sección se pinta sin la fila de icono + título + flecha. La usa la
    // tarjeta de identidad, porque en la referencia la nube ES la tarjeta — poner encima una
    // cabecera que dice «Perfil y nube» y debajo un bloque que dice «Nube» es decir lo mismo
    // dos veces y robarle el sitio a la única tarjeta que tiene que mandar.
    public sealed record Seccion(string Id, string Titulo, string Que, Lado Donde,
                                 int Icono, bool Suelta = false, bool SinCabecera = false);

    // Los glifos, de la fuente de iconos de Windows. Aquí y no en el constructor de cada
    // sección para poder comprobarlos todos de una vez: un glifo que no existe en la fuente
    // sale como un cuadradito y no lo nota nadie hasta que lo ve un usuario.
    public static class Glifos
    {
        public const int Perfil = 0xE77B;       // Contact2
        public const int Rapidas = 0xE945;      // LightningBolt
        public const int Volumen = 0xE767;      // Volume
        public const int Brillo = 0xE706;       // Brightness
        public const int Carpetas = 0xE8B7;     // FolderHorizontal
        public const int Maquina = 0xE950;      // DeveloperTools / chip
        public const int Ajustes = 0xE713;      // Settings
        public const int Medios = 0xE8D6;       // MusicInfo
        public const int Avisos = 0xEA8F;       // Message / campana
        public const int Calendario = 0xE787;   // Calendar
        public const int Escritorio = 0xE7F4;   // TVMonitor
        public const int Conexion = 0xE839;     // Ethernet
        public const int Clima = 0xE753;        // Cloud — y además es el logo de la casa

        // Los estados de red y de volumen, para los módulos sueltos de la barra. Viven aquí y
        // no en el fichero que los usa por el motivo de siempre: es la lista que la prueba
        // recorre para comprobar que TODOS existen en la fuente. Un glifo inventado sale como
        // un rectángulo vacío y no lo nota nadie leyendo el código.
        public const int Wifi = 0xE701;         // WiFi
        public const int SinRed = 0xEB55;       // error / sin conexión
        public const int Silencio = 0xE74F;     // Mute
        public const int VolumenBajo = 0xE993;  // Volume1
        public const int VolumenMedio = 0xE994; // Volume2
        public const int Teclado = 0xE765;      // KeyboardClassic
        public const int Pila = 0xEBA8;         // una pila con nivel, para el chip del editor

        public static readonly int[] Todos =
        {
            Perfil, Rapidas, Volumen, Brillo, Carpetas, Maquina, Ajustes,
            Medios, Avisos, Calendario, Escritorio, Conexion, Clima,
            Wifi, SinRed, Silencio, VolumenBajo, VolumenMedio, Teclado, Pila,
        };
    }

    public static readonly Seccion[] Secciones =
    {
        // ---------- Izquierdo: lo que CONTROLAS ----------
        new("perfil", "Nube", "Tu entorno, en calma", Lado.Izquierda, Glifos.Perfil,
            SinCabecera: true),
        new("rapidas", "Acciones rápidas", "Lo que se enciende y se apaga de un toque", Lado.Izquierda, Glifos.Rapidas),
        // Los dos deslizadores en UNA tarjeta, como la maqueta. Eran dos secciones con dos
        // cabeceras y dos marcos para dos controles de una línea cada uno: el panel se iba en
        // cromo. Y van juntos de verdad — lo que uno ajusta cuando ajusta el otro.
        new("audio", "Audio y brillo", "El volumen del sistema y el brillo del monitor",
            Lado.Izquierda, Glifos.Volumen),
        new("carpetas", "Carpetas", "Accesos a tus carpetas de siempre", Lado.Izquierda, Glifos.Carpetas),
        new("maquina", "Sistema", "CPU, memoria y almacenamiento", Lado.Izquierda, Glifos.Maquina),
        new("ajustes", "Ajustes", "Tema, modo nocturno y la ventana de ajustes", Lado.Izquierda, Glifos.Ajustes),

        // ---------- Derecho: lo que PASA ----------
        new("medios", "Reproduciendo", "El centro multimedia", Lado.Derecha, Glifos.Medios, Suelta: true),
        new("avisos", "Notificaciones", "Lo que ha llegado", Lado.Derecha, Glifos.Avisos, Suelta: true),
        new("calendario", "Calendario", "El mes, con hoy marcado", Lado.Derecha, Glifos.Calendario),
        new("escritorio", "Espacios de trabajo", "Los escritorios virtuales, y a cuál vas",
            Lado.Derecha, Glifos.Escritorio),
        new("conexion", "Conexión y batería", "Red y energía", Lado.Derecha, Glifos.Conexion),
        // La maqueta junta clima y sistema en UNA tarjeta: la nube y los grados a la
        // izquierda, y a la derecha la columna de Windows, procesador y memoria. No son dos
        // cosas puestas juntas por ahorrar sitio — es «dónde estoy y en qué estoy», que es lo
        // que uno mira de un vistazo al abrir el panel. Con el clima apagado la tarjeta es la
        // columna de siempre y una línea para encenderlo.
        new("maquina-info", "Clima y sistema", "El tiempo donde estás, y qué máquina es esta",
            Lado.Derecha, Glifos.Clima),
    };

    // El reparto de serie: lo que trae cada panel si no has movido nada.
    public static IReadOnlyList<Seccion> De(Lado l) =>
        Secciones.Where(s => s.Donde == l).ToList();

    // ================= Y el reparto que TÚ decides =================
    //
    // ---------- Por qué esto tenía que existir ----------
    //
    // `Donde` estaba escrito en el catálogo, así que el panel de cada sección lo decidía yo.
    // El usuario lo dijo sin rodeos: «quiero que todo sea editable y movible; si quiero que
    // el ethernet se mueva y se ponga en el medio que pueda hacerlo, si quiero que el tiempo
    // se ponga arriba que pueda hacerlo».
    //
    // Y tiene razón en algo más que en el gusto: los módulos de la BARRA ya se mueven desde
    // la fase A —zonas, orden, arrastre— y las secciones de los paneles no. Eran la única
    // parte de NubeBar donde el orden lo ponía el programa.
    //
    // ---------- Las tres reglas ----------
    //
    // 1. **Una lista vacía significa «el de serie»**, no «ningún panel». Si no, un config
    //    viejo —que no tiene estas claves— abriría los dos paneles en blanco.
    // 2. **Lo que no esté en la lista se añade al final**, en su orden de catálogo. Así una
    //    sección nueva de una actualización aparece sola en vez de quedarse invisible para
    //    todo el que ya tenga su orden guardado. Es el mismo problema que los módulos nuevos
    //    de la barra, y la misma solución.
    // 3. **Lo que no se reconoce se ignora** y no revienta: un id mal escrito a mano no puede
    //    dejar a nadie sin panel.
    public static IReadOnlyList<Seccion> Repartidas(Lado l,
                                                    IEnumerable<string>? izquierda,
                                                    IEnumerable<string>? derecha)
    {
        var pedidaIzq = (izquierda ?? Enumerable.Empty<string>()).ToList();
        var pedidaDer = (derecha ?? Enumerable.Empty<string>()).ToList();

        // Sin nada guardado en NINGUNO de los dos, manda el catálogo entero. Se miran los dos
        // a la vez a propósito: con uno solo puesto, el otro tiene que quedarse con lo que no
        // se ha llevado el primero, no con su reparto de fábrica — o una sección movida
        // saldría en los dos paneles.
        if (pedidaIzq.Count == 0 && pedidaDer.Count == 0) return De(l);

        var mia = l == Lado.Izquierda ? pedidaIzq : pedidaDer;
        var laOtra = l == Lado.Izquierda ? pedidaDer : pedidaIzq;

        var res = new List<Seccion>();
        foreach (var id in mia)
        {
            var s = Secciones.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (s != null && !res.Contains(s)) res.Add(s);     // id desconocido: se ignora
        }

        // Las que no ha colocado nadie vuelven a su panel de fábrica, al final.
        foreach (var s in Secciones)
        {
            if (res.Contains(s)) continue;
            bool laTieneLaOtra = laOtra.Any(id => id.Equals(s.Id, StringComparison.OrdinalIgnoreCase));
            if (laTieneLaOtra) continue;
            if (s.Donde == l) res.Add(s);
        }
        return res;
    }

    // Los ids tal como hay que guardarlos para un reparto dado. Lo usa Ajustes al mover una
    // sección: se parte del reparto de ahora, se toca una cosa y se guardan los dos lados.
    public static List<string> IdsDe(IReadOnlyList<Seccion> ss) => ss.Select(s => s.Id).ToList();

    // ================= Cómo se escriben las cifras =================
    //
    // ---------- El almacenamiento ----------
    //
    // En gigas o teras, con una cifra decimal como mucho, y diciendo **lo que queda libre**
    // primero. «234 GB libres de 931 GB» contesta la pregunta que uno tiene; «697 GB usados»
    // obliga a restar.
    public static string Almacen(long libres, long total)
    {
        if (total <= 0) return "sin datos";
        return $"{Tamano(libres)} libres de {Tamano(total)}";
    }

    // ---------- Cuánto queda de batería ----------
    //
    // Windows lo da en segundos y hay que escribirlo como lo diría una persona: «2h 14m», no
    // «8040 s» ni «2,23 horas». Y los minutos solos cuando queda menos de una hora, que es
    // justo cuando el dato importa: «0h 12m» hace leer dos veces para enterarte de que te
    // quedan doce minutos.
    public static string CuantoQueda(int segundos)
    {
        if (segundos <= 0) return "";
        int min = segundos / 60;
        int h = min / 60;
        int m = min % 60;
        return h > 0 ? $"{h}h {m}m" : $"{m}m";
    }

    public static string Tamano(long bytes)
    {
        if (bytes < 0) return "—";
        double gb = bytes / 1024.0 / 1024 / 1024;
        if (gb >= 1024) return $"{gb / 1024:0.#} TB";
        if (gb >= 10) return $"{gb:0} GB";
        if (gb >= 1) return $"{gb:0.#} GB";
        return $"{bytes / 1024.0 / 1024:0} MB";
    }

    // Cuánto se ha usado, de 0 a 1, para pintar una barra. Con el total a cero devuelve 0 en
    // vez de reventar: un disco que no se puede leer no es una barra llena.
    public static double UsadoDe(long libres, long total) =>
        total <= 0 ? 0 : Math.Clamp((total - libres) / (double)total, 0, 1);

    // ---------- Y cuándo hay que avisar ----------
    //
    // Por debajo de este hueco libre, la barra del disco se pinta en coral. No es una alarma
    // del sistema: es el mismo criterio de D6, coral para «ojo con esto».
    public const double PocoSitio = 0.90;

    public static bool QuedaPocoSitio(long libres, long total) =>
        total > 0 && UsadoDe(libres, total) >= PocoSitio;

    // ================= Las acciones rápidas =================
    //
    // Pocas y grandes, no veinte. Y **solo las que NubeBar puede hacer de verdad**: un
    // conmutador que no hace nada es peor que no tenerlo, porque se pulsa y se cree que ha
    // funcionado.
    public enum Rapida { NoMolestar, ModoNocturno, Tema, Wallpaper, Ajustes, VistaGeneral }

    // `Icono` es el glifo de la pastilla. La maqueta las pone con icono y tiene razón: cuatro
    // cuadros con dos líneas de texto cada uno son ocho líneas que hay que leer; con icono se
    // sabe cuál es cuál antes de leer ninguna.
    // `Explica` es la frase entera, para la ayuda emergente y para el lector de pantalla.
    // `Apagada`/`Encendida` son el estado corto que va **dentro** de la pastilla, donde hay
    // 95 px y no caben cinco palabras.
    public sealed record Accion(Rapida Que, string Titulo, string Apagada, string Encendida,
                                int Icono, string Explica);

    // Lo que se le dice a quien pasa el ratón o usa un lector: la frase completa, con el
    // estado de ahora delante. En la pastilla no cabe; aquí sí, y es donde se busca cuando
    // una palabra sola no basta.
    public static string Ayuda(Accion a, bool encendida) =>
        $"{a.Titulo}: {Estado(a, encendida)}. {a.Explica}";

    public static readonly Accion[] Rapidas =
    {
        // ---------- Los estados, cortos ----------
        //
        // «Los avisos se guardan» salía como «Los avisos se guar…» en una pastilla de 137 px
        // con icono delante. Y acortarlo no es una concesión al hueco: un estado se lee de
        // reojo, y «Silenciado» dice lo mismo en una palabra. La frase larga vale para la
        // ayuda emergente, que es donde hay sitio.
        new(Rapida.NoMolestar, "No molestar", "Llegan", "Silenciado", 0xE7ED,
            "Encendido, los avisos de Windows se guardan en el centro y no aparecen en pantalla."),
        new(Rapida.ModoNocturno, "Modo nocturno", "Normal", "Cálido", 0xE708,
            "Los colores de la pantalla tiran a cálido por la noche. Lo gestiona Windows."),
        new(Rapida.Tema, "Tema claro", "Oscuro", "Claro", 0xE793,
            "Cambia entre el tema claro y el oscuro de NubeBar."),
        new(Rapida.Wallpaper, "Wallpaper Engine", "Sin integrar", "Integrado", 0xE91B,
            "NubeBar toma los colores del fondo animado y le manda el acento."),
    };

    public static string Estado(Accion a, bool encendida) => encendida ? a.Encendida : a.Apagada;
}
