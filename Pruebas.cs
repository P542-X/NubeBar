using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
