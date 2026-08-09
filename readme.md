# OpenHOI3 - A rewritten replacement for the original Hearts of Iron 3 Their Finest Hour (TFH) in Godot

## Goal

The goal of this project is to create new Hoi3 TFH client, which can be used as a replacement for the original game exe.

Thanks to the Ghidra tool, we have a list of almost all ClassNames used in the orignal game. These shall be used to create a code structure that mimics the original source.

Users will have to provide their own copy of the game files. For development purposes the files shall be located in the "Hearts of Iron 3" folder, inside the parent folder of this git repo.

Only the executable is reimplemented. No game assets, data files or original code are redistributed by this project.

## Requirements

| Tool | Version | Notes |
| --- | --- | --- |
| Godot | **4.7.1 .NET/mono** build | The plain (non-.NET) build cannot run C# |
| .NET SDK | **9.x** | Godot 4.7.1 refuses to start its build tooling without an SDK matching its host runtime (`.NET Sdk not found. The required version is '9.0.10'`) |
| Ghidra | 12.1.2 | Optional; only needed to regenerate the class reference |

The project targets `net8.0`, which is what Godot 4.7.1's own project generator emits.
An SDK 9 builds that fine — the framework version and the SDK version are independent.

## Building and running

```sh
dotnet build OpenHOI3.sln
```

From VS Code, press `F5`. Three launch configurations are available: *Play OpenHOI3*,
*Play OpenHOI3 (headless)*, and *Open Godot Editor*. Each builds first.

The Godot executable path is not hardcoded. It is read from the
`godotTools.editorPath.godot4` setting contributed by the Godot Tools extension, so each
contributor points it at their own install. Set it in `.vscode/settings.json` (git-ignored)
or in your VS Code user settings.

To run without VS Code:

```sh
<godot> --path . --headless --quit-after 60
```

## Repository layout

```
scenes/Main.tscn     main scene (run/main_scene)
src/Core/            script parsing, install discovery, localisation
src/Gfx/             sprite definitions, texture loading, bitmap fonts
src/Gui/             parsed .gui types and the live controls built from them
src/FrontEnd/        the main menu screen
tools/ghidra/        Ghidra scripts for analysing the original binary
docs/                generated class reference - do not edit by hand
```

Class names follow the original where the mapping is clear, per the goal above. The
`C*Type` classes are parsed file definitions and the rest are live objects, which is
the split the original uses:

| Original | Here |
| --- | --- |
| `CGuiType` / `CWindowType` | parsed `.gui` entries |
| `CSpriteType` | parsed `.gfx` entries |
| `CGuiObject` / `TWindow` / `TButton` | controls on screen |
| `CBitmapFont` | `.fnt` + `.tga` fonts |
| `CFrontEnd` | the main menu |

## Interface data

Screens are not authored in Godot. They are read at runtime from the player's own
files, so the layout is the game's rather than a reproduction of it.

### Expansion layers

The install root holds **For the Motherland**. **Their Finest Hour** lives in `tfh/`,
which mirrors the entire tree and replaces only the files it ships. Every lookup walks
the layers highest priority first, so `tfh/` wins where it has a file and the base
install supplies the rest - the TFH front end uses its own backdrop and layout, but
falls back to the base `frontend_button` and `frontend_tile` textures.

Reading the root alone is not an error that announces itself: everything loads and
renders, you just get the previous expansion's interface. Layer order also decides
conflicts, so files are loaded base first and later definitions overwrite earlier ones.

Adding `tfh/` took the front end from 33 379 strings, 1308 sprites and 58 fonts to
34 513, 1468 and 60.

### File types

- `interface/*.gui` - window and control layout
- `interface/*.gfx` - `GFX_*` sprite names to texture files, and font declarations
- `localisation/*.csv` - the string table, semicolon separated, one column per language
- `gfx/fonts/*.fnt` - AngelCode BMFont descriptors with `.tga` atlases

Two quirks of the shipped data are worth knowing, since both look like bugs in a
reader that assumes well-formed input:

- Script files name textures with the wrong extension - the front end asks for
  `frontend_tile.tga` when only `frontend_tile.dds` exists - so texture lookups retry
  across the known image extensions.
- The `.fnt` descriptors declare `pages=1` but omit the `page` line naming the atlas,
  because the original loader assumed the image shares the descriptor's base name. A
  standards-compliant reader gets glyph rectangles with no texture and draws nothing,
  so the atlas is attached by hand after loading.

## What we know about the original binary

The target is `hoi3_tfh.exe` (~21 MB) from the Their Finest Hour install.

- **PE32, x86, 32-bit**, image base `0x400000`, six sections.
- Built with **MSVC**, and it ships **complete RTTI**. This is the single most useful fact
  about it: class names, inheritance, base subobject offsets and vftable layout are all
  recoverable exactly rather than inferred.
- It embeds **Lua via luabind**, which is a large part of why over half the RTTI type
  descriptors are library or template types rather than game classes.
- It shows the signature of **identical COMDAT folding** (`/OPT:ICF`): 335 function
  addresses appear in the vftables of unrelated class hierarchies, meaning distinct
  functions that compiled to identical code were merged onto one address. Any analysis
  that maps addresses back to classes has to account for this.

  Folding is heavier than that figure suggests. Only **523 distinct functions** sit in
  vftable slot 0 across the whole binary, and **104 of them are shared between classes**,
  covering **2114 classes** - one is reached from 477 luabind instantiations, another
  from `CButtonObserver` and all 171 of its observer glue adapters. Destructors fold
  readily because most are near-identical, so a name written per class will silently
  overwrite another class's. `CAir` and `CNavy` share a destructor for exactly this
  reason; `CArmy` does not, because its layout differs.

Recovered from RTTI:

| | |
| --- | --- |
| Type descriptors | 2769 |
| Complete object locators / vftables | 2707 |
| Classes total | 2645 |
| — application classes | 1212 |
| — library / template types | 1433 |
| Inheritance roots | 58 |
| Classes using multiple inheritance | 39 |
| Application vftable slots | 19402 |
| Distinct virtual functions | 4261 |
| — attributed to a declaring class | 3926 |
| — left unattributed (ICF-folded) | 335 |

No virtual inheritance is used anywhere in the application classes. 49 classes have no
recorded base; these are either genuine roots or interfaces whose own parents never appear
in any base class array.

## The class reference

`docs/` holds the recovered hierarchy, regenerated from the binary rather than maintained
by hand:

- **`hoi3_tfh-class-hierarchy.txt`** — the readable overview. Indentation follows the
  *primary base* (the one at offset 0, whose vftable the derived class extends);
  additional bases are listed inline. Each class shows its total vftable slots and how
  many virtual functions it introduces rather than inherits.
- **`hoi3_tfh-classes.json`** — the same data for tooling. Every class, including library
  types behind a `library` flag, with base offsets, vftable addresses, and the addresses
  of the virtual functions it introduces.

Example, showing multiple inheritance and slot attribution:

```
    CUnit  [also: PAVCSubUnit::__CList@0x38, CReferenceObject@0x8; 50 slots, 31 introduced]
        CAir  [52 slots, 9 introduced]
        CArmy  [50 slots, 10 introduced]
        CNavy  [52 slots, 10 introduced]
```

A leading `_A0x...` scope is MSVC's per-translation-unit anonymous namespace. It is kept
rather than stripped because different translation units declare classes of the same name —
there are, for instance, two unrelated `CAirAttackComparator`s.

The older root-level `ghidraClassReference` is a flat, prefix-grouped name list from an
earlier Ghidra 11.2.1 pass. It is superseded by the files above.

## Ghidra tooling

Four scripts live in `tools/ghidra/`. Add that directory via *Script Manager → Manage
Script Directories*; they appear under category **C++**. Keep that the only copy - Ghidra's
own `~/ghidra_scripts` takes precedence over any `-scriptPath`, so a stale duplicate there
runs instead of the one you are editing, silently.

Run them in this order, after auto-analysis has finished:

| Order | Script | Does |
| --- | --- | --- |
| 1 | `ReconstructClassesFromRtti` | class namespaces, vftables, virtual functions |
| 2 | `ChaseConstructorsAndDestructors` | constructors and destructors |
| 3 | `ApplyKnownNames` | names worked out by reading code |
| 4 | `ExportClassHierarchy` | regenerates `docs/` |

Only the first and last are truly standalone; the middle two expect the class namespaces
to exist already. None is specific to this game — retarget them by editing their
`LIBRARY_NAMESPACES` / `LIBRARY_PREFIXES` constants.

They parse the MSVC RTTI structures directly out of memory instead of going through
Ghidra's own RTTI classes, so they do not depend on which analyzers were run. The two
that classify code are the exception: they use Ghidra's function boundaries rather than
scanning for padding, because a hand-rolled boundary scan will happily place a function
in the middle of another one.

### `ReconstructClassesFromRtti.java`

Rebuilds the inheritance graph and writes it into the program database. It creates a class
namespace per application class, labels vftables, plate-comments each with its base list and
offsets, moves every virtual function into the class that *introduced* it, and names unnamed
ones `vf_NN` by slot.

The interesting part is the attribution rule. A function reachable from several vftables is
owned by whichever referencing class is an ancestor of all the others, so an inherited,
non-overridden slot is credited to the base rather than to every class that inherits it.
Where no referencing class is an ancestor of the rest, the address is shared between
unrelated hierarchies because of ICF, and it is left alone rather than misattributed.

Run it after auto-analysis. It executes as a single transaction, so one undo reverts it.

### `ChaseConstructorsAndDestructors.java`

Names the constructors and destructors of every class. Only a constructor or a
destructor writes a class's vftable pointer into the object, so one sweep of code for
each known vftable address as an instruction immediate finds exactly those functions.

Telling them apart uses the MSVC layout: vftable slot 0 holds the scalar deleting
destructor, and whatever it calls is the real one, leaving the other vftable writers as
constructors. Slot 0 is checked for the right shape first - it tests bit 0 of a flags
argument and calls something - because a class without a virtual destructor has an
ordinary method there instead.

Three things it refuses to guess at, each reported rather than hidden:

- A function writing several vftables belongs to the most derived of them, resolved
  from the RTTI hierarchy. One writing vftables of unrelated hierarchies is skipped.
- Where COMDAT folding gives several classes one function, the base class takes the
  primary name if one claimant is an ancestor of all the rest; otherwise the first
  alphabetically does, and every other class gets its own label at the same address so
  it stays navigable. This is the common case, not an edge case.
- Function boundaries come from Ghidra's analysis, never from scanning for padding.

Run it after auto-analysis and after `ReconstructClassesFromRtti`. It renames on the
order of 7500 functions, so save the project first; everything it writes is analysis
sourced, so re-running is safe and hand-written names are left alone.

### `ApplyKnownNames.java`

Names the functions worked out by reading the code. RTTI proves structure but carries
no names, so this is the manual layer on top: a table of address, name, confidence and
the evidence behind it, applied as symbols plus plate comments.

Run it after `ReconstructClassesFromRtti` so the class namespaces exist. Addresses are
image-base relative, so it survives a rebase, and anything you have renamed by hand is
left alone. Extend the table as more is identified.

### `ExportClassHierarchy.java`

Regenerates the `docs/` files. Works in the GUI (prompts for an output directory) or
headless. It only reads memory, so it does not need analysis to have run:

```sh
analyzeHeadless <proj-dir> <proj> -import hoi3_tfh.exe -noanalysis \
    -scriptPath tools/ghidra -postScript ExportClassHierarchy.java docs
```

It re-parses RTTI rather than reading back what `ReconstructClassesFromRtti` wrote, because
Ghidra has no queryable field for inheritance edges — there would be nothing to read back.
The hierarchy is a fixed property of the binary, so the generated files only need refreshing
if the target executable changes.
