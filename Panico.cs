using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// Cuándo un fallo deja de ser «un módulo tonto» y pasa a ser «hay que devolverle el
// escritorio al usuario».
//
// ---------- Por qué hace falta ----------
//
// NubeBar esconde la barra de tareas de Windows. Mientras funciona, eso está bien. Si se
// rompe y se queda a medias, el usuario se encuentra **sin barra de tareas y sin NubeBar**:
// ni menú de inicio, ni reloj, ni bandeja. Es el peor estado posible, y es peor que cerrarse.
//
// La red que había cubría medio caso: `Guardian` devuelve el shell si matan el proceso, y el
// manejador de excepciones llamaba a `MostrarBarraWindows`. Pero eso enseña la barra de
// Windows y deja NubeBar a medio morir — con la bandeja secuestrada, los ganchos puestos y la
// reserva de pantalla sin soltar. Lo que pedía el usuario es otra cosa: «que estos activen un
// exit que haga que al usuario se le active la barra de tareas normal, igual que salirse de
// NubeBar». O sea, la salida de verdad, no un parche.
//
// ---------- Y por qué la decisión vive aquí ----------
//
// Porque «¿esto es grave?» es una decisión, y las decisiones se prueban. Metida dentro del
// manejador de excepciones solo se podría comprobar provocando un fallo real, que es
// justamente lo que no se puede hacer en una prueba.
public static class Panico
{
    // ---------- Qué cuenta como fallo ----------
    public enum Clase
    {
        Interfaz,   // una excepción pintando: casi siempre un módulo, no la aplicación
        Tarea,      // una tarea de fondo que nadie miró
        Fondo,      // excepción sin capturar en un hilo: el proceso se va a pique
        Arranque,   // revienta al montar la barra
    }

    public readonly record struct Apunte(Clase Que, string Donde, DateTime Cuando);

    // ---------- Las dos reglas ----------
    //
    // 1. Una excepción sin capturar en un hilo de fondo mata el proceso quiera uno o no. No
    //    hay nada que deliberar: hay que soltar el escritorio ANTES de irse.
    //
    // 2. Lo demás se perdona una vez y se vigila. Un módulo que falla al pintar una vez es un
    //    módulo tonto; el MISMO fallo repitiéndose varias veces en pocos segundos es una
    //    barra que no se puede dibujar, y una barra que no se dibuja ya ha dejado al usuario
    //    sin escritorio aunque el proceso siga vivo.
    //
    // Se cuenta por SITIO (`Donde`), no en total: tres módulos distintos fallando una vez cada
    // uno es un mal día, pero el mismo fallando cinco veces seguidas es un bucle.
    public const int RepeticionesParaRendirse = 5;
    public static readonly TimeSpan Ventana = TimeSpan.FromSeconds(20);

    public static bool EsGraveDeEntrada(Clase que) =>
        que is Clase.Fondo or Clase.Arranque;

    // ¿Estos apuntes, mirados en este instante, dicen que hay que rendirse?
    //
    // `historial` llega entero y se filtra aquí: quien llama no tiene por qué saber cuál es la
    // ventana de tiempo, y así cambiarla no obliga a tocar el código que apunta.
    public static bool HayQueRendirse(IEnumerable<Apunte> historial, DateTime ahora)
    {
        var recientes = historial.Where(a => ahora - a.Cuando <= Ventana).ToList();
        if (recientes.Any(a => EsGraveDeEntrada(a.Que))) return true;

        // El mismo sitio, repitiéndose. `Donde` es la etiqueta que pasa quien apunta —el
        // nombre del módulo, de la sección o de la operación—, no el mensaje de la excepción:
        // el mensaje cambia con los datos y entonces cada repetición parecería distinta.
        return recientes
            .Where(a => !string.IsNullOrEmpty(a.Donde))
            .GroupBy(a => a.Donde, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() >= RepeticionesParaRendirse);
    }

    // Lo que se escribe en el registro al rendirse. Tiene que decir POR QUÉ, porque es la
    // última línea que alguien va a leer para entender qué pasó.
    public static string Motivo(IEnumerable<Apunte> historial, DateTime ahora)
    {
        var recientes = historial.Where(a => ahora - a.Cuando <= Ventana).ToList();

        var grave = recientes.FirstOrDefault(a => EsGraveDeEntrada(a.Que));
        if (grave.Donde != null && EsGraveDeEntrada(grave.Que))
            return $"{grave.Que} en «{grave.Donde}»: el proceso no sobrevive a esto";

        var bucle = recientes
            .Where(a => !string.IsNullOrEmpty(a.Donde))
            .GroupBy(a => a.Donde, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() >= RepeticionesParaRendirse);

        return bucle != null
            ? $"«{bucle.Key}» ha fallado {bucle.Count()} veces en {Ventana.TotalSeconds:0} s"
            : "sin motivo claro";
    }

    // ---------- Cuánto se espera a que la salida limpia termine ----------
    //
    // Al rendirse se intenta la MISMA salida que al cerrar NubeBar a mano: devolver la
    // bandeja, soltar los ganchos, liberar la reserva de pantalla y enseñar la barra de
    // Windows. Pero si la aplicación está rota, esa salida puede colgarse — y entonces el
    // usuario se queda sin barra igual, que es lo que se quería evitar.
    //
    // Así que hay tope: pasado esto, se enseña la barra de Windows a la fuerza y se mata el
    // proceso. Vale más un cierre feo que un escritorio secuestrado.
    public static readonly TimeSpan EsperaDeLaSalidaLimpia = TimeSpan.FromSeconds(4);
}
