using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NubeBar;

// `NubeBar.exe --ventanas [informe.txt]`: qué ventanas ve NubeBar y por qué descarta las
// demás.
//
// Existe porque "faltan aplicaciones" es una queja que no se puede investigar mirando el
// código: hay que ver qué devuelve `EnumWindows` EN ESTA MÁQUINA y qué regla se come
// cada una. Sin esto, lo único que se puede hacer es adivinar y cambiar el filtro a
// ciegas, que es exactamente como se rompen estas cosas.
//
// Es de solo lectura: enumera, pregunta y escribe un informe. No activa, no mueve, no
// cierra ni cambia el estado de ninguna ventana.
public static class Auditoria
{
    internal const string Bandera = "--ventanas";

    public static int Correr(string[] args)
    {
        var sb = new StringBuilder();
        void Di(string s) => sb.AppendLine(s);

        try
        {
            Di("=== Qué ventanas ve NubeBar ===");
            Di($"fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Di("");

            var todas = Nativo.TodasLasDeNivelSuperior();
            Di($"ventanas de nivel superior que devuelve Windows: {todas.Count}");

            var aceptadas = new List<(Nativo.FichaVentana F, string Titulo, string Exe)>();
            var descartadas = new Dictionary<string, int>();
            var ejemplos = new Dictionary<string, string>();

            foreach (var h in todas)
            {
                var f = Nativo.Ficha(h);
                if (GestorVentanas.EsDelUsuario(f, out string porque))
                {
                    aceptadas.Add((f, Nativo.Titulo(h), Nativo.Exe(h) ?? ""));
                    continue;
                }
                descartadas[porque] = descartadas.TryGetValue(porque, out int n) ? n + 1 : 1;
                if (!ejemplos.ContainsKey(porque))
                {
                    var t = Nativo.Titulo(h);
                    ejemplos[porque] = $"clase={f.Clase} titulo=\"{(t.Length > 40 ? t[..40] : t)}\"";
                }
            }

            Di($"ventanas que cuentan como del usuario: {aceptadas.Count}");
            Di("");
            Di("--- descartadas, por motivo ---");
            foreach (var (motivo, n) in descartadas.OrderByDescending(p => p.Value))
                Di($"  {n,4}  {motivo}   ·  p.ej. {ejemplos[motivo]}");

            // ---------- Cómo quedan agrupadas ----------
            Di("");
            Di("--- agrupadas por aplicación ---");
            var activa = Nativo.Activa();
            var porApp = aceptadas
                .GroupBy(a => GestorVentanas.NombreDeApp(a.Exe) is { Length: > 0 } n ? n : "(sin ejecutable)",
                         StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int minimizadas = 0, maximizadas = 0, sinExe = 0, sinTitulo = 0, inalcanzables = 0;
            foreach (var g in porApp)
            {
                Di($"  {g.Key}  ({g.Count()} ventana(s))");
                foreach (var a in g)
                {
                    bool min = Nativo.EstaMinimizada(a.F.Hwnd);
                    bool max = Nativo.EstaMaximizada(a.F.Hwnd);
                    bool esActiva = a.F.Hwnd == activa;
                    bool alcanzable = Nativo.ProcesoAlcanzable(a.F.Pid);
                    if (min) minimizadas++;
                    if (max) maximizadas++;
                    if (a.Exe.Length == 0) sinExe++;
                    if (a.Titulo.Length == 0) sinTitulo++;
                    if (!alcanzable) inalcanzables++;

                    var estado = min ? "minimizada" : max ? "maximizada" : "normal";
                    var mon = Monitores.Todos().FindIndex(m => m.Handle == Nativo.MonitorDe(a.F.Hwnd));
                    Di($"      0x{a.F.Hwnd.ToInt64():X8}  {estado,-11} " +
                       $"{(esActiva ? "ACTIVA " : "       ")}" +
                       $"pantalla {(mon < 0 ? "?" : (mon + 1).ToString())}  " +
                       $"clase={a.F.Clase}  \"{(a.Titulo.Length > 50 ? a.Titulo[..50] : a.Titulo)}\"");
                }
            }

            // ---------- El resumen que se pidió ----------
            Di("");
            Di("--- recuentos ---");
            Di($"  ventanas detectadas .............. {aceptadas.Count}");
            Di($"  ventanas descartadas ............. {descartadas.Values.Sum()}");
            Di($"  aplicaciones agrupadas ........... {porApp.Count}");
            Di($"  activas .......................... {aceptadas.Count(a => a.F.Hwnd == activa)}");
            Di($"  minimizadas ...................... {minimizadas}");
            Di($"  maximizadas ...................... {maximizadas}");
            Di($"  en segundo plano ................. {aceptadas.Count - aceptadas.Count(a => a.F.Hwnd == activa)}");
            Di($"  sin ejecutable resuelto .......... {sinExe}");
            Di($"  sin título ....................... {sinTitulo}");
            Di($"  proceso no alcanzable (elevado) .. {inalcanzables}");
            Di($"  monitores ........................ {Monitores.Todos().Count}");

            // ---------- Y lo que vería la vista general ----------
            GestorVentanas.Refrescar();
            Di("");
            Di("--- lo que enseñaría la vista general ---");
            Di($"  en la instantánea: {GestorVentanas.Cuantas}");
            Di($"  aplicaciones para el filtro: {string.Join(", ", GestorVentanas.Aplicaciones())}");
        }
        catch (Exception ex) { Di("EXCEPCIÓN: " + ex); }

        var texto = sb.ToString();
        Console.WriteLine(texto);
        var destino = args.Length > 1 ? args[1] : null;
        if (!string.IsNullOrWhiteSpace(destino))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
                File.WriteAllText(destino, texto, new UTF8Encoding(false));
            }
            catch (Exception ex) { Console.WriteLine("no se pudo escribir: " + ex.Message); }
        }
        return 0;
    }
}
