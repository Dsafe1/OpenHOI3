# A battle on screen

What the original shows of a land battle - the window, its row in a unit's panel, the marker
on the map and the arrow of the unit that attacks - written as rules this project can
implement. It is the third piece of land combat; what a battle *does* is `landcombat.md`.

Read out of the original on 2026-10-09: `reversing/findings/FINDINGS-combatview.md`.
`confirmed` means read; *measured* means taken off the maintainer's three screenshots of a
battle of his own, of that day, set beside ours; *ours* means decided here. It was built from
the reading, tried with real clicks (`--click`), and then **set against those screenshots,
which corrected four things**: where a turned flag stands, that a bar's picture is drawn
upside down, that a round shield's flag is cut to a disc, and that a name too long for its box
ends in three dots (`interface.md`, sections 5 and 7). After that the window's upper half lies on the
original's to within a pixel.

In the code: what it says is `lib/OpenHOI3.Interface/Screens/CCombatViewText.cs`; the window
is `src/Screens/CCombatWindow.cs` with `CCombatSideView` and `CCombatUnitEntry`, the panel's
row `src/Screens/CUnitCombatEntry.cs`, the marker `src/Map/CBattleMarkers.cs`.

**Only land battles.** The window serves a battle at sea and in the air too, with other side
windows; neither is built, since neither is fought.

---

## 1. How the window comes up and goes away

`confirmed`

- **A battle is a thing that can be selected, like a unit.** Selecting it brings its window
  up; selecting anything else takes it down.
- **A click on its row in a unit's panel** (section 7) lets go of the unit selected, selects
  the battle and puts the province fought over in the middle of the map. **Ours leaves the
  map where it is** - the maintainer's word, 2026-10-09; `docs/compatibility.md`, entry 8.
- **The window takes itself down**: it is written again every frame, and goes when either
  side has no units, or when both of a side's lists are empty.
- *Ours:* a click on the battle's **marker** (section 8) does what a click on the row does
  here: the window comes up and the map does not move.
  What the original's marker does when clicked was not read; it is how the game is remembered.
- *Ours:* selecting a unit takes the window down, and the window stands where the file puts
  it, which is where the unit panel is. The original hides something when a battle opens that
  was not identified; by what is on screen it is the panel of the selected unit.

## 2. The window

The window is `combat` of `interface/combat.gui`. `confirmed` unless marked.

- **Its two halves are windows of their own**, `combat_side_l` for the attacker and
  `combat_side_r` for the defender, hung on it as children. Each frame the attacker's is put at
  the place of the icon `left_pos` and the defender's at `right_pos`: those two icons are
  there for nothing else.
- **The picture**, `combat_terrain`: the file `gfx/interface/prov_pictures/provimg_` + the
  province's number + `.dds` where there is one, drawn in the shape of the sprite
  `GFX_terrainimg_plains`; otherwise the sprite `GFX_terrainimg_` + the name of the province's
  terrain.
- **Three backgrounds**, `combat_bg`, `combat_naval_bg` and `combat_air_bg`: the one for the
  kind of battle is shown and the other two hidden.
- **`label_battlename`** is the battle's name, set once: the text `BATTLE_OF`, a space, and
  the name of the province. Nothing else goes into it.
- **`closebutton`** takes the window down. **`retreat_button`** orders every unit of the
  player's in the battle to stop, whichever side it is on. *Ours:* a unit that is attacking
  is halted, which breaks its attack off (`landcombat.md`, section 9); a unit that is not on
  the march has nothing to stop, and is left.

## 3. Every frame

`confirmed`, in this order:

| element | what it is given |
| --- | --- |
| `combat_progress` | a bar: the attacker's share (section 6) in whole hundredths, the fraction cut. The first of its two pictures, the brown one, is the attacker's part |
| `combat_width` | the battle's width (`landcombat.md`, section 3) as a whole number |
| `combat_attack_indicator` | a frame: 2 where the attacker's tactic counters the defender's, 3 where the defender's counters the attacker's, 1 for neither. Where both hold it is 2 |
| `retreat_button` | enabled; a land battle may always be left |
| `combat_modifiers` | an icon for each modifier in play, below |

**The modifiers.** The box is a row of overlapping elements. For each modifier that this hour
holds for or against **any unit of either side**, once, a small window `combat_modifier` is
added whose icon `combat_mod_icon` takes the sprite `CMOD_` + the modifier's key - the keys of
`landcombat.md`, section 4, as the original names them: `BM_RIVER_PENALTY`, `BM_EXPERIENCE`
and the rest. The icons stand in the original's own order of its modifiers, which of the ones
built here is: dissent, envelopment, stacking, experience, territorial pride, dug in, landing,
fort, river, supplies, more than one battle, combined arms, terrain, weather, night. The box
is filled again only when the number of modifiers in play changes. *Ours:* it is filled again
when the set changes.

**The original works the modifiers out again for the window**, every frame, before it reads
them. *Ours:* the window shows what the battle's last hour made of each unit, which is the
same thing once an hour has been fought - and a battle on screen always has had one.

## 4. A side

`confirmed`

| element | what it is given |
| --- | --- |
| `committed_label` | the text `COMMITED_LABEL` with `$COUNT$` the units on the front and `$WIDTH$` their widths added up, whole (`landcombat.md`, section 3, for a unit's width) |
| `reserves_label` | `RESERVES_LABEL` with `$COUNT$` the units in reserve |
| `committed`, `reserves` | two lists, each with an entry (section 5) for every unit of that part of the side, **in the side's own order** |
| `shield` | the flag of the side's leader's country; with no leader the first country the side fights as; with no country left, of whoever holds the province |
| `leader_photo`, `leader_name` | the portrait and the name of the side's highest ranked leader, or for a side with none the text `NO_COMMANDER` under the picture the file gives |
| `initiative_indicator` | shown on the side whose leader has more skill than the other's; hidden where they are equal |
| `tactic_name` | the side's tactic by its name in the player's language - the tactic's own key is the text's |
| `tactic_picture` | the file `gfx/pictures/tactics/` + the tactic's `picture` + `.dds`, where that file exists, in the shape of the sprite `GFX_empty_tactics` |

- **A list is kept, not made again**: while it holds the same units each entry is only written
  again; when a unit comes or goes the list is emptied and filled.
- **A side with no tactic** keeps whatever name and picture it showed.
- *Ours:* leaders are not built, so every side says `NO_COMMANDER`, keeps the file's empty
  portrait, and nobody has the initiative.

## 5. A unit in a list

Each is a window `division_option`. `confirmed`

| element | what it is given |
| --- | --- |
| `counter_type` | the sprite `GFX_counter_` + the type of the unit's first brigade, where there is such a sprite |
| `name` | the unit's name |
| `location` | the name of the province it is in |
| `unit_cw_strip` | a frame by its brigades' widths added up: 1 under 2, 2 under 4, 3 under 6, 4 under 8, 5 from 8 - the unit panel's own strip |
| `str_bar` | its brigades' strength over what they have at full strength, in whole hundredths |
| `org_bar` | its average organisation over the most it can have, the same |
| `combat_unit_bg` | a bar, full. It is the plate the entry lies on; for a unit landing from the sea it shows how far the landing has got |

- *Ours:* the two bars are the unit panel's (`unitpanel.md`, section 3). The original takes
  the organisation bar's whole from the unit's summed figures and the panel's from a routine
  of its own; whether the two can differ was not looked for.
- *Measured:* a name too long for its box is cut and ends in three dots - "6a Divisione CCN
  ..." on the screenshot (`interface.md`, section 7).

## 6. The share

One number stands behind the bar, the row's bar, the marker's number and the marker's
colour: **the attacker's share of what the two sides are worth**, in thousandths. `confirmed`

    share = attacker's worth * 1000 / (attacker's worth + defender's worth)        each at least 1

and a side is worth, added up over **every brigade of every unit on it, on the front or not**:

    body  = the brigade's organisation * its strength
    hour  = what the hour makes of its unit's attack * what it makes of its defence
    arms  = (soft attack + hard attack + toughness) / 10          the brigade's own;
                                                                  defensiveness for a defender
    worth = body * hour * (arms where arms is more than 1)

- **Both of the hour's figures count for both sides**: a unit crossing a river at a third of
  its attack is worth a ninth, since the river is against its defence too. That is how one
  division attacking another across a river reads **6** on the maintainer's screenshot.
- It is a figure for the eye and for nothing else. The original works it out only for a battle
  the player has a part in, and nothing in the fight reads it.

## 7. The row in a unit's panel

A unit's panel (`unitpanel.md`) ends with a row for each battle the unit is in, the window
`combat_entry` of `single_unitpanel.gui`. `confirmed`

| element | what it is given |
| --- | --- |
| `desc` | the battle's name |
| `progress` | the same bar as the window's |
| `attacker_flag` | the flag of the first country the attacker fights as |
| `defender_flag` | the same for the defender, or with none the flag of whoever holds the province |
| `select` | the row's button: section 1 |

The row is written, and takes a click, only while the battle is still one of the unit's and
both of its sides have a country and a unit.

- *Measured:* the two flags are round. The sprite's mask is a disc, and the flag is cut to it
  (`interface.md`, section 5).

## 8. The marker on the map

`confirmed` unless marked.

- **Only for a battle the player has a part in.** Nobody else's battle has a marker.
- **It is a billboard** - a flat picture that faces the viewer - declared in
  `interface/mapitems.gfx` twice, as the counters are: `combat_status` from further off and
  `combat_status_close` from close by, each a strip of three frames, with a `scale` that is
  half its side in map units.
- **Its frame says how the battle goes for the player**: the picture is green, yellow, red.

  | the attacker's share | the player attacks | the player defends |
  | --- | --- | --- |
  | above 660 | green | red |
  | above 330 | yellow | yellow |
  | otherwise | red | green |

- **Its number** is the share as a percentage with nothing after the point, cut and not
  rounded: a share of 64 thousandths reads 6.
- **It stands half way between the two sides' first units**, where their counters are.
- **It points the way the attacker's first unit is going.** *Ours:* for a unit with nowhere
  to go, towards the defender.
- *Measured* on the screenshot: the number is written upright in the middle of the round
  part, a line of it eight tenths of the badge's half side high. *Ours:* the font is the
  one the declaration names, as an outline (`interface.md`, section 6).

## 9. The arrow

The arrow of a selected unit (`movement.md`, section 7) is the `attackArrow` of
`interface/arrows.gfx` - red where the march's is green - **where an enemy of the unit's
country holds the next province of its way**, whether or not anybody stands there; and the
`retreatArrow` for a unit falling back, which is asked first. `confirmed`. It is drawn as
the march's is, hollow ahead of the unit and filled behind it, so an attack that has hardly
begun is two red lines with an outlined head.

---

## What is not built

- **Leaders**: the portrait, the name, the initiative mark.
- **Tooltips.** Everything in the window has one - the terrain, the width, a unit with what
  each modifier is worth to it, a tactic with what it does and what counters it, the arrow
  between the tactics - and none is drawn anywhere yet.
- **The sounds** a window plays while it is up.
- **A battle at sea, in the air, and a bombing**, which have side windows of their own.
- **The enemy's counters**: the original writes question marks on a counter whose unit it
  does not know enough about, name and figures both - the defender's on the screenshot. Here
  every counter says what its unit is.
- **Where units in a battle stand.** The original has a place in each province for the
  attackers and one for the defenders; here a unit in a battle stands where it stood.

## What is not established

- **What a click on the marker does** in the original.
- **How the original measures a name for cutting.** On the screenshot the name ends ten
  pixels short of its box; here it is cut where it and the dots still fit.
- **A turned flag's place** is right for the two the window has, a negative quarter turn, and
  for nothing else.
- Nothing here was watched in a running game. The screenshot agrees with it wherever the two
  can be compared.
