using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.1: si tú mueves una ventana a mano, la regla no te la quita.
//
// ---------- Por qué esto va ANTES de aplicar ----------
//
// Es lo primero que rompe un gestor de ventanas automático, y lo que hace que la gente los
// desinstale el primer día: colocas una ventana donde la quieres, la regla la devuelve a su
// sitio, y la pelea la gana siempre el programa. No hay forma de trabajar así.
//
// La regla de la casa es la contraria: **el gesto del usuario manda**. Una ventana que has
// tocado a mano queda marcada, y las reglas dejan de opinar sobre ella hasta que se cierre.
//
// ---------- Cómo se sabe que lo has movido tú ----------
//
// Por eliminación, y es la única forma honesta: NubeHypr sabe exactamente dónde pidió que
// estuviera cada ventana que colocó. Si una ventana aparece en otro sitio, ese cambio no es
// nuestro. Puede ser tuyo o puede ser de la propia aplicación (un editor que recuerda su
// tamaño, un reproductor que se redimensiona al cargar un vídeo), y las dos merecen la
// misma respuesta: **no insistir**.
//
// Lo que NO se hace es intentar distinguir "lo movió el usuario" de "se movió la app". No se
// puede saber sin espiar el ratón, y acertar a medias sería peor que la regla simple.
public static class HyprRespeto
{
    // ================= De quién fue el cambio =================
    public enum Quien
    {
        SinCambio,      // está donde estaba
        Nuestro,        // está donde NubeHypr le pidió estar
        DeFuera,        // está en otro sitio: el gesto manda
    }

    // La tolerancia es la misma que usa `Confirmar` en el adaptador (8 px) por un motivo
    // concreto: Windows no siempre coloca exactamente donde se le pide —bordes invisibles,
    // tamaños mínimos de la propia ventana— y si aquí se exigiera igualdad exacta, nuestro
    // propio movimiento se leería como un gesto del usuario y la regla se desactivaría sola.
    public const double Tolerancia = 8;

    public static Quien DeQuienFue(NubeHypr.Rect antes, NubeHypr.Rect ahora,
                                   NubeHypr.Rect? loQuePedimos)
    {
        if (ahora.CasiIgual(antes, Tolerancia)) return Quien.SinCambio;
        if (loQuePedimos is { } p && p.Valido && ahora.CasiIgual(p, Tolerancia))
            return Quien.Nuestro;
        return Quien.DeFuera;
    }

    // ================= La marca =================
    //
    // Por `hwnd` y para toda la vida de la ventana. No caduca a propósito: si caducara, la
    // regla volvería a colocarte la ventana al rato y el usuario viviría la misma pelea, solo
    // más espaciada — que es peor, porque además sería impredecible.
    public sealed class Memoria
    {
        readonly Dictionary<IntPtr, DateTime> _tocadas = new();
        readonly Dictionary<IntPtr, NubeHypr.Rect> _loQuePedimos = new();

        public int Cuantas => _tocadas.Count;
        public IEnumerable<IntPtr> Todas => _tocadas.Keys;

        // Lo que NubeHypr pidió para esta ventana. Se apunta al aplicar, y es lo que permite
        // distinguir después nuestro propio movimiento de uno de fuera.
        public void Pedimos(IntPtr hwnd, NubeHypr.Rect destino)
        {
            if (hwnd == IntPtr.Zero || !destino.Valido) return;
            _loQuePedimos[hwnd] = destino;
        }

        public NubeHypr.Rect? LoQuePedimos(IntPtr hwnd) =>
            _loQuePedimos.TryGetValue(hwnd, out var r) ? r : null;

        public bool EstaTocada(IntPtr hwnd) => _tocadas.ContainsKey(hwnd);

        public DateTime? Cuando(IntPtr hwnd) =>
            _tocadas.TryGetValue(hwnd, out var c) ? c : null;

        // Devuelve si es la PRIMERA vez que se marca. Las siguientes no son un error: es que
        // la sigues moviendo, y lo que importa es que quede marcada, no cuántas veces.
        public bool Marcar(IntPtr hwnd, DateTime? cuando = null)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (_tocadas.ContainsKey(hwnd)) return false;
            _tocadas[hwnd] = cuando ?? DateTime.Now;
            return true;
        }

        // Soltar la marca es un acto EXPLÍCITO del usuario ("vuelve a organizar esta
        // ventana"), no algo que pase solo. Está aquí para que el panel pueda ofrecerlo.
        public bool Soltar(IntPtr hwnd) => _tocadas.Remove(hwnd);

        public void SoltarTodas() { _tocadas.Clear(); }

        // Las ventanas que ya no existen se van. Sin esto, una sesión larga acumula handles
        // muertos y —lo importante— un hwnd que Windows reutilice nacería marcado: la regla
        // no se aplicaría a una ventana nueva por culpa de una que se cerró ayer.
        public int Limpiar(IEnumerable<IntPtr> vivas)
        {
            var hay = new HashSet<IntPtr>(vivas);
            int fuera = 0;
            foreach (var h in _tocadas.Keys.Where(k => !hay.Contains(k)).ToList())
            { _tocadas.Remove(h); fuera++; }
            foreach (var h in _loQuePedimos.Keys.Where(k => !hay.Contains(k)).ToList())
                _loQuePedimos.Remove(h);
            return fuera;
        }

        public void Vaciar() { _tocadas.Clear(); _loQuePedimos.Clear(); }
    }

    // ================= La decisión =================
    //
    // Se mira ANTES de calcular nada: si la ventana está marcada, no hay nada que decidir y
    // no hay que gastar en resolver reglas para después tirarlo.
    public static bool SeLePuedeTocar(IntPtr hwnd, Memoria memoria) => !memoria.EstaTocada(hwnd);

    // Y el renglón del registro, que es la mitad del valor: "mi regla no funciona" se
    // contesta leyendo que la ventana está marcada y desde cuándo.
    public static string Linea(IntPtr hwnd, string proceso, Memoria memoria)
    {
        var cuando = memoria.Cuando(hwnd);
        return cuando == null
            ? $"[D9]  0x{hwnd.ToInt64():X6} {proceso}: las reglas pueden colocarla"
            : $"[D9]  0x{hwnd.ToInt64():X6} {proceso}: la moviste tú a las " +
              $"{cuando:HH:mm:ss}, las reglas no la tocan";
    }

    // ---------- Lo que se apunta cuando se detecta un gesto ----------
    public static string LineaDelGesto(IntPtr hwnd, string proceso, Quien quien,
                                       NubeHypr.Rect antes, NubeHypr.Rect ahora) =>
        $"[D9]  gesto hwnd=0x{hwnd.ToInt64():X6} proceso={proceso} de={quien} " +
        $"antes={antes} ahora={ahora}" +
        (quien == Quien.DeFuera ? "  -> marcada: las reglas la dejan en paz" : "");
}
