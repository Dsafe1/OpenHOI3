# The Strategic Warfare Manager

*Spec for `swm_window`, behind the top bar's `swm_open` (F8): `reversing/findings/FINDINGS-topscreens.md`, sections C and E. Read out of the original; the rings and the save are set against the maintainer's saves; the window tried with `--click` on those saves; the live tallies tried in tests and short battles. The maintainer has not looked at it. Files: `tfh/interface/stratwarfaremanager.gui`, `.gfx`. Figures are exe-hardcoded unless a define or a unit key is named.*

`Sim/StrategicWarfare` (`CWarfareLedger`, `CWarfareRecord`, `CWarfareCategory`, `CWarfareRing`, `CWarfareCosts`, `CWarfareTally`), `Interface/Screens` (`CWarfareText`, `CWarfareSeries`), `src/Screens` (`CWarfareView`, `CWarfareEntry`), `src/Gui/CLineChart`, `Content/Saves/CSaveWarfare`.

## 1. What it is

A record for each enemy the played country has been at war with, kept when the war ends: three categories - naval, air, land - each three rings of eight columns: 30 days, 12 months and the game's years (`years`: the end date's year less the start's, and one). It is the played country's alone, and changes with it.

## 2. The rings

A ring has `offset`, `is_full` and its elements; the day ring starts one on. **A tally goes in the slot before `offset`.** Once a day, first thing in the day, for a running record:

1. `day.offset` moves on, wrapping with `is_full` set.
2. Just wrapped: the month's element at its offset is the sum of the day ring; the month ring moves on; if it wrapped, the year's element is the sum of the month ring and the year ring moves on; the new month element is emptied.
3. Otherwise the month's element is the sum of the day ring (every slot if full, otherwise up to and with its offset), and the year's of the month ring likewise.
4. The day ring's new slot is emptied.

So a month's element is a rolling thirty days and a first month has 29 days of tallies. `start_date` is the first day it was found at war. A record whose war is over is marked ended and stops; a war that comes back zeroes it and starts it again. Set against two saves: offsets by the date, every year element the sum of its months.

*Ours:* a new game's first enemies get their records at the first day here, where the original makes them as the game is entered.

## 3. What goes in each column

Fix(x) is thousandths as 15-place fixed point, cut toward nought.

- **A part's price**: rate = Fix(max strength × 1000 / 30000) × 0.5 × (1 + `unit_repair`) × (1 + `reinforcement_bonus`); IC = `repair_cost_multiplier` × max(`build_cost_ic`, 1) × amount / rate; men = amount / max strength × `build_cost_manpower`.
- **A brigade's loss**: nothing at nought; its whole price (its type's build cost for the owner, days and all) if the loss is all it has; otherwise a part. Priced for its owner.
- **A side's loss** of some types: in a sea battle (or the ships' side of a bombing of ships) whole ships at the full price and the rest as a part; in any other only the destroyed figure, as a part. Destroyed: in a sea battle 1000 a ship gone, and the damage kept apart; otherwise each hour the strength about to be lost, **a brigade's a hundred times over**.

| category | column | what | where it comes from |
| --- | --- | --- | --- |
| naval | 0 / 3 | IC of the convoy lost | a raid that sank something, on the raider's record where the player lost it (0), on the victim's where the player sank it (3). Each transport is priced at the **escort** defines: time × cost / `CONVOY_CONSTRUCTION_SIZE`; a lend-lease convoy adds its share of the receipt |
| | 1 / 4 | cargo sunk | `CARGO_TONS_SUNK_SCALE` × the seven goods of a day × the share lost |
| | 2 / 5 | the raiders' damage | that raid; and a fleet battle's end where a fleet of a side was raiding - the raiders' on the record of the other side's countries or owner |
| air | 0 / 4 | bombers' IC | a bombing's end, on the first country bombed with a record (4) or the first bomber's with one (0, **whoever was bombed**); and an air battle's end, the player's bombers lost, on every country of the other side (**0 when the player attacked**) |
| | 1 / 5 | what was bombed | each building's `cost` × levels, the goods at `RESOURCE_TO_IC_COST`, and the units hit |
| | 2 / 6 | bombers' strength lost | |
| | 3 / 7 | the raw total | levels, goods and strength dealt |
| land | 0 / 2 | ours, IC and men | each hour of a land battle, after both sides have fired and before it is settled: **what the player lost goes whole onto every enemy's record** |
| | 1 / 3 | theirs, IC and men | the same, each enemy's own |

Also on land: a brigade aboard a transport that was hit; and a unit lost whole, every brigade at the full price, on top of what its hours counted - no retreat province or no way there, or killed falling back. **Attrition (`TallyAttrition`) waits on attrition, which is not built.**

## 4. The window

`swm_open` and `close_button` toggle it; it does not pause, and is moveable. It keeps its tab (naval first) and its duration: 0 the day ring ("Last month"), 1 the months, 2 the years. Tabs' frames 2 chosen and 1 not; durations' 1 chosen and 2 not.

The list is every record, running first and then in the countries' order; a row's flag, colour, name and its two dates, `background_button_present` or `_past`. `select_all`, `select_present`, `select_none`; the first opening ever selects the present rows.

Each chart is a line per chosen row in its country's colour, ring values oldest to newest, padded with noughts to the ring's size. The two charts of a pair share a scale: the largest value, never under a hundredth. A point's height is value / scale, whole percent. With no row chosen, one flat line of three points. It is drawn again when the hour changes.

- Naval: `linechart_our_convoy_ic` (column 0) and `_our_enemy_ic` (2), `_our_convoy_cargo` (1); `_their_convoy_ic` (3) and `_their_enemy_ic` (5), `_their_convoy_cargo` (4). Comparators on the totals: equal within 8 IC (third frame), else the second where the first is less.
- Air: `linechart_our_province` (1) and `_our_bombers` (0); `_their_bombers` (4) and `_their_province` (5). The `total_dam` lines are columns 3 and 2, 7 and 6.
- Land: IC 0 and 1, men 2 and 3. Comparators within 8 IC and 2 men, the other way round: the second frame where the second is less.
- `SWM_GRAPH_MAX_*`, `SWM_GRAPH_TOTAL_VAL` and the `_DESC_<duration + 1>` sentences. `our_bombers_desc` uses `SWM_THEIR_BOMBERS_...` and the other the reverse: the original's.

Every chart and tab has a tooltip of its own, `SWM_TOOLTIP_*`; nothing else in the window does.

## 5. The chart

A `LineChartType` is drawn by the install's own `gfx/FX/linechart.fx`, translated like any effect: the effect places each vertex from a table of 40 colours and heights, so it runs in a 3D viewport of its own, twice the chart's size, whose picture stands in the interface. Each segment is the original's two triangles, six vertices of four small numbers: the point, the way to the other end, the side, whether it is the outline. The table's height is percent / 100; `bIsOutline`, the file's `outline`; `vHalfLineWidthScale` half the file's `linewidth`, and the width over the points.

## 6. The save

`strategic_warfare` at the top level, last: `player`, `count`, and for each enemy at column 0 its `end`, `start_date`, `end_date` if ended, `years` and nine `container`s of `max_elements`, `offset`, `is_full` and `data`, eight figures an element on one line, the closing brace after them on that line. Byte for byte against `Germany1936_03_31_19`.
