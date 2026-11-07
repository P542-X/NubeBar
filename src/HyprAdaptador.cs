using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NubeBar;

// D2: el adaptador. Lo único de NubeHypr que habla con Windows.
//
// ---------- La regla de oro ----------
//
// Observar primero, mover después, confirmar al final. Y nunca calcular con un monitor
// para aplicar con otro implícito.
//
// El modelo (D1) decide y no sabe que existe Win32. Este fichero traduce en los dos
// sentidos: convierte el snapshot de `GestorVentanas` en `EstadoNubeHypr`, y convierte los
// `EfectoVentana` en llamadas de verdad. Nada más. Si aquí aparece una decisión de layout,
// está en el sitio equivocado.
//
// ---------- Y por qué el modo por defecto es SIMULACIÓN ----------
//
// Porque `SetWindowPos` tiene efectos visibles inmediatos y no se puede deshacer con una
// disculpa. En simulación se calcula todo, se apunta destino por destino y **no se mueve
// nada**: el log dice exactamente lo que habría pasado. Mover de verdad es una decisión
// explícita del usuario, con la capa encendida y el modo en "aplicar".
public static class HyprAdaptador
{
    // ---------- El contador de movimientos REALES ----------
    //
    // Sube una sola vez por `Nativo.Colocar` que de verdad se haya llamado. Existe para
    // poder AFIRMAR que un diagnóstico no movió nada en vez de suponerlo: `--diagreglas`
    // lo imprime al terminar y tiene que ser 0.
    public static long VecesMovida;

    // ================= Observar =================
    //
    // El snapshot ya existe y es el de `GestorVentanas`: eventos de Windows, sin sondeo,
    // con sus contadores y su aviso de cambio. Escribir aquí un segundo enumerador sería
    // tener dos verdades sobre las mismas ventanas, y la que se quedara atrás sería la que
    // se usara para mover.
    //
    // Lo que SÍ se hace aquí es el filtro: la lista protegida de D0, con los datos reales
    // de cada ventana (su clase, si es nuestra, si Windows nos deja tocarla).
    public sealed record Observacion(
        EstadoNubeHypr Estado,
        long Version,
        IReadOnlyList<(IntPtr Hwnd, string App, string Porque)> Descartadas)
    {
        public int Cuantas => Estado.Ventanas.Count;
    }

    public static Observacion Observar(int espacioActual = 0,
                                       NubeHypr.Layout layout = NubeHypr.Layout.Principal,
                                       double proporcion = 0.6)
    {
        long version = NubeHypr.SiguienteVersion();
        var dentro = new List<VentanaModelo>();
        var fuera = new List<(IntPtr, string, string)>();

        foreach (var w in GestorVentanas.Todas)
        {
            bool rectValido = w.Rect.Der > w.Rect.Izq && w.Rect.Aba > w.Rect.Arr;
            if (!NubeHypr.SePuedeOrganizar(w.Exe, Nativo.Clase(w.Hwnd), Nativo.EsNuestra(w.Hwnd),
                                           w.SePuedeActivar, rectValido, out string porque))
            {
                fuera.Add((w.Hwnd, w.App, porque));
                continue;
            }

            dentro.Add(new VentanaModelo(
                w.Hwnd, NubeHypr.NombreDeProceso(w.Exe), w.Titulo,
                DeRect(w.Rect),
                Visible: w.Visible,
                Minimizada: w.Estado == EstadoVentana.Minimizada,
                Maximizada: w.Estado == EstadoVentana.Maximizada,
                Flotante: false,
                Escritorio: espacioActual,
                Monitor: 0));
        }

        var estado = EstadoNubeHypr.Vacio with
        {
            Ventanas = dentro,
            Foco = GestorVentanas.Activa,
            Layout = layout,
            Proporcion = proporcion,
            EspacioActual = espacioActual,
        };
        return new Observacion(estado, version, fuera);
    }

    // La conversión, explícita y en un solo sitio. `Nativo.RECT` lleva los cuatro bordes;
    // `NubeHypr.Rect` lleva origen y tamaño, los dos en píxeles de pantalla. Hacer esta
    // resta a mano en cada sitio es como se cuela un ancho que en realidad era un borde.
    public static NubeHypr.Rect DeRect(Nativo.RECT r) =>
        new(r.Izq, r.Arr, r.Der - r.Izq, r.Aba - r.Arr);

    // ================= Aplicar =================
    //
    // ---------- El orden seguro, paso por paso ----------
    //
    // Es el del apéndice 16.2 de la especificación, y cada paso tiene su "si falla":
    //
    //  1. el destino, ya resuelto -> si no es válido, no se aplica nada (puro)
    //  2. si es simulación        -> se apunta y se acaba: simular NO llama a Windows
    //  3. comprobar el hwnd       -> si no existe, se descarta SOLO esa ventana
    //  4. mover                   -> se registra el resultado y se continúa
    //  5. confirmar con un snapshot nuevo
    //
    // El orden de 2 y 3 importa: con la comprobación del hwnd por delante, simular
    // llamaba a Win32 y dejaba de ser algo que se puede razonar y comprobar en frío.
    //
    // Y dos cosas que NO se hacen nunca al mover: activar la ventana y cambiarle el orden
    // Z. `Nativo.Colocar` ya pasa `SWP_NOACTIVATE | SWP_NOZORDER`. Organizar no puede
    // robarte el foco ni reordenarte las ventanas por detrás: eso es lo que convierte un
    // gestor de ventanas en una molestia.
    public sealed record Resultado(
        long Version, int Movidas, int Saltadas, int Viejas, bool Simulado,
        IReadOnlyList<string> Lineas)
    {
        public bool Divergio { get; init; }

        // Cuántos focos se dieron. Aparte de `Movidas` porque enfocar no es mover: ver el
        // comentario en el bucle y la prueba de D2 que lo fija.
        public int Enfocadas { get; init; }
    }

    public static Resultado Aplicar(long version, IReadOnlyList<EfectoVentana> efectos,
                                    NubeHypr.Modo modo, bool confirmar = true)
    {
        var lineas = new List<string>();
        int movidas = 0, saltadas = 0, viejas = 0, enfocadas = 0;
        bool simulado = modo != NubeHypr.Modo.Aplicar;

        // ---------- El snapshot viejo no gana ----------
        //
        // Si entre calcular y aplicar ha llegado otra observación, estos efectos son de
        // un mundo que ya no existe. Se descartan enteros: aplicar la mitad de un reparto
        // viejo deja la pantalla peor que no tocarla.
        if (NubeHypr.EsVieja(version))
        {
            lineas.Add($"[D2]  descartado: los efectos son de la versión {version} y la última " +
                       $"es la {NubeHypr.UltimaVersion}");
            return new Resultado(version, 0, 0, efectos.Count, simulado, lineas);
        }

        foreach (var ef in efectos)
        {
            // ---------- Los efectos que no son «colocar» ----------
            //
            // Hasta D9.5 esto era un `continue` que solo apuntaba `SeQueda`: `AEspacio` y
            // `Enfocar` se tiraban EN SILENCIO. No es un detalle de poco: «cuando aparezca
            // Discord, enviarlo al Escritorio 2» es el ejemplo con el que se abre la guía
            // de reglas, y no podía ocurrir ni con el modo en «aplicar», ni dejaba rastro
            // de por qué no. El modelo los producía, el adaptador los ignoraba, y entre los
            // dos no se quejaba nadie.
            if (ef.Tipo == TipoEfecto.SeQueda)
            {
                // `SeQueda` ya trae su motivo del modelo; se apunta y no se toca nada.
                saltadas++;
                lineas.Add($"[D2]  skip  hwnd=0x{ef.Hwnd.ToInt64():X6} porque={ef.Porque}");
                continue;
            }

            if (ef.Tipo == TipoEfecto.AEspacio)
            {
                if (simulado)
                {
                    movidas++;
                    lineas.Add($"[D2]  simular hwnd=0x{ef.Hwnd.ToInt64():X6} -> espacio {ef.Espacio}");
                    continue;
                }
                if (!Nativo.Existe(ef.Hwnd))
                {
                    saltadas++;
                    lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, "sin-ventana", 0));
                    continue;
                }

                var relojE = Stopwatch.StartNew();
                string comoFue;
                try
                {
                    // El espacio del modelo es un ÍNDICE desde 1 —así se escribe en una
                    // regla: «Escritorio 2»— y Windows los identifica por GUID. La
                    // traducción vive aquí, que es el único sitio que habla con Windows.
                    var espacios = Escritorios.Espacios();
                    int i = ef.Espacio - 1;
                    if (i < 0 || i >= espacios.Count)
                    {
                        comoFue = $"no-existe-el-espacio-{ef.Espacio}";
                        saltadas++;
                    }
                    else
                    {
                        comoFue = Escritorios.Enviar(ef.Hwnd, espacios[i].Id)
                                ? "ok" : "windows-dijo-que-no";
                        if (comoFue == "ok") { VecesMovida++; movidas++; } else saltadas++;
                    }
                }
                catch (Exception ex) { comoFue = "error:" + ex.GetType().Name; saltadas++; }
                relojE.Stop();
                lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, $"espacio {ef.Espacio}: {comoFue}",
                                                 relojE.Elapsed.TotalMilliseconds));
                continue;
            }

            if (ef.Tipo == TipoEfecto.Enfocar)
            {
                // Enfocar NO cuenta ni como movida ni como saltada, y eso es una decisión
                // de D2 que ya estaba escrita y comprobada: no es un movimiento, y meterlo
                // en el recuento haría que el log dijera que se movieron tres ventanas
                // cuando se movieron dos. Lleva su propio contador.
                if (simulado)
                {
                    enfocadas++;
                    lineas.Add($"[D2]  simular hwnd=0x{ef.Hwnd.ToInt64():X6} -> enfocar");
                    continue;
                }
                if (!Nativo.Existe(ef.Hwnd))
                {
                    lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, "enfocar: sin-ventana", 0));
                    continue;
                }

                var relojF = Stopwatch.StartNew();
                string foco;
                try { Nativo.Activar(ef.Hwnd); foco = "ok"; enfocadas++; }
                catch (Exception ex) { foco = "error:" + ex.GetType().Name; }
                relojF.Stop();
                lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, "enfocar: " + foco,
                                                 relojF.Elapsed.TotalMilliseconds));
                continue;
            }

            if (ef.Tipo != TipoEfecto.Colocar) continue;

            // 1. ¿El destino es un rectángulo de verdad? Es una comprobación pura y va
            //    primero: simular un movimiento imposible daría un log que promete algo
            //    que no se podría hacer.
            if (!ef.Destino.Valido)
            {
                saltadas++;
                lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, "destino-invalido", 0));
                continue;
            }

            // 2. En simulación se apunta y se acaba aquí. Es el camino por defecto, y no
            //    llama a Windows ni para preguntar: simular es un CÁLCULO. Preguntar por
            //    la ventana aquí convertía la simulación en algo que no se puede razonar
            //    ni comprobar en frío, que es justo para lo que existe.
            if (simulado)
            {
                movidas++;
                lineas.Add($"[D2]  simular hwnd=0x{ef.Hwnd.ToInt64():X6} -> {ef.Destino}");
                continue;
            }

            // 3. ¿Sigue ahí la ventana? Esta comprobación protege la llamada REAL, y por
            //    eso va justo antes de ella: entre calcular y aplicar, una ventana puede
            //    haberse cerrado.
            if (!Nativo.Existe(ef.Hwnd))
            {
                saltadas++;
                lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, "sin-ventana", 0));
                continue;
            }

            // 4. Mover de verdad, midiendo lo que tarda
            var reloj = Stopwatch.StartNew();
            string resultado;
            try
            {
                Nativo.Colocar(ef.Hwnd,
                               (int)Math.Round(ef.Destino.X), (int)Math.Round(ef.Destino.Y),
                               (int)Math.Round(ef.Destino.Ancho), (int)Math.Round(ef.Destino.Alto));
                resultado = "ok";
                VecesMovida++;   // el contador de "sí se tocó una ventana de verdad"
                movidas++;
            }
            catch (Exception ex)
            {
                resultado = "error:" + ex.GetType().Name;
                saltadas++;
            }
            reloj.Stop();
            lineas.Add(NubeHypr.LineaAplicar(ef.Hwnd, resultado, reloj.Elapsed.TotalMilliseconds));
        }

        // 5. Confirmar: se vuelve a mirar y se compara con lo que se pidió.
        //
        // Confirmar no es ceremonia. Windows puede no obedecer —una ventana con tamaño
        // mínimo propio, una app que se recoloca sola, un monitor que no acepta esa
        // posición— y sin comparar, el log diría "ok" de algo que no pasó.
        bool divergio = false;
        if (confirmar && !simulado)
        {
            var (iguales, distintas) = Confirmar(efectos);
            divergio = distintas.Count > 0;
            foreach (var d in distintas) lineas.Add("[D2]  diverge " + d);
            lineas.Add($"[D2]  confirmadas={iguales} divergentes={distintas.Count}");
        }

        // ---------- D7: y el supervisor se entera ----------
        //
        // Las rachas se cuentan aquí porque aquí es donde se sabe si salió bien. Una ventana
        // que no se deja mover es normal; quince seguidas es que el adaptador no está
        // haciendo nada útil, y entonces el supervisor apaga la capa en vez de seguir
        // intentándolo. Un acierto borra la racha: cinco fallos repartidos en una semana no
        // son lo mismo que cinco seguidos.
        //
        // En simulación no se cuenta nada: no se ha intentado mover, así que no hay ni
        // acierto ni fallo que contar — y un diagnóstico en simulación no puede empujar a
        // la capa hacia el modo seguro.
        if (!simulado)
        {
            if (saltadas > 0 || divergio) HyprSalud.SalioMal();
            else if (movidas > 0) HyprSalud.SalioBien();
        }

        lineas.Add(NubeHypr.LineaConfirmar(version, movidas, saltadas, viejas));
        return new Resultado(version, movidas, saltadas, viejas, simulado, lineas)
        { Divergio = divergio, Enfocadas = enfocadas };
    }

    // ¿Acabó cada ventana donde se le pidió? Se vuelve a leer su rectángulo de Windows,
    // no del snapshot: el snapshot puede ser de antes de mover.
    static (int Iguales, List<string> Distintas) Confirmar(IReadOnlyList<EfectoVentana> efectos)
    {
        int iguales = 0;
        var distintas = new List<string>();
        foreach (var ef in efectos.Where(e => e.Tipo == TipoEfecto.Colocar))
        {
            if (!Nativo.Existe(ef.Hwnd)) continue;      // se cerró mientras: no es divergencia
            var ahora = DeRect(Nativo.Rectangulo(ef.Hwnd));
            // Tolerancia generosa a propósito: muchas ventanas tienen bordes invisibles
            // de redimensionado y su rectángulo "real" no es el que se ve. Exigir el
            // píxel exacto marcaría como divergente media pantalla.
            if (ahora.CasiIgual(ef.Destino, 8)) iguales++;
            else distintas.Add($"0x{ef.Hwnd.ToInt64():X6} pedido={ef.Destino} real={ahora}");
        }
        return (iguales, distintas);
    }

    // ================= Un ciclo entero =================
    //
    // Observar, calcular, aplicar y apuntar, con las cuatro líneas del formato de la
    // especificación atadas por el mismo número de versión. Es lo que ejecuta el
    // diagnóstico y lo que ejecutará el motor cuando tenga quien le dé la orden.
    public static Resultado Ciclo(NubeHypr.Rect area, NubeHypr.Layout layout, double hueco,
                                  double proporcion, NubeHypr.Modo modo,
                                  Action<string>? apuntar = null, IntPtr soloEsta = default)
    {
        void Di(string s) { Log.Info(s); apuntar?.Invoke(s); }

        var obs = Observar(layout: layout, proporcion: proporcion);
        Di(NubeHypr.LineaSnapshot(obs.Version, obs.Cuantas, obs.Estado.Foco));
        foreach (var (hwnd, app, porque) in obs.Descartadas)
            Di($"[D2]  fuera hwnd=0x{hwnd.ToInt64():X6} {app} porque={porque}");

        var efectos = NubeHypr.Calcular(obs.Estado, area, hueco);

        // ---------- Empezar por UNA ventana ----------
        //
        // La especificación lo pide así y tiene razón: `SetWindowPos` puede dar efectos
        // visibles inesperados, y la forma de descubrirlo no es con doce ventanas a la
        // vez. Con `soloEsta` el ciclo calcula el reparto completo —para que el destino
        // sea el de verdad, no el de una ventana sola— y aplica solo a esa.
        if (soloEsta != IntPtr.Zero)
        {
            efectos = efectos.Where(e => e.Hwnd == soloEsta).ToList();
            Di($"[D2]  solo-una hwnd=0x{soloEsta.ToInt64():X6}: se calcula el reparto " +
               $"completo y se aplica a esta");
        }

        int monitor = 0;
        var primera = efectos.FirstOrDefault(e => e.Tipo == TipoEfecto.Colocar);
        if (primera != null) Di(NubeHypr.LineaPolitica(layout.ToString().ToLowerInvariant(), monitor, primera.Destino));

        var r = Aplicar(obs.Version, efectos, modo);
        foreach (var l in r.Lineas) Di(l);
        return r;
    }
}
