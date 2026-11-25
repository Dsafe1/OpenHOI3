# The loading screen

Read from `reversing/findings/FINDINGS-loadingscreen.md`. Figures marked [exe] are hard-coded in the original; [gfx] come from
the install's files. Built in `src/Loading` (`CLoadingScreen`, `CLoadingPicture`, `CLoadingSteps`) and `Interface/Loading`
(`CLoadingPictures`, `CLoadingLayout`), with `Content/Gui/CTrueTypeFontReader` for its font.

## 1. The picture

The pictures are every file whose name ends in `dds` in `gfx/loadingscreens`, over the install's layers, one a name, in
name order [gfx; the order is inferred] - six in Their Finest Hour, `tfh/gfx` adding a sixth to the base game's five.
**There is no draw of chance** [exe]: the first picture is shown, and the next each time a line that steps them is shown,
held at the last (section 3). **Once the first main menu has come up, only the first picture is shown** - the original lets
the others go then and never loads them again - so entering a game and every return to the menu show the first [exe].

The picture is drawn on **a square whose side is the screen's width**, centred, behind it black [exe]: a wide screen cuts it
equally at the top and the bottom - at 1920 by 1080, 420 pixels off each. The install's pictures are drawn for it, their
bands of black falling outside the screen.

## 2. The line

`gfx/load_textbg.dds` is drawn at **600 by 104, centred across, its bottom 4 pixels above the screen's bottom** [exe; the
picture is gfx]; without that file the original draws no loading screen at all, and here only black. Over it the line, in
**`Loading_Font`** [gfx], centred across the whole screen and in the 100 pixels below the strip's top (from 108 above the
bottom to 8 above it) [exe].

`Loading_Font` is looked for among the `font = { }` entries of `interface/*.gfx`'s `fonts` blocks first, then among the
bitmap fonts [exe]. The install's is a `font` entry: Arial, `height = 32`, white. *Ours*: it is drawn in the shipped
typeface that stands for its `fontname` (Liberation Sans for Arial, `interface.md`, section 6), at the size whose cell is
`height` pixels high, as Windows takes a positive height; the original's weight of 500 and antialiasing are its own.

**One line at a time** [exe]: each replaces the last, and nothing moves between them - no bar, no percentage. *Ours*: the
work runs on a thread of its own where it touches no engine object, so the window keeps answering; the original's stands
still between lines.

## 3. The lines and when they show

The original's startup shows, in this order [exe]: `LOADING_MAP_SPRITES`, `LOADING_GUI_DEF`, `LOADING_DATABASES`,
`INIT_MAP_LOGIC`, `CALC_PATHS` (only with the map cache stale), `LOADING_DATABASES2`, `LOADING_HISTORY`, `LOAD_EVENTS`,
`LOADING_SOUNDS`, `PROC_FLAGS` or `LOAD_FLAGS`, `CREATING_CHKSUM`, `LOAD_GFX`, `CREATING_PROVINCE`, `CREATING_BORDERS`,
`CREATING_TEXTS`, `CREATING_TREES`, `CREATING_RIVERS`, `CREATING_THE_WORLD`, the raw `CREATE_WAVES` with waves on, and
`MAP_PROPERLY_INIT`. The pictures step at `LOADING_MAP_SPRITES`, `LOADING_HISTORY`, `CREATING_CHKSUM`, `CREATING_TEXTS` and
`CREATING_THE_WORLD`, so the first line already shows the second picture. Entering a game shows `LOAD_GFX`, stepping,
`LOAD_EVENTS` and `ADAPTING_HISTORY`, the last until the game's first frame; a return to the menu, `LOAD_GFX` alone; and
**loading a save in the lobby shows no loading screen** [exe].

*Ours*: the work here is cut otherwise, so a line is shown for each step of ours that does the work the original's line
names, in the original's order where the work allows:

| | line | the work here | steps |
| --- | --- | --- | --- |
| the front end | (none) | the localisation, which the lines are written from | |
| | `LOADING_MAP_SPRITES` | the interface: `.gfx` and `.gui` | 1 |
| | `LOADING_DATABASES` | the countries | |
| | `INIT_MAP_LOGIC` | the map, and its cache, built where it is not the map's | |
| | `LOADING_DATABASES2` | the install's Lua | |
| | `LOADING_HISTORY` | the world at the start date and its bookmark | 1 |
| | `CREATING_CHKSUM` | the file checksum (`checksum.md`), worked out at the first front end of a run and kept for the rest | 1 |
| | `LOAD_GFX` | the map cache's quads | |
| | `CREATING_THE_WORLD` | the front end's map and its surface made; then, a frame later, the rest of the front end | 2 |
| a game from Play | `ADAPTING_HISTORY` | the new game set up, or the save's handed on | |
| | `LOADING_SOUNDS` | the songs (`music.md`) | |
| | `LOAD_GFX` | the map and the interface of the game | 1 |
| | *Preparing the Map Modes...* | section 4 | |
| a game from a switch | (none), `LOADING_HISTORY`, `LOADING_GUI_DEF`, then as from Play | the localisation; the game; the interface | 2 at the second |

Where the original steps the pictures at a line that has no step here - its province names - the next line
here steps once more, so that **each line shows the picture the original shows with it**. A return to the menu reads the
front end again, with its lines, where the original shows `LOAD_GFX` alone.

## 4. Preparing the map modes

*Ours, and so is its line*, written in English where no table has `OPENHOI3_PREPARING_MAP_MODES`: the map is put under the
screen, and every way of drawing its surface the map modes use - the terrain style and the others, each at the near and the
far height, and every technique of the sea - is made before the screen goes, a frame apart, where they were made in play
one every fifteen frames, each stopping the map. Then the screen waits for the frames under it to come at their ordinary
pace - five in a row under 40 ms, or 240 frames whatever they take - while the first frames compile the surface's shaders.
On 2026-11-11 it took 1.1 to 1.2 s, and a game's first frames ran at their ordinary pace from the first second, the longest
16 ms, where they had run 65 and 26 ms of drawing a frame and up to 144 ms for two seconds.

**The surface is kept from one map to the next** (`CMapSurfaceKeeper`, since 2026-11-12, *ours*): the main menu's map, each
world the lobby shows and the game Play makes draw the same map through the same interface files, so the game takes the
surface the lobby drew - its meshes, its materials and every way and sea technique already made - and only what the last
view drew on it is wiped: the selection, the hierarchy's lines, the fronts, the air bases' rings, the battle plans and the
trees' snow, the borders' corners being the new view's own. A surface made from another map, other interface files or
other `waves` and `renderTrees` settings is made anew and the old one freed. A return to the menu keeps it too, since
2026-11-14: **what no game changes is read by the first front end of a run and kept** (`CFrontEndContent`, *ours*) - the
localisation, the interface's files, the map with its cache and the cache's quads - and every loading line is still shown,
as the steps the pictures are timed by. What a game takes as its own is read anew each time: the countries' database, whose
countries the world is made of, the AI's Lua, whose state and generator a game carries on, and the world itself. Measured
in the Debug build on 2026-11-14: the front end read in 4.2 to 4.4 s the first time, 1.6 s of it the checksum kept before,
and **0.9 to 1.0 s on a return**; its map made in 1.1 s and then **17 to 21 ms**; and a game played from the second front
end the same on screen. Measured on 2026-11-12 with the optimised build, from Play on a new game as Germany:
the main thread held under `LOAD_GFX` **2.34 s before, 1.06 s after**, the map modes **1.14 s before, 58 ms after**, and
from `LOAD_GFX` to the screen going **4.34 s before, 1.27 s after**; the console's `tag`, making the session again around
the same game, 0.70 s. The game's first picture was the same as before but for the rivers' and the surf's flow, whose clock
runs on from the menu.

**Two more things are not made as the game is entered** (2026-11-12, *ours*). The borders' corners are read from the
cache's three files once and kept as read; each view takes a copy, made ahead on a thread of its own as the last is handed
out (`CMapSurfaceKeeper.Corners`), where reading them again took 0.2 to 0.3 s and a copy 0.15 s. And the eight screens the top
bar opens - production, technology, diplomacy, politics, intelligence, theatres, statistics and strategic warfare - make
their windows as each is first opened (`countryviews.md`, section 1), where making them took 0.26 s; production's, the
largest, costs some 80 ms at its first opening. With both, from Play on a new game as Germany: the main thread held under
`LOAD_GFX` **0.42 s**, and `LOAD_GFX` to the screen going **0.60 s** - from 2.34 and 4.34 s before the surface was kept.

## 5. Developer switch

`--loadingshot=path.png` saves the first loading screen with a line on it, as that line is drawn, to look at; nothing else
can, the other switches coming after it.

## 6. The notices over a game

Read in `reversing/findings/FINDINGS-loadingscreen.md`, section 3.3. The in-game screen draws three notices as the
last thing of a frame, over the interface: **one line in `Loading_Font`, centred on the whole screen, with no picture
and no strip** [exe: the placement; mod: the font, and the text by its key]. Each is the localisation of its key, and
no table of the original's has any of the three, so each shows as its key.

- **`AUTOSAVING`** [exe] on the one frame between an autosave being found wanted and its being written: the original
  marks that frame, draws the notice into it, and writes on the next - the write holding the game, the notice stands
  for as long as the write takes, and is gone with the first frame after. **Built 2026-11-25**, asked for as "the
  'AUTOSAVING' text popup is missing": an autosave decided on as a day begins puts the notice up and is held
  (`CHeldAutosaves`); the frame is drawn; the next frame writes before anything else and takes the notice down
  (`CSession.HoldAutosave`, `WriteHeldAutosaves`; the notice is `CScreenNotice`, the font `CLoadingFont`, which the
  loading screen's line is drawn through too). **No hour of the game starts while one is held** - *ours*: the
  original's hours run on in that frame, which is why its autosaves made at speed hold an hour later; here the file
  holds the turn of the day as before (`docs/compatibility.md`, entry 16). An enhanced game's two extra autosaves
  (`savegame.md`, section 11.1) show the same notice. Hours a script or `--days` runs with no frame between them
  write a held autosave at once, unshown.
- **`SCREENSHOT_TAKEN`** [exe] for 15 frames from the frame a screenshot is taken, and **`MAP_SAVED`** [exe] for 15
  from the frame the map is saved - by frames, so how long hangs on the frame rate. **Built 2026-11-25** with the
  two keys that raise them, F11 and F12 (`screenshots.md`; `CPictureKeys`, `CNoticeFrames`), each a notice of its
  own, so two at once lie over each other as the original's do.

**Checked** by `LoadingScreenTests` - the holding - and looked at once on 2026-11-25, BlackICE run by `--click` to 30
January 1936, the day of its pre-month autosave: the file written, and `AUTOSAVING` white in the middle of the
screen over the event windows - in a build that left the notice up after the write, the one frame not being caught
by a screenshot. **Not reviewed in play**, and not set beside the original for the font's size.

## Not established

- The order the original lists the pictures in.
- How a bitmap font named `Loading_Font` would be laid out in the box.

## Loose ends

- **Making the front end's map holds the screen still in two pieces** under `CREATING_THE_WORLD`, about 1.1 s and 0.8 s with
  a frame drawn between, on the main thread, where nodes are made; the window does not answer within each. It was one piece of
  3.2 s until 2026-11-14, measured part by part that day and cut so (*ours*, all of it):
  - the borders' corners are read and the first copy made on the loading screen's thread, under `LOAD_GFX`
    (`CMapSurfaceKeeper.PrepareCorners`), where the main thread read and copied them: 0.4 s;
  - the lobby's map enters the tree in the political style, which it was given only after, so its surface was made in the
    terrain style first - every land material of both heights, some 0.6 s - for nothing the lobby draws. The game Play
    makes, which opens in the terrain style, now makes those under *Preparing the Map Modes...*, a frame apart (section 4);
  - each tile's land, beach and border arrays are filled side by side, the engine's part of each mesh left on the main
    thread: 0.1 s of the 0.6 s the land took;
  - the front end's map and surface are made a step before the front end enters the tree (`CFrontEnd.PrepareMap`), so the
    two pieces are apart.

  What is left: the meshes handed to the engine and the water borders, rivers and surf, 1.1 s; the political style's land
  materials of both heights as the view takes its first height, 0.65 s; the front end's windows, 0.16 s. A return to the
  menu no longer pays them, its unchanging files and so its surface being kept (section 3).
