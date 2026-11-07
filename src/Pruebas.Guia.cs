using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Las pruebas de la guía de reglas.
//
// Lo que se puede comprobar sin abrir una ventana: que los ocho ejemplos son reglas de
// verdad, que la frase en castellano dice lo que la regla hace, que los avisos salen cuando
// tienen que salir, y que «probar» no toca nada.
//
// Lo que NO se puede: que la página se vea bien. Eso va por `--capturas`.
public static partial class Pruebas
{
    internal static void CorrerGuia()
    {
        GuiaLosEjemplosSonReglasDeVerdad();
        GuiaLaFraseDiceLoQueHaceLaRegla();
        GuiaLosProblemasSeDicenEnCastellano();
        GuiaProbarNoMueveNada();
        GuiaUsarUnEjemploNoDecideNada();
        GuiaElModoDeDemostracion();
    }

    // ================= El modo de demostración =================
    //
    // «Modo seguro» no es una promesa que se escribe en un comentario: son cuatro garantías
    // que se pueden comprobar una a una, y aquí se comprueban.
    static void GuiaElModoDeDemostracion()
    {
        Grupo("guía: el modo de demostración no puede tocar nada");

        // ---------- 1. Las dos banderas valen y no escriben ----------
        foreach (var b in new[] { DiagGuia.Bandera, DiagGuia.BanderaAlias })
        {
            Vale(App.BanderaConocida(b), $"«{b}» la conoce el arranque");
            Vale(Config.MiraYNoToca(new[] { b }), $"«{b}» mira y no toca");
            Vale(!Config.PuedeEscribir(new[] { b }), $"«{b}» no puede escribir el config");
            Vale(DiagGuia.Es(b), $"«{b}» la reconoce DiagGuia");
            Vale(Config.BanderasQueMiran.Contains(b, StringComparer.OrdinalIgnoreCase),
                 $"«{b}» está en la lista de las que solo miran");
        }

        Vale(DiagGuia.Es("--GUIA"), "no distingue mayúsculas");
        Vale(!DiagGuia.Es("--guiaX") && !DiagGuia.Es(null) && !DiagGuia.Es(""),
             "y no se confunde con otra cosa");

        // ---------- 2. Apagar, de verdad ----------
        //
        // El caso que importa es el del usuario que SÍ tiene NubeHypr encendido en su
        // fichero: la demostración no puede heredar eso.
        var encendido = new Config();
        encendido.NubeHypr.Activo = true;
        encendido.NubeHypr.Modo = "aplicar";

        bool antes = DiagGuia.Encendido;
        try
        {
            DiagGuia.Encendido = true;
            DiagGuia.Apagar(encendido);
            Vale(!encendido.NubeHypr.Activo, "con el modo puesto, NubeHypr queda apagado");
            Igual("simulacion", encendido.NubeHypr.Modo,
                  "y el modo baja a simulación aunque el fichero dijera «aplicar»");

            // Y no revienta con lo que puede llegarle.
            DiagGuia.Apagar(null!);
            DiagGuia.Apagar(new Config { NubeHypr = null! });
            Vale(true, "no revienta con un config nulo ni sin sección");
        }
        finally { DiagGuia.Encendido = antes; }

        // ---------- 3. Sin el modo, no se toca nada ----------
        //
        // La otra mitad, y la que de verdad hay que proteger: esta función corre dentro de
        // `Config.Cargar`, o sea en TODOS los arranques. Si apagara NubeHypr sin la bandera,
        // habría roto la capa para el uso normal.
        var normal = new Config();
        normal.NubeHypr.Activo = true;
        normal.NubeHypr.Modo = "aplicar";
        DiagGuia.Encendido = false;
        DiagGuia.Apagar(normal);
        Vale(normal.NubeHypr.Activo,
             "sin el modo, una configuración encendida se queda encendida");
        Igual("aplicar", normal.NubeHypr.Modo, "y su modo no se toca");

        // ---------- 4. El rótulo existe y dice lo que tiene que decir ----------
        Vale(DiagGuia.Rotulo.Contains("demostración"), "el rótulo nombra la demostración");
        Vale(DiagGuia.Rotulo.Contains("guarda"), "y dice que no se guarda nada");
    }

    // ================= Los ocho ejemplos =================
    static void GuiaLosEjemplosSonReglasDeVerdad()
    {
        Grupo("guía: los ejemplos son reglas que funcionan");

        // El documento pide ocho cosas concretas. Si mañana alguien borra una tarjeta, esto
        // lo dice en vez de dejar la guía coja sin que nadie se entere.
        Igual(8, HyprGuia.Ejemplos.Length, "hay ocho ejemplos");

        foreach (var e in HyprGuia.Ejemplos)
        {
            Vale(e.Que.Length > 0, "cada ejemplo tiene titular");
            Vale(e.Explicacion.Length > 20, $"«{e.Que}»: tiene explicación de verdad");
            Vale(e.Campos.Count > 0, $"«{e.Que}»: dice qué campos rellena");
            Vale(e.Frase.Length > 0, $"«{e.Que}»: tiene frase legible");

            // ---------- Lo importante: que la regla del ejemplo SEA válida ----------
            //
            // Un ejemplo que al usarlo deja el formulario en rojo es peor que no tener
            // ejemplo: enseña a escribir una regla rota. Se comprueba con la misma
            // `Valida` que usa el editor, pero encendida, porque los ejemplos nacen
            // apagados a propósito y «apagada» no es un defecto.
            var encendida = e.Regla with { Activa = true };
            Vale(HyprReglas.Valida(encendida, out string porque),
                 $"«{e.Que}»: la regla es válida ({porque})");

            Vale(!encendida.SinCriterios, $"«{e.Que}»: dice a qué ventanas se aplica");
            Vale(!encendida.SinAcciones, $"«{e.Que}»: dice qué hacer con ellas");

            // Y ninguno trae un problema GRAVE. Los suaves sí valen: el ejemplo de
            // combinar trae «flotante + …» a propósito.
            var graves = HyprGuia.Problemas(encendida).Where(p => p.Grave).ToList();
            Vale(graves.Count == 0,
                 $"«{e.Que}»: sin problemas graves ({string.Join(" / ", graves.Select(g => g.Texto))})");
        }

        // ---------- Que estén los ocho temas que se pidieron ----------
        //
        // No por el titular, que puede reescribirse, sino por lo que CADA uno enseña a hacer.
        var reglas = HyprGuia.Ejemplos.Select(e => e.Regla).ToList();
        Vale(reglas.Any(r => r.Espacio != null), "hay un ejemplo de enviar a un escritorio");
        Vale(reglas.Any(r => r.Flotante == true), "hay uno de flotante");
        Vale(reglas.Any(r => r.Monitor != null), "hay uno de elegir monitor");
        Vale(reglas.Any(r => r.Titulo != null), "hay uno de filtrar por título");
        Vale(reglas.Any(r => r.Layout != null), "hay uno de layout");
        Vale(reglas.Any(r => r.Ancho != null || r.Alto != null), "hay uno de tamaño");
        Vale(reglas.Any(r => r.Espacio != null && r.Flotante == true),
             "hay uno que COMBINA escritorio y flotante, como se pidió");
        Vale(HyprGuia.ComoSeResuelvenLosEmpates.Length > 200,
             "y está explicado qué pasa si dos reglas coinciden");

        // Todos los ejemplos nacen apagados: usar uno rellena el formulario, no decide.
        Vale(HyprGuia.Ejemplos.All(e => !e.Regla.Activa),
             "todos los ejemplos nacen APAGADOS");

        // Y con nombres de proceso de verdad, no con «MiAplicacion».
        Vale(reglas.Any(r => r.Proceso == "Discord"), "se usa una aplicación real como ejemplo");
        Vale(reglas.All(r => r.Proceso == null ||
                             !r.Proceso.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)),
             "y ningún ejemplo escribe el proceso con «.exe», que es el fallo que enseña a evitar");
    }

    // ================= La frase =================
    static void GuiaLaFraseDiceLoQueHaceLaRegla()
    {
        Grupo("guía: la regla dicha en castellano");

        // El caso exacto del documento.
        var discord = new HyprReglas.Regla("x", Proceso: "Discord", Espacio: 2, Flotante: true);
        var f = HyprGuia.EnPalabras(discord);
        Vale(f.Contains("Discord"), "nombra la aplicación");
        Vale(f.Contains("Escritorio 2"), "dice a qué escritorio");
        Vale(f.Contains("flotante"), "y que la deja flotante");
        Vale(f.StartsWith("Cuando aparezca"), "empieza por «Cuando aparezca»");
        Vale(f.EndsWith("."), "y es una frase, con su punto");

        // ---------- Una regla sin acciones no dice que haga algo ----------
        var soloCriterio = new HyprReglas.Regla("x", Proceso: "Discord");
        Vale(HyprGuia.EnPalabras(soloCriterio).Contains("falta decir qué quieres"),
             "sin acciones, lo dice en vez de inventarse una frase");

        // ---------- Y una sin criterios tampoco ----------
        var soloAccion = new HyprReglas.Regla("x", Espacio: 2);
        Vale(HyprGuia.EnPalabras(soloAccion).Contains("nunca se activará"),
             "sin criterios, avisa de que no se activará");

        // ---------- Apagada se dice ----------
        Vale(HyprGuia.EnPalabras(discord with { Activa = false }).Contains("apagada"),
             "una regla apagada lo dice en su propia frase");
        Vale(!HyprGuia.EnPalabras(discord).Contains("apagada"),
             "y una encendida no lo dice");

        // ---------- Los cuatro layouts tienen nombre ----------
        foreach (var l in Enum.GetValues<NubeHypr.Layout>())
        {
            var n = HyprGuia.NombreDeLayout(l);
            Vale(n.Length > l.ToString().Length,
                 $"el layout {l} se explica, no solo se nombra");
            Vale(HyprGuia.EnPalabras(new HyprReglas.Regla("x", Proceso: "a", Layout: l)).Contains(n),
                 $"y la frase usa esa explicación para {l}");
        }

        // «horizontal» reparte en COLUMNAS, que es al revés de lo que parece. Si alguien
        // invierte eso, que salte aquí y no en la cabeza de quien lea la guía.
        Vale(HyprGuia.NombreDeLayout(NubeHypr.Layout.Horizontal).Contains("columnas"),
             "«horizontal» se explica como columnas");
        Vale(HyprGuia.NombreDeLayout(NubeHypr.Layout.Vertical).Contains("filas"),
             "y «vertical» como filas");

        // ---------- El tamaño, en palabras ----------
        Vale(HyprGuia.Tamano(0.5, null).Contains("media pantalla"), "0,5 es media pantalla");
        Vale(HyprGuia.Tamano(0.5, null).Contains("como estuviera"),
             "y un alto sin poner se dice, no se inventa un cero");
        Vale(HyprGuia.Tamano(null, null).Length > 0, "sin nada no revienta");

        // ---------- Todos los campos aparecen en la frase ----------
        var todo = new HyprReglas.Regla("x", Prioridad: 3, Proceso: "a", Clase: "b",
                                        Titulo: "c", Ruta: "d", Espacio: 1, Monitor: 0,
                                        Layout: NubeHypr.Layout.Vertical, Ancho: 0.5,
                                        Alto: 0.5, Flotante: false, Foco: true);
        var completa = HyprGuia.EnPalabras(todo);
        foreach (var trozo in new[] { "a", "b", "c", "d", "Escritorio 1", "monitor 0" })
            Vale(completa.Contains(trozo), $"la frase completa menciona «{trozo}»");

        // Y los campos que toca se listan, para poder enseñarlos antes de abrir el formulario.
        Igual(11, HyprGuia.CamposDe(todo).Count, "con todo relleno se listan los once campos");
        Igual(0, HyprGuia.CamposDe(new HyprReglas.Regla("x")).Count, "y con nada, ninguno");
    }

    // ================= Los avisos =================
    static void GuiaLosProblemasSeDicenEnCastellano()
    {
        Grupo("guía: lo que va mal se dice en claro");

        // ---------- Los dos graves, los dos a la vez ----------
        //
        // `HyprReglas.Valida` da uno y para. Aquí tienen que salir los dos: arreglar uno,
        // guardar y encontrarse con el siguiente es lo que hace que un editor parezca que va
        // a trompicones.
        var vacia = new HyprReglas.Regla("x");
        var p = HyprGuia.Problemas(vacia);
        Vale(p.Count(x => x.Grave) >= 2, "una regla vacía enseña sus dos problemas a la vez");
        Vale(p.Any(x => x.Texto.Contains("a qué ventanas")), "dice que faltan criterios");
        Vale(p.Any(x => x.Texto.Contains("qué hacer")), "y que faltan acciones");
        Vale(p.All(x => !x.Texto.Contains("SinCriterios")),
             "y lo dice en castellano, no con el nombre del campo");

        // ---------- El escritorio 0 ----------
        //
        // Es el error de cálculo más fácil: los monitores empiezan en 0 y los escritorios en
        // 1. Quien acaba de leer «Monitor: 0 es el principal» escribe «Espacio 0».
        Vale(HyprGuia.Problemas(new HyprReglas.Regla("x", Proceso: "a", Espacio: 0))
                     .Any(x => x.Texto.Contains("desde 1")),
             "el escritorio 0 se explica, no se rechaza sin más");

        // ---------- El «.exe», que es EL fallo clásico ----------
        var conExe = HyprGuia.Problemas(new HyprReglas.Regla("x", Proceso: "Discord.exe", Espacio: 2));
        Vale(conExe.Any(x => x.Texto.Contains("Discord")),
             "con «.exe» se dice cómo se escribe bien, con el nombre ya corregido");
        Vale(conExe.All(x => !x.Grave),
             "pero no impide guardar: es un aviso, no un error");

        // ---------- Una ruta en el campo del proceso ----------
        Vale(HyprGuia.Problemas(new HyprReglas.Regla("x", Proceso: "C:\\x\\a", Espacio: 1))
                     .Any(x => x.Texto.Contains("Ruta contiene")),
             "una ruta en «Proceso» manda al campo correcto");

        // ---------- Flotante contra layout: raro, pero legal ----------
        var choque = HyprGuia.Problemas(new HyprReglas.Regla(
            "x", Proceso: "a", Flotante: true, Layout: NubeHypr.Layout.Principal));
        Vale(choque.Any(x => x.Texto.Contains("fuera del reparto")),
             "flotante + layout se avisa");
        Vale(choque.All(x => !x.Grave),
             "y se deja guardar: puede ser a propósito");

        // ---------- Una regla buena no inventa problemas ----------
        var buena = new HyprReglas.Regla("x", Proceso: "Discord", Espacio: 2);
        Vale(HyprGuia.Problemas(buena).Count == 0,
             "una regla correcta y encendida no tiene nada que avisar");
        Vale(HyprGuia.Problemas(buena with { Activa = false }).Any(x => !x.Grave),
             "y una apagada lo dice, sin tratarlo como un error");
    }

    // ================= Probar =================
    static void GuiaProbarNoMueveNada()
    {
        Grupo("guía: probar una regla evalúa y no mueve");

        var ventanas = new List<HyprGuia.Candidata>
        {
            new("Discord", "Discord", "@alguien - Discord"),
            new("Opera", "opera", "WhatsApp Web"),
            new("Opera", "opera", "Noticias"),
        };

        var r = new HyprReglas.Regla("x", Proceso: "Discord", Espacio: 2);
        var res = HyprGuia.Probar(r, ventanas, v => v.Proceso == "Discord");
        Vale(res.PuedeAplicarse, "una regla buena se puede probar");
        Igual(1, res.Coinciden.Count, "y encuentra la que coincide");
        Vale(res.Veredicto.Contains("1 ventana"), "lo dice en singular");

        var dos = HyprGuia.Probar(new HyprReglas.Regla("x", Proceso: "opera", Espacio: 1),
                                  ventanas, v => v.Proceso == "opera");
        Vale(dos.Veredicto.Contains("2 ventanas"), "y en plural cuando son dos");

        // ---------- Cero coincidencias se explica ----------
        //
        // «0 ventanas» a secas deja pensando si el filtro está mal o si la aplicación no
        // está abierta. Son dos cosas muy distintas y hay que nombrarlas.
        var cero = HyprGuia.Probar(new HyprReglas.Regla("x", Proceso: "noexiste", Espacio: 1),
                                   ventanas, _ => false);
        Vale(cero.PuedeAplicarse, "no coincidir con nada no es un error");
        Vale(cero.Veredicto.Contains("no está abierta") || cero.Veredicto.Contains("nombre del proceso"),
             "y se explican las dos causas posibles");

        // ---------- Con un problema grave NO se evalúa ----------
        //
        // Si se evaluara, una regla sin criterios daría «0 ventanas» y el usuario creería
        // que su filtro está bien escrito y que no hay nada abierto.
        var rota = HyprGuia.Probar(new HyprReglas.Regla("x"), ventanas, _ => true);
        Vale(!rota.PuedeAplicarse, "una regla rota no se evalúa");
        Igual(0, rota.Coinciden.Count, "y no devuelve coincidencias inventadas");
        Vale(rota.Problemas.Count > 0, "pero sí dice qué le pasa");

        // ---------- Lo que de verdad va a ocurrir, por modo ----------
        foreach (var m in Enum.GetValues<NubeHypr.Modo>())
            Vale(HyprGuia.LoQuePasaDeVerdad(m).Length > 20, $"el modo {m} tiene su aviso");

        Vale(HyprGuia.LoQuePasaDeVerdad(NubeHypr.Modo.Simulacion).Contains("NO mueve"),
             "en simulación se dice que NO mueve nada");
        Vale(HyprGuia.LoQuePasaDeVerdad(NubeHypr.Modo.Apagado) !=
             HyprGuia.LoQuePasaDeVerdad(NubeHypr.Modo.Simulacion),
             "apagado y simulación no dicen lo mismo");

        // ---------- Y lo que le haría a cada una ----------
        var que = HyprGuia.QueLeHariaA(r, ventanas[0]);
        Vale(que.Contains("Discord") && que.Contains("Escritorio 2"),
             "se dice qué le haría a cada ventana por su nombre");
        Vale(HyprGuia.QueLeHariaA(new HyprReglas.Regla("x", Proceso: "a"), ventanas[0])
                     .Contains("nada"),
             "y si la regla no hace nada, se dice");

        // ---------- Que no reviente con lo raro ----------
        Vale(HyprGuia.Probar(r, new List<HyprGuia.Candidata>(), _ => true).Coinciden.Count == 0,
             "sin ventanas abiertas no revienta");
        Vale(HyprGuia.Probar(r, ventanas, _ => throw new InvalidOperationException())
                     .Coinciden.Count == 0,
             "y si leer una ventana falla, esa se descarta en vez de tumbar la prueba");
    }

    // ================= Usar un ejemplo =================
    static void GuiaUsarUnEjemploNoDecideNada()
    {
        Grupo("guía: usar un ejemplo rellena, no decide");

        var yaHay = new List<HyprReglas.Regla>();

        foreach (var e in HyprGuia.Ejemplos)
        {
            var nueva = HyprGuia.ParaUsar(e, yaHay);
            Vale(!nueva.Activa, $"«{e.Que}»: se añade APAGADA");
            Vale(nueva.Id.Length > 0, $"«{e.Que}»: tiene id");
            Vale(yaHay.All(x => !x.Id.Equals(nueva.Id, StringComparison.OrdinalIgnoreCase)),
                 $"«{e.Que}»: el id no choca con los que ya hay");
            yaHay.Add(nueva);
        }

        Igual(8, yaHay.Count, "los ocho se pueden usar seguidos");

        // ---------- Dos clics en el MISMO ejemplo ----------
        //
        // Sin id libre, el segundo machacaría al primero y el usuario vería desaparecer lo
        // que acababa de escribir encima.
        var e0 = HyprGuia.Ejemplos[0];
        var a = HyprGuia.ParaUsar(e0, new List<HyprReglas.Regla>());
        var b = HyprGuia.ParaUsar(e0, new List<HyprReglas.Regla> { a });
        Vale(a.Id != b.Id, "dos clics en el mismo ejemplo dan dos reglas distintas");

        // Y lo que se añade es lo que la tarjeta enseñaba, campo a campo: si la tarjeta
        // dijera una cosa y el botón pusiera otra, la guía estaría mintiendo.
        Igual(e0.Regla.Proceso, a.Proceso, "el proceso es el del ejemplo");
        Igual(e0.Regla.Espacio, a.Espacio, "y el escritorio también");
    }
}
