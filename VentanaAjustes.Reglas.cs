using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// D9.3: el editor de reglas.
//
// ---------- Por qué hacía falta ----------
//
// D6 dejó el panel: se podían ver, buscar y agrupar. Crear una exigía abrir `config.json` y
// escribir un objeto a mano, acertando con los nombres de los campos. Eso no es una función
// a medias: es una función que solo puede usar quien escribió el formato.
//
// ---------- Las dos decisiones del diseño ----------
//
// 1. **Se edita una copia.** Lo que se toca aquí vive en `_reglasEditadas` hasta que pulsas
//    Guardar. Escribir cada tecla en el árbol significaría que cancelar no cancela nada, y
//    que una regla a medio escribir queda guardada si cierras la ventana.
//
// 2. **Se dice a cuántas ventanas afecta, en el momento.** Una regla que no coincide con
//    nada es el error más común y el más difícil de ver: escribes «chrome» cuando el
//    proceso se llama «chrome.exe»... o al revés. El contador lo dice antes de guardar.
public sealed partial class VentanaAjustes
{
    List<HyprReglas.Regla>? _reglasEditadas;
    string? _editando;        // el id de la que está abierta en el formulario, si hay alguna

    // La lista sobre la que trabaja el editor. Se saca de la configuración una sola vez y
    // a partir de ahí manda ella: si se releyera en cada repintado, lo que estás escribiendo
    // se perdería en cuanto cambiaras de pestaña.
    List<HyprReglas.Regla> Reglas =>
        _reglasEditadas ??= HyprReglas.Instantanea(_hypr.NubeHypr.Reglas);

    void CambiaronLasReglas()
    {
        _arbol.PonerReglas("NubeHypr", Reglas);
        Cambio();                 // marca la ventana como "hay cambios sin guardar"
        RepintarReglas();
    }

    // ---------- La fila de acciones de cada regla ----------
    FrameworkElement AccionesDeRegla(HyprReglas.Regla r)
    {
        var fila = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
        };

        fila.Children.Add(BotonPlano(_editando == r.Id ? "Cerrar" : "Editar", () =>
        {
            _editando = _editando == r.Id ? null : r.Id;
            RepintarReglas();
        }, false));

        fila.Children.Add(BotonPlano(r.Activa ? "Apagar" : "Encender", () =>
        {
            _reglasEditadas = HyprReglas.Alternar(Reglas, r.Id);
            CambiaronLasReglas();
        }, false));

        // Quitar es destructivo, así que va en coral y pregunta. El resto de la ventana usa
        // `Confirmar` para esto mismo; aquí basta con que el botón lo diga, porque una regla
        // borrada por error se vuelve a escribir en diez segundos y no se pierde trabajo.
        var quitar = BotonPlano("Quitar", () =>
        {
            _reglasEditadas = HyprReglas.Quitar(Reglas, r.Id);
            if (_editando == r.Id) _editando = null;
            CambiaronLasReglas();
        }, false);
        quitar.Foreground = new SolidColorBrush(
            HyprPinta.CoralTinta(_roles, _roles.SuperficieAltaSolida));
        fila.Children.Add(quitar);

        return fila;
    }

    // ================= El formulario =================
    //
    // Criterios arriba, acciones abajo, y en medio la cuenta de a cuántas ventanas afecta.
    // El orden no es estético: es el orden en que se piensa una regla —"a QUÉ ventanas" y
    // luego "qué les hago"— y poner las acciones primero invita a escribir una regla que
    // hace algo sin saber todavía a quién.
    FrameworkElement FormularioDeRegla(HyprReglas.Regla r)
    {
        var caja = new StackPanel { Margin = new Thickness(0, 8, 0, 4) };

        void Cambiar(Func<HyprReglas.Regla, HyprReglas.Regla> como)
        {
            var actual = Reglas.FirstOrDefault(x => x.Id == r.Id);
            if (actual == null) return;
            _reglasEditadas = HyprReglas.Poner(Reglas, como(actual));
            CambiaronLasReglas();
        }

        // ---------- Lo primero: qué va a pasar de verdad ----------
        //
        // Va arriba del todo y no al final. Quien abre el formulario está a punto de escribir
        // algo que cree que moverá ventanas, y enterarse de que no al final de once campos es
        // enterarse tarde.
        var modo = HyprSalud.ModoConSeguro(_hypr.NubeHypr.Activo, _hypr.NubeHypr.Modo);
        caja.Children.Add(CajaConFilo(HyprGuia.LoQuePasaDeVerdad(modo),
                                      HyprPinta.Coral(_roles), 11.5));

        // ---------- Criterios ----------
        caja.Children.Add(Subtitulo("A qué ventanas se aplica",
            "Todo lo que pongas tiene que cumplirse a la vez. Lo que dejes en blanco no se mira."));

        caja.Children.Add(CampoDeTexto("Proceso", r.Proceso,
            "el nombre del programa, sin «.exe»",
            v => Cambiar(x => x with { Proceso = v }),
            "El nombre del ejecutable sin la extensión y sin la ruta.\n\n" +
            "Ejemplo: para Discord se escribe «Discord», no «Discord.exe» ni " +
            "«C:\\…\\Discord.exe».\n\n" +
            "No distingue mayúsculas. Es el criterio más usado y el que menos cambia."));

        caja.Children.Add(CampoDeTexto("Clase de ventana", r.Clase,
            "para distinguir una ventana de un diálogo del mismo programa",
            v => Cambiar(x => x with { Clase = v }),
            "La clase que Windows le da a la ventana. Sirve cuando un mismo programa abre " +
            "varias clases de ventana y solo quieres una.\n\n" +
            "Ejemplo: «Chrome_WidgetWin_1» es la ventana principal de los navegadores " +
            "basados en Chromium; sus diálogos tienen otra.\n\n" +
            "Si no sabes la clase, déjalo en blanco: casi nunca hace falta."));

        caja.Children.Add(CampoDeTexto("Título contiene", r.Titulo,
            "un trozo del título; no distingue acentos ni mayúsculas",
            v => Cambiar(x => x with { Titulo = v }),
            "Para aplicar la regla a UNA ventana concreta en vez de a todo el programa.\n\n" +
            "Ejemplo: Proceso «opera» + Título contiene «WhatsApp» toca esa pestaña y no el " +
            "resto del navegador.\n\n" +
            "Ojo: muchos programas cambian el título solos (el documento abierto). Elige un " +
            "trozo que no cambie."));

        caja.Children.Add(CampoDeTexto("Ruta contiene", r.Ruta,
            "un trozo de la ruta del ejecutable",
            v => Cambiar(x => x with { Ruta = v }),
            "Para distinguir dos programas que se llaman igual pero están en carpetas " +
            "distintas.\n\n" +
            "Ejemplo: «Steam» aplica la regla a todo lo que se ejecute desde la carpeta de " +
            "Steam.\n\n" +
            "Se compara por «contiene», así que no hace falta la ruta entera."));

        // ---------- A cuántas afecta, AHORA ----------
        caja.Children.Add(CuantasAfecta(r));

        // ---------- Acciones ----------
        caja.Children.Add(Subtitulo("Qué hacer con ellas", "Lo que dejes en blanco no se toca: " +
            "otra regla puede decidirlo, y si nadie lo dice, lo deja Windows."));

        caja.Children.Add(CampoDeNumero("Espacio", r.Espacio, "el escritorio virtual: 1, 2, 3…",
            v => Cambiar(x => x with { Espacio = v }),
            "A qué escritorio virtual de Windows enviar la ventana.\n\n" +
            "Se cuentan desde 1: el primero es el 1, no el 0.\n\n" +
            "Ejemplo: Proceso «Discord» + Espacio 2 manda Discord al segundo escritorio."));

        caja.Children.Add(CampoDeNumero("Monitor", r.Monitor, "0 es el principal",
            v => Cambiar(x => x with { Monitor = v }),
            "En qué pantalla colocarla. Los monitores SÍ se cuentan desde 0, y el 0 es el " +
            "principal.\n\n" +
            "Ejemplo: Proceso «olk» + Monitor 1 abre el correo en la segunda pantalla.\n\n" +
            "Si ese monitor no está conectado, la regla no se aplica — y vuelve a funcionar " +
            "al reconectarlo."));

        caja.Children.Add(CampoDeLayout(r, l => Cambiar(x => x with { Layout = l })));

        caja.Children.Add(CampoDeFraccion("Ancho", r.Ancho,
            "fracción de la pantalla: 0,5 es la mitad",
            v => Cambiar(x => x with { Ancho = v }),
            "El ancho inicial, en fracción de la pantalla y no en píxeles: así la misma " +
            "regla vale en un portátil y en un 4K.\n\n" +
            "Ejemplo: 0,5 es media pantalla de ancho.\n\n" +
            "Puedes poner solo el ancho: el alto se queda como estuviera."));

        caja.Children.Add(CampoDeFraccion("Alto", r.Alto, "fracción de la pantalla: 0,6",
            v => Cambiar(x => x with { Alto = v }),
            "El alto inicial, en fracción de la pantalla.\n\n" +
            "Ejemplo: 0,6 ocupa poco más de la mitad de alto.\n\n" +
            "Déjalo en blanco para no tocar el alto."));

        caja.Children.Add(CampoDeTres("Flotante", r.Flotante,
            "fuera del reparto, donde Windows la ponga",
            v => Cambiar(x => x with { Flotante = v }),
            "«Sí» saca la ventana del reparto de la pantalla: queda donde Windows la ponga " +
            "y las demás se reparten el hueco sin contar con ella.\n\n" +
            "Ejemplo: la calculadora o un reproductor pequeño.\n\n" +
            "«No opina» es distinto de «No»: no opinar deja que lo decida otra regla."));

        caja.Children.Add(CampoDeTres("Darle el foco", r.Foco,
            "traerla delante al aparecer",
            v => Cambiar(x => x with { Foco = v }),
            "«Sí» trae la ventana al frente en cuanto aparece.\n\n" +
            "Ejemplo: útil para el programa con el que empiezas a trabajar; molesto para el " +
            "chat, que te robaría el teclado a mitad de una frase.\n\n" +
            "«No» la deja detrás a propósito."));

        caja.Children.Add(CampoDeNumero("Prioridad", r.Prioridad,
            "más alta, manda antes",
            v => Cambiar(x => x with { Prioridad = v ?? 0 }),
            "Solo importa cuando dos reglas rellenan EL MISMO campo con valores distintos: " +
            "gana la de prioridad más alta.\n\n" +
            "Con la misma prioridad gana la más concreta (la que mira más criterios).\n\n" +
            "Si tus reglas tocan campos distintos, no hace falta tocar esto: se combinan."));

        // ---------- La vista previa, en castellano ----------
        //
        // Once campos rellenos no dicen si has entendido lo que estás pidiendo. Una frase sí,
        // porque se lee y se compara con lo que querías. Y sale de la MISMA función que la
        // guía: si aquí se escribiera a mano, el día que una cambiara habría dos verdades.
        caja.Children.Add(Subtitulo("Esto es lo que hará", ""));
        var previa = Rotulo(HyprGuia.EnPalabras(r));
        previa.TextWrapping = TextWrapping.Wrap;
        previa.FontSize = 12.5;
        previa.Margin = new Thickness(0, 2, 0, 0);
        previa.Foreground = new SolidColorBrush(
            HyprPinta.AzulTinta(_roles, _roles.SuperficieAltaSolida));
        caja.Children.Add(previa);

        // ---------- Lo que va mal, TODO a la vez ----------
        //
        // `HyprReglas.Valida` da un motivo y para. Aquí se enseñan todos: arreglar uno,
        // guardar y encontrarse con el siguiente es la forma de que el editor parezca que va
        // a trompicones.
        foreach (var p in HyprGuia.Problemas(r))
            caja.Children.Add(CajaConFilo(
                p.Texto,
                p.Grave ? HyprPinta.Coral(_roles) : _roles.Borde,
                11));

        // ---------- Y el botón de probar ----------
        caja.Children.Add(BotonDeProbar(r));

        return caja;
    }

    // ---------- Cuántas ventanas de las abiertas coinciden ----------
    //
    // Es la pieza que convierte el editor en algo usable. El error más común al escribir una
    // regla es que no coincida con nada —«chrome» cuando el proceso se llama «chrome.exe», o
    // al revés— y sin esto no te enteras hasta que abres una ventana y no pasa nada.
    //
    // Se calcula con el MISMO `Coincide` que usa el motor. Si se recalculara aquí con otra
    // cuenta, el panel diría una cosa y el motor haría otra.
    FrameworkElement CuantasAfecta(HyprReglas.Regla r)
    {
        int cuantas = 0;
        var ejemplos = new List<string>();

        // ---------- Se cuenta COMO SI estuviera encendida ----------
        //
        // `HyprReglas.Coincide` descarta de entrada las reglas apagadas, y eso aquí mentía:
        // una regla de Discord recién creada —que nace apagada— decía «no coincide con
        // ninguna ventana abierta» con Discord delante. Quien lo lee concluye que el nombre
        // del proceso está mal y se pone a cambiarlo, que es el error contrario al que este
        // contador existe para evitar.
        //
        // Que esté apagada ya se dice en su propio aviso, abajo, y en la frase de la vista
        // previa. Aquí se contesta solo la pregunta que se hace: ¿a qué ventanas afectaría?
        var comoSiEstuviera = r with { Activa = true };

        try
        {
            foreach (var w in GestorVentanas.Todas)
            {
                var v = new VentanaModelo(w.Hwnd, NubeHypr.NombreDeProceso(w.Exe), w.Titulo,
                                          HyprAdaptador.DeRect(w.Rect));
                if (!HyprReglas.Coincide(comoSiEstuviera, v, Nativo.Clase(w.Hwnd), w.Exe, out _))
                    continue;
                cuantas++;
                if (ejemplos.Count < 3) ejemplos.Add(w.App.Length > 0 ? w.App : w.Titulo);
            }
        }
        catch (Exception ex) { Log.Error("contar a cuántas ventanas afecta una regla", ex); }

        var caja = new Border
        {
            Background = new SolidColorBrush(_roles.Superficie),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 8, 0, 10),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(cuantas > 0
                ? HyprPinta.Azul(_roles)
                : HyprPinta.Coral(_roles)),
        };

        var t = Rotulo(cuantas == 0
            ? "Ahora mismo no coincidiría con ninguna ventana abierta. Puede ser normal —esa " +
              "aplicación no está abierta— o puede que el nombre del proceso no sea ese."
            : $"Ahora mismo afectaría a {cuantas} ventana{(cuantas == 1 ? "" : "s")}" +
              (ejemplos.Count > 0 ? ": " + string.Join(", ", ejemplos) : "") +
              (cuantas > ejemplos.Count ? "…" : ""));
        t.TextWrapping = TextWrapping.Wrap;
        t.FontSize = 11.5;
        caja.Child = t;
        return caja;
    }

    // ================= Los campos =================
    //
    // Cada uno escribe su campo y deja los demás como estaban. Todos tratan el vacío como
    // **null**, que en una regla significa "no lo mires" — y eso es distinto de cero o de
    // falso, que son decisiones.

    FrameworkElement Subtitulo(string texto, string ayuda)
    {
        var p = new StackPanel { Margin = new Thickness(0, 10, 0, 4) };
        var t = Rotulo(texto);
        t.FontWeight = FontWeights.SemiBold;
        p.Children.Add(t);
        // Un subtítulo sin ayuda no deja un renglón vacío: deja de poner el renglón.
        if (ayuda is { Length: > 0 })
        {
            var a = Rotulo(ayuda);
            a.FontSize = 11;
            a.Opacity = 0.6;
            a.TextWrapping = TextWrapping.Wrap;
            p.Children.Add(a);
        }
        return p;
    }

    // ---------- Una fila: etiqueta, control, ayuda y tooltip ----------
    //
    // El `tooltip` es para el EJEMPLO largo y la ayuda de debajo para la frase corta. Son dos
    // cosas distintas: la ayuda está siempre a la vista y tiene que caber en un renglón; el
    // tooltip puede permitirse tres líneas con un caso concreto, porque solo aparece cuando
    // alguien se para encima a preguntárselo.
    //
    // Y el tooltip va en la ETIQUETA además de en el control: quien no sabe qué es «Clase de
    // ventana» pasa el ratón por el nombre, no por la caja vacía de al lado.
    FrameworkElement FilaDeCampo(string etiqueta, UIElement control, string? ayuda,
                                 string? tooltip = null)
    {
        var fila = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var r = Rotulo(etiqueta);
        r.VerticalAlignment = VerticalAlignment.Center;
        r.FontSize = 11.5;
        if (tooltip is { Length: > 0 })
        {
            r.ToolTip = tooltip;
            if (control is FrameworkElement fe) fe.ToolTip = tooltip;
            // Para quien no usa ratón: el lector de pantalla lo lee como la descripción del
            // campo. Un tooltip que solo existe al pasar el cursor deja fuera a quien navega
            // con el teclado.
            System.Windows.Automation.AutomationProperties.SetHelpText(control, tooltip);
            System.Windows.Automation.AutomationProperties.SetName(control, etiqueta);
        }
        fila.Children.Add(r);

        var derecha = new StackPanel();
        derecha.Children.Add(control);
        if (ayuda is { Length: > 0 })
        {
            var a = Rotulo(ayuda);
            a.FontSize = 10.5;
            a.Opacity = 0.55;
            a.TextWrapping = TextWrapping.Wrap;
            a.Margin = new Thickness(0, 2, 0, 0);
            derecha.Children.Add(a);
        }
        Grid.SetColumn(derecha, 1);
        fila.Children.Add(derecha);
        return fila;
    }

    TextBox CajaDeTexto(string? valor)
    {
        return new TextBox
        {
            Text = valor ?? "",
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            Foreground = _texto,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 5, 8, 5),
        };
    }

    FrameworkElement CampoDeTexto(string etiqueta, string? valor, string ayuda,
                                  Action<string?> poner, string? tooltip = null)
    {
        var c = CajaDeTexto(valor);
        // Al perder el foco, no en cada tecla: escribir en el árbol letra a letra haría que
        // «fire» quedara guardado un instante como una regla distinta de «firefox», y el
        // contador de coincidencias parpadearía con cada pulsación.
        c.LostFocus += (_, _) =>
        {
            var v = c.Text.Trim();
            poner(v.Length == 0 ? null : v);
        };
        return FilaDeCampo(etiqueta, c, ayuda, tooltip);
    }

    FrameworkElement CampoDeNumero(string etiqueta, int? valor, string ayuda, Action<int?> poner,
                                   string? tooltip = null)
    {
        var c = CajaDeTexto(valor?.ToString());
        c.LostFocus += (_, _) =>
        {
            var v = c.Text.Trim();
            if (v.Length == 0) { poner(null); return; }
            if (int.TryParse(v, out int n)) poner(n);
            else c.Text = valor?.ToString() ?? "";   // lo que no es un número, se rechaza
        };
        return FilaDeCampo(etiqueta, c, ayuda, tooltip);
    }

    FrameworkElement CampoDeFraccion(string etiqueta, double? valor, string ayuda,
                                     Action<double?> poner, string? tooltip = null)
    {
        var c = CajaDeTexto(valor?.ToString("0.##", CultureInfo.CurrentCulture));
        c.LostFocus += (_, _) =>
        {
            var v = c.Text.Trim();
            if (v.Length == 0) { poner(null); return; }

            // Con la coma y con el punto: en español se escribe 0,5 y en el fichero vive
            // como 0.5. Rechazar una de las dos formas sería rechazar a medio mundo.
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.CurrentCulture, out double d) ||
                double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                poner(Math.Clamp(d, 0.05, 1.0));
            else
                c.Text = valor?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
        };
        return FilaDeCampo(etiqueta, c, ayuda, tooltip);
    }

    // Tres estados, no dos: sí, no y **no opino**. Una casilla normal no puede decir "no
    // opino", y esa es justamente la diferencia entre "esta regla la pone flotante" y "esta
    // regla no habla de flotar" — que es lo que deja decidir a otra regla.
    FrameworkElement CampoDeTres(string etiqueta, bool? valor, string ayuda, Action<bool?> poner,
                                 string? tooltip = null)
    {
        // Vestido como el resto (ver `Desplegable`): antes se creaba a pelo con el texto en
        // negro, porque la plantilla de serie de WPF lo pintaba blanco en una ventana oscura.
        var combo = Desplegable(new[] { "No opina", "Sí", "No" });
        combo.SelectedIndex = valor == null ? 0 : valor.Value ? 1 : 2;
        combo.SelectionChanged += (_, _) => poner(combo.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => null,
        });
        return FilaDeCampo(etiqueta, combo, ayuda, tooltip);
    }

    FrameworkElement CampoDeLayout(HyprReglas.Regla r, Action<NubeHypr.Layout?> poner)
    {
        var nombres = new List<string> { "No opina" };
        nombres.AddRange(Enum.GetNames<NubeHypr.Layout>());

        var combo = Desplegable(nombres);
        combo.SelectedIndex = r.Layout == null ? 0 : nombres.IndexOf(r.Layout.ToString()!);
        combo.SelectionChanged += (_, _) =>
            poner(combo.SelectedIndex <= 0
                ? null
                : Enum.Parse<NubeHypr.Layout>(nombres[combo.SelectedIndex]));
        return FilaDeCampo("Layout", combo, "cómo se reparte la pantalla con las demás",
            "Cómo se reparten la pantalla las ventanas que NO son flotantes.\n\n" +
            "• Principal: una grande a un lado y las demás apiladas al otro.\n" +
            "• Horizontal: columnas una al lado de otra, de izquierda a derecha.\n" +
            "• Vertical: filas una encima de otra, de arriba abajo.\n" +
            "• Flotante: no se reparte nada.\n\n" +
            "Ojo con los nombres: «horizontal» es la dirección en que se recorre la " +
            "pantalla, no la forma de las ventanas.");
    }

    // ================= Añadir =================
    //
    // Nace apagada y sin criterios. Apagada porque una regla recién creada está a medias por
    // definición, y encenderla sola significaría que el simple hecho de pulsar «Añadir» ya
    // decide sobre tus ventanas. El id sale de `IdLibre`, que no repite.
    void AnadirRegla()
    {
        var id = HyprReglas.IdLibre(Reglas, "regla");
        _reglasEditadas = HyprReglas.Poner(Reglas, new HyprReglas.Regla(id, Activa: false));
        _editando = id;
        CambiaronLasReglas();
    }

    // ---------- Solo para `--capturas` ----------
    //
    // Mete una regla de muestra en la copia en memoria y abre su formulario, para poder
    // fotografiar el editor. **No escribe nada**: el diagnóstico corre con
    // `Config.SoloLectura` y la ventana se cierra sin guardar.
    //
    // Está aquí y no en el diagnóstico porque `_reglasEditadas` es privado de esta ventana,
    // y abrirlo al mundo para una captura sería pagar un precio permanente por una foto.
    internal void ReglaDeMuestraParaLaCaptura()
    {
        try
        {
            var id = HyprReglas.IdLibre(Reglas, "ejemplo");
            _reglasEditadas = HyprReglas.Poner(Reglas, new HyprReglas.Regla(
                id, Prioridad: 5, Proceso: "explorer",
                Espacio: 2, Ancho: 0.5, Flotante: false, Activa: true));
            _editando = id;
            RepintarReglas();        // repintar, no `CambiaronLasReglas`: NO se toca el árbol
        }
        catch (Exception ex) { Log.Error("la regla de muestra para la captura", ex); }
    }

    // Desplazar la página abierta. También para `--capturas`: el editor vive debajo del
    // estado y de la caja de búsqueda, así que una foto de la página sin desplazar retrata
    // justo lo que ya estaba retratado.
    internal void DesplazarLaPagina(double cuanto)
    {
        try
        {
            if (_pestanas?.SelectedItem is TabItem t && t.Content is ScrollViewer sv)
            {
                sv.UpdateLayout();
                sv.ScrollToVerticalOffset(cuanto < 0 ? sv.ScrollableHeight : cuanto);
                sv.UpdateLayout();
            }
        }
        catch (Exception ex) { Log.Error("desplazar la página de Ajustes", ex); }
    }
}
