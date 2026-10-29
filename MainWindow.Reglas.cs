using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.5: cuando una regla coincide con una ventana nueva, colocarla.
//
// ---------- Dónde encaja esto ----------
//
// `HyprVigilante` (D5.1) resuelve las reglas y apunta la decisión. `HyprColocar` (lógica
// pura) la convierte en efectos. `AplicarConRedes` (D9.2) es la puerta única por la que
// pasa todo lo que mueve ventanas, con el respeto a tu gesto, el tope de ritmo y el
// apunte para deshacer. Esto son las treinta líneas que faltaban entre los tres.
//
// ---------- Y la espera ----------
//
// No se coloca en el mismo instante en que aparece la ventana. Muchas aplicaciones hacen su
// propio `SetWindowPos` unos milisegundos después de ser visibles —recuperar el sitio de la
// sesión anterior, centrarse, ajustarse al DPI—, y colocarla justo entonces es entrar en
// una pelea que se ve: la ventana salta dos veces y a veces gana la aplicación.
//
// Se espera `HyprColocar.LoQueTardaEnAsentarse` y se vuelve a mirar: si la ventana ya no
// está, no se hace nada. Es la misma comprobación que hace el adaptador antes de mover, y
// se hace aquí también porque entre la espera y la puerta hay otro hueco.
public partial class MainWindow
{
    // Cuántas ventanas ha colocado una regla en toda la vida del proceso. Para poder
    // AFIRMAR que un diagnóstico no colocó nada, igual que `HyprAdaptador.VecesMovida`.
    internal static long ColocadasPorRegla;

    // ---------- Y el de D9.6, que es OTRA cosa ----------
    //
    // `ColocadasPorRegla` cuenta ventanas que una regla colocó **una a una**. Un layout no
    // coloca ventanas una a una: reparte un escritorio, y lo hace sobre ventanas que ninguna
    // regla ha nombrado. Contar las dos cosas en el mismo sitio haría que «la regla colocó 3»
    // significara dos cosas distintas según qué regla fuera.
    //
    // Es la misma lección que `Enfocadas` frente a `Movidas` en el adaptador: cuando un
    // contador empieza a significar dos cosas, deja de servir para afirmar ninguna.
    internal static long RepartidasPorRegla;

    void ColocarPorRegla(HyprVigilante.Apunte a, HyprAdaptador.Observacion obs, NubeHypr.Modo modo)
    {
        try
        {
            var v = obs.Estado.Ventanas.FirstOrDefault(x => x.Hwnd == a.Hwnd);
            if (v == null) return;

            string? porQueNo = HyprColocar.PorQueNoSeAplica(
                _cfg.NubeHypr.Activo, modo, a.AlgunaCoincidio, a.Decision.DiceAlgo);

            var plan = HyprColocar.Planear(v, a.Decision, SitioDeLasReglas());
            foreach (var l in HyprColocar.Lineas(v, plan, porQueNo)) Log.Info(l);

            // ---------- Dos cosas distintas, y hacen falta las dos ----------
            //
            // `plan.HaceAlgo` responde «¿la regla dice algo sobre ESTA ventana?» — mira
            // `AlEspacio`, `Destino` y `Enfocar`. Es lo de D9.5.
            //
            // Pero una regla puede no decir nada sobre esta ventana y aun así pedir un
            // layout, que es una decisión sobre **el escritorio entero**. Y esa es, de
            // hecho, la forma más natural de escribirla: «cuando aparezca el navegador,
            // enlosa esta pantalla en horizontal» no dice nada del navegador.
            //
            // Hasta aquí ponía `if (porQueNo != null || !plan.HaceAlgo) return;`, y esa
            // segunda condición dejaba **D9.6 inalcanzable** por su caso principal: el
            // reparto solo ocurría si la regla, además del layout, movía la ventana que lo
            // disparó. Lo encontró `--arneslayout` al primer intento, midiendo tres ventanas
            // que se quedaron las tres donde nacieron.
            bool colocaEstaVentana = plan.HaceAlgo;
            bool reparteElEscritorio = HyprColocar.LaReglaRepiteElEspacio(a.Decision);
            if (porQueNo != null || (!colocaEstaVentana && !reparteElEscritorio)) return;

            // ---------- La espera, y volver a mirar ----------
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(HyprColocar.LoQueTardaEnAsentarse);

                    // La capa puede haberse apagado, o el modo haber cambiado, en lo que
                    // duró la espera. Se vuelve a preguntar: una decisión de hace un
                    // tercio de segundo no es permiso para mover ahora.
                    if (!HyprColocar.SeAplica(_cfg.NubeHypr.Activo, ModoDeLaCapa(),
                                              a.AlgunaCoincidio, a.Decision.DiceAlgo))
                    {
                        Log.Info($"[D9.5]   se deja: la capa cambió mientras se esperaba");
                        return;
                    }
                    if (!Nativo.Existe(a.Hwnd))
                    {
                        Log.Info("[D9.5]   se deja: la ventana ya no está");
                        return;
                    }

                    // D9.5: lo que la regla dice de ESTA ventana, si dice algo.
                    //
                    // `automatico: true`: esto se dispara porque has abierto una ventana, no
                    // porque hayas pedido nada. Es exactamente el caso para el que existe
                    // `ConfirmarMovimientos`, y con la opción puesta `r` viene `null` hasta
                    // que digas «Mover» — que es lo correcto, porque «colocadas por regla»
                    // cuenta ventanas movidas, no propuestas.
                    if (colocaEstaVentana)
                    {
                        var efectos = HyprColocar.Efectos(plan, Porque(a));
                        var r = AplicarConRedes(efectos, Porque(a), ModoDeLaCapa(), automatico: true);
                        if (r is { Movidas: > 0 }) ColocadasPorRegla++;
                    }

                    // D9.6: y el layout, que es una decisión sobre el escritorio entero y no
                    // sobre esta ventana. Va aparte y no dentro del `if` de arriba: una regla
                    // que SOLO pide layout tiene que repartir igual, y que no lo hiciera era
                    // el fallo que dejaba D9.6 sin camino.
                    if (reparteElEscritorio)
                        RepartirPorRegla(a, Porque(a));
                }
                catch (Exception ex)
                {
                    HyprSalud.Reventó();
                    Log.Error("colocar una ventana por una regla", ex);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            HyprSalud.Reventó();
            Log.Error("decidir si una regla coloca una ventana", ex);
        }
    }

    // ================= D9.6: repartir el escritorio =================
    //
    // La única parte de NubeHypr que mueve ventanas que ninguna regla ha nombrado, porque
    // un layout es por definición una decisión sobre el conjunto. Ver el bloque de
    // `HyprColocar.LaReglaRepiteElEspacio` para el razonamiento.
    //
    // El reparto lo calcula `HyprLayout.Calcular`, que es el MISMO que usan los atajos: una
    // segunda forma de enlosar sería la que un día coloque distinto según por dónde entres.
    void RepartirPorRegla(HyprVigilante.Apunte a, string porque)
    {
        try
        {
            var layout = a.Decision.Layout ?? NubeHypr.Layout.Flotante;

            // `Flotante` es el layout en el que manda el usuario: no coloca a nadie. Se
            // dice y se sale, en vez de calcular un reparto que no va a mover nada.
            if (layout == NubeHypr.Layout.Flotante)
            {
                Log.Info($"[D9.6] la regla pide layout «Flotante»: nadie se mueve, mandas tú");
                return;
            }

            // 0,6 es la proporción de serie del layout «Principal» (ver `EstadoNubeHypr`):
            // la grande se queda el 60 %. Es la misma que usan los atajos.
            var obs = HyprAdaptador.Observar(layout: layout, proporcion: 0.6);
            var m = Monitores.DeVentana(a.Hwnd) ?? Monitores.Principal();
            var h = ZonasAjuste.AreaUtilCon(m, EstorbosDeAhora());
            var area = new NubeHypr.Rect(h.X, h.Y, h.Ancho, h.Alto);

            var efectos = NubeHypr.Calcular(obs.Estado, area, Math.Max(0, _cfg.NubeHypr.Gap));
            int aColocar = efectos.Count(e => e.Tipo == TipoEfecto.Colocar);

            Log.Info(HyprColocar.LineaDelLayout(layout, aColocar, seAplica: true));
            foreach (var e in efectos) Log.Info("[D9.6]   " + e);

            // Por la puerta, como todo: el respeto a tu gesto deja fuera lo que has movido
            // a mano, el ritmo corta las ráfagas y el lote queda apuntado para deshacerlo.
            // Y automático con más razón que el anterior: un layout mueve ventanas que
            // ninguna regla ha nombrado. Si hay algo en toda la capa que merece una pregunta
            // antes de ejecutarse, es esto.
            var r = AplicarConRedes(efectos, porque + " (layout)", ModoDeLaCapa(),
                                    automatico: true);
            if (r != null)
            {
                Log.Info($"[D9.6]   repartidas {r.Movidas}, saltadas {r.Saltadas}");
                if (r.Movidas > 0) RepartidasPorRegla += r.Movidas;
            }
        }
        catch (Exception ex)
        {
            HyprSalud.Reventó();
            Log.Error("repartir el escritorio por una regla", ex);
        }
    }

    // De dónde viene el movimiento, para el registro y para el «deshacer». Con el nombre de
    // la regla, no un «reglas» genérico: cuando algo se mueva solo, lo primero que uno
    // quiere saber es CUÁL lo mandó.
    static string Porque(HyprVigilante.Apunte a)
    {
        var quien = a.Decision.Quien.Values.Distinct().ToList();
        return quien.Count switch
        {
            0 => "una regla",
            1 => $"la regla «{quien[0]}»",
            _ => $"las reglas «{string.Join("», «", quien)}»",
        };
    }

    // ---------- El sitio, tal y como lo ven las reglas ----------
    //
    // El área ÚTIL de cada monitor, no su rectángulo entero: si una regla dice «medio
    // monitor», ese medio no puede quedar por debajo de la barra. Se reaprovecha el mismo
    // cálculo de estorbos que usa el ajuste de ventanas (E5), para que una regla y un
    // arrastre a un borde den el mismo rectángulo.
    // El modo que vale AHORA, pasado por el modo seguro: si el supervisor ha bajado la
    // capa por salud, aquí tiene que verse apagada igual que en el resto del programa.
    NubeHypr.Modo ModoDeLaCapa() =>
        HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);

    HyprColocar.Sitio SitioDeLasReglas()
    {
        var areas = new List<NubeHypr.Rect>();
        try
        {
            foreach (var m in Monitores.Todos())
            {
                var h = ZonasAjuste.AreaUtilCon(m, EstorbosDeAhora());
                areas.Add(new NubeHypr.Rect(h.X, h.Y, h.Ancho, h.Alto));
            }
        }
        catch (Exception ex) { Log.Error("leer el área de los monitores para las reglas", ex); }

        int espacios = 1;
        try { espacios = Math.Max(1, Escritorios.Todos().Count); }
        catch { }

        return new HyprColocar.Sitio(areas, espacios);
    }
}
