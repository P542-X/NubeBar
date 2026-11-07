using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// La isla, vista como anfitriona de widgets.
//
// Aquí viven tres cosas:
//
//  1. El registro de los widgets de casa. Cada uno envuelve LA MISMA expresión que
//     tenía en el switch de CrearModulo, sin tocarla: así se puede asegurar que el
//     reloj sigue siendo el reloj de antes. Lo que cambia es que ahora el reloj y un
//     widget de fuera entran por la misma puerta.
//  2. La implementación de IAnfitrionWidget, que es lo que un widget ve de la isla.
//  3. El bucle que rellena las zonas, que ya no son tres campos sueltos sino la
//     colección Paneles[0].Zonas.
public partial class MainWindow : Window, IAnfitrionWidget
{
    // ================= Lo que un widget ve de la isla =================
    // Explícito a propósito: estos miembros no ensucian el resto de MainWindow, y
    // quien quiera usarlos tiene que pedir el anfitrión, no la ventana.
    Roles IAnfitrionWidget.Roles => _roles;
    NivelAnimacion IAnfitrionWidget.Animacion => Animacion.Nivel;
    double IAnfitrionWidget.Grosor => _cfg.Barra.Alto;
    void IAnfitrionWidget.CadaTick(Action refresco) => _actualizadores.Add(refresco);
    Button IAnfitrionWidget.Boton(object contenido, string tip, Action accion) => Boton(contenido, tip, accion);
    TextBlock IAnfitrionWidget.Glifo(string codigo) => Glifo(codigo);
    TextBlock IAnfitrionWidget.Etiqueta(Func<string> valor, bool animar, string? muestra) => Etiqueta(valor, animar, muestra);
    TextBlock IAnfitrionWidget.Texto(string t, double tamano) => Texto(t, tamano);

    // ================= Registro de los widgets de casa =================
    static bool _widgetsRegistrados;

    // La isla le dice al registro QUIÉN sabe llenarlo, y así quien lo usa (la
    // biblioteca, y mañana el modo edición) solo tiene que pedir Asegurar() sin
    // depender de MainWindow. Se ejecuta la primera vez que alguien toca esta clase,
    // que en la aplicación de verdad es siempre antes de que haya interfaz.
    static MainWindow() => RegistroWidgets.AlIniciar(AsegurarWidgetsRegistrados);

    // Se llama antes de rellenar por primera vez, y también desde la ventana de
    // ajustes: si se abriera antes de que ninguna isla haya montado sus zonas, la
    // lista de módulos disponibles saldría vacía. Idempotente: con dos monitores hay
    // dos islas y las dos pasan por aquí.
    internal static void AsegurarWidgetsRegistrados()
    {
        if (_widgetsRegistrados) return;
        _widgetsRegistrados = true;

        // ---------- Los iconos del catálogo ----------
        //
        // Siete módulos se registraron sin icono —reloj, fecha, nube, cpu, ram, batería y
        // notificaciones: un tercio del catálogo— y nadie lo notó porque el sitio donde se
        // enseñan era una lista de texto con los ids en crudo. En cuanto la página de
        // módulos pasó a ser de chips con icono, quedaron siete huecos en fila.
        //
        // Cuatro de estos ya se pintan en otras superficies de NubeBar y están vistos; los
        // tres restantes los comprueba la prueba de glifos (que existan en la fuente) y una
        // captura (que sean lo que dicen ser, que es lo que una prueba no puede mirar).
        const string Reloj = "";          // Clock
        const string Calendario = "";     // Calendar
        const string Nubecita = "";       // Cloud — el mismo que ya usa NubeHypr
        const string Procesador = "";     // el de la tarjeta de CPU del centro de control
        const string Memoria = "";        // el de la tarjeta de RAM
        const string Pila = "";           // Battery9
        // La MISMA campana que dibuja el módulo en la barra (MainWindow.CentroNotificaciones).
        // Había dos: el módulo pintaba esta y el catálogo enseñaba otra, así que el icono
        // que veías en Ajustes no era el que ibas a ver en la barra. Un sistema visual, no
        // dos parecidos.
        const string Campana = "";

        void R(string id, string nombre, string desc, string glifo, CategoriaWidget cat,
               Func<MainWindow, UIElement> crear, string zona = "Derecha", bool repetible = false,
               string? puedeNoVerse = null)
            => RegistroWidgets.Registrar(new WidgetDeCasa(
                   new DescriptorWidget(id, nombre, desc, glifo, cat, zona, repetible, puedeNoVerse),
                   crear));

        // ---------- Reloj ----------
        // Cada etiqueta dice cómo se escribe APILADA: en una columna de 38 px no cabe
        // "00:05" de una pieza, y partirlo por su sitio es cosa de quien sabe lo que
        // significa. La hora va como en el panel de Plasma: las horas sobre los minutos.
        R("reloj", "Reloj", "La hora, en horas y minutos", Reloj, CategoriaWidget.Reloj,
          i => i.Etiqueta(() => DateTime.Now.ToString("HH:mm"), animar: true,
                          vertical: () => DateTime.Now.ToString("HH") + Salto + DateTime.Now.ToString("mm"),
                          brillo: true),
          zona: "Centro");

        R("fecha", "Fecha", "El día de la semana, el día y el mes", Calendario, CategoriaWidget.Reloj,
          i => i.Etiqueta(() => DateTime.Now.ToString("ddd d MMM"), animar: true,
                          vertical: () => DateTime.Now.ToString("ddd") + Salto + DateTime.Now.ToString("d MMM")));

        // ---------- Apps ----------
        R("inicio", "Inicio", "Abre el menú de aplicaciones", "", CategoriaWidget.Apps,
          i => i.ModuloInicio(), zona: "Izquierda");

        R("apps", "Apps abiertas", "El carrusel de ventanas, agrupadas por programa", "", CategoriaWidget.Apps,
          i => i.ModuloApps(), zona: "Centro");

        R("fijadas", "Apps fijadas", "Las apps que has anclado, abiertas o no", "", CategoriaWidget.Apps,
          i => i.ModuloFijas(), zona: "Centro",
          puedeNoVerse: "sin ninguna app anclada no ocupa sitio, para no descentrar el reloj");

        R("escritorios", "Escritorios", "Los escritorios virtuales, con el actual en acento", "", CategoriaWidget.Apps,
          i => i.ModuloEscritorios());

        // ---------- E1: el estado de la nube ----------
        //
        // La barra enseñaba el estado de todo —red, audio, batería— menos el de la pieza que
        // organiza tus ventanas. Para saber si NubeHypr está apagado, simulando o aplicando
        // había que abrir Ajustes, y es justo el dato que conviene tener delante.
        R("nube", "Estado de la nube", "Si NubeHypr está apagado, simulando o aplicando",
          Nubecita, CategoriaWidget.Sistema, i => i.ModuloNube());

        // ---------- Sistema ----------
        // Mismo criterio que el reloj: el rótulo sobre la cifra. "CPU 100%" mide 56 px
        // y no cabe en una columna ni subiendo el grosor a 56; partido, cada línea mide
        // menos de 30 y cabe hasta con el grosor al mínimo.
        R("cpu", "CPU", "Porcentaje de uso del procesador", Procesador, CategoriaWidget.Sistema,
          i => i.Etiqueta(() => $"CPU {Nativo.Cpu()}%", muestra: "CPU 100%",
                          vertical: () => "CPU" + Salto + $"{Nativo.Cpu()}%"));

        R("ram", "Memoria", "Porcentaje de memoria en uso", Memoria, CategoriaWidget.Sistema,
          i => i.Etiqueta(() => $"RAM {Nativo.Ram()}%", muestra: "RAM 100%",
                          vertical: () => "RAM" + Salto + $"{Nativo.Ram()}%"));

        R("bateria", "Batería", "Carga de la batería; no sale si el equipo no tiene", Pila, CategoriaWidget.Sistema,
          i => i.Etiqueta(Nativo.Bateria, muestra: "BAT 100%",
                          vertical: () => EnDosLineas(Nativo.Bateria())),
          puedeNoVerse: "en un equipo sin batería no se dibuja");

        R("bandeja", "Bandeja", "Los iconos de las apps en segundo plano", "", CategoriaWidget.Sistema,
          i => i.ModuloBandeja());

        R("control", "Centro de control", "Idioma, red, volumen y batería", "", CategoriaWidget.Sistema,
          i => i.ModuloControl());

        // ---------- La entrada de vídeo de cada monitor ----------
        //
        // Va con los de sistema y no con los de apps porque es estado del equipo, como la red o
        // el sonido. Y lleva `puedeNoVerse` porque de verdad puede no verse: si ningún monitor
        // del equipo contesta al VCP 0x60, el icono no se dibuja nunca (ver
        // `Entradas.Disponible`). Sin esa nota, el editor de módulos deja un hueco invisible en
        // la barra y no dice por qué.
        R("monitores", "Monitores", "La entrada de vídeo de cada pantalla: DisplayPort, HDMI…",
          char.ConvertFromUtf32(Lateral.Glifos.Escritorio), CategoriaWidget.Sistema,
          i => i.ModuloMonitores(),
          puedeNoVerse: "si ningún monitor permite cambiar su entrada por DDC/CI, no se dibuja");

        // ---------- Y los mismos, sueltos ----------
        //
        // «Centro de control» monta los cuatro juntos, así que en la barra se ven como cuatro
        // iconos pero el editor de módulos los trataba como uno: no había forma de llevarse el
        // de la red al centro y dejar el volumen a la derecha. El usuario lo pidió — «debo
        // poder mover todo lo que hay en la nube, tanto ethernet como sonido como lenguaje de
        // teclado, al centro, izquierda o derecha».
        //
        // El bloque se queda por compatibilidad: quitarlo dejaría un hueco en la barra de todo
        // el que ya lo tiene puesto. Quien quiera moverlos por separado añade estos y quita el
        // bloque; quien no toque nada, no nota nada.
        // El glifo de cada uno va por CÓDIGO y sale de `Lateral.Glifos`, que es la lista que
        // la prueba recorre para comprobar que existen de verdad en la fuente. Pasarlos como
        // cadena vacía —que es lo que hice primero— lo cazó una prueba al instante: «“red”
        // tiene icono para el chip». El editor enseña ese glifo en cada ficha.
        R("red", "Red", "El icono de red: cable, wifi o sin conexión",
          char.ConvertFromUtf32(Lateral.Glifos.Conexion), CategoriaWidget.Sistema,
          i => i.ModuloRed());

        R("volumen", "Volumen", "El altavoz, con rueda para subir y bajar",
          char.ConvertFromUtf32(Lateral.Glifos.Volumen), CategoriaWidget.Sistema,
          i => i.ModuloVolumen());

        R("teclado", "Idioma del teclado", "La distribución actual; no sale si solo tienes una",
          char.ConvertFromUtf32(Lateral.Glifos.Teclado), CategoriaWidget.Sistema,
          i => i.ModuloIdioma(),
          puedeNoVerse: "con una sola distribución no hay nada que enseñar");

        R("pila", "Pila", "La batería dibujada, con su nivel y el rayo al cargar",
          char.ConvertFromUtf32(Lateral.Glifos.Pila), CategoriaWidget.Sistema,
          i => i.ModuloPila(),
          puedeNoVerse: "en un equipo sin batería no se dibuja");

        R("notificaciones", "Notificaciones", "La campana, con las que no has leído y su historial",
          Campana, CategoriaWidget.Sistema, i => i.ModuloNotificaciones());

        // ---------- Medios ----------
        R("musica", "Música", "Carátula y barras de lo que suena", "", CategoriaWidget.Medios,
          i => new StackPanel
          {
              Orientation = Orientation.Horizontal,
              Children = { i.ModuloCaratula(), i.ModuloBarras() }
          },
          puedeNoVerse: "sin nada sonando no ocupa sitio");

        R("musica-caratula", "Música: carátula", "Solo la carátula del disco", "", CategoriaWidget.Medios,
          i => i.ModuloCaratula(),
          puedeNoVerse: "sin nada sonando no hay carátula que enseñar");

        R("musica-barras", "Música: barras", "Solo las barras que bailan con el sonido", "", CategoriaWidget.Medios,
          i => i.ModuloBarras(),
          puedeNoVerse: "las barras solo se dibujan mientras suena algo");

        // ---------- Aspecto ----------
        R("wallpaper", "Wallpaper", "Pausa, silencio y ocultar iconos de Wallpaper Engine", "", CategoriaWidget.Aspecto,
          i => i.ModuloWallpaper());

        R("wallpaper-titulo", "Wallpaper: título", "El nombre del fondo que tienes puesto", "", CategoriaWidget.Aspecto,
          i => i.Etiqueta(() => i._wpTitulo ?? "", animar: true));

        R("iconos", "Iconos del escritorio", "Enseña y esconde los iconos del escritorio", "", CategoriaWidget.Aspecto,
          i => i.Boton(i.Glifo(""), "Mostrar / ocultar iconos del escritorio", () =>
          {
              i._iconosOcultos = !i._iconosOcultos;
              WallpaperEngine.Enviar(i._iconosOcultos ? "hideIcons" : "showIcons");
          }));

        // ---------- Útiles ----------
        R("separador", "Separador", "Una rayita para separar grupos de widgets", "", CategoriaWidget.Util,
          i => i.Separador(), repetible: true);

        Log.Info($"widgets registrados: {RegistroWidgets.Todos.Count}");
    }

    // ---------- El estado de la capa de ventanas, al arrancar ----------
    //
    // Una línea por sesión con el formato de la especificación. No es decoración: es lo
    // que permite responder "¿tenías NubeHypr encendida?" leyendo el log del usuario, sin
    // pedirle que abra su config. Y sale SIEMPRE, también apagada: un diagnóstico que
    // solo aparece cuando algo está encendido no sirve para descartar.
    //
    // ---------- D7: y ahora el adaptador SÍ existe ----------
    //
    // Esta línea decía `adapter=stopped` fijo, y en D0 era la verdad porque no había motor.
    // Desde D2 lo hay, así que dejarlo a "stopped" pasó de ser honesto a ser una mentira al
    // revés: el adaptador está listo y lo que está apagado es la capa, que es otra cosa.
    // `off` es eso: montado y sin encender.
    void ApuntarSaludDeHypr()
    {
        if (!_esJefa) return;
        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        Log.Info(HyprSalud.Linea(
            adaptador: modo == NubeHypr.Modo.Apagado ? "off" : "ready",
            version: _cfg.Version));
        Log.Info($"[D0]  nubehypr activo={(_cfg.NubeHypr.Activo ? "sí" : "no")} " +
                 $"modo={modo.ToString().ToLowerInvariant()} " +
                 $"gap={_cfg.NubeHypr.Gap} margen={_cfg.NubeHypr.Margen} " +
                 $"layout={_cfg.NubeHypr.Layout} " +
                 $"reglas={_cfg.NubeHypr.Reglas.Count} atajos={_cfg.NubeHypr.Atajos.Count}");

        // Un modo que no se reconoce no se acepta en silencio: se dice que se ha caído en
        // simulación y de qué venía.
        if (_cfg.NubeHypr.Activo && !NubeHypr.ModoConocido(_cfg.NubeHypr.Modo))
            Log.Info($"[D0]  «{_cfg.NubeHypr.Modo}» no es un modo conocido: se usa simulación " +
                     "(calcula y apunta, no mueve nada)");
    }

    // ---------- Escribir apilado ----------
    //
    // El salto de línea de los formatos verticales. Como constante y no suelto en cada
    // registro por un motivo tonto pero real: escrito a mano dentro de una cadena de
    // formato de fecha es un carácter literal que nadie espera ahí, y escrito en medio
    // de una expresión se pierde entre comillas al editar el fichero.
    internal const string Salto = "\n";

    // Para lo que no tiene un sitio evidente por donde partirse: se corta por el último
    // espacio que deje las dos mitades más parejas. "BAT 100%" sale "BAT" sobre "100%";
    // un título de wallpaper largo se parte por la mitad y el resto lo arregla el
    // ajuste de línea de la propia etiqueta.
    internal static string EnDosLineas(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Contains('\n')) return texto;

        int mejor = -1;
        double objetivo = texto.Length / 2.0;
        for (int i = 0; i < texto.Length; i++)
        {
            if (texto[i] != ' ') continue;
            if (mejor < 0 || Math.Abs(i - objetivo) < Math.Abs(mejor - objetivo)) mejor = i;
        }
        return mejor < 0 ? texto : texto[..mejor] + Salto + texto[(mejor + 1)..];
    }
    // ================= El separador =================
    //
    // Una rayita vertical entre grupos de widgets. No hace nada más, y justo por eso
    // tenía tres cosas mal que no se notaban hasta que alguien lo ponía:
    //
    //  1. **No decía cómo alinearse.** Un Border nace con VerticalAlignment.Stretch, y
    //     con un Height fijo eso deja la posición en manos de cómo WPF resuelve un
    //     "estírate" que no se puede cumplir. Funcionaba de casualidad mientras todos
    //     sus vecinos midieran lo mismo. Ahora lo dice: Center.
    //
    //  2. **Su alto era un 16 escrito a mano**, que no sabía nada de Barra.Alto. Con la
    //     barra fina la raya ocupaba casi todo; con la barra gruesa era un muñón. Y si
    //     algún vecino hacía la fila más alta que Barra.Alto, la raya se centraba en la
    //     FILA y no en la isla, y el recorte de Isla (ClipToBounds) se le comía un
    //     trozo. Ahora sale del alto de la barra y nunca puede pasarse de ella.
    //
    //  3. **Su color era _texto con Opacity 0.3**, otra mezcla a ojo de las que quitó
    //     la fase B1. Un separador es un borde: le toca el rol Borde, que además lo
    //     hace temable (en Nube es azulado, no gris).
    //
    // MaxHeight además de Height: aunque alguien ponga un Barra.Alto raro, la raya no
    // puede crecer más que el hueco y provocar el recorte otra vez.
    internal UIElement Separador()
    {
        double largo = AltoSeparador(_cfg.Barra.Alto);

        // Y un cuarto detalle, de B6: en la barra VERTICAL la rayita va tumbada. Un
        // separador separa en la dirección en la que se apilan los widgets, así que
        // las dos medidas y los dos márgenes se cambian de sitio.
        bool v = Vertical;
        return new Border
        {
            Width = v ? largo : 1,
            Height = v ? 1 : largo,
            MaxWidth = v ? largo : double.PositiveInfinity,
            MaxHeight = v ? double.PositiveInfinity : largo,
            Margin = v ? new Thickness(0, 6, 0, 6) : new Thickness(6, 0, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            // No `_borde`: ver `ColorDelSeparador`. Con el rol de filo la rayita quedaba
            // al 23 % sobre el fondo de la barra y no se veía.
            Background = new SolidColorBrush(ColorDelSeparador(_roles)),
            // Una línea de 1 px entre píxeles sale gris y borrosa con DPI no entero
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
    }

    // Proporción, no número mágico: 16 px con la barra de 38 (lo de siempre), y de ahí
    // escala. El mínimo es para que no desaparezca con una barra muy fina; el máximo,
    // para que siempre quepa con holgura dentro de la fila.
    // ================= Dónde se separa un módulo de sus vecinos =================
    //
    // Un módulo se separa de los de al lado, y «al lado» no es lo mismo en las dos
    // orientaciones: en horizontal los vecinos están a izquierda y derecha, y en vertical
    // arriba y abajo. Escrito a pelo como `Thickness(4, 0, 4, 0)` queda bien en
    // horizontal y en vertical hace dos cosas mal a la vez: gasta ocho píxeles de ancho
    // en una columna de cuarenta, y deja a los módulos pegados unos a otros.
    //
    // Y hay una segunda mitad que no se ve leyendo. En una pila VERTICAL, un elemento con
    // `Width` fijo y sin alineación no queda centrado: WPF lo pega al filo izquierdo de
    // la columna. Así que la carátula del disco salía descolgada a un lado mientras el
    // resto de iconos iba centrado.
    //
    // Juntas las dos, es lo que contó el usuario con la barra a la izquierda y música
    // sonando: «hay huecos que no se completan, como que no van juntos».
    //
    // El separador ya lo hacía bien, pero con un `?:` suyo escrito a mano: la regla
    // estaba APLICADA en un sitio y no escrita en ninguno, así que el módulo siguiente
    // volvió a nacer sin ella. Aquí está escrita, y `EnLaZona` la aplica entera.
    internal static Thickness SeparacionDeModulo(bool vertical, double antes, double despues) =>
        vertical ? new Thickness(0, antes, 0, despues)
                 : new Thickness(antes, 0, despues, 0);

    // Coloca un módulo dentro de su zona: la separación en el eje en que se apila, y en
    // vertical además centrado en la columna. Devuelve el mismo elemento para poder
    // encadenarlo donde se construye.
    T EnLaZona<T>(T e, double antes, double despues) where T : FrameworkElement
    {
        e.Margin = SeparacionDeModulo(Vertical, antes, despues);
        if (Vertical) e.HorizontalAlignment = HorizontalAlignment.Center;
        return e;
    }

    internal static double AltoSeparador(double altoBarra)
    {
        double alto = Math.Round(altoBarra * 0.42);
        return Math.Clamp(alto, 8, Math.Max(8, altoBarra - 8));
    }

    // ---------- De qué color es un separador ----------
    //
    // «Los separadores no van»: iban, y ese era el problema. Se pintaban con el rol
    // `Borde`, que lleva un velo de 0x3A —un 23 % de opacidad—, pensado para el filo de una
    // tarjeta pegada a una superficie. Una rayita suelta de un píxel al 23 % sobre el fondo
    // de la barra no se distingue de la barra: está dibujada, ocupa su sitio, y no se ve.
    // Por eso ninguna prueba lo cazó —medían que existiera y que midiera algo, que es
    // verdad— y por eso en una captura parecía que el módulo no se había añadido.
    //
    // Aquí se MIDE. Se compone el color sobre el fondo de la barra y, si no llega al
    // mínimo para distinguir una línea fina, se sube al siguiente rol. Es la misma lección
    // de la marca del dock en E8: comparar colores crudos ignorando la opacidad mide algo
    // que no pasa en pantalla.
    //
    // 1.35 no es el 4.5 del texto: una rayita decorativa no tiene que gritar, pero tiene
    // que verse. Por debajo de ~1.3 deja de distinguirse en un panel oscuro.
    internal const double ContrasteMinimoDelSeparador = 1.35;

    internal static Color ColorDelSeparador(Roles r)
    {
        var candidatos = new[] { r.Borde, r.TextoAtenuado, r.Texto };
        foreach (var c in candidatos)
        {
            var real = Contraste.Mezclar(r.FondoOpaco, c, c.A / 255.0);
            if (Contraste.Entre(real, r.FondoOpaco) >= ContrasteMinimoDelSeparador)
                return Color.FromArgb(255, real.R, real.G, real.B);
        }
        // Ninguno llega: se usa el texto atenuado a plena opacidad, que es lo más parecido
        // a «una rayita discreta pero visible» que queda.
        var ultimo = r.TextoAtenuado;
        return Color.FromArgb(255, ultimo.R, ultimo.G, ultimo.B);
    }

    // ================= Rellenar las zonas =================

    // El panel que dibuja esta isla. Hoy hay uno solo; cuando haya varios, cada isla
    // recibirá el suyo en vez de buscar el principal.
    PanelNube PanelPrincipal =>
        _cfg.Paneles.FirstOrDefault(p => p.Id == "principal")
        ?? new PanelNube
        {
            Modo = _cfg.Barra.Modo,
            Posicion = _cfg.Barra.Posicion,
            Grosor = _cfg.Barra.Alto,
            Zonas = new ZonasPanel
            {
                Izquierda = _cfg.Modulos.Izquierda,
                Centro = _cfg.Modulos.Centro,
                Derecha = _cfg.Modulos.Derecha
            }
        };

    // ---------- ¿Está puesto este módulo? ----------
    //
    // Una sola respuesta, y sale de LO MISMO que se dibuja.
    //
    // Había dos. `LlenarZonas` recorre `PanelPrincipal.Zonas`; `PrepararFijas` y la
    // bandeja preguntaban por su cuenta a `_cfg.Modulos`. Hoy son las mismas listas
    // —`Config.MigrarYDerivarPaneles` hace que el panel las REFERENCIE, no las copie—,
    // así que coincidían por suerte, no por diseño. El día que algo asignó una lista
    // nueva en vez de mutar la de siempre, las dos fuentes se separaron: `PrepararFijas`
    // decidió que "fijadas" no estaba puesto, dejó `_tiraFijas` en null, y `ModuloFijas`
    // se estrelló contra `_tiraFijas!.Parent`. El hueco quedó vacío y el log dijo un
    // número perfectamente normal.
    //
    // Ese es el fallo de "añado un módulo, dice que sí y no aparece", visto de cerca: no
    // falla el guardado ni la recarga, falla que dos trozos del programa miran a sitios
    // distintos. Con un solo acceso no se pueden separar.
    internal bool ModuloPuesto(string id)
    {
        var z = PanelPrincipal.Zonas;
        return z.Todas().Any(p => p.Widgets.Any(m => m.Equals(id, StringComparison.OrdinalIgnoreCase)));
    }

    // Las zonas que esta isla sabe dibujar, emparejadas con su StackPanel.
    //
    // Los tres StackPanel del XAML siguen ahí a propósito: de ellos cuelgan el ancho
    // de la isla (MedirAncho), la expansión (FijarLados) y la entrada escalonada, y
    // cambiarlos por un contenedor genérico en la misma tanda que el contrato de
    // widget era pedir dos refactores a la vez. Lo que ya es colección es el RECORRIDO:
    // quien rellena no sabe cuántas zonas hay ni cómo se llaman.
    IEnumerable<(string Nombre, Panel Destino)> ZonasDibujables()
    {
        yield return ("Izquierda", Izq);
        yield return ("Centro", Cen);
        yield return ("Derecha", Der);
    }

    // Montar las zonas de cero. Es el ÚNICO sitio desde el que se hace, y tiene que
    // serlo: rellenar no es solo meter widgets en los StackPanel.
    //
    // Aquí estuvo el fallo de las fijadas desapareciendo. El modo edición llamaba a
    // LlenarZonas directamente y se saltaba este preámbulo. Y resulta que importa:
    //
    //   · PrepararFijas() crea una tira NUEVA. Sin ella se reutiliza la de antes, y
    //     como el cromo del modo edición la había metido dentro de un Grid, al
    //     remontar ese Grid quedaba huérfano pero SEGUÍA siendo su Parent. La guarda
    //     de ModuloFijas ("puesto más de una vez") lo tomaba por un duplicado y
    //     devolvía un panel vacío: adiós a las apps fijadas.
    //   · _actualizadores.Clear() evita que cada entrada y salida del modo edición
    //     deje otra tanda de refrescos apuntando a elementos que ya no existen.
    //   · _tira y _carrusel a null: si quitas "apps", el carrusel de la vez anterior
    //     ya no está en la isla y hay que olvidarlo.
    // Cuántas veces se han remontado las zonas en toda la vida del proceso.
    //
    // Es EL número detrás de «con la CPU alta y muchas ventanas no se reconstruyen los paneles
    // continuamente». Remontar es tirar los widgets y volver a construirlos, y este es el
    // único sitio desde el que se hace. Con la barra quieta tiene que quedarse parado; si sube
    // solo, algo está remontando en bucle. Lo lee `--vigilia`.
    internal static long RemontajesDeZonas;

    void RemontarZonas()
    {
        RemontajesDeZonas++;
        _actualizadores.Clear();
        _tira = null;
        _carrusel = null;
        PrepararFijas();                    // la tira de las fijadas, antes que el carrusel

        LlenarZonas(PanelPrincipal);

        // Con "fijadas" puesto y "apps" quitado no hay carrusel que refresque los
        // grupos, pero las fijas siguen necesitando que alguien las pinte y les lleve
        // la cuenta de ventanas abiertas
        if (FijasAparte && _tira == null)
        {
            _grupos.Clear();
            RefrescarApps();
            _actualizadores.Add(RefrescarApps);
        }

        ApuntarLoMontado();
    }

    // Una línea por remonte con lo que ha quedado dibujado en cada zona. Cuesta nada y
    // es lo que permite comprobar, leyendo el log, que entrar y salir de edición
    // veinte veces no se come ningún widget.
    //
    // Antes contaba HIJOS, y mentía justo cuando no debía: un widget que revienta al
    // montarse deja un Border colapsado, y un Border colapsado es un hijo más. «Cen=4»
    // con el módulo invisible en pantalla es la forma exacta que tiene este fallo de
    // esconderse —y pasó: hay un NullReferenceException de "fijadas" en el log con su
    // «Cen=3» tan tranquilo al lado—. Ahora dice los IDS, y marca con "!" el que no
    // llegó a montarse.
    void ApuntarLoMontado()
    {
        try
        {
            var partes = ZonasDibujables()
                .Select(z => $"{z.Nombre[..3]}=[{string.Join(",", IdsMontadosEn(z.Destino))}]");
            Log.Info($"zonas montadas ({(_editando ? "edición" : "normal")}): {string.Join(" ", partes)}");
        }
        catch { }
    }

    // ---------- Lo que hay dibujado AHORA, leído del árbol visual ----------
    //
    // Es la mitad que faltaba para poder comprobar que añadir un módulo funciona. El
    // modelo siempre dirá que el módulo está puesto: el modelo es justo lo que acabas
    // de editar. La pregunta es si llegó a la pantalla, y eso solo lo contesta la
    // pantalla.
    //
    // El id viaja en `AutomationProperties.AutomationId`, que no es un campo nuestro
    // inventado: es la propiedad que UIA ya expone, así que de paso un lector de
    // pantalla y cualquier herramienta de accesibilidad saben qué bloque es cuál.
    //
    // ---------- Y mira DENTRO de los grupos ----------
    //
    // Esto recorría solo los hijos directos de la zona. Valía mientras cada módulo colgaba de
    // la zona, y dejó de valer el día que se añadieron los grupos visuales: un módulo
    // agrupado cuelga de la caja de su grupo, no de la zona.
    //
    // El efecto es peor de lo que parece. La pregunta que contesta `--diagmodulos` es «lo
    // añado, ¿sale?», y para **cualquier módulo que caiga en un grupo** —cpu, memoria,
    // batería, bandeja, control, notificaciones: precisamente los que más se agrupan— la
    // respuesta era «no sale» estando en pantalla. Un diagnóstico que miente en el sentido
    // alarmante es el que se acaba ignorando.
    //
    // No saltó antes porque los módulos que el diagnóstico probaba quedaban sueltos.
    internal static List<string> IdsMontadosEn(Panel? destino)
    {
        var res = new List<string>();
        Recoger(destino, res);
        return res;
    }

    // ================= La raya entre grupos =================
    //
    // Un pelo de 1 px, corto y tenue. Las tres cosas importan:
    //
    // - **1 px y del color de los filos**, no del acento: esto no es un dato, es puntuación.
    //   Una raya que se ve más que los iconos que separa deja de separar y empieza a sumar.
    // - **Corta** (16 px, centrada), no de arriba abajo de la barra: una raya a toda altura
    //   parte la isla en cajones y pelea con sus esquinas redondeadas.
    // - **Sin margen propio**: el aire ya lo ponen las cajas de los tramos. Si además lo
    //   pusiera la raya, los grupos se separarían el doble de lo que dice el código que los
    //   separa, que es como se pierde el control del espaciado.
    //
    // En barra vertical se tumba, claro: lo que separa columnas apiladas es una raya
    // horizontal. Sin esto, en vertical salía un palito de pie entre dos módulos.
    UIElement RayaDeGrupo()
    {
        bool deLado = Vertical;
        return new Border
        {
            Width = deLado ? 16 : 1,
            Height = deLado ? 1 : 16,
            Background = PincelSolido(_roles.Borde),
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    // En profundidad y en el orden en que se ven. Un bloque CON id no se sigue hacia dentro:
    // sus hijos son sus piezas, no módulos, y mirar ahí inventaría ids.
    internal static void Recoger(Panel? donde, List<string> res)
    {
        if (donde == null) return;
        foreach (var hijo in donde.Children.OfType<FrameworkElement>())
        {
            var id = System.Windows.Automation.AutomationProperties.GetAutomationId(hijo);
            if (!string.IsNullOrEmpty(id))
            {
                res.Add(FallosDeMontaje.Contains(id) ? id + "!" : id);
                continue;
            }
            // Sin id: o es la caja de un grupo, o el borde que la envuelve. Se atraviesan los
            // dos, que es exactamente lo que hay entre la zona y un módulo agrupado.
            if (hijo is Panel p) Recoger(p, res);
            else if (hijo is Border b && b.Child is Panel dentro) Recoger(dentro, res);
        }
    }

    // Los ids que han reventado en el último remonte. Se vacía al empezar cada uno: un
    // fallo arreglado tiene que dejar de aparecer sin reiniciar la barra.
    internal static readonly HashSet<string> FallosDeMontaje =
        new(StringComparer.OrdinalIgnoreCase);

    void LlenarZonas(PanelNube panel)
    {
        AsegurarWidgetsRegistrados();
        MarcarZonas(_editando);

        var destinos = ZonasDibujables().ToDictionary(z => z.Nombre, z => z.Destino, StringComparer.OrdinalIgnoreCase);

        foreach (var (nombre, widgets) in panel.Zonas.Todas())
        {
            if (!destinos.TryGetValue(nombre, out var destino))
            {
                Log.Info($"la zona \"{nombre}\" no se dibuja en esta versión: se ignoran sus widgets");
                continue;
            }
            destino.Children.Clear();
            // Qué se dibuja lo decide ZonasWidgets.ADibujar, no este bucle: así la
            // regla es una sola, está escrita en un sitio sin interfaz y se puede
            // comprobar. El índice que se le pasa al cromo es el de la ZONA, no el de
            // esta lista, porque quitar tiene que retirar la instancia exacta.
            var aDibujar = ZonasWidgets.ADibujar(widgets, panel.Zonas.Ocultos, _editando);

            // ---------- Los grupos visuales ----------
            //
            // Los módulos seguidos de la misma categoría van dentro de una pastilla, para que
            // siete iconos en fila dejen de ser una tira indistinguible. Ver `GruposDeBarra`
            // para por qué se agrupa por categoría y no con una lista escrita a mano.
            //
            // En modo EDICIÓN no se agrupa: ahí lo que importa es ver y agarrar cada módulo
            // por separado, y una pastilla alrededor estorba al arrastre.
            // ---------- Una caja POR TRAMO, y en orden ----------
            //
            // Antes solo se creaba caja para los tramos agrupados, y los módulos sueltos se
            // colgaban de la zona en el segundo bucle. Eso tenía un fallo de orden que no se
            // ve leyendo pero sí en pantalla: las cajas se añaden aquí y los sueltos DESPUÉS,
            // así que un módulo suelto colocado **antes** de un grupo acababa detrás de él.
            //
            // En esta barra es el caso normal, no uno raro: reloj (categoría Reloj, suelto)
            // delante de cpu+memoria+bandeja+control (Sistema, agrupados). El usuario pone la
            // hora delante y la barra la dibuja detrás.
            //
            // Con una caja por tramo el orden sale por construcción: lo que se añade a la
            // zona son los tramos, en su orden, y cada módulo cae en la caja que le toca.
            //
            // ---------- Y el mapa va por POSICIÓN, no por id ----------
            //
            // Por id fallaba con los módulos repetibles: dos «separador» son dos tramos
            // distintos y el segundo habría ido a la caja del primero. Antes no llegaba a
            // pasar porque los repetibles son `Util` y `Util` no llevaba caja; ahora todos la
            // llevan, así que la indexación tiene que distinguirlos. `Partir` respeta el
            // orden de entrada, de modo que el tramo k cubre un tramo seguido de posiciones.
            var cajaDeLaPosicion = new Panel?[aDibujar.Count];
            if (!_editando)
            {
                var tramos = GruposDeBarra.Partir(
                    aDibujar.Select(x => x.Id),
                    id => RegistroWidgets.Buscar(id)?.Descriptor.Categoria ?? CategoriaWidget.Util);
                Log.Info($"zona {nombre}: {GruposDeBarra.Linea(tramos)}");

                int pos = 0;
                for (int ti = 0; ti < tramos.Count; ti++)
                {
                    var tr = tramos[ti];

                    // ---------- La raya entre grupos ----------
                    //
                    // Lo que pide la maqueta: «separadores verticales finos entre grupos. No
                    // pastillas de fondo: una línea». Cuándo toca lo decide
                    // `GruposDeBarra.RayaEntre`, que es puro y está probado.
                    // Y solo si las quieres: son lo único que la barra dibuja y que el editor
                    // de módulos no lista, así que tienen que poder apagarse desde algún
                    // sitio. Ver `Barra.SeparadoresDeGrupo`.
                    if (ti > 0 && _cfg.Barra.SeparadoresDeGrupo
                        && GruposDeBarra.RayaEntre(tramos[ti - 1], tr))
                        destino.Children.Add(RayaDeGrupo());

                    var caja = new StackPanel {
                        Orientation = Vertical ? Orientation.Vertical : Orientation.Horizontal,
                    };

                    // ---------- Se agrupa con ESPACIO, no con una caja ----------
                    //
                    // La primera versión le ponía fondo y esquinas a cada grupo. En la barra
                    // eso no se lee como «estos van juntos»: se lee como pegotes grises
                    // detrás de los iconos. El usuario lo vio en cuanto se instaló y lo dijo
                    // sin rodeos: «eso no lo quiero para nada». Tenía razón.
                    //
                    // Agrupar por proximidad es más fuerte y no ensucia: los de un grupo
                    // pegados, y un hueco mayor entre grupos. La raya de arriba remata lo
                    // que el hueco ya dice.
                    destino.Children.Add(new Border
                    {
                        Background = System.Windows.Media.Brushes.Transparent,
                        Margin = Vertical ? new Thickness(0, 5, 0, 5) : new Thickness(5, 0, 5, 0),
                        Child = caja,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    });

                    for (int k = 0; k < tr.Ids.Count && pos < cajaDeLaPosicion.Length; k++)
                        cajaDeLaPosicion[pos++] = caja;
                }
            }

            for (int n = 0; n < aDibujar.Count; n++)
            {
                var (id, oculto) = aDibujar[n];
                int i = widgets.FindIndex(m => m.Equals(id, StringComparison.OrdinalIgnoreCase));
                var w = CrearWidget(id);
                if (_editando)
                {
                    if (oculto) w.Opacity = 0.35;
                    // El índice importa: con un widget repetible (dos separadores) hay
                    // que poder quitar EL QUE TIENES DELANTE, no "uno de los que se
                    // llaman así".
                    w = ConChromeDeEdicion(w, id, nombre, i, oculto);
                }
                // El id se queda pegado al bloque que de verdad entra en la zona —el
                // cromo de edición envuelve, así que tiene que ser DESPUÉS de envolver—.
                // Sin esto no hay forma de preguntarle a la barra qué está dibujando.
                if (w is FrameworkElement fe)
                    System.Windows.Automation.AutomationProperties.SetAutomationId(fe, id);

                // A la caja de su tramo. Editando no hay tramos y van directos a la zona.
                (cajaDeLaPosicion[n] ?? destino).Children.Add(w);
            }

            // Y al final de cada zona, el "+" para añadir ahí mismo. Solo editando.
            if (_editando) destino.Children.Add(BotonAnadirWidgets(nombre));
        }
    }

    // Monta un widget por su id. Nunca lanza: un widget roto deja su hueco vacío y se
    // apunta en el log, pero la isla sigue montándose.
    //
    // Esto antes no hacía falta porque los dieciséis módulos eran nuestros y si uno
    // reventaba era un fallo que había que arreglar. Con widgets que no escribimos
    // nosotros, que uno malo tumbe la barra de tareas del usuario no es una opción.
    UIElement CrearWidget(string id)
    {
        FallosDeMontaje.Remove(id);
        var w = RegistroWidgets.Buscar(id);
        if (w == null) return ModuloDesconocido(id);

        try { return w.Crear(this); }
        catch (Exception ex)
        {
            // Apuntado, no solo registrado: el log se lee cuando ya sospechas. Esto es
            // lo que hace que el propio `zonas montadas` te lo diga en la misma línea
            // en la que antes ponía un número tranquilizador.
            FallosDeMontaje.Add(id);
            Log.Error($"montar el widget \"{id}\" (se deja el hueco vacío)", ex);
            return new Border { Visibility = Visibility.Collapsed };
        }
    }
}
