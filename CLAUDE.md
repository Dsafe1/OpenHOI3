# OpenHOI3: the rewrite

*These rules are for `OpenHOI3/`. Opened from the `workspaces/hoi3` root, all four of these
files arrive at once - apply each to its own folder.*

**OpenHOI3**: a drop-in engine replacement for Hearts of Iron 3: Their Finest Hour - pointed
at an install, it plays that game, its content and its mods as they are written, by the
original's own rules, on new technology. Godot **4.7.1 .NET/mono** and C#.

**It reimplements what `hoi3_tfh.exe` does, not how it is built.** Decided
2026-10-09: a reimplementation is what the work is aimed at now, and a drop-in engine replacement is the fitting description. So the rules
of the simulation follow the original as closely as they can be read and checked; the
engine, the rendering and every line of code are our own, and no address, offset or byte of
the original's binary comes here - the section *What this repository may not do* stands as
it was. What is read from an install is the **language its content is written in** - script,
data files, interface files - because that is where the modding knowledge is. `readme.md` is
where that goal is set out for players and contributors; `docs/worklist.md` holds the staged
plan, *Next up* and *Loose ends*, and is the document a working session starts from.

**This has turned twice.** Until 2026-10-07 the rule was our own formats throughout, fed by a
one-way converter, with "our format stays clean" as the test; and until 2026-10-09 this file
and the readme said "in the spirit of the original" and "not a reimplementation". A session
reading this repository's history will find both stances everywhere; neither is the one to
apply.

**The tree is early and provisional.** What exists is one iteration - a Clausewitz reader, an
install layerer, texture decoding, the map with the start-date ownership and
terrain of its provinces, a map view, fixed-point numbers, the command queue, an hourly tick
whose daily pass works out who has which strategic resource, rebuilds each province's and each country's modifier, adds to its manpower,
works out its leadership, its IC and its home production, adds that to its stockpile and runs
its six shares of IC and its four of leadership, research among them, the unit types and
every country's order of battle with what each brigade holds on the first hour, what a
unit draws each day - supplies and fuel, troop rotation, reinforcement and upgrades - the
officer ratio and the hourly return of a brigade's organisation under it, the build queue
from an order to a unit waiting to be placed, which unit types a country may build and
what a new unit is called, a tool that
checks all of it against savegames, an interface runtime that builds windows from the
original's `.gui` files and draws their text in outline fonts, three screens on it - the
top bar, production and technology - the four windows the production screen orders a
unit from, the units on the map as counters with the alert and the menu a finished one is
placed from, the ways between provinces as the original measures them and the order, the
route and the hourly march of a land unit, the panel of a selected unit and the lines of
its hierarchy on the map, the orders that send a fleet or an air unit with the window they
are given from, the distances by sea, the buildings on the map with the port and the
airfield, and a land battle from the unit that starts it to the province that changes
hands with what the ground, the night and the rest make of each unit in it, the tactic each
side picks, and its marker and its window, and supply - which depot every
province draws on and how far from it, the day's carrying of supplies and fuel out to the
provinces, a unit drawing on the province it stands in, the convoys that carry it across the
sea and bring cut-off land's resources home, and a map mode that shows it, and a battle
between fleets at sea with its window, and a fleet's missions with the raid on
convoys, and the two days a new game runs before its first hour, and an air unit's two
fighting missions with the battle in the air, and its eight bombing missions with the
bombing and the day's repair of what was bombed, and the window of an air battle and of a
bombing, and the whole of the air orders window with a mission's area, and the air map mode
with a list of finished battles, and leaders - who is in service, who commands which
unit, how far a command reaches, what a leader does in a battle, what a battle gives him and
the window he is chosen in - and how a tooltip is shown, with a province's, the top bar's, the production and technology screens', the builder windows', the unit panel's, the deploy menu's, the orders windows', a battle's and the leader window's, and a technology's requirements and effects, and the reading of every
event and decision with their triggers and effects by the original's grammar, what their conditions answer and what their effects do, and their firing and taking with the event window and the decision list, and what each condition and effect says of itself, and the on_actions of a battle and of a pride sunk, and the messages an answered event and a taken decision raise, popped up and logged, and what two countries hold of each other - relation, threat and neutrality, and the alignment triangle threat is weighed on, a new game's relations given by the install's own Lua on the original's interpreter built from its source, and the diplomacy screen with its lists, the triangle's markers, the faction boxes, the chosen country's panel and every tooltip on them, and the AI's answer to an offer in the install's Lua, its whole `autoexec.lua` loaded and the classes the answers reach handed over as the original hands them, and the agreements' first half - an offer and its answer, alliance, NAP, guarantee, transit rights, embargo, influence and alignment with how each ends, the screen's actions and the request dialog, and their messages, and the six effects and three conditions on pacts, and trade - a route, its price, its convoy and its day, the AI's trading in the install's Lua, the trade window, the list of trades with its cancel, Automate Trade, and the bar of offers made to the player with the dialog an offer opens, and aid - debt, shared technology, lend-lease and licences, with their dialogs and the lend-lease window - and war's first piece: casus belli types and war goals, a declaration and who joins it, limited war, the call to arms and its dialog, the truce and mobilisation, the war effects and conditions, the war rows and the declare-war dialog - and its second piece's first half: peace with its row and dialog, leaving a war, the day's return of occupied land, a change of hands with consequences, the surrender progress and `CAIIntel`, the acting capital with every capital read set to the original's, annexation, the puppet, the government in exile and the capitulation, and a battle's capture changing hands with consequences, and factions - joining and inviting, leaving by script, their rules, a faction's modifier, its progress and the victory conditions - and politics: the parties and their monthly drift, the cabinet, elections, the pool of ministers and the filling of posts, national unity's day and strategic warfare, a law or a minister changed, the politics screen with its pie, its cabinet and its laws and every tooltip on them, and the AI's politics in the install's Lua for the countries nobody plays, and espionage's first piece - every country's spies in every other, placed each day by priority, their nine missions, catching them, the coup and covert operations - and what a country knows, of another by its spies and of every province, made again daily and raised hourly - and the intelligence screen with its lists, its estimates, its research, production and cabinet, and every tooltip on it, and the AI's spying in the install's Lua for the countries nobody plays - and triggered modifiers, tested each day for every country and told to the player, with the Strategic Effects window that lists them - and occupation's first piece: the policies, the one each country holds towards each with what sets it, a province's whole modifier, nationalism, and the politics screen's occupied countries with the choice of a policy, and a province's modifier made again on the original's schedule - and revolt risk: suppression, the day's step, the figure read, its four conditions and effect, and its map mode - and the rebels: the roll, a rising and its army, who a rebel army takes land for, a faction's day and month and independence with the release - and the undergrounds: what a finished one starts at, its day, partisans, the spread and Organized Revolt - and buildings in the build queue: the confirm-build dialog, the price, a finished level the repair fills and a building placed from the deploy menu - and the province view, which follows the selection, with its figures, its weather strip, its building rows, its underground, its covert operations and the allied objectives - and convoys in the build queue: transports and escorts ordered from the confirm-build dialog, priced once and never again, ten ships an item to the free pool - and weather: fronts from the low pressure zones, high pressure, every province's hour, the seasons and a new game's 96 hours, with the weather map mode, its pictures and its tooltip, and the player's settings read from the original's settings.txt - and transport by sea: a land unit put aboard a fleet by the panel's load button or by marching into its sea, a fleet's room by its ships' transport_capability, passengers sailing with it, sharing its transports' damage and lost a brigade at a time when it can no longer carry them, getting off in port, and the landing - 24 at the landing's speed, aboard all the while, with an attack and a defence scale by its progress set beside screenshots of the original figure for figure - with the transport and invasion missions, the INVADEDUS warning, a fleet's list of what it carries and a landing unit's battle tooltip - and the carrier and carrying by air: a carrier as an air group's base with the rebase to carrier and carrier protection, which a fleet in a battle sends in, a land unit aboard aeroplanes by the panel's plane button, the paradrop with what a dropped unit fights at, and the air supply, each tried with `OpenHOI3.SaveCheck airborne` - and selection: a drag box that takes the player's land units, else its fleets, else its air units, Shift that adds a unit or takes it out, Ctrl and the single panel's ribbon that bring a command, a right click for every selected unit, the multi-unit panel with a row for each, and the digits' selection groups (`docs/mechanics/selection.md`) - and stage 2's headless run, 1936 to 1948 with nobody playing (`docs/mechanics/headless.md`), with the four things it found built - and stage 5's first piece, the tech and production ministers: the install's Lua researching, setting the sliders, ordering units, buildings and convoys for every country nobody plays, and the engine placing their finished units (`docs/mechanics/ministers.md`) - and its second and third, decisions and the whole foreign minister: the install's `ForeignMinister_Tick` for every country nobody plays - its decisions, calls to arms, transit rights, wars proposed, pacts, influence and alignment, through the thirteen diplomatic action classes the scripts make - the day's best proposal, and the wars a script prepares, declared or their decision taken when ready as far as readiness is built (`docs/mechanics/foreignminister.md`) - and its fourth, the AI's strategy: each country's personality and what it makes of every other - threats, antagonism, rivals, friends, puppets wanted, countries protected, provinces to take and hold - by the engine's terms and a walk of its faction's victory conditions, made as a game is entered, every ten days and at a change of war, every list the start save's for all 78 countries (`docs/mechanics/aistrategy.md`) - and the military AI's first piece, fronts and theatres: every area's fronts and a country's by the enemy faced, its theatres made, merged, kept by overlap and looked after each hour, and each one's headquarters adopted or made, all 108 the start save's, with readiness for a prepared war answered over the fronts (`docs/mechanics/theatres.md`) - and its second, the unit agents: each country's order of battle sorted into its theatres at its first pass, an agent bound to each theatre's headquarters building the ladder of commands under it a level a day, fleets and air groups split and merged, headquarters walked to their places, divisions balanced between theatres, and each theatre's plan - stances, powers, objectives, area of operations, aircraft - every plan's stances, area and objectives the 4 January save's for all 106 theatres (`docs/mechanics/agents.md`) - and its third, moving and attacking: each agent's divisions committed or held back, garrisons, a line of at most five a position and a reserve, the attack by the AI's own odds with the support attack and the strategic redeployment as orders, the axis, the sea targets, the fleets' hour, the sealift's embark and debark, invasions, the movers of fleets and transports, the AI's leaders and its stranded units, and the air - its groups rested, rebased and sent on close support, intercepts and strikes, the save's three AI air orders made again at their stance and province, an hour early - 774 of 908 divisions moving or staying as the 4 January save's and its fleets 221 to 222 (`docs/mechanics/moving.md`) - and a replay, the AI's commands recorded and played back in its place with the state hashed day by day (`docs/replay.md`) - and the outliner down the right of the screen, its nine sections and their switches, the tree of units and the battles beside them (`docs/mechanics/outliner.md`) - and the land unit's orders window on Ctrl and a right click, with the support attack and the strategic redeployment as the original's and a unit's two order hours (`docs/mechanics/landorders.md`) - and the reorganise window, two units handing brigades between them by the original's tests of what may move (`docs/mechanics/reorganise.md`) - and the merge button, units made one into the one its score picks, the more skilled leader kept, the AI's join orders merging the same way (`docs/mechanics/selection.md`, section 5.4) - and the single panel's new-unit button, an empty unit beside the selected one with the reorganise window between them, or with Ctrl a unit for every brigade but one (`docs/mechanics/unitpanel.md`, section 7.1) - and the in-game menu on Escape and the top bar's button, its pause, Quit, and the message settings with the player's `messagetypes_custom.txt` read and written as the original's, Resign greyed (`docs/mechanics/ingamemenu.md`) - and the game options window, its four tabs, Apply writing the player's `settings.txt` in the original's 21 keys and format and Back, the gamma live as a brightness layer (`docs/mechanics/gameoptions.md`, `settings.md`) - and the loader of the original's `.hoi3` saves, its first increment: `--load=`, the clock, the generator restored, the played country, every province's and country's figures and the factions, what is not loaded yet cleared and reported (`docs/mechanics/savegame.md`) - and its second, the units: every tree with its brigades, orders, plans, carriers' air groups and passengers, the leaders in service, the queue's lines, what waits to be placed and the templates (`savegame.md`, section 5) - and its third, the wars, the pacts, the trade routes, the convoys and mobilisation (section 6), and the battles and bombings being fought, the finished battles and the sunk ships (section 7) - and the writer's first increment, the original's layout with the top level's head, every province, each country's figures and the factions (section 8) - and its second, each country's military part with every number given back as the save's pair (section 9) - and its third, the pacts in the order they were made, the wars, the convoys, the battles, the finished battles and the sunk ships (section 10) - and saving in play, the menu's save dialog and the autosave at the settings' frequency (section 11) - with typing into the save dialog's edit box as the original's takes it (`interface.md`, section 8) - and the fourth increment's first piece, each country's politics and research queue both ways (section 12), and its second, each country's spies (section 13), and its third, the top level's counters, flags, victory conditions, fired events, fronts and faction progress (section 14), and its fourth, exile, the goods pools, battles fought, strategic warfare, alignment and lend-lease (section 15), and its fifth, the AI's strategy and theatres, kept on a load as the original keeps them (section 16) - and the front end, the original's main menu and its lobby pared down to the one start: the country chosen on the map, a save loaded the moment its row is chosen, Play and Back, and Resign back to the main menu (`docs/mechanics/frontend.md`) - and the lobby's options: the difficulty with its static and combat modifiers, the game mode greyed, and the areas a player hands to the AI, each giving his country that minister, saved and loaded (`frontend.md`, section 4; `savegame.md`, section 17) - and in play, the country screens' automation arrows with what each screen takes from the player as the original's and greyed where the original's only stops the mouse (`docs/compatibility.md`, entry 19), the slider automation setting the player's sliders each day and the automatic-deployment box (`frontend.md`, section 4.4) - and the original's effect files translated to Godot shaders, every pass of the install's and BlackICE's compiling, for the 3D renderer and the 2D canvas, the buttons, icons and scrollbars drawn through `buttonstate.fx` (`docs/mechanics/shaders.md`) - and the map's land, beaches and sea drawn as the original draws them, from the install's quad cache through its `terrain_2_0.fx` and `water.fx` as translated, under the original's camera made to agree with the 2D view (`docs/mechanics/mapdraw.md`), the land borders through the cache's border meshes, and the far view's country names laid out and bent along each country's land as the original lays them (`docs/mechanics/mapnames.md`), the sea zones' borders and the selected province's highlight, the original's map cache found where it finds it and refused when its stamp is not the map files', and the rivers and the surf as the original makes them, an edited river bitmap traced from its own sources, the trees as the original plants them and the province names as it writes them, the game's generator seeded as the original's, and the minimap with its sixteen buttons and every map mode the original has, each with its part of the province's tooltip (`docs/mechanics/minimap.md`, `mapmodes.md`), and war exhaustion - a new game's by the calendar months, the month's banking and its payback to neutrality at peace (`docs/mechanics/warexhaustion.md`) , and the fog of war on the map - a stack, a counter and a building by what the player knows (`docs/mechanics/mapunits.md`, sections 3 and 9), the zoom buttons by the minimap, and the editor's continent painting on a debug switch, and the tooltip over a counter, every unit at its place by type with strength bars (`tooltips.md`, section 7.1), and the fog of war's switch on the debug menu, and the map cache read from the player's Documents folder alone and built there from the map files where it is not the map's, every file the original's byte for byte (`docs/mechanics/mapcache.md`; `docs/compatibility.md`, entry 22), and the end of a game: the dialog, the end date and a player left with no land, the end-of-game screen, the victory conditions' window and alert and the lobby's choice of them (`docs/mechanics/endgame.md`), and the theatres screen with what a plan is short of and its Build (`docs/mechanics/theatresscreen.md`), and the Statistics screen's 26 pages of tables and pies (`docs/mechanics/statistics.md`), and the Strategic Warfare Manager with the records of each war, their tallies, the window and its charts through the install's `linechart.fx` (`docs/mechanics/strategicwarfare.md`), and the convoy windows - the production list, a convoy made from two lists of ports, its ships and goods and the three automatic flags - and the go-to box (`docs/mechanics/convoywindows.md`), and the order of battle browser with its dragging (`docs/mechanics/oobbrowser.md`), and a brigade's card, a ship's sunk list and the pride of the fleet (`docs/mechanics/brigadecard.md`), and attaching, detaching and making a headquarters from the unit panels (`docs/mechanics/attaching.md`), and battle plans - the window, the historical and saved files, the saved game's and the tools that draw them through the install's effect files (`docs/mechanics/battleplans.md`), and the diplomacy panel's war goals and influences, the battle-start and election popups, Disband, the production screen's theatre needs and the air builder's description, and what every message's Goto does (`docs/mechanics/messages.md`, section 4.1), and the picture, label and description of what a builder window has picked, and changing a brigade's type - the order that disposes of it, its line, the panels' lists and the row of one under way (`docs/mechanics/upgrades.md`), and the tooltip over a port and an airfield (`docs/mechanics/tooltips.md`, section 7.2), and the console with the commands its table rates easy and medium, each a replayable command, `tag` making every screen again around the same game (`docs/mechanics/console.md`), and a headquarters handed to the AI with its stances, powers and needs on the panel (`unitpanel.md`, section 9) and its dashed front line, objectives and arrows on the map, a right click setting them (`mapunits.md`, sections 11 and 12), and making a puppet and liberating a country (`politics.md`, section 11.1), and an expeditionary force lent and taken back (`docs/mechanics/expeditions.md`), and the sixteen alerts (`docs/mechanics/alerts.md`), a message type's settings on a right click (`messages.md`, section 4.2) and a ship technology's levels by name, and a NAP's balance of brigades along the border (`docs/mechanics/agreements.md`, section 6), and the picture behind the main windows (`countryviews.md`, section 6), and the nuclear bomb with `province_event` (`docs/mechanics/nukes.md`), and the base game's remaining script - `load_oob`, `release_vassal`, `create_alliance`, `surrender_inherit`, the port conditions and the undeclared war - with tribute and the AI's Lua on declaring war, puppets and liberation - and the music, the loading screen with the map modes prepared under it, and the standalone export that plays the install it is placed in (`docs/mechanics/music.md`, `loadingscreen.md`; `tools/build_engine.ps1`), and a large font drawn from its own glyphs as distance fields (`interface.md`, section 6), and multiplayer - a host and its clients on this engine's own networking, the AI always the host's, the shared lobby, several players of one country and joining a game that runs (`docs/multiplayer.md`, sections 5 to 14; `OpenHOI3.SaveCheck twomachines` and `joinmachines` run two machines' games side by side) - and what 2026-11-21 added: a unit where its country may no longer go, a trespasser (`war.md`, section 16), a shattered unit re-formed, a removed unit's officers and what it commanded (`units.md`, section 9), the air unit's reserve order (`fleetsandair.md`, section 5.1), a resilient rebel faction's weighing of the odds, a rebel unit worn to nothing, the licence dialog's list by the two countries' combat value of a type with its row's figures (`aid.md`, section 6), a new game's ministers drawn as a load's and the AI's one pass before the first hour (`ministers.md`, section 1), the green ring and the green name of a unit an AI commands (`mapunits.md`, section 8), every picture code gives an icon drawn through its picture sprite (`interface.md`, section 5), the front end's credits (`frontend.md`, section 10), a message written on the map (`messages.md`, section 6), and the unit AI's countries run side by side (`agents.md`, section 12) - and the enhanced mode's skeleton: `common/openhoi_enhanced.txt`, the rules a game carries in its hash, checksum and save, an enhanced save's folder, and one fix (`docs/enhanced.md`, *What is built*; `docs/blackice.md` the plan for BlackICE's release on it) - 3840 tests - and it is
being reconsidered rather than extended. Do not treat the current contents of `lib/` and `src/`
as settled, and do not preserve a structure merely because it is there.

## Six projects, and which way they may reference

Split on 2026-10-07, so that the simulation can run headless and under test; the fourth
was added on 2026-10-08 with the interface, for the same reason, the fifth on
2026-10-12 with Lua, and the sixth on 2026-11-18 with the launcher (`docs/launcher.md`):

| project | what it is | may reference |
| --- | --- | --- |
| `lib/OpenHOI3.Sim` | the model and the simulation | **nothing** - not Godot, not `Content` |
| `lib/OpenHOI3.Content` | reading an install: the Clausewitz format, layering, localisation, the loaders, the `.gui` declarations | `Sim` |
| `lib/OpenHOI3.Interface` | what the screens show, with no engine: the text of each figure as the original writes it, colour escapes, where an element goes | `Sim`, `Content` |
| `lib/OpenHOI3.Lua` | the install's Lua scripts on LuaJIT's interpreter built from `native/luajit` - or, behind a switch, the original's Lua 5.1.4 from `native/lua` - and the classes they are handed | `Sim`, `Content` |
| `OpenHOI3.csproj`, with `src/` | the Godot client: scenes, drawing, input, texture decoding, typefaces, the live interface | all four |
| `launcher/OpenHOI3.Launcher` | the launcher, in Avalonia: lists the mods and starts the engine a choice is played on - what it decides is `Content/Launch`, under test | `Sim`, `Content` - **not Godot, and never an engine at run time**: it speaks to one by `CLaunchContract`'s command line alone |

**The simulation asks the Lua through an interface of its own**, `Sim/Ai/IAiScripts`, which
`OpenHOI3.Lua` answers and the caller hands to `CGameStateLoader.LoadStart` - so `Sim` still
references nothing. **Lua code runs only inside a protected call, and a C# function never
raises a Lua error itself** (the trampoline in `native/lua/ohoi` does): a Lua error is a
`longjmp`, and one through a .NET frame is undefined. `native/luajit/build.ps1` builds
`openhoi3_lua.dll`, LuaJIT, and `native/lua/build.ps1` `openhoi3_lua514.dll`, the original's Lua
5.1.4, both with the Visual Studio C++ tools on `dotnet build`; they are built output and never
committed (`docs/mechanics/lua.md`, `native/luajit/readme.md`, `native/lua/readme.md`). **LuaJIT is
the default since 2026-11-19, and `pairs` walks a table in its order, not the original's**:
`OPENHOI3_LUA=5.1.4`, the client's `--lua=5.1.4` or the tool's `lua=5.1.4` runs 5.1.4 instead -
use it for anything set beside a run of the original's (`docs/compatibility.md`, entry 30).

**A screen is two halves.** What it says is a class in `OpenHOI3.Interface` - `CTopBarText` -
tested against the original's own strings; the class in `src/Screens/` finds the elements by
the original's names and sets them. Keep doing that: a format or a colour that lives only in
the client can only be checked by looking.

**Nothing under `lib/` may reference Godot** - not for a `Color`, not for a log call. That is
the whole point of the split, and one `using Godot;` there undoes it: the tests stop being a
plain .NET run. `CColor` and `System.Numerics.Vector2` stand in for the engine's value types,
`src/Gfx/CEngineTypes.cs` converts at the edge, and `CDiagnostics` holds a sink the client
points at Godot's log on startup.

`lib/`, `tests/`, `tools/` and `launcher/` each carry a `.gdignore`, so the editor does not import their sources or
write `.uid` files beside them. The client's project file removes them from its own
compile glob for the matching reason - `Godot.NET.Sdk` would otherwise compile them twice.

## Settled decisions, which are not merely unbuilt

Nothing should be designed around these coming back.

- **The campaign is the one way into a game: no custom game starts, no isolated scenarios.** The original
  also offers both; leaving them out means one kind of start to balance and one code path into the simulation, and a
  scenario's own map extent and victory conditions would be a second simulation to keep. So nothing here reads
  `scenarios/` (`docs/compatibility.md`, entries 1 and 17). ~~No bookmarked starts~~ - **bookmarks are built** since
  2026-11-15, by the decision of that morning ("put the bookmark support on the loose end. Scenarios should
  still be considered out of scope"): the lobby lists every historical bookmark of `common/bookmarks.txt` and a click
  makes the world afresh at its date, and the Quick Start panel's four buttons start their games
  (`docs/mechanics/bookmarks.md`; `docs/compatibility.md`, entry 27). **The defines' `start_date` stays
  `CGameState.StartDate` at any bookmark** - the clock is the bookmark's - so a rule that means "the game's first day"
  asks the clock, and only the technology head start and the date widgets ask the defines' date.
- **An install is read as it is. There is no converter and no format of ours to author in.**
  - *Script is loaded natively* - events, decisions, triggers, effects, technologies, units,
    laws, ministers, history - in the original's syntax with the original's keywords, so a
    mod's file is a valid file here. **Be stricter than the original where possible:** a key
    nothing recognises is reported, where the original drops it silently. **Events and
    decisions are read so since 2026-10-12** (`docs/mechanics/script.md`; `Content/Script`,
    `Sim/Script`), and their conditions answer since the same day (`docs/mechanics/triggers.md`,
    `CConditions`) and their effects run (`docs/mechanics/effects.md`, `CEffects`), and events
    fire and decisions are taken (`docs/mechanics/events.md`, `Sim/Events`), the on_actions of a battle and a pride sunk among them (section 8), and each condition and effect says itself in words (`docs/mechanics/scripttext.md`, `Interface/Script`), and an answered event and a taken decision raise their messages, popped up and logged (`docs/mechanics/messages.md`) - with **the AI's
    choice of an option drawn on the game's own generator**, where the original seeds a fresh one
    with the clock, by deliberate choice (`docs/compatibility.md`, entry 11); keep it so,
    because a game here is replayable from its seed and its commands. **A condition about something the simulation does not hold answers false, and an effect
    on it does nothing, and both are reported as unbuilt** - never answered from the history
    files or any other stand-in, by the decision of 2026-10-12: that "would just be
    adding a system that is wrong and would be removed later on anyways". And an event that holds
    only because of one fires: "Just fire them." Build on what the simulation holds, and take a
    keyword off `CUnansweredConditions` or `CUndoneEffects` when its system arrives. Flags,
    variables and the modifiers an event gives are script's own state and are built. What the
    reading keeps of the original's,
    though it looks wrong - do not tidy it: a name is matched case and all while a keyword is
    not; a key that is no keyword is tried as a name in the original's order, so `industry`
    is the building before the technology; a tag is a text's first three letters, matched
    without case only within the original's bucket; `PHI` and `PAN` are never scopes; a
    `limit` inside a scope does nothing and the effects beside it run; an `ai_will_do`'s
    factor is whole days. Everything is reported, the original's silent drops included.
  - *Heavy assets get a cached build* - textures, fonts, and whatever else is measured to be
    slow - into our own formats, rebuilt only when a source file changes. **Measure before
    caching:** the province bitmap was assumed to need this and builds in about 120 ms, so it
    is read every start (`docs/mechanics/map.md`, section 6). The cache is derived data: never
    authored, never committed.
  - *The interface is loaded from the original's `.gui` files* and laid out by our own
    runtime. See the next section.
- **The base game first, mods second.** Their Finest Hour alone is the target until it works;
  BlackICE then becomes a third layer over it.
- ~~**The AI does not touch the player's country.**~~ **Reversed 2026-11-02**: the player's country starts as the
  original's does. Decided 2026-10-08 ("Yes it should leave the players history laws"), on the reading then that the
  AI's Lua alone moved every country's laws before the first screen; the reading of 2026-11-02 found the engine's own
  new-game pass moving every country's laws and buying every country a head start in technology, the player's too, and
  the decision was "Yes everyone" and to drop the entry. So a new game brings every country's laws forward and
  buys its levels (`docs/mechanics/politics.md`, section 13; `research.md`, section 9), and the Lua's opening look at the
  laws runs for the player's country as well; the politics minister is still only the AI's countries'.
  `docs/compatibility.md`, entry 6, struck through.
- **The map always shows counters: `alwaysCounters=no` is not supported.** Decided 2026-10-15: "'no'
  means to use sprites, but no one uses them and they are also not easily moddable. So they are more or less a dead
  feature." So no unit figures, and the weather map mode draws only the `_simple` icons; a `settings.txt` saying `no` is
  reported. `docs/compatibility.md`, entry 12.
- **The player's settings are the original's `settings.txt` in the Documents folder** - "not the one next to the exe" -
  read ~~and never written~~, and **written by the game options window's Apply in the original's format**, by the
  decision of 2026-11-04 (`docs/mechanics/settings.md`, `Content/Settings`; `docs/compatibility.md`, entry 13).
- **Fixed-point numbers in the simulation.** Thousandths, as the original stores them, for
  determinism and for rounding the way the original does. No `float` or `double` in game state.
  The original has a second kind, with 15 binary places, for shares of IC and of leadership;
  it is `CFixed15` here, and a rule worked out in one kind is never computed in the other
  (`docs/mechanics/numbers.md`).
- **Every change to the game state is a command.** Nothing mutates the simulation directly;
  that is what makes a run replayable and, later, shareable between players.
- **A game is an object, not the process.** The plan starts at the map and reaches the main
  menu later, so nothing may assume the world is loaded once at startup and lives until exit.
  No static game state anywhere under `lib/`; the client creates a session, owns it, and can
  destroy it and create another. That is what "new game", "load" and "exit to menu" each need,
  and it is cheap now and a rewrite later. Start-up goes through a boot scene, which brings up the
  front end - the main menu and the lobby - since 2026-11-06, takes the game Play makes from it as a
  session, and brings a new front end back when the game is resigned (`docs/mechanics/frontend.md`).
- **The client's hours run beside the frame, and nothing reads the game while they do.** Decided
  2026-11-18, for a map that stays smooth under BlackICE's hours (`docs/mechanics/tick.md`, section 2):
  after a frame is drawn the hours due start on a worker (`CSimulation.BeginAdvance`), and until the first
  frame after they are done (`Collect`) the HUD and the tooltips are not processed, the map's layers keep
  what they drew (`CHeldRedraw`), the map moves only its camera (`CMapView.GameBusy`), every event but the
  camera's is held and handed out afterwards (`CSession._Input`), and the state's events reach the screens
  through `CSimulation.OnMain`. So **a new node that reads the game goes under the HUD, or asks
  `CSession.GameBusy`; a handler of a state event shows what it made through `OnMain`; and a map layer that
  reads the game as it draws asks for redraws through its `CHeldRedraw`** - anything else reads the game as
  the worker changes it, which is a crash that comes and goes. **At the top speed the worker runs hours back
  to back** (since 2026-11-19), stopping after an hour that turns a day, ends the game or tells through
  `OnMain` with `mayStopTheClock` - so **a handler that may pause the game, as a pausing message does, passes
  it**, or the clock stops some hours late.
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

**Built 2026-10-08, as far as three screens and four windows need**, and on 2026-10-09
the deploy menu and its alert, the panel of a selected unit, the window of orders a
fleet or an air unit is sent from, and the window of a battle.
`docs/mechanics/interface.md` is the spec, and `topbar.md`, `countryviews.md`,
`sliders.md`, `productionscreen.md`, `technologyscreen.md`, `builders.md`,
`mapunits.md`, `unitpanel.md` and `combatview.md` the
screens', `tooltips.md` how every tooltip is shown, with `movement.md` for the right click that orders a land unit and
`fleetsandair.md` for the one that asks a fleet or an air unit what it is to do;
`lib/OpenHOI3.Content/Gui` reads the files, `src/Gui` builds windows, icons,
buttons, checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements
from them, and `src/Screens` holds the screens. **The unit counters are not interface
elements**: they are declared in an `objectTypes` block of a `.gfx` file, read by
`lib/OpenHOI3.Content/Map`, worked out and painted in `lib/OpenHOI3.Interface/Map` and
drawn on the map by `src/Map/CUnitCounters` - `mapunits.md`. They and the deploy menu were
checked with `--click`; they were reviewed in play on 2026-10-09 and the near counter
is laid out from a screenshot of the original's. **A land unit's march -
`movement.md` - was checked with `--click` too, and has been reviewed in play**; its
distances agree with the original's own cache edge for edge and its speed with savegames
hours apart, division by division. The hour of an arrival rests on the reading. **The unit
panel - `unitpanel.md` - was built from a reading and checked with `--click`; it was set
beside the original on 2026-10-09 and its bars were wrong twice**, under two guesses at
how a rotated icon turns - the second one "confirmed" by a fit to the art that was a
coincidence. A screenshot of the original gave the rule, and two more: the panel's entries touch, and
where an edit box puts its text (`interface.md`, sections 2 and 7). All three are measured
on that screenshot and none is read. **Fleets and air units - `fleetsandair.md` - and the
buildings on the map were built from a reading and checked with `--click`, and have
not been reviewed in play.** Their sailing, their routes and the whole table of distances by sea
agree with the reference saves and the original's cache; the size of a building's picture and the
orders window rest on the reading and on the art. **The art corrected one rule on the
way**: a window's background that is a plain picture keeps its own size at the window's
corner and is not stretched - no window built before had one (`interface.md`, section 3).
**A land battle - `landcombat.md` - is the least checked thing here: read out of the
original, tried with `--click` and with `OpenHOI3.SaveCheck battle`, and set against
nothing.** No save had a battle in it then (several have since 2026-11-05, `savegame.md` section 7) and the original's rolls cannot be made again without its generator's whole state, so its
figures rest on the reading alone. The review of 2026-10-09 covers five things:
a week for two like divisions "seems right", an attack delay stops all movement but the walk
into the province attacked, occupied ground is striped (`map.md`, section 10), units that
attack together fill the front until the width is used, and one that joins later waits in
reserve and comes up by its chance. The order the first hour puts units on a front in, and
the score between two reserves, are still the reading's alone. Only
land units fight. Fifteen things bear on a unit each round - the ground, a river, forts, the
night, experience, being dug in, crowding and the rest (`landcombat.md`, section 4) - and
`CCombatModifiers` is where those that wait will go: shore bombardment and
the others listed there. **Each side picks a tactic** out of `common/combat_tactics.txt`
(`landcombat.md`, section 11), and with a battle's leader not built it is nearly always the
plainest.
**Reading for this corrected the fact base twice**: the weather
and the night are against a unit's attack and leave its defence alone, and of a tactic's two
numbers the one written down as what counters it was its own. There was no weather until 2026-10-15 (`weather.md`),
and at the time no diplomacy and no AI, so a war was given with `--war=GER:POL` and the enemy stands
still.
**A battle on screen - `combatview.md` - was built from a reading, checked with `--click`,
and then set against three screenshots of a battle in a reference game; ours was reviewed in play on 2026-10-09
and found good, with one thing asked for: opening a battle's window does not move the map**
(`docs/compatibility.md`, entry 8). It shows the marker - a land battle's alone, and from its
fourth hour - the row in a unit's panel, the window. Its upper half lies on the original's to
within a pixel, after the screenshots corrected four things about how the interface draws,
all measured and none read (`interface.md`, sections 5 and 7): **a progress bar's picture
is drawn upside down** - one of the unit panel's two bars had been the wrong way round
since it was built - **a turned flag turns about its own middle** where a turned bar turns
about its corner, **a round shield cuts its flag to a disc**, and **a name too long for a
`fixedsize` box ends in three dots**. The number on the marker, the bar and the marker's
colour are one figure, the attacker's share (`combatview.md`, section 6), and it read 6 on
the original's screenshot and on ours for the same kind of fight.
**The supply network - `supply.md` - is the best checked thing read out of the original
here**: which depot every province draws on, worked out from areas, groups of areas on one
side and a score, agrees with three savegames province for province - and the original
was then watched moving the depot of Manchukuo and Korea as one Korea and the other were released,
and the score gives each state seen (`supply.md`, section 9). **The
original takes some days to group a released puppet with its master and this does it at
once**, because the network here is made whole each time it is asked for - kept so by
decision (`docs/compatibility.md`, entry 9). The account of the original's behaviour given before
the reading - a puppet is in its master's network unless the two do not touch, allies keep
their own - was read exactly so. The areas it makes are also what home production, the
`overseas` modifier and the placing of a unit ask, each of which had guessed at them.
**The daily pass - `supply.md`, sections 11 to 17 - is the second of supply's three pieces,
built on 2026-10-10, and it reproduces the original's own figures**: with a save's units,
laws and production, a day, the filling of the provinces and a day give 184 of 234 networks
the same in every province and every figure the save keeps, Germany's 245 provinces and
Sweden's 210 among them (124 before the convoys, 171 before what leaders do to a unit's use). **It is written to do what the original does step for step, its oddities with it** -
the order its sort leaves equal provinces in, a share that stays cut, fuel tested by today
where supplies are tested by yesterday - because who goes short on a full road is decided by
nothing else; do not tidy them. A unit draws on the province it stands in, a marching unit
takes fifteen days of supplies with it, and a day's supplies and fuel are made where the
factories are and travel to the stockpile a province a day. **A new game fills its provinces
when it is made**, which takes about two seconds, between the two days it runs before its
first hour.
**Those two days - `tick.md`, section 6 - were built on 2026-10-10 and are checked like for
like**: a new game runs what the original runs as a game is entered - a day, the filling, a
day, a month's pass, one hour, with the clock standing still - as `CTick.RunFirstDays`, and
all of it with `IsBeingMade` set, which is the original's "no game is running yet": a
brigade may then have its own figure of organisation and nothing else, the month decays
nothing and a convoy is manned for nothing. Given the laws Germany's days were run under,
it is the start save's Germany brigade for brigade and good for good, supplies 0.030 apart.
**A plain new game is not the original's first screen and is not meant to
be**: there the AI has changed laws by then, and here the player keeps his history's
(`docs/compatibility.md`, entry 6) - so nothing holds a reserve down before the first hour
played does, and one sent to attack before that hour fights above its ceiling. Decided on
that, 2026-10-10: "The two days for the brigades is ok for now. Maybe it comes up again
later" - leave it. **The savegame tool's older checks load a game with `firstDays: false`** and
reason from day nought; leave them so, they are how each rule was pinned down.
**Convoys - `convoys.md` - are the third piece, built on 2026-10-10, and the best checked
thing here after the network**: a new game's convoys are the start-date save's, all 261 -
the same runs, the same way province for province, the same transports and escorts on each,
the same free ones for every country. So the route search and **its heap, down to which of
two equal entries comes out first**, are the original's, and so is the order of an area's
harbours - **largest naval base first**, which is what "an area's first harbour" means
everywhere; do not tidy either. A convoy lands in harbours and the daily pass carries it to
the depot; what cut-off land yields waits in its block's largest harbour. **Everything lying
in cut-off land is moved there each day, and a province's pool is also the stockpile of any
country whose capital it is - so the goods of a country that does not exist are shipped to
whoever holds its capital.** That is the original's, shown by the save, and the decision
on 2026-10-10 was to follow it; it was first built with the yield in a pool of its own
and the harbours' money came out wrong. With it go two more things the save bore out:
consumer goods and supplies are charged whatever the stockpile holds, and a day ends by
holding every good in a land province between nothing and 99999. Nothing attacks a convoy,
none was built until the convoy lines of 2026-10-15 (`convoylines.md`), and trade and lend-lease have none. **The supply map mode - `supplymap.md` - was read branch by branch and
looked at once with `--mode=supply`**; the button `mapmode_8` by the minimap turns it on (`minimap.md`), and
the tooltip of the province under the mouse carries the original's lines. There is no
intelligence, so it shows the player's own land. Supply was reviewed in the
game on 2026-10-10 and nothing was raised against it.
**A battle at sea - `navalcombat.md` - is the first of five pieces air and naval combat was
cut into on 2026-10-10, and like the land battle it was read out of the original and set
against nothing**: no save had one then. **The cut is this project's and has not
been decided on** - the go-ahead was given, not the how: fleet against fleet; a fleet's missions with the
raid on convoys; the battle in the air; bombing; and all of it on screen. A fleet at war looks
each hour at the first enemy fleet in its sea province and rolls to see it; each ship then
stands at **a distance of its own**, picks a target by its side's positioning, shoots, and
closes or opens the distance; a side whose ships average under one organisation has lost and
runs for its port; and a battle nobody loses ends when an hourly roll has set the fleets
counting and passes again. **It is written to roll in the original's order, its oddities
with it** - a ship that has lost the enemy shoots at its own side seven times in a hundred,
and the walk that picks which friend ends on the side's last ship; a transport, having no
target, runs in at full speed; a crowded fleet's own penalty leaves it hardly ever finding a
target at all - do not tidy them. **An idle fleet of a country nobody plays starts no battle**,
which is the original's and is why `CGameState.Played` exists; with no AI the enemy's fleets
never leave port, so `--sail=ENG:10531` sends one to sea for a trial by hand. There were no
leaders in it until 2026-10-11 (`leaders.md`, section 10). Reading for it corrected the fact
base's record twice.
**The sea battle on screen - `combatview.md`, section 10 - was built on 2026-10-10 from a
reading and checked with `--click`; it was reviewed in play that day - "The sea battle
window looked perfect"**: the land
battle's row and window, the window with halves of its own (`CNavalSideView`,
`CCombatShipEntry`) - each side's positioning and a list of its ships, **each ship's picture
standing along its row by the ship's distance**, forty and more at the far end. No picture of
the original's was measured against it, so it has a review by eye and no measurement. **It had the
land battle's marker on the map when it was reviewed, and has none now**: the marker is a land
battle's alone in the original, and from its fourth hour (`combatview.md`, section 8) -
found later that day, reading for the air side, which corrected a sentence of the fact base.
**That has not been raised for a decision**; `CCombatViewText.HasMarker` is the one place that says so.
**A fleet's missions - `navalmissions.md` - are the second piece, built on 2026-10-10 and,
like the battle, read and set against nothing**: reserve, patrol, intercept, sortie, the raid
on convoys and their escort; **transport and invasion are built since 2026-10-15** with carrying (`transport.md`).
The order set that day was fleet missions first, then the combat screen, then the new
game's two days - so the sea battle's marker and window came
next, ahead of the air, and the new game's two days after them; all three are built. A mission is an order asked
every hour whether it is still going, and that asking is where it does everything; all of
them send a **player's** fleet too worn for its stance to the nearest harbour, and nobody
else's. **A raid on a convoy is a battle**: the convoy's two counts become a throwaway fleet
of `destroyer`s and `transport_ship`s and five rounds of the sea battle are fought on the
spot, the raider shooting with `convoy_attack`. **Six things in it are the original's and
look wrong, and are built so - do not tidy them; `navalmissions.md`, section 12, lists them,
and the decision on them, 2026-10-10, is "All those things are correct, and they may
be bugs, but thats ok"**: an order that has told its fleet to return to
base never takes that back, so **an interceptor goes out once** and then lies in port under
its order; **a patrol sinks convoys** every twelve hours like a raider; a sortie ordered
from anywhere but its base is left with a path that does not begin where it is. **How well a
country knows a province is read whole since 2026-10-14** (`docs/mechanics/intelligence.md`):
made again once a day - 9 for the holder and its friends and for a foreign unit's owner on land,
2 at sea and 9 for a patrol shadowing somebody, 3 and more on the land beside, and radar - and
**raised at the end of every hour for the provinces units left or entered**, which is how a
fleet in port sees without rolling. The view on 2026-10-10 had been that it
is daily, before that hour's raise was read; once it was, the decision on 2026-10-14 was "Keep it as read."
There is no AI, so `--mission=ENG:patrol:10531` gives a country's fleet a mission for a
trial by hand. Reading for it corrected the fact base's record five times - the plainest
that the raid's caller the record had under the raid order is the patrol's.
**The battle in the air - `aircombat.md` - is the fourth piece, built on 2026-10-10, read
out of the original and set against nothing, and it has not been reviewed in play**:
air superiority and the intercept as missions (`CAirMissions`, `CAirSuperiority`,
`CAirIntercept` in `Sim/Missions`), and the air battle (`CAirBattle`, `CAirFire`,
`CAirModifiers`, `CAirRetreat` in `Sim/Combat`). An air mission is an order asked every hour
whether it is still going, and outlives its sorties: home under "return to base" the unit
flies again until the order's end hour. Air superiority goes round the mission's provinces
and flies one province ahead of the strongest enemy air unit it knows of; the intercept waits
at its base, watches two steps around its provinces and turns for home once it has caught
its target. **Anything in the air may be fought; a unit sitting at its base only by a unit
under one of the two orders, and only while it is itself fit to fly** - which corrected the
fact base, whose record had both orders needed everywhere. A battle is four hours of each
wing firing its `air_attack` in shots at one wing it keeps to, and a unit out of one starts
and joins no other for an hour **while it stays where it is - its first move ends the wait**,
which the reading for bombing corrected on 2026-10-10. **Eight things in it are the original's and look odd, and are built
so - do not tidy them; `aircombat.md`, section 9**: a beaten unit beside the sea flies out to
sea and not home, a worn unit at its base cannot be attacked and yet takes off every third
hour, a wing's defence and its `air_defence` do nothing, a critical hit multiplies the
strength alone. Settled on 2026-10-10: that the defence does nothing is "a bug
in the original", and of the others raised then, "Just note them" - none is to be mended
unless the user says so. **What a country knows of a province was read in part for it, and whole
on 2026-10-14** (`Sim/Intelligence`, `docs/mechanics/intelligence.md`): 9 for whoever holds it
and their friends and for a country with a unit on its ground, 2 with a unit in its water, the
land beside at 3 or more, grants and radar; nothing draws a fog of war. **A mission's area is a list of provinces that comes with the order**, and the air
orders window makes it - **built whole on 2026-10-11, read out of the original, tried with
`--click`, and it has not been reviewed in play** (`fleetsandair.md`, section 4;
`aircombat.md`, section 2): four shapes (`CAirArea` - point, region, circle, cone, the cone's
arc in single precision as the original works it), three sliders, a right click on the map
that aims a circle or a cone and moves a point or a region, shift that adds, the boxes of the
hours, two dates held to the defines' and "continuous", the ladder that ticks the first row,
what the window keeps for the next, and the map coloured by the unit's reach while it is up.
**Nine things in it are the original's and look odd, and are built so - do not tidy them**,
the plainest that **the air window's priority is never sent**; the worklist's *Next up* lists
them. Regions are read from `map/region.txt` for it (`CRegion`, `CRegionReader`). With no AI,
`--mission=GER:airsuperiority:1928` gives a country's air unit a mission for a trial by hand,
and `--select=Luftflotte II` selects an air unit no click can reach at its base outside the
air map mode.
**The air battle and the bombing on screen - `combatview.md`, section 11 - were built on
2026-10-10 from a reading and checked with `--click`, and they have not been reviewed in
play**: the land battle's row in a unit's panel and its window, with a third kind of half
(`CAirSideView`, `CCombatWingEntry`, and `CAirCombatText` for what they say). A side's list
is its wings; **the bombed side of a bombing lists first what of the province the first
bomber's mission bombs** - each building, what lies there of a good, what is made of one -
each with a bar of what is left. A bombing's window stays up with nobody on the bombed side,
and its retreat button sends an air unit where a beaten one goes and leaves it no order
(`CFallBackCommand`). **Neither has a marker on the map, and that is the original's**: it
shows them only once they are over, as icons in its air map mode, counted from a list of
finished battles - both built since.
**The air map mode - `airmap.md` - was built on 2026-10-11, read whole out of the original,
tried with `--click`, and it has not been reviewed in play**: grey, green where the
player's intercepts reach and red where its air superiority does, the sea too; a ring of range
round each of its air bases for each range of aircraft there, drawn with the original's
`map/Sector.dds` and coloured red to green by strength as its `circle.fx` does; the air bases
and anti-aircraft guns as large pictures by their level, **the large air base asked before any
counter on a click** - how an air unit at its base is picked there; and the three icons of the
week's air battles and bombings (`CAirMapColours`, `CAirMapIcons`, `CAirBaseCircles`,
`CAirMapText`, `src/Map/CAirMapLayer`). Under it, **`CCombatHistory`, the list of finished
battles, which keeps a week**: every battle of every kind goes on it as it ends, each side
named by the first of `CCombatSide.Standing` - the countries still in the fight, made again
from the units left whenever one is taken off, so a beaten side is named by nobody; do not fold
it into `Countries`, which keeps every country a side fought as. **Four things in it are the
original's and look odd, and are built so - do not tidy them; `airmap.md` lists them**: its
colours are made when the player's fighter orders start or go and not each day, a count of
battles is a signed byte, a beaten side is named by nobody, and an air base the player knows
little of is drawn at the size of a level 1. Its colours' alpha, which the original's shaders
use in a way not read, is laid over the ground turned grey - ours.
**Leaders - `leaders.md` - were taken up on 2026-10-11 and cut in four, the cut this
project's: who they are and who commands what; how far a command reaches; what a leader does
in a battle; and the window he is chosen in. The first is built, read whole out of the
original, and it is the original's unit for unit**: the start save's 1555 unit leaders and 101
countries' lists of leaders in service all agree (`CLeaderCheck` in the savegame tool's
start-date run). `history/leaders` and `common/traits.txt` are read (`CLeaderReader`,
`CTraitReader`); a leader's history is folded to the start and his experience raised to his
skill's floor; every leader with a rank is put in service as a game is made, and others on
the day their history first gives them one, at rank 1 (`CLeaders`, `Sim/Leaders`). **Every
unit with no leader is given the best free one of its country**, in the order the units were
made: skill and one in thousands, a set amount for each trait effect that suits the unit, the
first of equals winning, and a land unit taking only the exact rank of its level. A unit put on
the map or joined gets one the same way while the country's `AutoAssignLeaders` is on. The
panel shows a unit's leader, in red without the rank, and his portrait; the counter keeps the
unit's name (`docs/compatibility.md`, entry 7). **The second piece, how far a command
reaches, is built too, read whole and checked against the same save** (`leaders.md`, section
8; `CCommandReach`): a headquarters reaches in full within its first brigade's radio times the
`RADIO_*_LEADER_DISTANCE` define of its level, the radius over the distance out to twice
that, and nothing beyond, the links multiplied up the chain; a trait comes down the chain
halved at every step. The army's commander adds his skill by his reach to every brigade's
organisation ceiling - all 764 brigades under one agree with the save, 142 did - and the
army group's takes a twentieth a point off what a unit uses, with the leaders'
`supply_consumption` traits. **A new game's stockpile is reckoned before its leaders are
chosen**, as the original does it, so `CLeaders.SetAtStart` puts on only the files' leaders and
`CLeaders.AssignAtStart`, called after `CStockpile.AddUnitGoodsAtStart`, chooses the rest; keep
that order. The panel's radio icon and a broken dark red line on the map show a unit out of
range, checked with `--click`. **Distances here are the game's kilometres, which are
quarters of a map pixel** - the same measure as an aircraft's range: a corps reaches 200 km in
full, an army 400, an army group 600, a theatre 2000. **Each rank's skill does one thing of its
own**, as set out on 2026-10-11 (`leaders.md`, section 8): a division's combat
efficiency, a corps' chance of bringing up a reserve, an army's organisation, an army group's
supply use, a theatre's stacking penalty - the division's, the corps' and the theatre's in
battle. **The third piece was cut again, this project's cut: a land battle - built
2026-10-11, `leaders.md` section 9, `CCombatLeaders` and the leader terms through
`CCombatModifiers`, `CFrontLine`, `CCombatDamage`, `CAttackDelay` and `CMovementSpeed`, read
out of the original and set against nothing as the land battle is - then sea and air, built
the same day (`leaders.md` section 10, through the `CNaval`, `CAir` and `CBomb` classes of
`Sim/Combat`), then the experience and traits a battle gives, built the same day too
(`leaders.md` section 11; `CCombatExperience` and `CTraitGain`).** A unit's leader's bonus is
gated whole on his rank and skill, so an unled unit gets none of its commanders' offence or
defence traits; a side's leader is its highest ranked, first of equals. At sea and in the air
the bonus has no trait in it, and **a unit's own leader also draws a fleet up, picks targets
and finds ships for bombers whatever his rank** - `CCombatLeaders.UnitSkill` and not
`Bonus`; eleven traits come in there (`leaders.md`, section 10). Two technology figures went
in with the admiral because they sit in the same lines: `targeting_chance` and
`targeting_choice`. The aggressiveness a tactic is picked with is the leader's unit's, by the first plan or setting of its own from it upward (`unitpanel.md`, section 9). **Experience comes only from battle, in each unit's own hour** - `CMovement.RunUnitHour`,
after its order - and rolls a die of the game's own generator there, so a battle's rolls hang
on it; keep it in that place. **A trait is earned by hours banked at the head of each side's
fire and counted when a unit leaves a side** (`CCombatSide.Remove`, which takes the state for
it) **and at the battle's end** (`CCombats.Finish`); a tactic's `trigger` and a gainable
trait's are one class now, `CCombatCondition`, read by `CCombatConditionReader`.
`CLeaders.SetLeader` takes the original's two flags - throw away his banked hours, cost the unit
organisation - and **only a player's change of leader passes them**; the choosing passes neither.
**The fourth piece, the window a leader is chosen in, is built too** (2026-10-11, `leaders.md`
section 12; `CLeaderSelectionPanel` and its rows in `src/Screens`, `CLeaderWindowText` and
`CLeaderTraitFilter` in `Interface/Screens`, and four commands), **read out of the original,
tried with `--click`, and reviewed in play on 2026-10-11: "Everything looked good
with the leaders"**, with each side's leader in a battle's window.
**Tooltips - `tooltips.md` - were taken up next, on 2026-10-11, and the mechanism is built:
read out of the original, tried with `--click`, and it has not been reviewed in play.** One
box, `core.gui`'s `ToolTip` (`CTooltipBox`), on a layer over everything; each frame
`CTooltips` asks the element under the mouse - a `CGuiObject`'s `Tooltip` - or over the map the
province (not built: `MapTooltip`), adds a button's shortcut line, and shows a tooltip's
delayed half under a line of fourteen dashes once the mouse has been still past the next whole
second of the clock; the box hangs 64 below its element's corner, stands on it in the lowest
fifth of the screen, and is kept inside the right and bottom edges only. **No `.gui` file of
the original's or of BlackICE's sets a tooltip of its own** - every tooltip is a screen's code,
chosen by the element's name - so what each says comes screen by screen, in the order the
worklist's *Next up* gives. `--click` takes `m460,40` to move the pointer
without clicking and `w2` to wait two seconds, which a delayed half needs, and
`d560,140>760,340` to drag - **a window whose file says `moveable` is dragged by the left
button held anywhere on it but on a button, a scrollbar or a list** (2026-10-12, an
account of the original's event window; `interface.md`, section 7).
**The map's tooltip is built too** (2026-10-11, `tooltips.md` section 7, `CProvinceTooltip` in
`Interface/Tooltips`): `ProvinceTooltip_Build` read whole - a selected army's `UTM_*` warning,
the name and owner, the terrain, political, supply and air modes' parts, the distance from what
is selected - tried with `--click`; it was set beside the original on 2026-10-11 and
"the style looks good", and the screenshot of the original has the province's tooltip standing over the
minimap, built since 2026-11-07 (`minimap.md`, section 6). **The top bar's is built too**
(2026-10-11, `topbar.md` section 14, `CTopBarTooltips`): by the element's name, read whole -
each picture a heading with its description delayed, each resource's number its day's ledger,
IC, dissent, diplomacy, national unity, officers and manpower - tried with `--click`, and
reviewed in play on 2026-10-11: "Looks good". **Four of them end in where a modifier comes from**: a
country's rebuild now keeps every source it adds, in order, the scaled ones as their copies
(`CCountry.ModifierSources`), and `CModifierText` writes them out as the original does, each
kind by its display key and two flags, percent and more-is-better, read for all 107 of the
original's kinds (`CModifierKinds`, `modifiers.md` section 8). **A table's `\n` is a line
break from now on**, made so where localisation is read. Three oddities are kept as read,
`topbar.md` lists them. **The production and technology screens' are built too** (2026-10-11,
`productionscreen.md` section 8, `technologyscreen.md` section 9, `sliders.md` section 6;
`CProductionTooltips`, `CTechnologyTooltips`, `CShareTooltips`), read whole, tried with
`--click`, and reviewed in play on 2026-10-11: "Looks good". **A slider's tooltip is what its share
would do at the slider's position**, before it is sent - the two screens turn a position into a
share in different orders, rounding before or after dividing, and each is kept. An entry window
answers for all its elements through `CGuiWindow.SetTooltips`, **which leaves a text box letting
the mouse through** so that a background button keeps its clicks. **`£` and a name in a text draw
that unit's counter**, the middle of `GFX_counter_` + name (`CCounterIcons`, `CTextIcons`). **Its
last two parts were built on 2026-10-12** (`technologyscreen.md` section 9; `CRequirementText`, since 2026-11-14 `CConditionText`,
`CTechnologyEffectsText`), read whole and tried with `--click`, and they have not been reviewed
in play: why a technology may not be researched is its `allow` block's account of itself, every
condition with an asterisk and its words turned round inside a "none of", **indented as the mod's
DLL corrects the original, by decision - "Please use the fixed logic"**
(`docs/compatibility.md`, entry 10); what levels do is what a first level allows, each unit
type's figures on one line and the country's, all times the level the entry speaks of. Only the
base game's conditions have words yet. **The four builder windows' are built too** (2026-10-11, `builders.md` section 11,
`CBuilderTooltips`), read whole, tried with `--click`, and they have not been reviewed in
play: **only the column headers, the step buttons and the designer's combined arms and terrain
speak** - everything else in them is silent in the original too - with the designer's **terrain
summary** worked out from its brigades' adjusters, and **the practical icon** answered by the game
screen before any window, from the key the window keeps on it. **The step of ten is Ctrl**, which
the step buttons' own tooltip says; it was shift until then. The game state now carries the
terrains in their file's order (`CGameState.Terrains`) for the summary. **A scrollbar's end
buttons repeat while held, Shift makes a press ten times as long and Ctrl a hundred** - Shift
by decision, "Its shift." (2026-10-12,
`interface.md` section 2, `CScrollbarSteps`), read out of the original after an
account of the original's sliders, tried with `--click` (which holds a button with `t1.5:`, Ctrl with `c`
and Shift with `s`): **each frame counts the whole time held in 0.15 s repeats**, so a held button speeds up
the longer it is held and with the frame rate - the original's, kept. A list box's bar moves a
line a press. **The unit panel's are built too** (2026-10-12, `unitpanel.md` section 10;
`CUnitHeaderTooltips`, `CUnitStatusTooltips`, `CUnitOrderTooltips`, `CBrigadeTooltips` and
`CLeaderTooltip`), every entry's routine read whole, tried with `--click`, and they
have not been reviewed in play: **a brigade's summary** names its model and each technology's level -
a level with no text of its own is `Model` and **a year**, the technology's `start_year`,
`first_offset` and `additional_offset` - and **a leader's tooltip** is his everywhere he is
shown. **A text box over a button answers first and leaves the button its clicks**
(`CGuiObject.PassiveTooltip`, opted into by name with `CUnitPanelEntry.AnswerFor`), as the
original's topmost element answers - looked for down through a list's rows too. An idle unit's
order describes itself as nothing, and a headquarters out of reach is yellow, not red - both
the original's. **The deploy menu's and the orders windows' are built too** (2026-10-12,
`mapunits.md` section 10, `fleetsandair.md` section 8; `CDeployMenuTooltips`,
`COrdersTooltips`), read whole, tried with `--click`, and they have not been reviewed in
play: a deploy line lists its unit's brigades, a finished fleet's or air unit's nothing;
`accept` says why only while it cannot be pressed; and **both windows' priority buttons say
nothing**, the original asking twice for a name its file does not have - kept. **A battle's are
built too** (2026-10-12, `combatview.md` section 12; `CBattleTooltips`, `CBattleUnitTooltips`,
`CBattleRowTooltip`), read whole, tried with `--click`, and they have not been reviewed in
play: the in-game screen gives the window's elements to the window **by their group**, so its
close button alone says `CLOSE` and the modifier icons, carrying their id, say the modifier's
name and description; **the attacker's reserves say their chance of coming up and the
defender's a unit's whole**, and in a battle's row **one fleet's ships run together**, both the
original's. **The leader window's are built too** (2026-10-12, `leaders.md` section 13;
`CLeaderWindowTooltips`), read whole and tried with `--click`: a trait's icon says its name and
a line for each effect, and what the window leaves unsaid falls to the unit panel header's
names - so a row's button says "Assign new leader.". A leader's own tooltip says how many he may
command only where he commands a fleet or an air unit, which corrected the unit panel's
reading. **That the original shows them at all the bytes read did not settle**; a
screenshot of the original, 2026-10-12, has a trait's name over its icon. **`@` and a tag in a
text draw that country's flag**, 23 by 11 (`CFlagIcons`); **an occupied province's flags are the
holder's and then the owner's**, which the decompiler shows the other way round and the listing
does not. The supply mode's lines are the original's put together line for line, colours and
the line on infrastructure with them, and the readout of a selected province no longer repeats
them. **Its list is sorted with the original runtime's own `std::sort`, `CStdSort`,
copied step for step** - it is not stable, so which of two leaders of one skill comes first is
the sort's - and checked by running the original's code under an emulator on 1,360 lists; use
it wherever the original sorts with `std::sort` and the order of equals shows.
**Twenty-five things in the pieces are the original's and look odd, and are built so - do not
tidy them; `leaders.md` lists them**: the plainest that an order of battle's leader goes on
whoever he is, that nothing later in a leader's history than his first rank is ever applied,
that a command's reach drops from half to nothing at twice the radius - the one of them raised
for a decision - that `fort_defence` and `surprise_chance` do nothing in a land battle,
`dissent_impact` nothing at sea or in the air, that a raid on a convoy never counts the
hours its raider's leader banked, so `seawolf` cannot be earned by raiding, and that a unit's
portrait opens the leader window where its leader button cannot be pressed.
**Bombing - `bombing.md` - is the fifth piece, built on 2026-10-10, read out of the original
and set against nothing, and it has not been reviewed in play**: the eight bombing
missions (`CBombingOrders`, `CBombingMissions`, `CBombingProvinces` in `Sim/Missions`), the
bombing itself (`CBombing`, `CBomberFire`, `CProvinceBombing`, `CAntiAircraft`,
`CBomberModifiers`, `CBombTargetModifiers`, `CRadarCover` in `Sim/Combat`) and the day's
repair (`CProvinceRepair` in `Sim/Provinces`). A unit flies out, bombs a province for four
hours and is told to return the hour after. **A bombing is a battle of its own kind, one to
a province and kept apart from a unit's battles** (`CUnit.Bombing`, not `Combats`): a
division under bombs is not "fighting" and marches as before, and an air unit in one stays
over it. Each wing fires at a target its mission goes for and then **bombs the province** -
whole runs of its strategic attack, never fewer than one. **A building has two levels since
then**: `BuildingMaxLevel` is what was built and `BuildingLevel` what works of it; a
province's modifier is made from the second, "is there a port" is asked of the first
(`CMovementRules.BuildingLevel`, by name, answers the first), and which the remaining rules
mean was not read - the worklist's *Loose ends* says what each was given. **Nine things in it
are the original's and look odd, and are built so - do not tidy them; `bombing.md`, section
12 - and none has been raised for a decision**: a bomber with no leader has a ship on its
list of targets one time in a hundred, ten more for each point of his skill, so strikes at
ships wear the port and hardly touch the fleet; a fleet in port fires its air attack three
times over through its province; a second unit cannot join a bombing of the ground alone;
ground attack goes round its list and weighs nothing, and interdiction flies only at ground
its country sees into - since 2026-10-14 the land beside its own and beside its units, and
what its radar reaches (`docs/mechanics/intelligence.md`). **A carrier's
aircraft are built since 2026-10-16** (`transport.md`, sections 13.1 and 13.2). With no AI,
`--mission=GER:strategicbombing:1928` gives a mission for a trial by hand, and the orders
window gives all eight - checked with `--click`.
**The province readout under the top bar is scaffolding and shows only for a selected
province** - decided 2026-10-09, when it followed the mouse: "Very distracting." **Since the province view,
2026-10-15, it is put away behind a switch of the debug menu, on Shift and 4** - by choice, mirroring the original's
console on Shift and 3 (`src/Debug/CDebugMenu`); `--click` presses Shift and a key with `K4`.
**The four windows a unit is ordered
from - `builders.md` - have been tried by hand once and never measured against the
original.** That try found seven buttons that could not be pressed in windows every
scripted check had passed, because the checks opened and filled them by name and pressed
nothing: **check a screen with `--click`, which sends the mouse's own events, before
calling it working.** Edit boxes are drawn as their text, and since 2026-11-05 typed into where a screen asks - the save dialog's - as the original's take typing (`interface.md`, section 8; `--click` types with `type:Text`); a dialog's
shields are drawn since 2026-10-12, the popups' flags. **The two later screens were built from a reading nothing
checked against a running game.** They were tried by hand twice on 2026-10-08 and
what was wrong is mended - the worklist's *Next up* and *Loose ends* say what is left.
**When something on screen is a unit or two from the original, measure a screenshot
against the art and read the routine**; two guesses at where centred text goes were
wrong before the font's own arithmetic was read. What follows is the reasoning, which still holds.

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
- **The original's small bitmap fonts are not drawn.** Decided 2026-10-08, on
  seeing the top bar scaled: glyphs a dozen pixels high cannot be enlarged into anything
  readable, so the old font system is replaced. **Narrowed on 2026-11-18**, by decision -
  "large fonts (at least 38) get upscaled, and the other ones use a fallback": a font whose
  capital H stands 38 pixels or more in its texture is drawn from its own glyphs, made into
  distance fields as it loads, in the descriptor's own layout (`CDistanceFieldAtlas`,
  `CDistanceFieldFont`) - after a mod's `.ttf` and before any stand-in. That is the map's
  faction and province names and BlackICE's display faces; every other font of the base
  game and BlackICE is 26 or less. What follows is the small fonts'. A font is still *named* exactly as the
  files name it - `font = "Arial12"`, a `bitmapfont` entry, a `.fnt` in `gfx/fonts` - and
  the `.fnt` is still read, for three things: the typeface its `info` line says the
  glyphs were made from, how wide its characters came out, and the height of a line.
  The typeface is then drawn as outlines at the size that reproduces those widths, so
  text fits the boxes the `.gui` files give it. A `.ttf` or `.otf` of the font's name
  beside the descriptor is used first, which is how a mod supplies one and stays valid
  in the original; otherwise an open typeface from `fonts/` that stands for the one
  named, narrowed to fit where its letters are wider. **Do not bring a small font's glyph
  textures back** - a distance field below the line shows the pixels it came from - and
  do not size text from a descriptor's `size` figure - two copies of one
  font disagree on it. `docs/mechanics/interface.md`, section 6.
- **Typefaces are shipped, and nothing is taken from the machine.** Decided
  2026-10-08: the game must not depend on what is installed on the machine it runs on. So
  no `SystemFont`, no lookup of an installed font by name, and `allow_system_fallback`
  off on every typeface, in its `.import` file and in code - a character a typeface
  lacks stays missing. Everything drawn comes from `fonts/` or from a mod's own
  `gfx/fonts`. Those files are typefaces whose licences let them ship with the game -
  most under the SIL Open Font License, two free ones under their own terms (2026-11-12) -
  which is why they may be here when the original's may not: **a font goes in `fonts/`
  only with its licence text beside it and a row in `fonts/readme.md` saying where it came
  from and what its licence allows**, and never one extracted from an install or copied
  from a system folder.
- **Bitmaps do not gain detail, so a texture may be supplied at any resolution.** Icons
  soften when enlarged; the answer is that a mod can ship a sharper
  file, and the runtime works in ratios of the texture rather than in its pixel counts. Two
  things follow from how the files are written:
  - *An element's size on screen must never come from a replacement's pixel count.* Icons,
    buttons and checkboxes declare no size in the `.gui` file - about 1,500 entries in Their
    Finest Hour - and take it from the texture. So something has to say what size a texture
    was designed at. **How is not decided** (2026-10-07). The leading idea is that the
    original file stays as the size reference and a sharper one overrides its pixels from a
    higher layer, which needs no new syntax and leaves the mod valid in the original.
    **The runtime as built does the thing this warns against**: an icon or a button is the
    pixel size of whatever texture loaded. Right for the original's art, wrong the day a
    sharper file is dropped in, and it is in the worklist's *Loose ends* until this is decided.
  - *Sprite sheets need no naming scheme.* A sheet declares its frame count in the `.gfx`
    entry (`noOfFrames`, on 226 of about 1,300 `spriteType` entries), so a frame is the
    texture's width divided by that count at any resolution. The pixel-valued sprite keys -
    `size` and `borderSize` on the cornered-tile, tile and progress-bar kinds - scale by the
    same ratio as the texture they describe.
- **There is prior art in history.** `src/Gui/` and `src/FrontEnd/` ran the original's main menu
  from its `.gui` files and were removed in commit `72b0eb6`. Today's `src/Gui/` started from
  that one. ~~The lobby that `src/FrontEnd/` drove is not coming back.~~ **Reversed 2026-11-06**: a
  "Pared-down lobby" was chosen, and today's `src/FrontEnd/` builds the main menu and the lobby
  anew from a reading of the original (`docs/mechanics/frontend.md`).

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

`GitHub/hoi3-reversing`, normally checked out beside this repository: 250 `findings/FINDINGS-*.md`
write-ups, `CLASSES.md` for the class-by-class account, and `TRAPS.md`. Cite a findings file
with the `reversing/` prefix on the front - the fact base's logical name, which resolves
wherever it is checked out. Its `checkrefs.py` verifies those references from the other
side, including the ones written here.

**When a spec needs something nobody has read, the reading belongs to the fact base, and
all of it.** Read it there, by that repository's rules; write the findings file; **and
enter what was read in its `project.json`** - a standing instruction, and
the fact base's `CLAUDE.md` has the steps. This project then cites the findings file and
takes nothing but the prose. The record is not optional because the rewrite was the one
asking: on 2026-10-07 four functions were read for `docs/mechanics/production.md` and left
as prose, and had to be entered afterwards.

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
  scrollbar that runs up and down is read from the bottom, so its definition looks as though
  its arrow sprites are swapped; a map editor is present but hidden behind
  two `Hide()` calls because it was never finished. Expect the loaders to absorb this.

**There is no Ghidra tooling here any more.** `tools/ghidra/` was removed on 2026-10-07: there
is one Ghidra toolchain and it lives in the fact base - an 8212-address, 747-struct record with
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

From VS Code, `F5` offers *Play OpenHOI3*, which comes up on the main menu since 2026-11-06 - a
switch that says what to play, `--country=`, `--load=` or `--replay=`, starts that game at once, and the
front end alone plays `--click=` and `--screenshot=` where none is given - *Play OpenHOI3 (choose
country)*, which asks for a tag and whether to have every unit type, the prompt empty by default and an empty one
bringing up the main menu (decided 2026-11-06: a launch "without specifying a country" goes to the main
menu; an empty `--country=` says nothing, and any other switch then is printed as left aside), *Play OpenHOI3 (mod)*,
which asks for a mod by the name of its `.mod` file - `BlackICE GitHub` by default - and brings up the main menu with
it laid over the install (`--mod=`; the original launcher's `-mod=mod/<file>.mod` works too; `docs/mechanics/mods.md`), *Play OpenHOI3 (ask
for arguments)*, which asks for the developer switches themselves in one box - the game
cuts an argument wherever a space is followed by `--`, so the box may hold several -
*Play OpenHOI3 (headless)* and *Open Godot Editor*. The switches are listed above
`RunDeveloperArguments` in `src/CSession.cs`.
**Speed is judged in *Play OpenHOI3 (fast)***, which builds with `-p:OpenHOI3Fast=true` -
optimised code in the Debug configuration's folder, the only one the editor's binary loads
(`Directory.Build.targets`) - and runs without the debugger: on 2026-10-13 it ran full speed
at 27 frames a second where the plain Debug build ran 16, and at 43 once the trading ran side by side. The others are what a breakpoint
needs; a figure of speed from them says little.
The Godot executable path comes from the `godotTools.editorPath.godot4` setting rather than
being hardcoded, so set it in `.vscode/settings.json` (git-ignored) or in user settings.

**The install is found, not configured.** `CDirectorySettings.Discover` walks up from the
project directory it is given looking for a `Hearts of Iron 3` folder, and accepts one only if the content
is beside the executable - a folder holding a stray copy of the exe is not an install. Four
levels of walking is what reaches an install kept in the user's home directory rather than
beside the repository.

## Always

- **Never commit unless asked.**
- **Every class in its own file, named after it.** An instruction of
  2026-10-08, after several were found to a file. It covers every top-level type - a class, a
  record, a struct, an enum, an interface - in `lib/`, `src/`, `tools/` and `tests/` alike,
  so a small enum or a one-line record that only one class uses still gets its own file. A
  type nested inside another stays where it is. A new file under `src/` needs the `.uid`
  file Godot writes beside it: run the editor's import once and add it.
- **And every file in the folder for its subject, which is its namespace.**
  Decided 2026-10-08, when `lib/OpenHOI3.Sim` had reached 56 files side by
  side: "A flat folder with all is pretty chaotic." So `lib/OpenHOI3.Sim/Units/CUnit.cs`
  is `OpenHOI3.Sim.Units.CUnit`, as `Content/Gui` and `Content/Clausewitz` already were.
  `docs/layout.md` lists the folders and what each is for. A new type
  goes in the folder it belongs to, not at a project's root: the roots hold only what
  everything else is built on - in `Sim`, the game state, the tick, the clock, the date,
  the defines and the two kinds of number. When a subject has no folder, make one
  rather than leave the file at the root, and add it to `docs/layout.md`. The tests
  are in `Sim/`, `Content/` and `Interface/` by the library they test.
- **After creating `.cs` files from outside the editor, `touch` the project file that owns
  them.** VS Code's C# language server did not notice 34 files a script created on
  2026-10-08 and reported `CS0246` for every type that had moved, while `dotnet build` was
  clean. Changing a `.csproj`'s timestamp makes it reload the project and find them; so
  does *.NET: Restart Language Server*. Seen once, so whether it misses every file made
  outside the editor or only a burst of them is not known.
- **A deliberate departure from the original goes in `docs/compatibility.md`, in the
  session it is decided.** Decided 2026-10-08: every decision
  to depart deliberately from the original's compatibility is tracked.
  It is for decisions - a file that is valid in the original and that here means
  something else, or nothing, because somebody chose that - with who chose, when, what
  a mod loses and what would undo it. It is not for what is unbuilt (*Loose ends*) or
  for what nobody has read (a spec's **Ours**). When the user settles a question
  like "keep ours or read the file's", the entry is written before the session ends,
  and a reversed decision is struck through, not deleted.
- **A fault of the original is kept and listed.** An enhanced version of the engine is planned, said 2026-11-09. What looks like a bug, oversight or dead rule of the original is built as it is and given a line in `docs/enhanced.md`; it is not fixed here unless the user says so.
- **Check every addition against a whole game's replay that survives a save and a load.** Decided
  2026-11-12: the replay for players is to record a game from start to finish, through every save and load
  (`docs/enhanced.md`; the replay built today is for development only, `docs/replay.md`, section 1). So ask of anything
  added: would a game saved and loaded play on exactly as it would have without the load? State that later play hangs
  on - a figure kept, a countdown, a list, a generator's place, a draw - has to be in the save or made again from it the
  same. Where it cannot be, because the save keeps the original's keys, say so in the spec and add it to that entry in
  `docs/enhanced.md`: that list is what the enhanced engine's own save will have to hold.
- **Work in a worktree of your own, and ask before it goes back to `main`.** Several sessions
  work on this repository at once, so a session does not work in the main checkout: it makes a
  worktree beside the repository on a branch of its own - `git worktree add -b <topic>
  ../OpenHOI3-<topic> main` - and works there. When the work is done, ask the user whether to
  merge it back to `main`, and do so only on their word: rebased onto `main` and fast-forwarded,
  linear commits, never a merge commit. Remove the worktree and its branch once it has landed.
- **Keep `docs/worklist.md`'s *Next up* and *Loose ends* current.** They are what a session
  with no memory of the last one starts from. When a piece of the plan lands, move it out of
  *Next up* and into *Where things stand*; when something is deferred, it goes under *Loose
  ends*. `readme.md` is the public overview for players and contributors: keep it short, keep
  working notes out of it, and update it only when what a player or a contributor needs to know
  changes.
- **Check against the savegame.** `tools/OpenHOI3.SaveCheck` loads an install and reports,
  field by field, where the result differs from a savegame the original wrote on the start
  date. Run it after touching a loader or a formula; a rule that reproduces the save to the
  thousandth is worth more than one that reads well. The reference save is normally at
  `Documents\Paradox Interactive\Hearts of Iron III\save games\Germany1936_01_01_00.hoi3`.
  That run also sets every country's leaders in service and every unit's leader beside the
  save's - 101 of 101 and 1555 of 1555 - and should after any change to `Sim/Leaders`, the
  order of battle's reading or what a unit's figures are made of, which the choice weighs;
  and the organisation of every brigade under an army with a commander, 764 of 764, which
  his reach decides. It ends by saying how many land units start out of radio range - 95,
  none of them Germany's - which is a look, since a save keeps nothing of it.
  **Anything that moves from day to day is checked against two saves of one game**:
  `OpenHOI3.SaveCheck adjacency <install>` checks the edges between provinces - which
  there are, their order, kinds, distances and bearings - against the cache the original keeps of
  them in `map/cache/adjacencies.bin`, and each province's climate index against `map/cache/climate.bin`; all 83,596
  and all 14,168 agree and should after any change to the
  map loader. `OpenHOI3.SaveCheck weather <install> <earlier save> <later save>` gives every province a save's weather
  and the game its fronts, runs an hour and sets it beside a save an hour later: on 2026-10-15 `Germany1936_01_08_02`
  and `_03` give every front's provinces in order and every province's eight figures but 48 land provinces', each of
  which agrees given another terrain - the map's terrain rule, which the check lists (`weather.md`, section 7); since
  2026-11-13, the terrain counted round the default text point two pixels south of the centre, all of them; with only
  a start save it is a look at a new game's weather beside it. Run it after touching `Sim/Weather` or the map's
  terrain. `OpenHOI3.SaveCheck research <install> <earlier save> <later save> [loaded]` does it for research - `loaded`, as
  `march`'s, taking the earlier save as loaded, which a mod's play needs (`mods.md`, section 7) -
  the monthly decay and the build queue - a building line's price among it, 21 of 21 in January (`buildinglines.md`, section 8), and every convoy line's, 107 of 107 on 1 April, with the lines paid in full worked forward where the two saves are of one session - 69 of 70 to 20 April (`convoylines.md`, section 6) -
  and the same folder has four later saves of that game - the three
  autosaves and `Germany1936_04_20_03.hoi3`. An autosave is overwritten as the game goes
  on, so a comparison that matters should say which dates it was run against.
  `OpenHOI3.SaveCheck march <install> <earlier save> <later save>` does it for what an
  hour adds to a marching division, and wants saves **hours** apart, since the weather
  turns: `Germany1936_01_07_22.hoi3` and `Germany1936_01_08_02.hoi3` give 301 of 301, with
  `_01_07_14` and `_01_04_12` of the same game before them. The same command moves every
  fleet and air unit of the earlier save forward and sets it against the later:
  `_01_08_03`, `_01_08_06` and `_01_08_07` have a submarine flotilla and an air fleet of
  the reference game's player under way, and give 17 of 17 and 1 of 1. **A fleet's passengers go with it** (`transport.md`, section 14): the French fleet carrying the Division de Tonkin gives 1 of 1 from `_01_07_22` to `_01_08_02` and on to `_07` - where it stands, and where the hours took it.
  `OpenHOI3.SaveCheck landing <install> [country] [enemy] [hand|mission][-defended] [days] [seed]` is a trial - no save has a landing: a division put aboard the country's carrying fleet nearest the enemy's coast and landed, by hand or by the invasion mission, each hour told. France against Siam with nothing said: ashore in Trat 48 hours after its order. Run it after touching `Sim/Transport` or the land march.
  `OpenHOI3.SaveCheck automate <install> [country] [objective's country] [days] [seed] [corps|army|armygroup]` is a trial too - no save has an automated headquarters: the country's theatre with the most divisions handed to the AI, the other country's capital made its objective, and each day how many divisions march and have left where they stood, and its area (`mapunits.md`, section 12). On 2026-11-09, Germany's OB West with Amsterdam: the area 22 provinces down to the 9 facing the Netherlands, 17 of 24 divisions moved in six days. With a level, that headquarters under the theatre instead (`unitpanel.md`, section 9.1): on 2026-11-21 VII. Armeekorps given Warszawa, twelve provinces and all five divisions marching. Run it after touching `CAgentTick`, `CAgentArea` or the commands of a plan.
  `OpenHOI3.SaveCheck airborne <install> [paradrop|carrier|supply][-defended] [country] [enemy] [days] [seed]` is a trial too - no save has a drop, a carrier group rebased or an air supply, and no 1936 order of battle a transport plane or a paratrooper, so it makes them from the install's own types (`transport.md`, section 14). On 2026-10-16: Germany drops three paratroop brigades from Breslau on Ostrów Wielkopolski, empty and taken at once, Poland told nothing - and on Danzig with `paradrop-defended`, a battle and Poland told; the United Kingdom's group of HMS Furious goes to Glasgow and back, is given carrier protection, follows the Home Fleet to the Moray Firth and goes to Scapa Flow's air base when the fleet is taken away; Germany's air supply carries 13.6 supplies to Garmisch Partenkirchen in five days (12.5 on 2026-11-21). Run it after touching `Sim/Transport`, the air missions or `CMovement.RunHourly`'s choice of who has an hour.
  `OpenHOI3.SaveCheck revolt <install> <earlier save> <later save>` does it for revolt risk, and wants
  saves with **one midnight** between them: the world as the earlier save has it - owners, holders, revolt
  risk, the policies and every division of the start where it stands with its brigades' strength - and every
  province's suppression and one day's step set beside the later save. `Germany1936_01_07_22.hoi3` and
  `_01_08_02` give 1 of 1: Italy's 9987, suppression 0.692, 0.031 (`docs/mechanics/revolt.md`, section 6).
  Run it after touching `Sim/Revolts` or what a unit's suppression is made of.
  `OpenHOI3.SaveCheck rebels <install> [occupier] [occupied] [days] [seed]` is a trial and not a check - no reference
  save has a rising: the occupier at war with the occupied and holding ten of its provinces away from its capital,
  a rising of the roll's three brigades in the first of them and another in the occupier's own land where no unit stands,
  and the days run, telling each faction's armies and land and what the roll makes (`docs/mechanics/rebels.md`, section
  14). On 2026-10-15, Germany and Poland for thirty days: the Polish partisans hand Zarasai straight back and march into
  the land Germany holds, the rabble take Cranz, and the roll makes nothing. **Every rebel brigade is at nought
  organisation after its first hour**, the rebels having no officers (`rebels.md`, section 13) - the original's, by the
  account of 2026-10-15: they "dont" have organisation. Run it after touching
  `Sim/Revolts`, `CRelease` or the capture in `CCombats`.
  `OpenHOI3.SaveCheck underground <install> [occupier] [occupied] [days] [seed] [ai]` is a trial too - no save holds an
  underground: the same war and ten provinces held, an underground of the occupied finished in each of the first three,
  to spread, to raise partisans and to build up, as a line of the queue would finish it - the queue holds no buildings -
  and the days run, telling each one's strength and action (`docs/mechanics/undergrounds.md`, section 9). On 2026-10-15,
  ninety days: they start at about 0.33 from Poland's nearest air base and grow 0.01 a day, and Braslaw's partisans
  rise in Zarasai, hand it back to Poland and dissolve Zarasai's own underground with it. The occupied is played, so the
  three keep their actions; with `ai` the AI sets them (section 10) - on 2026-11-21 all three built up, then raised
  together. Run again later that day, neither run keeps an underground to the end - none is left by 20 February, by 31 January with `ai` - the trial's war having run to surrenders and an annexation by then (by its messages; not followed). Run it after touching `CUndergrounds`, `CUndergroundAi` or what a province's suppression is made of.
  `OpenHOI3.SaveCheck load <install> <savegame> [hours] [later savegame]` loads a save of the original's as a game here: it
  lists every key not loaded yet by its block, sets the owners, laws and technology levels beside the save's, then every
  unit, brigade, leader in service and line of the queue, found by its number, and runs the hours given - or, given a later
  save of the same game, the hours to it, and sets the units beside that, counting what differs by country. On
  2026-11-04, `Germany1936_01_07_22.hoi3`: 14,168 of 14,168, 707 of 707, 28,987 of 28,987, and 2,001 of 2,001 units,
  4,072 of 4,072 brigades, 3,303 of 3,303 leaders and 179 of 179 lines; four hours on to `_01_08_02`, Germany's every
  brigade's organisation the save's (`docs/mechanics/savegame.md`, sections 4 and 5). It also sets the wars, the goals,
  every kind of pact, the trade routes, the convoys and each country's pool beside the save: on 2026-11-05 all of both
  the 7 January save's and the Ireland game's of August 1938 (section 6). And every battle - kind, province, hours, units,
  each side's figures, lists and a bomber's tallies - the list's order, every finished battle and every sunk ship: on
  2026-11-05 all of five saves' with every kind of battle but a naval bombing in them, `Ireland1938_08_03_14.hoi3` the
  fullest (section 7). It measures each NAP's border again beside the save's figures: on 2026-11-09, `Soviet_Union1941_07_16_21.hoi3`, 7 of 8, Japan and China 118 to 142 as saved; the eighth is 23 against 15 for Germany and Vichy France, the save being of 21 hours after the day's measuring. `examples=TAG` after the rest makes every miss's examples that country's, a dozen of them, and `trace=TAG` gives the game the install's Lua and writes that country's AI as it runs, each balancing pick among it; `generator=FOLDER` puts in the generator dumped from the running game before each battle, with the target each wing or ship was holding to and a ship's distance and experience, which a save does not carry, and `battles=FILE` writes each battle's fronts, each unit's firing and the units after (`landcombat.md`, section 8), `watch=N,N` what those provinces hold after the hours, and `model=FILE` sets each unit's modifier logged from the running game's attack estimate, an attacker's or a defender's, beside ours on the save as loaded - 313 of 313 on 2026-11-12 (`moving.md`, section 11.4). Given a later save it also sets the ways the stretch gave beside the original's - which units the original sent or stopped, how many ours did the same, and what ours gave that the original did not - and `skipentry` leaves out the two AI passes of entering, for a save written after the original's own (`docs/worklist.md`, *Next up*, item 5). `series=A,B,C`, saves an hour apart of the original's own run from the later save on, A being that save, then plays each hour and sets that hour's ways alone beside the original's, one line each, written to `orders=FILE.hN`; two runs of the original differ, so it is judged against the original's own agreement with itself - 54 of 69 land orders over six hours on 2026-11-10, ours 53 of 73. `draws=FOLDER` with the series plays back a run of the original's recorded on the running game by the fact base's `probes/hoidraws.lua`, its hourly saves the series: the game's generator put back each hour and every country's AI draws answered with the original's, so a difference is a rule and not a roll (`docs/replay.md`, section 9) - on 2026-11-15 against such a run of July 1941, 267 the same and 59 ours alone, where without the draws 186 and 128. Run it
  after touching `Content/Saves`,
  `CGameStateLoader.BuildWorld` or the Clausewitz reader.
  `OpenHOI3.SaveCheck write <install> <savegame> [written file]` loads a save, writes it straight back and sets the two
  beside each other: every province block byte for byte, and every block key by key - what the save has and was not
  written, what was written and it has not, values that differ, keys out of order. On 2026-11-05 `Germany1936_01_07_22`
  gave 9,372 of 14,167 provinces byte for byte, the rest differing only by what the game does not hold (`savegame.md`,
  section 8). `keys=text,text` lists every key whose path holds one of the texts, where otherwise only the commonest
  are - `keys=theatre,navy,air,construction,active_leaders,deployment,templates` the military part, whose every
  country is the save's in six saves of 1936 to 1941 once the keys nothing holds are left out (section 9); the pacts,
  the wars, each country's convoys, the battles and the finished battles - `keys=diplomacy,active_war,previous_war,combat,
  sunk_ships,convoy` - are the save's byte for byte in fourteen saves but for a NAP's three figures, a sunk ship's `home`
  and `builder`, and the bombings a load turns round (section 10); each country's politics and research queue are the
  save's byte for byte in eleven saves of 1936 to 1941, but a load's `cabinet`, which is the seats allotted again and
  then the save's list, as the original appends it - 22 figures for 11 (section 12) - and each country's spies too
  (section 13), and the top level's counters, flags, victory conditions, fired events and fronts (section 14), and a
  country's exile, goods pools, battles fought, strategic warfare, alignment and lend-lease (section 15), and its AI's
  strategies and theatres but for the figures nothing holds (section 16); `write <install> new <start save>` writes a new game
  beside the start save, its history counter the save's 5,608 and its pacts the save's in its order. Run it after
  touching `CSaveWriter`, `CSaveUnitWriter`, a reader's `Write`, how a pact is numbered or anything they write.
  `OpenHOI3.SaveCheck checksum <install> [mod name]` works out the original's file checksum and sets it beside the last
  `Checksum is` line of the player's `logs/setup.log`, the mod's user folder's with a mod: on 2026-11-13 Their Finest Hour
  `1499173604 (GSKH)` and BlackICE 15.1 `-410340735 (ZAUD)`, the original's both (`docs/mechanics/checksum.md`). A log older
  than a summed file says nothing. Run it after touching `Content/Checksum` or how the map cache is found. On 2026-11-16,
  with the mod layer's search path, BlackICE GitHub `1599539514 (OXLS)` and BlackICE 16.0rc1 `-1864650988 (SDXE)` too.
  `OpenHOI3.SaveCheck modsurvey <install> [mod name]` is a look: it lays the mod over the install as the original does
  (`docs/mechanics/mods.md`) and reads everything a new game reads - localisation, countries, map, the `.gui` and `.gfx`
  with each texture and sprite they name, `autoexec.lua` and, where it stops, each later require on a state of its own, the
  world, the script - then makes a new game as far as it goes, and lists every problem by section, kind and file, the
  largest first, as **ours** (unbuilt keywords, Lua errors, native modules, the original's Lua classes, statics and methods
  nobody provides) or **files** (what the original passes over too). With no mod it is the control: on 2026-11-16 ours 0,
  and the script's thirteen problems among the files' 59. BlackICE GitHub the same day: the new game made in 9 s, ours
  about 27,000 from a handful of causes - `lfs`, which stopped `autoexec.lua` at its second require so that none of
  BlackICE's AI loaded (13,594 since its `file-io.lua` asks the global `OpenHOI3` instead, `mods.md` section 6);
  `remove_brigade` 6,919 times; wxLua; BiceLib's 42 functions, read from the mod's Lua and never from BiceLib; the
  original's country variables in Lua. It runs in the install as its working folder, where the mod's Lua writes
  `lua_output.txt` as the original's does - set `OPENHOI3_WORKDIR` to a scratch folder whose `tfh` links to the install's
  for a run that is not to write there - and after touching `CDirectorySettings`, `Content/Mods` or a reader a mod's files reach. **Every
  command takes `mod=<name>`** anywhere among its arguments since 2026-11-16 (`CToolDirectories`): the mod laid over the
  install, the working folder moved to the install - or to `OPENHOI3_WORKDIR` - and a relative file argument made whole
  first. A BlackICE new game runs under `events` and the start-save checks so: `events <linked install> GER 30
  mod="BlackICE GitHub"` fired 493 times in 54 s on 2026-11-16.
  `OpenHOI3.SaveCheck settings [settings.txt]` reads the player's settings - the Documents folder's with none given - and
  writes them again in memory beside the file: on 2026-11-04 the reference base game file and BlackICE's came out byte
  for byte (`docs/mechanics/settings.md`, section 2). Run it after touching `Content/Settings`.
  `OpenHOI3.SaveCheck shaders <install> [mod folder] [out=folder] [file.fx] [pre=file]` reads every effect file of
  `gfx/FX`, a mod's laid over the install's, and translates each pass of each technique to a Godot shader, listing every
  problem; `out=` writes the shaders, `pre=` one file preprocessed. The client's `--shadercheck=folder` then has Godot
  compile what `out=` wrote, printing each name before its errors. On 2026-11-06, the install with BlackICE: 87 of 88 passes
  without a problem - `shadowmap.fx`'s own - and all 88 compiling (`docs/mechanics/shaders.md`, section 4). Run both after
  touching `Content/Shaders`.
  `OpenHOI3.SaveCheck quads <install>` says first whether the map cache is current - its `time` stamp against the map
  files', `93E125C4` for TFH (`mapdraw.md`, section 2.1) - and where it was found, then reads the quad cache whole -
  `map/cache/quad_index.bin` and `all_quads.bin` - and counts what it holds: on 2026-11-06 1,774 tiles, 565,507 land vertices, 443,825 land triangles over 10,621
  provinces and 572,496 beach triangles, the fact base's figures every one, and every tile's land rectangle the size of
  its terrain-index picture (`docs/mechanics/mapdraw.md`, section 2) - and the border vertices against the map, the lookup texel's province within a pixel of its quad and the slot's neighbour, by the map's order of edges, within three on all but 456 of 2,790,564 (section 4) - and the surf's quads, 141,831 wave pixels and 567,324 vertices, x −173 to 219 and z −193 to 207 half pixels, and the rivers, 721 traced from 722 sources - `map.bin`'s list where it is current, set beside the bitmap's index-0 pixels - and 282 junctions, 61,532 nodes and 364,866 vertices, the fact base's figures every one - and the trees, 1,136,896 pixels drawing and every pixel of forest, forest2, jungle and jungle2 a tree, 1,002,520 in all. Run it after touching `CQuadCache`, the map's edges, `CSurfQuads`, the rivers' trace and strips or `CTreeField`. **The surface
  itself is checked by eye**: `--screenshot` in the political and terrain modes, far and near, against the 2D layers
  over it - its first screenshot was upside down.
  `OpenHOI3.SaveCheck mapcache <install or mod folder> [cache folder] [calls of rand before the first beach] [out=folder]` builds the whole map cache
  from the map files and sets every file beside a cache the original wrote, the player's Documents folder's with none given: on 2026-11-08, every file byte for byte on `Hearts of Iron 3` and on
  `GitHub/BlackICE` against `Documents\Paradox Interactive\Hearts of Iron III\BlackICE GitHub\map\cache` with `1140`, in 2.3 s each (`docs/mechanics/mapcache.md`). A cache's own count of `rand` calls is the last figure; `1140` for that one, and it varies from one of the original's runs to the next. Run it after touching
  `Content/Map/Cache`, the province raster, the map's edges, the distances by sea or terrain.txt's reading.
  `OpenHOI3.SaveCheck battleplans <install>` reads every shipped battle plan and writes it back: on 2026-11-08, 36 of 36 the same, 17 with no header, the `#empty` ones.
  `OpenHOI3.SaveCheck navaldist <install>` checks the distances by sea against the
  original's `map/cache/navaldist.bin`: all 17,621,016 agree, and it takes twenty seconds.
  **A battle has no save to be played forward against**: several reference saves hold one since 2026-11-05, but a save does not hold the generator's state, and the
  original's rolls cannot be made again (`docs/mechanics/landcombat.md`, section 8).
  `OpenHOI3.SaveCheck battle <install> [attacker] [defender] [seed]` is a trial instead: it
  fights one with the install's own units and says how it went - Italy against Ethiopia with
  no tags given, who are at war on the start date. Run it after touching `lib/OpenHOI3.Sim/Combat`.
  `OpenHOI3.SaveCheck seabattle <install> [attacker] [defender] [seed]` is the same at sea:
  the strongest fleet of each of two countries is put in one sea province, the first country
  played by a person, and the fight is told hour by hour - Germany against the United Kingdom
  with no tags given. Run it after touching the `CNaval` classes there.
  `OpenHOI3.SaveCheck raid <install> [raider] [victim] [days] [seed] [air | patrol]` is the trial of a
  fleet's missions: two countries are put at war, the first one's fleet with the most
  `convoy_attack` is sent to raid the water the most of the other's convoys go through, and
  each sinking is told - Germany against the United Kingdom for thirty days with no tags
  given, which sinks 51 transports and 6 escorts on 2026-11-11, with ports, the patrol's shadow and walking on a friend's ground asking the original's friend tests - 44 and 7 after the generator's seeding was corrected on 2026-11-07, 38 and 3
  before it, and 37 and none until 2026-10-13, when a war began to mobilise both sides (`docs/mechanics/war.md`, section 4) - and 37 and 5 on 2026-11-14 in the tree the raid from the air was built on, the same before and after it - and 25 and 4 later that day, once the AI's moves asked the click's test and the invasions and sealift were the original's: the dice fall on other convoys, and on 15 January a raid on an escorted one leaves the raider worn enough to go home - and on 2026-11-18 5 and none, a raider found being fought since (`navalmissions.md`, section 4): the British 49th Navy finds the Schlachtflotte in the Channel Approach on 4 January, and Germany's AI gives it a patrol once it is home, 41 and 7 before - and later that day 13 and none, once the AI's fleet orders were refused for no range and a fleet rebased by the naval body was free for nothing else that pass (`moving.md`, section 14): no British fleet meets the raider on 4 January, it sinks 11 in four raids by the 5th, and comes home four ships down to patrol the Southern Baltic. On 2026-11-21, 6 transports and no escort in five raids, and Germany's AI merges the raider into another fleet on 22 January, which ends the trial - it says what took its raider out of the game since; `air` 11 transports in 11 raids, `patrol` 14 transports and 1 escort in nine. And later that day, the unit AI's countries run side by side with a generator each (`agents.md`, section 12): 4 transports in three raids, the raider home six ships strong and patrolling the Kattegat at the end, merged into nothing; `air` 10 in ten, `patrol` the same 14 and 1. It ends by counting the raid's messages by type. With `air` the raider's air unit with the most `sea_attack` that reaches such water is given the raid on convoys from the air and its country is played: `ITA ENG 30 0 air` sinks a transport a raid, 5 raids by 6 January, its organisation worn to nought by then (`docs/mechanics/navalmissions.md`, sections 8.1 to 8.3). With `patrol` the fleet patrols that water instead, its country played, and each fleet it takes up is told with its `PATROLTRACK`: on 2026-11-14 `GER ENG 30 0 patrol` told seven between 5 and 8 January, the plain run 46 transports and 12 escorts before and after (`navalmissions.md`, section 3.1). Run it after touching
  `lib/OpenHOI3.Sim/Missions`, `CConvoyRaid` or what a convoy does about an attack.
  `OpenHOI3.SaveCheck firstdays <install> <start-date savegame>` sets a new game as a player
  gets it, its two days run, beside the save country by country. On 2026-11-14, with the Lua's changes of law run after
  the days, a new game's shares, manpower and history decisions as the original's (`politics.md`, section 12;
  `production.md`, section 1; `events.md`, section 13): every brigade's strength and organisation, technology, ability,
  officers, influence, manpower and line of the queue, and every stockpile's metal, rare materials and money, the save's;
  Germany's every figure but 0.030 of supplies. What differs is the AI's - shares it has moved, headquarters it has made. On 2026-11-21 the others' supplies are 62 of 77 and their fuel 76 of 77, every other figure the save's, the player's Germany whole (`stockpile.md`, section 2); `points=TAG,TAG` after the save prints those countries' goods at the four points of the two days.
  It also sets every province's nationalism and every pair's occupation policy beside the
  save's: on 2026-10-14 nationalism 5 of 5 and no policy on either side (`occupation.md`, section 6).
  The like-for-like is `supplypass <install> <start-date savegame> countries`, below: with
  `history=GER:training_laws+industrial_policy_laws history=DDR:industrial_policy_laws
  history=DFR:industrial_policy_laws` after it - the laws the AI changed only after the days
  were run - Germany agrees in everything but 0.030 of supplies, and the others'
  organisation is 2770 of 2770 where no commander is over them and 764 of 764 where one is.
  Run it after touching `CTick.RunFirstDays`, the
  daily pass's order, or anything that asks `IsBeingMade`.
  `OpenHOI3.SaveCheck airbattle <install> [attacker] [defender] [seed]` is the trial of the
  battle in the air: the two countries' nearest pair of air units with an air attack, the
  second given an intercept over its own base and the first air superiority over it, and a
  week told - Germany against Poland with no tags given, ten battles over Warszawa in
  two days and eight more over Tomaszów. On 2026-11-21 it tells one day: two battles over Warszawa and a third over Trebnitz, the German unit home with no order after it. Run it after touching the `CAir` classes of `Sim/Missions` or
  `Sim/Combat`, or `CIntelligence`.
  `OpenHOI3.SaveCheck nuke <install> [attacker] [defender] [seed]` is a trial too, no save having a bomb: the first country is given one and its air unit with the most strategic attack sent at the second's province with the most industry in its range, what stood there told before and after. On 2026-11-09, Germany on Poznan: 2.0 off Poland's unity scaled to 1.9, every building at nought, the goods gone, brigades down under 3% and their organisation to about 2 (about 4% and 1.8 on 2026-11-21) (`docs/mechanics/nukes.md`). Run it after touching `CNukeMission`.
  `OpenHOI3.SaveCheck bombing <install> [mission] [attacker] [defender] [days] [seed]` is
  the trial of bombing: the first country's air unit with the most of what the mission
  fires, sent at the province of the second's worth most to it, each sortie told and at the
  end what the province has lost - Germany's strategic bombing of Poland for three days
  with nothing said, which leaves a tenth of Poznan's factory. The missions are
  `strategicbombing`, `logisticalstrike`, `runwaycratering`, `installationstrike`,
  `groundattack`, `interdiction`, `portstrike` and `navalstrike`. Run it after touching the
  `CBomb` classes of `Sim/Missions` or `Sim/Combat`, `CAntiAircraft`, `CProvinceBombing` or
  `CProvinceRepair`.
  `OpenHOI3.SaveCheck script <install> [kind]` needs no save: it reads every event and
  decision and lists each problem in them, of one kind if a kind is given - `UnknownKey`,
  `UnknownName`, `OtherCase`, `WrongShape`, `Structure`, `Quirk` - and counts what it read by
  kind. **The base game should give fifteen problems, all its own** - four in its events, seven in its on_actions, whose battle events Their Finest Hour empties, two unknown keys, `unit_transfer` in two rebel types, which the original drops, and since 2026-11-19 two names in `cb_types.txt`'s `peace_order` that are no casus belli type
  (`docs/mechanics/script.md`, section 10). Run it after touching `Content/Script` or a list a
  key may name.
  `OpenHOI3.SaveCheck conditions <install> [tag]` is a look and not a check: it asks every
  event's trigger and every decision's `potential` and `allow` of every country on the start
  date, and lists what held and every condition that answered false unasked, with why
  (`docs/mechanics/triggers.md`, section 8). On 2026-10-12 the base game gave 16 events
  holding for somebody, 5 of them only through an unbuilt condition inside a `not`, and two
  decisions allowed. On 2026-11-21: 25 events holding and no decision allowed. Run it after touching `Sim/Script` or anything a condition reads.
  `OpenHOI3.SaveCheck effects <install> [tag]` is a look too: it runs every event's and
  decision's effects once, one after another on one new game in the country's scope (Germany
  by default), and lists what did nothing for want of what is not built. On 2026-10-12 the base
  game ran 2,882 effects in 615 blocks in about a second, `country_event` the most often unbuilt
  (`relation` was, until diplomacy's first piece on the same day).
  **Nothing in the files should trip it**; run it after touching `Sim/Script` or anything an
  effect changes.
  `OpenHOI3.SaveCheck events <install> [tag] [days]` plays a new game with only that country
  played (Germany for 180 days by default) and lists every event that fired, when and for whom,
  what is left open for the player, and the decisions offered him. A look: no save keeps what
  fired. On 2026-10-12 the first 120 days fired 46 events of 10 kinds, the death of George V for
  Britain on 14 January among them; on 2026-11-07, with the seeding corrected, the 180 days fired 129 times, 25 events
  (110 times, 26 events, under the old seeding the same day); on 2026-11-20, the event files read in the original's
  order (`mods.md`, section 2), 99 times, 24 events, where the build before gave 118 and 29 - the events are asked in
  the order they were read, and the draws fall to others. On 2026-11-21, 107 times, 23 events: the entry's two days ask events, and a new game's ministers are drawn as a load's with one AI pass before the first hour (`ministers.md`, section 1). And later that day, the unit AI's countries run side by side with a generator each (`agents.md`, section 12): 100 times, 27 events. Run it after touching `Sim/Events` or the tick's order.
  `OpenHOI3.SaveCheck scripttext <install> [tag] [event id or decision name]` writes every event
  option's tooltip, every `immediate` block and every decision's two tooltips as the country sees
  them (Germany by default), counts them and lists the key-shaped keys no table has - or, given
  an event or a decision, writes that one out whole. A look: nothing keeps what the original
  wrote. On 2026-10-12 the base game gave 689 texts, 2203 lines, none failing, in 150 ms (2282 lines on 2026-11-21), and
  event 2602's option "Edward VIII will become Head of State." Run it after touching
  `Interface/Script` or a condition or effect it asks.
  `OpenHOI3.SaveCheck techgap <install> <start-date savegame> [examples] [tag]` sets a new game's technology levels
  beside the start save's and sorts what differs. The save holds 476 levels above the history's, which **a new game
  gives since 2026-11-02** - the original's head start, bought with each country's core leadership
  (`docs/mechanics/research.md`, section 9): **28,987 of 28,987 the same**, where they were 28,511. Run it after
  touching `CStartTechnology`, research's cost, the technology files' reading or a province's leadership or cores.
  `OpenHOI3.SaveCheck diplomacy <install> <start-date savegame> [examples]` makes a new game
  with the install's Lua and sets what its countries hold of each other beside the save: every
  pair block's relation and threat, each country's neutrality and effective neutrality, its
  most-threatening country and where it stands on the alignment triangle. On 2026-10-12 it gave
  **7,878 of 7,878 pairs** for relation and threat, 101 of 101 for both neutralities and for the
  most-threatening country, and **50 of 101 for alignment** - all 11 of the factions'. On
  2026-10-13, with `SpyMission` built and the base game's whole `autoexec.lua` loading, it gave
  the same with no Lua error (`docs/mechanics/lua.md`). The alignment misses were the AI's opening laws and the cabinets the
  original fills as a game is entered: with both built it gives **79 of 101** (2026-10-14, `politics.md`, section 12), and 80 with the engine's own new-game pass
  (2026-11-02, section 13), and **100 of 101** once the Lua's changes of law came after the two days, with the shares,
  the manpower and a minister's first type of the same day (2026-11-14, section 12) - the rebels the one left; 101 of 101 on 2026-11-21;
  `laws=save` after the save gives every country the save's
  laws before the two days (`docs/mechanics/alignment.md`, *Ours*), and
  `explain=<tag>` prints a country's drift term by term as its first step weighs it, and
  `screen=<tag>` (or `<tag>:<player>`) is a look at what the diplomacy screen says of a country as
  Germany sees it - the panel, the status lines, the relation list's first rows with their
  tooltips, the marker's drift tooltip and each faction's (`docs/mechanics/diplomacyscreen.md`).
  Run it after touching `Sim/Diplomacy`, `lib/OpenHOI3.Lua`, the history's diplomacy or anything
  an alignment term reads, and the look after touching the screen's text.
  `OpenHOI3.SaveCheck aiscore <install> [function] [tag]` is a look: it makes a new game with the
  install's Lua and asks the eleven `DiploScore_*` functions that take nothing but tags for every
  pair of countries owning land, the recipient's AI answering, and says what they answered, what
  failed and what they read that is not built; given a tag, that country's answers to each
  (`docs/mechanics/lua.md`, section 6). On 2026-10-13 it asked 6,006 pairs of each in about eleven
  seconds and **only `DiploScore_PeaceAction` failed, every time, at `CAIIntel`** - none fails on 2026-11-21, `CAIIntel` built since. No save keeps what
  the original's AI answered. Run it after touching `lib/OpenHOI3.Lua/Api` or anything a method
  there reads.
  `OpenHOI3.SaveCheck trade <install> [days] [tag] [quiet|timed]` is a look too: it plays a new
  game with nobody playing and the install's Lua trading for every country, and tells the offers,
  the answers, the routes made and ended and every Lua error - `quiet` the hour with the scripts' trading left out, for timing - so no trade is offered at all - `timed` the
  time each Lua method took. On 2026-11-16, each country trading once a day at its hour (`trade.md`, section 9), ten days
  made 48 offers, 45 accepted and 45 routes standing. On 2026-11-21, 58 offers, 56 accepted and 55 routes standing. On 2026-10-13 ten days made 285 offers, 264 accepted and 194 routes
  standing, with no Lua error, under half a second a day; on 2026-11-07 142, 138 and 71 - 145, 142 and 76 under the
  generator's old seeding, so the fall came before it - and the trial asks each command `MayExecute` before it runs it,
  as the simulation does, where it had crashed on a unit gone (`docs/mechanics/trade.md`). **The trading runs
  side by side on eight Lua states of its own** (`CAiWorkers`; `trade.md`, *Ours*), and comes out the same
  as one state running the countries in turn: compare this trial's output before and after touching
  anything the scripts read, since a script may only read the state and post - something it reads that is
  made on first use has to be made before they run (`CTradeAi`), and nothing it calls may change the
  state. `timed` runs the countries in turn on the game's own state, which is the one it times. Run it after
  touching `Sim/Trade`, `Sim/Agreements` or the Lua classes trade reaches.
  `OpenHOI3.SaveCheck declare <install> [actor] [recipient] [limited] [hours] [peace] [occupy]` is a look too: it says
  what a new game holds of war - the casus belli types, the history's wars with their goals, who is
  mobilised - then has the actor declare on the recipient with the first goal the declare-war dialog
  offers, whether or not its row may be pressed, and runs some hours with the AI answering the calls
  to arms, telling who is on which side (`docs/mechanics/war.md`). Germany on Poland for 48 hours with
  nothing given; `who` instead lists every pair whose row may be pressed - none on a new game.
  On 2026-10-13 Italy against France brought the Allies in whole, the Soviet Union against Manchukuo
  had Japan called and accept. With `peace` after the rest the actor then offers the recipient peace as the
  row would and the hours run to the answer: Turkey's to Iraq scores 66 (70 on 2026-11-21) (79 later that day) and is accepted at the next hour.
  With `occupy` the recipient's units are taken away, the actor given all it owns to hold, and a day run:
  on 2026-10-13 Ethiopia became Italy's puppet, Austria was annexed by Germany and France went into exile in
  London. The land is given as a capture gives it, so it also says the occupation policy the capture set: on
  2026-10-14 `collaboration_government` for Italy towards Ethiopia and `civil_war` for China towards Guangxi, as
  the original's saves of February and April hold (`occupation.md`, section 6). With `lend=TAG` after `occupy`, that
  country first lends the recipient two divisions, one on a neutral's ground and one in its own seat, and says what
  became of each: on 2026-11-14 `GER FRA occupy lend=ENG` sent the first home to London, both sides told, and left the
  second France's on loan (`expeditions.md`, section 5). Run it after touching `Sim/Wars`,
  `Sim/Occupation`, what a declaration reads or `CExpeditions`.
  `OpenHOI3.SaveCheck faction <install> [actor] [recipient] [neutral]` is a look too: each faction of a new game with its
  members, rules, progress - the start save's 115, 263 and 173 of 1172 - and victory conditions met, how many countries
  each leader's join and invite rows are open to - none on 1 January 1936, every neutrality being too high - and then the
  actor sending the recipient the faction row that applies, `neutral` setting the joiner's neutrality to nought, with the
  install's Lua answering: on 2026-10-13 Hungary asked Germany and was taken in, Britain's invitation to Sweden scored 0
  (`docs/mechanics/factions.md`). Run it after touching `CFactions`, `CFactionOffers` or `CFactionProgress`.
  `OpenHOI3.SaveCheck politics <install> <start-date savegame> [examples] [laws=save]` sets a new game's government
  and laws, the install's Lua having looked at every country's laws and its two days run, beside the start save country
  by country: the ruling party, the government, who holds each post, the last election, national unity, dissent, the
  laws, each party's organisation and popularity and the cabinet. On 2026-10-14 the ruling party, the government and
  the last election 101 of 101, organisation 1010 of 1010, the posts 1009 of 1010, the laws 703 of 707 and national
  unity 98 of 101; `laws=save` (the save's laws in place of the AI's) gives 707 and 99 - and since 2026-11-02, with the
  engine's own new-game pass before the Lua's (`politics.md`, section 13), **the laws 707 of 707 and national unity 99
  of 101** without it. Popularity and the cabinet
  carry the drift's draw on a generator whose seeding is ours, so popularity is set beside the save to within five
  points (`docs/mechanics/politics.md`, section 10). **Dissent and national unity are 101 of 101 since 2026-11-14**, a
  new game's shares being set once its units are read, with the standing army's term, and the Lua's changes of law
  run after the days (`production.md`, section 1; `politics.md`, section 12); dissent was 85 of 101 from 2026-11-02. On 2026-11-21 the posts are 1010 of 1010 too, the cabinet 863 of 1010 and popularity within five 903 of 1010. `seed=N` after the save plays the save's player on that seed and runs the first frame's AI pass, telling whose leadership shares it moved - Vichy's alone at the 1941 and 1943 starts with the original's 5 (`ministers.md`, section 1).
  Run it after touching `Sim/Politics`, the history's government or
  the Lua classes the politics script reaches.
  `OpenHOI3.SaveCheck foreignai <install> [days]` is a look: a new game with nobody playing and every minister at work
  (180 days with none given), telling every offer the foreign ministers make but trade, by kind and value, how each was
  answered, the first sixty with the decisions taken, the pacts standing at the end and what of the Lua failed. On
  2026-10-16, 180 days: nine influences, three alignments and ten licences, no NAP, guarantee or alliance, no Lua error
  (2026-11-07, the seeding corrected: twelve influences, three alignments and fourteen licences - 26 under the old seeding) (2026-11-21: 25 influences offered and 15 taken, three alignments and five licences),
  the countries aligning and influenced within days of the original's in the Ireland game's saves
  (`docs/mechanics/foreignminister.md`, section 9). Run it after touching `Sim/Ai`, `Sim/Agreements` or the Lua classes the
  foreign minister reaches.
  `OpenHOI3.SaveCheck aistrategy <install> <start-date savegame> [tag | explain=<tag>:<tag>] [examples]` makes a new game
  as a player gets it and sets every country's AI strategy beside the save's `ai` block: the personality and each list -
  `conquer_prov`, `defend_prov`, `threat`, `antagonize`, `befriend`, `protect`, `rival`, `vassal` - entry for entry, in
  order and weight; given a tag, that country's lists both ways, and `explain=` the figures one country's terms of
  another are made of. On 2026-10-16, against `Germany1936_01_01_00.hoi3`, **all eight lists and the personality agree
  for all 78 countries with a strategy**, the save's other 23 blocks being empty (`docs/mechanics/aistrategy.md`,
  section 13). Run it after touching `Sim/Ai`'s strategy classes, anything a term reads - casus belli, factions,
  neighbours, the supply network's areas, a country's brigades - or the victory conditions.
  `OpenHOI3.SaveCheck theatres <install> <start-date savegame> [tag] [examples] [trace=TAG]` makes a new game as a player
  gets it and sets every country's theatres beside the save's `area_theatre` blocks: each theatre's number, its fronts -
  provinces in order, enemy, heat - its own provinces in order, its key province and headquarters; each headquarters
  made, by name, place and brigade; and the next theatre number. Given a tag, that country's both ways; `trace=` tells
  one country's making step by step. On 2026-10-17 **all 108 theatres of all 78 countries agree, and the next number,
  312**; the new brigades 86 of 86 in name and strength, and since 2026-11-14, the Lua's changes of law run after the
  two days, in experience and organisation too (`docs/mechanics/theatres.md`, section 13; `politics.md`, section 12). Run it after touching `Sim/Theatres`, the supply
  network's areas, the front-worthy table or the unit names.
  `OpenHOI3.SaveCheck agents <install> <start-date savegame> <later savegame> [tag] [examples]` makes a new game as a player
  gets it, runs it to the later save's hour, and sets beside the save every theatre's order of battle (army groups,
  armies, corps, divisions, fleets and air units under its headquarters) and plan - active, the three stances, the two
  powers, the area of operations in order, the objectives and the aircraft wanted - and each country's fleets and air units;
  given a tag, that country's fleets and air units both ways. With the start save given twice it is the first screen: on
  2026-10-17 every theatre's army groups, armies, corps, divisions and fleets 106 of 106. `_01_01_00` to `_01_04_12`: **active, the
  stances, the area and the objectives 106 of 106**, the ladder 87 to 99 of 106 by level, air units 99 of 101 countries,
  fleets 95 of 101 - on 2026-11-02 the ladder 91 to 99, air units 96 and fleets 97, on 2026-11-09 the ladder 104 to 106 with the pick of divisions as the running game showed it and the AI's lending of units; what differs is the powers
  (`docs/mechanics/agents.md`, section 14). **Since the third piece, 2026-10-19**, it also sets the plan's axis and embark
  and debark beside the save, and every division and air unit of a country nobody plays: where it stands, whether a
  division moved or is on its way and where it is bound, and an air unit's mission whole, and the land each country holds
  of another; and it counts, each day, the land units left marked falling back with no way, which nothing would clear -
  0 to 2 February - the axis 106 of 106, **777 of 908 divisions moving or staying as the save's and 298 of 542 bound for its
  province** (2026-11-03, with the may-leave test, `moving.md` section 7.6; 774 and 275 before; 779 and 298 on 2026-11-07
  with the seeding corrected), embark and debark 93 of 106, fleets 221 to the save's 222, **air units where the save has them 53 of 59 and
  their missions 59 of 59**, Italy's three intercepts an hour before the save's - on 2026-11-21 884 of 908 divisions where the save has them, 905 moving or staying as its and 532 of 542 bound for its province, embark and debark 101 of 106, air units 58 of 59, fleets 221 to 222 (`docs/mechanics/moving.md`, section 21).
  `player=TAG` plays another country than the later save's player - the Ireland game's saves have Germany run by the AI -
  and `trace=TAG` writes each pass of that country's land block and attack: the split, every position's want, have,
  standing and cap, each move and each halt with the step that posted it, and each attack's candidates, odds, bar and
  why a candidate was refused - and each day where each of its fleets and air groups is, with its base, way, order, an
  air group's wings and its lowest organisation, and its wars as they change, a war with an empty side among them; and
  why each air group is or is not merged when the regrouping runs; with `tracedays=N`, the trace for the last N days only,
  which a run of a year or more needs. `ready=TAG:TARGET,TARGET` says what the readiness for a prepared war counts on the
  country's fronts facing each target at the save's hour - each front province, ours and the save's divisions in it - and
  the front day by day for the last 45 days, the country's corps and its brigades' organisation against their ceiling
  and the save's; `unit=NAME` writes that land unit hour by hour for the last eight days, with its ceiling, its
  commander's bonus and the chain above it. On 2026-11-02, to the Ireland game's autosave of 1 July 1937, Japan holds all
  six China front provinces and is ready, as the original's is (`docs/mechanics/moving.md`, section 21). Run it
  after touching `Sim/Agents`, `Sim/Theatres` or anything a plan reads.
  `OpenHOI3.SaveCheck politicsai <install> [days] [tag]` is a look: a new game with that country played (Germany for
  sixty days by default) and every other country's politics minister at work, the hourly trading left out, telling each
  law, minister and mobilisation they post and what of the Lua failed or read what is not built. On 2026-10-14 a hundred
  and ten days changed 198 posts in 64 countries, where the original's changed 194 in 63 between the start save and one
  of 20 April 1936, with no Lua error (2026-11-07, the seeding corrected: 204 ministers appointed and 39 laws changed) (2026-11-21: 220 appointed and 48 laws changed, 201 posts differing in 65 countries) (220 and 50 with the unit AI side by side) (`docs/mechanics/politics.md`, section 12). Run it after touching `CPoliticsAi` or
  the Lua classes the politics script reaches.
  `OpenHOI3.SaveCheck espionage <install> <start-date savegame> [later savegame] [examples]` sets a new game's spies,
  the install's Lua having given the relations and its two days run, beside the start save: the free spies, and in
  every presence the level, the priority, the nine mission weights and the covert-ops points; given a later save, it
  plays on to that save's hour and sets the player's country beside it. On 2026-10-14 every home level 101 of 101,
  every priority, weight and point 10201 of 10201, **the levels abroad 10045 of 10100** (10052 on 2026-11-02, 10055 on 2026-11-07 with the seeding corrected) - each that differs a spy caught
  on the two days by another roll, the original catching more: 33 to our 22 with the triggered modifier `neutrality`
  built, 18 before it - and the free spies 100 of 101, Germany's for its history's laws - 101 of 101 since 2026-11-02, when the player's laws
  came to be brought forward too - the levels abroad 10053 of 10100 on 2026-11-21 (`docs/mechanics/espionage.md`, section 14). Run it
  after touching `Sim/Espionage`, the relations a new game gives or the espionage share.
  `OpenHOI3.SaveCheck espionageai <install> [days] [tag] [save]` plays a new game with that country played (Germany for
  110 days by default) and every other country's intelligence and politics ministers at work, the trading left out, and
  tells how many ministers acted and what they posted, and what of the Lua failed or read what is not built; given a save
  of the day it ends on, it sets every AI country's priority towards every country owning land beside it. On 2026-10-14,
  to 21 April 1936 beside `Germany1936_04_20_03.hoi3`: all 77 AI countries owning land had acted, no Lua error, **5921 of
  5929 priorities the save's** (5925 on 2026-11-07, the seeding corrected) - on 2026-11-21 all 78 had acted and 6067 of 6084 priorities are the save's, 16 of ours higher, SPA's among them (6065, 17 higher and 2 lower, with the unit AI side by side) (`docs/mechanics/espionage.md`, section 15). Run it after touching `CEspionageAi` or the Lua
  classes the intelligence script reaches.
  `OpenHOI3.SaveCheck profile <install> [days] [tag]` is a look at speed: a new game as the client makes it,
  the install's Lua loaded and one country played (Germany for 30 days by default), run hour by hour with
  `CGameState.Profile` set, so the tick times each of its steps; it tells an ordinary hour, an hour that turns a
  day, the slowest hours and every step by its time. Build it in Release for figures worth comparing. On
  2026-10-13 a day took 0.43 s, an ordinary hour 15 ms - 11 of them the AI's trading in the install's Lua, run
  side by side - and an hour that turns a day 90 ms. On 2026-11-16, 760 ms a day before each country traded once a day
  at its hour and 530 to 570 after: an ordinary hour's median 22.6 ms to 14 ms, the trading 11.3 ms an hour to 2.5, an
  hour that turns a day about 120 ms either way. On 2026-11-21, 547 ms a day, an ordinary hour's median 12.9 ms and an hour that turns a day 109 ms. `warm=DAYS` plays that many days before anything is measured, `inturn` runs the unit AI's countries in turn where they run side by side, to time the one beside the other, and the run tells the world it measured. The original, at its fastest on the July 1941 save, plays an hour a frame,
  35 ms, 0.84 s a day with no step at midnight; ours on that save, headless, 62 ms an hour (74 before). Run it after
  anything that may have made the hour slower.
  `OpenHOI3.SaveCheck headless <install> [until year or date] [savegame ...] [inspect=TAG]` is stage 2's run: a new game
  with the install's Lua and nobody playing, run hour by hour to the year given (1948 by default) with nothing given by
  hand. Twice a year it writes the great powers' figures and the world's, each day it looks for goods or manpower below
  nought, as they happen it tells wars, surrenders, annexations and changes of faction, and at the end the time of
  each year, the events and decisions, the Lua's errors and every key of script met that is not built (through
  `CGameState.ScriptReport`, which only a tool sets); given saves of the same start it sets every country's goods,
  manpower, officers, influence and technology beside each at its hour; `inspect=TAG` writes that country's hold on
  its land each day, its land army on the first of each month, what its units and its enemies' on its land are doing
  on the 1st, 11th and 21st - delayed, falling back, moving, fighting, the figures a save can be set beside - each
  retreat as it begins with its whole way, and each land unit it loses, as it stood and with the calls that took it out
  of the game (through `CGameState.UnitRemovedTrace`, which only a tool sets), and each move the AI posts for one of its
  units in a battle, marked where it defends, with the step that posted it (`CGameState.AiTrace`, filtered). A run that throws stops and says on which hour. On 2026-10-16 it ran 1936 to 1948 in 44 minutes
  in Release and found four things to mend (`docs/mechanics/headless.md`, section 4). **Since the same day the tech and
  production ministers act in it** (`ministers.md`): each snapshot also writes every great power's research queue, its
  two sets of shares and its units waiting, and its summed research progress, which is what to set beside a save's -
  on 11 March 1936 the eight majors' are the Ireland game's to within a few levels. A run to 1937 takes about three
  minutes. **No unit moves yet**: what it tests beyond the economy is everything else running unattended. Run it, in
  Release, after a change that a long game could break - and, by the decision of 2026-10-16, a few years at a
  time for now. **Since 2026-11-03 it records and replays** (`docs/replay.md`): `record=FILE` writes the AI's commands,
  draws and unit numbers as the game runs, and `replay=FILE` plays a recording back in the AI's place, the rest of the
  game running again at about a third of the time - **a recording holds only while the rules do not change**, so replay
  one to trace a run further or to see a bug again, never to try a fix; `hash` writes the state's hash part by part each
  midnight, which two runs of one game, or a recording and its replay, should agree on line for line. `inturn` runs the unit AI's countries in turn on the game's thread where they run side by side, which should come out the same hash for hash (`agents.md`, section 12). The client does the
  same with `--record=FILE`, `--replay=FILE` and `--hash`, a played game's player's orders recorded as they come through
  `CSimulation.Post` - the one way into the game from outside, which a replay shuts - and played back by whoever played
  it (`replay.md`, section 8).
  `OpenHOI3.SaveCheck replaycheck <install> [date]` runs two new games hour by hour side by side to the date (1 February
  1936 by default), the first recording its AI and the second playing it back with each step taken through the file's form
  and back, sets them beside each other every midnight and names every country's, province's and unit's figure they part
  on the first day they do, and how long each took. On 2026-11-03, 608 of 608 days to 1 September 1937, the replay's
  hours 224 s to the recording's 584 s. From 2026-11-14 to 2026-11-15 it parted on 5 January 1936, 3 of 31 days the same,
  because the sealift's decision as a land unit enters a province ran in the march outside any AI step; kept as one since
  (`CReplay.RunCalledStep`; `docs/replay.md`, section 3), 365 of 365 days to 1 January 1937 on 2026-11-15, the replay
  153 s to the recording's 354 s. On 2026-11-21, 365 of 365 again, 88 s to 165 s. With the unit AI's countries side by side, later that day: 365 of 365, 82 s to 112 s. Run it after touching what an AI step does to the game, `Sim/Replay` or a
  command's fields: an AI step that changes the game other than by a command shows here the day it does - and so does a
  call from the game's own passes into the AI, which has to go through `RunCalledStep` or be worked out from the game alone.
  `OpenHOI3.SaveCheck replayload <install> [save date ...] [player=TAG] [folder=FOLDER]` records a game beside its saves,
  saves and loads it at each date with each loaded game recorded on, then plays the chain to the last save back from a
  new game, writing and loading a save of its own at each crossing, and sets every midnight's hash beside the played
  game's (`docs/replay.md`, section 10). On 2026-11-25: the base game, saves on 10 and 20 January and 1 February 1936,
  31 of 31 days across 2 loads; BlackICE as Germany, 6, 12 and 18 January, 17 of 17. `takeover=DATE` also saves from the
  playback at that date, plays on from that save recorded and plays the new line back from the start: 24 of 24 on the
  base game. Run it after touching `Sim/Replay`,
  `Content/Replays`, the save's writer or loader, or anything entering a loaded game does.
  `OpenHOI3.SaveCheck reload <install> [date] [hours after] [player=TAG] [save=FILE] [groups=N] [examples=N]` is the
  check of the rule on a whole game's replay below: a new game with the install's Lua run to the date (1 February 1936
  by default), written as a save, the save loaded as a second game, and the two set beside each other field by field -
  every object either holds walked by reflection, the AI's own state with the rest, each path that differs told once
  with its count and examples - as loaded and again as entered, then hour by hour for the hours given (48 by default),
  the hash's parts and the first figures they part on. On 2026-11-14, saved on 1 March 1936 with `groups=5000`: 154
  paths as loaded and 297 as entered, every one under an entry of `docs/enhanced.md`, *A whole game's replay*, or one
  of the two of ours `savegame.md`, section 20, names, the
  check leaving out its own noise (`CReloadCheck.Skipped`; `savegame.md`, section 20), the two games parting in the
  first hour. On 2026-11-21, 229 paths as loaded and 391 as entered - 263 and 451 before that day's first-frame AI pass; sorted again that day, 218 and 380 with a modifier set beside its like by key, three paths under no entry listed since (`savegame.md`, section 20). Run it after adding anything the game keeps, and list what it shows lost.
  `OpenHOI3.SaveCheck supply <install> <savegame>` checks the supply network against what a
  save says of it, with the save's own owners, controllers and capitals, so a save of any
  day will do: the provinces marked `out_of_supply_days` are ours that draw on no depot -
  the same 4,400 in the saves of 1 and 8 January and 20 April 1936 - and the provinces
  marked `capital` are ours that are a capital or beside one, 402 and 400. It also says
  where the supplies lie in each network, which is all a save has on which province a
  depot is (`docs/mechanics/supply.md`, section 9). With `release=PRK:JAP` after the save it
  is a trial instead: the country is given its cores out of its master's land as a puppet
  and the check says who then draws on what - the three states watched in
  the original are `release=PRK:JAP`, the same with `release=KOR:JAP` after it, and neither.
  Run it after touching
  `lib/OpenHOI3.Sim/Supply` - and the start-date check too, since home production, the
  `overseas` modifier and where a unit may be placed all ask the same areas.
  `OpenHOI3.SaveCheck supplypass <install> <start-date savegame>` checks the carrying of
  supplies: it makes the units as the save has them, with the save's laws, ministers,
  technology and mud, runs what the original runs before a game's first hour - a day, the
  filling of the provinces, a day - and sets every province's supplies and fuel beside the
  save's: what lies there, what was asked of it and what passed through it, today and
  yesterday. 184 of 234 networks agree in every province and every figure (204 of 234 on 2026-11-21); the rest differ
  where the original's AI has a unit on the move or has moved a share, and in Moscow's, whose
  garrison is the save's since factions' modifiers apply, and whose network differs for a reason not found (`docs/mechanics/supply.md`, section 17). **The same run sets the convoys it made beside
  the save's** and should say 261 of 261 on every line and 20 of 20 countries, **and the
  money and resources lying in every province**: 88 of 88 harbours to the thousandth, and
  49 of 49 capitals that somebody else holds within a fiftieth
  (`docs/mechanics/convoys.md`, section 12). `network=<depot's number>` after the save prints
  one network province by province, `country=<tag>` one country's day and `convoys=<tag>`
  one country's convoys that are not the save's. Run it after touching the pass, the draw,
  where supplies are made, the filling or anything under `lib/OpenHOI3.Sim/Convoys` - and
  after touching the order of an area's provinces or harbours, which the convoys hang on.
  `OpenHOI3.SaveCheck supplyledger <install> <savegame | from=DATE> [days] [TAG,TAG] [dump=FOLDER]` is a look at where a
  country's supplies go over a stretch of days: each midnight its stockpile, what lies in the provinces it holds, the
  day's ledgers, what its provinces were asked for and passed and how many were asked for more than they can pass, and at
  the end where the goods lie. On 2026-11-15, from the 1941.12.11 bookmark, Germany's stockpile 29,674 to 1,996 by 31
  December and its provinces 29,571 to 66,676 (2026-11-21, twenty days: 29,604 to 16,492 and 28,194 to 65,669) (to 15,504 and 66,444 with the unit AI side by side), nothing lost - the original's throwing back of demand behind a narrow road
  (`supply.md`, section 12.1). Run it when a stockpile empties in a long run.
  Never copy a savegame, or anything from one, into the repository; a formula measured from
  it goes in the spec marked *measured*, with the figure it reproduced.
- Tests are where a silent misreading gets caught. The parser, the layering, the map, the numbers, the tick, modifiers, IC, resources, the stockpile, production, the conversions, leadership, research, manpower, unit types, historical models, orders of battle, what units draw each day, the officer ratio and organisation, the build queue, what a country may build and what a new unit is called, the interface files, font descriptors, the sliders, the three screens' figures and the builder windows', the unit counters and who a finished unit may join, the edges between provinces and a unit's march, the unit panel's figures and the hierarchy's lines, ports and the distances by sea, a fleet's and an air unit's orders, the buildings on the map and the window of orders, a land battle, what the hour makes of a unit in one, day and night, the game's generator of chance, the tactics, what a battle shows, which arrow a unit has, the supply network, the carrying of supplies, what a unit draws and brings, convoys with their heap and their harbours, the supply map mode's colours, who is whose puppet, a battle at sea - the sighting, where the ships stand, who a ship shoots at, sinking, breaking away and falling back - a fleet's missions with the raid on a convoy, the two days a new game runs first, an air unit's two missions with what a country knows and the battle in the air, and the bombing missions with the bombing, a building's two levels and the day's repair, what the window shows of a battle in the air and of a bombing, a mission's area with the air window's ladder, sliders, hours, dates and colours, the list of finished battles with the air map mode's colours, icons, rings and lines, and leaders - their files, their skill, who may command what, the choice of one, how far a command reaches, a leader in a battle on land, at sea and in the air, and the experience and traits a battle gives, the window a leader is chosen in and the original runtime's sort, and the tooltip - what the box shows of one, where it goes, when its second half comes and how a line is broken - with what a province's, the top bar's, the production and technology screens', the sliders', the builder windows', the unit panel's, the deploy menu's, the orders windows', a battle's and the leader window's say, a brigade's summary, a leader's tooltip and what a trait does, a technology's requirements with the corrected indentation and what its levels do, the flag and the counter in text and a modifier's sources written out, and a scrollbar's end buttons - the step, Shift, Ctrl and holding - and event and decision script - its words, the 153 trigger and 91 effect keywords and the names a key may be in the original's order, the tag's three letters and its bucket, `limit` in a scope, an event, a decision, a mean time to happen and the problems found - and what a condition answers - the blocks, the scope's forms and the sets, the leaves as the original answers them and what is reported unbuilt - and what an effect does - its blocks and draws, flags, variables, the modifiers an event gives, a country's and a province's effects - and an event firing and a decision taken - the candidates, the chance of a day, the firing, the AI's option, an answer, the four months, fire-once, the decisions offered and taken, an event's text and where its options stand - and a condition and an effect as words - each block's header and asterisk, a scope on one line, the doubled indent of effects, a run of cores on one line, a random list's chances, a missing key shown as itself, and a wrapped line keeping its indent - and the on_actions: the first event whose trigger holds, a fired fire-once event in front of the rest, a battle's winners and then its losers each counting it, a sea battle with an empty side, and a pride sunk - and messages: a type's keys and the null type, which message a player is told, the header's government and the lines' dark white, and the log's 200 messages, its rows cut one at a time and its rebuild of 101 - and a block inside a `not` with no asterisk of its own - and what countries hold of each other - a record towards every country, the history's one side, a new game's fill only where nought, the month's drift by neighbours, war, guarantees and puppets, a battle's +5, neutrality's day and its effect and conditions, threat spread to everyone and over the map with its floor, its resistance and its fall a step, the most-threatening country, the threat effect, and the triangle's clamp with its sign slip, a member put on its leader, a step every interval and a day and the drift worked out - and Lua: the interpreter's rounding, numbers and library, errors both ways, a new value at each push with one equality for every class, a class's global, its fields and iterators, the install's scripts through its layers and a relation that fails giving 25, `CFixedPoint` as a float and its backwards arithmetic with a number, the single-precision distance and resource figures, the API's friends, enemies, factions and tags, what is unbuilt reported once, and an offer put to the recipient's AI with a failure a refusal - and the diplomacy screen: its lists and the bytes they sort by, its panel, the ceiling for war, the status lines in turn, the relation list, the marker's place, a breakdown at its constants, a faction's rules and the drift tooltip with its top line - and the agreements: an offer charged and its diplomat on his way, the AI's answer over 30 and under a draw, each pact's arms with the keys each side is told, the endings by themselves, the upkeep, the screen's rows in order, greyed and said, and the dialog's lines and chance, and the pacts an effect makes and the conditions that see them - and trade: a route's price to the save's truncation, its two blocks set from one slider, barter and the free gift rule, who buys and owns the convoy, whether one is needed, the day's exchange, inactive for a convoy or for goods and its sticky days, the ends by cancel, war and embargo, one acceptance an hour, the Lua's trade action, goods values and routes, and the window's amounts, offer text and accept, a list line's icons in their columns, its tooltips and the cancel dialog's backwards text, the runtime's `rand()` from 47 and automate-trade's one acceptance an hour, an offer put to the player once with its icon's tooltip, blink and week and its dialog's text, and the trade messages' goods from the side told - and aid: debt allowed and revoked at the debtor's cost and repaid an 18,250th a day for ever, a war clearing it, a shared theory read at the higher level and a new sharer ending the old, the day's check and the leadership drain, lend-lease's refusals and ways, its offers and the hour's look, the giver's day and the receiver's whole IC as the reading's worked day has them, a licence's fee and its decline for want of money or its debt, and the licence action in Lua, and a province counting its own changes of hands, and war: a declaration's sides, puppets, guarantors and factions, limited war, a name's order, the row's gates and truce, a call to arms both ways and the player's unasked, a goal's monthly cooldown and spent casus belli, the null casus belli and the condition by the types, the rows and their tooltips, a goal's setup text and the call's dialog, and peace: what changes hands and to whom, the lone side leaving, a war's goals deleted with a leaver, the players told, the day's return, a capital's quarter looted, the surrender progress over owned cores, the AI's intel with a draw a unit, and the row and the dialog's account of who cedes what, and the capitulation: annexation's land, wars and forgetting, a puppet joining its faction leader's wars at the back, a surrender at the brink with its victor's annexing goal, an exile in its leader's capital with a tenth left, and a fallen capital looted and a government going back, and a battle's capture - to a puppet's master, to a fighting friend beside it, to an exile's head only against an enemy owner - with its loot and a defender's fall-back damage, and factions: a join's war sweep, the join and invite rows with their greying and texts, an accepted join bringing the joiner's puppets and ending its alliances with the members, the Allies' guarantee and call, a faction's modifier and an aligned outsider's resources, leaving by script, progress by holder and the share's cut, and the choice of primaries and the day's test, and the hour's trading run side by side in the countries' order with a random of each state's own, and the government - the parties from the history, the monthly drift, the cabinet's seats and a fractured government, elections, national unity's day and strategic warfare, the filling of posts, and the conditions and effects on them, and a law or a minister changed with the peace-time revert, and the politics screen - the elections line, the pie's whole percents, the choice of a minister and Replace, a law's price and choice, the change factors and the strategic-warfare figures - and the AI's politics: a new game's laws moved to their fixed point with the player's left alone, the minister's countdown and hour, the refill that never asks the head of state, and the law database, the null minister and null law, a law command's two flags and the commands a minister posts in Lua, and spies: a presence's shares, the bands and the cursor that does not wrap, priority 1 stopping at seven, a catch's friend rule and its two rolls, covert-ops points, a party boosted, the unity modifiers, military intelligence's day and grants, a steal's pick, the two commands, the coup's cost, covert operations, the two conditions and a new game's score, and what a country knows - the figure for another country, the day's rebuild with the land beside and the step by decryption, the hour's raise and its unscaled step, a whole rebuild for a changed pact, radar and local_intel_boost, and the intelligence screen - its lists and the Priority sort, the first chosen country, the priority buttons, the spies' colour, the foreign-actions mark, the production roll, the estimates and their question marks, and an estimate's and the coup's tooltips, and the AI's spying - a presence and its primary mission in Lua, MissionAllowed, who is spying on us, faction members as allies, the minister's commands and their exact arguments, and its countdown and hour - and triggered modifiers: their file, both blocks needed, the list in the order they came on, the day's rebuild testing first, the player told of one come and gone, and the Strategic Effects window's rows by `potential` in file order and the original's tally of a `trigger` block - and occupation: every record at `no_occupation`, a capture's first policy or `civil_war` keyed by the old holder and a set one kept, a province given its holder's policy towards its owner with no war asked, the command that checks nothing, the history's lines and annexation forgetting them, nationalism on a change of owner and the month's 83, its static by the years and its condition, the coast, land or sea and `no_adjacent_controlled`, and the occupied countries, the choice and their tooltips, and when a province is made again - a battle's capture and not a plain change of hands, a change of owner, a moved building at the hour's look, a repair across a whole level, the command's provinces alone, and a given modifier outliving its end - and revolt risk: a friend's units by their strength, two edges of one holder's land and the province twice, the day's step to the support less the cut and frozen at peace, the figure read and shown, the static by it, the conditions strict and on the rebels' land, the effect and a new game's support, and the tooltip's lines and the map's colours - and rebels: their file, the roll's draws and its threshold rounded to even, the type a province chooses and the country it frees, a rising zeroing the risk, its faction and its army of one to four at the occupier's efficiency and below its technology, partisans handing land back, a second rising joining, an exile's partisans, the siege and the land dropped, the day's hand-back and disbanding, independence and its war, the month's delay, `release` and a rebel type as an effect, and the tooltip's likely rebels and a province lost - and undergrounds: a finished one's strength by its air base, where one may go, finding rounded half to even, growth and liberation, partisans beside it, the spread walked the next day, dissolution on a capture, Organized Revolt and its tooltip, the mission kept, and the map's red - and building lines: the price by the category discount alone, an order with no province never refused and one in a province joining the last line there, a finished level built and not working, a building waiting placed where the country holds, a line in lost land cancelled, availability by technology, the dialog's cost, time and mark, the line's name and counter, and the placing map's grey and green - and the province view: the building columns split behind an empty place, a row's key matched with case, the pips green, orange and grey and below intelligence 6, a button enabled by any selected province, the first reason and the coastal line, a pip's damage and more built, the figures' full intelligence, the weather strip, the intel keys one out, a way by its kind, the underground's strength, twice the suppression and the mission's step, and allied objectives kept both ways and cleared on leaving a faction - and convoy lines: the price by the defines and the technology with its floor, set once and passed over by the queue's pricing, an order never refused, ten ships an item with what was paid past it kept, no test of land held, the dialog's time in thousandths and the line's name and counter - and weather: the season by the day and latitude, a province's hour against the original's Phongsali, rain's burn-off, frost between -5 and 0, the arc by C's remainder, a front's step and spread, the high pressure's walk, a front's birth and its draws, an edge's bearing, the climate index and the zones read, and the weather map's text and colours - and the settings file's map options read and alwaysCounters off reported - and transport: a fleet's room, boarding by command and by marching, getting off, too much aboard, a transport's damage shared out, the landing's march, wait, scales and penalty, the two missions and the warning, and the screen's buttons, list, counters, arrow and lines - and the carrier and carrying by air: a carrier's room, the rebase to carrier and carrier protection, a group following its fleet and let go when it sinks, boarding a plane, the paradrop with its delay, supplies, penalty, width and settling, the air supply, and the plane buttons, the air window's rows and their order lines - and selection: one kind and the player's own, Ctrl's whole command, the drag and the box's three walks with their margin and the map's seam, the selection groups and their command, the multi-unit panel's line, checkboxes, rows, wheel and carrying, and the orders window's rows greyed for several fleets, and a unit's draw where its capital's pool went below nought, and `country_event`, `split_troops`, a unit type given as an effect, `lost_national` and a province's count of units for the Lua, and the tech and production ministers - their hours, a finished level taken off an AI's queue, the AI's shares written as given and rounded half away from nought, `FastNormalizeByPriority`, desperation, a brigade's and a division's roles and the placing of a finished unit, the queue's priority, the spare manpower, a level's year, the databases by their exact keys, the AI's unit and building orders and an elite brigade's ration by its type's group - and the foreign minister - its first work and hour, the decisions pass in one scope with one draw, an answer to the nearest and a failure ending the pass, the prepared wars from Lua and their rules, the pruning, the walk that gives up transit rights and posts a ready war's decision, and a country's flags in Lua, a new minister's skipped first work, the day's proposal kept and posted above 50, the proposed war, the share occupied, a ready war declared limited, every minister told of a new pair at war, the action classes made and pressable by their kind, and a failing decision ending the tick reported once - and the AI's strategy: a list's add, cut and scaling, the personality's three scores and its share, the threat's border, faction and reach, antagonism's terms and the rival between equals, protection's cap of six, the walk of the victory conditions with its or, not and region, when a strategy is made, the prepared wars it drops, and its weights in Lua - and fronts and theatres: an area's fronts and a country's with their heat, the front-worthy table, a theatre of a front's own and the rest placed, a merge, the old theatre kept by overlap, a front's chain, a headquarters adopted, made and disbanded, readiness for war, names per country and what marks theatres - and the unit agents: an agent made with its theatre and bound by the pass, the sort into theatres, commands made a level at a time and filled five to a command, a new command's brigade and name, fleets and air groups split and merged, the stances, the area, the objectives, a unit's power and the aircraft wanted - and moving and attacking: a garrison's rating, the position cap, committing by the powers, the area's peacetime scores, a long way made a strategic redeployment with its speed, a support attack that never advances, a halt, the odds' ends, the air's close support over a beach, a worn group called off and the power ratio, and a province no way leads to - and the replay: a command written and read back, a live object refused, a step played back and one out of step, a recording on disk, a player's commands run where they ran and a replay taking none, a recording refused by a game played otherwise, and the state's hash - and the outliner, its sections, a unit's tree and its rows' figures and tooltips - and the land orders window's rows and tick, its date and the two orders' tests and hours - and the reorganise window's rows and fleet lines, what may be moved and when two units may be reorganised, and a format in quotes - and a merge's rule, its choice of the unit kept and its leaders - and the new-unit button's rule, Ctrl's and the split - and the message types: the player's file over the install's, a type overwritten whole in its place, the file written as the original writes it and Reset, and the message settings' tabs, boxes ticked where off and their tooltips with the pause box's slip - and the player's settings written in the original's order and format and read back the same, the keys written only off their defaults and a small size raised, and the game options' autosave wrap, the refresh rate looked for among the first mode's rates, a changed resolution's restart and bracket, multisampling's round through nought and Apply's store with the distance five times its slider and the least speeds - and a save's values, its dated hour, its five-place shares and its ids, and a save's units - the ids kept and numbered after the counter, a tree read whole with a carrier's group and a passenger put back under its superior, an order and its target, the leaders in service and the queue's lines - and a save's pacts of every kind with a route's terms and its convoy's number, a war's sides in order and its goal under its actor, a convoy's number, ships, way and goods, and a block with no key holding its own entries, and a save's battles - a side's units taken on as in play with its size overwritten in the database's order, its lists and tactic, a side saved before its first hour, a bombing at the list's front with its bombers' tallies, a finished battle as written and a sunk ship found by its id - and a side counting a brigade's men and a whole ship, again at each join, and a fleet taken out putting its ships on the sunk list, and a save written - the original's layout to the byte, and a province in its order with each key where the original writes it, a unit tree read and written again to the byte with its numbers given back as their pairs, and the leaders, a line and the templates padded to twelve, an intercept's two fleets kept apart and a given order's halt figure by its stance, and an intercept given on a fleet naming it, and the pacts written back to the byte in the order they were made whatever their kind and the history's numbered by date and file, a war made in play written under its name written out and a saved one read back into its parts, a convoy, the battles and the finished battles written back to the byte, and the blocks a game with none of them still writes, and the save dialog's offered name, a typed name's extension, a row's text and the folder's list, and the autosave's six frequencies and three files, and an edit box's typing - the caret's insert, the refused characters, the cap of 250, Backspace at the start, End by the stored length, the apostrophe's step, the wrapped line shown and the blink - and a typed character's Windows-1252 byte, and a country's politics and research queue read and written - the null date as never, the party keys at column 0, the pool on one line and a load's cabinet kept behind the seats - and a country's spies, each list's null country first, and the victory conditions chosen and met by their dates, the fired events and the fronts, and the ten pools written whatever they hold, lend-lease's countries by their number plus one and strategic warfare read unpruned, and a strategy's lists read back as they stand with its theatres and their fronts, and the front end's start out of the bookmarks, a save's head read by position, and the lobby's year label, war line and reasons Play may not be pressed, and the lobby's options - an arrow's wrap, the Technology row's setting and the chat's line - with the difficulty's blocks and combat step, the AI's run of a played country's areas, the tech minister's halves, the ministers made and dropped, and the save's `ai` and `gameplaysettings` both ways, and the screens' automation lines and arrows, the technology arrows' flag, the ministers' draw, the player's daily sliders at his level and the raised level's next hour, and the effect files' preprocessor, parser and translation - conditionals and repeated includes, macros, samplers, passes, mul by a matrix padded or not, the silent conversions, a renamed local, the canvas's drawn texture handed down, the device's states under a pass's with SRCALPHA / ZERO and the alpha test, a shared parameter made a global uniform and the position turned to Godot's clip space - and the map's camera, a point on the land where the 2D view draws it, a tile across the seam and the view moved into the camera - and the far view's country names, a level strip's name, one too long, the trim over the sea and not foreign land, a slanting land's bent name, the spline and the ANSI upper case - and the land borders' corner cache read and its country byte made from the controllers, and the sea zones' border quads by tile - and the map cache's stamp with its subtract-once checksum, a stale or missing stamp refusing the cache and the player's folder asked before the install - and the rivers' trace - the corner dropped, water keeping its pixel and one more on, a junction taken once with the pixel beyond, a mouth's two nodes - their strip's pairs, seam and draws, and the surf's records, its fourth record's larger coordinates and its quarter corners cut towards nought, and map.bin's list of sources read where current and its builder's taken where a file - the river bitmap among them - is newer or missing, and the trees - trees.txt's types and densities, terrain.txt's row by the colour list's place, a draw a pixel that may, every pixel of a density of one, a tile's order by type and province, the template's four flat cards and a tree moved onto its pixel with its row and snow, and the generator's seed shifted keeping the sign, and a province's name laid out - standing on its point, its lines centred, broken only at a space past 320, kerned, turned anticlockwise and cut to 49 - with its label read uncut from positions.txt - and the map modes: the infrastructure ramp's level in single precision, a region's colour handed out in turn and the region met as the list runs out kept white, what a unit weighs by what is known, a colour's parts held at one, and each mode's tooltip lines - the intel level's name, a region's fallback name and its peace star, the resources' goods with crude oil last, the diplomatic status a puppet sees, the infrastructure's bare percentage - and the naval icons' whether and not how many, and a resource icon on the building of its good's slot - and war exhaustion: a new game's by the calendar months at war or since peace, the month's banking under its maximum, the rebels' zeroed, the payback to neutrality at peace, the effect's floor and the condition - and the fog of war: a stack hidden under 2 and unfanned under 3, the unknown picture, `???` and `?` by the unit's own level, a friend's units known whole, and a building by what is known of its province or its `visibility` - and continent painting's colours in the file's order - and the counter's tooltip: every country's units at the place, the leader by rank and skill in order, the forms by intelligence and priority, a bar's tenths cut and full from 99%, and the strength bar in text - and the fog of war off giving every country 9 everywhere - and the map cache's making: the C runtime's `rand`, the raster's two passes, a border's sides and region bit, a run's quad, its welded corners and the corner its raster bit moves, a border quad's texel, a junction's slot and a cut's second half, a beach's kinds and its link triangle, the surf's kind, a texel's four entries, the fallback and the first value offered, the texture's head, a province's tile, and the cache of the player's folder alone, built once and whole, with a way's five numbers, the province raster's head and the format file's declarations - and the end of a game: its result by the tiers, a lobby's ticks fitted to the number and left alone by a new game's own choice, a faction's picture by its exact tag, and a centre-measured text box's arithmetic - and the theatres screen: its lists' words, an objective's score, the province that names a front, one odds figure for a theatre, what a plan wants and is short of with a shortage taken off twice, a group's surplus cancelling, a queue line aimed at a headquarters and a Build's square root of lines - and the Statistics screen: its pages going round, a table first by its rows' names and sorted by number, figure or text, each page's own columns, a pie with nothing in it, and every page made - and the strategic warfare records: a ring's offset and its slot before it, the month a rolling thirty days and a first month of 29, a series newest first and padded, a chart's height and scale, the comparators, running wars first, a part of a brigade's price, and a land hour's loss whole on every enemy's record - and the convoy windows: ships moved and refused, a convoy cancelled and its goods set, a flag turned, who may open or change one, and the go-to box: a number read as C reads it, names beginning with the text, the unnamed left out and a go by name or number - and the order of battle browser: a row open by the next being deeper, a moved unit's new place or none, the scroll, the size frames, the five brigades and dots, the counts of what is under a unit, what may be dropped on a row and a leader's swap - and a brigade's card: a pride found, moved and its dissent, a rename, the pictures tried, a ship's victims and the pride's dialog, and a unit left empty going by the hour - and attaching: the commanders offered in the original's insertion order, when each button may be pressed, a detach, a new headquarters over one unit or several with its men and a theatre of its own - and battle plans: the file read by position and written back, a saved game's visibility, a file's name, a stroke's points, smoothing, spline and strip, a click's hit, erase and a colour, the wheel's size, text placed and an apply - and the diplomacy panel's lists, a battle's message and its popup's columns and names, an election's percents, what may be disbanded, a theatre's line of needs and the air builder's lines - and which Goto a message has and what a builder calls the type picked - and a chain of upgrades closed and a line's price, and what a port and an airfield say, and the console's table, its look-up and answers, and a plan's rows and aggressiveness, and who may be made a puppet or liberated with what each command does, and a front line's walk, joins and strip, and what the alerts test and say, and a NAP's border counted and turned round, and how large the picture behind a main window is drawn, and the nuclear bomb's stock, order and drop, and tribute and a war nobody declared, and the songs' file and the pick, the loading screen's pictures and layout, the install beside the executable and `nextsong`, and a large font's glyphs made into distance fields - the transform, the edge at half solid, the box read alone, the H's 38 rows and the atlas, and a trespasser, a shattered unit and the officers a removed unit costs, the air reserve's exchange and the unit it passes over, a worn rebel unit, an empty fleet's disband, an exile's carrier letting its passengers go, a licence type's combat value and its row's greying and figures, a history's `officers`, the first frame's AI pass, the green ring by an agent or a plan, the credits' lines and roll, a map text's life and layout, and the unit AI's run side by side, and the rules a game carries - the faithful game's, an enhanced one's fixes, their line, the file, the save's keys and folder and what the Lua is told, and a country's own conversions with their command and save keys, and the record of finished battles with its tally and save, and the two pictures a game saves - the map's colours with and without Shift, the names, the ten frames and the bitmap, and a recording kept beside a game's saves - a save's mark, a playback ending at it, the chain by its parents, a file read while it is written and the save's key - carry 3840 of them
  because a wrong parse produces plausible-looking data rather than an error, and the original's
  own engine swallows its parse errors - so "it loaded" means nothing.
- Scripts go in the scratchpad, written with the `Write` tool, not a bash heredoc: heredocs here
  mangle apostrophes and turn `\Users` into a `\U` escape. **That goes for a script fed to
  an interpreter inline as well, and for editing a source file through one**: on 2026-10-09
  a Python edit run from a heredoc turned every `\n` inside the C# strings it was writing
  into a real line break, and the test file stopped compiling. Edit a file with the `Edit`
  tool.
