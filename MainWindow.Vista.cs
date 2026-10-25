using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace NubeBar;

// Vista general tipo Exposé: todas las ventanas REALES abiertas, en tarjetas, con
// atajo global para abrirla sin tocar el ratón.
//
// ---------- Qué cambió en C4 ----------
//
// Antes esto reusaba los grupos del carrusel, que son una vista de la barra de tareas:
// solo las ventanas de ESTE monitor, agrupadas por ejecutable y recortadas por lo que
// cupiera. Ahora lee de `GestorVentanas`, que es la instantánea de verdad, así que la
// vista enseña todo lo que hay, sabe en qué monitor está cada cosa y se entera sola
// cuando algo aparece o desaparece mientras está abierta.
//
// Y las tarjetas llevan foto de la ventana. No son miniaturas de DWM —no se componen
// dentro de ventanas por capas y todas las nuestras lo son— sino un `PrintWindow`
// sacado por detrás. Si no sale, la tarjeta enseña el icono y el título, que es lo que
// tiene que pasar: nunca un rectángulo roto.
public partial class MainWindow
{
    internal const string PanelVista = "vista";
    const int IdAtajoVista = 0xB0CA;
    const int MaxEnVista = 24;
    const double AnchoTarjeta = 236, AltoTarjeta = 158, AltoPie = 44;

    Button? _botonVista;
    TextBlock? _textoVista;
    HwndSource? _fuente;
    string? _filtroApp;                 // null = todas
    bool _vistaEscuchando;

    // ================= Atajo global =================
    void RegistrarAtajos()
    {
        _fuente = HwndSource.FromHwnd(_self);
        _fuente?.AddHook(Mensaje);
        AplicarAtajo();
    }

    void AplicarAtajo()
    {
        if (_self == IntPtr.Zero || !_esJefa) return;   // un solo dueño del atajo global
        var atajo = _cfg.Barra.AtajoVistaGeneral;
        if (string.IsNullOrWhiteSpace(atajo)) { Nativo.QuitarAtajo(_self, IdAtajoVista); return; }

        bool ok = Nativo.RegistrarAtajo(_self, IdAtajoVista, atajo);
        string motivo = Nativo.UltimoErrorAtajo switch
        {
            0 => "",
            -1 => " (no entiendo la combinación)",
            1409 => " (ya lo tiene cogido otra aplicación)",
            _ => $" (error {Nativo.UltimoErrorAtajo})"
        };
        Log.Info($"atajo de la vista general \"{atajo}\": {(ok ? "registrado" : "NO registrado" + motivo)}");
    }

    IntPtr Mensaje(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool manejado)
    {
        if (msg != Nativo.WM_HOTKEY || wParam.ToInt32() != IdAtajoVista) return IntPtr.Zero;
        manejado = true;
        // La vista general se abre en la pantalla donde tengas el ratón.
        // Si estaba abierta en otra, esa se cierra y no se abre una segunda.
        var abierta = Islas.FirstOrDefault(i => i._panelAbierto == PanelVista);
        (abierta ?? IslaDelRaton()).AlternarVista();
        return IntPtr.Zero;
    }

    // ================= Botón "+N" =================
    UIElement CrearBotonVista()
    {
        _textoVista = new TextBlock
        {
            FontFamily = new FontFamily(FuenteIconos),
            FontSize = 14,
            Foreground = _texto,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _botonVista = Boton(_textoVista, "Ver todas las ventanas", AlternarVista);
        ActualizarBotonVista();
        return _botonVista;
    }

    // Glifo de cuadrícula, y "+N" si hay grupos que no caben en el carrusel
    void ActualizarBotonVista()
    {
        if (_textoVista == null || _botonVista == null) return;
        int ocultos = _tira == null ? 0 : Math.Max(0, _tira.Children.Count - MaxAppsEfectivo);
        // OJO con los glifos de este archivo: son caracteres de la zona de uso privado de
        // Unicode (aquí, U+E8A9, "GridView" de Segoe Fluent Icons). No se ven al leer el
        // código y cualquier herramienta que reescriba el archivo sin cuidado se los
        // come dejando una cadena vacía — que es exactamente lo que pasó en C4.1: el
        // botón seguía funcionando y no se veía nada. Si hay que tocarlos, compruébalos
        // con `od -c`, no a ojo.
        _textoVista.Text = ocultos > 0 ? GlifoCuadricula + $" +{ocultos}" : GlifoCuadricula;
        _textoVista.FontFamily = ocultos > 0
            ? new FontFamily($"{FuenteIconos}, {_cfg.Tema.Fuente}")
            : new FontFamily(FuenteIconos);
        _botonVista.ToolTip = $"Ver todas las ventanas ({GestorVentanas.Cuantas})";
    }

    // ================= Abrir y cerrar =================
    void AlternarVista()   // el icono de la cuadrícula
    {
        ApuntarBoton("cuadrícula");    // TEMPORAL: diagnóstico del flujo
        if (_panelAbierto == PanelVista) { CerrarVista(); return; }
        AbrirVista();
    }

    List<VentanaReal> VentanasDeLaVista()
    {
        var todas = GestorVentanas.Todas.AsEnumerable();
        if (_filtroApp != null)
            todas = todas.Where(v => v.App.Equals(_filtroApp, StringComparison.OrdinalIgnoreCase));
        return todas.Take(MaxEnVista).ToList();
    }

    // ================= La vista se CONSTRUYE una vez y se RECONCILIA =================
    //
    // ---------- De dónde venía el parpadeo ----------
    //
    // Antes, cada aviso de `GestorVentanas` llamaba a `RepintarVista`, y `RepintarVista`
    // hacía `Cajon.Content = CrearVista()`: tiraba el árbol visual entero y lo volvía a
    // construir. Todas las tarjetas nuevas, todos los `Image` nuevos, un `ScrollViewer`
    // nuevo —así que el desplazamiento volvía arriba— y el orden recalculado, que pone
    // **la activa primero**: enfocar otra aplicación desde fuera recolocaba la rejilla.
    //
    // Y el aviso llegaba sin parar, porque el gestor avisaba en cada refresco y el
    // refresco lo pedía cualquier evento de Windows, incluidos los de nuestras propias
    // ventanas. Eso está arreglado en `GestorVentanas` (ver allí); esto es la otra mitad:
    // aunque llegue un aviso, la vista **no se rehace**.
    //
    // La identidad de una tarjeta es su HWND. Lo que hay aquí es una reconciliación:
    // crear solo las que falten, quitar solo las que ya no existan, actualizar en su
    // sitio las que siguen, y no tocar nada más.
    sealed class TarjetaViva
    {
        public IntPtr Hwnd;
        public Button Boton = null!;
        public Border Marco = null!;
        public Grid Hueco = null!;
        public Image Foto = null!;
        public Image Respaldo = null!;
        public TextBlock Titulo = null!;
        public TextBlock Sub = null!;
        public TextBlock? Minimizada;
        public bool Activa;
        public string TextoTitulo = "";
        public string TextoSub = "";
        public EstadoVentana Estado;
        public DateTime UltimaFoto = DateTime.MinValue;
        public bool TieneFoto;
    }

    readonly Dictionary<IntPtr, TarjetaViva> _tarjetas = new();
    readonly List<IntPtr> _ordenVista = new();      // CONGELADO mientras la vista está abierta
    UniformGrid? _rejillaVista;
    ScrollViewer? _scrollVista;
    StackPanel? _columnaVista;
    FrameworkElement? _filaFiltro;
    string _appsDelFiltro = "";
    int _generacionVista;

    // Cada cuánto, como mucho, se vuelve a fotografiar una misma ventana mientras la
    // vista está abierta. Sin este tope, cualquier ráfaga de eventos pedía otra foto de
    // todas y cada entrega cambiaba un `ImageSource`, que es un parpadeo por tarjeta.
    // Cinco segundos y no tres: la caché de `Miniaturas` vale 4 s, así que por debajo de
    // eso una petición solo da un viaje a la caché para devolver la MISMA imagen.
    static readonly TimeSpan MinEntreFotos = TimeSpan.FromSeconds(5);

    void AbrirVista()
    {
        // La instantánea se pide fresca al abrir: los eventos de Windows la mantienen
        // al día, pero abrir es el momento en que más importa que lo esté.
        GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
        _filtroApp = null;
        _generacionVista++;

        int columnas = Columnas(VentanasDeLaVista().Count);
        // Con teclado: la vista se abre a propósito —por su botón o por su atajo— y
        // entonces Esc tiene que cerrarla y devolver el foco a donde estabas, que es lo
        // que hace `ActivarTeclado(false)` al cerrar el panel.
        AbrirPanel(PanelVista, ConstruirVista(), AnchoDeVista(columnas), AltoDeVista(columnas),
                   teclado: true, scrollPropio: true);
        EscucharCambios(true);
        PreviewKeyDown -= TeclaDeLaVista;
        PreviewKeyDown += TeclaDeLaVista;
        if (_superpuesta is { } sup) { sup.PreviewKeyDown -= TeclaDeLaVista; sup.PreviewKeyDown += TeclaDeLaVista; }
        Log.Info($"vista abierta gen={_generacionVista} tarjetas={_tarjetas.Count} columnas={columnas}");
    }

    static int Columnas(int cuantas) =>
        Math.Max(1, Math.Min(4, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, cuantas)))));

    static double AnchoDeVista(int columnas) => columnas * AnchoTarjeta + 28;

    // El alto, aparte y estático para poder comprobarlo: lo que falla no es el caso
    // normal, es el portátil de 768 px con veinte ventanas abiertas.
    internal static double AltoDeLaVista(int cuantas, int columnas, double altoPantalla)
    {
        if (cuantas == 0) return 46 + 150;
        int filas = (int)Math.Ceiling(cuantas / (double)Math.Max(1, columnas));
        double alto = 46 + filas * AltoTarjeta + 16;
        double tope = Math.Max(260, altoPantalla * 0.72);
        return Math.Min(alto, tope);
    }

    double AltoDeVista(int columnas) =>
        AltoDeLaVista(VentanasDeLaVista().Count, columnas, _monitor.Alto / Math.Max(1, _monitor.Escala));

    // Mientras la vista está abierta, se entera de que una ventana se fue o llegó.
    void EscucharCambios(bool si)
    {
        if (si == _vistaEscuchando) return;
        _vistaEscuchando = si;
        if (si) GestorVentanas.Cambio += AlCambiarLasVentanas;
        else GestorVentanas.Cambio -= AlCambiarLasVentanas;
    }

    // ---------- Qué hacer con un aviso ----------
    //
    // Aparte y pura para poder comprobarla: era una decisión metida en un manejador, que
    // es el patrón que ya produjo los cuatro fallos de C4.1 y los tres de C4.2.
    internal enum QueHacerConElAviso { Nada, SoloActualizar, TocarLaRejilla, Aplazar }

    internal static QueHacerConElAviso QueHacer(GestorVentanas.Cambios que, bool vistaAbierta,
                                                bool menuAbierto, bool arrastrandoTarjeta)
    {
        if (!vistaAbierta) return QueHacerConElAviso.Nada;
        // Con el menú abierto o una tarjeta cogida, no se toca nada: rehacer o recolocar
        // justo mientras lo estás usando destruye el gesto.
        if (menuAbierto || arrastrandoTarjeta) return QueHacerConElAviso.Aplazar;
        if (que == GestorVentanas.Cambios.Nada) return QueHacerConElAviso.Nada;
        // Lo ÚNICO que justifica tocar la rejilla es que la lista de ventanas cambie.
        // Activarse, cambiar de título, minimizarse o moverse se arregla actualizando la
        // tarjeta que ya existe, sin crear, sin quitar y sin recolocar.
        return (que & GestorVentanas.Cambios.Lista) != 0
            ? QueHacerConElAviso.TocarLaRejilla
            : QueHacerConElAviso.SoloActualizar;
    }

    void AlCambiarLasVentanas(GestorVentanas.Cambios que, GestorVentanas.Motivo motivo)
    {
        if (_panelAbierto != PanelVista) { EscucharCambios(false); return; }

        switch (QueHacer(que, true, MenuDeVistaAbierto, ArrastrandoTarjeta))
        {
            case QueHacerConElAviso.Nada:
                return;
            case QueHacerConElAviso.Aplazar:
                DiagVista.Aplazados++;
                Log.Info($"vista gen={_generacionVista} aviso APLAZADO motivo={motivo} cambios={que}" +
                         (MenuDeVistaAbierto ? " (menú abierto)" : "") +
                         (ArrastrandoTarjeta ? " (arrastrando una tarjeta)" : ""));
                return;
            default:
                ReconciliarVista(que, motivo);
                return;
        }
    }

    // ---------- El modo diagnóstico ----------
    //
    // Abre la vista general, la deja QUIETA los segundos que se pidan y escribe el
    // recuento. Nada de esto corre en el uso normal: es `--diagvista`.

    // ================= --diaginicio =================
    //
    // Mide lo que ha salido en pantalla de verdad. Ver DiagInicio.cs.

    // ================= --diagbandeja =================
    //
    // Vigila la bandeja y apunta cada cambio con su causa. Ver DiagBandeja.cs.
    async System.Threading.Tasks.Task CorrerDiagnosticoDeLaBandeja()
    {
        try
        {
            Log.Info($"=== diagnóstico de la bandeja: {DiagBandeja.Segundos} s ===");
            Log.Info($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");

            // Esperar a que la bandeja se haya registrado y las apps hayan respondido al
            // "TaskbarCreated": lo que se mide después de esto es la sesión en marcha.
            await System.Threading.Tasks.Task.Delay(9000);

            var antes = MuestraDeBandeja();
            long anadidos = 0, eliminados = 0, duplicados = 0, aperturas = 0, bailes = 0;
            Apunta($"bandeja: enumerados={antes.Count} (los que había al empezar a mirar)");
            Apunta($"bandeja: fantasmas={Bandeja.Fantasmas()}");
            foreach (var f in antes) Apunta($"  · {f}");

            for (int s = 1; s <= DiagBandeja.Segundos; s++)
            {
                await System.Threading.Tasks.Task.Delay(1000);
                var ahora = MuestraDeBandeja();
                var c = DiagBandeja.Comparar(antes, ahora);

                if (!c.Nada)
                {
                    anadidos += c.Anadidos.Count;
                    eliminados += c.Eliminados.Count;
                    duplicados += c.Duplicados.Count;
                    if (c.OrdenMovido) bailes++;

                    if (c.Anadidos.Count > 0) Apunta($"bandeja: añadidos={c.Anadidos.Count} → {string.Join(", ", c.Anadidos)}  [t+{s}s]");
                    if (c.Eliminados.Count > 0) Apunta($"bandeja: eliminados={c.Eliminados.Count} → {string.Join(", ", c.Eliminados)}  [t+{s}s]");
                    if (c.Duplicados.Count > 0) Apunta($"bandeja: duplicados={c.Duplicados.Count} → {string.Join(", ", c.Duplicados)}  [t+{s}s]");
                    if (c.OrdenMovido) Apunta($"bandeja: el orden se ha movido sin que entrara ni saliera nada  [t+{s}s]");
                    Apunta($"bandeja: enumerados={ahora.Count}  [t+{s}s]");
                    antes = ahora;
                }

                // Y cada 10 s se abre y se cierra el desplegable de iconos ocultos: es la
                // comprobación de "refresco por apertura" y de que abrirlo muchas veces no
                // cambia el orden ni duplica nada.
                if (s % 10 == 0)
                {
                    aperturas++;
                    var alAbrir = MuestraDeBandeja();
                    AlternarBandejaExtra();
                    await System.Threading.Tasks.Task.Delay(500);
                    int enPanel = _panelAbierto == PanelBandeja
                        ? (Cajon.Content as FrameworkElement) is { } raiz ? Radiografiar(raiz).Hijos : 0
                        : -1;
                    Apunta($"bandeja: fantasmas={Bandeja.Fantasmas()} (iconos cuya ventana ya no existe)");
                    Apunta($"bandeja: refresco por apertura nº {aperturas} — la lista se vuelve a enumerar al abrir " +
                           $"({alAbrir.Count} iconos; panel={(enPanel < 0 ? "no se abrió (no sobra ninguno)" : enPanel + " en la rejilla")})");
                    if (_panelAbierto == PanelBandeja) { CerrarPanel(); await System.Threading.Tasks.Task.Delay(400); }
                }
            }

            Apunta($"bandeja: RESUMEN — añadidos={anadidos} eliminados={eliminados} duplicados={duplicados} " +
                   $"aperturas={aperturas} fantasmas={Bandeja.Fantasmas()} órdenes movidos sin motivo={bailes}");
            var fin = MuestraDeBandeja();
            Apunta($"bandeja: enumerados={fin.Count} al terminar");
            foreach (var f in fin) Apunta($"  · {f}");
        }
        catch (Exception ex) { Log.Error("el diagnóstico de la bandeja", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    static void Apunta(string linea) { Log.Info(linea); Console.WriteLine(linea); }

    List<DiagBandeja.Ficha> MuestraDeBandeja() =>
        Bandeja.Iconos is { } ic
            ? ic.Select(DiagBandeja.FichaDe).ToList()
            : new List<DiagBandeja.Ficha>();
    async System.Threading.Tasks.Task CorrerDiagnosticoDeInicio()
    {
        try
        {
            Log.Info("=== diagnóstico de la superficie de Inicio ===");
            Log.Info($"ejecutable medido: {Environment.ProcessPath}");
            Log.Info($"pid: {Environment.ProcessId}");

            // 1) El panel de Inicio, por el mismo camino que la tecla Windows
            ApuntarBoton("--diaginicio (simula la tecla Windows)");
            AbrirMenuInicio();
            await System.Threading.Tasks.Task.Delay(1200);   // que el layout y la animación acaben
            MedirLoAbierto("inicio");

            // Si se pidieron segundos, Inicio se queda abierto: es lo que deja al
            // muestreador de multimedia hacer su trabajo (ver AbrirMenuInicio).
            if (DiagInicio.Segundos > 0)
            {
                Log.Info($"--diaginicio: Inicio se queda abierto {DiagInicio.Segundos} s");
                Console.WriteLine($"Inicio abierto {DiagInicio.Segundos} s; mirando la música...");
                for (int s = 5; s <= DiagInicio.Segundos; s += 5)
                {
                    await System.Threading.Tasks.Task.Delay(5000);
                    Console.WriteLine($"  {s} s: panel={_panelAbierto ?? "cerrado"}");
                }
                MedirLoAbierto("inicio (al final)");
            }

            // 2) Y el panel de audio, que SÍ es un panel aparte y tiene que seguir siéndolo
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(600);
            AbrirDesde(BotonAltavoz);
            await System.Threading.Tasks.Task.Delay(1000);
            MedirLoAbierto("audio");

            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(400);
        }
        catch (Exception ex) { Log.Error("el diagnóstico de Inicio", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    // ---------- La radiografía de un panel abierto ----------
    //
    // Lo que hace falta para distinguir "no se ha creado" de "se ha creado y no se ve":
    // el árbol visual de verdad, con las medidas, el scroll y la visibilidad de cada
    // capa. Un panel negro casi vacío puede ser cualquiera de cinco cosas distintas y a
    // ojo se parecen todas.
    internal sealed class RadiografiaPanel
    {
        public string Raiz = "";
        public int Hijos, Tarjetas, TarjetasVisibles, TarjetasConArea;
        public bool ShellEsRaiz, ShellOpaco;
        public double ShellAncho, ShellAlto;
        public string Visibilidad = "";
        public bool Recorta;
        public double Viewport, Extent, OffsetY, OffsetX, ExtentAncho, ViewportAncho;
        public bool HayScroll, BarraVisible, ScrollInterno;
        public string Cadena = "";
    }

    // Recorre el árbol desde la raíz del cajón y rellena la radiografía. Es puro sobre el
    // árbol: no toca nada, solo mira.
    internal static RadiografiaPanel Radiografiar(FrameworkElement? raiz)
    {
        var r = new RadiografiaPanel();
        if (raiz == null) { r.Raiz = "no hay contenido"; return r; }

        r.Raiz = raiz.GetType().Name;
        r.Visibilidad = raiz.Visibility.ToString();
        r.Recorta = raiz.ClipToBounds;

        // La cadena de capas, de fuera a dentro: es lo que delata un envoltorio de más
        var cadena = new System.Text.StringBuilder();
        object? capa = raiz;
        FrameworkElement? shell = null;
        for (int i = 0; i < 6 && capa != null; i++)
        {
            if (capa is FrameworkElement fe)
                cadena.Append($"{fe.GetType().Name}({fe.ActualWidth:0}x{fe.ActualHeight:0})");
            else cadena.Append(capa.GetType().Name);

            if (capa is ScrollViewer sv)
            {
                r.HayScroll = true;
                r.Viewport = sv.ViewportHeight;
                r.Extent = sv.ExtentHeight;
                r.OffsetY = sv.VerticalOffset;
                r.OffsetX = sv.HorizontalOffset;
                r.ViewportAncho = sv.ViewportWidth;
                r.ExtentAncho = sv.ExtentWidth;
                r.BarraVisible = sv.ComputedVerticalScrollBarVisibility == Visibility.Visible;
            }
            if (shell == null && capa is Border b && b.Background is SolidColorBrush s && s.Color.A == 255)
            {
                shell = b;
                r.ShellEsRaiz = ReferenceEquals(b, raiz);
                r.ShellOpaco = true;
                r.ShellAncho = b.ActualWidth;
                r.ShellAlto = b.ActualHeight;
            }

            capa = capa switch
            {
                ScrollViewer sv2 => sv2.Content,
                Border bo => bo.Child,
                _ => null,
            };
            if (capa != null) cadena.Append(" > ");
        }
        r.Cadena = cadena.ToString();

        // Las tarjetas: existen en el árbol, se ven, y ocupan área. Las tres cosas son
        // distintas y el fallo puede estar en cualquiera.
        foreach (var t in Descendientes(raiz).OfType<Border>())
        {
            if (t.Background is not SolidColorBrush sb || sb.Color.A != 255) continue;
            if (ReferenceEquals(t, shell)) continue;
            r.Tarjetas++;
            if (t.IsVisible) r.TarjetasVisibles++;
            if (t.ActualWidth > 1 && t.ActualHeight > 1) r.TarjetasConArea++;
        }


        // El scroll que SÍ debe existir: el que el propio panel lleva dentro. Se busca en
        // todo el árbol, no solo en la cadena de fuera, porque vive bajo la fila de los
        // resultados. Lo que importa de él es que su barra NO esté a la vista (la
        // superficie no lleva barras) y que el contenido no se quede desplazado fuera.
        if (!r.HayScroll)
        {
            foreach (var sv in Descendientes(raiz).OfType<ScrollViewer>())
            {
                if (sv.ViewportHeight <= 0) continue;
                r.HayScroll = true;
                r.ScrollInterno = true;
                r.Viewport = sv.ViewportHeight;
                r.Extent = sv.ExtentHeight;
                r.OffsetY = sv.VerticalOffset;
                r.OffsetX = sv.HorizontalOffset;
                r.ViewportAncho = sv.ViewportWidth;
                r.ExtentAncho = sv.ExtentWidth;
                r.BarraVisible = sv.ComputedVerticalScrollBarVisibility == Visibility.Visible;
                break;
            }
        }

        if (raiz is Panel p) r.Hijos = p.Children.Count;
        else if (shell is Border bs && bs.Child is Panel pp) r.Hijos = pp.Children.Count;
        else if (raiz is ContentControl cc && cc.Content != null) r.Hijos = 1;

        return r;
    }

    // Todo el árbol visual por debajo, sin recursión infinita y sin reventar si una rama
    // aún no se ha realizado.
    static IEnumerable<DependencyObject> Descendientes(DependencyObject raiz)
    {
        var pila = new Stack<DependencyObject>();
        pila.Push(raiz);
        int tope = 0;
        while (pila.Count > 0 && tope++ < 20000)
        {
            var d = pila.Pop();
            yield return d;
            int n = 0;
            try { n = VisualTreeHelper.GetChildrenCount(d); } catch { }
            for (int i = 0; i < n; i++)
            {
                DependencyObject? h = null;
                try { h = VisualTreeHelper.GetChild(d, i); } catch { }
                if (h != null) pila.Push(h);
            }
        }
    }

    void MedirLoAbierto(string esperado)
    {
        var raiz = PanelSuperpuestoAbierto && _superpuesta is { } v
            ? v.Cajon.Content as FrameworkElement
            : Cajon.Content as FrameworkElement;
        var r = Radiografiar(raiz);

        // ¿Dos paneles encima? El cajón de la isla y la ventana superpuesta no pueden
        // tener contenido a la vez: si lo tienen, se está mirando el que no es.
        bool dos = Cajon.Content != null && _superpuesta is { Cajon.Content: not null };

        var lineas = new[]
        {
            $"--- medido: {esperado} ---",
            $"panel solicitado: {esperado}",
            $"panel abierto: {_panelAbierto ?? "ninguno"}",
            $"botón de origen: {_ultimoBoton}",
            $"ancla: {(_tarjetaAncla == null ? "NINGUNA" : _tarjetaAncla.GetType().Name + " \"" + _tarjetaAncla.ToolTip + "\"")}",
            $"ventana: {ActualWidth:0}x{ActualHeight:0}  isla: {Isla.ActualWidth:0}x{Isla.ActualHeight:0}",
            $"ancho pedido: {_panelAncho:0}  alto pedido: {_panelAlto:0}",
            $"tipo de raíz visual: {r.Raiz}",
            $"cadena de capas: {r.Cadena}",
            $"PanelShell: {(r.ShellOpaco ? $"{r.ShellAncho:0}x{r.ShellAlto:0}" : "NO HAY")}" +
                $"  es la raíz: {(r.ShellEsRaiz ? "sí" : "NO")}  opaco: {(r.ShellOpaco ? "sí" : "NO")}",
            $"hijos: {r.Hijos}  tarjetas: {r.Tarjetas} (visibles {r.TarjetasVisibles}, con área {r.TarjetasConArea})",
            $"Visibility: {r.Visibilidad}  ClipToBounds: {r.Recorta}",
            r.HayScroll
                ? $"ScrollViewer ({(r.ScrollInterno ? "interno, el del propio panel" : "ENVOLTORIO DE FUERA")}): " +
                  $"viewport {r.ViewportAncho:0}x{r.Viewport:0}  extent {r.ExtentAncho:0}x{r.Extent:0}  " +
                  $"offset ({r.OffsetX:0},{r.OffsetY:0})  barra visible: {(r.BarraVisible ? "SÍ" : "no")}"
                : "ScrollViewer: no hay",
            $"recorte del cajón: {(Isla.Clip == null ? "NO (de punta a punta)" : "sí")}",
            $"dos paneles a la vez: {(dos ? "SÍ — se está mirando el que no es" : "no")}",
        };
        foreach (var l in lineas) { Log.Info(l); Console.WriteLine(l); }

        // Y el veredicto, por si el que lee son unos ojos cansados
        foreach (var mal in Veredicto(r))
        {
            Log.Info("MAL: " + mal);
            Console.WriteLine("MAL: " + mal);
        }
    }

    // Las cinco cosas que NO pueden pasar. Está aparte y es pura para poder comprobarla
    // sin abrir una ventana: ver las pruebas de integración.
    internal static IEnumerable<string> Veredicto(RadiografiaPanel r)
    {
        if (r.Raiz == "no hay contenido") { yield return "el panel no tiene contenido"; yield break; }
        if (!r.ShellOpaco) yield return "PanelShell no está o no es opaco: se verá el escritorio por detrás";
        if (r.Tarjetas == 0) yield return "PanelShell tiene 0 tarjetas: el panel sale negro y vacío";
        if (r.Tarjetas > 0 && r.TarjetasVisibles == 0)
            yield return $"hay {r.Tarjetas} tarjetas en el árbol pero NINGUNA visible";
        if (r.Tarjetas > 0 && r.TarjetasConArea == 0)
            yield return $"hay {r.Tarjetas} tarjetas visibles pero todas miden 0: algo las ha aplastado";
        if (r.HayScroll && r.BarraVisible)
            yield return "hay una barra de desplazamiento VISIBLE: es la franja que se ve en el panel";
        if (r.HayScroll && !r.ScrollInterno)
            yield return "el scroll es un envoltorio de fuera: PanelShell deja de ser la raíz y la " +
                         "fila Star del panel se queda sin acotar";
        if (r.HayScroll && r.Viewport > 0 && r.OffsetY > r.Extent - r.Viewport + 1)
            yield return $"el contenido está desplazado fuera del viewport (offset {r.OffsetY:0} de {r.Extent - r.Viewport:0})";
        if (r.HayScroll && r.Viewport is > 0 and < 60)
            yield return $"el viewport mide {r.Viewport:0} px: no cabe nada dentro";
        if (r.ShellOpaco && r.ShellAlto < 60)
            yield return $"PanelShell mide {r.ShellAlto:0} px de alto: se ha quedado aplastado";
    }
    async System.Threading.Tasks.Task CorrerDiagnosticoDeLaVista()
    {
        try
        {
            Log.Info($"=== diagnóstico de la vista: {DiagVista.Segundos} s ===");
            DiagVista.Reiniciar();
            AbrirVista();
            // Un momento para que las primeras miniaturas lleguen: lo que se quiere medir
            // es la vista en reposo, no su apertura.
            await System.Threading.Tasks.Task.Delay(2500);
            DiagVista.Reiniciar();

            int tarjetas = _tarjetas.Count;
            await System.Threading.Tasks.Task.Delay(DiagVista.Segundos * 1000);

            var informe = DiagVista.Informe(DiagVista.Segundos, tarjetas);
            Console.WriteLine(informe);
            foreach (var linea in informe.Split('\n')) Log.Info("diag: " + linea.TrimEnd());
            CerrarVista();
            await System.Threading.Tasks.Task.Delay(300);
        }
        catch (Exception ex) { Log.Error("el diagnóstico de la vista", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    // Solo el dueño la apaga. Quien no la tiene, no la toca: era el camino por el que
    // cerrar la vista en mitad del arrastre de una ventana real se comía el ajuste.

    void CerrarVista()
    {
        EscucharCambios(false);
        CerrarMenuDeVista();
        PararArrastreDeTarjeta();
        PreviewKeyDown -= TeclaDeLaVista;
        if (_superpuesta is { } sup) sup.PreviewKeyDown -= TeclaDeLaVista;
        Miniaturas.Limpiar(GestorVentanas.Todas.Select(v => v.Hwnd).ToHashSet());
        _tarjetas.Clear();
        _ordenVista.Clear();
        _rejillaVista = null;
        _scrollVista = null;
        _columnaVista = null;
        _filaFiltro = null;
        _appsDelFiltro = "";
        if (_panelAbierto == PanelVista) CerrarPanel();
    }

    // ================= Las decisiones, aparte y comprobables =================
    //
    // Los cuatro fallos de C4.1 eran de decisión, no de dibujo: quién se arrastra, qué
    // cierra Esc, qué pasa al soltar y en qué orden se ajusta una ventana. Ninguno se
    // podía comprobar sin abrir ventanas, así que ninguno tenía prueba. Ahora sí.

    // El glifo de la cuadrícula, por su número y no como carácter suelto.
    //
    // En C4.1 estaba escrito como carácter literal de la zona de uso privado de Unicode,
    // se perdió al reescribir el archivo y el botón se quedó sin icono — funcionando,
    // pero invisible. Construido desde el número no se puede perder, y además se puede
    // comprobar que sigue siendo el de siempre.
    internal const int CodigoCuadricula = 0xE8A9;      // "GridView" de Segoe Fluent Icons
    internal static readonly string GlifoCuadricula = char.ConvertFromUtf32(CodigoCuadricula);

    internal enum QueCierra { Nada, Menu, Vista }

    // Esc va por capas: primero lo último que abriste. Si cerrara siempre la vista,
    // abrir un menú sin querer costaría tener que volver a abrirlo todo.
    internal static QueCierra EscCierra(bool vistaAbierta, bool menuAbierto) =>
        !vistaAbierta ? QueCierra.Nada
        : menuAbierto ? QueCierra.Menu
        : QueCierra.Vista;

    internal enum QuienArrastra { Nadie, Isla, Tarjeta }

    // Con la vista abierta el gesto es suyo: lo que se arrastra es una ventana del
    // escritorio, no la nube. En C4.1 faltaba esta regla y coger una tarjeta movía la
    // isla entera por la pantalla.
    internal static QuienArrastra ArrastraAlPulsar(bool vistaAbierta, bool enBotonDeLaTarjeta,
                                                   bool menuAbierto, bool islaArrastrable)
    {
        if (vistaAbierta)
            return menuAbierto || enBotonDeLaTarjeta ? QuienArrastra.Nadie : QuienArrastra.Tarjeta;
        return islaArrastrable ? QuienArrastra.Isla : QuienArrastra.Nadie;
    }

    // Soltar fuera de una zona no cambia nada, y un clic sin mover tampoco: eso es
    // pulsar la tarjeta, que es otra cosa.
    internal static bool AjustaAlSoltar(bool seMovio, Zona z) => seMovio && z != Zona.Ninguna;

    // ================= Arrastrar una tarjeta =================
    //
    // Coger una tarjeta y soltarla en un borde ajusta ESA ventana, sin tener que ir a
    // buscarla por la pantalla. Es el mismo gesto que arrastrar la ventana de verdad,
    // pero desde la cuadrícula.
    //
    // ---------- Por qué el ratón se sondea y no se escucha ----------
    //
    // Es el patrón del proyecto desde B2 (ver MainWindow.Arrastre.cs): `MouseMove` deja
    // de llegar en cuanto el puntero sale del control, y capturar el ratón rompe los
    // botones de dentro. Así que una vez por fotograma —`CompositionTarget.Rendering`—
    // se pregunta dónde está el puntero y si el botón sigue pulsado
    // (`Nativo.BotonIzquierdoPulsado`), que funciona esté donde esté.
    //
    // No se depende de `MouseLeftButtonUp`: si sueltas fuera del panel, ese evento no
    // llega nunca y el arrastre se quedaría colgado con la previsualización puesta.

    IntPtr _tarjetaArrastrada;
    EventHandler? _siguiendoTarjeta;
    Point _raizTarjeta;
    bool _tarjetaSeMovio;

    public bool ArrastrandoTarjeta => _tarjetaArrastrada != IntPtr.Zero;

    // Cuánto hay que separarse antes de que un clic se convierta en un arrastre. Por
    // debajo de esto sigue siendo un clic y la tarjeta se activa como siempre.
    const double UmbralTarjeta = 6;

    void EmpezarArrastreDeTarjeta(IntPtr h, MouseButtonEventArgs e)
    {
        // Los botones de la tarjeta (el aspa) y la capa del menú tienen lo suyo: un clic
        // ahí no es coger la ventana.
        bool enBoton = e.OriginalSource is DependencyObject d && EnUnBotonDeLaTarjeta(d);
        var quien = ArrastraAlPulsar(vistaAbierta: true, enBoton, MenuDeVistaAbierto,
                                     _cfg.Barra.Arrastrable);
        if (quien != QuienArrastra.Tarjeta)
        {
            Log.Info($"arrastre tarjeta IGNORADO hwnd=0x{h.ToInt64():X} quien={quien}" +
                     (enBoton ? " (el clic es de un botón de la tarjeta)" : "") +
                     (MenuDeVistaAbierto ? " (el menú está abierto)" : ""));
            return;
        }
        if (!Nativo.Existe(h)) return;

        // El gesto es de la tarjeta y queda dicho: la isla mira esto antes de empezar el
        // suyo. No vale con `e.Handled`, porque el manejador de la isla va en un
        // PreviewMouseLeftButtonDown de un ANTEPASADO y ya se ha ejecutado cuando
        // llegamos aquí — los eventos Preview van de la raíz a la hoja.
        _tarjetaArrastrada = h;
        _tarjetaSeMovio = false;
        _raizTarjeta = PuntoRaton();

        _siguiendoTarjeta = (_, _) => SeguirTarjeta();
        CompositionTarget.Rendering += _siguiendoTarjeta;
        DiagVista.ArrastresTarjeta++;
        Log.Info($"arrastre tarjeta INICIADO hwnd=0x{h.ToInt64():X} \"{Nativo.Titulo(h)}\"");
    }

    // Sale este clic de la vista general? Se sube por el árbol visual buscando su raíz.
    //
    // Hace falta porque el arrastre de la isla se engancha en un
    // `PreviewMouseLeftButtonDown` de un ANTEPASADO: los eventos Preview van de la raíz a
    // la hoja, así que la isla decide antes de que la tarjeta pueda decir nada, y poner
    // `e.Handled` en la tarjeta llega tarde por definición. Mirar el origen sí llega.
    internal bool EnLaVista(object? origen)
    {
        if (_raizVista == null || origen is not DependencyObject d) return false;
        for (var p = d; p != null; p = VisualTreeHelper.GetParent(p))
            if (ReferenceEquals(p, _raizVista)) return true;
        return false;
    }

    // El aspa de la tarjeta es un Button; el cuerpo también lo es. Lo que distingue
    // "he pulsado el aspa" de "he pulsado la tarjeta" es que el aspa está DENTRO: dos
    // botones por encima en vez de uno.
    static bool EnUnBotonDeLaTarjeta(DependencyObject d)
    {
        int botones = 0;
        for (var p = d; p != null; p = VisualTreeHelper.GetParent(p))
            if (p is Button) botones++;
        return botones > 1;
    }

    void SeguirTarjeta()
    {
        // Si la vista se ha cerrado por el camino —un clic fuera, otro panel— el arrastre
        // se acaba aquí: quedarse girando dejaría la previsualización puesta.
        if (_tarjetaArrastrada == IntPtr.Zero || _panelAbierto != PanelVista)
        { PararArrastreDeTarjeta(); return; }

        if (!Nativo.BotonIzquierdoPulsado())
        {
            var zona = _zonaPrevia;
            var monitor = _monitorPrevio;
            var ventana = _tarjetaArrastrada;
            bool hubo = _tarjetaSeMovio;
            PararArrastreDeTarjeta();

            // Soltada fuera de una zona: no se toca nada, que es lo que se espera de
            // "me lo he pensado mejor".
            if (!AjustaAlSoltar(hubo, zona))
            {
                Log.Info($"arrastre tarjeta TERMINADO hwnd=0x{ventana.ToInt64():X} " +
                         (hubo ? "sin zona: no se toca nada" : "sin moverse: era un clic"));
                return;
            }
            ApuntarArrastre();          // que el clic de soltar no active la tarjeta
            Log.Info($"arrastre tarjeta TERMINADO hwnd=0x{ventana.ToInt64():X} " +
                     $"destino={ZonasAjuste.Nombre(zona)} pantalla={NombreDeMonitor(monitor)}");
            // El monitor explícito, por lo mismo que en el arrastre de una ventana real:
            // lo prometido se calculó con el monitor DEL PUNTERO y es ahí donde tiene que
            // caer, no en el que deduzca `MonitorFromWindow`.
            Ajustar(ventana, zona, monitor);
            return;
        }

        var p = PuntoRaton();
        if (!_tarjetaSeMovio)
        {
            if (Math.Abs(p.X - _raizTarjeta.X) < UmbralTarjeta &&
                Math.Abs(p.Y - _raizTarjeta.Y) < UmbralTarjeta) return;
            _tarjetaSeMovio = true;
        }

        var m = Monitores.DelRaton();
        if (m == null) { Poner(DuenoPrevia.Tarjeta, Zona.Ninguna, null); return; }
        var r = Monitores.Raton();
        Poner(DuenoPrevia.Tarjeta, ZonasAjuste.ZonaDelPunto(r.X, r.Y, m), m);
    }

    void PararArrastreDeTarjeta()
    {
        if (_siguiendoTarjeta != null)
        {
            CompositionTarget.Rendering -= _siguiendoTarjeta;
            _siguiendoTarjeta = null;
        }
        _tarjetaArrastrada = IntPtr.Zero;
        _tarjetaSeMovio = false;
        // Solo se apaga la previsualización si era NUESTRA. Si en ese momento la tiene el
        // arrastre de una ventana real, apagarla aquí le borraba la zona y al soltar no
        // ajustaba nada.
        QuitarPrevia(DuenoPrevia.Tarjeta);
    }

    // ---------- Esc, con prioridad ----------
    //
    // Primero el menú y después la vista. Si Esc cerrara siempre la vista, abrir un
    // menú sin querer costaría tener que volver a abrirlo todo. Va en `PreviewKeyDown`
    // para llegar antes que cualquier otro que mire la tecla.
    void TeclaDeLaVista(object remitente, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        switch (EscCierra(_panelAbierto == PanelVista, MenuDeVistaAbierto))
        {
            case QueCierra.Menu: e.Handled = true; CerrarMenuDeVista(); break;
            case QueCierra.Vista: e.Handled = true; CerrarVista(); break;
        }
    }

    // ================= Pintado =================

    // La vista es una pila con la rejilla y, ENCIMA, una capa donde sale el menú de una
    // tarjeta. Es la misma solución que el menú de aplicaciones del buscador, y por el
    // mismo motivo: los paneles de la isla son de uno en uno, así que abrir el menú como
    // panel cerraría la vista. Montado como capa, la rejilla se queda detrás, visible y
    // en su sitio.
    // ---------- Construir, una vez ----------
    //
    // La vista es una pila con la rejilla y, ENCIMA, una capa donde sale el menú de una
    // tarjeta. Es la misma solución que el menú de aplicaciones del buscador, y por el
    // mismo motivo: los paneles de la isla son de uno en uno, así que abrir el menú como
    // panel cerraría la vista. Montado como capa, la rejilla se queda detrás, visible y
    // en su sitio.
    //
    // Esto se llama al ABRIR y nada más. Mientras la vista está abierta lo que corre es
    // `ReconciliarVista`.
    FrameworkElement ConstruirVista()
    {
        _tarjetas.Clear();
        _ordenVista.Clear();

        _columnaVista = new StackPanel { Margin = new Thickness(12, 2, 12, 10) };
        _filaFiltro = FiltroDeApps();
        _columnaVista.Children.Add(_filaFiltro);

        var ventanas = VentanasDeLaVista();
        int columnas = Columnas(ventanas.Count);
        _rejillaVista = new UniformGrid { Columns = columnas, Background = Brushes.Transparent };

        var activa = GestorVentanas.Activa;
        foreach (var v in ventanas)
        {
            var t = CrearTarjeta(v, v.Hwnd == activa);
            _tarjetas[v.Hwnd] = t;
            _ordenVista.Add(v.Hwnd);
            _rejillaVista.Children.Add(t.Boton);
        }

        _scrollVista = new ScrollViewer
        {
            Content = _rejillaVista,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(140, AltoDeVista(columnas) - 60),
            Margin = new Thickness(0, 4, -8, 0),
            Padding = new Thickness(0, 0, 8, 0)
        };

        _vacioVista = VistaVacia();
        _vacioVista.Visibility = ventanas.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        _scrollVista.Visibility = ventanas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _columnaVista.Children.Add(_scrollVista);
        _columnaVista.Children.Add(_vacioVista);
        _appsDelFiltro = HuellaDeLasApps();

        _raizVista = new Grid();
        _raizVista.Children.Add(_columnaVista);
        _raizVista.Children.Add(CrearCapaMenuVista());
        return _raizVista;
    }

    FrameworkElement? _vacioVista;

    // ---------- Reconciliar ----------
    //
    // Crear solo las nuevas, quitar solo las muertas, actualizar en su sitio las que
    // siguen. El orden se CONGELA al abrir: las que se van dejan su hueco y las que
    // llegan se ponen al final. Si se siguiera el orden de la instantánea —que pone la
    // activa primero— cambiar de aplicación desde fuera recolocaría toda la rejilla, que
    // es exactamente lo que se veía como "las tarjetas se mueven solas".
    void ReconciliarVista(GestorVentanas.Cambios que, GestorVentanas.Motivo motivo)
    {
        if (_rejillaVista == null || _columnaVista == null)
        {
            // No debería pasar nunca: significa que alguien tiró el árbol visual por
            // detrás. Se reconstruye para no quedarse con una vista muerta, pero queda
            // anotado como lo que es.
            Log.Error("vista: ANOMALIA, la rejilla no existe y hay que reconstruirla entera",
                      new InvalidOperationException($"motivo={motivo} cambios={que}"));
            RehacerVistaAPeticion("la rejilla no existía (anomalía)");
            return;
        }

        var ventanas = VentanasDeLaVista();
        var porHwnd = new Dictionary<IntPtr, VentanaReal>();
        foreach (var v in ventanas) porHwnd[v.Hwnd] = v;

        int antes = _tarjetas.Count;
        int filasAntes = Filas(antes, _rejillaVista.Columns);
        int actualizadas = 0;

        // El orden nuevo sale de la regla pura, que es la única autoridad: el congelado,
        // menos las que ya no existen, más las nuevas al final.
        var vivas = ventanas.Select(v => v.Hwnd).ToList();
        var nuevoOrden = OrdenReconciliado(_ordenVista, vivas);
        var quitadas = _ordenVista.Where(h => !porHwnd.ContainsKey(h)).ToList();
        var creadas = nuevoOrden.Where(h => !_tarjetas.ContainsKey(h)).ToList();

        // ----- 1. Las que ya no existen -----
        foreach (var h in quitadas)
        {
            if (_tarjetas.TryGetValue(h, out var muerta))
            {
                _rejillaVista.Children.Remove(muerta.Boton);
                _tarjetas.Remove(h);
            }
        }

        // ----- 2. Las nuevas, al final -----
        //
        // Quitar primero y añadir después al final deja los hijos de la UniformGrid
        // exactamente en `nuevoOrden`: nada se inserta en medio, así que ninguna tarjeta
        // que ya estaba cambia de hueco.
        var activa = GestorVentanas.Activa;
        foreach (var h in creadas)
        {
            if (!porHwnd.TryGetValue(h, out var v)) continue;
            var t = CrearTarjeta(v, v.Hwnd == activa);
            _tarjetas[v.Hwnd] = t;
            _rejillaVista.Children.Add(t.Boton);
        }

        _ordenVista.Clear();
        _ordenVista.AddRange(nuevoOrden);

        // ----- 3. Las que siguen: se actualizan EN SU SITIO -----
        foreach (var h in _ordenVista)
        {
            if (!_tarjetas.TryGetValue(h, out var t) || !porHwnd.TryGetValue(h, out var v)) continue;
            if (creadas.Contains(h)) continue;
            if (ActualizarTarjeta(t, v, v.Hwnd == activa, motivo)) actualizadas++;
        }

        // ----- 4. El hueco de "no hay nada", las columnas y el filtro -----
        bool tocoLaRejilla = creadas.Count > 0 || quitadas.Count > 0;
        int columnas = _rejillaVista.Columns;
        if (tocoLaRejilla)
        {
            columnas = Columnas(_ordenVista.Count);
            if (columnas != _rejillaVista.Columns) _rejillaVista.Columns = columnas;
            if (_vacioVista != null)
                _vacioVista.Visibility = _ordenVista.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            if (_scrollVista != null)
                _scrollVista.Visibility = _ordenVista.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RefrescarFiltroSiCambiaron();
        }

        // ----- 5. Solo se remide el panel si cambió el número de FILAS -----
        //
        // `ReajustarPanel` mide el contenido y, si el alto cambia, mueve y redimensiona
        // la ventana de NubeBar. Llamarlo cuando no ha cambiado nada es pedirle al shell
        // un SetWindowPos por nada, y con la isla de por medio eso se ve.
        DiagVista.Reconciliaciones++;
        DiagVista.Creadas += creadas.Count;
        DiagVista.Actualizadas += actualizadas;
        DiagVista.Eliminadas += quitadas.Count;
        int filasDespues = Filas(_ordenVista.Count, columnas);
        bool remide = filasDespues != filasAntes;
        if (remide) { DiagVista.PanelRemedido++; ReajustarPanel(PanelVista); }

        Log.Info($"vista gen={_generacionVista} motivo={motivo} cambios={que} " +
                 $"tarjetas {antes}->{_tarjetas.Count} creadas={creadas.Count} " +
                 $"actualizadas={actualizadas} eliminadas={quitadas.Count} " +
                 $"orden={(tocoLaRejilla ? "crece o encoge por los extremos" : "igual")} " +
                 $"rejilla={(tocoLaRejilla ? "tocada" : "intacta")} reconstruida=no " +
                 $"filas {filasAntes}->{filasDespues}{(remide ? " (remide el panel)" : "")}");

        foreach (var h in creadas)
            Log.Info($"vista gen={_generacionVista} tarjeta CREADA hwnd=0x{h.ToInt64():X}");
        foreach (var h in quitadas)
            Log.Info($"vista gen={_generacionVista} tarjeta ELIMINADA hwnd=0x{h.ToInt64():X}");
    }

    internal static int Filas(int cuantas, int columnas) =>
        (int)Math.Ceiling(Math.Max(0, cuantas) / (double)Math.Max(1, columnas));

    // ---------- El orden de la vista, como regla pura ----------
    //
    // El congelado, menos las que ya no existen, más las nuevas AL FINAL. Es la regla que
    // impide que las tarjetas se recoloquen solas: la instantánea de `GestorVentanas`
    // pone la ACTIVA primero, así que seguir su orden mientras la vista está abierta hace
    // que enfocar otra aplicación desde fuera reordene la rejilla entera.
    //
    // `vivas` viene en el orden de la instantánea, que es estable, así que lo que se
    // añade se añade en un orden determinado y no al capricho de un diccionario.
    internal static List<IntPtr> OrdenReconciliado(IEnumerable<IntPtr> congelado,
                                                   IEnumerable<IntPtr> vivas)
    {
        var quedan = new HashSet<IntPtr>(vivas);
        var r = new List<IntPtr>();
        foreach (var h in congelado) if (quedan.Contains(h)) r.Add(h);
        var ya = new HashSet<IntPtr>(r);
        foreach (var h in vivas) if (ya.Add(h)) r.Add(h);
        return r;
    }

    string HuellaDeLasApps() =>
        string.Join("|", GestorVentanas.Aplicaciones()) + "#" + GestorVentanas.Cuantas;

    // El filtro solo se vuelve a montar si cambió el conjunto de aplicaciones. Es una
    // fila de pastillas y no la rejilla, pero rehacerla en cada aviso también se ve.
    void RefrescarFiltroSiCambiaron()
    {
        if (_columnaVista == null || _filaFiltro == null) return;
        var apps = HuellaDeLasApps();
        if (apps == _appsDelFiltro) return;
        _appsDelFiltro = apps;
        int i = _columnaVista.Children.IndexOf(_filaFiltro);
        if (i < 0) return;
        var nueva = FiltroDeApps();
        _columnaVista.Children.RemoveAt(i);
        _columnaVista.Children.Insert(i, nueva);
        _filaFiltro = nueva;
    }

    // Vuelve a montar la vista entera A PROPÓSITO: lo usa el filtro por aplicación, que
    // es una elección del usuario y cambia qué ventanas se enseñan. No es una anomalía;
    // lo que no puede pasar es que esto lo dispare un evento de Windows.
    void RehacerVistaAPeticion(string porque)
    {
        if (_panelAbierto != PanelVista) return;
        DiagVista.Reconstrucciones++;
        Log.Info($"vista gen={_generacionVista} reconstruida A PETICION: {porque}");
        var contenido = ConstruirVista();
        if (PanelSuperpuestoAbierto && _superpuesta is { } v) v.Cajon.Content = contenido;
        else Cajon.Content = contenido;
        ReajustarPanel(PanelVista);
    }

    // ---------- La capa del menú ----------

    Grid? _raizVista;            // para medir y colocar la tarjeta del menú
    Grid? _capaMenuVista;        // transparente pero NO invisible al ratón: come el clic de fuera
    Canvas? _lienzoMenuVista;

    public bool MenuDeVistaAbierto => _capaMenuVista?.Visibility == Visibility.Visible;

    UIElement CrearCapaMenuVista()
    {
        _lienzoMenuVista = new Canvas();
        _capaMenuVista = new Grid
        {
            Visibility = Visibility.Collapsed,
            // Un Background nulo dejaría pasar los clics a las tarjetas de debajo y el
            // menú no se cerraría al pulsar al lado.
            Background = Brushes.Transparent,
            Children = { _lienzoMenuVista }
        };
        _capaMenuVista.MouseLeftButtonDown += (_, e) => { e.Handled = true; CerrarMenuDeVista(); };
        _capaMenuVista.MouseRightButtonUp += (_, e) => { e.Handled = true; CerrarMenuDeVista(); };
        return _capaMenuVista;
    }

    void CerrarMenuDeVista()
    {
        if (_capaMenuVista == null) return;
        _capaMenuVista.Visibility = Visibility.Collapsed;
        _lienzoMenuVista?.Children.Clear();
    }

    // El filtro por aplicación: una pastilla por app, y "Todas" delante.
    FrameworkElement FiltroDeApps()
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };

        Button Pastilla(string texto, bool elegida, Action accion)
        {
            var t = new TextBlock
            {
                Text = texto, FontSize = 11,
                Foreground = elegida ? _acentoLegible : _texto,
                FontWeight = elegida ? FontWeights.SemiBold : FontWeights.Normal
            };
            var b = Boton(t, elegida ? $"{texto} (puesto)" : $"Ver solo {texto}", accion);
            b.Height = 24;
            return b;
        }

        var apps = GestorVentanas.Aplicaciones().ToList();
        fila.Children.Add(Pastilla($"Todas · {GestorVentanas.Cuantas}", _filtroApp == null,
                                   () => { _filtroApp = null; RehacerVistaAPeticion("el filtro vuelve a Todas"); }));
        // Con una sola aplicación abierta el filtro no filtra nada: no se enseña.
        if (apps.Count > 1)
            foreach (var a in apps.Take(6))
            {
                var app = a;
                fila.Children.Add(Pastilla(app, string.Equals(_filtroApp, app, StringComparison.OrdinalIgnoreCase),
                                           () => { _filtroApp = app; RehacerVistaAPeticion("filtro por " + app); }));
            }
        return fila;
    }

    FrameworkElement VistaVacia()
    {
        var caja = new StackPanel
        {
            Margin = new Thickness(0, 26, 0, 22),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var g = Glifo(GlifoCuadricula);
        g.FontSize = 30;
        g.Foreground = _textoAtenuado;
        g.Opacity = 0.7;
        g.HorizontalAlignment = HorizontalAlignment.Center;
        caja.Children.Add(g);
        caja.Children.Add(new TextBlock
        {
            Text = _filtroApp != null ? $"{_filtroApp} no tiene ventanas abiertas"
                                      : "No hay ventanas abiertas",
            Foreground = _texto, FontSize = 13,
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        caja.Children.Add(new TextBlock
        {
            Text = _filtroApp != null ? "Quita el filtro para ver las demás"
                                      : "Abre una aplicación y aparecerá aquí",
            Foreground = _textoAtenuado, FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        return caja;
    }

    // ---------- Una tarjeta ----------

    // ---------- Una tarjeta ----------
    //
    // Se crea UNA vez por HWND y vive hasta que la ventana desaparece. Lo que cambia
    // después —el título, el subtítulo, el estado, si es la activa— se escribe encima de
    // los mismos elementos. Nada de esto crea objetos nuevos ni cambia el tamaño de la
    // tarjeta, así que nada se mueve.

    // El aspa, por su número y no como carácter suelto, por el mismo motivo que el
    // glifo de la cuadrícula: escrito como carácter literal de la zona de uso privado se
    // pierde al reescribir el archivo y el botón se queda funcionando pero invisible.
    internal const int CodigoAspa = 0xE711;            // "Cancel" de Segoe Fluent Icons
    internal static readonly string GlifoAspa = char.ConvertFromUtf32(CodigoAspa);

    // Si toca volver a fotografiar una ventana. Aparte y pura: era una decisión metida en
    // el constructor de la tarjeta, y de ahí venía media parte del parpadeo.
    internal static bool TocaFoto(bool nueva, bool minimizada, TimeSpan desdeLaUltima,
                                  GestorVentanas.Motivo motivo, TimeSpan minimo)
    {
        // Una minimizada no tiene nada que dibujar: PrintWindow devuelve un rectángulo
        // vacío, y pintarlo sería sustituir la foto buena por una en blanco.
        if (minimizada) return false;
        // Una tarjeta recién creada siempre pide la suya.
        if (nueva) return true;
        // Y NUNCA se fotografía todo por una activación: cambiar de ventana solo cambia
        // el indicador. Sin esto, cada Alt+Tab disparaba una captura por tarjeta.
        if (motivo == GestorVentanas.Motivo.PrimerPlano) return false;
        return desdeLaUltima >= minimo;
    }

    TarjetaViva CrearTarjeta(VentanaReal v, bool esActiva)
    {
        var h = v.Hwnd;
        var t = new TarjetaViva { Hwnd = h, Activa = esActiva, Estado = v.Estado };

        // El filo va SIEMPRE puesto y lo que cambia es su color. Con el grosor a 0 y a
        // 1,4 según el foco, marcar la activa movía el contenido de la tarjeta un pelo,
        // y eso con el foco cambiando solo se lee como que las tarjetas tiemblan.
        t.Marco = new Border
        {
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(5),
            Background = esActiva ? _elegido : _superficie,
            BorderThickness = new Thickness(1.4),
            BorderBrush = esActiva ? _acento : Brushes.Transparent,
            ClipToBounds = true
        };

        var dentro = new Grid();
        dentro.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dentro.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AltoPie) });

        // ----- La foto, o el icono si no hay -----
        t.Hueco = new Grid { Background = _superficieAlta, ClipToBounds = true };
        var icono = v.Icono ?? Nativo.Icono(h);
        t.Respaldo = new Image
        {
            Source = icono, Width = 34, Height = 34, Opacity = 0.85,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        t.Hueco.Children.Add(t.Respaldo);

        t.Foto = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed
        };
        t.Hueco.Children.Add(t.Foto);

        // Una minimizada no se puede fotografiar: no tiene nada que dibujar. Se dice,
        // en vez de dejar un hueco gris que parece un fallo.
        t.Minimizada = new TextBlock
        {
            Text = "Minimizada", Foreground = _textoAtenuado, FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 6),
            Visibility = v.Estado == EstadoVentana.Minimizada ? Visibility.Visible : Visibility.Collapsed
        };
        t.Hueco.Children.Add(t.Minimizada);
        if (v.Estado == EstadoVentana.Minimizada) t.Respaldo.Opacity = 0.45;

        dentro.Children.Add(t.Hueco);

        // ----- El pie: icono, título, app, monitor y estado -----
        var pie = new Grid { Margin = new Thickness(8, 4, 6, 4), Background = Brushes.Transparent };
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(pie, 1);

        if (icono != null)
            pie.Children.Add(new Image
            {
                Source = icono, Width = 16, Height = 16,
                Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center
            });

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        t.TextoTitulo = v.Titulo.Length > 0 ? v.Titulo : v.App;
        t.Titulo = new TextBlock
        {
            Text = t.TextoTitulo,
            Foreground = esActiva ? _acentoLegible : _texto,
            FontSize = 11.5,
            FontWeight = esActiva ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        textos.Children.Add(t.Titulo);
        t.TextoSub = Subtitulo(v, esActiva);
        t.Sub = new TextBlock
        {
            Text = t.TextoSub,
            Foreground = _textoAtenuado, FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        textos.Children.Add(t.Sub);
        Grid.SetColumn(textos, 1);
        pie.Children.Add(textos);

        var aspa = Glifo(GlifoAspa);
        aspa.FontSize = 10;
        aspa.Foreground = _textoAtenuado;
        var bAspa = Boton(aspa, "Cerrar esta ventana", () => CerrarLaVentana(h));
        bAspa.Width = 24;
        bAspa.Height = 22;
        Grid.SetColumn(bAspa, 2);
        pie.Children.Add(bAspa);
        dentro.Children.Add(pie);

        t.Marco.Child = dentro;

        t.Boton = Boton(t.Marco, v.Titulo, () => ActivarDesdeLaVista(h), levantar: false);
        t.Boton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            ApuntarActivaAlPulsar();
            EmpezarArrastreDeTarjeta(h, e);
        };
        t.Boton.Width = AnchoTarjeta - 2;
        t.Boton.Height = AltoTarjeta - 2;
        t.Boton.Padding = new Thickness(0);

        // Clic central: cerrar, como en el navegador y como estaba antes de C4
        t.Boton.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            CerrarLaVentana(h);
        };
        // Clic derecho: el menú de la casa, el mismo que el del carrusel
        var ficha = v;
        t.Boton.MouseRightButtonUp += (_, e) => { e.Handled = true; AbrirMenuDeVentana(ficha, t.Boton); };

        PedirFoto(t, nueva: true, GestorVentanas.Motivo.AMano);
        return t;
    }

    // ---------- Actualizar en su sitio ----------
    //
    // Devuelve si cambió algo. Lo que NO hace, a propósito: crear elementos, cambiar el
    // tamaño de la tarjeta, moverla de hueco, reiniciar una animación ni borrar la foto.
    bool ActualizarTarjeta(TarjetaViva t, VentanaReal v, bool esActiva, GestorVentanas.Motivo motivo)
    {
        bool cambio = false;

        if (t.Activa != esActiva)
        {
            t.Activa = esActiva;
            t.Marco.Background = esActiva ? _elegido : _superficie;
            t.Marco.BorderBrush = esActiva ? _acento : Brushes.Transparent;
            t.Titulo.Foreground = esActiva ? _acentoLegible : _texto;
            t.Titulo.FontWeight = esActiva ? FontWeights.SemiBold : FontWeights.Normal;
            cambio = true;
        }

        var titulo = v.Titulo.Length > 0 ? v.Titulo : v.App;
        if (titulo != t.TextoTitulo)
        {
            t.TextoTitulo = titulo;
            t.Titulo.Text = titulo;
            t.Boton.ToolTip = v.Titulo;
            cambio = true;
        }

        var sub = Subtitulo(v, esActiva);
        if (sub != t.TextoSub)
        {
            t.TextoSub = sub;
            t.Sub.Text = sub;
            cambio = true;
        }

        if (t.Estado != v.Estado)
        {
            bool eraMin = t.Estado == EstadoVentana.Minimizada;
            t.Estado = v.Estado;
            bool esMin = v.Estado == EstadoVentana.Minimizada;
            if (t.Minimizada != null)
                t.Minimizada.Visibility = esMin ? Visibility.Visible : Visibility.Collapsed;
            t.Respaldo.Opacity = esMin ? 0.45 : 0.85;
            // Al volver de minimizada, la foto guardada es de antes: se pide otra ya, sin
            // esperar el intervalo.
            if (eraMin && !esMin) t.UltimaFoto = DateTime.MinValue;
            cambio = true;
        }

        PedirFoto(t, nueva: false, motivo);
        return cambio;
    }

    // ---------- Pedir una foto, con freno ----------
    void PedirFoto(TarjetaViva t, bool nueva, GestorVentanas.Motivo motivo)
    {
        bool minimizada = t.Estado == EstadoVentana.Minimizada;
        if (!TocaFoto(nueva, minimizada, DateTime.Now - t.UltimaFoto, motivo, MinEntreFotos)) return;

        // La marca se pone ANTES de pedirla: si no, una ráfaga de avisos encola una
        // captura por aviso y cada entrega cambia un ImageSource.
        t.UltimaFoto = DateTime.Now;
        var h = t.Hwnd;
        int gen = _generacionVista;

        Miniaturas.Pedir(h, (int)(AnchoTarjeta * 2), Dispatcher,
            // `sigueValiendo` es lo que evita pintar sobre una tarjeta que ya no está: la
            // foto tarda, y para cuando llega la vista puede haberse cerrado o la ventana
            // haber desaparecido. La generación distingue "esta vista" de una posterior.
            () => _panelAbierto == PanelVista && gen == _generacionVista && _tarjetas.ContainsKey(h),
            img =>
            {
                // Si la captura falló, se queda la de antes. Nunca se sustituye por nada.
                if (img == null) return;
                if (!_tarjetas.TryGetValue(h, out var vt) || !ReferenceEquals(vt, t)) return;
                if (ReferenceEquals(t.Foto.Source, img)) return;
                t.Foto.Source = img;
                if (!t.TieneFoto)
                {
                    t.TieneFoto = true;
                    t.Foto.Visibility = Visibility.Visible;
                    t.Respaldo.Visibility = Visibility.Collapsed;
                }
                DiagVista.MiniaturasPuestas++;
                Log.Info($"vista gen={gen} miniatura REEMPLAZADA hwnd=0x{h.ToInt64():X} motivo={motivo}");
            });
    }

    string Subtitulo(VentanaReal v, bool esActiva)
    {
        var trozos = new List<string>();
        if (v.App.Length > 0) trozos.Add(v.App);
        if (esActiva) trozos.Add("activa");
        else if (v.Estado != EstadoVentana.Normal) trozos.Add(v.Estampa.ToLowerInvariant());
        if (NombreDeMonitor(v.Monitor) is { } m) trozos.Add(m);
        return string.Join(" · ", trozos);
    }

    // "Pantalla 2". Solo se dice si hay más de una: con un monitor, decirlo es ruido.
    static string? NombreDeMonitor(IntPtr monitor)
    {
        var todos = Monitores.Todos();
        if (todos.Count < 2) return null;
        int i = todos.FindIndex(m => m.Handle == monitor);
        return i < 0 ? null : $"Pantalla {i + 1}";
    }

    // ---------- Lo que se puede hacer con una ventana ----------
    //
    // El menú es el de la casa —el mismo `CrearMenuContextual` que usan el carrusel y
    // las fijadas— y no un `ContextMenu` de WPF: eso sería una cajita blanca del
    // sistema que no pega con nada y, en una ventana WS_EX_NOACTIVATE, con el foco
    // siempre a medias.
    //
    // Y se dibuja DENTRO de la vista, en su capa, no como panel. En C4.1 se abría como
    // panel y eso cerraba la cuadrícula: los paneles de la isla son de uno en uno. Como
    // capa, la rejilla se queda detrás, visible y en su sitio, y el clic de fuera cierra
    // el menú sin cerrar la vista.
    void AbrirMenuDeVentana(VentanaReal v, FrameworkElement ancla)
    {
        var h = v.Hwnd;
        var opciones = new List<(string, Action)>
        {
            ("Activar", () => ActivarDesdeLaVista(h)),
        };

        opciones.Add(v.Estado == EstadoVentana.Minimizada
            ? ("Restaurar", () => { Nativo.Restaurar(h); ActivarDesdeLaVista(h); })
            : ("Minimizar", () => { MinimizarVentana(h); GestorVentanas.HayQueMirar(); CerrarPanel(); }));

        if (v.Estado == EstadoVentana.Maximizada)
            opciones.Add(("Restaurar el tamaño", () => { Nativo.Restaurar(h); TrasAjustar(); }));
        else
            opciones.Add(("Maximizar", () => Ajustar(h, Zona.Maximizar)));

        opciones.Add(("Mitad izquierda", () => Ajustar(h, Zona.Izquierda)));
        opciones.Add(("Mitad derecha", () => Ajustar(h, Zona.Derecha)));
        opciones.Add(("Cuarto superior izquierdo", () => Ajustar(h, Zona.ArribaIzquierda)));
        opciones.Add(("Cuarto superior derecho", () => Ajustar(h, Zona.ArribaDerecha)));
        opciones.Add(("Cuarto inferior izquierdo", () => Ajustar(h, Zona.AbajoIzquierda)));
        opciones.Add(("Cuarto inferior derecho", () => Ajustar(h, Zona.AbajoDerecha)));

        string nombre = v.App.Length > 0 ? v.App : "esta ventana";
        opciones.Add(($"Cerrar {nombre}", () => Confirmar(
            $"¿Cerrar «{Recortar(v.Titulo, 40)}»?",
            "Se le pedirá que cierre. Si tiene algo sin guardar, te lo preguntará.",
            "Cerrar", () => CerrarLaVentana(h), peligro: false)));

        int suyas = GestorVentanas.Todas.Count(x => x.Pid == v.Pid);
        opciones.Add(($"Forzar el cierre de {nombre}", () => Confirmar(
            $"¿Forzar el cierre de {nombre}?",
            suyas > 1
                ? $"Se mata el proceso entero, con sus {suyas} ventanas. Lo que no esté guardado SE PIERDE."
                : "Se mata el proceso como con «Finalizar tarea». Lo que no esté guardado SE PIERDE.",
            "Forzar", () => { Nativo.MatarProceso(v.Pid); GestorVentanas.HayQueMirar(); }, peligro: true)));

        const double altoFila = 34;
        double ancho = Math.Clamp(opciones.Max(o => AnchoDeTexto(o.Item1)) + 48, 240, 460);
        double alto = opciones.Count * altoFila + 12;

        // Cada opción cierra el menú antes de hacer lo suyo. Hay que envolverlas aquí y
        // no confiar en que la acción cierre: las que solo colocan (minimizar) no cierran
        // la vista, y el menú se quedaría puesto encima.
        var envueltas = opciones
            .Select(o => (o.Item1, new Action(() => { CerrarMenuDeVista(); o.Item2(); })))
            .ToArray();

        var tarjeta = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = _superficieAlta,
            BorderBrush = _borde,
            BorderThickness = new Thickness(1),
            Width = ancho,
            Child = CrearMenuContextual(envueltas, ancho, altoFila)
        };

        if (_capaMenuVista == null || _lienzoMenuVista == null || _raizVista == null) return;
        _lienzoMenuVista.Children.Clear();
        _lienzoMenuVista.Children.Add(tarjeta);
        _capaMenuVista.Visibility = Visibility.Visible;

        // Pegada a la tarjeta, y dentro de la vista pase lo que pase: una tarjeta de la
        // última fila sacaría el menú por debajo del panel.
        var esquina = ancla.TranslatePoint(new Point(0, 0), _raizVista);
        double x = Math.Max(2, Math.Min(esquina.X + 12, _raizVista.ActualWidth - ancho - 2));
        double y = Math.Max(2, Math.Min(esquina.Y + 12, Math.Max(2, _raizVista.ActualHeight - alto - 2)));
        Canvas.SetLeft(tarjeta, x);
        Canvas.SetTop(tarjeta, y);
    }

    static string Recortar(string t, int tope) =>
        t.Length <= tope ? t : t[..Math.Max(0, tope - 1)] + "…";

    // Ajustar desde la vista es una ELECCIÓN del usuario, no una recolocación de fondo:
    // además de ponerla donde se le dice, hay que traerla delante y darle el foco.
    //
    // En C4.1 solo se colocaba, y una ventana que estaba detrás se quedaba detrás: bien
    // puesta y sin verse. Mover una ventana no la activa —ni debe—, así que la
    // activación va aparte y DESPUÉS: activar primero y colocar después deja un
    // parpadeo en el sitio viejo.
    //
    // Y el panel se cierra ANTES de nada: tiene el teclado pedido, y cerrarlo le
    // devuelve el foco a quien lo tenía. Si se activara primero, el cierre se lo
    // quitaría a la ventana recién elegida para dárselo a la de antes.
    // `monitor` es por dónde se eligió: si viene de arrastrar la tarjeta, el del PUNTERO,
    // que es con el que se dibujó la previsualización. Desde el menú no hay puntero que
    // valga y se deja a null, y entonces manda el monitor de la VENTANA: ajustar desde la
    // vista de la pantalla 1 algo que está en la 2 tiene que dejarlo en la 2.
    void Ajustar(IntPtr h, Zona z, Monitores.Info? monitor = null)
    {
        if (!Nativo.Existe(h)) { GestorVentanas.Refrescar(); return; }
        CerrarVista();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ZonasAjuste.Ajustar(h, z, monitor, activar: true);
            GestorVentanas.HayQueMirar();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    void TrasAjustar()
    {
        GestorVentanas.HayQueMirar();
        CerrarPanel();
    }

    void ActivarDesdeLaVista(IntPtr h)
    {
        if (!Nativo.Existe(h)) { GestorVentanas.Refrescar(); return; }
        // Cerrar ANTES de activar: el panel tiene el teclado pedido y, si se activara la
        // ventana primero, al cerrarse le devolvería el foco a la de antes.
        CerrarVista();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PulsarVentana(h);
            GestorVentanas.HayQueMirar();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    void CerrarLaVentana(IntPtr h)
    {
        if (!Nativo.Existe(h)) { GestorVentanas.Refrescar(); return; }
        Log.Info($"vista general: cerrar \"{Nativo.Titulo(h)}\"");
        Nativo.CerrarVentana(h);
        Miniaturas.Olvidar(h);
        // Se le da un respiro para que la app atienda el WM_CLOSE (puede preguntarte si
        // guardas). La lista se rehace sola cuando la ventana se vaya de verdad.
        GestorVentanas.HayQueMirar();
    }
}
