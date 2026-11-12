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
| | `INIT_MAP_LOGIC` | the map | |
| | `LOADING_DATABASES2` | the install's Lua | |
| | `LOADING_HISTORY` | the world at the start date and its bookmark | 1 |
| | `LOAD_GFX` | the map cache, built where it is not the map's, and its quads | 1 |
| | `CREATING_THE_WORLD` | the front end and its map made | 2 |
| a game from Play | `ADAPTING_HISTORY` | the new game set up, or the save's handed on | |
| | `LOADING_SOUNDS` | the songs (`music.md`) | |
| | `LOAD_GFX` | the map and the interface of the game | 1 |
| | *Preparing the Map Modes...* | section 4 | |
| a game from a switch | (none), `LOADING_HISTORY`, `LOADING_GUI_DEF`, then as from Play | the localisation; the game; the interface | 2 at the second |

Where the original steps the pictures at a line that has no step here - its checksum, its province names - the next line
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

## 5. Developer switch

`--loadingshot=path.png` saves the first loading screen with a line on it, as that line is drawn, to look at; nothing else
can, the other switches coming after it.

## Not established

- The order the original lists the pictures in.
- How a bitmap font named `Loading_Font` would be laid out in the box.

## Loose ends

- **Making the front end's map holds the screen still for about three seconds** under `CREATING_THE_WORLD`: the map view
  entering the tree - its surface, borders and names made as nodes - takes 2.7 s and its first style 0.5 s, on the main
  thread, where nodes are made. The line stays drawn; the window does not answer meanwhile.
