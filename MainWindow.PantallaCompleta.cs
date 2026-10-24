using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// Esconderse cuando algo ocupa la pantalla entera (un vídeo, un juego, F11 en el
// navegador). Se esconde SOLO la isla de esa pantalla; la de la otra sigue a lo suyo.
public partial class MainWindow : Window
{
    bool _recogida;   // el monitor tiene algo a pantalla completa
    bool _asomada;                // está asomando porque tienes el ratón en el borde
    DateTime _ratonEnElBorde = DateTime.MaxValue;
    TranslateTransform? _mover;

    static DispatcherTimer? _vigia;

    // Escondida con el clic derecho. Es de todas las islas a la vez y solo la tecla
    // Windows la devuelve: ni la pantalla completa ni el ratón en el borde la tocan.
    static bool _escondidaAMano;

    public static void EsconderAMano(bool esconder)
    {
        if (_escondidaAMano == esconder) return;
        _escondidaAMano = esconder;
        foreach (var i in Islas.ToList())
        {
            if (esconder) i.CerrarPanel();
            i._asomada = false;
            i._ratonEnElBorde = DateTime.MaxValue;
            // Al volver, si su pantalla sigue con algo a pantalla completa, se queda
            // fuera: el siguiente repaso la deja asomar como siempre con el ratón
            i.Deslizar(esconder || i._recogida);
        }
        Log.Info(esconder ? "isla escondida a mano (vuelve con Win)" : "isla de vuelta con la tecla Windows");
    }

    const double MsAsomar = 300;   // cuánto hay que aguantar el ratón arriba para que asome

    // Un solo temporizador para todas las islas: lo monta la jefa y revisa a todas.
    // 150 ms es suficiente para que el borde responda al instante y no se nota en la CPU.
    void VigilarPantallaCompleta()
    {
        if (!_esJefa || _vigia != null) return;
        _vigia = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _vigia.Tick += (_, _) =>
        {
            var jefa = Islas.FirstOrDefault(i => i._esJefa);
            jefa?.VigilarVolumen();
            jefa?.VigilarBarraDeWindows();
            foreach (var i in Islas.ToList())
            {
                try { i.VigilarClicFueraDelMenu(); i.VigilarClicFueraDelPanel(); }
                catch (Exception ex) { Log.Error("clic fuera del menú", ex); }
                try { i.RevisarPantallaCompleta(); }
                catch (Exception ex) { Log.Error("pantalla completa", ex); }
            }
        };
        _vigia.Start();
    }

    // Explorer vuelve a mostrar su barra por su cuenta en varios casos: al hacer
    // una captura de pantalla, al reiniciarse, al cambiar de pantalla completa.
    // Antes esto se miraba una vez por segundo y se veía el parpadeo; aquí se
    // mira cada 150 ms y no da tiempo a verla.
    void VigilarBarraDeWindows()
    {
        if (_cerrando || !_cfg.Barra.OcultarBarraWindows) return;
        try
        {
            var mias = MonitoresConIsla();
            if (Nativo.BarraWindowsVisible(mias)) Nativo.EsconderBarraWindows(mias);
        }
        catch { }
    }

    // ---------- ¿Hay algo a pantalla completa en MI monitor? ----------
    // "Maximizada" no cuenta: una ventana maximizada deja ver la isla y el borde de
    // Windows. Solo cuenta la que tapa el monitor entero, de verdad.
    static readonly string[] ClasesDelEscritorio =
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "SHELLDLL_DefView", "SysListView32", "Windows.UI.Core.CoreWindow"
    };

    bool HayPantallaCompleta()
    {
        var h = Nativo.Activa();
        if (h == IntPtr.Zero) return false;
        if (Nativo.EsNuestra(h)) return false;                 // nuestros propios paneles, no

        var clase = Nativo.Clase(h);
        if (ClasesDelEscritorio.Contains(clase)) return false; // el escritorio y las barras, tampoco

        // Wallpaper Engine pinta un "fondo" que ocupa toda la pantalla y que además
        // roba el foco cada pocos segundos: si contara, la isla viviría escondida.
        var exe = Nativo.Exe(h);
        if (exe.Length > 0 &&
            Path.GetFileNameWithoutExtension(exe).StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Monitores.HandleDe(h) != _monitor.Handle) return false;   // está en la otra pantalla

        // Maximizada NO es pantalla completa, y distinguirlas tiene truco. Lo que NO
        // sirve es mirar si Windows la marca como maximizada: al pulsar F11 en un
        // navegador que ya estaba maximizado, sigue marcada como maximizada y aun así
        // está a pantalla completa (medido con Edge). Lo que sí vale es el rectángulo
        // clavado: una ventana maximizada sobresale unos 8 px del monitor por cada
        // lado (son los bordes invisibles de redimensionar) y la de pantalla completa
        // coincide exactamente con el monitor.
        var r = Nativo.Rectangulo(h);
        if (Math.Abs(r.Izq - _monitor.X) <= 1 && Math.Abs(r.Arr - _monitor.Y) <= 1 &&
            Math.Abs(r.Der - _monitor.Derecha) <= 1 && Math.Abs(r.Aba - _monitor.Abajo) <= 1)
            return true;

        // Maximizada: tapa el área de trabajo entera (la pantalla menos lo que
        // reservamos nosotros y menos la barra de Windows si está). Por defecto no
        // cuenta, porque entonces la isla se iría cada vez que maximizas cualquier
        // cosa; con Barra.OcultarConMaximizada sí, para los juegos y los vídeos.
        // Se mira el rectángulo y no "¿Windows la marca como maximizada?" porque una
        // ventana puede estar estirada a mano hasta ocupar lo mismo.
        if (!_cfg.Barra.OcultarConMaximizada) return false;
        return r.Izq <= _monitor.TrabajoX + 2 && r.Arr <= _monitor.TrabajoY + 2 &&
               r.Der >= _monitor.TrabajoX + _monitor.TrabajoAncho - 2 &&
               r.Aba >= _monitor.TrabajoY + _monitor.TrabajoAlto - 2;
    }


    // ¿Está el ratón dentro de la franja donde vive la isla? Se mide con el GROSOR de
    // la barra, no con ActualWidth/Height: con un panel abierto la ventana es mucho
    // más grande y la franja saldría enorme.
    //
    // En vertical la franja es una columna pegada a su lado, no una fila: es la misma
    // cuenta con los dos ejes cambiados de sitio.
    bool RatonEnLaFranja()
    {
        var p = Monitores.Raton();
        double grosorDip = _cfg.Barra.Alto + _cfg.Barra.Margen * 2;
        int franja = (int)Math.Round(grosorDip * _monitor.Escala);

        if (Vertical)
            return p.Y >= _monitor.Y && p.Y < _monitor.Abajo &&
                   (PegadaIzquierda ? p.X < _monitor.X + franja : p.X > _monitor.Derecha - franja);

        return p.X >= _monitor.X && p.X < _monitor.Derecha &&
               (Arriba ? p.Y < _monitor.Y + franja : p.Y > _monitor.Abajo - franja);
    }

    // El filo de la pantalla por el que la barra asoma: los 2-3 px donde hay que dejar
    // el ratón para que vuelva. Mismo criterio, con el eje que toque.
    bool RatonEnElFilo()
    {
        var p = Monitores.Raton();
        if (Vertical)
            return p.Y >= _monitor.Y && p.Y < _monitor.Abajo &&
                   (PegadaIzquierda ? p.X <= _monitor.X + 2 : p.X >= _monitor.Derecha - 3);

        return p.X >= _monitor.X && p.X < _monitor.Derecha &&
               (Arriba ? p.Y <= _monitor.Y + 2 : p.Y >= _monitor.Abajo - 3);
    }

    // Dos motivos distintos para recogerse, y el mismo deslizamiento para los dos:
    //
    //   - el panel está en visibilidad "autoocultar": se recoge SIEMPRE, y asoma
    //     cuando pegas el ratón al borde (es lo que hace el panel de Plasma);
    //   - hay algo a pantalla completa y el config lo pide (lo de siempre).
    //
    // Lo de abajo no distingue cuál de los dos ha sido: le basta saber si toca estar
    // recogida. Así el asomar por el borde, el no esconderse con un panel abierto y
    // el quedarse asomada si tienes el ratón encima valen igual para el modo nuevo,
    // sin repetir ni una línea.
    // La regla está en PanelNube.TocaRecogerse (sin ventana, y por tanto probable);
    // aquí solo se le dan los tres datos. HayPantallaCompleta se consulta al final a
    // propósito: recorre ventanas y con el autoocultar encendido no hace falta.
    (bool Toca, string Motivo) TocaRecogerse() =>
        PanelNube.TocaRecogerse(VisibilidadPanel, _cfg.Barra.OcultarEnPantallaCompleta,
                                !PanelAutoOculta && _cfg.Barra.OcultarEnPantallaCompleta && HayPantallaCompleta());

    void RevisarPantallaCompleta()
    {
        if (_escondidaAMano) return;
        if (!PanelNube.PuedeRecogerse(VisibilidadPanel, _cfg.Barra.OcultarEnPantallaCompleta))
        {
            if (_recogida) { _recogida = false; _asomada = false; Deslizar(false); }
            return;
        }

        var (recoger, motivo) = TocaRecogerse();

        if (recoger != _recogida)
        {
            _recogida = recoger;
            // Si justo cuando toca esconderse tienes el ratón encima de la isla, se
            // queda asomada. Pasaba al cerrar un panel: el primer plano volvía a la
            // ventana maximizada y la isla desaparecía debajo del puntero, que es
            // justo donde acababas de hacer clic.
            _asomada = recoger && RatonEnLaFranja();
            _ratonEnElBorde = DateTime.MaxValue;
            Deslizar(recoger && !_asomada);
            Log.Info($"la isla de {_monitor.Dispositivo} " +
                     (recoger ? _asomada ? $"tendría que recogerse ({motivo}), pero se queda asomada (tienes el ratón encima)"
                                         : $"se recoge ({motivo})"
                              : "vuelve"));
            return;
        }
        if (!_recogida) return;

        // Escondida: el ratón pegado al filo durante 300 ms la hace asomar
        bool enElBorde = RatonEnElFilo();

        if (!_asomada)
        {
            if (!enElBorde) { _ratonEnElBorde = DateTime.MaxValue; return; }
            if (_ratonEnElBorde == DateTime.MaxValue) { _ratonEnElBorde = DateTime.Now; return; }
            if ((DateTime.Now - _ratonEnElBorde).TotalMilliseconds < MsAsomar) return;
            _asomada = true;
            Deslizar(false);
            return;
        }

        // Asomada: se va en cuanto sacas el ratón de la franja donde está la isla.
        // Con un panel abierto no: estarías usando el menú con el teclado y se
        // escondería debajo en cuanto apartases el ratón.
        if (_panelAbierto != null) return;
        if (RatonEnLaFranja()) return;

        _asomada = false;
        _ratonEnElBorde = DateTime.MaxValue;
        Deslizar(true);
    }

    // ---------- La animación ----------
    // Igual que el resto: el valor real de la propiedad es siempre el estado final y
    // la animación solo dibuja el camino. Si fallara, la isla quedaría donde toca.
    // Se va por SU borde: la horizontal por arriba o por abajo moviéndose en Y, la
    // vertical por su lado moviéndose en X. Es el mismo deslizamiento con el eje
    // cambiado, y por eso el autoocultar y la pantalla completa funcionan igual en los
    // cuatro bordes sin una línea de más en quien los decide.
    void Deslizar(bool fuera)
    {
        bool v = Vertical;
        var eje = v ? TranslateTransform.XProperty : TranslateTransform.YProperty;
        double propio = _cfg.Barra.Alto + _cfg.Barra.Margen * 2;
        double medida = v ? ActualWidth : ActualHeight;
        // Con un panel abierto la ventana es mucho más grande que la franja: lo que hay
        // que apartar es la BARRA, así que nunca más que su propio grosor.
        double distancia = medida > 0 ? Math.Min(medida, propio) : propio;
        bool haciaAtras = v ? PegadaIzquierda : Arriba;
        double destino = fuera ? (haciaAtras ? -distancia : distancia) : 0;

        if (_mover == null || Isla.RenderTransform is not TranslateTransform t || t != _mover)
        {
            _mover = Isla.RenderTransform as TranslateTransform;
            if (_mover == null) { _mover = new TranslateTransform(); Isla.RenderTransform = _mover; }
        }

        // Al cambiar de orientación en caliente el otro eje tiene que volver a cero, o
        // la barra se queda apartada en una dirección que ya no se usa.
        if (v && Math.Abs(_mover.Y) > 0.01) { _mover.BeginAnimation(TranslateTransform.YProperty, null); _mover.Y = 0; }
        if (!v && Math.Abs(_mover.X) > 0.01) { _mover.BeginAnimation(TranslateTransform.XProperty, null); _mover.X = 0; }

        double desde = v ? _mover.X : _mover.Y;
        _mover.BeginAnimation(eje, null);
        if (v) _mover.X = destino; else _mover.Y = destino;
        _mover.BeginAnimation(eje, Kf(desde, destino, 260, 0, Suave));
    }
}
