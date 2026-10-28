# Maqueta del rediseño (copia del código)

Copia del código de la maqueta aprobada el 28/10/2026
(<https://claude.ai/artifact/EPD2FVAWaDM8eeFbzKRcEF>, privada). Está aquí para que el
rediseño no dependa de poder abrir esa página: cada color, radio, letra y animación está en
estos ficheros. **Es referencia, no código de la app**: no se compila ni se publica.

| Fichero | Pantalla |
|---|---|
| `Main.dc.html` | Isla e Inicio (tema Profesional) |
| `Laterales.dc.html` | Laterales y dock (tema Profesional) |
| `Red.dc.html`, `Audio.dc.html`, `Bateria.dc.html`, `Avisos.dc.html`, `Bandeja.dc.html` | Los paneles que cuelgan de la isla (Profesional) |
| `Anime.dc.html` | Temas anime: Sakura, Samurái, Otoño y Sad, con su selector (objeto `TEMAS`) |
| `Temas.dc.html` | Oficina, Videojuegos, Invierno y Cyberpunk, con su selector (objeto `TEMAS`) |
| `Paneles.dc.html` | Todos los paneles a la vez en cada uno de los 9 temas (objeto `T`) |
| `Estudio.dc.html` | El Estudio de temas, funcional |
| `canvas.json` | La disposición de las pantallas en la maqueta |
| `temas.json` | **Los valores de los 9 temas en limpio**: el punto de partida de las fases 0 y 1 |

Notas para leerlos:

- Son ficheros `.dc.html` (HTML con plantillas `{{…}}` y una clase `Component` en JS). Los
  valores de cada tema están en los objetos `TEMAS` / `T` del script.
- Las imágenes `/_blob/…` son los iconos reales de las apps, guardados en la maqueta; aquí
  no cargan, y no hace falta: en la app los da Windows (`Iconos`, `Nativo.Icono`).
- Lo que en la maqueta es CSS tiene su equivalente WPF descrito en
  `docs/REDISENO-VISUAL.md` (degradados que rotan, chaflán, glitch, barrido, partículas).
