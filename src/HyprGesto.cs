using System;

namespace NubeBar;

// D3: un gesto de teclado que se puede cancelar.
//
// ---------- Por qué reutiliza la máquina de los deslizadores ----------
//
// Mantener pulsado Win+Alt+→ para empujar una ventana es un GESTO, no una pulsación: tiene
// principio, repetición y final, y el final puede llegar de varias formas. Es el mismo
// problema que el deslizador de volumen, y ese problema ya costó una corrección entera:
// el fin del gesto colgaba de `MouseLeftButtonUp`, que en una ventana `WS_EX_NOACTIVATE`
// se pierde, y el deslizador se quedaba agarrado para siempre.
//
// `GestoDeslizador` (MainWindow.Gesto.cs) es la máquina que salió de aquello: un solo
// camino de salida, idempotente, con los recursos en espejo y seis formas de terminar.
// Escribir aquí una segunda máquina de estados sería repetir el error con otro nombre, así
// que esto la ENVUELVE y le añade lo único que le falta para un gesto de ventanas: qué
// había antes, para poder cancelar de verdad.
//
// ---------- Qué significa "cancelar de verdad" ----------
//
// No es "parar de mover". Es **devolver la ventana al sitio donde estaba cuando empezó el
// gesto**. Un Esc que deja la ventana a medio camino es peor que no tener Esc: el usuario
// ya no sabe dónde la tenía.
public sealed class HyprGesto
{
    readonly MainWindow.GestoDeslizador _maquina = new();

    // Lo que había al empezar. Es lo que hace que Esc signifique algo.
    public NubeHypr.Rect Original { get; private set; }
    public NubeHypr.Rect Actual { get; private set; }
    public IntPtr Hwnd { get; private set; }

    // Cuántos pasos se han dado. Sirve para el log y para saber si cancelar tiene algo
    // que deshacer.
    public int Pasos { get; private set; }

    public bool EnMarcha => _maquina.Arrastrando;
    internal MainWindow.EstadoGesto Estado => _maquina.Estado;
    public int VecesLimpiado => _maquina.VecesLimpiado;

    // Lo que hay que hacer de verdad: colocar la ventana. Lo conecta el adaptador; aquí no
    // se llama a Windows.
    public Action<IntPtr, NubeHypr.Rect>? Colocar;
    public Action<string>? Apuntar;

    public HyprGesto()
    {
        // La máquina avisa de lo que hay que soltar; aquí no hay captura de ratón ni
        // oyente de fotograma, así que lo que se suelta es el estado del gesto.
        _maquina.Apuntar = m => Apuntar?.Invoke(m);
        _maquina.SoltarCaptura = () => { };
        _maquina.DejarDeSeguir = () => { };
    }

    // ---------- Empezar ----------
    //
    // Si ya había un gesto, la máquina lo termina primero: dos gestos mezclados es otra
    // forma de quedarse agarrado. Y se apunta el rectángulo original ANTES del primer
    // paso, que es el único momento en que se puede.
    public bool Empezar(IntPtr hwnd, NubeHypr.Rect donde)
    {
        if (!donde.Valido || hwnd == IntPtr.Zero)
        {
            Apuntar?.Invoke("hypr: gesto no empezado (ventana o rectángulo inválidos)");
            return false;
        }
        _maquina.Empezar();
        Hwnd = hwnd;
        Original = donde;
        Actual = donde;
        Pasos = 0;
        Apuntar?.Invoke(NubeHypr.LineaGesto("ventana", "started", $"desde {donde}"));
        return true;
    }

    // ---------- Un paso ----------
    //
    // Devuelve si el gesto sigue vivo. Un paso con el gesto parado NO mueve nada: es el
    // caso de una tecla que llega tarde, después de haber soltado.
    public bool Paso(NubeHypr.Rect nuevo)
    {
        if (!EnMarcha) return false;
        if (!nuevo.Valido) return true;         // un destino imposible no rompe el gesto
        if (nuevo.CasiIgual(Actual, 0.5)) return true;   // ya estaba ahí: no se repinta

        Actual = nuevo;
        Pasos++;
        Colocar?.Invoke(Hwnd, nuevo);
        _maquina.Mover();
        return true;
    }

    // ---------- Terminar bien ----------
    //
    // Se queda donde esté. Idempotente: llamarlo cinco veces no falla ni deja el gesto
    // puesto.
    public void Terminar(string porque = "soltado")
    {
        if (!EnMarcha) { _maquina.Terminar(porque); return; }
        var donde = Actual;
        _maquina.Terminar(porque);
        Apuntar?.Invoke(NubeHypr.LineaGesto("ventana", "clean", porque) + $" en {donde} tras {Pasos} paso(s)");
        Hwnd = IntPtr.Zero;
    }

    // ---------- Cancelar ----------
    //
    // **Devuelve la ventana a donde estaba.** Esc, perder el foco, cerrarse el panel,
    // empezar otro gesto: todas acaban aquí, y todas dejan la ventana donde el usuario la
    // tenía.
    //
    // Si no se dio ni un paso, no se coloca nada: pedirle a Windows que ponga la ventana
    // donde ya está es una llamada de más y un parpadeo posible.
    public void Cancelar(string porque = "Esc")
    {
        if (!EnMarcha) { _maquina.Cancelar(porque); return; }

        var vuelta = Original;
        int pasos = Pasos;
        var hwnd = Hwnd;
        _maquina.Cancelar(porque);

        if (pasos > 0 && hwnd != IntPtr.Zero)
        {
            Colocar?.Invoke(hwnd, vuelta);
            Actual = vuelta;
        }
        Apuntar?.Invoke(NubeHypr.LineaGesto("ventana", "cancelled", porque) +
                        (pasos > 0 ? $" devuelta a {vuelta}" : " sin nada que deshacer"));
        Hwnd = IntPtr.Zero;
    }

    // ---------- Y el fotograma ----------
    //
    // La misma verdad que en los deslizadores: la dice el estado real de la tecla, una vez
    // por fotograma, y no un evento que puede perderse. Si la tecla ya no está pulsada, el
    // gesto termina AQUÍ.
    public bool Fotograma(bool teclaPulsada) => _maquina.Fotograma(teclaPulsada);
}
