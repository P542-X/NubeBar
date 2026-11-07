using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NubeBar;

// E3: el contenido del panel izquierdo — lo que CONTROLAS.
//
// ---------- El criterio para repartir ----------
//
// Izquierda, lo que tú cambias: volumen, brillo, conmutadores, accesos, ajustes. Derecha, lo
// que pasa: música, avisos, calendario. Un reparto por «cosas que tocas» y «cosas que miras»
// es el que hace que sepas a cuál ir sin pensarlo.
//
// ---------- Ni una tarjeta de más ----------
//
// El documento de E lo dice y la referencia lo enseña: cuatro conmutadores y dos
// deslizadores, no veinte. Un panel lleno de tarjetas es un panel que nadie lee.
public partial class MainWindow
{
    // Un rótulo de panel: a la izquierda, con su tamaño, y no centrado como los de la barra.
    TextBlock Linea(string t, double tam = 12, double opacidad = 1)
    {
        var x = Texto(t, tam);
        x.HorizontalAlignment = HorizontalAlignment.Left;
        x.TextWrapping = TextWrapping.Wrap;
        x.Opacity = opacidad;
        return x;
    }

    // ---------- Una fila pulsable, alineada a la izquierda ----------
    //
    // La plantilla de `EstiloBoton` centra su contenido, que es lo correcto para un icono de
    // la barra y lo incorrecto para una lista de accesos: «Escritorio», «Documentos» y
    // «Descargas» salían centrados, cada uno en una posición distinta según su longitud, y
    // una lista así no se lee, se descifra.
    //
    // `HorizontalContentAlignment` no sirve: la plantilla no lo mira. Lo que sí funciona es
    // darle al contenido el ancho de la fila, para que no haya hueco que centrar.
    // `icono` es el glifo de la izquierda y `remate` lo que va a la derecha —una flecha, un
    // valor—. Los dos opcionales, pero la fila **sin** icono era justo el problema: seis filas
    // de texto plano en columna («Escritorio», «Documentos», «Descargas»…) no se leen, se
    // descifran. Con icono el ojo las distingue sin leerlas.
    // `remate` es un GLIFO, no texto: la primera versión pasaba el carácter de la flecha
    // dentro de una cadena normal y salía como una barra vertical, porque esos caracteres
    // viven en el área privada de la fuente de iconos y `Linea()` usa la fuente de texto.
    // Es el mismo error que ya costó un botón de apagado donde tenía que haber un escritorio.
    FrameworkElement FilaPulsable(string texto, string tip, Action accion,
                                  int icono = 0, int remate = 0)
    {
        // El ancho sale de `AnchoDeFilaEnTarjeta` y no de un «- 56» escrito a mano. Ese 56
        // daba por hecho el relleno del estilo «Normal» y se olvidaba del margen que
        // `EstiloBoton` le pone a todos los botones, así que la flecha de la derecha salía
        // mordida — poco, porque es fina, pero mordida. Con «Nube» o «Neón», más.
        var caja = new Grid { Width = AnchoDeFilaEnTarjeta() };
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (icono != 0)
        {
            var ic = Glifo(char.ConvertFromUtf32(icono));
            ic.FontSize = 13;
            ic.Opacity = 0.8;
            ic.Width = 18;
            ic.TextAlignment = TextAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(ic, 0);
            caja.Children.Add(ic);
        }

        var t = Linea(texto, 12);
        t.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(t, 1);
        caja.Children.Add(t);

        // El remate: una flecha que dice «esto abre algo fuera». Sin él, una fila que lanza el
        // explorador y otra que solo informa se ven exactamente igual.
        if (remate != 0)
        {
            var r = Glifo(char.ConvertFromUtf32(remate));
            r.FontSize = 12;
            r.Opacity = 0.6;
            r.VerticalAlignment = VerticalAlignment.Center;
            r.Margin = new Thickness(8, 0, 2, 0);
            Grid.SetColumn(r, 2);
            caja.Children.Add(r);
        }

        var b = Boton(caja, tip, accion, levantar: false);
        b.Padding = new Thickness(0);
        b.Height = 32;
        return b;
    }

    FrameworkElement? CuerpoDeSeccion(Lateral.Seccion s, Lateral.Lado l) => s.Id switch
    {
        "perfil" => SeccionPerfil(),
        "rapidas" => SeccionRapidas(),
        "audio" => SeccionAudioYBrillo(l),
        "carpetas" => SeccionCarpetas(),
        "maquina" => SeccionMaquina(),
        "ajustes" => SeccionAjustes(),
        _ when l == Lateral.Lado.Derecha => CuerpoDeSeccionDerecha(s),
        _ => null,
    };

    // ================= Perfil y nube =================
    FrameworkElement SeccionPerfil()
    {
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ---------- La nube, de protagonista ----------
        //
        // La maqueta la pone grande y la primera: es la identidad del escritorio, no un icono
        // de estado más. Antes medía 64 y compartía sitio con el nombre de usuario; ahora
        // manda ella y el texto la acompaña.
        //
        // Sigue siendo la MISMA pieza que el módulo de la barra (`Nubecita.Molde`), con el
        // mismo color de estado: si fueran dos dibujos, un día dirían cosas distintas.
        // Medida fija y no `TamanoDeLaNube`: esa función contesta «¿cuánto cabe dentro de una
        // barra de este grosor?», que es otra pregunta — devuelve una fracción del alto y
        // aquí daba 32 px. En una tarjeta de identidad el tamaño es una decisión, no un
        // resultado.
        // 72 y no 54: el usuario pidió que «la nube sea la identidad visual principal», y a
        // 54 compartía peso con el nombre en vez de mandar. Setenta y dos es lo que cabe sin
        // empujar la tarjeta: el bloque de texto de al lado mide ~66 de alto.
        const double lado = 72;
        var nube = new System.Windows.Shapes.Path
        {
            Data = Nubecita.Molde(),
            Stretch = Stretch.Uniform,
            Width = lado, Height = lado,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // ---------- Y con halo ----------
        //
        // Un resplandor del color de la casa detrás de la nube. No es el adorno del estilo
        // «Neón» —este va en los tres—, es lo que separa «un icono grande» de «la marca»: la
        // nube deja de estar apoyada en el fondo y pasa a estar encendida.
        //
        // Suave a propósito (18 de radio, 0,35 de opacidad). Un halo que se nota es un efecto;
        // uno que no se nota es profundidad, y eso es lo que se buscaba.
        nube.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = HyprPinta.Azul(_roles),
            BlurRadius = 18,
            ShadowDepth = 0,
            Opacity = 0.35,
        };

        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        bool seguro = HyprSalud.Seguro;

        // ---------- Aquí la nube es IDENTIDAD, no semáforo ----------
        //
        // El módulo de la barra pinta esta misma nube del color del estado, y ahí está bien:
        // es un indicador. En la tarjeta de identidad no, y se veía: con NubeHypr apagado
        // —que es como viene de serie— salía gris y al 55 % de opacidad, o sea que lo primero
        // que ves al abrir el panel es la marca del escritorio apagada.
        //
        // La identidad va siempre del color de la casa. El estado lo lleva la insignia de
        // debajo, que es su sitio: una cosa, un trabajo.
        nube.Fill = new SolidColorBrush(HyprPinta.Azul(_roles));

        // ---------- Y su accesorio, si el tema se lo pone ----------
        //
        // Samurái le ata el hachimaki, Invierno le pone el gorro, Videojuegos los auriculares.
        // Va encima de la nube y en la misma celda, así que lo que se mueve o se tiñe sigue
        // siendo la nube: el accesorio no es otra pieza que haya que mantener a su lado, es
        // una capa suya (ver `Accesorios`).
        if (Accesorios.Para(_roles.Pintura, lado) is { } puesto)
        {
            var conGorro = new Grid { Margin = nube.Margin, VerticalAlignment = VerticalAlignment.Center };
            nube.Margin = new Thickness(0);
            conGorro.Children.Add(nube);
            conGorro.Children.Add(puesto);
            fila.Children.Add(conGorro);
        }
        else fila.Children.Add(nube);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        // «Nube» y debajo quién eres. La maqueta pone «Nube / Tu entorno, en calma»; aquí la
        // segunda línea es tu nombre, que es un dato de verdad y no una frase inventada.
        var marca = Linea("Nube", 22);
        marca.FontWeight = FontWeights.SemiBold;
        textos.Children.Add(marca);

        // ---------- «Tu entorno, en calma», no el nombre de usuario ----------
        //
        // Aquí ponía `Environment.UserName`, y el motivo escrito era bueno: un dato de verdad
        // antes que una frase inventada. Pero la referencia pone la frase, el usuario mandó
        // la imagen diciendo «aplica el diseño de la imagen», y esto no es un dato falso: es
        // el lema del programa, no un número de CPU que me esté sacando de la manga.
        //
        // El nombre no se pierde: se va al tooltip de la tarjeta, que es donde se mira
        // cuando hace falta y no ocupa la línea que la referencia quiere para el lema.
        var lema = Linea("Tu entorno, en calma", 12, 0.6);
        lema.Margin = new Thickness(0, -1, 0, 0);
        textos.Children.Add(lema);

        // ---------- El estado, como insignia y no como texto gris ----------
        //
        // Antes era una línea de texto al 70 % de opacidad debajo del nombre, que es la forma
        // más barata de decir algo y la que menos se ve. El estado de NubeHypr es lo que
        // contesta «¿esto está tocando mis ventanas ahora mismo?», y eso merece color: la
        // misma insignia que usan los paneles de la isla.
        //
        // Y el color dice lo que pasa, no decora: coral cuando mueve ventanas de verdad o
        // está en modo seguro —las dos cosas que conviene mirar—, neutro cuando está apagado
        // o solo calculando.
        string estado = seguro ? "Modo seguro"
                      : modo switch
                      {
                          NubeHypr.Modo.Apagado => "Apagado",
                          NubeHypr.Modo.Aplicar => "Moviendo ventanas",
                          _ => "En simulación",
                      };
        bool ojo = seguro || modo == NubeHypr.Modo.Aplicar;

        var insignia = StatusBadge(estado, bien: !ojo, neutro: !ojo && modo == NubeHypr.Modo.Apagado);
        if (insignia is FrameworkElement fi)
        {
            fi.HorizontalAlignment = HorizontalAlignment.Left;
            fi.Margin = new Thickness(0, 5, 0, 0);
        }
        textos.Children.Add(insignia);

        Grid.SetColumn(textos, 1);
        fila.Children.Add(textos);
        return fila;
    }

    // ================= Acciones rápidas =================
    //
    // En rejilla de dos, como la referencia. Y solo las que NubeBar sabe hacer de verdad: un
    // conmutador que no hace nada se pulsa y se cree que ha funcionado.
    FrameworkElement SeccionRapidas()
    {
        var rejilla = new UniformGrid { Columns = 2 };

        // ---------- Aquí ya no se calcula ningún ancho ----------
        //
        // Hubo cuatro versiones de una cuenta —ancho del panel, menos márgenes del cuerpo,
        // menos relleno de la tarjeta, menos filo, menos el margen que el estilo del botón
        // añade— y las cuatro fallaron. La última por algo que desde aquí no se puede saber:
        // la tarjeta mide 308 px y no 312 porque la barra de desplazamiento del panel se
        // lleva cuatro. El usuario lo vio: «se corta por la derecha un poco».
        //
        // El error no era el número, era la idea. Un elemento no tiene que adivinar el sitio
        // que le queda: tiene que ocuparlo. Con `PlantillaDeAzulejo` el botón llena su celda
        // del `UniformGrid` y la pastilla llena el botón, así que la rejilla sale cuadrada
        // sola — con barra de desplazamiento o sin ella, con cualquier estilo, y con el ancho
        // del panel que sea.
        //
        // Y por lo mismo desaparece el bloque que medía las cuatro para igualarles el alto:
        // las celdas de un `UniformGrid` ya son iguales, y ahora el contenido las llena.
        const double hueco = 10;

        int i = 0;
        foreach (var a in Lateral.Rapidas)
        {
            bool izquierda = i % 2 == 0;
            i++;
            bool encendida = EstaEncendida(a.Que);
            var caja = new Border
            {
                Background = new SolidColorBrush(encendida
                    ? HyprPinta.Azul(_roles) with { A = 0x33 }
                    : _roles.Superficie),
                BorderThickness = new Thickness(encendida ? 1 : 0),
                BorderBrush = new SolidColorBrush(HyprPinta.Azul(_roles)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 9),
                // ---------- Sin margen AQUÍ ----------
                //
                // La separación la pone el BOTÓN, no la pastilla. Con el margen dentro del
                // botón, el resaltado del ratón —que `EstiloBoton` pinta sobre el botón
                // entero— salía más ancho que la pastilla y asomaba por su lado. El usuario
                // lo vio al pasar por encima de «Tema claro», y es el mismo fallo que las
                // pastillas de escritorio.
                Margin = new Thickness(0),
            };

            // ---------- Icono a la izquierda, texto a la derecha ----------
            //
            // Cuatro cuadros con dos líneas de texto cada uno son ocho líneas que hay que
            // leer para saber qué es cada cosa. Con icono se sabe antes de leer ninguna, que
            // es para lo que está una rejilla de accesos rápidos.
            var dentro = new Grid();
            dentro.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dentro.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var ic = Glifo(char.ConvertFromUtf32(a.Icono));
            ic.FontSize = 15;
            ic.Foreground = encendida ? _acentoLegible : _texto;
            ic.Opacity = encendida ? 1 : 0.75;
            ic.VerticalAlignment = VerticalAlignment.Center;

            // ---------- Ancho FIJO, para que los iconos hagan columna ----------
            //
            // La columna del icono es `Auto`, así que se ajustaba al glifo: una campana ancha
            // y un sol estrecho no miden lo mismo, y el texto de cada pastilla empezaba en un
            // sitio distinto. En la captura se ve que «Tema claro» arranca ~5 px antes que
            // «No molestar», y eso es lo que hace que una rejilla de cuatro cuadros iguales
            // siga pareciendo torcida aunque los cuadros midan exactamente lo mismo.
            //
            // Es la misma regla que `FilaDeDato` ya aplicaba con su `ic.Width = 22`, y que
            // aquí faltaba.
            ic.Width = 20;
            ic.TextAlignment = TextAlignment.Center;
            ic.Margin = new Thickness(0, 0, 9, 0);
            Grid.SetColumn(ic, 0);
            dentro.Children.Add(ic);

            // ---------- El título se PARTE, no se corta ----------
            //
            // Con `NoWrap` salía «Wallpaper Engi…»: 137 px de pastilla menos el icono y el
            // relleno dejan unos 95 para el texto, y «Wallpaper Engine» mide más. Cortar el
            // nombre de un programa es peor que gastar una línea más.
            //
            // Aquí ponía que al partirse «no descuadra nada, porque el `UniformGrid` da a
            // todas las celdas el alto de la más alta». Eso es cierto de las CELDAS y falso de
            // lo que se ve: la plantilla del botón centra su contenido, así que cada pastilla
            // se quedaba del alto de su propio texto, flotando dentro de una celda más grande.
            // En la captura salían las cuatro con alturas distintas.
            //
            // Es la misma lección del ancho que está doce líneas más abajo, y la volví a
            // tropezar por el mismo motivo: escrita por lógica y no comprobada mirando. Se
            // cuadra al final del bucle — ver «las cuatro, del mismo alto».
            var pila = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t = Linea(a.Titulo, 11.5);
            t.FontWeight = FontWeights.SemiBold;
            t.TextWrapping = TextWrapping.Wrap;
            pila.Children.Add(t);
            var est = Linea(Lateral.Estado(a, encendida), 10, 0.65);
            est.TextWrapping = TextWrapping.NoWrap;
            est.TextTrimming = TextTrimming.CharacterEllipsis;
            pila.Children.Add(est);
            Grid.SetColumn(pila, 1);
            dentro.Children.Add(pila);

            caja.Child = dentro;

            var b = Boton(caja, Lateral.Ayuda(a, encendida),
                          () => Alternar(a.Que), levantar: false);

            // ---------- La plantilla que ESTIRA ----------
            //
            // Sustituye a la de `EstiloBoton`, que centra su contenido y obligaba a darle a
            // cada pastilla un ancho calculado a mano. Ver `PlantillaDeAzulejo`: con ella el
            // botón llena su celda, la pastilla llena el botón, y no hay número que ajustar.
            b.Template = PlantillaDeAzulejo(10);
            b.Padding = new Thickness(0);
            b.Height = double.NaN;
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.VerticalAlignment = VerticalAlignment.Stretch;

            // La separación, en el BOTÓN y no en la pastilla. Media a cada lado de los que se
            // miran y nada por fuera: así la rejilla llega al filo de la tarjeta por los dos
            // lados por igual, y el resaltado del ratón —que se pinta sobre el botón— cae
            // exactamente encima de la pastilla en vez de asomar por un lado.
            b.Margin = izquierda
                ? new Thickness(0, 0, hueco / 2.0, hueco)
                : new Thickness(hueco / 2.0, 0, 0, hueco);
            System.Windows.Automation.AutomationProperties.SetName(b, Lateral.Ayuda(a, encendida));
            rejilla.Children.Add(b);
        }

        // ---------- Y aquí ya no hace falta igualar los altos ----------
        //
        // Antes había un bloque que medía las cuatro pastillas y le daba a todas la altura de
        // la más alta, porque la plantilla de serie las dejaba del alto de su propio texto.
        // Con `PlantillaDeAzulejo` el contenido se estira y las celdas de un `UniformGrid` ya
        // son iguales, así que la altura sale igualada sola.
        //
        // Es la segunda cuenta a mano que desaparece por la misma razón: medir el sitio que te
        // queda es peor que ocuparlo.
        return rejilla;
    }

    bool EstaEncendida(Lateral.Rapida q)
    {
        try
        {
            return q switch
            {
                Lateral.Rapida.NoMolestar => Centro.Actual.NoMolestar,
                Lateral.Rapida.Wallpaper => _cfg.Wallpaper.Integrar,
                Lateral.Rapida.Tema => Contraste.Luminancia(_roles.FondoOpaco) > 0.5,
                _ => false,
            };
        }
        catch { return false; }
    }

    void Alternar(Lateral.Rapida q)
    {
        try
        {
            switch (q)
            {
                case Lateral.Rapida.NoMolestar:
                    Centro.Actual.NoMolestar = !Centro.Actual.NoMolestar;
                    break;

                // El modo nocturno sigue sin tener función detrás, así que abre el sitio donde
                // SÍ se cambia en vez de fingir que hace algo. Un conmutador que no conmuta es
                // peor que un enlace honesto.
                case Lateral.Rapida.ModoNocturno:
                    AbrirAjustes("ms-settings:nightlight");
                    break;

                case Lateral.Rapida.Tema:
                    CambiarEntreClaroYOscuro();
                    break;

                case Lateral.Rapida.Wallpaper:
                    AbrirAjustes();
                    break;
            }
            Rellenar(Lateral.Lado.Izquierda);
            ColocarLateral(Lateral.Lado.Izquierda);
        }
        catch (Exception ex) { Log.Error($"la acción rápida {q}", ex); }
    }

    // ================= Claro / oscuro, de verdad =================
    //
    // ---------- Lo que hacía antes ----------
    //
    // Abrir Ajustes. Y el motivo escrito era defendible —no había tema claro que aplicar, y
    // más vale un enlace honesto que un interruptor de mentira— pero la pastilla TIENE pinta
    // de interruptor, así que prometía algo que no hacía. El usuario lo dijo en cuanto lo
    // probó: «si pongo tema oscuro se abre ajustes en vez de aplicarlo».
    //
    // ---------- Y por qué no se pierde tu tema ----------
    //
    // Al irte al claro, el tuyo se guarda ENTERO en un fichero antes de tocarlo, y al volver
    // se restaura desde ahí. Si tenías «Gruvbox» con tus retoques, vuelves a «Gruvbox» con tus
    // retoques — no al tema de la casa.
    //
    // Sin eso, esto sería un conmutador que te cuesta tu configuración de colores cada vez que
    // lo pulsas por curiosidad, y eso es peor que no tenerlo.
    void CambiarEntreClaroYOscuro()
    {
        string guardado = System.IO.Path.Combine(Temas.Carpeta, "_antes-del-claro.json");
        bool estoyEnClaro = Contraste.Luminancia(_roles.FondoOpaco) > 0.5;

        TemaExportado destino;
        if (estoyEnClaro)
        {
            // Volver: lo que hubiera guardado, y si no hay nada —porque entraste en claro por
            // otro camino— el tema de serie, que es oscuro.
            destino = Temas.Importar(guardado)
                      ?? new TemaExportado("el de serie", TemasDeSerie.Predeterminado, null);
        }
        else
        {
            try
            {
                System.IO.Directory.CreateDirectory(Temas.Carpeta);
                Temas.Exportar(TemaDeAhora("_antes-del-claro"), guardado);
            }
            catch (Exception ex)
            {
                // Si no se puede guardar, NO se cambia: entrar en claro sin poder volver es
                // justo el trato que esta función existe para no hacer.
                Log.Error("guardar el tema antes de pasar a claro", ex);
                AbrirAjustesEn("colores");
                return;
            }
            // Oficina es el claro sobrio de los nueve: blanco, grises y azul, sin mascota ni
            // partículas. Un «tema claro» no puede sorprenderte con pétalos cayendo.
            destino = new TemaExportado("Oficina", "oficina", null);
        }

        AplicarYGuardarTema(destino);
    }

    // El tema que hay puesto AHORA. Lee del config y no de los roles: los roles ya están
    // derivados y aplanados, así que reconstruir desde ellos devolvería un tema parecido pero
    // no el tuyo. Son dos cosas: de qué tema sales y qué le has cambiado.
    TemaExportado TemaDeAhora(string nombre) =>
        new(nombre, _cfg.Tema.Id, new Dictionary<string, string>(_cfg.Tema.Colores));

    // Se escribe con `ConfigArbol` por lo de siempre: para no perder las claves que NubeBar no
    // conoce del fichero del usuario.
    //
    // Y los colores del usuario se SUSTITUYEN enteros, no se mezclan. Si se conservaran, al
    // pasarte a claro te quedaría tu fondo oscuro de siempre puesto encima del tema claro —o
    // sea, ni uno ni otro—. Al volver se restauran desde el fichero, que es para lo que se
    // guardó. Es la misma regla que cumplía la versión anterior escribiendo los roles vacíos.
    void AplicarYGuardarTema(TemaExportado t)
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            arbol.Poner("Tema", "Id", TemasDeSerie.De(t.Id).Id);
            arbol.PonerColores("Tema", t.Colores ?? new Dictionary<string, string>());
            arbol.Guardar();
            Log.Info($"tema cambiado a «{t.Nombre}» ({t.Id}) desde la acción rápida");
            // Lo demás lo hace la recarga en caliente: el vigilante del config ve la
            // escritura y repinta las islas. No hace falta repintar a mano, y hacerlo
            // dejaría la barra con un tema y los paneles con otro durante un instante.
        }
        catch (Exception ex) { Log.Error("aplicar el tema desde la acción rápida", ex); }
    }

    // ================= Audio y brillo, en una tarjeta =================
    //
    // Antes eran dos secciones: dos cabeceras, dos marcos y dos flechas para dos controles de
    // una línea cada uno. El panel se iba en cromo, y la maqueta los pone juntos con razón:
    // son lo mismo —lo que ajustas cuando ajustas el otro—.
    //
    // Devuelve `null` si no hay ninguno de los dos: ni audio ni un monitor que responda a
    // DDC/CI. Una tarjeta vacía con su título es peor que no estar.
    FrameworkElement? SeccionAudioYBrillo(Lateral.Lado l)
    {
        var pila = new StackPanel();

        var vol = SeccionVolumen(l);
        if (vol != null) pila.Children.Add(vol);

        var bri = SeccionBrillo(l);
        if (bri != null)
        {
            bri.Margin = new Thickness(0, vol != null ? 10 : 0, 0, 0);
            pila.Children.Add(bri);
        }

        return pila.Children.Count == 0 ? null : pila;
    }

    // ================= Volumen y brillo =================
    //
    // Con la máquina de deslizadores de siempre (`Slider`), que es la que sabe no quedarse
    // agarrada. El mando se guarda para poder soltarlo si el panel se cierra con el ratón
    // pulsado.
    FrameworkElement? SeccionVolumen(Lateral.Lado l)
    {
        int v = Audio.Volumen();
        if (v < 0) return null;         // sin dispositivo de audio no se pinta la sección

        var mando = Slider(Audio.Silenciado ? 0 : v, 0, 100,
                           alArrastrar: x => Audio.PonerVolumen((int)Math.Round(x)),
                           alSoltar: x => Audio.PonerVolumen((int)Math.Round(x)),
                           nombre: "Volumen");
        _mandosDeLateral[l].Add(mando);
        // El nombre va dentro del propio `Slider` desde que el envoltorio de las flechas
        // es un botón: ponerlo aquí fuera nombraba el visual y dejaba mudo el botón, que
        // es lo que recorre el tabulador. Se deja también aquí porque no estorba y porque
        // la pregunta «¿quién nombra esto?» se contesta mejor viéndolo en los dos sitios.
        System.Windows.Automation.AutomationProperties.SetName(mando.Visual, "Volumen");
        return ConCifra(mando.Visual, () => Audio.Silenciado ? "silencio" : $"{Audio.Volumen()} %",
                        Audio.Silenciado ? 0xE74F : 0xE767);   // mudo / altavoz
    }

    FrameworkElement? SeccionBrillo(Lateral.Lado l)
    {
        // Un monitor con DDC/CI apagado de fábrica —lo normal en DisplayPort— no responde, y
        // entonces la sección no se pinta en vez de ofrecer un deslizador que no hace nada.
        List<Brillo.Pantalla> pantallas;
        try
        {
            if (!Brillo.Disponible) return null;
            pantallas = Brillo.Pantallas();
        }
        catch { return null; }

        if (pantallas.Count == 0) return null;
        var p = pantallas[0];
        int ahora = p.PorCiento;

        var mando = Slider(ahora, 0, 100,
                           // Al arrastrar NO se escribe en el monitor: cada escritura por el
                           // cable son 50 ms largos, y mandarlas sesenta veces por segundo
                           // deja el deslizador a trompicones. Se escribe al soltar.
                           alArrastrar: _ => { },
                           alSoltar: x =>
                           {
                               int pc = (int)Math.Round(x);
                               foreach (var q in pantallas) Brillo.Poner(q, pc);
                               AvisoDeBrillo(pc);
                           },
                           nombre: "Brillo");
        _mandosDeLateral[l].Add(mando);

        // Los handles del driver hay que devolverlos: no hacerlo los fuga, y ya pasó una vez
        // al abrir el centro de control. Se sueltan al cerrar el panel.
        _aSoltarDelLateral[l] = () => { try { Brillo.Soltar(pantallas); } catch { } };

        System.Windows.Automation.AutomationProperties.SetName(mando.Visual, "Brillo");
        return ConCifra(mando.Visual, () => $"{ahora} %", 0xE706,   // Brightness
                        nota: "Se aplica al soltar: el monitor no admite una escritura por fotograma.");
    }

    // Lo que hay que devolver al sistema cuando el panel se cierra.
    readonly Dictionary<Lateral.Lado, Action> _aSoltarDelLateral = new();

    // Un deslizador con su icono a la izquierda y la cifra a la derecha.
    //
    // ---------- Por qué el icono, y por qué la nota va arriba ----------
    //
    // Sin icono, un deslizador suelto dentro de una tarjeta no dice de qué es: hay que leer
    // la cabecera y bajar la vista. Con el altavoz o el sol delante se sabe sin leer.
    //
    // Y la cifra iba en una columna fija de 62 px **al lado** del deslizador. Eso es lo que
    // hacía que «se aplica al soltar», del brillo, partiera en dos líneas y empujara la barra
    // hacia arriba: una frase metida en el hueco de un porcentaje. Por eso el texto largo va
    // a lo ancho y no en esa columna — y eso sigue igual.
    //
    // ---------- Pero debajo, no encima ----------
    //
    // La nota se ponía ENCIMA de su deslizador, y en la captura se ve lo que eso provoca: la
    // tarjeta junta volumen y brillo, así que «Se aplica al soltar…» quedaba justo debajo de
    // la barra del volumen y encima de la del brillo. Se lee como una advertencia sobre el
    // volumen, que es de lo único que no habla.
    //
    // Debajo de su propio control no hay ambigüedad posible, y se conserva entero el motivo
    // por el que se sacó de la columna: sigue siendo una línea a lo ancho.
    FrameworkElement ConCifra(FrameworkElement control, Func<string> cifra, int icono = 0,
                              string? nota = null)
    {
        var col = new StackPanel();

        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (icono != 0)
        {
            var ic = Glifo(char.ConvertFromUtf32(icono));
            ic.FontSize = 14;
            ic.Opacity = 0.8;
            ic.Width = 20;
            ic.TextAlignment = TextAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(ic, 0);
            fila.Children.Add(ic);
        }

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        fila.Children.Add(control);

        var t = Linea(cifra(), 11.5, 0.8);
        t.FontWeight = FontWeights.SemiBold;
        t.MinWidth = 38;
        t.TextAlignment = TextAlignment.Right;
        t.VerticalAlignment = VerticalAlignment.Center;
        t.Margin = new Thickness(10, 0, 0, 0);
        _actualizadores.Add(() => { try { t.Text = cifra(); } catch { } });
        Grid.SetColumn(t, 2);
        fila.Children.Add(t);

        col.Children.Add(fila);

        if (nota is { Length: > 0 })
        {
            var n = Linea(nota, 10.5, 0.55);
            n.TextWrapping = TextWrapping.Wrap;
            n.Margin = new Thickness(0, 4, 0, 2);
            col.Children.Add(n);
        }

        return col;
    }

    // ================= Carpetas =================
    FrameworkElement SeccionCarpetas()
    {
        // Cada una con SU icono y SU color, no todas con el de carpeta en gris: lo que
        // distingue «Descargas» de «Imágenes» de un vistazo es el dibujo y el tono, no leer la
        // palabra. Y la flecha de la derecha dice que la fila abre algo fuera de NubeBar.
        //
        // El color es lo que esta tarjeta ganó en el rediseño. Antes eran cuatro glifos del
        // mismo gris en columna: una lista. El azulejo de color le da a cada fila un ancla por
        // la que entrar sin leer, que es la diferencia entre un centro de control y un menú.
        var pila = new StackPanel();
        foreach (var (nombre, carpeta, icono, tono) in new[]
        {
            ("Escritorio", Environment.SpecialFolder.DesktopDirectory, 0xE7F4, Tonos.Cian),
            ("Documentos", Environment.SpecialFolder.MyDocuments, 0xE8A5, Tonos.Verde),
            ("Descargas", Environment.SpecialFolder.UserProfile, 0xE896, Tonos.Ambar),
            ("Imágenes", Environment.SpecialFolder.MyPictures, 0xEB9F, Tonos.Lila),
        })
        {
            string ruta = Environment.GetFolderPath(carpeta);
            if (nombre == "Descargas") ruta = Path.Combine(ruta, "Downloads");
            if (!Directory.Exists(ruta)) continue;     // lo que no existe no se ofrece

            // Sin segunda línea: la ruta completa de «Imágenes» ocupa toda la fila para decir
            // lo que ya dice la palabra. Va al tooltip, que es su sitio.
            pila.Children.Add(FilaRica(icono, tono, nombre, null, ruta,
                                       () => Fuentes.Lanzar(ruta)));
        }
        return pila;
    }

    // ================= La máquina =================
    FrameworkElement SeccionMaquina()
    {
        var pila = new StackPanel();

        // ---------- CPU y memoria, con la misma tarjeta que el centro de control ----------
        //
        // Antes eran dos líneas de texto: «CPU   7 %» y «Memoria   80 %». Decían el dato y
        // nada más — ni si eso es mucho, ni si va subiendo.
        //
        // `MetricCard` es la pieza que ya usa el centro de control (`MainWindow.Control.cs`):
        // número grande **con color según el estado**, rótulo y una gráfica de los últimos
        // valores. Con ella, «80 %» deja de ser un número y pasa a ser una memoria que lleva
        // un rato llena. Y por ser la misma pieza, los dos sitios no pueden divergir.
        // ---------- Tres cuadros con anillo: CPU, memoria y disco ----------
        //
        // Lo que pide la maqueta. Antes eran dos líneas de texto —«CPU 7 %», «Memoria 80 %»—
        // y una barra de disco suelta debajo: tres cosas distintas diciendo lo mismo de tres
        // formas.
        //
        // Con anillo se leen de un vistazo —lleno, medio, vacío— sin comparar números. Y el
        // disco entra como uno más, que es lo que es: otra cosa que se llena.
        var fila = new Grid();
        for (int i = 0; i < 3; i++)
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var mCpu = MetricRing("CPU", 0xE950, Nativo.Cpu(), Historial.Cpu.ToList());
        var mRam = MetricRing("RAM", 0xE964, Nativo.Ram(), Historial.Ram.ToList());

        // «Disco» a secas y la letra abajo: «Disco C:» en una cabecera de 74 px útiles, con
        // icono delante, se cortaba. La unidad no se pierde — va en la línea de detalle,
        // donde además acompaña a los gigas, que es con lo que tiene sentido leerla.
        var disco = Discos().FirstOrDefault();
        var mDisco = disco.Total > 0
            ? MetricRing("Disco", 0xEDA2,
                         Lateral.UsadoDe(disco.Libres, disco.Total) * 100,
                         System.Array.Empty<double>(),
                         // Solo los gigas: «C: · 245 GB libres» no cabía en los 74 px útiles
                         // de la tarjeta y salía «C: · 245 GB libr…». La unidad del primer
                         // disco se da por supuesta; los secundarios salen abajo con su letra.
                         $"{Lateral.Tamano(disco.Libres)} libres")
            : null;

        var cuadros = mDisco == null
            ? new[] { mCpu, mRam }
            : new[] { mCpu, mRam, mDisco };

        for (int i = 0; i < cuadros.Length; i++)
        {
            cuadros[i].Visual.Margin = new Thickness(i == 0 ? 0 : 4, 0,
                                                     i == cuadros.Length - 1 ? 0 : 4, 0);
            Grid.SetColumn(cuadros[i].Visual, i);
            fila.Children.Add(cuadros[i].Visual);
        }
        pila.Children.Add(fila);

        // Se actualizan por el mismo pulso que ya tenían, y **solo ellas**: repintar el panel
        // entero cada segundo es lo que hacía parpadear la vista general en C4.1.
        //
        // El disco no: leer el espacio libre pregunta al sistema de ficheros, y hacerlo una
        // vez por segundo para un número que cambia en horas es gasto por nada.
        _actualizadores.Add(() =>
        {
            try
            {
                mCpu.Poner?.Invoke(Nativo.Cpu(), Historial.Cpu.ToList());
                mRam.Poner?.Invoke(Nativo.Ram(), Historial.Ram.ToList());
            }
            catch { }
        });

        // Y los discos que no caben arriba, debajo y a lo ancho.
        var mas = DiscosQueNoCabenArriba();
        if (mas != null) pila.Children.Add(mas);
        return pila;
    }

    // Los discos SECUNDARIOS, a lo ancho. Devuelve `null` cuando no hay ninguno, que es el
    // caso normal: así no se cuela un `StackPanel` vacío que ocupa margen sin dibujar nada.
    FrameworkElement? DiscosQueNoCabenArriba()
    {
        if (Discos().Count <= 1) return null;
        var pila = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        // ---------- Los discos que NO caben como anillo ----------
        //
        // El primero va arriba, de anillo, junto a CPU y memoria. Los demás —si los hay— a lo
        // ancho: un cuarto y un quinto anillo apretarían los tres de arriba hasta que no se
        // lea ninguno.
        //
        // Solo los fijos y solo los que responden: un lector de tarjetas vacío o una unidad
        // de red caída tiran `DriveInfo` y no tienen nada que enseñar.
        foreach (var d in Discos().Skip(1))
        {
            pila.Children.Add(BarraDeDisco(d.Nombre, d.Libres, d.Total));
        }
        return pila;
    }

    static List<(string Nombre, long Libres, long Total)> Discos()
    {
        var l = new List<(string, long, long)>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    l.Add((d.Name.TrimEnd('\\'), d.AvailableFreeSpace, d.TotalSize));
                }
                catch { }       // una unidad que no responde se salta, no tumba la sección
            }
        }
        catch (Exception ex) { Log.Error("leer los discos", ex); }
        return l;
    }

    FrameworkElement BarraDeDisco(string nombre, long libres, long total)
    {
        // ---------- En tarjeta, como las de CPU y memoria ----------
        //
        // Antes era una barra suelta colgando debajo de dos tarjetas, con el nombre del disco
        // pegado al filo. Dentro de «La máquina» había dos cosas con tarjeta y una sin ella,
        // que es de lo que hace que un panel parezca a medio hacer.
        var pila = new StackPanel();

        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ic = Glifo(char.ConvertFromUtf32(0xEDA2));     // HardDrive
        ic.FontSize = 13;
        ic.Opacity = 0.8;
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 8, 0);
        fila.Children.Add(ic);

        var n = Linea(nombre, 12);
        n.FontWeight = FontWeights.SemiBold;
        n.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(n, 1);
        fila.Children.Add(n);

        var c = Linea(Lateral.Almacen(libres, total), 11, 0.7);
        c.HorizontalAlignment = HorizontalAlignment.Right;
        c.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(c, 2);
        fila.Children.Add(c);
        pila.Children.Add(fila);

        // La barra, en coral cuando queda poco. Es el mismo criterio de D6: coral para «ojo»,
        // no una alarma del sistema.
        var canal = new Border
        {
            Height = 5, CornerRadius = new CornerRadius(2.5),
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            Margin = new Thickness(0, 4, 0, 0), ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var lleno = new Border
        {
            Height = 5, CornerRadius = new CornerRadius(2.5),
            Background = new SolidColorBrush(Lateral.QuedaPocoSitio(libres, total)
                ? HyprPinta.Coral(_roles) : HyprPinta.Azul(_roles)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        canal.Child = lleno;
        canal.SizeChanged += (_, _) => lleno.Width = canal.ActualWidth * Lateral.UsadoDe(libres, total);
        pila.Children.Add(canal);
        return Card(pila);
    }

    // ================= Ajustes =================
    FrameworkElement SeccionAjustes()
    {
        var pila = new StackPanel();

        // La segunda línea dice a dónde lleva cada una. Aquí sí vale la pena: «Abrir Ajustes»
        // y «NubeHypr» no se distinguen por el nombre si no sabes ya lo que hay dentro, y
        // estas dos filas son la puerta a toda la configuración del programa.
        pila.Children.Add(FilaRica(0xE713, Tonos.DeLaCasa(_roles), "Ajustes de la nube",
                                   "Tema, estilo, módulos y atajos",
                                   "Abrir la ventana de ajustes", AbrirAjustes));

        // El estado de NubeHypr en la propia fila: es la pregunta que uno tiene al verla.
        var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);
        string queHace = !_cfg.NubeHypr.Activo ? "Apagado"
                       : modo == NubeHypr.Modo.Aplicar ? "Moviendo ventanas de verdad"
                       : "En simulación: propone y no toca";

        pila.Children.Add(FilaRica(0xE753, Tonos.DeLaCasa(_roles), "NubeHypr",
                                   queHace, "Reglas, atajos y estado de NubeHypr",
                                   AbrirAjustesEnNubeHypr));

        // La tercera fila de la referencia. Lleva directa a Apariencia, que es donde están el
        // tema, el estilo y el halo — y que ahora mismo es lo que más se toca.
        pila.Children.Add(FilaRica(0xE790, Tonos.Lila, "Apariencia y temas",
                                   "Colores, estilo y halo",
                                   "Tema, estilo visual y halo del neón",
                                   () => AbrirAjustesEn("apariencia")));
        return pila;
    }
}
