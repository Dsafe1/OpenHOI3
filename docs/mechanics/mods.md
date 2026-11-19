# Mods

How the original lays a mod over the install, and how this game does: the descriptor, the search path, `replace_path`,
the player's folder, the command line that selects a mod, and the survey of what a mod asks of this game. Read in
`reversing/findings/FINDINGS-modlayer.md` (2026-11-16), with the checksum's account of the search path in
`reversing/findings/FINDINGS-checksum.md`, section 3, which that reading corrects. Built 2026-11-16
(`lib/OpenHOI3.Content/Mods`, `CDirectorySettings`); **stage 1 of mod compatibility** - the layer, the launch and the
survey. Stage 2, a BlackICE new game set beside a BlackICE save, is begun (section 7), and stage 3, playing it, is the
worklist's *Next up*.

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
and logged `Incorrect MOD descriptor: "<file>"`; the install's own `mod/*.mod`, written in the older `extend`/`replace`
form, are refused so under Their Finest Hour - **they are not mods but the base game's expansions from before it** (Dies
Irae Götterdämmerung, Dies Irae USA, Secret Weapons of WWII; the user's account, 2026-11-18), and only `tfh/mod` holds
Their Finest Hour's mods, which is all the launcher offers (`docs/launcher.md`, section 4). Of two descriptors of one name the first is kept.

*Ours*: `CModReader` reads the keys above and **reports every other** - `extend` and `replace` among them - where the
original drops them without a word - **but one of ours, `engine`**: the version of the engine a mod is made for, which the
launcher starts it on (`docs/launcher.md`, section 3). The original passes over it as over any key it does not know, so a
descriptor with one stays valid there; the engine itself does nothing with it. An `archive` is **not read**: a mod with a `path` as well is laid over from its folder
and that is said; one with an archive alone lays nothing (`docs/worklist.md`, *Loose ends*). A `path` that is no folder is said; the
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

**The order a folder is listed in is the names', not the layers'**: PhysFS lists every name over the whole search path
once, the frontmost copy's, **sorted by byte, case and all** - a binary search with `strcmp` in its listing callback - so
a base game file whose name sorts first comes before a mod's (`reversing/findings/FINDINGS-localisation.md`, section 3).
`confirmed`.

**The localisation is read as one text in that order**, the tables joined end to end with a line break after any that
lacks one, and **the first text a key is given stands**: a later line of a key that already has text in the language
read is logged as a duplicate and dropped, while a line with no text in it is logged as an empty key and leaves the key
to a later line. `confirmed` (`reversing/findings/FINDINGS-localisation.md`, sections 1 and 2). So BlackICE's `TECH_INVEST`, given twice in `zDD-tech.csv`, is its first line - "$TECH$
$DIRECTION$ $VALUE$", which names the technology and the amount - where reading the last gave "Gain Tech." for every
technology an event gives; 101 of its keys are given two different English texts, and none of the base game's.
`CLocalisation` sorts its tables so and keeps the first text (2026-11-19). **A key is matched case and all**: the
table is an exact, case-sensitive set (`confirmed`, the same findings, section 2), so the base game's `MANPOWER`
("Manpower Increase") and `manpower` ("Manpower") are two keys with two texts - as are `WAR_EXHAUSTION`, `ALLIES`,
`OPTIONS`, `industry` and `NONE`, besides eleven pairs whose texts agree - where matching without case, as this did
until 2026-11-19, kept one text for both. A line with fewer columns than the language's
takes another of the line's fields in the original - very likely the English - where this takes nothing: not read, and
English, the column read here, is never short.

*Ours*: `ListFiles` itself still lists the base layer first, then by name without case, which the registries built from
it were written against; which of them the original's order would change is in `docs/worklist.md`, *Loose ends*. The
localisation and, since 2026-11-19, the technology files sort their listing by byte with case.

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

**This project's launcher** (`docs/launcher.md`, section 5) writes neither form of the original's: it gives everything after
`--` - `--install=<folder>`, the install to play wherever the engine stands, and a `--mod=` for each mod by its virtual
path, which the developer's switch takes as it is - so that a mod whose file name holds a `-`, which the original's line
cannot name, can be chosen. `--install=` is read before anything else, ahead of the install the engine stands in.

**The savegame tool**: `mod=<name>` anywhere among the arguments lays that mod over the install for **every command**
(`CToolDirectories`), taken out before the command reads its own; `checksum <install> <mod name>` and `modsurvey <install>
<mod name>` keep their second argument too. Under a mod the tool works in the install, as the original does (section 6) -
or in the folder `OPENHOI3_WORKDIR` names, one whose `tfh` links to the install's, so that a run writes nothing into the
install - and every argument naming a relative file or folder (a path, or a name ending `.hoi3`, `.txt`, `.gz`, `.csv` and
the like, or a `key=` whose value is one) is made whole against where it was started first. Since 2026-11-16.

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
   files that do not name `wx`. **Built 2026-11-16** (`lua.md`, sections 4 to 5.1): the variables and the command, the unit
   list, the shares and `CFixedPoint64`, the free spies, allies, owned provinces, subject flag and cabinet, every modifier
   kind, the minister type database, a building's working level, a transport, any agreement, a faction's members, and every
   minister's `GetCountry`. What the survey still lists as missing methods - 61 calls of 15 - are wx's widgets' (`Clear`,
   `Freeze`, `GetSelection` and the like), the old utility's `UI` objects, in files that do not name `wx` themselves.
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
for the original's interpreter); designed so on 2026-11-16. BlackICE's `script/file-io.lua` requires `lfs` only
where `OpenHOI3` is nil and lists a folder through `OpenHOI3.Files` where it is not, so the original is untouched.

*Measured*, 2026-11-16, with the BlackICE repository's `file-io.lua` laid over an install linked together in a scratch
folder: the survey's ours from about 27,040 to 13,594 - `autoexec.lua` loads whole, and the calls of the game into the
scripts now run, the first of them to fail on `GetUnits`, a method of the AI's Lua classes not built (374 times,
`ai_politics_minister.lua`). The survey's static list still shows `lfs.dir` and `lfs.attributes`, which it reads without
the test around them.

*Measured*, 2026-11-16, the same way, once the Lua API of section 5's item 5 was built: the survey's ours 6,578 to 5,837,
none of it the calls of the game into the scripts; and `headless` to 1 February 1936 under BlackICE, the AI's Lua **7,626
errors to 39** - every one `IsUnitAvailable` handed nil for a unit type BlackICE's scripts named and its `units` folder
does not declare, which the original's process would not survive (`lua.md`, *Ours*). The mod renamed them the same
day; on 2026-11-19 the month gives **no Lua error**.

## 7. Stage 2: a BlackICE new game beside a BlackICE save

Begun 2026-11-19. **The reference saves** are `Documents\Paradox Interactive\Hearts of Iron III\BlackICE GitHub\save
games\Ireland1936_01_01_00.hoi3` and ten more of the same game to `Ireland1936_03_16_04.hoi3`, made that day by the
original with BlackICE GitHub as installed then and **BiceLib loaded with only `fixMaxInfrastructure` on** - the fix this
game makes for every game (`docs/compatibility.md`, entry 29) - so the original's own rules otherwise. A save made with
BiceLib's other patches on is no reference here: each changes a rule. **The checks are the base game's, given
`mod="BlackICE GitHub"`**, run with `OPENHOI3_WORKDIR` set to a scratch folder whose `tfh` links to the install's, so that
the mod's Lua writes nothing into the install (section 6).

**Mended on the way**, both of rules the base game cannot show:

- **A new game's look at the laws comes before its manpower** (`politics.md`, section 12): BlackICE's
  `conscription_law_two` script divides manpower by divisions, and with the pool counted it moved 17 countries to
  `volunteer_recruitment` - Germany's reserves at 70% to the save's 80%. Laws 839 to **856 of 856**, brigades' strength
  9,219 to **10,916 of 10,918**, the queue's prices 93 to **152 of 152**.
- **A new game's most manpower reads the provinces as the entry first made them** (`manpower.md`, section 4), with no
  country's `global_revolt_risk` in their `revolt_risk` static: `free_press` alone had cost Czechoslovakia a third.
  Manpower 40 to **77 of 79** and the player's.

**Where it stands**, against the start save as a player gets the new game (`firstdays`, `load`, `politics`, `diplomacy`,
`espionage`, `aistrategy`, `theatres`, `supplypass`), Their Finest Hour's own figures unchanged by either mending:

| | |
| --- | --- |
| loaded as a game here | every unit, brigade, leader and line, 2,815, 10,933, 8,819 and 154; the wars, pacts and convoys; technology levels 125,725 of 125,725. Not loaded yet: a province's `capital` (403), the country keys `nocategory`, `last_mission_cancel`, `prov_vp`, `offmap_vp` and `human`, 8 ships' technology not of their type, 69 brigades' `home` |
| a new game's two days | abilities, officers, influence, money and crude oil whole; strength 10,916 of 10,918 (New Zealand's two Samoa garrisons, by thousandths); organisation 10,486 of 10,918 (the Royal Navy's battleships 100.160 to 100.159 and the like); technology 92,825 of 92,825; energy, metal and rare materials 77 of 79 (Germany's a few, Spain's hundredths); fuel 68 and supplies 43 of 79 |
| politics | laws, government, ruling party, elections, unity and organisation whole; posts 1,167 of 1,177 and dissent 106 of 107 (the Russian rebels, `RUR`); the cabinet and popularity the rebels' (`REB`) |
| diplomacy | relation and threat 8,560 of 8,560 pairs, neutrality 107 of 107, alignment 105 of 107 |
| spies | everything but the level abroad, 11,252 of 11,342 |
| the AI's strategy | 79 of 79 in every list, since 2026-11-19 (below, *Capitals and air bases*) |
| theatres | 115 of 115 in every figure, the headquarters' brigades 66 of 66 |
| the supply pass | 199 of 262 networks the save's in every province and figure |

**The later saves** (2026-11-19). **Every one loads whole**: the units, brigades, leaders and lines, the wars, pacts,
trade routes, convoys and battles of the 16 January, 17 January and 16 March saves all the save's, their battles'
modifiers as entered too; the keys not loaded are the start save's and a province's `history` and six ships' `home`.
The 16 March save's three risings - the first rebel factions of any reference save - are read since 2026-11-19 and
written back byte for byte, armies numbered on the rebels' counter (`rebels.md`, section 6; `savegame.md`, section 14).
**The writer** leaves out a variable at nought and walks the rest by name as the original's (`savegame.md`, section 8),
and writes a technology declared twice at both places.

**A check that plays forward is taken from the save as loaded, under BlackICE**: `march ... loaded` and `research ...
loaded` (since 2026-11-19). Their plain run makes a new game and gives it the save's laws, levels and units, which is
enough for the base game; under BlackICE it leaves out what play has given - its AI's decisions give hidden
technologies (`AI_Transport_Ship_Amphib_Unlock` gives every AI country's transports 4 more speed), its Lua keeps country
variables the triggered modifiers read (the training and resource balances), events give modifiers. So, 17 to 18
January:

| | plain | `loaded` |
| --- | --- | --- |
| a fleet's progress along its edge | 4 of 12 | 17 of 18 |
| a division's progress, weather and fuel the same | 234 of 256 | 252 of 257 |
| research over the fortnight to 2 February | 630 of 1,081 | 985 of 1,059 |
| a building line's days | 37 of 77 | 56 of 77 |

What is left in them is the AI's - projects started and orders given in between - or the original's own figures moving
in between: a building line's price is set when it is made and again only on a day a line finishes, and a training
modifier comes on as BlackICE's variable passes its day mid-way (Latvia's `basic_training_days_active` 18 to 34, its
+3 starting experience from 30), both of which a check that holds the earlier save's figures for the whole stretch
counts as misses. The research check works each day's gain again after a month's decay since 2026-11-19 (`research.md`,
section 8) - it was 895 of 1,059 before, one project in twelve a unit ahead for 2 February - and leaves the rebels out, who divide no leadership (`leadership.md`, section 2)
and to whose projects a BlackICE save gives some.

**Capitals and air bases** (2026-11-19). Two rules the base game cannot show, both of the AI's strategy at the start, which
was 74 of 79 on `defend_prov` and 78 on three lists. **A capital the country does not own is moved before anything else of
a new game** - the original's in-game screen is made before its entry's set-up and moves it then, as each day's head does
(`war.md`, section 14): BlackICE's Shanxi, whose history names China's Beiping, has Taiyuan when its strategy is made,
where ours moved it only at its first day's head; its threat, antagonism and friends are the save's since. **The air base
is the building that carries `air_capacity`**, which BlackICE gives `fake_air_base` and not `air_base` (`buildinglines.md`,
section 1.1): the four countries' provinces with an `air_base` and no `fake_air_base` are on no list of the save's.

**A technology declared twice** (2026-11-19, `research.md`, section 1): BlackICE's `cag_design_hidden` is two technologies
under one name in the original, a look by name finding the later, and is so here; it had been the earlier alone, and the
checks had set ours beside the save's first entry of the two. The mod made the two one the same day, a slip of its own;
the rule stays, the original's for any mod that declares a name twice.

**The writer, set beside the March save** (2026-11-19, evening): nothing it holds is left out but one Saudi event
modifier whose date had passed at midnight - the original drops such a modifier when it next makes the country's
modifier, which entering a loaded game does, so a save it wrote after loading this one would leave it out too
(`modifiers.md`, section 4). Brought in on the way: a province's history and `capital`, a brigade's `home`, `prov_vp`,
`offmap_vp`, `last_mission_cancel`, the null category (`savegame.md`, sections 4, 5, 12 and 15), the technology files'
order, a leader's progress towards a trait two definitions give (`savegame.md`, section 5) and the posts the history
fills with another country's ministers (`politics.md`, section 2) - Reichskommissariat Ural's, which also brings the
start's posts to 1,177 of 1,177 and its dissent to 107 of 107. What still differs is held nowhere here - the leadership
put into each technology, `out_of_supply_days` counted once more on entering - or the order of `fired_events`, the
original's table's.

**The stockpiles of the start** (2026-11-19): what differs follows the shares of IC the AI sets in the two days, not a
rule of the day - `supplypass ... country=ENG` and `country=HOL` give each country's day the save's but for its
supplies share, Britain's 0.087 to the save's 0.04202, the Netherlands' 0.049 to its 0.20419, the other shares to the
fifth place; what makes and uses supplies agrees with the save's figures under each. So they wait on BlackICE's
production minister in Lua, the AI's side. **That side, 2026-11-19**: the supply need the script is handed is the
original's 15-binary-place figure, no longer cut to thousandths (`ministers.md`, section 5), and the written `distribution`
is the save's for 52 of 80 countries where it was 32, the start's supply stockpiles 43 of 79 as before - and 44, fuel 69,
since a unit on ground that draws on nothing pays the null country as the original's does (`supply.md`, section 13),
which Shanxi's does through the two days, its government sitting in China's Beiping. What is left
is mostly a new game's weather, drawn afresh with its mud elsewhere: given the save's mud and frost before the two days,
67 of 80 shares and 51 of 79 supply stockpiles, the Netherlands' supply share 0.20410 to the save's 0.20419. **Britain's
stockpile was settled in the running game** (2026-11-19: the original launched with BlackICE, a new game started as Ireland
and Britain's stockpile logged at its day's head and end and at the filling's start): ours was the original's up to the
filling and gained 163 supplies and 182 fuel more across it, paid a quarter over by a French fleet at Djibouti that the
original bases at Guadeloupe - the history gives no naval base to a province with no port, and a fleet with no base is
given its country's first (`fleetsandair.md`, sections 2 and 2.1). Since then Britain's stockpile is the save's to 0.065,
the supply stockpiles 54 of 79, fuel 70 and every brigade's strength 10,918 of 10,918 - New Zealand's Samoa garrisons,
whose 6617 also keeps no naval base. The Soviet Union's moves the other way, 153 short where it was 98 over: its day,
Moscow's network the worst of the supply pass, turns on small things upstream. What also stays open: the manpower of Ethiopia and China Yunnan
(`manpower.md`, *not established*), and 432 brigades' organisation a thousandth off - each country's one way, which a
single rule of rounding a positive figure would not give, and so not chased without a reading.

**Next**: stage 3, playing BlackICE, which waits for the user's word and the AI work.

## Not established

- Which mod's name the version label carries where several are played.
- Whether `-userdir=` given as a relative path is taken against the working folder (it is used as given, both here and there).
- The launcher's exact argument: the executable's side is settled, the launcher's code not read.
