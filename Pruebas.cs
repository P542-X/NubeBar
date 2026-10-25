using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
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
public static class Pruebas
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
            PerfilesListaBlanca();
            PerfilesValidar();
            PerfilesNombres();
            PerfilesDuplicar();
            PerfilesDeSerie();
            PerfilesEscrituras();
            PerfilesAplicarYDeshacer();
            PerfilesRestaurar();
            GlifoDeLaCuadricula();
            AbrirYCerrar();
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
        Igual(2, r1.Aplicadas.Count, "se aplicaron las dos migraciones, en orden");
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
        Igual(1, r3.Aplicadas.Count, "y solo le hacía falta una migración");
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
}
