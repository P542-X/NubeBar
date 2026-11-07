using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D3: lo que había antes, para poder devolverlo.
//
// ---------- El hallazgo de D2 ----------
//
// Midiendo el motor en simulación sobre un escritorio real salieron 6 ventanas y **5 se
// saltaron por estar maximizadas o minimizadas**. Los cuatro layouts dieron el mismo
// resultado porque solo quedaba una que repartir.
//
// No es un fallo del motor: es cómo se usa Windows. Pero significa que un organizador que
// se niegue a tocar ventanas maximizadas no organizará nada.
//
// ---------- Y por qué NO se desmaximiza sin permiso ----------
//
// Porque sacar una ventana de maximizada es una acción VISIBLE y sorprendente: el usuario
// la dejó a pantalla completa a propósito. Así que hacen falta dos cosas, las dos
// explícitas:
//
//  1. La opción `RestaurarAlOrganizar`, que nace APAGADA. Apagada, el comportamiento es el
//     de D2: maximizadas y minimizadas se quedan fuera, con su motivo.
//  2. Y aunque esté encendida, lo que se calcula es un EFECTO `Restaurar`, no una llamada.
//     En simulación se apunta y no pasa nada. Para que ocurra de verdad hace falta el modo
//     "aplicar", que es otra decisión del usuario.
//
// Y lo que se toca se puede devolver: antes de restaurar se guarda qué era y dónde estaba,
// y `Devolver` la deja como la encontró. Una capa que organiza sin saber deshacer no es
// una herramienta, es un accidente.
public static class HyprRestaurar
{
    // ---------- Lo que se guarda de una ventana ----------
    //
    // Su estado Y su rectángulo. El rectángulo hace falta porque una ventana que estaba
    // maximizada tiene además un "sitio de cuando no lo está", y devolverla a maximizada
    // sin él la dejaría, al desmaximizar otra vez, en un sitio que nunca eligió.
    public readonly record struct LoQueHabia(
        IntPtr Hwnd, bool Maximizada, bool Minimizada, NubeHypr.Rect Rectangulo)
    {
        public bool EstabaNormal => !Maximizada && !Minimizada;
        public override string ToString() =>
            $"0x{Hwnd.ToInt64():X6} " +
            (Maximizada ? "maximizada" : Minimizada ? "minimizada" : "normal") +
            $" en {Rectangulo}";
    }

    // ---------- La memoria ----------
    //
    // Un diccionario con reglas. No es una colección pelada porque las reglas importan:
    // se recuerda UNA vez (la primera, que es la que tenía el usuario), y se olvida al
    // devolver. Recordar dos veces machacaría el estado original con el intermedio, y
    // entonces "devolver" dejaría la ventana donde la puso NubeHypr, no donde estaba.
    public sealed class Memoria
    {
        readonly Dictionary<IntPtr, LoQueHabia> _loQueHabia = new();

        public int Cuantas => _loQueHabia.Count;
        public bool Sabe(IntPtr hwnd) => _loQueHabia.ContainsKey(hwnd);
        public IEnumerable<LoQueHabia> Todo => _loQueHabia.Values;

        // Devuelve si lo ha apuntado. La primera vez sí; las siguientes no, y eso NO es
        // un error: es la regla.
        public bool Recordar(VentanaModelo v)
        {
            if (_loQueHabia.ContainsKey(v.Hwnd)) return false;
            _loQueHabia[v.Hwnd] = new LoQueHabia(v.Hwnd, v.Maximizada, v.Minimizada, v.Rectangulo);
            return true;
        }

        public LoQueHabia? Consultar(IntPtr hwnd) =>
            _loQueHabia.TryGetValue(hwnd, out var q) ? q : null;

        // Al devolver se olvida: si no, la próxima vez que se organice se volvería a
        // "devolver" a un estado de hace media hora.
        public LoQueHabia? Olvidar(IntPtr hwnd)
        {
            if (!_loQueHabia.Remove(hwnd, out var q)) return null;
            return q;
        }

        // Una ventana que ya no existe no puede devolverse a ningún sitio. Se limpia con
        // el snapshot para que la memoria no crezca sin fin durante una sesión larga.
        public int Limpiar(IEnumerable<IntPtr> vivas)
        {
            var hay = new HashSet<IntPtr>(vivas);
            var muertas = _loQueHabia.Keys.Where(h => !hay.Contains(h)).ToList();
            foreach (var h in muertas) _loQueHabia.Remove(h);
            return muertas.Count;
        }

        public void Vaciar() => _loQueHabia.Clear();
    }

    // ---------- Qué hacer con las maximizadas y minimizadas ----------
    //
    // Devuelve los efectos de restauración y apunta en la memoria lo que había. Si la
    // opción está apagada —que es lo de serie— devuelve lista vacía y el reparto normal se
    // encarga de dejarlas fuera con su motivo.
    //
    // Puro sobre la memoria que se le pasa: no llama a Windows ni mira configuración
    // global.
    public static List<EfectoVentana> Preparar(
        EstadoNubeHypr estado, Memoria memoria, bool restaurarAlOrganizar)
    {
        var efectos = new List<EfectoVentana>();
        if (!restaurarAlOrganizar) return efectos;

        foreach (var v in estado.DelEspacio(estado.EspacioActual))
        {
            // Las flotantes NO se restauran: el usuario dijo que las lleva a mano, y una
            // flotante maximizada maximizada está porque él quiso.
            if (v.Flotante || !v.Visible) continue;
            if (!v.Maximizada && !v.Minimizada) continue;

            memoria.Recordar(v);
            efectos.Add(new EfectoVentana(
                TipoEfecto.Restaurar, v.Hwnd,
                Porque: v.Maximizada ? "estaba maximizada y se va a enlosar"
                                     : "estaba minimizada y se va a enlosar"));
        }
        return efectos;
    }

    // ---------- Y el camino de vuelta ----------
    //
    // Devolver una ventana a lo que era. Si no se recuerda nada de ella, no se hace nada:
    // inventar un estado anterior es peor que no tener ninguno.
    public static EfectoVentana? Devolver(Memoria memoria, IntPtr hwnd)
    {
        var q = memoria.Olvidar(hwnd);
        if (q == null) return null;
        var lo = q.Value;
        return new EfectoVentana(
            TipoEfecto.Devolver, hwnd, lo.Rectangulo,
            Porque: lo.Maximizada ? "vuelve a maximizada"
                  : lo.Minimizada ? "vuelve a minimizada"
                  : "vuelve a su sitio de antes");
    }

    // Devolverlo TODO. Es el "deshacer" de la capa: lo que hace falta para poder apagar
    // NubeHypr y que el escritorio quede como estaba.
    public static List<EfectoVentana> DevolverTodo(Memoria memoria)
    {
        var efectos = new List<EfectoVentana>();
        foreach (var hwnd in memoria.Todo.Select(q => q.Hwnd).ToList())
            if (Devolver(memoria, hwnd) is { } ef) efectos.Add(ef);
        return efectos;
    }

    // ---------- El estado que tendría una ventana ya restaurada ----------
    //
    // Para que el reparto la cuente. Sin esto habría que observar otra vez después de
    // restaurar —un segundo viaje a Windows— y el reparto se calcularía con un snapshot
    // en el que la ventana sigue maximizada: saldría fuera otra vez.
    public static EstadoNubeHypr ComoSiYaEstuvieranRestauradas(
        EstadoNubeHypr estado, IReadOnlyList<EfectoVentana> restauraciones)
    {
        if (restauraciones.Count == 0) return estado;
        var cuales = new HashSet<IntPtr>(restauraciones
            .Where(e => e.Tipo == TipoEfecto.Restaurar).Select(e => e.Hwnd));
        if (cuales.Count == 0) return estado;

        var lista = estado.Ventanas
            .Select(v => cuales.Contains(v.Hwnd)
                ? v with { Maximizada = false, Minimizada = false }
                : v)
            .ToList();
        return estado with { Ventanas = lista };
    }
}
