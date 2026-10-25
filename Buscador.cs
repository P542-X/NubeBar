using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NubeBar;

public enum Categoria { Calculo, Comando, App, Ventana, Ajuste, Archivo, Web }

// Cómo de bien encaja lo escrito con lo encontrado: los cuatro peldaños del ranking,
// de mejor a peor. Cada uno ocupa una FRANJA propia de puntuación, así que ordenar por
// puntos y ordenar por peldaño dan lo mismo y no hay dos verdades que mantener.
public enum Nivel { Ninguno = 0, Aproximada = 1, Palabra = 2, Prefijo = 3, Exacta = 4 }

public readonly record struct Coincidencia(Nivel Nivel, double Puntos)
{
    public static readonly Coincidencia Nada = new(Nivel.Ninguno, 0);
}

public sealed class Resultado
{
    public Categoria Categoria;
    public string Titulo = "";
    public string Detalle = "";
    public string Clave = "";            // identidad estable, para contar usos
    public string? IconoRuta;            // lo que se le pasa al shell para sacar el icono
    public string? Carpeta;              // para "abrir la ubicación" con Ctrl+Enter
    public Action Abrir = () => { };
    public double Puntos;
    public Nivel Nivel = Nivel.Ninguno;  // en qué peldaño del ranking cayó

    public string Etiqueta => Categoria switch
    {
        Categoria.Calculo => "Cálculo",
        Categoria.Comando => "Comando",
        Categoria.App     => "Aplicación",
        Categoria.Ventana => "Ventana abierta",
        Categoria.Ajuste  => "Ajuste",
        Categoria.Archivo => "Archivo",
        _                 => "Web"
    };
}

// Comparación difusa: tolera acentos, mayúsculas y erratas ("fotosop" -> Photoshop)
public static class Parecido
{
    // Quita acentos y pasa a minúsculas: "Cámara" -> "camara"
    public static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // 0 = no se parece en nada, 1 = clavado. Es `Medir` quedándose solo con la cifra,
    // para quien no necesita saber por qué peldaño entró.
    public static double Puntuar(string consultaNorm, string textoOriginal) =>
        Medir(consultaNorm, textoOriginal).Puntos;

    // ---------- Los cuatro peldaños ----------
    //
    // Antes esto devolvía un número suelto que mezclaba prefijo, subsecuencia y
    // distancia de edición, y la cifra era la única explicación de por qué una cosa
    // salía antes que otra. Funcionaba, pero no se podía razonar sobre ella: un 0,88
    // podía ser "las iniciales encajan" o "se parece bastante escrito mal", y esas dos
    // cosas no deben valer lo mismo nunca.
    //
    // Ahora cada criterio cae en su franja, y la franja es el peldaño:
    //
    //   Exacta      1,00          lo escrito ES el nombre
    //   Prefijo     0,90 – 0,99   el nombre empieza por lo escrito
    //   Palabra     0,78 – 0,89   alguna palabra empieza por ahí, o las iniciales
    //   Aproximada  0,01 – 0,76   subsecuencia o erratas
    //
    // Dentro de cada franja se afina con lo que cubre la consulta: escribir "note" para
    // "Notepad" vale más que para "Notepad++ plugin manager", porque cubre más del
    // nombre. Eso es lo que decide los empates de verdad, y no el orden en que la
    // fuente devolvió las cosas.
    public static Coincidencia Medir(string consultaNorm, string textoOriginal)
    {
        if (consultaNorm.Length == 0) return Coincidencia.Nada;
        var texto = Normalizar(textoOriginal);
        if (texto.Length == 0) return Coincidencia.Nada;

        // Cuánto del nombre cubre lo escrito, de 0 a 1. Afina dentro de la franja.
        double cobertura = Math.Min(1.0, (double)consultaNorm.Length / texto.Length);

        if (texto.Equals(consultaNorm, StringComparison.Ordinal))
            return new Coincidencia(Nivel.Exacta, 1.0);

        if (texto.StartsWith(consultaNorm, StringComparison.Ordinal))
            return new Coincidencia(Nivel.Prefijo, 0.90 + 0.09 * cobertura);

        var palabras = texto.Split(new[] { ' ', '-', '_', '.', '(', ')', '[', ']' },
                                   StringSplitOptions.RemoveEmptyEntries);

        // Iniciales de las palabras: "vsc" -> "Visual Studio Code"
        if (palabras.Length > 1)
        {
            var iniciales = new string(palabras.Select(p => p[0]).ToArray());
            if (iniciales.StartsWith(consultaNorm, StringComparison.Ordinal))
                return new Coincidencia(Nivel.Palabra, 0.86 + 0.03 * cobertura);
        }
        // Alguna palabra empieza por lo escrito
        if (palabras.Any(p => p.StartsWith(consultaNorm, StringComparison.Ordinal)))
            return new Coincidencia(Nivel.Palabra, 0.82 + 0.04 * cobertura);
        // O aparece entero por el medio. Va el último de su franja: "note" dentro de
        // "Keynote" encaja menos que "Notepad", que empieza por ahí.
        if (texto.Contains(consultaNorm, StringComparison.Ordinal))
            return new Coincidencia(Nivel.Palabra, 0.78 + 0.03 * cobertura);

        // Subsecuencia en orden: "vscode" dentro de "visual studio code"
        double sub = Subsecuencia(consultaNorm, texto);
        // Erratas: con el texto entero y con cada palabra, y gana la mejor
        double edicion = Similitud(consultaNorm, texto);
        foreach (var p in palabras) edicion = Math.Max(edicion, Similitud(consultaNorm, p));

        double flojo = Math.Max(sub, edicion);
        return flojo <= 0 ? Coincidencia.Nada
                          : new Coincidencia(Nivel.Aproximada, Math.Min(0.76, flojo));
    }

    // Si todas las letras aparecen en orden, puntúa según lo juntas que estén
    static double Subsecuencia(string consulta, string texto)
    {
        int i = 0, saltos = 0, ultimo = -1;
        foreach (var c in consulta)
        {
            int p = texto.IndexOf(c, i);
            if (p < 0) return 0;
            if (ultimo >= 0 && p != ultimo + 1) saltos++;
            ultimo = p;
            i = p + 1;
        }
        return 0.80 - Math.Min(0.35, saltos * 0.05);
    }

    // 1 - distancia de edición normalizada
    static double Similitud(string a, string b)
    {
        int d = Distancia(a, b);
        int largo = Math.Max(a.Length, b.Length);
        return largo == 0 ? 0 : Math.Max(0, 1.0 - (double)d / largo) * 0.78;
    }

    static int Distancia(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previa = new int[b.Length + 1];
        var actual = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previa[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            actual[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int coste = a[i - 1] == b[j - 1] ? 0 : 1;
                actual[j] = Math.Min(Math.Min(actual[j - 1] + 1, previa[j] + 1), previa[j - 1] + coste);
            }
            (previa, actual) = (actual, previa);
        }
        return previa[b.Length];
    }
}

// Cómo se ordena y se limpia lo encontrado.
//
// Vive aquí y no en la isla a propósito: es lógica pura —entra una lista, sale otra— y
// eso la hace comprobable sin abrir una ventana. Era el trozo del buscador que más
// decidía lo que ve el usuario y el único que no se podía probar.
public static class Ranking
{
    // El tope de cada franja: lo más que puede valer un resultado de ese peldaño.
    public static double TopeDe(Nivel nivel) => nivel switch
    {
        Nivel.Exacta => 1.00,
        Nivel.Prefijo => 0.99,
        Nivel.Palabra => 0.8999,
        Nivel.Aproximada => 0.7699,
        _ => 0,
    };

    // El empujón por costumbre NO puede cambiarte de peldaño.
    //
    // Antes se sumaba tal cual (hasta +0,35) y eso bastaba para que una errata de una
    // app muy usada adelantara a un nombre que empieza por lo escrito. Mal: el peldaño
    // dice cómo de bien encaja lo que has escrito, y eso no lo arregla la costumbre.
    //
    // El primer intento fue repartirlo sobre lo que falta para el 1,0, y **la prueba lo
    // tiró**: con 0,70 y el empujón al máximo salía 0,805, que ya es franja de palabra.
    // Lo que hay que mirar no es el 1,0 sino el techo de SU franja. Así la costumbre
    // desempata dentro del peldaño —que es lo que debe hacer— y no puede subirte uno.
    //
    // Si los puntos ya pasan de ese techo es que la fuente le ha puesto un extra a
    // propósito (el alias exacto de un comando vale 1,3): ahí no se toca nada.
    public static double ConCostumbre(double puntos, double empujon, Nivel nivel)
    {
        double tope = TopeDe(nivel);
        if (puntos >= tope) return puntos;
        return Math.Min(tope, puntos + Math.Max(0, empujon) * (tope - puntos));
    }

    // El orden definitivo. Los puntos mandan —ya traen el peldaño dentro de su franja—
    // y lo demás son desempates para que la lista no baile: con la misma puntuación,
    // dos búsquedas iguales tienen que dar exactamente el mismo orden.
    //
    // `OrderByDescending` de LINQ ya es estable, pero "estable" ahí significa "en el
    // orden en que llegaron", y el orden en que llegan las apps depende de cómo
    // recorrió Windows el menú Inicio. Eso no es estabilidad, es azar repetible.
    public static List<Resultado> Ordenar(IEnumerable<Resultado> encontrados)
    {
        var todos = SinRepetidos(encontrados);

        todos.Sort((a, b) =>
        {
            int c = b.Puntos.CompareTo(a.Puntos);
            if (c != 0) return c;
            c = b.Nivel.CompareTo(a.Nivel);
            if (c != 0) return c;
            c = a.Categoria.CompareTo(b.Categoria);
            if (c != 0) return c;
            c = string.Compare(a.Titulo, b.Titulo, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(a.Clave, b.Clave, StringComparison.Ordinal);
        });

        return Podar(todos);
    }

    // Dos filas que se leen igual son la misma fila. Pasa de verdad: una app puede estar
    // en el menú Inicio y además indexada como archivo .lnk, y una ventana abierta puede
    // coincidir con su propia app. Gana la de más puntos.
    //
    // La identidad es lo que SE VE —categoría, título y detalle— y no la clave: la clave
    // de dos ventanas del mismo programa es la misma a propósito (sirve para contar
    // usos), y colapsarlas escondería ventanas de verdad.
    static List<Resultado> SinRepetidos(IEnumerable<Resultado> encontrados)
    {
        var mejor = new Dictionary<(Categoria, string, string), Resultado>();
        var orden = new List<Resultado>();
        foreach (var r in encontrados)
        {
            var id = (r.Categoria, r.Titulo ?? "", r.Detalle ?? "");
            if (!mejor.TryGetValue(id, out var ya)) { mejor[id] = r; orden.Add(r); }
            else if (r.Puntos > ya.Puntos)
            {
                mejor[id] = r;
                orden[orden.IndexOf(ya)] = r;
            }
        }
        return orden;
    }

    // Con seis filas el relleno se nota: escribiendo "cmd" salía el Símbolo del sistema
    // el primero y detrás cuatro cosas que solo tienen las letras c, m y d desperdigadas
    // por el nombre ("Blackmagic RAW Speed Test"). Bajar el listón del parecido no vale,
    // porque es el mismo que aguanta las erratas ("fotosop" -> Photoshop). Lo que sí
    // vale es mirarlo en relación al mejor: si uno saca mucho más que otro, el otro no
    // es lo que buscabas.
    //
    // El listón lo pone el mejor de los resultados NORMALES: el cálculo y el comando
    // valen 10 a propósito para ir siempre los primeros y no deben subirlo. Y la
    // búsqueda en la web se queda siempre, que es la salida de emergencia.
    static List<Resultado> Podar(List<Resultado> orden)
    {
        double mejor = orden.Where(r => r.Puntos < 2).Select(r => r.Puntos).DefaultIfEmpty(0).Max();
        if (mejor <= 0) return orden;
        double liston = mejor * 0.6;
        return orden.Where(r => r.Puntos >= liston || r.Categoria == Categoria.Web).ToList();
    }
}

// El relevo de las búsquedas que tardan.
//
// Los archivos los busca Windows Search en otro hilo y pueden llegar tarde: si mientras
// tanto has seguido escribiendo, esos resultados son de una consulta que ya no existe y
// mezclarlos es peor que no tenerlos — te salen cosas de "doc" debajo de "documento".
//
// Cancelar el token no basta por sí solo: una tarea puede haber terminado justo antes de
// la cancelación y estar ya en la cola del hilo de interfaz. Por eso cada petición se
// lleva un número, y al volver se pregunta si sigue siendo la última. Es una cuenta de
// tres líneas, pero es la que decide si ves resultados viejos.
public sealed class Relevo
{
    long _ultima;

    public long Pedir() => ++_ultima;

    public bool SigueSiendoLaUltima(long turno) => turno == _ultima;

    // Al cerrar el panel: lo que llegue después ya no vale para nadie.
    public void Olvidar() => _ultima++;
}
