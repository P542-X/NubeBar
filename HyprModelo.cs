using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D1: el modelo. Ventanas, espacios, reglas, comandos y efectos.
//
// ---------- Por qué esto no llama a Windows ----------
//
// Todo lo de este fichero y de HyprLayout.cs recibe datos y devuelve decisiones. No
// enumera ventanas, no llama a SetWindowPos y no toca la interfaz. No es purismo: es lo
// único que permite comprobar cientos de casos —cero ventanas, cien ventanas, un monitor
// con origen negativo, DPI al 200 %— sin abrir una sola ventana de verdad, y es lo que
// evita que una decisión importante se quede escondida en un `callback` de WPF. Los dos
// fallos más caros de NubeBar —el parpadeo de la vista general y el deslizador que se
// quedaba agarrado— eran exactamente eso.
//
// El adaptador (D2) traducirá: observa, llama aquí, aplica los efectos y confirma.

// ---------- Una ventana, como la ve el modelo ----------
//
// `Rectangulo` va en PÍXELES DE PANTALLA, el mismo sistema que declara `NubeHypr.Rect` en
// D0. No hay unidades de WPF en el modelo: la conversión la hace el adaptador, a la vista,
// y nunca al revés. Mezclar los dos sistemas en silencio es de donde salen los ajustes
// desplazados en un monitor al 150 %.
public sealed record VentanaModelo(
    IntPtr Hwnd,
    string Proceso,
    string Titulo,
    NubeHypr.Rect Rectangulo,
    bool Visible = true,
    bool Minimizada = false,
    bool Maximizada = false,
    bool Flotante = false,
    int Escritorio = 0,
    int Monitor = 0)
{
    // ¿Entra en el reparto de un layout? Lo que no entra NO se mueve: se deja donde está
    // y el motivo se escribe en el efecto, para que el log lo diga.
    public bool Enlosable => Visible && !Minimizada && !Maximizada && !Flotante;
}

public sealed record EspacioTrabajo(string Id, string Nombre, int Orden);

// Una regla del usuario. D1 solo define el tipo y la comparación; aplicarlas de verdad es
// D5, y mezclarlo aquí sería meter persistencia en el modelo de layouts.
public sealed record ReglaVentana(
    string? Proceso, string? Titulo, string? Clase,
    string Accion, string? Espacio, bool Flotante);

// ---------- El estado ----------
//
// Inmutable a propósito: los comandos devuelven un estado NUEVO. Así "aplicar el mismo
// comando dos veces" se puede comprobar comparando objetos, y un efecto que llega tarde
// no puede mutar por detrás el estado que alguien está leyendo.
public sealed record EstadoNubeHypr(
    IReadOnlyList<VentanaModelo> Ventanas,
    IReadOnlyList<EspacioTrabajo> Espacios,
    IntPtr Foco,
    NubeHypr.Layout Layout = NubeHypr.Layout.Principal,
    double Proporcion = 0.6,
    int EspacioActual = 0)
{
    public static readonly EstadoNubeHypr Vacio =
        new(Array.Empty<VentanaModelo>(), Array.Empty<EspacioTrabajo>(), IntPtr.Zero);

    public VentanaModelo? De(IntPtr hwnd) => Ventanas.FirstOrDefault(v => v.Hwnd == hwnd);

    // Las del espacio que se está mirando, en su orden. El orden ES la lista: insertar
    // añade al final y quitar no recoloca a los demás, que es lo que hace que organizar
    // no dé saltos.
    public IReadOnlyList<VentanaModelo> DelEspacio(int espacio) =>
        Ventanas.Where(v => v.Escritorio == espacio).ToList();
}

// ---------- Lo que el modelo pide que se haga ----------
//
// Un efecto es una intención, no una acción: dice qué debería pasarle a una ventana. El
// adaptador decide si puede, lo intenta y apunta el resultado. Por eso un efecto puede
// venir con `Porque` y sin rectángulo: "esta se queda como está, y esta es la razón".
public enum TipoEfecto
{
    Colocar,        // ponla en este rectángulo
    Enfocar,        // dale el foco
    AEspacio,       // llévala a este escritorio
    SeQueda,        // no se toca, y aquí está el motivo

    // ---------- Los dos de D3 ----------
    //
    // Sacar una ventana de maximizada o minimizada para poder enlosarla, y devolverla
    // después. Son efectos aparte y no un detalle de `Colocar` porque son VISIBLES y
    // reversibles: el usuario tiene que poder pedirlos, y poder deshacerlos.
    Restaurar,      // sácala de maximizada/minimizada (guardando lo que era)
    Devolver,       // y vuélvela a dejar como estaba
}

public sealed record EfectoVentana(
    TipoEfecto Tipo,
    IntPtr Hwnd,
    NubeHypr.Rect Destino = default,
    int Espacio = 0,
    string Porque = "")
{
    public override string ToString() => Tipo switch
    {
        TipoEfecto.Colocar => $"colocar 0x{Hwnd.ToInt64():X6} en {Destino}",
        TipoEfecto.Enfocar => $"enfocar 0x{Hwnd.ToInt64():X6}",
        TipoEfecto.AEspacio => $"0x{Hwnd.ToInt64():X6} al espacio {Espacio}",
        _ => $"0x{Hwnd.ToInt64():X6} se queda ({Porque})",
    };
}

// ---------- Los comandos ----------
//
// Semánticos, no acciones directas: "enfoca el siguiente", no "llama a SetForegroundWindow
// con este hwnd". Es lo que pide la especificación para D3 y lo que permite que un atajo
// se configure sin saber nada de Win32.
public enum Direccion { Izquierda, Derecha, Arriba, Abajo }

public abstract record Comando
{
    public sealed record EnfocarSiguiente : Comando;
    public sealed record EnfocarAnterior : Comando;
    public sealed record EnfocarHacia(Direccion Hacia) : Comando;
    public sealed record MoverEnElOrden(bool Adelante) : Comando;
    public sealed record Redimensionar(double Delta) : Comando;
    public sealed record AlternarFlotante : Comando;
    public sealed record CambiarEspacio(int Espacio, bool LlevarLaVentana) : Comando;

    // ---------- Los de D4 ----------
    //
    // "Al vecino" no se puede expresar con un número: el número depende de cuántos
    // escritorios haya AHORA, y eso lo sabe el adaptador, no el atajo. Así que el comando
    // dice la intención y quien lo ejecuta la resuelve contra la lista de verdad.
    public sealed record AlVecino(bool Siguiente, bool LlevarLaVentana) : Comando;
    public sealed record AlNumero(int Numero, bool LlevarLaVentana) : Comando;
    public sealed record PonerLayout(NubeHypr.Layout Cual) : Comando;

    // ---------- El de D9.1 ----------
    //
    // Deshacer no es un comando como los demás: los otros dicen qué hacer con las ventanas
    // de ahora, y este dice "lo de hace un momento, no". Por eso no lleva ni dirección ni
    // ventana: el lote ya sabe cuáles eran.
    //
    // Es el comando número 20, y romper el "son 19" fue deliberado. Sin una tecla que
    // deshaga, encender el modo "aplicar" significa que la primera regla mal escrita te
    // recoloca diez ventanas y te deja recolocándolas a mano.
    public sealed record Deshacer : Comando;
}
