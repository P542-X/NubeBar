using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NubeBar;

// El módulo "escritorios": los numeritos de los workspaces, como en Linux.
public partial class MainWindow : Window
{
    StackPanel? _escritorios;

    UIElement ModuloEscritorios()
    {
        // ---------- E1: en vertical, los escritorios se APILAN ----------
        //
        // La orientación estaba escrita a mano en horizontal, y en una barra vertical eso
        // ponía los números uno al lado de otro: con dos escritorios, el «2» se salía de la
        // columna y se dibujaba **fuera de la superficie de la barra**, sobre el escritorio.
        //
        // No lo había cazado nadie porque la configuración de este equipo no lleva el módulo
        // de escritorios, y la única captura vertical anterior —la de D6.1— lo añadía estando
        // en horizontal. Lo vio la captura de la composición vertical de E1.
        _escritorios = new StackPanel
        {
            Orientation = Vertical ? Orientation.Vertical : Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent     // sin fondo no llega la rueda a los huecos
        };

        // ---------- Y la huella se olvida, porque describía el panel de ANTES ----------
        //
        // Aquí estaba el fallo de «añado escritorios, dice que se ha añadido, y no aparece».
        //
        // `RefrescarEscritorios` no repinta si la huella de lo que tocaría dibujar es la
        // misma que la última vez. Eso es correcto y es lo que mantiene la barra quieta. El
        // problema es que la huella vive en un campo de `MainWindow` y el StackPanel se tira
        // y se vuelve a crear en CADA remonte —y remontar es lo que pasa al añadir cualquier
        // módulo, al entrar o salir de edición y al tocar cualquier ajuste—. Con los mismos
        // escritorios de siempre, la huella no cambia: el panel recién nacido preguntaba
        // «¿hay algo que pintar?», la huella del panel muerto contestaba que no, y se quedaba
        // vacío. Cero por cero píxeles, dentro de la barra, con todo el modelo diciendo que
        // el módulo estaba puesto.
        //
        // Al arrancar funcionaba porque la huella empieza vacía. Por eso «reinicia NubeBar»
        // lo arreglaba y nada más lo arreglaba.
        //
        // La regla, para el siguiente módulo que lleve caché de repintado: **una huella
        // describe un elemento concreto; si recreas el elemento, la huella ya no vale**.
        _huellaEscritorios = "";

        if (!Escritorios.Disponible) return _escritorios;   // Windows que no reconoce: ni se pinta

        // La rueda pasa al siguiente o al anterior, como en los workspaces de Linux
        _escritorios.MouseWheel += (_, e) =>
        {
            Escritorios.Mover(e.Delta < 0);
            e.Handled = true;
            Dispatcher.BeginInvoke(new Action(RefrescarEscritorios),
                                   System.Windows.Threading.DispatcherPriority.Background);
        };

        RefrescarEscritorios();
        _actualizadores.Add(RefrescarEscritorios);
        return _escritorios;
    }

    string _huellaEscritorios = "";

    void RefrescarEscritorios()
    {
        if (_escritorios == null || !Escritorios.Disponible) return;
        try
        {
            var todos = Escritorios.Todos();
            var fichas = FichasDeEscritorios(todos);
            var talla = HyprIndicador.TallaQueCabe(SitioParaElIndicador(), Vertical);

            // ---------- Repintar solo cuando cambia lo que se dibuja ----------
            //
            // Antes se comparaban dos cosas: cuántos escritorios hay y cuál es el actual.
            // Con el nombre y el número de ventanas eso ya no basta — renombrar un
            // escritorio no cambia ninguna de las dos, y el indicador se quedaría
            // mintiendo hasta que cambiara el número de escritorios.
            //
            // Y tampoco puede entrar todo: con el título de las ventanas dentro, cada
            // pulsación de tecla en el navegador repintaría la isla. La huella la decide
            // `HyprIndicador`, y hay pruebas de qué entra y qué no.
            var huella = HyprIndicador.Huella(fichas, talla);
            if (!HyprVista.TocaRepintar(_huellaEscritorios, huella)) return;
            _huellaEscritorios = huella;

            // E6: la nube marca el cambio de escritorio. Va aquí, después de la huella, para
            // que solo salte cuando lo dibujado ha cambiado de verdad.
            var elActual = fichas.FirstOrDefault(f => f.EsElActual);
            if (elActual != null) NubeAlCambiarDeEscritorio(elActual.Numero);

            _escritorios.Children.Clear();

            // ---------- El rótulo del actual ----------
            //
            // Va delante de los números y es lo nuevo de D6: "2/4 · Trabajo". Con poco
            // sitio se cae solo (lo decide `TallaQueCabe`), y en vertical no sale nunca
            // porque una columna de 30 píxeles no tiene dónde ponerlo.
            var actual = fichas.FirstOrDefault(f => f.EsElActual);
            if (actual != null && talla is HyprIndicador.Talla.Completo or HyprIndicador.Talla.Medio)
                _escritorios.Children.Add(RotuloDelEscritorio(actual, fichas.Count, talla));

            foreach (var d in todos)
            {
                var f = fichas.FirstOrDefault(x => x.Numero == d.Numero)
                        ?? new HyprIndicador.Ficha(d.Numero, d.Nombre, d.EsElActual, 0);
                var b = Boton(NumeroEscritorio(d, f), HyprIndicador.ParaLeer(f, fichas.Count),
                              () => Escritorios.Ir(d.Id));
                b.Width = 22;
                b.Padding = new Thickness(0);
                System.Windows.Automation.AutomationProperties.SetName(
                    b, HyprIndicador.ParaLeer(f, fichas.Count));
                _escritorios.Children.Add(b);
            }

            var mas = Boton(Texto("+", 13), "Escritorio nuevo", () =>
            {
                Escritorios.Crear();
                RefrescarEscritorios();
            });
            mas.Width = 22;
            mas.Padding = new Thickness(0);
            mas.Opacity = 0.5;
            _escritorios.Children.Add(mas);

            AjustarAncho();
        }
        catch (Exception ex) { Log.Error("pintar los escritorios", ex); }
    }

    // ---------- El actual, en un cuadrado azul RELLENO ----------
    //
    // Es lo que pide la maqueta: «el actual en un cuadrado azul relleno, no en texto de
    // acento». Y al hacerlo se cae sola una cosa que estaba mal: el actual se decía TRES
    // veces —número azul, seminegrita, y una rayita azul debajo—. Tres marcas para un dato
    // binario es de lo que hace que una barra parezca recargada sin que uno sepa señalar por
    // qué. El relleno lo dice entero y de un vistazo, así que la rayita sobra.
    //
    // El azul no es el acento del tema: en NubeHypr el azul significa «aquí estás» en los
    // siete temas, y si fuera el acento, en el tema Nube «aquí estás» saldría coral — que es
    // el color de los avisos. Ver `HyprPinta`.
    //
    // El número de encima NO se pinta de un color fijo. Un blanco a pelo sobre un azul claro
    // —y hay temas claros— se lee mal; `Contraste.Legible` da la versión del color de texto
    // que de verdad contrasta sobre ese azul, que es exactamente para lo que existe.
    //
    // Y el puntito de estado sigue yendo por TAMAÑO, no por color: el color ya lo ocupa
    // «cuál es el actual», y dos informaciones en el mismo canal no se distinguen.
    UIElement NumeroEscritorio(Escritorios.Info d, HyprIndicador.Ficha f)
    {
        var azul = HyprPinta.Azul(_roles);
        var tintaEncima = Contraste.Legible(_roles.Texto, azul);

        var fila = new Grid { HorizontalAlignment = HorizontalAlignment.Center };

        // El cuadrado existe SIEMPRE y lo que cambia es si está relleno. Creándolo solo para
        // el actual, la caja del número cambiaría de tamaño al cambiar de escritorio y los
        // demás números darían un salto lateral — que se nota bastante más que el cambio.
        fila.Children.Add(new Border
        {
            Width = 20, Height = 20,
            CornerRadius = new CornerRadius(6),
            Background = d.EsElActual ? new SolidColorBrush(azul) : Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = d.Numero.ToString(),
                Foreground = d.EsElActual ? new SolidColorBrush(tintaEncima) : _texto,
                Opacity = d.EsElActual ? 1 : 0.45,
                FontSize = 12,
                FontWeight = d.EsElActual ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });

        double punto = HyprIndicador.PuntoDe(f.Como);
        if (punto > 0)
            fila.Children.Add(new Border
            {
                Width = punto, Height = punto,
                CornerRadius = new CornerRadius(punto / 2),
                // Sobre el relleno azul el punto va de la tinta legible, no del azul: con el
                // dibujo de antes el punto del escritorio ACTUAL habría quedado azul sobre
                // azul, invisible justo en el único donde importa.
                Background = new SolidColorBrush(d.EsElActual ? tintaEncima : _roles.Texto),
                Opacity = d.EsElActual ? 0.95 : 0.40,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 1, 0),
            });

        return fila;
    }

    // ---------- El rótulo "2/4 · Trabajo" ----------
    FrameworkElement RotuloDelEscritorio(HyprIndicador.Ficha actual, int total,
                                         HyprIndicador.Talla talla)
    {
        var caja = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
        };

        // La nube, pequeña: es la identidad de NubeHypr y la marca de que esto es suyo. El
        // tamaño lo decide `HyprPinta`, con topes, para que no pueda convertirse en una
        // mascota gigante — es la regla que el documento pide y la que impide que la
        // identidad se coma la barra.
        double lado = HyprPinta.TamanoDeLaNube(_cfg.Barra.Alto);
        lado = Math.Min(lado, 18);           // dentro de la isla manda el alto de la barra
        caja.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Nubecita.Molde(),
            Fill = new SolidColorBrush(HyprPinta.Azul(_roles)),
            Width = lado, Height = lado * Nubecita.Proporcion(),
            Stretch = Stretch.Uniform,
            Opacity = 0.9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            IsHitTestVisible = false,
        });

        caja.Children.Add(new TextBlock
        {
            Text = HyprIndicador.Rotulo(actual, total, talla),
            Foreground = new SolidColorBrush(HyprPinta.AzulTinta(_roles, _roles.FondoOpaco)),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        System.Windows.Automation.AutomationProperties.SetName(
            caja, HyprIndicador.ParaLeer(actual, total));
        return caja;
    }

    // ---------- Cuántas ventanas hay en cada escritorio ----------
    //
    // Se pregunta a `Escritorios.EspacioDe` por cada ventana de la instantánea que ya
    // existe: ni se enumera de nuevo ni se sondea. Si la API no contesta para una ventana,
    // esa no se cuenta — un número de más sería peor que un número de menos.
    //
    // ---------- Y por qué hay una caché aquí ----------
    //
    // `EspacioDe` es una llamada COM al shell. `RefrescarEscritorios` está en la lista de
    // actualizadores, o sea que corre **una vez por segundo**; con veinte ventanas abiertas
    // eso son veinte llamadas COM por segundo para un numerito que casi nunca cambia. Lo
    // encontré revisando el coste de lo que había añadido en D6, no en una medición: a 5
    // ventanas no se nota, y a 50 sí.
    //
    // Dos segundos de caché es el mismo plazo que usa `HyprEspacios.Mapa` desde D4, y por el
    // mismo motivo: mover una ventana de escritorio es un gesto humano, y tardar dos
    // segundos en reflejarlo no se percibe. Lo que sí se percibe es la CPU.
    static readonly TimeSpan CaducaElRecuento = TimeSpan.FromSeconds(2);
    DateTime _cuandoSeContaron = DateTime.MinValue;
    Dictionary<IntPtr, int> _ventanaPorEscritorio = new();

    List<HyprIndicador.Ficha> FichasDeEscritorios(List<Escritorios.Info> todos)
    {
        var mapa = _ventanaPorEscritorio;
        if (DateTime.Now - _cuandoSeContaron > CaducaElRecuento)
        {
            mapa = new Dictionary<IntPtr, int>();
            try
            {
                var porId = new Dictionary<Guid, int>();
                foreach (var d in todos) porId[d.Id] = d.Numero;

                foreach (var w in GestorVentanas.Todas)
                {
                    var id = Escritorios.EspacioDe(w.Hwnd);
                    if (id != Guid.Empty && porId.TryGetValue(id, out int n)) mapa[w.Hwnd] = n;
                }
                _ventanaPorEscritorio = mapa;
                _cuandoSeContaron = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error("contar las ventanas de cada escritorio", ex);
                // Si la API falla se usa el recuento anterior en vez de enseñar ceros: un
                // indicador que parpadea a "vacío" cada vez que el shell tarda es peor que
                // uno que se queda un momento con el número de antes.
                mapa = _ventanaPorEscritorio;
            }
        }

        var espacios = todos
            .Select(d => new HyprEspacios.Espacio(d.Id, d.Nombre, d.Numero - 1, d.EsElActual))
            .ToList();
        return HyprIndicador.Fichas(espacios, mapa);
    }

    // Cuánto sitio hay para el rótulo. En la isla horizontal es lo que queda libre; en
    // vertical, el ancho de la columna. Se pasa a `TallaQueCabe`, que decide cuánta
    // información cabe — y la decisión se puede comprobar sin pintar nada.
    double SitioParaElIndicador()
    {
        if (Vertical) return _cfg.Barra.Alto;      // en vertical, "alto" es el grosor
        double libre = ActualWidth > 0 ? ActualWidth : _monitor.Ancho / Math.Max(1, _monitor.Escala);
        return Math.Max(0, libre * 0.22);            // el indicador no se queda con la isla
    }
}
