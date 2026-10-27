using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D6: el indicador de escritorio — nombre, número y estado de ventanas.
//
// ---------- Qué le faltaba al de antes ----------
//
// El módulo "escritorios" pintaba los números y marcaba el actual en acento. Eso dice en
// qué escritorio estás y cuántos hay, y no dice nada más: ni cómo se llama el escritorio
// —aunque Windows deja ponerle nombre y NubeHypr sabe leerlo desde D4— ni si los demás
// tienen algo dentro. Y "¿en cuál tenía yo el correo?" es exactamente la pregunta que un
// indicador de escritorios existe para contestar.
//
// ---------- Y por qué el texto se decide aquí ----------
//
// Porque el sitio disponible cambia **mucho**: la isla horizontal tiene ancho de sobra y
// la vertical no tiene ninguno. Si cada orientación se apañara su rótulo, serían dos
// verdades sobre el mismo dato y una de las dos acabaría diciendo otra cosa. Aquí se
// decide una vez, en función del sitio, y se puede comprobar sin pintar nada.
public static class HyprIndicador
{
    // Lo que el indicador sabe de un escritorio. Es el `HyprEspacios.Espacio` de D4 más
    // una cosa que D4 no miraba: cuántas ventanas hay dentro.
    public sealed record Ficha(int Numero, string Nombre, bool EsElActual, int Ventanas)
    {
        // ---------- "Tiene nombre" significa un nombre PROPIO ----------
        //
        // Cuidado aquí: `HyprEspacios.NombreVisible` nunca devuelve vacío — un escritorio
        // sin nombre sale como "Escritorio 3", que es lo que hace Windows. Si se mirara
        // solo si la cadena está vacía, el rótulo completo diría "3/4 · Escritorio 3", que
        // es el número repetido tres veces y ninguna información nueva.
        public bool TieneNombre
        {
            get
            {
                var n = Nombre.Trim();
                if (n.Length == 0) return false;
                return !n.Equals($"{HyprEspacios.SinNombre} {Numero}",
                                 StringComparison.OrdinalIgnoreCase);
            }
        }

        // Vacío, con algo, o lleno. Son tres, no dos, porque "tiene una ventana" y "tiene
        // nueve" no se parecen: el salto de información está ahí, no en cero contra uno.
        public Estado Como => Ventanas == 0 ? Estado.Vacio
                            : Ventanas <= 3 ? Estado.ConAlgo
                                            : Estado.Lleno;
    }

    public enum Estado { Vacio, ConAlgo, Lleno }

    // ================= El rótulo =================
    //
    // Cuatro tallas, de la más completa a la que cabe en una columna de 30 píxeles. No es
    // una escala de tamaño de letra: es **menos información**, elegida en este orden —
    // primero se cae el total, luego el nombre, y el número no se cae nunca.
    public enum Talla { Completo, Medio, Corto, Minimo }

    public const int MaxNombre = 18;

    public static Talla TallaQueCabe(double anchoDisponible, bool vertical)
    {
        if (vertical) return anchoDisponible >= 54 ? Talla.Corto : Talla.Minimo;
        if (anchoDisponible >= 180) return Talla.Completo;
        if (anchoDisponible >= 120) return Talla.Medio;
        if (anchoDisponible >= 64) return Talla.Corto;
        return Talla.Minimo;
    }

    public static string Rotulo(Ficha actual, int total, Talla talla)
    {
        string nombre = Recortar(actual.Nombre, MaxNombre);
        return talla switch
        {
            // "2/4 · Trabajo" — dónde estás, de cuántos, y cómo se llama.
            Talla.Completo when actual.TieneNombre => $"{actual.Numero}/{total} · {nombre}",
            Talla.Completo => $"{actual.Numero}/{total}",

            // Sin el total: el nombre dice más que "de cuántos".
            Talla.Medio when actual.TieneNombre => $"{actual.Numero} · {nombre}",
            Talla.Medio => $"{actual.Numero}/{total}",

            Talla.Corto => $"{actual.Numero}/{total}",
            _ => actual.Numero.ToString(),
        };
    }

    static string Recortar(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..Math.Max(1, max - 1)] + "…";
    }

    // ---------- Lo que se le dice a un lector de pantalla ----------
    //
    // Siempre completo, aunque el rótulo visible esté recortado, y siempre con el estado
    // en palabras: el color y el tamaño del puntito no llegan a quien usa un lector.
    public static string ParaLeer(Ficha f, int total) =>
        $"Escritorio {f.Numero} de {total}" +
        (f.TieneNombre ? $", {f.Nombre.Trim()}" : "") +
        (f.EsElActual ? ", el actual" : "") +
        ", " + f.Ventanas switch
        {
            0 => "vacío",
            1 => "1 ventana",
            _ => $"{f.Ventanas} ventanas",
        };

    // ---------- El puntito de estado ----------
    //
    // Tres tamaños para tres estados, y el vacío no pinta nada. Que sea un tamaño y no un
    // color es a propósito: el color ya lo usa "cuál es el actual", y dos informaciones en
    // el mismo canal no se distinguen.
    public static double PuntoDe(Estado e) => e switch
    {
        Estado.Lleno => 5.0,
        Estado.ConAlgo => 3.0,
        _ => 0.0,
    };

    // ================= Cuántas ventanas hay en cada escritorio =================
    //
    // Pura sobre la lista de ventanas ya observada: ni enumera ni pregunta a Windows. El
    // mapa de "en qué escritorio está esta ventana" lo da el adaptador.
    public static List<Ficha> Fichas(IReadOnlyList<HyprEspacios.Espacio> espacios,
                                     IReadOnlyDictionary<IntPtr, int> ventanaAEspacio)
    {
        var cuenta = new Dictionary<int, int>();
        foreach (var n in ventanaAEspacio.Values)
            cuenta[n] = cuenta.GetValueOrDefault(n) + 1;

        var fichas = new List<Ficha>(espacios.Count);
        for (int i = 0; i < espacios.Count; i++)
        {
            int numero = i + 1;
            fichas.Add(new Ficha(numero, espacios[i].Nombre, espacios[i].EsElActual,
                                 cuenta.GetValueOrDefault(numero)));
        }
        return fichas;
    }

    // ================= La huella: cuándo hay que repintar =================
    //
    // El módulo de escritorios ya comparaba "cuántos hay" y "cuál es el actual" para no
    // repintar cada segundo. Con nombre y número de ventanas eso no basta: renombrar un
    // escritorio o mover una ventana no cambia ninguno de los dos, y el indicador se
    // quedaría mintiendo hasta que cambiara el número de escritorios.
    //
    // Pero tampoco se puede meter TODO en la huella. Si entrara el título de las ventanas,
    // cada pulsación de tecla en el navegador repintaría el indicador.
    public static string Huella(IReadOnlyList<Ficha> fichas, Talla talla) =>
        HyprVista.Huella(fichas.Select(f =>
            $"{f.Numero}:{f.Nombre}:{(f.EsElActual ? "1" : "0")}:{(int)f.Como};")) + "|" + talla;
}
