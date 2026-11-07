using System;
using System.Collections.Generic;

namespace NubeBar;

// El enganche de `HyprConfirmar`: la pregunta en pantalla y la respuesta.
//
// ---------- Lo que esto NO hace ----------
//
// No decide nada. Las decisiones —si hace falta preguntar, cuándo caduca la pregunta, cuántas
// ventanas son, qué se les va a hacer— están en `HyprConfirmar`, que es puro y se prueba sin
// abrir una ventana. Aquí solo queda lo que necesita pantalla.
//
// ---------- Por qué reutiliza el panel de cerrar apps ----------
//
// `Confirmar(pregunta, detalle, queHace, accion, peligro)` ya existe desde la fase 1, en
// `MainWindow.Cerrar.cs`, y lleva resueltas las cosas que se olvidan al escribir el segundo:
// sale dentro de la isla con el aire del resto, el texto se ajusta, los dos botones pasan por
// `Boton` —así que tienen nombre para el lector de pantalla— y el panel se cierra antes de
// ejecutar la acción, no después.
//
// Escribir aquí un segundo panel de confirmación habría dado dos sitios donde arreglar el
// mismo fallo, que es exactamente lo que pasó con la colocación de los paneles laterales.
public partial class MainWindow
{
    // La pregunta pendiente, si hay alguna. Una cada vez: ver `HyprConfirmar.Sustituir`.
    HyprConfirmar.Peticion? _confirmacionHypr;

    // Para los diagnósticos y las pruebas: cuántas se han preguntado y cómo acabaron. Un
    // contador de «preguntadas» sin uno de «aplicadas» no distingue entre «el usuario dijo
    // no» y «mi código se comió la respuesta».
    internal int ConfirmacionesPedidas { get; private set; }
    internal int ConfirmacionesAceptadas { get; private set; }
    internal int ConfirmacionesCaducadas { get; private set; }

    // Con red por lo mismo, y además por dónde se llama: `AplicarConRedes` corre desde el
    // oyente de reglas, o sea desde el evento de «ha aparecido una ventana». Si abrir el
    // panel reventara ahí, se llevaría por delante el camino que vigila las ventanas.
    void PedirConfirmacionDeHypr(IReadOnlyList<EfectoVentana> efectos, string porque)
    {
        try { PedirConfirmacionDeHyprDeVerdad(efectos, porque); }
        catch (Exception ex)
        {
            _confirmacionHypr = null;
            Log.Error("pedir la confirmación de NubeHypr", ex);
        }
    }

    void PedirConfirmacionDeHyprDeVerdad(IReadOnlyList<EfectoVentana> efectos, string porque)
    {
        var ahora = DateTime.Now;
        var (p, descartada) = HyprConfirmar.Sustituir(
            _confirmacionHypr, new HyprConfirmar.Peticion(porque, efectos, ahora));

        // Una petición que desaparece sin dejar rastro son treinta minutos de no entender por
        // qué no se movió nada.
        if (descartada != null)
            Log.Info(HyprConfirmar.Linea("descartada, llegó otra", descartada, ahora));

        _confirmacionHypr = p;
        ConfirmacionesPedidas++;
        Log.Info(HyprConfirmar.Linea("pedida", p, ahora));

        string detalle = HyprConfirmar.Detalle(efectos) is { Length: > 0 } d
            ? $"Va a {d}. Si no contestas, no se mueve nada."
            : "Si no contestas, no se mueve nada.";

        Confirmar(HyprConfirmar.Texto(p), detalle, "Mover",
                  () => ResponderConfirmacionDeHypr(p), peligro: false);
    }

    // ---------- La respuesta ----------
    //
    // Tres cosas se vuelven a mirar aquí, y las tres por el mismo motivo: entre la pregunta y
    // el clic pasa tiempo real, y lo que valía al preguntar puede no valer al contestar.
    //
    // Y con red, porque esto cuelga del clic de un botón: una excepción aquí no la recoge
    // nadie y sube al despachador de WPF, que en un reemplazo de shell no es «se cierra un
    // diálogo», es que se cae el escritorio. Es la misma red que lleva cualquier otro
    // manejador del proyecto.
    void ResponderConfirmacionDeHypr(HyprConfirmar.Peticion p)
    {
        try { ResponderConfirmacionDeHyprDeVerdad(p); }
        catch (Exception ex)
        {
            // Y si revienta, la petición NO se queda en pie: dejarla colgada haría que el
            // siguiente lote automático la «sustituyera» y apuntara un descarte que no
            // explica nada. Se suelta, y no se mueve nada, que es el lado seguro.
            _confirmacionHypr = null;
            Log.Error("contestar a la confirmación de NubeHypr", ex);
        }
    }

    void ResponderConfirmacionDeHyprDeVerdad(HyprConfirmar.Peticion p)
    {
        var ahora = DateTime.Now;

        // 1. ¿Es esta la pregunta que está en pie? Si llegó otra petición mientras el panel
        //    estaba abierto, el botón que se acaba de pulsar es el de una pregunta que ya no
        //    existe. Aplicar ahí movería un lote que el usuario no está viendo.
        if (!ReferenceEquals(_confirmacionHypr, p))
        {
            Log.Info(HyprConfirmar.Linea("contestada pero ya no estaba en pie", p, ahora));
            return;
        }
        _confirmacionHypr = null;

        // 2. ¿Sigue valiendo? Los efectos se calcularon contra las ventanas de hace un rato.
        if (p.Caducada(ahora))
        {
            ConfirmacionesCaducadas++;
            Log.Info(HyprConfirmar.Linea("caducada", p, ahora));
            // Y se dice, que es lo que no hacía el programa cuando una tecla no hacía nada:
            // un botón que se pulsa y no pasa nada es un botón roto a los ojos de cualquiera.
            Aviso("La propuesta ha caducado: vuelve a pedirla y te la calculo con las " +
                  "ventanas de ahora.");
            return;
        }

        // 3. ¿Y sigue la capa en aplicar? El modo se vuelve a leer, no se guarda al
        //    preguntar: si has apagado NubeHypr o lo has puesto en simulación mientras la
        //    pregunta estaba abierta, lo que vale es lo último que has dicho tú. Y si el
        //    modo seguro ha entrado por su cuenta, tampoco se mueve nada.
        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        if (modo != NubeHypr.Modo.Aplicar)
        {
            Log.Info(HyprConfirmar.Linea($"aceptada pero la capa está en «{modo}»", p, ahora));
            Avisar(HyprAviso.Apagado(HyprSalud.Seguro));
            return;
        }

        ConfirmacionesAceptadas++;
        Log.Info(HyprConfirmar.Linea("aceptada", p, ahora));

        // Por la misma puerta, con el permiso en la mano. No se llama al adaptador a pelo:
        // el respeto a tu gesto y el tope de ritmo se vuelven a consultar, y si en estos
        // segundos has movido una de esas ventanas a mano, esa se queda donde la pusiste.
        AplicarConRedes(p.Efectos, p.Porque, modo, automatico: true, yaConfirmado: true);
    }
}
