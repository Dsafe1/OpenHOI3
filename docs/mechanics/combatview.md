# A battle on screen

What the original shows of a land battle - the window, its row in a unit's panel, the marker
on the map and the arrow of the unit that attacks - written as rules this project can
implement. It is the third piece of land combat; what a battle *does* is `landcombat.md`.

Read out of the original on 2026-10-09: `reversing/findings/FINDINGS-combatview.md`.
`confirmed` means read; *measured* means taken off three screenshots of a
battle in the original, of that day, set beside ours; *ours* means decided here. It was built from
the reading, tried with real clicks (`--click`), and then **set against those screenshots,
which corrected four things**: where a turned flag stands, that a bar's picture is drawn
upside down, that a round shield's flag is cut to a disc, and that a name too long for its box
ends in three dots (`interface.md`, sections 5 and 7). After that the window's upper half lies on the
original's to within a pixel.

In the code: what it says is `lib/OpenHOI3.Interface/Screens/CCombatViewText.cs`; the window
is `src/Screens/CCombatWindow.cs` with `CCombatSideView` and `CCombatUnitEntry` - for a battle
at sea `CNavalSideView` and `CCombatShipEntry`, for one in the air and for a bombing
`CAirSideView` and `CCombatWingEntry`, whose text is `CAirCombatText.cs` beside the other -
the panel's row `src/Screens/CUnitCombatEntry.cs`, the marker `src/Map/CBattleMarkers.cs`.

**A battle at sea has the same window and row, with other halves**: section 10, built on
2026-10-10 from a reading and tried with real clicks. **It was reviewed in play that day:
"The sea battle window looked perfect."** No screenshot of the original's was laid beside it
and measured, as three were for the land window.

**A battle in the air and a bombing have the window and the row too, with a third kind of
half**: section 11, built on 2026-10-10 from a reading (`reversing/findings/FINDINGS-combatview.md`,
sections 13 and 14) and tried with real clicks. **It has not been reviewed in play**, and
nothing of the original's was laid beside it.

**Only a battle on land has a marker on the map.** The reading for the air side found the one
place the marker is refreshed, and it leaves out a battle at sea, a battle in the air and every
bombing: section 8. A battle at sea was built with the land battle's marker on 2026-10-10, from
a sentence of the fact base that was wrong, and has none since that evening - **which has not been raised for a decision**, and the
review in play saw the marker on the sea.

---

## 1. How the window comes up and goes away

`confirmed`

- **A battle is a thing that can be selected, like a unit.** Selecting it brings its window
  up; selecting anything else takes it down.
- **A click on its row in a unit's panel** (section 7) lets go of the unit selected, selects
  the battle and puts the province fought over in the middle of the map. **Ours leaves the
  map where it is** - decided on 2026-10-09; `docs/compatibility.md`, entry 8.
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
- **`closebutton`** takes the window down. **`retreat_button`** gives every unit of the
  player's in the battle, the attackers' first and then the defenders', whichever side it is
  on, the original's move order with no target - **go where a beaten unit goes** [exe]
  (`reversing/findings/FINDINGS-movement.md`, section 2). For a land unit that is the
  province beside the one it stands in that is worth most to fall back to (`landcombat.md`,
  section 9) - an attacker's from its own province, so it is not stopped where it stands as a
  beaten attacker is. With a way there, the unit leaves every battle it is in, is marked as
  falling back where the other side still has somebody, and is left with no order; with
  nowhere to go, or no way there, **nothing happens** - the button removes nobody, where a
  beaten unit with nowhere is removed from the game. Built so since 2026-11-14
  (`CFallBackCommand`); until then a land unit was only halted, which broke an attack off and
  left a defender standing.

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
| `shield` | the flag of the owner of the side's leader's unit; with no leader the first country still fighting on the side; with none left, of whoever holds the province |
| `leader_photo`, `leader_name` | the portrait and the name of the side's highest ranked leader, written again only when he changes; for a side with none the text `NO_COMMANDER`, the picture left as it was; with no country left either, both left as they were |
| `initiative_indicator` | shown on the side whose leader has more skill than the other's; hidden where they are equal |
| `tactic_name` | the side's tactic by its name in the player's language - the tactic's own key is the text's |
| `tactic_picture` | the file `gfx/pictures/tactics/` + the tactic's `picture` + `.dds`, where that file exists, in the shape of the sprite `GFX_empty_tactics` |

- **A list is kept, not made again**: while it holds the same units each entry is only written
  again; when a unit comes or goes the list is emptied and filled.
- **A side with no tactic** keeps whatever name and picture it showed.
- **The leader is built** (2026-10-11, with the window a leader is chosen in: `leaders.md`,
  section 12), for the three kinds of side alike, and tried with real clicks on Italy's attack at
  Gurbo: Giorgis for Italy, Imru - a corps commander in reserve, of higher rank than the
  division's general on the front - for Ethiopia, with the arrow of initiative his.
- "The countries still fighting" is the side's list the original makes again whenever a unit
  leaves it (`CCombatSide.Standing`); until 2026-10-11 the flag and `NO_COMMANDER` were asked of
  the list of every country the side ever fought as, which differs only once a country's units
  have all gone.

## 5. A unit in a list

Each is a window `division_option`. `confirmed`

| element | what it is given |
| --- | --- |
| `counter_type` | the sprite `GFX_counter_` + the type of the unit's first brigade, where the player knows the unit at intel level 3 or more and it has a brigade; otherwise `GFX_counter_unknown`, a counter box with a "?" - set only where there is such a sprite. The 3 is hardcoded, the map counter's own threshold (`mapunits.md`, section 3); a friend's units are always known. **Nothing else of the entry is hidden** - the name, the place, the bars and the tooltips are the unit's whatever is known, and no element writes `???` or `?`. The picture is chosen as the entry is made and kept until the list is made again. Ships and wings are never hidden (`reversing/findings/FINDINGS-combatview.md`, section 15) |
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
  division attacking another across a river reads **6** on a screenshot of the original.
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
- **Only for a battle on land, and not before its fourth hour.** The original refreshes the
  marker in one place, at the end of a battle's hour, beside the two questions of who has lost -
  which are not asked in the first three hours - and only where the battle is no bombing and is
  neither at sea nor in the air. It is made the first time it is refreshed. So a battle at sea,
  a battle in the air and a bombing never have one, and a land battle has none for three hours.
  A battle against a convoy has none either.
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

## 10. A battle at sea

`confirmed` (`reversing/findings/FINDINGS-combatview.md`, section 12) unless marked. What a
battle at sea *does* is `navalcombat.md`. Nothing here was measured against a picture of the
original's; the window was reviewed in play on 2026-10-10 and it "looked perfect".

**The row in a fleet's panel and the window are the land battle's** - one row, one window -
with the same share behind the bar (section 6), worked out for ships. **It has no marker**
(section 8); this section said it had the land battle's until the reading for the air side:

    a side is worth, for each ship of each of its fleets:
        organisation x strength x what the hour makes of the ship's attack x of its defence

a ship's guns and its type do not come into it.

**The window** differs from a land battle's in this:

- its background is `combat_naval_bg`, and it has no picture of the ground;
- the arrow between the two tactics is hidden: nobody at sea has one;
- **the retreat button cannot be pressed in the battle's first four rounds**, when no fleet can
  leave (`navalcombat.md`, section 6). Pressed, it gives every fleet of the player's in the
  battle the original's move order to "retreat": the province a beaten fleet would run for
  (`navalcombat.md`, section 6, *Falling back*), and nothing where there is none. A fleet that has not
  counted long enough keeps it and starts its count, its ships opening the distance, and
  sets off for it once the count has run - four hours with no trait; one already counted
  out, by the battle's own roll, is given its way at once (since 2026-11-14,
  `reversing/findings/FINDINGS-movement.md`, section 2; `FINDINGS-seabattle.md`, section 5);
- the icons of what the hour holds for the ships are the same ones, of every ship on either
  side: mission efficiency, poor screens and the submarines' surprise have theirs;
- its two halves are `naval_l_combat_side` for the attacker and `naval_r_combat_side` for the
  defender, and it goes away when either side has no fleet left.

**A half** has the flag and the leader as on land (section 4), and:

- **`positioning`**: the side's positioning as a percentage with one figure after the point;
- **`ships_label`**: the number of the side's ships, in yellow, and the text `SHIPS_LABEL`;
- **`ships`**: one list of every ship of every fleet on the side, **fleet by fleet in the order
  they joined and ship by ship in each fleet's order**. While it holds as many rows as the side
  has ships and each row's ship is still there, each row is only written again; otherwise the
  list is made anew.

**A ship's row** is the window `ship_l_option` on the attacker's side and `ship_r_option` on the
defender's:

| element | what it is given |
| --- | --- |
| `name` | the ship's name |
| `str_bar` | its strength over its full strength, in whole hundredths |
| `org_bar` | its organisation over the most its type has, the same |
| `ship_icon` | the sprite `GFX_navalcombat_l_` or `GFX_navalcombat_r_` + the ship's type, where there is one; else the one the file gives the row |

- **The picture stands along the row by the ship's distance** (`navalcombat.md`, section 2):

      t = distance / 40, and 1 at most
      o = t x (the row's width - the picture's), cut to a whole number
      the defender's, on the right:  o - 19 from the row's left edge
      the attacker's, on the left:   the row's width - o - the picture's width + 19

  So the end of each row nearer the middle of the window is "at the enemy", the far end is forty
  and more away, and the two fleets are seen closing on one another across the gap between the
  two lists - nineteen past the near end at their closest.
- **Its frame** is by whether the ship has a target still in the battle: an attacker's the
  second with one and the first with none, **a defender's the first with one and the second
  with none**. The two sides' pictures are different files. What the frames show was not read.

*Ours:* a ship with no name of its own - the throwaway ships of a raid never get here - shows
its type's key. Each side's leader is shown since 2026-10-11 (`leaders.md`, section 12) and the sound since 2026-11-12 (`sound.md`, section 8). A row's tooltip is built since
2026-10-12 (section 12).

**On the map** there is nothing: the window is reached by the row in a fleet's panel.

## 11. A battle in the air, and a bombing

`confirmed` (`reversing/findings/FINDINGS-combatview.md`, section 13) unless marked. What they
*do* is `aircombat.md` and `bombing.md`. Nothing here was measured against a picture of the
original's, and it has not been reviewed in play.

**The row in a unit's panel and the window are the land battle's**, and neither has a marker
(section 8). A bombing's row comes **after** the rows of the unit's battles, and is written for
as long as the bombing is the unit's - it is not asked whether both sides have a country and a
unit, as a battle's row is. A division under bombs has the row too.

**The name** of a bombing is the text `BOMBING_OF`, a space and the province's name; a battle
in the air is "Battle of", as on land.

**The window** differs from a land battle's in this:

- its background is `combat_air_bg`, it has no picture of the ground and no arrow between
  tactics;
- **the retreat button can always be pressed.** Pressed, the original gives every unit of the
  player's in the battle one order, whatever the battle: go where a beaten unit goes
  (`reversing/findings/FINDINGS-movement.md`, section 2). For an air unit that is
  the sea beside it with the most ports around, or with no sea its base (`aircombat.md`,
  section 8); with nowhere to go, or no way there, nothing happens. With a way, the unit leaves
  every battle and the bombing it is in, is marked as falling back where the other side still
  had somebody, and **is left with no order** - its mission is over. *Ours:* that the leaving
  covers a unit that is in a bombing and no battle; the original's test was read for battles;
- the icons of what the hour holds are the same ones, of every wing - and under bombs of every
  regiment and ship: the radio, the nearness of a base, a radar, the intercept and a battle of
  its own each have theirs, between the others in the original's order;
- its two halves are both made from the one window `air_combat_side`;
- **it goes away** when either side of a battle in the air has nothing in its list, and when
  the **bombers** of a bombing have nothing in theirs. The bombed side's list is not asked: a
  bombing of a province with nobody in it has a window.

**A half** has the flag and the leader as on land (section 4), and:

- **`planes_label`**: the number of the side's sub units, in yellow, and the text
  `PLANES_LABEL`. **The bombed side of a bombing has an empty one.**
- **`planes`**: one list. Every side has a row for each sub unit of each of its units, unit by
  unit in the order they joined - wings, and under bombs regiments and ships. **The bombed side
  of a bombing has rows before those**, by what the mission of the **first** bomber unit bombs
  (`bombing.md`, section 7):

      for each building, in the buildings' own order:
          one the mission bombs, of which the province has built any        ->  a row
      for each good, in the goods' own order:
          a good whose stockpile the mission bombs                          ->  a row of what lies there
          a good whose production the mission bombs,
          of which the province can make any                                ->  a row of what is made

  So a strategic bombing shows the guns, the factories and each resource the province digs; a
  logistical strike the guns, the roads, the supplies and the fuel - the last two whether any
  lies there or not. A second bomber unit under another mission adds nothing.
- While the list holds the same rows each is only written again; otherwise it is made anew.
  *Ours:* the original counts its rows and asks each whether it still stands for something,
  which comes to the same.

**A row** is the window `wing_option`:

| the row stands for | `name` | its picture | `str_bar`, in whole hundredths |
| --- | --- | --- | --- |
| a sub unit | its name | `counter_type` takes the sprite `GFX_counter_` + its type, where there is one | its strength over its full strength; and `org_bar` its organisation over the most its type has |
| a building | the building's name | `counter_type` takes `icon_build_` + the building's key, where there is one | what works of it over what was built (`bombing.md`, section 7, for a building's two levels) |
| what lies there of a good | the good's name | `pool` shows the good's frame | what lies in the province over **200** - full at two hundred |
| what is made of a good | the good's name | `produce` shows the good's frame | what the province makes today over the most it can make |

- Only a sub unit's row has the second bar; `produce` and `pool` are hidden on every row but
  their own. A good's row leaves `counter_type` as the file gives it, under the good's picture.
- A good's name is the text `SUPPLIES_IRO`, `FUEL_IRO`, `MONEY_IRO`, `CRUDE_OIL_IRO`,
  `METAL_IRO`, `ENERGY_IRO` or `RAREMAT_IRO`, and its frame its place in that order, from one.
- A figure over nothing is no bar at all.

**The share** behind the window's bar and the row's is section 6's, **taken sub unit by sub
unit**: a regiment counts as in a land battle, by its unit's hour and a tenth of its arms; a
ship and a wing by their organisation, their strength and what the hour makes of *them*, and by
nothing of their type. So bombers over a province with nobody in it show a bar that is all
theirs, and a division under bombs is worth what it is in a battle.

*Ours:* a building is asked nothing but whether the mission bombs it and the province has any;
the original asks the building one thing more, which was not identified. *Ours:* a unit's panel
says a bombing mission is "attacking" the province while the unit is in a bombing there, and
"resting" otherwise (`bombing.md`, section 13). *Not built:* a bombed row's tooltip.
Each side's leader is shown since 2026-10-11 (`leaders.md`, section 12) and the sound since 2026-11-12 (`sound.md`, section 8). A wing's row's tooltip is built since 2026-10-12 (section 12).

**On the map** the original shows a battle in the air and a bombing only once they are over,
and only in its air map mode: three small icons to a province - an air battle, "bombed us",
"bombed them" - counted each day out of the list of finished battles
(`reversing/findings/FINDINGS-combatview.md`, section 14). Built since 2026-10-11, with the
list of finished battles: `airmap.md`, section 4.

## 12. Tooltips

Read 2026-10-12, `reversing/findings/FINDINGS-battletooltips.md`; built in
`lib/OpenHOI3.Interface/Tooltips` - `CBattleTooltips`, `CBattleUnitTooltips` and
`CBattleRowTooltip` - and tried with `--click`. `confirmed` unless marked.

**Who answers.** The in-game screen gives every element of the window to the window's own
routine by its group - the window's and its entries' - except **`closebutton`, which says
`CLOSE`** before any window is asked, and **the icons of the modifiers in play**, which carry the
modifier's id and say **the modifier's name, and after a moment its `_desc` text**. Each half and
each entry answers for its own elements here, the window for the rest.

**The window, by the element's name**, the first that holds:

| element | what it says |
| --- | --- |
| `combat_terrain` | the battle's terrain's name |
| `combat_width` | `COMBAT_WIDTH` |
| `positioning` | `FLEET_POSITIONING` |
| `retreat_button` | `COMBAT_RETREAT` |
| an entry | its own, below - on land the attacker's front, **the attacker's reserves their chance**, the defender's front and **the defender's reserves a unit's whole** |
| `tactic_name`, `tactic_picture`, on land | that side's tactic's own text |
| `combat_attack_indicator`, on land | `DEFENDER_COUNTERED`, `ATTACKER_COUNTERED` or `NONE_COUNTERED`, by section 9 |
| `leader_name`, `leader_photo` | the side's highest ranked leader's tooltip (`unitpanel.md`, section 10.5), and after a moment `LEADER_INITIATIVE` where his skill is above the other side's leader's |

**A land unit** - `str_bar` `CURR_COMB_STR` and its strength as a percentage to one place,
`org_bar` `CURR_COMB_ORG` and its organisation, `unit_cw_strip` `UNIT_COMBAT_WIDTH` and its width;
anything else, the whole:

    LEADER_COMMAND_INFO with his NAME and SKILL; with no leader NO_LEADER_FOR_THIS_2 for a lone
          brigade, NO_LEADER_FOR_THIS_1 for more, and a line break
    BATTLE_ATTACKMOD + ": §Y" + its attack as a percentage, 2 places + "§!%" and a line break
    BATTLE_DEFENDMOD the same with its defence
    each of its combat modifiers: its name + ": " + its figure + "§W%\n"; where it does not do
          the same to attack and defence, the attack's, "§W%/", the defence's - a figure "§R" at
          nothing or less, "§G+" above, times 100 to 2 places
    its side's tactic's figures as the side feels them (below)
    AP_ATTACK + ": §Y" + its best piercing attack, trimmed + "§!\n"
    ARMOR_VALUE the same with its best armour

A landing's scale makes the first figure the figure times the scale, green above the figure and
red below, and then " / " and the figure itself in the other colour; with no landing the line is
one yellow figure. **A landing unit's lines** - in the transports, its progress, its two scales -
come after the leader's, and are `transport.md`'s, section 12; built since 2026-10-15 and set
beside three screenshots of the original of a landing at Danzig, figure for figure.

**An attacker's reserve** says `BATTLE_REINFORCE_CHANCE` + `: §Y` + its chance of coming up
(`landcombat.md`) as a percentage to two places + `§W%`, whatever is under the mouse.

**A ship** - its bars as a unit's; anything else:

    "==>" + its target, green where an enemy and red where a friend it mistook, + "§W";
          with none "<> " - so "==><>  (", two spaces, kept as read
    " (" + its distance whole + "km)" and a line break
    in a battle: EFF_POS + " : §Y" + its positioning and its side's, times 100, scaled by its
          country's targeting_chance (inferred: the original's figure is named
          naval_positioning_bonus), 1 place + "%§W\n"
    BATTLE_FIRINGRANGE + its distance shortened by the weather, 1 place + "km", and where the
          weather does that, "(" + red or green + by how much as a whole percentage + "%§W)";
          a line break
    BATTLE_ATTACKMOD and BATTLE_DEFENDMOD + ": §Y" + the figure, 2 places + "§W%\n"
    its combat modifiers, as a unit's

**A wing** - its bars, and anything else its attack, defence and modifiers as a ship's. **A row
of what is bombed** says a figure wherever it is asked (since 2026-11-14,
`CBattleUnitTooltips.BombedRow`): a building its working level to two places, `/` and its built
level to none - `2.50/4`; a good held what lies in the province, two places; a good made what the
province makes now `/` the most it can, both to two places [exe].

**A tactic's own text**: its figures, a line break; `TACTIC_COUNTERED_BY` with the tactic that
counters it; and `TACTIC_COUNTERS`, then a line break where it counters more than one, and each
on a line of its own after `§Y` and two spaces, in the tactics file's order. **Its figures**, each
where it is not nothing - the key, `: `, `§G+` or `§R`, the figure and its suffix: the width whole
and `§W\n`, the attacker's and defender's damage and the speed as whole percentages and `%§W\n`.
**A side feels** the width always, the attacker's damage and the speed where it attacks, the
defender's damage where it defends.

**The row in a unit's panel** says, the attacker's side first:

    each unit: "@" and its country's tag, " £" and its first brigade's type - "unknown", the
          unknown counter, where the player knows it under intel level 3 or it has no brigade -
          " " and its name; joined by ", " and " " + AND + " " before the last
          (corrected 2026-11-13: this said "none for an army or above", reading the figure
          tested as the unit's level; it is the intel level - FINDINGS-combatview.md, section 15)
    a fleet's or an air unit's ships or wings each so, with the FLEET's name, the joins counted
          by units - so one fleet's ships run together, kept as read
    a line break, and what the side comes to by its first unit: UNIT_ATTACK_INFO_ARMY with its
          land units' brigades, units and men; _NAVY with capital ships, screens, transports and
          submarines; _AIR with fighters, bombers and rockets
    "\n" + COMB_TT_VS + "\n", and the defender's side the same

**A bombing's row adds what of the province it bombs** (since 2026-11-14,
`CBattleRowTooltip.Bombed`) [exe], by its first bomber's mission and nothing where the bombers are
gone: a space and each building the mission bombs that the province has built any of; then a space,
`BOMB_PROD_OFF`, a space and each good whose **stockpile** it bombs; then the same with
`BOMB_POOL_OFF` and each good whose **production** it bombs that the province can make. Each list is
joined with `, `, ` ` + `AND` + ` ` before its last and `.` after it, and left out where it is
empty. **The two keys stand each over the other's goods** - a logistical strike, which bombs
supplies and fuel where they lie, reads "Production of Supplies and Fuel.", and a strategic
bombing's resources are "Stockpiles of" - as the original writes them (`docs/enhanced.md`).
Neither of the two was looked at on screen: no bombing was brought up for it.

---

### An enhanced game's: a unit's figures and its damage

After BiceLib's `activateCombatUnitStats`; `CBattleStatsText`, `CBattleDamage`, `CTextTable`. A block at the end of
a land unit's whole tooltip - a unit on the front or a reserve of the defender's; not the bars', not a ship's or a
wing's - where the localisation has `OPENHOI_BATTLE_STATS_HEAD`. Every word is the mod's, twelve keys
`OPENHOI_BATTLE_STATS_...` and `OPENHOI_BATTLE_DAMAGE_...`.

**The figures.** A blank line, the heading, and four rows: soft attack, hard attack, defensiveness and toughness,
each **summed over the unit's brigades** from the brigades' own figures, nothing weighed by strength. Beside each,
` -> ` and what the battle makes of it - the two attacks by the attack percentage above, the other two by the
defend percentage, a landing's second scale in both - where that is not exactly one. So the block says what the
two percentages are percentages of. Piercing and armour are left out: the lines above have them, and no modifier
scales them. To one decimal.

**The damage**, once the unit has dealt or taken any: the heading and a table - strength and organisation, dealt
and taken, over **the battle** and over **the last hour** in which it dealt or took anything.

- A shot of a land battle that lands is noted against its target with who fired it and what it asked. When the
  hour's damage is settled, **what is really taken off the target** - a hundredth of what the hits came to and no
  more than it had (`landcombat.md`) - is its *taken*, and is **shared among those who fired, each by its part of
  what was asked**, as their *dealt*. Strength and organisation alike. *Ours*: BiceLib's counts the strength a shot
  asks at once, before the hundredth and the cap, and shares only the organisation; here both are what the target
  lost, so a unit's dealt is what its targets lost by it and the two sides add up.
- A unit's organisation is its brigades' average: taken is the sum over their number, and dealt the target's.
- **One battle**: a shot of another battle starts a unit's figures afresh, and they go when it leaves the battle or
  the battle ends - the tooltip is a battle's window's. BiceLib's keeps a unit's last battle until its next.
- **Land battles only**, as BiceLib's. What bombs take off a unit that has figures counts as taken, by nobody.

**Columns** are made of spaces measured in the tooltip's own font (`CTextTable`): a column starts two spaces after
the widest cell before it, reached by the number of spaces that brings the line nearest, so it lands within half a
space; the damage table's two figure columns end together. **Every width is measured on the line as it is drawn** -
cut into its runs of one colour, each measured whole, as the box advances along a line - and never by multiplying
one space's width: the typefaces here are placed to a fraction of a pixel, so one space measures 5 where twelve
together measure 51, and a table reckoned by single spaces came out up to nine pixels apart between its rows
(seen in play, 2026-11-26).

**The damage is display only** - nothing in the game reads it, and it is not in the state's hash - **and in an
enhanced save** (`savegame.md`, section 21), so a loaded game's battle says what it said, where BiceLib's starts
afresh.

## What is not built

- ~~**The naval mode's icons** of convoys sunk and raiders fought~~ - built (`mapmodes.md`, section 11), as the air
  map mode's of finished air battles and bombings are (`airmap.md`).
- ~~**The marker's first three hours.**~~ Nothing is missing: the original has no marker for a land battle's first
  three hours either (section 8). *Ours*, and it shows the same: the original makes the marker when it is first
  refreshed and leaves it standing between a battle's hours, where here it is worked out again whenever the map is.

## What is not established

- **What a click on the marker does** in the original.
- **How the original measures a name for cutting.** On the screenshot the name ends ten
  pixels short of its box; here it is cut where it and the dots still fit.
- **A turned flag's place** is right for the two the window has, a negative quarter turn, and
  for nothing else.
- Nothing here was watched in a running game. The screenshot agrees with it wherever the two
  can be compared.
