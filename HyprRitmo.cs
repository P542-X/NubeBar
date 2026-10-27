using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.1: un tope de ritmo, para que aplicar no se convierta en una ráfaga.
//
// ---------- El caso que esto evita ----------
//
// Abres un proyecto en el editor y se abren seis ventanas en dos segundos. Abres el
// navegador y salen cuatro. Un instalador abre y cierra diálogos en cadena. Sin un tope,
// cada una de esas apariciones es un `SetWindowPos` inmediato: la pantalla se convierte en
// un baile, el equipo se atasca justo en el momento de más trabajo, y si algo va mal son
// diez movimientos equivocados en vez de uno.
//
// ---------- Y por qué no vale "esperar un poco" ----------
//
// Porque hay dos límites distintos y uno solo no cubre los dos:
//
//  - **Por ventana**: la misma ventana no se recoloca dos veces en medio segundo. Esto es
//    lo que corta los bucles: una app que se resiste y se recoloca sola provocaría que la
//    regla insista, y eso es una pelea infinita a sesenta fotogramas por segundo.
//  - **En total**: no más de un puñado por segundo entre todas. Esto es lo que corta la
//    ráfaga del editor que abre seis.
//
// Todo con el reloj inyectado, así que se puede comprobar sin esperar de verdad.
public static class HyprRitmo
{
    // Medio segundo por ventana: más que cualquier recolocación legítima y menos de lo que
    // se nota al abrir algo. Y cuatro por segundo en total: suficiente para que abrir un
    // proyecto coloque sus ventanas en orden, y poco para que no sea un baile.
    public static readonly TimeSpan EsperaPorVentana = TimeSpan.FromMilliseconds(500);
    public const int MaxPorSegundo = 4;
    public static readonly TimeSpan Ventana = TimeSpan.FromSeconds(1);

    // Y un tope de seguridad: si en un minuto se han pedido más de esto, algo está en bucle
    // y lo que toca no es seguir aplicando más despacio, es **dejar de aplicar** y que el
    // supervisor lo vea. Preferir no hacer nada, otra vez.
    public const int MaxPorMinuto = 60;
    public static readonly TimeSpan Minuto = TimeSpan.FromMinutes(1);

    public enum Veredicto { Adelante, DemasiadoPronto, DemasiadosAhora, EnBucle }

    public sealed record Respuesta(Veredicto Que, string Porque)
    {
        public bool Puede => Que == Veredicto.Adelante;
        public override string ToString() => Puede ? "adelante" : $"{Que}: {Porque}";
    }

    // ---------- La unidad del tope en total es la TANDA, no la ventana ----------
    //
    // Y esto lo destapó una prueba. La primera versión preguntaba por cada ventana y
    // contaba cada ventana, así que un reparto en mosaico de diez ventanas —que es UNA
    // decisión— se habría quedado en cuatro colocadas y seis donde estaban. **Medio mosaico
    // es peor que ninguno**: el usuario ve media pantalla ordenada y la otra media no, y no
    // tiene forma de saber por qué.
    //
    // Una tanda es un acto: un reparto, una regla que dispara, un atajo. El tope en total
    // cuenta actos. Lo que sigue siendo por ventana es la espera, que es otra cosa: protege
    // de la pelea entre una aplicación que se recoloca sola y una regla que insiste.
    public sealed class Contador
    {
        // Cuándo se tocó por última vez cada ventana, y el histórico reciente de TANDAS.
        readonly Dictionary<IntPtr, DateTime> _ultimaPorVentana = new();
        readonly List<DateTime> _tandas = new();

        public int Cuantas => _ultimaPorVentana.Count;
        public int EnElUltimoMinuto => _tandas.Count;

        // ---------- ¿Se puede hacer ESTA tanda? ----------
        //
        // El tope en total y el de seguridad. Se pregunta una vez por tanda, antes de mirar
        // ventana por ventana: si la tanda no cabe, no hace falta preguntar por ninguna.
        public Respuesta PreguntaLote(DateTime ahora)
        {
            Olvidar(ahora);

            if (_tandas.Count >= MaxPorMinuto)
                return new(Veredicto.EnBucle,
                           $"{_tandas.Count} tandas en el último minuto: " +
                           "algo está en bucle, se deja de aplicar");

            int enLaVentana = _tandas.Count(t => ahora - t < Ventana);
            if (enLaVentana >= MaxPorSegundo)
                return new(Veredicto.DemasiadosAhora,
                           $"{enLaVentana} tandas en el último segundo " +
                           $"(el tope es {MaxPorSegundo})");

            return new(Veredicto.Adelante, "");
        }

        // ---------- ¿Y esta ventana en concreto? ----------
        //
        // Solo la espera. Es lo que corta el bucle de una aplicación que se recoloca sola.
        public Respuesta PreguntaVentana(IntPtr hwnd, DateTime ahora)
        {
            if (_ultimaPorVentana.TryGetValue(hwnd, out var ultima))
            {
                var desde = ahora - ultima;
                if (desde < EsperaPorVentana)
                    return new(Veredicto.DemasiadoPronto,
                               $"esta ventana se colocó hace {desde.TotalMilliseconds:0} ms " +
                               $"(hacen falta {EsperaPorVentana.TotalMilliseconds:0})");
            }
            return new(Veredicto.Adelante, "");
        }

        // Se apunta DESPUÉS de aplicar, no al preguntar: si se apuntara al preguntar, una
        // consulta que acaba en "no" gastaría cupo igual que un movimiento de verdad, y dos
        // consultas seguidas bloquearían una ventana que no se ha movido nunca.
        public void ApuntadoLote(DateTime ahora)
        {
            _tandas.Add(ahora);
            Olvidar(ahora);
        }

        public void Apuntado(IntPtr hwnd, DateTime ahora)
        {
            _ultimaPorVentana[hwnd] = ahora;
            Olvidar(ahora);
        }

        void Olvidar(DateTime ahora)
        {
            _tandas.RemoveAll(t => ahora - t > Minuto);

            // Y las ventanas de las que ya no interesa la última vez: con el tope por
            // ventana en medio segundo, lo de hace un minuto no decide nada.
            foreach (var h in _ultimaPorVentana.Where(p => ahora - p.Value > Minuto)
                                               .Select(p => p.Key).ToList())
                _ultimaPorVentana.Remove(h);
        }

        public void Vaciar() { _ultimaPorVentana.Clear(); _tandas.Clear(); }
    }

    // El renglón del registro. Un "no" tiene que decir por qué: "mi regla no colocó esta
    // ventana" se contesta leyendo que llegó en mitad de una ráfaga.
    public static string Linea(IntPtr hwnd, Respuesta r) =>
        $"[D9]  ritmo hwnd=0x{hwnd.ToInt64():X6}: {r}";
}
