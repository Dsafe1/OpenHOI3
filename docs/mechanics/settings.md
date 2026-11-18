# Settings: the player's `settings.txt`

The player's settings are the original's own: `settings.txt` in `Documents\Paradox Interactive\Hearts of Iron III`, the
file the original writes as it is played - **not** the one shipped beside its executable. Read as a session starts
(`Content/Settings`, `CUserSettingsReader`) by the decision of 2026-10-15, and **written as the original writes
it** by the decision of 2026-11-04 (`docs/compatibility.md`, entry 13): by the game options window's Apply (`gameoptions.md`)
and as a session ends, the original writing it every time a game is left (`CUserSettingsWriter`). With no file, the
original's defaults stand. The same folder holds the player's message settings, `messagetypes_custom.txt`, which the
in-game menu writes too (`ingamemenu.md`, section 5). **With a mod both are the mod's player folder's**,
`...\Hearts of Iron III\<user_dir>`, the original's write folder then (`mods.md`, section 2; compatibility entry 13 says
where the original's reading falls through and this does not). Read out of the original
(`reversing/findings/FINDINGS-settingswindow.md`, sections B and C) and set against a reference file.

## 1. What is read

The file is Clausewitz script. Every key the original's writer writes is read, so that it is written back whole; the
defaults are the original's constructors' [exe]. A number is a figure, not thousandths.

| key | default | what it does here |
| --- | --- | --- |
| `gui = { language }` | `l_english` | read; nothing uses it yet |
| `gui = { has_set_language }` | no | read and written |
| `graphics = { size = { x y } }` | 1024 x 768 | **a width under 1024 is made 1024 and a height under 768 is made 768** [exe]; the original's constructor takes the desktop's size, which nothing here asks. Not applied: the game options window's resolution (`gameoptions.md`, *Ours*) |
| `graphics = { refreshRate fullScreen shadows anisotropic_filtering adapter vsync debugmonitor }` | 60, yes, no, 0, 0, yes, no | read and written; not applied |
| `graphics = { shadowSize }` | 2048 | **held to 256 under 1 and to 4096 above it** [exe]; not applied |
| `graphics = { multi_sampling }` | 0 | **applied as a session starts**: 2, 4 or 8 samples, the engine's - a count between taken down |
| `graphics = { gamma }` | 50 | **applied**: the screen scaled by a fiftieth of it, held to 0.1 and 1.9 (`gameoptions.md`, section 3) |
| `sound_fx_volume`, `music_volume`, `master_volume` | 100 | read and written; there is no sound |
| `scroll_speed`, `zoom_speed`, `mouse_speed` | 50 | read and written; `zoom_speed` sizes a wheel notch and its pull towards the pointer as the original's (`mapunits.md`, section 7), the others not used |
| `camera_rotation_speed`, `ambient_volume` | 50 | read and written; read by nothing else in the original either |
| `pause_on_event` | yes | read and written |
| `mapRenderingOptions = { alwaysCounters }` | yes (the original's constructor: no) | **only `yes` is supported** (`docs/compatibility.md`, entry 12): the map always shows counters and the weather map mode its `_simple` icons (`weather.md`, section 6); a `no` is reported, kept and written |
| `mapRenderingOptions = { render_hierarchy }` | yes | **applied**, as it is read and from Apply on (as read only since 2026-11-07): a selected unit's hierarchy drawn on the map (the `H` key turns it too) |
| `mapRenderingOptions = { simpleWater }` | no | **applied**: the sea by water.fx's simple techniques where yes (`mapdraw.md`), as it is read and from Apply on, the next frame (since 2026-11-07; a restart until then) |
| `mapRenderingOptions = { renderTrees waves }` | yes, yes | applied as they are read - the trees and the surf (`mapdraw.md`); a change by Apply waits for the next game |
| `mapRenderingOptions = { onmap }` | yes | read and written |
| `mapRenderingOptions = { show_unit_names }` | no | **applied** since 2026-11-14: a near counter carries its unit's name where yes, its leader's where no, as it is read and from Apply on (`mapunits.md`, section 2) |
| `mapRenderingOptions = { counter_distance }` | 100 | read and written; the height under which units are seen from near (`reversing/findings/FINDINGS-camera.md`) - the original's figures, or with `alwaysCounters` the near counter, and here always the near counter and the near battle marker, the far ones from it up to 500 (`mapunits.md`, section 7). Until 2026-11-12 the near counter went by a new install's 100 whatever the setting said |
| `mapRenderingOptions = { counter_scale }` | 1 | **applied** since 2026-11-07: it multiplies a far counter's growth with rank, as it is read and from Apply on (`mapunits.md`, section 2) |
| `lastplayer`, `lasthost`, `serveradress` | "", "", "diplomacy.valkyrienet.com" | read and written; there is no game of several players |
| `debug_saves` | 0 | read and written |
| `autosave` | `YEARLY` | read and written, and the autosave's frequency (`savegame.md`, section 11). A name that is none of the six is reported and read as `YEARLY` |
| `simple`, `categories` | yes, six noughts | read and written; what they mean is not read |
| `use_legacy_camera` | no | read and written |
| `outliner` | 10 4 5 6 | **applied**: the outliner's sections (`outliner.md`, section 3). What the outliner changes is not carried back into it yet |

## 2. How it is written

The original's writer, its order and its format [exe], in Windows-1252:

1. `gui`, `language` and then `has_set_language=yes` only where set - and no `language` line for a language that is
   none of its thirteen;
2. `graphics`: `size` with `x` and `y`, then `refreshRate`, `fullScreen`, `shadows`, `shadowSize`, `multi_sampling`,
   `anisotropic_filtering`, `gamma`, and three only off their defaults - `adapter` where not 0, `vsync=no` where off,
   `debugmonitor=yes` where on and the adapter 0;
3. `sound_fx_volume`, `music_volume`, `scroll_speed`, `camera_rotation_speed`, `zoom_speed`, `mouse_speed`,
   `master_volume`, `ambient_volume`, then `pause_on_event=no` only where off;
4. `mapRenderingOptions`: `renderTrees`, `onmap`, `simpleWater`, `alwaysCounters`, `counter_distance`, `waves`,
   `counter_scale`, `render_hierarchy`, `show_unit_names`;
5. `lastplayer`, `lasthost`, `serveradress` quoted, `debug_saves`, `autosave` quoted, `simple`, `use_legacy_camera`,
   `categories` only where it has any, `outliner`.

A block is `key=`, `{`, its lines, `}`; only `size`'s `x` and `y` are indented, by a tab; a figure has six places
(`55.000000`), a whole number none, a flag is `yes` or `no`; `categories` and `outliner` put their numbers on one line,
each followed by a space, and the brace after them. Every line ends CR LF, and a blank line follows `size`'s block -
both seen in every file the original writes, not read.

**Checked**: `OpenHOI3.SaveCheck settings [file]` reads a file and writes it again in memory: on 2026-11-04 the
reference base game file and BlackICE's came out **byte for byte**, 825 and 823 bytes. Nothing is written to the
disk by the check.

## Not established

- What `simple` and `categories` mean.
- Which file a mod reads; the original keeps one in a mod's user folder too, and no mod is played here yet.
