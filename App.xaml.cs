using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NubeBar;

public partial class App : Application
{
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

        // "NubeBar.exe --diagvista [segundos]": arranca NubeBar DE VERDAD, abre la vista
        // general, la deja quieta y cuenta qué le pasa. El bucle que hacía parpadear la
        // cuadrícula solo existe con la isla, sus animaciones y los ganchos de Windows en
        // marcha, así que no se puede medir con un arnés aparte. Ver DiagVista.cs.
        DiagVista.Leer(e.Args);
        DiagInicio.Leer(e.Args);      // "--diaginicio": mide la superficie de Inicio y se cierra
        DiagBandeja.Leer(e.Args);     // "--diagbandeja": vigila la bandeja un rato y se cierra

        if (e.Args.Length > 0 && e.Args[0] == Guardian.Bandera)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Guardian.Vigilar(e.Args);      // bloquea hasta que haya que actuar
            Shutdown(0);
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
