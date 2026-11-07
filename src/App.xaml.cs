using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NubeBar;

public partial class App : Application
{
    // ---------- Todas las banderas que esta versión entiende ----------
    //
    // En un sitio, y usada de verdad por el arranque: lo que no está aquí no arranca nada.
    // `Config.BanderasQueMiran` es un subconjunto de esta —las que además no pueden
    // escribir la configuración— y hay una prueba que lo exige, para que no se pueda añadir
    // una bandera de medida y olvidarse de protegerla.
    public static readonly string[] Banderas =
    {
        Salir.Bandera, Pruebas.Bandera, Auditoria.Bandera, Guardian.Bandera,
        DiagVista.Bandera, DiagInicio.Bandera, DiagBandeja.Bandera, DiagHypr.Bandera,
        DiagMotor.Bandera, DiagEspacios.Bandera, DiagAtajos.Bandera, DiagReglas.Bandera,
        DiagD6.Bandera, DiagD7.Bandera, DiagPrefijos.Bandera,
        ArnesEspacios.Bandera, ArnesAplicar.Bandera, ArnesLayout.Bandera, DiagCapturas.Bandera,
        DiagVigilia.Bandera, DiagGuia.Bandera, DiagGuia.BanderaAlias, DiagE0.Bandera,
        DiagModulos.Bandera, DiagVentanita.Bandera, DiagPaneles.Bandera, DiagRejilla.Bandera,
        DiagClima.Bandera, DiagNeon.Bandera,
    };

    public static bool BanderaConocida(string? a) =>
        a != null && Banderas.Contains(a, StringComparer.OrdinalIgnoreCase);


    // El mismo ejecutable hace dos papeles. Con --guardian no abre ninguna ventana:
    // se queda esperando a que el NubeBar de verdad muera para devolver el shell
    // (ver Guardian.cs). Sin argumentos, crea una isla por monitor y la del monitor
    // principal es la MainWindow, la que manda y la que cierra la aplicación.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Redes de seguridad. Una excepción suelta en un hilo de fondo no la caza
        // nadie y se lleva por delante el proceso entero (le pasó al buscador con
        // System.Data.OleDb). Aquí no se puede evitar el cierre, pero al menos queda
        // escrito en el log qué fue. Las de la interfaz sí se pueden perdonar: más
        // vale una isla con un módulo tonto que quedarte sin barra de tareas.
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            var ex = a.ExceptionObject as Exception;
            Log.Error("excepción sin capturar en un hilo de fondo", ex ?? new Exception(a.ExceptionObject?.ToString() ?? "desconocida"));
            // Nos vamos a pique, pero no con la pantalla pintada de negro
            EscenaArranque.Cerrar("excepción sin capturar");
        };
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Log.Error("tarea que falló y nadie miró", a.Exception);
            a.SetObserved();
        };
        DispatcherUnhandledException += (_, a) =>
        {
            Log.Error("excepción en la interfaz (no se cierra la aplicación)", a.Exception);
            a.Handled = true;
        };

        // ---------- Las herramientas miran y no tocan ----------
        //
        // Va aquí arriba, antes que cualquier rama, porque algunas de estas se van por su
        // `return` sin llegar más abajo. Mientras corre una herramienta de medida no se
        // escribe el config del usuario: ni la migración del esquema, ni los módulos de
        // serie, ni el orden de las apps.
        //
        // El motivo es medido, no teórico: ejecutar un binario de desarrollo con un
        // esquema más nuevo dejó el config real en una versión que la NubeBar instalada
        // —más antigua— ya no sabía migrar, y le apuntaba un error en cada arranque. Y no
        // lo hizo la migración: lo hizo un guardado rutinario, porque el modelo tipado
        // lleva `Version = Esquema.Actual` y cualquier escritura sube la versión.
        //
        // D8: la decisión vive en `Config.MiraYNoToca`, que es una función pura sobre los
        // argumentos. Estaba escrita aquí dentro con un `is ... or ...` de seis líneas, y eso
        // no se podía comprobar sin arrancar la aplicación: la única forma de probar la
        // protección era ejecutar un binario de desarrollo y mirar si el config del usuario
        // sobrevivía — que es exactamente lo que no se puede hacer.
        //
        // Y ahí está también `--salir`, que antes quedaba fuera con un razonamiento que
        // confundía dos procesos. Ver el comentario de `BanderasQueMiran`: solo el guardián
        // puede escribir, porque es el vigilante.
        Config.SoloLectura = Config.MiraYNoToca(e.Args);

        // ---------- Un binario de fase no arranca como barra ----------
        //
        // Va ANTES que todo lo demás: antes de cargar la configuración, que es lo que migra.
        // Ponerlo después sería cerrar la puerta cuando ya han pasado.
        //
        // Ver `Fase` para el porqué: ha pasado tres veces, y las dos primeras se intentaron
        // arreglar con cuidado y con un nombre de carpeta disuasorio. El cuidado no escala y
        // el nombre no se lee cuando lo que quieres es tu barra de vuelta.
        if (Fase.HayQueNegarse(Environment.ProcessPath, e.Args))
        {
            // ---------- Y lo PRIMERO es cerrar la configuración ----------
            //
            // Negarse a arrancar no basta, y esto costó un cuarto accidente con el config del
            // usuario para aprenderlo. `Shutdown(6)` no termina el proceso ahí: pasa por
            // `OnExit`, y `OnExit` llama a `CerrarIntegracionDeFondo` → `Config.Cargar()` →
            // y eso MIGRA. Sin banderas, `Config.SoloLectura` es falso, así que la puerta
            // trasera estaba abierta de par en par.
            //
            // Es exactamente el mismo camino que ya había mordido a `--salir` y que está
            // documentado en `BanderasQueMiran`: «su proceso solo manda la señal de salida,
            // pero al irse pasa por OnExit → Config.Cargar() y eso ejecuta las migraciones».
            // Lo que faltaba era aplicar esa lección aquí.
            //
            // Y va antes del log, no después, para que la frase «la configuración no se ha
            // tocado» sea una consecuencia de lo que acaba de hacer el código y no una
            // promesa. La anterior decía lo mismo y era mentira.
            Config.SoloLectura = true;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Console.WriteLine(Fase.Explicacion);
            Log.Info($"arranque rechazado: «{Environment.ProcessPath}» es un binario de fase " +
                     "y se ha pedido arrancarlo como barra normal. La configuración queda en " +
                     "solo lectura: ni se migra ni se escribe, tampoco al salir.");
            Shutdown(6);
            return;
        }

        // "NubeBar.exe --salir": cierra la que esté en marcha y se va. Es como lo
        // quita de en medio el instalador antes de tocar su exe, y sirve igual para
        // un atajo o un script. No abre ninguna ventana.
        if (e.Args.Length > 0 && e.Args[0] == Salir.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Salir.Pedir();
            Shutdown(0);
            return;
        }

        // "NubeBar.exe --ventanita «título» [segundos]": una ventana pelada que se cierra
        // sola. Ver DiagVentanita: existe para poder comprobar que una regla coloca una
        // ventana al aparecer, sin tocar ninguna ventana del usuario.
        //
        // Va ANTES de la comprobación de instancia única: la gracia es que corra MIENTRAS
        // hay una NubeBar de verdad funcionando, que es quien tiene que verla aparecer.
        DiagVentanita.Leer(e.Args);
        if (DiagVentanita.Encendido)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var v = new Window
            {
                Title = DiagVentanita.Titulo,
                Width = 500, Height = 340, Left = 60, Top = 60,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = true,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Ventana de prueba de NubeBar.\n\n" +
                           "Si hay una regla que hable de ella, debería moverse sola.\n" +
                           "Se cierra sin ayuda.",
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            v.Show();
            var reloj = new System.Windows.Threading.DispatcherTimer
            { Interval = DiagVentanita.Cuanto };
            reloj.Tick += (_, _) => { reloj.Stop(); try { v.Close(); } catch { } Shutdown(0); };
            reloj.Start();
            return;
        }

        // "NubeBar.exe --pruebas [informe.txt]": corre las comprobaciones de lógica y se
        // va. No abre ninguna ventana, no toca el config y devuelve 1 si algo falla,
        // para poder encadenarlo.
        if (e.Args.Length > 0 && e.Args[0] == Pruebas.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(Pruebas.Correr(e.Args));
            return;
        }

        // "NubeBar.exe --ventanas [informe.txt]": qué ventanas ve y por qué descarta las
        // demás. Solo lee: no activa, no mueve ni cierra nada. Está para que "faltan
        // aplicaciones" se pueda investigar con los datos de la máquina en vez de a base
        // de cambiar el filtro a ciegas.
        if (e.Args.Length > 0 && e.Args[0] == Auditoria.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(Auditoria.Correr(e.Args));
            return;
        }

        // "NubeBar.exe --diagclima [ciudad] [informe.txt]": pide el tiempo DE VERDAD y
        // comprueba que lo que vuelve es una temperatura y no cualquier cosa. Va aquí, con
        // los que no abren ventana, porque el clima no necesita ninguna: es una petición y
        // una tabla. Y no lee la ciudad del usuario — usa la que se le pase, o Madrid: un
        // diagnóstico que mide con los datos de alguien no se puede repetir.
        if (e.Args.Length > 0 && e.Args[0] == DiagClima.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(DiagClima.Correr(e.Args));
            return;
        }

        // "NubeBar.exe --diagmonitores [--cambiar <clave|nº> <hex>] [informe.txt]": qué monitores
        // hay, quiénes son según su EDID y qué entradas de vídeo gestionan, preguntado POR EL
        // CABLE. Va aquí, con los que no abren ventana, porque no necesita ninguna: es una tabla.
        //
        // Sin `--cambiar` solo LEE. Ningún diagnóstico le cambia el canal a tu monitor por su
        // cuenta: ver DiagMonitores.cs.
        if (e.Args.Length > 0 && e.Args[0] == DiagMonitores.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(DiagMonitores.Correr(e.Args));
            return;
        }

        // "NubeBar.exe --diagvista [segundos]": arranca NubeBar DE VERDAD, abre la vista
        // general, la deja quieta y cuenta qué le pasa. El bucle que hacía parpadear la
        // cuadrícula solo existe con la isla, sus animaciones y los ganchos de Windows en
        // marcha, así que no se puede medir con un arnés aparte. Ver DiagVista.cs.
        DiagVista.Leer(e.Args);
        DiagInicio.Leer(e.Args);      // "--diaginicio": mide la superficie de Inicio y se cierra
        DiagBandeja.Leer(e.Args);     // "--diagbandeja": vigila la bandeja un rato y se cierra
        DiagHypr.Leer(e.Args);        // "--diaghypr": la línea base de D0, apagado no mueve nada
        DiagMotor.Leer(e.Args);       // "--diagmotor": el motor de D2, en simulación
        DiagEspacios.Leer(e.Args);    // "--diagespacios": lee los escritorios virtuales
        DiagAtajos.Leer(e.Args);      // "--diagatajos": registra los atajos de verdad y los suelta
        DiagReglas.Leer(e.Args);      // "--diagreglas": resuelve las reglas de D5 sin mover nada
        DiagD6.Leer(e.Args);          // "--diagd6": monitores, DPI, contraste y bordes, en lectura
        DiagD7.Leer(e.Args);          // "--diagd7": supervisor, modo seguro y cierre limpio
        DiagPrefijos.Leer(e.Args);    // "--diagprefijos": qué prefijo de atajos está libre
        ArnesEspacios.Leer(e.Args);   // "--arnesespacios": cierra D4 sin tocar nada tuyo
        ArnesAplicar.Leer(e.Args);    // "--arnesaplicar": mueve UNA ventana nuestra, de verdad
        ArnesLayout.Leer(e.Args);     // "--arneslayout": reparte un escritorio CREADO para eso
        DiagRejilla.Leer(e.Args);     // "--diagrejilla": la vista con 2, 5, 7 y 12 ventanas
        DiagCapturas.Leer(e.Args);    // "--capturas": una foto de cada superficie, para mirarlas
        DiagNeon.Leer(e.Args);        // "--diagneon": el halo, fotografiado a lo largo de dos ciclos
        DiagVigilia.Leer(e.Args);     // "--vigilia":  la barra funcionando un rato, midiéndose
        DiagGuia.Leer(e.Args);        // "--guia":     la guía de reglas, sin poder guardar nada
        DiagE0.Leer(e.Args);          // "--diage0":   el tablero de superficies contra la realidad
        // "--diagmodulos": añadir un módulo y comprobar que SALE. Va el último de la tanda a
        // propósito, pero lo que importa es que va ANTES de que nadie cargue el config: es
        // ahí donde monta su arenero temporal y redirige `Config.Carpeta`. Montado más
        // tarde, el arranque ya habría leído —y podido migrar— el fichero de verdad.
        DiagModulos.Leer(e.Args);
        DiagPaneles.Leer(e.Args);     // "--diagpaneles": que no se corte ningún panel

        if (e.Args.Length > 0 && e.Args[0] == Guardian.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Guardian.Vigilar(e.Args);      // bloquea hasta que haya que actuar
            Shutdown(0);
            return;
        }

        // ================= Una bandera que no se conoce NO arranca la barra =================
        //
        // ---------- El accidente que esto evita, medido dos veces ----------
        //
        // Hasta aquí, un argumento que empezara por `--` y que esta versión no conociera se
        // caía por todos los `if` y terminaba en el arranque normal. O sea: pedirle a un
        // binario viejo un diagnóstico que todavía no existe **montaba la barra entera** y,
        // con ella, migraba el config del usuario al esquema de ese binario.
        //
        // Me pasó dos veces en la misma sesión: un `dist-d8/NubeBar.exe --diagprefijos`
        // sobre un ejecutable que aún no tenía esa bandera se convirtió en una NubeBar
        // completa que migró el config de la v3 a la v4 y se quedó corriendo diez minutos.
        //
        // Un `--loquesea` desconocido es, casi siempre, una errata o un binario
        // equivocado. Lo correcto es decirlo y salir con código 2, no tomar el escritorio.
        //
        // Lo que NO se filtra: los argumentos que no empiezan por `--`. El acceso directo
        // del instalador, el arranque de Windows y un doble clic no pasan ninguno, y si
        // alguien pasa una ruta por lo que sea, eso no es una bandera.
        if (e.Args.Length > 0 && e.Args[0].StartsWith("--") && !BanderaConocida(e.Args[0]))
        {
            Log.Info($"«{e.Args[0]}» no es una opción que esta versión conozca: no se arranca " +
                     "la barra. Opciones: " + string.Join(" ", Banderas));
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(2);
            return;
        }

        // Una sola NubeBar a la vez. El guardián queda fuera de esta comprobación
        // a propósito: es el mismo ejecutable y tiene que poder convivir con ella.
        if (!Instancia.SoyLaPrimera())
        {
            Log.Info("ya hay otra NubeBar abierta: esta se cierra sin tocar nada");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(0);
            return;
        }

        // ---------- Lo primero que se pregunta, y solo una vez ----------
        //
        // Va aquí y no más tarde por dos motivos. Uno: tiene que estar ANTES de que se
        // dibuje nada —la escena de entrada, las islas, el saludo del menú Inicio—, o la
        // primera cosa que vea alguien con Windows en inglés estaría en español. Y dos:
        // va DESPUÉS del cerrojo de instancia única, para que no salgan dos ventanas
        // preguntando lo mismo si se abre el programa dos veces.
        //
        // Solo en el arranque normal. Un diagnóstico no puede escribir el config (ver
        // Config.PuedeEscribir), así que preguntar sería pedirle al usuario una respuesta
        // que después se tira: mejor no preguntar.
        try
        {
            if (!Config.SoloLectura)
            {
                var cfg = Config.Cargar();
                if (cfg != null && ElegirIdioma.HayQuePreguntar(cfg)) ElegirIdioma.Preguntar(cfg);
            }
        }
        catch (Exception ex) { Log.Error("preguntar el idioma al arrancar", ex); }

        // La escena de entrada va ANTES de montar las islas: lo que tapa es justamente
        // el rato feo del arranque (Explorer recalculando el área de trabajo, la barra
        // de Windows escondiéndose, el carrusel llenándose). Si no toca mostrarla, no
        // crea nada y esto no cuesta nada. Ver EscenaArranque.
        EscenaArranque.Arrancar();

        // ---------- Y justo detrás, Wallpaper Engine ----------
        //
        // En este orden y no al revés: primero la capa de la escena protege la pantalla,
        // y DESPUÉS se le pide el fondo. Al contrario se vería el escritorio viejo
        // cambiando de wallpaper a la vista, que es justo el rato feo que la escena tapa.
        //
        // Va sin `await` a propósito: esto es `OnStartup` y bloquearlo aquí dejaría la
        // escena sin montar las islas detrás. La escena ya sabe esperar por su cuenta
        // (`EsperarAlFondo`) y abrirse igual si el fondo no llega.
        ArrancarIntegracionDeFondo();

        try
        {
            MainWindow = NubeBar.MainWindow.Arrancar();
        }
        catch
        {
            // Si la nube no llega a montarse, lo último que puede pasar es dejar al
            // usuario con la pantalla negra de la escena delante.
            EscenaArranque.Cerrar("la nube no pudo arrancar");
            throw;
        }

        // Y a la escucha de "NubeBar.exe --salir". Se cierran TODAS las islas, no
        // solo la jefa: con dos pantallas, cerrar la jefa deja viva la otra y el
        // proceso no termina (ShutdownMode es OnLastWindowClose). La jefa va la
        // última porque es la que desmonta lo global y devuelve las barras.
        Instancia.EscucharSalida(() => Dispatcher.BeginInvoke(new Action(() =>
        {
            Log.Info("salida pedida desde fuera (--salir)");
            // La escena primero: es una ventana más y, si se cerrara la última isla
            // con ella abierta, se quedaría sola en pantalla y en negro.
            EscenaArranque.Cerrar("--salir");
            foreach (var isla in NubeBar.MainWindow.Islas.ToList().Where(i => i != MainWindow))
                isla.Close();
            (MainWindow as Window)?.Close();
        })));
    }


    // ================= La integración con Wallpaper Engine =================
    //
    // El orden importa y es el que se pidió:
    //
    //   1. la capa de la escena ya está puesta (se hizo justo antes);
    //   2. se le dice a la escena que espere al fondo, y cuánto como mucho;
    //   3. se abre Wallpaper Engine si toca y se le pide el wallpaper o la playlist;
    //   4. cuando el fondo está —o se agota la espera— se levanta esa condición;
    //   5. la nube se abre y va revelando el escritorio CON el wallpaper ya puesto.
    //
    // El revelado progresivo no se programa aquí ni en ningún sitio: la escena es un
    // rectángulo negro con un agujero del tamaño de la nube, así que el wallpaper asoma
    // por donde la nube ya ha llegado, y nada más. No hay una segunda animación.
    static CancellationTokenSource? _cortarFondo;

    void ArrancarIntegracionDeFondo()
    {
        OpcionesWallpaper o;
        try
        {
            o = Config.Cargar()?.Wallpaper ?? new OpcionesWallpaper();
        }
        catch (Exception ex) { Log.Error("leer las opciones de Wallpaper Engine", ex); return; }

        if (!o.Integrar) return;

        // Si la escena no está en marcha —porque no tocaba mostrarla— no hay a quién
        // hacer esperar, pero el fondo se carga igual: la integración es del fondo, no
        // de la escena.
        double espera = Integracion.EsperaValida(o.SegundosEspera);
        if (EscenaArranque.EnMarcha) EscenaArranque.EsperarAlFondo(espera);

        _cortarFondo = new CancellationTokenSource(TimeSpan.FromSeconds(espera + 5));
        var ct = _cortarFondo.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var r = await Integracion.PrepararAsync(o, ct).ConfigureAwait(false);
                EscenaArranque.FondoListo(r.FondoListo ? r.Detalle : "no llegó: " + r.Detalle);
                Log.Info($"Wallpaper Engine: integración terminada en {r.Ms} ms " +
                         $"(estado={r.Estado}, fondo={(r.FondoListo ? "listo" : "no")})");
            }
            catch (OperationCanceledException)
            {
                // Te la has saltado con Esc, o se agotó el margen. Las dos cosas son
                // normales y la escena ya se ha encargado de no dejar nada tapado.
                EscenaArranque.FondoListo("cancelado");
            }
            catch (Exception ex)
            {
                Log.Error("la integración con Wallpaper Engine", ex);
                // Pase lo que pase, se levanta la puerta: un fallo aquí no puede dejar la
                // nube cerrada esperando a algo que no va a llegar.
                EscenaArranque.FondoListo("falló: " + ex.GetType().Name);
            }
        }, ct);
    }

    // Al salir. Solo toca lo que NubeBar haya abierto: ver Integracion.QueHacerAlSalir.
    internal static void CerrarIntegracionDeFondo()
    {
        try
        {
            _cortarFondo?.Cancel();
            _cortarFondo?.Dispose();
            _cortarFondo = null;
            var o = Config.Cargar()?.Wallpaper;
            if (o is { Integrar: true }) Integracion.AlSalir(o);
        }
        catch (Exception ex) { Log.Error("cerrar la integración con Wallpaper Engine", ex); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CerrarIntegracionDeFondo();
        Instancia.Soltar();
        base.OnExit(e);
    }
}
