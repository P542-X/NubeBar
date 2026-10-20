using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace NubeBar;

// Hook de teclado de bajo nivel para que la tecla Windows abra NUESTRO menú.
//
// Vive en su propio hilo con su propio bucle de mensajes, no en el de la interfaz:
// Windows llama al callback en el hilo que instaló el hook y, si ese hilo tarda más
// de LowLevelHooksTimeout (unos 300 ms por defecto), el sistema desactiva el hook sin
// avisar. Si colgásemos esto del hilo de la UI, cualquier animación pesada o un
// cuadro de diálogo dejarían el teclado del sistema a medias.
//
// El callback por tanto no llama a nada de WPF: solo toca campos volatile y una cola.
public sealed class GanchoTeclas : IDisposable
{
    // ---------- Win32 ----------
    delegate IntPtr Procedimiento(int codigo, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint datos, flags, tiempo; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, tiempo; public UIntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public MOUSEINPUT raton; [FieldOffset(0)] public KEYBDINPUT tecla; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint tipo; public UNION u; }

    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint msg; public IntPtr w, l; public uint tiempo; public int x, y; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int id, Procedimiento fn, IntPtr modulo, uint hilo);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int codigo, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? nombre);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] entradas, int tamano);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint hilo, uint msg, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    const uint WM_QUIT = 0x0012;

    public const int VkWinIzq = 0x5B, VkWinDer = 0x5C, VkNeutra = 0xFF;
    const int VkEsc = 0x1B, VkCtrl = 0x11, VkShift = 0x10, VkAlt = 0x12, VkImprPant = 0x2C;

    // Marca en dwExtraInfo para reconocer lo que inyectamos nosotros: sin esto el
    // hook se vería su propia tecla neutra y se realimentaría
    static readonly UIntPtr Firma = (UIntPtr)0x4E554245;   // 'NUBE'

    // ---------- Estado ----------
    public readonly record struct Pulsacion(uint Vk, uint Scan, bool Shift, bool Ctrl, bool Alt);

    // Lo que el hook quiere contarle a la interfaz. Se invocan desde el hilo del hook,
    // así que quien se suscriba tiene que saltar al hilo de la UI por su cuenta.
    public event Action? AbrirMenu;

    // Marca de cuándo soltaste la tecla, para poder medir lo que tarda el menú en
    // salir. Lo que se mide es de la tecla a la ventana colocada, sin la animación.
    public static long MarcaTecla;

    public readonly ConcurrentQueue<Pulsacion> Cola = new();

    volatile bool _capturando;
    long _capturaDesde;
    volatile bool _winAbajo;
    volatile bool _otraDurante;
    volatile bool _activo = true;

    IntPtr _hook;
    uint _hiloId;
    Thread? _hilo;
    Procedimiento? _proc;      // referencia viva: si la recoge el GC, el hook revienta

    public bool Instalado => _hook != IntPtr.Zero;

    // Mientras está en true el hook se traga las teclas y las encola en vez de
    // dejarlas llegar a la ventana que haya detrás
    public bool Capturando
    {
        get => _capturando;
        set
        {
            if (value) Interlocked.Exchange(ref _capturaDesde, Environment.TickCount64);
            _capturando = value;
        }
    }

    // Permite apagar la intercepción sin desinstalar el hook (config en caliente)
    public bool Activo { get => _activo; set => _activo = value; }

    // ---------- Arranque y parada ----------
    public void Instalar()
    {
        if (_hilo != null) return;
        _hilo = new Thread(Bucle) { IsBackground = true, Name = "NubeBar hook de teclado" };
        _hilo.SetApartmentState(ApartmentState.STA);
        _hilo.Start();
    }

    void Bucle()
    {
        _hiloId = GetCurrentThreadId();
        _proc = Callback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            Log.Info($"no se pudo instalar el hook de teclado (error {Marshal.GetLastWin32Error()})");
            _hilo = null;    // si no, Instalar() no vuelve a intentarlo en toda la sesión
            return;
        }
        Log.Info("hook de teclado instalado en su propio hilo");

        // Bucle de mensajes propio: es lo que permite que Windows nos llame
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        Log.Info("hook de teclado retirado");
    }

    public void Dispose()
    {
        _activo = false;
        _capturando = false;
        if (_hiloId != 0) PostThreadMessage(_hiloId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hilo?.Join(1000);
        _hilo = null;
    }

    // ---------- El callback ----------
    // Tiene que ser rapidísimo. Nada de WPF, nada de bloqueos, nada de disco.
    IntPtr Callback(int codigo, IntPtr wParam, IntPtr lParam)
    {
        if (codigo < 0) return CallNextHookEx(_hook, codigo, wParam, lParam);

        var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        // Lo que hemos inyectado nosotros pasa de largo
        if (info.dwExtraInfo == Firma) return CallNextHookEx(_hook, codigo, wParam, lParam);

        int msg = wParam.ToInt32();
        bool abajo = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool arriba = msg is WM_KEYUP or WM_SYSKEYUP;
        uint vk = info.vkCode;

        if (!_activo) return CallNextHookEx(_hook, codigo, wParam, lParam);

        // 1) Modo captura: las teclas son nuestras hasta que la caja tenga el foco
        if (_capturando)
        {
            // Red de seguridad: si la interfaz no contesta, se suelta sola
            if (Environment.TickCount64 - Interlocked.Read(ref _capturaDesde) > 1500)
            {
                _capturando = false;
            }
            else if (EsDelSistema(vk))
            {
                // Esto no es para la caja: pasa de largo sin encolarse
                return CallNextHookEx(_hook, codigo, wParam, lParam);
            }
            else
            {
                if (abajo && vk is not (VkWinIzq or VkWinDer or VkCtrl or VkShift or VkAlt))
                    Cola.Enqueue(new Pulsacion(vk, info.scanCode,
                                               Abajo(VkShift), Abajo(VkCtrl), Abajo(VkAlt)));
                return (IntPtr)1;    // no llega a la ventana de detrás
            }
        }

        // 2) La tecla Windows
        if (vk is VkWinIzq or VkWinDer)
        {
            if (abajo)
            {
                if (!_winAbajo) { _winAbajo = true; _otraDurante = false; }
                return CallNextHookEx(_hook, codigo, wParam, lParam);
            }
            if (arriba)
            {
                bool sola = _winAbajo && !_otraDurante;
                _winAbajo = false;
                if (sola)
                {
                    // La tecla máscara: Windows solo abre el Inicio si al soltar Win no
                    // se ha pulsado nada más. Inyectando una tecla neutra se lo creemos.
                    InyectarNeutra();
                    Capturando = true;
                    MarcaTecla = System.Diagnostics.Stopwatch.GetTimestamp();
                    AbrirMenu?.Invoke();
                }
                return CallNextHookEx(_hook, codigo, wParam, lParam);
            }
        }

        // 3) Cualquier otra tecla con Win abajo convierte la pulsación en combinación
        if (abajo)
        {
            if (_winAbajo) _otraDurante = true;

            // 4) Ctrl+Esc también abre el nuestro
            if (vk == VkEsc && Abajo(VkCtrl))
            {
                Capturando = true;
                MarcaTecla = System.Diagnostics.Stopwatch.GetTimestamp();
                AbrirMenu?.Invoke();
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hook, codigo, wParam, lParam);
    }

    static bool Abajo(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // Teclas que nunca son para la caja de búsqueda, aunque estemos capturando:
    // Impr Pant (sola o con Win/Alt/Shift, que es como se piden las capturas en
    // Windows 11) y cualquier combinación con Win o Alt, que son del sistema. Antes
    // se las tragaba todas y, nada más abrir el menú, el atajo de captura no hacía
    // nada. Las normales siguen yendo a la cola, que es para lo que está la captura.
    static bool EsDelSistema(uint vk) =>
        vk == VkImprPant || Abajo(VkWinIzq) || Abajo(VkWinDer) || Abajo(VkAlt);

    void InyectarNeutra()
    {
        var entradas = new[]
        {
            new INPUT { tipo = 1, u = new UNION { tecla = new KEYBDINPUT { vk = VkNeutra, extra = Firma } } },
            new INPUT { tipo = 1, u = new UNION { tecla = new KEYBDINPUT { vk = VkNeutra, flags = 2, extra = Firma } } }
        };
        SendInput((uint)entradas.Length, entradas, Marshal.SizeOf<INPUT>());
    }
}
