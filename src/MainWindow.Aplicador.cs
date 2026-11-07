using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.2: la puerta única, enganchada.
//
// ---------- La regla ----------
//
// **Nadie llama a `HyprAdaptador.Aplicar` directamente.** Se pasa por `AplicarConRedes`, y
// ahí se consulta el respeto a tu gesto, el tope de ritmo y se apunta lo que había para
// poder deshacerlo.
//
// Sin esta puerta, las tres redes de D9.1 son un cinturón en el maletero: existen, están
// probadas y no las mira nadie. Y consultarlas en cada sitio que mueva algo —el atajo, la
// regla, el gesto, el arnés— serían cuatro sitios que hay que acordarse de actualizar.
public partial class MainWindow
{
    readonly HyprRitmo.Contador _ritmoHypr = new();
    readonly HyprDeshacer.Pila _deshacerHypr = new();

    // Devuelve lo que pasó, para que quien llame pueda contarlo. `null` significa que no
    // quedó nada que aplicar — que no es un error: es el caso normal cuando todas las
    // ventanas del reparto son tuyas de haberlas movido a mano.
    // `automatico` dice si esto lo ha pedido NubeHypr por su cuenta (una regla, un reparto al
    // cambiar de layout) o si el usuario acaba de ordenarlo (un atajo, el arnés). Solo lo
    // primero se confirma: ver la nota larga en `HyprConfirmar.HaceFalta`.
    //
    // `yaConfirmado` lo pone únicamente la respuesta del usuario al volver a entrar con el
    // permiso dado. Nadie más tiene por qué ponerlo.
    HyprAdaptador.Resultado? AplicarConRedes(IReadOnlyList<EfectoVentana> efectos,
                                             string porque, NubeHypr.Modo modo,
                                             bool automatico = false, bool yaConfirmado = false)
    {
        if (efectos.Count == 0) return null;

        var ahora = DateTime.Now;
        var filtro = HyprAplicador.Filtrar(efectos, _respetoHypr, _ritmoHypr, ahora);
        foreach (var l in HyprAplicador.Lineas(porque, filtro, modo)) Log.Info(l);

        if (!filtro.HayAlgo) return null;

        // ---------- Y antes de mover nada, preguntar si toca ----------
        //
        // Aquí y no antes: se pregunta por lo que de verdad va a moverse, después de que el
        // respeto a tu gesto y el tope de ritmo hayan descartado lo suyo. Preguntar por
        // `efectos` en bruto diría «mover 8 ventanas» y moverían 3.
        //
        // Y aquí y no después: el apunte para deshacer no se toma hasta que hay permiso. Si
        // se tomara antes, una pregunta sin contestar dejaría un lote en la pila de deshacer
        // que nunca se aplicó, y «deshacer» devolvería ventanas que nadie movió.
        if (HyprConfirmar.HaceFalta(modo, _cfg.NubeHypr.ConfirmarMovimientos,
                                    automatico, yaConfirmado))
        {
            PedirConfirmacionDeHypr(filtro.Pasan, porque);
            return null;
        }

        // ---------- Lo que había, ANTES de mover ----------
        //
        // Leyendo de Windows, no del snapshot: el snapshot puede ser de hace un segundo y
        // "deshacer" devolvería las ventanas a donde estaban hace un segundo, no a donde
        // están. Y solo si de verdad se va a mover: en simulación no hay nada que deshacer.
        if (modo == NubeHypr.Modo.Aplicar)
        {
            var habia = HyprAplicador.LoQueHabia(
                filtro.Pasan,
                h => HyprAdaptador.DeRect(Nativo.Rectangulo(h)),
                h => Nativo.EstaMaximizada(h));

            if (_deshacerHypr.Apuntar(porque, habia))
                Log.Info($"[D9]  apuntado para deshacer: {habia.Count} ventana(s) de «{porque}»");
        }

        var r = HyprAdaptador.Aplicar(NubeHypr.SiguienteVersion(), filtro.Pasan, modo);
        foreach (var l in r.Lineas) Log.Info(l);

        // ---------- Y lo que se movió, apuntado ----------
        //
        // En simulación no: no se ha movido nada, así que no gasta cupo de ritmo ni hay un
        // destino que recordar. Si se apuntara igual, una tanda de simulaciones dejaría al
        // ritmo creyendo que acaba de mover veinte ventanas.
        if (modo == NubeHypr.Modo.Aplicar && r.Movidas > 0)
            HyprAplicador.Salio(filtro.Pasan, _respetoHypr, _ritmoHypr, ahora);

        return r;
    }

    // ================= Deshacer =================
    //
    // El comando número 20. Saca el último lote y lo devuelve **por el mismo camino**, que
    // es lo que hace que deshacer no sea una segunda forma de mover ventanas con sus
    // propias reglas.
    //
    // Y con una vuelta de tuerca: al deshacer se **sueltan las marcas de gesto** de esas
    // ventanas. Si no, deshacer colocaría la ventana donde tú la habías puesto y acto
    // seguido la marca seguiría ahí — que es lo correcto— pero el lote no se podría volver
    // a aplicar nunca. Soltarla deja las cosas como estaban antes del lote, que es lo que
    // "deshacer" promete.
    void DeshacerLoUltimo(NubeHypr.Modo modo)
    {
        var vivas = GestorVentanas.Todas.Select(w => w.Hwnd).ToList();
        var (lote, efectos) = _deshacerHypr.Deshacer(vivas);

        Log.Info(HyprDeshacer.Linea(lote, efectos.Count, modo));
        if (lote == null || efectos.Count == 0)
        {
            // Este es EL caso que necesita indicador. Deshacer sin nada que deshacer no
            // produce ni un parpadeo en pantalla: sin aviso es indistinguible de un atajo
            // que no está registrado.
            Avisar(HyprAviso.DelDeshacer(0, modo != NubeHypr.Modo.Aplicar));
            return;
        }

        foreach (var e in efectos) _respetoHypr.Soltar(e.Hwnd);

        // Va directo al adaptador y NO por `AplicarConRedes`: el ritmo cortaría justamente
        // lo que el usuario acaba de pedir —deshacer viene siempre pegado a lo que se quiere
        // deshacer— y el respeto descartaría las ventanas cuya marca se acaba de soltar.
        // Deshacer es una orden explícita, no una decisión automática: las redes están para
        // lo segundo.
        var r = HyprAdaptador.Aplicar(NubeHypr.SiguienteVersion(), efectos, modo);
        foreach (var l in r.Lineas) Log.Info(l);
        Log.Info($"[D9]  deshecho: {r.Movidas} movida(s), {r.Saltadas} saltada(s)");

        // Lo que se cuenta es lo que se MOVIÓ, no lo que se intentó: si de cinco ventanas
        // tres ya no existen, «Deshacer · 2 ventanas» es la verdad y «5» sería un número
        // inventado que el usuario puede desmentir mirando la pantalla.
        Avisar(HyprAviso.DelDeshacer(modo == NubeHypr.Modo.Aplicar ? r.Movidas : efectos.Count,
                                     modo != NubeHypr.Modo.Aplicar));
    }
}
