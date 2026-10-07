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
| 2. The clock and the economy | The hourly tick with its daily and monthly passes in the original's order; every state change as a command; IC, resources, manpower, production, research. The interface runtime arrives here, with the top bar as its first screen | A 1936-1948 run completes headless with totals that are plausible against a real savegame |
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

## Not carried over

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
| `lib/OpenHOI3.Sim` | Dates, countries, provinces, terrain and the game state - the beginnings of our own model. References nothing, so the simulation can run headless |
| `lib/OpenHOI3.Content` | Reads the Clausewitz script format, finds and layers a Hearts of Iron 3 install, and reads its localisation tables, its map and its province history. No engine in it either |
| `src/Map` | The map, political or by terrain: drawn by a shader from a province-id texture and a colour per province, with pan, zoom and picking |
| `src/Gfx` | The Godot client's side: decodes the original's DDS/TGA textures, bitmap fonts and sprite declarations |
| `tests/` | 95 tests over the parsing, the layering and the map, where silent misreadings are expensive |

Not built yet: the cached asset build, the interface runtime, and the simulation.

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
