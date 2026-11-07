using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// La previsualización del ajuste: el rectángulo que enseña dónde va a caer una ventana
// antes de soltarla.
//
// ---------- Por qué vive aparte ----------
//
// La usan DOS gestos distintos: arrastrar una tarjeta de la vista general y arrastrar una
// ventana de verdad por su barra de título. Mientras estaba metida en el archivo del
// segundo, los dos compartían los mismos campos, y ahí estaba el fallo: cerrar la vista en
// mitad del arrastre de una ventana real llamaba a `PararArrastreDeTarjeta`, que borraba la
// zona y escondía el rectángulo, y al soltar ya no había zona que ajustar — el gesto se
// perdía en silencio.
//
// De ahí el **dueño**: `DuenoPrevia` dice de quién es ahora mismo, y quien no la tiene no
// la puede apagar. Es una ventana sola porque con un solo botón del ratón no hay dos
// gestos a la vez; lo que no se puede es que el otro la toque.
//
// ---------- Y por qué es una VentanaSuperpuesta ----------
//
// Hacía falta algo que se dibuje encima de todo, que no coja el foco y que deje pasar los
// clics de verdad, y eso ya existía: la ventana de los paneles de la barra vertical.
// Montar una segunda ventana flotante con reglas propias habría sido exactamente la deuda
// que la Fase 7 quiere no tener.
public partial class MainWindow
{
    // ---------- De quién es la previsualización ----------
    internal enum DuenoPrevia { Nadie, Tarjeta, Ventana }

    VentanaSuperpuesta? _previa;        // el rectángulo que enseña dónde va a caer
    string? _rotuloPrevia;              // D8: lo último que se pintó, para no rehacerlo igual
    DuenoPrevia _duenoPrevia = DuenoPrevia.Nadie;
    Zona _zonaPrevia = Zona.Ninguna;
    Monitores.Info? _monitorPrevio;     // y en QUÉ PANTALLA lo prometió

    // Si hay algún ajuste en curso, de cualquiera de los dos tipos. La isla lo pregunta
    // para no empezar el suyo encima: con un solo botón no hay dos gestos a la vez, y si
    // el estado dice que sí, el que no empieza es el de la isla.
    public bool AjustandoAlgo => _duenoPrevia != DuenoPrevia.Nadie || ArrastrandoTarjeta ||
                                 _arrastrada != IntPtr.Zero;

    // ---------- Enseñar la promesa ----------
    //
    // El `dueno` no es ceremonia: es lo que impide que el arrastre de una tarjeta y el de
    // una ventana real se apaguen el uno al otro.
    void Poner(DuenoPrevia dueno, Zona z, Monitores.Info? m)
    {
        if (_duenoPrevia != DuenoPrevia.Nadie && _duenoPrevia != dueno) return;
        if (z == _zonaPrevia && _duenoPrevia == dueno) return;

        var antes = _zonaPrevia;
        _zonaPrevia = z;

        if (z == Zona.Ninguna || m == null)
        {
            _monitorPrevio = null;
            QuitarPrevia(dueno);
            return;
        }

        // E5: con el dock descontado. La previsualización tiene que enseñar el hueco de
        // verdad, no uno que al aplicarse deje la ventana debajo del dock.
        var hueco = ZonasAjuste.Colocacion(z, ZonasAjuste.AreaUtilCon(m, EstorbosDeAhora()));
        if (hueco.Vacio) { _monitorPrevio = null; QuitarPrevia(dueno); return; }

        _duenoPrevia = dueno;
        _monitorPrevio = m;
        _previa ??= CrearPrevia();
        _previa.Cajon.Content = CuerpoDeLaPrevia(z);
        _previa.Colocar(hueco.X, hueco.Y, hueco.Ancho, hueco.Alto);
        if (!_previa.IsVisible) _previa.Show();
        // El estilo de "los clics la atraviesan" necesita el hwnd, que no existe hasta el
        // primer Show(). Se pone aquí, y `HacerseAtravesable` es idempotente.
        _previa.HacerseAtravesable();
        _previa.Elevar();

        if (antes != z)
            Log.Info($"arrastre {(dueno == DuenoPrevia.Tarjeta ? "tarjeta" : "ventana")} " +
                     $"destino={ZonasAjuste.Nombre(z)} pantalla={NombreDeMonitor(m)} " +
                     $"hueco={hueco.X},{hueco.Y} {hueco.Ancho}x{hueco.Alto}");
    }

    VentanaSuperpuesta CrearPrevia()
    {
        var v = new VentanaSuperpuesta(this);
        // Que el contenido no cuente para el ratón tampoco: es la mitad de WPF del
        // asunto, y la otra mitad —WS_EX_TRANSPARENT y WM_NCHITTEST— la pone
        // `HacerseAtravesable` en cuanto hay hwnd.
        v.Tarjeta.IsHitTestVisible = false;
        v.Cajon.IsHitTestVisible = false;
        return v;
    }

    FrameworkElement CuerpoDeLaPrevia(Zona z) => CuerpoConFiloDoble(ZonasAjuste.Nombre(z));

    // ================= D6: el filo doble =================
    //
    // ---------- Lo que no funcionaba ----------
    //
    // Antes esto era un `Border` con el acento del tema de 2 px y un velo del 20 % dentro.
    // Sobre un escritorio oscuro se ve perfecto. Sobre una foto clara, un velo del 20 %
    // casi no se distingue —eso ya estaba escrito aquí— y además **el filo tampoco**: el
    // acento de Gruvbox es amarillo, y amarillo sobre una playa no es un borde, es nada.
    // Con Wallpaper Engine moviendo un vídeo, la luminancia de debajo cambia cada
    // fotograma, así que no hay un color que valga para todos.
    //
    // ---------- Y lo que sí ----------
    //
    // Dos líneas, no una: una exterior oscura (o clara si se sabe que el fondo es muy
    // oscuro) y, dentro, la línea azul de "aquí va a caer". Sobre claro se ve la exterior;
    // sobre oscuro, la azul; sobre un vídeo que cambia, siempre una de las dos. Así "el
    // filo visible" no depende de medir un fondo que no se puede medir.
    //
    // Los cuatro seguros de "los clics la atraviesan" siguen intactos y no se tocan aquí:
    // `IsHitTestVisible = false` en cada trozo —esta es la mitad de WPF— y
    // WS_EX_TRANSPARENT + WS_EX_NOACTIVATE + HTTRANSPARENT los pone `HacerseAtravesable`
    // en cuanto hay hwnd. Las pruebas `PreviaNoRecibeClics` y `PreviaNoRobaFoco` lo
    // comprueban; este cambio es de color y de grosor, no de comportamiento.
    FrameworkElement CuerpoConFiloDoble(string rotulo)
    {
        var filo = HyprPinta.FiloDelDestino(_roles, DetrasDeLaPrevia());

        var fuera = new Border
        {
            BorderBrush = Pincel(filo.Exterior),
            BorderThickness = new Thickness(filo.GrosorExterior),
            IsHitTestVisible = false,
        };
        var dentro = new Border
        {
            Background = Pincel(HyprPinta.RellenoDelDestino(_roles)),
            BorderBrush = Pincel(filo.Interior),
            BorderThickness = new Thickness(filo.GrosorInterior),
            IsHitTestVisible = false,
        };
        fuera.Child = dentro;

        var hueco = new Grid { IsHitTestVisible = false };
        dentro.Child = hueco;

        if (rotulo.Length > 0)
        {
            // El rótulo va sobre su propia chapita opaca. Un texto suelto encima del
            // escritorio tiene el mismo problema que el filo —no se sabe qué hay detrás—
            // y aquí sí se puede resolver del todo: se pinta su fondo.
            var chapa = new Border
            {
                Background = Pincel(_roles.SuperficieAltaSolida),
                CornerRadius = new CornerRadius(Math.Min(10, _roles.Radio)),
                Padding = new Thickness(10, 4, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = rotulo,
                    Foreground = Pincel(HyprPinta.AzulTinta(_roles, _roles.SuperficieAltaSolida)),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    IsHitTestVisible = false,
                },
            };
            hueco.Children.Add(chapa);
        }
        return fuera;
    }

    static SolidColorBrush Pincel(Color c)
    {
        var p = new SolidColorBrush(c);
        p.Freeze();                      // congelado: se crea en cada fotograma del arrastre
        return p;
    }

    // Qué hay detrás de la previsualización, si se sabe. Es el color medio del wallpaper
    // cuando Wallpaper Engine está dando uno; en cualquier otro caso, null — y entonces el
    // filo doble responde por sí solo. Devolver un color inventado sería peor que no
    // devolver ninguno: elegiría mal el filo con la confianza de haberlo medido.
    Color? DetrasDeLaPrevia() => _paleta?.MediaReal;

    // Solo el dueño la apaga. Quien no la tiene, no la toca: era el camino por el que
    // cerrar la vista en mitad del arrastre de una ventana real se comía el ajuste.
    void QuitarPrevia(DuenoPrevia dueno)
    {
        if (_duenoPrevia != DuenoPrevia.Nadie && _duenoPrevia != dueno) return;
        _duenoPrevia = DuenoPrevia.Nadie;
        // Se olvida el rótulo pintado: el próximo gesto empieza con el contenido recién
        // hecho y no con el de la vez anterior, que podría ser del otro tipo de arrastre.
        _rotuloPrevia = null;
        if (_previa == null) return;
        try { if (_previa.IsVisible) _previa.Hide(); }
        catch (Exception ex) { Log.Error("quitar la previsualización del ajuste", ex); }
    }

    // Qué pantalla es, para el log. El número que ve el usuario en Ajustes, y su área de
    // trabajo, que es lo que de verdad decide dónde cae el ajuste.
    static string NombreDeMonitor(Monitores.Info? m)
    {
        if (m == null) return "?";
        int i = Monitores.Todos().FindIndex(x => x.Handle == m.Handle);
        return (i < 0 ? "?" : (i + 1).ToString()) + $" ({m.TrabajoX},{m.TrabajoY} {m.TrabajoAncho}x{m.TrabajoAlto})";
    }

    // ================= D2: la previsualización de un destino =================
    //
    // ---------- Por qué reutiliza la ventana de C4 y no hace otra ----------
    //
    // El rectángulo de "aquí va a caer" ya existe para el ajuste a bordes, y ya resuelve
    // la parte difícil: `VentanaSuperpuesta` + `HacerseAtravesable` le ponen
    // WS_EX_TRANSPARENT, WS_EX_NOACTIVATE y la respuesta HTTRANSPARENT a WM_NCHITTEST, y
    // además `IsHitTestVisible = false` en el contenido, que es la mitad de WPF del
    // asunto. Una segunda ventana de previsualización sería una segunda ocasión de
    // olvidarse de uno de esos cuatro.
    //
    // Lo único que falta es poder pedirla con un RECTÁNGULO en vez de con una zona de
    // ajuste, que es lo que D2 necesita: el destino lo calcula el modelo.
    //
    // No recibe clics ni foco, y eso no es una promesa: es lo que comprueban
    // `PreviaNoRecibeClics` y `PreviaNoRobaFoco` en las pruebas.
    void PonerPreviaHypr(NubeHypr.Rect destino, string rotulo)
    {
        if (!destino.Valido) { QuitarPreviaHypr(); return; }

        // El dueño es el mismo token de C4: así el ajuste a bordes y esto no se apagan el
        // uno al otro, que fue el fallo que hubo que arreglar en su día.
        if (_duenoPrevia != DuenoPrevia.Nadie && _duenoPrevia != DuenoPrevia.Ventana) return;
        _duenoPrevia = DuenoPrevia.Ventana;

        _previa ??= CrearPrevia();

        // ---------- El contenido solo se rehace si cambia el rótulo ----------
        //
        // Esto se llama en cada paso del gesto, o sea muchas veces por segundo mientras
        // arrastras. Rehacer el árbol visual en cada paso es construir y tirar seis
        // elementos por fotograma para enseñar exactamente lo mismo — y es la clase de cosa
        // que luego se ve como un parpadeo del rótulo. Lo que sí cambia cada paso es la
        // POSICIÓN, y eso lo hace `Colocar`, que no toca el contenido.
        if (_rotuloPrevia != rotulo || _previa.Cajon.Content == null)
        {
            _rotuloPrevia = rotulo;
            _previa.Cajon.Content = CuerpoDeLaPreviaHypr(rotulo);
        }

        _previa.Colocar((int)Math.Round(destino.X), (int)Math.Round(destino.Y),
                        (int)Math.Round(destino.Ancho), (int)Math.Round(destino.Alto));
        if (!_previa.IsVisible) _previa.Show();
        // Necesita hwnd, que no existe hasta el primer Show(). Es idempotente.
        _previa.HacerseAtravesable();
        _previa.Elevar();
    }

    void QuitarPreviaHypr() => QuitarPrevia(DuenoPrevia.Ventana);

    // El destino de NubeHypr y la zona de ajuste comparten cuerpo desde D6. Eran dos
    // copias del mismo `Border` con el mismo velo y el mismo filo, y con el filo doble
    // serían dos copias de algo con cuatro números que hay que mantener iguales: la que se
    // quedara atrás sería la que el usuario viera en el gesto que menos se prueba.
    FrameworkElement CuerpoDeLaPreviaHypr(string rotulo) => CuerpoConFiloDoble(rotulo);
}
