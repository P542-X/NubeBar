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
}
