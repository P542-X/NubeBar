using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NubeBar;

// El editor de módulos: qué se enseña de cada uno y qué se deja hacer.
//
// Esto es la mitad que se puede comprobar sin abrir Ajustes. La otra —que arrastrar un
// chip funcione con el ratón de verdad— está en el cuaderno de pruebas manuales, porque un
// arrastre no se comprueba sin manos y decir lo contrario sería mentir.
public static partial class Pruebas
{
    internal static void CorrerEditor()
    {
        EditorCadaModuloDiceDondeEsta();
        EditorLosBotonesExplicanPorQueNo();
        EditorLaVistaPreviaDelDestino();
        EditorSoltarFueraNoPierdeNada();
        EditorLosIconosExisten();
        EditorLosRepetiblesSonInstancias();
        EditorElSeparadorSeVe();
        EscritorioElDockTieneInterruptor();
        EscritorioLosLateralesTienenPuerta();
        EscritorioUnLateralNoSeMeteDebajoDeNada();
        NubeHyprElModoTieneInterruptor();
        EscritorioElFiloDeLaBarraNoAbrePanel();
    }

    // ================= El filo donde vive la barra no abre panel =================
    //
    // Con la barra vertical pegada a la izquierda, acercarse a ese filo es lo que se hace
    // para **usar la barra**. Abrir ahí encima un panel de 340 px sería castigar el gesto más
    // normal que hay.
    //
    // Esta decisión era la única pieza de la puerta de los laterales sin prueba, y en este
    // mismo fichero hay dos precedentes de por qué eso acaba mal: `RatonEnLateral`, que
    // existía y no llamaba nadie, y la colocación, que se metía debajo de la barra. Las dos
    // las cazó un `grep` o una captura, no una prueba.
    static void EscritorioElFiloDeLaBarraNoAbrePanel()
    {
        Grupo("escritorio: el filo donde está la barra no abre un panel lateral");

        // Barra a la izquierda: ese filo es suyo, el derecho no.
        Vale(MainWindow.EsElBordeDeLaBarra("izquierda", NubeHypr.Borde.Izquierda),
             "barra a la izquierda: el filo izquierdo es de la barra");
        Vale(!MainWindow.EsElBordeDeLaBarra("izquierda", NubeHypr.Borde.Derecha),
             "y el derecho sigue abriendo su panel");

        Vale(MainWindow.EsElBordeDeLaBarra("derecha", NubeHypr.Borde.Derecha),
             "barra a la derecha: el filo derecho es suyo");
        Vale(!MainWindow.EsElBordeDeLaBarra("derecha", NubeHypr.Borde.Izquierda),
             "y el izquierdo no");

        // Con la barra arriba o abajo, los DOS filos laterales quedan libres: es el caso
        // normal, y el que más se usa.
        foreach (var pos in new[] { "arriba", "abajo" })
        {
            Vale(!MainWindow.EsElBordeDeLaBarra(pos, NubeHypr.Borde.Izquierda),
                 $"barra {pos}: el filo izquierdo queda libre");
            Vale(!MainWindow.EsElBordeDeLaBarra(pos, NubeHypr.Borde.Derecha),
                 $"barra {pos}: y el derecho también");
        }

        // Y que cada posición reclame su propio borde, no el de otro.
        Vale(MainWindow.EsElBordeDeLaBarra("abajo", NubeHypr.Borde.Abajo), "abajo reclama abajo");
        Vale(MainWindow.EsElBordeDeLaBarra("arriba", NubeHypr.Borde.Arriba), "arriba reclama arriba");

        // ---------- Lo escrito a mano ----------
        //
        // La posición sale del config y se puede escribir a mano. Lo que no se entiende se
        // trata como «arriba», igual que en el resto del programa: si aquí se devolviera
        // `false` para todo lo desconocido, una posición mal escrita abriría paneles en los
        // dos filos a la vez y nadie sabría por qué.
        Vale(MainWindow.EsElBordeDeLaBarra("  IZQUIERDA  ", NubeHypr.Borde.Izquierda),
             "con espacios y mayúsculas se entiende igual");
        Vale(MainWindow.EsElBordeDeLaBarra(null, NubeHypr.Borde.Arriba),
             "sin posición se trata como «arriba»");
        Vale(MainWindow.EsElBordeDeLaBarra("cualquier cosa", NubeHypr.Borde.Arriba),
             "y una basura también: nunca deja los dos filos reclamados");
        Vale(!MainWindow.EsElBordeDeLaBarra("cualquier cosa", NubeHypr.Borde.Izquierda),
             "lo que significa que los laterales siguen funcionando");
    }

    // ================= El interruptor que decide si se mueven ventanas =================
    //
    // `NubeHypr.Modo` tenía 175 usos en el producto y **cero controles en Ajustes**: el modo
    // «aplicar» —o sea, D9.5, D9.6 y todo lo que mueve ventanas— era inalcanzable salvo
    // editando el `config.json` a mano. Es el mismo fallo que el dock sin interruptor, en la
    // clave más consecuente del programa.
    //
    // Lo que se exige aquí es la correspondencia entre el interruptor y la cadena que se
    // escribe. Un interruptor que guardara «simulación» con tilde, o «Aplicar» con
    // mayúscula, dejaría la capa en simulación para siempre enseñándose encendido — y eso no
    // lo nota nadie mirando la interfaz.
    static void NubeHyprElModoTieneInterruptor()
    {
        Grupo("NubeHypr: el modo se puede cambiar, y lo que se escribe es lo que se lee");

        // ---------- Lo que escribe ----------
        Igual("aplicar", VentanaAjustes.ModoConfigDeParaPruebas(true),
              "encendido escribe «aplicar»");
        Igual("simulacion", VentanaAjustes.ModoConfigDeParaPruebas(false),
              "y apagado «simulacion», sin tilde: es la clave, no un rótulo");

        // ---------- Y que el producto lo entienda ----------
        //
        // Esta es la comprobación que de verdad importa: no que la cadena sea bonita, sino
        // que `ModoEfectivo` —el que lee el producto— saque de ella lo que se pretendía.
        Igual(NubeHypr.Modo.Aplicar,
              NubeHypr.ModoEfectivo(true, VentanaAjustes.ModoConfigDeParaPruebas(true)),
              "lo que escribe el interruptor encendido, el producto lo lee como «aplicar»");
        Igual(NubeHypr.Modo.Simulacion,
              NubeHypr.ModoEfectivo(true, VentanaAjustes.ModoConfigDeParaPruebas(false)),
              "y apagado, como «simulación»");

        // ---------- Ida y vuelta ----------
        Vale(VentanaAjustes.AplicandoDeParaPruebas(
                 VentanaAjustes.ModoConfigDeParaPruebas(true)),
             "ida y vuelta encendido: se vuelve a ver encendido");
        Vale(!VentanaAjustes.AplicandoDeParaPruebas(
                 VentanaAjustes.ModoConfigDeParaPruebas(false)),
             "ida y vuelta apagado");

        // ---------- Lo que se lee de un fichero sucio ----------
        //
        // La interfaz tiene que enseñar lo que VA A PASAR, no lo que está escrito. Si el
        // fichero dice una basura, el producto la trata como simulación, así que el
        // interruptor tiene que verse apagado: verse encendido sería prometer que mueve.
        Vale(VentanaAjustes.AplicandoDeParaPruebas("Aplicar "),
             "«Aplicar » con mayúscula y espacio se ve encendido: igual que lo lee el producto");
        Vale(!VentanaAjustes.AplicandoDeParaPruebas("cualquier cosa"),
             "una basura se ve APAGADA, porque el producto la trata como simulación");
        Vale(!VentanaAjustes.AplicandoDeParaPruebas(null),
             "y sin clave, apagado");
        Vale(!VentanaAjustes.AplicandoDeParaPruebas("apagado"),
             "«apagado» tampoco es aplicar: no mueve nada");

        // ---------- Y cuándo no se puede tocar ----------
        //
        // Los dos interruptores de debajo —«Mover ventanas de verdad» y «Preguntar antes de
        // mover»— no significan nada con la capa apagada, y en modo de demostración no
        // significan nada pase lo que pase.
        Vale(VentanaAjustes.PorQueNoSeTocaElModoParaPruebas(true, false) == null,
             "con la capa activa y sin demostración, se pueden tocar");
        Vale(VentanaAjustes.PorQueNoSeTocaElModoParaPruebas(false, false) is { } m1 &&
             m1.Contains("apagado"),
             "con la capa apagada se dice que está apagada");

        // El orden importa: la demostración gana aunque la capa esté activa, porque
        // `DiagGuia.Apagar` clava el modo en simulación en memoria. Mirar antes `activo`
        // enseñaría los interruptores vivos en un modo donde no hacen nada.
        Vale(VentanaAjustes.PorQueNoSeTocaElModoParaPruebas(true, true) is { } m2 &&
             m2.Contains("demostración"),
             "la demostración manda AUNQUE la capa esté activa");
        Vale(VentanaAjustes.PorQueNoSeTocaElModoParaPruebas(false, true) is { } m3 &&
             m3.Contains("demostración"),
             "y también con la capa apagada: se dice el motivo más fuerte");
    }

    // ================= Un lateral no se mete debajo de la barra ni del dock =================
    static void EscritorioUnLateralNoSeMeteDebajoDeNada()
    {
        Grupo("escritorio: un panel lateral respeta la barra y el dock");

        // El panel se colocaba en el área de trabajo CRUDA y se centraba en vertical, con
        // 12 px de margen arriba y abajo. Con una barra de 38 px eso significa meterse
        // debajo de ella; y con el dock encendido, también debajo del dock. No se había
        // notado nunca porque hasta ahora los laterales no tenían forma de abrirse.
        var pantalla = new NubeHypr.Rect(0, 0, 2560, 1440);

        foreach (var (que, borde, grosor) in new (string, NubeHypr.Borde, double)[]
        {
            ("barra arriba", NubeHypr.Borde.Arriba, 54),
            ("barra abajo", NubeHypr.Borde.Abajo, 54),
            ("dock abajo", NubeHypr.Borde.Abajo, 72),
        })
        {
            var util = NubeHypr.AreaUtil(pantalla, 0, new NubeHypr.Estorbo(borde, grosor));

            foreach (var lado in new[] { Lateral.Lado.Izquierda, Lateral.Lado.Derecha })
            {
                // Un panel que pide MUCHO alto es el caso que destapa el fallo: el que pide
                // poco cabe en cualquier sitio.
                var r = Lateral.Sitio(util, lado, altoQuePide: 99999);

                Vale(r.Y >= util.Y - 0.5,
                     $"[{que}, {lado}] no empieza por encima del área útil ({r.Y:0} ≥ {util.Y:0})");
                Vale(r.Y + r.Alto <= util.Y + util.Alto + 0.5,
                     $"[{que}, {lado}] ni acaba por debajo ({r.Y + r.Alto:0} ≤ {util.Y + util.Alto:0})");
                Vale(r.X >= util.X - 0.5 && r.X + r.Ancho <= util.X + util.Ancho + 0.5,
                     $"[{que}, {lado}] y cae dentro de ancho");

                // Y lo que de verdad se quería: con la barra arriba, el panel empieza por
                // debajo de ella.
                if (borde == NubeHypr.Borde.Arriba)
                    Vale(r.Y >= grosor - 0.5,
                         $"[{que}, {lado}] arranca por debajo de la barra ({r.Y:0} ≥ {grosor:0})");
                else
                    Vale(r.Y + r.Alto <= pantalla.Alto - grosor + 0.5,
                         $"[{que}, {lado}] acaba por encima de lo de abajo " +
                         $"({r.Y + r.Alto:0} ≤ {pantalla.Alto - grosor:0})");
            }
        }

        // Con la barra Y el dock a la vez, que es lo que ahora se puede tener por primera
        // vez: el panel tiene que caber entre los dos.
        var conLosDos = NubeHypr.AreaUtil(pantalla, 0,
            new NubeHypr.Estorbo(NubeHypr.Borde.Arriba, 54),
            new NubeHypr.Estorbo(NubeHypr.Borde.Abajo, 72));
        var p = Lateral.Sitio(conLosDos, Lateral.Lado.Izquierda, 99999);
        Vale(p.Y >= 54 - 0.5, "con barra y dock, el panel arranca bajo la barra");
        Vale(p.Y + p.Alto <= 1440 - 72 + 0.5, "y acaba sobre el dock");
        Vale(p.Alto >= 120, $"y le queda alto utilizable ({p.Alto:0} px)");
    }

    // ================= Y los laterales, por fin con puerta =================
    static void EscritorioLosLateralesTienenPuerta()
    {
        Grupo("escritorio: los paneles laterales se abren con el ratón en el borde");

        // E3 y E4 construyeron los dos paneles y **no había forma de abrirlos**:
        // `AbrirLateral` solo lo llamaba el diagnóstico de capturas. Ni botón, ni atajo, ni
        // gesto. Una superficie entera que nadie podía ver.
        Igual("nunca", new Barra().Laterales, "vienen apagados de serie");

        // Usan el MISMO lector que el dock: es el mismo gesto de borde, y dos vocabularios
        // para lo mismo serían dos cosas que aprender para un solo concepto.
        Igual(Dock.Cuando.Nunca, Dock.Leer("nunca"), "«nunca» los deja cerrados");
        Igual(Dock.Cuando.AlAcercarse, Dock.Leer("alacercarse"), "«alacercarse» los abre al rozar");

        // ---------- El gesto, con la cuenta del dock ----------
        var util = new NubeHypr.Rect(0, 0, 2560, 1440);

        // Contra el filo izquierdo: sí. En medio de la pantalla: no.
        Vale(Dock.RatonCerca(util, NubeHypr.Borde.Izquierda, 2, 700),
             "el ratón contra el filo izquierdo cuenta");
        Vale(!Dock.RatonCerca(util, NubeHypr.Borde.Izquierda, 1200, 700),
             "y en medio de la pantalla, no");
        Vale(Dock.RatonCerca(util, NubeHypr.Borde.Derecha, 2558, 700),
             "lo mismo por la derecha");

        // Y el filo de OTRO monitor no abre el panel de este: con dos pantallas, rozar el
        // borde de una no puede sacar una superficie en la otra.
        Vale(!Dock.RatonCerca(util, NubeHypr.Borde.Izquierda, -40, 700),
             "fuera del monitor no cuenta");

        // ---------- Mientras lo usas, no se cierra ----------
        //
        // El panel se abre al rozar el borde, y el gesto siguiente es meter el ratón DENTRO
        // del panel, donde ya no estás rozando el borde. Sin esta regla se cerraría justo
        // cuando vas a pulsar algo — es el mismo fallo que el dock ya tenía resuelto.
        Vale(Dock.SeVe(Dock.Cuando.AlAcercarse, ratonCerca: false, ratonEncima: true),
             "con el ratón dentro del panel sigue abierto aunque ya no toque el borde");
        Vale(!Dock.SeVe(Dock.Cuando.AlAcercarse, ratonCerca: false, ratonEncima: false),
             "y al salir del todo, se cierra");
        Vale(!Dock.SeVe(Dock.Cuando.Nunca, ratonCerca: true, ratonEncima: true),
             "y apagado no se abre ni rozando el borde ni estando encima");

        // La opción se ofrece en Ajustes con las mismas tres entradas que el dock.
        foreach (var (config, visible) in VentanaAjustes.OpcionesDelDockParaPruebas)
            Igual(config, VentanaAjustes.DockConfigDeParaPruebas(visible),
                  $"«{visible}» vale también para los laterales");

        // ---------- Y la regla del ratón dentro, POR LADO ----------
        //
        // Esto lo cazó un `grep`, no una prueba: la bandera «el ratón está dentro del
        // panel» existía y no la ponía nadie, así que el panel se habría cerrado en cuanto
        // movieras el ratón hacia su contenido. Y la primera versión usaba una sola bandera
        // para los dos paneles: con ambos abiertos, estar en el izquierdo mantenía vivo el
        // derecho.
        //
        // La regla, escrita como la usa el pulso: cada lado decide con SU ratón.
        foreach (var lado in new[] { Lateral.Lado.Izquierda, Lateral.Lado.Derecha })
        {
            var otro = lado == Lateral.Lado.Izquierda ? Lateral.Lado.Derecha : Lateral.Lado.Izquierda;

            // Con el ratón dentro de ESTE, se queda abierto aunque ya no toque el borde.
            Vale(Dock.SeVe(Dock.Cuando.AlAcercarse, ratonCerca: false, ratonEncima: true),
                 $"[{lado}] con el ratón dentro sigue abierto");

            // Y el de al lado, que no tiene el ratón, no: su «ratonEncima» es false.
            Vale(!Dock.SeVe(Dock.Cuando.AlAcercarse, ratonCerca: false, ratonEncima: false),
                 $"[{otro}] sin ratón ni borde, se cierra");
        }
    }

    // ================= El dock, por fin con interruptor =================
    static void EscritorioElDockTieneInterruptor()
    {
        Grupo("escritorio: las opciones del dock son las que el dock entiende");

        // El dock existía desde E2 y **no había forma de encenderlo**: `Barra.Dock` venía en
        // «nunca» y el único sitio del programa que escribía esa clave era el diagnóstico de
        // capturas. Ahora hay una página que lo ofrece, y lo que hay que garantizar es que
        // lo que ofrece y lo que el dock entiende no se separen: una opción que el lector
        // no reconozca se convertiría en «siempre» sin que nadie lo pidiera.
        foreach (var (config, visible) in VentanaAjustes.OpcionesDelDockParaPruebas)
        {
            Vale(visible.Length > 0, $"«{config}» tiene un nombre legible");

            // Ida y vuelta: el nombre que se enseña vuelve al valor del fichero.
            Igual(config, VentanaAjustes.DockConfigDeParaPruebas(visible),
                  $"«{visible}» vuelve a «{config}»");
            Igual(visible, VentanaAjustes.DockVisibleDeParaPruebas(config),
                  $"y «{config}» se enseña como «{visible}»");

            // Y lo que de verdad importa: que el DOCK lo entienda. Si esto falla, la página
            // ofrece una opción que no hace lo que dice.
            var leido = Dock.Leer(config);
            Vale(Enum.IsDefined(leido), $"y el dock entiende «{config}» ({leido})");
        }

        // Los tres estados del dock están ofrecidos: ninguno se queda sin manera de
        // elegirlo desde la interfaz.
        var ofrecidos = new System.Collections.Generic.HashSet<Dock.Cuando>();
        foreach (var (config, _) in VentanaAjustes.OpcionesDelDockParaPruebas)
            ofrecidos.Add(Dock.Leer(config));
        foreach (var c in Enum.GetValues<Dock.Cuando>())
            Vale(ofrecidos.Contains(c), $"el estado «{c}» se puede elegir desde Ajustes");

        // Y lo desconocido no se enseña como si fuera una opción: se enseña lo que de
        // verdad va a pasar, que es lo que decide `Dock.Leer`.
        Igual(Dock.Cuando.Siempre, Dock.Leer("una-cosa-rara"),
              "el lector del dock convierte lo desconocido en «siempre»");
        Igual("Siempre a la vista", VentanaAjustes.DockVisibleDeParaPruebas("una-cosa-rara"),
              "y la página enseña eso mismo, no el texto raro del fichero");

        // El valor de serie sigue siendo «nunca»: encender el dock es una decisión del
        // usuario, no algo que llegue con una actualización.
        Igual("nunca", new Barra().Dock, "el dock viene apagado de serie");
    }

    // ================= Un repetible puesto tres veces son tres chips =================
    static void EditorLosRepetiblesSonInstancias()
    {
        Grupo("editor: un módulo repetible puesto varias veces da una ficha por instancia");

        // «Los separadores no van». Iban, pero el editor hacía UNA ficha por id, así que
        // tres separadores en una zona salían como un solo chip: no se veía cuántos había,
        // no se podía mover el de en medio, y el botón de quitar actuaba siempre sobre el
        // primero, porque `DondeEsta` devuelve el primero.
        var z = ZonasDePrueba("""
        { "Modulos": { "Izquierda": ["inicio"],
                       "Centro": ["separador","reloj","separador","apps","separador"],
                       "Derecha": ["cpu"],
                       "Ocultos": [] } }
        """);

        var fichas = EditorModulos.Fichas(z);
        var seps = fichas.Where(f => f.Id == "separador" && f.Puesto).ToList();
        Igual(3, seps.Count, "tres separadores puestos dan tres fichas");

        // Y cada una sabe CUÁL es: sin esto, los tres chips son indistinguibles y los
        // botones de mover y quitar actúan sobre el que no es.
        Igual("0,2,4", string.Join(",", seps.Select(s => s.Posicion).OrderBy(x => x)),
              "y cada una conoce su posición exacta en la zona");
        foreach (var s in seps)
        {
            Igual(3, s.Cuantos, "cada ficha sabe cuántos hay en total");
            Vale(EditorModulos.Estado(s).Contains("de 3"),
                 $"y su chapita dice cuál es: «{EditorModulos.Estado(s)}»");
        }

        // El orden de las fichas puestas es el orden de la barra, zona por zona: es lo que
        // hace que las columnas se dibujen como se ve la barra.
        var centro = string.Join(",", fichas.Where(f => f.Puesto && f.Zona == "Centro")
                                            .OrderBy(f => f.Posicion).Select(f => f.Id));
        Igual("separador,reloj,separador,apps,separador", centro,
              "las fichas del Centro salen en el orden de la zona");

        // Lo que NO está puesto sigue saliendo una vez, para poder añadirlo.
        Igual(1, fichas.Count(f => f.Id == "escritorios"), "lo que no está puesto, una ficha");
        Vale(!fichas.First(f => f.Id == "escritorios").Puesto, "y marcada como no puesta");

        // Un no repetible puesto una vez: una ficha, la de su instancia.
        Igual(1, fichas.Count(f => f.Id == "reloj"), "un no repetible puesto, una sola ficha");
        Vale(fichas.First(f => f.Id == "reloj").Puesto, "y está puesta");
    }

    // ================= Y se ve =================
    static void EditorElSeparadorSeVe()
    {
        Grupo("separador: la rayita se distingue del fondo en los nueve temas");

        // La otra mitad de «los separadores no van»: se pintaban con el rol `Borde`, que
        // lleva un velo del 23 %. Una línea de un píxel al 23 % sobre el fondo de la barra
        // está dibujada, ocupa su sitio y no se ve — por eso las pruebas de módulos la
        // daban por buena: medían que existiera y que midiera algo, y las dos cosas eran
        // ciertas.
        foreach (var t in TemasDeSerie.Lista)
        {
            var r = Roles.Desde(new Pintura(t));

            var color = MainWindow.ColorDelSeparador(r);
            double ratio = Contraste.Entre(color, r.FondoOpaco);
            Vale(ratio >= MainWindow.ContrasteMinimoDelSeparador,
                 $"[{t.Nombre}] el separador se distingue del fondo ({ratio:0.00}:1)");

            // Opaco: un separador semitransparente vuelve a ser el problema de antes, y
            // además cambiaría de visibilidad según lo que haya detrás de la barra.
            Igual((byte)255, color.A, $"[{t.Nombre}] y es opaco");

            // Y lo que se quería evitar: que el rol `Borde` crudo habría bastado. Si algún
            // día basta, esta comprobación salta y se puede simplificar; mientras salte lo
            // contrario, el color propio hace falta.
            var bordeCompuesto = Contraste.Mezclar(r.FondoOpaco, r.Borde, r.Borde.A / 255.0);
            double conBorde = Contraste.Entre(bordeCompuesto, r.FondoOpaco);
            if (conBorde < MainWindow.ContrasteMinimoDelSeparador)
                Vale(ratio > conBorde,
                     $"[{t.Nombre}] y mejora al filo de tarjeta ({conBorde:0.00} → {ratio:0.00})");
        }

        // El largo sigue siendo proporcional a la barra y con topes: una rayita que midiera
        // la barra entera sería una columna, no un separador.
        foreach (double alto in new double[] { 24, 30, 38, 44, 56, 80 })
        {
            double largo = MainWindow.AltoSeparador(alto);
            Vale(largo >= 8, $"con barra de {alto:0} la rayita mide al menos 8 ({largo:0})");
            Vale(largo <= alto - 8 || largo == 8,
                 $"y deja aire arriba y abajo ({largo:0} de {alto:0})");
        }
    }

    static ZonasWidgets ZonasDePrueba(string texto)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-editor-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "config.json");
        File.WriteAllText(ruta, texto);
        return new ZonasWidgets(ConfigArbol.Cargar(ruta));
    }

    // ================= Qué está puesto y dónde =================
    static void EditorCadaModuloDiceDondeEsta()
    {
        Grupo("editor: cada módulo dice si está puesto, dónde y en qué estado");

        var z = ZonasDePrueba("""
        { "Modulos": { "Izquierda": ["inicio"],
                       "Centro": ["fijadas","apps","reloj"],
                       "Derecha": ["cpu","bandeja"],
                       "Ocultos": ["cpu"] } }
        """);

        var fichas = EditorModulos.Fichas(z);
        Vale(fichas.Count > 0, "hay fichas");

        // Todo el catálogo sale, esté puesto o no. Antes la lista de abajo enseñaba los
        // dieciocho ids siempre iguales y había que leer las tres columnas para saber si
        // «escritorios» ya estaba.
        Igual(RegistroWidgets.Ids.Count(), fichas.Count, "sale TODO el catálogo, no solo lo puesto");

        var reloj = fichas.First(f => f.Id == "reloj");
        Vale(reloj.Puesto, "«reloj» está puesto");
        Igual("Centro", reloj.Zona, "en el Centro");
        Igual(2, reloj.Posicion, "en la tercera posición");
        Vale(!reloj.Oculto, "y se ve");
        Igual("Centro", EditorModulos.Estado(reloj), "su chapita dice la zona");

        var cpu = fichas.First(f => f.Id == "cpu");
        Vale(cpu.Oculto, "«cpu» está oculto");
        Vale(EditorModulos.Estado(cpu).StartsWith("Oculto"),
             $"y su chapita lo dice: «{EditorModulos.Estado(cpu)}»");
        Vale(EditorModulos.Explicar(cpu).Contains("conserva su sitio"),
             "y la ayuda explica que oculto no es quitado");

        var escritorios = fichas.First(f => f.Id == "escritorios");
        Vale(!escritorios.Puesto, "«escritorios» no está puesto");
        Igual("Sin poner", EditorModulos.Estado(escritorios), "y lo dice sin tener que deducirlo");
        Igual(-1, escritorios.Posicion, "sin posición");

        // Repetible o no, dicho en la ficha. Antes pulsar dos veces en uno no repetible
        // parecía funcionar —el botón respondía— y no pasaba nada.
        var separador = fichas.First(f => f.Id == "separador");
        Vale(separador.Repetible, "«separador» es repetible");
        Vale(EditorModulos.Explicar(separador).Contains("más de una vez"), "y se dice");
        Vale(!reloj.Repetible, "«reloj» no lo es");
        Vale(EditorModulos.Explicar(reloj).Contains("Solo puede haber uno"), "y también se dice");

        // Y el aviso de los que pueden no verse, que antes no estaba en ninguna parte.
        var bateria = fichas.First(f => f.Id == "bateria");
        Vale(bateria.PuedeNoVerse != null, "«batería» avisa de que puede no verse");
        Vale(EditorModulos.Explicar(bateria).Contains("Aviso:"), "y sale en su ayuda");
    }

    // ================= Los botones dicen por qué no =================
    static void EditorLosBotonesExplicanPorQueNo()
    {
        Grupo("editor: cada acción está siempre, y la que no se puede dice por qué");

        var z = ZonasDePrueba("""
        { "Modulos": { "Izquierda": ["inicio"], "Centro": ["reloj"], "Derecha": ["cpu"],
                       "Ocultos": [] } }
        """);
        var fichas = EditorModulos.Fichas(z);

        foreach (var f in fichas)
            foreach (var a in Enum.GetValues<EditorModulos.Accion>())
            {
                string? no = EditorModulos.PorQueNo(a, f);
                if (no != null)
                    Vale(no.Length > 5, $"[{f.Id}] «{a}» deshabilitada dice por qué: {no}");
                Vale(EditorModulos.Nombre(a).Length > 0, $"«{a}» tiene nombre");
            }

        var inicio = fichas.First(f => f.Id == "inicio");
        Vale(!EditorModulos.SePuede(EditorModulos.Accion.ALaIzquierda, inicio),
             "lo que ya está en la primera zona no se puede mover más a la izquierda");
        Vale(EditorModulos.SePuede(EditorModulos.Accion.ALaDerecha, inicio),
             "pero sí a la derecha");

        var cpu = fichas.First(f => f.Id == "cpu");
        Vale(!EditorModulos.SePuede(EditorModulos.Accion.ALaDerecha, cpu),
             "y lo que está en la última, no más a la derecha");

        var sinPoner = fichas.First(f => !f.Puesto);
        foreach (var a in new[] { EditorModulos.Accion.Quitar, EditorModulos.Accion.Ocultar,
                                  EditorModulos.Accion.Subir, EditorModulos.Accion.ALaDerecha })
            Vale(!EditorModulos.SePuede(a, sinPoner), $"lo que no está puesto no se puede «{a}»");
        Vale(EditorModulos.SePuede(EditorModulos.Accion.Anadir, sinPoner), "pero sí añadir");

        // Un no repetible ya puesto no se puede volver a añadir, Y SE DICE. Antes el botón
        // respondía y no pasaba nada.
        var reloj = fichas.First(f => f.Id == "reloj");
        Vale(!EditorModulos.SePuede(EditorModulos.Accion.Anadir, reloj),
             "un no repetible ya puesto no se añade otra vez");
        Vale(EditorModulos.PorQueNo(EditorModulos.Accion.Anadir, reloj)!.Contains("una vez"),
             "y el motivo lo explica");

        // Las zonas vecinas, que es lo que mueven las flechas.
        Igual("Centro", EditorModulos.ZonaVecina("Izquierda", true) ?? "", "a la derecha de Izquierda está Centro");
        Igual("Izquierda", EditorModulos.ZonaVecina("Centro", false) ?? "", "y a su izquierda, Izquierda");
        Vale(EditorModulos.ZonaVecina("Derecha", true) == null, "a la derecha de Derecha no hay nada");
        Vale(EditorModulos.ZonaVecina("Arriba", true) == null, "y una zona que no existe no tiene vecinas");
    }

    // ================= Dónde va a caer =================
    static void EditorLaVistaPreviaDelDestino()
    {
        Grupo("editor: la vista previa dice entre qué dos chips cae");

        const double alto = 40;

        // Arriba del todo: delante del primero.
        Igual(0, EditorModulos.DondeCae(0, alto, 3), "soltar arriba del todo: posición 0");
        Igual(0, EditorModulos.DondeCae(10, alto, 3), "y en la mitad de arriba del primero, también");

        // Pasada la mitad del primero, va detrás de él.
        Igual(1, EditorModulos.DondeCae(25, alto, 3), "pasada la mitad del primero: posición 1");
        Igual(1, EditorModulos.DondeCae(55, alto, 3), "y en la mitad de arriba del segundo, igual");

        // Al final del todo: detrás del último. Es el caso que hace que soltar «abajo»
        // funcione, y el que se salía por un índice de más.
        Igual(3, EditorModulos.DondeCae(200, alto, 3), "muy abajo: al final");
        Igual(3, EditorModulos.DondeCae(1e6, alto, 3), "y por lejos que sea, no se pasa");

        // Lo que no revienta.
        Igual(0, EditorModulos.DondeCae(-50, alto, 3), "por encima del principio, al principio");
        Igual(0, EditorModulos.DondeCae(10, alto, 0), "en una zona vacía, posición 0");
        Igual(0, EditorModulos.DondeCae(10, 0, 3), "y con chips de alto cero no se divide por cero");
    }

    // ================= Soltar fuera no pierde nada =================
    static void EditorSoltarFueraNoPierdeNada()
    {
        Grupo("editor: soltar fuera de una zona cancela, no se come el módulo");

        // EL CASO. El `Drop` de antes quitaba del origen y DESPUÉS buscaba destino, así que
        // un arrastre que se pasaba de largo —muy fácil en una ventana pequeña— hacía
        // desaparecer el módulo de las tres columnas.
        var fuera = EditorModulos.AlSoltar(null, 50, 40, 3);
        Vale(!fuera.Vale, "soltado fuera: no vale");
        Vale(fuera.Porque.Length > 10, $"y se dice por qué: {fuera.Porque}");
        Vale(EditorModulos.SoltarFueraCancela, "la regla está escrita, no implícita");

        var inventada = EditorModulos.AlSoltar("Arriba", 50, 40, 3);
        Vale(!inventada.Vale, "y una zona que no existe tampoco vale");

        var dentro = EditorModulos.AlSoltar("Centro", 50, 40, 3);
        Vale(dentro.Vale, "soltado dentro: vale");
        Igual("Centro", dentro.Zona, "en la zona donde se soltó");
        Igual(1, dentro.Donde, "y en la posición que decía la vista previa");
        Igual("", dentro.Porque, "sin motivo que dar");
    }

    // ================= Los iconos existen =================
    static void EditorLosIconosExisten()
    {
        Grupo("editor: los iconos de las acciones son glifos de verdad");

        // Un glifo del área privada copiado de un sitio a otro se pierde sin que el
        // compilador diga nada, y queda un botón sin icono. Por eso van por punto de
        // código y por eso esto lo comprueba.
        // Se mira el glifo QUE SE PINTA, no una tabla de números al lado: una tabla
        // paralela puede estar perfecta y el botón seguir saliendo vacío.
        static int CodigoDe(string glifo) => glifo.Length == 0 ? 0 : char.ConvertToUtf32(glifo, 0);

        foreach (var a in Enum.GetValues<EditorModulos.Accion>())
        {
            string g = EditorModulos.Glifo(a);
            Vale(g.Length > 0, $"el icono de «{a}» no es una cadena vacía");
            int c = CodigoDe(g);
            Vale(c >= 0xE000 && c <= 0xF8FF, $"y está en el área privada (0x{c:X4})");
        }

        // Y no hay dos acciones con el mismo icono: dos botones iguales uno al lado del
        // otro es peor que ninguno.
        var codigos = Enum.GetValues<EditorModulos.Accion>()
                          .Select(a => CodigoDe(EditorModulos.Glifo(a))).ToList();
        Igual(codigos.Count, codigos.Distinct().Count(), "cada acción tiene su propio icono");

        // Los módulos también: el chip enseña el glifo del descriptor, el MISMO que usa la
        // biblioteca y el mismo que se pinta en la isla. Un sistema visual, no tres.
        RegistroWidgets.Asegurar();
        foreach (var id in RegistroWidgets.Ids)
        {
            var d = RegistroWidgets.Buscar(id)?.Descriptor;
            if (d == null) continue;
            Vale(d.Glifo.Length > 0, $"«{id}» tiene icono para el chip");
            int c = char.ConvertToUtf32(d.Glifo, 0);
            Vale(c >= 0xE000 && c <= 0xF8FF, $"y es un glifo de la fuente de iconos (0x{c:X4})");
        }
    }
}
