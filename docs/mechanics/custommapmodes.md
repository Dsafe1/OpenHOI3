# Custom map modes

An enhanced game's own: a map mode's button lists other things its mode can shade the land by. Nothing here is the
original's, which has sixteen map mode buttons and no way to add to what one shows.

It stands where BlackICE's utility had its Custom Mapmode page, and its rules are that page's (the mod's
`docs/bicelib/features.md`, *Custom Mapmode page and the custom map mode*) but for where it is reached from. The
user's words, 2026-11-22: "This one should also probably be done directly in the engine, skipping the utility step.
For this I think we could just make the Map-Mode buttons right click-able to open a small list (like the unit
upgrade selection)." And on 2026-11-24, asked whether each kind should go on the button it belongs to or all on the
Victory Points button as the DLL has it: "Yes do the first."

`CCustomMapSource`, `CCustomMapSources`, `CCustomMapScale`, `CCustomMapColours`, `CCustomMapLegend` in
`lib/OpenHOI3.Interface/Map`; `CCustomMapModes` in `src/Screens`; the painting in `CMapView.RefreshColours`.

## 1. In a line

A right click on the Infrastructure, Resources or Supply map mode button opens a list; a row chosen, the mode shades
every land province by that one figure in ten shades, with a legend; a left click on any map mode button is the
game's own mode again.

## 2. The list

**In an enhanced game** a right click on one of three buttons opens `openhoi_mapmode_list`, its foot on the button
and its right edge on the button's, kept on the screen. A second right click on the same button shuts it, and so
does a left click on any map mode button. Those three buttons' tooltips gain the line `OPENHOI_MAPMODE_TIP`.

| button | kind | its rows |
| --- | --- | --- |
| Infrastructure | a building's level | every building the game defines, in `buildings.txt`'s order, by its own name |
| Resources | a resource's amount | Energy, Metal, Rare Materials, Crude Oil, Manpower, Leadership |
| Supply | a supply figure | the eleven of section 3 |

The first row is always `OPENHOI_MAPMODE_GAME_OWN`, the mode as the game has it. A row pressed shuts the list, sets
the button's mode and, but for that first row, the source.

**The source lives with its host mode alone.** Any map mode button pressed - the host's too - puts the game's own mode
back; another mode chosen by a key or by a screen leaves the source set and unshown, and nothing brings it back but
the list. The legend's close button clears it too. Nothing of it is in a save, a hash or a command: it is what one
player looks at.

*Ours, against the DLL:* there the one custom mode takes the Victory Points button over and is chosen on a page of
the utility; the Victory Points mode here is the game's own always.

## 3. The sources

**A building**: the level that works in the province - what bombing left, not what was built - cut to whole levels.
Nothing to show under one, or above a hundred. Scale: level.

**A resource**: what the province yields today of energy, metal, rare materials or crude oil, or its manpower or its
leadership. Nothing to show at nought or below, or above 1000. Scale: map.

**A supply figure**, each the day's supply pass's own or made from it (`supply.md`):

| key | what | scale | unit | more is worse |
| --- | --- | --- | --- | --- |
| `days_of_supplies` | the supplies lying there over what the units drawing on the province need a day, held at 365. Nothing where they need nothing | bands 0, 1, 2, 3, 5, 7, 10, 15, 21, 30 | days | |
| `days_of_fuel` | the same for fuel | the same | days | |
| `depot_distance` | how far the province is from its depot as the network counts it; 100000 where the depot cannot be got to. Nothing for a province that draws on no depot | bands 0, 1, 2, 4, 7, 12, 20, 35, 60, cut off | cost | yes |
| `depot_detour` | that distance less the provinces on the way, never below nought. Nothing with no depot or cut off | bands 0, 1, 2, 3, 5, 8, 12, 18, 26, 40 | cost | yes |
| `supply_load` | what was asked of the province today over what it can pass on in a day, held at ten times. Full where it can pass nothing. Nothing where nothing was asked | bands 0, 10, 20 ... 70, 85, 100 percent | percent | yes |
| `fuel_load` | the same for fuel, against the same capacity | the same | percent | yes |
| `supply_tax` | what a load loses crossing the province. Nothing with no depot | bands 0, 0.025, 0.04, 0.05, 0.06, 0.075, 0.1, 0.125, 0.15, 0.19 | amount | yes |
| `supply_traffic`, `fuel_traffic` | what was taken out of the province today for provinces further out. Nothing at nought | map | amount | |
| `supply_stock`, `fuel_stock` | what lies in the province. Nothing at nought | map | amount | |

An amount below nought or above a million counts as nought. A building's row is named by the building's own text; a
resource's and a supply figure's by `OPENHOI_MAPMODE_<KEY>`, and a supply figure has an explanation,
`OPENHOI_MAPMODE_<KEY>_DESC`.

## 4. From a value to a shade

Ten shades.

- **Level**: the shade is the level, held between 1 and 10.
- **Bands**: ten fixed starts, lowest first. The shade is one and how many starts after the first the value reaches.
- **Map**: ten starts worked out from the map and then used as bands. Every province's value above nought is
  gathered and sorted; `low` is the entry at index `floor(0.01 n)` and `high` the one at `floor(0.99 n)`. Where `high`
  is not above `low` all ten starts are `low`. Else start *i* (0 to 9) is `low x (high / low) ^ (i / 9)`, rounded up -
  a billionth taken off first - with the first set to `low` and the last to `high`: the same step up a shade, not the
  same amount, since amounts like these are lopsided. *n* is kept for the legend.

**Where more is worse the shade is turned round**, eleven less it, so the bottom shade is the one to worry about
either way.

**The scale is made again with every colouring of the map** - when the source is chosen and whenever the mode's
colours are made again (`mapmodes.md`, section 1). *Ours:* the DLL works a resource's scale out when it is picked
alone, and a supply source's daily.

## 5. A province's colour

By what the player knows of the province (`intelligence.md`); it is **seen** at 6 or more, where the province view
starts showing buildings.

1. A supply figure, not seen: dark grey 120, whatever it holds.
2. Nothing to show: light grey 200 where seen, dark grey where not.
3. Else its shade where seen, and **shade 1 where not** - that there is some, and not how much.

One flat colour, no stripes. The sea is the host mode's own.

**Two palettes**, the player's choice, kept in `openhoi_settings.txt` as `mapmode_palette = green` or `heat`:

- **green**, the default: green `90 + 165 s / 10`, red and blue `24 + 36 s / 10`, in whole numbers, *s* the shade;
- **heat**: the Infrastructure map mode's own ten colours, red through yellow to green, with its step back in
  brightness between the sixth and the seventh.

## 6. The legend

`openhoi_mapmode_legend` stands while a source is shown in its host mode, and may be dragged: the source's name in
`title`; ten swatches, `swatch_1` to `swatch_10`, white pictures given their shade's colour; under each, `under_1` to
`under_10`, for a building the shade's number and else the value the shade starts at - which where more is worse
runs the other way, the largest under the first; `palette`, a button that says the palette in use
(`OPENHOI_MAPMODE_PALETTE_GREEN`, `_HEAT`) and changes to the other; and `close`, which puts the game's own mode
back.

A start is written by its unit: an *amount* and *days* to about three figures - no decimals from a hundred, one from
ten, two from one and three below, the noughts at the end taken off; a *cost* whole, or `OPENHOI_MAPMODE_CUT_OFF`; a
*percent* whole.

**The background's tooltip** is the lines the page had under its swatches: the source's explanation; for a map
source `OPENHOI_MAPMODE_MAP_NOTE` with `$COUNT$` provinces and `$TOP$` where the top shade begins, or
`OPENHOI_MAPMODE_NONE_HAS_ANY`; for a building `OPENHOI_MAPMODE_LEVEL_NOTE`; and what the greys mean,
`OPENHOI_MAPMODE_GREY_NOTE` or for supply `OPENHOI_MAPMODE_GREY_SUPPLY_NOTE`.

Both windows and the list's row, `openhoi_mapmode_entry` with its button `pick`, are declared in the mod's
`interface/openhoi/mapmodes.gui`, every picture a built-in element (`builtin.md`); the texts are in its
`localisation/openhoi`.

## 7. The province's tooltip

The host mode's own, since the mode is the host's: a supply figure under the Supply mode's lines, a resource under
the Resources mode's, a building under the Infrastructure mode's.

**In an enhanced game the Resources mode's tooltip gains two lines**, in the game's own mode and under a custom
source alike, after the four goods and before the strategic resource: `MANPOWER_IRO` and `PV_LEADERSHIP`, each with a
colon and the province's figure to two places rounded half up, where it is above nought and the player knows the
province at 1 or more - the goods' own test. The DLL adds the same two.

## 8. Trying it

`--custommap=<kind>:<key>` sets a source as its list would: `--custommap=supply:depot_distance`,
`--custommap=buildings:industry`, `--custommap=resources:energy`.

Tried with `--click` on 2026-11-24 - the list from a right click, a row chosen, the legend - and not reviewed in
play.

**The list goes at a click elsewhere and when a main screen opens** (2026-11-25): a press of the left or right button anywhere but on the list shuts it and is left to whatever it was meant for, and so does a country screen or the Statistics coming up (`CCustomMapModes._Input`, `ScreenOpen`). A second right click on the list's own button still shuts it and does not open it again.
