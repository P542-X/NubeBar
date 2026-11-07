using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NubeBar;

// El clima, por la parte de la ventana: cuándo se pide y qué se hace con lo que llega.
//
// Las decisiones están en `Clima.cs` y se prueban sin red. Aquí solo queda el cableado, que es
// lo que no se puede probar sin abrir una ventana: el temporizador, el salto al hilo de la
// interfaz y el guardado de las coordenadas.
//
// ---------- Las tres condiciones para que salga algo de este equipo ----------
//
// 1. `Clima.Activo` encendido en Ajustes. Nace apagado.
// 2. Una ciudad escrita que parezca una ciudad (`Clima.CiudadValida`).
// 3. Que no sea un diagnóstico (`Config.SoloLectura`).
//
// La tercera parece de más y no lo es: `--diagpaneles` y compañía construyen los paneles de
// verdad para medirlos, y sin esto cada pasada de cada diagnóstico se habría puesto a pedir el
// tiempo. Una herramienta de medida no tiene por qué hablar con internet.
public partial class MainWindow
{
    DispatcherTimer? _relojDelClima;

    // ---------- Al arrancar ----------
    //
    // Se engancha una vez. El temporizador es de quince minutos y el freno de `Clima` es
    // también de quince: el doble candado es a propósito, porque el panel puede pedirlo
    // también al abrirse y entre los dos no deben pasar de una petición por cuarto de hora.
    void ArrancarClima()
    {
        try
        {
            Clima.Cambio += ClimaCambio;

            _relojDelClima = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _relojDelClima.Tick += (_, _) => PedirElClima();
            _relojDelClima.Start();

            PedirElClima();
        }
        catch (Exception ex) { Log.Error("arrancar el clima", ex); }
    }

    // El evento llega desde el hilo de la petición, no del de la interfaz. Tocar un control
    // de WPF desde ahí revienta, así que se vuelve con `BeginInvoke` — y `Begin` y no
    // `Invoke` porque esto lo dispara código de red: un `Invoke` desde ahí mientras la
    // interfaz espera algo de la red sería un abrazo mortal.
    void ClimaCambio()
    {
        try { Dispatcher.BeginInvoke(new Action(RefrescarDerecho)); } catch { }
    }

    // ---------- Pedirlo ----------
    //
    // `aLaFuerza` se salta el freno de los quince minutos; lo usa el botón de recargar de la
    // tarjeta, que es un gesto explícito del usuario. El repintado del panel nunca lo usa.
    void PedirElClima(bool aLaFuerza = false)
    {
        try
        {
            if (Config.SoloLectura) return;           // un diagnóstico no habla con internet
            if (_cfg?.Clima is not { Activo: true } c) return;
            if (!Clima.CiudadValida(c.Ciudad)) return;

            // Se lee aquí y se pasa por valor: la tarea corre en otro hilo y el config puede
            // recargarse en caliente mientras ella vuela.
            string ciudad = c.Ciudad.Trim();
            double lat = c.Latitud, lon = c.Longitud;

            _ = Task.Run(async () =>
            {
                try
                {
                    // ---------- La ciudad se busca UNA vez ----------
                    //
                    // Sin coordenadas guardadas hay que pasar por el geocodificador. Con
                    // ellas, directo al tiempo: Madrid no se muda, y ahorrarse esa petición
                    // es la mitad del tráfico de esta función.
                    if (lat == 0 && lon == 0)
                    {
                        var sitio = await Clima.BuscarCiudad(ciudad);
                        if (sitio == null)
                        {
                            Log.Info($"clima: no se encuentra «{ciudad}»");
                            return;
                        }
                        lat = sitio.Value.Lat; lon = sitio.Value.Lon;
                        ciudad = sitio.Value.Nombre;
                        GuardarDondeEstamos(ciudad, lat, lon);
                    }

                    await Clima.Refrescar(lat, lon, ciudad, aLaFuerza);
                }
                catch (Exception ex) { Log.Error("pedir el clima", ex); }
            });
        }
        catch (Exception ex) { Log.Error("preparar la petición del clima", ex); }
    }

    // ---------- Guardar lo que contestó el buscador ----------
    //
    // Con `ConfigArbol` y no con el modelo tipado, por lo de siempre en este proyecto: para
    // no perder las claves que NubeBar no conoce del fichero del usuario.
    //
    // Y no hace falta comprobar aquí si se puede escribir: `ConfigArbol.Guardar` ya se niega
    // en redondo cuando hay un diagnóstico en marcha, y esa garantía está en un solo sitio a
    // propósito (ver el comentario de `NoSeDebeGuardar`). Repetirla aquí sería una segunda
    // copia que algún día diría otra cosa.
    void GuardarDondeEstamos(string ciudad, double lat, double lon)
    {
        try
        {
            var arbol = ConfigArbol.Cargar();
            arbol.Poner("Clima", "Ciudad", ciudad);
            arbol.Poner("Clima", "Latitud", lat);
            arbol.Poner("Clima", "Longitud", lon);
            arbol.Guardar();

            // También en memoria, para que el panel que se pinte dentro de un segundo no
            // vuelva a buscar la ciudad mientras el config se relee.
            if (_cfg?.Clima is { } c) { c.Ciudad = ciudad; c.Latitud = lat; c.Longitud = lon; }

            Log.Info($"clima: «{ciudad}» resuelta y guardada");
        }
        catch (Exception ex) { Log.Error("guardar la ciudad del clima", ex); }
    }
}
