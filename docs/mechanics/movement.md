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
`fleetsandair.md`. Everything that has to do with an enemy is not built, and the last
sections say what.

---

## 1. The order

- **A right click on a province with a unit selected** sends the unit there. With **shift**
  held the order is added to the path the unit already has; without, it replaces it.
  `confirmed`: the original's routine for the click takes the province and one flag, and
  passes the flag's opposite to the command as `clear`. That the flag is the shift key is
  the maintainer's word, 2026-10-09: "Yes it is shift."
- A click on **the province the unit stands in** halts it - unless the order is being
  added to a path, when it is one more leg. `confirmed`
- A click on a province the unit **may not go to** does nothing but play the error sound
  and mark the province as refused. `confirmed`; *ours:* nothing is played or marked yet.
- Nothing happens for a unit with no brigades. `confirmed`. The original also refuses a
  retreating unit and one under an AI's command.
- **That is a land unit's click.** A fleet's and an air unit's bring up a window of orders,
  and only a fleet with shift held is sent straight there: `fleetsandair.md`, section 4.

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
  where the country knows of enemy units standing there. *Ours:* the tenth; knowing of
  units waits for intelligence.
- The route handed back leaves out where it starts and ends with where it goes.

### Where a unit may go

Asked of the destination before anything, and of every step: `confirmed` unless marked

- **not over an impassable edge**;
- **not over a crossing that is closed**: one through a land province an enemy holds, or
  through a sea province an enemy fleet stands in. An air unit is never stopped. *Ours:*
  the first; the second waits for fleets that fight;
- **only into a province the unit may stand in**: one held by whoever holds the ground
  under it now, or one its country may walk on.

**Whose ground a country may walk on:** its own and a friend's, ground that belongs to
nobody, and the ground of anyone it is at war with. In full, ground is closed only when all
of these hold: the country is at war with neither the province's owner nor its controller;
the province is land; its controller is no friend; and its owner is no friend either, or is
at war with the controller.

A **friend**, in the original, is the country itself, its overlord or its puppet, an ally
or a member of its faction, a co-belligerent, and a country it has military access to.
*Ours:* the country itself and the members of its faction; the rest are diplomacy, which
is stage 4.

A land unit may not stand in the sea. *Ours:* in the original it may where a fleet of its
country waits there with room to carry it, which is how it is put aboard.

## 4. Setting off

Giving a unit a path: `confirmed`

- **Replacing** the path it has: what the unit has already walked is kept **only where the
  new path begins with the province the old one began with**. Otherwise its progress goes
  back to nothing.
- **Adding** to it: the new provinces go on the end and nothing else changes.
- **Halting**: the path is emptied and the progress lost.

A unit keeps, besides its path and its progress, **the province it was in before** the one
it is in.

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
| 1 | in a battle: a figure of the battle and of `combat_movement_speed`, between 10 and 1000 | not built |
| 2 | **fuel**, for a land unit or a fleet whose brigades burn any: `fuel received x 900 / 1000 + 100` | built |
| 3 | **the next province's terrain**: 1000 + the unit's adjuster for it | built |
| 4 | a leader's trait for that terrain | not built |
| 5 | **a river or a crossing** on the way to the next province: 1000 + the unit's adjuster for `river` | built |
| 6 | on land, **the province it is in**: 1000 + the weather there + its `local_unit_speed` | built |
| 7 | on land: 1000 + the country's `amm_movement_speed` x the share of its brigades that are `is_mobile` | built |
| 8 | retreating: 1100 | not built |

- **Fuel**: a unit that received none keeps a tenth of its speed, and one that received all
  of its ration keeps all of it.
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
- **The weather itself is not built.** A province holds the eight figures a savegame
  writes for it - humidity, pressure, wind, temperature, precipitation, mud, frost and
  cloud - and nothing changes them, so in a game here every one is nothing and the term
  is 1000 plus the local speed. The rule is in so that a march is right the day the
  weather is.

### Checked

`OpenHOI3.SaveCheck march <install> <earlier save> <later save>` takes two saves of one
game. Every division that has the same province and the same path in both, is under no
strategic redeployment and in no battle, is given the brigades, the fuel and the weather
the earlier save shows; what an hour adds here, times the hours between, is set against
what the saves say it gained. Where its weather or its fuel is not the same in both saves
the gain has only to lie between the two speeds. *Measured* against four saves of one
game the maintainer made on 2026-10-09, in which an AI has some 480 of 950 divisions on
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
  in frost and in neither, with fuel and without. The maintainer's own division, the
  SS-Verfügungstruppe out of Berlin, gains 0.405 an hour in the saves and here: its
  brigades' 8 with the 0.1 Germany's technology adds, on dry plains, times 0.05.
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
2. A path whose first province is the one the unit stands in loses that province, and the
   progress with it. Nothing more this hour.
3. `progress += speed`, when this is the pass that adds.
4. **The way has to be there still, every hour**: if the province the unit is in has no
   edge to the next, or the crossing has been closed, the unit is halted.
5. `progress < distance`: nothing more this hour. The **distance** is that of the edge from
   the province the unit is in to the next of its path - 1 where there is no such edge.
6. **It arrives**: the province comes off the path, `progress -= distance`, and if the unit
   may no longer stand there it is halted where it was. Otherwise the province it was in is
   remembered and it is in the next.

**The units move one after another, country by country**, on the one thread that runs the
hour: the countries in their order, each country's units in the order of its own list.
`confirmed`. *Ours:* a country's units go in the order of their numbers; the order of the
original's list was not read, and none matters until two units' marches can affect each
other.

**How long a march takes** is not kept; it is worked out when asked: for each leg, what is
left of its distance over the speed the unit would have on it, rounded up to whole hours,
with what is left over carried into the next leg as the march itself carries it.

## 7. On the map

- A unit on the march is in its province's **moving** stack, which stands beside the
  standing one (`mapunits.md`, section 1).
- **Every moving unit has a small green pointer beside its counter**, pointing the way
  it is going, selected or not (`mapunits.md`, section 2).
- **The selected unit's way is drawn as an arrow**, and only a selected unit's. Read out
  of the original after the maintainer gave a screenshot of it on 2026-10-09
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
  3.4 long, which is the picture's shaft and head on a band 4 wide. *Ours:* one more cut is
  made at the very end, so that the tip is always whole; the original's last cut is a
  twentieth of a leg short and its tip is whole on the screenshot all the same.
  The original has other arrows for an attack, a retreat, a strategic redeployment and a
  landing, which wait for those.
- **The unit panel** (`unitpanel.md`) says what a selected unit is doing - "Move to" the
  next province of its way - shows its speed, marks it as on the march, and has the
  button that halts it.
- The readout beside the panel names where the selected unit's way ends and when it will
  be there. The original says that in a tooltip; the readout is scaffolding and goes when
  there are tooltips.

## What is not built

- **Anything to do with an enemy.** In the original a unit does not set off while an
  attack delay runs; a unit in a battle has its progress held at the distance and enters
  only when the battle is over; entering a province looks for a battle to start; a unit
  with no supplies will not march onto enemy ground, nor one too weak to; a unit leaving a
  battle it was losing is retreating, a tenth faster.
- **Coordinated arrival**: the original keeps two hours on a unit, one before which it does
  not move and one before which it does not arrive, waiting at full progress.
- **Strategic redeployment**, which is an order of its own with a speed of its own.
- **Landing from the sea** and being carried.
- **A leader's** part in the speed, and **the weather itself**: what it does to a march is
  in (section 5), what makes it is not.
- **Walking on a puppet's, an overlord's or an ally's ground, or with military access.**
  A friend here is the country itself or one of its own faction.
- **The message** that a unit has arrived, and the sounds and the marks of the click.
- **The tooltip** that says when a unit will arrive.

## What is not established, in one place

- how the original's arrow comes to a whole tip, its last cut being short of the end;
- where the **moving stack** stands (`mapunits.md`);
- where a **port** is when `positions.txt` does not place it;
- one flag every province carries, set on all of them, that closes a province to everything
  when clear: what it is.
