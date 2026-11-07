using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NubeBar;

// Convierte una tecla en bruto (la que guardó el hook) en el carácter que
// escribiría, respetando la distribución de teclado del usuario: con un teclado
// español la tecla a la derecha de la L tiene que dar "ñ", no ";".
static class Tipografia
{
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint hilo);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint codigo, uint tipo, IntPtr distribucion);
    [DllImport("user32.dll")]
    static extern int ToUnicodeEx(uint vk, uint scan, byte[] estado, StringBuilder buffer,
                                  int tamano, uint flags, IntPtr distribucion);

    public static string Traducir(GanchoTeclas.Pulsacion p)
    {
        try
        {
            // Texto inyectado con KEYEVENTF_UNICODE (lo hacen algunos programas de
            // control remoto y las herramientas de automatización): no trae código de
            // tecla, el carácter viene en el scan code tal cual
            if (p.Vk == 0 && p.Scan != 0) return ((char)p.Scan).ToString();

            var distribucion = GetKeyboardLayout(0);
            var estado = new byte[256];
            if (p.Shift) estado[0x10] = 0x80;
            // Ctrl y Alt juntos son AltGr, que sí produce carácter (por ejemplo @ o €).
            // Ctrl solo, en cambio, no escribe nada: es un atajo.
            if (p.Ctrl && p.Alt) { estado[0x11] = 0x80; estado[0x12] = 0x80; }
            else if (p.Ctrl || p.Alt) return "";

            uint scan = p.Scan != 0 ? p.Scan : MapVirtualKeyEx(p.Vk, 0, distribucion);

            var buffer = new StringBuilder(8);
            // El flag 4 pide no tocar el estado del teclado: si no, las tildes muertas
            // se quedan a medias y la siguiente letra sale mal
            int n = ToUnicodeEx(p.Vk, scan, estado, buffer, buffer.Capacity, 4, distribucion);
            if (n <= 0) return "";

            var texto = buffer.ToString(0, Math.Min(n, buffer.Length));
            foreach (var c in texto) if (char.IsControl(c)) return "";
            return texto;
        }
        catch { return ""; }
    }
}
