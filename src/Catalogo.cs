using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace NubeBar;

// El catálogo de colores: UNA CLAVE POR CADA COSA QUE SE PINTA.
//
// ---------- Lo que esto resuelve ----------
//
// «Cualquier color de cualquier elemento de la app se puede cambiar. Sin excepciones»
// (`docs/REDISENO-VISUAL.md`, decisión B). Para que eso sea verdad hace falta que cada color
// de la interfaz TENGA UN NOMBRE: si el coral de «Apagar» está escrito como
// `Brushes.Tomato` dentro de una función, no hay forma de ofrecérselo al usuario, porque no
// existe en ninguna lista.
//
// Así que aquí está la lista. Cada entrada es una clave estable y legible —`energia.apagar`,
// `deslizador.pomo`, `reloj.texto`— con su zona (para agruparla en Ajustes), su rótulo, su
// papel (si hace de texto, de icono o de relleno, que es lo que decide cuánto contraste hay
// que exigirle) y, lo importante, DE DÓNDE SALE si nadie la toca.
//
// ---------- La cascada ----------
//
// El valor de una clave sale, por orden:
//
//   1. de lo que haya puesto el usuario (`Tema.Colores` en el config);
//   2. si no, de lo que diga el tema (los 9 de serie declaran 19 claves base; uno del usuario
//      puede declarar las que quiera, incluidas las de aquí);
//   3. si no, de la DERIVACIÓN que está escrita en esta tabla.
//
// Quien resuelve la cascada es `Pintura`. Aquí solo está el tercer escalón.
//
// Por eso un tema puede traer cuatro colores y que todo salga coherente: las ~120 claves de
// la interfaz se derivan de las 19 base, y las 19 base se derivan de `tinta`, `superficie` y
// `acento` cuando el tema calla.
//
// ---------- Y por qué las derivaciones piden a `Pintura` y no al tema ----------
//
// Porque una derivación encadena: `boton.fondo.raton` sale de `boton.fondo`, que sale de
// `tarjeta`, que sale del tema. Si cada derivación tuviera que mirar el tema en bruto,
// tocar `tarjeta` en el Estudio no movería el botón, y el usuario vería que «cambiar el fondo
// de las tarjetas no cambia los botones», que es mentirle con un selector de color.

public enum ZonaColor { Comun, Isla, Inicio, Paneles, Laterales, Dock, Avisos, Vista, Ajustes }

// Qué hace ese color, que es lo que decide cuánto contraste hay que exigirle. Un filo de 1 px
// a propósito tenue no puede medirse con la vara del texto: el aviso saltaría siempre y
// dejaría de significar nada.
public enum Papel
{
    Relleno,   // un fondo, un velo, una barra: no se le exige contraste
    Texto,     // 4,5:1 sobre su fondo (AA de la WCAG para texto normal)
    Icono,     // 3:1 sobre su fondo (AA para elementos gráficos)
    Filo,      // un borde: ni se mide ni se avisa
    Efecto,    // halos, destellos, sombras: se ven, no se leen
}

public sealed record ClaveColor(
    string Clave,
    ZonaColor Zona,
    string Rotulo,
    Papel Papel,
    string? Sobre,                       // la clave del fondo sobre el que se pinta, si lo hay
    Func<Pintura, Relleno> Derivar)
{
    // El mínimo que se le exige, o 0 si no se le exige nada.
    public double MinimoContraste => Papel switch
    {
        Papel.Texto => 4.5,
        Papel.Icono => 3.0,
        _ => 0,
    };

    public bool SeMide => MinimoContraste > 0 && Sobre is not null;
}

public static class Catalogo
{
    static ClaveColor[]? _todas;
    static Dictionary<string, ClaveColor>? _porClave;

    public static IReadOnlyList<ClaveColor> Todas => _todas ??= Montar();

    public static ClaveColor? Por(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;
        _porClave ??= Todas.ToDictionary(c => c.Clave, StringComparer.OrdinalIgnoreCase);
        return _porClave.TryGetValue(clave.Trim(), out var c) ? c : null;
    }

    public static bool Existe(string? clave) => Por(clave) is not null;

    // Agrupadas y en el orden en que las enseña Ajustes > Colores: primero lo común, que es
    // lo que cambia el 90 % de la gente, y luego zona por zona.
    public static IEnumerable<IGrouping<ZonaColor, ClaveColor>> PorZona() =>
        Todas.GroupBy(c => c.Zona).OrderBy(g => (int)g.Key);

    public static string RotuloDeZona(ZonaColor z) => z switch
    {
        ZonaColor.Comun => "Colores base",
        ZonaColor.Isla => "La isla",
        ZonaColor.Inicio => "El menú Inicio",
        ZonaColor.Paneles => "Los paneles",
        ZonaColor.Laterales => "Los laterales",
        ZonaColor.Dock => "El dock",
        ZonaColor.Avisos => "Los avisos",
        ZonaColor.Vista => "La vista general",
        ZonaColor.Ajustes => "La ventana de Ajustes",
        _ => z.ToString(),
    };

    // ================= Atajos para escribir la tabla =================

    static ClaveColor C(string clave, ZonaColor z, string rotulo, Func<Pintura, Relleno> derivar,
                        Papel papel = Papel.Relleno, string? sobre = null) =>
        new(clave, z, rotulo, papel, sobre, derivar);

    // «lo mismo que esa otra clave»: el caso más común y el que hace que el Estudio se sienta
    // coherente — cambias `tarjeta` y se mueve todo lo que cuelga de ella.
    static Func<Pintura, Relleno> Igual(string otra) => p => p.R(otra);

    // Esa otra clave con menos opacidad. Los velos de la interfaz (el fondo del botón bajo el
    // ratón, el relleno de lo elegido) son esto y no colores nuevos.
    static Func<Pintura, Relleno> Velo(string otra, double opacidad) =>
        p => p.R(otra).ConOpacidad(opacidad);

    // Esa clave acercada a otra. Para apagar un color (hacia el fondo) o subirlo (hacia la
    // tinta) sin inventar un hexadecimal.
    // El destino se pide OPACO: acercar un texto a un fondo translúcido daría un color que
    // depende de nada, porque un `#1EFFFFFF` no es claro ni oscuro hasta que se compone.
    static Func<Pintura, Relleno> Hacia(string otra, string destino, double k) =>
        p => Relleno.Liso(Contraste.Mezclar(p.Color(otra), p.ColorOpaco(destino), k));

    // Un color que tiene que LEERSE encima de otro: el mismo tono, corregido lo justo.
    static Func<Pintura, Relleno> TintaDe(string otra, string sobre, double minimo = 4.5) =>
        p => Relleno.Liso(Contraste.Legible(p.Color(otra), p.ColorOpaco(sobre), minimo));

    // ---------- Apagar un color, pero no hasta que desaparezca ----------
    //
    // «Atenuado» y «desactivado» son lo mismo con dos destinos: acercar la tinta al fondo.
    // Y tienen el mismo peligro: en un tema CLARO la tinta es oscura y el fondo es casi
    // blanco, así que un 35 % de acercamiento deja un gris que ya no se lee. Lo midieron las
    // pruebas en Oficina, Invierno, Sakura y Otoño: entre 2,5 y 3,0:1 donde hacen falta 3.
    //
    // Así que se apaga y luego se devuelve lo justo para llegar al mínimo. El resultado es un
    // texto desactivado que se distingue del activo Y que se puede leer, que es lo que un
    // botón desactivado tiene que ser: no pulsable, no invisible.
    static Func<Pintura, Relleno> HaciaLegible(string otra, string destino, double k, double minimo) =>
        p =>
        {
            var fondo = p.ColorOpaco(destino);
            return Relleno.Liso(Contraste.Legible(Contraste.Mezclar(p.Color(otra), fondo, k), fondo, minimo));
        };

    // ================= La tabla =================

    static ClaveColor[] Montar() => Comunes()
        .Concat(Isla()).Concat(Inicio()).Concat(Paneles()).Concat(Laterales())
        .Concat(Dock()).Concat(Avisos()).Concat(Vista()).Concat(Ajustes())
        .ToArray();

    // ---------- Las 19 base ----------
    //
    // Son claves del catálogo como todas las demás: el usuario puede editarlas en Ajustes >
    // Colores, y cambiar `acento` mueve de golpe las treinta cosas que lo derivan. Su
    // derivación es el último recurso, para un tema del usuario que solo declare cuatro
    // colores; los nueve de serie las declaran todas, así que en la práctica nunca se usa.
    static IEnumerable<ClaveColor> Comunes() => new[]
    {
        C(ClavesBase.Tinta, ZonaColor.Comun, "Texto principal",
          p => Relleno.Liso(p.Tema.Claro ? Color.FromRgb(0x1F, 0x27, 0x33) : Color.FromRgb(0xE6, 0xE9, 0xF2)),
          Papel.Texto, ClavesBase.Superficie),

        C(ClavesBase.Tenue, ZonaColor.Comun, "Texto secundario",
          Hacia(ClavesBase.Tinta, ClavesBase.Superficie, 0.35), Papel.Texto, ClavesBase.Tarjeta),

        C(ClavesBase.Superficie, ZonaColor.Comun, "Fondo de la isla",
          p => Relleno.Liso(p.Tema.Claro ? Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF)
                                         : Color.FromArgb(0xF0, 0x0C, 0x0E, 0x14))),

        C(ClavesBase.PanelFondo, ZonaColor.Comun, "Fondo de los paneles", Igual(ClavesBase.Superficie)),

        C(ClavesBase.Tarjeta, ZonaColor.Comun, "Fondo de las tarjetas",
          // Un velo de la TINTA, no un color opaco: así una tarjeta deja pasar un poco el
          // fondo de pantalla igual que la isla, y vale para temas claros y oscuros sin
          // preguntar (en un tema claro la tinta es oscura, y el velo sale oscuro).
          Velo(ClavesBase.Tinta, 0.12)),

        C(ClavesBase.Filo, ZonaColor.Comun, "Filo de las tarjetas",
          Velo(ClavesBase.Tinta, 0.23), Papel.Filo),

        C(ClavesBase.FiloIsla, ZonaColor.Comun, "Filo de la isla", Igual(ClavesBase.Filo), Papel.Filo),
        C(ClavesBase.FiloPanel, ZonaColor.Comun, "Filo de los paneles", Igual(ClavesBase.Filo), Papel.Filo),

        C(ClavesBase.Acento, ZonaColor.Comun, "Acento",
          p => Relleno.Liso(Color.FromRgb(0x8D, 0xA9, 0xE8))),

        C(ClavesBase.AcentoTinta, ZonaColor.Comun, "Acento sobre fondo (texto)",
          TintaDe(ClavesBase.Acento, ClavesBase.Tarjeta), Papel.Texto, ClavesBase.Tarjeta),

        // ---------- Por qué a esta se le exige 3:1 y no 4,5 ----------
        //
        // `sobre-acento` rotula un CONTROL RELLENO: el glifo de play dentro de su círculo, la
        // cifra de la insignia, la palabra de un segmento activo. No es un párrafo, y la vara
        // del texto corrido no es la suya — con 4,5 el aviso saltaría en Otoño (3,00) e
        // Invierno (3,45), que son valores de la maqueta aprobada y se ven perfectamente.
        //
        // Lo que NO se hace es apagar la medida: a 3:1 Sakura seguía sin pasar (blanco sobre
        // #FF8FB8 da 2,13), así que Sakura cambió su blanco por su propia tinta ciruela
        // (ver `temas-de-serie.json`, «correcciones»). La regla se ajustó donde era injusta y
        // el tema se corrigió donde estaba mal; no al revés.
        C(ClavesBase.SobreAcento, ZonaColor.Comun, "Texto encima del acento",
          // Blanco o negro, el que más se lea: partiendo del blanco, `Legible` no tiene a
          // dónde ir sobre un acento de tono medio. Es la cuenta que ya hacía
          // `Contraste.TintaSobre` para las chapitas.
          p => Relleno.Liso(Contraste.TintaSobre(p.Color(ClavesBase.Acento))),
          Papel.Icono, ClavesBase.Acento),

        C(ClavesBase.Sec1, ZonaColor.Comun, "Secundario",
          Hacia(ClavesBase.Tinta, ClavesBase.Superficie, 0.45)),

        C(ClavesBase.Sec2, ZonaColor.Comun, "Segundo secundario", Igual(ClavesBase.Acento)),

        C(ClavesBase.Ok, ZonaColor.Comun, "Estado correcto",
          p => Relleno.Liso(p.Tema.Claro ? Color.FromRgb(0x3B, 0xB2, 0x73) : Color.FromRgb(0x8F, 0xC1, 0xA0))),

        C(ClavesBase.Peligro, ZonaColor.Comun, "Acción destructiva",
          p => Relleno.Liso(p.Tema.Claro ? Color.FromRgb(0xC9, 0x3C, 0x3C) : Color.FromRgb(0xE3, 0xA0, 0x8F))),

        // El ámbar de «ojo con esto»: batería en ahorro, disco al 90 %, un campo con una
        // errata. No está entre los 19 de la maqueta pero el código ya lo usaba
        // (`Brushes.Gold`), y un aviso tiene que poder cambiarse como todo lo demás.
        C("aviso", ZonaColor.Comun, "Aviso",
          p => Relleno.Liso(p.Tema.Claro ? Color.FromRgb(0xB5, 0x75, 0x0A) : Color.FromRgb(0xD8, 0xC0, 0x8F))),

        C(ClavesBase.Sombra, ZonaColor.Comun, "Sombra",
          p => Relleno.Liso(p.Tema.Claro ? Color.FromArgb(0x33, 0x1F, 0x27, 0x33)
                                         : Color.FromArgb(0xD9, 0x00, 0x00, 0x00)), Papel.Efecto),

        C(ClavesBase.Halo, ZonaColor.Comun, "Halo", Igual(ClavesBase.Acento), Papel.Efecto),

        C(ClavesBase.Destello, ZonaColor.Comun, "Destello del filo",
          Igual(ClavesBase.Sec2), Papel.Efecto),

        C(ClavesBase.FondoEscritorio, ZonaColor.Comun, "Fondo de escritorio del tema",
          p => Relleno.Liso(p.ColorOpaco(ClavesBase.Superficie))),
    };

    // ---------- La isla ----------
    static IEnumerable<ClaveColor> Isla() => new[]
    {
        C("isla.fondo", ZonaColor.Isla, "Fondo", Igual(ClavesBase.Superficie)),
        C("isla.filo", ZonaColor.Isla, "Filo", Igual(ClavesBase.FiloIsla), Papel.Filo),
        C("isla.halo", ZonaColor.Isla, "Halo", Igual(ClavesBase.Halo), Papel.Efecto),
        C("isla.destello", ZonaColor.Isla, "Destello que recorre el filo", Igual(ClavesBase.Destello), Papel.Efecto),
        C("isla.sombra", ZonaColor.Isla, "Sombra", Igual(ClavesBase.Sombra), Papel.Efecto),

        // El punto de una app abierta es «tenue» (la maqueta: 4×3 px tenue), pero un VELO
        // del texto al 45 % no vale: en un tema claro da un gris a 2,3:1 del fondo y el punto
        // desaparece. Es la tinta tenue, corregida para verse.
        C("app.punto", ZonaColor.Isla, "Punto de una app abierta",
          TintaDe(ClavesBase.Tenue, ClavesBase.Superficie, 3.0), Papel.Icono, "isla.fondo"),
        // Y el de la activa es el acento EN SU VERSIÓN DE TINTA, que es para lo que la
        // maqueta trae `acento-tinta`: «el acento cuando hace de texto o icono». El acento
        // crudo de Sakura (#FF8FB8) sobre su isla casi blanca da 2,07:1.
        C("app.punto.activo", ZonaColor.Isla, "Punto de la app activa",
          TintaDe(ClavesBase.AcentoTinta, ClavesBase.Superficie, 3.0), Papel.Icono, "isla.fondo"),
        C("app.fondo.raton", ZonaColor.Isla, "Fondo del icono bajo el ratón", Velo(ClavesBase.Tinta, 0.06)),

        C("icono.sistema", ZonaColor.Isla, "Iconos de sistema",
          TintaDe(ClavesBase.Sec2, ClavesBase.Superficie, 3.0), Papel.Icono, "isla.fondo"),
        C("icono.sistema.raton", ZonaColor.Isla, "Iconos de sistema bajo el ratón",
          Igual(ClavesBase.Tinta), Papel.Icono, "isla.fondo"),

        C("reloj.texto", ZonaColor.Isla, "Reloj", Igual(ClavesBase.Tinta), Papel.Texto, "isla.fondo"),
        C("reloj.brillo", ZonaColor.Isla, "Brillo que recorre el reloj",
          p => Relleno.Liso(Colors.White), Papel.Efecto),
        C("fecha.texto", ZonaColor.Isla, "Fecha", Igual(ClavesBase.Tenue), Papel.Texto, "isla.fondo"),

        C("teclado.fondo", ZonaColor.Isla, "Pastilla del teclado («ES»)", Velo(ClavesBase.Tinta, 0.06)),
        C("teclado.texto", ZonaColor.Isla, "Letras del teclado",
          TintaDe(ClavesBase.Sec2, ClavesBase.Superficie), Papel.Texto, "isla.fondo"),

        C("insignia.fondo", ZonaColor.Isla, "Insignia de avisos", Igual(ClavesBase.Peligro)),
        C("insignia.texto", ZonaColor.Isla, "Número de la insignia",
          p => Relleno.Liso(Contraste.TintaSobre(p.Color("insignia.fondo"))), Papel.Texto, "insignia.fondo"),
        C("insignia.latido", ZonaColor.Isla, "Latido de la insignia", Igual("insignia.fondo"), Papel.Efecto),

        // El halo claro que se le pone detrás a un icono oscuro para que no desaparezca
        // sobre la isla (`Legible`, `Iconos.EsOscuro`). Es un efecto, no un color de marca,
        // pero tiene que poder cambiarse: en un tema CLARO el halo útil es oscuro.
        // ---------- El escritorio actual ----------
        //
        // Era un cian fijo (`HyprPinta.AzulNube`), y SIGUE SIENDO ese color: el usuario lo pidió
        // así al verlo cambiado — «el botón 1 déjalo como estaba». Lo único que cambia es que
        // ahora tiene nombre y se puede editar, que es lo que pide la decisión B; el aspecto no
        // se mueve ni un tono.
        //
        // El motivo del color sigue escrito donde lo dejó su autor (`MainWindow.Escritorios.cs`)
        // y es bueno: en NubeHypr el azul significa «aquí estás», y si saliera del acento, un
        // tema de acento rojo diría «aquí estás» con el color de los avisos. La regla entera
        // vive ahora en esta derivación: manda el color de sistema del tema si de verdad es
        // azul, y si no, el azul de la casa. Es lo mismo que hacía `HyprPinta.Azul`.
        C("escritorio.actual.fondo", ZonaColor.Isla, "Escritorio actual",
          p => Relleno.Liso(HyprPinta.EsAzul(p.Color(ClavesBase.Sec2))
                                ? p.Color(ClavesBase.Sec2)
                                : HyprPinta.AzulNube)),
        // ---------- Y el número de encima, que SÍ cambia ----------
        //
        // Hacía `Legible(tinta, relleno)`: coger el texto del tema y acercarlo al blanco o al
        // negro hasta que se lea. El problema es que la tinta de un tema oscuro ya es casi
        // blanca, así que sobre un cian claro no tiene a dónde ir: se quedaba en **2,2:1** en
        // Profesional, Videojuegos, Samurái y Sad. O sea, el número del escritorio en el que
        // estás era lo que peor se leía de la barra, y nadie lo había medido hasta ahora.
        //
        // Blanco o negro, el que más se lea — la misma cuenta que `sobre-acento`. La pastilla
        // no cambia de color; lo que cambia es que el número se lee.
        C("escritorio.actual.texto", ZonaColor.Isla, "Número del escritorio actual",
          p => Relleno.Liso(Contraste.TintaSobre(p.ColorOpaco("escritorio.actual.fondo"))),
          Papel.Texto, "escritorio.actual.fondo"),
        C("escritorio.numero", ZonaColor.Isla, "Números de los demás escritorios",
          TintaDe(ClavesBase.Tenue, ClavesBase.Superficie, 3.0), Papel.Icono, "isla.fondo"),

        // ---------- El color de las partículas ----------
        //
        // Sale de la FORMA que el tema declara, porque es lo que dice de qué están hechas: un
        // copo es blanco, la lluvia de neón es el color del halo, y un pétalo, una hoja o un
        // píxel son del acento del tema — que es justo lo que los nueve ya traen (el rosa de
        // Sakura, el bermellón de Samurái, el arce de Otoño, el verde de Videojuegos).
        //
        // Así un tema no tiene que declarar un color más: con decir `particulas:copo` ya cae
        // nieve blanca. Y el usuario puede cambiarlo, como cualquier otra clave.
        // ---------- Y que se VEAN ----------
        //
        // La primera versión tenía la regla de arriba y nada más, y en Invierno dejaba nieve
        // BLANCA sobre un panel casi blanco: cayendo de verdad y sin verse. Lo cazó la foto
        // del Inicio en los nueve temas, que es para lo que se hizo.
        //
        // Así que al color que toca por la forma se le exige separarse del panel, y si no lo
        // hace se prueban los otros colores DEL PROPIO TEMA por orden. Se queda en 1,6:1 —muy
        // por debajo de lo que se le pide a un texto— porque una partícula no se lee: se ve
        // pasar, y pedirle contraste de texto la convertiría en confeti.
        C("particula.color", ZonaColor.Inicio, "Color de las partículas",
          p =>
          {
              var porLaForma = p.Tema.Argumento(Decoraciones.Particulas) switch
              {
                  "copo" => ClavesBase.Destello,
                  "lluvia-neon" => ClavesBase.Halo,
                  _ => ClavesBase.Acento,
              };
              var panel = p.ColorOpaco("inicio.fondo");
              foreach (var clave in new[] { porLaForma, ClavesBase.Sec2, ClavesBase.Sec1,
                                            ClavesBase.Acento, ClavesBase.Tenue })
              {
                  var r = p.R(clave);
                  if (!r.EsInvisible && Contraste.Entre(p.ColorOpaco(clave), panel) >= 1.6) return r;
              }
              return p.R(ClavesBase.Tenue);
          }, Papel.Efecto),

        // ---------- La marca de agua ----------
        //
        // El kanji 侍 de Samurái detrás de las tarjetas. Sale del secundario del tema —el oro,
        // en su caso— y muy bajito: una marca de agua que compite con el contenido deja de ser
        // una marca de agua y pasa a ser una imagen de fondo con texto encima.
        C("marca.agua", ZonaColor.Inicio, "Marca de agua del tema",
          Velo(ClavesBase.Sec1, 0.16), Papel.Efecto),

        C("icono.halo", ZonaColor.Isla, "Halo de los iconos oscuros",
          p => Relleno.Liso(p.Tema.Claro ? Colors.Black : Colors.White), Papel.Efecto),

        // ---------- Los dos filos del pulso de audio ----------
        //
        // Asoman por los lados de la isla al ritmo del sonido, uno frío y otro cálido. Eran
        // dos hexadecimales escritos en `MainWindow.PulsoAudio.cs`; ahora tienen nombre y se
        // pueden cambiar. Su derivación es una constante porque ningún tema tiene un color
        // que signifique «el lado frío del pulso»: cuando un tema quiera los suyos, los
        // declara, que es justo para lo que sirve el escalón 2 de la cascada.
        C("pulso.frio", ZonaColor.Isla, "Filo frío del pulso de audio",
          p => Relleno.Liso(Color.FromRgb(0x4D, 0xE8, 0xFF)), Papel.Efecto),
        C("pulso.calido", ZonaColor.Isla, "Filo cálido del pulso de audio",
          p => Relleno.Liso(Color.FromRgb(0xFF, 0x4D, 0x8D)), Papel.Efecto),

        // ---------- Editar la barra ----------
        //
        // Las zonas que se encienden al arrastrar un módulo. Eran cuatro velos del acento
        // escritos a mano (0x12, 0x30, 0x90) dentro de `MainWindow.Edicion.cs`.
        C("editor.zona.fondo", ZonaColor.Isla, "Editar la barra: fondo de una zona",
          Velo(ClavesBase.Acento, 0.07)),
        C("editor.zona.filo", ZonaColor.Isla, "Editar la barra: filo de una zona",
          Velo(ClavesBase.Acento, 0.19), Papel.Filo),
        C("editor.zona.fondo.activa", ZonaColor.Isla, "Editar la barra: zona bajo el puntero",
          Velo(ClavesBase.Acento, 0.19)),
        C("editor.zona.filo.activa", ZonaColor.Isla, "Editar la barra: filo de la zona bajo el puntero",
          Velo(ClavesBase.Acento, 0.56), Papel.Filo),
    };

    // ---------- El menú Inicio ----------
    static IEnumerable<ClaveColor> Inicio() => new[]
    {
        C("inicio.fondo", ZonaColor.Inicio, "Fondo", Igual(ClavesBase.PanelFondo)),
        C("inicio.filo", ZonaColor.Inicio, "Filo", Igual(ClavesBase.FiloPanel), Papel.Filo),

        C("saludo.texto", ZonaColor.Inicio, "Saludo", Igual(ClavesBase.Tinta), Papel.Texto, "inicio.fondo"),
        C("saludo.fecha", ZonaColor.Inicio, "Fecha del saludo", Igual(ClavesBase.Tenue), Papel.Texto, "inicio.fondo"),
        C("reloj.grande", ZonaColor.Inicio, "Reloj grande", Igual(ClavesBase.Tinta), Papel.Texto, "inicio.fondo"),

        C("buscador.fondo", ZonaColor.Inicio, "Fondo del buscador", Igual(ClavesBase.Tarjeta)),
        C("buscador.filo", ZonaColor.Inicio, "Filo del buscador", Igual(ClavesBase.Filo), Papel.Filo),
        C("buscador.filo.foco", ZonaColor.Inicio, "Filo del buscador con el foco", Igual(ClavesBase.Acento), Papel.Filo),
        C("buscador.texto", ZonaColor.Inicio, "Texto escrito", Igual(ClavesBase.Tinta), Papel.Texto, "buscador.fondo"),
        C("buscador.pista", ZonaColor.Inicio, "Pista del buscador", Igual(ClavesBase.Tenue), Papel.Texto, "buscador.fondo"),
        C("buscador.lupa", ZonaColor.Inicio, "Lupa", Igual(ClavesBase.Tenue), Papel.Icono, "buscador.fondo"),

        C("resultado.fondo.raton", ZonaColor.Inicio, "Resultado bajo el ratón", Velo(ClavesBase.Tinta, 0.08)),
        C("resultado.fondo.elegido", ZonaColor.Inicio, "Resultado elegido", Velo(ClavesBase.Acento, 0.18)),
        C("resultado.titulo", ZonaColor.Inicio, "Título del resultado", Igual(ClavesBase.Tinta), Papel.Texto, "inicio.fondo"),
        C("resultado.detalle", ZonaColor.Inicio, "Ruta o detalle del resultado", Igual(ClavesBase.Tenue), Papel.Texto, "inicio.fondo"),
        C("resultado.grupo", ZonaColor.Inicio, "Título de un grupo de resultados",
          TintaDe(ClavesBase.AcentoTinta, "inicio.fondo"), Papel.Texto, "inicio.fondo"),

        C("musica.caratula.vacia", ZonaColor.Inicio, "Carátula cuando no hay",
          p => Relleno.Degradado(160, new[]
          {
              new Parada(p.Color(ClavesBase.Sec1), 0),
              new Parada(p.ColorOpaco(ClavesBase.Superficie), 1),
          })),
        C("musica.titulo", ZonaColor.Inicio, "Título de la canción", Igual(ClavesBase.Tinta), Papel.Texto, "inicio.fondo"),
        C("musica.artista", ZonaColor.Inicio, "Artista y app", Igual(ClavesBase.Tenue), Papel.Texto, "inicio.fondo"),
        C("musica.progreso", ZonaColor.Inicio, "Barra de progreso", Igual(ClavesBase.Acento)),
        C("musica.progreso.pista", ZonaColor.Inicio, "Pista del progreso", Velo(ClavesBase.Tinta, 0.14)),
        C("musica.progreso.brillo", ZonaColor.Inicio, "Brillo que recorre el progreso",
          p => Relleno.Liso(Colors.White), Papel.Efecto),
        C("musica.boton", ZonaColor.Inicio, "Anterior y siguiente", Igual("icono.sistema"), Papel.Icono, "inicio.fondo"),
        C("musica.play.fondo", ZonaColor.Inicio, "Círculo de play", Igual(ClavesBase.Tinta)),
        C("musica.play.glifo", ZonaColor.Inicio, "Glifo de play",
          p => Relleno.Liso(Contraste.TintaSobre(p.Color("musica.play.fondo"))), Papel.Icono, "musica.play.fondo"),
        C("ecualizador.barra", ZonaColor.Inicio, "Barras del ecualizador", Igual(ClavesBase.Acento)),

        C("energia.boton.fondo", ZonaColor.Inicio, "Botones de energía", Igual(ClavesBase.Tarjeta)),
        C("energia.boton.glifo", ZonaColor.Inicio, "Glifos de energía",
          Igual("icono.sistema"), Papel.Icono, "energia.boton.fondo"),
        C("energia.apagar", ZonaColor.Inicio, "Glifo de «Apagar»",
          TintaDe(ClavesBase.Peligro, ClavesBase.Tarjeta, 3.0), Papel.Icono, "energia.boton.fondo"),
        C("energia.apagar.fondo.raton", ZonaColor.Inicio, "Fondo de «Apagar» bajo el ratón",
          Velo(ClavesBase.Peligro, 0.12)),
        C("energia.apagar.filo.raton", ZonaColor.Inicio, "Filo de «Apagar» bajo el ratón",
          Velo(ClavesBase.Peligro, 0.45), Papel.Filo),
    };

    // ---------- Los paneles (los de la isla y los laterales comparten estas piezas) ----------
    static IEnumerable<ClaveColor> Paneles() => new[]
    {
        C("panel.fondo", ZonaColor.Paneles, "Fondo", Igual(ClavesBase.PanelFondo)),
        C("panel.filo", ZonaColor.Paneles, "Filo", Igual(ClavesBase.FiloPanel), Papel.Filo),
        C("panel.sombra", ZonaColor.Paneles, "Sombra", Igual(ClavesBase.Sombra), Papel.Efecto),
        C("panel.icono.abierto", ZonaColor.Paneles, "Icono del que cuelga el panel", Velo(ClavesBase.Acento, 0.18)),

        C("tarjeta.fondo", ZonaColor.Paneles, "Fondo de una tarjeta",
          Igual(ClavesBase.Tarjeta), Papel.Relleno, "panel.fondo"),
        C("tarjeta.fondo.raton", ZonaColor.Paneles, "Tarjeta bajo el ratón",
          Velo(ClavesBase.Tinta, 0.20), Papel.Relleno, "panel.fondo"),
        C("tarjeta.filo", ZonaColor.Paneles, "Filo de una tarjeta", Igual(ClavesBase.Filo), Papel.Filo),

        C("seccion.titulo", ZonaColor.Paneles, "Título de sección", Igual(ClavesBase.Tenue), Papel.Texto, "panel.fondo"),
        C("seccion.icono", ZonaColor.Paneles, "Icono de sección",
          TintaDe(ClavesBase.AcentoTinta, "tarjeta.fondo", 3.0), Papel.Icono, "tarjeta.fondo"),
        C("seccion.flecha", ZonaColor.Paneles, "Flecha de sección", Igual(ClavesBase.Tenue), Papel.Icono, "tarjeta.fondo"),
        C("separador", ZonaColor.Paneles, "Separadores", Igual(ClavesBase.Filo), Papel.Filo),

        C("boton.fondo", ZonaColor.Paneles, "Fondo de un botón", Igual(ClavesBase.Tarjeta)),
        C("boton.fondo.raton", ZonaColor.Paneles, "Botón bajo el ratón", Velo(ClavesBase.Tinta, 0.20)),
        C("boton.fondo.pulsado", ZonaColor.Paneles, "Botón pulsado", Velo(ClavesBase.Tinta, 0.28)),
        C("boton.fondo.activo", ZonaColor.Paneles, "Botón encendido", Velo(ClavesBase.Acento, 0.18)),
        C("boton.filo", ZonaColor.Paneles, "Filo de un botón", Igual(ClavesBase.Filo), Papel.Filo),
        C("boton.filo.activo", ZonaColor.Paneles, "Filo de un botón encendido", Igual(ClavesBase.Acento), Papel.Filo),
        C("boton.texto", ZonaColor.Paneles, "Texto de un botón", Igual(ClavesBase.Tinta), Papel.Texto, "boton.fondo"),
        C("boton.texto.activo", ZonaColor.Paneles, "Texto de un botón encendido",
          TintaDe(ClavesBase.AcentoTinta, "boton.fondo.activo"), Papel.Texto, "boton.fondo.activo"),
        // Desactivado SÍ se mide, pero con la vara de los iconos: tiene que leerse —si no,
        // el usuario no sabe qué dice el botón que no puede pulsar— sin parecer activo.
        C("boton.texto.desactivado", ZonaColor.Paneles, "Texto de un botón desactivado",
          HaciaLegible(ClavesBase.Tenue, "boton.fondo", 0.35, 3.0), Papel.Icono, "boton.fondo"),
        C("boton.destructivo.texto", ZonaColor.Paneles, "Texto de una acción destructiva",
          TintaDe(ClavesBase.Peligro, ClavesBase.Tarjeta), Papel.Texto, "boton.fondo"),
        C("boton.destructivo.fondo.raton", ZonaColor.Paneles, "Acción destructiva bajo el ratón",
          Velo(ClavesBase.Peligro, 0.14)),

        C("deslizador.pista", ZonaColor.Paneles, "Pista de un deslizador", Velo(ClavesBase.Tinta, 0.16)),
        C("deslizador.lleno", ZonaColor.Paneles, "Parte llena de un deslizador", Igual(ClavesBase.Acento)),
        C("deslizador.pomo", ZonaColor.Paneles, "Pomo de un deslizador", Igual(ClavesBase.Tinta)),
        C("deslizador.anillo", ZonaColor.Paneles, "Anillo del pomo", Velo(ClavesBase.Acento, 0.25), Papel.Efecto),
        C("deslizador.apagado", ZonaColor.Paneles, "Deslizador en silencio", Velo(ClavesBase.Tinta, 0.30)),

        C("interruptor.fondo", ZonaColor.Paneles, "Interruptor apagado", Velo(ClavesBase.Tinta, 0.20)),
        C("interruptor.fondo.activo", ZonaColor.Paneles, "Interruptor encendido", Igual(ClavesBase.Acento)),
        // ---------- El pomo no puede ser blanco ----------
        //
        // Lo era, y en un tema claro quedaba blanco sobre una pista gris clara: 1,5:1, o sea
        // un interruptor sin pomo. Se elige el que más se lea sobre la pista APAGADA, que es
        // el caso difícil (la encendida es el acento, saturado). En un tema claro sale un
        // pomo oscuro, que es justo lo que hacen los interruptores claros de Windows.
        C("interruptor.pomo", ZonaColor.Paneles, "Pomo del interruptor",
          p => Relleno.Liso(Contraste.TintaSobre(p.ColorOpaco("interruptor.fondo"))),
          Papel.Icono, "interruptor.fondo"),

        C("pastilla.ok.fondo", ZonaColor.Paneles, "Pastilla de estado correcto", Velo(ClavesBase.Ok, 0.18)),
        C("pastilla.ok.texto", ZonaColor.Paneles, "Texto de estado correcto",
          TintaDe(ClavesBase.Ok, ClavesBase.Tarjeta), Papel.Texto, "tarjeta.fondo"),
        // El verde de «Conectado» crudo sobre una tarjeta blanca da 1,7:1 en Sakura. Como
        // relleno (la pastilla) va bien; como PUNTO, que es un glifo, hay que corregirlo.
        C("pastilla.ok.punto", ZonaColor.Paneles, "Punto de estado correcto",
          TintaDe(ClavesBase.Ok, "tarjeta.fondo", 3.0), Papel.Icono, "tarjeta.fondo"),

        C("anillo.sistema", ZonaColor.Paneles, "Anillos de CPU, RAM y disco", Igual(ClavesBase.Acento)),
        C("anillo.sistema.alto", ZonaColor.Paneles, "Anillos por encima del 75 %", Igual("aviso")),
        C("anillo.pista", ZonaColor.Paneles, "Pista de los anillos", Velo(ClavesBase.Tinta, 0.14)),

        C("red.senal", ZonaColor.Paneles, "Barras de señal", Igual("icono.sistema"), Papel.Icono, "tarjeta.fondo"),
        C("red.senal.vacia", ZonaColor.Paneles, "Barras de señal apagadas", Velo(ClavesBase.Tinta, 0.22)),
        C("red.medida", ZonaColor.Paneles, "Franja que recorre la medida de velocidad",
          Igual(ClavesBase.Acento), Papel.Efecto),

        C("bateria.relleno", ZonaColor.Paneles, "Relleno de la batería", Igual(ClavesBase.Acento)),
        C("bateria.bajo", ZonaColor.Paneles, "Batería baja", Igual(ClavesBase.Peligro)),
        C("bateria.ahorro", ZonaColor.Paneles, "Batería en ahorro", Igual("aviso")),

        C("calendario.hoy.fondo", ZonaColor.Paneles, "Hoy, en el calendario", Velo(ClavesBase.Acento, 0.16)),
        C("calendario.hoy.filo", ZonaColor.Paneles, "Filo de hoy", Igual(ClavesBase.Acento), Papel.Filo),
        C("calendario.otro.mes", ZonaColor.Paneles, "Días de otro mes",
          HaciaLegible(ClavesBase.Tenue, "tarjeta.fondo", 0.4, 3.0), Papel.Icono, "tarjeta.fondo"),

        C("scroll.pastilla", ZonaColor.Paneles, "Barra de desplazamiento", Velo(ClavesBase.Tinta, 0.28)),
        C("scroll.pastilla.raton", ZonaColor.Paneles, "Barra de desplazamiento bajo el ratón",
          Velo(ClavesBase.Tinta, 0.45)),
        C("scroll.pista", ZonaColor.Paneles, "Pista del desplazamiento", Velo(ClavesBase.Tinta, 0.06)),

        C("tooltip.fondo", ZonaColor.Paneles, "Fondo de las ayudas emergentes",
          p => Relleno.Liso(p.ColorOpaco(ClavesBase.PanelFondo))),
        C("tooltip.filo", ZonaColor.Paneles, "Filo de las ayudas emergentes", Igual(ClavesBase.Filo), Papel.Filo),
        C("tooltip.texto", ZonaColor.Paneles, "Texto de las ayudas emergentes",
          Igual(ClavesBase.Tinta), Papel.Texto, "tooltip.fondo"),
    };

    // ---------- Los laterales ----------
    static IEnumerable<ClaveColor> Laterales() => new[]
    {
        C("lateral.fondo", ZonaColor.Laterales, "Fondo", Igual(ClavesBase.PanelFondo)),
        C("lateral.filo", ZonaColor.Laterales, "Filo", Igual(ClavesBase.FiloPanel), Papel.Filo),
        C("lateral.sombra", ZonaColor.Laterales, "Sombra", Igual(ClavesBase.Sombra), Papel.Efecto),
        C("accion.fondo", ZonaColor.Laterales, "Acción rápida apagada", Igual(ClavesBase.Tarjeta)),
        C("accion.fondo.activa", ZonaColor.Laterales, "Acción rápida encendida", Velo(ClavesBase.Acento, 0.12)),
        C("accion.filo.activa", ZonaColor.Laterales, "Filo de una acción encendida", Igual(ClavesBase.Acento), Papel.Filo),
        C("accion.icono", ZonaColor.Laterales, "Icono de una acción", Igual("icono.sistema"), Papel.Icono, "accion.fondo"),
        C("accion.icono.activa", ZonaColor.Laterales, "Icono de una acción encendida",
          TintaDe(ClavesBase.AcentoTinta, "accion.fondo.activa", 3.0), Papel.Icono, "accion.fondo.activa"),
        C("accion.texto", ZonaColor.Laterales, "Texto de una acción", Igual(ClavesBase.Tinta), Papel.Texto, "accion.fondo"),
    };

    // ---------- El dock ----------
    static IEnumerable<ClaveColor> Dock() => new[]
    {
        C("dock.fondo", ZonaColor.Dock, "Fondo", Igual(ClavesBase.Superficie)),
        C("dock.filo", ZonaColor.Dock, "Filo", Igual(ClavesBase.FiloIsla), Papel.Filo),
        C("dock.sombra", ZonaColor.Dock, "Sombra", Igual(ClavesBase.Sombra), Papel.Efecto),
        C("dock.punto", ZonaColor.Dock, "Punto de una app abierta", Igual("app.punto"), Papel.Icono, "dock.fondo"),
        C("dock.punto.activo", ZonaColor.Dock, "Punto de la app activa", Igual("app.punto.activo"), Papel.Icono, "dock.fondo"),
        C("dock.etiqueta.fondo", ZonaColor.Dock, "Fondo del nombre", Igual("tooltip.fondo")),
        C("dock.etiqueta.texto", ZonaColor.Dock, "Nombre de la app",
          Igual(ClavesBase.Tinta), Papel.Texto, "dock.etiqueta.fondo"),
    };

    // ---------- Los avisos ----------
    static IEnumerable<ClaveColor> Avisos() => new[]
    {
        C("aviso.fondo", ZonaColor.Avisos, "Fondo de un aviso", Igual(ClavesBase.Tarjeta)),
        C("aviso.filo", ZonaColor.Avisos, "Filo de un aviso", Igual(ClavesBase.Filo), Papel.Filo),
        C("aviso.app", ZonaColor.Avisos, "Nombre de la app",
          TintaDe(ClavesBase.AcentoTinta, "aviso.fondo"), Papel.Texto, "aviso.fondo"),
        C("aviso.titulo", ZonaColor.Avisos, "Título", Igual(ClavesBase.Tinta), Papel.Texto, "aviso.fondo"),
        C("aviso.texto", ZonaColor.Avisos, "Cuerpo", Igual(ClavesBase.Tenue), Papel.Texto, "aviso.fondo"),
        C("aviso.hora", ZonaColor.Avisos, "Hora", Igual(ClavesBase.Tenue), Papel.Texto, "aviso.fondo"),
        C("aviso.cerrar", ZonaColor.Avisos, "Aspa de cerrar", Igual(ClavesBase.Tenue), Papel.Icono, "aviso.fondo"),
        C("aviso.vacio", ZonaColor.Avisos, "«Todo al día»", Igual(ClavesBase.Tenue), Papel.Texto, "panel.fondo"),

        C("osd.fondo", ZonaColor.Avisos, "Fondo del aviso de volumen o brillo", Igual(ClavesBase.Superficie)),
        C("osd.relleno", ZonaColor.Avisos, "Barra del aviso", Igual(ClavesBase.Acento)),
        C("osd.pista", ZonaColor.Avisos, "Pista del aviso", Velo(ClavesBase.Tinta, 0.16)),
        C("osd.glifo", ZonaColor.Avisos, "Glifo del aviso", Igual(ClavesBase.Tinta), Papel.Icono, "osd.fondo"),
    };

    // ---------- La vista general y el ajuste de ventanas ----------
    static IEnumerable<ClaveColor> Vista() => new[]
    {
        C("vista.fondo", ZonaColor.Vista, "Fondo", p => p.R(ClavesBase.Superficie).ConOpacidad(0.9)),
        C("vista.tarjeta.fondo", ZonaColor.Vista, "Tarjeta de una ventana", Igual(ClavesBase.Tarjeta)),
        C("vista.tarjeta.filo", ZonaColor.Vista, "Filo de una tarjeta", Igual(ClavesBase.Filo), Papel.Filo),
        C("vista.tarjeta.filo.activa", ZonaColor.Vista, "Filo de la ventana activa", Igual(ClavesBase.Acento), Papel.Filo),
        C("vista.titulo", ZonaColor.Vista, "Título de la ventana", Igual(ClavesBase.Tinta), Papel.Texto, "vista.tarjeta.fondo"),
        C("vista.app", ZonaColor.Vista, "Nombre de la app", Igual(ClavesBase.Tenue), Papel.Texto, "vista.tarjeta.fondo"),
        C("vista.cerrar", ZonaColor.Vista, "Aspa de cerrar", Igual(ClavesBase.Tenue), Papel.Icono, "vista.tarjeta.fondo"),

        C("previa.zona.fondo", ZonaColor.Vista, "Zona de ajuste al arrastrar", Velo(ClavesBase.Acento, 0.22)),
        C("previa.zona.filo", ZonaColor.Vista, "Filo de la zona de ajuste", Igual(ClavesBase.Acento), Papel.Filo),

        C("arranque.fondo", ZonaColor.Vista, "Fondo de la escena de arranque",
          p => Relleno.Liso(p.ColorOpaco(ClavesBase.Superficie))),
        C("arranque.texto", ZonaColor.Vista, "Texto de la escena de arranque",
          Igual(ClavesBase.Tinta), Papel.Texto, "arranque.fondo"),
    };

    // ---------- La ventana de Ajustes ----------
    //
    // Tiene claves propias y no reutiliza las de los paneles por la «regla de seguridad»: si
    // el tema deja Ajustes ilegible, estas dos ventanas se pintan con Profesional. Para poder
    // sustituirlas en bloque tienen que ser un bloque.
    static IEnumerable<ClaveColor> Ajustes() => new[]
    {
        C("ajustes.fondo", ZonaColor.Ajustes, "Fondo", p => Relleno.Liso(p.ColorOpaco(ClavesBase.PanelFondo))),
        C("ajustes.lateral.fondo", ZonaColor.Ajustes, "Fondo de la lista de secciones", Velo(ClavesBase.Tinta, 0.05)),
        C("ajustes.seccion.fondo.elegida", ZonaColor.Ajustes, "Sección elegida", Velo(ClavesBase.Acento, 0.18)),
        C("ajustes.seccion.texto", ZonaColor.Ajustes, "Nombre de una sección",
          Igual(ClavesBase.Tinta), Papel.Texto, "ajustes.lateral.fondo"),
        C("ajustes.tarjeta.fondo", ZonaColor.Ajustes, "Fondo de una tarjeta", Igual(ClavesBase.Tarjeta)),
        C("ajustes.tarjeta.filo", ZonaColor.Ajustes, "Filo de una tarjeta", Igual(ClavesBase.Filo), Papel.Filo),
        C("ajustes.titulo", ZonaColor.Ajustes, "Títulos", Igual(ClavesBase.Tinta), Papel.Texto, "ajustes.fondo"),
        C("ajustes.subtitulo", ZonaColor.Ajustes, "Explicaciones", Igual(ClavesBase.Tenue), Papel.Texto, "ajustes.tarjeta.fondo"),
        C("ajustes.campo.fondo", ZonaColor.Ajustes, "Fondo de un campo", Velo(ClavesBase.Tinta, 0.08)),
        C("ajustes.campo.filo", ZonaColor.Ajustes, "Filo de un campo", Igual(ClavesBase.Filo), Papel.Filo),
        C("ajustes.aviso.mal", ZonaColor.Ajustes, "Aviso de que algo está mal",
          TintaDe(ClavesBase.Peligro, ClavesBase.Tarjeta), Papel.Texto, "ajustes.tarjeta.fondo"),
        C("ajustes.aviso.bien", ZonaColor.Ajustes, "Aviso de que algo está bien",
          TintaDe(ClavesBase.Ok, ClavesBase.Tarjeta), Papel.Texto, "ajustes.tarjeta.fondo"),
        C("ajustes.aviso.ojo", ZonaColor.Ajustes, "Aviso de «ojo con esto»",
          TintaDe("aviso", ClavesBase.Tarjeta), Papel.Texto, "ajustes.tarjeta.fondo"),
    };
}
