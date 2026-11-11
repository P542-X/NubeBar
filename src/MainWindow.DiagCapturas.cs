using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NubeBar;

// El runner de `--capturas`. Ver DiagCapturas.cs para lo que hace y lo que no.
public partial class MainWindow
{
    readonly List<string> _fotosHechas = new();

    // ---------- Girar la barra, en memoria, por los dos caminos ----------
    //
    // `PanelPrincipal` devuelve el panel de la lista si lo hay y, si no, uno **sintetizado**
    // a partir de `_cfg.Barra`. Escribir en el sintetizado no hace nada —es un objeto nuevo
    // cada vez— así que hay que tocar el que de verdad se lee. Y escribir `_cfg.Paneles[0]`
    // a secas revienta con la lista vacía, que es el caso de una configuración antigua.
    (string Modo, string Posicion) OrientacionDeAhora()
    {
        var p = _cfg.Paneles.FirstOrDefault(x => x.Id == "principal");
        return p != null ? (p.Modo, p.Posicion) : (_cfg.Barra.Modo, _cfg.Barra.Posicion);
    }

    void PonerOrientacion(string modo, string posicion)
    {
        var p = _cfg.Paneles.FirstOrDefault(x => x.Id == "principal");
        if (p != null) { p.Modo = modo; p.Posicion = posicion; }

        // Y TAMBIÉN en `Barra`, porque `Barra.Modo` no es otra opción: es un **derivado** del
        // panel que `Config.Cargar` recalcula al leer el fichero (ver "ahora al revés que
        // antes: Barra se deriva del panel"). Cambiar el panel en memoria no dispara esa
        // derivación, así que hay que mantener la pareja a mano.
        //
        // Esto estuvo mal y casi lo cuento como un fallo del producto: con solo el panel
        // cambiado, `Vertical` —que lee el panel— sí giraba la barra, pero `EsIsla` —que lee
        // `Barra.Modo`— seguía diciendo «isla», y `modo=barra` en horizontal salía como una
        // isla centrada. El producto no tiene ese problema: cuando el usuario lo cambia en
        // Ajustes se guarda y se recarga, y al recargar se deriva. Era el diagnóstico el que
        // aplicaba la mitad.
        _cfg.Barra.Modo = modo;
        _cfg.Barra.Posicion = posicion;
    }

    // ---------- La cadena de capas que hay encima de una tarjeta ----------
    //
    // Las tarjetas de la vista general salían mucho más claras con la barra vertical que con
    // la isla horizontal, y los pinceles decían que no deberían. Cuando el pincel y el píxel
    // no coinciden, lo que falta por mirar es lo que hay EN MEDIO: una capa opaca por el
    // camino tapa el fondo del panel y todo lo semitransparente de encima cambia de color.
    //
    // Esto imprime esa cadena de verdad, desde el marco de la tarjeta hasta la raíz, con
    // cualquier fondo sólido y cualquier opacidad que se encuentre. Se llama en las dos
    // orientaciones a propósito: lo que explica un fallo así no es una lista, es la
    // DIFERENCIA entre las dos listas.
    string? _cadenaHorizontal;

    // ================= Ajustes encogida al mínimo =================
    //
    // Estaba en la lista de pruebas manuales: «con la ventana de Ajustes encogida al mínimo,
    // y con el DPI al 150 %». El DPI no lo puedo cambiar —es tu configuración de pantalla—,
    // pero encoger la ventana sí, y es la mitad que de verdad rompe: `MinWidth = 720` es una
    // promesa de que a ese ancho todo sigue cabiendo, y nadie la había comprobado.
    //
    // Lo que se mide es si algo se sale de la ventana. No «si se ve apretado», que es
    // opinión: si un control queda fuera del área visible y no hay forma de alcanzarlo, eso
    // es un botón que no existe para quien tiene la ventana pequeña.
    async System.Threading.Tasks.Task AjustesAlMinimo(VentanaAjustes ajustes)
    {
        Apunta("--- Ajustes encogida a su tamaño mínimo ---");
        double anchoAntes = ajustes.Width, altoAntes = ajustes.Height;
        try
        {
            ajustes.Width = ajustes.MinWidth;
            ajustes.Height = ajustes.MinHeight;
            await System.Threading.Tasks.Task.Delay(700);
            Apunta($"[MIN] {ajustes.Width:0}x{ajustes.Height:0} (el mínimo declarado)");

            int malas = 0;
            foreach (var pag in VentanaAjustes.Secciones)
            {
                ajustes.Ir(pag);
                await System.Threading.Tasks.Task.Delay(400);

                if (ajustes.Content is not FrameworkElement raiz) continue;
                var r = ApuntarSiAlgoSeSale("ajustes/" + pag, raiz);
                if (!r.Bien) { malas++; Apunta($"[MIN] FALLA {pag}: {r}"); }
            }

            Vale2(malas == 0,
                  $"las {VentanaAjustes.Secciones.Length} páginas caben con la ventana en su " +
                  $"mínimo ({ajustes.MinWidth:0}x{ajustes.MinHeight:0})");

            await FotoDe(ajustes, "23g-ajustes-minimo",
                         "Ajustes en su tamaño mínimo: que no se salga nada");
        }
        catch (Exception ex)
        {
            Apunta($"[MIN] REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("medir Ajustes en su mínimo", ex);
        }
        finally
        {
            // Devuelta a como estaba: las capturas que vienen después son de la ventana
            // normal, y dejarla encogida las cambiaría todas sin que nadie lo hubiera pedido.
            ajustes.Width = anchoAntes;
            ajustes.Height = altoAntes;
            await System.Threading.Tasks.Task.Delay(500);
            Apunta($"[MIN] devuelta a {anchoAntes:0}x{altoAntes:0}");
        }
    }

    void ApuntarCadenaDeLaTarjeta(string donde)
    {
        if (_tarjetas.Count == 0) { Apunta($"[CAP] {donde}: no hay tarjetas que mirar"); return; }

        var t = _tarjetas.Values.First();
        Apunta($"[CAP] {donde} · tarjeta: marco={(t.Marco.Background as SolidColorBrush)?.Color} " +
               $"hueco={(t.Hueco.Background as SolidColorBrush)?.Color} " +
               $"opacidad marco={t.Marco.Opacity} hueco={t.Hueco.Opacity}");

        var sube = new List<string>();
        DependencyObject? n = t.Marco;
        while (n != null && sube.Count < 16)
        {
            string? fondo = n switch
            {
                Border b when b.Background is SolidColorBrush s => s.Color.ToString(),
                Panel p when p.Background is SolidColorBrush s => s.Color.ToString(),
                Control c when c.Background is SolidColorBrush s => s.Color.ToString(),
                _ => null,
            };
            double op = n is UIElement u ? u.Opacity : 1;
            if (fondo != null || op < 1)
                sube.Add($"{n.GetType().Name}[{fondo ?? "—"}{(op < 1 ? $",op={op:0.##}" : "")}]");
            n = VisualTreeHelper.GetParent(n);
        }
        var cadena = string.Join(" < ", sube);
        Apunta($"[CAP] {donde} · de la tarjeta hacia arriba: {cadena}");

        // ---------- Y la comparación, que es lo que de verdad vale ----------
        //
        // Una cadena suelta no dice nada; lo que delató el fallo fue que las dos no coincidían.
        // Así que se comparan solas.
        //
        // Se compara hasta la rejilla incluida, que es **lo que vive dentro del panel**. De
        // ahí hacia arriba están los recipientes —la isla en un caso, la tarjeta flotante en
        // el otro— y esos sí son distintos a propósito: son dos ventanas.
        if (donde == "horizontal") { _cadenaHorizontal = Dentro(sube); return; }

        if (_cadenaHorizontal is { } antes)
            Apunta(Dentro(sube) == antes
                ? "[CAP] las capas sobre la tarjeta son IGUALES en las dos orientaciones"
                : "[CAP] ¡OJO! las capas sobre la tarjeta NO coinciden:\n" +
                  $"        horizontal: {antes}\n" +
                  $"        vertical:   {Dentro(sube)}");

        static string Dentro(List<string> c)
        {
            int i = c.FindIndex(x => x.StartsWith("UniformGrid", StringComparison.Ordinal));
            return string.Join(" < ", i < 0 ? c : c.Take(i + 1));
        }
    }

    // ================= Un tema CLARO =================
    //
    // El punto de D6 que no se podía cerrar con los temas de serie: **los siete son oscuros**.
    // Azul y coral sobre un fondo claro es otra rama de `Roles` —la que oscurece la tinta para
    // que se lea— y nadie la había visto nunca; en lógica está probada, en pantalla no.
    //
    // Así que el tema claro se inventa aquí. Hay que decirlo tal cual: **no es un tema tuyo**,
    // es uno construido para la foto, con la paleta clara del documento (`#F4F7FA` de fondo y
    // `#172033` de texto, que son justo los dos colores que el documento da para las
    // superficies claras). Que funcione con éste no demuestra que funcione con el tuyo; lo que
    // demuestra es que la rama clara dibuja y con cuánto contraste.
    //
    // Y por eso lo importante de este bloque no son las fotos, son los NÚMEROS: las mismas
    // medidas que hace `--diagd6`, pero sobre el fondo claro.
    // Ya no se inventa: es **Oficina**, el claro sobrio de los nueve temas de serie. Lo que
    // decía el párrafo de arriba sigue valiendo —que funcione con éste no demuestra que
    // funcione con el tuyo— pero al menos ahora es un tema que existe y que alguien puede
    // elegir, no una paleta que solo vive en esta función.
    const string ClaroParaLaFoto = "oficina";

    async System.Threading.Tasks.Task ConTemaClaro()
    {
        // El tema de ahora, copiado por JSON. Restaurar campo a campo es la forma de
        // olvidarse de uno el día que se añada otro rol.
        var guardado = System.Text.Json.JsonSerializer.Serialize(_cfg.Tema);
        try
        {
            _cfg.Tema.Id = ClaroParaLaFoto;
            _cfg.Tema.Colores = new Dictionary<string, string>();   // sin tus retoques: el tema limpio
            Aplicar();
            await System.Threading.Tasks.Task.Delay(2000);

            var azul = HyprPinta.Azul(_roles);
            var coral = HyprPinta.Coral(_roles);
            Apunta($"[CAP] TEMA CLARO (inventado para la foto): fondo={_roles.FondoOpaco} " +
                   $"superficie={_roles.SuperficieSolida}");
            Apunta($"  azul contra el fondo  {Contraste.Entre(azul, _roles.FondoOpaco):0.00}:1");
            Apunta($"  coral contra el fondo {Contraste.Entre(coral, _roles.FondoOpaco):0.00}:1");
            Apunta($"  azul como tinta sobre superficie  " +
                   $"{Contraste.Entre(HyprPinta.AzulTinta(_roles, _roles.SuperficieSolida), _roles.SuperficieSolida):0.00}:1" +
                   $"  (mínimo {Roles.MinimoSuperficie})");
            Apunta($"  coral como tinta sobre superficie " +
                   $"{Contraste.Entre(HyprPinta.CoralTinta(_roles, _roles.SuperficieSolida), _roles.SuperficieSolida):0.00}:1");
            foreach (var f in new[] { Colors.White, Colors.Black, Color.FromRgb(0x80, 0x80, 0x80) })
            {
                var filo = HyprPinta.FiloDelDestino(_roles, f);
                double mejor = Math.Max(
                    Contraste.Entre(Color.FromRgb(filo.Exterior.R, filo.Exterior.G, filo.Exterior.B), f),
                    Contraste.Entre(filo.Interior, f));
                Apunta($"  filo sobre {f}: {mejor:0.00}:1 (mínimo {HyprPinta.MinimoFilo})");
            }

            await Foto("19-claro-isla", "Tema CLARO (inventado para la foto): la isla");

            MostrarMensaje(HyprAviso.DelComando("layout.principal", 4, simulado: true));
            await System.Threading.Tasks.Task.Delay(1100);
            await Foto("20-claro-indicador", "Tema claro: el indicador breve, coral sobre claro");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(400);

            var m = _monitor;
            PonerPreviaHypr(new NubeHypr.Rect(
                m.TrabajoX + m.TrabajoAncho * 0.1, m.TrabajoY + m.TrabajoAlto * 0.15,
                m.TrabajoAncho * 0.45, m.TrabajoAlto * 0.5), "Tema claro");
            await System.Threading.Tasks.Task.Delay(900);
            await FotoDe(_previa, "21-claro-previa", "Tema claro: la previsualización del ajuste");
            await FotoEnPantalla(_previa, "21-claro-previa-pantalla",
                                 "Tema claro: la previsualización sobre lo que hay debajo");
            QuitarPreviaHypr();
            await System.Threading.Tasks.Task.Delay(400);

            AlternarVista();
            await System.Threading.Tasks.Task.Delay(2200);
            await Foto("22-claro-vista", "Tema claro: la vista general");
            CerrarVista();
            await System.Threading.Tasks.Task.Delay(600);
        }
        catch (Exception ex)
        {
            Apunta($"[CAP] el tema claro REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("capturar con tema claro", ex);
        }
        finally
        {
            // Tu tema, de vuelta. Pase lo que pase: una captura que te deje la barra blanca
            // sería peor que no haberla hecho.
            try
            {
                var tuyo = System.Text.Json.JsonSerializer.Deserialize<Tema>(guardado);
                if (tuyo != null) _cfg.Tema = tuyo;
                Aplicar();
                await System.Threading.Tasks.Task.Delay(1200);
                Apunta($"[CAP] tu tema devuelto: {_cfg.Tema.Id} fondo={_roles.FondoOpaco} acento={_roles.Acento}");
            }
            catch (Exception ex) { Log.Error("devolver el tema del usuario", ex); }
        }
    }

    // ================= E1: las composiciones de la barra =================
    //
    // Se aplican **en memoria** y se devuelven en el `finally`. Igual que la vuelta a la
    // vertical: si el diagnóstico revienta, la barra no se queda con una distribución que el
    // usuario no ha pedido.
    //
    // Y se revisa antes de aplicar: una composición con un módulo que no existe se rechaza en
    // vez de dejar la barra a medias.
    async System.Threading.Tasks.Task ConLasComposicionesDeE1()
    {
        var habia = Composicion.Apuntar(_cfg.Modulos, _cfg.Barra.Modo, _cfg.Barra.Posicion,
                                        _cfg.Barra.Alto);
        try
        {
            foreach (var c in Composicion.Todas)
            {
                var quejas = Composicion.Revisar(c, id => RegistroWidgets.Buscar(id) != null);
                foreach (var q in quejas)
                    Apunta($"[E1] {(q.Grave ? "GRAVE" : "aviso")} en «{c.Nombre}»: {q.Texto}");

                if (!Composicion.SePuedeAplicar(c, id => RegistroWidgets.Buscar(id) != null))
                {
                    Apunta($"[E1] «{c.Nombre}» NO se aplica: tiene problemas graves");
                    continue;
                }

                Composicion.Aplicar(c, _cfg.Modulos);
                PonerOrientacion(c.Modo, c.Posicion);
                _cfg.Barra.Alto = c.Grosor;
                Aplicar();
                await System.Threading.Tasks.Task.Delay(2200);

                Apunta($"[E1] {c}  ·  EsIsla={EsIsla} Vertical={Vertical} " +
                       $"isla={Isla.ActualWidth:0}x{Isla.ActualHeight:0}");
                await Foto($"30-{c.Nombre}", $"E1: la composición «{c.Nombre}»");
            }
        }
        catch (Exception ex)
        {
            Apunta($"[E1] las composiciones REVENTARON: {ex.GetType().Name}: {ex.Message}");
            Log.Error("capturar las composiciones de E1", ex);
        }
        finally
        {
            Composicion.Devolver(habia, _cfg.Modulos);
            PonerOrientacion(habia.Modo, habia.Posicion);
            _cfg.Barra.Alto = habia.Grosor;
            Aplicar();
            await System.Threading.Tasks.Task.Delay(1500);
            Apunta($"[E1] tu barra, devuelta: {habia.Modo}/{habia.Posicion} {habia.Grosor:0}px · " +
                   $"{string.Join(",", _cfg.Modulos.Izquierda)} | " +
                   $"{string.Join(",", _cfg.Modulos.Centro)} | " +
                   $"{string.Join(",", _cfg.Modulos.Derecha)}");
        }
    }

    // ================= E2: el dock =================
    //
    // El dock viene apagado de serie, así que para fotografiarlo hay que encenderlo **en
    // memoria** y apagarlo al salir. Y se mide lo que E2 promete: que refrescar el dock NO
    // reconstruye las zonas de la barra.
    async System.Threading.Tasks.Task ConElDockDeE2()
    {
        var antes = _cfg.Barra.Dock;
        long remontajesAntes = RemontajesDeZonas;
        try
        {
            _cfg.Barra.Dock = "siempre";

            // Sin apps fijadas el dock solo lleva lo abierto, que ya basta para la foto;
            // pero con un par de fijadas se ve el separador, que es lo propio de E2.
            bool fijadasPuestas = false;
            if (_cfg.Barra.AppsFijadas.Count == 0)
            {
                var candidatas = GestorVentanas.Todas
                    .Select(v => v.Exe).Where(e => e is { Length: > 3 })
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList();
                _cfg.Barra.AppsFijadas.AddRange(candidatas);
                fijadasPuestas = candidatas.Count > 0;
            }

            // E5: el área ANTES de encender el dock, para comparar.
            Apunta($"[E5] sin dock: {LineaDelArea()}");

            _huellaDock = "";
            RefrescarDock();
            await System.Threading.Tasks.Task.Delay(1400);

            Apunta($"[E5] con dock: {LineaDelArea()}");

            Apunta($"[E2] dock en {BordeDelDock} · {_dockFila?.Children.Count ?? 0} elemento(s) · " +
                   $"fijadas de muestra={fijadasPuestas}");

            // Qué cree el dock que tiene. Sirve para distinguir «el indicador no se pinta» de
            // «el elemento no está marcado como abierto», que a tamaño de icono se parecen.
            foreach (var e in Dock.Montar(_cfg.Barra.AppsFijadas, VentanasDeEstaPantalla()))
                Apunta($"   {e}  abierta={e.Abierta} ventanas={e.Ventanas} activa={e.Activa}");
            if (_dockFila?.Children.Count > 0 && _dockFila.Children[0] is FrameworkElement b0)
                Apunta($"   primer botón: {b0.ActualWidth:0}x{b0.ActualHeight:0} " +
                       $"margen={b0.Margin}");
            await FotoEnPantalla(_dock, "31-dock", "E2: el dock, en el borde opuesto a la barra");

            // ---------- Lo que E2 promete ----------
            //
            // Refrescar el dock veinte veces no puede remontar las zonas de la barra ni una.
            // Es la diferencia entre una superficie independiente y un módulo más.
            for (int i = 0; i < 20; i++) RefrescarDock();
            long subieron = RemontajesDeZonas - remontajesAntes;
            Apunta(subieron == 0
                ? "[E2] 20 refrescos del dock: 0 remontajes de las zonas de la barra"
                : $"[E2] ¡OJO! el dock ha remontado las zonas {subieron} vez/veces");

            if (fijadasPuestas)
            {
                _cfg.Barra.AppsFijadas.Clear();
                _huellaDock = "";
                RefrescarDock();
                await System.Threading.Tasks.Task.Delay(800);
            }
        }
        catch (Exception ex)
        {
            Apunta($"[E2] el dock REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("capturar el dock", ex);
        }
        finally
        {
            _cfg.Barra.Dock = antes;
            RefrescarDock();
            await System.Threading.Tasks.Task.Delay(600);
            Apunta($"[E2] el dock vuelve a «{antes}»: visible={_dock?.IsVisible == true}");
        }
    }

    // ================= E3: los paneles laterales =================
    //
    // Lo que de verdad hay que comprobar aquí no es cómo se ven: es que **conviven**. Era el
    // objetivo de E0, y hasta ahora ninguna superficie de contenido lo había ejercitado.
    async System.Threading.Tasks.Task ConLosLateralesDeE3()
    {
        long remontajesAntes = RemontajesDeZonas;

        // ---------- Primero, la PUERTA ----------
        //
        // Hasta ahora esta tanda abría los paneles llamando a `AbrirLateral`, que es lo que
        // hacía el diagnóstico… y lo único que lo hacía en todo el programa. Los paneles no
        // tenían forma de abrirse para el usuario.
        //
        // Ahora la tienen, y se comprueba aquí: se enciende la opción EN MEMORIA y se deja
        // que el pulso de siempre los abra solo. Con «siempre» no hace falta el ratón —que
        // no se puede mover: inyectar entradas está prohibido—, así que lo que se prueba es
        // el cableado, y el gesto del borde lo cubren las pruebas de `Dock.RatonCerca`.
        var lateralesAntes = _cfg.Barra.Laterales;
        try
        {
            _cfg.Barra.Laterales = "siempre";
            Apunta("[E3] opción de laterales en memoria: «siempre» (el fichero no se toca)");
            await System.Threading.Tasks.Task.Delay(1500);     // un pulso de sobra

            bool izqSola = LateralAbierto(Lateral.Lado.Izquierda);
            bool derSola = LateralAbierto(Lateral.Lado.Derecha);
            Apunta($"[E3] se abrieron solos: izquierda={(izqSola ? "sí" : "NO")} " +
                   $"derecha={(derSola ? "sí" : "NO")}");

            _cfg.Barra.Laterales = "nunca";
            await System.Threading.Tasks.Task.Delay(1500);
            bool izqCerrada = !LateralAbierto(Lateral.Lado.Izquierda);
            bool derCerrada = !LateralAbierto(Lateral.Lado.Derecha);
            Apunta($"[E3] y al apagar la opción se cerraron: izquierda={(izqCerrada ? "sí" : "NO")} " +
                   $"derecha={(derCerrada ? "sí" : "NO")}");
        }
        catch (Exception ex) { Log.Error("probar la puerta de los laterales", ex); }
        finally { _cfg.Barra.Laterales = lateralesAntes; }

        try
        {
            AbrirLateral(Lateral.Lado.Izquierda);
            await System.Threading.Tasks.Task.Delay(1200);
            Apunta($"[E3] izquierdo abierto · tablero: {_mesa.Linea()}");
            await FotoEnPantalla(_laterales.GetValueOrDefault(Lateral.Lado.Izquierda),
                                 "32-panel-izquierdo", "E3: el panel izquierdo, de controles");

            // ---------- El segundo, sin cerrar el primero ----------
            // ---------- E4: y la música no se para ----------
            //
            // Es lo que E4 prohíbe explícitamente. Se apunta lo que suena ANTES de abrir y se
            // compara después: el centro multimedia lee la sesión de Windows, no la manda.
            bool sonabaAntes = _musica?.Sonando == true;
            string queSonaba = _musica?.Titulo ?? "(nada)";

            AbrirLateral(Lateral.Lado.Derecha);
            await System.Threading.Tasks.Task.Delay(1200);

            bool sonaDespues = _musica?.Sonando == true;
            Apunta(sonabaAntes
                ? (sonaDespues
                    ? $"[E4] la música sigue sonando tras abrir el panel: «{queSonaba}»"
                    : $"[E4] ¡OJO! abrir el panel ha parado «{queSonaba}»")
                : "[E4] no había nada sonando: la comprobación de la música queda pendiente");

            bool losDos = LateralAbierto(Lateral.Lado.Izquierda) &&
                          LateralAbierto(Lateral.Lado.Derecha);
            Apunta(losDos
                ? "[E3] los DOS paneles abiertos a la vez: E0 cumple lo que prometía"
                : "[E3] ¡OJO! abrir el derecho ha cerrado el izquierdo");

            Apunta($"[E3] tablero con los dos: {_mesa.Linea()}");

            await FotoEnPantalla(_laterales.GetValueOrDefault(Lateral.Lado.Derecha),
                                 "33-panel-derecho", "E4: el panel derecho, contextual");

            // ---------- Y sin tocar la barra ----------
            long subieron = RemontajesDeZonas - remontajesAntes;
            Apunta(subieron == 0
                ? "[E3] abrir los dos paneles: 0 remontajes de las zonas de la barra"
                : $"[E3] ¡OJO! los paneles han remontado la barra {subieron} vez/veces");

            // ---------- Cerrar uno no toca al otro ----------
            CerrarLateral(Lateral.Lado.Izquierda);
            await System.Threading.Tasks.Task.Delay(700);
            Apunta(LateralAbierto(Lateral.Lado.Derecha)
                ? "[E3] cerrar el izquierdo deja el derecho abierto"
                : "[E3] ¡OJO! cerrar uno ha cerrado el otro");
            Apunta($"[E3] tablero tras cerrar el izquierdo: {_mesa.Linea()}");
        }
        catch (Exception ex)
        {
            Apunta($"[E3] los paneles laterales REVENTARON: {ex.GetType().Name}: {ex.Message}");
            Log.Error("capturar los paneles laterales", ex);
        }
        finally
        {
            CerrarLateral(Lateral.Lado.Izquierda);
            CerrarLateral(Lateral.Lado.Derecha);
            await System.Threading.Tasks.Task.Delay(600);
            Apunta($"[E3] los dos cerrados · tablero: {_mesa.Linea()}");
        }

        await LosTresEstilos();
    }

    // ================= Los tres estilos, uno por uno =================
    //
    // El usuario puede elegir la decoración —Normal, Nube, Neón—, y eso significa que hay
    // **tres** aspectos que entregar, no uno. Entregar un estilo sin haberlo visto es
    // exactamente lo que este proyecto lleva toda la sesión intentando no hacer: una prueba
    // dice que las medidas son razonables, no que el resultado se vea bien.
    //
    // Se cambia EN MEMORIA y se devuelve al terminar, como el resto de este diagnóstico: el
    // fichero del usuario no se toca.
    async System.Threading.Tasks.Task LosTresEstilos()
    {
        Apunta("");
        Apunta("--- los nueve temas ---");

        // Esto fotografiaba los tres estilos (Normal, Nube y Neón). Ya no existen: la forma es
        // un valor del tema, así que lo que hay que mirar son los nueve. Y es justo la pantalla
        // «Paneles en cada tema» de la maqueta: el lateral izquierdo, el mismo, con los nueve
        // temas puestos, para ver que cada superficie cambia entera con el tema.
        string antes = _cfg.Tema.Id;
        var misColores = _cfg.Tema.Colores;
        try
        {
            foreach (var t in TemasDeSerie.Lista)
            {
                // El tema limpio, sin los retoques del usuario: lo que se fotografía es el
                // tema, no el tema más lo que tenga puesto quien lanza el diagnóstico.
                _cfg.Tema.Id = t.Id;
                _cfg.Tema.Colores = new Dictionary<string, string>();
                Aplicar();
                await System.Threading.Tasks.Task.Delay(400);

                var f = EstiloDeAhora;
                Apunta($"[TEMA] «{t.Nombre}»: radio {f.RadioCard}/{f.RadioShell} · hueco {f.Hueco} " +
                       $"· dentro {f.Dentro} · filo {f.GrosorFilo}" +
                       (f.Brilla ? $" · halo {f.Resplandor}→{f.ResplandorAlto}" : " · sin halo") +
                       $" · esquinas {t.Esquinas.ToString().ToLowerInvariant()}" +
                       (t.Decoraciones.Count > 0 ? $" · {string.Join(", ", t.Decoraciones)}" : ""));

                // Se vuelve a montar el panel para que tome las medidas nuevas: la forma se
                // lee al construir, no se aplica a lo que ya está dibujado.
                AbrirLateral(Lateral.Lado.Izquierda);
                await System.Threading.Tasks.Task.Delay(1200);
                await FotoEnPantalla(_laterales.GetValueOrDefault(Lateral.Lado.Izquierda),
                                     $"35-tema-{t.Id}",
                                     $"Tema «{t.Nombre}» ({t.Familia}): el panel lateral entero");
                CerrarLateral(Lateral.Lado.Izquierda);
                await System.Threading.Tasks.Task.Delay(400);

                // ---------- Y el Inicio, que es donde viven las decoraciones ----------
                //
                // El lateral enseña los colores y la forma; las partículas, la mascota y los
                // destellos caen DENTRO del menú Inicio. Sin esta foto, las fases 6 y 7 se
                // comprueban mirando el tema equivocado.
                AbrirMenuInicio();
                await System.Threading.Tasks.Task.Delay(1600);   // tiempo a que la lluvia entre
                var part = t.Argumento(Decoraciones.Particulas);
                await Foto($"36-inicio-{t.Id}",
                           $"Tema «{t.Nombre}»: el menú Inicio" +
                           (string.IsNullOrEmpty(part) ? " (sin partículas)" : $" (partículas: {part})"));
                CerrarPanel();
                await System.Threading.Tasks.Task.Delay(400);
            }
        }
        catch (Exception ex)
        {
            Apunta($"[TEMA] REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("fotografiar los nueve temas", ex);
        }
        finally
        {
            _cfg.Tema.Id = antes;
            _cfg.Tema.Colores = misColores;
            Aplicar();
            try { CerrarLateral(Lateral.Lado.Izquierda); } catch { }
            await System.Threading.Tasks.Task.Delay(400);
            Apunta($"[TEMA] tema devuelto a «{antes}» (en memoria; el fichero no se ha tocado)");
        }
    }

    async System.Threading.Tasks.Task CorrerCapturas()
    {
        try
        {
            Directory.CreateDirectory(DiagCapturas.Carpeta);
            Apunta("=== capturas de las superficies ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta($"carpeta: {DiagCapturas.Carpeta}");
            Apunta($"tema puesto: {_cfg.Tema.Id} fondo={_roles.FondoOpaco} acento={_roles.Acento} " +
                   $"alto contraste de Windows={SystemParameters.HighContrast}");
            // Seis segundos, no dos y medio: las dos primeras capturas de la tanda anterior
            // salieron con la isla a medio montar —sin su zona derecha— y parecían un fallo
            // del producto cuando eran una carrera de mi propio diagnóstico.
            await System.Threading.Tasks.Task.Delay(6000);

            // ---------- 1. La isla sola ----------
            await Foto("01-isla", "la isla, tal y como la tienes configurada");

            // ---------- 1b. Y con el indicador de escritorios ----------
            //
            // El módulo "escritorios" no está en las zonas de este usuario, así que el
            // indicador de D6 no sale en su isla. Se añade **solo en memoria** para poder
            // fotografiarlo: sin esto, la pieza más visible de D6 se quedaría sin verificar
            // porque este equipo no la tiene puesta. La configuración no se toca
            // (`Config.SoloLectura`), y al terminar se quita igual.
            bool loPuseYo = false;
            if (!_cfg.Modulos.Derecha.Contains("escritorios") &&
                !_cfg.Modulos.Izquierda.Contains("escritorios") &&
                !_cfg.Modulos.Centro.Contains("escritorios"))
            {
                _cfg.Modulos.Derecha.Insert(0, "escritorios");
                loPuseYo = true;
                Aplicar();
                await System.Threading.Tasks.Task.Delay(1200);
                await Foto("01b-indicador", "la isla con el indicador de escritorios de D6");
            }
            else await Foto("01b-indicador", "la isla: el indicador ya estaba puesto");

            // ---------- 2. Inicio ----------
            //
            // Por el mismo camino que la tecla Windows, igual que hace --diaginicio: una
            // captura de una superficie abierta por una vía que no es la del usuario no
            // prueba lo que el usuario ve.
            ApuntarBoton("--capturas (simula la tecla Windows)");
            AbrirMenuInicio();
            await System.Threading.Tasks.Task.Delay(1400);
            await Foto("02-inicio", "Inicio: saludo, buscador, tarjetas del centro de control");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(600);

            // ---------- 3. Audio ----------
            AbrirDesde(BotonAltavoz);
            await System.Threading.Tasks.Task.Delay(1200);
            await Foto("03-audio", "Audio: deslizador, salidas, mezclador por aplicación");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(500);

            // ---------- 4. Red ----------
            AbrirDesde(BotonRed);
            await System.Threading.Tasks.Task.Delay(1500);
            await Foto("04-red", "Red: la conexión de este equipo (aquí, Ethernet)");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(500);

            // ---------- 5. Batería ----------
            AbrirDesde(BotonBateria);
            await System.Threading.Tasks.Task.Delay(1000);
            await Foto("05-bateria", "Batería (en un equipo de sobremesa puede salir vacío)");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(500);

            // ---------- 6. La vista general ----------
            //
            // La de D6: con el halo de la elegida y el filo de la activa. Es la captura que
            // contesta "¿se distinguen de un vistazo?".
            // Tres segundos y medio, no uno y medio: las fotos de las tarjetas se piden
            // en segundo plano y la primera tanda tardó más que la captura. Una imagen
            // tomada antes de tiempo no enseña un fallo, enseña una carrera.
            AlternarVista();
            await System.Threading.Tasks.Task.Delay(3500);
            await Foto("06-vista", "Vista general: tarjetas, filo de la activa, halo de la elegida");
            ApuntarCadenaDeLaTarjeta("horizontal");

            // Y con la elección movida, para ver las dos marcas a la vez en sitios distintos
            MoverEleccion(HyprVista.Mover.Derecha);
            await System.Threading.Tasks.Task.Delay(700);
            await Foto("07-vista-elegida", "Vista general con la elegida distinta de la activa");
            CerrarVista();
            await System.Threading.Tasks.Task.Delay(600);

            // ---------- 7. La bandeja ----------
            AlternarBandejaExtra();
            await System.Threading.Tasks.Task.Delay(1200);
            await Foto("08-bandeja", "Bandeja: los iconos ocultos");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(500);

            // ---------- 8. Notificaciones ----------
            AbrirNotificaciones();
            await System.Threading.Tasks.Task.Delay(1200);
            await Foto("09-notificaciones", "Centro de notificaciones");
            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(500);

            // ---------- 9. La previsualización del ajuste ----------
            //
            // La de D6, con su filo doble. Se pide con un rectángulo directamente, que es
            // justo para lo que existe `PonerPreviaHypr`, y se fotografía su ventana.
            var m = _monitor;
            var destino = new NubeHypr.Rect(
                m.TrabajoX + m.TrabajoAncho * 0.1, m.TrabajoY + m.TrabajoAlto * 0.15,
                m.TrabajoAncho * 0.45, m.TrabajoAlto * 0.5);
            PonerPreviaHypr(destino, "Mitad izquierda");
            await System.Threading.Tasks.Task.Delay(900);
            await FotoDe(_previa, "10-previa", "Previsualización del ajuste: filo doble y chapa");

            // Y la misma, copiada de la PANTALLA. No es repetirse: el relleno de la
            // previsualización es semitransparente a propósito (alfa 0x30, para que se siga
            // viendo la ventana de debajo), y `PrintWindow` lo dibuja sobre nada. Lo que esa
            // foto enseña es el relleno sobre negro, que no es lo que ve nadie. Sobre la
            // pantalla de verdad sí se ve lo que hay debajo, que es el punto del diseño.
            await FotoEnPantalla(_previa, "10-previa-pantalla",
                                 "La MISMA previsualización sobre lo que hay de verdad debajo");
            QuitarPreviaHypr();
            await System.Threading.Tasks.Task.Delay(400);

            // ---------- 9b. El indicador breve de un comando ----------
            //
            // Las dos caras, porque son dos colores distintos y es justo lo que hay que mirar:
            // azul cuando algo ha pasado, coral cuando hay que leer el motivo. Se le pasan
            // mensajes construidos a mano y no se pulsa ningún atajo: una captura no tiene por
            // qué disparar la lógica de comandos para enseñar cómo se ve el panel.
            MostrarMensaje(HyprAviso.DelComando("layout.principal", 4, simulado: false));
            await System.Threading.Tasks.Task.Delay(900);
            await Foto("10b-indicador-azul", "El indicador breve: un comando que ha hecho algo");

            MostrarMensaje(HyprAviso.DelComando("foco.siguiente", 0, true, "no hay otra ventana"));
            await System.Threading.Tasks.Task.Delay(900);
            await Foto("10c-indicador-coral", "El indicador breve: el motivo cuando no pasa nada");

            MostrarMensaje(HyprAviso.DelEspacio("espacio.siguiente", "2 · Trabajo", null));
            await System.Threading.Tasks.Task.Delay(900);
            await Foto("10d-indicador-espacio", "El indicador breve: el destino, y que es simulación");

            // ---------- 9c. El cruce: texto -> barra -> texto ----------
            //
            // Los dos avisos comparten panel, y esta es la secuencia que fallaba: tras pasar
            // por la barra, el siguiente mensaje creía que su panel seguía puesto y pintaba en
            // un elemento ya desenganchado del árbol visual — no se veía nada. Si la última
            // foto enseña texto, el cruce funciona; si enseña la barra, ha vuelto el fallo.
            //
            // El aviso de brillo solo PINTA: no toca el brillo del monitor. Una captura no
            // tiene por qué cambiarte la pantalla para enseñar un panel.
            AvisoDeBrillo(60);
            await System.Threading.Tasks.Task.Delay(900);
            await Foto("10e-aviso-barra", "El mismo panel con la barra: el aviso de brillo");

            MostrarMensaje(HyprAviso.DelDeshacer(0, true));
            await System.Threading.Tasks.Task.Delay(900);
            await Foto("10f-indicador-tras-barra",
                       "Y texto otra vez DESPUÉS de la barra: el cruce de los dos avisos");

            CerrarPanel();
            await System.Threading.Tasks.Task.Delay(500);

            // ---------- 10. Ajustes, página por página ----------
            //
            // Incluida la nueva de NubeHypr, que es la que nadie ha visto. Se abre con
            // opacidad 0 y se sube justo antes de la foto: así no hay un fogonazo de una
            // ventana grande apareciendo y desapareciendo en pantalla.
            VentanaAjustes? ajustes = null;
            try
            {
                ajustes = new VentanaAjustes { ShowInTaskbar = false, Opacity = 0 };
                ajustes.Show();
                await System.Threading.Tasks.Task.Delay(1500);
                ajustes.Opacity = 1;
                await System.Threading.Tasks.Task.Delay(400);

                // ---------- El editor de reglas, con una regla dentro ----------
                //
                // Sin una regla, la página enseña el vacío y el editor de D9.3 no se ve. Se
                // añade una **a la copia en memoria del editor**, se retrata y se quita: el
                // fichero no se toca (`Config.SoloLectura`) y la ventana se cierra sin
                // guardar.
                // ---------- La guía: los ocho ejemplos, uno por uno ----------
                //
                // No se pulsan ocho botones con el ratón —eso es inyección y está prohibido—:
                // se recorre el mismo camino de código que el clic. Si una tarjeta o un
                // formulario revienta, sale aquí con su número.
                // ---------- La página de módulos ----------
                //
                // La que se rehízo entera: chips con icono, nombre y estado, botones de
                // mover por zona, y el catálogo con un botón por zona.
                //
                // Va fotografiada porque hay dos cosas aquí que ninguna prueba puede
                // contestar: si los veinte iconos son los que dicen ser —siete se acaban de
                // poner, y un glifo que existe en la fuente no es por eso el glifo correcto;
                // ya pasó con un botón de apagado donde tenía que haber un escritorio— y si
                // tres columnas de chips caben sin apretarse.
                // La página nueva de la macrofase E: el interruptor del dock, que hasta
                // ahora no existía en ninguna parte.
                ajustes.Ir("escritorio");
                await System.Threading.Tasks.Task.Delay(900);
                await FotoDe(ajustes, "23e-escritorio",
                             "Ajustes → Escritorio: el dock, por fin con interruptor");

                ajustes.Ir("modulos");
                await System.Threading.Tasks.Task.Delay(1200);
                await FotoDe(ajustes, "23c-modulos", "Ajustes → Módulos: los chips por zona");

                // ---------- Y la de NubeHypr, por tres controles que no existían ----------
                //
                // «Mover ventanas de verdad» es el importante: `NubeHypr.Modo` tenía 175 usos
                // en el producto y ningún control aquí, así que el modo «aplicar» —todo lo
                // que mueve ventanas— solo se alcanzaba editando el `config.json` a mano.
                //
                // Va fotografiada porque lo que hay que mirar no es si cabe —eso se mide—
                // sino si **se entiende**: tres interruptores seguidos que dicen «Activar
                // NubeHypr», «Mover ventanas de verdad» y «Preguntar antes de mover ventanas»
                // tienen que leerse como una escalera y no como tres opciones sueltas, porque
                // el de en medio es el que decide si un programa toca las ventanas de alguien.
                ajustes.Ir("nubehypr");
                await System.Threading.Tasks.Task.Delay(1400);
                await FotoDe(ajustes, "23f-nubehypr",
                             "Ajustes → NubeHypr: el modo, el hueco y la confirmación");

                // ---------- Y lo que una captura NO puede enseñar ----------
                //
                // «Teclado-accesible» se pidió para esta página y se escribió, pero un botón
                // con nombre de accesibilidad y uno sin él se dibujan exactamente igual. Esto
                // lo cuenta.
                // Las TRECE páginas, no solo esta. La primera versión miraba módulos y
                // encontró veinticinco botones sin nombre… que resultaron ser los de la
                // navegación lateral, o sea los de todas las páginas a la vez. Mirando una
                // sola página no se sabe si lo que falla es la página o el marco.
                int sinNombreTotal = 0;
                foreach (var pag in VentanaAjustes.Secciones)
                {
                    ajustes.Ir(pag);
                    await System.Threading.Tasks.Task.Delay(450);
                    var t = ajustes.MirarElTeclado();
                    sinNombreTotal += t.SinNombre;
                    Apunta($"[A11Y] {pag,-12} {t.Botones,3} botones · {t.ConFoco,3} con tabulador · " +
                           $"{t.SinNombre,2} sin nombre" +
                           (t.SinNombre > 0 ? $"  ← p. ej.: {t.Ejemplo}" : ""));
                }
                Vale2(sinNombreTotal == 0,
                      $"ningún botón de Ajustes se queda sin nombre para el lector de pantalla " +
                      $"({sinNombreTotal})");

                await AjustesAlMinimo(ajustes);

                ajustes.Ir("modulos");
                await System.Threading.Tasks.Task.Delay(500);
                ajustes.DesplazarLaPagina(500);
                await System.Threading.Tasks.Task.Delay(500);
                await FotoDe(ajustes, "23d-modulos-disponibles",
                             "Ajustes → Módulos: el catálogo, con un botón por zona");
                ajustes.DesplazarLaPagina(-500);
                await System.Threading.Tasks.Task.Delay(400);

                ajustes.Ir("nubehypr");
                await System.Threading.Tasks.Task.Delay(1200);
                await FotoDe(ajustes, "23-guia", "Ajustes → NubeHypr: «Qué puedes hacer»");

                // ---------- El rótulo del modo de demostración ----------
                //
                // Se retrata aquí aunque esta tanda no corra con `--guia`: el rótulo es un
                // control, y un control que solo existe en un modo que no se fotografía es
                // un control que nadie ha visto nunca. Se pide el elemento y se mira; no se
                // enciende el modo, que cambiaría el resto de la tanda.
                await FotoDeElemento(ajustes.RotuloDeDemostracion(),
                                     "23b-rotulo-demostracion",
                                     "El rótulo del modo de demostración (--guia)");

                ajustes.DesplazarLaPagina(700);
                await System.Threading.Tasks.Task.Delay(600);
                await FotoDe(ajustes, "24-guia-ejemplos", "La guía: tarjetas de ejemplo");

                int fallos = ajustes.EjercitarLaGuiaParaLaCaptura();
                await System.Threading.Tasks.Task.Delay(900);
                Apunta(fallos == 0
                    ? "[CAP] los 8 ejemplos de la guía se usan y se prueban sin reventar"
                    : $"[CAP] ¡OJO! {fallos} ejemplo(s) de la guía han fallado (en el registro)");

                // Varias alturas, porque la página es larga y una sola foto no enseña ni la
                // lista de reglas ni el formulario. Los números salen de mirar: la guía
                // ocupa las ocho tarjetas y las reglas empiezan después.
                foreach (var (y, nombre, que) in new[]
                {
                    (2600.0, "25-guia-usada", "Tras usar los 8 ejemplos: las reglas, apagadas"),
                    (3400.0, "26-reglas-anadidas", "La lista con las ocho, todas apagadas"),
                    (3900.0, "27-formulario", "El formulario abierto, con el aviso de simulación"),
                    (4500.0, "28-formulario-campos", "El formulario: etiquetas y ayudas de cada campo"),
                    (5100.0, "29-formulario-previa", "La vista previa en castellano y «Probar regla»"),
                })
                {
                    ajustes.DesplazarLaPagina(y);
                    await System.Threading.Tasks.Task.Delay(600);
                    await FotoDe(ajustes, nombre, que);
                }

                ajustes.ReglaDeMuestraParaLaCaptura();
                await System.Threading.Tasks.Task.Delay(900);
                ajustes.DesplazarLaPagina(420);
                await System.Threading.Tasks.Task.Delay(500);
                await FotoDe(ajustes, "12-editor-reglas",
                             "Ajustes → NubeHypr: el editor de una regla (D9.3)");

                foreach (var (id, que) in new[]
                {
                    ("nubehypr", "Ajustes → NubeHypr: estado, reglas y atajos (la nueva de D6)"),
                    ("atajos", "Ajustes → Atajos: el editor de los 19 comandos"),
                    ("apariencia", "Ajustes → Apariencia"),
                    ("colores", "Ajustes → Colores: los roles del tema"),
                    ("modulos", "Ajustes → Módulos"),
                })
                {
                    ajustes.Ir(id);
                    await System.Threading.Tasks.Task.Delay(900);
                    await FotoDe(ajustes, "11-ajustes-" + id, que);
                }
            }
            catch (Exception ex)
            {
                Apunta($"[CAP] Ajustes REVENTÓ: {ex.GetType().Name}: {ex.Message}");
                Log.Error("capturar Ajustes", ex);
            }
            finally
            {
                try { ajustes?.Close(); } catch { }
                await System.Threading.Tasks.Task.Delay(500);
            }

            // ---------- 11. La barra VERTICAL ----------
            //
            // La parte de D6 que estaba probada en lógica y no había visto nadie. En vertical
            // los paneles no se despliegan dentro de la isla —no tiene hacia dónde crecer—:
            // salen **flotando al lado** por otro camino (`AbrirPanelFlotante`), y ese camino
            // no se había fotografiado nunca con el indicador dentro.
            //
            // Se cambia **solo en memoria**, igual que el módulo de arriba, y se devuelve en
            // el `finally`: si una excepción se lleva el diagnóstico por delante, la barra no
            // se queda de lado. El fichero no se toca (`Config.SoloLectura`).
            var (modoAntes, posicionAntes) = OrientacionDeAhora();
            try
            {
                PonerOrientacion("barra", "izquierda");
                Aplicar();
                await System.Threading.Tasks.Task.Delay(2500);
                Apunta($"[CAP] barra vertical en memoria: Vertical={Vertical}");
                await Foto("13-vertical", "La barra vertical, con las zonas apiladas");

                MostrarMensaje(HyprAviso.DelComando("layout.principal", 4, simulado: true));
                await System.Threading.Tasks.Task.Delay(1100);
                await Foto("14-vertical-indicador",
                           "El indicador breve en vertical: sale FLOTANDO al lado, no dentro");

                AlternarVista();
                await System.Threading.Tasks.Task.Delay(2500);

                // El recipiente de este lado, que es lo único que cambia respecto a la isla.
                // Los pinceles de las tarjetas ya los enseña la cadena de abajo, y repetirlos
                // aquí era ruido de cuando se estaba persiguiendo el fallo.
                Apunta($"[CAP] vertical: tarjeta flotante=" +
                       $"{(Superpuesta().Tarjeta.Background as SolidColorBrush)?.Color}  " +
                       $"ventana={(Superpuesta().Background as SolidColorBrush)?.Color} " +
                       $"transparencia={Superpuesta().AllowsTransparency}");

                ApuntarCadenaDeLaTarjeta("vertical");

                await Foto("15-vertical-vista", "La vista general con la barra vertical");
                await FotoEnPantalla(_superpuesta, "15b-vertical-vista-pantalla",
                                     "La MISMA, copiada de la pantalla: lo que ven los ojos");
                CerrarVista();
                await System.Threading.Tasks.Task.Delay(600);

                // ---------- 12. Y los CUATRO bordes, en los dos modos ----------
                //
                // D6 los dejó probados en lógica —que el menú cae dentro del área útil, que el
                // indicador elige la talla que cabe— y solo se había mirado uno. Los ocho
                // tardan medio minuto y son la diferencia entre «la lógica dice que cabe» y
                // «se ve bien».
                //
                // El modo importa tanto como el borde: con `isla` en un lateral la isla se
                // ANCLA a ese lado y sigue horizontal; con `barra` en un lateral es una barra
                // vertical de verdad. Son dos cosas distintas y las dos hay que verlas.
                foreach (var (modo, pos, que) in new[]
                {
                    ("isla",  "arriba",    "isla anclada arriba (lo que tú tienes)"),
                    ("isla",  "abajo",     "isla anclada abajo"),
                    ("isla",  "izquierda", "isla anclada a la izquierda, horizontal"),
                    ("isla",  "derecha",   "isla anclada a la derecha, horizontal"),
                    // Los dos de la notch van aquí y no en el barrido de temas porque lo que
                    // cambia es la SILUETA, y una silueta solo se comprueba mirándola: que las
                    // dos esquinas de arriba estén rectas, que no haya aire entre el borde de
                    // la pantalla y la muesca, y que el fondo sea opaco. Nada de eso se mide
                    // con un número.
                    //
                    // Y no están los laterales: una notch pedida en un lateral se dibuja
                    // arriba, así que serían dos fotos de la de arriba con otro nombre.
                    ("notch", "arriba",    "notch pegada al borde de arriba"),
                    ("notch", "abajo",     "notch abajo, la silueta del revés"),
                    ("barra", "arriba",    "barra de lado a lado, arriba"),
                    ("barra", "abajo",     "barra de lado a lado, abajo"),
                    ("barra", "izquierda", "barra vertical a la izquierda"),
                    ("barra", "derecha",   "barra vertical a la derecha"),
                })
                {
                    PonerOrientacion(modo, pos);
                    Aplicar();
                    await System.Threading.Tasks.Task.Delay(1800);

                    // Que el modo SE HAYA APLICADO, y no solo pedido. Una foto de una isla
                    // negra sobre un escritorio negro no distingue «barra de lado a lado» de
                    // «isla centrada»; estos cuatro números sí.
                    Apunta($"[CAP] {modo}/{pos}: EsIsla={EsIsla} Vertical={Vertical} " +
                           $"ventana={ActualWidth:0}x{ActualHeight:0} isla={Isla.ActualWidth:0}x{Isla.ActualHeight:0}");

                    await Foto($"16-{modo}-{pos}", $"Borde: {que}");
                }

                // ---------- 13. La rama de ALTO CONTRASTE ----------
                //
                // Ojo con lo que esto es y lo que no es. `HyprPinta.Forzado` enciende la rama
                // de alto contraste **de NubeHypr** —velos opacos, filo más grueso, sin
                // relleno— sin tocar tu Windows. Eso es lo que se fotografía.
                //
                // NO es Windows en modo alto contraste: eso cambia los colores de TODO el
                // sistema y hace falta encenderlo en la configuración de Windows, que no se
                // toca. Así que esta captura verifica nuestra rama, y la otra mitad sigue
                // siendo prueba manual. Decir lo contrario sería dar por verificado algo que
                // no se ha medido.
                PonerOrientacion(modoAntes, posicionAntes);
                Aplicar();
                await System.Threading.Tasks.Task.Delay(1500);

                HyprPinta.Forzado = true;
                try
                {
                    var m2 = _monitor;
                    PonerPreviaHypr(new NubeHypr.Rect(
                        m2.TrabajoX + m2.TrabajoAncho * 0.1, m2.TrabajoY + m2.TrabajoAlto * 0.15,
                        m2.TrabajoAncho * 0.45, m2.TrabajoAlto * 0.5), "Alto contraste");
                    await System.Threading.Tasks.Task.Delay(900);
                    await FotoDe(_previa, "17-altocontraste-previa",
                                 "La previsualización con la rama de alto contraste de NubeHypr " +
                                 "(NO es Windows en alto contraste)");
                    QuitarPreviaHypr();
                    await System.Threading.Tasks.Task.Delay(400);

                    AlternarVista();
                    await System.Threading.Tasks.Task.Delay(2200);
                    await Foto("18-altocontraste-vista",
                               "La vista general con la rama de alto contraste de NubeHypr");
                    CerrarVista();
                    await System.Threading.Tasks.Task.Delay(600);
                }
                finally
                {
                    // Siempre, pase lo que pase: si se quedara forzado, el resto de capturas
                    // de esta tanda mentirían sin avisar.
                    HyprPinta.Forzado = null;
                    Apunta($"[CAP] alto contraste devuelto a lo que diga Windows " +
                           $"(ahora: {SystemParameters.HighContrast})");
                }

                await ConTemaClaro();
            }
            catch (Exception ex)
            {
                Apunta($"[CAP] la barra vertical REVENTÓ: {ex.GetType().Name}: {ex.Message}");
                Log.Error("capturar la barra vertical", ex);
            }
            finally
            {
                PonerOrientacion(modoAntes, posicionAntes);
                Aplicar();
                await System.Threading.Tasks.Task.Delay(1500);
                Apunta($"[CAP] la barra vuelve a {modoAntes}/{posicionAntes}: " +
                       $"Vertical={Vertical}");
            }

            await ConLasComposicionesDeE1();
            await ConElDockDeE2();
            await ConLosLateralesDeE3();

            // El módulo que se añadió para la foto se quita, y la isla vuelve a ser la
            // tuya. En memoria los dos movimientos: el fichero no se ha tocado.
            if (loPuseYo)
            {
                _cfg.Modulos.Derecha.Remove("escritorios");
                Aplicar();
                await System.Threading.Tasks.Task.Delay(600);
                Apunta("[CAP] el indicador de escritorios se quita: tu isla queda como estaba");
            }

            // ---------- El índice ----------
            var indice = Path.Combine(DiagCapturas.Carpeta, "indice.txt");
            File.WriteAllLines(indice, _fotosHechas);
            Apunta($"[CAP] {_fotosHechas.Count} capturas en {DiagCapturas.Carpeta}");
            Apunta("[CAP] lo que una imagen quieta NO puede enseñar: parpadeos, controles que");
            Apunta("      se quedan agarrados al ratón y animaciones descompasadas.");
        }
        catch (Exception ex)
        {
            Apunta($"[CAP] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("las capturas", ex);
        }
        finally
        {
            try { CerrarPanel(); } catch { }
            Application.Current.Shutdown(0);
        }
    }

    // La isla (o la ventana superpuesta, si el panel salió flotando en vertical).
    async System.Threading.Tasks.Task Foto(string nombre, string que)
    {
        // En vertical los paneles viven en `_superpuesta`, así que fotografiar solo la isla
        // dejaría el panel fuera de la imagen — y la captura diría "no hay panel" cuando sí.
        var cual = _superpuesta is { IsVisible: true } sup ? (Window)sup : this;
        await FotoDe(cual, nombre, que);
    }

    // La misma ventana, pero copiando los PÍXELES DE LA PANTALLA en vez de pedirle que se
    // redibuje. Sirve para contrastar: si las dos salen iguales, `PrintWindow` cuenta la
    // verdad; si salen distintas, la que vale es esta, porque es la que ven los ojos.
    async System.Threading.Tasks.Task FotoEnPantalla(Window? v, string nombre, string que)
    {
        await System.Threading.Tasks.Task.Delay(120);
        try
        {
            if (v == null) { Apunta($"[CAP] {nombre}: no hay ventana"); return; }

            // ---------- El aviso que esto necesita ----------
            //
            // Copiar de la pantalla copia **lo que haya en la pantalla**. La primera vez que
            // se usó, la foto de la previsualización salió con un correo abierto detrás:
            // nombres, direcciones y una firma con teléfono. Es exactamente lo que la foto
            // tiene que enseñar —que se ve lo de debajo— y exactamente lo que nadie espera
            // encontrarse en un PNG que luego comparte.
            //
            // No se tapa ni se difumina: eso sería mentir sobre lo que la captura enseña. Se
            // dice, cada vez, para que quien mire las imágenes sepa qué está mirando antes de
            // mandárselas a alguien.
            Apunta($"[CAP] {nombre}: copia de PANTALLA — sale lo que hubiera detrás " +
                   "(ventanas tuyas, su contenido). Míralo antes de compartirlo.");
            var h = new System.Windows.Interop.WindowInteropHelper(v).Handle;
            if (h == IntPtr.Zero) { Apunta($"[CAP] {nombre}: sin hwnd"); return; }

            var r = Nativo.Rectangulo(h);
            var foto = Nativo.FotoDeLaPantalla(r.Izq, r.Arr, r.Der - r.Izq, r.Aba - r.Arr);
            if (foto == null) { Apunta($"[CAP] {nombre}: la copia de pantalla no devolvió nada"); return; }

            var ruta = Path.Combine(DiagCapturas.Carpeta, nombre + ".png");
            var cod = new PngBitmapEncoder();
            cod.Frames.Add(BitmapFrame.Create(foto));
            using var f = File.Create(ruta);
            cod.Save(f);

            _fotosHechas.Add($"{nombre}.png  {foto.PixelWidth}x{foto.PixelHeight}  — {que}");
            Apunta($"[CAP] {nombre}.png  {foto.PixelWidth}x{foto.PixelHeight}  {que}");
        }
        catch (Exception ex)
        {
            Apunta($"[CAP] {nombre}: EXCEPCIÓN {ex.GetType().Name}: {ex.Message}");
            Log.Error($"captura de pantalla {nombre}", ex);
        }
    }

    // ---------- Un control suelto, sin ventana ----------
    //
    // `PrintWindow` necesita una ventana; un `Border` que todavía no está en ninguna, no
    // tiene hwnd. `RenderTargetBitmap` dibuja el árbol visual directamente, que es lo que
    // hace falta para retratar una pieza que solo aparece en un modo concreto.
    //
    // Medir y colocar a mano es obligatorio: un elemento que no ha pasado por un layout
    // tiene tamaño cero y saldría una imagen vacía.
    async System.Threading.Tasks.Task FotoDeElemento(FrameworkElement? e, string nombre, string que)
    {
        await System.Threading.Tasks.Task.Delay(60);
        try
        {
            if (e == null) { Apunta($"[CAP] {nombre}: no hay elemento"); return; }

            // ---------- Con el fondo del panel detrás ----------
            //
            // Sin esto la foto miente, y mintió: el rótulo salió casi invisible porque sus
            // superficies son semitransparentes y `RenderTargetBitmap` las dibuja sobre nada.
            // En la ventana de verdad hay un panel opaco debajo. Se le pone el mismo.
            var sobre = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(_roles.SuperficieSolida),
                Padding = new Thickness(14),
                Child = e,
            };
            e = sobre;

            const double ancho = 620;
            e.Measure(new Size(ancho, double.PositiveInfinity));
            e.Arrange(new Rect(0, 0, ancho, e.DesiredSize.Height));
            e.UpdateLayout();

            int w = (int)Math.Ceiling(ancho), h = (int)Math.Ceiling(e.DesiredSize.Height);
            if (w <= 0 || h <= 0) { Apunta($"[CAP] {nombre}: el elemento mide 0"); return; }

            var mapa = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            mapa.Render(e);
            mapa.Freeze();

            var ruta = Path.Combine(DiagCapturas.Carpeta, nombre + ".png");
            var cod = new PngBitmapEncoder();
            cod.Frames.Add(BitmapFrame.Create(mapa));
            using var f = File.Create(ruta);
            cod.Save(f);

            _fotosHechas.Add($"{nombre}.png  {w}x{h}  — {que}");
            Apunta($"[CAP] {nombre}.png  {w}x{h}  {que}");
        }
        catch (Exception ex)
        {
            Apunta($"[CAP] {nombre}: EXCEPCIÓN {ex.GetType().Name}: {ex.Message}");
            Log.Error($"captura del elemento {nombre}", ex);
        }
    }

    async System.Threading.Tasks.Task FotoDe(Window? v, string nombre, string que)
    {
        await System.Threading.Tasks.Task.Delay(120);   // que termine el último fotograma
        try
        {
            if (v == null) { Apunta($"[CAP] {nombre}: no hay ventana que fotografiar"); return; }
            var h = new System.Windows.Interop.WindowInteropHelper(v).Handle;
            if (h == IntPtr.Zero) { Apunta($"[CAP] {nombre}: sin hwnd"); return; }

            var foto = Nativo.Foto(h, 4000);
            if (foto == null) { Apunta($"[CAP] {nombre}: PrintWindow no devolvió nada"); return; }

            var ruta = Path.Combine(DiagCapturas.Carpeta, nombre + ".png");
            var cod = new PngBitmapEncoder();
            cod.Frames.Add(BitmapFrame.Create(foto));
            using var f = File.Create(ruta);
            cod.Save(f);

            _fotosHechas.Add($"{nombre}.png  {foto.PixelWidth}x{foto.PixelHeight}  — {que}");
            Apunta($"[CAP] {nombre}.png  {foto.PixelWidth}x{foto.PixelHeight}  {que}");
        }
        catch (Exception ex)
        {
            Apunta($"[CAP] {nombre}: no se pudo guardar ({ex.GetType().Name})");
            Log.Error("guardar la captura " + nombre, ex);
        }
    }
}
