using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E1: las pruebas de la composición de la barra.
//
// Puras. Lo que se comprueba es que la composición es correcta ANTES de aplicarla: que no
// pide módulos que no existen, que no repite, que cabe, y —lo que más importa— que probarla
// y deshacerla devuelve exactamente lo que había.
public static partial class Pruebas
{
    internal static void CorrerE1()
    {
        E1LasComposicionesSonValidas();
        E1LaVerticalNoLlevaEtiquetasLargas();
        E1RevisarCazaLoQueEstaMal();
        E1AplicarYDevolverNoDejanRastro();
        E1ElBordeDecideLaComposicion();
    }

    // ---------- Los módulos que existen: los de VERDAD ----------
    //
    // La primera versión de esto era una lista escrita a mano con los ids que yo creía que
    // había. Incluía «red». No existe: el estado de la conexión vive dentro del centro de
    // control y en la bandeja. La prueba pasaba porque se comprobaba contra mi propia lista,
    // y el fallo lo cazó el diagnóstico al aplicar la composición en la barra real.
    //
    // Una prueba que compara una suposición consigo misma no comprueba nada. Así que se
    // pregunta al registro, que es quien decide.
    static bool Existe(string m)
    {
        RegistroWidgets.Asegurar();
        return RegistroWidgets.Existe(m);
    }

    // ================= Las dos composiciones =================
    static void E1LasComposicionesSonValidas()
    {
        Grupo("E1: las composiciones de la barra son aplicables");

        Igual(2, Composicion.Todas.Length, "hay dos composiciones: horizontal y vertical");

        foreach (var c in Composicion.Todas)
        {
            Vale(c.Nombre.Length > 0, "cada composición tiene nombre");
            Vale(c.Que.Length > 20, $"«{c.Nombre}»: y explicación de qué es");
            Vale(c.Cuantos > 0, $"«{c.Nombre}»: tiene módulos");

            var quejas = Composicion.Revisar(c, Existe);
            var graves = quejas.Where(q => q.Grave).ToList();
            Vale(graves.Count == 0,
                 $"«{c.Nombre}»: sin problemas graves ({string.Join(" / ", graves.Select(g => g.Texto))})");
            Vale(Composicion.SePuedeAplicar(c, Existe), $"«{c.Nombre}»: se puede aplicar");

            // Ninguna repite módulos: hay un reloj, una bandeja y una nube.
            Igual(c.Cuantos, c.Todos.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                  $"«{c.Nombre}»: no repite ningún módulo");

            // Todos existen de verdad.
            foreach (var m in c.Todos)
                Vale(Existe(m), $"«{c.Nombre}»: el módulo «{m}» existe");

            Vale(c.Grosor is >= 24 and <= 120, $"«{c.Nombre}»: el grosor es de barra fina");
            Vale(c.ToString().Contains(c.Nombre), $"«{c.Nombre}»: su línea de registro la nombra");
        }

        // ---------- Lo que la composición tiene que traer ----------
        //
        // El documento de E pide que la barra lleve escritorios, apps activas, estado de red,
        // audio, batería, notificaciones, reloj, acceso al centro de control y estado de la
        // nube. Si alguien quita uno al reordenar, esto lo dice.
        foreach (var c in Composicion.Todas)
            foreach (var imprescindible in new[] { "escritorios", "apps", "bateria",
                                                   "notificaciones", "reloj", "control", "nube" })
                Vale(c.Todos.Contains(imprescindible, StringComparer.OrdinalIgnoreCase),
                     $"«{c.Nombre}» lleva «{imprescindible}», que E1 pide");

        // El audio llega por el centro de control, que es donde vive el mezclador: no hace
        // falta un módulo aparte y meterlo sería un segundo sitio para lo mismo.
        Vale(Composicion.Horizontal.Todos.Contains("musica"),
             "y la horizontal lleva el centro multimedia, que en vertical no cabe");
    }

    // ================= Vertical =================
    static void E1LaVerticalNoLlevaEtiquetasLargas()
    {
        Grupo("E1: la composición vertical no mete etiquetas que no caben");

        var v = Composicion.Vertical;
        Vale(PanelNube.EsVertical(v.Modo, v.Posicion), "la vertical es vertical de verdad");

        // Este es el motivo de que haya dos composiciones y no una. Una columna de 44 px no
        // admite «CPU 12%» ni una fecha de forma legible: meterlos no da una barra vertical,
        // da una columna de texto cortado.
        foreach (var largo in Composicion.DeTextoLargo)
            Vale(!v.Todos.Contains(largo, StringComparer.OrdinalIgnoreCase),
                 $"la vertical NO lleva «{largo}»");

        // El reloj sí, porque sabe escribirse en dos líneas desde B6.
        Vale(v.Todos.Contains("reloj"), "pero sí el reloj, que sabe apilarse");

        Vale(v.Cuantos < Composicion.Horizontal.Cuantos,
             "la vertical lleva menos módulos que la horizontal");
        Vale(v.Grosor > Composicion.Horizontal.Grosor,
             "y más grosor: en vertical el grosor es el ANCHO, y 38 px deja los iconos pegados");

        // ---------- Y el aviso sale si alguien los mete ----------
        var forzada = v with { Derecha = v.Derecha.Concat(new[] { "cpu" }).ToList() };
        var quejas = Composicion.Revisar(forzada, Existe);
        Vale(quejas.Any(q => q.Texto.Contains("cpu") && !q.Grave),
             "meter «cpu» en vertical avisa, pero no lo impide: puede ser a propósito");
    }

    // ================= Revisar =================
    static void E1RevisarCazaLoQueEstaMal()
    {
        Grupo("E1: una composición rota no se aplica a medias");

        var buena = Composicion.Horizontal;

        // ---------- Un módulo que no existe ----------
        //
        // Es lo que pasaría con una composición escrita para una versión con un módulo que
        // luego se quitó. Grave: la barra no sabría qué pintar ahí.
        var inventado = buena with { Centro = new[] { "modulo-que-no-existe" } };
        var q1 = Composicion.Revisar(inventado, Existe);
        Vale(q1.Any(q => q.Grave && q.Texto.Contains("modulo-que-no-existe")),
             "un módulo inexistente es grave");
        Vale(!Composicion.SePuedeAplicar(inventado, Existe), "y no se aplica");

        // ---------- Vacía ----------
        var vacia = buena with
        {
            Izquierda = Array.Empty<string>(),
            Centro = Array.Empty<string>(),
            Derecha = Array.Empty<string>(),
        };
        Vale(Composicion.Revisar(vacia, Existe).Any(q => q.Grave), "una barra vacía es grave");

        // ---------- Grosor ----------
        foreach (double g in new[] { 0.0, 10, 200, 1000 })
            Vale(Composicion.Revisar(buena with { Grosor = g }, Existe).Any(q => q.Grave),
                 $"un grosor de {g:0} px se rechaza");
        foreach (double g in new[] { 24.0, 38, 44, 120 })
            Vale(Composicion.SePuedeAplicar(buena with { Grosor = g }, Existe),
                 $"y uno de {g:0} px vale");

        // ---------- Repetidos: avisa, no impide ----------
        var repe = buena with { Centro = new[] { "reloj", "reloj" } };
        var q2 = Composicion.Revisar(repe, Existe);
        Vale(q2.Any(q => q.Texto.Contains("2 veces")), "un módulo repetido se dice");
        Vale(q2.All(q => !q.Grave || !q.Texto.Contains("veces")),
             "pero repetir no es grave: el separador se repite a propósito");

        // ---------- Nula ----------
        Vale(Composicion.Revisar(null!, Existe).Any(q => q.Grave),
             "revisar nada no revienta y se queja");

        // ---------- Buscar ----------
        Vale(Composicion.Buscar("escritorio-vertical") != null, "se busca por nombre");
        Vale(Composicion.Buscar("ESCRITORIO-VERTICAL") != null, "sin distinguir mayúsculas");
        Vale(Composicion.Buscar("no-existe") == null, "y lo que no hay, no sale");
        Vale(Composicion.Buscar(null) == null, "ni con null");
    }

    // ================= Aplicar y devolver =================
    static void E1AplicarYDevolverNoDejanRastro()
    {
        Grupo("E1: probar una composición y deshacerla devuelve lo que había");

        // La distribución del usuario, con sus módulos en su orden.
        var mios = new Modulos
        {
            Izquierda = new() { "inicio" },
            Centro = new() { "fijadas", "apps", "reloj" },
            Derecha = new() { "cpu", "ram", "bandeja", "control" },
        };

        var habia = Composicion.Apuntar(mios, "isla", "arriba", 38);

        // ---------- Se aplica ----------
        Composicion.Aplicar(Composicion.Vertical, mios);
        Igual(string.Join(",", Composicion.Vertical.Izquierda), string.Join(",", mios.Izquierda),
              "la zona de inicio es la de la composición");
        Igual(string.Join(",", Composicion.Vertical.Derecha), string.Join(",", mios.Derecha),
              "y la del final también");
        Vale(!mios.Derecha.Contains("cpu"), "y lo que el usuario tenía ya no está");

        // ---------- Se deshace ----------
        //
        // Esta es LA prueba del grupo. Si deshacer no devolviera exactamente lo que había,
        // probar una composición le costaría al usuario su distribución de módulos.
        Composicion.Devolver(habia, mios);
        Igual("inicio", string.Join(",", mios.Izquierda), "vuelve su zona de inicio");
        Igual("fijadas,apps,reloj", string.Join(",", mios.Centro), "vuelve su centro");
        Igual("cpu,ram,bandeja,control", string.Join(",", mios.Derecha), "y su zona del final");

        // ---------- Y se MODIFICAN las listas, no se sustituyen ----------
        //
        // Esta prueba existe por un fallo que las demás no podían cazar, porque miraban el
        // contenido y el fallo estaba en la IDENTIDAD de la lista.
        //
        // `PanelPrincipal.Zonas` no copia las listas de `Modulos`: las referencia. Sustituir
        // `m.Izquierda` por otra lista deja al panel apuntando a la vieja — el modelo cambia,
        // la barra no. Y el registro decía que la composición se había aplicado.
        //
        // Lo cazó una captura: la foto de la composición horizontal enseñaba la barra del
        // usuario. Aquí se comprueba sin mirar, quedándose con la referencia de antes.
        var vivo = new Modulos
        {
            Izquierda = new() { "inicio" },
            Centro = new() { "apps" },
            Derecha = new() { "reloj" },
        };
        var refIzq = vivo.Izquierda;
        var refCen = vivo.Centro;
        var refDer = vivo.Derecha;

        Composicion.Aplicar(Composicion.Horizontal, vivo);
        Vale(ReferenceEquals(refIzq, vivo.Izquierda),
             "aplicar MODIFICA la lista de la izquierda, no la sustituye");
        Vale(ReferenceEquals(refCen, vivo.Centro), "ni la del centro");
        Vale(ReferenceEquals(refDer, vivo.Derecha), "ni la de la derecha");
        Vale(refIzq.Contains("escritorios"),
             "y quien tuviera guardada esa referencia ve el cambio");

        var h4 = Composicion.Apuntar(vivo, "barra", "arriba", 38);
        Composicion.Aplicar(Composicion.Vertical, vivo);
        Composicion.Devolver(h4, vivo);
        Vale(ReferenceEquals(refIzq, vivo.Izquierda), "devolver tampoco sustituye");
        Vale(ReferenceEquals(refDer, vivo.Derecha), "en ninguna de las tres zonas");

        // ---------- Y el apunte es una COPIA ----------
        //
        // Si `Apuntar` guardara las mismas listas en vez de copiarlas, aplicar las
        // machacaría y deshacer repondría lo que acabas de poner: el usuario perdería su
        // distribución y encima parecería que deshacer funciona.
        var m2 = new Modulos { Izquierda = new() { "inicio" }, Centro = new(), Derecha = new() };
        var h2 = Composicion.Apuntar(m2, "isla", "arriba", 38);
        m2.Izquierda.Add("apps");                       // se toca la lista viva
        Igual(1, h2.Izquierda.Count, "el apunte es una copia, no la lista viva");

        // ---------- Ida y vuelta muchas veces ----------
        //
        // Probar las dos composiciones una detrás de otra y volver tiene que acabar igual que
        // empezó. Un rastro de un ciclo se multiplica por los que haga el usuario mirando.
        var m3 = new Modulos
        {
            Izquierda = new() { "inicio", "escritorios" },
            Centro = new() { "apps" },
            Derecha = new() { "reloj", "control" },
        };
        var h3 = Composicion.Apuntar(m3, "barra", "abajo", 40);
        string antes = $"{string.Join(",", m3.Izquierda)}|{string.Join(",", m3.Centro)}|{string.Join(",", m3.Derecha)}";

        for (int i = 0; i < 5; i++)
        {
            Composicion.Aplicar(Composicion.Horizontal, m3);
            Composicion.Aplicar(Composicion.Vertical, m3);
            Composicion.Devolver(h3, m3);
        }

        Igual(antes, $"{string.Join(",", m3.Izquierda)}|{string.Join(",", m3.Centro)}|{string.Join(",", m3.Derecha)}",
              "cinco idas y vueltas dejan la distribución exactamente igual");
        Igual("barra", h3.Modo, "y el apunte conserva el modo");
        Igual("abajo", h3.Posicion, "la posición");
        Igual(40.0, h3.Grosor, "y el grosor");
    }

    // ================= El borde decide =================
    static void E1ElBordeDecideLaComposicion()
    {
        Grupo("E1: la composición sale del borde, no de un ajuste aparte");

        // Que dependa del borde es lo que hace que mover la barra a un lateral no deje una
        // fila de etiquetas cortadas.
        foreach (var pos in new[] { "arriba", "abajo" })
        {
            Igual(Composicion.Horizontal.Nombre, Composicion.ParaElBorde("barra", pos).Nombre,
                  $"en «barra/{pos}» toca la horizontal");
            Igual(Composicion.Horizontal.Nombre, Composicion.ParaElBorde("isla", pos).Nombre,
                  $"y en «isla/{pos}» también");
        }

        foreach (var pos in new[] { "izquierda", "derecha" })
            Igual(Composicion.Vertical.Nombre, Composicion.ParaElBorde("barra", pos).Nombre,
                  $"en «barra/{pos}» toca la vertical");

        // Ojo con el matiz que ya existía: «isla» en un lateral NO es una barra vertical, es
        // una isla anclada a ese lado, que sigue siendo horizontal. La composición tiene que
        // respetarlo o pondría una columna de iconos en una isla tumbada.
        foreach (var pos in new[] { "izquierda", "derecha" })
            Igual(Composicion.Horizontal.Nombre, Composicion.ParaElBorde("isla", pos).Nombre,
                  $"pero «isla/{pos}» es una isla anclada, horizontal: le toca la horizontal");

        Vale(Composicion.ParaElBorde(null, null) != null, "sin datos no revienta");
    }
}
