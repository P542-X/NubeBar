using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// «Qué puedes hacer»: la guía visible de las reglas.
//
// ---------- Por qué va DENTRO de la página y no en un documento ----------
//
// Porque la pregunta se hace delante del formulario. Una guía en un fichero aparte la lee
// quien ya sospecha que existe; una lista de ejemplos encima del editor la ve quien acaba de
// abrir la página sin saber qué pedirle.
//
// Y porque cada ejemplo trae su botón: leer «se puede mandar Discord al escritorio 2» y tener
// que traducirlo a cuatro campos sigue siendo trabajo. Un clic lo rellena.
//
// ---------- Lo que esto NO hace ----------
//
// No guarda. No enciende nada. No mueve ninguna ventana. «Usar este ejemplo» deja una regla
// **apagada** en el formulario, y hasta que no pulses Guardar no toca tu `config.json`.
// Abrir la guía no escribe nada en ningún sitio.
public sealed partial class VentanaAjustes
{
    StackPanel? _cajaGuia;
    bool _guiaAbierta;
    StackPanel? _resultadoPrueba;

    // ================= La sección =================
    UIElement SeccionQuePuedesHacer()
    {
        var raiz = new StackPanel();

        // Las ocho tarjetas, siempre visibles. No van detrás de un desplegable: lo que hay
        // que descubrir no puede estar escondido, que es el motivo de que esta sección
        // exista.
        foreach (var e in HyprGuia.Ejemplos) raiz.Children.Add(TarjetaDeEjemplo(e));

        // Y la guía larga, esa sí plegada: son párrafos para leer una vez, y desplegados
        // empujarían los ejemplos fuera de la pantalla.
        var ver = BotonPlano("Ver guía completa", () =>
        {
            _guiaAbierta = !_guiaAbierta;
            if (_cajaGuia != null) _cajaGuia.Visibility = _guiaAbierta
                ? Visibility.Visible : Visibility.Collapsed;
        }, false);
        ver.Margin = new Thickness(0, 10, 0, 0);
        raiz.Children.Add(ver);

        _cajaGuia = new StackPanel { Visibility = Visibility.Collapsed };
        _cajaGuia.Children.Add(CajaConFilo(HyprGuia.ComoSeResuelvenLosEmpates,
                                           HyprPinta.Azul(_roles)));
        raiz.Children.Add(_cajaGuia);

        return raiz;
    }

    // ================= El rótulo del modo de demostración =================
    //
    // En coral y con la nube al lado. Coral porque es «ojo con esto», que es el papel que
    // tiene en la paleta de D6 desde el principio: no es una acción destructiva, pero sí
    // algo que cambia el significado de todo lo que hay debajo.
    //
    // Y dice las dos mitades: lo que NO va a pasar (nada se guarda) y lo que SÍ se puede
    // hacer. Un aviso que solo prohíbe deja al usuario sin saber para qué está ahí.
    internal FrameworkElement RotuloDeDemostracion()
    {
        var pila = new StackPanel();

        var t = Rotulo(DiagGuia.Rotulo);
        t.FontWeight = FontWeights.SemiBold;
        t.FontSize = 13;
        t.TextWrapping = TextWrapping.Wrap;
        t.Foreground = new SolidColorBrush(
            HyprPinta.CoralTinta(_roles, _roles.SuperficieAltaSolida));
        pila.Children.Add(t);

        var d = Rotulo(
            "Puedes usar los ocho ejemplos, escribir reglas y probarlas. Nada de esto toca " +
            "tu config.json: ni se guarda, ni se migra el esquema, ni se registran atajos " +
            "globales, ni se mueve ninguna ventana. NubeHypr está apagado en memoria aunque " +
            "tu fichero lo tenga encendido.\n\n" +
            "Al cerrar esta ventana no queda rastro de lo que hayas hecho aquí.");
        d.TextWrapping = TextWrapping.Wrap;
        d.FontSize = 11.5;
        d.Opacity = 0.8;
        d.Margin = new Thickness(0, 5, 0, 0);
        pila.Children.Add(d);

        return new Border
        {
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 10, 12, 11),
            BorderThickness = new Thickness(4, 0, 0, 0),
            BorderBrush = new SolidColorBrush(HyprPinta.Coral(_roles)),
            Child = pila,
        };
    }

    // ---------- Una tarjeta de ejemplo ----------
    //
    // Cuatro cosas en este orden: qué consigues, qué campos se rellenan, la frase en
    // castellano, y el botón. El orden importa: el titular dice si te interesa, los campos
    // enseñan el formulario, y la frase es la que se compara con lo que querías.
    FrameworkElement TarjetaDeEjemplo(HyprGuia.Ejemplo e)
    {
        var caja = new Border
        {
            Background = new SolidColorBrush(_roles.Superficie),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(HyprPinta.Azul(_roles)),
        };

        var pila = new StackPanel();

        var t = Rotulo(e.Que);
        t.FontWeight = FontWeights.SemiBold;
        t.TextWrapping = TextWrapping.Wrap;
        pila.Children.Add(t);

        var ex = Rotulo(e.Explicacion);
        ex.FontSize = 11.5;
        ex.Opacity = 0.75;
        ex.TextWrapping = TextWrapping.Wrap;
        ex.Margin = new Thickness(0, 3, 0, 0);
        pila.Children.Add(ex);

        // ---------- Qué campos se rellenan ----------
        pila.Children.Add(Etiquetas(e.Campos));

        // ---------- El ejemplo real, tal cual se escribe ----------
        //
        // Con el valor de cada campo, porque «Proceso» a secas no enseña que se escribe
        // «Discord» y no «Discord.exe». Es media explicación de la mitad de los fallos.
        pila.Children.Add(CajaConFilo(ValoresDe(e.Regla), _roles.Borde, 11));

        // ---------- La frase ----------
        var frase = Rotulo(e.Frase);
        frase.TextWrapping = TextWrapping.Wrap;
        frase.FontSize = 12;
        frase.Margin = new Thickness(0, 8, 0, 0);
        frase.Foreground = new SolidColorBrush(
            HyprPinta.AzulTinta(_roles, _roles.SuperficieSolida));
        pila.Children.Add(frase);

        // ---------- El «ojo con esto» ----------
        if (e.Ojo is { Length: > 0 })
            pila.Children.Add(CajaConFilo(e.Ojo, HyprPinta.Coral(_roles), 11));

        // ---------- Y el botón ----------
        var fila = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0),
        };
        fila.Children.Add(BotonPlano("Usar este ejemplo", () => UsarEjemplo(e), false));

        var nota = Rotulo("se añade apagada y sin guardar");
        nota.FontSize = 10.5;
        nota.Opacity = 0.55;
        nota.VerticalAlignment = VerticalAlignment.Center;
        nota.Margin = new Thickness(8, 0, 0, 0);
        fila.Children.Add(nota);

        pila.Children.Add(fila);
        caja.Child = pila;
        return caja;
    }

    // Los campos, como etiquetas sueltas. Enseñan el formulario antes de abrirlo.
    FrameworkElement Etiquetas(IReadOnlyList<string> campos)
    {
        var envoltura = new WrapPanel { Margin = new Thickness(0, 8, 0, 6) };
        foreach (var c in campos)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(_roles.SuperficieAlta),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 3),
                Margin = new Thickness(0, 0, 5, 5),
            };
            var t = Rotulo(c);
            t.FontSize = 10.5;
            b.Child = t;
            envoltura.Children.Add(b);
        }
        return envoltura;
    }

    static string ValoresDe(HyprReglas.Regla r)
    {
        var l = new List<string>();
        if (r.Proceso != null) l.Add($"Proceso = {r.Proceso}");
        if (r.Clase != null) l.Add($"Clase = {r.Clase}");
        if (r.Titulo != null) l.Add($"Título contiene = {r.Titulo}");
        if (r.Ruta != null) l.Add($"Ruta contiene = {r.Ruta}");
        if (r.Espacio != null) l.Add($"Espacio = {r.Espacio}");
        if (r.Monitor != null) l.Add($"Monitor = {r.Monitor}");
        if (r.Layout != null) l.Add($"Layout = {r.Layout}");
        if (r.Ancho != null) l.Add($"Ancho = {r.Ancho:0.##}");
        if (r.Alto != null) l.Add($"Alto = {r.Alto:0.##}");
        if (r.Flotante != null) l.Add($"Flotante = {(r.Flotante.Value ? "Sí" : "No")}");
        if (r.Foco != null) l.Add($"Darle el foco = {(r.Foco.Value ? "Sí" : "No")}");
        if (r.Prioridad != 0) l.Add($"Prioridad = {r.Prioridad}");
        return string.Join("\n", l);
    }

    FrameworkElement CajaConFilo(string texto, Color filo, double tamano = 11.5)
    {
        var t = Rotulo(texto);
        t.TextWrapping = TextWrapping.Wrap;
        t.FontSize = tamano;
        return new Border
        {
            Background = new SolidColorBrush(_roles.SuperficieAlta),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 7, 10, 8),
            Margin = new Thickness(0, 8, 0, 0),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(filo),
            Child = t,
        };
    }

    // ================= Usar un ejemplo =================
    //
    // Rellena el formulario y nada más. En concreto: la regla nace **apagada**, se abre su
    // editor, y no se guarda nada en disco — como «Añadir una regla», que es el camino que el
    // usuario ya conoce. Si cierras Ajustes sin guardar, no queda rastro.
    void UsarEjemplo(HyprGuia.Ejemplo e)
    {
        try
        {
            var nueva = HyprGuia.ParaUsar(e, Reglas);
            _reglasEditadas = HyprReglas.Poner(Reglas, nueva);
            _editando = nueva.Id;
            CambiaronLasReglas();
            Log.Info($"guía: ejemplo «{e.Que}» puesto en el formulario como «{nueva.Id}» " +
                     "(apagada, sin guardar)");
        }
        catch (Exception ex) { Log.Error("usar un ejemplo de la guía", ex); }
    }

    // ================= Ejercitar la guía entera, para `--capturas` =================
    //
    // Pulsar los ocho botones a mano es una prueba manual; esto recorre **el mismo camino de
    // código** que el botón, uno por uno, y además prueba cada regla resultante. Sin tocar el
    // ratón ni el teclado: se llama al manejador, que es lo que el clic acaba llamando.
    //
    // Lo que de verdad comprueba: que ninguna tarjeta revienta al construirse, que ningún
    // ejemplo deja el formulario en un estado que no se puede pintar, y que probar una regla
    // recién puesta no lanza nada. Un `try` por ejemplo, para que un fallo en el tercero no
    // esconda los cinco siguientes.
    //
    // No guarda: corre con `Config.SoloLectura` y la ventana se cierra sin guardar.
    internal int EjercitarLaGuiaParaLaCaptura()
    {
        int fallos = 0;
        foreach (var e in HyprGuia.Ejemplos)
        {
            try
            {
                UsarEjemplo(e);
                var puesta = Reglas.FirstOrDefault(x => x.Id == _editando);
                if (puesta == null)
                {
                    Log.Error($"guía: el ejemplo «{e.Que}» no dejó ninguna regla",
                              new InvalidOperationException("sin regla tras usar el ejemplo"));
                    fallos++;
                    continue;
                }

                // Y se prueba, que es el otro botón. `BotonDeProbar` crea el hueco donde se
                // escribe el resultado; sin llamarlo antes, `ProbarRegla` no tendría dónde
                // pintar y se callaría — que es justo el fallo que esto busca.
                BotonDeProbar(puesta);
                ProbarRegla(puesta);

                Log.Info($"guía: ejemplo «{e.Que}» -> «{puesta.Id}» puesto y probado, " +
                         $"{HyprGuia.EnPalabras(puesta)}");
            }
            catch (Exception ex)
            {
                Log.Error($"guía: el ejemplo «{e.Que}» REVENTÓ", ex);
                fallos++;
            }
        }

        // La guía larga también se despliega: es otro trozo de árbol visual que solo se
        // construye al pulsar, y lo que no se construye nunca es lo que se rompe sin avisar.
        try
        {
            _guiaAbierta = true;
            if (_cajaGuia != null) _cajaGuia.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { Log.Error("guía: desplegar la guía completa", ex); fallos++; }

        RepintarReglas();

        // Y una última prueba DESPUÉS del repintado. Las de arriba se hacen sobre el
        // formulario de cada momento y el repintado final las borra, así que sin esto no hay
        // forma de retratar el resultado — y el resultado es justo lo que hay que revisar.
        try
        {
            var ultima = Reglas.FirstOrDefault(x => x.Id == _editando);
            if (ultima != null) ProbarRegla(ultima);
        }
        catch (Exception ex) { Log.Error("guía: la prueba final para la captura", ex); fallos++; }

        return fallos;
    }

    // ================= Probar una regla =================
    //
    // Evalúa contra las ventanas abiertas y enseña qué haría. No mueve ni una.
    FrameworkElement BotonDeProbar(HyprReglas.Regla r)
    {
        var pila = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(BotonPlano("Probar regla", () => ProbarRegla(r), false));

        var nota = Rotulo("evalúa las ventanas abiertas; no mueve ninguna");
        nota.FontSize = 10.5;
        nota.Opacity = 0.55;
        nota.VerticalAlignment = VerticalAlignment.Center;
        nota.Margin = new Thickness(8, 0, 0, 0);
        fila.Children.Add(nota);
        pila.Children.Add(fila);

        _resultadoPrueba = new StackPanel();
        pila.Children.Add(_resultadoPrueba);
        return pila;
    }

    void ProbarRegla(HyprReglas.Regla r)
    {
        if (_resultadoPrueba is not { } caja) return;
        caja.Children.Clear();

        try
        {
            // Las ventanas se leen aquí y la decisión se toma en `HyprGuia`, que es puro.
            // Leer es lo único que toca al sistema; nada de esto llama al adaptador.
            var ventanas = new List<HyprGuia.Candidata>();
            var porNombre = new Dictionary<HyprGuia.Candidata, (VentanaModelo V, string Clase, string Exe)>();

            foreach (var w in GestorVentanas.Todas)
            {
                var cand = new HyprGuia.Candidata(
                    w.App.Length > 0 ? w.App : w.Titulo,
                    NubeHypr.NombreDeProceso(w.Exe), w.Titulo);
                ventanas.Add(cand);
                porNombre[cand] = (
                    new VentanaModelo(w.Hwnd, NubeHypr.NombreDeProceso(w.Exe), w.Titulo,
                                      HyprAdaptador.DeRect(w.Rect)),
                    Nativo.Clase(w.Hwnd), w.Exe);
            }

            // Se prueba la regla COMO SI estuviera encendida. Si no, una regla recién creada
            // —que nace apagada— daría siempre cero coincidencias y el botón no serviría
            // para nada hasta encenderla, que es justo lo que no queremos obligar a hacer.
            var comoSiEstuviera = r with { Activa = true };

            var res = HyprGuia.Probar(comoSiEstuviera, ventanas, c =>
            {
                var (v, clase, exe) = porNombre[c];
                return HyprReglas.Coincide(comoSiEstuviera, v, clase, exe, out _);
            });

            var modo = HyprSalud.ModoConSeguro(_hypr.NubeHypr.Activo, _hypr.NubeHypr.Modo);

            caja.Children.Add(CajaConFilo(
                res.Veredicto,
                res.PuedeAplicarse && res.Coinciden.Count > 0
                    ? HyprPinta.Azul(_roles) : HyprPinta.Coral(_roles)));

            foreach (var c in res.Coinciden.Take(8))
            {
                var t = Rotulo("· " + HyprGuia.QueLeHariaA(comoSiEstuviera, c));
                t.TextWrapping = TextWrapping.Wrap;
                t.FontSize = 11.5;
                t.Margin = new Thickness(10, 3, 0, 0);
                caja.Children.Add(t);
            }
            if (res.Coinciden.Count > 8)
            {
                var mas = Rotulo($"…y {res.Coinciden.Count - 8} más");
                mas.FontSize = 11;
                mas.Opacity = 0.6;
                mas.Margin = new Thickness(10, 3, 0, 0);
                caja.Children.Add(mas);
            }

            foreach (var p in res.Problemas)
                caja.Children.Add(CajaConFilo(p, HyprPinta.Coral(_roles), 11));

            // Y el recordatorio, siempre el último y siempre presente: sin él, «coincide con
            // 3 ventanas» se lee como «va a mover 3 ventanas».
            caja.Children.Add(CajaConFilo(HyprGuia.LoQuePasaDeVerdad(modo),
                                          HyprPinta.Coral(_roles), 11));

            if (r.Activa != comoSiEstuviera.Activa)
            {
                var ojo = Rotulo("La regla está apagada; la prueba la ha evaluado como si " +
                                 "estuviera encendida, para que puedas verla funcionar antes " +
                                 "de activarla.");
                ojo.TextWrapping = TextWrapping.Wrap;
                ojo.FontSize = 10.5;
                ojo.Opacity = 0.6;
                ojo.Margin = new Thickness(0, 6, 0, 0);
                caja.Children.Add(ojo);
            }

            Log.Info($"guía: regla «{r.Id}» probada -> {res.Veredicto} (no se ha movido nada)");
        }
        catch (Exception ex)
        {
            Log.Error("probar una regla", ex);
            caja.Children.Add(CajaConFilo(
                "No se ha podido probar la regla. El motivo está en el registro.",
                HyprPinta.Coral(_roles), 11));
        }
    }
}
