using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace NubeBar;

// La migración v4→v5: de «tres colores más diez roles» a «un tema y lo que le has cambiado».
//
// ---------- Por qué esto necesita tantas pruebas ----------
//
// Es la única parte del rediseño que toca el fichero de otra persona, y no se puede pedir
// perdón: cuando se ejecuta, la configuración de colores que el usuario llevaba meses
// afinando o sobrevive o se pierde. El resto del plan se puede rehacer en la versión
// siguiente; esto, no.
//
// Las dos reglas que se comprueban, y que son las que la decidieron:
//
//   1. **Lo que el usuario eligió se conserva.** Clave por clave, no en bloque: quien solo
//      cambió el acento conserva el acento y estrena el resto del tema.
//   2. **Lo que nadie eligió NO se arrastra.** Un `#E6000000` que está ahí porque es el valor
//      por defecto no es una decisión, y arrastrarlo dejaría a todo el mundo con la isla negra
//      de siempre sin ver nunca el tema nuevo.
//
// Y una tercera que no es del rediseño sino de la casa: **no se borra ninguna clave**.
public static partial class Pruebas
{
    internal static void MigracionDelTema()
    {
        Grupo("fase 0: la migración del tema (v4→v5)");

        // Un config en la v4, con la sección `Tema` que se le pida. Los tres colores van con
        // los valores POR DEFECTO de entonces salvo que la prueba los cambie.
        static JsonObject V4(params (string Clave, JsonNode? Valor)[] tema)
        {
            var t = new JsonObject
            {
                ["Fondo"] = "#E6000000",
                ["Texto"] = "#FFFFFF",
                ["Acento"] = "#7AA2F7",
                ["Radio"] = 20.0,
                ["Estilo"] = "normal",
                ["Fuente"] = "Segoe UI",
            };
            foreach (var (k, v) in tema) t[k] = v;
            return new JsonObject { ["Version"] = 4, ["Tema"] = t };
        }

        static JsonObject Migrado(JsonObject raiz)
        {
            var r = Esquema.Migrar(raiz);
            if (!r.Vale) throw new InvalidOperationException(r.Resumen);
            return raiz["Tema"]!.AsObject();
        }

        static string? Color(JsonObject tema, string clave) =>
            tema["Colores"] is JsonObject c && c[clave] is JsonValue v ? v.GetValue<string>() : null;

        static int Cuantos(JsonObject tema) =>
            tema["Colores"] is JsonObject c ? c.Count : 0;

        // ---------- «normal» y «neón» pasan a Profesional ----------
        foreach (var estilo in new[] { "normal", "neon" })
        {
            var t = Migrado(V4(("Estilo", estilo)));
            Igual("profesional", t["Id"]!.GetValue<string>(), $"estilo «{estilo}» pasa a Profesional");
            Igual(0, Cuantos(t), $"y sin arrastrar colores: los de «{estilo}» eran los de serie");
        }

        // ---------- «nube» sin tocar nada pasa a Anime ----------
        //
        // Sus tres colores son los que `Config.Cargar` ponía en una instalación nueva, así que
        // no los eligió: son el tema «Nube» tal cual. Se va a Sakura, que es la variante de
        // Anime que la maqueta usa de cabecera, y NO se arrastra nada — los colores de Nube
        // encima de Sakura darían un tema que no es ninguno de los dos.
        var nube = Migrado(V4(("Estilo", "nube"), ("Fondo", "#E6101A2B"),
                              ("Texto", "#F4F7FA"), ("Acento", "#E87562")));
        Igual("sakura", nube["Id"]!.GetValue<string>(), "«nube» sin tocar colores pasa a Sakura");
        Igual(0, Cuantos(nube), "y sin arrastrar los colores de Nube");

        // ---------- «nube» con los colores tocados pasa a Profesional, con ellos ----------
        var nubeTocada = Migrado(V4(("Estilo", "nube"), ("Fondo", "#E6101A2B"),
                                    ("Texto", "#F4F7FA"), ("Acento", "#9B5DE5")));
        Igual("profesional", nubeTocada["Id"]!.GetValue<string>(),
              "«nube» con el acento cambiado pasa a Profesional");
        Igual("#9B5DE5", Color(nubeTocada, ClavesBase.Acento), "conservando su acento");
        Igual("#E6101A2B", Color(nubeTocada, ClavesBase.Superficie), "y su fondo");

        // ---------- Un tema entero del usuario se conserva, clave por clave ----------
        //
        // Gruvbox con sus retoques. Es el caso que de verdad importa: alguien que lleva meses
        // con su tema y actualiza NubeBar.
        var gruvbox = Migrado(V4(
            ("Fondo", "#E6282828"), ("Texto", "#EBDBB2"), ("Acento", "#FABD2F"),
            ("Superficie", "#3C3836"), ("SuperficieAlta", "#504945"), ("Borde", "#665C54"),
            ("TextoAtenuado", "#BDAE93"), ("Secundario", "#D65D0E"), ("Sistema", "#83A598"),
            ("Positivo", "#B8BB26"), ("Aviso", "#FE8019"), ("Negativo", "#FB4934")));

        Igual("profesional", gruvbox["Id"]!.GetValue<string>(), "un tema del usuario pasa a Profesional");
        Igual("#E6282828", Color(gruvbox, ClavesBase.Superficie), "con su fondo");
        Igual("#EBDBB2", Color(gruvbox, ClavesBase.Tinta), "su texto");
        Igual("#FABD2F", Color(gruvbox, ClavesBase.Acento), "su acento");
        Igual("#3C3836", Color(gruvbox, ClavesBase.Tarjeta), "su superficie, que ahora es «tarjeta»");
        Igual("#504945", Color(gruvbox, "tarjeta.fondo.raton"), "y la de debajo del ratón");
        Igual("#665C54", Color(gruvbox, ClavesBase.Filo), "su borde, que ahora es «filo»");
        Igual("#BDAE93", Color(gruvbox, ClavesBase.Tenue), "su texto atenuado, que ahora es «tenue»");
        Igual("#D65D0E", Color(gruvbox, ClavesBase.Sec1), "su secundario");
        Igual("#83A598", Color(gruvbox, ClavesBase.Sec2), "su color de sistema");
        Igual("#B8BB26", Color(gruvbox, ClavesBase.Ok), "su positivo");
        Igual("#FE8019", Color(gruvbox, "aviso"), "su aviso");
        Igual("#FB4934", Color(gruvbox, ClavesBase.Peligro), "y su negativo");
        Igual(12, Cuantos(gruvbox), "las doce claves, ni una más ni una menos");

        // Y lo que se conserva se ENTIENDE: no vale guardar algo que luego la cascada ignora.
        var pintura = new Pintura(TemasDeSerie.De(gruvbox["Id"]!.GetValue<string>()),
                                  gruvbox["Colores"]!.AsObject()
                                      .ToDictionary(p => p.Key, p => p.Value!.GetValue<string>()));
        Igual("#FFFABD2F", Relleno.Hex(pintura.Color(ClavesBase.Acento)),
              "y el acento migrado es el que de verdad se pinta");

        // ---------- Clave por clave: quien solo cambió una conserva una ----------
        var soloAcento = Migrado(V4(("Acento", "#FF8FB8")));
        Igual(1, Cuantos(soloAcento), "quien solo cambió el acento conserva una sola clave");
        Igual("#FF8FB8", Color(soloAcento, ClavesBase.Acento), "y es el acento");

        // Los roles en blanco —que es como los deja Ajustes cuando no los usas— no cuentan
        // como elegidos. Sin esto, todo el mundo acabaría con doce claves vacías.
        var vacios = Migrado(V4(("Superficie", ""), ("Borde", "   "), ("Secundario", "")));
        Igual(0, Cuantos(vacios), "los roles en blanco no se arrastran");

        // ---------- El radio ----------
        var radioDeSerie = Migrado(V4(("Radio", 20.0)));
        Vale(radioDeSerie["Radios"] is JsonObject { Count: 0 },
             "el radio de serie (20) no se arrastra: nadie lo eligió");

        var radioTocado = Migrado(V4(("Radio", 28.0)));
        Igual(28.0, radioTocado["Radios"]!["isla"]!.GetValue<double>(), "un radio cambiado sí");
        Igual(28.0, radioTocado["Radios"]!["panel"]!.GetValue<double>(), "para la isla y para el panel");

        // ---------- La letra (v5→v6) ----------
        //
        // Mismo criterio que con los colores y el radio: se arrastra la que el usuario ELIGIÓ.
        // «Segoe UI» era la de serie, así que arrastrarla dejaría a todo el mundo con ella y
        // sin ver nunca la letra del tema — y la letra es media identidad de Cyberpunk.
        var letraDeSerie = Migrado(V4(("Fuente", "Segoe UI")));
        Vale(letraDeSerie["Letras"] is JsonObject { Count: 0 },
             "la letra de serie no se arrastra: nadie la eligió");

        var letraMia = Migrado(V4(("Fuente", "Consolas")));
        Igual("Consolas", letraMia["Letras"]!["texto"]!.GetValue<string>(),
              "una letra elegida a mano sí se conserva");

        var sinLetra = Migrado(V4(("Fuente", "   ")));
        Vale(sinLetra["Letras"] is JsonObject { Count: 0 }, "y una en blanco tampoco se arrastra");

        // ---------- No se borra NADA ----------
        //
        // Es la regla de la casa, y la que más caro ha salido aprender. `Estilo` «desaparece»
        // de donde importa —nadie lo lee para pintar— pero del fichero de alguien no se toca.
        var intacto = Migrado(V4(("Acento", "#112233"), ("LoMio", "no me toques")));
        foreach (var vieja in new[] { "Fondo", "Texto", "Acento", "Radio", "Estilo", "Fuente" })
            Vale(intacto[vieja] != null, $"la clave «{vieja}» sigue en el fichero");
        Igual("no me toques", intacto["LoMio"]!.GetValue<string>(),
              "y una clave que esta versión no conoce, también");

        // ---------- Migrar dos veces no cambia nada ----------
        //
        // Pasa de verdad: restauras una copia, vuelves a abrir, y el motor recorre nada. Si
        // no fuera idempotente, la segunda pasada pisaría con los valores viejos lo que el
        // usuario haya cambiado entre medias desde Ajustes.
        var dosVeces = V4(("Acento", "#112233"));
        Migrado(dosVeces);
        var tema = dosVeces["Tema"]!.AsObject();
        tema["Colores"]!["acento"] = "#ABCDEF";          // el usuario lo cambia luego
        tema["Id"] = "cyberpunk";                         // y se cambia de tema
        var otraVez = Esquema.Migrar(dosVeces);
        Vale(otraVez.Vale, "migrar un config ya migrado no falla");
        Igual("#ABCDEF", Color(dosVeces["Tema"]!.AsObject(), ClavesBase.Acento),
              "y no pisa lo que el usuario cambió después");
        Igual("cyberpunk", dosVeces["Tema"]!["Id"]!.GetValue<string>(), "ni el tema que eligió");

        // ---------- Un config sin sección Tema ----------
        var sinTema = new JsonObject { ["Version"] = 4 };
        var creada = Migrado(sinTema);
        Igual("profesional", creada["Id"]!.GetValue<string>(),
              "un config sin sección Tema sale con Profesional");

        // ---------- Y el resultado pasa la validación del esquema ----------
        //
        // `Esquema.Mal` es la red que impide escribir un árbol migrado que haya quedado
        // absurdo: sin versión, con una versión del futuro, o con menos secciones de las que
        // tenía. Que esta migración la pase no es automático: añade dos claves y toca una
        // sección, y «puede crecer, nunca encoger» es exactamente lo que vigila.
        var paraValidar = V4(("Acento", "#112233"));
        int antes = paraValidar.Count;
        Esquema.Migrar(paraValidar);
        var mal = Esquema.Mal(paraValidar, antes);
        Vale(mal == null, $"el árbol migrado pasa la validación del esquema ({mal ?? "bien"})");
    }
}
