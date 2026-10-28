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
    Dictionary<string, string>? Colores,
    Dictionary<string, double>? Radios = null,
    Dictionary<string, string>? Letras = null);

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

    // ---------- Los temas que te has guardado ----------
    //
    // Cada uno es un fichero en la carpeta de temas. Se listan por nombre y lo que no se
    // entienda se ignora en silencio: un fichero roto ahí no puede impedir que salgan los
    // demás, y menos aún que arranque la barra.
    //
    // Los que empiezan por `_` no se listan: son de uso interno, como el `_antes-del-claro`
    // que guarda la acción rápida de tema claro antes de cambiarlo.
    public static List<(string Fichero, TemaExportado Tema)> Mios()
    {
        var lista = new List<(string, TemaExportado)>();
        try
        {
            if (!Directory.Exists(Carpeta)) return lista;
            foreach (var f in Directory.EnumerateFiles(Carpeta, "*.json"))
            {
                if (Path.GetFileName(f).StartsWith("_", StringComparison.Ordinal)) continue;
                if (Importar(f) is { } t && !string.IsNullOrWhiteSpace(t.Nombre)) lista.Add((f, t));
            }
        }
        catch (Exception ex) { Log.Error("listar tus temas", ex); }
        lista.Sort((a, b) => string.Compare(a.Item2.Nombre, b.Item2.Nombre,
                                            StringComparison.CurrentCultureIgnoreCase));
        return lista;
    }

    // Un nombre de fichero que valga, a partir del que escriba el usuario. A nadie se le pide
    // que escriba un nombre de fichero: se le pide un nombre, y esto lo traduce.
    public static string RutaPara(string nombre)
    {
        var limpio = new string(Array.FindAll(nombre.ToCharArray(),
            c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')).Trim();
        if (limpio.Length == 0) limpio = "Mi tema";
        if (limpio.Length > 48) limpio = limpio[..48];
        return Path.Combine(Carpeta, limpio + ".json");
    }

    public static bool Borrar(string ruta)
    {
        try { File.Delete(ruta); return true; }
        catch (Exception ex) { Log.Error("borrar un tema tuyo", ex); return false; }
    }
}
