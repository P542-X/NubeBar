using System;
using System.Collections.Generic;

namespace NubeBar;

// Dónde puede ir una ventana cuando la ajustas a un borde.
public enum Zona
{
    Ninguna,
    Izquierda, Derecha,
    Maximizar,
    ArribaIzquierda, ArribaDerecha,
    AbajoIzquierda, AbajoDerecha,
}

// Un rectángulo en píxeles de pantalla. No se usa `Rect` de WPF a propósito: eso son
// unidades de WPF y aquí todo es crudo, que es lo único que vale con dos monitores a
// distinto DPI.
public readonly record struct Hueco(int X, int Y, int Ancho, int Alto)
{
    public int Derecha => X + Ancho;
    public int Abajo => Y + Alto;
    public bool Vacio => Ancho <= 0 || Alto <= 0;

    // Lo que queda al quitarle una franja por un borde. Es la cuenta que hace falta
    // para que una ventana ajustada no se meta debajo de NubeBar.
    public Hueco Menos(string borde, int grosor)
    {
        if (grosor <= 0) return this;
        grosor = Math.Min(grosor, (borde is "arriba" or "abajo" ? Alto : Ancho) / 2);
        return borde switch
        {
            "arriba" => new Hueco(X, Y + grosor, Ancho, Alto - grosor),
            "abajo" => new Hueco(X, Y, Ancho, Alto - grosor),
            "izquierda" => new Hueco(X + grosor, Y, Ancho - grosor, Alto),
            "derecha" => new Hueco(X, Y, Ancho - grosor, Alto),
            _ => this,
        };
    }
}

// El ajuste a los bordes, en cuentas puras.
//
// ---------- Qué es "el área útil" ----------
//
// La del monitor menos lo que los demás ya han reservado, que es lo que Windows llama
// área de trabajo. **Si NubeBar está registrada como AppBar, su franja ya está
// descontada ahí**: no hay que restarla otra vez ni saber dónde está. Y si NubeBar está
// flotando —que es lo de serie desde B6— la franja NO se descuenta, y eso también es lo
// correcto: flotar significa precisamente ponerse encima de las ventanas.
//
// Quien quiera lo contrario tiene "Reservar espacio" en Ajustes, que es el interruptor
// que ya existe y que significa justo esto. Inventar aquí una tercera regla habría
// hecho que la misma opción quisiera decir dos cosas distintas según quién preguntara.
//
// `Hueco.Menos` está para cuando haga falta restar algo a mano; hoy no hace falta.
public static class ZonasAjuste
{
    // Cuánto hay que acercarse a un borde para que cuente. Doce píxeles es lo que usa
    // Windows y es lo que distingue "he soltado la ventana cerca del borde" de "he
    // soltado la ventana".
    public const int Margen = 12;

    // Y cuánto de la longitud del borde ocupa una esquina. Un cuarto deja la mitad
    // central para la media pantalla de toda la vida.
    const double Esquina = 0.25;

    public static Hueco AreaUtil(Monitores.Info m) =>
        new(m.TrabajoX, m.TrabajoY, m.TrabajoAncho, m.TrabajoAlto);

    // ---------- E5: con las superficies de NubeBar descontadas ----------
    //
    // El área de trabajo de Windows conoce la barra —se registra como appbar cuando reserva
    // espacio— pero **no conoce el dock**, que flota por encima. Ajustar una ventana a la
    // mitad inferior la dejaba con su borde de abajo debajo del dock: no tapada a ratos,
    // tapada siempre.
    //
    // Se parte del área de TRABAJO, no del monitor, y solo se descuenta lo que Windows no
    // sabe. Restar aquí otra vez la barra dejaría una franja muerta de su grosor.
    //
    // Por borde se coge el estorbo más grueso, no la suma: sumarlos descontaría dos veces
    // cuando dos superficies comparten borde.
    public static Hueco AreaUtilCon(Monitores.Info m, IEnumerable<NubeHypr.Estorbo> estorbos)
    {
        double arriba = 0, abajo = 0, izq = 0, der = 0;
        foreach (var e in estorbos ?? Array.Empty<NubeHypr.Estorbo>())
        {
            if (!e.Cuenta) continue;
            switch (e.Donde)
            {
                case NubeHypr.Borde.Arriba: arriba = Math.Max(arriba, e.Grosor); break;
                case NubeHypr.Borde.Abajo: abajo = Math.Max(abajo, e.Grosor); break;
                case NubeHypr.Borde.Izquierda: izq = Math.Max(izq, e.Grosor); break;
                case NubeHypr.Borde.Derecha: der = Math.Max(der, e.Grosor); break;
            }
        }

        int w = m.TrabajoAncho, h = m.TrabajoAlto;
        int qArriba = (int)Math.Round(arriba), qAbajo = (int)Math.Round(abajo);
        int qIzq = (int)Math.Round(izq), qDer = (int)Math.Round(der);

        // Cada resta limitada a lo que queda: una pantalla pequeña con un dock gordo no puede
        // dar un área negativa, que acabaría en una ventana de ancho negativo pasada a
        // Windows. Se deja siempre un mínimo habitable.
        const int Minimo = 160;
        qArriba = Math.Clamp(qArriba, 0, Math.Max(0, h - Minimo));
        qAbajo = Math.Clamp(qAbajo, 0, Math.Max(0, h - Minimo - qArriba));
        qIzq = Math.Clamp(qIzq, 0, Math.Max(0, w - Minimo));
        qDer = Math.Clamp(qDer, 0, Math.Max(0, w - Minimo - qIzq));

        return new Hueco(m.TrabajoX + qIzq, m.TrabajoY + qArriba,
                         w - qIzq - qDer, h - qArriba - qAbajo);
    }

    // A dónde va una ventana ajustada a esa zona.
    //
    // Las mitades se calculan como "la mitad" y "lo que queda": con `Ancho / 2` en las
    // dos, un ancho impar deja una raya de un píxel por el medio, y eso se ve.
    public static Hueco Colocacion(Zona z, Hueco area)
    {
        if (area.Vacio) return area;
        int mitadX = area.Ancho / 2, restoX = area.Ancho - mitadX;
        int mitadY = area.Alto / 2, restoY = area.Alto - mitadY;

        return z switch
        {
            Zona.Izquierda => new Hueco(area.X, area.Y, mitadX, area.Alto),
            Zona.Derecha => new Hueco(area.X + mitadX, area.Y, restoX, area.Alto),
            Zona.Maximizar => area,
            Zona.ArribaIzquierda => new Hueco(area.X, area.Y, mitadX, mitadY),
            Zona.ArribaDerecha => new Hueco(area.X + mitadX, area.Y, restoX, mitadY),
            Zona.AbajoIzquierda => new Hueco(area.X, area.Y + mitadY, mitadX, restoY),
            Zona.AbajoDerecha => new Hueco(area.X + mitadX, area.Y + mitadY, restoX, restoY),
            _ => area,
        };
    }

    // Qué zona pide el puntero. Se mira contra el MONITOR entero y no contra el área
    // útil: el borde de la pantalla es donde el usuario empuja, aunque ahí viva una
    // barra. Lo que se respeta después, al colocar, es el área útil.
    //
    // Las esquinas van primero: estar en una esquina es estar en dos bordes a la vez, y
    // sin mirarlas antes siempre ganaría el primer borde que se comprobara.
    public static Zona ZonaDelPunto(int px, int py, Monitores.Info m, int margen = Margen)
    {
        if (m.Ancho <= 0 || m.Alto <= 0) return Zona.Ninguna;
        if (!m.Contiene(px, py)) return Zona.Ninguna;

        bool izquierda = px <= m.X + margen;
        bool derecha = px >= m.Derecha - 1 - margen;
        bool arriba = py <= m.Y + margen;
        bool abajo = py >= m.Abajo - 1 - margen;

        int anchoEsquina = (int)(m.Ancho * Esquina);
        int altoEsquina = (int)(m.Alto * Esquina);
        bool cercaArriba = py <= m.Y + altoEsquina;
        bool cercaAbajo = py >= m.Abajo - altoEsquina;

        // Esquinas: pegado a un lado Y arriba o abajo del todo
        if (izquierda && arriba) return Zona.ArribaIzquierda;
        if (derecha && arriba) return Zona.ArribaDerecha;
        if (izquierda && abajo) return Zona.AbajoIzquierda;
        if (derecha && abajo) return Zona.AbajoDerecha;

        // El borde de arriba, salvo en sus extremos, es maximizar
        if (arriba)
        {
            if (px <= m.X + anchoEsquina) return Zona.ArribaIzquierda;
            if (px >= m.Derecha - anchoEsquina) return Zona.ArribaDerecha;
            return Zona.Maximizar;
        }

        // Y los lados son media pantalla, con sus cuartos arriba y abajo
        if (izquierda) return cercaArriba ? Zona.ArribaIzquierda
                            : cercaAbajo ? Zona.AbajoIzquierda : Zona.Izquierda;
        if (derecha) return cercaArriba ? Zona.ArribaDerecha
                          : cercaAbajo ? Zona.AbajoDerecha : Zona.Derecha;

        return Zona.Ninguna;
    }

    public static string Nombre(Zona z) => z switch
    {
        Zona.Izquierda => "Mitad izquierda",
        Zona.Derecha => "Mitad derecha",
        Zona.Maximizar => "Maximizar",
        Zona.ArribaIzquierda => "Cuarto superior izquierdo",
        Zona.ArribaDerecha => "Cuarto superior derecho",
        Zona.AbajoIzquierda => "Cuarto inferior izquierdo",
        Zona.AbajoDerecha => "Cuarto inferior derecho",
        _ => "",
    };

    // ---------- Qué hay que hacer, y en qué orden ----------
    //
    // El orden no es un detalle de implementación, es lo que se ve:
    //
    //  - **Restaurar va primero.** A una ventana maximizada Windows le ignora el
    //    `SetWindowPos` y a una minimizada la mueve sin enseñarla; las dos cosas se leen
    //    como "no ha pasado nada".
    //  - **Activar va al final.** Mover una ventana no la activa —ni debe, o cualquier
    //    recolocación de fondo te robaría el foco— así que si la elección es del usuario
    //    hay que activarla aparte. Y después de colocarla: activar primero deja un
    //    parpadeo en el sitio viejo.
    //
    // En C4.1 faltaba el último paso y una ventana que estaba detrás se quedaba detrás:
    // bien puesta y sin verse. Esto es el plan, y existe aparte para poder comprobarlo
    // sin mover ninguna ventana de nadie.
    public enum Paso { Restaurar, Colocar, Maximizar, Activar }

    public static IReadOnlyList<Paso> Plan(Zona z, EstadoVentana estado, bool activar)
    {
        var pasos = new List<Paso>();
        if (z == Zona.Ninguna) return pasos;

        // Maximizar se le PIDE a la ventana en vez de imponerle el rectángulo: así queda
        // de verdad maximizada —con su botón de restaurar y su recuerdo del tamaño
        // anterior— y no simplemente estirada hasta ocupar lo mismo.
        if (z == Zona.Maximizar)
        {
            if (estado == EstadoVentana.Minimizada) pasos.Add(Paso.Restaurar);
            pasos.Add(Paso.Maximizar);
        }
        else
        {
            if (estado != EstadoVentana.Normal) pasos.Add(Paso.Restaurar);
            pasos.Add(Paso.Colocar);
        }

        if (activar) pasos.Add(Paso.Activar);
        return pasos;
    }

    // ---------- Hacerlo de verdad ----------
    //
    // `activar` dice si esto viene de una elección del usuario. Desde la vista general o
    // soltando una tarjeta, sí; una recolocación de fondo, no.
    public static bool Ajustar(IntPtr ventana, Zona z, Monitores.Info? monitor = null,
                               bool activar = false,
                               IEnumerable<NubeHypr.Estorbo>? estorbos = null)
    {
        if (z == Zona.Ninguna || !Nativo.Existe(ventana)) return false;
        try
        {
            var m = monitor ?? Monitores.DeVentana(ventana) ?? Monitores.Principal();
            var estado = Nativo.EstaMinimizada(ventana) ? EstadoVentana.Minimizada
                       : Nativo.EstaMaximizada(ventana) ? EstadoVentana.Maximizada
                       : EstadoVentana.Normal;

            // E5: el mismo hueco que enseñó la previsualización. Si aquí se calculara con el
            // área de Windows y allí con el dock descontado, la ventana acabaría en un sitio
            // distinto del que se le prometió al usuario.
            var destino = Colocacion(z, estorbos == null ? AreaUtil(m) : AreaUtilCon(m, estorbos));
            if (z != Zona.Maximizar && destino.Vacio) return false;

            bool ok = true;
            foreach (var paso in Plan(z, estado, activar))
                switch (paso)
                {
                    case Paso.Restaurar: Nativo.Restaurar(ventana); break;
                    case Paso.Maximizar: Nativo.Maximizar(ventana); break;
                    case Paso.Activar: Nativo.Activar(ventana); break;
                    case Paso.Colocar:
                        ok = Nativo.ColocarVentanaDelUsuario(ventana, destino.X, destino.Y,
                                                             destino.Ancho, destino.Alto);
                        break;
                }

            Log.Info($"ajuste: \"{Nativo.Titulo(ventana)}\" a {Nombre(z)}" +
                     (z == Zona.Maximizar ? "" : $" ({destino.X},{destino.Y} {destino.Ancho}x{destino.Alto})") +
                     (activar ? " y al frente" : "") + (ok ? "" : " — NO se pudo colocar"));
            return ok;
        }
        catch (Exception ex) { Log.Error("ajustar una ventana", ex); return false; }
    }
}
