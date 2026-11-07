using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NubeBar;

// La ENTRADA DE VÍDEO del monitor, por DDC/CI. Cambiar de DisplayPort a HDMI sin levantarse a
// buscar el botón de debajo de la pantalla.
//
// ---------- Qué es esto y qué NO es ----------
//
// Esto le habla al MONITOR por el cable y le dice «ponte en HDMI 1». El monitor obedece y
// enseña lo que le llegue por ese cable, que puede ser una consola. Lo que NO hace, y conviene
// que esté escrito aquí para que nadie lo prometa en la interfaz:
//
//   - No captura vídeo. La señal de la consola va del cable al panel y no pasa por Windows.
//   - No mete la consola en una ventana. Si cambias a HDMI 1, la pantalla deja de enseñar el
//     escritorio: es el propio monitor el que se cambia de canal.
//   - No toca el firmware ni los ajustes guardados del monitor. Solo manda comandos MCCS.
//
// ---------- De dónde sale el código ----------
//
// `Brillo.cs` ya hablaba DDC/CI para el brillo, así que la fontanería de abrir monitores
// físicos está probada en esta casa — incluida la trampa del `CharSet.Unicode` en
// `PHYSICAL_MONITOR`, que está explicada allí y repetida abajo porque repetir una advertencia
// que ya costó un machaque del montón sale gratis.
//
// Lo que NO se copia de `Brillo` es la gestión de handles: allí se abren, se guardan en la
// lista y se sueltan con `Soltar()` cuando la interfaz termina. Aquí se abren, se opera y se
// cierran dentro de la misma llamada. El motivo es el caso que este módulo tiene y el brillo
// no: cambiar la entrada puede DESCONECTAR la pantalla donde vive NubeBar, y un handle físico
// guardado de una pantalla que ya no está es un handle que nadie va a soltar.
static class Entradas
{
    // ================= Lo que se le pregunta al monitor =================

    const byte VcpEntrada = 0x60;       // Input Source (MCCS 2.2, tabla 8.4)
    const byte VcpBrillo = 0x10;        // solo para desempatar «DDC/CI apagado» de «no gestiona la entrada»

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        // CharSet.Unicode NO es decorativo: szDescription es WCHAR[128] en el nativo. Sin él,
        // ByValTStr se marshala como 128 BYTES, la estructura mide 136 en vez de 264, y dxva2
        // escribe 264 por monitor sobre un hueco de 136 y machaca el montón. Ya pasó una vez;
        // ver el comentario largo de `Brillo.cs`.
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szDescription;
    }

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint cuantos);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint tam, [Out] PHYSICAL_MONITOR[] monitores);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool DestroyPhysicalMonitor(IntPtr monitor);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr monitor, byte codigo, IntPtr tipo, out uint actual, out uint maximo);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool SetVCPFeature(IntPtr monitor, byte codigo, uint valor);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetCapabilitiesStringLength(IntPtr monitor, out uint largo);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr monitor, [Out] byte[] cadena, uint largo);

    // ================= Lo que la interfaz ve =================

    // Una entrada que el monitor dice tener. `Conocida` es false cuando el código no está en la
    // tabla de MCCS: entonces se enseña como «Desconocido (0x1B)» y no se le inventa un nombre.
    // Un botón que diga «USB-C» sobre un código que la norma no define es mentirle al usuario.
    public sealed record Entrada(byte Codigo, string Nombre, bool Conocida);

    // ---------- Por qué `Clave` y no el índice ----------
    //
    // Entre que la interfaz dibuja los botones y el usuario pulsa uno pueden pasar cosas:
    // apagar una pantalla, cambiar un cable, que Windows reordene los `DISPLAY`. Si el botón
    // llevara dentro un índice o —peor— un handle, el clic acabaría cambiándole la entrada al
    // monitor equivocado. Es el fallo que el plan prohíbe con nombre y apellidos.
    //
    // Así que el botón lleva la CLAVE, y `Poner` vuelve a buscar quién es esa clave justo antes
    // de mandar el comando. Si no la encuentra, no manda nada.
    public sealed record Pantalla(
        string Clave,
        string Dispositivo,        // \\.\DISPLAY1 — para enseñar, nunca para identificar
        int IndiceFisico,          // qué monitor físico de esa pantalla (casi siempre 0)
        string Descripcion,        // szDescription de dxva2: a menudo «Generic PnP Monitor»
        Edid.Datos? Edid,
        bool Responde,             // contesta a DDC/CI (aunque no gestione la entrada)
        bool GestionaEntrada,      // contesta al 0x60 en concreto
        byte? Actual,              // el valor del 0x60 ahora mismo, si contestó
        List<Entrada> Disponibles, // las que ANUNCIA; vacía si no las anuncia
        bool Anunciadas)           // si la lista sale de las capacidades o es un apaño
    {
        // El nombre bonito del EDID si lo hay; si no, lo que diga dxva2; y si eso es el genérico
        // de Windows, al menos la ranura, que distingue dos filas iguales.
        public string Rotulo
        {
            get
            {
                if (Edid is { } e) return e.Rotulo;
                var d = Descripcion?.Trim() ?? "";
                bool generico = d.Length == 0 ||
                                d.Contains("Generic", StringComparison.OrdinalIgnoreCase) ||
                                d.Contains("PnP", StringComparison.OrdinalIgnoreCase);
                return generico ? $"Monitor en {Corto(Dispositivo)}" : d;
            }
        }

        public string? NombreActual => Actual is { } a ? NombreDe(a) : null;
    }

    // `\\.\DISPLAY1` → `DISPLAY1`. Para la interfaz, que las barras invertidas no aportan nada.
    internal static string Corto(string? dispositivo) =>
        string.IsNullOrWhiteSpace(dispositivo) ? "?" : dispositivo.TrimStart('\\', '.', '?');

    // ================= La tabla de MCCS =================
    //
    // Tabla 8.4 de la norma MCCS 2.2, y SOLO esa. Los valores que se vieron en la máquina de
    // desarrollo —0x0F como DisplayPort y 0x11 como HDMI 1— coinciden con la norma, pero eso no
    // los convierte en universales: cada fabricante monta sus conectores sobre los códigos que
    // quiere, y hay monitores que usan 0x1B para USB-C sin que la norma lo diga en ninguna
    // parte. Lo que esta tabla hace es TRADUCIR códigos, no prometer que el monitor los tenga:
    // quién los tiene lo dice `Disponibles`, que sale de las capacidades del propio monitor.
    static readonly Dictionary<byte, string> Conocidas = new()
    {
        [0x01] = "VGA 1",        [0x02] = "VGA 2",
        [0x03] = "DVI 1",        [0x04] = "DVI 2",
        [0x05] = "Compuesto 1",  [0x06] = "Compuesto 2",
        [0x07] = "S-Vídeo 1",    [0x08] = "S-Vídeo 2",
        [0x09] = "Sintonizador 1", [0x0A] = "Sintonizador 2", [0x0B] = "Sintonizador 3",
        [0x0C] = "Componentes 1", [0x0D] = "Componentes 2", [0x0E] = "Componentes 3",
        [0x0F] = "DisplayPort 1", [0x10] = "DisplayPort 2",
        [0x11] = "HDMI 1",       [0x12] = "HDMI 2",
    };

    public static string NombreDe(byte codigo) =>
        Conocidas.TryGetValue(codigo, out var n) ? n : $"Desconocido (0x{codigo:X2})";

    public static bool EsConocida(byte codigo) => Conocidas.ContainsKey(codigo);

    // ================= Leer las capacidades =================
    //
    // La cadena de capacidades es lo ÚNICO que dice qué entradas tiene este monitor. El valor
    // máximo que devuelve el 0x60 no sirve: el plan lo dice y es verdad —un máximo de 0x03 no
    // significa cuatro entradas— porque en el 0x60 el campo es un enumerado, y en un enumerado
    // el máximo no enumera nada.
    //
    // Una cadena de ejemplo, recortada, de un monitor real:
    //
    //   (prot(monitor)type(LCD)model(Q27G3G3R3)cmds(01 02 03 07 0C E3 F3)
    //    vcp(02 04 05 08 10 12 14(01 05 08 0B) 16 18 1A 52 60(0F 11 12) 6C 6E 70 AC AE…)
    //    mccs_ver(2.2))
    //
    // Lo que hace falta es el grupo de `60(...)`. Y hay que sacarlo con cuidado: dentro de
    // `vcp(...)` hay OTROS códigos con grupo propio —el `14(01 05 08 0B)` de ahí arriba— así que
    // buscar «60(» a pelo en la cadena entera puede caer dentro de la lista de otro, o en el
    // modelo, o en los comandos.
    //
    // Pura y `internal`: se prueba con cadenas escritas a mano, incluidas las malformadas, que
    // es la mitad del trabajo. Hay monitores que devuelven paréntesis sin cerrar.
    internal static List<byte> EntradasDeCapacidades(string? caps)
    {
        var res = new List<byte>();
        if (string.IsNullOrWhiteSpace(caps)) return res;

        if (!BloqueVcp(caps, out int i, out int fin)) return res;

        while (i < fin)
        {
            char c = caps[i];

            // Un grupo que no cuelga de ningún código: se salta entero. Pasa en cadenas
            // malformadas, y entrar dentro sería leer los valores de otro como si fueran
            // entradas.
            if (c == '(') { i = TrasElGrupo(caps, i, fin, out _); continue; }
            if (!EsHex(c)) { i++; continue; }

            int ini = i;
            while (i < fin && EsHex(caps[i])) i++;
            var token = caps.AsSpan(ini, i - ini);

            // ¿Lleva grupo detrás? Entre el código y su paréntesis puede haber espacios.
            int j = i;
            while (j < fin && char.IsWhiteSpace(caps[j])) j++;
            if (j >= fin || caps[j] != '(') continue;

            int tras = TrasElGrupo(caps, j, fin, out bool cerro);

            // ---------- Un grupo que no cierra no se lee ----------
            //
            // Hay monitores que devuelven la cadena TRUNCADA. Si el corte cae dentro del
            // `60(...)`, lo que hay es media lista, y media lista no se distingue de una lista
            // entera: saldrían dos botones en un monitor con tres entradas y el tercero no
            // existiría. Mejor ninguno y el campo de código manual, que al menos dice la verdad.
            if (cerro && token.Length == 2 && token[0] == '6' && token[1] == '0')
                foreach (var b in BytesDe(caps, j + 1, tras - 1))
                    if (!res.Contains(b)) res.Add(b);
            i = tras;
        }

        return res;
    }

    // El interior de `vcp(...)`: `desde` apunta al primer carácter de dentro y `fin` al
    // paréntesis que lo cierra. Si no hay `vcp(` o se queda sin cerrar, false y no se lee nada.
    static bool BloqueVcp(string s, out int desde, out int fin)
    {
        desde = fin = 0;
        int p = s.IndexOf("vcp", StringComparison.OrdinalIgnoreCase);
        while (p >= 0)
        {
            int q = p + 3;
            while (q < s.Length && char.IsWhiteSpace(s[q])) q++;
            // `vcp` tiene que ser una palabra suelta: en `mccs_vcp(` el prefijo no es el nuestro.
            bool suelto = p == 0 || !char.IsLetterOrDigit(s[p - 1]) && s[p - 1] != '_';
            if (suelto && q < s.Length && s[q] == '(')
            {
                // ---------- Y si el `vcp(` es el que no cierra ----------
                //
                // La cadena está cortada, pero lo que hay DENTRO sigue siendo legible: si el
                // `60(0F 11)` alcanzó a cerrarse antes del corte, esa lista es buena. Antes se
                // tomaba `tras - 1` también en este caso, y `tras` era el final de la cadena:
                // eso se comía el último carácter, que justamente era el `)` del 60, y la lista
                // se perdía por un paréntesis que sí estaba.
                int tras = TrasElGrupo(s, q, s.Length, out bool cerro);
                desde = q + 1;
                fin = cerro ? tras - 1 : s.Length;
                return fin > desde;
            }
            p = s.IndexOf("vcp", p + 3, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    // Dado el índice de un '(', el índice justo DESPUÉS de su ')', contando anidamiento, y si de
    // verdad lo encontró.
    static int TrasElGrupo(string s, int abre, int limite, out bool cerro)
    {
        int profundo = 0;
        for (int i = abre; i < limite; i++)
        {
            if (s[i] == '(') profundo++;
            else if (s[i] == ')' && --profundo == 0) { cerro = true; return i + 1; }
        }
        // Nunca se cerró: se devuelve el límite para que el recorrido TERMINE. Una cadena
        // truncada no puede dejar a la interfaz dando vueltas, y `cerro` deja que quien llama
        // decida si lo que leyó vale.
        cerro = false;
        return limite;
    }

    static bool EsHex(char c) =>
        c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

    // Los bytes hexadecimales de un trozo. Se queda solo con los de DOS dígitos: en esa lista
    // un token de uno o de tres no es un código de entrada, y colar un 0x0F por un «F» suelto
    // pondría un botón de DisplayPort en un monitor que no lo tiene.
    static IEnumerable<byte> BytesDe(string s, int desde, int hasta)
    {
        for (int i = desde; i < hasta && i < s.Length; )
        {
            if (!EsHex(s[i])) { i++; continue; }
            int ini = i;
            while (i < hasta && i < s.Length && EsHex(s[i])) i++;
            if (i - ini == 2 && byte.TryParse(s.AsSpan(ini, 2), System.Globalization.NumberStyles.HexNumber,
                                              null, out byte b))
                yield return b;
        }
    }

    // ================= Serializar, que el bus no es de dos =================
    //
    // DDC/CI va por I²C sobre el cable de vídeo y es LENTO: entre 40 y 80 ms por pregunta, y la
    // cadena de capacidades puede pasar del segundo. Dos preguntas montadas en el mismo bus
    // devuelven basura o nada.
    //
    // El plan pide serializar POR DISPOSITIVO; esto serializa TODO, que es más estricto. Se hace
    // así a propósito: con dos monitores en la misma gráfica el bus puede ser compartido, y
    // ahorrarse 50 ms a cambio de una lectura corrupta es un mal cambio. Lo que no puede pasar
    // —y no pasa— es que esto se tome en el hilo de la interfaz: todo lo de aquí se llama desde
    // un trabajador (ver `MainWindow.ModuloMonitores.cs`).
    static readonly object _bus = new();

    // ================= Enumerar =================

    // Las capacidades no cambian mientras el monitor siga siendo el mismo, y leerlas cuesta más
    // de un segundo por pantalla. Se guardan por clave y se olvidan cuando cambia la
    // configuración de pantallas, que es la única vez que pueden haber cambiado de verdad.
    static readonly Dictionary<string, List<byte>> _capacidades = new(StringComparer.OrdinalIgnoreCase);

    public static void Olvidar()
    {
        lock (_bus) _capacidades.Clear();
    }

    // Todos los monitores físicos, con su entrada actual y las que anuncian.
    //
    // LENTA a propósito en el primer arranque (lee capacidades) y rápida después. Nunca se llama
    // desde el hilo de la interfaz.
    public static List<Pantalla> Pantallas()
    {
        var lista = new List<Pantalla>();

        lock (_bus)
        {
            foreach (var m in Monitores.Todos())
            {
                // Una pantalla lógica puede exponer VARIOS monitores físicos (un divisor, una
                // cadena DisplayPort). No se asume 1:1 en ningún sitio: el bucle de abajo
                // recorre los que haya y cada uno sale como fila propia.
                uint n;
                try
                {
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(m.Handle, out n) || n == 0) continue;
                }
                catch (Exception ex) { Log.Error($"contar monitores físicos de {m.Dispositivo}", ex); continue; }

                var fisicos = new PHYSICAL_MONITOR[n];
                try
                {
                    if (!GetPhysicalMonitorsFromHMONITOR(m.Handle, n, fisicos))
                    {
                        Log.Info($"monitores: no se pudieron abrir los físicos de {Corto(m.Dispositivo)} " +
                                 $"(error {Marshal.GetLastWin32Error()})");
                        continue;
                    }
                }
                catch (Exception ex) { Log.Error($"abrir monitores físicos de {m.Dispositivo}", ex); continue; }

                var edids = Edid.DePantalla(m.Dispositivo);

                try
                {
                    for (int i = 0; i < fisicos.Length; i++)
                        lista.Add(Interrogar(fisicos[i], m.Dispositivo, i,
                                             i < edids.Count ? edids[i] : null));
                }
                finally
                {
                    // Siempre, y uno a uno. `DestroyPhysicalMonitors` en bloque también vale,
                    // pero si uno de los handles ya no sirve se lleva a los demás con él.
                    foreach (var f in fisicos)
                        try { DestroyPhysicalMonitor(f.hPhysicalMonitor); } catch { }
                }
            }
        }

        // Dos monitores del mismo modelo con la misma serie grabada compartirían clave del EDID.
        // Se desempatan con la ranura: no es estable entre reinicios, pero sí dentro de una
        // sesión, que es lo que hace falta para que un clic no vaya al monitor de al lado.
        Desempatar(lista);
        return lista;
    }

    static Pantalla Interrogar(PHYSICAL_MONITOR f, string dispositivo, int indice, Edid.Datos? edid)
    {
        var h = f.hPhysicalMonitor;
        string clave = edid?.Clave ?? $"{Corto(dispositivo)}#{indice}";

        byte? actual = null;
        bool gestiona = false, responde = false;

        try
        {
            if (GetVCPFeatureAndVCPFeatureReply(h, VcpEntrada, IntPtr.Zero, out uint valor, out _))
            {
                actual = (byte)(valor & 0xFF);
                gestiona = responde = true;
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                // Segunda pregunta, y solo para poder DAR UN MENSAJE DISTINTO. «DDC/CI está
                // apagado en el menú del monitor» y «este monitor no sabe cambiar de entrada»
                // se arreglan de formas opuestas, y un único «no se pudo» manda al usuario a
                // buscar a ciegas.
                responde = GetVCPFeatureAndVCPFeatureReply(h, VcpBrillo, IntPtr.Zero, out _, out _);
                Log.Info($"monitores: {Corto(dispositivo)}#{indice} no contesta al 0x60 (error {err}); " +
                         $"DDC/CI {(responde ? "sí responde, pero no gestiona la entrada" : "no responde")}");
            }
        }
        catch (Exception ex) { Log.Error($"leer la entrada de {Corto(dispositivo)}#{indice}", ex); }

        var codigos = gestiona ? Capacidades(h, clave, dispositivo, indice) : new List<byte>();
        bool anunciadas = codigos.Count > 0;

        // Sin capacidades no se inventan botones. Lo único que se puede afirmar es la entrada
        // en la que ESTÁ —la acabamos de leer— así que se enseña esa y nada más; para el resto
        // queda el código manual, que avisa de que no está probado.
        if (!anunciadas && actual is { } a) codigos = new List<byte> { a };

        var disponibles = codigos
            .Select(c => new Entrada(c, NombreDe(c), EsConocida(c)))
            .ToList();

        return new Pantalla(clave, dispositivo, indice, f.szDescription ?? "", edid,
                            responde, gestiona, actual, disponibles, anunciadas);
    }

    static List<byte> Capacidades(IntPtr h, string clave, string dispositivo, int indice)
    {
        if (_capacidades.TryGetValue(clave, out var ya)) return ya;

        var codigos = new List<byte>();
        try
        {
            if (GetCapabilitiesStringLength(h, out uint largo) && largo is > 1 and < 64 * 1024)
            {
                var buf = new byte[largo];
                if (CapabilitiesRequestAndCapabilitiesReply(h, buf, largo))
                {
                    // Es LPSTR: ASCII terminado en nulo, y el largo que devuelve Windows
                    // INCLUYE el nulo. Pasarlo entero a Encoding dejaría un \0 al final que
                    // no estorba al parser pero sí al log.
                    int fin = Array.IndexOf(buf, (byte)0);
                    string caps = Encoding.ASCII.GetString(buf, 0, fin < 0 ? buf.Length : fin);
                    codigos = EntradasDeCapacidades(caps);
                    Log.Info($"monitores: {Corto(dispositivo)}#{indice} anuncia " +
                             (codigos.Count > 0
                                ? $"entradas {string.Join(", ", codigos.Select(c => $"0x{c:X2}"))}"
                                : "capacidades sin lista de entradas en el 0x60"));
                }
                else
                    Log.Info($"monitores: {Corto(dispositivo)}#{indice} no devolvió sus capacidades " +
                             $"(error {Marshal.GetLastWin32Error()})");
            }
        }
        catch (Exception ex) { Log.Error($"leer las capacidades de {Corto(dispositivo)}#{indice}", ex); }

        // Se guarda incluso vacía: un monitor que no sabe anunciar sus entradas no va a
        // aprender a media sesión, y reintentarlo cuesta un segundo cada vez que se abre el
        // panel.
        _capacidades[clave] = codigos;
        return codigos;
    }

    static void Desempatar(List<Pantalla> lista)
    {
        var vistas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lista.Count; i++)
        {
            var p = lista[i];
            if (!vistas.TryAdd(p.Clave, 1))
            {
                vistas[p.Clave]++;
                lista[i] = p with { Clave = $"{p.Clave}@{Corto(p.Dispositivo)}#{p.IndiceFisico}" };
            }
        }
    }

    // ================= Cambiar =================

    public enum Resultado
    {
        Cambiada,        // se mandó y se comprobó leyendo el 0x60
        YaEstaba,        // no se mandó nada: ya era esa
        Ignorada,        // lo aceptó, contesta, y sigue donde estaba
        SinVerificar,    // lo aceptó y dejó de contestar: no hay forma de saberlo
        Rechazada,       // SetVCPFeature dijo que no
        NoEstaba,        // la clave ya no corresponde a ningún monitor
        NoGestiona,      // ese monitor no contesta al 0x60
    }

    public sealed record Cambio(Resultado Que, byte? Leida, string Mensaje);

    // Cuánto se espera a que el monitor obedezca. Un AOC tarda entre medio segundo y dos en
    // conmutar, y mientras lo hace puede dejar de contestar al bus: los fallos de lectura de
    // esos intentos NO son el final, son el monitor ocupado.
    const int Intentos = 14;
    const int EsperaMs = 150;

    // Pone la entrada de UN monitor, el de esa clave y ninguno más.
    //
    // Siempre desde un trabajador: entre el comando y la verificación pueden pasar dos segundos,
    // y en el hilo de la interfaz eso es la barra congelada.
    public static Cambio Poner(string clave, byte codigo)
    {
        lock (_bus)
        {
            // ---------- Volver a buscar quién es ----------
            //
            // No se reutiliza nada de la enumeración con la que se dibujaron los botones. El
            // handle de entonces puede estar cerrado, y el índice puede apuntar ahora a otra
            // pantalla. Se busca la clave de cero y, si no está, no se manda NADA: es la única
            // forma de garantizar lo que el plan pide, que no se le cambie la entrada a un
            // monitor distinto del elegido.
            if (!Localizar(clave, out var m, out int indice, out var destino))
                return new Cambio(Resultado.NoEstaba, null,
                                  "Ese monitor ya no está conectado. Pulsa Actualizar.");

            var h = destino.hPhysicalMonitor;
            try
            {
                if (!GetVCPFeatureAndVCPFeatureReply(h, VcpEntrada, IntPtr.Zero, out uint antes, out _))
                    return new Cambio(Resultado.NoGestiona, null,
                                      "El monitor no contesta al cambio de entrada. Comprueba que DDC/CI " +
                                      "está activado en su menú.");

                byte actual = (byte)(antes & 0xFF);

                // Mandar un cambio a la entrada en la que ya está hace que algunos monitores
                // parpadeen o reconmuten el panel para nada. Y el plan lo pide expresamente.
                if (actual == codigo)
                    return new Cambio(Resultado.YaEstaba, actual,
                                      $"Ya estaba en {NombreDe(codigo)}.");

                Log.Info($"monitores: {Corto(m.Dispositivo)}#{indice} ({clave}) " +
                         $"0x{actual:X2} → 0x{codigo:X2}");

                if (!SetVCPFeature(h, VcpEntrada, codigo))
                {
                    int err = Marshal.GetLastWin32Error();
                    Log.Info($"monitores: el monitor rechazó el cambio (error {err})");
                    return new Cambio(Resultado.Rechazada, actual,
                                      $"El monitor rechazó {NombreDe(codigo)}.");
                }

                // ---------- «Lo aceptó» no es «lo hizo» ----------
                //
                // `SetVCPFeature` devuelve TRUE en cuanto el comando sale por el cable. Que el
                // monitor conmute de verdad es otra cosa, y hay entradas que el monitor acepta
                // y luego ignora porque no tienen señal. Así que se vuelve a leer.
                // Lo último que el monitor llegó a contestar. Es lo que separa las dos
                // formas distintas de no poder verificar un cambio, y se parecen tan poco que
                // dan consejos opuestos: ver abajo.
                byte? ultima = null;

                for (int i = 0; i < Intentos; i++)
                {
                    Thread.Sleep(EsperaMs);
                    if (!GetVCPFeatureAndVCPFeatureReply(h, VcpEntrada, IntPtr.Zero, out uint v, out _))
                        continue;       // ocupado conmutando: no es un fallo todavía
                    byte leida = (byte)(v & 0xFF);
                    ultima = leida;
                    if (leida != codigo) continue;
                    Log.Info($"monitores: verificado, está en 0x{leida:X2}");
                    return new Cambio(Resultado.Cambiada, leida, $"Ahora en {NombreDe(codigo)}.");
                }

                // ---------- No verificar tiene DOS causas, y no se parecen ----------
                //
                // (a) El monitor SIGUE CONTESTANDO y sigue donde estaba. Entonces aceptó el
                //     comando y lo ignoró, que es lo que hacen con una entrada que no tienen o
                //     que no tiene señal. Se vio en la máquina de desarrollo mandando 0x1B —un
                //     código que ese AOC no anuncia— al 25G3ZM: `SetVCPFeature` devolvió TRUE y
                //     el monitor se quedó en HDMI 1.
                //
                // (b) El monitor DEJÓ DE CONTESTAR. Lo normal si le has cambiado la entrada a la
                //     pantalla donde vive NubeBar: deja de recibir señal de este PC, Windows la
                //     da por desconectada y el handle físico muere. El cambio salió bien —lo
                //     estás viendo en el monitor— y aquí no hay forma de comprobarlo.
                //
                // Antes las dos daban el mensaje de (b), que en el caso (a) manda al usuario a
                // mirar una pantalla que no se ha movido.
                if (ultima is { } quedo)
                {
                    Log.Info($"monitores: aceptó 0x{codigo:X2} y sigue en 0x{quedo:X2} " +
                             $"tras {Intentos * EsperaMs} ms: lo ha ignorado");
                    return new Cambio(Resultado.Ignorada, quedo,
                                      $"El monitor aceptó {NombreDe(codigo)} pero sigue en " +
                                      $"{NombreDe(quedo)}. Suele pasar con una entrada que no " +
                                      "tiene o que no recibe señal.");
                }

                Log.Info($"monitores: el comando salió y el monitor dejó de contestar en " +
                         $"{Intentos * EsperaMs} ms");
                return new Cambio(Resultado.SinVerificar, null,
                                  $"Enviado {NombreDe(codigo)}, pero el monitor dejó de " +
                                  "contestar y no se pudo confirmar. Es lo normal si has " +
                                  "cambiado la pantalla donde está NubeBar.");
            }
            catch (Exception ex)
            {
                Log.Error($"cambiar la entrada de {clave}", ex);
                return new Cambio(Resultado.Rechazada, null, "No se pudo cambiar la entrada.");
            }
            finally
            {
                try { DestroyPhysicalMonitor(h); } catch { }
                foreach (var otro in _abiertosDeMas)
                    try { DestroyPhysicalMonitor(otro); } catch { }
                _abiertosDeMas.Clear();
            }
        }
    }

    // Lee el 0x60 de un monitor concreto, para refrescar sin volver a leer capacidades.
    public static byte? Leer(string clave)
    {
        lock (_bus)
        {
            if (!Localizar(clave, out _, out _, out var destino)) return null;
            try
            {
                return GetVCPFeatureAndVCPFeatureReply(destino.hPhysicalMonitor, VcpEntrada,
                                                       IntPtr.Zero, out uint v, out _)
                     ? (byte)(v & 0xFF) : null;
            }
            catch { return null; }
            finally
            {
                try { DestroyPhysicalMonitor(destino.hPhysicalMonitor); } catch { }
                foreach (var otro in _abiertosDeMas)
                    try { DestroyPhysicalMonitor(otro); } catch { }
                _abiertosDeMas.Clear();
            }
        }
    }

    // Los handles que `Localizar` abrió y no son el que se buscaba. Se cierran en el `finally`
    // de quien llamó: abrir los monitores físicos de una pantalla los abre TODOS de golpe, y
    // quedarse solo con uno fuga los demás. Es el mismo fallo que `Brillo.Pantallas` tuvo —
    // «cada vez que se abría el panel de control se fugaban tantos handles como monitores con
    // DDC/CI»— y no se va a repetir aquí.
    static readonly List<IntPtr> _abiertosDeMas = new();

    // Encuentra el monitor físico de esa clave y lo deja ABIERTO. Quien llama lo cierra.
    static bool Localizar(string clave, out Monitores.Info monitor, out int indice, out PHYSICAL_MONITOR destino)
    {
        monitor = default!;
        indice = -1;
        destino = default;
        _abiertosDeMas.Clear();

        foreach (var m in Monitores.Todos())
        {
            uint n;
            try { if (!GetNumberOfPhysicalMonitorsFromHMONITOR(m.Handle, out n) || n == 0) continue; }
            catch { continue; }

            var fisicos = new PHYSICAL_MONITOR[n];
            try { if (!GetPhysicalMonitorsFromHMONITOR(m.Handle, n, fisicos)) continue; }
            catch { continue; }

            var edids = Edid.DePantalla(m.Dispositivo);
            int encontrado = -1;

            for (int i = 0; i < fisicos.Length; i++)
            {
                var e = i < edids.Count ? edids[i] : null;
                string suya = e?.Clave ?? $"{Corto(m.Dispositivo)}#{i}";
                // La clave puede traer el desempate de `Desempatar` pegado detrás.
                if (suya.Equals(clave, StringComparison.OrdinalIgnoreCase) ||
                    clave.Equals($"{suya}@{Corto(m.Dispositivo)}#{i}", StringComparison.OrdinalIgnoreCase))
                {
                    encontrado = i;
                    break;
                }
            }

            if (encontrado < 0)
            {
                foreach (var f in fisicos) try { DestroyPhysicalMonitor(f.hPhysicalMonitor); } catch { }
                continue;
            }

            monitor = m;
            indice = encontrado;
            destino = fisicos[encontrado];
            for (int i = 0; i < fisicos.Length; i++)
                if (i != encontrado) _abiertosDeMas.Add(fisicos[i].hPhysicalMonitor);
            return true;
        }

        return false;
    }

    // ================= ¿Merece la pena ofrecer esto? =================
    //
    // Mismo criterio que `Brillo.Disponible`: si ningún monitor del equipo gestiona su entrada,
    // el módulo no se dibuja. Una fila que solo sabe decir «no disponible» no es información,
    // es sitio gastado.
    //
    // Se calcula UNA vez y en un trabajador, nunca al montar la barra: leer capacidades de dos
    // monitores puede costar dos segundos y eso en el arranque se nota.
    static bool? _hay;

    public static bool? YaSabemos => _hay;

    // ---------- Y la respuesta se lleva en un LOCAL, no se relee del campo ----------
    //
    // Esto decía `_hay = ...` y después leía `_hay.Value` dos veces, y eso es una carrera
    // con nombre y apellidos. Con dos monitores hay dos islas, las dos preguntan por su
    // cuenta (MainWindow.ModuloMonitores) y un cambio de pantallas llama a `Replantear`,
    // que pone el campo a null. Si eso cae entre la asignación y el `.Value`, salta
    // `InvalidOperationException: Nullable object must have a value`, el trabajador se
    // muere a medias y el módulo no se ofrece — aunque los monitores sí contesten.
    //
    // No es hipotético: está en el registro del usuario, 2026-11-07 12:51:56.221, con las
    // dos islas preguntando a la vez.
    //
    //   ERROR en ver si hay monitores con entrada DDC/CI:
    //   System.InvalidOperationException: Nullable object must have a value.
    //      at NubeBar.Entradas.Disponible()
    //
    // El arreglo no es un lock: es no volver a preguntarle al campo lo que ya se sabe. El
    // campo es solo la caché; la respuesta de ESTA llamada es la del local, y entonces da
    // igual que otro hilo la invalide en medio — lo que devolvemos sigue siendo cierto de
    // lo que acabamos de medir, y la siguiente llamada volverá a medir, que es justo lo
    // que `Replantear` quería.
    public static bool Disponible()
    {
        if (_hay is { } ya) return ya;
        var p = Pantallas();
        bool hay = p.Any(x => x.GestionaEntrada);
        _hay = hay;
        Log.Info($"entrada de vídeo por DDC/CI: " +
                 (hay
                    ? $"{p.Count(x => x.GestionaEntrada)} de {p.Count} monitor(es) la gestionan"
                    : "ningún monitor la gestiona, el módulo no se ofrece"));
        return hay;
    }

    // Cuando cambia la configuración de pantallas hay que volver a preguntarlo todo: capacidades,
    // quién hay y si merece la pena ofrecerlo.
    public static void Replantear()
    {
        Olvidar();
        _hay = null;
    }
}
