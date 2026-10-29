using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// La confirmación antes de mover ventanas de verdad.
//
// ---------- Por qué existe esto ----------
//
// `OpcionesHypr.ConfirmarMovimientos` llevaba desde D0 en la configuración, puesta a `true`,
// escrita en el `config.json` del usuario por `Esquema.cs`… y **no la leía nadie**. Tres
// menciones en todo el proyecto: la declaración, el esquema y nada más.
//
// Eso no es una clave sin usar: es una mentira en un fichero que habla de seguridad. Quien
// abría su configuración leía «ConfirmarMovimientos: true» y entendía que NubeHypr pregunta
// antes de tocarle las ventanas. No preguntaba.
//
// ---------- Por qué NO es un diálogo modal ----------
//
// Porque la barra es una ventana `WS_EX_NOACTIVATE` y `AplicarConRedes` se llama desde el
// manejador de un atajo y desde el oyente de reglas, los dos en el hilo de la interfaz. Un
// `ShowDialog` ahí congela la barra entera, y una barra congelada en un reemplazo de shell no
// es un error de una función: es el escritorio parado.
//
// Así que la confirmación es **asíncrona**: se guarda la petición, se pregunta, y el lote se
// aplica —o no— cuando el usuario contesta. `AplicarConRedes` devuelve `null`, que es lo que
// ya devolvía cuando no había nada que aplicar.
//
// ---------- La regla que lo hace seguro ----------
//
// **No contestar es no mover.** No hay plazo que venza aplicando, no hay confirmación por
// omisión y no hay botón preseleccionado. Si el usuario ignora la pregunta, cierra el panel o
// se va a comer, las ventanas se quedan donde están. Un fallo en este código solo puede
// hacer que NubeHypr mueva MENOS de lo pedido, nunca más.
internal static class HyprConfirmar
{
    // ---------- Lo que caduca y por qué ----------
    //
    // Los efectos se calcularon contra las ventanas de un momento concreto. Contestar «sí» a
    // los veinte minutos aplicaría un reparto pensado para una pantalla que ya no es esa: las
    // ventanas se han movido, han aparecido otras y algunas se han cerrado.
    //
    // Treinta segundos es el tiempo de mirar la barra, leer una línea y decidir. Pasado eso
    // la pregunta deja de valer, y lo que hay que hacer es volver a pedir el reparto, no
    // resucitar el de antes.
    internal static readonly TimeSpan Caducidad = TimeSpan.FromSeconds(30);

    internal sealed record Peticion(
        string Porque,
        IReadOnlyList<EfectoVentana> Efectos,
        DateTime Cuando)
    {
        internal bool Caducada(DateTime ahora) => ahora - Cuando >= Caducidad;

        // Lo que le queda de vida, para poder decirlo en pantalla sin que el número sea
        // negativo cuando ya venció.
        internal int SegundosQueQuedan(DateTime ahora) =>
            Math.Max(0, (int)Math.Ceiling((Caducidad - (ahora - Cuando)).TotalSeconds));
    }

    // ---------- ¿Hay que preguntar? ----------
    //
    // Cuatro condiciones, y las cuatro tienen que darse:
    //
    //  - **Modo aplicar.** En simulación no se mueve nada, así que preguntar sería pedir
    //    permiso para escribir una línea en el registro.
    //  - **La opción puesta.** Es del usuario, y quien la apaga está diciendo «muévelas sin
    //    preguntar», que es una respuesta legítima.
    //  - **Que el movimiento sea AUTOMÁTICO.** Ver abajo: es la condición que decide si esta
    //    opción es útil o insufrible.
    //  - **Y que no venga ya contestada.** Cuando el usuario pulsa «Mover», el lote vuelve a
    //    entrar por la misma puerta con el permiso en la mano; si no se mirara esto,
    //    preguntaría otra vez y nunca se movería nada.
    //
    // ---------- Lo de «automático» no es un matiz: es el diseño ----------
    //
    // Un atajo que acabas de pulsar YA es la confirmación. Si `Win+Alt+→` abriera un panel
    // preguntando «¿mover 1 ventana?», cada pulsación costaría dos gestos y el usuario
    // apagaría la opción en diez minutos — y entonces tampoco protegería de lo que importa.
    //
    // Lo que hay que confirmar es lo que NubeHypr hace **por su cuenta**: una regla que se
    // dispara porque has abierto una ventana, un reparto que salta al cambiar de layout.
    // Ahí no has pedido nada, y ahí es donde una pregunta vale.
    //
    // De paso, esto deja el arnés de D9.5 funcionando sin tocarlo: `--arnesaplicar` pide
    // mover explícitamente, igual que un atajo, así que no pregunta y sigue demostrando de
    // extremo a extremo que las reglas mueven ventanas de verdad.
    internal static bool HaceFalta(NubeHypr.Modo modo, bool opcion, bool automatico,
                                   bool yaConcedida) =>
        modo == NubeHypr.Modo.Aplicar && opcion && automatico && !yaConcedida;

    // ---------- Qué se le dice al usuario ----------
    //
    // El número de ventanas y el motivo, no la lista de rectángulos. «Mover 4 ventanas» se
    // entiende; «colocar 0x1A2B3C en 12,54,1264x680» es para el registro.
    //
    // Y en plural o singular de verdad: «1 ventanas» es el detalle que hace que un programa
    // parezca a medio hacer.
    internal static string Texto(Peticion p)
    {
        int n = CuantasVentanas(p.Efectos);
        return n == 1
            ? $"NubeHypr quiere mover 1 ventana ({p.Porque})."
            : $"NubeHypr quiere mover {n} ventanas ({p.Porque}).";
    }

    // Ventanas, no efectos: una misma ventana puede llevar «restaurar», «colocar» y
    // «enfocar», y decir «mover 3 ventanas» cuando es una sola sería contar mal delante del
    // usuario, que tiene la pantalla al lado para desmentirlo.
    internal static int CuantasVentanas(IReadOnlyList<EfectoVentana> efectos) =>
        efectos.Where(e => e.Tipo != TipoEfecto.SeQueda)
               .Select(e => e.Hwnd)
               .Distinct()
               .Count();

    // El desglose, para la línea de debajo: qué se les va a hacer. Ordenado y sin repetir,
    // porque un lote de diez ventanas da «colocar» diez veces.
    internal static string Detalle(IReadOnlyList<EfectoVentana> efectos)
    {
        var que = efectos.Where(e => e.Tipo != TipoEfecto.SeQueda)
                         .Select(e => EnPalabras(e.Tipo))
                         .Distinct()
                         .ToList();
        return que.Count == 0 ? "" : string.Join(", ", que);
    }

    static string EnPalabras(TipoEfecto t) => t switch
    {
        TipoEfecto.Colocar => "cambiarlas de sitio y de tamaño",
        TipoEfecto.Enfocar => "pasar el foco",
        TipoEfecto.AEspacio => "llevarlas a otro escritorio",
        TipoEfecto.Restaurar => "sacarlas de maximizadas",
        TipoEfecto.Devolver => "devolverlas a como estaban",
        _ => "dejarlas quietas",
    };

    // ---------- Una cada vez ----------
    //
    // Si llega una petición nueva con otra pendiente, la nueva sustituye a la vieja. Dos
    // preguntas a la vez obligan al usuario a adivinar cuál está contestando, y encolarlas
    // es peor: contestaría la primera y se le aplicaría la segunda sin verla.
    //
    // Devuelve lo que se descarta, para poder apuntarlo: una petición que desaparece sin
    // dejar rastro en el registro es media hora de no entender por qué no se movió nada.
    internal static (Peticion Nueva, Peticion? Descartada) Sustituir(
        Peticion? pendiente, Peticion nueva) => (nueva, pendiente);

    // ---------- La línea del registro ----------
    internal static string Linea(string que, Peticion p, DateTime ahora) =>
        $"[D9] confirmación {que}: «{p.Porque}» · " +
        $"{CuantasVentanas(p.Efectos)} ventana(s), {p.Efectos.Count} efecto(s) · " +
        $"pedida hace {(ahora - p.Cuando).TotalSeconds:0.0} s";
}
