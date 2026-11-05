using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NubeBar;

// El módulo «Monitores»: cambiar la entrada de vídeo de cada pantalla sin tocar sus botones.
//
// ---------- Qué enseña ----------
//
//     MONITORES                                    [Actualizar]
//
//     AOC Q27G3G3R3                                 DDC/CI ✓
//     Entrada actual: DisplayPort 1
//     [DisplayPort 1]  [HDMI 1]  [HDMI 2]
//
//     AOC 25G3ZM                                    DDC/CI ✓
//     Entrada actual: HDMI 1
//     [DisplayPort 1]  [HDMI 1]
//
// ---------- Las tres reglas que no se negocian ----------
//
// 1. **Nada de DDC/CI en el hilo de la interfaz.** Preguntarle a un monitor por el cable cuesta
//    entre 40 y 80 ms, la cadena de capacidades puede pasar del segundo, y verificar un cambio
//    son hasta dos segundos. Todo eso va a un trabajador y vuelve por el `Dispatcher`. Es lo
//    único que separa «la barra cambia la entrada» de «la barra se congela dos segundos».
//
// 2. **El botón lleva la CLAVE del monitor, no su índice ni su handle.** Entre dibujar el botón
//    y pulsarlo pueden apagarse pantallas o reordenarse los `DISPLAY`. `Entradas.Poner` vuelve a
//    buscar quién es esa clave antes de mandar nada; si no está, no manda. Así es imposible
//    cambiarle la entrada al monitor de al lado, que es el accidente que había que evitar.
//
// 3. **Solo se ofrecen las entradas que el monitor ANUNCIA.** Si no anuncia ninguna, se enseña
//    la que tiene puesta y se abre el campo de código manual, que avisa de que no está probado.
//    Un botón «HDMI 2» en un monitor que no tiene HDMI 2 es un botón que no hace nada y una
//    tarde perdida buscando el cable malo.
//
// ---------- Por qué no se refresca cada segundo ----------
//
// El resto de los módulos de la barra se repintan en el `Tick`. Este no se apunta a
// `_actualizadores` a propósito: un sondeo por segundo son 86.400 preguntas diarias por el bus
// I²C de cada monitor para enseñar un dato que cambia tres veces al día. Se lee al abrir el
// panel y cuando tú pulsas Actualizar.
public partial class MainWindow : Window
{
    internal const string PanelMonitores = "monitores";

    // Lo último que se sabe. `null` es «todavía no se ha preguntado»: no es lo mismo que una
    // lista vacía, y la interfaz dice cosas distintas en cada caso.
    List<Entradas.Pantalla>? _mons;

    // La línea de estado de la última operación. El plan la pide por su nombre («Estado de la
    // última operación») y es la diferencia entre «no ha pasado nada» y «el monitor dijo que no».
    string? _monsEstado;

    // Hay una operación en vuelo. Mientras lo haya, los botones no responden: dos cambios
    // montados en el mismo bus I²C devuelven basura.
    bool _monsOcupado;

    // En qué entrada estaba cada monitor ANTES del último cambio que hicimos nosotros, para
    // poder ofrecer «Volver a DisplayPort». El plan lo pide: «mantener la posibilidad de volver
    // a la entrada anterior».
    //
    // Estático y compartido por las dos islas: el monitor es el mismo desde las dos, y que el
    // «volver» aparezca solo en la isla desde la que pulsaste sería una rareza sin motivo.
    static readonly Dictionary<string, byte> _monsAntes = new(StringComparer.OrdinalIgnoreCase);

    // La clave del monitor al que le estás cambiando el nombre, si hay alguno.
    string? _monsRenombrando;

    FrameworkElement? _anclaMonitores;

    // ================= El módulo de la barra =================

    internal UIElement ModuloMonitores()
    {
        var icono = Glifo(char.ConvertFromUtf32(Lateral.Glifos.Escritorio));
        var b = Boton(icono, "Monitores: cambiar la entrada de vídeo", () => AbrirMonitores());
        _anclaMonitores = b;

        // ---------- Nace escondido ----------
        //
        // Averiguar si merece la pena ofrecerlo exige leer las capacidades de cada monitor, y
        // eso son hasta dos segundos. Hacerlo aquí sería hacerlo MIENTRAS SE MONTA LA BARRA, y
        // el arranque de NubeBar se mide en decenas de milisegundos (ver `Log.Nuevo`).
        //
        // Así que se pregunta en un trabajador y el icono aparece cuando hay respuesta. Mismo
        // criterio que `Brillo.Disponible`: si ningún monitor gestiona su entrada, no se dibuja
        // nunca. Una fila que solo sabe decir «no disponible» no es información.
        b.Visibility = Visibility.Collapsed;

        if (Entradas.YaSabemos is { } ya)
            b.Visibility = ya ? Visibility.Visible : Visibility.Collapsed;
        else
            Task.Run(() =>
            {
                bool hay;
                try { hay = Entradas.Disponible(); }
                catch (Exception ex) { Log.Error("ver si hay monitores con entrada DDC/CI", ex); return; }
                Dispatcher.InvokeAsync(() =>
                {
                    try { b.Visibility = hay ? Visibility.Visible : Visibility.Collapsed; }
                    catch { }
                });
            });

        return b;
    }

    // ================= El panel =================

    void AbrirMonitores()
    {
        // El alto que pide el contenido que HAY AHORA. Cuando la lista llegue del trabajador,
        // `RefrescarMonitores` vuelve a medir con `ReajustarPanel`, igual que hace la batería.
        if (!AlternarPanel(PanelMonitores, CrearMonitores, 380, AltoDeMonitores(),
                           ancla: _anclaMonitores)) return;

        // Se pregunta al abrir, no al montar la barra. Si ya hay una lista de antes se enseña
        // esa mientras llega la nueva: una lista de hace diez minutos es mejor que un hueco.
        PedirMonitores(desdeCero: false);
    }

    double AltoDeMonitores()
    {
        if (_mons is null) return 150;                      // «Buscando monitores…»
        if (_mons.Count == 0) return 160;
        // Por monitor: cabecera (30) + estado (18) + entrada actual (20) + la fila de botones
        // (68) + el aire de la tarjeta (22). Más la cabecera del panel y la línea de estado.
        double porMonitor = 158;
        if (_monsRenombrando is not null) porMonitor += 34;
        return Math.Min(TopeDeAlto, 86 + _mons.Count * porMonitor);
    }

    FrameworkElement CrearMonitores()
    {
        var columna = new StackPanel { Margin = new Thickness(12, 4, 12, 8) };

        // ---------- Cabecera ----------
        var actualizar = Boton(new TextBlock
        {
            Text = _monsOcupado ? "Un momento…" : "Actualizar",
            Foreground = _monsOcupado ? _textoAtenuado : _acentoLegible,
            FontSize = 11,
        }, "Volver a preguntar a los monitores", () => { if (!_monsOcupado) PedirMonitores(desdeCero: true); });
        actualizar.Height = 22;

        var cab = new StackPanel();
        cab.Children.Add(SectionHeader(Lateral.Glifos.Escritorio, "Monitores", actualizar));

        if (_mons is null)
        {
            cab.Children.Add(Tenue("Preguntando a los monitores por el cable…"));
            columna.Children.Add(Card(cab));
            return PanelShell(columna, 0);
        }

        if (_mons.Count == 0)
        {
            // Esto no es un error nuestro: es lo que pasa en una sesión remota, con un driver
            // básico de vídeo o con una pantalla por un conversor que no pasa el I²C.
            cab.Children.Add(Tenue("Windows no expone ningún monitor físico. " +
                                   "Pasa en sesiones remotas y con algunos conversores."));
            columna.Children.Add(Card(cab));
            return PanelShell(columna, 0);
        }

        columna.Children.Add(Card(cab));

        foreach (var p in _mons) columna.Children.Add(TarjetaDeMonitor(p));

        if (!string.IsNullOrWhiteSpace(_monsEstado))
        {
            var estado = Tenue(_monsEstado!);
            estado.TextWrapping = TextWrapping.Wrap;
            estado.Margin = new Thickness(2, 0, 2, 2);
            columna.Children.Add(estado);
        }

        // El recordatorio que evita la decepción. Es la pregunta que el usuario hizo primero
        // —«¿puedo ver la PS5 en una ventana?»— y la respuesta tiene que estar donde se toma la
        // decisión, no en un documento.
        var nota = Tenue("Cambia el canal del monitor, no trae la imagen a Windows.");
        nota.FontSize = 9.5;
        nota.Margin = new Thickness(2, 2, 2, 0);
        nota.TextWrapping = TextWrapping.Wrap;
        columna.Children.Add(nota);

        ApuntarPanel(PanelMonitores, columna, 380, TopeDeAlto);
        return PanelShell(columna, 0);
    }

    FrameworkElement TarjetaDeMonitor(Entradas.Pantalla p)
    {
        var col = new StackPanel();

        // ---------- Cabecera: quién es y si contesta ----------
        var insignia = p.GestionaEntrada ? StatusBadge("DDC/CI", bien: true)
                     : p.Responde ? StatusBadge("sin entrada", bien: false, neutro: true)
                     : StatusBadge("sin DDC/CI", bien: false);

        var derecha = new StackPanel { Orientation = Orientation.Horizontal };
        derecha.Children.Add(insignia);

        // El lápiz para el alias. El plan lo pide («permitir renombrar monitores») y hace falta
        // de verdad: con dos pantallas iguales, «AOC Q27G3G3R3» dos veces no distingue nada, y
        // «PS5» sí.
        var lapiz = Glifo(char.ConvertFromUtf32(Lateral.Glifos.Lapiz));
        lapiz.FontSize = 11;
        lapiz.Foreground = _textoAtenuado;
        var bLapiz = Boton(lapiz, "Ponerle un nombre a este monitor", () =>
        {
            _monsRenombrando = _monsRenombrando == p.Clave ? null : p.Clave;
            RefrescarMonitores();
        });
        bLapiz.Height = 20;
        bLapiz.Margin = new Thickness(4, 0, 0, 0);
        derecha.Children.Add(bLapiz);

        col.Children.Add(SectionHeader(Lateral.Glifos.Escritorio, NombreDeMonitor(p), derecha));

        // ---------- El campo del alias, si lo has abierto ----------
        if (_monsRenombrando == p.Clave) col.Children.Add(CampoDeAlias(p));

        // ---------- Qué entrada tiene ----------
        if (p.Actual is { } act)
        {
            var linea = new TextBlock
            {
                Text = $"Entrada actual: {Entradas.NombreDe(act)}",
                Foreground = _texto, FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 2),
            };
            col.Children.Add(linea);
        }
        else
        {
            // Y aquí el mensaje tiene que ser ACCIONABLE, que es la mitad del valor de este
            // módulo. «No se pudo» manda al usuario a buscar a ciegas; esto le dice dónde
            // mirar, y son dos sitios distintos según lo que haya contestado el monitor.
            var por = Tenue(p.Responde
                ? "Contesta por el cable, pero no gestiona su entrada: este modelo no permite " +
                  "cambiarla por software."
                : "No contesta por el cable. Activa DDC/CI en el menú del propio monitor " +
                  "(suele estar en Ajustes u OSD) y pulsa Actualizar.");
            por.TextWrapping = TextWrapping.Wrap;
            col.Children.Add(por);
            return Card(col);
        }

        // ---------- Los botones de entrada ----------
        var elegibles = p.Disponibles.Where(e => e.Codigo != p.Actual || p.Disponibles.Count == 1).ToList();

        if (p.Anunciadas)
        {
            var rejilla = new UniformGrid
            {
                Columns = Math.Min(3, Math.Max(1, p.Disponibles.Count)),
                Margin = new Thickness(-3, 4, -3, 0),
            };
            foreach (var e in p.Disponibles)
            {
                var suya = e;
                bool puesta = p.Actual == suya.Codigo;
                rejilla.Children.Add(QuickAction(
                    Lateral.Glifos.Escritorio,
                    suya.Nombre,
                    puesta,
                    () => CambiarEntrada(p, suya.Codigo),
                    puesta ? $"{suya.Nombre}: es la que está puesta"
                           : $"Pasar {NombreDeMonitor(p)} a {suya.Nombre}" +
                             (suya.Conocida ? "" : " (código que la norma MCCS no define)")));
            }
            col.Children.Add(rejilla);
        }
        else
        {
            // Sin capacidades no se dibujan botones de entradas no probadas. Es literal del
            // plan, y es lo correcto: aquí lo único que sabemos es en qué entrada está.
            var sin = Tenue("No anuncia qué entradas tiene. Puedes probar un código a mano.");
            sin.TextWrapping = TextWrapping.Wrap;
            sin.Margin = new Thickness(0, 2, 0, 0);
            col.Children.Add(sin);
            col.Children.Add(CampoDeCodigoManual(p));
        }

        // ---------- Volver a la de antes ----------
        if (_monsAntes.TryGetValue(p.Clave, out byte antes) && antes != p.Actual)
        {
            var volver = Boton(new TextBlock
            {
                Text = $"↩ Volver a {Entradas.NombreDe(antes)}",
                Foreground = _acentoLegible, FontSize = 11,
            }, $"Devolver {NombreDeMonitor(p)} a {Entradas.NombreDe(antes)}",
               () => CambiarEntrada(p, antes));
            volver.Height = 22;
            volver.HorizontalAlignment = HorizontalAlignment.Left;
            volver.Margin = new Thickness(-6, 4, 0, 0);
            col.Children.Add(volver);
        }

        return Card(col);
    }

    // El alias se guarda contra la clave del EDID, no contra `DISPLAY1`: ver `OpcionesMonitores`.
    string NombreDeMonitor(Entradas.Pantalla p) =>
        _cfg.Monitores.Alias.TryGetValue(p.Clave, out var a) && !string.IsNullOrWhiteSpace(a)
            ? a.Trim() : p.Rotulo;

    FrameworkElement CampoDeAlias(Entradas.Pantalla p)
    {
        var caja = new TextBox
        {
            Text = _cfg.Monitores.Alias.TryGetValue(p.Clave, out var a) ? a : "",
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 6),
            MaxLength = 40,
        };
        // El nombre de verdad del monitor como pista: así se ve a cuál le estás poniendo el
        // mote antes de escribirlo.
        caja.ToolTip = $"Un nombre para {p.Rotulo}. Vacío = su nombre de fábrica.";

        void Confirmar()
        {
            GuardarAliasDeMonitor(p.Clave, caja.Text);
            _monsRenombrando = null;
            RefrescarMonitores();
        }

        caja.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Confirmar(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _monsRenombrando = null; RefrescarMonitores(); e.Handled = true; }
        };
        caja.Loaded += (_, _) => { try { caja.Focus(); caja.SelectAll(); } catch { } };

        return caja;
    }

    // ---------- El código manual ----------
    //
    // La salida de emergencia que el plan pide para los monitores que no anuncian nada: «permitir
    // una configuración manual avanzada, sin presentar entradas no probadas como compatibles».
    //
    // Que sea un campo de texto y no una lista de botones es la parte importante. Una lista
    // diría «estas tienen que ir»; un campo dice «prueba y mira qué pasa», que es la verdad.
    FrameworkElement CampoDeCodigoManual(Entradas.Pantalla p)
    {
        var fila = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var caja = new TextBox { FontSize = 11.5, MaxLength = 4, ToolTip = "Un código VCP en hexadecimal: 0F, 11, 12…" };
        fila.Children.Add(caja);

        void Probar()
        {
            var t = (caja.Text ?? "").Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2);
            if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out byte c) || c == 0)
            {
                _monsEstado = $"«{caja.Text}» no es un código hexadecimal de un byte (prueba 0F, 11, 12).";
                RefrescarMonitores();
                return;
            }
            CambiarEntrada(p, c);
        }

        var b = Boton(new TextBlock { Text = "Probar", Foreground = _acentoLegible, FontSize = 11 },
                      "Mandar ese código al monitor", Probar);
        b.Height = 22;
        Grid.SetColumn(b, 1);
        fila.Children.Add(b);

        caja.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Probar(); e.Handled = true; } };
        return fila;
    }

    // ================= El trabajo lento, fuera del hilo de la interfaz =================

    void PedirMonitores(bool desdeCero)
    {
        if (_monsOcupado) return;
        _monsOcupado = true;
        if (desdeCero) { _mons = null; _monsEstado = null; }
        RefrescarMonitores();

        Task.Run(() =>
        {
            List<Entradas.Pantalla> lista;
            try
            {
                // `Replantear` olvida las capacidades guardadas y vuelve a preguntar si merece
                // la pena ofrecer el módulo. Solo cuando TÚ pulsas Actualizar o cuando cambia
                // la configuración de pantallas: en cualquier otro caso es un segundo por
                // pantalla para releer algo que no ha cambiado.
                if (desdeCero) Entradas.Replantear();
                lista = Entradas.Pantallas();
            }
            catch (Exception ex)
            {
                Log.Error("enumerar las entradas de los monitores", ex);
                lista = new List<Entradas.Pantalla>();
            }

            Dispatcher.InvokeAsync(() =>
            {
                _mons = lista;
                _monsOcupado = false;
                RefrescarMonitores();
            });
        });
    }

    void CambiarEntrada(Entradas.Pantalla p, byte codigo)
    {
        if (_monsOcupado) return;

        // La de antes, para poder volver. Se apunta ANTES de mandar nada: si el cambio
        // desconecta la pantalla donde está NubeBar, esta línea ya se ha ejecutado.
        if (p.Actual is { } ahora && ahora != codigo) _monsAntes[p.Clave] = ahora;

        _monsOcupado = true;
        _monsEstado = $"Cambiando {NombreDeMonitor(p)} a {Entradas.NombreDe(codigo)}…";
        _monsRenombrando = null;
        RefrescarMonitores();

        var clave = p.Clave;        // la CLAVE, no el objeto: ver la regla 2 de la cabecera
        var comoSeLlama = NombreDeMonitor(p);

        Task.Run(() =>
        {
            Entradas.Cambio r;
            try { r = Entradas.Poner(clave, codigo); }
            catch (Exception ex)
            {
                Log.Error($"cambiar la entrada de {clave}", ex);
                r = new Entradas.Cambio(Entradas.Resultado.Rechazada, null, "No se pudo cambiar la entrada.");
            }

            // Se vuelve a leer la lista entera para pintar el estado REAL y no el que
            // suponemos. Cuesta otra vuelta por el bus y vale la pena: el plan pide
            // «verificar el cambio leyendo de nuevo 0x60 y mostrando el resultado», y
            // enseñar el valor que pedimos en vez del que hay sería enseñar nuestro deseo.
            List<Entradas.Pantalla> lista;
            try { lista = Entradas.Pantallas(); }
            catch (Exception ex) { Log.Error("releer los monitores tras el cambio", ex); lista = new(); }

            Dispatcher.InvokeAsync(() =>
            {
                if (lista.Count > 0) _mons = lista;
                _monsOcupado = false;
                _monsEstado = $"{comoSeLlama}: {r.Mensaje}";

                // Si no llegó a cambiar, no se ofrece «volver» a donde ya está: sería un botón
                // que no hace nada.
                if (r.Que is Entradas.Resultado.YaEstaba or Entradas.Resultado.Rechazada
                           or Entradas.Resultado.Ignorada or Entradas.Resultado.NoGestiona
                           or Entradas.Resultado.NoEstaba)
                    _monsAntes.Remove(clave);

                RefrescarMonitores();
            });
        });
    }

    void RefrescarMonitores()
    {
        if (_panelAbierto != PanelMonitores) return;
        // Se sustituye el contenido sin cerrar y se vuelve a medir, igual que la batería: así no
        // hay que replegar y desplegar el cajón en cada paso de la operación.
        CambiarContenidoDelPanel(CrearMonitores());
        ReajustarPanel(PanelMonitores);
    }

    void GuardarAliasDeMonitor(string clave, string? alias)
    {
        try
        {
            alias = alias?.Trim();

            // Las dos islas tienen cada una su copia del config (ver `MainWindow.xaml.cs`), así
            // que el alias se pone en TODAS: si solo se tocara la de aquí, el mismo monitor
            // saldría con dos nombres según desde qué pantalla abras el panel.
            foreach (var i in Islas.ToList())
            {
                if (string.IsNullOrWhiteSpace(alias)) i._cfg.Monitores.Alias.Remove(clave);
                else i._cfg.Monitores.Alias[clave] = alias;
            }

            var arbol = ConfigArbol.Cargar();
            arbol.PonerMapa("Monitores", "Alias", _cfg.Monitores.Alias);
            arbol.Guardar();

            Log.Info(string.IsNullOrWhiteSpace(alias)
                ? $"monitores: quitado el alias de {clave}"
                : $"monitores: {clave} se llama «{alias}»");
        }
        catch (Exception ex) { Log.Error("guardar el alias de un monitor", ex); }
    }
}
