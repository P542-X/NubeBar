using System;
using System.Windows;

namespace NubeBar;

// Los cuatro modos de visibilidad del panel y los cuatro bordes, conectados con lo
// que de verdad hace la ventana.
//
// Hasta B4 esto eran dos cadenas en el config que no leía nadie. Aquí se cumplen.
// Qué significa cada modo está escrito en PanelNube (PorEncima, AutoOculta,
// AlFondoDelTodo, BordeEfectivo): aquí solo se traduce a Win32 y a WPF, para que la
// decisión se pueda probar sin abrir una ventana.
public partial class MainWindow : Window
{
    string VisibilidadPanel => PanelPrincipal.Visibilidad ?? "siempre";

    // ¿Este panel se esconde solo cuando no lo usas?
    public bool PanelAutoOculta => PanelNube.AutoOculta(VisibilidadPanel);

    // ---------- Orden Z ----------
    //
    // Ojo con la bandeja: TrayService manda la Shell_TrayWnd de Explorer al fondo y la
    // nuestra arriba cada 100 ms. Con "ventanas-encima" o "por-debajo" esos dos se
    // pelean, así que se avisa en el log una vez; no se apaga la bandeja por la
    // espalda, que sería peor sorpresa.
    void AplicarVisibilidad()
    {
        if (_cerrando) return;
        try
        {
            string v = VisibilidadPanel;
            bool encima = PanelNube.PorEncima(v);

            if (Topmost != encima) Topmost = encima;

            if (_self != IntPtr.Zero && PanelNube.AlFondoDelTodo(v))
                Nativo.AlFondo(_self);
            else if (_self != IntPtr.Zero && !encima)
                Nativo.NoSiempreEncima(_self);

            if (!encima && _cfg.Barra.Bandeja && HayModuloBandeja() && !_avisadoBandejaZ)
            {
                _avisadoBandejaZ = true;
                Log.Info($"visibilidad \"{v}\" con la bandeja encendida: el servicio de bandeja " +
                         "vuelve a poner la nube por encima cada 100 ms, así que el orden Z puede pelearse. " +
                         "Si molesta, quita el módulo bandeja o usa \"siempre\".");
            }

            // Autoocultar se apoya en el mismo deslizamiento que la pantalla completa:
            // la isla se va por su borde y asoma al acercar el ratón. Esa maquinaria ya
            // existe y está probada (ver TocaRecogerse en MainWindow.PantallaCompleta.cs),
            // así que aquí solo hay que darle un repaso ahora mismo en vez de esperar al
            // vigía: el cambio en Ajustes se ve al instante, y vale en los dos sentidos
            // (al encender el autoocultar se recoge, y al apagarlo vuelve).
            RevisarPantallaCompleta();
        }
        catch (Exception ex) { Log.Error("aplicar la visibilidad del panel", ex); }
    }

    bool _avisadoBandejaZ;

    // ---------- Los cuatro bordes ----------
    //
    // Desde B6 los cuatro se dibujan de verdad y esto ya no recorta nada. Lo que
    // cambia según el modo no es si el borde vale, sino qué significa: una ISLA en un
    // lateral se ancla a ese lado y sigue siendo horizontal (B5); una BARRA en un
    // lateral es vertical de verdad (ver MainWindow.Vertical.cs).
    public string PosicionEfectiva => PanelNube.BordeEfectivo(PanelPrincipal.Posicion);

    // Dónde se pega la isla dentro de su franja. Solo tiene sentido en horizontal: en
    // vertical la barra se estira de arriba abajo y la pega ColocarBarraVertical.
    HorizontalAlignment AlineacionDeLaIsla()
    {
        var p = PosicionEfectiva;
        if (Vertical) return PegadaIzquierda ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        if (p.Equals("izquierda", StringComparison.OrdinalIgnoreCase)) return HorizontalAlignment.Left;
        if (p.Equals("derecha", StringComparison.OrdinalIgnoreCase)) return HorizontalAlignment.Right;
        return EsIsla ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
    }

    bool ArribaEfectivo => PanelNube.CuelgaDeArriba(PanelPrincipal.Posicion);

    // ¿Le pedimos a Windows que nos guarde la franja? Solo el modo barra: la isla flota
    // encima de las ventanas, como en el iPhone. Y una barra que se autooculta tampoco,
    // que reservar sitio para algo que no está es lo que uno quiere evitar al
    // encender el autoocultar (en Plasma pasa lo mismo).
    // Barra.ReservarEspacio ya viene resuelto de la carga, con el defecto aplicado si
    // no lo has elegido (ver PanelNube.ReservaEfectiva). Y la ISLA no reserva nunca,
    // la elijas o no: flota encima de las ventanas, que es lo que es.
    bool ReservaHueco => !EsIsla && _cfg.Barra.ReservarEspacio && !PanelAutoOculta;

    // Para el log de arranque: en una línea, qué panel ha salido de verdad. Antes solo
    // se apuntaba "Modo=isla", que a partir de B5 ya no dice casi nada.
    string ResumenDelPanel()
    {
        var p = PanelPrincipal;
        return $"panel[{p.Id}] modo={p.Modo} borde={PosicionEfectiva} " +
               $"orientación={(Vertical ? "vertical" : "horizontal")}" +
               $" grosor={p.Grosor:0} margen={p.Margen:0} visibilidad={VisibilidadPanel}" +
               $" alineación={AlineacionDeLaIsla()} reserva={(ReservaHueco ? "sí" : "no")}" +
               (_recogida ? " [recogida]" : "") + ResumenVertical();
    }
}
