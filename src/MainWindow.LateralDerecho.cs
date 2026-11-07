using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NubeBar;

// E4: el contenido del panel derecho — lo que PASA.
//
// Música, avisos, calendario, el escritorio actual, conexión y batería. Lo que se mira, no lo
// que se toca: eso es el izquierdo.
//
// ---------- Lo que NO puede hacer abrirlo ----------
//
// Parar la música, mover la barra ni reconstruir los demás paneles. Lo primero porque el
// centro multimedia **lee** el estado de la sesión de Windows y no la toca; lo segundo y lo
// tercero porque es una superficie aparte, y eso se mide con `RemontajesDeZonas`.
//
// ---------- El clima ----------
//
// Durante toda la fase E aquí decía que el clima no estaba, y por qué: el documento lo pedía
// «si existe proveedor real», no existía, y dibujar un sol inventado habría sido una maqueta.
// Ese párrafo valía mientras no hubiera proveedor.
//
// Ahora lo hay —Open-Meteo, gratis y sin registro— y el usuario ha pedido que se ponga, así que
// el motivo ya no se sostiene y se cambia en vez de dejarlo ahí mintiendo.
//
// Lo que SÍ sigue en pie es la parte que de verdad importaba: aquí no se pinta ningún dato que
// no haya llegado. Sin lectura no hay números — hay una línea que dice cómo encenderlo. Y con
// el clima apagado la tarjeta es exactamente la de antes.
//
// Las decisiones están en `Clima.cs` y se prueban sin red; el cableado, en `MainWindow.Clima.cs`.
public partial class MainWindow
{
    FrameworkElement? CuerpoDeSeccionDerecha(Lateral.Seccion s) => s.Id switch
    {
        "medios" => SeccionMedios(),
        "avisos" => SeccionAvisos(),
        "calendario" => SeccionCalendario(),
        "escritorio" => SeccionEsteEscritorio(),
        "conexion" => SeccionConexion(),
        "maquina-info" => SeccionClimaYSistema(),
        _ => null,
    };

    // ================= Clima y sistema =================
    //
    // La maqueta junta las dos cosas en UNA tarjeta, y tiene sentido: «dónde estoy y en qué
    // estoy» es lo que uno mira de un vistazo al abrir el panel.
    //
    // Los datos del sistema se LEEN del registro y del entorno. La maqueta ponía «i7-12700H»
    // porque era una maqueta; aquí sale lo que de verdad hay dentro, y cada línea que no se
    // pueda leer simplemente no se pinta, en vez de poner «Desconocido», que ocupa lo mismo y
    // no dice nada.
    //
    // ---------- Por qué el clima va arriba y a lo ancho, y no al lado ----------
    //
    // En la maqueta están en dos columnas. Aquí no, y es una decisión tomada contra la
    // maqueta a sabiendas: el panel mide 340 px, y descontando márgenes de panel y de tarjeta
    // quedan unos 286 para repartir. En dos columnas, «AMD Ryzen 7 7800X3D» se queda con 180
    // y «Nubes dispersas» con 100.
    //
    // Y el usuario ya ha avisado DOS VECES en esta sesión de texto cortado por la derecha.
    // Una tarjeta que se parece más a la maqueta pero corta el nombre del procesador no se
    // parece más a la maqueta: se parece a un prototipo. A lo ancho, el efecto que la maqueta
    // busca de verdad —la temperatura grande— sale mejor, porque caben los 34 px.
    FrameworkElement? SeccionClimaYSistema()
    {
        try
        {
            var pila = new StackPanel();

            // ---------- El clima ----------
            var c = _cfg?.Clima;
            if (c is { Activo: true } && Clima.CiudadValida(c.Ciudad))
            {
                if (Clima.Ultima is { } l) pila.Children.Add(BloqueDeClima(l));
                else
                    // Encendido, con ciudad, y todavía sin dato: o está pidiéndolo o falló.
                    // Las dos cosas se dicen; lo que no se hace es dejar el hueco en blanco
                    // sin explicar por qué está en blanco.
                    pila.Children.Add(Linea(
                        Clima.Error ?? (Clima.EnCurso ? "Pidiendo el tiempo…" : "Sin datos del tiempo todavía."),
                        11, 0.6));
            }
            else
            {
                // ---------- Apagado, o encendido sin ciudad ----------
                //
                // Esto salió mirando la captura: con el clima apagado la tarjeta se titulaba
                // «Clima y sistema» y debajo había tres líneas de sistema y ni rastro del
                // clima. Una cabecera que promete algo que no está no se lee como «lo tengo
                // apagado», se lee como que falta algo.
                //
                // Y encendido pero sin ciudad es el caso que se come la mitad de los informes
                // de fallo de cualquier programa que pida una ubicación. Decir «no hay datos»
                // ahí le echaría la culpa a la red de algo que se arregla escribiendo
                // «Madrid», así que se dice exactamente qué falta.
                var f = Linea(c is { Activo: true }
                        ? "Escribe tu ciudad en Ajustes para ver el tiempo."
                        : "El tiempo está apagado. Puedes encenderlo en Ajustes.",
                    11, 0.6);
                f.TextWrapping = TextWrapping.Wrap;
                pila.Children.Add(f);
            }

            // ---------- La raya ----------
            //
            // Solo si hay clima encima. Una raya separando una cosa de nada es un adorno, y
            // de los que delatan que la tarjeta se montó a trozos.
            if (pila.Children.Count > 0)
                pila.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(_roles.Borde),
                    Margin = new Thickness(0, 10, 0, 12),
                });

            // ---------- El sistema ----------
            // Cada dato con su tono, como las carpetas del panel izquierdo: tres filas del
            // mismo gris eran una lista, y éste es el mismo lenguaje que el resto del panel.
            void Linea3(int icono, string valor, Color tono)
            {
                if (string.IsNullOrWhiteSpace(valor)) return;
                pila.Children.Add(FilaDeDato(icono, valor, null, null, tono: tono));
            }

            Linea3(0xE770, Maquina.Sistema(), Tonos.DeLaCasa(_roles));   // Windows
            Linea3(0xE950, Maquina.Procesador(), Tonos.Cian);            // el chip
            int gb = Maquina.MemoriaGB();
            if (gb > 0) Linea3(0xE964, $"{gb} GB de memoria", Tonos.Lila);

            return pila.Children.Count == 0 ? null : pila;
        }
        catch (Exception ex) { Log.Error("la sección de clima y sistema", ex); return null; }
    }

    // El clima cuando hay dato: glifo grande, temperatura grande, y debajo qué hace y dónde.
    //
    // El glifo va del color de la casa porque es el único sitio de la tarjeta que pide un
    // acento, y el acento sirve para que el ojo caiga primero ahí. El resto, del color del
    // texto: si todo lleva acento, el acento no señala nada.
    FrameworkElement BloqueDeClima(Clima.Lectura l)
    {
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ic = Glifo(char.ConvertFromUtf32(l.Glifo));
        ic.FontSize = 34;
        ic.Foreground = new SolidColorBrush(HyprPinta.Azul(_roles));
        ic.Margin = new Thickness(0, 0, 14, 0);
        ic.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(ic, 0);
        fila.Children.Add(ic);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var grados = Linea(Clima.Grados(l.Grados), 26);
        grados.FontWeight = FontWeights.SemiBold;
        textos.Children.Add(grados);

        // Qué hace y dónde, en una línea y con puntito en medio. En dos líneas separadas
        // esto ocupaba el doble para decir lo mismo, y la tarjeta ya es la última del panel.
        var donde = Linea($"{l.Que} · {l.Ciudad}", 11.5, 0.65);
        donde.TextWrapping = TextWrapping.Wrap;
        textos.Children.Add(donde);

        Grid.SetColumn(textos, 1);
        fila.Children.Add(textos);

        // ---------- Cuándo se midió ----------
        //
        // Solo cuando el dato ya es viejo. Si se refrescó hace tres minutos, poner «hace 3
        // min» es ruido; si la red lleva dos horas caída, es la diferencia entre un dato y una
        // mentira. Se dice también el error, que para eso se guarda.
        var cuanto = DateTime.Now - l.Cuando;
        if (cuanto >= Clima.CadaCuanto)
        {
            string viejo = cuanto.TotalHours >= 1
                ? $"Medido hace {(int)cuanto.TotalHours} h"
                : $"Medido hace {(int)cuanto.TotalMinutes} min";
            if (Clima.Error is { Length: > 0 } e) viejo += $" · {e}";

            var aviso = Linea(viejo, 10.5, 0.5);
            aviso.TextWrapping = TextWrapping.Wrap;
            aviso.Margin = new Thickness(0, 4, 0, 0);
            textos.Children.Add(aviso);
        }

        return fila;
    }

    // ================= Reproduciendo =================
    //
    // Lee la sesión de medios de Windows. **No la toca al abrir**: abrir un panel no puede
    // pausar lo que estás escuchando, y eso es lo que pide E4 explícitamente.
    FrameworkElement? SeccionMedios()
    {
        try
        {
            var m = _musica;
            if (m is null || m.Titulo is not { Length: > 0 })
                return null;        // sin nada sonando, la sección no se pinta

            // ---------- Con el reproductor de verdad ----------
            //
            // Esto era un título, un artista y tres botones de 40 px, todo suelto. Y mientras
            // tanto `MediaCard` existía y la usaba el centro de control: carátula recortada
            // en redondo, título, artista, origen, barra de avance y los tres controles con
            // su estado. Cero llamadas desde aquí.
            //
            // Es el mismo patrón que lleva toda la sesión —construido y sin usar—, esta vez
            // en lo visual. Y por ser la misma pieza, el reproductor del panel y el del
            // centro de control no pueden divergir: el día que uno gane algo, lo gana el otro.
            return (FrameworkElement)MediaCard(
                Musica.Caratula,
                m.Titulo,
                m.Artista,
                Notificaciones.NombreBonito(m.AppId),
                m.Avance,
                () => { Musica.Anterior(); RefrescarDerecho(); },
                () => { Musica.PlayPausa(); RefrescarDerecho(); },
                () => { Musica.Siguiente(); RefrescarDerecho(); },
                m.Sonando);
        }
        catch (Exception ex) { Log.Error("la sección de medios", ex); return null; }
    }

    // ================= Notificaciones =================
    FrameworkElement SeccionAvisos()
    {
        var pila = new StackPanel();
        try
        {
            var avisos = Centro.Actual.PorApp()
                               .SelectMany(g => g.Avisos)
                               .OrderByDescending(a => a.Cuando)
                               .Take(5).ToList();
            if (avisos.Count == 0)
            {
                // Con el estado vacío de verdad, no con una línea gris. `EmptyState` existía
                // y lo usan los paneles de la isla: icono, frase y qué esperar. «No hay
                // avisos.» en texto plano se lee igual que un panel que no ha cargado.
                pila.Children.Add(Card(EmptyState(
                    Lateral.Glifos.Avisos, "No hay avisos",
                    "Lo que llegue de Windows aparecerá aquí")));
                return pila;
            }

            foreach (var a in avisos)
            {
                var caja = new Border
                {
                    Background = new SolidColorBrush(_roles.Superficie),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 7, 10, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                };
                var dentro = new StackPanel();
                var t = Linea(a.Titulo is { Length: > 0 } ? a.Titulo : a.App, 12);
                t.FontWeight = FontWeights.SemiBold;
                t.TextTrimming = TextTrimming.CharacterEllipsis;
                dentro.Children.Add(t);
                if (a.Texto is { Length: > 0 })
                {
                    var c = Linea(a.Texto, 11, 0.75);
                    c.MaxHeight = 42;
                    dentro.Children.Add(c);
                }
                caja.Child = dentro;
                pila.Children.Add(caja);
            }
        }
        catch (Exception ex)
        {
            Log.Error("la sección de avisos", ex);
            pila.Children.Add(Linea("No se han podido leer los avisos.", 11, 0.6));
        }
        return pila;
    }

    // ================= Calendario =================
    // El mes que se está mirando. Null = este mes, que es a donde vuelve cada vez que se
    // abre el panel: el calendario es para mirar hoy, no para navegar y quedarse.
    DateTime? _mesDelCalendario;

    FrameworkElement SeccionCalendario()
    {
        var hoy = DateTime.Now;
        var mes = _mesDelCalendario ?? hoy;
        var pila = new StackPanel();

        // ---------- El mes, con sus flechas ----------
        //
        // La maqueta las pone y hacen falta: un calendario que solo enseña el mes actual
        // sirve para mirar la fecha, no para comprobar en qué día cae algo del mes que viene,
        // que es la otra mitad de para qué se abre un calendario.
        var cab = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        cab.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titulo = Linea(Calendario.Titulo(mes), 13);
        titulo.FontWeight = FontWeights.SemiBold;
        titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 0);
        cab.Children.Add(titulo);

        Button FlechaDeMes(int cuantos, int glifo, string que)
        {
            var g = Glifo(char.ConvertFromUtf32(glifo));
            g.FontSize = 11;
            g.Opacity = 0.7;
            var b = Boton(g, que, () =>
            {
                // Siempre desde el día 1: sumar meses a un día 31 da saltos raros —del 31 de
                // enero al 3 de marzo— y el calendario se salta febrero entero.
                var baseMes = new DateTime(mes.Year, mes.Month, 1);
                var nuevo = baseMes.AddMonths(cuantos);
                _mesDelCalendario = nuevo.Year == hoy.Year && nuevo.Month == hoy.Month
                                  ? null          // de vuelta a este mes: se olvida
                                  : nuevo;
                RefrescarDerecho();
            }, levantar: false);
            b.Padding = new Thickness(6, 0, 6, 0);
            b.MinWidth = 0;
            b.Height = 24;
            return b;
        }

        var atras = FlechaDeMes(-1, 0xE76B, "Mes anterior");      // ChevronLeft
        Grid.SetColumn(atras, 1);
        cab.Children.Add(atras);

        var alante = FlechaDeMes(+1, 0xE76C, "Mes siguiente");    // ChevronRight
        Grid.SetColumn(alante, 2);
        cab.Children.Add(alante);

        pila.Children.Add(cab);

        var rejilla = new UniformGrid { Columns = 7 };

        // Las cabeceras, empezando por el día que diga la cultura: en España lunes, en otros
        // sitios domingo. Un calendario que empiece el día equivocado coloca mal las cinco
        // semanas enteras.
        foreach (var c in Calendario.Cabeceras())
        {
            var t = Texto(c, 10);
            t.Opacity = 0.55;
            t.Margin = new Thickness(0, 0, 0, 4);
            rejilla.Children.Add(t);
        }

        // La rejilla del mes que se mira, con HOY marcado solo si cae en el: navegar a
        // noviembre no puede pintar el 27 de noviembre como si fuera hoy.
        foreach (var d in Calendario.Rejilla(mes, hoy))
        {
            var t = Texto(d.Numero.ToString(), 11.5);
            t.Margin = new Thickness(0, 2, 0, 2);

            if (!d.DeEsteMes) t.Opacity = 0.3;         // los de otro mes, atenuados

            if (d.EsHoy)
            {
                // Hoy en azul: es el foco de este panel, y azul es el color del foco desde D6.
                var marca = new Border
                {
                    Background = new SolidColorBrush(HyprPinta.Azul(_roles) with { A = 0x44 }),
                    BorderBrush = new SolidColorBrush(HyprPinta.Azul(_roles)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(0, 1, 0, 2),
                    Child = t,
                };
                t.Margin = new Thickness(0);
                rejilla.Children.Add(marca);
            }
            else rejilla.Children.Add(t);
        }

        pila.Children.Add(rejilla);
        return pila;
    }

    // ================= Este escritorio =================
    //
    // El resumen del escritorio virtual actual, con la misma `Ficha` que usa el indicador de
    // la barra: si se contara aparte, un día dirían cosas distintas.
    FrameworkElement? SeccionEsteEscritorio()
    {
        try
        {
            if (!Escritorios.Disponible) return null;

            var todos = Escritorios.Todos();
            var fichas = FichasDeEscritorios(todos);
            var actual = fichas.FirstOrDefault(f => f.EsElActual);
            if (actual == null) return null;

            var pila = new StackPanel();

            // ---------- Los escritorios, como pastillas numeradas ----------
            //
            // Lo que pide la maqueta: una fila de números, el actual en azul. Antes era una
            // frase —«Escritorio 1 de 3, el actual, 5 ventanas»— que dice lo mismo pero
            // obliga a leerla, y además no deja ir a ninguno.
            //
            // Éstas sí llevan: pulsar una te lleva a ese escritorio. Es la misma llamada que
            // usa el módulo de la barra, no una segunda forma de cambiar de escritorio.
            var tira = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8),
            };
            for (int k = 0; k < fichas.Count; k++)
            {
                var f = fichas[k];
                var num = Texto(f.Numero.ToString(), 12);
                num.FontWeight = f.EsElActual ? FontWeights.SemiBold : FontWeights.Normal;
                num.Foreground = f.EsElActual ? _acentoLegible : _texto;
                num.Opacity = f.EsElActual ? 1 : 0.7;

                var caja = new Border
                {
                    Width = 30, Height = 28,
                    CornerRadius = new CornerRadius(8),
                    Background = f.EsElActual
                        ? new SolidColorBrush(HyprPinta.Azul(_roles) with { A = 0x3A })
                        : new SolidColorBrush(_roles.SuperficieAlta),
                    BorderBrush = new SolidColorBrush(
                        f.EsElActual ? HyprPinta.Azul(_roles) : _roles.Borde),
                    BorderThickness = new Thickness(1),
                    // Sin margen AQUÍ: la separación entre pastillas la pone el botón, no la
                    // pastilla. Con el margen dentro, el resaltado del ratón —que `EstiloBoton`
                    // pinta sobre el botón ENTERO— salía 6 px más ancho que la pastilla y
                    // asomaba por la derecha. El usuario lo vio: «si me pongo encima de 1 es
                    // como si se sobresaliera».
                    Child = num,
                };

                // El GUID sale de la lista original: `Ficha` lleva lo que se PINTA —numero,
                // nombre, si es el actual— y no el identificador, que es cosa de Windows.
                var id = k < todos.Count ? todos[k].Id : Guid.Empty;
                var b = Boton(caja, $"Ir al escritorio {f.Numero}" +
                                    (f.Ventanas > 0 ? $" · {f.Ventanas} ventana(s)" : " · vacío"),
                              () => { Escritorios.Ir(id); RefrescarDerecho(); }, levantar: false);
                b.Padding = new Thickness(0);
                // La separación, aquí: así el botón mide exactamente lo que la pastilla y el
                // resaltado del ratón cae justo encima de ella. `EstiloBoton` ya añade 2 px
                // por lado, de modo que con 2 más quedan los 6 de antes entre pastillas.
                b.Margin = new Thickness(0, 0, 2, 0);
                tira.Children.Add(b);
            }

            // ---------- Y la rejilla al final ----------
            //
            // Lo pide la maqueta, y además cierra un hueco real: las pastillas te llevan a un
            // escritorio pero no te enseñan qué hay en ellos. Este botón abre la vista
            // general, que es donde se ve.
            //
            // Va con el filo punteado de «esto no es un escritorio, es una acción»: con el
            // mismo aspecto que las pastillas numeradas parecería un escritorio más, y
            // pulsarlo haría algo distinto de lo que promete.
            var rejilla = new Border
            {
                Width = 30, Height = 28,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(_roles.Borde),
                BorderThickness = new Thickness(1),
                Child = Glifo(char.ConvertFromUtf32(0xE8A9)),   // GridView
            };
            if (rejilla.Child is TextBlock gr) { gr.FontSize = 13; gr.Opacity = 0.75; }

            var bRejilla = Boton(rejilla, "Ver todas las ventanas",
                                 () => { CerrarLateral(Lateral.Lado.Derecha); AlternarVista(); },
                                 levantar: false);
            bRejilla.Padding = new Thickness(0);
            tira.Children.Add(bRejilla);

            pila.Children.Add(tira);

            var t = Linea(HyprIndicador.ParaLeer(actual, fichas.Count), 11.5, 0.75);
            pila.Children.Add(t);

            // La segunda línea cuenta lo que la primera NO dice: lo que hay en los DEMÁS
            // escritorios. Repetir «5 ventanas» justo debajo de «5 ventanas» es la saturación
            // que el documento de E prohíbe, y además no informa de nada.
            int enOtros = Math.Max(0, GestorVentanas.Cuantas - actual.Ventanas);
            pila.Children.Add(Linea(
                todos.Count <= 1
                    ? "Es el único escritorio."
                    : enOtros == 0
                        ? $"Los otros {todos.Count - 1} están vacíos."
                        : $"{enOtros} ventana{(enOtros == 1 ? "" : "s")} en los otros " +
                          $"{todos.Count - 1} escritorio{(todos.Count - 1 == 1 ? "" : "s")}.",
                11, 0.7));
            return pila;
        }
        catch (Exception ex) { Log.Error("la sección del escritorio actual", ex); return null; }
    }

    // ================= Conexión y batería =================
    FrameworkElement SeccionConexion()
    {
        // ---------- Dos filas con icono, valor e insignia ----------
        //
        // Antes eran dos líneas de texto: «Cable» y, con batería, «Batería 84 %». Dos datos
        // distintos, sin icono, sin jerarquía y sin decir si eso está bien o mal.
        //
        // Ahora cada uno es una fila con su glifo a la izquierda, el valor en grande y una
        // insignia de estado. La insignia no es adorno: «sin conexión» en coral y «Conectado»
        // en verde se distinguen **sin leer**, que es lo que se le pide a un panel que se
        // mira de reojo.
        var pila = new StackPanel();

        try
        {
            var tipo = Red.Estado();
            string nombre = Red.NombreDeLaConectada();
            bool hay = tipo != Red.Tipo.Sin;

            // El azulejo va del color del ESTADO, no del tipo de red: azul conectado, coral
            // sin conexión. Es lo mismo que dice la insignia, y a propósito — el color se ve
            // antes que la palabra, y esta fila se mira de reojo.
            pila.Children.Add(FilaDeDato(
                !hay ? 0xEB55 : tipo == Red.Tipo.Wifi ? 0xE701 : 0xE839,   // error / wifi / cable
                nombre is { Length: > 0 } ? nombre : tipo.ToString(),
                nombre is { Length: > 0 } ? tipo.ToString() : null,
                StatusBadge(hay ? "Conectado" : "Sin conexión", bien: hay),
                tono: hay ? Tonos.DeLaCasa(_roles) : Tonos.Coral));
        }
        catch (Exception ex) { Log.Error("leer la red para el panel derecho", ex); }

        try
        {
            var b = Bateria.Leer();
            if (b is { HayBateria: true })
            {
                // Lo que importa de una batería no es el número, es si hay que enchufarla.
                bool apurada = b.PorCiento <= 20 && !b.Cargando;

                // ---------- Y cuánto queda ----------
                //
                // La maqueta pone «Quedan 2h 14m», y Windows ya lo da: `Estado.Segundos`.
                // Estaba leído y sin enseñar — «78 %» no contesta la pregunta que uno tiene
                // delante de una batería, que es si llega a la reunión.
                //
                // Vale -1 cuando Windows todavía no lo sabe (recién desenchufado) o cuando
                // está enchufado. En ese caso no se inventa: se dice lo que sí se sabe.
                string apunte = b.Cargando ? "Cargando"
                              : b.Segundos > 0 ? $"Quedan {Lateral.CuantoQueda(b.Segundos)}"
                              : b.Enchufado ? "Enchufada"
                              : "Con batería";

                // El glifo con el NIVEL dibujado, que ya existía en `Bateria.Glifo` y usa el
                // módulo de la barra: una pila medio llena se ve medio llena.
                var ic = Glifo(Bateria.Glifo(b));
                ic.FontSize = 17;
                ic.Opacity = 0.9;
                ic.Width = 22;
                ic.TextAlignment = TextAlignment.Center;
                ic.VerticalAlignment = VerticalAlignment.Center;
                ic.Margin = new Thickness(0, 0, 10, 0);

                pila.Children.Add(FilaDeDato(
                    0, $"{b.PorCiento} %", apunte,
                    StatusBadge(apurada ? "Queda poca" : b.Cargando ? "Cargando" : "Bien",
                                bien: !apurada, neutro: b.Cargando),
                    iconoYaHecho: ic));
            }
        }
        catch (Exception ex) { Log.Error("leer la batería para el panel derecho", ex); }

        if (pila.Children.Count == 0) pila.Children.Add(Linea("Sin datos.", 11, 0.6));
        return pila;
    }

    // Una fila de dato: icono, valor, apunte debajo y una insignia a la derecha. Existe para
    // enseñar «una cosa y su estado» sin inventarse un componente por cada dato.
    // `iconoYaHecho` es para quien trae su propio glifo preparado —la batería, que dibuja el
    // nivel— en vez de un código. Sin esto habría que elegir entre repetir la fila entera o
    // perder el dibujo del nivel.
    // `tono` enciende el azulejo: con un color, el icono va dentro de un cuadrado teñido en
    // vez de suelto y en gris. Es lo que convierte estas filas en un centro de control en vez
    // de una lista — ver `Azulejo`. Sin color se queda como estaba, que es lo que necesita la
    // batería: ella trae su propio dibujo con el nivel pintado (`iconoYaHecho`), y meterlo en
    // un azulejo lo encogería justo donde el nivel se tiene que ver.
    FrameworkElement FilaDeDato(int icono, string valor, string? apunte, UIElement? insignia,
                               TextBlock? iconoYaHecho = null, Color? tono = null)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        UIElement ic;
        if (iconoYaHecho == null && tono is { } color)
        {
            var az = Azulejo(icono, color, 30);
            if (az is FrameworkElement afe) afe.Margin = new Thickness(0, 0, 11, 0);
            ic = az;
        }
        else
        {
            var tb = iconoYaHecho ?? Glifo(char.ConvertFromUtf32(icono));
            if (iconoYaHecho == null)
            {
                tb.FontSize = 15;
                tb.Opacity = 0.85;
                tb.Width = 22;
                tb.TextAlignment = TextAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Margin = new Thickness(0, 0, 10, 0);
            }
            ic = tb;
        }
        Grid.SetColumn(ic, 0);
        g.Children.Add(ic);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var v = Linea(valor, 12.5);
        v.FontWeight = FontWeights.SemiBold;
        v.TextTrimming = TextTrimming.CharacterEllipsis;
        textos.Children.Add(v);
        if (apunte is { Length: > 0 }) textos.Children.Add(Linea(apunte, 10.5, 0.6));
        Grid.SetColumn(textos, 1);
        g.Children.Add(textos);

        if (insignia is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Center;
            fe.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(fe, 2);
            g.Children.Add(fe);
        }
        return g;
    }

    // Repintar solo el panel derecho. Es lo que permite que pulsar «siguiente canción» no
    // reconstruya el izquierdo ni toque la barra.
    void RefrescarDerecho()
    {
        if (!LateralAbierto(Lateral.Lado.Derecha)) return;
        Rellenar(Lateral.Lado.Derecha);
        ColocarLateral(Lateral.Lado.Derecha);
    }
}
