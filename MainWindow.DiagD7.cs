using System;
using System.Linq;
using System.Windows;

namespace NubeBar;

// El runner de `--diagd7`. Ver DiagD7.cs para lo que este diagnóstico se compromete a no
// hacer.
public partial class MainWindow
{
    async System.Threading.Tasks.Task CorrerDiagnosticoD7()
    {
        bool activoAntes = _cfg.NubeHypr.Activo;
        string modoAntes = _cfg.NubeHypr.Modo;

        try
        {
            Apunta("=== fiabilidad y modo seguro (D7) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta("la capa va en SIMULACIÓN: no se mueve ninguna ventana en todo el diagnóstico");
            await System.Threading.Tasks.Task.Delay(2500);

            // ================= 1. El estado de partida =================
            Apunta(HyprSalud.Linea(adaptador: "ready", version: _cfg.Version));
            ApuntarInventarioDeHypr("al empezar");
            Apunta($"[D7]  instantánea de ventanas: {GestorVentanas.Todas.Count} ventanas, " +
                   $"última lectura hace {GestorVentanas.EdadDeLaInstantanea.TotalSeconds:0.0} s");
            Apunta($"[D7]  contadores: fallosSeguidos={HyprSalud.FallosSeguidos} " +
                   $"excepcionesSeguidas={HyprSalud.ExcepcionesSeguidas} " +
                   $"totalFallos={HyprSalud.TotalFallos} totalExcepciones={HyprSalud.TotalExcepciones}");
            Apunta($"[D7]  movimientos reales en toda la vida del proceso: {HyprAdaptador.VecesMovida}");

            // ================= 2. El supervisor, con señales de verdad =================
            //
            // Las señales son las de esta máquina ahora mismo; la única que se simula es
            // "la capa encendida", porque de serie está apagada y con la capa apagada el
            // supervisor —correctamente— no supervisa nada.
            var ahoraMismo = new HyprSalud.Señales(
                GestorVentanas.EdadDeLaInstantanea, HyprTeclas.Cuantos, 0,
                HyprSalud.FallosSeguidos, HyprSalud.ExcepcionesSeguidas, CapaEncendida: true);
            Apunta($"[D7]  diagnóstico con las señales de ahora: {HyprSalud.Mirar(ahoraMismo)}");

            // Y las tres fronteras, para que quede en el registro qué haría en cada caso.
            foreach (var (que, s) in new (string, HyprSalud.Señales)[]
            {
                ("4 fallos seguidos", ahoraMismo with { FallosSeguidos = 4 }),
                ("5 fallos seguidos", ahoraMismo with { FallosSeguidos = 5 }),
                ("15 fallos seguidos", ahoraMismo with { FallosSeguidos = 15 }),
                ("instantánea de 40 s", ahoraMismo with { EdadDeLaInstantanea = TimeSpan.FromSeconds(40) }),
                ("instantánea de 4 min", ahoraMismo with { EdadDeLaInstantanea = TimeSpan.FromMinutes(4) }),
                ("5 excepciones seguidas", ahoraMismo with { ExcepcionesSeguidas = 5 }),
                ("la capa apagada y todo mal", ahoraMismo with { CapaEncendida = false, FallosSeguidos = 99 }),
            })
                Apunta($"  {que,-28} -> {HyprSalud.Mirar(s)}");

            // ================= 3. Montarlo de verdad =================
            _cfg.NubeHypr.Activo = true;
            _cfg.NubeHypr.Modo = "simulacion";
            Apunta($"[D7]  capa encendida SOLO en memoria (antes: activo={activoAntes}, modo={modoAntes})");

            AplicarAtajosDeHypr();
            AplicarReglasDeHypr();
            await System.Threading.Tasks.Task.Delay(800);
            ApuntarInventarioDeHypr("con la capa montada");
            Apunta($"[D7]  atajos vivos={HyprTeclas.Cuantos}  reglas escuchando={_escuchandoReglas}");

            // ================= 4. Entrar en modo seguro =================
            //
            // Esto es la prueba de la fase: lo que se apaga y lo que NO.
            int atajosAntes = HyprTeclas.Cuantos;
            int modulosAntes = ContarDescendientes<System.Windows.Controls.Button>(this);

            Apunta("--- se fuerza el modo seguro ---");
            HyprSalud.Entrar("lo pide el diagnóstico --diagd7");
            AplicarAtajosDeHypr();
            AplicarReglasDeHypr();
            QuitarPreviaHypr();
            await System.Threading.Tasks.Task.Delay(800);

            Apunta(HyprSalud.Linea(adaptador: "safe", version: _cfg.Version));
            Apunta($"[D7]  atajos: {atajosAntes} -> {HyprTeclas.Cuantos} " +
                   $"(tienen que quedar 0)");
            Apunta($"[D7]  reglas escuchando: {_escuchandoReglas} (tiene que ser False)");
            Apunta($"[D7]  modo efectivo en modo seguro: " +
                   $"{HyprSalud.ModoConSeguro(true, "aplicar")} (tiene que ser Apagado " +
                   "aunque la configuración dijera «aplicar»)");

            // ---------- Y lo que tiene que seguir vivo ----------
            int modulosAhora = ContarDescendientes<System.Windows.Controls.Button>(this);
            Apunta($"[D7]  la ISLA sigue: botones en el árbol visual {modulosAntes} -> {modulosAhora} " +
                   $"({(modulosAhora >= modulosAntes ? "intacta" : "ATENCIÓN: ha perdido botones")})");
            Apunta($"[D7]  bandeja: {Bandeja.Vivos().Count} iconos  " +
                   $"carrusel y paneles: la ventana sigue visible={IsVisible}");
            Apunta($"[D7]  atajo de la vista general: " +
                   $"{(Nativo.TraducirAtajo(_cfg.Barra.AtajoVistaGeneral ?? "", out _, out _) ? "sigue traducible" : "NO se entiende")}" +
                   "  (el modo seguro no se lo lleva: no es de NubeHypr)");

            Apunta($"[D7]  {DiagD7.Segundos} s en modo seguro. Comprueba a mano que la isla, " +
                   "los paneles y la bandeja responden igual.");
            for (int s = 5; s <= DiagD7.Segundos; s += 5)
            {
                await System.Threading.Tasks.Task.Delay(5000);
                Apunta($"  {s} s en modo seguro: atajos={HyprTeclas.Cuantos} " +
                       $"reglas={_escuchandoReglas} isla visible={IsVisible}");
            }

            // ================= 5. Salir y volver a montar =================
            Apunta("--- se sale del modo seguro ---");
            HyprSalud.Salir();
            AplicarAtajosDeHypr();
            AplicarReglasDeHypr();
            await System.Threading.Tasks.Task.Delay(800);
            Apunta($"[D7]  atajos otra vez vivos={HyprTeclas.Cuantos} " +
                   $"(tendrían que volver a ser {atajosAntes})");
            Apunta($"[D7]  reglas escuchando otra vez={_escuchandoReglas}");
            Apunta(HyprSalud.Linea(adaptador: "ready", version: _cfg.Version));

            // ================= 6. El cierre limpio =================
            Apunta("--- se suelta todo, como en el cierre ---");
            SoltarTodoLoDeHypr();
            var queda = InventarioDeHypr();
            Apunta($"[D7]  ¿queda algo vivo?={(queda.Limpio ? "no, LIMPIO" : "SÍ: " + queda)}");
            Apunta($"[D7]  lo que hay que soltar son {HyprSalud.HayQueSoltar.Length} cosas:");
            foreach (var q in HyprSalud.HayQueSoltar) Apunta($"        {q}");

            // ================= 7. El rescate de la configuración =================
            //
            // SOLO LECTURA sobre el config de verdad: `Informe` lee y cuenta, no escribe, no
            // borra y no sustituye nada. Es lo que diría si el config estuviera roto.
            Apunta("--- el rescate de la configuración (solo lectura) ---");
            foreach (var linea in HyprRescate.Informe(Config.Ruta)) Apunta(linea);

            // ================= 8. Y lo que no puede comprobar este diagnóstico =================
            Apunta("[D7]  PENDIENTE DE TI: reiniciar NubeBar con ventanas organizadas, matar y");
            Apunta("      relanzar una aplicación, desconectar y reconectar un monitor, cambiar el");
            Apunta("      DPI y volver, y abrir y cerrar 100 ventanas a mano. Nada de eso se puede");
            Apunta("      simular desde dentro sin tocar tu escritorio.");
        }
        catch (Exception ex)
        {
            Apunta($"[D7]  EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico de D7", ex);
        }
        finally
        {
            // El modo seguro y la capa vuelven a como estaban, en memoria. El fichero no se
            // ha tocado en ningún momento.
            HyprSalud.Olvidar();
            _cfg.NubeHypr.Activo = activoAntes;
            _cfg.NubeHypr.Modo = modoAntes;
            Apunta($"[D7]  devuelto a activo={activoAntes}, modo={modoAntes}, modo seguro=no " +
                   "(todo en memoria; el fichero no se tocó)");
            Application.Current.Shutdown(0);
        }
    }
}
