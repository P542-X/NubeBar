using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        void R(string id, string nombre, string desc, string glifo, CategoriaWidget cat,
               Func<MainWindow, UIElement> crear, string zona = "Derecha", bool repetible = false)
            => RegistroWidgets.Registrar(new WidgetDeCasa(
                   new DescriptorWidget(id, nombre, desc, glifo, cat, zona, repetible), crear));

        // ---------- Reloj ----------
        // Cada etiqueta dice cómo se escribe APILADA: en una columna de 38 px no cabe
        // "00:05" de una pieza, y partirlo por su sitio es cosa de quien sabe lo que
        // significa. La hora va como en el panel de Plasma: las horas sobre los minutos.
        R("reloj", "Reloj", "La hora, en horas y minutos", "", CategoriaWidget.Reloj,
          i => i.Etiqueta(() => DateTime.Now.ToString("HH:mm"), animar: true,
                          vertical: () => DateTime.Now.ToString("HH") + Salto + DateTime.Now.ToString("mm")),
          zona: "Centro");

        R("fecha", "Fecha", "El día de la semana, el día y el mes", "", CategoriaWidget.Reloj,
          i => i.Etiqueta(() => DateTime.Now.ToString("ddd d MMM"), animar: true,
                          vertical: () => DateTime.Now.ToString("ddd") + Salto + DateTime.Now.ToString("d MMM")));

        // ---------- Apps ----------
        R("inicio", "Inicio", "Abre el menú de aplicaciones", "", CategoriaWidget.Apps,
          i => i.ModuloInicio(), zona: "Izquierda");

        R("apps", "Apps abiertas", "El carrusel de ventanas, agrupadas por programa", "", CategoriaWidget.Apps,
          i => i.ModuloApps(), zona: "Centro");

        R("fijadas", "Apps fijadas", "Las apps que has anclado, abiertas o no", "", CategoriaWidget.Apps,
          i => i.ModuloFijas(), zona: "Centro");

        R("escritorios", "Escritorios", "Los escritorios virtuales, con el actual en acento", "", CategoriaWidget.Apps,
          i => i.ModuloEscritorios());

        // ---------- Sistema ----------
        // Mismo criterio que el reloj: el rótulo sobre la cifra. "CPU 100%" mide 56 px
        // y no cabe en una columna ni subiendo el grosor a 56; partido, cada línea mide
        // menos de 30 y cabe hasta con el grosor al mínimo.
        R("cpu", "CPU", "Porcentaje de uso del procesador", "", CategoriaWidget.Sistema,
          i => i.Etiqueta(() => $"CPU {Nativo.Cpu()}%", muestra: "CPU 100%",
                          vertical: () => "CPU" + Salto + $"{Nativo.Cpu()}%"));

        R("ram", "Memoria", "Porcentaje de memoria en uso", "", CategoriaWidget.Sistema,
          i => i.Etiqueta(() => $"RAM {Nativo.Ram()}%", muestra: "RAM 100%",
                          vertical: () => "RAM" + Salto + $"{Nativo.Ram()}%"));

        R("bateria", "Batería", "Carga de la batería; no sale si el equipo no tiene", "", CategoriaWidget.Sistema,
          i => i.Etiqueta(Nativo.Bateria, muestra: "BAT 100%",
                          vertical: () => EnDosLineas(Nativo.Bateria())));

        R("bandeja", "Bandeja", "Los iconos de las apps en segundo plano", "", CategoriaWidget.Sistema,
          i => i.ModuloBandeja());

        R("control", "Centro de control", "Idioma, red, volumen y batería", "", CategoriaWidget.Sistema,
          i => i.ModuloControl());

        R("notificaciones", "Notificaciones", "La campana, con las que no has leído y su historial",
          "", CategoriaWidget.Sistema, i => i.ModuloNotificaciones());

        // ---------- Medios ----------
        R("musica", "Música", "Carátula y barras de lo que suena", "", CategoriaWidget.Medios,
          i => new StackPanel
          {
              Orientation = Orientation.Horizontal,
              Children = { i.ModuloCaratula(), i.ModuloBarras() }
          });

        R("musica-caratula", "Música: carátula", "Solo la carátula del disco", "", CategoriaWidget.Medios,
          i => i.ModuloCaratula());

        R("musica-barras", "Música: barras", "Solo las barras que bailan con el sonido", "", CategoriaWidget.Medios,
          i => i.ModuloBarras());

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
            Background = _borde,
            // Una línea de 1 px entre píxeles sale gris y borrosa con DPI no entero
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
    }

    // Proporción, no número mágico: 16 px con la barra de 38 (lo de siempre), y de ahí
    // escala. El mínimo es para que no desaparezca con una barra muy fina; el máximo,
    // para que siempre quepa con holgura dentro de la fila.
    internal static double AltoSeparador(double altoBarra)
    {
        double alto = Math.Round(altoBarra * 0.42);
        return Math.Clamp(alto, 8, Math.Max(8, altoBarra - 8));
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
    void RemontarZonas()
    {
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
    void ApuntarLoMontado()
    {
        try
        {
            var partes = ZonasDibujables()
                .Select(z => $"{z.Nombre[..3]}={z.Destino?.Children.Count ?? 0}");
            Log.Info($"zonas montadas ({(_editando ? "edición" : "normal")}): {string.Join(" ", partes)}");
        }
        catch { }
    }

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
            foreach (var (id, oculto) in aDibujar)
            {
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
                destino.Children.Add(w);
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
        var w = RegistroWidgets.Buscar(id);
        if (w == null) return ModuloDesconocido(id);

        try { return w.Crear(this); }
        catch (Exception ex)
        {
            Log.Error($"montar el widget \"{id}\" (se deja el hueco vacío)", ex);
            return new Border { Visibility = Visibility.Collapsed };
        }
    }
}
