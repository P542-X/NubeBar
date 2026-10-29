using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.2: la única puerta por la que se mueve una ventana.
//
// ---------- Por qué hace falta una puerta ----------
//
// Las tres redes de D9.1 —respetar tu gesto, el tope de ritmo y el deshacer— existen,
// están probadas y **no las consulta nadie**. Un cinturón de seguridad en el maletero.
//
// El error fácil sería consultarlas en cada sitio que mueva algo: el atajo, la regla, el
// gesto de la vista general, el arnés. Cuatro sitios que hay que acordarse de actualizar, y
// el día que se añada el quinto se moverán ventanas sin pasar por ninguna red.
//
// Así que hay un solo camino: **nadie llama a `HyprAdaptador.Aplicar` directamente**. Se
// pasa por aquí, y aquí se decide qué efectos sobreviven, se apunta lo que había para poder
// deshacerlo y se le dice al ritmo qué se movió.
//
// ---------- Lo que esto NO decide ----------
//
// El modo. Si la capa está apagada o en simulación, el adaptador ya no mueve nada —eso es
// de D0 y sigue intacto—. Esta puerta filtra **además**, y por motivos que el adaptador no
// conoce: él sabe mover, no sabe que tú colocaste esa ventana a mano hace diez minutos.
public static class HyprAplicador
{
    // Por qué se descartó un efecto. En palabras, no en código: lo va a leer un humano
    // buscando por qué su ventana no se movió.
    public sealed record Descartado(EfectoVentana Efecto, string Porque)
    {
        public override string ToString() =>
            $"0x{Efecto.Hwnd.ToInt64():X6}: {Porque}";
    }

    public sealed record Filtro(
        List<EfectoVentana> Pasan,
        List<Descartado> Fuera)
    {
        public bool HayAlgo => Pasan.Count > 0;

        public string Resumen =>
            Fuera.Count == 0
                ? $"{Pasan.Count} efecto(s), ninguno descartado"
                : $"{Pasan.Count} efecto(s), {Fuera.Count} descartado(s)";
    }

    // ================= El filtro =================
    //
    // El orden de las comprobaciones importa, y es de más barato a más caro... pero sobre
    // todo de más definitivo a menos:
    //
    //  1. **El respeto** va primero porque es permanente: una ventana que tocaste no se
    //     mueve hoy ni dentro de una hora. Preguntarle al ritmo por ella sería gastar cupo
    //     en algo que no va a pasar nunca.
    //  2. **El ritmo** después, y solo para las que sobrevivieron. Y `Pregunta` no apunta:
    //     se apunta cuando el movimiento sale bien, en `Salio`.
    //
    // Los efectos que no mueven nada —enfocar, "se queda"— pasan sin preguntar: el ritmo
    // cuenta movimientos, y marcar una ventana como tocada por haberla enfocado sería
    // desactivar las reglas por mirar.
    public static Filtro Filtrar(IReadOnlyList<EfectoVentana> efectos,
                                 HyprRespeto.Memoria respeto,
                                 HyprRitmo.Contador ritmo,
                                 DateTime ahora)
    {
        var pasan = new List<EfectoVentana>();
        var fuera = new List<Descartado>();

        // ---------- Primero, ¿cabe esta TANDA? ----------
        //
        // Una vez para todo el lote, no una por ventana. Un reparto en mosaico de diez
        // ventanas es UNA decisión, y cortarlo por la mitad dejaría media pantalla
        // ordenada y la otra media no — que es peor que no tocar nada.
        //
        // Si la tanda no cabe, se descartan todas con el mismo motivo. Nadie se queda a
        // medias, y el registro dice una sola razón en vez de diez iguales.
        var lote = ritmo.PreguntaLote(ahora);
        if (!lote.Puede)
            return new Filtro(efectos.Where(e => !Mueve(e)).ToList(),
                              efectos.Where(Mueve)
                                     .Select(e => new Descartado(e, lote.Porque)).ToList());

        foreach (var e in efectos)
        {
            // Lo que no mueve pasa sin preguntar: enfocar una ventana no es moverla, y si
            // contara, darle el foco la dejaría "recién movida" para el ritmo.
            if (!Mueve(e)) { pasan.Add(e); continue; }

            // El respeto va antes que la espera porque es permanente: una ventana que
            // tocaste no se mueve hoy ni dentro de una hora.
            if (!HyprRespeto.SeLePuedeTocar(e.Hwnd, respeto))
            {
                var cuando = respeto.Cuando(e.Hwnd);
                fuera.Add(new Descartado(e,
                    $"la moviste tú{(cuando is { } c ? $" a las {c:HH:mm:ss}" : "")}: " +
                    "las reglas no la tocan"));
                continue;
            }

            var suya = ritmo.PreguntaVentana(e.Hwnd, ahora);
            if (!suya.Puede) { fuera.Add(new Descartado(e, suya.Porque)); continue; }

            pasan.Add(e);
        }

        return new Filtro(pasan, fuera);
    }

    // ¿Este efecto mueve la ventana? Colocar y devolver sí; enfocar y "se queda", no.
    // Restaurar también mueve: saca de maximizada, que cambia el rectángulo.
    public static bool Mueve(EfectoVentana e) =>
        e.Tipo is TipoEfecto.Colocar or TipoEfecto.Restaurar or TipoEfecto.Devolver;

    // ================= Lo que había antes =================
    //
    // El lote de deshacer se construye **leyendo la geometría de ahora**, no el destino. Si
    // se apuntara el destino, "deshacer" devolvería las ventanas a donde ya están.
    //
    // `donde` es una función para que esto siga siendo puro: en producción es
    // `Nativo.Rectangulo`, y en las pruebas es lo que haga falta.
    public static List<HyprDeshacer.Antes> LoQueHabia(
        IReadOnlyList<EfectoVentana> efectos,
        Func<IntPtr, NubeHypr.Rect> donde,
        Func<IntPtr, bool> estaMaximizada)
    {
        var lista = new List<HyprDeshacer.Antes>();
        foreach (var e in efectos.Where(Mueve))
        {
            var r = donde(e.Hwnd);
            if (!r.Valido) continue;       // si no se puede leer, no se promete devolverla
            lista.Add(new HyprDeshacer.Antes(e.Hwnd, r, estaMaximizada(e.Hwnd)));
        }
        return lista;
    }

    // ================= Después de aplicar =================
    //
    // Dos cosas, y las dos importan:
    //
    //  - El ritmo apunta lo que se movió. Va aquí y no en `Filtrar` porque un efecto que se
    //    calculó y no llegó a aplicarse no gasta cupo.
    //  - El respeto apunta QUÉ se pidió para cada ventana. Sin esto, el propio movimiento se
    //    leería después como un gesto tuyo y la ventana quedaría marcada: el motor se
    //    desactivaría solo en la primera ventana que colocara.
    public static void Salio(IReadOnlyList<EfectoVentana> aplicados,
                             HyprRespeto.Memoria respeto,
                             HyprRitmo.Contador ritmo,
                             DateTime ahora)
    {
        var movidos = aplicados.Where(Mueve).ToList();
        if (movidos.Count == 0) return;

        // Una tanda, un apunte. Si se apuntara una por ventana, un reparto de diez se
        // comería el cupo del segundo él solo y el siguiente reparto legítimo se cortaría.
        ritmo.ApuntadoLote(ahora);

        foreach (var e in movidos)
        {
            ritmo.Apuntado(e.Hwnd, ahora);
            if (e.Destino.Valido) respeto.Pedimos(e.Hwnd, e.Destino);
        }
    }

    // ================= Las líneas =================
    public static List<string> Lineas(string porque, Filtro f, NubeHypr.Modo modo)
    {
        var lineas = new List<string>
        {
            $"[D9]  aplicar «{porque}»: {f.Resumen}, modo={modo.ToString().ToLowerInvariant()}" +
            (modo == NubeHypr.Modo.Aplicar ? "" : " (no se mueve nada)"),
        };
        foreach (var d in f.Fuera) lineas.Add("[D9]    descartado " + d);
        return lineas;
    }
}
