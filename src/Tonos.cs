using System.Windows.Media;

namespace NubeBar;

// Los colores de los azulejos de los paneles.
//
// (Se llama `Tonos` y no `Paleta` porque `Paleta` ya existe y es otra cosa: la que saca los
// colores dominantes de una imagen de fondo. Dos cosas con el mismo nombre en el mismo
// proyecto se confunden una vez y ya no se desconfunden.)
//
// ---------- Por qué hace falta una paleta y no un color por sitio ----------
//
// Los paneles enseñaban seis filas iguales: glifo pequeño, del color del texto, al 80 % de
// opacidad. Eso da una lista. Lo dijo el usuario mirándola: «parece una lista de texto con
// bordes, no un centro de control premium».
//
// Lo que distingue a ojo un centro de control de un menú es que cada cosa tiene su color y se
// reconoce **antes** de leerla. Para eso hace falta una paleta, no un color elegido a mano en
// cada fila: seis elecciones sueltas dan seis azules parecidos pero distintos, que es peor que
// uno solo, porque parece una errata.
//
// ---------- Y por qué no choca con «el acento solo para lo activo» ----------
//
// Esa regla —escrita en el objetivo visual, y de las buenas— habla del AZUL de acento: el azul
// significa «esto está encendido» y no se puede gastar en adornar. Por eso aquí el azul se
// reserva a lo de la casa (ajustes, la nube, la red) y las carpetas van de otros tonos: así
// ver azul sigue queriendo decir algo.
//
// ---------- Lo que NO decide esto ----------
//
// El contraste. Estos son colores de referencia; quien los pinta los pasa por
// `Roles.TintaSobre` contra la superficie de verdad, que es lo que hace que funcionen igual en
// los siete temas y en alto contraste. Una paleta no puede saber sobre qué va a caer.
public static class Tonos
{
    // Elegidos separados en el círculo de color, para que dos filas seguidas no se parezcan, y
    // con saturación suficiente para sobrevivir al rebajado del azulejo.
    public static readonly Color Azul = Color.FromRgb(0x7A, 0xA2, 0xF7);   // la casa: sistema y red
    public static readonly Color Verde = Color.FromRgb(0x56, 0xC5, 0x96);  // documentos, lo correcto
    public static readonly Color Ambar = Color.FromRgb(0xE8, 0xA8, 0x4B);  // descargas, lo que llega
    public static readonly Color Lila = Color.FromRgb(0xA2, 0x8C, 0xF0);   // imágenes y medios
    public static readonly Color Cian = Color.FromRgb(0x4F, 0xC3, 0xD9);   // pantalla y escritorio
    public static readonly Color Coral = Color.FromRgb(0xF0, 0x7B, 0x72);  // avisos y batería baja

    // El azul de la casa sale de `HyprPinta` cuando hay tema delante: así respeta el tema del
    // usuario en vez de imponerle el nuestro. `Azul`, aquí arriba, es solo el de reserva.
    public static Color DeLaCasa(Roles r) => HyprPinta.Azul(r);

    // Para poder comprobarlos todos de una vez — y esta vez con quien la llame (ver
    // `Pruebas.Confirmar`: la lista de glifos existía con ese mismo propósito y no la
    // llamaba nadie).
    public static readonly Color[] Todos = { Azul, Verde, Ambar, Lila, Cian, Coral };
}
