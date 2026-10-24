using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NubeBar;

// Apps fijadas: están siempre, abiertas o no, como en la barra de tareas de Windows.
//
// Y con el módulo "fijadas" puesto (viene de serie) no viven en el carrusel sino en
// su propia tira, que se ve TAMBIÉN con la isla recogida: así las tienes a un clic
// sin tener que acercar el ratón para desplegarla.
//
// Esa tira va en el CENTRO, pegada al reloj, y no a la izquierda con el carrusel.
// El motivo es la geometría de la isla: crece y se encoge desde el centro, así que
// la zona izquierda se desplaza casi 400 px al expandirse y el icono se escapaba de
// debajo del cursor justo cuando ibas a pulsarlo. La columna del centro es "Auto"
// entre dos estrellas iguales, o sea que está centrada siempre: los iconos fijos no
// se mueven ni un píxel entre recogida y expandida.
public partial class MainWindow : Window
{
    // Lo que se guarda en el config es o una ruta de ejecutable o
    // "shell:AppsFolder\<AppUserModelId>" para las apps de la Store. La clave con
    // la que se agrupan las ventanas abiertas es la ruta a secas o el AUMID a
    // secas, así que hay que quitarle el prefijo para que fijada y abierta sean
    // el MISMO icono y no dos.
    const string PrefijoStore = @"shell:AppsFolder\";

    static string ClaveDeFijada(string fijada) =>
        fijada.StartsWith(PrefijoStore, StringComparison.OrdinalIgnoreCase)
            ? fijada[PrefijoStore.Length..]
            : fijada;

    // Y al revés: de la clave de un grupo abierto a lo que hay que guardar.
    // Un AUMID no es una ruta: no tiene unidad ni barras invertidas.
    static string FijadaDeClave(string clave) =>
        clave.Contains('\\') || clave.Contains(':') ? clave : PrefijoStore + clave;

    List<string> Fijadas => _cfg.Barra.AppsFijadas ?? new List<string>();

    bool EstaFijada(string claveGrupo) =>
        Fijadas.Any(f => ClaveDeFijada(f).Equals(claveGrupo, StringComparison.OrdinalIgnoreCase));

    // ---------- La tira de las fijadas (módulo "fijadas") ----------

    StackPanel? _tiraFijas;          // null si no tienes el módulo puesto

    // ¿Tiene su propia tira, o van en el carrusel como antes?
    bool FijasAparte => _tiraFijas != null;

    // Se crea ANTES de montar los módulos, porque el carrusel ("apps") se construye
    // primero (zona izquierda) y en cuanto se construye ya reparte los botones entre
    // una tira y otra: si la de las fijas no existiera aún, se los quedaría todos.
    void PrepararFijas()
    {
        bool puesto = _cfg.Modulos.Izquierda.Concat(_cfg.Modulos.Centro).Concat(_cfg.Modulos.Derecha)
                          .Any(m => m.Equals("fijadas", StringComparison.OrdinalIgnoreCase));
        if (!puesto) { _tiraFijas = null; return; }

        // Con la barra vertical las fijadas también se apilan, y lo que se fija es su
        // ancho en vez de su alto (ver "El eje de la tira" en MainWindow.Apps.cs).
        bool v = Vertical;
        double grosor = Math.Max(20, _cfg.Barra.Alto - 8);
        _tiraFijas = new StackPanel
        {
            Orientation = v ? Orientation.Vertical : Orientation.Horizontal,
            Width = v ? grosor : double.NaN,
            Height = v ? double.NaN : grosor,
            Margin = v ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 4, 0),
            HorizontalAlignment = v ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            VerticalAlignment = v ? VerticalAlignment.Stretch : VerticalAlignment.Center
        };
    }

    UIElement ModuloFijas()
    {
        if (_tiraFijas == null) PrepararFijas();     // por si acaso: nunca debería pasar
        // Puesto dos veces en el config (en dos zonas), WPF reventaría al añadir el
        // mismo elemento a dos padres. Manda el primero y el segundo sale vacío.
        if (_tiraFijas!.Parent != null)
        {
            Log.Info("el módulo \"fijadas\" está puesto más de una vez: solo cuenta el primero");
            return new StackPanel();
        }
        return _tiraFijas;
    }

    // Los botones se van añadiendo según aparecen los grupos, así que el orden hay que
    // ponerlo aquí: el que tú les has dado en Barra.AppsFijadas.
    void OrdenarFijas()
    {
        if (_tiraFijas == null) return;
        // Sin ninguna fijada, el módulo no debe ni descentrar el reloj
        _tiraFijas.Margin = new Thickness(0, 0, _tiraFijas.Children.Count > 0 ? 4 : 0, 0);
        if (_tiraFijas.Children.Count < 2) return;
        var orden = Fijadas.Select(ClaveDeFijada).ToList();

        int Puesto(Button b)
        {
            var g = _grupos.Values.FirstOrDefault(x => ReferenceEquals(x.Boton, b));
            if (g == null) return int.MaxValue;
            int i = orden.FindIndex(k => k.Equals(g.Exe, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i;
        }

        var estan = _tiraFijas.Children.OfType<Button>().ToList();
        var deben = estan.OrderBy(Puesto).ToList();
        if (estan.SequenceEqual(deben)) return;

        _tiraFijas.Children.Clear();
        foreach (var b in deben) _tiraFijas.Children.Add(b);
    }

    // ---------- Anclar y desanclar ----------
    // Se escribe con ConfigArbol para no perder las claves que NubeBar no conoce,
    // y la recarga en caliente hace el resto: no hace falta repintar a mano.
    void CambiarFijada(string claveGrupo, bool fijar)
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            var lista = arbol.Lista("Barra", "AppsFijadas").ToList();
            var yaEsta = lista.FirstOrDefault(f => ClaveDeFijada(f).Equals(claveGrupo, StringComparison.OrdinalIgnoreCase));

            if (fijar && yaEsta == null) lista.Add(FijadaDeClave(claveGrupo));
            else if (!fijar && yaEsta != null) lista.Remove(yaEsta);
            else return;

            arbol.PonerLista("Barra", "AppsFijadas", lista);
            arbol.Guardar();
            Log.Info($"{(fijar ? "fijada" : "desfijada")} en la nube: {claveGrupo}");
        }
        catch (Exception ex) { Log.Error("fijar o desfijar una app", ex); }
    }

    // ---------- El menú del clic derecho sobre un icono ----------
    void MenuDeGrupo(Grupo g)
    {
        CerrarMiniLista();
        _tiempoMiniLista?.Stop();   // que no vuelva a saltar encima del menú

        bool fijada = EstaFijada(g.Exe);
        string nombre = NombreDeClave(g.Exe);

        var opciones = new List<(string, Action)>
        {
            (fijada ? $"Quitar {nombre} de la nube" : $"Fijar {nombre} en la nube",
             () => CambiarFijada(g.Exe, !fijada))
        };

        // Si está abierta, lo de siempre a mano
        if (g.Ventanas.Count > 0)
            opciones.Add(($"Minimizar {(g.Ventanas.Count > 1 ? $"sus {g.Ventanas.Count} ventanas" : "la ventana")}",
                          () => { foreach (var v in g.Ventanas.ToList()) MinimizarVentana(v); }));
        else
            opciones.Add(($"Abrir {nombre}", () => Fuentes.Lanzar(g.Lanzar)));

        opciones.AddRange(OpcionesDeCierre(g));

        // Mismo trato que la mini lista: este menú cuelga de un icono, así que se
        // dibuja como una tarjeta pegada a él y no como una losa de lado a lado.
        // El ancho lo manda la opción más larga, que aquí lleva el nombre del
        // programa dentro ("Quitar Explorador de archivos de la nube").
        const double altoFila = 34;
        double ancho = Math.Clamp(opciones.Max(o => AnchoDeTexto(o.Item1)) + 48, 240, 460);

        // El ancho deja de decidirlo la isla y lo decide el menú: dentro de la
        // tarjeta, una pila estirada se saldría por los lados y el recorte se comería
        // el principio de cada texto. El margen de la izquierda lo pone el recorte.
        var menu = CrearMenuContextual(opciones.ToArray(), ancho, altoFila);
        menu.Width = ancho;
        menu.HorizontalAlignment = HorizontalAlignment.Left;

        AbrirPanel(PanelMenu, menu, ancho, opciones.Count * altoFila + 12, ancla: g.Boton);
    }

    static string NombreDeClave(string clave)
    {
        try
        {
            if (clave.Contains('\\') || clave.Contains(':'))
            {
                var n = Path.GetFileNameWithoutExtension(clave);
                if (n.Length > 0) return n;
            }
            // AUMID: "Microsoft.WindowsStore_8wekyb3d8bbwe!App" -> "WindowsStore"
            var trozo = clave.Split('!')[0].Split('_')[0];
            int punto = trozo.LastIndexOf('.');
            if (punto >= 0 && punto < trozo.Length - 1) trozo = trozo[(punto + 1)..];
            return trozo.Length > 0 ? trozo : clave;
        }
        catch { return clave; }
    }
}
