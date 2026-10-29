using System;
using System.Linq;
using System.Windows;

namespace NubeBar;

// D7: el supervisor, enganchado al reloj que ya existe.
//
// ---------- Por qué no tiene su propio reloj ----------
//
// Porque la isla ya tiene uno de un segundo, y dos relojes para cosas parecidas es lo que
// acaba descompasado — está escrito en el Tick de la isla a propósito de las métricas del
// centro de control, y vale igual aquí. El supervisor no necesita precisión: necesita
// enterarse en algún momento de los próximos segundos.
//
// Y mira **una vez cada cinco segundos**, no cada tick. Un supervisor que recalcula sus
// cuatro señales cada segundo gasta más de lo que vigila.
public partial class MainWindow
{
    DateTime _ultimaMirada = DateTime.MinValue;
    HyprSalud.Estado _saludAnterior = HyprSalud.Estado.Sano;

    // ---------- La memoria de posiciones, por fin con dueño ----------
    //
    // D3 escribió `HyprRestaurar.Memoria` como una clase con estado y nadie la instanciaba:
    // existía probada y sin usar. Para D7 hace falta de verdad, porque el inventario de
    // cierre tiene que poder decir cuántas posiciones hay recordadas — y "no lo sé porque no
    // hay instancia" no es un inventario.
    //
    // `HyprGesto` sigue SIN instanciar, y eso es una deuda de D3 que D7 no cierra: el
    // arrastre de verdad lo lleva la maquinaria de C4 (`_arrastrada`, `ArrastrandoTarjeta`,
    // `AjustandoAlgo`), que es la que funciona y la que el usuario usa. Así que el
    // inventario pregunta por ESA, no por una instancia que no existe: un inventario que
    // mirara el campo nulo diría siempre "no hay gesto" y sería mentira.
    //
    // ---------- Y por qué esa deuda se queda como está ----------
    //
    // Revisada a conciencia buscando código construido y nunca enchufado — el mismo barrido
    // que encontró el dock sin interruptor y los laterales sin puerta. Veredicto: `HyprGesto`
    // no es una deuda que se pague enchufándola, porque **no se puede** enchufar como estaba
    // pensada, y lo que prometía ya está hecho de otra forma.
    //
    //  - Está escrita para una tecla MANTENIDA: principio, repetición y final. Los atajos van
    //    por `RegisterHotKey`, y `WM_HOTKEY` solo llega al PULSAR — no existe un `WM_HOTKEY`
    //    de soltar. Detectarlo pediría un gancho global de teclado, que en un reemplazo de
    //    shell es demasiado riesgo para esto.
    //  - Y «cancelar de verdad» —devolver la ventana a donde estaba— ya existe: es el comando
    //    número 20, `DeshacerLoUltimo`, que saca el último lote y lo devuelve por el mismo
    //    camino por el que se aplicó.
    //
    // Así que se queda escrita y probada, sin instanciar, y esta nota está aquí para que
    // quien la encuentre no vuelva a recorrerla entera: no es un cabo suelto, es una pieza
    // que la realidad dejó sin sitio.
    readonly HyprRestaurar.Memoria _memoriaHypr = new();

    public static readonly TimeSpan CadaCuantoMirar = TimeSpan.FromSeconds(5);

    void VigilarLaSaludDeHypr()
    {
        if (!_esJefa) return;
        var ahora = DateTime.Now;
        if (ahora - _ultimaMirada < CadaCuantoMirar) return;
        _ultimaMirada = ahora;

        try
        {
            // La capa encendida se mira SIN el modo seguro: si se mirara con él, en modo
            // seguro el supervisor vería la capa apagada, diría "nada que supervisar" y no
            // habría forma de saber si el problema sigue. Lo que hace el modo seguro es que
            // no se organice; no que se deje de mirar.
            bool encendida = NubeHypr.ModoEfectivo(_cfg.NubeHypr.Activo, _cfg.NubeHypr.Modo)
                             != NubeHypr.Modo.Apagado;

            var señales = new HyprSalud.Señales(
                EdadDeLaInstantanea: GestorVentanas.EdadDeLaInstantanea,
                AtajosVivos: HyprTeclas.Cuantos,
                AtajosEsperados: encendida && !HyprSalud.Seguro
                    ? HyprTeclas.MapaDe(_cfg.NubeHypr, _cfg.Barra.AtajoVistaGeneral).Atajos.Count
                    : 0,
                FallosSeguidos: HyprSalud.FallosSeguidos,
                ExcepcionesSeguidas: HyprSalud.ExcepcionesSeguidas,
                CapaEncendida: encendida);

            var d = HyprSalud.Mirar(señales);

            // Solo se apunta cuando el estado CAMBIA. Un renglón de "todo en orden" cada
            // cinco segundos convierte el registro en algo que nadie lee, y entonces el
            // renglón que importa se pierde entre doce mil iguales.
            if (d.Como != _saludAnterior)
            {
                Log.Info($"[D7]  salud: {_saludAnterior} -> {d}");
                Log.Info(HyprSalud.Linea(encendida ? "ready" : "off", _cfg.Version));
                _saludAnterior = d.Como;
            }

            if (d.Que == HyprSalud.Accion.ModoSeguro && HyprSalud.Entrar(d.Porque))
            {
                // Entrar en modo seguro **suelta lo que organiza y deja el resto**: los
                // atajos de NubeHypr se van, la escucha de reglas se va, y la isla, los
                // paneles, el carrusel y la bandeja siguen exactamente igual.
                AplicarAtajosDeHypr();
                AplicarReglasDeHypr();
                QuitarPreviaHypr();
                ApuntarInventarioDeHypr("tras entrar en modo seguro");
            }
        }
        catch (Exception ex)
        {
            // Si el propio supervisor revienta, lo que NO se hace es apagar la capa por
            // ello: el supervisor es lo accesorio y la capa es lo que el usuario pidió.
            Log.Error("el supervisor de NubeHypr", ex);
        }
    }

    // ================= D7: las aplicaciones que se van y vuelven =================
    //
    // Dos piezas: lo último que se supo de cada ventana (para poder apuntar dónde estaba
    // cuando desaparezca) y la memoria por aplicación (para reconocerla cuando vuelva con
    // otro handle). Ver HyprRegreso.cs.
    readonly HyprRegreso.Memoria _regresoHypr = new();
    readonly Dictionary<IntPtr, (string Huella, NubeHypr.Rect Donde, bool Max)> _ultimoSitio = new();

    // D9.1: y quién ha movido cada ventana. Ver HyprRespeto.cs.
    readonly HyprRespeto.Memoria _respetoHypr = new();

    // ================= Un solo recorrido por aviso =================
    //
    // Las tres cosas que hay que mirar cuando cambian las ventanas —las que se van, las que
    // llegan y las que se mueven— se hacen en el MISMO recorrido, con la observación ya
    // hecha. Tres pasadas sobre las mismas ventanas en el mismo aviso sería pagar tres
    // veces por lo mismo, y con cincuenta ventanas abiertas se nota.
    //
    // El orden importa y es este:
    //
    //  1. Las que se fueron: hay que leerlas de `_ultimoSitio` ANTES de actualizarlo.
    //  2. Las que llegaron: se deciden con la memoria de regresos ya puesta al día.
    //  3. Las que se movieron: se comparan con `_ultimoSitio` todavía viejo.
    //  4. Y al final se actualiza `_ultimoSitio` para todas.
    //
    // Hacer el 4 antes del 3 era el error fácil: comparar una ventana consigo misma siempre
    // dice "no se ha movido", y ningún gesto se detectaría jamás.
    void MirarLasVentanas(IReadOnlyList<VentanaModelo> ahora, NubeHypr.Modo modo,
                          bool cambioLaLista, bool cambiaronLosSitios,
                          Func<IntPtr, string> claseDe, Func<IntPtr, string> rutaDe)
    {
        if (modo == NubeHypr.Modo.Apagado)
        {
            _ultimoSitio.Clear();
            return;      // la memoria de respeto NO se borra: tu gesto no caduca por apagar
        }

        var vivas = new HashSet<IntPtr>(ahora.Select(v => v.Hwnd));

        // ---------- 1. Las que se han ido ----------
        foreach (var ida in _ultimoSitio.Where(p => !vivas.Contains(p.Key)).ToList())
        {
            _regresoHypr.Apuntar(ida.Value.Huella, ida.Value.Donde, ida.Value.Max);
            _ultimoSitio.Remove(ida.Key);
            Log.Info($"[D7]  se fue 0x{ida.Key.ToInt64():X6} «{ida.Value.Huella}»: " +
                     $"se apunta que estaba en {ida.Value.Donde.X:0},{ida.Value.Donde.Y:0} " +
                     $"{ida.Value.Donde.Ancho:0}x{ida.Value.Donde.Alto:0}");
        }

        // Y las marcas de las ventanas muertas se sueltan. No por la memoria: porque un
        // hwnd que Windows reutilice nacería marcado, y una ventana nueva no se colocaría
        // por culpa de una que se cerró ayer.
        int olvidadas = _respetoHypr.Limpiar(vivas);
        if (olvidadas > 0)
            Log.Info($"[D9]  {olvidadas} marca(s) de gesto se sueltan: esas ventanas ya no están");

        var areas = Monitores.Todos()
            .Select(m => new NubeHypr.Rect(m.TrabajoX, m.TrabajoY, m.TrabajoAncho, m.TrabajoAlto))
            .ToList();

        var nuevosSitios = new List<(IntPtr Hwnd, string Huella, NubeHypr.Rect Donde, bool Max)>();

        foreach (var v in ahora)
        {
            string clase = claseDe(v.Hwnd), ruta = rutaDe(v.Hwnd);
            var huella = HyprRegreso.Huella(ruta, clase);
            bool esNueva = !_ultimoSitio.TryGetValue(v.Hwnd, out var antes);
            nuevosSitios.Add((v.Hwnd, huella, v.Rectangulo, v.Maximizada));

            // ---------- 2. Las que acaban de aparecer ----------
            if (esNueva)
            {
                if (!cambioLaLista) continue;
                var d = HyprRegreso.Decidir(v, clase, ruta, _regresoHypr, areas, modo);

                // Solo se apunta cuando hay algo que contar. "No hay sitio recordado" es el
                // caso de casi todas las ventanas que abres, y un renglón por cada una
                // llenaría el registro de nada.
                if (d.Hacer == HyprRegreso.Que.Devolver) Log.Info(HyprRegreso.Linea(v, d, modo));
                continue;
            }

            // ---------- 3. Las que se han movido ----------
            if (!cambiaronLosSitios) continue;

            var quien = HyprRespeto.DeQuienFue(antes.Donde, v.Rectangulo,
                                               _respetoHypr.LoQuePedimos(v.Hwnd));
            if (quien != HyprRespeto.Quien.DeFuera) continue;

            // Solo la PRIMERA vez se apunta. Si no, arrastrar una ventana por la pantalla
            // escribiría un renglón por cada fotograma del arrastre.
            if (_respetoHypr.Marcar(v.Hwnd))
                Log.Info(HyprRespeto.LineaDelGesto(v.Hwnd, v.Proceso, quien,
                                                   antes.Donde, v.Rectangulo));
        }

        // ---------- 4. Y ahora sí, lo último que se sabe de cada una ----------
        foreach (var s in nuevosSitios) _ultimoSitio[s.Hwnd] = (s.Huella, s.Donde, s.Max);

        // Las entradas caducadas se tiran: dos horas es "lo que se cerró hace un rato", no
        // "ayer trabajé así".
        int caducadas = _regresoHypr.Caducar();
        if (caducadas > 0)
            Log.Info($"[D7]  {caducadas} sitio(s) recordado(s) han caducado");
    }

    // ================= El inventario de lo que está vivo =================
    //
    // Se mide, no se supone. Cada número sale de quien lo tiene, y un cierre limpio los
    // deja todos a cero. Es lo que permite afirmar "no quedan hooks ni suscripciones
    // atrapadas" en vez de confiarlo al orden de las líneas del cierre.
    HyprSalud.Inventario InventarioDeHypr() => new(
        Atajos: HyprTeclas.Cuantos,
        ReglasEscuchando: _escuchandoReglas,
        VistaEscuchando: _vistaEscuchando,
        PreviaViva: _previa is { IsVisible: true },
        PosicionesRecordadas: _memoriaHypr.Cuantas,
        VentanasVistas: HyprVigilante.Vistas,
        GestoEnCurso: AjustandoAlgo,
        MarcasDeGesto: _respetoHypr.Cuantas,
        SitiosRecordados: _regresoHypr.Cuantas);

    void ApuntarInventarioDeHypr(string cuando)
    {
        var i = InventarioDeHypr();
        Log.Info($"[D7]  inventario de NubeHypr ({cuando}): {i}");
    }

    // ---------- Y el cierre ----------
    //
    // Suelta las siete cosas que `HyprSalud.HayQueSoltar` declara, en un solo sitio, y
    // apunta el inventario antes y después. Si queda algo, el registro lo dice con nombre:
    // "quedan 2" es inútil, "atajos=2" se arregla.
    void SoltarTodoLoDeHypr()
    {
        ApuntarInventarioDeHypr("antes de soltar");
        try
        {
            HyprTeclas.Quitar(_self);              // 1. los atajos globales
            EscucharParaLasReglas(false);          // 2. la escucha de reglas
            EscucharCambios(false);                // 3. la escucha de la vista
            QuitarPreviaHypr();                    // 4. la previsualización
            _memoriaHypr.Vaciar();                 // 5. las posiciones recordadas
            HyprVigilante.Olvidar();               // 6. las ventanas ya vistas
            _respetoHypr.Vaciar();                 // 8. las marcas de gesto
            _regresoHypr.Vaciar();                 // 9. los sitios recordados
            // 7. el gesto en curso, si había. Cancelar devuelve la ventana a donde estaba:
            // cerrar NubeBar a mitad de un arrastre no puede dejarte la ventana a medias.
            PararArrastreDeTarjeta();              // y el arrastre a medias, si había uno
        }
        catch (Exception ex) { Log.Error("soltar lo de NubeHypr al cerrar", ex); }

        var queda = InventarioDeHypr();
        Log.Info($"[D7]  inventario de NubeHypr (después de soltar): {queda}");
        if (!queda.Limpio)
            Log.Info($"[D7]  ATENCIÓN: el cierre no quedó limpio. Lo que hay que soltar es: " +
                     string.Join("; ", HyprSalud.HayQueSoltar));
    }
}
