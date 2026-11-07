using System;

namespace NubeBar;

// La guarda que impide que un binario de fase arranque como barra normal.
//
// Existe porque el problema se repitió TRES veces: un `dist-d8` olvidado, un doble clic en el
// binario de entrega, y un `dist-e8` arrancado para recuperar la barra después de que un
// diagnóstico dejara al usuario sin ninguna. Las dos primeras se «arreglaron» con cuidado y
// con un nombre de carpeta disuasorio; el cuidado no escala, y el nombre no se lee cuando lo
// que quieres es tu barra de vuelta.
//
// Una guarda estructural sin prueba se borra sin que nadie se entere.
public static partial class Pruebas
{
    internal static void CorrerFase()
    {
        Grupo("un binario de fase se niega a arrancar como barra");

        // ---------- Las carpetas de desarrollo ----------
        foreach (var r in new[]
        {
            @"C:\x\dist-e8\NubeBar.exe",
            @"C:\x\dist-d6\NubeBar.exe",
            @"C:\x\experimental-NO-EJECUTAR\NubeBar-EXPERIMENTAL-no-ejecutar.exe",
            // Las dos siguientes son el motivo de haber invertido la regla. Con la lista de
            // prefijos («dist-d», «dist-e», «experimental») estas dos NO estaban cubiertas:
            // `dist-f1` es donde se publicó la revisión de los cuatro problemas, y
            // `dist-g0` es la carpeta que todavía no existe.
            @"C:\x\dist-f1\NubeBar.exe",
            @"C:\x\dist-g0\NubeBar.exe",
        })
        {
            Vale(Fase.EsBinarioDeFase(r), $"«{r}» es un binario de fase");
            Vale(Fase.HayQueNegarse(r, Array.Empty<string>()),
                 "y sin banderas se niega a arrancar");
            Vale(Fase.HayQueNegarse(r, null), "ni con argumentos nulos");
        }

        // ---------- Pero las banderas siguen funcionando ----------
        //
        // Negarse del todo rompería los diagnósticos, que son la razón de que esas carpetas
        // existan. Y con bandera no puede escribir: eso ya lo garantiza `Config.PuedeEscribir`.
        foreach (var b in new[] { "--pruebas", "--capturas", "--guia", "--sololectura",
                                  "--vigilia", "--diage0", "--diagmodulos", "--salir", "--guardian" })
            Vale(!Fase.HayQueNegarse(@"C:\x\dist-e8\NubeBar.exe", new[] { b }),
                 $"«{b}» sigue funcionando desde una carpeta de fase");

        // ---------- Y la versión ESTABLE arranca con normalidad ----------
        //
        // Es la mitad que de verdad hay que proteger: si esto fallara, la guarda habría
        // dejado al usuario sin barra, que es justo el accidente que la provocó.
        foreach (var r in new[]
        {
            @"C:\Users\x\Desktop\NubeWin\dist-nuevo\NubeBar.exe",
            @"C:\Program Files\NubeBar\NubeBar.exe",
            @"C:\x\dist\NubeBar.exe",
        })
        {
            Vale(!Fase.EsBinarioDeFase(r), $"«{r}» NO es de fase");
            Vale(!Fase.HayQueNegarse(r, Array.Empty<string>()),
                 "y arranca como barra con normalidad");
        }

        // ---------- Lo que no revienta ----------
        Vale(!Fase.EsBinarioDeFase(null), "una ruta nula no es de fase");
        Vale(!Fase.EsBinarioDeFase(""), "ni una vacía");
        Vale(!Fase.EsBinarioDeFase("NubeBar.exe"), "ni una sin carpeta");

        // Y el mensaje sirve para algo: dice dónde está la barra buena y qué sí se puede
        // ejecutar. Un «no se puede» a secas deja al usuario sin salida.
        Vale(Fase.Explicacion.Contains("dist-nuevo"),
             "el mensaje dice dónde está la barra de siempre");
        Vale(Fase.Explicacion.Contains("--guia"), "y qué sí se puede ejecutar");

        // ---------- Negarse no basta: hay que CERRAR la configuración ----------
        //
        // El cuarto accidente con el config del usuario, y el más instructivo, porque la
        // guarda parecía correcta: se negaba, devolvía 6 y escribía en el log «la
        // configuración no se ha tocado». Y la había migrado de v3 a v4.
        //
        // `Shutdown(6)` no termina el proceso ahí. Pasa por `OnExit` →
        // `CerrarIntegracionDeFondo` → `Config.Cargar()`, y cargar migra. Sin banderas,
        // `Config.SoloLectura` es falso, así que ese camino estaba abierto.
        //
        // Esto fija la regla: en el caso en que la guarda se niega, el config tiene que
        // quedar en solo lectura ANTES de pedir el cierre. Lo que se comprueba es la
        // consecuencia —que con solo lectura puesta, el árbol se niega a guardar el fichero
        // del usuario— porque la alternativa sería arrancar un binario de fase para ver si
        // el config sobrevive, que es justo lo que no se puede hacer.
        bool antes = Config.SoloLectura;
        try
        {
            Config.SoloLectura = true;
            Vale(ConfigArbol.NoSeDebeGuardar(Config.RutaDelUsuario),
                 "con el config en solo lectura, ni OnExit puede escribirlo");

            Config.SoloLectura = false;
            Vale(!ConfigArbol.NoSeDebeGuardar(Config.RutaDelUsuario),
                 "y sin ponerlo, la puerta de OnExit está abierta: por ahí se coló");
        }
        finally { Config.SoloLectura = antes; }

        LaVentanitaSeCierraSola();
    }

    // ================= `--ventanita` y su tope =================
    //
    // Abre una ventana pelada durante los segundos que le digas. Lo que hay que garantizar
    // es que **se cierre sola**: una ventana de pruebas que se queda en el escritorio para
    // siempre es basura, y el número de segundos lo escribe quien la lanza.
    static void LaVentanitaSeCierraSola()
    {
        Grupo("la ventanita de pruebas se cierra sola, diga lo que diga el argumento");

        // Se guarda y se devuelve el estado: estas banderas son estáticas y una prueba no
        // puede dejar el proceso creyendo que hay una ventanita pedida.
        bool encendidoAntes = DiagVentanita.Encendido;
        string tituloAntes = DiagVentanita.Titulo;
        var cuantoAntes = DiagVentanita.Cuanto;
        try
        {
            DiagVentanita.Leer(new[] { "--ventanita", "Hola", "7" });
            Vale(DiagVentanita.Encendido, "con la bandera, se enciende");
            Igual("Hola", DiagVentanita.Titulo, "y toma el título que se le da");
            Igual(7.0, DiagVentanita.Cuanto.TotalSeconds, "y los segundos");

            // EL TOPE. Sin él, un «--ventanita x 999999» deja una ventana para el resto de
            // la sesión. Es de las cosas que uno escribe a mano una vez y se olvida.
            DiagVentanita.Leer(new[] { "--ventanita", "x", "999999" });
            Vale(DiagVentanita.Cuanto <= DiagVentanita.Maximo,
                 $"un número enorme se corta en el tope ({DiagVentanita.Cuanto.TotalMinutes:0} min)");

            // Lo que no es un número, o no es positivo, deja el valor de serie en vez de
            // abrir una ventana de cero segundos o de duración negativa.
            foreach (var basura in new[] { "ocho", "-3", "0", "" })
            {
                DiagVentanita.Cuanto = TimeSpan.FromSeconds(8);
                DiagVentanita.Leer(new[] { "--ventanita", "x", basura });
                Vale(DiagVentanita.Cuanto.TotalSeconds > 0,
                     $"«{basura}» no deja una duración imposible ({DiagVentanita.Cuanto.TotalSeconds:0} s)");
            }

            // Y sin la bandera, no se enciende: es una bandera de diagnóstico, no algo que
            // pueda activarse de rebote.
            DiagVentanita.Encendido = false;
            DiagVentanita.Leer(new[] { "--pruebas" });
            Vale(!DiagVentanita.Encendido, "sin su bandera no se enciende");
            DiagVentanita.Leer(Array.Empty<string>());
            Vale(!DiagVentanita.Encendido, "ni sin argumentos");
        }
        finally
        {
            DiagVentanita.Encendido = encendidoAntes;
            DiagVentanita.Titulo = tituloAntes;
            DiagVentanita.Cuanto = cuantoAntes;
        }
    }
}
