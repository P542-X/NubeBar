using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace NubeBar;

// Apps fijadas en el carrusel: están siempre, abiertas o no, como en la barra
// de tareas de Windows.
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

        const double ancho = 300, altoFila = 34;
        AbrirPanel(PanelMenu, CrearMenuContextual(opciones.ToArray(), ancho, altoFila),
                   ancho, opciones.Count * altoFila + 12);
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
