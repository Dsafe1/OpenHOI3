# The minimap and the map mode buttons

The minimap at the lower right of the in-game screen and the sixteen buttons above it that choose how the map is
coloured. Written as rules this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary. Where this project chooses its own behaviour the
paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-minimap.md`, read for this, whole; `reversing/findings/FINDINGS-mapmode.md` for which
button sets which mode.

Code: `CMinimapFrame` and `CMapModeButtons` in `lib/OpenHOI3.Interface/Map` work out the frame, a press and the buttons'
names and tooltips; `src/Screens/CMinimapView` builds the minimap and `CMinimapFrameLine` draws its frame;
`CMessageLogView` shows the buttons on the bar; `CSession` binds them; a `scrollingSprite` is drawn by `CGuiIcon`.

---

## 1. What is on screen

`interface/menubar.gui` declares, at the lower right: `confirmed`

| element | what it is |
| --- | --- |
| `minimap_bg` | the minimap's frame, a top-level icon |
| `minimap_pic` | **the minimap itself**, `GFX_minimap`, a fixed picture of the whole map - `gfx/interface/minimap.dds`, 320 by 128 |
| `minimap_overlay` | `day_night_scroll`, a `scrollingSprite`: the day and night over the minimap (section 5) |
| `minimap_bg_shadow` | a shadow under the frame, in the `menubar` window |
| `mapmode_1` .. `mapmode_16` | the map mode buttons, in the `menubar` window, two frames each |

The bar's window also has the zoom buttons, which are not built here. **The minimap is hidden while a country screen is
up** - production, technology, diplomacy, politics, intelligence - and shown again when it shuts. Nothing else hides it
and there is no way to shut it: the file's `minimap_closed` is commented out and nothing reads it. `confirmed`

## 2. The picture, its frame and a press

**The picture never changes**: no map mode, date or owner reaches it. `confirmed`

**The camera's view is a white frame a pixel wide**, made again whenever the camera moves or changes height:
`confirmed`

    centre   the picture's corner + (x / the map's width) of its width across,
             and (1 - y / the map's height) of its height down, 4 up     y counted north
    t        the camera's height / 960                                  960 = 1000 - 40
    half     int(1410 t + 70) x 0.1 x 0.25 across, int(820 t + 80) x 0.1 x 0.25 down
    corners  each coordinate cut to a whole pixel

**Its size is a straight line in the camera's height**, not the view's projection, and does not depend on the screen's
shape: about 28 by 18 pixels at the starting height of 330. **Nothing clips it or wraps it** round the map's seam - near
an edge it runs off the picture. `confirmed`

**A left press strictly inside the picture takes the camera there at once, its height left alone**: `confirmed`

    rx, ry   the press less the picture's corner, whole pixels; nothing where rx or ry is 0 or the size or beyond
    x = (rx - 1) x the map's width / the picture's width
    y = (the picture's height - (ry - 1)) x the map's height / the picture's height       counted north

The press goes no further: the map under the minimap never sees it and nothing is selected. **There is no drag** - a
move, a release, the right and middle buttons do nothing - **no tooltip of its own**, and no sound. `confirmed`, but the
tooltip `inferred`.

## 3. The mode a game starts in

**The terrain mode, with `mapmode_1` down**, a new game and a loaded one alike: nothing keeps a mode. `confirmed`. Since
2026-11-07 a game here starts so, where it started in the political mode until then; `--mode=` still starts in another.
**The lobby's map, where a country is chosen, is political**: the original's front end sets the political style
(`reversing/findings/FINDINGS-frontend.md`, A.2), and the maintainer's word that day: "during country selection it is in
political map mode."

## 4. The buttons

Each button sets its mode, and the original's numbers are not its modes' (`FINDINGS-mapmode.md`): `confirmed`

| button | mode | here |
| --- | --- | --- |
| `mapmode_1` | terrain | built |
| `mapmode_2` | political | built |
| `mapmode_3` | weather | built (`weather.md`, section 6) |
| `mapmode_4` | intel | not built |
| `mapmode_5` | revolt risk | built (`revolt.md`, section 7) |
| `mapmode_6` | diplomatic | not built |
| `mapmode_7` | region | not built |
| `mapmode_8` | supply | built (`supplymap.md`) |
| `mapmode_9` | infrastructure | not built |
| `mapmode_10` | victory points | not built |
| `mapmode_11` | theatre | not built |
| `mapmode_12` | strength | not built |
| `mapmode_13` | resources | not built |
| `mapmode_14` | simplified terrain | not built |
| `mapmode_15` | air | built (`airmap.md`) |
| `mapmode_16` | naval | not built |

From left to right on screen they run 1, 14, 2 to 11, 15, 16, 12, 13 - the file's places. `confirmed`

- **A press sets the mode, presses its button and lets every other go**: the pressed one shows its second frame and the
  rest their first, by the button's own number. `confirmed`
- **No button is ever hidden or greyed** - but the supply mode's, `mapmode_8`, greyed every frame in the Arcade game
  mode, which has no supply network (`reversing/findings/FINDINGS-frontend.md`, D.2); the lobby keeps the game mode at
  Normal here (`frontend.md`, section 4), so it never is - and **no key chooses a mode**. `confirmed`. **Ours:** `Tab` went round the
  built modes until 2026-11-07; it is gone, the original having no key.
- **Its tooltip is one line**, the localised `MAPMODE_` and its number - "Terrain Mapmode" - but the seventh's is
  `MAPMODE_7G`, "Region Mapmode"; no shortcut line, the buttons having none. `confirmed`
- The original plays its `click` where the mode changes. **Ours:** there is no sound.
- The original enters three modes by itself and leaves them by setting again the mode it was in: the placing of a
  deployment, the air orders window and the theatre editor; none presses a button, so the last pressed stays down.
  **Ours:** none of the three changes the mode here.

**Ours:** a button whose mode is not built does nothing when pressed, and says so once in the log; the next pieces build
those modes.

## 5. The day and night overlay

`minimap_overlay` is a `scrollingSprite` - `daynight_overlay.tga`, 512 by 128, drawn 320 by 128 over the picture, `step`
24, through `progress.fx`'s `ScrollTech` - whose frame is **the hour of the day**, set every hour. `ScrollTech` samples
its picture at u + `CurrentState`, wrapping, and `CurrentState` = (frame - 1) / `step` x the share of its texture the
picture fills: so the night slides a twenty-fourth of the texture across the minimap each hour. `confirmed` for the
rule; that the share is the texture's whole width is `inferred`. Here a texture is the picture alone, so the share is one.

## 6. The map's tooltip stands on the minimap

**A province's tooltip stands over the minimap's upper left corner, 50 left of it and 20 above it, wherever on the map
the mouse is** - the box standing on that point, pulled in only at the screen's right edge. `confirmed`, and the reason
the maintainer's screenshot of the original has it over the minimap. Since 2026-11-07 this project's does so; it stood
over the mouse until then (`tooltips.md`, section 7). With no minimap - the front end - it stands over the mouse.

## Not established

- What the original's tooltip does over the minimap itself, and which element is under the mouse there.
- What the layer mask each mode sets hides of the counters and pictures.
- The zoom buttons, read and not built: Page Down and Page Up take a fifth off the camera's height or add one, between
  40 and 1000.
