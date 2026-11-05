using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NubeBar;

// `NubeBar.exe --diagmonitores`: qué monitores hay, quiénes son y qué entradas de vídeo
// gestionan. Medido sobre esta máquina y por el cable de verdad.
//
// ---------- Por qué hace falta ----------
//
// `Pruebas.Monitores.cs` comprueba los dos descifradores con cadenas y EDID escritos a mano, y
// eso es lo correcto para un formato binario. Pero no puede contestar la única pregunta que de
// verdad importa: **¿contesta TU monitor?** Eso necesita el monitor, el cable y DDC/CI activado
// en su menú, y no hay forma de simularlo.
//
// Sin esto, la alternativa para investigar «el botón de HDMI no hace nada» es arrancar la barra,
// abrir el panel y mirar el log. Con dos monitores y un cambio que te quita la pantalla de
// delante, eso es una tarde. Aquí sale una tabla y se cierra.
//
// ---------- De solo lectura, salvo que se pida lo contrario ----------
//
// Sin más argumentos NO MANDA NINGÚN COMANDO: enumera, lee el 0x60 y las capacidades, e imprime.
// Es la regla del plan —«no ejecutar comandos de cambio automáticamente durante la detección»— y
// además es lo que hace que se pueda correr sin pensar: un diagnóstico que te cambia el canal
// del monitor es un diagnóstico que no vas a volver a correr.
//
// Para cambiar de verdad hay que pedirlo con nombre y apellidos:
//
//     NubeBar.exe --diagmonitores
//     NubeBar.exe --diagmonitores informe.txt
//     NubeBar.exe --diagmonitores --cambiar AOCB309-ABC123 11
//     NubeBar.exe --diagmonitores --cambiar 2 0x11 informe.txt
//
// El monitor se nombra por su CLAVE (la que imprime la tabla) o por su número de fila. Y el
// código, en hexadecimal, con o sin `0x`.
//
// ---------- Códigos de salida ----------
//
//   0  se pudo preguntar y al menos un monitor gestiona su entrada
//   2  ningún monitor gestiona su entrada (DDC/CI apagado, no soportado, o sesión remota)
//   3  el cambio que se pidió no se pudo hacer
//   1  reventó por algo inesperado
public static class DiagMonitores
{
    internal const string Bandera = "--diagmonitores";

    public static int Correr(string[] args)
    {
        var sb = new StringBuilder();
        int codigo;

        try { codigo = Medir(args, sb); }
        catch (Exception ex)
        {
            sb.AppendLine();
            sb.AppendLine("EXCEPCIÓN: " + ex);
            codigo = 1;
        }

        var texto = sb.ToString();
        Log.Info("--diagmonitores:\n" + texto);

        // Al informe si se pidió, y siempre al log: el log es el que queda cuando el informe se
        // escribe en una carpeta temporal y se pierde.
        var destino = args.FirstOrDefault(a => a.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        if (destino is not null)
        {
            try { File.WriteAllText(destino, texto, Encoding.UTF8); }
            catch (Exception ex) { Log.Error($"escribir {destino}", ex); }
        }

        return codigo;
    }

    static int Medir(string[] args, StringBuilder sb)
    {
        sb.AppendLine("=== Monitores y entrada de vídeo por DDC/CI ===");
        sb.AppendLine($"{Log.Version} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // ---------- 1. Lo que Windows dice que hay ----------
        //
        // Antes de preguntar por el cable, lo que se ve sin preguntar. Si aquí ya no están las
        // dos pantallas, el problema no es DDC/CI.
        sb.AppendLine("--- pantallas lógicas (lo que ve Windows) ---");
        var logicas = Monitores.Todos();
        foreach (var m in logicas)
            sb.AppendLine($"  {Entradas.Corto(m.Dispositivo),-10} " +
                          $"{m.Ancho}x{m.Alto} en ({m.X},{m.Y})  " +
                          $"{m.Dpi} ppp (x{m.Escala:0.##})  " +
                          $"{(m.EsPrincipal ? "principal" : "")}");
        sb.AppendLine($"  total: {logicas.Count}");
        sb.AppendLine();

        // ---------- 2. Lo que contesta cada monitor por el cable ----------
        sb.AppendLine("--- monitores físicos (preguntando por el cable) ---");
        var pantallas = Entradas.Pantallas();

        if (pantallas.Count == 0)
        {
            sb.AppendLine("  NINGUNO. Windows no expone monitores físicos.");
            sb.AppendLine("  Pasa en sesiones remotas, con el driver básico de vídeo y con");
            sb.AppendLine("  algunos conversores que no pasan el I²C.");
            return 2;
        }

        for (int i = 0; i < pantallas.Count; i++)
        {
            var p = pantallas[i];
            sb.AppendLine();
            sb.AppendLine($"  [{i + 1}] {p.Rotulo}");
            sb.AppendLine($"       clave        {p.Clave}");
            sb.AppendLine($"       ranura       {Entradas.Corto(p.Dispositivo)} · físico #{p.IndiceFisico}");
            sb.AppendLine($"       dxva2 dice   «{p.Descripcion}»");

            if (p.Edid is { } e)
                sb.AppendLine($"       EDID         fabricante {e.Fabricante} · producto 0x{e.Codigo:X4} · " +
                              $"serie {(string.IsNullOrWhiteSpace(e.Serie) ? $"0x{e.NumeroDeSerie:X8}" : e.Serie)} · " +
                              $"modelo {e.Nombre ?? "(no lo dice)"}");
            else
                // Sin EDID el monitor sigue funcionando: lo que pierde es el nombre bonito y la
                // identidad estable entre reinicios. Conviene saberlo porque explica por qué un
                // alias no «se pega».
                sb.AppendLine("       EDID         no se pudo leer (sin permiso, o clave de registro a medias)");

            sb.AppendLine($"       DDC/CI       {(p.Responde ? "responde" : "NO responde")}");
            sb.AppendLine($"       VCP 0x60     {(p.GestionaEntrada ? "contesta" : "NO contesta")}");

            if (p.Actual is { } act)
                sb.AppendLine($"       entrada      0x{act:X2}  {Entradas.NombreDe(act)}");
            else
                sb.AppendLine(p.Responde
                    ? "       entrada      no la gestiona: este modelo no permite cambiarla por software"
                    : "       entrada      no contesta. Activa DDC/CI en el menú del propio monitor");

            if (p.Anunciadas)
                sb.AppendLine($"       anuncia      " + string.Join("  ",
                    p.Disponibles.Select(x => $"0x{x.Codigo:X2}={x.Nombre}")));
            else if (p.GestionaEntrada)
                sb.AppendLine("       anuncia      nada: gestiona la entrada pero no dice cuáles tiene. " +
                              "Solo queda el código manual.");
        }

        sb.AppendLine();
        int gestionan = pantallas.Count(p => p.GestionaEntrada);
        sb.AppendLine($"  total: {pantallas.Count} monitor(es) físico(s), {gestionan} gestiona(n) su entrada");

        // ---------- 3. El cambio, solo si se pidió ----------
        int donde = Array.FindIndex(args, a => a.Equals("--cambiar", StringComparison.OrdinalIgnoreCase));
        if (donde < 0)
        {
            sb.AppendLine();
            sb.AppendLine("No se ha mandado ningún comando: esto solo ha LEÍDO.");
            sb.AppendLine("Para cambiar una entrada: --diagmonitores --cambiar <clave|nº> <código hex>");
            return gestionan > 0 ? 0 : 2;
        }

        sb.AppendLine();
        sb.AppendLine("--- cambio pedido ---");

        if (donde + 2 >= args.Length)
        {
            sb.AppendLine("  Faltan argumentos: --cambiar <clave|nº> <código hex>");
            return 3;
        }

        var quien = args[donde + 1];
        var elegido = Resolver(quien, pantallas);
        if (elegido is null)
        {
            sb.AppendLine($"  No encuentro ningún monitor que sea «{quien}».");
            sb.AppendLine("  Usa su clave o su número de fila de la tabla de arriba.");
            return 3;
        }

        var cod = args[donde + 2];
        if (cod.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) cod = cod.Substring(2);
        if (!byte.TryParse(cod, System.Globalization.NumberStyles.HexNumber, null, out byte valor) || valor == 0)
        {
            sb.AppendLine($"  «{args[donde + 2]}» no es un código hexadecimal de un byte (0F, 11, 12…).");
            return 3;
        }

        sb.AppendLine($"  monitor  {elegido.Rotulo}  ({elegido.Clave})");
        sb.AppendLine($"  de       0x{elegido.Actual:X2} {elegido.NombreActual}");
        sb.AppendLine($"  a        0x{valor:X2} {Entradas.NombreDe(valor)}");

        // Si no la anuncia, se manda igual —es la configuración manual avanzada que el plan
        // permite— pero queda escrito que iba a ciegas.
        if (elegido.Anunciadas && elegido.Disponibles.All(x => x.Codigo != valor))
            sb.AppendLine($"  OJO      el monitor NO anuncia 0x{valor:X2} entre sus entradas");

        var r = Entradas.Poner(elegido.Clave, valor);
        sb.AppendLine($"  resultado {r.Que}: {r.Mensaje}");
        if (r.Leida is { } leida) sb.AppendLine($"  releído   0x{leida:X2} {Entradas.NombreDe(leida)}");

        // ---------- Y que el OTRO monitor no se haya movido ----------
        //
        // Es el criterio de aceptación que más importa de los diez: «no cambia accidentalmente
        // la entrada del otro monitor». Comprobarlo a ojo con dos pantallas iguales es
        // exactamente donde se cuela un fallo, así que se comprueba aquí.
        sb.AppendLine();
        sb.AppendLine("--- ¿se ha movido alguien más? ---");
        bool intruso = false;
        foreach (var antes in pantallas)
        {
            if (antes.Clave == elegido.Clave || antes.Actual is null) continue;
            var ahora = Entradas.Leer(antes.Clave);
            if (ahora is null)
            {
                sb.AppendLine($"  {antes.Rotulo}: ya no contesta (¿se ha reconfigurado la pantalla?)");
                continue;
            }
            bool igual = ahora == antes.Actual;
            if (!igual) intruso = true;
            sb.AppendLine($"  {antes.Rotulo}: 0x{antes.Actual:X2} → 0x{ahora:X2}  " +
                          (igual ? "sin tocar, bien" : "¡HA CAMBIADO! esto es un fallo"));
        }

        // `Ignorada` NO cuenta como conseguido: el monitor contestó y se quedó donde estaba.
        // `SinVerificar` sí, porque ahí lo más probable es que el cambio haya salido bien y lo
        // que falta sea la pantalla con la que comprobarlo.
        bool fue = r.Que is Entradas.Resultado.Cambiada or Entradas.Resultado.YaEstaba
                          or Entradas.Resultado.SinVerificar;
        return fue && !intruso ? 0 : 3;
    }

    // Por clave (entera o por su principio, que las del EDID son largas) o por número de fila.
    static Entradas.Pantalla? Resolver(string quien, List<Entradas.Pantalla> pantallas)
    {
        if (int.TryParse(quien, out int n) && n >= 1 && n <= pantallas.Count) return pantallas[n - 1];

        return pantallas.FirstOrDefault(p => p.Clave.Equals(quien, StringComparison.OrdinalIgnoreCase))
            ?? pantallas.FirstOrDefault(p => p.Clave.StartsWith(quien, StringComparison.OrdinalIgnoreCase))
            ?? pantallas.FirstOrDefault(p => p.Rotulo.Contains(quien, StringComparison.OrdinalIgnoreCase));
    }
}
