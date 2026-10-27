using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E0: las pruebas del gestor de superficies.
//
// Todo puro: ni una ventana, ni un píxel. Lo que se comprueba es lo que el gestor decide —qué
// hay abierto, en qué orden, qué se cierra con un clic fuera, quién reserva espacio— que es
// justo lo que no se puede comprobar mirando una captura.
public static partial class Pruebas
{
    internal static void CorrerE0()
    {
        E0AbrirYCerrarSinTocarLasDemas();
        E0VariasALaVez();
        E0ElOrdenDePintado();
        E0ActualizarNoReconstruye();
        E0ElClicFuera();
        E0ElModoClasicoSeComportaComoAntes();
        E0VariosMonitores();
        E0LimpiezaAlCerrar();
    }

    static Superficies.Tablero NuevoTablero(bool clasico = false) =>
        new() { ModoClasico = clasico };

    // ================= Abrir y cerrar =================
    static void E0AbrirYCerrarSinTocarLasDemas()
    {
        Grupo("E0: abrir y cerrar una superficie no toca a las demás");

        var t = NuevoTablero();
        Igual(0, t.Cuantas, "un tablero nace vacío");

        var r = t.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra));
        Vale(r.Vale && r.Que == Superficies.Resultado.Abierta, "la barra se abre");
        Vale(t.Hay("barra"), "y está");
        Igual(1, t.Cuantas, "hay una");

        // ---------- Lo que no está ----------
        Vale(!t.Hay("loquesea"), "preguntar por lo que no hay no revienta");
        Vale(t.De("loquesea") == null, "ni devuelve nada");
        Vale(!t.Cerrar("loquesea"), "cerrar lo que no está dice que no había nada");
        Igual(1, t.Cuantas, "y no se lleva por delante lo que sí estaba");

        // ---------- Lo que no se puede abrir ----------
        foreach (var malo in new[] { "", "   " })
            Vale(!t.Abrir(Superficies.Nueva(malo, Superficies.Clase.Panel)).Vale,
                 $"una superficie con id «{malo}» se rechaza");
        Vale(!t.Abrir(null!).Vale, "y una nula también");
        Igual(1, t.Cuantas, "ninguna de las rechazadas se cuela");

        // ---------- Cerrar la que es ----------
        t.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock));
        t.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda));
        Igual(3, t.Cuantas, "tres abiertas");

        Vale(t.Cerrar("izq"), "se cierra la de en medio");
        Igual(2, t.Cuantas, "quedan dos");
        Vale(t.Hay("barra") && t.Hay("dock"), "y son las otras dos, intactas");
        Vale(!t.Hay("izq"), "la cerrada ya no está");

        Vale(!t.Cerrar("izq"), "cerrarla otra vez ya no hace nada");
    }

    // ================= Dos, tres y cuatro a la vez =================
    static void E0VariasALaVez()
    {
        Grupo("E0: dos, tres y cuatro superficies simultáneas");

        // La composición de la macrofase E al completo. Si esto no se sostiene, no hay E.
        var todas = new[]
        {
            ("barra",  Superficies.Clase.Barra),
            ("dock",   Superficies.Clase.Dock),
            ("izq",    Superficies.Clase.Izquierda),
            ("der",    Superficies.Clase.Derecha),
            ("notas",  Superficies.Clase.Notificaciones),
            ("medios", Superficies.Clase.Multimedia),
            ("overlay",Superficies.Clase.Overlay),
        };

        for (int cuantas = 2; cuantas <= todas.Length; cuantas++)
        {
            var t = NuevoTablero();
            foreach (var (id, c) in todas.Take(cuantas))
                Vale(t.Abrir(Superficies.Nueva(id, c)).Vale, $"[{cuantas}] «{id}» abre");

            Igual(cuantas, t.Cuantas, $"con {cuantas} abiertas, hay {cuantas}");
            Igual(cuantas, t.Orden.Count, "y todas salen en el orden de pintado");
            Igual(cuantas, t.Orden.Select(s => s.Id).Distinct().Count(),
                  "sin repetirse ninguna");

            // Y se cierran de una en una sin llevarse a las vecinas.
            foreach (var (id, _) in todas.Take(cuantas))
            {
                int antes = t.Cuantas;
                Vale(t.Cerrar(id), $"[{cuantas}] «{id}» se cierra");
                Igual(antes - 1, t.Cuantas, $"[{cuantas}] y solo se cierra ella");
            }
            Igual(0, t.Cuantas, $"[{cuantas}] al final no queda ninguna");
        }

        // ---------- Abrir dos veces la misma ----------
        //
        // No puede duplicarla: sería pintar dos paneles idénticos uno encima de otro, y el
        // segundo cierre dejaría el primero huérfano.
        var u = NuevoTablero();
        u.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda));
        var otra = u.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda) with { Ancho = 320 });
        Igual(Superficies.Resultado.Reemplazada, otra.Que, "abrir la misma la reemplaza");
        Igual(1, u.Cuantas, "y no la duplica");
        Igual(320.0, u.De("izq")!.Ancho, "con los valores nuevos");
    }

    // ================= El orden de pintado =================
    static void E0ElOrdenDePintado()
    {
        Grupo("E0: el orden de pintado es estable y tiene sentido");

        var t = NuevoTablero();
        // A propósito en desorden: el orden de apertura no puede decidir quién tapa a quién.
        t.Abrir(Superficies.Nueva("overlay", Superficies.Clase.Overlay));
        t.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra));
        t.Abrir(Superficies.Nueva("notas", Superficies.Clase.Notificaciones));
        t.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock));

        var ids = t.Orden.Select(s => s.Id).ToList();
        Igual("barra", ids[0], "la barra va al fondo: es el suelo de la composición");
        Igual("dock", ids[1], "después el dock");
        Igual("notas", ids[2], "las notificaciones por encima de los paneles");
        Igual("overlay", ids[3], "y los overlays arriba del todo");

        // Un aviso detrás del panel que tienes abierto es un aviso que no has visto.
        Vale(Superficies.ZDeSerie(Superficies.Clase.Notificaciones) >
             Superficies.ZDeSerie(Superficies.Clase.Panel),
             "una notificación se pinta por encima de un panel");
        Vale(Superficies.ZDeSerie(Superficies.Clase.Barra) <
             Superficies.ZDeSerie(Superficies.Clase.Overlay),
             "y la barra por debajo de un overlay");

        // ---------- Dos con la misma Z ----------
        //
        // El panel izquierdo y el derecho empatan a propósito. Desempata el orden de llegada,
        // y eso tiene que ser estable: si dependiera del diccionario, dos arranques podrían
        // pintar distinto y nadie sabría por qué.
        var e = NuevoTablero();
        e.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda));
        e.Abrir(Superficies.Nueva("der", Superficies.Clase.Derecha));
        Igual("izq", e.Orden[0].Id, "a igual Z, manda el orden de llegada");
        Igual("der", e.Orden[1].Id, "la segunda va encima");

        for (int i = 0; i < 20; i++)
            Igual("izq", e.Orden[0].Id, "y el orden no cambia al volver a preguntarlo");

        // ---------- Una Z a mano gana ----------
        var m = NuevoTablero();
        m.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra) with { Z = 999 });
        m.Abrir(Superficies.Nueva("overlay", Superficies.Clase.Overlay));
        Igual("barra", m.Orden[^1].Id, "una Z puesta a mano manda sobre la de la clase");
    }

    // ================= Actualizar sin reconstruir =================
    static void E0ActualizarNoReconstruye()
    {
        Grupo("E0: actualizar una superficie no la reconstruye");

        var t = NuevoTablero();
        t.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda));
        t.Abrir(Superficies.Nueva("der", Superficies.Clase.Derecha));
        t.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock));

        var antes = t.Orden.Select(s => s.Id).ToList();

        Vale(t.Actualizar("izq", s => s with { Ancho = 340, Alto = 700 }),
             "se puede actualizar una superficie abierta");
        Igual(340.0, t.De("izq")!.Ancho, "el ancho nuevo está");
        Igual(700.0, t.De("izq")!.Alto, "y el alto");
        Igual(3, t.Cuantas, "sin abrir ni cerrar nada");

        // ---------- Y NO se va al frente ----------
        //
        // Es la mitad que importa. Si actualizar reordenara, cambiar el tamaño del panel
        // izquierdo lo pondría encima del derecho, y el usuario vería saltar la composición
        // por haber movido un deslizador.
        Igual(string.Join(",", antes), string.Join(",", t.Orden.Select(s => s.Id)),
              "y el orden de pintado no cambia");

        // ---------- Lo que no se puede ----------
        Vale(!t.Actualizar("noexiste", s => s), "actualizar lo que no hay dice que no");
        Vale(!t.Actualizar("izq", null!), "sin función, tampoco");
        Vale(!t.Actualizar("izq", _ => null!), "ni devolviendo nada");
        Vale(!t.Actualizar("izq", s => s with { Id = "otra" }),
             "y NO se puede cambiar el id: eso sería cerrar una y abrir otra a escondidas");
        Vale(t.Hay("izq") && !t.Hay("otra"), "así que «izq» sigue siendo «izq»");
    }

    // ================= El clic fuera =================
    static void E0ElClicFuera()
    {
        Grupo("E0: qué se cierra al pulsar fuera");

        var t = NuevoTablero();
        t.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra));
        t.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock));
        t.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda));
        t.Abrir(Superficies.Nueva("notas", Superficies.Clase.Notificaciones));

        // ---------- Clic en el escritorio ----------
        var enElEscritorio = t.AlClicFuera(null);
        Vale(!enElEscritorio.Contains("barra"), "la barra NO se cierra: es permanente");
        Vale(!enElEscritorio.Contains("dock"), "ni el dock");
        Vale(enElEscritorio.Contains("izq"), "el panel sí se cierra");
        Vale(enElEscritorio.Contains("notas"), "y la notificación también");

        // ---------- Clic en el dock ----------
        //
        // Una notificación no se descarta porque pulses el dock: estás usando el escritorio,
        // no diciéndole al aviso que se vaya.
        var enElDock = t.AlClicFuera("dock");
        Vale(enElDock.Contains("izq"), "pulsar el dock cierra el panel abierto");
        Vale(!enElDock.Contains("notas"), "pero NO la notificación");
        Vale(!enElDock.Contains("dock"), "y el dock no se cierra a sí mismo");

        // ---------- Clic dentro del propio panel ----------
        var dentro = t.AlClicFuera("izq");
        Vale(!dentro.Contains("izq"), "pulsar dentro de un panel no lo cierra");

        // ---------- Y no cierra nada por su cuenta ----------
        //
        // Devuelve qué se cerraría; cerrarlo es de quien llama, que puede querer animarlo o
        // preguntar antes.
        Igual(4, t.Cuantas, "preguntar por el clic fuera no cierra nada todavía");
    }

    // ================= El modo clásico =================
    static void E0ElModoClasicoSeComportaComoAntes()
    {
        Grupo("E0: con la composición E apagada, se comporta como antes");

        // Esta es la prueba que permite meter E0 sin cambiar lo que el usuario ve hoy.
        var t = NuevoTablero(clasico: true);

        t.Abrir(Superficies.Nueva("inicio", Superficies.Clase.Panel));
        var segundo = t.Abrir(Superficies.Nueva("audio", Superficies.Clase.Panel));

        Igual(Superficies.Resultado.Sustituyo, segundo.Que, "un panel echa al anterior");
        Vale(segundo.Cerradas.Contains("inicio"), "y dice a cuál echó");
        Igual(1, t.Cuantas, "solo hay un panel a la vez");
        Vale(t.Hay("audio") && !t.Hay("inicio"), "y es el último");

        // ---------- Pero la barra y el dock conviven igual ----------
        //
        // Ya convivían antes de E0: la barra siempre ha estado ahí mientras había un panel
        // abierto. El modo clásico es solo sobre los paneles.
        t.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra));
        t.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock));
        t.Abrir(Superficies.Nueva("notas", Superficies.Clase.Notificaciones));
        Igual(4, t.Cuantas, "barra, dock y notificación conviven con el panel");

        var tercero = t.Abrir(Superficies.Nueva("red", Superficies.Clase.Panel));
        Vale(tercero.Cerradas.Contains("audio"), "y el panel nuevo sigue echando al anterior");
        Vale(t.Hay("barra") && t.Hay("dock") && t.Hay("notas"),
             "sin tocar a la barra, el dock ni la notificación");

        // ---------- Con la composición E encendida, conviven ----------
        var e = NuevoTablero(clasico: false);
        e.Abrir(Superficies.Nueva("inicio", Superficies.Clase.Panel));
        var dos = e.Abrir(Superficies.Nueva("audio", Superficies.Clase.Panel));
        Igual(Superficies.Resultado.Abierta, dos.Que, "con E encendida, no echa a nadie");
        Igual(2, e.Cuantas, "y los dos paneles conviven");
    }

    // ================= Varios monitores =================
    static void E0VariosMonitores()
    {
        Grupo("E0: cada pantalla tiene sus superficies");

        var t = NuevoTablero(clasico: true);
        t.Abrir(Superficies.Nueva("barra-1", Superficies.Clase.Barra, "pantalla1"));
        t.Abrir(Superficies.Nueva("barra-2", Superficies.Clase.Barra, "pantalla2"));
        t.Abrir(Superficies.Nueva("inicio-1", Superficies.Clase.Panel, "pantalla1"));
        t.Abrir(Superficies.Nueva("inicio-2", Superficies.Clase.Panel, "pantalla2"));

        Igual(4, t.Cuantas, "dos barras y dos paneles, uno por pantalla");
        Igual(2, t.DelDueño("pantalla1").Count, "cada pantalla ve las suyas");
        Igual(2, t.DelDueño("pantalla2").Count, "y la otra, las suyas");
        Igual(0, t.DelDueño("pantalla9").Count, "y una que no existe, ninguna");

        // ---------- El modo clásico es POR PANTALLA ----------
        //
        // Abrir Inicio en una pantalla no puede cerrar el panel de la otra: son dos islas
        // distintas y el usuario puede estar usando las dos.
        var otro = t.Abrir(Superficies.Nueva("audio-1", Superficies.Clase.Panel, "pantalla1"));
        Vale(otro.Cerradas.Contains("inicio-1"), "echa al panel de SU pantalla");
        Vale(!otro.Cerradas.Contains("inicio-2"), "y NO al de la otra");
        Vale(t.Hay("inicio-2"), "que sigue abierto");

        // ---------- Reservar espacio: una por pantalla ----------
        Igual("barra-1", t.Reserva("pantalla1")?.Id, "la barra de la pantalla 1 reserva");
        Igual("barra-2", t.Reserva("pantalla2")?.Id, "y la de la 2, la suya");
        Vale(t.Reserva("pantalla9") == null, "donde no hay barra, no se reserva nada");

        // El dock NO reserva: flota por encima, que es lo que deja el escritorio amplio.
        var d = NuevoTablero();
        d.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock));
        Vale(d.Reserva("principal") == null, "el dock no roba área de trabajo");
    }

    // ================= Limpieza al cerrar =================
    static void E0LimpiezaAlCerrar()
    {
        Grupo("E0: al cerrar NubeBar no queda ninguna superficie");

        var t = NuevoTablero();
        foreach (var (id, c) in new[]
        {
            ("barra", Superficies.Clase.Barra),
            ("dock", Superficies.Clase.Dock),
            ("izq", Superficies.Clase.Izquierda),
            ("der", Superficies.Clase.Derecha),
            ("notas", Superficies.Clase.Notificaciones),
        })
            t.Abrir(Superficies.Nueva(id, c));

        Igual(5, t.Cuantas, "cinco superficies abiertas");

        var cerradas = t.CerrarTodo();
        Igual(5, cerradas.Count, "se cierran las cinco");
        Igual(0, t.Cuantas, "y no queda ninguna");

        // ---------- De arriba abajo ----------
        //
        // Se desmonta al revés de como se monta. Cerrar la barra antes que el panel que vive
        // encima deja un instante con el panel colgando de nada — y eso, en ventanas de
        // verdad, es un parpadeo.
        Igual("notas", cerradas[0], "se desmonta primero lo de más arriba");
        Igual("barra", cerradas[^1], "y la barra, la última");

        // ---------- Y cerrar todo dos veces no revienta ----------
        Igual(0, t.CerrarTodo().Count, "cerrar todo otra vez no devuelve nada");
        Igual(0, t.Cuantas, "y sigue vacío");

        // ---------- Un tablero cerrado sirve otra vez ----------
        //
        // Al recargar la configuración se desmonta y se vuelve a montar. Si CerrarTodo dejara
        // el tablero inservible, recargar el tema apagaría el escritorio.
        Vale(t.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra)).Vale,
             "después de cerrarlo todo, se puede volver a abrir");
        Igual(1, t.Cuantas, "y cuenta bien");
        Igual("barra", t.Orden[0].Id, "y el orden vuelve a empezar limpio");

        // ---------- El teclado ----------
        var k = NuevoTablero();
        k.Abrir(Superficies.Nueva("barra", Superficies.Clase.Barra));
        Vale(k.Teclado == null, "si nadie pide teclado, nadie lo tiene");
        k.Abrir(Superficies.Nueva("izq", Superficies.Clase.Izquierda) with { PideTeclado = true });
        Igual("izq", k.Teclado?.Id, "lo tiene quien lo pide");
        k.Abrir(Superficies.Nueva("overlay", Superficies.Clase.Overlay) with { PideTeclado = true });
        Igual("overlay", k.Teclado?.Id, "y si lo piden dos, el de más arriba");
        k.Cerrar("overlay");
        Igual("izq", k.Teclado?.Id, "al cerrarse, vuelve al de debajo");

        // La línea del registro no revienta en ningún estado.
        Vale(NuevoTablero().Linea().Length > 0, "un tablero vacío tiene línea de registro");
        Vale(k.Linea().Contains("izq"), "y uno lleno nombra lo que hay");
    }
}
