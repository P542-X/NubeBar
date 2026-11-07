using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// La página «Módulos» de Ajustes, rehecha.
//
// ---------- Qué contesta ahora y antes no ----------
//
// Las preguntas eran: qué tengo puesto, dónde, qué me queda por poner, cuál puedo repetir,
// cómo lo muevo sin arrastrar, y dónde va a caer lo que arrastro. Ninguna tenía respuesta
// en tres `ListBox` de ids en crudo.
//
//  · **Qué hay puesto y dónde** — tres columnas con chips en el orden real de la barra, y
//    cada chip con su icono, su nombre legible y una chapita de estado.
//  · **Qué queda** — abajo, el resto del catálogo, también como chips, con un botón de
//    añadir POR ZONA. El de antes añadía siempre a la izquierda.
//  · **Qué se repite** — lo dice el chip, y el botón de añadir sale deshabilitado con el
//    motivo cuando ya está puesto y no es repetible. Antes el botón respondía y no pasaba
//    nada.
//  · **Cómo se mueve sin arrastrar** — cada chip puesto lleva sus flechas: zona anterior,
//    zona siguiente, subir, bajar. Todas son botones de verdad, así que llegan con el
//    tabulador y con el teclado. Arrastrar sigue estando, pero ya no es la única forma, y
//    esa era la mitad que fallaba en ventanas pequeñas y con mucho DPI.
//  · **Dónde va a caer** — una línea de inserción entre dos chips mientras arrastras, en
//    una capa por encima para que nada se mueva hasta que sueltes.
//  · **Y soltar fuera no pierde nada** — se decide el destino ANTES de tocar el modelo. El
//    `Drop` de antes quitaba del origen y después buscaba destino.
//
// ---------- Y el mismo sistema visual que la barra ----------
//
// El icono de cada chip es `DescriptorWidget.Glifo`: el mismo que usa la biblioteca y el
// mismo que se pinta en la isla. Siete módulos no tenían ninguno —reloj, fecha, nube, cpu,
// ram, batería, notificaciones— y nadie lo había notado porque el sitio donde se enseñaban
// era una lista de texto. Ahora los tienen.
//
// La lógica —qué estado tiene cada módulo, qué acciones admite, dónde cae un arrastre—
// está en `EditorModulos`, sin WPF y con pruebas. Esto solo dibuja.
public partial class VentanaAjustes
{
    // ---------- Por qué el chip tiene DOS filas ----------
    //
    // La primera versión lo puso todo en una: asa, icono, nombre, estado y seis botones.
    // En la captura se vio lo que la aritmética ya decía y yo no había hecho: el contenido
    // de Ajustes mide unos 660 px, tres columnas salen a 220, y asa (20) + icono (20) +
    // seis botones (132) dejan menos de cincuenta píxeles para el nombre. El nombre
    // desaparecía — justo lo que esta página venía a arreglar.
    //
    // Dos filas: arriba lo que hay que leer, abajo lo que hay que pulsar. Cabe en 220 px y
    // sigue cabiendo con la ventana más estrecha y con el texto más grande.
    const double AltoChip = 54;
    const double AltoColumna = 330;

    StackPanel? _colIzq, _colCen, _colDer;
    StackPanel? _disponibles;
    Canvas? _marcaDestino;
    Grid? _rejillaZonas;

    ZonasWidgets Zonas => new(_arbol);

    // Un glifo de la fuente de iconos, como `TextBlock`. La ventana de ajustes ya tiene un
    // `Glifo(int)` que devuelve la CADENA, no el bloque, y media docena de sitios se montan
    // el TextBlock a mano con la misma familia y el mismo tamaño. Aquí hacen falta veinte,
    // así que va una sola vez.
    TextBlock Pinta(string glifo, double tamano, Brush color) => new()
    {
        Text = glifo,
        FontFamily = new FontFamily(FuenteDeIconos),
        FontSize = tamano,
        Foreground = color,
        VerticalAlignment = VerticalAlignment.Center,
    };

    UIElement PaginaDeModulos()
    {
        var p = NuevaPagina("modulos");

        p.Grupo("Lo que tienes puesto, por zonas",
                IconoDeGrupo("Lo que tienes puesto, por zonas"));
        p.Mas(new TextBlock
        {
            Text = "Arrastra un módulo de una columna a otra, o usa sus flechas. " +
                   "Las flechas funcionan con el teclado; arrastrar, no.",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });

        // ---------- Las rayitas que NO son módulos ----------
        //
        // La barra dibuja una raya fina entre grupos de módulos de distinta categoría. No son
        // módulos, así que no salen en las columnas de abajo — y el usuario se encontró dos
        // rayas en su barra que no podía ni localizar aquí ni quitar de ningún sitio: «en la
        // nube hay 2 separadores que no se ven en los ajustes».
        //
        // El interruptor va JUNTO a las columnas y no en otra página, porque la pregunta
        // («¿qué son esas rayas?») se hace mirando esta lista y no encontrándolas.
        p.Mas(Interruptor("Separadores automáticos entre grupos",
                          _arbol.Bandera("Barra", "SeparadoresDeGrupo", false),
                          v => _arbol.Poner("Barra", "SeparadoresDeGrupo", v)));
        var pistaRayas = Rotulo(
            "Vienen apagados. Encendidos, dibujan una rayita fina entre módulos de distinto " +
            "tipo: por ejemplo entre el reloj y el grupo de CPU, memoria y bandeja. No son " +
            "módulos, y por eso no salen en las columnas de aquí abajo. Apagados, los grupos " +
            "se distinguen por el hueco. El módulo «Separador» —el que colocas tú— es otra " +
            "cosa y no se toca.");
        pistaRayas.TextWrapping = TextWrapping.Wrap;
        pistaRayas.Opacity = 0.55;
        p.Mas(pistaRayas);

        _rejillaZonas = new Grid();
        for (int i = 0; i < 3; i++) _rejillaZonas.ColumnDefinitions.Add(new ColumnDefinition());

        _colIzq = ColumnaDeZona("Izquierda", 0);
        _colCen = ColumnaDeZona("Centro", 1);
        _colDer = ColumnaDeZona("Derecha", 2);

        // La marca de destino va en una capa POR ENCIMA y no como un hijo más de la
        // columna. Insertándola entre los chips, cada movimiento del ratón empujaba a los
        // demás, lo que movía la propia marca: temblaba y saltaba entre dos posiciones.
        // Aquí no participa en la medida y nada se recoloca hasta que sueltas.
        _marcaDestino = new Canvas { IsHitTestVisible = false };
        var capa = new Grid();
        capa.Children.Add(_rejillaZonas);
        capa.Children.Add(_marcaDestino);
        p.Mas(capa);

        p.Grupo("Disponibles", IconoDeGrupo("Disponibles"));
        p.Mas(new TextBlock
        {
            Text = "Pulsa la zona donde quieres ponerlo. Lo que ya está puesto y no se " +
                   "puede repetir sale apagado, con el motivo.",
            Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });
        _disponibles = new StackPanel();
        p.Mas(_disponibles);

        Repintar();
        return p.Fin();
    }

    // ================= Las columnas =================
    StackPanel ColumnaDeZona(string zona, int columna)
    {
        var pila = new StackPanel();

        var titulo = new TextBlock
        {
            Text = zona, Foreground = _acentoLegible, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(4, 0, 0, 6),
        };

        var dentro = new StackPanel { Margin = new Thickness(2) };
        var caja = new Border
        {
            Background = _superficie,
            CornerRadius = new CornerRadius(8),
            BorderBrush = _superficieAlta, BorderThickness = new Thickness(1),
            MinHeight = AltoColumna,
            Margin = new Thickness(3),
            Padding = new Thickness(4),
            AllowDrop = true,
            Child = new ScrollViewer
            {
                Content = dentro,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = AltoColumna,
            },
        };
        caja.Tag = zona;

        caja.DragOver += (s, e) =>
        {
            bool nuestro = e.Data.GetDataPresent(FormatoChip) || e.Data.GetDataPresent(FormatoNuevo);
            if (!nuestro) { e.Effects = DragDropEffects.None; return; }
            e.Effects = e.Data.GetDataPresent(FormatoNuevo) ? DragDropEffects.Copy : DragDropEffects.Move;
            e.Handled = true;
            PintarDestino(caja, dentro, e.GetPosition(dentro));
        };
        caja.DragLeave += (_, _) => BorrarDestino();
        caja.Drop += (s, e) => Soltar(zona, dentro, e);

        pila.Children.Add(titulo);
        pila.Children.Add(caja);
        Grid.SetColumn(pila, columna);
        _rejillaZonas!.Children.Add(pila);

        // El StackPanel de dentro es el que se rellena en cada repintado; la caja y el
        // título se quedan.
        pila.Tag = dentro;
        return dentro;
    }

    // ---------- Dos formatos de arrastre, y por qué hacen falta dos ----------
    //
    // `FormatoChip` lleva «zona|índice»: la INSTANCIA exacta que estás arrastrando. Llevaba
    // solo el id, y con un módulo repetible eso es ambiguo: arrastrar el tercer separador
    // buscaba su sitio con `DondeEsta(id)`, que devuelve el PRIMERO, y acababas moviendo
    // uno que no habías tocado.
    //
    // `FormatoNuevo` lleva el id de un módulo del catálogo. Sin él, soltar algo que aún no
    // está puesto no se distinguía de mover algo que sí, y la única forma de añadir era el
    // botón de zona, que pone siempre al final: de ahí que los separadores «se acumularan»
    // en vez de ponerse donde uno quiere.
    const string FormatoChip = "nubebar-modulo";
    const string FormatoNuevo = "nubebar-modulo-nuevo";

    // ================= Repintar =================
    //
    // Se vuelve a dibujar entero y a propósito: la página tiene veinte chips, rehacerla
    // cuesta nada y así no hay dos caminos —uno que crea y otro que actualiza— que puedan
    // discrepar. Es lo contrario que en la barra, donde remontar sí se nota.
    void Repintar()
    {
        if (_colIzq == null || _colCen == null || _colDer == null || _disponibles == null) return;

        var z = Zonas;
        var fichas = EditorModulos.Fichas(z);

        foreach (var (zona, pila) in new[]
        {
            ("Izquierda", _colIzq), ("Centro", _colCen), ("Derecha", _colDer),
        })
        {
            pila.Children.Clear();
            var enLaZona = fichas.Where(f => f.Puesto && f.Zona == zona)
                                 .OrderBy(f => f.Posicion).ToList();
            if (enLaZona.Count == 0)
                pila.Children.Add(new TextBlock
                {
                    Text = "Vacía",
                    Foreground = _textoAtenuado, FontSize = 11,
                    Margin = new Thickness(8, 10, 0, 0),
                });
            foreach (var f in enLaZona) pila.Children.Add(ChipPuesto(f, enLaZona.Count));
        }

        // ---------- Los que faltan ----------
        _disponibles.Children.Clear();
        var fila = new WrapPanel();
        // `DistinctBy(Id)`: desde que hay una ficha por INSTANCIA, un separador puesto tres
        // veces traía tres fichas, y sin esto saldría tres veces en el catálogo de abajo.
        // Ahí se añade por id, así que basta con una.
        foreach (var f in fichas.Where(f => !f.Puesto || f.Repetible)
                                .DistinctBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(f => f.Categoria).ThenBy(f => f.Nombre))
            fila.Children.Add(ChipDisponible(f));
        _disponibles.Children.Add(fila);

        // ---------- Y los que la barra no conoce ----------
        //
        // Un config escrito por una NubeBar más nueva, o un módulo retirado. Se enseñan
        // para poder quitarlos: si no, la barra dibuja un hueco de «módulo desconocido» y
        // desde aquí no había forma de limpiarlo.
        var raros = EditorModulos.Desconocidos(z);
        if (raros.Count > 0)
        {
            _disponibles.Children.Add(new TextBlock
            {
                Text = "Puestos pero desconocidos para esta versión de NubeBar. " +
                       "Dibujan un hueco vacío en la barra:",
                Foreground = _textoAtenuado, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 6),
            });
            var filaRaros = new WrapPanel();
            foreach (var id in raros)
            {
                var suyo = id;
                var b = BotonChico(EditorModulos.Glifo(EditorModulos.Accion.Quitar),
                                   $"Quitar «{suyo}»",
                                   () => { Zonas.Quitar(suyo); Guardado(); });
                var caja = new StackPanel { Orientation = Orientation.Horizontal };
                caja.Children.Add(new TextBlock
                {
                    Text = suyo, Foreground = _texto, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 4, 0),
                });
                caja.Children.Add(b);
                filaRaros.Children.Add(new Border
                {
                    Background = _superficie, CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(2),
                    Child = caja,
                });
            }
            _disponibles.Children.Add(filaRaros);
        }
    }

    // ---------- Un cambio en los módulos, como cualquier otro cambio ----------
    //
    // Esto guardaba al instante —`_arbol.Guardar()` directo— con el razonamiento de que el
    // usuario acaba de pulsar algo y quiere verlo ya. Era un error, y el que explica «el
    // separador a veces no se guarda»:
    //
    // la ventana de Ajustes revierte el fichero al cerrarse si no se ha pulsado Guardar
    // (`if (!_guardado) _arbol.Revertir()`), y su pie lo dice con todas las letras: «Los
    // cambios se guardan al pulsar Guardar». Guardando por mi cuenta, el cambio llegaba al
    // disco, la barra lo mostraba… y al cerrar con Cancelar o con la X la reversión lo
    // deshacía. Daba la sensación de «a veces sí y a veces no» porque dependía de por dónde
    // se cerrara la ventana.
    //
    // Ahora sigue el mismo camino que el resto de la ventana: se toca el árbol, se avisa
    // del cambio, y Guardar y Cancelar hacen lo que prometen. `Repintar` deja la página al
    // día leyendo del árbol, así que lo que ves sigue siendo lo que has editado.
    void Guardado()
    {
        Cambio();
        Repintar();
    }

    // ================= Un chip puesto =================
    UIElement ChipPuesto(EditorModulos.Ficha f, int cuantosEnLaZona)
    {
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // asa
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // icono
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // El asa de arrastre. Existe para que se VEA que esto se puede arrastrar: sin
        // ella, un chip arrastrable y uno que no lo es se dibujan igual.
        var asa = Pinta(Glifo(0xE700), 11, _textoAtenuado);
        asa.Margin = new Thickness(4, 0, 4, 0);
        Grid.SetColumn(asa, 0);
        fila.Children.Add(asa);

        var icono = Pinta(f.Glifo, 13, f.Oculto ? _textoAtenuado : _texto);
        icono.Margin = new Thickness(0, 0, 6, 0);
        Grid.SetColumn(icono, 1);
        fila.Children.Add(icono);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = f.Nombre, Foreground = f.Oculto ? _textoAtenuado : _texto,
            FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        // La chapita de estado. Nunca vacía: un chip sin estado obliga a deducirlo de la
        // columna en la que está, que es justo lo que no se entendía.
        if (f.Oculto || f.Cuantos > 1)
            textos.Children.Add(new TextBlock
            {
                Text = EditorModulos.Estado(f), Foreground = _textoAtenuado, FontSize = 9.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        Grid.SetColumn(textos, 2);
        fila.Children.Add(textos);

        // La segunda fila: lo que se pulsa. Separada de lo que se lee, para que el nombre
        // no tenga que competir por el ancho con seis botones.
        var botones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 1, 0, 0),
        };
        foreach (var a in new[]
        {
            EditorModulos.Accion.ALaIzquierda, EditorModulos.Accion.Subir,
            EditorModulos.Accion.Bajar, EditorModulos.Accion.ALaDerecha,
            f.Oculto ? EditorModulos.Accion.Mostrar : EditorModulos.Accion.Ocultar,
            EditorModulos.Accion.Quitar,
        })
            botones.Children.Add(BotonDeAccion(a, f, cuantosEnLaZona));

        var dosFilas = new StackPanel();
        dosFilas.Children.Add(fila);
        dosFilas.Children.Add(botones);

        var chip = new Border
        {
            Background = f.Oculto ? _superficie : _superficieAlta,
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(0, 0, 0, 4),
            Padding = new Thickness(2, 2, 2, 2),
            Height = AltoChip - 4,
            Opacity = f.Oculto ? 0.72 : 1.0,
            Child = dosFilas,
            Cursor = Cursors.SizeAll,
            ToolTip = EditorModulos.Explicar(f),
        };
        AutomationProperties.SetName(chip, $"{f.Nombre}, {EditorModulos.Estado(f)}");
        AutomationProperties.SetAutomationId(chip, f.Id);

        // ---------- Arrastrar ----------
        //
        // Con umbral: `DoDragDrop` nada más pulsar convierte cualquier clic torpe en un
        // arrastre, y en una ventana de ajustes eso significa mover módulos sin querer.
        Point? pulsado = null;
        chip.PreviewMouseLeftButtonDown += (_, e) => pulsado = e.GetPosition(chip);
        chip.PreviewMouseLeftButtonUp += (_, _) => pulsado = null;
        chip.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || pulsado == null) return;
            var ahora = e.GetPosition(chip);
            if (Math.Abs(ahora.X - pulsado.Value.X) < 6 && Math.Abs(ahora.Y - pulsado.Value.Y) < 6) return;
            pulsado = null;
            try
            {
                // «zona|índice»: la instancia exacta, no el id. Ver la nota de los dos
                // formatos: con el id a secas, arrastrar el tercer separador movía el primero.
                DragDrop.DoDragDrop(chip, new DataObject(FormatoChip, $"{f.Zona}|{f.Posicion}"),
                                    DragDropEffects.Move);
            }
            finally { BorrarDestino(); }      // pase lo que pase, la marca se va
        };

        return chip;
    }

    // ================= Un chip disponible =================
    UIElement ChipDisponible(EditorModulos.Ficha f)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal };

        var icono = Pinta(f.Glifo, 13, _texto);
        icono.Margin = new Thickness(8, 0, 6, 0);
        fila.Children.Add(icono);

        fila.Children.Add(new TextBlock
        {
            Text = f.Nombre, Foreground = _texto, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        // Un botón por zona, en vez del «añadir a la izquierda» de antes que además
        // mentía en su rótulo. Teniendo las tres delante no hay que adivinar dónde cae.
        string? no = EditorModulos.PorQueNo(EditorModulos.Accion.Anadir, f);
        foreach (var zona in ZonasWidgets.Nombres)
        {
            var suya = zona;
            var b = new Button
            {
                Content = new TextBlock { Text = zona[..3], FontSize = 10, Foreground = _texto },
                // La misma plantilla que los botones del chip, y por lo mismo: sin ella, el
                // botón de una zona donde no se puede añadir salía como un bloque gris de
                // WPF, más llamativo que los dos que sí se pueden pulsar.
                Template = PlantillaDeBotonDeChip(),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 3, 0),
                Background = _superficieAlta,
                Foreground = _texto,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                MinWidth = 34, Height = 22,
                IsEnabled = no == null,
                Opacity = no == null ? 1.0 : 0.45,
                ToolTip = no ?? $"Añadir «{f.Nombre}» a {zona}",
            };
            AutomationProperties.SetName(b, no == null
                ? $"Añadir {f.Nombre} a {zona}"
                : $"Añadir {f.Nombre} a {zona} (no disponible: {no})");
            var ficha = f;
            b.Click += (_, _) =>
            {
                var d = RegistroWidgets.Buscar(ficha.Id)?.Descriptor;
                if (d == null) return;
                Zonas.Anadir(d, suya);
                Guardado();
            };
            fila.Children.Add(b);
        }

        // ---------- Y se puede arrastrar al sitio exacto ----------
        //
        // Los tres botones de zona ponen al final, que está bien para la mayoría y mal para
        // lo que se repite: pulsar tres veces «Cen» apila tres separadores al final de la
        // columna en vez de ponerlos donde uno los quiere, que es entre dos grupos. Esto es
        // la otra forma: lo coges del catálogo y lo sueltas entre los dos chips donde va,
        // con la misma línea de destino que al mover.
        var marco = new Border
        {
            Background = _superficie,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(2),
            Cursor = no == null ? Cursors.Hand : Cursors.Arrow,
            Child = fila,
            // La descripción del catálogo y, debajo, lo que el propio módulo avisa de sí
            // mismo —«sin batería no se dibuja»—. Es el dato que faltaba para que «lo he
            // añadido y no aparece» no significara dos cosas distintas.
            ToolTip = (RegistroWidgets.Buscar(f.Id)?.Descriptor.Descripcion ?? f.Nombre) +
                      (no == null ? "\nArrástralo a una columna para ponerlo en un sitio concreto."
                                  : "\n" + no) +
                      (f.PuedeNoVerse == null ? "" : "\nAviso: " + f.PuedeNoVerse),
        };

        if (no == null)
        {
            // Con umbral, como los chips puestos: `DoDragDrop` nada más pulsar convierte
            // cualquier clic torpe en un arrastre.
            Point? pulsado = null;
            var suyo = f.Id;
            marco.PreviewMouseLeftButtonDown += (_, e) => pulsado = e.GetPosition(marco);
            marco.PreviewMouseLeftButtonUp += (_, _) => pulsado = null;
            marco.MouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || pulsado == null) return;
                var ahora = e.GetPosition(marco);
                if (Math.Abs(ahora.X - pulsado.Value.X) < 6 && Math.Abs(ahora.Y - pulsado.Value.Y) < 6) return;
                pulsado = null;
                try
                {
                    DragDrop.DoDragDrop(marco, new DataObject(FormatoNuevo, suyo),
                                        DragDropEffects.Copy);
                }
                finally { BorrarDestino(); }
            };
        }

        return marco;
    }

    // ================= Los botones de un chip =================
    Button BotonDeAccion(EditorModulos.Accion a, EditorModulos.Ficha f, int cuantosEnLaZona)
    {
        // «Bajar» es la única cuyo tope no cabe en la ficha: hace falta saber cuántos hay
        // en la zona, y eso lo sabe quien tiene la lista delante.
        string? no = a == EditorModulos.Accion.Bajar && f.Posicion >= cuantosEnLaZona - 1
                   ? "Ya es el último de su zona"
                   : EditorModulos.PorQueNo(a, f);

        var b = BotonChico(EditorModulos.Glifo(a), no ?? EditorModulos.Nombre(a),
                           () => HacerConElModulo(a, f));
        b.IsEnabled = no == null;
        // 0,45 y no 0,3: lo que no se puede pulsar tiene que seguir LEYÉNDOSE, o no hay
        // forma de saber qué acción es la que está apagada ni por qué pasar el ratón por
        // encima. Apagado no es invisible.
        b.Opacity = no == null ? 1.0 : 0.45;
        AutomationProperties.SetName(b, no == null
            ? $"{EditorModulos.Nombre(a)}: {f.Nombre}"
            : $"{EditorModulos.Nombre(a)}: {f.Nombre} (no disponible: {no})");
        AutomationProperties.SetHelpText(b, no ?? "");
        return b;
    }

    // ---------- La plantilla de los botones del chip ----------
    //
    // Sin ella, un botón deshabilitado se dibujaba con el cromo de serie de WPF: un
    // rectángulo gris claro RELLENO. En la captura se veía perfectamente el disparate —las
    // flechas que no se pueden pulsar pesaban MÁS que las que sí, porque un bloque gris
    // llama más que un galón fino—. La jerarquía al revés.
    //
    // Con plantilla propia, deshabilitado es lo que tiene que ser: el mismo icono, más
    // apagado. La diferencia la lleva el color, no un bloque.
    ControlTemplate? _plantillaChip;

    ControlTemplate PlantillaDeBotonDeChip()
    {
        if (_plantillaChip != null) return _plantillaChip;

        var marco = new FrameworkElementFactory(typeof(Border));
        marco.Name = "marco";
        marco.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var dentro = new FrameworkElementFactory(typeof(ContentPresenter));
        dentro.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        dentro.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        marco.AppendChild(dentro);

        var p = new ControlTemplate(typeof(Button)) { VisualTree = marco };

        // Solo el fondo al pasar por encima. Nada en «deshabilitado»: de eso se encarga la
        // opacidad que pone `BotonDeAccion`, que además es la que se ve bien en los siete
        // temas sin escribir un gris a mano.
        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BackgroundProperty, _superficieAlta, "marco"));
        p.Triggers.Add(encima);

        return _plantillaChip = p;
    }

    Button BotonChico(string glifo, string ayuda, Action alPulsar)
    {
        var b = new Button
        {
            Content = Pinta(glifo, 10, _texto),
            Template = PlantillaDeBotonDeChip(),
            Width = 22, Height = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(1, 0, 1, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = ayuda,
        };
        b.Click += (_, e) => { e.Handled = true; alPulsar(); };
        return b;
    }

    void HacerConElModulo(EditorModulos.Accion a, EditorModulos.Ficha f)
    {
        var z = Zonas;
        switch (a)
        {
            case EditorModulos.Accion.Quitar: z.Quitar(f.Zona, f.Posicion); break;
            case EditorModulos.Accion.Ocultar: z.Ocultar(f.Id); break;
            case EditorModulos.Accion.Mostrar: z.Mostrar(f.Id); break;
            case EditorModulos.Accion.Subir: z.Mover(f.Zona, f.Posicion, f.Zona, f.Posicion - 1); break;
            case EditorModulos.Accion.Bajar: z.Mover(f.Zona, f.Posicion, f.Zona, f.Posicion + 1); break;
            case EditorModulos.Accion.ALaIzquierda:
            case EditorModulos.Accion.ALaDerecha:
                var destino = EditorModulos.ZonaVecina(f.Zona, a == EditorModulos.Accion.ALaDerecha);
                if (destino != null) z.Mover(f.Zona, f.Posicion, destino, z.De(destino).Count);
                break;
        }
        Guardado();
    }

    // ================= Soltar =================
    void Soltar(string zona, StackPanel dentro, DragEventArgs e)
    {
        BorrarDestino();
        e.Handled = true;
        var z = Zonas;

        // El destino se decide ANTES de tocar nada. El `Drop` de antes quitaba del origen y
        // después buscaba dónde meterlo, así que un arrastre que acababa fuera —muy fácil
        // en una ventana pequeña— se comía el módulo.
        var suelta = EditorModulos.AlSoltar(zona, e.GetPosition(dentro).Y, AltoChip,
                                            z.De(zona).Count);
        if (!suelta.Vale) { Log.Info("ajustes: " + suelta.Porque); return; }

        // ---------- Uno nuevo, del catálogo ----------
        //
        // Se añade y se lleva al sitio donde lo has soltado. Añadir y mover, y no un
        // «insertar» propio, porque las dos operaciones ya existen, están comprobadas y son
        // las mismas que usa el modo edición de la isla: una tercera forma de meter un
        // módulo en una lista sería la que un día discrepe de las otras dos.
        if (e.Data.GetData(FormatoNuevo) is string nuevo)
        {
            var d = RegistroWidgets.Buscar(nuevo)?.Descriptor;
            if (d == null) return;
            if (!z.Anadir(d, suelta.Zona)) return;        // no repetible y ya puesto
            int ultimo = z.De(suelta.Zona).Count - 1;
            if (ultimo != suelta.Donde) z.Mover(suelta.Zona, ultimo, suelta.Zona, suelta.Donde);
            Guardado();
            return;
        }

        // ---------- O uno que ya estaba ----------
        //
        // El payload lleva «zona|índice», la instancia exacta. Con solo el id, arrastrar el
        // tercer separador movía el primero.
        if (e.Data.GetData(FormatoChip) is not string donde) return;
        var partes = donde.Split('|');
        if (partes.Length != 2 || !int.TryParse(partes[1], out int desde)) return;
        string origen = partes[0];
        if (desde < 0 || desde >= z.De(origen).Count) return;

        z.Mover(origen, desde, suelta.Zona, suelta.Donde);
        Guardado();
    }

    // ================= La vista previa del destino =================
    void PintarDestino(Border caja, StackPanel dentro, Point donde)
    {
        if (_marcaDestino == null || _rejillaZonas == null) return;
        _marcaDestino.Children.Clear();

        int i = EditorModulos.DondeCae(donde.Y, AltoChip, dentro.Children.Count);
        double y = i * AltoChip;

        try
        {
            var esquina = dentro.TranslatePoint(new Point(0, 0), _marcaDestino);
            var linea = new Border
            {
                Width = Math.Max(20, caja.ActualWidth - 16),
                Height = 3,
                CornerRadius = new CornerRadius(2),
                Background = _acentoLegible,
            };
            Canvas.SetLeft(linea, esquina.X);
            Canvas.SetTop(linea, esquina.Y + y - 1);
            _marcaDestino.Children.Add(linea);
        }
        catch { /* si el elemento no está en el árbol todavía, no se dibuja marca y ya */ }
    }

    void BorrarDestino() => _marcaDestino?.Children.Clear();
}
