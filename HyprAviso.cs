using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D6 (lo que quedaba): el indicador breve de un comando.
//
// ---------- Para qué ----------
//
// Pulsas `Win+Ctrl+Alt+→` y... ¿ha pasado algo? Con la capa en simulación no se mueve nada,
// así que la respuesta honesta es "ha decidido, no ha movido" — y sin un indicador, eso solo
// se sabe abriendo el registro. Un atajo que no da señal es un atajo que la gente pulsa dos
// veces.
//
// El documento lo pide en D6 ("el usuario puede entender el estado sin leer un log") y se
// quedó sin construir. Esto es eso: **un segundo de texto** diciendo qué ha hecho.
//
// ---------- Lo que decide este archivo ----------
//
// Qué se escribe, de qué tono y cuánto dura. Dónde se dibuja es de la isla, que ya tiene su
// aviso de volumen y sabe reaprovecharlo. Hacer una ventana nueva para esto habría sido una
// segunda superficie flotante con sus propias reglas de foco y de clics.
public static class HyprAviso
{
    // Dos tonos, los mismos dos colores de D6: azul para "esto ha pasado" y coral para
    // "ojo". No hay un tercero a propósito — un indicador que dura un segundo no puede
    // enseñar una escala de gravedad.
    public enum Tono { Normal, Aviso }

    public sealed record Mensaje(string Glifo, string Texto, Tono Como)
    {
        public override string ToString() => $"{Texto} ({Como})";
    }

    // ---------- Los glifos ----------
    //
    // Uno por FAMILIA de comando, no uno por comando: veinte iconos distintos en un
    // indicador que dura un segundo es ruido, no información.
    //
    // Se escriben como puntos de código y no como el carácter pegado en el fuente, por lo
    // mismo que el resto del proyecto: un glifo pegado viaja mal entre editores y acaba
    // saliendo como una caja.
    //
    // ---------- Y están MIRADOS, no recordados ----------
    //
    // La primera versión de esta tabla puso 0xE7E8 para los escritorios porque sonaba a eso.
    // Es un **botón de encendido**. La prueba de glifos no lo pilló —comprueba que el punto
    // de código esté en el bloque de iconos, y lo estaba— y lo pilló la captura: un atajo de
    // escritorio con un icono de apagar al lado. Así que cada uno de estos se dibujó con la
    // fuente de verdad y se miró antes de escribirlo aquí.
    public const int CodigoFoco = 0xE737;      // una ventana con su barra: "esta ventana manda"
    public const int CodigoMover = 0xE8AB;     // Switch: las dos flechas de intercambio
    public const int CodigoTamano = 0xE740;    // FullScreen: las flechas en diagonal
    public const int CodigoLayout = 0xE8A9;    // GridView, el mismo de la cuadrícula de la barra
    public const int CodigoEspacio = 0xE7C4;   // TaskView: las ventanas superpuestas de los escritorios
    public const int CodigoDeshacer = 0xE7A7;  // Undo: la flecha que vuelve
    public const int CodigoNada = 0xE711;      // Cancel, el mismo del aspa de las tarjetas

    public static string Glifo(int codigo) => char.ConvertFromUtf32(codigo);

    public static string GlifoFoco => Glifo(CodigoFoco);
    public static string GlifoMover => Glifo(CodigoMover);
    public static string GlifoTamano => Glifo(CodigoTamano);
    public static string GlifoLayout => Glifo(CodigoLayout);
    public static string GlifoEspacio => Glifo(CodigoEspacio);
    public static string GlifoDeshacer => Glifo(CodigoDeshacer);
    public static string GlifoNada => Glifo(CodigoNada);

    // Todos los que puede enseñar, para que la prueba de glifos los repase sin tener que
    // acordarse de añadirlos uno a uno.
    public static readonly int[] Codigos =
    {
        CodigoFoco, CodigoMover, CodigoTamano, CodigoLayout,
        CodigoEspacio, CodigoDeshacer, CodigoNada,
    };

    public static string GlifoDe(string nombre) => (nombre ?? "").Split('.').FirstOrDefault() switch
    {
        "foco" => GlifoFoco,
        "mover" => GlifoMover,
        "tamano" => GlifoTamano,
        "layout" => GlifoLayout,
        "ventana" => GlifoLayout,
        "espacio" => GlifoEspacio,
        "deshacer" => GlifoDeshacer,
        _ => GlifoNada,
    };

    // ================= Lo que se escribe =================
    //
    // El rótulo del comando y, detrás, **lo que de verdad ha pasado**. Que son dos cosas
    // distintas: "Foco →" dice lo que pediste y "nada que enfocar" dice lo que salió. Un
    // indicador que solo repite lo que has pulsado no informa de nada, porque eso ya lo
    // sabes: acabas de pulsarlo.
    public static Mensaje DelComando(string nombre, int efectos, bool simulado, string? porque = null)
    {
        var rotulo = HyprAtajos.Rotulo(nombre);
        var glifo = GlifoDe(nombre);

        if (efectos <= 0)
            return new Mensaje(GlifoNada,
                               $"{rotulo}: {(porque is { Length: > 0 } p ? p : "no había nada que hacer")}",
                               Tono.Aviso);

        // En simulación se dice, y se dice siempre. Es la diferencia entre "lo he hecho" y
        // "lo haría", y ocultarla haría que el usuario creyera que la capa está moviendo
        // ventanas cuando no lo está.
        return new Mensaje(glifo,
                           simulado
                               ? $"{rotulo} · simulado, no se ha movido nada"
                               : $"{rotulo} · {efectos} ventana{(efectos == 1 ? "" : "s")}",
                           simulado ? Tono.Aviso : Tono.Normal);
    }

    // ---------- D9.4: lo que PROPUSO el motor y lo que SALIÓ por la puerta ----------
    //
    // Son dos números distintos y confundirlos es mentir con uno de ellos. El motor puede
    // proponer cuatro ventanas y el respeto a tu gesto descartar tres porque las colocaste tú:
    // decir «4 ventanas» es un número que se desmiente mirando la pantalla.
    //
    // Y el caso en que no sale ninguna necesita su propia frase. «Principal: no había nada que
    // hacer» sería falso —había cuatro— y dejaría al usuario pensando que el comando no
    // funciona, cuando lo que pasa es que NubeHypr está respetando lo que él hizo a mano.
    public static Mensaje DelComandoAplicado(string nombre, int propuestas, int salieron,
                                             bool simulado)
    {
        // En simulación no hay "salieron": no se mueve nada por definición, así que el número
        // honesto es lo que se haría.
        if (simulado) return DelComando(nombre, propuestas, true);

        if (propuestas > 0 && salieron == 0)
            return DelComando(nombre, 0, false, "esas ventanas las has colocado tú");

        return DelComando(nombre, salieron, false);
    }

    // Los de escritorio van aparte porque lo que importa es OTRA cosa: no cuántas ventanas se
    // tocan —ninguna—, sino a qué escritorio se iría. Y cuando no hay destino, el motivo: un
    // «Espacio →» que no hace nada en el último escritorio parece un atajo roto, y «no hay
    // escritorio a la derecha» se entiende a la primera.
    // El destino llega ya rotulado —«2 · Trabajo» o «Escritorio 3»— y no como número: el
    // mismo rótulo que enseña el indicador de escritorio de la barra. Dos formas distintas de
    // nombrar el mismo escritorio en la misma pantalla es una forma barata de confundir.
    public static Mensaje DelEspacio(string nombre, string? destino, string? porque)
    {
        var rotulo = HyprAtajos.Rotulo(nombre);
        if (destino is not { Length: > 0 })
            return new Mensaje(GlifoNada,
                               $"{rotulo}: {(porque is { Length: > 0 } p ? p : "no hay destino")}",
                               Tono.Aviso);

        // Dos puntos y no "·": el rótulo del escritorio ya trae el suyo ("2 · Trabajo"), y
        // con el otro delante salía «Espacio → · 2 · Trabajo», que se lee como una lista.
        // Sigue siendo simulación, y eso se dice: se nombra el destino Y que no se ha ido.
        return new Mensaje(GlifoEspacio,
                           $"{rotulo}: {destino} (simulado, no se ha cambiado)",
                           Tono.Aviso);
    }

    public static Mensaje DelDeshacer(int ventanas, bool simulado) =>
        ventanas <= 0
            ? new Mensaje(GlifoNada, "Deshacer: no hay nada que deshacer", Tono.Aviso)
            : new Mensaje(GlifoDeshacer,
                          simulado
                              ? $"Deshacer · simulado, {ventanas} ventana{(ventanas == 1 ? "" : "s")}"
                              : $"Deshacer · {ventanas} ventana{(ventanas == 1 ? "" : "s")}",
                          simulado ? Tono.Aviso : Tono.Normal);

    // Cuando el atajo lanza una excepción. No se enseña el mensaje de la excepción: en
    // pantalla y durante dos segundos no sirve de nada, y el sitio donde sí sirve —con su
    // traza— es el registro, que es donde ya está.
    public static Mensaje Reventado(string nombre) =>
        new(GlifoNada, $"{HyprAtajos.Rotulo(nombre)}: ha fallado (está en el registro)", Tono.Aviso);

    // Cuando la capa está apagada o en modo seguro. Sale en coral porque es lo contrario de
    // lo que el usuario espera al pulsar una tecla.
    public static Mensaje Apagado(bool modoSeguro) =>
        new(GlifoNada,
            modoSeguro
                ? "NubeHypr está en modo seguro: no organiza"
                : "NubeHypr está apagado",
            Tono.Aviso);

    // ================= Cuánto dura =================
    //
    // Un indicador que se va antes de poder leerlo no sirve, y uno que se queda tapa la
    // pantalla. Base y un poco por palabra, con tope: leer «Foco →» cuesta menos que leer
    // «Espacio →: no hay más escritorios».
    public static readonly TimeSpan Minimo = TimeSpan.FromMilliseconds(1200);
    public static readonly TimeSpan Maximo = TimeSpan.FromMilliseconds(3000);
    public const double MsPorPalabra = 180;

    public static TimeSpan Duracion(string texto)
    {
        int palabras = (texto ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var ms = Minimo.TotalMilliseconds + palabras * MsPorPalabra;
        return TimeSpan.FromMilliseconds(Math.Clamp(ms, Minimo.TotalMilliseconds,
                                                        Maximo.TotalMilliseconds));
    }

    // ================= Reaprovechar, no reabrir =================
    //
    // Pulsar `Foco →` cinco veces seguidas tiene que actualizar el texto, no abrir y cerrar
    // el panel cinco veces. Es exactamente lo que ya hace el aviso de volumen, y es la
    // diferencia entre un indicador y un parpadeo.
    public static bool SoloCambiarElTexto(bool yaEstaPuesto, Mensaje antes, Mensaje ahora) =>
        yaEstaPuesto && !(antes.Texto == ahora.Texto && antes.Como == ahora.Como);

    // Y si es EXACTAMENTE el mismo mensaje, ni eso: solo se alarga el tiempo. Repintar el
    // mismo texto no se ve como una actualización, se ve como un tirón.
    public static bool NiSiquieraEso(bool yaEstaPuesto, Mensaje antes, Mensaje ahora) =>
        yaEstaPuesto && antes.Texto == ahora.Texto && antes.Como == ahora.Como;
}
