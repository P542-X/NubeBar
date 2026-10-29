using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// El panel de red, comprobado sin desenchufarle el cable a nadie.
//
// Todo lo que decide el panel vive en `RedEstado`, que es lógica pura: una lectura entra,
// un estado sale. Por eso los siete estados —incluidos los que en este equipo no se pueden
// provocar: sin permisos, sin adaptadores, error de Windows— se comprueban igual de bien
// que el normal.
public static partial class Pruebas
{
    internal static void CorrerRed()
    {
        RedLosSieteEstados();
        RedLosBotonesNoDesaparecen();
        RedUnaLecturaVaciaNoBorraNada();
        RedLaListaNoParpadea();
    }

    static RedEstado.Lectura L(int con = 0, int wifis = 0, int adap = 1,
                               Red.ComoFue wlan = Red.ComoFue.Bien) =>
        new(con, wifis, adap, wlan);

    // ================= Los siete estados =================
    static void RedLosSieteEstados()
    {
        Grupo("red: cada situación tiene un estado con nombre");

        Igual(RedEstado.Que.Conectado, RedEstado.De(L(con: 1, wifis: 5)),
              "con una conexión con salida: Conectado");
        Igual(RedEstado.Que.Conectado, RedEstado.De(L(con: 1, wifis: 0, wlan: Red.ComoFue.SinTarjeta)),
              "y con cable en un equipo sin wifi, también: lo que importa es que hay red");
        Igual(RedEstado.Que.SinConexion, RedEstado.De(L(con: 0, wifis: 3, adap: 2)),
              "con adaptadores pero sin salida: Sin conexión");
        Igual(RedEstado.Que.SinAdaptador, RedEstado.De(L(con: 0, adap: 0)),
              "sin ninguna interfaz viva: Sin adaptadores");
        Igual(RedEstado.Que.SinTarjetaWifi, RedEstado.De(L(con: 0, adap: 1, wlan: Red.ComoFue.SinTarjeta)),
              "con adaptador pero sin tarjeta wifi: Sin tarjeta Wi-Fi");
        Igual(RedEstado.Que.SinPermisos, RedEstado.De(L(con: 1, adap: 1, wlan: Red.ComoFue.SinPermisos)),
              "acceso denegado manda incluso con conexión: explica por qué no hay wifis");
        Igual(RedEstado.Que.Error, RedEstado.De(L(con: 0, adap: 1, wlan: Red.ComoFue.NoSePuedeAbrir)),
              "wlanapi que no abre: Error");
        Igual(RedEstado.Que.Error, RedEstado.De(L(con: 0, adap: 1, wlan: Red.ComoFue.NoSePuedenEnumerar)),
              "y wlanapi que no enumera: Error");

        // Y todos, sin excepción, tienen algo que enseñar. Un estado sin texto es un panel
        // en blanco, que es indistinguible de uno roto.
        foreach (var q in Enum.GetValues<RedEstado.Que>())
        {
            Vale(RedEstado.Titulo(q).Length > 0, $"«{q}» tiene título");
            // Conectado es el único sin explicación, y a propósito: lo que hay que leer son
            // los datos de la conexión, no una frase diciendo que va bien.
            if (q != RedEstado.Que.Conectado)
                Vale(RedEstado.Explicacion(q).Length > 20,
                     $"«{q}» explica qué pasa y no solo cómo se llama");
        }

        // La sección wifi se esconde solo cuando no puede haber wifis. En «sin permisos» se
        // queda, con su explicación dentro: esconderla sería ocultar justo el sitio donde
        // el usuario va a buscar por qué no ve su red.
        Vale(RedEstado.HayQueEnseñarLasWifis(RedEstado.Que.SinPermisos),
             "con permisos denegados la sección wifi sigue a la vista, con el motivo");
        Vale(!RedEstado.HayQueEnseñarLasWifis(RedEstado.Que.SinTarjetaWifi),
             "sin tarjeta wifi no se enseña: no hay nada que pueda salir ahí");
    }

    // ================= Los botones no desaparecen =================
    static void RedLosBotonesNoDesaparecen()
    {
        Grupo("red: las cinco acciones están en los siete estados");

        foreach (var q in Enum.GetValues<RedEstado.Que>())
            foreach (bool elegida in new[] { false, true })
                foreach (bool midiendo in new[] { false, true })
                {
                    // El conjunto es fijo: cinco, siempre, en el mismo orden. Lo que cambia
                    // es si se pueden pulsar.
                    Igual(5, RedEstado.Todas.Length, $"[{q}] siguen siendo cinco acciones");

                    foreach (var a in RedEstado.Todas)
                    {
                        Vale(RedEstado.Nombre(a).Length > 0, $"[{q}] «{a}» tiene nombre");

                        // Y la regla que de verdad arregla el fallo: si no se puede, hay un
                        // motivo escrito. Nunca «no se puede» a secas, y nunca desaparecer.
                        string? no = RedEstado.PorQueNo(a, q, elegida, midiendo);
                        if (no != null)
                            Vale(no.Length > 10,
                                 $"[{q}] «{a}» deshabilitada explica por qué ({no})");
                    }
                }

        // Las dos salidas de emergencia se pueden pulsar SIEMPRE. Son justo las que hacen
        // falta cuando algo va mal, y deshabilitarlas en el estado de error dejaba al
        // usuario mirando un panel roto sin nada que tocar.
        foreach (var q in Enum.GetValues<RedEstado.Que>())
        {
            Vale(RedEstado.SePuede(RedEstado.Accion.Actualizar, q, false, false),
                 $"[{q}] «Actualizar» siempre se puede pulsar");
            Vale(RedEstado.SePuede(RedEstado.Accion.AjustesDeWindows, q, false, false),
                 $"[{q}] «Ajustes de Windows» siempre se puede pulsar");
        }

        // Y las que dependen del estado, dependen del estado y no de un azar de lectura.
        Vale(RedEstado.SePuede(RedEstado.Accion.Desconectar, RedEstado.Que.Conectado, false, false),
             "conectado: se puede desconectar");
        Vale(!RedEstado.SePuede(RedEstado.Accion.Desconectar, RedEstado.Que.SinConexion, false, false),
             "sin conexión: no, y con su motivo");
        Vale(!RedEstado.SePuede(RedEstado.Accion.MedirVelocidad, RedEstado.Que.Conectado, false, midiendo: true),
             "midiendo ya, no se puede volver a medir");
        Vale(!RedEstado.SePuede(RedEstado.Accion.Conectar, RedEstado.Que.SinConexion, hayElegida: false, false),
             "sin red elegida no se puede conectar");
        Vale(RedEstado.SePuede(RedEstado.Accion.Conectar, RedEstado.Que.SinConexion, hayElegida: true, false),
             "con una elegida, sí");
    }

    // ================= Una lectura vacía no borra nada =================
    static void RedUnaLecturaVaciaNoBorraNada()
    {
        Grupo("red: un parpadeo de Windows no vacía el panel");

        var m = new RedEstado.Memoria();
        Igual(RedEstado.Que.Cargando, m.Ahora, "de entrada, Cargando");

        Igual(RedEstado.Que.Conectado, m.Encaja(L(con: 1, wifis: 4)),
              "la primera lectura buena se cree a la primera");

        // EL CASO. Una lectura vacía —la que llega al despertar de suspensión, al reiniciar
        // WLANSVC o mientras Windows reenumera— no puede tirar la conexión.
        Igual(RedEstado.Que.Conectado, m.Encaja(L(con: 0, wifis: 0)),
              "una sola lectura vacía NO cambia el estado");
        Igual(1, m.ParpadeosIgnorados, "y queda constancia de que se ha ignorado");

        Igual(RedEstado.Que.Conectado, m.Encaja(L(con: 1, wifis: 4)),
              "y al volver la buena, sigue conectado sin haber parpadeado");

        // Dos seguidas sí: eso ya no es un parpadeo, es que se ha ido la red.
        m.Encaja(L(con: 0, wifis: 0));
        Igual(RedEstado.Que.SinConexion, m.Encaja(L(con: 0, wifis: 0)),
              "dos lecturas seguidas diciendo lo mismo sí cambian el estado");

        // Recuperarse es inmediato: lo que molesta es que desaparezca algo que estaba, no
        // que vuelva algo que había vuelto.
        Igual(RedEstado.Que.Conectado, m.Encaja(L(con: 1)),
              "y volver a tener red se nota en la primera lectura");

        // Dos lecturas malas DISTINTAS tampoco cuentan: no están de acuerdo en nada.
        m.Encaja(L(con: 0, adap: 0));                      // SinAdaptador
        Igual(RedEstado.Que.Conectado, m.Encaja(L(con: 0, adap: 1)),   // SinConexion
              "dos lecturas malas que no coinciden no mueven el estado");

        // Y al reabrir el panel se empieza de cero.
        m.Olvidar();
        Igual(RedEstado.Que.Cargando, m.Ahora, "al reabrir el panel, Cargando otra vez");
    }

    // ================= La lista tampoco parpadea =================
    static void RedLaListaNoParpadea()
    {
        Grupo("red: la lista de wifis no se vacía mientras se busca");

        var lista = new RedEstado.ListaEstable<string>();
        var tres = new List<string> { "casa", "vecino", "cafeteria" };
        var vacia = new List<string>();

        Igual(3, lista.Encaja(tres).Count, "entran tres redes");
        Vale(!lista.EnsenandoLoDeAntes, "y son las de ahora");

        // Escanear vacía la caché de Windows un instante. Sin esto, pulsar «buscar» hacía
        // desaparecer la lista entera y volver medio segundo después: el salto que hacía
        // que pulsar en una red fallara porque la fila ya no estaba donde estaba.
        Igual(3, lista.Encaja(vacia).Count, "una lectura vacía no la borra");
        Vale(lista.EnsenandoLoDeAntes, "y se sabe que lo que se ve es lo de antes");

        Igual(3, lista.Encaja(tres).Count, "vuelve la lista y sigue habiendo tres");
        Vale(!lista.EnsenandoLoDeAntes, "ya no es lo de antes");

        lista.Encaja(vacia);
        Igual(0, lista.Encaja(vacia).Count, "dos vacías seguidas sí la vacían");
        Vale(!lista.EnsenandoLoDeAntes, "y entonces no se está enseñando nada viejo");

        lista.Olvidar();
        Igual(0, lista.Ultima.Count, "al reabrir el panel, vacía");
    }
}
