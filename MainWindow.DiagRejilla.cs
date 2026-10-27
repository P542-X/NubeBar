using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NubeBar;

// El runner de `--diagrejilla`. Ver DiagRejilla.cs para qué mide y por qué esos números.
public partial class MainWindow
{
    // ---------- Elegir una tarjeta y ver si la ventana viene al frente ----------
    //
    // Devuelve si la comprobación salió bien. Se hace sobre **una ventana nuestra**: traer al
    // frente una del usuario sería moverle el escritorio para probarme a mí.
    //
    // Lo que puede fallar aquí y no se ve de otra forma: que la tarjeta se dibuje bien y el
    // clic no lleve a ninguna parte, o que lleve a la ventana equivocada —que es el fallo de
    // verdad cuando el orden de la rejilla y el de las tarjetas se desincronizan—.
    async Task<bool> ElegirUnaDeLasNuestras(int cuantas)
    {
        try
        {
            // La #1, que es la primera que abrimos y por tanto la más tapada por las demás:
            // si viene al frente esa, el camino funciona.
            var h = Nativo.PorTitulo("NubeBar · rejilla #1");
            if (h == IntPtr.Zero) { Apunta("   --   no se encuentra la ventana #1 para elegirla"); return true; }

            if (!_tarjetas.TryGetValue(h, out var t) || t.Boton == null)
            {
                Apunta("   --   la ventana #1 no tiene tarjeta en la vista");
                return true;
            }

            var antes = Nativo.Activa();
            VentanaAjustes.Pulsar(t.Boton);
            await Task.Delay(1200);
            var despues = Nativo.Activa();

            bool bien = despues == h;
            Apunta($"   {(bien ? "ok  " : "FALLA")} elegir la tarjeta de la ventana #1 la trae al " +
                   $"frente (antes 0x{antes.ToInt64():X6} → después 0x{despues.ToInt64():X6}, " +
                   $"se esperaba 0x{h.ToInt64():X6})");
            return bien;
        }
        catch (Exception ex)
        {
            Apunta($"   FALLA elegir una ventana REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("elegir una ventana desde la vista en --diagrejilla", ex);
            return false;
        }
    }

    // ---------- Una ventana minimizada no puede seguir marcada como «activa» ----------
    //
    // Esto sale de un fallo que contó el usuario: «clico dos veces una misma app desde el
    // dock y ya no me deja volver a abrirla».
    //
    // El dock minimiza cuando pulsas la que ya está delante (`ActivarDesdeElDock`), que es lo
    // que hace cualquier barra de tareas. Pero si la instantánea sigue diciendo que esa
    // ventana está activa **después** de minimizarla, el clic siguiente vuelve a minimizar
    // una ventana ya minimizada — no pasa nada visible, y la app parece atascada.
    //
    // Y puede pasar porque `_activa` es pegajosa a propósito: `GestorVentanas` solo la
    // actualiza cuando la ventana de delante NO es nuestra, para que pulsar en la barra no le
    // haga perder la cuenta. Si al minimizar el foco acaba en NubeBar, `_activa` se queda
    // apuntando a la que acabas de minimizar.
    //
    // Se reproduce con una ventana NUESTRA: se activa, se minimiza y se vuelve a mirar.
    async Task<bool> MinimizadaNoEsActiva()
    {
        Apunta("");
        Apunta("──────── una minimizada no sigue siendo «la activa» ────────");
        try
        {
            var h = Nativo.PorTitulo("NubeBar · rejilla #1");
            if (h == IntPtr.Zero) { Apunta("   --   no hay ventana con la que probarlo"); return true; }

            Nativo.Activar(h);
            await Task.Delay(900);
            GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
            await Task.Delay(500);

            var v = GestorVentanas.Todas.FirstOrDefault(w => w.Hwnd == h);
            bool activaAntes = v?.Activa == true;
            Apunta($"   tras activarla: Activa={activaAntes} Estado={v?.Estado}");

            Nativo.Minimizar(h);
            await Task.Delay(900);
            GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
            await Task.Delay(500);

            v = GestorVentanas.Todas.FirstOrDefault(w => w.Hwnd == h);
            bool minimizada = v?.Estado == EstadoVentana.Minimizada;
            bool sigueActiva = v?.Activa == true;
            Apunta($"   tras minimizarla: Activa={sigueActiva} Estado={v?.Estado}");

            bool bien = minimizada && !sigueActiva;
            Apunta(bien
                ? "   ok   minimizada y ya NO marcada como activa: el dock la restauraría"
                : "   FALLA sigue marcada como activa estando minimizada: el dock la volvería " +
                  "a minimizar en cada clic, y la app parece que no se puede abrir");

            // Y se deja como estaba, que es abierta: no vamos a devolverle la ventana
            // minimizada a nadie.
            Nativo.Activar(h);
            await Task.Delay(400);
            return bien;
        }
        catch (Exception ex)
        {
            Apunta($"   FALLA la prueba de minimizada REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("comprobar si una minimizada sigue activa", ex);
            return false;
        }
    }

    // ---------- Una app escondida en la bandeja se recupera, no se relanza ----------
    //
    // El fallo que contó el usuario: «clico dos veces una misma app desde el dock y ya no me
    // deja volver a abrirla». La causa: Discord, Steam y casi todo lo que vive en la bandeja
    // no se minimizan, **se esconden**. Su ventana deja de ser visible, sale de la lista de
    // ventanas, y NubeBar la daba por cerrada y relanzaba el ejecutable — que no hace nada,
    // porque ya hay un proceso corriendo.
    //
    // Se reproduce con una ventana NUESTRA, escondiéndola igual que lo hacen ellas
    // (`SW_HIDE`), y se comprueba lo que de verdad importa: que `EscondidaDe` la encuentra y
    // que `MostrarYActivar` la devuelve.
    async Task<bool> LaEscondidaSeRecupera()
    {
        Apunta("");
        Apunta("──────── una app escondida se recupera, no se relanza ────────");
        IntPtr h = IntPtr.Zero;
        try
        {
            h = Nativo.PorTitulo("NubeBar · rejilla #2");
            if (h == IntPtr.Zero) { Apunta("   --   no hay ventana con la que probarlo"); return true; }

            string exe = Nativo.Exe(h);
            Apunta($"   ventana 0x{h.ToInt64():X6} de {System.IO.Path.GetFileName(exe)}");

            // Estando visible, no se la toma por escondida.
            Vale2(Nativo.Ficha(h).Visible, "de partida la ventana está visible");

            // Y ahora como hace Discord: esconderla, no minimizarla.
            Nativo.Esconder(h);
            await Task.Delay(900);
            GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
            await Task.Delay(600);

            bool fueraDeLaLista = GestorVentanas.Todas.All(w => w.Hwnd != h);
            Apunta($"   tras esconderla: ¿sale de la lista de ventanas?={(fueraDeLaLista ? "sí" : "NO")}");
            Vale2(fueraDeLaLista,
                  "una escondida desaparece de la lista — por eso NubeBar la daba por cerrada");

            // ---------- Y esto es lo que hace el arreglo ----------
            //
            // No busca cuál de las ventanas invisibles «parece» la buena —eso falló dos veces
            // y encontraba otra del mismo proceso—. Comprueba la que YA conocíamos: sigue
            // existiendo, y ya no está visible. Eso es «escondida», sin adivinar nada.
            bool sigueViva = Nativo.Existe(h);
            bool yaNoVisible = !Nativo.Ficha(h).Visible;
            Apunta($"   la ventana que conocíamos: ¿existe?={sigueViva} ¿visible?={!yaNoVisible}");
            Vale2(sigueViva && yaNoVisible,
                  "la ventana recordada sigue viva y escondida: no está cerrada");

            if (!sigueViva || !yaNoVisible) return false;

            Nativo.MostrarYActivar(h);
            await Task.Delay(1000);
            GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
            await Task.Delay(600);

            bool vuelve = GestorVentanas.Todas.Any(w => w.Hwnd == h);
            Vale2(vuelve, "y al enseñarla vuelve a la lista: el icono la recupera de verdad");
            return vuelve;
        }
        catch (Exception ex)
        {
            Apunta($"   FALLA la prueba de la escondida REVENTÓ: {ex.GetType().Name}: {ex.Message}");
            Log.Error("comprobar la recuperación de una ventana escondida", ex);
            return false;
        }
        finally
        {
            // Pase lo que pase, la ventana se queda visible: esconder una y dejarla escondida
            // sería dejar un proceso sin forma de cerrarse al terminar el diagnóstico.
            if (h != IntPtr.Zero) { try { Nativo.MostrarYActivar(h); } catch { } }
            await Task.Delay(300);
        }
    }

    async Task CorrerDiagnosticoDeRejilla()
    {
        int malos = 0;

        // Fuera del try, como siempre: el `finally` tiene que poder cerrar lo que se abriera
        // aunque la excepción saltara justo después de abrirlo.
        var procesos = new List<System.Diagnostics.Process>();
        var copias = new List<string>();

        try
        {
            Apunta("=== REJILLA: «Ver todas las ventanas» con ventanas de verdad ===");
            Apunta($"ejecutable: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"pantalla: {_monitor.Ancho}x{_monitor.Alto} escala {_monitor.Escala:0.##}");
            Apunta("");
            Apunta("Abre 2, 5, 7 y 12 ventanas PROPIAS, que se suman a las que ya tengas: la");
            Apunta("vista las enseña todas, y cerrarte las tuyas para cuadrar un número no entra");
            Apunta("en lo que un diagnóstico puede hacer. Así que el número que vale es el de");
            Apunta("tarjetas MEDIDAS, que es el que se apunta en cada caso.");
            Apunta("");
            Apunta("Lo que se exige: todas las tarjetas del mismo tamaño, y que la última fila");
            Apunta("no deje más de media fila vacía. Medido sobre lo que WPF dibuja, no sobre");
            Apunta("lo que el reparto pide: ahí es donde se escondían las tarjetas desiguales.");
            await Task.Delay(2500);

            foreach (int n in DiagRejilla.Cuantas)
            {
                Apunta("");
                Apunta($"──────── {n} ventana(s) ────────");

                // ---------- Abrir las que falten ----------
                //
                // Incrementalmente: de 2 a 5 se abren tres más, no se cierran las dos y se
                // abren cinco. Menos trasiego y menos tiempo, y el caso de «aparece una
                // ventana con la vista abierta» queda ejercitado de paso.
                while (procesos.Count < n)
                {
                    int i = procesos.Count + 1;
                    string copia = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"NubeVentanita-R{i}-" + DateTime.Now.ToString("HHmmssfff") + ".exe");
                    System.IO.File.Copy(Environment.ProcessPath ?? "", copia, true);
                    copias.Add(copia);

                    var p = System.Diagnostics.Process.Start(
                        copia, new[] { DiagVentanita.Bandera, $"NubeBar · rejilla #{i}", "110" });
                    if (p != null) procesos.Add(p);
                    await Task.Delay(500);
                }

                await Task.Delay(1200);
                GestorVentanas.Refrescar(GestorVentanas.Motivo.AMano);
                await Task.Delay(1200);

                // ---------- Abrir la vista y medirla ----------
                AlternarVista();
                await Task.Delay(1800);

                if (_panelAbierto != PanelVista)
                {
                    malos++;
                    Apunta($"   FALLA la vista no se abrió con {n} ventanas");
                    continue;
                }

                var m = MedidaDeLasTarjetas();
                int columnas = Math.Max(1, _reparto.Columnas);
                int hueco = DiagRejilla.HuecoDeLaUltimaFila(m.Cuantas, columnas);
                bool filaBien = DiagRejilla.LaUltimaFilaEstaBien(m.Cuantas, columnas);

                Apunta($"   {m.Linea}");
                Apunta($"   reparto: {columnas} columna(s) · {m.Cuantas} tarjeta(s) · " +
                       $"hueco en la última fila: {hueco}");

                // Las nuestras más las que ya tuvieras abiertas: la vista las enseña todas,
                // y eso está bien. Lo que se mide es la FORMA de la cuadrícula, no cuántas
                // ventanas hay — por eso la cuenta sale de lo medido y no de `n`.
                if (m.Cuantas != n)
                    Apunta($"   (nuestras: {n} · medidas: {m.Cuantas} · las demás son tuyas, " +
                           "y la vista las enseña con razón)");

                if (!m.TodasIguales)
                {
                    malos++;
                    Apunta($"   FALLA hay {m.TamanosDistintos} tamaños de tarjeta distintos");
                }
                else Apunta($"   ok   todas las tarjetas miden {m.Ancho:0}x{m.Alto:0}");

                if (!filaBien)
                {
                    malos++;
                    Apunta($"   FALLA la última fila deja {hueco} de {columnas} vacíos: " +
                           "más de media fila es un agujero que se ve");
                }
                else Apunta("   ok   la última fila no deja un agujero grande");

                // ---------- Y elegir una: que venga al frente ----------
                //
                // Solo en el último caso, que es cuando hay más tarjetas y el acierto cuenta
                // más. Estaba en la lista de manuales —«elegir una ventana y comprobar que
                // viene al frente»— y sí se puede medir: se levanta el `Click` de la tarjeta
                // de **una ventana nuestra** y se mira quién tiene el foco después.
                //
                // No es inyección de entrada: es llamar al manejador del botón, el mismo
                // camino que recorre una pulsación. Y la ventana elegida es siempre nuestra:
                // traer al frente una tuya sería moverte el escritorio para probarme.
                if (n == DiagRejilla.Cuantas.Last() && !await ElegirUnaDeLasNuestras(n)) malos++;

                CerrarPanel();
                await Task.Delay(700);
            }

            if (!await MinimizadaNoEsActiva()) malos++;
            if (!await LaEscondidaSeRecupera()) malos++;

            Apunta("");
            Apunta(malos == 0
                ? "[REJ] las cuatro cuadrículas salen bien: tarjetas iguales y sin agujeros."
                : $"[REJ] ¡OJO! {malos} problema(s).");
        }
        catch (Exception ex)
        {
            malos++;
            Apunta($"[REJ] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico de la rejilla", ex);
        }
        finally
        {
            try { CerrarPanel(); } catch { }
            foreach (var p in procesos) { try { if (!p.HasExited) p.Kill(); } catch { } }
            await Task.Delay(900);
            foreach (var c in copias)
            {
                try { if (System.IO.File.Exists(c)) System.IO.File.Delete(c); }
                catch (Exception ex) { Apunta($"[REJ] no se pudo borrar {System.IO.Path.GetFileName(c)}: {ex.GetType().Name}"); }
            }
            Apunta($"[REJ] {procesos.Count} ventana(s) cerradas y {copias.Count} copia(s) borradas.");
            Application.Current.Shutdown(malos == 0 ? 0 : 5);
        }
    }
}
