using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace NubeBar;

// D7: las pruebas de fiabilidad, recuperación y modo seguro.
//
// Lo que se puede comprobar sin ojos es más de lo que parece: que el supervisor no se
// precipite, que el modo seguro apague la organización y **no** la isla, que el cierre no
// deje nada vivo, que una posición recordada en un monitor que ya no existe se descarte, que
// un config roto se rescate de la copia más nueva que sirva, y que quinientas ventanas
// pasen por el motor en un tiempo razonable.
//
// Lo que no se puede: matar el proceso de verdad, desconectar un monitor de verdad y
// cambiar el DPI de verdad. Eso queda en la lista de pruebas manuales, no colado como
// verificado.
public static partial class Pruebas
{
    internal static void CorrerD7()
    {
        D7ElSupervisorNoSePrecipita();
        D7ElModoSeguroApagaLaOrganizacionYNoLaIsla();
        D7LasRachasDeFallos();
        D7ElCierreNoDejaNadaVivo();
        D7LaAppQueSeVaYVuelve();
        D7UnMonitorQueYaNoEsta();
        D7RescatarUnConfigRoto();
        D7ElRescateConservaLoDesconocido();
        D7QuinientasVentanas();
    }

    // ================= El supervisor =================

    static HyprSalud.Señales Señal(
        double segundos = 0, int vivos = 19, int esperados = 19,
        int fallos = 0, int excepciones = 0, bool encendida = true) =>
        new(TimeSpan.FromSeconds(segundos), vivos, esperados, fallos, excepciones, encendida);

    static void D7ElSupervisorNoSePrecipita()
    {
        Grupo("D7: el supervisor prefiere no hacer nada");

        // La regla que gobierna la fase, y está en el documento como riesgo: *una
        // recuperación automática demasiado agresiva puede empeorar el problema*.
        Igual(HyprSalud.Accion.Nada, HyprSalud.Mirar(Señal()).Que, "todo en orden: nada");
        Igual(HyprSalud.Estado.Sano, HyprSalud.Mirar(Señal()).Como, "y el estado es sano");

        // ---------- Con la capa apagada no se supervisa nada ----------
        //
        // Esto va primero en el código y aquí también, porque sin él una instantánea vieja
        // con la capa apagada —lo normal con el equipo en reposo— pediría modo seguro sin
        // que hubiera nada que apagar.
        var apagada = HyprSalud.Mirar(Señal(segundos: 600, vivos: 0, esperados: 0,
                                            fallos: 99, excepciones: 99, encendida: false));
        Igual(HyprSalud.Accion.Nada, apagada.Que, "con la capa apagada, nunca se actúa");
        Igual(HyprSalud.Estado.Sano, apagada.Como, "y no se inventa un problema");

        // ---------- Sospechoso: se apunta y no se toca nada ----------
        //
        // Es la mitad del valor de esto: que el registro diga qué pasaba ANTES de apagar, en
        // vez de saltar de "todo bien" a "modo seguro" sin nada en medio.
        var sospecha = HyprSalud.Mirar(Señal(fallos: HyprSalud.FallosParaSospechar));
        Igual(HyprSalud.Estado.Sospechoso, sospecha.Como, "cinco fallos: sospechoso");
        Igual(HyprSalud.Accion.Mirar, sospecha.Que, "y la acción es mirar, no apagar");
        Vale(sospecha.Porque.Contains("5"), "y se dice cuántos");

        var vieja = HyprSalud.Mirar(Señal(segundos: HyprSalud.InstantaneaVieja.TotalSeconds));
        Igual(HyprSalud.Estado.Sospechoso, vieja.Como, "una instantánea de 30 s: sospechoso");
        Igual(HyprSalud.Accion.Mirar, vieja.Que, "tampoco se apaga por eso");

        var sinAtajos = HyprSalud.Mirar(Señal(vivos: 0));
        Igual(HyprSalud.Estado.Sospechoso, sinAtajos.Como,
              "19 atajos esperados y ninguno vivo: sospechoso");

        // ---------- Detenido: ahí sí ----------
        var muerta = HyprSalud.Mirar(Señal(segundos: HyprSalud.InstantaneaMuerta.TotalSeconds + 1));
        Igual(HyprSalud.Estado.Detenido, muerta.Como, "tres minutos sin refrescar: detenido");
        Igual(HyprSalud.Accion.ModoSeguro, muerta.Que, "y eso sí pide modo seguro");
        Vale(muerta.Porque.Contains("gancho"), "y se dice que el gancho no está vivo");

        var muchosFallos = HyprSalud.Mirar(Señal(fallos: HyprSalud.FallosParaParar));
        Igual(HyprSalud.Accion.ModoSeguro, muchosFallos.Que, "quince fallos seguidos: modo seguro");

        var revienta = HyprSalud.Mirar(Señal(excepciones: HyprSalud.ExcepcionesParaParar));
        Igual(HyprSalud.Accion.ModoSeguro, revienta.Que, "cinco excepciones seguidas, también");

        // ---------- Y los umbrales son generosos a propósito ----------
        //
        // Un supervisor nervioso que apaga la capa cada vez que una ventana tarda es peor
        // que no tener supervisor: el usuario no sabría por qué se apaga y lo dejaría
        // apagado.
        Vale(HyprSalud.FallosParaParar > HyprSalud.FallosParaSospechar,
             "sospechar antes que parar");
        Vale(HyprSalud.InstantaneaMuerta > HyprSalud.InstantaneaVieja, "y vieja antes que muerta");
        Vale(HyprSalud.InstantaneaMuerta.TotalSeconds >= 120,
             "hacen falta minutos sin refrescar, no segundos");
        Vale(HyprSalud.Mirar(Señal(fallos: 1)).Que == HyprSalud.Accion.Nada,
             "un fallo suelto no es nada: pasa todos los días");
        Vale(HyprSalud.Mirar(Señal(fallos: 4)).Que == HyprSalud.Accion.Nada,
             "ni cuatro");

        // Y no existe ninguna acción que REORGANICE. Es el otro lado de la misma regla: el
        // supervisor deja de tocar, nunca toca más.
        Vale(Enum.GetNames<HyprSalud.Accion>().All(n => n is "Nada" or "Mirar" or "ModoSeguro"),
             "las únicas acciones son no hacer nada, mirar y apagar");
    }

    static void D7ElModoSeguroApagaLaOrganizacionYNoLaIsla()
    {
        Grupo("D7: el modo seguro apaga organizar ventanas, no NubeBar");

        HyprSalud.Olvidar();
        Vale(!HyprSalud.Seguro, "de serie no se está en modo seguro");

        // ---------- El modo efectivo, con el seguro por encima de todo ----------
        Igual(NubeHypr.Modo.Simulacion, HyprSalud.ModoConSeguro(true, "simulacion"),
              "sin modo seguro, manda la configuración");
        Igual(NubeHypr.Modo.Aplicar, HyprSalud.ModoConSeguro(true, "aplicar"),
              "y «aplicar» es «aplicar»");

        Vale(HyprSalud.Entrar("una prueba"), "se entra en modo seguro");
        Vale(HyprSalud.Seguro, "y queda puesto");
        Igual("una prueba", HyprSalud.RazonDelSeguro, "con su motivo guardado");

        // Esto es lo que de verdad importa: encendida o no, en simulación o en aplicar, en
        // modo seguro NO se organiza nada.
        foreach (var modo in new[] { "simulacion", "aplicar", "cualquier-cosa" })
            Igual(NubeHypr.Modo.Apagado, HyprSalud.ModoConSeguro(true, modo),
                  $"en modo seguro, «{modo}» queda en apagado");

        Vale(!HyprSalud.Entrar("otra vez"), "entrar dos veces no vuelve a entrar");
        Igual("una prueba", HyprSalud.RazonDelSeguro, "y no se pierde el motivo original");
        Igual(1, HyprSalud.VecesQueSeHaEntrado, "y se cuenta una sola entrada");

        // ---------- La línea de estado, con el formato del documento ----------
        var linea = HyprSalud.Linea(version: 4);
        Vale(linea.Contains("safe-mode=true"), "la línea dice que está en modo seguro");
        Vale(linea.Contains("config-version=4"), "y la versión del config que se le pasa");
        Vale(linea.Contains("adapter="), "y el estado del adaptador");
        Vale(linea.Contains("reason="), "y en modo seguro, el motivo");

        // Sin versión escribe `?`, y eso es información: significa "nadie me ha dicho de qué
        // fichero hablamos". Con `Esquema.Actual` como respaldo, el registro decía v4 y v3
        // del MISMO fichero en dos líneas seguidas — lo vio el primer `--diagd7`.
        Vale(HyprSalud.Linea().Contains("config-version=?"),
             "sin decirle la versión, no se la inventa");
        Vale(!HyprSalud.Linea().Contains($"config-version={Esquema.Actual}"),
             "y no usa la versión del esquema del código como si fuera la del usuario");

        Vale(HyprSalud.Salir(), "se sale a mano");
        Vale(!HyprSalud.Seguro, "y queda fuera");
        Vale(!HyprSalud.Linea().Contains("reason="), "fuera del modo seguro no hay motivo que dar");
        Vale(!HyprSalud.Salir(), "salir dos veces no es salir");

        // ---------- Y no se sale solo ----------
        //
        // El supervisor no saca del modo seguro aunque las señales vuelvan a estar bien: si
        // el problema va y viene, entraría y saldría en bucle, y cada entrada apaga la capa
        // de golpe mientras el usuario la está usando.
        HyprSalud.Entrar("para comprobar que no se sale solo");
        var sano = HyprSalud.Mirar(Señal());
        Igual(HyprSalud.Accion.Nada, sano.Que, "con todo en orden no hay acción");
        Vale(HyprSalud.Seguro, "y se SIGUE en modo seguro: salir es siempre a mano");
        HyprSalud.Olvidar();

        // ---------- El modo seguro no es una clave de configuración ----------
        //
        // Si se guardara, un fallo de una tarde dejaría NubeHypr apagado para siempre sin
        // que el usuario supiera por qué.
        Vale(!Perfiles.Permitida("NubeHypr", "ModoSeguro"), "no es una clave de perfil");
        var opc = new OpcionesHypr();
        Vale(opc.GetType().GetProperty("ModoSeguro") == null,
             "y no existe en las opciones: es estado de ejecución, no configuración");
    }

    static void D7LasRachasDeFallos()
    {
        Grupo("D7: las rachas, no el total");

        HyprSalud.OlvidarContadores();
        Igual(0, HyprSalud.FallosSeguidos, "se empieza a cero");

        for (int i = 0; i < 4; i++) HyprSalud.SalioMal();
        Igual(4, HyprSalud.FallosSeguidos, "cuatro fallos seguidos");
        Igual(4L, HyprSalud.TotalFallos, "y el total también los cuenta");

        // Un acierto borra la racha. Si no la borrara, cinco fallos repartidos en una semana
        // apagarían la capa igual que cinco seguidos, y no es lo mismo.
        HyprSalud.SalioBien();
        Igual(0, HyprSalud.FallosSeguidos, "un acierto borra la racha");
        Igual(4L, HyprSalud.TotalFallos, "pero el total no se borra: es historia");

        HyprSalud.Reventó();
        Igual(1, HyprSalud.ExcepcionesSeguidas, "las excepciones se cuentan aparte");
        HyprSalud.SalioBien();
        Igual(0, HyprSalud.ExcepcionesSeguidas, "y un acierto también las borra");

        HyprSalud.OlvidarContadores();
    }

    static void D7ElCierreNoDejaNadaVivo()
    {
        Grupo("D7: el cierre no deja hooks, suscripciones ni memorias vivas");

        // El inventario se MIDE. Es lo que permite afirmar "no queda nada atrapado" en vez
        // de confiarlo al orden de las líneas del cierre.
        var limpio = new HyprSalud.Inventario(0, false, false, false, 0, 0, false, 0, 0);
        Vale(limpio.Limpio, "un inventario a cero está limpio");
        Igual(0, limpio.Total, "y su total es cero");
        Vale(limpio.ToString().Contains("LIMPIO"), "y lo dice con esa palabra");

        // Cada cosa que quede suelta cuenta, y se nota POR NOMBRE: "quedan 2" es inútil,
        // "atajos=2" se arregla.
        var conAtajos = limpio with { Atajos = 2 };
        Vale(!conAtajos.Limpio, "dos atajos sin soltar: no está limpio");
        Vale(conAtajos.ToString().Contains("atajos=2"), "y se dice qué quedó");

        foreach (var sucio in new[]
        {
            limpio with { Atajos = 1 },
            limpio with { ReglasEscuchando = true },
            limpio with { VistaEscuchando = true },
            limpio with { PreviaViva = true },
            limpio with { PosicionesRecordadas = 1 },
            limpio with { VentanasVistas = 1 },
            limpio with { GestoEnCurso = true },
            limpio with { MarcasDeGesto = 1 },
            limpio with { SitiosRecordados = 1 },
        })
            Vale(!sucio.Limpio, $"esto no está limpio: {sucio}");

        // ---------- Y la lista de lo que hay que soltar está declarada ----------
        //
        // La forma de que una se quede sin soltar no es escribirla mal: es añadir la octava
        // y no acordarse del cierre. Con la lista declarada, esta prueba falla el día que
        // el inventario tenga un campo más y la lista no.
        // Nueve desde D9.1: las siete de D7 más las dos memorias nuevas. El número está
        // escrito a mano a propósito — es lo que hace que esta prueba falle el día que el
        // inventario crezca y la lista declarada no, que es el descuido que de verdad pasa.
        Igual(9, HyprSalud.HayQueSoltar.Length, "nueve cosas declaradas");
        Igual(9, typeof(HyprSalud.Inventario).GetProperties()
                     .Count(p => p.Name is not ("Total" or "Limpio")),
              "y el inventario mide exactamente esas nueve");
        Vale(HyprSalud.HayQueSoltar.All(s => s.Length > 5), "cada una dicha con palabras");
    }

    // ================= Las aplicaciones que se van y vuelven =================

    static void D7LaAppQueSeVaYVuelve()
    {
        Grupo("D7: una aplicación que se reinicia vuelve a su sitio (decidido, no movido)");

        var areas = new List<NubeHypr.Rect> { new(0, 0, 1920, 1040) };
        var memoria = new HyprRegreso.Memoria();

        // La huella es ejecutable + clase, NO el título: un navegador cambia de título cada
        // vez que cambias de pestaña, y entonces cada pestaña sería otra aplicación.
        var h1 = HyprRegreso.Huella(@"C:\Program Files\Editor\code.exe", "Chrome_WidgetWin_1");
        var h2 = HyprRegreso.Huella(@"D:\otra\ruta\code.exe", "Chrome_WidgetWin_1");
        Igual(h1, h2, "la misma aplicación en otra ruta es la misma huella");
        Vale(h1 != HyprRegreso.Huella(@"C:\x\code.exe", "OtraClase"),
             "pero otra clase de ventana es otra cosa: el diálogo de copiar no es la ventana principal");

        // ---------- Se apunta al IRSE ----------
        memoria.Apuntar(h1, new NubeHypr.Rect(100, 50, 800, 600), false);
        Igual(1, memoria.Cuantas, "se recuerda un sitio");

        // ---------- Y se usa al volver ----------
        var vuelve = new VentanaModelo((IntPtr)77, "code", "code - nuevo",
                                       new NubeHypr.Rect(700, 400, 640, 480));
        var d = HyprRegreso.Decidir(vuelve, "Chrome_WidgetWin_1", @"C:\x\code.exe",
                                    memoria, areas, NubeHypr.Modo.Simulacion);
        Igual(HyprRegreso.Que.Devolver, d.Hacer, "se decide devolverla");
        Igual(100.0, d.Destino.X, "al sitio exacto que tenía");
        Vale(d.Porque.Contains("estaba ahí"), "y se dice por qué");

        // Una vez usada se olvida: si no, cada ventana nueva del programa iría al mismo
        // sitio y acabarían todas apiladas una encima de otra.
        Igual(0, memoria.Cuantas, "el sitio se gasta al usarlo");
        Igual(HyprRegreso.Que.Nada,
              HyprRegreso.Decidir(vuelve, "Chrome_WidgetWin_1", @"C:\x\code.exe",
                                  memoria, areas, NubeHypr.Modo.Simulacion).Hacer,
              "y la segunda ventana ya no se coloca encima de la primera");

        // ---------- Los casos en que NO se toca ----------
        memoria.Apuntar(h1, new NubeHypr.Rect(100, 50, 800, 600), false);

        Igual(HyprRegreso.Que.Nada,
              HyprRegreso.Decidir(vuelve, "c", "r", memoria, areas, NubeHypr.Modo.Apagado).Hacer,
              "con la capa apagada no se decide nada");
        Igual(1, memoria.Cuantas, "y apagado no se toca ni la memoria");

        var minimizada = vuelve with { Minimizada = true };
        var dMin = HyprRegreso.Decidir(minimizada, "Chrome_WidgetWin_1", @"C:\x\code.exe",
                                       memoria, areas, NubeHypr.Modo.Simulacion);
        Igual(HyprRegreso.Que.Nada, dMin.Hacer, "una minimizada no se coloca");
        Vale(dMin.Porque.Contains("minimizada"), "y se dice por qué: restaurarla sería peor");

        // Ya está donde estaba: no se toca. Mover una ventana a donde ya está es un
        // SetWindowPos que no hace nada visible pero sí puede robarle el foco.
        var yaEsta = vuelve with { Rectangulo = new NubeHypr.Rect(102, 51, 800, 600) };
        var dYa = HyprRegreso.Decidir(yaEsta, "Chrome_WidgetWin_1", @"C:\x\code.exe",
                                      memoria, areas, NubeHypr.Modo.Simulacion);
        Igual(HyprRegreso.Que.Nada, dYa.Hacer, "si ya está ahí (±8 px), no se toca");
        Vale(dYa.Porque.Contains("ya está"), "y se dice");

        // Sin nada recordado, el motivo lo dice también: "mi editor no volvió a su sitio" se
        // contesta leyendo por qué, no adivinando entre cinco causas.
        var sinNada = HyprRegreso.Decidir(vuelve, "OtraClase", @"C:\x\otro.exe",
                                          memoria, areas, NubeHypr.Modo.Simulacion);
        Vale(sinNada.Porque.Contains("no hay sitio recordado"), "sin recuerdo, se dice");

        // ---------- Caducidad y tope ----------
        var vieja = new HyprRegreso.Memoria();
        var hace3h = DateTime.Now - TimeSpan.FromHours(3);
        vieja.Apuntar(h1, new NubeHypr.Rect(10, 10, 400, 300), false, hace3h);
        Vale(vieja.Buscar(h1) == null, "un sitio de hace tres horas ha caducado");
        Igual(0, vieja.Cuantas, "y se tira al consultarlo");

        var muchos = new HyprRegreso.Memoria();
        for (int i = 0; i < HyprRegreso.MaxSitios + 20; i++)
            muchos.Apuntar("app" + i, new NubeHypr.Rect(0, 0, 100, 100), false,
                           DateTime.Now.AddMinutes(-i));
        Vale(muchos.Cuantas <= HyprRegreso.MaxSitios,
             $"la memoria no crece sin límite ({muchos.Cuantas} <= {HyprRegreso.MaxSitios})");
        Vale(muchos.Buscar("app0") != null, "y lo que se conserva es lo RECIENTE");

        // Un rectángulo inválido no se apunta: restaurar a 0x0 sería peor que no restaurar.
        var malo = new HyprRegreso.Memoria();
        malo.Apuntar(h1, new NubeHypr.Rect(0, 0, 0, 0), false);
        Igual(0, malo.Cuantas, "un rectángulo sin tamaño no se recuerda");

        // ---------- La línea del registro ----------
        var m2 = new HyprRegreso.Memoria();
        m2.Apuntar(h1, new NubeHypr.Rect(100, 50, 800, 600), false);
        var dec = HyprRegreso.Decidir(vuelve, "Chrome_WidgetWin_1", @"C:\x\code.exe",
                                      m2, areas, NubeHypr.Modo.Simulacion);
        var linea = HyprRegreso.Linea(vuelve, dec, NubeHypr.Modo.Simulacion);
        Vale(linea.Contains("hwnd=0x"), "la línea lleva el hwnd");
        Vale(linea.Contains("proceso="), "el proceso");
        Vale(linea.Contains("efecto=devolver"), "el efecto");
        Vale(linea.Contains("resultado=solo apuntado"),
             "y que en simulación SOLO SE APUNTA: no se ha movido nada");
        Vale(linea.Contains("modo=simulacion"), "y el modo");
    }

    static void D7UnMonitorQueYaNoEsta()
    {
        Grupo("D7: una posición en un monitor desconectado se descarta");

        // ---------- El caso que esto evita ----------
        //
        // Tenías el editor en el monitor de la derecha, lo cierras, desconectas ese monitor y
        // lo vuelves a abrir. La posición recordada está en coordenadas que ya no existen: la
        // ventana se restauraría FUERA de la pantalla, y tendrías una ventana abierta que no
        // puedes ver ni alcanzar con el ratón.
        var soloUno = new List<NubeHypr.Rect> { new(0, 0, 1920, 1040) };
        var dos = new List<NubeHypr.Rect> { new(0, 0, 1920, 1040), new(1920, 0, 1920, 1080) };

        var enElSegundo = new NubeHypr.Rect(2400, 200, 800, 600);
        Vale(HyprRegreso.SigueCabiendo(enElSegundo, dos), "con dos monitores, cabe");
        Vale(!HyprRegreso.SigueCabiendo(enElSegundo, soloUno),
             "con uno solo, ya no: se descarta en vez de dejar la ventana fuera de pantalla");

        // ---------- "Tocar" no es "caber" ----------
        //
        // Una ventana con cuatro píxeles dentro de la pantalla no se puede coger por la barra
        // de título. Hace falta un trozo de verdad.
        var asomando = new NubeHypr.Rect(1900, 500, 800, 600);   // 20 px dentro
        Vale(!HyprRegreso.SigueCabiendo(asomando, soloUno),
             "asomar 20 px no es caber: no se podría coger con el ratón");
        var dentroDeVerdad = new NubeHypr.Rect(1700, 500, 800, 600);   // 220 px dentro
        Vale(HyprRegreso.SigueCabiendo(dentroDeVerdad, soloUno), "220 px sí es caber");

        Vale(!HyprRegreso.SigueCabiendo(new NubeHypr.Rect(0, 0, 0, 0), soloUno),
             "un rectángulo sin tamaño no cabe en ninguna parte");
        Vale(!HyprRegreso.SigueCabiendo(enElSegundo, new List<NubeHypr.Rect>()),
             "y sin monitores, nada cabe");

        // ---------- Y la decisión lo dice ----------
        var memoria = new HyprRegreso.Memoria();
        var h = HyprRegreso.Huella("code.exe", "Clase");
        memoria.Apuntar(h, enElSegundo, false);
        var v = new VentanaModelo((IntPtr)5, "code", "t", new NubeHypr.Rect(50, 50, 400, 300));
        var d = HyprRegreso.Decidir(v, "Clase", "code.exe", memoria, soloUno,
                                    NubeHypr.Modo.Simulacion);
        Igual(HyprRegreso.Que.Nada, d.Hacer, "no se devuelve a un monitor que no está");
        Vale(d.Porque.Contains("ya no cae en ninguna pantalla"), "y se explica con esas palabras");
    }

    // ================= Rescatar la configuración =================

    static string CarpetaDePrueba(string nombre)
    {
        var dir = Path.Combine(Path.GetTempPath(), "nubebar-pruebas-d7", nombre);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void D7RescatarUnConfigRoto()
    {
        Grupo("D7: un config roto se rescata de la copia más nueva que sirva");

        // Sobre ficheros de PRUEBA en el temporal. Nunca sobre el config del usuario: eso es
        // lo que no se puede hacer, y por eso el rescate es una función sobre una ruta y no
        // algo pegado a `Config.Ruta`.
        var dir = CarpetaDePrueba("rescate");
        var cfg = Path.Combine(dir, "config.json");

        // ---------- El juicio: "se puede parsear" no es suficiente ----------
        Vale(HyprRescate.Juzgar(null).Como == HyprRescate.Estado.NoExiste, "sin fichero");
        Vale(HyprRescate.Juzgar("").Como == HyprRescate.Estado.Vacio, "vacío");
        Vale(HyprRescate.Juzgar("   ").Como == HyprRescate.Estado.Vacio, "en blanco");
        Vale(HyprRescate.Juzgar("{\"Version\": 4").Como == HyprRescate.Estado.NoEsJson,
             "cortado a medias");
        Vale(HyprRescate.Juzgar("[1,2,3]").Como == HyprRescate.Estado.NoEsJson,
             "una lista no es una configuración");
        Vale(HyprRescate.Juzgar("{\"Version\": \"cuatro\"}").Como == HyprRescate.Estado.SinVersion,
             "una versión que no es número");
        Vale(HyprRescate.Juzgar("{\"Version\": 0}").Como == HyprRescate.Estado.SinVersion,
             "y una versión que no tiene sentido");

        var bien = HyprRescate.Juzgar("{\"Version\": 4, \"Tema\": {}}");
        Vale(bien.Sirve, "un config entero sirve");
        Igual(4, bien.Version, "y se lee su versión");

        // Un config sin `Version` es legítimo: las migraciones lo tratan como v1.
        var sinVersion = HyprRescate.Juzgar("{\"Tema\": {}}");
        Vale(sinVersion.Sirve, "sin Version también sirve");
        Igual(1, sinVersion.Version, "y se trata como v1, que es lo que hacen las migraciones");

        // ---------- El orden de preferencia ----------
        //
        // Es una decisión, no un efecto. El `.tmp` primero porque es LO MÁS NUEVO que el
        // usuario quiso guardar: si existe, la escritura se cortó justo después de dejarlo
        // listo y antes de sustituir.
        File.WriteAllText(cfg, "{\"Version\": 4, roto");
        File.WriteAllText(cfg + ".bak", "{\"Version\": 4, \"Barra\": {\"Alto\": 40}}");
        File.WriteAllText(cfg + ".tmp", "{\"Version\": 4, \"Barra\": {\"Alto\": 44}}");

        var mejor = HyprRescate.Mejor(cfg);
        Vale(mejor != null, "hay algo que rescatar");
        Vale(mejor!.Ruta.EndsWith(".tmp"), "y lo primero es la escritura que se quedó a medias");

        // Si el .tmp también está roto, se baja al .bak. Un candidato que no pasa el juicio
        // se salta entero: no se usa a medias.
        File.WriteAllText(cfg + ".tmp", "{{{");
        Vale(HyprRescate.Mejor(cfg)!.Ruta.EndsWith(".bak"),
             "si el .tmp está roto, el siguiente es el config anterior");

        // Y si no queda ninguno, no se inventa nada.
        File.Delete(cfg + ".tmp");
        File.Delete(cfg + ".bak");
        Vale(HyprRescate.Mejor(cfg) == null, "sin copias no hay rescate");
        Vale(HyprRescate.Cargar(cfg) == null, "y cargar devuelve null, como antes de D7");

        // ---------- El informe lo cuenta todo ----------
        File.WriteAllText(cfg + ".bak", "{\"Version\": 3, \"Barra\": {\"Alto\": 40}}");
        var informe = HyprRescate.Informe(cfg);
        Vale(informe.Any(l => l.Contains("estado del config")), "dice qué le pasa al config");
        Vale(informe.Any(l => l.Contains("candidato")), "cuántos candidatos hay");
        Vale(informe.Any(l => l.Contains("el config anterior")), "y cuál se usaría");
        Vale(informe.Any(l => l.Contains("NO se borra")),
             "y que el fichero roto se queda: un rescate que borra puede perder la prueba del fallo");

        // ---------- Cargar: devuelve el árbol y de dónde salió ----------
        var r = HyprRescate.Cargar(cfg);
        Vale(r != null, "se rescata");
        Igual(3, r!.Version, "con su versión original");
        Igual(40, r.Arbol["Barra"]!["Alto"]!.GetValue<int>(), "y el contenido del usuario entero");

        // Y un config sano no se toca: el rescate no se mete donde no hace falta.
        File.WriteAllText(cfg, "{\"Version\": 4, \"Barra\": {\"Alto\": 38}}");
        var sano = HyprRescate.Cargar(cfg);
        Igual("el config", sano!.DeDonde, "si el config se lee, se usa el config");
        Vale(HyprRescate.Informe(cfg).Any(l => l.Contains("nada que rescatar")),
             "y el informe lo dice");

        Directory.Delete(dir, true);
    }

    static void D7ElRescateConservaLoDesconocido()
    {
        Grupo("D7: rescatar no puede ser una excusa para perder claves");

        var dir = CarpetaDePrueba("desconocido");
        var cfg = Path.Combine(dir, "config.json");

        File.WriteAllText(cfg, "{ roto");
        File.WriteAllText(cfg + ".bak", """
        {
          "Version": 3,
          "Barra": { "Alto": 42 },
          "UnaSeccionDelFuturo": { "x": 1, "y": [2, 3] },
          "NubeHypr": { "Activo": false, "ClaveQueNoConozco": "ahí sigue" }
        }
        """);

        var r = HyprRescate.Cargar(cfg);
        Vale(r != null, "se rescata del .bak");

        // Lo rescatado es el ÁRBOL, no el modelo tipado. Es lo que hace que las claves
        // desconocidas sobrevivan: el modelo solo ve lo que conoce.
        Vale(r!.Arbol["UnaSeccionDelFuturo"] is JsonObject,
             "una sección entera desconocida sigue en el árbol rescatado");
        Igual("ahí sigue",
              r.Arbol["NubeHypr"]!["ClaveQueNoConozco"]!.GetValue<string>(),
              "y una clave desconocida dentro de una sección conocida, también");

        // Y migrarlo en memoria tampoco las pierde.
        var resultado = Esquema.Migrar(r.Arbol);
        Vale(resultado.Vale, $"la migración del rescate va bien: {resultado.Resumen}");
        Igual("ahí sigue",
              r.Arbol["NubeHypr"]!["ClaveQueNoConozco"]!.GetValue<string>(),
              "después de migrar, la clave desconocida sigue ahí");
        Vale(r.Arbol["UnaSeccionDelFuturo"] is JsonObject, "y la sección desconocida también");
        Igual(42, r.Arbol["Barra"]!["Alto"]!.GetValue<int>(), "y lo que el usuario tenía puesto");

        // Y la capa sigue apagada después de un rescate: recuperarse de un fallo no puede
        // encender nada.
        Igual(false, r.Arbol["NubeHypr"]!["Activo"]!.GetValue<bool>(),
              "NubeHypr sigue apagado después del rescate");

        Directory.Delete(dir, true);
    }

    // ================= Resistencia =================

    static void D7QuinientasVentanas()
    {
        Grupo("D7: quinientas ventanas por el motor, sin reconstruir nada");

        // El documento pide "abrir y cerrar 100 ventanas en una sesion de prueba". Eso
        // necesita ventanas de verdad y queda pendiente a mano; lo que SÍ se puede hacer aquí
        // es pasar quinientas por el modelo y medir que no se va de las manos.
        const int cuantas = 500;
        var reglas = new List<HyprReglas.Regla>
        {
            R("nav", prio: 5, proc: "firefox", espacio: 2),
            R("ed", prio: 3, proc: "code", layout: NubeHypr.Layout.Principal),
            R("flota", proc: "notepad", flotante: true),
            R("por-titulo", titulo: "Documento", foco: true),
            R("por-ruta", ruta: "Windows", monitor: 1),
        };

        var ventanas = new List<VentanaModelo>(cuantas);
        for (int i = 0; i < cuantas; i++)
            ventanas.Add(new VentanaModelo(
                (IntPtr)(1000 + i),
                i % 3 == 0 ? "firefox" : i % 3 == 1 ? "code" : "notepad",
                i % 7 == 0 ? "Documento " + i : "ventana " + i,
                new NubeHypr.Rect(i % 20 * 60, i % 10 * 80, 600, 400)));

        // ---------- 1. Resolver las reglas de las 500 ----------
        HyprVigilante.Olvidar();
        var reloj = Stopwatch.StartNew();
        var apuntes = HyprVigilante.AlCambiarLaLista(
            NubeHypr.Modo.Simulacion, ventanas, reglas,
            _ => "Clase", h => h.ToInt64() % 2 == 0 ? @"C:\Windows\x.exe" : @"D:\x.exe");
        reloj.Stop();

        Igual(cuantas, apuntes.Count, "se resuelven las 500");
        Vale(reloj.ElapsedMilliseconds < 3000,
             $"y en un tiempo razonable ({reloj.ElapsedMilliseconds} ms para 500 ventanas × 5 reglas)");

        // La segunda vez no se resuelve ninguna: es lo que evita el bucle con CPU alta.
        reloj.Restart();
        var otraVez = HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Simulacion, ventanas, reglas);
        reloj.Stop();
        Igual(0, otraVez.Count, "la segunda pasada no resuelve ninguna");
        Vale(reloj.ElapsedMilliseconds < 500,
             $"y es barata ({reloj.ElapsedMilliseconds} ms): no se recalcula nada");

        // ---------- 2. Repartir 500 ventanas en un layout ----------
        var area = new NubeHypr.Rect(0, 0, 3840, 2160);
        reloj.Restart();
        var tramos = NubeHypr.Tramos(area.X, area.Ancho, cuantas, 8);
        reloj.Stop();
        Vale(reloj.ElapsedMilliseconds < 200,
             $"repartir 500 tramos es inmediato ({reloj.ElapsedMilliseconds} ms)");

        // Y el reparto sigue cumpliendo lo suyo aunque sean absurdas: ni se solapan ni se
        // salen. Con 500 ventanas en 3840 px cada una mide menos que el mínimo, y lo que NO
        // puede pasar es que el reparto devuelva basura.
        Igual(cuantas, tramos.Count, "un tramo por ventana");
        for (int i = 1; i < tramos.Count; i++)
            Vale(tramos[i].Inicio >= tramos[i - 1].Inicio + tramos[i - 1].Tamano - 0.001,
                 i == 1 ? "los tramos no se solapan ni con 500" : "");

        // ---------- 3. Las memorias de D7 con 500 ----------
        var memoria = new HyprRegreso.Memoria();
        reloj.Restart();
        foreach (var v in ventanas)
            memoria.Apuntar(HyprRegreso.Huella(v.Proceso + ".exe", "Clase" + v.Hwnd),
                            v.Rectangulo, false);
        reloj.Stop();
        Vale(memoria.Cuantas <= HyprRegreso.MaxSitios,
             $"la memoria de regresos no crece con 500 ventanas ({memoria.Cuantas})");
        Vale(reloj.ElapsedMilliseconds < 500,
             $"y apuntarlas es barato ({reloj.ElapsedMilliseconds} ms)");

        // ---------- 4. Y el supervisor no se altera por el volumen ----------
        //
        // 500 ventanas no son un fallo. Si el supervisor confundiera "muchas" con "mal",
        // apagaría la capa justo cuando más se necesita.
        Igual(HyprSalud.Accion.Nada,
              HyprSalud.Mirar(Señal(segundos: 1, fallos: 0, excepciones: 0)).Que,
              "500 ventanas no son un motivo para el modo seguro");

        HyprVigilante.Olvidar();
    }
}
