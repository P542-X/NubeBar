using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NubeBar;

// "Arrancar con Windows" = un acceso directo en la carpeta Inicio del usuario.
// Se usa esa carpeta y no el registro porque se ve, se puede quitar a mano y no
// necesita permisos de administrador.
public static class Arranque
{
    const string NombreAcceso = "NubeBar.lnk";

    public static string Carpeta => Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    public static string RutaAcceso => Path.Combine(Carpeta, NombreAcceso);

    // El exe autónomo; si no existe todavía, el de la compilación
    public static string ExeRecomendado()
    {
        var proceso = Environment.ProcessPath;
        var raizProyecto = AppContext.BaseDirectory;
        var dist = Path.GetFullPath(Path.Combine(raizProyecto, "..", "..", "..", "dist", "NubeBar.exe"));
        if (File.Exists(dist)) return dist;
        return proceso ?? dist;
    }

    public static bool Activo => File.Exists(RutaAcceso);

    public static bool Poner(bool activo, out string mensaje)
    {
        mensaje = "";
        try
        {
            if (!activo)
            {
                if (File.Exists(RutaAcceso)) File.Delete(RutaAcceso);
                mensaje = "quitado de la carpeta Inicio";
                return true;
            }

            var destino = ExeRecomendado();
            if (!File.Exists(destino)) { mensaje = "no encuentro el exe: " + destino; return false; }

            var tipo = Type.GetTypeFromProgID("WScript.Shell");
            if (tipo == null) { mensaje = "WScript.Shell no disponible"; return false; }

            dynamic? shell = Activator.CreateInstance(tipo);
            if (shell == null) { mensaje = "no se pudo crear WScript.Shell"; return false; }
            try
            {
                Directory.CreateDirectory(Carpeta);
                dynamic acceso = shell.CreateShortcut(RutaAcceso);
                acceso.TargetPath = destino;
                acceso.WorkingDirectory = Path.GetDirectoryName(destino);
                acceso.Description = "NubeBar";
                acceso.Save();
            }
            finally { if (Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); }

            mensaje = "creado hacia " + destino;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("arranque con Windows", ex);
            mensaje = ex.Message;
            return false;
        }
    }
}
