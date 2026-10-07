# Working in this repository

**OpenHOI3**: a grand strategy game in the spirit of Hearts of Iron 3: Their Finest Hour, on
its own technology and its own asset formats. Godot **4.7.1 .NET/mono** and C#.

**This is not a reimplementation of `hoi3_tfh.exe`.** Matching the original file-for-file means
inheriting its data formats, its interface layout and its limits forever, and every design
decision then gets made by a 2012 engine. The game should *feel* like Hearts of Iron 3; it does
not have to *be* it. `readme.md` is where that goal is set out and is the document to read
first.

**The tree is early and provisional.** What exists is one iteration - a Clausewitz reader, an
install layerer, texture and font decoding, the beginnings of a data model, 66 tests - and it is
being reconsidered rather than extended. Do not treat the current shape of `src/` as settled,
and do not preserve a structure merely because it is there. `tools/ghidra/` in particular is
**superseded**: see below.

## Settled decisions, which are not merely unbuilt

Nothing should be designed around these coming back.

- **One canonical start.** No custom game starts, no bookmarked starts, no isolated scenarios.
  The original offers all four; collapsing them to one means one set of starting conditions to
  balance, one code path into the simulation, and one thing for the converter to produce. Every
  extra entry point is a permanent tax on all three. It also means the converter never reads
  `common/bookmarks.txt` or `scenarios/`, and no lobby is needed to choose between them.
- **A converter, which serves the game and not the reverse.** It reads a Hearts of Iron 3
  install and produces this project's own assets, so anyone owning the original can play with
  its content. Where the original's format is awkward, **the converter absorbs the awkwardness
  and our format stays clean.**
- **No game assets or data files are redistributed here.** Players who want the original's
  content must own a legitimate copy. This is a hard rule, not a preference: do not commit
  anything extracted from an install, including converted output, test fixtures lifted from it,
  or a texture "just for a screenshot".

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

**Most of this project does not need the executable at all.** The converter, the map, the
interface and the data model are driven by the original's `.txt` files, which are plain text and
largely self-describing. The executable is only needed where its behaviour is genuinely *not*
inferable from its data: the simulation formulas, the AI's scoring, and the orderings and caps
that nothing documents.

Two things the reverse engineering established that are worth knowing regardless:

- **The class structure is recoverable in full.** `hoi3_tfh.exe` is a 32-bit MSVC build
  shipping complete RTTI, so class names, inheritance, base offsets and vftable layout can be
  read exactly rather than guessed. `docs/hoi3_tfh-class-hierarchy.txt` holds a snapshot - 1212
  application classes under 58 roots - and is **generated, not edited**.
- **The data formats carry a lot of accidental history.** Interface files name textures with the
  wrong extension; font descriptors declare a texture page and then omit the line naming it; a
  scrollbar definition has its arrow sprites swapped; a map editor is present but hidden behind
  two `Hide()` calls because it was never finished. Expect the converter to absorb this.

**`tools/ghidra/` is superseded and outdated.** There is one Ghidra toolchain and it lives in
the fact base - a 2467-address, 291-struct record with an idempotent apply. Two toolchains
against one binary is the duplication to avoid; regenerate anything needed from that one rather
than extending these scripts.

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

**The install is found, not configured.** `CDirectorySettings.Discover()` walks up from the
project directory looking for a `Hearts of Iron 3` folder, and accepts one only if the content
is beside the executable - a folder holding a stray copy of the exe is not an install. Four
levels of walking is what reaches an install kept in the user's home directory rather than
beside the repository.

## Always

- **Never commit unless asked.**
- Tests are where a silent misreading gets caught. The parser and layering carry 66 of them
  because a wrong parse produces plausible-looking data rather than an error, and the original's
  own engine swallows its parse errors - so "it loaded" means nothing.
- Scripts go in the scratchpad, written with the `Write` tool, not a bash heredoc: heredocs here
  mangle apostrophes and turn `\Users` into a `\U` escape.
