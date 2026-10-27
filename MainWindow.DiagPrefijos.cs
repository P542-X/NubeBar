using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace NubeBar;

// El runner de `--diagprefijos`. Ver DiagPrefijos.cs para lo que se compromete a no hacer.
public partial class MainWindow
{
    sealed record Nota(string Prefijo, int Entran, int Ocupados, int NoSeEntienden, int Errores,
                       List<string> Detalle)
    {
        public int Total => Entran + Ocupados + NoSeEntienden + Errores;

        // El que más entran, y en caso de empate el que viene antes en la lista de
        // candidatos — que ya está ordenada por "cuánto se cruza con lo que Windows se
        // reserva". Desempatar por orden y no por azar importa: si no, dos ejecuciones del
        // diagnóstico podrían recomendar prefijos distintos con los mismos datos.
        public string Resumen =>
            $"{Entran}/{Total} entran" +
            (Ocupados > 0 ? $", {Ocupados} ocupados" : "") +
            (NoSeEntienden > 0 ? $", {NoSeEntienden} que Windows no entiende" : "") +
            (Errores > 0 ? $", {Errores} con error" : "");
    }

    async System.Threading.Tasks.Task CorrerDiagnosticoDePrefijos()
    {
        try
        {
            Apunta("=== prefijos de atajos: cuál está libre en este equipo ===");
            Apunta($"ejecutable medido: {Environment.ProcessPath}  pid {Environment.ProcessId}");
            Apunta($"configuración en solo lectura={Config.SoloLectura}");
            Apunta("se registra y se suelta cada candidato. No se inyecta ninguna pulsación,");
            Apunta("no se mueve ninguna ventana y no se escribe la configuración.");
            Apunta($"prefijo de serie ahora: {HyprAtajos.Prefijo} " +
                   $"(segundo nivel: {HyprAtajos.PrefijoSegundo})");
            await System.Threading.Tasks.Task.Delay(2000);

            // El atajo de la vista general se declara reservado en cada prueba: es de
            // NubeBar y no se le puede quitar a sí misma.
            var reservados = new[] { _cfg.Barra.AtajoVistaGeneral ?? "" };

            var notas = new List<Nota>();

            foreach (var prefijo in HyprAtajos.Candidatos)
            {
                // Se suelta lo que hubiera antes de cada candidato. Sin esto, el segundo
                // candidato se encontraría ocupado por el primero —por NubeBar misma— y la
                // tabla diría que está cogido cuando no lo está.
                HyprTeclas.Quitar(_self);

                var crudo = HyprAtajos.ConPrefijo(prefijo);
                var mapa = HyprAtajos.Construir(crudo, reservados);

                var hechos = HyprTeclas.Registrar(_self, mapa);
                int entran = hechos.Count(h => h.Ok);
                int ocupados = hechos.Count(h => h.Como == HyprTeclas.Como.YaLoTieneOtro);
                int noEntiende = hechos.Count(h => h.Como == HyprTeclas.Como.NoSeEntiende);
                int errores = hechos.Count(h => h.Como == HyprTeclas.Como.Error);

                var detalle = hechos.Where(h => !h.Ok)
                                    .Select(h => $"{h.Combinacion} ({h.Nombre}): {h.Explicado}")
                                    .ToList();
                if (mapa.Problemas.Count > 0)
                    detalle.AddRange(mapa.Problemas.Select(p => "mapa: " + p));

                notas.Add(new Nota(prefijo, entran, ocupados, noEntiende, errores, detalle));

                Apunta($"[D8]  {prefijo,-18} segundo={HyprAtajos.SegundoDe(prefijo),-22} " +
                       $"{entran}/{hechos.Count} entran" +
                       (ocupados > 0 ? $"  ocupados={ocupados}" : "") +
                       (noEntiende > 0 ? $"  no se entienden={noEntiende}" : "") +
                       (errores > 0 ? $"  errores={errores}" : ""));
                foreach (var d in detalle) Apunta("        " + d);

                HyprTeclas.Quitar(_self);
                await System.Threading.Tasks.Task.Delay(300);
            }

            // ---------- La tabla y la recomendación ----------
            Apunta("--- resumen ---");
            foreach (var n in notas.OrderByDescending(n => n.Entran))
                Apunta($"  {n.Prefijo,-18} {n.Resumen}");

            var mejor = notas
                .Select((n, i) => (n, i))
                .OrderByDescending(x => x.n.Entran)
                .ThenBy(x => x.i)              // empate: el orden de la lista de candidatos
                .First().n;

            Apunta($"[D8]  recomendado: {mejor.Prefijo} ({mejor.Resumen})");
            if (mejor.Prefijo != HyprAtajos.Prefijo)
                Apunta($"[D8]  el de serie es {HyprAtajos.Prefijo} y la medición prefiere " +
                       $"{mejor.Prefijo}: hay que cambiar la constante `HyprAtajos.Prefijo`");
            else
                Apunta("[D8]  el de serie coincide con lo que dice la medición");

            // Y la comprobación que importa de verdad: que el atajo de la vista general
            // siga en pie después de todo el trajín.
            Apunta($"[D8]  atajos vivos al terminar: {HyprTeclas.Cuantos} (tiene que ser 0)");
            Apunta($"[D8]  atajo de la vista general «{_cfg.Barra.AtajoVistaGeneral}»: " +
                   $"{(Nativo.TraducirAtajo(_cfg.Barra.AtajoVistaGeneral ?? "", out _, out _) ? "sigue entendiéndose" : "NO se entiende")}");

            Apunta("[D8]  PENDIENTE DE TI: pulsar unos cuantos con el prefijo elegido. Que");
            Apunta("      Windows conceda el registro no significa que la combinación sea");
            Apunta("      cómoda ni que tu teclado la dé con una mano.");
        }
        catch (Exception ex)
        {
            Apunta($"[D8]  EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            Log.Error("el diagnóstico de prefijos", ex);
        }
        finally
        {
            // En `finally`: si algo revienta a mitad, lo que no puede quedar es un puñado de
            // combinaciones del usuario cogidas por un proceso que ya se fue.
            int soltados = HyprTeclas.Quitar(_self);
            Apunta($"[D8]  soltados al salir: {soltados}; vivos={HyprTeclas.Cuantos}");
            Application.Current.Shutdown(0);
        }
    }
}
