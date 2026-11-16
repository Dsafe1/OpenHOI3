# BiceLib: what BlackICE's DLL gives the original, given by this engine

BlackICE ships **BiceLib**, a DLL injected into the original's process that the mod's Lua loads with `require("BiceLib")`.
It registers 58 functions with the game's Lua, in a table `BiceLib` and fourteen tables under it, which switch on its
patches and hooks, give its overlay figures, and call back into the mod's Lua. This engine cannot load the DLL - a 32-bit
module built against one build of one executable - so **it gives the functions itself, as `OpenHOI3.BiceLib`**: the
maintainer's decision of 2026-11-16. BlackICE's `script/bicelib_lua.lua` takes `OpenHOI3.BiceLib` in the DLL's place where
the global `OpenHOI3` exists (`mods.md`, section 6) and `require`s the DLL only where it does not, so the original is
untouched.

**Where this comes from.** By the maintainer's exception of 2026-11-16 (`CLAUDE.md`, *What this repository may not do*),
BiceLib's README and source were read for **what each function does** - its rule, its text, its switch - and nothing
else: no address, offset, structure layout or hook is here or in the code. Where a patch changes a rule of the original's
own, the original's rule is the fact base's, cited at each entry; where the fact base has no reading of it, that is said.
What BlackICE calls was read from its own `script` folder, every file of it.

`lib/OpenHOI3.Lua/BiceLib` (`CBiceLibApi`, `CBiceLibFunctions`), `lib/OpenHOI3.Sim/Mods` (`CBiceLibSwitches`,
`CBiceLibDaily`, `IBiceLibScripts`), `CSetUnitLimitCommand` and `CBiceLibSwitchCommand` (`Sim/Commands`).

## 1. The table

`OpenHOI3.BiceLib` has **every function the DLL registers, under the same tables and names**: `setModuleBase`,
`startConsole`, `stopConsole`, and `GameInfo`, `Leaders`, `Units`, `Navy`, `BytePatches`, `ComplexPatches`, `Events`,
`EffectTexts`, `Tooltips`, `Messages`, `Reversing`, `Commands`, `Inspector` and `Overlay`. Each is a Lua closure handing its
path and its arguments to one C# dispatcher. By its verdict (section 2):

- **nothing to do here** and **already so here**: a plain no-op, reported nowhere. Where the DLL's function answers
  whether it worked, this answers `true` - or `false` where nothing was done that the caller could use: an example command
  not posted, a probe not armed.
- **not built**: does nothing, answers nothing - so a getter the mod reads a value from gets `nil`, which every caller in
  BlackICE tests or defaults (`or 0`) - and is **reported once a session, by name**, through the Lua API's report of what is
  not built: `LUA unbuilt: BiceLib.Tooltips.activateManpowerBreakdown` (`CGameApi.ReportUnbuilt`, `lua.md`, section 4). So
  `modsurvey` lists it as ours (section 6).
- **built**: sets one of the game's switches (section 3) and answers `true` where the DLL answers whether its hook went in.

A path the table does not have is a Lua error naming it, as a missing method is.

## 2. The functions

The functions BlackICE calls are marked **called**: in `bicelib_lua.lua` unless another file is named; those in the old
utility's files are behind the `OpenHOI3 == nil` gate and never run here (section 6). Sizes are for what is left to build.

### Nothing to do here

| function | what it does in the original | called |
| --- | --- | --- |
| `setModuleBase` | prints the executable's base to the DLL's console | yes |
| `startConsole`, `stopConsole` | opens and detaches the DLL's console window | `gui-utility-options.lua` (gated) |
| `Commands.registerBiceCommands` | puts the DLL's own command in the game's command registry, for its multiplayer-safe actions. Every change here is a command already (`tick.md`, section 4); answers `true` | yes |
| `Commands.postExampleCommand` | posts the DLL's example command; answers `false`, nothing posted | no |
| `Reversing.activateCounters`, `activateWatch`, `watchWrite`, `watchCombatModifiers`, `watchSubunit`, `stopWatching`, `probeMessages` | the DLL's workbench probes on the running executable; the watches and the probe answer `false` | commented out |
| `Overlay.enable`, `Overlay.toggle` | installs the Direct3D hooks the DLL's overlay draws through, and shows or hides it. The overlay itself is section 4's | `gui-imgui.lua` |

### Already so here

A fix this engine already makes for every game: a no-op. **Each is a place where this engine departs from the original
without a switch, for every game and the base game too** - so each is also a question for the maintainer: whether to keep
the original's behaviour unless a mod's Lua turns the switch on. None is decided here.

| function | what it does in the original | here |
| --- | --- | --- |
| `EffectTexts.activateTriggerIndent` | indents a requirement tooltip's conditions inside an `and` and an `or`, which the game draws flush left | `docs/compatibility.md`, entry 10: the maintainer chose the corrected arithmetic for every game on 2026-10-12. The one already decided |
| `BytePatches.fixMinisterTechDecay` | the per-category technology decay modifier the ministers give is thrown away when the country's modifier is rebuilt - the technologies' figure is stored over it - so ministers' `decay` does nothing; the patch adds the two (`reversing/findings/FINDINGS-bicelibsites.md`, section 4; `FINDINGS-techdecay.md`, section 4) | `research.md`, section 6, adds the ministers' and the technologies' (`CResearch`, `CCountryModifiers.MinisterDecay`), so ministers' decay works in every game. **Not in `docs/compatibility.md`** |
| `ComplexPatches.fixMaxInfrastructure` | a province's infrastructure for its supply area is taken from whichever building with the `infrastructure` effect the game handles last - with BlackICE's `extra_infra` after `infra`, a loaded province read `extra_infra`'s, about 1,600 provinces fell out of every area and the AI's sealift crashed; the patch keeps `infra`'s (`reversing/findings/FINDINGS-extrainfra.md`). Answers `true` | `CSupplyFigures.MostInfrastructure` takes the **first** building in `buildings.txt` with the effect - `infra` in BlackICE, before `extra_infra` - so it is so. Their Finest Hour has one such building and no difference |
| `Events.rebuildCandidates` | a loaded game's candidate event lists are empty in the original until the month turns, so events fire in a burst on the 1st; the mod's Lua has the game rebuild them once after a load (`ai_omg_handlers.lua`; `reversing/findings/FINDINGS-events.md`) | `events.md`, section 1: entering a game makes every country's candidates, `CTick.RunEntryPass`, a loaded game's too. The fact base's account of the original says a load leaves them empty, which would make this a departure for every loaded game |

### A rule of the simulation

| function | what it does | touches here | size | built |
| --- | --- | --- | --- | --- |
| `Leaders.activateOobLeaderRanks` | a leader an order of battle puts on a land unit of his own country gets the rank the unit takes, raised or lowered, and is put in service (section 3.1) | `CLeaders.PutOnFromFile`, a new game's files and `load_oob` | small | **yes** |
| `Leaders.activateHistoryLeaderRanks` | a leader whose history dates his first rank after the start comes into service at that rank, not 1 (section 3.2) | `CLeaders.ActivateDaily`, `CLeader.FirstRank` | small | **yes** |
| `Units.setCorpsUnitLimit`, `setArmyUnitLimit`, `setArmyGroupUnitLimit` | how many units a corps, an army and an army group takes, per country or for all (section 3.3) | `CDeployment.MayTakeSubordinate` | small | **yes** |
| `Units.addCommandLimitTrait` | a leader trait that raises or lowers his headquarters' limit (commented out in BlackICE) | the same | small | **yes** |
| `BytePatches.disableWarExhaustionNeutralityReset` | every country's war exhaustion is put back to nought each month, so none is ever paid into neutrality (section 3.4) | `CWarExhaustion.RunMonthly` | small | **yes** |
| `ComplexPatches.fixQueuedHistoricalModel` | a brigade queued with `historical_model` gets level 0 for each technology its model leaves out, instead of the levels after it moving up a place (section 3.5) | `CBuildQueue.SetModelToHistorical` | small | **yes** |
| `ComplexPatches.enablePlacingNonResearchedBuildings` | a finished building waiting to be placed may go where the country lacks its technology - only research waived, every other test of the game's kept (`reversing/findings/FINDINGS-bicelibsites.md`, section 7) | where a building waiting is placed (`buildinglines.md`, section 6) | small | no |
| `ComplexPatches.fixOffMapIC` | the country's own `ic` modifier - an event's or a triggered modifier's - becomes IC beside its provinces', which the original never reads (`reversing/findings/FINDINGS-ic.md`, *The offmap IC fix*). **What the original's figure holds when the DLL reads it is not established** there | `ic.md`, section 3, `CIndustrialCapacity` | medium | no |
| `ComplexPatches.customResourceConversion` | a played country converts energy to crude oil and crude oil to fuel by eight country variables of its own (`zzDsafe_*`), held to the game's capacity and never into the day's energy for industry; the AI as the game does (`reversing/findings/FINDINGS-distribute.md`, sections 4, 6, 7) | the day's conversions (`resources.md`) | medium | no |
| `Navy.setScreenPenalty`, `setScreensPerCapitalRatio` | the poor-screens penalty's third, and screens needed per capital ship (commented out in BlackICE) | `navalcombat.md`, the poor-screens row | small | no |
| `Leaders.addTraitToLeader` | adds a trait to a leader; as a command here (not called) | `Sim/Leaders` | small | no |
| `Leaders.activateRankSpecificTraits`, `addRankSpecificTrait`, `checkRankSpecificTraitsConsistency` | traits in two states, swapped as a leader's rank enters or leaves a range (commented out in BlackICE; the maintainer keeps them off, to be cleaned up) | `Sim/Leaders` | medium | no |

### The interface or a tooltip

| function | what it does | touches here | size |
| --- | --- | --- | --- |
| `Leaders.activateLeaderListShowMaxSkill`, `...Selected` | the leader window's list, and its chosen leader, show `skill (max skill)` | `leaders.md`, section 12 | small |
| `Leaders.activateCtrlUnassignSelected` | Ctrl on the unassign-all dialog's confirm unassigns the selected units' leaders alone | the leader window's dialog, a command naming its units | small |
| `EffectTexts.activateKillLeaderVariables` | `$UNIT$`, `$LOCATION$`, `$WHERE$` for `KILL_LEADER_EFFECT` | `scripttext.md` | small |
| `EffectTexts.activateLoadOobDetails` | `load_oob`'s line says what the file would make and which leaders it takes, read off disk | `scripttext.md` | medium |
| `EffectTexts.activateTriggerScroll` | a requirement tooltip taller than the screen scrolls with Alt and the arrows | `tooltips.md` | small |
| `Tooltips.activateManpowerBreakdown` | `$LAND$`, `$AIR$`, `$NAVY$` for `MANPOWER_DETAILS_IRO` | `topbar.md`, section 14 | small |
| `Tooltips.activateCombatUnitStats` | a battle's unit tooltip adds the division's attacks, defences, toughness, armour and air attack (`BATTLE_ATTACKMOD`) | `combatview.md`, section 12 | small |
| `Tooltips.activateParadropLine` | the division designer's terrain tooltip says whether it can be dropped, and which brigades stop it | `builders.md`, section 11 | small |
| `Tooltips.activateCombinedArmsLine` | the designer's combined arms icons add the group's bonus and whether it is a base group | `builders.md`, section 11 | small |
| `BytePatches.historicalModelLogicFix` | the model a brigade is shown as: a model asking for more than the brigade has scores a flat, enormous penalty instead of the absolute difference | `units.md`, section 5, *What a brigade is shown as* | small |
| `BytePatches.seaTerrainColourInSimplifiedMapMode` | the simplified terrain map mode colours the sea too | `mapmodes.md`, section 6 | small |
| `Messages.show` | a message popup of the mod's `BICE_MESSAGE` type with text of the caller's (not called) | `messages.md` | small |

### A getter for the overlay pages

Each answers `nil` here. BlackICE reads them only for its overlay and the old utility: through `utility_data/bicedata_*`,
whose providers only those two ask, and `utility_imgui` and `utility/gameinfos` (gated).

| function | what it answers | size |
| --- | --- | --- |
| `GameInfo.getCountryFlags`, `getCountryVariables`, `getCountryActiveEventModifiers`, `getCountryGeneralModifiers` | a country's flags, variables, event modifiers with their end dates, general modifiers | small each |
| `GameInfo.getCountryOffmapIc`, `getCountryResourceConversion` | the figures of `fixOffMapIC` and `customResourceConversion`, nil until those are built | small, after them |
| `GameInfo.getProvinceDetails` | a province's pools, resources, manpower, leadership and local modifiers | small |
| `Leaders.getLeaderDetails` | everything about one leader | small |
| `Inspector.getSelectedEntity` | what the player has selected, as tables | small |

### AI territory

| function | what it does | here |
| --- | --- | --- |
| `BytePatches.disableInterAiExpeditionaries` | the AI never lends units to, or takes them back from, another AI country (`reversing/findings/FINDINGS-bicelibsites.md`, section 9) | `agents.md`, section 10.5, the AI's lending: another piece of work has the AI. Not built |

## 3. The rules built

Each holds only while the mod's Lua has turned it on; with nothing turned on, every game is as before.

### 3.1 Leaders an order of battle names

Where an order of battle file puts a leader on a unit - a new game's history (`CLeaders.SetAtStart`) or `load_oob`
(`COrderOfBattleReader.ReadInto`), never a save - and `activateOobLeaderRanks` is on: a leader **of the land**, **of the
unit's own country**, put on **a land unit**, is given the rank it takes - a division 1, a corps 2, an army 3, an army
group or a theatre 4 - raised or lowered to it, and put in service where he is not yet. A fleet, an air unit, a leader of
the wrong kind and another country's are left as they are, and the leader then goes on as before (`leaders.md`,
section 5). A new game puts every country's ranked leaders in service before any file's leaders go on, so one put in
service here is not put in again.

### 3.2 A leader's first rank

On the day a leader's history first gives him a rank, where he is not in service yet, the original puts him in service at
rank 1 whatever the line says (`leaders.md`, section 5). With `activateHistoryLeaderRanks` on, at the rank of that line,
held between 1 and 4. A later rank line is still never applied, and a leader an order of battle has already put in service
is passed over as before. The rank is read with the history (`CLeader.FirstRank`).

### 3.3 How many units a headquarters takes

The original's test: a commander of a higher kind; a theatre takes any number of land units, anything else fewer than five
(`attaching.md`; `CDeployment.MayTakeSubordinate`). Once any of the four `Units` functions has been called:

    a one-brigade division is always taken; a commander with nothing under it takes anything
    limit = for an army group, an army or a corps: the country's own figure for the level, else the default, else 5
            otherwise 5
          + what each trait of the commander's leader adds (addCommandLimitTrait)
          + the one-brigade divisions already under it, which are not to count
    taken where its land units are fewer than the limit

The country is the owner of the unit put under it. `set*UnitLimit(limit, "---")` sets a level's default; a tag sets that
country's, and 0 puts it back to the default. BlackICE sets every default to `G_BASE_COC_LIMIT`, 4, as its Lua loads, and
each country's from its technology in its daily look (section 4) - so under BlackICE a headquarters takes four until its
country's technology raises it. The AI's own ladder of commands is the AI's (`agents.md`) and is not touched.

### 3.4 War exhaustion

With `disableWarExhaustionNeutralityReset` on, the month's pass puts every country's war exhaustion back to nought in place
of growing it, so nothing is paid into neutrality at peace: the patch replaces the month's addition with a store of
nought (`reversing/findings/FINDINGS-leadership.md`, *What is not established*, settled 2026-11-07; `FINDINGS-bicelibsites.md`, section
5). The war exhaustion effect still adds until the month's pass (`warexhaustion.md`).

### 3.5 A queued brigade's historical model

A line read from a file with a brigade's `historical_model` takes the model's level of each technology of its type in the
type's order, and nothing for one the model leaves out, so the levels after it move up a place (`buildqueue.md`,
section 5). With `fixQueuedHistoricalModel` on, a level of nought for each one left out, as the same model gives a unit
placed on the map (`reversing/findings/FINDINGS-buildqueue.md`, section 10).

## 4. Calls back into the mod's Lua

| call | in the original | here |
| --- | --- | --- |
| `BiceLibDailyPeriodics(firstDay)` | once a game day, in every game of a session, from the first hour that is one o'clock or later - after the scheduled scripts of one o'clock - on the game's own thread; `firstDay` is 1 on the first call after a new game or a load. BlackICE's answers a table the DLL's overlay shows, and calls `CheckOobUnitLimitTechnologyStatus`, which sets every country's three limits from its technology on the first day and every fifth | **built**: `CBiceLibDaily`, at the end of hour 1, after the hour's AI, never while a new game is being made, where the scripts define the function (`CAiScripts.RunBiceLibDaily`). What it answers is left. Taken through `CReplay.RunCalledStep` under the name `BiceLibDailyPeriodics`, so a replay plays back the commands it posted without the Lua (`docs/replay.md`, section 3) |
| `BiceLibLuaLog(text)` | the DLL writes its own lines - "Loaded BiceLib" as it loads - into the mod's log through this | nothing to do: no DLL lines to write |
| `BiceLibGui.*`, 75 functions of `gui-imgui.lua` and `utility_imgui` | the overlay's pages call them every frame for their figures and their buttons | **not built**: there is no overlay here. Bringing BlackICE's overlay pages to this engine would be an interface piece of its own, **large**, the maintainer's to decide; until then they are read by nothing |

The same check is made a second way in BlackICE: its OMG country's intelligence minister runs
`CheckOobUnitLimitTechnologyStatus` among its scheduled scripts - the AI's step, recorded as one.

**A limit set in play is a command.** `set*UnitLimit` called while a game is asked posts a `CSetUnitLimitCommand`, and a
limit the game already holds is not posted again, so the daily look posts only what changed. An `activate` function or
`addCommandLimitTrait` called in play posts a `CBiceLibSwitchCommand`; BlackICE calls them only as it loads.

## 5. How a switch reaches a game

- **As the scripts load**, before any game, a function writes into the scripts' own `CBiceLibSwitches`
  (`CAiScripts.BiceLibSwitches`).
- **Each game made with those scripts takes a copy** as it is made - a new game, a bookmark and a loaded save alike, in
  `CGameStateLoader.BuildWorld` before anything of the game is read - and keeps it as `CGameState.BiceLib`. Nothing is
  static: a front end's scripts are made anew with each front end, and so are the switches.
- **In play**, by command (section 4).

So a saved game loaded is given the switches again the same way: **the original's save keeps none of them**, the DLL being
switched on by the same script each session, and neither does this engine's. A replay is made with the same scripts and
gets the same switches. One thing is not given back at once: **a limit set in play**. A loaded game has the defaults the
scripts set as they loaded until its first day's call (`firstDay` 1) sets every country's again, at one o'clock - as in the
original, whose DLL holds them in memory. A game saved and loaded so differs, until then, wherever a headquarters is
filled; `docs/enhanced.md`, *A whole game's replay*. `OpenHOI3.SaveCheck reload` leaves out
`CBiceLibSwitches.DailyCalled`, whether the day's call has been made, which a load starts again.

## 6. The survey

`OpenHOI3.SaveCheck modsurvey` (`mods.md`, section 5) checks every `BiceLib.*` a mod's Lua uses against this table: a
function not built, or a name the table does not have, is ours - `bicelib / Unbuilt` and `bicelib / Missing`, counted by
name; a built one, or one with nothing to do here, says nothing. The DLL is no longer a native module to it.

And it sets apart **the Lua this engine never comes to**: a `require` inside `if OpenHOI3 == nil then` or the `else` of
`if OpenHOI3 ~= nil then` (`CLuaGlobalScan.GatedRequires`), and every module reached from `autoexec.lua` only through such
a require. Their files' problems, and every use of a native module required only there - `wx` and `lfs` in BlackICE - go
under a third heading, `gated`, left out of both ours and files; the gated requires are listed under
`gated off: require`, and no longer tried on their own.

## 7. What BiceLib does that no Lua function switches

The DLL also makes autosaves of its own and a crash save, draws custom map modes, keeps a combat log, scrolls the map at
the screen's edge, and catches the game's out-of-memory and pure-call failures - configured from its own
`BiceLibSettings.ini`, not from Lua. None is asked of this engine by the mod's script, and none is built.

## Not established

- **The original's rule under `fixOffMapIC`**: what the country's `ic` figure holds when the DLL reads it
  (`reversing/findings/FINDINGS-ic.md`, *What is not established*).
- Whether the original leaves a loaded game's candidate events empty, against `events.md`, section 1 - which decides
  whether `rebuildCandidates` is a departure (section 2).
- The exact hour of the day's call: the DLL makes it on the first frame of a day at one o'clock or later, after the hour's
  scheduled scripts; here at the end of hour 1, after the hour's AI.
