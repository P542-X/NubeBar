using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E0: el gestor de superficies simultáneas.
//
// ---------- El problema ----------
//
// Hoy NubeBar tiene **un panel abierto a la vez**: `_panelAbierto` es un solo campo, y abrir
// cualquier panel cierra el anterior. Fue la decisión correcta para una isla con paneles —y
// gracias a ella un aviso de volumen no se traga el menú Inicio—, pero es incompatible con la
// composición de la macrofase E, donde el panel izquierdo, el derecho, el dock y una
// notificación pueden estar los cuatro a la vez.
//
// ---------- Qué es esto y qué no ----------
//
// Esto decide **qué superficies hay abiertas, en qué orden se pintan, cuál se cierra al hacer
// clic fuera y quién reserva espacio de trabajo**. No abre ventanas, no mide píxeles de la
// pantalla y no llama a Win32: recibe datos y devuelve decisiones, como todo lo de NubeHypr.
// Dibujarlas es de la isla.
//
// Que sea puro es lo que permite probar «cuatro superficies abiertas, cierro la segunda, ¿qué
// queda y en qué orden?» sin montar un escritorio entero.
//
// ---------- La compatibilidad, que no es opcional ----------
//
// Mientras la composición E esté apagada —y lo está de serie— el tablero se comporta como
// hasta ahora: los paneles clásicos son excluyentes entre sí. Así E0 entra sin cambiar lo que
// el usuario ve, y lo nuevo se enciende cuando esté listo.
public static class Superficies
{
    // ================= Qué clases de superficie hay =================
    //
    // La clase no es decoración: de ella salen el orden de pintado de serie, si reserva
    // espacio y qué hace un clic fuera. Dos superficies de la misma clase se tratan igual sin
    // tener que repetir la configuración en cada sitio que abra una.
    public enum Clase
    {
        Barra,            // fina y permanente; es la única que reserva espacio de trabajo
        Izquierda,        // panel lateral de ajustes y controles
        Derecha,          // panel contextual: multimedia, notificaciones, calendario
        Dock,             // favoritas y abiertas
        Notificaciones,   // los avisos pequeños de la esquina
        Multimedia,       // el centro multimedia, cuando vive suelto
        Overlay,          // la previsualización del ajuste, la vista general, los avisos
        Panel,            // los paneles clásicos de la isla (Inicio, audio, red…)
    }

    // ---------- Qué hace un clic fuera ----------
    //
    // Tres respuestas, no dos. «Se queda» es para la barra y el dock, que son permanentes.
    // «Se cierra» es para un panel que abriste a propósito. Y «se cierra si no estás con
    // ella» es para lo que se va solo con el tiempo —una notificación— pero no tiene por qué
    // irse porque hayas pulsado en otro sitio.
    public enum Clic { SeQueda, SeCierra, SeCierraSiNoLaUsas }

    // ---------- Una superficie ----------
    //
    // `Dueño` es la isla (el monitor) a la que pertenece: con dos pantallas hay dos barras y
    // dos docks, y cerrar el panel de una no puede cerrar el de la otra. Es la clave que
    // separa «todas las superficies» de «las de esta pantalla».
    public sealed record Superficie(
        string Id,
        Clase Que,
        string Dueño = "principal",
        NubeHypr.Borde? Borde = null,
        double Ancho = 0,
        double Alto = 0,
        int? Z = null,
        Clic AlClicFuera = Clic.SeCierra,
        bool PideTeclado = false,
        bool ReservaEspacio = false)
    {
        // El orden de pintado efectivo: el que se haya puesto a mano o el de su clase.
        public int ZEfectiva => Z ?? ZDeSerie(Que);

        public override string ToString() =>
            $"{Id}[{Que} {Dueño} z={ZEfectiva}" +
            (Borde is { } b ? $" {b}" : "") +
            (Ancho > 0 || Alto > 0 ? $" {Ancho:0}x{Alto:0}" : "") +
            (PideTeclado ? " teclado" : "") +
            (ReservaEspacio ? " reserva" : "") + "]";
    }

    // ---------- El orden de serie ----------
    //
    // De abajo arriba. La barra va al fondo porque es el suelo de la composición; los overlays
    // arriba del todo porque son lo que se superpone a todo por definición. Las
    // notificaciones van por encima de los paneles a propósito: un aviso que aparece detrás
    // del panel que tienes abierto es un aviso que no has visto.
    public static int ZDeSerie(Clase c) => c switch
    {
        Clase.Barra => 10,
        Clase.Dock => 20,
        Clase.Izquierda => 30,
        Clase.Derecha => 30,
        Clase.Multimedia => 35,
        Clase.Panel => 40,
        Clase.Notificaciones => 50,
        Clase.Overlay => 60,
        _ => 40,
    };

    // ---------- Lo que una clase hace de serie ----------
    //
    // Para no tener que repetir la política en cada sitio que abra una superficie. Quien
    // quiera otra cosa la pone en el record; quien no, hereda lo razonable.
    public static Superficie Nueva(string id, Clase que, string dueño = "principal") => new(
        Id: id,
        Que: que,
        Dueño: dueño,
        AlClicFuera: que switch
        {
            Clase.Barra or Clase.Dock => Clic.SeQueda,
            Clase.Notificaciones => Clic.SeCierraSiNoLaUsas,
            _ => Clic.SeCierra,
        },
        ReservaEspacio: que == Clase.Barra);

    // ---------- El resultado de abrir ----------
    public enum Resultado
    {
        Abierta,        // no estaba y ahora está
        Reemplazada,    // ya había una con ese id: se actualiza en su sitio, sin parpadeo
        Sustituyo,      // en modo clásico: cerró a la anterior de su clase
        Rechazada,      // id vacío, o algo que no se puede abrir
    }

    public sealed record Apertura(Resultado Que, Superficie? Abierta, IReadOnlyList<string> Cerradas)
    {
        public bool Vale => Que != Resultado.Rechazada;
    }

    // ================= El tablero =================
    public sealed class Tablero
    {
        readonly Dictionary<string, Superficie> _abiertas = new(StringComparer.OrdinalIgnoreCase);

        // El orden de llegada, para desempatar dos superficies con la misma Z. Sin esto, el
        // orden de pintado dependería del orden interno del diccionario, que no está
        // garantizado: dos arranques podrían pintar distinto.
        readonly List<string> _llegada = new();

        // ---------- El modo clásico ----------
        //
        // Encendido (lo de serie): los paneles son excluyentes entre sí, exactamente como
        // antes de E0. Apagado: conviven. Las demás clases —barra, dock, notificaciones—
        // conviven siempre, también en modo clásico: ya lo hacían.
        public bool ModoClasico { get; set; } = true;

        public int Cuantas => _abiertas.Count;

        public bool Hay(string id) => id is { Length: > 0 } && _abiertas.ContainsKey(id);

        public Superficie? De(string id) =>
            id is { Length: > 0 } && _abiertas.TryGetValue(id, out var s) ? s : null;

        // Lo que hay, de abajo arriba: por Z y, a igualdad, por orden de llegada.
        public IReadOnlyList<Superficie> Orden =>
            _abiertas.Values
                .OrderBy(s => s.ZEfectiva)
                .ThenBy(s => _llegada.IndexOf(s.Id))
                .ToList();

        public IReadOnlyList<Superficie> DelDueño(string dueño) =>
            Orden.Where(s => string.Equals(s.Dueño, dueño, StringComparison.OrdinalIgnoreCase))
                 .ToList();

        public IReadOnlyList<Superficie> DeClase(Clase c) =>
            Orden.Where(s => s.Que == c).ToList();

        // ---------- Abrir ----------
        public Apertura Abrir(Superficie s)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.Id))
                return new(Resultado.Rechazada, null, Array.Empty<string>());

            // Ya estaba: se reemplaza en su sitio y **conserva su orden de llegada**. Esto es
            // lo que hace que actualizar una superficie no la mande al frente ni la haga
            // parpadear — es el equivalente a reaprovechar el panel en vez de reabrirlo.
            if (_abiertas.ContainsKey(s.Id))
            {
                _abiertas[s.Id] = s;
                return new(Resultado.Reemplazada, s, Array.Empty<string>());
            }

            var cerradas = new List<string>();

            // En modo clásico, un panel echa a los demás paneles. Solo a los de su MISMO
            // dueño: con dos pantallas, abrir Inicio en una no puede cerrar el de la otra.
            if (ModoClasico && s.Que == Clase.Panel)
            {
                foreach (var otra in _abiertas.Values
                             .Where(x => x.Que == Clase.Panel &&
                                         string.Equals(x.Dueño, s.Dueño, StringComparison.OrdinalIgnoreCase))
                             .ToList())
                {
                    Cerrar(otra.Id);
                    cerradas.Add(otra.Id);
                }
            }

            _abiertas[s.Id] = s;
            _llegada.Add(s.Id);
            return new(cerradas.Count > 0 ? Resultado.Sustituyo : Resultado.Abierta, s, cerradas);
        }

        // ---------- Cerrar una, sin tocar las demás ----------
        //
        // Devuelve si de verdad había algo que cerrar. Cerrar lo que no está no es un error
        // —pasa continuamente, por ejemplo al cerrar dos veces— pero quien llama a veces
        // necesita saberlo para no animar una salida que no existe.
        public bool Cerrar(string id)
        {
            if (id is not { Length: > 0 } || !_abiertas.Remove(id)) return false;
            _llegada.Remove(id);
            return true;
        }

        // ---------- Actualizar sin reconstruir ----------
        //
        // El punto de E0 que más importa para el rendimiento: cambiar el tamaño de un panel o
        // su borde **no puede** significar cerrarlo y volver a abrirlo. Conserva el orden de
        // llegada, así que no se va al frente ni parpadea.
        public bool Actualizar(string id, Func<Superficie, Superficie> como)
        {
            if (De(id) is not { } vieja || como == null) return false;
            var nueva = como(vieja);
            if (nueva == null) return false;

            // El id no se puede cambiar por esta vía: sería cerrar una y abrir otra con otro
            // nombre, y el que llama creería que ha actualizado.
            if (!string.Equals(nueva.Id, vieja.Id, StringComparison.OrdinalIgnoreCase)) return false;

            _abiertas[id] = nueva;
            return true;
        }

        // ---------- Un clic fuera ----------
        //
        // Devuelve **qué se cierra**, no lo cierra: quien llama puede querer animarlo o
        // preguntar antes. `dondeSePulso` es el id de la superficie bajo el cursor, o null si
        // se pulsó en el escritorio.
        //
        // Una superficie nunca se cierra por un clic dentro de sí misma, obviamente; y
        // tampoco por un clic en otra de su mismo grupo cuando es `SeCierraSiNoLaUsas`, que
        // es como una notificación sobrevive a que pulses el dock.
        public IReadOnlyList<string> AlClicFuera(string? dondeSePulso)
        {
            var fuera = new List<string>();
            foreach (var s in Orden)
            {
                if (string.Equals(s.Id, dondeSePulso, StringComparison.OrdinalIgnoreCase)) continue;
                switch (s.AlClicFuera)
                {
                    case Clic.SeQueda:
                        break;
                    case Clic.SeCierra:
                        fuera.Add(s.Id);
                        break;
                    case Clic.SeCierraSiNoLaUsas:
                        // Un clic en el escritorio sí la cierra; un clic en otra superficie,
                        // no: estás usando el escritorio, no descartando el aviso.
                        if (dondeSePulso == null) fuera.Add(s.Id);
                        break;
                }
            }
            return fuera;
        }

        // ---------- Quién reserva espacio ----------
        //
        // Solo una por dueño, y es la barra. Dos superficies reservando en el mismo monitor
        // significaría descontar dos veces del área de trabajo y dejar un hueco muerto; y el
        // dock, en esta composición, **flota por encima** en vez de robar sitio, que es lo que
        // deja el escritorio amplio.
        public Superficie? Reserva(string dueño) =>
            DelDueño(dueño).FirstOrDefault(s => s.ReservaEspacio);

        // ---------- Quién manda en el teclado ----------
        //
        // La de más arriba que lo pida. Si no lo pide ninguna, nadie: el foco se queda donde
        // estaba, que es lo que permite abrir un panel sin que la ventana con la que trabajas
        // pierda el cursor de texto.
        public Superficie? Teclado =>
            Orden.LastOrDefault(s => s.PideTeclado);

        // ---------- Cerrar todo ----------
        //
        // Al apagar NubeBar. Devuelve los ids en orden INVERSO al de pintado: se desmonta de
        // arriba abajo, igual que se montó de abajo arriba. Cerrar la barra antes que el panel
        // que vive encima deja un instante con el panel colgando de nada.
        public IReadOnlyList<string> CerrarTodo()
        {
            var ids = Orden.Reverse().Select(s => s.Id).ToList();
            _abiertas.Clear();
            _llegada.Clear();
            return ids;
        }

        // Para el registro y los diagnósticos: una línea con lo que hay.
        public string Linea() =>
            Cuantas == 0 ? "sin superficies"
                         : $"{Cuantas} superficie(s): " + string.Join(" · ", Orden.Select(s => s.ToString()));
    }
}
