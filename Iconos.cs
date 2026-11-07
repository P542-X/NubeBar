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

    // ---------- Un fallo NO se guarda para siempre ----------
    //
    // La caché guardaba también los nulos, y eso convertía un tropiezo de un momento en
    // permanente. El shell no contesta siempre a la primera: una app que acaba de
    // arrancar, un ejecutable en un disco que estaba dormido, o el shell ocupado mientras
    // un juego se come la máquina. Preguntado medio segundo después habría contestado,
    // pero ya nadie volvía a preguntar — la app se quedaba con su inicial de respaldo
    // hasta reiniciar NubeBar. El usuario lo contó así: «cuando abro una app tipo Discord
    // hay veces que se queda con la letra D y no identifica qué app es, es como si los
    // iconos no se actualizaran».
    //
    // Así que en `_cache` solo entran los ACIERTOS, y los fallos van aparte con la hora:
    // se reintenta pasado un rato. El rato existe para que un ejecutable que de verdad no
    // tiene icono no cueste una llamada COM en cada repintado del carrusel —que es cada
    // segundo—, y es corto porque lo que se está esperando es que una app termine de
    // arrancar.
    static readonly Dictionary<string, DateTime> _fallos = new(StringComparer.OrdinalIgnoreCase);

    internal static readonly TimeSpan ReintentarTras = TimeSpan.FromSeconds(5);

    // Devuelve null si el shell no sabe qué es (no pasa nada: se enseña sin icono)
    public static ImageSource? De(string rutaOId, int tamano = 32)
    {
        if (string.IsNullOrWhiteSpace(rutaOId)) return null;
        var clave = tamano + "|" + rutaOId;
        if (_cache.TryGetValue(clave, out var guardado) && guardado != null) return guardado;
        if (_fallos.TryGetValue(clave, out var fallo) && DateTime.UtcNow - fallo < ReintentarTras)
            return null;

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

        if (img != null) { _cache[clave] = img; _fallos.Remove(clave); }
        else _fallos[clave] = DateTime.UtcNow;
        return img;
    }

    // ---------- ¿Es un icono oscuro? ----------
    //
    // Hay apps cuyo icono es un trazo negro sobre transparente (ChatGPT, algunas de
    // terminal, las de GitHub…). Sobre la isla, que es negra, no se veían: lo contó el
    // usuario, «hay algunos iconos como el de chatgpt que se ven en negro y el fondo es
    // negro». Esto mira los píxeles que se ven —pesados por su opacidad— y dice si en
    // conjunto son oscuros. Se calcula una vez por imagen.
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, object> _oscuros = new();

    public const double LuminanciaOscura = 0.06;

    public static bool EsOscuro(ImageSource? img)
    {
        if (img is not BitmapSource bmp || bmp.PixelWidth == 0 || bmp.PixelHeight == 0) return false;
        if (_oscuros.TryGetValue(img, out var ya)) return (bool)ya;

        bool oscuro = false;
        try
        {
            var bgra = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            int ancho = bgra.PixelWidth, alto = bgra.PixelHeight, paso = ancho * 4;
            var px = new byte[paso * alto];
            bgra.CopyPixels(px, paso, 0);
            oscuro = LuminanciaMedia(px) is { } l && l < LuminanciaOscura;
        }
        catch { oscuro = false; }

        _oscuros.AddOrUpdate(img, oscuro);
        return oscuro;
    }

    // Luminancia media (WCAG) de los píxeles BGRA visibles, pesada por su alfa. Null si
    // casi no hay nada que se vea. Aparte para poder probarla sin bitmaps.
    internal static double? LuminanciaMedia(byte[] bgra)
    {
        double suma = 0, peso = 0;
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            double a = bgra[i + 3] / 255.0;
            if (a < 0.1) continue;
            // Bgra32 no viene premultiplicado: el color es el del píxel tal cual
            suma += a * Contraste.Luminancia(Color.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]));
            peso += a;
        }
        return peso < 1 ? null : suma / peso;
    }
}
