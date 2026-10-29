using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NubeBar;

// E4: el calendario del panel derecho.
//
// ---------- Por qué puro ----------
//
// Un calendario es aritmética con trampas: meses de 28 a 31 días, años bisiestos, y —la que
// más se falla— **qué día empieza la semana**, que no es el mismo en todas partes. En España
// la semana empieza el lunes y en Estados Unidos el domingo, y un calendario que empiece el
// día equivocado coloca mal las cinco semanas enteras.
//
// Siendo puro se puede comprobar febrero de 2024 contra febrero de 2025, un mes que empieza
// en domingo, y los dos criterios de inicio de semana, sin esperar a que llegue esa fecha.
public static class Calendario
{
    // ---------- Un día de la rejilla ----------
    //
    // La rejilla lleva días del mes anterior y del siguiente para completar las semanas: sin
    // ellos, la primera fila queda coja y el día 1 aparece flotando. Van marcados como «de
    // otro mes» para pintarlos atenuados.
    public readonly record struct Dia(DateTime Fecha, bool DeEsteMes, bool EsHoy)
    {
        public int Numero => Fecha.Day;
        public override string ToString() =>
            $"{Numero}{(DeEsteMes ? "" : "·")}{(EsHoy ? "*" : "")}";
    }

    // ---------- El primer día de la semana ----------
    //
    // Sale de la cultura del usuario, no de una constante: es la diferencia entre un
    // calendario que se lee de un vistazo y uno que hay que contar con el dedo.
    public static DayOfWeek PrimerDia(CultureInfo? cultura = null) =>
        (cultura ?? CultureInfo.CurrentCulture).DateTimeFormat.FirstDayOfWeek;

    // Las cabeceras de las columnas, empezando por el día que toque. Dos letras: «lu», «ma»…
    // En una columna de 340 px caben, y el nombre entero no.
    public static List<string> Cabeceras(CultureInfo? cultura = null)
    {
        var c = cultura ?? CultureInfo.CurrentCulture;
        var primero = PrimerDia(c);
        var l = new List<string>();
        for (int i = 0; i < 7; i++)
        {
            var d = (DayOfWeek)(((int)primero + i) % 7);
            var nombre = c.DateTimeFormat.GetAbbreviatedDayName(d);
            l.Add(nombre.Length <= 2 ? nombre : nombre[..2]);
        }
        return l;
    }

    // ---------- La rejilla ----------
    //
    // Siempre semanas completas, y **siempre las que hagan falta**: 4, 5 o 6. Un calendario de
    // seis filas fijas deja una vacía la mayoría de los meses y descoloca el panel entero al
    // cambiar de mes; uno de cinco se come días en los meses que necesitan seis (un mes de 31
    // días que empieza el último día de la semana).
    public static List<Dia> Rejilla(DateTime mes, DateTime hoy, CultureInfo? cultura = null)
    {
        var primeroDelMes = new DateTime(mes.Year, mes.Month, 1);
        var primerDiaSemana = PrimerDia(cultura);

        // Cuántos días hay que retroceder para llegar al inicio de la primera semana.
        int atras = ((int)primeroDelMes.DayOfWeek - (int)primerDiaSemana + 7) % 7;
        var empieza = primeroDelMes.AddDays(-atras);

        int diasDelMes = DateTime.DaysInMonth(mes.Year, mes.Month);
        int total = atras + diasDelMes;
        int semanas = (int)Math.Ceiling(total / 7.0);

        var l = new List<Dia>(semanas * 7);
        for (int i = 0; i < semanas * 7; i++)
        {
            var f = empieza.AddDays(i);
            l.Add(new Dia(f, f.Month == mes.Month && f.Year == mes.Year, f.Date == hoy.Date));
        }
        return l;
    }

    public static int Semanas(DateTime mes, CultureInfo? cultura = null) =>
        Rejilla(mes, mes, cultura).Count / 7;

    // El título del mes, con mayúscula inicial. `ToString("MMMM yyyy")` da «octubre 2026» en
    // español, y en un título queda mejor «Octubre 2026».
    public static string Titulo(DateTime mes, CultureInfo? cultura = null)
    {
        var c = cultura ?? CultureInfo.CurrentCulture;
        var s = mes.ToString("MMMM yyyy", c);
        return s.Length == 0 ? s : char.ToUpper(s[0], c) + s[1..];
    }
}
