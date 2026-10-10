# The supply map mode

What the map shows when it is coloured by supply, and what it says of the province under it.
Written as rules this project can implement. No addresses and no offsets. Each rule says how
sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary. Where this project
chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-supplyconvoys.md`, section 9 (the colours and the tooltip,
read for this), and `reversing/findings/FINDINGS-mapmode.md` (how a map mode colours a province
at all).

Code: `CSupplyMapColours` in `lib/OpenHOI3.Interface/Map` works the two colours out and
`CSupplyMapText` in `lib/OpenHOI3.Interface/Screens` the lines; `src/Map/CMapView` paints the
first and `src/CSession` shows the second.

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

*Ours:* there is no intelligence yet. The player knows everything of ground it holds and
nothing of any other, so every other country's land is the dim grey - which is also what the
original shows of land one has no intelligence on. The sea is drawn as in every other mode.

## 3. What it says of a province

In the original this is the tooltip of the province under the mouse. `confirmed` for which lines
and which figures; the string building was not read line for line.

**On land:**

- under two levels of infrastructure: `TOOLOWINFRA` and nothing else;
- `LOGTT_1` and the name of the depot it draws on, and ` (` its distance from it ` ` `LOGTT_2` `)`;
- and, of a province the player knows fully:
  - `LOGTT_3` what lies there of supplies, `LOGTT_4` of fuel - each in red where it is less than
    its units ask for in a day;
  - `LOGTT_5` what was asked of it today of supplies, `LOGTT_6` of fuel;
  - `LOGTT_7` what passed through it today of supplies, `LOGTT_6` of fuel;
  - with a naval base, `PORT_SHIPS` with what the harbour can land in a day (`convoys.md`,
    section 2).

Every figure to two places. The original has a line on what the province can pass as well, with
its most, which was not read closely.

**On the sea:** for each convoy of the player's whose way goes through the province, `LOGTT_8`,
the name of where it loads, `LOGTT_9`, the name of where it unloads, `LOGTT_10`; and under it a
line for each good it carried.

*Ours:* there are no tooltips yet, so the lines stand in the readout of the **selected**
province while the mode is on, without their colours. The goods a convoy carried and the line
on what a province can pass are left out.

## 4. Turning it on

*Ours:* `Tab` goes from the political map to the terrain to supply and round again, and
`--mode=supply` starts in it. The original has a row of buttons by the minimap, `mapmode_8` for
this one, which waits for the minimap.

## Not built

- **Intelligence**, and so what the map shows of anybody else's land.
- **The map mode buttons**, and the tooltip itself.

## Not established

- Whether the original draws anything on the sea in this mode besides what the tooltip says of
  a convoy: the colouring leaves the sea alone, and nothing else was read.
