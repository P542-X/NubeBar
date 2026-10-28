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
        AplicarAtajosDeHypr();
        AplicarReglasDeHypr();
    }

    // ---------- Quién tiene el atajo de la vista, y cómo prestarlo un momento ----------
    //
    // Lo necesita la ventana de Ajustes para comprobar si una combinación está libre:
    // `RegisterHotKey` dice "ya registrado" igual si lo tiene otra aplicación que si lo
    // tenemos nosotros, así que para preguntar de verdad hay que soltar el nuestro antes.
    // Sin esto, Ajustes marcaba en rojo tu propio atajo, que estaba funcionando.
    internal bool TieneElAtajoDeLaVista { get; private set; }

    internal string AtajoDeLaVistaPuesto => _cfg.Barra.AtajoVistaGeneral ?? "";

    internal void SoltarAtajoDeLaVista()
    {
        if (!TieneElAtajoDeLaVista) return;
        Nativo.QuitarAtajo(_self, IdAtajoVista);
        TieneElAtajoDeLaVista = false;
    }

    internal void PonerAtajoDeLaVista() => AplicarAtajo();

    void AplicarAtajo()
    {
        if (_self == IntPtr.Zero || !_esJefa) return;   // un solo dueño del atajo global
        var atajo = _cfg.Barra.AtajoVistaGeneral;
        if (string.IsNullOrWhiteSpace(atajo))
        {
            Nativo.QuitarAtajo(_self, IdAtajoVista);
            TieneElAtajoDeLaVista = false;
            return;
        }

        bool ok = Nativo.RegistrarAtajo(_self, IdAtajoVista, atajo);
        TieneElAtajoDeLaVista = ok;
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
        if (msg != Nativo.WM_HOTKEY) return IntPtr.Zero;
        int id = wParam.ToInt32();

        // ---------- El atajo de la vista general, intacto ----------
        //
        // Va primero y con su id de siempre (0xB0CA). Los de NubeHypr viven en otro rango
        // (0xB100+), así que ni por id ni por combinación se pueden cruzar.
        if (id == IdAtajoVista)
        {
            manejado = true;
            // La vista general se abre en la pantalla donde tengas el ratón.
            // Si estaba abierta en otra, esa se cierra y no se abre una segunda.
            var abierta = Islas.FirstOrDefault(i => i._panelAbierto == PanelVista);
            (abierta ?? IslaDelRaton()).AlternarVista();
            return IntPtr.Zero;
        }

        // ---------- Y los de NubeHypr ----------
        if (HyprTeclas.EsNuestro(id))
        {
            manejado = true;
            AtajoDeHypr(id);
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    // ---------- Lo que hace un atajo de NubeHypr ----------
    //
    // Resuelve el nombre, lo convierte en comando y lo ejecuta contra el estado observado.
    // Y con la capa en SIMULACIÓN —que es lo de serie— calcula y apunta sin mover nada:
    // un atajo no puede ser la puerta trasera por la que se mueven ventanas sin que el
    // usuario haya puesto el modo en "aplicar".
    void AtajoDeHypr(int id)
    {
        var nombre = HyprTeclas.NombreDe(id);
        if (nombre == null) { Log.Info($"atajo de NubeHypr {id}: no se sabe qué era"); return; }

        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        if (modo == NubeHypr.Modo.Apagado)
        {
            // No debería poder pasar —con la capa apagada no se registra nada— pero si
            // pasa, se dice en vez de hacer algo a medias.
            Log.Info($"atajo «{HyprAtajos.Rotulo(nombre)}» con la capa apagada: no se hace nada");
            // Y se dice EN PANTALLA, no solo en el registro: una tecla que no hace nada y no
            // explica por qué es una tecla que el usuario cree rota.
            Avisar(HyprAviso.Apagado(HyprSalud.Seguro));
            return;
        }

        var comando = HyprAtajos.Comando(nombre);
        if (comando == null) { Log.Info($"atajo «{nombre}»: ese comando no existe"); return; }

        // D9.1: deshacer va aparte de los demás. Los otros comandos miran las ventanas de
        // ahora y calculan algo; este saca el último lote y lo devuelve. No hay estado que
        // observar ni reparto que calcular.
        if (comando is Comando.Deshacer)
        {
            Log.Info($"[D9]  atajo «{HyprAtajos.Rotulo(nombre)}»");
            DeshacerLoUltimo(modo);
            return;
        }

        try
        {
            // Los de espacio se resuelven contra la lista de verdad; los demás, contra el
            // estado observado.
            if (comando is Comando.AlVecino or Comando.AlNumero)
            {
                var espacios = Escritorios.Espacios();
                var destino = HyprEspacios.Resolver(espacios, comando);
                Log.Info($"[D3]  atajo «{HyprAtajos.Rotulo(nombre)}» -> " +
                         (destino == null ? "no hay destino: no se hace nada"
                                          : $"{destino.Value.Espacio} ({destino.Value.Porque})"));

                // El indicador dice el MOTIVO cuando no hay destino. «Espacio →» sin más, en
                // el último escritorio, parecería que el atajo está roto; «no hay más
                // escritorios a la derecha» se entiende a la primera.
                // El rótulo se arma con la misma `Ficha` que usa el indicador de la barra, así
                // que el escritorio se llama igual en los dos sitios. Si aquí se escribiera
                // "escritorio 3" y el indicador dijera "3 · Trabajo", serían dos nombres para
                // la misma cosa en la misma pantalla.
                Avisar(HyprAviso.DelEspacio(nombre, destino == null ? null
                    : HyprIndicador.Rotulo(
                        new HyprIndicador.Ficha(destino.Value.Espacio.Numero,
                                                HyprEspacios.NombreVisible(destino.Value.Espacio.Nombre,
                                                                           destino.Value.Espacio.Orden),
                                                true, 0),
                        espacios.Count, HyprIndicador.Talla.Medio),
                    "no hay escritorio al que ir"));
                return;     // aplicarlo de verdad es D4 con su arnés; aquí se apunta
            }

            var obs = HyprAdaptador.Observar(
                layout: NubeHypr.Layout.Principal, proporcion: _cfg.NubeHypr.Gap > 0 ? 0.6 : 0.6);
            var r = NubeHypr.Ejecutar(obs.Estado, comando);
            Log.Info($"[D3]  atajo «{HyprAtajos.Rotulo(nombre)}» -> {r.Porque} " +
                     $"({r.Efectos.Count} efecto(s), modo={modo.ToString().ToLowerInvariant()})");
            foreach (var ef in r.Efectos) Log.Info("    " + ef);

            // ---------- D9.4: y por la PUERTA, no por el registro ----------
            //
            // Hasta aquí el atajo calculaba y lo apuntaba. Eso estaba bien mientras no había
            // puerta: aplicar desde un manejador de teclas, sin respeto a tu gesto ni tope de
            // ritmo ni forma de deshacer, es justo lo que D9.1 existe para impedir.
            //
            // Con el portón montado, el atajo pasa por él como todo lo demás. Lo que **no**
            // cambia es quién decide si se mueve algo: el modo. En simulación —que es lo de
            // serie y lo que tú tienes— `AplicarConRedes` calcula, apunta y no toca una sola
            // ventana. Pulsar una tecla no es la puerta trasera por la que se activa nada.
            var aplicado = AplicarConRedes(r.Efectos, $"atajo «{HyprAtajos.Rotulo(nombre)}»", modo);

            // Y lo que se cuenta es lo que SALIÓ por la puerta, no lo que el motor propuso.
            // La diferencia entre los dos números la decide `HyprAviso`, que es puro y tiene
            // pruebas; aquí solo se le dan los dos.
            Avisar(HyprAviso.DelComandoAplicado(nombre, r.Efectos.Count,
                                                aplicado?.Movidas ?? 0,
                                                modo != NubeHypr.Modo.Aplicar));
        }
        catch (Exception ex)
        {
            Log.Error($"ejecutar el atajo «{nombre}»", ex);
            // Un atajo que revienta tiene que decirlo en pantalla. Callarlo deja al usuario
            // pulsando una tecla muerta sin saber que hay un error en el registro.
            Avisar(HyprAviso.Reventado(nombre));
        }
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

        // D6: el anillo de foco del teclado, POR FUERA del marco. Aparte del marco a
        // propósito: "la activa del sistema" y "la elegida con las flechas" son dos cosas
        // distintas y tienen que poder verse a la vez. Y por fuera, para que encenderlo no
        // mueva ni un píxel de lo que hay dentro — mover contenido al cambiar el foco es
        // exactamente lo que se lee como que las tarjetas tiemblan.
        public Border Halo = null!;
        public bool Elegida;

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

        RecalcularReparto(VentanasDeLaVista().Count);
        int columnas = _reparto.Columnas;
        // Con teclado: la vista se abre a propósito —por su botón o por su atajo— y
        // entonces Esc tiene que cerrarla y devolver el foco a donde estabas, que es lo
        // que hace `ActivarTeclado(false)` al cerrar el panel.
        AbrirPanel(PanelVista, ConstruirVista(), _reparto.AnchoTotal, _reparto.AltoTotal,
                   teclado: true, scrollPropio: true);
        EscucharCambios(true);
        PreviewKeyDown -= TeclaDeLaVista;
        PreviewKeyDown += TeclaDeLaVista;
        if (_superpuesta is { } sup) { sup.PreviewKeyDown -= TeclaDeLaVista; sup.PreviewKeyDown += TeclaDeLaVista; }

        // ---------- La elección empieza en la ventana activa ----------
        //
        // Y si no hay activa —puede pasar: la activa es la propia isla mientras el panel
        // tiene el teclado— en la primera. Abrir sin nada elegido obligaría a pulsar una
        // flecha solo para empezar, y la primera pulsación se perdería.
        _elegidaVista = IntPtr.Zero;
        var cursor = _ordenVista.Contains(GestorVentanas.Activa)
            ? GestorVentanas.Activa
            : _ordenVista.FirstOrDefault();
        if (cursor != IntPtr.Zero) Elegir(cursor, porTeclado: false);

        Log.Info($"vista abierta gen={_generacionVista} tarjetas={_tarjetas.Count} columnas={columnas} " +
                 $"tarjeta={_reparto.AnchoTarjeta:0}x{_reparto.AltoTarjeta:0} hueco={_reparto.Hueco:0} " +
                 $"caja={_reparto.AnchoTotal:0}x{_reparto.AltoTotal:0} rueda={_reparto.ConRueda} " +
                 $"elegida=0x{_elegidaVista.ToInt64():X6}");

        // Y el tamaño REAL de cada tarjeta, una vez medida la cuadrícula.
        //
        // No es lo mismo que el reparto: el reparto es lo que se ha pedido, esto es lo que
        // WPF ha hecho. La diferencia es justo donde se esconden las tarjetas de distinto
        // tamaño, y una captura no la resuelve —mirando una imagen se discute si una
        // tarjeta «parece» más pequeña; midiéndola, no—.
        Dispatcher.InvokeAsync(() =>
        {
            if (_panelAbierto != PanelVista) return;
            Log.Info("vista: " + MedidaDeLasTarjetas().Linea);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ---------- Lo mismo, pero devuelto ----------
    //
    // Esto vivía dentro del `InvokeAsync` de arriba y solo sabía escribir en el registro. Un
    // diagnóstico que quisiera EXIGIR que las tarjetas son iguales tenía que leerse el log y
    // buscar una cadena, que es la forma más frágil de comprobar algo.
    //
    // Devolverlo no cambia lo que se apunta: la línea es la misma. Lo que cambia es que
    // ahora se puede afirmar sobre ella.
    internal sealed record MedidaDeTarjetas(int Cuantas, int TamanosDistintos,
                                            double Ancho, double Alto, string Linea)
    {
        public bool TodasIguales => TamanosDistintos <= 1;
    }

    internal MedidaDeTarjetas MedidaDeLasTarjetas()
    {
        var botones = _ordenVista
            .Where(_tarjetas.ContainsKey)
            .Select(h => _tarjetas[h].Boton)
            .Where(b => b != null)
            .Select(b => b!)
            .ToList();

        var medidas = botones.Select(b => $"{b.ActualWidth:0}x{b.ActualHeight:0}").ToList();
        var distintas = medidas.Distinct().ToList();
        string linea = $"tarjetas medidas = {string.Join(" ", medidas)}" +
                       (distintas.Count <= 1 ? "  (todas iguales)"
                                             : $"  ¡OJO! {distintas.Count} tamaños distintos");

        return new MedidaDeTarjetas(botones.Count, distintas.Count,
                                    botones.Count > 0 ? botones[0].ActualWidth : 0,
                                    botones.Count > 0 ? botones[0].ActualHeight : 0,
                                    linea);
    }

    // ---------- El reparto de la cuadrícula ----------
    //
    // Aquí vivían dos líneas que no miraban la pantalla:
    //
    //     static int Columnas(int cuantas) => clamp(ceil(sqrt(cuantas)), 1, 4);
    //     const double AnchoTarjeta = 236, AltoTarjeta = 158;
    //
    // De ellas salían, una por una, las cosas que se veían mal: tarjetas del mismo tamaño
    // en un portátil de 768 px y en un 4K, un agujero del tamaño de una tarjeta en la
    // última fila con cinco ventanas, un tope de cuatro columnas en monitores donde caben
    // seis, y una cuadrícula que no se recolocaba al cerrar una ventana porque el número
    // de columnas solo se calculaba al abrir.
    //
    // Ahora lo decide `VistaReparto`, que sí mira el sitio que hay, y está comprobado en
    // ocho pantallas distintas —4K al 175 %, vertical, portátil pequeño— sin tenerlas
    // delante.
    VistaReparto.Reparto _reparto = VistaReparto.Para(0, new VistaReparto.Sitio(1920, 1080, 1, false));

    VistaReparto.Sitio SitioDeLaVista()
    {
        double escala = Math.Max(0.1, _monitor.Escala);
        return new VistaReparto.Sitio(_monitor.Ancho / escala, _monitor.Alto / escala, escala, Vertical);
    }

    // Recalcula el reparto para el número de ventanas de AHORA, y dice si ha cambiado la
    // forma. Eso último es lo que distingue «quita una tarjeta» de «recoloca la rejilla»:
    // cerrar la cuarta de cuatro pasa de 2x2 a una fila de tres, y dejar las tres donde
    // estaban deja un agujero donde estaba la cerrada.
    bool RecalcularReparto(int cuantas)
    {
        var antes = _reparto;
        _reparto = VistaReparto.Para(cuantas, SitioDeLaVista());
        return antes.Columnas != _reparto.Columnas ||
               Math.Abs(antes.AnchoTarjeta - _reparto.AnchoTarjeta) > 0.5;
    }

    // El alto total de la vista. Se conserva el nombre y la forma estática porque hay
    // pruebas de A–D que la llaman; por dentro ya es el reparto nuevo.
    internal static double AltoDeLaVista(int cuantas, int columnas, double altoPantalla) =>
        VistaReparto.Para(cuantas, new VistaReparto.Sitio(
            Math.Max(640, columnas * VistaReparto.AnchoMaximo / VistaReparto.ParteDelAncho),
            altoPantalla, 1, false)).AltoTotal;

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




    // ================= --diagespacios =================
    //
    // Lee los escritorios de verdad y los enseña. **No crea, no renombra, no reordena y no
    // mueve ninguna ventana**: D4 se comprueba en lectura, porque crear escritorios en el
    // equipo de alguien para ver si la API responde es pasarse.
    async System.Threading.Tasks.Task CorrerDiagnosticoDeEspacios()
    {
        try
        {
            Apunta("=== escritorios virtuales (D4) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            await System.Threading.Tasks.Task.Delay(3000);

            Apunta($"[D4]  api disponible={(Escritorios.Disponible ? "sí" : "NO")}");

            var espacios = Escritorios.Espacios();
            bool modoDeUno = HyprEspacios.EsElModoDeUno(espacios);
            Apunta($"[D4]  espacios={espacios.Count} " +
                   (modoDeUno ? "(MODO DE UNO: la API no contestó)" : "(de la API)"));
            foreach (var e in espacios) Apunta($"  · {e}  id={e.Id}");

            int actual = HyprEspacios.IndiceDelActual(espacios);
            Apunta($"[D4]  actual={(actual < 0 ? "no se sabe" : espacios[actual].ToString())}");

            // Los vecinos, sin ir a ninguno
            var sig = HyprEspacios.Vecino(espacios, true);
            var ant = HyprEspacios.Vecino(espacios, false);
            Apunta($"[D4]  siguiente={(sig?.Nombre ?? "ninguno (estás en el último)")}");
            Apunta($"[D4]  anterior={(ant?.Nombre ?? "ninguno (estás en el primero)")}");

            // El mapa de ventana a espacio, leído de verdad
            var mapa = new HyprEspacios.Mapa();
            var actualId = actual >= 0 ? espacios[actual].Id : HyprEspacios.IdUnico;
            int sabidas = 0, nosabidas = 0;
            foreach (var w in GestorVentanas.Todas)
            {
                var id = Escritorios.EspacioDe(w.Hwnd);
                if (id != Guid.Empty) { mapa.Apuntar(w.Hwnd, id); sabidas++; }
                else nosabidas++;
                var (donde, reciente, sabido) = mapa.Consultar(w.Hwnd, actualId, HyprEspacios.Caducidad);
                var nombre = espacios.FirstOrDefault(e => e.Id == donde).Nombre ?? "(otro)";
                Apunta($"  {w.App,-18} espacio={nombre,-16} " +
                       $"{(sabido ? "de la API" : "supuesto: el actual")}{(sabido && !reciente ? " (viejo)" : "")}");
            }
            Apunta($"[D4]  mapa: contestó por {sabidas} ventana(s), no contestó por {nosabidas}");

            // Y la reconciliación, mirando dos veces: si cambias de escritorio a mano
            // durante estos segundos, aquí se ve.
            Apunta($"[D4]  mirando {DiagEspacios.Segundos} s; cambia de escritorio a mano si quieres");
            var antes = espacios;
            for (int s = 2; s <= DiagEspacios.Segundos; s += 2)
            {
                await System.Threading.Tasks.Task.Delay(2000);
                var ahora = Escritorios.Espacios();
                var c = HyprEspacios.Comparar(antes, ahora);
                if (!c.Nada)
                {
                    Apunta($"[D4]  cambio en t+{s}s: actual={(c.CambioElActual ? "sí" : "no")} " +
                           $"nuevos={c.Nuevos.Count} quitados={c.Quitados.Count} " +
                           $"renombrados={c.Renombrados.Count} orden={(c.CambioElOrden ? "sí" : "no")} " +
                           $"=> {(c.TocaRepartir ? "TOCA REPARTIR" : "no hace falta repartir")}");
                    foreach (var e in ahora) Apunta($"  · {e}");
                    antes = ahora;
                }
            }
            Apunta("[D4]  nada se creó, se renombró, se reordenó ni se movió");
        }
        catch (Exception ex) { Log.Error("el diagnóstico de espacios", ex); }
        finally { Application.Current.Shutdown(0); }
    }
    // ================= --diagmotor =================
    //
    // El motor de D2, en simulación salvo que se le dé un hwnd. Ver DiagMotor.cs.
    async System.Threading.Tasks.Task CorrerDiagnosticoDelMotor()
    {
        try
        {
            Apunta($"=== motor de NubeHypr (D2): {DiagMotor.Segundos} s ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");

            var modo = DiagMotor.SoloEsta != IntPtr.Zero
                ? NubeHypr.Modo.Aplicar : NubeHypr.Modo.Simulacion;
            Apunta($"[D2]  modo={modo.ToString().ToLowerInvariant()} " +
                   (DiagMotor.SoloEsta != IntPtr.Zero
                        ? $"(se aplicará SOLO a 0x{DiagMotor.SoloEsta.ToInt64():X6})"
                        : "(no se mueve ninguna ventana)"));
            Apunta(NubeHypr.LineaSalud(modoSeguro: false, adaptadorListo: true, _cfg.Version));

            await System.Threading.Tasks.Task.Delay(4000);

            // El área útil sale del monitor de esta isla y de la geometría real de la
            // barra. Calcular con un monitor y aplicar con otro implícito es justo lo que
            // la especificación prohíbe.
            var m = _monitor;
            var monitor = new NubeHypr.Rect(m.X, m.Y, m.Ancho, m.Alto);
            var borde = Vertical
                ? (PegadaIzquierda ? NubeHypr.Borde.Izquierda : NubeHypr.Borde.Derecha)
                : (Arriba ? NubeHypr.Borde.Arriba : NubeHypr.Borde.Abajo);
            double reserva = _cfg.Barra.ReservarEspacio ? Grosor + _cfg.Barra.Margen * 2 : 0;
            double escala = m.Escala;
            var area = NubeHypr.AreaUtil(monitor, borde, reserva * escala,
                                         _cfg.NubeHypr.Margen * escala);

            Apunta($"[D2]  monitor={monitor} borde={borde} reserva={reserva * escala:0} " +
                   $"margen={_cfg.NubeHypr.Margen * escala:0} area={area}");

            // ---------- Las cuatro vueltas, una por layout ----------
            foreach (var layout in new[] { NubeHypr.Layout.Principal, NubeHypr.Layout.Horizontal,
                                           NubeHypr.Layout.Vertical, NubeHypr.Layout.Flotante })
            {
                Apunta($"--- layout {layout.ToString().ToLowerInvariant()} ---");
                var r = HyprAdaptador.Ciclo(area, layout, _cfg.NubeHypr.Gap * escala,
                                            0.6, NubeHypr.Modo.Simulacion, Apunta);
                Apunta($"[D2]  {layout}: movidas(simuladas)={r.Movidas} saltadas={r.Saltadas}");
                await System.Threading.Tasks.Task.Delay(700);
            }

            // ---------- La previsualización ----------
            //
            // Se enseña el destino de la PRIMERA ventana del reparto principal, el rato
            // que se pida. Es atravesable y no se activa: ver PonerPreviaHypr.
            var obs = HyprAdaptador.Observar(layout: NubeHypr.Layout.Principal);
            var efectos = NubeHypr.Calcular(obs.Estado, area, _cfg.NubeHypr.Gap * escala);
            var primero = efectos.FirstOrDefault(e => e.Tipo == TipoEfecto.Colocar);
            if (primero != null)
            {
                PonerPreviaHypr(primero.Destino, "aquí iría");
                Apunta($"[D2]  previa en {primero.Destino} (atravesable, sin foco)");
                await System.Threading.Tasks.Task.Delay(2500);
                Apunta($"[D2]  previa: ¿tiene el foco?={(Nativo.Activa() == _previaHwnd ? "SÍ (MAL)" : "no")}");
            }

            // ---------- Y solo si se pidió: mover UNA ----------
            if (DiagMotor.SoloEsta != IntPtr.Zero)
            {
                if (!Nativo.Existe(DiagMotor.SoloEsta))
                    Apunta($"[D2]  0x{DiagMotor.SoloEsta.ToInt64():X6} no existe: no se mueve nada");
                else
                {
                    var antes = HyprAdaptador.DeRect(Nativo.Rectangulo(DiagMotor.SoloEsta));
                    Apunta($"[D2]  antes  0x{DiagMotor.SoloEsta.ToInt64():X6} en {antes}");
                    var r = HyprAdaptador.Ciclo(area, NubeHypr.Layout.Principal,
                                                _cfg.NubeHypr.Gap * escala, 0.6,
                                                NubeHypr.Modo.Aplicar, Apunta, DiagMotor.SoloEsta);
                    await System.Threading.Tasks.Task.Delay(600);
                    var despues = HyprAdaptador.DeRect(Nativo.Rectangulo(DiagMotor.SoloEsta));
                    Apunta($"[D2]  después 0x{DiagMotor.SoloEsta.ToInt64():X6} en {despues}");
                    Apunta($"[D2]  ¿se movió?={(antes.CasiIgual(despues) ? "no" : "sí")} " +
                           $"divergió={(r.Divergio ? "SÍ" : "no")}");

                    // Y se devuelve a donde estaba: una medida no deja la pantalla
                    // cambiada.
                    await System.Threading.Tasks.Task.Delay(1500);
                    Nativo.Colocar(DiagMotor.SoloEsta, (int)antes.X, (int)antes.Y,
                                   (int)antes.Ancho, (int)antes.Alto);
                    await System.Threading.Tasks.Task.Delay(400);
                    var vuelta = HyprAdaptador.DeRect(Nativo.Rectangulo(DiagMotor.SoloEsta));
                    Apunta($"[D2]  devuelta a {vuelta}  ¿igual que al empezar?=" +
                           $"{(antes.CasiIgual(vuelta, 8) ? "sí" : "NO")}");
                }
            }

            for (int s = 5; s <= DiagMotor.Segundos; s += 5)
            {
                await System.Threading.Tasks.Task.Delay(5000);
                Apunta($"  {s} s: ventanas={GestorVentanas.Cuantas}");
            }

            QuitarPreviaHypr();
        }
        catch (Exception ex) { Log.Error("el diagnóstico del motor", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    // El hwnd de la previsualización, para poder comprobar que NO tiene el foco.
    IntPtr _previaHwnd => _previa is { } p ? p.Self : IntPtr.Zero;
    // ================= --diaghypr =================
    //
    // La línea base de D0: apagado no mueve nada. Ver DiagHypr.cs.
    async System.Threading.Tasks.Task CorrerDiagnosticoDeHypr()
    {
        try
        {
            Apunta($"=== línea base de NubeHypr: {DiagHypr.Segundos} s ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
            Apunta($"[D0]  modo efectivo={modo.ToString().ToLowerInvariant()} " +
                   $"(Activo={_cfg.NubeHypr.Activo}, Modo=\"{_cfg.NubeHypr.Modo}\")");
            Apunta(NubeHypr.LineaSalud(modoSeguro: false, adaptadorListo: false, _cfg.Version));

            // Que la isla esté montada y las ventanas leídas
            await System.Threading.Tasks.Task.Delay(4000);

            // ---------- El inventario: qué se podría organizar y qué no ----------
            long v = NubeHypr.SiguienteVersion();
            var antes = GeometriaDeAhora();
            Apunta(NubeHypr.LineaSnapshot(v, antes.Count, GestorVentanas.Activa));

            int organizables = 0;
            var porQue = new Dictionary<string, int>();
            foreach (var w in GestorVentanas.Todas)
            {
                bool ok = NubeHypr.SePuedeOrganizar(
                    w.Exe, Nativo.Clase(w.Hwnd), Nativo.EsNuestra(w.Hwnd),
                    w.SePuedeActivar, w.Rect.Der > w.Rect.Izq && w.Rect.Aba > w.Rect.Arr,
                    out string motivo);
                if (ok) { organizables++; Apunta($"  organizable  {w.App}  \"{Corto(w.Titulo)}\""); }
                else
                {
                    porQue[motivo] = porQue.GetValueOrDefault(motivo) + 1;
                    Apunta($"  PROTEGIDA    {w.App}  \"{Corto(w.Titulo)}\"  ({motivo})");
                }
            }
            Apunta($"[D0]  organizables={organizables} protegidas={antes.Count - organizables}" +
                   (porQue.Count > 0 ? "  por: " + string.Join(", ", porQue.Select(p => $"{p.Key} x{p.Value}")) : ""));

            // ---------- El rato de espera ----------
            for (int s = 5; s <= DiagHypr.Segundos; s += 5)
            {
                await System.Threading.Tasks.Task.Delay(5000);
                Apunta($"  {s} s: ventanas={GestorVentanas.Cuantas} activa=0x{GestorVentanas.Activa.ToInt64():X6}");
            }

            // ---------- Y la comparativa ----------
            var despues = GeometriaDeAhora();
            var fuera = NubeHypr.Comparar(antes, despues);
            Apunta($"[D0]  comparativa: antes={antes.Count} después={despues.Count} " +
                   $"divergencias={fuera.Count}");
            foreach (var d in fuera) Apunta("  MOVIDA  " + d);
            Apunta(fuera.Count == 0
                ? "[D0]  apagado no movió ninguna ventana"
                : $"[D0]  ATENCIÓN: {fuera.Count} ventana(s) cambiaron de sitio con la capa apagada");

            Apunta(NubeHypr.LineaConfirmar(v, 0, antes.Count, 0));
        }
        catch (Exception ex) { Log.Error("la línea base de NubeHypr", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    // La geometría de ahora mismo, en píxeles de pantalla, con la identidad de cada
    // ventana. En píxeles y no en unidades de WPF a propósito: es lo que `Nativo.RECT`
    // devuelve, y convertir aquí sería meter una conversión implícita en una medida.
    List<(IntPtr Hwnd, string App, NubeHypr.Rect Rect)> GeometriaDeAhora() =>
        GestorVentanas.Todas.Select(w => (
            w.Hwnd,
            w.App,
            new NubeHypr.Rect(w.Rect.Izq, w.Rect.Arr,
                              w.Rect.Der - w.Rect.Izq, w.Rect.Aba - w.Rect.Arr))).ToList();

    static string Corto(string s) =>
        s.Length <= 48 ? s : s[..45] + "...";
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

            // ---------- Y de paso: quién manda notificaciones en este equipo ----------
            //
            // Va aquí porque es la pregunta hermana de la bandeja, y se confunden mucho: una
            // app puede tener icono en la bandeja —Steam, Epic, Discord lo tienen— y no haber
            // mandado jamás una notificación de Windows. Son dos cosas distintas y esta lista
            // lo deja claro de un vistazo.
            //
            // NubeBar no filtra por app: lee `wpndatabase.db`, que es donde Windows guarda
            // las suyas. Lo que sale aquí, el centro lo enseña. Lo que no sale es que nunca
            // ha pasado por la plataforma de notificaciones.
            Apunta("");
            Apunta("--- quién ha mandado notificaciones de Windows alguna vez ---");
            var remitentes = Notificaciones.Remitentes();
            if (remitentes.Count == 0)
                Apunta("  (ninguno: o no hay base de notificaciones, o está vacía)");
            else
                foreach (var (app, aumid, cuantas) in remitentes)
                    Apunta($"  · {app,-34} {cuantas,4} notificación(es)   [{aumid}]");
            Apunta($"  total: {remitentes.Count} remitente(s)");
            Apunta("  Las apps que dibujan su propio recuadro en vez de usar el sistema no");
            Apunta("  salen aquí y no se pueden leer: no están filtradas, es que para Windows");
            Apunta("  no existen.");
            Apunta("");

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
        // ---------- Y por qué hace falta el `Extent > Viewport` ----------
        //
        // Sin él, esto cantaba MAL en todo panel cuyo contenido fuera MÁS CORTO que su hueco:
        // con extent 332 y viewport 338 el máximo desplazable sale -6, y un offset de 0 es
        // «mayor que -5». O sea que avisaba de que el contenido se ha ido fuera justo en el
        // caso en que no hay nada que irse. Lo cazó `--diaginicio` después del rediseño.
        //
        // Un diagnóstico que avisa cuando no pasa nada es peor que no tenerlo: enseña a no
        // mirarlo. Solo se mide si hay algo de verdad que desplazar.
        if (r.HayScroll && r.Viewport > 0 && r.Extent > r.Viewport &&
            r.OffsetY > r.Extent - r.Viewport + 1)
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
        if (e.Key == Key.Escape)
        {
            switch (EscCierra(_panelAbierto == PanelVista, MenuDeVistaAbierto))
            {
                case QueCierra.Menu: e.Handled = true; CerrarMenuDeVista(); break;
                case QueCierra.Vista: e.Handled = true; CerrarVista(); break;
            }
            return;
        }

        // ---------- D6: el resto del teclado ----------
        //
        // Con el menú de una tarjeta abierto, el teclado es del menú: mover la elección de
        // debajo mientras hay un menú encima deja el menú apuntando a otra ventana.
        if (_panelAbierto != PanelVista || MenuDeVistaAbierto) return;

        switch (e.Key)
        {
            case Key.Left: e.Handled = true; MoverEleccion(HyprVista.Mover.Izquierda); break;
            case Key.Right: e.Handled = true; MoverEleccion(HyprVista.Mover.Derecha); break;
            case Key.Up: e.Handled = true; MoverEleccion(HyprVista.Mover.Arriba); break;
            case Key.Down: e.Handled = true; MoverEleccion(HyprVista.Mover.Abajo); break;
            case Key.Home: e.Handled = true; MoverEleccion(HyprVista.Mover.Principio); break;
            case Key.End: e.Handled = true; MoverEleccion(HyprVista.Mover.Final); break;

            // Enter activa la elegida. Es la razón de ser de todo lo anterior: llegar a una
            // ventana y traerla delante sin tocar el ratón.
            case Key.Enter or Key.Space:
                if (_elegidaVista == IntPtr.Zero) break;
                e.Handled = true;
                ActivarDesdeLaVista(_elegidaVista);
                break;

            // Y el menú de la elegida con la tecla de menú contextual o con Mayús+F10, que
            // es lo que hace Windows en cualquier lista. Son dos casos y no uno con una
            // condición enrevesada: `Key.Apps` no lleva modificador y F10 solo cuenta con
            // Mayús —F10 a secas es otra cosa en muchas aplicaciones y no hay por qué
            // quedárselo.
            case Key.Apps:
            case Key.F10 when Keyboard.Modifiers == ModifierKeys.Shift:
                if (_elegidaVista == IntPtr.Zero) break;
                var cual = GestorVentanas.Todas.FirstOrDefault(x => x.Hwnd == _elegidaVista);
                if (cual == null || !_tarjetas.TryGetValue(_elegidaVista, out var suya)) break;
                e.Handled = true;
                AbrirMenuDeVentana(cual, suya.Boton);
                break;

            // Supr cierra la elegida, con la misma confirmación que el menú: cerrar una
            // ventana con una tecla y sin preguntar perdería trabajo sin aviso.
            case Key.Delete:
                if (_elegidaVista == IntPtr.Zero) break;
                e.Handled = true;
                CerrarLaVentana(_elegidaVista);
                break;
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
        RecalcularReparto(ventanas.Count);
        int columnas = _reparto.Columnas;
        _rejillaVista = new UniformGrid { Columns = Math.Max(1, columnas), Background = Brushes.Transparent };

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
            MaxHeight = Math.Max(140, _reparto.AltoTotal - VistaReparto.Cabecera - VistaReparto.Margen),
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

        // El orden de ANTES, para poder recolocar la elección al final: si la ventana
        // elegida se cerró, su hueco es el de esta lista y no el de la nueva.
        var ordenAntes = _ordenVista.ToList();

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
            // Recalcular aquí es lo que hace que la cuadrícula se RECOLOQUE al cerrar o
            // abrir una ventana con la vista delante. Antes `Columnas` solo se llamaba al
            // abrir, así que cerrar la cuarta de cuatro dejaba tres tarjetas en una
            // rejilla de dos columnas con un agujero donde estaba la que se fue.
            bool cambioLaForma = RecalcularReparto(_ordenVista.Count);
            columnas = Math.Max(1, _reparto.Columnas);
            if (columnas != _rejillaVista.Columns) _rejillaVista.Columns = columnas;
            // Y si además cambió el tamaño de la tarjeta, hay que pasárselo a todas: si no,
            // la rejilla tendría columnas nuevas con tarjetas del tamaño viejo.
            if (cambioLaForma) foreach (var t in _tarjetas.Values) MedirTarjeta(t);
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

        // ---------- D6: y la elección sobrevive al cambio ----------
        //
        // Si la ventana elegida se cerró, la elección se queda en SU HUECO —no salta al
        // principio—, que es lo que uno espera al ir cerrando tarjetas en fila. Y si sigue
        // viva, se queda en ella aunque haya cambiado de sitio.
        if (quitadas.Count > 0 || creadas.Count > 0)
        {
            int donde = HyprVista.Recolocar(_elegidaVista, ordenAntes, _ordenVista);
            if (donde >= 0 && donde < _ordenVista.Count && _ordenVista[donde] != _elegidaVista)
                Elegir(_ordenVista[donde], porTeclado: false);
            else if (_ordenVista.Count == 0) _elegidaVista = IntPtr.Zero;
        }
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
        CambiarContenidoDelPanel(contenido);
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
            Margin = new Thickness(2),
            Background = esActiva ? _elegido : _superficie,
            BorderThickness = new Thickness(HyprVista.GrosorDelFilo),
            BorderBrush = esActiva ? _acento : Brushes.Transparent,
            ClipToBounds = true
        };

        // El anillo de foco. Siempre puesto y siempre del mismo grosor; lo que cambia es
        // si tiene color. Los márgenes suman lo que sumaba el marco solo (1+2+2 = 5), así
        // que la rejilla queda igual que antes de D6.
        t.Halo = new Border
        {
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(1),
            BorderThickness = new Thickness(HyprVista.GrosorDelHalo),
            BorderBrush = Brushes.Transparent,
            Child = t.Marco,
        };

        var dentro = new Grid();
        dentro.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dentro.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AltoPie) });

        // ----- La foto, o el icono si no hay -----
        t.Hueco = new Grid { Background = _superficieAlta, ClipToBounds = true };
        var icono = v.Icono ?? Nativo.Icono(h);
        t.Respaldo = Legible(new Image
        {
            Source = icono, Width = 34, Height = 34, Opacity = 0.85,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Una minimizada no se puede fotografiar: no tiene nada que dibujar. Se dice, en vez
        // de dejar un hueco gris que parece un fallo.
        t.Minimizada = new TextBlock
        {
            Text = "Minimizada", Foreground = _textoAtenuado, FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = v.Estado == EstadoVentana.Minimizada ? Visibility.Visible : Visibility.Collapsed
        };

        // ---------- Apilados, no superpuestos ----------
        //
        // El icono iba centrado en el hueco y «Minimizada» pegado abajo, cada uno por su
        // cuenta dentro del mismo `Grid`. Con una miniatura alta no se tocan y parecía
        // correcto; con una baja —y la de una ventana minimizada es la más baja de todas— se
        // montan uno encima del otro. En la captura se leía «Mini▮izada», con el icono del
        // terminal encima de la palabra.
        //
        // En una pila no pueden pisarse por geometría, midan lo que midan. Y la pila va
        // centrada, así que con el rótulo oculto —el caso normal— el icono queda exactamente
        // donde estaba.
        var pilaDelHueco = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        pilaDelHueco.Children.Add(t.Respaldo);
        pilaDelHueco.Children.Add(t.Minimizada);
        t.Hueco.Children.Add(pilaDelHueco);

        t.Foto = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed
        };
        t.Hueco.Children.Add(t.Foto);

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

        t.Boton = Boton(t.Halo, v.Titulo, () => ActivarDesdeLaVista(h), levantar: false);
        t.Boton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            ApuntarActivaAlPulsar();
            EmpezarArrastreDeTarjeta(h, e);
        };
        t.Boton.Padding = new Thickness(0);
        MedirTarjeta(t);

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

        // El ratón también elige: pasar por encima mueve la elección del teclado al mismo
        // sitio. Si no, el ratón y el teclado llevarían dos cursores distintos y pulsar
        // una flecha después de mover el ratón daría un salto inexplicable.
        t.Boton.MouseEnter += (_, _) => Elegir(h, porTeclado: false);

        PintarEleccion(t, v.Estado == EstadoVentana.Minimizada);
        PedirFoto(t, nueva: true, GestorVentanas.Motivo.AMano);
        return t;
    }

    // ================= D6: la elección, el foco visible y el teclado =================
    //
    // ---------- Por qué hacía falta ----------
    //
    // Hasta D6 la vista general se abría con `Ctrl+Alt+W` y lo único que hacía el teclado
    // era cerrarla con Esc. Para elegir una ventana había que coger el ratón. Eso no es
    // "teclado completo", que es lo que pide el documento, y además no había dónde enseñar
    // la elección: el acento marcaba la ventana ACTIVA del sistema, que es otra cosa.
    IntPtr _elegidaVista = IntPtr.Zero;

    int IndiceElegido()
    {
        for (int i = 0; i < _ordenVista.Count; i++) if (_ordenVista[i] == _elegidaVista) return i;
        return -1;
    }

    void Elegir(IntPtr h, bool porTeclado)
    {
        if (_elegidaVista == h) return;

        var antes = _elegidaVista;
        _elegidaVista = h;

        // Se repintan DOS tarjetas, la que deja de estar elegida y la nueva. No la rejilla:
        // rehacerla por mover el cursor es el bucle que hizo parpadear la vista en C4.1.
        if (antes != IntPtr.Zero && _tarjetas.TryGetValue(antes, out var vieja))
            PintarEleccion(vieja, vieja.Estado == EstadoVentana.Minimizada);
        if (_tarjetas.TryGetValue(h, out var nueva))
        {
            PintarEleccion(nueva, nueva.Estado == EstadoVentana.Minimizada);
            // Solo con el teclado: con el ratón ya estás mirando la tarjeta, y un
            // desplazamiento automático debajo del puntero es de las cosas que más
            // molestan de una lista.
            if (porTeclado) nueva.Boton.BringIntoView();
        }
    }

    // El nivel y su pinta los decide `HyprVista`, no este método: así "la elegida lleva
    // halo y la activa filo de acento" se puede comprobar sin abrir una ventana.
    void PintarEleccion(TarjetaViva t, bool minimizada)
    {
        bool elegida = t.Hwnd == _elegidaVista;
        t.Elegida = elegida;

        var pinta = HyprVista.PintaDe(HyprVista.NivelDe(t.Activa, elegida, minimizada));

        t.Halo.BorderBrush = pinta.Halo
            ? new SolidColorBrush(HyprPinta.Azul(_roles))
            : Brushes.Transparent;
        t.Marco.BorderBrush = pinta.FiloDeAcento ? _acento : Brushes.Transparent;
        t.Marco.Background = pinta.FiloDeAcento ? _elegido : _superficie;
        t.Titulo.FontWeight = pinta.Negrita ? FontWeights.SemiBold : FontWeights.Normal;

        // La atenuación va en el HUECO de la foto, no en el marco entero. Atenuar el marco
        // baja también el título y el subtítulo, y un 0,62 sobre el texto se cae por debajo
        // del contraste mínimo: la tarjeta de una ventana minimizada quedaría ilegible. Lo
        // que tiene que verse apagado es la imagen, no el nombre.
        t.Hueco.Opacity = pinta.Opacidad;

        // El estado en PALABRAS, para quien no ve el color. El documento lo pide así: el
        // color nunca puede ser el único portador de la información.
        System.Windows.Automation.AutomationProperties.SetName(
            t.Boton, t.TextoTitulo + (pinta.Estado.Length > 0 ? $" ({pinta.Estado})" : ""));
    }

    // ---------- El tamaño de una tarjeta ----------
    //
    // En un solo sitio, y tomado del reparto de ahora. Era `AnchoTarjeta - 2` y
    // `AltoTarjeta - 2` con dos constantes, así que una tarjeta medía lo mismo en un
    // portátil de 1366 que en un 4K: en el primero, cuatro columnas ocupaban casi toda la
    // pantalla; en el segundo, la cuadrícula era un sello en medio del monitor.
    //
    // El hueco entre tarjetas también sale de aquí, como margen de cada una: media
    // separación por lado, que es lo que deja el mismo aire entre dos tarjetas que entre
    // una tarjeta y el borde.
    void MedirTarjeta(TarjetaViva t)
    {
        if (t.Boton == null) return;
        double m = _reparto.Hueco / 2;
        t.Boton.Width = Math.Max(80, _reparto.AnchoTarjeta - _reparto.Hueco);
        t.Boton.Height = Math.Max(60, _reparto.AltoTarjeta - _reparto.Hueco);
        t.Boton.Margin = new Thickness(m);
    }

    // ---------- Mover la elección ----------
    void MoverEleccion(HyprVista.Mover a)
    {
        if (_ordenVista.Count == 0) return;
        // Del reparto de AHORA, no de una fórmula aparte: si el teclado creyera que hay
        // tres columnas y la rejilla tuviera cuatro, la flecha abajo saltaría a la tarjeta
        // equivocada. Eran dos cuentas distintas del mismo número.
        int columnas = Math.Max(1, _reparto.Columnas);
        int donde = HyprVista.Siguiente(IndiceElegido(), _ordenVista.Count, columnas, a);
        if (donde < 0 || donde >= _ordenVista.Count) return;
        Elegir(_ordenVista[donde], porTeclado: true);
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
            t.Titulo.Foreground = esActiva ? _acentoLegible : _texto;
            // El marco, el halo y la negrita los pone `PintarEleccion` desde `HyprVista`:
            // aquí se ponían a mano mirando solo `esActiva`, y eso borraba el anillo de
            // foco del teclado cada vez que cambiabas de aplicación por fuera.
            PintarEleccion(t, v.Estado == EstadoVentana.Minimizada);
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
            // D6: y el nivel de la tarjeta se recalcula. Sin esto, minimizar una ventana que
            // no es la activa no cambiaba su pinta: el nivel solo se repintaba cuando
            // cambiaba el foco, y "apagada" es un nivel que depende de minimizarse.
            PintarEleccion(t, esMin);
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

        // ---------- D6: al lado de la tarjeta, no encima ----------
        //
        // Antes el menú se ponía en la esquina de la tarjeta + 12 px y se recortaba para
        // no salirse del panel. Con catorce opciones el menú mide casi 500 px de alto, así
        // que lo que hacía era TAPAR la tarjeta: te quedabas eligiendo "mitad izquierda"
        // sin ver de qué ventana. Es justo lo que el documento no quiere.
        //
        // Ahora el sitio lo elige `HyprVista.DondeCabe`: de los cuatro lados de la tarjeta,
        // el que tenga hueco, y entre los que tengan, el que más. Si no cabe por ninguno
        // —una tarjeta grande en un panel pequeño— devuelve el mejor sitio y avisa de que
        // solapa; entonces se le baja un poco la opacidad, para que al menos se vea por
        // dónde va la tarjeta de debajo.
        var esquina = ancla.TranslatePoint(new Point(0, 0), _raizVista);
        var dela = new NubeHypr.Rect(esquina.X, esquina.Y, ancla.ActualWidth, ancla.ActualHeight);
        var area = new NubeHypr.Rect(0, 0, _raizVista.ActualWidth, _raizVista.ActualHeight);

        var sitio = HyprVista.DondeCabe(dela, area, ancho, alto);
        Canvas.SetLeft(tarjeta, Math.Max(2, sitio.Donde.X));
        Canvas.SetTop(tarjeta, Math.Max(2, sitio.Donde.Y));
        if (sitio.Solapa) tarjeta.Opacity = 0.96;

        Log.Info($"vista menú de 0x{h.ToInt64():X6}: {ancho:0}x{alto:0} al {sitio.Lado} " +
                 $"en {sitio.Donde.X:0},{sitio.Donde.Y:0}" + (sitio.Solapa ? " (no cabía: solapa)" : ""));
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
            ZonasAjuste.Ajustar(h, z, monitor, activar: true, estorbos: EstorbosDeAhora());
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

    // ---------- Los atajos de NubeHypr ----------
    //
    // Se llama desde el mismo sitio que el de la vista general, así que recargar la
    // configuración los rehace a los dos. `HyprTeclas.Registrar` suelta antes de registrar,
    // así que recargar veinte veces no deja veinte atajos.
    //
    // Con la capa APAGADA no se registra nada y se suelta lo que hubiera: quedarse con 19
    // atajos globales del usuario "por si acaso" sería lo contrario de apagada.
    void AplicarAtajosDeHypr()
    {
        if (_self == IntPtr.Zero || !_esJefa) return;   // un solo dueño, como el de la vista

        // ---------- El modo de demostración no coge teclas de nadie ----------
        //
        // Con la capa apagada ya no se registraría ninguno, y en demostración la capa va
        // apagada. Pero esto no se deja colgando de eso: son diecinueve combinaciones
        // globales del sistema, y «no se registran porque además está apagada» es un
        // razonamiento de dos pasos donde hace falta uno. Si mañana alguien cambia lo otro,
        // esto sigue en pie.
        if (DiagGuia.Encendido)
        {
            int sueltos = HyprTeclas.Quitar(_self);
            Log.Info($"[GUÍA] modo de demostración: no se registra ningún atajo global" +
                     (sueltos > 0 ? $" ({sueltos} soltados)" : ""));
            return;
        }

        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        if (modo == NubeHypr.Modo.Apagado)
        {
            int soltados = HyprTeclas.Quitar(_self);
            if (soltados > 0)
                Log.Info($"atajos de NubeHypr: la capa está apagada, {soltados} soltados");
            return;
        }

        var mapa = HyprTeclas.MapaDe(_cfg.NubeHypr, _cfg.Barra.AtajoVistaGeneral);
        HyprTeclas.Registrar(_self, mapa);
    }

    // ---------- D5.1: las reglas, enganchadas a que aparezca una ventana ----------
    //
    // Esta suscripción es APARTE de la de la vista general (`EscucharCambios`), y a
    // propósito: aquella solo escucha mientras la vista está abierta, porque solo entonces
    // hay rejilla que rehacer. Las reglas tienen que oír la ventana que aparece **aunque no
    // haya ningún panel abierto**, que es el caso normal.
    //
    // Y es la isla jefa la única que escucha: con dos monitores hay dos MainWindow, y sin
    // esto cada ventana nueva se resolvería dos veces y saldría dos veces en el log.
    bool _escuchandoReglas;

    void EscucharParaLasReglas(bool si)
    {
        if (si == _escuchandoReglas) return;
        _escuchandoReglas = si;
        if (si) GestorVentanas.Cambio += AlAparecerUnaVentana;
        else GestorVentanas.Cambio -= AlAparecerUnaVentana;
    }

    // Se engancha o se suelta según el modo. Con la capa apagada no se escucha nada: ni un
    // delegado colgado del evento.
    void AplicarReglasDeHypr()
    {
        if (!_esJefa) return;
        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        bool quiero = modo != NubeHypr.Modo.Apagado;
        if (quiero != _escuchandoReglas)
            Log.Info($"[D5]  reglas al aparecer una ventana: {(quiero ? "escuchando" : "sin escuchar")} " +
                     $"(modo={modo}, {_cfg.NubeHypr.Reglas.Count} regla(s))");
        EscucharParaLasReglas(quiero);
    }

    // El manejador.
    //
    // Hasta D9.5 aquí ponía «**aquí no se mueve ninguna ventana**: se observa, se decide,
    // se apunta y se termina; que el modo sea "aplicar" no cambia nada todavía». Era
    // verdad y era deliberado: conectar las reglas con la realidad y moverlas con ellas
    // habrían sido dos cambios en uno.
    //
    // Ya no lo es. Se observa, se decide, se apunta **y, si la capa está encendida y el
    // modo es "aplicar", se coloca** (ver `ColocarPorRegla`). Con la capa apagada o en
    // simulación —que es como viene de serie— sigue sin moverse nada.
    void AlAparecerUnaVentana(GestorVentanas.Cambios que, GestorVentanas.Motivo motivo)
    {
        // ---------- Dos avisos interesan, y por motivos distintos ----------
        //
        // La LISTA: una ventana ha nacido o ha muerto. Es cuando se resuelven las reglas y
        // cuando se mira si una aplicación vuelve a su sitio.
        //
        // Los SITIOS: una ventana se ha movido o cambiado de tamaño. Es cuando se detecta
        // tu gesto (D9.1). Esto NO estaba antes porque hasta D7 no hacía falta: resolver
        // reglas en cada cambio de sitio sería el camino al parpadeo que costó C4.1, y por
        // eso lo que se hace aquí es comparar rectángulos, no recalcular nada.
        //
        // Los títulos, los estados y el cambio de ventana activa siguen sin interesar.
        bool lista = (que & GestorVentanas.Cambios.Lista) != 0;
        bool sitios = (que & GestorVentanas.Cambios.Sitios) != 0;
        if (!lista && !sitios) return;

        try
        {
            var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);

            // La lista sale de `Observar`, no de `GestorVentanas.Todas` en crudo: así la
            // lista protegida de D0 (el shell, los diálogos del sistema, las nuestras)
            // filtra también aquí. Resolver reglas sobre la propia NubeBar no tendría
            // sentido, y sobre un cuadro de diálogo del sistema sería peor.
            var obs = HyprAdaptador.Observar();

            // La clase y la ruta no están en el modelo puro (a propósito: el modelo no sabe
            // que existe Win32), pero las reglas las usan como criterio. La ruta se saca de
            // la instantánea del gestor, que ya la tiene leída.
            var porHwnd = new Dictionary<IntPtr, string>();
            foreach (var w in GestorVentanas.Todas) porHwnd[w.Hwnd] = w.Exe;

            string Clase(IntPtr h) => Nativo.Clase(h);
            string Ruta(IntPtr h) => porHwnd.TryGetValue(h, out var r) ? r : "";

            // Las reglas, solo cuando la lista cambia: una ventana que se mueve no es una
            // ventana nueva, y resolverlas en cada movimiento sería recalcular cuarenta
            // veces por segundo mientras arrastras.
            if (lista)
            {
                var apuntes = HyprVigilante.AlCambiarLaLista(
                    modo, obs.Estado.Ventanas, _cfg.NubeHypr.Reglas, Clase, Ruta);

                // Con el modo: es el sitio donde de verdad importa, porque estas líneas son
                // las que uno lee para saber si algo se movió — y llevaban desde D9.5
                // diciendo «simulacion» con la capa aplicando.
                foreach (var a in apuntes)
                    foreach (var linea in HyprVigilante.Lineas(a, modo))
                        Log.Info(linea);

                // D9.5: y ahora, además de apuntarlo, COLOCARLA.
                //
                // Hasta aquí las reglas resolvían y no movían nada, a propósito: «conectarlo
                // y aplicarlo a la vez sería meter dos cambios en uno». Esto es la otra
                // mitad, y es lo que hace verdad la frase con la que se abre la guía de
                // reglas —«cuando aparezca Discord, enviarlo al Escritorio 2»—, que llevaba
                // desde D9.4 sin código detrás que la cumpliera.
                foreach (var a in apuntes) ColocarPorRegla(a, obs, modo);
            }

            // Y en el mismo aviso, con la misma observación: las que se van, las que
            // vuelven (D7) y las que has movido tú (D9.1). Tres pasadas sobre las mismas
            // ventanas sería pagar tres veces por lo mismo. Nada de esto mueve nada: decide
            // y lo apunta, igual que las reglas.
            MirarLasVentanas(obs.Estado.Ventanas, modo, lista, sitios, Clase, Ruta);
        }
        catch (Exception ex)
        {
            // D7: una excepción aquí se CUENTA, además de apuntarse. Es una de las cuatro
            // señales del supervisor, y sin contarla "el adaptador revienta en cada ventana
            // nueva" sería un montón de líneas en el registro y ninguna consecuencia.
            HyprSalud.Reventó();
            Log.Error("resolver las reglas de una ventana nueva", ex);
        }
    }

    // ================= --diagatajos =================
    //
    // Registra de verdad, recoge los códigos de Windows, deja los atajos vivos el rato que
    // se pida para poder pulsarlos, y los suelta. Ver DiagAtajos.cs.
    async System.Threading.Tasks.Task CorrerDiagnosticoDeAtajos()
    {
        try
        {
            Apunta("=== atajos de NubeHypr (D3) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            await System.Threading.Tasks.Task.Delay(2500);

            // ---------- El de la vista general, primero ----------
            //
            // Es el que no se puede perder. Se comprueba que está registrado ANTES de
            // tocar nada: si se rompiera, hay que saberlo aquí y no al final.
            Apunta($"[D3]  atajo de la vista general: «{_cfg.Barra.AtajoVistaGeneral}» " +
                   $"traducible={(Nativo.TraducirAtajo(_cfg.Barra.AtajoVistaGeneral ?? "", out _, out _) ? "sí" : "NO")}");

            // La capa se enciende SOLO en memoria, y en simulación
            _cfg.NubeHypr.Activo = true;
            _cfg.NubeHypr.Modo = "simulacion";
            Apunta($"[D3]  capa encendida solo en memoria, modo=simulacion " +
                   $"(la configuración NO se escribe: SoloLectura={Config.SoloLectura})");

            // ---------- El mapa ----------
            var mapa = HyprTeclas.MapaDe(_cfg.NubeHypr, _cfg.Barra.AtajoVistaGeneral);
            Apunta($"[D3]  mapa: {mapa.Atajos.Count} atajos, {mapa.Problemas.Count} problema(s)");
            foreach (var p in mapa.Problemas) Apunta("  problema: " + p);
            foreach (var (c, n) in mapa.Atajos) Apunta($"  {c,-22} -> {n}");

            // ---------- Registrar de verdad ----------
            var hechos = HyprTeclas.Registrar(_self, mapa);
            int ok = hechos.Count(h => h.Ok);
            int ocupados = hechos.Count(h => h.Como == HyprTeclas.Como.YaLoTieneOtro);
            int raros = hechos.Count(h => h.Como is HyprTeclas.Como.NoSeEntiende or HyprTeclas.Como.Error);
            Apunta($"[D3]  registrados={ok} ocupados(1409)={ocupados} otros={raros} de {hechos.Count}");
            foreach (var h in hechos) Apunta($"  id=0x{h.Id:X4}  {h}");
            Apunta($"[D3]  vivos según el registro: {HyprTeclas.Cuantos}");

            // ---------- Registrar OTRA VEZ: no se duplica ----------
            //
            // Es el caso de recargar la configuración. Si `Registrar` no soltara antes,
            // aquí saldrían errores y quedarían atajos vivos que ya no están en el mapa.
            var otraVez = HyprTeclas.Registrar(_self, mapa);
            int ok2 = otraVez.Count(h => h.Ok);
            Apunta($"[D3]  recargando: registrados={ok2} (antes {ok}), vivos={HyprTeclas.Cuantos}");
            Apunta($"[D3]  ¿se duplicó algo?={(HyprTeclas.Cuantos == ok2 ? "no" : "SÍ, MAL")}");

            // ---------- Vivos un rato, para poder pulsarlos ----------
            Apunta($"[D3]  atajos vivos {DiagAtajos.Segundos} s. Prueba Win+Alt+flechas y Ctrl+Alt+W; " +
                   "cada pulsación sale en el log como «[D3] atajo ...»");
            for (int s = 5; s <= DiagAtajos.Segundos; s += 5)
            {
                await System.Threading.Tasks.Task.Delay(5000);
                Apunta($"  {s} s: vivos={HyprTeclas.Cuantos}");
            }


            // ---------- Comprobación de humo del editor de Ajustes (D5) ----------
            //
            // El código de la página solo corre cuando se abre Ajustes y se entra en esa
            // pestaña, así que una excepción ahí no la ve nadie hasta que el usuario pulsa.
            // Esto la construye, cuenta lo que salió y la cierra: no es "se ve bien", es
            // "no revienta y tiene filas".
            try
            {
                var ajustes = new VentanaAjustes();
                ajustes.Opacity = 0;            // se construye sin dar un fogonazo en pantalla
                ajustes.ShowInTaskbar = false;
                ajustes.Show();
                await System.Threading.Tasks.Task.Delay(1200);
                ajustes.Ir("atajos");
                await System.Threading.Tasks.Task.Delay(1200);

                int campos = ContarDescendientes<TextBox>(ajustes);
                int botones = ContarDescendientes<Button>(ajustes);
                Apunta($"[D5]  editor de atajos: la página se construyó. " +
                       $"campos de texto={campos} botones={botones}");
                Apunta($"[D5]  ¿hay una fila por comando?=" +
                       $"{(campos >= HyprAtajos.Nombres.Length ? "sí" : $"NO: {campos} < {HyprAtajos.Nombres.Length}")}");
                ajustes.Close();
                await System.Threading.Tasks.Task.Delay(400);
                Apunta("[D5]  Ajustes cerrada");
            }
            catch (Exception ex)
            {
                Apunta($"[D5]  el editor de atajos REVENTÓ: {ex.GetType().Name}: {ex.Message}");
                Log.Error("construir el editor de atajos", ex);
            }

            // ---------- Soltarlos todos ----------
            int soltados = HyprTeclas.Quitar(_self);
            Apunta($"[D3]  soltados={soltados}  vivos ahora={HyprTeclas.Cuantos}");
            Apunta($"[D3]  ¿queda alguno?={(HyprTeclas.Cuantos == 0 ? "no" : "SÍ, MAL")}");

            // Soltar otra vez no falla y no suelta nada: es idempotente, que es lo que
            // permite ponerlo en el cierre sin preguntar si hacía falta.
            Apunta($"[D3]  soltar otra vez devuelve {HyprTeclas.Quitar(_self)} (tiene que ser 0)");

            // ---------- Y volver a registrar después de soltar ----------
            var tercera = HyprTeclas.Registrar(_self, mapa);
            Apunta($"[D3]  tras soltar, vuelve a registrar: {tercera.Count(h => h.Ok)} " +
                   $"(tiene que ser {ok})");
            HyprTeclas.Quitar(_self);
            Apunta($"[D3]  limpio al salir: vivos={HyprTeclas.Cuantos}");
        }
        catch (Exception ex) { Log.Error("el diagnóstico de atajos", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    // ================= --arnesespacios =================
    //
    // Cierra D4 probando renombrar, reordenar y enviar, sin tocar nada del usuario. Ver
    // ArnesEspacios.cs para las reglas.
    async System.Threading.Tasks.Task CorrerArnesDeEspacios()
    {
        // Lo que hay que limpiar, declarado FUERA del try: el `finally` tiene que poder
        // limpiar lo que se creara, incluso si la excepción saltó justo después de crearlo.
        Window? ventana = null;
        IntPtr hwnd = IntPtr.Zero;
        Guid dePrueba = Guid.Empty;
        Guid elOriginal = Guid.Empty;
        var ordenDelUsuarioAntes = new List<Guid>();
        bool limpiezaOk = true;

        try
        {
            Apunta("=== arnés de escritorios (D4) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta("sin inyección de teclado ni de ratón, y sin tocar ninguna ventana tuya");
            await System.Threading.Tasks.Task.Delay(3000);

            if (!Escritorios.Disponible)
            {
                Apunta("[D4]  la API no está disponible: no hay nada que probar, y eso NO es un fallo");
                return;
            }

            // ---------- 1. Guardar el estado de antes ----------
            var antes = Escritorios.Espacios();
            int iActual = HyprEspacios.IndiceDelActual(antes);
            elOriginal = iActual >= 0 ? antes[iActual].Id : Guid.Empty;
            ordenDelUsuarioAntes = antes.Select(e => e.Id).ToList();

            Apunta($"[D4]  ANTES: {antes.Count} escritorio(s)");
            foreach (var e in antes) Apunta($"  · {e}  id={e.Id}");
            Apunta($"[D4]  el tuyo ahora mismo: {(elOriginal == Guid.Empty ? "no se sabe" : elOriginal.ToString())}");

            // ---------- 2. Crear el de prueba ----------
            //
            // Crear NO cambia de escritorio: te quedas donde estabas. Se comprueba justo
            // después, porque si esa suposición fuera falsa el arnés te habría movido.
            dePrueba = Escritorios.CrearYDevolver();
            if (dePrueba == Guid.Empty) { Apunta("[D4]  no se pudo crear: se para aquí"); return; }

            var tras = Escritorios.Espacios();
            var sigoAqui = tras.FirstOrDefault(e => e.EsElActual).Id;
            Apunta($"[D4]  creado  id={dePrueba}  escritorios={tras.Count}");
            Apunta($"[D4]  ¿crear me movió de escritorio?={(sigoAqui == elOriginal ? "no" : "SÍ")}");

            var elNuevo = tras.FirstOrDefault(e => e.Id == dePrueba);
            Apunta($"[D4]  el de prueba nace en la posición {elNuevo.Orden} con el nombre «{elNuevo.Nombre}»");

            // ---------- 3. Renombrar ----------
            var nombre = ArnesEspacios.NombreDePrueba;
            bool renombrado = Escritorios.Renombrar(dePrueba, nombre);
            await System.Threading.Tasks.Task.Delay(400);
            var trasNombre = Escritorios.Espacios().FirstOrDefault(e => e.Id == dePrueba);
            bool nombreLlego = trasNombre.Nombre == nombre;
            Apunta($"[D4]  RENOMBRAR -> «{nombre}»: llamada={(renombrado ? "ok" : "FALLÓ")}, " +
                   $"leído=«{trasNombre.Nombre}», ¿llegó?={(nombreLlego ? "sí" : "NO")}");

            // ---------- 4. Reordenar ----------
            //
            // Se mueve el de prueba una posición hacia arriba y después se devuelve al
            // final. Mientras está arriba, el último de los tuyos baja un puesto: es
            // transitorio, y al terminar se comprueba que la secuencia de los tuyos es
            // exactamente la de antes.
            int destino = Math.Max(0, Escritorios.Espacios().Count - 2);
            bool movido = Escritorios.Ordenar(dePrueba, destino);
            await System.Threading.Tasks.Task.Delay(400);
            var trasOrden = Escritorios.Espacios().FirstOrDefault(e => e.Id == dePrueba);
            bool ordenLlego = trasOrden.Orden == destino;
            Apunta($"[D4]  REORDENAR -> posición {destino}: llamada={(movido ? "ok" : "FALLÓ")}, " +
                   $"leída={trasOrden.Orden}, ¿llegó?={(ordenLlego ? "sí" : "NO")}");

            // Y de vuelta al final, para no dejar tus escritorios reordenados
            int alFinal = Escritorios.Espacios().Count - 1;
            Escritorios.Ordenar(dePrueba, alFinal);
            await System.Threading.Tasks.Task.Delay(400);
            Apunta($"[D4]  devuelto al final (posición {alFinal})");

            // ---------- 5. Una ventana PROPIA del arnés ----------
            //
            // No se usa ninguna ventana tuya, ni se enumeran. Esta la crea el arnés, la
            // usa y la cierra.
            ventana = new Window
            {
                Title = "NubeHypr: ventana de prueba del arnés",
                Width = 320, Height = 200,
                Left = 80, Top = 80,
                ShowInTaskbar = false,
                Topmost = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = new TextBlock
                {
                    Text = "Ventana temporal del arnés de NubeHypr.\nSe cierra sola.",
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            ventana.Show();
            await System.Threading.Tasks.Task.Delay(700);
            hwnd = new System.Windows.Interop.WindowInteropHelper(ventana).Handle;
            var dondeNace = Escritorios.EspacioDe(hwnd);
            Apunta($"[D4]  ventana temporal creada: hwnd=0x{hwnd.ToInt64():X6} " +
                   $"«{ventana.Title}», nace en {dondeNace}");

            // ---------- 6. Enviarla al de prueba ----------
            bool enviada = Escritorios.Enviar(hwnd, dePrueba);
            await System.Threading.Tasks.Task.Delay(800);
            var dondeEsta = Escritorios.EspacioDe(hwnd);
            bool llego = dondeEsta == dePrueba;
            Apunta($"[D4]  ENVIAR -> {dePrueba}: llamada={(enviada ? "ok" : "FALLÓ")}, " +
                   $"leído={dondeEsta}, ¿llegó?={(llego ? "sí" : "NO")}");

            // Y se comprueba con la otra vía, la que usa el carrusel: si la ventana se fue
            // a otro escritorio, ya no está en el actual.
            bool sigueEnElMio = Escritorios.EstaEnElActual(hwnd);
            Apunta($"[D4]  comprobación cruzada: ¿sigue en tu escritorio?=" +
                   $"{(sigueEnElMio ? "sí (no se movió)" : "no (se fue, como se pidió)")}");

            // ---------- 7. Y el escritorio actual no se movió en ningún momento ----------
            var sigoDonde = Escritorios.Espacios().FirstOrDefault(e => e.EsElActual).Id;
            Apunta($"[D4]  ¿sigo en mi escritorio de siempre?={(sigoDonde == elOriginal ? "sí" : "NO, ATENCIÓN")}");
        }
        catch (Exception ex)
        {
            Apunta($"[D4]  EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el arnés de espacios", ex);
        }
        finally
        {
            // ================= La limpieza =================
            //
            // En `finally` y en este orden: primero la ventana, después el escritorio.
            // Borrar un escritorio con ventanas dentro las manda a otro, y entonces la
            // limpieza dejaría una ventana suelta en un escritorio tuyo.
            Apunta("--- limpieza ---");

            try
            {
                if (ventana != null)
                {
                    ventana.Close();
                    await System.Threading.Tasks.Task.Delay(500);
                    bool sigue = hwnd != IntPtr.Zero && Nativo.Existe(hwnd);
                    Apunta($"[D4]  ventana temporal cerrada: ¿sigue existiendo?={(sigue ? "SÍ, MAL" : "no")}");
                    if (sigue) limpiezaOk = false;
                }
            }
            catch (Exception ex)
            {
                limpiezaOk = false;
                Apunta($"[D4]  no se pudo cerrar la ventana temporal: {ex.GetType().Name}");
            }

            try
            {
                if (dePrueba != Guid.Empty)
                {
                    bool borrado = Escritorios.Borrar(dePrueba);
                    await System.Threading.Tasks.Task.Delay(600);
                    bool sigue = Escritorios.Espacios().Any(e => e.Id == dePrueba);
                    Apunta($"[D4]  escritorio de prueba borrado: llamada={(borrado ? "ok" : "FALLÓ")}, " +
                           $"¿sigue en la lista?={(sigue ? "SÍ, MAL" : "no")}");
                    if (sigue || !borrado) limpiezaOk = false;
                }
            }
            catch (Exception ex)
            {
                limpiezaOk = false;
                Apunta($"[D4]  no se pudo borrar el escritorio de prueba: {ex.GetType().Name}");
            }

            // ---------- Si algo quedó torcido: volver al escritorio original ----------
            try
            {
                var ahora = Escritorios.Espacios();
                var dondeEstoy = ahora.FirstOrDefault(e => e.EsElActual).Id;

                if (elOriginal != Guid.Empty && dondeEstoy != elOriginal)
                {
                    Apunta($"[D4]  estoy en otro escritorio: se vuelve a {elOriginal}");
                    Escritorios.Ir(elOriginal);
                    await System.Threading.Tasks.Task.Delay(700);
                    ahora = Escritorios.Espacios();
                    dondeEstoy = ahora.FirstOrDefault(e => e.EsElActual).Id;
                }

                // La garantía que de verdad importa: la secuencia de TUS escritorios,
                // exactamente la de antes. No "el mismo número": los mismos, en el mismo
                // orden.
                var ordenAhora = ahora.Select(e => e.Id).Where(id => id != dePrueba).ToList();
                bool mismoOrden = ordenAhora.SequenceEqual(ordenDelUsuarioAntes);

                Apunta($"[D4]  DESPUÉS: {ahora.Count} escritorio(s)");
                foreach (var e in ahora) Apunta($"  · {e}  id={e.Id}");
                Apunta($"[D4]  ¿tus escritorios, los mismos y en el mismo orden?={(mismoOrden ? "sí" : "NO, ATENCIÓN")}");
                Apunta($"[D4]  ¿estoy en tu escritorio de siempre?={(dondeEstoy == elOriginal ? "sí" : "NO, ATENCIÓN")}");
                Apunta($"[D4]  limpieza={(limpiezaOk && mismoOrden ? "COMPLETA" : "INCOMPLETA: míralo")}");
            }
            catch (Exception ex)
            {
                Apunta($"[D4]  la comprobación final falló: {ex.GetType().Name}");
                Log.Error("la comprobación final del arnés", ex);
            }

            Application.Current.Shutdown(0);
        }
    }

    // Cuenta los descendientes de un tipo en el árbol visual. Solo para la comprobación
    // de humo: "la página se construyó y tiene filas" no se puede afirmar sin contar algo.
    static int ContarDescendientes<T>(DependencyObject raiz) where T : DependencyObject
    {
        int n = 0;
        var pila = new Stack<DependencyObject>();
        pila.Push(raiz);
        int tope = 0;
        while (pila.Count > 0 && tope++ < 20000)
        {
            var d = pila.Pop();
            if (d is T) n++;
            int hijos = 0;
            try { hijos = VisualTreeHelper.GetChildrenCount(d); } catch { }
            for (int i = 0; i < hijos; i++)
            {
                DependencyObject? h = null;
                try { h = VisualTreeHelper.GetChild(d, i); } catch { }
                if (h != null) pila.Push(h);
            }
        }
        return n;
    }

    // ================= --diagreglas =================
    //
    // Las reglas de D5 contra las ventanas de verdad, **sin mover ninguna**. Ver
    // DiagReglas.cs para lo que este diagnóstico se compromete a no hacer.
    async System.Threading.Tasks.Task CorrerDiagnosticoDeReglas()
    {
        try
        {
            Apunta("=== reglas de NubeHypr (D5.1) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta("este diagnóstico NO llama a SetWindowPos: solo resuelve y apunta");
            await System.Threading.Tasks.Task.Delay(2500);

            // ---------- Las reglas que se van a usar ----------
            //
            // Las tuyas si tienes alguna. Si no —y lo normal es que no, porque la lista
            // nace vacía— un juego de ejemplo **solo en memoria**: un diagnóstico de
            // reglas sin reglas no mide nada, y escribir reglas en tu configuración para
            // poder medirlas sería exactamente lo que no se debe hacer.
            var tuyas = HyprReglas.Instantanea(_cfg.NubeHypr.Reglas);
            bool deEjemplo = tuyas.Count == 0;
            var reglas = deEjemplo ? ReglasDeEjemplo() : tuyas;

            Apunta($"[D5]  reglas: {reglas.Count} ({(deEjemplo ? "DE EJEMPLO, solo en memoria: tu configuración no tiene ninguna" : "las de tu configuración")})");
            Apunta($"[D5]  configuración en solo lectura={Config.SoloLectura}");
            foreach (var r in HyprReglas.Ordenar(reglas))
            {
                bool ok = HyprReglas.Valida(r, out string porque);
                Apunta($"  {r}{(ok ? "" : $"   <- NO VÁLIDA: {porque}")}");
            }

            // ---------- Y el modo ----------
            //
            // Se enciende SOLO en memoria y en simulación, igual que en --diagatajos: para
            // medir hace falta que la capa esté encendida, y encenderla en el fichero sería
            // dejarte la capa encendida después de un diagnóstico.
            bool activoAntes = _cfg.NubeHypr.Activo;
            string modoAntes = _cfg.NubeHypr.Modo;
            _cfg.NubeHypr.Activo = true;
            _cfg.NubeHypr.Modo = "simulacion";
            Apunta($"[D5]  capa encendida solo en memoria (antes: activo={activoAntes}, modo={modoAntes})");

            // ---------- Pasada 1: las ventanas que ya hay ----------
            HyprVigilante.Olvidar();
            var obs = HyprAdaptador.Observar();
            Apunta($"[D5]  ventanas organizables ahora: {obs.Cuantas} " +
                   $"(descartadas por la lista protegida: {obs.Descartadas.Count})");
            foreach (var (h, app, porque) in obs.Descartadas)
                Apunta($"  fuera 0x{h.ToInt64():X6} {app}: {porque}");

            var porHwnd = new Dictionary<IntPtr, string>();
            foreach (var w in GestorVentanas.Todas) porHwnd[w.Hwnd] = w.Exe;
            string Ruta(IntPtr h) => porHwnd.TryGetValue(h, out var r) ? r : "";

            Apunta("--- resolviendo las que ya están abiertas ---");
            int conRegla = 0, sinRegla = 0;
            foreach (var v in obs.Estado.Ventanas)
            {
                var a = HyprVigilante.Examinar(v, reglas, Nativo.Clase(v.Hwnd), Ruta(v.Hwnd));
                if (a.AlgunaCoincidio) conRegla++; else sinRegla++;
                foreach (var linea in HyprVigilante.Lineas(a)) Apunta(linea);
            }
            Apunta($"[D5]  con alguna regla={conRegla}  sin ninguna={sinRegla}");

            // ---------- Idempotencia, medida ----------
            //
            // Resolver dos veces la misma ventana tiene que dar lo mismo. Si no lo diera,
            // aplicar sería impredecible: la ventana acabaría donde la pusiera el azar del
            // orden de evaluación.
            if (obs.Estado.Ventanas.Count > 0)
            {
                var v = obs.Estado.Ventanas[0];
                var a1 = HyprVigilante.Examinar(v, reglas, Nativo.Clase(v.Hwnd), Ruta(v.Hwnd));
                var a2 = HyprVigilante.Examinar(v, reglas, Nativo.Clase(v.Hwnd), Ruta(v.Hwnd));
                bool igual = a1.Decision.ToString() == a2.Decision.ToString() &&
                             a1.CamposPuestos.SequenceEqual(a2.CamposPuestos);
                Apunta($"[D5]  idempotencia sobre 0x{v.Hwnd.ToInt64():X6}: " +
                       $"{(igual ? "la misma decisión las dos veces" : "DISTINTA, MAL")}");
            }

            // ---------- Pasada 2: la conexión de verdad ----------
            //
            // Ahora se engancha al evento y se espera: abre una aplicación durante este
            // rato y su línea «[D5] ventana nueva» tiene que salir una sola vez.
            Apunta($"--- escuchando ventanas nuevas {DiagReglas.Segundos} s ---");
            Apunta("abre una aplicación: su decisión sale aquí, y NO se moverá");
            // Las reglas que use el manejador son las de la configuración EN MEMORIA, que
            // es como tiene que ser en producción. Pero si son las de ejemplo —porque tu
            // configuración no tiene ninguna— hay que ponerlas también ahí, o la fase de
            // escucha contaría cada ventana nueva como "sin reglas" y no mediría nada. En
            // memoria y se devuelven después: el fichero no se toca (SoloLectura).
            var reglasEnMemoria = _cfg.NubeHypr.Reglas;
            if (deEjemplo) _cfg.NubeHypr.Reglas = reglas;

            HyprVigilante.Olvidar();
            EscucharParaLasReglas(true);

            for (int s = 5; s <= DiagReglas.Segundos; s += 5)
            {
                await System.Threading.Tasks.Task.Delay(5000);
                Apunta($"  {s} s: vistas={HyprVigilante.Vistas} resueltas={HyprVigilante.Resueltas} " +
                       $"sinReglas={HyprVigilante.SinReglas}");
            }

            EscucharParaLasReglas(false);
            _cfg.NubeHypr.Reglas = reglasEnMemoria;
            Apunta($"[D5]  desenganchado. resueltas en total={HyprVigilante.Resueltas}");

            // ---------- Y la prueba de que no se movió nada ----------
            //
            // Los contadores del adaptador: si `--diagreglas` hubiera movido una ventana,
            // aquí habría aplicaciones contadas. Cero es la afirmación, no la esperanza.
            Apunta($"[D5]  movimientos aplicados por este diagnóstico: {HyprAdaptador.VecesMovida} " +
                   $"(tiene que ser 0)");

            _cfg.NubeHypr.Activo = activoAntes;
            _cfg.NubeHypr.Modo = modoAntes;
            Apunta($"[D5]  modo devuelto a activo={activoAntes}, modo={modoAntes} (en memoria; el fichero no se tocó)");
        }
        catch (Exception ex) { Log.Error("el diagnóstico de reglas", ex); }
        finally { Application.Current.Shutdown(0); }
    }

    // Un juego de reglas de ejemplo, para cuando la configuración no tiene ninguna. Están
    // aquí y no en el fichero del usuario a propósito: solo viven en memoria durante el
    // diagnóstico. Cubren los casos que pide D5.1 —prioridad, empate, varias reglas que
    // aportan campos distintos— contra procesos que es probable que estén abiertos.
    static List<HyprReglas.Regla> ReglasDeEjemplo() => new()
    {
        new HyprReglas.Regla("ejemplo-navegador", Prioridad: 10, Proceso: "chrome",
                             Espacio: 2, Layout: NubeHypr.Layout.Principal),
        new HyprReglas.Regla("ejemplo-navegador-grande", Prioridad: 5, Proceso: "chrome",
                             Ancho: 0.7, Alto: 0.9),
        new HyprReglas.Regla("ejemplo-explorador", Proceso: "explorer", Flotante: true),
        new HyprReglas.Regla("ejemplo-ajustes-por-titulo", Titulo: "Ajustes", Foco: true),
        new HyprReglas.Regla("ejemplo-por-ruta", Ruta: "Windows", Monitor: 1),
    };
}
