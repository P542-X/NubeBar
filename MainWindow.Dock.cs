using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// E2: el dock, como superficie propia.
//
// ---------- Por qué una ventana aparte y no un módulo más ----------
//
// Porque el documento de E pide que **se pueda esconder y mostrar sin reconstruir el resto
// del escritorio**. Un módulo dentro de la barra comparte su ciclo de vida: cada vez que el
// dock cambiara, `RemontarZonas` tiraría los quince widgets y los volvería a construir.
//
// Siendo ventana propia, el dock se refresca solo. El contador `RemontajesDeZonas` que mide
// `--vigilia` tiene que quedarse quieto mientras el dock se actualiza, y hay diagnóstico que
// lo comprueba.
//
// ---------- Qué reutiliza ----------
//
// `VentanaSuperpuesta`, que ya es lo que hace falta: ventana por capas, sin foco, fuera de
// Alt+Tab, colocada en píxeles de pantalla y —desde E0.1— con los estilos de la isla. Y el
// comportamiento de las apps, que es el del carrusel desde la fase 1.
public partial class MainWindow
{
    VentanaSuperpuesta? _dock;
    StackPanel? _dockFila;
    string _huellaDock = "";
    bool _ratonEnElDock;

    // Solo la isla principal tiene dock. Con dos pantallas, dos docks idénticos compitiendo
    // por las mismas apps es ruido, no una función.
    bool MeTocaElDock => _esJefa && !_cerrando;

    NubeHypr.Borde BordeDelDock => Dock.BordeDelDock(BordeDeLaBarra);

    // El borde de la barra, en el lenguaje de NubeHypr. La barra lo guarda como texto.
    NubeHypr.Borde BordeDeLaBarra => PosicionEfectiva.ToLowerInvariant() switch
    {
        "abajo" => NubeHypr.Borde.Abajo,
        "izquierda" => NubeHypr.Borde.Izquierda,
        "derecha" => NubeHypr.Borde.Derecha,
        _ => NubeHypr.Borde.Arriba,
    };

    // ---------- El área útil de ESTA pantalla ----------
    //
    // En píxeles y del monitor de esta isla, no del escritorio entero: con dos pantallas, un
    // dock colocado en coordenadas del escritorio aparecería en la de al lado.
    NubeHypr.Rect UtilDeEstaPantalla()
    {
        var m = _monitor;
        return new NubeHypr.Rect(m.TrabajoX, m.TrabajoY, m.TrabajoAncho, m.TrabajoAlto);
    }

    // ================= Refrescar =================
    //
    // Se llama desde el Tick de siempre. No construye nada si lo que se dibujaría es idéntico
    // a lo que hay: con el dock refrescándose cada segundo y sin esta guarda, cada segundo se
    // tirarían y recrearían los iconos, que es justo lo que E2 promete no hacer.
    void RefrescarDock()
    {
        if (!MeTocaElDock) return;
        try
        {
            var cuando = Dock.Leer(_cfg.Barra.Dock);
            if (cuando == Dock.Cuando.Nunca) { QuitarDock(); return; }

            var elementos = Dock.Montar(_cfg.Barra.AppsFijadas, VentanasDeEstaPantalla());

            // Un dock vacío no se enseña: sería una pastilla negra con nada dentro.
            if (elementos.Count == 0) { QuitarDock(); return; }

            // La misma guardia que los laterales: ver `MuestrearBordes`. Un juego a pantalla
            // completa que te clava el puntero contra el filo de abajo no saca el dock.
            bool cerca = Dock.RatonCerca(UtilDeEstaPantalla(), BordeDelDock,
                                         PuntoRaton().X, PuntoRaton().Y)
                      && GestoEnElBorde(BordeDelDock);
            if (!Dock.SeVe(cuando, cerca, _ratonEnElDock)) { QuitarDock(); return; }

            // ---------- La huella ----------
            //
            // Lleva lo que decide el dibujo —qué apps, cuántas ventanas, cuál está delante— y
            // nada más. Con el título de las ventanas dentro, escribir en el navegador
            // reconstruiría el dock en cada tecla.
            var huella = string.Join("|", elementos.Select(e => e.ToString())) +
                         $"@{BordeDelDock}";
            bool hayQueRehacer = huella != _huellaDock || _dock == null;

            MontarDock(elementos, hayQueRehacer);
            _huellaDock = huella;
        }
        catch (Exception ex) { Log.Error("refrescar el dock", ex); }
    }

    List<VentanaReal> VentanasDeEstaPantalla() =>
        _cfg.Barra.AppsPorMonitor
            ? GestorVentanas.Todas.Where(v => v.Monitor == _monitor.Handle).ToList()
            : GestorVentanas.Todas.ToList();

    void MontarDock(List<Dock.Elemento> elementos, bool rehacer)
    {
        var v = _dock ??= CrearDock();

        if (rehacer)
        {
            _dockFila!.Children.Clear();
            _dockFila.Orientation = Dock.EsVertical(BordeDelDock)
                ? Orientation.Vertical : Orientation.Horizontal;

            foreach (var e in elementos)
                _dockFila.Children.Add(e.EsSeparador ? SeparadorDelDock() : IconoDelDock(e));
        }

        // Colocar siempre: aunque el contenido no cambie, el monitor puede haber cambiado de
        // tamaño o la barra de borde.
        Escalar(v.Tarjeta, EscalaDePaneles());
        var r = SitioDelDock(elementos.Count);
        v.Colocar((int)r.X, (int)r.Y, (int)r.Ancho, (int)r.Alto);
        if (!v.IsVisible)
        {
            v.Show();
            // Una entrada breve, y solo al aparecer. Animar en cada refresco sería un dock
            // que parpadea una vez por segundo.
            v.Tarjeta.BeginAnimation(OpacityProperty, null);
            v.Tarjeta.Opacity = 1;
            // E6: del catálogo, como el panel lateral.
            v.Tarjeta.BeginAnimation(OpacityProperty,
                Kf(0, 1, Transiciones.Duracion(Transiciones.Que.PanelEntra).TotalMilliseconds));
        }
        v.Elevar();
    }

    // ---------- Dónde va, en PÍXELES ----------
    //
    // `Dock.Sitio` cuenta con las medidas del dock —iconos de 44, relleno de 10— que son
    // unidades de WPF, y lo que sale se le pasa a `Colocar`, que va en píxeles. Al 125 % la
    // ventana quedaba un 20 % más pequeña que su contenido y los iconos se veían enormes y
    // apretados. Lo contó el usuario: «en el dock ahora se ve como muy zoomeado». (Antes no
    // se notaba por casualidad: la primera colocación, sin hwnd, metía esos números como
    // unidades de WPF y compensaba el error; ver `VentanaSuperpuesta.Colocar`.)
    //
    // Mismo arreglo que los laterales: las medidas se pasan a píxeles con la escala del
    // monitor, y el dock se compacta con `EscalaDePaneles` como el resto de superficies.
    NubeHypr.Rect SitioDelDock(int cuantos)
    {
        double s = Math.Max(0.1, _monitor.Escala);
        double k = s * EscalaDePaneles();
        return Dock.Sitio(UtilDeEstaPantalla(), BordeDelDock, cuantos,
                          Dock.Lado * k, Dock.Hueco * k, Dock.Aire * k, Dock.DelFilo * s);
    }

    VentanaSuperpuesta CrearDock()
    {
        var v = new VentanaSuperpuesta(this);
        v.Tarjeta.Background = _fondo;
        v.Tarjeta.BorderBrush = _acento;
        v.Tarjeta.BorderThickness = Isla.BorderThickness;
        v.Tarjeta.CornerRadius = Isla.CornerRadius;
        v.Tarjeta.Padding = new Thickness(Dock.Aire);

        _dockFila = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        v.Cajon.Content = _dockFila;

        // Que el dock sepa si tiene el ratón encima es lo que impide que se esconda justo
        // cuando vas a pulsar un icono, en el modo «al acercarse».
        v.MouseEnter += (_, _) => _ratonEnElDock = true;
        v.MouseLeave += (_, _) => { _ratonEnElDock = false; SinLupa(); };

        // El dock se apunta en el tablero de E0 como la superficie que es. No reserva área de
        // trabajo: flota por encima, que es lo que deja el escritorio amplio.
        _mesa.Abrir(Superficies.Nueva("dock", Superficies.Clase.Dock, DuenoDeLaMesa));
        Log.Info($"dock creado en {BordeDelDock}");
        return v;
    }

    void QuitarDock()
    {
        if (_dock is not { } v) return;
        try
        {
            if (v.IsVisible) v.Hide();
            _mesa.Cerrar("dock");
            _huellaDock = "";
        }
        catch (Exception ex) { Log.Error("esconder el dock", ex); }
    }

    // Al cerrar NubeBar. La ventana es hija de la isla, así que se cerraría sola, pero el
    // tablero tiene que quedarse vacío: lo comprueba `--diage0`.
    void SoltarElDock()
    {
        try
        {
            _mesa.Cerrar("dock");
            _dock?.Close();
            _dock = null;
            _dockFila = null;
        }
        catch (Exception ex) { Log.Error("soltar el dock", ex); }
    }

    // ================= Un icono =================
    FrameworkElement IconoDelDock(Dock.Elemento e)
    {
        var pila = new Grid { Width = Dock.Lado, Height = Dock.Lado };

        var icono = Legible(new Image
        {
            Source = IconoDeApp(e.Clave),
            Width = 28, Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            // Una app cerrada se ve, pero apagada: está en el dock porque la anclaste, no
            // porque esté pasando algo con ella.
            Opacity = e.Abierta ? 1.0 : 0.45,
        });
        pila.Children.Add(icono);

        // ---------- El indicador ----------
        //
        // Una rayita debajo (o al lado, en vertical). Azul si es la de delante, atenuada si
        // solo está abierta, nada si está cerrada. Es la regla de D6: azul para el foco.
        if (e.Abierta)
        {
            // La decisión —color, opacidad y longitud— la toma `Dock.MarcaDe`, que es puro
            // y tiene pruebas. Aquí solo se pinta: si se decidiera también aquí, habría
            // dos sitios diciendo lo mismo y un día dirían cosas distintas.
            var marca = Dock.MarcaDe(e);
            bool vert = Dock.EsVertical(BordeDelDock);
            var raya = new Border
            {
                Background = new SolidColorBrush(e.Activa
                    ? HyprPinta.Azul(_roles)
                    : _roles.TextoAtenuado),
                CornerRadius = new CornerRadius(1.5),
                Width = vert ? 3 : marca.Largo,
                Height = vert ? marca.Largo : 3,
                HorizontalAlignment = vert ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                VerticalAlignment = vert ? VerticalAlignment.Center : VerticalAlignment.Bottom,
                Opacity = marca.Opacidad,
            };
            pila.Children.Add(raya);
        }

        var boton = Boton(pila, e.ParaLeer, () => ActivarDesdeElDock(e));
        boton.Padding = new Thickness(0);

        // ---------- El tamaño lo manda el dock, no la barra ----------
        //
        // `Boton` fija la altura al grosor de la BARRA (`Barra.Alto - 8`), que es lo correcto
        // para un módulo de la barra y lo incorrecto aquí: con una barra de 38 px daba
        // botones de 30, la celda del dock es de 44, y el indicador de «abierta» se quedaba
        // fuera del botón. No se veía porque una rayita recortada y una rayita que no existe
        // son lo mismo en una captura; se vio midiendo el botón (44x30).
        boton.Height = Dock.Lado;
        boton.Width = Dock.Lado;

        // ---------- El hueco va SOLO en el eje largo ----------
        //
        // Con el margen en los cuatro lados, cada botón ocupaba 44+6 = 50 px también de
        // grueso, y la tarjeta solo tiene 44 (`Lado + Aire*2` descuenta el aire, no el
        // hueco). El resultado: los iconos se recortaban por abajo y **el indicador de
        // «abierta» no se dibujaba**. Se vio midiendo los píxeles de la captura, no mirándola:
        // a ese tamaño, una rayita que falta y una rayita oscura se parecen mucho.
        double h = Dock.Hueco / 2;
        boton.Margin = Dock.EsVertical(BordeDelDock)
            ? new Thickness(0, h, 0, h)
            : new Thickness(h, 0, h, 0);
        System.Windows.Automation.AutomationProperties.SetName(boton, e.ParaLeer);

        // ---------- La lupa ----------
        //
        // El origen de la escala va en el borde por el que está pegado el dock: creciendo
        // desde el centro, el icono se saldría por fuera de la tarjeta tanto como crece.
        boton.RenderTransformOrigin = Dock.EsVertical(BordeDelDock)
            ? new Point(BordeDelDock == NubeHypr.Borde.Izquierda ? 0 : 1, 0.5)
            : new Point(0.5, BordeDelDock == NubeHypr.Borde.Arriba ? 0 : 1);
        boton.MouseEnter += (_, _) => Lupa(boton);

        boton.MouseRightButtonUp += (_, args) => { args.Handled = true; MenuDelDock(e); };
        return boton;
    }

    // ================= La lupa del dock =================
    //
    // La maqueta: «el icono bajo el ratón escala 1,32 y sube 6 px; SUS DOS VECINOS escalan
    // 1,12 y suben 2 px. Con muelle».
    //
    // Los vecinos son la mitad del efecto, y la que más cuesta de entender hasta que se ve:
    // sin ellos el icono de debajo del ratón pega un salto y el resto se queda quieto, que se
    // lee como un fallo. Con ellos la fila se comba, y eso se lee como una superficie que
    // responde al dedo.
    //
    // Se mira la POSICIÓN en la fila y no el elemento: los separadores también ocupan sitio y
    // no tienen que crecer, pero sí cuentan para saber quién es el vecino.
    void Lupa(FrameworkElement bajoElRaton)
    {
        if (_dockFila == null || !Animacion.Adorno) return;

        var hijos = _dockFila.Children.OfType<FrameworkElement>().ToList();
        int centro = hijos.IndexOf(bajoElRaton);
        if (centro < 0) return;

        for (int i = 0; i < hijos.Count; i++)
        {
            int d = Math.Abs(i - centro);
            CrecerEnElDock(hijos[i], d == 0 ? 1.32 : d == 1 ? 1.12 : 1.0,
                                     d == 0 ? 6 : d == 1 ? 2 : 0);
        }
    }

    // Al salir del dock, todos a su sitio. Va en el `MouseLeave` de la ventana entera y no en
    // el de cada icono: saliendo de uno para entrar en el de al lado, el `MouseLeave` del
    // primero llega DESPUÉS del `MouseEnter` del segundo, y la fila se quedaría plana justo
    // mientras pasas el ratón por encima.
    void SinLupa()
    {
        if (_dockFila == null) return;
        foreach (var h in _dockFila.Children.OfType<FrameworkElement>())
            CrecerEnElDock(h, 1.0, 0);
    }

    void CrecerEnElDock(FrameworkElement el, double escala, double sube)
    {
        if (el.RenderTransform is not TransformGroup g)
        {
            g = new TransformGroup();
            g.Children.Add(new ScaleTransform(1, 1));
            g.Children.Add(new TranslateTransform());
            el.RenderTransform = g;
        }
        var s = (ScaleTransform)g.Children[0];
        var t = (TranslateTransform)g.Children[1];

        // Hacia dónde es «subir» depende del borde: en el dock de arriba, subir es bajar.
        bool vert = Dock.EsVertical(BordeDelDock);
        double dx = vert ? (BordeDelDock == NubeHypr.Borde.Izquierda ? sube : -sube) : 0;
        double dy = vert ? 0 : (BordeDelDock == NubeHypr.Borde.Arriba ? sube : -sube);

        s.BeginAnimation(ScaleTransform.ScaleXProperty, Kf(s.ScaleX, escala, 220, 0, Muelle));
        s.BeginAnimation(ScaleTransform.ScaleYProperty, Kf(s.ScaleY, escala, 220, 0, Muelle));
        t.BeginAnimation(TranslateTransform.XProperty, Kf(t.X, dx, 220, 0, Muelle));
        t.BeginAnimation(TranslateTransform.YProperty, Kf(t.Y, dy, 220, 0, Muelle));
    }

    FrameworkElement SeparadorDelDock()
    {
        bool vert = Dock.EsVertical(BordeDelDock);
        return new Border
        {
            Background = new SolidColorBrush(_roles.Borde),
            Width = vert ? Dock.Lado * 0.5 : 1,
            Height = vert ? 1 : Dock.Lado * 0.5,
            Margin = new Thickness(vert ? 0 : 5, vert ? 5 : 0, vert ? 0 : 5, vert ? 5 : 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    // El icono de una app: de una ventana suya si está abierta, y si no, el del ejecutable.
    ImageSource? IconoDeApp(string clave)
    {
        try
        {
            var v = GestorVentanas.Todas.FirstOrDefault(
                w => string.Equals(Dock.Clave(w.App.Length > 0 ? w.App : w.Exe), clave,
                                   StringComparison.OrdinalIgnoreCase));
            if (v?.Icono != null) return v.Icono;
            if (v != null && v.Hwnd != IntPtr.Zero) return Nativo.Icono(v.Hwnd);
            // Cerrada: el icono del ejecutable que la fijada apunta a lanzar.
            var fijada = _cfg.Barra.AppsFijadas?.FirstOrDefault(
                f => string.Equals(Dock.Clave(ClaveDeFijada(f)), clave, StringComparison.OrdinalIgnoreCase));
            return fijada is { Length: > 0 } ? Iconos.De(fijada, 32) : null;
        }
        catch { return null; }
    }

    // ================= El menú contextual =================
    //
    // Las acciones las decide `Dock.MenuDe`, que es puro y tiene pruebas: a una app cerrada
    // no se le ofrece «Cerrar», y a una fijada no se le ofrece «Anclar». Aquí solo se pintan.
    void MenuDelDock(Dock.Elemento e)
    {
        try
        {
            var menu = new ContextMenu();
            foreach (var a in Dock.MenuDe(e))
            {
                var it = new MenuItem { Header = Dock.Rotulo(a, e) };

                // La destructiva en coral, como en todo el proyecto desde D6.
                if (Dock.EsDestructiva(a))
                    it.Foreground = new SolidColorBrush(
                        HyprPinta.CoralTinta(_roles, _roles.SuperficieSolida));

                var accion = a;
                it.Click += (_, _) => HacerEnElDock(accion, e);
                menu.Items.Add(it);
            }
            menu.IsOpen = true;
        }
        catch (Exception ex) { Log.Error("el menú del dock", ex); }
    }

    void HacerEnElDock(Dock.Accion a, Dock.Elemento e)
    {
        try
        {
            var suyas = GestorVentanas.Todas
                .Where(w => string.Equals(Dock.Clave(w.App.Length > 0 ? w.App : w.Exe), e.Clave,
                                          StringComparison.OrdinalIgnoreCase))
                .ToList();

            switch (a)
            {
                case Dock.Accion.Abrir:
                case Dock.Accion.Activar:
                    ActivarDesdeElDock(e);
                    break;

                case Dock.Accion.VerVentanas:
                    // La lista de ventanas de un programa ya existe: es la vista general
                    // filtrada por esa app. Un segundo selector sería otra cosa que mantener.
                    _filtroApp = e.Clave;
                    AbrirVista();
                    break;

                case Dock.Accion.Cerrar:
                    // Se cierran por las buenas (WM_CLOSE): cada programa decide si pregunta
                    // por lo que no has guardado. Matar el proceso perdería tu trabajo.
                    foreach (var w in suyas) Nativo.CerrarVentana(w.Hwnd);
                    Log.Info($"dock: cerradas {suyas.Count} ventana(s) de «{e.Clave}»");
                    break;

                case Dock.Accion.Fijar:
                case Dock.Accion.Soltar:
                    CambiarFijada(e.Clave, fijar: a == Dock.Accion.Fijar);
                    _huellaDock = "";       // que se rehaga en el siguiente refresco
                    break;
            }
        }
        catch (Exception ex) { Log.Error($"«{a}» sobre «{e.Clave}» desde el dock", ex); }
    }

    // ================= Lo que hace un clic =================
    void ActivarDesdeElDock(Dock.Elemento e)
    {
        try
        {
            var suyas = GestorVentanas.Todas
                .Where(w => string.Equals(Dock.Clave(w.App.Length > 0 ? w.App : w.Exe), e.Clave,
                                          StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (suyas.Count == 0)
            {
                // Cerrada: se lanza lo que la fijada apunte. Por el mismo camino que la tira
                // de fijadas, para que abrir desde el dock y desde la barra hagan lo mismo.
                var fijada = _cfg.Barra.AppsFijadas?.FirstOrDefault(
                    f => string.Equals(Dock.Clave(ClaveDeFijada(f)), e.Clave, StringComparison.OrdinalIgnoreCase));
                // ---------- ¿Cerrada, o solo escondida? ----------
                //
                // Lo mismo que en la tira de fijadas, y por el mismo motivo: una app que se
                // esconde en la bandeja sale de la lista de ventanas, y relanzar su
                // ejecutable no hace nada porque ya hay uno corriendo. Se busca su ventana
                // escondida y se enseña.
                //
                // Va aquí además de allí a propósito: abrir una app desde el dock y desde la
                // barra tienen que hacer lo mismo, y dejar esto en un solo sitio sería dejar
                // el otro con el fallo.
                if (fijada is { Length: > 0 })
                {
                    var oculta = EscondidaRecordadaPorClave(e.Clave);
                    if (oculta != IntPtr.Zero)
                    {
                        Log.Info($"dock: «{e.Clave}» estaba ESCONDIDA en 0x{oculta.ToInt64():X6}; " +
                                 "se enseña en vez de relanzarla");
                        Nativo.MostrarYActivar(oculta);
                        return;
                    }

                    Log.Info($"dock: abriendo la app fijada {fijada}");
                    Uso.Apuntar("dock:" + fijada);
                    Fuentes.Lanzar(fijada);
                    return;
                }

                // ---------- Y si no hay ni ventanas ni fijada, se DICE ----------
                //
                // Aquí había un `return` mudo. Un clic que no hace nada y no deja rastro es
                // imposible de explicar: ni el usuario sabe por qué, ni se puede mirar
                // después. Y pasa de verdad con las apps que al minimizarse **esconden** su
                // ventana en vez de minimizarla —Discord, Steam, casi todo lo que vive en la
                // bandeja—: salen de la lista, el icono del dock se queda un pulso sin
                // respaldo detrás, y pulsarlo no hace absolutamente nada.
                Log.Info($"dock: «{e.Clave}» no tiene ventanas y no está fijada: no hay nada " +
                         $"que abrir (ventanas vivas={GestorVentanas.Todas.Count}, " +
                         $"fijadas={_cfg.Barra.AppsFijadas?.Count ?? 0}). " +
                         "Si la app se esconde en la bandeja al minimizarse, se vuelve desde ahí.");
                return;
            }

            // ---------- Clic en la que ya está delante: se minimiza ----------
            //
            // Es lo que hace el carrusel desde la fase 1 y lo que espera cualquiera que venga
            // de una barra de tareas.
            //
            // Y con una condición más que no estaba: **que no esté ya minimizada**. Sin eso,
            // si la instantánea llegara a decir que una minimizada sigue activa, cada clic la
            // volvería a minimizar y la app parecería atascada — que es justo el síntoma de
            // «clico dos veces y ya no me deja abrirla». Hoy la instantánea se porta bien
            // (`--diagrejilla` lo comprueba), pero esto no depende de que se siga portando:
            // minimizar lo que ya está minimizado no es nunca lo que el usuario quiere.
            var activa = suyas.FirstOrDefault(w => w.Activa);
            if (activa != null && suyas.Count == 1 && activa.Estado != EstadoVentana.Minimizada)
            {
                Log.Info($"dock: «{e.Clave}» estaba delante y se minimiza");
                Nativo.Minimizar(activa.Hwnd);
                return;
            }

            var cual = activa == null ? suyas[0] : suyas[(suyas.IndexOf(activa) + 1) % suyas.Count];
            Log.Info($"dock: «{e.Clave}» → se activa 0x{cual.Hwnd.ToInt64():X6} " +
                     $"({suyas.Count} ventana(s), estado={cual.Estado})");
            Nativo.Activar(cual.Hwnd);
        }
        catch (Exception ex) { Log.Error($"activar «{e.Clave}» desde el dock", ex); }
    }
}
