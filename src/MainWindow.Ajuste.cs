using System;
using System.Windows.Threading;

namespace NubeBar;

// Arrastrar una ventana DE VERDAD a un borde y que se ajuste, con previsualización.
//
// El rectángulo que se enseña y las cuentas de dónde cae no están aquí: la
// previsualización es compartida con el arrastre de tarjetas de la vista general y vive en
// `MainWindow.Previa.cs`; las zonas, en `ZonasAjuste.cs`. Aquí está solo lo propio de
// arrastrar una ventana ajena: enterarse, seguir el ratón y decidir al soltar.
//
// ---------- Cómo se entera, sin capturar nada ----------
//
// Windows avisa de que el usuario ha empezado a mover una ventana
// (`EVENT_SYSTEM_MOVESIZESTART`) y de que la ha soltado (`...MOVESIZEEND`). Esos avisos
// ya los recibe `GestorVentanas`, así que aquí no se instala ningún gancho más: se
// escucha lo que él publica.
//
// Mientras dura el arrastre se pregunta dónde está el puntero cada 60 ms con
// `GetCursorPos`. **Eso no es capturar el ratón**: no hay gancho de entrada, no se
// intercepta nada, nadie deja de recibir sus clics y se deja de preguntar en cuanto se
// suelta. Es la misma técnica con la que la isla se deja arrastrar desde B2, y la razón de
// no usar `MouseMove` es que el ratón lo tiene capturado la ventana que se está moviendo.
public partial class MainWindow
{
    DispatcherTimer? _miraElRaton;
    IntPtr _arrastrada;                 // la ventana REAL que se está arrastrando

    // ---------- Las dos decisiones, aparte ----------
    //
    // Están fuera de los manejadores a propósito, y es la lección de C4.1: los cuatro
    // fallos que solo se vieron usándola eran decisiones metidas dentro de un manejador
    // de eventos, donde no hay forma de comprobarlas sin arrastrar ventanas de verdad.

    // Si merece la pena vigilar este arrastre.
    internal static bool VigilarElArrastre(bool activado, bool existe, bool esNuestra, bool maximizada) =>
        activado && existe && !esNuestra && !maximizada;

    internal enum FinDeArrastre
    {
        NoEsNuestro,        // no es la ventana que vigilábamos: no se toca nada
        SoltarSinAjustar,   // es la nuestra, pero no acabó en ninguna zona
        Ajustar,
    }

    // Qué hacer cuando Windows avisa de que alguien ha soltado una ventana.
    //
    // `NoEsNuestro` es lo que hacía falta: `MoverSeTermina` salta por CUALQUIER ventana
    // que termine de moverse, no solo por la que se arrastra. Sin distinguirlo, una
    // recolocación de fondo en mitad del gesto apagaba el reloj, borraba el estado y
    // hacía desaparecer la previsualización, y el arrastre se quedaba sin ajustar.
    internal static FinDeArrastre AlTerminar(IntPtr enCurso, IntPtr soltada, Zona zona)
    {
        if (enCurso == IntPtr.Zero || soltada != enCurso) return FinDeArrastre.NoEsNuestro;
        return zona == Zona.Ninguna ? FinDeArrastre.SoltarSinAjustar : FinDeArrastre.Ajustar;
    }

    // Solo la isla jefa se encarga: con dos monitores hay dos MainWindow y el arrastre
    // es uno solo. Si no, se pelearían por enseñar dos previsualizaciones.
    void MontarAjusteDeBordes()
    {
        if (!_esJefa) return;
        GestorVentanas.MoverSeEmpieza += EmpiezaElArrastre;
        GestorVentanas.MoverSeTermina += TerminaElArrastre;
        Log.Info("ajuste a los bordes en marcha");
    }

    void DesmontarAjusteDeBordes()
    {
        GestorVentanas.MoverSeEmpieza -= EmpiezaElArrastre;
        GestorVentanas.MoverSeTermina -= TerminaElArrastre;
        _miraElRaton?.Stop();           // si no, al cerrar en mitad de un arrastre sigue preguntando
        _arrastrada = IntPtr.Zero;
        QuitarPrevia(DuenoPrevia.Ventana);
    }

    void EmpiezaElArrastre(IntPtr h)
    {
        // Una maximizada no se ajusta: lo que hace Windows al arrastrarla es
        // restaurarla, y meterse ahí sería pelearse con el gestor del sistema.
        bool nuestra = Nativo.EsNuestra(h);
        if (!VigilarElArrastre(_cfg.Barra.AjustarAlArrastrar, Nativo.Existe(h),
                               nuestra, Nativo.EstaMaximizada(h)))
        {
            Log.Info($"arrastre ventana IGNORADO hwnd=0x{h.ToInt64():X}" +
                     (nuestra ? " (es de NubeBar)" : "") +
                     (!_cfg.Barra.AjustarAlArrastrar ? " (apagado en Ajustes)" : "") +
                     (Nativo.EstaMaximizada(h) ? " (está maximizada)" : "") +
                     (!Nativo.Existe(h) ? " (ya no existe)" : ""));
            return;
        }

        _arrastrada = h;
        _zonaPrevia = Zona.Ninguna;
        _monitorPrevio = null;

        _miraElRaton ??= new DispatcherTimer(TimeSpan.FromMilliseconds(60),
            DispatcherPriority.Background, (_, _) => MirarDondeCae(), Dispatcher);
        _miraElRaton.Start();
        DiagVista.ArrastresVentana++;
        Log.Info($"arrastre ventana INICIADO hwnd=0x{h.ToInt64():X} \"{Nativo.Titulo(h)}\"");
    }

    void TerminaElArrastre(IntPtr h)
    {
        var que = AlTerminar(_arrastrada, h, _zonaPrevia);
        if (que == FinDeArrastre.NoEsNuestro)
        {
            // Esto es lo que antes cancelaba el gesto: el aviso de otra ventana.
            if (_arrastrada != IntPtr.Zero)
            {
                DiagVista.AvisosAjenos++;
                Log.Info($"arrastre ventana: aviso AJENO hwnd=0x{h.ToInt64():X}, " +
                         $"el gesto sigue siendo de 0x{_arrastrada.ToInt64():X}");
            }
            return;
        }

        var zona = _zonaPrevia;
        var monitor = _monitorPrevio;
        var ventana = _arrastrada;

        _miraElRaton?.Stop();
        _arrastrada = IntPtr.Zero;
        _zonaPrevia = Zona.Ninguna;
        _monitorPrevio = null;
        QuitarPrevia(DuenoPrevia.Ventana);

        if (que != FinDeArrastre.Ajustar)
        {
            Log.Info($"arrastre ventana TERMINADO hwnd=0x{ventana.ToInt64():X} sin zona: no se toca nada");
            return;
        }

        // El monitor va explícito, y es EL MISMO con el que se dibujó la
        // previsualización. Sin pasarlo, `Ajustar` lo deduce de la ventana, que en el
        // salto entre dos pantallas no es el del puntero: el cuerpo de la ventana sigue
        // en la de origen mientras el ratón ya empuja el borde de la otra, así que la
        // previsualización prometía una pantalla y el ajuste caía en la otra.
        //
        // `activar` se queda en false: la ventana que se arrastra YA es la del frente,
        // así que activarla sería un paso de más, y si `SetForegroundWindow` fallara,
        // `Nativo.Activar` haría el truco del Alt para nada.
        Log.Info($"arrastre ventana TERMINADO hwnd=0x{ventana.ToInt64():X} " +
                 $"destino={ZonasAjuste.Nombre(zona)} pantalla={NombreDeMonitor(monitor)}");
        ZonasAjuste.Ajustar(ventana, zona, monitor, estorbos: EstorbosDeAhora());
        GestorVentanas.HayQueMirar(GestorVentanas.Motivo.AMano);
    }

    void MirarDondeCae()
    {
        if (_arrastrada == IntPtr.Zero) { _miraElRaton?.Stop(); return; }

        // Si el botón ya no está pulsado es que se soltó y el aviso de Windows se
        // perdió (pasa al arrastrar fuera de la pantalla). Se cierra solo.
        if (!Nativo.BotonIzquierdoPulsado()) { TerminaElArrastre(_arrastrada); return; }

        try
        {
            var p = Monitores.Raton();
            var m = Monitores.DelRaton();
            if (m == null) { Poner(DuenoPrevia.Ventana, Zona.Ninguna, null); return; }
            Poner(DuenoPrevia.Ventana, ZonasAjuste.ZonaDelPunto(p.X, p.Y, m), m);
        }
        catch (Exception ex) { Log.Error("mirar dónde cae la ventana", ex); }
    }
}
