using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// Ajustes > Colores: elegir el tema y cambiar CUALQUIER color.
//
// ---------- Lo que esta página tiene que cumplir ----------
//
// «Cualquier color de cualquier elemento de la app se puede cambiar. Sin excepciones»
// (`docs/REDISENO-VISUAL.md`, decisión B). El catálogo ya tiene las ~130 claves con su zona y
// su rótulo; esta página es la puerta: la lista agrupada, el buscador, la muestra de cada
// una, «Restablecer» por color y «Restablecer todo».
//
// ---------- Por qué la lista va en desplegables y no entera ----------
//
// Ciento treinta filas con su selector de color cada una es una página que tarda en abrirse y
// en la que no se encuentra nada. Así que: un desplegable por zona, con «Colores base»
// abierto —es lo que cambia casi todo el mundo— y el contenido de cada zona construido la
// primera vez que se abre. Y un buscador que, en cuanto se escribe algo, pasa a una lista
// plana de coincidencias de todas las zonas: buscar «apagar» tiene que encontrar
// `energia.apagar` sin saber en qué zona vive.
//
// ---------- Lo que todavía no está, y no es un olvido ----------
//
//   - **Los degradados** se enseñan pero no se editan. `SelectorColor` da un color, y un
//     degradado son paradas, ángulo y opacidad: es el apartado 3 del Estudio de temas
//     (fase 9). Mientras tanto una clave con degradado se ve, se puede restablecer y se
//     puede sustituir por un color liso; lo que no se puede es afinar sus paradas aquí.
//   - **«Elegir en pantalla»** (pulsar un elemento de la barra y que se abra su color) pide
//     que cada cosa pintada sepa de qué clave salió. Eso llega solo cuando las fases 1 a 8
//     pasen cada pieza a preguntar por su clave; hacerlo antes sería mantener un segundo
//     mapa «elemento → clave» a mano, que se desincroniza el primer día.
public partial class VentanaAjustes
{
    // El tema que se ha pulsado y está esperando respuesta a «¿conservo tus colores?».
    // Null cuando no hay pregunta pendiente.
    TemaBase? _temaPendiente;

    // Lo que el usuario ha escrito en el buscador de colores.
    string _filtroColores = "";

    UIElement SeccionColores()
    {
        var p = NuevaPagina("colores");

        // ================= El tema =================

        p.Grupo("El tema", IconoDeGrupo("tema"),
                "Nueve temas completos: colores, forma, letra y decoraciones.");

        // La regla de seguridad, dicha donde se puede arreglar. Si el tema deja esta ventana
        // ilegible se pinta con Profesional, y callárselo sería dejar al usuario pensando que
        // su tema no se aplica.
        if (TemaIlegible)
            p.Aviso("Con el tema puesto no se lee el texto de esta ventana, así que Ajustes y el " +
                    "Estudio se pintan con Profesional. Mira los colores de la zona «La ventana de " +
                    "Ajustes», más abajo, o vuelve al tema de serie.");

        if (_temaPendiente is { } pendiente) p.Mas(PreguntaDeTema(pendiente));

        string puesto = _arbol.Texto("Tema", "Id", TemasDeSerie.Predeterminado);
        foreach (var familia in TemasDeSerie.Lista.GroupBy(t => t.Familia))
        {
            p.Mas(Rotulo(familia.Key));
            var muestras = new WrapPanel();
            foreach (var t in familia) muestras.Children.Add(MuestraTema(t, puesto));
            p.Mas(muestras);
        }

        // ================= Todos los colores =================

        p.Grupo("Todos los colores", IconoDeGrupo("color"),
                "Cada color de cada elemento. Lo que no cambies lo pone el tema.");

        var buscador = BuscadorDeColores();
        var lista = new StackPanel();
        buscador.TextChanged += (_, _) =>
        {
            _filtroColores = buscador.Text ?? "";
            LlenarColores(lista);
        };
        p.Mas(buscador);
        p.Mas(lista);
        LlenarColores(lista);

        int mios = _arbol.Colores("Tema").Count;
        var pieLista = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        pieLista.Children.Add(new TextBlock
        {
            Text = mios == 0
                ? Textos.T("No has cambiado ningún color: todo lo pone el tema.")
                : string.Format(Textos.T("Has cambiado {0} color(es)."), mios),
            Foreground = _textoAtenuado, FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (mios > 0) pieLista.Children.Add(BotonPlano("Restablecer todo", RestablecerTodosLosColores, false));
        p.Mas(pieLista);

        // ================= Lo demás =================

        p.Grupo("Desde el wallpaper", IconoDeGrupo("wallpaper"));
        p.Mas(Interruptor("Sacar los colores del fondo de escritorio",
                          _arbol.Bandera("Tema", "DesdeWallpaper", false),
                          v => _arbol.Poner("Tema", "DesdeWallpaper", v)));
        p.Mas(Interruptor("Mandar mi acento al fondo de escritorio",
                          _arbol.Bandera("Tema", "EnviarColorAlWallpaper", false),
                          v => _arbol.Poner("Tema", "EnviarColorAlWallpaper", v)));
        p.Pista("Con esto encendido, el fondo, el texto y el acento salen de tu wallpaper y " +
                "mandan sobre el tema y sobre lo que hayas cambiado a mano.");

        p.Grupo("Mis temas", IconoDeGrupo("export"));
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(BotonPlano("Exportar", ExportarTema, false));
        fila.Children.Add(BotonPlano("Importar", ImportarTema, false));
        fila.Children.Add(BotonPlano("Abrir carpeta", () =>
        {
            Directory.CreateDirectory(Temas.Carpeta);
            Process.Start(new ProcessStartInfo(Temas.Carpeta) { UseShellExecute = true })?.Dispose();
        }, false));
        p.Mas(fila);
        p.Pista("Un tema exportado lleva el tema de partida y los colores que le has cambiado. " +
                "El formato completo —con las fuentes y las imágenes dentro— llega con el Estudio de temas.");

        return p.Fin();
    }

    // El buscador de la lista. Con su propio estilo y no el de la búsqueda de la cabecera:
    // esta va dentro de una tarjeta, así que necesita filo para que se vea que es un campo.
    TextBox BuscadorDeColores()
    {
        var caja = new TextBox
        {
            Text = _filtroColores,
            Height = 28,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 4, 0, 6),
            Background = _superficieAlta,
            Foreground = _texto,
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = Textos.T("Busca «reloj», «apagar», «filo»… por nombre o por clave"),
        };
        System.Windows.Automation.AutomationProperties.SetName(caja, Textos.T("Buscar un color"));

        // Esc limpia la búsqueda en vez de cerrar la ventana, igual que el buscador de la
        // cabecera: cerrar Ajustes con una búsqueda a medias se lee como perder el trabajo.
        caja.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && caja.Text.Length > 0) { caja.Text = ""; e.Handled = true; }
        };
        return caja;
    }

    // ================= La muestra de un tema =================

    UIElement MuestraTema(TemaBase t, string puesto)
    {
        bool esElPuesto = t.Id.Equals(puesto, StringComparison.OrdinalIgnoreCase);
        var pintura = new Pintura(t);

        // Cuatro colores y no tres: con fondo, texto y acento, Oficina e Invierno se parecen
        // demasiado en un rectángulo de 18 px. El segundo secundario es lo que los separa.
        var tira = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
        foreach (var clave in new[] { ClavesBase.Superficie, ClavesBase.Tinta,
                                      ClavesBase.Acento, ClavesBase.Sec2 })
            tira.Children.Add(new Border
            {
                Width = 20, Height = 11,
                Background = new SolidColorBrush(pintura.ColorOpaco(clave)),
                BorderBrush = new SolidColorBrush(_roles.Borde),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 3, 0),
            });

        var caja = new StackPanel();
        caja.Children.Add(new TextBlock
        {
            Text = t.Nombre, Foreground = _texto, FontSize = 12.5,
            FontWeight = esElPuesto ? FontWeights.SemiBold : FontWeights.Normal,
        });
        caja.Children.Add(tira);
        if (esElPuesto)
            caja.Children.Add(new TextBlock
            {
                Text = Textos.T("puesto"), Foreground = _acentoLegible,
                FontSize = 10.5, Margin = new Thickness(0, 4, 0, 0),
            });

        var b = new Button
        {
            Content = caja,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(10, 8, 10, 8),
            Background = esElPuesto ? _elegido : _superficie,
            BorderBrush = esElPuesto ? _acento : new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(esElPuesto ? 2 : 1),
            Foreground = _texto,
            Cursor = Cursors.Hand,
        };

        // El contenido es una pila con el nombre y cuatro rectángulos de color. Para UIA eso
        // es un objeto sin nombre, así que los temas se anunciaban como botones iguales y
        // mudos. Y el color NO puede ser el único portador: quien no lo ve necesita el
        // nombre, y necesita saber cuál está puesto.
        System.Windows.Automation.AutomationProperties.SetName(
            b, esElPuesto ? $"Tema {t.Nombre} (puesto)" : $"Tema {t.Nombre}");

        b.Click += (_, _) => AplicarTema(t);
        return b;
    }

    // ---------- Cambiar de tema, y qué pasa con tus colores ----------
    //
    // El plan dice: «Cambiar de tema conserva o descarta esas personalizaciones, a elección
    // del usuario (se pregunta)».
    //
    // Se pregunta DENTRO de la página y no con un cuadro de diálogo del sistema. No por
    // gusto: este producto no tiene ni un `MessageBox` en ciento cincuenta ficheros, y los
    // avisos y las confirmaciones (cerrar una app, apagar, probar un perfil) se dicen dentro
    // de su propia superficie. Un modal del sistema aquí sería el primero, y además el que
    // menos falta hace: la pregunta cabe donde está la decisión.
    //
    // Y si no hay nada que conservar, no hay pregunta: se aplica y punto.
    void AplicarTema(TemaBase t)
    {
        if (_arbol.Colores("Tema").Count == 0) { PonerTema(t, conservar: false); return; }

        _temaPendiente = t;
        RehacerPagina("colores", SeccionColores);
    }

    UIElement PreguntaDeTema(TemaBase t)
    {
        int cuantos = _arbol.Colores("Tema").Count;
        var texto = new TextBlock
        {
            Text = string.Format(
                Textos.T("Tienes {0} color(es) cambiados a mano. ¿Los conservo encima de «{1}»?"),
                cuantos, t.Nombre),
            Foreground = _texto, FontSize = 12, TextWrapping = TextWrapping.Wrap,
        };

        var botones = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        botones.Children.Add(BotonPlano("Conservarlos", () => PonerTema(t, conservar: true), true));
        botones.Children.Add(BotonPlano("Usar el tema limpio", () => PonerTema(t, conservar: false), false));
        botones.Children.Add(BotonPlano("Dejarlo", () =>
        {
            _temaPendiente = null;
            RehacerPagina("colores", SeccionColores);
        }, false));

        var dentro = new StackPanel();
        dentro.Children.Add(texto);
        dentro.Children.Add(botones);

        return new Border
        {
            Background = _superficieAlta,
            BorderBrush = new SolidColorBrush(_roles.Aviso),
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(11, 9, 11, 10),
            Margin = new Thickness(0, 8, 0, 4),
            Child = dentro,
        };
    }

    void PonerTema(TemaBase t, bool conservar)
    {
        _temaPendiente = null;
        if (!conservar) _arbol.PonerColores("Tema", new Dictionary<string, string>());
        _arbol.Poner("Tema", "Id", t.Id);
        _arbol.Guardar();
        RehacerPagina("colores", SeccionColores);
    }

    // ================= La lista de colores =================

    void LlenarColores(StackPanel donde)
    {
        donde.Children.Clear();
        var filtro = _filtroColores.Trim();

        if (filtro.Length > 0)
        {
            var hallados = Catalogo.Todas.Where(c => Coincide(c, filtro)).ToList();
            if (hallados.Count == 0)
            {
                donde.Children.Add(new TextBlock
                {
                    Text = Textos.T("Ningún color se llama así."),
                    Foreground = _textoAtenuado, FontSize = 12, Margin = new Thickness(0, 10, 0, 4),
                });
                return;
            }
            foreach (var c in hallados) donde.Children.Add(FilaDeColor(c));
            return;
        }

        foreach (var zona in Catalogo.PorZona())
        {
            var dentro = new StackPanel();
            var des = new Expander
            {
                Header = $"{Catalogo.RotuloDeZona(zona.Key)}  ({zona.Count()})",
                Foreground = _texto,
                Margin = new Thickness(0, 6, 0, 0),
                IsExpanded = zona.Key == ZonaColor.Comun,
                Content = dentro,
            };

            // El contenido, la primera vez que se abre. Construir las ~130 filas con su
            // selector de color de golpe se nota al abrir la página; así solo se paga por lo
            // que se mira.
            var claves = zona.ToList();
            bool montado = false;
            void Montar()
            {
                if (montado) return;
                montado = true;
                foreach (var c in claves) dentro.Children.Add(FilaDeColor(c));
            }
            des.Expanded += (_, _) => Montar();
            if (des.IsExpanded) Montar();

            donde.Children.Add(des);
        }
    }

    static bool Coincide(ClaveColor c, string filtro) =>
        c.Clave.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
        c.Rotulo.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
        Textos.T(c.Rotulo).Contains(filtro, StringComparison.OrdinalIgnoreCase);

    // El cajón de color que está abierto ahora mismo, para cerrarlo al abrir otro. Uno cada
    // vez: con dos abiertos ya no se ve la lista, que es lo que se vino a mirar.
    Border? _colorAbierto;

    // ---------- Una fila ----------
    //
    // ---------- El error que esto corrige ----------
    //
    // La primera versión ponía un `SelectorColor` **montado** en cada fila. `SelectorColor` no
    // es una muestra: es un panel de 120 px de alto con su cuadrado de saturación, su barra de
    // tono, su barra de opacidad y su campo hexadecimal. Ciento treinta de esos, uno debajo de
    // otro, no es una lista de colores: es un muro. El usuario lo vio y lo dijo con una
    // palabra — «devastador» —, y tenía toda la razón.
    //
    // Una fila es **una línea**: qué es, de qué color está y si lo has tocado. El selector
    // aparece debajo cuando pulsas la muestra, y solo uno a la vez.
    UIElement FilaDeColor(ClaveColor c)
    {
        var mios = _arbol.Colores("Tema");
        bool personalizado = mios.ContainsKey(c.Clave);
        var relleno = _roles.Pintura.R(c.Clave);

        // ---------- La izquierda: qué es ----------
        var izquierda = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        izquierda.Children.Add(new TextBlock
        {
            Text = Textos.T(c.Rotulo), Foreground = _texto, FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var bajo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
        bajo.Children.Add(new TextBlock
        {
            Text = c.Clave, Foreground = _textoAtenuado, FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (personalizado)
            bajo.Children.Add(new TextBlock
            {
                Text = "· " + Textos.T("cambiado por ti"), Foreground = _acentoLegible,
                FontSize = 10.5, Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

        // ---------- El contraste, cuando a esa clave se le mide ----------
        //
        // «Se avisa, no se impide: manda el usuario». Así que esto es un número y, cuando no
        // llega, una frase. No un candado.
        if (c.SeMide)
        {
            double r = _roles.Pintura.ContrasteDe(c.Clave);
            bool vale = r >= c.MinimoContraste;
            bajo.Children.Add(new TextBlock
            {
                Text = vale
                    ? string.Format(Textos.T("· {0}:1"), r.ToString("0.0"))
                    : string.Format(Textos.T("· {0}:1 — no se lee sobre «{1}» (hace falta {2}:1)"),
                                    r.ToString("0.0"), c.Sobre, c.MinimoContraste.ToString("0.0")),
                Foreground = vale ? _textoAtenuado : _roles.Pincel("ajustes.aviso.mal"),
                FontSize = 10.5, Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        izquierda.Children.Add(bajo);

        // ---------- La derecha: la muestra, que es el botón ----------
        var derecha = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var cajon = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = _superficie,
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 10),
            Margin = new Thickness(0, 4, 0, 6),
        };

        var muestra = new Border
        {
            Width = 38, Height = 22,
            Background = relleno.Pincel(),
            BorderBrush = new SolidColorBrush(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var dentroDelBoton = new StackPanel { Orientation = Orientation.Horizontal };
        dentroDelBoton.Children.Add(muestra);
        dentroDelBoton.Children.Add(new TextBlock
        {
            Text = relleno.EsDegradado ? Textos.T("degradado") : Relleno.Hex(relleno.Color),
            Foreground = _textoAtenuado, FontSize = 10.5,
            Margin = new Thickness(7, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var abrir = new Button
        {
            Content = dentroDelBoton,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 2, 4, 2),
            Cursor = Cursors.Hand,
            // Un degradado se ve y se puede restablecer, pero no se afina aquí: `SelectorColor`
            // da un color, y un degradado son paradas, ángulo y opacidad. Es el apartado 3 del
            // Estudio de temas (fase 9).
            IsEnabled = !relleno.EsDegradado,
            ToolTip = relleno.EsDegradado
                ? Textos.T("Un degradado. Se edita en el Estudio de temas.")
                : string.Format(Textos.T("Cambiar «{0}»"), Textos.T(c.Rotulo)),
        };
        System.Windows.Automation.AutomationProperties.SetName(
            abrir, string.Format(Textos.T("Color de {0}"), Textos.T(c.Rotulo)));

        abrir.Click += (_, _) =>
        {
            if (cajon.Visibility == Visibility.Visible) { Cerrar(); return; }

            // Uno cada vez.
            if (_colorAbierto is { } otro) otro.Visibility = Visibility.Collapsed;
            _colorAbierto = cajon;

            // El selector se monta la PRIMERA vez que se abre esta fila, no al construir la
            // lista: es justo lo que hacía que la página pesara ciento treinta veces de más.
            if (cajon.Child == null)
            {
                // Opacidad sí, salvo en lo que hace de texto: una letra a medio alfa no es una
                // decisión, es un texto que no se lee, y para eso ya está el color.
                var sel = new SelectorColor { ConOpacidad = c.Papel != Papel.Texto }.Montar();
                sel.Valor = relleno.Color;
                sel.Cambiado += v =>
                {
                    muestra.Background = new SolidColorBrush(v);
                    EscribirColor(c, Relleno.Liso(v));
                };
                cajon.Child = sel;
            }
            cajon.Visibility = Visibility.Visible;

            void Cerrar()
            {
                cajon.Visibility = Visibility.Collapsed;
                if (ReferenceEquals(_colorAbierto, cajon)) _colorAbierto = null;
            }
        };

        derecha.Children.Add(abrir);

        if (personalizado)
        {
            var quitar = new Button
            {
                Content = "⟲",
                FontSize = 13,
                Width = 26, Height = 22,
                Foreground = _textoAtenuado,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = Textos.T("Devolverle este color al tema"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            System.Windows.Automation.AutomationProperties.SetName(
                quitar, string.Format(Textos.T("Restablecer {0}"), Textos.T(c.Rotulo)));
            quitar.Click += (_, _) => RestablecerColor(c);
            derecha.Children.Add(quitar);
        }

        var rejilla = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(izquierda, 0);
        Grid.SetColumn(derecha, 1);
        rejilla.Children.Add(izquierda);
        rejilla.Children.Add(derecha);

        var pila = new StackPanel();
        pila.Children.Add(rejilla);
        pila.Children.Add(cajon);
        return pila;
    }

    void EscribirColor(ClaveColor c, Relleno valor)
    {
        var mios = _arbol.Colores("Tema");
        mios[c.Clave] = valor.Escribir();
        _arbol.PonerColores("Tema", mios);
        Cambio();
    }

    void RestablecerColor(ClaveColor c)
    {
        var mios = _arbol.Colores("Tema");
        if (!mios.Remove(c.Clave)) return;
        _arbol.PonerColores("Tema", mios);
        _arbol.Guardar();
        RehacerPagina("colores", SeccionColores);
    }

    void RestablecerTodosLosColores()
    {
        _arbol.PonerColores("Tema", new Dictionary<string, string>());
        _arbol.Guardar();
        RehacerPagina("colores", SeccionColores);
    }

    // ================= Exportar e importar =================
    //
    // El formato de verdad —un `.nubetema` con las fuentes y las imágenes dentro— es el
    // apartado 10 del Estudio de temas (fase 9). Esto es lo que se puede tener hoy sin
    // prometer de más: el tema de partida y los colores cambiados, en un JSON legible.

    void ExportarTema()
    {
        var t = new TemaExportado(
            "Mi tema " + DateTime.Now.ToString("yyyy-MM-dd HHmm"),
            _arbol.Texto("Tema", "Id", TemasDeSerie.Predeterminado),
            _arbol.Colores("Tema"));
        var ruta = Path.Combine(Temas.Carpeta, t.Nombre.Replace(':', '-') + ".json");
        try
        {
            Temas.Exportar(t, ruta);
            Avisar("Tema guardado en " + ruta, false);
        }
        catch (Exception ex) { Avisar("No se pudo exportar: " + ex.Message, true); }
    }

    void ImportarTema()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Temas de la nube (*.json)|*.json",
            InitialDirectory = Directory.Exists(Temas.Carpeta) ? Temas.Carpeta : Config.Carpeta,
        };
        if (dlg.ShowDialog() != true) return;
        if (Temas.Importar(dlg.FileName) is not { } tema) { Avisar("Ese archivo no es un tema", true); return; }

        // Un tema importado no puede tumbar la barra: su id se valida (uno desconocido cae en
        // Profesional) y de sus colores solo entran las claves que el catálogo conoce y los
        // valores que se entienden. Lo que no, se ignora y se apunta.
        var limpios = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int fuera = 0;
        foreach (var (k, v) in tema.Colores ?? new Dictionary<string, string>())
        {
            if (Catalogo.Existe(k) && Relleno.Leer(v) is not null) limpios[k] = v;
            else { fuera++; Log.Info($"tema importado: se ignora \"{k}\" = \"{v}\""); }
        }

        _arbol.Poner("Tema", "Id", TemasDeSerie.De(tema.Id).Id);
        _arbol.PonerColores("Tema", limpios);
        _arbol.Guardar();
        RehacerPagina("colores", SeccionColores);
        Avisar(fuera == 0
            ? "Tema importado: " + tema.Nombre
            : $"Tema importado: {tema.Nombre} ({fuera} valores que no se entienden, ignorados)", false);
    }
}
