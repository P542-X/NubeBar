using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NubeBar;

public enum Categoria { Calculo, Comando, App, Ventana, Ajuste, Archivo, Web }

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

    // 0 = no se parece en nada, 1 = clavado. Mezcla tres criterios porque cada uno
    // falla donde el otro acierta: prefijo, subsecuencia y distancia de edición.
    public static double Puntuar(string consultaNorm, string textoOriginal)
    {
        if (consultaNorm.Length == 0) return 0;
        var texto = Normalizar(textoOriginal);
        if (texto.Length == 0) return 0;

        if (texto.StartsWith(consultaNorm, StringComparison.Ordinal)) return 1.0;
        if (texto.Contains(consultaNorm, StringComparison.Ordinal)) return 0.9;

        // Iniciales de las palabras: "vsc" -> "Visual Studio Code"
        var palabras = texto.Split(new[] { ' ', '-', '_', '.', '(', ')', '[', ']' },
                                   StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length > 1)
        {
            var iniciales = new string(palabras.Select(p => p[0]).ToArray());
            if (iniciales.StartsWith(consultaNorm, StringComparison.Ordinal)) return 0.88;
        }
        // Alguna palabra empieza por la consulta
        if (palabras.Any(p => p.StartsWith(consultaNorm, StringComparison.Ordinal))) return 0.85;

        // Subsecuencia en orden: "vscode" dentro de "visual studio code"
        double sub = Subsecuencia(consultaNorm, texto);
        // Erratas: comparar con el texto entero y con cada palabra, y quedarse con lo mejor
        double edicion = Similitud(consultaNorm, texto);
        foreach (var p in palabras) edicion = Math.Max(edicion, Similitud(consultaNorm, p));

        return Math.Max(sub, edicion);
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
