using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NubeBar;

// El runner de `--diagmodulos`. Ver DiagModulos.cs para qué comprueba y por qué.
public partial class MainWindow
{
    // Lo que tarda en recorrerse el camino de vuelta: el guardado atómico, el aviso del
    // FileSystemWatcher, los 250 ms de antirrebote de la recarga y el remonte. Con margen:
    // un diagnóstico que mide demasiado pronto inventa fallos, y un fallo inventado cuesta
    // más caro que esperar medio segundo de más.
    static readonly TimeSpan LoQueTardaEnVolver = TimeSpan.FromMilliseconds(1400);

    int _fallosMod;

    async Task CorrerDiagnosticoModulos()
    {
        try
        {
            Apunta("=== MÓDULOS: añadir uno y comprobar que SALE ===");
            Apunta($"ejecutable: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"arenero:    {DiagModulos.Arenero}");
            Apunta($"tu config:  {Config.RutaDelUsuario}");
            Apunta($"            huella al entrar: {DiagModulos.HuellaDeEntrada}");
            Apunta($"configuración en solo lectura={Config.SoloLectura} " +
                   $"(y aun así el arenero SÍ se puede escribir: por eso se reproduce)");
            Apunta("");

            await Task.Delay(4000);     // que la barra termine de asentarse

            foreach (var (modo, posicion, como) in new[]
            {
                ("barra", "arriba", "HORIZONTAL"),
                ("barra", "izquierda", "VERTICAL"),
            })
            {
                Apunta("");
                Apunta($"──────── {como} ({modo}/{posicion}) ────────");
                await PonerOrientacionDeVerdad(modo, posicion);
                Apunta($"   vertical={Vertical}  ·  {LineaDeZonas()}");

                await LaCadenaCompletaDe("escritorios", como);
                await TodoElCatalogoUnaVez(como);
            }

            await PulsandoLosBotonesDeVerdad();

            // ---------- Y tu fichero, intacto ----------
            Apunta("");
            string ahora = DiagModulos.Huella(Config.RutaDelUsuario);
            bool igual = ahora == DiagModulos.HuellaDeEntrada;
            Apunta($"tu config al salir: {ahora}");
            Apunta(igual
                ? "   igual que al entrar: el diagnóstico no ha tocado tu configuración."
                : "   ¡OJO! HA CAMBIADO. Eso es un fallo del diagnóstico, no un resultado.");
            if (!igual) _fallosMod++;

            Apunta("");
            Apunta(_fallosMod == 0
                ? "[MOD] todo lo que se añadió, se movió, se ocultó y se quitó llegó a la pantalla."
                : $"[MOD] ¡OJO! {_fallosMod} fallo(s): hay operaciones que el modelo acepta y la " +
                  "pantalla no refleja.");
            Apunta("[MOD] no se ha movido ninguna ventana.");
        }
        catch (Exception ex)
        {
            Apunta($"[MOD] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico de módulos", ex);
            _fallosMod++;
        }
        finally
        {
            DiagModulos.RecogerElArenero();
            Application.Current.Shutdown(_fallosMod == 0 ? 0 : 5);
        }
    }

    // ================= La cadena completa, para un módulo =================
    //
    // Añadir, mover entre zonas, mover dentro de la zona, ocultar, mostrar y quitar. Cada
    // paso por el camino de verdad y comprobado contra el árbol visual.
    async Task LaCadenaCompletaDe(string id, string como)
    {
        Apunta("");
        Apunta($"[MOD] {como} · la cadena completa de «{id}»");

        // De partida no tiene que estar puesto: si lo estuviera, lo que se mediría después
        // no sería "lo han añadido" sino "ya estaba".
        await Quitar(id);
        Comprobar($"de partida «{id}» no está en ninguna zona", !EstaEnPantalla(id));

        // ---------- 1. Añadir ----------
        await Anadir(id, "Derecha");
        Comprobar($"tras añadirlo, «{id}» se DIBUJA en Derecha",
                  DondeSeDibuja(id) == "Derecha", DondeSeDibuja(id));
        Comprobar($"y es el último de Derecha, que es donde se soltó",
                  IdsDe("Derecha").LastOrDefault() == id, string.Join(",", IdsDe("Derecha")));

        // ---------- 2. Mover a otra zona ----------
        await MoverEntreZonas(id, "Derecha", "Izquierda");
        Comprobar($"movido a Izquierda, «{id}» se dibuja allí",
                  DondeSeDibuja(id) == "Izquierda", DondeSeDibuja(id));
        Comprobar($"y ya NO se dibuja en Derecha", !IdsDe("Derecha").Contains(id),
                  string.Join(",", IdsDe("Derecha")));

        // ---------- 3. Mover dentro de la zona ----------
        //
        // A la primera posición. Es el caso que una comprobación por "¿está?" deja pasar
        // entera: el módulo sigue estando, y lo que falla es el ORDEN.
        var antes = IdsDe("Izquierda");
        if (antes.Count >= 2)
        {
            await MoverDentro("Izquierda", antes.IndexOf(id), 0);
            Comprobar($"movido al principio de Izquierda, «{id}» sale el primero",
                      IdsDe("Izquierda").FirstOrDefault() == id,
                      string.Join(",", IdsDe("Izquierda")));
        }

        // ---------- 4. Ocultar y mostrar ----------
        //
        // Ocultar no quita: conserva el sitio. Así que al volver tiene que reaparecer DONDE
        // estaba, no al final — que es la forma barata de "funcionar" y la que se nota.
        int sitio = IdsDe("Izquierda").IndexOf(id);
        await Editar(z => { z.Ocultar(id); return true; });
        Comprobar($"oculto, «{id}» no se dibuja", !EstaEnPantalla(id));

        await Editar(z => { z.Mostrar(id); return true; });
        Comprobar($"al mostrarlo vuelve a su sitio ({sitio}), no al final",
                  IdsDe("Izquierda").IndexOf(id) == sitio,
                  $"está en {IdsDe("Izquierda").IndexOf(id)}");

        // ---------- 5. Sobrevivir a releer el fichero ----------
        await RecargarDelTodo();
        Comprobar($"tras releer el config entero, «{id}» sigue dibujado",
                  EstaEnPantalla(id), LineaDeZonas());

        // ---------- 6. Quitar ----------
        await Quitar(id);
        Comprobar($"quitado, «{id}» desaparece de las tres zonas", !EstaEnPantalla(id),
                  LineaDeZonas());
    }

    // ================= Todo el catálogo, una vez =================
    //
    // Añadir y quitar cada id que el registro conoce. No comprueba la cadena entera —eso
    // tardaría de más— pero sí lo que de verdad se pidió: que ningún módulo del catálogo
    // diga que sí y luego no salga, ni en horizontal ni en vertical.
    //
    // Los que YA están puestos se quitan primero y se vuelven a añadir. La primera versión
    // se los saltaba, y se saltaba justo a los nueve más usados —reloj, apps, bandeja,
    // control…—. Y resulta que el fallo que se persigue solo aparece en el SEGUNDO montaje:
    // un módulo que se añade por primera vez en la vida del proceso nunca tiene una huella
    // vieja que lo deje vacío. Saltarse los que ya están era saltarse el caso.
    async Task TodoElCatalogoUnaVez(string como)
    {
        Apunta("");
        Apunta($"[MOD] {como} · el catálogo entero, uno a uno");

        foreach (var id in RegistroWidgets.Ids.ToList())
        {
            bool yaEstaba = EstaEnPantalla(id);
            if (yaEstaba)
            {
                await Quitar(id);
                if (EstaEnPantalla(id))
                {
                    Comprobar($"«{id}» se puede quitar para probarlo", false, LineaDeZonas());
                    continue;
                }
            }

            await Anadir(id, "Derecha");
            string donde = DondeSeDibuja(id);
            bool sale = donde == "Derecha";

            Comprobar($"«{id}» se dibuja al añadirlo{(yaEstaba ? " (segunda vez)" : "")}",
                      sale, donde);

            // Que esté en el árbol no basta: un widget que revienta deja un Border
            // colapsado, y un Border colapsado ES un hijo. Si mide cero, en pantalla no hay
            // nada, por mucho que el modelo diga que el módulo está puesto.
            if (sale)
            {
                string? avisado = RegistroWidgets.Buscar(id)?.Descriptor.PuedeNoVerse;
                if (TieneTamano(id))
                    Apunta($"   ok   «{id}» ocupa sitio en pantalla");
                else if (avisado != null)
                    // Anunciado por el propio widget: no es un fallo, pero se dice. Un hueco
                    // esperado que nadie nombra es indistinguible de uno roto.
                    Apunta($"   --   «{id}» no se ve, y lo avisa: {avisado}  ({MedidaDe(id)})");
                else
                    Comprobar($"«{id}» ocupa sitio en pantalla (no es un hueco vacío)", false,
                              MedidaDe(id));
            }

            await Quitar(id);
            Comprobar($"«{id}» desaparece al quitarlo", !EstaEnPantalla(id));

            // Y se devuelve a su sitio, para que el siguiente se mida sobre la misma barra
            // con la que empezó este.
            if (yaEstaba) await Anadir(id, "Derecha");
        }
    }

    // ================= Las operaciones, por el camino de verdad =================
    //
    // Todas pasan por `EditarNube`, que es EXACTAMENTE lo que usa la barra de edición: se
    // abre el árbol del config, se aplica la operación y se guarda. Llamar a `ZonasWidgets`
    // a pelo comprobaría el modelo, que es la mitad que nunca falló.

    async Task Editar(Func<ZonasWidgets, bool> operacion)
    {
        EditarNube(operacion);
        await Task.Delay(LoQueTardaEnVolver);
    }

    Task Anadir(string id, string zona)
    {
        var d = RegistroWidgets.Buscar(id)?.Descriptor;
        if (d == null) { Apunta($"   «{id}» no está en el registro"); return Task.CompletedTask; }
        return Editar(z => z.Anadir(d, zona));
    }

    Task Quitar(string id) => Editar(z => z.Quitar(id));

    Task MoverEntreZonas(string id, string de, string a)
    {
        int i = IdsDe(de).IndexOf(id);
        return i < 0 ? Task.CompletedTask : Editar(z => z.Mover(de, i, a, 0));
    }

    Task MoverDentro(string zona, int desde, int hasta) =>
        Editar(z => z.Mover(zona, desde, zona, hasta));

    // La orientación por el camino del usuario: se escribe en el panel del config y se deja
    // que la recarga en caliente derive `Barra.Modo` y gire la barra. Tocar `_cfg` a mano
    // aplicaría la mitad (ver la nota larga en `PonerOrientacion`).
    async Task PonerOrientacionDeVerdad(string modo, string posicion)
    {
        var arbol = ConfigArbol.Cargar();
        arbol.PonerDelPanel("principal", "Modo", modo);
        arbol.PonerDelPanel("principal", "Posicion", posicion);
        arbol.Guardar();
        await Task.Delay(LoQueTardaEnVolver);
    }

    // Releer el config entero y remontar, que es lo que pasa al cerrar y volver a abrir
    // Ajustes. Comprueba que lo editado está en el FICHERO y no solo en la memoria de la
    // barra: un cambio que vive en un objeto vivo se pierde al reiniciar y nadie se entera
    // hasta el día siguiente.
    async Task RecargarDelTodo()
    {
        if (Config.Cargar() is { } nueva) { _cfg = nueva; Aplicar(); }
        await Task.Delay(400);
    }

    // ================= Lo que de verdad hay en pantalla =================

    static readonly string[] Zonas3 = { "Izquierda", "Centro", "Derecha" };

    Panel? PanelDeZona(string zona) => zona switch
    {
        "Izquierda" => Izq,
        "Centro" => Cen,
        _ => Der,
    };

    // Los ids montados en una zona, sin la marca "!" de los que reventaron: para saber el
    // ORDEN hace falta el id limpio.
    List<string> IdsDe(string zona) =>
        IdsMontadosEn(PanelDeZona(zona)).Select(s => s.TrimEnd('!')).ToList();

    string DondeSeDibuja(string id) =>
        Zonas3.FirstOrDefault(z => IdsDe(z).Contains(id)) ?? "(en ninguna)";

    bool EstaEnPantalla(string id) => Zonas3.Any(z => IdsDe(z).Contains(id));

    // Busca EN PROFUNDIDAD, por lo mismo que `IdsMontadosEn`: desde que hay grupos visuales
    // un módulo agrupado cuelga de la caja de su grupo, no de la zona. Mirando solo los hijos
    // directos, `TieneTamano("cpu")` contestaba «no está» de un módulo que se ve en la barra
    // — y esa es justo la medida con la que este diagnóstico decide si algo llegó a pantalla.
    FrameworkElement? BloqueDe(string id) =>
        Zonas3.Select(PanelDeZona).Where(p => p != null)
              .Select(p => BuscarBloque(p!, id))
              .FirstOrDefault(e => e != null);

    static FrameworkElement? BuscarBloque(Panel donde, string id)
    {
        foreach (var hijo in donde.Children.OfType<FrameworkElement>())
        {
            if (string.Equals(System.Windows.Automation.AutomationProperties.GetAutomationId(hijo),
                              id, StringComparison.OrdinalIgnoreCase))
                return hijo;

            var dentro = hijo as Panel ?? (hijo as Border)?.Child as Panel;
            if (dentro != null && BuscarBloque(dentro, id) is { } hallado) return hallado;
        }
        return null;
    }

    bool TieneTamano(string id)
    {
        var e = BloqueDe(id);
        return e != null && e.IsVisible && e.ActualWidth > 0 && e.ActualHeight > 0;
    }

    string MedidaDe(string id)
    {
        var e = BloqueDe(id);
        return e == null ? "(no está)"
             : $"{e.ActualWidth:0}x{e.ActualHeight:0} visible={e.IsVisible}";
    }

    string LineaDeZonas() =>
        string.Join("  ", Zonas3.Select(z => $"{z[..3]}=[{string.Join(",", IdsMontadosEn(PanelDeZona(z)))}]"));

    // ================= Pulsando los botones de Ajustes de verdad =================
    //
    // Todo lo de arriba recorre el camino del MODELO: `Zonas.Anadir`, `Zonas.Mover`… Y está
    // bien, porque es donde vive la lógica. Pero deja sin contestar la pregunta que de verdad
    // se hizo el usuario: **«añado el módulo en Ajustes, pulso Guardar, y no sale en la
    // barra»**. Entre el botón y el modelo hay un tramo que nadie medía, y es exactamente
    // donde vivía el fallo.
    //
    // Así que aquí se abre la ventana de Ajustes de verdad, se busca el botón por su nombre
    // de accesibilidad y se levanta su `Click`. No es inyección de entrada —ver
    // `VentanaAjustes.Pulsar`—: es llamar al manejador del botón, el mismo camino que recorre
    // una pulsación.
    //
    // Y se hace aquí y no en `--capturas` por un motivo que importa: este diagnóstico corre
    // contra un **config de usar y tirar** (`Config.CarpetaDeArena`), así que pulsar
    // «Guardar» escribe en el arenero y no en el fichero del usuario. En cualquier otro sitio
    // esto sería inaceptable.
    async Task PulsandoLosBotonesDeVerdad()
    {
        Apunta("");
        Apunta("──────── pulsando los botones de Ajustes de verdad ────────");
        Apunta("(levantando el Click de WPF, no inyectando entrada: ver VentanaAjustes.Pulsar)");

        VentanaAjustes? aj = null;
        try
        {
            // De partida fuera, para que lo que se mida sea «lo ha añadido el botón».
            await Quitar("escritorios");
            Comprobar("de partida «escritorios» no está puesto", !EstaEnPantalla("escritorios"));

            aj = new VentanaAjustes { ShowInTaskbar = false, Opacity = 0 };
            aj.Show();
            await Task.Delay(1500);
            aj.Ir("modulos");
            await Task.Delay(1200);

            // ---------- El botón del catálogo ----------
            const string nombre = "Añadir Escritorios a Centro";
            var b = aj.BotonLlamado(nombre);
            if (b == null)
            {
                _fallosMod++;
                Apunta($"   FALLA no se encuentra el botón «{nombre}»");
                var hay = aj.NombresDeBotones().Where(n => n.StartsWith("Añadir")).Take(6);
                Apunta($"   los que había: {string.Join(" · ", hay)}");
                return;
            }
            Apunta($"   botón encontrado: «{nombre}»");
            VentanaAjustes.Pulsar(b);
            await Task.Delay(900);

            // ---------- 1. La vista previa en vivo ----------
            //
            // Ajustes escribe a los 120 ms de cada cambio (`Cambio()` → `_arbol.Guardar()`),
            // para que la barra enseñe el resultado mientras decides. Así que aquí ya tiene
            // que verse, sin haber pulsado Guardar.
            await RecargarDelTodo();
            Comprobar("al pulsar el botón del catálogo, «escritorios» ya se DIBUJA en la barra",
                      EstaEnPantalla("escritorios"), LineaDeZonas());
            Comprobar("y en la zona donde se pidió (Centro)",
                      DondeSeDibuja("escritorios") == "Centro", DondeSeDibuja("escritorios"));

            // ---------- 2. Guardar, que es lo que lo hace permanente ----------
            //
            // Y esto es la mitad que importa de verdad: **cerrar sin guardar REVIERTE**
            // (`VentanaAjustes` línea 116). Así que comprobar que el módulo aparece con la
            // ventana abierta no prueba nada sobre si se queda — es exactamente el fallo que
            // tuvieron los separadores, que se guardaban solos y se perdían al cancelar.
            var g = aj.BotonLlamado("Guardar");
            Comprobar("el botón «Guardar» existe y se puede pulsar", g != null);
            if (g == null)
            {
                var hay = aj.NombresDeBotones().Where(n => n.Length < 20).Distinct().Take(10);
                Apunta($"   los que había: {string.Join(" · ", hay)}");
            }
            else
            {
                VentanaAjustes.Pulsar(g);
                await Task.Delay(1200);
            }

            // ---------- 3. Cerrar, y que SIGA ahí ----------
            //
            // Cerrar es donde se decide: con Guardar pulsado tiene que quedarse, y sin él
            // tendría que haberse revertido. Esta es la comprobación que convierte la prueba
            // en una prueba.
            aj.Close();
            aj = null;
            await Task.Delay(1200);
            await RecargarDelTodo();

            Comprobar("tras CERRAR la ventana, «escritorios» sigue dibujado: Guardar lo hizo " +
                      "permanente", EstaEnPantalla("escritorios"), LineaDeZonas());
            Comprobar("y sigue en Centro", DondeSeDibuja("escritorios") == "Centro",
                      DondeSeDibuja("escritorios"));
        }
        catch (Exception ex)
        {
            _fallosMod++;
            Apunta($"   FALLA pulsando los botones REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("pulsar los botones de Ajustes en --diagmodulos", ex);
        }
        finally
        {
            try { aj?.Close(); } catch { }
            await Task.Delay(600);
        }
    }

    void Comprobar(string que, bool bien, string? detalle = null)
    {
        if (bien) { Apunta($"   ok   {que}"); return; }
        _fallosMod++;
        Apunta($"   FALLA {que}" + (detalle == null ? "" : $"  →  {detalle}"));
    }
}
