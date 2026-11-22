# A brigade's card, the sunk ships and the pride of the fleet

*Spec for `brigade_view` and `sunk_ships_view`: `reversing/findings/FINDINGS-sunkships.md`. Read out of the original, tried with `--click`, and they have not been reviewed in play. File: `tfh/interface/brigadeview.gui`. Figures are exe-hardcoded unless a define is named.*

`CBrigadeCardText` (`Interface/Screens`), `CPride` (`Sim/Units`), `CMakePrideCommand`, `CRenameSubUnitCommand` (`Sim/Commands`), `CBrigadeCards`, `CBrigadeCard`, `CSunkShipsWindow` (`src/Screens`).

## 1. In a line

A click on a brigade's row in the unit panel opens a card of everything it is; a ship's card can open a list of the ships it sank and make it the pride of the fleet.

## 2. Opening

**A plain click on a row's `select`** opens a card for any brigade, ship or wing of the selected unit. There is one card to a brigade, a second click doing nothing, but as many of different brigades as are asked for. **Nothing pauses and a change of selection does not reach them.** The windows are moveable. A card goes on its own when its brigade can no longer be found.

## 3. The card

Filled once as it opens, so it is a picture of that moment except for its two buttons. *Ours:* the country whose models the card asks after is the owner's, since we do not hold a ship's builder.

`photo`: `gfx/models/<TAG>_<type>_<model>.tga`, else `gfx/models/<type>_<model>.tga`, else `gfx/models/<type>_0.tga`, the file's `GFX_no_ship_selected` if none exists. With no model the original tries a path it has put together with no folder, which is never found. `label`: `(§Y<model>§W)\n`. Stars: the frame of a unit panel's brigade, from its experience with `PRIDE_BONUS_EXP` (a define) on a pride; the land, air or naval icon by its type. `subunit_name` is typed into and refuses `"`; **a name typed goes out when the card shuts, trimmed, if it differs and is not empty, and never while it is up** (`CRenameSubUnitCommand`, the old name given up from the country's names in use, the new one taken).

`list`, label left and value right; a figure is cut to the places given:

| row | value |
| --- | --- |
| `UNIT_STATS` | nothing |
| `BV_STRENGTH` | a brigade's men; a ship or wing its strength, 0 places and `%` |
| `BV_EXPERIENCE` | with the pride's bonus, 1 |
| `MAX_STRENGTH` | a land brigade's only, men |
| `DEFAULT_ORG` | 1 |
| `DEFAULT_MORALE` | times 100, 1 and `%` |
| `RADIO_STRENGTH` | 2 |
| `MAXIMUM_SPEED` | 2, then `KPH` with no space |
| `SUPPLY_CONSUMPTION`, `FUEL_CONSUMPTION` | the brigade's own, 2 |

Then by kind. **Land**: `TRANSPORT_WEIGHT`, `DEFENSIVENESS`, `TOUGHNESS`, `SOFTNESS` (times 100, 0 and `%`), `AIR_DEFENCE`, `SUPPRESSION`, `SOFT_ATTACK`, `HARD_ATTACK`, `AIR_ATTACK`, `COMBAT_WIDTH`, `AP_ATTACK`, `ARMOR_VALUE`. **A ship**: `RANGE` (0, then `KM`), `DISTANCE` (1, then `KM`), then the detection, defence and attack figures, `HULL`, `POSITIONING`, `TRANSPORT_CAPABILITY`, the two amphibious figures and `CARRIER_CAPACITY`. **A wing**: `SURFACE_DEFENCE`, `AIR_DEFENCE`, `STRATEGIC_ATTACK`, `SOFT_ATTACK`, `HARD_ATTACK`, `TRANSPORT_CAPABILITY`, `AIR_ATTACK`, `SEA_ATTACK`. These 2 places but where said.

Then a blank row; `NIGHT_ADJUSTER`, `FORT_ADJUSTER`, `RIVER_ADJUSTER`, `AMPH_ADJUSTER` and each terrain in the map's order, up to four rows each for what is not nothing - the name, a space and `UA_ATTACK`, `UA_DEFENCE`, `UA_MOVEMENT` or `UA_ATTRITION`; `+` where above nothing, then **the figure times 100 to 2 places and `%` - attrition the figure itself, with the `%` all the same**; a blank row; `UNIT_MODELS`; and for each technology on the brigade its name and, `§Y` if it may upgrade, Yes or No or the level's name, and how far towards the next.

The file's `model_overlay` and `background` are not touched.

`sunk` and `make_pride` are shown for a ship alone. `sunk` is pressable where the ship has sunk something. `make_pride` is shown where its type has `can_be_pride`, greyed where it is the pride already. Neither has a tooltip.

## 4. The pride of the fleet

`make_pride` raises `DefaultConfirm`, another on each press and nothing guarding it: `CONFIRM_MAKE_PRIDE_TITLE` and `CONFIRM_MAKE_PRIDE_DESC` with `$EFFECTS$`, `MAKE_PRIDE_EXP_EFFECT` with the define, `MAKE_PRIDE_SUNK_EFFECT`, whose 2 is a literal in the table, and where the country has a pride `MAKE_PRIDE_PREVIOUS_EFFECT` and, above nothing, `MAKE_PRIDE_DISSENT_EFFECT`.

`CMakePrideCommand` finds the country's first pride, the first ship so marked in its units as made; adds the redesignation's dissent, floored at nothing, and takes its mark; and marks the ship. It tests nothing else - not the type, and not the same ship again.

The dissent is `PRIDE_BASE_REDESIGNATION_DISSENT` (a define, 1), divided by the difference in technology levels plus one where the new ship holds more, and halved where its hull is the greater. Truncated each time; nothing with no old pride.

## 5. `sunk_ships_view`

From `sunk` alone, another on each press. `label` is `SUNK_SHIP_LABEL`. `list` has a row for every ship on the list of those that have left the game whose sinker is this one, in the order they left, as the list stood when the window opened. A row's `name`, its counter, `country` the sunk ship's owner's flag, `model` its model. No row does anything and nothing has a tooltip.

## 6. What is the original's and looks wrong

Attrition's row is not times 100; a pride's 2 dissent on the dialog is a literal in the table and `PRIDE_SUNK_DISSENT_IMPACT` is read by nothing; `KM` has no text in the tables and reads as its key; a wing has no range row, where a brigade's summary gives one; the command tests nothing.

*Unbuilt:* the `+*` that marks a model not the one the country builds today, as in a brigade's summary (`unitpanel.md`).

## 7. The card of a whole unit - an enhanced game's

*Ours, asked for on 2026-11-22: "make the ribbon which hold the amount of subunits in a unit clickable ... This
would open a page just like when selecting a single subunit, but with the stats for the whole division (and
anything missing from it)."* `CUnitCardText`, `CUnitCard`; it stands where BlackICE's utility had its Unit
Inspector. Nothing of it is the original's, and a faithful game has none of it.

**In an enhanced game a click on the ribbon over a selected unit's brigades** - `sup_subunits`' `bg_label_brigade`,
which says how many it has - opens a card of the whole unit in the brigade card's own window, `brigade_view`, so a
mod that restyles the one restyles the other. The ribbon's tooltip is the text of `OPENHOI_UNITCARD_TIP`. One card
to a unit, as many of different units as are asked for; filled once as it opens; it goes when it is shut or its
unit can no longer be found. A unit with no brigade of its own has none.

`photo` and the stars' kind are **the unit's first brigade's**, which is its highest in `priority` - a division's
headquarters brigade where it has one; the stars are the unit's average experience. `subunit_name` is the unit's
name and is not typed into. `label` is the text of `OPENHOI_UNITCARD_ENTIRE`, "Entire unit". `sunk` and
`make_pride` are hidden.

`list` has the brigade card's rows in its order and format, each figure the unit's **as the game makes it from
the brigades'**:

| row | the unit's |
| --- | --- |
| `BV_STRENGTH` | the men added up; a fleet's or an air unit's average strength |
| `BV_EXPERIENCE` | the average, a pride's bonus with it |
| `MAX_STRENGTH` | added up |
| `DEFAULT_ORG`, `DEFAULT_MORALE` | averaged over every brigade, those that no longer count giving nothing |
| `RADIO_STRENGTH` | the first brigade's, which is what a headquarters reaches by (`leaders.md`, section 8) |
| `MAXIMUM_SPEED` | the slowest brigade's |
| `SUPPLY_CONSUMPTION`, `FUEL_CONSUMPTION` | the unit's day (`supply.md`) |
| land: weight, defensiveness, air defence, suppression, the three attacks | added up |
| `TOUGHNESS` | added up and doubled, as a battle counts it (`landcombat.md`) |
| `SOFTNESS` | averaged |
| `COMBAT_WIDTH` | added up and by the country's modifier, never under one |
| `AP_ATTACK`, `ARMOR_VALUE` | the best brigade's, as a battle compares them |
| a fleet, an air unit | `RANGE` the shortest; attacks, defences, hull and what it carries added up; detection, visibility, firing distance and the two amphibious figures the best ship's; positioning averaged |

Then a blank row and **what the unit cost, which no brigade's card says**: the heading `OPENHOI_UNITCARD_COST`,
`OPENHOI_UNITCARD_OFFICERS`, `_MANPOWER` and `_IC` added up, and `_TIME` the longest brigade's, built side by side
as they are. Then a blank row and the circumstances and terrains as on a brigade's card, each the brigades' over how
many the unit has, which is how a battle and a march take them. The technologies are a brigade's and are not
listed.

*Ours, and a guess where the game has no such figure:* a fleet's and an air unit's rows but speed, range and what
they use - their ships and wings fight one by one, so a sum or a best there is a summary and not a figure the
game holds. The old inspector's column of a unit's speed on each terrain is not carried over.

The texts are the mod's, in `localisation/openhoi`.
