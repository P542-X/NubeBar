using System;
using System.Collections.Generic;
using System.Linq;

namespace NubeBar;

// E2: las pruebas del dock.
//
// Puras: ventanas inventadas y rectángulos de pantalla inventados. Eso permite comprobar lo
// que no se puede provocar a mano —cuarenta ventanas abiertas, un monitor de 1024 px, el dock
// más largo que la pantalla— sin abrir ni una ventana de verdad.
public static partial class Pruebas
{
    internal static void CorrerE2()
    {
        E2LoQueLlevaElDock();
        E2UnaAppFijadaYAbiertaSaleUnaVez();
        E2DondeSeColoca();
        E2CuandoSeVe();
        E2ElMenuDeCadaElemento();
    }

    static VentanaReal Vent(string app, bool activa = false) =>
        new() { App = app, Exe = app + ".exe", Titulo = app, Activa = activa, Hwnd = new IntPtr(app.GetHashCode()) };

    // ================= Qué lleva =================
    static void E2LoQueLlevaElDock()
    {
        Grupo("E2: el dock lleva las fijadas y luego lo abierto");

        var d = Dock.Montar(
            new[] { "firefox", "code" },
            new[] { Vent("discord"), Vent("notepad") });

        Igual(5, d.Count, "dos fijadas, un separador y dos abiertas");
        Igual("firefox", d[0].Clave, "las fijadas van primero");
        Igual("code", d[1].Clave, "y en SU orden, el que les dio el usuario");
        Vale(d[2].EsSeparador, "después un separador");
        Vale(!d[3].Fijada && !d[4].Fijada, "y detrás lo abierto que no está fijado");

        // Las abiertas no fijadas van por nombre: sin un orden, cada refresco las barajaría.
        Igual("discord", d[3].Clave, "lo abierto se ordena por nombre");
        Igual("notepad", d[4].Clave, "para que el dock no baile en cada refresco");

        // ---------- El separador solo si hay las dos cosas ----------
        //
        // Un separador al principio o al final es una raya suelta que no separa nada.
        Vale(Dock.Montar(new[] { "firefox" }, Array.Empty<VentanaReal>()).All(e => !e.EsSeparador),
             "solo fijadas: sin separador");
        Vale(Dock.Montar(Array.Empty<string>(), new[] { Vent("discord") }).All(e => !e.EsSeparador),
             "solo abiertas: sin separador");
        Igual(0, Dock.Montar(Array.Empty<string>(), Array.Empty<VentanaReal>()).Count,
              "sin nada, un dock vacío");

        // ---------- Varias ventanas del mismo programa ----------
        //
        // Son UN elemento con cinco ventanas, no cinco elementos. Es lo que distingue un dock
        // de una lista de ventanas.
        var muchas = Dock.Montar(Array.Empty<string>(),
                                 Enumerable.Range(0, 5).Select(_ => Vent("opera")).ToList());
        Igual(1, muchas.Count, "cinco ventanas del mismo programa son un elemento");
        Igual(5, muchas[0].Ventanas, "que sabe cuántas tiene");
        Vale(muchas[0].Abierta, "y que está abierta");

        // ---------- La activa ----------
        var conActiva = Dock.Montar(Array.Empty<string>(), new[] { Vent("opera"), Vent("code", activa: true) });
        Vale(conActiva.Single(e => e.Clave == "code").Activa, "se sabe cuál es la de delante");
        Vale(!conActiva.Single(e => e.Clave == "opera").Activa, "y cuál no");

        // ---------- El tope ----------
        //
        // Con cuarenta ventanas, un dock de cuarenta iconos ocupa la pantalla y deja de ser
        // un dock.
        var tope = Dock.Montar(new[] { "firefox" },
                               Enumerable.Range(0, 40).Select(i => Vent("app" + i)).ToList(),
                               tope: 10);
        Igual(10, tope.Count, "se corta en el tope");
        Vale(tope[0].Fijada, "y las fijadas no se caen: son las que el usuario eligió");
        Vale(!tope[^1].EsSeparador, "y el corte no deja un separador colgando al final");

        // ---------- Lo que no revienta ----------
        Vale(Dock.Montar(null!, null!).Count == 0, "sin listas no revienta");
        Vale(Dock.Montar(new string?[] { null, "", "  " }!, Array.Empty<VentanaReal>()).Count == 0,
             "las fijadas vacías se ignoran");
        Vale(Dock.Montar(new[] { "firefox", "FIREFOX", "firefox.exe" }, Array.Empty<VentanaReal>())
                 .Count == 1,
             "y una fijada repetida de tres formas distintas es una sola");

        // Todo elemento se puede leer en voz alta: el dock es iconos, y sin esto quien use un
        // lector de pantalla oye «botón» nueve veces.
        foreach (var e in d) Vale(e.ParaLeer.Length > 0, "cada elemento se puede leer");
        Vale(d[0].ParaLeer.Contains("anclada"), "y dice si está anclada");
        Vale(conActiva.Single(e => e.Clave == "code").ParaLeer.Contains("delante"),
             "y cuál es la de delante");
    }

    // ================= Fijada Y abierta =================
    static void E2UnaAppFijadaYAbiertaSaleUnaVez()
    {
        Grupo("E2: una app fijada y abierta no sale dos veces");

        // El fallo clásico de las barras de tareas: dos iconos del mismo programa, uno porque
        // está anclado y otro porque está abierto.
        var d = Dock.Montar(new[] { "firefox", "code" },
                            new[] { Vent("firefox", activa: true), Vent("firefox"), Vent("discord") });

        Igual(1, d.Count(e => e.Clave == "firefox"), "firefox sale UNA vez");

        var ff = d.Single(e => e.Clave == "firefox");
        Vale(ff.Fijada, "en su sitio de fijada");
        Igual(2, ff.Ventanas, "con sus dos ventanas");
        Vale(ff.Activa, "y sabiendo que es la de delante");
        Igual(0, d.IndexOf(ff), "y conserva su orden de fijada, el primero");

        // Y la fijada que NO está abierta sigue estando, apagada.
        var code = d.Single(e => e.Clave == "code");
        Vale(code.Fijada && !code.Abierta, "una fijada sin abrir sigue en el dock, cerrada");
        Vale(code.ParaLeer.Contains("no abierta"), "y se dice");

        // Mayúsculas y rutas: la clave normaliza igual que NubeHypr, así que «Discord.exe» de
        // una ventana y «discord» de una fijada son lo mismo.
        var mezcla = Dock.Montar(new[] { "Discord" },
                                 new[] { new VentanaReal { App = "Discord", Exe = @"C:\x\Discord.exe" } });
        Igual(1, mezcla.Count, "la fijada y la ventana se reconocen como el mismo programa");
        Vale(mezcla[0].Fijada && mezcla[0].Abierta, "fijada y abierta a la vez");
    }

    // ================= Dónde se coloca =================
    static void E2DondeSeColoca()
    {
        Grupo("E2: el dock se coloca en el borde opuesto a la barra");

        // Un monitor de 2560×1440 con la barra arriba de 38 px: el área útil empieza en 38.
        var util = new NubeHypr.Rect(0, 38, 2560, 1402);

        // ---------- El borde opuesto ----------
        //
        // Dos superficies permanentes en el mismo borde se pisan y hay que decidir cuál cede.
        Igual(NubeHypr.Borde.Abajo, Dock.BordeDelDock(NubeHypr.Borde.Arriba), "barra arriba, dock abajo");
        Igual(NubeHypr.Borde.Arriba, Dock.BordeDelDock(NubeHypr.Borde.Abajo), "y al revés");
        Igual(NubeHypr.Borde.Derecha, Dock.BordeDelDock(NubeHypr.Borde.Izquierda), "barra a la izquierda, dock a la derecha");
        Igual(NubeHypr.Borde.Izquierda, Dock.BordeDelDock(NubeHypr.Borde.Derecha), "y al revés");

        // ---------- Abajo, centrado ----------
        var abajo = Dock.Sitio(util, NubeHypr.Borde.Abajo, 8);
        Vale(abajo.Ancho < util.Ancho, "el dock no ocupa toda la pantalla");
        Vale(Math.Abs((abajo.X + abajo.Ancho / 2) - (util.X + util.Ancho / 2)) < 1,
             "está centrado en el eje largo");
        Vale(abajo.Y + abajo.Alto <= util.Y + util.Alto, "y no se sale por abajo");
        Vale(abajo.Y + abajo.Alto > util.Y + util.Alto - 80, "pegado a su borde");

        // ---------- Arriba ----------
        var arriba = Dock.Sitio(util, NubeHypr.Borde.Arriba, 8);
        Vale(arriba.Y >= util.Y, "arriba no se mete debajo de la barra");
        Igual(abajo.Ancho, arriba.Ancho, "y mide lo mismo");

        // ---------- En los laterales se pone de pie ----------
        var izq = Dock.Sitio(util, NubeHypr.Borde.Izquierda, 8);
        Vale(izq.Alto > izq.Ancho, "a la izquierda el dock es una columna");
        Vale(izq.X >= util.X, "pegado al filo izquierdo");
        Vale(Math.Abs((izq.Y + izq.Alto / 2) - (util.Y + util.Alto / 2)) < 1, "centrado en vertical");

        var der = Dock.Sitio(util, NubeHypr.Borde.Derecha, 8);
        Vale(der.X + der.Ancho <= util.X + util.Ancho, "y a la derecha no se sale");
        Igual(izq.Alto, der.Alto, "los dos laterales miden igual");

        // ---------- Crece con los elementos ----------
        Vale(Dock.Sitio(util, NubeHypr.Borde.Abajo, 12).Ancho >
             Dock.Sitio(util, NubeHypr.Borde.Abajo, 4).Ancho,
             "más elementos, más largo");

        // ---------- Pero no más que la pantalla ----------
        //
        // Con treinta iconos en un portátil, un dock más largo que el monitor se sale por los
        // dos extremos a la vez y no se ve ni el primero ni el último.
        var estrecha = new NubeHypr.Rect(0, 0, 1024, 768);
        foreach (int n in new[] { 20, 40, 100 })
        {
            var r = Dock.Sitio(estrecha, NubeHypr.Borde.Abajo, n);
            Vale(r.Ancho <= estrecha.Ancho, $"con {n} elementos sigue cabiendo de ancho");
            Vale(r.X >= estrecha.X, $"con {n} no se sale por la izquierda");
            Vale(r.X + r.Ancho <= estrecha.X + estrecha.Ancho, $"con {n} tampoco por la derecha");

            var v = Dock.Sitio(estrecha, NubeHypr.Borde.Izquierda, n);
            Vale(v.Alto <= estrecha.Alto, $"con {n} cabe de alto en vertical");
            Vale(v.Y >= estrecha.Y && v.Y + v.Alto <= estrecha.Y + estrecha.Alto,
                 $"con {n} no se sale por arriba ni por abajo");
        }

        // ---------- Un dock vacío no mide en negativo ----------
        foreach (var b in Enum.GetValues<NubeHypr.Borde>())
        {
            var r = Dock.Sitio(util, b, 0);
            Vale(r.Ancho > 0 && r.Alto > 0, $"con 0 elementos en {b} sigue siendo un rectángulo");
        }

        // ---------- Un monitor que no empieza en 0 ----------
        //
        // El segundo monitor empieza donde acaba el primero. Un dock que colocara en
        // coordenadas del monitor en vez de del escritorio saldría en la pantalla de al lado.
        var segundo = new NubeHypr.Rect(2560, 0, 1080, 1920);
        var enElSegundo = Dock.Sitio(segundo, NubeHypr.Borde.Abajo, 6);
        Vale(enElSegundo.X >= 2560, "en el segundo monitor, el dock sale en el segundo monitor");
        Vale(enElSegundo.X + enElSegundo.Ancho <= 2560 + 1080, "y no se escapa al tercero");
    }

    // ================= Cuándo se ve =================
    static void E2CuandoSeVe()
    {
        Grupo("E2: esconder y mostrar el dock");

        Igual(Dock.Cuando.Siempre, Dock.Leer(null), "de serie, siempre visible");
        Igual(Dock.Cuando.Siempre, Dock.Leer("loquesea"), "y un valor raro no lo esconde");
        Igual(Dock.Cuando.Nunca, Dock.Leer("nunca"), "«nunca» se entiende");
        Igual(Dock.Cuando.AlAcercarse, Dock.Leer("ALACERCARSE"), "sin distinguir mayúsculas");

        Vale(Dock.SeVe(Dock.Cuando.Siempre, false, false), "siempre, se ve sin ratón cerca");
        Vale(!Dock.SeVe(Dock.Cuando.Nunca, true, true), "nunca, no se ve ni con el ratón encima");

        // ---------- Y el detalle que importa ----------
        //
        // Con «al acercarse», el dock tiene que seguir visible mientras el ratón esté ENCIMA.
        // Si solo mirara la franja del borde, se escondería justo cuando vas a pulsar un
        // icono, porque al subir el ratón al dock ya no estás en la franja.
        Vale(Dock.SeVe(Dock.Cuando.AlAcercarse, true, false), "al acercarse al borde, aparece");
        Vale(Dock.SeVe(Dock.Cuando.AlAcercarse, false, true),
             "y sigue visible con el ratón encima, aunque ya no estés en la franja");
        Vale(!Dock.SeVe(Dock.Cuando.AlAcercarse, false, false), "lejos, se esconde");

        // ---------- La franja ----------
        var util = new NubeHypr.Rect(0, 0, 2560, 1440);
        Vale(Dock.RatonCerca(util, NubeHypr.Borde.Abajo, 1280, 1438), "el ratón al pie, cerca");
        Vale(!Dock.RatonCerca(util, NubeHypr.Borde.Abajo, 1280, 700), "en medio, lejos");
        Vale(Dock.RatonCerca(util, NubeHypr.Borde.Izquierda, 1, 700), "pegado a la izquierda, cerca");
        Vale(!Dock.RatonCerca(util, NubeHypr.Borde.Izquierda, 2559, 700), "en el otro filo, no");

        // ---------- Y no se saca el dock de otra pantalla ----------
        //
        // Con dos monitores, acercarse al filo de uno no puede hacer aparecer el dock del
        // otro.
        Vale(!Dock.RatonCerca(util, NubeHypr.Borde.Abajo, 3000, 1438),
             "un ratón fuera de este monitor no cuenta");
        Vale(!Dock.RatonCerca(util, NubeHypr.Borde.Abajo, 1280, 2000), "ni por debajo de él");
    }

    // ================= El menú =================
    static void E2ElMenuDeCadaElemento()
    {
        Grupo("E2: el menú de un elemento del dock");

        // ---------- Cerrada y fijada ----------
        var cerrada = new Dock.Elemento("code", "Code", Fijada: true);
        var m1 = Dock.MenuDe(cerrada);
        Vale(m1.Contains(Dock.Accion.Abrir), "a una cerrada se le ofrece Abrir");
        Vale(!m1.Contains(Dock.Accion.Cerrar), "y NO Cerrar: no hay nada que cerrar");
        Vale(m1.Contains(Dock.Accion.Soltar), "una fijada se puede quitar del dock");
        Vale(!m1.Contains(Dock.Accion.Fijar), "y no se le ofrece fijar lo que ya está fijado");

        // ---------- Abierta con una ventana ----------
        var una = new Dock.Elemento("opera", "Opera", Fijada: false, Ventanas: 1);
        var m2 = Dock.MenuDe(una);
        Vale(m2.Contains(Dock.Accion.Activar), "a una abierta, traerla al frente");
        Vale(!m2.Contains(Dock.Accion.VerVentanas), "con una sola ventana no hay lista que ver");
        Vale(m2.Contains(Dock.Accion.Fijar), "y se puede anclar");
        Vale(m2.Contains(Dock.Accion.Cerrar), "y cerrar");
        Igual(Dock.Accion.Cerrar, m2[^1], "Cerrar va la última: es la destructiva");

        // ---------- Abierta con varias ----------
        var varias = new Dock.Elemento("opera", "Opera", Fijada: false, Ventanas: 4);
        Vale(Dock.MenuDe(varias).Contains(Dock.Accion.VerVentanas),
             "con cuatro ventanas sí hay lista que ver");
        Vale(Dock.Rotulo(Dock.Accion.Cerrar, varias).Contains("4"),
             "y cerrar dice cuántas se van a cerrar");
        Vale(!Dock.Rotulo(Dock.Accion.Cerrar, una).Contains("1 ventana"),
             "con una sola, «Cerrar» a secas");

        // ---------- El separador no tiene menú ----------
        Igual(0, Dock.MenuDe(Dock.Separador).Count, "un separador no tiene menú");

        // ---------- Todo rótulo existe y la destructiva está marcada ----------
        foreach (var a in Enum.GetValues<Dock.Accion>())
            Vale(Dock.Rotulo(a, varias).Length > 0, $"la acción {a} tiene rótulo");

        Vale(Dock.EsDestructiva(Dock.Accion.Cerrar), "cerrar es destructiva: va en coral");
        Vale(!Dock.EsDestructiva(Dock.Accion.Fijar), "anclar no");
        Vale(!Dock.EsDestructiva(Dock.Accion.Activar), "ni activar");
    }
}
