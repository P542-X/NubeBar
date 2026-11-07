using System;
using System.IO;
using System.Linq;

namespace NubeBar;

// El modelo de módulos: añadir, quitar, mover, ocultar, guardar y volver a leer.
//
// ---------- Qué cubre esto y qué no ----------
//
// Esto es la mitad PURA del problema de «añado un módulo y no aparece»: que las listas
// queden como tienen que quedar y que sobrevivan al disco. Se puede comprobar sin abrir
// una ventana y por eso está aquí.
//
// La otra mitad —que lo que queda en las listas llegue a la pantalla— no se puede
// comprobar así, y no por pereza: el fallo de verdad que se encontró vivía en un caché de
// repintado que solo existe con una barra montada, y las pruebas puras lo habrían dado
// por bueno para siempre. Esa mitad la cubre `--diagmodulos`, que arranca una barra de
// verdad contra un config de usar y tirar y mira el árbol visual. Lo que se mide aquí es
// lo que se puede medir aquí, y queda dicho qué no.
public static partial class Pruebas
{
    internal static void CorrerModulos()
    {
        ModulosElModeloHaceLoQueDice();
        ModulosSobrevivenAlDisco();
        ModulosUnaSolaFuenteDeVerdad();
        ModulosElCatalogoEstaCompleto();
    }

    // Un árbol de config de usar y tirar, en una carpeta temporal. Nunca el del usuario:
    // lo garantiza `ConfigArbol.NoSeDebeGuardar`, pero además es que ni se nombra.
    static (ConfigArbol Arbol, string Ruta) ArbolDePrueba(string texto)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-pruebas-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "config.json");
        File.WriteAllText(ruta, texto);
        return (ConfigArbol.Cargar(ruta), ruta);
    }

    const string TresZonas = """
    { "Modulos": { "Izquierda": ["inicio"],
                   "Centro": ["fijadas","apps","reloj"],
                   "Derecha": ["cpu","ram","fecha"],
                   "Ocultos": [] } }
    """;

    static DescriptorWidget Desc(string id, bool repetible = false) =>
        new(id, id, id, "", CategoriaWidget.Sistema, "Derecha", repetible);

    // ================= Añadir, quitar, mover, ocultar =================
    static void ModulosElModeloHaceLoQueDice()
    {
        Grupo("módulos: el modelo hace lo que dice");

        var (arbol, _) = ArbolDePrueba(TresZonas);
        var z = new ZonasWidgets(arbol);

        // ---------- Añadir ----------
        Vale(z.Anadir(Desc("escritorios"), "Derecha"), "se añade «escritorios» a Derecha");
        Igual("escritorios", z.De("Derecha").Last(), "y cae al final, que es donde se soltó");
        Igual("Derecha", z.DondeEsta("escritorios") ?? "", "y se encuentra allí");
        Igual(1, z.CuantosHay("escritorios"), "una sola vez");

        // Un no repetible no se añade dos veces, y lo dice devolviendo false en vez de
        // dejar la zona con dos y que la isla se estrelle al meter el mismo elemento en
        // dos padres.
        Vale(!z.Anadir(Desc("escritorios"), "Izquierda"), "no se añade un segundo «escritorios»");
        Igual(1, z.CuantosHay("escritorios"), "sigue habiendo uno");

        // Y uno repetible sí.
        Vale(z.Anadir(Desc("separador", repetible: true), "Centro"), "se añade un separador");
        Vale(z.Anadir(Desc("separador", repetible: true), "Centro"), "y otro");
        Igual(2, z.CuantosHay("separador"), "porque el separador sí es repetible");

        // ---------- Mover entre zonas ----------
        int i = z.De("Derecha").IndexOf("escritorios");
        Vale(z.Mover("Derecha", i, "Izquierda", 0), "se mueve «escritorios» al principio de Izquierda");
        Igual("escritorios", z.De("Izquierda").First(), "y sale el primero");
        Vale(!z.De("Derecha").Contains("escritorios"), "y ya no está en Derecha");

        // ---------- Mover dentro de la zona ----------
        //
        // Es el caso que una comprobación por «¿está?» deja pasar entera: el módulo sigue
        // estando y lo que ha fallado es el orden.
        Vale(z.Mover("Izquierda", 0, "Izquierda", 1), "se mueve dentro de Izquierda");
        Igual("escritorios", z.De("Izquierda")[1], "y queda en la segunda posición");
        Vale(!z.Mover("Izquierda", 1, "Izquierda", 1), "moverlo a donde ya está no cuenta como cambio");

        // ---------- Ocultar conserva el sitio ----------
        //
        // Lo que hace que ocultar no sea «quitar y volver a añadir»: al mostrarlo vuelve
        // donde estaba y no al final. Si se perdiera el sitio, ocultar un módulo del medio
        // te reordenaría la barra cada vez.
        int sitio = z.De("Izquierda").IndexOf("escritorios");
        z.Ocultar("escritorios");
        Vale(z.EstaOculto("escritorios"), "oculto");
        Igual(sitio, z.De("Izquierda").IndexOf("escritorios"), "pero sigue en su sitio en la zona");
        Vale(!ZonasWidgets.ADibujar(z.De("Izquierda"), z.Ocultos(), editando: false)
                          .Any(p => p.Id == "escritorios"),
             "y no se dibuja en modo normal");
        Vale(ZonasWidgets.ADibujar(z.De("Izquierda"), z.Ocultos(), editando: true)
                         .Any(p => p.Id == "escritorios"),
             "pero SÍ editando, o no habría forma de volver a mostrarlo desde la isla");
        z.Mostrar("escritorios");
        Igual(sitio, z.De("Izquierda").IndexOf("escritorios"), "al mostrarlo vuelve a su sitio");

        // ---------- Quitar ----------
        Vale(z.Quitar("escritorios"), "se quita");
        Igual(0, z.CuantosHay("escritorios"), "y no queda ninguno");
        Vale(!z.Quitar("escritorios"), "quitar lo que no está no hace nada y lo dice");
        Vale(!z.EstaOculto("escritorios"),
             "y al quitar el último deja de figurar como oculto: si no, al volver a " +
             "añadirlo aparecería invisible");

        // ---------- Lo que no revienta ----------
        Vale(!z.Mover("Arriba", 0, "Centro", 0), "una zona que no existe no mueve nada");
        Vale(!z.Mover("Centro", 99, "Derecha", 0), "ni un índice fuera de la lista");
        Vale(!z.Quitar("Centro", -1), "ni un índice negativo");
    }

    // ================= Y sobreviven al disco =================
    static void ModulosSobrevivenAlDisco()
    {
        Grupo("módulos: lo editado llega al fichero y vuelve igual");

        var (arbol, ruta) = ArbolDePrueba(TresZonas);
        new ZonasWidgets(arbol).Anadir(Desc("escritorios"), "Centro");
        arbol.Guardar();

        // Releído del disco por otro árbol: es la comprobación que importa. Un cambio que
        // solo vive en el objeto en memoria se pierde al cerrar Ajustes, y eso no se nota
        // hasta el día siguiente.
        var vuelto = new ZonasWidgets(ConfigArbol.Cargar(ruta));
        Igual("Centro", vuelto.DondeEsta("escritorios") ?? "", "tras releer el fichero sigue en Centro");
        Igual(4, vuelto.De("Centro").Count, "con los tres de antes y el nuevo");

        // Y las claves que esta versión no conoce no se pierden por el camino: es la razón
        // de que la edición pase por el árbol y no por el modelo tipado.
        var (conRarezas, ruta2) = ArbolDePrueba("""
        { "Modulos": { "Izquierda": ["inicio"], "Centro": [], "Derecha": [] },
          "UnaClaveDelFuturo": "no la conozco",
          "Modulos2": { "x": 1 } }
        """);
        new ZonasWidgets(conRarezas).Anadir(Desc("reloj"), "Centro");
        conRarezas.Guardar();
        var texto = File.ReadAllText(ruta2);
        Vale(texto.Contains("UnaClaveDelFuturo"), "editar módulos no se come las claves desconocidas");
        Vale(texto.Contains("Modulos2"), "ni las secciones desconocidas");
        Vale(texto.Contains("reloj"), "y sí escribe lo editado");
    }

    // ================= Una sola fuente de verdad =================
    //
    // El fallo que costó encontrar: `Config.MigrarYDerivarPaneles` hace que el panel
    // REFERENCIE las listas de `Modulos`, no que las copie. Mientras eso sea verdad, mirar
    // una u otra da lo mismo. En cuanto algo asigna una lista nueva en vez de mutar la de
    // siempre, se separan — y el trozo del programa que mire la que no se dibuja tomará
    // decisiones sobre una barra que no existe.
    //
    // Pasó: `PrepararFijas` preguntaba a `_cfg.Modulos` si "fijadas" estaba puesto, dijo
    // que no, dejó la tira en null, y `ModuloFijas` se estrelló contra ella mientras
    // `LlenarZonas` —que mira el panel— la montaba igual. En el log: un
    // NullReferenceException, un hueco vacío y un recuento de widgets perfectamente normal.
    static void ModulosUnaSolaFuenteDeVerdad()
    {
        Grupo("módulos: el panel y Modulos son la MISMA lista, no una copia");

        var c = new Config
        {
            Modulos = new Modulos
            {
                Izquierda = new() { "inicio" },
                Centro = new() { "fijadas", "apps", "reloj" },
                Derecha = new() { "cpu" },
                Ocultos = new() { "cpu" },
            },
        };
        // El método de verdad, no un atajo de pruebas: lo que se quiere comprobar es lo que
        // hace el arranque, y un atajo propio comprobaría el atajo.
        Config.MigrarYDerivarPaneles(c);

        var p = c.Paneles.FirstOrDefault(x => x.Id == "principal");
        Vale(p != null, "hay panel principal tras derivar");
        if (p == null) return;

        Vale(ReferenceEquals(p.Zonas.Izquierda, c.Modulos.Izquierda), "Izquierda es la misma instancia");
        Vale(ReferenceEquals(p.Zonas.Centro, c.Modulos.Centro), "Centro es la misma instancia");
        Vale(ReferenceEquals(p.Zonas.Derecha, c.Modulos.Derecha), "Derecha es la misma instancia");
        Vale(ReferenceEquals(p.Zonas.Ocultos, c.Modulos.Ocultos), "y los Ocultos también");

        // Y la consecuencia, que es lo que de verdad hay que poder dar por hecho: tocar una
        // se ve en la otra. Mutando, que es la única forma: asignar rompe el enlace.
        c.Modulos.Centro.Add("escritorios");
        Vale(p.Zonas.Centro.Contains("escritorios"),
             "añadir a Modulos.Centro se ve en el panel sin volver a derivar");

        p.Zonas.Derecha.Remove("cpu");
        Vale(!c.Modulos.Derecha.Contains("cpu"),
             "y quitar del panel se ve en Modulos: es una lista, no dos");
    }

    // ================= El catálogo =================
    static void ModulosElCatalogoEstaCompleto()
    {
        Grupo("módulos: el catálogo y lo que se puede poner en el config");

        RegistroWidgets.Asegurar();
        var ids = RegistroWidgets.Ids.ToList();

        Vale(ids.Count > 0, "el registro tiene widgets");
        Igual(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
              "y no hay dos con el mismo id");

        foreach (var id in ids)
        {
            var d = RegistroWidgets.Buscar(id)?.Descriptor;
            Vale(d != null, $"«{id}» se puede buscar por su id");
            if (d == null) continue;

            Vale(d.Nombre.Length > 0, $"«{id}» tiene nombre legible");
            Vale(d.Descripcion.Length > 0, $"«{id}» tiene una línea de descripción");
            Vale(ZonasWidgets.Nombres.Contains(d.ZonaPreferida),
                 $"«{id}» prefiere una zona que existe ({d.ZonaPreferida})");
            // Un widget que puede no verse tiene que decir POR QUÉ, con una frase, no con
            // un booleano: el aviso se enseña tal cual al usuario.
            if (d.PuedeNoVerse != null)
                Vale(d.PuedeNoVerse.Length > 10, $"«{id}» explica por qué puede no verse");
        }

        // Lo que la ventana de ajustes ofrece es EXACTAMENTE el registro. Tuvo su propia
        // lista escrita a mano y la lista se quedó atrás: «red» llegó a estar ofrecida sin
        // existir, y añadirla dejaba un hueco de «módulo desconocido» en la barra.
        var puestos = new[] { "inicio", "reloj", "fecha", "apps", "fijadas", "escritorios" };
        foreach (var id in puestos)
            Vale(RegistroWidgets.Existe(id), $"«{id}», que la gente tiene puesto, existe en el registro");

        // ---------- «red» ahora SÍ existe ----------
        //
        // Aquí se exigía lo contrario: «red» no era un módulo, era parte de «control». Y la
        // prueba estaba bien puesta, porque el fallo que vigilaba era real — la ventana de
        // ajustes llegó a ofrecer «red» sin que existiera, y añadirla dejaba un hueco de
        // «módulo desconocido» en la barra.
        //
        // Lo que ha cambiado es la decisión, no el riesgo: el usuario pidió poder mover cada
        // icono por su cuenta —«tanto ethernet como sonido como lenguaje de teclado»— y los
        // cuatro se han registrado de verdad. Así que lo que hay que exigir ahora es que
        // existan Y que se puedan montar, que es lo que aquella prueba protegía en el fondo.
        foreach (var id in new[] { "red", "volumen", "teclado", "pila" })
        {
            Vale(RegistroWidgets.Existe(id), $"«{id}» existe como módulo suyo y se puede mover");
            var d = RegistroWidgets.Buscar(id)?.Descriptor;
            Vale(d is { Glifo.Length: > 0 }, $"«{id}» trae glifo para el editor");
        }

        // Y el bloque sigue ahí: quitarlo dejaría un hueco en la barra de todo el que ya lo
        // tiene puesto, que es exactamente el fallo que vigilaba la prueba de arriba.
        Vale(RegistroWidgets.Existe("control"),
             "y «control» sigue existiendo: quien lo tenga puesto no se queda sin nada");
    }
}
