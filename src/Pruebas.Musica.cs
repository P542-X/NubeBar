using System;
using System.Linq;

namespace NubeBar;

// Lo que suena, por la parte que se puede comprobar sin música sonando ni ventana abierta:
// quién está reproduciendo, y en qué modos existe el módulo.
//
// Era `Pruebas.Letra.cs` y tenía además el formato LRC, qué verso tocaba en cada segundo y el
// PKCE de Spotify. Las tres cosas que probaban se han ido del programa, así que se han ido de
// aquí: una prueba de algo que ya no existe no protege nada y se tarda en entender por qué
// está.
public static partial class Pruebas
{
    internal static void CorrerMusica()
    {
        QuienReproduceEsSpotify();
        LoQueSuenaSoloEnLaIsla();
    }

    // ================= ¿Quién reproduce? =================
    static void QuienReproduceEsSpotify()
    {
        Grupo("música: quién está reproduciendo");

        Vale(Musica.EsSpotify("Spotify.exe"), "la aplicación de escritorio");
        Vale(Musica.EsSpotify("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"),
             "y la de la Store, que se llama de otra forma");
        Vale(Musica.EsSpotify("spotify.exe"), "sin importar las mayúsculas");

        // ---------- Y el navegador NO ----------
        //
        // `open.spotify.com` en Edge se anuncia como `msedge.exe`, porque quien reproduce es
        // el navegador. Que no cuente es correcto —lo que se pidió fue «lo que suene en
        // Spotify»— pero parece un fallo, así que está escrito aquí: si alguien lo
        // «arregla», esto se cae y le cuenta por qué.
        Vale(!Musica.EsSpotify("msedge.exe"), "Spotify en el navegador no es Spotify: es el navegador");
        Vale(!Musica.EsSpotify("chrome.exe"), "ni Chrome");
        Vale(!Musica.EsSpotify("vlc.exe"), "ni un reproductor cualquiera");
        Vale(!Musica.EsSpotify(null), "ni nada");
    }

    // ================= El módulo solo existe en la isla y en la notch =================
    static void LoQueSuenaSoloEnLaIsla()
    {
        Grupo("música: el módulo solo existe en la isla y en la notch");

        RegistroWidgets.Asegurar();
        var d = RegistroWidgets.Buscar("spotify")?.Descriptor;
        Vale(d != null, "«spotify» está registrado");
        if (d != null)
        {
            Vale(d.ExisteEn("isla"), "existe en modo isla");
            Vale(d.ExisteEn("notch"), "existe en modo notch");
            Vale(!d.ExisteEn("barra"), "NO existe en modo barra");
            Vale(d.ExisteEn("ISLA"), "las mayúsculas del config no lo quitan");

            // ---------- Y lo dice ANTES de que lo añadas ----------
            //
            // Es la razón de que el límite esté en el descriptor y no en un `if` dentro del
            // widget. Puesto y sin aparecer, «lo he añadido y no se ve» significa dos cosas
            // distintas y no hay forma de saber cuál; en la ficha, la biblioteca lo cuenta y
            // `--diagmodulos` no lo apunta como hueco roto.
            Vale(d.SoloEnTexto != null, "explica en qué modos existe");
            Vale(d.Avisos != null && d.Avisos.Length > 20, "avisa, y con una frase, no con una palabra");
        }

        // Un widget de los de siempre sigue existiendo en los tres modos. Es la otra mitad: lo
        // que no se limita no puede quedarse limitado por accidente.
        var reloj = RegistroWidgets.Buscar("reloj")?.Descriptor;
        Vale(reloj != null && PanelNube.Modos.All(reloj.ExisteEn), "el reloj existe en los tres modos");
        Vale(reloj?.SoloEnTexto == null, "y no tiene nada que explicar al respecto");

        // Y la lista de modos de isla sale de `Modos`, así que no puede contradecirla.
        Vale(PanelNube.ModosDeIsla.All(PanelNube.EsIsla), "todos los «modos de isla» son islas");
        Vale(!PanelNube.ModosDeIsla.Contains("barra"), "y la barra no está entre ellos");
        Igual(2, PanelNube.ModosDeIsla.Length, "hoy son dos: isla y notch");
    }
}
