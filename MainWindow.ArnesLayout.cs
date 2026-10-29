using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NubeBar;

// El runner de `--arneslayout`. Ver ArnesLayout.cs para las reglas y el porqué.
public partial class MainWindow
{
    async Task CorrerArnesDeLayout()
    {
        // Todo lo que hay que limpiar, declarado FUERA del try: el `finally` tiene que poder
        // limpiarlo aunque la excepción saltara justo después de crearlo. Es la misma regla
        // que `--arnesespacios`, y viene de que una limpieza que no corre deja escritorios y
        // procesos sueltos en la máquina de alguien.
        var procesos = new List<System.Diagnostics.Process>();
        var copias = new List<string>();
        Guid dePrueba = Guid.Empty, elOriginal = Guid.Empty;
        var ordenDelUsuarioAntes = new List<Guid>();

        var reglasAntes = _cfg.NubeHypr.Reglas;
        bool activoAntes = _cfg.NubeHypr.Activo;
        string modoAntes = _cfg.NubeHypr.Modo;
        bool confirmarAntes = _cfg.NubeHypr.ConfirmarMovimientos;
        bool bien = false;

        try
        {
            Apunta("=== arnés de layout (D9.6): una regla reparte un escritorio ENTERO ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta("en un escritorio virtual creado para esto, con ventanas nuestras.");
            Apunta("ni una ventana tuya, ni un escritorio tuyo, y cero inyección de entrada.");
            await Task.Delay(2500);

            if (!Escritorios.Disponible)
            {
                Apunta("[D9.6] la API de escritorios no está: no hay dónde hacer esto, y eso NO es un fallo");
                bien = true;
                return;
            }

            // ---------- 1. Guardar lo de antes ----------
            var antes = Escritorios.Espacios();
            int iActual = HyprEspacios.IndiceDelActual(antes);
            elOriginal = iActual >= 0 ? antes[iActual].Id : Guid.Empty;
            ordenDelUsuarioAntes = antes.Select(e => e.Id).ToList();
            Apunta($"[D9.6] ANTES: {antes.Count} escritorio(s); el tuyo es {elOriginal}");

            // ---------- 2. Un escritorio para esto ----------
            dePrueba = Escritorios.CrearYDevolver();
            if (dePrueba == Guid.Empty) { Apunta("[D9.6] no se pudo crear el escritorio: se para"); return; }
            Escritorios.Renombrar(dePrueba, ArnesLayout.NombreDePrueba);
            await Task.Delay(500);

            Escritorios.Ir(dePrueba);
            await Task.Delay(1200);
            Apunta($"[D9.6] escritorio de prueba creado y en pie: {dePrueba}");

            // ---------- 3. La capa, encendida EN MEMORIA ----------
            //
            // Con la confirmación apagada a propósito: lo que se mide aquí es que el reparto
            // OCURRE. Que preguntar lo detiene se prueba en `--arnesaplicar`, y las dos
            // mitades juntas son lo que hace honesta la comprobación.
            // ---------- Y empieza en SIMULACIÓN, a propósito ----------
            //
            // «Con el modo en simulación no se mueve nada» es la promesa más repetida de toda
            // la capa y estaba comprobada solo en lógica. Aquí se comprueba contra Windows:
            // las tres ventanas nacen con la regla activa y el modo en simulación, y tienen
            // que quedarse donde Windows las puso. Después se pasa a «aplicar» y se mira que
            // entonces sí.
            //
            // Las dos mitades sobre las MISMAS ventanas: probar solo la segunda daría verde a
            // una capa que mueve siempre, y probar solo la primera, a una que no mueve nunca.
            string titulo = "NubeBar · D9.6 · " + DateTime.Now.ToString("HH:mm:ss.fff");
            _cfg.NubeHypr.Activo = true;
            _cfg.NubeHypr.Modo = "simulacion";
            _cfg.NubeHypr.ConfirmarMovimientos = false;
            _cfg.NubeHypr.Reglas = new List<HyprReglas.Regla>
            {
                // `Titulo` es «contiene», no un patrón: las tres ventanas se llaman
                // «…#1», «…#2» y «…#3», así que el prefijo común las coge a las tres. Un
                // comodín aquí buscaría un asterisco literal y no coincidiría con ninguna.
                new(Id: "arnes-d96", Prioridad: 100, Titulo: titulo,
                    Layout: NubeHypr.Layout.Horizontal),
            };
            AplicarReglasDeHypr();
            Apunta($"[D9.6] regla en memoria: título contiene «{titulo}» → layout Horizontal");
            Apunta($"[D9.6] capa: activo=sí modo=SIMULACIÓN confirmar=no (todo en memoria)");

            // ---------- 4. Tres ventanas nuestras, con otro nombre de ejecutable ----------
            //
            // Renombradas porque `NubeHypr.SePuedeOrganizar` protege por NOMBRE DE PROCESO y
            // «NubeBar» está el primero de la lista: una segunda copia nuestra sería igual de
            // invisible para las reglas que la barra. Es la lección que ya costó dos intentos
            // en D9.5.
            for (int i = 1; i <= ArnesLayout.Cuantas; i++)
            {
                string copia = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"NubeVentanita-L{i}-" + DateTime.Now.ToString("HHmmssfff") + ".exe");
                System.IO.File.Copy(Environment.ProcessPath ?? "", copia, true);
                copias.Add(copia);

                var p = System.Diagnostics.Process.Start(
                    copia, new[] { DiagVentanita.Bandera, $"{titulo} #{i}", "60" });
                if (p != null) procesos.Add(p);
                await Task.Delay(900);
            }
            Apunta($"[D9.6] {procesos.Count} ventana(s) nuestras lanzadas en el escritorio de prueba");

            // Que nazcan, que el gestor se entere, que las reglas resuelvan y que pase la
            // espera de asentamiento de `HyprColocar`.
            await Task.Delay(2000);
            GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
            await Task.Delay(2500);

            // ---------- 4a. En SIMULACIÓN no se ha movido nada ----------
            //
            // La regla está activa, coincide con las tres y pide un layout. En simulación eso
            // se calcula, se apunta en el registro y **no se ejecuta**. Si alguna se hubiera
            // movido, la promesa de la que cuelga toda la capa sería falsa.
            long movidasAlNacer = HyprAdaptador.VecesMovida;
            var alNacer = new List<NubeHypr.Rect>();
            for (int i = 1; i <= ArnesLayout.Cuantas; i++)
            {
                var hh = Nativo.PorTitulo($"{titulo} #{i}");
                if (hh != IntPtr.Zero) alNacer.Add(HyprAdaptador.DeRect(Nativo.Rectangulo(hh)));
            }
            foreach (var s in alNacer) Apunta($"[D9.6]   en simulación: {s}");

            bool todasIguales = alNacer.Count > 1 &&
                                alNacer.All(s => s.CasiIgual(alNacer[0], 4));
            Vale2(todasIguales,
                  "EN SIMULACIÓN NO SE HA MOVIDO NADA: las tres siguen donde Windows las puso " +
                  "(si se hubieran repartido, estarían en sitios distintos)");
            Vale2(RepartidasPorRegla == 0,
                  $"y el contador de repartos sigue a cero ({RepartidasPorRegla})");
            Vale2(HyprAdaptador.VecesMovida == movidasAlNacer,
                  $"y el de movimientos reales no se ha tocado ({movidasAlNacer})");

            // ---------- 4b. Y ahora a «aplicar» ----------
            //
            // El cambio de modo por sí solo no mueve nada: las reglas se disparan cuando
            // aparece una ventana. Así que nace una más, y al dispararse reparte el
            // escritorio entero — las cuatro, incluidas las tres que ya estaban y que
            // ninguna regla volvió a nombrar. Eso es exactamente lo que promete D9.6.
            _cfg.NubeHypr.Modo = "aplicar";
            AplicarReglasDeHypr();
            Apunta("");
            Apunta($"[D9.6] capa a modo APLICAR · {ModoDeLaCapa()}");

            string ultima = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "NubeVentanita-Lx-" + DateTime.Now.ToString("HHmmssfff") + ".exe");
            System.IO.File.Copy(Environment.ProcessPath ?? "", ultima, true);
            copias.Add(ultima);
            var pUlt = System.Diagnostics.Process.Start(
                ultima, new[] { DiagVentanita.Bandera, $"{titulo} #{ArnesLayout.Cuantas + 1}", "60" });
            if (pUlt != null) procesos.Add(pUlt);

            await Task.Delay(2000);
            GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
            await Task.Delay(2500);

            // ---------- Lo que el arnés tiene que decir POR SÍ MISMO ----------
            //
            // La primera versión de esto se apoyaba en el registro de la barra para saber por
            // qué no había pasado nada… y al reiniciarse la barra estable, el registro se
            // llevó las trazas por delante. Un arnés que necesita otro fichero para
            // explicarse no se explica: todo lo que hace falta para entender un fallo se
            // apunta aquí.
            Apunta($"[D9.6] ¿escuchando reglas?={_escuchandoReglas} · modo de la capa={ModoDeLaCapa()}");

            var obs = HyprAdaptador.Observar();
            Apunta($"[D9.6] el observador ve {obs.Estado.Ventanas.Count} ventana(s) organizables");

            // Y la decisión para cada una de las nuestras, por el mismo camino que usa el
            // producto. Si la regla no coincide, aquí se ve por qué y no hay que adivinarlo.
            for (int i = 1; i <= ArnesLayout.Cuantas; i++)
            {
                var h = Nativo.PorTitulo($"{titulo} #{i}");
                if (h == IntPtr.Zero) continue;
                bool laVe = obs.Estado.Ventanas.Any(w => w.Hwnd == h);
                Apunta($"[D9.6]   #{i} hwnd=0x{h.ToInt64():X6} · ¿el observador la ve?={(laVe ? "sí" : "NO")}");

                string exe = Nativo.Exe(h);
                var modelo = new VentanaModelo(h, System.IO.Path.GetFileNameWithoutExtension(exe),
                                               Nativo.Titulo(h),
                                               HyprAdaptador.DeRect(Nativo.Rectangulo(h)));
                var d = HyprVigilante.Examinar(modelo, _cfg.NubeHypr.Reglas,
                                               Nativo.Clase(h), exe);
                foreach (var l in HyprVigilante.Lineas(d)) Apunta("      " + l);
            }

            // ---------- 5. ¿Dónde han acabado? ----------
            var sitios = new List<NubeHypr.Rect>();
            for (int i = 1; i <= ArnesLayout.Cuantas + 1; i++)   // +1: la que disparó el reparto
            {
                var h = Nativo.PorTitulo($"{titulo} #{i}");
                if (h == IntPtr.Zero) { Apunta($"[D9.6] la ventana #{i} no se encuentra"); continue; }
                var r = HyprAdaptador.DeRect(Nativo.Rectangulo(h));
                sitios.Add(r);
                Apunta($"[D9.6]   ventana #{i} en {r}");
            }

            var m = Monitores.Principal();
            var hueco = ZonasAjuste.AreaUtilCon(m, EstorbosDeAhora());
            var area = new NubeHypr.Rect(hueco.X, hueco.Y, hueco.Ancho, hueco.Alto);
            Apunta($"[D9.6] área útil: {area}");

            // ---------- 6. El veredicto ----------
            //
            // No vale con «se movieron 3». Tres ventanas pueden moverse las tres al mismo
            // sitio, y eso contaría igual. Lo que se exige es la FORMA del reparto: ninguna
            // fuera, ninguna encima de otra, y que entre todas ocupen la pantalla.
            var v = ArnesLayout.MiraElReparto(sitios, area, ArnesLayout.Tolerancia);
            Vale2(sitios.Count == ArnesLayout.Cuantas + 1,
                  $"las {ArnesLayout.Cuantas + 1} ventanas existen y se encuentran ({sitios.Count})");
            Vale2(v.Bien, $"EL REPARTO ES UN REPARTO: {v.Porque}");

            // Y que haya sido la regla, no otra cosa: sin esto, lo de arriba podría ser pura
            // casualidad de dónde nacieron las ventanas.
            //
            // El contador que tiene que subir es `RepartidasPorRegla`, no
            // `ColocadasPorRegla`: el segundo cuenta ventanas colocadas UNA A UNA por el
            // camino de D9.5, y esta regla no dice nada de ninguna ventana en concreto —solo
            // pide un layout—, así que es correcto que se quede en cero. La primera versión
            // de esta comprobación miraba el contador equivocado y daba MAL con el reparto
            // ya hecho y medido.
            Apunta($"[D9.6] repartidas por regla: {RepartidasPorRegla} · " +
                   $"colocadas una a una: {ColocadasPorRegla} (0 es lo correcto: la regla " +
                   $"solo pide layout)");
            Vale2(RepartidasPorRegla >= ArnesLayout.Cuantas,
                  $"y fue una regla quien lo repartió, sin que nadie llamara a nada " +
                  $"({RepartidasPorRegla} ventana(s))");

            bien = v.Bien
                && sitios.Count == ArnesLayout.Cuantas + 1
                && RepartidasPorRegla >= ArnesLayout.Cuantas
                && todasIguales;                 // y que en simulación no se moviera nada
        }
        catch (Exception ex)
        {
            Apunta($"[D9.6] REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el arnés de layout", ex);
        }
        finally
        {
            bool limpiezaOk = true;
            try
            {
                // ---------- Las ventanas PRIMERO ----------
                //
                // Antes de borrar el escritorio, siempre. Borrar un escritorio con ventanas
                // dentro las manda a otro, y la limpieza te dejaría tres ventanas sueltas en
                // un escritorio tuyo.
                foreach (var p in procesos)
                {
                    try { if (!p.HasExited) p.Kill(); } catch { }
                }
                await Task.Delay(900);

                foreach (var c in copias)
                {
                    try { if (System.IO.File.Exists(c)) System.IO.File.Delete(c); }
                    catch (Exception ex) { Apunta($"[D9.6] no se pudo borrar {System.IO.Path.GetFileName(c)}: {ex.GetType().Name}"); }
                }

                // ---------- La capa, como estaba ----------
                _cfg.NubeHypr.Reglas = reglasAntes;
                _cfg.NubeHypr.Activo = activoAntes;
                _cfg.NubeHypr.Modo = modoAntes;
                _cfg.NubeHypr.ConfirmarMovimientos = confirmarAntes;
                try { AplicarReglasDeHypr(); } catch { }
                Apunta($"[D9.6] capa devuelta: activo={activoAntes} modo={modoAntes} " +
                       $"confirmar={confirmarAntes} reglas={reglasAntes.Count} (en memoria)");

                // ---------- A tu escritorio, y el de prueba fuera ----------
                if (elOriginal != Guid.Empty)
                {
                    Escritorios.Ir(elOriginal);
                    await Task.Delay(1000);
                }
                if (dePrueba != Guid.Empty)
                {
                    bool borrado = Escritorios.Borrar(dePrueba);
                    await Task.Delay(800);
                    Apunta($"[D9.6] escritorio de prueba borrado: {(borrado ? "sí" : "NO")}");
                    if (!borrado) limpiezaOk = false;
                }

                // ---------- Y que los tuyos estén EXACTAMENTE como estaban ----------
                var despues = Escritorios.Espacios();
                var ordenAhora = despues.Select(e => e.Id).ToList();
                bool mismos = string.Join(",", ordenDelUsuarioAntes) == string.Join(",", ordenAhora);
                Apunta($"[D9.6] tus escritorios: {ordenDelUsuarioAntes.Count} antes, " +
                       $"{ordenAhora.Count} ahora · ¿la misma secuencia?={(mismos ? "sí" : "NO")}");
                if (!mismos) limpiezaOk = false;

                var actual = despues.FirstOrDefault(e => e.EsElActual).Id;
                bool vuelto = elOriginal == Guid.Empty || actual == elOriginal;
                Apunta($"[D9.6] ¿estás en tu escritorio de antes?={(vuelto ? "sí" : "NO")}");
                if (!vuelto) limpiezaOk = false;
            }
            catch (Exception ex)
            {
                limpiezaOk = false;
                Apunta($"[D9.6] la LIMPIEZA reventó: {ex.GetType().Name}: {ex.Message}");
                Log.Error("limpiar el arnés de layout", ex);
            }

            Apunta("");
            Apunta(bien && limpiezaOk
                ? "[D9.6] D9.6 queda probado contra Windows: una regla con layout reparte un "
                + "escritorio entero, y todo lo que se creó para probarlo está recogido."
                : "[D9.6] ¡OJO! algo no ha salido. Mira las líneas de arriba.");

            // La limpieza pesa igual que la prueba: un arnés que demuestra algo y deja un
            // escritorio suelto en tu máquina no ha salido bien.
            Application.Current.Shutdown(bien && limpiezaOk ? 0 : 1);
        }
    }
}
