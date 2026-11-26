# The game options window

`settings.gui`'s `menu_settings_ingame`, which the in-game menu's Game Options opens (`ingamemenu.md`): four tabs - Game,
Video, Audio, Controls - an Apply and a Back, over the menu's picture. It holds the player's `settings.txt` (`settings.md`)
and writes it. Read out of the original on 2026-11-04 (`reversing/findings/FINDINGS-settingswindow.md`), and set against
a reference `settings.txt`, which the original wrote; that Apply with nothing to restart shuts the whole menu was
confirmed in the original the same day. `confirmed` unless marked.

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
2. **whether a restart is asked**: where the mode or the refresh rate chosen is not the one the game runs at - and, in
   the original, where the multisampling chosen is not the settings', which no row chooses here (section 3). The running rate is looked for **among the first mode's rates**, as far as the
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
only on Apply, and **outlives Back**: shown again at the next opening and stored by any later Apply. Applied, it is
the frequency the autosave goes by from the next day on (`savegame.md`, section 11).

### Video

| control | holds | the rule [exe unless marked] | takes effect |
| --- | --- | --- | --- |
| `resolution_value` and its buttons | `graphics.size` | the screen's modes in their order, wrapping; a change sets the rate back to the mode's first. Text `1920x1080`, and while a restart is waiting the one asked for in brackets, `1920x1080 (1600x900)` | the next start |
| `refreshrate_value` and its buttons | `graphics.refreshRate` | the chosen mode's own rates, wrapping. Text `60 Hz`, bracketed as above | the next start |
| `multisample_value` and its buttons | **ours: the window's mode**, `window_mode` in `openhoi_settings.txt` | below. In the original the row is `graphics.multi_sampling`: the sample counts the screen supports, nought between the greatest and the least, the bare number, both buttons greyed where none is supported, at the next start | **at Apply, at once**, and at every start after |
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

**The window's mode stands where the original has its multisampling**, in every game, a faithful one too - decided
2026-11-26: "Window modes. Fullscreen, Windowed, Borderless Window. I think we can place the setting in the video
category by replacing the 'multisample level' setting" (`docs/compatibility.md`, entry 33). Ours throughout:

- **The row is the original's own elements**, found by their names as ever - `multisample_label`,
  `multisample_value`, `decrease_multisample_button`, `increase_multisample_button` - so a mod's `settings.gui` lays
  it out as it lays out the original's. The heading's text is set over the file's `SM_MULTISAMPLE`: the text of
  `OPENHOI_WINDOW_MODE` where a table has that key, else "Window Mode". The value is the text of
  `OPENHOI_WINDOW_MODE_FULLSCREEN`, `_WINDOWED` or `_BORDERLESS`, else "Fullscreen", "Windowed", "Borderless
  Window". The original's tables have none of the four keys; a mod may give them words.
- **The buttons go round the three**, in that order, either way, never greyed (`CGameOptions.StepWindowMode`).
- **What each is** (`CGameOptionsWindow.ApplyWindowMode`): *Fullscreen* is the screen for the game alone, the
  engine's exclusive full screen; *Borderless Window* is a window with no frame over the whole screen, the engine's
  plain full screen, with the other programs a key away; *Windowed* is a framed window, made as large as the
  screen allows where it would otherwise not fit on it. The screen's size and rate are never changed: the
  resolution and refresh rate rows are stored and not applied, as before (*Ours*).
- **At each opening** the row shows the mode stored, or - none stored - the mode the window is in.
- **Apply puts it in force at once, with no restart asked**, and stores it where it is a choice: another than the
  one stored, or, with none stored, another than the window was in. So a player who never touches the row has no
  mode stored, and the window is left as the system opens it.
- **It is kept in `openhoi_settings.txt`**, this engine's own file beside `settings.txt` (section 5), as
  `window_mode = fullscreen`, `windowed` or `borderless` - in a faithful game too, which reads nothing else of that
  file; the other keys are written back as they were. The original's `fullScreen` is left as it is, read and
  written and not applied: `settings.txt` is the original's too, and a third value there would be its to
  misread. A failure to write is told as the settings' own is.
- **At the start** the program reads the key and puts the window in that mode before the first screen
  (`Main._Ready`) - only where one is stored, and **not when run from the editor**, whose runs are a developer's and
  the scripted checks' and stay windows.
- **The samples** are what `settings.txt` says, applied as a session starts as before, and written back
  unchanged; no row chooses them. They smooth the stair-stepped edges of what is drawn, at a cost in frame rate.

Tried with `--click` on 2026-11-26: the row under its heading, the arrows stepping it, and Apply on Borderless
Window covering the whole screen and storing the key with the file's other keys kept. "Borderless Window" just
fits between the row's arrows in the original's layout. Not tried: Fullscreen, and the start reading the key,
which only an exported game does.

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

## 5. An enhanced game's rows

**Ours, built 2026-11-21** (`docs/enhanced.md`, *Interface*; `docs/blackice.md`, step 6). In an enhanced game the Game
tab holds four rows more, below the autosave frequency; in a faithful game the window is the original's and none of
this is there. They stand on the Game tab and not on a tab of their own by the user's choice of that day: the window
is 240 wide and its four tabs fill it, in the base game and in BlackICE, and a fifth declared in `settings.gui` the
original would draw, its contents never hidden.

- **The mod's interface files declare them**, as they declare every element (the user's word, 2026-11-21: "it should
  become its own elements in the .gui files", the utility's windows having to come the same way): one window,
  `openhoi_game_options`, which the engine makes as a part of `menu_settings_GAME`, so that its position is measured
  from that tab's corner. In it, for each of the rows `premonth`, `timed`, `edge` and `event`: `openhoi_<row>_label`,
  whose text is the file's own; `openhoi_<row>_value`, which the screen writes; and the arrows
  `decrease_openhoi_<row>_button` and `increase_openhoi_<row>_button` (`CEnhancedOptionRows`). The names are the
  contract, as everywhere; the layout is the mod's. With no file declaring the window there are no rows, reported as
  any missing window is. The first shape of it, the engine copying the tab's autosave row, lasted an hour.
- **A mod keeps such files in `interface/openhoi/`**, a folder of its own, read after `interface/*.gui` and only in an
  enhanced game (`CGuiTypes.EnhancedDirectory`). They are not in `settings.gui` because the original reads that and
  draws what it finds in a window it makes - every child a window declares is built, shown and drawn with it, asked
  for by name or not. **The original never opens a subfolder of `interface`, `localisation` or `script`**: its
  listing takes one folder and drops directories, and it lists `script` not at all - read on 2026-11-22
  (`reversing/findings/FINDINGS-guifolders.md`). BlackICE's is
  `interface/openhoi/gameoptions.gui`, each row its `settings.gui`'s autosave row 46 pixels further down.
- **Each is stepped by its arrows and wraps at its ends**, as the frequency above them (`CEnhancedOptions`):
  - *Save before the month ends*: Off, then 1 to 27 days before the 1st of the next month (`savegame.md`, section
    11.1);
  - *Save on a timer*: Off, then 5, 10, 15, 20, 30, 45, 60, 90, 120, 180 and 240 minutes of real time. The file may
    hold any figure from 1 to 240; one between two steps goes to the step beside it at the first press;
  - *Scroll at the screen's edge*: On, Off - Off leaves the map still with the pointer at an edge, the arrow keys and
    the drag as they were (`map.md`, section 8);
  - *Event windows*: Default, On the left, In the middle - where a country event's `EventWindow` stands across the
    screen: nought, or half of what the screen is wider than the window, at the height its file gives it. Default
    leaves it where the file puts it. A province's event window is never moved. The next window shown goes by it; no
    restart is asked.
- **Every word is the localisation's** (the user's word, 2026-11-21: "Please dont build it in. Always use
  localisation."): the engine words nothing, and a key no table has shows as itself, as every missing text does. A
  label's key is whatever its file says. A value's is the engine's (`CEnhancedOptionsText`): `OPENHOI_OPT_OFF`,
  `OPENHOI_OPT_ON`, `OPENHOI_OPT_PREMONTH_ONE`, `OPENHOI_OPT_PREMONTH_DAYS` with `$DAYS$`, `OPENHOI_OPT_TIMED_MINUTES`
  with `$MINUTES$`, `OPENHOI_OPT_EVENT_DEFAULT`, `OPENHOI_OPT_EVENT_LEFT`, `OPENHOI_OPT_EVENT_CENTRE`. **A mod keeps such
  tables in `localisation/openhoi/`**, read after `localisation/*.csv` and only in an enhanced game
  (`CLocalisation.EnhancedDirectory`); a key both have keeps the first text, as between any two tables. BlackICE's are
  in `localisation/openhoi/options.csv` (in `localisation/` itself for a day, where the original read them too). A
  value has the width between the two arrows and no more.
- **Made again from their file at every opening and stored by Apply**, after `settings.txt` is written: in force at
  once and written to **`openhoi_settings.txt`** in the player's folder, beside `settings.txt` and not in it - the
  original reads that one too and has to find it as it wrote it (`CEnhancedSettingsFile`; UTF-8, four keys:
  `premonth_autosave_days`, `timed_autosave_minutes`, `edge_scrolling`, `event_window`). A failure to write is told as
  the settings' own is. **Two more keys are in that file since 2026-11-22 and no row shows them**: `window_key` and
  `window_theme`, the key and the look of a mod's own windows, set from a page of theirs (`modwindows.md`, section
  6.4); the rows are made from the file and Apply writes those two back as they were (`CEnhancedOptions`). Back drops what was not applied. The front end's Settings window has the rows too.
- **Checked on 2026-11-21 with `--click`**, BlackICE's working copy played from a linked stand-in install: the rows
  drawn from its `gameoptions.gui` and worded from its table, stepped, applied - `openhoi_settings.txt` written and `settings.txt` byte for byte what it
  was - and in a second run an event's window at the left where the mod's file puts it in the middle, at the same
  height, and `autosave_premonth.hoi3` written on 30 January. The timer and the edge were tried by their tests alone.

BiceLib offers the same four on its overlay's Options pages (`docs/bicelib/features.md` of the mod's repository), with
a file name of the player's choosing for each save and any minute from 1 to 240; here the names are fixed and the
timer goes by steps. Its event window choice rewrites the mod's own interface file and asks a restart.

## Ours

- **The screen's modes**: the original asks the graphics card for every mode of at least 1024 by 720, in the driver's
  order. Here the list is the size and rate the settings name - with the screen's own rate beside it where the screen is
  that size - and the screen's own size where it is another, and the game is taken to run at what the settings name. The
  sample counts that may be had are 2, 4 and 8, the engine's.
- **What waits on a start is not applied at one**: OpenHOI3 does not take its size, rate or full screen from the
  settings. Multisampling it does take, at the session's start. **The window's mode is the player's own choice on
  the Video tab** (section 3), kept in a file of ours; with none chosen the game opens in the window it is given.
- **The settings nothing here drives yet** are kept, shown and written all the same: the scroll and mouse
  speeds. `settings.md`, section 1, says of each setting whether it is applied and from when; this list named the
  volumes, the zoom speed, the legacy camera, the trees, the water, the unit names and the counter distance and scale
  until each was.
- The gamma is a layer over everything drawn, where the original's is the screen's own ramp - which Direct3D applies
  only in full screen, by its documentation; here it applies in a window too.

## Not established

- What the original's `simple` and `categories` settings mean; both are read and written as they are.
- Which `settings.txt` a mod reads - its user folder's has one; mods are not played here yet.
- Whether dragging a slider gives fractions.
