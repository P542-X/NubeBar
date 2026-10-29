using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// `NubeBar.exe --diagbandeja [segundos]`: vigila la bandeja durante ese rato y apunta
// cada cambio con su causa.
//
// ---------- Por qué hace falta ----------
//
// "Los iconos ocultos no muestran las apps que arranco después" no se puede resolver
// leyendo el código: el código dice que la colección es observable y que hay un
// `CollectionChanged` enganchado. Lo que hay que saber es si **de verdad** llega un
// icono nuevo cuando una app se registra con NubeBar ya en marcha, si desaparece al
// cerrarla, si tras reiniciar Explorer vuelve a llegar algo, y si entre medias se cuela
// un duplicado o el orden se mueve sin motivo.
//
// Mira y cuenta, no toca nada: ni enumera por su cuenta ni fuerza refrescos. Si la
// bandeja se actualiza sola, esto lo demuestra; y si no, dice exactamente en qué paso
// se quedó.
public static class DiagBandeja
{
    internal const string Bandera = "--diagbandeja";

    public static bool Encendido;
    public static int Segundos = 120;

    public static void Leer(string[] args)
    {
        if (args.Length == 0 || args[0] != Bandera) return;
        Encendido = true;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s is > 0 and <= 900) Segundos = s;
    }

    // ---------- La identidad de un icono ----------
    //
    // Es la misma que usa Windows: la ventana que lo registró y su número dentro de esa
    // ventana, o el GUID si la app usó la versión moderna del protocolo. El título NO
    // sirve de identidad —cambia con el tooltip, y dos apps pueden llamarse igual— pero
    // se lleva al lado para que el log se pueda leer.
    public readonly record struct Ficha(string Id, string Nombre, bool Oculto)
    {
        public override string ToString() => $"{Nombre} [{Id}]{(Oculto ? " (oculto)" : "")}";
    }

    public static Ficha FichaDe(ManagedShell.WindowsTray.NotifyIcon i)
    {
        string guid = i.GUID.ToString();
        string id = guid != "00000000-0000-0000-0000-000000000000"
            ? "guid:" + guid
            : $"hwnd:{i.HWnd}/uid:{i.UID}";
        string nombre = (i.Title ?? "").Split('\n')[0];
        if (nombre.Length == 0) nombre = System.IO.Path.GetFileName(i.Path ?? "");
        if (nombre.Length == 0) nombre = "(sin nombre)";
        return new Ficha(id, nombre, i.IsHidden);
    }

    // ---------- Lo que cambia de una muestra a la siguiente ----------
    //
    // Puro: se le dan dos listas y dice qué entró, qué salió, qué se repite y si el
    // orden de los que siguen estando se ha movido. Está fuera para poder comprobarlo
    // sin bandeja, sin Explorer y sin arrancar nada.
    public readonly record struct Cambio(
        List<string> Anadidos, List<string> Eliminados, List<string> Duplicados, bool OrdenMovido)
    {
        public bool Nada => Anadidos.Count == 0 && Eliminados.Count == 0 &&
                            Duplicados.Count == 0 && !OrdenMovido;
    }

    public static Cambio Comparar(List<Ficha> antes, List<Ficha> ahora)
    {
        var idsAntes = antes.Select(f => f.Id).ToList();
        var idsAhora = ahora.Select(f => f.Id).ToList();

        var anadidos = ahora.Where(f => !idsAntes.Contains(f.Id)).Select(f => f.ToString()).ToList();
        var eliminados = antes.Where(f => !idsAhora.Contains(f.Id)).Select(f => f.ToString()).ToList();

        // Un duplicado es la MISMA identidad dos veces en la misma muestra. Es lo que
        // pasaría si al reiniciar Explorer una app se registrara otra vez sin que la
        // anterior se hubiera retirado.
        var duplicados = idsAhora.GroupBy(x => x).Where(g => g.Count() > 1)
                                 .Select(g => $"{g.Key} x{g.Count()}").ToList();

        // El orden de los que estaban antes y siguen estando: si la secuencia relativa
        // cambia sin que haya entrado ni salido nada, el panel baila sin motivo.
        var comunesAntes = idsAntes.Where(idsAhora.Contains).ToList();
        var comunesAhora = idsAhora.Where(idsAntes.Contains).ToList();
        bool ordenMovido = !comunesAntes.SequenceEqual(comunesAhora);

        return new Cambio(anadidos, eliminados, duplicados, ordenMovido);
    }
}
