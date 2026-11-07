using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NubeBar;

// Red, volumen, idioma del teclado y pila: cada uno como MÓDULO SUELTO.
//
// ---------- Por qué ----------
//
// Los cuatro vivían dentro de «Centro de control», que es un solo módulo. En la barra se ven
// como cuatro iconos, así que uno espera poder moverlos por separado — y no se podía: el
// editor de módulos solo los veía como un bloque. El usuario lo pidió así: «debo poder mover
// todo lo que hay en la nube, tanto ethernet como sonido como lenguaje de teclado, al centro,
// izquierda o derecha».
//
// ---------- Y «Centro de control» no desaparece ----------
//
// Sigue existiendo y sigue montando los cuatro juntos, porque es lo que tiene puesto todo el
// que ya usa NubeBar: quitarlo les dejaría un hueco en la barra sin avisar. Quien quiera
// moverlos por separado los añade sueltos y quita el bloque; quien no, no nota nada.
//
// ---------- Los glifos van por CÓDIGO ----------
//
// Como en todo el proyecto: viven en el área privada de la fuente de iconos y escritos dentro
// de una cadena normal salen como una barra vertical. Están en `Lateral.Glifos` para que la
// prueba que los comprueba contra la fuente los vea — ya hubo una flecha rota por saltarse
// esto, y un botón de apagado donde tenía que haber un escritorio.
public partial class MainWindow
{
    // ---------- Red ----------
    internal UIElement ModuloRed()
    {
        var icono = Glifo(char.ConvertFromUtf32(Lateral.Glifos.Conexion));
        var b = Boton(icono, "Red", () => AbrirDesde(BotonRed));
        _anclaRed = b;

        _actualizadores.Add(() =>
        {
            try
            {
                var tipo = Red.Estado();
                icono.Text = char.ConvertFromUtf32(tipo switch
                {
                    Red.Tipo.Cable => Lateral.Glifos.Conexion,
                    Red.Tipo.Wifi => Lateral.Glifos.Wifi,
                    _ => Lateral.Glifos.SinRed,
                });
                string nombre = Red.NombreDeLaConectada();
                b.ToolTip = tipo == Red.Tipo.Sin
                    ? "Sin conexión"
                    : nombre is { Length: > 0 } ? $"{nombre} · {tipo}" : tipo.ToString();
            }
            catch { }
        });
        return b;
    }

    // ---------- Volumen ----------
    //
    // La rueda sube y baja de 2 en 2 sin abrir nada, que es lo que uno espera de un icono de
    // altavoz en una barra. Y subir con el sonido silenciado lo desilencia: querer más volumen
    // cuando está mudo solo puede querer decir eso.
    internal UIElement ModuloVolumen()
    {
        var icono = Glifo(char.ConvertFromUtf32(Lateral.Glifos.Volumen));
        var b = Boton(icono, "Volumen y sonido", () => AbrirDesde(BotonAltavoz));
        _anclaVolumen = b;

        b.MouseWheel += (_, e) =>
        {
            int v = Audio.Volumen();
            if (v >= 0) Audio.PonerVolumen(v + (e.Delta > 0 ? 2 : -2));
            if (Audio.Silenciado && e.Delta > 0) Audio.Silenciar(false);
            e.Handled = true;
        };

        _actualizadores.Add(() =>
        {
            try
            {
                int v = Audio.Volumen();
                bool mudo = Audio.Silenciado;
                icono.Text = char.ConvertFromUtf32(
                    mudo || v == 0 ? Lateral.Glifos.Silencio
                    : v < 33 ? Lateral.Glifos.VolumenBajo
                    : v < 66 ? Lateral.Glifos.VolumenMedio
                    : Lateral.Glifos.Volumen);
                b.ToolTip = mudo ? "Volumen: silenciado" : $"Volumen: {v}%";
            }
            catch { }
        });
        return b;
    }

    // ---------- Idioma del teclado ----------
    //
    // Solo sale si tienes más de una distribución: con una sola, un indicador que nunca cambia
    // es sitio gastado. Clic pasa a la siguiente (como Win+Espacio) y la rueda va y vuelve.
    internal UIElement ModuloIdioma()
    {
        var texto = new TextBlock
        {
            Foreground = _texto, FontSize = 11, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var b = Boton(texto, "Idioma del teclado", () => Idioma.Siguiente());
        b.MouseWheel += (_, e) => { Idioma.Siguiente(atras: e.Delta > 0); Tick(); e.Handled = true; };
        b.Visibility = Visibility.Collapsed;

        _actualizadores.Add(() =>
        {
            try
            {
                var lista = Idioma.Lista();
                if (lista.Count > 1)
                {
                    var actual = lista.FirstOrDefault(d => d.Hkl == Idioma.Actual());
                    texto.Text = actual?.Corto ?? "··";
                    b.ToolTip = $"Teclado: {actual?.Nombre ?? "?"}\nClic o rueda para cambiar";
                    b.Visibility = Visibility.Visible;
                }
                else b.Visibility = Visibility.Collapsed;
            }
            catch { }
        });
        return b;
    }

    // ---------- La pila, dibujada ----------
    //
    // Distinta del módulo «Batería», que es una etiqueta de texto («BAT 100%»). Éste es el
    // icono con el nivel pintado y el rayo al cargar, y es el que estaba dentro del centro de
    // control. Los dos pueden convivir: uno dice la cifra y el otro se mira de reojo.
    internal UIElement ModuloPila()
    {
        var icono = Glifo(char.ConvertFromUtf32(Lateral.Glifos.Volumen));   // se fija al refrescar
        var b = Boton(icono, "Batería", () => AbrirDesde(BotonBateria));
        _anclaBateria = b;

        _actualizadores.Add(() =>
        {
            try
            {
                var energia = Bateria.Leer();
                b.Visibility = energia.HayBateria ? Visibility.Visible : Visibility.Collapsed;
                if (!energia.HayBateria) return;

                icono.Text = Bateria.Glifo(energia);
                // El color dice lo que pasa: ámbar en ahorro, coral cuando queda poco y no
                // está enchufado. Por debajo del 15 % y sin cable es cuando hay que mirar.
                icono.Foreground = energia.Ahorro ? _roles.Pincel("bateria.ahorro")
                                 : energia.PorCiento <= 15 && !energia.Enchufado
                                   ? _roles.Pincel("bateria.bajo")
                                 : _texto;
                b.ToolTip = $"Batería: {energia.PorCiento}% · {Bateria.Texto(energia)}";
            }
            catch { }
        });
        return b;
    }
}
