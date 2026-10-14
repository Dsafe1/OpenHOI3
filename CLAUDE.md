# OpenHOI3: the rewrite

*These rules are for `OpenHOI3/`. Opened from the `workspaces/hoi3` root, all four of these
files arrive at once - apply each to its own folder.*

**OpenHOI3**: a drop-in engine replacement for Hearts of Iron 3: Their Finest Hour - pointed
at an install, it plays that game, its content and its mods as they are written, by the
original's own rules, on new technology. Godot **4.7.1 .NET/mono** and C#.

**It reimplements what `hoi3_tfh.exe` does, not how it is built.** The maintainer's word,
2026-10-09: a reimplementation "is what we are working towards right now, and what I
currently want", and a drop-in engine replacement is the fitting description. So the rules
of the simulation follow the original as closely as they can be read and checked; the
engine, the rendering and every line of code are our own, and no address, offset or byte of
the original's binary comes here - the section *What this repository may not do* stands as
it was. What is read from an install is the **language its content is written in** - script,
data files, interface files - because that is where the modding knowledge is. `readme.md` is
where that goal is set out and is the document to read first; it also holds the staged plan.

**This has turned twice.** Until 2026-10-07 the rule was our own formats throughout, fed by a
one-way converter, with "our format stays clean" as the test; and until 2026-10-09 this file
and the readme said "in the spirit of the original" and "not a reimplementation". A session
reading this repository's history will find both stances everywhere; neither is the one to
apply.

**The tree is early and provisional.** What exists is one iteration - a Clausewitz reader, an
install layerer, texture decoding, the map with the start-date ownership and
terrain of its provinces, a map view, fixed-point numbers, the command queue, an hourly tick
whose daily pass works out who has which strategic resource, rebuilds each province's and each country's modifier, adds to its manpower,
works out its leadership, its IC and its home production, adds that to its stockpile and runs
its six shares of IC and its four of leadership, research among them, the unit types and
every country's order of battle with what each brigade holds on the first hour, what a
unit draws each day - supplies and fuel, troop rotation, reinforcement and upgrades - the
officer ratio and the hourly return of a brigade's organisation under it, the build queue
from an order to a unit waiting to be placed, which unit types a country may build and
what a new unit is called, a tool that
checks all of it against savegames, an interface runtime that builds windows from the
original's `.gui` files and draws their text in outline fonts, three screens on it - the
top bar, production and technology - the four windows the production screen orders a
unit from, the units on the map as counters with the alert and the menu a finished one is
placed from, the ways between provinces as the original measures them and the order, the
route and the hourly march of a land unit, the panel of a selected unit and the lines of
its hierarchy on the map, the orders that send a fleet or an air unit with the window they
are given from, the distances by sea, the buildings on the map with the port and the
airfield, and a land battle from the unit that starts it to the province that changes
hands with what the ground, the night and the rest make of each unit in it, the tactic each
side picks, and its marker and its window, and supply - which depot every
province draws on and how far from it, the day's carrying of supplies and fuel out to the
provinces, a unit drawing on the province it stands in, the convoys that carry it across the
sea and bring cut-off land's resources home, and a map mode that shows it, and a battle
between fleets at sea with its window, and a fleet's missions with the raid on
convoys, and the two days a new game runs before its first hour, and an air unit's two
fighting missions with the battle in the air, and its eight bombing missions with the
bombing and the day's repair of what was bombed, and the window of an air battle and of a
bombing, and the whole of the air orders window with a mission's area, and the air map mode
with a list of finished battles, and leaders - who is in service, who commands which
unit, how far a command reaches, what a leader does in a battle, what a battle gives him and
the window he is chosen in - and how a tooltip is shown, with a province's, the top bar's, the production and technology screens', the builder windows', the unit panel's, the deploy menu's, the orders windows', a battle's and the leader window's, and a technology's requirements and effects, and the reading of every
event and decision with their triggers and effects by the original's grammar, what their conditions answer and what their effects do, and their firing and taking with the event window and the decision list, and what each condition and effect says of itself, and the on_actions of a battle and of a pride sunk, and the messages an answered event and a taken decision raise, popped up and logged, and what two countries hold of each other - relation, threat and neutrality, and the alignment triangle threat is weighed on, a new game's relations given by the install's own Lua on the original's interpreter built from its source, and the diplomacy screen with its lists, the triangle's markers, the faction boxes, the chosen country's panel and every tooltip on them, and the AI's answer to an offer in the install's Lua, its whole `autoexec.lua` loaded and the classes the answers reach handed over as the original hands them, and the agreements' first half - an offer and its answer, alliance, NAP, guarantee, transit rights, embargo, influence and alignment with how each ends, the screen's actions and the request dialog, and their messages, and the six effects and three conditions on pacts, and trade - a route, its price, its convoy and its day, the AI's trading in the install's Lua, the trade window, the list of trades with its cancel, Automate Trade, and the bar of offers made to the player with the dialog an offer opens, and aid - debt, shared technology, lend-lease and licences, with their dialogs and the lend-lease window - and war's first piece: casus belli types and war goals, a declaration and who joins it, limited war, the call to arms and its dialog, the truce and mobilisation, the war effects and conditions, the war rows and the declare-war dialog - and its second piece's first half: peace with its row and dialog, leaving a war, the day's return of occupied land, a change of hands with consequences, the surrender progress and `CAIIntel`, the acting capital with every capital read set to the original's, annexation, the puppet, the government in exile and the capitulation, and a battle's capture changing hands with consequences, and factions - joining and inviting, leaving by script, their rules, a faction's modifier, its progress and the victory conditions - 1856 tests - and it is
being reconsidered rather than extended. Do not treat the current contents of `lib/` and `src/`
as settled, and do not preserve a structure merely because it is there.

## Five projects, and which way they may reference

Split on 2026-10-07, so that the simulation can run headless and under test; the fourth
was added on 2026-10-08 with the interface, for the same reason, and the fifth on
2026-10-12 with Lua:

| project | what it is | may reference |
| --- | --- | --- |
| `lib/OpenHOI3.Sim` | the model and the simulation | **nothing** - not Godot, not `Content` |
| `lib/OpenHOI3.Content` | reading an install: the Clausewitz format, layering, localisation, the loaders, the `.gui` declarations | `Sim` |
| `lib/OpenHOI3.Interface` | what the screens show, with no engine: the text of each figure as the original writes it, colour escapes, where an element goes | `Sim`, `Content` |
| `lib/OpenHOI3.Lua` | the install's Lua scripts on the original's interpreter, Lua 5.1.4 built from `native/lua`, and the classes they are handed | `Sim`, `Content` |
| `OpenHOI3.csproj`, with `src/` | the Godot client: scenes, drawing, input, texture decoding, typefaces, the live interface | all four |

**The simulation asks the Lua through an interface of its own**, `Sim/Ai/IAiScripts`, which
`OpenHOI3.Lua` answers and the caller hands to `CGameStateLoader.LoadStart` - so `Sim` still
references nothing. **Lua code runs only inside a protected call, and a C# function never
raises a Lua error itself** (the trampoline in `native/lua/ohoi` does): a Lua error is a
`longjmp`, and one through a .NET frame is undefined. `native/lua/build.ps1` builds
`openhoi3_lua.dll` with the Visual Studio C++ tools on `dotnet build`; it is built output and
never committed (`docs/mechanics/lua.md`, `native/lua/readme.md`).

**A screen is two halves.** What it says is a class in `OpenHOI3.Interface` - `CTopBarText` -
tested against the original's own strings; the class in `src/Screens/` finds the elements by
the original's names and sets them. Keep doing that: a format or a colour that lives only in
the client can only be checked by looking.

**Nothing under `lib/` may reference Godot** - not for a `Color`, not for a log call. That is
the whole point of the split, and one `using Godot;` there undoes it: the tests stop being a
plain .NET run. `CColor` and `System.Numerics.Vector2` stand in for the engine's value types,
`src/Gfx/CEngineTypes.cs` converts at the edge, and `CDiagnostics` holds a sink the client
points at Godot's log on startup.

`lib/` and `tests/` each carry a `.gdignore`, so the editor does not import their sources or
write `.uid` files beside them. The client's project file removes both folders from its own
compile glob for the matching reason - `Godot.NET.Sdk` would otherwise compile them twice.

## Settled decisions, which are not merely unbuilt

Nothing should be designed around these coming back.

- **One canonical start.** No custom game starts, no bookmarked starts, no isolated scenarios.
  The original offers all four; collapsing them to one means one set of starting conditions to
  balance, one code path into the simulation, and one start state to build from the history
  files. Every extra entry point is a permanent tax on all three. It also means nothing here
  reads `common/bookmarks.txt` or `scenarios/`, and no lobby is needed to choose between them.
- **An install is read as it is. There is no converter and no format of ours to author in.**
  - *Script is loaded natively* - events, decisions, triggers, effects, technologies, units,
    laws, ministers, history - in the original's syntax with the original's keywords, so a
    mod's file is a valid file here. **Be stricter than the original where possible:** a key
    nothing recognises is reported, where the original drops it silently. **Events and
    decisions are read so since 2026-10-12** (`docs/mechanics/script.md`; `Content/Script`,
    `Sim/Script`), and their conditions answer since the same day (`docs/mechanics/triggers.md`,
    `CConditions`) and their effects run (`docs/mechanics/effects.md`, `CEffects`), and events
    fire and decisions are taken (`docs/mechanics/events.md`, `Sim/Events`), the on_actions of a battle and a pride sunk among them (section 8), and each condition and effect says itself in words (`docs/mechanics/scripttext.md`, `Interface/Script`), and an answered event and a taken decision raise their messages, popped up and logged (`docs/mechanics/messages.md`) - with **the AI's
    choice of an option drawn on the game's own generator**, where the original seeds a fresh one
    with the clock, by the maintainer's choice (`docs/compatibility.md`, entry 11); keep it so,
    because a game here is replayable from its seed and its commands. **A condition about something the simulation does not hold answers false, and an effect
    on it does nothing, and both are reported as unbuilt** - never answered from the history
    files or any other stand-in, by the maintainer's word on 2026-10-12: that "would just be
    adding a system that is wrong and would be removed later on anyways". And an event that holds
    only because of one fires: "Just fire them." Build on what the simulation holds, and take a
    keyword off `CUnansweredConditions` or `CUndoneEffects` when its system arrives. Flags,
    variables and the modifiers an event gives are script's own state and are built. What the
    reading keeps of the original's,
    though it looks wrong - do not tidy it: a name is matched case and all while a keyword is
    not; a key that is no keyword is tried as a name in the original's order, so `industry`
    is the building before the technology; a tag is a text's first three letters, matched
    without case only within the original's bucket; `PHI` and `PAN` are never scopes; a
    `limit` inside a scope does nothing and the effects beside it run; an `ai_will_do`'s
    factor is whole days. Everything is reported, the original's silent drops included.
  - *Heavy assets get a cached build* - textures, fonts, and whatever else is measured to be
    slow - into our own formats, rebuilt only when a source file changes. **Measure before
    caching:** the province bitmap was assumed to need this and builds in about 120 ms, so it
    is read every start (`docs/mechanics/map.md`, section 6). The cache is derived data: never
    authored, never committed.
  - *The interface is loaded from the original's `.gui` files* and laid out by our own
    runtime. See the next section.
- **The base game first, mods second.** Their Finest Hour alone is the target until it works;
  BlackICE then becomes a third layer over it.
- **The AI does not touch the player's country.** The maintainer's decision, 2026-10-08:
  "Yes it should leave the players history laws." In the original something has moved
  every country's laws by the first screen, the player's included - Germany shows
  `two_year_draft` where its history says `volunteer_army` - and he takes that for a bug.
  So the country being played starts on the laws its history gives it and its units are
  made under them, and an AI, when one is built, chooses laws only for the countries it
  runs. Ministers were not asked about. `docs/compatibility.md`, entry 6.
- **Fixed-point numbers in the simulation.** Thousandths, as the original stores them, for
  determinism and for rounding the way the original does. No `float` or `double` in game state.
  The original has a second kind, with 15 binary places, for shares of IC and of leadership;
  it is `CFixed15` here, and a rule worked out in one kind is never computed in the other
  (`docs/mechanics/numbers.md`).
- **Every change to the game state is a command.** Nothing mutates the simulation directly;
  that is what makes a run replayable and, later, shareable between players.
- **A game is an object, not the process.** The plan starts at the map and reaches the main
  menu later, so nothing may assume the world is loaded once at startup and lives until exit.
  No static game state anywhere under `lib/`; the client creates a session, owns it, and can
  destroy it and create another. That is what "new game", "load" and "exit to menu" each need,
  and it is cheap now and a rewrite later. Start-up goes through a boot scene that today only
  starts a session, so the menu has a place to arrive.
- **A mechanics spec before each system.** The findings mix formulas with addresses, so
  coding straight from one leaks offsets into this repository. Write the spec first -
  formulas, ordering, rounding, caps, no addresses - citing the findings file it came from.
- **No game assets or data files are redistributed here.** Players who want the original's
  content must own a legitimate copy. This is a hard rule, not a preference: do not commit
  anything extracted from an install, including the cached build's output, test fixtures lifted
  from it, or a texture "just for a screenshot".

## The interface: the original's files, on a scalable canvas

Decided 2026-10-07. The `.gui` files are neither discarded nor translated once on disk,
because mods ship their own - BlackICE changes all 33 it shares with Their Finest Hour and
adds 20.

**Built 2026-10-08, as far as three screens and four windows need**, and on 2026-10-09
the deploy menu and its alert, the panel of a selected unit, the window of orders a
fleet or an air unit is sent from, and the window of a battle.
`docs/mechanics/interface.md` is the spec, and `topbar.md`, `countryviews.md`,
`sliders.md`, `productionscreen.md`, `technologyscreen.md`, `builders.md`,
`mapunits.md`, `unitpanel.md` and `combatview.md` the
screens', `tooltips.md` how every tooltip is shown, with `movement.md` for the right click that orders a land unit and
`fleetsandair.md` for the one that asks a fleet or an air unit what it is to do;
`lib/OpenHOI3.Content/Gui` reads the files, `src/Gui` builds windows, icons,
buttons, checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements
from them, and `src/Screens` holds the screens. **The unit counters are not interface
elements**: they are declared in an `objectTypes` block of a `.gfx` file, read by
`lib/OpenHOI3.Content/Map`, worked out and painted in `lib/OpenHOI3.Interface/Map` and
drawn on the map by `src/Map/CUnitCounters` - `mapunits.md`. They and the deploy menu were
checked with `--click`; the maintainer looked at them on 2026-10-09 and the near counter
is laid out from a screenshot of the original's he gave. **A land unit's march -
`movement.md` - was checked with `--click` too, and he has looked at it**; its
distances agree with the original's own cache edge for edge and its speed with savegames
hours apart, division by division. The hour of an arrival rests on the reading. **The unit
panel - `unitpanel.md` - was built from a reading and checked with `--click`; he set it
beside the original on 2026-10-09 and its bars were wrong twice**, under two guesses at
how a rotated icon turns - the second one "confirmed" by a fit to the art that was a
coincidence. His screenshot gave the rule, and two more: the panel's entries touch, and
where an edit box puts its text (`interface.md`, sections 2 and 7). All three are measured
on that screenshot and none is read. **Fleets and air units - `fleetsandair.md` - and the
buildings on the map were built from a reading and checked with `--click`, and he has
not looked at them.** Their sailing, their routes and the whole table of distances by sea
agree with his saves and the original's cache; the size of a building's picture and the
orders window rest on the reading and on the art. **The art corrected one rule on the
way**: a window's background that is a plain picture keeps its own size at the window's
corner and is not stretched - no window built before had one (`interface.md`, section 3).
**A land battle - `landcombat.md` - is the least checked thing here: read out of the
original, tried with `--click` and with `OpenHOI3.SaveCheck battle`, and set against
nothing.** No save has a battle in it and the original's rolls cannot be made again, so its
figures rest on the reading alone. The maintainer's word on 2026-10-09 covers five things:
a week for two like divisions "seems right", an attack delay stops all movement but the walk
into the province attacked, occupied ground is striped (`map.md`, section 10), units that
attack together fill the front until the width is used, and one that joins later waits in
reserve and comes up by its chance. The order the first hour puts units on a front in, and
the score between two reserves, are still the reading's alone. Only
land units fight. Fifteen things bear on a unit each round - the ground, a river, forts, the
night, experience, being dug in, crowding and the rest (`landcombat.md`, section 4) - and
`CCombatModifiers` is where the seven that wait will go: leaders, encirclement and
the others listed there. **Each side picks a tactic** out of `common/combat_tactics.txt`
(`landcombat.md`, section 11), and with a battle's leader not built it is nearly always the
plainest.
**Reading for this corrected the fact base twice**: the weather
and the night are against a unit's attack and leave its defence alone, and of a tactic's two
numbers the one written down as what counters it was its own. There is no weather
yet, no diplomacy and no AI, so a war is given with `--war=GER:POL` and the enemy stands
still.
**A battle on screen - `combatview.md` - was built from a reading, checked with `--click`,
and then set against three screenshots of a battle of his; he looked at ours on 2026-10-09
and found it good, and asked for one thing: opening a battle's window does not move the map**
(`docs/compatibility.md`, entry 8). It shows the marker - a land battle's alone, and from its
fourth hour - the row in a unit's panel, the window. Its upper half lies on his to
within a pixel, after the screenshots corrected four things about how the interface draws,
all measured and none read (`interface.md`, sections 5 and 7): **a progress bar's picture
is drawn upside down** - one of the unit panel's two bars had been the wrong way round
since it was built - **a turned flag turns about its own middle** where a turned bar turns
about its corner, **a round shield cuts its flag to a disc**, and **a name too long for a
`fixedsize` box ends in three dots**. The number on the marker, the bar and the marker's
colour are one figure, the attacker's share (`combatview.md`, section 6), and it read 6 on
his screenshot and on ours for the same kind of fight.
**The supply network - `supply.md` - is the best checked thing read out of the original
here**: which depot every province draws on, worked out from areas, groups of areas on one
side and a score, agrees with three savegames province for province - and the maintainer
then watched the original move the depot of Manchukuo and Korea as he released one Korea
and the other, and the score gives each state he saw (`supply.md`, section 9). **The
original takes some days to group a released puppet with its master and this does it at
once**, because the network here is made whole each time it is asked for - kept so by his
decision (`docs/compatibility.md`, entry 9). His account before
the reading - a puppet is in its master's network unless the two do not touch, allies keep
their own - was read exactly so. The areas it makes are also what home production, the
`overseas` modifier and the placing of a unit ask, each of which had guessed at them.
**The daily pass - `supply.md`, sections 11 to 17 - is the second of supply's three pieces,
built on 2026-10-10, and it reproduces the original's own figures**: with a save's units,
laws and production, a day, the filling of the provinces and a day give 184 of 234 networks
the same in every province and every figure the save keeps, Germany's 245 provinces and
Sweden's 210 among them (124 before the convoys, 171 before what leaders do to a unit's use). **It is written to do what the original does step for step, its oddities with it** -
the order its sort leaves equal provinces in, a share that stays cut, fuel tested by today
where supplies are tested by yesterday - because who goes short on a full road is decided by
nothing else; do not tidy them. A unit draws on the province it stands in, a marching unit
takes fifteen days of supplies with it, and a day's supplies and fuel are made where the
factories are and travel to the stockpile a province a day. **A new game fills its provinces
when it is made**, which takes about two seconds, between the two days it runs before its
first hour.
**Those two days - `tick.md`, section 6 - were built on 2026-10-10 and are checked like for
like**: a new game runs what the original runs as a game is entered - a day, the filling, a
day, a month's pass, one hour, with the clock standing still - as `CTick.RunFirstDays`, and
all of it with `IsBeingMade` set, which is the original's "no game is running yet": a
brigade may then have its own figure of organisation and nothing else, the month decays
nothing and a convoy is manned for nothing. Given the laws Germany's days were run under,
it is the start save's Germany brigade for brigade and good for good, supplies 0.030 apart.
**A plain new game is not the original's first screen and is not meant to
be**: there the AI has changed laws by then, and here the player keeps his history's
(`docs/compatibility.md`, entry 6) - so nothing holds a reserve down before the first hour
played does, and one sent to attack before that hour fights above its ceiling. His word on
that, 2026-10-10: "The two days for the brigades is ok for now. Maybe it comes up again
later" - leave it. **The savegame tool's older checks load a game with `firstDays: false`** and
reason from day nought; leave them so, they are how each rule was pinned down.
**Convoys - `convoys.md` - are the third piece, built on 2026-10-10, and the best checked
thing here after the network**: a new game's convoys are the start-date save's, all 261 -
the same runs, the same way province for province, the same transports and escorts on each,
the same free ones for every country. So the route search and **its heap, down to which of
two equal entries comes out first**, are the original's, and so is the order of an area's
harbours - **largest naval base first**, which is what "an area's first harbour" means
everywhere; do not tidy either. A convoy lands in harbours and the daily pass carries it to
the depot; what cut-off land yields waits in its block's largest harbour. **Everything lying
in cut-off land is moved there each day, and a province's pool is also the stockpile of any
country whose capital it is - so the goods of a country that does not exist are shipped to
whoever holds its capital.** That is the original's, shown by the save, and the maintainer's
word on 2026-10-10 was to follow it; it was first built with the yield in a pool of its own
and the harbours' money came out wrong. With it go two more things the save bore out:
consumer goods and supplies are charged whatever the stockpile holds, and a day ends by
holding every good in a land province between nothing and 99999. Nothing attacks a convoy,
none is built, and trade and lend-lease have none. **The supply map mode - `supplymap.md` - was read branch by branch and
looked at once with `--mode=supply`**; `Tab` goes round political, terrain and supply, and
the tooltip of the province under the mouse carries the original's lines. There is no
intelligence, so it shows the player's own land. The maintainer looked at supply in the
game on 2026-10-10 and raised nothing against it.
**A battle at sea - `navalcombat.md` - is the first of five pieces air and naval combat was
cut into on 2026-10-10, and like the land battle it was read out of the original and set
against nothing**: no save has one. **The cut is this project's and the maintainer has not
spoken to it** - he said to go on, not how: fleet against fleet; a fleet's missions with the
raid on convoys; the battle in the air; bombing; and all of it on screen. A fleet at war looks
each hour at the first enemy fleet in its sea province and rolls to see it; each ship then
stands at **a distance of its own**, picks a target by its side's positioning, shoots, and
closes or opens the distance; a side whose ships average under one organisation has lost and
runs for its port; and a battle nobody loses ends when an hourly roll has set the fleets
counting and passes again. **It is written to roll in the original's order, its oddities
with it** - a ship that has lost the enemy shoots at its own side seven times in a hundred,
and the walk that picks which friend ends on the side's last ship; a transport, having no
target, runs in at full speed; a crowded fleet's own penalty leaves it hardly ever finding a
target at all - do not tidy them. **An idle fleet of a country nobody plays starts no battle**,
which is the original's and is why `CGameState.Played` exists; with no AI the enemy's fleets
never leave port, so `--sail=ENG:10531` sends one to sea for a trial by hand. There were no
leaders in it until 2026-10-11 (`leaders.md`, section 10). Reading for it corrected the fact
base's record twice.
**The sea battle on screen - `combatview.md`, section 10 - was built on 2026-10-10 from a
reading and checked with `--click`; the maintainer looked at it that day - "The sea battle
window looked perfect"**: the land
battle's row and window, the window with halves of its own (`CNavalSideView`,
`CCombatShipEntry`) - each side's positioning and a list of its ships, **each ship's picture
standing along its row by the ship's distance**, forty and more at the far end. No picture of
the original's was measured against it, so it has his eye and no measurement. **It had the
land battle's marker on the map when he looked, and has none now**: the marker is a land
battle's alone in the original, and from its fourth hour (`combatview.md`, section 8) -
found later that day, reading for the air side, which corrected a sentence of the fact base.
**That has not been put to him**; `CCombatViewText.HasMarker` is the one place that says so.
**A fleet's missions - `navalmissions.md` - are the second piece, built on 2026-10-10 and,
like the battle, read and set against nothing**: reserve, patrol, intercept, sortie, the raid
on convoys and their escort; **transport and invasion are not built** and wait for carrying.
The maintainer's word that day set what followed: "Fleet missions first, then the combat
sceen, and then we tackle the games 2 days" - so the sea battle's marker and window came
next, ahead of the air, and the new game's two days after them; all three are built. A mission is an order asked
every hour whether it is still going, and that asking is where it does everything; all of
them send a **player's** fleet too worn for its stance to the nearest harbour, and nobody
else's. **A raid on a convoy is a battle**: the convoy's two counts become a throwaway fleet
of `destroyer`s and `transport_ship`s and five rounds of the sea battle are fought on the
spot, the raider shooting with `convoy_attack`. **Six things in it are the original's and
look wrong, and are built so - do not tidy them; `navalmissions.md`, section 12, lists them,
and the maintainer's word on them, 2026-10-10, is "All those things are correct, and they may
be bugs, but thats ok"**: an order that has told its fleet to return to
base never takes that back, so **an interceptor goes out once** and then lies in port under
its order; **a patrol sinks convoys** every twelve hours like a raider; a sortie ordered
from anywhere but its base is left with a path that does not begin where it is. **How well a
country knows a sea province is worked out once a day**, not asked for on the spot: 2 where
one of its units was at the day's turn, 9 where that unit was a patrol shadowing somebody,
and land a unit stands in at 9 at once - which is how a fleet in port sees without rolling.
Whether an ordinary game of the original's does that daily or hourly was not read; the
maintainer's word, 2026-10-10: "I think its daily", which is what is built.
There is no AI, so `--mission=ENG:patrol:10531` gives a country's fleet a mission for a
trial by hand. Reading for it corrected the fact base's record five times - the plainest
that the raid's caller the record had under the raid order is the patrol's.
**The battle in the air - `aircombat.md` - is the fourth piece, built on 2026-10-10, read
out of the original and set against nothing, and the maintainer has not looked at it**:
air superiority and the intercept as missions (`CAirMissions`, `CAirSuperiority`,
`CAirIntercept` in `Sim/Missions`), and the air battle (`CAirBattle`, `CAirFire`,
`CAirModifiers`, `CAirRetreat` in `Sim/Combat`). An air mission is an order asked every hour
whether it is still going, and outlives its sorties: home under "return to base" the unit
flies again until the order's end hour. Air superiority goes round the mission's provinces
and flies one province ahead of the strongest enemy air unit it knows of; the intercept waits
at its base, watches two steps around its provinces and turns for home once it has caught
its target. **Anything in the air may be fought; a unit sitting at its base only by a unit
under one of the two orders, and only while it is itself fit to fly** - which corrected the
fact base, whose record had both orders needed everywhere. A battle is four hours of each
wing firing its `air_attack` in shots at one wing it keeps to, and a unit out of one starts
and joins no other for an hour **while it stays where it is - its first move ends the wait**,
which the reading for bombing corrected on 2026-10-10. **Eight things in it are the original's and look odd, and are built
so - do not tidy them; `aircombat.md`, section 9**: a beaten unit beside the sea flies out to
sea and not home, a worn unit at its base cannot be attacked and yet takes off every third
hour, a wing's defence and its `air_defence` do nothing, a critical hit multiplies the
strength alone. The maintainer's word on 2026-10-10: that the defence does nothing is "a bug
in the original", and of the others he was shown, "Just note them" - none is to be mended
unless he says so. **What a country knows of a province was read whole for it** (`CIntelligence`,
`aircombat.md`, section 6): 9 for whoever holds it and their friends and for a country with a
unit on its ground, 2 with a unit in its water; radar is not built, and nothing draws a fog
of war. **A mission's area is a list of provinces that comes with the order**, and the air
orders window makes it - **built whole on 2026-10-11, read out of the original, tried with
`--click`, and the maintainer has not looked at it** (`fleetsandair.md`, section 4;
`aircombat.md`, section 2): four shapes (`CAirArea` - point, region, circle, cone, the cone's
arc in single precision as the original works it), three sliders, a right click on the map
that aims a circle or a cone and moves a point or a region, shift that adds, the boxes of the
hours, two dates held to the defines' and "continuous", the ladder that ticks the first row,
what the window keeps for the next, and the map coloured by the unit's reach while it is up.
**Nine things in it are the original's and look odd, and are built so - do not tidy them**,
the plainest that **the air window's priority is never sent**; the readme's *Next up* lists
them. Regions are read from `map/region.txt` for it (`CRegion`, `CRegionReader`). With no AI,
`--mission=GER:airsuperiority:1928` gives a country's air unit a mission for a trial by hand,
and `--select=Luftflotte II` selects an air unit no click can reach at its base outside the
air map mode.
**The air battle and the bombing on screen - `combatview.md`, section 11 - were built on
2026-10-10 from a reading and checked with `--click`, and the maintainer has not looked at
them**: the land battle's row in a unit's panel and its window, with a third kind of half
(`CAirSideView`, `CCombatWingEntry`, and `CAirCombatText` for what they say). A side's list
is its wings; **the bombed side of a bombing lists first what of the province the first
bomber's mission bombs** - each building, what lies there of a good, what is made of one -
each with a bar of what is left. A bombing's window stays up with nobody on the bombed side,
and its retreat button sends an air unit where a beaten one goes and leaves it no order
(`CFallBackCommand`). **Neither has a marker on the map, and that is the original's**: it
shows them only once they are over, as icons in its air map mode, counted from a list of
finished battles - both built since.
**The air map mode - `airmap.md` - was built on 2026-10-11, read whole out of the original,
tried with `--click`, and the maintainer has not looked at it**: grey, green where the
player's intercepts reach and red where its air superiority does, the sea too; a ring of range
round each of its air bases for each range of aircraft there, drawn with the original's
`map/Sector.dds` and coloured red to green by strength as its `circle.fx` does; the air bases
and anti-aircraft guns as large pictures by their level, **the large air base asked before any
counter on a click** - how an air unit at its base is picked there; and the three icons of the
week's air battles and bombings (`CAirMapColours`, `CAirMapIcons`, `CAirBaseCircles`,
`CAirMapText`, `src/Map/CAirMapLayer`). Under it, **`CCombatHistory`, the list of finished
battles, which keeps a week**: every battle of every kind goes on it as it ends, each side
named by the first of `CCombatSide.Standing` - the countries still in the fight, made again
from the units left whenever one is taken off, so a beaten side is named by nobody; do not fold
it into `Countries`, which keeps every country a side fought as. **Four things in it are the
original's and look odd, and are built so - do not tidy them; `airmap.md` lists them**: its
colours are made when the player's fighter orders start or go and not each day, a count of
battles is a signed byte, a beaten side is named by nobody, and an air base the player knows
little of is drawn at the size of a level 1. Its colours' alpha, which the original's shaders
use in a way not read, is laid over the ground turned grey - ours.
**Leaders - `leaders.md` - were taken up on 2026-10-11 and cut in four, the cut this
project's: who they are and who commands what; how far a command reaches; what a leader does
in a battle; and the window he is chosen in. The first is built, read whole out of the
original, and it is the original's unit for unit**: the start save's 1555 unit leaders and 101
countries' lists of leaders in service all agree (`CLeaderCheck` in the savegame tool's
start-date run). `history/leaders` and `common/traits.txt` are read (`CLeaderReader`,
`CTraitReader`); a leader's history is folded to the start and his experience raised to his
skill's floor; every leader with a rank is put in service as a game is made, and others on
the day their history first gives them one, at rank 1 (`CLeaders`, `Sim/Leaders`). **Every
unit with no leader is given the best free one of its country**, in the order the units were
made: skill and one in thousands, a set amount for each trait effect that suits the unit, the
first of equals winning, and a land unit taking only the exact rank of its level. A unit put on
the map or joined gets one the same way while the country's `AutoAssignLeaders` is on. The
panel shows a unit's leader, in red without the rank, and his portrait; the counter keeps the
unit's name (`docs/compatibility.md`, entry 7). **The second piece, how far a command
reaches, is built too, read whole and checked against the same save** (`leaders.md`, section
8; `CCommandReach`): a headquarters reaches in full within its first brigade's radio times the
`RADIO_*_LEADER_DISTANCE` define of its level, the radius over the distance out to twice
that, and nothing beyond, the links multiplied up the chain; a trait comes down the chain
halved at every step. The army's commander adds his skill by his reach to every brigade's
organisation ceiling - all 764 brigades under one agree with the save, 142 did - and the
army group's takes a twentieth a point off what a unit uses, with the leaders'
`supply_consumption` traits. **A new game's stockpile is reckoned before its leaders are
chosen**, as the original does it, so `CLeaders.SetAtStart` puts on only the files' leaders and
`CLeaders.AssignAtStart`, called after `CStockpile.AddUnitGoodsAtStart`, chooses the rest; keep
that order. The panel's radio icon and a broken dark red line on the map show a unit out of
range, checked with `--click`. **Distances here are the game's kilometres, which are
quarters of a map pixel** - the same measure as an aircraft's range: a corps reaches 200 km in
full, an army 400, an army group 600, a theatre 2000. **Each rank's skill does one thing of its
own**, as the maintainer set out on 2026-10-11 (`leaders.md`, section 8): a division's combat
efficiency, a corps' chance of bringing up a reserve, an army's organisation, an army group's
supply use, a theatre's stacking penalty - the division's, the corps' and the theatre's in
battle. **The third piece was cut again, this project's cut: a land battle - built
2026-10-11, `leaders.md` section 9, `CCombatLeaders` and the leader terms through
`CCombatModifiers`, `CFrontLine`, `CCombatDamage`, `CAttackDelay` and `CMovementSpeed`, read
out of the original and set against nothing as the land battle is - then sea and air, built
the same day (`leaders.md` section 10, through the `CNaval`, `CAir` and `CBomb` classes of
`Sim/Combat`), then the experience and traits a battle gives, built the same day too
(`leaders.md` section 11; `CCombatExperience` and `CTraitGain`).** A unit's leader's bonus is
gated whole on his rank and skill, so an unled unit gets none of its commanders' offence or
defence traits; a side's leader is its highest ranked, first of equals. At sea and in the air
the bonus has no trait in it, and **a unit's own leader also draws a fleet up, picks targets
and finds ships for bombers whatever his rank** - `CCombatLeaders.UnitSkill` and not
`Bonus`; eleven traits come in there (`leaders.md`, section 10). Two technology figures went
in with the admiral because they sit in the same lines: `targeting_chance` and
`targeting_choice`. The aggressiveness a tactic is picked with is 0: it comes from the AI's
plans. **Experience comes only from battle, in each unit's own hour** - `CMovement.RunUnitHour`,
after its order - and rolls a die of the game's own generator there, so a battle's rolls hang
on it; keep it in that place. **A trait is earned by hours banked at the head of each side's
fire and counted when a unit leaves a side** (`CCombatSide.Remove`, which takes the state for
it) **and at the battle's end** (`CCombats.Finish`); a tactic's `trigger` and a gainable
trait's are one class now, `CCombatCondition`, read by `CCombatConditionReader`.
`CLeaders.SetLeader` takes the original's two flags - throw away his banked hours, cost the unit
organisation - and **only a player's change of leader passes them**; the choosing passes neither.
**The fourth piece, the window a leader is chosen in, is built too** (2026-10-11, `leaders.md`
section 12; `CLeaderSelectionPanel` and its rows in `src/Screens`, `CLeaderWindowText` and
`CLeaderTraitFilter` in `Interface/Screens`, and four commands), **read out of the original,
tried with `--click`, and the maintainer looked at it on 2026-10-11: "Everything looked good
with the leaders"**, with each side's leader in a battle's window.
**Tooltips - `tooltips.md` - were taken up next, on 2026-10-11, and the mechanism is built:
read out of the original, tried with `--click`, and the maintainer has not looked at it.** One
box, `core.gui`'s `ToolTip` (`CTooltipBox`), on a layer over everything; each frame
`CTooltips` asks the element under the mouse - a `CGuiObject`'s `Tooltip` - or over the map the
province (not built: `MapTooltip`), adds a button's shortcut line, and shows a tooltip's
delayed half under a line of fourteen dashes once the mouse has been still past the next whole
second of the clock; the box hangs 64 below its element's corner, stands on it in the lowest
fifth of the screen, and is kept inside the right and bottom edges only. **No `.gui` file of
the original's or of BlackICE's sets a tooltip of its own** - every tooltip is a screen's code,
chosen by the element's name - so what each says comes screen by screen, in the order the
readme's *Next up* gives. `--click` takes `m460,40` to move the pointer
without clicking and `w2` to wait two seconds, which a delayed half needs, and
`d560,140>760,340` to drag - **a window whose file says `moveable` is dragged by the left
button held anywhere on it but on a button, a scrollbar or a list** (2026-10-12, the
maintainer's account of the event window; `interface.md`, section 7).
**The map's tooltip is built too** (2026-10-11, `tooltips.md` section 7, `CProvinceTooltip` in
`Interface/Tooltips`): `ProvinceTooltip_Build` read whole - a selected army's `UTM_*` warning,
the name and owner, the terrain, political, supply and air modes' parts, the distance from what
is selected - tried with `--click`; the maintainer set it beside the original on 2026-10-11 and
"the style looks good", and his screenshot has the province's tooltip standing over the
minimap, which is not built (`tooltips.md`, *Not established*). **The top bar's is built too**
(2026-10-11, `topbar.md` section 14, `CTopBarTooltips`): by the element's name, read whole -
each picture a heading with its description delayed, each resource's number its day's ledger,
IC, dissent, diplomacy, national unity, officers and manpower - tried with `--click`, and the
maintainer looked at it on 2026-10-11: "Looks good". **Four of them end in where a modifier comes from**: a
country's rebuild now keeps every source it adds, in order, the scaled ones as their copies
(`CCountry.ModifierSources`), and `CModifierText` writes them out as the original does, each
kind by its display key and two flags, percent and more-is-better, read for all 107 of the
original's kinds (`CModifierKinds`, `modifiers.md` section 8). **A table's `\n` is a line
break from now on**, made so where localisation is read. Three oddities are kept as read,
`topbar.md` lists them. **The production and technology screens' are built too** (2026-10-11,
`productionscreen.md` section 8, `technologyscreen.md` section 9, `sliders.md` section 6;
`CProductionTooltips`, `CTechnologyTooltips`, `CShareTooltips`), read whole, tried with
`--click`, and the maintainer looked at them on 2026-10-11: "Looks good". **A slider's tooltip is what its share
would do at the slider's position**, before it is sent - the two screens turn a position into a
share in different orders, rounding before or after dividing, and each is kept. An entry window
answers for all its elements through `CGuiWindow.SetTooltips`, **which leaves a text box letting
the mouse through** so that a background button keeps its clicks. **`£` and a name in a text draw
that unit's counter**, the middle of `GFX_counter_` + name (`CCounterIcons`, `CTextIcons`). **Its
last two parts were built on 2026-10-12** (`technologyscreen.md` section 9; `CRequirementText`,
`CTechnologyEffectsText`), read whole and tried with `--click`, and the maintainer has not looked
at them: why a technology may not be researched is its `allow` block's account of itself, every
condition with an asterisk and its words turned round inside a "none of", **indented as the mod's
DLL corrects the original, by the maintainer's word - "Please use the fixed logic"**
(`docs/compatibility.md`, entry 10); what levels do is what a first level allows, each unit
type's figures on one line and the country's, all times the level the entry speaks of. Only the
base game's conditions have words yet. **The four builder windows' are built too** (2026-10-11, `builders.md` section 11,
`CBuilderTooltips`), read whole, tried with `--click`, and the maintainer has not looked at
them: **only the column headers, the step buttons and the designer's combined arms and terrain
speak** - everything else in them is silent in the original too - with the designer's **terrain
summary** worked out from its brigades' adjusters, and **the practical icon** answered by the game
screen before any window, from the key the window keeps on it. **The step of ten is Ctrl**, which
the step buttons' own tooltip says; it was shift until then. The game state now carries the
terrains in their file's order (`CGameState.Terrains`) for the summary. **A scrollbar's end
buttons repeat while held, Shift makes a press ten times as long and Ctrl a hundred** - Shift
by the maintainer's word, "Its shift." (2026-10-12,
`interface.md` section 2, `CScrollbarSteps`), read out of the original after the maintainer's
account of the sliders, tried with `--click` (which holds a button with `t1.5:`, Ctrl with `c`
and Shift with `s`): **each frame counts the whole time held in 0.15 s repeats**, so a held button speeds up
the longer it is held and with the frame rate - the original's, kept. A list box's bar moves a
line a press. **The unit panel's are built too** (2026-10-12, `unitpanel.md` section 10;
`CUnitHeaderTooltips`, `CUnitStatusTooltips`, `CUnitOrderTooltips`, `CBrigadeTooltips` and
`CLeaderTooltip`), every entry's routine read whole, tried with `--click`, and the maintainer
has not looked at them: **a brigade's summary** names its model and each technology's level -
a level with no text of its own is `Model` and **a year**, the technology's `start_year`,
`first_offset` and `additional_offset` - and **a leader's tooltip** is his everywhere he is
shown. **A text box over a button answers first and leaves the button its clicks**
(`CGuiObject.PassiveTooltip`, opted into by name with `CUnitPanelEntry.AnswerFor`), as the
original's topmost element answers - looked for down through a list's rows too. An idle unit's
order describes itself as nothing, and a headquarters out of reach is yellow, not red - both
the original's. **The deploy menu's and the orders windows' are built too** (2026-10-12,
`mapunits.md` section 10, `fleetsandair.md` section 8; `CDeployMenuTooltips`,
`COrdersTooltips`), read whole, tried with `--click`, and the maintainer has not looked at
them: a deploy line lists its unit's brigades, a finished fleet's or air unit's nothing;
`accept` says why only while it cannot be pressed; and **both windows' priority buttons say
nothing**, the original asking twice for a name its file does not have - kept. **A battle's are
built too** (2026-10-12, `combatview.md` section 12; `CBattleTooltips`, `CBattleUnitTooltips`,
`CBattleRowTooltip`), read whole, tried with `--click`, and the maintainer has not looked at
them: the in-game screen gives the window's elements to the window **by their group**, so its
close button alone says `CLOSE` and the modifier icons, carrying their id, say the modifier's
name and description; **the attacker's reserves say their chance of coming up and the
defender's a unit's whole**, and in a battle's row **one fleet's ships run together**, both the
original's. **The leader window's are built too** (2026-10-12, `leaders.md` section 13;
`CLeaderWindowTooltips`), read whole and tried with `--click`: a trait's icon says its name and
a line for each effect, and what the window leaves unsaid falls to the unit panel header's
names - so a row's button says "Assign new leader.". A leader's own tooltip says how many he may
command only where he commands a fleet or an air unit, which corrected the unit panel's
reading. **That the original shows them at all the bytes read did not settle**; the
maintainer's screenshot of it, 2026-10-12, has a trait's name over its icon. **`@` and a tag in a
text draw that country's flag**, 23 by 11 (`CFlagIcons`); **an occupied province's flags are the
holder's and then the owner's**, which the decompiler shows the other way round and the listing
does not. The supply mode's lines are the original's put together line for line, colours and
the line on infrastructure with them, and the readout of a selected province no longer repeats
them. **Its list is sorted with the original runtime's own `std::sort`, `CStdSort`,
copied step for step** - it is not stable, so which of two leaders of one skill comes first is
the sort's - and checked by running the original's code under an emulator on 1,360 lists; use
it wherever the original sorts with `std::sort` and the order of equals shows.
**Twenty-five things in the pieces are the original's and look odd, and are built so - do not
tidy them; `leaders.md` lists them**: the plainest that an order of battle's leader goes on
whoever he is, that nothing later in a leader's history than his first rank is ever applied,
that a command's reach drops from half to nothing at twice the radius - the one of them put
to the maintainer - that `fort_defence` and `surprise_chance` do nothing in a land battle,
`dissent_impact` nothing at sea or in the air, that a raid on a convoy never counts the
hours its raider's leader banked, so `seawolf` cannot be earned by raiding, and that a unit's
portrait opens the leader window where its leader button cannot be pressed.
**Bombing - `bombing.md` - is the fifth piece, built on 2026-10-10, read out of the original
and set against nothing, and the maintainer has not looked at it**: the eight bombing
missions (`CBombingOrders`, `CBombingMissions`, `CBombingProvinces` in `Sim/Missions`), the
bombing itself (`CBombing`, `CBomberFire`, `CProvinceBombing`, `CAntiAircraft`,
`CBomberModifiers`, `CBombTargetModifiers`, `CRadarCover` in `Sim/Combat`) and the day's
repair (`CProvinceRepair` in `Sim/Provinces`). A unit flies out, bombs a province for four
hours and is told to return the hour after. **A bombing is a battle of its own kind, one to
a province and kept apart from a unit's battles** (`CUnit.Bombing`, not `Combats`): a
division under bombs is not "fighting" and marches as before, and an air unit in one stays
over it. Each wing fires at a target its mission goes for and then **bombs the province** -
whole runs of its strategic attack, never fewer than one. **A building has two levels since
then**: `BuildingMaxLevel` is what was built and `BuildingLevel` what works of it; a
province's modifier is made from the second, "is there a port" is asked of the first
(`CMovementRules.BuildingLevel`, by name, answers the first), and which the remaining rules
mean was not read - the readme's *Loose ends* says what each was given. **Nine things in it
are the original's and look odd, and are built so - do not tidy them; `bombing.md`, section
12 - and none has been put to the maintainer**: a bomber with no leader has a ship on its
list of targets one time in a hundred, ten more for each point of his skill, so strikes at
ships wear the port and hardly touch the fleet; a fleet in port fires its air attack three
times over through its province; a second unit cannot join a bombing of the ground alone;
ground attack goes round its list and weighs nothing, and interdiction flies only at ground
its country sees into - **which here is none of an enemy's, because the part of the
original's intelligence that sees ground beside one's units is unread**. **A carrier's
aircraft are not built**: a carrier is not a base here yet. With no AI,
`--mission=GER:strategicbombing:1928` gives a mission for a trial by hand, and the orders
window gives all eight - checked with `--click`.
**The province readout under the top bar is scaffolding and shows only for a selected
province** - his word, 2026-10-09, when it followed the mouse: "Very distracting."
**The four windows a unit is ordered
from - `builders.md` - have been tried by hand once and never measured against the
original.** That try found seven buttons that could not be pressed in windows every
scripted check had passed, because the checks opened and filled them by name and pressed
nothing: **check a screen with `--click`, which sends the mouse's own events, before
calling it working.** Edit boxes are read and not drawn; a dialog's
shields are drawn since 2026-10-12, the popups' flags. **The two later screens were built from a reading nothing
checked against a running game.** The maintainer tried them twice on 2026-10-08 and
what was wrong is mended - the readme's *Next up* and *Loose ends* say what is left.
**When something on screen is a unit or two from the original, measure a screenshot
against the art and read the routine**; two guesses at where centred text goes were
wrong before the font's own arithmetic was read. What follows is the reasoning, which still holds.

- **The files do not assume a screen size.** Every element has a pixel size, a pixel position
  and an `orientation` naming the corner or centre of its parent the position is measured from.
  184 of the 1573 orientations in Their Finest Hour's 36 files are something other than
  `UPPER_LEFT`, and the largest fixed window is 1120 x 768. It is tempting to describe the job
  as "translating from 1920 x 1080"; nothing in the files is tied to that size.
- **So the runtime adds density scaling and nothing else.** A `.gui` pixel is one unit on a
  1920 x 1080 reference canvas, an orientation becomes an anchor, and the canvas is scaled to
  the screen. `project.godot` is already set that way: 1920 x 1080, `canvas_items` stretch,
  `expand` aspect. Do not rewrite positions per resolution and do not bake a scaled copy.
- **Element names are the contract.** A `.gui` file is layout only; what a window does was in
  the executable, bound to elements by name. Each screen here needs code that finds its
  elements by the original's names, so a mod that restyles a window keeps working and one that
  renames an element breaks as it would in the original. The fact base's four interface
  write-ups - `reversing/findings/FINDINGS-gui.md`, `reversing/findings/FINDINGS-guistatic.md`,
  `reversing/findings/FINDINGS-guicontainers.md`, `reversing/findings/FINDINGS-guilive.md` -
  are where the element kinds and the lookup rules are.
- **The original's bitmap fonts are not drawn.** The maintainer's decision, 2026-10-08, on
  seeing the top bar scaled: glyphs a dozen pixels high cannot be enlarged into anything
  readable, so the old font system is replaced. A font is still *named* exactly as the
  files name it - `font = "Arial12"`, a `bitmapfont` entry, a `.fnt` in `gfx/fonts` - and
  the `.fnt` is still read, for three things: the typeface its `info` line says the
  glyphs were made from, how wide its characters came out, and the height of a line.
  The typeface is then drawn as outlines at the size that reproduces those widths, so
  text fits the boxes the `.gui` files give it. A `.ttf` or `.otf` of the font's name
  beside the descriptor is used first, which is how a mod supplies one and stays valid
  in the original; otherwise an open typeface from `fonts/` that stands for the one
  named, narrowed to fit where its letters are wider. **Do not bring the glyph textures
  back**, and do not size text from a descriptor's `size` figure - two copies of one
  font disagree on it. `docs/mechanics/interface.md`, section 6.
- **Typefaces are shipped, and nothing is taken from the machine.** The maintainer's
  word, 2026-10-08: "I dont want a software that depends on the local pc installs." So
  no `SystemFont`, no lookup of an installed font by name, and `allow_system_fallback`
  off on every typeface, in its `.import` file and in code - a character a typeface
  lacks stays missing. Everything drawn comes from `fonts/` or from a mod's own
  `gfx/fonts`. Those files are open typefaces under the SIL Open Font License, which is
  why they may be here when the original's may not: **a font goes in `fonts/` only with
  its licence text beside it and a row in `fonts/readme.md` saying where it came from**,
  and never one extracted from an install or copied from a system folder.
- **Bitmaps do not gain detail, so a texture may be supplied at any resolution.** Icons
  soften when enlarged; the answer is that a mod can ship a sharper
  file, and the runtime works in ratios of the texture rather than in its pixel counts. Two
  things follow from how the files are written:
  - *An element's size on screen must never come from a replacement's pixel count.* Icons,
    buttons and checkboxes declare no size in the `.gui` file - about 1,500 entries in Their
    Finest Hour - and take it from the texture. So something has to say what size a texture
    was designed at. **How is not decided** (2026-10-07). The leading idea is that the
    original file stays as the size reference and a sharper one overrides its pixels from a
    higher layer, which needs no new syntax and leaves the mod valid in the original.
    **The runtime as built does the thing this warns against**: an icon or a button is the
    pixel size of whatever texture loaded. Right for the original's art, wrong the day a
    sharper file is dropped in, and it is in the readme's *Loose ends* until this is decided.
  - *Sprite sheets need no naming scheme.* A sheet declares its frame count in the `.gfx`
    entry (`noOfFrames`, on 226 of about 1,300 `spriteType` entries), so a frame is the
    texture's width divided by that count at any resolution. The pixel-valued sprite keys -
    `size` and `borderSize` on the cornered-tile, tile and progress-bar kinds - scale by the
    same ratio as the texture they describe.
- **There is prior art in history.** `src/Gui/` and `src/FrontEnd/` ran the original's main menu
  from its `.gui` files and were removed in commit `4269708`. Today's `src/Gui/` started from
  that one; the lobby that `src/FrontEnd/` drove is not coming back.

## What this repository may not do

    the fact base    depends on nothing
    OpenHOI3         depends on the fact base - PROSE ONLY
    OpenHOI3         never depends on BiceLib

- **No build-specific offsets, addresses or vftable indices.** They describe one build of one
  executable we are not reimplementing, so they are meaningless here. What this project needs
  from the reverse engineering is **mechanics** - formulas, orderings, rounding, caps,
  tie-breaks - which are build-independent and live in the prose.
- **Never read anything out of BiceLib.** That is the mod's injected DLL. Its headers are full
  of exactly the offsets above, and "the DLL already worked this out" is the tempting shortcut
  that imports them.

## Where knowledge about the original comes from

`GitHub/hoi3-reversing`, normally checked out beside this repository: 157 `findings/FINDINGS-*.md`
write-ups, `CLASSES.md` for the class-by-class account, and `TRAPS.md`. Cite a findings file
with the `reversing/` prefix on the front - the fact base's logical name, which resolves
wherever it is checked out. Its `checkrefs.py` verifies those references from the other
side, including the ones written here.

**When a spec needs something nobody has read, the reading belongs to the fact base, and
all of it.** Read it there, by that repository's rules; write the findings file; **and
enter what was read in its `project.json`** - the maintainer's standing instruction, and
the fact base's `CLAUDE.md` has the steps. This project then cites the findings file and
takes nothing but the prose. The record is not optional because the rewrite was the one
asking: on 2026-10-07 four functions were read for `docs/mechanics/production.md` and left
as prose, and had to be entered afterwards.

**Much of this project does not need the executable at all.** The loaders, the map and the
data model are driven by the original's `.txt` files, which are plain text and largely
self-describing. The executable is needed where its behaviour is genuinely *not* inferable
from its data: the simulation formulas, the AI's scoring, the orderings and caps that nothing
documents, which script keys exist and what each one does, and which interface elements a
screen looks up by name.

Two things the reverse engineering established that are worth knowing regardless:

- **The class structure is recoverable in full.** `hoi3_tfh.exe` is a 32-bit MSVC build
  shipping complete RTTI, so class names, inheritance, base offsets and vftable layout can be
  read exactly rather than guessed. `reversing/ghidra/hoi3_tfh-class-hierarchy.txt` holds a
  snapshot - 1212 application classes under 58 roots, names only - and is **generated, not
  edited**. It lived in this repository's `docs/` until 2026-10-07. The JSON beside it is
  vftable addresses and is not for this project.
- **The data formats carry a lot of accidental history.** Interface files name textures with the
  wrong extension; font descriptors declare a texture page and then omit the line naming it; a
  scrollbar that runs up and down is read from the bottom, so its definition looks as though
  its arrow sprites are swapped; a map editor is present but hidden behind
  two `Hide()` calls because it was never finished. Expect the loaders to absorb this.

**There is no Ghidra tooling here any more.** `tools/ghidra/` was removed on 2026-10-07: there
is one Ghidra toolchain and it lives in the fact base - a 5007-address, 494-struct record with
an idempotent apply. The two scripts the fact base was still relying on, and the class export
they generate, moved there; the other two were deleted and are in this repository's history.
Do not start a second toolchain here.

## Building and running

| Tool | Version | Notes |
| --- | --- | --- |
| Godot | **4.7.1 .NET/mono** | the plain build cannot run C# |
| .NET SDK | **9.x** | Godot 4.7.1 refuses to start its build tooling without an SDK matching its host runtime |

The project targets `net8.0`, which is what Godot 4.7.1's generator emits; an SDK 9 builds that
fine, because the framework version and the SDK version are independent.

    dotnet build OpenHOI3.sln
    dotnet test tests/OpenHOI3.Tests

From VS Code, `F5` offers *Play OpenHOI3*, *Play OpenHOI3 (choose
country)*, which asks for a tag and whether to have every unit type, *Play OpenHOI3 (ask
for arguments)*, which asks for the developer switches themselves in one box - the game
cuts an argument wherever a space is followed by `--`, so the box may hold several -
*Play OpenHOI3 (headless)* and *Open Godot Editor*. The switches are listed above
`RunDeveloperArguments` in `src/CSession.cs`.
**Speed is judged in *Play OpenHOI3 (fast)***, which builds with `-p:OpenHOI3Fast=true` -
optimised code in the Debug configuration's folder, the only one the editor's binary loads
(`Directory.Build.targets`) - and runs without the debugger: on 2026-10-13 it ran full speed
at 27 frames a second where the plain Debug build ran 16, and at 43 once the trading ran side by side. The others are what a breakpoint
needs; a figure of speed from them says little.
The Godot executable path comes from the `godotTools.editorPath.godot4` setting rather than
being hardcoded, so set it in `.vscode/settings.json` (git-ignored) or in user settings.

**The install is found, not configured.** `CDirectorySettings.Discover` walks up from the
project directory it is given looking for a `Hearts of Iron 3` folder, and accepts one only if the content
is beside the executable - a folder holding a stray copy of the exe is not an install. Four
levels of walking is what reaches an install kept in the user's home directory rather than
beside the repository.

## Always

- **Never commit unless asked.**
- **Every class in its own file, named after it.** The maintainer's instruction,
  2026-10-08, after finding several to a file. It covers every top-level type - a class, a
  record, a struct, an enum, an interface - in `lib/`, `src/`, `tools/` and `tests/` alike,
  so a small enum or a one-line record that only one class uses still gets its own file. A
  type nested inside another stays where it is. A new file under `src/` needs the `.uid`
  file Godot writes beside it: run the editor's import once and add it.
- **And every file in the folder for its subject, which is its namespace.** The
  maintainer's word, 2026-10-08, when `lib/OpenHOI3.Sim` had reached 56 files side by
  side: "A flat folder with all is pretty chaotic." So `lib/OpenHOI3.Sim/Units/CUnit.cs`
  is `OpenHOI3.Sim.Units.CUnit`, as `Content/Gui` and `Content/Clausewitz` already were.
  `readme.md`, *Repository layout*, lists the folders and what each is for. A new type
  goes in the folder it belongs to, not at a project's root: the roots hold only what
  everything else is built on - in `Sim`, the game state, the tick, the clock, the date,
  the defines and the two kinds of number. When a subject has no folder, make one
  rather than leave the file at the root, and add it to the readme's list. The tests
  are in `Sim/`, `Content/` and `Interface/` by the library they test.
- **After creating `.cs` files from outside the editor, `touch` the project file that owns
  them.** VS Code's C# language server did not notice 34 files a script created on
  2026-10-08 and reported `CS0246` for every type that had moved, while `dotnet build` was
  clean. Changing a `.csproj`'s timestamp makes it reload the project and find them; so
  does *.NET: Restart Language Server*. Seen once, so whether it misses every file made
  outside the editor or only a burst of them is not known.
- **A deliberate departure from the original goes in `docs/compatibility.md`, in the
  session it is decided.** The maintainer's word, 2026-10-08: "maybe we should start
  tracking our decisions on what was deliberately changed in terms of compatibility."
  It is for decisions - a file that is valid in the original and that here means
  something else, or nothing, because somebody chose that - with who chose, when, what
  a mod loses and what would undo it. It is not for what is unbuilt (*Loose ends*) or
  for what nobody has read (a spec's **Ours**). When the maintainer settles a question
  like "keep ours or read the file's", the entry is written before the session ends,
  and a reversed decision is struck through, not deleted.
- **Keep `readme.md`'s *Next up* and *Loose ends* current.** They are what a session with no
  memory of the last one starts from. When a piece of the plan lands, move it out of *Next
  up* and into *Where things stand*; when something is deferred, it goes under *Loose ends*.
- **Check against the savegame.** `tools/OpenHOI3.SaveCheck` loads an install and reports,
  field by field, where the result differs from a savegame the original wrote on the start
  date. Run it after touching a loader or a formula; a rule that reproduces the save to the
  thousandth is worth more than one that reads well. The maintainer's save is normally at
  `Documents\Paradox Interactive\Hearts of Iron III\save games\Germany1936_01_01_00.hoi3`.
  That run also sets every country's leaders in service and every unit's leader beside the
  save's - 101 of 101 and 1555 of 1555 - and should after any change to `Sim/Leaders`, the
  order of battle's reading or what a unit's figures are made of, which the choice weighs;
  and the organisation of every brigade under an army with a commander, 764 of 764, which
  his reach decides. It ends by saying how many land units start out of radio range - 95,
  none of them Germany's - which is a look, since a save keeps nothing of it.
  **Anything that moves from day to day is checked against two saves of one game**:
  `OpenHOI3.SaveCheck adjacency <install>` checks the edges between provinces - which
  there are, their order, kinds and distances - against the cache the original keeps of
  them in `map/cache/adjacencies.bin`; all 83,596 agree and should after any change to the
  map loader. `OpenHOI3.SaveCheck research <install> <earlier save> <later save>` does it for research,
  the monthly decay and the build queue, and the same folder has four later saves of that game - the three
  autosaves and `Germany1936_04_20_03.hoi3`. An autosave is overwritten as the game goes
  on, so a comparison that matters should say which dates it was run against.
  `OpenHOI3.SaveCheck march <install> <earlier save> <later save>` does it for what an
  hour adds to a marching division, and wants saves **hours** apart, since the weather
  turns: `Germany1936_01_07_22.hoi3` and `Germany1936_01_08_02.hoi3` give 301 of 301, with
  `_01_07_14` and `_01_04_12` of the same game before them. The same command moves every
  fleet and air unit of the earlier save forward and sets it against the later:
  `_01_08_03`, `_01_08_06` and `_01_08_07` have a submarine flotilla and an air fleet of
  the maintainer's under way, and give 17 of 17 and 1 of 1.
  `OpenHOI3.SaveCheck navaldist <install>` checks the distances by sea against the
  original's `map/cache/navaldist.bin`: all 17,621,016 agree, and it takes twenty seconds.
  **A battle has no save to be checked against**: none of the maintainer's has one, and the
  original's rolls cannot be made again (`docs/mechanics/landcombat.md`, section 8).
  `OpenHOI3.SaveCheck battle <install> [attacker] [defender] [seed]` is a trial instead: it
  fights one with the install's own units and says how it went - Italy against Ethiopia with
  no tags given, who are at war on the start date. Run it after touching `lib/OpenHOI3.Sim/Combat`.
  `OpenHOI3.SaveCheck seabattle <install> [attacker] [defender] [seed]` is the same at sea:
  the strongest fleet of each of two countries is put in one sea province, the first country
  played by a person, and the fight is told hour by hour - Germany against the United Kingdom
  with no tags given. Run it after touching the `CNaval` classes there.
  `OpenHOI3.SaveCheck raid <install> [raider] [victim] [days] [seed]` is the trial of a
  fleet's missions: two countries are put at war, the first one's fleet with the most
  `convoy_attack` is sent to raid the water the most of the other's convoys go through, and
  each sinking is told - Germany against the United Kingdom for thirty days with no tags
  given, which sinks 38 transports and 3 escorts - 37 and none until 2026-10-13, when a war began to
  mobilise both sides (`docs/mechanics/war.md`, section 4). Run it after touching
  `lib/OpenHOI3.Sim/Missions`, `CConvoyRaid` or what a convoy does about an attack.
  `OpenHOI3.SaveCheck firstdays <install> <start-date savegame>` sets a new game as a player
  gets it, its two days run, beside the save country by country. **It is meant to differ** -
  the original's AI has changed laws and shares by then - and is there to show by how much.
  The like-for-like is `supplypass <install> <start-date savegame> countries`, below: with
  `history=GER:training_laws+industrial_policy_laws history=DDR:industrial_policy_laws
  history=DFR:industrial_policy_laws` after it - the laws the AI changed only after the days
  were run - Germany agrees in everything but 0.030 of supplies, and the others'
  organisation is 2770 of 2770 where no commander is over them and 764 of 764 where one is.
  Run it after touching `CTick.RunFirstDays`, the
  daily pass's order, or anything that asks `IsBeingMade`.
  `OpenHOI3.SaveCheck airbattle <install> [attacker] [defender] [seed]` is the trial of the
  battle in the air: the two countries' nearest pair of air units with an air attack, the
  second given an intercept over its own base and the first air superiority over it, and a
  week told - Germany against Poland with no tags given, ten battles over Warszawa in
  two days and eight more over Tomaszów. Run it after touching the `CAir` classes of `Sim/Missions` or
  `Sim/Combat`, or `CIntelligence`.
  `OpenHOI3.SaveCheck bombing <install> [mission] [attacker] [defender] [days] [seed]` is
  the trial of bombing: the first country's air unit with the most of what the mission
  fires, sent at the province of the second's worth most to it, each sortie told and at the
  end what the province has lost - Germany's strategic bombing of Poland for three days
  with nothing said, which leaves a tenth of Poznan's factory. The missions are
  `strategicbombing`, `logisticalstrike`, `runwaycratering`, `installationstrike`,
  `groundattack`, `interdiction`, `portstrike` and `navalstrike`. Run it after touching the
  `CBomb` classes of `Sim/Missions` or `Sim/Combat`, `CAntiAircraft`, `CProvinceBombing` or
  `CProvinceRepair`.
  `OpenHOI3.SaveCheck script <install> [kind]` needs no save: it reads every event and
  decision and lists each problem in them, of one kind if a kind is given - `UnknownKey`,
  `UnknownName`, `OtherCase`, `WrongShape`, `Structure`, `Quirk` - and counts what it read by
  kind. **The base game should give no unknown key and eleven problems, all its own** - four in its events and seven in its on_actions, whose battle events Their Finest Hour empties
  (`docs/mechanics/script.md`, section 10). Run it after touching `Content/Script` or a list a
  key may name.
  `OpenHOI3.SaveCheck conditions <install> [tag]` is a look and not a check: it asks every
  event's trigger and every decision's `potential` and `allow` of every country on the start
  date, and lists what held and every condition that answered false unasked, with why
  (`docs/mechanics/triggers.md`, section 8). On 2026-10-12 the base game gave 16 events
  holding for somebody, 5 of them only through an unbuilt condition inside a `not`, and two
  decisions allowed. Run it after touching `Sim/Script` or anything a condition reads.
  `OpenHOI3.SaveCheck effects <install> [tag]` is a look too: it runs every event's and
  decision's effects once, one after another on one new game in the country's scope (Germany
  by default), and lists what did nothing for want of what is not built. On 2026-10-12 the base
  game ran 2,882 effects in 615 blocks in about a second, `country_event` the most often unbuilt
  (`relation` was, until diplomacy's first piece on the same day).
  **Nothing in the files should trip it**; run it after touching `Sim/Script` or anything an
  effect changes.
  `OpenHOI3.SaveCheck events <install> [tag] [days]` plays a new game with only that country
  played (Germany for 180 days by default) and lists every event that fired, when and for whom,
  what is left open for the player, and the decisions offered him. A look: no save keeps what
  fired. On 2026-10-12 the first 120 days fired 46 events of 10 kinds, the death of George V for
  Britain on 14 January among them. Run it after touching `Sim/Events` or the tick's order.
  `OpenHOI3.SaveCheck scripttext <install> [tag] [event id or decision name]` writes every event
  option's tooltip, every `immediate` block and every decision's two tooltips as the country sees
  them (Germany by default), counts them and lists the key-shaped keys no table has - or, given
  an event or a decision, writes that one out whole. A look: nothing keeps what the original
  wrote. On 2026-10-12 the base game gave 689 texts, 2203 lines, none failing, in 150 ms, and
  event 2602's option "Edward VIII will become Head of State." Run it after touching
  `Interface/Script` or a condition or effect it asks.
  `OpenHOI3.SaveCheck techgap <install> <start-date savegame> [examples] [tag]` is a look and
  not a check: the 476 technology levels the start save has above the history's. 453 of them
  are the highest level of that technology due before 1936; which technologies are chosen is
  not found (`docs/mechanics/resources.md`, section 5). **A new game does not give them**,
  and that waits for a reading and then for the maintainer's word.
  `OpenHOI3.SaveCheck diplomacy <install> <start-date savegame> [examples]` makes a new game
  with the install's Lua and sets what its countries hold of each other beside the save: every
  pair block's relation and threat, each country's neutrality and effective neutrality, its
  most-threatening country and where it stands on the alignment triangle. On 2026-10-12 it gave
  **7,878 of 7,878 pairs** for relation and threat, 101 of 101 for both neutralities and for the
  most-threatening country, and **50 of 101 for alignment** - all 11 of the factions'. On
  2026-10-13, with `SpyMission` built and the base game's whole `autoexec.lua` loading, it gave
  the same with no Lua error (`docs/mechanics/lua.md`). The alignment misses are the AI's opening laws and the cabinets the
  original fills as a game is entered: `laws=save` after the save gives every country the save's
  laws before the two days and makes it 67 (`docs/mechanics/alignment.md`, *Ours*), and
  `explain=<tag>` prints a country's drift term by term as its first step weighs it, and
  `screen=<tag>` (or `<tag>:<player>`) is a look at what the diplomacy screen says of a country as
  Germany sees it - the panel, the status lines, the relation list's first rows with their
  tooltips, the marker's drift tooltip and each faction's (`docs/mechanics/diplomacyscreen.md`).
  Run it after touching `Sim/Diplomacy`, `lib/OpenHOI3.Lua`, the history's diplomacy or anything
  an alignment term reads, and the look after touching the screen's text.
  `OpenHOI3.SaveCheck aiscore <install> [function] [tag]` is a look: it makes a new game with the
  install's Lua and asks the eleven `DiploScore_*` functions that take nothing but tags for every
  pair of countries owning land, the recipient's AI answering, and says what they answered, what
  failed and what they read that is not built; given a tag, that country's answers to each
  (`docs/mechanics/lua.md`, section 6). On 2026-10-13 it asked 6,006 pairs of each in about eleven
  seconds and **only `DiploScore_PeaceAction` failed, every time, at `CAIIntel`**. No save keeps what
  the original's AI answered. Run it after touching `lib/OpenHOI3.Lua/Api` or anything a method
  there reads.
  `OpenHOI3.SaveCheck trade <install> [days] [tag] [quiet|timed]` is a look too: it plays a new
  game with nobody playing and the install's Lua trading for every country, and tells the offers,
  the answers, the routes made and ended and every Lua error - `quiet` only the totals, `timed` the
  time each Lua method took. On 2026-10-13 ten days made 285 offers, 264 accepted and 194 routes
  standing, with no Lua error, under half a second a day (`docs/mechanics/trade.md`). **The trading runs
  side by side on eight Lua states of its own** (`CTradeWorkers`; `trade.md`, *Ours*), and comes out the same
  as one state running the countries in turn: compare this trial's output before and after touching
  anything the scripts read, since a script may only read the state and post - something it reads that is
  made on first use has to be made before they run (`CTradeAi`), and nothing it calls may change the
  state. `timed` runs the countries in turn on the game's own state, which is the one it times. Run it after
  touching `Sim/Trade`, `Sim/Agreements` or the Lua classes trade reaches.
  `OpenHOI3.SaveCheck declare <install> [actor] [recipient] [limited] [hours] [peace] [occupy]` is a look too: it says
  what a new game holds of war - the casus belli types, the history's wars with their goals, who is
  mobilised - then has the actor declare on the recipient with the first goal the declare-war dialog
  offers, whether or not its row may be pressed, and runs some hours with the AI answering the calls
  to arms, telling who is on which side (`docs/mechanics/war.md`). Germany on Poland for 48 hours with
  nothing given; `who` instead lists every pair whose row may be pressed - none on a new game.
  On 2026-10-13 Italy against France brought the Allies in whole, the Soviet Union against Manchukuo
  had Japan called and accept. With `peace` after the rest the actor then offers the recipient peace as the
  row would and the hours run to the answer: Turkey's to Iraq scores 66 and is accepted at the next hour.
  With `occupy` the recipient's units are taken away, the actor given all it owns to hold, and a day run:
  on 2026-10-13 Ethiopia became Italy's puppet, Austria was annexed by Germany and France went into exile in
  London. Run it after touching `Sim/Wars` or what a declaration reads.
  `OpenHOI3.SaveCheck faction <install> [actor] [recipient] [neutral]` is a look too: each faction of a new game with its
  members, rules, progress - the start save's 115, 263 and 173 of 1172 - and victory conditions met, how many countries
  each leader's join and invite rows are open to - none on 1 January 1936, every neutrality being too high - and then the
  actor sending the recipient the faction row that applies, `neutral` setting the joiner's neutrality to nought, with the
  install's Lua answering: on 2026-10-13 Hungary asked Germany and was taken in, Britain's invitation to Sweden scored 0
  (`docs/mechanics/factions.md`). Run it after touching `CFactions`, `CFactionOffers` or `CFactionProgress`.
  `OpenHOI3.SaveCheck profile <install> [days] [tag]` is a look at speed: a new game as the client makes it,
  the install's Lua loaded and one country played (Germany for 30 days by default), run hour by hour with
  `CGameState.Profile` set, so the tick times each of its steps; it tells an ordinary hour, an hour that turns a
  day, the slowest hours and every step by its time. Build it in Release for figures worth comparing. On
  2026-10-13 a day took 0.43 s, an ordinary hour 15 ms - 11 of them the AI's trading in the install's Lua, run
  side by side - and an hour that turns a day 90 ms. Run it after anything that may have made the hour slower.
  `OpenHOI3.SaveCheck supply <install> <savegame>` checks the supply network against what a
  save says of it, with the save's own owners, controllers and capitals, so a save of any
  day will do: the provinces marked `out_of_supply_days` are ours that draw on no depot -
  the same 4,400 in the saves of 1 and 8 January and 20 April 1936 - and the provinces
  marked `capital` are ours that are a capital or beside one, 402 and 400. It also says
  where the supplies lie in each network, which is all a save has on which province a
  depot is (`docs/mechanics/supply.md`, section 9). With `release=PRK:JAP` after the save it
  is a trial instead: the country is given its cores out of its master's land as a puppet
  and the check says who then draws on what - the three states the maintainer watched in
  the original are `release=PRK:JAP`, the same with `release=KOR:JAP` after it, and neither.
  Run it after touching
  `lib/OpenHOI3.Sim/Supply` - and the start-date check too, since home production, the
  `overseas` modifier and where a unit may be placed all ask the same areas.
  `OpenHOI3.SaveCheck supplypass <install> <start-date savegame>` checks the carrying of
  supplies: it makes the units as the save has them, with the save's laws, ministers,
  technology and mud, runs what the original runs before a game's first hour - a day, the
  filling of the provinces, a day - and sets every province's supplies and fuel beside the
  save's: what lies there, what was asked of it and what passed through it, today and
  yesterday. 184 of 234 networks agree in every province and every figure; the rest differ
  where the original's AI has a unit on the move or has moved a share, and in Moscow's, whose
  garrison is the save's since factions' modifiers apply, and whose network differs for a reason not found (`docs/mechanics/supply.md`, section 17). **The same run sets the convoys it made beside
  the save's** and should say 261 of 261 on every line and 20 of 20 countries, **and the
  money and resources lying in every province**: 88 of 88 harbours to the thousandth, and
  49 of 49 capitals that somebody else holds within a fiftieth
  (`docs/mechanics/convoys.md`, section 12). `network=<depot's number>` after the save prints
  one network province by province, `country=<tag>` one country's day and `convoys=<tag>`
  one country's convoys that are not the save's. Run it after touching the pass, the draw,
  where supplies are made, the filling or anything under `lib/OpenHOI3.Sim/Convoys` - and
  after touching the order of an area's provinces or harbours, which the convoys hang on.
  Never copy a savegame, or anything from one, into the repository; a formula measured from
  it goes in the spec marked *measured*, with the figure it reproduced.
- Tests are where a silent misreading gets caught. The parser, the layering, the map, the numbers, the tick, modifiers, IC, resources, the stockpile, production, the conversions, leadership, research, manpower, unit types, historical models, orders of battle, what units draw each day, the officer ratio and organisation, the build queue, what a country may build and what a new unit is called, the interface files, font descriptors, the sliders, the three screens' figures and the builder windows', the unit counters and who a finished unit may join, the edges between provinces and a unit's march, the unit panel's figures and the hierarchy's lines, ports and the distances by sea, a fleet's and an air unit's orders, the buildings on the map and the window of orders, a land battle, what the hour makes of a unit in one, day and night, the game's generator of chance, the tactics, what a battle shows, which arrow a unit has, the supply network, the carrying of supplies, what a unit draws and brings, convoys with their heap and their harbours, the supply map mode's colours, who is whose puppet, a battle at sea - the sighting, where the ships stand, who a ship shoots at, sinking, breaking away and falling back - a fleet's missions with the raid on a convoy, the two days a new game runs first, an air unit's two missions with what a country knows and the battle in the air, and the bombing missions with the bombing, a building's two levels and the day's repair, what the window shows of a battle in the air and of a bombing, a mission's area with the air window's ladder, sliders, hours, dates and colours, the list of finished battles with the air map mode's colours, icons, rings and lines, and leaders - their files, their skill, who may command what, the choice of one, how far a command reaches, a leader in a battle on land, at sea and in the air, and the experience and traits a battle gives, the window a leader is chosen in and the original runtime's sort, and the tooltip - what the box shows of one, where it goes, when its second half comes and how a line is broken - with what a province's, the top bar's, the production and technology screens', the sliders', the builder windows', the unit panel's, the deploy menu's, the orders windows', a battle's and the leader window's say, a brigade's summary, a leader's tooltip and what a trait does, a technology's requirements with the corrected indentation and what its levels do, the flag and the counter in text and a modifier's sources written out, and a scrollbar's end buttons - the step, Shift, Ctrl and holding - and event and decision script - its words, the 153 trigger and 91 effect keywords and the names a key may be in the original's order, the tag's three letters and its bucket, `limit` in a scope, an event, a decision, a mean time to happen and the problems found - and what a condition answers - the blocks, the scope's forms and the sets, the leaves as the original answers them and what is reported unbuilt - and what an effect does - its blocks and draws, flags, variables, the modifiers an event gives, a country's and a province's effects - and an event firing and a decision taken - the candidates, the chance of a day, the firing, the AI's option, an answer, the four months, fire-once, the decisions offered and taken, an event's text and where its options stand - and a condition and an effect as words - each block's header and asterisk, a scope on one line, the doubled indent of effects, a run of cores on one line, a random list's chances, a missing key shown as itself, and a wrapped line keeping its indent - and the on_actions: the first event whose trigger holds, a fired fire-once event in front of the rest, a battle's winners and then its losers each counting it, a sea battle with an empty side, and a pride sunk - and messages: a type's keys and the null type, which message a player is told, the header's government and the lines' dark white, and the log's 200 messages, its rows cut one at a time and its rebuild of 101 - and a block inside a `not` with no asterisk of its own - and what countries hold of each other - a record towards every country, the history's one side, a new game's fill only where nought, the month's drift by neighbours, war, guarantees and puppets, a battle's +5, neutrality's day and its effect and conditions, threat spread to everyone and over the map with its floor, its resistance and its fall a step, the most-threatening country, the threat effect, and the triangle's clamp with its sign slip, a member put on its leader, a step every interval and a day and the drift worked out - and Lua: the interpreter's rounding, numbers and library, errors both ways, a new value at each push with one equality for every class, a class's global, its fields and iterators, the install's scripts through its layers and a relation that fails giving 25, `CFixedPoint` as a float and its backwards arithmetic with a number, the single-precision distance and resource figures, the API's friends, enemies, factions and tags, what is unbuilt reported once, and an offer put to the recipient's AI with a failure a refusal - and the diplomacy screen: its lists and the bytes they sort by, its panel, the ceiling for war, the status lines in turn, the relation list, the marker's place, a breakdown at its constants, a faction's rules and the drift tooltip with its top line - and the agreements: an offer charged and its diplomat on his way, the AI's answer over 30 and under a draw, each pact's arms with the keys each side is told, the endings by themselves, the upkeep, the screen's rows in order, greyed and said, and the dialog's lines and chance, and the pacts an effect makes and the conditions that see them - and trade: a route's price to the save's truncation, its two blocks set from one slider, barter and the free gift rule, who buys and owns the convoy, whether one is needed, the day's exchange, inactive for a convoy or for goods and its sticky days, the ends by cancel, war and embargo, one acceptance an hour, the Lua's trade action, goods values and routes, and the window's amounts, offer text and accept, a list line's icons in their columns, its tooltips and the cancel dialog's backwards text, the runtime's `rand()` from 47 and automate-trade's one acceptance an hour, an offer put to the player once with its icon's tooltip, blink and week and its dialog's text, and the trade messages' goods from the side told - and aid: debt allowed and revoked at the debtor's cost and repaid an 18,250th a day for ever, a war clearing it, a shared theory read at the higher level and a new sharer ending the old, the day's check and the leadership drain, lend-lease's refusals and ways, its offers and the hour's look, the giver's day and the receiver's whole IC as the reading's worked day has them, a licence's fee and its decline for want of money or its debt, and the licence action in Lua, and a province counting its own changes of hands, and war: a declaration's sides, puppets, guarantors and factions, limited war, a name's order, the row's gates and truce, a call to arms both ways and the player's unasked, a goal's monthly cooldown and spent casus belli, the null casus belli and the condition by the types, the rows and their tooltips, a goal's setup text and the call's dialog, and peace: what changes hands and to whom, the lone side leaving, a war's goals deleted with a leaver, the players told, the day's return, a capital's quarter looted, the surrender progress over owned cores, the AI's intel with a draw a unit, and the row and the dialog's account of who cedes what, and the capitulation: annexation's land, wars and forgetting, a puppet joining its faction leader's wars at the back, a surrender at the brink with its victor's annexing goal, an exile in its leader's capital with a tenth left, and a fallen capital looted and a government going back, and a battle's capture - to a puppet's master, to a fighting friend beside it, to an exile's head only against an enemy owner - with its loot and a defender's fall-back damage, and factions: a join's war sweep, the join and invite rows with their greying and texts, an accepted join bringing the joiner's puppets and ending its alliances with the members, the Allies' guarantee and call, a faction's modifier and an aligned outsider's resources, leaving by script, progress by holder and the share's cut, and the choice of primaries and the day's test, and the hour's trading run side by side in the countries' order with a random of each state's own - carry 1856 of them
  because a wrong parse produces plausible-looking data rather than an error, and the original's
  own engine swallows its parse errors - so "it loaded" means nothing.
- Scripts go in the scratchpad, written with the `Write` tool, not a bash heredoc: heredocs here
  mangle apostrophes and turn `\Users` into a `\U` escape. **That goes for a script fed to
  an interpreter inline as well, and for editing a source file through one**: on 2026-10-09
  a Python edit run from a heredoc turned every `\n` inside the C# strings it was writing
  into a real line break, and the test file stopped compiling. Edit a file with the `Edit`
  tool.
