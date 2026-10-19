using System;
using System.Globalization;

namespace NubeBar;

// Evaluador de expresiones aritméticas: + - * / ^ % y paréntesis.
// Descenso recursivo, sin dependencias ni eval de nada.
public static class Calculadora
{
    public static double Evaluar(string texto)
    {
        int i = 0;
        // La coma se admite como decimal, igual que la escribes en español
        double v = Suma(texto.Replace(',', '.'), ref i);
        SaltarEspacios(texto, ref i);
        if (i < texto.Length) throw new FormatException("sobra texto");
        return v;
    }

    static double Suma(string s, ref int i)
    {
        double v = Producto(s, ref i);
        while (true)
        {
            SaltarEspacios(s, ref i);
            if (i < s.Length && s[i] == '+') { i++; v += Producto(s, ref i); }
            else if (i < s.Length && s[i] == '-') { i++; v -= Producto(s, ref i); }
            else return v;
        }
    }

    static double Producto(string s, ref int i)
    {
        double v = Potencia(s, ref i);
        while (true)
        {
            SaltarEspacios(s, ref i);
            if (i < s.Length && s[i] == '*') { i++; v *= Potencia(s, ref i); }
            else if (i < s.Length && s[i] == '/') { i++; v /= Potencia(s, ref i); }
            else if (i < s.Length && s[i] == '%') { i++; v %= Potencia(s, ref i); }
            else return v;
        }
    }

    static double Potencia(string s, ref int i)
    {
        double b = Unario(s, ref i);
        SaltarEspacios(s, ref i);
        if (i < s.Length && s[i] == '^') { i++; return Math.Pow(b, Potencia(s, ref i)); }
        return b;
    }

    static double Unario(string s, ref int i)
    {
        SaltarEspacios(s, ref i);
        if (i < s.Length && s[i] == '-') { i++; return -Unario(s, ref i); }
        if (i < s.Length && s[i] == '+') { i++; return Unario(s, ref i); }
        return Atomo(s, ref i);
    }

    static double Atomo(string s, ref int i)
    {
        SaltarEspacios(s, ref i);
        if (i < s.Length && s[i] == '(')
        {
            i++;
            double v = Suma(s, ref i);
            SaltarEspacios(s, ref i);
            if (i >= s.Length || s[i] != ')') throw new FormatException("falta un paréntesis");
            i++;
            return v;
        }

        int inicio = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
        if (i == inicio) throw new FormatException("se esperaba un número");
        return double.Parse(s[inicio..i], CultureInfo.InvariantCulture);
    }

    static void SaltarEspacios(string s, ref int i)
    {
        while (i < s.Length && s[i] == ' ') i++;
    }
}
