# Selection: the click, the drag box, Shift and several units

What the player selects on the map and what the screen does with several units selected. Read for this project in
the fact base's selection wave of 2026-10-16 (`reversing/findings/FINDINGS-selection.md`, sections A and B), on the
maintainer's word that day: "implement the drag-box-selection; implement the shift-multiselect for units (combines
with the drag box)". The province's own click and Shift-click are `provinceview.md`, section 1; the single unit panel
is `unitpanel.md`; carrying several units at once is `transport.md`, section 12.

Every rule here is the executable's own, **hardcoded**, unless it names a file: nothing in `defines.lua` touches the
selection. What a mod may change is the interface - `multi_unitpanel` and `unitpanel` in `unitpanel.gui`,
`unitlist_start` and `unitlist_offset` there, the sprites, the localisation keys named.

## 1. What a selection is

A selection is a list, **in the order its things were selected**, of things **of one kind**: land units, fleets, air
units or provinces. A thing joins only where the list is empty or the first thing is of its kind - so provinces join
only provinces, and a fleet never joins land units. `confirmed`

**Only the player's own units may be selected.** A plain click on another country's counter empties the selection and
selects nothing; with Shift it does nothing. `confirmed`

Land units of every level are one kind: a theatre, an army group, an army, a corps and a division may be selected
together. A fleet in port and an air unit at its base are fleets and air units like any other.

## 2. The left button

**A left press stores the point of the map under the pointer; its release is what acts.** At the release the gesture is
**a drag where the pointer's point of the map has moved 2.0 map units or more** from the press - exactly 2.0 counts -
and otherwise **a click**. Map units are the map's own pixels, so the threshold is a long move close in and a tremor
from far out. `confirmed`, the figure a literal of the executable's.

### 2.1 A click

What is under the pointer is looked for in order, the first hit winning: a battle's marker, then counters and the
pictures of buildings, then the province (`mapunits.md`, section 3; `combatview.md`, section 8).

**With Shift and something under the pointer:**
- already selected - taken out;
- not selected and of the selection's kind (or the selection empty) - **added, nothing let go**;
- otherwise - nothing at all.

**Otherwise** (no Shift, or Shift with nothing under the pointer): the selection is let go - and the province view, the
orders window and the side menus with it - and what is under the pointer is selected where it may be. **With Ctrl, a
land unit brings every land unit under it**, in the order of battle's own order, each followed by its own command before the next, each where it is not
selected already: a headquarters with its whole command. `confirmed`

Where the click lands:

| on | plain click | Shift-click |
| --- | --- | --- |
| the player's unit's counter | that unit alone | added if of the kind, out if selected, else nothing |
| **the one selected unit's own counter** | **the next of the player's units in that province**, wrapping (`mapunits.md`, section 3) | **the next unit added** |
| another country's counter | nothing selected | nothing |
| a port or an airfield | the next of the player's fleets in the port, or air units based there (`fleetsandair.md`) | with units selected, nothing |
| a battle's marker | its window | its window only with nothing selected |
| a province | that province alone | `provinceview.md`, section 1 - with units selected, nothing |

There is no double click.

### 2.2 The drag box

**While the button is held and the gesture is a drag**, the box is drawn: **a white outline two pixels wide**, a
rectangle on the map between the press's point and the pointer's, square to the map. No fill. `confirmed`

**At the release**, in order:

1. **The old selection is let go, unless Shift is held and units are selected.** So Shift and a box add to selected
   units, and Shift and a box over selected provinces let them go first. The province view, the orders window and the
   side menus go with it - the deploy menu stays shut.
2. **Three walks of the player's units**, in the order the country lists them, every level of the order of battle:
   1. **land units standing in a land province**;
   2. **fleets**, at sea or in port;
   3. **air units**, in the air or at their base.

   Each is selected where it may join the selection (section 1) and **its point lies in the box grown by 2 map units on
   every side**. A unit's point is its province's unit point (`map/positions.txt`'s `unit`); **a fleet in port is at the
   province's naval base and an air unit at its base at the province's air base**, where the positions file puts them.
3. Nothing else: no province is ever taken by a box.

So **a box over land and sea takes only the land units**; fleets only if no land unit is in it; air units only if
neither. With Shift and units selected, theirs is the kind that wins. The box takes headquarters with their divisions,
units in a battle and units aboard an air unit; it leaves land units aboard a fleet, which stand in a sea province.
`confirmed`, but for the last two, *inferred* (`FINDINGS-selection.md` A.11).

## 3. The right click with several selected

**Every selected unit, in selection order, is given the right click on its own**, as if it were alone
(`movement.md`, `fleetsandair.md`): each land unit is sent there, or halted where it stands, or refused. With the
orders window up the click is the window's, as with one unit. `confirmed`

**Fleets and air units without Shift: the window comes up for the first selected unit that asks for it, and no unit
out of a battle moves** - each later one finds the window already up and does nothing, the window keeping its first unit.
**A unit in a battle is still sent**, wherever it comes in the selection: a fleet in a battle as with Shift, an air unit
in a battle straight there (`fleetsandair.md`, section 4). With Shift every fleet is sent straight there; **an air unit
is not** - its click never reads Shift, and one in no battle brings the window up all the same. `confirmed`
(`reversing/findings/FINDINGS-movement.md`, section 13.2). *Corrected 2026-11-14*: this said every air unit was moved with
Shift, after the fact base's selection write-up, which its own `FINDINGS-seaair.md` section 1 contradicted; the client
had built the window for every air unit and so never followed it. Until the same day a later unit was skipped once the
window was up, a fleet or an air unit in a battle with it.

The map's tooltip with several selected reads only the first: its warning line and its distance (`tooltips.md`,
section 7).

## 4. On the map

**Every selected unit's counter wears the white ring**; every unit under a selected one, not itself selected, the
second colour's ring (`mapunits.md`, section 2; `hierarchy.md`). **The lines of the hierarchy are drawn for the first
selected land unit only.** Each selected unit on the move shows its way.

## 5. The panels

**One unit selected: the single unit panel** (`unitpanel.md`). **Two or more: the multi-unit panel** -
`multi_unitpanel`, at its file's place - **and a row for each selected unit**, in selection order; the single panel is
never up with two selected, and with its battle rows, brigades and children absent. `confirmed`

### 5.1 The rows

A row is the single panel's header, the `unitpanel` window, for one unit (`unitpanel.md`, section 3), its name not to be
typed in. The rows stand one under the other from `unitlist_start` (0, 144) - `unitlist_offset` (16, 40) apart - as many
as fit: `(screen height - unitlist_start.y) / unitlist_offset.y - 1`, 22 at 1080 lines. **Where there are more**, the
rows move right by `unitlist_offset.x` and `multiunit_slider` stands at their left, `unitlist_offset.y` times that many
high; the first row shown is `trunc((rows - fit) x value / 100)` of the slider's value, its end buttons stepping 1 of
its 100, and **the mouse wheel over the rows moves exactly one row**. The rows are laid out and filled again every
frame. `confirmed`

A row has three buttons:

| button | does | tooltip |
| --- | --- | --- |
| `remove_unit_from_selection_button` | takes the unit out, at the end of the frame | `DESELECT_UNIT`, delayed `DESELECT_UNIT_DRO` |
| `only_unit_from_selection_button` | **this unit alone**; **with Ctrl, it and every unit under it**; **with Shift, marks the row**, its button 15 pixels right - and **when Shift is let go, the marked rows' units alone are selected**, of the first marked one's kind, in the order they were marked | `OSELECT_UNIT`, delayed `OSELECT_UNIT_DRO` |
| `leader_button`, `leader_photo` | the leader window for the row's unit (`leaders.md`, section 12) | the header's |

Nothing else on a row takes a click; a right click on its buttons does nothing.

### 5.2 The panel

`desc` is the count, a space and `ARMIES` "Land Units", `NAVIES` "Naval Units" or `AIRWINGS` "Air Units" by the
first row's unit: "3 Land Units".

| element | shown | enabled | does | tooltip |
| --- | --- | --- | --- | --- |
| `prio_checkbox`, `reinf_checkbox`, `upgrade_checkbox` | always | always | **ticked where any selected unit has the flag**; a click sets the flag of every selected unit that differs to the box's new state | `MULTI_SEL_PRIO_DESC`, `MULTI_SEL_PRIO_REINF_DESC`, `MULTI_SEL_PRIO_UPG_DESC`: NUM with the flag, MAX selected |
| `load_multiunit`, `load_on_air_multiunit`, `unmount_multiunit` | where the first row's unit is a land unit | where a fleet, an air unit, a passenger is found (`transport.md`, section 12) | a load or unload for each unit | `transport.md`, section 12 |
| `close_multiunit` | always | always | lets the selection go | none |
| `upgrade_all` | always | - | the window of what the selection may become (`upgrades.md`, section 6) | `MULTI_SEL_UPGRADE_DESC`: its count is of the brigades that may be upgraded where they stand |
| `reorganizebutton` | always | exactly two units that may be reorganised (`reorganise.md`, section 1) | the reorganise window for the first and the second | `REORGANISE_UNITS` or `NO_REORGANISE_UNITS` with `REORGANISE_UNITS_DRO` |
| `mergebutton` | always | by the merge rule (section 5.4); where it holds but an AI agent runs one of the units, left as it was | one merge of every selected unit, unless an AI agent runs one | `MERGE_UNITS` with `MERGE_UNITS_DRO` |
| `detach_multiunit`, `highercomm_multiunit`, `disband_multiunit` | always | - | **not built** | `DETACH_ALL`, `HIGHER_COMMAND_MULTI_UNITS`, `DISBAND_ALL_TITLE` |

The carrying three (`FINDINGS-transport.md` C.3): **two or more selected**; walking the selection in order, the
whole weight adds up and the search fails at the first unit that is not a land unit, stands in another province than
the one before, stands in a sea province, is in a battle, or stands where no naval base (an air base, for the planes)
is built. Then in the last unit's province the first fleet whose passengers and the whole selection fit within
`trunc(transport_capability / 1000)` and which may carry **the last selected unit** - for the planes, the first air
unit at its base there that may load it. A press posts one load for each selected unit, all naming it; unmount posts an
unload from a plane for each unit aboard one, then an unload for each aboard a fleet, with no test of where the carrier
is.

**What takes a unit out**: any deselection; a unit that ceases to be. A land unit that goes aboard **stays selected**.

### 5.3 The single panel: the ribbon and Ctrl on its rows

Read 2026-10-16 after the maintainer's screenshot of the ribbon, "1 Army group(s), 4 Corps", and his word: "In the
original clicking the ribbon will select all subordinate units" (`FINDINGS-selection.md` A.12). `confirmed`

- **The ribbon** - `select_all_childunits`, the header of the units under the unit (`unitpanel.md`, section 8),
  pressable for a land unit only - **adds every land unit under the unit to the selection, the unit staying selected**:
  nothing is let go, so the multi-unit panel comes up with the unit first and its command after it.
- **Ctrl and a click on a row** of a unit above, a unit under it or a passenger: that unit alone, then every land
  unit under it. Without Ctrl, that unit alone, as before. The map goes to it either way.

Both walk the command as section 2.1's Ctrl-click does: each unit followed by its own command before the next, in the
order of battle's order.

### 5.4 Merge

Read 2026-11-04 (`reversing/findings/FINDINGS-merge.md`, and `reversing/findings/FINDINGS-selection.md` B.4 for the
button). `confirmed`

**The rule that greys the button**, asked of the selected units in selection order with the player's country - false
at the first unit that fails, each against the **first**:

- two or more units;
- each in no battle, not falling back, going nowhere, in the first's province, not on loan (nothing is lent here), with
  no attack delay;
- **each of the first's level, and that a division, a fleet or an air unit** - no command above a division merges;
- each a group of carrier aircraft where the first is, and not where it is not; where the first is an air unit on a
  carrier, each on the same carrier;
- **where the first is a land unit, all their brigades together no more than a division may hold** - the define
  `BRIGADES_IN_DIVISION`, mod-able, 4 in the base game. A fleet or an air unit has no cap.

The owner is not asked. Where the rule holds, the button is enabled **unless an AI agent runs one of the units or a
unit above one**, and then it is left as it was. A press does nothing in that case either; otherwise it posts **one
merge of every selected unit**, in selection order.

**The merge asks nothing again** - the greying is the only gate - and passes over a unit gone since. It:

1. **chooses the unit the rest go into**: the highest of `brigades x 10 + share x 10 / 1000 + skill x 2`, where `share`
   is the unit's strength over its brigades' summed `max_strength` in thousandths (cut toward nought, so 95% gives 9;
   nought where the sum is nought) and `skill` its leader's, nought without one. **Strictly highest, so the first of
   equals**; the first unit where nobody scores above nought. The weights are exe-hardcoded;
2. merges each other unit into it and takes it out of the game;
3. gives the one chosen the best free leader where it has none and its country chooses its own leaders.

There is no message.

**One unit merged into another** - the merge's, and the AI's join-fleet and join-air orders' (`agents.md`, section
5.4):

- every brigade of the giver, in its order, to the taker;
- two fleets: the giver's passengers go aboard the taker, dug in no longer and their staging province forgotten; two
  air units: the giver's passengers to the taker the same way, their target forgotten. Nothing checks the taker's room
  until the next test of too much aboard;
- **the leaders**: where the taker has none, it takes the giver's; where both have one and **the giver's skill is
  strictly higher**, the taker's goes free and it takes the giver's; otherwise each keeps his own and the giver's goes
  free with it. The leader who moves loses the hours he banked towards a trait, and nobody loses organisation.

A carrier's air groups are aboard their ship and go where it goes.

## 6. The orders window with several

**Accept gives the order to every selected unit of the window's kind**: a naval order to every fleet, and a row is
greyed where any selected fleet may not take it; an air order to every air unit **but one changing its base**
(`fleetsandair.md`, section 4). The window is the first unit's in everything else. `confirmed` (`FINDINGS-seaair.md`
section 2, `FINDINGS-airorders.md` section 10)

## 7. Selection groups

The digits are selection groups, ten for each country, **kept in the game state** - a command sets one, so it is the
same on every machine. `confirmed`

| keys | does |
| --- | --- |
| **Ctrl and a digit** | with units selected, the group becomes the selected units, in selection order; with nothing selected, nothing |
| **a digit** | the selection let go, and the group's units selected in order - each still there, the player's and of the first one's kind; one gone is skipped |
| **the same digit again** | with units selected, the map goes to the first selected unit's province, and nothing else |

"Again" is forgotten as soon as anything else is selected.

## 8. What looks wrong and is built so

- The drag threshold is in map units, not on the screen.
- **Land beats sea beats air** in a box; with Shift the selection's kind wins.
- Shift keeps the selection under a box only where units are selected: selected provinces are let go first.
- **A plain right click with several fleets or air units brings the window up for the first and moves nobody.**
- A Shift-click on a port, an airfield, a battle, a building or a province with units selected does nothing.
- A checkbox ticked where any unit has the flag, with no mixed state: a click on it sets every unit's flag off.
- A row's "select this" with Shift does nothing to be seen but move its button, until Shift is let go.

## 9. Ours

- **The box is a rectangle on the screen**: the map here is drawn straight down, so the original's rectangle on the
  ground is one on the screen as well. Its width is two pixels of the screen at any zoom.
- **The map wraps**: a box crossing the map's east-west seam takes what lies on either copy.
- **Alt does nothing to a box.** The original cancels a box in an input handler that reads two keys its keyboard
  never sets (`FINDINGS-selection.md` A.1, *inferred*), so in play Alt changes nothing; we do the same.
- **The rings follow the order of battle as it stands**, as they already did (`mapunits.md`): a unit taken out of a
  selection that keeps its superior wears the second colour here, where the original leaves it bare until the
  superior is selected again.
- The checkboxes post the support command with the new state for each differing unit, where the original posts a
  toggle; the end is the same.

## 10. Not built

- Upgrade-all, detach, higher command and disband: shown, never enabled - none of their systems is
  built for one unit either. A disband command exists since 2026-10-17 (`CDisbandCommand`, `theatres.md`, section 9),
  posted only by the AI for a theatre-level unit no theatre owns; the buttons are not wired to it.
- **Ctrl and a click on the single panel's battle row**: the original selects the battle - which opens its window -
  and with it the panel unit's land units, but not the unit (`FINDINGS-selection.md` A.12). Here a selection holds no
  battle, so the row opens the battle's window and Ctrl does nothing more.
- The theatre map mode's box; the province right-click menu; the cursor's picture while dragging.
- The selection groups are not saved: there is no saving yet.

## 11. Not established

- Whether the original ever sees Alt (`FINDINGS-selection.md` A.11).
- Which units lie in a stack's cycle and in what order: ours is `mapunits.md`'s.
- Whether a side menu moves the rows right: the rows here stand at `unitlist_start` whatever is open.
- Whether the merge button's shortcut, `g`, works while the button is greyed: here a shortcut goes through the button,
  which a greyed one refuses.
