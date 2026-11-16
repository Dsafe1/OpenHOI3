# Mods

How the original lays a mod over the install, and how this game does: the descriptor, the search path, `replace_path`,
the player's folder, the command line that selects a mod, and the survey of what a mod asks of this game. Read in
`reversing/findings/FINDINGS-modlayer.md` (2026-11-16), with the checksum's account of the search path in
`reversing/findings/FINDINGS-checksum.md`, section 3, which that reading corrects. Built 2026-11-16
(`lib/OpenHOI3.Content/Mods`, `CDirectorySettings`); **stage 1 of mod compatibility** - the layer, the launch and the
survey. Stage 2, a BlackICE new game set beside a BlackICE save, and stage 3, playing it, are the readme's *Next up*.

Every rule below is the executable's [exe]; what is in a `.mod` file and the folders it names is the mod's [mod].

## 1. The descriptor

A mod is a `.mod` file [mod], read with the Clausewitz grammar. The original lists them as `mod/*.mod` over its first
search path - the player's folder (section 2), then `tfh/`, then the install - each file name once, the first folder's;
so `tfh/mod/BlackICE GitHub.mod` beside the folder `tfh/mod/BlackICE GitHub`, and the install's own `mod/` too. A
descriptor's **virtual path** is `mod/<file>.mod`, never `tfh/...` and never absolute.

The keys it reads:

| key | what it is |
| --- | --- |
| `name` | required; the mod's name, what `dependencies` name and the tie-break of section 2 compares |
| `path` | the folder laid over the install, relative to the working folder - the install's, where the launcher starts the game |
| `archive` | an archive in its place, preferred over `path` |
| `user_dir` | the mod's player folder below the Documents folder's |
| `replace_path` | a folder whose listing the mod replaces (section 2); each line adds one, `\` turned to `/` and nothing else |
| `dependencies` | `{ "name" ... }`, mods to be laid behind this one |
| `checksum`, `affects_checksum`, `steam_id`, `pdx_id`, `gamersgate_id` | read and not used here (no reader of the first two was found) |

A key given twice keeps its last value. A descriptor with no `name`, or with neither `path` nor `archive`, is **refused**
and logged `Incorrect MOD descriptor: "<file>"`; the base game's own `mod/Dies_irae_*.mod`, written in the older
`extend`/`replace` form, are refused so under Their Finest Hour. Of two descriptors of one name the first is kept.

*Ours*: `CModReader` reads the keys above and **reports every other** - `extend` and `replace` among them - where the
original drops them without a word. An `archive` is **not read**: a mod with a `path` as well is laid over from its folder
and that is said; one with an archive alone lays nothing (readme, *Loose ends*). A `path` that is no folder is said; the
original's mount of it fails silently and its `user_dir` still applies (*likely*), as here.

## 2. The search path and `replace_path`

The original reads every file through one search path, front first:

- **without a mod**: the player's folder, `Documents\Paradox Interactive\Hearts of Iron III`; `tfh/`; the install.
- **with mods**: the player's folder - the Documents folder's `<user_dir>`, or with several mods their `user_dir`s joined
  with `_` - then each mod's folder, then `tfh/`, then the install. **The plain Documents folder is not in it at all.**
  Where no selected mod has a `user_dir`, the player's folder is the plain Documents folder.

The player's folder is also where everything is written - saves, logs (`setup.log`), `settings.txt`, the map cache - its
parent folders made as needed. `-userdir=` on the command line, or `userdir.txt` in the install, puts the Documents
folder's place elsewhere.

**Several mods.** Each starts at order -1000; a dependency's order is raised above its dependent's until nothing changes;
they are mounted highest order first and, among equals, the greater name first, each in front of the last - so the lowest
order and the smallest name stand frontmost, and a mod stands in front of what it depends on. The `user_dir`s are joined
in the order they are mounted.

**`replace_path`** is honoured only where a folder is **listed** for files - an event folder read whole, the history's
provinces, the localisation. Then:

- an entry applies where its `replace_path` equals the folder listed **exactly**: case and all, a trailing `/`
  significant, `\` turned to `/` and nothing else changed;
- **no subfolder is covered**: listing a folder's subfolders has no test, and each is then listed as a folder of its own,
  so `history/provinces` does not cover `history/provinces/africa` - which is why BlackICE names every one of the base
  game's province subfolders, its own being in `bice_*` folders;
- where entries apply, a listed file is kept only where its **first copy** is in the frontmost mod that names the folder,
  or in front of it - the player's folder always counts;
- **a file opened by its name is never stopped**: `map/default.map`, or a `common/*.txt` named in code, falls through to
  `tfh/` and the install where the mod lacks it;
- *likely*: a mod whose `path` is written with a backslash never matches its own place in the search path, so its
  `replace_path` lines do nothing.

*Ours*: `CDirectorySettings` is made with the selected mods (`Mods`, front first) and the Documents folder; a session is
made with one, so the mods are a property of the game being made and nothing static. `Resolve`, `ResolveTexture`,
`ListFiles` and `ListFilesRecursive` walk each mod's folder, then `tfh/`, then the install; the two listings apply
`replace_path` as above, per folder, by the first copy's layer; `LayerFolders` gives the same folders and `MayList` the
test, for a caller that lists by itself. `SearchPath()` is the original's whole search path, the player's folder first,
and what the file checksum walks (`checksum.md`, section 3). `UserFolder` is the player's folder of what is played, and
everything that reads or writes one there asks it: the settings (`CUserSettingsReader.DefaultPath`), the saves and the
save lists, the message settings, the map cache, the battle plans' folder and the detailed log.

**One departure**: the player's folder is **not a layer of game files** here (`CModRules.UserFolderIsLayer`), where the
original's search path puts it in front of everything - `docs/compatibility.md`, entry 28. And where the original reads
`settings.txt` and the map cache over the search path when the player's folder has none, this reads the player's folder
alone (entries 13 and 22).

## 3. Selecting a mod

**The original's command line**: its program's name taken off, the line is cut at **every** `-`, the piece before the
first dropped, each piece trimmed of space, tab, CR and LF - not of quotes - and compared case and all. `mod=` with
something after it selects a mod and may be given more than once; `userdir=` moves the player's folder, the last kept;
`threads=`, `logpostfix=`, `settings=`, `exclude_dlc=`, `noasserts`, `randomlog` and `dlcmod` are the others, and anything
else is passed over. A `mod=` value is matched **byte for byte** against a descriptor's virtual path, so a mod whose file
name holds a `-` cannot be selected. The launcher (*likely*) starts `hoi3_tfh.exe -mod=mod/BlackICE GitHub.mod`. **A value
that matches no descriptor, or a descriptor refused, selects nothing, and the game is plain Their Finest Hour with the
plain Documents folder, without an error.**

*Ours*: `Main` takes the engine's own arguments - what Godot leaves of the command line before a `--` - joins them back
into one line and reads it as the original reads it (`CModSwitch.Parse`), so an exported build placed in an install and
started as the launcher starts the original plays the mod the player picked; `-userdir=` is taken too. Beside it, the
developer's **`--mod=<name>`** after `--`, the name of a `.mod` file with or without the extension, case aside
(`CModReader.VirtualPathFor`) - listed above `RunDeveloperArguments` in `src/CSession.cs`. Both are read before anything
else is, so the main menu is the mod's too. A mod named that is not there is **said**, and the install is played as it is,
as the original plays it. VS Code's *Play OpenHOI3 (mod)* asks for the name, `BlackICE GitHub` by default, and brings up
the main menu with it.

The main menu's `version_label` and the lobby's `checksum` then carry the original's four spaces, `Mod: ` and the mod's
localised name after the letters (`CVersionText`; `reversing/findings/FINDINGS-frontend.md`, A.1) - the frontmost mod's,
which of several the original names not being read.

*Not built*: `userdir.txt`, `-settings=`, `-logpostfix=`, `-exclude_dlc=` and `-dlcmod`.

**The savegame tool**: `mod=<name>` after a command's other arguments lays that mod over the install for it
(`CToolDirectories`), taken out before the command reads its own; `checksum <install> <mod name>` keeps its second
argument too. `modsurvey`, `checksum`, `quads`, `mapcache` and `shaders` take it.

## 4. Checked

- **Without a mod nothing changed**, on 2026-11-16 against a baseline taken first from the same tree: `firstdays` on the
  1936 start save the same line for line but the timings; `script` the base game's thirteen problems, line for line;
  `replaycheck` 31 of 31 days, the same 6,766 steps, 15,795 commands and 17,445 AI draws; and the tests.
- **The file checksum is the original's** with the search path above - the player's folder in front without a mod, the
  Documents folder left out with one: Their Finest Hour `1499173604 (GSKH)`, BlackICE GitHub `1599539514 (OXLS)` and
  BlackICE 16.0rc1 `-1864650988 (SDXE)`, each the same as the original's own `setup.log`.
- The client brings up BlackICE's main menu with `--mod="BlackICE GitHub"` and with the launcher's
  `-mod=mod/BlackICE GitHub.mod`, its checksum's letters `OXLS` the original's, and Their Finest Hour's without either.

## 5. The survey

`OpenHOI3.SaveCheck modsurvey <install> [mod name]` lays the mod over the install and reads everything a new game reads:
the localisation, the countries, the map, the interface's `.gui` and `.gfx` - each texture a `.gfx` names looked for, and
each sprite a `.gui` names looked for among the `.gfx` names - the install's `autoexec.lua` and what it requires, the world
with every database, the history, the events and decisions, and then a new game as a player gets it, as far as it goes;
the AI is run no further than making a game runs it. Nothing in the simulation changes for it. It gathers every problem
the readers report, by section, kind and file, and sorts the sections into **ours** - what this game does not answer -
and **files** - what the files say that the original passes over as well:

| section | | what |
| --- | --- | --- |
| `unbuilt condition`, `unbuilt effect` | ours | every condition and effect keyword of the events and decisions that `CUnansweredConditions` or `CUndoneEffects` lists |
| `met in the two days` | ours | what the new game's two days asked of script that is not built (`CScriptReport`) |
| `lua` | ours | `autoexec.lua`'s errors as it loads |
| `lua, each require on its own` | ours | where `autoexec.lua` stopped, each later module it names required on its own, on a state of its own, and what fails; a module only on the `package.path` the mod writes relative to the working folder is told apart |
| `lua, asked in the world/new game` | ours | the Lua's errors and its unbuilt reports as the game is made |
| `lua native` | ours | a module required that is no Lua file anywhere - a DLL beside the original - with every field used under it |
| `lua class`, `lua static`, `lua method` | ours | a class of the original's API nobody provides, a static of one of ours it lacks, a method called with `:` that no class of ours has and no script defines |
| `script`, `interface`, `world`, `map`, `lua undefined` | files | the script's problems, the readers' reports, a texture or sprite in no file, a global no file defines |

The Lua's globals are read from the text (`CLuaGlobalScan`): every name that is no field, keyword or local of a block it
stands in, set against what the state holds once everything has loaded that would and what any file defines. It reads
`autoexec.lua`, every module loaded and every Lua file of the layered `script` folder and of `common`. A name made through
`_G[...]` is not seen, and a method is matched against every class's names, not its receiver's.

**On 2026-11-16, Their Finest Hour alone** (the control): ours 0; files 59 - the script's thirteen problems, the same as
`SaveCheck script`, 43 in the interface (33 of them the tutorial's textures, which the install does not have) and three
more of the world and the map.

**BlackICE GitHub**: the new game is made, in about 9 s, and ours comes to some 27,000 problems with a handful of causes:

1. **`lfs`, LuaFileSystem**, a native module the original loads from `tfh/mod/lfs.dll`: `script/file-io.lua` requires it on
   its first line, so `autoexec.lua` stops at its second require, and **none of BlackICE's AI is loaded** - every call of
   the game into the scripts fails, `AI_GenerateNonHistoricalRelation` 6,988 times in the new game alone. **Settled
   2026-11-16** by the global `OpenHOI3` (section 6): BlackICE's `file-io.lua` requires `lfs` only where it is absent.
2. **`remove_brigade`**, an effect not built: 6,919 uses, the order-of-battle events above all (Soviet, Spanish Civil War,
   German).
3. **`wx`, wxLua**, the native GUI library of BlackICE's old utility: 3,988 uses of 73 fields in 14 files, and 1,824 calls of
   its objects' methods; its successor, the ImGui overlay, is BiceLib's.
4. **BiceLib**, the mod's DLL (`docs/compatibility.md`, entry 25): 42 functions, 47 uses, in `bicelib_lua.lua` and the
   utilities - `BiceLib.Leaders` (6), `BiceLib.Units` (3), `BiceLib.GameInfo` (7), `BiceLib.ComplexPatches` (5),
   `BiceLib.BytePatches` (5), `BiceLib.Tooltips` (4), `BiceLib.EffectTexts` (4), `Commands`, `Events`, `Inspector`,
   `Overlay` and the console. Read from the mod's own Lua; nothing was read out of BiceLib.
5. The original's Lua API not provided: **country variables** - `CSetVariableCommand` (80 uses) and the methods
   `GetVariable` (81) and `GetVariables` (38) - `CMinisterTypeDataBase`, nine `CModifier._MODIFIER_*_` constants, and 29
   methods more (`GetProductionDistributionAt` 13, `GetPercentage` 11, `GetUnits` 6, ...), a few of them wx's objects in
   files that do not name `wx`.
6. `last_battle_loser_losses` and `last_battle_winner_losses`, conditions not built: 96 uses, most in `event test.txt`.

Of the files' 834: 333 `remove_country_modifier`s of modifiers BlackICE does not declare, 197 events with no title or
description, 84 counter textures in no folder, four tags whose history the base game ships and BlackICE's countries do not
declare, and 21 colours of BlackICE's `provinces.bmp` that `definition.csv` does not list - each what the original passes
over too. The mod's Lua writes `lua_output.txt` into the working folder as it loads (section 6).

## 6. A mod's script under this engine

**The working folder is the install.** The original's process runs in the install - the launcher starts it there, and
`tfh` and a mod's `path` are mounted relative to it (`reversing/findings/FINDINGS-modlayer.md`, section 3) - and a mod's
script opens its files, and writes its `package.path`, against it: BlackICE's `"tfh\\mod\\BlackICE " .. G_MOD_VERSION ..
"\\technologies"`. So the client makes the install its working folder as it starts, once the install is found. The folder
it started in is kept first, and a path switch's relative value - `--screenshot=`, `--loadingshot=`, `--record=`,
`--replay=`, `--load=` - is taken against that; from the editor it is the project folder, as before. The savegame tool
keeps its own working folder, since its commands take relative file arguments, but for `modsurvey`, which moves to the
install - or to the folder `OPENHOI3_WORKDIR` names, one whose `tfh` is a link to the install's, for a run that is not to
write into the install. A mod's script writes where the original's does: BlackICE's `lua_output.txt` in the install.

**The global `OpenHOI3`** (*ours*, `CLuaFiles`) is in every Lua state, and the original has no such global, so a script
that finds it knows which engine runs it. Its `Files[path]` is the names of a folder's files - its subfolders left out, in
the order the file system lists them, as LuaFileSystem's `lfs.dir` gives them on Windows - or nil where there is no such
folder; a relative path is taken against the working folder, and a folder is listed the first time it is asked for and
kept. It stands in for the one thing BlackICE asks of `lfs`, a native module this engine cannot load (a 32-bit DLL built
for the original's interpreter); the maintainer's design, 2026-11-16. BlackICE's `script/file-io.lua` requires `lfs` only
where `OpenHOI3` is nil and lists a folder through `OpenHOI3.Files` where it is not, so the original is untouched.

*Measured*, 2026-11-16, with the BlackICE repository's `file-io.lua` laid over an install linked together in a scratch
folder: the survey's ours from about 27,040 to 13,594 - `autoexec.lua` loads whole, and the calls of the game into the
scripts now run, the first of them to fail on `GetUnits`, a method of the AI's Lua classes not built (374 times,
`ai_politics_minister.lua`). The survey's static list still shows `lfs.dir` and `lfs.attributes`, which it reads without
the test around them.

## Not established

- Which mod's name the version label carries where several are played.
- Whether `-userdir=` given as a relative path is taken against the working folder (it is used as given, both here and there).
- The launcher's exact argument: the executable's side is settled, the launcher's code not read.
