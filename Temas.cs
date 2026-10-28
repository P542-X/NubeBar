using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NubeBar;

// Un tema del usuario, en un fichero.
//
// ---------- Lo que es un tema desde el rediseño ----------
//
// Antes era «tres colores más diez roles». Ahora es **el tema de partida y lo que le has
// cambiado**: `Id` dice de cuál de los nueve sales y `Colores` lleva solo las claves del
// catálogo que has tocado. Es exactamente lo que guarda `config.json`, así que exportar es
// copiar y no traducir — y traducir es donde se pierden valores.
//
// ---------- Y lo que todavía NO es ----------
//
// El formato que pide el plan es un `.nubetema`: un zip con el JSON de valores, las fuentes y
// las imágenes dentro, validado al importarlo (apartado 10 del Estudio de temas, fase 9).
// Esto es el JSON legible y nada más. Se llama distinto a propósito: cuando llegue el
// `.nubetema` no habrá dos cosas peleándose por el mismo nombre.
//
// Al leerlo, lo que no se entienda se ignora y se apunta — lo hace quien lo importa
// (`VentanaAjustes.ImportarTema`): un tema de otra versión, o editado a mano con una errata,
// no puede tumbar la barra.
public sealed record TemaExportado(
    string Nombre,
    string Id,
    Dictionary<string, string>? Colores);

// Guardado y carga de los temas del usuario en %APPDATA%\NubeBar\temas
public static class Temas
{
    public static string Carpeta => Path.Combine(Config.Carpeta, "temas");

    public static void Exportar(TemaExportado tema, string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        ConfigArbol.EscribirAtomico(ruta,
            JsonSerializer.Serialize(tema, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static TemaExportado? Importar(string ruta)
    {
        try { return JsonSerializer.Deserialize<TemaExportado>(File.ReadAllText(ruta)); }
        catch (Exception ex) { Log.Error("importar tema", ex); return null; }
    }
}
