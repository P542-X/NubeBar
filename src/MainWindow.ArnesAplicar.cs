using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NubeBar;

// El runner de `--arnesaplicar`. Ver ArnesAplicar.cs para las condiciones.
public partial class MainWindow
{
    async System.Threading.Tasks.Task CorrerArnesDeAplicar()
    {
        Window? ventana = null;
        IntPtr hwnd = IntPtr.Zero;
        NubeHypr.Rect original = default;
        bool activoAntes = _cfg.NubeHypr.Activo;
        string modoAntes = _cfg.NubeHypr.Modo;
        bool limpiezaOk = true;
        long movidasAlEmpezar = HyprAdaptador.VecesMovida;

        try
        {
            Apunta("=== arnés de aplicar (la puerta de D9) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta("se mueve UNA ventana, creada por el arnés. Ninguna tuya se toca ni se lee.");
            Apunta("sin inyección de teclado ni de ratón.");
            await System.Threading.Tasks.Task.Delay(2500);

            // ---------- 1. La ventana del arnés ----------
            //
            // Propia, visible y con un tamaño conocido. Visible a propósito: `SetWindowPos`
            // sobre una ventana oculta puede "funcionar" sin que se vea nada, y entonces el
            // arnés no probaría lo que dice probar.
            ventana = new Window
            {
                Title = ArnesAplicar.TituloDePrueba,
                Width = 420, Height = 300,
                Left = 120, Top = 120,
                ShowInTaskbar = false,
                Topmost = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = new TextBlock
                {
                    Text = "Ventana del arnés de NubeHypr.\n" +
                           "La está moviendo el motor de verdad.\nSe cierra sola.",
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            ventana.Show();
            await System.Threading.Tasks.Task.Delay(700);

            hwnd = new System.Windows.Interop.WindowInteropHelper(ventana).Handle;
            if (hwnd == IntPtr.Zero) { Apunta("[D9]  no hay hwnd: se para aquí"); return; }

            original = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            Apunta($"[D9]  ventana del arnés: hwnd=0x{hwnd.ToInt64():X6} «{ventana.Title}»");
            Apunta($"[D9]  donde nació: {original}");

            // ---------- 2. La capa, encendida en memoria y en APLICAR ----------
            //
            // Es la única parte del arnés que no se puede hacer en simulación: lo que se
            // está probando es justamente el camino que mueve. Vuelve a su valor en el
            // `finally`, y el fichero no se toca en ningún momento.
            _cfg.NubeHypr.Activo = true;
            _cfg.NubeHypr.Modo = "aplicar";
            var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
            Apunta($"[D9]  capa en memoria: activo=sí modo=aplicar -> efectivo={modo}");
            Apunta($"[D9]  (antes era activo={activoAntes}, modo={modoAntes}; se devuelve al salir)");

            // ---------- 3. Una regla que habla SOLO de esta ventana ----------
            //
            // Por el título, que es único y lleva la hora. Así la regla no puede coincidir
            // con nada tuyo ni por accidente: no hay otra ventana en el equipo con ese
            // título.
            var regla = new HyprReglas.Regla(
                Id: "arnes-de-aplicar",
                Prioridad: 100,
                Titulo: ventana.Title,
                Ancho: 0.5, Alto: 0.5,
                Flotante: true);

            var modelo = new VentanaModelo(hwnd, "NubeBar", ventana.Title, original);
            var decision = HyprVigilante.Examinar(modelo, new[] { regla },
                                                  Nativo.Clase(hwnd), Environment.ProcessPath ?? "");

            Apunta("[D9]  lo que decide la regla:");
            foreach (var l in HyprVigilante.Lineas(decision)) Apunta("    " + l);
            if (!decision.AlgunaCoincidio)
            {
                Apunta("[D9]  la regla no coincide con su propia ventana: algo está mal, se para");
                return;
            }

            // ---------- 4. De la decisión a un destino de verdad ----------
            //
            // La decisión dice "la mitad de ancho y la mitad de alto"; traducirlo a píxeles
            // es cosa del área útil del monitor, que es lo que sabe el adaptador y no el
            // modelo. Se centra en su monitor para que el destino no caiga en un borde.
            var m = Monitores.DeVentana(hwnd)
                    ?? Monitores.Principal();
            // Por `HyprColocar`, que es lo que usa el producto desde D9.5. Esto estaba
            // calculado A MANO aquí —el área del monitor, la fracción, el centrado—, es
            // decir, una segunda forma de hacer la misma cuenta: el arnés podía dar verde
            // con una fórmula que el producto no usa. Ahora prueba la de verdad.
            var sitio = SitioDeLasReglas();
            var plan = HyprColocar.Planear(modelo, decision.Decision, sitio);
            foreach (var l in HyprColocar.Lineas(modelo, plan, null)) Apunta("    " + l);

            Vale2(plan.Destino != null, "la decisión de la regla produce un destino");
            if (plan.Destino is not { } destino) return;

            var area = sitio.Monitores.Count > 0
                     ? sitio.Monitores[0]
                     : new NubeHypr.Rect(m.TrabajoX, m.TrabajoY, m.TrabajoAncho, m.TrabajoAlto);
            Apunta($"[D9]  área útil del monitor: {area}");
            Apunta($"[D9]  destino calculado: {destino}");
            Vale2(destino.Valido, "el destino es válido");
            Vale2(destino.DentroDe(area), "y cae dentro del área útil");

            // ---------- 5. Aplicar DE VERDAD ----------
            var efectos = HyprColocar.Efectos(plan, "arnés de aplicar");

            // Por la PUERTA, no por el adaptador: desde D9.2 nadie llama a
            // `HyprAdaptador.Aplicar` directamente, y el arnés tampoco — si el arnés
            // tomara un atajo, estaría probando un camino que el producto no usa.
            var r = AplicarConRedes(efectos, "arnés de aplicar", modo);
            if (r == null) { Apunta("[D9]  la puerta no dejó pasar nada: se para aquí"); return; }
            foreach (var l in r.Lineas) Apunta("    " + l);

            Apunta($"[D9]  resultado: movidas={r.Movidas} saltadas={r.Saltadas} " +
                   $"viejas={r.Viejas} simulado={r.Simulado} divergió={r.Divergio}");
            Vale2(!r.Simulado, "NO fue una simulación: se aplicó de verdad");
            Vale2(r.Movidas == 1, "se movió exactamente una ventana");
            Vale2(HyprAdaptador.VecesMovida == movidasAlEmpezar + 1,
                  $"y el contador de movimientos reales subió una vez " +
                  $"({movidasAlEmpezar} -> {HyprAdaptador.VecesMovida})");

            // ---------- 6. ¿Llegó donde se le dijo? ----------
            await System.Threading.Tasks.Task.Delay(600);
            var donde = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            Apunta($"[D9]  leído de Windows después de mover: {donde}");
            Vale2(donde.CasiIgual(destino, 10),
                  $"la ventana está donde se le pidió (±10 px): {donde} frente a {destino}");
            Vale2(!donde.CasiIgual(original, 10), "y ya no está donde nació");
            Vale2(!r.Divergio, "el adaptador no reportó divergencia");

            // ---------- 7. Y el supervisor se ha enterado ----------
            //
            // Es la otra mitad de D7: si el camino de aplicar no alimentara los contadores,
            // el supervisor nunca sabría que algo va mal al mover.
            Apunta($"[D9]  supervisor: fallosSeguidos={HyprSalud.FallosSeguidos} " +
                   $"totalFallos={HyprSalud.TotalFallos}");
            Vale2(HyprSalud.FallosSeguidos == 0, "un movimiento que sale bien borra la racha de fallos");

            await LaConfirmacionDeVerdad(hwnd, original, modo);

            // ================= D9.1: las tres redes, sobre esta misma ventana =================
            //
            // Aquí es donde dejan de ser lógica probada y pasan a ser lógica que ha corrido
            // contra Windows. Las tres se ejercitan con la ventana del arnés, nunca con una
            // tuya.
            Apunta("--- las tres redes de D9.1 ---");

            // ---------- a) El ritmo ----------
            var ritmo = new HyprRitmo.Contador();
            var ahora = DateTime.Now;
            Vale2(ritmo.PreguntaLote(ahora).Puede, "ritmo: la primera tanda se permite");
            Vale2(ritmo.PreguntaVentana(hwnd, ahora).Puede, "ritmo: y esta ventana también");
            ritmo.ApuntadoLote(ahora);
            ritmo.Apuntado(hwnd, ahora);

            var deSeguido = ritmo.PreguntaVentana(hwnd, ahora.AddMilliseconds(80));
            Vale2(!deSeguido.Puede, $"ritmo: 80 ms después se corta ({deSeguido.Que})");
            Apunta("    " + HyprRitmo.Linea(hwnd, deSeguido));
            Vale2(ritmo.PreguntaVentana(hwnd, ahora.Add(HyprRitmo.EsperaPorVentana)).Puede,
                  "ritmo: pasada la espera, vuelve a permitirse");

            // ---------- b) El respeto al gesto ----------
            //
            // Se simula tu gesto moviendo la ventana a mano —con el mismo `Nativo.Colocar`,
            // porque es la ventana del arnés— y se comprueba que el motor lo reconoce como
            // "de fuera" y deja de tocarla.
            var respeto = new HyprRespeto.Memoria();
            respeto.Pedimos(hwnd, destino);
            Vale2(HyprRespeto.SeLePuedeTocar(hwnd, respeto),
                  "respeto: antes del gesto, las reglas pueden colocarla");

            var aMano = new NubeHypr.Rect(destino.X + 200, destino.Y + 120, destino.Ancho, destino.Alto);
            Nativo.Colocar(hwnd, (int)aMano.X, (int)aMano.Y, (int)aMano.Ancho, (int)aMano.Alto);
            await System.Threading.Tasks.Task.Delay(500);

            var trasElGesto = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            var quien = HyprRespeto.DeQuienFue(destino, trasElGesto, respeto.LoQuePedimos(hwnd));
            Apunta("    " + HyprRespeto.LineaDelGesto(hwnd, "arnés", quien, destino, trasElGesto));
            Vale2(quien == HyprRespeto.Quien.DeFuera,
                  $"respeto: el movimiento a mano se lee como de fuera ({quien})");

            respeto.Marcar(hwnd);
            Vale2(!HyprRespeto.SeLePuedeTocar(hwnd, respeto),
                  "respeto: y a partir de ahí las reglas no la tocan");
            Apunta("    " + HyprRespeto.Linea(hwnd, "arnés", respeto));

            // Y lo contrario: un movimiento NUESTRO no se confunde con un gesto. Sin esto,
            // el motor se desactivaría a sí mismo en la primera ventana que colocara.
            Vale2(HyprRespeto.DeQuienFue(original, destino, destino) == HyprRespeto.Quien.Nuestro,
                  "respeto: lo que colocamos nosotros NO se lee como un gesto tuyo");

            // ---------- c) Deshacer ----------
            var pila = new HyprDeshacer.Pila();
            pila.Apuntar("el arnés de aplicar",
                         new[] { new HyprDeshacer.Antes(hwnd, original, false) });
            var (elLote, losEfectos) = pila.Deshacer(new[] { hwnd });
            Apunta("    " + HyprDeshacer.Linea(elLote, losEfectos.Count, modo));
            Vale2(elLote != null && losEfectos.Count == 1, "deshacer: un lote con un efecto");
            Vale2(pila.Cuantos == 0, "deshacer: el lote se consume, no se repite");

            // ---------- 7bis. D9.5: el camino AUTOMÁTICO ----------
            //
            // Todo lo de arriba prueba la segunda mitad: con una decisión en la mano, el
            // motor coloca. Falta la primera: que **al aparecer una ventana**, las reglas
            // se resuelvan y la coloquen sin que nadie llame a nada. Es lo que hace el
            // producto desde D9.5 y es lo que el usuario va a ver.
            //
            // Se prueba con otra ventana NUESTRA y con una regla que habla solo de ella
            // por el título, que lleva la hora y es único en el equipo. La regla se mete en
            // `_cfg.NubeHypr.Reglas` EN MEMORIA porque es de ahí de donde las lee el
            // producto; se quita en cuanto termina, y el fichero no se toca en ningún
            // momento (`Config.SoloLectura` lo impide además).
            Apunta("--- D9.5: una ventana aparece y la regla la coloca sola ---");
            //
            // En OTRO PROCESO, y esto es el detalle que costó una vuelta: el observador
            // descarta las ventanas de NubeBar comparando el PID (`Nativo.EsNuestra`),
            // porque un gestor de ventanas no puede recolocarse a sí mismo la barra. Una
            // ventana creada aquí dentro nunca llegaba a `HyprVigilante`, así que el camino
            // automático no se recorría y el arnés daba verde probando solo la otra mitad.
            //
            // `--ventanita` es nuestro código y nuestra ventana, con otro PID y un título
            // único. Ninguna ventana del usuario entra en esto.
            System.Diagnostics.Process? proceso = null;
            string? copia = null;
            var reglasAntes = _cfg.NubeHypr.Reglas;
            bool confirmarAntes = _cfg.NubeHypr.ConfirmarMovimientos;
            try
            {
                // ---------- Y sin preguntar, porque lo que se mide aquí es el movimiento ----
                //
                // `ConfirmarMovimientos` viene a `true` de serie, y una regla que coloca una
                // ventana nueva es exactamente lo que confirma: el camino autónomo. Con la
                // opción puesta, esta parte del arnés medía 0 colocadas y daba MAL —y tenía
                // razón, porque con la opción puesta NO se mueve nada hasta que el usuario
                // dice «Mover».
                //
                // Esto lo cazó el propio arnés al añadir la confirmación, que es para lo que
                // está. Las dos mitades se prueban, cada una con la opción que le toca:
                // aquí apagada, para exigir que la regla mueve; y en
                // `LaConfirmacionDeVerdad`, encendida, para exigir que preguntar no mueve.
                // Probar solo una de las dos dejaría pasar un gate que bloquea siempre.
                _cfg.NubeHypr.ConfirmarMovimientos = false;
                Apunta("[D9.5] confirmación apagada EN MEMORIA para esta parte: lo que se " +
                       "mide es que la regla mueve. El otro lado se prueba aparte.");

                string titulo = "NubeBar · D9.5 · " + DateTime.Now.ToString("HH:mm:ss.fff");
                var reglaAuto = new HyprReglas.Regla(
                    Id: "arnes-d95", Prioridad: 100, Titulo: titulo,
                    Ancho: 0.4, Alto: 0.4, Flotante: true);
                _cfg.NubeHypr.Reglas = new List<HyprReglas.Regla> { reglaAuto };
                Apunta($"[D9.5] regla en memoria: título=«{titulo}» → 40% x 40%");

                // Y ENGANCHAR LA ESCUCHA. Sin esto no pasa nada, y es justo lo que pasaba:
                // `EscucharParaLasReglas` se decide al arrancar según el config, que tiene
                // la capa apagada, así que el delegado no estaba colgado del evento. Se
                // encendió la capa en memoria y nadie volvió a preguntar.
                //
                // Que haga falta decirlo aquí NO es un apaño del arnés: es exactamente lo
                // que hace el producto cuando tocas el interruptor en Ajustes
                // (`MainWindow.xaml.cs` → `AplicarReglasDeHypr`). El arnés enciende la capa
                // como la enciende el usuario, no por un atajo.
                AplicarReglasDeHypr();
                Apunta("[D9.5] escucha de reglas enganchada");

                long colocadasAntes = ColocadasPorRegla;

                // ---------- Y con OTRO NOMBRE de ejecutable ----------
                //
                // Segundo intento, y segunda cosa aprendida: no basta con otro PID.
                // `NubeHypr.SePuedeOrganizar` protege por NOMBRE DE PROCESO, y «NubeBar»
                // está el primero de la lista de protegidos. Es lo correcto —ninguna regla
                // puede recolocar una ventana de NubeBar, venga del proceso que venga— y
                // deja a un segundo NubeBar igual de invisible para las reglas que al
                // primero.
                //
                // Así que la ventanita se lanza desde una copia del ejecutable con otro
                // nombre, en la carpeta temporal. Sigue siendo nuestro código y nuestra
                // ventana; lo único que cambia es cómo se llama el fichero. Se borra al
                // terminar.
                copia = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "NubeVentanita-" + DateTime.Now.ToString("HHmmssfff") + ".exe");
                System.IO.File.Copy(Environment.ProcessPath ?? "", copia, true);
                Apunta($"[D9.5] copia para la ventanita: {System.IO.Path.GetFileName(copia)}");

                // Con la lista de argumentos, no con una cadena: el título lleva espacios y
                // puntos, y concatenarlo a mano es como se cuela una comilla mal puesta.
                proceso = System.Diagnostics.Process.Start(
                    copia, new[] { DiagVentanita.Bandera, titulo, "10" });

                // Que la ventana nazca, que el gestor se entere por sus eventos, que se
                // resuelvan las reglas y que pase la espera de asentamiento.
                await System.Threading.Tasks.Task.Delay(1800);
                GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
                await System.Threading.Tasks.Task.Delay(2200);

                var h = Nativo.PorTitulo(titulo);
                var acabo = h != IntPtr.Zero
                          ? HyprAdaptador.DeRect(Nativo.Rectangulo(h))
                          : default;
                Apunta($"[D9.5] la ventana acabó en {acabo} (hwnd=0x{h.ToInt64():X6})");
                Apunta($"[D9.5] colocadas por regla: {colocadasAntes} -> {ColocadasPorRegla}");

                Vale2(h != IntPtr.Zero, "la ventana de otro proceso existe y se encuentra");
                Vale2(ColocadasPorRegla > colocadasAntes,
                      "una ventana nueva fue colocada por una regla, sin que nadie llamara a nada");
                Vale2(acabo.Ancho > 0 && (acabo.Ancho < 500 - 20 || acabo.X > 60 + 20),
                      $"y de verdad se movió de donde nació (60,60,500,340 -> {acabo})");
            }
            finally
            {
                _cfg.NubeHypr.Reglas = reglasAntes;
                _cfg.NubeHypr.ConfirmarMovimientos = confirmarAntes;
                Apunta($"[D9.5] reglas devueltas: {_cfg.NubeHypr.Reglas.Count} (en memoria) · " +
                       $"confirmación devuelta a {confirmarAntes}");
                // La escucha se suelta donde se suelta siempre: en el `finally` de fuera,
                // al devolver la capa a como estaba. Aquí solo se quitan las reglas.
                try { if (proceso is { HasExited: false }) proceso.Kill(); } catch { }
                await System.Threading.Tasks.Task.Delay(400);
                // Y la copia no se queda en el temporal del usuario.
                try { if (copia != null && System.IO.File.Exists(copia)) System.IO.File.Delete(copia); }
                catch (Exception ex) { Apunta($"[D9.5] no se pudo borrar la copia: {ex.GetType().Name}"); }
            }

            // ---------- 8. Devolverla a donde estaba ----------
            //
            // Con el mismo camino, no con un `Left`/`Top` de WPF: si la vuelta usara otra
            // vía, el arnés no estaría probando que el motor sabe devolver una ventana.
            Apunta("--- se devuelve a donde nació, por el mismo camino ---");
            var vuelta = HyprAdaptador.Aplicar(
                NubeHypr.SiguienteVersion(),
                new List<EfectoVentana> { new(TipoEfecto.Colocar, hwnd, original, Porque: "devolver") },
                modo, confirmar: true);
            foreach (var l in vuelta.Lineas) Apunta("    " + l);

            await System.Threading.Tasks.Task.Delay(600);
            var final = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            Apunta($"[D9]  leído al final: {final}");
            Vale2(final.CasiIgual(original, 10),
                  $"ha vuelto a donde nació: {final} frente a {original}");
        }
        catch (Exception ex)
        {
            Apunta($"[D9]  EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el arnés de aplicar", ex);
            limpiezaOk = false;
        }
        finally
        {
            Apunta("--- limpieza ---");

            // La capa vuelve a lo que estaba ANTES de cerrar la ventana: si el cierre
            // fallara, lo que no puede quedar es la capa en "aplicar".
            _cfg.NubeHypr.Activo = activoAntes;
            _cfg.NubeHypr.Modo = modoAntes;
            // Y con la capa devuelta, la escucha de reglas se suelta sola: lo decide el
            // modo. Es la misma llamada que al encenderla, y por eso no hay dos formas de
            // dejar el motor quieto.
            try { AplicarReglasDeHypr(); } catch { }
            Apunta($"[D9]  capa devuelta: activo={activoAntes} modo={modoAntes} " +
                   $"(en memoria; el fichero no se tocó)");

            try
            {
                if (ventana != null)
                {
                    ventana.Close();
                    await System.Threading.Tasks.Task.Delay(500);
                    bool sigue = hwnd != IntPtr.Zero && Nativo.Existe(hwnd);
                    Apunta($"[D9]  ventana del arnés cerrada: ¿sigue existiendo?={(sigue ? "SÍ, MAL" : "no")}");
                    if (sigue) limpiezaOk = false;
                }
            }
            catch (Exception ex)
            {
                limpiezaOk = false;
                Apunta($"[D9]  no se pudo cerrar la ventana del arnés: {ex.GetType().Name}");
            }

            // Son los del ADAPTADOR. El gesto simulado de D9.1 llama a `Nativo.Colocar`
            // directamente —es la ventana del arnés y hace de tu mano— y ese no cuenta
            // aquí, igual que no contarían los tuyos: el contador mide lo que mueve el
            // motor, que es justo lo que hay que poder vigilar.
            Apunta($"[D9]  movimientos del motor en todo el arnés: " +
                   $"{HyprAdaptador.VecesMovida - movidasAlEmpezar} (colocar y devolver). " +
                   "El gesto simulado no cuenta: ese hace de tu mano, no del motor.");
            Apunta($"[D9]  limpieza={(limpiezaOk ? "COMPLETA" : "INCOMPLETA: míralo")}");
            Apunta("[D9]  y NO se ha conectado nada: las reglas siguen sin aplicarse a tus");
            Apunta("      ventanas. Esto prueba que el camino funciona, no lo enciende.");

            Application.Current.Shutdown(limpiezaOk ? 0 : 1);
        }
    }

    // Un "vale" para los arneses: apunta en el registro en vez de contar en la batería de
    // pruebas. Se llama distinto que el `Vale` de `Pruebas` a propósito — son dos cosas y
    // confundirlas haría pensar que un arnés cuenta como prueba pura.
    void Vale2(bool bien, string que) =>
        Apunta($"[D9]  {(bien ? "OK  " : "MAL ")} {que}");

    // ================= La confirmación, contra Windows =================
    //
    // `Pruebas.Confirmar.cs` exige las decisiones: cuándo se pregunta, qué caduca, cómo se
    // cuentan las ventanas. Lo que no puede exigir es la frase que de verdad importa: **que
    // con la opción puesta, la ventana NO se mueve**. Eso solo lo dice Windows.
    //
    // Se hace sobre la ventana del arnés, que es nuestra, y se prueban los dos lados: que
    // preguntar no mueve, y que contestar sí. Un gate que bloquea siempre pasaría la mitad
    // de esta comprobación y rompería el programa.
    async System.Threading.Tasks.Task LaConfirmacionDeVerdad(IntPtr hwnd,
                                                             NubeHypr.Rect aDonde,
                                                             NubeHypr.Modo modo)
    {
        Apunta("--- preguntar antes de mover (ConfirmarMovimientos) ---");
        bool antes = _cfg.NubeHypr.ConfirmarMovimientos;
        try
        {
            // En memoria, como el resto de los diagnósticos: el fichero no se toca.
            _cfg.NubeHypr.ConfirmarMovimientos = true;

            var estaba = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            int pedidasAntes = ConfirmacionesPedidas;
            long movidasAntes = HyprAdaptador.VecesMovida;

            // Un destino distinto de donde está: el sitio donde nació. Así «no se ha movido»
            // es comprobable, y si se mueve, se nota.
            var efectos = new List<EfectoVentana>
            {
                new(TipoEfecto.Colocar, hwnd, aDonde, Porque: "arnés: confirmación"),
            };

            // ---------- 1. Pedirlo como lo pide una regla ----------
            var r = AplicarConRedes(efectos, "arnés: confirmación", modo, automatico: true);

            Vale2(r == null, "con la opción puesta, la puerta no aplica nada de entrada");

            // El discriminador que hace honesta esta comprobación: `null` también lo
            // devuelve el tope de ritmo, y acabamos de mover esta ventana. Si la cuenta de
            // confirmaciones no sube, el `null` de arriba era del ritmo y esto no ha
            // probado nada — así que se mira, en vez de dar verde por el motivo equivocado.
            bool pregunto = ConfirmacionesPedidas == pedidasAntes + 1;
            Vale2(pregunto, $"y preguntó: confirmaciones pedidas {pedidasAntes} -> " +
                            $"{ConfirmacionesPedidas}" +
                            (pregunto ? "" : "  (ojo: el null de arriba puede ser del ritmo)"));

            await System.Threading.Tasks.Task.Delay(500);
            var sigue = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            Apunta($"[D9]  estaba en {estaba} · ahora en {sigue} · se le pedía {aDonde}");
            Vale2(sigue.CasiIgual(estaba, 10), "LA VENTANA NO SE HA MOVIDO: preguntar no mueve");
            Vale2(HyprAdaptador.VecesMovida == movidasAntes,
                  $"y el contador de movimientos reales no se ha tocado ({movidasAntes})");

            if (!pregunto) { Apunta("[D9]  sin pregunta en pie no hay respuesta que probar"); return; }

            // ---------- 2. Y ahora contestar ----------
            //
            // Por el mismo camino que el botón «Mover» del panel, no llamando al adaptador:
            // si el arnés tomara un atajo aquí, probaría un camino que el usuario no recorre.
            var pendiente = _confirmacionHypr;
            Vale2(pendiente != null, "hay una petición en pie, con sus efectos guardados");
            if (pendiente == null) return;

            Apunta($"[D9]  lo que diría el panel: «{HyprConfirmar.Texto(pendiente)}»");
            Apunta($"[D9]  y el detalle: «{HyprConfirmar.Detalle(pendiente.Efectos)}»");

            // El panel está abierto desde `Confirmar(...)`: se cierra, que es lo que hace el
            // botón antes de ejecutar la acción.
            CerrarPanel();
            ResponderConfirmacionDeHypr(pendiente);

            await System.Threading.Tasks.Task.Delay(700);
            var final = HyprAdaptador.DeRect(Nativo.Rectangulo(hwnd));
            Apunta($"[D9]  tras aceptar está en {final}");
            Vale2(ConfirmacionesAceptadas >= 1, "la respuesta se contó como aceptada");
            Vale2(final.CasiIgual(aDonde, 10),
                  $"Y AHORA SÍ se ha movido a donde se pedía: {final} frente a {aDonde}");
            Vale2(_confirmacionHypr == null, "y la petición ya no está en pie: no se repite");
        }
        catch (Exception ex)
        {
            Apunta($"[D9]  la confirmación REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el arnés de la confirmación", ex);
        }
        finally
        {
            _cfg.NubeHypr.ConfirmarMovimientos = antes;
            try { CerrarPanel(); } catch { }
            Apunta($"[D9]  opción devuelta a {antes} (en memoria; el fichero no se ha tocado)");
        }
    }
}
