# Movement

How a unit is told to go somewhere, how its way is found, and what an hour of the march
does. Read out of the original on 2026-10-09:
`reversing/findings/FINDINGS-movement.md`, with the route finder's earlier account in
`reversing/findings/FINDINGS-redeploy.md`. `confirmed` means read; *ours* means decided
here. Section 2 is also **checked against the original's own cache, edge for edge**, and
section 5 **against savegames a few hours apart, division by division**.

**In one line.** A unit holds the provinces still ahead of it and one number, its progress
towards the first. Every hour its speed is added to the progress; when the progress reaches
the distance of the edge from the province it is in to the next, it is in the next, and
what is left over counts towards the one after.

**This is the march itself, and a land unit's orders.** A fleet and an air unit march by
the same rules and are told to by others - a window of orders, a range, ports:
`fleetsandair.md`. What a march does when it meets an enemy - the battle it starts, the
delay an attack leaves, falling back - is `landcombat.md`; the parts of it that are the
march's own are here.

---

## 1. The order

- **A right click on a province with a unit selected** sends the unit there. With **shift**
  held the order is added to the path the unit already has; without, it replaces it.
  `confirmed`: the original's routine for the click takes the province and one flag, and
  passes the flag's opposite to the command as `clear`. That the flag is the shift key was
  confirmed on 2026-10-09: "Yes it is shift."
- A click on **the province the unit stands in** halts it - unless the order is being
  added to a path, when it is one more leg. `confirmed`
- A click on a province the unit **may not go to** does nothing but play the error sound
  and mark the province as refused; one it is sent to is marked as sent (section 7). `confirmed`; both built.
  **For a land unit that includes ground held by a country at war with it** - a war nobody
  declared counting where its regions take the province in - **when the unit is too weak**
  (section 3), **or was given no supplies on its last day and uses some** - not asked of a
  unit aboard a fleet - **or no fuel and burns some**. `confirmed` (`CUnit::CanMoveTo`,
  `reversing/findings/FINDINGS-movement.md`, section 12); built 2026-11-14 in `CMovementRules.MayMoveTo`.
- **The rest of the click's test** (`CUnit::CanMoveTo` whole, `confirmed`, built 2026-11-14 in the same place), in its
  order after the unit's stand-in test and before the land tests above:
  - **a land unit at sea** - aboard a fleet - **is sent to land only where an edge leads from that land to its sea**:
    the landing's own province, not one further inland;
  - **a unit in a battle is sent only where it may fall back** - the original's `CanRetreatToProvince`: the province it
    stands in or one an edge of its province leads to, of any kind; then the enemy land units standing there must add up
    to less than one whole point of strength (their brigades' strength summed in thousandths and cut to whole points),
    and the province must not be held by a country at war with the unit's there - a war nobody declared counting where it
    covers the province - unless the unit's organisation averages one or more. **An air unit** asks only that the enemy
    air units in the province add up to less than a point, and no edge: it may be sent anywhere else;
  - and after the land tests, **no unit is sent anywhere while it has a way, is in a battle, and one of its battles is in
    the next province of that way**;
  - **a fleet only where the sea joins its own** - its province, or the sea its port opens onto - by `SeaConnected`
    (`fleetsandair.md`, section 1), **and not while anything it carries has a way of its own**.
- **What asks it.** The click of a land unit, before the command is posted; a refusal is the error sound and the refused
  mark (section 7). The orders window's move and change of base (`fleetsandair.md`,
  `landorders.md`), the fleet's missions, an air mission's area and the AI's landing ask it as the original does. **The
  AI's `CEU3AI::MoveUnit` asks it as its second gate** (`reversing/findings/FINDINGS-liveread.md`, section 88) - and
  `CAiMoves.Move` asks it so since 2026-11-14 (`moving.md`, section 10). **The command does not ask it** (`CMoveCommand::Execute` asks
  nothing): `CMoveUnitCommand` asks `MayCommandMoveTo`, the test less the edge from a sea, the battle's way out and the
  battle ahead (*ours*), so that what the original's command does with a unit in a battle it does here. Its land tests
  the route asks again of every step (section 3).
- **A unit in a battle is never added to**: the shift key is let go of for it (`confirmed`, section 1 of the findings).
- Nothing happens for a unit with no brigades. `confirmed`. The original also refuses a
  retreating unit and one under an AI's command.
- **That is a land unit's click.** A fleet's and an air unit's bring up a window of orders,
  and only a fleet with shift held, or either in a battle, is sent straight there: `fleetsandair.md`, section 4. **An air
  unit in a battle is asked nothing** - none of the test above, not its range - is marked sent and is given the plain
  move whatever comes of it. `confirmed` (`reversing/findings/FINDINGS-movement.md`, section 13.2).

The click posts a **command**, and the way is worked out when the command runs, so that
every machine works out the same one: `CMoveUnitCommand(country, unit, province, append)`,
and `CCancelMovementCommand(country, unit)` for the halt.

When the command runs: `confirmed`

1. the route starts from the province the unit is in - or, when the order is added to a
   path, from that path's **last** province;
2. the route is found (section 3);
3. **found**: it becomes the unit's path, or is added to it (section 4);
4. **not found**: the unit keeps whatever it had.

## 2. The ways between provinces

Each province holds an **edge** for every neighbour: where it goes, its **kind**, the
province it goes **through** if it is a crossing, and its **distance**. The edge back is
another edge with figures of its own. `confirmed`, and checked - see the end of the
section.

### Which there are, and in what order

The order matters: the route finder tries a province's neighbours in it.

- The map is gone through for each province from 1 up, each of its pixels in turn - row by
  row from the south, west to east - looking **east, west, north and south** of the pixel,
  x wrapping round the map. A different province met for the first time gets an edge to
  this one, and this one an edge to it. So **a province's edges are first those to
  lower-numbered neighbours, ascending, and then those to higher-numbered ones in the
  order its own pixels meet them.**
- `adjacencies.csv` is then read, a row at a time. Its third column names the kind:

  | `Type` | kind |
  | --- | --- |
  | `sea` | 1, a **crossing** through the province in the fourth column |
  | `river` | 2, a **river** |
  | `impassable` | 3 |
  | anything else, `canal` included | 0, **plain** |

  Where the two provinces already have an edge, the row **writes its kind and its crossed
  province over that edge, both ways**. Where they have none, an edge is added each way, at
  the end. So the file can make a land border a wall or a river, and not only add straits.

### How far it is

**The distance is not a length.** It is worked out by a walk:

    from this province's unit point to the neighbour's (map.md, section 4)
        - but between land and sea, the land end is the land province's port
    until the walk is at its end:
        add the movement_cost of the terrain painted on the pixel it is on
        if the river bitmap's byte for that pixel is under 254 and the edge is plain,
            the edge is a river
        step one pixel east or west towards the end - the short way round the map -
        and one north or south, both in the same step

So the walk goes diagonally until it is level with its end and straight after, is as many
steps long as the larger of the two differences, counts the pixel it starts on and not the
one it ends on, and each pixel costs what `terrain.txt` gives the terrain painted there as
`movement_cost`. A pixel of plains is 1. What follows from that:

- **The way back is a different distance.** The two walks count different ends and their
  diagonal parts cross different pixels.
- **A river is any pixel of `rivers.bmp` that a walk happens to cross**, so whether an edge
  is a river depends on where the two unit points are, and can differ by direction too.
- **`movement_cost` is spent here and nowhere else.** A province's own terrain does not
  decide how long it takes to cross; the terrain painted between the two points does. The
  province's terrain acts on the speed instead (section 5).
- A pixel off the east or north edge of the bitmap counts as palette index 0. *Ours:* a
  pixel whose index no terrain covers costs nothing.

### Checked

The original works all this out once and keeps it in `map/cache/adjacencies.bin`, which
the base game ships. `OpenHOI3.SaveCheck adjacency <install>` builds the edges from the map
files by the rules above and compares (2026-10-09, Their Finest Hour):

| | agree |
| --- | --- |
| provinces with the same neighbours | 14,168 of 14,168 |
| provinces with them in the same order | 14,168 of 14,168 |
| kind | 83,596 of 83,596 |
| province crossed | 83,596 of 83,596 |
| distance | 83,596 of 83,596 |

To the thousandth, for every edge of the map. Run it again after touching the map loader.
The file is read where it lies and nothing of it is kept.

*Ours:* the edges are worked out on every start and not cached (`map.md`, section 6).
The original's cache has a consequence worth knowing when modding it: **a change to
`movement_cost` or to a unit point does nothing there until the cache is rebuilt.**

## 3. The route

The original's search is an A\* in outline and differs from one in a way that shows:
`confirmed`

- The provinces to look at next are kept in one list, in order of **what each has cost so
  far plus an estimate of what is left**: the straight line between the two provinces'
  bounding-box centres in whole pixels - rounded down, the short way round the map - times
  **1.25**. A new province goes in before the first that is not cheaper, so of equals the
  newest is taken first.
- **A province is settled the first time a step reaches it.** A cheaper way to it found
  later does not replace the first, and it is never looked at again. So the route is not
  always the cheapest one.
- A province's neighbours are tried in the order of its edges (section 2).
- A step costs the edge's distance - a tenth more into ground an enemy holds, and double
  where the country knows that ground at intelligence 2 or more and units stand in it
  (the two factors 1100.5 and 2000.5, truncated). Built 2026-11-16, intelligence being
  built: the Soviet 7th Air wing's way home from 1306 on 16 July 1941 goes round German-held
  1440 as the original's does (`reversing/findings/FINDINGS-liveread.md`, section 113).
- The route handed back leaves out where it starts and ends with where it goes.

### Where a unit may go

Asked of the destination before anything, and of every step: `confirmed` unless marked

- **not over an impassable edge**;
- **not over a crossing that is closed**: one through a land province an enemy holds, or
  through a sea province an enemy fleet stands in. An air unit is never stopped. *Ours:*
  the first; the second waits for fleets that fight;
- a fleet between land and sea only by its port (`fleetsandair.md`, section 1);
- **only into a province the unit may stand in**: one held by whoever holds the ground
  under it now, or one its country may walk on - and **for a land unit, only land that is in an area**: held by somebody
  and with more than one level of infrastructure built (`supply.md`, section 2). Land with one level or none is closed to
  every land unit, the goal of a route as well as a step on it. `confirmed` on the running game, 2026-11-10: its own search
  never added such a province, and with the rule 57 of 60 of its searches set beside ours give its road
  (`reversing/findings/FINDINGS-liveread.md`, section 32). It is what keeps a long march or a strategic redeployment on the
  railways, where the shortest road would cross poor land;
- and last, **for a land unit stepping into a province its own country does not hold**, four
  tests, each by the unit's owner (`CPathFind::MayStep`; `reversing/findings/FINDINGS-movement.md`, section 12,
  all read on the listing 2026-11-14 and built that day, `CMovementRules.MayLandUnitStep`):
  1. **not onto ground held by a country at war with it when it is too weak**: its strength
     over the most its brigades have had together, in thousandths, below its **shatter
     threshold** - `min(1000 - 500 x officer ratio, 500)`, less 10 for each point of its
     leader's skill and 3 for each point of its average experience, all in thousandths
     (`landcombat.md`, section 6; the officers are a lender's for a unit lent). A unit whose brigades have never had any
     strength counts as -1, and is always too weak. So a full-strength division of a country
     with all its officers goes anywhere and one at half strength goes nowhere an enemy holds,
     **player's and AI's alike**: every finder asks it, the plain and both careful ones;
  2. **not onto ground held by a country at war with it that was given no supplies on its
     last day and uses some**, and
  3. **not when given no fuel and it burns some** (built 2026-10-19). A unit cut off is routed
     round the enemy's land or not at all;
  4. **not into a country it is at war with out of a neutral that merely lets it through**:
     refused where the owner has military access through the country holding the province it
     leaves, that country is not at war with the one holding the province entered - rebels
     counted - and the owner is in a declared war with that one. So a march through a
     neutral by access stops at the enemy's border; the same march through a co-belligerent's
     or an ally's ground - no access, or access to a country at war with the enemy itself - goes
     on into it.

  "At war" in the first three is the original's tag test, rebels on either side counting; the
  fourth asks the declared war of the pair. **Only a land unit is asked any of the four**: the
  supply and fuel tests were asked of every kind of unit here until 2026-11-14, so an air unit
  given no fuel was kept off an enemy's ground it may fly over in the original.

**Whose ground a country may walk on:** its own and a friend's, ground that belongs to
nobody, and the ground of anyone it is at war with. In full, ground is closed only when all
of these hold: the country is at war with neither the province's owner nor its controller;
the province is land; its controller is no friend; and its owner is no friend either, or is
at war with the controller.

A **friend**, in the original, is the country itself, its overlord or its puppet, an ally
or a member of its faction, a co-belligerent, and a country it has military access to -
`IsFriend` with access counting (`reversing/findings/FINDINGS-movement.md`, section 3.1). So
since 2026-11-11 (`CCountryLists.IsFriend`), when it was found that Japan's divisions could not
march through Manchukuo, its puppet: entering the save of April 1936, four Japanese divisions
went to the wrong positions for want of a road there. **Every other place asks the original's own test, and the right way
round**, `IsFriend` not being symmetric (`reversing/findings/FINDINGS-liveread.md`, section 41): a port, the fleet's country of the
port's holder; the naval and air rebase orders, the province's holder of the unit's country; a falling-back unit spared by a
friend's hold, the standing unit's country of the moving one's; the unit panel's capital, its holder of the unit's owner. A
beaten unit's way counts a neighbour friendly by `CanOperateIn` - on its side, or fighting beside it and not its enemy - and a
patrol's shadow lets off its own faction and any country with a friendly agreement (`navalmissions.md`).

A land unit stands in the sea only where a fleet of its country waits there with room to
carry it, which is how it is put aboard; and a land unit going ashore from the sea - its
own coast or an enemy's - walks a leg of its own, `AMPHIBIOUS_INVADE_MOVEMENT_COST` at the
landing's speed and nothing else, and waits or is halted on an enemy's coast if it cannot
fight. Both are `transport.md`'s, sections 3 and 8. *Ours:* a land unit already aboard may
not be sent on to another sea.

**The safe and very safe ways.** The original has two more finders that hold a land unit back further, short of its
goal - the last step into the goal is always allowed (`reversing/findings/FINDINGS-moving.md`, section A.4c):

- **safe** (`CSafePathFind`): not through a province where the strength of the units of countries at war with it - a
  rebel's not counted on its own country's land - is more than its own;
- **very safe** (`CVerySafePathFind`): not into a province held by a country at war with it or by the rebels, nor one
  with a land battle in it.

The AI asks for them (`moving.md`, section 10); a player's click takes the plain way. They weigh a fleet or an air unit
otherwise, which is not built - neither holds one back here. **A strategic redeployment takes the very safe way**, the
player's as well as the AI's: the order works its route out as it is given (`Start`, which `CSetOrderCommand` calls on the
order it has just put in) and always with `CVerySafePathFind`, never the plain or the safe finder
(`CStrategicRedeploymentOrder::RouteUnit` in the fact base's record; `reversing/findings/FINDINGS-redeploy.md`).

## 4. Setting off

Giving a unit a path: `confirmed`

- **Replacing** the path it has: what the unit has already walked is kept **only where the
  new path begins with the province the old one began with**. Otherwise its progress goes
  back to nothing.
- **Adding** to it: the new provinces go on the end and nothing else changes.
- **Halting** - a click on its own province, or the cross beside its order in its panel:
  its order goes, it leaves **every battle it is attacking in** (not one it defends), the
  path is emptied and the progress lost; and where it then stands on ground an enemy
  controls, not falling back and in no battle, it looks for a battle there at once.
  `confirmed` (`CUnit::CancelMovement`, `reversing/findings/FINDINGS-movement.md`, section 4).
  What the attack delay has left to run is kept.

A unit keeps, besides its path and its progress, **the province it was in before** the one
it is in.

**A unit in a battle that is given somewhere to go leaves every battle it is in**, and where
it was holding ground it is falling back from then on. **Halting a unit in the middle of an
attack breaks the attack off.** (`landcombat.md`, section 7.)

**A unit with an attack delay running does not take its first step** - of any march, not only
towards an enemy - and one that arrives with a delay running starts its next leg from
nothing. A unit already walking keeps walking. `confirmed`, and an account of the original for it
on 2026-10-09: a delay stops all movement but the walk into the province that was attacked,
where the unit may arrive with it still running (`landcombat.md`, section 9).

## 5. Speed

What an hour adds to the progress, in the same thousandths as a distance: `confirmed`

    speed = the unit's top speed x its modifier / 1000              (cut)
    speed = speed x LAND_SPEED_MODIFIER                             (cut)

- **The top speed is the lowest `maximum_speed` among the unit's brigades**, each with its
  technologies added. A headquarters that keeps a garrison brigade walks at the garrison's
  pace.
- `LAND_SPEED_MODIFIER` is 0.05 in the base game, so infantry at 4 covers 0.2 an hour: a
  province fifteen pixels of plains away is three days.
- A fleet is scaled by `NAVAL_SPEED_MODIFIER` and an air unit by `AIR_SPEED_MODIFIER`.

**The modifier** starts at 1000 and is multiplied by each of these in turn, each time
`modifier x term / 1000` cut:

| | term | |
| --- | --- | --- |
| 1 | **in a battle, with a path**: attacking in one, `(1000 + the country's combat_movement_speed + the technologies' attack_movement_speed + the leaders' combat_move_speed) x (COMBAT_MOVEMENT_SPEED + the tactics' speed) / 1000`, held between 10 and 1000; in one any other way, 10 | built; the technologies' term since 2026-11-14 |
| 2 | **fuel**, for a land unit or a fleet whose brigades burn any: `fuel received x 900 / 1000 + 100` | built |
| 3 | **the next province's terrain**: 1000 + the unit's adjuster for it | built |
| 4 | 1000 + the leaders' `terrain_speed` for that terrain, by its name, down the chain of command (`leaders.md`, section 8) | built 2026-10-11 |
| 5 | **a river or a crossing** on the way to the next province: 1000 + the unit's adjuster for `river` | built |
| 6 | on land, **the province it is in**: 1000 + the weather there + its `local_unit_speed` | built |
| 7 | on land: 1000 + the country's `amm_movement_speed` x the share of its brigades that are `is_mobile` | built |
| 8 | **falling back**: 1100 | built |

- **In a battle** an attacker walks on at a third of its speed in the base game
  (`COMBAT_MOVEMENT_SPEED` is 0.33), which is what fills its arrow while it fights; a unit
  that is being attacked and has somewhere to go all but stands.
- **Fuel**: a unit that received none keeps a tenth of its speed, and one that received all
  of its ration keeps all of it. A unit falling back is not slowed for want of fuel.
- **A unit's adjuster** for a terrain or for rivers is its brigades' `movement` figures for
  it added up and divided by how many brigades it has - one that says nothing counts as
  zero.
- **The mobile share** is the brigades that are `is_mobile` over all of them, in
  thousandths, cut. The base game's `rubber` is what gives a country `amm_movement_speed`.
- **Terms 3 to 5 look ahead and term 6 does not**: the terrain and the river are those of
  the step being taken, the weather and the local speed those of the province the unit is
  standing in.
- **Infrastructure is no part of it.** In the original it sets the speed of a strategic
  redeployment and nothing of an ordinary march.

**The weather** in term 6 is a figure in thousandths, never above nothing
(`reversing/findings/FINDINGS-weather.md`, section 3): `confirmed`

    mud    = muddyness x MUDDYNESSMOVEMENTMODIFIER        only where frozen is exactly 0
    cold   = COLDMOVEMENTMODIFIER x (1000 - the country's winter_effects) / 1000
                                                           only where temperature < LANDLOWTEMPERATURETHRESHOLD
    weather = max(mud + cold, -989)

- **Frozen ground pays nothing for mud**, however little of it is frozen: it is a switch.
- **It is added to the province's `local_unit_speed`, not multiplied with it**: the two
  share one term.
- The floor of -989 is the original's own and not a define: the weather alone leaves a
  unit a ninetieth of its speed at worst.
- In the base game mud and cold each take three quarters, the cold from one degree of
  frost, and the strategic resource `fur` gives a country 0.15 of `winter_effects`.
- ~~**The weather itself is not built.**~~ Built 2026-10-15 (`weather.md`): a province's eight figures are made
  every hour, and this term reads them.

### Checked

`OpenHOI3.SaveCheck march <install> <earlier save> <later save>` takes two saves of one
game. Every division that has the same province and the same path in both, is under no
strategic redeployment and in no battle, is given the brigades, the fuel and the weather
the earlier save shows; what an hour adds here, times the hours between, is set against
what the saves say it gained. Where its weather or its fuel is not the same in both saves
the gain has only to lie between the two speeds. *Measured* against four saves of one
reference game made on 2026-10-09, in which an AI has some 480 of 950 divisions on
the move:

| saves | weather and fuel the same | changed in between |
| --- | --- | --- |
| 7 January 22:00 to 8 January 02:00, 4 hours | **301 of 301** to the thousandth | 73 of 73 between the two speeds |
| 7 January 14:00 to 22:00, 8 hours | 289 of 290 | 65 of 68 |
| 4 January 12:00 to 7 January 14:00, 74 hours | 151 of 167 | 32 of 40 |

- The longer the span the more often the weather was something else in between than at
  either end, which is all the misses are: the one of the eight hours' first column
  gained more than a muddy hour gives eight times over, having had some hours of dry
  ground between two muddy saves.
- It covers terms 2, 3, 5, 6 and 7 and the two cuts: infantry, cavalry, motorised and
  armoured divisions, garrisons and mountain troops, over plain edges and rivers, in mud,
  in frost and in neither, with fuel and without. The player's own division, the
  SS-Verfügungstruppe out of Berlin, gains 0.405 an hour in the saves and here: its
  brigades' 8 with the 0.1 Germany's technology adds, on dry plains, times 0.05.
- **`loaded`** after the saves loads the earlier save as a game (`savegame.md`) and takes each division as it
  loads - its own brigades, their technology, its leader and whether it is falling back - where without it a
  division is put together out of a new game's brigades and the start date's technology, which only a save of
  early 1936 answers. *Measured* 2026-11-14 on the war start of 6 September 1939 (`Ireland1939_09_06_18`, two
  reference runs saved hourly): seven single hours, 57 to 60 of 55 to 61 divisions an hour to the
  thousandth with it, 40 to 45 without - the misses without it Poland's retreating divisions, a tenth faster,
  and the 1939 brigades and technology. Those left with it are a Hungarian cavalry division in mountain mud
  (0.032 an hour in the saves, 0.072 here), a German division of a headquarters brigade alone (0.019, 0.150)
  and one single hour of a German infantry division, none of them read.
- **A unit that got no fuel on one day and some the next** showed the day's fuel arriving
  between 22:00 and 02:00: its four hours came to two at each speed.
- In every one of the 1,482 cases the next province of a moving division is one its own
  has an edge to, and **its progress is short of that edge's distance** - which is what
  section 6's arrival says it must be.
- **Not checked by it**: the hour a unit arrives and what it keeps; a strategic
  redeployment (74 divisions in the last pair of saves); and the route. The check does set
  the way found here beside each division's path, for information: 436 of 475 are the
  same. The rest are the AI's own orders, which may ask for one of the original's two
  careful finders or be given in stages, and a few are through a puppet's ground, which
  nothing here allows yet.

## 6. The hour

Each hour every unit with a path does this once with its speed added, and then again
without adding for as long as the last pass ended with the unit in a new province and its
progress already covering the next distance. So one hour can carry a fast unit through
several short provinces - a savegame shows an air unit through six (`fleetsandair.md`,
section 6). `confirmed`

**It comes before the unit's organisation, not after.** The original runs one routine for
every unit of every country in turn - the look at its order, its battles, and this march -
and only when all of them are done runs each unit's own hour, where organisation returns.
This document said the opposite until 2026-10-09: the reading it was written from named the
wrong caller, which the reading for fleets found.

1. A unit with no brigades does nothing.
2. **A unit that has not taken a step and has an attack delay running does nothing.**
   - A land unit going ashore from the sea onto ground an enemy holds that cannot fight waits
     the hour where a land unit of its country that can has a path ending there, and is
     halted otherwise (`transport.md`, section 8).
3. A path whose first province is the one the unit stands in loses that province, and the
   progress with it. Nothing more this hour.
4. **A land unit that was given no supplies at all does not walk onto ground an enemy
   holds**, unless it already has a battle for it: it is halted - and if it was falling
   back it is removed from the game.
5. `progress += speed`, when this is the pass that adds.
6. **In a battle the progress is held at the distance and the hour ends there**: a unit does
   not enter while it fights.
7. **The way has to be there still, every hour**: if the province the unit is in has no
   edge to the next, or the crossing has been closed, the unit is halted.
8. `progress < distance`: nothing more this hour. The **distance** is that of the edge from
   the province the unit is in to the next of its path - 1 where there is no such edge.
9. **It arrives**: the province comes off the path, `progress -= distance` - and the progress
   is nothing at all where an attack delay is running - and it is falling back no longer. If
   the unit may no longer stand there it is halted where it was. Otherwise the province it
   was in is remembered, it is in the next, and **a land unit looks at the province it has
   entered** (`landcombat.md`, section 1): that is where it meets an enemy, or takes the
   ground.
10. **The message that it has arrived**: when it is in no battle and its path is empty after
    the arrival - `UNITARRIVED_LAND`, `UNITARRIVED_NAVAL` or `UNITARRIVED_AIR` by its kind
    (`CUnit::AnnounceArrival`, `reversing/findings/FINDINGS-movement.md`, section 12; built 2026-11-14,
    `CArrival`): `confirmed`
    - **only for the player's own unit**; a land unit and a fleet wherever they arrive, **an air
      unit only at its own base**, and **never a fleet on patrol, escorting convoys or raiding
      them** - whose order sends them round and round. A strategic redeployment, a retreat and
      the AI's own orders for a delegated unit are told like any other;
    - about **the province it stands in**, both flags the player's, with the player's own values
      (`messages.md`, section 3) and then `MESSENGER` the general staff (`MESSAGE_HEAD_MARS`),
      `NAME` the unit's, `PROV` the province's and `TYPE` its kind's word - `ARMY`, `NAVY` or
      `EXP_SQUADRON`;
    - **its Goto** clears the selection, selects the unit and centres the map on it, and the
      popup closes (`messages.md`, section 4.1);
    - **a unit halted on arrival because it may no longer stand in the province ahead is told
      it has arrived too**, in the province it was halted in - the halt empties its path and the
      test comes after it. Not a land unit that found no fleet to board: that one leaves without
      a word;
    - **a land unit boarding a fleet** is told once the path is emptied, in the sea; and once
      more before, still on the shore, where what is left of its path after this step ends in
      that same sea.

    Their Finest Hour ships all three types with `log`, `popup` and `onmap` off, so a player
    sees nothing until he ticks them in the message settings. Checked on screen 2026-11-14 with
    the popup ticked: the 3. Panzer-Division sent from Berlin to Frankfurt an der Oder, "Army
    Arrived! ... our Army of 3. Panzer-Division has safely arrived in Frankfurt an der Oder",
    its line in the log, and its Goto closing the popup with the division selected.

A halt by the march itself - the way gone, the ground no longer its to stand on - also takes
the unit out of the battles it was attacking in.

**A unit's whole hour**, of which the march is the fourth step: the look at its order; for a
land unit with a path, the look at the province ahead; the battles it is no longer beside are
left and its attack delay is seen to; the march; and, with no path and no battle, the look at
the province it stands in. A unit of a country at peace that is going nowhere has nothing to
do in it.

**The units move one after another, country by country**, on the one thread that runs the
hour: the countries in their order, each country's units in the order of its own list -
the order they were made in, and on a load the save's tree walked children first, those made
since after them (`savegame.md`, section 5). `confirmed`: Germany's list read on the running
game at 21:00 on 16 July 1941 is that order unit for unit, all 253 loaded units
(`reversing/findings/FINDINGS-liveread.md`, section 95). It matters wherever one unit's hour
changes what another's finds: the 181. Infanterie-Division, 31st in Germany's list, looks at
its support attack on 1750 before the 81., 57. and 11. - 99th, 120th and 46th - start the
battle there, and so is given its way an hour later, as in both runs. Ours went by number
until then, which put it after them.

**How long a march takes** is not kept; it is worked out when asked: for each leg, what is
left of its distance over the speed the unit would have on it, rounded up to whole hours,
with what is left over carried into the next leg as the march itself carries it.

## 7. On the map

- A unit on the march is in its province's **moving** stack, which stands beside the
  standing one (`mapunits.md`, section 1).
- **Every moving unit has a small green pointer beside its counter**, pointing the way
  it is going, selected or not (`mapunits.md`, section 2).
- **The selected unit's way is drawn as an arrow**, and only a selected unit's. Read out
  of the original after a screenshot of it was given on 2026-10-09
  (`reversing/findings/FINDINGS-movement.md`, section 10): `confirmed` unless marked

  - it is the `moveArrow` `arrowType` of `interface/arrows.gfx`, whose picture is the arrow
    twice, one over the other: in outline above and filled below. In each the middle
    column is the shaft and the right half the head;
  - **its points** are the unit's own place - where its counter stands - and the unit
    point of every province of its path, the short way round the map;
  - **it is a smooth curve through all of them**: a Catmull-Rom spline, with a point added
    5 beyond each end in line with the end's leg for the curve to bend from, cut twenty
    times between each two points;
  - **the band is 4 map units wide** - the declaration's `size` is not where that comes
    from - laid across the curve at each cut, the direction across evened out with the cut
    before's from the second leg on;
  - **the head lies over the last 4 units**: a cut that near the end takes its column of
    the picture from the right half, further right the nearer it is;
  - **it is hollow ahead of the unit and filled behind it**: a cut is drawn from the filled
    picture when its place along the arrow - its number over the number of cuts - is not
    beyond the unit's progress over the distances of every leg of its path added up. The
    arrow is made again in each province the unit enters, so the fill starts again there.

  On the screenshot the shaft's two lines are 1 map unit apart and the head 3.3 wide and
  3.4 long, which is the picture's shaft and head on a band 4 wide.

  **Since 2026-11-07 the arrow is drawn through the install's own `arrow.fx`**, translated for
  the canvas, as the original feeds it (`reversing/findings/FINDINGS-movement.md`, section 11),
  `confirmed`: a strip of two vertices a cut, the left edge with the picture's v nought and the
  right with v a half; the first set of coordinates a cut's number over the number of cuts -
  never one - and the second its column and v; `CurrentProgress` the share of the way walked,
  and the effect filling every cut up to the first not yet reached. **The picture is the type's
  `bodytexture`**, not its `textureFile`, which the original loads and never draws, and **every
  arrow type draws through the first type's `effect`**, the original keeping them all under one
  key; `size`, `endAt`, `height`, `type`, `heading` and the two colours do nothing. Three rules
  came with it: **a point within a tenth of the next on both axes is left out, and so is the
  next** - the points beyond the ends counted - where a point was dropped by its distance alone;
  **only the last 119 cuts are weighed for the head**, so a way that comes back near its end
  keeps its shaft there; and **the band stops at its last cut**, a twentieth of the last leg
  short of the end, so a long last leg cuts the head's tip by about two pixels - this project
  had made one more cut at the very end, which the reading shows the original does not.
  *Ours:* the arrow lies on the canvas where the 2D view puts it, where the original's stands
  0.48 over land and 0.78 at sea, flat; and it is drawn at three copies of the map, the
  original's at two.
- **An attack and a retreat have arrows of their own**, drawn the same way with another
  picture: `retreatArrow` for a unit falling back, which is asked first, and `attackArrow`
  **where an enemy of the unit's country holds the next province of its way** - whether or
  not anybody stands there, so a walk into undefended enemy ground is drawn as an attack.
  `confirmed` (`reversing/findings/FINDINGS-combatview.md`, section 9); the attack's was set
  beside a screenshot of one on 2026-10-09 (`combatview.md`, section 9). A land
  unit going ashore from the sea, on anybody's ground, has `unitInvasionArrow`, asked after the
  retreat's (`transport.md`, section 12). **A unit under a strategic redeployment has
  `stratArrow`**, asked after the attack's and before the march's (`reversing/findings/FINDINGS-movement.md`,
  the arrow's reading; built 2026-11-04 with the land orders window, `landorders.md`, on an
  account of the original that its colour differs there) - its picture, `stratarrow.tga`,
  is the colour.
- **The click leaves a mark on the province ordered**: `legal_selection_projection` where the
  unit was sent - a blue ring - and `illegal_selection_projection` where it was refused - a red
  cross. Read out of the original on 2026-11-14 (`reversing/findings/FINDINGS-movement.md`, section
  13), `confirmed` unless marked; built the same day and set on screen with `--click` (below):

  - **what it is**: a `projectionType` of `interface/mapitems.gfx` [mod], ten keys with the
    original's defaults for what a declaration leaves out - `size` 1, `pulseLowest` 0.4,
    `pulseSpeed` 3, the rest nought - drawn through **`gfx/FX/project.fx`, whatever the
    declaration says** [exe: the file and that it has no key for one; mod: the file's content],
    which lays the picture flat on the map from above (`CProjectionType`, `CProjectionTypes`);
  - **where**: where the unit would stand in that province - the province's unit point
    [mod: `map/positions.txt`], a fleet's naval base in a land province, an air unit's air base
    where it is at its own base there - one mark for each selected unit the click is given to;
    only a selected unit leaves one [exe];
  - **which** (`reversing/findings/FINDINGS-movement.md`, section 13.2): a land unit sent, the
    sent mark with `army_move`, refused, the refused mark with `error`; halted where it stands,
    none. A headquarters under its own plan, the sent mark with an objective or an axis, and
    **neither the mark nor the sound where no way leads to the axis**. A fleet sent straight there,
    the sent mark, or the refused mark where its range refuses it; **a province no sea of its
    reaches, the refused mark and `error`, with Shift as without** - the sea's test is asked before
    anything else - unless the coast opens the orders window for what it carries. An air unit in a
    battle, sent straight on, the sent mark, never the refused one. The orders window, opened, leaves
    none;
  - **how it plays out** [mod: the declaration's figures, exe: the rules], on the process's clock
    and not the game's, so a paused game's marks play out too: it grows from 0.1 of a unit to
    `size` over `expanding` seconds at full alpha; then, where `pulsating`, its alpha is
    `pulseLowest + (1 − pulseLowest) × (sin((t − t₀) × 3.14 × pulseSpeed) + 1) / 2`, t₀ the last
    frame of growing; after `duration` seconds that pulse is faded linearly to nothing over
    `fadeout` seconds - **pulsing whether or not the type does** - and the mark is gone, at once
    where `fadeout` is nought; with no `duration` it stays. It turns `spin` × 3.14 radians a
    second. The install's two: `size` 4, `expanding` 0.3, `pulseLowest` 0.8, `pulseSpeed` 1,
    `duration` 1, `fadeout` 1, no spin - grown in 0.3 s, gone at 2 s (`CProjectionMark`);
  - **how it is drawn** (`reversing/findings/FINDINGS-movement.md`, section 13.4) [exe]: the picture
    covers a square `size` map units a side about the mark, north up, turned by its angle - a look
    straight down from 50 above at a point 49 above, an orthographic view of the square from 1 to
    70 and the original's bias into the picture's coordinates, as `TextureMatrix`. **On a land
    province it is laid on that province's own triangles of the quad cache, so it is cut to the
    province's shape**; on a sea zone on a square 20 units a side about the mark, whose sea anchor
    stands at 0.3. Blended by alpha - or added, where `additative` - and drawn over everything
    without the depth test, as the original's map pictures are before the counters
    (`CProjectionMarks`, `CMapSurface.AddMark`).

  *Ours:* the mark is drawn on the map's 3D surface alone - with no quad cache the map is flat and
  shows no mark; a land unit's mark also follows the command's own refusal, which the original's
  click does not ask. **Checked with `--click` on 2026-11-14**: a division of Potsdam sent to Zerbst
  leaves a blue ring at Zerbst's unit point, about the size of a counter, with the way's arrow and
  the panel's "Move to" as the positive control; one of Köslin sent into the Southern Baltic a red
  cross at the sea zone's point, the panel saying "No Order". Nobody has set it beside the original's
  on screen.

  **`selection_projection`, the mark under a selected unit, is the figures' alone, and so is not
  drawn here** (`reversing/findings/FINDINGS-movement.md`, section 13.8, read 2026-11-14, `confirmed`
  unless marked). The original makes it as a unit is selected **only where the player's
  `alwaysCounters` is off** [exe; mod: `settings.txt`] - with counters, the install's setting and the
  only one this project has (`docs/compatibility.md`, entry 12), there is none, and a selected counter
  is marked by its ring alone (`mapunits.md`, section 8). What it is without counters, for the record:
  one for every selected unit, of every kind, hung on its province and lying at its stack's point - the
  standing, the moving, a battle's or the aircraft's - hidden while a fleet is in port or an air unit
  at its base; it follows the unit from province to province and between the standing and the moving
  stack as it marches, and goes when the unit is deselected. The install's type [mod]: `size` 5,
  `expanding` 0.7, `spin` 0.4, not pulsing and with no `duration`, so by the rules above it grows to 5
  units in 0.7 s, turns 0.4 × 3.14 radians a second and never fades. Nothing of it is built: building
  it would draw what the original never draws with counters. *Inferred*: which of its maker's branches
  hide it on entering a province, by the avatar's matching ones.
- **The unit panel** (`unitpanel.md`) says what a selected unit is doing - "Move to" the
  next province of its way - shows its speed, marks it as on the march, and has the
  button that halts it.
- The readout beside the panel names where the selected unit's way ends and when it will
  be there. The original says that in a tooltip; the readout is scaffolding and goes when
  there are tooltips. It is the readout of a selected province, and is not there without
  one: select the province, then the unit.

## What is not built

- ~~**A unit too weak is not routed onto an enemy's ground** in the original (one with no
  supplies or no fuel is not, here either: section 3). Nor is a country kept from
  attacking out of a neutral that merely lets it through.~~ Both built 2026-11-14 (section 3),
  and the click's own tests of a land unit sent onto an enemy's ground (section 1). ~~The rest of
  the click's test of a province - a unit in a battle sent only where it may fall back, and none
  while it has a battle in the next province of its path - is not.~~ Built 2026-11-14 with the land
  unit at sea and the fleet's sea connection (section 1). **The AI's `MoveUnit` asking it** is not built:
  the AI's moving's to add.
- ~~**Coordinated arrival**: the original keeps two hours on a unit, one before which it does
  not move and one before which it does not arrive, waiting at full progress.~~ Built
  2026-11-04 with the land orders window (`landorders.md`, section 5).
- ~~**Strategic redeployment**, which is an order of its own with a speed of its own.~~ Built,
  and given by hand since 2026-11-04 (`landorders.md`, section 8).
- **The weather itself**: what it does to a march is in (section 5), what makes it is not.
  A leader's part in the speed is built (2026-10-11), and the march check against the saves
  hours apart still gives 301 of 301.
- ~~**Walking on a puppet's, an overlord's or an ally's ground, or with military access.**~~
  Built 2026-11-11, with every other friend test the original's (section 3).
- ~~**The message** that a unit has arrived~~ - built 2026-11-14 (section 6) - ~~and the sounds
  and the marks of the click~~ - built, the marks 2026-11-14 (section 7). ~~**Left of the click**: an air
  unit in a battle the original sends straight on with the sent mark and nothing asked, where this
  project opens the orders window for every air unit (`sound.md`, section 7).~~ Built 2026-11-14 (section 1;
  `fleetsandair.md`, section 4). ~~And `selection_projection`~~ -
  read 2026-11-14 and the original's only without counters, so not drawn here (section 7).
- **The tooltip** that says when a unit will arrive.

## What is not established, in one place

- how the original's arrow comes to a whole tip, its last cut being short of the end;
- where the **moving stack** stands (`mapunits.md`);
- where a **port** is when `positions.txt` does not place it;
- one flag every province carries, set on all of them, that closes a province to everything
  when clear: what it is.
