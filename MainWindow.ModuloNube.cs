using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// E1: el módulo del estado de la nube.
//
// ---------- Por qué hacía falta ----------
//
// La barra enseña el estado de todo —red, audio, batería, notificaciones— menos el de la
// pieza que organiza tus ventanas. Para saber si NubeHypr está apagado, simulando o aplicando
// hay que abrir Ajustes, y es justo el dato que conviene tener delante: la diferencia entre
// «mis reglas no funcionan» y «mis reglas están en simulación» es un vistazo.
//
// ---------- Por qué la nube de verdad y no un glifo ----------
//
// Porque es la marca de la casa y porque `Nubecita` es geometría, no una fuente: no se
// convierte en una caja vacía el día que falte un tipo de letra. Y porque el estado se puede
// contar con el COLOR de la propia nube en vez de con un punto al lado, que sería un segundo
// elemento para decir lo mismo.
//
// Pequeña a propósito. El documento de E lo dice: protagonista, sí; mascota gigante que tapa
// el escritorio, no. El tamaño sale de `HyprPinta.TamanoDeLaNube`, que es quien impone los
// topes desde D6.
public partial class MainWindow
{
    UIElement ModuloNube()
    {
        // El lado de la nube: el grosor de la barra menos el aire, con los topes de D6. En
        // una barra de 38 px da una nube de 16-20; en una de 56, algo más. Nunca más grande
        // que eso.
        double lado = HyprPinta.TamanoDeLaNube(Math.Max(16, Grosor - 16));

        var nube = new System.Windows.Shapes.Path
        {
            Data = Nubecita.Molde(),
            Stretch = Stretch.Uniform,
            Width = lado,
            Height = lado,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var boton = Boton(nube, "NubeHypr", () => AbrirAjustesEnNubeHypr());

        // Se repinta con el resto de la barra, no con un reloj propio: el estado cambia
        // cuando cambias el modo o cuando el supervisor entra en modo seguro, no cada
        // segundo. Un temporizador más para esto sería trabajo para no enseñar nada nuevo.
        _actualizadores.Add(() => PintarLaNubeDelModulo(nube, boton));
        PintarLaNubeDelModulo(nube, boton);
        return boton;
    }

    // ---------- Los cuatro estados, contados con el color ----------
    //
    // Y con el tooltip, que es donde cabe la frase entera. El color dice «hay algo distinto»
    // de un vistazo; el tooltip dice qué.
    void PintarLaNubeDelModulo(System.Windows.Shapes.Path nube, FrameworkElement boton)
    {
        try
        {
            bool seguro = HyprSalud.Seguro;
            var modo = HyprSalud.ModoConSeguro(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo);

            // Apagada: el color del texto atenuado, como cualquier icono inactivo de la
            // barra. No en coral: estar apagada no es un aviso, es lo normal.
            // Simulación: azul, que es «esto está funcionando».
            // Aplicando: coral, porque es el único estado que de verdad toca tus ventanas.
            // Modo seguro: coral también, porque algo ha ido mal.
            var (color, que) = (seguro, modo) switch
            {
                (true, _) => (HyprPinta.Coral(_roles),
                              "NubeHypr en MODO SEGURO: ha dejado de organizar porque algo iba mal"),
                (_, NubeHypr.Modo.Apagado) => (_roles.TextoAtenuado,
                              "NubeHypr está apagado. Clic para abrir sus ajustes"),
                (_, NubeHypr.Modo.Aplicar) => (HyprPinta.Coral(_roles),
                              "NubeHypr está APLICANDO: los atajos mueven ventanas de verdad"),
                _ => (HyprPinta.Azul(_roles),
                              "NubeHypr en simulación: decide y lo apunta, pero no mueve nada"),
            };

            nube.Fill = new SolidColorBrush(color);
            // Apagada se ve, pero discreta: no puede competir con lo que sí está pasando.
            nube.Opacity = modo == NubeHypr.Modo.Apagado && !seguro ? 0.55 : 1.0;
            boton.ToolTip = que;
            System.Windows.Automation.AutomationProperties.SetName(boton, que);
        }
        catch (Exception ex) { Log.Error("pintar el módulo de la nube", ex); }
    }

    // Abrir Ajustes directamente donde importa. Un icono de estado que abre una ventana de
    // ajustes por su primera página obliga a buscar, y lo que quieres es lo que el icono
    // estaba contando.
    internal void AbrirAjustesEnNubeHypr() => AbrirAjustesEn("nubehypr");

    // Lo de arriba, generalizado. Era este mismo cuerpo con "nubehypr" escrito dentro, y en
    // cuanto hizo falta una segunda página —la del clima, desde la cabecera de «Clima y
    // sistema»— lo que tocaba era un parámetro y no una segunda copia: dos copias de «abre o
    // enfoca la ventana y vete a esta página» divergen el día que una de las dos aprenda algo.
    internal void AbrirAjustesEn(string pagina)
    {
        try
        {
            if (VentanaAjustes.Abierta is { } ya) { ya.Activate(); ya.Ir(pagina); return; }
            var v = new VentanaAjustes();
            v.Show();
            v.Activate();
            v.Ir(pagina);
        }
        catch (Exception ex) { Log.Error($"abrir Ajustes en {pagina}", ex); }
    }
}
