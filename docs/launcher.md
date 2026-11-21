# The launcher

How a player starts this game and chooses what to play: a launcher of its own, outside the engine, that lists the mods,
picks the engine each choice is to run on and starts it. **Decided 2026-11-18, and built the same day**:
`launcher/OpenHOI3.Launcher` (the window, in Avalonia), `lib/OpenHOI3.Content/Launch` (everything it decides, under
test), `tools/build_launcher.ps1` and `tools/build_both.ps1` (the build, section 7). What the engine does with a mod once it is started is `mechanics/mods.md`;
this is what happens before.

## 1. Why a launcher of its own

The original's own launcher, `launcher.exe`, draws a window from `launcher/launcher.cfg` - a combo box under "Select mod
to play", a settings tab, a start button - and starts the game with `-mod=mod/<file>.mod` (`mechanics/mods.md`, section
3). The engine here already reads that command line, so the normal engine needs a launcher only to give the player the
choice without typing it.

The reason for a program of its own is **the enhanced engine** (`enhanced.md`). It will change the game sweepingly, and
a mod written for it is written for the version of it that does what the mod asks - as BlackICE is written for one
build of `hoi3_tfh.exe` today. So a mod for the enhanced engine **ships the engine it was made for**, and the launcher
runs that engine, not one of its own choosing. The user's decision of 2026-11-18. A picker inside the engine, which the
engine's design would have allowed (a session is made with its mods, nothing static), could not choose *which engine*
to run, so it was not taken.

**What it is, decided 2026-11-18**: an Avalonia program in C#, about 38 MB as one self-contained file - an engine's
export is about 210 MB, most of it Godot and .NET, which a launcher made with Godot would carry again. It compiles in
`OpenHOI3.Content` to read descriptors as the engine reads them, at its own version; nothing is shared with an engine at
run time (section 5). It draws in the repository's own Liberation Sans, as nothing drawn here comes from the machine.

## 2. Where an engine lives

**The enhanced engine lays a mod over `tfh/` and the install as the normal engine does** (decided 2026-11-18). So a mod
and its engine stay where the original keeps a mod, and the launcher keeps its own the same way:

    <anywhere>/                     the launcher's folder, which need not be in the install (decided 2026-11-18)
        OpenHOI3.Launcher.exe
        engine/                     the launcher's own engine: the normal engine
        licences/
    <install>/
        tfh/mod/<name>.mod          the descriptor, naming the engine
        tfh/mod/<name>/             the mod's folder, laid over tfh/ and the install
        tfh/mod/<name>/engine/      the engine it ships

The launcher's own engine plays the base game and every mod that names none - which is every mod written for the
original. A mod's engine folder is inside a layer of game files, which is harmless: nothing the original or this engine
opens or lists is under it, and the file checksum sums only its own folders (`mechanics/checksum.md`, section 1).

**An engine says what it is in an `engine.txt`** beside its program (`CEngineFile`), so the launcher knows it without
starting it - ours, so UTF-8:

    version = "openhoi3-0.1.0"
    executable = "OpenHOI3.exe"
    contract = 1

`version` is what a mod's descriptor names, compared case and all; `executable` is relative to the folder; `contract` is
the version of the command line the engine understands (section 5). An engine folder with no `engine.txt`, or one
whose program is not there, is no engine.

**The version, since 2026-11-21** (`tools/engine_version.ps1`, which `tools/build_engine.ps1` asks): one program plays
the faithful game and the enhanced one (`enhanced.md`), so the version says nothing of the mode - it was `normal-`, the
date and the commit until then. **A release's is `openhoi3-<x>`**, where the tree is clean and its commit carries the
tag `v<x>`: tag `v0.1.0` gives `openhoi3-0.1.0`, which is what a mod is made for and names. Any other build's is
`openhoi3-<yyyy.MM.dd>-<commit>`, with a `+` where the tree had changes not committed - a build between two releases,
for its builder.

**A mod may ship a launcher too**, in `tfh/mod/<name>/launcher/` (decided 2026-11-21, for BlackICE: the launcher and
the engine go with each version of the mod, so that a new version of the mod brings a new launcher and none has to
stay compatible with an old one). A launcher's own engine is the `engine` folder in its own folder or, where that
holds none, the `engine` folder beside its own (`CEngineFile.OwnFolder`) - so a launcher a mod ships plays the base
game and every mod naming no engine on the engine that mod ships, the one copy.

## 3. The descriptor names its engine

A mod says which engine it wants in its `.mod` file, by a key of ours:

    engine = "enhanced-1.0"

rather than leaving the launcher to look for a program in its folder. Then the launcher can say *this mod wants engine X
and it is not here* instead of failing as it starts, and a shipped binary is only one way of having X. The original
passes over a key it does not know, so a descriptor with one stays valid there. `CModReader` reports every key it does
not know (`mechanics/mods.md`, section 1) but this one, `CModReader.EngineKey`, which it reads into `CMod.Engine`; the
engine does nothing with it.

## 4. The mods, and which engine runs

**Several mods may be ticked at once** (decided 2026-11-18), as the original's command line takes them and its launcher
does not. **Only `tfh/mod` is offered** (`CModList`): the original also lists the install's own `mod` folder and the
player's folder's, but the install's `mod` holds the base game's expansions from before Their Finest Hour - Dies Irae
Götterdämmerung, Dies Irae USA, Secret Weapons of WWII - which are not mods, and only `tfh/mod` holds Their Finest Hour's
(the user's account, 2026-11-18). Each is shown in the original's order, with its file and what it needs. A refused one -
no name, or neither a path nor an archive, or a second of one name - is shown greyed with the reason in a line; what the
reader reports only for a mod's maker, a key no `.mod` file has, goes to the engine's log and not to the player.

Ticking a mod ticks what it depends on, through every step, and unticking one unticks what depends on it (`CModPicks`):
so a choice never lacks a dependency that is there to be had. A dependency no descriptor has is said on the mod's row,
and the mod may still be ticked - the original plays it so. The engine orders what is ticked as the original mounts
mods.

Then the engine (`CEngineChoice`):

- No mod, or mods none of which names an engine: the launcher's own.
- Mods that name an engine: that engine, found in the `engine` folder of a mod that names it with that `version`, or the
  launcher's own where its version is that one. A mod that depends on another and names none takes the one its
  dependency names, since the dependency is ticked with it - which is how a submod of BlackICE runs on BlackICE's.
- Mods that name two different engines cannot be played together, and the launcher says so and greys Play.
- An engine whose `contract` is not the launcher's is not started, and the launcher says so.

The line beside Play says what will be played on which engine - "BlackICE GitHub, on the launcher's own engine,
normal-2026.11.18-9d2ff5f" - or why nothing can be.

## 5. What passes between the launcher and an engine

**The command line, and nothing else.** A launcher of one year will start an engine of a later one, so it shares no code
with an engine at run time (`CLaunchContract`). It starts the engine's program in the engine's folder with

    OpenHOI3.exe -- --install=<the install> --mod=mod/<file>.mod --mod=...

and closes, as the original's launcher does. Everything is after `--`, where the engine's own switches are, so no value
is cut at a '-' as the original's reading of its line cuts it: a mod whose file name holds one can be chosen here.

- **`--install=`** names the install, which the engine checks as a player's choice is and plays wherever it stands - read
  before anything else, ahead of the install the engine stands in (`Main.FindInstall`, `CDirectorySettings.Named`).
- **`--mod=`** names a mod by its virtual path, which the developer's switch takes as it is (`mechanics/mods.md`,
  section 3).

**`CLaunchContract.Version` is 1**, and is raised whenever a switch changes its meaning or the engine gains one it must
not ignore; `engine.txt` states it and the launcher matches it exactly. The player's folder is not moved by the launcher,
so `-userdir=` is not passed.

## 6. Finding the install, and what is remembered

The launcher looks for an install (`CInstallSearch`) where it remembers one, in its own folder and walking up from it as
the engine walks, and then in each Steam library - Steam's own folder from the registry, and every `"path"` of its
`steamapps/libraryfolders.vdf`, each with `steamapps/common/Hearts of Iron 3`. The first that is an install - one with
`interface`, `gfx` and `localisation` - is taken; **Choose...** picks another, which must be one too.

**It remembers the install and the mods ticked** in `%APPDATA%\OpenHOI3\launcher.txt` (`CLauncherSettingsFile`) - in the
player's application data, as the launcher's own folder may not be writable - when an install is chosen and when Play is
pressed. A remembered mod whose descriptor is gone is dropped, and the dependencies of the rest ticked again.

## 7. Building it

Three scripts in `tools/`, since 2026-11-19 (the user's: "Just make it 3"):

- **`build_engine.ps1`** exports the engine into `export/OpenHOI3/`, with its `engine.txt`;
- **`build_launcher.ps1`** publishes `export/Launcher/`: the launcher as one self-contained, trimmed file for Windows,
  and `licences/` with this project's GPL, Liberation's OFL and a note of the MIT terms of .NET, Avalonia, SkiaSharp and
  HarfBuzzSharp - with no engine of its own;
- **`build_both.ps1`** runs the two and copies the engine into `export/Launcher/engine/`, about 245 MB in all: what a
  player is given.

And since 2026-11-21 a fourth and a helper:

- **`engine_version.ps1`** says the version a build of the tree is (section 2); `build_engine.ps1` and
  `build_both.ps1` take `-Release`, which refuses a tree that is no release before anything is built;
- **`build_release.ps1`** builds both from a release's tree and packs `export/release/`: `OpenHOI3-engine-<x>.zip`, the
  engine alone with `engine.txt` at its root, which a mod's release puts in its `engine` folder;
  `OpenHOI3-launcher-<x>.zip`, the launcher alone; `OpenHOI3-<x>.zip`, the launcher with the engine as its own, for a
  player of the base game; `SHA256SUMS.txt`, which a mod's packaging checks what it fetched against; and `release.md`,
  the notes, with the version a mod names and the tag that is the source. **It publishes nothing**: it ends by printing
  the `git push` of the tag and the `gh release create` that would, which are its user's to run. `-AllowUntagged`
  packs a tree that is no release under its date-and-commit version, as a trial of the packaging.

A release is made so: commit, `git tag v<x>`, `tools/build_release.ps1`, then the two commands it prints.

The launcher run by hand takes **`--engine=<folder>`** in place of its own `engine` folder.

**Checked on 2026-11-18**: the logic's 30 tests (`EngineChoiceTests`, `ModPicksTests`, `LaunchContractTests`,
`LauncherSettingsTests`), the engine's reading of the command line among them; and the published launcher with its
engine, run: on its own it found a Steam install by `libraryfolders.vdf`; given the repository's install with BlackICE
GitHub ticked, Play started the engine in `engine/` with the line above, the launcher closed, and BlackICE's main menu
came up - the engine standing outside the install.

## 8. Saves and multiplayer

- **A save made under an enhanced engine is that engine's.** It should record the engine it was made with, and the
  launcher, asked to go on with a save, start that engine.
- **The file checksum does not cover an engine**, so two players on different engines of one mod show the same letters.
  Whatever this game checks between players has to check the engine as well - **built with multiplayer**: the play
  checksum a machine is let in by hashes the engine's version (`CPlayChecksum`; `mechanics/checksum.md`, section 8).

**The save's record is built since 2026-11-21**: an enhanced game's save holds `openhoi_engine`, the version of the
engine that wrote it, beside its rules (`enhanced.md`, *What is built*), and the play checksum hashes the version the
`engine.txt` beside the running program names (`CEngineFile.VersionBeside`), where it hashed an empty one. The
launcher going on with a save is not built.

## 9. A mod's repository and its release

A mod's repository holds the version it names, not the engine: an export is over a hundred megabytes, and a binary kept
in git stays in its history. The mod's release puts that engine into its folder - which is how OpenRA's mods are made,
each one naming an engine release that its tools fetch and package with it. **Nothing of a mod's release is built**:
BlackICE names no engine, being written for the original. Planned on 2026-11-21, for BlackICE's version 16
(`blackice.md`): its release fetches a release of this engine (section 7) by the version it names, and its installer
puts the engine in the mod's `engine` folder and the launcher in its `launcher` folder (section 2), the one copy
playing the base game as well.

## 10. Licence and trust

- The engine is GPL-3.0 (`readme.md`). A mod that ships it has to offer its source, which a link to the tagged release
  does; the mod's release carries the link.
- The launcher runs a program a mod brought. A mod can run code under the original already - a Lua native module, an
  injected DLL - so nothing new is let in, but the launcher says which engine it is starting: the line beside Play
  names it and whether it is its own or the mod's.

## Decided, and not built

- **Updates** (decided 2026-11-18): the launcher starts what is there and says which engine is missing. Fetching engines,
  or updating itself, may come later.
- **Settings**: none in the launcher; the game options window writes `settings.txt`.
- **Steam**: Steam starts the install's own `launcher.exe`, which is the original's, and is left alone. A player adds
  the launcher to Steam as a non-Steam game.
- **Other systems**: the launcher is built and checked for Windows. Avalonia runs elsewhere; the engine's export and the
  install search have not been tried there.
