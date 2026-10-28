using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// E3: los paneles laterales, como superficies independientes.
//
// ---------- Por qué los dos lados en un solo archivo ----------
//
// Porque son la misma superficie con otro contenido. Lo que tienen en común —ser una ventana
// aparte, colocarse sin pisar la barra ni al otro panel, abrirse y cerrarse sin tocar nada
// más, anotarse en el tablero de E0— es todo; lo que cambia son las secciones.
//
// Escribirlos por separado habría dado dos sitios donde arreglar el mismo fallo de
// colocación, que es exactamente lo que pasó con el aviso de volumen y el indicador de D6.
//
// ---------- Lo que esto NO hace ----------
//
// No cierra el otro panel. No cierra los paneles clásicos de la isla. No reconstruye la
// barra. Es el primer sitio del proyecto donde **dos superficies de contenido conviven**, que
// era el objetivo de E0.
public partial class MainWindow
{
    readonly Dictionary<Lateral.Lado, VentanaSuperpuesta> _laterales = new();
    readonly Dictionary<Lateral.Lado, StackPanel> _cuerposLaterales = new();

    static string IdDe(Lateral.Lado l) => l == Lateral.Lado.Izquierda ? "izquierda" : "derecha";

    static Superficies.Clase ClaseDe(Lateral.Lado l) =>
        l == Lateral.Lado.Izquierda ? Superficies.Clase.Izquierda : Superficies.Clase.Derecha;

    internal bool LateralAbierto(Lateral.Lado l) =>
        _laterales.TryGetValue(l, out var v) && v.IsVisible;

    // ================= Que el tema llegue a los paneles abiertos =================
    //
    // ---------- Lo que pasaba ----------
    //
    // Cambiabas de tema claro a oscuro y la barra se repintaba, pero los paneles abiertos se
    // quedaban con los colores viejos hasta que los cerrabas y los volvías a abrir. El usuario
    // lo dijo: «cuando pongo tema oscuro o claro no se actualiza en los paneles
    // automáticamente».
    //
    // El motivo es que un lateral es una VENTANA APARTE, con sus colores volcados al
    // construirla. `RecargarTodas` llama a `Aplicar()`, que repinta la isla y remonta sus
    // zonas — y de las otras ventanas no sabe nada.
    //
    // ---------- Y se tira la ventana entera, no se repinta ----------
    //
    // Los colores no están solo en la tarjeta: están en cada pincel de cada fila, cada
    // azulejo, cada anillo y cada insignia, resueltos por contraste contra la superficie de
    // entonces. Ir repintando eso pieza a pieza sería inventar una segunda forma de aplicar
    // el tema, y las dos formas acabarían discrepando.
    //
    // Reconstruir es lo que ya hace `Rellenar`, y lo que ya se hace al abrir el panel.
    internal void RepintarLateralesAbiertos()
    {
        foreach (var l in new[] { Lateral.Lado.Izquierda, Lateral.Lado.Derecha })
        {
            try
            {
                if (!LateralAbierto(l)) continue;
                if (!_laterales.TryGetValue(l, out var v)) continue;

                PintarConchaLateral(v);
                Rellenar(l);
                ColocarLateral(l);
            }
            catch (Exception ex) { Log.Error($"repintar el panel {IdDe(l)} al cambiar de tema", ex); }
        }
    }

    // ================= Abrir, cerrar, alternar =================
    internal void AlternarLateral(Lateral.Lado l)
    {
        if (LateralAbierto(l)) CerrarLateral(l); else AbrirLateral(l);
    }

    internal void AbrirLateral(Lateral.Lado l)
    {
        if (_cerrando) return;
        try
        {
            // ---------- ¿Caben los dos? ----------
            //
            // En una pantalla estrecha, dos columnas de 340 px no dejan escritorio en medio, y
            // un panel que tapa lo que hay debajo es peor que un panel que no se abre. Se
            // dice en vez de abrirlo encima del otro.
            var otro = l == Lateral.Lado.Izquierda ? Lateral.Lado.Derecha : Lateral.Lado.Izquierda;
            double s = Math.Max(0.1, _monitor.Escala);
            if (LateralAbierto(otro) &&
                !Lateral.CabenLosDos(UtilDeEstaPantalla(), Lateral.Ancho * EscalaDePaneles() * s,
                                     Lateral.DelFilo * s))
            {
                Log.Info($"panel {IdDe(l)}: no cabe junto al otro en esta pantalla " +
                         $"({UtilDeEstaPantalla().Ancho:0} px); se cierra el otro");
                CerrarLateral(otro);
            }

            var v = _laterales.TryGetValue(l, out var ya) ? ya : CrearLateral(l);

            // ---------- La concha, cada vez que se abre ----------
            //
            // Lo pintaba `CrearLateral` y solo él, o sea UNA vez en la vida de la ventana. Y
            // `RepintarLateralesAbiertos` repinta los que están abiertos, con este
            // razonamiento escrito al lado: «reconstruir es lo que ya hace `Rellenar`, y lo
            // que ya se hace al abrir el panel». Lo que se reconstruía al abrir era el
            // CONTENIDO; la concha no.
            //
            // Resultado: cambias de tema con un panel cerrado, lo abres, y sus tarjetas salen
            // del tema nuevo sobre una concha del tema viejo. Con los ocho temas de antes se
            // notaba poco —todos eran oscuros—; con Sakura sobre Profesional son tarjetas
            // blancas sobre un fondo casi negro. Lo cazó `--capturas` midiendo los nueve.
            PintarConchaLateral(v);
            Rellenar(l);
            ColocarLateral(l);

            if (!v.IsVisible)
            {
                v.Show();
                // Y otra vez ya visible: enseñar la ventana puede devolverle lo que WPF
                // tenía apuntado, y es el rectángulo en píxeles el que manda.
                ColocarLateral(l);
                v.Tarjeta.BeginAnimation(OpacityProperty, null);
                v.Tarjeta.Opacity = 1;
                // E6: la duración sale del catálogo de transiciones, no de un número
                // escrito aquí. Dos sitios con dos números es como se acaba con un panel
                // que entra más despacio que el dock sin que nadie lo haya decidido.
                double ms = Transiciones.Duracion(Transiciones.Que.PanelEntra).TotalMilliseconds;
                v.Tarjeta.BeginAnimation(OpacityProperty, Kf(0, 1, ms));
                EntrarDesdeElBorde(v, l, ms);
                EscalonarSecciones(l, ms);
            }
            v.Elevar();

            _mesa.Abrir(Superficies.Nueva(IdDe(l), ClaseDe(l), DuenoDeLaMesa) with
            {
                Ancho = Lateral.Ancho,
                Borde = l == Lateral.Lado.Izquierda ? NubeHypr.Borde.Izquierda : NubeHypr.Borde.Derecha,
            });

            Log.Info($"panel {IdDe(l)} abierto · tablero: {_mesa.Linea()}");
        }
        catch (Exception ex) { Log.Error($"abrir el panel {IdDe(l)}", ex); }
    }

    internal void CerrarLateral(Lateral.Lado l)
    {
        try
        {
            if (_laterales.TryGetValue(l, out var v) && v.IsVisible) v.Hide();
            _mesa.Cerrar(IdDe(l));

            // ---------- Y nada de deslizadores agarrados ----------
            //
            // Cerrar un panel con un deslizador cogido dejaba el gesto vivo: el ratón seguía
            // capturado y el siguiente clic iba al deslizador de un panel que ya no está. Es
            // un fallo que ya se arregló una vez en el centro de control, y aquí no puede
            // volver por la puerta de atrás.
            SoltarLosDeslizadoresDe(l);
        }
        catch (Exception ex) { Log.Error($"cerrar el panel {IdDe(l)}", ex); }
    }

    internal void SoltarTodosLosLaterales()
    {
        foreach (var l in new[] { Lateral.Lado.Izquierda, Lateral.Lado.Derecha })
        {
            try
            {
                SoltarLosDeslizadoresDe(l);
                _mesa.Cerrar(IdDe(l));
                if (_laterales.TryGetValue(l, out var v)) v.Close();
            }
            catch (Exception ex) { Log.Error($"soltar el panel {IdDe(l)}", ex); }
        }
        _laterales.Clear();
        _cuerposLaterales.Clear();
        _mandosDeLateral.Clear();
    }

    // Los deslizadores vivos de cada panel, para poder soltarlos al cerrar.
    readonly Dictionary<Lateral.Lado, List<MandoSlider>> _mandosDeLateral = new();

    void SoltarLosDeslizadoresDe(Lateral.Lado l)
    {
        if (!_mandosDeLateral.TryGetValue(l, out var mandos)) return;
        foreach (var m in mandos)
        {
            try { m.Gesto?.Terminar("se cierra el panel lateral"); } catch { }
        }

        // Y lo que haya que devolverle al sistema: los handles del driver del monitor, que
        // no soltarlos los fuga (ya pasó una vez con el centro de control).
        if (_aSoltarDelLateral.TryGetValue(l, out var soltar))
        {
            try { soltar(); } catch { }
            _aSoltarDelLateral.Remove(l);
        }
        mandos.Clear();
    }

    // ================= Cómo entra un panel lateral =================
    //
    // La maqueta: «deslizan 24 px desde su borde y aparecen, y sus secciones escalonadas».
    //
    // ---------- Por qué DESDE SU BORDE y no desde cualquier sitio ----------
    //
    // Un panel que aparece deslizándose desde el borde por el que está pegado dice de dónde
    // ha salido y, al cerrarse, a dónde se va. Entrando desde arriba o desde el centro sería
    // una cosa que aparece — y entonces el usuario tiene que acordarse de dónde estaba en vez
    // de verlo.
    //
    // Va en la TARJETA y no en la ventana: mover la ventana son 24 px de recolocación por
    // fotograma pedidos a Windows, y `ColocarLateral` acaba de medir dónde tiene que estar.
    void EntrarDesdeElBorde(VentanaSuperpuesta v, Lateral.Lado l, double ms)
    {
        if (!Animacion.Adorno) return;
        double desde = l == Lateral.Lado.Izquierda ? -24 : 24;
        var mueve = new TranslateTransform(desde, 0);
        v.Tarjeta.RenderTransform = mueve;
        mueve.BeginAnimation(TranslateTransform.XProperty, Kf(desde, 0, ms, 0, Suave));
        mueve.X = 0;
    }

    // ---------- Y sus secciones, una detrás de otra ----------
    //
    // Mismo escalonado que el menú Inicio: 50 ms entre tarjetas y 10 px de subida. Un panel
    // con seis tarjetas que aparece entero de golpe se lee como una imagen; entrando en orden
    // se lee como una lista, y el ojo la recorre por donde está puesta.
    //
    // Solo al ABRIR, nunca al refrescar. `Rellenar` se llama cada vez que cambia un dato —el
    // volumen, la CPU, una carpeta— y animar ahí dejaría el panel temblando mientras lo miras.
    void EscalonarSecciones(Lateral.Lado l, double ms)
    {
        if (!Animacion.Adorno) return;
        if (!_laterales.TryGetValue(l, out var v)) return;
        if (v.Cajon.Content is not Panel cuerpo) return;

        int i = 0;
        foreach (var hijo in cuerpo.Children.OfType<FrameworkElement>())
        {
            double retardo = i * 50;
            var mover = new TranslateTransform { Y = 10 };
            hijo.RenderTransform = mover;
            hijo.BeginAnimation(OpacityProperty, Kf(0, 1, ms * 0.6, retardo));
            mover.BeginAnimation(TranslateTransform.YProperty, Kf(10, 0, ms, retardo, Muelle));
            mover.Y = 0;
            i++;
        }
    }

    // ---------- El color y la forma de la concha de un panel lateral ----------
    //
    // En un solo sitio porque se pide desde tres: al crear la ventana, al abrirla y al
    // cambiar de tema con ella abierta. Estaba escrito dos veces y la tercera faltaba, que es
    // justo como se acaba con un panel del tema nuevo dentro de una concha del viejo.
    //
    // Opaco de verdad, no el fondo del tema con su alfa: un panel de controles con el
    // escritorio transparentándose detrás hace ilegible cualquier texto pequeño. Es la misma
    // regla que `PanelShell` desde C5.
    //
    // Y el halo de AMBIENTE: los laterales no pasan por `PanelShell` —montan su propia
    // tarjeta—, así que el halo de la concha no les llegaba nunca. Era lo que hacía que el
    // resplandor no se viera en los paneles por mucho que respirara. El margen lo pone
    // `ColocarLateral`, que es quien sabe cuánto aire cabe por cada lado; aquí solo se
    // engancha el efecto.
    void PintarConchaLateral(VentanaSuperpuesta v)
    {
        var est = EstiloDeAhora;
        v.Tarjeta.Background = PincelSolido(_roles.FondoOpaco);
        v.Tarjeta.BorderBrush = new SolidColorBrush(_roles.Borde);
        v.Tarjeta.BorderThickness = new Thickness(est.GrosorFilo);
        v.Tarjeta.CornerRadius = new CornerRadius(est.RadioShell);
        PonerResplandor(v.Tarjeta, est, Realce.Ambiente);
        ChaflanSiToca(v.Tarjeta);
    }

    // ================= La ventana =================
    VentanaSuperpuesta CrearLateral(Lateral.Lado l)
    {
        var v = new VentanaSuperpuesta(this);

        // Opaco de verdad, no el fondo del tema: un panel de controles con el escritorio
        // transparentándose detrás hace ilegible cualquier texto pequeño. Es la misma regla
        // que `PanelShell` desde C5.
        v.Tarjeta.Padding = new Thickness(0);
        PintarConchaLateral(v);

        var cuerpo = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };
        var scroll = new ScrollViewer
        {
            Content = cuerpo,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        v.Cajon.Content = scroll;

        // ---------- Y que no se cierre cuando vas a usarlo ----------
        //
        // El panel se abre al rozar el filo de la pantalla, y lo siguiente que hace uno es
        // meter el ratón DENTRO, donde ya no está rozando el filo. Sin esto se cerraría en
        // ese instante: el gesto para abrirlo y el gesto para usarlo son incompatibles.
        //
        // Es el mismo detalle que el dock ya tenía resuelto con `_ratonEnElDock`, y aquí
        // faltaba: la bandera existía y no la ponía nadie, así que la puerta nueva habría
        // salido inusable a la primera. Lo cazó un `grep`, no una prueba — de ahí la de
        // abajo, que exige que la regla se cumpla.
        v.MouseEnter += (_, _) => RatonEnLateral(l, true);
        v.MouseLeave += (_, _) => RatonEnLateral(l, false);

        // Al pasar de un monitor a otro con distinto DPI, Windows le propone a la ventana
        // otro tamaño y WPF lo aplica: el rectángulo que se había calculado en píxeles deja
        // de ser el que hay. Se vuelve a colocar con la escala ya buena. Recolocar en el
        // mismo monitor no cambia el DPI, así que esto no se repite en bucle.
        v.DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (v.IsVisible) ColocarLateral(l);
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        _laterales[l] = v;
        _cuerposLaterales[l] = cuerpo;
        _mandosDeLateral[l] = new List<MandoSlider>();
        return v;
    }

    void ColocarLateral(Lateral.Lado l)
    {
        if (!_laterales.TryGetValue(l, out var v)) return;
        if (!_cuerposLaterales.TryGetValue(l, out var cuerpo)) return;

        // ---------- Unidades de WPF por dentro, píxeles por fuera ----------
        //
        // El hueco y `Colocar` van en píxeles de pantalla; el ancho del panel, lo que mide el
        // contenido y las separaciones, en unidades de WPF. Se pasaban tal cual de unas a
        // otras, y al 125 % la ventana salía un 20 % más baja y más estrecha que su
        // contenido: el final quedaba cortado por abajo. Lo contó el usuario: «en los
        // paneles laterales flotantes ahora se cortan por abajo». Ahora todo lo que es de
        // WPF se convierte con la escala del monitor antes de colocar.
        //
        // Y el panel se compacta con la misma escala que el resto (`EscalaDePaneles`): en
        // una pantalla pequeña salía igual de grande y alargado que en una de 1440.
        double s = Math.Max(0.1, _monitor.Escala);
        double f = EscalaDePaneles();
        if (v.Cajon.Content is FrameworkElement dentro) Escalar(dentro, f);

        // Lo que pide de alto, medido de verdad y no a ojo: un panel con cuatro secciones y
        // uno con siete no miden lo mismo, y dejar un hueco fijo da una columna medio vacía.
        cuerpo.Measure(new Size(Lateral.Ancho - 28, double.PositiveInfinity));
        double bordes = v.Tarjeta.BorderThickness.Top + v.Tarjeta.BorderThickness.Bottom;
        double pide = ((cuerpo.DesiredSize.Height + 26) * f + bordes) * s;
        double ancho = Lateral.Ancho * f * s;

        // Con los estorbos descontados, no con el área de trabajo cruda: ver
        // `EstorbosParaLosLaterales`. Antes el panel se centraba en la pantalla entera y se
        // metía debajo de la barra —12 px de margen contra 38 de isla— y, con el dock
        // encendido, también debajo del dock.
        var h = ZonasAjuste.AreaUtilCon(_monitor, EstorbosParaLosLaterales());
        var r = Lateral.Sitio(new NubeHypr.Rect(h.X, h.Y, h.Ancho, h.Alto), l, pide,
                              ancho, Lateral.DelFilo * s, Lateral.DelOtroLado * s);

        // ---------- Sitio para el halo ----------
        //
        // Un `DropShadowEffect` se dibuja FUERA del elemento. Con la ventana midiendo
        // exactamente lo que la tarjeta, el halo del neón caía justo en el borde de la
        // ventana y se recortaba entero: en las capturas no se veía nada de nada, y por eso
        // el estilo parecía «solo un borde».
        //
        // Se agranda la VENTANA —que es transparente— y se mete la tarjeta hacia dentro el
        // mismo margen (ver `CrearLateral`). La tarjeta visible no cambia de sitio ni de
        // tamaño: lo único que aparece es el aire alrededor donde el halo puede existir.
        //
        // Sin halo, aire cero: una ventana más grande de lo necesario captura el ratón donde
        // no hay nada pintado.
        //
        // ---------- Y solo el aire que CABE ----------
        //
        // La primera versión sumaba 30 px por los cuatro lados sin mirar. `--diagpaneles` lo
        // cazó en la misma pasada: «4 con algo fuera y sin desplazamiento». Claro — el panel
        // ya está pegado al filo de la pantalla y al área que dejan la barra y el dock, así
        // que agrandarlo lo saca del hueco útil.
        //
        // Se pide el aire y se recorta contra ese hueco, lado por lado. Donde no cabe, el
        // halo se corta por ahí y ya está: mejor un halo cortado por un lado que un panel
        // fuera de la pantalla.
        double quiero = EstiloDeAhora.Brilla ? Lateral.AireDelHalo * s : 0;
        var aire = Lateral.AireQueCabe(new NubeHypr.Rect(h.X, h.Y, h.Ancho, h.Alto), r, quiero);

        // El aire se ha contado en píxeles; el margen de la tarjeta va en unidades de WPF.
        v.Tarjeta.Margin = new Thickness(aire.Izq / s, aire.Arr / s, aire.Der / s, aire.Aba / s);
        v.Colocar((int)(r.X - aire.Izq), (int)(r.Y - aire.Arr),
                  (int)(r.Ancho + aire.Izq + aire.Der),
                  (int)(r.Alto + aire.Arr + aire.Aba));

        // Las coordenadas, en el registro. No es ruido: es la única forma de comprobar desde
        // fuera que el panel no se mete debajo de la barra ni del dock, que es justo lo que
        // hacía hasta que `EstorbosParaLosLaterales` entró en juego. Una captura enseña que
        // el panel existe; estos números enseñan DÓNDE.
        Log.Info($"panel {IdDe(l)} colocado en {r} · pide {pide:0} de alto · " +
                 $"hueco {h.X:0},{h.Y:0} {h.Ancho:0}x{h.Alto:0} · estorbos: " +
                 string.Join(" ", EstorbosParaLosLaterales().Where(x => x.Cuenta)
                                      .Select(x => $"{x.Donde} {x.Grosor:0}px")));

        // Y lo que WPF cree que tiene, una vez hecho el layout. Los píxeles de arriba son lo
        // que se le pidió a Windows; esto es lo que la ventana se cree, que es lo que decide
        // si el scroll sale o no. Si no cuadran (DPI distinto del del monitor), el panel se
        // corta por abajo SIN barra, porque el scroll piensa que todo cabe.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(v);
                string scroll = v.Cajon.Content is ScrollViewer sv
                    ? $"scroll visible {sv.ViewportHeight:0} de {sv.ExtentHeight:0} · barra {sv.ComputedVerticalScrollBarVisibility}"
                    : "sin scroll";
                Log.Info($"panel {IdDe(l)} según WPF: {v.ActualWidth:0}x{v.ActualHeight:0} a escala " +
                         $"{dpi.DpiScaleY:0.00} (monitor {s:0.00}) · tarjeta {v.Tarjeta.ActualHeight:0} · {scroll}");
            }
            catch { }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // Las secciones que de verdad lleva este panel, con el orden que tenga puesto el usuario.
    // En un solo sitio para que el panel, los ajustes y los diagnósticos vean lo mismo: dos
    // formas de contestar «¿qué lleva este panel?» acaban discrepando el día que una aprenda
    // algo que la otra no.
    internal IReadOnlyList<Lateral.Seccion> SeccionesDe(Lateral.Lado l) =>
        Lateral.Repartidas(l, _cfg?.Laterales?.Izquierda, _cfg?.Laterales?.Derecha);

    // ================= El contenido =================
    void Rellenar(Lateral.Lado l)
    {
        if (!_cuerposLaterales.TryGetValue(l, out var cuerpo)) return;
        cuerpo.Children.Clear();
        _mandosDeLateral[l].Clear();

        // Tu reparto, no el mío: `Repartidas` mira lo que tengas guardado y cae en el de
        // serie si no has movido nada todavía.
        foreach (var s in SeccionesDe(l))
        {
            try
            {
                var contenido = CuerpoDeSeccion(s, l);
                if (contenido == null) continue;        // una sección sin datos no se pinta
                cuerpo.Children.Add(Seccion(s, contenido));
            }
            catch (Exception ex)
            {
                Log.Error($"la sección «{s.Id}» del panel {IdDe(l)}", ex);
                // Una sección que revienta no se lleva el panel por delante: se dice y se
                // sigue con las demás.
                var mal = Linea("No se ha podido cargar. El motivo está en el registro.", 11);
                mal.TextWrapping = TextWrapping.Wrap;
                mal.FontSize = 11;
                mal.Foreground = new SolidColorBrush(
                    HyprPinta.CoralTinta(_roles, _roles.FondoOpaco));
                cuerpo.Children.Add(Seccion(s, mal));
            }
        }
    }

    // ================= Una sección, con su tarjeta =================
    //
    // ---------- Por qué esto cambió ----------
    //
    // Antes era un `TextBlock` en negrita y debajo el contenido, los dos sueltos sobre el
    // fondo del panel. El resultado, dicho por el usuario mirándolo: «parece un prototipo:
    // paneles negros con texto plano, poca jerarquía, pocos iconos».
    //
    // Y tenía razón, con una causa concreta: estos paneles se escribieron con `Linea()` y
    // `StackPanel` pelados mientras el resto del programa ya tenía un sistema de componentes
    // —`Card`, `SectionHeader`, `Toggle`, `Slider`, `MetricCard`, `MediaCard`— que usan los
    // paneles de la isla. Cero llamadas desde aquí. Es el mismo patrón que el dock sin
    // interruptor o los laterales sin puerta, esta vez en lo visual: construido y sin usar.
    //
    // Ahora cada sección es una **tarjeta** con su cabecera de icono y título, que es lo que
    // da la jerarquía: el ojo salta de tarjeta en tarjeta sin leer, y dentro de cada una hay
    // un solo tema.
    //
    // ---------- Las que van «sueltas» ----------
    //
    // Las notificaciones y el reproductor traen sus propias tarjetas dentro. Meterlas en otra
    // da un marco rodeando marcos, que es ruido. Esas llevan solo la cabecera.
    FrameworkElement Seccion(Lateral.Seccion s, UIElement contenido)
    {
        var columna = new StackPanel();

        // La flecha, con destino cuando la sección tiene uno. Ver `FlechaDeCabecera`: es lo
        // que hace que seis tarjetas distintas se lean como el mismo sistema.
        // ---------- Las que no llevan cabecera ----------
        //
        // Hoy solo la identidad. En la referencia, la nube ES la tarjeta: ponerle encima una
        // cabecera que dice «Perfil y nube» y debajo un bloque que dice «Nube» repite el
        // nombre y le roba altura a lo único que tiene que mandar en ese panel.
        UIElement? cabecera = null;
        if (!s.SinCabecera)
        {
            cabecera = SectionHeader(s.Icono, s.Titulo, FlechaDeCabecera(DondeLlevaLaSeccion(s)));
            if (cabecera is FrameworkElement fe) fe.ToolTip = s.Que;
            columna.Children.Add(cabecera);
        }
        columna.Children.Add(contenido);

        if (s.Suelta)
        {
            // ---------- Y la cabecera suelta, metida hasta donde entran las demás ----------
            //
            // Esto salió mirando una captura, no leyendo el código. Una sección con tarjeta
            // mete su cabecera el relleno de la tarjeta más su filo —14 px con el estilo
            // normal—; una suelta no tiene tarjeta, así que su cabecera se quedaba pegada al
            // borde. El resultado, bajando por el panel derecho: «Notificaciones» 17 px a la
            // izquierda de «Calendario», y su flecha 13 px más a la derecha que la de todas
            // las demás. Las seis cabeceras zigzagueaban.
            //
            // Nadie lo lee como «aquí falta un relleno»; se lee como que el panel está mal
            // hecho, que es justo lo que había que quitar de esta fase.
            //
            // Se mete la CABECERA y no la columna: el contenido de estas secciones ya trae
            // sus propias tarjetas, y esas sí tienen que ir de filo a filo como el resto.
            // ---------- Y más aire DEBAJO ----------
            //
            // `SectionHeader` deja 8 px bajo el título, que es lo correcto cuando va dentro
            // de una tarjeta: ahí el relleno de la tarjeta ya separa. Suelta no hay relleno,
            // así que el rótulo quedaba pegado al borde de su tarjeta — el usuario lo dijo
            // mirando «Notificaciones»: «esto también está como muy junto».
            //
            // Un rótulo pegado a la caja de abajo se lee como parte de esa caja, que es justo
            // lo contrario de lo que hace una cabecera.
            var e = EstiloDeAhora;
            if (cabecera is FrameworkElement ce)
                ce.Margin = new Thickness(e.Dentro + e.GrosorFilo, 2,
                                          e.Dentro + e.GrosorFilo, 6);

            columna.Margin = new Thickness(0, 0, 0, HuecoCard);
            return columna;
        }
        return Card(columna);
    }

    // ---------- A dónde lleva la flecha de cada sección ----------
    //
    // `null` significa «esta sección no lleva a ningún sitio», y entonces la flecha se pinta
    // atenuada y no responde. Es a propósito: una flecha que se puede pulsar y no hace nada
    // enseña que el panel está a medias.
    //
    // Y lo que hay aquí son sitios que ya existen, no pantallas por hacer. El día que haya
    // una vista de notificaciones o un calendario grande, se añaden; mientras tanto, la
    // flecha de esas dos dice la verdad quedándose quieta.
    Action? DondeLlevaLaSeccion(Lateral.Seccion s) => s.Id switch
    {
        "perfil" => AbrirAjustesEnNubeHypr,
        "rapidas" => AbrirAjustes,
        "audio" => () => AbrirAjustes("ms-settings:sound"),
        "carpetas" => () => Fuentes.Lanzar(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        "maquina" => () => AbrirAjustes("ms-settings:about"),
        "ajustes" => AbrirAjustes,
        "conexion" => () => AbrirAjustes("ms-settings:network"),
        // Esta lleva a los ajustes de NubeBar y no a los de Windows, al contrario que
        // «maquina»: lo que uno quiere de esta cabecera es encender el clima o cambiar de
        // ciudad, y eso vive en nuestra ventana. Y es la ÚNICA puerta a ese interruptor desde
        // el panel: sin esta flecha, el clima apagado no se podría encender desde el sitio
        // donde se nota que está apagado.
        "maquina-info" => () => AbrirAjustesEn("sistema"),
        _ => null,
    };
}
