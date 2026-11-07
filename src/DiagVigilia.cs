using System;

namespace NubeBar;

// `NubeBar.exe --vigilia [minutos]`: la barra funcionando de verdad, midiéndose.
//
// ---------- Para qué ----------
//
// Lo que D6 y D7 prometen no se puede contestar con una foto ni con una prueba pura: que con
// la CPU alta y muchas ventanas **no se reconstruyan los paneles continuamente**, y que un
// proceso que vive encendido todo el día no se vaya hinchando. Eso solo lo dice el tiempo.
//
// Hasta ahora esa medida se hacía a mano: arrancar un binario de fase como barra normal y
// mirarlo un rato. Eso tiene un problema serio y ya mordió una vez — una barra arrancada **sin
// banderas puede escribir la configuración**, y una versión nueva se la migra sin preguntar.
//
// Por eso esto es una bandera. `Config.PuedeEscribir` es falso en cuanto hay una bandera
// conocida, así que la vigilia no puede tocar el fichero ni aunque el código quisiera: la
// garantía es estructural, no un cuidado que haya que recordar.
//
// ---------- Qué mide ----------
//
// Memoria (privada y gestionada), CPU acumulada del proceso, recolecciones de basura por
// generación, manejadores abiertos, y lo que es propio de NubeBar: cuántas veces se ha
// repintado de verdad. Una barra sana sube al principio —se llenan las cachés— y después se
// queda plana. Lo que hay que cazar es la pendiente que no para.
public static class DiagVigilia
{
    internal const string Bandera = "--vigilia";

    public static bool Encendido;

    // Cuánto dura y cada cuánto se apunta una línea. Quince minutos es lo que se midió en D8
    // y conviene que siga siendo comparable; se puede pedir otro número.
    public static double Minutos = 15;
    public static readonly TimeSpan Cada = TimeSpan.FromSeconds(30);

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;

        // Un número raro no se interpreta a medias: se dice y se usa el de siempre. Un
        // diagnóstico que mide quince minutos cuando le pediste cinco es peor que uno que
        // se niega.
        if (args.Length > 1)
        {
            if (double.TryParse(args[1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var m)
                && m > 0 && m <= 240)
                Minutos = m;
            else
                Console.WriteLine($"[VIG] «{args[1]}» no son unos minutos válidos (0 a 240): " +
                                  $"se miden {Minutos}.");
        }
    }

    // ---------- Una muestra ----------
    public readonly record struct Muestra(
        TimeSpan Cuando, long MemoriaPrivada, long MemoriaGestionada, TimeSpan Cpu,
        int Gen0, int Gen1, int Gen2, int Manejadores, long Repintados)
    {
        public override string ToString() =>
            $"{Cuando.TotalMinutes,5:0.0} min  " +
            $"privada={MemoriaPrivada / 1048576.0,7:0.0} MB  " +
            $"gestionada={MemoriaGestionada / 1048576.0,6:0.0} MB  " +
            $"cpu={Cpu.TotalSeconds,6:0.0} s  " +
            $"gc={Gen0}/{Gen1}/{Gen2}  " +
            $"manejadores={Manejadores,5}  " +
            $"repintados={Repintados}";
    }

    // ---------- El veredicto ----------
    //
    // No se mira el valor final sino la PENDIENTE, y no desde el arranque sino desde el
    // primer tercio: los primeros minutos suben siempre porque se llenan las cachés de
    // iconos y de miniaturas, y contar esa subida como fuga sería acusar a algo que está
    // haciendo bien su trabajo.
    public const double MargenPorMinuto = 0.5;   // MB/min de los dos tercios finales

    // Y hace falta TIEMPO, no solo muestras. Con minuto y medio salen cuatro muestras y una
    // pendiente de +3 MB/min que no es una fuga: es que las cachés de iconos y miniaturas
    // todavía se están llenando. Dar un veredicto ahí sería inventarse una conclusión con
    // datos que no la sostienen, así que por debajo de este tiempo no se da ninguno.
    public static readonly TimeSpan MinimoParaJuzgar = TimeSpan.FromMinutes(5);

    public static string Veredicto(System.Collections.Generic.IReadOnlyList<Muestra> m)
    {
        if (m.Count < 4) return "pocas muestras para decir nada";
        if (m[^1].Cuando < MinimoParaJuzgar)
            return $"la vigilia ha durado {m[^1].Cuando.TotalMinutes:0.#} min y hacen falta " +
                   $"{MinimoParaJuzgar.TotalMinutes:0} para juzgar la memoria: los primeros " +
                   "minutos suben siempre porque se llenan las cachés. Las muestras valen; " +
                   "la pendiente, no.";

        int desde = m.Count / 3;
        var a = m[desde];
        var b = m[^1];
        double minutos = (b.Cuando - a.Cuando).TotalMinutes;
        if (minutos <= 0) return "las muestras no avanzan en el tiempo";

        double pendiente = (b.MemoriaPrivada - a.MemoriaPrivada) / 1048576.0 / minutos;
        double cpu = (b.Cpu - a.Cpu).TotalSeconds / (minutos * 60) * 100;

        // ---------- La dirección, dicha bien ----------
        //
        // La primera versión comparaba el valor absoluto y escribía «SUBE» para cualquier
        // pendiente que pasara del margen. En la primera medida larga salió
        // «-4,55 MB/min (SUBE)»: un número negativo etiquetado como subida. Una fuga es una
        // pendiente POSITIVA; que baje no es un problema, es el recolector haciendo su
        // trabajo o Windows recortando el conjunto de trabajo.
        string direccion = pendiente > MargenPorMinuto ? "SUBE"
                         : pendiente < -MargenPorMinuto ? "baja (recolección o recorte del "
                                                          + "conjunto de trabajo, no es un problema)"
                         : "plana";

        // ---------- «Sube» y «acaba por debajo de su pico» no son lo mismo ----------
        //
        // Una fuga sube y SE QUEDA arriba. Lo que sube, hace un pico y vuelve a bajar es
        // trabajo: abrir el menú Inicio construye un panel grande de iconos y eso asigna
        // memoria, que el recolector devuelve después.
        //
        // La primera versión decía «SUBE» en los dos casos. Pasó de verdad: una medida salió
        // «+7,72 MB/min SUBE» con el recorrido 200→333 acabando en 280, y lo que había en el
        // registro de esos minutos era el usuario abriendo el menú Inicio.
        bool subeYSeQueda = pendiente > MargenPorMinuto;

        // ---------- Y el recorrido, porque una pendiente sola engaña ----------
        //
        // En esa misma medida la memoria estuvo en 250 MB diez minutos y cayó a 207 de golpe
        // en el minuto 13. La pendiente de la ventana entera da -4,55 MB/min, que suena a
        // tendencia y no lo es: es un escalón. Con el mínimo, el máximo y el último valor
        // delante, un escalón se distingue de una tendencia de un vistazo.
        double menor = double.MaxValue, mayor = 0;
        int manMenor = int.MaxValue, manMayor = 0;
        for (int i = desde; i < m.Count; i++)
        {
            double mb = m[i].MemoriaPrivada / 1048576.0;
            if (mb < menor) menor = mb;
            if (mb > mayor) mayor = mb;
            if (m[i].Manejadores < manMenor) manMenor = m[i].Manejadores;
            if (m[i].Manejadores > manMayor) manMayor = m[i].Manejadores;
        }

        // Si acaba bastante por debajo de su máximo, lo que hubo fue un pico, no una
        // tendencia: se dice así en vez de dejar al lector con la pendiente a secas.
        double final = b.MemoriaPrivada / 1048576.0;
        bool volvio = subeYSeQueda && final < mayor - Math.Max(10, (mayor - menor) * 0.3);
        if (volvio) direccion = "subió y volvió a bajar: fue un pico de trabajo, no una fuga";

        return $"desde el minuto {a.Cuando.TotalMinutes:0.0}: " +
               $"memoria {pendiente:+0.00;-0.00;0.00} MB/min ({direccion}); " +
               $"recorrido {menor:0} a {mayor:0} MB, acaba en {b.MemoriaPrivada / 1048576.0:0}; " +
               $"cpu media {cpu:0.0} % de un núcleo; " +
               $"manejadores entre {manMenor} y {manMayor}";
    }
}
