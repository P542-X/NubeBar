using System;
using System.Globalization;
using System.Linq;

namespace NubeBar;

// E4: las pruebas del calendario.
//
// Un calendario es aritmética con trampas: meses de 28 a 31 días, años bisiestos y —la que
// más se falla— qué día empieza la semana. Siendo puro se puede comprobar febrero de 2024
// contra el de 2025, y los dos criterios de inicio de semana, sin esperar a esas fechas.
public static partial class Pruebas
{
    internal static void CorrerE4()
    {
        E4LaRejillaDelMes();
        E4ElPrimerDiaDeLaSemana();
        E4LosMesesDifíciles();
        E4ElPanelDerecho();
    }

    static readonly CultureInfo Es = new("es-ES");   // la semana empieza en lunes
    static readonly CultureInfo En = new("en-US");   // la semana empieza en domingo

    // ================= La rejilla =================
    static void E4LaRejillaDelMes()
    {
        Grupo("E4: la rejilla del calendario");

        var oct = new DateTime(2026, 10, 15);
        var r = Calendario.Rejilla(oct, oct, Es);

        Igual(0, r.Count % 7, "la rejilla tiene semanas completas");
        Vale(r.Count / 7 is >= 4 and <= 6, "entre cuatro y seis semanas, nunca más");

        // Todos los días del mes están, una sola vez.
        int diasDelMes = DateTime.DaysInMonth(2026, 10);
        Igual(diasDelMes, r.Count(d => d.DeEsteMes), "están todos los días del mes");
        Igual(diasDelMes, r.Where(d => d.DeEsteMes).Select(d => d.Numero).Distinct().Count(),
              "y ninguno repetido");

        // Y en orden, sin saltos: un calendario con un día de más o de menos descoloca el
        // resto de la fila y no se nota hasta que cuentas.
        for (int i = 1; i < r.Count; i++)
            Vale(r[i].Fecha == r[i - 1].Fecha.AddDays(1),
                 $"el día {i} va justo detrás del anterior");

        // ---------- Hoy, marcado una sola vez ----------
        Igual(1, r.Count(d => d.EsHoy), "hoy sale marcado exactamente una vez");
        Vale(r.Single(d => d.EsHoy).Fecha.Date == oct.Date, "y es el día que toca");

        // Si «hoy» es de otro mes, no se marca nada: mirar noviembre no puede pintar un día
        // de octubre como si fuera hoy.
        var otro = Calendario.Rejilla(new DateTime(2026, 12, 1), oct, Es);
        Igual(0, otro.Count(d => d.EsHoy && d.DeEsteMes),
              "mirando otro mes, ningún día de ESE mes es hoy");

        // ---------- Los de relleno ----------
        //
        // La rejilla lleva días del mes anterior y del siguiente para completar las semanas.
        // Sin ellos la primera fila queda coja y el día 1 aparece flotando.
        Vale(r.Any(d => !d.DeEsteMes) || r.Count == diasDelMes,
             "hay días de relleno, o el mes encaja justo");
        foreach (var d in r.Where(d => !d.DeEsteMes))
            Vale(d.Fecha.Month != 10, "los de relleno son de otro mes de verdad");

        // ---------- Primeros y últimos de mes, que es donde se ve el relleno ----------
        //
        // Estaba en la lista de pruebas manuales —«el calendario a primeros y últimos de mes,
        // donde se ven los días de relleno»— y es de las que sí se pueden exigir: lo que hay
        // que comprobar es aritmética de fechas, no si queda bonito.
        //
        // Los doce meses de dos años, uno bisiesto. Con uno solo se prueban doce comienzos de
        // semana; con dos se cubren todos los posibles, que es donde una rejilla mal cuadrada
        // se rompe.
        foreach (int anio in new[] { 2026, 2028 })          // 2028 es bisiesto
            for (int mes = 1; mes <= 12; mes++)
            {
                var primero = new DateTime(anio, mes, 1);
                var ultimo = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
                var rr = Calendario.Rejilla(primero, primero, Es);

                Igual(0, rr.Count % 7, $"{anio}-{mes:00}: semanas completas");

                // El día 1 y el último tienen que estar, y una sola vez. Es justo lo que
                // falla cuando el relleno se cuenta mal: el 1 se cae de la primera fila, o
                // el último se duplica en la siguiente.
                Igual(1, rr.Count(d => d.DeEsteMes && d.Numero == 1),
                      $"{anio}-{mes:00}: el día 1 sale una vez");
                Igual(1, rr.Count(d => d.DeEsteMes && d.Numero == ultimo.Day),
                      $"{anio}-{mes:00}: el día {ultimo.Day} sale una vez");

                // Y el relleno cae fuera del mes de verdad: uno del mes equivocado se dibuja
                // igual de gris y no lo nota nadie.
                foreach (var d in rr.Where(x => !x.DeEsteMes))
                    Vale(d.Fecha < primero || d.Fecha > ultimo,
                         $"{anio}-{mes:00}: el relleno cae fuera del mes");

                // Y empieza en el primer día de la semana: si no, la cabecera de días no
                // cuadra con las columnas y todo el mes sale corrido una casilla.
                Vale(rr[0].Fecha.DayOfWeek == Calendario.PrimerDia(Es),
                     $"{anio}-{mes:00}: empieza en el primer día de la semana");
            }

        // ---------- El título ----------
        var t = Calendario.Titulo(oct, Es);
        Vale(t.Contains("2026"), "el título lleva el año");
        Vale(char.IsUpper(t[0]), "y empieza por mayúscula");
    }

    // ================= El primer día de la semana =================
    static void E4ElPrimerDiaDeLaSemana()
    {
        Grupo("E4: la semana empieza donde dice la cultura");

        // El fallo clásico: un calendario que empieza el día equivocado coloca mal las cinco
        // semanas enteras, y no se ve hasta que buscas una fecha concreta.
        Igual(DayOfWeek.Monday, Calendario.PrimerDia(Es), "en España, lunes");
        Igual(DayOfWeek.Sunday, Calendario.PrimerDia(En), "en Estados Unidos, domingo");

        var oct = new DateTime(2026, 10, 1);
        var esp = Calendario.Rejilla(oct, oct, Es);
        var usa = Calendario.Rejilla(oct, oct, En);

        Igual(DayOfWeek.Monday, esp[0].Fecha.DayOfWeek, "la rejilla española empieza en lunes");
        Igual(DayOfWeek.Sunday, usa[0].Fecha.DayOfWeek, "y la americana en domingo");

        // Las cabeceras siguen el mismo criterio, o el calendario diría una cosa y pintaría
        // otra.
        var ces = Calendario.Cabeceras(Es);
        var cus = Calendario.Cabeceras(En);
        Igual(7, ces.Count, "siete cabeceras");
        Igual(7, cus.Count, "en las dos culturas");
        Vale(ces.All(c => c.Length is > 0 and <= 2), "de dos letras: en 340 px no cabe más");
        Vale(ces[0] != cus[0], "y la primera columna no es la misma en las dos");

        // Todas las columnas son días distintos: una cabecera repetida sería un día perdido.
        Igual(7, ces.Distinct(StringComparer.CurrentCultureIgnoreCase).Count(),
              "las siete cabeceras son distintas");
    }

    // ================= Los meses difíciles =================
    static void E4LosMesesDifíciles()
    {
        Grupo("E4: febrero, los bisiestos y los meses de seis semanas");

        // ---------- Febrero bisiesto y no bisiesto ----------
        Igual(29, Calendario.Rejilla(new DateTime(2024, 2, 1), DateTime.Now, Es)
                            .Count(d => d.DeEsteMes), "febrero de 2024 tiene 29 días");
        Igual(28, Calendario.Rejilla(new DateTime(2025, 2, 1), DateTime.Now, Es)
                            .Count(d => d.DeEsteMes), "y el de 2025, 28");

        // 2000 fue bisiesto y 1900 no: la regla de los siglos, que se falla a menudo.
        Igual(29, Calendario.Rejilla(new DateTime(2000, 2, 1), DateTime.Now, Es)
                            .Count(d => d.DeEsteMes), "2000 fue bisiesto");
        Igual(28, Calendario.Rejilla(new DateTime(1900, 2, 1), DateTime.Now, Es)
                            .Count(d => d.DeEsteMes), "y 1900 no");

        // ---------- Un febrero de cuatro semanas justas ----------
        //
        // 28 días empezando en lunes: cuatro filas exactas, sin un solo día de relleno. Es el
        // único caso en el que la rejilla no lleva relleno, y conviene que no se rompa.
        var feb2021 = new DateTime(2021, 2, 1);
        if (feb2021.DayOfWeek == DayOfWeek.Monday)
        {
            var r = Calendario.Rejilla(feb2021, DateTime.Now, Es);
            Igual(28, r.Count, "un febrero que empieza en lunes cabe en cuatro semanas");
            Vale(r.All(d => d.DeEsteMes), "y no necesita ni un día de relleno");
        }

        // ---------- Los de seis semanas ----------
        //
        // Un mes de 31 días que empieza el último día de la semana necesita seis filas. Un
        // calendario de cinco fijas se comería días.
        int conSeis = 0;
        for (int anno = 2020; anno <= 2030; anno++)
            for (int mes = 1; mes <= 12; mes++)
                if (Calendario.Semanas(new DateTime(anno, mes, 1), Es) == 6) conSeis++;
        Vale(conSeis > 0, "hay meses que necesitan seis semanas");

        // ---------- Y ninguno se pierde un día, en once años ----------
        //
        // La comprobación que de verdad vale: 132 meses, dos culturas, y en todos la rejilla
        // contiene exactamente los días del mes.
        foreach (var cultura in new[] { Es, En })
            for (int anno = 2020; anno <= 2030; anno++)
                for (int mes = 1; mes <= 12; mes++)
                {
                    var r = Calendario.Rejilla(new DateTime(anno, mes, 1), DateTime.Now, cultura);
                    int esperados = DateTime.DaysInMonth(anno, mes);
                    if (r.Count(d => d.DeEsteMes) != esperados || r.Count % 7 != 0)
                        Vale(false, $"{anno}-{mes:00} ({cultura.Name}): la rejilla no cuadra");
                }
        Vale(true, "132 meses y dos culturas: ninguna rejilla pierde ni repite un día");

        // ---------- Los extremos del calendario ----------
        Vale(Calendario.Rejilla(DateTime.MinValue.AddDays(40), DateTime.Now, Es).Count > 0,
             "el primer mes representable no revienta");
        Vale(Calendario.Rejilla(new DateTime(9999, 11, 1), DateTime.Now, Es).Count > 0,
             "ni uno del año 9999");
    }

    // ================= El panel derecho =================
    static void E4ElPanelDerecho()
    {
        Grupo("E4: lo que lleva el panel derecho");

        var der = Lateral.De(Lateral.Lado.Derecha);
        foreach (var id in new[] { "medios", "avisos", "calendario", "escritorio", "conexion" })
            Vale(der.Any(s => s.Id == id), $"el panel derecho lleva «{id}»");

        // ---------- El clima ----------
        //
        // Aquí decía «y NO lleva clima: no hay proveedor real, y una maqueta no cuenta», y el
        // comentario terminaba con «si algún día hay proveedor, esta prueba es la que hay que
        // cambiar». Ese día llegó: hay proveedor (Open-Meteo, gratis y sin registro) y el
        // usuario lo ha pedido. Así que se cambia, que es lo que estaba previsto, en vez de
        // dejarla ahí exigiendo lo contrario de lo que el programa hace ahora.
        //
        // Lo que se exige en su lugar NO es «hay una tarjeta de clima»: eso lo cumpliría
        // igual de bien un sol dibujado, que es justo lo que la prueba vieja impedía. Se
        // exige lo que de verdad importaba de aquella decisión y sigue importando: **que no
        // salga nada de la máquina del usuario sin que él lo haya pedido dos veces**.
        Vale(der.Any(s => s.Titulo.Contains("Clima")), "el panel derecho lleva el clima");

        Vale(!new OpcionesClima().Activo,
             "pero nace APAGADO: enseñar el tiempo implica hablar con un servidor de otro");
        Vale(!Clima.CiudadValida(new OpcionesClima().Ciudad),
             "y sin ciudad escrita no hay petición que hacer, ni encendido");

        // El reloj no es una sección aparte: está en la barra y en el calendario. Dos sitios
        // enseñando la hora en la misma pantalla es ruido.
        Vale(!der.Any(s => s.Id == "reloj"),
             "ni un reloj aparte: ya está en la barra y en el calendario");
    }
}
