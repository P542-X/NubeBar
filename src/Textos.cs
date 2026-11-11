using System;
using System.Collections.Generic;
using System.Globalization;

namespace NubeBar;

// NubeBar en inglés, sin romper el español.
//
// ---------- La decisión que lo hace posible ----------
//
// La clave del diccionario es **la propia cadena en español**, no un identificador. O sea
// `T("Acciones rápidas")` y no `T("panel.rapidas.titulo")`.
//
// Con identificadores habría que inventar ~1900 nombres, tocar 1900 sitios y, el día que
// falte uno, el usuario ve «panel.rapidas.titulo» en pantalla. Con la cadena como clave, lo
// que no está traducido **sale en español**, que es lo que ya había: nunca peor que antes.
//
// El precio es que dos cadenas iguales en español que necesiten dos ingleses distintos
// chocan. Es un problema real pero pequeño y se ve enseguida; el otro era grande y silencioso.
//
// ---------- Y por qué se traduce en las FUNCIONES, no en las llamadas ----------
//
// Las cadenas de interfaz de este proyecto no se asignan: se pasan como argumento a un puñado
// de ayudantes —`Linea`, `Boton`, `SectionHeader`, `p.Grupo`, `p.Pista`…—. Traduciendo dentro
// de esos ayudantes se cubren cientos de sitios tocando diez funciones, y sin riesgo de
// romper una llamada por el camino.
//
// ---------- Lo que NO se traduce ----------
//
// El registro. Un log en el idioma del usuario es un log que no se puede buscar ni comparar
// entre máquinas, y el que lo lee soy yo o es él pegándomelo aquí. Se queda en español a
// propósito, igual que los identificadores del config.
public static class Textos
{
    public enum Lengua { Auto, Espanol, Ingles }

    public static Lengua Elegida { get; private set; } = Lengua.Auto;

    // Lo que de verdad se está usando, con `Auto` ya resuelto.
    public static Lengua EnUso =>
        Elegida == Lengua.Auto ? DeWindows() : Elegida;

    public static bool HayQueTraducir => EnUso == Lengua.Ingles;

    // ---------- Y las fechas también ----------
    //
    // Una fecha no pasa por la tabla: la monta .NET con una cultura. Si esa cultura es la
    // de Windows, alguien con el sistema en español y NubeBar en inglés lee «Appearance»
    // encima de «sábado, 7 de noviembre». La mitad de una frase en cada idioma se lee como
    // un fallo, y además es que no es lo que ha pedido: ha elegido el idioma del programa.
    //
    // Así que la cultura sale del idioma elegido. en-GB y no en-US porque el resto de la
    // traducción es británica («colours», «personalisation»), y porque un «7 November»
    // al lado de un «November 7» canta.
    public static CultureInfo Cultura =>
        EnUso == Lengua.Ingles ? CultureInfo.GetCultureInfo("en-GB") : CultureInfo.GetCultureInfo("es-ES");

    // El «de» de «7 de noviembre» es una palabra, no un separador: va escrita dentro del
    // formato y por eso el formato tiene que cambiar con el idioma, no solo la cultura.
    public static string FormatoFechaLarga =>
        EnUso == Lengua.Ingles ? "dddd, d MMMM" : "dddd, d 'de' MMMM";

    // «auto» pregunta a Windows. Quien tenga el sistema en español ve NubeBar en español sin
    // tocar nada; quien lo tenga en cualquier otro idioma ve inglés, que es el que entiende
    // más gente. No se inventa una tercera opción: o es español o es inglés.
    static Lengua DeWindows()
    {
        try
        {
            var dos = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return dos.Equals("es", StringComparison.OrdinalIgnoreCase)
                ? Lengua.Espanol : Lengua.Ingles;
        }
        catch { return Lengua.Espanol; }
    }

    public static void Elegir(string? valor)
    {
        var antes = Elegida;
        Elegida = (valor ?? "auto").Trim().ToLowerInvariant() switch
        {
            "es" or "espanol" or "español" => Lengua.Espanol,
            "en" or "ingles" or "inglés" or "english" => Lengua.Ingles,
            _ => Lengua.Auto,
        };
        if (Elegida != antes)
            Log.Info($"idioma: {Elegida} (config: \"{valor}\") · en uso: {EnUso}");
    }

    public static string Clave(Lengua l) => l switch
    {
        Lengua.Espanol => "es",
        Lengua.Ingles => "en",
        _ => "auto",
    };

    // ---------- La traducción ----------
    //
    // Lo que no esté sale tal cual. Es la propiedad que hace que esto se pueda ir completando
    // poco a poco sin que ninguna versión intermedia quede rota.
    public static string T(string? texto)
    {
        if (string.IsNullOrEmpty(texto) || !HayQueTraducir) return texto ?? "";
        if (En.TryGetValue(texto, out var t)) return t;
        Apuntar(texto);
        return texto;
    }

    // ---------- Lo que pasa por aquí y no está ----------
    //
    // Traducir una interfaz tiene dos huecos, y el segundo no se ve leyendo el código: una
    // cadena puede faltar en la tabla, o estar en la tabla y no pasar por `T()`. El primero
    // se puede contar con un extractor; el segundo solo sale mirando capturas, y así se
    // encontraron seis sitios que dibujaban sin traducir.
    //
    // Esto cubre el primero sin extractores: cada cadena que llega hasta aquí y no se
    // encuentra se apunta UNA vez en el registro, con prefijo. Recorrer la interfaz en
    // inglés y leer el log da la lista exacta de lo que falta, sin adivinar qué ayudante
    // la dibujó ni en qué fichero vive.
    //
    // Solo en inglés —en español no se traduce nada— y solo una vez por cadena, así que
    // no ensucia: lo que sale es un inventario, no un chorro.
    static readonly HashSet<string> _apuntadas = new(StringComparer.Ordinal);

    static void Apuntar(string texto)
    {
        lock (_apuntadas)
        {
            if (!_apuntadas.Add(texto)) return;
        }
        try { Log.Info($"[i18n] sin traducir: {texto}"); } catch { }
    }

    // Cuántas van. Para poder decir «faltan N» sin leerse el registro entero.
    public static int SinTraducir { get { lock (_apuntadas) return _apuntadas.Count; } }

    // Cuántas hay y cuántas faltan, para poder medir el avance sin contarlas a mano.
    public static int Traducidas => En.Count;

    // La tabla entera, para que una prueba pueda recorrerla. De solo lectura: lo que se
    // quiere es poder auditarla, no que nadie le añada cosas en caliente.
    public static IReadOnlyDictionary<string, string> Tabla => En;

    static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        // ---------- Los paneles de la isla y la guía de NubeHypr ----------
        //
        // Salieron del registro: `T()` apunta lo que le llega y no encuentra, así que
        // basta recorrer la interfaz en inglés y leer el log. Nada de extractores.
        ["Abrir"] = "Open",
        ["Abrir el ahorro de batería en Configuración"] = "Open battery saver in Settings",
        ["Abrir Energía y batería en Configuración"] = "Open Power & battery in Settings",
        ["Abrir la ventana de ajustes"] = "Open the settings window",
        ["Ajustes de Windows"] = "Windows settings",
        ["Modo de energía"] = "Power mode",
        ["Eficiencia"] = "Efficiency",
        ["Rendimiento"] = "Performance",
        ["Pasar a eficiencia"] = "Switch to efficiency",
        ["Pasar a rendimiento"] = "Switch to performance",
        ["Equilibrado (puesto)"] = "Balanced (current)",
        ["Sin batería"] = "No battery",
        ["Sonido"] = "Sound",
        ["Volumen y sonido"] = "Volume and sound",
        ["Salida de audio"] = "Audio output",
        ["Por aplicación"] = "Per application",
        ["Brillo"] = "Brightness",
        ["Se aplica al soltar: el monitor no admite una escritura por fotograma."] = "Applied when you let go: the monitor can't take one write per frame.",
        ["Red e Internet"] = "Network and internet",
        ["Redes Wi-Fi"] = "Wi-Fi networks",
        ["Redes wifi"] = "Wi-Fi networks",
        ["Conectar"] = "Connect",
        ["Conectar (pide contraseña)"] = "Connect (asks for a password)",
        ["Desconectar"] = "Disconnect",
        ["Actualizar"] = "Refresh",
        ["Velocidad"] = "Speed",
        ["Medir velocidad"] = "Measure speed",
        ["Bajada"] = "Download",
        ["Subida"] = "Upload",
        ["Pidiéndole a Windows el estado de la red."] = "Asking Windows for the network status.",
        ["Monitores: cambiar la entrada de vídeo"] = "Monitors: switch the video input",
        ["Bloquear"] = "Lock",
        ["Suspender"] = "Sleep",
        ["Reiniciar"] = "Restart",
        ["Apagar"] = "Shut down",
        ["Cerrar"] = "Close",
        ["Cerrar esta ventana"] = "Close this window",
        ["Encender"] = "Turn on",
        ["Editar"] = "Edit",
        ["Anterior"] = "Previous",
        ["Siguiente"] = "Next",
        ["Buscar"] = "Search",
        ["Libre"] = "Free",
        ["Disco"] = "Disk",
        ["Quitar todas las notificaciones del centro"] = "Clear every notification from the centre",
        ["No molestar: activado. Se siguen guardando, no se asoman"] = "Do Not Disturb: on. They are still stored, they just don't pop up",
        ["No molestar: Silenciado. Encendido, los avisos de Windows se guardan en el centro y no aparecen en pantalla."] = "Do Not Disturb: Muted. When on, Windows notices are stored in the centre and don't appear on screen.",
        ["Modo nocturno: Normal. Los colores de la pantalla tiran a cálido por la noche. Lo gestiona Windows."] = "Night mode: Normal. The screen colours warm up at night. Windows handles it.",
        ["Tema claro: Claro. Cambia entre el tema claro y el oscuro de NubeBar."] = "Light theme: Light. Switches between NubeBar's light and dark themes.",
        ["Tema claro: Oscuro. Cambia entre el tema claro y el oscuro de NubeBar."] = "Light theme: Dark. Switches between NubeBar's light and dark themes.",
        ["Wallpaper Engine: Integrado. NubeBar toma los colores del fondo animado y le manda el acento."] = "Wallpaper Engine: Integrated. NubeBar takes the colours from the animated background and sends it the accent.",
        ["Modo de demostración: ningún cambio se guarda"] = "Demo mode: no change is saved",
        ["Mes anterior"] = "Previous month",
        ["Mes siguiente"] = "Next month",
        ["Espacio"] = "Space",
        ["espacio"] = "space",
        ["Todos los monitores"] = "Every monitor",
        ["Los otros 2 están vacíos."] = "The other 2 are empty.",
        ["Acento"] = "Accent",
        ["Acento sobre fondo (texto)"] = "Accent over background (text)",
        ["Texto encima del acento"] = "Text on top of the accent",
        ["Texto principal"] = "Main text",
        ["Texto secundario"] = "Secondary text",
        ["Secundario"] = "Secondary",
        ["Segundo secundario"] = "Second secondary",
        ["Aviso"] = "Warning",
        ["Halo"] = "Glow",
        ["Sombra"] = "Shadow",
        ["Destello del filo"] = "Edge flash",
        ["Filo de la isla"] = "Island edge",
        ["Filo de las tarjetas"] = "Card edge",
        ["Filo de los paneles"] = "Panel edge",
        ["Fondo de la isla"] = "Island background",
        ["Fondo de las tarjetas"] = "Card background",
        ["Fondo de los paneles"] = "Panel background",
        ["Fondo de escritorio del tema"] = "Theme desktop background",
        ["Color sólido"] = "Solid colour",
        ["degradado"] = "gradient",
        ["Un degradado. Se edita en el Estudio de temas."] = "A gradient. It is edited in the theme Studio.",
        ["Nombre del tema"] = "Theme name",
        ["Buscar un color"] = "Find a colour",
        ["Busca «reloj», «apagar», «filo»… por nombre o por clave"] = "Search for «clock», «shut down», «edge»… by name or by key",
        ["No has cambiado ningún color: todo lo pone el tema."] = "You haven't changed any colour: the theme sets them all.",
        ["Todavía no has guardado ninguno."] = "You haven't saved any yet.",
        ["Tema, estilo visual y halo del neón"] = "Theme, visual style and neon glow",
        ["El tema puesto pide «Segoe UI Variable». Seis de los nueve usan letras que todavía no están incrustadas en el programa: hasta las fases 6 y 7 se ven con la del sistema."] = "The current theme asks for «Segoe UI Variable». Six of the nine use typefaces not yet embedded in the program: until phases 6 and 7 they are shown with the system one.",
        ["Anime"] = "Anime",
        ["Juego"] = "Gaming",
        ["Sobrios"] = "Understated",
        ["Estaciones"] = "Seasons",
        ["Alto"] = "Height",
        ["Ancho"] = "Width",
        ["Más ancho"] = "Wider",
        ["Más estrecho"] = "Narrower",
        ["Más iconos"] = "More icons",
        ["arriba"] = "top",
        ["Al acercar el ratón al borde"] = "When the mouse gets close to the edge",
        ["siempre"] = "always",
        ["Nunca"] = "Never",
        ["Esperar al fondo (segundos)"] = "Wait for the background (seconds)",
        ["activa"] = "active",
        ["Activa"] = "Active",
        ["apagada"] = "off",
        ["puesto"] = "current",
        ["Estado correcto"] = "All good",
        ["Acción destructiva"] = "Destructive action",
        ["A qué ventanas se aplica"] = "Which windows it applies to",
        ["Esto es lo que hará"] = "This is what it will do",
        ["Proceso"] = "Process",
        ["Clase de ventana"] = "Window class",
        ["Título contiene"] = "Title contains",
        ["Ruta contiene"] = "Path contains",
        ["Prioridad"] = "Priority",
        ["Flotante"] = "Floating",
        ["Principal"] = "Main",
        ["Layout"] = "Layout",
        ["layout"] = "layout",
        ["Columnas"] = "Columns",
        ["Filas"] = "Rows",
        ["foco"] = "focus",
        ["mover"] = "move",
        ["deshacer"] = "undo",
        ["ventana"] = "window",
        ["tamano"] = "size",
        ["Darle el foco"] = "Give it focus",
        ["Definir el tamaño inicial"] = "Set the initial size",
        ["Elegir el monitor"] = "Choose the monitor",
        ["Elegir el layout: principal, horizontal o vertical"] = "Choose the layout: main, horizontal or vertical",
        ["Enviar una aplicación a un escritorio"] = "Send an application to a desktop",
        ["Que una aplicación salga flotante"] = "Have an application come out floating",
        ["Combinar varias cosas en una regla"] = "Combine several things in one rule",
        ["Aplicar la regla solo a UNA ventana concreta, por el título"] = "Apply the rule to just ONE particular window, by its title",
        ["Qué hacer con ellas"] = "What to do with them",
        ["Qué pasa si dos reglas coinciden"] = "What happens if two rules match",
        ["Todavía no hay ninguna regla"] = "There are no rules yet",
        ["Reglas, atajos y estado de NubeHypr"] = "NubeHypr rules, shortcuts and state",
        ["NubeHypr está apagado: las reglas no se evalúan."] = "NubeHypr is off: the rules are not evaluated.",
        ["NubeHypr está apagado: esto es solo un cálculo. No se evalúa ni se mueve nada."] = "NubeHypr is off: this is only a calculation. Nothing is evaluated and nothing moves.",
        ["No se evalúa nada: ni reglas ni atajos."] = "Nothing is evaluated: no rules, no shortcuts.",
        ["Está apagada: se guarda, pero no se evalúa hasta que la enciendas."] = "It is off: it is saved, but not evaluated until you turn it on.",
        ["0 es el principal"] = "0 is the main one",
        ["más alta, manda antes"] = "higher wins first",
        ["el escritorio virtual: 1, 2, 3…"] = "the virtual desktop: 1, 2, 3…",
        ["el nombre del programa, sin «.exe»"] = "the program's name, without «.exe»",
        ["un trozo de la ruta del ejecutable"] = "part of the executable's path",
        ["un trozo del título; no distingue acentos ni mayúsculas"] = "part of the title; it ignores accents and capitals",
        ["para distinguir una ventana de un diálogo del mismo programa"] = "to tell a window apart from a dialog of the same program",
        ["fracción de la pantalla: 0,5 es la mitad"] = "a fraction of the screen: 0.5 is half",
        ["fracción de la pantalla: 0,6"] = "a fraction of the screen: 0.6",
        ["fuera del reparto, donde Windows la ponga"] = "out of the layout, wherever Windows puts it",
        ["traerla delante al aparecer"] = "bring it to the front when it appears",
        ["cómo se reparte la pantalla con las demás"] = "how the screen is shared with the others",
        ["Cómo se reparte la pantalla entre las ventanas que NO son flotantes."] = "How the screen is shared among the windows that are NOT floating.",
        ["Las reglas no se pisan entre sí: se COMBINAN campo a campo."] = "Rules don't override one another: they COMBINE field by field.",
        ["Lo que dejes en blanco no se toca: otra regla puede decidirlo, y si nadie lo dice, lo deja Windows."] = "Whatever you leave blank is untouched: another rule may decide it, and if nobody does, Windows does.",
        ["Todo lo que pongas tiene que cumplirse a la vez. Lo que dejes en blanco no se mira."] = "Everything you set has to be true at once. Whatever you leave blank isn't checked.",
        ["Puedes poner solo el ancho o solo el alto. Lo que dejes en blanco se queda como estuviera."] = "You can set only the width or only the height. Whatever you leave blank stays as it was.",
        ["El tamaño va en fracción de la pantalla, no en píxeles: 0,5 es la mitad. Así la misma regla vale en un portátil y en un monitor de 4K."] = "The size goes as a fraction of the screen, not in pixels: 0.5 is half. That way the same rule works on a laptop and on a 4K monitor.",
        ["El mismo programa puede tener veinte ventanas. El título distingue una: así la regla toca la pestaña de WhatsApp del navegador y no todo el navegador."] = "The same program can have twenty windows. The title singles one out: that way the rule touches the browser's WhatsApp tab and not the whole browser.",
        ["El título se compara por «contiene», sin distinguir mayúsculas ni acentos. Ojo: muchos programas cambian el título solo (el nombre del documento abierto), así que elige un trozo que no cambie."] = "The title is matched by «contains», ignoring capitals and accents. Careful: many programs change their own title (the name of the open document), so pick a part that doesn't change.",
        ["Los nombres engañan y conviene saberlo: «horizontal» reparte en COLUMNAS, de izquierda a derecha, y «vertical» en FILAS, de arriba abajo. El nombre es la dirección en que se recorre la pantalla, no la forma de las ventanas."] = "The names are misleading and it's worth knowing: «horizontal» lays out in COLUMNS, left to right, and «vertical» in ROWS, top to bottom. The name is the direction the screen is travelled in, not the shape of the windows.",
        ["Una regla dice qué hacer con una ventana cuando aparece: a qué espacio va, en qué monitor, si flota o qué tamaño toma."] = "A rule says what to do with a window when it appears: which space it goes to, on which monitor, whether it floats, and what size it takes.",
        ["Una calculadora o un reproductor pequeño no tienen por qué entrar en el reparto de la pantalla. Flotante significa: déjala donde Windows la ponga y no la coloques con las demás."] = "A calculator or a small player has no business being in the screen layout. Floating means: leave it where Windows puts it and don't place it with the rest.",
        ["Lo más pedido: que el chat viva siempre en su escritorio y no se mezcle con el trabajo. Basta con el nombre del proceso y el número del escritorio."] = "The most asked for: having the chat always live on its own desktop and not get mixed in with work. The process name and the desktop number are enough.",
        ["Con dos pantallas, que el correo se abra siempre en la de al lado. Los monitores se cuentan desde 0, y el 0 es el principal."] = "With two screens, having mail always open on the one next door. Monitors are counted from 0, and 0 is the main one.",
        ["Discord en el Escritorio 2 Y flotante. No hacen falta dos reglas: una misma regla puede rellenar todos los campos que quieras."] = "Discord on Desktop 2 AND floating. You don't need two rules: a single rule can fill in as many fields as you like.",
        ["También funcionaría con dos reglas separadas, una por campo: NubeHypr las combina. Una sola es más fácil de leer; dos permiten apagar una mitad."] = "It would also work as two separate rules, one per field: NubeHypr combines them. One is easier to read; two let you switch off one half.",
        ["Esta no es una regla para usar, es la que explica el resto. Pruébala junto a la de Discord de arriba: las dos coinciden con Discord y cada una rellena un campo distinto, así que se suman en vez de pisarse."] = "This isn't a rule to use, it's the one that explains the rest. Try it alongside the Discord one above: both match Discord and each fills a different field, so they add up instead of overriding each other.",
        ["Si desconectas ese monitor, la regla deja de tener dónde poner la ventana y no se aplica. No se pierde: vuelve a funcionar al reconectarlo."] = "If you unplug that monitor, the rule has nowhere to put the window and doesn't apply. It isn't lost: it works again when you plug it back in.",
        ["No coincide con ninguna ventana abierta ahora mismo. Puede estar bien —quizá esa aplicación no está abierta— o puede que el nombre del proceso no sea el que has escrito."] = "It doesn't match any open window right now. That may be fine — perhaps that application isn't open — or the process name may not be the one you typed.",

        // ---------- El saludo del menú Inicio ----------
        ["Buenos días"] = "Good morning",
        ["Buenas tardes"] = "Good afternoon",
        ["Buenas noches"] = "Good evening",
        ["No hay nada sonando"] = "Nothing is playing",
        ["Pon música y aparecerá aquí con sus controles"] =
            "Put some music on and it will show up here with its controls",
        ["Teclado"] = "Keyboard",

        // ---------- Ajustes: descripciones de grupo y valores de desplegable ----------
        //
        // Un VALOR de desplegable («isla», «barra», «auto») es lo que se guarda en el
        // config.json, así que la lista no se traduce: se traduce lo que se DIBUJA, con
        // la plantilla de `Desplegable`, y por debajo el elemento sigue siendo este de
        // aquí. Traducir la lista escribiría «island» en el fichero y la siguiente
        // carga no lo entendería.
        ["A qué pantalla se refiere todo lo de abajo"] = "Which screen everything below refers to",
        ["Cada color de cada elemento. Lo que no cambies lo pone el tema."] = "Every colour of every element. Whatever you don't change, the theme sets.",
        ["Cada tarjeta es un perfil: lo que cambia y qué puedes hacer con él"] = "Each card is a profile: what it changes and what you can do with it",
        ["Cuánto se redondean las tarjetas y los paneles. Lo pone el tema; aquí se afina."] = "How much the cards and panels are rounded. The theme sets it; here you fine-tune it.",
        ["De qué pantalla se leen los colores del wallpaper"] = "Which screen the wallpaper colours are read from",
        ["El monitor de destino, qué hacer al salir y cuánto espera la nube"] = "The target monitor, what to do on exit, and how long the cloud waits",
        ["El wallpaper o la playlist que NubeBar le pedirá"] = "The wallpaper or playlist NubeBar will ask it for",
        ["Estos botones son de esta página: el fondo se pone al momento"] = "These buttons belong to this page: the background is applied straight away",
        ["Guárdate lo que has afinado con un nombre y vuelve a él cuando quieras."] = "Save what you have tuned under a name and come back to it whenever you like.",
        ["La capa de organización nace apagada. Estos atajos solo se registran cuando la enciendes."] = "The arrangement layer ships off. These shortcuts are only registered once you turn it on.",
        ["La decisión de cada ventana abierta, con la regla que pone cada campo y el motivo por el que las demás se descartan."] = "The decision for every open window, with the rule that sets each field and the reason the rest are discarded.",
        ["Lo que se va a ver en el escritorio"] = "What you will see on the desktop",
        ["Los 19 comandos. El editor completo está en la página de Atajos."] = "All 19 commands. The full editor is on the Shortcuts page.",
        ["Los controles propios del fondo elegido"] = "The controls belonging to the chosen background",
        ["Mueve cualquier sección de un panel al otro, o súbela y bájala dentro del suyo. El orden que dejes aquí es el que verás al abrirlos."] = "Move any section from one panel to the other, or up and down within its own. The order you leave here is the one you will see when you open them.",
        ["NubeHypr organiza ventanas. De serie está apagado, y encendido empieza en simulación."] = "NubeHypr arranges windows. It ships off, and when turned on it starts in simulation.",
        ["Nueve temas completos: colores, forma, letra y decoraciones."] = "Nine complete themes: colours, shape, typeface and decorations.",
        ["Ocho ejemplos con aplicaciones de verdad. Cada uno dice qué campos se rellenan y qué hace, y se puede poner en el formulario de un clic."] = "Eight examples using real applications. Each one says which fields get filled in and what it does, and goes into the form with one click.",
        ["Prueba con lo que hay escrito ahora, sin cerrar los ajustes"] = "Try it with what is written right now, without closing the settings",
        ["Qué clase de fondo pone NubeBar"] = "What kind of background NubeBar sets",
        ["Qué hacer con una ventana según su proceso, su clase, su título o su ruta."] = "What to do with a window according to its process, its class, its title or its path.",
        ["Rellenar recorta; ajustar deja la imagen entera. Ninguno deforma."] = "Fill crops; fit leaves the whole image. Neither of them distorts it.",
        ["Varias ventanas del mismo programa, en un solo icono"] = "Several windows of the same program, in a single icon",
        ["Izquierda"] = "Left",
        ["Centro"] = "Centre",
        ["Derecha"] = "Right",
        ["Español"] = "Spanish",
        ["English"] = "English",
        ["No opina"] = "No opinion",
        ["isla"] = "island",
        ["barra"] = "bar",
        // «notch» se queda igual: es la palabra que se usa en inglés y la que la gente busca.
        // No se traduce como «muesca» porque nadie llamaría a esto «notch bar» en español y en
        // inglés no hay otra forma de decirlo.
        ["notch"] = "notch",
        ["auto"] = "auto",
        ["completo"] = "full",
        ["equilibrado"] = "balanced",
        ["reducido"] = "reduced",
        ["desactivado"] = "off",
        ["primera-vez"] = "first time",
        ["cada-inicio"] = "every start",
        ["al-cambiar-tema"] = "when the theme changes",
        ["nunca"] = "never",
        ["todos"] = "all",
        ["principal"] = "main",
        ["nada"] = "nothing",
        ["cerrar"] = "close",
        ["pausar"] = "pause",

        // ---------- Ajustes: el marco y la barra lateral ----------
        //
        // Lo que sale en TODAS las páginas: el título, el pie, los trece nombres de
        // sección y sus subtítulos. No lo veía el extractor porque no pasa por los
        // ayudantes de construir controles, sino por el registro de secciones y por
        // el marco de la ventana. Lo vio una captura.
        ["Los cambios se guardan al pulsar Guardar"] = "Changes are saved when you press Save",
        ["Personalización"] = "Personalisation",
        ["Personaliza el aspecto de tu escritorio"] = "Customise the look of your desktop",
        ["Controla ventanas, atajos y comportamiento"] = "Control windows, shortcuts and behaviour",
        ["El tema, la forma y cuánto se mueve todo"] = "The theme, the shape, and how much everything moves",
        ["Los colores del tema y sus roles"] = "The theme's colours and their roles",
        ["El fondo del escritorio, por tipo y por monitor"] = "The desktop background, by type and by monitor",
        ["Wallpaper Engine y su integración con la escena de inicio"] = "Wallpaper Engine and how it ties into the startup scene",
        ["Configura tu perfil actual"] = "Configure your current profile",
        ["Qué widgets hay en cada zona de la nube"] = "Which widgets live in each zone of the cloud",
        ["Añade, oculta o quita widgets"] = "Add, hide or remove widgets",
        ["El carrusel de aplicaciones y las fijadas"] = "The application carousel and the pinned ones",
        ["El dock y los paneles laterales"] = "The dock and the side panels",
        ["Las combinaciones de teclado"] = "The keyboard combinations",
        ["Organizar ventanas: reglas, atajos y estado"] = "Arranging windows: rules, shortcuts and state",
        ["El menú Inicio propio y su buscador"] = "Its own Start menu and its search",
        ["Arranque, notificaciones y diagnóstico"] = "Startup, notifications and diagnostics",
        ["Suave"] = "Soft",

        // ---------- Ajustes: las pistas largas ----------
        //
        // Los parrafos que explican cada ajuste. Van enteros y no por trozos: `T()`
        // recibe la cadena ya concatenada, asi que la clave es la suma de los
        // literales del codigo. Traducir un trozo suelto no casa con nada, y el
        // fallo seria silencioso: saldria en castellano sin que nadie sepa por que.
        ["Al borde izquierdo o derecho, media pantalla; a una esquina, un cuarto; arriba, maximizar. Antes de soltar se ve dónde va a caer. Respeta el área de trabajo, así que con «Reservar espacio» puesto no se mete debajo de la nube."] =
            "To the left or right edge, half the screen; to a corner, a quarter; to the top, maximised. Before you let go you can see where it will land. It respects the work area, so with «Reserve space» on it won't slide under the cloud.",
        ["Apagado —como viene de serie— la nube FLOTA: las ventanas pasan por debajo de ella y una ventana maximizada la tapa. Encendido, Windows le guarda su franja igual que a la barra de tareas, y las ventanas maximizadas llegan solo hasta donde empieza la nube."] =
            "Off — the way it ships — the cloud FLOATS: windows pass underneath it and a maximised window covers it. On, Windows keeps its strip for it just as it does for the taskbar, and maximised windows only reach the point where the cloud starts.",
        ["Apagado —como viene— NubeHypr calcula lo que haría y lo apunta en el registro sin tocar ninguna ventana. Encendido, las mueve. Las reglas y los atajos son los mismos en los dos casos: lo único que cambia es si se ejecutan. Si el modo seguro entra por su cuenta, manda él y no se mueve nada."] =
            "Off — the way it ships — NubeHypr works out what it would do and writes it to the log without touching a single window. On, it moves them. The rules and the shortcuts are the same either way: the only difference is whether they run. If safe mode comes on by itself, safe mode wins and nothing moves.",
        ["Busca en el id, el proceso, la clase, el título, la ruta y también en lo que la regla hace: «espacio 2», «flotante», «principal»."] =
            "Searches the id, the process, the class, the title, the path, and also what the rule does: «space 2», «floating», «main».",
        ["Con el tema puesto no se lee el texto de esta ventana, así que Ajustes y el Estudio se pintan con Profesional. Mira los colores de la zona «La ventana de Ajustes», más abajo, o vuelve al tema de serie."] =
            "With this theme on, the text in this window can't be read, so Settings and the Studio are painted with Profesional. Look at the colours in the «The Settings window» group further down, or go back to a built-in theme.",
        ["Con esto encendido, el fondo, el texto y el acento salen de tu wallpaper y mandan sobre el tema y sobre lo que hayas cambiado a mano."] =
            "With this on, the background, the text and the accent come from your wallpaper, and they override the theme and anything you changed by hand.",
        ["Con esto puesto, NubeBar puede abrir Wallpaper Engine al arrancar, cargar el wallpaper que elijas y esperar a que esté listo antes de que la nube se abra: así la escena de inicio va descubriendo el escritorio con el fondo ya puesto. Está desactivada de serie porque significa abrir y dar órdenes a un programa que no es NubeBar."] =
            "With this on, NubeBar can open Wallpaper Engine at startup, load the wallpaper you choose and wait until it is ready before the cloud opens: that way the startup scene uncovers a desktop that already has its background on. It ships off, because it means opening and giving orders to a program that isn't NubeBar.",
        ["Con las animaciones reducidas o desactivadas el halo se queda quieto por su cuenta, aunque la respiración esté encendida."] =
            "With animations reduced or switched off, the glow stays still on its own, even if breathing is on.",
        ["Dos listas vacías significan «el reparto de serie», no «paneles vacíos»: por eso volver atrás es seguro y no te deja sin nada."] =
            "Two empty lists mean «the default layout», not «empty panels»: that's why going back is safe and won't leave you with nothing.",
        ["El halo solo se ve con el estilo «Neón» puesto. Apagar la respiración lo deja fijo y visible, no lo quita — para no tener halo se elige otro estilo."] =
            "The glow is only visible with the «Neón» style on. Turning breathing off leaves it fixed and visible, it doesn't remove it — to have no glow, pick another style.",
        ["En «izquierda» y «derecha», el modo barra se vuelve una barra vertical con las zonas apiladas; el modo isla solo se ancla a ese lado."] =
            "On «left» and «right», bar mode becomes a vertical bar with the zones stacked; island mode only anchors to that side.",
        ["Esconderse también con una ventana maximizada (juegos, vídeos)"] =
            "Hide for a merely maximised window too (games, videos)",
        ["Escribe tu ciudad: «Madrid», «A Coruña», «Berlín». NubeBar la busca una vez, se guarda las coordenadas y a partir de ahí solo pide la temperatura, cada quince minutos. Los datos son de Open-Meteo, que es gratuito y no pide registro: sale de tu equipo esa coordenada y nada más — ni tu nombre, ni tu IP a un localizador, ni ningún identificador. Con el interruptor apagado, o sin ciudad escrita, NubeBar no hace ninguna petición."] =
            "Type your city: «Madrid», «A Coruña», «Berlin». NubeBar looks it up once, keeps the coordinates and from then on only asks for the temperature, every fifteen minutes. The data comes from Open-Meteo, which is free and needs no account: what leaves your machine is that coordinate and nothing else — not your name, not your IP to a locator, not any identifier. With the switch off, or with no city typed, NubeBar makes no request at all.",
        ["Hay un perfil aplicado hace poco. Puedes volver a como estaba antes."] =
            "A profile was applied recently. You can go back to how things were before.",
        ["La propuesta ha caducado: vuelve a pedirla y te la calculo con las ventanas de ahora."] =
            "The proposal has expired: ask for it again and it will be worked out with the windows you have now.",
        ["La regla está apagada; la prueba la ha evaluado como si estuviera encendida, para que puedas verla funcionar antes de activarla."] =
            "The rule is switched off; the test evaluated it as if it were on, so you can watch it work before you enable it.",
        ["La traducción al inglés cubre la interfaz; lo que falte sale en español en vez de salir en blanco. El registro se queda siempre en español a propósito: un log traducido no se puede buscar ni comparar entre máquinas."] =
            "The English translation covers the interface; anything missing comes out in Spanish instead of coming out blank. The log stays in Spanish on purpose: a translated log can't be searched or compared across machines.",
        ["Las teclas de volumen enseñan el aviso de la isla, no el de Windows"] =
            "The volume keys show the island's overlay, not Windows'",
        ["Lo que la nube espera, como mucho, antes de abrirse. Si se agota, la escena se abre igual con el escritorio que haya: la pantalla no se queda en negro."] =
            "How long the cloud waits, at most, before opening. If it runs out, the scene opens anyway with whatever desktop is there: the screen is never left black.",
        ["Lo que no pongas aquí lo hereda del fondo de todos los monitores. «Usar este fondo en todos los monitores» borra lo propio de esta pantalla."] =
            "Whatever you leave unset here is inherited from the background shared by every monitor. «Use this background on every monitor» clears what is specific to this screen.",
        ["Los cambios se ven en cuanto sueltas: los paneles se vuelven a montar con lo que acabas de poner."] =
            "The changes show the moment you let go: the panels are rebuilt with what you have just set.",
        ["Los perfiles se guardan en %APPDATA%\\NubeBar\\perfiles, uno por fichero y con su versión. Un perfil de una versión más nueva se rechaza en vez de leerse a medias."] =
            "Profiles are stored in %APPDATA%\\NubeBar\\perfiles, one per file and with its version. A profile from a newer version is rejected rather than read halfway.",
        ["Los píxeles que NubeHypr deja entre una ventana y otra al repartirlas, y entre las ventanas y el filo de la pantalla. A 0 quedan pegadas. Se aplica al próximo reparto, no recoloca lo que ya está puesto."] =
            "The pixels NubeHypr leaves between one window and the next when it lays them out, and between the windows and the edge of the screen. At 0 they sit flush. It applies to the next layout, it doesn't rearrange what is already placed.",
        ["Mientras el modo sea «simulación», encenderlo no mueve ninguna ventana: solo deja que las reglas decidan y lo apunten en el registro."] =
            "While the mode is «simulation», turning this on moves no window: it only lets the rules decide and write it to the log.",
        ["No hay perfiles. Importa uno o duplica alguno de los de serie."] =
            "There are no profiles. Import one or duplicate one of the built-in ones.",
        ["Para reasignarlos, ver cuáles están cogidos por otra aplicación y guardarlos, ve a Sistema → Atajos."] =
            "To reassign them, see which ones are taken by another application and save them, go to System → Shortcuts.",
        ["Puedes usar los ocho ejemplos, escribir reglas y probarlas. Nada de esto toca tu config.json: ni se guarda, ni se migra el esquema, ni se registran atajos globales, ni se mueve ninguna ventana. NubeHypr está apagado en memoria aunque tu fichero lo tenga encendido.\n\nAl cerrar esta ventana no queda rastro de lo que hayas hecho aquí."] =
            "You can use the eight examples, write rules and test them. None of this touches your config.json: nothing is saved, the schema isn't migrated, no global shortcuts are registered, and no window is moved. NubeHypr is off in memory even if your file has it on.\n\nWhen you close this window, nothing you did here is left behind.",
        ["Se calcula cuando lo pides, no en bucle: resolver las reglas de todas las ventanas cada segundo sería trabajo para no enseñar nada nuevo."] =
            "It is worked out when you ask for it, not in a loop: resolving the rules for every window once a second would be work done to show nothing new.",
        ["Sin reglas no hay nada que decidir: todas las ventanas se quedan donde las ponga Windows."] =
            "With no rules there is nothing to decide: every window stays where Windows puts it.",
        ["Solo para lo que NubeHypr hace por su cuenta: una regla que se dispara al abrir una ventana, o un reparto al cambiar de layout. Un atajo que acabas de pulsar no pregunta, porque pulsarlo ya es la respuesta. La pregunta sale en la isla con «Mover» y «Cancelar», y si no contestas no se mueve nada."] =
            "Only for what NubeHypr does on its own: a rule that fires when a window opens, or a layout applied when you switch. A shortcut you just pressed doesn't ask, because pressing it is the answer. The question appears in the island with «Move» and «Cancel», and if you don't answer, nothing moves.",
        ["Solo se aplica a lo que haya abierto NubeBar. Si Wallpaper Engine ya estaba abierto cuando arrancaste, no se toca al salir: no es nuestro."] =
            "It only applies to what NubeBar opened. If Wallpaper Engine was already running when you started, it is left alone on the way out: it isn't ours.",
        ["Un perfil guarda el tema, el nivel de animación, el panel, los widgets, las notificaciones, el No molestar y el fondo. Aplicarlo lo cambia todo de una vez y con una sola escritura; lo que el perfil no menciona se queda como lo tengas."] =
            "A profile stores the theme, the animation level, the panel, the widgets, the notifications, Do Not Disturb and the background. Applying it changes everything at once and in a single write; whatever the profile doesn't mention is left as you have it.",
        ["Un tema tuyo lleva el tema de partida, los colores que le has cambiado, los radios y las letras. Es un JSON legible en la carpeta de temas, así que se puede pasar a otro ordenador copiándolo. El formato empaquetado —con las fuentes y las imágenes dentro— llega con el Estudio de temas."] =
            "A theme of yours carries the theme it started from, the colours you changed, the radii and the typefaces. It is readable JSON in the themes folder, so you can move it to another computer by copying it. The packaged format — with the fonts and the images inside — arrives with the theme Studio.",
        ["Una nubecita aparece en el centro, saluda y se abre: por dentro de ella va apareciendo el escritorio ya montado. Al final se recoge y se mete en la barra. Se salta con Esc o con un clic, y con el movimiento desactivado no aparece."] =
            "A little cloud appears in the middle, says hello and opens: the already-built desktop shows up inside it. At the end it folds away into the bar. Skip it with Esc or a click, and with movement switched off it doesn't appear at all.",
        ["Una sección que llegue en una actualización futura aparece sola al final de su panel, no se queda invisible porque tú ya tuvieras un orden guardado."] =
            "A section that arrives in a future update shows up on its own at the end of its panel; it isn't left invisible just because you already had an order saved.",
        ["Vacío usa el color de acento del tema. Un color (#7AA2F7) manda sobre él. Dos separados por coma (#7AA2F7,#C77DFF) dan degradado: el primero es el filo y el segundo la luz. Lo que no se entienda se ignora y vuelve al color del tema."] =
            "Empty uses the theme's accent colour. One colour (#7AA2F7) overrides it. Two separated by a comma (#7AA2F7,#C77DFF) give a gradient: the first is the edge and the second the light. Anything that can't be understood is ignored and falls back to the theme colour.",
        ["Vale en los dos modos. Una isla estrecha reserva la franja entera de su borde, porque Windows solo sabe guardar bordes de pantalla y no trozos. Con la visibilidad en «ocultar automáticamente» no se reserva nada, aunque esto esté encendido: guardar sitio para algo que no está es justo lo que se quiere evitar."] =
            "It works in both modes. A narrow island reserves the whole strip along its edge, because Windows only knows how to keep screen edges, not pieces of them. With visibility set to «auto-hide» nothing is reserved even with this on: keeping room for something that isn't there is exactly what you were trying to avoid.",
        ["Vienen apagados. Encendidos, dibujan una rayita fina entre módulos de distinto tipo: por ejemplo entre el reloj y el grupo de CPU, memoria y bandeja. No son módulos, y por eso no salen en las columnas de aquí abajo. Apagados, los grupos se distinguen por el hueco. El módulo «Separador» —el que colocas tú— es otra cosa y no se toca."] =
            "They ship off. On, they draw a thin line between modules of different kinds: between the clock and the CPU, memory and tray group, for instance. They are not modules, which is why they don't appear in the columns below. Off, the groups are told apart by the gap. The «Separator» module — the one you place yourself — is a different thing and isn't touched.",
        ["«Automático» pregunta a Windows: con el sistema en español verás NubeBar en español, y en cualquier otro idioma lo verás en inglés."] =
            "«Automatic» asks Windows: with the system in Spanish you'll see NubeBar in Spanish, and in any other language you'll see it in English.",
        ["«Usar este ejemplo» solo rellena el formulario: la regla nace apagada y no se guarda hasta que pulses Guardar. Nada de esto mueve ventanas."] =
            "«Use this example» only fills in the form: the rule is born switched off and isn't saved until you press Save. None of this moves windows.",
        ["¿Prefieres que la nube ocupe su sitio en pantalla, como la barra de tareas, en vez de flotar por encima de las ventanas? Eso es «Reservar espacio en pantalla», y está en Apariencia → Forma, con lo demás de la geometría de la nube."] =
            "Would you rather the cloud took its own place on screen, like the taskbar, instead of floating over the windows? That is «Reserve screen space», and it lives in Appearance → Shape, with the rest of the cloud's geometry.",

        // ---------- Ajustes: lo que se lee de un vistazo ----------
        //
        // Títulos de grupo, interruptores, botones y opciones de las trece páginas.
        // Salieron de recorrer el código y comparar con esta tabla, no a ojo: hay un
        // extractor que junta los literales que pasan por `T()` —uniendo las
        // concatenaciones, que si no la clave no coincide— y resta los que ya están.
        ["A este otro"] = "To this one",
        ["Abrir carpeta"] = "Open folder",
        ["Activar NubeHypr"] = "Turn NubeHypr on",
        ["Activar la integración"] = "Turn the integration on",
        ["Agrupar"] = "Group",
        ["Agrupar las ventanas del mismo programa"] = "Group windows of the same program",
        ["Ajustar ventanas a los bordes"] = "Snap windows to the edges",
        ["Ajustes de este tipo"] = "Settings for this type",
        ["Al salir de NubeBar"] = "When NubeBar quits",
        ["Animaciones"] = "Animations",
        ["Aplicar"] = "Apply",
        ["Aplicar el fondo ahora"] = "Apply the background now",
        ["Apps fijadas en la barra"] = "Apps pinned to the bar",
        ["Arrancar con Windows"] = "Start with Windows",
        ["Arrastrar una ventana a un borde la coloca ahí"] = "Dragging a window to an edge places it there",
        ["Atajos de NubeHypr"] = "NubeHypr shortcuts",
        ["Atajos de organización de ventanas"] = "Window arrangement shortcuts",
        ["Añadir"] = "Add",
        ["Añadir una regla"] = "Add a rule",
        ["Barra"] = "Bar",
        ["Biblioteca de widgets"] = "Widget library",
        ["Borrar"] = "Delete",
        ["Buscador web"] = "Web search",
        ["Buscar también en archivos (índice de Windows)"] = "Search files too (Windows index)",
        ["Cada isla enseña las apps de su pantalla"] = "Each island shows the apps on its own screen",
        ["Calcular ahora"] = "Work it out now",
        ["Cambiar cada (segundos)"] = "Change every (seconds)",
        ["Carpeta con las imágenes"] = "Folder with the images",
        ["Color"] = "Colour",
        ["Comprobar cuáles están cogidos"] = "Check which ones are taken",
        ["Conservarlos"] = "Keep them",
        ["Cuándo se abren"] = "When they open",
        ["Cuándo se ve"] = "When it is shown",
        ["Cómo encaja"] = "How it fits",
        ["Cómo encaja la imagen"] = "How the image fits",
        ["De este color"] = "In this colour",
        ["De serie"] = "Built in",
        ["Dejarlo"] = "Leave it",
        ["Desde el wallpaper"] = "From the wallpaper",
        ["Deshacer"] = "Undo",
        ["Devolverle el radio al tema"] = "Give the radius back to the theme",
        ["Devolverle la letra al tema"] = "Give the typeface back to the theme",
        ["Disponibles"] = "Available",
        ["Dock"] = "Dock",
        ["Duplicar"] = "Duplicate",
        ["Dónde se guardan"] = "Where they are stored",
        ["Dónde y hasta cuándo"] = "Where, and for how long",
        ["Editar el fondo de"] = "Edit the background of",
        ["El tema"] = "The theme",
        ["Elegir el ejecutable…"] = "Choose the executable…",
        ["Elegir el wallpaper…"] = "Choose the wallpaper…",
        ["Eliminar"] = "Remove",
        ["Enseñar las notificaciones en la isla"] = "Show notifications in the island",
        ["Enseñar solo las activas"] = "Show only the active ones",
        ["Enseñar solo las apps del escritorio virtual actual"] = "Show only the apps on the current virtual desktop",
        ["Enviar mi color de acento al wallpaper"] = "Send my accent colour to the wallpaper",
        ["Escena de entrada"] = "Startup scene",
        ["Esconderse cuando algo ocupa la pantalla entera"] = "Hide when something takes over the whole screen",
        ["Estado"] = "Status",
        ["Estado de la integración"] = "Integration status",
        ["Exportar…"] = "Export…",
        ["Fuente"] = "Typeface",
        ["Grosor"] = "Thickness",
        ["Guardar atajos"] = "Save shortcuts",
        ["Guardar lo que tengo"] = "Save what I have",
        ["Hueco entre ventanas"] = "Gap between windows",
        ["Iconos de aplicaciones"] = "Application icons",
        ["Iconos visibles antes del desplegable (0 = solo el \"^\")"] = "Icons shown before the flyout (0 = only the \"^\")",
        ["Idioma de la interfaz"] = "Interface language",
        ["Imagen"] = "Image",
        ["Importar"] = "Import",
        ["Importar un perfil…"] = "Import a profile…",
        ["Iniciar Wallpaper Engine con NubeBar"] = "Start Wallpaper Engine with NubeBar",
        ["Integración con Wallpaper Engine"] = "Wallpaper Engine integration",
        ["Ir a los ajustes de Wallpaper Engine"] = "Go to the Wallpaper Engine settings",
        ["La isla respira con lo que suena"] = "The island breathes with what is playing",
        ["La tecla Windows abre el menú de la nube"] = "The Windows key opens the cloud's menu",
        ["Lo que tienes puesto, por zonas"] = "What you have in place, by zone",
        ["Mandar mi acento al fondo de escritorio"] = "Send my accent to the desktop background",
        ["Margen"] = "Margin",
        ["Menú Inicio"] = "Start menu",
        ["Mis temas"] = "My themes",
        ["Modo"] = "Mode",
        ["Monitor"] = "Monitor",
        ["Monitor de destino"] = "Target monitor",
        ["Monitor del que se leen los colores"] = "Monitor the colours are read from",
        ["Monitores"] = "Monitors",
        ["Mover la isla"] = "Moving the island",
        ["Mover ventanas de verdad"] = "Actually move windows",
        ["Movimiento"] = "Movement",
        ["Máximo visibles"] = "Maximum visible",
        ["Nada de esto se ha aplicado: ninguna ventana se ha movido."] = "None of this has been applied: no window has been moved.",
        ["Neon"] = "Neon",
        ["No hay ninguna ventana organizable abierta."] = "There is no arrangeable window open.",
        ["No se pudo calcular: míralo en el registro."] = "It could not be worked out: check the log.",
        ["O el nombre de una playlist (manda sobre el wallpaper)"] = "Or the name of a playlist (it overrides the wallpaper)",
        ["Ocultarla mientras la nube esté abierta"] = "Hide it while the cloud is running",
        ["Orden aleatorio"] = "Shuffle",
        ["Paleta y monitor"] = "Palette and monitor",
        ["Paneles laterales"] = "Side panels",
        ["Pantallas"] = "Screens",
        ["Pasar a la siguiente ahora"] = "Go to the next one now",
        ["Perfiles de escritorio"] = "Desktop profiles",
        ["Ponérmelo"] = "Put it on",
        ["Posición"] = "Position",
        ["Preguntar antes de mover ventanas"] = "Ask before moving windows",
        ["Probar"] = "Try it",
        ["Probar regla"] = "Test rule",
        ["Probar wallpaper"] = "Test wallpaper",
        ["Quitar"] = "Remove",
        ["Qué cargar"] = "What to load",
        ["Qué pasaría con las ventanas de ahora"] = "What would happen to the windows open now",
        ["Qué puedes hacer"] = "What you can do",
        ["Radio de esquinas"] = "Corner radius",
        ["Reacción al sonido"] = "Reaction to sound",
        ["Reglas"] = "Rules",
        ["Renombrar"] = "Rename",
        ["Restablecer todo"] = "Reset everything",
        ["Restaurar"] = "Restore",
        ["Sacar los colores del fondo de escritorio"] = "Pull the colours from the desktop background",
        ["Saludo en la cabecera"] = "Greeting in the header",
        ["Se puede apartar arrastrándola"] = "It can be moved aside by dragging",
        ["Segundos antes de volver a su sitio"] = "Seconds before it springs back",
        ["Tamaño"] = "Size",
        ["Tema"] = "Theme",
        ["Texto"] = "Text",
        ["Tipo"] = "Type",
        ["Tipo de fondo"] = "Background type",
        ["Todos de serie"] = "All built in",
        ["Todos los colores"] = "All the colours",
        ["Traer los iconos de la bandeja a la isla"] = "Bring the tray icons into the island",
        ["Tus perfiles"] = "Your profiles",
        ["Usar el tema limpio"] = "Use the clean theme",
        ["Usar este ejemplo"] = "Use this example",
        ["Usar este fondo en todos los monitores"] = "Use this background on every monitor",
        ["Usar la ruta detectada"] = "Use the detected path",
        ["Ver escena de inicio"] = "Watch the startup scene",
        ["Ver guía completa"] = "See the full guide",
        ["Ver la escena ahora"] = "Watch the scene now",
        ["Visibilidad"] = "Visibility",
        ["Vista general de ventanas"] = "Window overview",
        ["Vista previa"] = "Preview",
        ["Volver al fondo anterior"] = "Back to the previous background",
        ["Wallpaper"] = "Wallpaper",
        ["Wallpaper o playlist"] = "Wallpaper or playlist",
        ["evalúa las ventanas abiertas; no mueve ninguna"] = "evaluates the open windows; it moves none",
        ["se añade apagada y sin guardar"] = "it is added switched off and unsaved",
        ["Ángulo"] = "Angle",

        // ---------- Paneles: secciones ----------
        ["Nube"] = "Nube",
        ["Tu entorno, en calma"] = "Your desktop, at ease",
        ["Acciones rápidas"] = "Quick actions",
        ["Lo que se enciende y se apaga de un toque"] = "What you switch on and off with one tap",
        ["Audio y brillo"] = "Audio and brightness",
        ["El volumen del sistema y el brillo del monitor"] = "System volume and monitor brightness",
        ["Carpetas"] = "Folders",
        ["Accesos a tus carpetas de siempre"] = "Shortcuts to your usual folders",
        ["Sistema"] = "System",
        ["CPU, memoria y almacenamiento"] = "CPU, memory and storage",
        ["Ajustes"] = "Settings",
        ["Tema, modo nocturno y la ventana de ajustes"] = "Theme, night light and the settings window",
        ["Reproduciendo"] = "Now playing",
        ["El centro multimedia"] = "The media centre",
        ["Notificaciones"] = "Notifications",
        ["Lo que ha llegado"] = "What has come in",
        ["Calendario"] = "Calendar",
        ["El mes, con hoy marcado"] = "The month, with today marked",
        ["Espacios de trabajo"] = "Workspaces",
        ["Los escritorios virtuales, y a cuál vas"] = "Virtual desktops, and which one you go to",
        ["Conexión y batería"] = "Connection and battery",
        ["Red y energía"] = "Network and power",
        ["Clima y sistema"] = "Weather and system",
        ["El tiempo donde estás, y qué máquina es esta"] = "The weather where you are, and what this machine is",

        // ---------- Acciones rápidas ----------
        ["No molestar"] = "Do not disturb",
        ["Modo nocturno"] = "Night light",
        ["Tema claro"] = "Light theme",
        ["Tema oscuro"] = "Dark theme",
        ["Wallpaper Engine"] = "Wallpaper Engine",
        ["Silenciado"] = "Muted",
        ["Normal"] = "Normal",
        ["Oscuro"] = "Dark",
        ["Claro"] = "Light",
        ["Integrado"] = "Integrated",
        ["Activo"] = "On",
        ["Apagado"] = "Off",
        ["Automático"] = "Automatic",

        // ---------- Carpetas ----------
        ["Escritorio"] = "Desktop",
        ["Documentos"] = "Documents",
        ["Descargas"] = "Downloads",
        ["Imágenes"] = "Pictures",

        // ---------- Ajustes, filas del panel ----------
        ["Ajustes de la nube"] = "Nube settings",
        ["Tema, estilo, módulos y atajos"] = "Theme, style, modules and shortcuts",
        ["NubeHypr"] = "NubeHypr",
        ["Apariencia y temas"] = "Appearance and themes",
        ["Colores, estilo y halo"] = "Colours, style and glow",
        ["Moviendo ventanas de verdad"] = "Actually moving windows",
        ["En simulación: propone y no toca"] = "Simulating: it proposes, it doesn't touch",
        ["Modo seguro"] = "Safe mode",
        ["Moviendo ventanas"] = "Moving windows",
        ["En simulación"] = "Simulating",

        // ---------- Estados vacíos y avisos ----------
        ["No hay avisos"] = "No notifications",
        ["Lo que llegue de Windows aparecerá aquí"] = "Anything from Windows will show up here",
        ["No hay nada sonando"] = "Nothing is playing",
        ["Sin datos."] = "No data.",
        ["Conectado"] = "Connected",
        ["Sin conexión"] = "No connection",
        ["Cable"] = "Wired",
        ["El tiempo está apagado. Puedes encenderlo en Ajustes."] =
            "Weather is off. You can turn it on in Settings.",
        ["Escribe tu ciudad en Ajustes para ver el tiempo."] =
            "Type your city in Settings to see the weather.",
        ["Pidiendo el tiempo…"] = "Fetching the weather…",
        ["Sin datos del tiempo todavía."] = "No weather data yet.",
        ["No se llega al servicio del tiempo"] = "Can't reach the weather service",
        ["Este panel está vacío. Mueve alguna sección desde el otro."] =
            "This panel is empty. Move a section over from the other one.",
        ["No se ha podido cargar. El motivo está en el registro."] =
            "Couldn't load. The reason is in the log.",

        // ---------- El tiempo ----------
        ["Despejado"] = "Clear",
        ["Casi despejado"] = "Mostly clear",
        ["Nubes dispersas"] = "Scattered clouds",
        ["Nublado"] = "Cloudy",
        ["Niebla"] = "Fog",
        ["Llovizna"] = "Drizzle",
        ["Lluvia"] = "Rain",
        ["Nieve"] = "Snow",
        ["Chubascos"] = "Showers",
        ["Nevadas"] = "Snowfall",
        ["Tormenta"] = "Thunderstorm",
        ["Sin datos"] = "No data",

        // ---------- Módulos de la barra ----------
        ["Reloj"] = "Clock",
        ["La hora, en horas y minutos"] = "The time, in hours and minutes",
        ["Fecha"] = "Date",
        ["El día de la semana, el día y el mes"] = "Day of the week, day and month",
        ["Inicio"] = "Start",
        ["Abre el menú de aplicaciones"] = "Opens the app menu",
        ["Apps abiertas"] = "Open apps",
        ["El carrusel de ventanas, agrupadas por programa"] = "The window carousel, grouped by program",
        ["Apps fijadas"] = "Pinned apps",
        ["Las apps que has anclado, abiertas o no"] = "The apps you pinned, open or not",
        ["Escritorios"] = "Desktops",
        ["CPU"] = "CPU",
        ["Porcentaje de uso del procesador"] = "Processor usage percentage",
        ["Memoria"] = "Memory",
        ["Porcentaje de memoria en uso"] = "Memory in use, as a percentage",
        ["Batería"] = "Battery",
        ["Bandeja"] = "Tray",
        ["Los iconos de las apps en segundo plano"] = "Icons of apps running in the background",
        ["Centro de control"] = "Control centre",
        ["Idioma, red, volumen y batería, en un bloque"] = "Language, network, volume and battery, as one block",
        ["Red"] = "Network",
        ["El icono de red: cable, wifi o sin conexión"] = "The network icon: wired, Wi-Fi or offline",
        ["Volumen"] = "Volume",
        ["El altavoz, con rueda para subir y bajar"] = "The speaker, scroll to turn it up and down",
        ["Idioma del teclado"] = "Keyboard language",
        ["La distribución actual; no sale si solo tienes una"] =
            "The current layout; hidden if you only have one",
        ["Pila"] = "Battery icon",
        ["La batería dibujada, con su nivel y el rayo al cargar"] =
            "The drawn battery, with its level and a bolt while charging",
        ["Música"] = "Music",
        ["Lo que suena"] = "Now playing",
        ["La canción y su carátula; al pulsar, el avance"] =
            "The track and its cover art; click for the progress",
        ["sin nada sonando no ocupa sitio"] = "with nothing playing it takes up no room",
        ["Separador"] = "Separator",
        ["Una rayita para separar grupos de widgets"] = "A thin line to separate groups of widgets",

        // ---------- Lo que suena, y su letra ----------
        ["No hay nada sonando."] = "Nothing is playing.",
        ["No hay nada sonando en Spotify."] = "Nothing is playing on Spotify.",

        // ---------- Estilos ----------
        ["Nube (estilo)"] = "Nube",
        ["Neón"] = "Neon",
        ["Sobrio, con el filo justo. Lo de siempre."] = "Sober, with just enough edge. The usual.",
        ["Más redondo y con más aire. El de las tarjetas grandes."] =
            "Rounder, with more room to breathe. The one with big cards.",
        ["Filo discreto y un halo de color que respira desde fuera."] =
            "A discreet edge and a colour glow that breathes from outside.",
        ["Susurro"] = "Whisper",
        ["Equilibrado"] = "Balanced",
        ["Intenso"] = "Intense",
        ["Fijo"] = "Steady",
        ["Personalizado"] = "Custom",

        // ---------- Ajustes: páginas y grupos ----------
        ["Apariencia"] = "Appearance",
        ["Colores"] = "Colours",
        ["Fondo"] = "Background",
        ["Perfiles"] = "Profiles",
        ["Módulos"] = "Modules",
        ["Biblioteca"] = "Library",
        ["Apps"] = "Apps",
        ["Atajos"] = "Shortcuts",
        ["Estilo"] = "Style",
        ["Forma"] = "Shape",
        ["Clima"] = "Weather",
        ["Archivos"] = "Files",

        // ---------- Ajustes → Sistema → Música ----------
        ["Solo cuando reproduce Spotify"] = "Only when Spotify is playing",
        ["Apagado —como viene— los módulos de música enseñan lo que suene, venga de Spotify, del navegador o de un reproductor. Encendido, se callan con todo lo que no sea Spotify. Ojo: Spotify EN EL NAVEGADOR no cuenta, porque Windows lo anuncia como el navegador y no como Spotify."] =
            "Off — as it ships — the music modules show whatever is playing, be it Spotify, " +
            "the browser or a media player. On, they stay quiet for anything that isn't " +
            "Spotify. Note: Spotify IN THE BROWSER does not count, because Windows announces " +
            "it as the browser and not as Spotify.",
        ["Y de este equipo no sale nada por la música: lo que se enseña viene de la sesión de medios de Windows, que es local. No hay cuenta que conectar ni servicio al que preguntar."] =
            "And nothing leaves your machine because of music: what you see comes from the " +
            "Windows media session, which is local. There is no account to connect and no " +
            "service to ask.",

        // ---------- Ajustes → Apariencia → Forma: la notch ----------
        ["La notch es la isla con otra silueta: pegada al borde sin margen, redondeada solo por el lado que da a la pantalla y con el fondo opaco, como la muesca de un MacBook. Hace todo lo que hace la isla —se centra, se abre al pasar el ratón, los paneles salen de dentro— y el radio sigue siendo el del tema: con Cyberpunk, que va a radio 0, la muesca sale rectangular."] =
            "The notch is the island with a different silhouette: flush against the edge with " +
            "no margin, rounded only on the side facing the screen, and with an opaque " +
            "background, like a MacBook's notch. It does everything the island does — it " +
            "centres itself, it opens when the pointer reaches it, panels grow out of it " +
            "— and the corner radius still comes from the theme: with Cyberpunk, which " +
            "goes to radius 0, the notch comes out rectangular.",
        ["En «izquierda» y «derecha», el modo barra se vuelve una barra vertical con las zonas apiladas; el modo isla solo se ancla a ese lado, y la notch se dibuja arriba —una muesca sale del borde de la cámara, no de un lado—."] =
            "On «left» and «right», bar mode becomes a vertical bar " +
            "with the zones stacked; island mode just anchors to that side, and the notch is " +
            "drawn at the top — a notch comes out of the camera's edge, not out of a " +
            "side.",
        ["Arranque"] = "Startup",
        ["Bandeja del sistema"] = "System tray",
        ["Barra de tareas de Windows"] = "Windows taskbar",
        ["Reservar espacio en pantalla, como la barra de tareas"] =
            "Reserve screen space, like the taskbar",
        ["Halo del neón"] = "Neon glow",
        ["Preajuste"] = "Preset",
        ["Intensidad del halo"] = "Glow intensity",
        ["Radio del halo (px)"] = "Glow radius (px)",
        ["Lo que tarda en respirar (ms)"] = "Breathing cycle (ms)",
        ["Que el halo respire"] = "Let the glow breathe",
        ["Color del halo"] = "Glow colour",
        ["Qué lleva cada panel"] = "What each panel holds",
        ["Panel izquierdo"] = "Left panel",
        ["Panel derecho"] = "Right panel",
        ["Volver al reparto de serie"] = "Back to the default layout",
        ["Subir"] = "Move up",
        ["Bajar"] = "Move down",
        ["Llevar al panel derecho"] = "Move to the right panel",
        ["Llevar al panel izquierdo"] = "Move to the left panel",
        ["Separadores automáticos entre grupos"] = "Automatic separators between groups",
        ["Enseñar el tiempo en el panel derecho"] = "Show the weather in the right panel",
        ["Detectar mi ubicación"] = "Detect my location",
        ["Idioma"] = "Language",
        ["Guardar"] = "Save",
        ["Cancelar"] = "Cancel",
        ["Cambios sin guardar"] = "Unsaved changes",
        ["Abrir la carpeta del config"] = "Open the config folder",
        ["Abrir Ajustes"] = "Open Settings",
        ["Ajustes de la nube"] = "Nube settings",
        ["Ajustes del sistema"] = "System settings",
        ["Ver todas las ventanas"] = "Show all windows",
        ["Escritorio nuevo"] = "New desktop",
        ["Buscar aplicaciones, archivos y en la web…"] = "Search apps, files and the web…",
    };
}
