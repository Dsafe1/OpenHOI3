# Transport: land units carried by sea, the landing, and on screen

Land units carried by a fleet: what boards what and how much a fleet carries, getting off, a passenger's hours at sea,
what a sea battle and too little room do to it, the transport mission; a landing on a coast from the sea with its march,
its battle and the invasion mission; the warning a landing raises; and what the screen shows of all of it. Read out of
the original on 2026-10-15 (`reversing/findings/FINDINGS-transport.md`, sections A, B and C), and set against
three screenshots of the original's of a landing at Danzig (section B.9 there) and against the reference saves, which hold carrying
fleets. `confirmed` unless marked.

**The carrier as an air base, carrying by air, the paradrop and the air supply** were read in the same wave (section D
there) and are this piece's second half, section 13; what carrier protection asks of its province and what becomes of a
shot-down transport's paratroopers were read through on 2026-10-16.

**Built 2026-10-15**, sections 1 to 12 (`Sim/Transport`, `CTransportMission` and `CInvasionMission` in `Sim/Missions`,
`CLoadUnitCommand` and `CUnloadUnitCommand`; `CTransportText` and `CCarryButtons` in `Interface/Screens`; the panel's
`CLoadedUnitsEntry` and `CLoadedUnitRow`), tried with `--click` and set against the saves (section 14). It has
not been reviewed in play.

**Built 2026-10-16**, section 13 (`CCarriers` and `CAirTransport` in `Sim/Transport`, `CParadrop` and `CAirSupply` in
`Sim/Missions`, `CLoadOnPlaneCommand` and `CUnloadFromPlaneCommand`; the strip's plane buttons, the air window's four
rows and their order lines), tried with `OpenHOI3.SaveCheck airborne` (section 14). Nothing of it was looked at on screen.

Figures marked *define* are `common/defines.lua`'s and mod-able; *units file* are a unit type's; everything else is
the original's own and compiled in.

## 1. A passenger

A passenger is a land unit on a fleet's list of passengers. **It is tied to the fleet, never to a ship.** The list keeps
the order the units came aboard in; everything that walks it walks it in that order. The passenger knows the fleet it
is aboard. Only the unit itself is aboard: a corps or a headquarters boards alone, and the units under it stay where
they are.

## 2. What a fleet carries

    weight(unit)     = the sum of its own brigades' transport_weight, cut to a whole number     (units file)
    load(fleet)      = the sum of weight(p) over its passengers, each cut on its own
    capacity(fleet)  = the sum of its ships' transport_capability, cut to a whole number       (units file)

The key is **`transport_capability`** for ships as for transport planes; no `transport_capacity` key exists. Their
Finest Hour: `transport_ship` 40, `invasion_transport_ship` 60, `assault_ship` 60, every other ship none;
`infantry_brigade` 10, armour 20, cavalry, light armour and motorised 15, artillery 5, a headquarters 10. One transport
ship carries a division of three infantry brigades.

**A fleet may take a unit aboard** (`CanCarry`) when all of these hold:

- the two have one owner - an ally's fleet never carries;
- the fleet is in no battle, has no path and is not falling back;
- `weight(unit) + load(fleet) <= capacity(fleet)`.

## 3. Boarding

**By command** (the load button, section 12): a command naming the land unit **and the fleet**. It is refused where
either is gone or the fleet may not take the unit aboard. Otherwise, for a land unit not already aboard anything:

1. the unit goes on the fleet's list, at its end;
2. its dig-in is lost;
3. its order is dropped.

**Nothing asks where the unit or the fleet is**, and the unit is not moved: the command posted from the screen names
only a fleet in the unit's own port (section 12), and a command naming a distant fleet leaves the unit standing where
it is until the fleet next enters a province. Boarding is at once and costs nothing - no time, no organisation, no
supplies. **Its path is kept**; only its order goes.

**By marching.** A land unit whose next province is **sea** may stand there where a fleet lies there that may take it
aboard - so a route may end in a sea province with a fleet of the unit's own country in it. On arriving, the unit
boards **the first fleet in the sea province, in the order the province lists its units, that may take it aboard**: it
is on the fleet's list, the province it left becomes its previous one, it is in the sea province, and its path is
gone. With no such fleet by then, its move is cancelled.

## 4. Getting off

1. **By command** (the unload button): refused unless the unit is aboard a fleet and **stands in a land province** -
   the fleet being in port. It gets off where it is, with nothing brought.
2. **A fleet ending its path in a land province** - a port - puts every passenger off there, each bringing its days of
   supplies (below). A port the fleet passes through on its way keeps them aboard.
3. **A landing** (section 8): the passenger gets off on arrival in the coastal province, bringing its days of supplies.
4. **The transport mission's unloading** (section 7): every passenger off where the fleet is, with nothing brought.

Getting off, in this order: the passenger is no longer aboard; its dig-in is lost; where it brings supplies,
`SUPPLYPOOL_DAYS` (15, *define*) days of what it uses are taken from the pool of **its previous province where that is
land**, or else from its country's stockpile, and put into **the province it stands in at that moment**; it leaves the
fleet's list; and it looks for a battle in the province it stands in. **On arrival in port and on a landing that
province is still the sea it is leaving**: the supplies go into the sea's pool. The unit then enters the land province
**as any land unit entering land does, bringing its days again** (`supply.md`, section 14) - out of its previous
province's pool where that is land, otherwise out of the stockpile. On a landing the previous province is cleared
before the first and is the sea for the second, so **the stockpile pays both**; arriving in port, the land it last
walked out of pays both.

## 5. At sea

**Where it is.** Each time the fleet enters a province, every passenger enters it with the fleet, leaving any battle it
was in. Its previous province does not change: the Division de Tonkin keeps Bac Ninh as its previous province through
every save in which it sails. **The fleet sails at its own speed**; nothing about what it carries changes it.

**Each hour**, a passenger:

- regains no organisation while its fleet is at sea; in port it regains as any unit does (the saves: 29.995 for
  seventeen hours);
- starts no battle while its fleet has a path;
- going nowhere, fights no one and is fought by no one, takes no province and holds none.

**Each day**, a passenger digs in no further; is not reinforced; still loses strength to troop rotation (the saves:
29.984 to 29.982 across a midnight); suffers the attrition of the province it is in; and draws its supplies where its
fleet's base is (`supply.md`). It burns no fuel, going nowhere.

**What a country knows** of the province counts it as any unit there: 2 at sea, 9 in port (`intelligence.md`).

**The fleet**, while it carries:

- **may not be ordered to move while any passenger has a path**;
- may be given any order; only the invasion's asks what it carries;
- under a transport order, starts no sea battle, and is fought like any fleet when an enemy finds it
  (`reversing/findings/FINDINGS-liveread.md`, section 126 - until 2026-11-18 ours held it the defender of none);
- is found without a roll by an enemy fleet while any passenger is moving or fighting (`navalcombat.md`).

## 6. Too much aboard, a sea battle, and the fleet's end

**Too much aboard.** While `load > capacity`, the passengers are walked in order:

- one standing in a land province - the fleet in port - is put off where it is, bringing nothing, and the walk goes on;
- one at sea **loses its first brigade**, which is gone from the game; a unit with none left is removed; and the load is
  measured again.

Nothing is said to the player. It is asked of every fleet **every hour**, every day, **after every round of a sea
battle**, and of the fleet a ship has been split or moved out of.

**A sea battle.** The passengers take no part in it and are no ship's target. But each round, the damage the fleet's
**transports** (`is_transport`) took is laid on every brigade aboard, with `n` the number of brigades aboard:

    organisation lost by each brigade  = damage * 1000 / (n * 2000)
    strength lost by each brigade      = damage * 1000 / (n * 1000)

settled at once, with no test of whether a passenger breaks. The sunk ships then go, the capacity falls, and the test
above takes the brigades that no longer fit **at sea** - so **a fleet sunk whole takes every passenger with it**, a
brigade at a time.

**The fleet's end.** A fleet disbanded, or removed in a battle, takes its passengers with it. A fleet merged into
another fleet hands its passengers to that fleet, at the end of its list, and the new load is not asked about until the
next test. A fleet split hands none to the new one, and its own test is asked at once.

## 7. The transport mission

"Transport land units on board the fleet to the friendly land province, then return to base." (`ORDER_TRANSPORT_DESC`)

**It may be given** where the fleet is in no battle, the province has a naval base, it is in the fleet's range, and the
fleet may lie there (an open port, `fleetsandair.md`). **It does not ask what the fleet carries.**

**Each hour**, in this order:

    no base, or the naval order's own test says it is done        -> the order ends
    in a battle, or told to go home                                -> nothing
    may no longer lie in the province                              -> home (unless at its base)
    has a path                                                     -> nothing
    at its base, carrying                                          -> a path to the province, the safe way
    in the province, carrying                                      -> every passenger off (section 4, way 4)
    in the province, empty                                         -> home
    anywhere else                                                  -> a path to the province, the safe way

So it waits at its base for passengers, sails, puts them off, and goes home. Arriving at the end of its path in the
port already puts them off (section 4, way 2), so its next hour is the way home. **The French fleet** of the
reference saves - three transports, the Division de Tonkin aboard, from Saigon to Luganville - is in "has a path"
every hour of the five saves, sailing 7.5 an hour (`transport_ship`'s 15 times `NAVAL_SPEED_MODIFIER` 0.5, *units file*
and *define*).

## 8. The landing

A passenger at sea may be sent to a **land province beside the sea province it is in** - by the player (section 12) or
by the invasion mission (section 10). The rules of the route are the land march's (`movement.md`, section 3): an edge
from the sea, not impassable; a crossing an enemy blocks is closed; and the unit may stand there - its own ground, a
friend's, nobody's, or ground of a country it is at war with, never a neutral's. **Nothing asks for a port**, and
nothing refuses a province where a battle goes on. The path may run on inland after the coast.

**The leg from the sea to the coast:**

    distance = AMPHIBIOUS_INVADE_MOVEMENT_COST                              24       (define)
    speed    = AMPHIBIOUS_INVADE_SPEED_BASE + the fleet's invasion speed    0.5 +    (define)

and **nothing else**: not the terrain, the weather, fuel, a river, the land speed define or a retreat. The fleet's
invasion speed is the **mean of `amphibious_invasion_speed` over the ships that have one** (*units file*:
`invasion_transport_ship` 0.50, `assault_ship` 0.75; `amphibious_invasion_technology` adds 0.10 a level to the three
transport types), so escorts do not lower it. Plain transports land a unit in **48 hours**; assault ships with that
technology at level 2 in 17. The same march puts a unit ashore on its own coast outside a port.

**While it marches it is still aboard**: on the fleet's list, in the sea province, moving with the fleet. Each hour, before
its step:

- **A unit that cannot fight** - its front brigades averaging under 0.1 strength or 1.0 organisation - landing on ground
  an enemy holds: where another land unit of its country that can fight has a path **ending** in that province, it waits
  the hour; otherwise its move is cancelled.
- A unit given no supplies at its last draw, landing on ground an enemy holds with no battle of its own there: its move
  is cancelled (`movement.md`).
- **The fleet moved**: where the sea it is in now has no edge to the coast, the move is cancelled; where it has, the
  march goes on and keeps its progress.
- While the fleet has a path, the passenger starts no battle.

**Off the ship**, on arrival only - its progress has reached 24, it is in no battle, and its order's hour has come: where
it may still stand in the coast, it gets off bringing its days of supplies (section 4), its previous province is the sea,
it enters the coast and looks for a battle there - which takes the coast if nobody holds it against it (`landcombat.md`,
section 1). If it may no longer stand there - peace was made, say - the move is cancelled and it stays aboard. **In a
battle its progress grows to 24 and stops there**, so it never comes ashore while it fights: a won landing still walks
up to the end of its 24 and lands the next hour it is free.

## 9. The landing's battle

**It starts as any attack does** (`landcombat.md`, section 1): a landing unit with an enemy land unit in the coastal
province, no battle of its own there yet, and supplies at its last draw starts one as the attacker, in the coastal
province. **A landing on an empty enemy coast fights no battle** - the unit walks its 24 and takes the province on
arrival. A landing into a province where a battle already goes on joins it on the attackers' side.

**The progress** is what the battle and the tooltip show:

    progress = movement progress * 1000 / 24, cut                                  thousandths

**The two scales.** Each battle hour, after every unit's figures are reset, for every unit of a side that has a path, is
a land unit, stands in the sea, and whose next province is land held by an enemy of its owner - **attacker or not**:

    attack scale  = ATTACK_LOW + (ATTACK_HIGH - ATTACK_LOW) * progress / 1000                (defines: 0.2, 1.0)
    low           = DEFEND_LOW + the carrying fleet's invasion defence                       (define: 1.5)
    defence scale = low + (DEFEND_HIGH - low) * progress / 1000                              (define: 1.0)

each product **cut toward nought** - the defence's falls, so 1500 - 20.5 is 1480. The fleet's invasion defence is the
**plain sum of `amphibious_invasion_defence` over its ships** (*units file*: `invasion_transport_ship` 0.10,
`assault_ship` 0.25; `amphibious_assault_units` adds 0.10 a level to the three transport types). Plain transports and no
technology: attack 20% to 100% and defence 150% to 100% over the 48 hours. The scales act where a unit fires and is fired
on (`landcombat.md`, section 5): **its shots are its attack figure times the attack scale**, and **its defence is its
defence figure times the defence scale**, each product cut.

**Measured** on the screenshots of the original: progress 4.10% (1.0 of 24), attack 23.20%, defence 148.00%; progress
39.50% (9.5 of 24), attack 51.60%, defence 130.30%; at 100.00% both back at 1000. Every figure is the arithmetic above.

**The amphibious penalty** (`BM_AMPH_PENALTY`), an attacker's alone. A unit counts as landing when it stands in the sea -
unless it is the rebels' - **or attacks over a strait** (an edge of the crossing kind) from land:

    v = AMPHIBIOUS_LANDING_PENALTY + the unit's amphibious attack adjuster + its leaders' amph_attack trait     (define -0.7)
    where v < 0, it stands in the sea and is aboard a fleet:
        s = 1000 + for each of the unit's brigades, the fleet's extra_amphibious_defence for its kind            (always 0)
        r = min(s * the fleet's invasion defence / 1000, LANDING_PENALTY_DECREASE)                              (define 3.5)
        v = v * (1010 - r * 1000 / LANDING_PENALTY_DECREASE) / 1000
    v = max(v, -989)
    where v < 0: the modifier

So with no invasion defence the penalty is **made worse by a hundredth** - -70% is -70.70%, as the screenshots show - and
at a defence of 3.5 it is cut to a hundredth. **`s` is always 1000**: a ship type's `extra_amphibious_defence` (*units
file*: `assault_ship` gives 0.25 for the armoured and mechanised brigades) is meant to raise a landing brigade's share of
the defence, but the original asks it of the one definition a fleet's ships are summed into, which it makes with an empty
table and to which the summing copies no ship's (`reversing/findings/FINDINGS-transport.md`, B.4). The table is kept on
each type and asked so (`CLanding.FleetExtraDefence`), and it does nothing. The adjuster is the mean over the unit's brigades (`amphibious`, *units
file*: engineers 0.4, marines 0.5). A strait assault from land gets the penalty but not the cut and not the scales.

**An enhanced game's fix `amphibious_defence`** (2026-11-27) mends both: the fleet's table is **its ships' own added
up**, kind by kind, as its invasion defence is its ships' added up - so `s` is 1000 and, for each of the unit's
brigades, what the fleet's ships together give that kind, the cap `LANDING_PENALTY_DECREASE` holding `r` as before;
and **the factor is held at one**, `min(1000, 1010 - r x 1000 / cap)`, so a landing with no invasion defence has the
define's penalty exactly and the whole defence still leaves a hundredth of it. Three infantry brigades off one ship
that gives infantry 0.25, at an invasion defence of 1: `s` 1750, `r` 1.75, the penalty cut by a half, -35.7% where the
original has -50.7%.

**The forts it meets** are the coast's `COASTAL_FORT_LEVEL`, and **shore bombardment** counts the enemy fleets in the sea
beside the battle that have **no path** (`landcombat.md`).

**Lost**, a landing unit falls back to the province it is in - the sea - which leaves it where it is: its march is
cancelled, its progress gone, **still aboard**, not falling back. **Won**, it lands when its 24 is walked.

## 10. The invasion mission

"Land troops, carried on the fleet, in the selected province" (`ORDER_INVADE_DESC`). It keeps one figure of its own, the
sea province the landing is made from.

**It may be given** where the unit is a fleet, in no battle, the province is land, in the fleet's range, the fleet
carries something, and **its first passenger may stand in the province**. It does not ask for a war or a coast.

**When it is given** it chooses the sea: of the province's neighbours in the order of its edges - impassable ones
included - each that is sea, on the map, and that the fleet may go to, it takes the first, and then any whose distance by
sea from the province's own named sea (`positions.txt`'s port sea; nought where it names none) is **strictly less** than
the chosen one's. **A distance of nought counts as nothing chosen yet**, so the next candidate always displaces it. With
none, the order does nothing for as long as it lasts.

**Each hour:**

    the fleet has no base                                   -> it ends
    in a battle                                             -> nothing
    the naval order's own test says it is done              -> it ends
    told to go home                                         -> nothing (sailing home)
    has a path                                              -> nothing
    not at the landing sea: carrying                        -> a path there, the safe way, where one was chosen
                            empty                           -> home, and it ends
    at the landing sea, carrying:
        flanks  = each neighbour of the province over an edge that is not impassable, that is land, held by an enemy of
                  the fleet's country, beside the landing sea, with no enemy land unit in it
        heading = how many passengers' next province is the target
        for each passenger in order, in no battle:
            cannot fight: set aside if it has no path, else passed over
            may not go to the target                        -> home, and it ends
            has a path: passed over
            e = the enemy land units in the target
            flanks there, heading < e + 6 and heading < 2e  -> a path to the next flank in turn
            else                                            -> a path to the target; heading += its brigades
        each one set aside: to the target, or else the first flank, where a land unit of its country that can fight
            has a path ending there
        one set aside left with nowhere                     -> home, and it ends
    at the landing sea, empty:
        not at war with the target's holder                 -> home, and it ends
        a land battle in the target                         -> nothing (it waits)
        otherwise                                           -> home; the order stays on, in port

It is the one mission that never sends a worn fleet home (`navalmissions.md`, section 1). A beaten passenger is back
aboard with no path, so the next hour sends it again.

## 11. The warning: `INVADEDUS`

A list of landings on the player's ground, kept by country. **Whenever a unit is given a path**, it goes on the list
under its owner where the player's country is its owner's enemy, it is a land unit in the sea whose next province is
land, and **the player holds** that province. A country new to the list, or a unit new under it, marks the country as
having something new; **a unit already listed and given a path again clears the mark**.

**The first thing each hour does**: each country's units that are gone or no longer qualify are struck off; where some
remain and the country is marked, the mark is cleared and the player is told `INVADEDUS` - "$ENEMY$ is storming our
beach in $PROV$." - with `ENEMY` the invader and `PROV` the provinces the units still listed are landing on, each once,
in list order, written as a list ("A, B and C"). So the warning comes **the hour after the landing is ordered**, up to 48
hours before anyone is ashore. A change of player empties the list.

## 12. On screen

**The unit panel's buttons.** `load_button` and `unload_button` share a place, as do `load_plane_button` and
`unload_plane_button`; one of each pair shows.

- A land unit aboard a fleet: `unload_button`, pressable where the province it stands in is land - "Unload this unit from
  the fleet" (`UW_UNLOAD_VALID`), or "You can only unload if your fleet is located in a port." (`UW_UNLOAD_NOT_VALID`).
- A land unit aboard nothing: `load_button`, pressable where a fleet is found for it - "Click to load this unit onto a
  transport fleet." (`UW_LOAD_IS_VALID`), or "There is no fleet here that this unit can be loaded onto."
  (`UW_LOAD_NO_FLEET_FOUND`).
- Anything else: neither.
- **The fleet found** for a land unit in a land province with a naval base built, in no battle: **the first fleet lying
  in the port that may take it aboard**. The player is never offered a choice.
- Pressing posts the load command naming that fleet, or the unload command.
- `disbandbutton` is greyed for a passenger; `newunitbutton` for a passenger and for a fleet carrying anything.

**A fleet's passengers** (`sup_loaded`), on every fleet's panel and every air unit's:

- the header `LOAD_CAPACITY_LABEL` "Can carry $NUM$ weight." - with nothing aboard `NUM` is the capacity, otherwise what
  is left and then the capacity in brackets: the French fleet reads **"Can carry 90 (120) weight."**, and a warship's
  "Can carry 0 weight." (seen in the original, 2026-10-15);
- a row (`loadedunit_entry`) for each passenger, then for a fleet a row for every air unit aboard its carriers: the
  first brigade's counter where the player's intelligence of it is 3 or more, the size by the unit's level, its name,
  its order, its objective, and its organisation and strength bars - no strength figure. Pressing a row selects that
  unit and centres the map on it;
- `select_land` - "Select all attached land units." (`UW_SELECT_LAND`, shortcut `.`) - pressable where anything is
  aboard, on a fleet's panel or an air unit's; `select_air` - "Select all CAG's attached to this fleet." (`UW_SELECT_AIR`,
  shortcut `,`) - shown on a fleet's panel alone and pressable where any of its carriers (a ship with a `carrier_size`
  above nought, *units file*) has aircraft aboard.
- **A press** (read 2026-11-14, `reversing/findings/FINDINGS-transport.md`, section C.2): **nothing at all unless the
  carrier is a fleet** - so an air unit's `select_land`, pressable over its paratroopers, does nothing (oddity 21).
  For a fleet, everything selected - the fleet among it - is let go at the end of the frame's panel pass, and
  `select_land` selects every passenger in the order they boarded, `select_air` every air unit aboard each carrier, ship
  by ship in the fleet's order and each carrier's in its own. Each goes through the screen's own call to select, which
  asks neither owner nor kind; the selection then shows the single panel for one unit and the panel of several for more.
  **Nothing else happens**: the map is not moved, no province is chosen, nothing is posted; each unit selected sounds as
  any selection does (`sound.md`, section 7). Built so, and tried with `--click` (section 14).
- **The list is made when the fleet is selected and not again while it stays selected**: a unit that gets off stays in
  it until the fleet is selected anew (seen in the original, 2026-10-15). An air unit's list is made again when its count
  changes.

**Several units selected** (`multi_unitpanel`), where the first selected is a land unit: `load_multiunit`,
`load_on_air_multiunit` and `unmount_multiunit`.

- `load_multiunit` is pressable where the selection - two or more land units, all in one land province with a naval
  base built, none in a battle - fits **all together** on one fleet in the port, which must also take **the last
  selected unit** aboard. Pressing posts one load command per unit, all naming that fleet. Its tooltip: `MUL_LOAD_ONTO`
  and the fleet's name in yellow, "Load these units onto *name*", or `NO_MULTILOAD` "There is no possible fleet to load
  these units onto."; its delayed half `TOTAL_LOAD_WILL_BE` and the summed weight in yellow, "The units weigh a total of
  30".
- `unmount_multiunit` is pressable where any selected unit is aboard anything, and posts an unload command for each -
  **asking nothing of where the fleet is**; the commands refuse the ones not in port. "Unmount $NUM$ units from air and
  naval transports." (`UNMOUNT_MULTILOAD`), or `UNMOUNT_MULTILOAD_NONE`.
- `load_on_air_multiunit` is the same with air units at their base and the planes' test (section 13.3).

**The map.**

- A right click is on a province, never a counter: on a fleet's counter in port it is a click on the port.
- A land unit is sent to a sea province where it may stand - a fleet of its own there with room (section 3).
- A passenger is sent only to a land province beside its sea (section 8). It is not refused for want of supplies.
- **A fleet carrying anything**, right-clicked on a coast that is not reached by sea, **opens the orders window** where
  its first passenger may stand there, shift is not held and it is in no battle; otherwise the refusal.
- The province's tooltip has **no line for carrying or landing**.
- **Counters**: a passenger in port has none; one at sea has its own, among the moving counters while its fleet has a
  path and among the still ones otherwise. Nothing marks a landing on the map; its arrow is the ordinary path arrow, in
  the texture `unitInvasionArrow`.

**The orders window** lists `Transport` and `Invasion` always, each greyed where out of range or where its test says no
(sections 7 and 10), with `ORDER_TRANSPORT_DESC` and `ORDER_INVADE_DESC` as their tooltips. Where the fleet may lie in
the province, its holder is not at war with the fleet's country, a naval base is built there and a rebase may be given,
the window opens on **Transport if the fleet carries anything**, else Rebase; otherwise on Move.

**A landing unit's tooltip in a battle**, after the leader's line, while it is landing:

    This unit is currently in amphibious transports. Its offensive capabilities are reduced
    and its defence improved by the transports' armour.          (AMPH_INVASION_COMBAT_PROGRESS_DESC, while < 1000, then a blank line)
    Amphibious invasion progress: §R4.10§!%                      (AMPH_INVASION_PROGRESS; §G from 1000)
    Invasion total attack ability: §R23.20§!%                     (AMPH_INVASION_ATTACK_SCALE, where not 1000; §G above)
    Invasion total defence ability: §G148.00§!%                   (AMPH_INVASION_DEFEND_SCALE, where not 1000)
    (a blank line)

and the "Attack Modifier" and "Defend Modifier" lines that follow carry the scale in their first figure: 33.10% times
23.2% is 7.60%.

## 13. The carrier and carrying by air

Read with the rest (`reversing/findings/FINDINGS-transport.md`, section D), and carrier protection's two tests and what
the removal of a transport plane does with its passengers read through on 2026-10-16 (sections D.1 and D.7 there).

### 13.1 A carrier as an air base

**An air group aboard** is on a ship's list, and knows the ship. Its base is that ship, and **its base province is the
province the ship's fleet is in** - so its range is measured from wherever the carrier is. It is **at its base** only
where it stands in that province **and the fleet is in no battle and no bombing**; every test of "at its base" fails
while the carrier fights. A fleet entering another province takes with it each of its carriers' groups that has no path,
is in no battle and no bombing, and stands in the province the fleet left; a group out on a mission stays where it is
and comes home to wherever the carrier has gone. A group aboard is never crowded (`organisation.md`), and draws on the
carrier's own base (`supply.md`).

**Rebase to carrier** (`REBASE_TO_CAG_MISSION`) may be given for a province where:

- every wing of the unit is a carrier air group (`is_cag`, *units file*), and
- a fleet **of the unit's own country** in the province has a ship whose room holds the unit:

      the wings of the air units on the ship's list + the unit's wings  <=  carrier_size / 1000, cut   (units file:
                                                                             carrier 2, escort_carrier 1)

The province's fleets are asked in the order it lists its units - a port's fleets are among them - and in each fleet its
ships in order; the first fit is taken. **Nothing asks about range, war, an ally's carrier or whether the carrier is at
sea.**

**Given**, the order keeps that fleet, and the unit's base becomes the first ship of it with room **at once**: the unit
leaves the ship it was on and goes on the new ship's list, and **it is hung under the fleet in the order of battle** -
out from under the commander it had, the last of the fleet's units (one already under the fleet goes to the end), the
AI agent above where it was and the one above the fleet told to sort their units again, the second to regroup its air.
Then it flies:

- **To the carrier**: it is given the way to the province the fleet is in now, and the light at its hour of arrival is
  judged as a strike judges it (`bombing.md`, section 2) - by day it will not arrive at night, by night not by day; with
  the wrong light, a unit standing in its base's province has its way cancelled and any other is told to return to base.
- **Each hour**: the fleet gone, or the unit with no base, the order is done. In a battle, a bombing or with a path it
  goes on. **At its base, it is done.** Otherwise it flies to the carrier again. No range is measured on the way, and
  nothing halves its organisation on landing.

A group based on a carrier **with no order is given carrier protection** for the province it is in, the hour after.
**Rebased to an airfield** it leaves the carrier, **and with it its place under the fleet** - to the top of the order of
battle, not back to the commander it had - and lands with half its organisation, as any air unit (`fleetsandair.md`,
section 5).

**Its carrier sunk**, a group based on it is let go: off the ship, no base, no order, and **given the nearest air base
of its country in a straight line**, where it stands - **and it stays under the fleet in the order of battle**. **Its
fleet removed whole**, each carrier's groups the same, and each taken out from under the fleet.

**In the order of battle** (`reversing/findings/FINDINGS-transport.md`, section D.1, read 2026-11-14). An air group aboard a carrier
is one of its fleet's units: an order of battle's or a save's `air` block inside a `ship` hangs the group under the ship's
fleet as it is met, and the rebase above does the same. What follows is the original's, and is so here:

- **The unit panel's units above** a group are the fleet and what is above the fleet (`unitpanel.md`, section 2), and a
  click on the fleet's row selects the fleet. **The fleet's list of the units under it leaves out a group based on a
  carrier**, its list of what it carries showing it (`unitpanel.md`, section 8).
- **The order of battle browser** shows the groups under their fleet, and the outliner under the fleet in the naval
  units; a click on a group's row selects the group.
- **The attach and detach buttons**: a group of carrier air groups alone with a unit above it shows no detach button,
  and with none may not be attached; nothing but the rebase puts an air unit under a fleet, a fleet taking no air unit by
  the attach command.
- **Whatever walks up the chain walks through the fleet**: the radio reach - a fleet passes all of its command through, so
  a group is out of range where its fleet is - a leader's traits coming down the chain at half a step, the experience a
  battle gives the leaders above a unit, and the AI agent a unit answers to.
- **`split_troops` keeps a fleet with a group under it** (`effects.md`).
- **A save** writes a group based on a carrier inside its ship alone, and one under a fleet whose carrier sank as one of
  the fleet's units.
- **A fleet merged into another**: its ships go over with their groups aboard, and the fleet's removal leaves each of its
  units with nobody above it - a group still aboard then belongs to no unit. **A removed fleet's units are left with
  nobody above them** whatever they are; what the removal of a headquarters does with the land units under it here is
  the worklist's *Loose ends*.

### 13.2 Carrier protection

`CAG_DUTY` - "Protect the carrier on which it is based from air attack, and bomb any fleets the carrier is fighting."

- **Its hour**: no base, it is done. The unit's **plain average organisation** - the wings' summed over their count, not
  a share of a maximum - under `CAG_DUTY_MINIMUM_ORG_LIMIT` (0.2, *define*): told to return to base. In no battle, no
  bombing and with no path: away from its base's province, told to return to base; and **the order's province becomes
  its base's** - where the carrier is. It is never done by itself.
- **It fights in the air** - is taken into an air battle, and starts or joins one - while the same average is at least
  `CAG_DUTY_MINIMUM_ORG_LIMIT` and **a battle is going on in the order's province**, of any kind but a bombing. Fit to
  fly as any air order is (`aircombat.md`, section 3).
- **It bombs ships in the order's province alone** - its carrier's. Its bombing is a naval strike's (`bombing.md`):
  the fleets it may bomb, sighted, with `CAG_SHIP_ATTACK_STR_BONUS` and `_ORG_BONUS` (*define*) on a fleet in a battle.
- **Into a sea battle**: each hour **a fleet in a battle or a bombing** first sends each air unit aboard its carriers
  that is under carrier protection, stands in the fleet's province, is in no battle and no bombing, has no path and can
  fight (some wing with the strength and organisation not to count as spent) to look for a fight where it stands - an
  air battle, then a bombing of the enemy's fleets.

### 13.3 Carrying by air

A land unit aboard an air unit is on its list of passengers, in the order they came aboard, and knows the air unit. **It
is tied to the air unit**, as a fleet's passenger to its fleet.

**May board** (`CLoadOnPlaneCommand`, the panel's `load_plane_button`), all of these:

1. the two have one owner;
2. the air unit is in no battle, has no path, is not falling back, and is **at its base**;
3. the land unit's attack delay is not running;
4. the base province has an air base **working** at level 1 or more;
5. **every brigade** has `can_paradrop` (*units file*: paratroopers, rangers, Gurkhas) and organisation x 1000 / its most
   - a most of nought counting as -1 - at least `PARATROOP_MISSION_ORG_REQ` (0.95, *define*);
6. `weight(unit) + load(air unit) <= capacity(air unit)`, section 2's figures - `transport_capability` 15 on a transport
   plane, `transport_weight` 5 on a paratrooper brigade (*units file*), so a transport wing carries three.

**Where the land unit stands is not asked**: the panel offers only air units based in the province the unit is in.

**Boarding** puts it on the list, leaves it where it is, and gives it no order. **Aboard**, it goes wherever the air unit
goes; it is never fought and starts no battle; it is not reinforced; and it draws on the province of the air unit's
base.

**Getting off** (`CUnloadFromPlaneCommand`, `unload_plane_button`) only where it stands on land and its air unit is at
its base - "You can only unload at an airbase." Off the list, no supplies moved, and **it looks for a battle where it
stands**.

**An enhanced game** with the fix `plane_passenger_march` (2026-11-27) gets a passenger off by itself when it is given a
way - a move, or an order that routes it - where the two tests above hold, and refuses it the way where they do not: aboard
an air unit in the air it cannot be sent. The original sends it anyway and it walks, still aboard (*Ours*, below;
`moving.md`, section 9.1).

**An air unit removed** - shot down, disbanded - **takes its passengers with it**; a land unit removed leaves its air
unit's list.

### 13.4 The paradrop

`PARADROP_MISSION` - "Airborne Assault". **May be given for any land province** - nothing else is asked, not range, not
war, not that anything is aboard; the window offers it only to an air unit carrying something, over land, and **moves
its start to now and `PARADROP_MIN_TIME` (24, *define*) hours** where it is earlier - every frame while its row is ticked,
as the window's update does, and as it is accepted, so the start follows the clock while the window is open
(`fleetsandair.md`, section 4).

**Taking off**, as it is given and every hour it is not over its target:

1. No fuel, or not fit to fly: told to return to base, and nothing more this hour. (Before its start hour a unit is never
   fit to fly, so it is told so every hour it waits.)
2. Before its start hour: nothing.
3. With `hostile` meaning the target's holder is at war with the air unit's country: **it waits** while `hostile` and
   any passenger's attack delay is running, or while any passenger brigade is under `PARATROOP_MISSION_ORG_REQ` of its
   most.
4. Otherwise it is given the way to the target, the light at its arrival judged as for a strike.

**Each hour**: no base, done. In a battle, a bombing or with a path: going on. At its base with nothing aboard: done.
Before its start hour: going on. Not over the target: take off again. **Over the target, the drop:**

- `hostile` is now the air unit's country at war with **the holder of the province it is over**. For each passenger in
  order: marked **dropped** where `hostile`; given an attack delay where `hostile`; off the list, **bringing
  `PARASUPPLYPOOL_DAYS` (7, *define*) days** of what it uses into the province - out of the pool of the land province it
  last walked out of, or the stockpile; and **it looks for a battle there** - joining or starting one, or **taking an
  undefended enemy province at once**.
- Every wing of the air unit has its organisation multiplied by `PARATROOP_DROP_ORG_MULT_PLANE` (0.01, *define*), cut.
- It is told to return to base.
- The first time only - marked first, told or not - where the target's holder **after the drop** is the player and the
  air unit's country is at war with the player: `PARATROOPEDUS`, "$ENEMY$ is dropping paratroopers / in $PROV$."

**A dropped unit**:

- its attack delay is `PARATROOP_MISSION_DELAY` (168, *define*) where an ordinary one's is `UNIT_ATTACK_DELAY`, the roll
  and the rest as before (`landcombat.md`);
- attacking, it has **`BM_PARATROOP_PENALTY`**, `PARADROP_PENALTY` (-0.5, *define*) and its leader's
  `paradrop_mission` trait, where below nought - and **no landing penalty** then;
- it has **no `BM_LACK_OF_SUPPLIES`**;
- **the battle's width counts every dropped attacker after the first not at all** - all of them one direction;
- **the first hour it is in no battle** it loses the mark and every brigade's organisation is multiplied by
  `PARATROOP_DROP_ORG_MULT` (0.66, *define*), cut - dropped onto an empty province, the same hour.

A drop on ground nobody is at war with marks nothing, but brings the days and looks for a battle all the same.

### 13.5 The air supply

`TRANS_SUP_MISSION` - "Air Supply". **May be given** for land, to an air unit with a `transport_capability`. Its hour is
the **bombing frame's** (`bombing.md`, section 3), with its own pick and its own cargo:

- **Its list** keeps the provinces whose holder counts the unit's country a friend, military access counting.
- **The pick**: over the list in order, the first allowed is taken whatever it lacks, and **every later one replaces it
  where `(drawn - throughput)` of fuel and of supplies, today, added, is above nought** - so the last short province of
  the list wins.
- **Each hour**, before the frame: standing in its target, **the target's pool gains the cargo**, and cargo and target
  are cleared. At its base with a target, an empty cargo is loaded:

      cap      = AIR_SUPPLY_FACTOR x transport_capability / 1000                   (0.5, define)
      supplies = min(max(target.drawn - target.throughput, 0), cap)     today
      base.drawn += supplies
      supplies = min(supplies, base.pool);  base.pool -= supplies
      and fuel the same, after

  At its base with a target and nothing aboard it waits where it is. Otherwise the frame runs, and the air orders' own
  hourly test answers.

### 13.6 On screen

- **The unit panel**: `load_plane_button` and `unload_plane_button` share a place (section 12). A land unit aboard an air
  unit: `unload_plane_button`, pressable where that air unit is at its base - "Unload from transport planes."
  (`UW_UNLOAD_PLANE_BASE_OK`) or "You can only unload at an airbase." (`UW_UNLOAD_PLANE_BASE_ONLY`). A land unit aboard
  nothing: `load_plane_button`, pressable where an air unit is found - "Load this unit onto transport planes."
  (`UW_LOAD_PLANE`), or, found none, "Our paratroops have too low organization. They need to be at 95% of their maximum."
  (`UW_LOAD_PLANE_FAIL_LOW_ORG`) where a brigade is under `PARATROOP_MISSION_ORG_REQ`, else "There are no transport planes
  here that this unit can be loaded onto." (`UW_LOAD_PLANE_FAIL`). Anything else - a fleet's passenger among them -
  neither. **The air unit found**: for a land unit in a land province with an air base built, in no battle, the first
  air unit based in the province, at its base, that may take it aboard.
- **An air unit's list of passengers** is section 12's, and is **made again when its count changes**.
- **A passenger aboard an air unit** has its own counter among the air units'.
- **The orders window** lists for an air unit, in the original's order: carrier protection first, where the unit is
  based on a carrier in this province, then air superiority and the intercept, for a unit with an air attack; **the
  paradrop**, for a unit carrying anything, over land; **rebase to carrier**, where a ship in the province carries
  aircraft; the air supply, for a unit with a capability, over land; and the rest as before (`fleetsandair.md`).
- **The unit panel's order line**: `PARADROP_MISSION_SHORT_DESC` "Airborne Assault ($TYPE$}" - TYPE "Resting" while unfit
  or in its base's province, "Returning", "Flying" in no battle and no bombing, "Fighting"; `REBASE_TO_CAG_MISSION_SHORT_DESC`
  "Rebase to Carrier ($TYPE$)" - "Returning", "Resting" in its base's province, "Flying", "Fighting";
  `TRANS_SUP_MISSION_SHORT_DESC` "Air Supply ($TYPE$)" - "Returning", "Waiting" at its base with no path, "Flying".
  Each row's tooltip is the original's table's description: `PARADROP_MISSION_DESC`, `REBASE_TO_CAG_MISSION_DESC`,
  `TRANS_SUP_MISSION_DESC`, `CAG_DUTY_DESC`.

## 14. Saves and checks

A fleet's block in a save holds each passenger as a block of its own under its level's key - `division` for a division -
with its `location` the fleet's; a passenger with no location is dropped. A loaded passenger's dig-in is lost as it
boards. The AI's `staging_province` and `target` on a land unit are its own, and the second is kept aboard.

**Against the saves.** `OpenHOI3.SaveCheck march` moves every fleet of the earlier save forward and sets it beside the
later; a fleet's passengers now come aboard as the save has them and go with it. On 2026-10-15 the French fleet carrying
the Division de Tonkin gives, from `Germany1936_01_07_22` to `_01_08_02`, from `_02` to `_03` and from `_03` to `_07`, a
passenger standing where its fleet does 1 of 1 and where the hours took it - province and previous province - 1 of 1.

**A trial**, since no save has a landing: `OpenHOI3.SaveCheck landing <install> [country] [enemy] [hand|mission][-defended]
[days] [seed]` puts a division aboard the country's carrying fleet nearest the enemy's coast and lands it, by hand or by the
invasion mission, telling each hour of it. On 2026-10-15, France against Siam: the Saigon fleet sails to the Gulf of
Thailand in 15 hours, Siam is told the next hour, and the Division de Cochinchine-Cambodge is ashore in Trat 48 hours
after its order and takes it, the stockpile paying 31 supplies into the sea and 31 into Trat; by the mission the fleet
lands from the Gulf of Thailand, the last sea in Trat's edges. Japan against China with `hand-defended`: a battle for
Nantong at the first hour, the scales 0.216 and 1.490 at a progress of 20, the progress held at 1000 while it lasts.

**A carrier's air groups in a save** are `air` blocks inside a `ship` block, with a `location` and **no `base`**; here
each is given its fleet's province for its base as the fleet is read.

**A second trial**, since no save has a drop, a group rebased or an air supply, and no 1936 order of battle a transport
plane or a paratrooper: `OpenHOI3.SaveCheck airborne <install> [paradrop|carrier|supply][-defended] [country] [enemy]
[days] [seed]`. On 2026-10-16, with the install's own unit types:

- `paradrop`, Germany on Poland: a transport wing and three paratroop brigades made at Breslau, the target Ostrów
  Wielkopolski 67 away. Loaded at once, the order starting the next midnight: the wing takes off in that hour and is over
  the target by its end, and the next hour drops the division - 11 supplies brought into the province out of the
  stockpile, the division having walked out of nowhere - and flies home within it. The empty province is Germany's at
  once, so Poland is told nothing; the division settles the same hour, its organisation 41.58, two thirds of 63; the wing
  is left at 0.477 organisation.
- `paradrop-defended`: from Königsberg onto Danzig, four Polish units in it. The drop starts a battle, Poland is told
  "Germany is dropping paratroopers in Danzig" the hour of the drop, the division keeps its mark and an attack delay of
  122.
- `carrier`, the United Kingdom: HMS Furious in Scapa Flow carries two unnamed groups of a wing each. The first is given
  carrier protection for Scapa Flow its first hour; rebased to Glasgow it leaves the carrier, flies there in three hours
  and lands with half its organisation; rebased back, it is aboard HMS Furious at once and done two hours later, with
  carrier protection again the hour after. The Home Fleet sails to the Moray Firth and the group goes with it, its base
  there; the fleet taken away, the group is given Scapa Flow's air base, the nearest, and no order.
- `supply`, Germany: two transport wings at Berlin a day in, over the 244 provinces of Germany's in their range, 39 of
  them short that day; it picks Garmisch Partenkirchen, the last short one of the list, loads at Berlin at 1:00,
  delivers at 4:00 and loads again at 9:00, and over five days carries 13.6 supplies there, about 1.5 a load - what
  Garmisch went short of, under the cap of 15.

**The two select buttons, tried with `--click`** on 2026-11-14 (section 12). France played in `Germany1936_01_07_22`, the
French transport fleet selected: its panel lists the Division de Tonkin, `select_land` pressable and `select_air` greyed;
`select_land` pressed, the Division de Tonkin's panel stands alone, the map where it was; `select_air` pressed instead,
nothing - and a press of the Diplomacy button after it opens the screen, the run's positive control. The United Kingdom
in a new game, the Home Fleet selected: two air wings aboard HMS Furious, `select_land` greyed and `select_air`
pressable; `select_land` pressed changes nothing, then `select_air` gives the panel of several with the two wings,
each row's strength 1.

## What looks wrong and is built so

1. **Nothing asks where a unit boards**: the command puts a unit aboard a fleet wherever the two are.
2. **A fleet's list of passengers on the panel goes stale** while the fleet stays selected; a unit put ashore stays in it.
3. **Every fleet's panel says what it can carry**, a warship's "Can carry 0 weight."
4. **The days of supplies a unit brings ashore go into the sea it leaves**, and the unload button and the overload test
   bring none.
5. **Boarding keeps a unit's path**, and a fleet with a passenger on the move may not move.
6. **The landing penalty is made a hundredth worse** with no landing craft (`-70.70%`), and **`extra_amphibious_defence`
   does nothing**: the original asks it of a fleet's summed table, which is always empty (section 9).
7. **The landing scales are set for any landing unit in a battle**, attacker or not.
8. **The invasion mission sends every passenger to the flanks** while the units bound for the target are fewer than the
   enemy's plus six and twice the enemy's - and counts the first in units and the rest in brigades. A distance of nought
   to the landing sea counts as nothing chosen.
9. **One passenger the target refuses, or one that cannot fight left with nowhere, ends the whole invasion.**
10. **A landing on one's own coast is the same 48 hours**, and no landing is slowed by anything but the define.
11. **The multi-unit load weighs the whole selection but asks the fleet about the last unit alone**, and the multi-unit
    unload asks nothing of where the fleets are.
12. **Re-ordering a listed invader clears its country's pending warning**, and the warning lists every province still
    being landed on, not only the new ones.
13. **Nothing asks where a land unit boards an air unit** either; the panel offers only the air units based where it
    stands.
14. **A paratrooper's seven days come out of the land province it last walked out of**, not out of the stockpile, and
    the unload button brings none.
15. **The transport planes are left at a hundredth of their organisation after every drop**, on an enemy or not.
16. **`PARATROOPEDUS` goes only to the player holding the target once the drop is over, and once an order** - so a
    player whose empty province the paratroopers took at once is never told.
17. **Neither a paradrop nor a rebase to carrier measures range** once given, and before its start hour a paradrop is told
    to return to base every hour it waits, being unfit to fly until then.
18. **The air supply counts its load into the base province's drawn before it caps it by what lies there**, picks the
    *last* short province of its area, not the neediest, and delivers whether or not the target is still a friend's.
19. **Only one's own carriers are taken**, an ally's never.
20. **Carrier protection is never done by itself**, and fights in the air wherever a battle is going on in the
    carrier's province, of any kind.
21. **An air unit's `select_land` may be pressed and does nothing**: the panel's update enables it wherever anything is
    aboard, a fleet or an air unit, and the click asks first whether the carrier is a fleet. Listed in `enhanced.md`.
22. **A sunk carrier's air groups stay under its fleet** in the order of battle, though based on an airfield now, and
    **a fleet merged into another leaves its carriers' groups aboard with nobody above them**: the merge hands the ships
    over and the removal of the emptied fleet takes its units out from under it. Listed in `enhanced.md`.

## Ours

- **A carried land unit may not be sent to another sea province.** The original lets it board another of its country's
  fleets standing there; the reading did not follow the fleet's list in that case, so here the route refuses it.
- **The invasion mission's three tests not read through are left out**: a landing sea's "on the map" flag, a flank's
  owner area's say, and the empty fleet's wait for a unit falling back towards its sea - which the reading takes never to
  hold.
- **The warning notes a landing where the player's move or the invasion gives a passenger its path**, the two ways one
  is given here; the original notes it wherever any path is set.
- **The selection goes to the fleet once the load command has run**, where the original moves it inside the command, so
  that the fleet's list is made with the unit in it.
- **A land unit boarding an air unit has its path taken away.** The original's command leaves a path alone; the panel
  offers a plane only where the unit stands, so a unit on the march is the one case. ~~One aboard is not sent
  anywhere~~: **a passenger aboard an air unit may be sent, and walks still aboard**, as the original's AI sent the July
  1941 save's 1. Fallschirmjäger Division from Berlin to Oslo (2026-11-18; `moving.md`, section 9.1;
  `reversing/findings/FINDINGS-liveread.md`, section 127; `enhanced.md`).
- **Carrier protection's line in the unit panel** is `CAG_DUTY_SHORT_DESC` with "Returning", "Resting" in its base's
  province, "Fighting" in a battle or a bombing and "Flying" otherwise - the rebase to carrier's four. What the original
  puts there was not read.
- **A carrier air group's base province is kept in step with its fleet** each time the fleet enters a province, where the
  original asks the ship for it; the two answer alike.
- **"Its country's air bases"**, which a group whose carrier is gone is given the nearest of, are the land provinces its
  country holds with an air base built; the first of equals in the map's order wins. The reading says no more.
- **The air unit the planes' load button finds** is looked for among the units in the province, in the order it lists
  them, that are based there; the original walks the province's own list of the units based on it.

## Not built

- **The panel for several units selected** - its load, load on aeroplanes and unmount - waits for selecting several.
- A paratrooper's and a transport plane's technology efficiencies: no reader of `paradrop_mission` or
  `transport_supplies_mission` of their own was found. Since 2026-11-21 both count as their orders' mission efficiency
  in a battle, as every mission's does (`landcombat.md`, section 4; `CMissionEfficiency`).
- The AI: nobody but the player boards, lands or orders a transport or an invasion, the original's AI posters of the two
  commands and its use of the progress among them (`reversing/findings/FINDINGS-invasion.md`).
- A fleet merged, split or with ships moved out: there are no such orders yet.
- The province the warning's message goes to on a click (its GOTO).

## Not established

- ~~A transport plane shot down with paratroopers aboard~~: **read on 2026-10-16** - they are removed with it, as a
  fleet's are (section 13.3). It was not known from play.
- Whether anything draws on a sea province's pool of supplies.
- **A carrier removed lets its passengers go where they stand, or takes them with it**, by a flag its caller passes (read
  on 2026-10-16): the hourly removal, a capitulation and an annexation take them; a government going into exile, for a unit
  it destroys outright, lets them go; what a country's reset passes was not read. Here every removal takes them.
- What the fleet's landing marker (a figure on an object of the fleet's) shows.
