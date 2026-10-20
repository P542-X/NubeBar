using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// Cerrar apps desde la isla.
//
// Hay dos formas y no son lo mismo:
//  - Cerrar: se le PIDE a la ventana que se cierre, como al pulsar su aspa. La
//    app puede preguntarte si quieres guardar, y debe poder.
//  - Forzar el cierre: se mata el proceso, como "Finalizar tarea" del
//    Administrador de tareas. Lo que no estuviera guardado se pierde.
//
// Por eso las dos piden confirmación, y la de forzar lo dice con todas las
// letras. Ninguna de las dos se ejecuta de un solo clic.
public partial class MainWindow : Window
{
    // Nombre propio y no PanelMenu: el menú contextual se cierra solo cuando el
    // ratón sale de su lista, y esa misma regla cerraba la confirmación recién
    // abierta antes de que pudieras leerla.
    const string PanelConfirmar = "confirmar";

    // ---------- Una app ----------
    void CerrarGrupo(Grupo g, bool forzando)
    {
        var ventanas = g.Ventanas.ToList();
        if (ventanas.Count == 0) return;

        if (!forzando)
        {
            Log.Info($"cerrando {System.IO.Path.GetFileName(g.Exe)} ({ventanas.Count} ventanas)");
            foreach (var h in ventanas) Nativo.CerrarVentana(h);
            DespuesDeCerrar();
            return;
        }

        // Forzar: un proceso puede tener varias ventanas, pero también puede
        // haber varias ventanas de procesos distintos bajo el mismo icono
        // (Chrome, las apps de la Store). Se matan todos los procesos implicados.
        var pids = ventanas.Select(Nativo.PidDe).Where(p => p > 0).Distinct().ToList();
        var fallos = new List<string>();
        foreach (var pid in pids)
            if (Nativo.MatarProceso(pid) is { } porque) fallos.Add(porque);

        if (fallos.Count > 0) Aviso($"No pude forzar el cierre: {fallos[0]}");
        DespuesDeCerrar();
    }

    // ---------- Todas ----------
    void CerrarTodas(bool forzando)
    {
        // Las fijadas que están cerradas no cuentan: no hay nada que cerrar
        var grupos = _grupos.Values.Where(g => g.Ventanas.Count > 0).ToList();
        if (grupos.Count == 0) return;

        Log.Info($"cerrando TODAS las apps ({grupos.Count} programas){(forzando ? " a la fuerza" : "")}");
        foreach (var g in grupos) CerrarGrupo(g, forzando);
    }

    void DespuesDeCerrar() =>
        Dispatcher.InvokeAsync(async () =>
        {
            await System.Threading.Tasks.Task.Delay(500);
            RefrescarApps();
        });

    // ---------- Preguntar antes ----------
    // Cerrar cosas puede perder trabajo, así que nunca va de un clic. El panel
    // de confirmación sale dentro de la isla, con el mismo aire que el resto.
    void Confirmar(string pregunta, string detalle, string queHace, Action accion, bool peligro)
    {
        const double ancho = 340, alto = 128;

        var columna = new StackPanel { Margin = new Thickness(16, 2, 16, 12) };
        columna.Children.Add(new TextBlock
        {
            Text = pregunta, Foreground = _texto, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        columna.Children.Add(new TextBlock
        {
            Text = detalle,
            Foreground = peligro ? Brushes.Tomato : _texto,
            Opacity = peligro ? 1 : 0.6,
            FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var fila = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        fila.Children.Add(Boton(new TextBlock { Text = "Cancelar", Foreground = _texto }, "No hacer nada", CerrarPanel));
        var si = Boton(new TextBlock { Text = queHace, Foreground = peligro ? Brushes.Tomato : _acento },
                       queHace, () => { CerrarPanel(); accion(); });
        fila.Children.Add(si);
        columna.Children.Add(fila);

        AbrirPanel(PanelConfirmar, columna, ancho, alto);
    }

    // Un recado corto dentro de la isla, para cuando algo no se pudo hacer
    void Aviso(string texto)
    {
        var tb = new TextBlock
        {
            Text = texto, Foreground = Brushes.Tomato, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 4, 16, 12)
        };
        AbrirPanel(PanelConfirmar, tb, 340, 64);
        Log.Info("aviso: " + texto);
    }

    // ---------- Lo que se añade a los menús ----------
    IEnumerable<(string, Action)> OpcionesDeCierre(Grupo g)
    {
        if (g.Ventanas.Count == 0) yield break;
        string nombre = NombreDeClave(g.Exe);
        string cuantas = g.Ventanas.Count > 1 ? $"sus {g.Ventanas.Count} ventanas" : "su ventana";

        yield return ($"Cerrar {nombre}", () => Confirmar(
            $"¿Cerrar {nombre}?",
            $"Se le pedirá que cierre {cuantas}. Si tiene algo sin guardar, te lo preguntará.",
            "Cerrar", () => CerrarGrupo(g, false), peligro: false));

        yield return ($"Forzar el cierre de {nombre}", () => Confirmar(
            $"¿Forzar el cierre de {nombre}?",
            "Se mata el proceso como con «Finalizar tarea». Lo que no esté guardado SE PIERDE.",
            "Forzar", () => CerrarGrupo(g, true), peligro: true));
    }

    IEnumerable<(string, Action)> OpcionesDeCerrarTodas()
    {
        int cuantos = _grupos.Values.Count(g => g.Ventanas.Count > 0);
        if (cuantos == 0) yield break;

        yield return ($"Cerrar todas las apps ({cuantos})", () => Confirmar(
            $"¿Cerrar las {cuantos} apps abiertas?",
            "Se les pedirá que cierren. Las que tengan algo sin guardar te lo preguntarán.",
            "Cerrar todas", () => CerrarTodas(false), peligro: false));
    }
}
