using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace NubeBar;

// El lenguaje visual de los paneles: «el centro de control de NubeBar».
//
// ---------- Por qué existe ----------
//
// Cada panel se dibujaba por su cuenta, y por eso parecían de programas distintos: el de
// red con sus filas, el centro de control con sus botoncitos, el de batería con otra
// medida de margen. Aquí están las piezas, una vez, y todos los paneles las usan.
//
// ---------- Y por qué las superficies son OPACAS ----------
//
// Esto es el arreglo de la queja de fondo. `Roles.Superficie` es un velo translúcido del
// color de texto —12 %— y encima la isla pinta con `Tema.Fondo`, que de serie es
// `#E6000000`: un 90 % de negro. Noventa por ciento suena a mucho y no lo es: por el 10 %
// que queda se lee perfectamente el texto de un terminal, y un panel de ajustes con el
// escritorio asomando por debajo no se lee.
//
// `Roles` ya daba la solución y nadie la usaba: `SuperficieSolida` y
// `SuperficieAltaSolida` son esas mismas superficies **aplanadas sobre el fondo opaco del
// tema**. Mismo color a la vista, sin nada detrás. Se derivan del tema, así que los siete
// temas siguen mandando y no hay ni una mezcla a mano.
//
// La isla sigue siendo translúcida: eso es su identidad. Lo que se vuelve opaco es la
// superficie de las TARJETAS, que es donde hay que leer.
public partial class MainWindow
{
    // ---------- Las medidas, en un sitio ----------
    //
    // Si cada panel elige sus números, cada panel mide distinto. Estos son los de todos.
    //
    // Y desde el rediseño salen del TEMA, no de un ajuste aparte: la forma es un valor del
    // tema igual que lo son los colores, así que Videojuegos va en chaflán y a radio 0 y
    // Sakura a 25 sin que nadie tenga que elegir dos cosas que en realidad son una.
    //
    // Se calcula una vez por tema aplicado y no en cada pregunta: `Formas.Del` mira
    // decoraciones y resuelve colores, y esto se pide tres veces por tarjeta.
    internal Forma EstiloDeAhora => _forma;
    Forma _forma = Formas.Del(Pintura.PorDefecto());

    internal double RadioCard => EstiloDeAhora.RadioCard;
    internal double RadioShell => EstiloDeAhora.RadioShell;
    internal double HuecoCard => EstiloDeAhora.Hueco;      // entre tarjetas
    internal double DentroCard => EstiloDeAhora.Dentro;    // del filo al contenido

    // Éste NO sale del estilo: es la medida mínima para poder pulsar algo con el ratón, y eso
    // no es decoración. Un estilo que la encogiera haría los controles más difíciles de usar
    // a cambio de verse más compacto, que es exactamente el trato que ningún estilo puede
    // ofrecer.
    internal const double AltoTocable = 34;

    // ================= Cuánto mide una fila dentro de una tarjeta =================
    //
    // La cuenta que hacía falta en tres sitios y estaba escrita tres veces, mal las tres.
    //
    // ---------- Los cuatro píxeles que faltaban ----------
    //
    // `EstiloBoton` le pone a TODOS los botones `Margin = (2,0,2,0)`. Una fila que se calcula
    // el ancho «de filo a filo de la tarjeta» y luego se mete en un botón se pasa por esos
    // cuatro píxeles, y lo que sobresale se recorta por la derecha — que es justo donde va la
    // flecha. Se veía como un galón partido por la mitad pegado al borde.
    //
    // Y el relleno de la tarjeta no es una constante desde que hay estilos: «Normal» mete 13,
    // «Nube» 15 y «Neón» 12 con filo de 1,4. Las cuentas con el 26 escrito a mano daban una
    // tarjeta distinta en cuanto cambiabas de estilo.
    //
    // `conBoton: false` para lo que no va dentro de un botón y por tanto no paga ese margen.
    internal double AnchoDeFilaEnTarjeta(bool conBoton = true)
    {
        var e = EstiloDeAhora;
        // 28 = los márgenes del cuerpo del panel (14 a cada lado, ver MainWindow.Laterales).
        double dentro = Lateral.Ancho - 28 - (e.Dentro + e.GrosorFilo) * 2;
        return Math.Max(40, dentro - (conBoton ? 4 : 0));
    }

    SolidColorBrush PincelSolido(Color c) => new(c);

    // ================= PanelShell =================
    //
    // El contenedor de un panel. Lo que aporta: superficie opaca, filo sutil, esquinas y
    // un margen interior consistente. No decide el tamaño — eso es de `AbrirPanel`, que
    // mide y coloca, y cuya lógica no se toca.
    internal Border PanelShell(UIElement contenido, double margen = 12)
    {
        var e = EstiloDeAhora;

        // Todo el panel va marcado: la red de seguridad por si una pieza nueva se olvida
        // de marcarse. Un panel es, por definición, una superficie con la que se interactúa.
        // ---------- La concha pide SU clave, no la de las tarjetas ----------
        //
        // Las dos pedían `SuperficieSolida`, o sea el fondo de TARJETA, así que el panel y sus
        // tarjetas salían del mismo tono y la jerarquía se perdía: seis bloques flotando sobre
        // un fondo de su mismo color. La maqueta los separa —`panel-fondo` y `tarjeta` son dos
        // valores distintos en los nueve temas— y ahora el código también.
        //
        // Opaco de verdad: un panel de controles con el escritorio transparentándose detrás
        // hace ilegible cualquier texto pequeño. Es la regla de C5, y por eso `OpacoDe`.
        var b = SinArrastre(new Border
        {
            Background = PincelSolido(_roles.OpacoDe("panel.fondo")),
            // Filo discreto SIEMPRE. Antes aquí y en `Card` se miraba `FiloDeAcento`, y por
            // eso el panel y sus seis tarjetas salían todos con el mismo contorno azul.
            BorderBrush = _roles.Pincel("panel.filo"),
            BorderThickness = new Thickness(e.GrosorFilo),
            CornerRadius = new CornerRadius(e.RadioShell),
            Padding = new Thickness(margen),
            Child = contenido,
        });
        // La concha lleva el halo de AMBIENTE: ancho y flojo, como luz detrás del panel. Es
        // lo que hace que el panel parezca despegado del fondo de pantalla sin dibujarle un
        // marco alrededor.
        PonerResplandor(b, e, Realce.Ambiente, aparte: true);
        ChaflanSiToca(b);
        return b;
    }

    // ================= Card =================
    //
    // La tarjeta. `alta` la pinta con la superficie de "elegido o bajo el ratón", para
    // poder anidar una dentro de otra sin que se pierda el borde.
    // `dentro` a `double.NaN` significa «el del estilo». No se puede poner `DentroCard` como
    // valor por defecto del parámetro porque ya no es una constante: sale del estilo elegido,
    // y C# exige que los valores por defecto se sepan al compilar.
    // `realce` dice cuánta luz le toca. Por defecto `Reposo`: una tarjeta normal lleva filo
    // oscuro y un brillo que casi no se nota. Lo que de verdad está activo lo pide.
    internal Border Card(UIElement contenido, bool alta = false, double dentro = double.NaN,
                         Realce realce = Realce.Reposo)
    {
        var e = EstiloDeAhora;
        var b = new Border
        {
            Background = PincelSolido(_roles.OpacoDe(alta ? "tarjeta.fondo.raton" : "tarjeta.fondo")),
            // ---------- Filo oscuro, no de acento ----------
            //
            // Aquí estaba el «borde azul fijo alrededor de todas las tarjetas». El acento en
            // el filo se reserva a lo que está activo: si lo lleva todo, no distingue nada.
            BorderBrush = realce == Realce.Reposo || realce == Realce.Ambiente
                ? _roles.Pincel("tarjeta.filo")
                : _roles.Pincel(ClavesBase.Acento),
            BorderThickness = new Thickness(e.GrosorFilo),
            CornerRadius = new CornerRadius(e.RadioCard),
            Padding = new Thickness(double.IsNaN(dentro) ? e.Dentro : dentro),
            Margin = new Thickness(0, 0, 0, e.Hueco),
            Child = BajoElBarrido(contenido),
        };
        ChaflanSiToca(b);
        PonerResplandor(b, e, realce, aparte: true);
        return b;
    }

    // ---------- El resplandor del neón ----------
    //
    // Una sombra difuminada del color de acento, sin desplazamiento: eso es lo que se lee
    // como «brilla» y no como «tiene sombra».
    //
    // Va aquí y no repartido por cada tarjeta para que un estilo nuevo no haya que
    // enchufarlo en quince sitios — y para que apagarlo sea de verdad apagarlo: un efecto
    // olvidado en una superficie es lo que hace que un tema «sobrio» tenga una esquina que
    // brilla.
    // `late` a false da el resplandor sin animarlo. Lo usan las piezas pequeñas —el icono de
    // cada cabecera—: con los dos paneles abiertos hay una docena, y animarlas todas
    // multiplica por tres los efectos que se recalculan en cada fotograma para un latido que
    // en 13 píxeles no se aprecia. El que se ve es el de las tarjetas, y ese sí late.
    //
    // `aparte` saca el halo a una pieza propia detrás del contenido (ver `CapaDelHalo`).
    // Lo piden la concha y las tarjetas de los paneles, que llevan texto dentro.
    void PonerResplandor(UIElement quien, Forma e, Realce realce = Realce.Activo,
                         bool late = true, bool aparte = false)
    {
        if (!e.Brilla) { quien.Effect = null; return; }
        if (aparte && quien is Border { Child: not null } conTexto) quien = CapaDelHalo(conTexto);

        // ---------- Un halo sobre algo recortado sale CUADRADO ----------
        //
        // WPF recorta el resultado del efecto con el `ClipToBounds` del elemento, y ese
        // recorte es el RECTÁNGULO de la caja: el halo pierde las esquinas redondeadas y se
        // convierte en un rectángulo de luz alrededor de una superficie curva.
        //
        // Ha pasado dos veces —la isla (ver el aura de `MainWindow.xaml`) y la concha de los
        // paneles laterales (ver `VentanaSuperpuesta.Envoltura`)—, y las dos se descubrieron
        // mirando una captura. No hay forma de arreglarlo desde aquí: la solución es poner el
        // efecto en una pieza sin recorte, y eso lo decide quien monta la superficie. Así que
        // al menos queda dicho en el registro, que es donde se mira cuando algo «se ve raro».
        if (quien.ClipToBounds)
            Log.Info($"AVISO: halo sobre un {quien.GetType().Name} con ClipToBounds; " +
                     "saldrá cuadrado. Va en una pieza aparte sin recorte (ver Envoltura).");

        var n = Neon.Limpia(_cfg?.Neon);
        var (fDif, fInt) = Realces.De(realce);

        // ---------- El color del halo ----------
        //
        // Puede ser distinto del filo: eso es lo que pide un degradado de dos colores, y es
        // además lo que hace que el halo se lea como LUZ y no como una sombra del borde.
        // Sin nada puesto, los dos salen del acento del tema — una sola fuente de verdad.
        var (_, deLaLuz) = Neon.Colores(n, _roles.Acento);

        double bajo = Math.Max(2, e.Resplandor * fDif);
        double alto = Math.Max(bajo + 1, n.Radio * fDif);
        double opacidad = Math.Clamp(0.55 * fInt * n.Intensidad, 0.04, 0.95);

        var brillo = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = deLaLuz,
            BlurRadius = bajo,
            ShadowDepth = 0,          // sin desplazamiento: resplandor, no sombra
            Opacity = opacidad,
        };
        quien.Effect = brillo;

        // ---------- Y que respire ----------
        //
        // Las cuatro decisiones de esta animación, y por qué:
        //
        // 1. **Se animan el difuminado Y la intensidad**, a la vez. Solo el difuminado se
        //    lee como «se desenfoca»; solo la intensidad, como «se enciende y se apaga». Lo
        //    que parece luz respirando es crecer y avivarse juntos.
        // 2. **El halo NUNCA se apaga del todo.** El mínimo de la intensidad es el 45 % del
        //    máximo, no cero: un efecto que desaparece y vuelve parpadea, y un parpadeo en
        //    algo que vives todo el día es insoportable a los diez minutos.
        // 3. **No se toca la opacidad de la TARJETA** —solo la del efecto—, ni su tamaño, ni
        //    su posición. El contenido no se mueve ni cambia de brillo: si al respirar se
        //    moviera el texto, esto dejaría de ser decoración y pasaría a estorbar.
        // 4. **Solo con las animaciones al máximo** (`Animacion.Adorno`), y si el usuario no
        //    lo ha apagado. Con animaciones reducidas el halo se queda FIJO y visible, que es
        //    lo que pide la accesibilidad: sin movimiento, pero sin perder el estilo.
        if (!late || !e.Late || !n.Respira || !Animacion.Adorno) return;

        var suave = new System.Windows.Media.Animation.SineEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut,
        };
        var cuanto = TimeSpan.FromMilliseconds(n.Velocidad / 2.0);   // ida; la vuelta la hace AutoReverse

        brillo.BeginAnimation(
            System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = bajo, To = alto, Duration = cuanto, AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = suave,
            });

        brillo.BeginAnimation(
            System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = opacidad * Neon.SueloDeLaRespiracion, To = opacidad,
                Duration = cuanto, AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = suave,
            });
    }

    // ---------- El halo, en una pieza aparte ----------
    //
    // Los paneles se veían borrosos, y no era el escalado: era el halo. Un `DropShadowEffect`
    // pinta el elemento ENTERO —con su texto— en una imagen intermedia y la pega después; en
    // cuanto esa imagen cae a medio píxel, todo lo de dentro se suaviza. La barra, sin halo,
    // salía nítida en la misma ventana.
    //
    // Medido fotografiando el panel de red (varianza del laplaciano; más es más nítido):
    //   416 con el halo en la tarjeta · 792 sin halo · el escalado al 86 % no cambia nada
    //
    // Así que el efecto va en una pieza sin texto: una copia del fondo de la tarjeta, debajo
    // del contenido, que da la misma silueta para difuminar. Es lo mismo que el aura de la
    // isla (ver el XAML). Fondo y esquinas van enlazados a la tarjeta, así que si cambia de
    // fondo al pasar el ratón, la copia cambia con ella.
    const string CapaHalo = "capa-del-halo";

    UIElement CapaDelHalo(Border b)
    {
        if (b.Child is Grid { Tag: CapaHalo } ya && ya.Children.Count > 0) return ya.Children[0];

        var sombra = new Border
        {
            IsHitTestVisible = false,
            // Por dentro del filo: el relleno de la tarjeta, sin pisar su borde
            Margin = new Thickness(-b.Padding.Left, -b.Padding.Top, -b.Padding.Right, -b.Padding.Bottom),
        };
        sombra.SetBinding(Border.BackgroundProperty,
                          new System.Windows.Data.Binding(nameof(Border.Background)) { Source = b });
        sombra.SetBinding(Border.CornerRadiusProperty,
                          new System.Windows.Data.Binding(nameof(Border.CornerRadius)) { Source = b });

        var dentro = b.Child;
        b.Child = null;
        var capa = new Grid { Tag = CapaHalo };
        capa.Children.Add(sombra);
        capa.Children.Add(dentro);
        b.Child = capa;
        return sombra;
    }

    // ================= SectionHeader =================
    //
    // El rótulo de una sección dentro de un panel: icono, título y, a la derecha, lo que
    // haga falta (un porcentaje, un botón, una insignia).
    // ---------- La flecha de la cabecera ----------
    //
    // En la maqueta que mandó el usuario, **todas** las cabeceras llevan una flecha a la
    // derecha. Parece un detalle y es lo que más hace: con ella, seis tarjetas distintas se
    // leen como el mismo sistema; sin ella, como seis cosas que coincidieron en un panel.
    //
    // Es un adorno **solo** si no hay dónde ir. Cuando la sección tiene un sitio al que
    // llevarte, se le pasa la acción y la flecha se vuelve pulsable; si no, se pinta
    // atenuada y no se puede pulsar — una flecha que no lleva a ningún sitio y además
    // responde al clic es peor que no tenerla.
    internal UIElement FlechaDeCabecera(Action? alPulsar, string? ayuda = null)
    {
        var f = Glifo(char.ConvertFromUtf32(0xE76C));   // ChevronRight
        f.FontSize = 12;
        f.Foreground = _roles.Pincel("seccion.flecha");
        f.Opacity = alPulsar == null ? 0.45 : 0.9;
        f.VerticalAlignment = VerticalAlignment.Center;

        if (alPulsar == null) return f;

        var b = Boton(f, ayuda ?? "Abrir", alPulsar, levantar: false);
        b.Padding = new Thickness(4, 0, 2, 0);
        b.MinWidth = 0;
        return b;
    }

    // ================= El brillo que recorre un texto =================
    //
    // La maqueta, para el reloj: «degradado lineal de texto (texto → blanco → texto) cuyo
    // desplazamiento se anima en 7 s: un brillo que lo recorre».
    //
    // ---------- Por qué un TranslateTransform y no mover las paradas ----------
    //
    // Animar las `GradientStop` una a una son tres animaciones sincronizadas que WPF recalcula
    // por fotograma. Un `TranslateTransform` en el `RelativeTransform` del pincel es UNA, y
    // además la hace la composición: el pincel no se vuelve a construir, solo se mapea en otro
    // sitio. Es la misma idea que el giro del filo de la isla.
    //
    // ---------- Y por qué solo el reloj ----------
    //
    // Es el único texto de la barra que está SIEMPRE y que nadie lee entero: se mira de reojo.
    // Un brillo recorriendo la fecha, el título del wallpaper o el nombre de una app sería
    // movimiento encima de algo que sí se lee, y eso cansa. La maqueta lo pide solo aquí.
    //
    // Respeta el nivel de animación: sin adorno, el texto se queda del color de siempre.
    internal void PonerBrilloDeTexto(TextBlock tb)
    {
        if (!Animacion.Adorno) return;

        // ---------- O el glitch, si el tema lo pide ----------
        //
        // Cyberpunk cambia el brillo por un tropiezo: el reloj se desplaza un par de píxeles y
        // se tiñe de cian o magenta durante un fotograma, cada pocos segundos.
        //
        // Con keyframes DISCRETOS, no interpolados: un glitch que se desvanece suavemente no
        // es un glitch, es un fundido. Lo que lo hace leerse como un fallo de señal es que
        // aparezca y desaparezca de golpe.
        if (_roles.Pintura.Tema.Lleva(Decoraciones.RelojGlitch)) { PonerGlitch(tb); return; }

        try
        {
            var tinta = _roles.Texto;
            var luz = _roles.ColorDe("reloj.brillo");

            var g = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
            // La franja de luz, estrecha y en medio del recorrido.
            g.GradientStops.Add(new GradientStop(tinta, 0.00));
            g.GradientStops.Add(new GradientStop(tinta, 0.38));
            g.GradientStops.Add(new GradientStop(luz, 0.50));
            g.GradientStops.Add(new GradientStop(tinta, 0.62));
            g.GradientStops.Add(new GradientStop(tinta, 1.00));

            // De un lado a otro: el pincel mide lo que el texto, así que desplazarlo una
            // vez y media lo saca por completo por los dos extremos.
            var mueve = new TranslateTransform(-1.4, 0);
            g.RelativeTransform = mueve;
            tb.Foreground = g;

            var a = new DoubleAnimation(-1.4, 1.4, TimeSpan.FromSeconds(7))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            a.Freeze();
            mueve.BeginAnimation(TranslateTransform.XProperty, a);
        }
        catch (Exception ex) { Log.Error("poner el brillo del reloj", ex); }
    }

    // ---------- El glitch del reloj ----------
    //
    // Tres canales desplazados sería lo fiel a un monitor roto, y son tres `TextBlock`
    // superpuestos que hay que mantener sincronizados con la hora. Aquí se consigue lo mismo
    // con uno: el desplazamiento y el color saltan a la vez en keyframes discretos, y a la
    // velocidad a la que ocurre el ojo lo lee igual.
    void PonerGlitch(TextBlock tb)
    {
        try
        {
            var tinta = _roles.Texto;
            var cian = _roles.ColorDe(ClavesBase.Sec2);
            var magenta = _roles.ColorDe(ClavesBase.Halo);

            var pincel = new SolidColorBrush(tinta);
            tb.Foreground = pincel;
            var mueve = new TranslateTransform();
            tb.RenderTransform = mueve;

            var ciclo = TimeSpan.FromMilliseconds(2800);

            var salto = new DoubleAnimationUsingKeyFrames { Duration = ciclo, RepeatBehavior = RepeatBehavior.Forever };
            void Salto(double x, double ms) =>
                salto.KeyFrames.Add(new DiscreteDoubleKeyFrame(x, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            Salto(0, 0); Salto(-2, 2400); Salto(2, 2460); Salto(-1, 2520); Salto(0, 2580);

            var tinte = new ColorAnimationUsingKeyFrames { Duration = ciclo, RepeatBehavior = RepeatBehavior.Forever };
            void Tinte(Color c, double ms) =>
                tinte.KeyFrames.Add(new DiscreteColorKeyFrame(c, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            Tinte(tinta, 0); Tinte(cian, 2400); Tinte(magenta, 2460); Tinte(cian, 2520); Tinte(tinta, 2580);

            salto.Freeze(); tinte.Freeze();
            mueve.BeginAnimation(TranslateTransform.XProperty, salto);
            pincel.BeginAnimation(SolidColorBrush.ColorProperty, tinte);
        }
        catch (Exception ex) { Log.Error("poner el glitch del reloj", ex); }
    }

    // ================= Las líneas de barrido =================
    //
    // La trama de monitor CRT que Cyberpunk pone sobre sus paneles. Un `DrawingBrush` teselado
    // de 3 px: se repite en GPU, así que cuesta lo mismo cubrir una tarjeta que la pantalla.
    //
    // Va en una capa POR ENCIMA del contenido y sin recibir el ratón. Debajo no valdría: lo
    // que hace que se lea como un monitor es que las líneas pasen por encima del texto, no que
    // asomen por los huecos.
    //
    // El alfa es bajísimo (0,07) y no es tacañería: a 3 px de paso, cualquier cosa más fuerte
    // deja el texto pequeño ilegible — y una decoración no puede cobrarse la lectura.
    UIElement BajoElBarrido(UIElement contenido)
    {
        if (!_roles.Pintura.Tema.Lleva(Decoraciones.LineasDeBarrido)) return contenido;

        var c = _roles.ColorDe(ClavesBase.Sec2);
        var raya = new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(0x12, c.R, c.G, c.B)),
            null,
            new RectangleGeometry(new Rect(0, 0, 3, 1)));

        var trama = new DrawingBrush(raya)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 3, 3),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        trama.Freeze();

        var capa = new Grid();
        capa.Children.Add(contenido);
        capa.Children.Add(new Border { Background = trama, IsHitTestVisible = false });
        return capa;
    }

    // ================= El chaflán =================
    //
    // Videojuegos y Cyberpunk declaran `esquinas: chaflan`: la esquina CORTADA en diagonal,
    // no la esquina recta. Hasta ahora se aproximaba poniéndoles radio 0, que es otra cosa —
    // un rectángulo a secas es lo que hace cualquier programa que no ha decidido nada, y el
    // chaflán es una decisión que se ve.
    //
    // ---------- Por qué un Clip y no un Border con forma ----------
    //
    // `Border` solo sabe redondear (`CornerRadius`). Para cortar hace falta una geometría, y
    // ponerla como `Clip` recorta ADEMÁS el fondo, el filo y todo lo que haya dentro, que es
    // justo lo que se quiere: la tarjeta entera con la esquina cortada, no un marco cortado
    // con un fondo rectangular asomando por debajo.
    //
    // Se recalcula en `SizeChanged` porque la geometría va en píxeles y la tarjeta cambia de
    // tamaño: una tarjeta que crece con el chaflán de cuando medía otra cosa se corta por
    // donde no es.
    //
    // Dos cortes y no cuatro, en diagonal opuesta: es lo que la maqueta dibuja y lo que hace
    // que la forma tenga dirección en vez de parecer un octógono.
    internal static Geometry FormaDeChaflan(double w, double h, double corte)
    {
        corte = Math.Max(2, Math.Min(corte, Math.Min(w, h) / 2));
        var f = new PathFigure { StartPoint = new Point(corte, 0), IsClosed = true, IsFilled = true };
        f.Segments.Add(new LineSegment(new Point(w, 0), true));
        f.Segments.Add(new LineSegment(new Point(w, h - corte), true));
        f.Segments.Add(new LineSegment(new Point(w - corte, h), true));
        f.Segments.Add(new LineSegment(new Point(0, h), true));
        f.Segments.Add(new LineSegment(new Point(0, corte), true));
        var g = new PathGeometry();
        g.Figures.Add(f);
        g.Freeze();
        return g;
    }

    // Le pone el chaflán a una superficie si el tema lo pide, y si no la deja como está.
    // El corte sale del tamaño: un chaflán fijo de 10 px se come media tarjeta pequeña y no se
    // nota en un panel grande.
    internal void ChaflanSiToca(Border b)
    {
        if (_roles.Pintura.Tema.Esquinas != Esquinas.Chaflan) return;
        b.CornerRadius = new CornerRadius(0);
        void Recortar()
        {
            if (b.ActualWidth < 6 || b.ActualHeight < 6) return;
            b.Clip = FormaDeChaflan(b.ActualWidth, b.ActualHeight,
                                    Math.Clamp(Math.Min(b.ActualWidth, b.ActualHeight) * 0.22, 5, 14));
        }
        b.SizeChanged += (_, _) => Recortar();
        Recortar();
    }

    // ================= El filo de la isla, y la luz que lo recorre =================
    //
    // La maqueta lo describe así: «detrás del filo, un Border con un LinearGradientBrush
    // (transparente → plata 60 % → acento → transparente, en una franja estrecha) y un
    // RotateTransform en su RelativeTransform animado 0→360° en 9 s, lineal, infinito».
    //
    // Aquí no hace falta un Border detrás: el propio `BorderBrush` de la isla puede ser ese
    // degradado, y así la luz sigue las esquinas redondeadas sin recortar nada. El filo mide
    // 1 px y el degradado se mapea sobre la caja del borde, que es justo lo que hace que la
    // franja parezca dar la vuelta.
    //
    // Se para solo cuando no toca: sin la decoración puesta, sin nivel de adorno, o con la
    // isla escondida — `Animacion.Adorno` ya lo decide, y una animación infinita corriendo
    // detrás de una barra que no se ve es gasto puro.
    internal void PonerFiloDeLaIsla(Pintura pintura, bool bordeAcento)
    {
        Isla.BorderThickness = new Thickness(bordeAcento ? pintura.Tema.Medida(Medidas.GrosorFilo, 1) : 0);
        if (!bordeAcento) { Isla.BorderBrush = null; return; }

        var filo = pintura.R("isla.filo");
        bool destella = pintura.Tema.Lleva(Decoraciones.DestelloFilo)
                        && !pintura.R("isla.destello").EsInvisible
                        && Animacion.Adorno;

        if (!destella) { Isla.BorderBrush = filo.Pincel(); return; }

        Isla.BorderBrush = pintura.Tema.Lleva(Decoraciones.ModoRgb)
            ? FiloRgb(9)
            : FiloQueRecorre(filo.Color, pintura.Color("isla.destello"),
                             pintura.Color(ClavesBase.Acento), 9);
    }

    // ---------- Una franja de luz dando la vuelta a un filo ----------
    //
    // La usa la isla (9 s) y el buscador del Inicio (4 s). Está aquí y no copiada en los dos
    // porque dos degradados animados escritos aparte acaban discrepando en el ancho de la
    // franja, y entonces la barra tiene dos luces que se parecen pero no son la misma.
    //
    // La franja va estrecha a propósito: ancha deja de leerse como una luz que pasa y se lee
    // como un degradado de dos colores, que es otra cosa.
    // ---------- El modo RGB ----------
    //
    // Lo que pide Videojuegos: «filo en modo RGB que rota de color». No es el mismo degradado
    // con otros colores — es una rueda de tono entera dando vueltas, que es lo que hace que se
    // lea como el led de una torre y no como un borde bonito.
    //
    // Los seis tonos van a saturación y brillo fijos y no salen del tema a propósito: el RGB
    // ES el efecto. Un «modo RGB» en los colores del tema sería un degradado, y eso ya existe
    // tres líneas más abajo.
    internal static Brush FiloRgb(double segundos)
    {
        var g = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
        for (int i = 0; i <= 6; i++)
            g.GradientStops.Add(new GradientStop(
                SelectorColor.DesdeHsv(i * 60 % 360, 0.95, 1.0, 0xFF), i / 6.0));

        var giro = new RotateTransform(0, 0.5, 0.5);
        g.RelativeTransform = giro;
        var a = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(segundos))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        a.Freeze();
        giro.BeginAnimation(RotateTransform.AngleProperty, a);
        return g;
    }

    internal static Brush FiloQueRecorre(Color baseFilo, Color luz, Color acento, double segundos)
    {
        var g = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
        g.GradientStops.Add(new GradientStop(baseFilo, 0.00));
        g.GradientStops.Add(new GradientStop(baseFilo, 0.40));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x99, luz.R, luz.G, luz.B), 0.47));
        g.GradientStops.Add(new GradientStop(acento, 0.50));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x99, luz.R, luz.G, luz.B), 0.53));
        g.GradientStops.Add(new GradientStop(baseFilo, 0.60));
        g.GradientStops.Add(new GradientStop(baseFilo, 1.00));

        var giro = new RotateTransform(0, 0.5, 0.5);
        g.RelativeTransform = giro;

        var a = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(segundos))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        a.Freeze();
        giro.BeginAnimation(RotateTransform.AngleProperty, a);
        return g;
    }

    // ================= El aura: el halo que respira =================
    //
    // Va en la envoltura `Aura` y no en la isla. El motivo, con su medida, está en el XAML:
    // un efecto sobre un elemento con `ClipToBounds` se recorta al rectángulo, y salía un halo
    // cuadrado asomando por las puntas redondas.
    //
    // Los números son los de la maqueta: color de acento, `ShadowDepth 0`, radio ~26, y la
    // opacidad respirando entre 0,08 y 0,2 en 6 s. Los mandos del usuario (`Neon.Limpia`)
    // siguen mandando sobre la intensidad, el radio y el ciclo.
    internal void PonerAuraDeLaIsla(Pintura pintura)
    {
        bool hay = pintura.Tema.Lleva(Decoraciones.Halo) && !pintura.R("isla.halo").EsInvisible;
        if (!hay) { Aura.Effect = null; return; }

        var n = Neon.Limpia(_cfg.Neon);
        var efecto = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = pintura.Color("isla.halo"),
            ShadowDepth = 0,
            BlurRadius = n.Radio,
            Opacity = 0.14 * n.Intensidad,
        };
        Aura.Effect = efecto;

        // Sin adorno, el halo se queda quieto y visible. Es la regla de `Animacion.Adorno`:
        // en «reducido» no se mueve nada que no haga falta, pero tampoco se apaga lo que
        // informa de que la barra está ahí.
        if (!n.Respira || !Animacion.Adorno) return;

        var a = new DoubleAnimation(0.08 * n.Intensidad, 0.20 * n.Intensidad,
                                    TimeSpan.FromMilliseconds(n.Velocidad))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        efecto.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, a);
    }

    // ---------- De un nombre de letra a una letra que existe ----------
    //
    // ---------- Lo primero: «Segoe UI Variable» NO es una familia ----------
    //
    // Windows 11 la reparte en tres —`Segoe UI Variable Small`, `… Text` y `… Display`— y la
    // familia a secas no existe. Buscándola por nombre exacto no aparece, así que Profesional
    // y Oficina caían a Segoe UI y la letra del tema seguía sin aplicarse: el fallo que esta
    // fase venía a arreglar, arreglado a medias. Medido en esta máquina, con las tres
    // variantes instaladas y la familia base ausente.
    //
    // Por eso, si el nombre exacto no está, se busca una variante que empiece por él y se
    // elige por PAPEL: `Display` para títulos y relojes, `Text` para el cuerpo. Es lo que la
    // propia Microsoft recomienda para Segoe UI Variable, y de paso vale para cualquier otra
    // familia que venga partida.
    //
    // ---------- Y lo segundo: seis de los nueve temas piden letras que no están ----------
    //
    // Rajdhani, Orbitron, M PLUS Rounded 1c, Zen Maru Gothic, Shippori Mincho y Nunito. El
    // plan las incrusta en el exe como recurso en las fases 6 y 7; hasta entonces, pedirlas
    // devuelve la del sistema. Lo que no puede pasar es que lo haga EN SILENCIO: un tema que
    // se ve con otra letra y un registro mudo es el fallo que cuesta media tarde encontrar.
    // Se apunta una vez por nombre, no en cada repintado.
    static readonly Dictionary<string, FontFamily> _letrasResueltas = new(StringComparer.OrdinalIgnoreCase);

    // ---------- Primero las que viajan dentro del programa ----------
    //
    // Seis de los nueve temas piden letras que no están en Windows. Viajan en el exe como
    // recurso (`Fuentes\*.ttf`) y WPF las carga con un `pack://application:,,,/Fuentes/#` más
    // el nombre de la FAMILIA — no el del fichero.
    //
    // Se prueba esto ANTES que las del sistema a propósito: si alguien tiene instalada una
    // versión distinta de Rajdhani, el tema tiene que verse como lo diseñamos y no como lo
    // tenga esa máquina. Un tema que cambia de aspecto según el equipo no es un tema.
    static readonly string[] Propias =
    {
        "Rajdhani", "Orbitron", "Nunito",
        "M PLUS Rounded 1c", "Zen Maru Gothic", "Shippori Mincho",
    };

    internal static FontFamily LetraDe(string nombre, string papel = "texto")
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new FontFamily("Segoe UI");

        var cache = nombre + "|" + papel;
        if (_letrasResueltas.TryGetValue(cache, out var ya)) return ya;

        FontFamily elegida;
        try
        {
            if (Propias.Any(n => n.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
            {
                var f = new FontFamily(new Uri("pack://application:,,,/"), "./Fuentes/#" + nombre);
                // `FontFamily` no se queja si no encuentra nada: devuelve un objeto que pinta
                // con la letra por defecto. La única forma de saber si de verdad está es
                // pedirle sus tipos — una familia que no existe no tiene ninguno.
                if (f.GetTypefaces().Count > 0)
                {
                    _letrasResueltas[cache] = f;
                    return f;
                }
                Log.Info($"la letra «{nombre}» viaja en el programa pero no se pudo cargar; " +
                         "se busca en el sistema");
            }

            var todas = Fonts.SystemFontFamilies.Select(n => n.Source).ToList();
            var exacta = todas.FirstOrDefault(n => n.Equals(nombre, StringComparison.OrdinalIgnoreCase));
            if (exacta != null) elegida = new FontFamily(exacta);
            else
            {
                // ---------- «Segoe UI Variable» NO es una familia ----------
                //
                // Windows 11 la reparte en tres —`… Small`, `… Text` y `… Display`— y la
                // familia a secas no existe. Buscándola por nombre exacto no aparece, así que
                // Profesional y Oficina caían a Segoe UI y la letra del tema seguía sin
                // aplicarse. Si el nombre exacto no está, se busca una variante que empiece
                // por él y se elige por PAPEL: `Display` para títulos, `Text` para el cuerpo.
                var variantes = todas
                    .Where(n => n.StartsWith(nombre + " ", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                string? quiero = papel.Equals(Letras.Titulos, StringComparison.OrdinalIgnoreCase)
                    ? variantes.FirstOrDefault(n => n.EndsWith(" Display", StringComparison.OrdinalIgnoreCase))
                    : variantes.FirstOrDefault(n => n.EndsWith(" Text", StringComparison.OrdinalIgnoreCase));
                var cual = quiero ?? variantes.FirstOrDefault() ?? "Segoe UI";
                elegida = new FontFamily(cual);

                Log.Info(variantes.Count > 0
                    ? $"la letra «{nombre}» viene partida en variantes: para {papel} se usa «{cual}»"
                    : $"la letra «{nombre}» no está ni en el programa ni en el sistema: se usa «{cual}»");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"resolver la letra «{nombre}»", ex);
            elegida = new FontFamily("Segoe UI");
        }

        _letrasResueltas[cache] = elegida;
        return elegida;
    }

    // ---------- Un rótulo de sección ----------
    //
    // Mayúsculas, 11,5 px, seminegrita, tenue y con 0,06 em de aire entre letras.
    //
    // ---------- El espaciado, que WPF no tiene ----------
    //
    // No hay `LetterSpacing` en `TextBlock`: ni propiedad, ni `Typography`, ni nada
    // equivalente. Lo que sí hay es meter un espacio fino (U+2009) entre caracteres, que a
    // 11,5 px da del orden de los 0,7 px que pide la maqueta.
    //
    // Y eso rompería al lector de pantalla, que leería «A C C I O N E S» letra por letra. Por
    // eso el texto VISIBLE va espaciado y el nombre de accesibilidad va limpio: los ojos leen
    // uno y NVDA lee el otro. Lo mismo que ya se hace con los botones del dock.
    //
    // `ToUpperInvariant` va DESPUÉS de traducir: `Textos.T` busca por la cadena en español
    // tal cual está escrita, y en mayúsculas no la encontraría.
    internal TextBlock Rotulito(string texto, Brush color)
    {
        var limpio = texto.ToUpperInvariant();
        var t = new TextBlock
        {
            Text = string.Join("\u2009", limpio.ToCharArray()),
            FontFamily = LetraDe(_roles.Pintura.Fuente(Letras.Titulos), Letras.Titulos),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center,
        };
        System.Windows.Automation.AutomationProperties.SetName(t, texto);
        return t;
    }

    internal UIElement SectionHeader(int icono, string titulo, UIElement? derecha = null)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var est = EstiloDeAhora;

        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 13;
        ic.Foreground = _roles.Pincel("seccion.icono");
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(ic, 0);
        g.Children.Add(ic);

        // ---------- El estilo, DENTRO de la tarjeta ----------
        //
        // Con `TintaDeAcento` —hoy solo «Neón»— el icono de la cabecera se enciende y el
        // título toma el color de acento. Es la respuesta a «solo lo aplicas a los bordes»:
        // un estilo que únicamente pinta el filo deja el contenido exactamente igual que el
        // estilo sobrio, y entonces lo que has elegido es un marco.
        //
        // Se para en la CABECERA. El cuerpo sigue con el color de texto normal, porque un
        // panel entero en color de acento se lee peor — y una decoración puede cambiar cómo
        // se ve algo, pero no puede cobrarse que se lea.
        //
        // El título va de `_acentoLegible` y no del acento a pelo: ese ya viene pasado por
        // contraste contra la superficie, así que en un tema claro no se queda en un pastel
        // ilegible.
        if (est.TintaDeAcento) PonerResplandor(ic, est, late: false);

        // ---------- Mayúsculas pequeñas, y por qué ----------
        //
        // La maqueta lo fija: «títulos de sección en mayúsculas pequeñas, 11,5 px, espaciado
        // 0,06 em, color tenue». Iban en 12,5 seminegrita y del color del texto normal, o sea
        // compitiendo con el contenido de la tarjeta.
        //
        // Lo que gana el cambio no es gusto: un título que pesa lo mismo que su contenido
        // obliga a leer dos veces para saber cuál es cuál. Bajándolo a tenue, a mayúsculas y
        // con aire entre letras, el ojo lo lee como etiqueta y salta al dato, que es a lo que
        // viene.
        var t = Rotulito(Textos.T(titulo),
                         est.TintaDeAcento ? _acentoLegible : _roles.Pincel("seccion.titulo"));
        Grid.SetColumn(t, 1);
        g.Children.Add(t);

        if (derecha != null)
        {
            Grid.SetColumn(derecha, 2);
            g.Children.Add(derecha);
        }
        return g;
    }

    // ================= Azulejo =================
    //
    // El icono dentro de un cuadrado redondeado y teñido de su propio color.
    //
    // ---------- Por qué esto, y por qué cambia tanto ----------
    //
    // Los paneles tenían filas de «glifo de 13 px al 80 % de opacidad + texto + flecha». Seis
    // de esas en columna son una lista; el usuario lo dijo mirándolo: «parece una lista de
    // texto con bordes, no un centro de control premium». Y tenía razón, pero el problema no
    // era el texto: era que **todos los iconos pesaban lo mismo y eran del mismo color**, así
    // que no había nada por donde entrar a la tarjeta salvo leyendo.
    //
    // Un azulejo le da a cada fila un ancla de color y un peso visual. Es lo que distingue a
    // ojo un centro de control de un menú: se reconoce «Descargas» por el azulejo azul con la
    // flecha antes de haber leído la palabra.
    //
    // ---------- Y el fondo NO es gris ----------
    //
    // El relleno es el propio color del icono muy rebajado, nunca un gris de relleno. La
    // diferencia importa y ya costó un disgusto: cuando los grupos de la barra llevaban
    // pastilla gris, el usuario lo vio y dijo «eso no lo quiero para nada». Un gris detrás de
    // algo no dice nada; un tinte del mismo color dice «esto es de este color».
    internal UIElement Azulejo(int icono, Color color, double lado = 32)
    {
        // El icono, legible sobre su propio tinte. El tinte es el color al 0x26 sobre la
        // superficie de la tarjeta, así que el icono a pelo encima podría quedarse corto de
        // contraste en un tema claro; `TintaSobre` lo resuelve con la regla de la casa.
        var sobre = Roles.Aplanar(color with { A = 0x26 }, _roles.SuperficieSolida);
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = Math.Round(lado * 0.46);
        ic.Foreground = PincelSolido(Roles.TintaSobre(color, sobre));
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        ic.VerticalAlignment = VerticalAlignment.Center;

        return new Border
        {
            Width = lado, Height = lado,
            CornerRadius = new CornerRadius(Math.Round(lado * 0.3)),
            Background = PincelSolido(sobre),
            VerticalAlignment = VerticalAlignment.Center,
            Child = ic,
        };
    }

    // ================= FilaRica =================
    //
    // La fila de un centro de control: azulejo, título, una segunda línea que dice algo de
    // verdad, y el remate de la derecha.
    //
    // ---------- La segunda línea no es relleno ----------
    //
    // Solo se pinta si hay algo que decir. Una fila con un subtítulo inventado —«Documentos /
    // Carpeta»— ocupa el doble y no informa; es la clase de adorno que hace que una interfaz
    // parezca más llena y sea más lenta de leer. Donde no hay dato, la fila es de una línea y
    // el azulejo se queda centrado.
    //
    // ---------- El ancho explícito, otra vez ----------
    //
    // La plantilla del botón centra su contenido y no mira `HorizontalContentAlignment`. Es
    // la misma lección que está escrita en `FilaPulsable` y en la rejilla de acciones
    // rápidas, y por tercera vez la solución es darle el ancho para que no quede hueco que
    // centrar.
    internal FrameworkElement FilaRica(int icono, Color color, string titulo, string? debajo,
                                       string tip, Action? accion, int remate = 0xE76C)
    {
        var caja = new Grid { Width = AnchoDeFilaEnTarjeta(conBoton: accion != null) };
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        caja.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var az = Azulejo(icono, color);
        if (az is FrameworkElement afe) afe.Margin = new Thickness(0, 0, 11, 0);
        Grid.SetColumn(az, 0);
        caja.Children.Add(az);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var t = new TextBlock
        {
            Text = Textos.T(titulo), FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            Foreground = _texto, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        textos.Children.Add(t);
        if (debajo is { Length: > 0 })
            textos.Children.Add(new TextBlock
            {
                Text = Textos.T(debajo), FontSize = 10.5, Foreground = _texto, Opacity = 0.55,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0),
            });
        Grid.SetColumn(textos, 1);
        caja.Children.Add(textos);

        if (remate != 0)
        {
            var r = Glifo(char.ConvertFromUtf32(remate));
            r.FontSize = 11;
            r.Opacity = 0.45;
            r.VerticalAlignment = VerticalAlignment.Center;
            // El 2 de la derecha no es capricho: sin él la flecha queda a ras del filo de la
            // tarjeta y el antialiasing se la come por el lado.
            r.Margin = new Thickness(8, 0, 2, 0);
            Grid.SetColumn(r, 2);
            caja.Children.Add(r);
        }

        // Sin acción no es un botón: una fila que solo informa y se ilumina al pasar por
        // encima promete que hace algo. Se devuelve el contenido tal cual, con su aire.
        if (accion == null)
        {
            caja.Margin = new Thickness(0, 5, 0, 5);
            return caja;
        }

        var b = Boton(caja, tip, accion, levantar: false);
        b.Padding = new Thickness(0);
        b.Height = double.NaN;
        b.MinHeight = AltoTocable + 8;
        return b;
    }

    // ================= Toggle =================
    //
    // Un interruptor de verdad: una pista y un botoncito que se va de un lado al otro.
    // Sustituye al `CheckBox` del sistema, que en un panel oscuro sale como una caja
    // blanca y no se parece a nada de NubeBar.
    //
    // La animación respeta el nivel: en "Desactivado" el botoncito cambia de sitio y ya.
    internal UIElement Toggle(string etiqueta, bool valor, Action<bool> alCambiar, string? ayuda = null)
    {
        bool estado = valor;

        var pista = new Border
        {
            Width = 38, Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = PincelSolido(estado ? _roles.Acento : _roles.SuperficieAltaSolida),
            BorderBrush = PincelSolido(_roles.Borde),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var bolita = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = PincelSolido(estado ? _roles.FondoOpaco : _roles.Texto),
            HorizontalAlignment = estado ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0),
        };
        pista.Child = bolita;

        void Pintar()
        {
            pista.Background = PincelSolido(estado ? _roles.Acento : _roles.SuperficieAltaSolida);
            bolita.Fill = PincelSolido(estado ? _roles.FondoOpaco : _roles.Texto);
            bolita.HorizontalAlignment = estado ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = etiqueta, FontSize = 12, Foreground = _texto, TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(ayuda))
            textos.Children.Add(new TextBlock
            {
                Text = ayuda, FontSize = 10.5, Foreground = _textoAtenuado,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0),
            });

        var g = new Grid { MinHeight = AltoTocable, Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(textos, 0);
        g.Children.Add(textos);
        Grid.SetColumn(pista, 1);
        g.Children.Add(pista);

        // Todo el ancho es pulsable, no solo el interruptor: en un panel, acertar en 38 px
        // con el ratón es peor que acertar en la fila entera.
        var boton = new Button
        {
            Content = g,
            Template = PlantillaDeFila(),
            Cursor = Cursors.Hand,
            Focusable = true,
            ToolTip = ayuda ?? etiqueta,
        };
        // El nombre para el lector de pantalla. La ayuda emergente lleva la frase buena
        // desde siempre, pero un ToolTip no es el nombre de UIA: ver la nota larga en
        // `MainWindow.Boton`.
        System.Windows.Automation.AutomationProperties.SetName(boton, etiqueta);
        boton.Click += (_, _) => { estado = !estado; Pintar(); alCambiar(estado); };
        return boton;
    }

    // ================= Slider =================
    //
    // Un deslizador con su valor a la vista, hecho con piezas propias. El del sistema es
    // una línea de 1 px con un cuadradito: es lo que se ve como "un control sin estilo".
    //
    // `alArrastrar` se llama mientras se mueve (respuesta inmediata, que es lo que se
    // pidió) y `alSoltar` al terminar, para lo que cueste caro.
    internal sealed class MandoSlider
    {
        public FrameworkElement Visual = null!;
        public Action<double>? Poner;
        public GestoDeslizador? Gesto;      // para poder comprobar que no queda nada agarrado
    }

    // `nombre` es para el lector de pantalla, y es un parámetro y no un texto deducido
    // porque solo quien lo crea sabe de qué deslizador se trata: el volumen general, el
    // brillo de una pantalla concreta, el de una aplicación del mezclador.
    //
    // Sin él, los seis deslizadores del panel de audio se anunciaban como seis botones
    // mudos: el envoltorio que les da las flechas lleva un `Grid` de contenido, y de un
    // `Grid` no se saca ningún nombre. Lo encontró la medida de `--diagpaneles`.
    internal MandoSlider Slider(double valor, double min, double max,
                                Action<double> alArrastrar, Action<double>? alSoltar = null,
                                bool apagado = false, string? nombre = null)
    {
        double v = Math.Clamp(valor, min, max);
        var mando = new MandoSlider();

        var fondo = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var lleno = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = PincelSolido(apagado ? _roles.TextoAtenuado : _roles.Acento),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var agarre = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = PincelSolido(apagado ? _roles.TextoAtenuado : _roles.Acento),
            Stroke = PincelSolido(_roles.FondoOpaco),
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // La capa va marcada: su gesto es suyo y no de la isla. Ver MainWindow.Gesto.cs.
        var capa = SinArrastre(new Grid
        {
            Height = AltoTocable,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
        });
        capa.Children.Add(fondo);
        capa.Children.Add(lleno);
        capa.Children.Add(agarre);

        void Pintar()
        {
            double w = capa.ActualWidth;
            if (w <= 1) return;
            double k = (v - min) / Math.Max(0.0001, max - min);
            lleno.Width = Math.Max(0, w * k);
            agarre.Margin = new Thickness(Math.Max(0, w * k - 7), 0, 0, 0);
        }
        capa.SizeChanged += (_, _) => Pintar();
        mando.Poner = nv => { v = Math.Clamp(nv, min, max); Pintar(); };

        // ================= El gesto =================
        //
        // ---------- Por qué NO cuelga de MouseLeftButtonUp ----------
        //
        // Porque en una ventana `WS_EX_NOACTIVATE` y por capas ese evento se pierde: al
        // soltar fuera, al cambiar de ventana, al perder la captura. Si no llega, nadie
        // limpia y el deslizador se queda agarrado moviéndose con el ratón. Pasó.
        //
        // La verdad la dice `Nativo.BotonIzquierdoPulsado()` una vez por fotograma, que es
        // el patrón de la casa desde B2 —el mismo con el que la isla se deja arrastrar— y
        // funciona esté el puntero donde esté. `MouseLeftButtonUp` se mantiene como una
        // vía más, no como la única.
        var gesto = new GestoDeslizador();
        EventHandler? porFotograma = null;

        // La posición se lee del ratón de VERDAD, no del evento: así seguir el arrastre
        // fuera del control sigue funcionando.
        double DeLaPantalla()
        {
            try
            {
                var p = Monitores.Raton();
                var local = capa.PointFromScreen(new Point(p.X, p.Y));
                return min + (max - min) * Math.Clamp(local.X / Math.Max(1, capa.ActualWidth), 0, 1);
            }
            catch { return v; }
        }

        gesto.Apuntar = m => Log.Info(m);
        gesto.SoltarCaptura = () => { try { if (capa.IsMouseCaptured) capa.ReleaseMouseCapture(); } catch { } };
        gesto.DejarDeSeguir = () =>
        {
            if (porFotograma == null) return;
            CompositionTarget.Rendering -= porFotograma;
            porFotograma = null;
            // La apariencia vuelve a la normal: el agarre deja de estar "cogido".
            agarre.Width = agarre.Height = 14;
            alSoltar?.Invoke(v);
        };

        capa.MouseLeftButtonDown += (_, e) =>
        {
            gesto.Empezar();
            capa.CaptureMouse();
            agarre.Width = agarre.Height = 17;       // se nota que lo tienes cogido
            v = DeLaPantalla();
            Pintar();
            alArrastrar(v);

            porFotograma = (_, _) =>
            {
                // Lo único que decide si el gesto sigue vivo.
                if (!gesto.Fotograma(Nativo.BotonIzquierdoPulsado())) return;
                double nv = DeLaPantalla();
                if (Math.Abs(nv - v) < 0.001) return;    // sin cambio, sin trabajo
                v = nv;
                Pintar();
                alArrastrar(v);
            };
            CompositionTarget.Rendering += porFotograma;
            e.Handled = true;
        };

        // Las otras cinco salidas. Todas van al mismo sitio y limpiar es idempotente.
        capa.MouseLeftButtonUp += (_, _) => gesto.Terminar("soltado");
        capa.LostMouseCapture += (_, _) => gesto.Terminar("se perdió la captura");
        capa.Unloaded += (_, _) => gesto.Cancelar("se cerró el panel");
        capa.KeyDown += (_, e) => { if (e.Key == Key.Escape) gesto.Cancelar("Esc"); };

        // Y si la ventana pierde el foco. Se desuscribe al descargarse la capa para no
        // dejar un oyente colgado de la ventana por cada deslizador que se haya creado.
        void AlPerderFoco(object? s, EventArgs e) => gesto.Cancelar("la ventana perdió el foco");
        Deactivated += AlPerderFoco;
        capa.Unloaded += (_, _) => Deactivated -= AlPerderFoco;

        // Flechas y foco: un panel que solo se puede usar con el ratón no es accesible.
        var boton = new Button
        {
            Content = capa,
            Template = PlantillaDeFila(),
            Focusable = true,
            Padding = new Thickness(0),
        };
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            System.Windows.Automation.AutomationProperties.SetName(boton, nombre);
            System.Windows.Automation.AutomationProperties.SetHelpText(
                boton, "Flechas para ajustar, Esc para cancelar");
        }
        boton.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { gesto.Cancelar("Esc"); return; }
            double paso = (max - min) / 20;
            double nv = e.Key switch
            {
                Key.Left or Key.Down => v - paso,
                Key.Right or Key.Up => v + paso,
                Key.Home => min,
                Key.End => max,
                _ => v,
            };
            if (Math.Abs(nv - v) < 0.0001) return;
            v = Math.Clamp(nv, min, max);
            Pintar();
            alArrastrar(v);
            alSoltar?.Invoke(v);
            e.Handled = true;
        };

        mando.Visual = boton;
        mando.Gesto = gesto;
        return mando;
    }

    // ================= MetricCard =================
    //
    // Una métrica: el número grande, su etiqueta y una tira con el histórico. El estado
    // sale del propio valor, no de un color escrito a mano: normal, alto y crítico usan
    // los roles semánticos, que es lo que hace que un "crítico" siga siendo rojo en
    // Gruvbox.
    internal enum EstadoMetrica { Normal, Alto, Critico }

    internal static EstadoMetrica EstadoDe(double porCiento) =>
        porCiento >= 90 ? EstadoMetrica.Critico
        : porCiento >= 70 ? EstadoMetrica.Alto
        : EstadoMetrica.Normal;

    Color ColorDeEstado(EstadoMetrica e) => e switch
    {
        EstadoMetrica.Critico => _roles.Negativo,
        EstadoMetrica.Alto => _roles.Aviso,
        _ => _roles.Acento,
    };

    internal sealed class MandoMetrica
    {
        public FrameworkElement Visual = null!;
        public Action<double, IReadOnlyList<double>>? Poner;
    }

    // ================= El cuadro de métrica con anillo =================
    //
    // Lo que pide la maqueta para «Sistema»: tres cuadros —CPU, memoria, disco— cada uno con
    // un **anillo**, el porcentaje grande al lado y una gráfica debajo.
    //
    // ---------- Por qué un anillo y no la barra de antes ----------
    //
    // Tres anillos se leen de un vistazo: lleno, medio, vacío. Tres números sueltos obligan a
    // compararlos leyendo, y tres barras horizontales a mirar dónde acaba cada una.
    //
    // La geometría está en `Anillo`, aparte y probada: las dos banderas del arco de WPF son
    // fáciles de poner al revés y el fallo solo aparece pasada la mitad.
    internal MandoMetrica MetricRing(string etiqueta, int icono, double porCiento,
                                     IReadOnlyList<double> historico, string? detalle = null)
    {
        var mando = new MandoMetrica();
        // 26 y no 34: en el panel lateral estos tres cuadros miden unos 92 px, y con un anillo
        // de 34 mas el numero de 17 el porcentaje se cortaba — salia «13⁹». Lo midio una
        // captura, que es la unica forma de ver que algo se corta.
        const double lado = 26, grosor = 3;
        double r = (lado - grosor) / 2, c = lado / 2;

        var fondo = new System.Windows.Shapes.Ellipse
        {
            Width = lado - grosor, Height = lado - grosor,
            Stroke = PincelSolido(_roles.SuperficieAlta),
            StrokeThickness = grosor,
        };
        var arco = new System.Windows.Shapes.Path
        {
            StrokeThickness = grosor,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };

        var aro = new Grid { Width = lado, Height = lado, VerticalAlignment = VerticalAlignment.Center };
        aro.Children.Add(fondo);
        aro.Children.Add(arco);

        var numero = new TextBlock
        {
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 0, 0, 0),
        };

        var sub = new TextBlock
        {
            Text = detalle ?? "", FontSize = 9.5, Foreground = _textoAtenuado,
            Opacity = 0.8, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 4, 0, 0),
        };

        var grafica = new Polyline
        {
            StrokeThickness = 1.4, Height = 18, StrokeLineJoin = PenLineJoin.Round,
        };
        var hueco = new Grid { Height = 18, Margin = new Thickness(0, 5, 0, 0), ClipToBounds = true };
        hueco.Children.Add(grafica);

        void Dibujar(double pc, IReadOnlyList<double> hist)
        {
            var color = PincelSolido(ColorDeEstado(EstadoDe(pc)));
            numero.Text = $"{Math.Round(pc)}%";
            numero.Foreground = color;
            arco.Stroke = color;
            grafica.Stroke = color;

            // El anillo. Lleno se dibuja como circunferencia entera: un arco de 360° es
            // ambiguo para WPF y no pinta nada, que es lo contrario de lo que se quiere ver.
            double parte = Anillo.Parte(pc);
            if (Anillo.Vacio(parte)) arco.Data = null;
            else if (Anillo.Lleno(parte))
                arco.Data = new EllipseGeometry(new Point(c, c), r, r);
            else
            {
                var (x0, y0) = Anillo.Punto(c, c, r, 0);
                var (x1, y1) = Anillo.Punto(c, c, r, parte);
                var fig = new PathFigure { StartPoint = new Point(x0, y0) };
                fig.Segments.Add(new ArcSegment(
                    new Point(x1, y1), new Size(r, r), 0,
                    Anillo.PorElLadoLargo(parte), SweepDirection.Clockwise, true));
                arco.Data = new PathGeometry(new[] { fig });
            }

            double w = Math.Max(10, hueco.ActualWidth > 1 ? hueco.ActualWidth : 90);
            var pts = new PointCollection();
            int n = hist.Count;
            if (n >= 2)
                for (int i = 0; i < n; i++)
                    pts.Add(new Point(w * i / (n - 1.0),
                                      16 - 14 * Math.Clamp(hist[i], 0, 100) / 100.0));
            grafica.Points = pts;
        }
        hueco.SizeChanged += (_, _) => Dibujar(porCiento, historico);
        mando.Poner = (pc, hist) => Dibujar(pc, hist);

        var arriba = new Grid();
        arriba.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        arriba.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(aro, 0); arriba.Children.Add(aro);
        Grid.SetColumn(numero, 1); arriba.Children.Add(numero);

        var col = new StackPanel();
        col.Children.Add(SectionHeader(icono, etiqueta));
        col.Children.Add(arriba);
        if (detalle != null) col.Children.Add(sub);
        col.Children.Add(hueco);

        var card = Card(col, dentro: 9);
        Dibujar(porCiento, historico);
        mando.Visual = card;
        return mando;
    }

    internal MandoMetrica MetricCard(string etiqueta, int icono, double porCiento,
                                     IReadOnlyList<double> historico, string? detalle = null)
    {
        var mando = new MandoMetrica();
        var estado = EstadoDe(porCiento);

        var numero = new TextBlock
        {
            Text = $"{Math.Round(porCiento)}%",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Foreground = PincelSolido(ColorDeEstado(estado)),
        };
        // ---------- Sin repetir la etiqueta ----------
        //
        // Aquí había un `rotulo` con la MISMA etiqueta que ya pone la cabecera, puesto debajo
        // del número. La tarjeta salía «CPU … 13 % … CPU». Se ve en cuanto hay dos juntas, y
        // nadie lo había mirado porque el centro de control las tiene separadas.
        //
        // Lo que va debajo del número es `detalle`, cuando lo hay: algo que la cabecera no
        // dice ya.
        var sub = new TextBlock
        {
            Text = detalle ?? "", FontSize = 10, Foreground = _textoAtenuado,
            Opacity = 0.75, TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var grafica = new Polyline
        {
            Stroke = PincelSolido(ColorDeEstado(estado)),
            StrokeThickness = 1.6,
            Height = 26,
            StrokeLineJoin = PenLineJoin.Round,
        };
        var hueco = new Grid { Height = 26, Margin = new Thickness(0, 6, 0, 0), ClipToBounds = true };
        hueco.Children.Add(grafica);

        void Dibujar(double pc, IReadOnlyList<double> hist)
        {
            var est = EstadoDe(pc);
            var color = PincelSolido(ColorDeEstado(est));
            numero.Text = $"{Math.Round(pc)}%";
            numero.Foreground = color;
            grafica.Stroke = color;

            // La gráfica se redibuja SOLO aquí, con los puntos que le dan. No hay reloj
            // propio ni se repinta el panel entero: eso es lo que hacía que parpadeara.
            double w = Math.Max(10, hueco.ActualWidth > 1 ? hueco.ActualWidth : 120);
            var pts = new PointCollection();
            int n = hist.Count;
            if (n >= 2)
                for (int i = 0; i < n; i++)
                {
                    double x = w * i / (n - 1.0);
                    double y = 24 - 22 * Math.Clamp(hist[i], 0, 100) / 100.0;
                    pts.Add(new Point(x, y));
                }
            grafica.Points = pts;
        }
        hueco.SizeChanged += (_, _) => Dibujar(porCiento, historico);
        mando.Poner = (pc, hist) => Dibujar(pc, hist);

        var col = new StackPanel();
        col.Children.Add(SectionHeader(icono, etiqueta));
        col.Children.Add(numero);
        if (detalle != null) col.Children.Add(sub);
        col.Children.Add(hueco);

        var card = Card(col);
        Dibujar(porCiento, historico);
        mando.Visual = card;
        return mando;
    }

    // ================= QuickAction =================
    //
    // Un botón de la cuadrícula de acciones rápidas: icono arriba, etiqueta abajo, y el
    // estado puesto con el velo de selección y el filo del acento — nunca solo con color,
    // que es la regla de accesibilidad que ya seguían los semánticos.
    internal Button QuickAction(int icono, string etiqueta, bool activo, Action alPulsar,
                                string? ayuda = null)
    {
        var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 17;
        ic.Foreground = activo ? _acentoLegible : _texto;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        col.Children.Add(ic);
        col.Children.Add(new TextBlock
        {
            Text = etiqueta,
            FontSize = 10,
            Foreground = activo ? _acentoLegible : _textoAtenuado,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
        });

        var b = new Button
        {
            Content = col,
            Template = PlantillaDeAccion(activo),
            MinHeight = 60,
            Margin = new Thickness(3),
            Cursor = Cursors.Hand,
            ToolTip = ayuda ?? etiqueta,
        };
        // El rótulo ya está escrito debajo del icono, pero para UIA el contenido es un
        // `StackPanel`: hay que decirlo aparte.
        System.Windows.Automation.AutomationProperties.SetName(b, etiqueta);
        b.Click += (_, _) => alPulsar();
        return b;
    }

    // Una acción delicada: igual que `QuickAction` pero con el filo y el icono del rol
    // `Negativo`. Del TEMA, no un rojo escrito a mano — así sigue siendo rojo en Gruvbox
    // y no choca en un tema claro.
    internal Button AccionDelicada(int icono, string etiqueta, Action alPulsar, string? ayuda = null)
    {
        var b = QuickAction(icono, etiqueta, false, alPulsar, ayuda);
        var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 17;
        ic.Foreground = PincelSolido(_roles.NegativoTinta);
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        col.Children.Add(ic);
        col.Children.Add(new TextBlock
        {
            Text = etiqueta, FontSize = 10,
            Foreground = PincelSolido(_roles.NegativoTinta),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            TextAlignment = TextAlignment.Center,
        });
        b.Content = col;
        return b;
    }

    // La cuadrícula que las agrupa. Columnas fijas para que no bailen de ancho al cambiar
    // cuántas hay.
    internal UIElement RejillaDeAcciones(IEnumerable<Button> acciones, int columnas = 4)
    {
        var r = new UniformGrid { Columns = Math.Max(1, columnas) };
        foreach (var a in acciones) r.Children.Add(a);
        return r;
    }

    // ================= StatusBadge =================
    //
    // Una insignia de estado: un punto y una palabra. Sin el punto, un estado dicho solo
    // con color no lo distingue quien no distingue ese color.
    internal UIElement StatusBadge(string texto, bool bien, bool neutro = false)
    {
        var color = neutro ? _roles.TextoAtenuado : bien ? _roles.Positivo : _roles.Aviso;
        var fila = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fila.Children.Add(new Ellipse
        {
            Width = 7, Height = 7,
            Fill = PincelSolido(color),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        fila.Children.Add(new TextBlock
        {
            Text = texto, FontSize = 10.5, Foreground = _textoAtenuado,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return new Border
        {
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 9, 3),
            Child = fila,
        };
    }

    // ================= EmptyState =================
    //
    // El hueco cuando no hay nada. Con icono y una frase que diga qué hacer: un panel
    // vacío sin explicación se lee como un panel roto.
    internal UIElement EmptyState(int icono, string titulo, string? queHacer = null)
    {
        var col = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 18),
        };
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 24;
        ic.Foreground = _textoAtenuado;
        ic.Opacity = 0.65;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        col.Children.Add(ic);
        col.Children.Add(new TextBlock
        {
            Text = titulo, FontSize = 12, Foreground = _texto,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0), TextAlignment = TextAlignment.Center,
        });
        if (!string.IsNullOrWhiteSpace(queHacer))
            col.Children.Add(new TextBlock
            {
                Text = queHacer, FontSize = 10.5, Foreground = _textoAtenuado,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Opacity = 0.8,
            });
        return col;
    }

    // ================= MediaCard =================
    //
    // El reproductor. La portada va recortada en redondo y con `UniformToFill`, que es lo
    // que evita que una carátula cuadrada salga estirada.
    internal UIElement MediaCard(ImageSource? portada, string titulo, string? artista,
                                 string? origen, double progreso,
                                 Action anterior, Action reproducir, Action siguiente,
                                 bool sonando)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            return Card(EmptyState(0xE8D6, "No hay nada sonando",
                                   "Pon música y aparecerá aquí con sus controles"));

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var hueco = new Border
        {
            Width = 52, Height = 52,
            CornerRadius = new CornerRadius(9),
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 11, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (portada != null)
            hueco.Child = new Image { Source = portada, Stretch = Stretch.UniformToFill };
        else
        {
            var ic = Glifo(char.ConvertFromUtf32(0xE8D6));
            ic.FontSize = 20;
            ic.Foreground = _textoAtenuado;
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            hueco.Child = ic;
        }
        Grid.SetColumn(hueco, 0);
        g.Children.Add(hueco);

        var col = new StackPanel();
        col.Children.Add(new TextBlock
        {
            Text = titulo, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            Foreground = _texto, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(artista))
            col.Children.Add(new TextBlock
            {
                Text = artista!, FontSize = 11, Foreground = _textoAtenuado,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        if (!string.IsNullOrWhiteSpace(origen))
            col.Children.Add(new TextBlock
            {
                Text = origen!, FontSize = 9.5, Foreground = _textoAtenuado,
                Opacity = 0.7, Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

        // El progreso: una tira, no un control. Solo informa.
        var pista = new Border
        {
            Height = 4, CornerRadius = new CornerRadius(2),
            Background = _roles.Pincel("musica.progreso.pista"),
            Margin = new Thickness(0, 7, 0, 0),
        };
        var lleno = new Border
        {
            Height = 4, CornerRadius = new CornerRadius(2),
            Background = _roles.Pincel("musica.progreso"),
            HorizontalAlignment = HorizontalAlignment.Left,
            ClipToBounds = true,
        };

        // ---------- El brillo que recorre lo ya reproducido ----------
        //
        // La maqueta: «un brillo blanco que la recorre (2,8 s)». Va DENTRO de la parte llena
        // y recortado por ella, así que el brillo no se sale por delante del avance — si se
        // saliera, estaría diciendo que la canción va por donde no va.
        //
        // Solo mientras suena. Un brillo corriendo en una canción en pausa es movimiento que
        // informa de algo que no está pasando.
        if (sonando && Animacion.Adorno)
        {
            var chispa = new Border
            {
                Width = 34,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Colors.Transparent, 0),
                        new GradientStop(_roles.ColorDe("musica.progreso.brillo"), 0.5),
                        new GradientStop(Colors.Transparent, 1),
                    }, 0),
                IsHitTestVisible = false,
            };
            var corre = new TranslateTransform();
            chispa.RenderTransform = corre;
            lleno.Child = chispa;
            lleno.SizeChanged += (_, _) =>
            {
                corre.BeginAnimation(TranslateTransform.XProperty, null);
                double w = lleno.ActualWidth;
                if (w < 10) return;
                var a = new DoubleAnimation(-34, w, TimeSpan.FromMilliseconds(2800))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                a.Freeze();
                corre.BeginAnimation(TranslateTransform.XProperty, a);
            };
        }

        var pilaProg = new Grid();
        pilaProg.Children.Add(pista);
        pilaProg.Children.Add(lleno);
        pilaProg.SizeChanged += (_, _) =>
            lleno.Width = Math.Max(0, pilaProg.ActualWidth * Math.Clamp(progreso, 0, 1));
        col.Children.Add(pilaProg);

        var mandos = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(-6, 6, 0, 0),
        };
        mandos.Children.Add(BotonIcono(0xE892, "Anterior", anterior));
        mandos.Children.Add(BotonIcono(sonando ? 0xE769 : 0xE768, sonando ? "Pausar" : "Reproducir", reproducir));
        mandos.Children.Add(BotonIcono(0xE893, "Siguiente", siguiente));
        col.Children.Add(mandos);

        Grid.SetColumn(col, 1);
        g.Children.Add(col);
        return Card(g);
    }

    // Un botón que es solo un icono, con su zona pulsable cómoda.
    internal Button BotonIcono(int icono, string ayuda, Action alPulsar, bool activo = false)
    {
        var ic = Glifo(char.ConvertFromUtf32(icono));
        ic.FontSize = 14;
        ic.Foreground = activo ? _acentoLegible : _texto;
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        ic.VerticalAlignment = VerticalAlignment.Center;

        var b = new Button
        {
            Content = ic,
            Template = PlantillaDeAccion(activo),
            Width = AltoTocable, Height = AltoTocable,
            Margin = new Thickness(3, 0, 3, 0),
            Cursor = Cursors.Hand,
            ToolTip = ayuda,
        };
        // Este solo lleva un glifo: sin nombre, un lector lee un carácter del área privada.
        if (!string.IsNullOrWhiteSpace(ayuda))
            System.Windows.Automation.AutomationProperties.SetName(b, ayuda);
        b.Click += (_, _) => alPulsar();
        return b;
    }


    // ================= SearchBox =================
    //
    // La caja de búsqueda: lupa, campo y aspa para limpiar, en una superficie propia con
    // filo. El `TextBox` se le pasa de fuera porque el que ya existe lleva sus oyentes de
    // teclado y su lógica de búsqueda: aquí solo se le pone el marco.
    internal Border SearchBox(TextBox campo, string ayuda, Action? alLimpiar = null)
    {
        campo.Background = Brushes.Transparent;
        campo.Foreground = _texto;
        campo.CaretBrush = _acentoLegible;
        campo.BorderThickness = new Thickness(0);
        campo.VerticalContentAlignment = VerticalAlignment.Center;
        campo.ToolTip = ayuda;

        var lupa = Glifo(char.ConvertFromUtf32(0xE721));
        lupa.FontSize = 13;
        lupa.Foreground = _textoAtenuado;
        lupa.VerticalAlignment = VerticalAlignment.Center;
        lupa.Margin = new Thickness(2, 0, 9, 0);

        var aspa = BotonIcono(0xE711, "Limpiar la búsqueda", () =>
        {
            campo.Text = "";
            campo.Focus();
            alLimpiar?.Invoke();
        });
        aspa.Width = aspa.Height = 26;
        aspa.Visibility = campo.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        campo.TextChanged += (_, _) =>
            aspa.Visibility = campo.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(lupa, 0);
        g.Children.Add(lupa);
        Grid.SetColumn(campo, 1);
        g.Children.Add(campo);
        Grid.SetColumn(aspa, 2);
        g.Children.Add(aspa);

        var marco = SinArrastre(new Border
        {
            Background = PincelSolido(_roles.SuperficieAltaSolida),
            BorderBrush = PincelSolido(_roles.Borde),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 3, 5, 3),
            Child = g,
        });

        // El filo se enciende con el acento cuando el campo tiene el foco: es el
        // indicador de "aquí va lo que escribas".
        campo.GotKeyboardFocus += (_, _) => marco.BorderBrush = PincelSolido(_roles.Acento);
        campo.LostKeyboardFocus += (_, _) => marco.BorderBrush = PincelSolido(_roles.Borde);
        return marco;
    }

    // ================= FilterChip =================
    //
    // Una pastilla de filtro. Elegida lleva el velo de selección Y el filo del acento:
    // nunca solo color, que es la regla de accesibilidad de los semánticos.
    internal Button FilterChip(string texto, bool elegida, Action alPulsar, int? cuantas = null)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(new TextBlock
        {
            Text = texto,
            FontSize = 11,
            Foreground = elegida ? _acentoLegible : _texto,
            FontWeight = elegida ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (cuantas is { } n)
            fila.Children.Add(new TextBlock
            {
                Text = "  " + n,
                FontSize = 10,
                Foreground = _textoAtenuado,
                VerticalAlignment = VerticalAlignment.Center,
            });

        var b = new Button
        {
            Content = fila,
            Template = PlantillaDeChip(elegida),
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(11, 4, 11, 4),
            MinHeight = 26,
            Cursor = Cursors.Hand,
            ToolTip = elegida ? $"{texto} (puesto) — vuelve a pulsar para quitarlo" : "Ver solo " + texto,
        };
        // Y el estado en el NOMBRE, no solo en el color: un chip elegido y uno sin elegir se
        // distinguen por el acento, y el acento no lo oye nadie.
        System.Windows.Automation.AutomationProperties.SetName(
            b, elegida ? $"{texto}, puesto" : texto);
        b.Click += (_, _) => alPulsar();
        return b;
    }

    ControlTemplate PlantillaDeChip(bool elegida)
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty,
            PincelSolido(elegida ? _roles.SeleccionSolida : _roles.SuperficieAltaSolida));
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        marco.SetValue(Border.BorderBrushProperty,
            elegida ? PincelSolido(_roles.Acento) : PincelSolido(_roles.Borde));
        marco.SetBinding(Border.PaddingProperty,
            new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        marco.AppendChild(cp);
        p.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(encima);

        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }

    // ================= Las plantillas =================
    //
    // Dos, y las dos con la misma idea: el filo SIEMPRE puesto y lo que cambia es su
    // color. Con el grosor variable, el contenido se mueve al pasar el ratón.
    // ================= La plantilla de un azulejo que LLENA su celda =================
    //
    // ---------- Por qué hizo falta ----------
    //
    // `EstiloBoton` fija `HorizontalAlignment = Center` en su presentador, así que todo lo que
    // mete uno en un botón se queda del tamaño de su contenido y centrado. Para que una
    // rejilla salga cuadrada había que darle a cada pastilla un ancho explícito, y ese ancho
    // había que calcularlo: ancho del panel, menos márgenes del cuerpo, menos relleno de la
    // tarjeta, menos filo, menos el margen que el propio estilo del botón añade...
    //
    // Esa cuenta falló cuatro veces seguidas, y la última por algo que no se puede calcular
    // desde aquí: la tarjeta mide 308 px y no 312, porque la barra de desplazamiento del
    // panel se lleva cuatro. El usuario lo vio: «se corta por la derecha un poco».
    //
    // La lección es que la cuenta no era mejorable: estaba mal planteada. Un elemento no debe
    // adivinar el sitio que le queda — debe ocuparlo. Con esta plantilla el presentador se
    // estira, el botón llena su celda del `UniformGrid` y la pastilla llena el botón. No hay
    // número que ajustar, y da igual que mañana aparezca una barra de desplazamiento, cambie
    // el relleno del estilo o se toque el ancho del panel.
    //
    // ---------- Y el resaltado deja de asomar ----------
    //
    // De regalo se cae el otro fallo: el marco del ratón tiene el MISMO radio y el MISMO
    // tamaño que la pastilla, así que no puede sobresalir por un lado como hacía antes.
    ControlTemplate PlantillaDeAzulejo(double radio)
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(radio));

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        marco.AppendChild(cp);
        p.VisualTree = marco;

        // El foco de teclado se ve, como en las demás plantillas de la casa: sin esto, la
        // rejilla se puede recorrer con el tabulador pero no se sabe dónde estás.
        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BackgroundProperty,
                                    PincelSolido(_roles.SuperficieAlta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }

    ControlTemplate PlantillaDeFila()
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        marco.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        marco.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        marco.AppendChild(cp);
        p.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BackgroundProperty, PincelSolido(_roles.SuperficieAltaSolida), "marco"));
        p.Triggers.Add(encima);

        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }

    ControlTemplate PlantillaDeAccion(bool activo)
    {
        var p = new ControlTemplate(typeof(Button));
        var marco = new FrameworkElementFactory(typeof(Border), "marco");
        marco.SetValue(Border.BackgroundProperty,
            PincelSolido(activo ? _roles.SeleccionSolida : _roles.SuperficieAltaSolida));
        marco.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        marco.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        marco.SetValue(Border.BorderBrushProperty,
            activo ? PincelSolido(_roles.Acento) : PincelSolido(_roles.Borde));
        marco.SetValue(Border.PaddingProperty, new Thickness(6));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        marco.AppendChild(cp);
        p.VisualTree = marco;

        var encima = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        encima.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(encima);

        var pulsado = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pulsado.Setters.Add(new Setter(Border.BackgroundProperty, PincelSolido(_roles.SeleccionSolida), "marco"));
        p.Triggers.Add(pulsado);

        var foco = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        foco.Setters.Add(new Setter(Border.BorderBrushProperty, PincelSolido(_roles.AcentoTinta), "marco"));
        p.Triggers.Add(foco);
        return p;
    }
}
