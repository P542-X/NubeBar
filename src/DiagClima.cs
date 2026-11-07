using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NubeBar;

// `NubeBar.exe --diagclima [ciudad]`: el clima contra el servicio de verdad, y se va.
//
// ---------- Por qué hace falta, si ya hay pruebas ----------
//
// `Pruebas.Confirmar.cs` comprueba todo lo que se decide SIN red: los códigos de la OMM, el
// freno de los quince minutos, qué ciudad se considera una ciudad. Y eso es lo correcto para
// una prueba, porque una prueba que depende del wifi falla los días que no falla nada.
//
// Pero entonces nada comprueba la mitad que puede romperse de verdad: que la dirección sea la
// buena, que el JSON tenga las claves que este código busca, que `current.temperature_2m`
// siga llamándose así. Eso solo lo contesta una petición de verdad, y por eso es un
// diagnóstico y no una prueba: se ejecuta cuando uno quiere, no en cada compilación.
//
// ---------- Lo que NO hace ----------
//
// No escribe el config. Es una bandera, así que `Config.PuedeEscribir` es falso por
// construcción y `ConfigArbol.Guardar` se niega; pero además es que este diagnóstico ni
// siquiera lee la ciudad del usuario: usa la que se le pase por la línea de órdenes, o Madrid.
// Un diagnóstico que mide con los datos de alguien es un diagnóstico que no se puede repetir.
public static class DiagClima
{
    internal const string Bandera = "--diagclima";

    public static int Correr(string[] args)
    {
        var sb = new StringBuilder();
        int mal = 0;

        void Di(string s) { Console.WriteLine(s); sb.AppendLine(s); }
        void Comprobar(bool ok, string que)
        {
            if (!ok) mal++;
            Di($"    {(ok ? "BIEN" : "MAL ")} · {que}");
        }

        string ciudad = args.Length > 1 && Clima.CiudadValida(args[1]) ? args[1] : "Madrid";

        Di("=== Clima: contra el servicio de verdad ===");
        Di("");
        Di($"Ciudad de prueba: «{ciudad}»");
        Di("Servicio: Open-Meteo (geocoding-api + api.open-meteo.com), sin clave ni registro.");
        Di("Sale de este equipo: el nombre de la ciudad, y después unas coordenadas. Nada más.");
        Di("");

        try
        {
            // ---------- 1. Buscar la ciudad ----------
            Di("--- 1. Buscar la ciudad ---");
            var sitio = Clima.BuscarCiudad(ciudad).GetAwaiter().GetResult();

            if (sitio == null)
            {
                Di("    MAL  · no se encuentra la ciudad (¿sin red?)");
                Di("");
                Di("Sin coordenadas no se puede seguir: el resto del diagnóstico necesita un sitio.");
                return Fin(sb, args, 1);
            }

            var (lat, lon, nombre) = sitio.Value;
            Di($"    devuelve: «{nombre}» en {lat.ToString("0.####", CultureInfo.InvariantCulture)}, " +
               $"{lon.ToString("0.####", CultureInfo.InvariantCulture)}");

            // Que las coordenadas estén en el planeta. Parece una tontería hasta que el
            // servicio cambia el orden de latitud y longitud y nadie se entera.
            Comprobar(lat >= -90 && lat <= 90, "la latitud está entre -90 y 90");
            Comprobar(lon >= -180 && lon <= 180, "la longitud está entre -180 y 180");
            Comprobar(!(lat == 0 && lon == 0),
                      "y no es 0,0 — que es el centinela de «sin resolver», no un sitio");
            Comprobar(!string.IsNullOrWhiteSpace(nombre), "y viene con nombre");

            // ---------- 2. Pedir el tiempo ----------
            Di("");
            Di("--- 2. Pedir el tiempo ---");
            Clima.Refrescar(lat, lon, nombre, aLaFuerza: true).GetAwaiter().GetResult();

            if (Clima.Ultima is not { } l)
            {
                Di($"    MAL  · no hay lectura. Error: {Clima.Error ?? "(ninguno, que es peor)"}");
                return Fin(sb, args, mal + 1);
            }

            Di($"    devuelve: {Clima.Grados(l.Grados)} · {l.Que} · {l.Ciudad}");
            Comprobar(Clima.Error == null, "sin error");

            // ---------- 3. ¿Es un dato o es cualquier cosa? ----------
            //
            // Aquí está el valor de verdad de este diagnóstico. Un JSON que cambie de forma no
            // revienta: `GetDouble` de una clave que falta lanza, y eso se vería; lo que NO se
            // vería es que devolviera 0 por defecto y el panel enseñara «0°» todo el invierno
            // y todo el verano. Así que se exige que el número esté en el rango en el que vive
            // la temperatura del aire en este planeta.
            Di("");
            Di("--- 3. ¿El número es una temperatura? ---");
            Comprobar(l.Grados > -90 && l.Grados < 60,
                      $"{l.Grados} °C está en el rango en el que vive el aire en la Tierra");
            Comprobar(l.Que != "Sin datos",
                      "el código de la OMM que llegó está en la tabla (si no, revisar `Clima.Tiempo`)");
            Comprobar(l.Ciudad == nombre, "la lectura se queda con el nombre de la ciudad buscada");
            Comprobar((DateTime.Now - l.Cuando).TotalSeconds < 60, "y con la hora de ahora");

            // ---------- 4. El glifo ----------
            Di("");
            Di("--- 4. El icono que le toca ---");
            Di($"    código de la OMM traducido a: «{l.Que}», glifo U+{l.Glifo:X4}");
            Comprobar(l.Glifo >= 0xE000 && l.Glifo <= 0xF8FF,
                      "está en la zona de uso privado de la fuente de iconos");

            // ---------- 5. El freno ----------
            //
            // Lo que impide que abrir y cerrar el panel se convierta en una ráfaga de
            // peticiones. Se prueba también en frío, pero aquí se comprueba sobre la lectura
            // que acaba de llegar de verdad.
            Di("");
            Di("--- 5. El freno de los quince minutos ---");
            Comprobar(!Clima.Caducada(Clima.Ultima, DateTime.Now),
                      "recién pedido, NO se vuelve a pedir");
            Comprobar(Clima.Caducada(Clima.Ultima, DateTime.Now.AddMinutes(16)),
                      "y dieciséis minutos después, sí");

            // ---------- 6. Y el config, intacto ----------
            Di("");
            Di("--- 6. Lo que este diagnóstico NO ha tocado ---");
            Comprobar(!Config.PuedeEscribir(args),
                      "con bandera, `Config.PuedeEscribir` es falso: no puede escribir el config");
        }
        catch (Exception ex)
        {
            Di($"    MAL  · reventó: {ex.GetType().Name}: {ex.Message}");
            mal++;
        }

        Di("");
        Di(mal == 0 ? "TODO BIEN" : $"{mal} MAL");
        return Fin(sb, args, mal);
    }

    // El informe a fichero si se pide, como hacen los demás. Un diagnóstico que solo escribe
    // en la consola no se puede adjuntar a nada.
    static int Fin(StringBuilder sb, string[] args, int mal)
    {
        try
        {
            if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
                File.WriteAllText(args[2], sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex) { Console.WriteLine($"no se pudo escribir el informe: {ex.Message}"); }
        return mal == 0 ? 0 : 1;
    }
}
