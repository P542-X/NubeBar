using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace NubeBar;

public enum EstadoVentana { Normal, Minimizada, Maximizada }

// Una ventana REAL de Windows, con todo lo que NubeBar necesita saber de ella.
//
// Es un retrato de un instante, no un objeto vivo: el HWND puede estar muerto cuando lo
// mires. Por eso cada acción vuelve a preguntar antes de hacer nada — ver `Existe`.
public sealed class VentanaReal
{
    public IntPtr Hwnd;
    public string Titulo = "";
    public string Exe = "";              // ruta completa del ejecutable, si se pudo
    public string App = "";              // el nombre bonito: "notepad", "explorer"
    public int Pid;
    public IntPtr Monitor;               // el handle del monitor donde está
    public Nativo.RECT Rect;             // en píxeles de pantalla
    public EstadoVentana Estado;
    public bool Activa;
    public bool Visible;
    public bool SePuedeActivar;          // falso si el proceso va elevado y no nos deja
    public ImageSource? Icono;

    // Orden de uso: cuánto hace que fue la última activa. Lo lleva el gestor.
    public long UltimaVez;

    public int Ancho => Rect.Der - Rect.Izq;
    public int Alto => Rect.Aba - Rect.Arr;

    public string Estampa => Estado switch
    {
        EstadoVentana.Minimizada => "Minimizada",
        EstadoVentana.Maximizada => "Maximizada",
        _ => "Normal"
    };
}

// El gestor de ventanas: la única instantánea de lo que hay abierto.
//
// ---------- Por qué existe ----------
//
// Hasta C3, "qué ventanas hay" era una pregunta que cada sitio contestaba por su cuenta:
// el carrusel enumeraba y agrupaba, la vista general reusaba los grupos del carrusel, y
// el buscador miraba otra vez. Tres recorridos por segundo y tres criterios parecidos
// pero no iguales. Y lo peor: el criterio —qué ventana es "del usuario"— vivía dentro de
// una lambda de `EnumWindows`, así que no se podía comprobar sin abrir ventanas de
// verdad, que es justo lo que no se debe hacer en una prueba.
//
// Aquí la decisión es una FUNCIÓN PURA sobre una ficha de datos (`EsDelUsuario`), y la
// instantánea es una sola para toda la aplicación.
//
// ---------- Y por qué no se refresca cada segundo ----------
//
// Porque Windows avisa. `SetWinEventHook` fuera de contexto dice cuándo se crea, se
// destruye, se activa, se minimiza, se restaura o cambia de nombre una ventana, sin
// inyectar nada en otros procesos y sin un proceso residente. Lo que hace el gestor es
// marcar "hay que mirar" y mirar una vez, poco después: los eventos llegan a ráfagas
// —abrir el Explorador dispara ocho— y rehacer la lista ocho veces no sirve de nada.
public static class GestorVentanas
{
    // ---------- La decisión, en una función que se puede probar ----------
    //
    // No se mira SOLO el título, que era lo que hacía antes: un título vacío es
    // sospechoso pero no basta, y hay ventanas con título que no son del usuario.
    //
    // El orden de las reglas importa: primero lo que descarta sin discusión (no está
    // visible, es nuestra, está tapada) y después lo que necesita matices.
    public static bool EsDelUsuario(Nativo.FichaVentana f, out string porque)
    {
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        const int WS_EX_NOACTIVATE = 0x08000000;

        if (f.Hwnd == IntPtr.Zero) { porque = "no es una ventana"; return false; }
        if (!f.Visible) { porque = "no está visible"; return false; }
        if (f.EsNuestra) { porque = "es de NubeBar"; return false; }

        // "Cloaked" no es lo mismo que invisible: es la forma que tiene DWM de decir
        // "existe pero no se ve". Son las de otro escritorio virtual, las aplicaciones
        // de la Store suspendidas y el `CoreWindow` que vive dentro de cada una.
        if (f.Tapada) { porque = "está tapada (otro escritorio o suspendida)"; return false; }

        // Una ventana de herramientas no sale en la barra de tareas por definición, y
        // eso incluye nuestros propios paneles si algún día dejaran de ser nuestros.
        if ((f.EstiloEx & WS_EX_TOOLWINDOW) != 0) { porque = "es una ventana de herramientas"; return false; }

        // Las que no se pueden activar no sirven de nada en una vista de ventanas:
        // pulsarlas no haría nada. Son overlays, avisos y capas de dibujo.
        if ((f.EstiloEx & WS_EX_NOACTIVATE) != 0) { porque = "no se puede activar"; return false; }

        // Colgar de otra ventana significa ser su diálogo o su paleta. La excepción es
        // WS_EX_APPWINDOW, que es justo la forma documentada de decir "aunque cuelgue,
        // trátame como una aplicación". Sin esa excepción se perdían ventanas de verdad.
        if (f.TieneDueno && (f.EstiloEx & WS_EX_APPWINDOW) == 0)
        { porque = "cuelga de otra ventana"; return false; }

        if (ClaseDelSistema(f.Clase)) { porque = $"es del sistema ({f.Clase})"; return false; }

        if (f.LargoTitulo == 0) { porque = "no tiene título"; return false; }

        porque = "";
        return true;
    }

    // Las clases de ventana que son del escritorio, de la barra de Windows o del propio
    // shell. Tienen título y son visibles, así que ninguna regla general las pilla: hay
    // que conocerlas por su nombre.
    static readonly HashSet<string> DelSistema = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",                      // el escritorio
        "WorkerW",                      // la capa de debajo del fondo de pantalla
        "Shell_TrayWnd",                // la barra de tareas
        "Shell_SecondaryTrayWnd",       // la de los demás monitores
        "Button",                       // el botón de Inicio de toda la vida
        "DV2ControlHost",               // el menú Inicio antiguo
        "MsgrIMEWindowClass",
        "SysShadow",
        "Windows.UI.Core.CoreWindow",   // el núcleo de una app de la Store; el bueno es el marco
        "ForegroundStaging",
        "ApplicationManager_ImmersiveShellWindow",
        "MultitaskingViewFrame",        // la vista de tareas de Windows
        "XamlExplorerHostIslandWindow", // el Alt+Tab nuevo y los widgets del sistema
        "TaskListThumbnailWnd",
    };

    public static bool ClaseDelSistema(string? clase) => clase != null && DelSistema.Contains(clase);

    // ---------- La instantánea ----------

    static readonly List<VentanaReal> _ventanas = new();
    static readonly Dictionary<IntPtr, long> _vistas = new();   // cuándo fue activa cada una
    static long _reloj;
    static IntPtr _activa;

    public static IReadOnlyList<VentanaReal> Todas => _ventanas;
    public static IntPtr Activa => _activa;
    public static int Cuantas => _ventanas.Count;

    // ---------- Por qué cambió la lista ----------
    //
    // El motivo viaja con el aviso, y no es cosmético: la vista general reacciona de
    // forma distinta según qué haya pasado. Una activación solo cambia qué tarjeta va
    // marcada; un título solo cambia una línea de texto; que aparezca o desaparezca una
    // ventana es lo único que toca la cuadrícula. Sin el motivo, el único refresco
    // posible era "rehazlo todo", que es exactamente de donde venía el parpadeo.
    public enum Motivo
    {
        Arranque, AMano, Crear, Destruir, Mostrar, Ocultar,
        Nombre, PrimerPlano, Minimizar, Restaurar, Sitio,
    }

    public static Motivo UltimoMotivo { get; private set; } = Motivo.Arranque;

    // Qué cambió de verdad entre dos instantáneas. La vista lo usa para no hacer de más.
    [Flags]
    public enum Cambios
    {
        Nada = 0,
        Lista = 1,      // alguna ventana apareció o desapareció
        Activa = 2,     // la de delante es otra
        Titulos = 4,    // alguna cambió de título
        Estados = 8,    // alguna se minimizó, se restauró o se maximizó
        Sitios = 16,    // alguna se movió o cambió de monitor
    }

    public static Cambios UltimoCambio { get; private set; } = Cambios.Nada;

    // Avisa de que la lista ha cambiado, y de QUÉ ha cambiado. Siempre en el hilo de
    // interfaz. Solo se dispara si algo cambió de verdad: antes se disparaba en cada
    // refresco, y como el refresco lo pedía cualquier evento de Windows, la vista se
    // rehacía entera varias veces por segundo sin que hubiera pasado nada.
    public static event Action<Cambios, Motivo>? Cambio;

    // ---------- Contadores, para poder diagnosticar en vez de adivinar ----------
    public static long VecesRefrescado, VecesAvisado, VecesSinCambio;
    public static readonly Dictionary<string, long> EventosPorTipo = new();
    public static long EventosNuestros, EventosDeSitio;

    public static void OlvidarContadores()
    {
        VecesRefrescado = VecesAvisado = VecesSinCambio = 0;
        EventosNuestros = EventosDeSitio = 0;
        EventosPorTipo.Clear();
    }

    public static string Contadores() =>
        $"refrescos={VecesRefrescado} avisos={VecesAvisado} sinCambio={VecesSinCambio} " +
        $"eventosNuestros={EventosNuestros} eventosDeSitio={EventosDeSitio} " +
        $"porTipo=[{string.Join(" ", EventosPorTipo.OrderByDescending(p => p.Value).Select(p => p.Key + ":" + p.Value))}]";

    // La huella de la instantánea: con esto se sabe si hace falta avisar, y de qué.
    static string _huellaLista = "", _huellaTitulos = "", _huellaEstados = "", _huellaSitios = "";
    static IntPtr _huellaActiva;

    // Rehace la instantánea. Es lo único que escribe `_ventanas`, y solo se llama desde
    // el hilo de interfaz.
    public static void Refrescar() => Refrescar(Motivo.AMano);

    public static void Refrescar(Motivo motivo)
    {
        try
        {
            VecesRefrescado++;
            UltimoMotivo = motivo;
            var antes = _ventanas.Count;
            var activa = Nativo.Activa();
            if (activa != IntPtr.Zero && !Nativo.EsNuestra(activa)) _activa = activa;

            var nuevas = new List<VentanaReal>();
            foreach (var h in Nativo.TodasLasDeNivelSuperior())
            {
                var f = Nativo.Ficha(h);
                if (!EsDelUsuario(f, out _)) continue;

                // La hora de uso se conserva entre refrescos: es lo que ordena la vista.
                if (h == _activa) _vistas[h] = ++_reloj;
                else if (!_vistas.ContainsKey(h)) _vistas[h] = 0;

                var exe = Nativo.Exe(h) ?? "";
                nuevas.Add(new VentanaReal
                {
                    Hwnd = h,
                    Titulo = Nativo.Titulo(h),
                    Exe = exe,
                    App = NombreDeApp(exe),
                    Pid = f.Pid,
                    Monitor = Nativo.MonitorDe(h),
                    Rect = Nativo.Rectangulo(h),
                    Estado = Nativo.EstaMinimizada(h) ? EstadoVentana.Minimizada
                           : Nativo.EstaMaximizada(h) ? EstadoVentana.Maximizada
                           : EstadoVentana.Normal,
                    Activa = h == _activa,
                    Visible = f.Visible,
                    SePuedeActivar = Nativo.ProcesoAlcanzable(f.Pid),
                    UltimaVez = _vistas[h],
                });
            }

            // Orden: la activa primero, después por lo reciente que fue su último turno,
            // y a igualdad por título y handle. Lo último es lo que lo hace ESTABLE: sin
            // ello, dos ventanas que nunca han estado activas bailarían entre refrescos
            // porque EnumWindows devuelve orden Z, que cambia solo.
            //
            // Ojo: este es el orden de la INSTANTÁNEA, y cambia al cambiar el foco. La
            // vista general NO lo sigue mientras está abierta —congela el suyo— porque
            // si no, enfocar otra aplicación desde fuera le recolocaba las tarjetas.
            nuevas.Sort(Comparar);

            _ventanas.Clear();
            _ventanas.AddRange(nuevas);

            // Las que ya no existen dejan de ocupar sitio en la memoria de usos
            if (_vistas.Count > 400)
            {
                var vivas = new HashSet<IntPtr>(_ventanas.Select(v => v.Hwnd));
                foreach (var muerta in _vistas.Keys.Where(k => !vivas.Contains(k)).ToList())
                    _vistas.Remove(muerta);
            }

            // ---------- Y ahora: cambió algo de verdad? ----------
            //
            // Cuatro huellas separadas en vez de una, porque hace falta saber QUÉ
            // cambió y no solo que algo cambió. La lista es lo único que obliga a tocar
            // la cuadrícula; lo demás se arregla actualizando la tarjeta que ya existe.
            var hLista = string.Join(",", _ventanas.Select(v => v.Hwnd.ToInt64().ToString("X")));
            var hTitulos = string.Join("\u0001", _ventanas.Select(v => v.Titulo));
            var hEstados = string.Join(",", _ventanas.Select(v => (int)v.Estado));
            var hSitios = string.Join(",", _ventanas.Select(
                v => v.Rect.Izq + "/" + v.Rect.Arr + "/" + v.Ancho + "x" + v.Alto + "/" + v.Monitor.ToInt64().ToString("X")));

            var que = Cambios.Nada;
            if (hLista != _huellaLista) que |= Cambios.Lista;
            if (_activa != _huellaActiva) que |= Cambios.Activa;
            if (hTitulos != _huellaTitulos) que |= Cambios.Titulos;
            if (hEstados != _huellaEstados) que |= Cambios.Estados;
            if (hSitios != _huellaSitios) que |= Cambios.Sitios;

            _huellaLista = hLista; _huellaTitulos = hTitulos;
            _huellaEstados = hEstados; _huellaSitios = hSitios; _huellaActiva = _activa;
            UltimoCambio = que;

            if (antes != _ventanas.Count)
                Log.Info($"ventanas: {_ventanas.Count} (antes {antes}) motivo={motivo}");

            // Y aquí está la diferencia con lo de antes: si no cambió nada, NO se avisa.
            // Un Cambio por refresco, con el refresco colgado de cualquier evento de
            // Windows, es lo que hacía que la vista se rehiciera sola sin parar.
            if (que == Cambios.Nada) { VecesSinCambio++; return; }

            VecesAvisado++;
            Cambio?.Invoke(que, motivo);
        }
        catch (Exception ex) { Log.Error("refrescar las ventanas", ex); }
    }

    // El orden, aparte y público para poder comprobarlo con fichas inventadas.
    public static int Comparar(VentanaReal a, VentanaReal b)
    {
        if (a.Activa != b.Activa) return a.Activa ? -1 : 1;
        int c = b.UltimaVez.CompareTo(a.UltimaVez);
        if (c != 0) return c;
        c = string.Compare(a.App, b.App, StringComparison.OrdinalIgnoreCase);
        if (c != 0) return c;
        c = string.Compare(a.Titulo, b.Titulo, StringComparison.OrdinalIgnoreCase);
        if (c != 0) return c;
        return a.Hwnd.ToInt64().CompareTo(b.Hwnd.ToInt64());
    }

    // "C:\...\notepad.exe" -> "notepad". Y si no hay ruta, lo que haya.
    public static string NombreDeApp(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return "";
        try { return Path.GetFileNameWithoutExtension(exe); }
        catch { return exe; }
    }

    public static IEnumerable<VentanaReal> DeMonitor(IntPtr monitor) =>
        _ventanas.Where(v => v.Monitor == monitor);

    public static IEnumerable<string> Aplicaciones() =>
        _ventanas.Select(v => v.App).Where(a => a.Length > 0)
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .OrderBy(a => a, StringComparer.OrdinalIgnoreCase);

    public static VentanaReal? Buscar(IntPtr h) => _ventanas.FirstOrDefault(v => v.Hwnd == h);

    // ---------- Escuchar a Windows ----------

    static System.Windows.Threading.Dispatcher? _interfaz;
    static System.Windows.Threading.DispatcherTimer? _poso;
    static bool _encendido;

    // Los eventos llegan a ráfagas: abrir el Explorador dispara crear, mostrar, nombre y
    // primer plano en pocos milisegundos. Rehacer la lista en cada uno es tirar trabajo;
    // se marca que hay que mirar y se mira una vez, 90 ms después.
    const int MsDePoso = 90;

    public static void Arrancar(System.Windows.Threading.Dispatcher interfaz)
    {
        if (_encendido) return;
        _encendido = true;
        _interfaz = interfaz;

        _poso = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(MsDePoso),
            System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => { _poso!.Stop(); Refrescar(_motivoPendiente); },
            interfaz);
        _poso.Stop();

        // Un gancho por tramo, no uno por evento: Windows cobra por gancho instalado.
        Nativo.EscucharEventos(Nativo.EvtPrimerPlano, Nativo.EvtRestaurar, AlPasarAlgo);
        Nativo.EscucharEventos(Nativo.EvtCrear, Nativo.EvtNombre, AlPasarAlgo);

        Refrescar(Motivo.Arranque);
        Log.Info("gestor de ventanas en marcha (eventos de Windows, sin sondeo)");
    }

    public static void Parar()
    {
        if (!_encendido) return;
        _encendido = false;
        _poso?.Stop();
        _poso = null;
        Nativo.DejarDeEscuchar();
        MoverSeEmpieza = null;
        MoverSeTermina = null;
    }

    // Quien quiera enterarse de que el usuario está arrastrando una ventana. Lo usa el
    // ajuste a los bordes; vive aquí porque el gancho ya está puesto y poner otro sería
    // pagar dos veces por lo mismo.
    public static event Action<IntPtr>? MoverSeEmpieza;
    public static event Action<IntPtr>? MoverSeTermina;

    // ---------- El callback ----------
    //
    // Llega en el hilo que instaló el gancho, que es el de interfaz, pero NO se toca la
    // interfaz aquí: lo único que se hace es encender el temporizador.
    //
    // ---------- Las dos cosas que ya NO se escuchan ----------
    //
    // **1. Lo que le pasa a nuestras propias ventanas.** Esto era un bucle que se
    // alimentaba solo, y es el origen del parpadeo de la vista general: la isla se mueve
    // y se redimensiona constantemente —las animaciones, abrir un panel, la vuelta a su
    // sitio, el reajuste del alto— y cada `SetWindowPos` sobre una ventana propia
    // dispara `EVENT_OBJECT_LOCATIONCHANGE`. Ese aviso pedía refrescar; el refresco
    // avisaba de un cambio; la vista se rehacía entera; al rehacerse volvía a medir y a
    // recolocar la ventana, y eso disparaba otro `LOCATIONCHANGE`.
    //
    // Una ventana nuestra **nunca** entra en la lista —`EsDelUsuario` la descarta por
    // `EsNuestra`— así que enterarse de lo que le pasa no podía cambiar nada de lo que
    // se enseña: solo costaba un refresco y un repintado.
    //
    // **2. `LOCATIONCHANGE` de cualquiera.** Mover o redimensionar una ventana no la
    // añade, no la quita y no la renombra. Dispara cientos de avisos por segundo
    // mientras alguien arrastra y no cambia nada de lo que la vista enseña. Se cuenta,
    // para poder verlo en el diagnóstico, y no se refresca por él: el siguiente evento
    // de verdad —o el refresco al abrir la vista— recoge la geometría nueva.
    static void AlPasarAlgo(IntPtr gancho, uint evento, IntPtr hwnd, int objeto, int hijo,
                            uint hilo, uint cuando)
    {
        try
        {
            // OBJID_WINDOW = 0: lo que pasa en un botón de dentro de una ventana no es
            // asunto nuestro, y es la inmensa mayoría de los avisos.
            if (objeto != 0 || hwnd == IntPtr.Zero) return;

            // Lo nuestro, ni mirarlo. Ver arriba: era el bucle del parpadeo.
            if (Nativo.EsNuestra(hwnd)) { EventosNuestros++; return; }

            Contar(evento);

            if (evento == Nativo.EvtPrimerPlano) _activa = hwnd;

            if (evento == Nativo.EvtMoverEmpieza) { Avisar(MoverSeEmpieza, hwnd); return; }
            if (evento == Nativo.EvtMoverTermina) { Avisar(MoverSeTermina, hwnd); return; }

            // Moverse no cambia la lista. Se cuenta y se deja pasar.
            if (evento == Nativo.EvtSitio) { EventosDeSitio++; return; }

            HayQueMirar(DeEvento(evento));
        }
        catch (Exception ex) { Log.Error("evento de ventana", ex); }
    }

    static void Contar(uint evento)
    {
        var k = NombreDeEvento(evento);
        EventosPorTipo[k] = EventosPorTipo.TryGetValue(k, out long n) ? n + 1 : 1;
    }

    static string NombreDeEvento(uint evento) => evento switch
    {
        Nativo.EvtPrimerPlano => "primerPlano",
        Nativo.EvtMoverEmpieza => "moverEmpieza",
        Nativo.EvtMoverTermina => "moverTermina",
        Nativo.EvtCrear => "crear",
        Nativo.EvtDestruir => "destruir",
        Nativo.EvtSitio => "sitio",
        Nativo.EvtNombre => "nombre",
        Nativo.EvtRestaurar => "restaurar",
        _ => "0x" + evento.ToString("X"),
    };

    internal static Motivo DeEvento(uint evento) => evento switch
    {
        Nativo.EvtPrimerPlano => Motivo.PrimerPlano,
        Nativo.EvtCrear => Motivo.Crear,
        Nativo.EvtDestruir => Motivo.Destruir,
        Nativo.EvtNombre => Motivo.Nombre,
        Nativo.EvtRestaurar => Motivo.Restaurar,
        Nativo.EvtSitio => Motivo.Sitio,
        _ => Motivo.AMano,
    };

    static bool _arrastrando;

    // Si el usuario está arrastrando una ventana de verdad ahora mismo. El ajuste a los
    // bordes y la vista lo preguntan para no pisarse.
    public static bool ArrastrandoVentana => _arrastrando;

    static void Avisar(Action<IntPtr>? quien, IntPtr h)
    {
        // Comparar delegados no valía con los dos a null (nadie suscrito): `null == null`
        // daba true y dejaba `_arrastrando` puesto justo al soltar.
        _arrastrando = quien != null && ReferenceEquals(quien, MoverSeEmpieza);
        var d = _interfaz;
        if (d == null) return;
        d.BeginInvoke(new Action(() => { try { quien?.Invoke(h); } catch (Exception ex) { Log.Error("aviso de arrastre", ex); } }));
    }

    public static void HayQueMirar() => HayQueMirar(Motivo.AMano);

    public static void HayQueMirar(Motivo motivo)
    {
        var t = _poso;
        if (t == null) return;
        _motivoPendiente = motivo;
        if (t.Dispatcher.CheckAccess()) { t.Stop(); t.Start(); }
        else t.Dispatcher.BeginInvoke(new Action(() => { t.Stop(); t.Start(); }));
    }

    static Motivo _motivoPendiente = Motivo.AMano;
}
