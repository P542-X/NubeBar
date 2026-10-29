using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NubeBar;

// De quién es el gesto: de un control o de la nube.
//
// ---------- El fallo que esto arregla ----------
//
// Arrastrar el deslizador de volumen movía TAMBIÉN la isla por la pantalla. El valor
// cambiaba bien, pero la nube se iba detrás del ratón.
//
// La causa es la de siempre y ya mordió en C4.3: el arrastre de la isla se engancha en
// `Isla.PreviewMouseLeftButtonDown`, y los eventos **Preview van de la raíz a la hoja**.
// Cuando el deslizador se entera del clic, la isla ya ha empezado su arrastre. Poner
// `e.Handled` en el control llega tarde **por definición** — no es que se olvidara, es que
// no puede funcionar.
//
// La guarda que había solo conocía la vista general (`_panelAbierto == PanelVista`), así
// que con el panel de audio abierto el campo valía "audio" y el arrastre arrancaba.
//
// ---------- La regla ----------
//
// Si el clic nace sobre algo con lo que se interactúa, **el gesto es de ese algo**. Se
// mira el origen y se sube por el árbol visual; no se consulta ningún campo de estado,
// porque el estado no sabe dónde pusiste el dedo.
//
// Hay dos formas de decir "esto no arrastra la nube":
//
//  1. Ser un control conocido (`Slider`, `Button`, `ComboBox`, `TextBox`, `ScrollViewer`…).
//  2. Llevar puesta la propiedad adjunta `NoArrastrarNubeBar`, para lo que no es un
//     control del marco pero se comporta como uno: mis piezas propias, una fila de red,
//     la capa de un deslizador hecho a mano.
public partial class MainWindow
{
    // ---------- La marca explícita ----------
    //
    // Se pone en un elemento y protege a él y a todo lo que tenga dentro. Hace falta
    // porque mis componentes no son controles del marco: el `Slider` de NubeBar es un
    // `Grid` con tres `Border` y una `Ellipse`, y por tipo no hay forma de saber que eso
    // es un deslizador.
    public static readonly DependencyProperty NoArrastrarNubeBarProperty =
        DependencyProperty.RegisterAttached(
            "NoArrastrarNubeBar", typeof(bool), typeof(MainWindow),
            new FrameworkPropertyMetadata(false));

    public static void SetNoArrastrarNubeBar(DependencyObject d, bool valor) =>
        d.SetValue(NoArrastrarNubeBarProperty, valor);

    public static bool GetNoArrastrarNubeBar(DependencyObject d) =>
        (bool)d.GetValue(NoArrastrarNubeBarProperty);

    // Marca un elemento y devuelve el mismo, para poder encadenarlo al construirlo.
    internal static T SinArrastre<T>(T e) where T : DependencyObject
    {
        SetNoArrastrarNubeBar(e, true);
        return e;
    }

    // ---------- Los tipos que siempre son suyos ----------
    //
    // `ButtonBase` cubre Button, ToggleButton, CheckBox y RadioButton. `TextBoxBase`
    // cubre TextBox y RichTextBox. `Thumb` es el agarre de un ScrollBar o de un Slider
    // del marco, y `RangeBase` el propio Slider y la ProgressBar.
    internal static bool EsTipoInteractivo(object o) => o is
        ButtonBase or RangeBase or Thumb or ComboBox or ComboBoxItem or
        TextBoxBase or PasswordBox or ScrollViewer or ScrollBar or
        ListBoxItem or ListBox or Selector or MenuItem or System.Windows.Controls.ContextMenu or
        System.Windows.Controls.Slider or ToolBar or TabControl;

    // ---------- La decisión ----------
    //
    // Pura sobre el árbol: se le da el origen del evento y dice si el gesto es de un
    // control. Se sube hasta `tope` —la isla— y no más allá, para no salirse de la nube.
    //
    // Pública y comprobable: se puede montar un `Border` con un `Button` dentro y
    // preguntar, sin abrir ninguna ventana ni pulsar nada.
    internal static bool GestoDeUnControl(object? origen, DependencyObject? tope = null)
    {
        if (origen is not DependencyObject d) return false;
        for (var p = d; p != null; p = Padre(p))
        {
            if (EsTipoInteractivo(p)) return true;
            if (p is DependencyObject dp && GetNoArrastrarNubeBar(dp)) return true;
            if (tope != null && ReferenceEquals(p, tope)) break;
        }
        return false;
    }

    // El visual primero y el lógico de respaldo: una plantilla puede romper la cadena
    // visual, y entonces el lógico es el que sigue llevando al control.
    static DependencyObject? Padre(DependencyObject d)
    {
        try
        {
            if (d is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                var v = VisualTreeHelper.GetParent(d);
                if (v != null) return v;
            }
        }
        catch { /* un objeto que no está en el árbol visual: se prueba el lógico */ }
        try { return LogicalTreeHelper.GetParent(d); }
        catch { return null; }
    }

    // ================= El gesto de un deslizador =================
    //
    // ---------- El fallo que esto arregla ----------
    //
    // Los deslizadores se quedaban agarrados: al soltar el botón, el control seguía en
    // estado de arrastre y el valor seguía moviéndose con el ratón.
    //
    // La causa: el fin del gesto colgaba de `MouseLeftButtonUp`. Las ventanas de NubeBar
    // son `WS_EX_NOACTIVATE` y por capas, y en esa clase de ventana **ese evento se
    // pierde** —al soltar fuera, al cambiar de ventana, al perder la captura—. Si no
    // llega, nadie limpia y el arrastre se queda puesto para siempre.
    //
    // ---------- Y por qué una máquina de estados ----------
    //
    // Porque hay SEIS formas de terminar —soltar, perder la captura, Esc, cerrar el
    // panel, cambiar de panel, empezar otro gesto— y con banderas sueltas cada una se
    // limpia a su manera y alguna se olvida. Aquí hay un solo camino de salida y es
    // **idempotente**: llamarlo cinco veces no falla ni deja el control bloqueado.
    //
    // La máquina no sabe de WPF a propósito: recibe "pasó esto" y avisa de lo que hay que
    // soltar. Así se puede comprobar con diez arrastres seguidos sin tocar un ratón.
    internal enum EstadoGesto { Inactivo, Arrastrando, Finalizando, Cancelado }

    internal sealed class GestoDeslizador
    {
        public EstadoGesto Estado { get; private set; } = EstadoGesto.Inactivo;

        // Espejo de los recursos de verdad: la captura del ratón y el oyente de fotograma.
        // Si estos dos quedan en false, no hay nada agarrado.
        public bool Capturado { get; private set; }
        public bool Siguiendo { get; private set; }

        // Para poder comprobar que limpiar es idempotente.
        public int VecesLimpiado { get; private set; }

        public bool Arrastrando => Estado == EstadoGesto.Arrastrando;

        // Lo que hay que soltar de verdad. Lo conecta el deslizador de WPF.
        public Action? SoltarCaptura;
        public Action? DejarDeSeguir;
        public Action<string>? Apuntar;

        // Empezar. Si ya había un gesto en marcha se termina primero: dos interacciones
        // mezcladas es otra forma de quedarse agarrado.
        public bool Empezar()
        {
            if (Estado == EstadoGesto.Arrastrando) Terminar("empezó otro gesto");
            Estado = EstadoGesto.Arrastrando;
            Capturado = true;
            Siguiendo = true;
            Apuntar?.Invoke("slider: inicio");
            return true;
        }

        // Un fotograma. Devuelve si el gesto sigue vivo; si el botón ya no está pulsado,
        // termina aquí — y esto es lo que hace que no haga falta `MouseLeftButtonUp`.
        public bool Fotograma(bool botonPulsado)
        {
            if (Estado != EstadoGesto.Arrastrando) return false;
            if (!botonPulsado)
            {
                Apuntar?.Invoke("slider: soltado detectado");
                Terminar("el botón ya no está pulsado");
                return false;
            }
            return true;
        }

        public void Mover()
        {
            if (Estado == EstadoGesto.Arrastrando) Apuntar?.Invoke("slider: movimiento");
        }

        // Terminar bien. Idempotente: si no había gesto, limpia por si quedaba algo y se va.
        public void Terminar(string porque = "")
        {
            if (Estado == EstadoGesto.Finalizando) return;      // ya estamos dentro
            if (Estado != EstadoGesto.Arrastrando) { Limpiar(); return; }
            Estado = EstadoGesto.Finalizando;
            Limpiar();
            Estado = EstadoGesto.Inactivo;
            Apuntar?.Invoke("slider: estado limpio" + (porque.Length > 0 ? " (" + porque + ")" : ""));
        }

        // Cancelar: igual, pero sin aplicar nada nuevo. Esc, perder el foco, cerrar el panel.
        public void Cancelar(string porque = "")
        {
            if (Estado == EstadoGesto.Finalizando) return;
            bool habia = Estado == EstadoGesto.Arrastrando;
            Estado = EstadoGesto.Cancelado;
            Limpiar();
            Estado = EstadoGesto.Inactivo;
            if (habia) Apuntar?.Invoke("slider: cancelado" + (porque.Length > 0 ? " (" + porque + ")" : ""));
        }

        // El único camino de salida. Suelta lo que haya y nada más de una vez.
        void Limpiar()
        {
            VecesLimpiado++;
            if (Capturado)
            {
                Capturado = false;
                Apuntar?.Invoke("slider: captura liberada");
                SoltarCaptura?.Invoke();
            }
            if (Siguiendo)
            {
                Siguiendo = false;
                DejarDeSeguir?.Invoke();
            }
        }
    }
}
