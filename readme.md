# OpenHOI3 - a grand strategy game in the spirit of Hearts of Iron 3

## Goal

The goal is a game that plays like Hearts of Iron 3: Their Finest Hour - the same feel,
the same kind of decisions, the same scale - built on its own technology, and able to
run the original's content and its mods as they are written.

This is deliberately **not** a reimplementation of the original executable. The engine,
the simulation and the rendering are our own, and nothing here is derived from the
original's binary. What is kept is the **language its content is written in**: the
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
assets or data files are redistributed here.

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

Two limits are worth stating. Scaling cannot add detail to a bitmap, so icons and the
original's bitmap fonts soften when enlarged. The remedy is that the runtime works in
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
| 2. The clock and the economy | The hourly tick with its daily and monthly passes in the original's order; every state change as a command; IC, resources, manpower, production, research. The interface runtime arrives here, with the top bar as its first screen | A 1936-1948 run completes headless with totals that are plausible against a real savegame. **The clock, the command queue, fixed-point numbers, buildings, laws, ministers, technology levels, each country's IC, its home production, what its stockpile starts with, its six shares of IC and what consumer goods and supplies do with theirs, its leadership with what its four shares make, and research, are in (2026-10-08) and checked against savegames; manpower and everything that needs units are not** |
| 3. Units and war | Orders of battle, units on the map, movement, land combat, supply; then air and naval | Two countries can fight a land war to a result |
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

As of 2026-10-08, inside stage 2. Kept current as work lands, so that picking this up cold
needs nothing but this file and `docs/mechanics/`.

1. **The interface runtime, with the top bar as its first screen.** There are real numbers
   for it to show now: IC, the stockpile, manpower, leadership and what its shares make.
2. **Units**, which is stage 3, and which the rest of the economy is waiting on: the build
   queue, reinforcement and upgrades spend IC and manpower on them, and they are what
   draws supplies and fuel. `docs/mechanics/production.md`, section 5.

Worth doing before either, in the fact base: **read the rest of the routine that rebuilds a
country's modifier.** Every figure checked so far that does not fit the savegame fails for
something that routine decides - triggered modifiers, event modifiers, when a strategic
resource counts. See *Loose ends*.

Done so far inside this stage: the stockpile (`docs/mechanics/stockpile.md`); the six
shares of IC with the two that spend without units, consumer goods and supplies
(`docs/mechanics/production.md`); the two conversions, energy into crude oil and crude
oil into fuel (`docs/mechanics/ic.md`, section 4); leadership with its four shares, of
which officers, diplomatic influence and spies are made each day
(`docs/mechanics/leadership.md`); research - the queue, what a level costs, what a day
adds, what finishing one does, and the monthly decay of what a country knows
(`docs/mechanics/research.md`); and manpower - what a day adds and what a new game starts
a country with (`docs/mechanics/manpower.md`).

There are five savegames of one game to check against now, from 1 January to 20 April
1936. The first is the start; the later four are a month or less apart, which is what
checks anything that moves from day to day.

**When a spec needs something read out of the executable**, the reading is done in the
fact base and entered in its record, not only written up: this repository's `CLAUDE.md`,
*Where knowledge about the original comes from*.

### Loose ends

None of these blocks the list above.

- **`positions.txt` is not read**, so a province's position is the centre of its bounding
  box. It matters once units are drawn. `docs/mechanics/map.md`, section 4.
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
- **Nothing spends manpower**, so the pool only grows. Building units and bringing them
  back to strength are what take it, and wait for units.
- **A country starts with the manpower its history's laws give**, where the original's AI
  has already chosen its own: Germany begins with 469 here and 882 there. The same goes
  for the decision the United States' AI takes at once, which costs it three quarters of
  its manpower. `docs/mechanics/manpower.md`, sections 4 and 6.
- **The United Kingdom gains a tenth more manpower a day than anything found gives it**,
  and its leadership is off in the same way.
- **Nothing queues research but a command.** There is no AI and no screen, so no country
  researches anything in a run nobody drives.
- **A technology's `allow` block is read for the six kinds of condition the base game
  uses**, and none of the six has been read out of the original. Whether a port is a
  naval base, and whether it is owning or controlling a province that counts, are guesses.
  A mod that uses any other condition gets a warning and the condition left out.
  `docs/mechanics/research.md`, section 5.
- **What a country's modifier is made of has only partly been read.** The original's
  routine starts from `base_values` and the faction and takes in the modifier of every
  province the country controls; where laws, ministers, strategic resources and triggered
  and event modifiers join it is the next thing to read there. It would settle whether a
  strategic resource counts once or once a province, and why one is missing when a new
  game is set up. `docs/mechanics/modifiers.md`, section 4.
- **Leadership makes officers, influence and spies that nothing spends.** They accumulate
  until units, diplomacy and espionage exist. Officers start at nothing where the original
  gives a country what its units need.
- **The original's AI script changes laws and ministers before the first hour**, for every
  country including the player's, so what a player sees on the first screen is not what
  the history files say. This project has no AI, so its countries start with the
  history's laws - Germany on basic mobilisation where the original shows a war economy.
  `docs/mechanics/resources.md`, section 5. It goes on changing them after the last of
  those days is run, so a savegame's laws are not always the laws its figures were made
  under; `docs/mechanics/production.md`, section 7.
- **The original has run two days before the first screen**, and this project starts from
  day nought. A stockpile here on 1 January is what the original had before those two
  days. `docs/mechanics/stockpile.md`, section 3.
- **Puppets are not read**, and one consequence shows already: Manchukuo starts with 1890
  metal where the original holds a subject to 1000. Wars are read for who is in them and
  when they began.
- **Supplies and fuel start at nothing.** The original gives each country as many days of
  what its units use as it gives of resources, so this waits for units.
  `docs/mechanics/stockpile.md`, section 2.
- **Four of the six shares of IC spend nothing**: lend-lease, production, reinforcement
  and upgrades. IC put in them is wasted until there are units, and wasted IC costs no
  resources, so stockpiles still grow faster than they should.
- **Consumer goods demand is missing its standing-army term**, which needs units and
  neutrality. So no country here is unhappy about the size of its army.
  `docs/mechanics/production.md`, section 3.
- **Nothing sets the shares but a new game and a command.** There is no AI to move them
  and no screen to move them with, so every country keeps the shares it started with.
- **A misspelt modifier is not reported.** The modifier kinds are not a closed list here,
  so any key a block does not own is taken as one. `docs/mechanics/modifiers.md`, section 5.
- **A country's modifier is `base_values`, its laws, its ministers and its strategic
  resources.** The other static modifiers - difficulty, war and peace - and ideology do
  not feed it yet, and for most of those how the original applies them is not established.
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
  The rest have not been filed in its work queue.

Being in the spirit of the original means choosing what not to take. These are settled
decisions rather than things merely unbuilt, so nothing should be designed around them
coming back later.

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

Early. What exists is the reading of an install, plus the beginnings of a data model,
in three projects:

| Area | What it does |
| --- | --- |
| `lib/OpenHOI3.Sim` | The game state - dates, countries, provinces, terrain - and what drives it: fixed-point numbers, the command queue, and the hourly tick with its daily, monthly and yearly passes, and the first systems in them: each province's modifier and each country's, from its laws, ministers and strategic resources; its IC, what it produces at home, the stockpile that goes into, the six shares its IC is divided between, with the money, supplies, dissent and resource bill that come of them, the conversions of energy into crude oil and crude oil into fuel, its manpower, its leadership with the officers, influence and spies its shares make, and its research with the monthly decay of its theories and practicals. References nothing, so the simulation runs headless |
| `lib/OpenHOI3.Content` | Reads the Clausewitz script format, finds and layers a Hearts of Iron 3 install, and reads its localisation tables, its map with its continents, its defines, its buildings, laws, ministers, technologies and strategic resources, and its province, country and war history. No engine in it either |
| `src/Map` | The map, political or by terrain: drawn by a shader from a province-id texture and a colour per province, with pan, zoom and picking |
| `src/Gfx` | The Godot client's side: decodes the original's DDS/TGA textures, bitmap fonts and sprite declarations |
| `tools/OpenHOI3.SaveCheck` | Compares what the libraries compute with a savegame the original wrote, field by field; and, given two saves of one game, what a day of research adds with how far each technology moved between them. The oracle for everything above |
| `tests/` | 357 tests over the parsing, the layering, the map, the numbers, the tick, modifiers, IC, resources, the stockpile, production, the conversions, leadership, research and manpower, where silent misreadings are expensive |

Not built yet: the cached asset build, the interface runtime, and nearly every system the
tick is there to run. The game also does not stop at the end date.

In the game: the mouse wheel zooms, a middle or right drag pans, the arrow keys scroll, a
click selects a province and Tab switches between the political and terrain maps. Space or
Pause stops and starts time, and plus and minus change its speed. A game starts paused.

An earlier iteration reproduced the original's front end directly from its `.gui` files
and ran its main menu and lobby. That work left the tree in commit `4269708` and remains
in git history. It is where the lessons below come from, and its `src/Gui` is the place
to start from when the interface runtime is built - though the lobby it drove is not
coming back.

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

From VS Code, press `F5`. Three launch configurations are available: *Play OpenHOI3*,
*Play OpenHOI3 (headless)*, and *Open Godot Editor*. The Godot executable path is read
from the `godotTools.editorPath.godot4` setting rather than hardcoded, so set it in
`.vscode/settings.json` (git-ignored) or your VS Code user settings.

To run with content, place a Hearts of Iron 3 install in a `Hearts of Iron 3` folder
in the parent of this repository.

## Repository layout

```
scenes/Main.tscn        main scene (run/main_scene)
src/                    the Godot client
src/Gfx/                decoding the original's textures and fonts
lib/OpenHOI3.Sim/       our own data model and, in time, the simulation
lib/OpenHOI3.Content/   the Clausewitz reader, locating and layering an install, localisation
tools/OpenHOI3.SaveCheck/   checks the libraries against a savegame the original wrote
docs/mechanics/         one spec per system: what the original computes, and how sure that is
tests/                  parser, layering and data tests
```

Nothing under `lib/` references Godot, and `OpenHOI3.Sim` references nothing at all. The
tests run against those two as a plain .NET project, with no engine behind them.

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
omit the line naming it, one scrollbar definition has its arrow sprites swapped, and a
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
caps - and never an address or an offset, which describe one build of an executable
this is not a reimplementation of.
