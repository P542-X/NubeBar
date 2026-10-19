using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NubeBar;

// Saca el icono de cualquier cosa que el shell sepa identificar: un .lnk, un .exe,
// una carpeta o una app de la Store (shell:AppsFolder\<id>). Es la misma ruta que
// usa el propio Explorador, así que sale el icono bueno y no uno genérico.
static class Iconos
{
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr bc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        void GetImage([In, MarshalAs(UnmanagedType.Struct)] SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr bc, ref Guid riid,
                                                   [MarshalAs(UnmanagedType.Interface)] out object item);

    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    const int SIIGBF_RESIZETOFIT = 0x00, SIIGBF_ICONONLY = 0x04;

    static readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Devuelve null si el shell no sabe qué es (no pasa nada: se enseña sin icono)
    public static ImageSource? De(string rutaOId, int tamano = 32)
    {
        if (string.IsNullOrWhiteSpace(rutaOId)) return null;
        var clave = tamano + "|" + rutaOId;
        if (_cache.TryGetValue(clave, out var guardado)) return guardado;

        ImageSource? img = null;
        IntPtr hbm = IntPtr.Zero;
        try
        {
            var iidItem = typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(rutaOId, IntPtr.Zero, ref iidItem, out object obj);
            if (obj is IShellItemImageFactory fabrica)
            {
                fabrica.GetImage(new SIZE { cx = tamano, cy = tamano }, SIIGBF_RESIZETOFIT | SIIGBF_ICONONLY, out hbm);
                if (hbm != IntPtr.Zero)
                {
                    img = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty,
                                                                BitmapSizeOptions.FromEmptyOptions());
                    img.Freeze();
                }
            }
            if (obj is not null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
        }
        catch { img = null; }
        finally { if (hbm != IntPtr.Zero) DeleteObject(hbm); }

        _cache[clave] = img;
        return img;
    }
}
