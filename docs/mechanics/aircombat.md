# The battle in the air: air superiority, the intercept and the air battle

What an air unit on a fighting mission does each hour and how a battle between air units is
fought, written as rules this project can implement. No addresses and no offsets. Each rule
says how sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary - and
where this project chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-airbattle.md`, with `reversing/findings/FINDINGS-airnaval.md`
(what starts an air battle, the fitness test, the four hours),
`reversing/findings/FINDINGS-combatmods.md` (what the hour makes of a wing),
`reversing/findings/FINDINGS-airdefence.md` (what the shot reads),
`reversing/findings/FINDINGS-seaair.md` (the unit's hour, the range) and
`reversing/findings/FINDINGS-airorders.md` (a mission's area, its hours and dates, as the
orders window makes them).

**Nothing here was watched in the original, and nothing can be checked against a save**: none
of the maintainer's has an air battle, and the rolls cannot be made again. It is the second of
the pieces air and naval combat was cut into (`navalcombat.md`); bombing, anti-aircraft fire,
a carrier's aircraft and the air side on screen are not in it.

**It is written to do what the original does, its oddities with it.** Section 9 gathers the
oddest.

In the code: `lib/OpenHOI3.Sim/Missions` (`CAirMissions`, `CAirSuperiority`, `CAirIntercept`,
`CAirTargets`, `CAirMissionArea`) and `lib/OpenHOI3.Sim/Combat` (`CAirBattle`, `CAirFire`,
`CAirModifiers`, `CAirRetreat`, `CIntelligence`).

## In one paragraph

An air mission is an order on an air unit, asked every hour whether it is still going, and the
asking is where it flies. **Air superiority** goes round the provinces of its mission and flies
at the strongest enemy air unit it knows of in them. **The intercept** waits at its base,
watches its provinces and two steps around them, flies to meet the strongest enemy air unit in
them or about to enter them, and turns for home when it has caught it. A battle starts when an
air unit under a fighting order finds an enemy air unit in the province it has entered or
stands in; each hour every wing fires `air_attack` shots at one enemy wing; after four hours
it is over, and a side worn to nothing falls back.

## 1. The frame every air mission shares

An order has, beside its province: a list of **provinces** (the mission's area), a **stance**,
a **time** (by day, by night, or both), a **start** and an **end** hour, and whether it has been
told to **return to base**. `confirmed`

**The day and night gate**, asked wherever a mission would do something: a mission by day at
night, or by night by day, is stopped - at its base its path is dropped, elsewhere it is told
to return to base - and nothing more is done that hour. Night is the night of the province
the unit is in (`landcombat.md`, section 5).

**When it is given**: a unit with a base that is not at it is told to return to base.

**Every hour, whether it is still going**:

    the unit has no base                                              -> done
    not told to return:
        the gate
        still not, and in no battle:
            the unit's range < its distance from its base             told to return -> done
    told to return:
        at its base: no longer told to return                         -> done once past its end hour
        with no path and in no battle: given the way home again
    -> still going

- **A mission outlives its sorties.** Home again it is no longer "returning", and the
  mission's own hour sends it out again, until the end hour has passed.
- **A unit that finds itself out of range loses its order**, and flies home.
- "Told to return" gives the unit the way to its base at once, where it is not there.

**Fit to fly**: the order's start hour has come, and

    the wings' strength in all, over their types' max_strength in all   >= the stance's strength limit
    their average organisation, over their type's default_organisation  >= the stance's organisation limit

with `AGGRESSIVE_`, `DEFENSIVE_` and `PASSIVE_STRENGTH_LIMIT` and `_ORGANISATION_LIMIT` - 0.25
and 0.20, 0.50 and 0.30, 0.75 and 0.50 in the base game. There is no fuel in it and no weather.
`confirmed`

**Sent home empty**: an air unit away from its base whose wings have no organisation at all
loses its order and is given a plain way home; one with under a thousandth of strength in all
is removed. `confirmed`, from the unit's hour.

**With no order at all**, an air unit away from its base that has no way to go, is in no
battle and no bombing, and has a base is given the way home each hour, where there is one.
`confirmed` (`reversing/findings/FINDINGS-seaair.md`, section 3): it is the empty order's own
hour. So a mission ended or taken back over enemy ground ends in the flight home, and a beaten
unit that fell back out to sea goes on home from there.

**The two hours come from the orders window** (`fleetsandair.md`, section 4): the start
widget's date, and the end widget's - or with "continuous" ticked, which it is when the window
opens, **the defines' `end_date`**. `confirmed`

*Ours:* an order made with nothing said - the developer switch, the trials - flies day and
night, starts at once and **never ends**.

## 2. A mission's area

`confirmed` The mission's provinces come with the order - the original's command carries the
list - and each is kept only where it is **within the unit's range of its base**.

**The window makes the list** (`fleetsandair.md`, section 4, has the window) from **a shape**,
**a range**, **an angle** and **a width**, which the sliders and a right click on the map set.
"In range" below is always the unit's range of its base. `confirmed` throughout.

| shape | the list |
| --- | --- |
| **point** | the window's province, where it is in range |
| **region** | every province of the window's province's **first region** that is in range, in the region's own order; with no region, the province as for a point |
| **circle** | the walk below |
| **cone** | the walk, with an arc |

**The walk**: from the window's province outwards, through neighbours the unit may fly to,
every province in range and **within the area's range of the window's province** is added as
it is reached, and the walk goes on only from provinces that are. Distances are the range's
own: straight lines between provinces (`fleetsandair.md`, section 3). **For a cone** a
neighbour must also lie in its arc before it is added or walked from.

**The arc**: the neighbour's **bearing** from the window's province - between their points,
`atan2(y2 - y1, x1 - x2)` plus three quarters of a turn, which is the compass bearing, north 0
and east a quarter turn, with x not wrapped round the map - must lie between the angle less
half the width and the angle plus half, each taken modulo a turn **keeping its sign**:

    high = (angle + width / 2) mod a turn
    low  = (angle - width / 2) mod a turn
    b    =  bearing mod a turn
    high above low:   low <= b <= high
    otherwise:        b >= low  or  b <= high

So **an arc whose low end falls below north loses its part west of north** - a cone pointed
just east of north and wider than twice its angle reaches round to north and stops. All of it
in single precision, as the original works it.

**A right click on the map**, the window up:

| | without shift | with shift |
| --- | --- | --- |
| circle, cone | **aims it**: the range becomes the distance to the province, no further than the unit can fly; a cone's angle the bearing to it; the list made again | the province **taken off the list, or put on its end** - and one out of range only ever taken off |
| point | the window **moves** to the province: its list, its ticked row and its area made again for it | the province **added**, where the order may list it and it is not listed |
| region | the window moves, as for a point | every province of the province's first region added so, or the province where it has none |

The window's own province does nothing to a circle or a cone. "May list" is the order's own
test - for the eight bombing missions theirs (`bombing.md`, section 1), **which asks no range**,
and for anything else the range - while a region's own list asks the range alone.

**A slider** sets its value and makes the list again for a shape that uses it - the range for
the circle and the cone, the angle and the width for the cone alone. **A shape button** makes
the list again for its shape **keeping the range and the angle a right click gave**; a slider
moved by enough replaces them.

**A province's regions** are the regions of `map/region.txt` that list it, in the file's order;
6,136 of Their Finest Hour's 11,632 provinces are in more than one. That the original keeps them
in the file's order is `inferred` from which two regions it names first.

*Ours:* `CAirArea` is the window's area; `CAirMissionArea.Circle` is its circle for the
developer switch and the trials, which take a radius.

## 3. Air superiority

Its hour, before the frame's:

    told to return                                                    -> nothing
    not fit to fly: told to return                                    (and on)
    in a battle, or with a path                                       -> nothing
    it was given no fuel: told to return                              -> nothing more
    past its end hour: told to return                                 (and on)
    the gate

    no target, at its base and fit to fly: it picks one               (section 5)
    a target that is in none of the mission's provinces is dropped

    no destination and no target, or standing at its destination:
        the destination is the province after it in the mission's list - the first where
        there was none, and the first again after the last

    a target in the unit's own province:
        not past its end hour: the gate; it picks again; no destination
        otherwise: told to return
        -> nothing more

    no target: with a destination, the gate, and the way there
    a target:
        it is moving and its next province is the destination already: as with no target
        the gate
        it is moving: the way to its next province, which becomes the destination
        otherwise: the way to the province it is in, which becomes the destination

`confirmed`, as a reading.

- **It flies to where its target is going**, one province ahead of it.
- **It picks only at its base or on top of its target**; in between it keeps what it has.
- With nothing to go for it goes round the mission's provinces in their order.
- Unfit or past its end it is told to return **and the hour goes on**, so the way home may be
  replaced by a way to a target in the same hour; the next hour finds it "returning" and does
  nothing, and it flies where its path leads until the frame gives it the way home again.

## 4. The intercept

**When it is given**, after the frame's part: the provinces it **watches** are made - the
mission's, and every province within two steps of one.

Its hour, before the frame's:

    with a path, or in a battle                                       -> nothing
    the gate; told to return                                          -> nothing
    given fuel, not past its end hour, and fit to fly:
        standing in its meeting province: it picks again where it can, and goes
        no target:
            at its base: it picks where it can, and goes              -> nothing more
        a target:
            in a province it does not watch: dropped, told to return  -> nothing more
            not in the unit's own province: it goes                   -> nothing more
    told to return

and then, where its own province is beyond its range from its base, told to return, with its
destination forgotten. `confirmed`, as a reading.

**Going**: to the meeting province where it has one and is not in it; otherwise to the
target's next province where the target is moving - nothing where that is the destination
already - and else to the province the target is in.

- **It waits at its base** and is told to return whenever it is away from it with nothing to
  chase, has no fuel, is unfit or past its end.
- **Having caught its target it is told to return.** The battle begins as it enters the
  province, a wing in a battle does not move, and it flies home when the battle lets it go.

## 5. Picking a target

**Air superiority**: among the air units standing in the mission's provinces that the unit
may fly to **and its country knows at 2 or better** (section 6) - units of a country at war
with its own.

**The intercept**: among the air units of any other country in the provinces it watches,
flyable and known at 2 or better, that are either **in one of the mission's provinces** - no
meeting province - or **have one among the first three provinces of their path** - which
becomes the meeting province. A unit with no wings is never picked.

Both score a candidate the same way, and the highest wins, the first found among equals:

    it is at its own base, or is not an enemy                         never
    wings already after it - every air unit at war with it whose
        mission is an intercept or air superiority with it for
        target - are more than none, and with this unit's own
        wings more than 3                                             never
    otherwise                                                         its strength in all

`confirmed` So **three wings are enough for one enemy unit** unless one unit brings more, and
of what is left the strongest is taken. A unit at its own base is never chased - though it can
be fought there (section 7).

## 6. What a country knows of a province

`confirmed` A level from 0 to 9, worked out afresh once a day:

    whoever holds the province, and each of its allies and fellow faction members    9
    a country with a unit standing there: on land                                    9
                                          at sea                                     2
                                          at sea, a patrol shadowing somebody        9
    radar: up to 8, in two rings about each station                                  not built

**Nothing else**: not ground a country owns and an enemy holds, and not its neighbours'. So an
intercept over its own or a friend's ground always sees what flies in, and air superiority
over an enemy's ground has a target to chase only where its country has a unit - which, the
levels being a day old, includes a province it flew through yesterday. **A battle needs none
of this**: it starts on entering the province.

*Ours:* `CIntelligence`. The holder's and its friends' level is asked on the spot and the
units' part is the daily snapshot `navalmissions.md`, section 9, already keeps. The original
also takes a level off a list each unit carries, which nothing read fills, and has no fog at
all with a setting off; neither is built.

## 7. What may fight what

`confirmed` Two air units of countries at war, neither falling back. **The orders count only
at a base**:

    this unit is at its own base:    its order is a fighting one, and it is fit to fly
    the other is at its own base:    THIS unit's order is a fighting one, and the OTHER is fit to fly

A fighting order is air superiority or an intercept. So two units in the air fight whatever
their orders - a bomber on its way is fair game - a unit sitting at its base is fought only
by a fighter, and **a unit at its base too worn to fly cannot be fought at all**.

**The start** is the land battle's (`landcombat.md`, section 1): a unit that enters a
province, or stands in one with no path, joins the air battle there if a side will have it,
and otherwise starts one with **the first** air unit of the province it may fight - but only
a unit **under a fighting order** starts one. One air battle to a province. Everyone else
there is offered a side.

**The hour's grace.** A unit that is in an air battle as its hour begins starts and joins no
other for two of its hours, that one counted. A battle ends after every unit's hour, so the
hour after a battle a unit that stays where it is starts none. **The wait ends with its first
move**: every pass of the march of a unit in no battle wipes it, so a unit that flies on may
fight again in the very next province it enters. And the wait is the unit's own - it keeps
*it* from starting or joining, and a unit that finds it is not asked for its. *Corrected
2026-10-10 by the reading for `bombing.md`: this had the hour hold wherever the unit went.*

## 8. The battle

Each hour, the attacker first at every step, as on land: two dice nothing reads; what the
hour makes of every wing; each side's shooting; the settling of the damage; and at the fourth
hour the end.

**What the hour makes of a wing**, each as a factor on its attack, none under a hundredth:
`confirmed` unless marked.

| | |
| --- | --- |
| experience | a hundredth of the wing's own, with `PRIDE_BONUS_EXP`, x (1 + the unit's leaders' `experience_bonus`) |
| base | `BASE_PROXIMITY_BONUS` where the battle is in or beside the province of the unit's base |
| mission | `INTERCEPT_ATTACK_BONUS` for a wing that is no bomber on an intercept; `AIR_SUP_DEFEND_BONUS` on air superiority |
| mission efficiency | the country's, for the unit's order (`navalmissions.md`, section 10) |
| supplies | less `COMBAT_SUPPLY_LACK_IMPACT` by what the unit was not given, x (1 + the leaders' `out_of_supply_modifier`) |
| the radio | the country's `combat_efficiency`, which the `radio` technology gives |
| dissent | less `COMBAT_DISSENT_IMPACT` by it |
| crowding | `AIR_STACKING_PENALTY` for each wing of the side after the first, `AIR_STACKING_PENALTY_MAX` at most |
| radar | the highest radar level a friend of the side holds in the province or beside it, times `RADAR_COMBAT_IMPACT` and one plus the country's `radar_impact` |
| the weather | less the wind by `AIRWINDSPEEDMODIFIER`, the cold or the heat past their thresholds by their impacts, the cloud by `AIRCLOUDMODIFIER` and the rain by `AIRRAINMODIFIER` - on the attack alone |
| night | `BASE_NIGHT_PENALTY` and the wing's own night figure, attack for the attacker's side and defence for the defender's, and the leaders' `night_attack`, less a hundredth of its `air_detection`; only when it comes out against it, and on the attack alone |
| the leader | last: `COMBAT_LEADER_IMPACT` for each point of the unit's own leader's skill, where his rank's `AIR_RANK_*` commands as many wings as it has and he has some skill; no trait (`leaders.md`, section 10) |

The two mission bonuses are on the attack alone as well, the one the defines call a
defender's among them. The leaders' traits come down the unit's chain of command; dissent has
no trait here. Not built: difficulty. The radio and the radar were
added on 2026-10-10 with `bombing.md`, when the reading for it named their technology keys.
**A wing's defence is worked out and nothing uses it**: only its attack decides anything.

**The shot.** Every wing of the side with an `air_attack`, in the order of the side's units
and of each unit's wings:

    target: the one it had, while that wing is still in the game; otherwise a new one (below)
    n     = air_attack x what the hour made of its attack
    shots = the whole part of n, and one more by its fraction's chance in a hundred
    each shot, while the target's strength less what is already against it is not under nothing:
        a roll nothing uses
        avoided by CHANCE_TO_AVOID_HIT_AT_NO_DEF in a hundred, 99 at most
        organisation: (a die of AIR_COMBAT_ORG_DICE_SIZE) x AIR_COMBAT_ORG_DAMAGE_MODIFIER
        strength:     (a die of AIR_COMBAT_STR_DICE_SIZE),
                      x AIR_COMBAT_CRITICAL_HIT_DAMAGE_MUL by AIR_COMBAT_CRITICAL_HIT_DAMAGE_CHANCE in a hundred,
                      x AIR_COMBAT_STR_DAMAGE_MODIFIER
        against a carrier's air group: x AIR_COMBAT_CAG_STR_DAMAGE_MOD and x AIR_COMBAT_CAG_ORG_DAMAGE_MOD

`confirmed`, rolls and all: one unused, the avoiding, the organisation die, the strength die,
the critical one - the last three only for a hit.

- **Nothing of the target comes into it** but whether it is a carrier's air group: no air
  defence, no experience.
- Settled, a wing takes **a hundredth** of what was rolled against it, like a brigade
  (`landcombat.md`, section 6). With the base game's figures an interceptor fires four shots
  an hour, about half hit, and a hit costs 0.125 to 1.125 organisation and 0.1 to 0.4 strength
  - a critical one 1 to 4.

**A new target.** The other side's wings are gathered in the order of its units and their
wings - each whose strength less what is already against it is not under nothing. Then one
roll in a hundred against a tenth of the wing's **targeting skill** - its country's
`bomber_targeting` and `fighter_targeting` added together, as thousandths, and ten for each
point of its unit's leader's skill, whatever his rank: above it, **a random one**; else **the
best**, the highest of

    priority x (250 + a hundredth of the strength it has lost) x (1 + targeting)

with the target's type's `priority`, and for `targeting` the firing wing's country's
`bomber_targeting` against a bomber and `fighter_targeting` otherwise. The first among equals;
one that scores nothing is never the best. `confirmed`

*Ours:* a wing keeps its target while that wing is **on the other side of the battle**. The
original keeps it while it is anywhere in the game, which differs only for a unit that leaves
a battle by an order in the middle of it; and a unit that joins a battle forgets its targets,
as there.

**The end.** At the fourth hour, the attacker asked first: a side with under a hundredth of
strength in all, or whose wings average under 1 organisation, **has lost**, and its units fall
back. And at the fourth hour the battle is over whether anyone lost or not. `confirmed`

**Falling back.** A beaten unit standing in the battle's province looks at the provinces
beside it that it may fly to: `confirmed`, as a reading

    a sea province:  1, and 2 more for each land province beside it with a naval base it may fly to
    a land province: nothing

and takes the highest; with no sea beside it, **its base's province**. It is given the way
there and is "falling back" until it arrives, which keeps it out of every battle. One with
nowhere to go, or no way there, **is removed from the game**; one not in the battle's province
stays where it is.

## 9. Worth a look in the original

Nothing here was watched. Each of these is built as read. **The maintainer, shown four of them
on 2026-10-10** - items 1, 2, 3 and 8: of item 3, "Yes thats a bug in the original."; of the
rest, "The other stuff i dont know of the top of my head. Just note them." So item 3 is the
original's by his word as well as by the reading, and is kept as it is there - he called it
a bug and did not ask for it to be mended; the others are noted and wait for somebody who
can watch the original.

1. **A beaten air unit beside the sea flies out to sea**, to the water with the most ports
   around it, and not to its base (section 8).
2. **A unit too worn to fly, sitting at its base, cannot be attacked in the air** (section 7).
3. **A wing's defence does nothing in an air battle**, and nor does `air_defence` (section 8).
   A bug in the original, by the maintainer's word.
4. **Air superiority chases a target only where its country already knows the province** -
   over enemy ground, in practice where it has been (sections 5 and 6).
5. **The intercept never asks whether its target's country is at war with its own** before
   scoring it; the score's own test does (section 5).
6. **A critical hit multiplies the strength lost and not the organisation** (section 8).
7. **A unit on air superiority that is too worn to fly still takes off.** At its base it is
   told to return - which there does nothing but mark it - and the same hour is given the way
   to the next province of its round; the mark is taken off again before it has moved, the
   next hour finds it unfit in the air and turns it back, and so on: an hour out and an hour
   home for as long as it is worn (section 3). In the air it can be fought.
8. **Battles follow one another with an hour between** for as long as two units stay in one
   province and fit: four hours, the hour of grace, four more (section 7). A unit that moves
   on loses its hour and may fight at once where it arrives.

## 10. Trying it, and what is not here

`OpenHOI3.SaveCheck airbattle <install> [attacker] [defender] [seed]` puts two countries at
war, takes the pair of their air units with an air attack whose bases are nearest with the
second's in the first's range, gives the second an intercept over its own base and the first
air superiority over it, and tells the week: the first battle round by round and the rest a
line each. It is a trial and not a check: there is nothing to set it against. With no tags it
is Germany against Poland - nothing of Germany's reaches France from where it starts. On the
start date:

    three wings of German tactical bombers from Breslau over Warszawa, 231 away of their range
    of 400, against Poland's three there: over Warszawa in two hours and into a battle; at
    night each wing's attack at 0.73 and 0.62 of itself, the German air marshal's skill of 4
    adding 0.2 and the Pole's 0.05; four hours, nobody lost, an hour's pause and the next.
    After nine battles in two days the Germans are down to 215 of 300 strength and 7.0
    organisation of 40 and turn for home; the Poles, at 254 and 15.2 of 30, catch them over
    Tomaszów on the way and fight them there ten times more in the week, until the Germans
    have 125 of their 300 and the Poles 220

(The figures are of 2026-10-11 after the leaders were built: the radio is in them, and a unit
that flies on from a battle may fight again where it arrives.)

In the client `--mission=GER:airsuperiority:<province>` and `--mission=GER:airintercept:<province>`
give that mission to the country's air unit with the most air attack that has the province in
its range, there being no AI to; a number after the province makes the mission's area a circle
of that range about it. **The orders window gives both too**, with its shapes, sliders, hours
and dates (`fleetsandair.md`, section 4); `--select=Luftflotte II` selects an air unit that no
click can reach at its base, and `--click=` with `r` and `s` drives the window.
A selected unit's panel names the mission with one word after it - `Waiting`, `Attacking` or
`Returning`; the intercept's are the original's and air superiority's were not read and are
given the same. **A battle in the air has a window and a row in a unit's panel, and no marker
on the map** - the original gives it none: `combatview.md`, section 11. The readout of a
selected province says there is a battle.

### Not built

- A **carrier's aircraft**, with the order that keeps them over their fleet. Bombing and
  anti-aircraft fire are `bombing.md`'s.
- **Every other air order** - the reserve, the transport of supplies, the paradrop.
- The orders window is whole since 2026-10-11, and the air map mode is built: `airmap.md`.
- **Difficulty** in what the hour makes of a wing; **radar** in what a country knows. The
  leaders are built since 2026-10-11 (`leaders.md`, section 10). The weather's part is built and there is no weather yet.
- **The practical** an air battle earns, and experience.
- The AI's own giving of missions, and its 120-hour end.

### Not established

- Whether flying costs a wing organisation: nothing read takes any.
- What fills the list of who sees a unit, by which a country sees into ground beside its
  own units (section 6): nothing here does, and interdiction needs it (`bombing.md`).
