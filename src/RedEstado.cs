using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// El estado del panel de red, decidido en un solo sitio y sin interfaz de por medio.
//
// ---------- El fallo del que sale ----------
//
// «Los botones del panel de red a veces aparecen y a veces no.» Tenía tres causas, y las
// tres son la misma de fondo: **el panel decidía qué dibujar mirando directamente los
// datos crudos, cada trozo por su cuenta.**
//
//  1. `Red.Wifis()` y `Red.Conexiones()` devuelven una lista vacía cuando falla, igual que
//     cuando de verdad no hay nada. Un fallo de un segundo —y los hay: al despertar de
//     suspensión, al reiniciar WLANSVC, al desenchufar el cable— se leía como «no hay
//     red», y el panel borraba la fila de la conexión con sus botones dentro.
//  2. Cada fila se construía con sus botones, así que no había forma de que un botón
//     siguiera ahí si su fila desaparecía. «No se puede hacer» y «no existe» se dibujaban
//     igual: no dibujando nada.
//  3. La cabecera leía `Red.Estado()` UNA vez, al abrir el panel, y ya no volvía a
//     mirarla. Con el panel abierto y el cable fuera, seguía diciendo «Conectado».
//
// ---------- Lo que hace esto ----------
//
// Una lectura entra, un estado sale. Siete estados, todos con nombre, todos con una frase
// para el usuario, y un conjunto FIJO de acciones que siempre están —habilitadas o no, y
// cuando no, con el motivo escrito—. El panel no decide nada: pregunta y pinta.
//
// Y la histéresis, que es la pieza que arregla el síntoma de verdad: hacen falta DOS
// lecturas seguidas diciendo lo mismo para abandonar un estado bueno. Una sola lectura
// vacía no cambia nada. Es la diferencia entre «parpadea» y «no parpadea», y no se puede
// poner en la interfaz: hay que recordar la lectura anterior, y eso es estado.
// `internal` y no `public` por una razón concreta: `Red` es internal, y esto habla de
// `Red.ComoFue` en su superficie. Hacer público lo de fuera para que encaje lo de dentro
// es la forma habitual de que una capa interna acabe siendo pública sin que nadie lo
// decida.
internal static class RedEstado
{
    // ---------- Los estados ----------
    //
    // `Cargando` es un estado de verdad y no un hueco: el panel se abre antes de tener
    // datos (a propósito: esperar a leer antes de dibujar se sentía como un tirón), y sin
    // nombrarlo ese primer instante se pintaba como «sin conexión».
    public enum Que
    {
        Cargando,       // aún no ha llegado ninguna lectura
        Conectado,      // hay al menos una conexión con salida
        SinConexion,    // hay adaptadores, pero ninguno saca a internet
        SinAdaptador,   // no hay ninguna interfaz utilizable
        SinTarjetaWifi, // hay red (o no), pero este equipo no tiene wifi
        SinPermisos,    // wlanapi dice acceso denegado: WLANSVC parado o política
        Error,          // Windows ha fallado de otra forma
    }

    // ---------- Una lectura ----------
    //
    // Lo mínimo para decidir, y nada de WPF ni de P/Invoke: así la decisión entera se
    // puede comprobar con una prueba pura, que es como se comprueban los siete estados sin
    // desenchufarle el cable a nadie.
    public readonly record struct Lectura(
        int Conexiones,             // cuántas conexiones con salida ha visto
        int Wifis,                  // cuántas redes wifi a la vista
        int Adaptadores,            // cuántas interfaces vivas (no loopback, no túnel)
        Red.ComoFue Wlan);          // qué dijo wlanapi al recorrer las interfaces

    // ---------- La decisión, pura ----------
    public static Que De(Lectura l)
    {
        // El orden importa, y es el orden en que las cosas tapan a las otras: un problema
        // de permisos hay que decirlo aunque además haya cable, porque explica por qué la
        // lista de wifis está vacía. Pero tener conexión manda sobre «no hay tarjeta
        // wifi»: en un sobremesa con cable, lo que el usuario quiere ver es su conexión.
        if (l.Wlan == Red.ComoFue.SinPermisos) return Que.SinPermisos;
        if (l.Conexiones > 0) return Que.Conectado;
        if (l.Wlan is Red.ComoFue.NoSePuedeAbrir or Red.ComoFue.NoSePuedenEnumerar) return Que.Error;
        if (l.Adaptadores == 0) return Que.SinAdaptador;
        if (l.Wlan == Red.ComoFue.SinTarjeta) return Que.SinTarjetaWifi;
        return Que.SinConexion;
    }

    // ---------- Qué lee el usuario ----------
    public static string Titulo(Que q) => q switch
    {
        Que.Cargando => "Mirando la red…",
        Que.Conectado => "Conectado",
        Que.SinConexion => "Sin conexión",
        Que.SinAdaptador => "Sin adaptadores de red",
        Que.SinTarjetaWifi => "Sin tarjeta Wi-Fi",
        Que.SinPermisos => "Wi-Fi no disponible",
        _ => "No se ha podido leer la red",
    };

    public static string Explicacion(Que q) => q switch
    {
        Que.Cargando => "Pidiéndole a Windows el estado de la red.",
        Que.Conectado => "",
        Que.SinConexion => "Hay adaptadores encendidos, pero ninguno llega a internet.",
        Que.SinAdaptador => "Windows no ve ninguna tarjeta de red encendida.",
        Que.SinTarjetaWifi => "Este equipo no tiene Wi-Fi, o está apagada desde el sistema.",
        Que.SinPermisos => "El servicio de Wi-Fi de Windows (WLANSVC) no responde o está " +
                           "bloqueado por una directiva. Las redes inalámbricas no se pueden listar.",
        _ => "Windows ha devuelto un error al consultar la red. Vuelve a intentarlo.",
    };

    // Si en este estado tiene sentido enseñar la sección de redes wifi. Ojo: «enseñarla»,
    // no «tener algo dentro». Con la sección escondida, el usuario que busca su wifi no
    // encuentra ni el sitio donde debería estar.
    public static bool HayQueEnseñarLasWifis(Que q) =>
        q is not (Que.SinTarjetaWifi or Que.SinAdaptador);

    // ================= Las acciones =================
    //
    // El conjunto es FIJO: estas cinco están siempre, en este orden, en los siete estados.
    // Lo que cambia es si se pueden pulsar y, si no, por qué — y el porqué se enseña.
    //
    // Que sea fijo no es cosmética. Un botón que desaparece deja al usuario sin saber si
    // la acción no existe, si está en otro sitio o si el programa está roto; y además
    // mueve de sitio a los que quedan, así que el que iba a pulsar «Actualizar» acaba
    // pulsando «Ajustes de Windows». Deshabilitado con su motivo contesta las tres cosas.
    public enum Accion { Actualizar, Conectar, Desconectar, MedirVelocidad, AjustesDeWindows }

    public static readonly Accion[] Todas =
    {
        Accion.Actualizar, Accion.Conectar, Accion.Desconectar,
        Accion.MedirVelocidad, Accion.AjustesDeWindows,
    };

    public static string Nombre(Accion a) => a switch
    {
        Accion.Actualizar => "Actualizar",
        Accion.Conectar => "Conectar",
        Accion.Desconectar => "Desconectar",
        Accion.MedirVelocidad => "Medir velocidad",
        _ => "Ajustes de Windows",
    };

    // null = se puede pulsar. Si no, el motivo, tal cual, para la ayuda emergente.
    //
    // `hayElegida` es si hay una wifi seleccionada en la lista; `midiendo`, si el test de
    // velocidad ya está en marcha.
    public static string? PorQueNo(Accion a, Que q, bool hayElegida, bool midiendo) => a switch
    {
        // Actualizar y Ajustes de Windows SIEMPRE se pueden. Son justo las dos que hay que
        // poder pulsar cuando algo va mal, y deshabilitarlas en el estado de error dejaba
        // al usuario mirando un panel roto sin ninguna salida.
        Accion.Actualizar => null,
        Accion.AjustesDeWindows => null,

        Accion.Conectar =>
            q == Que.SinPermisos ? "El servicio de Wi-Fi de Windows no responde"
          : q == Que.SinTarjetaWifi ? "Este equipo no tiene Wi-Fi"
          : q == Que.SinAdaptador ? "No hay ninguna tarjeta de red encendida"
          : q == Que.Cargando ? "Todavía se está leyendo la red"
          : !hayElegida ? "Elige antes una red de la lista"
          : null,

        Accion.Desconectar =>
            q == Que.Conectado ? null
          : q == Que.Cargando ? "Todavía se está leyendo la red"
          : "No hay ninguna conexión activa",

        _ =>                                   // MedirVelocidad
            midiendo ? "Ya se está midiendo"
          : q == Que.Conectado ? null
          : q == Que.Cargando ? "Todavía se está leyendo la red"
          : "Hace falta una conexión para medir la velocidad",
    };

    public static bool SePuede(Accion a, Que q, bool hayElegida, bool midiendo) =>
        PorQueNo(a, q, hayElegida, midiendo) == null;

    // ================= La histéresis =================
    //
    // Lo que impide que un parpadeo de Windows borre el panel.
    //
    // La regla: para SALIR de un estado hacen falta dos lecturas seguidas de acuerdo. Para
    // ENTRAR en uno bueno, una basta — recuperar la conexión tiene que notarse ya, y una
    // lectura que dice «hay red» no es un parpadeo: es un dato positivo, y los positivos
    // no se inventan solos.
    //
    // Asimétrico a propósito: lo que molesta es que desaparezca algo que estaba, no que
    // aparezca algo que vuelve.
    public sealed class Memoria
    {
        public const int LecturasParaDarloPorMalo = 2;

        public Que Ahora { get; private set; } = Que.Cargando;

        Que _candidato = Que.Cargando;
        int _seguidas;

        // Cuántas veces se ha descartado un cambio por ser el primero de su clase. Es el
        // número que enseña que la histéresis hace algo, y lo lee la prueba.
        public int ParpadeosIgnorados { get; private set; }

        public Que Encaja(Lectura l) => Encaja(De(l));

        public Que Encaja(Que visto)
        {
            if (visto == Ahora) { _candidato = visto; _seguidas = 0; return Ahora; }

            // Volver a estar conectado se cree a la primera.
            if (visto == Que.Conectado) { Ahora = visto; _candidato = visto; _seguidas = 0; return Ahora; }

            if (visto != _candidato) { _candidato = visto; _seguidas = 1; }
            else _seguidas++;

            if (_seguidas >= LecturasParaDarloPorMalo) { Ahora = visto; _seguidas = 0; }
            else ParpadeosIgnorados++;

            return Ahora;
        }

        // Al abrir el panel de nuevo se empieza de cero: lo que recordaba era de la vez
        // anterior y puede ser de hace horas.
        public void Olvidar()
        {
            Ahora = _candidato = Que.Cargando;
            _seguidas = 0;
        }
    }

    // ================= Y la lista de redes =================
    //
    // Mismo principio, un escalón más abajo: una lectura vacía no borra la lista que ya
    // había. La lista que se enseña es la última NO vacía, hasta que dos seguidas digan
    // que de verdad no hay nada.
    //
    // Es lo que hace que buscar redes no vacíe la lista mientras busca.
    public sealed class ListaEstable<T>
    {
        readonly List<T> _ultima = new();
        int _vaciasSeguidas;

        public IReadOnlyList<T> Ultima => _ultima;
        public bool EnsenandoLoDeAntes { get; private set; }

        public IReadOnlyList<T> Encaja(IReadOnlyList<T> recien)
        {
            if (recien.Count > 0)
            {
                _ultima.Clear();
                _ultima.AddRange(recien);
                _vaciasSeguidas = 0;
                EnsenandoLoDeAntes = false;
                return _ultima;
            }

            _vaciasSeguidas++;
            if (_vaciasSeguidas >= Memoria.LecturasParaDarloPorMalo)
            {
                _ultima.Clear();
                EnsenandoLoDeAntes = false;
            }
            else EnsenandoLoDeAntes = _ultima.Count > 0;

            return _ultima;
        }

        public void Olvidar() { _ultima.Clear(); _vaciasSeguidas = 0; EnsenandoLoDeAntes = false; }
    }
}
