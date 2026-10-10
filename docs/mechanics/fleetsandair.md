# Fleets and air units on the move

How a fleet and an air unit are sent somewhere: where they may go, how far, the window that
asks what they are to do there, and the orders that take them. Read out of the original on
2026-10-09: `reversing/findings/FINDINGS-seaair.md`, on top of
`reversing/findings/FINDINGS-movement.md`. `confirmed` means read; *ours* means decided here.
Section 3's table is also **checked against the original's own cache, cell for cell**, and
sections 1 and 6 **against savegames an hour apart**.

**In one line.** A fleet and an air unit go over the same provinces as a division, by the
same route finder and the same hourly march (`movement.md`), at their own speeds. What is
their own is how they are told to: a right click brings up **a window of orders** for the
province, one of which is ticked and accepted, and the unit then holds **an order** that sets
it on its way and is looked at again every hour. A fleet goes between land and sea only
through **a port**, and only as far from its base as its range, measured **by sea**; an air
unit's range is a straight line.

---

## 1. Where they may go

The route and the march are `movement.md`'s, sections 3 to 6. What a fleet and an air unit
add to "where a unit may go": `confirmed`

**A fleet may be in**

- any **sea province**, but for a closed water (below);
- a **land province** only where it has an owner, **a port** (section 2) and a naval base,
  and whoever holds it is no enemy of the fleet's country and is counted a friend by it.

**A fleet takes a step**

- never from land to land;
- **between a port and the sea only through the one sea province the port opens onto**,
  whatever else the land province borders;
- out of a port it may no longer lie in - one that has changed hands - always.

**A fleet is sent somewhere** only where there is sea at both ends - the province itself, or
the sea its port opens onto - and not while something it carries is on the move itself.

**The five waters a country can close.** The defines' `map` section names five pairs, a sea
province and a land one: `SUEZ`, `PANAMA`, `BALTIC`, `BLACKSEA` and `GIBRALTAR`, each with its
`_BLOCKER`. **A fleet may not be in one of the five sea provinces while an enemy of its
country holds that water's blocker.** So a canal or a strait is closed by holding one named
province, and to enemies only.

**An air unit** has no rule of its own for where it may be: it flies over the sea, over its
own country's ground and a friend's, and over ground at war with it - and **not over a
neutral's**. It is not sent to a province whose sky is as much cloud as
`ALLOWEDTOFLYTHRESHOLD`; nothing here makes weather yet, so no sky is.

*Ours:* a friend is the country itself and the members of its faction (`movement.md`,
section 3). The original also asks a province's owner area one thing that was not read, and
before any search asks whether the two ends lie in seas that closed waters cut off from each
other - the Black Sea, the Baltic, the Mediterranean - which only saves a search that would
fail anyway and is not built.

## 2. Ports

**A land province has a port where the pixel its `naval_base` position falls on belongs to a
sea province that is not a lake.** That sea province is the one its ships put out into.
`confirmed`, and checked - section 3.

- The position is `positions.txt`'s `building_position = { naval_base = { x y } }`, cut to
  whole pixels. A position on land makes no port, however near the water: the original looks
  at three pixels beside it and then at the position again, and keeps only the last look.
- **A lake** is a sea province none of whose neighbours is sea, or one `continent.txt` lists
  under the continent `lake`.
- A province with a naval base and no port can hold no fleet. The base game gives every
  coastal province a position in the water.

## 3. Range, and the distances by sea

A unit's **range** is the shortest `range` among its ships or wings, with their
technologies. **A range is in quarters of a pixel of the map.** `confirmed`

**An order is out of range** where the province is further from **the unit's base** than its
range. Not from where the unit is. Two things are never out of range: **a change of base**,
and anything for a unit that has no base.

- **An air unit's distance is a straight line**: four times the pixels between the centres of
  the two provinces' bounding boxes, the short way round the map, rounded down. Its reach is
  a circle about its base.
- **A fleet's distance is by sea**, out of a table:

      the places of the table are the ports, and the sea provinces that are not lakes
      two places are joined where their provinces have an edge
      a step is as long as the straight line above between the two provinces
      the distance is the shortest sum of steps, kept in 16 bits

  A land province with no port is looked up by nothing and is **no distance** from anywhere;
  so is a pair of places with no way between them. No distance is always in range.
- **A port is a place of the table like a sea province**, joined to every sea province and
  every port it borders - so a way "by sea" may cross a neck of land where two ports touch.

The places are numbered: the ports first, in the order `positions.txt` names them, then the
sea provinces by id. The order only matters to the file below.

### Checked

The original works the table out once and keeps it in `map/cache/navaldist.bin`, which the
base game ships: the half of the table on and above its diagonal, row by row.
`OpenHOI3.SaveCheck navaldist <install>` builds it from the map files by the rules above and
compares (2026-10-09, Their Finest Hour):

| | |
| --- | --- |
| places | 5,936 here and there: 2,406 ports and 3,530 sea provinces; 17 lakes left out |
| distances | **17,621,016 of 17,621,016 agree** |
| pairs with no way between them | 23,735 |

*Ours:* a row of the table is worked out the first time a fleet based there asks, about four
thousandths of a second, and kept for the game; the whole table takes twenty seconds and is
only ever made by the check. Nothing is cached on disk.

## 4. The window of orders

**A right click with a fleet or an air unit selected does not send it: it asks.** `confirmed`

| | |
| --- | --- |
| a fleet, plain click | the window `naval_unit_order` for the province |
| a fleet, **shift** held | it is sent straight there, if the province is in range: a move with no order |
| a fleet under way, clicked where it is | it halts |
| a fleet, a province it cannot reach by sea | nothing, and the error sound |
| an air unit, shift or none | the window `air_unit_order` |

The window is headed with **the province's name** and lists orders, one to a row - an
`order_entry` with a box and a name. **One box is ticked at a time**; `accept` gives that
order to the selected unit and closes the window, `cancel` and Escape only close it, and
`accept` cannot be pressed while nothing is ticked.

**A fleet's list is every naval order there is**, always, in this order - and a row whose
order cannot be given has its box greyed:

    Move Order, Rebase, Reserve, Patrol, Intercept, Sortie, Convoy Raiding, Transport, Invasion
    and an escort order for each of the player's convoys through the province

**An air unit's list is only what its wings could ever fly**, by what they can attack and
whether the province is land:

| rows | shown where |
| --- | --- |
| Air superiority, Air Intercept | a wing has `air_attack` |
| Air Supply | land, and a wing has `transport_capability` |
| Strategic Bombing, Logistical Bombing, Runway Cratering, Installation Strike | land, and `strategic_attack` |
| Ground Attack, Interdiction | land, and `soft_attack` or `hard_attack` |
| Port Strike | land, and `sea_attack` |
| Naval Strike, Convoy Raiding | sea, and `sea_attack` |
| Reserve, **Rebase** | land |

**An air unit has no plain move.** It goes somewhere to stay only by changing its base.

**The row ticked when the window comes up**: the change of base, where the unit may be in
the province, whoever holds it is not at war with the unit's country, it has the base for it
and the order can be given; for a fleet otherwise the move, where that can be given; and
otherwise none. An air unit has more - below.

Under the list are the **stance** - Aggressive, Defensive, Passive, the first to start with -
and the **priority**, 0 to 4, each with a button either side that steps it and goes round at
the ends. They belong to the orders that fight.

**While the window is up, a right click on the map is the window's**: the naval one does
nothing with it, the air one aims or moves its area (below). `confirmed`

### The air window

`reversing/findings/FINDINGS-airorders.md`, read on 2026-10-11. `confirmed` throughout but
where a line says otherwise.

**What it opens with.** The **first** air window of a session: priority 0, the aggressive
stance, the **point** shape. **Every later one: the last one's priority, stance and shape.**
"Both" is ticked and day and night clear, "continuous" is ticked, the start date is the hour
now and the end a month on (the day of the month kept, so 31 January runs on to 3 March), and
the three sliders stand at 0 for the range, 0.016 of a turn for the angle and 0.25 for the width
- the `.gui` file's own starting values are not used.

**The row ticked** is, in order: the change of base as above; otherwise the order **kept** -
the last window's, or for a session's first window the unit's own - where its row can be
given; otherwise a ladder, by what the unit's wings are - a wing that is no bomber is a
fighter, a bomber whose strategic attack is above half its soft and hard attack together a
strategic bomber, any other bomber a tactical one:

    fighters outnumber each kind of bomber: air superiority over an enemy's province,
        otherwise the intercept
    then, whatever that ticked, a wing with sea attack: the port strike at an owned
        province with a port and a naval base, otherwise the naval strike at sea
    then, where nothing is ticked, more strategic bombers than tactical, over a province
        at war with the unit's country: runway cratering where it has an air base, the
        installation strike where it has a radar station, a reactor or a rocket site,
        otherwise strategic bombing where it makes IC
    then, where nothing is ticked, a soft or hard attack: ground attack, otherwise interdiction

each only where its row can be given. **The second rung asks nothing of what the first
ticked and replaces it.**

**The area.** The ticked order has a list of provinces, made by one of four shapes - point,
region, circle, cone - with the three sliders and the right click: `aircombat.md`, section 2,
has how. A shape's button is drawn pressed; **the range slider works for the circle and the
cone, the angle and width sliders for the cone alone**, and the others are locked.

**A slider is taken only when it has moved by more than a twentieth of where it now is** from
the value taken last. The range is the slider's share of how far the unit can fly, the angle
and width its share of a turn. The three figures beside them are the values taken -
`ORDER_RANGE_VAL` to the whole, `ORDER_ANGLE_VAL` and `ORDER_WIDTH_VAL` in degrees to two
places - **and not what a right click gave the area**.

**Day, night and both act as one choice**: ticking one clears the other two and sets the hours
the mission flies - but **clearing the ticked box leaves none ticked and the hours as they
were**. Under them, `ORDER_DAY_TIME` with the province ("Locally in Poznan:") and whether it is
night there at the start date's hour and month, as a picture and a word.

**The dates**: two `datewidget`s, at the `positionType`s `mission_timewidget_start` and
`_stop`, each with an arrow up and down for the year (8,760 hours), the month, the day and
the hour, **held between the defines' `start_date` and `end_date`**. While "continuous" is
ticked their arrows are switched off and the mission ends on the defines' `end_date`;
otherwise it flies from the first date to the second. Before its start an air mission is not
fit to fly (`aircombat.md`, section 1).

**`second_target`** reads `ORDER_NO_SECOND_TARGET` - "Right-click on the map to select a
second target province." - whenever accept cannot be pressed, and is left alone when it can:
**so it is empty until the first time accept is greyed, and the hint from then on.** The
original never shows `ORDER_SECOND_TARGET`: what it would name is never set.

**Accept** can be pressed with a row ticked and a province on the area's list. It gives the
order with the list, the stance, the hours, the start and the end - **and not the priority,
which the air window shows, steps and remembers and never sends** - unless the unit's own
order is a change of base still under way, which is left to finish. The window then closes.

**The map while it is up** is the window's, whatever the mode, and comes back as the mode
draws it when the window closes:

    out of the unit's range of its base        nothing
    sea, or land the unit's country holds      (0.6, 0.9, 0.6)
    land of a country at war with it           (0.9, 0.6, 0.6)
    other land                                 (0.6, 0.6, 0.6)
    on the area's list, and in range           (0.5, 0.8, 0.8)

*Ours:* **"nothing" is drawn as the terrain mode draws the province** - how the original's map
draws a colour with no alpha was not read. **The window's title is not written again** when a
right click moves it to another province, which is the original's. There is no tooltip
anywhere, the paradrop's earliest start is not kept (there is no paradrop), and a click at
the base province of an air unit cannot pick it out from under the land units there:
`--select=` does it for a trial.

*Ours:* **a fleet's move, its change of base and its six missions are built** - the missions
are `navalmissions.md` - **and an air unit's change of base, its two fighting missions
(`aircombat.md`) and its eight bombing missions (`bombing.md`).** Every other row is listed as
the original lists it and cannot be ticked: a fleet's transport and invasion, and an air
unit's reserve, supply flight and raid on convoys.
An air unit's rows for a paradrop, a carrier and a bomb are left out, wanting what is not built
to know when they show. The stance and the priority go with a fleet's order and mean something
to its missions. In the original a row and `accept` say in a tooltip why they cannot be used.

## 5. The orders

A unit holds one order, or none. `CSetUnitOrderCommand(country, unit, order, province)`
gives one; it replaces the one the unit had and at once sets the unit on its way. `confirmed`

| | may be given where | giving it |
| --- | --- | --- |
| **move** (a fleet) | the province is in range, and the fleet may be sent there (section 1) | the way is found; with none, the fleet keeps the path it had |
| **rebase** (a fleet) | the province has a naval base, the fleet may be sent there, and whoever holds it counts the fleet's country a friend | the way is found, and **with none the fleet's path is emptied** |
| **rebase** (an air unit) | the province is land with an air base, held by someone not at war with the unit's country who counts it a friend | the way is found; with none, nothing |

A change of base asks nothing of the range, and an air unit's not even whether the unit may
fly there.

**Every hour, before the unit moves, its order is looked at**, and dropped when it is done:

    move      the unit is in the province, or has no path left          -> done
    rebase    the unit has no base                                       -> done
              the province becomes the unit's base, if it is not
              the unit is in the province, or has no path left           -> done
    rebase    the unit has no base                                       -> done
    (air)     the province becomes the unit's base, if it is not; a unit
              based on a carrier leaves the carrier
              the unit is in the province: every wing's organisation is
              halved                                                     -> done
              it has no path: the way is looked for again; still none    -> done

- **The base changes the first hour the order is looked at, not on arrival.** A savegame
  shows it: an air unit seventeen provinces short of its new airfield already has it for its
  base. So the range of a unit under way is already measured from where it is going.
- **An air unit lands at a new base with half its organisation**, each wing's cut to the
  thousandth.
- The look comes before the hour's march, so a unit that arrives is still under its order
  until the hour after.
- A fleet sent straight somewhere with shift, and any unit halted, has no order afterwards.

*Ours:* an order starts the hour it is given. The original keeps a date on a unit before
which it does not move, which the window sets to the hour it was opened.

## 6. Speed and the march

`movement.md`, sections 5 and 6, with these terms: the unit's top speed is the lowest
`maximum_speed` among its ships or wings; **a fleet that burns fuel** keeps a tenth of its
speed with none and all of it with its whole ration; the terrain and river terms find
nothing on a ship or a wing; and none of the land terms apply. The result is scaled by
`NAVAL_SPEED_MODIFIER`, 0.5 in the base game, or `AIR_SPEED_MODIFIER`, 0.3. `confirmed`

A wing at 200 covers 60 an hour and most edges are under 15, so **an air unit goes through
several provinces in an hour**: the hour's speed is added once, and the unit enters province
after province for as long as what it has left covers the next distance.

### Checked

`OpenHOI3.SaveCheck march <install> <earlier save> <later save>` gives every fleet and air
unit that has a path in the earlier save its ships or wings, its fuel, its place, its path
and its progress, moves it here hour by hour for the hours between, and sets it against the
later save. *Measured* against saves of one game the maintainer made on 2026-10-09 with a
submarine flotilla sailing from Kiel for Königsberg and an air fleet changing its base to
Königsberg, the AI having some fifteen more fleets under way:

| saves | the province it is in and the path left | progress along the next edge |
| --- | --- | --- |
| 8 January 06:00 to 07:00 | 17 of 17 fleets, 1 of 1 air units | 17 of 17, 1 of 1, to the thousandth |
| 8 January 03:00 to 06:00 | 17 of 17 fleets | 17 of 17 |
| 7 January 22:00 to 8 January 02:00 | 12 of 12 fleets | 12 of 12 |

- The maintainer's submarines gain 5.75 an hour: 11.5 with their technologies, times a half.
- **The air unit crosses six provinces in its hour** and ends 6.7 along the seventh edge,
  here and in the save: the carrying over of section 6 of `movement.md`, which had only been
  read.
- **The route**: for all 17 fleets and the air unit the way found here from where they are
  to where they are going is the path the save has - section 1's rules, the ports and the
  order of the edges together. The air unit's seventeen provinces run over land and sea.
- **Range**, for information: 14 of the 17 fleets are bound for a province within their
  range of their base. The three that are not are transport flotillas of the AI's, under
  orders that were not read.

## 7. On the map

- **A fleet in port and an air unit at its own base have no counter.** They are seen, and
  picked, by the province's port and airfield: `mapunits.md`, section 9. At sea or in the air
  they have a counter like any unit, and the air units of a province stack by themselves.
- **A selected unit's way is drawn** as a land unit's is (`movement.md`, section 7), with
  two differences: a fleet's arrow starts at the port where the fleet is in one, and ends at
  the port where its way ends in a land province. `confirmed`
- **The unit panel** names the order and the province it is for, the unit's base with its
  range, and counts a fleet's ships and an air unit's wings: `unitpanel.md`.

## What is not built

- **A fleet's transport and invasion, and an air unit's reserve, supply flight and raid on
  convoys.** The air window's other controls are built since 2026-10-11. A fleet's reserve, patrol, intercept,
  sortie, convoy raiding and escort are built since 2026-10-10 and are `navalmissions.md`;
  an air unit's air superiority and intercept since the same day, and are `aircombat.md`;
  and its eight bombing missions since the same day, and are `bombing.md`.
- **Carrying**: troops aboard a fleet, an air group aboard a carrier, a paradrop. A fleet
  with troops aboard is offered a land province to put them ashore in; not here.
- **A fleet meeting another** is built since 2026-10-10 and is `navalcombat.md`. One thing
  of it belongs here: **a fleet in a battle that is sent somewhere is not given its way** -
  the province is kept, and it sets off four hours on (section 6 there); and a right click
  with such a fleet selected sends it without asking, as shift does, which is the
  original's.
- **An enemy otherwise**: a crossing closed by an enemy fleet, a base lost to
  an enemy - the original gives a fleet whose base an enemy takes the nearest of its
  country's by sea, then of its faction's, and removes it where its country has no port
  left; and gives an air unit with no base the nearest of its country's.
- **An air unit sent home**: away from its base and worn down, the original orders it back.
- **Several units selected** given one order together.
- **The window's tooltips**, the sounds of the click, and the mark it leaves on the province.
- **The land window**, `land_unit_order`, which a land unit's click brings up under a key
  that was not identified.

## What is not established

- how the original shows **a row that cannot be ticked**: here the box is greyed as any
  button that cannot be pressed is;
- which **order the shift click's range is measured under** - taken to be the unit's own;
- a naval order's own part in "still going", which reads three fields that were not named;
- what the **owner area** of a province is asked;
- whether the window keeps its place when it is dragged: it is declared movable, and here it
  stands where the file puts it.
