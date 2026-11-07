using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NubeBar;

// «Nada de colores escritos a mano en la interfaz» (`docs/REDISENO-VISUAL.md`, decisión B,
// punto 3). La prueba que lo vigila.
//
// ---------- Por qué hace falta una prueba y no basta con haberlos quitado ----------
//
// Porque un color escrito a mano no da error, no se ve raro y es lo más cómodo del mundo de
// escribir. Se quitaron veinticinco; el siguiente entra el día que alguien —yo— tenga prisa y
// ponga un `Brushes.Tomato` porque «es solo un aviso». Y cada uno que entra es un color que el
// usuario no puede cambiar, lo cual convierte la promesa de la decisión B en media promesa.
//
// ---------- Qué cuenta como color escrito a mano ----------
//
// `Color.FromRgb(0x16, 0x16, 0x1A)`, `Brushes.Tomato`, `Colors.CornflowerBlue`: un color
// decidido en el código. Eso se convierte en una clave del catálogo.
//
// Lo que NO cuenta, y no es una excepción sino otra cosa:
//
//   - `Color.FromArgb(0x2A, color.R, color.G, color.B)` — los canales vienen de una variable.
//     No es un color: es una OPACIDAD aplicada a un color que ya venía del tema.
//   - `Brushes.Transparent` — es estructural: sin `Background` puesto, un `Panel` de WPF no
//     recibe el ratón en sus huecos. No se pinta nada; se declara que ahí se puede pulsar.
//   - Los diagnósticos (`MainWindow.Diag*.cs`) — miden contraste CONTRA blanco, negro y gris
//     medio. Esos tres no son paleta: son la vara de medir.
//
// ---------- Y la salida de emergencia ----------
//
// Una línea con el comentario `// color literal a propósito` se salta. No es un agujero: es
// la obligación de escribir por qué. Las que lo llevan hoy son máscaras de degradado, los
// valores de reserva de un color que elige el usuario en un widget, y un pincel que no se
// pinta (medir el ancho de un texto necesita uno, y da igual cuál).
//
// ---------- Lo que esta prueba no puede hacer ----------
//
// Leerse el código desde un exe publicado, porque allí no hay código. Así que busca las
// fuentes hacia arriba desde el ejecutable; si no las encuentra, lo dice en el informe y no
// falla. Donde importa —esta máquina, donde se escribe cada cambio y se pasa `--pruebas`
// antes de publicar— están siempre.
public static partial class Pruebas
{
    // Los ficheros que la decisión B señala: la interfaz. El resto del programa (el extractor
    // de paleta, el selector de color, `Contraste`) trabaja CON colores, y prohibirle
    // nombrarlos no tendría sentido.
    static readonly string[] EnLaMira = { "MainWindow*.cs", "VentanaAjustes*.cs", "Biblioteca.cs" };

    const string Perdon = "color literal a propósito";

    // Un color con nombre que sí se puede escribir. `Transparent` no pinta: declara que ahí
    // se puede pulsar.
    static readonly string[] NombresQueValen = { "Transparent" };

    internal static void SinColoresAMano()
    {
        Grupo("fase 0: ningún color escrito a mano en la interfaz");

        var raiz = RaizDelCodigo();
        if (raiz is null)
        {
            // No es un fallo: es un exe publicado sin su código al lado. Queda escrito en el
            // informe para que nadie lea «todo bien» creyendo que esto se comprobó.
            _informe.AppendLine("    (no se comprueba: no hay código fuente junto al ejecutable)");
            return;
        }

        var sueltos = new List<string>();
        int mirados = 0;

        // Recursivo desde que el codigo vive en `src/`. Con la busqueda plana esto
        // encontraba CERO ficheros el dia que se movieron, y lo unico que avisaba era el
        // `mirados >= 20` de abajo. Una guarda que se queda sin nada que mirar no dice
        // «todo bien»: dice que ha dejado de mirar, y eso tiene que notarse.
        foreach (var patron in EnLaMira)
            foreach (var ruta in Directory.EnumerateFiles(raiz, patron, SearchOption.AllDirectories))
            {
                var nombre = Path.GetFileName(ruta);
                if (nombre.StartsWith("MainWindow.Diag", StringComparison.OrdinalIgnoreCase)) continue;
                mirados++;

                var lineas = File.ReadAllLines(ruta);
                for (int i = 0; i < lineas.Length; i++)
                {
                    var l = lineas[i];
                    if (l.Contains(Perdon)) continue;
                    if (l.TrimStart().StartsWith("//")) continue;      // un comentario no pinta

                    foreach (var hallado in ColoresDe(l))
                        sueltos.Add($"{nombre}:{i + 1}  {hallado}");
                }
            }

        Vale(mirados >= 20, $"se ha mirado la interfaz entera ({mirados} ficheros)");

        foreach (var s in sueltos)
            Vale(false, $"color escrito a mano: {s} — dale una clave en Catalogo.cs");

        Vale(sueltos.Count == 0,
             $"la interfaz no decide ni un color por su cuenta ({sueltos.Count} sueltos)");
    }

    // ---------- Encontrar los colores de una línea ----------

    // `Brushes.Algo` y `Colors.Algo`, salvo los que valen.
    static readonly Regex ConNombre = new(@"\b(?:Brushes|Colors)\.([A-Z]\w*)", RegexOptions.Compiled);

    // Un argumento que es un número y nada más: `0x16`, `255`, `(byte)0x30`.
    static readonly Regex SoloNumero =
        new(@"^\s*(?:\(byte\)\s*)?(?:0[xX][0-9A-Fa-f]+|\d+)\s*$", RegexOptions.Compiled);

    static IEnumerable<string> ColoresDe(string linea)
    {
        foreach (Match m in ConNombre.Matches(linea))
            if (!NombresQueValen.Contains(m.Groups[1].Value))
                yield return m.Value;

        foreach (var (llamada, args) in Llamadas(linea, "Color.FromRgb", "Color.FromArgb"))
        {
            // Si TODOS los argumentos son números, es un color decidido aquí. Si alguno viene
            // de una variable (`color.R`, `_roles.Acento.G`), es una opacidad sobre un color
            // que ya venía del tema, y eso sí se puede escribir.
            var partes = PartirArgumentos(args);
            if (partes.Count >= 3 && partes.All(a => SoloNumero.IsMatch(a)))
                yield return llamada + "(" + string.Join(", ", partes.Select(a => a.Trim())) + ")";
        }
    }

    // Las llamadas a esos nombres con su lista de argumentos en crudo, contando paréntesis
    // para no cortar por una coma que esté dentro de otra llamada.
    static IEnumerable<(string Nombre, string Args)> Llamadas(string linea, params string[] nombres)
    {
        foreach (var n in nombres)
        {
            int desde = 0;
            while (true)
            {
                int i = linea.IndexOf(n + "(", desde, StringComparison.Ordinal);
                if (i < 0) break;
                int ab = i + n.Length;
                int hondo = 0, j = ab;
                for (; j < linea.Length; j++)
                {
                    if (linea[j] == '(') hondo++;
                    else if (linea[j] == ')') { hondo--; if (hondo == 0) break; }
                }
                if (hondo != 0) break;                       // la llamada sigue en la línea siguiente
                yield return (n, linea[(ab + 1)..j]);
                desde = j;
            }
        }
    }

    static List<string> PartirArgumentos(string args)
    {
        var fuera = new List<string>();
        int hondo = 0, desde = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == '(') hondo++;
            else if (args[i] == ')') hondo--;
            else if (args[i] == ',' && hondo == 0) { fuera.Add(args[desde..i]); desde = i + 1; }
        }
        if (args.Length > 0) fuera.Add(args[desde..]);
        return fuera;
    }

    // La carpeta del proyecto, subiendo desde el ejecutable hasta encontrar el .csproj.
    // `Environment.ProcessPath` y no `AppContext.BaseDirectory`: con el exe de un solo
    // archivo, el segundo apunta a la carpeta de extracción en %TEMP%.
    static string? RaizDelCodigo()
    {
        try
        {
            var d = new FileInfo(Environment.ProcessPath ?? "").Directory;
            for (int i = 0; i < 6 && d != null; i++, d = d.Parent)
                if (d.EnumerateFiles("NubeBar.csproj").Any()) return d.FullName;
        }
        catch { }
        return null;
    }
}
