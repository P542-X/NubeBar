using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// D7: cuando una aplicación se va y vuelve, dónde estaba.
//
// ---------- Qué problema es ----------
//
// El documento lo pide así: *"restauracion de posiciones anteriores cuando una aplicacion
// reaparece"*. El caso real es el de siempre: tienes el editor colocado a la izquierda, se
// actualiza y se reinicia solo, y vuelve en el sitio que le dé a Windows. Lo que había
// antes se ha perdido, porque la memoria de D3 está indexada por `hwnd` y el `hwnd` de la
// ventana nueva es otro.
//
// ---------- Por qué hace falta otra memoria y no vale la de D3 ----------
//
// `HyprRestaurar.Memoria` recuerda "esta ventana estaba maximizada antes de organizarla", y
// está bien así: su clave es el handle porque lo que recuerda vale mientras esa ventana
// viva. Esta memoria recuerda otra cosa —"las ventanas de este programa suelen estar aquí"—
// y tiene que sobrevivir a que el programa se cierre, así que su clave no puede ser un
// handle.
//
// ---------- Y lo que NO hace ----------
//
// **No mueve nada.** Decide y devuelve la decisión; moverla es del adaptador, y solo en modo
// "aplicar", que está apagado de serie. En simulación se apunta en el registro y se queda
// ahí. Es la misma disciplina de D5.1 y por el mismo motivo: una ventana que se mueve sola
// sin que lo hayas pedido asusta más que una que no se mueve.
public static class HyprRegreso
{
    // ================= La huella de una aplicación =================
    //
    // Ejecutable y clase de ventana. **No el título**: un navegador cambia de título cada
    // vez que cambias de pestaña, y entonces cada pestaña sería una aplicación distinta y
    // la memoria no acertaría nunca.
    //
    // Y la clase sí entra, porque un mismo programa tiene ventanas que no son lo mismo: la
    // principal de Explorer (`CabinetWClass`) y su cuadro de copiar archivos no deberían
    // compartir sitio recordado.
    public static string Huella(string exe, string clase)
    {
        var nombre = NubeHypr.NombreDeProceso(exe ?? "");
        return (nombre + "|" + (clase ?? "")).ToLowerInvariant();
    }

    public sealed record Sitio(string Huella, NubeHypr.Rect Donde, bool Maximizada, DateTime Cuando)
    {
        public TimeSpan Edad => DateTime.Now - Cuando;
    }

    // ---------- Los dos topes ----------
    //
    // Sin ellos esto es una fuga: una sesión de días con decenas de programas acumula
    // entradas que nunca se borran, y encima acabaría restaurando posiciones de hace una
    // semana que ya no tienen sentido.
    //
    // Dos horas es la medida de "lo que se cerró hace un rato": cubre una actualización,
    // un reinicio del programa o un cierre por error, y no cubre "ayer trabajé así".
    public static readonly TimeSpan Caducidad = TimeSpan.FromHours(2);
    public const int MaxSitios = 64;

    public sealed class Memoria
    {
        readonly Dictionary<string, Sitio> _sitios = new();

        public int Cuantas => _sitios.Count;
        public IEnumerable<Sitio> Todo => _sitios.Values;

        // Se apunta cuando la ventana DESAPARECE, no cuando aparece: lo que interesa es
        // dónde estaba la última vez que se la vio, y eso solo se sabe al perderla.
        public void Apuntar(string huella, NubeHypr.Rect donde, bool maximizada, DateTime? cuando = null)
        {
            if (huella.Length == 0 || !donde.Valido) return;
            _sitios[huella] = new Sitio(huella, donde, maximizada, cuando ?? DateTime.Now);

            // Si se pasa del tope, se tira la más vieja. Tirar la más vieja y no la última
            // en entrar: lo que se quiere conservar es lo reciente.
            if (_sitios.Count > MaxSitios)
            {
                var vieja = _sitios.Values.OrderBy(s => s.Cuando).First();
                _sitios.Remove(vieja.Huella);
            }
        }

        public Sitio? Buscar(string huella, DateTime? ahora = null)
        {
            if (!_sitios.TryGetValue(huella, out var s)) return null;
            var edad = (ahora ?? DateTime.Now) - s.Cuando;
            if (edad > Caducidad) { _sitios.Remove(huella); return null; }
            return s;
        }

        // Una vez usada se olvida. Si no se olvidara, cada ventana nueva del programa
        // iría al mismo sitio y acabarían todas apiladas una encima de otra.
        public Sitio? Gastar(string huella, DateTime? ahora = null)
        {
            var s = Buscar(huella, ahora);
            if (s != null) _sitios.Remove(huella);
            return s;
        }

        public int Caducar(DateTime? ahora = null)
        {
            var corte = ahora ?? DateTime.Now;
            var viejas = _sitios.Where(p => corte - p.Value.Cuando > Caducidad)
                                .Select(p => p.Key).ToList();
            foreach (var k in viejas) _sitios.Remove(k);
            return viejas.Count;
        }

        public void Vaciar() => _sitios.Clear();
    }

    // ================= ¿Cabe todavía ahí? =================
    //
    // ---------- El caso que esto evita ----------
    //
    // Tenías el editor en el monitor de la derecha. Lo cierras, desconectas ese monitor, y
    // lo vuelves a abrir. La posición recordada está en coordenadas que ya no existen: la
    // ventana se restauraría **fuera de la pantalla**, y el usuario tendría una ventana
    // abierta que no puede ver ni alcanzar con el ratón.
    //
    // Por eso un sitio recordado solo vale si sigue cayendo dentro de algún monitor. Y no
    // "tocando": hace falta que esté dentro de verdad lo bastante para poder cogerla por la
    // barra de título.
    public const double MinimoVisible = 120;

    public static bool SigueCabiendo(NubeHypr.Rect donde, IReadOnlyList<NubeHypr.Rect> areas)
    {
        if (!donde.Valido || areas.Count == 0) return false;

        foreach (var a in areas)
        {
            // Cuánto de la ventana cae dentro de este monitor
            double x1 = Math.Max(donde.X, a.X), y1 = Math.Max(donde.Y, a.Y);
            double x2 = Math.Min(donde.X + donde.Ancho, a.X + a.Ancho);
            double y2 = Math.Min(donde.Y + donde.Alto, a.Y + a.Alto);
            if (x2 - x1 >= MinimoVisible && y2 - y1 >= MinimoVisible) return true;
        }
        return false;
    }

    // ================= La decisión =================
    //
    // Devuelve qué habría que hacer, y el motivo — también cuando la respuesta es "nada".
    // El motivo no es decoración: "mi editor no volvió a su sitio" se contesta leyendo por
    // qué, y sin el motivo hay que adivinar entre cinco causas.
    public enum Que { Nada, Devolver }

    public sealed record Decision(Que Hacer, NubeHypr.Rect Destino, string Porque)
    {
        public static Decision No(string porque) =>
            new(Que.Nada, default, porque);

        public override string ToString() => Hacer == Que.Nada
            ? $"no se devuelve: {Porque}"
            : $"devolver a {Destino.X:0},{Destino.Y:0} {Destino.Ancho:0}x{Destino.Alto:0} ({Porque})";
    }

    public static Decision Decidir(VentanaModelo v, string clase, string ruta,
                                   Memoria memoria, IReadOnlyList<NubeHypr.Rect> areas,
                                   NubeHypr.Modo modo, DateTime? ahora = null)
    {
        // Apagado no decide nada. Va primero: ni siquiera se consulta la memoria, porque
        // `Gastar` la modifica y un diagnóstico en modo apagado no debe tocar nada.
        if (modo == NubeHypr.Modo.Apagado) return Decision.No("la capa está apagada");

        // Una ventana minimizada no se coloca: restaurarla para ponerla en su sitio sería
        // desminimizar algo que el usuario dejó minimizado.
        if (v.Minimizada) return Decision.No("la ventana está minimizada");

        var huella = Huella(ruta, clase);
        if (huella.Length <= 1) return Decision.No("no se sabe de qué programa es");

        var sitio = memoria.Buscar(huella, ahora);
        if (sitio == null) return Decision.No($"no hay sitio recordado para «{huella}»");

        if (!SigueCabiendo(sitio.Donde, areas))
            return Decision.No($"el sitio recordado ya no cae en ninguna pantalla " +
                               $"({sitio.Donde.X:0},{sitio.Donde.Y:0} " +
                               $"{sitio.Donde.Ancho:0}x{sitio.Donde.Alto:0})");

        // Si ya está donde estaba, no se toca. Mover una ventana a donde ya está es un
        // `SetWindowPos` que no hace nada visible pero sí puede robarle el foco.
        if (v.Rectangulo.CasiIgual(sitio.Donde, 8))
            return Decision.No("ya está donde estaba");

        memoria.Gastar(huella, ahora);
        return new Decision(Que.Devolver, sitio.Donde,
                            $"«{huella}» estaba ahí hace {sitio.Edad.TotalMinutes:0.#} min");
    }

    // ---------- El renglón del registro ----------
    //
    // Con el formato de D7 que pide el documento: fase, hwnd, proceso, efecto y resultado.
    // Y `modo=` siempre, para que nadie lea esta línea como "esto se ha aplicado".
    public static string Linea(VentanaModelo v, Decision d, NubeHypr.Modo modo) =>
        $"[D7]  regreso hwnd=0x{v.Hwnd.ToInt64():X6} proceso={v.Proceso} " +
        $"efecto={(d.Hacer == Que.Devolver ? "devolver" : "nada")} " +
        $"resultado={(d.Hacer == Que.Devolver && modo == NubeHypr.Modo.Aplicar ? "aplicado" : "solo apuntado")} " +
        $"modo={modo.ToString().ToLowerInvariant()}  {d}";
}
