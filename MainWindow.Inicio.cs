using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NubeBar;

// Menú Inicio propio: una caja de búsqueda y una lista de resultados que salen
// de varias fuentes a la vez (apps, ventanas abiertas, ajustes, archivos, cálculos,
// comandos y web). Lo rápido se calcula al teclear; los archivos van por detrás.
public partial class MainWindow
{
    const string PanelInicio = "inicio";
    const double AnchoMenu = 660, AltoFila = 44, FilasVisibles = 9;
    const int MaxFilas = 20;

    TextBox? _caja;
    StackPanel? _lista;
    Border? _marcaSeleccion;
    ScrollViewer? _scroll;
    TextBlock? _pista;

    List<Resultado> _resultados = new();
    int _seleccion;
    Categoria? _filtro;                 // Tab va rotando entre las categorías que hay
    CancellationTokenSource? _buscaArchivos;
    DispatcherTimer? _esperaArchivos;

    // Con la portada (lo que suena, accesos, gráficas, wallpaper, fijadas y energía)
    // hace falta bastante más alto que con la lista de resultados a secas
    double AltoCabecera => _cfg.Inicio.Saludo ? 58 : 0;
    double AltoMenu => AltoCabecera + 8 + 38 + 8 + AltoFila * FilasVisibles + 10;

    // ================= Tecla Windows =================
    // Hay un solo hook de teclado para todas las islas: lo monta la jefa y lo
    // comparten todas (viven en el mismo hilo de interfaz, así que no hay carrera)
    static GanchoTeclas? _gancho;

    void MontarTeclaWindows()
    {
        if (_gancho == null)
        {
            _gancho = new GanchoTeclas();
            // El hook avisa desde SU hilo: hay que saltar al de la interfaz.
            // El menú sale en la isla del monitor donde tengas el ratón.
            _gancho.AbrirMenu += () => Dispatcher.BeginInvoke(new Action(() => IslaDelRaton().MenuDesdeTecla()));
            _gancho.Instalar();
        }
        _gancho.Activo = _cfg.Inicio.TeclaWindows;
    }

    // Win sola (o Ctrl+Esc): si el menú está abierto se cierra, y si no se abre.
    // Con varias islas, el menú abierto en OTRA pantalla también cuenta como abierto:
    // pulsar Win se lo lleva, no abre un segundo menú.
    void MenuDesdeTecla()
    {
        foreach (var otra in Islas.Where(i => i != this && i._panelAbierto == PanelInicio).ToList())
        {
            _gancho!.Capturando = false;
            otra.VaciarCola(descartar: true);
            otra.CerrarMenu();
            return;
        }
        if (_panelAbierto == PanelInicio)
        {
            _gancho!.Capturando = false;
            VaciarCola(descartar: true);
            CerrarMenu();
            return;
        }
        AbrirMenuInicio();
    }

    // Traduce lo que el hook guardó mientras la caja no tenía el foco y lo mete
    // en orden. Se vacía dos veces: entre la primera y soltar la captura todavía
    // puede entrar alguna tecla más.
    void VaciarCola(bool descartar = false)
    {
        if (_gancho == null) return;

        for (int vuelta = 0; vuelta < 2; vuelta++)
        {
            while (_gancho.Cola.TryDequeue(out var p))
            {
                if (descartar || _caja == null) continue;
                AplicarPulsacion(p);
            }
            if (vuelta == 0) _gancho.Capturando = false;
        }
    }

    void AplicarPulsacion(GanchoTeclas.Pulsacion p)
    {
        if (_caja == null) return;
        switch (p.Vk)
        {
            case 0x08:                                   // retroceso
                if (_caja.Text.Length > 0) _caja.Text = _caja.Text[..^1];
                break;
            case 0x1B:                                   // escape
                CerrarMenu();
                break;
            case 0x0D:                                   // intro
                var v = Visibles;
                if (_seleccion < v.Count) Ejecutar(v[_seleccion], p.Ctrl);
                break;
            case 0x26: case 0x28:                        // flechas arriba/abajo
                var lista = Visibles;
                if (lista.Count > 0)
                {
                    _seleccion = p.Vk == 0x28
                        ? (_seleccion + 1) % lista.Count
                        : (_seleccion - 1 + lista.Count) % lista.Count;
                    MoverMarca();
                }
                break;
            default:
                var texto = Tipografia.Traducir(p);
                if (texto.Length > 0) _caja.Text += texto;
                break;
        }
        _caja.CaretIndex = _caja.Text.Length;
    }

    // ================= Abrir y cerrar =================
    void AbrirMenuInicio()
    {
        if (!AlternarPanel(PanelInicio, CrearMenuInicio, AnchoMenu, AltoMenu, teclado: true)) return;
        _filtro = null;
        CapturarClicsFuera(true);
        Dispatcher.InvokeAsync(() =>
        {
            _caja?.Focus();
            Keyboard.Focus(_caja);
            Buscar("");
            // Ya hay dónde escribir: se sueltan las teclas que el hook guardó
            // mientras se montaba el panel, en el mismo orden en que se pulsaron
            VaciarCola();
        }, DispatcherPriority.Input);
    }

    void CerrarMenu()
    {
        if (_panelAbierto != PanelInicio) return;
        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") == "1")
            Log.Info("DIAG cerrar menú desde: " +
                     string.Join(" <- ", new System.Diagnostics.StackTrace(1, false).GetFrames()
                        .Take(5).Select(f => f.GetMethod()?.Name ?? "?")));
        CapturarClicsFuera(false);
        _buscaArchivos?.Cancel();
        _esperaArchivos?.Stop();
        CerrarPanel();
    }

    // El menú NO se cierra por perder el foco. Wallpaper Engine se lo devuelve a sí
    // mismo a los pocos segundos y cerraba el menú solo mientras escribías. El criterio
    // bueno es el que pediste: un clic de verdad fuera. Se consigue capturando el ratón
    // en el subárbol de la isla, que es como se comportan los menús de Windows.
    void CapturarClicsFuera(bool activar)
    {
        if (activar)
        {
            Isla.PreviewMouseDown += ClicQuizaFuera;
            Isla.PreviewMouseUp += RearmarCaptura;
            Isla.LostMouseCapture += RecuperarCaptura;
            Mouse.Capture(Isla, CaptureMode.SubTree);
        }
        else
        {
            Isla.PreviewMouseDown -= ClicQuizaFuera;
            Isla.PreviewMouseUp -= RearmarCaptura;
            Isla.LostMouseCapture -= RecuperarCaptura;
            if (ReferenceEquals(Mouse.Captured, Isla)) Mouse.Capture(null);
        }
    }

    void ClicQuizaFuera(object remitente, MouseButtonEventArgs e)
    {
        if (_panelAbierto != PanelInicio) return;
        var p = e.GetPosition(Isla);
        bool dentro = p.X >= 0 && p.Y >= 0 && p.X <= Isla.ActualWidth && p.Y <= Isla.ActualHeight;
        if (dentro) return;
        CerrarMenu();
        e.Handled = true;
    }

    // La captura del ratón no basta cuando hay un juego delante: en cuanto otra
    // aplicación se activa, Windows nos quita la captura y el clic de fuera ya no
    // llega. Así que el clic fuera se mira también a pelo, cada 150 ms, preguntando
    // por el botón y por dónde está el puntero. InputHitTest acierta hasta en las
    // esquinas redondeadas, que es más de lo que daría comparar rectángulos.
    bool _clicContado;
    bool _tuvoFoco;        // el menú ha llegado a tener el primer plano alguna vez

    void VigilarClicFueraDelMenu()
    {
        if (_panelAbierto != PanelInicio) { _clicContado = false; _tuvoFoco = false; return; }

        // 1) ¿Se ha llevado el primer plano una aplicación de verdad? Entonces has
        //    hecho clic en ella y el menú sobra. Esto va aquí, cada 150 ms, y no en
        //    el tick de un segundo: tardaba dos segundos largos en enterarse.
        //    Ojo: solo si el menú LLEGÓ a tener el foco. Con un juego a pantalla
        //    completa puede que no nos lo suelte nunca, y entonces cerrar aquí sería
        //    cerrar el menú nada más abrirlo.
        var otra = Nativo.Activa();
        if (otra == _self || Nativo.EsNuestra(otra)) { _tuvoFoco = true; return; }
        if (_tuvoFoco && !SeLoDevuelveSolo(otra))
        {
            Log.Info($"el primer plano se ha ido a \"{Nativo.Titulo(otra)}\": el menú se cierra");
            CerrarMenu();
            return;
        }

        // 2) Y un clic fuera aunque nadie se lleve el foco (el escritorio, por
        //    ejemplo). HuboClicIzquierdo no se pierde los clics cortos.
        bool clic = Nativo.BotonIzquierdoPulsado() || Nativo.HuboClicIzquierdo();
        if (!clic) { _clicContado = false; return; }
        if (_clicContado) return;
        _clicContado = true;
        try
        {
            var r = Monitores.Raton();
            if (Isla.InputHitTest(Isla.PointFromScreen(new Point(r.X, r.Y))) != null) return;
            Log.Info("clic fuera del menú: se cierra");
            CerrarMenu();
        }
        catch { }
    }

    int _intentosFoco;

    // Quien se lleva el primer plano puede ser una aplicación de verdad (has hecho
    // clic en ella) o uno de esos programas que se lo devuelven a sí mismos cada
    // pocos segundos: Wallpaper Engine y el escritorio. Con los primeros no hay nada
    // que pelear, el menú sobra. Con los segundos sí, que si no te quedas escribiendo
    // en el vacío. Antes se peleaba con todos, y con un juego delante era imposible
    // cerrar el menú con un clic fuera: lo cerrabas y el foco volvía solo.
    static readonly string[] SeDevuelvenElFoco =
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "SHELLDLL_DefView", "SysListView32"
    };

    static bool SeLoDevuelveSolo(IntPtr h)
    {
        if (h == IntPtr.Zero) return true;                       // nadie al frente: no es un clic tuyo
        if (SeDevuelvenElFoco.Contains(Nativo.Clase(h))) return true;
        var exe = Nativo.Exe(h);
        return exe.Length > 0 &&
               Path.GetFileNameWithoutExtension(exe).StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase);
    }

    void VigilarFocoDelMenu()
    {
        if (_panelAbierto != PanelInicio) { _intentosFoco = 0; return; }
        var otra = Nativo.Activa();
        if (otra == _self || Nativo.EsNuestra(otra)) { _intentosFoco = 0; return; }

        // Si se lo ha llevado una aplicación de verdad no se toca nada: ya lo cierra
        // el vigilante de 150 ms. Pelearse aquí le robaba el primer plano al juego
        // durante un instante antes de cerrarse.
        if (!SeLoDevuelveSolo(otra)) return;

        if (++_intentosFoco > 3)
        {
            Log.Info("el menú no consigue mantener el foco: se cierra");
            CerrarMenu();
            return;
        }
        Log.Info($"el menú ha perdido el foco frente a \"{Nativo.Titulo(otra)}\": recuperándolo ({_intentosFoco})");
        Nativo.ForzarPrimerPlano(_self);
        Dispatcher.InvokeAsync(() => { _caja?.Focus(); Keyboard.Focus(_caja); }, DispatcherPriority.Input);
    }

    // Un Button captura el ratón al pulsarlo y lo suelta al soltarlo: así sabe si
    // levantaste el dedo encima. Si se la quitamos en ese momento, nunca llega a su
    // Click. Por eso solo se recupera cuando la captura no la tiene NADIE.
    void RecuperarCaptura(object remitente, MouseEventArgs e)
    {
        if (_panelAbierto != PanelInicio) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (_panelAbierto != PanelInicio) return;
            if (Mouse.Captured != null) return;      // se la ha llevado algo de dentro
            Mouse.Capture(Isla, CaptureMode.SubTree);
        }, DispatcherPriority.Input);
    }

    // Y al soltar el botón se vuelve a armar, que es cuando la suelta él
    void RearmarCaptura(object remitente, MouseButtonEventArgs e)
    {
        if (_panelAbierto != PanelInicio) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (_panelAbierto != PanelInicio) return;
            if (Mouse.Captured != null) return;
            Mouse.Capture(Isla, CaptureMode.SubTree);
        }, DispatcherPriority.Input);
    }

    // ================= Montaje =================
    // Los actualizadores del menú viven aparte de los de la isla: se tiran a la
    // basura al cerrarlo, porque sus controles ya no existen
    readonly List<Action> _actualizadoresMenu = new();

    FrameworkElement CrearMenuInicio()
    {
        _actualizadoresMenu.Clear();

        var raiz = new Grid { Margin = new Thickness(6, 8, 6, 10) };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // cabecera
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // caja de búsqueda
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _caja = new TextBox
        {
            Height = 38,
            Padding = new Thickness(12, 0, 12, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(0x24, _cTexto.R, _cTexto.G, _cTexto.B)),
            Foreground = _texto,
            CaretBrush = _acento,
            BorderThickness = new Thickness(0),
            FontSize = FontSize + 3,
            Tag = "caja"
        };
        _caja.TextChanged += (_, _) => Buscar(_caja!.Text);
        _caja.PreviewKeyDown += CajaTecla;
        Grid.SetRow(_caja, 1);
        raiz.Children.Add(_caja);

        if (_cfg.Inicio.Saludo)
        {
            var cabecera = CrearCabecera();
            Grid.SetRow(cabecera, 0);
            raiz.Children.Add(cabecera);
        }

        // La marca de selección va por detrás de las filas y se desliza entre ellas
        _marcaSeleccion = new Border
        {
            Height = AltoFila - 4,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x26, _cAcento.R, _cAcento.G, _cAcento.B)),
            BorderBrush = _acento,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2, 0, 2, 0),
            Visibility = Visibility.Collapsed,
            RenderTransform = new TranslateTransform()
        };

        _lista = new StackPanel();
        var capa = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        capa.Children.Add(_marcaSeleccion);
        capa.Children.Add(_lista);

        _scroll = new ScrollViewer
        {
            Content = capa,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(_scroll, 2);
        raiz.Children.Add(_scroll);

        // Sin nada escrito no se ven resultados, sino el panel de siempre: lo que
        // suena, los accesos rápidos, las gráficas, el wallpaper y las fijadas
        _portada = CrearPortada();
        Grid.SetRow(_portada, 2);
        raiz.Children.Add(_portada);

        _pista = new TextBlock
        {
            Foreground = _texto,
            Opacity = 0.4,
            Margin = new Thickness(12, 14, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Text = "Sin resultados"
        };
        Grid.SetRow(_pista, 2);
        raiz.Children.Add(_pista);

        return raiz;
    }

    ScrollViewer? _portada;

    // Entra escalonada: cada tarjeta un pelín después que la anterior
    ScrollViewer CrearPortada()
    {
        var pila = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var piezas = new UIElement[]
        {
            CrearTarjetaMusica(),
            CrearAccesosRapidos(),
            CrearGraficas(),
            CrearTarjetaWallpaper(),
            CrearFijadas(),
            CrearEnergia()
        };

        for (int i = 0; i < piezas.Length; i++)
        {
            pila.Children.Add(piezas[i]);
            if (piezas[i] is not FrameworkElement fe) continue;
            var mover = new TranslateTransform { Y = 14 };
            fe.RenderTransform = mover;
            double retardo = 90 + i * 45;
            fe.BeginAnimation(OpacityProperty, Kf(0, 1, 240, retardo));
            mover.BeginAnimation(TranslateTransform.YProperty, Kf(14, 0, 420, retardo, Muelle));
            mover.Y = 0;
        }

        return new ScrollViewer
        {
            Content = pila,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    // ================= Búsqueda =================
    void Buscar(string consulta)
    {
        if (_lista == null) return;
        var limpia = consulta.Trim();
        var norm = Parecido.Normalizar(limpia);

        var reloj = Stopwatch.StartNew();
        var encontrados = new List<Resultado>();

        if (limpia.Length > 0)
        {
            if (Fuentes.Comando(limpia) is { } c) encontrados.Add(c);
            else
            {
                if (Fuentes.Calculo(limpia) is { } calc) encontrados.Add(calc);
                encontrados.AddRange(Fuentes.Apps(norm, 40));
                encontrados.AddRange(VentanasAbiertas(norm));
                encontrados.AddRange(Fuentes.Ajustes(norm));
                if (AjustesDeLaNube(norm) is { } aj) encontrados.Add(aj);
                encontrados.Add(Fuentes.Web(limpia, _cfg.Inicio.BuscadorWeb));
            }
        }

        foreach (var r in encontrados) r.Puntos += Uso.Empujon(r.Clave);
        _resultados = encontrados.OrderByDescending(r => r.Puntos).ToList();
        Pintar();
        reloj.Stop();

        if (Environment.GetEnvironmentVariable("NUBEBAR_DIAG") == "1")
            Log.Info($"DIAG buscar \"{limpia}\": {_resultados.Count} resultados en {reloj.Elapsed.TotalMilliseconds:0.0} ms");

        ProgramarArchivos(limpia);
    }

    // La propia ventana de ajustes, buscable como cualquier otra cosa
    Resultado? AjustesDeLaNube(string norm)
    {
        double p = Math.Max(Parecido.Puntuar(norm, "Ajustes de la nube"),
                            Parecido.Puntuar(norm, "NubeBar configuración"));
        if (p < 0.5) return null;
        return new Resultado
        {
            Categoria = Categoria.Ajuste,
            Titulo = "Ajustes de la nube",
            Detalle = "Cambiar colores, módulos y comportamiento",
            Clave = "ajuste:nubebar",
            Puntos = p,
            Abrir = AbrirAjustes
        };
    }

    // Las ventanas abiertas salen de los grupos que ya mantiene el carrusel
    IEnumerable<Resultado> VentanasAbiertas(string norm)
    {
        foreach (var g in _grupos.Values)
            foreach (var h in g.Ventanas)
            {
                var titulo = Nativo.Titulo(h);
                double p = Parecido.Puntuar(norm, titulo);
                double pe = Parecido.Puntuar(norm, Path.GetFileNameWithoutExtension(g.Exe));
                double mejor = Math.Max(p, pe * 0.9);
                if (mejor < 0.5) continue;
                var v = h;
                yield return new Resultado
                {
                    Categoria = Categoria.Ventana,
                    Titulo = titulo,
                    Detalle = Path.GetFileName(g.Exe),
                    Clave = "ventana:" + Path.GetFileName(g.Exe),
                    IconoRuta = g.Exe.StartsWith("hwnd:") ? null : g.Exe,
                    Puntos = mejor * 1.02,     // saltar a algo ya abierto suele ser lo que quieres
                    Abrir = () => Nativo.Activar(v)
                };
            }
    }

    // Los archivos tardan: se piden un poco después y se mezclan cuando llegan
    void ProgramarArchivos(string consulta)
    {
        _buscaArchivos?.Cancel();
        _esperaArchivos?.Stop();
        if (!_cfg.Inicio.BuscarArchivos || consulta.Length < 3 || consulta.StartsWith(">")) return;

        _esperaArchivos ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _esperaArchivos.Tick -= LanzarArchivos;
        _esperaArchivos.Tick += LanzarArchivos;
        _esperaArchivos.Start();
    }

    void LanzarArchivos(object? remitente, EventArgs e)
    {
        _esperaArchivos?.Stop();
        if (_panelAbierto != PanelInicio || _caja == null) return;
        var consulta = _caja.Text.Trim();
        if (consulta.Length < 3) return;

        var cts = new CancellationTokenSource();
        _buscaArchivos = cts;
        Task.Run(() => Fuentes.Archivos(consulta, 8, cts.Token), cts.Token)
            .ContinueWith(t =>
            {
                if (cts.IsCancellationRequested || !t.IsCompletedSuccessfully) return;
                if (_caja == null || _caja.Text.Trim() != consulta) return;   // ya has seguido escribiendo
                foreach (var r in t.Result) r.Puntos += Uso.Empujon(r.Clave);
                _resultados = _resultados.Concat(t.Result).OrderByDescending(r => r.Puntos).ToList();
                Pintar();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ================= Pintado =================
    void Pintar()
    {
        if (_lista == null || _pista == null) return;

        var mostrar = _filtro is { } f ? _resultados.Where(r => r.Categoria == f).ToList() : _resultados;
        bool escribiendo = _caja?.Text.Trim().Length > 0;

        // Con la caja vacía manda la portada; en cuanto escribes, los resultados
        if (_portada != null) _portada.Visibility = escribiendo ? Visibility.Collapsed : Visibility.Visible;
        if (_scroll != null) _scroll.Visibility = escribiendo ? Visibility.Visible : Visibility.Collapsed;
        _pista.Visibility = escribiendo && mostrar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _lista.Children.Clear();
        // Más filas de las que caben no aportan nada y cada una cuesta un icono
        foreach (var r in mostrar.Take(MaxFilas)) _lista.Children.Add(CrearFila(r));

        _seleccion = Math.Min(_seleccion, Math.Max(0, mostrar.Count - 1));
        if (mostrar.Count == 0) _seleccion = 0;
        MoverMarca(inmediato: true);
    }

    List<Resultado> Visibles =>
        _filtro is { } f ? _resultados.Where(r => r.Categoria == f).Take(MaxFilas).ToList() : _resultados.Take(MaxFilas).ToList();

    UIElement CrearFila(Resultado r)
    {
        var fila = new Grid { Height = AltoFila, Margin = new Thickness(10, 0, 10, 0) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (r.IconoRuta is { } ruta)
        {
            var img = new Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetColumn(img, 0);
            fila.Children.Add(img);
            // Sacar el icono es una llamada COM al shell y la primera vez cuesta.
            // Se pide en segundo plano para que teclear no se quede esperando.
            Dispatcher.InvokeAsync(() => img.Source = Iconos.De(ruta, 32), DispatcherPriority.Background);
        }

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = r.Titulo,
            Foreground = _texto,
            FontSize = FontSize + 1,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (r.Detalle.Length > 0)
            textos.Children.Add(new TextBlock
            {
                Text = r.Detalle,
                Foreground = _texto,
                Opacity = 0.45,
                FontSize = FontSize - 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        Grid.SetColumn(textos, 1);
        fila.Children.Add(textos);

        var etiqueta = new TextBlock
        {
            Text = r.Etiqueta,
            Foreground = _acento,
            Opacity = 0.7,
            FontSize = FontSize - 3,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(etiqueta, 2);
        fila.Children.Add(etiqueta);

        var zona = new Border { Background = Brushes.Transparent, Child = fila, Cursor = Cursors.Hand };
        zona.MouseEnter += (_, _) =>
        {
            int i = Visibles.FindIndex(x => ReferenceEquals(x, r));
            if (i >= 0) { _seleccion = i; MoverMarca(); }
        };
        zona.MouseLeftButtonUp += (_, _) => Ejecutar(r, false);
        return zona;
    }

    // La marca de selección se desliza hasta la fila elegida en vez de saltar
    void MoverMarca(bool inmediato = false)
    {
        if (_marcaSeleccion == null || _lista == null) return;
        var cuantos = Visibles.Count;
        if (cuantos == 0) { _marcaSeleccion.Visibility = Visibility.Collapsed; return; }

        _marcaSeleccion.Visibility = Visibility.Visible;
        _marcaSeleccion.Width = Math.Max(0, (_scroll?.ActualWidth ?? AnchoMenu) - 8);

        double destino = _seleccion * AltoFila + 2;
        if (_marcaSeleccion.RenderTransform is not TranslateTransform t)
            _marcaSeleccion.RenderTransform = t = new TranslateTransform();

        double ahora = t.Y;
        t.BeginAnimation(TranslateTransform.YProperty, null);
        t.Y = destino;
        if (!inmediato && Math.Abs(ahora - destino) > 0.5)
            t.BeginAnimation(TranslateTransform.YProperty, Kf(ahora, destino, 180, 0, Suave));

        // Mantener la selección a la vista
        _scroll?.ScrollToVerticalOffset(Math.Max(0, destino - (FilasVisibles - 1) * AltoFila));
    }

    // ================= Teclado =================
    void CajaTecla(object remitente, KeyEventArgs e)
    {
        var visibles = Visibles;
        switch (e.Key)
        {
            case Key.Escape:
                CerrarMenu();
                e.Handled = true;
                break;

            case Key.Down:
                if (visibles.Count > 0) { _seleccion = (_seleccion + 1) % visibles.Count; MoverMarca(); }
                e.Handled = true;
                break;

            case Key.Up:
                if (visibles.Count > 0) { _seleccion = (_seleccion - 1 + visibles.Count) % visibles.Count; MoverMarca(); }
                e.Handled = true;
                break;

            case Key.Enter:
                if (_seleccion < visibles.Count) Ejecutar(visibles[_seleccion], Teclado.Control);
                e.Handled = true;
                break;

            case Key.Tab:
                RotarFiltro();
                e.Handled = true;
                break;
        }
    }

    // Tab rota entre las categorías que hay ahora mismo, y vuelve a "todas"
    void RotarFiltro()
    {
        var categorias = _resultados.Select(r => r.Categoria).Distinct().OrderBy(c => c).ToList();
        if (categorias.Count == 0) return;

        if (_filtro is not { } actual) _filtro = categorias[0];
        else
        {
            int i = categorias.IndexOf(actual);
            _filtro = (i < 0 || i == categorias.Count - 1) ? null : categorias[i + 1];
        }
        _seleccion = 0;
        Pintar();
    }

    void Ejecutar(Resultado r, bool abrirUbicacion)
    {
        Uso.Apuntar(r.Clave);
        if (abrirUbicacion && r.Carpeta is { } carpeta && Directory.Exists(carpeta))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{r.IconoRuta ?? carpeta}\"")
                { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex) { Log.Error("abrir ubicación", ex); }
        }
        else
        {
            try { r.Abrir(); } catch (Exception ex) { Log.Error("abrir resultado", ex); }
        }
        // El cálculo se copia y el menú se queda, que es lo cómodo para encadenar cuentas
        if (r.Categoria != Categoria.Calculo) CerrarMenu();
    }
}

// Azúcar para leer el modificador sin arrastrar using por todo el archivo
static class Teclado
{
    public static bool Control => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
}
