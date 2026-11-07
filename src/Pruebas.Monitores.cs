using System;
using System.Linq;

namespace NubeBar;

// El control de la entrada de vídeo por DDC/CI, en lo que se puede comprobar sin monitores.
//
// ---------- Qué se puede probar y qué no ----------
//
// Mandarle un comando a un monitor no se puede probar aquí: necesita el monitor, el cable, y
// DDC/CI activado en su menú. Eso es una prueba manual y está en `PRUEBAS-PARA-PAULO.md`.
//
// Lo que SÍ se puede probar —y es justo donde se rompen estas cosas— son los dos descifradores:
//
//   - la cadena de CAPACIDADES del monitor, que decide qué botones se dibujan;
//   - el EDID, que decide QUIÉN es cada monitor y por tanto a quién le llega el clic.
//
// Los dos leen formatos binarios o cuasi-binarios escritos por fabricantes distintos, y los dos
// tienen la propiedad incómoda de fallar en silencio: un descifrador de capacidades que se cuela
// no da error, da una lista de entradas equivocada, y eso se descubre cuando un botón «HDMI 2»
// no hace nada. Con cadenas escritas a mano se ven en un segundo.
public static partial class Pruebas
{
    internal static void CorrerMonitores()
    {
        CapacidadesDelMonitor();
        NombresDeEntrada();
        EdidDiceQuienEs();
        RutaDelEdidEnElRegistro();
        ModuloSeOfreceEnAjustesYBiblioteca();
    }

    // ================= La cadena de capacidades =================

    // Una cadena como la que devuelve de verdad un AOC: el `vcp(...)` con el `60(0F 11 12)`
    // dentro, y —esto es lo que importa— OTRO código con grupo propio delante (`14(05 08 0B)`,
    // que son las temperaturas de color). Buscar «60(» a pelo en la cadena entera es lo primero
    // que se le ocurre a cualquiera y es lo que estas pruebas impiden.
    const string CapsAoc =
        "(prot(monitor)type(LCD)model(Q27G3G3R3)cmds(01 02 03 07 0C E3 F3)" +
        "vcp(02 04 05 08 10 12 14(05 08 0B) 16 18 1A 52 60(0F 11 12) 6C 6E 70 AC AE B2 B6 C0 C6 C8 C9 D6(01 04 05) DF)" +
        "mswhql(1)asset_eep(40)mccs_ver(2.2))";

    static void CapacidadesDelMonitor()
    {
        Grupo("monitores: qué entradas anuncia el monitor");

        // ---------- El caso bueno ----------
        var aoc = Entradas.EntradasDeCapacidades(CapsAoc);
        Igual(3, aoc.Count, "el AOC anuncia tres entradas");
        Vale(aoc.SequenceEqual(new byte[] { 0x0F, 0x11, 0x12 }),
             "y son DisplayPort 1, HDMI 1 y HDMI 2, en ese orden");

        // ---------- Lo que NO se puede confundir con una entrada ----------
        //
        // Cada una de estas cuatro es un sitio donde aparece un «60» que no es el nuestro. Si
        // alguna colara, el panel dibujaría botones de entradas que el monitor no tiene.
        Vale(Entradas.EntradasDeCapacidades(
                "(model(X60)cmds(01 60)vcp(02 60(0F 11))mccs_ver(2.2))")
             .SequenceEqual(new byte[] { 0x0F, 0x11 }),
             "un «60» en el modelo y otro en los comandos no se cuelan");

        Vale(Entradas.EntradasDeCapacidades("(vcp(14(05 60 0B) 16))").Count == 0,
             "un 60 dentro de la lista de OTRO código no es una entrada");

        Vale(Entradas.EntradasDeCapacidades("(vcp(02 04 16))").Count == 0,
             "un monitor que no declara el 0x60 no anuncia ninguna entrada");

        Vale(Entradas.EntradasDeCapacidades("(vcp(02 60 70))").Count == 0,
             "y declarar el 0x60 SIN lista tampoco anuncia ninguna: " +
             "soporta cambiar de entrada pero no dice cuáles");

        // El `mccs_vcp` de ahí abajo existe en cadenas reales de algunos fabricantes. Con un
        // `IndexOf("vcp(")` a pelo, el bloque que se leería sería ese.
        Vale(Entradas.EntradasDeCapacidades("(mccs_vcp(60(01 02))vcp(60(0F 11)))")
             .SequenceEqual(new byte[] { 0x0F, 0x11 }),
             "«mccs_vcp(» no es «vcp(»: se lee el bloque de verdad");

        // ---------- Cadenas estropeadas ----------
        //
        // Hay monitores que devuelven la cadena truncada, o con un paréntesis sin cerrar, o
        // vacía. Ninguna puede colgar la interfaz ni dar una lista inventada: el panel se abre
        // igual y lo que pierde es la lista de botones.
        Igual(0, Entradas.EntradasDeCapacidades(null).Count, "una cadena nula no anuncia nada");
        Igual(0, Entradas.EntradasDeCapacidades("").Count, "una vacía tampoco");
        Igual(0, Entradas.EntradasDeCapacidades("   ").Count, "ni una de espacios");
        Igual(0, Entradas.EntradasDeCapacidades("(prot(monitor)type(LCD)").Count,
              "una cortada antes del vcp no anuncia nada");
        Igual(0, Entradas.EntradasDeCapacidades("(vcp(02 60(0F 11").Count,
              "un paréntesis sin cerrar no anuncia nada, y no se queda dando vueltas");
        Vale(Entradas.EntradasDeCapacidades("(vcp(60(0F 11)").SequenceEqual(new byte[] { 0x0F, 0x11 }),
             "pero si el grupo del 60 sí cerró, lo de dentro vale");
        Igual(0, Entradas.EntradasDeCapacidades("))))((((").Count,
              "y una cadena que es solo paréntesis no revienta");

        // ---------- Detalles del formato ----------
        Vale(Entradas.EntradasDeCapacidades("(VCP(60(0f 11 12)))")
             .SequenceEqual(new byte[] { 0x0F, 0x11, 0x12 }),
             "mayúsculas y minúsculas dan igual, en «VCP» y en los dígitos");

        Vale(Entradas.EntradasDeCapacidades("(vcp(60 ( 0F  11 )))")
             .SequenceEqual(new byte[] { 0x0F, 0x11 }),
             "con espacios entre el código y su paréntesis, también");

        Vale(Entradas.EntradasDeCapacidades("(vcp(60(0F F 111 11)))")
             .SequenceEqual(new byte[] { 0x0F, 0x11 }),
             "solo cuentan los códigos de DOS dígitos: un «F» suelto no es DisplayPort");

        Vale(Entradas.EntradasDeCapacidades("(vcp(60(0F 11 0F 11)))")
             .SequenceEqual(new byte[] { 0x0F, 0x11 }),
             "y los repetidos salen una sola vez: dos botones «HDMI 1» no tienen sentido");
    }

    // ================= Traducir los códigos =================

    static void NombresDeEntrada()
    {
        Grupo("monitores: cómo se llaman las entradas");

        // Los tres que se midieron en la máquina de desarrollo con los AOC de verdad.
        Igual("DisplayPort 1", Entradas.NombreDe(0x0F), "0x0F");
        Igual("HDMI 1", Entradas.NombreDe(0x11), "0x11");
        Igual("HDMI 2", Entradas.NombreDe(0x12), "0x12");
        Igual("DisplayPort 2", Entradas.NombreDe(0x10), "0x10");
        Igual("DVI 1", Entradas.NombreDe(0x03), "0x03");

        // ---------- Y lo que NO se inventa ----------
        //
        // 0x1B es el código que varios fabricantes usan para USB-C. La norma MCCS no lo define,
        // así que aquí no se le pone ese nombre: un botón «USB-C» sobre una suposición es
        // mentirle al usuario con la cara de un dato.
        Igual("Desconocido (0x1B)", Entradas.NombreDe(0x1B),
              "un código que la norma no define se enseña tal cual");
        Igual("Desconocido (0xFF)", Entradas.NombreDe(0xFF), "y otro igual");

        Vale(Entradas.EsConocida(0x11), "el HDMI 1 está en la tabla de MCCS");
        Vale(!Entradas.EsConocida(0x1B), "y el 0x1B no");

        // La ranura, para la interfaz.
        Igual("DISPLAY1", Entradas.Corto(@"\\.\DISPLAY1"), "la ranura se enseña sin barras");
        Igual("?", Entradas.Corto(null), "y sin ranura, una interrogación");
    }

    // ================= El EDID =================

    // Un EDID de 128 bytes montado a mano, con los datos del monitor objetivo: AOC (PnP id
    // 0x05E3), código de producto 0xB309 y el modelo escrito en un descriptor 0xFC.
    //
    // Montarlo aquí en vez de guardar un volcado de 128 bytes como fichero de prueba es a
    // propósito: así se VE qué byte significa qué, y la prueba documenta el formato además de
    // comprobarlo.
    static byte[] EdidDePrueba(ushort fabricante = 0x05E3, ushort producto = 0xB309,
                               uint serie = 0x12345678,
                               string? nombre = "Q27G3G3R3", string? serieTexto = null,
                               bool cabeceraBuena = true)
    {
        var e = new byte[128];

        if (cabeceraBuena)
            for (int i = 1; i <= 6; i++) e[i] = 0xFF;      // 00 FF FF FF FF FF FF 00

        e[8] = (byte)(fabricante >> 8);                   // big-endian, cinco bits por letra
        e[9] = (byte)(fabricante & 0xFF);
        e[10] = (byte)(producto & 0xFF);                  // little-endian
        e[11] = (byte)(producto >> 8);
        e[12] = (byte)(serie & 0xFF);
        e[13] = (byte)((serie >> 8) & 0xFF);
        e[14] = (byte)((serie >> 16) & 0xFF);
        e[15] = (byte)((serie >> 24) & 0xFF);

        int d = 54;
        foreach (var (etiqueta, texto) in new[] { ((byte)0xFC, nombre), ((byte)0xFF, serieTexto) })
        {
            if (texto is null) continue;
            e[d] = 0; e[d + 1] = 0; e[d + 2] = 0; e[d + 3] = etiqueta; e[d + 4] = 0;
            for (int i = 0; i < 13; i++)
                e[d + 5 + i] = i < texto.Length ? (byte)texto[i] : (i == texto.Length ? (byte)0x0A : (byte)0x20);
            d += 18;
        }

        return e;
    }

    static void EdidDiceQuienEs()
    {
        Grupo("monitores: quién es cada monitor, según su EDID");

        var d = Edid.Parsear(EdidDePrueba());
        Vale(d is not null, "un EDID bien formado se descifra");
        if (d is null) return;

        Igual("AOC", d.Fabricante, "el fabricante sale de los cinco bits por letra");
        Igual((ushort)0xB309, d.Codigo, "el código de producto del Q27G3G3R3");
        Igual("Q27G3G3R3", d.Nombre, "y el modelo, del descriptor 0xFC");
        Igual("AOC Q27G3G3R3", d.Rotulo, "el rótulo lleva el fabricante delante");
        Vale(d.Clave.StartsWith("AOCB309", StringComparison.Ordinal),
             $"la clave estable empieza por fabricante y código ({d.Clave})");

        // ---------- El otro monitor del equipo ----------
        var otro = Edid.Parsear(EdidDePrueba(producto: 0xB322, nombre: "25G3ZM"));
        Igual("AOC 25G3ZM", otro?.Rotulo, "el 25G3ZM también");
        Vale(otro?.Clave != d.Clave, "y los dos AOC NO comparten clave");

        // ---------- Dos iguales se distinguen por la serie ----------
        //
        // Es el caso que haría que un clic fuera al monitor de al lado: dos pantallas del mismo
        // modelo. La serie es lo que los separa.
        var gemelo = Edid.Parsear(EdidDePrueba(serie: 0x9999));
        Vale(gemelo?.Clave != d.Clave,
             "dos monitores del mismo modelo con serie distinta tienen claves distintas");

        // La serie en LETRA manda sobre la numérica cuando el monitor la trae: es la que está
        // impresa en la pegatina de atrás.
        var conTexto = Edid.Parsear(EdidDePrueba(serieTexto: "ABC123XYZ"));
        Vale(conTexto?.Clave.Contains("ABC123XYZ", StringComparison.Ordinal) == true,
             $"con serie en texto, la clave la usa ({conTexto?.Clave})");

        // ---------- Un monitor callado ----------
        //
        // Sin descriptor de nombre no hay rótulo bonito, pero sigue habiendo identidad: el
        // fabricante y el código de producto son campos OBLIGATORIOS del EDID.
        var callado = Edid.Parsear(EdidDePrueba(nombre: null));
        Vale(callado is not null, "un EDID sin descriptor de nombre se descifra igual");
        Igual("AOC B309", callado?.Rotulo, "y el rótulo cae al código de producto");

        // Sin serie de ninguna clase la clave es solo fabricante y modelo. Es lo mejor que se
        // puede hacer, y por eso `Entradas.Desempatar` existe.
        var anonimo = Edid.Parsear(EdidDePrueba(serie: 0, nombre: null));
        Igual("AOCB309", anonimo?.Clave, "sin serie, la clave es fabricante y código");

        // ---------- Nombre partido en dos descriptores ----------
        //
        // Un modelo de más de 13 caracteres no cabe en un descriptor y los fabricantes lo
        // parten en dos 0xFC seguidos. Quedarse con el primero daba modelos a medias.
        var largo = new byte[128];
        Array.Copy(EdidDePrueba(nombre: null), largo, 128);
        Partir(largo, 54, 0xFC, "MONITOR-MUY-");    // 12 caracteres, sin 0x0A: sigue
        Partir(largo, 72, 0xFC, "LARGO");
        var junto = Edid.Parsear(largo);
        Igual("MONITOR-MUY-LARGO", junto?.Nombre, "un nombre partido en dos descriptores se junta");

        // ---------- Y lo que NO es un EDID ----------
        //
        // En el registro hay claves de monitores desconectados con valores a medias. Descifrar
        // basura «con éxito» sacaría un fabricante de tres letras aleatorias en la interfaz.
        Vale(Edid.Parsear(null) is null, "nada no es un EDID");
        Vale(Edid.Parsear(new byte[0]) is null, "ni cero bytes");
        Vale(Edid.Parsear(new byte[64]) is null, "ni 64 bytes: el EDID son 128");
        Vale(Edid.Parsear(EdidDePrueba(cabeceraBuena: false)) is null,
             "ni 128 bytes con la cabecera mal");
        Vale(Edid.Parsear(EdidDePrueba(fabricante: 0x0000)) is null,
             "ni uno cuyo fabricante no son tres letras");
    }

    // ================= Que se pueda PONER =================
    //
    // ---------- Por qué esto es una prueba y no una suposición ----------
    //
    // `MainWindow.Widgets.cs` lleva escrito que registrar un widget basta para que salga en los
    // tres sitios —la isla, la lista de módulos de Ajustes y la biblioteca—, y `VentanaAjustes`
    // repite la promesa en su comentario: «ahora registrar un widget basta para que salga en los
    // tres sitios». Las dos frases cuentan un fallo que ya pasó: había un `switch` de dieciséis
    // casos y los sitios que lo copiaban se olvidaban de los módulos nuevos.
    //
    // Una promesa de ese tipo sin prueba se rompe sin que nadie se entere: el módulo compila,
    // funciona en la barra de quien lo escribió, y no aparece en Ajustes para nadie más. Así que
    // aquí se comprueba por los CUATRO caminos que el usuario tiene para ponerlo.
    static void ModuloSeOfreceEnAjustesYBiblioteca()
    {
        Grupo("monitores: el módulo se puede poner desde Ajustes, la biblioteca y el editor");

        RegistroWidgets.Asegurar();

        // ---------- 1. Está registrado ----------
        Vale(RegistroWidgets.Existe("monitores"), "«monitores» está en el registro de widgets");

        var w = RegistroWidgets.Buscar("monitores");
        Vale(w is not null, "y se puede buscar por su id");
        if (w is null) return;
        var d = w.Descriptor;

        Igual("Monitores", d.Nombre, "su nombre");
        Igual(CategoriaWidget.Sistema, d.Categoria, "va con los de sistema");
        Igual("Derecha", d.ZonaPreferida,
              "y al añadirlo cae a la derecha, como el resto de los de estado");
        Vale(!string.IsNullOrWhiteSpace(d.Glifo), "tiene glifo: sin él, el editor enseña un hueco");
        Vale(!d.Repetible, "no es repetible: dos iconos para el mismo panel no tienen sentido");
        Vale(!string.IsNullOrWhiteSpace(d.Descripcion), "y una descripción para la biblioteca");

        // Y el aviso, que aquí NO es decorativo: en un equipo cuyos monitores no gestionen su
        // entrada el icono no se dibuja nunca, y sin esta nota el editor dejaría un hueco
        // invisible sin decir por qué.
        Vale(d.PuedeNoVerse is not null, "avisa de que puede no verse");

        // ---------- 2. La lista de Ajustes ----------
        //
        // `VentanaAjustes.ModulosDisponibles()` es exactamente `RegistroWidgets.Ids.ToList()`
        // (ver VentanaAjustes.cs), así que comprobar la fuente comprueba la lista.
        Vale(RegistroWidgets.Ids.Contains("monitores"),
             "sale en la lista de módulos que lee Ajustes");

        // ---------- 3. La biblioteca ----------
        //
        // Se nutre de `PorCategoria()` y de `DeCategoria()`: los dos caminos, porque la
        // biblioteca usa uno cuando filtras por categoría y el otro cuando no.
        Vale(RegistroWidgets.DeCategoria(CategoriaWidget.Sistema).Any(x => x.Descriptor.Id == "monitores"),
             "sale al filtrar la biblioteca por «Sistema»");
        Vale(RegistroWidgets.PorCategoria()
                .Any(g => g.Key == CategoriaWidget.Sistema && g.Any(x => x.Descriptor.Id == "monitores")),
             "y sale en la biblioteca sin filtrar, dentro de su grupo");

        // ---------- 4. El editor de módulos ----------
        //
        // Lo que de verdad contesta «¿puedo ponerlo?»: una configuración que NO lo lleva tiene
        // que ofrecer su ficha para añadirlo.
        var sinEl = ZonasDePrueba("""
        { "Modulos": { "Izquierda": ["inicio"], "Centro": ["reloj"], "Derecha": ["cpu"],
                       "Ocultos": [] } }
        """);
        var fichas = EditorModulos.Fichas(sinEl);
        Vale(fichas.Any(f => f.Id == "monitores"),
             "el editor ofrece su ficha aunque no esté puesto");
        if (!fichas.Any(f => f.Id == "monitores")) return;

        // `Ficha` es un `record struct`, así que `FirstOrDefault` no devuelve null: devolvería
        // una ficha vacía que pasaría las comprobaciones de abajo sin que nadie lo notara. De ahí
        // el `Any` de arriba y el `First` de aquí.
        var ficha = fichas.First(f => f.Id == "monitores");

        Vale(!ficha.Puesto, "y la da como «no puesto»");
        Igual("Sin poner", EditorModulos.Estado(ficha), "dicho sin tener que deducirlo");
        Igual(-1, ficha.Posicion, "sin posición todavía");
        Vale(EditorModulos.Explicar(ficha).Contains("Aviso:"),
             "y su ayuda trae el aviso de que puede no verse");

        // ---------- Y puesto, se reconoce donde está ----------
        //
        // El viaje de vuelta. Sin esto, «sale en la lista» podría ser verdad y «ponerlo
        // funciona» mentira: son dos caminos distintos del mismo modelo.
        var conEl = ZonasDePrueba("""
        { "Modulos": { "Izquierda": ["inicio"], "Centro": ["reloj"],
                       "Derecha": ["cpu", "monitores", "fecha"], "Ocultos": [] } }
        """);
        var deConEl = EditorModulos.Fichas(conEl);
        Vale(deConEl.Any(f => f.Id == "monitores"), "sigue teniendo ficha estando puesto");
        var puesta = deConEl.First(f => f.Id == "monitores");
        Vale(puesta.Puesto, "puesto en el config, el editor lo da como puesto");
        Igual("Derecha", puesta.Zona, "en su zona");
        Igual(1, puesta.Posicion, "y en su sitio dentro de ella");

        // ---------- Lo que NO pasa: nadie se lo encuentra sin pedirlo ----------
        //
        // El módulo es opcional a propósito. Un cambio de versión que te mete un icono nuevo en
        // la barra sin preguntar es un cambio que mueve de sitio todos los demás, y esta casa ya
        // tiene maquinaria para añadir uno a todo el mundo cuando se decide (`AnadirUnaVez` y las
        // banderas `…DeSerie` de `Config.ModulosDeSerie`). Mientras no se use, no sale solo.
        var serie = new Modulos();
        Vale(!serie.Izquierda.Contains("monitores") &&
             !serie.Centro.Contains("monitores") &&
             !serie.Derecha.Contains("monitores"),
             "la barra de serie NO lo trae puesto: se añade desde Ajustes");
    }

    // Mete un descriptor de texto a pelo, sin el 0x0A del final: así es como viaja la primera
    // mitad de un nombre partido.
    static void Partir(byte[] e, int d, byte etiqueta, string texto)
    {
        e[d] = 0; e[d + 1] = 0; e[d + 2] = 0; e[d + 3] = etiqueta; e[d + 4] = 0;
        for (int i = 0; i < 13; i++)
            e[d + 5 + i] = i < texto.Length ? (byte)texto[i] : (byte)0x20;
    }

    // ================= De dónde se saca el EDID =================

    static void RutaDelEdidEnElRegistro()
    {
        Grupo("monitores: la ruta del EDID en el registro");

        // El nombre de interfaz tal como lo devuelve `EnumDisplayDevices` con
        // EDD_GET_DEVICE_INTERFACE_NAME, y la ruta que le corresponde. Es puro manejo de
        // cadenas y es la parte que se rompe sola el día que alguien «limpie» el formato.
        Igual(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\AOC2432\5&1a2b3c4d&0&UID4353\Device Parameters",
              Edid.RutaDelRegistro(
                  @"\\?\DISPLAY#AOC2432#5&1a2b3c4d&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}"),
              "el nombre de interfaz se convierte en ruta del registro");

        Igual(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\AOC2432\5&1a2b3c4d&0&UID4353\Device Parameters",
              Edid.RutaDelRegistro(@"\\.\DISPLAY#AOC2432#5&1a2b3c4d&0&UID4353#{e6f07b5f}"),
              "con el prefijo \\\\.\\ también");

        Igual(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\AOC2432\5&1a2b3c4d&0&UID4353\Device Parameters",
              Edid.RutaDelRegistro(@"DISPLAY#AOC2432#5&1a2b3c4d&0&UID4353"),
              "y sin prefijo ni GUID, igual");

        Vale(Edid.RutaDelRegistro(null) is null, "sin nombre de interfaz no hay ruta");
        Vale(Edid.RutaDelRegistro("") is null, "ni con una cadena vacía");
        Vale(Edid.RutaDelRegistro(@"\\?\DISPLAY#AOC2432") is null,
             "ni sin la instancia: no habría clave que abrir");
    }
}
