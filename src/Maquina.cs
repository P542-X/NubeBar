using System;
using Microsoft.Win32;

namespace NubeBar;

// Lo que es esta máquina: sistema, procesador y memoria.
//
// ---------- Por qué existe ----------
//
// La maqueta lo pone en el panel derecho —«Windows 11», «i7-12700H», «16 GB RAM»— y es un
// dato que NubeBar no enseñaba por ningún sitio. Para saber qué procesador tienes había que
// abrir la ventana de información del sistema.
//
// ---------- Y por qué se LEE, no se escribe ----------
//
// Todo lo de aquí sale del registro de Windows o del propio entorno. Nada está escrito a
// mano, y por eso la tarjeta dice la verdad en cualquier equipo. La maqueta ponía
// «i7-12700H» porque era una maqueta; aquí sale lo que de verdad hay dentro.
//
// Cada lectura puede fallar por su cuenta —un registro recortado, una política— y cuando
// falla se devuelve cadena vacía: la tarjeta se salta esa línea en vez de enseñar
// «Desconocido», que ocupa lo mismo y no dice nada.
public static class Maquina
{
    // ---------- El nombre del sistema ----------
    //
    // `Environment.OSVersion` da «10.0.26200», que es verdad y no es lo que nadie llama a su
    // Windows. El nombre bonito está en el registro, puesto por el propio instalador.
    //
    // Y ojo con el 11: Microsoft dejó `ProductName` en «Windows 10» durante las primeras
    // versiones de Windows 11, así que leerlo a secas dice la versión equivocada. Lo que
    // distingue es el número de compilación — 22000 en adelante es 11.
    public static string Sistema()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string nombre = k?.GetValue("ProductName") as string ?? "";
            string build = k?.GetValue("CurrentBuildNumber") as string ?? "";

            if (int.TryParse(build, out int n) && n >= 22000 && nombre.Contains("Windows 10"))
                nombre = nombre.Replace("Windows 10", "Windows 11");

            return nombre.Trim();
        }
        catch { return ""; }
    }

    // ---------- El procesador ----------
    //
    // `PROCESSOR_IDENTIFIER` del entorno da «AMD64 Family 25 Model 97 Stepping 2», que es
    // para una máquina y no para una persona. El nombre comercial está en el registro.
    //
    // Y viene con espacios de relleno al final —el registro lo guarda así— que hay que
    // quitar, o la tarjeta sale con un hueco a la derecha que parece un fallo de alineación.
    public static string Procesador()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            string s = (k?.GetValue("ProcessorNameString") as string ?? "").Trim();

            // «AMD Ryzen 7 7800X3D 8-Core Processor» → «AMD Ryzen 7 7800X3D». La coletilla
            // no informa: que un procesador tenga núcleos ya se da por hecho, y ocupa la
            // mitad de la línea en una tarjeta estrecha.
            return QuitarLaColetilla(s);
        }
        catch { return ""; }
    }

    // Puro para poder exigirlo sin registro delante.
    public static string QuitarLaColetilla(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "";
        var s = nombre.Trim();
        foreach (var sobra in new[] { " 8-Core Processor", " 6-Core Processor",
                                      " 12-Core Processor", " 16-Core Processor",
                                      " Processor", " CPU" })
            if (s.EndsWith(sobra, StringComparison.OrdinalIgnoreCase))
            {
                s = s[..^sobra.Length].Trim();
                break;
            }

        // Lo que Intel pone después de una arroba —«@ 2.30GHz»— ya lo dice el modelo.
        int arroba = s.IndexOf('@');
        if (arroba > 0) s = s[..arroba].Trim();
        return s;
    }

    // ---------- La memoria ----------
    //
    // En gigas redondos, que es como se compra y como se dice. Devuelve 0 si no se puede
    // leer, y entonces la tarjeta se salta la línea.
    public static int MemoriaGB()
    {
        try
        {
            // `GetPhysicallyInstalledSystemMemory` da lo INSTALADO en kilobytes, que es lo
            // que pone en la caja. `TotalPhysicalMemory` da lo que ve Windows, que es algo
            // menos porque el hardware se queda un trozo: con 32 GB instalados diría 31,8 y
            // nadie llama a eso «32 GB».
            if (Nativo.MemoriaInstaladaKB(out long kb) && kb > 0)
                return (int)Math.Round(kb / 1024.0 / 1024.0);
        }
        catch { }
        return 0;
    }
}
