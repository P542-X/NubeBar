using System;
using System.Threading.Tasks;

namespace NubeBar;

// El lado ventana de `--diagneon`. Ver `DiagNeon.cs` para el porqué.
public partial class MainWindow
{
    async Task CorrerDiagnosticoDelNeon()
    {
        try
        {
            if (DiagNeon.Carpeta is { Length: > 2 }) DiagCapturas.Carpeta = DiagNeon.Carpeta;

            // La crea este diagnóstico, no `FotoEnPantalla`: el flujo de `--capturas` la
            // creaba por su cuenta y al reutilizar sus fotos desde aquí nadie la creaba. Las
            // ocho fotos salieron con `DirectoryNotFoundException`, una por una.
            System.IO.Directory.CreateDirectory(DiagCapturas.Carpeta);

            // ---------- Se fuerza un tema que brille ----------
            //
            // Esto forzaba el estilo «Neón», que ya no existe: el halo es ahora una decoración
            // del tema (ver `Formas`). Se usa Cyberpunk, que la lleva encendida y con un color
            // bien visible — con un tema sin halo no habría nada que mirar.
            //
            // En memoria y devuelto al final. El fichero no se toca: `--diagneon` es una
            // bandera, así que `Config.PuedeEscribir` ya es falso.
            string antes = _cfg.Tema.Id;
            var misColores = _cfg.Tema.Colores;
            _cfg.Tema.Id = "cyberpunk";
            _cfg.Tema.Colores = new Dictionary<string, string>();
            Aplicar();

            var n = Neon.Limpia(_cfg.Neon);
            Apunta("=== El halo del neón, a lo largo de dos ciclos ===");
            Apunta($"[NEON] ajuste: intensidad {n.Intensidad} · radio {n.Radio} px · " +
                   $"ciclo {n.Velocidad} ms · respira={n.Respira} · preajuste «{Neon.NombreDe(n)}»");
            Apunta($"[NEON] nivel de animación: {Animacion.Nivel}. " +
                   (Animacion.Adorno
                        ? "Hay adorno: el halo DEBE moverse entre las fotos."
                        : "Sin adorno: el halo tiene que salir FIJO y visible en las seis."));
            Apunta($"[NEON] realces — ambiente {Realces.De(Realce.Ambiente)} · " +
                   $"reposo {Realces.De(Realce.Reposo)} · activo {Realces.De(Realce.Activo)}");
            Apunta("[NEON] lo que hay que mirar: si la 3ª y la 6ª tienen el halo MÁS ancho y");
            Apunta("       más vivo que la 1ª y la 5ª, respira. Si las seis salen idénticas,");
            Apunta("       es una línea dibujada y el neón no está terminado.");

            // Se monta el panel DESPUÉS de cambiar el estilo: el estilo se lee al construir.
            AbrirLateral(Lateral.Lado.Izquierda);
            await Task.Delay(900);

            // ---------- El ciclo se REARRANCA aquí ----------
            //
            // Antes se tomaba este instante como origen y ya. Pero la animación no empieza
            // aquí: empieza al crear el efecto, que es al montar el panel — 900 ms antes, más
            // lo que tarde en construirse. Con un ciclo de 2.600 ms, ese desfase era casi un
            // tercio de vuelta, así que la foto rotulada «mínimo» salía con el halo en su
            // punto ALTO (blur 39,6) y la rotulada «MÁXIMO» en el bajo (21,2).
            //
            // Los números eran correctos; el rótulo era el que mentía. Y un diagnóstico que
            // etiqueta mal es peor que no tenerlo: me hizo concluir que el halo no respiraba
            // cuando llevaba respirando todo el rato.
            //
            // Volviendo a poner el efecto, la animación arranca de cero justo antes del
            // primer disparo y las fases pasan a significar lo que dicen.
            var vIzq = _laterales.GetValueOrDefault(Lateral.Lado.Izquierda);
            if (vIzq != null) PonerResplandor(vIzq.Tarjeta, EstiloDeAhora, Realce.Ambiente);
            await Task.Delay(60);   // que el primer fotograma del ciclo nuevo llegue a pintarse

            var origen = DateTime.Now;
            double ciclo = n.Velocidad;

            for (int i = 0; i < DiagNeon.Momentos.Length; i++)
            {
                double objetivo = DiagNeon.Momentos[i] * ciclo;
                double llevamos = (DateTime.Now - origen).TotalMilliseconds;
                int esperar = (int)Math.Max(0, objetivo - llevamos);
                if (esperar > 0) await Task.Delay(esperar);

                double enElCiclo = ((DateTime.Now - origen).TotalMilliseconds % ciclo) / ciclo;

                // ---------- Lo que el efecto dice que vale AHORA ----------
                //
                // Esto parte el problema en dos y evita seguir adivinando. Si estos números
                // cambian entre disparos, la animación corre y lo que falla es el dibujado o
                // la captura. Si no cambian, es que la animación no ha llegado a arrancar.
                //
                // Se lee con `GetValue`, no con la propiedad: en un `Freezable` animado la
                // propiedad devuelve el valor base y `GetValue` el valor efectivo, que es el
                // que de verdad se está pintando. Preguntar por el sitio equivocado habría
                // dado «no cambia» con la animación corriendo perfectamente.
                var vLat = _laterales.GetValueOrDefault(Lateral.Lado.Izquierda);
                if (vLat?.Tarjeta.Effect is System.Windows.Media.Effects.DropShadowEffect ef)
                {
                    double blur = (double)ef.GetValue(
                        System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty);
                    double op = (double)ef.GetValue(
                        System.Windows.Media.Effects.DropShadowEffect.OpacityProperty);
                    Apunta($"[NEON] {i + 1}/6 efecto: blur={blur:0.0} opacidad={op:0.000} " +
                           $"congelado={ef.IsFrozen} fase={enElCiclo:0.00}");
                }
                else Apunta($"[NEON] {i + 1}/6 · la tarjeta NO tiene efecto puesto");

                await FotoEnPantalla(_laterales.GetValueOrDefault(Lateral.Lado.Izquierda),
                                     $"40-neon-{i + 1}-{DiagNeon.QueTocaVer[i].Split(' ')[0]}",
                                     $"Neón {i + 1}/6 — {DiagNeon.QueTocaVer[i]} " +
                                     $"(t={objetivo:0} ms, fase {enElCiclo:0.00} del ciclo)");
            }

            // ---------- Y los dos paneles a la vez ----------
            //
            // Lo pidió explícitamente, y además es donde se ve si el halo de ambiente de dos
            // conchas se suma en el centro de la pantalla y ensucia.
            AbrirLateral(Lateral.Lado.Derecha);
            await Task.Delay(900);
            await FotoEnPantalla(_laterales.GetValueOrDefault(Lateral.Lado.Derecha),
                                 "41-neon-derecho", "Neón: el panel derecho");
            await Foto("42-neon-los-dos", "Neón: los dos paneles abiertos a la vez");

            CerrarLateral(Lateral.Lado.Izquierda);
            CerrarLateral(Lateral.Lado.Derecha);
            _cfg.Tema.Id = antes;
            _cfg.Tema.Colores = misColores;
            Aplicar();
            Apunta($"[NEON] tema devuelto a «{antes}» (en memoria; el fichero no se ha tocado)");
        }
        catch (Exception ex)
        {
            Apunta($"[NEON] reventó: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico del neón", ex);
        }
        finally
        {
            // Como todos los diagnósticos que abren interfaz: se cierra solo. Un diagnóstico
            // que se queda abierto deja una barra de fase corriendo como barra del usuario,
            // que es justo lo que `Fase` existe para impedir.
            Apunta($"[NEON] hechas en {DiagCapturas.Carpeta}");
            try { CerrarPanel(); } catch { }
            System.Windows.Application.Current.Shutdown(0);
        }
    }
}
