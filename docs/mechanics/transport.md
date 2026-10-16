# Transport: land units carried by sea, the landing, and on screen

Land units carried by a fleet: what boards what and how much a fleet carries, getting off, a passenger's hours at sea,
what a sea battle and too little room do to it, the transport mission; a landing on a coast from the sea with its march,
its battle and the invasion mission; the warning a landing raises; and what the screen shows of all of it. Read out of
the original on 2026-10-15 (`reversing/findings/FINDINGS-transport.md`, sections A, B and C), and set against the
maintainer's three screenshots of a landing at Danzig (section B.9 there) and against his saves, which hold carrying
fleets. `confirmed` unless marked.

**The carrier as an air base, carrying by air, the paradrop and the air supply** were read in the same wave (section D
there) and are this piece's second half; section 13 below is their spec, and they are **not built yet**.

**Built 2026-10-15**, sections 1 to 12 (`Sim/Transport`, `CTransportMission` and `CInvasionMission` in `Sim/Missions`,
`CLoadUnitCommand` and `CUnloadUnitCommand`; `CTransportText` and `CCarryButtons` in `Interface/Screens`; the panel's
`CLoadedUnitsEntry` and `CLoadedUnitRow`), tried with `--click` and set against the saves (section 14). The maintainer has
not looked at it.

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
- under a transport order, is never made the defender of a sea battle, and if nobody plays its country, starts none;
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
maintainer's saves - three transports, the Division de Tonkin aboard, from Saigon to Luganville - is in "has a path"
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

**Measured** on the maintainer's screenshots: progress 4.10% (1.0 of 24), attack 23.20%, defence 148.00%; progress
39.50% (9.5 of 24), attack 51.60%, defence 130.30%; at 100.00% both back at 1000. Every figure is the arithmetic above.

**The amphibious penalty** (`BM_AMPH_PENALTY`), an attacker's alone. A unit counts as landing when it stands in the sea -
unless it is the rebels' - **or attacks over a strait** (an edge of the crossing kind) from land:

    v = AMPHIBIOUS_LANDING_PENALTY + the unit's amphibious attack adjuster + its leaders' amph_attack trait     (define -0.7)
    where v < 0, it stands in the sea and is aboard a fleet:
        r = min(the fleet's invasion defence, LANDING_PENALTY_DECREASE)                                         (define 3.5)
        v = v * (1010 - r * 1000 / LANDING_PENALTY_DECREASE) / 1000
    v = max(v, -989)
    where v < 0: the modifier

So with no invasion defence the penalty is **made worse by a hundredth** - -70% is -70.70%, as the screenshots show - and
at a defence of 3.5 it is cut to a hundredth. The adjuster is the mean over the unit's brigades (`amphibious`, *units
file*: engineers 0.4, marines 0.5). A strait assault from land gets the penalty but not the cut and not the scales.

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
  "Can carry 0 weight." (the maintainer, 2026-10-15);
- a row (`loadedunit_entry`) for each passenger, then for a fleet a row for every air unit aboard its carriers: the
  first brigade's counter where the player's intelligence of it is 3 or more, the size by the unit's level, its name,
  its order, its objective, and its organisation and strength bars - no strength figure. Pressing a row selects that
  unit and centres the map on it;
- `select_land` - "Select all attached land units." (`UW_SELECT_LAND`) - selects every passenger, pressable where any is
  aboard; `select_air` - "Select all CAG's attached to this fleet." (`UW_SELECT_AIR`) - every aircraft aboard the
  carriers, pressable where any is, hidden for an air unit.
- **The list is made when the fleet is selected and not again while it stays selected**: a unit that gets off stays in
  it until the fleet is selected anew (the maintainer, 2026-10-15). An air unit's list is made again when its count
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
- `load_on_air_multiunit` waits for section 13.

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

## 13. The carrier and carrying by air - the second half, not built yet

**A carrier is an air base.** Rebase to carrier may be given to an air unit whose every wing is a carrier air group
(`is_cag`), where the province - or failing that its port - holds a fleet **of the unit's own country** with a ship whose
`carrier_size` (*units file*: carrier 2, escort carrier 1) holds the wings aboard it and the unit's together. The first
such fleet and its first such ship are taken. Given, the unit's base is that ship at once, it is put under the fleet in
the order of battle, and it flies to where the fleet is now, as the strike missions judge the light of its arrival; it
follows the fleet, and it is done when it is at its base. **No range is measured.** Aboard, its base province is where
the fleet is, so its range moves with the ship; it is at its base only while the fleet is in no battle and no bombing;
it moves with the ship when it has no path and no battle; it is never crowded. Its carrier sunk, it is given the nearest
air base of its country in a straight line.

**Carrying by air.** A land unit boards an air unit at its base - same owner; the air unit in no battle, no path, not
falling back, at its own base with an airfield there; the land unit's attack delay not running; **every brigade
`can_paradrop`** at 95% of its organisation or more (`PARATROOP_MISSION_ORG_REQ`, *define*); and its weight fitting
the air unit's `transport_capability` (*units file*: `transport_plane` 15; paratroopers 5). It moves with the plane, is
never fought, not reinforced, and draws at the plane's base. It gets off only at an air base.

**The paradrop** may be given over any land, at least `PARADROP_MIN_TIME` (24, *define*) hours ahead. The plane waits
while a passenger's attack delay runs (where the target is an enemy's) or a brigade is under 95%. Over the target each
passenger, in order, is marked dropped where the target's holder is at war with it, has an attack delay of
`PARATROOP_MISSION_DELAY` (168, *define*) where hostile, gets off bringing `PARASUPPLYPOOL_DAYS` (7, *define*) days of
supplies, and looks for a battle - taking an empty enemy province at once. The planes are left at
`PARATROOP_DROP_ORG_MULT_PLANE` (0.01, *define*) of their organisation and go home. A dropped unit fights at
`PARADROP_PENALTY` (-0.5, *define*), is spared the lack of supplies, counts once in the battle's width, and on its first
hour out of a battle loses the mark and is multiplied by `PARATROOP_DROP_ORG_MULT` (0.66, *define*). `PARATROOPEDUS`
tells the player holding the target, once.

**The air supply** goes to the last province of its area short of supplies or fuel, with `AIR_SUPPLY_FACTOR` (0.5,
*define*) times the planes' capability, loaded at its base.

`reversing/findings/FINDINGS-transport.md`, section D, has the whole of it.

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

## What looks wrong and is built so

1. **Nothing asks where a unit boards**: the command puts a unit aboard a fleet wherever the two are.
2. **A fleet's list of passengers on the panel goes stale** while the fleet stays selected; a unit put ashore stays in it.
3. **Every fleet's panel says what it can carry**, a warship's "Can carry 0 weight."
4. **The days of supplies a unit brings ashore go into the sea it leaves**, and the unload button and the overload test
   bring none.
5. **Boarding keeps a unit's path**, and a fleet with a passenger on the move may not move.
6. **The landing penalty is made a hundredth worse** with no landing craft (`-70.70%`), and **`extra_amphibious_defence`
   does nothing**: the original never reads it.
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
- **The two buttons that select every passenger or every aircraft aboard cannot be pressed**: selecting several units at
  once is not built.

## Not built

- Section 13: the carrier as an air base, carrying by air, the paradrop, the air supply.
- **The panel for several units selected** - its load, load on aeroplanes and unmount - waits for selecting several.
- The AI: nobody but the player boards, lands or orders a transport or an invasion, the original's AI posters of the two
  commands and its use of the progress among them (`reversing/findings/FINDINGS-invasion.md`).
- A fleet merged, split or with ships moved out: there are no such orders yet.
- The province the warning's message goes to on a click (its GOTO).

## Not established

- **A transport plane shot down with paratroopers aboard**: whether they die, stay or are let go. The maintainer does not
  know (2026-10-15), and the reading found no air branch where a fleet's passengers are dealt with.
- Whether anything draws on a sea province's pool of supplies.
- What the removal of a fleet in the hourly drain, a country's reset and a government going into exile do with
  passengers.
- What the fleet's landing marker (a figure on an object of the fleet's) shows.
