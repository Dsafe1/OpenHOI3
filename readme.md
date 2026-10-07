# OpenHOI3 - a grand strategy game in the spirit of Hearts of Iron 3

## Goal

The goal is a game that plays like Hearts of Iron 3: Their Finest Hour - the same feel,
the same kind of decisions, the same scale - built on its own technology and its own
asset formats.

This is deliberately **not** a 1:1 reimplementation of the original executable. Matching
the original file-for-file means inheriting its data formats, its interface layout and
its limits forever, and every design decision ends up being made by a 2012 engine.
Fidelity is the wrong master. The game should feel like Hearts of Iron 3; it does not
have to *be* Hearts of Iron 3.

A converter that reads a Hearts of Iron 3 install and produces this project's own
assets is part of the project, so that anyone owning the original can play with its
content. That converter serves the game, not the other way round - where the original's
format is awkward, the converter absorbs the awkwardness and our format stays clean.

Players who want to use the original's content must own a legitimate copy. No game
assets or data files are redistributed here.

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
path into the simulation, and one thing for the converter to produce. Every extra entry
point is a permanent tax on all three.

It also means the converter has no reason to read `common/bookmarks.txt` or
`scenarios/`, and no lobby is needed to choose between them.

## Where things stand

Early. What exists is the reading half of the converter, plus the beginnings of a data
model:

| Area | What it does |
| --- | --- |
| `src/Clausewitz` | Reads the Clausewitz script format the original's data files are written in |
| `src/Core` | Finds and layers a Hearts of Iron 3 install, and reads its localisation tables |
| `src/Gfx` | Decodes the original's DDS/TGA textures, bitmap fonts and sprite declarations |
| `src/Game` | Dates and countries - the beginnings of our own model |
| `tests/` | 66 tests over the parsing and layering, where silent misreadings are expensive |

Not built yet: our own asset formats, the converter's writing half, the interface, the
map, and the simulation.

An earlier iteration reproduced the original's front end directly from its `.gui` files
and ran its main menu and lobby. That work is gone from the tree but remains in git
history, and it is where the lessons below come from.

## Requirements

| Tool | Version | Notes |
| --- | --- | --- |
| Godot | **4.7.1 .NET/mono** build | The plain build cannot run C# |
| .NET SDK | **9.x** | Godot 4.7.1 refuses to start its build tooling without an SDK matching its host runtime |
| Ghidra | 12.1.2 | Optional; only for the reverse engineering tooling |

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

For the converter, place a Hearts of Iron 3 install in a `Hearts of Iron 3` folder in
the parent of this repository.

## Repository layout

```
scenes/Main.tscn     main scene (run/main_scene)
src/Clausewitz/      the Clausewitz script format reader
src/Core/            locating and layering an install, localisation
src/Gfx/             decoding the original's textures and fonts
src/Game/            our own data model
tests/               parser, layering and data tests
tools/ghidra/        Ghidra scripts for studying the original binary
docs/                recovered class hierarchy - generated, do not edit
```

## What the original taught us

The original is worth studying as a design reference even though we are not copying it.
Two things came out of the reverse engineering that are worth keeping in mind.

**Its class structure is recoverable in full.** `hoi3_tfh.exe` is a 32-bit MSVC build
shipping complete RTTI, so class names, inheritance, base offsets and vftable layout can
be read exactly rather than guessed. `docs/hoi3_tfh-class-hierarchy.txt` holds the
result: 1212 application classes under 58 roots. That is a map of how a shipped grand
strategy game divides its responsibilities, which is useful whatever technology we
build on.

**Its data formats carry a lot of accidental history.** The interface files name
textures with the wrong extension, the font descriptors declare a texture page and then
omit the line naming it, one scrollbar definition has its arrow sprites swapped, and a
map editor is present but hidden by two `Hide()` calls because it was never finished.
None of that is worth reproducing; all of it is worth knowing about when writing a
converter, because each quirk is a case where a straightforward reader silently produces
something wrong rather than failing.

## Studying the original

Four Ghidra scripts live in `tools/ghidra/`. Add that directory via *Script Manager →
Manage Script Directories*; they appear under category **C++**. Keep that the only copy -
Ghidra's own `~/ghidra_scripts` takes precedence over any `-scriptPath`, so a stale
duplicate there runs instead of the one you are editing, silently.

Run them in this order, after auto-analysis has finished:

| Order | Script | Does |
| --- | --- | --- |
| 1 | `ReconstructClassesFromRtti` | class namespaces, vftables, virtual functions |
| 2 | `ChaseConstructorsAndDestructors` | constructors and destructors |
| 3 | `ApplyKnownNames` | names worked out by reading code |
| 4 | `ExportClassHierarchy` | regenerates `docs/` |

They parse the MSVC RTTI structures directly out of memory rather than going through
Ghidra's own RTTI classes, so they do not depend on which analyzers were run. The two
that classify code use Ghidra's function boundaries rather than scanning for padding,
because a hand-rolled boundary scan will happily place a function inside another one.

Identical COMDAT folding matters throughout: only 523 distinct functions sit in vftable
slot 0 across the whole binary, and 104 of them are shared between classes covering 2114
classes, so anything that maps addresses back to classes has to expect one function to
belong to several.
