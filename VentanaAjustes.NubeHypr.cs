using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// D6: la página de NubeHypr en Ajustes — reglas y atajos, con búsqueda, grupos, estado y
// la explicación de lo que decidiría cada ventana abierta.
//
// ---------- Por qué una página y no una pestaña más de "Atajos" ----------
//
// Porque son dos preguntas distintas. "¿Qué tecla hace esto?" es la de Atajos, y ahí sigue
// el editor de D5. "¿Por qué mi navegador no se va al espacio 2?" es esta, y no se contesta
// con una lista: se contesta viendo qué regla gana, qué reglas se descartaron y por qué, y
// —antes de nada— que la capa está en simulación y no mueve nada.
//
// ---------- Lo que esta página NO hace ----------
//
// No mueve ventanas. No enciende la capa por su cuenta. Y el interruptor de encender está
// aquí con su aviso, porque encender NubeHypr es una decisión del usuario y tiene que
// poder tomarla en el mismo sitio donde ve lo que haría.
public sealed partial class VentanaAjustes
{
    TextBox? _buscarReglas;
    StackPanel? _listaReglas;
    StackPanel? _listaDecisiones;
    string _busquedaReglas = "";
    bool _soloReglasActivas;

    // La configuración que esta página LEE. Se carga una vez al construir la sección, como
    // hace el editor de atajos: la ventana de Ajustes escribe por `_arbol` —el árbol JSON,
    // que conserva las claves que esta versión no conoce— y no por el modelo tipado.
    // Escribir por el modelo subiría la versión del esquema en cada guardado, que es
    // exactamente el fallo que ya migró una vez el config real del usuario.
    Config _hypr = new();

    // ---------- La correspondencia entre el interruptor y la clave ----------
    //
    // Sacada aquí y no escrita en el lambda del interruptor por el mismo motivo que la del
    // dock: es donde se cuela un fallo que no se ve. Un interruptor que escribe
    // «simulación» con tilde, o «Aplicar» con mayúscula, deja la capa en simulación para
    // siempre y en la interfaz se ve encendido. Eso es exactamente la clase de fallo que
    // tuvo esta página —un modo sin control durante toda la fase D—, y no vuelve sin que una
    // prueba lo diga.
    internal static string ModoConfigDeParaPruebas(bool aplicando) => ModoConfigDe(aplicando);
    internal static bool AplicandoDeParaPruebas(string? modo) => AplicandoDe(modo);
    internal static string? PorQueNoSeTocaElModoParaPruebas(bool activo, bool demostracion) =>
        PorQueNoSeTocaElModo(activo, demostracion);

    // ---------- Por qué los dos interruptores de debajo pueden estar apagados ----------
    //
    // La demostración va PRIMERO, y ese orden importa. Con `--guia`, `DiagGuia.Apagar` clava
    // el modo en «simulacion» en memoria aunque el fichero diga «aplicar» y aunque la capa
    // esté activa: en demostración no puede haber ningún camino que mueva ventanas. Si se
    // mirara antes `activo`, una configuración con la capa encendida enseñaría los dos
    // interruptores vivos en un modo donde no hacen nada.
    //
    // Es la misma regla que el resto del programa: la interfaz enseña lo que VA A PASAR, no
    // lo que hay escrito en el fichero.
    static string? PorQueNoSeTocaElModo(bool activo, bool demostracion) =>
        demostracion ? "estás en modo de demostración"
      : !activo      ? "NubeHypr está apagado"
      : null;

    static string ModoConfigDe(bool aplicando) => aplicando ? "aplicar" : "simulacion";

    // Por `ModoEfectivo`, que es quien lo lee en el producto: así un «Aplicar » con mayúscula
    // y un espacio —o una basura cualquiera— se interpreta aquí igual que allí. Con
    // `activo: true` porque lo que se pregunta es qué dice la clave del modo, no si la capa
    // está encendida: eso es el otro interruptor.
    static bool AplicandoDe(string? modo) =>
        NubeHypr.ModoEfectivo(true, modo) == NubeHypr.Modo.Aplicar;

    UIElement SeccionNubeHypr()
    {
        _hypr = Config.Cargar() ?? new Config();
        var p = NuevaPagina("nubehypr");

        // ================= El rótulo del modo de demostración =================
        //
        // Lo primero de la página, antes incluso del estado. Un modo seguro que no se anuncia
        // es un modo en el que nadie se atreve a tocar nada, y entonces no sirve para lo que
        // se hizo: para poder pulsar los ocho ejemplos sin miedo.
        if (DiagGuia.Encendido)
        {
            p.Grupo("", Ico.Nube);
            p.Mas(RotuloDeDemostracion());
        }

        // ================= El estado, lo primero =================
        p.Grupo("Estado", Ico.Nube,
                "NubeHypr organiza ventanas. De serie está apagado, y encendido empieza en simulación.");

        var modo = HyprSalud.ModoConSeguro(_hypr.NubeHypr.Activo, _hypr.NubeHypr.Modo);
        p.Mas(CabeceraDeNubeHypr(modo));

        // El aviso sale SIEMPRE, también apagado: un panel que enseña decisiones sin
        // aclarar que no se ejecutan es un panel que miente.
        p.Aviso(HyprCatalogo.AvisoDeSimulacion(modo));

        // ---------- Los tres interruptores son una escalera, no tres opciones ----------
        //
        // «Mover ventanas de verdad» y «Preguntar antes de mover» no significan nada con la
        // capa apagada: con `Activo` en false, `ModoEfectivo` devuelve `Apagado` pase lo que
        // pase. Dejarlos pulsables tal cual es ofrecer dos interruptores que no hacen nada.
        //
        // Se desactivan, no se esconden. Es la regla que este proyecto ya tomó con el panel
        // de red —un botón desactivado con su motivo enseña que la función existe y qué falta
        // para tenerla; uno escondido hace creer que no existe— y aquí importa más, porque lo
        // que se está escondiendo sería la forma de que NubeHypr mueva ventanas.
        //
        // En vivo, no al abrir la página: se declaran antes para que el lambda de «Activar»
        // los alcance. Si solo se decidiera al construir, encender la capa dejaría los otros
        // dos muertos hasta cerrar y reabrir Ajustes, que es una trampa peor que la que se
        // arregla.
        // Y desactivar son CUATRO cosas, no una. La primera versión de esto solo ponía
        // `IsEnabled`, y la captura lo delató: se veían exactamente igual que el de arriba,
        // porque `Interruptor` fija `Foreground` en el propio control y un valor local gana
        // al atenuado que WPF aplica por plantilla a lo desactivado. Un interruptor que
        // parece disponible y no responde es peor que uno que se ofrece de más.
        //
        // Las cuatro son las mismas que usa el panel de red en `AplicarEstadoALosBotones`:
        // apagarlo, atenuarlo, decir el motivo al pasar el ratón y —lo que se olvida siempre—
        // decirlo también en el nombre de UIA, porque quien va por lector de pantalla no ve
        // ni el gris ni la ayuda emergente.
        // ---------- Y el modo de demostración manda sobre los dos ----------
        //
        // Con `--guia`, `DiagGuia.Apagar` fuerza el modo a «simulacion» en memoria aunque el
        // fichero diga «aplicar»: en demostración no puede haber ningún camino que mueva
        // ventanas. Pero este interruptor lee el ÁRBOL, que es el fichero crudo, así que
        // sin esto se vería encendido mientras el producto está clavado en simulación.
        //
        // Sería la misma mentira que acabo de exigir en la prueba: la interfaz tiene que
        // enseñar lo que VA A PASAR, no lo que hay escrito.
        // Fuera del lambda y recibiendo la demostración por parámetro, no leyendo el global:
        // así se puede exigir en una prueba sin tocar `DiagGuia.Encendido`, que es estado
        // compartido de todo el proceso.
        CheckBox? swMover = null, swPreguntar = null;

        static void Atenuar(CheckBox? c, string? porQueNo, string nombre)
        {
            if (c == null) return;
            bool se = porQueNo == null;
            c.IsEnabled = se;
            c.Opacity = se ? 1.0 : 0.45;
            c.ToolTip = se ? null : $"No disponible: {porQueNo}";
            System.Windows.Automation.AutomationProperties.SetHelpText(c, porQueNo ?? "");
            System.Windows.Automation.AutomationProperties.SetName(
                c, se ? nombre : $"{nombre} (no disponible: {porQueNo})");
        }

        void LosDeAbajo(bool activo)
        {
            var no = PorQueNoSeTocaElModo(activo, DiagGuia.Encendido);
            Atenuar(swMover, no, "Mover ventanas de verdad");
            Atenuar(swPreguntar, no, "Preguntar antes de mover ventanas");
        }

        p.Mas(Interruptor("Activar NubeHypr", _hypr.NubeHypr.Activo, v =>
        {
            _hypr.NubeHypr.Activo = v;
            // Por el árbol, no por el modelo: así las claves de `NubeHypr` que esta
            // versión no conozca siguen ahí después de guardar.
            _arbol.Poner("NubeHypr", "Activo", v);
            LosDeAbajo(v);
        }));
        p.Pista("Mientras el modo sea «simulación», encenderlo no mueve ninguna ventana: " +
                "solo deja que las reglas decidan y lo apunten en el registro.");

        // ================= Y el interruptor que NO existía =================
        //
        // `NubeHypr.Modo` tenía 175 usos en el producto —es la clave que decide si la capa
        // mueve ventanas o solo lo apunta— y **cero controles en Ajustes**. El único
        // interruptor de esta página era «Activar», que pone `Activo`; el modo se quedaba en
        // «simulacion» para siempre.
        //
        // Dicho de otra forma: D9.5, D9.6 y todo lo que mueve ventanas de verdad era
        // **inalcanzable** salvo editando el `config.json` a mano. Y la guía de pruebas decía
        // «pon el modo en aplicar», una instrucción que no se podía seguir.
        //
        // Lo encontró el mismo barrido que el dock y el hueco, con una vuelta de tuerca: la
        // primera pasada comparaba por NOMBRE de clave y dio falso negativo, porque
        // `Barra.Modo` —isla o barra— sí tiene desplegable. Hubo que barrer por sección.
        //
        // ---------- Por qué un interruptor y no un desplegable ----------
        //
        // `Opciones` enseña los valores crudos en un ComboBox: el usuario leería «simulacion»
        // y «aplicar» tal cual. Para la decisión más consecuente del programa eso es poco:
        // aquí lo que se pregunta es «¿quieres que mueva ventanas de verdad?», y eso es sí o
        // no. Además un interruptor no puede quedarse en un valor que no signifique nada,
        // que es lo que `ModoEfectivo` tiene que ir limpiando.
        // Y en demostración se enseña APAGADO aunque el fichero diga «aplicar», porque es lo
        // que el producto va a hacer: `DiagGuia.Apagar` lo clava en simulación.
        swMover = (CheckBox)Interruptor("Mover ventanas de verdad",
                                        !DiagGuia.Encendido &&
                                        AplicandoDe(_arbol.Texto("NubeHypr", "Modo", "simulacion")),
                                        v =>
        {
            _hypr.NubeHypr.Modo = ModoConfigDe(v);
            _arbol.Poner("NubeHypr", "Modo", ModoConfigDe(v));
        });
        p.Mas(swMover);
        p.Pista("Apagado —como viene— NubeHypr calcula lo que haría y lo apunta en el " +
                "registro sin tocar ninguna ventana. Encendido, las mueve. Las reglas y los " +
                "atajos son los mismos en los dos casos: lo único que cambia es si se " +
                "ejecutan. Si el modo seguro entra por su cuenta, manda él y no se mueve nada.");

        // ================= Preguntar antes de mover =================
        //
        // Esta clave llevaba desde D0 en tu `config.json`, puesta a `true`, y **no la leía
        // nadie**: tres menciones en todo el proyecto —la declaración, el esquema y nada
        // más—. No era una clave sin usar, era una promesa de seguridad que el programa no
        // cumplía: quien abría el fichero leía «ConfirmarMovimientos: true» y entendía que
        // NubeHypr pregunta antes de tocarle las ventanas.
        swPreguntar = (CheckBox)Interruptor(
            "Preguntar antes de mover ventanas",
            _arbol.Bandera("NubeHypr", "ConfirmarMovimientos", true),
            v => _arbol.Poner("NubeHypr", "ConfirmarMovimientos", v));
        p.Mas(swPreguntar);

        // Y el estado de partida, ahora que los dos existen. Va aquí y no arriba por el
        // mismo motivo por el que se declararon antes: arriba todavía eran nulos.
        LosDeAbajo(_hypr.NubeHypr.Activo);
        p.Pista("Solo para lo que NubeHypr hace por su cuenta: una regla que se dispara al " +
                "abrir una ventana, o un reparto al cambiar de layout. Un atajo que acabas " +
                "de pulsar no pregunta, porque pulsarlo ya es la respuesta. La pregunta sale " +
                "en la isla con «Mover» y «Cancelar», y si no contestas no se mueve nada.");

        // ================= El hueco entre ventanas =================
        //
        // Esto llevaba desde D0 sin puerta. `NubeHypr.Gap` lo leen el reparto
        // (`NubeHypr.Calcular`), la vista general y las reglas —son cuatro sitios en
        // producción, no es una clave muerta—, pero la única forma de cambiarlo era editar el
        // `config.json` a mano.
        //
        // Es el mismo fallo que tenía el dock, y lo encontré igual: barriendo qué claves de
        // la configuración nadie escribe desde la interfaz. Un ajuste que solo se puede tocar
        // con un editor de texto es un ajuste que no existe para quien usa el programa; y de
        // todos los de esta capa, el hueco es precisamente el que uno quiere mover hasta que
        // le gusta, no el que calcula una vez.
        p.Mas(Deslizador("Hueco entre ventanas", 0, 32,
                         _arbol.Numero("NubeHypr", "Gap", 8),
                         v => _arbol.Poner("NubeHypr", "Gap", Math.Round(v))));
        p.Pista("Los píxeles que NubeHypr deja entre una ventana y otra al repartirlas, y " +
                "entre las ventanas y el filo de la pantalla. A 0 quedan pegadas. " +
                "Se aplica al próximo reparto, no recoloca lo que ya está puesto.");

        // ================= Qué puedes hacer =================
        //
        // Va ANTES de la lista de reglas y antes del editor, a propósito. La pregunta «¿qué
        // le puedo pedir a esto?» se hace al abrir la página, no después de pelearse con un
        // formulario de once campos. Una guía que haya que ir a buscar solo la encuentra
        // quien ya sabe que existe.
        p.Grupo("Qué puedes hacer", Ico.Nube,
                "Ocho ejemplos con aplicaciones de verdad. Cada uno dice qué campos se " +
                "rellenan y qué hace, y se puede poner en el formulario de un clic.");
        p.Mas(SeccionQuePuedesHacer());
        p.Pista("«Usar este ejemplo» solo rellena el formulario: la regla nace apagada y no " +
                "se guarda hasta que pulses Guardar. Nada de esto mueve ventanas.");

        // ================= Las reglas =================
        p.Grupo("Reglas", Ico.Zonas,
                "Qué hacer con una ventana según su proceso, su clase, su título o su ruta.");

        _buscarReglas = new TextBox
        {
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            Foreground = _texto,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 8),
        };
        _buscarReglas.TextChanged += (_, _) =>
        {
            _busquedaReglas = _buscarReglas!.Text;
            RepintarReglas();
        };
        p.Mas(ConRotulo("Buscar", _buscarReglas));
        p.Pista("Busca en el id, el proceso, la clase, el título, la ruta y también en lo que " +
                "la regla hace: «espacio 2», «flotante», «principal».");

        p.Mas(Interruptor("Enseñar solo las activas", _soloReglasActivas, v =>
        {
            _soloReglasActivas = v;
            RepintarReglas();
        }));

        _listaReglas = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        p.Mas(_listaReglas);

        // ================= Lo que decidiría ahora mismo =================
        p.Grupo("Qué pasaría con las ventanas de ahora", Ico.Apps,
                "La decisión de cada ventana abierta, con la regla que pone cada campo y el " +
                "motivo por el que las demás se descartan.");

        _listaDecisiones = new StackPanel();
        p.Mas(_listaDecisiones);
        p.Mas(BotonPlano("Calcular ahora", RepintarDecisiones, false));
        p.Pista("Se calcula cuando lo pides, no en bucle: resolver las reglas de todas las " +
                "ventanas cada segundo sería trabajo para no enseñar nada nuevo.");

        // ================= Y los atajos, con búsqueda =================
        p.Grupo("Atajos de NubeHypr", Ico.Teclado,
                "Los 19 comandos. El editor completo está en la página de Atajos.");
        p.Mas(ResumenDeAtajos());
        p.Pista("Para reasignarlos, ver cuáles están cogidos por otra aplicación y guardarlos, " +
                "ve a Sistema → Atajos.");

        RepintarReglas();
        return p.Fin();
    }

    // ---------- La cabecera con la nube ----------
    //
    // La nube de `Nubecita`, del tamaño que decide `HyprPinta` y con sus topes: es la
    // identidad de la casa y aquí tiene sitio para verse, pero no puede crecer hasta
    // convertirse en una mascota que se come la página.
    UIElement CabeceraDeNubeHypr(NubeHypr.Modo modo)
    {
        double lado = HyprPinta.TamanoDeLaNube(140);
        var azul = HyprPinta.Azul(_roles);
        var coral = HyprPinta.Coral(_roles);

        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nube = new System.Windows.Shapes.Path
        {
            Data = Nubecita.Molde(),
            Fill = new SolidColorBrush(modo == NubeHypr.Modo.Apagado
                ? Color.FromArgb(0x66, azul.R, azul.G, azul.B)
                : azul),
            Width = lado, Height = lado * Nubecita.Proporcion(),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        fila.Children.Add(nube);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titulo = Rotulo(modo switch
        {
            NubeHypr.Modo.Apagado => "Apagado",
            NubeHypr.Modo.Simulacion => "En simulación",
            _ => "Aplicando",
        }, titulo: true);
        // El azul dice "esto es lo normal"; el coral, "cuidado". `Aplicar` es el único
        // estado que de verdad toca tus ventanas, y es el único que sale en coral.
        titulo.Foreground = new SolidColorBrush(modo == NubeHypr.Modo.Aplicar
            ? HyprPinta.CoralTinta(_roles, _roles.SuperficieSolida)
            : HyprPinta.AzulTinta(_roles, _roles.SuperficieSolida));
        textos.Children.Add(titulo);

        var cuenta = Rotulo($"{_hypr.NubeHypr.Reglas.Count} regla" +
                            $"{(_hypr.NubeHypr.Reglas.Count == 1 ? "" : "s")}" +
                            $"  ·  {_hypr.NubeHypr.Atajos.Count} atajo" +
                            $"{(_hypr.NubeHypr.Atajos.Count == 1 ? "" : "s")} guardado" +
                            $"{(_hypr.NubeHypr.Atajos.Count == 1 ? "" : "s")}");
        cuenta.Opacity = 0.7;
        cuenta.FontSize = 11.5;
        textos.Children.Add(cuenta);

        // ---------- Qué significa el estado, en una frase ----------
        //
        // "Aplicando" en coral dice que hay cuidado, no QUÉ cuidado. Desde D9.4 un atajo
        // pulsado en ese estado mueve ventanas de verdad, y eso tiene que estar escrito aquí
        // y no solo en un documento: el sitio donde se lee el estado es el sitio donde hay
        // que enterarse de lo que hace.
        var queSignifica = Rotulo(modo switch
        {
            NubeHypr.Modo.Apagado => "No se evalúa nada: ni reglas ni atajos.",
            NubeHypr.Modo.Simulacion =>
                "Los atajos y las reglas deciden y se apuntan, pero NO se mueve ninguna ventana.",
            _ => "Los atajos mueven ventanas de verdad. Las reglas, todavía no.",
        });
        queSignifica.Opacity = 0.7;
        queSignifica.FontSize = 11.5;
        queSignifica.TextWrapping = TextWrapping.Wrap;
        queSignifica.Margin = new Thickness(0, 3, 0, 0);
        textos.Children.Add(queSignifica);

        Grid.SetColumn(textos, 1);
        fila.Children.Add(textos);
        return fila;
    }

    // ---------- La lista de reglas, agrupada ----------
    void RepintarReglas()
    {
        if (_listaReglas == null) return;
        _listaReglas.Children.Clear();

        var todas = Reglas;      // la copia que edita D9.3, no la del fichero
        if (todas.Count == 0)
        {
            _listaReglas.Children.Add(VacioConNube(
                "Todavía no hay ninguna regla",
                "Una regla dice qué hacer con una ventana cuando aparece: a qué espacio va, " +
                "en qué monitor, si flota o qué tamaño toma."));
            _listaReglas.Children.Add(BotonPlano("Añadir una regla", AnadirRegla, true));
            return;
        }

        var encontradas = HyprCatalogo.Buscar(todas, _busquedaReglas, _soloReglasActivas);
        if (encontradas.Count == 0)
        {
            var nada = Rotulo($"Ninguna de las {todas.Count} reglas coincide con «{_busquedaReglas}»");
            nada.Opacity = 0.7;
            _listaReglas.Children.Add(nada);
            return;
        }

        foreach (var g in HyprCatalogo.Agrupar(encontradas))
        {
            var cab = new Grid { Margin = new Thickness(0, 10, 0, 4) };
            cab.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nombre = Rotulo(g.Nombre);
            nombre.FontWeight = FontWeights.SemiBold;
            // El grupo de las que no miran nada va en coral: no es un grupo más, es una
            // bandeja de reglas a medio escribir que nunca se van a aplicar.
            if (g.Nombre == HyprCatalogo.GrupoSinCriterios)
                nombre.Foreground = new SolidColorBrush(HyprPinta.CoralTinta(_roles, _roles.SuperficieSolida));
            cab.Children.Add(nombre);

            var resumen = Rotulo(g.Resumen);
            resumen.Opacity = 0.6;
            resumen.FontSize = 11;
            Grid.SetColumn(resumen, 1);
            cab.Children.Add(resumen);
            _listaReglas.Children.Add(cab);

            foreach (var r in g.Reglas) _listaReglas.Children.Add(FilaDeRegla(r));
        }

        // Al final, no al principio: añadir es lo que se hace DESPUÉS de mirar lo que ya
        // hay, y un botón de añadir arriba invita a crear una regla que ya existe.
        var pie = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        pie.Children.Add(BotonPlano("Añadir una regla", AnadirRegla, true));
        _listaReglas.Children.Add(pie);
    }

    FrameworkElement FilaDeRegla(HyprReglas.Regla r)
    {
        bool valida = HyprReglas.Valida(r, out string porque);

        var caja = new Border
        {
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(11, 8, 11, 8),
            Margin = new Thickness(0, 0, 0, 5),
            // El filo de la izquierda es el estado: azul si está activa, coral si no se
            // puede aplicar, y nada si está apagada. Y además lo dice el texto, porque el
            // color no puede ser el único portador del estado.
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(
                !valida ? HyprPinta.Coral(_roles)
                : r.Activa ? HyprPinta.Azul(_roles)
                : Colors.Transparent),
        };

        var filas = new StackPanel { Opacity = r.Activa ? 1.0 : 0.55 };

        var arriba = new Grid();
        arriba.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        arriba.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var id = Rotulo(r.Id);
        id.FontWeight = FontWeights.SemiBold;
        arriba.Children.Add(id);

        var estado = Rotulo(!valida ? "no se puede aplicar" : r.Activa ? "activa" : "apagada");
        estado.FontSize = 11;
        estado.Opacity = 0.75;
        Grid.SetColumn(estado, 1);
        arriba.Children.Add(estado);
        filas.Children.Add(arriba);

        var detalle = Rotulo(r.ToString());
        detalle.FontSize = 11;
        detalle.Opacity = 0.7;
        detalle.TextWrapping = TextWrapping.Wrap;
        filas.Children.Add(detalle);

        if (!valida)
        {
            var mal = Rotulo(porque);
            mal.FontSize = 11;
            mal.Foreground = new SolidColorBrush(HyprPinta.CoralTinta(_roles, _roles.SuperficieAltaSolida));
            mal.TextWrapping = TextWrapping.Wrap;
            filas.Children.Add(mal);
        }

        // D9.3: los botones, y el formulario si esta es la que se está editando. El
        // formulario va DENTRO de la fila y no en una ventana aparte: una regla se escribe
        // mirando las que ya hay, y un diálogo encima tapa justo eso.
        filas.Children.Add(AccionesDeRegla(r));
        if (_editando == r.Id)
        {
            filas.Opacity = 1;      // mientras la editas se ve entera, aunque esté apagada
            filas.Children.Add(FormularioDeRegla(r));
            caja.Background = new SolidColorBrush(_roles.SuperficieAltaSolida);
        }

        caja.Child = filas;
        System.Windows.Automation.AutomationProperties.SetName(
            caja, $"Regla {r.Id}, {(valida ? (r.Activa ? "activa" : "apagada") : "no se puede aplicar")}");
        return caja;
    }

    // ---------- Qué decidiría cada ventana de ahora ----------
    //
    // Se construye desde el MISMO `HyprVigilante.Apunte` que se escribe en el registro, no
    // con una cuenta propia: si se recalculara aquí, el día que una de las dos cambiara
    // habría dos verdades y ninguna forma de saber cuál se aplica.
    void RepintarDecisiones()
    {
        if (_listaDecisiones == null) return;
        _listaDecisiones.Children.Clear();

        var reglas = HyprReglas.Instantanea(_hypr.NubeHypr.Reglas);
        if (reglas.Count == 0)
        {
            var nada = Rotulo("Sin reglas no hay nada que decidir: todas las ventanas se quedan " +
                              "donde las ponga Windows.");
            nada.Opacity = 0.7;
            nada.TextWrapping = TextWrapping.Wrap;
            _listaDecisiones.Children.Add(nada);
            return;
        }

        List<HyprVigilante.Apunte> apuntes;
        try
        {
            var obs = HyprAdaptador.Observar();
            var rutas = new Dictionary<IntPtr, string>();
            foreach (var w in GestorVentanas.Todas) rutas[w.Hwnd] = w.Exe;

            apuntes = obs.Estado.Ventanas
                .Select(v => HyprVigilante.Examinar(v, reglas, Nativo.Clase(v.Hwnd),
                                                    rutas.GetValueOrDefault(v.Hwnd, "")))
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error("calcular las decisiones para el panel de NubeHypr", ex);
            _listaDecisiones.Children.Add(Rotulo("No se pudo calcular: míralo en el registro."));
            return;
        }

        if (apuntes.Count == 0)
        {
            _listaDecisiones.Children.Add(Rotulo("No hay ninguna ventana organizable abierta."));
            return;
        }

        foreach (var a in apuntes)
        {
            var caja = new Border
            {
                Background = new SolidColorBrush(_roles.SuperficieAlta),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(3, 0, 0, 0),
                BorderBrush = new SolidColorBrush(a.AlgunaCoincidio
                    ? HyprPinta.Azul(_roles) : Colors.Transparent),
            };
            var pila = new StackPanel();

            foreach (var fila in HyprCatalogo.Explicar(a))
            {
                var t = Rotulo(fila.Texto);
                t.TextWrapping = TextWrapping.Wrap;
                switch (fila.Que)
                {
                    case HyprCatalogo.Tipo.Cabecera:
                        t.FontWeight = FontWeights.SemiBold;
                        break;
                    case HyprCatalogo.Tipo.Puesto:
                        t.FontSize = 11.5;
                        t.Foreground = new SolidColorBrush(
                            HyprPinta.AzulTinta(_roles, _roles.SuperficieAltaSolida));
                        break;
                    case HyprCatalogo.Tipo.SinOpinion:
                    case HyprCatalogo.Tipo.NoCoincide:
                        t.FontSize = 11;
                        t.Opacity = 0.6;
                        break;
                    case HyprCatalogo.Tipo.Nada:
                        t.FontSize = 11.5;
                        t.Opacity = 0.8;
                        break;
                    default:
                        t.FontSize = 11;
                        t.Opacity = 0.85;
                        break;
                }
                pila.Children.Add(t);
            }

            caja.Child = pila;
            _listaDecisiones.Children.Add(caja);
        }

        // Y el recordatorio, otra vez, al pie de la lista: es donde acaba de leer el
        // usuario, y es donde más fácil sería pensar que esto ya está pasando.
        var recuerda = Rotulo("Nada de esto se ha aplicado: ninguna ventana se ha movido.");
        recuerda.FontSize = 11;
        recuerda.Opacity = 0.75;
        recuerda.Margin = new Thickness(0, 6, 0, 0);
        _listaDecisiones.Children.Add(recuerda);
    }

    // ---------- Los atajos, agrupados por familia ----------
    FrameworkElement ResumenDeAtajos()
    {
        var pila = new StackPanel();
        var mapa = HyprTeclas.MapaDe(_hypr.NubeHypr, _hypr.Barra.AtajoVistaGeneral);
        var porNombre = new Dictionary<string, string>();
        foreach (var (combo, nombre) in mapa.Atajos) porNombre[nombre] = combo;

        foreach (var g in HyprCatalogo.AgruparAtajos(HyprAtajos.Nombres))
        {
            var cab = Rotulo(g.Familia);
            cab.FontWeight = FontWeights.SemiBold;
            cab.Margin = new Thickness(0, 9, 0, 3);
            pila.Children.Add(cab);

            foreach (var n in g.Nombres)
            {
                var fila = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var rot = Rotulo(HyprAtajos.Rotulo(n));
                rot.FontSize = 11.5;
                fila.Children.Add(rot);

                var combo = porNombre.GetValueOrDefault(n);
                var tecla = Rotulo(combo ?? "sin asignar");
                tecla.FontSize = 11;
                tecla.Opacity = combo == null ? 0.5 : 0.85;
                tecla.Foreground = combo == null
                    ? _texto
                    : new SolidColorBrush(HyprPinta.AzulTinta(_roles, _roles.SuperficieSolida));
                Grid.SetColumn(tecla, 1);
                fila.Children.Add(tecla);
                pila.Children.Add(fila);
            }
        }
        return pila;
    }

    // ---------- El vacío, con la nube ----------
    //
    // Un panel vacío que solo dice "no hay nada" es una caja genérica, que es lo que el
    // documento pide no enseñar. Con la nube y una frase de qué es esto, el vacío explica.
    FrameworkElement VacioConNube(string titulo, string explicacion)
    {
        var pila = new StackPanel { Margin = new Thickness(0, 14, 0, 10) };
        double lado = HyprPinta.TamanoDeLaNube(190);

        pila.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Nubecita.Molde(),
            Fill = new SolidColorBrush(Color.FromArgb(
                0x55, HyprPinta.Azul(_roles).R, HyprPinta.Azul(_roles).G, HyprPinta.Azul(_roles).B)),
            Width = lado, Height = lado * Nubecita.Proporcion(),
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
        });

        var t = Rotulo(titulo);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.FontWeight = FontWeights.SemiBold;
        pila.Children.Add(t);

        var e = Rotulo(explicacion);
        e.HorizontalAlignment = HorizontalAlignment.Center;
        e.TextAlignment = TextAlignment.Center;
        e.TextWrapping = TextWrapping.Wrap;
        e.FontSize = 11.5;
        e.Opacity = 0.7;
        e.MaxWidth = 420;
        e.Margin = new Thickness(0, 4, 0, 0);
        pila.Children.Add(e);
        return pila;
    }

    // Un campo con su rótulo encima, que es el patrón del resto de la ventana.
    FrameworkElement ConRotulo(string etiqueta, FrameworkElement control)
    {
        var pila = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        var r = Rotulo(etiqueta);
        r.FontSize = 11.5;
        r.Opacity = 0.8;
        r.Margin = new Thickness(0, 0, 0, 4);
        pila.Children.Add(r);
        pila.Children.Add(control);
        return pila;
    }
}
