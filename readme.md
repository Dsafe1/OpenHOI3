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
| 3. Units and war | Orders of battle, units on the map, movement, land combat, supply; then air and naval | Two countries can fight a land war to a result. **The unit types and the orders of battle are in (2026-10-08): every brigade, ship and wing the base game starts with, holding what the original gives it on the first hour, checked one by one against a savegame. So is what a unit draws each day - supplies and fuel, the men who go home, reinforcement and upgrades - checked brigade by brigade two days on; and the officer ratio with what it does to organisation, hour by hour, checked the same way. And the build queue: what a unit costs and takes, the day's work on it, and the unit that comes out, checked against five saves of one game over four months - with the division designer and the ship, air and brigade builders that order one, read out of the original and not yet looked at beside it. And the units are on the map (2026-10-09): a counter for every unit that is not in port or at its base, stacked by province, and the menu a finished unit is placed from. And land units move (2026-10-09): an order by a right click, a route found as the original finds it, and an hourly march at the original's speed over the original's distances - every one of the map's 83,596 edges checked against the cache the game keeps of its own. And a selected unit has its panel (2026-10-09). And fleets and air units are sent about (2026-10-09): the port and the airfield on the map a docked fleet and a grounded wing are picked by, the window of orders a right click brings up, a move and a change of base, the range, and the sea's own distances - all 17,621,016 of them checked against the cache the game keeps of them, and the sailing and the routes against savegames. And land units fight (2026-10-09): a unit walking at an enemy starts a battle and fights it from where it stands, the front and the reserves, the hourly round with its damage, the side that gives way and falls back, the province taken, and the delay an attack leaves - read out of the original and not watched, there being no savegame with a battle in it. What a leader does to a battle is not in yet. And units are supplied (2026-10-10): a network of depots, the day's carrying province by province, and convoys across the sea - the network set against three savegames province for province, the carrying against a fourth in 184 networks of 234, and all 261 of that save's convoys made again way for way. And fleets fight at sea (2026-10-10), the first of five pieces of air and naval combat: the sighting, each ship's own distance, who it shoots at, sinking, breaking away and falling back - read out of the original and not watched, like the land battle** |
| 4. Politics and script | Events and decisions with their triggers and effects; diplomacy, factions, politics, espionage, occupation, revolt | The base game's events load with no unknown key and fire on schedule. **The first half is reached (2026-10-12): every event and decision of the base game is read, with no unknown key** |
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

As of 2026-10-13, in stage 3 with stage 4 built as far as items 6 and 7 cut it - events and decisions, and diplomacy through factions - and the economy of stage 2 waiting on them. Kept
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
(`docs/mechanics/combatview.md`). And they are supplied along a network
(`docs/mechanics/supply.md`): every province draws on a depot, supplies and fuel go out to
it province by province, and a unit draws on the province it stands in. And across the sea
by convoy (`docs/mechanics/convoys.md`), with a map mode that shows how it all stands
(`docs/mechanics/supplymap.md`). And fleets fight one another at sea
(`docs/mechanics/navalcombat.md`), and have their missions, the raid on convoys among
them (`docs/mechanics/navalmissions.md`).
In order:

1. **Air and naval combat**, which is what stage 3 has left. Cut in five on 2026-10-10 -
   **the cut is this project's: the maintainer said to go on, and has not spoken to it**:

   - **the battle at sea - built** (2026-10-10, `navalcombat.md`): fleet against fleet;
   - **a fleet's missions - built** (2026-10-10, `navalmissions.md`): reserve, patrol,
     intercept, sortie, the raid on convoys and their escort, and the battle a raid fights.
     **Transport and invasion are not**: they wait for carrying, which is its own work;
   - **the battle at sea on screen - built** (2026-10-10, `combatview.md`, section 10): its
     row in a fleet's panel and its window. **It was built with the land battle's marker on
     the map too, and that was taken away the same day**: the reading for the air side
     found that the original's marker is a land battle's alone. **The maintainer saw the
     marker when he looked and has not been asked.** He put this piece here on
     2026-10-10 - "Fleet missions first, then the combat sceen, and then we tackle the
     games 2 days" - ahead of the two pieces below. **He looked at it that day: "The sea
     battle window looked perfect"**; no picture of the original's was measured against it;
   - **the battle in the air - built** (2026-10-10, `aircombat.md`): air superiority and
     the intercept, a mission's cycle from its base and back, what a country knows of a
     province, and the four hours an air battle lasts. **Read out of the original and set
     against nothing**, and the maintainer has not looked at it;
   - **bombing - built** (2026-10-10, `bombing.md`): ground attack and interdiction, the
     four strategic kinds, the strikes at ships and at ports, anti-aircraft fire, what a
     bomb does to a province and what comes back each day. **Read out of the original and
     set against nothing**, and the maintainer has not looked at it. **A carrier's aircraft
     are not in it**: a carrier is not a base here at all yet, which is its own work;
   - **the battle in the air and the bombing on screen - built** (2026-10-10,
     `combatview.md`, section 11): the row in a unit's panel and the window, whose list for
     the bombed side of a bombing is the province's buildings, stockpiles and production
     that the bombers' mission bombs, each with a bar of what is left of it. **Neither has
     a marker on the map: the original gives none**, to them or to a battle at sea. **Read
     out of the original, tried with real clicks, and the maintainer has not looked at
     it**;
   - **the rest of the air orders window - built** (2026-10-11, `fleetsandair.md`, section 4,
     and `aircombat.md`, section 2): the four shapes of a mission's area - point, region,
     circle, cone - with the three sliders, the right click that aims a circle or a cone and
     the shift that adds to one, the boxes of the hours, the two dates and "continuous", the
     readout of the day, the ladder that ticks the first row, what it remembers for the
     next, and the map's colours while it is up. **Read whole out of the original, tried with
     real clicks, and the maintainer has not looked at it.** Nine things in it are the
     original's and look odd, and are built so (`fleetsandair.md`, section 4, and below),
     none put to him: the priority is shown and never sent; a slider must move a twentieth
     before it counts; the figures show the sliders and not what a right click aimed; a cone
     loses its part west of north when it starts below north; clearing the ticked box of the
     hours leaves none ticked; the hint under the sliders is empty until accept is first
     greyed; the second rung of the ladder replaces the first; a month on keeps the day and
     runs past a short month; and the title stays when a right click moves the window;
   - **the air map mode - built** (2026-10-11, `airmap.md`), the last of the air side on
     screen: grey, green where the player's intercepts reach and red where its air
     superiority does; a ring round each of its air bases for each range of aircraft there,
     red to green by their strength; the air bases and anti-aircraft guns as large pictures
     by their level, the large air base clicked before any counter - which is how an air unit
     at its base is picked in this mode; the three icons of the week's air battles and
     bombings; and the province's two lines. Under it, **a list of finished battles that
     keeps a week**, every battle of every kind going on it as it ends (`CCombatHistory`).
     **Read whole out of the original, tried with real clicks, and the maintainer has not
     looked at it.** Four things in it are the original's and look odd, and are built so
     (`airmap.md`), none put to him: its colours are not made again each day, only when the
     player's fighter orders start or go; a count of battles is a signed byte, so 128 in a
     week hide the icon; a beaten side is named by nobody; and an air base the player knows
     little of is drawn at the size of a level 1. Reading for it corrected the fact base
     twice. `Tab` reaches it after supply, and `--mode=air` starts in it.

   With that, **stage 3 has what it set out to have on the air side**; transport and
   invasion, and a carrier's aircraft, wait for carrying, which is its own work.

   **Bombing** (`bombing.md`): a unit on a bombing mission flies out to a province of its
   list, bombs it for four hours and is told to return the hour after; home, it flies again.
   **A bombing is a battle of its own kind**, one to a province - of the ground, of land
   units or of ships - and a unit in one is not "fighting": a division under bombs marches
   and attacks as before. Each hour every wing fires at one target its mission goes for and
   then **bombs the province**: whole runs of its strategic attack, never fewer than one,
   each a die's worth off the buildings, the stockpile and the day's production its mission
   names. The province fires back with its guns and with one and a half times every friendly
   unit's air attack. **A building now has two levels** - what was built and what works of
   it - and a province's IC, roads, guns and radar are made from the second; what was bombed
   comes back a little every day, faster on good roads (`CProvinceRepair`).
   `OpenHOI3.SaveCheck bombing` flies one with the install's own units: four German wings
   over Poznan leave a fifth of its one factory after the first sortie and a tenth after
   three days. **Nine things in it are the original's and look odd, and are built so**
   (`bombing.md`, section 12), and **none has been put to the maintainer**: a bomber with no
   leader finds a ship one time in a hundred, so strikes at ships wear the port and hardly
   touch the fleet - ten in a hundred more for each point of its leader's skill; a fleet in its port fires its air
   attack three times over; two units cannot bomb one city's ground together; interdiction
   flies only at ground its country sees into, which here is none of an enemy's. In the
   client `--mission=GER:strategicbombing:1928` gives a country's air unit a mission, and
   the orders window gives all eight for the province clicked.
   **Reading for it corrected the air battle**: the hour in which a unit out of a battle
   starts no other ends with its first move, so one that flies on may fight at once where it
   arrives; and it named the radio's and the radar's technology keys, which are now in an
   air battle too.

   **The battle in the air** (`aircombat.md`): an air unit under air superiority goes round
   the provinces of its mission and flies at the strongest enemy air unit it knows of in
   them, one province ahead of it; one under an intercept waits at its base, watches its
   provinces and two steps around them, flies to meet the strongest enemy on its way in,
   and turns for home when it has caught it. A battle begins when a unit under either order
   finds an enemy air unit where it has arrived or stands - **anything in the air may be
   fought, and a unit sitting at its base only by a fighter and only while it is itself fit
   to fly**. Each hour every wing fires its `air_attack` in shots at one enemy wing, about
   half hit, and a hit costs a little organisation and less strength; after four hours it
   is over, a side worn to nothing falls back, and the units have an hour in which they
   cannot be fought again. A mission outlives its sorties: home under "return to base" a
   unit goes out again until its end hour. `OpenHOI3.SaveCheck airbattle` flies one with the
   install's own units: three wings of German bombers over Warszawa fight Poland's fighters
   ten times in two days and turn for home at two thirds of their strength, and the Poles
   catch them eight times more on the way.
   **Eight things in it are the original's and look odd, and are built so** (`aircombat.md`,
   section 9) - a beaten unit beside the sea flies out to sea, one too worn to fly cannot be
   attacked at its base and yet takes off every third hour, a wing's defence and its
   `air_defence` do nothing. Of that last the maintainer on 2026-10-10: "Yes thats a bug in
   the original"; of the others, "i dont know of the top of my head. Just note them" - so
   they are noted there and nothing is changed. In the client `--mission=GER:airsuperiority:1928` gives a
   country's air unit a mission, and the orders window gives both for the province clicked.

   **The new game's two days - built** (2026-10-10, `tick.md`, section 6), by the same word
   of his. A new game no longer starts at its history files: as it is made it runs what the
   original runs before a game's first hour - a day, the filling of the provinces, a day, a
   month's pass and one hour, the clock standing still and the game not yet "running", which
   is when a brigade may have its own figure of organisation and nothing else, nothing
   decays and a convoy is manned for nothing. So two days of production, research, building,
   upgrades and rotation are behind the first screen. **Checked like for like, it is the
   start save's Germany**: given the laws that country's days were run under, every one of
   its 172 brigades, ships and wings has the save's strength and organisation, its manpower,
   officers, influence, abilities, technology and build queue are the save's, and six of its
   seven goods to the thousandth - supplies are 0.030 over in 8410, and were 3.3 under
   before what leaders do to a unit's use was built.
   **What a player gets is still not what the original's first screen shows**, and is not
   meant to be: there its AI has changed laws by then, the player's among them, and here
   the player's country keeps its history's (`docs/compatibility.md`, entry 6).
   `OpenHOI3.SaveCheck firstdays` sets the two side by side.

   **The battle at sea** (`navalcombat.md`): a fleet of a country at war looks each hour
   at the first enemy fleet in its sea province and rolls to see it, ship by ship; a
   sighting starts a battle and every other fleet there joins the side whose enemies are
   its own. **Each ship stands at a distance of its own** - two ships reach one another
   when their two distances together are within the shooter's reach - and each hour it
   picks a target by its side's positioning, fires, and closes the distance or opens it.
   Damage is two dice over the target's hull; a ship has no defence but that. A side
   whose ships average under one organisation has lost and its fleets run for the port
   they lie off; a battle nobody loses ends when the hourly roll to break off, which
   gets likelier by the hour and much likelier at night, has set the fleets counting and
   then passes again; and a fleet told to leave counts four hours before it goes.
   **Read out of the original and set against nothing**, like the land battle: no save
   has one. `OpenHOI3.SaveCheck seabattle` fights one with the install's fleets:
   Germany's battle fleet gives way to the Mediterranean Fleet after 17 hours and falls
   back to Wilhelmshaven, one British destroyer sunk; a Japanese and an American
   squadron break off after ten hours, two cruisers down. **Reading it found three things
   nobody had written down**: every ship has its own distance and closes it at a quarter
   of its speed; a ship that has lost the enemy shoots at its own side seven times in a
   hundred; and a fleet cannot be taken out of a sea battle in its first four hours.
   **Two things about it are the original's and look wrong, and are built so**: an idle
   fleet of a country nobody plays starts no battle, and a crowded fleet's stacking
   penalty leaves its ships hardly ever finding a target. **There is no AI**, so no
   enemy fleet leaves port: `--sail=ENG:10531` on the command line sends one to sea, and
   `--war=GER:ENG` with it gives the player's own fleet something to find. **On screen**
   (`combatview.md`, section 10) a battle at sea has the land battle's row and window - and
   no marker on the map, which is a land battle's alone - the window with halves of its
   own: each side's positioning, and a list of its
   ships whose pictures stand along their rows by each ship's distance, so that the two
   fleets are seen closing across the gap between the lists. Built from a reading and
   tried with real clicks; nothing of it was set against the original.

   **A fleet's missions** (`navalmissions.md`): an order is asked every hour whether it is
   still going, and that is where a mission does what it does. A **patrol** goes round
   the sea provinces beside its own, shadows a foreign fleet it sights - which makes that
   fleet twice as easily seen and, from the day's turn, the water known without a roll -
   and every twelve hours tries the convoys where it is. A **convoy raid** is the same
   round with no shadowing, and a raider starts no battle between fleets. An **intercept**
   waits in port, sees an enemy in its province or beside it without rolling, and goes
   for one no stronger than itself. A **sortie** sails out and turns for home on
   arriving. An **escort** sails up and down the stretch of a convoy's route within its
   range. A **reserve** lies in port and once a day gives its sound ships for the worn
   ones of the fleets beside it. A fleet of the player's too worn for its stance makes
   for the nearest harbour. **A raid on a convoy is a battle**: the convoy's escorts and
   transports become a throwaway fleet of destroyers and transport ships, five rounds of
   the battle at sea are fought on the spot, and what that fleet lost comes off the
   convoy - which then wants escorts, is given them, and keeps out of that water.
   **Read out of the original and set against nothing.** `OpenHOI3.SaveCheck raid` sends
   the install's raiders at the install's convoys: Germany's battle fleet in the Channel
   Approach sinks 37 British transports in a month. **Six things in it are
   the original's and look wrong, and are built so** (`navalmissions.md`, section 12) -
   the plainest that **an interceptor goes out once** and then lies in port under its
   order, and that **a patrol sinks convoys**. The maintainer on 2026-10-10: "All those
   things are correct, and they may be bugs, but thats ok." In the client `--mission=ENG:patrol:10531` gives a country's fleet a
   mission, there being no AI to.

   Supply came before it, cut in three with the maintainer on 2026-10-09, and **all three
   pieces are in** (2026-10-10). **He looked at it in the game the same day** and raised
   nothing against it.

   **The network, built**: the land cut into areas - what one country controls, touching,
   each province with more than one level of infrastructure - areas on one side grouped, a
   depot picked for each group, a depot found for land its group gives none, and every
   province's distance from its own; with what a province can pass in a day and what a load
   loses crossing it. **It agrees with three savegames province for province**: the same
   4,400 provinces out of supply and the same 402 that pass without limit
   (`OpenHOI3.SaveCheck supply`, `supply.md`, section 9). **And he watched the original pick
   a depot**: Manchukuo on Hamhung as the game starts, on Pusan with North Korea released,
   on P'yongyang with both - and the score gives all three. In the original a released
   puppet draws on its own capital for some days first; here it is in its master's group at
   once, and stays so on purpose (`docs/compatibility.md`, entry 9). The maintainer said beforehand that
   a puppet is in its master's network unless the two do not touch, and that allies keep
   their own; both were read so.

   **The daily pass, built** (`supply.md`, sections 11 to 17): once a day, before any
   country's own day, the provinces are gone through furthest from their depots first; each
   works out what it is short of fifteen days of what its units ask for, takes what the
   provinces nearer the depot held the day before, and passes the rest of its want inward;
   then what any province holds spare goes one province inward. **A unit draws on the
   province it stands in** - its share of what lies there - and on a friend's depot its
   country pays the friend. **A marching unit takes its fifteen days with it.** **A day's
   supplies and fuel are made where the factories are** and come to the stockpile a province a
   day. And **a new game fills its provinces first**: the pass run 251 times over, with the
   stockpiles put back each time, in about two seconds, between the two days it runs before
   its first hour. The readout of a selected
   province says what lies there, what its units ask for and what the day asked of it, and
   the unit panel's supply and fuel bars are real.

   **It reproduces the original's own figures**: with the units, laws and production of the
   maintainer's start-date save, a day, the filling and a day give **184 of 234 networks the
   same in every province and every figure the save keeps** - Germany's 245 provinces,
   Sweden's 210, Turkey's 195 (`OpenHOI3.SaveCheck supplypass`, `supply.md`, section 17). It
   was 124 before the convoys and 171 before what leaders do to a unit's use.
   The others differ by what goes in: the original's AI has units on the move and has moved
   shares of IC. **Reading it found four
   things nobody had written down**: what a load loses is an amount and not a share; a
   marching unit carries its supplies; a puppet's stockpile drains into its master's
   network; and the original runs a day *before* it fills the provinces, so what that day
   made away from the capitals is lost.

   **The sea and the screen, built** (`convoys.md`, `supplymap.md`): a country's supply
   convoys are made for it - at peace one to every harbour of each block of its land across
   the sea, the depot's first - each finds its way by the original's search, wants so many
   transports for the length of its way and the size of the harbour it ends in, and lands
   each day what those harbours have room for; resource convoys bring home what cut-off land
   yields, which waits in the block's largest harbour; a country's transports and escorts are
   shared out among its convoys each day, supply convoys first; and a new game gives every
   convoy its ships for nothing and every country a free transport for each two of its base
   IC. **All 261 convoys of the maintainer's start-date save come out of it**: the same runs
   with the same goods, the same way province for province, the same transports and escorts
   on each, and the same free ones left over for all 20 countries that have any. **Reading
   it found three things nobody had written down**: an area's harbours are kept largest
   first, so "the first harbour" is the largest; a convoy lands in harbours and the daily
   pass carries it to the depot; and the yield of land that draws on a depot in the capital's
   block counts as produced at home. **The supply map mode** colours the player's land green
   where it holds what was asked of it, yellow where it is behind, red where it is cut off,
   with red stripes on a road that is full - `Tab` goes round the political map, the terrain
   and supply - and the tooltip of the province under the mouse says what the original's
   does, line for line and in its colours. Nobody attacks a convoy yet, none is built, traded for or lent, and there is no
   list of them on the production screen.

   **The areas were wanted by three older rules**, each of which had guessed at them: home
   production, the `overseas` modifier and where a finished unit may be placed. All three
   now use the area as read, and the save's home production of metal is right for 78
   countries of 78 where it was 76 (`supply.md`, section 10).

   Land combat came before it, in three pieces, and all three are in (2026-10-09).
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
   181 hours, Japan takes the town of Tianjin in 347, and a German division sent across the
   Rhine at the Maginot Line attacks at four hundredths of its strength.

   **What is on screen was set against the original**: three screenshots of a battle of his,
   of the same day - the marker, the panel's row and the window. They corrected four things
   about how the interface draws, none of them about battles: a bar's picture is drawn
   upside down, a turned flag turns about its middle, a round shield cuts its flag to a
   disc, and a name too long for its box ends in three dots (`interface.md`, sections 5 and
   7). With those the window's upper half lies on his to within a pixel. **He looked at
   ours on 2026-10-09** - "Looks very good" - and asked that opening the window should not
   move the map, which the original's row in a unit's panel does (`docs/compatibility.md`,
   entry 8).

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
2. **Leaders** (`leaders.md`), which the maintainer took up next on 2026-10-11. Cut in four
   the same day - **the cut is this project's**:

   - **who they are and who commands what - built** (2026-10-11): `history/leaders` and
     `common/traits.txt` read, a leader's skill from his experience, who is in service as a
     game starts and from the day his history first gives him a rank, the rank each level of
     a unit takes, and **the choice of a leader for every unit that has none** - his skill
     and a set amount for each of his traits that suits the unit, the first of equals
     winning - as a game is made and as a unit is put on the map or joined. **It is the
     original's unit for unit**: the start save's 1555 unit leaders and 101 countries'
     leaders in service all agree (`OpenHOI3.SaveCheck`). The panel shows a unit's leader,
     in red where he has not the rank, and his portrait. **Eight things in it are the
     original's and look odd, and are built so** (`leaders.md`), none put to the
     maintainer: an order of battle's leader goes on whoever he is; the day's putting in
     service gives rank 1 and nothing later in a history is applied; a division of one
     brigade is never given one; the paratroop trait never counts towards choosing; the
     defensive one suits only a unit with a garrison brigade; the rank that counts is
     looser than the rank that is chosen; a trait's figure plays no part; a dated skill
     does nothing;
   - **how far a command reaches - built** (2026-10-11, `leaders.md`, section 8): a
     headquarters reaches in full within its radio's radius - 200 km for a corps, 400 for an
     army, 600 for an army group, 2000 for a theatre - less out to twice it and not at all
     beyond, the links multiplied up the chain; a leader's trait comes down the
     chain halved at every step; the army's commander adds his skill by his reach to every
     brigade's organisation ceiling, and the army group's takes a twentieth a point off what
     its units use, with the `supply_consumption` traits. The radio icon of the unit panel
     and the broken dark red line on the map show a unit out of range. **Read whole out of
     the original and checked against the start save**: the organisation of all 764
     brigades under a commander agrees (142 before), 62 of 65 capitals' use of supplies
     (38), and 184 of 234 supply networks in every province and figure (171). Each rank's
     skill has its own part, as the maintainer set out on 2026-10-11 - the division's,
     corps' and theatre's are in battle and wait for the next piece. **Two more things in
     it are the original's and look odd, and are built so**: the reach drops from half to
     nothing at twice the radius, and a new game's stockpile is reckoned before its leaders
     are chosen. The first was put to the maintainer that day; the other nine have not been;
   - **what a leader does in a battle**, cut again the same day, the cut this project's:
     - **in a land battle - built** (2026-10-11, `leaders.md`, section 9; `landcombat.md`):
       a unit's leader's bonus - 5% a point of skill, with his offence or defence trait and
       his trait for the ground - every other trait in the figure it names, the theatre
       commander off crowding, the corps commander bringing reserves up, a defender's leader
       in the order of its front, a leader in shattering, a side's highest ranked leader in
       its tactic and the `trait` condition read for it, and the chain's speed traits in a
       march. **Read out of the original and set against nothing**, as the land battle is;
       reading it confirmed the defender's front order off the instructions and found a
       corps in reserve counting its own commander. **Three more things look odd and are
       built so**: a unit whose own leader lacks the rank or the skill gets none of the
       leader's bonus, its commanders' offence and defence traits with it; `fort_defence`
       and `surprise_chance` do nothing; the `trait` condition asks the leader's own traits
       only. The aggressiveness a tactic is picked with stays 0: plans are the AI's;
     - **at sea and in the air - built** (2026-10-11, `leaders.md`, section 10;
       `navalcombat.md`, `aircombat.md`, `bombing.md`): a unit's leader's bonus on every ship
       and wing - his skill alone, no trait - where he has the rank; his skill, whatever his
       rank, in drawing a fleet up, in a ship's or a wing's choice of target and in a bomber
       finding ships; a side's leader in its positioning at sea and against friendly fire;
       and eleven traits where they name, spotting to strategic attack. Two technology
       figures went in beside the admiral, `targeting_chance` and `targeting_choice`.
       **Read out of the original and set against nothing**; most of it had been read
       before, and the reading named those two figures' keys in the record, and found the
       fact base's "an admiral of skill 5 draws his ships up at exactly half their reach"
       missing the floor that leaves one further. **Two more things look odd and are built
       so**: an admiral too junior for his fleet still draws it up and picks its targets,
       and `dissent_impact` does nothing at sea or in the air;
     - **the experience and the traits a battle gives - built** (2026-10-11, `leaders.md`,
       section 11): every unit's hour in a battle or a bombing gives experience - a die, the
       size of the enemy, the defines and the `xp_gain` traits - to each brigade, ship and
       wing, to its leader and, at a share each step, to every commander above; a leader's
       skill rises from it, faster the further below his best. Each side's leaders bank their
       hours in battle and have them counted, when a unit leaves the side and at the end,
       towards the traits of `common/gainable_traits.txt` whose condition holds - six
       conditions read for it, the tactics' condition made a battle's. **Read out of the
       original and set against nothing**; the start check still agrees leader for leader.
       **Four more things look odd and are built so**: a raid never counts its raider's
       hours, so `seawolf` cannot be earned by raiding; `has_combined_arms = no` holds like
       `= yes`; a battle broken off is won by both; a ship with no target learns nothing;
   - **the window a leader is chosen in - built** (2026-10-11, `leaders.md`, section 12), with
     his place in a battle's window: opened from a unit's leader, it lists the owner's leaders
     of the unit's kind who are free, through a filter of traits, sorted by name, skill, traits
     or rank **with the original runtime's own sort** - copied step for step and checked against
     the original's code run under an emulator on 1,360 lists, since which of equals comes
     first is the sort's - and posts four commands: a leader given, a rank up or down, every
     leader taken off after a confirmation, and the country's own choosing turned off. A
     battle's window shows each side's highest ranked leader, his portrait and the arrow of
     initiative. **Read out of the original and tried with real clicks; the maintainer has not
     looked at it.** Reading for it corrected the fact base's rule for when the unit panel's
     leader button may be pressed. **Six more things look odd and are built so**, none put to
     him: the portrait opens the window where the button cannot be pressed; nothing asks whose
     unit it is; an expeditionary force's refusal is undone by the original's own last test; a
     general promoted is taken off his unit while the window's head keeps him; a filter with
     everything off is forgotten; and "remove all" closes the window before it is answered.

   **With that, leaders are built** as the cut set out; the AI's own reshuffle, leaders events
   make, and the window's tooltips wait (`leaders.md`, *Not built*).

3. **Tooltips, for everything built so far** - the maintainer's word on 2026-10-11, after
   leaders and not before: "Lets finish the leaders as we planned, then after that lets
   implement tooltips for all the things we have already." **The mechanism is built**
   (2026-10-11, `docs/mechanics/tooltips.md`): read out of the original, tried with `--click`,
   and the maintainer has not looked at it - the one box from `core.gui`, a tooltip's delayed
   half under a line of dashes once the mouse has been still, an element's tooltip hanging 64
   below its corner, the box kept inside the right and bottom of the screen, and a button's
   shortcut, which is all that shows yet. **No `.gui` file sets a tooltip of its own**, so
   every tooltip is a screen's code; what each says is read and built a screen at a time, in
   this order, the cut this project's:
   1. **the map's - built** (2026-10-11, `tooltips.md` section 7): the province under the
      mouse, `ProvinceTooltip_Build` read whole - a selected army's warning, the name and
      owner, the flags (`@TAG` in text draws a flag), the terrain, political, supply and air
      modes' parts, and the distance from what is selected. Tried with `--click`; the
      maintainer set it beside the original - "the style looks good" - and on his screenshot
      the tooltip stands over the minimap, which waits for the minimap. The readout no longer
      carries the supply and air lines;
   2. **the top bar's - built** (2026-10-11, `topbar.md` section 14): by the element's name -
      each picture a heading with its description to come, each resource's number its day's
      ledger, IC, dissent, diplomacy, national unity, officers and manpower with the figures
      behind them, and **where a modifier comes from**, source by source with its name, which
      four of them end in (`modifiers.md`, section 8). Tried with `--click`; the maintainer
      looked at it on 2026-10-11: "Looks good". Three oddities are kept as read (`topbar.md`,
      section 14);
   3. **the production and technology screens' - built** (2026-10-11, `productionscreen.md`
      section 8, `technologyscreen.md` section 9, `sliders.md` section 6): by the element's
      name - the resources' ledgers, a strategic resource with each province that gives it, the
      bombs, the buttons, a build queue line by its element with its brigades drawn as their
      counters (`£` in text, `tooltips.md` section 7); **what each of the ten sliders' shares
      would do at the slider's position**, with the lock line on the knob; where leadership
      comes from, a tree entry, a queue line with research efficiency, a category icon's speed
      and **the start button's days at full funding**. Tried with `--click`; the maintainer
      looked at them on 2026-10-11: "Looks good". Two parts wait for readings of their own:
      **what a level of a technology does**, which a tree entry's tooltip and the panel's
      `effects` list are made of, and **why a technology may not be researched**, which its
      `allow` block says;
   4. **the four builder windows' - built** (2026-10-11, `builders.md` section 11): each
      window's own routine read whole - **only the column headers speak**, a name and then what
      the column means, the four step buttons say "Ctrl + LMB to add or remove 10", and the
      designer's combined arms icons say their group; **the designer's terrain summary**, its
      brigades' ground, river, night, fort and landing figures averaged; and **the practical
      icon**, how much cheaper the country builds what it teaches. Tried with `--click`; the
      maintainer has not looked at them. The step of ten is Ctrl now, on the tooltip's word,
      where it was shift. Reading for it corrected the fact base: the routine it had as the
      builders' headers is the ledger's;
   5. **a unit's panel - built** (2026-10-12, `unitpanel.md` section 10): every entry's own
      routine read whole - the header, the status, the order with what each kind of order
      calls itself, the brigades with **a brigade's summary** (its model by name, its
      strength, organisation and experience, a ship's speed and range, and the level of each
      technology it carries, a level named by its text or by **a model year**), the buttons, a
      headquarters' row, and **a leader's own tooltip** - his rank, command, skill, how far to
      the next and his traits. A text box over a button now answers first where the mouse is
      over it and leaves the button its clicks (`CGuiObject.PassiveTooltip`). Tried with
      `--click`; the maintainer has not looked at it. Attrition, supply and fuel, a base's
      supply, the speed's terms and the hourly regain wait for what they describe (*Loose
      ends*). Reading for it corrected the fact base: the order of the radio lines' text and
      line break, and that a headquarters out of reach is yellow;
   6. **a unit's other windows - built** (2026-10-12, `mapunits.md` section 10,
      `fleetsandair.md` section 8): read whole - **the deploy menu's lines** list their unit's
      brigades by their counters, a finished fleet's or air unit's line nothing; **the orders
      windows** say why `accept` cannot be pressed, the stance and the priority, the air
      window's shapes, continuous box and day readout, and each row its order's description,
      the fleet's with how far out of reach it is. **Both windows' priority buttons say
      nothing**, the original asking twice for a name its file does not have. A list's rows are
      now searched for a text box that answers (`CTooltips`). Tried with `--click`; the
      maintainer has not looked at them;
   7. **a battle's window - built** (2026-10-12, `combatview.md` section 12): the window's
      routine and every entry's read whole - the terrain, the width, the retreat, the arrow
      between the tactics, **a tactic's own text** with what counters it and what it counters,
      each side's leader with his initiative; **a land unit's whole** - its leader, attack and
      defence, what each combat modifier is worth to it, its side's tactic as the side feels it,
      piercing and armour; **an attacker's reserve its chance of coming up**, where the
      defender's say a unit's whole; a ship's target, positioning and reach; a wing's attack
      and defence; the icons of the modifiers in play; the close button `CLOSE`; and **the row
      in a unit's panel**, each side's units by name and what they come to, with `VERSUS`
      between - where **one fleet's ships run together**, the original's. Tried with
      `--click` on a battle at Chrzanow; the maintainer has not looked at them. A landing's
      lines, the bombed side's rows and a bombing's addition to the row wait (*Loose ends*);
   8. **the leader window's - built** (2026-10-12, `leaders.md` section 13): the window's
      routine read whole - the trait filter first and final, then a row, then by name, and what
      is left to the unit panel header's names, so **a row's button says the header's "Assign
      new leader."**; a portrait the leader's own tooltip, `gaintrait_info` the traits he is on
      his way to, **a trait's icon its name and a line for each effect**, a filter row what its
      trait does, the filter button how it works. The leader's own tooltip now says how many he
      may command only where he commands a fleet or an air unit, which corrected the unit
      panel's reading. Tried with `--click`; the maintainer's screenshot of the original shows
      a trait's icon answering with its name, which settled that the original shows them;
   9. **the technology screen's last two - built** (2026-10-12, `technologyscreen.md`
      section 9; `CRequirementText`, `CTechnologyEffectsText`): read whole out of the original,
      tried with `--click`, and the maintainer has not looked at them. **Why not** is the
      `allow` block's account of itself, every condition with a red or green asterisk and the
      words turned round inside a "none of" - **indented as the mod's DLL corrects the
      original**, by his word (`docs/compatibility.md`, entry 10). **What levels do** is the
      units and building a first level allows, each usable unit type's figures folded onto one
      line, and 46 country-wide figures, the tactics, groups, missions and decay - every
      figure times the level the entry speaks of. `activate_building` and `listening_station`
      are read for it.

   **With that, every screen built has its tooltips**; from now on a new screen comes with its
   own.

4. **A look at the screens beside the original**, figure by figure. It needs nothing
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
5. **What the builder windows leave unread**, none of it in the way of ordering a unit:
   the name and the picture of the historical model a type is shown by, the name of each
   level of a ship's technology and the text of what it does, the description of a wing
   or a brigade, and what saving a template does
   (`reversing/findings/FINDINGS-builders.md`, the end).
6. **Stage 4: events and decisions**, taken up on 2026-10-12 on the maintainer's word ("Ok
   lets go") after the cut was put to him: reading the script; what a trigger answers; what
   an effect does; and an event firing and a decision taken, with the event's window and the
   decision screen. **All four are built** (2026-10-12); what waits for other systems is in
   *Loose ends*. **Then, on the maintainer's word ("Lets finish the event and decision stuff
   first"), three more**: the words of conditions and effects, the on_actions, and messages -
   read out of the original together by a wave of seven - **all three built** (2026-10-12).
   1. **reading the script - built** (2026-10-12, `docs/mechanics/script.md`;
      `Content/Script`, `Sim/Script`): read out of the original by a wave of three
      (`reversing/findings/FINDINGS-eventfiles.md`, `FINDINGS-triggergrammar.md`,
      `FINDINGS-effectgrammar.md`) - the words a file is cut into, an event's, an option's and
      a decision's keys, the mean time to happen, all 153 trigger keywords and 91 effect
      keywords with the shape of each one's value, and the names the original looks a key up
      in when it is no keyword, in its order. **Every problem is reported where the original
      is silent** - an unknown key, a name nothing has, a value of the wrong shape, and what
      the original does that a file cannot have meant, `limit` inside a scope the plainest.
      **The base game's 232 events and 74 decisions load with no unknown key**, and with four
      problems, all the base game's own: a `controls` with no `=`, `units_in_province = GER`,
      and two events sent that no file has (`script.md`, section 10).
      `OpenHOI3.SaveCheck script <install>` lists them. Nothing fires yet;
   2. **what a trigger answers - built** (2026-10-12, `docs/mechanics/triggers.md`;
      `Sim/Script`, with `CConditions` the way in): the scope and its eleven forms, the four
      blocks and the five sets, and every leaf the simulation can answer, as the original
      answers it - `month` from nought, `war_with = REB` true for everyone, `neighbour` asked
      of the other country's list, a province's number throwing the country away. The 78
      leaves nothing had read were read for it by a wave of three
      (`reversing/findings/FINDINGS-triggerleaves.md`). **A condition about something the
      simulation does not hold yet answers false and is reported as unbuilt** - diplomacy,
      the government, parties, flags, revolts, espionage, the AI's missions, a war's record
      (`triggers.md`, section 8) - by the maintainer's choice on 2026-10-12: feeding them from
      the history files "would just be adding a system that is wrong and would be removed
      later on anyways", since that state will be the country's own when its system is built.
      Laws, the government's posts, the faction, the overlord, technologies, IC, manpower,
      dissent, national unity, units, convoys, wars and alliances answer.
      `OpenHOI3.SaveCheck conditions <install>` asks every event and decision of every
      country on the start date: 16 events hold for somebody, and **5 of them only because
      an unbuilt condition inside a `not` makes the `not` true** - and they are to fire all the
      same, by the maintainer's word ("Just fire them");
   3. **what an effect does - built** (2026-10-12, `docs/mechanics/effects.md`; `Sim/Script`,
      with `CEffects` the way in): the scope blocks, `limit` in a scope doing nothing, the
      eleven blocks that pick members with the state of chance each moves, `random` and
      `random_list`, and the effects on what the simulation holds - flags, variables and the
      modifiers an event gives (new state, step 13 of a country's modifier and step 5 of a
      province's, `common/event_modifiers.txt` read for them), a country's figures, goods,
      capital, abilities, ministers, leaders, an alliance and a war, a province's figures,
      buildings, resource, name, cores, owner and controller. The 68 effects nothing had read
      were read for it by a wave of three (`reversing/findings/FINDINGS-effectrun.md`).
      **What the simulation does not hold does nothing and is reported** (`effects.md`,
      section 8): diplomacy, factions, the government and parties, revolts, espionage, the AI,
      units an event gives, and `country_event` and `province_event`, which wait for the next
      piece. `OpenHOI3.SaveCheck effects <install>` runs every effect of the base game once:
      2,882 in 615 blocks, under a second, and nothing trips;
   4. **an event firing and a decision taken - built** (2026-10-12, `docs/mechanics/events.md`;
      `Sim/Events`, the two commands in `Sim/Commands`, `Interface/Events`, and
      `CEventWindows`, `CEventWindowView`, `CDiplomacyView` and `CDecisionEntry` in
      `src/Screens`), read out of the original by a wave of three
      (`reversing/findings/FINDINGS-eventfire.md`): each country's candidate events made
      again each month and when a game is entered, the day's roll on the mean time to happen,
      the firing - `immediate` first, a person's event opened, the AI's option drawn and
      posted - `fire_only_once` by the event alone, an unanswered event taking its first
      option after four months, and decisions: only the diplomatic ones offered, asked again
      when they run, and nothing recording them, so they may be taken again. **The AI's option
      is drawn on the game's own generator** where the original seeds one with the clock -
      the maintainer's choice (`docs/compatibility.md`, entry 11). The event window,
      `EventWindow` or `Event_Province_Window`, and the decision list on the diplomacy screen
      were tried with `--click` (`--event=2602` fires an event for the player,
      `--screen=diplomacy` opens the list); **the maintainer has not looked at them**.
      `OpenHOI3.SaveCheck events <install>` plays a new game: 46 events fire in its first 120
      days, the death of George V on 14 January among them.
   5. **conditions and effects as words - built** (2026-10-12, `docs/mechanics/scripttext.md`;
      `Interface/Script`), read out of the original by five of the wave
      (`reversing/findings/FINDINGS-scripttext.md`): every condition's line in both wordings
      with its asterisk, the blocks' headers, a scope written on one line after its name, every
      effect's line, a scope's head, the blocks over a set once for each member, `random` and
      `random_list` with their chances - an event option's tooltip, the `immediate` block under
      the description, and a decision's button and icon. **The original's oddities are kept**:
      a key no table has shows as itself (about fifty in the base game), lines with no break
      run into the next, a negative figure keeps its minus inside "Lose". The conditions take
      the corrected indentation (`docs/compatibility.md`, entry 10, extended). Tried with
      `--click`; **the maintainer has not looked at them**. `OpenHOI3.SaveCheck scripttext
      <install> [tag] [event or decision]` writes all 689 of the base game's in 150 ms, none
      failing, or one whole.
   6. **the on_actions - built** (2026-10-12, `docs/mechanics/events.md`, section 8;
      `COnActionReader` in `Content/Script`, `COnActions` in `Sim/Events`), read out of the
      original by one of the wave (`reversing/findings/FINDINGS-onactions.md`): the file read,
      the first event whose trigger holds picked - a fired fire-once event standing in front of
      those after it, as in the original - and fired for each country of a decided battle's
      winners and then its losers, land, air and sea, each counting the battle
      (`land_battles_fought` and the two others answer since), and for a pride of the fleet sunk,
      remembered for `$PRIDE_SUNK$`. **In Their Finest Hour only the pride's events exist**: it
      empties the base game's battle and encirclement event files. Encirclement, surrender, coups
      and the bomb wait for what they hang on.
   7. **messages - built for events and decisions** (2026-10-12, `docs/mechanics/messages.md`;
      `Content/Messages`, `Interface/Messages`, and `CMessagePopup`, `CMessagePopups` and
      `CMessageLogView` in `src/Screens`), read out of the original by one of the wave
      (`reversing/findings/FINDINGS-messageshow.md`): `interface/messagetypes.txt` read, an
      answered event's and a taken decision's message made as the player on this machine is told
      it - `US` for his own, `OTHER` and `MAJOR...OTHER` for the rest - with his government's
      posts in its header; the popup, `DefaultPopup`, its lines laid one under the other and its
      buttons by `option`, cascading over eleven places; and the message log on the bottom bar,
      200 messages kept, a row for each piece of a wrapped line, six category filters. Tried with
      `--click` (`--event=2001:SPR` has Spain answer its major event and pops it up);
      **the maintainer has not looked at them**. Every other message the original raises waits
      for what raises it.
7. **Stage 4: diplomacy**, taken up on 2026-10-12 and cut by the maintainer in this order: what
   two countries hold of each other; the diplomacy screen showing it; Lua, which the AI answers
   offers with; the agreements; war and peace; factions. Read in one wave
   (`reversing/findings/FINDINGS-diplostate.md`, `FINDINGS-diplomacyview.md`), which moved two
   things forward on his word: **a first slice of Lua into the first piece** - a new game's
   relations come from the mod's own `AI_GenerateNonHistoricalRelation` - and **the alignment
   triangle**, which threat is weighed on.
   1. **what two countries hold of each other - built** (2026-10-12, `docs/mechanics/diplomacy.md`,
      `docs/mechanics/alignment.md`; `Sim/Diplomacy`), read out of the original and **the maintainer
      has not looked at it**: every country's record towards every country; **relation** - the
      history's one side at a time, the Lua for the rest, the month's drift written both ways, the
      `relation` effect and condition, and +5 for a land battle won beside a friend; **threat** - its
      two spreaders, to everyone and over the map, from every unit put on the map, a battle or a
      strategic bombing started, convoy ships sunk, the history and the `threat` effect, never
      decaying, with the most-threatening country, `threat` and `is_threatend`; **neutrality** - its
      day, effective neutrality, `neutrality`'s modifier, the effect and the two conditions; and
      **the alignment triangle** threat is weighed on - each country's history point, its step every
      25 days and on a new game's first, the seven terms and the clamp with its sign slip. **A new
      game is the original's start save in every pair's relation and threat, 7,878 of 7,878, in
      every country's neutrality and most-threatening country, 101 of 101, and in alignment 50 of
      101** - 67 with the save's laws given, the rest the AI's opening laws and cabinets, not built
      (`OpenHOI3.SaveCheck diplomacy`).
   2. **Lua, its first slice - built** (`docs/mechanics/lua.md`; `lib/OpenHOI3.Lua`,
      `native/lua`): the original's interpreter, **Lua 5.1.4 built from its source** by the
      maintainer's choice, rounding as the original's 32-bit build does; its state as the
      original makes it, with the install's `autoexec.lua` and a searcher over the layers; and
      the eight methods `AI_GenerateNonHistoricalRelation` calls. The base game's `autoexec`
      stops at the first engine global it reads that is not built, `SpyMission`.
   3. **the diplomacy screen - built** (2026-10-13, `docs/mechanics/diplomacyscreen.md`;
      `CDiplomacyView`, `CDiplomacyCountryEntry`, `CRelationListEntry` and `CFactionBox` in
      `src/Screens`, `CDiplomacyLists` and `CDiplomacyScreenText` in `Interface/Screens`,
      `CDiplomacyTooltips` and `CDriftTooltip` in `Interface/Tooltips`), read out of the
      original - the reading's third part, the triangle, the faction boxes and the tooltips left
      over, the same day (`FINDINGS-diplomacyview.md`, section F) - tried with `--click`, and **the
      maintainer has not looked at it**: the five tabs, a button for each faction and each
      continent, the nine sorts on one shared flag, a row with its threat and its seven tinted
      icons; **each country's marker on the triangle**, where its place puts it, made again every
      25 days; a box for each faction with its members; the chosen country's panel - relation,
      both threats, both neutralities with the ceiling for war, the highest threat, its status
      lines, the relation list with its two sorts and the breakdown of a year's change - and every
      tooltip on them, **a marker's the drift towards each faction, term by term**. Text boxes
      draw `@TAG` flags now, as the relation list needs. `OpenHOI3.SaveCheck diplomacy ...
      screen=TAG` prints what it says of a country. What is not built shows nothing or nought:
      *Loose ends*.
   4. **Lua, which the AI answers offers with - built** (2026-10-13, `docs/mechanics/lua.md`,
      sections 3 to 6; `lib/OpenHOI3.Lua/Api`), read out of the original
      (`reversing/findings/FINDINGS-luaapi.md`) and **the maintainer has not looked at it**: **the
      base game's whole `autoexec.lua` loads**, the one engine global it reads as it loads being
      `SpyMission`; every class is a global with its static functions and constants; **each push
      is a new value and `==` is one operator for every class**, as luabind does it, so comparing
      two countries is the original's error; iterators; `CFixedPoint` with **`Get()` a float and a
      number on the left working `-` and `/` the other way round**; the methods the eleven
      `DiploScore_*` functions that take nothing but tags reach, on the simulation; and
      `IAiScripts.DiploScore`, which answers **0 on a failure**, as the original refuses the offer.
      What the scripts read that is not built answers nought and is reported once. Nothing asks it
      yet - offers are the agreements' - so `OpenHOI3.SaveCheck aiscore` asks every pair: on a new
      game all eleven answer 6,006 pairs without an error but **`DiploScore_PeaceAction`, which fails
      at `CAIIntel` - espionage - every time**. `JoinFactionGoal`, `LicenceTechnology` and
      `OfferTrade` read the offer itself, and come with it.
   5. **the agreements - built** (2026-10-13, `docs/mechanics/agreements.md`;
      `Sim/Agreements`, `COfferCommand`, `CDiplomacyActions` in `Interface/Screens`,
      `CMessages.Pact`, and `CDiplomacyActionEntry` and `CDiplomacyRequestDialog` in `src/Screens`),
      read out of the original in one wave (`reversing/findings/FINDINGS-agreements.md`) and cut
      in two by this project - **the maintainer has not spoken to the cut**: the offer and the
      pacts first, trade and aid - trade, debt, lend-lease, licences, shared technology - second.
      Built: **an offer** as a command, its influence and the diplomat on his way for seven days;
      **the AI's answer** each hour, by the install's Lua - over 30 and under a draw of the game's
      generator accepts - and the answer posted back as a command; offers dropped after 96 hours;
      **alliance, NAP, guarantee, both kinds of transit rights, embargo, influence and alignment**,
      arm by arm with their relation, influence, threat and messages; how each ends by itself, the
      influence's upkeep, a war's ending of influence, and access in the month's drift; NAPs read
      from the history; and **the screen's action list** - the rows in the original's order, greyed
      by its own test or a diplomat, each with its tooltip - and **the request dialog** with its
      description and the word for the AI's likely answer. Tried with `--click`: Germany's offer of a
      NAP to Poland, Poland's Lua answering -410, the refusal at the next hour and "Poland rejected
      our offer of a non-aggression pact." **The maintainer has not looked at it.** Reading for it
      corrected the fact base twice: the triples of message keys are not all actor-first (a declined
      NAP tells the proposer `THEYREJECTNAP`) and a revoked guarantee is told to everybody.
      **Trade, the first of the second half, is built** (2026-10-13, `docs/mechanics/trade.md`;
      `Sim/Trade`, `CTradeWindowText` in `Interface/Screens`, `CTradeRequestDialog` in
      `src/Screens`), read in a second wave (`reversing/findings/FINDINGS-tradeaid.md`): a route's
      two blocks and **its price, which reproduces 582 routes of the start save**; the offer and its
      tests, the answer by `DiploScore_OfferTrade`, the route made with its convoy, its day's
      exchange after the cut-off goods are gathered, inactive for want of a convoy or of the goods,
      and its ends - cancelled, at war, under embargo, its convoy gone; **the AI's own trading**, by
      the install's `ProposeTrades` and `EvalutateExistingTrades` each hour for every country nobody
      plays; and the trade window, tried with `--click`: Germany selling Poland 50 metal a day for
      3.75 money. `OpenHOI3.SaveCheck trade` plays it: ten days of a new game make 283 offers, 262
      accepted. **The maintainer set the window beside the original's on 2026-10-13 and asked for the
      trade interface finished**, read in a third wave (`reversing/findings/FINDINGS-tradeui.md`) and
      built the same day: the window's place, the screen's middle and `diplo_request_pos`, and its
      sliders' "Sell" and "Buy"; **the bar of offers made to the player** with each icon's tooltip,
      blinking and week, and the dialog an offer opens (`CDiploOffers`, `COfferAskDialog`,
      `COfferAskText`); **the production screen's list of trades** with its tooltips, inactive marker
      and cancel dialog (`CTradeListEntry`, `CTradeCancelDialog`, `CTradeListText`); **Automate Trade**,
      the hour's roll of the C runtime's `rand()` seeded to 1 (`CAutomatedTrades`, `CRuntimeRand`); and
      the trade messages' goods. Tried with `--click`: three days bring Germany nine offers, Finland's
      opened and accepted, its line in the list, its tooltip and its cancel.
      **Aid is built too** (2026-10-13, `docs/mechanics/aid.md`; `Sim/Aid`, the four offer kinds in
      `Sim/Agreements`, `CLicenceText`, `CShareTechnologyText`, `CLendLeaseText` in `Interface/Screens`,
      and `CShareTechnologyDialog`, `CLicenceDialog`, `CLendLeaseWindow` in `src/Screens`), read in
      `reversing/findings/FINDINGS-tradeaid.md` C and `FINDINGS-tradeui.md` D and E: **debt** - asked,
      allowed, revoked at the debtor's cost, cleared by war, repaid a 18,250th a peaceful day for ever;
      **shared technology** - a theory read at the higher of the two levels, a new sharer ending the
      old, the day's check, the leadership it costs, the screen's green figure and its tooltip;
      **lend-lease** - who may and by which way, asked for and offered, the hourly look, the receiver's
      convoy, the giver's day to the thousandth and the receiver's whole IC, free to its five shares;
      **licences** - the fee, the buyer's runs at the seller's levels, the AI's answer in the install's
      Lua; and their rows, dialogs and window, and their offers put to the player. Tried with
      `--click`: Britain sharing with France, the licence dialog's list, the empty lend-lease window.
      **The maintainer has not looked at it.** Reading for it corrected the fact base and this
      project: reinforcement and upgrades charge their resources whatever the stockpile holds.
   6. **war and peace - the first piece built** (2026-10-13, `docs/mechanics/war.md`; `Sim/Wars`,
      `CWarEffects` and the war conditions in `Sim/Script`, `CWarActions` and `CWarText` in
      `Interface/Screens`, `CWarGoalDialog` in `src/Screens`), read in one wave
      (`reversing/findings/FINDINGS-war.md`) and cut in two by this project, **the cut the
      maintainer's on 2026-10-13 ("Ok split it")**: declaring a war and who joins it first, peace
      and the capitulation second. Built: **the casus belli types** of `cb_types.txt` with their blocks and `peace_order`,
      and the casus belli held - every one made in play of the null type, as the original's;
      **war goals** - available, valid for a war, added once a month, the history's own (Italy's
      `puppet` on Ethiopia); **a declaration** step by step - a new war or one widened, its name
      ("War of German Aggression", ordered), the puppets, guarantors and factions in at once, the
      overlord and the allies asked, the relation, the threat of a new war, the message - and
      **limited war**; **the call to arms** both ways, the AI's answer by `DiploScore_CallAlly`, and
      the player's dialog with its warning lines; **the truce** and **mobilisation** - a new game
      mobilises Italy and Ethiopia to 31 January as the start save has them; the `war`,
      `add_wargoal`, `casus_belli` and `add_casus_belli` effects and the `truce_with`, `casus_belli`,
      `has_wargoal` and `has_cb` conditions; the war ceiling's claims term; and on the screen **the
      war rows** with their tooltips and **the declare-war dialog** with its goal, country and
      region lists. Tried with `--click`: Germany, its neutrality set to nought, declaring on Austria
      for a puppet; Japan answering Manchukuo's call against the Soviet Union, taking the lead.
      `OpenHOI3.SaveCheck declare` tries a declaration on a new game. **Nobody may declare war on 1
      January 1936** - every country's neutrality is above its ceiling - so `--neutrality=GER:0`
      and `--declare=TUR:IRQ` are there for trials. **The maintainer has not looked at it.** Reading
      for it corrected the fact base four times: a truce counts from the last change of a war's
      membership, not its end; the casus belli type's month count is `months`, not `truce_months`; the surrender sums are over
      cores, nested; and the war's flag the record had named twice is `limited`, as both earlier readings had it.
      **The second piece, peace and the capitulation, is built** (2026-10-13, `war.md` sections 11 to
      14; `CPeace`, `COccupiedLand`, `CSurrender` in `Sim/Wars`, `CProvinceHands` in `Sim/Provinces`,
      `CPeaceText` in `Interface/Screens`, `CAiIntelClass` in `Lua/Api`), after a second wave read what the
      capitulation calls (`reversing/findings/FINDINGS-surrender.md`): **peace** - the row and its gates, the
      dialog with what would change hands, the AI's answer by `DiploScore_PeaceAction` now that **`CAIIntel` is
      built** (all 6,006 pairs answer), the provinces moved, the occupied land given back and the war ended;
      **leaving a war** for peace and `end_war`; **the day's return of occupied land**; **a change of hands with
      consequences** - the 50% building damage, production stopped, a fallen capital's quarter looted; and **the
      surrender progress**, the victory points of owned cores counted each day, with `surrender_progress` and
      Lua's `GetSurrenderLevel`. Tried with `--click`: Germany offering Austria peace - "Very Likely" - and
      Austria accepting at the next hour. **The maintainer has not looked at it.** Reading for it corrected the
      fact base: the AI's sighting draw is never negative.
      **Then, on the maintainer's word ("Continue with your plan"), the rest in his order**: **the acting
      capital** - where a government sits and so whose pool is its stockpile, moving at once when its capital
      falls and back when it is held again, with the declared capital moving only on a loss of ownership - and
      **every one of about a hundred reads of a country's capital set to the one the original reads there**, read
      site by site in a third wave (`reversing/findings/FINDINGS-capitalreaders.md`; `CActingCapital`);
      **annexation** (`CAnnexation`, `inherit`) and **the puppet** (`CPuppets`, `create_vassal`), with a faction's
      members kept in the order they joined (`CFactionMembership`); **the government in exile** (`CExile`,
      `form_government_in_exile`, `government_in_exile`) - its own stockpile, its cuts, its seat in its leader's
      capital, the day's hand-over, the puppet swap and what an exile may not do; and **the capitulation** -
      the daily surrender to the best-scoring enemy with `on_surrender`, every enemy's goals enforced in
      `peace_order` by the original runtime's sort, the made-up `conquer`, vassals freed or puppeted, and each peace
      option (`CCapitulation`, `CWarGoalEnforcement`). `OpenHOI3.SaveCheck declare ... occupy` tries it on the
      install: Ethiopia, its units gone and its land held, surrenders to Italy at the day's end and is made Italy's
      puppet by the history's goal; Austria to Germany is annexed by `conquer`; France to Germany goes into exile
      in London. **The maintainer has not looked at any of it.** **A battle's capture then went through the same
      setter** (`landcombat.md`, section 7): a puppet's conquest goes to its master, ground taken alone among a
      fighting friend's to the friend, an exile's to its faction's head only where the head is an enemy of the
      owner - which this had left out - and a capital taken is looted and its government moves at once; and a
      defending front unit falling back may damage the province's buildings. Read off the bytes for it, the
      record's `CanOperateIn` was corrected: the byte it tests is the co-belligerent one, not military access.
   7. **factions - built** (2026-10-13, `docs/mechanics/factions.md`; `CFactions`, `CFactionOffers`,
      `CFactionProgress`, `CVictoryConditions` in `Sim/Wars`, `CFactionAimsReader` in `Content/Script`, `CFactionText` in
      `Interface/Screens`), read in one wave (`reversing/findings/FINDINGS-factionplay.md`), and with it **diplomacy
      is built as far as the maintainer cut it**: **joining** - `AddCountry` with its war sweep, every join through
      it; **Join Faction and Invite to Faction**, one action, its rows, greying, tooltip and dialog, the AI's answer by
      `DiploScore_InviteToFaction` with the tags turned round for a join, nothing on a proposal, the joiner's
      puppets following and its alliances with the members ended, the messages; `join_faction` and `leave_faction`;
      **the rules** - the Allies' guarantee given by every member and an answered call bringing the faction in;
      **the faction's modifier on its members**, which made Moscow's garrison the start save's (28 of 28 capitals);
      the faction leader at war taking an aligned outsider's resources; **a faction's progress**, the points its
      members hold, hourly - the start save's 115, 263 and 173 of 1172 - with `faction_progress`, the box's bar and
      tooltip; and **the victory conditions** of `faction_aims.txt`, each faction's fifteen primaries, tested daily.
      **Nobody may leave a faction by choice**: the original's leave button is its custom game's. Tried with
      `--click` - Germany's Invite to Faction row, the Axis's "0/15" - and with `OpenHOI3.SaveCheck faction`:
      Hungary asks Germany and is taken in, Sweden refuses Britain. **The maintainer has not looked at it.** Reading
      for it corrected the fact base three times: the faction rules are written - so the Allies' faction-wide
      guarantee and call are live - `CFactionAction::Apply`'s "gate" is the direction, and a guarantor by the
      faction is called like any other. **The game's end is not built**: the dialog that ends it was not read.

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
generator of chance (`docs/mechanics/landcombat.md`). And since: supply - the network, the
day's carrying, convoys and the map mode (`supply.md`, `convoys.md`, `supplymap.md`) - a
battle between fleets (`docs/mechanics/navalcombat.md`), and a fleet's missions with the
raid on convoys (`docs/mechanics/navalmissions.md`).

There are five savegames of one game to check against now, from 1 January to 20 April
1936. The first is the start; the later four are a month or less apart, which is what
checks anything that moves from day to day.

**When a spec needs something read out of the executable**, the reading is done in the
fact base and entered in its record, not only written up: this repository's `CLAUDE.md`,
*Where knowledge about the original comes from*.

### Loose ends

None of these blocks the list above.

- **Lua's API is built as far as the offers that take only tags, and trade, reach** (2026-10-13,
  `lua.md`): about a hundred methods of twenty-six classes, where the original registers some six
  hundred. Nought or false, reported once: **the AI's strategy** (friendliness, antagonism, threat -
  nothing fills it without `add_ai_strategy`), governments in exile, espionage - `CAIIntel` is built
  with nobody holding spies anywhere - and the day's tribute and debt. No `CFixedPoint` is
  written by `tostring`, nor made by a script. The lists of neighbours are worked out at each call,
  a third of a millisecond. **The original's main thread ran its Lua at
  24-bit x87 precision** (Direct3D 9's doing) and this runs it in doubles: whole numbers agree, what
  the AI's fractions do is not established. **`Laws(country)`**, which the original calls for every
  country entering a new game, is not called - for the player by `docs/compatibility.md`, entry 6,
  for the rest until there is an AI. **The interpreter is built for 64-bit Windows only**, with the
  Visual Studio C++ tools, as `openhoi3_lua.dll` - a name chosen because Lua for Windows puts a
  32-bit `lua51.dll` on the PATH, which Godot loaded in its place.
- **War and its end leave out** (`war.md`, *Not built*): `surrender_inherit`, `release` and `release_vassal`;
  expeditionary forces; the cabinet a new puppet's government would fill; an exile's free supply from friends,
  its brigades at its leader's technology, liberation, the underground and militia; **the puppet's tribute**;
  and a landless country's units, which the original destroys each hour. **A change of hands leaves out** unit
  rebasing, radar, the occupation policy, the underground and nationalism. **A
  pact is one object for both countries**, so annexation ends both sides of one (`war.md`, *Ours*). War
  exhaustion is not built, so nothing pays back neutrality. **Units on ground they may no longer stand on are not marked** (`war.md`, *Not
  built*), and undeclared wars and the revolt war are not built. Mobilising and spies spread no threat
  until they are built.
- **Factions leave out** (`factions.md`, *Not built*): the custom game's leave and join; the AI inviting, and
  the Axis AI's limited wars; the victory conditions' random fill for a faction with fewer primaries than the
  count, reported; **the end of a game** - the `VCConfirm` dialog was not read, so a game the conditions finish
  is marked so and goes on, and the end date does nothing; the victory conditions' window and alert; and
  `release_vassal` with the liberation and puppet commands.
- **The agreements' first half leaves out** (`agreements.md`, section 9): **an AI's offer other than
  trade** - the AI's foreign minister is not built, so only trades reach the player's bar of offers;
  the player handing his diplomacy to the AI; **the NAP's border balance**, so a NAP may not be
  broken inside its three windows and the tooltip says the player has 0% of the brigades; the NAP
  alert; and aid. The dialog's influence and alignment drift
  says each day's `align_towards` and the days; a message's `VAL` and `TIME`, which no text of the
  base game uses, are not given. **Every `DiploScore_NonAgression` answer on a new game is below
  nought** (`OpenHOI3.SaveCheck aiscore`), so the AI declines every NAP; whether the original's
  answers are too was not checked.
- **Aid leaves out** (`aid.md`, *Ours*): the licence list's combat-value comparison (technology levels
  stand in), a licence row's stat block, a convoy's lend-lease efficiency lowered by sinkings, and the
  production screen losing its tooltips while the lend-lease window is open. **Every licence model shows
  "+*"** in a new game: the exact-match rule behind the mark is inferred from the record's word. No AI
  proposes debt, lend-lease, licences or sharing - the foreign minister's Lua is not built - and nothing
  of lend-lease was set against a save, none of which holds any.
- **Trade leaves out** (`trade.md`, section 16): what becomes of a route
  when either country ends, not read; handing foreign policy to the AI; and the sound a new offer
  plays. **Automate Trade is a command here**, where the original's checkbox writes its local player's
  own flag (`trade.md`, *Ours*). **The offer dialogs' background**, `SandboxFloaterBackground`, is
  drawn as its texture is, see-through grey; nobody has set it beside the original's. **It is slow**: with nobody playing, the install's trading scripts take
  about 80 ms an hour, some two seconds a game day, nearly all of it the Lua itself after the
  province scans it asked were cached (`CCountryFacts`); the original spreads it over worker
  threads.
- **The diplomacy screen shows nought or nothing for what is not built** (`diplomacyscreen.md`,
  *Ours*): every country's surrender bar is full, a faction's victory bar empty and its tooltip
  "0 out of 0" - no province's victory points are read yet - and its box has no victory
  conditions; the goal lists and the drift list are empty, the action list holds the agreements
  but not war's rows, and
  `trade_automate_checkbox` does nothing. Which rows' checkboxes the original clears when another
  is chosen, the order of equals under the nine sorts, and the order of a faction's members (the
  history's, here the countries') were not read.
- **The alignment triangle drifts without faction progress or influence** (`alignment.md`): a
  faction's share of the victory points is taken as nought, so revanchism weighs every held core in
  full, and no `align_towards_*` exists. **Its first step agrees with the start save for 50 countries
  of 101** because the original's AI moves many countries' press laws and fills empty cabinet posts
  as a game is entered, which changes their drift speed and susceptibility - neither is built here.
  The most-threatening country is worked out for every country each day, where the original does it
  for countries a flag it sets marks.

- **Events fire and decisions are taken** (2026-10-12, `script.md`, `triggers.md`,
  `effects.md`, `events.md`), and much around them waits:
  - **four on_actions have nothing to fire from** (`events.md`, section 8): `on_encirclement`
    waits for a province's days without supply to be counted, `on_surrender` for surrender,
    the coups for espionage, `on_nuke_dropped` for the bomb; and a battle's `last_*_losses`
    are not kept, so the six conditions on them answer false;
  - **the words of a condition or an effect about something not held are the original's, with
    what it would name left out** (`scripttext.md`, *Ours*): a ruling party's name is empty, and
    a guarantee or a pact is never
    had - so `guarantee` always writes its line and `end_guarantee` never; province none has no
    name, so an effect in a region names none;
  - **of messages, only an event's and a decision's are raised** (`messages.md`, *Ours*): none
    on the map, no settings window and no `messagetypes_custom.txt` a player keeps his choices
    in, and the bottom bar shows its log and nothing else of itself - the minimap, the map modes'
    buttons and the chat are hidden; whether the original's button that opens the log shows
    only while it is shut was not read;
  - **the AI takes no decision**: the original's take them in its Lua scripts, and there is no
    AI (`events.md`, section 9);
  - **no event is asked in a new game's two first days** - ours; whether the original's are was
    not read (`events.md`, section 2);
  - **the decision list's button's `SELECT` is wider than its 32-pixel picture**, as the files
    have it - not set beside the original;
  - **a condition the simulation cannot answer is false**, and the unbuilt list is long
    (`triggers.md`, section 8). Inside a `not` that makes the `not` true, so `not = {
    government = x }` holds everywhere, and **such an event fires** - the maintainer's word on
    2026-10-12, "Just fire them" - so some of the base game's events will fire for every
    country until governments and parties are built;
  - **an effect on what the simulation does not hold does nothing** (`effects.md`, section 8):
    `relation` (271 of the base game's effects), `neutrality`, `war`, the government's and the
    parties', a unit type's name and the rest - each a system of its own still to build;
  - **a change of controller by script does what a battle's does here**, the province changing
    hands and nothing else - the damage to buildings, the plunder of a capital and the rebasing
    of aircraft and ships wait for both (`effects.md`, section 5);
  - **`create_alliance`** runs a part of the alliance action nobody has read, and does nothing;
  - **`num_of_ports`, `total_num_of_ports`, `has_hostile_spy_mission` and `sea_zone`** are
    unread in the original, not unbuilt here, and answer false until they are read;
  - **the lists the original keeps a day** - neighbours, allies, the nations a country could
    release - are worked out when asked (`triggers.md`, section 6);
  - **the other places the original writes triggers and effects** are not read with it -
    a rebel type's `spawn_chance`, `on_actions.txt`, `triggered_modifiers.txt`, the casus
    belli, the AI's files - and a technology's `allow` and a tactic's or a trait's `trigger`
    still go through the small readers they were built with (`CResearchCondition`,
    `CCombatCondition`), which should become this one's - the battle's conditions answer
    false here for want of a battle in the scope, so that is where they go next;
  - **BlackICE's events have not been read**: a mod is not a layer here yet, and its 178
    event and 98 decision files are the harder test;
  - **missions, spy missions, ministers' and leaders' ids** are taken as written, nothing
    here reading the lists they name (`script.md`, *Not checked here*);
  - **a tag spelled like another of the original's own words** is not caught: only `PHI` and
    `PAN`, the two the base game's and BlackICE's tags collide with, are known.
- **The production and technology screens' tooltips leave two things out** (2026-10-11): the
  standing army's term of the consumer goods
  demand, which the simulation does not have either, so the slider says `0.00` of it
  (`production.md`, section 3); and the lines of the trade and convoy lists, which are empty.
  Nothing disables a building's button or the underground's, so their tooltips' second
  lines never show (`productionscreen.md`, section 8).
- **A technology's requirement text knows the base game's conditions only** (2026-10-12,
  `technologyscreen.md` section 9): a technology's level, `num_of_ports`, `any_owned_province`
  with `has_building`, and the three groups. BlackICE's `allow` blocks use `war`, `faction`,
  `check_variable`, `has_country_flag` and more, which the loader leaves out and the tooltip
  cannot say until they are read - `has_country_flag`'s words are read already. A building
  under `any_owned_province` is "in" the province numbered nothing, whose name is empty here.
  The panel's `effects` list is still ours; the original cuts the same text into its rows.
- **The battle window's tooltips leave three things out** (2026-10-12, `combatview.md`
  section 12): a landing's lines, which wait for landings; the bombed side's rows, which say a
  figure each in the original whose joins were not read; and what of the province a bombing's
  row in a unit's panel adds. A ship's positioning is scaled by `targeting_chance`, taken to be
  the figure the original names `naval_positioning_bonus`.
- **The unit panel's tooltips leave out what is not built under them** (2026-10-12,
  `unitpanel.md` section 10): the attrition breakdown, the supply and fuel lines, a fleet's
  or an air unit's base and what it supplies, the terms of the speed's multiplier one by one,
  the organisation a brigade regains an hour, what technology adds to the digging-in cap, the
  `+*` on a model the country no longer builds, a faction leader's name for a model, and the
  upgrade button's list. A leader's traits on the way are in the traits file's order, where
  the original's are in the order they lie in its memory.
- **The step of ten in the builder windows is Ctrl on the word of a tooltip** (2026-10-11):
  the original's own buttons say "Ctrl + LMB to add or remove 10", and the key its code asks
  was not traced (`builders.md`, section 6). It was shift here until then.
- **Which end of a scrollbar that runs up and down adds is not settled** (2026-10-12): the
  original's scrollbar is given a flag that swaps its two buttons, and what sets it was not
  read (`interface.md`, section 2). The sliders, across, take away on the left as here.
- **A land battle has its leaders since 2026-10-11, less their aggressiveness**, which comes
  from the AI's plans and is 0 here, so a reckless tactic weighs half (`landcombat.md`,
  section 11). Of the things that bear on a unit in a round, six wait for what they read -
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
  original says what each modifier is worth to a unit and what a tactic does - and no sound;
  and only a land battle has one. An enemy's counter says what its
  unit is, where the original writes question marks on one it knows too little of; units
  in a battle stand where they stood, where the original has a place in each province for
  the attackers and one for the defenders; and what a click on the original's marker does
  was not read - here it opens the window. `docs/mechanics/combatview.md` ends with the
  list.
- **An air unit at its base cannot be clicked** where land units stand in the same
  province, **outside the air map mode**: since 2026-10-10 an air stack stands on the
  province's own point as the land stack does (`mapunits.md`, section 1), and a click takes
  the stack drawn on top, which is the land one. In the air map mode the large air base is
  asked first, as in the original (`airmap.md`, section 5). How the original keeps such a
  unit's counter off the map in the other modes was read (`reversing/findings/FINDINGS-seaair.md`,
  section 6) and is not built. `--select=<name>` picks one for a trial.
- **The air map mode greys the wrong terrain.** The original lays its colours over its
  terrain map mode turned grey - the textured one, tiles out of `map/terrain/texturesheet`
  picked by `terrain.bmp` and tinted by `colormap.dds` - and here they lie over the flat
  colour of each province's terrain, which is nearer the original's simplified terrain, since
  the textured mode is not built. The maintainer, 2026-10-11, offered building it now: leave
  it as is, for later in the plan.
- **The air map mode is drawn by rules of ours where the original's were not read**
  (`airmap.md`): its colours' alpha is laid at a half over that grey and the sea at the
  water's 0.8, where what the original's shaders do was not followed through; its icons stand about
  the province's own point, not its label's; the small building pictures are still drawn
  under the large ones; the large pictures and their frames are worked out once a day; a
  ring's range is the shortest of the unit's wings'; and the rings and icons show from any
  height.
- **The air orders window is less than the original's in four ways**: no tooltips, no
  paradrop's earliest start, a province out of the unit's reach drawn as the terrain mode
  draws it - how the original draws "nothing" was not read - and the line under the sliders
  is not wrapped into its box. `fleetsandair.md`, section 4.
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
- **Of an air unit's orders eleven are built**: air superiority, the intercept, the eight
  bombing missions and the change of base. The window lists the rest as the original does -
  the reserve, the supply flight, the raid on convoys - and they cannot be ticked; **its
  target shapes, sliders, day and night boxes and dates are drawn and do nothing**, so a
  mission given through it covers the one province clicked, flies day and night and never
  ends - the original's window makes an area of several provinces and gives two dates,
  and how was read only for the circle (`aircombat.md`, section 2).
  `docs/mechanics/fleetsandair.md`, sections 4 and 5.
- **A battle at sea, a battle in the air and a bombing have no marker on the map**, because
  the original gives them none: its marker is a land battle's alone, and from the battle's
  fourth hour (`combatview.md`, section 8). So they are reached by the row in a unit's
  panel and by nothing on the map. **The sea battle had a marker here when the maintainer
  looked at it**, and its going was not put to him: one line of `CCombatViewText.HasMarker`
  brings it back if he remembers the original otherwise. What the original shows of them
  on the map is in its **air and naval map modes** - icons of the battles that are over -
  and neither mode is built, nor the list of finished battles they are counted from.
- **The retreat button of a battle on land is not the original's.** The original gives every
  unit of the player's the one order, to go where a beaten unit goes; here that is so for an
  air unit, a fleet is set breaking away, and a land unit is only halted
  (`combatview.md`, sections 2 and 11).
- **A battle in the air was read and never watched.** Its window and its row in a unit's
  panel were tried with real clicks and the maintainer has not looked at them
  (`combatview.md`, section 11). Leaders and difficulty are not
  in what the hour makes of a wing; a battle earns no practical and no experience.
  `aircombat.md`, section 10. `OpenHOI3.SaveCheck airbattle` is a trial of it.
- **Bombing was read and never watched**; its window is the air battle's and as little
  looked at. **A carrier's aircraft are not built** - their order, their bombing beside a sea battle,
  and a carrier as a base at all - nor the raid on convoys from the air, nor what a
  strategic bombing does to threat and to the bombed country's unity. `bombing.md`,
  section 13. `OpenHOI3.SaveCheck bombing` is a trial of it.
- **A country sees into no ground but its own, its friends' and where its units stand**:
  the part of the original's intelligence by which it sees ground beside its units was not
  read (`aircombat.md`, section 6). Interdiction flies only at ground its country sees
  into, so it has nowhere to fly; ground attack bombs on its way only where it sees; and
  air superiority finds no target over an enemy's ground. It wants a reading.
- **A building has two levels now, and most rules were written when it had one.** A
  province's modifier is made from what works of a building, and whether there is a port or
  an airfield is asked of what was built. The supply network's roads are what was built, as
  the original keeps that figure, and a base's room is what works of it, as was read. **The
  rest were not read for which they mean** - the order of an area's harbours, where a depot
  may be, a convoy's choice of harbour - and are given what was built, so that bombing does
  not rearrange a network. `bombing.md`, section 7.
- **A land battle does no damage to a province's buildings.** In the original a defender
  falling back does, by a roll for each building (`landcombat.md`); it waited for a
  building to have a level that can be damaged, which it has now.
- **No AI gives an air mission**, so in a game only the player's air units fly one; the
  command line is the only way an enemy's do.
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
- **The unit panel's attrition figure is always nought**: nothing wears a unit down for
  where it stands yet. Its supply and fuel bars are real since the supply pass was built.
  `unitpanel.md`, section 4.
- **One unit at a time.** Nothing selects several, so the original's list of selected
  units and the window over it are not built.
- **The hierarchy of a selected unit is on the map, less what an AI would add.** A
  selected land unit is joined to its commander and everything under it to theirs, in the
  original's picture and colours, a unit out of radio range by a broken dark red line
  (since 2026-10-11); H switches the lines off and on; and the counter of every unit under
  it wears the original's second ring colour. The setting is kept nowhere, the lines are
  drawn at any zoom, and the green ring of a selected unit an AI commands has no AI to
  show for. `docs/mechanics/mapunits.md`, section 8.
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
  on; with it off the original writes the leader's. There are leaders now and no settings;
  the maintainer decided on 2026-10-09 that it stays so until the settings are built.
  `docs/compatibility.md`, entry 7.
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
- **A new game is short of 476 technology levels the original starts with**, and why is
  half found: 453 of them bring a country exactly up to what was due before 1936 in that
  technology - Germany gets 22, the three that allow mountain troops, engineers and
  carrier air groups among them - but which technologies are chosen is not found, it is
  not research, and none of four guesses from the data fits. It wants a reading, and then
  the maintainer's word on whether the player's country gets them, since they are given
  to it as the laws were. `OpenHOI3.SaveCheck techgap`; `docs/mechanics/resources.md`,
  section 5.
- **Things the savegame shows that are not explained**: home production for three countries, governments for
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
- **A battle at sea was read and never watched.** What decides its course - the order
  ships are drawn up in, who is shot at - rests on the reading alone (`navalcombat.md`,
  the end). `OpenHOI3.SaveCheck seabattle` is a trial of it.
- **The sea battle's window was measured against no picture of the original's.** The
  maintainer looked at it on 2026-10-10 - "The sea battle window looked perfect"
  (`combatview.md`, section 10) - so where a ship's picture stands in its row and which of
  its two frames it shows have his eye and no measurement; the land battle's window had
  four things wrong that only his screenshots showed. Its tooltips, its leader and its
  sounds are not built.
- **A fleet's missions were read and never watched, and six things in them look wrong.**
  They are built as read and listed in `navalmissions.md`, section 12, for somebody who can
  watch the original: an interceptor goes out once and then lies in port under its order;
  a patrol sinks convoys; a sortie ordered from anywhere but its base is given a path that
  does not begin where it is, and one that turns aside for an enemy does not go on; a
  reserve never sees to a fleet of priority 4; a raider's ships shoot at their own last
  ship; an escort hesitates an hour at the first end of its beat.
- **Nobody gives the other countries' fleets a mission.** There is no AI: `--mission` on
  the command line is the only way an enemy fleet patrols, raids or intercepts. And an
  idle fleet of a country nobody plays starts no battle - the original's rule.
- **A fleet carries nothing.** Transport and invasion are listed in the orders window and
  cannot be ticked; no land unit goes by sea.
- **Intelligence is read whole and built without radar.** The original keeps a level for
  every country in every province: 9 for whoever holds it and their allies and faction, 9
  for a country with a unit on its ground, 2 for one with a unit in its water - 9 where
  that is a patrol shadowing somebody - and up to 8 from radar (`aircombat.md`, section 6).
  All but the radar is built, and only fleets' sightings and air missions ask it: **the map
  still shows every country's units**, there being no fog of war drawn. **And whether the levels are
  worked out daily or hourly in an ordinary game was not established** - the original
  does it daily always, and hourly once a change of who plays a country has asked for it,
  which nothing found ever stops; here it is daily, and the maintainer's word on
  2026-10-10 was "I think its daily" (`navalmissions.md`, section 9).
- **A raid from the air is not built**, nor the threat a raider's country gains for a
  ship sunk, nor any message about a raid.
- **Who is played by a person is state the client sets**, `CGameState.Played`, and not a
  command: it is asked in one place, the idle fleet's, and wants a proper home when a
  game can be joined or left.
- **A fleet whose last ship is sunk is removed from the game at once**; what the original
  does with an empty fleet was not followed.
- **A ship's own technology and its admiral are not in a sea battle**: the positioning a
  technology adds, the targeting it adds, and every leader's skill and trait - which
  between them decide how well a fleet is drawn up and whether it shoots at what matters.
- **Convoys lose ships and none is built.** A raid sinks transports and escorts
  (`navalmissions.md`, section 8), and no transport or escort is built, so a country lives
  on those a new game gives it and only ever has fewer; no trade or lend-lease convoy; and
  the production screen's list of them is empty, with its three switches always on.
  `docs/mechanics/convoys.md`, *Not built*.
- **The goods of a country that does not exist are shipped to whoever holds its capital**,
  where that capital is cut off from the holder's own: everything lying in cut-off land goes
  to its harbour each day, and a province's pool is also such a country's stockpile. It is
  what the original does - Jerusalem, Cairo and Amman have no resources left two days into
  the maintainer's save, and Britain has 450 crude oil it did not make - and **his word,
  2026-10-10, is to follow it**. Such a country is still charged for consumer goods and
  supplies, and its pool is made nothing again at each day's end. Not a loose end; noted here
  because it looks like one. `convoys.md`, section 8.
- **Seven countries that do not exist make about a tenth fewer supplies than the save says.**
  Sixteen of 23 agree. The maintainer's pointer for when it is taken up: a minister effect
  on supplies, and triggered modifiers that give supplies, which are not applied here yet.
  `docs/mechanics/production.md`, section 7.
- **What a province yields is now read, and is still computed as it was measured.** The
  original takes the technology first and then one factor that is the sum of the province's
  and the country's modifiers; here they are multiplied in turn. They agree with the save for
  78 countries of 78, and could part under a law and a local modifier at once.
  `reversing/findings/FINDINGS-supplyconvoys.md`, section 8.
- **The supply map mode shows the player's own land only.** There is no intelligence, so
  every other country's land is the grey the original gives land one knows nothing of; the
  mode is turned on by `Tab` and not by the minimap's button; and its tooltip says of
  anybody else's province only its name and owner. `docs/mechanics/supplymap.md`.
- **A new game's two days are run, and what the original's AI does around them is not.**
  There it changes laws for every country, some before the days and some after, and makes
  its theatres; a change of law holds every brigade to the running game's ceilings,
  which is why the original's first screen shows reserves already down. Here nothing
  changes a law, so a reserve stands at its own figure of organisation on the first screen
  and the first hour played brings it down - **unless the unit is sent to attack before
  that hour is run**, since a unit that is fighting gets no hourly return and so is not
  held either: a German reserve division then fights its first battle at 35 where its
  ceiling is 14. Holding every brigade once the days are run would close it; that is not
  what the original does, and the maintainer's word on 2026-10-10 was to leave it: "The
  two days for the brigades is ok for now. Maybe it comes up again later." For the player's country
  the unchanged laws are the decision (`docs/compatibility.md`, entry 6); for the rest it
  waits for the AI. `tick.md`, section 6.
- **Making a new game takes about six seconds**, four of them the first convoys' routes
  and two the filling. Neither has been looked at for speed; a day played takes a tenth of
  a second.
- **A province's days out of supply are not counted**, and nothing is raised on its first.
  The original's arcade mode, where units draw on the stockpile and the network does not
  run, is not built, nor is a unit on loan to another country.
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
- **Leaders in battle are built whole since 2026-10-11** (`docs/mechanics/leaders.md`,
  sections 9 to 11) but for two things: the aggressiveness a tactic is picked with, which is
  the AI's, and `winter_attrition`, which waits for attrition. Nothing of a leader's
  experience or progress towards a trait is set against a save: no save has a battle in it.
- **An order of battle that names a leader no file declares** has him ignored; what the
  original does was not read.
- **Only the player turns a country's `auto_assign_leaders` off**, with the leader window's
  checkbox: every country starts with it on, as in the original, and with no AI nobody else
  ever changes it.
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
- **The savegame check's main run still reasons from day nought.** It loads a game with
  its two days not run and takes two days of each figure off the save, system by system,
  which is how each rule was first pinned down; its model of the units' two days has an
  hour before each day, where the original - and now the game - has one hour after both.
  The two give the same figures on the start save. `OpenHOI3.SaveCheck supplypass ...
  countries` is the check that runs the game's own first days (`tick.md`, section 6).
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
  dialog. Nor is every screen's own tooltip shown yet (*Next up*, item 3), or an icon's `rotation` applied, and a checkbox is a
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
- **Six of the top bar's screen buttons open nothing** - politics, intelligence,
  theatres, statistics, strategic warfare and the menu - and the country
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
sea and by air - in five projects:

| Area | What it does |
| --- | --- |
| `lib/OpenHOI3.Sim` | The game state - dates, countries, provinces, terrain - and what drives it: fixed-point numbers, the command queue, and the hourly tick with its daily, monthly and yearly passes, and the first systems in them: each province's modifier and each country's, from its laws, ministers, provinces and the strategic resources it has the use of; its IC, what it produces at home, the stockpile that goes into, the six shares its IC is divided between, with the money, supplies, dissent and resource bill that come of them, the conversions of energy into crude oil and crude oil into fuel, its manpower, its leadership with the officers, influence and spies its shares make, and its research with the monthly decay of its theories and practicals. And the units: the kinds of brigade, ship and wing, each one's own technology levels and figures, the chain of command they sit in, what each holds as a game starts, and what a day does to one: the supplies and fuel it draws, the men who go home, and the reinforcement and upgrades its country's IC pays for. And what an hour does: the officer ratio is worked out, and each brigade's organisation is held under it and comes back towards it. And the build queue: ordering a unit, what it costs and takes, the day's work the production share pays for, the unit that comes out, where it may be put and which unit in the field it may join. And movement: the route, the hourly march, where each kind of unit may go, the orders that send a fleet or an air unit and change its base, its range, and the distances by sea. And land battles: what starts one, its front and its reserves, the hourly round, the damage and what it costs a country, falling back, shattering, the province that changes hands and the delay an attack leaves, on the game's one generator of chance, with the tactic each side picks by weight and what the two tactics make of the battle. And a fleet's missions, battles at sea, an air unit's two fighting missions, what a country knows of a province, and battles in the air. And leaders: who is in service, which unit each may command, the choice of one for a unit, and how far a command reaches, with what it does to organisation and to what a unit uses. And events and decisions as they are read - an event with its options, a decision, a mean time to happen, a trigger and an effect - none of which does anything yet. References nothing, so the simulation runs headless |
| `lib/OpenHOI3.Content` | Reads the Clausewitz script format, finds and layers a Hearts of Iron 3 install, and reads its localisation tables, its map with its continents, each province's points and terrain and the edges between provinces with their kinds and distances, where its buildings stand, which provinces have a port and onto which sea, the two kinds of unit counter, its defines, its buildings, laws, ministers, technologies and strategic resources, its province, country and war history, its unit types, historical models and orders of battle, its leaders and their traits, its combat tactics, its interface files, and its events and decisions with every trigger and effect in them, by the original's grammar, each problem in them reported. No engine in it either |
| `lib/OpenHOI3.Interface` | What the screens show, still with no engine: the text of every figure of the top bar, the production screen with the lines of its build queue, the technology screen, and the four windows a unit is ordered from - their columns, a design's totals, the order panel - in the original's format and colour, the colour escapes in text, how a set of sliders shares out what one of them gives up, and where an element goes on a canvas that scales. And of the map: which units have a counter and how they stack, what a counter says and where its parts go, and the painting of one out of its plate, its mask, its unit's picture and its size mark; the buildings that show on the map and which unit a click on a port or an airfield picks; the rows of the window of orders; which arrow a unit's way is drawn with; and what a battle shows - its name, the attacker's share of the two sides' worth, the marker's colour and number, the labels of each side and the icons of what is in play |
| `lib/OpenHOI3.Lua` | The install's Lua, on the original's own interpreter - Lua 5.1.4 built from its source in `native/lua` - as the original runs it: every standard library, a searcher over the install's layers, `autoexec.lua`, the base game's whole; the classes the original hands its scripts, each a global, as far as a new game's relations and the AI's answers to offers that take only tags need them, with `CFixedPoint` and the iterators; the call that gives those relations, and the AI's answer to an offer. A C# function never raises a Lua error itself, and Lua runs only in a protected call |
| `src/Map` | The map, political or by terrain: drawn by a shader from a province-id texture and two colours per province - its owner's, and its holder's striped over that where they differ - with pan, zoom and picking. And the units on it, as counters: a stack to a province, the near counter or the far one by the zoom, a click to select one, a right click to send it somewhere and the way it is going drawn; and the buildings, the port and the airfield among them, with a click that picks a unit in one; and the markers of the battles the player has a part in, with a click that opens one |
| `src/Gfx` | The Godot client's side: decodes the original's DDS/TGA textures and sprite declarations, and finds the typeface for each font the interface files name |
| `src/Gui` | The interface runtime: builds a window from the original's `.gui` declaration - its icons, buttons, checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements, in the file's order and at the file's positions - and finds them again by name |
| `src/Screens` | The screens, one class each. The top bar: the date, the country, its resources, IC, manpower and the rest, and the buttons that stop, slow and speed time. The production screen: the stockpiles with what the day did to each, the six sliders that divide IC, and the build queue with buttons that move a line and take it out. The four windows it opens to order a unit: the division designer with its brigade picker, and the ship, air and brigade builders. The technology screen: the four sliders that divide leadership, the tree a folder at a time, the theories and practicals, the research queue and a panel for one technology. The alert that says units are waiting, and the deploy menu it opens: the units waiting, the units one of them may join, and a click on the map to place it. The panel of the unit selected on the map: a list of the units above it, its header, its status, its order, its buttons, its brigades, the units under it and the battles it is in, each a class of its own. The window of orders for a fleet or an air unit. And the window of a battle, with its two sides and a line for each unit on them |
| `tools/OpenHOI3.SaveCheck` | Compares what the libraries compute with a savegame the original wrote, field by field; and, given two saves of one game, what a day of research adds with how far each technology moved between them. It also sets every starting brigade beside the save's, by number, and runs the save's two days of rotation, reinforcement and upgrades to set each beside it again, its organisation and its build queue included; and the queue again between two saves, run forward from the earlier and set beside the later. And the edges between provinces against the original's own cache of them, and what an hour adds to a marching division against two saves hours apart, with where the hours between took every fleet and air unit. And the distances by sea against the original's own cache of those. And the supply network against the provinces a save marks as out of supply and as beside a capital, and the carrying of supplies against what a start-date save holds in every province, with the convoys a new game makes against the save's, way for way and ship for ship. The oracle for everything above - but for a battle, which no save has: `battle` fights one with the install's own units and says how it went, as a trial, `seabattle` does the same with two countries' fleets, and `raid` sends one country's raiders at another's convoys for a month. And `firstdays` sets a new game as a player gets it, its two days run, beside the start save country by country - meant to differ, since the original's AI has changed laws by then - where `supplypass ... countries` does it like for like, with the save's laws given. `techgap` looks at the technology levels the start save has above the history's, and `airbattle` flies an air mission with the install's own units and tells the battles it comes to; `bombing` flies a bombing mission for some days and tells what it did to the province. `script` reads an install's events and decisions and lists every problem in them, with no save needed |
| `tests/` | 1851 tests over the parsing, the layering, the map, the numbers, the tick, modifiers and strategic resources, IC, resources, the stockpile, production, the conversions, leadership, research, manpower, unit types, historical models, orders of battle and what a brigade starts with, what units draw each day, the officer ratio and organisation, the build queue, what a country may build and what a new unit is called, the interface files, font descriptors, the sliders, the three screens' figures and the builder windows', the counters and who may join whom, the edges between provinces and a unit's march, what the weather does to it, the unit panel's figures, ports, the distances by sea, a fleet's and an air unit's orders and range, the buildings on the map and the window of orders, a land battle from its first hour to the province taken, what the hour makes of a unit in one, day and night, the tactics and their file, what a battle shows, the supply network, the carrying of supplies and what a unit draws and brings, convoys - the heap, the way, the harbours, a sailing and the sharing out of ships - the supply map mode's colours and lines, who is whose puppet, a battle at sea from the sighting to the fleet that falls back, a fleet's missions with the raid on a convoy, an air unit's two missions with the battle in the air, the eight bombing missions with the bombing, what a bomb does to a province and the day's repair, what the window shows of a battle in the air and of a bombing, a mission's area with the air window's ladder, sliders, hours, dates and colours, the list of finished battles with the air map mode's colours, icons, rings and lines, and leaders - their files, their skill, who may command what, the choice of one, how far a command reaches, what a leader does in a battle on land, at sea and in the air, the experience and traits a battle gives, and the window a leader is chosen in with the four commands it posts and the original runtime's sort, and how a tooltip is shown with what a province's, the top bar's, the production and technology screens', the sliders', the builder windows', the unit panel's, the deploy menu's, the orders windows', a battle's and the leader window's say - the designer's terrain summary, the practical's discount, a brigade's summary, a leader's own and what a trait does among them - a modifier's sources written out and the counter in text - and the reading of event and decision script, its words, its triggers and effects by the original's grammar, an event, a decision and the problems found, and what a condition answers - the scope and its forms, the blocks and the sets, the leaves and what is reported unbuilt - and what an effect does, with flags, variables and the modifiers an event gives, and an event firing and a decision taken, with their text, and what countries hold of each other - a new game's relations, the month's drift, a battle's gain, neutrality and its effect and conditions, threat's two spreaders, the most-threatening country and the alignment triangle's clamp, step and drift - and Lua: the interpreter's rounding and numbers, errors and objects crossing both ways, the install's scripts found through the layers and the relation they give, a class's global, its fields, one equality for every class, iterators, `CFixedPoint` and its backwards arithmetic, the API's answers and an offer put to the recipient's AI - and the diplomacy screen: its lists and sorts, its panel and status lines, the relation list, the ceiling for war, every tooltip and the drift tooltip - and the agreements: an offer, the AI's answer, each pact's arms and how it ends, the screen's rows, tooltips and request dialog, and a pact's message - and trade: a route's price, terms, convoy, day and ends, the AI's trading, the trade window and list, automate-trade and an offer put to the player - and aid: debt and its repayment, shared theories, lend-lease's rules and IC, licences and their Lua - where silent misreadings are expensive |

Not built yet: the cached asset build, every screen but those, and nearly every
system the tick is there to run. The game also does not stop at the end date.

In the game: the mouse wheel zooms, a middle or right drag pans, the arrow keys scroll, a
click selects a province - or, on the top counter of a stack, its unit, and clicked again
the next unit under it - a right click sends the selected unit to the province under the
pointer, with shift after whatever it was already told, and on its own province halts it,
and Tab goes round the political map, the terrain and supply. Sent at a province an enemy
stands in, a land unit fights for it from where it is: its arrow is red, its panel says so
and ends in a row for the battle, from the battle's fourth hour a round marker with a
number stands between the two sides, and a click on the row or the marker brings up the
battle's window, whose cross closes it and whose button at the upper left calls the
player's units off. A fleet's battle at sea, an air unit's in the air and a bombing have
the row and the window and no marker. A selected
province has a readout at the left - a second click on it puts the readout away - which
says who is attacking whom there and for how long, and a province that changes
hands takes the colour of whoever now holds it, with thin stripes of its owner's. A click on a port's or an
airfield's badge selects a fleet lying there or an air unit based there, and again the
next; a right click with one of those selected brings up the orders for that province -
tick one and accept - and with shift sends a fleet straight there. A selected unit's panel comes up
at the left: a click on a unit above it or under it selects that one, the cross beside its
order halts a marching land unit and takes any other unit's order back - an air unit then
flies home once its battle or bombing is over - the button on its province's name takes the map there, and the three
boxes switch its priority, its reinforcement and its upgrades. A click on its leader's
name or portrait opens the window a leader is chosen in: a click on a row gives the unit that
leader, the arrows on a row or at the head promote and demote, the column headings sort and
sort back, Filters opens a list of traits to tick - a right click on one shows it alone - and
the buttons at the foot leave the unit with nobody, take every leader off after asking, and
turn the country's own choosing of leaders off. While it is selected lines
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
Ctrl. A game starts paused, as Germany - `--country=ENG` after `--` on the command line
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
*Play OpenHOI3 (choose country)* - which asks for a country's
tag and whether it may order every unit type - *Play OpenHOI3 (ask for arguments)* - which
asks for the switches themselves, all in one box with spaces between, as in
`--country=GER --war=GER:ENG --mission=ENG:patrol:10531 --run` - *Play OpenHOI3 (headless)*, and
*Open Godot Editor*. The box reaches the game as one argument, and the game cuts an
argument wherever a space is followed by `--`. The Godot executable path is read
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
                        the clock, the two kinds of number, the generator of chance and
                        the original runtime's sort are at its root
    Commands/           every change to the game state
    Provinces/          a province, its terrain, a building, and the day's repair of what
                        was bombed
    Countries/          a country, its tag, its laws, its ministers, its alliances and
                        guarantees
    Wars/               a war and its sides, casus belli types and those held, war goals,
                        a declaration and who joins it, the call to arms, the truce,
                        peace, the surrender and the capitulation, annexation, puppets
                        and the government in exile, factions - joining, inviting, leaving,
                        their rules - their progress and the victory conditions
    Diplomacy/          what two countries hold of each other: relation, threat and
                        neutrality, a new game's, the month's drift and each change
    Agreements/         an offer and the AI's answer to it, the pacts - NAP, embargo,
                        influence, alignment - what each does and how each ends
    Aid/                debt and its repayment, shared technology, lend-lease - who may, its
                        convoys, its IC given and received - and a licence's terms
    Trade/              a trade route, its terms and price, its convoy, its day's exchange
                        and how it ends, the AI's trading each hour, and the player's
                        automated trades
    Ai/                 what the simulation asks of the install's Lua
    Modifiers/          what a modifier is, and the rebuilding of a province's and a country's
    Economy/            goods, resources, IC, the stockpile, the six shares, manpower, leadership
    Research/           technologies, their categories, the queue and the decay
    Units/              units and brigades, their types, what a day does to one, and its
                        organisation hour by hour
    Leaders/            leaders and their traits, who is in service, which unit each
                        may command, and the choice of one for a unit
    Movement/           where a unit may go, the way there, its speed and its hourly march
    Combat/             a battle and its two sides, the front, the round, the damage,
                        falling back, the attack delay, and a province changing hands;
                        and the battle at sea, in the classes named CNaval - the
                        sighting, each ship's distance and target, its shots, sinking,
                        breaking away and falling back - with the raid on a convoy;
                        the battle in the air, in the classes named CAir; what a
                        country knows of a province; and the bombing - who is in one,
                        the bombers' fire, what the bombs do to the province, the fire
                        that comes back, and what radar gives a side
    Missions/           a fleet's missions: reserve, patrol, intercept, sortie, the raid
                        on convoys and their escort, the frame they share, the look for
                        an enemy fleet, and how good a country is at each; and an air
                        unit's, in the classes named CAir: air superiority, the
                        intercept, their frame, their targets and a mission's area;
                        and the eight bombing missions, in the classes named CBombing:
                        what tells them apart, their hour, and which province they fly at
    Weather/            the weather over a province, what it does to a march and to a
                        battle, and whether it is night there
    Supply/             the supply network - areas, groups, depots and distances, and what
                        a province passes and takes - the day's pass along it, what a unit
                        draws and brings, where supplies are made, and a new game's filling
    Convoys/            a convoy, the way it sails and the heap that is found with, what it
                        wants and lands, the making of supply and resource convoys, and
                        the sharing out of transports and escorts
    Construction/       the build queue: a line, its brigades, the rules, and placing what
                        comes out
    Script/             events and decisions as read: an event, an option, a decision, a
                        mean time to happen, a trigger and an effect, and a value; and
                        what a condition answers - the scope, its forms, the sets, the
                        leaves, and what is reported unbuilt; and what an effect does -
                        the blocks, the draws, flags, variables, the modifiers an event
                        gives, and the effects on countries and provinces
    Events/             when events are asked and fire: each country's candidates, the
                        day's roll, the chance of a day, a firing, the AI's option, an open
                        event; and which decisions are offered
lib/OpenHOI3.Content/   reading an install: finding and layering it, localisation, the loader
    Clausewitz/         the script format
    Map/                the province bitmap, shapes, the edges between provinces with their
                        kinds and distances, terrain, a province's points, and the
                        declarations of the unit counters, their pointer and the arrows
    Definitions/        what common/, technologies/ and units/ declare
    History/            what history/ gives on the start date, orders of battle among it
    Gui/                the interface files' declarations, and font descriptors
    Script/             events and decisions: their words, the keyword tables, the names
                        a key may be, the readers of triggers, effects, events and
                        decisions and the on_actions, and the problems found
    Messages/           the message types of interface/messagetypes.txt
lib/OpenHOI3.Interface/ what the screens show, with no engine: sliders and placement at its root
    Text/               colour escapes, number formats, templates, breaking lines
    Screens/            the text of each screen's figures
    Tooltips/           a tooltip, what the box shows of it, where it goes and when its
                        second half comes; the map's, the top bar's, the production and
                        technology screens', the sliders', the builder windows', the
                        unit panel's, a leader's, the deploy menu's, the orders
                        windows' and a battle's
    Modifiers/          how each kind of modifier is written, and a modifier's sources
                        written out
    Map/                the unit counters: which units have one, how they stack, what one
                        says, where its parts go, the painting of one, and the shape of
                        the arrow along a selected unit's way
    Events/             an event's and a decision's text with its $VARIABLES$, and which
                        window an event is shown in and where its options stand
    Script/             what a block of conditions and a block of effects say of
                        themselves: each leaf's line, the blocks, the asterisks
    Messages/           a message, its words, and the log it is kept in
lib/OpenHOI3.Lua/       the install's Lua, run on the simulation: the state the original
                        makes, its searcher and autoexec, and what the game asks it
    Native/             the interpreter's C API, the trampoline, a state, a call, a class
    Api/                the classes the original hands its scripts, on the simulation
native/lua/             Lua 5.1.4's source, its licence, our trampoline and the build of
                        openhoi3_lua.dll, which the Lua project runs
tools/OpenHOI3.SaveCheck/   checks the libraries against a savegame the original wrote
docs/mechanics/         one spec per system: what the original computes, and how sure that is
docs/compatibility.md   where a valid file of the original's is treated differently on purpose
tests/OpenHOI3.Tests/   Sim/, Content/, Interface/ and Lua/: the tests of each library
```

Nothing under `lib/` references Godot, and `OpenHOI3.Sim` references nothing at all. The
tests run against those four as a plain .NET project, with no engine behind them - the Lua
project's native interpreter copied beside them.

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
