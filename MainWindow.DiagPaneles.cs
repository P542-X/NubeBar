using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NubeBar;

// El runner de `--diagpaneles`. Ver DiagPaneles.cs para qué comprueba y por qué.
public partial class MainWindow
{
    // Las cinco colocaciones de la barra. El modo isla va aparte del modo barra porque la
    // isla mide distinto —se recoge— y el recorte en tarjeta solo existe ahí.
    static readonly (string Modo, string Posicion)[] ColocacionesDeLaBarra =
    {
        ("isla", "arriba"),
        ("barra", "arriba"),
        ("barra", "abajo"),
        ("barra", "izquierda"),
        ("barra", "derecha"),
    };

    async Task CorrerDiagnosticoDePaneles()
    {
        int malos = 0, mirados = 0;
        var (modoAntes, posAntes) = OrientacionDeAhora();
        string monitoresAntes = _cfg.Barra.Monitores;

        try
        {
            Apunta("=== PANELES: que no se corte ninguno, en ninguna posición de la barra ===");
            Apunta($"ejecutable: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"pantalla: {_monitor.Ancho}x{_monitor.Alto} escala {_monitor.Escala:0.##} " +
                   $"→ {_monitor.Ancho / _monitor.Escala:0}x{_monitor.Alto / _monitor.Escala:0} de diseño");
            Apunta($"tope de panel en esta pantalla: {AltoMaximoPanel():0} px");
            Apunta("");
            Apunta("La regla: o el panel cabe entero, o hay desplazamiento interno.");
            Apunta("Un control por debajo del borde SIN rueda es un botón que nadie puede pulsar.");

            await Task.Delay(3000);

            var (m1, b1) = await BarrerLosPaneles(ColocacionesDeLaBarra);
            mirados += m1; malos += b1;

            // Una sola vez y aquí, no dentro del barrido: el barrido corre una vez por isla,
            // y repetir veinte aperturas en el segundo monitor no mediría nada nuevo.
            malos += await AbrirYCerrarLaRedDiezVeces();

            // ---------- Y el SEGUNDO MONITOR ----------
            //
            // Todo lo de arriba corre en el monitor principal, que es donde está la barra.
            // El segundo es otra geometría —aquí, vertical de 1080x1920— y un panel que cabe
            // de sobra en 2560x1440 puede no caber en 1080 de ancho. Estaba en la lista de
            // pruebas manuales justo por eso, y es lo que un diagnóstico hace mejor que unos
            // ojos: abrir treinta y cinco paneles y medirlos todos.
            //
            // La segunda isla se enciende EN MEMORIA y se apaga al terminar. Durante unos
            // segundos aparece una barra en la otra pantalla: es el precio de medirla.
            var pantallas = Monitores.Todos().DistinctBy(m => m.Handle).ToList();
            if (pantallas.Count > 1)
            {
                Apunta("");
                Apunta($"════════ el segundo monitor ({pantallas.Count} pantallas) ════════");
                _cfg.Barra.Monitores = "todos";
                foreach (var i in Islas.ToList()) i._cfg = _cfg;
                Reorganizar();
                await Task.Delay(2500);

                var otra = Islas.FirstOrDefault(i => !i._esJefa);
                if (otra == null) Apunta("[PAN] no ha llegado a crearse la segunda isla");
                else
                {
                    Apunta($"[PAN] segunda isla en {otra._monitor.Dispositivo} " +
                           $"{otra._monitor.Ancho}x{otra._monitor.Alto} " +
                           $"escala {otra._monitor.Escala:0.##} → " +
                           $"{otra._monitor.Ancho / otra._monitor.Escala:0}x" +
                           $"{otra._monitor.Alto / otra._monitor.Escala:0} de diseño");
                    var (m2, b2) = await otra.BarrerLosPaneles(ColocacionesDeLaBarra);
                    mirados += m2; malos += b2;
                }
            }
            else Apunta("[PAN] solo hay una pantalla: no hay segundo monitor que mirar.");

            Apunta("");
            Apunta($"[PAN] {mirados} panel(es) mirados.");
            Apunta(malos == 0
                ? "[PAN] ninguno deja un control por debajo del borde sin forma de alcanzarlo."
                : $"[PAN] ¡OJO! {malos} con algo fuera y sin desplazamiento.");
            Apunta("[PAN] no se ha escrito la configuración: todo se cambió en memoria.");
        }
        catch (Exception ex)
        {
            Apunta($"[PAN] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico de paneles", ex);
            malos++;
        }
        finally
        {
            try
            {
                foreach (var i in Islas.ToList()) { try { i.CerrarPanel(); } catch { } }

                // Las pantallas primero: devolver la orientación con dos islas vivas la
                // dejaría puesta solo en una.
                _cfg.Barra.Monitores = monitoresAntes;
                foreach (var i in Islas.ToList()) i._cfg = _cfg;
                Reorganizar();
                await Task.Delay(1200);

                PonerOrientacion(modoAntes, posAntes);
                Aplicar();
                Apunta($"[PAN] barra devuelta a {modoAntes}/{posAntes}, monitores «{monitoresAntes}» " +
                       $"(en memoria). Islas vivas: {Islas.Count}");
            }
            catch (Exception ex) { Log.Error("devolver la barra a su sitio", ex); }
            Application.Current.Shutdown(malos == 0 ? 0 : 5);
        }
    }

    // ---------- El barrido, para UNA isla ----------
    //
    // Instancia y no estático a propósito: abrir un panel es abrirlo en ESTA isla, con su
    // monitor y su orientación. Lo que hace que valga para el segundo monitor es que se
    // llama sobre la otra isla, no que reciba un monitor por parámetro.
    internal async Task<(int Mirados, int Malos)> BarrerLosPaneles(
        (string Modo, string Posicion)[] colocaciones)
    {
        int malos = 0, mirados = 0;

        // Las mismas puertas que usa el usuario. Abrir por `AbrirPanel` a pelo probaría un
        // camino que el producto no recorre.
        var puertas = new (string Que, Action Abrir)[]
        {
            ("audio", () => AbrirAudio()),
            ("red", () => AbrirRed()),
            ("bateria", () => AbrirBateria()),
            ("bandeja", () => AlternarBandejaExtra()),
            ("notificaciones", () => AbrirNotificaciones()),
            ("vista", () => AlternarVista()),
            // El más alto de todos y el que SIEMPRE se desplaza: es el caso en el que
            // «se sale» tiene que ser correcto, y por eso entra en la ronda.
            ("inicio", () => AbrirMenuInicio()),

            // ---------- Y la pregunta de «¿muevo estas ventanas?» ----------
            //
            // Entra en la ronda porque es el panel más nuevo y el único que nace de una
            // decisión de seguridad: si se corta por abajo, el botón que se queda fuera es
            // «Mover» o «Cancelar», y un usuario que no puede alcanzar ninguno de los dos se
            // queda con las ventanas en el aire.
            //
            // Con el texto DE VERDAD, no con un «lorem ipsum»: lo que se mide es si cabe la
            // frase que va a salir, y una frase corta de prueba cabría donde la real no.
            // Con la barra vertical el panel sale en una ventana flotante y no en la isla,
            // así que esta es la única forma de mirar esa composición sin provocar un
            // movimiento real.
            //
            // La acción es vacía A PROPÓSITO: el diagnóstico mide la superficie, no mueve
            // ventanas. Pulsar aquí no pulsa nada — nadie pulsa, se abre y se mide.
            ("confirmar", () => Confirmar(
                HyprConfirmar.Texto(new HyprConfirmar.Peticion(
                    "regla «Discord»",
                    new[]
                    {
                        new EfectoVentana(TipoEfecto.Colocar, (IntPtr)1),
                        new EfectoVentana(TipoEfecto.Colocar, (IntPtr)2),
                        new EfectoVentana(TipoEfecto.AEspacio, (IntPtr)3),
                    },
                    DateTime.Now)),
                "Va a cambiarlas de sitio y de tamaño, llevarlas a otro escritorio. " +
                "Si no contestas, no se mueve nada.",
                "Mover", () => { }, peligro: false)),
        };

        foreach (var (modo, posicion) in colocaciones)
        {
            Apunta("");
            Apunta($"──────── {_monitor.Dispositivo} · {modo}/{posicion} ────────");
            PonerOrientacion(modo, posicion);
            Aplicar();
            await Task.Delay(1200);
            Apunta($"   vertical={Vertical}  ·  tope={AltoMaximoPanel():0}");

            foreach (var (que, abrir) in puertas)
            {
                try
                {
                    abrir();
                    // Lo que espera la medida de dentro (700 ms) más el margen de la
                    // animación de alto y del relleno que llega tarde.
                    await Task.Delay(1900);

                    if (_panelAbierto != que)
                    {
                        Apunta($"   --   «{que}» no llegó a abrirse aquí (abierto: " +
                               $"{_panelAbierto ?? "ninguno"})");
                        CerrarPanel();
                        await Task.Delay(500);
                        continue;
                    }

                    var raiz = RaizDelPanelAbierto();
                    if (raiz == null) Apunta($"   --   «{que}» abierto pero sin raíz medible");
                    else
                    {
                        mirados++;
                        var r = ApuntarSiAlgoSeSale(que, raiz);

                        // Y de paso, lo que oiría un lector de pantalla. La misma medida
                        // encontró cincuenta y un botones mudos en Ajustes; estos son los
                        // de la barra, y no los había mirado nadie.
                        var a = MirarElTecladoDe(raiz);
                        string voz = a.SinNombre > 0
                            ? $"  ← {a.SinNombre} de {a.Botones} botones sin nombre ({a.Ejemplo})"
                            : "";
                        if (a.SinNombre > 0) malos++;

                        if (r.Bien && a.SinNombre == 0) Apunta($"   ok   «{que}»: {r}");
                        else { if (!r.Bien) malos++; Apunta($"   FALLA «{que}»: {r}{voz}"); }
                    }

                    CerrarPanel();
                    await Task.Delay(600);
                }
                catch (Exception ex)
                {
                    malos++;
                    Apunta($"   FALLA «{que}» REVENTÓ: {ex.GetType().Name}: {ex.Message}");
                    Log.Error($"diagnóstico de paneles, {que} en {modo}/{posicion}", ex);
                    try { CerrarPanel(); } catch { }
                    await Task.Delay(500);
                }
            }

            var (mL, bL) = await MirarLosLaterales(modo, posicion);
            mirados += mL; malos += bL;
        }

        return (mirados, malos);
    }

    // ---------- Abrir y cerrar la red diez veces ----------
    //
    // Estaba en la lista de pruebas manuales así: «abrirlo y cerrarlo diez veces: los cinco
    // botones de abajo tienen que estar SIEMPRE». Y era una prueba manual porque el fallo
    // que describe no sale a la primera: salía cuando una lectura de red llegaba vacía justo
    // al montar el panel, y entonces los botones no se creaban. Una vez de cada tantas.
    //
    // Diez aperturas seguidas lo provocan mucho mejor que una persona abriendo y cerrando, y
    // además cuentan: «los he visto las diez veces» es una afirmación, y «me ha parecido que
    // estaban» no lo es.
    //
    // Lo que se exige son las CINCO acciones fijas de `RedEstado.Todas`, no «algunos
    // botones»: la superficie estable de D-red es justamente que el juego de acciones no
    // depende de lo que devuelva Windows. Pueden estar desactivadas con su motivo —eso es
    // correcto y es el diseño—, pero no pueden faltar.
    async Task<int> AbrirYCerrarLaRedDiezVeces()
    {
        int malos = 0;
        const int veces = 10;
        Apunta("");
        Apunta($"──────── la red, abierta y cerrada {veces} veces ────────");

        var cuentas = new List<int>();
        try
        {
            for (int i = 1; i <= veces; i++)
            {
                AbrirRed();
                await Task.Delay(1100);

                int cuantos = _redBotones.Count;
                cuentas.Add(cuantos);

                if (cuantos != RedEstado.Todas.Length)
                {
                    malos++;
                    var faltan = RedEstado.Todas.Where(a => !_redBotones.ContainsKey(a))
                                                .Select(RedEstado.Nombre);
                    Apunta($"   FALLA vuelta {i}: {cuantos} de {RedEstado.Todas.Length} botones " +
                           $"(faltan: {string.Join(", ", faltan)})");
                }

                CerrarPanel();
                await Task.Delay(500);
            }

            string serie = string.Join(" ", cuentas);
            Apunta(malos == 0
                ? $"   ok   las {veces} veces salieron los {RedEstado.Todas.Length} botones: {serie}"
                : $"   FALLA la serie fue: {serie}");
        }
        catch (Exception ex)
        {
            malos++;
            Apunta($"   FALLA abrir y cerrar la red REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("abrir y cerrar la red diez veces", ex);
            try { CerrarPanel(); } catch { }
        }
        return malos;
    }

    // ---------- Y los laterales, que son ventanas aparte ----------
    //
    // No entran en la ronda de arriba porque no son paneles de la isla: son dos ventanas
    // propias que se colocan solas. Pero sufren el mismo problema con otra cara —en vez de
    // cortarse por abajo, se meten DEBAJO de la barra— y hay que mirarlas en las cinco
    // colocaciones por el mismo motivo.
    //
    // Lo que se mide es el solape de dos rectángulos de verdad: el de la ventana del panel
    // contra el de la ventana de la barra, los dos leídos de WPF. No se recalcula
    // `EstorbosParaLosLaterales`, que es justo lo que se está poniendo a prueba: comprobar una
    // cuenta con la misma cuenta no comprueba nada.
    //
    // Con la barra arriba esto pasaba aunque estuviera roto —un panel de 760 px en 1392 se
    // centra lejos de la barra—, así que el caso que lo cazó es la barra de canto: ahí el
    // choque es horizontal y no depende de lo alto que sea el contenido.
    async Task<(int Mirados, int Malos)> MirarLosLaterales(string modo, string posicion)
    {
        int malos = 0, mirados = 0;
        var lados = new[] { Lateral.Lado.Izquierda, Lateral.Lado.Derecha };

        static NubeHypr.Rect DeLaVentana(Window w) =>
            new(w.Left, w.Top, w.ActualWidth, w.ActualHeight);

        try
        {
            var barra = DeLaVentana(this);
            foreach (var l in lados)
            {
                try
                {
                    AbrirLateral(l);
                    await Task.Delay(900);

                    if (!_laterales.TryGetValue(l, out var v) || !v.IsVisible)
                    {
                        Apunta($"   --   lateral {IdDe(l)} no llegó a abrirse");
                        continue;
                    }

                    mirados++;
                    var p = DeLaVentana(v);
                    bool pisaLaBarra = HyprVista.Solapan(p, barra);
                    bool pisaElDock = _dock is { IsVisible: true } d &&
                                      HyprVista.Solapan(p, DeLaVentana(d));

                    if (pisaLaBarra || pisaElDock)
                    {
                        malos++;
                        Apunta($"   FALLA lateral {IdDe(l)} en {p.X:0},{p.Y:0} {p.Ancho:0}x{p.Alto:0}: " +
                               $"se mete debajo de {(pisaLaBarra ? "la barra" : "")}" +
                               $"{(pisaLaBarra && pisaElDock ? " y " : "")}" +
                               $"{(pisaElDock ? "el dock" : "")} " +
                               $"(barra en {barra.X:0},{barra.Y:0} {barra.Ancho:0}x{barra.Alto:0})");
                    }
                    else
                        Apunta($"   ok   lateral {IdDe(l)} en {p.X:0},{p.Y:0} " +
                               $"{p.Ancho:0}x{p.Alto:0}: libre de la barra y del dock");
                }
                catch (Exception ex)
                {
                    malos++;
                    Apunta($"   FALLA lateral {IdDe(l)} REVENTÓ: {ex.GetType().Name}: {ex.Message}");
                    Log.Error($"diagnóstico del lateral {IdDe(l)} en {modo}/{posicion}", ex);
                }
                finally
                {
                    try { CerrarLateral(l); } catch { }
                    await Task.Delay(400);
                }
            }
        }
        catch (Exception ex)
        {
            malos++;
            Apunta($"   FALLA los laterales REVENTARON: {ex.GetType().Name}: {ex.Message}");
            Log.Error($"diagnóstico de los laterales en {modo}/{posicion}", ex);
        }

        return (mirados, malos);
    }

    // La raíz de lo que hay dentro del panel abierto, sea el cajón de la isla o la ventana
    // flotante de la barra vertical. Son dos sitios distintos y el diagnóstico tiene que
    // mirar en el que toque: en vertical el panel no vive en la isla.
    FrameworkElement? RaizDelPanelAbierto()
    {
        if (PanelSuperpuestoAbierto && _superpuesta is { } sup)
            return sup.Content as FrameworkElement;
        return Cajon.Content as FrameworkElement;
    }
}
