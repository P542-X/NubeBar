using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace NubeBar;

// El runner de `--diagd6`. Ver DiagD6.cs para lo que este diagnóstico se compromete a no
// hacer.
public partial class MainWindow
{
    async System.Threading.Tasks.Task CorrerDiagnosticoD6()
    {
        try
        {
            Apunta("=== integración visual (D6) ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta("solo lectura: no se mueve ninguna ventana, no se abre ningún panel");
            await System.Threading.Tasks.Task.Delay(2000);

            // ================= Monitores y DPI =================
            //
            // Es la parte que una prueba pura no puede contestar: cuántas pantallas hay y a
            // qué escala. Y el caso que de verdad rompe las cosas no es "dos monitores", es
            // "dos monitores con escalas distintas".
            var monitores = Monitores.Todos();
            Apunta($"[D6]  monitores: {monitores.Count}");
            var escalas = new HashSet<double>();
            for (int i = 0; i < monitores.Count; i++)
            {
                var m = monitores[i];
                escalas.Add(m.Escala);
                Apunta($"  {i + 1}: {m.Ancho}x{m.Alto} escala={m.Escala:0.##} " +
                       $"({m.Escala * 100:0}% DPI)  trabajo={m.TrabajoX},{m.TrabajoY} " +
                       $"{m.TrabajoAncho}x{m.TrabajoAlto}{(m.EsPrincipal ? "  [principal]" : "")}");
            }
            Apunta($"[D6]  escalas distintas en juego: {escalas.Count} " +
                   $"({string.Join(", ", escalas.OrderBy(e => e).Select(e => $"{e * 100:0}%"))})");
            if (escalas.Count > 1)
                Apunta("[D6]  OJO: hay monitores con escalas distintas. Es el caso que rompe " +
                       "las cuentas de píxeles; lo visual de ese caso necesita tus ojos.");

            // ================= La isla: borde y orientación =================
            var borde = PanelNube.BordeEfectivo(_cfg.Paneles.FirstOrDefault()?.Posicion);
            Apunta($"[D6]  isla: modo={_cfg.Paneles.FirstOrDefault()?.Modo ?? "?"} " +
                   $"borde={borde} vertical={Vertical} alto={_cfg.Barra.Alto}");
            Apunta($"[D6]  islas montadas: {Islas.Count} (una por monitor con isla)");

            // ================= El indicador de escritorio =================
            double sitio = SitioParaElIndicador();
            var talla = HyprIndicador.TallaQueCabe(sitio, Vertical);
            Apunta($"[D6]  indicador: sitio={sitio:0} px -> talla={talla}");

            if (Escritorios.Disponible)
            {
                var todos = Escritorios.Todos();
                var fichas = FichasDeEscritorios(todos);
                foreach (var f in fichas)
                    Apunta($"  {HyprIndicador.Rotulo(f, fichas.Count, talla),-22} " +
                           $"estado={f.Como,-8} nombrePropio={(f.TieneNombre ? "sí" : "no")}  " +
                           $"«{HyprIndicador.ParaLeer(f, fichas.Count)}»");
                Apunta($"[D6]  huella del indicador: {HyprIndicador.Huella(fichas, talla)}");
            }
            else Apunta("[D6]  los escritorios virtuales no están disponibles en este Windows");

            // ================= Contraste con el tema PUESTO =================
            //
            // Las pruebas puras miden los siete temas de serie. Esto mide el que el usuario
            // tiene ahora, que puede ser uno suyo con colores que nadie ha visto.
            Apunta($"[D6]  alto contraste de Windows: {SystemParameters.HighContrast}");
            var azul = HyprPinta.Azul(_roles);
            var coral = HyprPinta.Coral(_roles);
            Apunta($"[D6]  tema puesto: fondo={_roles.FondoOpaco} superficie={_roles.SuperficieSolida}");
            Apunta($"  azul de foco      = {azul}   " +
                   $"contra el fondo {Contraste.Entre(azul, _roles.FondoOpaco):0.00}:1");
            Apunta($"  azul como tinta   = {HyprPinta.AzulTinta(_roles, _roles.SuperficieSolida)}   " +
                   $"sobre superficie {Contraste.Entre(HyprPinta.AzulTinta(_roles, _roles.SuperficieSolida), _roles.SuperficieSolida):0.00}:1" +
                   $"  (mínimo {Roles.MinimoSuperficie})");
            Apunta($"  coral de aviso    = {coral}   " +
                   $"contra el fondo {Contraste.Entre(coral, _roles.FondoOpaco):0.00}:1");
            Apunta($"  coral como tinta  = {HyprPinta.CoralTinta(_roles, _roles.SuperficieSolida)}   " +
                   $"sobre superficie {Contraste.Entre(HyprPinta.CoralTinta(_roles, _roles.SuperficieSolida), _roles.SuperficieSolida):0.00}:1");
            double sep = Math.Abs(Contraste.Tono(azul) - Contraste.Tono(coral));
            sep = Math.Min(sep, 360 - sep);
            Apunta($"  separación de tono entre los dos: {sep:0}° (tiene que pasar de 60)");

            // ================= El filo de la previsualización =================
            var detras = DetrasDeLaPrevia();
            Apunta($"[D6]  lo que hay detrás de la previsualización: " +
                   $"{(detras is { } d ? d.ToString() + $" (luminancia {Contraste.Luminancia(d):0.000})" : "no se sabe (sin Wallpaper Engine)")}");
            var filo = HyprPinta.FiloDelDestino(_roles, detras);
            Apunta($"  filo exterior={filo.Exterior} {filo.GrosorExterior} px  " +
                   $"interior={filo.Interior} {filo.GrosorInterior} px  total={filo.Total} px");
            Apunta($"  relleno={HyprPinta.RellenoDelDestino(_roles)} " +
                   $"(alfa {HyprPinta.RellenoDelDestino(_roles).A}/255: la ventana de debajo se sigue viendo)");

            foreach (var fondo in new[] { Colors.White, Colors.Black, Color.FromRgb(0x80, 0x80, 0x80) })
            {
                var f = HyprPinta.FiloDelDestino(_roles, fondo);
                double mejorContraste = Math.Max(
                    Contraste.Entre(Color.FromRgb(f.Exterior.R, f.Exterior.G, f.Exterior.B), fondo),
                    Contraste.Entre(f.Interior, fondo));
                Apunta($"  sobre {fondo}: la línea que más se ve da {mejorContraste:0.00}:1 " +
                       $"(mínimo {HyprPinta.MinimoFilo})");
            }

            // ================= Dónde caería el menú de una tarjeta =================
            //
            // Con el área útil DE VERDAD de cada monitor, que es lo que una prueba pura no
            // tiene. Es la comprobación de los cuatro bordes sobre esta máquina.
            Apunta("[D6]  dónde cabe el menú de una tarjeta, por monitor:");
            foreach (var m in monitores)
            {
                var area = new NubeHypr.Rect(m.TrabajoX, m.TrabajoY, m.TrabajoAncho, m.TrabajoAlto);
                var elegida = new NubeHypr.Rect(area.X + area.Ancho * 0.1, area.Y + area.Alto * 0.1,
                                                area.Ancho * 0.45, area.Alto * 0.5);
                var s = HyprVista.DondeCabe(elegida, area, 320, 480);
                bool dentro = s.Donde.X >= area.X - 0.5 && s.Donde.Y >= area.Y - 0.5 &&
                              s.Donde.X + s.Donde.Ancho <= area.X + area.Ancho + 0.5 &&
                              s.Donde.Y + s.Donde.Alto <= area.Y + area.Alto + 0.5;
                Apunta($"  área {area.Ancho}x{area.Alto} -> al {s.Lado} en " +
                       $"{s.Donde.X:0},{s.Donde.Y:0}  solapa={(s.Solapa ? "sí" : "no")}  " +
                       $"dentro del área={(dentro ? "sí" : "NO, MAL")}");
            }

            // ================= La nube =================
            Apunta($"[D6]  tamaño de la nube: en la isla={Math.Min(HyprPinta.TamanoDeLaNube(_cfg.Barra.Alto), 18):0.#} px, " +
                   $"en una cabecera de Ajustes={HyprPinta.TamanoDeLaNube(140):0.#} px, " +
                   $"en un panel vacío={HyprPinta.TamanoDeLaNube(190):0.#} px");
            Apunta($"  topes: mínimo {HyprPinta.MinNube}, máximo {HyprPinta.MaxNube}, " +
                   $"y nunca más del {HyprPinta.FraccionMaxima * 100:0}% del lado");

            // ================= Animaciones =================
            Apunta($"[D6]  animaciones: nivel={Animacion.Nivel} factor={Animacion.Factor:0.##} " +
                   $"fundido={Animacion.FactorFundido:0.##}  " +
                   $"un fundido de 200 ms dura {HyprPinta.Duracion(200).TotalMilliseconds:0} ms");
            if (!Animacion.HayFundido)
                Apunta("  con este nivel no se lanza ningún fundido: todo aparece en su estado final");

            // ================= Y lo que NO puede decir este diagnóstico =================
            Apunta("[D6]  PENDIENTE DE TUS OJOS: que el filo se vea sobre un vídeo de Wallpaper");
            Apunta("      Engine, que no haya glifos en caja, que no haya parpadeos al abrir y");
            Apunta("      cerrar paneles rápido, y que el halo del teclado se distinga del filo");
            Apunta("      de la ventana activa. Nada de eso se puede medir desde aquí.");
        }
        catch (Exception ex) { Log.Error("el diagnóstico de D6", ex); }
        finally { Application.Current.Shutdown(0); }
    }
}
