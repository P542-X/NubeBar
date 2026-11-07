using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.1: deshacer lo último, porque si no hay vuelta atrás no se puede encender.
//
// ---------- Por qué es un requisito y no una comodidad ----------
//
// El día que las reglas coloquen de verdad, la primera vez que una esté mal escrita va a
// recolocar diez ventanas a la vez. Sin una vuelta atrás, el usuario tiene que recolocar
// diez ventanas a mano mientras la regla se lo deshace. Con una tecla, es un susto de dos
// segundos.
//
// `HyprRestaurar` ya guarda el estado anterior de una ventana al organizarla, pero su
// pregunta es otra: "esta ventana estaba maximizada antes de entrar en el mosaico". Lo que
// hace falta aquí es **el lote**: lo que había justo antes de la última aplicación, entero,
// para poder devolverlo de una vez.
public static class HyprDeshacer
{
    // Una ventana y dónde estaba. No se guarda el proceso ni el título: lo que se devuelve
    // es geometría, y guardar más sería guardar algo que puede haber cambiado.
    public readonly record struct Antes(IntPtr Hwnd, NubeHypr.Rect Donde, bool Maximizada)
    {
        public override string ToString() =>
            $"0x{Hwnd.ToInt64():X6} -> {Donde}{(Maximizada ? " (estaba maximizada)" : "")}";
    }

    public sealed record Lote(string Porque, DateTime Cuando, List<Antes> Ventanas)
    {
        public int Cuantas => Ventanas.Count;
        public TimeSpan Edad => DateTime.Now - Cuando;

        public override string ToString() =>
            $"{Cuantas} ventana(s) de «{Porque}» hace {Edad.TotalSeconds:0} s";
    }

    // ---------- Cuántos lotes se guardan ----------
    //
    // Cinco. No uno, porque una ráfaga de aplicaciones puede producir tres lotes seguidos y
    // deshacer solo el último dejaría al usuario a medias. Y no cincuenta, porque un
    // "deshacer" de hace diez minutos devuelve ventanas a sitios que ya no tienen sentido —
    // y porque esto es memoria, no historial.
    public const int MaxLotes = 5;

    // Y una caducidad: pasados cinco minutos, lo que había antes ya no es "lo de antes".
    public static readonly TimeSpan Caducidad = TimeSpan.FromMinutes(5);

    public sealed class Pila
    {
        readonly List<Lote> _lotes = new();

        public int Cuantos => _lotes.Count;
        public IReadOnlyList<Lote> Todos => _lotes;
        public Lote? Ultimo => _lotes.Count > 0 ? _lotes[^1] : null;

        // Se apunta ANTES de aplicar, con la geometría leída de Windows. Si se apuntara
        // después, lo que se guardaría es el destino y "deshacer" no haría nada.
        public bool Apuntar(string porque, IEnumerable<Antes> ventanas, DateTime? cuando = null)
        {
            var lista = ventanas.Where(v => v.Hwnd != IntPtr.Zero && v.Donde.Valido).ToList();
            if (lista.Count == 0) return false;      // un lote vacío no es un lote

            _lotes.Add(new Lote(porque, cuando ?? DateTime.Now, lista));
            while (_lotes.Count > MaxLotes) _lotes.RemoveAt(0);
            return true;
        }

        // Saca el último y devuelve los efectos que lo deshacen. Los efectos se construyen
        // aquí y no en el adaptador porque "deshacer" es una decisión de este archivo: el
        // adaptador solo sabe mover.
        //
        // `vivas` filtra las que ya no existen: devolver una ventana cerrada no es un error,
        // es nada, y hacerlo saltar sería convertir "una se cerró" en "no se puede deshacer".
        public (Lote? Lote, List<EfectoVentana> Efectos) Deshacer(
            IReadOnlyCollection<IntPtr> vivas, DateTime? ahora = null)
        {
            Caducar(ahora);
            if (_lotes.Count == 0) return (null, new List<EfectoVentana>());

            var lote = _lotes[^1];
            _lotes.RemoveAt(_lotes.Count - 1);

            var efectos = lote.Ventanas
                .Where(v => vivas.Contains(v.Hwnd))
                .Select(v => new EfectoVentana(
                    v.Maximizada ? TipoEfecto.Restaurar : TipoEfecto.Colocar,
                    v.Hwnd, v.Donde, Porque: "deshacer: " + lote.Porque))
                .ToList();

            return (lote, efectos);
        }

        public int Caducar(DateTime? ahora = null)
        {
            var corte = ahora ?? DateTime.Now;
            int fuera = _lotes.RemoveAll(l => corte - l.Cuando > Caducidad);
            return fuera;
        }

        public void Vaciar() => _lotes.Clear();
    }

    // ---------- Lo que se le dice al usuario ----------
    //
    // Con el número de ventanas y el motivo del lote. "Deshecho" a secas no sirve: si se
    // deshizo lo que no era, hay que poder verlo en el registro.
    public static string Linea(Lote? lote, int efectos, NubeHypr.Modo modo) =>
        lote == null
            ? "[D9]  deshacer: no hay nada que deshacer"
            : $"[D9]  deshacer {lote} -> {efectos} efecto(s), " +
              $"modo={modo.ToString().ToLowerInvariant()}" +
              (modo == NubeHypr.Modo.Aplicar ? "" : " (solo apuntado)");
}
