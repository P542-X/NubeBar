using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// La previsualización del ajuste: el rectángulo que enseña dónde va a caer una ventana
// antes de soltarla.
//
// ---------- Por qué vive aparte ----------
//
// La usan DOS gestos distintos: arrastrar una tarjeta de la vista general y arrastrar una
// ventana de verdad por su barra de título. Mientras estaba metida en el archivo del
// segundo, los dos compartían los mismos campos, y ahí estaba el fallo: cerrar la vista en
// mitad del arrastre de una ventana real llamaba a `PararArrastreDeTarjeta`, que borraba la
// zona y escondía el rectángulo, y al soltar ya no había zona que ajustar — el gesto se
// perdía en silencio.
//
// De ahí el **dueño**: `DuenoPrevia` dice de quién es ahora mismo, y quien no la tiene no
// la puede apagar. Es una ventana sola porque con un solo botón del ratón no hay dos
// gestos a la vez; lo que no se puede es que el otro la toque.
//
// ---------- Y por qué es una VentanaSuperpuesta ----------
//
// Hacía falta algo que se dibuje encima de todo, que no coja el foco y que deje pasar los
// clics de verdad, y eso ya existía: la ventana de los paneles de la barra vertical.
// Montar una segunda ventana flotante con reglas propias habría sido exactamente la deuda
// que la Fase 7 quiere no tener.
public partial class MainWindow
{
    // ---------- De quién es la previsualización ----------
    internal enum DuenoPrevia { Nadie, Tarjeta, Ventana }

    VentanaSuperpuesta? _previa;        // el rectángulo que enseña dónde va a caer
    DuenoPrevia _duenoPrevia = DuenoPrevia.Nadie;
    Zona _zonaPrevia = Zona.Ninguna;
    Monitores.Info? _monitorPrevio;     // y en QUÉ PANTALLA lo prometió

    // Si hay algún ajuste en curso, de cualquiera de los dos tipos. La isla lo pregunta
    // para no empezar el suyo encima: con un solo botón no hay dos gestos a la vez, y si
    // el estado dice que sí, el que no empieza es el de la isla.
    public bool AjustandoAlgo => _duenoPrevia != DuenoPrevia.Nadie || ArrastrandoTarjeta ||
                                 _arrastrada != IntPtr.Zero;

    // ---------- Enseñar la promesa ----------
    //
    // El `dueno` no es ceremonia: es lo que impide que el arrastre de una tarjeta y el de
    // una ventana real se apaguen el uno al otro.
    void Poner(DuenoPrevia dueno, Zona z, Monitores.Info? m)
    {
        if (_duenoPrevia != DuenoPrevia.Nadie && _duenoPrevia != dueno) return;
        if (z == _zonaPrevia && _duenoPrevia == dueno) return;

        var antes = _zonaPrevia;
        _zonaPrevia = z;

        if (z == Zona.Ninguna || m == null)
        {
            _monitorPrevio = null;
            QuitarPrevia(dueno);
            return;
        }

        var hueco = ZonasAjuste.Colocacion(z, ZonasAjuste.AreaUtil(m));
        if (hueco.Vacio) { _monitorPrevio = null; QuitarPrevia(dueno); return; }

        _duenoPrevia = dueno;
        _monitorPrevio = m;
        _previa ??= CrearPrevia();
        _previa.Cajon.Content = CuerpoDeLaPrevia(z);
        _previa.Colocar(hueco.X, hueco.Y, hueco.Ancho, hueco.Alto);
        if (!_previa.IsVisible) _previa.Show();
        // El estilo de "los clics la atraviesan" necesita el hwnd, que no existe hasta el
        // primer Show(). Se pone aquí, y `HacerseAtravesable` es idempotente.
        _previa.HacerseAtravesable();
        _previa.Elevar();

        if (antes != z)
            Log.Info($"arrastre {(dueno == DuenoPrevia.Tarjeta ? "tarjeta" : "ventana")} " +
                     $"destino={ZonasAjuste.Nombre(z)} pantalla={NombreDeMonitor(m)} " +
                     $"hueco={hueco.X},{hueco.Y} {hueco.Ancho}x{hueco.Alto}");
    }

    VentanaSuperpuesta CrearPrevia()
    {
        var v = new VentanaSuperpuesta(this);
        // Que el contenido no cuente para el ratón tampoco: es la mitad de WPF del
        // asunto, y la otra mitad —WS_EX_TRANSPARENT y WM_NCHITTEST— la pone
        // `HacerseAtravesable` en cuanto hay hwnd.
        v.Tarjeta.IsHitTestVisible = false;
        v.Cajon.IsHitTestVisible = false;
        return v;
    }

    FrameworkElement CuerpoDeLaPrevia(Zona z)
    {
        // Relleno con el velo de "elegido" —un 20 % del acento, para que se siga viendo
        // lo que hay debajo— y filo con el acento de verdad. El filo no es adorno: sobre
        // un fondo claro un velo del 20 % casi no se distingue, y lo que tiene que
        // quedar clarísimo es EL BORDE del hueco, que es la promesa.
        var caja = new Border
        {
            Background = _elegido,
            BorderBrush = _acento,
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false
        };
        var dentro = new Grid { IsHitTestVisible = false };
        caja.Child = dentro;
        dentro.Children.Add(new TextBlock
        {
            Text = ZonasAjuste.Nombre(z),
            Foreground = _acentoLegible,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.9,
            IsHitTestVisible = false
        });
        return caja;
    }

    // Solo el dueño la apaga. Quien no la tiene, no la toca: era el camino por el que
    // cerrar la vista en mitad del arrastre de una ventana real se comía el ajuste.
    void QuitarPrevia(DuenoPrevia dueno)
    {
        if (_duenoPrevia != DuenoPrevia.Nadie && _duenoPrevia != dueno) return;
        _duenoPrevia = DuenoPrevia.Nadie;
        if (_previa == null) return;
        try { if (_previa.IsVisible) _previa.Hide(); }
        catch (Exception ex) { Log.Error("quitar la previsualización del ajuste", ex); }
    }

    // Qué pantalla es, para el log. El número que ve el usuario en Ajustes, y su área de
    // trabajo, que es lo que de verdad decide dónde cae el ajuste.
    static string NombreDeMonitor(Monitores.Info? m)
    {
        if (m == null) return "?";
        int i = Monitores.Todos().FindIndex(x => x.Handle == m.Handle);
        return (i < 0 ? "?" : (i + 1).ToString()) + $" ({m.TrabajoX},{m.TrabajoY} {m.TrabajoAncho}x{m.TrabajoAlto})";
    }
}
