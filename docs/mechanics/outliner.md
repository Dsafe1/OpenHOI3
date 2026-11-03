# The outliner

The list down the right of the in-game screen: the player's battles, units, allied objectives and bombings, a section
each. Window `outliner` in `interface/outliner.gui`. Read whole on 2026-11-03 in one wave of three readers
(`reversing/findings/FINDINGS-outliner.md`), against three screenshots of the maintainer's: a land battle row and its
tooltip, the land units' tree and an army's tooltip, the naval units' counts. Every figure in them is accounted for.

## 1. Where it is, and when

- **The top bar's `topbar_outlinerbutton`** opens and closes it, showing frame 2 while it is open and frame 1 while it is
  shut. The outliner is made with the top bar.
- **While any country screen is open** - production, technology, diplomacy, politics, intelligence, theatres - or the
  ledger, the outliner is shut and the button hidden; when the screen goes, the button comes back, and the outliner
  opens again if it was shut that way.
- **Open or shut from one game to the next**: the player's `settings.txt` key `outliner = { ... }` holds 10 when it is
  open (section 3). *Ours*: the settings file is read and never written (`docs/compatibility.md`, entry 13), so it opens
  as the file says and what the player changes lasts for the session.
- The window stands at the top right where its file puts it; the list's height is at most
  `((screen height - 358) / 16) x 16 - 48` - 672 at 1080 - and the window's foot (`outliner_bottom`) is put under the list.
  *Hardcoded*: the height; *mod-able*: everything in the file.

## 2. The sections

Nine, always in this order, each with a title row, and **shown only where its switch is on and it has an entry**:

| | section | title key | switch |
| --- | --- | --- | --- |
| 1 | land battles | `OL_COMBATS` | `outliner_view_landcombat` |
| 2 | sea battles | `OL_NAVAL_COMBAT` | `outliner_view_navalcombat` |
| 3 | air battles | `OL_AIR_COMBAT` | `outliner_view_aircombat` |
| 4 | land units | `ARMIES` | `outliner_view_landunits` |
| 5 | fleets | `NAVIES` | `outliner_view_navalunits` |
| 6 | air units | `OL_AIR_UNITS` | `outliner_view_airunits` |
| 7 | allied objectives | `OL_ALLIED_OBJECTIVE` | `outliner_view_ao` |
| 8 | our bombings | `OL_WE_BOMB` | `outliner_view_bombonus` |
| 9 | hostile bombing | `OL_BOMB_ON_US` | `outliner_view_bombonthem` |

A switch's tooltip is its section's title. **The two bombing switches look swapped** - `_bombonus` is our bombings -
and are built so. A title row (`outliner_header_entry`) shows the title in `entry_text` and nothing else: no count, no
tooltip, no collapse.

**What each holds:**

- **Battles** (1-3): every battle a unit of the player's is in, once, in the order met walking the player's units: a land
  battle in 1, a sea battle in 2, an air battle in 3.
- **Units** (4-6): the player's units in the order of its list. A unit is a **root** where it has no superior, or where it
  is a fleet or an air unit whose superior is a land unit. Land units: the roots that are land units. Fleets and air
  units: their roots, but not one with an AI agent above it (none, for the player's own) and not an air unit whose base is
  a carrier. **Each root is followed by its subordinates, depth first in their order, where the unit is expanded** - a
  collapsed unit hides all below it. A fleet or air unit under a land headquarters is listed twice: in the land tree and
  as a root of its own section.
- **Allied objectives** (7): first those asked of the player, then those it asked; one row a province.
- **Bombings** (8, 9): every bombing a unit of the player's is in - in 8 where that unit is an air unit of the
  player's, else in 9. One with no unit of the player's in it is not listed.

The file's `outliner_province_entry` (with `occupied` and `siege`), `outliner_standard_entry` and `outliner_unit_entry` are
never used: no code names them.

## 3. The switches and `settings.txt`

`outliner = { 1 2 3 ... 10 }` in the player's `settings.txt`: 1 to 9 a section switched on, 10 the outliner open, in no
order. With no such key: open, with land units, fleets and air units (`{ 10 4 5 6 }`). *Hardcoded*: that default.

A switch is a checkbox at the top of the window, read each frame; a change shows within a frame. The file's
configuration window (`outliner_configuration`) is made and never shown in the original, and is not built here.

## 4. When the rows are made again

Each frame while it is open the outliner gathers what each switched-on section would hold. **The rows are made again only
where that differs** from what is shown - a section's count, or the thing behind a row, in order (allied objectives by
count alone) - **or a country's control has changed hands**; otherwise every row brings its figures up to date. A unit
expanded or collapsed changes what the land section holds, so it is made again. The list keeps its scroll.

## 5. A unit's row

Two windows: `outliner_unit_entry_bg`, whose button `outliner_standard` is the whole row, and in it
`outliner_top_unit_entry` with `expand_button`, `entry_text`, `info_text` and `movment`.

- **`entry_text`**: the unit's name cut at 21 characters to its first 18 and `...`; a land unit under an AI agent first
  `§G` (never for the player's own here); a unit in one of the player's selection groups `(§Yn§W) ` after it - the first
  group of ten holding it, no space before, one after.
- **`info_text`**: a land unit's strength as a share of the most its brigades may have, in percent to one decimal, cut:
  `100.0%`, `99.9%` - **red when the row is made** where its strength, against the most its brigades have had, is under
  its shatter threshold (`landcombat.md`), and written plainly once the strength changes; a fleet's ships and an air
  unit's wings as whole numbers.
- **`movment`** shown exactly while the unit has a way; a fleet's or air unit's only when its row is made.
- **`expand_button`**: frame 1 collapsed, 2 expanded, set when the row is made; disabled where the unit has no
  subordinates. A click turns the unit's expanded state over, and nothing else. **The state is the unit's and is never
  saved**: every unit starts collapsed. *Ours*: kept by the client, not in the game.
- **Indented**: a land row moves right by `outliner_browser_offset`'s x (10) for each unit above it; fleet and air rows
  never.
- **A left click** on the row, for a unit of the player's: the selection is that unit alone, and the map centres on it.
  No Shift, Ctrl, double click or right click.

**The tooltip**: over `movment` while moving, `OL_UNIT_MOVING` with the next province; over `info_text`,
`SHATTER_TOOLTIP` (its strength against its threshold and the threshold's three parts); anywhere else `OL_ARMY` -
`$COMMANDER$` `OL_COMMANDER` with the leader's name, or `OL_NOCOMMANDER`, and `$PROV$` where it stands - and while moving
a line more, `OL_ARMY_MOVE` with the last province of its way. A fleet's and an air unit's whole row: `OL_NAVY` /
`OL_NAVY_MOVE`, `OL_AIR` / `OL_AIR_MOVE`, the same way.

## 6. A battle's row

Window `outliner_combat_entry`: the province's name in `entry_text`, the figure in `info_text`, the whole row a button.
Which side the player is on is fixed when the row is made.

- **Land**: the attacker's share (`landcombat.md`), x 100 and cut to a whole number: `§G7%§W` where the player attacks,
  `§R-7%§W` where it defends. **The colour says the player's side, not how the battle goes.**
- **Sea and air**: the two sides' ships or planes, the attacker's first, the player's green: `§G a §W - §R d §W` or
  `§R a §W - §G d §W`. An air row starts at `0 - 0`.
- **Bombing**: the province alone; no figure, no tooltip.
- **The tooltip**, land: `OL_LAND_COMBAT_ATTACK` or `OL_LAND_COMBAT_DEFEND` - `$PROGRESS$` the attacker's share to one
  decimal, not negated; each side's highest ranked leader or `NO_COMMANDER`; `$ENEMY$` the adjective of the first of the
  other side's countries still fighting, `NONE` where there is none; each side's brigades at the front and in reserve.
  Sea and air: the battle's name, then each side's leader where it has one and its count of `OL_SHIPS` or `OL_WING`, the
  attacker's above a `-`, the player's green.
- **A left click**: the selection let go, the battle's window opened, the map centred on it. An air battle's row does
  nothing while that battle's window is already up.

## 7. An allied objective's row

Window `outliner_allied_objective_entry`: the province's name, `§R` where it was asked of the player, `§G` where the
player asked it. The tooltip: the countries that asked, each `§Y` named, joined by `, ` and ` and ` before the last,
`OL_AO_THEY_REQUEST`; or `OL_AO_WE_REQUEST` and the countries asked; then `OL_AO_DEFEND` where the province's controller
is in the player's faction, else `OL_AO_ATTACK`; then the province, `§Y` named, and a full stop. A click centres the
map on it.

## 8. Kept as the original does it

The bombing switches' names; the land row's minus where the player defends and the tooltip's figure without it; the
shatter red only until the strength changes; a fleet's arrow only when its row is made; the expand frame only when the
row is made; the doubled space in " and "; the name cut at 21 with no regard to the box's width; a fleet or air unit
listed twice when a land headquarters commands it; `NONE` for an enemy with nobody left. *Not kept*: the ten title rows
the original makes and never frees on every opening; the configuration window.

## 9. Not built

A unit's aggression star (no unit here has an aggression of its own); the press-and-drag of a unit's row onto another;
a row standing for a naval or an air base, which nothing here makes; the custom game's bar.
