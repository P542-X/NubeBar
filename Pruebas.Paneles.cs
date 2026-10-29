using System;
using System.Linq;

namespace NubeBar;

// Que un panel cabe en la pantalla, o se desplaza por dentro. Nunca ni lo uno ni lo otro.
//
// ---------- De dónde sale esto ----------
//
// El panel «Red e Internet» salía con la tarjeta de acciones cortada por abajo: los cinco
// botones existían, estaban bien colocados dentro del panel, y ciento once píxeles de ellos
// caían por debajo del borde de la isla sin ninguna barra de desplazamiento que los
// alcanzara. Un botón que no se puede pulsar.
//
// La causa no se podía comprobar con una prueba —era `DesiredSize` contestándose a sí mismo
// bajo una fila `Star`, y eso necesita WPF midiendo de verdad—. Lo que SÍ se puede fijar
// aquí es la regla que el arreglo tiene que cumplir, en todas las pantallas:
//
//     o el panel cabe entero, o se enciende el desplazamiento interno;
//     y el alto que se le da NUNCA pasa del tope de la pantalla.
//
// Mientras esas dos se cumplan, no puede quedar un control por debajo del borde: o está
// dentro, o se llega a él con la rueda.
public static partial class Pruebas
{
    internal static void CorrerPaneles()
    {
        PanelesElTopeMiraLaPantalla();
        PanelesOCabeOSeDesplaza();
        PanelesEnCadaPantallaYOrientacion();
        RespaldoDeMigracionNuncaSePisa();
        PanelesSeEscalanConLaPantalla();
        IconosOscurosSeReconocen();
        LaBarraDeScrollEsLaNuestra();
        ElHaloDelPanelLateralNoVaEnLoRecortado();
    }

    // ================= El halo del panel lateral =================
    //
    // ---------- De dónde sale esto ----------
    //
    // «En los paneles laterales los bordes redondeados del neón no se ven redondeados, se
    // ven rectangulares» (v1.0.5). El halo estaba enganchado a `VentanaSuperpuesta.Tarjeta`,
    // que tiene `ClipToBounds` —lo necesita: es lo que impide que el contenido asome por las
    // esquinas—, y WPF recorta el resultado del efecto con ese recorte: al RECTÁNGULO de la
    // caja. Un rectángulo de luz alrededor de un panel con las puntas redondas.
    //
    // Es la SEGUNDA vez que pasa. La primera fue la isla, y al arreglarla allí se escribió
    // «los paneles y los laterales sí lo llevan, y ahí se ve bien: sus conchas no recortan»
    // — media verdad, porque la concha de los laterales no es la de `PanelShell`, es esta
    // tarjeta, y sí recorta. Un comentario no impidió la repetición; esta prueba sí.
    //
    // Lo que se exige es la FORMA de la envoltura, no el color de un píxel: que exista una
    // pieza sin recortar donde colgar el efecto, que esté DETRÁS de la tarjeta y no
    // envolviéndola —envolver mete el texto dentro del efecto otra vez, y eso es la
    // borrosidad que se midió en `CapaDelHalo`— y que la tarjeta siga recortando.
    //
    // Va sobre `Envoltura()` y no sobre una ventana de verdad porque `Owner` obliga a que la
    // dueña ya se haya mostrado, y una prueba no abre ventanas.
    static void ElHaloDelPanelLateralNoVaEnLoRecortado()
    {
        Grupo("panel lateral: el halo va en una pieza sin recortar, y detrás");

        var (raiz, aura, tarjeta, cajon) = VentanaSuperpuesta.Envoltura();

        // La regla que se incumplió: el halo necesita algo que NO se recorte.
        Vale(!aura.ClipToBounds, "el aura no recorta: es quien puede llevar el halo");

        // Y la que no se puede perder al arreglarla: el contenido no asoma por las esquinas.
        Vale(tarjeta.ClipToBounds, "la tarjeta sigue recortando su contenido");

        // Detrás, no alrededor. Si el aura envolviera a la tarjeta, el efecto volvería a
        // pintar el texto en una imagen intermedia y los paneles volverían a verse borrosos.
        Vale(!ReferenceEquals(aura.Child, tarjeta), "el aura no envuelve a la tarjeta");
        Vale(aura.Child == null, "el aura no lleva nada escrito dentro que difuminar");
        Igual(2, raiz.Children.Count, "la raíz tiene las dos piezas");
        Igual(0, raiz.Children.IndexOf(aura), "el aura va primero: se pinta detrás");
        Igual(1, raiz.Children.IndexOf(tarjeta), "y la tarjeta encima, opaca y nítida");

        // Y el cajón, donde siempre: dentro de la tarjeta.
        Vale(ReferenceEquals(tarjeta.Child, cajon), "el cajón va dentro de la tarjeta");

        // Un halo no puede comerse los clics del panel que adorna.
        Vale(!aura.IsHitTestVisible, "el aura no recibe ratón");

        // La silueta tiene que seguir a la tarjeta sola. El margen lo mueve `ColocarLateral`
        // en cada colocación —es el aire donde cabe el halo— y las esquinas las cambia el
        // tema; una copia que hay que acordarse de actualizar es una copia que se queda atrás.
        foreach (var (prop, como) in new[]
        {
            ((System.Windows.DependencyProperty)System.Windows.FrameworkElement.MarginProperty, "el margen"),
            (System.Windows.Controls.Border.CornerRadiusProperty, "las esquinas"),
        })
        {
            var enlace = System.Windows.Data.BindingOperations.GetBinding(aura, prop);
            Vale(enlace != null && ReferenceEquals(enlace.Source, tarjeta),
                 $"{como} del aura sigue a la tarjeta por enlace");
        }

        // El fondo NO va enlazado, y es a propósito: el dock y la previsualización usan esta
        // misma clase sin halo, y un relleno heredado les saldría detrás como un rectángulo
        // de su propio color. Lo pone `PintarConchaLateral`, y solo si el estilo brilla.
        Vale(aura.Background == null, "el aura nace sin fondo: lo pone quien quiera halo");
        Vale(System.Windows.Data.BindingOperations.GetBinding(
                 aura, System.Windows.Controls.Border.BackgroundProperty) == null,
             "y el fondo no va enlazado");
    }

    // ================= La barra de desplazamiento propia =================
    //
    // Va en XAML dentro de una cadena: una errata no la ve el compilador, revienta al
    // aplicar el tema y deja la barra de Windows. Aquí se monta de verdad, en los dos
    // sentidos y con temas claros y oscuros.
    static void LaBarraDeScrollEsLaNuestra()
    {
        Grupo("barra de desplazamiento: la nuestra, en vertical y en horizontal");

        foreach (var (tinta, acento) in new[]
        {
            (System.Windows.Media.Colors.White, System.Windows.Media.Color.FromRgb(0x7A, 0xA2, 0xF7)),
            (System.Windows.Media.Colors.Black, System.Windows.Media.Colors.OrangeRed),
        })
        {
            System.Windows.Style? estilo = null;
            try { estilo = BarraDeScroll.Estilo(tinta, acento); }
            catch (Exception ex) { Vale(false, $"el XAML de la barra no se lee: {ex.Message}"); continue; }

            Vale(estilo.TargetType == typeof(System.Windows.Controls.Primitives.ScrollBar),
                 "es un estilo de ScrollBar");

            foreach (var o in new[] { System.Windows.Controls.Orientation.Vertical,
                                      System.Windows.Controls.Orientation.Horizontal })
            {
                var barra = new System.Windows.Controls.Primitives.ScrollBar { Orientation = o, Style = estilo };
                bool montada = false;
                try { montada = barra.ApplyTemplate(); }
                catch (Exception ex) { Vale(false, $"[{o}] la plantilla revienta: {ex.Message}"); continue; }
                Vale(montada && barra.Track != null, $"[{o}] monta su plantilla y tiene carril");
                Vale(barra.Track?.Thumb != null, $"[{o}] y pastilla");
                Igual(o == System.Windows.Controls.Orientation.Vertical, barra.Track?.IsDirectionReversed == true,
                      $"[{o}] y el sentido bueno (arriba es el principio)");
            }
        }
    }

    // ================= El tamaño de los paneles según la pantalla =================
    //
    // En un 1920x1080 al 125 % (864 de alto en unidades de WPF) los paneles salían con las
    // medidas de un 2560x1440 y ocupaban media pantalla.
    static void PanelesSeEscalanConLaPantalla()
    {
        Grupo("paneles: se escalan con el alto de la pantalla");

        Igual(1.0, MainWindow.EscalaDePanelesPura(1440, 2560, 0), "en un 1440 al 100 % salen tal cual");
        Igual(1.0, MainWindow.EscalaDePanelesPura(1080, 1920, 0), "en un 1080 al 100 % también");
        double portatil = MainWindow.EscalaDePanelesPura(864, 1536, 0);
        Vale(portatil < 0.9 && portatil >= MainWindow.EscalaMinima,
             $"en un 1080 al 125 % encogen ({portatil:0.00})");
        double vertical = MainWindow.EscalaDePanelesPura(1920, 1080, 0);
        Vale(vertical < 1.0, $"en un monitor vertical, por estrecho, también ({vertical:0.00})");
        Igual(MainWindow.EscalaMinima, MainWindow.EscalaDePanelesPura(600, 1024, 0),
              "y nunca por debajo del mínimo, que el texto se tiene que leer");
        Igual(0.9, MainWindow.EscalaDePanelesPura(864, 1536, 0.9), "el config.json manda si lo pones");
        Igual(1.5, MainWindow.EscalaDePanelesPura(864, 1536, 7), "pero sin disparates");
    }

    static void IconosOscurosSeReconocen()
    {
        Grupo("iconos: los oscuros se reconocen y los demás no");

        static byte[] Lleno(byte r, byte g, byte b, byte a) =>
            Enumerable.Range(0, 64).SelectMany(_ => new[] { b, g, r, a }).ToArray();

        Vale(Iconos.LuminanciaMedia(Lleno(10, 10, 10, 255)) < Iconos.LuminanciaOscura,
             "un trazo negro es oscuro");
        Vale(Iconos.LuminanciaMedia(Lleno(0, 120, 215, 255)) > Iconos.LuminanciaOscura,
             "el azul de Windows no lo es");
        Vale(Iconos.LuminanciaMedia(Lleno(255, 255, 255, 255)) > Iconos.LuminanciaOscura,
             "el blanco tampoco");
        Vale(Iconos.LuminanciaMedia(Lleno(0, 0, 0, 0)) == null,
             "un icono transparente no se juzga");
    }

    // ================= Y la red de las migraciones =================
    //
    // No va de paneles, pero va de lo mismo: una red de seguridad que existía, se creía
    // puesta y no estaba.
    //
    // Antes de migrar se guarda una copia con la versión de origen en el nombre. Si ese
    // nombre ya estaba cogido —y basta con restaurar una copia vieja y volver a migrar;
    // en una sola sesión pasó tres veces con la misma v3— NO se escribía nada y el registro
    // decía «ya había copia de v3, no se sobrescribe». Suena a que estás cubierto, y lo que
    // tienes es un fichero de otro día con otro contenido.
    static void RespaldoDeMigracionNuncaSePisa()
    {
        Grupo("esquema: cada migración deja su propia copia, y ninguna pisa a la anterior");

        const string cfg = @"C:\x\config.json";
        var ahora = new DateTime(2026, 10, 27, 0, 25, 13);

        // La primera vez, el nombre limpio: es el que se busca a mano cuando hace falta.
        string primera = Esquema.RutaRespaldoQueNoPisa(cfg, 3, _ => false, ahora);
        Igual(@"C:\x\config.json.antes-de-v3.json", primera, "la primera copia lleva el nombre limpio");
        Igual(Esquema.RutaRespaldo(cfg, 3), primera, "que es el de siempre");

        // La segunda, con fecha, y DISTINTA de la primera: eso es lo que antes no pasaba.
        string segunda = Esquema.RutaRespaldoQueNoPisa(cfg, 3, r => r == primera, ahora);
        Vale(segunda != primera, "la segunda no pisa a la primera");
        Vale(segunda.Contains("antes-de-v3-"), $"y lleva la versión de origen ({segunda})");
        Vale(segunda.Contains("20261027"), "y la fecha, para saber de cuándo es");
        Vale(segunda.EndsWith(".json"), "y sigue siendo un .json");

        // Cada versión de origen tiene su serie: migrar de 1 a 4 deja rastro de cada paso.
        foreach (int v in new[] { 1, 2, 3, 4, 17 })
        {
            string r = Esquema.RutaRespaldoQueNoPisa(cfg, v, _ => false, ahora);
            Vale(r.Contains($"antes-de-v{v}."), $"la copia de v{v} se llama por su versión");
        }

        // Y dos migraciones en el mismo segundo desde versiones distintas no chocan.
        string deV2 = Esquema.RutaRespaldoQueNoPisa(cfg, 2, _ => true, ahora);
        string deV3 = Esquema.RutaRespaldoQueNoPisa(cfg, 3, _ => true, ahora);
        Vale(deV2 != deV3, "dos versiones de origen distintas no comparten nombre");
    }

    // Las pantallas de verdad, con su escala. El alto va SIEMPRE en píxeles de diseño: es
    // en lo que mide WPF, y pasarle los físicos daría el doble de sitio del que hay en un
    // monitor al 200 %.
    static readonly (string Nombre, double AltoDip, double Escala)[] PantallasDePanel =
    {
        ("1366x768",            768,  1.0),
        ("1920x1080",          1080,  1.0),
        ("2560x1440",          1440,  1.0),
        ("4K al 150%",         1440,  1.5),   // 2160 físicos
        ("4K al 200%",         1080,  2.0),
        ("1366x768 al 150%",    512,  1.5),   // el caso apretado de verdad
        ("vertical 1080x1920", 1920,  1.0),
        ("vertical al 125%",   1536,  1.25),
    };

    // ================= El tope mira la pantalla =================
    static void PanelesElTopeMiraLaPantalla()
    {
        Grupo("paneles: el tope sale del monitor, la barra y su margen");

        foreach (var (nombre, alto, _) in PantallasDePanel)
            foreach (double barra in new double[] { 30, 38, 44, 56 })
                foreach (double margen in new double[] { 0, 8, 16 })
                {
                    double tope = MainWindow.AltoMaximoDePanel(alto, barra, margen);

                    // Nunca más que la pantalla menos lo que ocupa la barra: si el tope
                    // fuera mayor, la isla crecería fuera del monitor y lo de abajo sería
                    // inalcanzable con el ratón.
                    Vale(tope <= alto - barra || tope == 200,
                         $"[{nombre} barra={barra} margen={margen}] el tope ({tope:0}) " +
                         $"no pasa de la pantalla menos la barra ({alto - barra:0})");

                    // Y nunca cero o negativo: en una pantalla ridícula vale más un panel
                    // que se desplaza que uno de altura imposible.
                    Vale(tope >= 200, $"[{nombre}] y nunca baja de 200 ({tope:0})");

                    // Una barra más gruesa deja MENOS sitio, no más. Parece obvio y es la
                    // forma en que un signo cambiado se escapa.
                    double conBarraGorda = MainWindow.AltoMaximoDePanel(alto, barra + 20, margen);
                    Vale(conBarraGorda <= tope || tope == 200,
                         $"[{nombre}] una barra más gruesa deja menos sitio");
                }

        // El DPI entra por el alto en píxeles de diseño, que es lo correcto: la misma
        // pantalla física al 100 % y al 200 % NO da el mismo tope, porque en píxeles de
        // diseño es media pantalla.
        double cien = MainWindow.AltoMaximoDePanel(2160, 38, 8);
        double doscientos = MainWindow.AltoMaximoDePanel(1080, 38, 8);
        Vale(doscientos < cien,
             $"un 4K al 200 % deja menos sitio de diseño que al 100 % ({doscientos:0} < {cien:0})");
    }

    // ================= O cabe, o se desplaza =================
    static void PanelesOCabeOSeDesplaza()
    {
        Grupo("paneles: o cabe entero o se desplaza por dentro, nunca se corta");

        foreach (double tope in new double[] { 200, 434, 690, 1026, 1362 })
            foreach (double pide in new double[] { 0, 44, 161, 492, 608, 788, 893, 1236, 4000 })
            {
                var (alto, scroll) = MainWindow.ComoCabeElPanel(pide, tope);

                // LA REGLA. Si no se cumple, hay un control por debajo del borde.
                Vale(alto <= tope + 0.001,
                     $"[tope {tope:0}, pide {pide:0}] el alto que se da ({alto:0}) no pasa del tope");
                Vale(scroll || alto >= pide - 0.001,
                     $"[tope {tope:0}, pide {pide:0}] si no hay desplazamiento, cabe entero");
                Vale(!scroll || Math.Abs(alto - tope) < 0.001,
                     $"[tope {tope:0}, pide {pide:0}] y si hay desplazamiento, se usa el tope entero");

                // Y lo que de verdad se quiere decir con las tres de arriba: nada queda
                // fuera sin forma de llegar a ello.
                bool nadaSeQuedaFuera = pide <= alto + 0.001 || scroll;
                Vale(nadaSeQuedaFuera,
                     $"[tope {tope:0}, pide {pide:0}] nada se queda fuera sin desplazamiento");
            }

        // Los casos de los extremos.
        Vale(!MainWindow.ComoCabeElPanel(0, 500).ConScroll, "un panel vacío no pide desplazamiento");
        Vale(!MainWindow.ComoCabeElPanel(500, 500).ConScroll, "justo en el tope, tampoco");
        Vale(MainWindow.ComoCabeElPanel(501, 500).ConScroll, "un píxel más, sí");
        Igual(500.0, MainWindow.ComoCabeElPanel(9999, 500).Alto, "y por mucho que pida, se queda en el tope");
        Igual(0.0, MainWindow.ComoCabeElPanel(-5, 500).Alto, "un alto negativo se queda en cero");
    }

    // ================= En cada pantalla y orientación =================
    static void PanelesEnCadaPantallaYOrientacion()
    {
        Grupo("paneles: los de verdad, en cada pantalla, DPI y posición de barra");

        // Lo que miden los paneles reales, tomado del registro de NubeBar con el panel ya
        // asentado. No son números inventados: son los que salen al abrirlos.
        var paneles = new (string Nombre, double Pide)[]
        {
            ("aviso", 44),
            ("bandeja", 140),
            ("bateria", 161),
            ("notificaciones", 154),
            ("vista", 320),
            ("red", 608),          // el del recorte: 4 tarjetas, con las wifis dentro
            ("audio", 865),
            ("inicio", 1236),      // el más alto, y el que SIEMPRE se desplaza
        };

        foreach (var (pantalla, altoDip, _) in PantallasDePanel)
            foreach (double barra in new double[] { 38, 56 })
                foreach (var (panel, pide) in paneles)
                {
                    double tope = MainWindow.AltoMaximoDePanel(altoDip, barra, 8);
                    var (alto, scroll) = MainWindow.ComoCabeElPanel(pide, tope);

                    // La isla entera —barra más panel— tiene que caber en el monitor. Es la
                    // comprobación que de verdad contesta «¿queda algo fuera de la ventana?».
                    double isla = barra + alto;
                    Vale(isla <= altoDip || tope == 200,
                         $"[{pantalla} barra={barra}] «{panel}» deja la isla en {isla:0} " +
                         $"y la pantalla mide {altoDip:0}");

                    Vale(pide <= alto + 0.001 || scroll,
                         $"[{pantalla} barra={barra}] «{panel}» pide {pide:0}, se le dan {alto:0}" +
                         (scroll ? " y se desplaza" : " SIN desplazamiento"));
                }

        // El panel de red, que es el del fallo, en las pantallas que se pidieron comprobar.
        foreach (var (pantalla, altoDip, escala) in PantallasDePanel)
        {
            double tope = MainWindow.AltoMaximoDePanel(altoDip, 38, 8);
            var (alto, scroll) = MainWindow.ComoCabeElPanel(608, tope);
            Vale(608 <= alto + 0.001 || scroll,
                 $"[{pantalla} al {escala:0.##}x] el panel de red cabe ({alto:0}) o se desplaza ({scroll})");
        }

        // Y en una pantalla pequeña con la barra gruesa, que es donde el tope muerde: tiene
        // que DESPLAZARSE, no cortarse.
        double apretado = MainWindow.AltoMaximoDePanel(512, 56, 8);
        var red = MainWindow.ComoCabeElPanel(608, apretado);
        Vale(red.ConScroll,
             $"en 1366x768 al 150 % con barra de 56, el panel de red se desplaza (tope {apretado:0})");
        Vale(56 + red.Alto <= 512 || apretado == 200,
             "y la isla sigue cabiendo en la pantalla");
    }
}
