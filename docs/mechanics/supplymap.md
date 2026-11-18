# The supply map mode

What the map shows when it is coloured by supply, and what it says of the province under it.
Written as rules this project can implement. No addresses and no offsets. Each rule says how
sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary. Where this project
chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-supplyconvoys.md`, section 9 (the colours and the tooltip,
read for this), and `reversing/findings/FINDINGS-mapmode.md` (how a map mode colours a province
at all).

Code: `CSupplyMapColours` in `lib/OpenHOI3.Interface/Map` works the two colours out and
`CSupplyMapText` in `lib/OpenHOI3.Interface/Screens` the lines, both asking
`CSupplyMapColours.Intelligence` - the player's level of the province, `CProvinceIntel`
(`intelligence.md`); `src/Map/CMapView` paints the first and `src/CSession` shows the second.

---

## 1. In short

- Every province has **two colours**: its ground, and its stripes. Where they are the same the
  province is plain. `confirmed`
- **Only supplies are looked at, never fuel.** `confirmed`
- The figures are the day's: what lies in the province, what lay there when the day's pass last
  came to it, what was asked of it today, what passed through it today, and what its units ask
  for in a day (`supply.md`, section 12). So the map changes once a day.
- **How bright** a province is drawn says how much the player knows of it, and under a certain
  knowledge nothing but that is shown.

## 2. The colours

With `b` = 1 where the player's intelligence on the province is 2 or more and 0.5 below, and
`grey = 0.4 x b`. `confirmed`, branch by branch:

| | ground (red, green, blue) | when |
| --- | --- | --- |
| black | 0, 0, 0 | under two levels of infrastructure: it carries nothing |
| grey | grey, grey, grey | intelligence under 6; or nothing below holds |
| flat green | grey, 0.8b, grey | intelligence 6 or 7, whatever the supplies |
| **green** | grey, 0.8b, grey | it holds at least what was asked of it today, and something was |
| **red** | 0.6b, grey, grey | asked for more than it holds; nothing lay there and nothing passed; and its units ask for more than lies there |
| **blue-green** | grey, 0.6b, 0.6b | it holds supplies and nothing was asked of it |
| **yellow** | y, y, grey | asked for more than it holds, with something there, or lying there before, or passing |

taken in that order, the first that holds. Yellow's `y` is `0.8b` where the province's
infrastructure is below what was built - by damage or by what bears on it - and `0.5b` where it
is whole.

**The stripes** are the ground's colour, except that **a province asked for more than it can
pass in a day** (`supply.md`, section 7) is striped `0.7b, grey, grey`: red.

So green is a province that is keeping up, yellow one that is being supplied but is behind, red
one that is cut off, and red stripes a road that is full.

**The intelligence is the player's, whoever holds the province** - the level of it the player's
country has (`intelligence.md`, section 2); nothing asks the controller. `confirmed`: the
original reads the byte of the province's intelligence for the map's player, once for `b` and
once for the thresholds, and an id past the end of the province's list reads as nothing
(`reversing/findings/FINDINGS-supplyconvoys.md`, section 9). So of the player's own land and its
friends' it shows the figures (9); of the land beside them, 3, only the plain grey; of a
foreign province with a unit of the player's on it, its figures; of land it knows nothing of,
the dim grey. **With the fog of war off** (`intelligence.md`, section 3; the debug menu on
Shift and 4) the player knows every province at 9, and every country's land is coloured by its
supplies. Black is asked before anything the player knows. The original's `b` is nought for a
province the session does not simulate - none, in a campaign. The sea is drawn as in every other
mode.

**When it is coloured again**: in the original, when the day turns, by the in-game screen's own
daily update, which the tick calls after the day's pass (`reversing/findings/FINDINGS-tick.md`;
`reversing/findings/FINDINGS-autosave.md`) - and the day's pass is where what each country knows is
made again (`reversing/findings/FINDINGS-schedule.md`). So what an hour's raise of intelligence
shows waits for the next day. `confirmed` as to the order, read for those findings and not for
this. And on a change of hands or of the diplomatic statuses (`mapmodes.md`, section 1;
`reversing/findings/FINDINGS-mapmodescountries.md`, section 5: the refresh the diplomatic routines
ask for runs the supply mode's setter among others). Here at each of those - the diplomatic statuses
only since 2026-11-18, when the session's list of modes that follow them was found to leave the
supply mode out - and, *ours*, at once when the fog of war's switch is turned.

## 3. What it says of a province

The supply mode's part of the tooltip of the province under the mouse (`tooltips.md`, section 7),
read line for line on 2026-10-11 (`reversing/findings/FINDINGS-provincetooltip.md`, section 6) and
built so, `CSupplyMapText`. Every figure to two places; each line starts with a newline.

**On land under two levels of infrastructure:** `TOOLOWINFRA`, and nothing else - whatever the
player knows, the test coming before the intelligence is asked.

What the player knows is its level of the province, as for the colours: the original reads the
played country's byte of it (`reversing/findings/FINDINGS-provincetooltip.md`, section 6).
`confirmed`.

**Otherwise, of a province the player knows more than a little of** (more than 5):

- **what its roads pass**: `SUPPLY_INFRA_EFFECT_INFO` with `INFRA` its level of infrastructure,
  0.10 to 10, in red where it is below the most it can have and green where it is not, `MAX` that
  most and `THRU` what it can pass in a day; or, beside a capital, `SUPPLY_INFRA_CAPITAL_EFFECT_INFO`
  with `VALUE_MAX`. A blank line follows it;
- `LOGTT_1`, the name of the depot it draws on in yellow, and ` (` its distance from it ` `
  `LOGTT_2` `)`. Nothing asks whether there is a depot.

**And of one it knows well** (more than 8):

- `LOGTT_3` what lies there of supplies, `LOGTT_4` of fuel - each red where it is less than its
  units ask for in a day, green where not;
- `LOGTT_5` what was asked of it today of supplies, `LOGTT_6` of fuel - each red where it is less
  than what passed through, yellow where not;
- `LOGTT_7` what passed through it today, `LOGTT_6` of fuel - each yellow below what the province can
  pass and red at it;
- with a naval base, `PORT_SHIPS` with what the harbour can land in a day (`convoys.md`, section 2).

**On the sea:** for each convoy of the player's whose way goes through the province, `LOGTT_8`, the
name of where it loads, `LOGTT_9`, the name of where it unloads, `LOGTT_10`; and under it, indented
three spaces, `CONV_SHIP_IRO` for each good it shipped when it last sailed.

So of a province the player knows by 5 or less only the name and the owner are told (the
tooltip's frame, `tooltips.md`, section 7); by 6 to 8 its roads and its depot - and the depot is
the one the province itself draws on, whoever's it is; by 9 everything. The readout of the
selected province no longer repeats any of it.

## 4. Turning it on

The button `mapmode_8` by the minimap turns it on (`minimap.md`, section 4), and `--mode=supply` starts in it.

## Not built

- ~~**Intelligence**, and so what the map shows of anybody else's land.~~ Built 2026-11-18: the
  colours and the lines ask the player's level of each province (section 2), where until then the
  player knew 9 of the land it held and nothing of any other - so with the fog of war off every
  other country's land stayed grey, which is how it was found.
- ~~**The map mode buttons.**~~ Built 2026-11-07 (`minimap.md`).

## Not established

- Whether the original draws anything on the sea in this mode besides what the tooltip says of
  a convoy: the colouring leaves the sea alone, and nothing else was read.
