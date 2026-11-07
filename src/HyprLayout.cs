using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

public static partial class NubeHypr
{
    // ---------- Los cuatro layouts ----------
    public enum Layout
    {
        Flotante,       // nadie se mueve: el usuario coloca a mano
        Horizontal,     // columnas, de izquierda a derecha
        Vertical,       // filas, de arriba abajo
        Principal,      // una grande a un lado y el resto apiladas al otro
    }

    public enum Borde { Arriba, Abajo, Izquierda, Derecha }

    // ================= El área útil =================
    //
    // ---------- La cuenta que decide dónde se puede poner algo ----------
    //
    // Del rectángulo del monitor se descuenta la franja que ocupa la barra —solo por SU
    // borde— y después el margen, por los cuatro lados. En ese orden: el margen se mide
    // contra el hueco que queda, no contra la pantalla, porque si no, el lado de la barra
    // acaba con el doble de aire que los otros tres y se nota.
    //
    // `reserva` es el grosor que la barra se queda. Cuando NubeBar flota sobre las
    // ventanas y no reserva espacio, aquí se pasa 0: el área es la pantalla entera, y las
    // ventanas pasan por debajo de la isla a propósito.
    public static Rect AreaUtil(Rect monitor, Borde barra, double reserva, double margen)
    {
        reserva = Math.Max(0, reserva);
        margen = Math.Max(0, margen);

        double x = monitor.X, y = monitor.Y, w = monitor.Ancho, h = monitor.Alto;

        switch (barra)
        {
            case Borde.Arriba: y += reserva; h -= reserva; break;
            case Borde.Abajo: h -= reserva; break;
            case Borde.Izquierda: x += reserva; w -= reserva; break;
            case Borde.Derecha: w -= reserva; break;
        }

        x += margen; y += margen;
        w -= margen * 2; h -= margen * 2;

        // Una pantalla pequeña con una barra gruesa y mucho margen puede dar negativo. Un
        // área negativa no es un área: se devuelve vacía y quien la reciba no reparte
        // nada. Devolver un rectángulo "casi válido" aquí acabaría en ventanas de ancho
        // negativo pasadas a Windows.
        if (w <= 0 || h <= 0) return Rect.Nada;
        return new Rect(x, y, w, h);
    }

    // ================= E5: el área cuando hay más superficies =================
    //
    // ---------- El problema que aparece con E ----------
    //
    // Hasta E, la única superficie de NubeBar que estorbaba al reparto era la barra. Con el
    // dock y los paneles laterales hay más, y repartir sobre la pantalla entera dejaría una
    // ventana **permanentemente debajo del dock**: no tapada a ratos, tapada siempre, porque
    // el dock es permanente.
    //
    // ---------- Qué se descuenta y qué NO ----------
    //
    // Se descuenta lo **permanente**: la barra y el dock.
    //
    // NO se descuentan los paneles laterales, aunque tapen más. Y no es un descuido: si se
    // descontaran, abrir el panel izquierdo recolocaría todas las ventanas del escritorio y
    // cerrarlo las recolocaría otra vez. Mirar el volumen te movería la pantalla entera. Un
    // panel que se abre y se cierra pasa por delante; no cambia la forma del escritorio.
    //
    // Es la misma distinción que hace el tablero de E0 entre «reserva espacio» y «flota».
    public readonly record struct Estorbo(Borde Donde, double Grosor)
    {
        public bool Cuenta => Grosor > 0;
    }

    // Se descuenta, por cada borde, **el más grueso** de los estorbos que haya ahí. Sumarlos
    // sería descontar dos veces cuando dos superficies comparten borde, y el área saldría más
    // pequeña de lo que es.
    public static Rect AreaUtil(Rect monitor, double margen, params Estorbo[] estorbos)
    {
        margen = Math.Max(0, margen);

        double arriba = 0, abajo = 0, izq = 0, der = 0;
        foreach (var e in estorbos ?? Array.Empty<Estorbo>())
        {
            if (!e.Cuenta) continue;
            switch (e.Donde)
            {
                case Borde.Arriba: arriba = Math.Max(arriba, e.Grosor); break;
                case Borde.Abajo: abajo = Math.Max(abajo, e.Grosor); break;
                case Borde.Izquierda: izq = Math.Max(izq, e.Grosor); break;
                case Borde.Derecha: der = Math.Max(der, e.Grosor); break;
            }
        }

        double x = monitor.X + izq;
        double y = monitor.Y + arriba;
        double w = monitor.Ancho - izq - der;
        double h = monitor.Alto - arriba - abajo;

        // El margen, contra el hueco que queda y no contra la pantalla: por el mismo motivo
        // que en la versión de un solo estorbo, o el lado de la barra acaba con el doble de
        // aire que los otros tres.
        x += margen; y += margen;
        w -= margen * 2; h -= margen * 2;

        if (w <= 0 || h <= 0) return Rect.Nada;
        return new Rect(x, y, w, h);
    }

    // ================= El reparto, sin costuras ni desbordes =================
    //
    // ---------- Por qué no vale dividir y multiplicar ----------
    //
    // El reparto obvio —`ancho / n` y colocar cada trozo en `i * trozo`— falla de dos
    // maneras a la vez en cuanto hay redondeo: deja rayas de un píxel entre ventanas, y la
    // última se pasa del área. Con tres ventanas en 1000 px y hueco 8, el error se acumula
    // y la tercera acaba en 1001.
    //
    // La forma correcta es repartir los BORDES y no los tamaños: se calcula dónde empieza
    // cada trozo con la cuenta exacta, se redondea cada borde una sola vez, y el tamaño de
    // cada trozo es la diferencia entre su borde y el siguiente. Así los bordes coinciden
    // por construcción —no hay costura posible— y el último borde es exactamente el final
    // del área, así que no se desborda nunca.
    //
    // Devuelve los tramos [inicio, tamaño] a lo largo de un eje.
    internal static List<(double Inicio, double Tamano)> Tramos(
        double inicio, double largo, int cuantos, double hueco)
    {
        var tramos = new List<(double, double)>();
        if (cuantos <= 0 || largo <= 0) return tramos;
        hueco = Math.Max(0, hueco);

        // Los huecos van ENTRE tramos: con n tramos hay n-1 huecos. Si no caben, se
        // quedan sin hueco antes que dar tamaños negativos.
        double total = hueco * (cuantos - 1);
        if (total >= largo) { hueco = 0; total = 0; }

        double util = largo - total;
        for (int i = 0; i < cuantos; i++)
        {
            double a = Math.Round(inicio + util * i / cuantos + hueco * i);
            double b = Math.Round(inicio + util * (i + 1) / cuantos + hueco * i);
            tramos.Add((a, Math.Max(0, b - a)));
        }
        return tramos;
    }

    // Lo más estrecho que se deja una ventana. Por debajo de esto no es una ventana: es
    // una raya, y la barra de título ya no se puede ni agarrar.
    public const double MinimoLado = 120;

    // Cuántas ventanas caben a lo largo de un eje sin bajar del mínimo. Hace falta porque
    // la alternativa —repartir entre todas y que salgan de 20 px— es peor que no
    // organizar: la especificación lo dice, "preferir no hacer nada".
    public static int CuantasCaben(double largo, double hueco, double minimo)
    {
        if (largo <= 0) return 0;
        minimo = Math.Max(1, minimo);
        hueco = Math.Max(0, hueco);
        // n * minimo + (n-1) * hueco <= largo
        int n = (int)Math.Floor((largo + hueco) / (minimo + hueco));
        return Math.Max(0, n);
    }

    // ================= Calcular un layout =================
    //
    // Recibe el estado, el área y los huecos, y devuelve los efectos. No muta nada.
    //
    // Las que no entran en el reparto salen con `SeQueda` y su motivo: una ventana
    // minimizada, una maximizada, una flotante o una que no cabe. Que salgan en la lista
    // —en vez de desaparecer de ella— es lo que permite que el log diga por qué no se
    // movió algo, que es la pregunta que se hace siempre.
    public static List<EfectoVentana> Calcular(
        EstadoNubeHypr estado, Rect area, double hueco, double minimo = MinimoLado)
    {
        var efectos = new List<EfectoVentana>();
        var delEspacio = estado.DelEspacio(estado.EspacioActual);

        // Lo que no se enlosa, se queda, con su motivo
        foreach (var v in delEspacio.Where(v => !v.Enlosable))
            efectos.Add(new EfectoVentana(TipoEfecto.SeQueda, v.Hwnd, Porque: MotivoDeNoEnlosar(v)));

        var enlosables = delEspacio.Where(v => v.Enlosable).ToList();
        if (enlosables.Count == 0) return efectos;

        // Flotante: el layout no coloca a nadie. No es "no hacer nada por pereza", es el
        // layout en el que el usuario manda.
        if (estado.Layout == Layout.Flotante)
        {
            foreach (var v in enlosables)
                efectos.Add(new EfectoVentana(TipoEfecto.SeQueda, v.Hwnd, Porque: "el layout es flotante"));
            return efectos;
        }

        if (!area.Valido)
        {
            foreach (var v in enlosables)
                efectos.Add(new EfectoVentana(TipoEfecto.SeQueda, v.Hwnd,
                                              Porque: "no hay área útil en este monitor"));
            return efectos;
        }

        var sitios = Repartir(estado.Layout, area, enlosables.Count, hueco, estado.Proporcion, minimo);

        for (int i = 0; i < enlosables.Count; i++)
        {
            if (i >= sitios.Count)
            {
                efectos.Add(new EfectoVentana(TipoEfecto.SeQueda, enlosables[i].Hwnd,
                                              Porque: $"no caben más de {sitios.Count} sin bajar de {minimo:0} px"));
                continue;
            }
            efectos.Add(new EfectoVentana(TipoEfecto.Colocar, enlosables[i].Hwnd, sitios[i]));
        }
        return efectos;
    }

    static string MotivoDeNoEnlosar(VentanaModelo v) =>
        !v.Visible ? "no está visible"
        : v.Minimizada ? "está minimizada"
        : v.Maximizada ? "está maximizada"
        : "es flotante";

    // ---------- Los rectángulos de cada layout ----------
    //
    // Puro del todo: ni estado ni ventanas, solo un área y un número. Es el trozo que se
    // puede comprobar con una tabla de casos, y el que de verdad decide si una ventana
    // acaba fuera de la pantalla.
    public static List<Rect> Repartir(Layout layout, Rect area, int cuantas,
                                      double hueco, double proporcion, double minimo = MinimoLado)
    {
        var sitios = new List<Rect>();
        if (cuantas <= 0 || !area.Valido || layout == Layout.Flotante) return sitios;

        // Una sola ventana ocupa el área entera, en los tres layouts que colocan. Es lo
        // que se espera y evita el caso raro de "la principal al 60 % y el otro 40 %
        // vacío".
        if (cuantas == 1) { sitios.Add(area); return sitios; }

        switch (layout)
        {
            case Layout.Horizontal:
            {
                int n = Math.Min(cuantas, CuantasCaben(area.Ancho, hueco, minimo));
                foreach (var (ini, tam) in Tramos(area.X, area.Ancho, n, hueco))
                    sitios.Add(new Rect(ini, area.Y, tam, area.Alto));
                break;
            }
            case Layout.Vertical:
            {
                int n = Math.Min(cuantas, CuantasCaben(area.Alto, hueco, minimo));
                foreach (var (ini, tam) in Tramos(area.Y, area.Alto, n, hueco))
                    sitios.Add(new Rect(area.X, ini, area.Ancho, tam));
                break;
            }
            case Layout.Principal:
            {
                // La principal a la izquierda con su proporción, el resto apiladas a la
                // derecha. La proporción se acota: al 5 % la principal sería una raya, y
                // al 95 % la columna de al lado no podría con una sola ventana.
                double p = Math.Clamp(proporcion, 0.2, 0.8);

                // Si ni partiendo en dos caben dos columnas del mínimo, esto no es un
                // "principal": se cae a vertical, que al menos reparte algo utilizable.
                if (CuantasCaben(area.Ancho, hueco, minimo) < 2)
                    return Repartir(Layout.Vertical, area, cuantas, hueco, proporcion, minimo);

                double anchoPrincipal = Math.Round(Math.Max(minimo, (area.Ancho - hueco) * p));
                double anchoResto = area.Ancho - hueco - anchoPrincipal;
                if (anchoResto < minimo)
                {
                    anchoResto = minimo;
                    anchoPrincipal = area.Ancho - hueco - anchoResto;
                }

                sitios.Add(new Rect(area.X, area.Y, anchoPrincipal, area.Alto));

                int restantes = cuantas - 1;
                int n = Math.Min(restantes, CuantasCaben(area.Alto, hueco, minimo));
                double xResto = area.X + anchoPrincipal + hueco;
                foreach (var (ini, tam) in Tramos(area.Y, area.Alto, n, hueco))
                    sitios.Add(new Rect(xResto, ini, anchoResto, tam));
                break;
            }
        }
        return sitios;
    }

    // ================= Los comandos =================
    //
    // `(estado, comando) -> (estado nuevo, efectos)`. Un comando que no se puede cumplir
    // devuelve **el mismo estado** y ningún efecto: ni lanza, ni deja el estado a medias,
    // ni se inventa una ventana. Es la propiedad de §14.2 y la que evita que un atajo mal
    // configurado desordene el escritorio.
    public sealed record Resultado(EstadoNubeHypr Estado, IReadOnlyList<EfectoVentana> Efectos, string Porque)
    {
        public bool Hizo => Efectos.Count > 0;
        public static Resultado Nada(EstadoNubeHypr e, string porque) =>
            new(e, Array.Empty<EfectoVentana>(), porque);
    }

    public static Resultado Ejecutar(EstadoNubeHypr e, Comando c)
    {
        switch (c)
        {
            case Comando.EnfocarSiguiente: return Rotar(e, +1);
            case Comando.EnfocarAnterior: return Rotar(e, -1);
            case Comando.EnfocarHacia h: return HaciaDonde(e, h.Hacia);
            case Comando.MoverEnElOrden m: return MoverOrden(e, m.Adelante);
            case Comando.Redimensionar r: return Proporcionar(e, r.Delta);
            case Comando.AlternarFlotante: return Flotar(e);
            case Comando.CambiarEspacio ce: return AlEspacio(e, ce.Espacio, ce.LlevarLaVentana);
            case Comando.PonerLayout pl: return OtroLayout(e, pl.Cual);
            default: return Resultado.Nada(e, "comando desconocido");
        }
    }

    // ---------- Enfocar ----------
    //
    // El foco rota entre las enlosables del espacio actual, EN SU ORDEN, y da la vuelta.
    // Rotar es lo que se espera de "siguiente" y no deriva: dar n vueltas con n ventanas
    // te deja donde empezaste.
    static Resultado Rotar(EstadoNubeHypr e, int paso)
    {
        var lista = e.DelEspacio(e.EspacioActual).Where(v => v.Visible && !v.Minimizada).ToList();
        if (lista.Count == 0) return Resultado.Nada(e, "no hay ventanas que enfocar");

        int i = lista.FindIndex(v => v.Hwnd == e.Foco);
        // Sin foco conocido se empieza por la primera (o la última si vas hacia atrás),
        // en vez de no hacer nada: es lo que espera quien acaba de pulsar el atajo.
        int siguiente = i < 0 ? (paso > 0 ? 0 : lista.Count - 1)
                              : ((i + paso) % lista.Count + lista.Count) % lista.Count;
        var elegida = lista[siguiente];
        if (elegida.Hwnd == e.Foco) return Resultado.Nada(e, "ya estaba enfocada");

        return new Resultado(e with { Foco = elegida.Hwnd },
                             new[] { new EfectoVentana(TipoEfecto.Enfocar, elegida.Hwnd) },
                             "foco rotado");
    }

    // Foco por dirección: la más cercana cuyo centro está hacia ese lado. Se mide del
    // centro al centro y se exige que esté DE VERDAD en esa dirección; si no, "arriba"
    // acabaría enfocando algo que está a la derecha porque quedaba más cerca.
    static Resultado HaciaDonde(EstadoNubeHypr e, Direccion d)
    {
        var yo = e.De(e.Foco);
        if (yo == null) return Rotar(e, +1);        // sin foco, el primero

        var candidatas = e.DelEspacio(e.EspacioActual)
            .Where(v => v.Hwnd != yo.Hwnd && v.Visible && !v.Minimizada)
            .Where(v => EstaHacia(yo.Rectangulo, v.Rectangulo, d))
            .OrderBy(v => Distancia(yo.Rectangulo, v.Rectangulo))
            .ToList();

        if (candidatas.Count == 0) return Resultado.Nada(e, $"no hay ninguna ventana hacia {d}");
        var elegida = candidatas[0];
        return new Resultado(e with { Foco = elegida.Hwnd },
                             new[] { new EfectoVentana(TipoEfecto.Enfocar, elegida.Hwnd) },
                             $"foco hacia {d}");
    }

    static bool EstaHacia(Rect yo, Rect otra, Direccion d)
    {
        double cx = yo.X + yo.Ancho / 2, cy = yo.Y + yo.Alto / 2;
        double ox = otra.X + otra.Ancho / 2, oy = otra.Y + otra.Alto / 2;
        return d switch
        {
            Direccion.Izquierda => ox < cx,
            Direccion.Derecha => ox > cx,
            Direccion.Arriba => oy < cy,
            _ => oy > cy,
        };
    }

    static double Distancia(Rect a, Rect b)
    {
        double dx = (a.X + a.Ancho / 2) - (b.X + b.Ancho / 2);
        double dy = (a.Y + a.Alto / 2) - (b.Y + b.Alto / 2);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ---------- Mover en el orden ----------
    //
    // Intercambia la ventana con la foco con su vecina en el orden del reparto. En los
    // extremos NO da la vuelta: se queda quieta. Dar la vuelta aquí haría que mantener
    // pulsado el atajo mandara la ventana de un extremo al otro sin parar, que es la
    // "deriva" que la especificación pide evitar.
    static Resultado MoverOrden(EstadoNubeHypr e, bool adelante)
    {
        var lista = e.Ventanas.ToList();
        int i = lista.FindIndex(v => v.Hwnd == e.Foco && v.Enlosable);
        if (i < 0) return Resultado.Nada(e, "la ventana con el foco no entra en el reparto");

        // El vecino entre las enlosables del MISMO espacio, no el de la lista completa
        var mismos = lista.Where(v => v.Escritorio == e.EspacioActual && v.Enlosable).ToList();
        int j = mismos.FindIndex(v => v.Hwnd == e.Foco);
        int k = j + (adelante ? 1 : -1);
        if (j < 0 || k < 0 || k >= mismos.Count)
            return Resultado.Nada(e, adelante ? "ya es la última" : "ya es la primera");

        int ia = lista.FindIndex(v => v.Hwnd == mismos[j].Hwnd);
        int ib = lista.FindIndex(v => v.Hwnd == mismos[k].Hwnd);
        (lista[ia], lista[ib]) = (lista[ib], lista[ia]);

        // El efecto no es "colocar": el reparto lo recalcula quien llame, con el estado
        // nuevo. Devolver aquí rectángulos obligaría a conocer el área, y el área no es
        // asunto de un comando.
        return new Resultado(e with { Ventanas = lista },
                             new[] { new EfectoVentana(TipoEfecto.Enfocar, e.Foco) },
                             adelante ? "movida hacia adelante" : "movida hacia atrás");
    }

    // ---------- Redimensionar ----------
    //
    // En un layout enlosado no se redimensiona una ventana: se mueve la frontera. Cambia
    // la proporción de la principal, acotada, y el reparto hace el resto.
    static Resultado Proporcionar(EstadoNubeHypr e, double delta)
    {
        if (e.Layout is Layout.Flotante)
            return Resultado.Nada(e, "en flotante no hay frontera que mover");

        double nueva = Math.Clamp(e.Proporcion + delta, 0.2, 0.8);
        if (Math.Abs(nueva - e.Proporcion) < 0.0001)
            return Resultado.Nada(e, "la proporción ya está en el tope");

        // Sin efectos: el que llama recalcula el reparto con la proporción nueva. Un
        // comando que devolviera rectángulos tendría que conocer el monitor.
        return new Resultado(e with { Proporcion = nueva }, Array.Empty<EfectoVentana>(),
                             $"proporción {e.Proporcion:0.00} -> {nueva:0.00}");
    }

    // ---------- Alternar flotante ----------
    static Resultado Flotar(EstadoNubeHypr e)
    {
        var v = e.De(e.Foco);
        if (v == null) return Resultado.Nada(e, "no hay ventana con el foco");

        var cambiada = v with { Flotante = !v.Flotante };
        var lista = e.Ventanas.Select(x => x.Hwnd == v.Hwnd ? cambiada : x).ToList();
        return new Resultado(e with { Ventanas = lista },
                             new[] { new EfectoVentana(TipoEfecto.SeQueda, v.Hwnd,
                                     Porque: cambiada.Flotante ? "ahora es flotante" : "vuelve al reparto") },
                             "flotante alternado");
    }

    // ---------- Cambiar de espacio ----------
    static Resultado AlEspacio(EstadoNubeHypr e, int espacio, bool llevarVentana)
    {
        if (espacio < 0) return Resultado.Nada(e, "un espacio no puede ser negativo");
        if (e.Espacios.Count > 0 && espacio >= e.Espacios.Count)
            return Resultado.Nada(e, $"no hay espacio {espacio}: solo {e.Espacios.Count}");

        if (!llevarVentana)
        {
            if (espacio == e.EspacioActual) return Resultado.Nada(e, "ya estabas en ese espacio");
            return new Resultado(e with { EspacioActual = espacio }, Array.Empty<EfectoVentana>(),
                                 $"al espacio {espacio}");
        }

        var v = e.De(e.Foco);
        if (v == null) return Resultado.Nada(e, "no hay ventana con el foco que llevar");
        if (v.Escritorio == espacio) return Resultado.Nada(e, "la ventana ya estaba ahí");

        var lista = e.Ventanas.Select(x => x.Hwnd == v.Hwnd ? x with { Escritorio = espacio } : x).ToList();
        return new Resultado(e with { Ventanas = lista, EspacioActual = espacio },
                             new[] { new EfectoVentana(TipoEfecto.AEspacio, v.Hwnd, Espacio: espacio) },
                             $"ventana al espacio {espacio}");
    }

    static Resultado OtroLayout(EstadoNubeHypr e, Layout cual)
    {
        if (cual == e.Layout) return Resultado.Nada(e, "ya era ese layout");
        return new Resultado(e with { Layout = cual }, Array.Empty<EfectoVentana>(), $"layout {cual}");
    }


    // ================= D3: mover y redimensionar con pasos =================
    //
    // ---------- Para quién es esto ----------
    //
    // Para las ventanas FLOTANTES. En un layout enlosado no se mueve una ventana: se mueve
    // su sitio en el orden, o la frontera. Pero una flotante la coloca el usuario, y con el
    // teclado hace falta un paso: cuántos píxeles por pulsación.
    //
    // Las dos funciones son puras y acotan al área. Una ventana que se sale de la pantalla
    // con el teclado es imposible de recuperar sin el ratón, y es el tipo de fallo que
    // obliga a reiniciar la sesión.
    public static Rect PasoDeMover(Rect ventana, Rect area, Direccion hacia, double paso)
    {
        if (!ventana.Valido || !area.Valido) return ventana;
        paso = Math.Abs(paso);

        double x = ventana.X, y = ventana.Y;
        switch (hacia)
        {
            case Direccion.Izquierda: x -= paso; break;
            case Direccion.Derecha: x += paso; break;
            case Direccion.Arriba: y -= paso; break;
            default: y += paso; break;
        }

        // Acotada: nunca se sale. Si la ventana es más grande que el área —pasa con una
        // ventana enorme en un monitor pequeño— se pega al origen en vez de dar un
        // máximo menor que el mínimo, que daría un salto absurdo.
        double maxX = Math.Max(area.X, area.Derecha - ventana.Ancho);
        double maxY = Math.Max(area.Y, area.Abajo - ventana.Alto);
        return new Rect(Math.Clamp(x, area.X, maxX), Math.Clamp(y, area.Y, maxY),
                        ventana.Ancho, ventana.Alto);
    }

    public static Rect PasoDeTamano(Rect ventana, Rect area, Direccion hacia, double paso,
                                    double minimo = MinimoLado)
    {
        if (!ventana.Valido || !area.Valido) return ventana;
        minimo = Math.Max(1, minimo);

        double w = ventana.Ancho, h = ventana.Alto;
        if (hacia is Direccion.Izquierda or Direccion.Derecha)
            w += hacia == Direccion.Derecha ? paso : -paso;
        else
            h += hacia == Direccion.Abajo ? paso : -paso;

        // Entre el mínimo y lo que queda de área desde donde empieza la ventana: crecer
        // hacia la derecha no puede empujar el borde fuera de la pantalla.
        double maxW = Math.Max(minimo, area.Derecha - ventana.X);
        double maxH = Math.Max(minimo, area.Abajo - ventana.Y);
        return new Rect(ventana.X, ventana.Y,
                        Math.Clamp(w, minimo, maxW), Math.Clamp(h, minimo, maxH));
    }

    // El paso de serie. 32 px es un salto que se ve y que no se pasa de largo: con 8 hay
    // que machacar la tecla y con 100 no se puede colocar nada con precisión.
    public const double PasoEnPixeles = 32;
    // ================= Insertar y quitar =================
    //
    // ---------- Por qué va aquí y no en el adaptador ----------
    //
    // "Insertar, quitar, ordenar y recolocar sin saltos" es una política, no una llamada
    // a Windows. Y la regla que lo hace funcionar es aburrida: **insertar añade al final
    // y quitar no recoloca a nadie**. Cualquier otra —ordenar por título, por proceso, por
    // cuándo se usó— hace que abrir el navegador te reorganice la pantalla entera.
    public static EstadoNubeHypr Insertar(EstadoNubeHypr e, VentanaModelo v)
    {
        // La misma ventana dos veces no se duplica: se actualiza. Es el caso de un título
        // que cambia o una ventana que se mueve, y duplicarla dejaría dos huecos para una.
        int i = e.Ventanas.ToList().FindIndex(x => x.Hwnd == v.Hwnd);
        if (i >= 0)
        {
            var lista = e.Ventanas.ToList();
            lista[i] = v;
            return e with { Ventanas = lista };
        }
        return e with { Ventanas = e.Ventanas.Append(v).ToList() };
    }

    public static EstadoNubeHypr Quitar(EstadoNubeHypr e, IntPtr hwnd)
    {
        if (e.Ventanas.All(v => v.Hwnd != hwnd)) return e;      // no estaba: nada cambia
        var lista = e.Ventanas.Where(v => v.Hwnd != hwnd).ToList();
        // Si se va la que tenía el foco, el foco se suelta. Dejarlo apuntando a una
        // ventana que ya no existe es como se cuelan los "enfocar 0x0" en el log.
        var foco = e.Foco == hwnd ? IntPtr.Zero : e.Foco;
        return e with { Ventanas = lista, Foco = foco };
    }
}
