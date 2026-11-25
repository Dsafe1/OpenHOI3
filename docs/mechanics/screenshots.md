# The two pictures a game saves: F11 and F12

Read in `reversing/findings/FINDINGS-screenshots.md` on 2026-11-25 and built the same day (`CPictureKeys` in
`src/Screenshots`; `CMapPicture`, `CScreenshotNames`, `CPictureGate`, `CNoticeFrames` and `CBitmapWriter` in
`Interface/Screenshots`). Asked for that day: "With F12 i think you could save a bmp of the whole world in the
political map mode (controlled provinces)" - it is F12, and the colours are the controllers', whatever map mode is on.

Each rule is marked **[exe]** where the original's executable fixes it and **[mod]** where a file does.

## 1. The keys

- **F11 saves the screen and F12 the whole map** [exe]. Both are taken before the interface and before anything else
  that reads keys, so they work with any window open, and nothing else sees the key.
- The front end has F11 alone [exe].
- **Ten frames lie between two pictures of one kind** [exe]: a press inside them is dropped, not kept, which is what
  thins out a key held down (`CPictureGate`).
- Each picture raises a notice for fifteen frames, `SCREENSHOT_TAKEN` and `MAP_SAVED` (`loadingscreen.md`, section 6;
  `CNoticeFrames`). The screenshot's follows the attempt, saved or failed [exe]; the map's only a picture saved.

## 2. The screen

`Screenshots/HoI3_<n>.bmp` under the player's folder - the mod's own where it has one [mod: `user_dir`] - **n the first
from 1 whose file is not there**, and nothing saved once 9999 are [exe]. The picture is the screen as it was last
drawn, the interface and a tooltip with it. The log says `Screenshot taken: <path>`.

## 3. The map

    Screenshots/HoI3_MAP_<tag>[mp]_<year>.<month>.<day>.<hour>_<n>.bmp

[exe] The played country's tag, `mp` after it in a game between machines, the game's date and hour with no leading
noughts, and n as the screenshot's: `HoI3_MAP_GER_1939.9.1.0_1.bmp`. The log says `Map look saved: <path>`.

**The picture is the province bitmap's size, a pixel for a pixel, north up** [mod: the map] - 5616 by 2160 for the
game's map, 36 MB a file. A pixel's colour [exe], in this order:

1. **white** where it is no province or the province is water;
2. without Shift, **black** where nobody controls the province, else **its controller's colour** [mod: the country
   file's `color`];
3. **with Shift held, the players' land alone**: black where nobody owns the province; the controller's colour where a
   person plays it; where the controller is somebody's subject and a person plays its overlord, **the overlord's
   colour at 0.75** in each part; black for everything else.

A part of a colour becomes its byte times 255 and cut towards nought [exe].

**It asks nothing of the map mode, of what the player knows or of the fog of war** [exe]: every province's true
controller, whoever looks, in a game between machines too. Kept, and listed in `docs/enhanced.md`.

## 4. The file

A Windows bitmap of 24 bits, rows from the bottom up, both resolutions 2834 a metre as the original's.

## Ours

- **The size in the file's head is the file's size.** The original's writer puts width + 3 x height + 110 there, which
  is no size; the usual readers do not ask it. Nothing reads these files back, so this is not a departure a mod meets.
- **The rows are padded to four bytes**, as the format wants. The original's padding was not read; its own map's rows
  need none.
- **The map's picture waits while hours run beside the frame** (`tick.md`, section 2): it reads the game. The press is
  kept until the frame the hours are collected, where the original makes it on the next frame.
- **Shift is asked as the key is pressed and again as the picture is made**; the original asks only as it is made,
  a frame later.
- A key held gives a press for each repeat the system sends, as the original's does.

## Checked

`ScreenshotTests`: the colour rule both ways, a part's cut, the picture from the north, the names and the 9999, the
ten frames, the fifteen, and the file. **Tried on 2026-11-25** with `--click` (`f11`, `f12`, and `F12` for Shift and
F12) on BlackICE as Germany on 1 January 1936: the three files written under the mod's player folder; the map a world
of the countries' colours with white seas; with Shift, Germany in its grey and all other land black; and the notice
caught in a screenshot. **Not reviewed in play, and not set beside a picture the original saved** - above all which
way a colour's parts are cut, where a part may come out one off.
