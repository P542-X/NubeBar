using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D9.1: las pruebas de las tres redes que tienen que existir ANTES de aplicar.
//
// Respetar el gesto del usuario, un tope de ritmo y un deshacer. Las tres son lógica pura
// con el reloj inyectado, así que se comprueban sin esperar, sin abrir ventanas y sin
// mover nada.
public static partial class Pruebas
{
    internal static void CorrerD9()
    {
        D9ElGestoDelUsuarioManda();
        D9DeQuienFueElCambio();
        D9ElTopeDeRitmo();
        D9DeshacerLoUltimo();
        D9LaPuertaUnica();
        D9ElComandoVeinte();
        D9EscribirReglasEnElArbol();
    }

    // ================= El editor =================

    static void D9EscribirReglasEnElArbol()
    {
        Grupo("D9.3: escribir reglas desde Ajustes sin perder nada");

        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                         "nubebar-pruebas-d9", Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        var ruta = System.IO.Path.Combine(dir, "config.json");

        // Un config con cosas que esta versión conoce y cosas que no. Lo que de verdad se
        // prueba es lo segundo: el editor de reglas escribe UNA clave de la sección, y las
        // demás —incluidas las del futuro— tienen que seguir ahí.
        System.IO.File.WriteAllText(ruta, """
        {
          "Version": 4,
          "Barra": { "Alto": 44 },
          "NubeHypr": {
            "Activo": false,
            "Modo": "simulacion",
            "Gap": 12,
            "Atajos": { "Win+Ctrl+Alt+RIGHT": "foco.derecha" },
            "Reglas": [],
            "ClaveDelFuturo": "no la conozco"
          }
        }
        """);

        var arbol = ConfigArbol.Cargar(ruta);
        arbol.PonerReglas("NubeHypr", new[]
        {
            new HyprReglas.Regla("nav", Prioridad: 5, Proceso: "firefox", Espacio: 2),
            new HyprReglas.Regla("flota", Clase: "CabinetWClass", Flotante: true, Activa: false),
        });
        arbol.Guardar();

        var texto = System.IO.File.ReadAllText(ruta);
        var leido = System.Text.Json.Nodes.JsonNode.Parse(texto)!.AsObject();
        var hypr = leido["NubeHypr"]!.AsObject();

        // ---------- Las reglas están ----------
        var reglas = hypr["Reglas"]!.AsArray();
        Igual(2, reglas.Count, "se escriben las dos reglas");
        Igual("nav", reglas[0]!["Id"]!.GetValue<string>(), "con su id");
        Igual("firefox", reglas[0]!["Proceso"]!.GetValue<string>(), "y su criterio");
        Igual(2, reglas[0]!["Espacio"]!.GetValue<int>(), "y su acción");

        // ---------- Y lo demás de la sección, también ----------
        Igual("no la conozco", hypr["ClaveDelFuturo"]!.GetValue<string>(),
              "una clave desconocida de NubeHypr sobrevive a escribir las reglas");
        Igual(12, hypr["Gap"]!.GetValue<int>(), "y las que sí se conocen");
        Vale(hypr["Atajos"] is System.Text.Json.Nodes.JsonObject,
             "y los atajos, que son otra clave de la misma sección");
        Igual(false, hypr["Activo"]!.GetValue<bool>(),
             "y la capa sigue apagada: escribir una regla no la enciende");
        Igual(44, leido["Barra"]!["Alto"]!.GetValue<int>(), "y otras secciones ni se tocan");

        // ---------- Los nulos no se escriben ----------
        //
        // Un `"Clase": null` se lee igual que si no estuviera, pero al abrir el fichero a
        // mano parece una decisión en vez de un hueco.
        Vale(!reglas[0]!.AsObject().ContainsKey("Clase"),
             "lo que la regla no mira no se escribe como null");
        Vale(!reglas[0]!.AsObject().ContainsKey("Titulo"), "ni el título");
        Vale(reglas[1]!.AsObject().ContainsKey("Clase"), "y lo que sí mira, sí se escribe");

        // ---------- Y vuelve a leerse como lo que era ----------
        var opc = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new HyprReglas.LectorTolerante() },
        };
        var vuelta = System.Text.Json.JsonSerializer.Deserialize<OpcionesHypr>(
            hypr.ToJsonString(), opc);
        Igual(2, vuelta!.Reglas.Count, "al releer siguen siendo dos");
        Igual("firefox", vuelta.Reglas[0].Proceso, "con su criterio intacto");
        Vale(!vuelta.Reglas[1].Activa, "y la apagada sigue apagada");
        Vale(vuelta.Reglas[0].Clase == null, "y lo que no miraba sigue sin mirar");

        // ---------- Una lista vacía también se escribe ----------
        //
        // Borrar la última regla tiene que dejar `"Reglas": []`, no quitar la clave: un
        // hueco donde había una lista es una invitación a que algo la dé por no migrada.
        arbol.PonerReglas("NubeHypr", Array.Empty<HyprReglas.Regla>());
        arbol.Guardar();
        var vacio = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(ruta))!;
        Vale(vacio["NubeHypr"]!["Reglas"] is System.Text.Json.Nodes.JsonArray a && a.Count == 0,
             "al quitar la última, queda una lista vacía y no un hueco");
        Igual("no la conozco", vacio["NubeHypr"]!["ClaveDelFuturo"]!.GetValue<string>(),
              "y lo desconocido sigue ahí");

        System.IO.Directory.Delete(dir, true);

        // ---------- Cómo se escribe un tamaño ----------
        //
        // Lo encontró una captura del editor: una regla con ancho 0,5 y sin alto salía como
        // «tamaño=1x0». Dos fallos en la misma línea — el formato redondeaba media pantalla
        // a una entera, y un alto que nadie había puesto se enseñaba como cero, que querría
        // decir una ventana sin altura.
        Igual("0.5x—", HyprReglas.Tamano(0.5, null), "media pantalla de ancho y el alto sin poner");
        Igual("—x—", HyprReglas.Tamano(null, null), "nada puesto, nada que enseñar");
        Igual("0.5x0.9", HyprReglas.Tamano(0.5, 0.9), "los dos puestos");
        Igual("1x1", HyprReglas.Tamano(1, 1), "y una pantalla entera no lleva decimales de adorno");
        Vale(HyprReglas.Tamano(0.5, null) != "1x0",
             "y desde luego 0,5 ya no se escribe como 1");

        // Y con la coma decimal del sistema o sin ella, se escribe igual: el fichero y el
        // registro no pueden cambiar de forma según el idioma de quien lo mire.
        Vale(HyprReglas.Tamano(0.5, 0.25).Contains('.'),
             "se escribe con punto, como en el fichero");

        var conTamano = new HyprReglas.Regla("t", Proceso: "x", Ancho: 0.5);
        Vale(conTamano.ToString().Contains("0.5x—"), "y la regla lo enseña igual en su resumen");
    }

    // ================= La puerta =================

    static EfectoVentana Colocar(long h, double x, double y) =>
        new(TipoEfecto.Colocar, (IntPtr)h, R(x, y, 800, 600), Porque: "prueba");

    static void D9LaPuertaUnica()
    {
        Grupo("D9.2: la puerta única consulta las tres redes");

        var respeto = new HyprRespeto.Memoria();
        var ritmo = new HyprRitmo.Contador();
        var t0 = new DateTime(2026, 1, 1, 9, 0, 0);

        var efectos = new List<EfectoVentana>
        {
            Colocar(1, 0, 0),
            Colocar(2, 100, 0),
            Colocar(3, 200, 0),
        };

        // ---------- Sin nada marcado, pasan todos ----------
        var libre = HyprAplicador.Filtrar(efectos, respeto, ritmo, t0);
        Igual(3, libre.Pasan.Count, "sin marcas ni ráfaga, pasan los tres");
        Igual(0, libre.Fuera.Count, "y no se descarta ninguno");
        Vale(libre.HayAlgo, "hay algo que aplicar");
        Vale(libre.Resumen.Contains("ninguno descartado"), "y el resumen lo dice");

        // ---------- Una ventana que tocaste no pasa ----------
        respeto.Marcar((IntPtr)2, t0);
        var conMarca = HyprAplicador.Filtrar(efectos, respeto, ritmo, t0);
        Igual(2, conMarca.Pasan.Count, "la que moviste tú se queda fuera");
        Igual(1, conMarca.Fuera.Count, "y se cuenta como descartada");
        Vale(conMarca.Fuera[0].Porque.Contains("moviste tú"),
             "con el motivo en palabras, no en código");
        Vale(conMarca.Fuera[0].Porque.Contains("09:00"), "y con la hora a la que la moviste");
        Vale(!conMarca.Pasan.Any(e => e.Hwnd == (IntPtr)2), "y de verdad no está entre los que pasan");

        // ---------- El respeto va ANTES que el ritmo ----------
        //
        // Una ventana marcada no se mueve hoy ni dentro de una hora, así que preguntarle al
        // ritmo por ella sería gastar cupo en algo que no va a pasar nunca.
        var soloMarcada = HyprAplicador.Filtrar(new[] { Colocar(2, 0, 0) }, respeto, ritmo, t0);
        Igual(0, soloMarcada.Pasan.Count, "la marcada no pasa");
        var despues = HyprAplicador.Filtrar(new[] { Colocar(9, 0, 0) }, respeto, ritmo, t0);
        Vale(despues.HayAlgo,
             "y preguntar por ella no ha gastado el cupo de las demás");

        // ---------- Lo que no mueve, no se pregunta ----------
        //
        // Enfocar una ventana no es moverla. Si contara, el ritmo se gastaría mirando y una
        // ventana quedaría "recién movida" por haberle dado el foco.
        var mirones = new List<EfectoVentana>
        {
            new(TipoEfecto.Enfocar, (IntPtr)2, Porque: "enfocar la marcada"),
            new(TipoEfecto.SeQueda, (IntPtr)2, Porque: "no entra en el reparto"),
        };
        var pasanMirones = HyprAplicador.Filtrar(mirones, respeto, ritmo, t0);
        Igual(2, pasanMirones.Pasan.Count,
              "enfocar y «se queda» pasan aunque la ventana esté marcada: no la mueven");
        Vale(!HyprAplicador.Mueve(mirones[0]), "enfocar no mueve");
        Vale(HyprAplicador.Mueve(Colocar(1, 0, 0)), "colocar sí");
        Vale(HyprAplicador.Mueve(new EfectoVentana(TipoEfecto.Restaurar, (IntPtr)1)),
             "y restaurar también: saca de maximizada, que cambia el rectángulo");

        // ---------- Una tanda grande pasa ENTERA ----------
        //
        // Diez ventanas en un reparto son una decisión, no diez. Cortarla por la mitad
        // dejaría media pantalla ordenada, que es peor que no tocar nada.
        var r2 = new HyprRitmo.Contador();
        var m2 = new HyprRespeto.Memoria();
        var muchos = Enumerable.Range(1, 10).Select(i => Colocar(100 + i, i * 10, 0)).ToList();
        var entera = HyprAplicador.Filtrar(muchos, m2, r2, t0);
        Igual(10, entera.Pasan.Count, "las diez de un reparto pasan juntas");
        Igual(0, entera.Fuera.Count, "y no se queda ninguna a medias");

        // ---------- Pero la ráfaga de tandas sí se corta ----------
        //
        // Seis ventanas que abre un editor son seis tandas, no una. Eso es lo que corta el
        // tope por segundo.
        HyprAplicador.Salio(muchos, m2, r2, t0);
        for (int i = 0; i < HyprRitmo.MaxPorSegundo; i++)
            HyprAplicador.Salio(new[] { Colocar(900 + i, 0, 0) }, m2, r2, t0.AddMilliseconds(i * 10));

        var cortado = HyprAplicador.Filtrar(new[] { Colocar(999, 0, 0) }, m2, r2, t0.AddMilliseconds(60));
        Igual(0, cortado.Pasan.Count, "pasada la ráfaga de tandas, la siguiente se corta");
        Vale(cortado.Fuera.Any(d => d.Porque.Contains("segundo")),
             "y el motivo habla del tope por segundo");
        Vale(cortado.Fuera.Count == 1, "con un motivo por efecto, no diez iguales");

        // ---------- Después de aplicar se apunta ----------
        var m3 = new HyprRespeto.Memoria();
        var r3 = new HyprRitmo.Contador();
        var uno = new[] { Colocar(5, 300, 300) };
        Vale(HyprAplicador.Filtrar(uno, m3, r3, t0).HayAlgo, "pasa la primera vez");

        HyprAplicador.Salio(uno, m3, r3, t0);
        Vale(!HyprAplicador.Filtrar(uno, m3, r3, t0.AddMilliseconds(50)).HayAlgo,
             "y después de aplicarla, el ritmo la corta");

        // Y lo más importante de `Salio`: se recuerda QUÉ se pidió, para que el propio
        // movimiento no se lea luego como un gesto tuyo. Sin esto el motor se desactivaría
        // solo en la primera ventana que colocara.
        Vale(m3.LoQuePedimos((IntPtr)5) != null, "se recuerda el destino que se pidió");
        Igual(HyprRespeto.Quien.Nuestro,
              HyprRespeto.DeQuienFue(R(0, 0, 800, 600), uno[0].Destino, m3.LoQuePedimos((IntPtr)5)),
              "y así el movimiento se reconoce como nuestro, no como tuyo");

        // ---------- Lo que había, para deshacer ----------
        var habia = HyprAplicador.LoQueHabia(
            uno, _ => R(10, 20, 400, 300), _ => false);
        Igual(1, habia.Count, "se apunta una ventana");
        Igual(10.0, habia[0].Donde.X, "con la geometría de AHORA, no con el destino");

        var ilegible = HyprAplicador.LoQueHabia(uno, _ => default, _ => false);
        Igual(0, ilegible.Count,
              "si no se puede leer dónde está, no se promete poder devolverla");

        // ---------- Las líneas ----------
        var lineas = HyprAplicador.Lineas("reglas", conMarca, NubeHypr.Modo.Simulacion);
        Vale(lineas[0].Contains("reglas"), "la línea dice de dónde venían los efectos");
        Vale(lineas[0].Contains("no se mueve nada"), "y que en simulación no se mueve nada");
        Vale(lineas.Any(l => l.Contains("descartado")), "y se enumera lo descartado");
        Vale(!HyprAplicador.Lineas("x", libre, NubeHypr.Modo.Aplicar)[0].Contains("no se mueve"),
             "en modo aplicar no dice que no se mueve");
    }

    static void D9ElComandoVeinte()
    {
        Grupo("D9.1: deshacer es el comando número 20");

        // Romper el «son 19» fue deliberado: sin una tecla que deshaga, encender el modo
        // «aplicar» significa que la primera regla mal escrita te recoloca diez ventanas y
        // te deja recolocándolas a mano.
        Igual(20, HyprAtajos.Nombres.Length, "hay veinte comandos");
        Vale(HyprAtajos.EsNombre(HyprAtajos.DeshacerUltimo), "y «deshacer.ultimo» es uno");
        Vale(HyprAtajos.Comando(HyprAtajos.DeshacerUltimo) is Comando.Deshacer,
             "que se traduce al comando de deshacer");
        Igual("Deshacer", HyprAtajos.Rotulo(HyprAtajos.DeshacerUltimo), "con su rótulo");

        // Tiene tecla, está en el primer nivel y no pisa a nadie
        var serie = HyprAtajos.DeSerie();
        Igual(20, serie.Count, "y el mapa de serie trae los veinte");
        var suya = serie.FirstOrDefault(p => p.Value == HyprAtajos.DeshacerUltimo);
        Vale(suya.Key != null, "deshacer tiene combinación de serie");
        Vale(suya.Key!.EndsWith("+Z"), $"y es la Z, donde la gente busca un deshacer ({suya.Key})");
        Igual(1, serie.Count(p => p.Value == HyprAtajos.DeshacerUltimo),
              "una sola combinación para deshacer");

        // Y sigue siendo un comando como los demás para el catálogo del panel
        Vale(HyprCatalogo.AgruparAtajos(HyprAtajos.Nombres)
                         .Any(g => g.Nombres.Contains(HyprAtajos.DeshacerUltimo)),
             "sale en los grupos del panel de Ajustes");
        Igual(20, HyprCatalogo.AgruparAtajos(HyprAtajos.Nombres).Sum(g => g.Nombres.Count),
              "y no se pierde ni se duplica");
    }

    static NubeHypr.Rect R(double x, double y, double w, double h) => new(x, y, w, h);

    // ================= Respetar el gesto =================

    static void D9ElGestoDelUsuarioManda()
    {
        Grupo("D9.1: si mueves una ventana a mano, la regla no te la quita");

        var m = new HyprRespeto.Memoria();
        var a = (IntPtr)1; var b = (IntPtr)2;

        Vale(HyprRespeto.SeLePuedeTocar(a, m), "una ventana que no has tocado se puede colocar");

        Vale(m.Marcar(a), "se marca la primera vez");
        Vale(!m.Marcar(a), "y la segunda no vuelve a marcar: no es un error, es que la sigues moviendo");
        Vale(m.EstaTocada(a), "queda marcada");
        Vale(!HyprRespeto.SeLePuedeTocar(a, m), "y las reglas ya no la tocan");
        Vale(HyprRespeto.SeLePuedeTocar(b, m), "la de al lado sigue libre");

        // ---------- No caduca ----------
        //
        // A propósito. Si caducara, la regla volvería a colocarte la ventana al rato y
        // vivirías la misma pelea, solo más espaciada — que es peor, porque además sería
        // impredecible.
        Vale(m.Cuando(a) != null, "se guarda cuándo la moviste");
        Vale(m.EstaTocada(a), "y la marca no se va sola con el tiempo");

        // Soltarla es un acto explícito ("vuelve a organizar esta ventana")
        Vale(m.Soltar(a), "se puede soltar la marca a mano");
        Vale(HyprRespeto.SeLePuedeTocar(a, m), "y entonces las reglas vuelven a poder");
        Vale(!m.Soltar(a), "soltar dos veces no suelta nada");

        // ---------- Las que se cierran se olvidan ----------
        //
        // Lo importante no es la memoria: es que un hwnd que Windows reutilice nacería
        // marcado, y una ventana nueva no se colocaría por culpa de una que se cerró ayer.
        m.Marcar(a); m.Marcar(b);
        Igual(2, m.Cuantas, "dos marcadas");
        Igual(1, m.Limpiar(new[] { b }), "al cerrarse una, se olvida una");
        Igual(1, m.Cuantas, "queda la viva");
        Vale(HyprRespeto.SeLePuedeTocar(a, m), "y un hwnd reutilizado nace limpio");

        // ---------- Lo que pedimos nosotros ----------
        m.Vaciar();
        m.Pedimos(a, R(100, 100, 800, 600));
        Vale(m.LoQuePedimos(a) is { } p && p.X == 100, "se recuerda lo que NubeHypr pidió");
        Vale(m.LoQuePedimos(b) == null, "y de la que no se pidió nada, nada");
        m.Pedimos(a, default);
        Vale(m.LoQuePedimos(a) is { } q && q.X == 100,
             "un rectángulo inválido no borra lo que había: no se pide 'a ninguna parte'");

        // ---------- Lo que se apunta ----------
        var linea = HyprRespeto.Linea(a, "firefox", m);
        Vale(linea.Contains("pueden colocarla"), "sin marca, se dice que se puede");
        m.Marcar(a);
        Vale(HyprRespeto.Linea(a, "firefox", m).Contains("la moviste tú"),
             "con marca, se dice que la moviste tú");
        Vale(HyprRespeto.Linea(a, "firefox", m).Contains("no la tocan"),
             "y qué consecuencia tiene: «mi regla no funciona» se contesta leyendo esto");
    }

    static void D9DeQuienFueElCambio()
    {
        Grupo("D9.1: de quién fue el cambio de sitio");

        var estaba = R(100, 100, 800, 600);

        Igual(HyprRespeto.Quien.SinCambio,
              HyprRespeto.DeQuienFue(estaba, estaba, null), "no se ha movido: de nadie");
        Igual(HyprRespeto.Quien.SinCambio,
              HyprRespeto.DeQuienFue(estaba, R(104, 103, 800, 600), null),
              "y unos pocos píxeles tampoco cuentan");

        // ---------- Lo nuestro se reconoce ----------
        //
        // Es lo que impide que el motor se desactive a sí mismo: sin esto, cada ventana que
        // NubeHypr coloca se leería como un gesto del usuario y la regla dejaría de
        // aplicarse a la primera.
        var pedimos = R(640, 360, 1280, 720);
        Igual(HyprRespeto.Quien.Nuestro,
              HyprRespeto.DeQuienFue(estaba, pedimos, pedimos), "está donde lo pedimos: nuestro");
        Igual(HyprRespeto.Quien.Nuestro,
              HyprRespeto.DeQuienFue(estaba, R(644, 364, 1280, 720), pedimos),
              "con la tolerancia de 8 px, que es la misma que usa confirmar");

        // ---------- Y lo de fuera ----------
        Igual(HyprRespeto.Quien.DeFuera,
              HyprRespeto.DeQuienFue(estaba, R(900, 500, 800, 600), pedimos),
              "en otro sitio: de fuera");
        Igual(HyprRespeto.Quien.DeFuera,
              HyprRespeto.DeQuienFue(estaba, R(900, 500, 800, 600), null),
              "y si no pedimos nada, cualquier cambio es de fuera");

        // Un destino inválido no convierte un gesto en «nuestro»
        Igual(HyprRespeto.Quien.DeFuera,
              HyprRespeto.DeQuienFue(estaba, R(900, 500, 800, 600), R(0, 0, 0, 0)),
              "un destino sin tamaño no vale como coartada");

        // Redimensionar también es un gesto: mismo sitio, otro tamaño.
        Igual(HyprRespeto.Quien.DeFuera,
              HyprRespeto.DeQuienFue(estaba, R(100, 100, 1200, 600), null),
              "cambiar de tamaño sin moverse también es un gesto");

        // ---------- La línea ----------
        var l = HyprRespeto.LineaDelGesto((IntPtr)7, "code", HyprRespeto.Quien.DeFuera,
                                          estaba, R(900, 500, 800, 600));
        Vale(l.Contains("hwnd=0x"), "lleva el hwnd");
        Vale(l.Contains("proceso=code"), "y el proceso");
        Vale(l.Contains("marcada"), "y dice la consecuencia cuando el gesto es de fuera");
        Vale(!HyprRespeto.LineaDelGesto((IntPtr)7, "code", HyprRespeto.Quien.Nuestro,
                                        estaba, pedimos).Contains("marcada"),
             "y no la dice cuando el movimiento fue nuestro");
    }

    // ================= El tope de ritmo =================

    static void D9ElTopeDeRitmo()
    {
        Grupo("D9.1: el tope de ritmo, para que aplicar no sea una ráfaga");

        var c = new HyprRitmo.Contador();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0);
        var v = (IntPtr)1;

        // ---------- Dos preguntas distintas, y eso lo enseñó una prueba ----------
        //
        // La primera versión tenía una sola: preguntaba por cada ventana y contaba cada
        // ventana. Con eso, un reparto en mosaico de diez ventanas —que es UNA decisión—
        // se habría quedado en cuatro colocadas y seis donde estaban. Media pantalla
        // ordenada es peor que ninguna.
        //
        // Ahora el tope en total cuenta TANDAS, y la espera sigue siendo por ventana.
        Vale(c.PreguntaLote(t0).Puede, "la primera tanda se puede");
        Vale(c.PreguntaVentana(v, t0).Puede, "y la ventana también");

        // ---------- Preguntar no gasta cupo ----------
        Vale(c.PreguntaLote(t0).Puede, "preguntar dos veces seguidas no gasta nada");
        Vale(c.PreguntaVentana(v, t0).Puede, "ni por ventana");

        c.ApuntadoLote(t0);
        c.Apuntado(v, t0);

        // ---------- Por ventana: la espera ----------
        var pronto = c.PreguntaVentana(v, t0.AddMilliseconds(100));
        Vale(!pronto.Puede, "100 ms después, la MISMA ventana no");
        Igual(HyprRitmo.Veredicto.DemasiadoPronto, pronto.Que, "y se dice por qué");
        Vale(pronto.Porque.Contains("ms"), "con el número de milisegundos");

        Vale(c.PreguntaVentana((IntPtr)2, t0.AddMilliseconds(100)).Puede,
             "pero OTRA ventana sí: la espera es por ventana");

        Vale(c.PreguntaVentana(v, t0.Add(HyprRitmo.EsperaPorVentana)).Puede,
             "pasada la espera, la misma ventana vuelve a poder");

        // ---------- En total: las tandas ----------
        //
        // Es el que corta la ráfaga del editor que abre seis ventanas en dos segundos: seis
        // apariciones son seis tandas.
        var c2 = new HyprRitmo.Contador();
        var t = t0;
        for (int i = 0; i < HyprRitmo.MaxPorSegundo; i++)
        {
            Vale(c2.PreguntaLote(t).Puede, $"la tanda nº {i + 1} del segundo, sí");
            c2.ApuntadoLote(t);
            t = t.AddMilliseconds(50);
        }
        var quinta = c2.PreguntaLote(t);
        Vale(!quinta.Puede, "la quinta tanda en el mismo segundo, no");
        Igual(HyprRitmo.Veredicto.DemasiadosAhora, quinta.Que, "y se dice que son demasiadas ahora");

        Vale(c2.PreguntaLote(t0.AddSeconds(2)).Puede,
             "dos segundos después, la ventana de un segundo ya ha pasado");

        // ---------- Y una tanda GRANDE pasa entera ----------
        //
        // Diez ventanas en una tanda son un acto, no diez. Esta es la prueba que impide
        // volver al diseño de antes.
        var c5 = new HyprRitmo.Contador();
        Vale(c5.PreguntaLote(t0).Puede, "una tanda de diez cabe");
        for (int i = 0; i < 10; i++)
            Vale(c5.PreguntaVentana((IntPtr)(500 + i), t0).Puede,
                 i == 0 ? "y cada una de sus diez ventanas pasa" : "");
        c5.ApuntadoLote(t0);
        Igual(1, c5.EnElUltimoMinuto, "y toda la tanda cuenta como UN acto, no como diez");

        // ---------- El bucle ----------
        //
        // Sesenta tandas en un minuto no es una ráfaga: es algo peleándose. Y entonces no se
        // aplica más despacio, se deja de aplicar. Preferir no hacer nada.
        var c3 = new HyprRitmo.Contador();
        var r = t0;
        for (int i = 0; i < HyprRitmo.MaxPorMinuto; i++)
        {
            c3.ApuntadoLote(r);
            r = r.AddMilliseconds(900);          // repartidas, no en ráfaga
        }
        var bucle = c3.PreguntaLote(r);
        Vale(!bucle.Puede, "al llegar al tope del minuto, se para");
        Igual(HyprRitmo.Veredicto.EnBucle, bucle.Que, "y se llama bucle, que es lo que es");
        Vale(bucle.Porque.Contains("bucle"), "y se dice con esa palabra");

        // Y pasado el minuto se olvida: un bucle que se arregló no bloquea para siempre.
        Vale(c3.PreguntaLote(r.AddMinutes(2)).Puede, "pasado el minuto, se vuelve a poder");

        // ---------- No crece sin límite ----------
        var c4 = new HyprRitmo.Contador();
        var u = t0;
        for (int i = 0; i < 500; i++)
        {
            c4.ApuntadoLote(u);
            c4.Apuntado((IntPtr)(1 + i), u);
            u = u.AddSeconds(1);
        }
        Vale(c4.EnElUltimoMinuto <= HyprRitmo.MaxPorMinuto + 1,
             $"el histórico no crece sin límite ({c4.EnElUltimoMinuto})");
        Vale(c4.Cuantas < 500, $"ni la tabla por ventana ({c4.Cuantas})");

        // ---------- La línea ----------
        Vale(HyprRitmo.Linea(v, pronto).Contains("hwnd=0x"), "la línea lleva el hwnd");
        Vale(HyprRitmo.Linea(v, pronto).Contains("DemasiadoPronto"), "y el veredicto");
    }

    // ================= Deshacer =================

    static void D9DeshacerLoUltimo()
    {
        Grupo("D9.1: deshacer lo último, que es lo que permite encender sin miedo");

        var p = new HyprDeshacer.Pila();
        var a = (IntPtr)1; var b = (IntPtr)2;
        var vivas = new[] { a, b };

        Vale(p.Ultimo == null, "al empezar no hay nada que deshacer");
        var (nada, sinEfectos) = p.Deshacer(vivas);
        Vale(nada == null, "y deshacer no devuelve lote");
        Igual(0, sinEfectos.Count, "ni efectos");

        // ---------- Un lote ----------
        Vale(p.Apuntar("regla nav", new[]
        {
            new HyprDeshacer.Antes(a, R(100, 100, 800, 600), false),
            new HyprDeshacer.Antes(b, R(900, 100, 800, 600), true),
        }), "se apunta un lote");
        Igual(1, p.Cuantos, "hay un lote");
        Igual(2, p.Ultimo!.Cuantas, "con dos ventanas");

        var (lote, efectos) = p.Deshacer(vivas);
        Vale(lote != null, "se deshace");
        Igual(2, efectos.Count, "con un efecto por ventana");
        Igual(0, p.Cuantos, "y el lote se consume: deshacer dos veces no repite");

        // La que estaba maximizada se RESTAURA, no se coloca: colocarla con su rectángulo
        // la dejaría del tamaño que tenía antes de maximizar, pero sin estar maximizada.
        Vale(efectos.Any(e => e.Hwnd == b && e.Tipo == TipoEfecto.Restaurar),
             "la que estaba maximizada se restaura");
        Vale(efectos.Any(e => e.Hwnd == a && e.Tipo == TipoEfecto.Colocar),
             "y la normal se coloca donde estaba");
        Vale(efectos.All(e => e.Porque.Contains("deshacer")),
             "y cada efecto dice que viene de deshacer");

        // ---------- Un lote vacío no es un lote ----------
        Vale(!p.Apuntar("nada", Array.Empty<HyprDeshacer.Antes>()), "un lote vacío no se apunta");
        Vale(!p.Apuntar("malos", new[] { new HyprDeshacer.Antes(IntPtr.Zero, R(0, 0, 10, 10), false) }),
             "ni uno con handles vacíos");
        Vale(!p.Apuntar("sin sitio", new[] { new HyprDeshacer.Antes(a, R(0, 0, 0, 0), false) }),
             "ni uno con rectángulos sin tamaño");
        Igual(0, p.Cuantos, "así que la pila sigue vacía");

        // ---------- Las ventanas cerradas no se devuelven ----------
        //
        // Y no es un error: devolver una ventana que ya no existe es nada, y hacerlo saltar
        // convertiría "una se cerró" en "no se puede deshacer".
        p.Apuntar("mixto", new[]
        {
            new HyprDeshacer.Antes(a, R(10, 10, 400, 300), false),
            new HyprDeshacer.Antes(b, R(20, 20, 400, 300), false),
        });
        var (l2, e2) = p.Deshacer(new[] { a });
        Igual(2, l2!.Cuantas, "el lote recuerda las dos");
        Igual(1, e2.Count, "pero solo se devuelve la que sigue viva");

        // ---------- El tope de lotes ----------
        var p2 = new HyprDeshacer.Pila();
        for (int i = 0; i < HyprDeshacer.MaxLotes + 3; i++)
            p2.Apuntar("lote " + i, new[] { new HyprDeshacer.Antes(a, R(i, i, 100, 100), false) });
        Igual(HyprDeshacer.MaxLotes, p2.Cuantos, "la pila no crece sin límite");
        Vale(p2.Ultimo!.Porque.Contains((HyprDeshacer.MaxLotes + 2).ToString()),
             "y lo que se conserva es lo ÚLTIMO, no lo primero");

        // ---------- Caducidad ----------
        //
        // Pasados cinco minutos, "lo de antes" ya no es lo de antes: devolver ventanas a
        // sitios de hace media hora sería peor que no deshacer.
        var p3 = new HyprDeshacer.Pila();
        var hace10 = DateTime.Now - TimeSpan.FromMinutes(10);
        p3.Apuntar("viejo", new[] { new HyprDeshacer.Antes(a, R(1, 1, 100, 100), false) }, hace10);
        Igual(1, p3.Caducar(), "un lote de hace diez minutos caduca");
        Igual(0, p3.Cuantos, "y se va");

        var (sinLote, _) = p3.Deshacer(vivas);
        Vale(sinLote == null, "y entonces no hay nada que deshacer");

        // ---------- La línea ----------
        var p4 = new HyprDeshacer.Pila();
        p4.Apuntar("prueba", new[] { new HyprDeshacer.Antes(a, R(1, 1, 100, 100), false) });
        var (l4, e4) = p4.Deshacer(vivas);
        var linea = HyprDeshacer.Linea(l4, e4.Count, NubeHypr.Modo.Simulacion);
        Vale(linea.Contains("1 ventana"), "la línea dice cuántas ventanas");
        Vale(linea.Contains("prueba"), "y de qué lote venían");
        Vale(linea.Contains("solo apuntado"),
             "y que en simulación NO se ha movido nada");
        Vale(HyprDeshacer.Linea(null, 0, NubeHypr.Modo.Simulacion).Contains("no hay nada"),
             "y sin lote, lo dice");
    }
}
