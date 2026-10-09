# OpenHOI3 - a drop-in engine replacement for Hearts of Iron 3

## Goal

The goal is a **drop-in replacement for the engine of Hearts of Iron 3: Their Finest
Hour**: pointed at an install, it plays that game - its content and its mods as they are
written, by the original's own rules, as close to the original as we can get - on new
technology.

So it is a reimplementation of what the original *does*, and not of how its executable
is built. The rules of the simulation are taken from what the reverse engineering has
found the original to compute - formulas, orderings, rounding, caps - and checked against
its savegames wherever one can be. The engine, the rendering and every line of code are
our own, and no address, offset or byte of the original's binary is used here. What is
read from an install is the **language its content is written in**: the
script, the data files and the interface files of a Hearts of Iron 3 install. Years of
modding knowledge exist in that language and the largest mods rewrite nearly all of it -
BlackICE changes every one of the 33 interface files it shares with Their Finest Hour
and adds 20 of its own - so a game that asked for all of that to be redone in a new format
would begin with no content and no modders.

So an install is read as it is, in three ways:

- **Script is loaded natively.** Events, decisions, triggers, effects, technologies,
  units, laws, ministers and history are read in the original's syntax, with the
  original's keywords. There is no conversion step, and a mod's file is a valid file
  here. Where we can be stricter than the original, we are: a key that nothing
  recognises is reported, where the original drops it without a word.
- **Heavy assets get a cached build.** Textures, fonts and whatever else proves slow to
  read are turned into our own formats once, and rebuilt only when a source file changes.
  Starting the game is all it takes; nobody runs a converter by hand.
- **The interface is loaded from the original's `.gui` files** and laid out by our own
  runtime. See *The interface* below for how files written in pixels become scalable.

*Until 2026-10-07 this section said the opposite: our own formats throughout, fed by a
one-way converter, with "fidelity is the wrong master" as the rule. That was reversed
for content, and only for content - the technology underneath is still entirely ours.*

Players who want to use the original's content must own a legitimate copy. No game
assets or data files are redistributed here. The typefaces in `fonts/` are not the
original's: they are open ones, shipped under their own licence.

## The interface

The original's interface files are neither thrown away nor translated once. They are
read at startup like the script is, because mods ship their own.

**They do not assume a screen size, which is what makes this workable.** A `.gui` file
gives every element a size and a position in pixels, plus an *orientation* that says
which corner or centre of its parent the position is measured from. Of the 1573
orientations in Their Finest Hour's 36 files, 184 anchor to something other than the upper
left, and the largest fixed window is 1120 x 768. So the files already describe a layout
that adapts to the screen's shape; what they cannot do is adapt to its *density*.

That is the one thing our runtime adds:

- **A pixel in a `.gui` file is a unit on a 1920 x 1080 reference canvas.** The canvas is
  scaled to the screen, so a 4K display shows everything at twice the size instead of
  half, and a user setting can scale it further.
- **An orientation becomes an anchor.** Elements stay attached to the edge or centre
  they named, so a wider or taller screen gains room rather than stretching anything.
- **Nothing is rewritten on disk.** A mod's interface file works unchanged, and so does
  the next version of it.

**Text is the exception to drawing what the files ship.** The original's fonts are
textures of glyphs a dozen pixels high, and a dozen pixels cannot be scaled into anything
readable. So they are not drawn (decided 2026-10-08). Every font descriptor in an install
names the typeface its glyphs were made from - Arial, Trebuchet MS, Franklin Gothic - and
an open typeface that stands for it is drawn afresh, as outlines, at the size that makes
text as wide as the original's glyphs made it. The interface files name fonts exactly as
before, text lands where it did, and it is sharp at any size. **Those typefaces ship with
the game**, in `fonts/`, under the SIL Open Font License; nothing is taken from the
machine it runs on, so text is the same on every one. A mod supplies a typeface of its
own by putting a `.ttf` of the font's name beside its descriptor.

Two limits are worth stating. Scaling cannot add detail to a bitmap, so icons soften
when enlarged. The remedy is that the runtime works in
ratios of a texture rather than its pixel counts, so a mod can supply a sharper file at
any resolution; what tells the runtime the size a texture was designed at is not yet
decided. And a `.gui` file holds layout only - what each window *does* lived in
the original's executable, bound to elements by name. Each screen therefore needs code
of ours that finds its elements by the names the original used, and **those names are
the contract**: a mod that restyles or rearranges a window keeps working, while one
that renames an element breaks here exactly as it would there.

## The plan

Agreed 2026-10-07. Each stage ends with something that runs.

| Stage | What gets built | Done when |
| --- | --- | --- |
| 1. The world on screen | Provinces, adjacency, terrain, countries and start-date ownership read from an install; a political map with pan, zoom and province picking | The map of the start date is on screen. **Reached 2026-10-07** |
| 2. The clock and the economy | The hourly tick with its daily and monthly passes in the original's order; every state change as a command; IC, resources, manpower, production, research. The interface runtime arrives here, with the top bar as its first screen | A 1936-1948 run completes headless with totals that are plausible against a real savegame. **The clock, the command queue, fixed-point numbers, buildings, laws, ministers, technology levels, each country's IC, its home production, what its stockpile starts with, its six shares of IC and what consumer goods and supplies do with theirs, its leadership with what its four shares make, research and manpower are in (2026-10-08) and checked against savegames, and so is the interface runtime with the top bar and the production and technology screens; of what needs units, reinforcement, upgrades and the build queue are in (stage 3), with the four windows a unit is ordered from** |
| 3. Units and war | Orders of battle, units on the map, movement, land combat, supply; then air and naval | Two countries can fight a land war to a result. **The unit types and the orders of battle are in (2026-10-08): every brigade, ship and wing the base game starts with, holding what the original gives it on the first hour, checked one by one against a savegame. So is what a unit draws each day - supplies and fuel, the men who go home, reinforcement and upgrades - checked brigade by brigade two days on; and the officer ratio with what it does to organisation, hour by hour, checked the same way. And the build queue: what a unit costs and takes, the day's work on it, and the unit that comes out, checked against five saves of one game over four months - with the division designer and the ship, air and brigade builders that order one, read out of the original and not yet looked at beside it. And the units are on the map (2026-10-09): a counter for every unit that is not in port or at its base, stacked by province, and the menu a finished unit is placed from. And land units move (2026-10-09): an order by a right click, a route found as the original finds it, and an hourly march at the original's speed over the original's distances - every one of the map's 83,596 edges checked against the cache the game keeps of its own. And a selected unit has its panel (2026-10-09). And fleets and air units are sent about (2026-10-09): the port and the airfield on the map a docked fleet and a grounded wing are picked by, the window of orders a right click brings up, a move and a change of base, the range, and the sea's own distances - all 17,621,016 of them checked against the cache the game keeps of them, and the sailing and the routes against savegames. And land units fight (2026-10-09): a unit walking at an enemy starts a battle and fights it from where it stands, the front and the reserves, the hourly round with its damage, the side that gives way and falls back, the province taken, and the delay an attack leaves - read out of the original and not watched, there being no savegame with a battle in it. What the ground, the night, the weather and a leader do to a battle is not in yet, nor supply** |
| 4. Politics and script | Events and decisions with their triggers and effects; diplomacy, factions, politics, espionage, occupation, revolt | The base game's events load with no unknown key and fire on schedule |
| 5. AI | The planning and scoring layers, last because they consume everything else | A whole game runs with no human player |

Two notes on stage 1. The province bitmap turned out not to need a cache: it is read in
about 120 ms. And `docs/mechanics/map.md` lists the things about the map that are not
established - among them that the terrain rule comes from modding experience rather than
from the executable.

Four choices sit under all of it:

- **The base game first, mods second.** Their Finest Hour on its own is the target until
  it works; BlackICE then becomes a third layer over it, and a much harder test.
- **Fixed-point numbers.** Values are held in thousandths, as the original stores them,
  so that the simulation is deterministic and rounds the way the original does.
- **Every change to the game state is a command.** Nothing mutates the simulation
  directly, which is what lets a run be replayed and, later, shared between players.
- **A written mechanics spec comes before each system.** The reverse engineering
  write-ups mix formulas with addresses, so each system gets a spec of its own here -
  formulas, ordering, rounding, caps, no addresses - citing the write-up it came from.

### Next up

As of 2026-10-09, in stage 3, with the economy of stage 2 waiting on it. Kept
current as work lands, so that picking this up cold needs nothing but this file and
`docs/mechanics/`.

The units exist (`docs/mechanics/units.md`) and are kept: they draw supplies, lose and
regain men, and are re-equipped (`docs/mechanics/upkeep.md`), and their organisation is
held and returned by how many officers their country has
(`docs/mechanics/organisation.md`). More are built: the queue is in, with the thirty units
the orders of battle start under construction (`docs/mechanics/buildqueue.md`), and the
four windows a unit is ordered from (`docs/mechanics/builders.md`). And they are on the
map: a counter each, and a menu that puts a finished one there
(`docs/mechanics/mapunits.md`). And the land units move (`docs/mechanics/movement.md`),
a selected unit has its panel (`docs/mechanics/unitpanel.md`), and fleets and air units
are given their orders (`docs/mechanics/fleetsandair.md`). And land units fight
(`docs/mechanics/landcombat.md`), with the battle on screen
(`docs/mechanics/combatview.md`).
In order:

1. **Supply**, which replaces the stand-in of `upkeep.md`, section 2. Land combat came
   before it, in three pieces, and all three are in (2026-10-09).
   **A battle exists**: a land unit walking at a province an enemy stands in
   starts a battle for it and fights from where it stands; each side's units go on the front
   until the battle's width is used and the rest wait in reserve; each hour both sides
   shoot and the damage is settled; a front unit with no organisation left falls back; a
   side with nobody on its front has lost; the winner walks in and the province changes
   hands; and the attack leaves a delay of a week or so (`landcombat.md`).

   **And the hour's circumstances count**: fifteen things multiply into a unit's attack and
   defence each round - a river, a strait, forts, the ground, the night, the weather,
   experience, combined arms, dissent, lack of supplies, being dug in, crowding,
   envelopment, several battles at once, its country's cores - with day and night by each
   province's own clock and digging in a level a day (`landcombat.md`, section 4). Seven more
   wait: the leader and his traits, encirclement, shore bombardment, the difficulty setting
   and the rest listed there. **The weather's part is built and there is no weather.**

   **And it is on screen** (`combatview.md`): a battle the player has a part in has a round
   marker on the map, green, yellow or red by how it goes for him, with the attacker's share
   of the two sides' strength on it; the unit that attacks has the red arrow; its panel ends
   in a row for the battle; and a click on the row or on the marker brings up the battle's
   window, out of the original's `combat.gui` - the two flags, the picture of the ground,
   an icon for each thing in play, each side's tactic, and its units on the front and in
   reserve. **With the window came the tactics** (`landcombat.md`, section 11): each side
   picks one out of `common/combat_tactics.txt` by weight when the battle starts and again
   every day of it, and a tactic the other side's counters does nothing. Most of what tells
   one tactic from another asks for a leader, and there are none.

   **The fight was read and not watched.** None of the maintainer's saves has a battle in it,
   and the original's rolls cannot be made again here (`landcombat.md`, section 8), so
   nothing sets a round against the original's hit for hit. **The maintainer has spoken to
   five things about it, all on 2026-10-09**: a battle of two like divisions lasting about a
   week "seems right"; an attack delay stops all movement but the walk into the province
   attacked, where a unit may arrive with it still running, which is what is built; occupied
   ground is striped; units that attack together stand on the front until the width is used
   and the rest are reserves; and one that joins later waits in reserve and comes up by its
   chance - which is how it was read. `landcombat.md` ends with what is not established, and
   `OpenHOI3.SaveCheck battle <install> [attacker] [defender]` fights one with the install's
   own units and says how it went: Italy fails across a river against Ethiopia at Gurbo in
   182 hours, Japan takes the town of Tianjin in 347, and a German division sent across the
   Rhine at the Maginot Line attacks at four hundredths of its strength.

   **What is on screen was set against the original**: three screenshots of a battle of his,
   of the same day - the marker, the panel's row and the window. They corrected four things
   about how the interface draws, none of them about battles: a bar's picture is drawn
   upside down, a turned flag turns about its middle, a round shield cuts its flag to a
   disc, and a name too long for its box ends in three dots (`interface.md`, sections 5 and
   7). With those the window's upper half lies on his to within a pixel. He has not looked
   at ours.

   **There is no diplomacy, so a war has to be given**: `--war=GER:POL` on the command line,
   which is `CStartWarCommand` and nothing like a declaration. On the start date only Italy
   and Ethiopia are at war. **And there is no AI**: the enemy stands where its order of
   battle put it.

   Movement, cut in three with the maintainer on 2026-10-09, was done before it as far as
   it goes without an enemy: the order, the route and the march for land units; the unit's
   own panel; and fleets and air units, with the window of orders that sends them, their
   range, the sea's own distances, and the port and the airfield a docked fleet and a
   grounded wing are picked by (`fleetsandair.md`). Every order of a fleet or an air unit
   but a move and a change of base is a mission against somebody, and waits for the battles
   at sea and in the air.

   **Fleets and air units have not been looked at beside the original.** They were built
   from the reading and checked with real clicks - a port clicked, a sea province right
   clicked, the window's row and `accept`, hours let go by - and their sailing, their
   route and the whole table of distances by sea agree with the original's saves and its
   own cache. What has nothing to check it but the reading and the art: how big a
   building's picture is on the map and from what zoom, the orders window beside the
   original's, and which unit of several a click on a port brings up first.

   **The maintainer set the unit panel beside the original on 2026-10-09**, one idle
   infantry division in both. It was built from the reading and checked with real
   clicks - a row of a unit above, an order and the button that halts it, the three
   checkboxes. His screenshot mended three things, each now measured on it and none
   read: where a turned bar stands, that the panel's entries touch, and where the name
   sits in its box. `unitpanel.md` ends with what is still unseen - a headquarters, a
   unit on the march.

   **The march's speed is checked** against four saves of one game the maintainer made
   on 2026-10-09 with units on the move: `OpenHOI3.SaveCheck march`, 301 of 301
   divisions to the thousandth over four hours (`movement.md`, section 5). The weather's
   part in the speed was built for it; the weather itself is not. The hour of an arrival
   and the route are not what that check covers for a division; for a fleet and an air
   unit it covers both (`fleetsandair.md`, section 6).
2. **A look at the screens beside the original**, figure by figure. It needs nothing
   built, only the game open, so it can happen whenever suits. The specs each end with
   what a look would settle. The production screen has four needs to look at and the
   lines of its build queue. **The four builder windows have been tried twice by hand**,
   on 2026-10-08 and the day after; what was wrong is mended and the second time he
   found them right (`builders.md`, section 10). Nothing in them has been measured
   against a screenshot, and `builders.md` ends with what that would settle: where each
   figure sits, the checkbox's and the combined arms strip's frames, the spare template
   buttons, the panel with nothing picked. **The near counter is measured against a
   screenshot of the original's** (2026-10-09), which put its figures on their line and
   its name down its right edge; `mapunits.md` ends with what a further look would
   settle - a counter's size at a given zoom, the far counter's texts, which way a stack
   fans.
3. **What the builder windows leave unread**, none of it in the way of ordering a unit:
   the name and the picture of the historical model a type is shown by, the name of each
   level of a ship's technology and the text of what it does, the description of a wing
   or a brigade, the forty tooltips a window, and what saving a template does
   (`reversing/findings/FINDINGS-builders.md`, the end).

The interface runtime is in, with three screens - the top bar, production and technology -
the four windows the production screen orders a unit from, the deploy menu with the
alert that opens it, and the panel of a selected unit. Windows, icons, buttons,
checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements are built
from the original's `.gui` files on a canvas that scales, found by the names the
original's code used, and filled with the figures in the original's own formats and
colours (`docs/mechanics/interface.md`, `topbar.md`, `countryviews.md`, `sliders.md`,
`productionscreen.md`, `technologyscreen.md`, `builders.md`). **A game can now be driven by hand as far
as the simulation goes**: the six IC sliders and the four leadership sliders set a
country's shares, and the technology tree and the research queue set what it
researches. What the screens write into their elements was read out of the original
for them: `reversing/findings/FINDINGS-topbar.md` and
`reversing/findings/FINDINGS-countryviews.md`.

The routine that rebuilds a country's modifier has been read from end to end
(`docs/mechanics/modifiers.md`, sections 4 to 6). What it showed that could be built without
the script's conditions is built: strategic resources as the original shares and scales
them, the provinces' modifiers, `war`, `peace` and `dissent`, and what a minister does to
the decay of what a country knows. What is left of every mismatch with the savegames is
now a named cause, and each is stage 4 or the AI: see *Loose ends*.

Done so far inside this stage: the stockpile (`docs/mechanics/stockpile.md`); the six
shares of IC with the two that spend without units, consumer goods and supplies
(`docs/mechanics/production.md`); the two conversions, energy into crude oil and crude
oil into fuel (`docs/mechanics/ic.md`, section 4); leadership with its four shares, of
which officers, diplomatic influence and spies are made each day
(`docs/mechanics/leadership.md`); research - the queue, what a level costs, what a day
adds, what finishing one does, and the monthly decay of what a country knows
(`docs/mechanics/research.md`); manpower - what a day adds and what a new game starts
a country with (`docs/mechanics/manpower.md`); the interface runtime with the top
bar; and the production and technology screens. And the first two pieces of stage 3: the
unit types, the historical models and the orders of battle, with what a brigade holds on
the first hour and the officers a country's units bring it (`docs/mechanics/units.md`);
and what a unit draws each day - what it uses in supplies and fuel and the stock of both
a new game starts with, troop rotation, and the reinforcement and upgrade shares of IC
with the needs their sliders show (`docs/mechanics/upkeep.md`). And the third: the
officer ratio, the most organisation a brigade may have, the hourly return towards it,
and what a crowded base does to a fleet or an air group
(`docs/mechanics/organisation.md`). And the fourth: the build queue - ordering a unit,
what it costs and takes, the production share's day, the unit that comes out and where
it may be put, and what a new game makes of the units its files start under construction
(`docs/mechanics/buildqueue.md`). And the fifth: the windows a unit is ordered from - the
division designer with its brigade picker, and the ship, air and brigade builders - with
what a country may build and how many, the combined arms a design shows, the country's
division templates, and what a new unit is called (`docs/mechanics/builders.md`). And the
sixth: the units on the map - where a province's units stand, what a counter is made of
and which units have one, the two figures it shows, and the alert and the menu a finished
unit is placed from, alone or with a unit in the field (`docs/mechanics/mapunits.md`).
And the seventh: movement, for land units - the edges between provinces with their kinds
and the distance of each, the order by a right click, the route as the original finds it,
the speed and each thing that scales it, and the hourly march
(`docs/mechanics/movement.md`). With it the rule that gives a province its terrain was
read and replaces the maintainer's (`docs/mechanics/map.md`, section 9). And the eighth:
the panel of a selected unit - the units above it, its header with its men and its two
bars, its status, what it is doing, its brigades and the units under it, with the three
switches for reinforcement, upgrades and priority, and the button that halts a march
(`docs/mechanics/unitpanel.md`). And the ninth: fleets and air units - where each may
go, ports, the range and the distances by sea, the window of orders, the move and the
change of base, and the buildings on the map with the port and the airfield a unit in
one is picked by (`docs/mechanics/fleetsandair.md`; `mapunits.md`, section 9). And the
tenth: a land battle - what starts one, the front and the reserves, the round and its
damage, the end, falling back, the province taken and the attack delay, with the game's
generator of chance (`docs/mechanics/landcombat.md`).

There are five savegames of one game to check against now, from 1 January to 20 April
1936. The first is the start; the later four are a month or less apart, which is what
checks anything that moves from day to day.

**When a spec needs something read out of the executable**, the reading is done in the
fact base and entered in its record, not only written up: this repository's `CLAUDE.md`,
*Where knowledge about the original comes from*.

### Loose ends

None of these blocks the list above.

- **A land battle has no leader in it**, and so the plainest tactics: with no skill and no
  traits to ask for, an attacker has the plain attack and now and then an assault or a
  shock, and a defender the plain defence (`landcombat.md`, section 11). Of the things that
  bear on a unit in a round, seven wait for what they read - a leader and his traits,
  encirclement, shore bombardment, the difficulty setting, mission efficiency, paratroops,
  landing craft -
  and three technology figures are left out for want of their keys; `landcombat.md`,
  section 4, lists them. **And nothing makes weather**, so the weather's part of a battle
  and of a march comes to nothing and every province's night is by the clock alone. Beside
  those: the buildings a pushed-back defender damages, the relations and the events a decided
  battle moves, a battle's experience and practical knowledge, and three of the four cases
  of who a taken province goes to - a puppet's master, an exile's faction, a friend who
  holds the ground beside it.
- **The battle window says less than the original's.** No tooltip - which is where the
  original says what each modifier is worth to a unit and what a tactic does - no leader's
  portrait, no sound; and only a land battle has one. An enemy's counter says what its
  unit is, where the original writes question marks on one it knows too little of; units
  in a battle stand where they stood, where the original has a place in each province for
  the attackers and one for the defenders; and what a click on the original's marker does
  was not read - here it opens the window. `docs/mechanics/combatview.md` ends with the
  list.
- **Four rules of how the interface draws are measured on screenshots and not read**: a
  bar's picture upside down, where a turned flag stands, where a round shield puts its
  flag, and where a name too long for its box is cut - the last of which ends ten pixels
  later here than in the original. `docs/mechanics/interface.md`, sections 5 and 7.
- **Where the heavy border runs beside occupied ground** is read off one screenshot of the
  maintainer's and not out of the original: at the edge of what a country holds, not of
  what it owns. Which colour the stripes are is his word - the wide bands the holder's,
  the narrow the owner's. `docs/mechanics/map.md`, section 10.
- **The game's rolls start from a seed of 1** every game, so the same orders fight the same
  battle. Nothing sets it yet; a new game should. `CGameState.Seed`.
- **Of `positions.txt` a province's points are read but for its city**: where its units
  stand, where its name is written - its terrain is taken around that - where its port
  is, the land end of a distance to the sea, and where each of its buildings is drawn.
  `docs/mechanics/map.md`, section 4, and `mapunits.md`, section 9.
- **A window's background is no longer stretched to the window.** A plain picture named
  as a window's background keeps its own size at the window's corner; only a cornered
  tile fills the window. Measured on the orders windows and the files on 2026-10-09
  (`docs/mechanics/interface.md`, section 3). No window built before then has a plain
  picture for a background, so none changed; the fifteen of the base game that have one -
  the event windows, the brigade view, the province finder - will be right when they come.
- **Of the orders a fleet or an air unit can be given, three are built**: a fleet's move,
  and the change of base of either. The window lists the rest as the original does and
  they cannot be ticked; its stance and priority step and mean nothing; the air window's
  target shapes, sliders, day and night and dates are drawn and do nothing.
  `docs/mechanics/fleetsandair.md`, sections 4 and 5.
- **Every country's ports and airfields show whether they are in use**, there being no fog
  of war; a building's picture is drawn at a size and from a zoom that are the reading's
  and ours, never set beside the original; and a selected fleet in port has nothing on
  the map to show it is selected. `docs/mechanics/mapunits.md`, section 9.
- **A unit now moves before its organisation returns, not after.** The reading for fleets
  found that the original marches its units in the routine that looks at their orders, one
  country after another, before any unit's own hour. `docs/mechanics/tick.md`, section 3.
  Nothing yet reads one from the other.
- **A province's terrain no longer follows the rule the maintainer gave.** His, from
  modding the original, was the terrain covering most of the province; the original's,
  read on 2026-10-09, counts only a square of twenty pixels around the province's name. It
  changes 384 of the base game's 10,621 land provinces. Nothing in a savegame says which
  is right, so it rests on the reading. `docs/mechanics/map.md`, section 9.
- **Nothing an enemy would change about a march is built**: no battle holds a unit, no
  attack delay, no retreat; no fleet meets another or closes a crossing. A unit walks onto ground
  its country may walk on - its own, its faction's, nobody's, and that of a country it is
  at war with - and military access, alliances and puppets are not known yet.
  `docs/mechanics/movement.md`, the last two sections.
- **The march's speed is checked against savegames and its arrival is not.** The
  distances agree with the original's cache exactly and what an hour adds agrees with
  saves hours apart, division by division; the hour a unit arrives and what it keeps are
  as read. **There is no weather**: a province holds the figures and what they do to a
  march is built, but nothing makes them, so no march here is slowed by mud or frost -
  in the original's January most of Europe's are. `movement.md`, section 5.
- **The route found here is not always the AI's.** Set beside the paths in a savegame,
  about nine in ten are the same; the original has two more careful finders the AI may
  ask for, which are not read. A player's order uses the plain one, which is.
- **The arrow of a selected unit and the pointer beside every moving one are the
  original's own**, each read and held against a screenshot the maintainer gave on
  2026-10-09: the arrow a band along a curve through the path, hollow ahead of the unit
  and filled behind it, from the original's picture of it. One cut is added at the very
  end so that the tip is always whole; the original's last is a little short of it and
  its tip is whole all the same, which was not made out. Where a moving unit's counter
  stands is from one screenshot, by a stack whose depth had to be counted off its edges.
  `docs/mechanics/movement.md`, section 7, and `mapunits.md`, sections 1 and 2.
- **Only the marching arrow is drawn.** The original has others for an attack, a
  retreat, a redeployment and a landing, declared beside it in `interface/arrows.gfx`.
- **The unit panel shows more buttons than work.** The three checkboxes, the button that
  halts a march, the button that goes to the unit's province and the rows that select a
  unit above or under it do what the original's do. Making a headquarters, attaching and
  detaching, loading, disbanding, reorganising, the leader, the order of battle browser
  and selecting all subordinates are shown as the original shows them and cannot be
  pressed, and so is a headquarters' row for the AI that might command it - the box that
  hands it over and the slider for how aggressive it is. What an AI in command would
  add to that, what a unit carries and the battles it is in
  are not there, and neither is any tooltip - which is where the original says when a
  unit will arrive, so the readout beside the panel still does, when a province is
  selected as well as the unit. `docs/mechanics/unitpanel.md`.
- **A unit's name cannot be changed**: the edit box it is in is shown and not typed in.
  Where an edit box puts its text and how a rotated icon is placed are measured and not read, both from the
  unit panel alone (`docs/mechanics/interface.md`, section 7).
- **The unit panel's supply and fuel bars are always full.** The original's measure what
  lies in the unit's province against what the units there need; a province's need waits
  for the supply network. Its attrition figure is always nought for the same kind of
  reason. `unitpanel.md`, section 4.
- **One unit at a time.** Nothing selects several, so the original's list of selected
  units and the window over it are not built.
- **The hierarchy of a selected unit is on the map, less what an AI and radio range would
  add.** A selected land unit is joined to its commander and everything under it to
  theirs, in the original's picture and colours; H switches the lines off and on; and
  the counter of every unit under it wears the original's second ring colour. The
  setting is kept nowhere, the broken red line of a unit out of radio range is never
  drawn, the lines are drawn at any zoom, and the green ring of a selected unit an AI
  commands has no AI to show for. `docs/mechanics/mapunits.md`, section 8.
- **What shows on the map at which zoom is the original's, told in its camera's heights**:
  nothing but the map from a height of 500 up, the far counter under that, the near one
  under 100. The maintainer's word for the two views, 2026-10-09, and the camera read for
  the figures (`mapunits.md`, section 7). The zoom here goes out further than the
  original's camera goes up, until the whole map is on screen.
- **Where four of a province's five stacks stand is ours.** The original stands a standing unit at the
  province's `unit` point, as here, and makes four more points from that for units
  moving, fighting and flying - how was not found. That the higher
  rank is on top of a stack is the maintainer's word, the original's sorting not having
  been read. `mapunits.md`, sections 1, 3 and 7.
- **A counter's name is its unit's**, as the original writes it with `show_unit_names`
  on; with it off the original writes the leader's, and there are no leaders and no
  settings. The maintainer decided on 2026-10-09 that it stays so until the settings are
  built. `docs/compatibility.md`, entry 7.
- **A unit can be selected and sent somewhere, and nothing more.** A click on the counter
  on top of a stack puts a ring on its unit, and further clicks go through the stack - the
  maintainer's account of the original, 2026-10-09; the order they go in is ours. There is
  no panel for a unit.
- **The provinces a unit may be placed in are tinted green**, which the maintainer has
  seen and passed. That the original marks them is remembered; how, and the click on the
  map that places a unit, were not read. `mapunits.md`, section 6.
- **One alert of sixteen is built**, and it is up whenever a unit waits: when the
  original puts it up was not read. `mapunits.md`, section 5.
- **Loading runs on the main thread** and holds the window for under a second at startup.
- **The game does not stop at the end date** in `common/defines.lua`.
- **Things the savegame shows that are not explained**: 476 technology levels higher
  than the history files give, home production for three countries, governments for
  countries that do not exist yet (`docs/mechanics/resources.md`, section 5), what sets a
  country's shares of IC at the start, and why nine untouched countries make 10% more
  supplies than their laws give (`docs/mechanics/production.md`). Two that were on this
  list are answered: three countries' extra money is the gold they hold, and a country at
  war starts with five days fewer of resources for each month of the war
  (`docs/mechanics/stockpile.md`, section 2).
- **Triggered modifiers are not applied, and one of them touches nearly everybody.**
  `common/triggered_modifiers.txt` gives a modifier while a condition holds, and the
  conditions are the script's, which is stage 4. `neutrality` takes 5% off the research of
  every country outside the three factions - ninety of them in 1936 - so research here is
  that much too fast for them. Five countries' leadership is also a little low for want of
  others, and France has half as much manpower again as it should. `docs/mechanics/research.md`, section 8,
  `docs/mechanics/leadership.md`, section 8, and `docs/mechanics/manpower.md`, section 6.
- **The four windows a unit is ordered from have been tried by hand and never measured
  against the original.** They are built from a reading of its code, the figures and the
  rules checked in the listing. The maintainer's first try, on 2026-10-08, found four
  things wrong; they are mended and on the second he found the windows right
  (`builders.md`, section 10). Parts of them are ours for want of a reading: the name and the
  picture a type is shown by, the names of a ship's technology levels and what each
  does, the description of a wing or a brigade, every tooltip.
  `docs/mechanics/builders.md`, the last four sections.
- **Which element takes a click is ours, but for two things the maintainer saw.** Nothing
  of how the original hands the mouse to an element was read. A button, a scrollbar and
  an icon take a click, the uppermost first; a window, a text box and a list box do not;
  and an icon hands its click on to a button of its own window that it lies on, as the
  original's does - a click on a line's counter picks the line. What an icon does over
  anything else is a guess. `docs/mechanics/interface.md`, the last section.
- **A texture no layer has under the extension it is named with is still looked for
  under another**, `.png` among them. The original looks for a `.tga` as a `.dds` and for
  nothing else, and reports the rest missing. `interface.md`, section 5.
- **An order is refused here that the original would carry out.** Its commands ask
  nothing and its windows do the refusing; here the command refuses too, so that nothing
  can be ordered round the window. Not a departure a mod can meet: the same things are
  refused, a step later. `builders.md`, section 9.
- **A unit type's `available_trigger` is not asked.** It is script; a type that has one is
  taken to pass. The base game gives it to seven elite brigades, each also kept to its
  own country by `usable_by`.
- **Every country's finished units but the player's wait for ever.** A finished unit
  joins those waiting to be placed, where it eats and calls for officers, and the player
  places his from the deploy menu. No other country orders, cancels or places anything,
  there being no AI: what their files start them building is finished and then waits.
- **The queue builds units and nothing else.** The original's also holds buildings and
  convoys, and a finished one of either prices every line of the queue again.
- **Six countries' queues start somewhere else than the original's**: Britain's, the United
  States', Japan's, France's, Belgium's and the Netherlands' lines are priced as the game
  starts under laws or ministers the original's AI had already changed, and which they
  were is not known. Every line's cost and days agree once the save's laws are given.
  `buildqueue.md`, section 9.
- **A unit shows as a counter and nowhere else.** No screen lists the units or says what
  one holds; for that there is `tools/OpenHOI3.SaveCheck` - and, indirectly, the
  production screen's supplies, fuel and four needs, and its queue.
- **Units draw on their country's stockpile, wherever they stand.** The original moves
  supplies out to the provinces through its supply network, loses some on the way, and a
  unit the network has not reached goes short - 68 brigades in its start save. Here no
  unit is short while its country has supplies. It is the original's own arcade rule,
  standing in until the network is built. `docs/mechanics/upkeep.md`, section 2. It also
  means no unit here loses organisation for want of supply, which is what going wholly
  without does (`docs/mechanics/organisation.md`, section 3).
- **Nothing burns fuel but a fleet away from its base**, because nothing moves.
- **What a unit uses has no leader in it.** A commander's skill and traits and the
  headquarters above a unit all change what it uses in the original; in 27 of 65 capitals
  that is the whole of the difference from the save. `upkeep.md`, section 1.
- **A faction's modifier is not applied.** `ideologies.txt` gives each faction one - the
  Comintern's makes its members' units use a third less - and whether the original adds
  it to a country's modifier is not read. The Soviet Union starts with a quarter less
  fuel in the save than here, which is about what it would do. `upkeep.md`, the end.
- **The fuel a new game starts with is exact for 18 countries of 76** and close for about
  twenty more, but twenty-five hold 2% to 19% more in the save than the rule gives -
  Germany 9%, France 19%, landlocked Czechoslovakia 14%. What they have in common was not
  found, so something is missing from the sum. `docs/mechanics/upkeep.md`, the end.
- **Nothing changes a law, so nothing holds a country's brigades when one changes, or
  prices its queue again.** The original holds every brigade to its strength and its
  organisation ceilings after any change of law and gives back the men taken off, and
  works out afresh what every line of the build queue costs and takes. Both routines are
  built and tested and nothing calls them, there being no politics screen and no AI.
  `docs/mechanics/organisation.md`, section 5, and `buildqueue.md`, section 2.
- **Three things stop the hourly return of organisation in the original, and none can
  happen here yet**: fighting, marching into a province an enemy holds, and being
  carried at sea. `organisation.md`, section 3.
- **Whose units share a base, and whether a technology's `naval_base_efficiency` is what
  widens a port**, are `inferred`. `organisation.md`, section 4.
- **A game's units are made under its history's laws**, where the original's are made
  under a conscription law something has already changed: Germany's reserves start at a
  quarter of their strength here and at half there. It is the same gap as the manpower
  one below, and like it waits for the AI. The maintainer confirmed it in the running
  game on 2026-10-08 - an untouched first screen as Germany already shows
  `two_year_draft` - and calls it a bug, since the AI should not touch the player's
  country. **Decided the same day: the player's country keeps its history's laws**, so
  for the country being played this gap is meant and stays (`docs/compatibility.md`,
  entry 6); for the countries an AI runs it is still to be built.
  `docs/mechanics/units.md`, section 8.
- **The original's AI makes 86 theatres as a game starts**, each a headquarters with one
  brigade, and puts every unit the files leave at the top under one. Here the units stay
  as the files nest them, so 34 are under a different commander.
- **Leaders are not built.** The number an order of battle gives a unit is kept and
  nothing more. Three things that are built wait for them: an army's commander adds his
  skill to the most organisation every brigade under him may have - the original's start
  save shows it on 622 brigades, by one, two or three points
  (`docs/mechanics/organisation.md`, sections 2 and 7) - and a commander and the army
  group's change what a unit uses (`upkeep.md`, section 1). Both need how far a command
  reaches, which falls with distance, so leaders sit best after units are on the map.
- **Twenty-four air groups aboard carriers, and their wings, have no name**, where the
  original names them `1st Air wing` and `1st CAG` by a rule that has not been read.
- **Two blocks of a unit type are read and not kept**: `available_trigger`, which is
  script, and `extra_amphibious_defence`.
- **A country starts with the manpower its history's laws give**, where the original's AI
  has already chosen its own: Germany begins with 469 here and 882 there. The same goes
  for the decision the United States' AI takes at once, which costs it three quarters of
  its manpower. `docs/mechanics/manpower.md`, sections 4 and 6.
- **Event modifiers are not applied.** What an event or a decision gives a country for a
  time is one of the things its modifier takes in, with an end date. Nothing gives one
  until events and decisions exist. `docs/mechanics/modifiers.md`, section 4.
- **Strategic resources are shared on one of the original's three roads.** A faction's
  majors and big industries on the holder's continent are given its resources. The faction
  leader at war taking outsiders' needs alignment, and sharing across continents needs to
  know what the original means by a sea region. A country's faction is also only what its
  history file says: the original adds a leader's subjects.
  `docs/mechanics/modifiers.md`, sections 6 and 7.
- **A resource once had is never lost here, and that the original behaves so is only
  `likely`.** Its one routine for the figure only raises it, and a search found nothing
  else that lowers it short of resetting the session - but that is a search coming up
  empty, and nobody has watched a running game lose a resource province. Worth settling in
  the running game, and deciding on purpose, before anything depends on it.
  `docs/mechanics/modifiers.md`, section 6.
- **A government adds no modifier.** The original adds whatever a government's block
  declares beyond its own keys; Their Finest Hour's declare nothing, a mod's might.
- **Only the player queues research.** There is no AI, so no other country researches
  anything, and nor does the player's until the technology screen is used.
- **A technology's `allow` block is read for the six kinds of condition the base game
  uses**, and none of the six has been read out of the original. Whether a port is a
  naval base, and whether it is owning or controlling a province that counts, are guesses.
  A mod that uses any other condition gets a warning and the condition left out.
  `docs/mechanics/research.md`, section 5.
- **Leadership makes influence and spies that nothing spends, and officers that nothing
  takes away.** Influence and spies accumulate until diplomacy and espionage exist.
  Officers are used - the ratio of them to what the units call for is what holds
  organisation - but only battle loses any, and there is none. A country does start with
  the officers its units bring (`docs/mechanics/units.md`, section 7).
- **The original's AI script changes laws and ministers before the first hour**, for every
  country including the player's, so what a player sees on the first screen is not what
  the history files say. This project has no AI, so its countries start with the
  history's laws - Germany on basic mobilisation where the original shows a war economy.
  For the player's country that is now the decision and not a gap
  (`docs/compatibility.md`, entry 6); whether the AI may change the player's ministers
  was not asked.
  `docs/mechanics/resources.md`, section 5. It goes on changing them after the last of
  those days is run, so a savegame's laws are not always the laws its figures were made
  under; `docs/mechanics/production.md`, section 7.
- **The original has run two days before the first screen**, and this project starts from
  day nought. A stockpile here on 1 January is what the original had before those two
  days. `docs/mechanics/stockpile.md`, section 3. How it runs them has never been read;
  organisation says they are run before the original counts a game as running, with an
  hour before each day - on the evidence of three brigades
  (`docs/mechanics/organisation.md`, section 7).
- **Puppets are not read**, and one consequence shows already: Manchukuo starts with 1890
  metal where the original holds a subject to 1000. Wars are read for who is in them and
  when they began.
- **One of the six shares of IC spends nothing**: lend-lease. IC put in it is wasted, and
  wasted IC costs no resources. Production, reinforcement and upgrades spend what their
  queue and their units take, and no more.
- **Consumer goods demand is missing its standing-army term**, which needs neutrality as
  well as the units it now has. So no country here is unhappy about the size of its army.
  `docs/mechanics/production.md`, section 3.
- **Only the player moves the shares.** There is no AI to move them, so every other
  country keeps the shares it started with - a fifth of what consumer goods leave on
  production, on reinforcement and on upgrades each, whatever its queue and its units
  need.
- **The laws the original's first two days ran under are not all known.** Its AI changes
  a country's training law and industrial policy more than once before the first screen,
  and both set what an upgrade costs and how long it takes. Germany's two days fit its
  history's training law and not the one its save shows; the savegame check tries both
  for every country and says which fitted. `docs/mechanics/upkeep.md`, section 9.
- **An icon or a button is the pixel size of its texture.** That is right for the
  original's art and is the one thing a sharper replacement must not change; what says
  the size a texture was designed at is still not decided (*The interface*, above).
- **Only Arial's stand-in has Arial's widths.** Liberation Sans was drawn to them, so
  text in it lands where the original's did. Fira Sans for Trebuchet MS, Barlow
  Condensed for Franklin Gothic, EB Garamond and Anton for Impact are near relatives,
  sized by the height of a line and narrowed where they come out wider; which stands
  for which is a choice made by eye and easy to change (`fonts/readme.md`). Six display
  typefaces the original names have no stand-in and are drawn in Liberation Sans, with
  a line in the log. `docs/mechanics/interface.md`, section 6.
- **The colours of coloured text are fixed in the code, and the original's are in its
  files**, a `colorcodes` block in each font's declaration that a mod can change. The
  maintainer decided on 2026-10-08, knowing that, that ours stay for now.
  `docs/compatibility.md`, entry 5.
- **How a button looks under the mouse, held and disabled is the original's own**, from
  its shader, which is plain text in the install; and where a text box puts its text
  is the original's own too, read out of its font. `docs/mechanics/interface.md`,
  sections 5 and 6.
- **Two kinds of element are read and not drawn**: edit boxes and the shields of a
  dialog. Nor are tooltips shown, or an icon's `rotation` applied, and a checkbox is a
  button that does not stay down. `docs/mechanics/interface.md`, section 2.
- **How scrollbars and boxes of overlapping elements lay themselves out is ours.**
  Neither was read out of the original; each is the plain reading of what the files
  declare. So is where a line of text breaks. **The list box was the third, and when it
  was read on 2026-10-09 the plain reading turned out wrong** - the original spaces
  nothing and cuts nothing off - so the other two are worth less than they look.
  `docs/mechanics/interface.md`, sections 2 and 6.
- **The base game's ship list is 620 high in a frame of 300.** That is what its file
  says, and by the routine a list is as many lines as its height holds: a thirteenth
  ship type is drawn across the frame's lower edge, where the maintainer remembers the
  game never drawing one. It shows only with every ship type to build, which
  `--all-units` gives. Not settled: `docs/mechanics/builders.md`, section 10.
- **The two screens have been tried by hand.** The maintainer used them on 2026-10-08:
  dragging a slider, the folder buttons, the queue's priority and cancel buttons,
  `start`, the queue's dates reading year first and a falling resource in red with no
  minus sign are all right. Four things were wrong and are mended - a locked slider is
  grey, the list's arrows are the right way up, text is placed as the original's font
  places it, and the panel's category picture is full size. The figures themselves
  have not been compared one by one with the original's.
- **What the two screens' buttons do is ours**, from how the game is remembered and from
  its own tooltip keys: the original's handlers were not read.
  `docs/mechanics/technologyscreen.md` and `productionscreen.md`, what is not
  established.
- **The production screen is part empty.** Its build queue is listed, with buttons that
  move a line and take it out, and its four builder buttons open their windows; its other
  three lists have nothing to list and its building buttons have nothing to build, until
  there are buildings in the queue, trade and convoys. Five of its
  six sliders show a need now - all but lend-lease - and the stock of supplies and of
  fuel falls by what the units draw. What its queue's lines show was read out of the
  original and has not been looked at beside it; what their buttons post is ours.
  `docs/mechanics/productionscreen.md`, section 6.
- **A technology's effects are listed as its file's keys and figures**, where the
  original writes them out in words, and what it does to a kind of unit is not listed
  at all. `docs/mechanics/technologyscreen.md`, section 7.
- **A slider's lock lasts as long as the screen**, where the original keeps it with the
  country and so in the savegame. `docs/mechanics/sliders.md`, section 3.
- **Seven of the top bar's screen buttons open nothing** - diplomacy, politics,
  intelligence, theatres, statistics, strategic warfare and the menu - and the country
  played is Germany unless the game is started with `--country=TAG`.
- **Two of the top bar's figures are less than the original's.** A resource's colour
  comes from four of the thirteen ledgers, the rest being trade, convoys, debt and
  tribute; and national unity never moves, where the savegames show it moving by a
  thousandth or two a day from the first, which fits spies.
  `docs/mechanics/topbar.md`, sections 2 and 6. The officer ratio was the third and is
  worked out now (section 9).
- **Whether the top bar's picture changes with the country** was not read. The install
  has a background for each of many tags beside the generic one that is drawn, and the
  country screens do change theirs. `docs/mechanics/countryviews.md`, section 2.
- **A misspelt modifier is not reported.** The modifier kinds are not a closed list here,
  so any key a block does not own is taken as one. `docs/mechanics/modifiers.md`, section 7.
- **A country's modifier is seven of the original's eighteen sources**: `base_values`, its
  ministers, its laws, its strategic resources, its provinces' modifiers, `war` or `peace`,
  and `dissent`. The rest wait for what they need - a difficulty setting, neutrality, war
  exhaustion, espionage, subjects. `docs/mechanics/modifiers.md`, section 7.
- **A province's modifier is its buildings, `overseas` and `non_core`.** Terrain, the
  coast, occupation policies, nationalism and revolt risk do not feed it yet.
  `docs/mechanics/modifiers.md`, section 3.
- **Each spec ends with what is not established.** Those are questions for the reverse
  engineering. Production's, the oil conversions', leadership's, the starting
  stockpile's, research's and manpower's were answered by reading the executable; those
  readings are `reversing/findings/FINDINGS-distribute.md`,
  `reversing/findings/FINDINGS-leadership.md`,
  `reversing/findings/FINDINGS-researchshare.md` and
  `reversing/findings/FINDINGS-manpowergain.md`, and are entered in the fact base's record.
  So was what a country's modifier is made of, the question five of the specs shared:
  `reversing/findings/FINDINGS-countrymodifier.md`. The two screens' were read before
  they were built: `reversing/findings/FINDINGS-countryviews.md`. And what units draw
  each day was read for its spec: `reversing/findings/FINDINGS-unitdaily.md`; and the
  officer ratio and the return of organisation for theirs:
  `reversing/findings/FINDINGS-organisation.md`; and the build queue for its:
  `reversing/findings/FINDINGS-buildqueue.md`; and the counters and the deploy menu for
  theirs: `reversing/findings/FINDINGS-counters.md` and
  `reversing/findings/FINDINGS-deploymenu.md`.
  The rest have not been filed in its work queue.

A replacement for the engine still leaves a few things of the original's out. These are settled
decisions rather than things merely unbuilt, so nothing should be designed around them
coming back later. **Every place where a file that is valid in the original is
deliberately treated differently here is listed in `docs/compatibility.md`**, with who
decided it, what a mod loses by it and what would undo it: these three, and the ones
about the interface and its text.

- **Custom game starts.** No screen for assembling a game from arbitrary settings
  before it begins.
- **Bookmarked starts.** The original offers eight dated entry points, from 1936 to
  1944, each with its own suggested countries and flavour text. There is one starting
  point here.
- **Isolated scenarios.** The original ships nine self-contained battles - Fall Blau,
  the Bulge, Operation Downfall and so on - each with its own dates, map extent and
  victory conditions, living apart from the campaign. There are none.

Together these collapse the original's four ways into a game down to one. That is the
point: a single canonical start is one set of starting conditions to balance, one code
path into the simulation, and one start state to build from the history files. Every
extra entry point is a permanent tax on all three.

It also means nothing here has a reason to read `common/bookmarks.txt` or `scenarios/`,
and no lobby is needed to choose between them.

## Where things stand

Early. What exists is the reading of an install, the beginnings of a data model, the
first three screens, the four windows a unit is ordered from, and the units on the map
with the menu that puts one there and the orders that send one somewhere - by land, by
sea and by air - in four projects:

| Area | What it does |
| --- | --- |
| `lib/OpenHOI3.Sim` | The game state - dates, countries, provinces, terrain - and what drives it: fixed-point numbers, the command queue, and the hourly tick with its daily, monthly and yearly passes, and the first systems in them: each province's modifier and each country's, from its laws, ministers, provinces and the strategic resources it has the use of; its IC, what it produces at home, the stockpile that goes into, the six shares its IC is divided between, with the money, supplies, dissent and resource bill that come of them, the conversions of energy into crude oil and crude oil into fuel, its manpower, its leadership with the officers, influence and spies its shares make, and its research with the monthly decay of its theories and practicals. And the units: the kinds of brigade, ship and wing, each one's own technology levels and figures, the chain of command they sit in, what each holds as a game starts, and what a day does to one: the supplies and fuel it draws, the men who go home, and the reinforcement and upgrades its country's IC pays for. And what an hour does: the officer ratio is worked out, and each brigade's organisation is held under it and comes back towards it. And the build queue: ordering a unit, what it costs and takes, the day's work the production share pays for, the unit that comes out, where it may be put and which unit in the field it may join. And movement: the route, the hourly march, where each kind of unit may go, the orders that send a fleet or an air unit and change its base, its range, and the distances by sea. And land battles: what starts one, its front and its reserves, the hourly round, the damage and what it costs a country, falling back, shattering, the province that changes hands and the delay an attack leaves, on the game's one generator of chance, with the tactic each side picks by weight and what the two tactics make of the battle. References nothing, so the simulation runs headless |
| `lib/OpenHOI3.Content` | Reads the Clausewitz script format, finds and layers a Hearts of Iron 3 install, and reads its localisation tables, its map with its continents, each province's points and terrain and the edges between provinces with their kinds and distances, where its buildings stand, which provinces have a port and onto which sea, the two kinds of unit counter, its defines, its buildings, laws, ministers, technologies and strategic resources, its province, country and war history, its unit types, historical models and orders of battle, its combat tactics, and its interface files. No engine in it either |
| `lib/OpenHOI3.Interface` | What the screens show, still with no engine: the text of every figure of the top bar, the production screen with the lines of its build queue, the technology screen, and the four windows a unit is ordered from - their columns, a design's totals, the order panel - in the original's format and colour, the colour escapes in text, how a set of sliders shares out what one of them gives up, and where an element goes on a canvas that scales. And of the map: which units have a counter and how they stack, what a counter says and where its parts go, and the painting of one out of its plate, its mask, its unit's picture and its size mark; the buildings that show on the map and which unit a click on a port or an airfield picks; the rows of the window of orders; which arrow a unit's way is drawn with; and what a battle shows - its name, the attacker's share of the two sides' worth, the marker's colour and number, the labels of each side and the icons of what is in play |
| `src/Map` | The map, political or by terrain: drawn by a shader from a province-id texture and two colours per province - its owner's, and its holder's striped over that where they differ - with pan, zoom and picking. And the units on it, as counters: a stack to a province, the near counter or the far one by the zoom, a click to select one, a right click to send it somewhere and the way it is going drawn; and the buildings, the port and the airfield among them, with a click that picks a unit in one; and the markers of the battles the player has a part in, with a click that opens one |
| `src/Gfx` | The Godot client's side: decodes the original's DDS/TGA textures and sprite declarations, and finds the typeface for each font the interface files name |
| `src/Gui` | The interface runtime: builds a window from the original's `.gui` declaration - its icons, buttons, checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements, in the file's order and at the file's positions - and finds them again by name |
| `src/Screens` | The screens, one class each. The top bar: the date, the country, its resources, IC, manpower and the rest, and the buttons that stop, slow and speed time. The production screen: the stockpiles with what the day did to each, the six sliders that divide IC, and the build queue with buttons that move a line and take it out. The four windows it opens to order a unit: the division designer with its brigade picker, and the ship, air and brigade builders. The technology screen: the four sliders that divide leadership, the tree a folder at a time, the theories and practicals, the research queue and a panel for one technology. The alert that says units are waiting, and the deploy menu it opens: the units waiting, the units one of them may join, and a click on the map to place it. The panel of the unit selected on the map: a list of the units above it, its header, its status, its order, its buttons, its brigades, the units under it and the battles it is in, each a class of its own. The window of orders for a fleet or an air unit. And the window of a battle, with its two sides and a line for each unit on them |
| `tools/OpenHOI3.SaveCheck` | Compares what the libraries compute with a savegame the original wrote, field by field; and, given two saves of one game, what a day of research adds with how far each technology moved between them. It also sets every starting brigade beside the save's, by number, and runs the save's two days of rotation, reinforcement and upgrades to set each beside it again, its organisation and its build queue included; and the queue again between two saves, run forward from the earlier and set beside the later. And the edges between provinces against the original's own cache of them, and what an hour adds to a marching division against two saves hours apart, with where the hours between took every fleet and air unit. And the distances by sea against the original's own cache of those. The oracle for everything above - but for a battle, which no save has: `battle` fights one with the install's own units and says how it went, as a trial |
| `tests/` | 997 tests over the parsing, the layering, the map, the numbers, the tick, modifiers and strategic resources, IC, resources, the stockpile, production, the conversions, leadership, research, manpower, unit types, historical models, orders of battle and what a brigade starts with, what units draw each day, the officer ratio and organisation, the build queue, what a country may build and what a new unit is called, the interface files, font descriptors, the sliders, the three screens' figures and the builder windows', the counters and who may join whom, the edges between provinces and a unit's march, what the weather does to it, the unit panel's figures, ports, the distances by sea, a fleet's and an air unit's orders and range, the buildings on the map and the window of orders, a land battle from its first hour to the province taken, what the hour makes of a unit in one, day and night, the tactics and their file, and what a battle shows, where silent misreadings are expensive |

Not built yet: the cached asset build, every screen but those, and nearly every
system the tick is there to run. The game also does not stop at the end date.

In the game: the mouse wheel zooms, a middle or right drag pans, the arrow keys scroll, a
click selects a province - or, on the top counter of a stack, its unit, and clicked again
the next unit under it - a right click sends the selected unit to the province under the
pointer, with shift after whatever it was already told, and on its own province halts it,
and Tab switches between the political and terrain maps. Sent at a province an enemy
stands in, a land unit fights for it from where it is: its arrow is red, its panel says so
and ends in a row for the battle, a round marker with a number stands between the two
sides, and a click on the row or the marker brings up the battle's window, whose cross
closes it and whose button at the upper left calls the player's units off. A selected
province has a readout at the left - a second click on it puts the readout away - which
says who is attacking whom there and for how long, and a province that changes
hands takes the colour of whoever now holds it, with thin stripes of its owner's. A click on a port's or an
airfield's badge selects a fleet lying there or an air unit based there, and again the
next; a right click with one of those selected brings up the orders for that province -
tick one and accept - and with shift sends a fleet straight there. A selected unit's panel comes up
at the left: a click on a unit above it or under it selects that one, the cross beside its
order halts it, the button on its province's name takes the map there, and the three
boxes switch its priority, its reinforcement and its upgrades. While it is selected lines
on the map join it to its commander and the units under it to theirs, and H switches them
off and on. When a unit is finished an icon comes up under the top
bar; a click on it opens the list of units waiting, a click on one of those chooses it,
and then either a click on a green province places it there or a click on a line of the
list puts it with that unit. Space or
Pause stops and starts time, and plus and minus change its speed; so do the top bar's own
buttons, and clicking its date. The top bar's Production and Technology buttons open
those screens, and Escape or the cross closes one. On either, a slider is dragged, a
right click locks it, and the button under its title sets it to what it needs; on the
technology screen a click chooses a technology and a double click starts or stops its
research; on the production screen a line of the build queue has four arrows that move
it and a cross that takes it out, and the Division, Air Wing, Flotilla and Brigade
Attachment buttons each open the window that orders one: pick from the list, set how
many, and Start Production. In the division designer a click on a brigade adds it and the
cross on its line takes it out; in the ship builder the button beside a technology
chooses the level to build with; a held plus or minus repeats, and steps by ten with
shift. A game starts paused, as Germany - `--country=ENG` after `--` on the command line
plays another, `--order=infantry_brigade,artillery_brigade` orders a unit of those
outright, `--all-units` gives the country played every unit type to order whatever
technology or country its file keeps it to (*Play OpenHOI3 (all units)* under `F5` starts
that way, and *Play OpenHOI3 (choose country)* asks for both), `--war=GER:POL` puts two countries at war - there is no other way to start one
yet - `--builder=ship:destroyer` opens a window with that picked, `--days=8` runs
that many days at once - long enough for the ship Germany starts with six days from done -
`--screen=deploy` opens the deploy menu, and
`--click=563,349;r700,573;h48` clicks the mouse at those points of the canvas in turn -
with `r` the right button, with `s` shift held; `h48` lets 48 hours go by and `kh`
presses the H key. **A
screen is checked with `--click`, not only with a switch that reaches past its buttons**:
the four builder windows passed every check that opened and filled them by name while
seven of their buttons could not be pressed.

An earlier iteration reproduced the original's front end directly from its `.gui` files
and ran its main menu and lobby. That work left the tree in commit `4269708` and remains
in git history. It is where the lessons below come from, and today's `src/Gui` started
from its - though the lobby it drove is not coming back.

## Requirements

| Tool | Version | Notes |
| --- | --- | --- |
| Godot | **4.7.1 .NET/mono** build | The plain build cannot run C# |
| .NET SDK | **9.x** | Godot 4.7.1 refuses to start its build tooling without an SDK matching its host runtime |

The project targets `net8.0`, which is what Godot 4.7.1's project generator emits. An
SDK 9 builds that fine - the framework version and the SDK version are independent.

## Building and running

```sh
dotnet build OpenHOI3.sln          # game
dotnet test tests/OpenHOI3.Tests   # tests

# check against a savegame the original wrote on the start date, with no mods
dotnet run --project tools/OpenHOI3.SaveCheck -- "<install folder>" "<savegame>.hoi3"
```

From VS Code, press `F5`. Five launch configurations are available: *Play OpenHOI3*,
*Play OpenHOI3 (all units)*, *Play OpenHOI3 (choose country)* - which asks for a country's
tag and whether it may order every unit type - *Play OpenHOI3 (headless)*, and *Open Godot
Editor*. The Godot executable path is read
from the `godotTools.editorPath.godot4` setting rather than hardcoded, so set it in
`.vscode/settings.json` (git-ignored) or your VS Code user settings.

To run with content, place a Hearts of Iron 3 install in a `Hearts of Iron 3` folder
in the parent of this repository.

## Repository layout

```
scenes/Main.tscn        main scene (run/main_scene)
fonts/                  the open typefaces all text is drawn in, with their licences
src/                    the Godot client
src/Map/                the map view, and the unit counters on it
src/Gfx/                decoding the original's textures and fonts
src/Gui/                the interface runtime: windows from the original's .gui files
src/Screens/            one class for each screen: the top bar, production, technology,
                        the builder windows, the deploy menu, the alerts
lib/OpenHOI3.Sim/       our own data model and the simulation: the game state, the tick,
                        the clock, the two kinds of number and the generator of chance
                        are at its root
    Commands/           every change to the game state
    Provinces/          a province, its terrain, a building
    Countries/          a country, its tag, its laws, its ministers, its wars
    Modifiers/          what a modifier is, and the rebuilding of a province's and a country's
    Economy/            goods, resources, IC, the stockpile, the six shares, manpower, leadership
    Research/           technologies, their categories, the queue and the decay
    Units/              units and brigades, their types, what a day does to one, and its
                        organisation hour by hour
    Movement/           where a unit may go, the way there, its speed and its hourly march
    Combat/             a battle and its two sides, the front, the round, the damage,
                        falling back, the attack delay, and a province changing hands
    Weather/            the weather over a province, what it does to a march and to a
                        battle, and whether it is night there
    Construction/       the build queue: a line, its brigades, the rules, and placing what
                        comes out
lib/OpenHOI3.Content/   reading an install: finding and layering it, localisation, the loader
    Clausewitz/         the script format
    Map/                the province bitmap, shapes, the edges between provinces with their
                        kinds and distances, terrain, a province's points, and the
                        declarations of the unit counters, their pointer and the arrows
    Definitions/        what common/, technologies/ and units/ declare
    History/            what history/ gives on the start date, orders of battle among it
    Gui/                the interface files' declarations, and font descriptors
lib/OpenHOI3.Interface/ what the screens show, with no engine: sliders and placement at its root
    Text/               colour escapes, number formats, templates
    Screens/            the text of each screen's figures
    Map/                the unit counters: which units have one, how they stack, what one
                        says, where its parts go, the painting of one, and the shape of
                        the arrow along a selected unit's way
tools/OpenHOI3.SaveCheck/   checks the libraries against a savegame the original wrote
docs/mechanics/         one spec per system: what the original computes, and how sure that is
docs/compatibility.md   where a valid file of the original's is treated differently on purpose
tests/OpenHOI3.Tests/   Sim/, Content/ and Interface/: the tests of each library
```

Nothing under `lib/` references Godot, and `OpenHOI3.Sim` references nothing at all. The
tests run against those three as a plain .NET project, with no engine behind them.

A folder is a namespace: `lib/OpenHOI3.Sim/Units/CUnit.cs` is `OpenHOI3.Sim.Units.CUnit`.

## What the original taught us

The original is worth studying as a design reference even though we are not copying it.
Two things came out of the reverse engineering that are worth keeping in mind.

**Its class structure is recoverable in full.** `hoi3_tfh.exe` is a 32-bit MSVC build
shipping complete RTTI, so class names, inheritance, base offsets and vftable layout can
be read exactly rather than guessed. The result is 1212 application classes under 58
roots, kept as `ghidra/hoi3_tfh-class-hierarchy.txt` in the reverse engineering
repository described below. That is a map of how a shipped grand
strategy game divides its responsibilities, which is useful whatever technology we
build on.

**Its data formats carry a lot of accidental history.** The interface files name
textures with the wrong extension, the font descriptors declare a texture page and then
omit the line naming it, one scrollbar definition looks as though its arrow sprites are swapped until it is
read from the bottom up, and a
map editor is present but hidden by two `Hide()` calls because it was never finished.
None of that is worth reproducing; all of it is worth knowing about when reading these
files, because each quirk is a case where a straightforward reader silently produces
something wrong rather than failing.

## Studying the original

The reverse engineering of `hoi3_tfh.exe` is its own repository, `hoi3-reversing`,
normally checked out beside this one. It holds the write-ups this project takes its
mechanics from, the class hierarchy mentioned above, and the one Ghidra toolchain.

This repository had Ghidra scripts of its own under `tools/ghidra/` until 2026-10-07.
They are gone: the two that were still in use moved there, and the rest are in this
repository's history.

What this project takes from that work is **mechanics** - formulas, orderings, rounding,
caps - and never an address or an offset, which describe one build of the original's
executable and mean nothing to an engine that is not built as it is.
