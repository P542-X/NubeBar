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
        return En.TryGetValue(texto, out var t) ? t : texto;
    }

    // Cuántas hay y cuántas faltan, para poder medir el avance sin contarlas a mano.
    public static int Traducidas => En.Count;

    static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
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
        ["Separador"] = "Separator",
        ["Una rayita para separar grupos de widgets"] = "A thin line to separate groups of widgets",

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
        ["Arranque"] = "Startup",
        ["Bandeja del sistema"] = "System tray",
        ["Barra de tareas de Windows"] = "Windows taskbar",
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
