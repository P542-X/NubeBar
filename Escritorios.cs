using System;
using System.Collections.Generic;
using System.Linq;
using WindowsDesktop;

namespace NubeBar;

// Los escritorios virtuales de Windows.
//
// La API de esto no es pública: Microsoft la cambia entre versiones de Windows 11
// y hay que adivinar los CLSID de cada build. Por eso va con la librería
// Slions.VirtualDesktop (MIT), que los mantiene al día, y por eso TODO está
// envuelto en try/catch: si en una actualización deja de reconocer tu Windows,
// el módulo se esconde y NubeBar sigue funcionando igual.
public static class Escritorios
{
    public sealed record Info(Guid Id, string Nombre, int Numero, bool EsElActual);

    static bool? _hay;

    public static bool Disponible
    {
        get
        {
            if (_hay is { } ya) return ya;
            try
            {
                VirtualDesktop.Configure();
                _hay = VirtualDesktop.IsSupported && VirtualDesktop.GetDesktops().Length > 0;
            }
            catch (Exception ex)
            {
                Log.Info($"escritorios virtuales: no disponibles en este Windows ({ex.GetType().Name}), el módulo se esconde");
                _hay = false;
            }
            if (_hay == true) Log.Info($"escritorios virtuales: {Todos().Count} detectados");
            return _hay.Value;
        }
    }

    public static List<Info> Todos()
    {
        var lista = new List<Info>();
        try
        {
            var actual = VirtualDesktop.Current;
            var todos = VirtualDesktop.GetDesktops();
            for (int i = 0; i < todos.Length; i++)
            {
                var d = todos[i];
                string nombre = string.IsNullOrWhiteSpace(d.Name) ? $"Escritorio {i + 1}" : d.Name;
                lista.Add(new Info(d.Id, nombre, i + 1, d.Id == actual?.Id));
            }
        }
        catch (Exception ex) { Log.Error("listar escritorios", ex); }
        return lista;
    }

    public static int Actual()
    {
        try { return Todos().FirstOrDefault(d => d.EsElActual)?.Numero ?? 1; }
        catch { return 1; }
    }

    public static void Ir(Guid id)
    {
        try { VirtualDesktop.FromId(id)?.Switch(); }
        catch (Exception ex) { Log.Error("cambiar de escritorio", ex); }
    }

    // Rueda arriba = el de la izquierda, rueda abajo = el de la derecha.
    // Sin dar la vuelta: en el primero, hacia atrás no hace nada, como en Windows.
    public static void Mover(bool siguiente)
    {
        try
        {
            var vecino = siguiente ? VirtualDesktop.Current?.GetRight() : VirtualDesktop.Current?.GetLeft();
            vecino?.Switch();
        }
        catch (Exception ex) { Log.Error("ir al escritorio vecino", ex); }
    }

    public static void Crear()
    {
        try { VirtualDesktop.Create(); }
        catch (Exception ex) { Log.Error("crear un escritorio", ex); }
    }

    // ¿En qué escritorio vive esta ventana? Para que el carrusel enseñe solo las
    // del actual. Si no se sabe, se dice que sí: más vale enseñar de más que
    // esconder una ventana que existe.
    public static bool EstaEnElActual(IntPtr ventana)
    {
        try { return VirtualDesktop.IsCurrentVirtualDesktop(ventana); }
        catch { return true; }
    }

    // ================= D4: lo que faltaba =================
    //
    // Todo con try/catch y devolviendo si se pudo, por lo mismo que el resto del fichero:
    // la API no es pública y Microsoft le cambia los CLSID entre builds. Un fallo aquí no
    // puede tumbar nada; lo que hace es dejar a NubeHypr en modo de un solo escritorio.

    // ---------- Nombrar ----------
    //
    // `Name` es escribible en esta librería. Se valida antes con `HyprEspacios`, que es
    // quien sabe qué nombre vale, y aquí solo se escribe.
    public static bool Renombrar(Guid id, string nombre)
    {
        if (!HyprEspacios.NombreValido(nombre, out string limpio, out string porque))
        {
            Log.Info($"escritorios: no se renombra ({porque})");
            return false;
        }
        try
        {
            var d = VirtualDesktop.FromId(id);
            if (d == null) { Log.Info($"escritorios: {id} ya no existe"); return false; }
            d.Name = limpio;
            Log.Info($"escritorios: {id} se llama ahora «{limpio}»");
            return true;
        }
        catch (Exception ex) { Log.Error("renombrar un escritorio", ex); return false; }
    }

    // ---------- Ordenar ----------
    //
    // Mover un escritorio a otra posición. El índice es el de la API, empezando en 0.
    public static bool Ordenar(Guid id, int aIndice)
    {
        if (aIndice < 0) return false;
        try
        {
            var d = VirtualDesktop.FromId(id);
            if (d == null) return false;
            d.Move(aIndice);
            Log.Info($"escritorios: {id} movido a la posición {aIndice}");
            return true;
        }
        catch (Exception ex) { Log.Error("reordenar un escritorio", ex); return false; }
    }

    // ---------- Enviar una ventana ----------
    //
    // Y NO se cambia de escritorio detrás de ella: son dos cosas distintas y mezclarlas
    // quita el control. "Manda esto al 2" y "vete al 2" son dos comandos, y el que los
    // junta es el usuario.
    public static bool Enviar(IntPtr ventana, Guid espacio)
    {
        if (ventana == IntPtr.Zero) return false;
        try
        {
            var d = VirtualDesktop.FromId(espacio);
            if (d == null) return false;
            VirtualDesktop.MoveToDesktop(ventana, d);
            return true;
        }
        catch (Exception ex) { Log.Error("enviar una ventana a otro escritorio", ex); return false; }
    }

    // ---------- En qué escritorio está, con su id ----------
    //
    // `EstaEnElActual` contesta sí/no y vale para el carrusel. D4 necesita el id, para
    // poder apuntar el mapa de ventana a espacio. Devuelve `Guid.Empty` si no se sabe —y
    // eso NO es un error: es lo que pasa justo durante un cambio, y el mapa lo trata.
    public static Guid EspacioDe(IntPtr ventana)
    {
        try { return VirtualDesktop.FromHwnd(ventana)?.Id ?? Guid.Empty; }
        catch { return Guid.Empty; }
    }

    // ---------- Lo que ve la política ----------
    //
    // El puente entre la API y `HyprEspacios`: si la API no está o no contesta, devuelve
    // el modo de un solo escritorio en vez de una lista vacía. Así nadie más tiene que
    // preguntar si hay API.
    public static List<HyprEspacios.Espacio> Espacios()
    {
        if (!Disponible) return HyprEspacios.UnSoloEscritorio();
        try
        {
            var crudos = Todos().Select(d => (d.Id, (string?)d.Nombre, d.EsElActual)).ToList();
            if (crudos.Count == 0) return HyprEspacios.UnSoloEscritorio();
            return HyprEspacios.Presentar(crudos);
        }
        catch (Exception ex)
        {
            Log.Error("leer los espacios", ex);
            return HyprEspacios.UnSoloEscritorio();
        }
    }

    // ---------- Crear devolviendo el id ----------
    //
    // `Crear()` no dice cuál creó, y el arnés necesita saberlo para poder borrarlo
    // después. Sin el id, "borrar el de prueba" se convertiría en "borrar el último", y
    // eso es borrar un escritorio del usuario si entre medias creó otro.
    //
    // Crear NO cambia de escritorio: el usuario se queda donde estaba.
    public static Guid CrearYDevolver()
    {
        try
        {
            var d = VirtualDesktop.Create();
            Log.Info($"escritorios: creado {d?.Id}");
            return d?.Id ?? Guid.Empty;
        }
        catch (Exception ex) { Log.Error("crear un escritorio", ex); return Guid.Empty; }
    }

    // ---------- Borrar ----------
    //
    // Borrar un escritorio con ventanas dentro las manda a otro. Así que quien llame tiene
    // que haber vaciado el suyo antes; aquí no se adivina.
    public static bool Borrar(Guid id)
    {
        try
        {
            var d = VirtualDesktop.FromId(id);
            if (d == null) { Log.Info($"escritorios: {id} ya no estaba"); return true; }
            d.Remove();
            Log.Info($"escritorios: borrado {id}");
            return true;
        }
        catch (Exception ex) { Log.Error("borrar un escritorio", ex); return false; }
    }
}
