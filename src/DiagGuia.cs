using System;
using System.Linq;

namespace NubeBar;

// `NubeBar.exe --guia`: la guía de NubeHypr, para tocarla con las manos sin riesgo.
//
// ---------- Para qué ----------
//
// La guía de reglas se puede probar en lógica y fotografiar, pero lo que de verdad falta es
// usarla: escribir en los campos, pulsar los ocho ejemplos, probar reglas, ver los tooltips.
// Eso necesita manos.
//
// El problema es que hacerlo con una barra normal significa abrir la puerta a todo lo que una
// barra normal puede hacer: escribir el `config.json`, migrarlo a un esquema nuevo, registrar
// diecinueve atajos globales y —si se toca el interruptor— encender NubeHypr de verdad.
//
// Y eso no es hipotético. En esta sesión el config del usuario se migró de v3 a v4 y apareció
// con `Activo: true` después de que un binario de fase corriera como barra normal. Se pudo
// restaurar byte a byte desde el respaldo, pero el susto estaba servido.
//
// ---------- Qué garantiza este modo ----------
//
// 1. **No escribe el config.** Es una bandera, así que `Config.PuedeEscribir` es falso y
//    `ConfigArbol.Guardar` se niega. Estructural, no un cuidado que recordar.
// 2. **No migra.** `Config.Cargar` solo llama a `Esquema.MigrarFichero` si NO es solo
//    lectura. Un v3 sigue siendo un v3 después de abrir la guía.
// 3. **NubeHypr queda apagado en memoria**, aunque tu fichero lo tenga encendido. El
//    interruptor de Ajustes se puede tocar: cambia la copia en memoria y no llega a disco.
// 4. **No registra atajos globales.** Con la capa apagada no se registra ninguno —ya era
//    así— y además este modo lo fuerza, para que no dependa de lo que diga el fichero.
// 5. **No mueve ventanas.** Nada aquí llama al adaptador.
//
// ---------- Y lo que se ve ----------
//
// Un rótulo permanente: «Modo de demostración: ningún cambio se guarda». Un modo seguro que
// no se anuncia es un modo en el que el usuario no se atreve a tocar nada, y entonces no
// sirve para lo que se hizo.
// ---------- Y el guardia que faltaba ----------
//
// Esto no es de la guía, pero vive aquí porque es el mismo problema visto por tercera vez.
//
// Un binario de FASE —`dist-e8`, `dist-d6`…— conoce un esquema de configuración más nuevo que
// el instalado. Arrancado con bandera no puede escribir nada; arrancado **sin banderas** es
// una barra normal, y una barra normal migra el config. Eso es correcto para una barra de
// verdad y es un accidente esperando a pasar en una carpeta de desarrollo.
//
// Ha pasado tres veces:
//   1. Un `dist-d8` olvidado corriendo diez minutos.
//   2. Un doble clic en `dist-d8-unico`.
//   3. Un `dist-e8` arrancado para recuperar la barra después de que un diagnóstico dejara
//      al usuario sin ninguna.
//
// Las dos primeras se «arreglaron» con cuidado y con un nombre de carpeta disuasorio. No
// bastó, porque el cuidado no escala y el nombre no se lee cuando lo que quieres es tu barra
// de vuelta.
//
// Esto sí es estructural: un binario que vive en una carpeta de fase **se niega a arrancar
// como barra normal**. No impide nada legítimo —las banderas siguen funcionando, y la versión
// estable no está en una carpeta de fase— y cierra la puerta por la que se coló tres veces.
public static class Fase
{
    // ---------- La regla, al revés ----------
    //
    // La primera versión era una LISTA de prefijos de desarrollo: `dist-d`, `dist-e`,
    // `experimental`. Tiene la misma forma de fallo que la lista de banderas que ya se
    // invirtió en `Config.PuedeEscribir`, y falló igual de rápido: la revisión de los
    // cuatro problemas publicó en `dist-f1`, que no empieza por ninguno de los tres, y ese
    // binario —que conoce el esquema v4 con el config del usuario en v3— volvía a poder
    // arrancar como barra normal y migrarlo. El tercer accidente de la misma familia,
    // esperando a pasar por cuarta vez.
    //
    // Así que la pregunta correcta no es «¿esta carpeta es de desarrollo?» —hay que
    // acordarse de añadirla— sino «¿es una de las DOS carpetas estables?». Una carpeta de
    // fase nueva nace protegida sin que nadie haga nada.
    static readonly string[] LasEstables = { "dist", "dist-nuevo" };

    public static bool EsBinarioDeFase(string? ruta)
    {
        try
        {
            var carpeta = System.IO.Path.GetFileName(
                System.IO.Path.GetDirectoryName(ruta ?? "") ?? "");
            if (carpeta.Length == 0) return false;
            if (LasEstables.Any(e => carpeta.Equals(e, StringComparison.OrdinalIgnoreCase)))
                return false;
            return carpeta.StartsWith("dist", StringComparison.OrdinalIgnoreCase) ||
                   carpeta.StartsWith("experimental", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // Solo se niega cuando NO hay banderas: con bandera es un diagnóstico y no puede escribir.
    public static bool HayQueNegarse(string? ruta, string[]? args) =>
        EsBinarioDeFase(ruta) && args is not { Length: > 0 };

    public const string Explicacion =
        "Este binario está en una carpeta de fase (dist-d*, dist-e*, experimental*) y conoce\n" +
        "un esquema de configuración más nuevo que el que tienes instalado. Arrancarlo como\n" +
        "barra normal MIGRARÍA tu config.json sin preguntar.\n\n" +
        "Por eso no arranca. Lo que sí puedes hacer con él:\n" +
        "  --guia / --sololectura   la guía de reglas, sin poder guardar nada\n" +
        "  --pruebas                la batería de pruebas\n" +
        "  --capturas               una foto de cada superficie\n" +
        "  --vigilia 15             la barra funcionando un rato, midiéndose\n\n" +
        "Tu barra de siempre es dist-nuevo\\NubeBar.exe.";
}

public static class DiagGuia
{
    internal const string Bandera = "--guia";

    // El alias que pidió el usuario. Las dos banderas hacen lo mismo; «--guia» dice para qué
    // es y «--sololectura» dice qué garantiza, y según lo que uno tenga en la cabeza busca
    // una o la otra.
    internal const string BanderaAlias = "--sololectura";

    public static bool Encendido;

    public const string Rotulo = "Modo de demostración: ningún cambio se guarda";

    public static bool Es(string? a) =>
        string.Equals(a, Bandera, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(a, BanderaAlias, StringComparison.OrdinalIgnoreCase);

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || !Es(args[0])) return;
        Encendido = true;
    }

    // ---------- Lo que hay que dejar apagado, en memoria ----------
    //
    // Se toca la copia cargada, nunca el fichero. Y se hace en un sitio y no repartido por
    // la interfaz: si cada pantalla tuviera que acordarse de mirar `Encendido`, la que se
    // olvidara sería la que encendiera la capa.
    public static void Apagar(Config c)
    {
        if (!Encendido || c?.NubeHypr == null) return;

        bool estaba = c.NubeHypr.Activo;
        c.NubeHypr.Activo = false;

        // El modo se deja en simulación aunque el fichero diga «aplicar»: en demostración no
        // puede haber un camino que mueva ventanas de verdad, ni siquiera el de los atajos.
        string antes = c.NubeHypr.Modo ?? "";
        c.NubeHypr.Modo = "simulacion";

        Log.Info($"[GUÍA] modo de demostración: NubeHypr apagado en memoria " +
                 $"(el fichero decía activo={estaba} modo={antes}; el fichero NO se toca)");
    }
}
