using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows.Media;

namespace NubeBar;

// D8: estabilización. Ni una función nueva — lo que hay aquí son las pruebas de regresión
// que faltaban sobre lo que YA existía.
//
// ---------- Por qué hace falta un grupo propio ----------
//
// D6 y D7 añadieron piezas y las probaron. Lo que no estaba probado es lo de siempre, las
// cosas que se dan por sabidas: que una configuración de la v1 llegue a la v4 sin perder
// nada, que los glifos que se pintan existan, que ningún identificador de sección esté
// repetido, que las banderas de diagnóstico no choquen entre sí y que ninguna de ellas
// escriba la configuración. Son las que se rompen al añadir la fase siguiente.
public static partial class Pruebas
{
    internal static void CorrerD8()
    {
        D8LaCadenaDeMigracionesCompleta();
        D8TodasLasBanderasDeDiagnosticoSonDistintas();
        D8ElVeredictoDeLaVigilia();
        D8ElPrefijoDeLosAtajos();
        D8NingunDiagnosticoEscribeLaConfiguracion();
        D8LasSeccionesDeAjustesNoChocan();
        D8UnSoloValorDeSerieParaElAtajo();
        D8LosGlifosQueSePintanExisten();
        D8NubeHyprSigueApagadoDeSerie();
        D8ElCatalogoDeFasesEstaCompleto();
    }

    // ================= Migraciones =================

    static void D8LaCadenaDeMigracionesCompleta()
    {
        Grupo("D8: de la v1 a la actual sin perder nada");

        // ---------- Una configuración "antigua" de verdad ----------
        //
        // Con lo que un usuario de la v1 tendría: su tema, su barra, sus apps fijadas. Y con
        // dos cosas que esta versión no conoce, porque eso es lo que de verdad se pierde en
        // una migración mal hecha.
        var vieja = new JsonObject
        {
            ["Version"] = 1,
            ["Tema"] = new JsonObject { ["Acento"] = "#AABBCC", ["Radio"] = 14 },
            ["Barra"] = new JsonObject
            {
                ["Alto"] = 44,
                ["AppsFijadas"] = new JsonArray("firefox.exe", "code.exe"),
            },
            ["UnaClaveDelFuturo"] = "no la conozco",
            ["SeccionRara"] = new JsonObject { ["x"] = 1, ["y"] = new JsonArray(2, 3) },
        };

        var r = Esquema.Migrar(vieja);
        Vale(r.Vale, $"la migración completa va bien: {r.Resumen}");
        Igual(Esquema.Actual, vieja["Version"]!.GetValue<int>(), "y acaba en la versión actual");
        Igual(Esquema.Actual - 1, r.Aplicadas.Count,
              "se aplican todos los pasos que hay, no un número escrito a mano");

        // Lo del usuario, intacto
        Igual("#AABBCC", vieja["Tema"]!["Acento"]!.GetValue<string>(), "su acento sigue");
        Igual(44, vieja["Barra"]!["Alto"]!.GetValue<int>(), "su alto de barra sigue");
        Igual(2, vieja["Barra"]!["AppsFijadas"]!.AsArray().Count, "y sus apps fijadas");

        // Y lo que esta versión no entiende, también
        Igual("no la conozco", vieja["UnaClaveDelFuturo"]!.GetValue<string>(),
              "una clave desconocida sobrevive a toda la cadena");
        Vale(vieja["SeccionRara"] is JsonObject, "y una sección entera, también");
        Igual(3, vieja["SeccionRara"]!["y"]!.AsArray()[1]!.GetValue<int>(),
              "con su contenido hasta el fondo");

        // ---------- Y NubeHypr nace apagado al migrar ----------
        //
        // Es la promesa de todas las fases: actualizar NubeBar no puede empezar a moverte
        // ventanas. Si una migración encendiera la capa, el usuario se encontraría su
        // escritorio reorganizado sin haber pedido nada.
        Vale(vieja["NubeHypr"] is JsonObject, "la sección de NubeHypr se crea");
        Igual(false, vieja["NubeHypr"]!["Activo"]!.GetValue<bool>(), "y nace APAGADA");
        Igual("simulacion", vieja["NubeHypr"]!["Modo"]!.GetValue<string>(),
              "y en simulación, que no mueve nada");
        Igual(0, vieja["NubeHypr"]!["Reglas"]!.AsArray().Count, "sin reglas");

        // ---------- Migrar dos veces no cambia nada ----------
        //
        // Es lo que pasa en cada arranque: si no fuera idempotente, cada arranque haría un
        // respaldo nuevo y la carpeta se llenaría de copias.
        var antes = vieja.ToJsonString();
        var otra = Esquema.Migrar(vieja);
        Vale(!otra.Cambio, "la segunda migración no cambia nada");
        Igual(antes, vieja.ToJsonString(), "y el árbol es idéntico, byte a byte");

        // ---------- El validador no rechaza lo que no entiende ----------
        Vale(Esquema.Mal(vieja, 0) == null,
             "el validador no tumba un árbol por llevar claves que no conoce");
    }

    // ================= Las banderas de diagnóstico =================

    // Todas las banderas que acepta el ejecutable. Escritas aquí a mano a propósito: es la
    // lista que un humano puede leer y comparar con la ayuda, y si alguien añade una bandera
    // sin ponerla aquí, las pruebas de abajo la echan de menos.
    static readonly (string Bandera, bool SoloLectura)[] TodasLasBanderas =
    {
        ("--pruebas", true),
        // La auditoría de ventanas se llama `--ventanas`, no `--auditoria`: el archivo es
        // `Auditoria.cs` y la bandera es la otra. Lo descubrió esta misma prueba al comparar
        // la lista escrita a mano con la decisión del programa, que es para lo que está.
        ("--ventanas", true),
        ("--diagvista", true),
        ("--diaginicio", true),
        ("--diagbandeja", true),
        ("--diaghypr", true),
        ("--diagmotor", true),
        ("--diagespacios", true),
        ("--diagatajos", true),
        ("--diagreglas", true),
        ("--diagd6", true),
        ("--diagd7", true),
        ("--diagprefijos", true),
        ("--arnesespacios", true),
        // El arnés de aplicar SÍ mueve una ventana de verdad —la suya— pero tampoco escribe
        // la configuración: enciende la capa en memoria y la devuelve al salir.
        ("--arnesaplicar", true),
        // D9.6: reparte un escritorio ENTERO, pero uno que crea él y borra al terminar, con
        // tres ventanas suyas dentro. Enciende la capa en memoria y la devuelve; escribir la
        // configuración no puede, como ninguno.
        ("--arneslayout", true),
        // La rejilla abre ventanas suyas, abre la vista y mide las tarjetas. Ni toca las
        // tuyas ni escribe nada.
        ("--diagrejilla", true),
        // Las capturas abren paneles y los fotografían. Añaden un módulo a la isla en
        // memoria para poder retratar el indicador de D6, y lo quitan al terminar.
        ("--capturas", true),
        // El clima: la única bandera que sale a internet por su cuenta. Ni abre ventana ni
        // lee la ciudad del usuario —usa la que se le pase, o Madrid—, así que lo único que
        // sale de aquí es un nombre de ciudad y unas coordenadas. Escribir el config no
        // puede, como ninguna.
        ("--diagclima", true),
        // El del neón abre el panel izquierdo, lo fotografía seis veces y se va. Fuerza el
        // estilo «Neón» en memoria para tener halo que mirar, y lo devuelve al terminar.
        ("--diagneon", true),
        // La vigilia es la barra funcionando de verdad durante un rato, así que es la bandera
        // que MÁS se parece a arrancar sin banderas — y justo por eso tiene que ser una
        // bandera: así no puede escribir el config. Medir la estabilidad arrancando un
        // binario de fase a pelo es lo que migró el config una vez.
        ("--vigilia", true),
        // El modo de demostración de la guía: abre la interfaz entera pero no puede escribir
        // el config, ni migrarlo, ni encender NubeHypr, ni coger un atajo global.
        ("--guia", true),
        ("--sololectura", true),
        // E0: abre los paneles de verdad y compara el tablero con la isla. Solo lectura.
        ("--diage0", true),
        // El de módulos es el único que escribe un config… el SUYO, en una carpeta temporal
        // que monta antes de leer nada y borra al salir. Tiene que escribir: el fallo que
        // persigue —«lo añado y no sale»— solo aparece recorriendo guardar → recarga en
        // caliente → remonte. El config del usuario sigue protegido, y por una razón que es
        // la mitad del diseño: `NoSeDebeGuardar` mira `Config.RutaDelUsuario`, que la
        // redirección de la carpeta no mueve.
        ("--diagmodulos", true),
        // La ventanita: un `Window` pelado en un proceso aparte, para que el observador de
        // NubeHypr lo vea como una ventana normal —a las suyas las descarta por PID— y se
        // pueda comprobar que una regla la coloca al aparecer. No toca el config.
        ("--ventanita", true),
        // Abre cada panel en las cinco posiciones de barra y comprueba que no queda ningún
        // control por debajo del borde sin rueda que lo alcance. Solo mira.
        ("--diagpaneles", true),
        // `--salir` TAMBIÉN mira y no toca, y esto es el resultado de un fallo medido en D8:
        // su proceso solo manda la señal de salida, pero al irse pasa por `OnExit` →
        // `Config.Cargar()` y eso ejecuta las migraciones. Un `--salir` lanzado desde un
        // binario de desarrollo migró el config real de la v3 a la v4. Ver `BanderasQueMiran`.
        ("--salir", true),
        // El guardián sí puede escribir: es el vigilante y tiene que poder trabajar.
        ("--guardian", false),
    };

    static void D8TodasLasBanderasDeDiagnosticoSonDistintas()
    {
        Grupo("D8: ninguna bandera choca con otra");

        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (b, _) in TodasLasBanderas)
            Vale(vistas.Add(b), $"«{b}» no está repetida");

        // Y las constantes del código son las que están aquí. Es la mitad de la prueba: sin
        // esto, renombrar una bandera en su archivo dejaría esta lista mintiendo.
        foreach (var real in new[]
        {
            Pruebas.Bandera, Auditoria.Bandera, DiagVista.Bandera, DiagInicio.Bandera,
            DiagBandeja.Bandera, DiagHypr.Bandera, DiagMotor.Bandera, DiagEspacios.Bandera,
            DiagAtajos.Bandera, DiagReglas.Bandera, DiagD6.Bandera, DiagD7.Bandera,
            ArnesEspacios.Bandera, ArnesLayout.Bandera, DiagRejilla.Bandera, Salir.Bandera,
        })
            Vale(vistas.Contains(real), $"la bandera real «{real}» está en la lista de D8");

        // Todas empiezan por "--": una bandera sin guiones se confundiría con una ruta.
        Vale(TodasLasBanderas.All(b => b.Bandera.StartsWith("--")),
             "todas empiezan por dos guiones");

        // ---------- Una bandera desconocida NO arranca la barra ----------
        //
        // El accidente que esto evita me pasó dos veces en la misma sesión: pedirle a un
        // binario un diagnóstico que esa versión todavía no tenía. El argumento se caía por
        // todos los `if` y terminaba en el arranque normal, así que montaba la barra entera
        // y migraba el config del usuario al esquema de ese binario.
        foreach (var (b, _) in TodasLasBanderas)
            Vale(App.BanderaConocida(b), $"«{b}» la conoce el arranque");

        foreach (var invento in new[] { "--diagprefijos-que-no-existe", "--loquesea", "--d9",
                                        "--DIAGVISTAX", "--" })
            Vale(!App.BanderaConocida(invento), $"«{invento}» NO se conoce: no arranca nada");

        Vale(App.BanderaConocida("--PRUEBAS"), "y no distingue mayúsculas");
        Vale(!App.BanderaConocida(null), "ni revienta con null");

        // Las dos listas tienen que cuadrar: lo que mira y no toca es un subconjunto de lo
        // que se conoce. Sin esta prueba se podría añadir una bandera de medida, olvidarse
        // de protegerla, y que escribiera la configuración sin que nada avisara.
        foreach (var b in Config.BanderasQueMiran)
            Vale(App.BanderaConocida(b),
                 $"«{b}» mira y no toca, y además el arranque la conoce");

        // ---------- Y cuáles FALTAN, no solo cuántas ----------
        //
        // Antes esto solo comparaba tamaños y decía «esperado 18, real 17». Con eso se sabe
        // que falta una, no cuál, y se pierde un rato largo contando a mano en dos ficheros.
        // Ahora dice los nombres que sobran y los que faltan.
        var enLaPrueba = TodasLasBanderas.Select(b => b.Bandera).ToArray();
        var soloEnElArranque = App.Banderas.Except(enLaPrueba, StringComparer.OrdinalIgnoreCase).ToArray();
        var soloEnLaPrueba = enLaPrueba.Except(App.Banderas, StringComparer.OrdinalIgnoreCase).ToArray();

        Vale(soloEnElArranque.Length == 0,
             $"ninguna bandera del arranque falta en esta prueba (faltan: {string.Join(", ", soloEnElArranque)})");
        Vale(soloEnLaPrueba.Length == 0,
             $"ninguna bandera de esta prueba falta en el arranque (sobran: {string.Join(", ", soloEnLaPrueba)})");

        // Y ninguna repetida. Esto es lo que de verdad falló cuando los tamaños no cuadraban
        // pero el contenido sí: una bandera puesta dos veces en la misma lista.
        Vale(App.Banderas.Length == App.Banderas.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
             "el arranque no repite ninguna bandera: " + string.Join(" ", App.Banderas));
        Vale(enLaPrueba.Length == enLaPrueba.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
             "esta prueba no repite ninguna bandera: " + string.Join(" ", enLaPrueba));

        Igual(TodasLasBanderas.Length, App.Banderas.Length,
              $"la lista del arranque y la de esta prueba tienen el mismo tamaño. " +
              $"arranque=[{string.Join(" ", App.Banderas)}] prueba=[{string.Join(" ", enLaPrueba)}]");
    }

    // ================= El prefijo de los atajos =================

    static void D8ElPrefijoDeLosAtajos()
    {
        Grupo("D8: el prefijo de los atajos sale de una medición, no de un gusto");

        // ---------- Todo candidato tiene que dar los 19 ----------
        //
        // Esta es la prueba que habría pillado el fallo sin ejecutar nada. La primera
        // versión de `SegundoDe` encadenaba condiciones a mano y con `Win+Ctrl+Shift`
        // devolvía `Win+Ctrl+Shift+Ctrl`: el modificador repetido hacía que las cinco
        // combinaciones del segundo nivel colapsaran sobre las del primero, y el mapa se
        // quedaba en **14 comandos de 19** sin que nada avisara. Lo destapó
        // `--diagprefijos` al enseñar "13/14 entran" para ese candidato.
        foreach (var p in HyprAtajos.Candidatos)
        {
            var mapa = HyprAtajos.ConPrefijo(p);
            Igual(HyprAtajos.Nombres.Length, mapa.Count,
                  $"«{p}» da los 19 atajos (segundo nivel: {HyprAtajos.SegundoDe(p)})");
            Igual(HyprAtajos.Nombres.Length, mapa.Values.Distinct().Count(),
                  $"«{p}»: los 19 comandos, sin repetir ninguno");
        }

        // El segundo nivel añade un modificador que el prefijo NO lleva, nunca uno repetido.
        foreach (var p in HyprAtajos.Candidatos)
        {
            var segundo = HyprAtajos.SegundoDe(p);
            Vale(segundo != p, $"«{p}» tiene un segundo nivel distinto de sí mismo");
            foreach (var m in new[] { "Win", "Ctrl", "Alt", "Shift" })
            {
                int enPrefijo = p.Split('+').Count(t => t.Equals(m, StringComparison.OrdinalIgnoreCase));
                int enSegundo = segundo.Split('+').Count(t => t.Equals(m, StringComparison.OrdinalIgnoreCase));
                Vale(enSegundo <= 1, $"«{segundo}» no repite {m}");
                Vale(enSegundo >= enPrefijo, $"«{segundo}» no pierde el {m} del prefijo");
            }
        }

        // Con los cuatro modificadores puestos no hay segundo nivel posible, y entonces se
        // devuelve el mismo en vez de inventarse uno repetido. No es un caso que se use,
        // pero un `SegundoDe` que devolviera basura ahí sería una bomba para el día que
        // alguien escriba ese prefijo en su configuración.
        Igual("Win+Ctrl+Alt+Shift", HyprAtajos.SegundoDe("Win+Ctrl+Alt+Shift"),
              "con los cuatro puestos no se inventa un quinto modificador");

        // ---------- El de serie es uno de los candidatos y está bien formado ----------
        Vale(HyprAtajos.Candidatos.Contains(HyprAtajos.Prefijo),
             $"el prefijo de serie («{HyprAtajos.Prefijo}») es uno de los medidos");
        Igual(HyprAtajos.PrefijoSegundo, HyprAtajos.SegundoDe(HyprAtajos.Prefijo),
              "y su segundo nivel es el que le toca, no uno escrito a mano aparte");

        // ---------- Y ya no hay un `Win+Alt` escrito a mano ----------
        //
        // El prefijo está en UNA constante. Si volviera a haber veinte cadenas completas,
        // cambiarlo volvería a ser reescribir veinte y no equivocarse en ninguna — que es
        // la razón por la que no se cambió durante cinco fases.
        var serie = HyprAtajos.DeSerie();
        Igual(20, serie.Count, "el mapa de serie trae los 20");
        Vale(serie.Keys.All(k => k.StartsWith(HyprAtajos.Prefijo, StringComparison.OrdinalIgnoreCase)),
             "y todas las combinaciones empiezan por el prefijo");
        Igual(20, HyprAtajos.Teclas.Length, "una entrada de la tabla por comando");
        Vale(HyprAtajos.Teclas.Select(t => t.Comando).Distinct().Count() == 20,
             "sin comandos repetidos en la tabla");
        Igual(5, HyprAtajos.Teclas.Count(t => t.SegundoNivel),
              "cinco en el segundo nivel, quince en el primero");

        // Y el mapa de serie sigue sin pisar lo de Windows
        foreach (var deWindows in new[] { "Win+RIGHT", "Win+LEFT", "Win+UP", "Win+DOWN",
                                          "Win+Ctrl+RIGHT", "Win+Ctrl+LEFT", "Win+TAB", "Alt+TAB" })
            Vale(!serie.ContainsKey(deWindows), $"de serie no se coge «{deWindows}», que es de Windows");
    }

    static void D8NingunDiagnosticoEscribeLaConfiguracion()
    {
        Grupo("D8: ningún diagnóstico puede escribir tu configuración");

        // ---------- La protección de verdad ----------
        //
        // Esto no es un detalle: en esta sesión mi configuración real se migró a la v4 dos
        // veces por ejecutar builds de desarrollo en modo normal. La protección es
        // estructural —`Config.MiraYNoToca` y `ConfigArbol.NoSeDebeGuardar`— y esta prueba
        // es la que exige que siga estando.
        //
        // Cada bandera de la lista se comprueba contra la DECISIÓN del programa, no contra el
        // valor que esta prueba se ha escrito al lado. La primera versión de esto comparaba
        // el booleano consigo mismo y pasaba siempre: no probaba nada.
        foreach (var (b, deberia) in TodasLasBanderas)
            Igual(deberia, Config.MiraYNoToca(new[] { b }),
                  $"«{b}»: {(deberia ? "mira y no toca" : "sí puede escribir, a propósito")}");

        // Y los casos de los bordes del argumento
        Vale(!Config.MiraYNoToca(null), "sin argumentos, no es una herramienta de medida");
        Vale(!Config.MiraYNoToca(Array.Empty<string>()), "con la lista vacía, tampoco");
        // Una bandera que no existe TAMBIÉN mira y no toca, y esto es el tercer accidente
        // convertido en regla: el guardia que rechaza la bandera desconocida pasa igualmente
        // por `OnExit`, que carga la configuración, y cargarla migra. Con la regla al revés
        // —solo escriben la barra sin argumentos y el guardián— ese camino ya está cerrado
        // sin que nadie tenga que acordarse de añadirlo a una lista.
        Vale(Config.MiraYNoToca(new[] { "--loquesea" }),
             "una bandera que no existe tampoco puede escribir");
        Vale(Config.MiraYNoToca(new[] { "--diagd9-del-futuro" }),
             "ni una de una versión futura: un diagnóstico nuevo nace protegido");
        Vale(Config.MiraYNoToca(new[] { "--pruebas", "ruido" }),
             "lo que cuenta es el primer argumento, como en todo el arranque");
        Vale(!Config.MiraYNoToca(new[] { "--guardian", "--pruebas" }),
             "y un «--guardian» con ruido detrás sigue siendo un «--guardian»");

        // ---------- Y CUÁL falta, no solo cuántas ----------
        //
        // Esto decía «esperado 18, real 17» y nada más. Con eso se sabe que falta una bandera
        // en una de las dos listas, no cuál ni en cuál — y hay que ponerse a contar a mano en
        // dos ficheros. Pasó al añadir `--vigilia`: estaba en el arranque y no aquí.
        //
        // Nombrar lo que falta no hace la prueba más estricta; hace que cuando salte sirva.
        var miranEnLaPrueba = TodasLasBanderas.Where(b => b.SoloLectura)
                                              .Select(b => b.Bandera).ToArray();
        var faltanEnElPrograma = miranEnLaPrueba
            .Except(Config.BanderasQueMiran, StringComparer.OrdinalIgnoreCase).ToArray();
        var sobranEnElPrograma = Config.BanderasQueMiran
            .Except(miranEnLaPrueba, StringComparer.OrdinalIgnoreCase).ToArray();

        Vale(faltanEnElPrograma.Length == 0,
             $"a `Config.BanderasQueMiran` no le falta ninguna (le faltan: {string.Join(", ", faltanEnElPrograma)})");
        Vale(sobranEnElPrograma.Length == 0,
             $"y no le sobra ninguna (le sobran: {string.Join(", ", sobranEnElPrograma)})");

        Igual(TodasLasBanderas.Count(b => b.SoloLectura), Config.BanderasQueMiran.Length,
              "la lista del programa y la de esta prueba tienen el mismo tamaño");

        // El guardia del árbol depende de `SoloLectura`, así que se comprueban las dos ramas
        // sin tocar el valor real más de un instante.
        bool antes = Config.SoloLectura;
        try
        {
            Config.SoloLectura = true;
            Vale(ConfigArbol.NoSeDebeGuardar(Config.Ruta),
                 "con SoloLectura puesto, el config del usuario no se guarda");
            var otro = Path.Combine(Path.GetTempPath(), "nubebar-pruebas-d8", "config.json");
            Vale(!ConfigArbol.NoSeDebeGuardar(otro),
                 "pero un fichero de prueba en el temporal sí se puede escribir");

            Config.SoloLectura = false;
            Vale(!ConfigArbol.NoSeDebeGuardar(Config.Ruta),
                 "y sin SoloLectura, el config normal se guarda como siempre");
        }
        finally { Config.SoloLectura = antes; }
    }

    // ================= Ajustes =================

    static void D8LasSeccionesDeAjustesNoChocan()
    {
        Grupo("D8: las secciones de Ajustes, con la de NubeHypr dentro");

        // La página de NubeHypr es nueva en D6. Lo que esta prueba exige es que no haya
        // pisado nada: ni un id repetido, ni un subtítulo que falte, ni un icono duplicado
        // en la navegación —que es lo que hace que dos secciones se confundan de un vistazo.
        MainWindow.AsegurarWidgetsRegistrados();

        Vale(VentanaAjustes.SubtituloDe("nubehypr").Length > 0,
             "la página de NubeHypr tiene subtítulo");
        Vale(VentanaAjustes.GlifoDeSeccion("nubehypr").Length > 0, "y glifo");
        Vale(VentanaAjustes.GlifoDeSeccion("nubehypr") != VentanaAjustes.GlifoDeSeccion("atajos"),
             "y no es el mismo glifo que Atajos: son dos páginas distintas");

        // Un id que no existe cae en el glifo de respaldo y no revienta: una página nueva
        // mal registrada tiene que salir fea, no tumbar la ventana.
        Vale(VentanaAjustes.GlifoDeSeccion("no-existe").Length > 0,
             "un id desconocido devuelve el glifo de respaldo");
        Igual("", VentanaAjustes.SubtituloDe("no-existe"), "y subtítulo vacío, no una excepción");
    }

    static void D8UnSoloValorDeSerieParaElAtajo()
    {
        Grupo("D8: el atajo de la vista general tiene UN valor de serie, no dos");

        // ---------- El fallo que esto cierra ----------
        //
        // Se vio mirando una captura de Ajustes → Atajos: el campo enseñaba
        // «Ctrl+Alt+Space» con un error en rojo debajo, cuando el atajo registrado y
        // funcionando era «Ctrl+Alt+W». La página leía el valor del árbol con el respaldo
        // VIEJO, y el modelo tipado usaba el nuevo. Con la clave ausente del fichero —el
        // caso de cualquier instalación que no la haya tocado— los dos no coincidían.
        //
        // Y lo peor no era enseñarlo mal: pulsar Guardar habría escrito «Ctrl+Alt+Space»
        // encima de un atajo que funcionaba, dejando al usuario sin vista general.
        Igual(Barra.AtajoDeSerie, new Barra().AtajoVistaGeneral,
              "el modelo tipado usa la constante, no un literal suyo");

        // Un config SIN la clave tiene que dar el mismo valor por los dos caminos: el
        // tipado (lo que se registra) y el árbol con respaldo (lo que enseña Ajustes).
        var sinClave = new System.Text.Json.Nodes.JsonObject
        {
            ["Version"] = Esquema.Actual,
            ["Barra"] = new System.Text.Json.Nodes.JsonObject { ["Alto"] = 38 },
        };
        var delArbol = sinClave["Barra"]!["AtajoVistaGeneral"]?.GetValue<string>()
                       ?? Barra.AtajoDeSerie;
        Igual(new Barra().AtajoVistaGeneral, delArbol,
              "sin la clave en el fichero, el árbol y el modelo dicen lo MISMO");

        // Y el de serie tiene que ser una combinación que Windows entienda: un valor de
        // serie que no se pueda traducir dejaría a todo el mundo sin vista general.
        Vale(Nativo.TraducirAtajo(Barra.AtajoDeSerie, out _, out _),
             $"«{Barra.AtajoDeSerie}» es una combinación que Windows entiende");
        Vale(HyprAtajos.Valida(Barra.AtajoDeSerie), "y es válida como atajo (lleva modificador)");

        // No puede chocar con ninguno de los 19 de NubeHypr: si chocara, uno de los dos
        // no se registraría y el usuario no sabría cuál.
        var serie = HyprAtajos.DeSerie();
        var normalizado = HyprAtajos.Normalizar(Barra.AtajoDeSerie);
        Vale(!serie.ContainsKey(normalizado),
             $"«{normalizado}» no choca con ninguno de los 19 de NubeHypr");
    }

    static void D8LosGlifosQueSePintanExisten()
    {
        Grupo("D8: los glifos son puntos de código de verdad, no cajas");

        // No se puede comprobar desde aquí que la fuente tenga el glifo —eso necesita ojos y
        // la fuente instalada— pero sí que lo que se pinta sea un carácter válido y esté en
        // el rango de los iconos de Windows. Un `char.ConvertFromUtf32` con un número
        // inventado no falla: devuelve algo que la fuente no tiene y sale una caja.
        // De `VentanaAjustes.Secciones`, no de una lista escrita aquí. La de antes tenía los
        // doce ids a mano y al añadir el decimotercero no se enteró: «escritorio» se quedó
        // sin icono propio, cayó en la rueda dentada por defecto y acabó compartiendo icono
        // con «Sistema» en la misma barra lateral, con la prueba en verde.
        foreach (var id in VentanaAjustes.Secciones)
        {
            var g = VentanaAjustes.GlifoDeSeccion(id);
            Vale(g.Length >= 1, $"«{id}» tiene glifo");
            int cp = char.ConvertToUtf32(g, 0);
            // El bloque de uso privado de Segoe MDL2 / Fluent Icons
            Vale(cp >= 0xE000 && cp <= 0xF8FF,
                 $"«{id}» usa un punto de código del bloque de iconos (U+{cp:X4})");

            // Y propio, no heredado del valor por defecto: si no, dos secciones distintas
            // se dibujan igual en la lista por la que se navega.
            Vale(VentanaAjustes.TieneIconoPropio(id), $"«{id}» tiene icono propio, no el de reserva");
            Vale(VentanaAjustes.TieneSubtituloPropio(id),
                 $"«{id}» tiene subtítulo propio, no el de su categoría");
            Vale(VentanaAjustes.SubtituloDe(id).Length > 10,
                 $"y el subtítulo dice algo ({VentanaAjustes.SubtituloDe(id)})");
        }

        // Ninguna comparte icono con otra. Es lo que de verdad se quería y lo que no se
        // comprobaba: solo se miraban dos secciones sueltas entre sí.
        var vistos = new Dictionary<string, string>();
        foreach (var id in VentanaAjustes.Secciones)
        {
            var g = VentanaAjustes.GlifoDeSeccion(id);
            Vale(!vistos.ContainsKey(g),
                 $"«{id}» no repite el icono de «{(vistos.TryGetValue(g, out var otra) ? otra : "")}»");
            vistos[g] = id;
        }

        // Los dos de la vista general, que se escriben a mano en MainWindow.Vista.cs
        foreach (var (nombre, cp) in new[]
        {
            ("cuadrícula", MainWindow.CodigoCuadricula),
            ("aspa", MainWindow.CodigoAspa),
        })
            Vale(cp >= 0xE000 && cp <= 0xF8FF,
                 $"el glifo de {nombre} está en el bloque de iconos (U+{cp:X4})");

        // Y los del indicador breve, que se repasan desde su propio array para que añadir
        // uno nuevo no se quede fuera de esta prueba por olvido.
        foreach (var cp in HyprAviso.Codigos)
        {
            Vale(cp >= 0xE000 && cp <= 0xF8FF,
                 $"el glifo U+{cp:X4} del indicador está en el bloque de iconos");
            Vale(HyprAviso.Glifo(cp).Length >= 1, $"U+{cp:X4} produce un carácter");
        }
        Igual(HyprAviso.Codigos.Length, HyprAviso.Codigos.Distinct().Count(),
              "y no hay dos familias con el mismo punto de código repetido por copiar y pegar");

        // ---------- Que la FUENTE lo tenga, no solo que el número esté en el rango ----------
        //
        // Esta parte existe por un fallo real: el glifo de los escritorios era 0xE7E8, que es
        // un botón de encendido. Estaba en el rango, así que la comprobación de arriba lo dio
        // por bueno; se vio en una captura, con un icono de apagar al lado de «Espacio →».
        //
        // Esto no comprueba que el dibujo sea el ADECUADO —eso necesita ojos, y se hizo
        // mirando las capturas— pero sí que exista: un punto de código que la fuente no tiene
        // sale como una caja vacía, y eso sí se puede detectar aquí.
        //
        // Si la fuente no estuviera instalada, no se finge que la prueba pasa: se salta y se
        // dice. Dar por verificado algo que no se ha podido medir es peor que no medirlo.
        var tipo = new Typeface(MainWindow.FuenteIconos);
        if (tipo.TryGetGlyphTypeface(out var gt))
        {
            foreach (var cp in HyprAviso.Codigos)
                Vale(gt.CharacterToGlyphMap.ContainsKey(cp),
                     $"la fuente tiene de verdad el glifo U+{cp:X4} del indicador");

            foreach (var id in new[] { "apariencia", "colores", "fondo", "wallpaper", "perfiles",
                                       "modulos", "biblioteca", "apps", "atajos", "nubehypr",
                                       "inicio", "sistema" })
                Vale(gt.CharacterToGlyphMap.ContainsKey(
                         char.ConvertToUtf32(VentanaAjustes.GlifoDeSeccion(id), 0)),
                     $"la fuente tiene de verdad el glifo de «{id}»");

            foreach (var cp in new[] { MainWindow.CodigoCuadricula, MainWindow.CodigoAspa })
                Vale(gt.CharacterToGlyphMap.ContainsKey(cp),
                     $"la fuente tiene de verdad U+{cp:X4}");
        }
        else Log.Info($"[pruebas] «{MainWindow.FuenteIconos}» no está instalada: " +
                      "no se puede comprobar que los glifos existan de verdad");

        // Y la nube de la escena no es un glifo: es geometría. Es lo que la hace inmune a
        // que falte una fuente, y conviene que siga siendo así.
        Vale(Nubecita.Molde().Bounds.Width > 0, "la nube es geometría, no un carácter");
    }

    // ================= Lo que no puede cambiar =================

    static void D8NubeHyprSigueApagadoDeSerie()
    {
        Grupo("D8: después de ocho fases, NubeHypr sigue apagado de serie");

        // Es la promesa que atraviesa todas las fases, y la que más fácil se rompe sin
        // querer: basta con que alguien ponga un `= true` en las opciones.
        var opc = new OpcionesHypr();
        Vale(!opc.Activo, "la capa nace apagada");
        Igual("simulacion", opc.Modo, "y el modo de serie es simulación");
        Igual(0, opc.Reglas.Count, "sin reglas");
        Igual(0, opc.Atajos.Count, "sin atajos guardados");
        Vale(!opc.RestaurarAlOrganizar, "y sin desmaximizar ventanas por su cuenta");

        // Un config nuevo del todo, igual
        var nuevo = new Config();
        Vale(!nuevo.NubeHypr.Activo, "un config nuevo nace con la capa apagada");

        // Y el modo efectivo de un valor desconocido NUNCA es "aplicar". Es la regla de D0 y
        // sigue en pie: lo peor que puede pasar por una errata en el config es que calcule y
        // no mueva, no que mueva sin que se le pida.
        foreach (var raro in new[] { "APLICAR_TODO", "si", "true", "", "   ", "aplicarrr" })
            Vale(NubeHypr.ModoEfectivo(true, raro) != NubeHypr.Modo.Aplicar,
                 $"«{raro}» no se interpreta como «aplicar»");

        // Con la capa apagada, el modo es apagado diga lo que diga el modo.
        Igual(NubeHypr.Modo.Apagado, NubeHypr.ModoEfectivo(false, "aplicar"),
              "apagada gana sobre el modo");

        // Y el modo seguro gana sobre todo lo demás.
        HyprSalud.Olvidar();
        HyprSalud.Entrar("prueba de D8");
        Igual(NubeHypr.Modo.Apagado, HyprSalud.ModoConSeguro(true, "aplicar"),
              "y el modo seguro gana sobre la capa encendida");
        HyprSalud.Olvidar();
    }

    // ================= El veredicto de la vigilia =================
    //
    // Esto es lógica pura y me equivoqué en ella, así que le toca prueba. La primera versión
    // comparaba el valor absoluto de la pendiente y escribió «-4,55 MB/min (SUBE)» en la
    // primera medida larga de verdad: un número negativo etiquetado como subida.
    //
    // Se prueba con muestras inventadas, que es lo que permite comprobar el caso que no se
    // puede provocar a voluntad en una máquina real: una fuga.
    static DiagVigilia.Muestra Muestra(double minuto, double mb, int manejadores = 1000) =>
        new(TimeSpan.FromMinutes(minuto), (long)(mb * 1048576), 10 * 1048576,
            TimeSpan.FromSeconds(minuto * 2), 0, 0, 0, manejadores, 1);

    static void D8ElVeredictoDeLaVigilia()
    {
        Grupo("vigilia: el veredicto dice la dirección correcta");

        // ---------- Plana ----------
        var plana = new List<DiagVigilia.Muestra>();
        for (int i = 0; i <= 30; i++) plana.Add(Muestra(i * 0.5, 250 + (i % 3)));
        Vale(DiagVigilia.Veredicto(plana).Contains("plana"), "memoria estable: plana");
        Vale(!DiagVigilia.Veredicto(plana).Contains("SUBE"), "y no dice que suba");

        // ---------- Una fuga de verdad ----------
        var fuga = new List<DiagVigilia.Muestra>();
        for (int i = 0; i <= 30; i++) fuga.Add(Muestra(i * 0.5, 200 + i * 2));
        Vale(DiagVigilia.Veredicto(fuga).Contains("SUBE"), "una fuga se llama SUBE");

        // ---------- Bajar NO es subir ----------
        //
        // El fallo exacto que hubo. Y además: bajar no es un problema, así que el texto
        // tiene que decir por qué pasa, no sonar a alarma.
        var baja = new List<DiagVigilia.Muestra>();
        for (int i = 0; i <= 30; i++) baja.Add(Muestra(i * 0.5, 300 - i * 2));
        var v = DiagVigilia.Veredicto(baja);
        Vale(!v.Contains("SUBE"), "una memoria que BAJA no se llama SUBE");
        Vale(v.Contains("baja"), "se llama bajar");

        // ---------- Un escalón no es una tendencia ----------
        //
        // Diez minutos quieta en 250 y una caída a 207 por una recolección. La pendiente de
        // la ventana entera sale negativa y suena a tendencia; el recorrido lo desmiente.
        var escalon = new List<DiagVigilia.Muestra>();
        for (int i = 0; i <= 24; i++) escalon.Add(Muestra(i * 0.5, 250));
        for (int i = 25; i <= 30; i++) escalon.Add(Muestra(i * 0.5, 207));
        var e = DiagVigilia.Veredicto(escalon);
        Vale(e.Contains("recorrido"), "el veredicto enseña el recorrido, no solo la pendiente");
        Vale(e.Contains("250") && e.Contains("207"),
             "y con el máximo y el valor final se ve que fue un escalón");

        // ---------- Un pico de trabajo NO es una fuga ----------
        //
        // Sube, hace un pico y vuelve a bajar: eso es abrir el menú Inicio, que
        // construye un panel grande de iconos. Una fuga sube y SE QUEDA arriba.
        // La primera versión decía «SUBE» en los dos casos y me hizo perseguir una
        // fuga que no existía: lo que había en el registro era el usuario usando la
        // barra.
        var pico = new List<DiagVigilia.Muestra>();
        for (int i = 0; i <= 20; i++) pico.Add(Muestra(i * 0.5, 200));
        for (int i = 21; i <= 26; i++) pico.Add(Muestra(i * 0.5, 330));
        for (int i = 27; i <= 30; i++) pico.Add(Muestra(i * 0.5, 240));
        var p = DiagVigilia.Veredicto(pico);
        Vale(p.Contains("pico"), "un pico que vuelve a bajar se llama pico");
        Vale(!p.Contains("SUBE"), "y NO se llama SUBE");

        // Y una que sube y se queda arriba sigue siendo una fuga.
        var seQueda = new List<DiagVigilia.Muestra>();
        for (int i = 0; i <= 20; i++) seQueda.Add(Muestra(i * 0.5, 200));
        for (int i = 21; i <= 30; i++) seQueda.Add(Muestra(i * 0.5, 200 + (i - 20) * 15));
        Vale(DiagVigilia.Veredicto(seQueda).Contains("SUBE"),
             "lo que sube y se queda arriba sigue llamándose SUBE");

        // ---------- Demasiado corto para juzgar ----------
        //
        // Cuatro muestras en minuto y medio dieron «+3,16 MB/min SUBE» y no era una fuga:
        // era el calentamiento de las cachés. Ahora se dice que no se puede juzgar.
        var corta = new List<DiagVigilia.Muestra>
        {
            Muestra(0.1, 145), Muestra(0.6, 170), Muestra(1.1, 175), Muestra(1.5, 173),
        };
        var c = DiagVigilia.Veredicto(corta);
        Vale(!c.Contains("SUBE"), "minuto y medio no basta para gritar fuga");
        Vale(c.Contains("min"), "y se dice cuánto ha durado y cuánto hace falta");

        Vale(DiagVigilia.Veredicto(new List<DiagVigilia.Muestra>()).Length > 0,
             "sin muestras no revienta");

        // ---------- Los minutos de la bandera ----------
        Vale(DiagVigilia.Minutos > 0, "la duración de serie es positiva");
        Vale(DiagVigilia.Cada.TotalSeconds > 0, "y el intervalo también");
        Vale(DiagVigilia.MinimoParaJuzgar > DiagVigilia.Cada,
             "y hace falta más de una muestra para juzgar");
    }

    static void D8ElCatalogoDeFasesEstaCompleto()
    {
        Grupo("D8: las piezas de las ocho fases están todas en su sitio");

        // Una prueba de inventario. No comprueba comportamiento: comprueba que no falta una
        // pieza. Sirve para el día que un archivo se quede fuera del proyecto por un merge o
        // por un patch mal aplicado — que en esta sesión ya pasó una vez con
        // `MainWindow.Vista.cs`, y lo que lo delató fue el compilador por casualidad.
        var piezas = new (string Fase, Func<bool> Hay)[]
        {
            ("D0: contrato y modos", () => Enum.GetValues<NubeHypr.Modo>().Length == 3),
            ("D0: lista protegida", () => !NubeHypr.SePuedeOrganizar("explorer.exe", "Shell_TrayWnd", false, true, true, out _)),
            ("D1: layouts", () => Enum.GetValues<NubeHypr.Layout>().Length >= 4),
            ("D1: comandos", () => NubeHypr.Ejecutar(EstadoNubeHypr.Vacio, new Comando.EnfocarSiguiente()) != null),
            ("D2: adaptador", () => HyprAdaptador.VecesMovida >= 0),
            ("D3+D9.1: 20 atajos", () => HyprAtajos.Nombres.Length == 20),
            ("D3: rango de ids", () => HyprTeclas.EsNuestro(HyprTeclas.IdBase)),
            ("D3: restaurar", () => new HyprRestaurar.Memoria().Cuantas == 0),
            ("D4: espacios", () => HyprEspacios.UnSoloEscritorio().Count == 1),
            ("D5: reglas", () => HyprReglas.Ordenar(new List<HyprReglas.Regla>()).Count == 0),
            ("D5: lector tolerante", () => new HyprReglas.LectorTolerante().HandleNull),
            ("D5.1: vigilante", () => HyprVigilante.Nuevas(new List<VentanaModelo>(), new HashSet<IntPtr>()).Count == 0),
            ("D6: paleta", () => HyprPinta.EsAzul(HyprPinta.AzulNube) && HyprPinta.EsCoral(HyprPinta.CoralNube)),
            ("D6: vista", () => HyprVista.PintaDe(HyprVista.Nivel.Elegida).Halo),
            ("D6: indicador", () => HyprIndicador.PuntoDe(HyprIndicador.Estado.Lleno) > 0),
            ("D6: catálogo", () => HyprCatalogo.AgruparAtajos(HyprAtajos.Nombres).Count > 1),
            ("D7: supervisor", () => Enum.GetValues<HyprSalud.Estado>().Length == 3),
            ("D7: modo seguro", () => !HyprSalud.Seguro),
            ("D7: regresos", () => new HyprRegreso.Memoria().Cuantas == 0),
            ("D7: rescate", () => HyprRescate.Juzgar("{\"Version\":4}").Sirve),
            ("D6: indicador breve", () => HyprAviso.Codigos.Length == 7),
            ("D9.1: respeto", () => HyprRespeto.DeQuienFue(default, default, default) == HyprRespeto.Quien.SinCambio),
            ("D9.1: deshacer", () => new HyprDeshacer.Pila().Cuantos == 0),
            ("vigilia: mide y no toca", () => Config.MiraYNoToca(new[] { DiagVigilia.Bandera })),
        };

        foreach (var (fase, hay) in piezas)
        {
            bool ok;
            try { ok = hay(); } catch (Exception ex) { ok = false; Log.Error("D8 inventario " + fase, ex); }
            Vale(ok, $"{fase}: está y responde");
        }

        // Y el esquema va por donde tiene que ir: v4 es la que trae la sección de NubeHypr.
        Vale(Esquema.Actual >= 4, $"el esquema va por la v{Esquema.Actual} (la capa llegó en la v4)");
        Vale(Esquema.Actual >= 4, "y no ha bajado");
    }
}
