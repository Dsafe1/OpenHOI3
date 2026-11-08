# The order of battle browser

*Spec for `oobbrowser`: `reversing/findings/FINDINGS-oobbrowser.md`. Read out of the original, tried with `--click`, and the maintainer has not looked at it. File: `tfh/interface/oobbrowser.gui`. Figures are exe-hardcoded; the window's layout is the file's.*

`COobTree`, `COobRow`, `COobBrowserText` (`Interface/Screens`); `COobBrowser`, `COobEntry` (`src/Screens`).

## 1. Opening and shutting

The unit panel's `oob_button` opens it, or shuts it if up. It does not pause and is not modal. It shuts itself whenever the units selected are not exactly one, so it lives as long as one unit stays selected. A country screen opening leaves it up. Beside it, the unit panel stands at the browser's width.

## 2. The window

At (0, 109), its height the screen's less 260 and its list that less 90. `oob_label` is `OOB_LABEL`, the player's adjective written in. `split_window` is a second window at its right, hidden as the browser opens.

## 3. The buttons

| button | does |
| --- | --- |
| `collapse_button` | the list back to its roots |
| `uncollapse_button` | every row open |
| `theatre_forces_button` (main only) | the player's whole order of battle sorted into the theatres its units stand in, by `CTheatreForcesCommand` |
| `focus_button` | scrolls to the selected unit |
| `split_button` | shows or hides the second window, copying the first list into it as it opens |
| `match_views_button` (split only) | the first list copied into the second |

The two collapse buttons share a place: `uncollapse_button` where no row is indented and `collapse_button` where any is. Only `theatre_forces_button` has a tooltip. *Ours:* the original greys it in a custom game, which we do not have, and calls the sort straight from the button where we post a command.

## 4. The tree

The roots are every unit of the player with no commander, as made; a child's order is its commander's. **There is no remembered expansion: a row is open exactly where the next row is deeper.** Opening adds the direct children only; closing takes every deeper row following; `ExpandAll` opens everything.

Each frame: any row whose unit is gone puts that list back to its roots, which loses what was open. A unit with a new commander or new in the game, while the browser was up or shut, has its row and those under it removed, then goes in at the end if it has no commander, at the end of its commander's children if his row is open, or is left out. *Ours:* the change is found by comparing commanders each frame, where the original is told by each command.

A scroll to a unit opens every row above it, then brings it to the last row in view, plus one more row for each row under it up to two short of the rows in view, not below the first.

## 5. A row

A row stands in by 16 a level (a deep row runs under the scrollbar's side). `unit_name`; `unit_info` is `£type ` for each of its first five brigades and `...` after them where there are more; `leader_photo` his portrait; `counter_type` the first brigade's; `counter_size` by level - theatre 6, army group 5, army 4, corps 3, a division 1 with one brigade or 2 with more, a fleet 2, otherwise 1; `expand_button` shown where it has children, its second frame where open. `drag_button` is the row: its second frame is the selected unit's row, and any row a valid drop is under; while a drag is on, a row a drop is not allowed on is disabled.

A click selects its unit and centres the map on it, closing any country screen, and does not scroll the list. Selection made anywhere else does.

**The tooltip.** Over the portrait, the leader's own. Anywhere else: the leader's name in yellow, the unit's, its province's, then for each brigade type held `%d £type ` and a `{NN` bar of tenths for each brigade. Delayed: `OOB_UNDER_COMMAND`, a line `%d word` for each level with units under it (`NUM_GROUPS`, `NUM_ARMIES`, `NUM_CORPS`, `NUM_DIVISONS` - so spelled - `NUM_SHIPS`, `NUM_WINGS`, `NUM_UNKNOWNS`), a blank line, and every brigade under it by type, `%d £type  `.

## 6. Dragging

A press on a row's `drag_button` picks its unit up, on its portrait its leader. A press on a brigade's row of the unit panel picks the brigade up. It is a drag once the mouse has moved more than **2 pixels** on either axis. A floating row follows it, with no expand button; a leader's at 10 to its right. A closed row the mouse stays on for a second opens. Above or below a list, in its column, the list creeps by `sqrt(distance) * elapsed`, a line each time that passes 1.

**On release, on a row** (the main list first):

| dragged | test | posted |
| --- | --- | --- |
| a unit | the commander ranks above it, a land commander short of a theatre holding under five land units, a fleet or air unit only under a corps or above | `CAttachUnitCommand` |
| a brigade, ship or wing | the row's unit is not its own, and the transfer's eleven tests | `CTransferSubUnitCommand` |
| a leader | may command the row's unit; where it has one, he may command the dragged leader's | `CAssignLeaderCommand`, and a second the other way - a swap |

A drop not on a row does nothing. Nothing is undoable.

## 7. The second list

The same as the first, with its own rows, scroll and expansion.

## 8. What is missing and what is the original's

- **Unbuilt.** A unit dragged in from the unit panel's child rows or the outliner, a leader from the panel's leader button, and the deploy menu's drop into the browser: only the panel's brigades and the browser's own rows are sources. The window of the original has no leader list: `oob_leader_entry` is the floating row alone.
- *Ours:* a row with no leader keeps the file's picture where the original draws its null leader's.
- *The original's, kept:* the split button's opening copy, losing what the second list held; a row's unit gone loses the list's expansion; `theatre_forces_button` on the main window alone.
