using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// La confirmación antes de mover ventanas de verdad.
//
// Esto se prueba sin abrir una ventana porque todas las decisiones están fuera de la interfaz
// (`HyprConfirmar`), que era el objetivo al escribirlo así: lo que decide si NubeHypr toca las
// ventanas de alguien tiene que poder exigirse en una prueba, no comprobarse a ojo.
//
// Lo que estas pruebas protegen, en una frase: **no contestar es no mover**.
public static partial class Pruebas
{
    internal static void CorrerConfirmar()
    {
        ConfirmarCuandoHaceFalta();
        ConfirmarLoQueCaduca();
        ConfirmarComoSeCuenta();
        ConfirmarLoQueSeLeDice();
        ConfirmarUnaCadaVez();
        LayoutQueEsUnRepartoYQueNo();
        RejillaElHuecoDeLaUltimaFila();
        BarraLosGruposVisuales();
        AnilloLaGeometria();
        EstiloLosTresPresets();
        BateriaCuantoQueda();
        MaquinaElNombreDelProcesador();
        ClimaLoQueSeDecideSinRed();
        GlifosQueExistenDeVerdad();
        BarraLosIdsSeVenDentroDeLosGrupos();
        NeonLoQueDebeCumplir();
        LateralesSeMuevenDeSitio();
    }

    // ================= Mover las secciones de los paneles =================
    //
    // Pedido así: «quiero que todo sea editable y movible; si quiero que el ethernet se mueva
    // y se ponga en el medio que pueda hacerlo, si quiero que el tiempo se ponga arriba que
    // pueda hacerlo». Los módulos de la barra se movían desde la fase A; las secciones de los
    // paneles eran la única parte donde el orden lo ponía el programa.
    //
    // Lo que se exige aquí son los tres casos que dejan a alguien sin panel si se hacen mal.
    static void LateralesSeMuevenDeSitio()
    {
        Grupo("laterales: las secciones se mueven, y nada se pierde por el camino");

        // ---------- Sin nada guardado, el reparto de siempre ----------
        //
        // El caso de TODOS los config que existen hoy. Si una lista vacía significara «este
        // panel no lleva nada», la primera actualización abriría los dos paneles en blanco.
        var izqSerie = Lateral.Repartidas(Lateral.Lado.Izquierda, null, null);
        Igual(string.Join(",", Lateral.IdsDe(Lateral.De(Lateral.Lado.Izquierda))),
              string.Join(",", Lateral.IdsDe(izqSerie)),
              "sin nada guardado, el panel izquierdo es el de serie");
        Vale(Lateral.Repartidas(Lateral.Lado.Derecha, null, null).Count > 0,
             "y el derecho tampoco sale vacío");

        // ---------- El ethernet al medio del otro panel ----------
        //
        // El ejemplo literal del usuario. «conexion» nace en el panel derecho; aquí se lleva
        // al izquierdo y se coloca en mitad de la lista.
        var izq = new List<string> { "perfil", "conexion", "rapidas", "audio" };
        var der = new List<string> { "medios", "avisos", "calendario" };

        var ahoraIzq = Lateral.Repartidas(Lateral.Lado.Izquierda, izq, der);
        Igual("conexion", ahoraIzq[1].Id, "«conexion» se ha movido al izquierdo, y al medio");
        Vale(Lateral.Repartidas(Lateral.Lado.Derecha, izq, der).All(s => s.Id != "conexion"),
             "y ya NO sale en el derecho: moverla es moverla, no duplicarla");

        // ---------- Nada se queda invisible ----------
        //
        // El fallo que se come a una sección: con un orden guardado, las que no estén en
        // ninguna de las dos listas tienen que volver a su panel de origen, al final. Si no,
        // una sección nueva de una actualización no la vería nunca quien ya hubiera movido
        // algo — y no habría forma de recuperarla salvo borrando el config.
        var todas = Lateral.Repartidas(Lateral.Lado.Izquierda, izq, der)
                    .Concat(Lateral.Repartidas(Lateral.Lado.Derecha, izq, der))
                    .Select(s => s.Id).ToList();
        foreach (var s in Lateral.Secciones)
            Vale(todas.Contains(s.Id), $"«{s.Id}» sigue estando en algún panel");

        Igual(todas.Count, todas.Distinct().Count(),
              "y ninguna sale en los dos paneles a la vez");

        // ---------- Un id inventado no tumba el panel ----------
        var conBasura = Lateral.Repartidas(Lateral.Lado.Izquierda,
                                           new List<string> { "no-existe", "perfil" }, der);
        Vale(conBasura.All(s => s.Id != "no-existe"), "un id desconocido se ignora");
        Vale(conBasura.Any(s => s.Id == "perfil"), "y lo que sí existe se respeta");

        // ---------- Vaciar un panel del todo es legal ----------
        //
        // Si alguien se lleva TODO al izquierdo, el derecho tiene que quedarse vacío de
        // verdad y no repoblarse con su reparto de fábrica.
        var todasIzq = Lateral.Secciones.Select(s => s.Id).ToList();
        Igual(0, Lateral.Repartidas(Lateral.Lado.Derecha, todasIzq, new List<string>()).Count,
              "con todo movido al izquierdo, el derecho se queda vacío");
        Igual(Lateral.Secciones.Length,
              Lateral.Repartidas(Lateral.Lado.Izquierda, todasIzq, new List<string>()).Count,
              "y el izquierdo las lleva todas");
    }

    // ================= El neón =================
    //
    // Dos versiones devueltas por el usuario, las dos con el mismo diagnóstico de fondo: lo
    // que se veía era un **filo de color repetido en todas las tarjetas**, no una luz. Sus
    // palabras la segunda vez: «un borde azul fijo alrededor de todas; parece un contorno
    // seleccionado repetido en todos los elementos».
    //
    // Estas pruebas fijan lo que no puede volver a pasar. Ninguna abre una ventana: todo lo
    // que se exige aquí es una decisión, y por eso vive en `Neon` y en `Realces` en vez de
    // dentro del código que crea el efecto.
    static void NeonLoQueDebeCumplir()
    {
        Grupo("neón: luz que respira, no un contorno de selección");

        // ---------- Lo que en reposo NO puede parecer ----------
        //
        // La regla que se rompió. Una tarjeta normal tiene que distinguirse claramente de una
        // activa; si no, el color de «esto está encendido» deja de querer decir nada.
        Vale(Realces.SeDistingue(Realce.Reposo, Realce.Activo),
             "una tarjeta en reposo NO puede parecer seleccionada");
        Vale(Realces.De(Realce.Reposo).Intensidad < Realces.De(Realce.Activo).Intensidad,
             "lo activo brilla más que lo que está quieto");
        Vale(Realces.De(Realce.Principal).Intensidad > Realces.De(Realce.Activo).Intensidad,
             "y la acción importante, más todavía");

        // El ambiente es lo contrario: MUY ancho y MUY flojo. Es luz de fondo, no un borde.
        Vale(Realces.De(Realce.Ambiente).Difuminado > Realces.De(Realce.Activo).Difuminado,
             "el halo de ambiente es el más ancho");
        Vale(Realces.De(Realce.Ambiente).Intensidad < Realces.De(Realce.Activo).Intensidad,
             "y a la vez el más flojo: si fuera fuerte sería un marco");

        // ---------- El brillo nunca se apaga ----------
        //
        // Pedido con estas palabras: «el brillo nunca desaparece completamente». Un efecto
        // que llega a cero y vuelve parpadea, y un parpadeo en algo que está en pantalla
        // todo el día es insoportable a los diez minutos.
        Vale(Neon.SueloDeLaRespiracion > 0, "el suelo de la respiración no es cero");
        Vale(Neon.SueloDeLaRespiracion < 1, "pero sí baja: si no, no respiraría");

        // ---------- Los topes ----------
        //
        // Un ajuste decora; no puede dejar la interfaz ilegible ni vaciar el estilo.
        var bestia = Neon.Limpia(new OpcionesNeon { Intensidad = 99, Radio = 999, Velocidad = 1 });
        Vale(bestia.Intensidad <= Neon.IntensidadMax, "una intensidad absurda se topa");
        Vale(bestia.Radio <= Neon.RadioMax, "y un radio absurdo también");
        Vale(bestia.Velocidad >= Neon.VelocidadMin, "y una velocidad imposible se frena");

        var nada = Neon.Limpia(new OpcionesNeon { Intensidad = 0, Radio = 0 });
        Vale(nada.Intensidad >= Neon.IntensidadMin,
             "y por abajo tampoco se apaga: para no tener neón se elige otro estilo");

        // NaN no es un número pequeño: `Clamp` lo deja pasar y revienta el render más tarde,
        // lejos de aquí y sin pista de por qué.
        var roto = Neon.Limpia(new OpcionesNeon { Intensidad = double.NaN, Radio = double.PositiveInfinity });
        Vale(double.IsFinite(roto.Intensidad) && double.IsFinite(roto.Radio),
             "un NaN o un infinito en el fichero no llegan al render");

        // Un config viejo, sin la sección, da exactamente lo de serie.
        var viejo = Neon.Limpia(null);
        Igual(Neon.Limpia(new OpcionesNeon()), viejo, "sin la sección en el fichero, lo de serie");

        // ---------- Los colores ----------
        var acento = System.Windows.Media.Color.FromRgb(0x7A, 0xA2, 0xF7);
        var (f1, l1) = Neon.Colores(Neon.Limpia(new OpcionesNeon()), acento);
        Igual(acento, f1, "sin color puesto, el filo sale del acento del tema");
        Igual(acento, l1, "y la luz también: una sola fuente de verdad");

        var (f2, l2) = Neon.Colores(Neon.Limpia(new OpcionesNeon { Color = "#FF0000,#00FF00" }), acento);
        Vale(f2 != l2, "con dos colores, el del filo y el de la luz son distintos (degradado)");

        var (f3, l3) = Neon.Colores(Neon.Limpia(new OpcionesNeon { Color = "#FF0000" }), acento);
        Igual(f3, l3, "con uno solo, los dos van de ese");

        var (f4, _) = Neon.Colores(Neon.Limpia(new OpcionesNeon { Color = "no soy un color" }), acento);
        Igual(acento, f4, "y un color mal escrito no tumba nada: se usa el del tema");

        // ---------- Los presets ----------
        //
        // Para no obligar a entender «radio del difuminado» antes de tener el escritorio
        // bonito. Y el que está puesto se puede nombrar, que es lo que permite enseñarlo en
        // Ajustes sin que el usuario deduzca su estilo de cuatro números.
        Vale(Neon.Presets.Length >= 3, "hay presets de sobra para no tener que afinar a mano");
        Igual(Neon.Presets.Length, Neon.Presets.Select(p => p.Nombre).Distinct().Count(),
              "sin dos presets con el mismo nombre");
        foreach (var (nombre, como) in Neon.Presets)
            Igual(nombre, Neon.NombreDe(como), $"«{nombre}» se reconoce a sí mismo");

        Igual("Personalizado", Neon.NombreDe(new Neon.Ajuste(1.33, 19, 2345, true, "")),
              "y lo que no es ninguno se llama Personalizado");

        // El preset «Fijo» es el que contesta a la accesibilidad: halo visible, sin moverse.
        var fijo = Neon.Preset("Fijo");
        Vale(fijo is { Respira: false }, "«Fijo» no respira");
        Vale(fijo!.Intensidad > 0, "pero sigue brillando: quieto no es apagado");

        Vale(Neon.Preset("no existe") == null, "un preset que no está no se inventa");
    }

    // ================= Un módulo agrupado SÍ está montado =================
    //
    // `IdsMontadosEn` es con lo que `--diagmodulos` contesta «lo añado, ¿sale?». Miraba solo
    // los hijos directos de la zona, y desde que existen los grupos visuales un módulo
    // agrupado cuelga de la caja de su grupo. O sea que para cpu, memoria, batería, bandeja,
    // control y notificaciones —los que más se agrupan— el diagnóstico decía «no sale» de
    // cosas que se estaban viendo en la barra.
    //
    // No saltó nunca porque los módulos que el diagnóstico probaba quedaban sueltos. Esto es
    // la prueba que lo habría cazado: se monta a mano el árbol que hace la barra —zona,
    // borde del grupo, caja, módulos dentro— y se exige que los vea todos y en orden.
    static void BarraLosIdsSeVenDentroDeLosGrupos()
    {
        Grupo("barra: los módulos de dentro de un grupo también están montados");

        static System.Windows.FrameworkElement Con(string id)
        {
            var e = new System.Windows.Controls.Border();
            System.Windows.Automation.AutomationProperties.SetAutomationId(e, id);
            return e;
        }

        // El árbol tal como lo monta la zona: un módulo suelto, y un grupo con tres dentro
        // envuelto en su borde.
        var zona = new System.Windows.Controls.StackPanel();
        zona.Children.Add(Con("reloj"));

        var caja = new System.Windows.Controls.StackPanel();
        caja.Children.Add(Con("cpu"));
        caja.Children.Add(Con("ram"));
        caja.Children.Add(Con("bandeja"));
        zona.Children.Add(new System.Windows.Controls.Border { Child = caja });

        zona.Children.Add(Con("fecha"));

        var vistos = MainWindow.IdsMontadosEn(zona);
        Igual("reloj,cpu,ram,bandeja,fecha", string.Join(",", vistos),
              "se ven los cinco, y en el orden en que se leen");

        // Lo que de verdad protege esta prueba, dicho aparte para que el fallo se lea solo:
        Vale(vistos.Contains("cpu"),
             "«cpu» dentro de un grupo cuenta como montado (antes decía que no estaba)");

        // Y no se inventa ids bajando dentro de un módulo: sus hijos son sus piezas.
        var conPiezas = new System.Windows.Controls.StackPanel();
        var modulo = new System.Windows.Controls.Border();
        System.Windows.Automation.AutomationProperties.SetAutomationId(modulo, "control");
        var piezas = new System.Windows.Controls.StackPanel();
        piezas.Children.Add(Con("no-soy-un-modulo"));
        modulo.Child = piezas;
        conPiezas.Children.Add(modulo);

        Igual("control", string.Join(",", MainWindow.IdsMontadosEn(conPiezas)),
              "un bloque con id no se sigue hacia dentro");
    }

    // ================= Los glifos existen =================
    //
    // `Lateral.Glifos.Todos` se escribió con un comentario que decía, literal: «aquí y no en
    // el constructor de cada sección para poder comprobarlos todos de una vez». Y no lo
    // llamaba nadie: un `grep` de `Glifos.Todos` por todo el repositorio no daba ni un uso.
    // La lista existía, el motivo estaba escrito, y la comprobación no se hizo nunca.
    //
    // Es el mismo fallo que esta sesión lleva encontrando una y otra vez, y que está apuntado
    // en PROGRESO.md: lo que falla no es el código mal escrito, es el bien escrito que nadie
    // llama. Esto es la llamada.
    //
    // Y no es teórico: un código que no está en la fuente sale como un rectángulo vacío, y eso
    // no lo nota nadie leyendo el código — solo quien abre el panel. Ya pasó esta misma sesión
    // con la flecha de las cabeceras, que salió como una barra vertical.
    static void GlifosQueExistenDeVerdad()
    {
        Grupo("glifos: todos están en la fuente de iconos");

        // La fuente de verdad, la misma que usa `Glifo()`. Si no se puede abrir ninguna de las
        // dos no se inventa un veredicto: se dice que no se pudo comprobar y se sale. Fallar
        // aquí en una máquina sin la fuente sería una alarma falsa, y una alarma falsa que
        // salta siempre se acaba ignorando.
        System.Windows.Media.GlyphTypeface? gt = null;
        foreach (var nombre in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            try
            {
                var tf = new System.Windows.Media.Typeface(nombre);
                if (tf.TryGetGlyphTypeface(out var g) &&
                    g.CharacterToGlyphMap.ContainsKey(0xE753))   // la nube, que seguro está
                { gt = g; break; }
            }
            catch { }
        }

        if (gt == null) { Vale(true, "(sin fuente de iconos en esta máquina: no se comprueba)"); return; }

        foreach (var cod in Lateral.Glifos.Todos)
            Vale(gt.CharacterToGlyphMap.ContainsKey(cod),
                 $"U+{cod:X4} existe en la fuente (si no, sale un cuadradito)");

        // Los del clima no están en `Glifos.Todos` —los elige `Clima.Tiempo` según el código de
        // la OMM, no la sección—, así que se comprueban por su puerta: pasando por la función
        // que de verdad los devuelve. Un glifo de lluvia roto solo se vería el día que llueva,
        // que es la peor forma de descubrirlo.
        foreach (var cod in new[] { 0, 1, 2, 3, 45, 48, 55, 63, 75, 81, 86, 97, 1234 })
        {
            var (_, glifo) = Clima.Tiempo(cod);
            Vale(gt.CharacterToGlyphMap.ContainsKey(glifo),
                 $"el glifo del código {cod} de la OMM existe en la fuente");
        }
    }

    // ================= El clima, sin tocar la red =================
    //
    // Traducir un código de la OMM, decidir si toca volver a preguntar y escribir una
    // temperatura son decisiones, y por eso están en `Clima` y no dentro de la llamada HTTP.
    // Ninguna de estas pruebas abre un socket.
    static void ClimaLoQueSeDecideSinRed()
    {
        Grupo("clima: lo que se decide antes de salir a internet");

        // ---------- Los códigos ----------
        Igual("Despejado", Clima.Tiempo(0).Que, "el 0 de la OMM es cielo despejado");
        Igual("Nubes dispersas", Clima.Tiempo(2).Que, "el 2, nubes dispersas");
        Igual("Lluvia", Clima.Tiempo(63).Que, "el 63 cae en el rango de lluvia");
        Igual("Nieve", Clima.Tiempo(73).Que, "el 73, en el de nieve");
        Igual("Tormenta", Clima.Tiempo(95).Que, "el 95, tormenta");

        // Lo importante de verdad: un código que no esté en la tabla NO puede caer en
        // «Despejado». Los códigos de la OMM tienen huecos sin asignar, y enseñar un sol
        // mientras fuera cae granizo es peor que no enseñar nada.
        Igual("Sin datos", Clima.Tiempo(1234).Que, "un código desconocido no se inventa");
        Igual("Sin datos", Clima.Tiempo(-1).Que, "ni uno imposible");
        foreach (var raro in new[] { 4, 20, 44, 50, 60, 70, 79, 90, 100 })
            Vale(Clima.Tiempo(raro).Que != "Despejado",
                 $"el código {raro}, sin asignar, no acaba diciendo «Despejado»");

        // ---------- La temperatura ----------
        Igual("18°", Clima.Grados(17.8), "17,8 se dice «18°»: el decimal no informa");
        Igual("0°", Clima.Grados(0.2), "y cerca de cero no se queda en blanco");
        Igual("-3°", Clima.Grados(-2.6), "bajo cero, con su signo");

        // ---------- El freno ----------
        //
        // Sin esto, abrir y cerrar el panel diez veces serían diez peticiones a un servicio
        // gratuito. Y el panel derecho se reconstruye solo con pulsar «siguiente canción».
        var ahora = new DateTime(2026, 10, 27, 12, 0, 0);
        Vale(Clima.Caducada(null, ahora), "sin lectura ninguna, hay que pedir");

        var recien = new Clima.Lectura(18, "Nublado", 0xE753, "Madrid", ahora);
        Vale(!Clima.Caducada(recien, ahora.AddMinutes(1)),
             "un minuto después NO se vuelve a pedir: el tiempo no cambia en un minuto");
        Vale(!Clima.Caducada(recien, ahora.AddMinutes(14)), "ni a los catorce");
        Vale(Clima.Caducada(recien, ahora.AddMinutes(15)), "a los quince, sí");
        Vale(Clima.Caducada(recien, ahora.AddHours(3)), "y desde luego a las tres horas");

        // ---------- La ciudad, antes de mandar nada ----------
        //
        // Este es el único sitio donde se decide si sale algo de la máquina del usuario. Una
        // ciudad vacía no se manda: además de ser una petición segura de fallar, el caso en
        // que no debe salir nada es justamente «el usuario no ha dicho dónde está».
        Vale(!Clima.CiudadValida(null), "sin ciudad no se pide nada");
        Vale(!Clima.CiudadValida(""), "ni con la cadena vacía");
        Vale(!Clima.CiudadValida("   "), "ni con espacios, que es lo que deja un campo tocado y borrado");
        Vale(!Clima.CiudadValida("M"), "una sola letra no es una ciudad");
        Vale(Clima.CiudadValida("Madrid"), "«Madrid» sí");
        Vale(Clima.CiudadValida(" Vigo "), "y los espacios de alrededor no estorban");

        // ---------- De serie, apagado ----------
        //
        // Es la única parte de NubeBar que saldría a un servidor ajeno por su cuenta. Que nazca
        // apagada no es una preferencia: es la diferencia entre pedirlo y asumirlo.
        var limpio = new OpcionesClima();
        Vale(!limpio.Activo, "el clima nace APAGADO en una instalación nueva");
        Igual("", limpio.Ciudad, "y sin ciudad puesta");
        Vale(limpio.Latitud == 0 && limpio.Longitud == 0, "y sin coordenadas resueltas");

        // Y con el interruptor encendido pero sin ciudad tampoco sale nada: son dos
        // condiciones, no una.
        Vale(!Clima.CiudadValida(new OpcionesClima { Activo = true }.Ciudad),
             "encenderlo sin escribir ciudad no manda ninguna petición");
    }

    // ================= El nombre del procesador =================
    //
    // El registro lo guarda con coletillas que no informan y con espacios de relleno. En una
    // tarjeta estrecha, «AMD Ryzen 7 7800X3D 8-Core Processor» ocupa dos líneas para decir lo
    // mismo que «AMD Ryzen 7 7800X3D».
    static void MaquinaElNombreDelProcesador()
    {
        Grupo("máquina: el procesador, como lo diría una persona");

        Igual("AMD Ryzen 7 7800X3D",
              Maquina.QuitarLaColetilla("AMD Ryzen 7 7800X3D 8-Core Processor           "),
              "se quita «8-Core Processor» y los espacios de relleno del registro");

        Igual("Intel(R) Core(TM) i7-12700H",
              Maquina.QuitarLaColetilla("Intel(R) Core(TM) i7-12700H @ 2.30GHz"),
              "y lo que va tras la arroba, que ya lo dice el modelo");

        Igual("AMD Ryzen 5 5600X",
              Maquina.QuitarLaColetilla("AMD Ryzen 5 5600X 6-Core Processor"),
              "también con seis núcleos");

        // Lo que no tiene coletilla se queda igual: quitar a ciegas el último trozo dejaría
        // nombres mutilados.
        Igual("Apple M2", Maquina.QuitarLaColetilla("Apple M2"), "y lo que ya está limpio no se toca");

        // Y lo que no se puede leer no se inventa.
        Igual("", Maquina.QuitarLaColetilla(""), "sin dato, cadena vacía");
        Igual("", Maquina.QuitarLaColetilla("   "), "y con espacios, también");

        // ---------- Lo que de verdad hay en esta máquina ----------
        //
        // No se comprueba el VALOR —cambia de equipo en equipo— sino que se pueda leer y que
        // no venga con basura. Una tarjeta que enseña espacios de relleno parece rota.
        var cpu = Maquina.Procesador();
        Vale(cpu == cpu.Trim(), "el procesador leído no trae espacios sobrantes");
        var so = Maquina.Sistema();
        Vale(so == so.Trim(), "ni el sistema");
    }

    // ================= Cuánto queda de batería =================
    //
    // Windows lo da en segundos. Hay que escribirlo como lo diría una persona.
    static void BateriaCuantoQueda()
    {
        Grupo("batería: «2h 14m», no «8040 s»");

        Igual("2h 14m", Lateral.CuantoQueda(2 * 3600 + 14 * 60), "horas y minutos");
        Igual("1h 0m", Lateral.CuantoQueda(3600), "una hora justa");

        // Menos de una hora: los minutos solos. «0h 12m» hace leer dos veces para enterarse
        // de que quedan doce minutos, y es justo cuando el dato más importa.
        Igual("12m", Lateral.CuantoQueda(12 * 60), "menos de una hora, solo minutos");
        Igual("1m", Lateral.CuantoQueda(90), "y se redondea hacia abajo: 90 s es 1m, no 2");

        // ---------- Lo que no se puede escribir ----------
        //
        // Windows devuelve -1 cuando todavía no lo sabe —recién desenchufado— o cuando está
        // enchufado. Devolver «0m» ahí sería decir que la batería se acaba ya.
        Igual("", Lateral.CuantoQueda(-1), "si Windows no lo sabe, no se dice nada");
        Igual("", Lateral.CuantoQueda(0), "ni con cero");
        Igual("", Lateral.CuantoQueda(-99999), "ni con una basura");

        // Y una batería enorme no se escribe en días: «51h 0m» es raro pero es verdad, y
        // convertirlo a «2d 3h» obliga a hacer la cuenta al revés para saber si llega a la
        // tarde.
        Igual("51h 0m", Lateral.CuantoQueda(51 * 3600), "y una muy larga sigue en horas");
    }

    // ================= Los estilos visuales =================
    //
    // Lo que el usuario puede elegir: la FORMA de las superficies. Lo que ningún estilo puede
    // hacer: quitar función. Si elegir una decoración te costara un control, no sería una
    // decoración.
    static void EstiloLosTresPresets()
    {
        Grupo("estilo: tres decoraciones, ninguna quita nada");

        // ---------- «Normal» es exactamente lo que había ----------
        //
        // Si no coincide al píxel, el ajuste nuevo no es un ajuste: es un cambio de aspecto
        // con un interruptor encima, y quien no pidió nada se lo encuentra puesto.
        var n = Estilos.Normal;
        Igual(12.0, n.RadioCard, "«Normal» conserva el radio de tarjeta que había");
        Igual(14.0, n.RadioShell, "y el de la concha");
        Igual(10.0, n.Hueco, "y el hueco entre tarjetas");
        Igual(13.0, n.Dentro, "y el relleno de dentro");
        Vale(!n.Brilla, "y no brilla");
        Vale(!n.Late, "ni late");
        Vale(!n.TintaDeAcento, "ni le toca las cabeceras");

        // ---------- El neón tiene que ser NEÓN ----------
        //
        // La primera versión solo cambiaba el filo, y el usuario lo dijo al probarlo: «solo
        // lo aplicas a los bordes y eso no tiene que ser así, tiene que estar animado». Un
        // marco de color no es neón. Las dos mitades de arreglarlo se exigen aquí.
        var neon = Estilos.Neon;
        Vale(neon.Brilla, "«Neón» brilla");
        Vale(neon.Late, "y el resplandor respira, no se queda quieto");
        Vale(neon.ResplandorAlto > neon.Resplandor,
             "el latido crece desde el resplandor de reposo, no al revés");
        Vale(neon.TintaDeAcento,
             "y el color entra en las cabeceras: si no, el estilo se queda en el filo");

        // «Nube» NO hereda nada de esto. Es el fallo fácil al añadir campos a un record con
        // valores por defecto: tocar uno y que se cuele en los demás sin que nadie lo note.
        Vale(!Estilos.Nube.Late, "«Nube» no late");
        Vale(!Estilos.Nube.TintaDeAcento, "ni enciende las cabeceras: es el sobrio con aire");
        Vale(!Estilos.Nube.Brilla, "ni brilla");

        // ---------- Los tres son distintos de verdad ----------
        //
        // Un selector con tres opciones que se ven igual es peor que no tenerlo: hace dudar
        // de si ha funcionado.
        Igual(3, Estilos.Todos.Length, "hay tres");
        Igual(3, Estilos.Todos.Select(e => e.Nombre).Distinct().Count(), "con nombres distintos");

        // Esto miraba solo `RadioCard`, y era un proxy: daba por hecho que «distinto» quiere
        // decir «redondea distinto». Dejó de valer cuando el neón pasó a distinguirse por la
        // LUZ y no por el redondeo — con el filo de acento fuera, su radio ya no tiene por
        // qué ser raro solo para que una prueba lo note.
        //
        // Lo que de verdad hay que exigir es que el conjunto de lo que define el aspecto sea
        // distinto en los tres, que es lo que hace que se note cuál está puesto.
        static string Firma(Estilo e) =>
            $"{e.RadioCard}/{e.RadioShell}/{e.Hueco}/{e.Dentro}/{e.GrosorFilo}/" +
            $"{e.Resplandor}/{e.ResplandorAlto}/{e.TintaDeAcento}";

        Igual(3, Estilos.Todos.Select(Firma).Distinct().Count(),
              "los tres se ven distintos de verdad, no solo en el nombre");

        foreach (var e in Estilos.Todos)
        {
            Vale(e.Nombre.Length > 0, $"«{e.Que}» tiene nombre");
            Vale(e.Que_Hace.Length > 0, $"y dice qué hace, para poder elegir sin probar");

            // ---------- Ninguno puede dejar una superficie sin forma ----------
            //
            // Un radio o un relleno a cero no es «más sobrio», es una tarjeta rota. Y un filo
            // de grosor cero deja dos tarjetas pegadas sin línea entre ellas.
            Vale(e.RadioCard > 0 && e.RadioShell > 0, $"«{e.Nombre}»: las esquinas existen");
            Vale(e.Dentro >= 8, $"«{e.Nombre}»: el texto no se pega al filo");
            Vale(e.Hueco > 0, $"«{e.Nombre}»: las tarjetas no se tocan");
            Vale(e.GrosorFilo > 0, $"«{e.Nombre}»: el filo se ve");
        }

        // Solo el neón brilla: si brillaran dos, el selector tendría dos opciones parecidas y
        // una de más.
        Igual(1, Estilos.Todos.Count(e => e.Brilla), "solo uno brilla, y es el neón");
        Vale(Estilos.Neon.Brilla && Estilos.Neon.Late,
             "el neón brilla y respira — la luz viene del halo, no de un filo de color");

        // ---------- Ida y vuelta por el fichero ----------
        foreach (var e in Estilos.Todos)
            Igual(e.Que, Estilos.Leer(Estilos.Clave(e.Que)).Que,
                  $"«{e.Nombre}» se guarda y se vuelve a leer igual");

        // ---------- Y lo que no se entiende cae en «Normal» ----------
        //
        // Nunca en el más llamativo. Un fichero con una errata no puede dejar el escritorio
        // lleno de resplandores sin que nadie lo haya pedido: misma regla que
        // `NubeHypr.ModoEfectivo`, que convierte lo desconocido en simulación y nunca en
        // aplicar.
        Igual(EstiloVisual.Normal, Estilos.Leer("cualquier cosa").Que, "una basura es «Normal»");
        Igual(EstiloVisual.Normal, Estilos.Leer(null).Que, "y sin clave, también");
        Igual(EstiloVisual.Normal, Estilos.Leer("").Que, "y vacía");
        Igual(EstiloVisual.Neon, Estilos.Leer("  NEÓN ").Que,
              "pero «NEÓN» con tilde, mayúsculas y espacios sí se entiende: es lo que alguien escribiría");

        // Las claves del fichero, sin tildes ni mayúsculas: no son rótulos.
        foreach (var e in Estilos.Todos)
        {
            var k = Estilos.Clave(e.Que);
            Igual(k.ToLowerInvariant(), k, $"la clave de «{e.Nombre}» va en minúsculas");
            Vale(k.All(char.IsLetter), $"y sin acentos ni símbolos: «{k}»");
        }
    }

    // ================= El anillo de progreso =================
    //
    // Dibujar un arco en WPF lleva dos banderas que deciden si va por el lado corto o por el
    // largo. Equivocarse en ellas da un anillo que se ve bien al 30 % y se vuelve del revés
    // al 60 % — un fallo que solo aparece con la máquina cargada, que es cuando menos ganas
    // hay de mirarlo. Por eso la aritmética está aparte y se exige aquí.
    static void AnilloLaGeometria()
    {
        Grupo("anillo: el cero arriba, girando como las agujas del reloj");

        const double cx = 50, cy = 50, r = 20;

        // El cero, arriba. Si esto se desplaza, los tres anillos empiezan en sitios
        // distintos y el panel parece torcido.
        var p0 = Anillo.Punto(cx, cy, r, 0);
        Vale(Math.Abs(p0.X - cx) < 0.001, "en 0 el punto está justo encima del centro (x)");
        Vale(Math.Abs(p0.Y - (cy - r)) < 0.001, "y arriba del todo (y)");

        // Un cuarto: a la derecha. Es lo que fija el SENTIDO — si girara al revés, saldría
        // a la izquierda y el progreso se leería hacia atrás.
        var p25 = Anillo.Punto(cx, cy, r, 0.25);
        Vale(Math.Abs(p25.X - (cx + r)) < 0.001, "a un cuarto está a la derecha: gira como el reloj");
        Vale(Math.Abs(p25.Y - cy) < 0.001, "y a la altura del centro");

        // La mitad: abajo.
        var p50 = Anillo.Punto(cx, cy, r, 0.5);
        Vale(Math.Abs(p50.Y - (cy + r)) < 0.001, "a la mitad, abajo del todo");

        // ---------- La bandera del lado largo ----------
        Vale(!Anillo.PorElLadoLargo(0.25), "un cuarto va por el lado corto");
        Vale(!Anillo.PorElLadoLargo(0.5), "la mitad justa, también");
        Vale(Anillo.PorElLadoLargo(0.75), "tres cuartos van por el largo");
        Vale(Anillo.PorElLadoLargo(0.99), "y casi lleno, también");

        // ---------- Los dos casos que no son un arco ----------
        //
        // Con 1 el punto final coincide con el inicial, y un arco de 360° es ambiguo: WPF no
        // pinta nada. Sin esta comprobación, el disco lleno se vería VACÍO.
        Vale(Anillo.Vacio(0), "cero es vacío");
        Vale(Anillo.Lleno(1), "uno es lleno, y se dibuja como circunferencia, no como arco");
        Vale(!Anillo.Vacio(0.01) && !Anillo.Lleno(0.99), "y lo de en medio es un arco normal");

        // ---------- Lo que llega mal ----------
        //
        // Un contador de memoria que devuelve 101 por redondeo no puede dar más de una vuelta.
        Igual(1.0, Anillo.Parte(140), "un porcentaje imposible se queda en lleno");
        Igual(0.0, Anillo.Parte(-5), "y uno negativo, en vacío");
        Igual(0.5, Anillo.Parte(50), "y el 50 % es media vuelta");

        var fuera = Anillo.Punto(cx, cy, r, 3.7);
        Vale(Math.Abs(fuera.X - cx) < 0.001 && Math.Abs(fuera.Y - (cy - r)) < 0.001,
             "y una fracción fuera de rango no manda el punto a cualquier parte");
    }

    // ================= Los grupos visuales de la barra =================
    //
    // La barra ponía sus módulos uno detrás de otro sin nada entre ellos: siete iconos
    // pegados en la zona derecha donde no se ve dónde acaba uno y empieza otro.
    //
    // Se agrupa por CATEGORÍA y no por una lista escrita a mano, porque los módulos los
    // coloca el usuario: una lista fija deja de valer en cuanto mueve uno, y entonces el
    // agrupado enseña como relacionadas cosas que él separó a propósito.
    static void BarraLosGruposVisuales()
    {
        Grupo("barra: los módulos se agrupan por lo que son, no por una lista fija");

        static CategoriaWidget Cat(string id) => id switch
        {
            "reloj" or "fecha" => CategoriaWidget.Reloj,
            "apps" or "fijadas" or "escritorios" => CategoriaWidget.Apps,
            "cpu" or "ram" or "bateria" or "bandeja" or "control" => CategoriaWidget.Sistema,
            "separador" => CategoriaWidget.Util,
            _ => CategoriaWidget.Aspecto,
        };

        // Lo seguido de la misma categoría va junto.
        var t = GruposDeBarra.Partir(new[] { "cpu", "ram", "bateria", "fecha" }, Cat);
        Igual(2, t.Count, "tres de sistema y uno de reloj dan dos tramos");
        Igual("cpu,ram,bateria", string.Join(",", t[0].Ids), "los tres de sistema, juntos");
        Vale(t[0].ConPastilla, "y con pastilla, porque agrupan de verdad");

        // Uno solo NO lleva pastilla: un icono dentro de su propia cápsula es ruido, no
        // agrupación. La pastilla dice «estos van juntos», y eso no se dice de uno.
        Igual("fecha", string.Join(",", t[1].Ids), "la fecha va sola");
        Vale(!t[1].ConPastilla, "y sin pastilla: una cápsula para uno solo es ruido");

        // ---------- Lo que el usuario separa, se respeta ----------
        //
        // Si mete el reloj entre dos de sistema, son dos grupos. Juntarlos igualmente sería
        // deshacer lo que acaba de pedir al colocarlos así.
        var u = GruposDeBarra.Partir(new[] { "cpu", "reloj", "ram" }, Cat);
        Igual(3, u.Count, "el reloj en medio parte el grupo de sistema en dos");
        Vale(u.All(x => !x.ConPastilla), "y ninguno agrupa, porque ninguno tiene dos");

        // ---------- El separador ----------
        //
        // Es lo que uno pone para separar: meterlo dentro de una pastilla lo anularía.
        var v = GruposDeBarra.Partir(new[] { "cpu", "ram", "separador", "bandeja", "control" }, Cat);
        Igual(3, v.Count, "el separador parte, y va en su propio tramo");
        Igual("separador", string.Join(",", v[1].Ids), "él solo");
        Vale(!v[1].ConPastilla, "y nunca con pastilla");
        Vale(v[0].ConPastilla && v[2].ConPastilla, "los de los lados sí agrupan");

        // ---------- Los bordes ----------
        Igual(0, GruposDeBarra.Partir(new string[0], Cat).Count, "sin módulos no hay tramos");
        var w = GruposDeBarra.Partir(new[] { "reloj" }, Cat);
        Igual(1, w.Count, "con uno hay un tramo");
        Vale(!w[0].ConPastilla, "y sin pastilla");

        // Y la línea del registro, para poder leer el reparto sin abrir la barra.
        Vale(GruposDeBarra.Linea(v).Contains("[cpu,ram]"),
             $"la línea enseña los grupos: «{GruposDeBarra.Linea(v)}»");

        // ---------- La raya entre grupos ----------
        //
        // La maqueta la pide: «separadores verticales finos entre grupos». Lo que se exige
        // aquí es el caso que es fácil equivocar — que NO se ponga pegada al separador que
        // coloca el usuario, porque entonces salen dos rayas juntas y la suya parece un
        // fallo de dibujo.
        Vale(GruposDeBarra.RayaEntre(v[0], v[2]),
             "entre dos grupos de verdad va una raya");
        Vale(!GruposDeBarra.RayaEntre(v[0], v[1]),
             "pero NO antes del separador que pusiste tú: serían dos rayas seguidas");
        Vale(!GruposDeBarra.RayaEntre(v[1], v[2]), "ni después de él");

        // Un tramo de uno no deja de ser un grupo a efectos de la raya: lo que separa son
        // categorías, no tamaños. Entre el reloj y el bloque de sistema tiene que haber raya
        // aunque el reloj vaya solo — es justo la frontera que la maqueta quiere marcar.
        var r = GruposDeBarra.Partir(new[] { "reloj", "cpu", "ram" }, Cat);
        Igual(2, r.Count, "el reloj y el sistema son dos tramos");
        Vale(!r[0].ConPastilla, "el reloj va solo, sin pastilla");
        Vale(GruposDeBarra.RayaEntre(r[0], r[1]),
             "y aun así lleva raya con el grupo de al lado: la frontera es de categoría");
    }

    // ================= Cuándo la última fila deja un agujero =================
    static void RejillaElHuecoDeLaUltimaFila()
    {
        Grupo("rejilla: media fila vacía se ve, y menos de media no");

        // Exacto: no sobra nada.
        Igual(0, DiagRejilla.HuecoDeLaUltimaFila(12, 4), "12 en 4 columnas no deja hueco");
        Igual(0, DiagRejilla.HuecoDeLaUltimaFila(12, 3), "ni 12 en 3");
        Igual(0, DiagRejilla.HuecoDeLaUltimaFila(2, 2), "ni 2 en 2");

        // 7 en 3 columnas: última fila con 1, faltan 2. Dos tercios de fila vacía.
        Igual(2, DiagRejilla.HuecoDeLaUltimaFila(7, 3), "7 en 3 deja dos huecos");
        Vale(!DiagRejilla.LaUltimaFilaEstaBien(7, 3),
             "y eso es más de media fila: se ve el agujero");

        // 7 en 4: última fila con 3, falta 1. Eso es lo razonable.
        Igual(1, DiagRejilla.HuecoDeLaUltimaFila(7, 4), "7 en 4 deja uno");
        Vale(DiagRejilla.LaUltimaFilaEstaBien(7, 4), "y uno de cuatro no canta");

        // 5 es primo: ninguna cuadrícula lo llena, así que la elección importa.
        Igual(1, DiagRejilla.HuecoDeLaUltimaFila(5, 3), "5 en 3 deja uno");
        Vale(DiagRejilla.LaUltimaFilaEstaBien(5, 3), "que está bien");
        Igual(1, DiagRejilla.HuecoDeLaUltimaFila(5, 2), "5 en 2 deja uno");
        Vale(!DiagRejilla.LaUltimaFilaEstaBien(5, 2),
             "pero uno de DOS es media fila entera: eso ya no");

        // Y los bordes, que es donde una división entera se va a negativo o revienta.
        Igual(0, DiagRejilla.HuecoDeLaUltimaFila(0, 4), "cero ventanas no dejan hueco");
        Igual(0, DiagRejilla.HuecoDeLaUltimaFila(7, 0), "y sin columnas no se divide");
        Vale(!DiagRejilla.LaUltimaFilaEstaBien(7, 0), "con cero columnas nada está bien");
    }

    // ================= D9.6: qué es un reparto y qué es un amontonamiento =================
    //
    // El arnés de layout mide dónde acaban tres ventanas de verdad, pero «¿esto es un
    // reparto?» no se contesta mirando: se contesta con geometría. Y el fallo que esto
    // protege es el que un «se movieron 3 ventanas» no ve — **tres ventanas pueden moverse
    // las tres al mismo sitio** y el contador diría lo mismo.
    static void LayoutQueEsUnRepartoYQueNo()
    {
        Grupo("D9.6: un reparto se demuestra, no se cuenta");

        var area = new NubeHypr.Rect(0, 0, 1200, 600);
        const int tol = 16;

        // ---------- El bueno ----------
        var reparto = new[]
        {
            new NubeHypr.Rect(0, 0, 400, 600),
            new NubeHypr.Rect(400, 0, 400, 600),
            new NubeHypr.Rect(800, 0, 400, 600),
        };
        var v = ArnesLayout.MiraElReparto(reparto, area, tol);
        Vale(v.Bien, $"tres columnas que cubren la pantalla son un reparto ({v.Porque})");

        // Pegadas comparten una línea de píxeles cuando el hueco es 0. Llamar a eso
        // «solaparse» daría rojo a un reparto perfecto, que es el falso positivo más fácil
        // de escribir aquí.
        Vale(ArnesLayout.MiraElReparto(reparto, area, tol).Bien,
             "y estar pegadas no es pisarse");

        // ---------- El que un contador no distingue ----------
        var amontonadas = new[]
        {
            new NubeHypr.Rect(0, 0, 1200, 600),
            new NubeHypr.Rect(0, 0, 1200, 600),
            new NubeHypr.Rect(0, 0, 1200, 600),
        };
        var a = ArnesLayout.MiraElReparto(amontonadas, area, tol);
        Vale(!a.Bien, "tres ventanas en el MISMO sitio no son un reparto, aunque se movieran las tres");
        Vale(a.Porque.Contains("pisan"), $"y se dice por qué: «{a.Porque}»");

        // ---------- Fuera de la pantalla ----------
        //
        // El fallo más caro: una ventana repartida fuera no se ve y no se recupera sin
        // teclado.
        var fuera = new[]
        {
            new NubeHypr.Rect(0, 0, 600, 600),
            new NubeHypr.Rect(1400, 0, 600, 600),
        };
        var f = ArnesLayout.MiraElReparto(fuera, area, tol);
        Vale(!f.Bien, "una ventana fuera del área útil invalida el reparto");
        Vale(f.Porque.Contains("sale"), $"y lo dice: «{f.Porque}»");

        // ---------- Apiladas en una esquina sin tocarse ----------
        //
        // No se pisan, están dentro… y dejan la pantalla vacía. Sin esta tercera condición
        // pasaría por reparto.
        var minúsculas = new[]
        {
            new NubeHypr.Rect(0, 0, 100, 100),
            new NubeHypr.Rect(120, 0, 100, 100),
            new NubeHypr.Rect(240, 0, 100, 100),
        };
        var p = ArnesLayout.MiraElReparto(minúsculas, area, tol);
        Vale(!p.Bien, "tres ventanas diminutas en una esquina no reparten nada");
        Vale(p.Porque.Contains("%"), $"y se dice cuánto ocupan: «{p.Porque}»");

        // ---------- Y con una sola no hay nada que repartir ----------
        var una = new[] { new NubeHypr.Rect(0, 0, 1200, 600) };
        Vale(!ArnesLayout.MiraElReparto(una, area, tol).Bien,
             "con una ventana no se distingue repartir de maximizar: no vale como prueba");
    }

    static EfectoVentana Ef(TipoEfecto t, long h, string porque = "regla") =>
        new(t, (IntPtr)h, Porque: porque);

    // ================= Cuándo se pregunta =================
    static void ConfirmarCuandoHaceFalta()
    {
        Grupo("confirmar: las cuatro condiciones, y que ninguna sobra");

        // El caso que tiene que preguntar: la capa aplicando, la opción puesta, un movimiento
        // automático y sin permiso previo.
        Vale(HyprConfirmar.HaceFalta(NubeHypr.Modo.Aplicar, true, true, false),
             "aplicando + opción + automático + sin permiso: se pregunta");

        // ---------- Y las cuatro formas de que no haga falta ----------
        //
        // En simulación no se mueve nada: preguntar sería pedir permiso para escribir una
        // línea en el registro.
        Vale(!HyprConfirmar.HaceFalta(NubeHypr.Modo.Simulacion, true, true, false),
             "en simulación no se pregunta: no hay nada que confirmar");
        Vale(!HyprConfirmar.HaceFalta(NubeHypr.Modo.Apagado, true, true, false),
             "y apagado tampoco");

        // La opción es del usuario. Quien la apaga está diciendo «muévelas sin preguntar».
        Vale(!HyprConfirmar.HaceFalta(NubeHypr.Modo.Aplicar, false, true, false),
             "con la opción apagada no se pregunta: es una respuesta legítima");

        // Lo que el usuario acaba de ordenar no se vuelve a preguntar. Si `Win+Alt+→`
        // abriera un panel, cada pulsación costaría dos gestos y la opción se apagaría.
        Vale(!HyprConfirmar.HaceFalta(NubeHypr.Modo.Aplicar, true, false, false),
             "un atajo que acabas de pulsar no pregunta: pulsarlo ya es la respuesta");

        // Y la condición sin la cual nada se movería NUNCA: cuando el usuario dice «Mover»,
        // el lote vuelve a entrar por la misma puerta con el permiso dado.
        Vale(!HyprConfirmar.HaceFalta(NubeHypr.Modo.Aplicar, true, true, true),
             "ya concedida: no se vuelve a preguntar, o no se movería nunca");
    }

    // ================= Lo que caduca =================
    static void ConfirmarLoQueCaduca()
    {
        Grupo("confirmar: una propuesta de hace diez minutos no vale");

        var t0 = new DateTime(2026, 10, 27, 3, 0, 0);
        var p = new HyprConfirmar.Peticion("regla «Discord»",
                                           new[] { Ef(TipoEfecto.Colocar, 0x10) }, t0);

        Vale(!p.Caducada(t0), "recién pedida no está caducada");
        Vale(!p.Caducada(t0.AddSeconds(29)), "a los 29 s sigue valiendo");
        Vale(p.Caducada(t0.AddSeconds(30)), "a los 30 s caduca");
        Vale(p.Caducada(t0.AddMinutes(10)), "y a los diez minutos, con más razón");

        // Los efectos se calcularon contra las ventanas de un momento. Aplicarlos tarde
        // movería un reparto pensado para una pantalla que ya no es esa.
        Igual(30, p.SegundosQueQuedan(t0), "recién pedida le quedan los 30");
        Igual(1, p.SegundosQueQuedan(t0.AddSeconds(29)), "al final le queda 1");

        // Y nunca negativo: un «caduca en -412 s» en pantalla es un número que delata que
        // nadie miró el caso.
        Igual(0, p.SegundosQueQuedan(t0.AddMinutes(10)),
              "y pasada la hora no quedan segundos negativos");
    }

    // ================= Contar ventanas, no efectos =================
    static void ConfirmarComoSeCuenta()
    {
        Grupo("confirmar: se cuentan ventanas, que es lo que el usuario ve");

        // Una misma ventana puede llevar restaurar + colocar + enfocar. Decir «mover 3
        // ventanas» cuando es una sola sería contar mal delante de quien tiene la pantalla
        // al lado para desmentirlo.
        var tres = new[]
        {
            Ef(TipoEfecto.Restaurar, 0x10),
            Ef(TipoEfecto.Colocar, 0x10),
            Ef(TipoEfecto.Enfocar, 0x10),
        };
        Igual(1, HyprConfirmar.CuantasVentanas(tres),
              "tres efectos sobre la misma ventana son UNA ventana");

        var dos = new[] { Ef(TipoEfecto.Colocar, 0x10), Ef(TipoEfecto.Colocar, 0x20) };
        Igual(2, HyprConfirmar.CuantasVentanas(dos), "dos ventanas distintas son dos");

        // `SeQueda` no es un movimiento: es el motivo por el que algo NO se toca. Contarlo
        // haría que un lote donde nadie se mueve pidiera permiso para no hacer nada.
        var quietas = new[]
        {
            Ef(TipoEfecto.SeQueda, 0x30),
            Ef(TipoEfecto.SeQueda, 0x40),
            Ef(TipoEfecto.Colocar, 0x50),
        };
        Igual(1, HyprConfirmar.CuantasVentanas(quietas),
              "las que se quedan no cuentan: no se las mueve");
        Igual(0, HyprConfirmar.CuantasVentanas(new[] { Ef(TipoEfecto.SeQueda, 0x30) }),
              "y un lote donde nadie se mueve cuenta cero");
    }

    // ================= Lo que se le dice =================
    static void ConfirmarLoQueSeLeDice()
    {
        Grupo("confirmar: el texto dice la verdad, y en singular cuando toca");

        var t0 = new DateTime(2026, 10, 27, 3, 0, 0);

        var una = new HyprConfirmar.Peticion("regla «Discord»",
                                             new[] { Ef(TipoEfecto.Colocar, 0x10) }, t0);
        Vale(HyprConfirmar.Texto(una).Contains("1 ventana ("),
             "una ventana se dice «1 ventana», no «1 ventanas»");
        Vale(HyprConfirmar.Texto(una).Contains("Discord"),
             "y dice de dónde viene: una pregunta sin motivo no se puede contestar");

        var cuatro = new HyprConfirmar.Peticion("reparto (layout)", new[]
        {
            Ef(TipoEfecto.Colocar, 0x10), Ef(TipoEfecto.Colocar, 0x20),
            Ef(TipoEfecto.Colocar, 0x30), Ef(TipoEfecto.Colocar, 0x40),
        }, t0);
        Vale(HyprConfirmar.Texto(cuatro).Contains("4 ventanas"), "cuatro se dicen en plural");

        // El desglose: qué se les va a hacer, sin repetir «colocar» cuatro veces.
        Igual("cambiarlas de sitio y de tamaño", HyprConfirmar.Detalle(cuatro.Efectos),
              "cuatro «colocar» dan UNA frase, no cuatro");

        var mezcla = HyprConfirmar.Detalle(new[]
        {
            Ef(TipoEfecto.Colocar, 0x10),
            Ef(TipoEfecto.AEspacio, 0x10),
            Ef(TipoEfecto.SeQueda, 0x20),
        });
        Vale(mezcla.Contains("sitio") && mezcla.Contains("escritorio"),
             "y una mezcla nombra las dos cosas");
        Vale(!mezcla.Contains("quietas"),
             "pero no promete tocar las que se quedan");

        Igual("", HyprConfirmar.Detalle(new[] { Ef(TipoEfecto.SeQueda, 0x30) }),
              "y sin nada que hacer no se inventa una frase");

        // La línea del registro tiene que dejar rastro de las dos cuentas: ventanas y
        // efectos. Con una sola no se distingue «una ventana con tres pasos» de «tres
        // ventanas», que es justo lo que uno quiere saber leyendo el registro a posteriori.
        var l = HyprConfirmar.Linea("pedida", cuatro, t0.AddSeconds(2));
        Vale(l.Contains("4 ventana(s)") && l.Contains("4 efecto(s)"), "la línea da las dos cuentas");
        Vale(l.Contains("2,0 s") || l.Contains("2.0 s"), "y cuánto lleva pedida");
    }

    // ================= Una cada vez =================
    static void ConfirmarUnaCadaVez()
    {
        Grupo("confirmar: dos preguntas a la vez obligarían a adivinar");

        var t0 = new DateTime(2026, 10, 27, 3, 0, 0);
        var vieja = new HyprConfirmar.Peticion("regla A", new[] { Ef(TipoEfecto.Colocar, 0x10) }, t0);
        var nueva = new HyprConfirmar.Peticion("regla B", new[] { Ef(TipoEfecto.Colocar, 0x20) },
                                               t0.AddSeconds(3));

        var (queda, fuera) = HyprConfirmar.Sustituir(vieja, nueva);
        Vale(ReferenceEquals(queda, nueva), "la nueva sustituye a la pendiente");
        Vale(ReferenceEquals(fuera, vieja),
             "y la vieja se devuelve para poder apuntarla, no desaparece en silencio");

        var (sola, nada) = HyprConfirmar.Sustituir(null, nueva);
        Vale(ReferenceEquals(sola, nueva), "sin nada pendiente, la nueva queda en pie");
        Vale(nada == null, "y no se descarta nada que no existía");

        // Encolarlas sería peor que sustituirlas: el usuario contestaría la primera y se le
        // aplicaría la segunda sin haberla visto.
        Vale(!ReferenceEquals(queda, vieja), "y la vieja NO sigue en pie: no se encolan");
    }
}
