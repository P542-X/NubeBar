using System;
using System.Windows.Media;

namespace NubeBar;

// El halo del estilo «Neón»: lo que se puede decidir sin pintar nada.
//
// ---------- Por qué existe este fichero ----------
//
// Topar un número, elegir el color del halo y decidir cuánto baja la intensidad al respirar
// son decisiones, y las decisiones se prueban. Metidas dentro del código que crea el
// `DropShadowEffect` solo se pueden comprobar a ojo, y a ojo es como se coló dos veces un
// neón que no era neón.
//
// Aquí no se abre ninguna superficie ni se toca WPF más allá de `Color`, que es una struct.
public static class Neon
{
    // ---------- Cuánto baja la luz al respirar ----------
    //
    // El 45 % de su máximo, no cero. Es la diferencia entre respirar y parpadear: un efecto
    // que se apaga del todo y vuelve a encenderse llama la atención cada ciclo, y en algo que
    // está en pantalla todo el día eso se vuelve insoportable. Pedido explícitamente: «el
    // brillo nunca desaparece completamente».
    public const double SueloDeLaRespiracion = 0.45;

    // ---------- Los topes ----------
    //
    // Un ajuste puede decorar; no puede dejar la interfaz rota. Con intensidad 9 el halo tapa
    // el texto de la tarjeta de al lado, y con radio 300 el panel entero se convierte en una
    // mancha. Y por abajo: a 0 el estilo «Neón» dejaría de verse, y entonces el selector de
    // estilos mentiría — para no tener neón se elige otro estilo, no se apaga este.
    //
    // Topar y no rechazar: un config a mano con un 9 no tiene por qué impedir arrancar.
    public const double IntensidadMin = 0.15, IntensidadMax = 2.5;
    public const double RadioMin = 6, RadioMax = 60;
    public const int VelocidadMin = 800, VelocidadMax = 12000;

    public sealed record Ajuste(double Intensidad, double Radio, int Velocidad, bool Respira, string Color);

    // Lo que llega del fichero, pasado por los topes. Un `null` —config viejo que no tiene la
    // sección— da el ajuste de serie, que es lo mismo que tenía el estilo antes de ser
    // configurable: quien no toque nada no nota que esto existe.
    public static Ajuste Limpia(OpcionesNeon? o)
    {
        o ??= new OpcionesNeon();
        return new Ajuste(
            Math.Clamp(Raro(o.Intensidad, 1.0), IntensidadMin, IntensidadMax),
            Math.Clamp(Raro(o.Radio, 26), RadioMin, RadioMax),
            Math.Clamp(o.Velocidad <= 0 ? 2600 : o.Velocidad, VelocidadMin, VelocidadMax),
            o.Respira,
            o.Color ?? "");
    }

    // NaN e infinito no son números pequeños ni grandes: `Clamp` los deja pasar y revientan
    // el render más tarde, lejos de aquí. Se cambian por el de serie.
    static double Raro(double v, double siNo) => double.IsFinite(v) ? v : siNo;

    // ---------- Los dos colores ----------
    //
    // Vacío: los dos salen del acento del tema, que es la fuente de verdad. Un color: manda
    // ese para los dos. Dos separados por coma: el primero es el del filo y el segundo el del
    // halo — que es lo que da el degradado, y además lo que hace que el halo se lea como luz
    // y no como la sombra del borde.
    //
    // Un color mal escrito no tumba nada: se ignora y se usa el del tema. Es la misma regla
    // que `Roles.Leer` y que `Estilos.Leer`: lo que no se entiende cae en lo de serie.
    public static (Color DelFilo, Color DeLaLuz) Colores(Ajuste a, Color acento)
    {
        var partes = (a.Color ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        Color uno = Leer(partes.Length > 0 ? partes[0] : null) ?? acento;
        Color dos = Leer(partes.Length > 1 ? partes[1] : null) ?? uno;
        return (uno, dos);
    }

    static Color? Leer(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); }
        catch { return null; }
    }

    // ---------- Los presets ----------
    //
    // Para no obligar a nadie a entender «radio del difuminado» antes de poder tener su
    // escritorio bonito. Quien quiera afinar, afina; quien no, elige una palabra.
    public static readonly (string Nombre, Ajuste Como)[] Presets =
    {
        ("Susurro", new Ajuste(0.45, 16, 3400, true, "")),
        ("Equilibrado", new Ajuste(1.0, 26, 2600, true, "")),
        ("Intenso", new Ajuste(1.7, 38, 2000, true, "")),
        ("Fijo", new Ajuste(1.0, 26, 2600, false, "")),
    };

    public static Ajuste? Preset(string? nombre)
    {
        foreach (var (n, como) in Presets)
            if (string.Equals(n, nombre, StringComparison.OrdinalIgnoreCase)) return como;
        return null;
    }

    // Cómo se llama lo que hay puesto, para enseñarlo sin que el usuario tenga que deducirlo
    // de cuatro números. «Personalizado» cuando no coincide con ninguno.
    public static string NombreDe(Ajuste a)
    {
        foreach (var (n, como) in Presets)
            if (Math.Abs(como.Intensidad - a.Intensidad) < 0.01 &&
                Math.Abs(como.Radio - a.Radio) < 0.5 &&
                como.Velocidad == a.Velocidad && como.Respira == a.Respira)
                return n;
        return "Personalizado";
    }
}
