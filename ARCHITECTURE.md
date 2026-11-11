# How NubeBar works

This is the guided tour: what each part of the program is, where it lives and why
it is built that way. It is meant to be read before touching the code — 236 files
and 85,000 lines are a lot to meet all at once, and almost all of it falls into a
dozen pieces that make sense on their own.

If what you want is to *use* NubeBar, the [README](README.en.md) is the place.
The reasoning behind individual decisions — what was tried first, what broke,
what was chosen instead — lives in the code, in the comment next to the thing it
explains. There is a lot of it, and it is there on purpose.

---

## The shape of the thing

One process. Inside it, **one window per monitor** — the `MainWindow` class — and
the one on the primary screen is *la jefa*, "the boss": it carries whatever has
to exist exactly once (the keyboard hook, the tray, the global shortcuts) so the
others don't duplicate it.

Each of those windows is a transparent strip across its monitor, and the island
is a rounded `Border` inside it. That is the first thing worth knowing, because
it explains a lot of the rest: **the island is not a window**. It is a shape
drawn inside a window that is bigger than it, and that is what lets it grow, slide
away, and open panels out of itself without any of it being a new window that
Windows has to manage.

`MainWindow` is split across 63 `MainWindow.*.cs` files — one per
subject: `MainWindow.Apps.cs`, `MainWindow.Paneles.cs`, `MainWindow.Red.cs`. They
are all the same class. The split is for reading, not for structure.

### Three modes, two of which are the same thing

`Paneles[0].Modo` is `isla`, `barra` or `notch`. The notch is the island with a
different silhouette: flush against the edge with no margin, rounded only on the side
facing the screen, opaque background.

The one decision that makes it cheap is worth knowing before you touch any of it:
**`EsIsla` is `!EsBarra`, not `== "isla"`**. Seventeen places ask `EsIsla` — the
hand-set width, the centred alignment, `Expandir`, editing, the margin — and with the
obvious spelling the notch would have fallen through all seventeen into the bar's
branch, silently, producing an edge-to-edge bar where a notch was asked for. Asked by
the negative, the notch inherits all seventeen and there is nothing to go and find.

What the notch actually changes lives in `PanelNube` as pure functions —
`Esquinas` (which of the four corners take the theme's radius), `MargenEfectivo`,
`BordeEfectivo(modo, posicion)` — so all of it is fixed by tests without opening a
window. The radius itself still comes from the **theme**, which is the rule in
`Forma.cs`: with Cyberpunk at radius 0 the notch is a clean rectangle, and that is not
a bug, that is Cyberpunk.

---

## Configuration

Everything the user can change lives in one file, `%APPDATA%\NubeBar\config.json`,
and the program **reloads it while running**: save the file and the island adapts.
There is no "apply" step anywhere.

Three things about it are worth knowing before you touch it:

**Keys it doesn't recognise are kept.** Config is read into a tree
(`ConfigArbol`) that preserves what it doesn't understand, so a file written by a
newer version doesn't lose anything when an older one opens it.

**`Paneles` is the authority on geometry.** Mode, edge, thickness, margin,
visibility, which monitors, whether it reserves space — all of it lives in
`Paneles[0]`. The old `Barra.*` keys are still read by sixty-odd places in the
code, so they are *derived* from `Paneles` at load time and never written back.
Editing them by hand does nothing.

**Not everyone may write it.** `Config.PuedeEscribir` is inverted on purpose:
only the bar with no arguments and the guardian can write. Every diagnostic is
read-only by construction, so a new one is born protected without anyone having to
remember. That rule exists because the opposite cost four separate accidents where
a development build migrated a real config to a schema the installed version
couldn't read.

Schema changes go through `Esquema.cs`, which migrates forward and refuses to read
a file from the future rather than reading it halfway.

---

## The island, zone by zone

The island has **three zones** — left, centre, right — and each one is a stack of
modules. The layout is a `* Auto *` grid, which has one consequence you will see
in the code: the two star columns always get the same width, so the centre is
genuinely centred, and the narrower side gets padding. That is deliberate.

**The centre zone is always visible.** The left and right ones only appear when
the island expands under the pointer. That is why the apps live in the centre: so
they are one click away without having to approach the island first.

When the bar is **vertical** (bar mode on a side edge), the same three zones stack
downwards instead, and the modules separate on the other axis. There is one helper
for that, `SeparacionDeModulo`, and the reason it exists is that the rule used to
be applied in one place and written nowhere, so every new module was born without
it.

Below the module row sits **the drawer** (`Cajon`): an empty row that panels grow
into. Opening the network panel doesn't create a window — it puts content in the
drawer, grows the island's height, and clips the result to look like a card
hanging from the icon you clicked. In vertical mode there is nowhere to grow, so
panels do get their own window (`VentanaSuperpuesta`).

---

## Modules

A module is a widget: a small piece with a registered id (`reloj`, `apps`,
`bateria`…). `RegistroWidgets` holds them, which is why the same list appears in
the config file, in **Settings → Modules** and in the log — there is only one.

A widget receives `IAnfitrionWidget`: the resolved theme colours, the animation
level, the row thickness, a once-a-second refresh, and four construction helpers
(a button, a glyph, a label, a text). Deliberately not the whole window.

The label helper is worth a mention: `Etiqueta` can reserve the width of its
longest value, so the clock doesn't make the island breathe every minute.

---

## Apps

`Nativo.VentanasAbiertas` enumerates top-level windows and filters out everything
that isn't a real application window — and `NubeBar.exe --ventanas` prints why each
one was discarded, because "my app is missing" is otherwise impossible to debug.

Windows are **grouped by executable** into one icon with a dot per window. Pinned
apps (`Barra.AppsFijadas`) appear whether they are open or not. The order is
whatever you dragged them into, and it is written back to the config.

With several monitors, each island shows the apps on its own screen, and moving a
window from one monitor to the other makes its icon travel with an animation.

Bringing a window to the front is `Nativo.TraerAlFrente`, and it is longer than
you would expect: Windows refuses `SetForegroundWindow` to a process that hasn't
just received input, and NubeBar's windows are `WS_EX_NOACTIVATE`, so clicking the
island doesn't count. The workaround is in the code with its explanation.

---

## Panels

Network, audio, battery, notifications, the Start menu, the window overview, the
tray flyout, monitors. They are all built the same way: content into the drawer,
height animated, clipped as a card.

Two rules they share:

- **A click outside closes the one that is open**, whatever it is. That used to be
  a list of four panels and it is now the default, with a list of exceptions that
  has one entry.
- **How much a panel really needs is asked to its content, not to its container.**
  The drawer sits in a `Star` row, so the container's `DesiredSize` is whatever it
  was given — asking it is a circular conversation. `AltoQueDeVerdadPide` asks the
  content instead.

---

## Themes

A theme is not a palette. It brings its **shape** (how much each piece is rounded,
or whether it goes chamfered), its **typeface**, its glow, and its decorations.
Nine ship with the program, in `temas-de-serie.json`.

Colours are **roles**, about 130 of them with names like `isla.fondo`,
`panel.filo`, `icono.halo`. What a theme doesn't declare is derived from the
background and the text, so a theme can still be three colours. Every accent also
has a *text* version that is not configurable: the same colour corrected until it
can be read against the real background (WCAG, 3.5:1 over the island).

**A theme of yours stores the recipe, not the result**: which theme you started
from and what you changed, not the 130 resolved colours. If the base theme
improves, yours improves with it.

There is a test that forbids writing a colour by hand anywhere in the interface
files. It reads the source and fails on literals.

---

## Talking to Windows

This is the part that is mostly P/Invoke, and the part most likely to surprise:

- **Hiding the taskbar** is `SHAppBarMessage` with auto-hide, plus hiding the
  window. The original state is saved to a file, because a crash would otherwise
  leave the taskbar hidden forever and the next start would read *that* as the
  original state.
- **Reserving space** is a real appbar registration, one per island, each against
  its own monitor's rectangle. Windows only understands screen edges, so a narrow
  island reserves its whole edge.
- **The tray** comes from [ManagedShell](https://github.com/cairoshell/ManagedShell):
  NubeBar becomes the shell's tray host, and gives the icons back on exit.
- **Virtual desktops** through `Slions.VirtualDesktop`.
- **Notifications** are read from the database where Windows stores them, with
  SQLite. The official route would mean packaging and signing the app, so NubeBar
  finds out *after* Windows has shown its own banner — which is why they can appear
  twice, and why the README explains how to turn one off.
- **Monitor inputs** are DDC/CI over the cable (`dxva2.dll`, VCP `0x60`), with the
  monitor's real model read from its EDID.
- **File search** uses the Windows Search index through OleDb.
- **Wallpaper Engine**, if installed, can be started, paused, and read for a colour
  palette.

---

## What's playing

One source, and it needs nothing: `Musica.cs` reads title, artist, playing state,
position, length, thumbnail and which app is playing from
`GlobalSystemMediaTransportControlsSessionManager`. Spotify feeds it like any other
player, so the `spotify` module works on a fresh install with no account, no key and no
network.

### The two layers that aren't there

Both were built on top of this one and both were pulled. They are written down because
they will be asked for again, and because what they leave behind is the interesting
part.

**Spotify's Web API**, signed into with OAuth, adding album name, 640 px cover art and
Liked Songs. Spotify stopped accepting HTTP loopback redirect URIs —
`http://127.0.0.1:PORT` — for apps registered after April 2025, while their
documentation still says loopback is allowed. What remained was HTTPS on loopback with
a self-signed certificate, which puts a browser security warning in front of the user
mid-flow, or a private-use URI scheme their Web API docs don't cover. Three cosmetic
additions don't justify either.

**Synced lyrics** from LRCLIB. These worked, and were pulled for a design reason worth
keeping: the sung line lived in a strip of the island that grows and shrinks with every
word, so the bar breathed in time with the lyrics. A bar that moves while you read it
is not a bar you can read. Tuning the width doesn't fix that — the shape is wrong, and
the right shape (a fixed-height surface that never moves the island) is a different
piece of work.

Two things survived, and they are the reason this section exists at all:

- **`EsSpotify` moved down into `Musica.cs`.** Knowing *who* is playing never needed an
  account: it's a string the media session already hands us. When the top layer came
  off, nothing below it noticed — which is how you find out the layering was right.
- **A widget can declare which modes it exists in.** `spotify` lives off the island's
  elastic width, so it is limited to `isla` and `notch`
  (`DescriptorWidget.SoloEnModos`). The limit is in the *descriptor* and not in an `if`
  inside the widget, for the same reason `PuedeNoVerse` exists: placed and absent, "I
  added it and it doesn't show" means two very different things and there is no way to
  tell which. In the ficha, the library says it before you add it and `--diagmodulos`
  doesn't count it as a broken gap.

## NubeHypr

A window arranger in the style of the Linux tiling managers, built into the bar.
It **ships off**, and when on it has two modes: *simulation*, which works out what
it would do and writes it to the log without moving anything, and *apply*.

Four layouts — floating, horizontal (columns), vertical (rows) and main — plus
**rules** matched on process, class, title or path. Rules don't override one
another: they combine field by field, and whatever a rule leaves blank is left for
another rule or for Windows.

It respects the work area: it subtracts the bar if the bar reserves space, and the
dock if the dock is visible. That calculation lives in one place
(`MainWindow.AreaDeTrabajo.cs`) because the engine, the preview and the
diagnostics all need the same answer.

The sentence that explains a rule in plain language (`HyprGuia.EnPalabras`) is
built from translated **templates with a placeholder**, not from concatenated
pieces — because in English the verb goes after the subject and gluing translated
fragments gives Spanish with English words.

---

## The dock and the side panels

Both are separate always-on-top windows, independent of the bar: you can have one,
both, or neither. They open always, never, or when the mouse reaches the edge of
the screen — the same gesture and the same reader for both, on purpose.

---

## Not leaving you without a shell

NubeBar hides your taskbar. Everything in this section exists because of that one
sentence.

- **One instance at a time**, enforced with a named mutex.
- **A guardian process** — the same executable with `--guardian` — watches the bar.
  If it is killed from Task Manager or hangs, the guardian gives both taskbars back
  in under half a second without restarting Explorer.
- **Panic mode** (`Panico.cs`) decides when the program should give up and hand the
  desktop back. The two mistakes it avoids are symmetrical: surrendering over a
  harmless error, and not surrendering with a broken bar.
- **An unknown `--flag` exits with code 2** instead of starting the bar. Asking an
  old binary for a diagnostic it doesn't have used to mount the whole bar and
  migrate the config.
- **A binary in a phase folder refuses to start as a bar at all.**

---

## Two languages

Spanish and English, chosen the first time you open it and changeable in Settings.
`Textos.T(text)` looks the Spanish string up in a table and returns it unchanged
when there is no entry — which is what allows the translation to be completed
gradually without any intermediate version being broken.

Two things are easy to get wrong here and both have been:

- A string can be **missing from the table**, or **in the table and not routed
  through `T()`**. The second one doesn't show up in any count. `T()` now logs every
  string that reaches it and isn't found, so walking the interface in English and
  reading the log gives the exact list.
- **Dates don't go through the table** — .NET builds them from a culture. That
  culture follows the chosen *language*, not Windows, or you get half a sentence in
  each language.

---

## How it is tested

The test suite lives **inside the executable**: `NubeBar.exe --pruebas report.txt`
runs some thirteen thousand checks and opens no window. It is there and not in a
separate harness because what it protects are rules that have to still be true in
six months.

Alongside it are 29 command-line flags, almost all of them diagnostics — `--capturas` photographs every surface,
`--diagpaneles` measures panels, `--vigilia` runs the bar for a while measuring
itself — all read-only, all unable to touch your config.

The pattern worth copying: when something can only be verified by looking,
**measure it instead**. The separators that shimmered were diagnosed by
screenshotting them once a second and comparing pixel positions, not by staring at
the bar.

---

## Where things are

The file name is the map:

| | |
|---|---|
| `MainWindow.*.cs` | the island, one file per subject |
| `VentanaAjustes*.cs` | the settings window, one file per page |
| `Hypr*.cs`, `NubeHypr.cs` | the window arranger |
| `Diag*.cs`, `MainWindow.Diag*.cs` | diagnostics |
| `Pruebas*.cs` | the test suite |
| `Tema*.cs`, `Roles.cs`, `Catalogo.cs`, `Paleta.cs` | theming |
| `Nativo.cs`, `Monitores.cs`, `AppBar.cs` | talking to Windows |
| `Config.cs`, `ConfigArbol.cs`, `Esquema.cs` | configuration |
| `Musica.cs`, `MainWindow.Spotify.cs` | what's playing |

Everything is under `src/`. The project file, the fonts, the stock themes and the
installer script stay at the root.
