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
        R("reloj", "Reloj", "La hora, en horas y minutos", "", CategoriaWidget.Reloj,
          i => i.Etiqueta(() => DateTime.Now.ToString("HH:mm"), animar: true), zona: "Centro");

        R("fecha", "Fecha", "El día de la semana, el día y el mes", "", CategoriaWidget.Reloj,
          i => i.Etiqueta(() => DateTime.Now.ToString("ddd d MMM"), animar: true));

        // ---------- Apps ----------
        R("inicio", "Inicio", "Abre el menú de aplicaciones", "", CategoriaWidget.Apps,
          i => i.Boton(i.Texto("⊞", 16), "Inicio", i.AbrirMenuInicio), zona: "Izquierda");

        R("apps", "Apps abiertas", "El carrusel de ventanas, agrupadas por programa", "", CategoriaWidget.Apps,
          i => i.ModuloApps(), zona: "Centro");

        R("fijadas", "Apps fijadas", "Las apps que has anclado, abiertas o no", "", CategoriaWidget.Apps,
          i => i.ModuloFijas(), zona: "Centro");

        R("escritorios", "Escritorios", "Los escritorios virtuales, con el actual en acento", "", CategoriaWidget.Apps,
          i => i.ModuloEscritorios());

        // ---------- Sistema ----------
        R("cpu", "CPU", "Porcentaje de uso del procesador", "", CategoriaWidget.Sistema,
          i => i.Etiqueta(() => $"CPU {Nativo.Cpu()}%", muestra: "CPU 100%"));

        R("ram", "Memoria", "Porcentaje de memoria en uso", "", CategoriaWidget.Sistema,
          i => i.Etiqueta(() => $"RAM {Nativo.Ram()}%", muestra: "RAM 100%"));

        R("bateria", "Batería", "Carga de la batería; no sale si el equipo no tiene", "", CategoriaWidget.Sistema,
          i => i.Etiqueta(Nativo.Bateria, muestra: "BAT 100%"));

        R("bandeja", "Bandeja", "Los iconos de las apps en segundo plano", "", CategoriaWidget.Sistema,
          i => i.ModuloBandeja());

        R("control", "Centro de control", "Idioma, red, volumen y batería", "", CategoriaWidget.Sistema,
          i => i.ModuloControl());

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
          i => new Border
          {
              Width = 1, Height = 16, Margin = new Thickness(6, 0, 6, 0),
              Background = i._texto, Opacity = 0.3
          }, repetible: true);

        Log.Info($"widgets registrados: {RegistroWidgets.Todos.Count}");
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

    void LlenarZonas(PanelNube panel)
    {
        AsegurarWidgetsRegistrados();

        var destinos = ZonasDibujables().ToDictionary(z => z.Nombre, z => z.Destino, StringComparer.OrdinalIgnoreCase);

        foreach (var (nombre, widgets) in panel.Zonas.Todas())
        {
            if (!destinos.TryGetValue(nombre, out var destino))
            {
                Log.Info($"la zona \"{nombre}\" no se dibuja en esta versión: se ignoran sus widgets");
                continue;
            }
            destino.Children.Clear();
            foreach (var id in widgets) destino.Children.Add(CrearWidget(id));
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
