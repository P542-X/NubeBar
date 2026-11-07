using System;

namespace NubeBar;

// `NubeBar.exe --diagpaneles`: ningún panel se corta, en ninguna posición de la barra.
//
// ---------- De dónde sale ----------
//
// El panel «Red e Internet» salía con la tarjeta de acciones cortada por abajo: cinco
// botones de los que ciento once píxeles caían por debajo del borde de la isla, sin
// desplazamiento que los alcanzara. La causa era una medida que se contestaba a sí misma
// bajo una fila `Star`, y se arregló.
//
// Lo que quedó sin contestar es la otra mitad de la pregunta: **¿y los demás?**. Se
// comprobaron a ojo con la barra arriba y en este monitor, y en la lista de pruebas
// manuales quedaron anotados los casos que no pude provocar: la barra abajo, la barra
// vertical, y una pantalla donde el panel ya no cabe y tiene que aparecer la rueda.
//
// Esto los recorre todos solo. Para cada posición de la barra abre cada panel por la misma
// puerta que los abre el usuario, espera a que estén quietos y mira si queda algún control
// por debajo del borde —y si queda, si hay desplazamiento que lo rescate—.
//
// ---------- La regla que comprueba ----------
//
//     o el panel cabe entero, o hay desplazamiento interno.
//
// Un control fuera del borde SIN rueda es un botón que nadie puede pulsar. Con rueda es
// correcto: el menú Inicio mide 1236 px y nunca va a caber en una pantalla de 1440 con la
// barra puesta.
//
// Solo mira. No escribe la configuración —es una bandera— y lo único que toca es la
// orientación de la barra EN MEMORIA, que devuelve al terminar.
public static class DiagPaneles
{
    internal const string Bandera = "--diagpaneles";

    public static bool Encendido;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], Bandera, StringComparison.OrdinalIgnoreCase))
            return;
        Encendido = true;
    }
}
