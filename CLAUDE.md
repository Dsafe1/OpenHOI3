# OpenHOI3: the rewrite

*These rules are for `OpenHOI3/`. Opened from the `workspaces/hoi3` root, all four of these
files arrive at once - apply each to its own folder.*

**OpenHOI3**: a grand strategy game in the spirit of Hearts of Iron 3: Their Finest Hour, on
its own technology, able to run the original's content and its mods as they are written. Godot
**4.7.1 .NET/mono** and C#.

**This is not a reimplementation of `hoi3_tfh.exe`.** The engine, the simulation and the
rendering are our own and nothing is derived from the original's binary. What is kept is the
**language its content is written in** - script, data files, interface files - because that is
where the modding knowledge is. `readme.md` is where that goal is set out and is the document
to read first; it also holds the staged plan.

**This reversed on 2026-10-07.** Until then the rule was our own formats throughout, fed by a
one-way converter, with "our format stays clean" as the test. A session reading this
repository's history will find that stance everywhere; it is no longer the one to apply.

**The tree is early and provisional.** What exists is one iteration - a Clausewitz reader, an
install layerer, texture and font decoding, the map with the start-date ownership and
terrain of its provinces, a map view, 95 tests - and it is
being reconsidered rather than extended. Do not treat the current contents of `lib/` and `src/`
as settled, and do not preserve a structure merely because it is there.

## Three projects, and which way they may reference

Split on 2026-10-07, so that the simulation can run headless and under test:

| project | what it is | may reference |
| --- | --- | --- |
| `lib/OpenHOI3.Sim` | the model and the simulation | **nothing** - not Godot, not `Content` |
| `lib/OpenHOI3.Content` | reading an install: the Clausewitz format, layering, localisation, the loaders | `Sim` |
| `OpenHOI3.csproj`, with `src/` | the Godot client: scenes, drawing, input, texture and font decoding | both |

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
    nothing recognises is reported, where the original drops it silently.
  - *Heavy assets get a cached build* - textures, fonts, and whatever else is measured to be
    slow - into our own formats, rebuilt only when a source file changes. **Measure before
    caching:** the province bitmap was assumed to need this and builds in about 120 ms, so it
    is read every start (`docs/mechanics/map.md`, section 6). The cache is derived data: never
    authored, never committed.
  - *The interface is loaded from the original's `.gui` files* and laid out by our own
    runtime. See the next section.
- **The base game first, mods second.** Their Finest Hour alone is the target until it works;
  BlackICE then becomes a third layer over it.
- **Fixed-point numbers in the simulation.** Thousandths, as the original stores them, for
  determinism and for rounding the way the original does. No `float` or `double` in game state.
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
- **Bitmaps do not gain detail, so a texture may be supplied at any resolution.** Icons and the
  original's bitmap fonts soften when enlarged; the answer is that a mod can ship a sharper
  file, and the runtime works in ratios of the texture rather than in its pixel counts. Two
  things follow from how the files are written:
  - *An element's size on screen must never come from a replacement's pixel count.* Icons,
    buttons and checkboxes declare no size in the `.gui` file - about 1,500 entries in Their
    Finest Hour - and take it from the texture. So something has to say what size a texture
    was designed at. **How is not decided** (2026-10-07). The leading idea is that the
    original file stays as the size reference and a sharper one overrides its pixels from a
    higher layer, which needs no new syntax and leaves the mod valid in the original.
  - *Sprite sheets need no naming scheme.* A sheet declares its frame count in the `.gfx`
    entry (`noOfFrames`, on 226 of about 1,300 `spriteType` entries), so a frame is the
    texture's width divided by that count at any resolution. The pixel-valued sprite keys -
    `size` and `borderSize` on the cornered-tile, tile and progress-bar kinds - scale by the
    same ratio as the texture they describe.
- **There is prior art in history.** `src/Gui/` and `src/FrontEnd/` ran the original's main menu
  from its `.gui` files and were removed in commit `4269708`. Start from `src/Gui/`; the lobby
  that `src/FrontEnd/` drove is not coming back.

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

`GitHub/hoi3-reversing`, normally checked out beside this repository: 85 `findings/FINDINGS-*.md`
write-ups, `CLASSES.md` for the class-by-class account, and `TRAPS.md`. Cite a findings file
with the `reversing/` prefix on the front - the fact base's logical name, which resolves
wherever it is checked out. Its `checkrefs.py` verifies those references from the other
side, including the ones written here.

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
  scrollbar definition has its arrow sprites swapped; a map editor is present but hidden behind
  two `Hide()` calls because it was never finished. Expect the loaders to absorb this.

**There is no Ghidra tooling here any more.** `tools/ghidra/` was removed on 2026-10-07: there
is one Ghidra toolchain and it lives in the fact base - a 2467-address, 291-struct record with
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

From VS Code, `F5` offers *Play OpenHOI3*, *Play OpenHOI3 (headless)* and *Open Godot Editor*.
The Godot executable path comes from the `godotTools.editorPath.godot4` setting rather than
being hardcoded, so set it in `.vscode/settings.json` (git-ignored) or in user settings.

**The install is found, not configured.** `CDirectorySettings.Discover` walks up from the
project directory it is given looking for a `Hearts of Iron 3` folder, and accepts one only if the content
is beside the executable - a folder holding a stray copy of the exe is not an install. Four
levels of walking is what reaches an install kept in the user's home directory rather than
beside the repository.

## Always

- **Never commit unless asked.**
- Tests are where a silent misreading gets caught. The parser, the layering and the map carry 95 of them
  because a wrong parse produces plausible-looking data rather than an error, and the original's
  own engine swallows its parse errors - so "it loaded" means nothing.
- Scripts go in the scratchpad, written with the `Write` tool, not a bash heredoc: heredocs here
  mangle apostrophes and turn `\Users` into a `\U` escape.
