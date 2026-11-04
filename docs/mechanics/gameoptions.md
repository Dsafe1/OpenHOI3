# The game options window

`settings.gui`'s `menu_settings_ingame`, which the in-game menu's Game Options opens (`ingamemenu.md`): four tabs - Game,
Video, Audio, Controls - an Apply and a Back, over the menu's picture. It holds the player's `settings.txt` (`settings.md`)
and writes it. Read out of the original on 2026-11-04 (`reversing/findings/FINDINGS-settingswindow.md`), and set against
the maintainer's `settings.txt`, which the original wrote; that Apply with nothing to restart shuts the whole menu the
maintainer confirmed in the original the same day. `confirmed` unless marked.

**[exe]** marks a rule the original's executable holds, **[mod: ...]** what a file may change.

## 1. Opening it, the tabs, Back

- **Game Options** shows the window and hides the menu's six buttons; the menu's window and picture stay up behind it.
  It pauses nothing of its own - the menu has. Nothing in it has a tooltip [exe].
- **The tabs** `settings_tab_1` to `_4` [mod: settings.gui] are **plain buttons**, not checkboxes: each hides the tab
  shown and shows `menu_settings_GAME`, `_VIDEO`, `_AUDIO` or `_CONTROLS` in its place [exe]. Nothing marks the one
  chosen. **The window opens on Video the first time** and afterwards on the tab last shown [exe].
- **Each opening makes every control again from the settings** - all but the autosave frequency's, which is read from
  them once, as the window is made [exe]. So what was not applied is dropped at the next opening, the autosave choice
  apart.
- **Back** (`BackButton`, no shortcut) puts the three volumes back as they were when the window opened, then runs the
  menu's close, which goes **back to the menu's six buttons** (`ingamemenu.md`, section 2). **Escape**, which Back does
  not take, closes the menu the same way **without** putting the volumes back (*inferred*).
- `SettingsMustRestartWindow`, in the same file, is never shown [exe]; the restart notice is a `DefaultInfoDialog`
  (section 2).

## 2. Apply

`ApplyButton`, in order [exe]:

1. **every tab's choices into the settings** - the Video tab's boxes and sliders, the autosave frequency, the gamma, the
   volumes (and they become what Back puts back), scroll speed and zoom speed, `counters_checkbox` and
   `legacy_camera_checkbox` as they stand;
2. **whether a restart is asked**: where the multisampling chosen is not the settings', or the mode or the refresh rate
   chosen is not the one the game runs at. The running rate is looked for **among the first mode's rates**, as far as the
   chosen mode has rates - the original's slip, which can ask a restart with nothing changed where the first mode's
   rates are not the running mode's. The size and rate chosen are stored either way. **Nothing changes the screen**: they
   take effect at the next start;
3. **`settings.txt` written** (`settings.md`, section 2). Where that fails, `DefaultInfoDialog` says `SM_ALERT` "Alert",
   `SM_WP` "Error writing to the settings-file. Check that the file is not write protected and try again" [mod:
   frontend.csv] - and the window stays up, the choices already in force;
4. **with a restart asked**, `DefaultInfoDialog` says `RESTART_REQ` "Restart Required", `RESTART_REQ_DESC` "You must
   restart the game for these changes to take effect." [mod: misc.csv], and the window closes once - back to the menu's
   six buttons, the notice over them;
5. **without**, the window's close runs **twice**: the first goes back to the six buttons, the second finds nothing of
   the menu's own up and **shuts the menu** - the game left paused, the menu having been opened again on the way
   (`ingamemenu.md`, section 2). Confirmed in the original.

## 3. The tabs

### Game

`autosave_value` [mod: settings.gui] shows the frequency's key, which is its name - `NEVER` "Never", `WEEKLY`, `MONTHLY`
"Monthly", `HALFYEAR` "Half Year", `YEARLY` "Yearly", `FIVE_YEAR` "Five Years" [mod: localisation]. `increase_` steps
up and **wraps from Five Years to Never**, `decrease_` steps down and wraps the other way [exe]. It reaches the settings
only on Apply, and **outlives Back**: shown again at the next opening and stored by any later Apply. Here nothing
autosaves yet, so the choice is kept and written and does nothing else.

### Video

| control | holds | the rule [exe unless marked] | takes effect |
| --- | --- | --- | --- |
| `resolution_value` and its buttons | `graphics.size` | the screen's modes in their order, wrapping; a change sets the rate back to the mode's first. Text `1920x1080`, and while a restart is waiting the one asked for in brackets, `1920x1080 (1600x900)` | the next start |
| `refreshrate_value` and its buttons | `graphics.refreshRate` | the chosen mode's own rates, wrapping. Text `60 Hz`, bracketed as above | the next start |
| `multisample_value` and its buttons | `graphics.multi_sampling` | the sample counts the screen supports, **nought between the greatest and the least**; the bare number; both buttons greyed where none is supported | the next start |
| `gamma_slider` | `graphics.gamma` | 0 to 100, step 1 [mod: settings.gui]; the screen scaled by **value / 50, held to 0.1 and 1.9**, every channel and cut at full - a brightness scale, not a power curve; 50 is the screen as it is | **at once, as it moves**; stored at Apply. **Back does not undo it** - the next opening puts the stored one back |
| `counter_slider` ("Counter Dist.") | `counter_distance` | 0 to 100 [mod: settings.gui]; **the distance is five times it**, 0 to 500 | at Apply |
| `counter_scale_slider` | `counter_scale` | 0 to 2, step 0.1 [mod: settings.gui] | at Apply |
| `counters_checkbox` | `alwaysCounters` | ticked is yes; set from the settings at each opening, read as it stands at Apply | at Apply |
| `hierarchy_checkbox` | `render_hierarchy` | ticked is yes | at Apply |
| `unit_name_checkbox` | `show_unit_names` | ticked is yes | at Apply |
| `trees_checkbox` | `renderTrees` | ticked is yes | at Apply |
| `water_checkbox` ("Advanced Water") | `simpleWater` | **ticked is no** | at Apply |

Looks wrong and is the original's: the slider's position comes of the distance through the slider's own report, so a
`settings.gui` without `counter_slider` makes every Apply multiply the distance by five.

### Audio

`mastervolume_slider`, `effectvolume_slider`, `musicvolume_slider`: 0 to 100, step 1 [mod: settings.gui], into
`master_volume`, `sound_fx_volume` and `music_volume` - **as they move, before Apply**; Back puts them back. All three are
greyed where `master_volume` is below nought, which only a file edited by hand can make it [exe].

### Controls

`scrollspeed_slider` and `zoomspeed_slider`: 0 to 100 [mod: settings.gui], into `scroll_speed` and `zoom_speed` **at
Apply**, held there to **at least 5 and 1** [exe]. `legacy_camera_checkbox` into `use_legacy_camera` at Apply - and
**never set from the settings**: it starts unticked as its file leaves it, so Apply without ticking it turns the legacy
camera off, and the file then says so [exe].

## 4. What looks wrong and is built so

1. Apply with nothing to restart shuts the whole menu (section 2).
2. The refresh rate is looked for among the first mode's rates, at each opening and at Apply (sections 2 and 3).
3. The autosave choice outlives Back (section 3).
4. A gamma moved and not applied stays on screen after Back (section 3).
5. The legacy camera box is never set from the settings (section 3).
6. Escape leaves without putting the volumes back (section 1, *inferred*).

## Ours

- **The screen's modes**: the original asks the graphics card for every mode of at least 1024 by 720, in the driver's
  order. Here the list is the size and rate the settings name - with the screen's own rate beside it where the screen is
  that size - and the screen's own size where it is another, and the game is taken to run at what the settings name. The
  sample counts that may be had are 2, 4 and 8, the engine's.
- **What waits on a start is not applied at one**: OpenHOI3 opens in the window it is given and does not take its size,
  rate or full screen from the settings. Multisampling it does take, at the session's start.
- **The settings nothing here drives yet** are kept, shown and written all the same: the volumes (there is no sound),
  scroll speed, zoom speed and the legacy camera (the camera is OpenHOI3's own), trees, advanced water and the unit names
  on the counters (none is drawn), and the counter distance and scale. The gamma and the hierarchy's lines on the map are
  driven. The readme's *Loose ends* lists the rest.
- The gamma is a layer over everything drawn, where the original's is the screen's own ramp - which Direct3D applies
  only in full screen, by its documentation; here it applies in a window too.

## Not established

- What the original's `simple` and `categories` settings mean; both are read and written as they are.
- Which `settings.txt` a mod reads - its user folder's has one; mods are not played here yet.
- Whether dragging a slider gives fractions.
