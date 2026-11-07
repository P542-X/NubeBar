using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D3, cerrando la deuda: los atajos registrados de verdad en Windows.
//
// ---------- Lo que faltaba ----------
//
// D3 dejó el mapa, la validación y los comandos, pero nadie llamaba a `RegisterHotKey`:
// pulsar `Win+Alt+→` no hacía nada. Y el conflicto que detectaba el mapa era aritmética
// sobre una tabla — quién tiene cogida de verdad una combinación en ESTE equipo solo lo
// dice Windows devolviendo 1409.
//
// ---------- Las cuatro cosas que hay que no estropear ----------
//
//  1. **No duplicar al recargar.** `RegisterHotKey` con un id que ya está registrado
//     falla; y si se cambia la configuración y se vuelve a registrar sin soltar lo
//     anterior, quedan atajos vivos que ya no están en el mapa. Así que registrar
//     SIEMPRE empieza por soltar: `Quitar` y después `Registrar`, en ese orden.
//
//  2. **Soltarlos todos al cerrar.** Un atajo global que sobrevive al proceso se queda
//     cogido hasta reiniciar la sesión, y el usuario se encuentra una tecla que no hace
//     nada y no sabe por qué. Se guarda qué ids se registraron —no se supone— y se
//     sueltan uno a uno.
//
//  3. **No pisar `Ctrl+Alt+W`.** Es el atajo de la vista general de NubeBar. Va como
//     reservado al construir el mapa, así que se rechaza antes de llegar aquí, y además
//     los ids viven en otro rango: ni por id ni por combinación se pueden cruzar.
//
//  4. **Nada se registra con la capa apagada.** NubeHypr nace apagada, y quedarse con 19
//     atajos globales del usuario "por si acaso" sería lo contrario de apagada.
public static class HyprTeclas
{
    // ---------- El rango de ids ----------
    //
    // `IdAtajoVista` es 0xB0CA. Estos empiezan en 0xB100 y suben de uno en uno: 19 caben
    // de sobra antes de acercarse a nada. Un rango propio no es ceremonia — es lo que
    // permite que el hook pregunte "¿este id es mío?" sin una lista de excepciones.
    public const int IdBase = 0xB100;
    public const int IdTope = IdBase + 63;

    public static bool EsNuestro(int id) => id >= IdBase && id <= IdTope;

    // ---------- Lo que pasó con cada atajo ----------
    public enum Como { Registrado, YaLoTieneOtro, NoSeEntiende, Error }

    public readonly record struct Resultado(
        int Id, string Combinacion, string Nombre, Como Como, int CodigoDeWindows)
    {
        public bool Ok => Como == Como.Registrado;

        public string Explicado => Como switch
        {
            Como.Registrado => "registrado",
            Como.YaLoTieneOtro => "ya lo tiene cogido otra aplicación",
            Como.NoSeEntiende => "Windows no entiende la combinación",
            _ => $"error {CodigoDeWindows}",
        };

        public override string ToString() => $"{Combinacion} -> {Nombre}: {Explicado}";
    }

    // La traducción del código de Windows. 1409 es ERROR_HOTKEY_ALREADY_REGISTERED y es
    // **el caso interesante**: significa que el atajo es bueno pero alguien se le adelantó.
    // Confundirlo con un error genérico haría que el log no distinguiera "mal escrito" de
    // "ocupado", que son dos problemas con dos soluciones distintas.
    public static Como Clasificar(bool ok, int codigo) =>
        ok ? Como.Registrado
        : codigo == 1409 ? Como.YaLoTieneOtro
        : codigo == -1 ? Como.NoSeEntiende
        : Como.Error;

    // ---------- Qué hay registrado ahora mismo ----------
    //
    // Se guarda. No se supone ni se recalcula del mapa: si la configuración cambió entre
    // registrar y soltar, recalcular soltaría los ids nuevos y dejaría vivos los viejos.
    static readonly Dictionary<int, Resultado> _vivos = new();

    public static int Cuantos => _vivos.Count;
    public static IEnumerable<Resultado> Vivos => _vivos.Values;

    public static string? NombreDe(int id) => _vivos.TryGetValue(id, out var r) ? r.Nombre : null;

    // ================= Registrar =================
    //
    // Empieza soltando lo que hubiera: es lo que hace que recargar la configuración veinte
    // veces no deje veinte atajos.
    //
    // Devuelve el resultado de cada uno, incluidos los que no se pudieron. Los que fallan
    // NO se guardan como vivos —no hay nada que soltar— pero sí se devuelven, porque el
    // usuario tiene que poder enterarse de que su atajo está ocupado.
    public static List<Resultado> Registrar(IntPtr ventana, HyprAtajos.Mapa mapa)
    {
        var hechos = new List<Resultado>();
        if (ventana == IntPtr.Zero)
        {
            Log.Info("atajos de NubeHypr: no hay ventana donde registrarlos");
            return hechos;
        }

        Quitar(ventana);        // SIEMPRE, antes de nada. Ver el punto 1 de arriba.

        int id = IdBase;
        foreach (var (combinacion, nombre) in mapa.Atajos.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (id > IdTope)
            {
                Log.Info($"atajos de NubeHypr: no caben más de {IdTope - IdBase + 1}; " +
                         $"«{combinacion}» se queda fuera");
                break;
            }

            bool ok = Nativo.RegistrarAtajo(ventana, id, combinacion);
            var r = new Resultado(id, combinacion, nombre,
                                  Clasificar(ok, Nativo.UltimoErrorAtajo), Nativo.UltimoErrorAtajo);
            hechos.Add(r);
            if (ok) _vivos[id] = r;
            id++;
        }

        int bien = hechos.Count(h => h.Ok);
        int ocupados = hechos.Count(h => h.Como == Como.YaLoTieneOtro);
        Log.Info($"atajos de NubeHypr: {bien} de {hechos.Count} registrados" +
                 (ocupados > 0 ? $", {ocupados} ya los tiene otra aplicación" : ""));
        foreach (var h in hechos.Where(x => !x.Ok)) Log.Info("  atajo NO registrado: " + h);
        return hechos;
    }

    // ================= Soltar =================
    //
    // Devuelve cuántos soltó. Idempotente: llamarlo dos veces no falla y la segunda
    // devuelve cero. Es lo que permite ponerlo en el cierre sin preguntar si hacía falta.
    public static int Quitar(IntPtr ventana)
    {
        if (_vivos.Count == 0) return 0;
        int cuantos = 0;
        foreach (var id in _vivos.Keys.ToList())
        {
            try { Nativo.QuitarAtajo(ventana, id); cuantos++; }
            catch (Exception ex) { Log.Error($"soltar el atajo {id}", ex); }
        }
        _vivos.Clear();
        if (cuantos > 0) Log.Info($"atajos de NubeHypr: {cuantos} soltados");
        return cuantos;
    }

    // Solo para las pruebas: olvida el registro sin llamar a Windows. Hace falta porque
    // una prueba que registrara de verdad cogería atajos globales del equipo de quien la
    // ejecuta.
    internal static void OlvidarTodo() => _vivos.Clear();

    // ================= El mapa que de verdad se usa =================
    //
    // Junta las tres fuentes en el orden correcto: lo que el usuario puso, lo de serie si
    // no puso nada, y lo reservado que no se puede pisar.
    //
    // `Ctrl+Alt+W` —o lo que el usuario tenga puesto como atajo de la vista general— entra
    // como reservado. Así se rechaza al construir el mapa, con su problema explicado, y no
    // en `RegisterHotKey` con un 1409 que parecería culpa de otra aplicación.
    public static HyprAtajos.Mapa MapaDe(OpcionesHypr opciones, string? atajoDeLaVista)
    {
        var crudo = opciones.Atajos.Count > 0
            ? new Dictionary<string, string>(opciones.Atajos, StringComparer.OrdinalIgnoreCase)
            : HyprAtajos.DeSerie();

        var reservados = new List<string>();
        if (!string.IsNullOrWhiteSpace(atajoDeLaVista)) reservados.Add(atajoDeLaVista);

        var mapa = HyprAtajos.Construir(crudo, reservados);
        foreach (var p in mapa.Problemas) Log.Info("atajos de NubeHypr: " + p);
        return mapa;
    }
}
