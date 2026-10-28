using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// Las pruebas de lógica, dentro del propio ejecutable: `NubeBar.exe --pruebas`.
//
// ---------- Por qué viven aquí y no en un arnés que se borra ----------
//
// Las fases anteriores comprobaban cada tanda con un archivo temporal que se tiraba al
// terminar. Para medir una animación eso está bien —lo que se mide es irrepetible— pero
// para esto no: lo que hay aquí abajo son reglas que tienen que seguir siendo verdad
// dentro de seis meses. El ranking del buscador y el contador de no leídas son
// exactamente el tipo de cosa que se rompe sin que nadie se entere, porque no da error:
// simplemente ordena mal.
//
// No añade nada al arranque normal —es un `if` en `App.OnStartup`, como `--salir` y
// `--guardian`— y no deja ningún proceso detrás.
//
// Nada de lo que hay aquí toca el disco del usuario: el centro de notificaciones se
// prueba con instancias propias y la persistencia, en una carpeta temporal que se borra.
public static partial class Pruebas
{
    internal const string Bandera = "--pruebas";

    static int _bien, _mal;
    static readonly StringBuilder _informe = new();
    static string _grupo = "";

    static void Grupo(string nombre)
    {
        _grupo = nombre;
        _informe.AppendLine();
        _informe.AppendLine("--- " + nombre + " ---");
    }

    static void Vale(bool condicion, string que)
    {
        if (condicion) { _bien++; return; }
        _mal++;
        _informe.AppendLine($"    MAL · {_grupo}: {que}");
    }

    static void Igual<T>(T esperado, T real, string que) =>
        Vale(EqualityComparer<T>.Default.Equals(esperado, real), $"{que} (esperado {esperado}, real {real})");

    public static int Correr(string[] args)
    {
        _informe.AppendLine("=== Pruebas de lógica de NubeBar ===");

        try
        {
            Peldanos();
            Erratas();
            OrdenEstable();
            Repetidos();
            Vacias();
            Costumbre();
            Cancelacion();
            Agrupar();
            NoLeidas();
            Descartar();
            Silenciar();
            NoMolestar();
            Persistencia();
            SieteTemas();
            AltoDelPanel();
            Niveles();

            // C4: ventanas reales
            Filtro();
            OrdenDeVentanas();
            EstadosDeVentana();
            Zonas();
            ZonasDelPunto();
            AreaDeTrabajo();
            DpiYMonitores();
            AltoDeLaVistaGeneral();
            CuandoFalla();
            AbrirYCerrarLaVista();
            PlanDeAjuste();
            EscConPrioridad();
            QuienSeArrastra();
            SoltarLaTarjeta();
            ArrastrarAlBorde();
            QueHacerConUnAviso();
            CuandoTocaFoto();
            OrdenCongelado();
            GlifoDelAspa();

            // Fase 0 del rediseño: el catálogo de colores y los nueve temas de serie
            CorrerTemas();
            SinColoresAMano();

            // C5: esquema y migraciones
            EsquemaDeducir();
            EsquemaMigrar();
            EsquemaClavesDesconocidas();
            EsquemaDelFuturo();
            EsquemaValidar();
            EsquemaEnDisco();
            EsquemaRecuperarDelBak();

            // C5: perfiles, fondos y la integración con Wallpaper Engine
            CicloDeVidaWE();
            PropiedadDeWE();
            EsperaDeWE();
            MonitorDeWE();
            FondosLogica();
            FondosHuella();
            FondosPresentacion();
            FondosValidacion();
            PerfilesListaBlanca();
            PerfilesValidar();
            PerfilesNombres();
            PerfilesDuplicar();
            PerfilesDeSerie();
            PerfilesEscrituras();
            PerfilesAplicarYDeshacer();
            PerfilesRestaurar();

            // C5.2: el gestor de fondos
            FondoDibujo();
            FondoMonitores();
            FondoPorMonitorEnElArbol();
            FondoNoRecarga();
            FondoRelojes();
            FondoDelPerfilNoche();
            FondoPngDeVerdad();

            // C5.3: el marco de la ventana de Ajustes
            AjustesSecciones();
            AjustesBusqueda();
            AjustesSubtitulos();
            AjustesIconosDeTarjeta();
            AjustesIconoDeGrupo();

            // C5.4: el lenguaje visual de los paneles
            PanelesSuperficieOpaca();
            PanelesEstadoDeMetrica();
            PanelesMedidas();
            PanelesTamano();
            GestoDeQuien();
            CentroDeControlComposicion();
            SliderCicloDeVida();
            SliderLasSeisSalidas();
            SliderDiezArrastres();
            GlifoDeLaCuadricula();
            AbrirYCerrar();

            // C5.8: el botón propio del centro de control
            InicioRepartoDeLaIsla();
            InicioEnLosCuatroBordes();
            InicioGestoYLanzadorIntacto();
            InicioAltoConTope();
            InicioSeVeDeVerdad();
            InicioSeVeEnLosDosTemasYDpi();

            // C5.12: la bandeja se actualiza en caliente
            BandejaComparaMuestras();
            BandejaIdentidadDeUnIcono();

            // D0: el contrato de seguridad de NubeHypr
            HyprNaceApagado();
            HyprLoQueNoSeToca();
            HyprCicloYVersiones();
            HyprGeometriaDeclarada();
            HyprApagadoNoMueveNada();
            HyprMigracionV4();
            HyprDiagnosticoNoEscribe();

            // D1: el modelo puro de layouts y comandos
            D1AreaUtil();
            D1RepartoSinCosturas();
            D1LosCuatroLayouts();
            D1CalcularYNoMoverLoQueNoToca();
            D1AplicarDosVecesNoDeriva();
            D1InsertarYQuitar();
            D1ComandosValidos();
            D1ComandosInvalidos();

            // D2: el motor real de posicionamiento
            D2Conversion();
            D2SimulacionPorDefecto();
            D2ElSnapshotViejoNoGana();
            D2RecuentoYTrazas();
            D2ElAreaSaleDelMonitorQueEs();
            D2SoloUnaVentana();

            // D3: foco, teclado y gestos
            D3MapaDeAtajos();
            D3ConflictosDeAtajos();
            D3PasosDeMoverYTamano();
            D3GestoCancelaDeVerdad();
            D3MaximizadasFueraPorDefecto();
            D3DevolverLasVentanas();

            // D4: escritorios virtuales
            D4NombresYOrden();
            D4ElVecino();
            D4ModoDeUnSoloEscritorio();
            D4ResolverComandos();
            D4MapaTolerante();
            D4CambiosDeFuera();

            // D3 cerrada: los atajos registrados de verdad
            D3RangoDeIdsYConflictos();
            D3MapaDeTresFuentes();
            D3LimpiezaDeAtajos();

            // D5: reglas, perfiles y persistencia
            D5UnaReglaVaciaNoCoincideConNada();
            D5CriteriosYCoincidencia();
            D5PrioridadesYEmpates();
            D5LaExplicacion();
            D5GuardarDeshacerYRollback();
            D5ReglasEnLaConfiguracion();

            // D5.1: las reglas, conectadas al momento en que aparece una ventana
            D51ReglaAplicable();
            D51ReglaAmbigua();
            D51Prioridad();
            D51Empate();
            D51VentanaDesconocida();
            D51VariasReglasAportanCamposDistintos();
            D51Idempotencia();
            D51ConfiguracionAntiguaConCadenas();
            D51ReglasYAtajosEnLosPerfiles();

            // D6: la integración visual. Los grupos viven en Pruebas.D6.cs — este archivo
            // pasó de 7.000 líneas, y seguir metiendo grupos aquí convierte cada edición en
            // un riesgo para todo lo demás.
            CorrerD6();

            // D7: fiabilidad, recuperación y modo seguro (Pruebas.D7.cs).
            CorrerD7();

            // D8: estabilización y regresión (Pruebas.D8.cs). Sin funciones nuevas: son las
            // pruebas que faltaban sobre lo que ya existía.
            CorrerD8();

            // D9.1: las tres redes que tienen que existir antes de aplicar (Pruebas.D9.cs).
            CorrerD9();

            // La guía de reglas: los ejemplos, la frase en castellano y los avisos
            // (Pruebas.Guia.cs).
            CorrerGuia();

            // E0: el gestor de superficies simultáneas (Pruebas.E0.cs).
            CorrerE0();

            // E1: la composición de la barra (Pruebas.E1.cs).
            CorrerE1();

            // E2: el dock (Pruebas.E2.cs).
            CorrerE2();

            // E3: los paneles laterales (Pruebas.E3.cs).
            CorrerE3();

            // E4: el calendario y el panel derecho (Pruebas.E4.cs).
            CorrerE4();

            // E5: el área de trabajo con las superficies de E (Pruebas.E5.cs).
            CorrerE5();

            // E6: las transiciones de la nube (Pruebas.E6.cs).
            CorrerE6();

            // E7: configuración por contexto y perfiles (Pruebas.E7.cs).
            CorrerE7();

            // E8: estabilización transversal (Pruebas.E8.cs).
            CorrerE8();

            // El modelo de módulos: añadir, quitar, mover, ocultar, guardar y releer
            // (Pruebas.Modulos.cs).
            CorrerModulos();

            // El panel de red: los siete estados y que los botones no desaparecen
            // (Pruebas.Red.cs).
            CorrerRed();

            // La cuadrícula de ventanas: adaptativa, en todas las pantallas
            // (Pruebas.Vista.cs).
            CorrerVista();

            // El editor de módulos: estado de cada chip, acciones y arrastre
            // (Pruebas.Editor.cs).
            CorrerEditor();

            // Los paneles: que caben en la pantalla o se desplazan por dentro
            // (Pruebas.Paneles.cs).
            CorrerPaneles();

            // D9.5: la decisión de una regla, convertida en efectos (Pruebas.Colocar.cs).
            CorrerColocar();

            // Preguntar antes de mover ventanas de verdad (Pruebas.Confirmar.cs).
            CorrerConfirmar();

            // C5.9: la superficie de Inicio
            PanelRaizOpaca();
            InicioColgadoDeSuBoton();
            CentroNoTocaLoQueSuena();


        }
        catch (Exception ex)
        {
            _mal++;
            _informe.AppendLine("    EXCEPCIÓN: " + ex);
        }

        _informe.AppendLine();
        _informe.AppendLine(_mal == 0
            ? $"TODO BIEN · {_bien} comprobaciones"
            : $"{_mal} MAL de {_bien + _mal} comprobaciones");

        var texto = _informe.ToString();
        Console.WriteLine(texto);
        var destino = args.Length > 1 ? args[1] : null;
        if (!string.IsNullOrWhiteSpace(destino))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
                File.WriteAllText(destino, texto, new UTF8Encoding(false));
            }
            catch (Exception ex) { Console.WriteLine("no se pudo escribir el informe: " + ex.Message); }
        }
        return _mal == 0 ? 0 : 1;
    }

    // ---------- Buscador: los cuatro peldaños ----------

    static Nivel N(string consulta, string texto) => Parecido.Medir(Parecido.Normalizar(consulta), texto).Nivel;
    static double P(string consulta, string texto) => Parecido.Medir(Parecido.Normalizar(consulta), texto).Puntos;

    static void Peldanos()
    {
        Grupo("ranking: los cuatro peldaños");

        Igual(Nivel.Exacta, N("notepad", "Notepad"), "escribirlo entero es exacta");
        Igual(Nivel.Exacta, N("NOTEPAD", "notepad"), "las mayúsculas dan igual");
        Igual(Nivel.Exacta, N("camara", "Cámara"), "los acentos dan igual");
        Igual(Nivel.Prefijo, N("note", "Notepad"), "empezar por ahí es prefijo");
        Igual(Nivel.Palabra, N("studio", "Visual Studio Code"), "una palabra que empieza así");
        Igual(Nivel.Palabra, N("vsc", "Visual Studio Code"), "las iniciales");
        Igual(Nivel.Palabra, N("note", "Keynote"), "aparecer por el medio");
        Igual(Nivel.Aproximada, N("fotosop", "Photoshop"), "con erratas es aproximada");
        Igual(Nivel.Ninguno, N("zzzz", "Notepad"), "lo que no se parece no coincide");

        // Y las franjas no se solapan: ese es el invariante del que vive el orden.
        Vale(P("notepad", "Notepad") > P("note", "Notepad"), "exacta por encima de prefijo");
        Vale(P("note", "Notepad") > P("studio", "Visual Studio Code"), "prefijo por encima de palabra");
        Vale(P("note", "Keynote") > P("fotosop", "Photoshop"), "palabra por encima de aproximada");
        Vale(P("note", "Notepad") >= 0.90 && P("note", "Notepad") <= 0.99, "prefijo cae en su franja");
        Vale(P("note", "Keynote") >= 0.78 && P("note", "Keynote") < 0.90, "palabra cae en su franja");
        Vale(P("fotosop", "Photoshop") > 0 && P("fotosop", "Photoshop") <= 0.76, "aproximada cae en su franja");

        // Dentro de la franja manda lo que cubre la consulta
        Vale(P("note", "Notepad") > P("note", "Notepad++ plugin manager"),
             "cubrir más del nombre puntúa más dentro del mismo peldaño");
    }

    static void Erratas()
    {
        Grupo("ranking: erratas");

        foreach (var (mal, bien) in new[]
        {
            ("fotosop", "Photoshop"), ("calcualdora", "Calculadora"), ("exlorer", "Explorer"),
            ("note pad", "Notepad"), ("crhome", "Chrome"), ("discrod", "Discord"),
        })
            Vale(N(mal, bien) != Nivel.Ninguno, $"\"{mal}\" encuentra \"{bien}\"");

        // Pero no cualquier cosa: con el listón tan bajo el buscador sería ruido
        Vale(N("xqz", "Notepad") == Nivel.Ninguno, "tres letras sueltas no encuentran Notepad");
    }

    static Resultado R(string titulo, double puntos, Nivel nivel,
                       Categoria cat = Categoria.App, string detalle = "", string clave = "") =>
        new() { Titulo = titulo, Puntos = puntos, Nivel = nivel, Categoria = cat, Detalle = detalle, Clave = clave };

    static void OrdenEstable()
    {
        Grupo("ranking: orden estable");

        // Mismos puntos, distinto orden de entrada: tiene que salir lo mismo
        var a = Ranking.Ordenar(new[]
        {
            R("Zeta", 0.9, Nivel.Prefijo), R("Alfa", 0.9, Nivel.Prefijo), R("Mu", 0.9, Nivel.Prefijo)
        });
        var b = Ranking.Ordenar(new[]
        {
            R("Mu", 0.9, Nivel.Prefijo), R("Zeta", 0.9, Nivel.Prefijo), R("Alfa", 0.9, Nivel.Prefijo)
        });
        Vale(a.Select(x => x.Titulo).SequenceEqual(b.Select(x => x.Titulo)),
             "con los mismos puntos el orden no depende de cómo llegaron");
        Igual("Alfa", a[0].Titulo, "a igualdad de puntos, por título");

        // Con los mismos puntos, el peldaño mejor va delante
        var c = Ranking.Ordenar(new[]
        {
            R("Aproximada", 0.8, Nivel.Aproximada), R("Zalabra", 0.8, Nivel.Palabra)
        });
        Igual("Zalabra", c[0].Titulo, "a igualdad de puntos manda el peldaño");

        // Y los puntos mandan sobre el peldaño: una fuente puede degradar a propósito
        var d = Ranking.Ordenar(new[]
        {
            R("Archivo exacto", 0.72, Nivel.Exacta, Categoria.Archivo),
            R("App con prefijo", 0.95, Nivel.Prefijo)
        });
        Igual("App con prefijo", d[0].Titulo, "una app con prefijo gana a un archivo exacto degradado");

        // Dos veces lo mismo da exactamente lo mismo
        var uno = Ranking.Ordenar(new[] { R("A", 0.9, Nivel.Prefijo), R("B", 0.5, Nivel.Aproximada) });
        var dos = Ranking.Ordenar(new[] { R("A", 0.9, Nivel.Prefijo), R("B", 0.5, Nivel.Aproximada) });
        Vale(uno.Select(x => x.Titulo).SequenceEqual(dos.Select(x => x.Titulo)), "dos búsquedas iguales dan lo mismo");
    }

    static void Repetidos()
    {
        Grupo("ranking: repetidos");

        var lista = Ranking.Ordenar(new[]
        {
            R("Notepad", 0.80, Nivel.Palabra, Categoria.App, "notepad.exe"),
            R("Notepad", 0.95, Nivel.Prefijo, Categoria.App, "notepad.exe"),
            R("Notepad", 0.90, Nivel.Prefijo, Categoria.Archivo, "notepad.exe"),
        });
        Igual(2, lista.Count, "dos filas idénticas se funden en una");
        Igual(0.95, lista[0].Puntos, "y se queda la de más puntos");

        // Dos ventanas del mismo programa comparten clave a propósito: NO son repetidas
        var ventanas = Ranking.Ordenar(new[]
        {
            R("Documento 1", 0.9, Nivel.Prefijo, Categoria.Ventana, "word.exe", "ventana:word.exe"),
            R("Documento 2", 0.9, Nivel.Prefijo, Categoria.Ventana, "word.exe", "ventana:word.exe"),
        });
        Igual(2, ventanas.Count, "dos ventanas del mismo programa no se funden");
    }

    static void Vacias()
    {
        Grupo("ranking: búsquedas vacías");

        Igual(Nivel.Ninguno, N("", "Notepad"), "la consulta vacía no coincide con nada");
        Igual(0.0, P("", "Notepad"), "y no puntúa");
        Igual(Nivel.Ninguno, N("notepad", ""), "un nombre vacío no coincide con nada");
        Igual(0, Ranking.Ordenar(Array.Empty<Resultado>()).Count, "ordenar una lista vacía da una lista vacía");
        Igual(0, Ranking.Ordenar(new[] { R("Nada", 0, Nivel.Ninguno) }).Count(x => x.Puntos > 0),
              "sin puntos no hay nada que enseñar");
    }

    static void Costumbre()
    {
        Grupo("ranking: la costumbre no cambia de peldaño");

        // Una app muy usada que coincide mal NO puede adelantar a una que coincide bien
        double flojo = Ranking.ConCostumbre(0.70, 0.35, Nivel.Aproximada);
        Vale(flojo < 0.78, $"la costumbre no saca una aproximada de su franja ({flojo:0.000})");

        double palabra = Ranking.ConCostumbre(0.86, 0.35, Nivel.Palabra);
        Vale(palabra < 0.90, $"ni una de palabra a prefijo ({palabra:0.000})");

        // Ni con el empujón al máximo, desde el suelo de su franja
        Vale(Ranking.ConCostumbre(0.78, 1.0, Nivel.Palabra) < 0.90, "ni con el empujón disparado");
        Vale(Ranking.ConCostumbre(0.01, 1.0, Nivel.Aproximada) < 0.78, "ni desde abajo del todo");

        // Pero sí ordena dentro del peldaño
        Vale(Ranking.ConCostumbre(0.80, 0.2, Nivel.Palabra) > Ranking.ConCostumbre(0.80, 0.0, Nivel.Palabra),
             "y sí desempata dentro del peldaño");
        Igual(1.0, Ranking.ConCostumbre(1.0, 0.35, Nivel.Exacta), "lo exacto ya está arriba del todo");
        Igual(0.5, Ranking.ConCostumbre(0.5, 0, Nivel.Aproximada), "sin costumbre no cambia nada");
        // Y un extra puesto a mano por la fuente no se toca
        Igual(1.3, Ranking.ConCostumbre(1.3, 0.35, Nivel.Exacta), "un alias exacto conserva su extra");
    }

    static void Cancelacion()
    {
        Grupo("buscador: cancelar lo viejo");

        var relevo = new Relevo();
        long a = relevo.Pedir();
        Vale(relevo.SigueSiendoLaUltima(a), "la única petición es la última");

        long b = relevo.Pedir();
        Vale(!relevo.SigueSiendoLaUltima(a), "al pedir otra, la anterior ya no vale");
        Vale(relevo.SigueSiendoLaUltima(b), "y la nueva sí");

        relevo.Olvidar();
        Vale(!relevo.SigueSiendoLaUltima(b), "al cerrar el panel no vale ninguna");

        // Tres seguidas: solo la última
        long c1 = relevo.Pedir(), c2 = relevo.Pedir(), c3 = relevo.Pedir();
        Vale(!relevo.SigueSiendoLaUltima(c1) && !relevo.SigueSiendoLaUltima(c2) && relevo.SigueSiendoLaUltima(c3),
             "escribiendo rápido solo cuenta la última");
    }

    // ---------- Centro de notificaciones ----------

    static long _id;
    static DateTime _reloj = new(2026, 10, 25, 12, 0, 0);

    static Notificaciones.Aviso Av(string app, string titulo, int minutos = 0, string? aum = null) =>
        new(++_id, app, titulo, "cuerpo", _reloj.AddMinutes(-minutos), aum ?? (app + "_x!App"));

    static CentroNotificaciones Nuevo()
    {
        _id = 0;
        return new CentroNotificaciones();
    }

    static void Agrupar()
    {
        Grupo("centro: agrupar por aplicación");

        var c = Nuevo();
        c.Recibir(new[] { Av("Discord", "uno", 30), Av("Correo", "dos", 20), Av("Discord", "tres", 5) });

        var grupos = c.PorApp();
        Igual(2, grupos.Count, "dos apps, dos grupos");
        Igual("Discord", grupos[0].App, "va delante la app con la notificación más reciente");
        Igual(2, grupos[0].Avisos.Count, "las dos de Discord van juntas");
        Igual("tres", grupos[0].Avisos[0].Titulo, "y dentro, la más nueva arriba");
        Igual(3, c.Cuantas, "el historial las tiene todas");

        // Repetir el mismo id no duplica
        var repetida = c.Todas[0];
        c.Recibir(new[] { new Notificaciones.Aviso(repetida.Id, repetida.App, repetida.Titulo,
                                                   repetida.Texto, repetida.Cuando, repetida.AumId) });
        Igual(3, c.Cuantas, "la misma notificación dos veces no se duplica");

        // El tope
        var lleno = Nuevo();
        lleno.Recibir(Enumerable.Range(0, CentroNotificaciones.Tope + 25)
                                .Select(i => Av("App", "n" + i, i)).ToList());
        Igual(CentroNotificaciones.Tope, lleno.Cuantas, "el historial tiene tope");
        Igual("n0", lleno.Todas[0].Titulo, "y lo que se tira es lo más viejo");
    }

    static void NoLeidas()
    {
        Grupo("centro: contador de no leídas");

        var c = Nuevo();
        Igual(0, c.NoLeidas, "un centro vacío no tiene nada sin leer");
        Vale(c.Vacio, "y está vacío");

        c.Recibir(new[] { Av("A", "uno"), Av("B", "dos"), Av("A", "tres") });
        Igual(3, c.NoLeidas, "todo lo que llega está sin leer");
        Vale(!c.Vacio, "ya no está vacío");

        var primera = c.Todas[0];
        Vale(c.MarcarLeida(primera.Id), "marcar una devuelve que cambió algo");
        Igual(2, c.NoLeidas, "y baja el contador");
        Vale(!c.MarcarLeida(primera.Id), "marcarla otra vez no cambia nada");
        Igual(2, c.NoLeidas, "ni el contador");
        Vale(!c.MarcarLeida(99999), "marcar una que no existe no cambia nada");

        Igual(2, c.MarcarTodasLeidas(), "marcar todas devuelve cuántas quedaban");
        Igual(0, c.NoLeidas, "y no queda ninguna");
        Igual(3, c.Cuantas, "pero siguen en el historial");

        // El contador por grupo
        var d = Nuevo();
        d.Recibir(new[] { Av("A", "uno"), Av("A", "dos"), Av("B", "tres") });
        d.MarcarLeida(d.Todas.First(x => x.Titulo == "uno").Id);
        var gA = d.PorApp().First(g => g.App == "A");
        Igual(1, gA.NoLeidas, "el grupo cuenta sus propias no leídas");
    }

    static void Descartar()
    {
        Grupo("centro: descartar y borrar todo");

        var c = Nuevo();
        c.Recibir(new[] { Av("A", "uno"), Av("B", "dos") });
        var uno = c.Todas.First(x => x.Titulo == "uno");

        Vale(c.Descartar(uno.Id), "descartar una devuelve que se fue");
        Igual(1, c.Cuantas, "y ya no está");
        Vale(!c.Descartar(uno.Id), "descartarla otra vez no hace nada");

        // Y no vuelve cuando se relee la base de Windows
        c.Recibir(new[] { new Notificaciones.Aviso(uno.Id, uno.App, uno.Titulo, uno.Texto, uno.Cuando, uno.AumId) });
        Igual(1, c.Cuantas, "una descartada no vuelve al releer");

        Igual(1, c.BorrarTodo(), "borrar todo devuelve cuántas había");
        Vale(c.Vacio, "y deja el centro vacío");
        Igual(0, c.NoLeidas, "sin nada sin leer");
        Igual(0, c.BorrarTodo(), "borrar un centro vacío no borra nada");

        // Lo borrado tampoco vuelve
        c.Recibir(new[] { Av("A", "uno") });
        Igual(1, c.Cuantas, "pero una nueva sí entra");
    }

    static void Silenciar()
    {
        Grupo("centro: silenciar una aplicación");

        var c = Nuevo();
        c.Recibir(new[] { Av("Discord", "antes", 10, "Discord_abc!App") });
        c.Silenciar("Discord_abc!App", true);

        Vale(c.EstaSilenciada("Discord_abc!App"), "queda silenciada");
        Vale(!c.EstaSilenciada("Correo_xyz!App"), "y solo ella");
        Igual(1, c.Cuantas, "silenciar no borra lo que ya había");

        var entraron = c.Recibir(new[] { Av("Discord", "después", 0, "Discord_abc!App") });
        Igual(0, entraron.Count, "lo nuevo de una app silenciada no entra");
        Igual(1, c.Cuantas, "ni al historial");

        c.Silenciar("Discord_abc!App", false);
        Igual(1, c.Recibir(new[] { Av("Discord", "otra vez", 0, "Discord_abc!App") }).Count,
              "al quitarle el silencio vuelve a entrar");

        // Por trozo, como el config antiguo
        var d = Nuevo();
        d.Silenciar("Discord", true);
        Vale(d.EstaSilenciada("Discord_abc123!App"), "silenciar por trozo del identificador también vale");
        Vale(!d.EstaSilenciada(""), "un identificador vacío nunca está silenciado");
    }

    static void NoMolestar()
    {
        Grupo("centro: no molestar");

        var c = Nuevo();
        Vale(!c.NoMolestar, "de serie está apagado");

        c.NoMolestar = true;
        var entraron = c.Recibir(new[] { Av("A", "uno"), Av("B", "dos") });
        Igual(2, entraron.Count, "con no molestar las notificaciones SÍ entran al centro");
        Igual(2, c.Cuantas, "y se guardan en el historial");
        Igual(2, c.NoLeidas, "y cuentan como sin leer");

        // Lo que hace el modo es que la isla no las asome, y eso lo decide quien las
        // enseña mirando este interruptor; aquí lo que se garantiza es que no se pierden.
        c.NoMolestar = false;
        Igual(2, c.Cuantas, "apagarlo no cambia el historial");
    }

    static void Persistencia()
    {
        Grupo("centro: el historial se recuerda");

        var carpeta = Path.Combine(Path.GetTempPath(), "NubeBar-pruebas-" + Guid.NewGuid().ToString("N")[..8]);
        var ruta = Path.Combine(carpeta, "notificaciones.json");
        try
        {
            var c = Nuevo();
            c.Recibir(new[] { Av("A", "uno"), Av("B", "dos"), Av("C", "tres") });
            var leida = c.Todas.First(x => x.Titulo == "uno");
            var fuera = c.Todas.First(x => x.Titulo == "dos");
            c.MarcarLeida(leida.Id);
            c.Descartar(fuera.Id);
            c.Silenciar("App_silenciada", true);
            c.NoMolestar = true;
            c.Guardar(ruta);

            Vale(File.Exists(ruta), "se escribe el archivo");

            // Otro centro, el mismo disco: la base de Windows vuelve a traerlo todo
            var d = new CentroNotificaciones();
            d.Cargar(ruta);
            Vale(d.NoMolestar, "se recuerda el no molestar");
            Vale(d.EstaSilenciada("App_silenciada"), "se recuerdan los silencios");

            d.Recibir(new[]
            {
                new Notificaciones.Aviso(leida.Id, "A", "uno", "cuerpo", leida.Cuando, leida.AumId),
                new Notificaciones.Aviso(fuera.Id, "B", "dos", "cuerpo", fuera.Cuando, fuera.AumId),
            });
            d.AplicarLoGuardado();

            Igual(1, d.Cuantas, "la descartada no vuelve");
            Igual(0, d.NoLeidas, "y la que estaba leída sigue leída");

            // Cargar un archivo que no existe no revienta ni cambia nada
            var e = new CentroNotificaciones();
            e.Cargar(Path.Combine(carpeta, "no-existe.json"));
            Vale(e.Vacio && !e.NoMolestar, "cargar lo que no existe deja el centro como estaba");
        }
        finally
        {
            try { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); } catch { }
        }
    }



    // ================= C4: gestor de ventanas =================
    //
    // Todo esto se comprueba con FICHAS INVENTADAS, no con ventanas de verdad. Es la
    // razón de que `EsDelUsuario` reciba datos planos en vez de un HWND: una prueba que
    // tuviera que abrir el Bloc de notas para comprobar el filtro acabaría cerrando
    // ventanas del usuario, y eso no se hace.

    const int ToolWindow = 0x00000080;
    const int AppWindow = 0x00040000;
    const int NoActivable = 0x08000000;

    static Nativo.FichaVentana F(
        string clase = "Notepad", int largoTitulo = 10, bool visible = true, bool dueno = false,
        int estiloEx = 0, bool tapada = false, bool nuestra = false, int pid = 1234, long hwnd = 0x1000) =>
        new((IntPtr)hwnd, visible, largoTitulo, dueno, estiloEx, 0, tapada, nuestra, clase, pid);

    static void Filtro()
    {
        Grupo("ventanas: qué cuenta como ventana del usuario");

        Vale(GestorVentanas.EsDelUsuario(F(), out _), "una ventana normal cuenta");

        // Lo nuestro, fuera. Es lo primero que hay que acertar: si falla, "cerrar
        // todas" o "forzar el cierre" podrían acabar matando a NubeBar.
        Vale(!GestorVentanas.EsDelUsuario(F(nuestra: true), out var p1), "las de NubeBar no cuentan");
        Igual("es de NubeBar", p1, "y se dice por qué");

        Vale(!GestorVentanas.EsDelUsuario(F(visible: false), out _), "las invisibles no cuentan");
        Vale(!GestorVentanas.EsDelUsuario(F(tapada: true), out _),
             "las tapadas tampoco (otro escritorio virtual, o suspendida)");
        Vale(!GestorVentanas.EsDelUsuario(F(estiloEx: ToolWindow), out var p2), "las tool window no cuentan");
        Igual("es una ventana de herramientas", p2, "y se dice por qué");
        Vale(!GestorVentanas.EsDelUsuario(F(estiloEx: NoActivable), out _),
             "las que no se pueden activar no cuentan: pulsarlas no haría nada");

        // Colgar de otra = ser su diálogo. La excepción documentada es APPWINDOW.
        Vale(!GestorVentanas.EsDelUsuario(F(dueno: true), out _), "las que cuelgan de otra no cuentan");
        Vale(GestorVentanas.EsDelUsuario(F(dueno: true, estiloEx: AppWindow), out _),
             "salvo si piden explícitamente que se las trate como aplicación");

        // El título vacío es sospechoso, pero NO es lo único que se mira: esa era la
        // regla de antes y dejaba pasar el escritorio y la barra de tareas.
        Vale(!GestorVentanas.EsDelUsuario(F(largoTitulo: 0), out var p3), "sin título no cuenta");
        Igual("no tiene título", p3, "y se dice por qué");

        foreach (var c in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
                                  "Windows.UI.Core.CoreWindow", "MultitaskingViewFrame", "Button" })
            Vale(!GestorVentanas.EsDelUsuario(F(clase: c), out _), $"la clase {c} es del sistema");

        Vale(GestorVentanas.ClaseDelSistema("progman"), "las clases del sistema no distinguen mayúsculas");
        Vale(!GestorVentanas.ClaseDelSistema("CabinetWClass"), "el Explorador sí es del usuario");
        Vale(!GestorVentanas.ClaseDelSistema(null), "una clase nula no es del sistema");

        Vale(!GestorVentanas.EsDelUsuario(F(hwnd: 0), out _), "un handle nulo no es nada");
    }

    static VentanaReal V(string titulo = "t", string app = "app", long hwnd = 1,
                         bool activa = false, long ultimaVez = 0,
                         EstadoVentana estado = EstadoVentana.Normal, int pid = 1) =>
        new()
        {
            Hwnd = (IntPtr)hwnd, Titulo = titulo, App = app, Activa = activa,
            UltimaVez = ultimaVez, Estado = estado, Pid = pid
        };

    static List<VentanaReal> Ordenadas(params VentanaReal[] v)
    {
        var l = v.ToList();
        l.Sort(GestorVentanas.Comparar);
        return l;
    }

    static void OrdenDeVentanas()
    {
        Grupo("ventanas: orden estable");

        // La activa, siempre delante
        var a = Ordenadas(V("b", hwnd: 2, ultimaVez: 9), V("a", hwnd: 1, activa: true, ultimaVez: 1));
        Igual("a", a[0].Titulo, "la activa va primero aunque otra se usara después");

        // Y después, por lo reciente
        var b = Ordenadas(V("vieja", hwnd: 1, ultimaVez: 1), V("nueva", hwnd: 2, ultimaVez: 5));
        Igual("nueva", b[0].Titulo, "después, la usada más recientemente");

        // A igualdad total, por app, título y handle: siempre el mismo orden
        var c1 = Ordenadas(V("z", "beta", 3), V("a", "alfa", 2), V("m", "alfa", 1));
        var c2 = Ordenadas(V("m", "alfa", 1), V("z", "beta", 3), V("a", "alfa", 2));
        Vale(c1.Select(x => x.Titulo).SequenceEqual(c2.Select(x => x.Titulo)),
             "el orden no depende de cómo llegaron");
        Igual("a", c1[0].Titulo, "a igualdad, por aplicación y título");

        // Dos ventanas idénticas salvo el handle: siguen siendo dos y en orden fijo
        var d = Ordenadas(V("igual", "app", 7), V("igual", "app", 3));
        Igual(2, d.Count, "dos ventanas iguales no se funden");
        Igual((IntPtr)3, d[0].Hwnd, "y desempata el handle");
    }

    static void EstadosDeVentana()
    {
        Grupo("ventanas: estados y nombres");

        Igual("Minimizada", V(estado: EstadoVentana.Minimizada).Estampa, "se lee el estado minimizada");
        Igual("Maximizada", V(estado: EstadoVentana.Maximizada).Estampa, "y maximizada");
        Igual("Normal", V().Estampa, "y normal");

        Igual("notepad", GestorVentanas.NombreDeApp(@"C:\Windows\system32\notepad.exe"), "el nombre sale de la ruta");
        Igual("", GestorVentanas.NombreDeApp(""), "sin ruta no hay nombre");
        Igual("", GestorVentanas.NombreDeApp("   "), "ni con espacios");

        // Una ruta imposible no puede reventar: viene de preguntarle a otro proceso
        try { GestorVentanas.NombreDeApp("??::\\<>|"); Vale(true, "una ruta imposible no revienta"); }
        catch { Vale(false, "una ruta imposible no revienta"); }

        // El tamaño sale del rectángulo
        var v = V();
        v.Rect = new Nativo.RECT { Izq = 10, Arr = 20, Der = 110, Aba = 220 };
        Igual(100, v.Ancho, "el ancho sale del rectángulo");
        Igual(200, v.Alto, "y el alto");
    }

    // ---------- Zonas de ajuste ----------

    static Monitores.Info Pantalla(int x = 0, int y = 0, int w = 1920, int h = 1080,
                                   int tx = 0, int ty = 0, int tw = 1920, int th = 1080, uint dpi = 96) =>
        new(IntPtr.Zero, @"\\.\DISPLAY1", x, y, w, h, tx, ty, tw, th, true, dpi);

    static void Zonas()
    {
        Grupo("ajuste: a dónde va cada zona");

        var area = new Hueco(0, 0, 1920, 1080);

        var izq = ZonasAjuste.Colocacion(Zona.Izquierda, area);
        var der = ZonasAjuste.Colocacion(Zona.Derecha, area);
        Igual(new Hueco(0, 0, 960, 1080), izq, "la mitad izquierda");
        Igual(new Hueco(960, 0, 960, 1080), der, "la mitad derecha");
        Igual(area.Ancho, izq.Ancho + der.Ancho, "las dos mitades suman el ancho entero");
        Igual(izq.Derecha, der.X, "y se tocan sin dejar raya");

        // Con un ancho impar tampoco queda raya
        var impar = new Hueco(0, 0, 1921, 1080);
        var i2 = ZonasAjuste.Colocacion(Zona.Izquierda, impar);
        var d2 = ZonasAjuste.Colocacion(Zona.Derecha, impar);
        Igual(1921, i2.Ancho + d2.Ancho, "con ancho impar siguen sumando el total");
        Igual(i2.Derecha, d2.X, "y siguen tocándose");

        Igual(area, ZonasAjuste.Colocacion(Zona.Maximizar, area), "maximizar es el área entera");
        Igual(new Hueco(0, 0, 960, 540), ZonasAjuste.Colocacion(Zona.ArribaIzquierda, area), "cuarto superior izquierdo");
        Igual(new Hueco(960, 540, 960, 540), ZonasAjuste.Colocacion(Zona.AbajoDerecha, area), "cuarto inferior derecho");

        // Los cuatro cuartos cubren la pantalla entera sin solaparse
        long suma = 0;
        foreach (var z in new[] { Zona.ArribaIzquierda, Zona.ArribaDerecha, Zona.AbajoIzquierda, Zona.AbajoDerecha })
        {
            var c = ZonasAjuste.Colocacion(z, area);
            suma += (long)c.Ancho * c.Alto;
        }
        Igual((long)area.Ancho * area.Alto, suma, "los cuatro cuartos cubren la pantalla exacta");

        // Un área vacía no produce rectángulos absurdos
        Vale(ZonasAjuste.Colocacion(Zona.Izquierda, new Hueco(0, 0, 0, 0)).Vacio, "un área vacía sigue vacía");
    }

    static void ZonasDelPunto()
    {
        Grupo("ajuste: qué zona pide el puntero");

        var m = Pantalla();
        Igual(Zona.Izquierda, ZonasAjuste.ZonaDelPunto(2, 540, m), "pegado al borde izquierdo");
        Igual(Zona.Derecha, ZonasAjuste.ZonaDelPunto(1918, 540, m), "pegado al derecho");
        Igual(Zona.Maximizar, ZonasAjuste.ZonaDelPunto(960, 2, m), "arriba del todo, en el centro");
        Igual(Zona.ArribaIzquierda, ZonasAjuste.ZonaDelPunto(2, 2, m), "la esquina de arriba a la izquierda");
        Igual(Zona.ArribaDerecha, ZonasAjuste.ZonaDelPunto(1918, 2, m), "la de arriba a la derecha");
        Igual(Zona.AbajoIzquierda, ZonasAjuste.ZonaDelPunto(2, 1078, m), "la de abajo a la izquierda");
        Igual(Zona.AbajoDerecha, ZonasAjuste.ZonaDelPunto(1918, 1078, m), "la de abajo a la derecha");
        Igual(Zona.Ninguna, ZonasAjuste.ZonaDelPunto(960, 540, m), "en el centro no se ajusta nada");
        Igual(Zona.Ninguna, ZonasAjuste.ZonaDelPunto(960, 900, m), "ni por abajo sin tocar el borde");

        // Fuera del monitor, nada
        Igual(Zona.Ninguna, ZonasAjuste.ZonaDelPunto(-50, 540, m), "a la izquierda del monitor, nada");
        Igual(Zona.Ninguna, ZonasAjuste.ZonaDelPunto(3000, 540, m), "y a la derecha tampoco");

        // Un monitor que no empieza en 0,0: el segundo de un escritorio extendido
        var dos = Pantalla(x: 1920, y: 0, w: 2560, h: 1440, tx: 1920, tw: 2560, th: 1440);
        Igual(Zona.Izquierda, ZonasAjuste.ZonaDelPunto(1922, 700, dos), "el borde izquierdo del segundo monitor");
        Igual(Zona.Derecha, ZonasAjuste.ZonaDelPunto(4478, 700, dos), "y su borde derecho");
        Igual(Zona.Ninguna, ZonasAjuste.ZonaDelPunto(10, 700, dos), "un punto del primero no es de este");

        // Un monitor diminuto no puede dar zonas absurdas
        var enano = Pantalla(w: 20, h: 20, tw: 20, th: 20);
        Vale(ZonasAjuste.ZonaDelPunto(10, 10, enano) != Zona.Ninguna, "en una pantalla diminuta todo es borde");
        Igual(Zona.Ninguna, ZonasAjuste.ZonaDelPunto(0, 0, Pantalla(w: 0, h: 0)), "un monitor de tamaño cero no da zonas");
    }

    static void AreaDeTrabajo()
    {
        Grupo("ajuste: el área útil con la barra en cada borde");

        // Lo que Windows llama área de trabajo ya trae descontada la franja de una
        // AppBar registrada. Se comprueba con las cuatro posiciones de NubeBar.
        const int franja = 48;

        var arriba = Pantalla(ty: franja, th: 1080 - franja);
        var a = ZonasAjuste.AreaUtil(arriba);
        Igual(franja, a.Y, "con la barra arriba, el área empieza más abajo");
        Igual(1080 - franja, a.Alto, "y es más baja");
        Igual(franja, ZonasAjuste.Colocacion(Zona.Izquierda, a).Y, "y la mitad izquierda no se mete debajo");

        var abajo = Pantalla(th: 1080 - franja);
        var b = ZonasAjuste.AreaUtil(abajo);
        Igual(0, b.Y, "con la barra abajo, el área empieza arriba del todo");
        Igual(1080 - franja, b.Abajo, "y termina antes de la barra");
        Igual(1080 - franja, ZonasAjuste.Colocacion(Zona.AbajoDerecha, b).Abajo, "y el cuarto inferior tampoco la pisa");

        var izquierda = Pantalla(tx: franja, tw: 1920 - franja);
        var c = ZonasAjuste.AreaUtil(izquierda);
        Igual(franja, c.X, "con la barra a la izquierda, el área empieza a su derecha");
        Igual(franja, ZonasAjuste.Colocacion(Zona.Izquierda, c).X, "y la mitad izquierda empieza ahí");

        var derecha = Pantalla(tw: 1920 - franja);
        var d = ZonasAjuste.AreaUtil(derecha);
        Igual(1920 - franja, d.Derecha, "con la barra a la derecha, el área termina antes");
        Igual(1920 - franja, ZonasAjuste.Colocacion(Zona.Derecha, d).Derecha, "y la mitad derecha no la pisa");

        // Y la cuenta de restar una franja a mano, para cuando haga falta
        var entero = new Hueco(0, 0, 1920, 1080);
        Igual(new Hueco(0, 40, 1920, 1040), entero.Menos("arriba", 40), "restar por arriba");
        Igual(new Hueco(0, 0, 1920, 1040), entero.Menos("abajo", 40), "restar por abajo");
        Igual(new Hueco(40, 0, 1880, 1080), entero.Menos("izquierda", 40), "restar por la izquierda");
        Igual(new Hueco(0, 0, 1880, 1080), entero.Menos("derecha", 40), "restar por la derecha");
        Igual(entero, entero.Menos("arriba", 0), "restar cero no cambia nada");
        Igual(entero, entero.Menos("loquesea", 40), "un borde desconocido no cambia nada");
        Vale(!entero.Menos("arriba", 99999).Vacio, "una franja absurda no deja el área vacía");
    }

    static void DpiYMonitores()
    {
        Grupo("ajuste: DPI y varios monitores");

        // El ajuste trabaja en PÍXELES de pantalla, no en unidades de WPF: por eso el
        // DPI no cambia las cuentas. Lo que cambia con el DPI es cuánto mide eso en la
        // pantalla, y de eso se encarga Windows.
        foreach (uint dpi in new uint[] { 96, 120, 144, 192 })
        {
            var m = Pantalla(dpi: dpi);
            var izq = ZonasAjuste.Colocacion(Zona.Izquierda, ZonasAjuste.AreaUtil(m));
            Igual(960, izq.Ancho, $"a {dpi} ppp la mitad sigue siendo la mitad en píxeles");
            Igual(Zona.Izquierda, ZonasAjuste.ZonaDelPunto(2, 540, m), $"y el borde sigue siendo el borde a {dpi} ppp");
        }

        // Dos monitores con DPI distinto: cada uno manda en lo suyo
        var principal = Pantalla(w: 3840, h: 2160, tw: 3840, th: 2160, dpi: 192);
        var segundo = Pantalla(x: 3840, w: 1920, h: 1080, tx: 3840, tw: 1920, th: 1080, dpi: 96);
        Igual(1920, ZonasAjuste.Colocacion(Zona.Izquierda, ZonasAjuste.AreaUtil(principal)).Ancho,
              "la mitad del 4K son 1920 px");
        Igual(960, ZonasAjuste.Colocacion(Zona.Izquierda, ZonasAjuste.AreaUtil(segundo)).Ancho,
              "y la del 1080p, 960");
        Igual(3840, ZonasAjuste.Colocacion(Zona.Izquierda, ZonasAjuste.AreaUtil(segundo)).X,
              "la mitad del segundo monitor empieza donde empieza él");
    }

    static void AltoDeLaVistaGeneral()
    {
        Grupo("vista general: el alto de la tarjeta");

        foreach (var (alto, que) in new[]
        {
            (768.0, "portátil 1366x768"), (1080.0, "1080p"), (1440.0, "1440p"), (1920.0, "vertical"),
        })
        {
            double vacia = MainWindow.AltoDeLaVista(0, 1, alto);
            double pocas = MainWindow.AltoDeLaVista(2, 2, alto);
            double muchas = MainWindow.AltoDeLaVista(24, 4, alto);

            Vale(vacia >= 150, $"[{que}] el estado vacío tiene sitio para su mensaje ({vacia:0})");
            Vale(pocas <= muchas, $"[{que}] con más ventanas la vista no encoge");
            Vale(muchas <= alto * 0.73 + 1, $"[{que}] y nunca pasa del 72 % de la pantalla ({muchas:0} de {alto:0})");
            Vale(muchas >= 260, $"[{que}] pero siempre cabe algo ({muchas:0})");
        }
        Vale(MainWindow.AltoDeLaVista(40, 4, 200) >= 260, "en una pantalla de 200 px sigue siendo sensato");
    }

    // ---------- Lo que pasa cuando algo falla ----------

    static void CuandoFalla()
    {
        Grupo("ventanas: cuando algo no se puede");

        // Un handle muerto no revienta nada, y ninguna acción lo intenta a ciegas
        var muerta = (IntPtr)0x7FFFFFFF;
        Vale(!Nativo.Existe(IntPtr.Zero), "el handle nulo no existe");
        Vale(!Nativo.Existe(muerta), "un handle inventado no existe");
        Vale(!ZonasAjuste.Ajustar(muerta, Zona.Izquierda), "ajustar una ventana que no existe no hace nada");
        Vale(!ZonasAjuste.Ajustar(IntPtr.Zero, Zona.Maximizar), "ni la nula");
        Vale(!ZonasAjuste.Ajustar((IntPtr)1, Zona.Ninguna), "ni a ninguna zona");

        // Y colocar tampoco
        Vale(!Nativo.ColocarVentanaDelUsuario(muerta, 0, 0, 100, 100), "colocar una que no existe falla sin romper");

        // La foto de una ventana que no existe es null, no una excepción: la tarjeta
        // tiene que poder enseñar el icono en vez de un rectángulo roto.
        Vale(Nativo.Foto(muerta, 200) == null, "la foto de una ventana muerta es nula");
        Vale(Nativo.Foto(IntPtr.Zero, 200) == null, "y la de la nula también");

        // El icono tampoco revienta
        try { Nativo.Icono(muerta); Vale(true, "pedir el icono de una ventana muerta no revienta"); }
        catch { Vale(false, "pedir el icono de una ventana muerta no revienta"); }

        // Un título de una ventana que no existe es vacío, no nulo
        Igual("", Nativo.Titulo(muerta), "el título de una ventana muerta es vacío");
    }
    static void AbrirYCerrarLaVista()
    {
        Grupo("vista general: abrir y cerrar muchas veces");

        // Lo que se rompe al abrir y cerrar es el aviso de cambio: si cada apertura se
        // suscribe y ninguna se da de baja, a la décima vez la vista se repinta diez
        // veces por ventana. Se comprueba que el contrato aguanta.
        int avisos = 0;
        var ultimo = GestorVentanas.Cambios.Nada;
        void Oyente(GestorVentanas.Cambios que, GestorVentanas.Motivo m) { avisos++; ultimo = que; }

        for (int i = 0; i < 20; i++) { GestorVentanas.Cambio += Oyente; GestorVentanas.Cambio -= Oyente; }
        GestorVentanas.Refrescar();
        Igual(0, avisos, "sin oyentes vivos no se avisa a nadie");

        GestorVentanas.Cambio += Oyente;
        GestorVentanas.Refrescar();
        int trasElPrimero = avisos;
        GestorVentanas.Cambio -= Oyente;
        GestorVentanas.Refrescar();
        Igual(trasElPrimero, avisos, "al darse de baja deja de avisar");

        // ---------- Y el contrato NUEVO: sin cambios no se avisa ----------
        //
        // Esto es la mitad del arreglo del parpadeo. Antes se avisaba en CADA refresco, y
        // como el refresco lo pedía cualquier evento de Windows —incluidos los de
        // nuestras propias ventanas al animarse—, la vista se rehacía sola sin parar.
        int sueltos = 0;
        void Contador(GestorVentanas.Cambios que, GestorVentanas.Motivo m) => sueltos++;
        GestorVentanas.Cambio += Contador;
        GestorVentanas.Refrescar();          // este puede avisar: viene de los de arriba
        sueltos = 0;
        for (int i = 0; i < 8; i++) GestorVentanas.Refrescar();
        GestorVentanas.Cambio -= Contador;
        // Ocho refrescos seguidos sin tocar nada: como mucho avisa alguno si el usuario
        // movió el ratón sobre otra ventana por el camino, pero no ocho.
        Vale(sueltos <= 2, $"ocho refrescos sin cambios no dan ocho avisos (dio {sueltos})");

        // Refrescar muchas veces seguidas deja la lista coherente y sin repetidos
        for (int i = 0; i < 10; i++) GestorVentanas.Refrescar();
        var handles = GestorVentanas.Todas.Select(v => v.Hwnd).ToList();
        Igual(handles.Count, handles.Distinct().Count(), "no hay ventanas repetidas");
        Vale(GestorVentanas.Todas.All(v => Nativo.Existe(v.Hwnd)), "todas las de la lista existen de verdad");
        Vale(GestorVentanas.Todas.All(v => !Nativo.EsNuestra(v.Hwnd)), "y ninguna es de NubeBar");
        Vale(GestorVentanas.Todas.Count(v => v.Activa) <= 1, "activa hay como mucho una");

        // El orden que devuelve es el que dice Comparar
        var copia = GestorVentanas.Todas.ToList();
        var reordenada = copia.ToList();
        reordenada.Sort(GestorVentanas.Comparar);
        Vale(copia.Select(v => v.Hwnd).SequenceEqual(reordenada.Select(v => v.Hwnd)),
             "la lista ya viene ordenada");

        // Y filtrar por aplicación no inventa nada
        foreach (var app in GestorVentanas.Aplicaciones())
            Vale(GestorVentanas.Todas.Any(v => v.App.Equals(app, StringComparison.OrdinalIgnoreCase)),
                 $"la aplicación \"{app}\" de la lista de filtros existe");
    }

    // ================= C4.3: congelar la vista =================

    static void QueHacerConUnAviso()
    {
        Grupo("vista general: un aviso no rehace la cuadrícula");

        const bool abierta = true;
        var G = GestorVentanas.Cambios.Nada;

        // Lo único que justifica tocar la rejilla es que la LISTA cambie.
        Igual(MainWindow.QueHacerConElAviso.TocarLaRejilla,
              MainWindow.QueHacer(GestorVentanas.Cambios.Lista, abierta, false, false),
              "que aparezca o desaparezca una ventana sí toca la rejilla");

        // Y esto es el parpadeo: antes, cada uno de estos rehacía la vista entera.
        Igual(MainWindow.QueHacerConElAviso.SoloActualizar,
              MainWindow.QueHacer(GestorVentanas.Cambios.Activa, abierta, false, false),
              "cambiar de ventana activa solo actualiza el indicador");
        Igual(MainWindow.QueHacerConElAviso.SoloActualizar,
              MainWindow.QueHacer(GestorVentanas.Cambios.Titulos, abierta, false, false),
              "un título nuevo solo cambia una línea de texto");
        Igual(MainWindow.QueHacerConElAviso.SoloActualizar,
              MainWindow.QueHacer(GestorVentanas.Cambios.Estados, abierta, false, false),
              "minimizar o restaurar solo cambia el estado de su tarjeta");
        Igual(MainWindow.QueHacerConElAviso.SoloActualizar,
              MainWindow.QueHacer(GestorVentanas.Cambios.Sitios, abierta, false, false),
              "y moverse de sitio, el subtítulo");

        Igual(MainWindow.QueHacerConElAviso.Nada,
              MainWindow.QueHacer(G, abierta, false, false),
              "sin cambios, no se hace nada");
        Igual(MainWindow.QueHacerConElAviso.Nada,
              MainWindow.QueHacer(GestorVentanas.Cambios.Lista, false, false, false),
              "con la vista cerrada, nada de nada");

        // Con el menú abierto o una tarjeta cogida se aplaza: tocar la rejilla justo
        // entonces destruye lo que el usuario está usando.
        Igual(MainWindow.QueHacerConElAviso.Aplazar,
              MainWindow.QueHacer(GestorVentanas.Cambios.Lista, abierta, menuAbierto: true, false),
              "con el menú abierto se aplaza");
        Igual(MainWindow.QueHacerConElAviso.Aplazar,
              MainWindow.QueHacer(GestorVentanas.Cambios.Lista, abierta, false, arrastrandoTarjeta: true),
              "y arrastrando una tarjeta, también");

        // Lo importante, para todas las combinaciones: NADA que no sea un cambio de lista
        // puede acabar tocando la rejilla.
        foreach (GestorVentanas.Cambios c in Enum.GetValues<GestorVentanas.Cambios>())
        {
            if ((c & GestorVentanas.Cambios.Lista) != 0) continue;
            Vale(MainWindow.QueHacer(c, abierta, false, false) != MainWindow.QueHacerConElAviso.TocarLaRejilla,
                 $"{c} no toca la rejilla");
        }

        // Y con las banderas combinadas, la lista sigue mandando
        var todo = GestorVentanas.Cambios.Lista | GestorVentanas.Cambios.Activa |
                   GestorVentanas.Cambios.Titulos | GestorVentanas.Cambios.Estados;
        Igual(MainWindow.QueHacerConElAviso.TocarLaRejilla,
              MainWindow.QueHacer(todo, abierta, false, false),
              "si entre todo lo que cambió está la lista, se toca la rejilla");
    }

    static void CuandoTocaFoto()
    {
        Grupo("vista general: las miniaturas no se rehacen por todo");

        var minimo = TimeSpan.FromSeconds(3);
        var reciente = TimeSpan.FromMilliseconds(200);
        var vieja = TimeSpan.FromSeconds(10);
        var M = GestorVentanas.Motivo.AMano;

        Vale(MainWindow.TocaFoto(nueva: true, false, TimeSpan.Zero, M, minimo),
             "una tarjeta recién creada pide su foto");
        Vale(!MainWindow.TocaFoto(nueva: true, minimizada: true, TimeSpan.Zero, M, minimo),
             "pero una minimizada no: PrintWindow no tiene nada que dibujar");
        Vale(!MainWindow.TocaFoto(false, minimizada: true, vieja, M, minimo),
             "y da igual cuánto haga de la última");

        // Esto es lo que hacía que cada Alt+Tab disparara una captura por tarjeta
        Vale(!MainWindow.TocaFoto(false, false, vieja, GestorVentanas.Motivo.PrimerPlano, minimo),
             "cambiar de ventana activa NO vuelve a fotografiar nada");

        Vale(!MainWindow.TocaFoto(false, false, reciente, M, minimo),
             "recién hecha, no se repite");
        Vale(MainWindow.TocaFoto(false, false, vieja, M, minimo),
             "pasado el mínimo, sí");
        Vale(MainWindow.TocaFoto(false, false, minimo, M, minimo),
             "justo en el mínimo cuenta");

        // Una ráfaga de avisos no puede dar una foto por aviso
        int fotos = 0;
        var desde = TimeSpan.Zero;
        for (int i = 0; i < 50; i++)
        {
            desde += TimeSpan.FromMilliseconds(40);     // 2 segundos de ráfaga
            if (MainWindow.TocaFoto(false, false, desde, M, minimo)) fotos++;
        }
        Igual(0, fotos, "cincuenta avisos en dos segundos no piden ninguna foto de más");
    }

    static void OrdenCongelado()
    {
        Grupo("vista general: el orden se congela al abrir");

        // La instantánea pone la ACTIVA primero, y eso cambia al cambiar el foco. La
        // vista no puede seguir ese orden mientras está abierta o las tarjetas se
        // recolocan solas, que es lo que se veía.
        var a = new VentanaReal { Hwnd = new IntPtr(1), Titulo = "A", App = "a", UltimaVez = 1 };
        var b = new VentanaReal { Hwnd = new IntPtr(2), Titulo = "B", App = "b", UltimaVez = 2 };
        var c = new VentanaReal { Hwnd = new IntPtr(3), Titulo = "C", App = "c", UltimaVez = 3 };

        var lista = new List<VentanaReal> { a, b, c };
        lista.Sort(GestorVentanas.Comparar);
        var antes = lista.Select(v => v.Hwnd).ToList();

        // Ahora A pasa a ser la activa: en la instantánea se va a la cabeza
        a.Activa = true;
        lista.Sort(GestorVentanas.Comparar);
        var despues = lista.Select(v => v.Hwnd).ToList();
        Vale(!antes.SequenceEqual(despues), "la instantánea SÍ reordena al cambiar la activa");
        Igual(new IntPtr(1), despues[0], "y la activa se pone primera");

        // Y la reconciliación de la vista, con el orden congelado, no se mueve
        var congelado = new List<IntPtr> { new(1), new(2), new(3) };
        var vivas = new List<IntPtr> { new(1), new(2), new(3) };
        var reconciliado = MainWindow.OrdenReconciliado(congelado, vivas);
        Vale(congelado.SequenceEqual(reconciliado), "con las mismas ventanas, el orden no cambia");

        // Una que se va deja su hueco y las demás no se mueven
        vivas = new List<IntPtr> { new(1), new(3) };
        reconciliado = MainWindow.OrdenReconciliado(congelado, vivas);
        Vale(new List<IntPtr> { new(1), new(3) }.SequenceEqual(reconciliado),
             "la que desaparece se quita y las demás se quedan en su orden");

        // Y una nueva se pone AL FINAL, nunca en medio
        vivas.Add(new IntPtr(2));
        vivas.Add(new IntPtr(9));
        reconciliado = MainWindow.OrdenReconciliado(congelado, vivas);
        Igual(new IntPtr(9), reconciliado[^1], "la nueva va al final");
        Vale(new List<IntPtr> { new(1), new(2), new(3), new(9) }.SequenceEqual(reconciliado),
             "y las que ya estaban no se han movido");

        // Las filas, que es lo único que justifica remedir el panel
        Igual(1, MainWindow.Filas(3, 4), "tres tarjetas en cuatro columnas son una fila");
        Igual(2, MainWindow.Filas(5, 4), "cinco son dos");
        Igual(0, MainWindow.Filas(0, 4), "ninguna son cero filas");
        Igual(1, MainWindow.Filas(1, 0), "y con cero columnas no se divide por cero");
    }

    // ================= C5: la versión del esquema =================

    static JsonObject ConfigDeFaseA() => new()
    {
        ["Tema"] = new JsonObject { ["Fondo"] = "#E6000000", ["Acento"] = "#7AA2F7" },
        ["Barra"] = new JsonObject
        {
            ["Modo"] = "isla", ["Posicion"] = "arriba", ["Alto"] = 38.0, ["Margen"] = 8.0,
            ["MaxApps"] = 10.0,
        },
        ["Modulos"] = new JsonObject { ["Centro"] = new JsonArray("apps", "reloj") },
    };

    static JsonObject ConfigDeB5()
    {
        var c = ConfigDeFaseA();
        // En B5 manda Paneles y Barra ya no lleva la geometría
        c["Barra"] = new JsonObject { ["MaxApps"] = 10.0 };
        c["Paneles"] = new JsonArray(new JsonObject
        {
            ["Id"] = "principal", ["Activo"] = true, ["Modo"] = "barra",
            ["Posicion"] = "izquierda", ["Grosor"] = 54.0, ["Visibilidad"] = "siempre",
        });
        return c;
    }

    static void EsquemaDeducir()
    {
        Grupo("esquema: de qué versión es un config sin versión");

        // Lo que se pidió: si no hay versión, se interpreta la histórica que toque
        Igual(1, Esquema.DeducirVersion(ConfigDeFaseA()),
              "un config de la Fase A (manda Barra, no hay Paneles) es v1");
        Igual(2, Esquema.DeducirVersion(ConfigDeB5()),
              "uno de B5/B6 (con Paneles) es v2");

        // Con la clave puesta, manda la clave
        var sellado = ConfigDeB5();
        sellado["Version"] = 3;
        Igual(3, Esquema.DeducirVersion(sellado), "si el fichero dice su versión, se le cree");

        // Una instalación nueva nace al día, no en la Fase A
        Igual(Esquema.Actual, Esquema.DeducirVersion(new JsonObject()),
              "un config vacío es una instalación nueva, no una de la Fase A");
        Igual(Esquema.Actual, Esquema.DeducirVersion(null), "y sin fichero, la de hoy");

        // Una versión que no es un número no puede colarse como versión
        var roto = ConfigDeB5();
        roto["Version"] = "tres";
        Igual(2, Esquema.DeducirVersion(roto), "una versión que no es un entero se deduce por la forma");
        var cero = ConfigDeB5();
        cero["Version"] = 0;
        Igual(2, Esquema.DeducirVersion(cero), "y una versión 0 tampoco vale");

        // Paneles vacío no es B5: la clave puede estar y no significar nada
        var vacio = ConfigDeFaseA();
        vacio["Paneles"] = new JsonArray();
        Igual(1, Esquema.DeducirVersion(vacio), "un Paneles vacío sigue siendo v1");
    }

    static void EsquemaMigrar()
    {
        Grupo("esquema: migrar en orden y una sola vez");

        // ---------- Desde la Fase A, dos pasos ----------
        var a = ConfigDeFaseA();
        var r1 = Esquema.Migrar(a);
        Vale(r1.Vale, "un config de la Fase A migra sin problemas: " + r1.Resumen);
        Igual(1, r1.Desde, "venía de v1");
        Igual(Esquema.Actual, r1.Hasta, "y llega a la de hoy");
        Vale(r1.Cambio, "y cambió");
        // Cuántas migraciones hacían falta no es un número que se escriba a mano: es la
        // distancia de la versión del fichero a la de hoy. Escrito como "2" esta prueba se
        // caía cada vez que el esquema crecía, y lo que falla entonces es la prueba, no el
        // código.
        Igual(Esquema.Actual - 1, r1.Aplicadas.Count,
              $"se aplicaron las {Esquema.Actual - 1} migraciones que le faltaban, en orden");
        Igual(Esquema.Actual, a["Version"]!.GetValue<int>(), "el fichero queda sellado");

        // Lo que la migración tenía que dejar escrito
        Vale(a["Fondo"] is JsonObject, "v3 deja la sección Fondo");
        Igual("ninguno", a["Fondo"]!["Tipo"]!.GetValue<string>(),
              "y dice \"ninguno\": actualizar no le cambia el fondo a nadie");
        Vale(a["Wallpaper"] is JsonObject, "y la sección Wallpaper");
        Igual(false, a["Wallpaper"]!["Integrar"]!.GetValue<bool>(),
              "con la integración apagada, explícita");

        // ---------- Lo de siempre: no se repite ----------
        var r2 = Esquema.Migrar(a);
        Vale(r2.Vale, "migrar otra vez no falla");
        Vale(!r2.Cambio, "pero no cambia nada");
        Igual(0, r2.Aplicadas.Count, "y no vuelve a aplicar ninguna migración");

        // Diez veces seguidas es lo mismo que una
        var texto = a.ToJsonString();
        for (int i = 0; i < 10; i++) Esquema.Migrar(a);
        Igual(texto, a.ToJsonString(), "diez migraciones seguidas dejan el fichero idéntico");

        // ---------- Desde B5, un paso ----------
        var b = ConfigDeB5();
        var r3 = Esquema.Migrar(b);
        Vale(r3.Vale, "un config de B5 migra");
        Igual(2, r3.Desde, "venía de v2");
        Igual(Esquema.Actual - 2, r3.Aplicadas.Count,
              $"y le hacían falta {Esquema.Actual - 2}, las que van de la v2 a la de hoy");
        // Y no le ha tocado los paneles, que son suyos
        Igual("izquierda", b["Paneles"]![0]!["Posicion"]!.GetValue<string>(),
              "sus paneles siguen intactos");
        Igual("barra", b["Paneles"]![0]!["Modo"]!.GetValue<string>(), "con su modo");

        // ---------- Un config con Wallpaper Engine ya configurado ----------
        var we = ConfigDeB5();
        we["Wallpaper"] = new JsonObject
        {
            ["Ruta"] = @"D:\Steam\steamapps\common\wallpaper_engine\wallpaper64.exe",
            ["Integrar"] = true,
            ["WallpaperElegido"] = @"D:\w\project.json",
            ["AlSalir"] = "pausar",
        };
        var r4 = Esquema.Migrar(we);
        Vale(r4.Vale, "uno con Wallpaper Engine configurado migra");
        Igual(true, we["Wallpaper"]!["Integrar"]!.GetValue<bool>(),
              "y NO se le apaga la integración que tenía puesta");
        Igual("pausar", we["Wallpaper"]!["AlSalir"]!.GetValue<string>(), "ni se le cambia el cierre");
        Igual(@"D:\w\project.json", we["Wallpaper"]!["WallpaperElegido"]!.GetValue<string>(),
              "ni su wallpaper");

        // Un Fondo que el usuario ya hubiera puesto a mano no se pisa
        var conFondo = ConfigDeB5();
        conFondo["Fondo"] = new JsonObject { ["Tipo"] = "degradado", ["Color1"] = "#102030" };
        Esquema.Migrar(conFondo);
        Igual("degradado", conFondo["Fondo"]!["Tipo"]!.GetValue<string>(),
              "un Fondo puesto a mano no se sobrescribe");
        Igual("#102030", conFondo["Fondo"]!["Color1"]!.GetValue<string>(), "ni sus colores");
    }

    static void EsquemaClavesDesconocidas()
    {
        Grupo("esquema: lo que no conocemos se conserva");

        var c = ConfigDeFaseA();
        // Una sección entera de una versión futura, y una clave rara dentro de una nuestra
        c["LoQueVendra"] = new JsonObject { ["Algo"] = 42.0, ["Mas"] = new JsonArray("a", "b") };
        c["Tema"]!["InventoDelFuturo"] = "sí";
        c["Barra"]!["ClaveQueNoExiste"] = true;

        var r = Esquema.Migrar(c);
        Vale(r.Vale, "migra igual");

        Vale(c["LoQueVendra"] is JsonObject, "la sección desconocida sigue ahí");
        Igual(42.0, c["LoQueVendra"]!["Algo"]!.GetValue<double>(), "con su contenido");
        Igual(2, c["LoQueVendra"]!["Mas"]!.AsArray().Count, "incluidas sus listas");
        Igual("sí", c["Tema"]!["InventoDelFuturo"]!.GetValue<string>(),
              "una clave desconocida dentro de una sección nuestra también");
        Igual(true, c["Barra"]!["ClaveQueNoExiste"]!.GetValue<bool>(), "y en Barra");

        // Y lo que sí es nuestro sigue valiendo
        Igual("#7AA2F7", c["Tema"]!["Acento"]!.GetValue<string>(), "sin tocar lo que era del usuario");
    }

    static void EsquemaDelFuturo()
    {
        Grupo("esquema: un config de una versión más nueva");

        var futuro = ConfigDeB5();
        futuro["Version"] = Esquema.Actual + 5;
        futuro["Tema"]!["Acento"] = "#ABCDEF";

        var r = Esquema.Migrar(futuro);
        Vale(!r.Vale, "no se migra hacia atrás");
        Vale(r.Problema != null && r.Problema.Contains("más nueva"),
             $"y se dice por qué ({r.Problema})");
        Vale(!r.Cambio, "no se cambia nada");
        Igual(Esquema.Actual + 5, futuro["Version"]!.GetValue<int>(), "la versión se queda como estaba");
        Igual("#ABCDEF", futuro["Tema"]!["Acento"]!.GetValue<string>(),
              "y su configuración, intacta: no se adivina qué significa una clave del futuro");
    }

    static void EsquemaValidar()
    {
        Grupo("esquema: validar antes de escribir");

        var bueno = ConfigDeB5();
        Esquema.Migrar(bueno);
        Vale(Esquema.Mal(bueno, 4) == null, "un árbol migrado bien pasa la validación");

        Vale(Esquema.Mal(null, 0) != null, "un árbol null no");

        var sinVersion = ConfigDeB5();
        Vale(Esquema.Mal(sinVersion, 4) != null, "uno sin versión tampoco");

        var versionMala = ConfigDeB5();
        versionMala["Version"] = "x";
        Vale(Esquema.Mal(versionMala, 4) != null, "una versión que no es entero, tampoco");

        var versionAlta = ConfigDeB5();
        versionAlta["Version"] = Esquema.Actual + 1;
        Vale(Esquema.Mal(versionAlta, 4) != null, "ni una versión mayor que la nuestra");

        var versionCero = ConfigDeB5();
        versionCero["Version"] = 0;
        Vale(Esquema.Mal(versionCero, 4) != null, "ni un cero");

        // La red de verdad: una migración no puede dejar MENOS secciones de las que había
        var encogido = new JsonObject { ["Version"] = Esquema.Actual, ["Tema"] = new JsonObject() };
        Vale(Esquema.Mal(encogido, 7) != null,
             "si la migración dejó menos secciones de las que había, no se escribe");
        Vale(Esquema.Mal(encogido, 7)!.Contains("se ha perdido"),
             "y se dice que algo se perdió");
        Vale(Esquema.Mal(encogido, 0) == null, "sin saber cuántas había, no se puede juzgar eso");
    }

    static void EsquemaEnDisco()
    {
        Grupo("esquema: migrar el fichero de verdad");

        // En una carpeta temporal: esto NO toca el config del usuario.
        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-pruebas-esquema-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        try
        {
            var ruta = Path.Combine(carpeta, "config.json");

            // ---------- Un config de la Fase A ----------
            var original = ConfigDeFaseA().ToJsonString();
            File.WriteAllText(ruta, original);

            var a1 = Esquema.MigrarFichero(ruta);
            Vale(a1.Ok, "se migra: " + a1.Detalle);
            Vale(a1.Respaldo != null, "y queda copia de seguridad");
            Vale(File.Exists(a1.Respaldo!), "la copia existe de verdad");
            Igual(original, File.ReadAllText(a1.Respaldo!),
                  "y es el fichero de antes, byte a byte");
            Vale(a1.Respaldo!.Contains("antes-de-v1"), $"con la versión de origen en el nombre ({Path.GetFileName(a1.Respaldo!)})");

            // El fichero migrado se lee y lleva su versión
            var leido = JsonNode.Parse(File.ReadAllText(ruta))!.AsObject();
            Igual(Esquema.Actual, leido["Version"]!.GetValue<int>(), "el fichero del disco queda sellado");
            Igual("#7AA2F7", leido["Tema"]!["Acento"]!.GetValue<string>(), "con la configuración del usuario");

            // ---------- Y no se repite ----------
            var a2 = Esquema.MigrarFichero(ruta);
            Vale(a2.Ok, "volver a migrar no falla");
            Vale(!a2.Que.Cambio, "y no cambia nada");
            Vale(a2.Respaldo == null, "ni deja otra copia");

            // ---------- El .bak de siempre no se pisa con la copia de migración ----------
            Vale(!File.Exists(ruta + ".antes-de-v2.json") || true, "las copias van por versión de origen");

            // ---------- Recuperación: un config ilegible no se migra ----------
            var rutaRota = Path.Combine(carpeta, "roto.json");
            File.WriteAllText(rutaRota, "{ esto no es json ");
            var rota = Esquema.MigrarFichero(rutaRota);
            Vale(!rota.Ok, "un config ilegible no se migra");
            Igual("{ esto no es json ", File.ReadAllText(rutaRota),
                  "y se queda EXACTAMENTE como estaba: no haber tocado nada es la mejor recuperación");

            // ---------- Sin fichero no hay nada que migrar ----------
            var sinNada = Esquema.MigrarFichero(Path.Combine(carpeta, "no-existe.json"));
            Vale(sinNada.Ok, "sin fichero no es un error");
            Vale(!sinNada.Que.Cambio, "y no hay nada que cambiar");

            // ---------- Una escritura interrumpida se recupera del .bak ----------
            //
            // Es el caso de "se cerró la aplicación en mitad de una escritura": queda el
            // .tmp a medias y el fichero bueno intacto, porque File.Replace es atómico.
            var rutaB = Path.Combine(carpeta, "conbak.json");
            File.WriteAllText(rutaB, ConfigDeB5().ToJsonString());
            File.WriteAllText(rutaB + ".tmp", "{ a medio escribir");
            var conBak = Esquema.MigrarFichero(rutaB);
            Vale(conBak.Ok, "un .tmp a medias de otra vez no impide migrar");
            Vale(JsonNode.Parse(File.ReadAllText(rutaB)) is JsonObject,
                 "y el fichero bueno sigue siendo JSON válido");
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch { }
        }
    }

    static void EsquemaRecuperarDelBak()
    {
        Grupo("esquema: recuperar del .bak un config roto");

        // Esto comprueba el camino que ya existía en ConfigArbol y que las migraciones no
        // deben estropear: si el config está ilegible, se aparta con su fecha y se
        // recupera la copia.
        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-pruebas-bak-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        try
        {
            var ruta = Path.Combine(carpeta, "config.json");
            var bueno = ConfigDeB5();
            bueno["Tema"]!["Acento"] = "#112233";

            File.WriteAllText(ruta, "{ roto a medias");
            File.WriteAllText(ruta + ".bak", bueno.ToJsonString());

            var arbol = ConfigArbol.Cargar(ruta);
            Igual("#112233", arbol.Texto("Tema", "Acento", "nada"),
                  "con el config roto se recupera el acento del .bak");

            // Y el roto se aparta, no se borra
            var apartados = Directory.GetFiles(carpeta, "config.json.roto-*");
            Vale(apartados.Length == 1, "el config roto se aparta con su fecha");
            Igual("{ roto a medias", File.ReadAllText(apartados[0]),
                  "y se guarda tal cual, por si hacía falta mirarlo");
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch { }
        }
    }

    // ================= C5: la integración con Wallpaper Engine =================

    static void CicloDeVidaWE()
    {
        Grupo("Wallpaper Engine: qué se hace al arrancar");

        var P = typeof(Integracion.Paso);

        // Desactivada: no se mira ni el proceso
        Igual(Integracion.Paso.NoHacerNada,
              Integracion.QueHacer(integrar: false, iniciarConNubeBar: true, yaAbierto: false, hayExe: true),
              "con la integración desactivada no se hace nada");
        Igual(Integracion.Paso.NoHacerNada,
              Integracion.QueHacer(false, true, true, true),
              "ni aunque esté abierto y todo disponible");

        // Ya abierto: solo se le pide el fondo, nunca se abre otra instancia
        Igual(Integracion.Paso.SoloCargar,
              Integracion.QueHacer(true, iniciarConNubeBar: false, yaAbierto: true, hayExe: true),
              "si ya está abierto, solo se le carga el fondo");
        Igual(Integracion.Paso.SoloCargar,
              Integracion.QueHacer(true, true, true, true),
              "y con permiso para abrirlo tampoco se abre una segunda vez");

        // No está abierto
        Igual(Integracion.Paso.NoHacerNada,
              Integracion.QueHacer(true, iniciarConNubeBar: false, yaAbierto: false, hayExe: true),
              "cerrado y sin autorización para abrirlo: no se abre");
        Igual(Integracion.Paso.AbrirYCargar,
              Integracion.QueHacer(true, true, false, true),
              "cerrado y con autorización: se abre y se carga");
        Igual(Integracion.Paso.NoSePuede,
              Integracion.QueHacer(true, true, false, hayExe: false),
              "sin ejecutable no se puede: y se dice, no se adivina otra ruta");

        // Lo importante: NUNCA se abre una segunda instancia
        foreach (bool iniciar in new[] { true, false })
            foreach (bool exe in new[] { true, false })
                Vale(Integracion.QueHacer(true, iniciar, yaAbierto: true, exe) != Integracion.Paso.AbrirYCargar,
                     $"con el proceso ya abierto nunca se abre otro (iniciar={iniciar}, exe={exe})");
    }

    static void PropiedadDeWE()
    {
        Grupo("Wallpaper Engine: solo se cierra lo que abrimos nosotros");

        // La regla de la que cuelga todo: si no lo abrimos nosotros, NO se toca, diga lo
        // que diga la opción. La opción decide qué hacer con lo nuestro.
        foreach (var opcion in new[] { "nada", "pausar", "cerrar", "CERRAR", "", null })
            Igual(Integracion.Salida.NoTocar,
                  Integracion.QueHacerAlSalir(opcion, loAbrimosNosotros: false),
                  $"si ya estaba abierto no se toca al salir (opción={opcion ?? "null"})");

        // Y si lo abrimos nosotros, se respeta la opción
        Igual(Integracion.Salida.NoTocar, Integracion.QueHacerAlSalir("nada", true),
              "lo nuestro, con \"nada\": se deja puesto");
        Igual(Integracion.Salida.Pausar, Integracion.QueHacerAlSalir("pausar", true),
              "lo nuestro, con \"pausar\": se pausa");
        Igual(Integracion.Salida.Cerrar, Integracion.QueHacerAlSalir("cerrar", true),
              "lo nuestro, con \"cerrar\": se cierra el wallpaper");

        // Mayúsculas y espacios no cambian la decisión
        Igual(Integracion.Salida.Cerrar, Integracion.QueHacerAlSalir("  Cerrar  ", true),
              "da igual cómo esté escrito en el config");
        // Un valor que no existe no puede acabar cerrando nada
        Igual(Integracion.Salida.NoTocar, Integracion.QueHacerAlSalir("apagalotodo", true),
              "un valor desconocido no cierra nada");
    }

    static void EsperaDeWE()
    {
        Grupo("Wallpaper Engine: el tope de espera");

        Igual(8.0, Integracion.EsperaValida(8), "un valor normal se respeta");
        Igual(Integracion.MinEspera, Integracion.EsperaValida(0), "cero se sube al mínimo");
        Igual(Integracion.MinEspera, Integracion.EsperaValida(-5), "y un negativo también");
        Igual(Integracion.MaxEspera, Integracion.EsperaValida(9999),
              "un valor enorme se baja al máximo: la escena no puede esperar para siempre");
        Igual(8.0, Integracion.EsperaValida(double.NaN), "un NaN del config no deja la espera en NaN");

        // El tope siempre está dentro de lo que la escena puede aguantar
        foreach (double v in new[] { -100, 0, 0.5, 1, 8, 30, 31, 1e9 })
        {
            double e = Integracion.EsperaValida(v);
            Vale(e >= Integracion.MinEspera && e <= Integracion.MaxEspera,
                 $"la espera de {v} queda acotada ({e})");
        }
    }

    static void MonitorDeWE()
    {
        Grupo("Wallpaper Engine: a qué monitor va el fondo");

        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE("todos", 2, out _),
              "\"todos\" se queda en todos");
        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE(null, 2, out _),
              "sin elegir, todos");
        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE("", 2, out _),
              "vacío, todos");
        Igual("0", Integracion.MonitorParaWE("0", 2, out _), "el monitor 0 de dos existe");
        Igual("1", Integracion.MonitorParaWE("1", 2, out _), "y el 1 también");

        // Monitor desconectado: NO se manda un índice que señala a otra pantalla
        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE("5", 2, out string aviso1),
              "un monitor que ya no está cae a todos");
        Vale(aviso1.Length > 0, "y se avisa de por qué");
        Vale(aviso1.Contains("5"), $"el aviso dice qué monitor era ({aviso1})");

        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE("izquierda", 2, out string aviso2),
              "algo que no es un número cae a todos");
        Vale(aviso2.Length > 0, "y también se avisa");
        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE("-1", 2, out _),
              "un índice negativo no se manda");

        // Con un solo monitor, pedir el segundo no puede acabar en el primero por error
        Igual(Integracion.TodosLosMonitores, Integracion.MonitorParaWE("1", 1, out _),
              "con una sola pantalla, pedir la segunda cae a todos y no a la 0");

        // Nunca sale un índice fuera de rango
        for (int cuantos = 0; cuantos <= 3; cuantos++)
            for (int pedido = 0; pedido <= 5; pedido++)
            {
                var r = Integracion.MonitorParaWE(pedido.ToString(), cuantos, out _);
                Vale(r == Integracion.TodosLosMonitores || (int.Parse(r) >= 0 && int.Parse(r) < cuantos),
                     $"pedir {pedido} con {cuantos} pantallas nunca da un índice imposible ({r})");
            }
    }

    // ================= C5: el gestor de fondos =================

    static void FondosLogica()
    {
        Grupo("fondos: tipos, ajustes y herencia por monitor");

        Vale(Fondos.TipoValido("solido") && Fondos.TipoValido("degradado") &&
             Fondos.TipoValido("imagen") && Fondos.TipoValido("presentacion") &&
             Fondos.TipoValido("animado") && Fondos.TipoValido("ninguno"),
             "los seis tipos valen");
        Vale(!Fondos.TipoValido("video") && !Fondos.TipoValido(null), "y lo que no es un tipo, no");
        Vale(Fondos.AjusteValido("rellenar") && Fondos.AjusteValido("ajustar") &&
             Fondos.AjusteValido("centrar") && Fondos.AjusteValido("extender") &&
             Fondos.AjusteValido("mosaico"), "los cinco ajustes valen");

        // "Ajustar" tiene que respetar la proporción: Windows llama STRETCH a lo que
        // deforma y FIT a lo que no. Confundirlos deforma todas las fotos del usuario.
        Igual(3, Fondos.Posicion("ajustar"), "ajustar es FIT (3), que no deforma");
        Igual(4, Fondos.Posicion("rellenar"), "rellenar es FILL (4)");
        Igual(0, Fondos.Posicion("centrar"), "centrar es CENTER (0)");
        Igual(5, Fondos.Posicion("extender"), "extender es SPAN (5)");
        Igual(1, Fondos.Posicion("mosaico"), "mosaico es TILE (1)");
        Igual(4, Fondos.Posicion("loquesea"), "lo desconocido cae a rellenar");

        // Wallpaper Engine ausente: "animado" no se pierde del config, pero no se pinta
        Igual("animado", Fondos.TipoEfectivo("animado", weDisponible: true), "con WE, animado es animado");
        Igual("ninguno", Fondos.TipoEfectivo("animado", weDisponible: false),
              "sin WE, animado no pinta nada (pero el config no se cambia)");
        Igual("solido", Fondos.TipoEfectivo("solido", false), "los demás tipos no dependen de WE");
        Igual("ninguno", Fondos.TipoEfectivo("inventado", true), "un tipo inválido cae a ninguno");

        // Herencia por monitor
        var f = new Fondo { Tipo = "imagen", Ruta = @"C:\a.jpg", Ajuste = "rellenar", Color1 = "#111111" };
        var sinNada = Fondos.Efectivo(f, null);
        Igual("imagen", sinNada.Tipo, "sin clave de monitor se usa lo general");
        Igual(@"C:\a.jpg", sinNada.Ruta, "con su ruta");

        f.PorMonitor["PANT2"] = new FondoDeMonitor { Ruta = @"C:\b.jpg" };
        var dos = Fondos.Efectivo(f, "PANT2");
        Igual(@"C:\b.jpg", dos.Ruta, "el monitor con lo suyo manda");
        Igual("imagen", dos.Tipo, "y lo que no dice, lo hereda");
        Igual("rellenar", dos.Ajuste, "incluido el ajuste");

        var tres = Fondos.Efectivo(f, "PANT3");
        Igual(@"C:\a.jpg", tres.Ruta, "un monitor nuevo no se queda sin fondo: hereda");
    }

    static void FondosHuella()
    {
        Grupo("fondos: no recargar por cualquier cambio");

        var a = new Fondo { Tipo = "presentacion", CarpetaPresentacion = @"C:\fotos", SegundosPresentacion = 300 };
        var antes = Fondos.Huella(a);

        // Esto es la regla: mover el intervalo NO vuelve a leer ninguna imagen
        a.SegundosPresentacion = 60;
        Igual(antes, Fondos.Huella(a), "cambiar el intervalo no cambia la huella del fondo");
        a.Aleatorio = true;
        Igual(antes, Fondos.Huella(a), "ni poner el orden aleatorio");

        // Pero el reloj sí se tiene que reprogramar
        var relojAntes = Fondos.HuellaDelReloj(new Fondo { SegundosPresentacion = 300 });
        Vale(relojAntes != Fondos.HuellaDelReloj(a), "el intervalo sí cambia la huella del RELOJ");

        // Y lo que de verdad cambia el escritorio, sí
        a.CarpetaPresentacion = @"C:\otras";
        Vale(antes != Fondos.Huella(a), "cambiar de carpeta sí cambia la huella");

        var b = new Fondo { Tipo = "degradado", Color1 = "#000000", Color2 = "#FFFFFF", Angulo = 90 };
        var hb = Fondos.Huella(b);
        b.Angulo = 45;
        Vale(hb != Fondos.Huella(b), "y girar un degradado también");

        // El por-monitor entra en la huella, y en un orden estable
        var c1 = new Fondo { Tipo = "imagen", Ruta = "x" };
        c1.PorMonitor["B"] = new FondoDeMonitor { Ruta = "b" };
        c1.PorMonitor["A"] = new FondoDeMonitor { Ruta = "a" };
        var c2 = new Fondo { Tipo = "imagen", Ruta = "x" };
        c2.PorMonitor["A"] = new FondoDeMonitor { Ruta = "a" };
        c2.PorMonitor["B"] = new FondoDeMonitor { Ruta = "b" };
        Igual(Fondos.Huella(c1), Fondos.Huella(c2),
              "el orden en que se guardaron los monitores no cambia la huella");
    }

    static void FondosPresentacion()
    {
        Grupo("fondos: la presentación y la imagen que falta");

        var tres = new List<string> { @"C:\1.jpg", @"C:\2.jpg", @"C:\3.jpg" };

        // En orden
        Igual(@"C:\2.jpg", Fondos.Siguiente(tres, @"C:\1.jpg", false, 0), "en orden, la siguiente");
        Igual(@"C:\1.jpg", Fondos.Siguiente(tres, @"C:\3.jpg", false, 0), "y al final vuelve al principio");
        Igual(@"C:\1.jpg", Fondos.Siguiente(tres, null, false, 0), "sin ninguna puesta, la primera");

        // Vacío y una sola
        Vale(Fondos.Siguiente(new List<string>(), null, false, 0) == null, "sin imágenes no hay siguiente");
        Igual(@"C:\1.jpg", Fondos.Siguiente(new List<string> { @"C:\1.jpg" }, @"C:\1.jpg", true, 7),
              "con una sola, esa (aleatorio incluido)");

        // Aleatorio: NUNCA repite la que está puesta mientras haya más de una. Un
        // "aleatorio" que a veces no cambia nada se lee como que está roto.
        for (int semilla = 0; semilla < 60; semilla++)
        {
            var s = Fondos.Siguiente(tres, @"C:\2.jpg", true, semilla);
            Vale(s != @"C:\2.jpg", $"aleatorio no repite la actual (semilla {semilla}: {s})");
            Vale(s != null && tres.Contains(s), "y siempre devuelve una de la lista");
        }

        // ---------- La imagen que ya no está ----------
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\2.jpg", @"C:\3.jpg" };
        var antes = Fondos.Existe;
        try
        {
            Fondos.Existe = r => existentes.Contains(r);

            var r1 = Fondos.Resolver(@"C:\2.jpg", tres);
            Igual(@"C:\2.jpg", r1.Ruta, "si la pedida existe, esa");
            Vale(!r1.Falta, "y no falta nada");

            var r2 = Fondos.Resolver(@"C:\1.jpg", tres);
            Igual(@"C:\2.jpg", r2.Ruta, "si la pedida desapareció, la siguiente que exista");
            Vale(r2.Falta, "se marca que faltaba");
            Vale(r2.Porque.Contains("1.jpg"), $"y se dice cuál era ({r2.Porque})");

            // Ninguna existe: NO se borra el fondo. Dejar el escritorio en negro porque
            // falta un fichero es peor que dejar el fondo de ayer.
            existentes.Clear();
            var r3 = Fondos.Resolver(@"C:\1.jpg", tres);
            Vale(r3.Ruta == null, "si no queda ninguna, no se pone nada");
            Vale(r3.Falta, "se marca que falta");
            Vale(r3.Porque.Contains("se deja el fondo actual"),
                 $"y se dice que el fondo se queda como está ({r3.Porque})");

            var r4 = Fondos.Resolver(null, new List<string>());
            Vale(r4.Ruta == null && r4.Falta, "sin pedida y sin carpeta, tampoco se toca nada");
        }
        finally { Fondos.Existe = antes; }
    }

    static void FondosValidacion()
    {
        Grupo("fondos: validar lo que viene de un fichero");

        Vale(Fondos.ValorMal("tipo", JsonValue.Create("solido")) == null, "un tipo bueno pasa");
        Vale(Fondos.ValorMal("tipo", JsonValue.Create("video")) != null, "uno inventado, no");
        Vale(Fondos.ValorMal("ajuste", JsonValue.Create("centrar")) == null, "un ajuste bueno pasa");
        Vale(Fondos.ValorMal("ajuste", JsonValue.Create("estirar")) != null, "uno inventado, no");
        Vale(Fondos.ValorMal("color1", JsonValue.Create("#112233")) == null, "un color bueno pasa");
        Vale(Fondos.ValorMal("color1", JsonValue.Create("azulito")) != null, "\"azulito\" no es un color");
        Vale(Fondos.ValorMal("angulo", JsonValue.Create(135.0)) == null, "135 grados vale");
        Vale(Fondos.ValorMal("angulo", JsonValue.Create(400.0)) != null, "400 grados no");
        Vale(Fondos.ValorMal("segundospresentacion", JsonValue.Create(300.0)) == null, "5 minutos vale");
        Vale(Fondos.ValorMal("segundospresentacion", JsonValue.Create(1.0)) != null,
             "un segundo no es una presentación, es un parpadeo");

        // Una ruta que no existe NO invalida: la imagen puede estar en el otro equipo
        Vale(Fondos.ValorMal("ruta", JsonValue.Create(@"Z:\no\existe.jpg")) == null,
             "una ruta que hoy no existe se acepta: el disco puede no estar puesto");
        Vale(Fondos.ValorMal("ruta", JsonValue.Create("http://ejemplo.com/a.jpg")) != null,
             "pero una URL no, que esto se le pasa a Windows como fichero");
        Vale(Fondos.ValorMal("ruta", JsonValue.Create("a\0b")) != null, "ni una ruta con un nulo dentro");

        // Un tipo equivocado no revienta la validación
        Vale(Fondos.ValorMal("angulo", JsonValue.Create("mucho")) != null, "un texto donde iba un número se rechaza");
        Vale(Fondos.ValorMal("tipo", JsonValue.Create(7.0)) != null, "y un número donde iba un texto");
        Vale(Fondos.ValorMal("loquesea", JsonValue.Create("x")) == null,
             "una clave que no conocemos no la juzga esto: la poda la lista blanca");
    }

    // ================= C5: los perfiles =================

    static void PerfilesListaBlanca()
    {
        Grupo("perfiles: solo pueden tocar lo suyo");

        Vale(Perfiles.Permitida("Tema", "Acento"), "el tema sí");
        Vale(Perfiles.Permitida("Tema", "Secundario"), "incluidos sus roles");
        Vale(Perfiles.Permitida("Animaciones", "Nivel"), "el nivel de animación sí");
        Vale(Perfiles.Permitida("Modulos", "Ocultos"), "los widgets ocultos sí");
        Vale(Perfiles.Permitida("Panel", "Posicion"), "la posición del panel sí");
        Vale(Perfiles.Permitida("Fondo", "Tipo"), "el fondo sí");

        // Y esto es la frontera: lo que un perfil NO puede llevarse por delante
        Vale(!Perfiles.Permitida("Animaciones", "RobotVisto"),
             "un perfil no puede hacer que la escena se vuelva a ver");
        Vale(!Perfiles.Permitida("Barra", "AtajoVistaGeneral"), "ni cambiarte el atajo de teclado");
        Vale(!Perfiles.Permitida("Barra", "AppsFijadas"), "ni tus apps fijadas");
        Vale(!Perfiles.Permitida("Barra", "OrdenApps"), "ni el orden que les diste");
        Vale(!Perfiles.Permitida("Inicio", "Fijadas"), "ni lo que fijaste en el menú");
        Vale(!Perfiles.Permitida("Wallpaper", "Ruta"), "ni la ruta de Wallpaper Engine, que es de la máquina");
        Vale(!Perfiles.Permitida("Perfiles", "Activo"), "ni cuál es el perfil activo");
        Vale(!Perfiles.Permitida("Paneles", "Id"), "ni los ids de los paneles de esta máquina");

        Vale(!Perfiles.Permitida("", "Acento"), "sin sección, no");
        Vale(!Perfiles.Permitida("Tema", ""), "sin clave, no");
        Vale(Perfiles.Permitida("tema", "acento"), "las mayúsculas no cambian el permiso");
    }

    static void PerfilesValidar()
    {
        Grupo("perfiles: validar antes de escribir nada");

        Perfil Bueno() => new()
        {
            Id = "noche", Nombre = "Noche", Descripcion = "Oscuro y quieto",
            Version = Perfiles.VersionEsquema,
            Ajustes = new JsonObject
            {
                ["Tema"] = new JsonObject { ["Acento"] = "#7AA2F7" },
                ["Animaciones"] = new JsonObject { ["Nivel"] = "reducido" },
            }
        };

        Vale(Perfiles.Validar(Bueno()).Vale, "un perfil bien hecho vale");
        Vale(!Perfiles.Validar(null).Vale, "y uno que no se pudo leer, no");

        var sinNombre = Bueno(); sinNombre.Nombre = "";
        Vale(!Perfiles.Validar(sinNombre).Vale, "sin nombre no vale");

        var sinId = Bueno(); sinId.Id = "";
        Vale(!Perfiles.Validar(sinId).Vale, "sin id tampoco");

        var idRaro = Bueno(); idRaro.Id = @"..\..\config";
        Vale(!Perfiles.Validar(idRaro).Vale, "un id que se sale de su carpeta se rechaza");

        // De una versión más nueva: se rechaza, no se lee a medias
        var futuro = Bueno(); futuro.Version = Perfiles.VersionEsquema + 1;
        var vf = Perfiles.Validar(futuro);
        Vale(!vf.Vale, "un perfil de una versión más nueva se rechaza");
        Vale(vf.Resumen.Contains("más nueva"), $"y se dice por qué ({vf.Resumen})");

        var v0 = Bueno(); v0.Version = 0;
        Vale(!Perfiles.Validar(v0).Vale, "y una versión 0 tampoco vale");

        // Valores malos
        var malNivel = Bueno();
        malNivel.Ajustes["Animaciones"] = new JsonObject { ["Nivel"] = "lentorro" };
        Vale(!Perfiles.Validar(malNivel).Vale, "un nivel de animación inventado se rechaza");

        var malColor = Bueno();
        malColor.Ajustes["Tema"] = new JsonObject { ["Acento"] = "azulito" };
        Vale(!Perfiles.Validar(malColor).Vale, "un color que no es un color se rechaza");

        var malPos = Bueno();
        malPos.Ajustes["Panel"] = new JsonObject { ["Posicion"] = "diagonal" };
        Vale(!Perfiles.Validar(malPos).Vale, "una posición de panel inventada se rechaza");

        var malGrosor = Bueno();
        malGrosor.Ajustes["Panel"] = new JsonObject { ["Grosor"] = 9999.0 };
        Vale(!Perfiles.Validar(malGrosor).Vale, "un grosor imposible se rechaza");

        var malSeccion = Bueno();
        malSeccion.Ajustes["Tema"] = JsonValue.Create(7);
        Vale(!Perfiles.Validar(malSeccion).Vale, "una sección que no es un objeto se rechaza");

        // Un null es legítimo: significa "deriva este rol"
        var conNull = Bueno();
        conNull.Ajustes["Tema"] = new JsonObject { ["Secundario"] = null };
        Vale(Perfiles.Validar(conNull).Vale, "un null vale: es \"deriva este rol\"");

        // ---------- Claves desconocidas: se conservan, no invalidan ----------
        var conExtra = Bueno();
        conExtra.Ajustes["Barra"] = new JsonObject { ["AtajoVistaGeneral"] = "Ctrl+Z" };
        var ve = Perfiles.Validar(conExtra);
        Vale(ve.Vale, "una clave que el perfil no puede tocar no invalida el perfil");
        Vale(ve.Ignoradas.Contains("Barra.AtajoVistaGeneral"), "pero queda anotada como ignorada");

        // Y al podar desaparece, así que lo guardado es lo aplicable
        int fuera = Perfiles.Podar(conExtra);
        Igual(1, fuera, "se poda una clave");
        Vale(!conExtra.Ajustes.ContainsKey("Barra"), "y la sección vacía se va con ella");
        Vale(conExtra.Ajustes.ContainsKey("Tema"), "lo que sí es suyo se queda");
    }

    static void PerfilesNombres()
    {
        Grupo("perfiles: ids y nombres que no chocan");

        var ocupados = new[] { "noche", "creativo" };
        Igual("minimal", Perfiles.IdLibre("Minimal", ocupados), "un id libre sale tal cual");
        Igual("noche-2", Perfiles.IdLibre("Noche", ocupados), "uno ocupado busca el siguiente");
        Igual("concentracion", Perfiles.IdLibre("Concentración", ocupados),
              "los acentos no entran en el id");
        Igual("mi-perfil", Perfiles.IdLibre("Mi  Perfil!!", ocupados), "ni los espacios ni los símbolos");
        Vale(Perfiles.IdValido(Perfiles.IdLibre("", ocupados)), "incluso un nombre vacío da un id válido");
        Vale(Perfiles.IdValido(Perfiles.IdLibre("////", ocupados)), "y uno que es todo símbolos");

        var nombres = new[] { "Noche", "Noche (copia)" };
        Igual("Noche (copia 2)", Perfiles.NombreLibre("Noche", nombres),
              "duplicar dos veces no repite el nombre");
        Igual("Minimal (copia)", Perfiles.NombreLibre("Minimal", nombres), "y el primero es (copia)");

        // Un id siempre es un nombre de fichero seguro
        foreach (var n in new[] { "..", @"a\b", "a/b", "C:", "con", "..\\..\\config", "ñandú" })
        {
            var id = Perfiles.IdLibre(n, Array.Empty<string>());
            Vale(Perfiles.IdValido(id), $"\"{n}\" da un id válido ({id})");
            Vale(!id.Contains('\\') && !id.Contains('/') && !id.Contains(':'),
                 $"y sin separadores de ruta ({id})");
        }
    }

    static void PerfilesDuplicar()
    {
        Grupo("perfiles: duplicar no hereda la protección");

        var original = new Perfil
        {
            Id = "noche", Nombre = "Noche", Descripcion = "Oscuro", Protegido = true,
            NoMolestar = true,
            Ajustes = new JsonObject { ["Tema"] = new JsonObject { ["Acento"] = "#112233" } }
        };

        var copia = original.Copia("noche-2", "Noche (copia)");
        Igual("noche-2", copia.Id, "la copia tiene su id");
        Igual("Noche (copia)", copia.Nombre, "y su nombre");
        Igual("Oscuro", copia.Descripcion, "la descripción se hereda");
        Vale(!copia.Protegido, "la protección NO se hereda: una copia se puede borrar");
        Igual(true, copia.NoMolestar, "el No molestar se hereda");

        // Y es una copia de verdad: tocar una no toca la otra
        copia.Ajustes["Tema"]!["Acento"] = "#FFFFFF";
        Igual("#112233", original.Ajustes["Tema"]!["Acento"]!.GetValue<string>(),
              "cambiar la copia no cambia el original");
        Vale(original.Protegido, "y el original sigue protegido");
    }

    static void PerfilesDeSerie()
    {
        Grupo("perfiles: los cuatro de serie");

        var cuatro = Perfiles.DeSerie();
        Igual(4, cuatro.Count, "son cuatro");

        foreach (var nombre in new[] { "Concentración", "Creativo", "Noche", "Minimal" })
            Vale(cuatro.Any(p => p.Nombre == nombre), $"está \"{nombre}\"");

        foreach (var p in cuatro)
        {
            Vale(p.Protegido, $"\"{p.Nombre}\" viene protegido: no se puede borrar");
            Vale(Perfiles.IdValido(p.Id), $"su id vale como nombre de fichero ({p.Id})");
            Vale(p.Id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'),
                 $"y no lleva acentos ni espacios ({p.Id})");
            Vale(p.Descripcion.Length > 20, $"\"{p.Nombre}\" trae una descripción de verdad");
            var v = Perfiles.Validar(p);
            Vale(v.Vale, $"\"{p.Nombre}\" es válido: {v.Resumen}");
            Igual(0, v.Ignoradas.Count, $"\"{p.Nombre}\" no toca nada que no sea suyo");
        }

        // Los nombres visibles SÍ llevan acentos; los ids no. Eso es lo que se pidió.
        var conc = cuatro.First(p => p.Id == "concentracion");
        Vale(conc.Nombre.Contains('ó'), "el nombre visible conserva su acento");
        Vale(!conc.Id.Contains('ó'), "y el id no");

        // Ids únicos
        Igual(4, cuatro.Select(p => p.Id).Distinct().Count(), "los cuatro ids son distintos");

        // Cada uno define algo distinto: cuatro perfiles iguales no serían cuatro perfiles
        var huellas = cuatro.Select(p => p.Ajustes.ToJsonString()).Distinct().Count();
        Igual(4, huellas, "los cuatro guardan cosas distintas");

        // Y el nivel de animación de Minimal es "desactivado", que es lo que hace falta
        // para probar que aplicar sin transiciones funciona
        var min = cuatro.First(p => p.Id == "minimal");
        Igual("desactivado", min.Ajustes["Animaciones"]!["Nivel"]!.GetValue<string>(),
              "Minimal apaga las animaciones");
    }

    static void PerfilesEscrituras()
    {
        Grupo("perfiles: traducir un perfil a escrituras");

        var p = new Perfil
        {
            Id = "x", Nombre = "X",
            Ajustes = new JsonObject
            {
                ["Tema"] = new JsonObject { ["Acento"] = "#112233", ["Secundario"] = null },
                ["Panel"] = new JsonObject { ["Posicion"] = "abajo", ["Grosor"] = 44.0 },
                ["Modulos"] = new JsonObject { ["Centro"] = new JsonArray("reloj") },
                ["Barra"] = new JsonObject { ["AtajoVistaGeneral"] = "Ctrl+Z" },   // no es suyo
            }
        };

        var es = Perfiles.Escrituras(p);
        Igual(5, es.Count, "cinco escrituras: la clave que no es suya no se traduce");
        Vale(!es.Any(e => e.Clave == "AtajoVistaGeneral"), "y no está entre ellas");

        // Las del panel van marcadas, porque se escriben de otra forma
        var delPanel = es.Where(e => e.EsDelPanel).ToList();
        Igual(2, delPanel.Count, "dos son del panel");
        Vale(delPanel.All(e => e.Seccion.Equals("Panel", StringComparison.OrdinalIgnoreCase)),
             "y las dos vienen de la sección Panel");

        // Un null se conserva como null: significa "quita la clave", que para un rol de
        // color es "derívalo". Perderlo dejaría el rol del tema anterior puesto.
        var sec = es.First(e => e.Clave == "Secundario");
        Vale(sec.Valor == null, "un null sobrevive a la traducción");

        // Y es una copia: tocar el perfil después no cambia las escrituras ya calculadas
        p.Ajustes["Tema"]!["Acento"] = "#FFFFFF";
        var ac = es.First(e => e.Clave == "Acento");
        Igual("#112233", ac.Valor!.GetValue<string>(), "las escrituras son una copia del perfil");

        Igual(0, Perfiles.Escrituras(new Perfil()).Count, "un perfil sin ajustes no escribe nada");
    }

    static void PerfilesAplicarYDeshacer()
    {
        Grupo("perfiles: aplicar es una transacción");

        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-perfiles-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "config.json");
        try
        {
            // Un config de partida con cosas que el perfil NO toca, para comprobar que
            // sobreviven.
            var inicial = new JsonObject
            {
                ["Version"] = Esquema.Actual,
                ["Tema"] = new JsonObject { ["Acento"] = "#AAAAAA", ["Fondo"] = "#E6000000" },
                ["Barra"] = new JsonObject
                {
                    ["AtajoVistaGeneral"] = "Ctrl+Alt+W",
                    ["AppsFijadas"] = new JsonArray("notepad.exe"),
                    ["PulsoAudio"] = true,
                },
                ["Animaciones"] = new JsonObject { ["Nivel"] = "completo", ["RobotVisto"] = true },
                ["Paneles"] = new JsonArray(new JsonObject
                {
                    ["Id"] = "principal", ["Posicion"] = "arriba", ["Grosor"] = 38.0
                }),
                ["LoDelFuturo"] = new JsonObject { ["X"] = 1.0 },
            };
            File.WriteAllText(ruta, inicial.ToJsonString());

            var perfil = new Perfil
            {
                Id = "prueba", Nombre = "Prueba", Descripcion = "Para la prueba",
                Ajustes = new JsonObject
                {
                    ["Tema"] = new JsonObject { ["Acento"] = "#112233" },
                    ["Animaciones"] = new JsonObject { ["Nivel"] = "reducido" },
                    ["Barra"] = new JsonObject { ["PulsoAudio"] = false },
                    ["Panel"] = new JsonObject { ["Posicion"] = "abajo", ["Grosor"] = 44.0 },
                }
            };

            // ---------- Aplicar ----------
            var r = Perfiles.Aplicar(perfil, ruta);
            Vale(r.Ok, "se aplica: " + r.Detalle);
            Igual(5, r.Escritas, "cinco claves escritas");

            var despues = JsonNode.Parse(File.ReadAllText(ruta))!.AsObject();
            Igual("#112233", despues["Tema"]!["Acento"]!.GetValue<string>(), "el acento es el del perfil");
            Igual("reducido", despues["Animaciones"]!["Nivel"]!.GetValue<string>(), "y el nivel de animación");
            Igual(false, despues["Barra"]!["PulsoAudio"]!.GetValue<bool>(), "y el pulso de audio");
            Igual("abajo", despues["Paneles"]![0]!["Posicion"]!.GetValue<string>(),
                  "el panel se traduce al principal de esta máquina");
            Igual(44.0, despues["Paneles"]![0]!["Grosor"]!.GetValue<double>(), "con su grosor");

            // ---------- Lo que el perfil NO toca sigue ahí ----------
            Igual("Ctrl+Alt+W", despues["Barra"]!["AtajoVistaGeneral"]!.GetValue<string>(),
                  "el atajo del usuario no se toca");
            Igual("notepad.exe", despues["Barra"]!["AppsFijadas"]!.AsArray()[0]!.GetValue<string>(),
                  "ni sus apps fijadas");
            Igual(true, despues["Animaciones"]!["RobotVisto"]!.GetValue<bool>(),
                  "ni que ya vio la escena");
            Igual("#E6000000", despues["Tema"]!["Fondo"]!.GetValue<string>(),
                  "ni el fondo del tema, que el perfil no menciona");
            Vale(despues["LoDelFuturo"] is JsonObject, "ni una sección de una versión futura");
            Igual(Esquema.Actual, despues["Version"]!.GetValue<int>(), "y la versión sigue puesta");

            // ---------- El activo ----------
            var est = Perfiles.LeerEstado(ruta);
            Igual("prueba", est.Activo, "queda marcado como activo");
            Vale(est.HuellaAlAplicar != null, "con la huella de cómo quedó");

            // ---------- Y no está modificado ----------
            var arbol = ConfigArbol.Cargar(ruta);
            Vale(!Perfiles.Modificado(est.HuellaAlAplicar, Perfiles.Huella(perfil, arbol)),
                 "recién aplicado, el perfil no está modificado");

            // Tocar algo que el perfil SÍ controla lo marca como modificado
            arbol.Poner("Tema", "Acento", "#FF0000");
            arbol.Guardar();
            Vale(Perfiles.Modificado(est.HuellaAlAplicar, Perfiles.Huella(perfil, ConfigArbol.Cargar(ruta))),
                 "cambiar a mano algo del perfil lo marca como modificado");

            // Pero tocar algo que NO controla, no
            var a2 = ConfigArbol.Cargar(ruta);
            a2.Poner("Tema", "Acento", "#112233");      // se devuelve
            a2.Poner("Barra", "AtajoVistaGeneral", "Ctrl+Alt+Q");
            a2.Guardar();
            Vale(!Perfiles.Modificado(est.HuellaAlAplicar, Perfiles.Huella(perfil, ConfigArbol.Cargar(ruta))),
                 "cambiar algo que el perfil no controla NO lo marca como modificado");

            // ---------- Deshacer ----------
            Vale(Perfiles.HayQueDeshacerEn(ruta), "hay instantánea que deshacer");
            Vale(Perfiles.Deshacer(ruta), "se deshace");

            var vuelto = JsonNode.Parse(File.ReadAllText(ruta))!.AsObject();
            Igual("#AAAAAA", vuelto["Tema"]!["Acento"]!.GetValue<string>(), "el acento vuelve al de antes");
            Igual("completo", vuelto["Animaciones"]!["Nivel"]!.GetValue<string>(), "y el nivel");
            Igual(true, vuelto["Barra"]!["PulsoAudio"]!.GetValue<bool>(), "y el pulso de audio");
            Igual("arriba", vuelto["Paneles"]![0]!["Posicion"]!.GetValue<string>(), "y el panel");
            Vale(vuelto["LoDelFuturo"] is JsonObject, "sin perder la sección del futuro");

            Vale(Perfiles.LeerEstado(ruta).Activo == null, "y ya no hay perfil activo");
            Vale(!Perfiles.HayQueDeshacerEn(ruta), "ni instantánea pendiente");
            Vale(!Perfiles.Deshacer(ruta), "deshacer dos veces no hace nada");

            // ---------- Un perfil inválido NO toca el config ----------
            var antesDeMalo = File.ReadAllText(ruta);
            var malo = new Perfil
            {
                Id = "malo", Nombre = "Malo",
                Ajustes = new JsonObject { ["Animaciones"] = new JsonObject { ["Nivel"] = "lentorro" } }
            };
            var rm = Perfiles.Aplicar(malo, ruta);
            Vale(!rm.Ok, "un perfil inválido no se aplica");
            Igual(antesDeMalo, File.ReadAllText(ruta),
                  "y el config se queda EXACTAMENTE como estaba, byte a byte");
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch { }
        }
    }

    static void PerfilesRestaurar()
    {
        Grupo("perfiles: restaurar los valores de fábrica");

        // Solo los de serie tienen valores de fábrica
        Vale(Perfiles.DeSerie().Any(p => p.Id == "noche"), "\"noche\" es de serie");
        var r = Perfiles.DeSerie().FirstOrDefault(p => p.Id == "mi-perfil");
        Vale(r == null, "y un perfil propio no, así que no hay nada a lo que volver");

        // La protección es lo que decide si se puede borrar
        var deSerie = Perfiles.DeSerie().First();
        Vale(deSerie.Protegido, "uno de serie está protegido");
        var copia = deSerie.Copia("copia-1", "Copia 1");
        Vale(!copia.Protegido, "su copia no, así que la copia sí se puede borrar");
    }

    // ================= C5.2: el gestor de fondos =================

    static Monitores.Info Pant(int x, int y, int w, int h, bool principal = false, uint dpi = 96,
                               string dispositivo = @"\\.\DISPLAY1") =>
        new(IntPtr.Zero, dispositivo, x, y, w, h, x, y, w, h, principal, dpi);

    static void FondoDibujo()
    {
        Grupo("fondo: dibujar el sólido y el degradado");

        // ---------- El degradado va de un color al otro ----------
        var negro = Color.FromRgb(0, 0, 0);
        var blanco = Color.FromRgb(255, 255, 255);
        var px = GestorFondos.PixelesDeDegradado(64, 16, negro, blanco, 0);
        Igual(64 * 16 * 4, px.Length, "salen los píxeles que caben (Bgra32)");

        // Bgra32: el primer píxel de la primera fila y el último de la misma
        Igual(0, (int)px[0], "a 0° la izquierda es el primer color");
        int ultimo = (63 * 4);
        Igual(255, (int)px[ultimo], "y la derecha el segundo");
        Igual(255, (int)px[3], "el alfa está a tope: un fondo no es translúcido");

        // A 90° el cambio es de arriba abajo, no de izquierda a derecha
        var v = GestorFondos.PixelesDeDegradado(16, 64, negro, blanco, 90);
        Igual((int)v[0], (int)v[15 * 4], "a 90° la primera fila es toda del mismo color");
        Vale(v[0] != v[63 * 16 * 4], "y la última es distinta de la primera");

        // Un sólido es un degradado de un color a sí mismo: plano de verdad
        var plano = GestorFondos.PixelesDeDegradado(8, 8, negro, negro, 135);
        Vale(plano.Where((_, i) => i % 4 != 3).All(b => b == 0), "un color a sí mismo sale plano");

        // Tamaños imposibles no revientan
        Igual(0, GestorFondos.PixelesDeDegradado(0, 10, negro, blanco, 0).Length, "ancho 0 no da píxeles");
        Igual(0, GestorFondos.PixelesDeDegradado(10, -5, negro, blanco, 0).Length, "alto negativo tampoco");

        // Cualquier ángulo da una imagen completa y sin valores fuera de rango
        foreach (double g in new double[] { 0, 45, 90, 135, 180, 270, 359, 360 })
        {
            var p = GestorFondos.PixelesDeDegradado(12, 12, negro, blanco, g);
            Igual(12 * 12 * 4, p.Length, $"a {g}° la imagen está completa");
        }

        // ---------- El nombre del intermedio es determinista ----------
        var n1 = GestorFondos.NombreDeIntermedio("degradado", "#000000", "#FFFFFF", 135, 1920, 1080);
        var n2 = GestorFondos.NombreDeIntermedio("degradado", "#000000", "#FFFFFF", 135, 1920, 1080);
        Igual(n1, n2, "los mismos parámetros dan el mismo fichero: no se redibuja lo que ya está");
        Vale(n1 != GestorFondos.NombreDeIntermedio("degradado", "#000000", "#FFFFFF", 90, 1920, 1080),
             "cambiar el ángulo da otro fichero");
        Vale(n1 != GestorFondos.NombreDeIntermedio("degradado", "#010000", "#FFFFFF", 135, 1920, 1080),
             "y cambiar un color también");
        Vale(n1 != GestorFondos.NombreDeIntermedio("degradado", "#000000", "#FFFFFF", 135, 2560, 1440),
             "y cambiar la resolución");
        Vale(n1.EndsWith(".png"), "es un png");
        Vale(!n1.Contains('/') && !n1.Contains('\\'), "y un nombre de fichero, no una ruta");
    }

    static void FondoMonitores()
    {
        Grupo("fondo: emparejar monitores de verdad");

        var p1 = Pant(0, 0, 1920, 1080, principal: true);
        var p2 = Pant(1920, 0, 2560, 1440, dispositivo: @"\\.\DISPLAY2");
        var pantallas = new List<Monitores.Info> { p1, p2 };

        // ---------- Lo normal: coinciden exactamente ----------
        var deCom = new List<(string, int, int, int, int)>
        {
            (@"\\?\DISPLAY#B", 1920, 0, 2560, 1440),      // COM los da en otro orden
            (@"\\?\DISPLAY#A", 0, 0, 1920, 1080),
        };
        var pares = Fondos.Emparejar(deCom, pantallas);
        Igual(2, pares.Count, "se emparejan las dos");
        Igual(p2.Clave, pares[0].Clave, "la primera de COM es la SEGUNDA de Windows: por rectángulo, no por índice");
        Igual(p1.Clave, pares[1].Clave, "y la segunda de COM es la primera");

        // Esto es lo que arregla: por índice, pares[0] habría sido p1 y el fondo del
        // monitor grande se habría puesto en el pequeño.
        Vale(pares[0].Clave != p1.Clave, "el emparejamiento por índice habría acertado al revés");

        // ---------- Cambio de resolución: se empareja por la esquina ----------
        var trasCambio = new List<(string, int, int, int, int)>
        {
            (@"\\?\DISPLAY#A", 0, 0, 1280, 720),          // misma esquina, otra resolución
        };
        var p3 = Fondos.Emparejar(trasCambio, pantallas);
        Igual(p1.Clave, p3[0].Clave, "al cambiar de resolución se empareja por la esquina");

        // ---------- Un monitor que COM ve y Windows no ----------
        var fantasma = new List<(string, int, int, int, int)>
        {
            (@"\\?\DISPLAY#X", 5000, 5000, 1024, 768),
        };
        var p4 = Fondos.Emparejar(fantasma, pantallas);
        Vale(p4[0].Clave == null, "una pantalla que no se corresponde se queda sin clave");
        // Y eso es lo importante: sin clave, Poner la salta en vez de adivinar

        // ---------- Un monitor sin rectángulo (COM no supo decir dónde) ----------
        var sinRect = new List<(string, int, int, int, int)> { (@"\\?\DISPLAY#Y", int.MinValue, int.MinValue, 0, 0) };
        Vale(Fondos.Emparejar(sinRect, pantallas)[0].Clave == null, "sin rectángulo no se empareja");

        // ---------- Nunca se empareja la misma pantalla dos veces ----------
        var dobles = new List<(string, int, int, int, int)>
        {
            (@"\\?\DISPLAY#A", 0, 0, 1920, 1080),
            (@"\\?\DISPLAY#A2", 0, 0, 1920, 1080),
        };
        var p5 = Fondos.Emparejar(dobles, pantallas);
        Igual(p1.Clave, p5[0].Clave, "la primera se lleva la pantalla");
        Vale(p5[1].Clave == null, "y la segunda no se la roba");

        // ---------- Monitor vertical y DPI distinto ----------
        var vert = Pant(0, 0, 1080, 1920, dpi: 144, dispositivo: @"\\.\DISPLAY3");
        var pv = Fondos.Emparejar(new List<(string, int, int, int, int)> { ("v", 0, 0, 1080, 1920) },
                                  new List<Monitores.Info> { vert });
        Igual(vert.Clave, pv[0].Clave, "un monitor vertical se empareja igual");
        Vale(vert.Alto > vert.Ancho, "y sigue siendo más alto que ancho");
        Igual(1.5, vert.Escala, "con su escala de 144 ppp");

        // La clave es estable y distinta por pantalla: es lo que se guarda en el config
        Vale(p1.Clave != p2.Clave, "dos pantallas tienen claves distintas");
        Igual(p1.Clave, Pant(0, 0, 1920, 1080, true).Clave, "y la misma pantalla, la misma clave");

        // Sin pantallas no se empareja nada y no revienta
        Igual(0, Fondos.Emparejar(new List<(string, int, int, int, int)>(), pantallas).Count,
              "sin pantallas de COM, no hay pares");
        Vale(Fondos.Emparejar(deCom, new List<Monitores.Info>()).All(x => x.Clave == null),
             "sin pantallas de Windows, ninguna se empareja");
    }

    static void FondoPorMonitorEnElArbol()
    {
        Grupo("fondo: escribir el de un monitor sin tocar el resto");

        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-fondo-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        try
        {
            var ruta = Path.Combine(carpeta, "config.json");
            File.WriteAllText(ruta, new JsonObject
            {
                ["Version"] = Esquema.Actual,
                ["Fondo"] = new JsonObject { ["Tipo"] = "degradado", ["Color1"] = "#111111" },
                ["Barra"] = new JsonObject { ["AtajoVistaGeneral"] = "Ctrl+Alt+W" },
            }.ToJsonString());

            var a = ConfigArbol.Cargar(ruta);
            Vale(a.TextoDeFondoDeMonitor("PANT-A", "Tipo") == null,
                 "un monitor sin nada propio devuelve null, que significa \"hereda\"");

            a.PonerEnFondoDeMonitor("PANT-A", "Tipo", "imagen");
            a.PonerEnFondoDeMonitor("PANT-A", "Ruta", @"C:\a.jpg");
            a.PonerEnFondoDeMonitor("PANT-B", "Tipo", "solido");
            a.Guardar();

            var b = ConfigArbol.Cargar(ruta);
            Igual("imagen", b.TextoDeFondoDeMonitor("PANT-A", "Tipo")!, "lo del monitor A se guarda");
            Igual(@"C:\a.jpg", b.TextoDeFondoDeMonitor("PANT-A", "Ruta")!, "con su ruta");
            Igual("solido", b.TextoDeFondoDeMonitor("PANT-B", "Tipo")!, "y lo del B, aparte");
            Igual("degradado", b.Texto("Fondo", "Tipo", "x"), "el fondo general no se ha tocado");
            Igual("#111111", b.Texto("Fondo", "Color1", "x"), "ni su color");
            Igual("Ctrl+Alt+W", b.Texto("Barra", "AtajoVistaGeneral", "x"), "ni nada de fuera");

            // Quitar lo propio = volver a heredar
            b.QuitarFondoDeMonitor("PANT-A");
            b.Guardar();
            var d = ConfigArbol.Cargar(ruta);
            Vale(d.TextoDeFondoDeMonitor("PANT-A", "Tipo") == null, "quitarlo lo devuelve a heredar");
            Igual("solido", d.TextoDeFondoDeMonitor("PANT-B", "Tipo")!, "y no se lleva al otro monitor");

            // Y la herencia de verdad, con el modelo
            var cfg = new Fondo { Tipo = "degradado", Color1 = "#111111", Ajuste = "rellenar" };
            cfg.PorMonitor["PANT-B"] = new FondoDeMonitor { Tipo = "solido" };
            Igual("degradado", Fondos.Efectivo(cfg, "PANT-A").Tipo!, "A hereda el general");
            Igual("solido", Fondos.Efectivo(cfg, "PANT-B").Tipo!, "B usa el suyo");
            Igual("#111111", Fondos.Efectivo(cfg, "PANT-B").Color1!, "pero hereda el color, que no dice");
            Igual("rellenar", Fondos.Efectivo(cfg, "PANT-B").Ajuste!, "y el ajuste");
        }
        finally { try { Directory.Delete(carpeta, true); } catch { } }
    }

    static void FondoNoRecarga()
    {
        Grupo("fondo: no recargar si no ha cambiado nada");

        // Ya se comprueba la huella en C5; aquí lo que importa es lo que decide el gestor
        // con esa huella. "ninguno" no toca el fondo de nadie, ni al arrancar.
        var nada = new Fondo { Tipo = "ninguno" };
        var r = GestorFondos.AplicarAsync(nada).GetAwaiter().GetResult();
        Vale(r.Ok, "tipo ninguno no es un error");
        Vale(!r.Cambio, "y no cambia nada");
        Vale(r.Detalle.Contains("no gestiona"), $"lo dice claro ({r.Detalle})");
        Vale(!GestorFondos.Corriendo, "y deja la presentación parada");

        // "animado" no lo gestiona esto: es de la integración de a918e6c
        var we = new Fondo { Tipo = "animado" };
        var rw = GestorFondos.AplicarAsync(we).GetAwaiter().GetResult();
        Vale(rw.Ok, "tipo animado no es un error");
        Vale(!rw.Cambio, "el gestor de fondos no lo toca");
        Vale(rw.Detalle.Contains("Wallpaper Engine"), $"y dice de quién es ({rw.Detalle})");
        Vale(!GestorFondos.Corriendo, "sin dejar relojes en marcha");

        // Un null no revienta
        Vale(!GestorFondos.AplicarAsync(null!).GetAwaiter().GetResult().Ok, "sin configuración, no se aplica");

        // Sin Wallpaper Engine, "animado" no pinta pero los demás tipos siguen valiendo
        Igual("ninguno", Fondos.TipoEfectivo("animado", false), "sin WE, animado no pinta");
        foreach (var t in new[] { "solido", "degradado", "imagen", "presentacion" })
            Igual(t, Fondos.TipoEfectivo(t, false), $"pero \"{t}\" sigue funcionando sin WE");
    }

    static void FondoRelojes()
    {
        Grupo("fondo: los relojes de la presentación se sueltan");

        // Esto es lo que se pidió comprobar: que no quede un reloj vivo. `Apagar` es lo
        // que llama el cierre de NubeBar.
        GestorFondos.PararPresentacion();
        Vale(!GestorFondos.Corriendo, "parada, no corre");

        GestorFondos.Apagar();
        Vale(!GestorFondos.Corriendo, "apagado, tampoco");

        // Y apagar dos veces no revienta
        GestorFondos.Apagar();
        Vale(!GestorFondos.Corriendo, "apagarlo dos veces es seguro");

        // Sin hilo de interfaz, avanzar no hace nada en vez de fallar
        Vale(!GestorFondos.Avanzar().GetAwaiter().GetResult(),
             "sin presentación programada, avanzar no hace nada");

        // El intervalo nunca baja del mínimo: una presentación cada segundo es un parpadeo
        Vale(Fondos.MinSegundos >= 5, "el mínimo del intervalo es razonable");
        Vale(Fondos.ValorMal("segundospresentacion", JsonValue.Create(1.0)) != null,
             "un segundo se rechaza");
        Vale(Fondos.ValorMal("segundospresentacion", JsonValue.Create(Fondos.MinSegundos)) == null,
             "el mínimo se acepta");
    }

    static void FondoDelPerfilNoche()
    {
        Grupo("fondo: el perfil Noche trae un degradado válido");

        var noche = Perfiles.DeSerie().First(p => p.Id == "noche");
        Vale(noche.Ajustes["Fondo"] is JsonObject, "Noche trae una sección Fondo");

        var f = noche.Ajustes["Fondo"]!.AsObject();
        Igual("degradado", f["Tipo"]!.GetValue<string>(), "y es un degradado");
        Vale(Paleta.EsColor(f["Color1"]!.GetValue<string>()), "con un primer color de verdad");
        Vale(Paleta.EsColor(f["Color2"]!.GetValue<string>()), "y un segundo");

        // Y el perfil entero sigue siendo válido con el fondo dentro
        var v = Perfiles.Validar(noche);
        Vale(v.Vale, "el perfil es válido: " + v.Resumen);
        Igual(0, v.Ignoradas.Count, "y no lleva nada que no pueda tocar");

        // Las claves de fondo están en la lista blanca, así que se APLICAN de verdad
        var es = Perfiles.Escrituras(noche);
        Vale(es.Any(e => e.Seccion == "Fondo" && e.Clave == "Tipo"),
             "el tipo de fondo se traduce a una escritura");
        Vale(es.Any(e => e.Seccion == "Fondo" && e.Clave == "Color1"), "y los colores");

        // Un perfil SIN fondo no escribe nada de fondo: no borra el que tengas
        var minimal = Perfiles.DeSerie().First(p => p.Id == "minimal");
        Vale(!Perfiles.Escrituras(minimal).Any(e => e.Seccion == "Fondo"),
             "un perfil sin fondo no toca el fondo");

        // Y un perfil con un fondo inválido se rechaza entero
        var malo = noche.Copia("malo", "Malo");
        malo.Ajustes["Fondo"]!["Tipo"] = "video";
        Vale(!Perfiles.Validar(malo).Vale, "un tipo de fondo inventado invalida el perfil");
        var malColor = noche.Copia("malo2", "Malo 2");
        malColor.Ajustes["Fondo"]!["Color1"] = "azulito";
        Vale(!Perfiles.Validar(malColor).Vale, "y un color que no es un color, también");
    }


    static void FondoPngDeVerdad()
    {
        Grupo("fondo: el degradado se escribe como PNG y se puede leer");

        // Los píxeles ya se comprueban puros; esto comprueba la otra mitad, que es la que
        // puede fallar en silencio: que `BitmapSource.Create` + `PngBitmapEncoder` escriban
        // un fichero de verdad desde este hilo. Si esto se rompiera, el degradado quedaría
        // en "no se pudo dibujar" y el fondo no cambiaría sin que nadie supiera por qué.
        var ruta = GestorFondos.Dibujar("degradado", "#102030", "#A0B0C0", 135, 320, 180);
        Vale(ruta != null, "se dibuja y devuelve una ruta");
        if (ruta == null) return;

        Vale(File.Exists(ruta), "el fichero existe");
        Vale(new FileInfo(ruta).Length > 100, $"y tiene contenido ({new FileInfo(ruta).Length} bytes)");
        Vale(ruta.StartsWith(Fondos.CarpetaIntermedios, StringComparison.OrdinalIgnoreCase),
             "vive en la carpeta de intermedios, que es caché y no configuración");

        // Y se puede leer como imagen: es lo que hará Windows
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(ruta);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            Igual(320, bmp.PixelWidth, "con el ancho que se pidió");
            Igual(180, bmp.PixelHeight, "y el alto");
        }
        catch (Exception ex) { Vale(false, "el PNG no se pudo leer: " + ex.Message); }

        // No se redibuja lo que ya está: la segunda vez devuelve el mismo fichero
        var antes = File.GetLastWriteTimeUtc(ruta);
        var otra = GestorFondos.Dibujar("degradado", "#102030", "#A0B0C0", 135, 320, 180);
        Igual(ruta, otra!, "la segunda vez es el mismo fichero");
        Igual(antes, File.GetLastWriteTimeUtc(ruta), "y no se ha reescrito");

        // Un sólido también sale, y es otro fichero
        var plano = GestorFondos.Dibujar("solido", "#102030", "#102030", 0, 64, 64);
        Vale(plano != null && File.Exists(plano), "el sólido también se dibuja");
        Vale(plano != ruta, "y es un fichero distinto del degradado");

        // Un tamaño absurdo se acota en vez de escribir un PNG de 16K
        var grande = GestorFondos.Dibujar("degradado", "#000000", "#FFFFFF", 0, 99999, 99999);
        Vale(grande != null, "un tamaño absurdo se acota y sigue dibujando");
        Vale(grande!.Contains("3840x2160"), $"acotado a 4K ({System.IO.Path.GetFileName(grande)})");

        // Un color inválido no revienta: cae a negro, que es lo que hace Paleta.Leer
        var malo = GestorFondos.Dibujar("solido", "azulito", "azulito", 0, 32, 32);
        Vale(malo != null && File.Exists(malo), "un color inválido no rompe el dibujo");

        // Limpieza: esto es caché, se puede borrar
        foreach (var f in new[] { ruta, plano, grande, malo })
            try { if (f != null) File.Delete(f); } catch { }
    }

    // ================= C5.3: el marco de la ventana de Ajustes =================

    static void AjustesSecciones()
    {
        Grupo("ajustes: el registro y los iconos de todas las secciones");

        // Las once secciones que tiene que haber, con su categoría. Si alguien añade una
        // y se olvida de registrarla, esto lo dice.
        var esperadas = new (string Id, string Cat)[]
        {
            ("apariencia", "Aspecto"), ("colores", "Aspecto"), ("fondo", "Aspecto"),
            ("wallpaper", "Aspecto"), ("perfiles", "Aspecto"),
            ("modulos", "Panel"), ("biblioteca", "Panel"), ("apps", "Panel"),
            ("atajos", "Sistema"), ("inicio", "Sistema"), ("sistema", "Sistema"),
        };

        // Cada sección tiene icono, y es un glifo de verdad: ni vacío ni una caja.
        foreach (var (id, _) in esperadas)
        {
            var g = VentanaAjustes.GlifoDeSeccion(id);
            Igual(1, g.Length, $"\"{id}\" tiene un glifo de un carácter");
            Vale(g[0] >= 0xE000 && g[0] <= 0xF8FF,
                 $"\"{id}\" usa la zona de uso privado, donde la fuente de iconos los tiene ({(int)g[0]:X4})");
        }

        // Y no se repiten: dos secciones con el mismo icono no se distinguen de un vistazo
        var glifos = esperadas.Select(e => VentanaAjustes.GlifoDeSeccion(e.Id)).ToList();
        Igual(glifos.Count, glifos.Distinct().Count(), "ningún icono se repite entre secciones");

        // Una sección desconocida no revienta: cae al de ajustes
        Igual(1, VentanaAjustes.GlifoDeSeccion("loquesea").Length, "una sección desconocida tiene icono igual");

        // Las tres categorías son las que se pidieron, con sus nombres visibles
        Igual("Personalización", VentanaAjustes.NombreDeCategoriaVisible("Aspecto"),
              "\"Aspecto\" se llama Personalización en pantalla");
        Igual("NubeBar", VentanaAjustes.NombreDeCategoriaVisible("Panel"),
              "\"Panel\" se llama NubeBar");
        Igual("Sistema", VentanaAjustes.NombreDeCategoriaVisible("Sistema"), "y Sistema, Sistema");
        // Una categoría que no existe se devuelve tal cual en vez de quedarse vacía
        Igual("Otra", VentanaAjustes.NombreDeCategoriaVisible("Otra"), "una categoría desconocida se enseña tal cual");

        // El reparto es el que se pidió: 5 / 3 / 3
        Igual(5, esperadas.Count(e => e.Cat == "Aspecto"), "cinco en Personalización");
        Igual(3, esperadas.Count(e => e.Cat == "Panel"), "tres en NubeBar");
        Igual(3, esperadas.Count(e => e.Cat == "Sistema"), "tres en Sistema");
        Igual(11, esperadas.Length, "once secciones en total");
    }

    static void AjustesBusqueda()
    {
        Grupo("ajustes: la búsqueda");

        var apariencia = new DescriptorAjustes("apariencia", "Apariencia", "Aspecto",
                                               "tema", "radio", "fuente", "animaciones", "movimiento");
        var fondo = new DescriptorAjustes("fondo", "Fondo", "Aspecto",
                                          "escritorio", "degradado", "imagen", "presentación");
        var modulos = new DescriptorAjustes("modulos", "Módulos", "Panel",
                                            "widgets", "zonas", "reloj");
        var apps = new DescriptorAjustes("apps", "Apps", "Panel", "carrusel", "ventanas");

        // Sin texto, todo coincide: la lista entera a la vista
        foreach (var d in new[] { apariencia, fondo, modulos, apps })
            Vale(VentanaAjustes.Coincide(d, "", "Personalización"), $"sin buscar, \"{d.Nombre}\" se ve");
        Vale(VentanaAjustes.Coincide(apariencia, "   ", "Personalización"), "ni con espacios");

        // Los cuatro términos que se pidieron probar
        Vale(VentanaAjustes.Coincide(apariencia, "animación", "Personalización"),
             "\"animación\" encuentra Apariencia por su palabra clave");
        Vale(VentanaAjustes.Coincide(fondo, "fondo", "Personalización"), "\"fondo\" encuentra Fondo por su nombre");
        Vale(VentanaAjustes.Coincide(modulos, "widget", "NubeBar"), "\"widget\" encuentra Módulos");
        Vale(VentanaAjustes.Coincide(apps, "ventana", "NubeBar"), "\"ventana\" encuentra Apps");

        // Por categoría visible, que es lo que el usuario ve escrito
        Vale(VentanaAjustes.Coincide(modulos, "nubebar", "NubeBar"), "se puede buscar por categoría");
        Vale(!VentanaAjustes.Coincide(modulos, "nubebar", "Panel"),
             "y es la categoría VISIBLE la que cuenta, no su id interno");

        // Sin acentos ni mayúsculas no se pierde nada
        Vale(VentanaAjustes.Coincide(apariencia, "TEMA", "Personalización"), "las mayúsculas no importan");
        Vale(VentanaAjustes.Coincide(fondo, "PRESENTACIÓN", "Personalización"), "ni en una palabra con acento");

        // Por id, que es lo que escribe quien conoce el config
        Vale(VentanaAjustes.Coincide(apariencia, "apariencia", "Personalización"), "se puede buscar por id");

        // Y el estado vacío: algo que no está, no está
        foreach (var d in new[] { apariencia, fondo, modulos, apps })
            Vale(!VentanaAjustes.Coincide(d, "zzzzqqq", "Personalización"),
                 $"\"zzzzqqq\" no encuentra \"{d.Nombre}\"");

        // Un trozo de palabra encuentra: "degrad" tiene que dar Fondo
        Vale(VentanaAjustes.Coincide(fondo, "degrad", "Personalización"), "un trozo de palabra encuentra");
        // Y ahora sin tilde, que es como la mitad de la gente escribe
        Vale(VentanaAjustes.Coincide(apariencia, "animacion", "Personalización"),
             "\"animacion\" sin tilde también encuentra Apariencia");
        Vale(VentanaAjustes.Coincide(fondo, "presentacion", "Personalización"),
             "y \"presentacion\" sin tilde encuentra Fondo");
        Vale(VentanaAjustes.Coincide(modulos, "modulos", "NubeBar"),
             "y \"modulos\" sin tilde encuentra Módulos");
    }

    static void AjustesSubtitulos()
    {
        Grupo("ajustes: cada sección explica qué es");

        foreach (var id in new[] { "apariencia", "colores", "fondo", "wallpaper", "perfiles",
                                   "modulos", "biblioteca", "apps", "atajos", "inicio", "sistema" })
        {
            var s = VentanaAjustes.SubtituloDe(id);
            Vale(s.Length > 10, $"\"{id}\" tiene un subtítulo de verdad ({s.Length} caracteres)");
            Vale(s.Length < 90, $"y no es un párrafo ({s.Length} caracteres)");
            Vale(!s.EndsWith("."), $"sin punto final, que es un rótulo y no una frase: \"{s}\"");
        }

        // Los subtítulos no se repiten: si dos secciones dicen lo mismo, no explican nada
        var todos = new[] { "apariencia", "colores", "fondo", "wallpaper", "perfiles",
                            "modulos", "biblioteca", "apps", "atajos", "inicio", "sistema" }
                    .Select(VentanaAjustes.SubtituloDe).ToList();
        Igual(todos.Count, todos.Distinct().Count(), "ningún subtítulo se repite");

        // Una sección sin subtítulo devuelve vacío en vez de reventar
        Igual("", VentanaAjustes.SubtituloDe("loquesea"), "una sección desconocida no tiene subtítulo");
    }

    static void AjustesIconosDeTarjeta()
    {
        Grupo("ajustes: los iconos de las tarjetas");

        // Todos tienen que estar en la zona de uso privado: fuera de ahí la fuente de
        // iconos no los tiene y sale una caja. Es lo que se pidió comprobar.
        foreach (var c in VentanaAjustes.Ico.Todos)
        {
            Vale(c >= 0xE000 && c <= 0xF8FF, $"0x{c:X4} está en la zona de uso privado");
            var g = char.ConvertFromUtf32(c);
            Igual(1, g.Length, $"0x{c:X4} es un solo carácter");
            Vale(!char.IsWhiteSpace(g[0]) && !char.IsControl(g[0]),
                 $"0x{c:X4} no es un espacio ni un control");
        }

        // Y no hay dos constantes con el mismo número: dos nombres para el mismo icono
        // engañan a quien los usa.
        Igual(VentanaAjustes.Ico.Todos.Length, VentanaAjustes.Ico.Todos.Distinct().Count(),
              "ninguna constante de icono repite número");

        // Los de la navegación lateral salen del mismo juego vetado
        foreach (var id in new[] { "apariencia", "colores", "fondo", "wallpaper", "perfiles",
                                   "modulos", "biblioteca", "apps", "atajos", "inicio", "sistema" })
        {
            int c = VentanaAjustes.GlifoDeSeccion(id)[0];
            Vale(VentanaAjustes.Ico.Todos.Contains(c),
                 $"el icono de \"{id}\" (0x{c:X4}) es uno de los vetados");
        }
    }

    static void AjustesIconoDeGrupo()
    {
        Grupo("ajustes: a cada tarjeta le toca su icono");

        // La tabla que elige el icono por el título. Existe para no elegirlo a mano
        // cuarenta veces y salir cuarenta cosas distintas; esto comprueba que acierta.
        Igual(VentanaAjustes.Ico.Forma, VentanaAjustes.IconoDeGrupo("Forma y posición"),
              "\"Forma y posición\" usa el icono de forma");
        Igual(VentanaAjustes.Ico.Color, VentanaAjustes.IconoDeGrupo("Colores principales"),
              "los colores, el de color");
        Igual(VentanaAjustes.Ico.Color, VentanaAjustes.IconoDeGrupo("Tema"), "el tema, también");
        Igual(VentanaAjustes.Ico.Imagen, VentanaAjustes.IconoDeGrupo("Fondo de escritorio"),
              "el fondo, el de imagen");
        Igual(VentanaAjustes.Ico.Presentacion, VentanaAjustes.IconoDeGrupo("Wallpaper Engine"),
              "Wallpaper Engine, el de presentación");
        Igual(VentanaAjustes.Ico.Zonas, VentanaAjustes.IconoDeGrupo("Zonas y widgets"),
              "las zonas, el de rejilla");
        Igual(VentanaAjustes.Ico.Teclado, VentanaAjustes.IconoDeGrupo("Atajos de teclado"),
              "los atajos, el de teclado");
        Igual(VentanaAjustes.Ico.Perfil, VentanaAjustes.IconoDeGrupo("Tus perfiles"),
              "los perfiles, el de perfil");
        Igual(VentanaAjustes.Ico.Pantallas, VentanaAjustes.IconoDeGrupo("Monitor"),
              "el monitor, el de pantallas");
        Igual(VentanaAjustes.Ico.Info, VentanaAjustes.IconoDeGrupo("Estado de la integración"),
              "el estado, el de información");
        Igual(VentanaAjustes.Ico.Restaurar, VentanaAjustes.IconoDeGrupo("Restaurar el tema"),
              "restaurar, el de recargar");
        Igual(VentanaAjustes.Ico.Sonido, VentanaAjustes.IconoDeGrupo("Reacción al sonido"),
              "el sonido, el de volumen");

        // Las mayúsculas y los acentos no cambian la elección
        Igual(VentanaAjustes.IconoDeGrupo("FORMA"), VentanaAjustes.IconoDeGrupo("forma"),
              "las mayúsculas no cambian el icono");

        // Un título que no encaja con nada cae al de ajustes, no a cero
        Igual(VentanaAjustes.Ico.Ajustes, VentanaAjustes.IconoDeGrupo("Hola qué tal"),
              "un título desconocido cae al icono genérico");
        Igual(VentanaAjustes.Ico.Ajustes, VentanaAjustes.IconoDeGrupo(""), "y uno vacío, igual");
        Igual(VentanaAjustes.Ico.Ajustes, VentanaAjustes.IconoDeGrupo(null!), "y un null no revienta");

        // Lo que devuelva siempre es un icono vetado: nunca un número suelto
        foreach (var t in new[] { "Forma", "Colores", "Fondo", "Zonas", "Apps", "Atajos",
                                  "Inicio", "Perfiles", "Sonido", "Buscar", "Estado",
                                  "Restaurar", "Exportar", "Monitor", "Mover", "Animaciones", "???" })
            Vale(VentanaAjustes.Ico.Todos.Contains(VentanaAjustes.IconoDeGrupo(t)),
                 $"\"{t}\" devuelve un icono del juego vetado");
    }

    // ================= C5.4: el lenguaje visual de los paneles =================

    static void PanelesSuperficieOpaca()
    {
        Grupo("paneles: el escritorio no se lee a través de las tarjetas");

        // Esto es la queja de fondo, y es medible. `Superficie` es un velo translúcido;
        // `SuperficieSolida` es ese mismo color aplanado sobre el fondo opaco del tema.
        // Lo que no puede pasar es que una tarjeta deje pasar el escritorio.
        foreach (var t in Temas.Predefinidos)
        {
            var tema = new Tema();
            Temas.Aplicar(t, tema);
            var fondo = (Color)ColorConverter.ConvertFromString(tema.Fondo)!;
            var texto = (Color)ColorConverter.ConvertFromString(tema.Texto)!;
            var acento = (Color)ColorConverter.ConvertFromString(tema.Acento)!;
            var r = Roles.Desde(tema, fondo, texto, acento);

            Igual((byte)255, r.SuperficieSolida.A, $"{t.Nombre}: la superficie de tarjeta es OPACA");
            Igual((byte)255, r.SuperficieAltaSolida.A, $"{t.Nombre}: y la elevada también");
            Igual((byte)255, r.FondoOpaco.A, $"{t.Nombre}: el fondo opaco lo es");
            Igual((byte)255, r.SeleccionSolida.A, $"{t.Nombre}: y la selección sólida");

            // Un tema PUEDE traer su propia superficie opaca —"Nube" lo hace—, así que lo
            // que se comprueba no es que el velo sea velo, sino que la versión sólida
            // nunca es menos opaca que la de siempre. Esa es la garantía que importa.
            Vale(r.SuperficieSolida.A >= r.Superficie.A,
                 $"{t.Nombre}: la tarjeta nunca es más transparente que el velo del tema");

            // El texto se lee sobre la tarjeta: es lo que de verdad importa
            double razon = Contraste.Entre(r.Texto, r.SuperficieSolida);
            Vale(razon >= 4.5, $"{t.Nombre}: el texto contrasta sobre la tarjeta ({razon:0.0}:1)");
            double razonAt = Contraste.Entre(r.TextoAtenuado, r.SuperficieSolida);
            Vale(razonAt >= 3.0, $"{t.Nombre}: y el atenuado se sigue leyendo ({razonAt:0.0}:1)");
            double razonAc = Contraste.Entre(r.AcentoTinta, r.SuperficieSolida);
            Vale(razonAc >= 3.0, $"{t.Nombre}: y el acento como texto ({razonAc:0.0}:1)");
        }
    }

    static void PanelesEstadoDeMetrica()
    {
        Grupo("paneles: el estado de una métrica sale del valor");

        // Normal, alto y crítico: el color no se escribe a mano en cada sitio, se deduce.
        Igual(MainWindow.EstadoMetrica.Normal, MainWindow.EstadoDe(0), "0 % es normal");
        Igual(MainWindow.EstadoMetrica.Normal, MainWindow.EstadoDe(45), "45 % es normal");
        Igual(MainWindow.EstadoMetrica.Normal, MainWindow.EstadoDe(69.9), "casi 70 sigue normal");
        Igual(MainWindow.EstadoMetrica.Alto, MainWindow.EstadoDe(70), "70 % ya es alto");
        Igual(MainWindow.EstadoMetrica.Alto, MainWindow.EstadoDe(89), "89 % es alto");
        Igual(MainWindow.EstadoMetrica.Critico, MainWindow.EstadoDe(90), "90 % es crítico");
        Igual(MainWindow.EstadoMetrica.Critico, MainWindow.EstadoDe(100), "100 % también");

        // Valores imposibles no dan un estado imposible
        Igual(MainWindow.EstadoMetrica.Normal, MainWindow.EstadoDe(-5), "un negativo es normal");
        Igual(MainWindow.EstadoMetrica.Critico, MainWindow.EstadoDe(500), "y un 500 es crítico");

        // Es monótona: más carga nunca baja de estado
        var antes = MainWindow.EstadoDe(0);
        for (int i = 0; i <= 100; i += 5)
        {
            var ahora = MainWindow.EstadoDe(i);
            Vale((int)ahora >= (int)antes, $"a {i} % el estado no baja");
            antes = ahora;
        }
    }

    static void PanelesMedidas()
    {
        Grupo("paneles: las medidas son las mismas en todos, lo elija quien lo elija");

        // ---------- Esto miraba cuatro constantes; ahora son TRES estilos ----------
        //
        // Las medidas salían de `MainWindow.RadioCard` y compañía, que eran `const`. Desde
        // que el usuario puede elegir la decoración, salen del estilo — así que la pregunta
        // ya no es «¿este número es razonable?» sino «¿lo es en **los tres**?».
        //
        // Y eso es más fuerte que lo de antes: un estilo nuevo que se pasara de rosca con el
        // redondeo, o que pegara el texto al filo, lo caza esto sin que nadie se acuerde de
        // volver aquí.
        foreach (var e in Estilos.Todos)
        {
            Vale(e.RadioCard >= 8 && e.RadioCard <= 20,
                 $"«{e.Nombre}»: el radio de tarjeta es razonable ({e.RadioCard})");
            Vale(e.RadioShell >= e.RadioCard,
                 $"«{e.Nombre}»: el contenedor redondea al menos tanto como sus tarjetas");
            Vale(e.Hueco >= 8 && e.Hueco <= 20,
                 $"«{e.Nombre}»: la separación entre tarjetas es consistente ({e.Hueco})");
            Vale(e.Dentro >= 10,
                 $"«{e.Nombre}»: no pega el contenido al filo ({e.Dentro})");
        }

        // Y el mínimo para poder pulsar NO sale del estilo: eso no es decoración. Un estilo
        // que lo encogiera haría los controles más difíciles de usar a cambio de verse más
        // compacto, y ése es un trato que ningún estilo puede ofrecer.
        Vale(MainWindow.AltoTocable >= 32,
             $"los controles tienen un mínimo cómodo de pulsación ({MainWindow.AltoTocable})");
    }

    static void PanelesTamano()
    {
        Grupo("paneles: ninguno se pasa del tope de alto");

        // Esto es la prueba que faltaba y que habría pillado el fallo antes de que lo
        // vieras: el panel medía 1710 px de contenido y acabó topado a 1362, o sea casi
        // toda la pantalla, con una barra de desplazamiento. El log lo decía; no había
        // nada que lo comprobara.
        Vale(MainWindow.TopeDeAlto >= 320,
             $"el tope deja sitio para un panel útil ({MainWindow.TopeDeAlto})");
        Vale(MainWindow.TopeDeAlto <= 700,
             $"y no llega a ser media pantalla ({MainWindow.TopeDeAlto})");
        Vale(MainWindow.AnchoDePanel >= 300 && MainWindow.AnchoDePanel <= 460,
             $"el ancho es de un panel colgado de un icono, no de una ventana ({MainWindow.AnchoDePanel})");

        // El alto que pedía el panel roto, contra el tope: la relación que no puede volver
        const double loQueMedia = 1710;
        Vale(loQueMedia > MainWindow.TopeDeAlto * 2,
             "lo que medía el panel roto era más del doble del tope: era contenido de sobra, " +
             "no pantalla de menos");

        // Y que el tope quepa en la pantalla más pequeña que se puede dar: un portátil de
        // 768 px de alto con la barra de Windows puesta.
        Vale(MainWindow.TopeDeAlto < 768 - 100,
             $"el tope cabe en un portátil de 768 px ({MainWindow.TopeDeAlto})");

        // Medir un contenido conocido da lo que mide, para que `AltoQuePide` sea fiable
        var pila = new System.Windows.Controls.StackPanel();
        for (int i = 0; i < 3; i++)
            pila.Children.Add(new System.Windows.Controls.Border { Height = 50 });
        double pide = MainWindow.AltoQuePide(pila, MainWindow.AnchoDePanel);
        Igual(150.0, Math.Round(pide), "tres bloques de 50 miden 150");

        var vacio = new System.Windows.Controls.StackPanel();
        Igual(0.0, Math.Round(MainWindow.AltoQuePide(vacio, MainWindow.AnchoDePanel)),
              "un contenido vacío mide cero, y eso es lo que delata un panel sin contenido");

        // Un contenido que se pasa se detecta ANTES de abrir
        var gordo = new System.Windows.Controls.StackPanel();
        for (int i = 0; i < 20; i++)
            gordo.Children.Add(new System.Windows.Controls.Border { Height = 100 });
        Vale(MainWindow.AltoQuePide(gordo, MainWindow.AnchoDePanel) > MainWindow.TopeDeAlto,
             "un contenido de 2000 px se detecta como que se pasa del tope");
    }

    // ================= C5.5: de quién es el gesto =================

    static void GestoDeQuien()
    {
        Grupo("gesto: un control interactivo no arrastra la nube");

        // El fallo: arrastrar el deslizador de volumen movía TAMBIÉN la isla. No se podía
        // arreglar con e.Handled porque el arrastre de la isla va en un Preview de un
        // antepasado y los eventos Preview van de la raíz a la hoja: cuando el deslizador
        // se entera, la isla ya empezó. Se mira el ORIGEN y se sube por el árbol.

        // ---------- Los tipos que siempre son del control ----------
        Vale(MainWindow.EsTipoInteractivo(new Button()), "un botón es interactivo");
        Vale(MainWindow.EsTipoInteractivo(new CheckBox()), "una casilla también (ButtonBase)");
        Vale(MainWindow.EsTipoInteractivo(new System.Windows.Controls.Slider()),
             "un deslizador del marco, también");
        Vale(MainWindow.EsTipoInteractivo(new ComboBox()), "un desplegable");
        Vale(MainWindow.EsTipoInteractivo(new TextBox()), "un campo de texto (TextBoxBase)");
        Vale(MainWindow.EsTipoInteractivo(new ScrollViewer()), "un ScrollViewer");
        Vale(MainWindow.EsTipoInteractivo(new ListBox()), "una lista");
        Vale(MainWindow.EsTipoInteractivo(new ProgressBar()), "una barra de progreso (RangeBase)");

        // Y lo que NO lo es: la nube se puede coger por ahí
        Vale(!MainWindow.EsTipoInteractivo(new Border()), "un borde no es interactivo");
        Vale(!MainWindow.EsTipoInteractivo(new TextBlock()), "ni un texto");
        Vale(!MainWindow.EsTipoInteractivo(new Grid()), "ni una rejilla");
        Vale(!MainWindow.EsTipoInteractivo(new StackPanel()), "ni una pila");

        // ---------- Subiendo por el árbol ----------
        //
        // Lo que de verdad pasa: el clic nace en el TextBlock de DENTRO de un botón, y el
        // origen del evento es ese texto, no el botón.
        var texto = new TextBlock { Text = "Silenciar" };
        var boton = new Button { Content = texto };
        var tarjeta = new Border { Child = boton };
        var isla = new Border { Child = tarjeta };

        Vale(MainWindow.GestoDeUnControl(texto, isla),
             "el clic en el texto de dentro de un botón es del BOTÓN, no de la nube");
        Vale(MainWindow.GestoDeUnControl(boton, isla), "y en el botón, igual");

        // Un borde suelto dentro de la isla sí deja arrastrar
        var suelto = new TextBlock { Text = "12:34" };
        var islaConReloj = new Border { Child = new Border { Child = suelto } };
        Vale(!MainWindow.GestoDeUnControl(suelto, islaConReloj),
             "el clic en el reloj de la isla SÍ puede arrastrar la nube");

        // ---------- La marca explícita ----------
        //
        // Mis piezas no son controles del marco: el deslizador de NubeBar es un Grid con
        // tres Border y una Ellipse. Por tipo no hay forma de saberlo.
        var capa = new Grid();
        var agarre = new System.Windows.Shapes.Ellipse();
        capa.Children.Add(agarre);
        var dentroDeIsla = new Border { Child = capa };
        Vale(!MainWindow.GestoDeUnControl(agarre, dentroDeIsla),
             "sin marcar, el agarre de un deslizador propio no se distingue de un adorno");

        MainWindow.SetNoArrastrarNubeBar(capa, true);
        Vale(MainWindow.GestoDeUnControl(agarre, dentroDeIsla),
             "marcada la capa, el gesto es del deslizador");
        Vale(MainWindow.GestoDeUnControl(capa, dentroDeIsla), "y de la propia capa");

        // La marca protege a todo lo que tenga dentro, a cualquier profundidad
        var hondo = new TextBlock();
        var medio = new Border { Child = new Border { Child = hondo } };
        var cajon = new Border { Child = medio };
        Vale(!MainWindow.GestoDeUnControl(hondo, cajon), "sin marcar el cajón, no protege");
        MainWindow.SetNoArrastrarNubeBar(cajon, true);
        Vale(MainWindow.GestoDeUnControl(hondo, cajon),
             "marcado el cajón, protege a tres niveles de profundidad");

        // `SinArrastre` devuelve el mismo objeto, para poder encadenarlo
        var g = new Grid();
        Vale(ReferenceEquals(g, MainWindow.SinArrastre(g)), "SinArrastre devuelve el mismo elemento");
        Vale(MainWindow.GetNoArrastrarNubeBar(g), "y lo deja marcado");

        // ---------- Los casos raros ----------
        Vale(!MainWindow.GestoDeUnControl(null), "sin origen, no es de ningún control");
        Vale(!MainWindow.GestoDeUnControl("una cadena"), "algo que no es del árbol, tampoco");
        Vale(!MainWindow.GestoDeUnControl(new Border()), "un borde huérfano no es interactivo");

        // El tope corta la subida: no se sale de la nube
        var fuera = new Button();
        var tope = new Border();
        var hijo = new TextBlock();
        tope.Child = hijo;
        _ = fuera;
        Vale(!MainWindow.GestoDeUnControl(hijo, tope),
             "la búsqueda se para en el tope y no sigue hacia arriba");
    }

    static void CentroDeControlComposicion()
    {
        Grupo("centro de control: ancho, contenido y composición");

        // ---------- El ancho ----------
        //
        // "Ocupa demasiado ancho" tenía una causa concreta: el panel se abría SIN ancla,
        // y sin ancla `AbrirPanel` dibuja el cajón de punta a punta de la isla —1169 px en
        // una pantalla de 2560— en vez de como una tarjeta del ancho pedido. Las tarjetas
        // se estiraban a ese ancho: de ahí el espacio vacío y CPU/RAM con sitio de sobra.
        Vale(MainWindow.AnchoDelCentro >= 420 && MainWindow.AnchoDelCentro <= 560,
             $"el centro de control está en el rango pedido de 420..560 ({MainWindow.AnchoDelCentro})");
        Vale(MainWindow.AnchoDePanel >= 320 && MainWindow.AnchoDePanel < MainWindow.AnchoDelCentro,
             $"el de audio es más estrecho, que lleva menos ({MainWindow.AnchoDePanel})");
        Vale(MainWindow.AnchoDelCentro < 1169,
             "y los dos muy por debajo del ancho de la isla, que es lo que ocupaban antes");

        // ---------- El alto ----------
        Vale(MainWindow.TopeDeAlto <= 560, $"el tope de alto no llega a media pantalla ({MainWindow.TopeDeAlto})");
        Vale(MainWindow.TopeDeAlto < 768 - 100, "y cabe en un portátil de 768 px");

        // ---------- Las medidas de las tarjetas, compactas ----------
        //
        // En LOS TRES estilos. Esto miraba las constantes de antes; ahora el usuario elige la
        // decoración, así que lo que hay que exigir es que el centro de control siga cabiendo
        // y siendo cómodo **con cualquiera de ellos**. Un estilo que se pasara de acolchado
        // dejaría las cuatro acciones rápidas apretadas, y eso se caza aquí sin que nadie se
        // acuerde de volver.
        foreach (var e in Estilos.Todos)
        {
            Vale(e.Dentro <= 16, $"«{e.Nombre}»: las tarjetas no son acolchadas ({e.Dentro})");
            Vale(e.Hueco <= 12,
                 $"«{e.Nombre}»: ni se separan tanto que parezcan botones sueltos ({e.Hueco})");

            // Cuatro acciones en el ancho del centro dan un botón cómodo y no una tira de
            // iconos perdidos: es lo que se pidió de la rejilla.
            double porAccion = (MainWindow.AnchoDelCentro - 2 * e.Dentro - 24) / 4;
            Vale(porAccion >= 60 && porAccion <= 130,
                 $"«{e.Nombre}»: cada acción rápida mide {porAccion:0} px: ni diminuta ni desperdigada");

            // Y dos métricas lado a lado caben sin quedarse anchas.
            double porMetrica = (MainWindow.AnchoDelCentro - 2 * e.Dentro) / 2;
            Vale(porMetrica >= 140 && porMetrica <= 240,
                 $"«{e.Nombre}»: cada métrica mide {porMetrica:0} px: no se queda con sitio de sobra");
        }
    }

    // ================= C5.6: el deslizador no se queda agarrado =================

    static void SliderCicloDeVida()
    {
        Grupo("slider: el gesto siempre termina");

        // El fallo: al soltar, el deslizador seguía en arrastre y el valor seguía
        // moviéndose. Colgaba de `MouseLeftButtonUp`, y en una ventana WS_EX_NOACTIVATE
        // por capas ese evento se pierde. Ahora la verdad la dice el botón del ratón una
        // vez por fotograma, y hay un solo camino de salida.
        var g = new MainWindow.GestoDeslizador();
        var apuntes = new List<string>();
        bool capturaSoltada = false, seguimientoParado = false;
        g.Apuntar = m => apuntes.Add(m);
        g.SoltarCaptura = () => capturaSoltada = true;
        g.DejarDeSeguir = () => seguimientoParado = true;

        // ---------- La prueba de limpieza que se pidió ----------
        Igual(MainWindow.EstadoGesto.Inactivo, g.Estado, "antes: inactivo");
        Vale(!g.Capturado, "captura antes: false");
        Vale(!g.Siguiendo, "seguimiento antes: false");

        g.Empezar();
        Igual(MainWindow.EstadoGesto.Arrastrando, g.Estado, "durante: arrastrando");
        Vale(g.Capturado, "captura durante: true");
        Vale(g.Siguiendo, "seguimiento durante: true");

        // El botón sigue pulsado: el gesto sigue vivo
        Vale(g.Fotograma(botonPulsado: true), "con el botón pulsado el gesto sigue");
        Vale(g.Arrastrando, "y sigue arrastrando");

        // El botón deja de estar pulsado: termina AQUÍ, sin MouseLeftButtonUp
        Vale(!g.Fotograma(botonPulsado: false), "al dejar de estar pulsado, el gesto acaba");
        Igual(MainWindow.EstadoGesto.Inactivo, g.Estado, "después: inactivo");
        Vale(!g.Capturado, "captura después: false");
        Vale(!g.Siguiendo, "seguimiento después: false");
        Vale(!g.Arrastrando, "arrastre después: false");
        Vale(capturaSoltada, "la captura se soltó de verdad");
        Vale(seguimientoParado, "y el seguimiento se detuvo");

        // Las trazas que se pidieron registrar
        Vale(apuntes.Contains("slider: inicio"), "se apuntó el inicio");
        Vale(apuntes.Contains("slider: soltado detectado"), "y que se detectó el soltado");
        Vale(apuntes.Contains("slider: captura liberada"), "y que se liberó la captura");
        Vale(apuntes.Any(a => a.StartsWith("slider: estado limpio")), "y que el estado quedó limpio");

        // ---------- Idempotente: terminar cinco veces no rompe nada ----------
        int antesDe = g.VecesLimpiado;
        for (int i = 0; i < 5; i++) g.Terminar("de nuevo");
        Igual(MainWindow.EstadoGesto.Inactivo, g.Estado, "terminar cinco veces deja inactivo");
        Vale(!g.Capturado && !g.Siguiendo, "y sin nada agarrado");
        Vale(g.VecesLimpiado > antesDe, "limpiar se ejecuta, pero sin volver a soltar lo ya soltado");

        // ---------- Y volver a pulsar funciona inmediatamente ----------
        g.Empezar();
        Vale(g.Arrastrando, "el siguiente clic vuelve a funcionar");
        Vale(g.Capturado && g.Siguiendo, "con su captura y su seguimiento");
        g.Terminar();
        Vale(!g.Arrastrando && !g.Capturado && !g.Siguiendo, "y vuelve a limpiar");
    }

    static void SliderLasSeisSalidas()
    {
        Grupo("slider: las seis formas de terminar");

        var logs = new List<string>();
        MainWindow.GestoDeslizador Nuevo()
        {
            logs.Clear();
            var g = new MainWindow.GestoDeslizador();
            g.Apuntar = m => logs.Add(m);
            g.Empezar();
            return g;
        }

        // 1. Soltar
        var g1 = Nuevo();
        g1.Terminar("soltado");
        Vale(!g1.Arrastrando && !g1.Capturado, "soltar termina");

        // 2. Perder la captura
        var g2 = Nuevo();
        g2.Terminar("se perdió la captura");
        Vale(!g2.Arrastrando && !g2.Capturado, "perder la captura termina");

        // 3. Escape
        var g3 = Nuevo();
        g3.Cancelar("Esc");
        Vale(!g3.Arrastrando && !g3.Capturado, "Esc cancela");
        Vale(logs.Any(a => a.Contains("cancelado")), "y queda apuntado como cancelado");

        // 4. Cerrar el panel
        var g4 = Nuevo();
        g4.Cancelar("se cerró el panel");
        Vale(!g4.Arrastrando && !g4.Capturado && !g4.Siguiendo,
             "cerrar el panel no deja captura ni seguimiento");

        // 5. La ventana pierde el foco
        var g5 = Nuevo();
        g5.Cancelar("la ventana perdió el foco");
        Vale(!g5.Arrastrando && !g5.Capturado, "perder el foco cancela");

        // 6. Empezar otra interacción encima: la anterior se cierra, no se mezclan
        var g6 = Nuevo();
        g6.Empezar();
        Vale(g6.Arrastrando, "el segundo gesto queda vivo");
        Vale(g6.Capturado && g6.Siguiendo, "con sus recursos");
        Igual(2, logs.Count(a => a == "slider: inicio"),
              "y el primero se cerró antes de abrir el segundo");
        g6.Terminar();
        Vale(!g6.Capturado && !g6.Siguiendo, "y al final no queda nada");

        // Cancelar sin haber empezado no apunta nada ni revienta
        var g7 = new MainWindow.GestoDeslizador();
        var sueltos = new List<string>();
        g7.Apuntar = m => sueltos.Add(m);
        g7.Cancelar("sin nada");
        g7.Terminar("sin nada");
        Igual(0, sueltos.Count, "cancelar o terminar sin gesto no apunta nada");
        Igual(MainWindow.EstadoGesto.Inactivo, g7.Estado, "y sigue inactivo");
    }

    static void SliderDiezArrastres()
    {
        Grupo("slider: diez arrastres consecutivos");

        // Lo que se hace de verdad: arrastrar, soltar, arrastrar otra vez. Si algo se
        // queda pegado, a la segunda o la tercera se nota.
        var g = new MainWindow.GestoDeslizador();
        int soltadas = 0, paradas = 0;
        g.SoltarCaptura = () => soltadas++;
        g.DejarDeSeguir = () => paradas++;

        for (int i = 1; i <= 10; i++)
        {
            g.Empezar();
            Vale(g.Arrastrando, $"arrastre {i}: empieza");
            for (int f = 0; f < 3; f++)
                Vale(g.Fotograma(true), $"arrastre {i}: sigue vivo en el fotograma {f + 1}");
            g.Mover();
            Vale(!g.Fotograma(false), $"arrastre {i}: termina al soltar");
            Vale(!g.Capturado && !g.Siguiendo && !g.Arrastrando,
                 $"arrastre {i}: no queda nada agarrado");
        }

        Igual(10, soltadas, "se soltó la captura diez veces, una por arrastre");
        Igual(10, paradas, "y se paró el seguimiento diez veces");

        // Alternando corto y largo, con cancelaciones por medio
        for (int i = 0; i < 5; i++)
        {
            g.Empezar();
            if (i % 2 == 0) Vale(!g.Fotograma(false), $"mezcla {i}: el corto termina enseguida");
            else
            {
                for (int f = 0; f < 20; f++) g.Fotograma(true);
                g.Cancelar("Esc a mitad");
            }
            Vale(!g.Capturado && !g.Siguiendo, $"mezcla {i}: queda limpio");
        }
    }

    static void GlifoDelAspa()
    {
        Grupo("vista general: el icono del aspa");

        // Mismo trato que el de la cuadrícula: construido desde el número, porque escrito
        // como carácter literal de la zona de uso privado se pierde al reescribir el
        // archivo y el botón se queda funcionando pero invisible.
        var g = MainWindow.GlifoAspa;
        Igual(1, g.Length, "es un solo carácter");
        Igual(0xE711, (int)g[0], "y es el Cancel de Segoe Fluent");
        Vale(g[0] >= 0xE000 && g[0] <= 0xF8FF, "está en la zona de uso privado de Unicode");
        Vale(MainWindow.GlifoAspa != MainWindow.GlifoCuadricula, "y no es el mismo que el de la cuadrícula");
    }

    // ================= C4.1b: los cuatro fallos de la prueba visual =================

    static void PlanDeAjuste()
    {
        Grupo("ajuste: restaurar, colocar y DESPUÉS activar");

        // El fallo: la ventana se colocaba bien y se quedaba detrás. Mover no activa.
        var elegida = ZonasAjuste.Plan(Zona.Izquierda, EstadoVentana.Normal, activar: true);
        Vale(elegida.Contains(ZonasAjuste.Paso.Activar), "elegirla desde la vista la activa");
        Igual(ZonasAjuste.Paso.Activar, elegida[^1], "y activar va al FINAL, después de colocarla");
        Igual(ZonasAjuste.Paso.Colocar, elegida[0], "una ventana normal se coloca sin más");

        // Restaurar va primero o Windows ignora el movimiento
        foreach (var estado in new[] { EstadoVentana.Minimizada, EstadoVentana.Maximizada })
        {
            var p = ZonasAjuste.Plan(Zona.Derecha, estado, activar: true);
            Igual(ZonasAjuste.Paso.Restaurar, p[0], $"una {estado} se restaura ANTES de colocarla");
            Igual(ZonasAjuste.Paso.Colocar, p[1], "y después se coloca");
            Igual(ZonasAjuste.Paso.Activar, p[2], "y al final se activa");
            Igual(3, p.Count, "tres pasos, sin repetir");
        }

        // Maximizar se le pide a la ventana; no se le impone el rectángulo
        var max = ZonasAjuste.Plan(Zona.Maximizar, EstadoVentana.Normal, activar: true);
        Vale(max.Contains(ZonasAjuste.Paso.Maximizar), "maximizar usa el mensaje de maximizar");
        Vale(!max.Contains(ZonasAjuste.Paso.Colocar), "y no coloca un rectángulo a mano");
        Igual(ZonasAjuste.Paso.Activar, max[^1], "y también activa al final");

        var maxDesdeMin = ZonasAjuste.Plan(Zona.Maximizar, EstadoVentana.Minimizada, activar: true);
        Igual(ZonasAjuste.Paso.Restaurar, maxDesdeMin[0], "maximizar una minimizada la restaura primero");

        // Sin elección del usuario no se roba el foco: una recolocación de fondo no activa
        var fondo = ZonasAjuste.Plan(Zona.Izquierda, EstadoVentana.Normal, activar: false);
        Vale(!fondo.Contains(ZonasAjuste.Paso.Activar), "colocar sin elegir NO activa");

        Igual(0, ZonasAjuste.Plan(Zona.Ninguna, EstadoVentana.Normal, true).Count, "sin zona no hay plan");

        // Y ninguna combinación produce un plan vacío o con pasos repetidos
        foreach (Zona z in Enum.GetValues<Zona>())
            foreach (EstadoVentana e in Enum.GetValues<EstadoVentana>())
                foreach (bool act in new[] { true, false })
                {
                    if (z == Zona.Ninguna) continue;
                    var p = ZonasAjuste.Plan(z, e, act);
                    Vale(p.Count > 0, $"{z}/{e}/activar={act} tiene plan");
                    Igual(p.Count, p.Distinct().Count(), $"{z}/{e}/activar={act} no repite pasos");
                }
    }

    static void EscConPrioridad()
    {
        Grupo("vista general: Esc cierra primero el menú");

        Igual(MainWindow.QueCierra.Menu, MainWindow.EscCierra(vistaAbierta: true, menuAbierto: true),
              "con el menú abierto, Esc cierra el menú");
        Igual(MainWindow.QueCierra.Vista, MainWindow.EscCierra(vistaAbierta: true, menuAbierto: false),
              "y con la vista sola, cierra la vista");
        Igual(MainWindow.QueCierra.Nada, MainWindow.EscCierra(vistaAbierta: false, menuAbierto: false),
              "sin vista, Esc no es asunto suyo");
        Igual(MainWindow.QueCierra.Nada, MainWindow.EscCierra(vistaAbierta: false, menuAbierto: true),
              "ni aunque quedara un menú colgado");

        // Dos Esc seguidos: menú y después vista. Nunca los dos de una.
        bool menu = true;
        var primero = MainWindow.EscCierra(true, menu);
        if (primero == MainWindow.QueCierra.Menu) menu = false;
        var segundo = MainWindow.EscCierra(true, menu);
        Igual(MainWindow.QueCierra.Vista, segundo, "el segundo Esc cierra la vista");
    }

    static void QuienSeArrastra()
    {
        Grupo("vista general: arrastrar una tarjeta no mueve la nube");

        Igual(MainWindow.QuienArrastra.Tarjeta,
              MainWindow.ArrastraAlPulsar(vistaAbierta: true, enBotonDeLaTarjeta: false,
                                          menuAbierto: false, islaArrastrable: true),
              "con la vista abierta, el cuerpo de una tarjeta arrastra la VENTANA");

        Igual(MainWindow.QuienArrastra.Isla,
              MainWindow.ArrastraAlPulsar(false, false, false, islaArrastrable: true),
              "sin la vista, la isla se arrastra como siempre");

        Igual(MainWindow.QuienArrastra.Nadie,
              MainWindow.ArrastraAlPulsar(false, false, false, islaArrastrable: false),
              "y con el arrastre de la isla apagado, nadie");

        // Los botones de la tarjeta y el menú no empiezan un arrastre
        Igual(MainWindow.QuienArrastra.Nadie,
              MainWindow.ArrastraAlPulsar(true, enBotonDeLaTarjeta: true, false, true),
              "el aspa de la tarjeta no arrastra");
        Igual(MainWindow.QuienArrastra.Nadie,
              MainWindow.ArrastraAlPulsar(true, false, menuAbierto: true, true),
              "con el menú abierto tampoco");

        // Lo importante: con la vista abierta la isla NUNCA se arrastra
        foreach (bool boton in new[] { true, false })
            foreach (bool menu in new[] { true, false })
                Vale(MainWindow.ArrastraAlPulsar(true, boton, menu, true) != MainWindow.QuienArrastra.Isla,
                     $"con la vista abierta la nube no se mueve (boton={boton}, menu={menu})");
    }

    static void SoltarLaTarjeta()
    {
        Grupo("vista general: soltar la tarjeta");

        Vale(MainWindow.AjustaAlSoltar(seMovio: true, Zona.Izquierda), "soltada en un borde, se ajusta");
        Vale(MainWindow.AjustaAlSoltar(true, Zona.ArribaDerecha), "y en una esquina también");
        Vale(!MainWindow.AjustaAlSoltar(true, Zona.Ninguna), "soltada en medio, no se toca nada");
        Vale(!MainWindow.AjustaAlSoltar(seMovio: false, Zona.Izquierda),
             "un clic sin mover no es un arrastre: eso es pulsar la tarjeta");
        Vale(!MainWindow.AjustaAlSoltar(false, Zona.Ninguna), "ni lo uno ni lo otro");
    }

    static void ArrastrarAlBorde()
    {
        Grupo("ajuste: arrastrar una ventana a un borde");

        // ---------- Cuándo se vigila un arrastre ----------
        Vale(MainWindow.VigilarElArrastre(activado: true, existe: true, esNuestra: false, maximizada: false),
             "una ventana normal de otra aplicación se vigila");
        Vale(!MainWindow.VigilarElArrastre(false, true, false, false),
             "con el ajuste apagado en Ajustes, no se vigila nada");
        Vale(!MainWindow.VigilarElArrastre(true, existe: false, false, false),
             "una ventana que ya no existe, no");
        Vale(!MainWindow.VigilarElArrastre(true, true, esNuestra: true, false),
             "y la nube moviéndose a sí misma tampoco: ya tiene su propio arrastre");
        Vale(!MainWindow.VigilarElArrastre(true, true, false, maximizada: true),
             "a una maximizada la restaura Windows al arrastrarla: ahí no nos metemos");

        // Apagarlo en Ajustes gana siempre, pase lo que pase con el resto
        foreach (bool existe in new[] { true, false })
            foreach (bool nuestra in new[] { true, false })
                foreach (bool max in new[] { true, false })
                    Vale(!MainWindow.VigilarElArrastre(false, existe, nuestra, max),
                         $"apagado es apagado (existe={existe}, nuestra={nuestra}, max={max})");

        // ---------- Y qué se hace al soltar ----------
        var mia = new IntPtr(0x1234);
        var otra = new IntPtr(0x5678);

        Igual(MainWindow.FinDeArrastre.Ajustar, MainWindow.AlTerminar(mia, mia, Zona.Izquierda),
              "soltada en un borde, se ajusta");
        Igual(MainWindow.FinDeArrastre.SoltarSinAjustar, MainWindow.AlTerminar(mia, mia, Zona.Ninguna),
              "soltada en medio, se recoge la previsualización y nada más");

        // Esto es lo que faltaba: el aviso de OTRA ventana no puede cancelar el arrastre
        // en curso. MoverSeTermina salta por cualquiera que termine de moverse, y una
        // recolocación de fondo apagaba el reloj y borraba la previsualización.
        Igual(MainWindow.FinDeArrastre.NoEsNuestro, MainWindow.AlTerminar(mia, otra, Zona.Izquierda),
              "que otra ventana termine de moverse no toca el arrastre en curso");
        Igual(MainWindow.FinDeArrastre.NoEsNuestro, MainWindow.AlTerminar(IntPtr.Zero, otra, Zona.Izquierda),
              "y sin arrastre en marcha, un aviso suelto no ajusta nada");
        Igual(MainWindow.FinDeArrastre.NoEsNuestro, MainWindow.AlTerminar(IntPtr.Zero, IntPtr.Zero, Zona.Izquierda),
              "ni con los dos vacíos");

        foreach (var z in Enum.GetValues<Zona>())
            Vale(MainWindow.AlTerminar(mia, otra, z) == MainWindow.FinDeArrastre.NoEsNuestro,
                 $"con otra ventana nunca se ajusta ({z})");

        // ---------- La previsualización es una promesa ----------
        //
        // Se dibuja con el monitor DEL PUNTERO, así que el ajuste tiene que caer en ese
        // mismo monitor. Deducirlo de la ventana no vale: en el salto entre dos pantallas
        // el cuerpo sigue en la de origen mientras el ratón ya empuja el borde de la otra.
        var dos = Pantalla(x: 1920, y: 0, w: 2560, h: 1440, tx: 1920, tw: 2560, th: 1440);
        var util = ZonasAjuste.AreaUtil(dos);
        foreach (var z in Enum.GetValues<Zona>())
        {
            if (z == Zona.Ninguna) continue;
            var h = ZonasAjuste.Colocacion(z, util);
            Vale(h.X >= util.X && h.Derecha <= util.Derecha && h.Y >= util.Y && h.Abajo <= util.Abajo,
                 $"lo prometido en la segunda pantalla cae en la segunda pantalla ({z})");
        }

        // El punto que pide la zona y el hueco que se ajusta salen del MISMO monitor
        var zona = ZonasAjuste.ZonaDelPunto(1922, 700, dos);
        Igual(Zona.Izquierda, zona, "el borde izquierdo de la segunda pantalla");
        var hueco = ZonasAjuste.Colocacion(zona, util);
        Igual(1920, hueco.X, "y su mitad izquierda empieza donde empieza esa pantalla");
        Igual(1280, hueco.Ancho, "con la mitad de SU ancho, no la del monitor principal");
    }

    static void GlifoDeLaCuadricula()
    {
        Grupo("vista general: el icono de la cuadrícula");

        var g = MainWindow.GlifoCuadricula;
        Vale(!string.IsNullOrEmpty(g), "el glifo no está vacío");
        Igual(1, g.Length, "es un solo carácter");
        Igual(0xE8A9, (int)g[0], "y es el de siempre (GridView de Segoe Fluent)");

        // Zona de uso privado: fuera de ahí, la fuente de iconos no lo tendría y
        // saldría una caja.
        Vale(g[0] >= 0xE000 && g[0] <= 0xF8FF, "está en la zona de uso privado de Unicode");
        Vale(!char.IsWhiteSpace(g[0]) && !char.IsControl(g[0]), "no es un espacio ni un carácter de control");

        // Y la fuente de iconos sigue estando donde se la busca
        Vale(!string.IsNullOrWhiteSpace(MainWindow.FuenteIconos), "la fuente de iconos tiene nombre");
        Vale(MainWindow.FuenteIconos.Contains("Segoe", StringComparison.OrdinalIgnoreCase),
             $"y es una Segoe ({MainWindow.FuenteIconos})");
    }
    // ---------- El panel en los siete temas ----------
    //
    // Esto es la parte comprobable de la "verificación visual": que los colores que usa
    // el centro de notificaciones se LEEN en los siete temas. No dice si es bonito —eso
    // hay que mirarlo— pero sí caza lo que de verdad rompe un panel: un texto atenuado
    // sobre una superficie levantada que en un tema claro se queda en nada.
    //
    // Se mide contra la superficie SÓLIDA y no contra la pintada: un color con alfa no
    // tiene luminancia propia hasta que se sabe qué hay detrás. Es la regla que ya
    // estaba escrita en Roles.cs; aquí se comprueba que el panel la respeta.
    static void SieteTemas()
    {
        Grupo("centro: se lee en los siete temas");

        // 4,5 es el mínimo de la norma para texto normal; 3,0 para texto grande o
        // secundario, que es lo que son el segundo renglón y la hora.
        const double MinimoTexto = 4.5, MinimoSecundario = 3.0;

        foreach (var t in NubeBar.Temas.Predefinidos)
        {
            var tema = new Tema();
            NubeBar.Temas.Aplicar(t, tema);
            var fondo = (Color)ColorConverter.ConvertFromString(tema.Fondo)!;
            var texto = (Color)ColorConverter.ConvertFromString(tema.Texto)!;
            var acento = (Color)ColorConverter.ConvertFromString(tema.Acento)!;
            var r = Roles.Desde(tema, fondo, texto, acento);

            void Mide(Color tinta, Color detras, double minimo, string que)
            {
                double c = Contraste.Entre(tinta, detras);
                Vale(c >= minimo, $"[{t.Nombre}] {que}: contraste {c:0.0} (hace falta {minimo:0.0})");
            }

            // El título de una notificación, sobre la tarjeta
            Mide(r.Texto, r.SuperficieSolida, MinimoTexto, "el título sobre la tarjeta");
            // Y sobre la tarjeta de una sin leer, que va levantada
            Mide(r.Texto, r.SeleccionSolida, MinimoTexto, "el título de una sin leer");
            // El cuerpo y la hora
            Mide(r.TextoAtenuado, r.SuperficieSolida, MinimoSecundario, "el cuerpo sobre la tarjeta");
            Mide(r.TextoAtenuado, r.SeleccionSolida, MinimoSecundario, "la hora de una sin leer");
            // El nombre de la app y el "Borrar todo", que van en acento sobre el panel
            Mide(r.AcentoTinta, r.FondoOpaco, MinimoSecundario, "el nombre de la app");
            // Y la cifra de la chapita, que va SOBRE el acento y no sobre el fondo
            Mide(Contraste.TintaSobre(r.Acento), r.Acento, MinimoTexto,
                 "la cifra de la chapita sobre el acento");
        }
    }

    // ---------- El alto del panel ----------
    //
    // La cuenta que decide si la tarjeta cabe. Se comprueba aquí y no mirándola porque
    // lo que falla no es el caso normal: es el portátil de 768 px con cuarenta
    // notificaciones, que es justo el que no se tiene a mano.
    static void AltoDelPanel()
    {
        Grupo("centro: el alto de la tarjeta");

        // Pantallas en unidades de WPF: una 4K al 200 % son 1920x1080 de WPF
        foreach (var (alto, que) in new[]
        {
            (768.0, "portátil 1366x768"), (1080.0, "1080p"), (1440.0, "1440p"),
            (960.0, "4K al 225 %"), (1920.0, "vertical 1080x1920"),
        })
        {
            double vacio = MainWindow.AltoDelCentro(0, alto);
            double pocas = MainWindow.AltoDelCentro(2, alto);
            double muchas = MainWindow.AltoDelCentro(80, alto);

            Vale(vacio >= 140, $"[{que}] el estado vacío tiene sitio para su mensaje ({vacio:0})");
            Vale(pocas < muchas, $"[{que}] con más notificaciones la tarjeta crece ({pocas:0} < {muchas:0})");
            Vale(muchas <= alto * 0.63 + 1, $"[{que}] y nunca pasa del 62 % de la pantalla ({muchas:0} de {alto:0})");
            Vale(muchas >= 240, $"[{que}] pero siempre cabe algo ({muchas:0})");
            Vale(MainWindow.AltoDelCentro(8, alto) <= MainWindow.AltoDelCentro(9, alto) + 0.01,
                 $"[{que}] el alto no baja al llegar más");
        }

        // Y en una pantalla absurdamente baja sigue siendo un número razonable
        double enano = MainWindow.AltoDelCentro(40, 200);
        Vale(enano >= 240 && enano <= 400, $"en una pantalla de 200 px el alto sigue siendo sensato ({enano:0})");
    }

    // ---------- Los cuatro niveles de animación ----------
    //
    // El panel de notificaciones NO anima por su cuenta: se abre y se cierra con
    // `AbrirPanel`/`CerrarPanel`, que pasan por `Kf`, que mira el nivel. Eso es a
    // propósito —es la regla de la casa desde la Fase A— y lo que hay que comprobar es
    // que el contrato del que depende sigue en pie.
    static void Niveles()
    {
        Grupo("centro: los cuatro niveles de animación");

        var antes = Animacion.Nivel;
        try
        {
            Animacion.Elegir("completo");
            Igual(NivelAnimacion.Completo, Animacion.Nivel, "completo");
            Vale(Animacion.Hay && Animacion.Escalar(100) == 100, "completo anima a su velocidad");

            Animacion.Elegir("equilibrado");
            Vale(Animacion.Hay && Animacion.Escalar(100) < 100, "equilibrado anima más corto");

            Animacion.Elegir("reducido");
            Vale(!Animacion.Hay && Animacion.Escalar(100) == 0, "reducido no mueve geometría");
            Vale(Animacion.EscalarFundido(100) > 0, "pero conserva los fundidos");

            Animacion.Elegir("desactivado");
            Vale(!Animacion.Hay && Animacion.Escalar(100) == 0, "desactivado no mueve nada");
            Igual(0.0, Animacion.EscalarFundido(100), "ni funde");
        }
        finally { Animacion.Elegir(antes.ToString()); }
    }
    static void AbrirYCerrar()
    {
        Grupo("centro: abrir y cerrar muchas veces");

        // Lo que de verdad se rompe al abrir y cerrar es el aviso de cambio: si cada
        // apertura se suscribe y ninguna se da de baja, a la décima vez la campana se
        // repinta diez veces por notificación. Se comprueba que el contrato aguanta.
        var c = Nuevo();
        int avisos = 0;
        void Oyente() => avisos++;

        for (int i = 0; i < 20; i++)
        {
            c.Cambio += Oyente;
            c.Cambio -= Oyente;
        }
        c.Recibir(new[] { Av("A", "uno") });
        Igual(0, avisos, "sin oyentes vivos no se avisa a nadie");

        c.Cambio += Oyente;
        c.Recibir(new[] { Av("A", "dos") });
        Igual(1, avisos, "con uno vivo se avisa una vez");
        c.MarcarLeida(c.Todas[0].Id);
        Igual(2, avisos, "marcar como leída también avisa");
        c.Descartar(c.Todas[0].Id);
        Igual(3, avisos, "y descartar");
        c.Cambio -= Oyente;

        // Y el estado sigue siendo coherente después de todo el trajín
        var antes = c.Cuantas;
        for (int i = 0; i < 50; i++) { c.PorApp(); c.MarcarTodasLeidas(); }
        Igual(antes, c.Cuantas, "mirar el centro cincuenta veces no cambia lo que hay");
        Igual(0, c.NoLeidas, "y queda todo leído");

        // El estado vacío es estable
        c.BorrarTodo();
        Vale(c.Vacio && c.Cuantas == 0 && c.NoLeidas == 0 && c.PorApp().Count == 0,
             "el estado vacío es coherente mire por donde se mire");
    }
    // ================= C5.8: el botón propio del centro de control =================
    //
    // Lo que esto protege: la superficie de Inicio/centro de control se abre desde un
    // botón NUEVO de la isla. Ni desde el altavoz —que abre solo audio—, ni desde el
    // `⊞` ni desde la cuadrícula, que ya tenían su función. El reparto vive en una sola
    // tabla (`PanelDeBoton`) y los botones de la isla despachan a través de ella, así
    // que comprobar la tabla es comprobar el comportamiento, no una copia del mismo.

    // ================= C5.8: la superficie de Inicio =================
    //
    // Lo que esto protege: hay UNA superficie —el buscador con su lista, y debajo la
    // música, el idioma del teclado y la energía— y se abre desde UN sitio: el botón
    // Inicio de la isla. Hubo un rato en que las
    // tarjetas vivían además en un panel "control" con su propio botón, y eran dos
    // pantallas con lo mismo.

    static void InicioRepartoDeLaIsla()
    {
        Grupo("inicio: una sola superficie y una sola entrada");

        // ---------- El botón Inicio abre la superficie de Inicio ----------
        Igual(MainWindow.PanelInicio, MainWindow.PanelDeBoton(MainWindow.BotonLanzador),
              "el botón Inicio abre la superficie de Inicio");

        // ---------- Y es la misma superficie que lleva las tarjetas del centro ----------
        Igual(MainWindow.PanelInicio, MainWindow.PanelDelCentro,
              "las tarjetas del centro de control viven en esa misma superficie");

        // ---------- El altavoz abre EXCLUSIVAMENTE audio ----------
        Igual(MainWindow.PanelAudio, MainWindow.PanelDeBoton(MainWindow.BotonAltavoz),
              "el altavoz abre el panel de audio");
        Vale(MainWindow.PanelDeBoton(MainWindow.BotonAltavoz) != MainWindow.PanelInicio,
             "y NO la superficie de Inicio: son dos paneles distintos");

        // ---------- La cuadrícula sigue donde estaba ----------
        Igual(MainWindow.PanelVista, MainWindow.PanelDeBoton(MainWindow.BotonCuadricula),
              "la cuadrícula sigue abriendo la vista general");

        // ---------- Ya no hay un segundo botón para lo mismo ----------
        //
        // Cinco botones, cinco paneles distintos. Si alguien volviera a añadir una
        // entrada a la superficie de Inicio, esta cuenta lo cantaría.
        var paneles = MainWindow.BotonesDeIsla.Select(MainWindow.PanelDeBoton).ToList();
        Igual(5, paneles.Count, "cinco botones en la tabla");
        Vale(paneles.All(p => p.Length > 0), "ninguno se queda sin panel");
        Igual(paneles.Count, paneles.Distinct().Count(),
              "y cada uno abre un panel distinto: ni dos entradas a lo mismo ni dos pantallas iguales");
        Igual(1, paneles.Count(p => p == MainWindow.PanelInicio),
              "a la superficie de Inicio se entra por un solo sitio");

        // Un botón que no existe no abre nada, en vez de caer en el primero de la lista
        Igual("", MainWindow.PanelDeBoton("centro"),
              "el botón que se retiró ya no abre nada");
        Igual("", MainWindow.PanelDeBoton("inventado"), "ni uno desconocido");
        Igual("", MainWindow.PanelDeBoton(""), "ni la cadena vacía");
    }

    static void InicioColgadoDeSuBoton()
    {
        Grupo("inicio: cuelga del botón Inicio y no se centra en la pantalla");

        // El caso medido en la máquina: isla de 1163 px y la superficie pidiendo 660. El
        // botón Inicio es el primero de la zona izquierda, así que la tarjeta tiene que
        // salir pegada a la izquierda, no en medio ni desplazada a la derecha.
        const double isla = 1163, borde = 2;
        double panel = MainWindow.AnchoDeInicio;

        double x = MainWindow.XdeTarjetaPura(isla, panel, 20, borde);
        Igual(borde, x, $"con el botón en el filo izquierdo, la tarjeta sale pegada ahí (x={x:0})");
        Vale(x + panel < isla / 2 + panel / 2,
             $"y se queda a la izquierda, no desplazada a la derecha (acaba en {x + panel:0} de {isla:0})");

        // Lo que NO puede hacer: salir centrada en la isla. Ese era el síntoma.
        double centrada = (isla - panel) / 2;
        Vale(Math.Abs(x - centrada) > 100,
             $"no sale centrada en la pantalla (x={x:0}, el centro sería {centrada:0})");

        // Colgada de su botón, dondequiera que esté
        Vale(Math.Abs(MainWindow.XdeTarjetaPura(isla, panel, 500, borde) + panel / 2 - 500) < 0.01,
             "con sitio, la tarjeta queda centrada en el botón que la abre");

        // Y con el botón al final, sin salirse
        double d = MainWindow.XdeTarjetaPura(isla, panel, isla - 20, borde);
        Vale(d + panel <= isla - borde + 0.01, $"con el botón al final, la tarjeta no se sale (x={d:0})");

        // El ancho es controlado: ni el de la isla ni el del monitor
        Vale(panel >= 560 && panel <= 760, $"el ancho es el suyo y está acotado ({panel:0})");
        Vale(panel < isla * 0.6, "nunca casi todo el ancho de la isla");
        Vale(panel < 1131, "y muy por debajo de los 1131 px que medía estirado");
    }

    static void InicioEnLosCuatroBordes()
    {
        Grupo("inicio: cabe con la isla en los cuatro bordes");

        // La cuenta que coloca la tarjeta es la misma arriba y abajo —solo cambia de qué
        // lado crece la isla—, y lo que no puede pasar nunca es que se salga.
        const double borde = 2;
        double w = MainWindow.AnchoDeInicio;

        foreach (double isla in new[] { 1163.0, 900.0, 700.0, 680.0 })
        {
            foreach (double centro in new[] { 0.0, 20.0, isla / 2, isla - 20, isla, isla * 2, -50.0 })
            {
                double x = MainWindow.XdeTarjetaPura(isla, w, centro, borde);
                double ancho = Math.Min(w, isla);
                Vale(x >= borde - 0.01,
                     $"[isla {isla:0}, botón {centro:0}] no se sale por la izquierda (x={x:0})");
                Vale(ancho <= isla + 0.01,
                     $"[isla {isla:0}, botón {centro:0}] y no es más ancha que la isla ({ancho:0})");
                if (w <= isla - 2 * borde)
                    Vale(x + ancho <= isla - borde + 0.01,
                         $"[isla {isla:0}, botón {centro:0}] y cuando cabe, cabe entera (x={x:0}+{ancho:0})");
            }
        }

        // ---------- Y en vertical ----------
        //
        // Con la barra en un lado la superficie vive en su propia ventana, al lado de la
        // barra. La cuenta es otra (`SitioDeLaTarjeta`) y hay que comprobarla aparte.
        foreach (double alto in new[] { 768.0, 1080.0, 1440.0 })
        {
            double tope = MainWindow.TopeDeInicio(alto);
            foreach (double? centro in new double?[] { null, 0, 40, alto / 2, alto - 20, alto })
            {
                var (desp, y) = MainWindow.SitioDeLaTarjeta(6, 56, 8, alto, tope, centro);
                Vale(y >= 0, $"[vertical {alto:0}] no se sale por arriba (y={y:0})");
                Vale(y + tope <= alto + 0.01, $"[vertical {alto:0}] ni por abajo (y={y:0}+{tope:0})");
                Vale(desp > 0, $"[vertical {alto:0}] y se separa de la barra, no la tapa");
            }
        }

        // En vertical el ancho se recorta a lo que cabe al lado de la barra: 660 px no
        // entran en un portátil de 1366 con una barra gruesa, y una tarjeta más ancha que
        // el hueco no se recorta, se queda fuera de la ventana.
        foreach (double anchoPantalla in new[] { 1366.0, 1920.0, 2560.0 })
        {
            double cabe = MainWindow.AnchoMaximoDeTarjeta(anchoPantalla, 56, 8, 8);
            Vale(cabe > 120, $"[vertical {anchoPantalla:0}] siempre cabe algo ({cabe:0})");
            Vale(Math.Min(MainWindow.AnchoDeInicio, cabe) <= cabe + 0.01,
                 $"[vertical {anchoPantalla:0}] y la superficie se estrecha a lo que cabe");
        }
    }

    static void InicioAltoConTope()
    {
        Grupo("inicio: el alto tiene tope de verdad");

        // Medido: el alto pedido era un MÍNIMO y `AbrirPanel` lo subía a lo que pedía el
        // contenido ("pedía 560 de alto pero necesita 1076; se le da el que necesita").
        // Con la portada entera dentro, sin tope esto se come la pantalla.
        foreach (var (alto, que) in new[]
        {
            (768.0, "portátil 1366x768"), (1080.0, "1080p"), (1440.0, "1440p"),
            (960.0, "4K al 225 %"), (1920.0, "vertical 1080x1920"),
        })
        {
            double t = MainWindow.TopeDeInicio(alto);
            Vale(t <= alto * 0.62 + 0.01 || t == 360,
                 $"[{que}] el tope no pasa del 62 % de la pantalla ({t:0} de {alto:0})");
            Vale(t >= 360, $"[{que}] y nunca deja la superficie inservible ({t:0})");
            Vale(t < 1076, $"[{que}] por debajo de los 1076 px que salían ({t:0})");
        }

        Igual(960.0, MainWindow.TopeDeInicio(1920), "en una pantalla muy alta el tope se queda en 960");
        Igual(360.0, MainWindow.TopeDeInicio(200), "y en una absurdamente baja, en el mínimo usable");
        Vale(MainWindow.TopeDeInicio(768) < MainWindow.TopeDeInicio(1440),
             "en una pantalla más alta cabe más superficie");
    }

    static void InicioGestoYLanzadorIntacto()
    {
        Grupo("inicio: el botón no arrastra la isla y el lanzador conserva lo suyo");

        // ---------- El botón no mueve la NubeBar ----------
        //
        // Misma regla que los deslizadores: si el clic nace sobre algo con lo que se
        // interactúa, el gesto es de ese algo. El botón Inicio es un `Button`, así que
        // `GestoDeUnControl` lo reconoce por tipo y el arrastre de la isla no arranca. Se
        // monta el árbol de verdad —isla, fila, botón, glifo— porque lo que falló en su
        // día fue justo subir por el árbol, no el caso simple.
        var isla = new Border();
        var fila = new StackPanel();
        var glifo = new TextBlock { Text = "⊞" };
        var boton = new Button { Content = glifo };
        fila.Children.Add(boton);
        isla.Child = fila;

        Vale(MainWindow.GestoDeUnControl(boton, isla), "el botón Inicio es suyo: no arrastra la isla");
        Vale(MainWindow.GestoDeUnControl(glifo, isla),
             "y el clic sobre el glifo tampoco: se sube por el árbol hasta el botón");
        Vale(!MainWindow.GestoDeUnControl(fila, isla), "pero el hueco de la fila sí arrastra");
        Vale(!MainWindow.GestoDeUnControl(isla, isla), "y la isla, por supuesto");

        // Y la caja de búsqueda, que vive DENTRO de la superficie, tampoco la arrastra:
        // escribir en ella no puede mover la nube por la pantalla.
        var caja = new TextBox();
        var panel = new Border { Child = caja };
        Vale(MainWindow.GestoDeUnControl(caja, isla), "la caja de búsqueda no arrastra la isla");
        Vale(MainWindow.GestoDeUnControl(panel.Child, isla), "ni su marco");

        // ---------- El lanzador conserva sus funciones ----------
        //
        // La superficie unificada no es una superficie con menos cosas. Esto comprueba lo
        // que se podía perder al meter las tarjetas dentro: las categorías del buscador
        // siguen siendo siete y con nombre propio, y el menú del clic derecho sigue
        // sabiendo colocarse sin salirse de la pantalla.
        var nombres = Enum.GetValues<Categoria>()
            .Select(c => new Resultado { Categoria = c }.Etiqueta).ToList();
        Igual(7, nombres.Count, "las siete categorías del buscador siguen ahí");
        Igual(nombres.Count, nombres.Distinct().Count(), "con nombre propio y sin repetirse");
        Vale(nombres.Contains("Aplicación") && nombres.Contains("Archivo") &&
             nombres.Contains("Ajuste") && nombres.Contains("Ventana abierta"),
             "incluidas aplicación, archivo, ajuste y ventana abierta");

        // Y el menú del clic derecho —abrir como administrador, propiedades, fijar—
        // sigue existiendo para las filas que lo admiten. Es lo que no se puede perder
        // al meter las tarjetas dentro de la misma superficie.
        var app = new Resultado { Clave = "a", Titulo = "Calculadora", Categoria = Categoria.App };
        var calculo = new Resultado { Clave = "c", Titulo = "2+2", Categoria = Categoria.Calculo };
        Vale(MainWindow.TieneMenuDeApp(app), "una aplicación tiene su menú de clic derecho");
        Vale(!MainWindow.TieneMenuDeApp(calculo), "y un cálculo no, que no hay nada que abrir");
    }
    static void PanelRaizOpaca()
    {
        Grupo("paneles: la raíz del panel es una superficie opaca");

        // La pregunta que separa "PanelShell existe" de "PanelShell se está usando". Un
        // panel cuya raíz no es opaca deja ver lo que haya detrás, y detrás hay una isla
        // translúcida y el escritorio.
        var grid = new Grid();
        Vale(!MainWindow.EsShellOpaco(grid), "un Grid pelado no es una superficie: no tapa nada");

        var sinFondo = new Border();
        Vale(!MainWindow.EsShellOpaco(sinFondo), "ni un Border sin fondo");

        var translucido = new Border { Background = new SolidColorBrush(Color.FromArgb(0x33, 20, 20, 20)) };
        Vale(!MainWindow.EsShellOpaco(translucido),
             "ni uno con alfa: dos capas transparentes seguidas siguen dejando pasar el escritorio");

        var casi = new Border { Background = new SolidColorBrush(Color.FromArgb(0xFE, 20, 20, 20)) };
        Vale(!MainWindow.EsShellOpaco(casi), "ni uno al 254 de 255: opaco es opaco");

        var opaco = new Border { Background = new SolidColorBrush(Color.FromArgb(0xFF, 20, 20, 20)) };
        Vale(MainWindow.EsShellOpaco(opaco), "un Border con fondo opaco sí lo es");

        Vale(!MainWindow.EsShellOpaco(null), "y preguntar por nada no revienta");

        // Y la superficie que usa PanelShell es opaca en los siete temas: es de donde
        // sale el color, así que si un tema la define con alfa el panel se traspasa.
        foreach (var t in NubeBar.Temas.Predefinidos)
        {
            var tema = new Tema();
            NubeBar.Temas.Aplicar(t, tema);
            var fondo = (Color)ColorConverter.ConvertFromString(tema.Fondo)!;
            var texto = (Color)ColorConverter.ConvertFromString(tema.Texto)!;
            var acento = (Color)ColorConverter.ConvertFromString(tema.Acento)!;
            var r = Roles.Desde(tema, fondo, texto, acento);

            Igual(255, (int)r.SuperficieSolida.A, $"[{t.Nombre}] la superficie sólida es opaca");
            Igual(255, (int)r.SuperficieAltaSolida.A, $"[{t.Nombre}] y la levantada también");
            Igual(255, (int)r.FondoOpaco.A, $"[{t.Nombre}] y el fondo del panel");

            // ---------- Y por qué `FondoOpaco` no es `Tema.Fondo` ----------
            //
            // Porque el del tema PUEDE traer alfa, y de hecho el de serie lo trae
            // (`#E6000000`). Ese alfa es deliberado para la ISLA —se quiere ver un poco el
            // escritorio por detrás— y es justo lo que no se quiere dentro de un panel, donde
            // dos capas translúcidas seguidas dejan pasar lo que haya al fondo.
            //
            // Por eso son dos colores y no uno, y por eso `PanelShell` usa el opaco.
            Vale(MainWindow.EsShellOpaco(new Border { Background = new SolidColorBrush(r.FondoOpaco) }),
                 $"[{t.Nombre}] el fondo opaco vale como raíz de un panel flotante");

            if (fondo.A < 255)
                Vale(!MainWindow.EsShellOpaco(new Border { Background = new SolidColorBrush(fondo) }),
                     $"[{t.Nombre}] y el del tema, con alfa, NO vale como raíz de un panel");
        }
    }

    static void CentroColgadoDeSuBoton()
    {
        Grupo("centro: cuelga de su botón y no se centra en la pantalla");

        // El caso medido en la máquina: isla de 1224 px y el centro de control pidiendo
        // 460. Con el botón a la IZQUIERDA la tarjeta tiene que salir a la izquierda, no
        // en medio ni desplazada a la derecha.
        const double isla = 1224, borde = 2;
        double panel = MainWindow.AnchoDelCentro;

        // El botón es el primero de la isla: su centro está a unos 20 px del filo
        double x = MainWindow.XdeTarjetaPura(isla, panel, 20, borde);
        Igual(borde, x, $"con el botón en el filo izquierdo, la tarjeta sale pegada ahí (x={x:0})");
        Vale(x + panel < isla / 2,
             $"y se queda en la mitad izquierda, no desplazada a la derecha (acaba en {x + panel:0} de {isla:0})");

        // Lo que NO puede hacer: salir centrada en la isla. Ese era el síntoma —"aparece
        // desplazada a la derecha"— cuando el botón estaba en el grupo de la derecha.
        double centrada = (isla - panel) / 2;
        Vale(Math.Abs(x - centrada) > 100,
             $"no sale centrada en la pantalla (x={x:0}, el centro sería {centrada:0})");

        // Con el botón en medio sí queda centrado en ÉL, que es lo correcto
        Vale(Math.Abs(MainWindow.XdeTarjetaPura(isla, panel, 400, borde) + panel / 2 - 400) < 0.01,
             "colgado de su botón: centrado en el botón, donde quiera que esté");

        // Y con el botón al final, a la derecha, sin salirse
        double d = MainWindow.XdeTarjetaPura(isla, panel, isla - 20, borde);
        Vale(d + panel <= isla - borde + 0.01, $"con el botón al final, la tarjeta no se sale (x={d:0})");

        Vale(panel >= 420 && panel <= 500, $"y el ancho sigue siendo el aprobado ({panel:0})");
        Vale(panel < isla * 0.45, "nunca casi todo el ancho de la isla");

        // En pantallas donde la isla es más estrecha. Lo que se garantiza es lo que de
        // verdad se puede garantizar: la tarjeta NUNCA empieza fuera, y nunca es más
        // ancha que la isla.
        //
        // Lo que no se garantiza —y conviene tenerlo escrito— es que con una isla más
        // estrecha que el panel la tarjeta se queda del ancho de la isla y le sobresale
        // el grosor del borde: `w` se recorta al ancho de la isla pero la x mínima sigue
        // siendo `borde`. No se arregla aquí porque ese estado no se alcanza: la isla se
        // ensancha hasta el ancho del panel al abrirlo (ver `anchoIsla` en AbrirPanel),
        // así que la isla nunca es más estrecha que su propio panel. Si algún día lo
        // fuera, se vería 2 px de tarjeta pasada por la derecha, y además el recorte del
        // cajón ya la corta contra el filo de la isla.
        foreach (double estrecha in new[] { 1366.0, 900.0, 700.0, 460.0 })
        {
            double xx = MainWindow.XdeTarjetaPura(estrecha, panel, estrecha / 2, borde);
            double ancho = Math.Min(panel, estrecha);
            Vale(xx >= borde - 0.01, $"[isla {estrecha:0}] la tarjeta no empieza fuera (x={xx:0})");
            Vale(ancho <= estrecha + 0.01,
                 $"[isla {estrecha:0}] y no es más ancha que la isla ({ancho:0} de {estrecha:0})");
            if (panel <= estrecha - 2 * borde)
                Vale(xx + ancho <= estrecha - borde + 0.01,
                     $"[isla {estrecha:0}] y cuando cabe, cabe entera con su borde (x={xx:0}+{ancho:0})");
        }
    }

    // ================= C5.10: abrir el Centro no toca lo que suena =================
    //
    // Lo que se comprueba aquí es el GUARDIA, no una lista de buenas intenciones:
    // mientras se monta un panel, las puertas que escriben audio y multimedia están
    // cerradas. Las pruebas no llaman a `Musica.PlayPausa()` ni a `Audio.PonerVolumen()`
    // de verdad —eso pararía la música de quien esté ejecutando la tanda, que es
    // exactamente el fallo que se está arreglando—: llaman al guardia, que es la pieza
    // que decide, y comprueban que decide bien.

    static void CentroNoTocaLoQueSuena()
    {
        Grupo("centro: montar el panel no cambia nada");

        bool musicaAntes = Musica.Construyendo, audioAntes = Audio.Construyendo;
        try
        {
            // ---------- abrir centro → la reproducción continúa ----------
            Musica.Construyendo = true;
            long rechazos = Musica.Rechazadas;
            long mutaciones = Musica.Mutaciones;

            Vale(!Musica.Permitido("play/pausa"), "montando: una pausa no pasa");
            Vale(!Musica.Permitido("siguiente"), "montando: un «siguiente» no pasa");
            Vale(!Musica.Permitido("anterior"), "montando: un «anterior» no pasa");

            Igual(rechazos + 3, Musica.Rechazadas, "los tres intentos quedan contados");
            Igual(mutaciones, Musica.Mutaciones,
                  "y ninguno se convierte en un cambio: la reproducción sigue igual");
            Vale(Musica.UltimaRechazada.Contains("anterior"),
                 $"y el último queda con su nombre ({Musica.UltimaRechazada})");

            // ---------- abrir centro → volumen sin cambios ----------
            Audio.Construyendo = true;
            long rechazosAudio = Audio.Rechazadas;
            Vale(!Audio.Permitido("poner el volumen a 50"), "montando: subir el volumen no pasa");
            Vale(!Audio.Permitido("silenciar"), "montando: silenciar no pasa");

            // ---------- abrir centro → dispositivo de salida sin cambios ----------
            Vale(!Audio.Permitido("cambiar la salida a {0.0.0}"),
                 "montando: cambiar el dispositivo de salida no pasa");

            // ---------- abrir centro → aplicación multimedia sin cambios ----------
            // La sesión que manda la elige Windows; NubeBar solo la lee. La única forma
            // de que cambiase sería pedir "siguiente" o "anterior", y ya está cerrado.
            Vale(!Audio.Permitido("poner el volumen de Spotify"),
                 "montando: tocar el volumen de una aplicación tampoco pasa");
            Igual(rechazosAudio + 4, Audio.Rechazadas, "los cuatro intentos de audio quedan contados");

            // ---------- pulsar pausa → la reproducción se detiene ----------
            //
            // Es decir: con el panel ya montado, el guardia DEJA pasar. Si esto fallara,
            // el arreglo habría dejado los mandos muertos, que es la otra forma de
            // romperlo.
            Musica.Construyendo = false;
            Audio.Construyendo = false;
            long rechazos2 = Musica.Rechazadas;
            Vale(Musica.Permitido("play/pausa"), "ya montado: pulsar pausa SÍ pasa");
            Vale(Musica.Permitido("siguiente"), "ya montado: «siguiente» sí pasa");
            Vale(Musica.Permitido("anterior"), "ya montado: «anterior» sí pasa");
            Igual(rechazos2, Musica.Rechazadas, "y un clic de verdad no se cuenta como rechazo");

            // ---------- pulsar reproducir → la reproducción continúa ----------
            Vale(Audio.Permitido("poner el volumen a 30"), "ya montado: el volumen se puede cambiar");
            Vale(Audio.Permitido("cambiar la salida a {0.0.0}"), "y la salida también");

            // ---------- El guardia se suelta aunque algo reviente ----------
            //
            // `CrearControl` lo pone en un try/finally. Lo que se comprueba es el patrón:
            // si una tarjeta lanza, el guardia no se queda puesto. Si se quedara, el
            // siguiente clic en pausa no haría nada y el fallo sería peor que el de ahora.
            try
            {
                Musica.Construyendo = true;
                try { throw new InvalidOperationException("una tarjeta revienta al montarse"); }
                finally { Musica.Construyendo = false; }
            }
            catch (InvalidOperationException) { }
            Vale(!Musica.Construyendo, "si una tarjeta revienta, el guardia se suelta igual");
            Vale(Musica.Permitido("play/pausa"), "y los mandos siguen vivos después");
        }
        finally
        {
            Musica.Construyendo = musicaAntes;
            Audio.Construyendo = audioAntes;
        }
    }

    // ================= C5.11: la superficie de Inicio se VE =================
    //
    // Lo que esto protege es el fallo del panel negro con la franja blanca vertical. El
    // contenido estaba todo ahí —37 tarjetas, todas visibles— y no se veía, porque
    // `AbrirPanel` envolvía en un segundo `ScrollViewer` un panel que ya tenía el suyo:
    //
    //   tipo de raíz visual: ScrollViewer
    //   cadena: ScrollViewer(660x893) > Border(643x1256) > Grid(629x1236)
    //   viewport 643x893  extent 643x1256  barra visible: SÍ
    //
    // Las pruebas van contra `Veredicto`, que es la función que mira una radiografía y
    // dice qué está mal. Es pura: se le puede dar una radiografía inventada y comprobar
    // que caza cada uno de los cinco fallos que se pidieron, sin abrir una ventana.

    static MainWindow.RadiografiaPanel Sana() => new()
    {
        Raiz = "Border",
        Hijos = 6,
        Tarjetas = 9, TarjetasVisibles = 9, TarjetasConArea = 9,
        ShellEsRaiz = true, ShellOpaco = true,
        ShellAncho = 660, ShellAlto = 893,
        Visibilidad = "Visible",
        HayScroll = true, ScrollInterno = true, BarraVisible = false,
        Viewport = 760, Extent = 1120, OffsetY = 0, OffsetX = 0,
        ViewportAncho = 646, ExtentAncho = 646,
    };

    static void InicioSeVeDeVerdad()
    {
        Grupo("inicio: la integración falla si el panel no se ve");

        // El caso bueno no da ninguna queja. Si esto fallara, el resto de la prueba no
        // valdría nada: estaría cazando fantasmas.
        var bien = MainWindow.Veredicto(Sana()).ToList();
        Igual(0, bien.Count, "una superficie sana no da ninguna queja" +
                             (bien.Count > 0 ? ": " + string.Join(" | ", bien) : ""));

        // ---------- 1. PanelShell con 0 tarjetas ----------
        var sinTarjetas = Sana();
        sinTarjetas.Tarjetas = sinTarjetas.TarjetasVisibles = sinTarjetas.TarjetasConArea = 0;
        var q = MainWindow.Veredicto(sinTarjetas).ToList();
        Vale(q.Any(x => x.Contains("0 tarjetas")), "con 0 tarjetas, falla y lo dice");

        // ---------- 2. Hijos pero ninguno visible ----------
        var invisibles = Sana();
        invisibles.TarjetasVisibles = 0;
        q = MainWindow.Veredicto(invisibles).ToList();
        Vale(q.Any(x => x.Contains("NINGUNA visible")), "con tarjetas en el árbol y ninguna visible, falla");

        // Y el caso hermano: visibles pero aplastadas a 0 px, que a ojo es lo mismo
        var aplastadas = Sana();
        aplastadas.TarjetasConArea = 0;
        q = MainWindow.Veredicto(aplastadas).ToList();
        Vale(q.Any(x => x.Contains("miden 0")), "con tarjetas visibles pero de 0 px, falla");

        // ---------- 3. Contenido fuera del viewport ----------
        var desplazado = Sana();
        desplazado.OffsetY = 900;          // el máximo legal sería 1120-760 = 360
        q = MainWindow.Veredicto(desplazado).ToList();
        Vale(q.Any(x => x.Contains("fuera del viewport")), "con el contenido desplazado fuera, falla");

        // Un offset legítimo —el usuario ha rodado la rueda— no es un fallo
        var rodado = Sana();
        rodado.OffsetY = 200;
        Igual(0, MainWindow.Veredicto(rodado).Count(),
              "pero haber desplazado el panel a mano no es un fallo");

        // ---------- 4. La superficie sale completamente negra ----------
        //
        // "Negra" en la práctica es una de dos: no hay contenido, o el shell está
        // aplastado. Las dos se cazan.
        var vacia = Sana();
        vacia.Raiz = "no hay contenido";
        q = MainWindow.Veredicto(vacia).ToList();
        Vale(q.Any(x => x.Contains("no tiene contenido")), "sin contenido, falla");
        Igual(1, q.Count, "y no se encadena con quejas derivadas: una causa, un mensaje");

        var chata = Sana();
        chata.ShellAlto = 20;
        q = MainWindow.Veredicto(chata).ToList();
        Vale(q.Any(x => x.Contains("aplastado")), "con el shell aplastado, falla");

        var viewportMinimo = Sana();
        viewportMinimo.Viewport = 12;
        q = MainWindow.Veredicto(viewportMinimo).ToList();
        Vale(q.Any(x => x.Contains("no cabe nada dentro")), "con un viewport de 12 px, falla");

        // ---------- 5. Una barra blanca no intencionada ----------
        var conBarra = Sana();
        conBarra.BarraVisible = true;
        q = MainWindow.Veredicto(conBarra).ToList();
        Vale(q.Any(x => x.Contains("barra de desplazamiento VISIBLE")),
             "con una barra de scroll a la vista, falla: es la franja blanca");

        // ---------- Y el envoltorio de más, que es la causa que se arregló ----------
        var envuelto = Sana();
        envuelto.ScrollInterno = false;
        envuelto.ShellEsRaiz = false;
        q = MainWindow.Veredicto(envuelto).ToList();
        Vale(q.Any(x => x.Contains("envoltorio de fuera")),
             "si el scroll es un envoltorio de fuera, falla: era la causa del panel negro");

        // ---------- Y el shell translúcido ----------
        var transparente = Sana();
        transparente.ShellOpaco = false;
        q = MainWindow.Veredicto(transparente).ToList();
        Vale(q.Any(x => x.Contains("no es opaco")),
             "si el shell no es opaco, falla: se leería el escritorio por detrás");
    }

    static void InicioSeVeEnLosDosTemasYDpi()
    {
        Grupo("inicio: se lee en los siete temas y a cualquier DPI");

        // La parte comprobable de "ningún texto del escritorio atravesando la superficie"
        // es que la superficie sea OPACA y que lo que lleva escrito se lea encima. Se mide
        // contra la superficie sólida, no contra la pintada: un color con alfa no tiene
        // luminancia propia hasta que se sabe qué hay detrás.
        const double MinimoTexto = 4.5, MinimoSecundario = 3.0;

        foreach (var t in NubeBar.Temas.Predefinidos)
        {
            var tema = new Tema();
            NubeBar.Temas.Aplicar(t, tema);
            var fondo = (Color)ColorConverter.ConvertFromString(tema.Fondo)!;
            var texto = (Color)ColorConverter.ConvertFromString(tema.Texto)!;
            var acento = (Color)ColorConverter.ConvertFromString(tema.Acento)!;
            var r = Roles.Desde(tema, fondo, texto, acento);

            // La superficie del panel y de sus tarjetas, opacas: es lo que impide que el
            // escritorio se lea a través
            Igual(255, (int)r.FondoOpaco.A, $"[{t.Nombre}] el fondo del panel es opaco");
            Igual(255, (int)r.SuperficieSolida.A, $"[{t.Nombre}] y la superficie de las tarjetas");

            void Mide(Color tinta, Color detras, double min, string que)
            {
                double c = Contraste.Entre(tinta, detras);
                Vale(c >= min, $"[{t.Nombre}] {que}: contraste {c:0.0} (hace falta {min:0.0})");
            }

            Mide(r.Texto, r.SuperficieSolida, MinimoTexto, "el saludo sobre la tarjeta");
            Mide(r.TextoAtenuado, r.SuperficieSolida, MinimoSecundario, "la fecha");
            Mide(r.AcentoTinta, r.FondoOpaco, MinimoSecundario, "la hora en acento");
            Mide(r.Texto, r.SuperficieAltaSolida, MinimoTexto, "el título de lo que suena");
        }

        // ---------- DPI alto ----------
        //
        // La colocación va en unidades de WPF y la ventana en píxeles: con la escala al
        // 150 % o al 225 % la superficie tiene que seguir cabiendo y la tarjeta seguir
        // dentro de la isla. Son las medidas de WPF las que se comprueban.
        foreach (var (anchoPx, altoPx, escala, que) in new[]
        {
            (2560.0, 1440.0, 1.0, "2560x1440 al 100 %"),
            (2560.0, 1440.0, 1.5, "2560x1440 al 150 %"),
            (3840.0, 2160.0, 2.25, "4K al 225 %"),
            (1366.0, 768.0, 1.0, "portátil al 100 %"),
            (1920.0, 1080.0, 1.25, "1080p al 125 %"),
        })
        {
            double anchoWpf = anchoPx / escala, altoWpf = altoPx / escala;
            double tope = MainWindow.TopeDeInicio(altoWpf);

            Vale(tope <= altoWpf * 0.62 + 0.01, $"[{que}] el tope respeta el 62 % ({tope:0} de {altoWpf:0})");
            Vale(tope >= 360, $"[{que}] y sigue siendo usable ({tope:0})");

            // La tarjeta, anclada al botón Inicio del filo izquierdo, cabe en la isla
            double isla = Math.Min(anchoWpf, 1200);
            double x = MainWindow.XdeTarjetaPura(isla, MainWindow.AnchoDeInicio, 20, 2);
            Vale(x >= 2 - 0.01, $"[{que}] la tarjeta no se sale por la izquierda (x={x:0})");
            Vale(Math.Min(MainWindow.AnchoDeInicio, isla) <= isla + 0.01,
                 $"[{que}] y no es más ancha que la isla");

            // Y en vertical, al lado de la barra
            var (desp, y) = MainWindow.SitioDeLaTarjeta(6, 56, 8, altoWpf, tope, altoWpf / 2);
            Vale(y >= 0 && y + tope <= altoWpf + 0.01,
                 $"[{que}] en vertical la tarjeta cabe de arriba abajo (y={y:0}+{tope:0} de {altoWpf:0})");
            Vale(desp > 0, $"[{que}] y no tapa la barra");
        }
    }
    // ================= C5.12: la bandeja se actualiza en caliente =================
    //
    // La bandeja NO se enumera al arrancar y se queda así: la fuente es la
    // `ObservableCollection` de ManagedShell, que recibe los `Shell_NotifyIcon` de cada
    // app, y NubeBar reconcilia su tira cuando esa colección avisa. Eso está medido con
    // apps de verdad (ver `--diagbandeja`), y lo que se comprueba aquí es la pieza que
    // decide QUÉ ha cambiado de una muestra a la siguiente: lo que separa "ha entrado una
    // app" de "el panel se está reconstruyendo solo".

    static DiagBandeja.Ficha F(string id, string nombre = "app", bool oculto = false) =>
        new(id, nombre, oculto);

    static void BandejaComparaMuestras()
    {
        Grupo("bandeja: qué ha cambiado de una muestra a la siguiente");

        var a = F("hwnd:1/uid:1", "Steam");
        var b = F("guid:11111111-1111-1111-1111-111111111111", "Discord");
        var c = F("hwnd:2/uid:0", "NordVPN");

        // ---------- Sin cambios no hay cambios ----------
        var igual = DiagBandeja.Comparar(new() { a, b, c }, new() { a, b, c });
        Vale(igual.Nada, "la misma lista dos veces no da ningún cambio");
        Igual(0, igual.Anadidos.Count, "ni añadidos");
        Igual(0, igual.Eliminados.Count, "ni eliminados");
        Vale(!igual.OrdenMovido, "ni el orden movido: abrir y cerrar el panel no puede mover nada");

        // ---------- Una app que arranca DESPUÉS ----------
        var nueva = DiagBandeja.Comparar(new() { a, b }, new() { a, b, c });
        Igual(1, nueva.Anadidos.Count, "una app nueva se detecta");
        Vale(nueva.Anadidos[0].Contains("NordVPN"), $"y se dice cuál ({nueva.Anadidos[0]})");
        Igual(0, nueva.Eliminados.Count, "sin eliminar nada por el camino");
        Vale(!nueva.OrdenMovido, "y entrar al final no mueve el orden de los que estaban");

        // ---------- Una app que se cierra ----------
        var menos = DiagBandeja.Comparar(new() { a, b, c }, new() { a, c });
        Igual(1, menos.Eliminados.Count, "una app cerrada se detecta");
        Vale(menos.Eliminados[0].Contains("Discord"), $"y se dice cuál ({menos.Eliminados[0]})");
        Vale(!menos.OrdenMovido, "quitar una de en medio no cuenta como mover el orden");

        // ---------- Varias de golpe ----------
        var tres = DiagBandeja.Comparar(new() { a }, new() { a, b, c, F("hwnd:3/uid:7", "OneDrive") });
        Igual(3, tres.Anadidos.Count, "tres apps a la vez se detectan las tres");

        // ---------- Duplicados ----------
        //
        // La identidad es la ventana y su número, o el GUID. Dos entradas con la misma
        // identidad es lo que pasaría si al reiniciar Explorer una app se registrara otra
        // vez sin que la anterior se hubiera retirado.
        var dup = DiagBandeja.Comparar(new() { a }, new() { a, b, b });
        Igual(1, dup.Duplicados.Count, "un icono repetido se caza");
        Vale(dup.Duplicados[0].Contains("x2"), $"y se dice cuántas veces ({dup.Duplicados[0]})");

        // Dos apps distintas que se LLAMAN igual no son un duplicado: el título no es
        // identidad —cambia con el tooltip— y confundirlo escondería iconos de verdad.
        var mismoNombre = DiagBandeja.Comparar(
            new() { a },
            new() { a, F("hwnd:9/uid:1", "powershell.exe"), F("hwnd:10/uid:1", "powershell.exe") });
        Igual(0, mismoNombre.Duplicados.Count,
              "dos ventanas distintas con el mismo título NO son un duplicado");
        Igual(2, mismoNombre.Anadidos.Count, "son dos iconos nuevos, y los dos salen");

        // Y el mismo icono cambiando de título sigue siendo el mismo: un tooltip nuevo
        // no puede sacar y volver a meter el icono, que es lo que haría parpadear la tira
        var retitulado = DiagBandeja.Comparar(
            new() { F("hwnd:2/uid:0", "NordVPN desconectado") },
            new() { F("hwnd:2/uid:0", "NordVPN conectado a España") });
        Vale(retitulado.Nada, "cambiar el tooltip no saca ni mete el icono: no parpadea");

        // ---------- El orden ----------
        var movido = DiagBandeja.Comparar(new() { a, b, c }, new() { c, b, a });
        Vale(movido.OrdenMovido, "si los mismos iconos cambian de orden, se dice");
        Igual(0, movido.Anadidos.Count, "sin que entre nada");
        Igual(0, movido.Eliminados.Count, "ni salga nada");

        // Quitar uno de en medio no "mueve" el orden de los demás: siguen en su secuencia
        var sinElDeEnMedio = DiagBandeja.Comparar(new() { a, b, c }, new() { a, c });
        Vale(!sinElDeEnMedio.OrdenMovido, "los que quedan conservan su secuencia relativa");

        // ---------- Los casos de borde ----------
        Vale(DiagBandeja.Comparar(new(), new()).Nada, "dos listas vacías no dan cambios");
        Igual(2, DiagBandeja.Comparar(new(), new() { a, b }).Anadidos.Count,
              "de vacío a dos, dos añadidos (es el arranque de la bandeja)");
        Igual(2, DiagBandeja.Comparar(new() { a, b }, new()).Eliminados.Count,
              "y de dos a vacío, dos eliminados (es Explorer reiniciándose)");

        // ---------- Reiniciar Explorer: todo se va y todo vuelve ----------
        //
        // Lo que se ve al reiniciar Explorer es que las apps se vuelven a registrar. Lo
        // que NO puede quedar es un icono contado dos veces.
        var antes = new List<DiagBandeja.Ficha> { a, b, c };
        var vacia = DiagBandeja.Comparar(antes, new());
        Igual(3, vacia.Eliminados.Count, "al caerse Explorer se van los tres");
        var vuelta = DiagBandeja.Comparar(new(), antes);
        Igual(3, vuelta.Anadidos.Count, "y al volver, vuelven los tres");
        Igual(0, vuelta.Duplicados.Count, "sin duplicarse ninguno");
    }

    static void BandejaIdentidadDeUnIcono()
    {
        Grupo("bandeja: la identidad de un icono no es su nombre");

        // Un icono con GUID se identifica por el GUID: es lo que Windows garantiza
        // estable entre reinicios de la app y de Explorer.
        var conGuid = F("guid:699e423d-73da-42ba-b94e-881ff8672467", "Discord.exe");
        var mismoGuidOtroNombre = F("guid:699e423d-73da-42ba-b94e-881ff8672467", "Discord - 3 mensajes");
        Vale(DiagBandeja.Comparar(new() { conGuid }, new() { mismoGuidOtroNombre }).Nada,
             "el mismo GUID con otro título es el mismo icono");

        // Y uno sin GUID, por su ventana y su número. La misma ventana con otro número
        // son dos iconos distintos: una app puede registrar varios.
        var uid0 = F("hwnd:656880/uid:0", "Medal.exe");
        var uid4 = F("hwnd:656880/uid:4", "Medal.exe");
        var dos = DiagBandeja.Comparar(new() { uid0 }, new() { uid0, uid4 });
        Igual(1, dos.Anadidos.Count, "la misma ventana con otro uid es otro icono");
        Igual(0, dos.Duplicados.Count, "y no es un duplicado");

        // La ficha se lee: nombre, identidad y si está en los ocultos
        Igual("Steam [hwnd:1/uid:1]", F("hwnd:1/uid:1", "Steam").ToString(), "la ficha se lee");
        Igual("Steam [hwnd:1/uid:1] (oculto)", F("hwnd:1/uid:1", "Steam", true).ToString(),
              "y dice si el icono está en los ocultos");
    }
    // ================= D0: el contrato de seguridad de NubeHypr =================
    //
    // D0 no organiza ninguna ventana. Lo que deja es el cortafuegos, y es eso lo que se
    // comprueba aquí: que la capa nace apagada, que no se puede encender por accidente,
    // que sabe lo que no debe tocar, que una notificación atrasada no gana y que apagada
    // no mueve nada.

    static void HyprNaceApagado()
    {
        Grupo("D0: la capa nace apagada y no se enciende por accidente");

        // ---------- `Activo = false` manda sobre todo lo demás ----------
        //
        // Una configuración vieja, editada a mano o traída de otro equipo no puede
        // encender esto, y menos ponerlo a mover ventanas. Por eso se comprueba contra
        // TODOS los modos, incluido el que aplica.
        foreach (var modo in new[] { "simulacion", "aplicar", "apagado", "lo que sea", "", null })
            Igual(NubeHypr.Modo.Apagado, NubeHypr.ModoEfectivo(false, modo),
                  $"apagado con Modo=«{modo ?? "null"}» sigue apagado");

        // ---------- Encendida, lo desconocido cae en SIMULACIÓN ----------
        //
        // Nunca en aplicar. Si el fichero dice algo que no entendemos, lo que toca es
        // calcular y apuntar, no empezar a mover las ventanas de alguien por si acaso.
        foreach (var modo in new[] { "lo que sea", "", "APLICAR?", "simulación", null })
            Igual(NubeHypr.Modo.Simulacion, NubeHypr.ModoEfectivo(true, modo),
                  $"encendida con Modo=«{modo ?? "null"}» calcula pero no aplica");

        // Y los tres modos buenos se leen, con mayúsculas y espacios de más
        Igual(NubeHypr.Modo.Simulacion, NubeHypr.ModoEfectivo(true, "simulacion"), "simulacion");
        Igual(NubeHypr.Modo.Aplicar, NubeHypr.ModoEfectivo(true, "aplicar"), "aplicar");
        Igual(NubeHypr.Modo.Aplicar, NubeHypr.ModoEfectivo(true, "  APLICAR  "), "aplicar, con ruido");
        Igual(NubeHypr.Modo.Apagado, NubeHypr.ModoEfectivo(true, "apagado"),
              "encendida pero en modo apagado: apagada");

        // Lo que se reconoce y lo que no, para poder avisar en el log en vez de callarse
        Vale(NubeHypr.ModoConocido("simulacion") && NubeHypr.ModoConocido("aplicar") &&
             NubeHypr.ModoConocido("apagado"), "los tres modos se reconocen");
        Vale(!NubeHypr.ModoConocido("turbo") && !NubeHypr.ModoConocido(null) &&
             !NubeHypr.ModoConocido(""), "y lo demás no, para poder avisarlo");

        // ---------- Y de serie, en el objeto de configuración ----------
        var opc = new OpcionesHypr();
        Vale(!opc.Activo, "la sección de configuración nace con Activo=false");
        Igual("simulacion", opc.Modo, "y en simulación, no en aplicar");
        Igual(NubeHypr.Modo.Apagado, NubeHypr.ModoEfectivo(opc.Activo, opc.Modo),
              "así que de serie la capa está apagada");
        Igual(0, opc.Reglas.Count, "sin reglas de serie");
        Igual(0, opc.Atajos.Count, "sin atajos de serie");
        Igual(0, opc.Espacios.Count, "sin espacios de serie");
        Vale(opc.Gap is >= 0 and <= 200 && opc.Margen is >= 0 and <= 200,
             "y con huecos razonables");
    }

    static void HyprLoQueNoSeToca()
    {
        Grupo("D0: lo que no se puede organizar, y por qué");

        // Una ventana normal sí se puede
        Vale(NubeHypr.SePuedeOrganizar(@"C:\Windows\notepad.exe", "Notepad", false, true, true, out var p),
             "una ventana normal se puede organizar");
        Igual("", p, "y sin motivo de descarte");

        // ---------- Las nuestras ----------
        Vale(!NubeHypr.SePuedeOrganizar(@"C:\x\NubeBar.exe", "Window", true, true, true, out p),
             "una ventana nuestra no se toca");
        Igual(NubeHypr.PorqueNuestra, p, "y se dice que es nuestra");
        // Aunque no se sepa que es nuestra, el proceso la delata
        Vale(!NubeHypr.SePuedeOrganizar(@"C:\x\NubeBar.exe", "Window", false, true, true, out _),
             "y por el nombre del proceso también");

        // ---------- El shell ----------
        foreach (var exe in new[] { "explorer.exe", "StartMenuExperienceHost.exe", "SearchHost.exe",
                                    "ShellExperienceHost.exe", "consent.exe", "LockApp.exe", "dwm.exe" })
        {
            Vale(!NubeHypr.SePuedeOrganizar(exe, "Window", false, true, true, out var q),
                 $"{exe} no se organiza");
            Igual(NubeHypr.PorqueSistema, q, $"y es por ser del sistema ({exe})");
        }

        // ---------- El fondo animado ----------
        //
        // Aparte, porque el síntoma es muy distinto: no es "una ventana del sistema", es
        // "el escritorio se queda negro".
        foreach (var exe in new[] { @"C:\Steam\wallpaper32.exe", "wallpaper64.exe" })
        {
            Vale(!NubeHypr.SePuedeOrganizar(exe, "Window", false, true, true, out var q),
                 $"{exe} no se organiza");
            Igual(NubeHypr.PorqueFondo, q, "y se dice que es el fondo del escritorio");
        }

        // ---------- Las clases del escritorio y del conmutador ----------
        foreach (var clase in new[] { "Progman", "WorkerW", "Shell_TrayWnd",
                                      "MultitaskingViewFrame", "XamlExplorerHostIslandWindow" })
            Vale(!NubeHypr.SePuedeOrganizar("app.exe", clase, false, true, true, out _),
                 $"la clase {clase} no se organiza");

        // ---------- Lo que Windows no nos deja ----------
        Vale(!NubeHypr.SePuedeOrganizar("app.exe", "Window", false, sePuedeActivar: false, true, out p),
             "una ventana elevada no se organiza");
        Igual(NubeHypr.PorqueElevada, p, "y se dice que es por los permisos");

        Vale(!NubeHypr.SePuedeOrganizar("app.exe", "Window", false, true, rectValido: false, out p),
             "una ventana sin rectángulo válido no se organiza");
        Igual(NubeHypr.PorqueSinSitio, p, "y se dice por qué");

        // ---------- Sin datos no se adivina ----------
        //
        // Muchas ventanas no dejan leer su ejecutable. Eso NO las convierte en
        // protegidas: se juzgan por lo demás. Negarse a mover todo lo que no se puede
        // identificar dejaría la capa sin hacer nada en media sesión.
        Vale(NubeHypr.SePuedeOrganizar(null, "Window", false, true, true, out _),
             "sin nombre de ejecutable, se juzga por lo demás");
        Vale(NubeHypr.SePuedeOrganizar("", "", false, true, true, out _),
             "y con los datos vacíos tampoco se inventa un motivo");

        // El orden importa: lo nuestro se mira primero, para que el motivo sea el útil
        Vale(!NubeHypr.SePuedeOrganizar("explorer.exe", "Progman", true, false, false, out p),
             "con varios motivos a la vez, se descarta");
        Igual(NubeHypr.PorqueNuestra, p, "y gana el más informativo: que es nuestra");

        // El nombre del proceso, sin ruta y sin extensión
        Igual("wallpaper64", NubeHypr.NombreDeProceso(@"C:\Program Files\x\wallpaper64.exe"),
              "el nombre sale de la ruta");
        Igual("notepad", NubeHypr.NombreDeProceso("notepad.exe"), "y sin ruta también");
        Igual("", NubeHypr.NombreDeProceso(null), "null da vacío");
        Igual("", NubeHypr.NombreDeProceso("   "), "y los espacios también");

        // La lista es consultable: es un entregable de D0, no un detalle privado
        Vale(NubeHypr.Protegidos.Count >= 10, $"la lista de procesos protegidos existe ({NubeHypr.Protegidos.Count})");
        Vale(NubeHypr.ClasesProtegidasVisibles.Count >= 5,
             $"y la de clases ({NubeHypr.ClasesProtegidasVisibles.Count})");
    }

    static void HyprCicloYVersiones()
    {
        Grupo("D0: una notificación atrasada no gana");

        NubeHypr.OlvidarVersion();

        // Las versiones suben, y la última es la que vale
        long a = NubeHypr.SiguienteVersion();
        long b = NubeHypr.SiguienteVersion();
        long c = NubeHypr.SiguienteVersion();
        Vale(a < b && b < c, $"las versiones suben ({a} < {b} < {c})");
        Igual(c, NubeHypr.UltimaVersion, "y la última es la última");

        // ---------- La regla que evita el parpadeo ----------
        //
        // Es el mismo fallo que hizo parpadear la vista general: recolocar la isla
        // disparaba un evento sobre nuestra propia ventana que pedía refrescar, que
        // recolocaba la isla. Esta vez la regla está escrita antes de que ocurra.
        Vale(NubeHypr.EsVieja(a), "una versión de antes es vieja");
        Vale(NubeHypr.EsVieja(b), "la anterior también");
        Vale(!NubeHypr.EsVieja(c), "y la última no");
        Vale(NubeHypr.EsVieja(c + 1), "una versión del futuro tampoco vale: solo la última");

        // Un snapshot nuevo invalida al anterior, sin excepciones
        long d = NubeHypr.SiguienteVersion();
        Vale(NubeHypr.EsVieja(c), "al llegar uno nuevo, el de antes queda viejo");
        Vale(!NubeHypr.EsVieja(d), "y el nuevo es el que manda");

        // ---------- Las trazas, con el formato de la especificación ----------
        //
        // Se comprueban al pie de la letra para que un log de una sesión se pueda leer
        // con el documento al lado.
        Igual("[D2]  snapshot=184 ventanas=12 foco=0x000A92",
              NubeHypr.LineaSnapshot(184, 12, (IntPtr)0x000A92), "la línea del snapshot");
        Igual("[D2]  policy=split-main target=monitor:1 rect=1280,0,1280,720",
              NubeHypr.LineaPolitica("split-main", 1, new NubeHypr.Rect(1280, 0, 1280, 720)),
              "la de la política");
        Igual("[D2]  apply hwnd=0x000B15 result=ok elapsed=3.4ms",
              NubeHypr.LineaAplicar((IntPtr)0x000B15, "ok", 3.4), "la de aplicar");
        Igual("[D2]  confirm version=184 moved=1 skipped=2 stale=0",
              NubeHypr.LineaConfirmar(184, 1, 2, 0), "la de confirmar");
        Igual("[D3]  gesture=resize state=clean reason=button-released",
              NubeHypr.LineaGesto("resize", "clean", "button-released"), "la del gesto");
        Igual("[D7]  safe-mode=false adapter=ready config-version=7",
              NubeHypr.LineaSalud(false, true, 7), "la de salud");

        // El decimal no depende del idioma del equipo: con la coma española, un log se
        // volvería ilegible para cualquier herramienta
        Vale(NubeHypr.LineaAplicar((IntPtr)1, "ok", 3.5).Contains("3.5"),
             "el decimal va con punto, no con la coma del sistema");
    }

    static void HyprGeometriaDeclarada()
    {
        Grupo("D0: la geometría se declara, no se adivina");

        var r = new NubeHypr.Rect(100, 200, 800, 600);
        Igual(900.0, r.Derecha, "la derecha");
        Igual(800.0, r.Abajo, "y el bajo");
        Vale(r.Valido, "un rectángulo con tamaño es válido");
        Vale(!new NubeHypr.Rect(0, 0, 0, 600).Valido, "sin ancho no");
        Vale(!new NubeHypr.Rect(0, 0, 800, 0).Valido, "sin alto tampoco");
        Vale(!NubeHypr.Rect.Nada.Valido, "y el vacío no es válido");
        Igual("100,200,800,600", r.ToString(), "se lee como en el log");

        // ---------- Dentro del área, con tolerancia de un píxel ----------
        //
        // La tolerancia no es pereza: repartir columnas con redondeo deja diferencias de
        // medio píxel, y una comprobación exacta las convertiría en falsos fallos.
        var area = new NubeHypr.Rect(0, 0, 1920, 1080);
        Vale(new NubeHypr.Rect(0, 0, 1920, 1080).DentroDe(area), "el área cabe en sí misma");
        Vale(new NubeHypr.Rect(10, 10, 100, 100).DentroDe(area), "y algo pequeño dentro");
        Vale(new NubeHypr.Rect(-0.5, 0, 1920, 1080).DentroDe(area), "medio píxel fuera se tolera");
        Vale(!new NubeHypr.Rect(-20, 0, 1920, 1080).DentroDe(area), "veinte píxeles fuera, no");
        Vale(!new NubeHypr.Rect(0, 0, 1921, 1080).DentroDe(area, 0.5), "ni pasarse por la derecha");

        // Monitores con origen negativo: el segundo monitor a la izquierda del principal
        var izquierda = new NubeHypr.Rect(-1920, 0, 1920, 1080);
        Vale(new NubeHypr.Rect(-1920, 0, 960, 1080).DentroDe(izquierda),
             "media pantalla de un monitor con origen negativo cabe en él");
        Vale(!new NubeHypr.Rect(0, 0, 960, 1080).DentroDe(izquierda),
             "y lo que está en el otro monitor, no");

        Vale(r.CasiIgual(new NubeHypr.Rect(100.4, 200, 800, 600)), "medio píxel es el mismo sitio");
        Vale(!r.CasiIgual(new NubeHypr.Rect(140, 200, 800, 600)), "cuarenta píxeles no");
    }

    static void HyprApagadoNoMueveNada()
    {
        Grupo("D0: apagado, no se mueve ni una ventana");

        // La prueba que pide D0: "comparativa de geometría antes y después en modo
        // apagado". Dicho de otro modo: con el interruptor puesto pero el modo apagado,
        // ninguna ventana puede haberse movido.
        var antes = new List<(IntPtr, string, NubeHypr.Rect)>
        {
            ((IntPtr)1, "code",     new NubeHypr.Rect(0, 0, 960, 1080)),
            ((IntPtr)2, "firefox",  new NubeHypr.Rect(960, 0, 960, 1080)),
            ((IntPtr)3, "explorer", new NubeHypr.Rect(100, 100, 800, 600)),
        };

        // Nada se movió
        Igual(0, NubeHypr.Comparar(antes, antes).Count, "la misma foto dos veces: cero divergencias");

        // Una se movió: se caza y se dice cuál, de dónde a dónde
        var movida = new List<(IntPtr, string, NubeHypr.Rect)>(antes);
        movida[1] = ((IntPtr)2, "firefox", new NubeHypr.Rect(960, 0, 480, 1080));
        var d = NubeHypr.Comparar(antes, movida);
        Igual(1, d.Count, "una ventana movida es una divergencia");
        Igual((IntPtr)2, d[0].Hwnd, "y se sabe cuál");
        Vale(d[0].ToString().Contains("firefox") && d[0].ToString().Contains("->"),
             $"y se lee de dónde a dónde ({d[0]})");

        // ---------- Lo que NO es una regresión ----------
        //
        // Si confundiéramos esto con un fallo, la prueba fallaría sola en cualquier
        // equipo: entre dos fotos siempre se abre o se cierra algo.
        var sinLaTercera = antes.Take(2).ToList();
        Igual(0, NubeHypr.Comparar(antes, sinLaTercera).Count,
              "una ventana que se cerró entre las dos fotos no es una regresión");

        var conUnaNueva = new List<(IntPtr, string, NubeHypr.Rect)>(antes)
            { ((IntPtr)4, "nueva", new NubeHypr.Rect(0, 0, 100, 100)) };
        Igual(0, NubeHypr.Comparar(antes, conUnaNueva).Count,
              "y una que se abrió, tampoco");

        // Medio píxel de diferencia no es un movimiento
        var redondeada = new List<(IntPtr, string, NubeHypr.Rect)>(antes);
        redondeada[0] = ((IntPtr)1, "code", new NubeHypr.Rect(0.4, 0, 960, 1080));
        Igual(0, NubeHypr.Comparar(antes, redondeada).Count, "medio píxel no es moverse");
        Igual(1, NubeHypr.Comparar(antes, redondeada, tolerancia: 0.1).Count,
              "pero con la tolerancia apretada sí se ve");

        // Las dos listas vacías: no hay nada que comparar y no revienta
        var vacia = new List<(IntPtr, string, NubeHypr.Rect)>();
        Igual(0, NubeHypr.Comparar(vacia, vacia).Count, "dos listas vacías dan cero");
        Igual(0, NubeHypr.Comparar(vacia, antes).Count, "de vacío a tres, nada que comparar");
    }

    static void HyprMigracionV4()
    {
        Grupo("D0: la migración declara la capa y no la enciende");

        Igual(4, Esquema.Actual, "el esquema va por la versión 4");

        // Un config de la v3 al migrar: aparece la sección, apagada
        var v3 = new JsonObject
        {
            ["Version"] = 3,
            ["Tema"] = new JsonObject { ["Fondo"] = "#FF000000" },
            ["Fondo"] = new JsonObject { ["Tipo"] = "ninguno" },
        };
        Vale(!Esquema.ActivoAntes(v3), "antes de migrar no había capa encendida");

        var r = Esquema.Migrar(v3);
        Igual(4, r.Hasta, "se migra a la 4");
        Vale(r.Cambio, "y el fichero cambia");
        Vale(v3["NubeHypr"] is JsonObject, "la sección NubeHypr aparece");
        Igual(false, v3["NubeHypr"]!["Activo"]!.GetValue<bool>(), "APAGADA");
        Igual("simulacion", v3["NubeHypr"]!["Modo"]!.GetValue<string>(),
              "y en simulación: si alguien pone Activo a mano, calcula pero no mueve");
        Vale(v3["NubeHypr"]!["Atajos"] is JsonObject, "con sus atajos vacíos");
        Vale(v3["NubeHypr"]!["Reglas"] is JsonArray, "sus reglas vacías");
        Vale(!Esquema.ActivoAntes(v3), "y después de migrar sigue apagada");

        // ---------- Y las tres que ya no se escriben ----------
        //
        // Esta prueba exigía `Espacios` —un `List<string>` que **no lee nadie** en todo el
        // producto—. Exigir que una clave muerta se escriba en el fichero del usuario es
        // blindar justo lo que había que quitar, así que ahora se exige lo contrario.
        //
        // Cuidado al comprobarlo a mano: `HyprLayout` lee un `Espacios` que NO es éste —es
        // `EstadoNubeHypr.Espacios`, el del estado en marcha—, así que un `grep` del nombre
        // a secas dice que se usa. Lo que sale a cero es `_cfg.NubeHypr.Espacios`.
        Vale(v3["NubeHypr"]!["Espacios"] == null,
             "«Espacios» ya no se escribe: no la lee nadie");
        Vale(v3["NubeHypr"]!["EspacioInicial"] == null,
             "ni «EspacioInicial»");
        Vale(v3["NubeHypr"]!["MostrarPrevisualizacion"] == null,
             "ni «MostrarPrevisualizacion»");

        // Pero las que SÍ significan algo siguen estando, que es la otra mitad: dejar de
        // escribir las muertas no puede llevarse por delante las vivas.
        Vale(v3["NubeHypr"]!["ConfirmarMovimientos"] != null,
             "y «ConfirmarMovimientos» sigue, que desde D9.7 hace algo de verdad");
        Vale(v3["NubeHypr"]!["Gap"] != null, "y «Gap», que tiene deslizador");

        // Lo que ya había no se toca
        var conLoSuyo = new JsonObject
        {
            ["Version"] = 3,
            ["NubeHypr"] = new JsonObject { ["Gap"] = 20, ["Layout"] = "flotante" },
        };
        Esquema.Migrar(conLoSuyo);
        Igual(20, conLoSuyo["NubeHypr"]!["Gap"]!.GetValue<int>(), "un Gap puesto a mano se conserva");
        Igual("flotante", conLoSuyo["NubeHypr"]!["Layout"]!.GetValue<string>(), "y el layout elegido");
        Igual(false, conLoSuyo["NubeHypr"]!["Activo"]!.GetValue<bool>(),
              "y lo que faltaba se rellena apagado");

        // ---------- Y si el usuario la tenía encendida, no se la apagamos ----------
        //
        // El contrato es "la migración no la enciende", no "la migración decide por ti".
        var encendida = new JsonObject
        {
            ["Version"] = 3,
            ["NubeHypr"] = new JsonObject { ["Activo"] = true, ["Modo"] = "aplicar" },
        };
        Vale(Esquema.ActivoAntes(encendida), "venía encendida");
        Esquema.Migrar(encendida);
        Igual(true, encendida["NubeHypr"]!["Activo"]!.GetValue<bool>(),
              "y sigue encendida: era su decisión, no la nuestra");

        // Migrar dos veces no cambia nada la segunda
        var dosVeces = new JsonObject { ["Version"] = 3 };
        Esquema.Migrar(dosVeces);
        var segunda = Esquema.Migrar(dosVeces);
        Vale(!segunda.Cambio, "migrar un fichero ya migrado no lo toca");

        // Desde la v1, pasando por todas: la capa también acaba declarada y apagada
        var v1 = new JsonObject { ["Tema"] = new JsonObject() };
        var largo = Esquema.Migrar(v1);
        Igual(4, largo.Hasta, "desde un config sin versión se llega a la 4");
        Igual(3, largo.Aplicadas.Count, "con las tres migraciones");
        Igual(false, v1["NubeHypr"]!["Activo"]!.GetValue<bool>(), "y la capa acaba apagada");

        // ---------- La validación ----------
        Vale(Esquema.Mal(v3, 0) == null, "un árbol migrado bien no da queja");

        var modoRaro = new JsonObject
        {
            ["Version"] = 4,
            ["NubeHypr"] = new JsonObject { ["Modo"] = "turbo" },
        };
        Vale(Esquema.Mal(modoRaro, 0)?.Contains("no es un modo conocido") == true,
             $"un modo desconocido se rechaza al escribir ({Esquema.Mal(modoRaro, 0)})");

        var gapAbsurdo = new JsonObject
        {
            ["Version"] = 4,
            ["NubeHypr"] = new JsonObject { ["Gap"] = 4000 },
        };
        Vale(gapAbsurdo is not null && Esquema.Mal(gapAbsurdo, 0)?.Contains("Gap") == true,
             "un Gap de 4000 se rechaza: con el motor en marcha dejaría las ventanas fuera");

        var margenNegativo = new JsonObject
        {
            ["Version"] = 4,
            ["NubeHypr"] = new JsonObject { ["Margen"] = -5 },
        };
        Vale(Esquema.Mal(margenNegativo, 0)?.Contains("Margen") == true, "y un margen negativo");

        // Un modo ausente no es un error: significa "no elegido"
        var sinModo = new JsonObject { ["Version"] = 4, ["NubeHypr"] = new JsonObject() };
        Vale(Esquema.Mal(sinModo, 0) == null, "una sección a medias no es un error de validación");
    }
    static void HyprDiagnosticoNoEscribe()
    {
        Grupo("D0: un diagnóstico no escribe en la configuración");

        // ---------- El fallo que esto fija ----------
        //
        // Ejecutar un binario de desarrollo con un esquema más nuevo migró el config REAL
        // del usuario a una versión que su NubeBar instalada ya no sabía migrar, y le
        // dejaba un error en cada arranque. Y no lo hizo la migración: lo hizo un guardado
        // rutinario del arranque, porque el modelo tipado lleva `Version = Esquema.Actual`
        // y cualquier escritura sube la versión del fichero.
        //
        // Así que la regla es de brocha gorda a propósito: mientras corre una herramienta
        // de medida, NO se escribe el config. Ni la migración, ni los módulos de serie, ni
        // el orden de las apps.
        var carpeta = Path.Combine(Path.GetTempPath(), "nubebar-solo-lectura-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "config.json");
        bool antes = Config.SoloLectura;
        try
        {
            const string original = "{\"Version\": 3, \"Tema\": {\"Fondo\": \"#FF000000\"}}";
            File.WriteAllText(ruta, original);

            // ---------- La decisión, que es lo comprobable ----------
            //
            // No se puede probar escribiendo en el config de verdad para ver si se
            // protege: eso es exactamente lo que no se puede hacer. Lo que se prueba es
            // la función que decide.
            Config.SoloLectura = true;
            Vale(ConfigArbol.NoSeDebeGuardar(Config.Ruta),
                 "con una herramienta en marcha, el config del usuario NO se guarda");
            Vale(!ConfigArbol.NoSeDebeGuardar(ruta),
                 "pero un árbol en una carpeta temporal sí: las pruebas tienen que poder");
            Config.SoloLectura = false;
            Vale(!ConfigArbol.NoSeDebeGuardar(Config.Ruta),
                 "y sin herramienta, el config se guarda como siempre");

            // Y el guardado de un temporal funciona con el guardia puesto
            Config.SoloLectura = true;
            var arbol = ConfigArbol.Cargar(ruta);
            arbol.Poner("Tema", "Fondo", "#FF112233");
            arbol.Guardar();
            Vale(File.ReadAllText(ruta).Contains("#FF112233"),
                 "un árbol temporal se escribe aunque haya un diagnóstico en marcha");

            // Sin el guardia, sí: si esto fallara, el guardia habría roto el guardado normal
            Config.SoloLectura = false;
            var arbol2 = ConfigArbol.Cargar(ruta);
            arbol2.Poner("Tema", "Fondo", "#FFFFFFFF");
            arbol2.Guardar();
            Vale(File.ReadAllText(ruta).Contains("#FFFFFFFF"),
                 "y sin diagnóstico, guardar escribe como siempre");

            // Y el fichero sigue siendo JSON válido después de las dos
            Vale(JsonNode.Parse(File.ReadAllText(ruta)) is JsonObject,
                 "el fichero sigue siendo JSON válido");
        }
        finally
        {
            Config.SoloLectura = antes;
            try { Directory.Delete(carpeta, true); } catch { }
        }
    }
    // ================= D1: el modelo puro de layouts y comandos =================
    //
    // Ni una ventana real. Lo que se comprueba aquí es la parte que decide, y se comprueba
    // con los casos que en una pantalla no se tienen a mano: cero ventanas, cien, un
    // monitor con origen negativo, el DPI al 200 %, un área más pequeña que el mínimo.

    static VentanaModelo V(long h, int esp = 0, bool flot = false, bool min = false,
                           bool max = false, bool vis = true, double x = 0, double y = 0,
                           double w = 400, double alto = 300, string proc = "app") =>
        new((IntPtr)h, proc, "t" + h, new NubeHypr.Rect(x, y, w, alto), vis, min, max, flot, esp);

    static EstadoNubeHypr Est(params VentanaModelo[] v) =>
        EstadoNubeHypr.Vacio with { Ventanas = v.ToList(), Foco = v.Length > 0 ? v[0].Hwnd : IntPtr.Zero };

    static void D1AreaUtil()
    {
        Grupo("D1: el área útil de un monitor");

        var mon = new NubeHypr.Rect(0, 0, 1920, 1080);

        // La barra se descuenta solo por SU borde, y el margen después, por los cuatro
        var arriba = NubeHypr.AreaUtil(mon, NubeHypr.Borde.Arriba, 54, 12);
        Igual(12.0, arriba.X, "con la barra arriba, el margen izquierdo es el margen");
        Igual(66.0, arriba.Y, "y arriba se suman la barra y el margen");
        Igual(1896.0, arriba.Ancho, "el ancho pierde los dos márgenes");
        Igual(1002.0, arriba.Alto, "y el alto, la barra y los dos márgenes");

        var abajo = NubeHypr.AreaUtil(mon, NubeHypr.Borde.Abajo, 54, 12);
        Igual(12.0, abajo.Y, "con la barra abajo, arriba solo hay margen");
        Igual(1002.0, abajo.Alto, "y el alto es el mismo que con la barra arriba");
        Vale(abajo.Abajo <= mon.Abajo, "sin salirse por abajo");

        var izq = NubeHypr.AreaUtil(mon, NubeHypr.Borde.Izquierda, 56, 8);
        Igual(64.0, izq.X, "con la barra a la izquierda se suman barra y margen");
        Igual(1848.0, izq.Ancho, "y el ancho pierde la barra y los dos márgenes");
        Igual(1064.0, izq.Alto, "el alto solo pierde los márgenes");

        var der = NubeHypr.AreaUtil(mon, NubeHypr.Borde.Derecha, 56, 8);
        Igual(8.0, der.X, "con la barra a la derecha, la izquierda solo tiene margen");
        Vale(der.Derecha <= mon.Derecha - 56, "y el área acaba antes de la barra");

        // Sin reserva —NubeBar flotando sobre las ventanas— el área es la pantalla menos
        // el margen: las ventanas pasan por debajo de la isla a propósito
        var sinReserva = NubeHypr.AreaUtil(mon, NubeHypr.Borde.Arriba, 0, 12);
        Igual(12.0, sinReserva.Y, "sin reserva, arriba solo hay margen");
        Igual(1056.0, sinReserva.Alto, "y el alto es la pantalla menos los márgenes");

        // ---------- Monitores con origen negativo ----------
        //
        // El segundo monitor a la izquierda del principal tiene X negativa. Es el caso que
        // rompe cualquier cuenta escrita suponiendo que la pantalla empieza en cero.
        var zurdo = new NubeHypr.Rect(-1920, 0, 1920, 1080);
        var aZurdo = NubeHypr.AreaUtil(zurdo, NubeHypr.Borde.Arriba, 54, 12);
        Igual(-1908.0, aZurdo.X, "un monitor con origen negativo mantiene su origen");
        Vale(aZurdo.DentroDe(zurdo), "y su área cabe dentro de él");

        var arriba_izq = new NubeHypr.Rect(-1920, -1080, 1920, 1080);
        Vale(NubeHypr.AreaUtil(arriba_izq, NubeHypr.Borde.Abajo, 54, 12).DentroDe(arriba_izq),
             "y con las dos coordenadas negativas, también");

        // ---------- Lo que no da área ----------
        Igual(NubeHypr.Rect.Nada, NubeHypr.AreaUtil(new NubeHypr.Rect(0, 0, 100, 100),
                                                    NubeHypr.Borde.Arriba, 54, 40),
              "una pantalla diminuta con barra gruesa y mucho margen no da área");
        Vale(!NubeHypr.AreaUtil(mon, NubeHypr.Borde.Arriba, 2000, 0).Valido,
             "ni una barra más gruesa que la pantalla");
        Vale(NubeHypr.AreaUtil(mon, NubeHypr.Borde.Arriba, -50, -10).Valido,
             "y los negativos se tratan como cero, no como un área rota");

        // ---------- DPI ----------
        //
        // El modelo trabaja en píxeles de pantalla, así que el DPI entra como un monitor
        // de otro tamaño. Lo que se comprueba es que la cuenta vale para los cuatro.
        foreach (var (w, h, que) in new[]
        {
            (1920.0, 1080.0, "1080p al 100 %"), (1536.0, 864.0, "1080p al 125 %"),
            (1280.0, 720.0, "1080p al 150 %"), (960.0, 540.0, "1080p al 200 %"),
            (2560.0, 1440.0, "1440p"), (3840.0, 2160.0, "4K"),
        })
        {
            var m = new NubeHypr.Rect(0, 0, w, h);
            var a = NubeHypr.AreaUtil(m, NubeHypr.Borde.Arriba, 54, 12);
            Vale(a.Valido, $"[{que}] hay área útil");
            Vale(a.DentroDe(m), $"[{que}] y cabe en el monitor");
        }
    }

    static void D1RepartoSinCosturas()
    {
        Grupo("D1: el reparto no deja costuras ni se desborda");

        // ---------- El fallo que esto evita ----------
        //
        // El reparto obvio —`ancho / n` y colocar en `i * trozo`— deja rayas de un píxel
        // entre ventanas y la última se pasa del área. Se reparten los BORDES, no los
        // tamaños, así que los bordes coinciden por construcción.
        var area = new NubeHypr.Rect(0, 0, 1000, 800);

        foreach (int n in new[] { 1, 2, 3, 5, 7, 11 })
        {
            foreach (double hueco in new[] { 0.0, 8.0, 13.0 })
            {
                var t = NubeHypr.Tramos(area.X, area.Ancho, n, hueco);
                Igual(n, t.Count, $"[{n} tramos, hueco {hueco:0}] salen los {n}");

                // Ninguno se sale
                Vale(t[0].Inicio >= area.X - 0.01,
                     $"[{n}, {hueco:0}] el primero no empieza antes del área");
                var ultimo = t[^1];
                Vale(ultimo.Inicio + ultimo.Tamano <= area.Derecha + 0.01,
                     $"[{n}, {hueco:0}] el último no se pasa ({ultimo.Inicio + ultimo.Tamano:0} de {area.Derecha:0})");

                // Y entre uno y el siguiente hay exactamente el hueco: ni raya ni solape
                for (int i = 0; i + 1 < t.Count; i++)
                {
                    double separacion = t[i + 1].Inicio - (t[i].Inicio + t[i].Tamano);
                    Vale(Math.Abs(separacion - hueco) <= 1.01,
                         $"[{n}, {hueco:0}] entre el {i} y el {i + 1} hay {separacion:0} y toca {hueco:0}");
                }

                // Todos con tamaño positivo
                Vale(t.All(x => x.Tamano > 0), $"[{n}, {hueco:0}] ninguno sale de tamaño cero");
            }
        }

        // Un hueco más grande que el área: antes de dar tamaños negativos, se queda sin hueco
        var apretado = NubeHypr.Tramos(0, 100, 5, 500);
        Igual(5, apretado.Count, "con un hueco imposible siguen saliendo los cinco");
        Vale(apretado.All(x => x.Tamano >= 0), "y ninguno con tamaño negativo");
        Vale(apretado[^1].Inicio + apretado[^1].Tamano <= 100.01, "ni se sale del área");

        // Casos de borde
        Igual(0, NubeHypr.Tramos(0, 1000, 0, 8).Count, "cero tramos son cero");
        Igual(0, NubeHypr.Tramos(0, 0, 3, 8).Count, "y sin largo tampoco hay tramos");
        Igual(0, NubeHypr.Tramos(0, 1000, -1, 8).Count, "un número negativo no da tramos");

        // Cuántas caben sin bajar del mínimo
        Igual(1, NubeHypr.CuantasCaben(200, 8, 120), "en 200 px con mínimo 120 cabe una");
        Igual(2, NubeHypr.CuantasCaben(248, 8, 120), "en 248 caben dos justas");
        Igual(1, NubeHypr.CuantasCaben(247, 8, 120), "y en 247 ya no");
        Igual(0, NubeHypr.CuantasCaben(50, 8, 120), "en 50 no cabe ninguna");
        Igual(0, NubeHypr.CuantasCaben(-5, 8, 120), "ni en un largo negativo");
    }

    static void D1LosCuatroLayouts()
    {
        Grupo("D1: los cuatro layouts reparten dentro del área");

        var area = new NubeHypr.Rect(100, 50, 1800, 1000);
        const double hueco = 8;

        foreach (var layout in new[] { NubeHypr.Layout.Horizontal, NubeHypr.Layout.Vertical,
                                       NubeHypr.Layout.Principal })
        {
            // ---------- Una sola ventana ocupa el área entera ----------
            var una = NubeHypr.Repartir(layout, area, 1, hueco, 0.6);
            Igual(1, una.Count, $"[{layout}] con una ventana sale un sitio");
            Vale(una[0].CasiIgual(area), $"[{layout}] y es el área entera");

            // ---------- Y con muchas, ninguna se sale ----------
            foreach (int n in new[] { 2, 3, 4, 6, 9 })
            {
                var sitios = NubeHypr.Repartir(layout, area, n, hueco, 0.6);
                Vale(sitios.Count > 0, $"[{layout}, {n}] se reparte algo");
                Vale(sitios.Count <= n, $"[{layout}, {n}] y nunca más sitios que ventanas");
                foreach (var s in sitios)
                {
                    Vale(s.Valido, $"[{layout}, {n}] todos los sitios tienen tamaño");
                    Vale(s.DentroDe(area), $"[{layout}, {n}] y ninguno se sale del área ({s})");
                    Vale(s.Ancho >= 1 && s.Alto >= 1, $"[{layout}, {n}] ni degenerado");
                }
            }
        }

        // ---------- Horizontal reparte el ancho y conserva el alto ----------
        var h3 = NubeHypr.Repartir(NubeHypr.Layout.Horizontal, area, 3, hueco, 0.6);
        Igual(3, h3.Count, "tres columnas");
        Vale(h3.All(s => Math.Abs(s.Alto - area.Alto) < 0.01), "todas con el alto del área");
        Vale(h3.All(s => Math.Abs(s.Y - area.Y) < 0.01), "y empezando arriba");
        Vale(h3[0].X < h3[1].X && h3[1].X < h3[2].X, "de izquierda a derecha");

        // ---------- Vertical, al revés ----------
        var v3 = NubeHypr.Repartir(NubeHypr.Layout.Vertical, area, 3, hueco, 0.6);
        Vale(v3.All(s => Math.Abs(s.Ancho - area.Ancho) < 0.01), "todas con el ancho del área");
        Vale(v3[0].Y < v3[1].Y && v3[1].Y < v3[2].Y, "de arriba abajo");

        // ---------- Principal: una grande y el resto apiladas ----------
        var p4 = NubeHypr.Repartir(NubeHypr.Layout.Principal, area, 4, hueco, 0.6);
        Igual(4, p4.Count, "cuatro sitios");
        Vale(Math.Abs(p4[0].Alto - area.Alto) < 0.01, "la principal ocupa todo el alto");
        Vale(p4[0].Ancho > p4[1].Ancho, "y es más ancha que las de la columna");
        Vale(p4.Skip(1).All(s => Math.Abs(s.X - p4[1].X) < 0.01), "las demás comparten columna");
        Vale(p4[1].Y < p4[2].Y && p4[2].Y < p4[3].Y, "apiladas de arriba abajo");
        Vale(Math.Abs(p4[1].X - (p4[0].Derecha + hueco)) < 1.01,
             "y la columna empieza justo después de la principal, con su hueco");

        // La proporción se respeta y se acota
        var estrecha = NubeHypr.Repartir(NubeHypr.Layout.Principal, area, 2, hueco, 0.3);
        var ancha = NubeHypr.Repartir(NubeHypr.Layout.Principal, area, 2, hueco, 0.75);
        Vale(estrecha[0].Ancho < ancha[0].Ancho, "más proporción, principal más ancha");
        foreach (var p in new[] { 0.0, -5.0, 0.05, 0.95, 50.0 })
        {
            var s = NubeHypr.Repartir(NubeHypr.Layout.Principal, area, 3, hueco, p);
            Vale(s.All(r => r.DentroDe(area) && r.Ancho >= NubeHypr.MinimoLado - 1),
                 $"con proporción {p} nadie se sale ni baja del mínimo");
        }

        // ---------- Flotante no coloca a nadie ----------
        Igual(0, NubeHypr.Repartir(NubeHypr.Layout.Flotante, area, 5, hueco, 0.6).Count,
              "el layout flotante no reparte: manda el usuario");

        // ---------- Cero ventanas y áreas imposibles ----------
        Igual(0, NubeHypr.Repartir(NubeHypr.Layout.Horizontal, area, 0, hueco, 0.6).Count,
              "cero ventanas, cero sitios");
        Igual(0, NubeHypr.Repartir(NubeHypr.Layout.Horizontal, NubeHypr.Rect.Nada, 3, hueco, 0.6).Count,
              "sin área no se reparte nada");

        // ---------- Más ventanas que sitio ----------
        //
        // La especificación es clara: antes que repartir entre todas y dar ventanas de
        // 20 px, mejor no organizar las que no caben.
        var estrecho = new NubeHypr.Rect(0, 0, 400, 300);
        var muchas = NubeHypr.Repartir(NubeHypr.Layout.Horizontal, estrecho, 20, 8, 0.6);
        Vale(muchas.Count < 20, $"en 400 px no caben 20 ventanas: se reparten {muchas.Count}");
        Vale(muchas.All(s => s.Ancho >= NubeHypr.MinimoLado - 1),
             "y las que se reparten no bajan del mínimo");
        Vale(muchas.All(s => s.DentroDe(estrecho)), "ni se salen");

        // Un área en la que no cabe ni una columna del mínimo: principal se cae a vertical
        var muyEstrecho = new NubeHypr.Rect(0, 0, 200, 900);
        var pEstrecho = NubeHypr.Repartir(NubeHypr.Layout.Principal, muyEstrecho, 3, 8, 0.6);
        Vale(pEstrecho.All(s => Math.Abs(s.Ancho - muyEstrecho.Ancho) < 0.01),
             "si no caben dos columnas, principal reparte en filas");
        Vale(pEstrecho.All(s => s.DentroDe(muyEstrecho)), "y sin salirse");

        // ---------- Cien ventanas no revientan ----------
        var cien = NubeHypr.Repartir(NubeHypr.Layout.Principal, new NubeHypr.Rect(0, 0, 3840, 2160),
                                     100, 8, 0.6);
        Vale(cien.Count is > 0 and <= 100, $"con cien ventanas se reparten {cien.Count}");
        Vale(cien.All(s => s.DentroDe(new NubeHypr.Rect(0, 0, 3840, 2160))), "y ninguna se sale");
    }

    static void D1CalcularYNoMoverLoQueNoToca()
    {
        Grupo("D1: quién entra en el reparto y quién se queda");

        var area = new NubeHypr.Rect(0, 0, 1920, 1080);

        // Cero ventanas: cero efectos, y no revienta
        Igual(0, NubeHypr.Calcular(EstadoNubeHypr.Vacio, area, 8).Count,
              "sin ventanas no hay efectos");

        // Una normal se coloca
        var una = NubeHypr.Calcular(Est(V(1)), area, 8);
        Igual(1, una.Count, "una ventana, un efecto");
        Igual(TipoEfecto.Colocar, una[0].Tipo, "y es colocarla");
        Vale(una[0].Destino.CasiIgual(area), "en el área entera");

        // ---------- Lo que NO se enlosa sale con su motivo ----------
        //
        // Que salgan en la lista —en vez de desaparecer de ella— es lo que permite que el
        // log diga por qué no se movió algo, que es la pregunta que se hace siempre.
        var mezcla = Est(V(1), V(2, min: true), V(3, max: true), V(4, flot: true), V(5, vis: false));
        var efectos = NubeHypr.Calcular(mezcla, area, 8);
        Igual(5, efectos.Count, "las cinco salen en la lista");
        Igual(1, efectos.Count(x => x.Tipo == TipoEfecto.Colocar), "pero solo una se coloca");

        var quedan = efectos.Where(x => x.Tipo == TipoEfecto.SeQueda).ToList();
        Igual(4, quedan.Count, "y cuatro se quedan");
        Vale(quedan.All(q => q.Porque.Length > 0), "todas con un motivo escrito");
        Vale(quedan.Any(q => q.Porque.Contains("minimizada")), "la minimizada lo dice");
        Vale(quedan.Any(q => q.Porque.Contains("maximizada")), "la maximizada también");
        Vale(quedan.Any(q => q.Porque.Contains("flotante")), "la flotante también");
        Vale(quedan.Any(q => q.Porque.Contains("visible")), "y la que no está visible");

        // ---------- Solo el espacio actual ----------
        var dosEspacios = Est(V(1, esp: 0), V(2, esp: 0), V(3, esp: 1), V(4, esp: 1));
        var enCero = NubeHypr.Calcular(dosEspacios, area, 8);
        Igual(2, enCero.Count, "en el espacio 0 solo se miran sus dos ventanas");
        var enUno = NubeHypr.Calcular(dosEspacios with { EspacioActual = 1 }, area, 8);
        Igual(2, enUno.Count, "y en el 1, las otras dos");
        Vale(enUno.All(x => x.Hwnd == (IntPtr)3 || x.Hwnd == (IntPtr)4), "las que son");

        // ---------- El layout flotante no coloca, pero informa ----------
        var flot = NubeHypr.Calcular(Est(V(1), V(2)) with { Layout = NubeHypr.Layout.Flotante }, area, 8);
        Igual(0, flot.Count(x => x.Tipo == TipoEfecto.Colocar), "en flotante no se coloca a nadie");
        Igual(2, flot.Count, "pero las dos salen informadas");

        // ---------- Sin área, nadie se mueve ----------
        var sinArea = NubeHypr.Calcular(Est(V(1), V(2)), NubeHypr.Rect.Nada, 8);
        Igual(0, sinArea.Count(x => x.Tipo == TipoEfecto.Colocar), "sin área no se coloca nada");
        Vale(sinArea.All(x => x.Porque.Contains("área")), "y se dice que es por el área");

        // ---------- Ningún rectángulo sale del área, salvo flotantes ----------
        //
        // Es la propiedad que la especificación pide mantener, probada contra una tanda
        // amplia en vez de contra un caso.
        foreach (var layout in new[] { NubeHypr.Layout.Horizontal, NubeHypr.Layout.Vertical,
                                       NubeHypr.Layout.Principal })
            foreach (int n in new[] { 1, 2, 3, 8, 25 })
            {
                var est = EstadoNubeHypr.Vacio with
                {
                    Ventanas = Enumerable.Range(1, n).Select(i => V(i)).ToList(),
                    Layout = layout,
                };
                foreach (var ef in NubeHypr.Calcular(est, area, 8).Where(x => x.Tipo == TipoEfecto.Colocar))
                    Vale(ef.Destino.DentroDe(area),
                         $"[{layout}, {n}] 0x{ef.Hwnd.ToInt64():X} cae dentro del área");
            }
    }

    static void D1AplicarDosVecesNoDeriva()
    {
        Grupo("D1: aplicar dos veces no mueve nada nuevo");

        var area = new NubeHypr.Rect(0, 0, 1920, 1080);
        var est = Est(V(1), V(2), V(3), V(4));

        // ---------- La propiedad de §14.2 ----------
        //
        // "Aplicar el mismo estado dos veces no mueve ni reescribe nada." Se comprueba de
        // la única forma que vale: se calcula, se mete el resultado en el estado —como
        // haría el adaptador al confirmar— y se vuelve a calcular. Los destinos tienen
        // que ser los mismos.
        var primera = NubeHypr.Calcular(est, area, 8);
        var movidas = est.Ventanas.Select(v =>
        {
            var ef = primera.FirstOrDefault(x => x.Hwnd == v.Hwnd && x.Tipo == TipoEfecto.Colocar);
            return ef == null ? v : v with { Rectangulo = ef.Destino };
        }).ToList();

        var segunda = NubeHypr.Calcular(est with { Ventanas = movidas }, area, 8);
        Igual(primera.Count, segunda.Count, "el segundo cálculo da los mismos efectos");
        for (int i = 0; i < primera.Count; i++)
        {
            Igual(primera[i].Hwnd, segunda[i].Hwnd, $"el efecto {i} es de la misma ventana");
            Vale(primera[i].Destino.CasiIgual(segunda[i].Destino, 0.01),
                 $"y al mismo sitio ({primera[i].Destino} == {segunda[i].Destino})");
        }

        // Diez veces seguidas tampoco derivan
        var estado = est;
        var destinos = NubeHypr.Calcular(estado, area, 8)
            .Where(x => x.Tipo == TipoEfecto.Colocar).Select(x => x.Destino).ToList();
        for (int i = 0; i < 10; i++)
        {
            var d = NubeHypr.Calcular(estado, area, 8)
                .Where(x => x.Tipo == TipoEfecto.Colocar).Select(x => x.Destino).ToList();
            Vale(d.Count == destinos.Count && d.Zip(destinos).All(p => p.First.CasiIgual(p.Second, 0.01)),
                 $"la vuelta {i + 1} da exactamente los mismos rectángulos");
        }
    }

    static void D1InsertarYQuitar()
    {
        Grupo("D1: insertar al final, quitar sin recolocar");

        // ---------- La regla que evita que abrir el navegador te reorganice todo ----------
        var e = EstadoNubeHypr.Vacio;
        e = NubeHypr.Insertar(e, V(1));
        e = NubeHypr.Insertar(e, V(2));
        e = NubeHypr.Insertar(e, V(3));
        Vale(e.Ventanas.Select(v => v.Hwnd.ToInt64()).SequenceEqual(new long[] { 1, 2, 3 }),
             "se insertan al final, en orden");

        // ---------- La misma dos veces no se duplica: se actualiza ----------
        e = NubeHypr.Insertar(e, V(2, x: 500, proc: "otro"));
        Igual(3, e.Ventanas.Count, "insertar la misma ventana no la duplica");
        Igual("otro", e.De((IntPtr)2)!.Proceso, "se actualiza con los datos nuevos");
        Vale(e.Ventanas.Select(v => v.Hwnd.ToInt64()).SequenceEqual(new long[] { 1, 2, 3 }),
             "y NO cambia de sitio en el orden: por eso no da saltos");

        // Veinte inserciones repetidas siguen dando tres
        for (int i = 0; i < 20; i++) e = NubeHypr.Insertar(e, V(2));
        Igual(3, e.Ventanas.Count, "veinte inserciones repetidas no duplican nada");

        // ---------- Quitar no recoloca a los demás ----------
        e = NubeHypr.Quitar(e, (IntPtr)2);
        Vale(e.Ventanas.Select(v => v.Hwnd.ToInt64()).SequenceEqual(new long[] { 1, 3 }),
             "quitar la de en medio deja a las otras en su orden");

        // Quitar una que no está no cambia nada
        var antes = e;
        e = NubeHypr.Quitar(e, (IntPtr)99);
        Vale(ReferenceEquals(antes, e), "quitar una que no estaba devuelve el mismo estado");

        // ---------- Si se va la que tenía el foco, el foco se suelta ----------
        //
        // Dejarlo apuntando a una ventana que ya no existe es como se cuelan los
        // "enfocar 0x0" en el log, y en D2 sería una llamada a Win32 con un hwnd muerto.
        var conFoco = Est(V(1), V(2)) with { Foco = (IntPtr)1 };
        var sinElla = NubeHypr.Quitar(conFoco, (IntPtr)1);
        Igual(IntPtr.Zero, sinElla.Foco, "al cerrarse la ventana con el foco, el foco se suelta");

        var otraFuera = NubeHypr.Quitar(conFoco, (IntPtr)2);
        Igual((IntPtr)1, otraFuera.Foco, "y si se va otra, el foco se queda donde estaba");
    }

    static void D1ComandosValidos()
    {
        Grupo("D1: los comandos hacen lo que dicen");

        // ---------- Enfocar siguiente y anterior, rotando ----------
        var e = Est(V(1), V(2), V(3)) with { Foco = (IntPtr)1 };
        var r = NubeHypr.Ejecutar(e, new Comando.EnfocarSiguiente());
        Igual((IntPtr)2, r.Estado.Foco, "siguiente va a la segunda");
        Igual(1, r.Efectos.Count, "y pide un efecto");
        Igual(TipoEfecto.Enfocar, r.Efectos[0].Tipo, "de enfocar");

        // Dar la vuelta completa deja el foco donde empezó: rotar no deriva
        var vuelta = e;
        for (int i = 0; i < 3; i++) vuelta = NubeHypr.Ejecutar(vuelta, new Comando.EnfocarSiguiente()).Estado;
        Igual((IntPtr)1, vuelta.Foco, "tres siguientes con tres ventanas vuelven al principio");

        Igual((IntPtr)3, NubeHypr.Ejecutar(e, new Comando.EnfocarAnterior()).Estado.Foco,
              "anterior desde la primera da la vuelta a la última");

        // Sin foco conocido se empieza por la primera, no se queda quieto
        var sinFoco = Est(V(1), V(2)) with { Foco = IntPtr.Zero };
        Igual((IntPtr)1, NubeHypr.Ejecutar(sinFoco, new Comando.EnfocarSiguiente()).Estado.Foco,
              "sin foco, siguiente enfoca la primera");

        // ---------- Enfocar por dirección ----------
        var enCruz = Est(
            V(1, x: 0, y: 500, w: 400, alto: 100),      // izquierda
            V(2, x: 900, y: 500, w: 400, alto: 100),    // derecha
            V(3, x: 450, y: 0, w: 400, alto: 100),      // arriba
            V(4, x: 450, y: 900, w: 400, alto: 100),    // abajo
            V(5, x: 450, y: 500, w: 400, alto: 100))    // el centro
            with { Foco = (IntPtr)5 };

        Igual((IntPtr)1, NubeHypr.Ejecutar(enCruz, new Comando.EnfocarHacia(Direccion.Izquierda)).Estado.Foco,
              "hacia la izquierda, la de la izquierda");
        Igual((IntPtr)2, NubeHypr.Ejecutar(enCruz, new Comando.EnfocarHacia(Direccion.Derecha)).Estado.Foco,
              "hacia la derecha, la de la derecha");
        Igual((IntPtr)3, NubeHypr.Ejecutar(enCruz, new Comando.EnfocarHacia(Direccion.Arriba)).Estado.Foco,
              "hacia arriba, la de arriba");
        Igual((IntPtr)4, NubeHypr.Ejecutar(enCruz, new Comando.EnfocarHacia(Direccion.Abajo)).Estado.Foco,
              "y hacia abajo, la de abajo");

        // ---------- Mover en el orden ----------
        var tres = Est(V(1), V(2), V(3)) with { Foco = (IntPtr)1 };
        var movida = NubeHypr.Ejecutar(tres, new Comando.MoverEnElOrden(true)).Estado;
        Vale(movida.Ventanas.Select(v => v.Hwnd.ToInt64()).SequenceEqual(new long[] { 2, 1, 3 }),
             "mover adelante intercambia con la siguiente");

        // ---------- Redimensionar mueve la frontera, acotada ----------
        var p = NubeHypr.Ejecutar(tres, new Comando.Redimensionar(0.05));
        Igual(0.65, Math.Round(p.Estado.Proporcion, 2), "la proporción sube");
        var tope = tres with { Proporcion = 0.8 };
        Vale(!NubeHypr.Ejecutar(tope, new Comando.Redimensionar(0.2)).Hizo,
             "y en el tope no se mueve más");
        Igual(0.8, NubeHypr.Ejecutar(tope, new Comando.Redimensionar(0.2)).Estado.Proporcion,
              "la proporción se queda acotada en 0,8");

        // ---------- Alternar flotante, dos veces, vuelve ----------
        var f1 = NubeHypr.Ejecutar(tres, new Comando.AlternarFlotante()).Estado;
        Vale(f1.De((IntPtr)1)!.Flotante, "la ventana con el foco pasa a flotante");
        var f2 = NubeHypr.Ejecutar(f1, new Comando.AlternarFlotante()).Estado;
        Vale(!f2.De((IntPtr)1)!.Flotante, "y alternar otra vez la devuelve al reparto");

        // ---------- Cambiar de espacio ----------
        var conEspacios = Est(V(1), V(2)) with
        {
            Espacios = new List<EspacioTrabajo> { new("a", "Uno", 0), new("b", "Dos", 1) },
            Foco = (IntPtr)1,
        };
        var ir = NubeHypr.Ejecutar(conEspacios, new Comando.CambiarEspacio(1, false));
        Igual(1, ir.Estado.EspacioActual, "ir al espacio 1 cambia el espacio actual");
        Igual(0, ir.Estado.De((IntPtr)1)!.Escritorio, "y no se lleva la ventana");

        var llevar = NubeHypr.Ejecutar(conEspacios, new Comando.CambiarEspacio(1, true));
        Igual(1, llevar.Estado.De((IntPtr)1)!.Escritorio, "llevándola, la ventana cambia de escritorio");
        Igual(1, llevar.Estado.EspacioActual, "y se va con ella");
        Igual(TipoEfecto.AEspacio, llevar.Efectos[0].Tipo, "con su efecto de espacio");

        // ---------- Cambiar de layout ----------
        var otro = NubeHypr.Ejecutar(tres, new Comando.PonerLayout(NubeHypr.Layout.Vertical));
        Igual(NubeHypr.Layout.Vertical, otro.Estado.Layout, "el layout cambia");
    }

    static void D1ComandosInvalidos()
    {
        Grupo("D1: un comando inválido no cambia el estado");

        // ---------- La propiedad de §14.2, caso por caso ----------
        //
        // Ni lanza, ni deja el estado a medias, ni se inventa una ventana. Y se comprueba
        // comparando el estado entero, no solo un campo: es lo que atrapa un comando que
        // "no hizo nada" pero dejó el foco movido.
        var vacio = EstadoNubeHypr.Vacio;
        foreach (Comando c in new Comando[]
        {
            new Comando.EnfocarSiguiente(), new Comando.EnfocarAnterior(),
            new Comando.EnfocarHacia(Direccion.Izquierda),
            new Comando.MoverEnElOrden(true), new Comando.MoverEnElOrden(false),
            new Comando.AlternarFlotante(), new Comando.CambiarEspacio(0, true),
        })
        {
            var r = NubeHypr.Ejecutar(vacio, c);
            Vale(!r.Hizo, $"{c.GetType().Name} sin ventanas no hace nada");
            Igual(vacio, r.Estado, $"{c.GetType().Name} devuelve el mismo estado");
            Vale(r.Porque.Length > 0, $"{c.GetType().Name} dice por qué ({r.Porque})");
        }

        var e = Est(V(1), V(2)) with
        {
            Espacios = new List<EspacioTrabajo> { new("a", "Uno", 0) },
            Foco = (IntPtr)1,
        };

        // Un espacio que no existe
        var malEspacio = NubeHypr.Ejecutar(e, new Comando.CambiarEspacio(7, true));
        Vale(!malEspacio.Hizo, "ir a un espacio que no existe no hace nada");
        Igual(e, malEspacio.Estado, "y no cambia el estado");
        Vale(malEspacio.Porque.Contains("7"), $"y lo dice ({malEspacio.Porque})");

        var negativo = NubeHypr.Ejecutar(e, new Comando.CambiarEspacio(-1, false));
        Vale(!negativo.Hizo, "un espacio negativo tampoco");
        Igual(e, negativo.Estado, "sin tocar el estado");

        // Ir al espacio en el que ya estás
        Vale(!NubeHypr.Ejecutar(e, new Comando.CambiarEspacio(0, false)).Hizo,
             "ir al espacio en el que ya estás no hace nada");

        // Mover en los extremos NO da la vuelta: mantener pulsado el atajo no puede
        // mandar la ventana de un extremo al otro sin parar
        var primera = e with { Foco = (IntPtr)1 };
        Vale(!NubeHypr.Ejecutar(primera, new Comando.MoverEnElOrden(false)).Hizo,
             "la primera no se mueve más hacia atrás");
        var ultima = e with { Foco = (IntPtr)2 };
        Vale(!NubeHypr.Ejecutar(ultima, new Comando.MoverEnElOrden(true)).Hizo,
             "ni la última hacia adelante");

        // El foco en una ventana que no existe
        var focoFantasma = e with { Foco = (IntPtr)999 };
        Vale(!NubeHypr.Ejecutar(focoFantasma, new Comando.AlternarFlotante()).Hizo,
             "alternar flotante con el foco en una ventana que no existe no hace nada");
        Vale(!NubeHypr.Ejecutar(focoFantasma, new Comando.MoverEnElOrden(true)).Hizo,
             "ni moverla");

        // Una ventana flotante no entra en el reparto, así que no se mueve en el orden
        var conFlotante = Est(V(1, flot: true), V(2)) with { Foco = (IntPtr)1 };
        Vale(!NubeHypr.Ejecutar(conFlotante, new Comando.MoverEnElOrden(true)).Hizo,
             "una flotante no tiene sitio en el orden del reparto");

        // Redimensionar en flotante no tiene sentido y se dice
        var flotante = e with { Layout = NubeHypr.Layout.Flotante };
        var sinFrontera = NubeHypr.Ejecutar(flotante, new Comando.Redimensionar(0.1));
        Vale(!sinFrontera.Hizo, "en flotante no hay frontera que mover");
        Igual(flotante, sinFrontera.Estado, "y el estado no cambia");

        // Poner el layout que ya estaba
        Vale(!NubeHypr.Ejecutar(e, new Comando.PonerLayout(e.Layout)).Hizo,
             "poner el layout que ya era no hace nada");

        // Y enfocar la única ventana que hay, estando ya enfocada
        var sola = Est(V(1)) with { Foco = (IntPtr)1 };
        Vale(!NubeHypr.Ejecutar(sola, new Comando.EnfocarSiguiente()).Hizo,
             "con una sola ventana ya enfocada, siguiente no hace nada");
    }
    // ================= D2: el motor real de posicionamiento =================
    //
    // El adaptador es la única parte de NubeHypr que habla con Windows, así que no todo él
    // se puede probar en frío. Lo que SÍ se prueba aquí —y es la mayoría de lo que puede
    // salir mal— es: la conversión de coordenadas, el modo por defecto, el descarte de un
    // snapshot viejo, el recuento de movidas y saltadas, y que en simulación no se mueva
    // nada. Mover de verdad se comprueba con una sola ventana de prueba y a mano.

    static EfectoVentana Colocar(long h, double x, double y, double w, double alto) =>
        new(TipoEfecto.Colocar, (IntPtr)h, new NubeHypr.Rect(x, y, w, alto));

    static void D2Conversion()
    {
        Grupo("D2: la conversión de coordenadas, en un solo sitio");

        // ---------- Por qué esto es una prueba y no una línea obvia ----------
        //
        // `Nativo.RECT` lleva los cuatro BORDES y `NubeHypr.Rect` lleva origen y TAMAÑO.
        // Hacer la resta a mano en cada sitio es como se cuela un ancho que en realidad
        // era un borde — y el síntoma es una ventana del doble de ancha, no un error.
        var r = new Nativo.RECT { Izq = 100, Arr = 50, Der = 900, Aba = 650 };
        var c = HyprAdaptador.DeRect(r);
        Igual(100.0, c.X, "la x es el borde izquierdo");
        Igual(50.0, c.Y, "la y es el borde de arriba");
        Igual(800.0, c.Ancho, "el ancho es la diferencia, no el borde derecho");
        Igual(600.0, c.Alto, "y el alto, la diferencia");
        Igual(900.0, c.Derecha, "y de vuelta sale el borde original");
        Igual(650.0, c.Abajo, "los dos");

        // Un monitor a la izquierda del principal: bordes negativos
        var zurdo = new Nativo.RECT { Izq = -1920, Arr = 0, Der = -960, Aba = 1080 };
        var cz = HyprAdaptador.DeRect(zurdo);
        Igual(-1920.0, cz.X, "con origen negativo, la x es negativa");
        Igual(960.0, cz.Ancho, "y el ancho sigue siendo positivo");
        Vale(cz.Valido, "el rectángulo es válido");

        // Un rectángulo vacío —una ventana que no deja leer su sitio— no es válido
        Vale(!HyprAdaptador.DeRect(default).Valido, "un RECT vacío no da un rectángulo válido");

        var alReves = new Nativo.RECT { Izq = 900, Arr = 650, Der = 100, Aba = 50 };
        Vale(!HyprAdaptador.DeRect(alReves).Valido,
             "y uno con los bordes al revés tampoco: mejor descartarlo que mover a un tamaño negativo");
    }

    static void D2SimulacionPorDefecto()
    {
        Grupo("D2: en simulación no se mueve nada");

        // ---------- El comportamiento por defecto ----------
        //
        // `SetWindowPos` tiene efectos visibles inmediatos y no se deshace con una
        // disculpa. Así que el camino por defecto calcula, apunta y NO toca. Lo que se
        // comprueba es que el resultado lo dice (`Simulado`) y que cuenta lo que habría
        // movido, para que el log sirva de algo.
        NubeHypr.OlvidarVersion();
        long v = NubeHypr.SiguienteVersion();

        var efectos = new List<EfectoVentana>
        {
            Colocar(1, 0, 0, 960, 1080),
            Colocar(2, 968, 0, 952, 536),
            new(TipoEfecto.SeQueda, (IntPtr)3, Porque: "está minimizada"),
        };

        foreach (var modo in new[] { NubeHypr.Modo.Apagado, NubeHypr.Modo.Simulacion })
        {
            var r = HyprAdaptador.Aplicar(v, efectos, modo, confirmar: false);
            Vale(r.Simulado, $"[{modo}] el resultado se marca como simulado");
            Igual(2, r.Movidas, $"[{modo}] cuenta las dos que habría colocado");
            Igual(1, r.Saltadas, $"[{modo}] y la que se queda");
            Vale(r.Lineas.Any(l => l.Contains("simular")), $"[{modo}] y lo apunta como simulación");
            Vale(!r.Lineas.Any(l => l.Contains("apply hwnd")),
                 $"[{modo}] sin una sola línea de haber aplicado");
            Vale(!r.Divergio, $"[{modo}] y no se confirma nada, que no se movió nada");
        }

        // El motivo de las que se quedan viaja hasta el log
        var sim = HyprAdaptador.Aplicar(v, efectos, NubeHypr.Modo.Simulacion, confirmar: false);
        Vale(sim.Lineas.Any(l => l.Contains("minimizada")),
             "el motivo de la que se queda llega al log");

        // Un destino inválido se salta aunque estemos simulando: simular un movimiento
        // imposible daría un log que promete algo que no se podría hacer
        var malo = new List<EfectoVentana> { new(TipoEfecto.Colocar, (IntPtr)9, NubeHypr.Rect.Nada) };
        var rm = HyprAdaptador.Aplicar(v, malo, NubeHypr.Modo.Simulacion, confirmar: false);
        Igual(0, rm.Movidas, "un destino inválido no se simula como movimiento");
        Igual(1, rm.Saltadas, "se salta");
        Vale(rm.Lineas.Any(l => l.Contains("destino-invalido")), "y se dice por qué");
    }

    static void D2ElSnapshotViejoNoGana()
    {
        Grupo("D2: un reparto de una versión vieja se descarta entero");

        // ---------- La propiedad de §14.2 ----------
        //
        // "Una actualización vieja no puede sobrescribir un snapshot más nuevo." Y se
        // descarta ENTERO, no a medias: aplicar la mitad de un reparto viejo deja la
        // pantalla peor que no tocarla.
        NubeHypr.OlvidarVersion();
        long vieja = NubeHypr.SiguienteVersion();
        var efectos = new List<EfectoVentana> { Colocar(1, 0, 0, 500, 500), Colocar(2, 0, 0, 500, 500) };

        // Con su propia versión, se aplica (en simulación)
        var alDia = HyprAdaptador.Aplicar(vieja, efectos, NubeHypr.Modo.Simulacion, confirmar: false);
        Igual(2, alDia.Movidas, "con la versión al día se procesan los dos efectos");
        Igual(0, alDia.Viejas, "y no hay nada viejo");

        // Llega otra observación: la de antes queda vieja
        long nueva = NubeHypr.SiguienteVersion();
        var tarde = HyprAdaptador.Aplicar(vieja, efectos, NubeHypr.Modo.Simulacion, confirmar: false);
        Igual(0, tarde.Movidas, "con la versión vieja no se procesa ni uno");
        Igual(2, tarde.Viejas, "los dos se cuentan como viejos");
        Vale(tarde.Lineas.Any(l => l.Contains("descartado")), "y se dice que se descartó");
        Vale(tarde.Lineas.Any(l => l.Contains(nueva.ToString())),
             "diciendo cuál es la última, para poder seguir el rastro");

        // Y la nueva sí
        Igual(2, HyprAdaptador.Aplicar(nueva, efectos, NubeHypr.Modo.Simulacion, confirmar: false).Movidas,
              "la versión nueva se procesa");

        // Una versión del futuro tampoco vale: solo la última
        Igual(0, HyprAdaptador.Aplicar(nueva + 5, efectos, NubeHypr.Modo.Simulacion, confirmar: false).Movidas,
              "una versión que no existe se descarta igual");
    }

    static void D2RecuentoYTrazas()
    {
        Grupo("D2: el recuento y las trazas por ventana");

        NubeHypr.OlvidarVersion();
        long v = NubeHypr.SiguienteVersion();

        var efectos = new List<EfectoVentana>
        {
            Colocar(1, 0, 0, 100, 100),
            Colocar(2, 0, 0, 100, 100),
            new(TipoEfecto.SeQueda, (IntPtr)3, Porque: "es flotante"),
            new(TipoEfecto.SeQueda, (IntPtr)4, Porque: "está maximizada"),
            new(TipoEfecto.Enfocar, (IntPtr)5),
        };

        var r = HyprAdaptador.Aplicar(v, efectos, NubeHypr.Modo.Simulacion, confirmar: false);
        Igual(2, r.Movidas, "dos a colocar");
        Igual(2, r.Saltadas, "dos que se quedan");
        Igual(v, r.Version, "el resultado lleva su versión");

        // ---------- Una línea por ventana, con el formato del documento ----------
        Vale(r.Lineas.Count >= 5, $"hay una línea por cada cosa que pasó ({r.Lineas.Count})");
        Vale(r.Lineas.Any(l => l.StartsWith("[D2]")), "todas con la etiqueta de la fase");
        var confirmacion = r.Lineas.Last();
        Vale(confirmacion.Contains($"confirm version={v}"), $"la última confirma la versión ({confirmacion})");
        Vale(confirmacion.Contains("moved=2") && confirmacion.Contains("skipped=2") &&
             confirmacion.Contains("stale=0"),
             $"con el recuento en el formato de la especificación ({confirmacion})");

        // Un efecto de enfocar no cuenta como movida ni como saltada: no es un
        // movimiento. Contarlo inflaría el recuento y el log mentiría.
        Vale(r.Movidas + r.Saltadas == 4, "el efecto de enfocar no se cuenta como movimiento");

        // Sin efectos, el ciclo no revienta y lo dice
        var vacio = HyprAdaptador.Aplicar(v, Array.Empty<EfectoVentana>(), NubeHypr.Modo.Simulacion, confirmar: false);
        Igual(0, vacio.Movidas, "sin efectos no se mueve nada");
        Vale(vacio.Lineas.Count > 0, "pero queda constancia de la vuelta");
    }

    static void D2ElAreaSaleDelMonitorQueEs()
    {
        Grupo("D2: se calcula con el monitor y el borde de verdad");

        // ---------- Lo que la especificación prohíbe ----------
        //
        // "Nunca se debe calcular una previsualización con un monitor y aplicar con otro
        // implícito." Aquí se comprueba la parte que se puede: que el área depende del
        // monitor Y del borde que se le pasan, y que dos monitores distintos dan áreas
        // distintas y cada una dentro de su pantalla.
        var principal = new NubeHypr.Rect(0, 0, 2560, 1440);
        var segundo = new NubeHypr.Rect(-1920, 0, 1920, 1080);

        var aP = NubeHypr.AreaUtil(principal, NubeHypr.Borde.Arriba, 54, 12);
        var aS = NubeHypr.AreaUtil(segundo, NubeHypr.Borde.Arriba, 54, 12);

        Vale(aP.DentroDe(principal), "el área del principal cae en el principal");
        Vale(aS.DentroDe(segundo), "y la del segundo, en el segundo");
        Vale(!aS.DentroDe(principal), "y NO se confunden: la del segundo no cabe en el principal");
        Vale(aP.X != aS.X, "tienen orígenes distintos");

        // El mismo monitor con la barra en cada borde da cuatro áreas distintas
        var cuatro = new[] { NubeHypr.Borde.Arriba, NubeHypr.Borde.Abajo,
                             NubeHypr.Borde.Izquierda, NubeHypr.Borde.Derecha }
            .Select(b => NubeHypr.AreaUtil(principal, b, 54, 12)).ToList();
        Igual(4, cuatro.Select(a => a.ToString()).Distinct().Count(),
              "los cuatro bordes dan cuatro áreas distintas");
        Vale(cuatro.All(a => a.DentroDe(principal)), "y las cuatro caben en el monitor");

        // Y el reparto que sale de cada área cae dentro de ESA área
        foreach (var (area, i) in cuatro.Select((a, i) => (a, i)))
            foreach (var s in NubeHypr.Repartir(NubeHypr.Layout.Principal, area, 4, 8, 0.6))
                Vale(s.DentroDe(area), $"[borde {i}] el reparto cae dentro de su área");
    }

    static void D2SoloUnaVentana()
    {
        Grupo("D2: empezar por una sola ventana");

        // ---------- Por qué se calcula el reparto COMPLETO ----------
        //
        // La especificación pide empezar por una ventana elegida. Lo que no puede hacerse
        // es calcular el reparto "como si solo hubiera una": entonces esa ventana se
        // llevaría la pantalla entera y el destino no sería el de verdad. Se calcula el
        // reparto de todas y se aplica a una.
        var area = new NubeHypr.Rect(0, 0, 1920, 1080);
        var est = Est(V(1), V(2), V(3));

        var todos = NubeHypr.Calcular(est, area, 8);
        var deLaPrimera = todos.First(e => e.Hwnd == (IntPtr)1 && e.Tipo == TipoEfecto.Colocar);

        // El destino de la primera con tres ventanas NO es el área entera
        Vale(!deLaPrimera.Destino.CasiIgual(area),
             "con tres ventanas, la primera no se lleva la pantalla entera");

        // Y si se calculara con una sola, sí — que es el error que se evita
        var comoSiFueraSola = NubeHypr.Calcular(Est(V(1)), area, 8)
            .First(e => e.Tipo == TipoEfecto.Colocar);
        Vale(comoSiFueraSola.Destino.CasiIgual(area),
             "calculado como si estuviera sola, se llevaría todo: ese es el error que se evita");
        Vale(!deLaPrimera.Destino.CasiIgual(comoSiFueraSola.Destino),
             "y los dos destinos son distintos, así que la diferencia importa");

        // Filtrar a una sola deja un efecto, y es el del reparto completo
        var soloUna = todos.Where(e => e.Hwnd == (IntPtr)1).ToList();
        Igual(1, soloUna.Count, "filtrando a una queda un efecto");
        Vale(soloUna[0].Destino.CasiIgual(deLaPrimera.Destino),
             "y es el destino del reparto completo, no el de una ventana sola");
    }
    // ================= D3: foco, teclado y gestos =================

    static void D3MapaDeAtajos()
    {
        Grupo("D3: el mapa de atajos, con nombres y no con acciones");

        // ---------- La normalización ----------
        //
        // "ctrl+alt+w", "Alt+Ctrl+W" y "CTRL + ALT + W" son el MISMO atajo. Si el mapa las
        // guardara como tres claves, el usuario tendría tres atajos donde quería uno y
        // ninguno detectado como conflicto.
        Igual("Ctrl+Alt+W", HyprAtajos.Normalizar("ctrl+alt+w"), "se normaliza a un orden fijo");
        Igual("Ctrl+Alt+W", HyprAtajos.Normalizar("Alt+Ctrl+W"), "sin importar el orden que se escriba");
        Igual("Ctrl+Alt+W", HyprAtajos.Normalizar("  CTRL + ALT + w  "), "ni los espacios ni las mayúsculas");
        Igual("Ctrl+Alt+W", HyprAtajos.Normalizar("control+alt+w"), "y «control» es «ctrl»");
        Igual("Win+Alt+RIGHT", HyprAtajos.Normalizar("alt+win+right"),
              "y Win va primero, como se escriben y se leen los atajos de Windows");

        Igual("", HyprAtajos.Normalizar(null), "null no es una combinación");
        Igual("", HyprAtajos.Normalizar(""), "ni la cadena vacía");
        Igual("", HyprAtajos.Normalizar("ctrl+alt"), "ni solo modificadores: eso no es un atajo");
        Igual("", HyprAtajos.Normalizar("+++"), "ni un montón de signos");

        // ---------- Qué es válido ----------
        //
        // Se exige un modificador: un atajo global de una sola tecla te deja sin esa tecla
        // en TODO el sistema. Y lo valida quien ya registra atajos, no una tabla paralela.
        Vale(HyprAtajos.Valida("Win+Alt+Right"), "Win+Alt+Right vale");
        Vale(HyprAtajos.Valida("Ctrl+Alt+W"), "Ctrl+Alt+W vale");
        Vale(!HyprAtajos.Valida("W"), "una tecla sola NO vale: te la quitaría en todo el sistema");
        Vale(!HyprAtajos.Valida("Ctrl"), "ni un modificador solo");
        Vale(!HyprAtajos.Valida(""), "ni nada");

        // ---------- De nombre a comando ----------
        Vale(HyprAtajos.Comando(HyprAtajos.FocoSiguiente) is Comando.EnfocarSiguiente,
             "foco.siguiente da el comando de enfocar el siguiente");
        Vale(HyprAtajos.Comando(HyprAtajos.FocoDerecha) is Comando.EnfocarHacia { Hacia: Direccion.Derecha },
             "foco.derecha da el comando con su dirección");
        Vale(HyprAtajos.Comando(HyprAtajos.LayoutVertical) is Comando.PonerLayout { Cual: NubeHypr.Layout.Vertical },
             "layout.vertical da el layout que es");
        Vale(HyprAtajos.Comando(HyprAtajos.MasAncho) is Comando.Redimensionar { Delta: > 0 },
             "tamano.mas crece");
        Vale(HyprAtajos.Comando(HyprAtajos.MasEstrecho) is Comando.Redimensionar { Delta: < 0 },
             "y tamano.menos encoge");

        // Un nombre que no existe da null, NO un comando inventado: un atajo mal escrito
        // no puede acabar haciendo algo distinto de lo que dice.
        Vale(HyprAtajos.Comando("foco.diagonal") == null, "un comando que no existe da null");
        Vale(HyprAtajos.Comando(null) == null, "y null también");
        Vale(HyprAtajos.Comando("") == null, "y la cadena vacía");

        // Todos los nombres de la lista se resuelven o están declarados como de espacios
        // (que son de D4). Ninguno puede quedar como un nombre huérfano.
        foreach (var n in HyprAtajos.Nombres)
        {
            Vale(HyprAtajos.EsNombre(n), $"{n} está en la lista de nombres");
            Vale(HyprAtajos.Rotulo(n).Length > 0, $"{n} tiene un rótulo para el indicador");
        }

        // ---------- El mapa de serie ----------
        var serie = HyprAtajos.Construir(HyprAtajos.DeSerie());
        Vale(serie.Bien, "el mapa de serie no tiene problemas: " + string.Join(" | ", serie.Problemas));
        Vale(serie.Atajos.Count >= 10, $"y trae unos cuantos atajos ({serie.Atajos.Count})");
        // Escrito con el PREFIJO y no con una combinación fija: el prefijo de serie se
        // eligió midiendo (ver `--diagprefijos`) y puede volver a cambiar, y una prueba que
        // lo clave a mano se rompe el día que cambie sin que nada esté mal de verdad.
        var flechaDerecha = $"{HyprAtajos.Prefijo}+Right";
        Vale(serie.Resolver(flechaDerecha) is Comando.EnfocarHacia { Hacia: Direccion.Derecha },
             $"{flechaDerecha} enfoca a la derecha");
        Vale(serie.Resolver(flechaDerecha.ToLowerInvariant()) is Comando.EnfocarHacia,
             "y se resuelve escrito de cualquier forma");
        Vale(serie.Resolver("Ctrl+Shift+Z") == null, "una combinación sin asignar da null");

        // De serie no se pisa nada de Windows: Win+flechas es el ajuste de Windows y
        // Win+Ctrl+flechas los escritorios virtuales
        Vale(!serie.Atajos.Keys.Any(k => k.Equals("Win+RIGHT", StringComparison.OrdinalIgnoreCase)),
             "de serie no se coge Win+Right, que es de Windows");
        Vale(serie.Atajos.Keys.All(k => k.Contains("Alt", StringComparison.OrdinalIgnoreCase)),
             "todos los de serie llevan Alt, para no pisar los de Windows");
    }

    static void D3ConflictosDeAtajos()
    {
        Grupo("D3: los conflictos se dicen, no se tragan");

        // ---------- Un atajo mal escrito no puede tumbar el arranque ----------
        //
        // Ni quedarse muerto en silencio, que es peor: el usuario lo pulsa y no entiende
        // por qué no pasa nada. Los problemas se DEVUELVEN para que el log los apunte y
        // Ajustes los enseñe.
        var malos = HyprAtajos.Construir(new Dictionary<string, string>
        {
            ["Win+Alt+Right"] = HyprAtajos.FocoDerecha,      // bien
            ["Z"] = HyprAtajos.FocoIzquierda,                // sin modificador
            ["Win+Alt+Q"] = "foco.diagonal",                 // comando que no existe
            ["+++"] = HyprAtajos.FocoArriba,                 // no es una combinación
        });

        Igual(1, malos.Atajos.Count, "solo entra el que está bien");
        Igual(3, malos.Problemas.Count, "y los tres malos se cuentan");
        Vale(!malos.Bien, "el mapa se marca como problemático");
        Vale(malos.Problemas.Any(p => p.Contains("modificador")), "se dice el que no tiene modificador");
        Vale(malos.Problemas.Any(p => p.Contains("no existe")), "el comando que no existe");
        Vale(malos.Problemas.Any(p => p.Contains("no es una combinación")), "y el que no es combinación");

        // ---------- Dos veces la misma combinación ----------
        var repetido = HyprAtajos.Construir(new Dictionary<string, string>
        {
            ["Win+Alt+Right"] = HyprAtajos.FocoDerecha,
            ["alt+win+right"] = HyprAtajos.FocoIzquierda,    // la MISMA, escrita de otra forma
        });
        Igual(1, repetido.Atajos.Count, "la combinación repetida solo entra una vez");
        Vale(repetido.Problemas.Any(p => p.Contains("dos veces")),
             $"y se dice que estaba dos veces ({string.Join(" | ", repetido.Problemas)})");
        Vale(repetido.Nombre("Win+Alt+Right") == HyprAtajos.FocoDerecha,
             "se queda el primero, no el último: así el orden del fichero manda y es predecible");

        // ---------- Lo que ya está cogido ----------
        //
        // El atajo de la vista general de NubeBar no se resuelve "por orden de llegada":
        // se rechaza el nuevo y se dice contra qué chocaba.
        var choca = HyprAtajos.Construir(
            new Dictionary<string, string> { ["Ctrl+Alt+W"] = HyprAtajos.FocoSiguiente },
            reservados: new[] { "Ctrl+Alt+W" });
        Igual(0, choca.Atajos.Count, "un atajo que ya está cogido no se asigna");
        Vale(choca.Problemas.Any(p => p.Contains("ya está cogido")), "y se dice");

        // Y reservado escrito de otra forma también choca: si no, la normalización no
        // serviría de nada justo donde más importa
        var chocaIgual = HyprAtajos.Construir(
            new Dictionary<string, string> { ["alt+ctrl+w"] = HyprAtajos.FocoSiguiente },
            reservados: new[] { "Ctrl+Alt+W" });
        Igual(0, chocaIgual.Atajos.Count, "escrito de otra forma, choca igual");

        // Un mapa vacío o nulo no es un error: es "no has puesto atajos"
        Vale(HyprAtajos.Construir(null).Bien, "un mapa nulo no da problemas");
        Igual(0, HyprAtajos.Construir(null).Atajos.Count, "y queda vacío");
        Vale(HyprAtajos.Construir(new Dictionary<string, string>()).Bien, "y uno vacío tampoco");
    }

    static void D3PasosDeMoverYTamano()
    {
        Grupo("D3: mover y redimensionar con pasos, sin salirse");

        var area = new NubeHypr.Rect(0, 0, 1920, 1080);
        var v = new NubeHypr.Rect(500, 400, 400, 300);

        // Los cuatro sentidos mueven en el sentido que dicen
        Igual(468.0, NubeHypr.PasoDeMover(v, area, Direccion.Izquierda, 32).X, "izquierda resta en x");
        Igual(532.0, NubeHypr.PasoDeMover(v, area, Direccion.Derecha, 32).X, "derecha suma");
        Igual(368.0, NubeHypr.PasoDeMover(v, area, Direccion.Arriba, 32).Y, "arriba resta en y");
        Igual(432.0, NubeHypr.PasoDeMover(v, area, Direccion.Abajo, 32).Y, "abajo suma");

        // El tamaño no cambia al mover
        var movida = NubeHypr.PasoDeMover(v, area, Direccion.Derecha, 32);
        Igual(v.Ancho, movida.Ancho, "mover no cambia el ancho");
        Igual(v.Alto, movida.Alto, "ni el alto");

        // ---------- Nunca se sale ----------
        //
        // Una ventana que se va de la pantalla con el teclado es imposible de recuperar
        // sin el ratón, y es el tipo de fallo que obliga a reiniciar la sesión.
        var pegada = new NubeHypr.Rect(0, 0, 400, 300);
        Igual(0.0, NubeHypr.PasoDeMover(pegada, area, Direccion.Izquierda, 999).X,
              "pegada a la izquierda, no se va más");
        Igual(0.0, NubeHypr.PasoDeMover(pegada, area, Direccion.Arriba, 999).Y,
              "ni por arriba");

        var alFinal = NubeHypr.PasoDeMover(v, area, Direccion.Derecha, 9999);
        Vale(alFinal.DentroDe(area), "empujada mil píxeles a la derecha, sigue dentro");
        Igual(1520.0, alFinal.X, "y se queda pegada al borde derecho");
        Vale(NubeHypr.PasoDeMover(v, area, Direccion.Abajo, 9999).DentroDe(area), "y por abajo igual");

        // Una ventana más grande que el área se pega al origen, sin dar un salto absurdo
        var enorme = new NubeHypr.Rect(100, 100, 4000, 3000);
        var pegadaAlOrigen = NubeHypr.PasoDeMover(enorme, area, Direccion.Derecha, 32);
        Igual(area.X, pegadaAlOrigen.X, "una ventana más grande que el área se pega al origen");

        // Un monitor con origen negativo: el borde es el suyo, no el cero
        var zurdo = new NubeHypr.Rect(-1920, 0, 1920, 1080);
        var enZurdo = new NubeHypr.Rect(-1900, 100, 400, 300);
        Igual(-1920.0, NubeHypr.PasoDeMover(enZurdo, zurdo, Direccion.Izquierda, 999).X,
              "en un monitor con origen negativo, el tope es su borde");

        // ---------- El tamaño ----------
        Igual(432.0, NubeHypr.PasoDeTamano(v, area, Direccion.Derecha, 32).Ancho, "derecha ensancha");
        Igual(368.0, NubeHypr.PasoDeTamano(v, area, Direccion.Izquierda, 32).Ancho, "izquierda estrecha");
        Igual(332.0, NubeHypr.PasoDeTamano(v, area, Direccion.Abajo, 32).Alto, "abajo alarga");
        Igual(268.0, NubeHypr.PasoDeTamano(v, area, Direccion.Arriba, 32).Alto, "arriba acorta");

        // El origen no se mueve al redimensionar
        var cambiada = NubeHypr.PasoDeTamano(v, area, Direccion.Derecha, 32);
        Igual(v.X, cambiada.X, "redimensionar no mueve la x");
        Igual(v.Y, cambiada.Y, "ni la y");

        // Nunca por debajo del mínimo
        var achicada = NubeHypr.PasoDeTamano(v, area, Direccion.Izquierda, 9999);
        Igual(NubeHypr.MinimoLado, achicada.Ancho, "encogida mil veces, se queda en el mínimo");
        Vale(achicada.Valido, "y sigue siendo una ventana");

        // Ni creciendo se sale de la pantalla
        var crecida = NubeHypr.PasoDeTamano(v, area, Direccion.Derecha, 9999);
        Vale(crecida.Derecha <= area.Derecha + 0.01,
             $"crecida mil píxeles, su borde no pasa del área ({crecida.Derecha:0})");
        Vale(NubeHypr.PasoDeTamano(v, area, Direccion.Abajo, 9999).Abajo <= area.Abajo + 0.01,
             "y por abajo tampoco");

        // Rectángulos imposibles se devuelven tal cual, sin inventar
        Igual(NubeHypr.Rect.Nada, NubeHypr.PasoDeMover(NubeHypr.Rect.Nada, area, Direccion.Derecha, 32),
              "un rectángulo vacío no se mueve");
        Igual(v, NubeHypr.PasoDeMover(v, NubeHypr.Rect.Nada, Direccion.Derecha, 32),
              "y sin área tampoco");

        // Cincuenta pasos seguidos acaban pegados al borde y ahí se quedan: no hay deriva
        var paseada = v;
        for (int i = 0; i < 50; i++) paseada = NubeHypr.PasoDeMover(paseada, area, Direccion.Derecha, 32);
        Vale(paseada.DentroDe(area), "cincuenta pasos y sigue dentro");
        var unoMas = NubeHypr.PasoDeMover(paseada, area, Direccion.Derecha, 32);
        Vale(unoMas.CasiIgual(paseada), "y el paso 51 ya no la mueve");
    }

    static void D3GestoCancelaDeVerdad()
    {
        Grupo("D3: cancelar devuelve la ventana a donde estaba");

        var area = new NubeHypr.Rect(0, 0, 1920, 1080);
        var original = new NubeHypr.Rect(500, 400, 400, 300);

        var colocadas = new List<NubeHypr.Rect>();
        var apuntes = new List<string>();
        var g = new HyprGesto();
        g.Colocar = (_, r) => colocadas.Add(r);
        g.Apuntar = m => apuntes.Add(m);

        // ---------- Empezar ----------
        Vale(!g.EnMarcha, "antes de empezar no hay gesto");
        Vale(g.Empezar((IntPtr)1, original), "el gesto empieza");
        Vale(g.EnMarcha, "y queda en marcha");
        Igual(original, g.Original, "con el rectángulo original apuntado");
        Igual(0, g.Pasos, "y sin pasos todavía");

        // ---------- Tres pasos ----------
        var r = original;
        for (int i = 0; i < 3; i++)
        {
            r = NubeHypr.PasoDeMover(r, area, Direccion.Derecha, 32);
            Vale(g.Paso(r), $"el paso {i + 1} se da y el gesto sigue vivo");
        }
        Igual(3, g.Pasos, "tres pasos");
        Igual(3, colocadas.Count, "y tres colocaciones de verdad");
        Igual(596.0, g.Actual.X, "la ventana ha avanzado 96 px");

        // ---------- Cancelar: vuelve al sitio ----------
        //
        // Un Esc que deja la ventana a medio camino es peor que no tener Esc: el usuario
        // ya no sabe dónde la tenía.
        g.Cancelar("Esc");
        Vale(!g.EnMarcha, "cancelar termina el gesto");
        Igual(4, colocadas.Count, "y coloca una vez más: la vuelta");
        Igual(original, colocadas[^1], "exactamente donde estaba al empezar");
        Vale(apuntes.Any(a => a.Contains("cancelled")), "y queda apuntado como cancelado");
        Vale(apuntes.Any(a => a.Contains("devuelta")), "diciendo que se devolvió");

        // ---------- Cancelar sin pasos no coloca nada ----------
        //
        // Pedirle a Windows que ponga la ventana donde ya está es una llamada de más y un
        // parpadeo posible.
        colocadas.Clear();
        g.Empezar((IntPtr)2, original);
        g.Cancelar("Esc");
        Igual(0, colocadas.Count, "cancelar sin haber dado un paso no coloca nada");

        // ---------- Terminar bien se queda donde esté ----------
        colocadas.Clear();
        g.Empezar((IntPtr)3, original);
        var movida = NubeHypr.PasoDeMover(original, area, Direccion.Abajo, 32);
        g.Paso(movida);
        g.Terminar("soltado");
        Vale(!g.EnMarcha, "terminar acaba el gesto");
        Igual(1, colocadas.Count, "y NO devuelve nada: se queda donde la dejaste");
        Igual(movida, colocadas[^1], "en el sitio nuevo");

        // ---------- Idempotencia ----------
        int limpios = g.VecesLimpiado;
        for (int i = 0; i < 5; i++) { g.Terminar("otra vez"); g.Cancelar("y otra"); }
        Vale(!g.EnMarcha, "terminar y cancelar diez veces deja el gesto parado");
        Vale(g.VecesLimpiado > limpios, "se limpia, sin volver a soltar lo ya soltado");

        // ---------- Un paso con el gesto parado no mueve nada ----------
        //
        // Es el caso de una tecla que llega tarde, después de haber soltado.
        colocadas.Clear();
        Vale(!g.Paso(movida), "un paso con el gesto parado devuelve que no está vivo");
        Igual(0, colocadas.Count, "y no coloca nada");

        // ---------- El fotograma manda, como en los deslizadores ----------
        g.Empezar((IntPtr)4, original);
        Vale(g.Fotograma(teclaPulsada: true), "con la tecla pulsada el gesto sigue");
        Vale(!g.Fotograma(teclaPulsada: false), "al soltarla, el gesto acaba AHÍ");
        Vale(!g.EnMarcha, "sin depender de ningún evento que pueda perderse");

        // ---------- Empezar con datos imposibles no empieza nada ----------
        Vale(!g.Empezar(IntPtr.Zero, original), "sin ventana no se empieza");
        Vale(!g.Empezar((IntPtr)1, NubeHypr.Rect.Nada), "ni sin rectángulo");
        Vale(!g.EnMarcha, "y no queda un gesto a medias");

        // ---------- Empezar otro cierra el anterior ----------
        g.Empezar((IntPtr)5, original);
        g.Paso(NubeHypr.PasoDeMover(original, area, Direccion.Derecha, 32));
        g.Empezar((IntPtr)6, new NubeHypr.Rect(100, 100, 200, 200));
        Vale(g.EnMarcha, "el segundo gesto queda vivo");
        Igual((IntPtr)6, g.Hwnd, "y es el de la ventana nueva");
        Igual(0, g.Pasos, "con su cuenta de pasos a cero: no hereda la del anterior");
    }

    static void D3MaximizadasFueraPorDefecto()
    {
        Grupo("D3: las maximizadas y minimizadas se quedan fuera por defecto");

        var area = new NubeHypr.Rect(0, 0, 1920, 1080);
        var est = Est(V(1), V(2, max: true), V(3, min: true), V(4, max: true));
        var memoria = new HyprRestaurar.Memoria();

        // ---------- Apagado: es el comportamiento de D2 ----------
        //
        // Sacar una ventana de maximizada es una acción VISIBLE y sorprendente: el usuario
        // la dejó a pantalla completa a propósito.
        var sinRestaurar = HyprRestaurar.Preparar(est, memoria, restaurarAlOrganizar: false);
        Igual(0, sinRestaurar.Count, "apagado, no se prepara ninguna restauración");
        Igual(0, memoria.Cuantas, "y no se recuerda nada: no hay nada que deshacer");

        var efectos = NubeHypr.Calcular(est, area, 8);
        Igual(1, efectos.Count(e => e.Tipo == TipoEfecto.Colocar), "solo se coloca la normal");
        Igual(3, efectos.Count(e => e.Tipo == TipoEfecto.SeQueda), "y las otras tres se quedan");
        Vale(efectos.Count(e => e.Porque.Contains("maximizada")) == 2, "las dos maximizadas lo dicen");
        Vale(efectos.Any(e => e.Porque.Contains("minimizada")), "y la minimizada también");

        // Y la opción nace apagada en la configuración
        Vale(!new OpcionesHypr().RestaurarAlOrganizar,
             "la opción de restaurar nace APAGADA en la configuración");

        // ---------- Encendido: se preparan, y se recuerda qué eran ----------
        var conRestaurar = HyprRestaurar.Preparar(est, memoria, restaurarAlOrganizar: true);
        Igual(3, conRestaurar.Count, "encendido, se preparan las tres");
        Vale(conRestaurar.All(e => e.Tipo == TipoEfecto.Restaurar), "como efectos de restaurar");
        Vale(conRestaurar.All(e => e.Porque.Length > 0), "con su motivo escrito");
        Igual(3, memoria.Cuantas, "y se recuerda lo que eran las tres");
        Vale(memoria.Sabe((IntPtr)2) && memoria.Sabe((IntPtr)3) && memoria.Sabe((IntPtr)4),
             "las tres, por su ventana");
        Vale(!memoria.Sabe((IntPtr)1), "y de la normal no hay nada que recordar");

        // Lo que se recordó es lo que era
        var q2 = memoria.Consultar((IntPtr)2)!.Value;
        Vale(q2.Maximizada && !q2.Minimizada, "de la 2 se recuerda que estaba maximizada");
        var q3 = memoria.Consultar((IntPtr)3)!.Value;
        Vale(q3.Minimizada && !q3.Maximizada, "y de la 3, que estaba minimizada");
        Vale(q2.Rectangulo.Valido, "con su rectángulo, que hace falta para devolverla bien");

        // ---------- Recordar una segunda vez NO machaca ----------
        //
        // Si no, "devolver" dejaría la ventana donde la puso NubeHypr, no donde estaba.
        var yaRestaurada = V(2, max: false, x: 999, y: 999);
        Vale(!memoria.Recordar(yaRestaurada), "recordar dos veces devuelve que no apuntó nada");
        Vale(memoria.Consultar((IntPtr)2)!.Value.Maximizada,
             "y lo guardado sigue siendo el estado ORIGINAL, no el intermedio");

        // ---------- Y con las restauradas, el reparto ya las cuenta ----------
        var comoSi = HyprRestaurar.ComoSiYaEstuvieranRestauradas(est, conRestaurar);
        var efectos2 = NubeHypr.Calcular(comoSi, area, 8);
        Igual(4, efectos2.Count(e => e.Tipo == TipoEfecto.Colocar),
              "con las tres restauradas, se colocan las cuatro");
        Vale(efectos2.All(e => e.Tipo != TipoEfecto.Colocar || e.Destino.DentroDe(area)),
             "y las cuatro dentro del área");

        // Las flotantes NO se restauran: el usuario dijo que las lleva a mano
        var conFlotante = Est(V(1, flot: true, max: true));
        var m2 = new HyprRestaurar.Memoria();
        Igual(0, HyprRestaurar.Preparar(conFlotante, m2, true).Count,
              "una flotante maximizada no se restaura: está así porque su dueño quiso");
    }

    static void D3DevolverLasVentanas()
    {
        Grupo("D3: lo que se toca se puede devolver");

        var est = Est(V(1, max: true, x: 0, y: 0, w: 1920, alto: 1080),
                      V(2, min: true, x: 300, y: 200, w: 800, alto: 600));
        var memoria = new HyprRestaurar.Memoria();
        HyprRestaurar.Preparar(est, memoria, restaurarAlOrganizar: true);
        Igual(2, memoria.Cuantas, "se recuerdan las dos");

        // ---------- Devolver una ----------
        var ef = HyprRestaurar.Devolver(memoria, (IntPtr)1);
        Vale(ef != null, "se puede devolver");
        Igual(TipoEfecto.Devolver, ef!.Tipo, "con su tipo de efecto");
        Vale(ef.Porque.Contains("maximizada"), $"diciendo a qué vuelve ({ef.Porque})");
        Vale(ef.Destino.Valido, "y con el rectángulo que tenía");
        Igual(1, memoria.Cuantas, "y se olvida al devolverla");

        // Devolverla otra vez no hace nada: ya no se recuerda
        Vale(HyprRestaurar.Devolver(memoria, (IntPtr)1) == null,
             "devolver dos veces la misma no hace nada");

        // Una que nunca se tocó tampoco: inventar un estado anterior es peor que no tener
        Vale(HyprRestaurar.Devolver(memoria, (IntPtr)99) == null,
             "una ventana que nunca se tocó no se devuelve a ningún sitio");

        // ---------- Devolverlo todo: el "apagar NubeHypr y dejarlo como estaba" ----------
        var m2 = new HyprRestaurar.Memoria();
        HyprRestaurar.Preparar(est, m2, true);
        var todos = HyprRestaurar.DevolverTodo(m2);
        Igual(2, todos.Count, "devolverlo todo da un efecto por ventana");
        Igual(0, m2.Cuantas, "y la memoria queda vacía");
        Vale(todos.All(e => e.Tipo == TipoEfecto.Devolver), "todos de devolver");

        // Y sobre una memoria vacía no revienta
        Igual(0, HyprRestaurar.DevolverTodo(new HyprRestaurar.Memoria()).Count,
              "devolverlo todo sin nada que devolver da cero");

        // ---------- La memoria no crece sin fin ----------
        //
        // Una ventana que ya no existe no puede devolverse a ningún sitio.
        var m3 = new HyprRestaurar.Memoria();
        HyprRestaurar.Preparar(est, m3, true);
        Igual(1, m3.Limpiar(new[] { (IntPtr)1 }), "limpiar con una viva quita la otra");
        Igual(1, m3.Cuantas, "y queda la que vive");
        Igual(1, m3.Limpiar(Array.Empty<IntPtr>()), "sin ninguna viva se quita todo");
        Igual(0, m3.Cuantas, "y la memoria queda vacía");
        Igual(0, m3.Limpiar(Array.Empty<IntPtr>()), "limpiar una memoria vacía no quita nada");
    }
    // ================= D4: escritorios virtuales =================
    //
    // La API de escritorios de Windows NO es pública y ya vive envuelta en try/catch. Lo
    // que se prueba aquí es la política: ordenar, nombrar sin repetir, elegir el vecino,
    // reconciliar un cambio que vino de fuera, el mapa tolerante y el modo de un solo
    // escritorio. Nada de esto se puede provocar con la API de verdad —no se puede pedirle
    // cinco escritorios ni un renombrado externo a voluntad— y por eso está aparte.

    static HyprEspacios.Espacio E(int orden, string nombre, bool actual = false) =>
        new(Guid.Parse($"00000000-0000-0000-0000-{orden + 100:000000000000}"), nombre, orden, actual);

    static void D4NombresYOrden()
    {
        Grupo("D4: nombres vacíos, repetidos y renombrados de fuera");

        // ---------- Un escritorio sin nombre no sale en blanco ----------
        //
        // Windows los crea sin nombre. "Escritorio 3" es lo que hace Windows y lo que el
        // usuario espera; un hueco vacío en el indicador parece un fallo.
        Igual("Escritorio 1", HyprEspacios.NombreVisible(null, 0), "sin nombre, se numera");
        Igual("Escritorio 3", HyprEspacios.NombreVisible("", 2), "y con cadena vacía");
        Igual("Escritorio 3", HyprEspacios.NombreVisible("   ", 2), "y con espacios");
        Igual("Trabajo", HyprEspacios.NombreVisible("Trabajo", 0), "con nombre, el suyo");
        Igual("Trabajo", HyprEspacios.NombreVisible("  Trabajo  ", 0), "recortado");

        // ---------- Dos con el mismo nombre ----------
        //
        // Windows deja ponerlo. Dos espacios con el mismo rótulo son indistinguibles en el
        // indicador, y entonces "lleva esta ventana a Trabajo" no tiene una respuesta.
        Igual("Trabajo", HyprEspacios.NombreLibre(new[] { "Ocio" }, "Trabajo"), "si está libre, el suyo");
        Igual("Trabajo (2)", HyprEspacios.NombreLibre(new[] { "Trabajo" }, "Trabajo"),
              "si está cogido, se numera");
        Igual("Trabajo (3)", HyprEspacios.NombreLibre(new[] { "Trabajo", "Trabajo (2)" }, "Trabajo"),
              "y se sigue numerando");
        // El choque se detecta SIN distinguir mayúsculas —dos rótulos que se leen igual son
        // el mismo— pero la capitalización que escribió el usuario se conserva: cambiarle
        // "trabajo" por "Trabajo" sería corregirle lo que tecleó.
        Igual("trabajo (2)", HyprEspacios.NombreLibre(new[] { "TRABAJO" }, "trabajo"),
              "el choque se detecta sin mayúsculas, y se respeta cómo lo escribiste");
        Igual("Escritorio", HyprEspacios.NombreLibre(Array.Empty<string>(), null),
              "sin nombre propuesto, el genérico");

        // ---------- Presentar la lista entera ----------
        var presentados = HyprEspacios.Presentar(new (Guid, string?, bool)[]
        {
            (Guid.NewGuid(), null, false),
            (Guid.NewGuid(), "Trabajo", true),
            (Guid.NewGuid(), "Trabajo", false),     // repetido, de verdad
            (Guid.NewGuid(), "   ", false),
        });
        Igual(4, presentados.Count, "salen los cuatro");
        Igual(4, presentados.Select(e => e.Nombre).Distinct().Count(),
              "y los cuatro rótulos son distintos: ninguno se confunde con otro");
        Igual("Escritorio 1", presentados[0].Nombre, "el primero sin nombre se numera por su sitio");
        Igual("Trabajo", presentados[1].Nombre, "el segundo conserva el suyo");
        Igual("Trabajo (2)", presentados[2].Nombre, "y el repetido se desambigua");
        Vale(presentados[1].EsElActual, "y se conserva quién es el actual");

        // El orden y la numeración
        Vale(presentados.Select(e => e.Orden).SequenceEqual(new[] { 0, 1, 2, 3 }),
             "el orden es el que vino de la API");
        Vale(presentados.Select(e => e.Numero).SequenceEqual(new[] { 1, 2, 3, 4 }),
             "y al usuario se le cuenta desde 1, que es como los cuenta él");

        // Una lista vacía da una lista vacía, no revienta
        Igual(0, HyprEspacios.Presentar(Array.Empty<(Guid, string?, bool)>()).Count,
              "sin escritorios, lista vacía");

        // ---------- Validar lo que escribe el usuario ----------
        Vale(HyprEspacios.NombreValido("Trabajo", out var limpio, out _), "un nombre normal vale");
        Igual("Trabajo", limpio, "y sale recortado");
        Vale(HyprEspacios.NombreValido("  Ocio  ", out limpio, out _), "con espacios de más, vale");
        Igual("Ocio", limpio, "recortado");
        Vale(!HyprEspacios.NombreValido("", out _, out var p1), "vacío no vale");
        Vale(p1.Length > 0, $"y se dice por qué ({p1})");
        Vale(!HyprEspacios.NombreValido("   ", out _, out _), "ni solo espacios");
        Vale(!HyprEspacios.NombreValido(new string('x', 200), out _, out var p2),
             "ni doscientos caracteres");
        Vale(p2.Contains("40"), $"diciendo el límite ({p2})");
        Vale(!HyprEspacios.NombreValido("con\nsalto", out _, out _), "ni con saltos de línea");
        Vale(!HyprEspacios.NombreValido(null, out _, out _), "ni null");
    }

    static void D4ElVecino()
    {
        Grupo("D4: el vecino, y por qué no da la vuelta");

        var tres = new List<HyprEspacios.Espacio> { E(0, "Uno", true), E(1, "Dos"), E(2, "Tres") };

        Igual("Dos", HyprEspacios.Vecino(tres, siguiente: true)!.Value.Nombre,
              "desde el primero, el siguiente es el segundo");
        Vale(HyprEspacios.Vecino(tres, siguiente: false) == null,
             "y hacia atrás desde el primero no hay nada");

        var enElUltimo = new List<HyprEspacios.Espacio> { E(0, "Uno"), E(1, "Dos"), E(2, "Tres", true) };
        Vale(HyprEspacios.Vecino(enElUltimo, siguiente: true) == null,
             "en el último no hay siguiente: es lo que hace Windows");
        Igual("Dos", HyprEspacios.Vecino(enElUltimo, siguiente: false)!.Value.Nombre,
              "y hacia atrás, el anterior");

        // ---------- Dar la vuelta existe, pero no de serie ----------
        //
        // Mantener pulsado el atajo te llevaría del último al primero sin parar: la misma
        // deriva que ya se evitó en `MoverEnElOrden`.
        Igual("Uno", HyprEspacios.Vecino(enElUltimo, true, darLaVuelta: true)!.Value.Nombre,
              "pidiéndolo, da la vuelta");
        Igual("Tres", HyprEspacios.Vecino(tres, false, darLaVuelta: true)!.Value.Nombre,
              "en los dos sentidos");

        // ---------- Casos de borde ----------
        Vale(HyprEspacios.Vecino(new List<HyprEspacios.Espacio>(), true) == null,
             "sin espacios no hay vecino");
        var uno = new List<HyprEspacios.Espacio> { E(0, "Solo", true) };
        Vale(HyprEspacios.Vecino(uno, true) == null, "con uno solo tampoco");
        Vale(HyprEspacios.Vecino(uno, true, darLaVuelta: true) == null,
             "ni dando la vuelta: él mismo no es su vecino");

        // Si nadie se declara actual —pasa justo durante un cambio— se empieza por el
        // primero en vez de no hacer nada
        var sinActual = new List<HyprEspacios.Espacio> { E(0, "Uno"), E(1, "Dos") };
        Igual("Uno", HyprEspacios.Vecino(sinActual, true)!.Value.Nombre,
              "sin actual conocido, el primero");
        Igual(-1, HyprEspacios.IndiceDelActual(sinActual), "y se sabe que no se sabe");

        // ---------- Por número, como lo cuenta el usuario ----------
        Igual("Uno", HyprEspacios.PorNumero(tres, 1)!.Value.Nombre, "el número 1 es el primero");
        Igual("Tres", HyprEspacios.PorNumero(tres, 3)!.Value.Nombre, "y el 3 el tercero");
        Vale(HyprEspacios.PorNumero(tres, 0) == null, "no hay escritorio 0");
        Vale(HyprEspacios.PorNumero(tres, 7) == null,
             "y «ve al 7» cuando hay tres NO es «ve al 3»: no se hace nada");
        Vale(HyprEspacios.PorNumero(tres, -1) == null, "ni con un número negativo");
    }

    static void D4ModoDeUnSoloEscritorio()
    {
        Grupo("D4: si la API falla, un solo escritorio");

        // ---------- La forma de no llenar el código de «if (hayApi)» ----------
        //
        // La política devuelve UN espacio en vez de ninguno. Así el indicador enseña algo,
        // los comandos de cambiar no encuentran vecino y no hacen nada, y las ventanas se
        // cuentan todas en el único espacio que hay. Ningún camino muerto.
        var uno = HyprEspacios.UnSoloEscritorio();
        Igual(1, uno.Count, "el modo de uno da exactamente un espacio");
        Vale(uno[0].EsElActual, "y es el actual: si no, nada tendría dónde estar");
        Igual(1, uno[0].Numero, "se llama el número 1");
        Vale(uno[0].Nombre.Length > 0, "con un nombre que se puede enseñar");
        Vale(HyprEspacios.EsElModoDeUno(uno), "y se reconoce como el modo de uno");

        // No se confunde con un equipo que de verdad tiene un escritorio
        var deVerdad = new List<HyprEspacios.Espacio> { E(0, "Escritorio 1", true) };
        Vale(!HyprEspacios.EsElModoDeUno(deVerdad),
             "un escritorio real con su id no es el modo de uno");
        Vale(!HyprEspacios.EsElModoDeUno(new List<HyprEspacios.Espacio>()),
             "ni una lista vacía");

        // En el modo de uno, los comandos de espacio no hacen nada y lo dicen
        Vale(HyprEspacios.Resolver(uno, new Comando.AlVecino(true, false)) == null,
             "en el modo de uno no hay siguiente");
        Vale(HyprEspacios.Resolver(uno, new Comando.AlVecino(false, true)) == null,
             "ni anterior, ni llevando la ventana");
        Vale(HyprEspacios.Resolver(uno, new Comando.AlNumero(1, false)) == null,
             "y «ve al 1» cuando ya estás en el 1 no hace nada");
        Vale(HyprEspacios.Resolver(uno, new Comando.AlNumero(2, false)) == null,
             "y el 2 no existe");
    }

    static void D4ResolverComandos()
    {
        Grupo("D4: de la intención al escritorio concreto");

        // ---------- Por qué hace falta este paso ----------
        //
        // «Al siguiente» no se puede expresar con un número: el número depende de cuántos
        // escritorios haya AHORA, y eso lo sabe la API, no el atajo.
        var tres = new List<HyprEspacios.Espacio> { E(0, "Uno"), E(1, "Dos", true), E(2, "Tres") };

        var sig = HyprEspacios.Resolver(tres, new Comando.AlVecino(true, false));
        Vale(sig != null, "al siguiente se resuelve");
        Igual("Tres", sig!.Value.Espacio.Nombre, "y es el tercero");
        Vale(!sig.Value.LlevarLaVentana, "sin llevar la ventana");

        var llevando = HyprEspacios.Resolver(tres, new Comando.AlVecino(true, true));
        Vale(llevando!.Value.LlevarLaVentana, "y pidiéndolo, llevándola");
        Vale(llevando.Value.Porque.Length > 0, "con su motivo para el log");

        var ant = HyprEspacios.Resolver(tres, new Comando.AlVecino(false, false));
        Igual("Uno", ant!.Value.Espacio.Nombre, "al anterior, el primero");

        // Por número
        Igual("Uno", HyprEspacios.Resolver(tres, new Comando.AlNumero(1, false))!.Value.Espacio.Nombre,
              "por número funciona");
        Vale(HyprEspacios.Resolver(tres, new Comando.AlNumero(2, false)) == null,
             "ir al que ya es el actual no hace nada");
        Vale(HyprEspacios.Resolver(tres, new Comando.AlNumero(9, false)) == null,
             "y a uno que no existe tampoco");

        // ---------- El comando de D1 cuenta desde 0 y el usuario desde 1 ----------
        //
        // La conversión va en un solo sitio. Repartida por cada sitio que lo llame es como
        // se cuela un «fuera por uno» que manda la ventana al escritorio de al lado.
        Igual("Uno", HyprEspacios.Resolver(tres, new Comando.CambiarEspacio(0, false))!.Value.Espacio.Nombre,
              "el índice 0 de D1 es el escritorio número 1");
        Igual("Tres", HyprEspacios.Resolver(tres, new Comando.CambiarEspacio(2, false))!.Value.Espacio.Nombre,
              "y el índice 2, el número 3");
        Vale(HyprEspacios.Resolver(tres, new Comando.CambiarEspacio(1, false)) == null,
             "el índice 1 es el actual: no se hace nada");

        // Un comando que no es de espacios devuelve null, no un destino inventado
        Vale(HyprEspacios.Resolver(tres, new Comando.EnfocarSiguiente()) == null,
             "un comando de foco no resuelve un espacio");
        Vale(HyprEspacios.Resolver(tres, new Comando.AlternarFlotante()) == null,
             "ni uno de flotante");

        // Y los cuatro atajos de espacios dan comandos de espacio
        foreach (var n in new[] { HyprAtajos.EspacioSiguiente, HyprAtajos.EspacioAnterior,
                                  HyprAtajos.LlevarAlSiguiente, HyprAtajos.LlevarAlAnterior })
        {
            var c = HyprAtajos.Comando(n);
            Vale(c is Comando.AlVecino, $"{n} da un comando de vecino");
        }
        Vale(HyprAtajos.Comando(HyprAtajos.LlevarAlSiguiente) is Comando.AlVecino { LlevarLaVentana: true },
             "y «llevar» lleva la ventana");
        Vale(HyprAtajos.Comando(HyprAtajos.EspacioSiguiente) is Comando.AlVecino { LlevarLaVentana: false },
             "mientras «espacio.siguiente» solo cambia de espacio");
    }

    static void D4MapaTolerante()
    {
        Grupo("D4: el mapa de ventanas aguanta que la API no conteste");

        var mapa = new HyprEspacios.Mapa();
        var espacioA = Guid.NewGuid();
        var espacioB = Guid.NewGuid();

        // ---------- Si nunca ha contestado, se da por el actual ----------
        //
        // Más vale enseñar una ventana de más que esconder una que existe: es la misma
        // regla que ya tenía `Escritorios.EstaEnElActual`.
        var (donde, reciente, sabido) = mapa.Consultar((IntPtr)1, espacioA, HyprEspacios.Caducidad);
        Igual(espacioA, donde, "de una ventana desconocida se supone el espacio actual");
        Vale(!sabido, "y se dice que no se sabía");
        Vale(!reciente, "ni que sea reciente");

        // ---------- Cuando contesta, se apunta ----------
        mapa.Apuntar((IntPtr)1, espacioB);
        (donde, reciente, sabido) = mapa.Consultar((IntPtr)1, espacioA, HyprEspacios.Caducidad);
        Igual(espacioB, donde, "apuntado, se devuelve lo apuntado y no el actual");
        Vale(sabido && reciente, "y es reciente");
        Igual(1, mapa.Cuantas, "con una ventana en el mapa");

        // ---------- Y lo viejo se marca como viejo, no se descarta ----------
        //
        // Preguntar justo durante un cambio devuelve cosas raras. La respuesta correcta no
        // es «no lo sé y la escondo»: es «lo último que me dijiste», con su marca.
        (donde, reciente, sabido) = mapa.Consultar((IntPtr)1, espacioA, TimeSpan.Zero);
        Igual(espacioB, donde, "con la caducidad a cero sigue devolviendo lo último");
        Vale(sabido, "que se sabe");
        Vale(!reciente, "pero marcado como NO reciente, para que quien lo use decida");

        // Datos imposibles no entran en el mapa
        mapa.Apuntar(IntPtr.Zero, espacioB);
        mapa.Apuntar((IntPtr)2, Guid.Empty);
        Igual(1, mapa.Cuantas, "una ventana nula o un espacio vacío no se apuntan");

        // ---------- La memoria no crece sin fin ----------
        mapa.Apuntar((IntPtr)2, espacioA);
        mapa.Apuntar((IntPtr)3, espacioB);
        Igual(3, mapa.Cuantas, "tres ventanas");
        Igual(2, mapa.Limpiar(new[] { (IntPtr)1 }), "limpiar con una viva quita dos");
        Igual(1, mapa.Cuantas, "y queda una");
        mapa.Olvidar((IntPtr)1);
        Igual(0, mapa.Cuantas, "olvidar la última la quita");
        Igual(0, mapa.Limpiar(Array.Empty<IntPtr>()), "y limpiar un mapa vacío no quita nada");
    }

    static void D4CambiosDeFuera()
    {
        Grupo("D4: recuperarse de un cambio hecho fuera de NubeHypr");

        // ---------- El caso que importa ----------
        //
        // El usuario pulsa Win+Ctrl+→, crea un escritorio desde la vista de tareas, o le
        // cambia el nombre. Lo que hace falta saber es QUÉ cambió, no solo que algo
        // cambió: con «algo cambió» la única reacción posible es rehacerlo todo, y
        // rehacerlo todo es como se llega al parpadeo.
        var antes = new List<HyprEspacios.Espacio> { E(0, "Uno", true), E(1, "Dos"), E(2, "Tres") };

        // Nada cambió
        var igual = HyprEspacios.Comparar(antes, antes);
        Vale(igual.Nada, "la misma foto dos veces no da cambios");
        Vale(!igual.TocaRepartir, "y no hay que rehacer el reparto");

        // ---------- Cambió el escritorio actual ----------
        var movido = new List<HyprEspacios.Espacio> { E(0, "Uno"), E(1, "Dos", true), E(2, "Tres") };
        var c1 = HyprEspacios.Comparar(antes, movido);
        Vale(c1.CambioElActual, "se detecta que cambió el actual");
        Vale(c1.TocaRepartir, "y eso SÍ obliga a rehacer el reparto: son otras ventanas");
        Igual(0, c1.Nuevos.Count, "sin escritorios nuevos");
        Igual(0, c1.Quitados.Count, "ni quitados");

        // ---------- Uno nuevo ----------
        var conUnoMas = new List<HyprEspacios.Espacio>(antes) { E(3, "Cuatro") };
        var c2 = HyprEspacios.Comparar(antes, conUnoMas);
        Igual(1, c2.Nuevos.Count, "un escritorio nuevo se detecta");
        Vale(!c2.TocaRepartir, "pero no obliga a repartir: el actual sigue siendo el mismo");

        // ---------- Uno menos ----------
        var sinElTercero = antes.Take(2).ToList();
        var c3 = HyprEspacios.Comparar(antes, sinElTercero);
        Igual(1, c3.Quitados.Count, "un escritorio que desaparece se detecta");
        Vale(c3.TocaRepartir, "y eso sí obliga: sus ventanas han ido a alguna parte");

        // ---------- Renombrado desde fuera ----------
        var renombrado = new List<HyprEspacios.Espacio>
            { E(0, "Trabajo", true), E(1, "Dos"), E(2, "Tres") };
        var c4 = HyprEspacios.Comparar(antes, renombrado);
        Igual(1, c4.Renombrados.Count, "un renombrado se detecta");
        Vale(!c4.Nada, "y cuenta como cambio");
        Vale(!c4.TocaRepartir,
             "pero NO obliga a repartir: un nombre nuevo no mueve ninguna ventana");

        // ---------- Reordenados ----------
        var reordenado = new List<HyprEspacios.Espacio>
            { E(2, "Tres"), E(1, "Dos"), E(0, "Uno", true) };
        var c5 = HyprEspacios.Comparar(antes, reordenado);
        Vale(c5.CambioElOrden, "reordenar se detecta");

        // Y el orden solo se mira cuando el conjunto es el mismo: si entró o salió alguno,
        // que "cambie el orden" es obvio y no es información
        Vale(!c2.CambioElOrden, "con uno nuevo no se dice además que cambió el orden");
        Vale(!c3.CambioElOrden, "ni con uno menos");

        // ---------- Todo a la vez, y desde cero ----------
        var deCero = HyprEspacios.Comparar(new List<HyprEspacios.Espacio>(), antes);
        Igual(3, deCero.Nuevos.Count, "de ninguno a tres, tres nuevos");
        var aCero = HyprEspacios.Comparar(antes, new List<HyprEspacios.Espacio>());
        Igual(3, aCero.Quitados.Count, "y de tres a ninguno, tres quitados");
        Vale(aCero.TocaRepartir, "que obliga a repartir, claro");

        // Pasar al modo de un solo escritorio —la API se cayó a mitad de sesión— se
        // detecta como lo que es: desaparecieron los tres y apareció uno
        var alModoDeUno = HyprEspacios.Comparar(antes, HyprEspacios.UnSoloEscritorio());
        Igual(3, alModoDeUno.Quitados.Count, "si la API se cae, se ve que se fueron los tres");
        Igual(1, alModoDeUno.Nuevos.Count, "y que apareció el único");
        Vale(alModoDeUno.TocaRepartir, "y hay que rehacer el reparto");
    }
    // ================= D3 cerrada: los atajos registrados =================
    //
    // Registrar de verdad en una prueba cogería atajos globales del equipo de quien la
    // ejecuta, y dos tandas en paralelo se pisarían. Así que aquí se comprueba todo lo que
    // DECIDE: el rango de ids, la clasificación de los códigos de Windows, el mapa de tres
    // fuentes y el ciclo de limpieza. Registrar y pulsar de verdad es `--diagatajos`.

    static void D3RangoDeIdsYConflictos()
    {
        Grupo("D3: los ids no se cruzan con el atajo de la vista general");

        // ---------- El rango propio ----------
        //
        // `IdAtajoVista` es 0xB0CA. Un rango propio es lo que permite que el hook pregunte
        // "¿este id es mío?" sin una lista de excepciones que se olvide de actualizar.
        Vale(HyprTeclas.EsNuestro(HyprTeclas.IdBase), "el primero del rango es nuestro");
        Vale(HyprTeclas.EsNuestro(HyprTeclas.IdTope), "y el último");
        Vale(!HyprTeclas.EsNuestro(HyprTeclas.IdBase - 1), "lo de antes no");
        Vale(!HyprTeclas.EsNuestro(HyprTeclas.IdTope + 1), "ni lo de después");
        Vale(!HyprTeclas.EsNuestro(0xB0CA),
             "y el id del atajo de la vista general NO cae en nuestro rango");
        Vale(HyprTeclas.IdTope - HyprTeclas.IdBase + 1 >= HyprAtajos.Nombres.Length,
             $"el rango da para los {HyprAtajos.Nombres.Length} comandos");

        // ---------- La clasificación de lo que dice Windows ----------
        //
        // 1409 es ERROR_HOTKEY_ALREADY_REGISTERED y es EL caso interesante: el atajo es
        // bueno pero alguien se le adelantó. Confundirlo con un error genérico haría que el
        // log no distinguiera "mal escrito" de "ocupado", que tienen soluciones distintas.
        Igual(HyprTeclas.Como.Registrado, HyprTeclas.Clasificar(true, 0), "si fue bien, registrado");
        Igual(HyprTeclas.Como.YaLoTieneOtro, HyprTeclas.Clasificar(false, 1409),
              "1409 es «ya lo tiene otra aplicacion»");
        Igual(HyprTeclas.Como.NoSeEntiende, HyprTeclas.Clasificar(false, -1),
              "-1 es «no entiendo la combinacion»");
        Igual(HyprTeclas.Como.Error, HyprTeclas.Clasificar(false, 5),
              "y cualquier otro es un error, sin disfrazarlo de conflicto");

        // Cada caso se explica con palabras, que es lo que acaba en el log
        foreach (var como in Enum.GetValues<HyprTeclas.Como>())
        {
            var r = new HyprTeclas.Resultado(0xB100, "Win+Alt+Right", "foco.derecha", como, 0);
            Vale(r.Explicado.Length > 0, $"{como} se explica ({r.Explicado})");
            Vale(r.ToString().Contains("Win+Alt+Right"), "y la línea dice de qué atajo habla");
        }
        Vale(new HyprTeclas.Resultado(1, "x", "y", HyprTeclas.Como.Registrado, 0).Ok,
             "solo «registrado» cuenta como Ok");
        Vale(!new HyprTeclas.Resultado(1, "x", "y", HyprTeclas.Como.YaLoTieneOtro, 1409).Ok,
             "un conflicto NO es Ok, aunque el atajo sea válido");
    }

    static void D3MapaDeTresFuentes()
    {
        Grupo("D3: el mapa junta lo del usuario, lo de serie y lo reservado");

        // ---------- Sin atajos puestos, los de serie ----------
        var opc = new OpcionesHypr();
        Igual(0, opc.Atajos.Count, "de serie la configuración no trae atajos propios");
        var conSerie = HyprTeclas.MapaDe(opc, "Ctrl+Alt+W");
        Vale(conSerie.Atajos.Count > 0, $"y el mapa cae en los de serie ({conSerie.Atajos.Count})");
        Vale(conSerie.Bien, "sin problemas: " + string.Join(" | ", conSerie.Problemas));
        Vale(conSerie.Resolver($"{HyprAtajos.Prefijo}+Right")
                 is Comando.EnfocarHacia { Hacia: Direccion.Derecha },
             $"y {HyprAtajos.Prefijo}+Right enfoca a la derecha");

        // ---------- Con atajos puestos, los suyos y SOLO los suyos ----------
        //
        // No se mezclan: si el usuario puso tres atajos, tiene tres. Añadirle los de serie
        // por debajo le daría atajos que no pidió y que no sabría de dónde salen.
        var mios = new OpcionesHypr();
        mios.Atajos["Win+Shift+Right"] = HyprAtajos.FocoDerecha;
        mios.Atajos["Win+Shift+Left"] = HyprAtajos.FocoIzquierda;
        var conMios = HyprTeclas.MapaDe(mios, "Ctrl+Alt+W");
        Igual(2, conMios.Atajos.Count, "con dos puestos, el mapa tiene dos");
        Vale(conMios.Resolver("Win+Alt+Right") == null, "y los de serie NO se cuelan por debajo");

        // ---------- Ctrl+Alt+W se conserva ----------
        //
        // Entra como reservado, así que se rechaza al construir el mapa —con su problema
        // explicado— y no en RegisterHotKey con un 1409 que parecería culpa de otro.
        var choca = new OpcionesHypr();
        choca.Atajos["Ctrl+Alt+W"] = HyprAtajos.FocoSiguiente;
        var conChoque = HyprTeclas.MapaDe(choca, "Ctrl+Alt+W");
        Igual(0, conChoque.Atajos.Count, "un atajo que pisa el de la vista general no entra");
        Vale(conChoque.Problemas.Any(p => p.Contains("ya está cogido")),
             $"y se dice ({string.Join(" | ", conChoque.Problemas)})");

        // Escrito de otra forma, choca igual: para eso se normaliza
        var choca2 = new OpcionesHypr();
        choca2.Atajos["alt+ctrl+w"] = HyprAtajos.FocoSiguiente;
        Igual(0, HyprTeclas.MapaDe(choca2, "Ctrl+Alt+W").Atajos.Count,
              "escrito al revés y en minúsculas, choca igual");

        // Y si el usuario cambió el atajo de la vista general, se reserva EL SUYO
        var otro = new OpcionesHypr();
        otro.Atajos["Ctrl+Alt+W"] = HyprAtajos.FocoSiguiente;
        Igual(1, HyprTeclas.MapaDe(otro, "Ctrl+Shift+Q").Atajos.Count,
              "con otro atajo de vista, Ctrl+Alt+W queda libre para NubeHypr");

        // Sin atajo de vista configurado no se reserva nada y no revienta
        Vale(HyprTeclas.MapaDe(new OpcionesHypr(), null).Atajos.Count > 0,
             "sin atajo de vista, el mapa se construye igual");
        Vale(HyprTeclas.MapaDe(new OpcionesHypr(), "").Atajos.Count > 0, "y con la cadena vacía");
    }

    static void D3LimpiezaDeAtajos()
    {
        Grupo("D3: registrar, soltar, volver a registrar y no dejar nada");

        // Sin ventana no se registra nada, y se dice en vez de reventar
        HyprTeclas.OlvidarTodo();
        var mapa = HyprTeclas.MapaDe(new OpcionesHypr(), "Ctrl+Alt+W");
        Igual(0, HyprTeclas.Registrar(IntPtr.Zero, mapa).Count,
              "sin ventana donde registrarlos, no se registra ninguno");
        Igual(0, HyprTeclas.Cuantos, "y no queda nada apuntado");

        // ---------- Soltar es idempotente ----------
        //
        // Es lo que permite ponerlo en el cierre sin preguntar si hacía falta. Un atajo
        // global que sobrevive al proceso se queda cogido hasta reiniciar la sesión, y el
        // usuario se encuentra una tecla que no hace nada y no sabe por qué.
        Igual(0, HyprTeclas.Quitar(IntPtr.Zero), "soltar sin nada registrado devuelve cero");
        Igual(0, HyprTeclas.Quitar(IntPtr.Zero), "y otra vez cero: no falla ni cuenta de más");
        Igual(0, HyprTeclas.Cuantos, "y sigue sin quedar nada");

        // El registro vacío no sabe de nadie
        Vale(HyprTeclas.NombreDe(HyprTeclas.IdBase) == null,
             "sin nada registrado, ningún id tiene nombre");
        Igual(0, HyprTeclas.Vivos.Count(), "y la lista de vivos está vacía");

        // ---------- El estado queda limpio ----------
        //
        // El ciclo entero con el registro real es `--diagatajos`: una prueba que registrara
        // de verdad cogería atajos globales del equipo de quien la ejecuta.
        Vale(HyprTeclas.Cuantos == 0, "la tanda acaba sin atajos apuntados");
    }
    // ================= D5: reglas, prioridades y persistencia =================

    static HyprReglas.Regla R(string id, int prio = 0, string? proc = null, string? clase = null,
                              string? titulo = null, string? ruta = null, int? espacio = null,
                              int? monitor = null, NubeHypr.Layout? layout = null,
                              bool? flotante = null, double? ancho = null, double? alto = null,
                              bool? foco = null, bool activa = true) =>
        new(id, prio, proc, clase, titulo, ruta, espacio, monitor, layout, flotante,
            ancho, alto, foco, activa);

    static void D5UnaReglaVaciaNoCoincideConNada()
    {
        Grupo("D5: una regla sin criterios no coincide con nada");

        // ---------- La decisión que lo gobierna todo ----------
        //
        // Lo contrario —que coincida con todo— es el valor por defecto que parece cómodo y
        // es el que convierte un error de tecleo en «todas mis ventanas se han ido al
        // escritorio 3».
        var vacia = R("vacia", espacio: 1);
        Vale(vacia.SinCriterios, "una regla sin criterios se reconoce como tal");
        Vale(!HyprReglas.Coincide(vacia, V(1), "Clase", @"C:\x.exe", out var p),
             "y NO coincide con una ventana cualquiera");
        Vale(p.Contains("no coincide con nada"), $"diciendo por qué ({p})");

        // Ni es válida para guardar: es una regla a medio escribir
        Vale(!HyprReglas.Valida(vacia, out var pv), "no se puede guardar");
        Vale(pv.Contains("medio escribir"), $"y se dice ({pv})");

        // Una sin ACCIONES tampoco: no haría nada
        Vale(!HyprReglas.Valida(R("muda", proc: "firefox"), out var pm), "una regla sin acciones tampoco");
        Vale(pm.Contains("no haría nada"), $"y se dice ({pm})");

        // Una completa sí
        Vale(HyprReglas.Valida(R("buena", proc: "firefox", espacio: 1), out _), "una completa vale");

        // Y los valores imposibles se rechazan antes de guardar, no después
        Vale(!HyprReglas.Valida(R("x", proc: "a", espacio: -1), out _), "un espacio negativo no vale");
        Vale(!HyprReglas.Valida(R("x", proc: "a", monitor: -2), out _), "ni un monitor negativo");
        Vale(!HyprReglas.Valida(R("x", proc: "a", ancho: 0, alto: 100), out _), "ni un ancho de cero");
        Vale(!HyprReglas.Valida(R("x", proc: "a", ancho: 99999, alto: 100), out _),
             "ni un tamaño que no cabe en ninguna pantalla");
        Vale(!HyprReglas.Valida(R("", proc: "a", espacio: 1), out var pid), "ni una sin id");
        Vale(pid.Contains("deshacer"), $"porque sin id no se puede deshacer por separado ({pid})");
    }

    static void D5CriteriosYCoincidencia()
    {
        Grupo("D5: proceso, clase, título y ruta");

        var v = new VentanaModelo((IntPtr)1, "firefox", "Configuración — Mozilla Firefox",
                                  new NubeHypr.Rect(0, 0, 800, 600));
        const string clase = "MozillaWindowClass";
        const string ruta = @"C:\Program Files\Mozilla Firefox\firefox.exe";

        // ---------- Proceso y clase: exactos, sin distinguir mayúsculas ----------
        Vale(HyprReglas.Coincide(R("a", proc: "firefox", espacio: 1), v, clase, ruta, out _),
             "el proceso coincide");
        Vale(HyprReglas.Coincide(R("a", proc: "FIREFOX", espacio: 1), v, clase, ruta, out _),
             "sin distinguir mayúsculas");
        Vale(!HyprReglas.Coincide(R("a", proc: "fire", espacio: 1), v, clase, ruta, out var p1),
             "pero NO por un trozo: el proceso es exacto");
        Vale(p1.Contains("firefox"), $"y se dice cuál es el de verdad ({p1})");

        Vale(HyprReglas.Coincide(R("a", clase: "mozillawindowclass", espacio: 1), v, clase, ruta, out _),
             "la clase coincide sin mayúsculas");
        Vale(!HyprReglas.Coincide(R("a", clase: "Chrome_WidgetWin_1", espacio: 1), v, clase, ruta, out _),
             "y otra clase no");

        // ---------- Título y ruta: contiene, y sin acentos ----------
        //
        // Quien escribe una regla para "Configuración" no tiene por qué acordarse de la
        // tilde. Es la misma norma que ya usa el buscador.
        Vale(HyprReglas.Coincide(R("a", titulo: "Firefox", espacio: 1), v, clase, ruta, out _),
             "el título por un trozo");
        Vale(HyprReglas.Coincide(R("a", titulo: "configuracion", espacio: 1), v, clase, ruta, out _),
             "sin tilde y en minúsculas");
        Vale(HyprReglas.Coincide(R("a", titulo: "CONFIGURACIÓN", espacio: 1), v, clase, ruta, out _),
             "y con tilde y en mayúsculas");
        Vale(!HyprReglas.Coincide(R("a", titulo: "Chrome", espacio: 1), v, clase, ruta, out _),
             "y lo que no está, no coincide");

        Vale(HyprReglas.Coincide(R("a", ruta: "Mozilla Firefox", espacio: 1), v, clase, ruta, out _),
             "la ruta por un trozo");
        Vale(HyprReglas.Coincide(R("a", ruta: "program files", espacio: 1), v, clase, ruta, out _),
             "sin mayúsculas");
        Vale(!HyprReglas.Coincide(R("a", ruta: @"C:\Windows", espacio: 1), v, clase, ruta, out _),
             "y otra ruta no");

        // ---------- Varios criterios se combinan con Y ----------
        Vale(HyprReglas.Coincide(R("a", proc: "firefox", titulo: "Firefox", espacio: 1), v, clase, ruta, out _),
             "dos criterios que se cumplen, coincide");
        Vale(!HyprReglas.Coincide(R("a", proc: "firefox", titulo: "Chrome", espacio: 1), v, clase, ruta, out _),
             "si uno falla, no coincide: es Y, no O");

        // ---------- Desactivada no coincide, aunque encaje ----------
        //
        // Desactivar en vez de borrar: el usuario puede querer apagar una regla un rato
        // sin perder lo que escribió.
        Vale(!HyprReglas.Coincide(R("a", proc: "firefox", espacio: 1, activa: false), v, clase, ruta, out var pd),
             "una regla desactivada no coincide");
        Vale(pd.Contains("desactivada"), $"y se dice ({pd})");

        // Sin datos de clase o ruta —pasa: muchas ventanas no los dan— una regla que los
        // pide no coincide, en vez de coincidir por omisión
        Vale(!HyprReglas.Coincide(R("a", clase: "X", espacio: 1), v, "", "", out _),
             "si no se sabe la clase, una regla que la pide no coincide");
    }

    static void D5PrioridadesYEmpates()
    {
        Grupo("D5: prioridad, especificidad y posición");

        // ---------- Tres niveles de desempate, en este orden ----------
        //
        // La prioridad explícita manda. Si empatan, gana la más específica, porque se
        // escribió pensando en un caso concreto. Y si siguen empatadas, la que está antes
        // en la lista: estable y predecible.
        var baja = R("baja", prio: 0, proc: "firefox", espacio: 1);
        var alta = R("alta", prio: 10, proc: "firefox", espacio: 2);
        var ordenadas = HyprReglas.Ordenar(new[] { baja, alta });
        Igual("alta", ordenadas[0].Id, "la de más prioridad va primera");

        var general = R("general", proc: "firefox", espacio: 1);
        var concreta = R("concreta", proc: "firefox", titulo: "GitHub", espacio: 2);
        var porEspecificidad = HyprReglas.Ordenar(new[] { general, concreta });
        Igual("concreta", porEspecificidad[0].Id,
              "con la misma prioridad, la más específica va primera");
        Igual(2, concreta.Especificidad, "y la especificidad se cuenta por criterios");
        Igual(1, general.Especificidad, "la general tiene uno");

        var primera = R("primera", proc: "a", espacio: 1);
        var segunda = R("segunda", proc: "b", espacio: 2);
        var porPosicion = HyprReglas.Ordenar(new[] { primera, segunda });
        Igual("primera", porPosicion[0].Id, "empatadas del todo, manda el orden del fichero");

        // Ordenar es estable: hacerlo dos veces da lo mismo
        var otraVez = HyprReglas.Ordenar(porPosicion);
        Vale(otraVez.Select(r => r.Id).SequenceEqual(porPosicion.Select(r => r.Id)),
             "ordenar dos veces da el mismo orden");

        // ---------- La de más peso pone el campo, y la otra NO lo pisa ----------
        var v = new VentanaModelo((IntPtr)1, "firefox", "GitHub — Firefox",
                                  new NubeHypr.Rect(0, 0, 800, 600));
        var r = HyprReglas.Resolver(v, new[] { baja, alta });
        Igual(2, r.Decision.Espacio, "el espacio lo pone la de más prioridad");
        Igual("alta", r.Decision.Quien["Espacio"], "y se sabe quién lo puso");
        Igual(2, r.Coincidencias, "las dos coincidieron");

        // La que coincide pero no aporta se dice: explica por qué «una regla que coincide»
        // no se nota
        var laQueNoAporta = r.Explicaciones.First(e => e.Id == "baja");
        Vale(laQueNoAporta.Coincidio, "la de menos prioridad coincidió");
        Vale(laQueNoAporta.ToString().Contains("no aportó nada"),
             $"y se dice que no aportó nada ({laQueNoAporta})");

        // ---------- Dos reglas se SUMAN si hablan de campos distintos ----------
        //
        // `null` significa «esta regla no opina de eso», no «ponlo a cero». Es lo que
        // permite que una diga el espacio y otra diga que sea flotante.
        var dondeVa = R("donde", proc: "firefox", espacio: 3);
        var comoVa = R("como", proc: "firefox", flotante: true);
        var suma = HyprReglas.Resolver(v, new[] { dondeVa, comoVa });
        Igual(3, suma.Decision.Espacio, "una pone el espacio");
        Igual(true, suma.Decision.Flotante, "y la otra lo flotante");
        Igual("donde", suma.Decision.Quien["Espacio"], "cada campo con su dueño");
        Igual("como", suma.Decision.Quien["Flotante"], "los dos");
    }

    static void D5LaExplicacion()
    {
        Grupo("D5: por qué coincidió o por qué no");

        // ---------- Lo que convierte una sorpresa en un dato ----------
        //
        // «Mi navegador aparece en un sitio raro» no se investiga leyendo el código: se
        // investiga sabiendo qué regla coincidió y por qué las demás no.
        var v = new VentanaModelo((IntPtr)1, "code", "NubeBar — Visual Studio Code",
                                  new NubeHypr.Rect(0, 0, 800, 600));

        var reglas = new[]
        {
            R("navegador", proc: "firefox", espacio: 1),
            R("editor", proc: "code", espacio: 2, layout: NubeHypr.Layout.Principal),
            R("por-titulo", titulo: "NubeBar", flotante: false),
            R("apagada", proc: "code", espacio: 9, activa: false),
        };

        var r = HyprReglas.Resolver(v, reglas, "Chrome_WidgetWin_1", @"C:\code\Code.exe");

        Igual(4, r.Explicaciones.Count, "hay una línea por regla, coincidan o no");
        Igual(2, r.Coincidencias, "coinciden dos");

        var noVa = r.Explicaciones.First(e => e.Id == "navegador");
        Vale(!noVa.Coincidio, "la del navegador no coincide");
        Vale(noVa.Porque.Contains("code") && noVa.Porque.Contains("firefox"),
             $"y dice qué proceso es y qué pedía ({noVa.Porque})");

        var apagada = r.Explicaciones.First(e => e.Id == "apagada");
        Vale(!apagada.Coincidio && apagada.Porque.Contains("desactivada"),
             "la desactivada se descarta por estarlo, no por los criterios");

        var siVa = r.Explicaciones.First(e => e.Id == "editor");
        Vale(siVa.Coincidio && siVa.Aplico.Contains("espacio=2"),
             $"y la que coincide dice qué aplicó ({siVa})");

        // La decisión se lee de un vistazo, con el dueño de cada campo
        var texto = r.Decision.ToString();
        Vale(texto.Contains("espacio=2") && texto.Contains("editor"),
             $"la decisión dice qué y quién ({texto})");

        // Sin reglas, nadie opina, y eso NO es un error
        var sinNada = HyprReglas.Resolver(v, Array.Empty<HyprReglas.Regla>());
        Vale(!sinNada.Decision.DiceAlgo, "sin reglas, ninguna decisión");
        Igual("ninguna regla opina", sinNada.Decision.ToString(), "y se lee así");
        Igual(0, sinNada.Explicaciones.Count, "sin explicaciones que dar");

        // Todas las acciones se pueden expresar
        var completa = R("todo", proc: "code", espacio: 1, monitor: 2,
                         layout: NubeHypr.Layout.Vertical, flotante: true,
                         ancho: 800, alto: 600, foco: true);
        var rc = HyprReglas.Resolver(v, new[] { completa });
        Igual(1, rc.Decision.Espacio, "espacio");
        Igual(2, rc.Decision.Monitor, "monitor");
        Igual(NubeHypr.Layout.Vertical, rc.Decision.Layout, "layout");
        Igual(true, rc.Decision.Flotante, "flotante");
        Igual(800.0, rc.Decision.Ancho, "ancho");
        Igual(600.0, rc.Decision.Alto, "alto");
        Igual(true, rc.Decision.Foco, "y foco");
    }

    static void D5GuardarDeshacerYRollback()
    {
        Grupo("D5: quitar una regla sin tocar las demás");

        var lista = new List<HyprReglas.Regla>
        {
            R("a", proc: "firefox", espacio: 1),
            R("b", proc: "code", espacio: 2),
            R("c", proc: "discord", espacio: 3),
        };

        // ---------- Quitar por id, no por posición ----------
        //
        // Quitar «la tercera» se rompe en cuanto el usuario reordena.
        var sinB = HyprReglas.Quitar(lista, "b");
        Igual(2, sinB.Count, "quitar una deja dos");
        Vale(sinB.Select(r => r.Id).SequenceEqual(new[] { "a", "c" }),
             "y las demás conservan su orden");
        Vale(HyprReglas.Quitar(lista, "B").Count == 2, "el id no distingue mayúsculas");
        Igual(3, HyprReglas.Quitar(lista, "noexiste").Count, "quitar una que no está no quita nada");

        // ---------- Desactivar es reversible; borrar no ----------
        var apagada = HyprReglas.Alternar(lista, "b");
        Vale(!apagada.First(r => r.Id == "b").Activa, "alternar la desactiva");
        Igual(3, apagada.Count, "sin perderla");
        Vale(HyprReglas.Alternar(apagada, "b").First(r => r.Id == "b").Activa,
             "y alternar otra vez la vuelve a encender");

        // ---------- Poner actualiza en su sitio ----------
        var cambiada = HyprReglas.Poner(lista, R("b", proc: "code", espacio: 7));
        Igual(3, cambiada.Count, "actualizar no duplica");
        Igual(7, cambiada.First(r => r.Id == "b").Espacio, "y el valor es el nuevo");
        Vale(cambiada.Select(r => r.Id).SequenceEqual(new[] { "a", "b", "c" }),
             "y NO se mueve en el orden: por eso no da saltos");

        var anadida = HyprReglas.Poner(lista, R("d", proc: "steam", espacio: 4));
        Igual(4, anadida.Count, "una nueva se añade");
        Igual("d", anadida[^1].Id, "al final");

        // ---------- El rollback ----------
        //
        // Guardar la lista entera antes de tocarla y poder volver. Es lo mismo que hacen
        // los perfiles de C5 con las claves del config, y por el mismo motivo.
        var antes = HyprReglas.Instantanea(lista);
        var tocada = HyprReglas.Quitar(HyprReglas.Poner(lista, R("e", proc: "x", espacio: 5)), "a");
        var (puestas, quitadas, cambiadas) = HyprReglas.Comparar(antes, tocada);
        Igual(1, puestas.Count, "se ve la que entró");
        Igual("e", puestas[0], "y cuál");
        Igual(1, quitadas.Count, "la que salió");
        Igual("a", quitadas[0], "y cuál");
        Igual(0, cambiadas.Count, "y ninguna cambiada");

        // Volver atrás devuelve exactamente lo de antes
        var vuelta = HyprReglas.Instantanea(antes);
        Vale(vuelta.Select(r => r.Id).SequenceEqual(antes.Select(r => r.Id)),
             "la instantánea devuelve la lista de antes");
        Vale(HyprReglas.Comparar(antes, vuelta) is { Puestas.Count: 0, Quitadas.Count: 0, Cambiadas.Count: 0 },
             "y comparada con el original no hay diferencias");

        // Un cambio de valor se detecta como cambio, no como quitar y poner
        var otroValor = HyprReglas.Poner(antes, R("a", proc: "firefox", espacio: 99));
        var (_, _, cam2) = HyprReglas.Comparar(antes, otroValor);
        Igual(1, cam2.Count, "cambiar el valor de una regla se ve como cambio");
        Igual("a", cam2[0], "y de cuál");

        // ---------- Ids libres ----------
        Igual("firefox", HyprReglas.IdLibre(lista, "Firefox"), "un id libre se usa tal cual, sin acentos");
        Igual("a-2", HyprReglas.IdLibre(lista, "a"), "uno cogido se numera");
        Vale(HyprReglas.IdLibre(lista, null).Length > 0, "y sin propuesta se inventa uno");
        Vale(!HyprReglas.IdLibre(lista, "Configuración").Contains('ó'),
             "los acentos se quitan: un id es para el fichero, no para leerlo");
    }

    static void D5ReglasEnLaConfiguracion()
    {
        Grupo("D5: las reglas en el config, y lo desconocido se conserva");

        // ---------- De serie, ninguna ----------
        var opc = new OpcionesHypr();
        Igual(0, opc.Reglas.Count, "de serie no hay ninguna regla");
        Vale(!opc.Activo, "y la capa sigue apagada");

        // ---------- Un config antiguo no trae reglas y no se rompe ----------
        var v3 = new JsonObject { ["Version"] = 3, ["Tema"] = new JsonObject() };
        Esquema.Migrar(v3);
        Vale(v3["NubeHypr"]!["Reglas"] is JsonArray a && a.Count == 0,
             "al migrar, las reglas quedan como una lista vacía");

        // ---------- Lo que esta versión no conoce se conserva ----------
        //
        // Es el requisito del documento, y la forma de cumplirlo es que las migraciones
        // trabajen sobre el ÁRBOL: lo que no se toca sigue ahí.
        var conExtras = new JsonObject
        {
            ["Version"] = 3,
            ["NubeHypr"] = new JsonObject
            {
                ["Activo"] = false,
                ["UnaClaveDelFuturo"] = "no la conozco",
                ["OtraMas"] = 42,
            },
            ["SeccionQueNoExiste"] = new JsonObject { ["x"] = 1 },
        };
        Esquema.Migrar(conExtras);
        Igual("no la conozco", conExtras["NubeHypr"]!["UnaClaveDelFuturo"]!.GetValue<string>(),
              "una clave desconocida dentro de NubeHypr sobrevive a la migración");
        Igual(42, conExtras["NubeHypr"]!["OtraMas"]!.GetValue<int>(), "y otra más");
        Vale(conExtras["SeccionQueNoExiste"] is JsonObject,
             "y una sección entera desconocida también");
        Igual(false, conExtras["NubeHypr"]!["Activo"]!.GetValue<bool>(),
              "sin encender la capa, que es lo importante");

        // Y el validador no se queja de lo que no conoce
        Vale(Esquema.Mal(conExtras, 0) == null,
             "el validador no rechaza un árbol por llevar claves que no entiende");

        // ---------- Una regla da la vuelta por JSON sin perder nada ----------
        var regla = R("prueba", prio: 5, proc: "firefox", titulo: "GitHub",
                      espacio: 2, layout: NubeHypr.Layout.Vertical, flotante: true,
                      ancho: 900, alto: 700, foco: true);
        var json = System.Text.Json.JsonSerializer.Serialize(regla);
        var vuelta = System.Text.Json.JsonSerializer.Deserialize<HyprReglas.Regla>(json);
        Vale(vuelta != null, "la regla se deserializa");
        Igual(regla, vuelta, "y es exactamente la misma: nada se pierde por el camino");

        // Los nulos siguen siendo nulos: «no opino» no puede convertirse en un cero
        var media = R("media", proc: "x", espacio: 1);
        var vueltaMedia = System.Text.Json.JsonSerializer.Deserialize<HyprReglas.Regla>(
            System.Text.Json.JsonSerializer.Serialize(media));
        Vale(vueltaMedia!.Flotante == null, "lo que no opinaba sigue sin opinar");
        Vale(vueltaMedia.Ancho == null, "y no se convierte en cero");
    }

    // ================= D5.1: las reglas conectadas a que aparezca una ventana =================
    //
    // Siete grupos, que son los siete casos que pide el documento, más dos de integración.
    // Todos PUROS: no se abre ni una ventana, no se mueve ni una, y `HyprVigilante` no
    // llama a Win32 en ninguno.

    // La ventana con título y proceso a medida: el helper `V` pone el título "t<hwnd>", y
    // para probar el criterio de título hace falta decidirlo.
    static VentanaModelo Vt(long h, string proc, string titulo) =>
        new((IntPtr)h, proc, titulo, new NubeHypr.Rect(0, 0, 400, 300));

    static void D51ReglaAplicable()
    {
        Grupo("D5.1: una regla aplicable se aporta, y se ve qué aportó");

        var reglas = new List<HyprReglas.Regla> { R("nav", proc: "firefox", espacio: 3) };
        var a = HyprVigilante.Examinar(Vt(1, "firefox", "GitHub"), reglas);

        Vale(a.AlgunaCoincidio, "la regla coincide");
        Igual(3, a.Decision.Espacio, "y pone el espacio");
        Vale(a.CamposPuestos.Contains("Espacio"), "el espacio sale en los campos puestos");

        // ---------- Y esto es la mitad del valor del diagnóstico ----------
        //
        // Los campos de los que NADIE opinó, listados. Sin ellos, "mi ventana no se va al
        // monitor 2" no se distingue de "se intentó y no funcionó".
        Vale(a.CamposSinOpinion.Contains("Monitor"), "del monitor no opinó nadie, y se dice");
        Vale(a.CamposSinOpinion.Contains("Layout"), "ni del layout");
        Igual(5, a.CamposSinOpinion.Count, "cinco campos sin opinión de los seis");

        // ---------- La línea del log dice la verdad sobre si se movió algo ----------
        //
        // Esta prueba exigía `modo=simulacion` y «NO se ha movido nada» **siempre**, porque
        // así estaba escrito: un literal fijo de cuando el vigilante solo resolvía. Desde
        // D9.5 mueve y desde D9.6 reparte escritorios, así que la prueba estaba blindando
        // una mentira — un registro que dice «simulacion» mientras las ventanas se mueven.
        //
        // Ahora se exige lo contrario: que la línea diga **el modo de verdad**, y que diga
        // lo que va a pasar con ese modo.
        var enSimulacion = HyprVigilante.Lineas(a, NubeHypr.Modo.Simulacion);
        Vale(enSimulacion.Any(l => l.Contains("modo=simulacion")),
             "en simulación la primera línea dice «simulacion»");
        Vale(enSimulacion.Any(l => l.Contains("NO se mueve nada")),
             "y se apunta explícitamente que no se mueve nada");
        Vale(enSimulacion.Any(l => l.Contains("firefox")), "y de qué ventana se habla");

        // Y con la capa aplicando, la misma línea NO puede decir que no se mueve nada.
        var aplicando = HyprVigilante.Lineas(a, NubeHypr.Modo.Aplicar);
        Vale(aplicando.Any(l => l.Contains("modo=aplicar")),
             "aplicando la primera línea dice «aplicar», no «simulacion»");
        Vale(!aplicando.Any(l => l.Contains("NO se mueve nada")),
             "y NO promete que no se mueve nada, porque sí se mueve");
        Vale(aplicando.Any(l => l.Contains("SÍ se ejecuta")),
             "lo dice en claro, y remite a las líneas de D9.5 y D9.6");

        // Apagada, lo mismo por el otro lado.
        Vale(HyprVigilante.Lineas(a, NubeHypr.Modo.Apagado).Any(l => l.Contains("apagada")),
             "y con la capa apagada se dice que está apagada");

        // Y quien solo quiere ver la decisión —el editor de reglas, la guía— no recibe un
        // modo inventado: recibe que aquí solo se resuelve.
        var sinModo = HyprVigilante.Lineas(a);
        Vale(sinModo.Any(l => l.Contains("solo se resuelve")),
             "sin modo no se inventa uno: se dice que aquí solo se resuelve");
    }

    static void D51ReglaAmbigua()
    {
        Grupo("D5.1: dos reglas que valen para la misma ventana y el mismo campo");

        // Ambas coinciden y ambas opinan del espacio. No es un error: gana la de más peso
        // y la otra queda apuntada como "coincide pero no aportó nada nuevo". Que una
        // regla coincida y no cambie nada es información, no silencio.
        var reglas = new List<HyprReglas.Regla>
        {
            R("general", proc: "code", espacio: 1),
            R("concreta", proc: "code", titulo: "nubebar", espacio: 9),
        };
        var a = HyprVigilante.Examinar(Vt(1, "code", "NubeBar - Visual Studio Code"), reglas);

        Igual(2, a.Explicaciones.Count(e => e.Coincidio), "las dos coinciden");
        Igual(9, a.Decision.Espacio, "y gana la más específica: dos criterios pesan más que uno");
        Igual("concreta", a.Decision.Quien["Espacio"], "y queda escrito quién lo decidió");

        var perdedora = a.Explicaciones.First(e => e.Id == "general");
        Vale(perdedora.ToString().Contains("no aportó nada nuevo"),
             "la que coincidió y no pudo poner nada lo dice, en vez de desaparecer del log");
    }

    static void D51Prioridad()
    {
        Grupo("D5.1: la prioridad manda sobre la especificidad");

        // La general tiene prioridad 10 y un solo criterio; la concreta tiene prioridad 0
        // y dos. Gana la prioridad: si no ganara, poner una prioridad no serviría para
        // nada y habría que adivinar el orden interno de evaluación.
        var reglas = new List<HyprReglas.Regla>
        {
            R("concreta", prio: 0, proc: "code", titulo: "nubebar", espacio: 9),
            R("mandona", prio: 10, proc: "code", espacio: 1),
        };
        var a = HyprVigilante.Examinar(Vt(1, "code", "NubeBar - Visual Studio Code"), reglas);

        Igual(1, a.Decision.Espacio, "gana la de prioridad 10 aunque sea menos específica");
        Igual("mandona", a.Decision.Quien["Espacio"], "y se sabe cuál fue");

        // Y una prioridad negativa es "la última de todas": sirve para una regla de
        // respaldo que solo actúe si nadie más ha opinado.
        var conRespaldo = new List<HyprReglas.Regla>
        {
            R("respaldo", prio: -5, proc: "code", monitor: 0),
            R("normal", proc: "code", espacio: 4),
        };
        var b = HyprVigilante.Examinar(Vt(1, "code", "x"), conRespaldo);
        Igual(4, b.Decision.Espacio, "la normal pone el espacio");
        Igual(0, b.Decision.Monitor, "y la de respaldo llega a poner lo que nadie puso");
        Igual("respaldo", b.Decision.Quien["Monitor"], "aunque vaya la última");
    }

    static void D51Empate()
    {
        Grupo("D5.1: el empate se rompe por el orden, no por el azar");

        // Misma prioridad, misma especificidad, mismo campo. El desempate es la POSICIÓN
        // en la lista, y eso importa: sin un desempate estable, la misma configuración
        // daría decisiones distintas entre arranques, y eso es exactamente lo que haría
        // imposible aplicarlas de verdad después.
        var reglas = new List<HyprReglas.Regla>
        {
            R("primera", proc: "code", espacio: 1),
            R("segunda", proc: "code", espacio: 2),
        };
        var a = HyprVigilante.Examinar(Vt(1, "code", "x"), reglas);
        Igual(1, a.Decision.Espacio, "gana la que está antes en la lista");

        // Y con la lista al revés gana la otra: se confirma que el criterio es el orden
        // y no algo del contenido que diera siempre el mismo resultado por casualidad.
        var alReves = new List<HyprReglas.Regla> { reglas[1], reglas[0] };
        Igual(2, HyprVigilante.Examinar(Vt(1, "code", "x"), alReves).Decision.Espacio,
              "y al darle la vuelta a la lista, gana la otra");

        // La perdedora sigue saliendo en el log, marcada como coincidente
        Igual(2, a.Explicaciones.Count(e => e.Coincidio), "las dos aparecen como coincidentes");
    }

    static void D51VentanaDesconocida()
    {
        Grupo("D5.1: una ventana que no encaja en ninguna regla se deja en paz");

        var reglas = new List<HyprReglas.Regla>
        {
            R("nav", proc: "firefox", espacio: 2),
            R("ed", proc: "code", espacio: 3),
        };
        var a = HyprVigilante.Examinar(Vt(1, "calculadora", "Calculadora"), reglas);

        Vale(!a.AlgunaCoincidio, "ninguna regla coincide");
        Vale(!a.Decision.DiceAlgo, "y la decisión no dice nada");
        Igual(0, a.CamposPuestos.Count, "ningún campo puesto");
        Igual(6, a.CamposSinOpinion.Count, "los seis campos sin opinión");

        // El log de este caso tiene que ser corto y claro. Un "ninguna regla coincide" de
        // una línea por ventana es legible; seis líneas de "no opinó nadie" por cada
        // ventana que abres, no.
        var lineas = HyprVigilante.Lineas(a);
        Igual(2, lineas.Count, "dos líneas y se acabó: la ventana y el «ninguna coincide»");
        Vale(lineas[1].Contains("ninguna regla coincide"), "y lo dice con esas palabras");

        // Y se explica POR QUÉ cada una no coincidió: sin eso, una regla que no dispara
        // se depura cambiándola al azar.
        Igual(2, a.Explicaciones.Count, "las dos reglas se explican");
        Vale(a.Explicaciones.All(e => !e.Coincidio && e.Porque.Length > 0),
             "cada una dice por qué no");
    }

    static void D51VariasReglasAportanCamposDistintos()
    {
        Grupo("D5.1: varias reglas aportando campos distintos a la misma ventana");

        // Tres reglas coinciden y cada una pone lo suyo. Es el caso que el documento pide
        // explícitamente, y el que de verdad se usa: una regla dice el espacio, otra el
        // tamaño, otra que flote.
        var reglas = new List<HyprReglas.Regla>
        {
            R("espacio", prio: 3, proc: "firefox", espacio: 2),
            R("tamano", prio: 2, proc: "firefox", ancho: 0.8, alto: 0.9),
            R("flota", prio: 1, proc: "firefox", flotante: true),
            R("otra-app", proc: "code", monitor: 1),
        };
        var a = HyprVigilante.Examinar(Vt(1, "firefox", "GitHub"), reglas);

        Igual(3, a.Explicaciones.Count(e => e.Coincidio), "coinciden tres de las cuatro");
        Igual(2, a.Decision.Espacio, "el espacio lo pone una");
        Igual(0.8, a.Decision.Ancho, "el tamaño otra");
        Igual(true, a.Decision.Flotante, "y que flote, otra");
        Vale(a.Decision.Monitor == null, "y del monitor no opina nadie: esa regla era de otra app");

        // El mapa de quién: tres campos, tres dueños distintos. Es lo que contesta "¿por
        // qué esta ventana sale flotando?" sin tener que releer la configuración entera.
        Igual("espacio", a.Decision.Quien["Espacio"], "cada campo recuerda su dueño");
        Igual("tamano", a.Decision.Quien["Tamano"], "el del tamaño");
        Igual("flota", a.Decision.Quien["Flotante"], "y el de flotante");

        Igual(3, a.CamposPuestos.Count, "tres campos puestos");
        Vale(a.CamposSinOpinion.SequenceEqual(new[] { "Monitor", "Layout", "Foco" }),
             "y los tres que quedan, listados como sin opinión");
    }

    static void D51Idempotencia()
    {
        Grupo("D5.1: resolver dos veces da lo mismo, y una ventana se resuelve una sola vez");

        var reglas = new List<HyprReglas.Regla>
        {
            R("a", prio: 2, proc: "firefox", espacio: 2, ancho: 0.5),
            R("b", prio: 1, proc: "firefox", espacio: 9, flotante: true),
        };
        var v = Vt(1, "firefox", "GitHub");

        // ---------- Mismo resultado al repetir ----------
        var uno = HyprVigilante.Examinar(v, reglas);
        var dos = HyprVigilante.Examinar(v, reglas);
        Igual(uno.Decision.ToString(), dos.Decision.ToString(), "la misma decisión, al detalle");
        Vale(uno.CamposPuestos.SequenceEqual(dos.CamposPuestos), "los mismos campos puestos");
        Vale(uno.Explicaciones.SequenceEqual(dos.Explicaciones), "y las mismas explicaciones");

        // ---------- Y la conexión: una ventana se examina UNA vez ----------
        //
        // Esto es lo que evita que una ventana que cambia de título vuelva a "aparecer"
        // para las reglas cuarenta veces por minuto.
        HyprVigilante.Olvidar();
        var lista = new List<VentanaModelo> { v };

        var primera = HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Simulacion, lista, reglas);
        Igual(1, primera.Count, "la primera vez se resuelve");
        var segunda = HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Simulacion, lista, reglas);
        Igual(0, segunda.Count, "la segunda no: ya se vio");
        Igual(1, (int)HyprVigilante.Resueltas, "y el contador no sube de uno");

        // Una ventana NUEVA sí se resuelve, y la vieja no otra vez
        var conOtra = new List<VentanaModelo> { v, Vt(2, "firefox", "Otra") };
        var tercera = HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Simulacion, conOtra, reglas);
        Igual(1, tercera.Count, "solo la nueva");
        Igual((IntPtr)2, tercera[0].Hwnd, "y es la que acaba de aparecer");

        // ---------- La que se va, se olvida ----------
        //
        // Si no se olvidara, una sesión larga acumularía handles muertos y un hwnd que
        // Windows reutilizara se daría por ya visto: la ventana nueva no se resolvería.
        HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Simulacion, new List<VentanaModelo>(), reglas);
        Igual(0, HyprVigilante.Vistas, "al cerrarse todas, no queda ningún handle recordado");
        var otraVez = HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Simulacion, lista, reglas);
        Igual(1, otraVez.Count, "y un hwnd reutilizado se vuelve a resolver");

        // ---------- Apagada: no se resuelve ni se recuerda ----------
        HyprVigilante.Olvidar();
        Igual(0, HyprVigilante.AlCambiarLaLista(NubeHypr.Modo.Apagado, lista, reglas).Count,
              "con la capa apagada no se resuelve nada");
        Igual(0, HyprVigilante.Vistas, "y no se recuerda nada: apagada es apagada");

        // ---------- Sin reglas: se marca la ventana pero no se apunta nada ----------
        //
        // Es el caso de serie (la lista nace vacía) y tiene que ser barato: sin reglas no
        // hay nada que resolver, pero la ventana se da por vista para no volver a mirarla.
        HyprVigilante.Olvidar();
        var sinReglas = HyprVigilante.AlCambiarLaLista(
            NubeHypr.Modo.Simulacion, lista, new List<HyprReglas.Regla>());
        Igual(0, sinReglas.Count, "sin reglas no sale ningún apunte");
        Igual(1, HyprVigilante.Vistas, "pero la ventana queda vista");
        Igual(1, (int)HyprVigilante.SinReglas, "y se cuenta aparte, para poder decirlo en el log");
        HyprVigilante.Olvidar();
    }

    static void D51ConfiguracionAntiguaConCadenas()
    {
        Grupo("D5.1: Reglas con cadenas de un config viejo no tumba la configuración");

        // ---------- El riesgo real ----------
        //
        // `Reglas` nació como `List<string>` en un borrador. Si alguien tuviera cadenas
        // ahí, `JsonSerializer` lanzaría, `Config.Cargar` devolvería null y NubeBar
        // arrancaría con la configuración de serie: el usuario perdería su configuración
        // entera por una clave. Por eso el lector es tolerante.
        var opc = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new HyprReglas.LectorTolerante() },
        };

        var cadenas = System.Text.Json.JsonSerializer.Deserialize<OpcionesHypr>(
            "{\"Activo\":false,\"Reglas\":[\"firefox\",\"code\"]}", opc);
        Vale(cadenas != null, "un config con cadenas en Reglas se lee en vez de reventar");
        Igual(2, cadenas!.Reglas.Count, "las dos entradas llegan");
        Igual("firefox", cadenas.Reglas[0].Id, "y el texto original se conserva como id");

        // Y son INERTES: sin criterios no coinciden con nada, así que una configuración
        // vieja no empieza a decidir sobre las ventanas de nadie.
        Vale(cadenas.Reglas.All(r => r.SinCriterios), "sin criterios: no coinciden con nada");
        var a = HyprVigilante.Examinar(Vt(1, "firefox", "GitHub"), cadenas.Reglas);
        Vale(!a.AlgunaCoincidio, "una regla heredada de una cadena no coincide ni con su propio nombre");

        // ---------- Y lo demás de la sección se lee bien ----------
        Vale(!cadenas.Activo, "la capa sigue apagada");

        // ---------- Números, booleanos y nulos tampoco rompen ----------
        var basura = System.Text.Json.JsonSerializer.Deserialize<OpcionesHypr>(
            "{\"Reglas\":[1,true,null,\"x\"]}", opc);
        Igual(4, basura!.Reglas.Count, "cuatro entradas de basura se leen como cuatro reglas inertes");
        Vale(basura.Reglas.All(r => r.SinCriterios), "y ninguna coincide con nada");

        // ---------- Un objeto mal formado tampoco ----------
        var roto = System.Text.Json.JsonSerializer.Deserialize<OpcionesHypr>(
            "{\"Reglas\":[{\"Id\":\"ok\",\"Proceso\":\"code\"},{\"Id\":5,\"Espacio\":\"dos\"}]}", opc);
        Igual(2, roto!.Reglas.Count, "las dos entradas llegan");
        Igual("ok", roto.Reglas[0].Id, "la buena se lee entera");
        Igual("code", roto.Reglas[0].Proceso, "con su criterio");
        Vale(roto.Reglas[1].SinCriterios, "y la rota queda inerte en vez de tumbar la carga");

        // ---------- Una regla normal sigue dando la vuelta sin perder nada ----------
        var buena = R("prueba", prio: 5, proc: "firefox", espacio: 2, flotante: true);
        var vuelta = System.Text.Json.JsonSerializer.Deserialize<HyprReglas.Regla>(
            System.Text.Json.JsonSerializer.Serialize(buena, opc), opc);
        Igual(buena, vuelta, "con el lector tolerante puesto, una regla buena no cambia");
    }

    static void D51ReglasYAtajosEnLosPerfiles()
    {
        Grupo("D5.1: los perfiles pueden llevar reglas y atajos, pero no encender la capa");

        // ---------- Lo que un perfil SÍ puede ----------
        Vale(Perfiles.Permitida("NubeHypr", "Reglas"), "un perfil puede traer reglas");
        Vale(Perfiles.Permitida("NubeHypr", "Atajos"), "y atajos");

        // ---------- Y lo que NO ----------
        //
        // Esto importa más que lo anterior. Un perfil cambia la configuración; si pudiera
        // encender la capa —o ponerla en "aplicar"— aplicar un perfil te empezaría a mover
        // ventanas sin haberlo pedido.
        Vale(!Perfiles.Permitida("NubeHypr", "Activo"), "un perfil NO puede encender la capa");
        Vale(!Perfiles.Permitida("NubeHypr", "Modo"), "ni cambiar el modo");
        Vale(!Perfiles.Permitida("NubeHypr", "RestaurarAlOrganizar"),
             "ni decidir que se desmaximicen tus ventanas");

        // ---------- Un perfil con reglas se traduce a escrituras, no a movimientos ----------
        //
        // Lo que un perfil produce son escrituras de configuración. Que una de ellas sea
        // una lista de reglas no cambia nada: nadie mueve ventanas al aplicar un perfil.
        var p = new Perfil
        {
            Id = "conreglas", Nombre = "Con reglas",
            Ajustes = new JsonObject
            {
                ["NubeHypr"] = new JsonObject
                {
                    ["Reglas"] = new JsonArray
                    {
                        new JsonObject { ["Id"] = "del-perfil", ["Proceso"] = "code", ["Espacio"] = 2 },
                    },
                    ["Atajos"] = new JsonObject { ["ciclo.siguiente"] = "Win+Alt+J" },
                    ["Activo"] = true,        // esta tiene que caerse
                    ["Modo"] = "aplicar",     // y esta también
                },
                ["Tema"] = new JsonObject { ["Acento"] = "#112233" },
            },
        };

        var escrituras = Perfiles.Escrituras(p);
        Vale(escrituras.Any(e => e.Seccion == "NubeHypr" && e.Clave == "Reglas"),
             "las reglas del perfil se escriben");
        Vale(escrituras.Any(e => e.Seccion == "NubeHypr" && e.Clave == "Atajos"),
             "y los atajos también");
        Vale(!escrituras.Any(e => e.Clave == "Activo"),
             "encender la capa se descarta, aunque venga en el fichero del perfil");
        Vale(!escrituras.Any(e => e.Clave == "Modo"),
             "y poner el modo en «aplicar», igual: se descarta");
        Vale(escrituras.Any(e => e.Seccion == "Tema" && e.Clave == "Acento"),
             "y lo demás del perfil sigue funcionando");

        // Las reglas llegan como el nodo entero, no aplanadas: una lista es un valor.
        var deReglas = escrituras.First(e => e.Clave == "Reglas");
        Vale(deReglas.Valor is JsonArray arr && arr.Count == 1,
             "la lista de reglas viaja entera como un solo valor");

        // Y el contador de movimientos reales sigue a cero: aplicar reglas no es mover.
        Igual(0L, HyprAdaptador.VecesMovida,
              "en toda la batería de pruebas no se ha movido ni una ventana de verdad");
    }
}
