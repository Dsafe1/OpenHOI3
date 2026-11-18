# The reorganise window, and what may be moved between units

The window two selected units hand brigades, ships or wings between: the original's `reorg_window` in
`interface/unitpanel.gui`, brought up by the multi-unit panel's `reorganizebutton`. Read out of the original whole in one
wave (`reversing/findings/FINDINGS-reorgwindow.md`, section A the window and B what may be moved) and set against the
maintainer's two screenshots of it, two land divisions in one province and two fleets in one port, every figure on which
the reading accounts for.

What is hardcoded and what a mod may change: the window's layout is `unitpanel.gui`'s and its picture `reorg_bg.dds`, its
words localisation (`POSITIONING_PENALTY`, `UNIT_RATIO`, `REORG_WND_SHIPS_RATIO_DESC`, `TRANSFEREDSUBUNIT`); which types
are buildable, carrier groups, capitals, screens, transports and submarines are the unit files' (`is_buildable`, `is_cag`,
`capital`, `transport`, `is_sub`); the division's limit is `BRIGADES_IN_DIVISION` and the technologies' `division_size`,
and the positioning penalty the `NAVAL_STACK_POS_*` defines. Every rule is the executable's.

## 1. The button

`reorganizebutton` can be pressed with **exactly two units selected** where both may be reorganised
(`selection.md`, section 5; the rule is the original's `UnitList_MayReorganise`): each in no battle, not falling back,
with no way to go, no attack delay, standing in the first's province, a group of carrier aircraft only with another, and
two air units based on a carrier only on the same carrier; and **not where the two together have exactly two brigades**.
Pressed, it opens the window with **the first selected unit on the left and the second on the right**. The single
panel's new-unit button opens it too, with the unit and an empty one made beside it (`unitpanel.md`, section 7.1).

## 2. What the window shows

- **The title** is `reorg_label`'s own text, "Reorganise Unit"; the two headers and the arrow between them are drawn in
  `reorg_bg`.
- **`unitname_1` and `unitname_2`**: the left and the right unit's names, written again every frame.
- **`left_list`**: a `reorg_entry` row for each of the left unit's brigades, **in the order the unit holds them**, each
  with an arrow to the right; **`right_list`** the same for the right unit from `reorg_entry_right`, arrows to the left.
  A brigade sent across goes in among the other unit's by its type's `priority`, not at the end, as every brigade
  joining a unit does (`units.md`, section 3, *The order of a unit's sub units*). `confirmed`
- **A row**, made once and never brought up to date:
  - `subunit_name` the brigade's name; `subunit_type` its type's name; `subunit_level` its model as a Roman numeral, I
    for none (as the unit panel's, `unitpanel.md`);
  - `subunit_strength` a brigade's men - strength times a hundred, cut to a whole man, so 29.996 is "2,999" - and a
    ship's or a wing's strength as a whole percentage, "100%";
  - `counter_type` the type's counter; `counter_size` its first frame; `band_reorg_regiment` the file's;
  - `org_bar` the organisation over **the type's own `default_organisation`** - not the ceiling the unit panel's bar
    uses, so no officers, commander or reserve in it - and `str_bar` the strength over its most, each held to a whole;
  - `transferbutton`, **never greyed**: pressed, it sends the brigade to the other unit (section 4), with no test of its
    own.
- **Where either unit is a fleet**, four more lines, the left ones for the left unit and the right for the right:
  - `positioning_left` / `_right`: "Positioning Penalty:" and the unit's own crowding penalty as a battle at sea reckons
    it (`navalcombat.md`) - green at nought, red otherwise - **printed in thousandths with three decimals and no
    hundredfold**, so a penalty of 5% reads "0.050%";
  - `ships_ratio_left` / `_right`: "Units Ratio:" and capitals / screens / transports, all three green - a ship that is a
    transport counts as one, else a capital, else **anything but a submarine** as a screen; a submarine counts nowhere.
  Where neither is a fleet the four are hidden - why the land screenshot has none.
- **`closebutton`** shuts it.

## 3. Each frame

- **The two units standing in different provinces shut the window.** Nothing else does but the close button and a change
  of the selection.
- The names are written again.
- **Where a list's rows no longer match its unit's brigades in number**, both lists are made again: a row whose brigade
  is still there, in its place, is kept as it is - with the figures it was made with - and the rest made or taken away.
  **A fleet's four lines are written again with them**, so the penalty and the ratio follow a ship moved across.

## 4. What may be moved

A press posts the move of the brigade to the other unit, and **the move decides**: it is refused, silently, at the first
of these, in this order (the original's `CUnit_CanTakeSubUnit`):

1. the receiving unit is in a battle;
2. the brigade's type is not buildable (`is_buildable = no`: in the base game the headquarters brigade and the partisans);
3. the kinds differ - a brigade into a land unit, a ship into a fleet, a wing into an air unit;
4. the receiving unit is all carrier aircraft and the wing is not;
5. a carrier group's wing into a unit based on a carrier: **refused unless the ship's `carrier_size` is above the wings
   of every air unit aboard it and the receiving unit's own** - and the receiving unit, being aboard, counts twice;

and then, the brigade being in a unit:

6. it is the **first brigade of a theatre, an army group, an army or a corps** - its headquarters;
7. the receiving unit has any brigades and one unit is all carrier aircraft and the other not;
8. either unit is on loan (nothing is lent here, so never);
9. the unit it is in is in a battle;
10. the two units stand in different provinces;

and last:

11. the receiving unit is a land unit and already has as many brigades as a division may - **at every level of command**.

**Nothing else is asked**: not the owner, not what the giving unit keeps - **a unit's last brigade, ship or wing may go**
- not the sea, a port or a transport.

The move itself is as it was (`agents.md`, section 5.4): the brigade leaves one unit and joins the other, a receiving unit
with no name is named, given a leader where its country chooses its own and it is not a lone land brigade, the giving unit
left empty - or a land unit left with one brigade - loses its leader, a fleet left lighter is checked for what it carries,
a carrier brings its aircraft. **The player is told** where the receiving unit is his: `TRANSFEREDSUBUNIT`, with the
brigade, the unit it left and the unit it joined. **A receiving unit of one brigade looks for a battle** where it stands.

## 5. Closing

The window is hidden, and **each unit left with no brigades is disbanded** (`CDisbandCommand`).

**Each hour a unit with no brigades is disbanded** if it was not made to stand empty and no production line is upgrading a brigade of it - where its country is nobody's (`CEmptyUnits`, in the tick), or the player's and it is not selected (the client, which alone knows what is selected and posts the command). Only the new-unit button's unit may stand empty: the flag comes with the command that makes it, and the window's closing clears it on one that has brigades (`CClearAllowedEmptyCommand`). So a unit emptied in the window stands while both are selected, and one emptied by a drag in the order of battle browser goes as soon as the clock runs and it is let go. An AI's new unit may not stand empty, and a save does not keep the flag.

## 6. Tooltips

The window's answer is final, even an empty one:

- `ships_ratio_left` / `_right`, where either unit is a fleet: `REORG_WND_SHIPS_RATIO_DESC`;
- any element of a row - its arrow, bars and counter too - the brigade's summary, as the unit panel's row gives it
  (`unitpanel.md`, section 10);
- everything else, `closebutton` included, nothing.

## 7. Kept as the original does them

Each looks wrong and is built so - do not tidy them:

- **The arrows are never greyed**: a press the move refuses does nothing, and says nothing.
- **A row is never brought up to date**; a kept row keeps the figures it was made with.
- **The positioning penalty is printed without its hundredfold.**
- **A submarine counts in no part of the ratio.**
- **A carrier's room counts the receiving unit's wings twice.**
- **A unit's last brigade may go**, and the empty unit stands until the window shuts or, if it is not selected, until the hour.
- **The division's limit holds a corps' or a theatre's own brigades too.**
- **The organisation bar is over the type's figure**, not the brigade's ceiling.

## 8. Ours

- **What shuts the window when the selection changes**: the original closes it as a row of the multi-unit panel is added
  or taken away; here, when the selection is no longer exactly the two units.
- **The AI-agent test of the button** (no agent on either unit or above it) asks nothing: the player's units have none.
- **A brigade's model** is the one it holds (`unitpanel.md`); the original picks the best of its country's models of the
  type for it each time.

## 9. Not established

- The greyed "Select" button at the top left of the maintainer's screenshots, which is not the window's - taken to be a
  selected unit's row beside it.
