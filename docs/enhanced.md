# Candidates for the enhanced engine

The normal engine does what the original does, its faults included. An **enhanced** mode changes or fixes some of them (*The shape* and *What is built*, below). This file is the list of what it changes and of what it might: an entry not marked as built is a candidate, and nothing about it is decided.

**The marks.** ✅ built - in the enhanced mode, or in every game where the entry says so. 🟡 built in part, the entry saying what is left. ❌ not built, which includes what was decided to be kept as the original's. *The shape* is what was decided of the mode itself and carries none.

Add to it whenever a spec records something of the original's that looks like a mistake. Say what it does, why it is a candidate, and where it is written down. When the enhanced engine takes one up, mark it done here and leave the normal engine as it is.

**Kinds:** *bug* (the code does not do what it plainly means to), *oversight* (a case nobody thought about), *display* (what the screen says is wrong or misleading), *dead* (a rule, figure or control that does nothing), *unfinished* (a feature left half made).

**Risk** says what a change would disturb. *None* means only what the player sees. Anything that moves a figure can also part the game from the savegame checks in `CLAUDE.md`, which is why these belong in a separate mode.

**How this list was made (2026-11-09).** A survey of `docs/mechanics` by the phrases its specs use for such things ("kept as read", "looks wrong", "does nothing" and so on), with each hit read in context. Of 102 keyword-searched files, about 75 had flagged passages read in context, and 26, mostly tooltip, map and format specs, were searched only, so an oddity described in other words may be missing. Nearly all of this comes from reading the original's code, not from watching it; only the entries that say so have been confirmed. Section numbers are from the spec named.

## Asked for

- 🟡 **Everything BiceLib adds to the original.** BiceLib, the DLL the BlackICE mod injects into the game, adds tooltips, hooks and patches; all of them should be considered for the enhanced engine. They are not listed here: since 2026-11-20 they are written up as behaviour, with no address in it, in the mod's own `docs/bicelib/features.md`, which is what this project reads of BiceLib and all it reads (*The shape*, below). Its offsets are no part of this project. **The normal engine gives none of them** - decided on 2026-11-16: making compatibility patches for a DLL made for another binary is not prudent; the normal engine is finished first, and BlackICE tests the original's own modding API (it runs without the DLL, but for the max infrastructure fix, `docs/compatibility.md`, entry 29). The enhanced engine takes BiceLib's patches and mechanics in outright, with **an API for mod-defined native interface elements** in place of BiceLib's overlay. A first attempt at giving them to the normal engine as a Lua shim, `OpenHOI3.BiceLib`, was taken back off main and discarded on 2026-11-16; the enhanced engine starts afresh, natively.
- ✅ **Shipped inside its mod.** Decided on 2026-11-18: a mod written for the enhanced engine ships the version of it the mod was made for, in the mod's own folder under `tfh/mod/`, laid over `tfh/` and the install as the normal engine lays a mod, and names it in its descriptor; the launcher runs that version. Its saves record the engine they were made with, and whatever players check between them checks it too, the file checksum not covering it. `launcher.md`.
- ❌ **The brigade builder's technology list can overflow.** The list of what a brigade's technologies are runs out of its box when there are many. A scrollable window is suggested. `builders.md`, 7. *Display.* Risk: none.
- ❌ **A building's line in an order of battle could name its building by key.** Asked for on 2026-11-19: a
  `building_construction` (and a `building_deployment`) names its building by its place in `common/buildings.txt` counted
  from one - BlackICE's Peenemünde file says `building = 24` for `rocket_test` - so a mod that adds, removes or reorders a
  building silently turns every such file onto another one. Accepting the key, `building = rocket_test`, beside the
  number would make the files readable and robust; a number would still be read as today. `units.md`, "A file of units as
  a game runs"; `savegame.md`, 5. *Oversight.* Risk: none - a file written with a key is not valid in the original, which
  only matters to a mod made for both.
- ❌ **A headquarters' supply tooltip could show its subordinates' use.** The unit panel's supply and fuel tooltips give only the unit's own use. For a headquarters it should show its own consumption and, separately, that of all the units under it. `unitpanel.md`, 10. *Unfinished.* Risk: none.
- ❌ **A mission that strikes a building of the player's choice.** Asked for on 2026-11-27, in place of widening the
  installation strike (*Air*, below): a ninth bombing mission whose order carries a building by its key in
  `common/buildings.txt`, bombing that building's working level and the guns. Proposed with it: a building is on the
  mission's list where its entry says `openhoi_air_target = yes`, none unless a mod says so; the mission is given for
  land an enemy holds with a level of it built and flies at the highest working level over the guns; a row more in
  the air orders window with a short list to choose from; an order of ours in the enhanced save. The AI would not fly
  it until its air pass is taught to. `bombing.md`, 1. *Unfinished.* Risk: moderate - whatever a mod opens is bombed.
- ❌ **A file of the rules a leader is chosen by.** Asked for on 2026-11-27, in place of mending the two faults of the
  choosing one by one (*Leaders*, below): the table of what each trait effect is worth to which unit, which is the
  executable's, read from a file of the mod's. Proposed with it: `common/openhoi_leader_choice.txt`, an entry a trait
  effect with its `worth` and a `unit` block of a small set of tests of ours - the slowest brigade's speed, the
  combined arms worth, an adjuster's attack, one figure against another, the first ship's type, a flag or a type
  every brigade or any has; an effect not named keeps the original's line, and with no file the table is the
  original's whole. `leaders.md`, 5. *Unfinished.* Risk: small - who the choosing gives which unit.
- ❌ **A save could hold the AI's generator.** The original keeps two generators, the game's and the AI's, and a save holds where the game's stands (`seed`, `count`) and nothing of the AI's, which a load seeds afresh. Here a load restores the game's (`compatibility.md`, entry 15) and seeds the AI's from the save's `seed` at its first draw, so a save loaded twice plays the same, but the AI's rolls after a load are not those the saved game would have made. Writing its draws beside `count` would mend that; the decision of 2026-11-12 is to keep the save's keys the original's. `savegame.md`, 4; `landcombat.md`, 8. *Oversight.* Risk: none to a check; one key more in every save.
- 🟡 **A replay of a whole game, for the player to enjoy** - **built 2026-11-25**, recording, watching and sharing (`replay.md`, sections 10 to 17), another way than this entry foresaw: the playback makes each load the player made, so nothing has to be added to the save; the list below is still what a load loses, and each entry mended makes a crossing's step smaller. *Still open*: moving to a date without playing everything before it, clearing recordings no save leads to, and a recording that outlives its engine. Wished for on 2026-11-12: a game recorded from its start to its end and played back to watch, through every save and load the player made on the way. The replay built today (`replay.md`) is **for development only**: it records from one starting point - a new game, or one save loaded - and plays back from that same point, so it never has to cross a load. A whole game's recording does, and that is the headache: **after a load the game must go on exactly as it would have without one**, or the recording's next command finds a different game. Today it does not: a save in the original's keys does not hold everything later play depends on, and entering a loaded game works some of it out again. The next section lists what. So this needs a save of the enhanced engine's own, holding all of it, and a load that does not enter the game afresh - or a recording that carries what a load loses. Every addition is to be checked against it (`CLAUDE.md`, *Always*; `OpenHOI3.SaveCheck reload`), and what it finds is listed there. *Unfinished.* Risk: none to the normal engine; the enhanced save's keys differ from the original's.

## The shape, decided 2026-11-20

Worked out while the normal engine still lacked multiplayer, which came first. **The skeleton is built since
2026-11-21, and one fix behind it** (*What is built*, below); the rest is not. How it reaches BlackICE and its players
is `blackice.md`.

- **A mode, not a branch.** The enhanced engine is switches in this one codebase, so the faithful rules stay under the
  savegame checks. Should the faithful engine one day be retired, it is that one that becomes the branch.
- **A game carries its rules.** They are an object on the game state, asked at the place a rule differs - never a static,
  as nothing under `lib/` is - and part of what a game is: in the enhanced save, in the state's hash and in whatever
  players check between them. A switch is added when a candidate of this file is taken up, with a test for each arm.
- **A mod's data turns the mode on**, by a file of ours in its layer; with none a game is the faithful one, so Their
  Finest Hour and every mod written for the original are untouched. **Every fix is on unless the mod names it** as one
  it does not want: a mod moving to a newer engine gets that engine's fixes, which is acceptable because it names the
  engine it ships (`launcher.md`).
- **BiceLib's features come in by their kind, not as its functions.** A fix of the original is how the enhanced mode
  behaves. A rule that is a mod's choice becomes a define or a key in the file that already holds its subject. A text
  becomes variables the localisation may ask for. The overlay and what mods build on - windows, map modes, a day's
  call into Lua, a mod's own commands - become an interface at the simulation's edge, a command's body an effect
  block, so that every change stays a command. Not a `BiceLib` table in Lua, and not compiled code shipped by a mod.
- **Where their description comes from.** `docs/bicelib/features.md` in the mod's repository, written on BiceLib's
  side as behaviour - formulas, order, caps, edge cases, what a mod gives each - and the fact base's findings for what
  the original does at each place. Never BiceLib's source or headers.
- **BlackICE stays playable in the original for a version or two.** So one tree serves both engines, a third switch in
  the mod's Lua beside the two it has, and what a rule needs declared twice for that time - once for the DLL, once
  here - is declared twice.
- **What a file meant for both engines may hold**, by the reading of 2026-11-20
  (`reversing/findings/FINDINGS-unknownkeys.md`): the original passes over a key it does not know written as
  `key = value`, in every kind of file read for it, and does **not** step over one written as a block - the block's
  contents are read as the enclosing object's and its `}` closes that object early. So a new key in a file the original
  reads is a scalar, always. `common/defines.lua` takes new entries and new sections, being fetched by name through
  Lua, and a new file under `common/` is never parsed. No new key goes in a country's or a province's history, where
  an unknown key is taken for a name and acted on. A new key may not be the name of anything a mod declares, the
  original trying a key as a building, a technology, a unit type, a terrain and more before it gives up on it. And a
  condition the original drops is a condition absent - an `and` of it alone holds - so script that leans on a new
  condition needs a gate the original answers false.

### What is built

Begun 2026-11-21; as of 2026-11-27.

- ✅ **The file is `common/openhoi_enhanced.txt`** (`CRulesReader`), in any layer; its being there makes every game made
  from those folders an enhanced one. The name carries the prefix everything of this engine's in a mod carries, or
  stands in a subfolder of its own (`blackice.md`). It may be empty. What it may say:

      without = { minister_tech_decay }

  the fixes the mod does not want, by name. Any other key, and a name that is no fix, is reported.
- ✅ **The rules are `CRules`, on `CGameState.Rules`**: whether the game is enhanced and which fixes it goes without. A
  rule that differs asks `state.Rules.Has(EFix.X)` where it differs. A fix is one value of `EFix` and one name in
  `CFixes` - lower case with underscores, and kept once given out, a mod's file and every enhanced save holding it.
- ✅ **One line says them** (`CRules.Text`): empty for the faithful game, `enhanced`, or `enhanced without a b`. That
  line is hashed into the play checksum (`mechanics/checksum.md`, section 8) and is the last part of the state's hash,
  `rules` (`replay.md`).
- ✅ **A fix is switched in a running game by an order** (`CSetFixCommand`, 2026-11-27, asked for that day: a page
  "in which we can enable/disable each patch individually"). The game's rules change, and with them the save's
  line and the hash's last part. **It is the host's alone** between machines - "Host only." - an `IHostOrder`,
  which `COrderCheck` refuses from anybody else (`multiplayer.md`, section 4.1), and it runs on every machine and
  goes into a recording as every order does. Refused in a faithful game, for a name that is no fix, and for a fix
  that acts only as the files are read - `unit_figure_decimals` and `tech_repair_cost`
  (`CFixes.IsReadWithTheFiles`), which are in the unit types and technologies a game is made from. A rule asked
  from then on answers by the new state, also while the game is paused; a figure made once a day or once a month
  follows at its next making, and **`OpenHOI3.Fixes` in the mod's scripts stays what the folders said when the
  scripts were loaded** - no script of BlackICE's asks it yet. A page reads the rules with `OpenHOI3.Rules()` and
  gives the order with `OpenHOI3.SetFix` (`mechanics/modwindows.md`, section 6.6); BlackICE's is Engine Fixes,
  under its Debug tab. It is for trying a fix against the game without it: `common/openhoi_enhanced.txt` stays
  what a new game starts with.
- ✅ **An enhanced save holds two keys of ours** at its top, after `gameplaysettings`: `openhoi_rules`, that line, and
  `openhoi_engine`, the version of the engine that wrote it (`launcher.md`, section 8). A faithful game writes
  neither, so its save stays the original's key for key. **A loaded save's rules stand**, whatever the folders it is
  loaded under say; a fix it names that this engine does not know is reported and left out. A save with no such key
  takes the folders'.
- ✅ **An enhanced game's saves are in `save games/openhoi`** under the player's folder, written there and listed from
  there (`CSaveFiles.FolderFor`): kept apart from the original's, nobody loads one in the original. Decided 2026-11-21.
- ✅ **A mod's Lua is told**: `OpenHOI3.Enhanced` is true or false, and `OpenHOI3.Fixes` holds `true` under the name of
  each fix that is on (`CLuaRules`) - so a script made for both engines asks `OpenHOI3 ~= nil and OpenHOI3.Enhanced`.
  They are the folders' rules, set as the scripts load.
- ✅ **The first fix, as proof: `minister_tech_decay`** (*Economy and production*, below).
- ✅ **The second, to be seen: `simplified_terrain_sea`** (2026-11-21, the user's choice for trying the mode: "much more
  visible than the minister one"). The Simplified Terrain map mode sets the air mode's map style in place of its own, so
  the water is drawn from the provinces' colours, and gives each sea province its terrain's colour as it gives the land
  (`mechanics/mapmodes.md`, section 6). Display only.
- ✅ **BlackICE's working copy holds the file since 2026-11-21**. Since 2026-11-27 it goes without three fixes that
  move the balance, until the mod's figures have been looked at: `air_defence_counts`, `southern_seasons` and
  `amphibious_defence`.
- ✅ **The statistics record, 2026-11-24** (`mechanics/statisticsrecord.md`): every country's 72 figures kept each
  day in the game and its save, a packed block a country's year - about half a MiB a game year of BlackICE - with
  the collection a rule of the lobby, `OpenHOI3.Statistics` for a page and the record written out as files when
  asked. Display only.
- ✅ **Native pages, 2026-11-24** (`mechanics/nativepages.md`): a mod's Lua fills a window of the game's own interface -
  `OpenHOI3.Gui`, a page that names a `window`, a `host` screen and an `opener` - the user's choice over moving a
  mod's logic into the engine. BlackICE's two slider AI pages are the first, behind buttons on the production and
  technology screens.
- ✅ **Custom map modes, 2026-11-24** (`mechanics/custommapmodes.md`): a right click on the Infrastructure, Resources
  or Supply map mode button lists what else the mode can shade the land by - a building's level, a resource's
  amount, one of eleven supply figures - in ten shades of one of two palettes, with a legend. Display only. With it
  the Resources mode's tooltip says a province's manpower and leadership.
- ✅ **Built-in interface elements, 2026-11-24** (`mechanics/builtin.md`): `built_in = { style = .. size = .. color = .. }`
  on an icon or a button of `interface/openhoi/` in place of a sprite, painted by the engine in a style of a small
  library a mod's `interface/openhoi/builtin_styles.txt` may change. What the engine's own new screens are laid out
  with, and a mod's.
- ✅ **Game options of the engine's own, 2026-11-21** (`mechanics/gameoptions.md`, section 5): two more rotating
  autosaves - one some days before each month's end, one by time played (`mechanics/savegame.md`, section 11.1) -
  scrolling at the screen's edge, and where an event's window comes up; kept in `openhoi_settings.txt`.
- ✅ **A country's own conversions, 2026-11-22** (`mechanics/ic.md`, section 4.1): energy into crude oil and crude oil
  into fuel set by the player - how much a day, how much of the source to keep, the level to stop at - by a command,
  in the save and the hash. Changes play, for the country that sets it.
- ✅ **A mod's windows, 2026-11-22** (`mechanics/modwindows.md`): pages a mod's Lua draws with Dear ImGui, reading the
  game through `OpenHOI3` and ordering through commands - BlackICE's utility, its Trade AI and statistics pages
  among them. Display, but for the orders a page gives.
- ✅ **The brigade card for a whole unit, 2026-11-22** (`mechanics/brigadecard.md`, section 7): the ribbon over a
  unit's brigades opens it with the unit's summed figures - the DLL's unit inspector. Display only.
- ✅ **The record of finished battles, 2026-11-22, packed 2026-11-25** (`mechanics/combatreports.md`): a report a
  battle - both sides, their losses and men, who won - kept for the whole game and in the save, a packed block a
  year with every figure exact: 853 KiB for the 72,621 battles of a BlackICE game to 1943, where a line a battle
  came to 4.75 MiB. Read by a page; nothing in play reads it.
- ✅ **The host's rule on other files, 2026-11-25** (`multiplayer.md`, section 15): a machine whose play checksum is
  not the host's is let in where its engine, rules and the original's checksum are, and is told which files
  differ. The lobby's choice, between machines only.
- ✅ **The replay, 2026-11-25** (`replay.md`, sections 10 to 17): a game recorded beside its saves by the lobby's
  rule, each save naming its place; played back from its start through every save and load the player made, the
  playback making each load itself, so the save needed nothing added; watched from the lobby through any country,
  changed on a bar while it runs; a save written from a playback taking the game over at that hour; a host's game
  between machines recorded too; and a replay exported as one file with its save and imported. Nothing of it
  changes play - but a recorded new game is saved and loaded once as it begins.
- ✅ **Five fixes of the interface, 2026-11-25**, chosen by the user from the list below - "1, 2, 6 and 7" of seven
  put to him - each a name a mod may go without:
  - ✅ `fresh_screens`: the province view and the technology panel written again each hour of the game, a diplomacy
    row's threat, name and flag too, the list of trades made again when its routes are others, and a fleet's list
    of what it carries when that changes. Display only.
  - ✅ `menu_resumes`: closing the in-game menu starts the clock again where the menu stopped it, whichever of its
    windows was opened meanwhile.
  - ✅ `tooltips_on_screen`: a tooltip is kept off the left and the top of the screen too. Display only.
  - ✅ `shown_figures`: ~~a circumstance's attrition times a hundred as its other three figures are, on the brigade card
    and the designer's terrain summary~~ (**taken back on 2026-11-27**: attrition is a percentage as it stands, so
    the hundredfold showed it a hundred times too large - the entry under *Interface*, below); a fleet's positioning
    penalty in the reorganise window with its
    hundredfold; a production, reinforcement or upgrade share with nothing needed written as covering 100%; and a
    fleet's hourly regain of organisation at sea without the roads. Display only.
  - ✅ `unit_ordinals`: a new unit's number takes the ending of its last two digits, 111th and not 111st. Changes the
    names of units made, and so what a save holds.
  **Kept as the original's, by the same word**: a port or an airfield under a counter cannot be clicked, and the
  zoom, the wheel and the timed texts go by the frame. **And `air_orders_for_all`**, by the user's choice of the same day between the
  missions every unit can fly and those any can - "only the missions every selected unit can do": with several air
  units selected the orders window greys a mission any of them may not fly, as it has always done for several
  fleets, where the original offers the first unit's and hands them to all. The click itself still brings the
  window up, as for one unit.
- ✅ **Three fixes of how data is read and used, 2026-11-25**, the first batch of three put to the user - "Yes, start
  with batch 1" - each a name a mod may go without:
  - ✅ `key_without_equals`: in a province's history and a unit file, a key with no `=` takes the word after it -
    `manpower 0.66` is manpower 0.66. Read when the files are, so by the folders' rules and not a loaded save's.
  - ✅ `unit_figure_decimals`: a unit figure of more than three decimals is cut to three, in a unit's file and a
    technology's block for a unit type - `0.3375` is 0.337. By the folders' rules too.
  - ✅ `yield_modifiers`: a province's yield of fuel is scaled by its holder's `global_fuel` and of money by its
    `global_money`. Changes the simulation where a province yields either.
  **And for every game**, a minister's second, different type for a post is reported; the first still stands.
  Each fix's arm is tested; BlackICE's files hold none of the faulty lines today, so the mod plays the same with
  them.
- ✅ **Six fixes that give a dead value its meaning, 2026-11-26**, the second batch, each put to the user first and each
  a name a mod may go without. What a value was meant to do is nowhere in the original, so each rule is ours:
  - ✅ `tech_repair_cost`: a technology's `repair_cost_multiplier` for a unit type is added with its other figures.
    Set as the technologies are read, so by the folders' rules and not a loaded save's.
  - ✅ `trait_dissent_impact`: a leader's `dissent_impact` scales what dissent costs his unit at sea, in the air and
    under bombs, as it does on land.
  - ✅ `raid_trait_hours`: a raid on a convoy, by sea or from the air, counts the hours its raider's leader banked in
    it, so `seawolf` can be earned by raiding.
  - ✅ `trait_fort_defence`: a defender behind a whole level of fort - the coastal fort where an attacker comes from
    the sea - has its leaders' `fort_defence` on its attack and defence, shown as the fort's modifier.
  - ✅ `radar_efficiency`: a radar station's reach is longer by its country's `radar_efficiency` - "radar reach sounds
    good".
  - ✅ `trait_surprise_chance`: the side that picks its tactic second rolls its highest ranked leader's
    `surprise_chance` and under it takes the tactic that counters the other's outright, where it may pick that
    tactic at all - the user's rule, "a small % chance that the leader picks the counter-tactic". One roll more,
    and only for a side with a chance.
  **Kept dead, by the same word**: `call_ally` in a `war` effect - "a leftover from EU3" - and the casus belli the
  two effects make, which neither the base game nor BlackICE uses. Each fix's arm is tested but the raid's, the
  fort's and the dissent's, which were only run; a game of 45 days on BlackICE went through.
- ✅ **How many units a command holds, 2026-11-26** (`mechanics/attaching.md`, section 8), the first of BiceLib's
  rules taken in - "Start with the command limits". A mod's choice written as data, and no fix: a base for a
  corps, an army and an army group in `defines.lua` (`OPENHOI_CORPS_UNIT_LIMIT` and its like, five with none),
  three kinds of modifier that raise or lower it for a country - `corps_unit_limit`, `army_unit_limit`,
  `army_group_unit_limit`, from a technology's levels or any source of a modifier - and a define that lets a
  division of one brigade take no place. BlackICE's steps are at set levels of a technology, which a figure a level
  cannot say, so they are twelve triggered modifiers, **in `common/triggered_modifiers.txt` with the mod's others
  and in no file of the engine's own** - a second file read after it was built first and taken out the same day,
  "please dont make it a new modifier file". The original reads them too: it logs the kind as unknown and drops
  it, so there each is a row of the Strategic Effects window with a name and no effect, BiceLib's Lua setting the
  same figures. Nothing is kept for it: the limit is worked out from
  the defines and the country's modifier each time it is asked. Changes play, for the player; **the AI's ladder of
  commands is left counting five**, as BiceLib leaves it (*Not decided*, in the entry below).
- ✅ **Six fixes and two defines after BiceLib's, 2026-11-26** - "Ok do 1 to 8", the rest of what the mod has
  switched on that changes play. The fixes, each a name a mod may go without:
  - ✅ `historical_model_choice`: a model that asks for a level above the one held loses to every model that does
    not (`mechanics/units.md`, section 5). Display only.
  - ✅ `queued_historical_model`: a queued brigade's `historical_model` gives nought for a technology it does not
    name, so no level lands on the technology before it (`mechanics/buildqueue.md`, section 5).
  - ✅ `place_unresearched_building`: a finished building may be placed without its technology, the placing's other
    tests standing and an order still asking for it (`mechanics/buildinglines.md`, section 6).
  - ✅ `oob_leader_ranks` and `history_leader_ranks`: a land leader a file of units puts on a land unit of his own
    country takes that unit's rank and is put in service; one who comes into service on his history's day takes
    the rank its line names (`mechanics/leaders.md`, section 5).
  - ✅ `saved_event_candidates`: the save keeps each country's candidate events and a load puts them back, where
    BiceLib makes them again after a load - so a loaded game plays on as the saved one would have
    (`mechanics/events.md`, section 1; `mechanics/savegame.md`, section 21).
  The defines, a mod's choice and no fix, the original's rule where one is not given:
  `OPENHOI_WAR_EXHAUSTION_TO_NEUTRALITY = 0` - no hand-over to neutrality at peace, the figure back to nought all
  the same (`mechanics/warexhaustion.md`, section 2) - and `OPENHOI_AI_LENDS_UNITS = 0` - the AI lends no unit, and
  still gives a loaned one back (`mechanics/agents.md`, section 10.5). Each arm is tested, the AI's lending through
  its expeditions pass with a unit on a friend's land and one on loan; `OpenHOI3.SaveCheck enhancedstart` looks at a
  new game under them, and forty days of BlackICE went through. Not reviewed in play.
- ✅ **The display lines after BiceLib's, 2026-11-26** - "Go ahead and build it", each put to the user first. What
  BiceLib writes in hard-coded English is written here **in the mod's words**: a line is on where the
  localisation has its key (`localisation/openhoi/`), so these are no fixes and need no switch -
  - ✅ a leader's skill with his highest, in the leader window (`mechanics/leaders.md`, section 12);
  - ✅ `kill_leader`'s unit, place and bracket, for a text that asks (`mechanics/scripttext.md`);
  - ✅ what a `load_oob` file would do, counted off the engine's own reading of it (`mechanics/scripttext.md`);
  - ✅ the manpower tooltip's need by branch, rotation, attrition and trickleback (`mechanics/topbar.md`, section
    14);
  - ✅ a land unit's summed figures and its damage in a battle's tooltip, in columns measured in the tooltip's
    font (`mechanics/combatview.md`, section 12);
  - ✅ the division designer's paradrop line and a combined arms group's bonus (`mechanics/builders.md`, section
    11).
  Two are behaviour and have names a mod may go without: `ctrl_unassign_selected`, Ctrl on "remove all leaders"
  taking the selected units' leaders alone, and `tooltip_scroll`, a tooltip taller than the screen moved through
  with Alt and the wheel or the arrow keys (`mechanics/tooltips.md`, section 8). **Two records are kept for
  them, display only, in an enhanced save and not in the hash** - by the user's word on both: a country's
  manpower record, thirty days of it, and the damage of the units in the land battles being fought
  (`mechanics/savegame.md`, section 21). Other than BiceLib's: any tooltip scrolls and says what is left below;
  a battle's damage is what was really taken off a unit, strength as organisation; the rotation's and the
  attrition's cost is kept a thousand times finer than a thousandth of manpower, which a brigade's day of it
  does not come to. Each has its tests; `OpenHOI3.SaveCheck scripttext` and `enhancedstart` look at BlackICE
  under them, and forty-five days of it went through. Not reviewed in play.

- ✅ **`air_regroup_agrees`, 2026-11-26**: the AI joins a small air group to another only where no wing of the two
  together breaks the mix its own split goes by (*AI*, below; `mechanics/agents.md`, section 5.3). Found profiling a
  BlackICE save of July 1939 the user sent for its speed, where the engine's profiler had the transfers of wings at
  the top of the orders: 180 a day, eleven countries' light bombers and flying boats going round. A fix and no rule of
  the mod's: on in every enhanced game, a saved one too - a save names the fixes it goes without, and no save
  written before a fix names it. Tested both ways; on that save 5 transfers a day.

- ✅ **Fixes from the worklist of 2026-11-27**, the points the user chose from this file's list (`worklist-enhanced.md` of that day), each a
  name a mod may go without and each arm tested:
  - ✅ `debt_repaid`: each payment on a debt comes off it, the last being what is left, and the day's figure is at
    least a thousandth, so a debt is gone after `YEARS_TO_REPAY_DEBT` years of peace with money in hand
    (`mechanics/aid.md`, section 1).
  - ✅ `debt_revoke_cost`: revoking a country's leave to run a debt charges the creditor who revokes, and `DEBTREVOKE`
    names the creditor to the debtor it is told to (`mechanics/aid.md`, section 1).
  - ✅ `lend_lease_partial`: a lend-lease receiver whose share of the day is more than the giver's stockpile pays for
    is given what it does pay for, and the giver charged that much (`mechanics/aid.md`, section 3).
  - ✅ `tactic_aggressiveness`: each side of a land battle picks its tactic with its own leader's aggressiveness, and
    with none where it has no leader (`mechanics/landcombat.md`, section 11).
  - ✅ `friendly_fire_target`: a ship that takes one of its own side for the enemy shoots at the ship its roll points
    to, so friendly fire is spread over the side (`mechanics/navalcombat.md`, section 4).
  - ✅ `combined_arms_condition`: the condition `has_combined_arms` asks what was written after it, so `= no` holds
    where no unit of the side has combined arms. Neither the base game nor BlackICE writes `= no` today
    (`mechanics/leaders.md`, oddity 17).
  - ✅ `redeploy_delay_once`: a strategic redeployment holds its unit's attack delay at the one with no roll in it on
    the way, drawing nothing, so that one cut short still leaves a delay, and draws it once, as the unit arrives - the
    same whole delay on arrival for one draw, where the original draws every hour of the way
    (`mechanics/landorders.md`, section 8). Fewer draws, so every later roll of the game falls otherwise.
  - ✅ `shown_figures`: added to it - the ledger tooltip's tribute sent to a master is written red and below nought, as
    the loss it is (`mechanics/topbar.md`, section 14). Display only.
  - ✅ `shown_figures`: added to it - a leader at the last skill of the table shows no share towards the next, in his
    tooltip and on the browser's dragged row, where the original reads the word after the table
    (`mechanics/unitpanel.md`, section 10.5). Display only.
  - ✅ `shown_figures`: added to it - the division designer's terrain summary counts the ground's own attrition in
    its line, as its attack and defence are; **and taken out of it** - the hundredfold on attrition it had since
    2026-11-25, attrition being a percentage as it stands (`mechanics/builders.md`, section 11). Display only.
  - ✅ `fresh_screens`: added to it - the air map's green and red are made again each midnight and when one of the
    player's intercept or air superiority units changes its base, and a count of battles stops at 127, where 128 hid
    the icon (`mechanics/airmap.md`, oddities 1 and 2). Display only.
  - ✅ `fresh_screens`: added to it - the diplomacy screen's decision list is made again each hour of the game where
    the decisions offered are others or one may now be taken that might not, where the original's rows only ever
    grey until the screen is opened again (`mechanics/events.md`, section 12). Display only.
  - ✅ `interdiction_target`: interdiction flies at the province of its list where the most of the enemy is on the
    move, and with no enemy in any of them at one of them by a draw of the game's generator - the user's rule, "fly
    to a random province" (`mechanics/bombing.md`, section 2).
  - ✅ `air_raid_waits`: a unit raiding convoys from the air waits at its base until its hours to the next try are no
    more than the flight out takes, so every sortie arrives to a try (`mechanics/navalmissions.md`, section 8.1).
  - ✅ `minister_intel`: the two modifiers the original reads nowhere add to how well a country knows another -
    `offmap_political_intel` towards every country, `peace_offmap_intel` towards one it is not at war with - each
    worth that much of a spy's share, for the AI's intelligence and the intelligence screen alike. The rule is ours
    (`mechanics/intelligence.md`, section 1).
  - ✅ `weighted_songs`: the next song is drawn among those that may play, each as likely as its chance, so a factor of
    3 is three times as likely and not always first (`mechanics/music.md`, section 2.2). Nothing of the game hangs
    on it.
  - ✅ `occupation_policy_checked`: the command that sets an occupation policy asks the policy's `allow` as the button
    does, and at a change of a country's wars the policy it holds towards a country it is not at war with goes back
    to none (`mechanics/occupation.md`, *What looks wrong*). Left as it was: a policy applying to land held from an
    owner with no war between the two.
  - ✅ `amphibious_defence`: a ship type's `extra_amphibious_defence` counts, the fleet's table being its ships' own
    added up, and a landing with no invasion defence has the define's penalty exactly (`mechanics/transport.md`,
    section 9). Their Finest Hour's `assault_ship` and BlackICE's `landing_craft` begin to help the brigades they
    name.
  - ✅ `southern_seasons`: a province's latitude is its distance from the map's middle either way, so the south takes
    the season table's own southern column, the cold of its high latitudes and days that lengthen and shorten
    (`mechanics/weather.md`, section 1). Large for the southern theatres; BlackICE starts with it under `without`.
  - ✅ `air_defence_counts`: a wing meets as many of an hour's shots as its `air_defence`, by what the hour makes of
    it, is whole, each avoided at `OPENHOI_AIR_CHANCE_TO_AVOID_HIT` - the land's `BASE_CHANCE_TO_AVOID_HIT` where a
    mod gives none - and no draw is added (`mechanics/aircombat.md`, section 8). Changes every air battle; BlackICE
    starts with it under `without`.
  - ✅ `map_text_by_time`: a message's text on the map lives by the second, as long on every machine as the original's
    at sixty frames a second - 3.3 seconds at the slowest speed, 0.67 at the fastest (`mechanics/messages.md`,
    section 6). Display only.

**Built for every game, and so no part of the mode**: the notice `AUTOSAVING` and the two picture keys, F11 and F12,
which are the original's (`mechanics/loadingscreen.md`, section 6; `mechanics/screenshots.md`).

## BiceLib's features, each one's place

By `docs/bicelib/features.md` of the mod's repository, which has each feature's rule in full. **Proposed on
2026-11-20 and not objected to; the three marked *decided* were asked and answered that day.** Each line says what of it is built; the
names of keys and defines not built yet are proposals.

**Fixes - how the enhanced mode behaves, on unless the mod names one**

- ✅ A minister's technology decay counts (*Economy and production*, above). **Built 2026-11-21**, `minister_tech_decay`.
- ✅ A historical model that needs a technology the country has not researched loses to every model that does not.
  **Built 2026-11-26**, `historical_model_choice`.
- ✅ A queued brigade's `historical_model` gets a level for every technology of its type, so none lands on another.
  **Built 2026-11-26**, `queued_historical_model`.
- ✅ A country-level `ic` modifier counts, as off-map IC: a country's IC is its provinces' sum and that figure.
  **Built 2026-11-22**, `offmap_ic` (`mechanics/ic.md`, section 3).
- ✅ A leader an order of battle puts on a land unit gets the rank of that unit's level and is put in service; a leader
  whose history dates his first rank after the start comes into service at the rank the line names (*Leaders*, above).
  **Built 2026-11-26**, `oob_leader_ranks` and `history_leader_ranks`.
- ✅ A finished building may be placed without its technology, every other test of the placing standing.
  **Built 2026-11-26**, `place_unresearched_building`.
- ✅ A loaded game has its event candidates (*Script, events and weather*, above) - the enhanced save keeps them.
  **Built 2026-11-26**, `saved_event_candidates`.
- ✅ The Simplified Terrain map mode colours the sea. **Built 2026-11-21**, `simplified_terrain_sea`.
- ✅ Already the normal engine's: a province's most infrastructure (`compatibility.md`, entry 29) and the requirement
  tooltip's indentation (entry 10).

**Rules - a mod's choice, written as data**

- 🟡 **How many units a command holds**: three modifier kinds - `corps_unit_limit`, `army_unit_limit`,
  `army_group_unit_limit` - added to a base in `defines.lua`, so a technology, a triggered modifier, a minister or a law
  may raise it and no script has to set figures day by day. A leader's trait raises or lowers it by a trait effect,
  `command_limit`. A division of one brigade takes no place. The country asked is the commander's.
  **Built 2026-11-26** (`mechanics/attaching.md`, section 8), with two things other than proposed here: the
  defines carry the prefix, `OPENHOI_CORPS_UNIT_LIMIT`, `OPENHOI_ARMY_UNIT_LIMIT`, `OPENHOI_ARMY_GROUP_UNIT_LIMIT`,
  in the section `military`; and the one-brigade division's allowance is a define of its own,
  `OPENHOI_LONE_BRIGADE_TAKES_NO_PLACE`, so that a mod that gives no define plays the original's rule. BlackICE's
  twelve steps are triggered modifiers in `common/triggered_modifiers.txt`. **Not built**: the trait effect `command_limit` - BiceLib's `addCommandLimitTrait`,
  which the mod leaves off. **Not decided**: whether the AI's own ladder of commands (`mechanics/agents.md`,
  section 4) follows the limit. It counts five to a command on both engines today, BiceLib not changing it, so an
  AI's corps holds five where the player's holds four; following the limit would change how many headquarters
  the AI makes.
- ❌ **Screens and capital ships**: two defines, the flat penalty and the screens wanted for each capital ship.
- ❌ **A trait that counts only at some ranks**: `min_rank` and `max_rank` on the trait; no pair of traits to exchange,
  and nothing left out of a save. Off in the mod today, so it may come last.
- ✅ **War exhaustion** - *decided*: no payback into neutrality at peace, and nothing else. War exhaustion gathers and
  weighs as the original's; a define turns the hand-over off. BiceLib's patch does more - it sets the figure to nought
  every month - and that is not wanted here. **Built 2026-11-26**: `OPENHOI_WAR_EXHAUSTION_TO_NEUTRALITY` in the
  section `diplomacy`, nought turning the hand-over off; the figure still goes back to nought at peace.
- ✅ **The AI's lending of units** - *decided*: a define stops the AI lending a unit to another country; a unit on loan
  is still given back, which BiceLib's patch stops too. **Built 2026-11-26**: `OPENHOI_AI_LENDS_UNITS` in the
  section `military`, nought stopping the lending.
- ✅ **A country's own conversion of energy into crude oil and of crude oil into fuel** - *decided*: a native setting,
  not country variables. **Built 2026-11-22** (`mechanics/ic.md`, section 4.1), in one thing otherwise than proposed.
  - **What is built**: for each country and each of the two conversions, whether it is the player's own, how much a
    day, how much of the source to keep and the level of the product to stop at. It is changed by a command of its
    own, kept in the enhanced save and in the hash, and set from a page of the mod's
    (`mechanics/modwindows.md`, section 6.1). A country nobody plays converts as the original. The caps the original
    has - its capacity by IC, the energy industry needs that day, the rate by technology - stand.
  - **What is otherwise**: the proposal had the setting set by a script effect too. That was left out by the user's
    word of 2026-11-22 - not wanted for now, the page being the way to set it. So BlackICE's events that set the
    conversion on the original do nothing here; they stay in the mod for the release that plays on both engines and
    are marked there for removal.

**Texts - built in, their wording from the localisation**

- ✅ Variables filled where a text asks for them: the unit, place and manner of a `kill_leader`; the manpower tooltip's
  reinforcement need by land, air and navy with its four further figures. **Built 2026-11-26.**
- ✅ Lines of the engine's own under new keys: a leader's skill with his highest, what a `load_oob` file would do, a
  unit's summed figures and damage in a battle's tooltip, whether a design may be dropped by parachute and which
  brigades stop it, what a combined-arms group gives, and manpower and leadership in the resources map mode's tooltip.
  **Built 2026-11-26**, the keys `OPENHOI_LEADER_SKILL`, `OPENHOI_LOAD_OOB_...`, `OPENHOI_BATTLE_STATS_...`,
  `OPENHOI_BATTLE_DAMAGE_...` and `OPENHOI_DESIGNER_...`; the map mode's two lines earlier, with the custom map modes.
- ✅ A tooltip taller than the screen scrolls - any tooltip, not the requirement's alone (*Interface*, above).
  **Built 2026-11-26**, `tooltip_scroll`.

**Interface**

- ✅ Ctrl on the "unassign all leaders" confirmation unassigns the selected units' leaders alone, as commands that each
  name their unit. **Built 2026-11-26**, `ctrl_unassign_selected`.
- ✅ Game options of the engine's own: two more rotating autosaves, scrolling at the screen's edge, where an event's
  window comes up. **Built 2026-11-21**, as four rows on the game options' Game tab kept in `openhoi_settings.txt`
  (`mechanics/gameoptions.md`, section 5; `mechanics/savegame.md`, section 11.1).
- ❌ A map mode a mod declares in data: the figure a province is coloured by, its bands and colours.
- 🟡 Windows of the engine's own for what the engine holds: a country's flags, variables and timed modifiers, the order of
  battle as a tree and counted, a selected unit's figures, where a building stands, the record of battles. **Built otherwise, 2026-11-22 to 24**: a unit's figures on the brigade card and a building's place in the custom map modes are the engine's own; the rest are pages of the mod's that read the engine (`mechanics/modwindows.md`), and no window of the engine's.
- ✅ Windows a mod defines, through the interface for mod-defined elements: laid out in `.gui` files and fed by the mod's
  Lua - BlackICE's country, game-information, set-up, help and statistics pages. **Built 2026-11-22 as far as a
  page that reads** (`mechanics/modwindows.md`), and otherwise than this line proposed: drawn with Dear ImGui by the
  page's own Lua and not laid out in `.gui` files, by the user's decision of that day. The menu, a page a file, and
  the first page, Active Flags.
- ✅ **After the port, some of those pages are to move closer into the engine** - the user's, 2026-11-22, once the
  utility stands here as it stands on the original: "There are some pages which we could incorporate more closely
  with the new engine, after we have the current utility state ported." Two were named, as examples and not as the
  whole list:
  - ✅ **The custom sliders and the trade AI as part of the game's own interface** - "the custome slider and trade ai
    could be incorporated as a native game GUI": what the Prod. Sliders AI, LS Sliders AI and Trade AI pages set
    belongs beside the production, technology and trade screens it steers, and not in a utility window. **Built
    2026-11-24** as native pages (`mechanics/nativepages.md`): windows of the game's own interface the mod's Lua
    fills, behind buttons on the production and technology screens; their settings stay the country's variables
    the mod's AI Lua reads.
  - ✅ **Statistics from a source of the engine's own** - "the stat collection should then also be made use a built-in
    data source, not the CSV-via-LUA approach": the mod's collection writes a text file a statistic a country a
    day from the ministers' Lua, its folders made by the shell, and **it does not work on this engine** (the
    user's, the same day; set aside by his word). The engine already keeps series for its own Statistics screen
    and strategic warfare charts; the collection is to be a record the engine keeps and a page reads, and no
    file written by a script. **Built 2026-11-24** (`mechanics/statisticsrecord.md`): 72 figures a country a
    day, for the whole game, in the save, the collection a rule of the lobby.
- 🟡 **The Inspector's last three, each given its place by the user on 2026-11-22**, the first two not as pages of the
  utility at all:
  - ✅ **The unit inspector in the game's own interface** - "this one we should do in the native GUI directly, so skip
    the Utility implementation. To make it readable we will probably need some new engine-GUI elements." What it
    shows on the original: for whatever is selected, some forty figures of the unit as it stands - the sums over
    its regiments with their technology in them - and its modifier on every terrain. **Built 2026-11-22** as the
    user set it out that day: the ribbon over a unit's brigades opens the brigade card's window for the whole
    unit (`mechanics/brigadecard.md`, section 7). No new kind of element was needed for it.
  - 🟡 **The custom map mode in the engine** - "done directly in the engine, skipping the utility step. For this I
    think we could just make the Map-Mode buttons right click-able to open a small list (like the unit upgrade
    selection)." A right click on a map mode's button by the minimap opens a short list to choose from, as a
    brigade's upgrade list opens; what a mod declares for it is the line above this list, *A map mode a mod
    declares in data*. **Built 2026-11-24** for three buttons - Infrastructure, Resources and Supply - with what
    the engine offers for each (`mechanics/custommapmodes.md`); *not built*: a mode a mod declares.
  - ✅ **Combat reports stay in the utility**, as a page - "This one should stay in the utility, but as you said needs
    the engine to keep records": the engine keeps a record of every finished battle and a page reads it.
    **Built 2026-11-22** (`mechanics/combatreports.md`): a report a battle, with both sides named, their losses and
    men and who won; kept for the whole game, in the game's state and in an enhanced save, so that a save loaded has
    its own record - in the place of the DLL's files, campaigns and sessions. Not in the hash: nothing reads it but
    a page. **Packed since 2026-11-25**, every figure exact (*Save bloat*, below).

**What mods build on**

- ❌ A command a mod defines: a name, its arguments, and an effect block for its body, so that it does the same on every
  machine. BiceLib has the frame for one and no command of its own in use.
- ❌ A call into the mod's Lua once a game day on every machine, which may post commands and change nothing itself. With
  the modifier kinds above BlackICE's one use of it goes.
- 🟡 Reads for Lua under the global `OpenHOI3`: a message popup with the caller's text, a country's flags, variables,
  timed modifiers and modifier totals, a province's figures, a leader's state, what the player has selected. **Built 2026-11-22 to 26, but for the message popup and what the player has selected** (`mechanics/modwindows.md`, section 6).
- ✅ The record of finished battles kept in the enhanced save, not in files and a country's variables.

**Not taken**: BiceLib's reverse-engineering tools, console, crash report and crash save, memory watch, warm-up and
debug pages, the mover of unit animation files, and the loss of skill on promotion, which its maker abandoned.

## A whole game's replay: what a save does not give back

What the enhanced engine's own save would have to hold, and what its load would have to leave alone, for a game to go on after a load as it would have without one. Found on 2026-11-12 by `OpenHOI3.SaveCheck reload` - a new game run to 5 January and to 1 March 1936, saved, loaded, and every field of the two games set beside each other - and by reading what each field is. **Each is the original's too**: the save keeps the original's keys, and where the original works a thing out again on a load this does the same (`savegame.md`, 5; `war.md`, 4). Caches that are made again on demand to the same figures are left out. The two games part in the first hour after the load.

**Sorted again on 2026-11-21** (`savegame.md`, section 20), a game saved on 1 March 1936: 218 paths as loaded and 380 as entered, every one under an entry here but for the three added that day - a theatre's priority, the order of the triggered modifiers and a carrier's air group under another fleet - and what the check left out as its own noise.

**Checked path by path on 2026-11-14** (`savegame.md`, section 20): every path the check told at 5 January and 1 March 1936, as loaded and as entered, is under an entry here, or was the loader's own gap and is mended, or is noise in the check itself, which **the check now leaves out**, saying so in `CReloadCheck.Skipped`: the numbers a unit's file or save names its leader by and a unit line's `is_reserve`, read only as a game is made or loaded; whether a game was loaded; caches made again on demand to the same figures (the distances by sea, the route finder's arrays, the land within two edges, the air bases each country holds); the count of changes of hands since a game began and the stamps of the caches that watch it, which count events and not state; and a unit's and a brigade's figures of a battle's hour, made again each battle hour before they are read and left on a unit after its battle - all but a unit's defence product, which the AI's odds read after the battle too (below). An array grown only as far as its last entry above nought, a province's building levels, counts its missing tail as nought. The table of every path, its class and its entry is the session's (`savegame.md`, section 20, says what it found).

**The AI**
- ❌ **Its generator**: the entry above.
- ❌ **Every agent and what it holds** - its committed, held-back, garrison and reserve divisions, its positions, the subordinates promised, its plan's ports and priority - and each country's unit pass: made afresh on entering, which runs two passes and gives their orders again, so divisions move in the first hour where the saved game's would not (`savegame.md`, 2 and 5).
- ❌ **The AI strategy**: the save's lists are kept only until entering makes every country's again (`savegame.md`, 16).
- ❌ **A theatre's priority**: its share of its country's attention, made in the hour's upkeep of the theatres, is nought after a load until that upkeep next runs (`theatres.md`, section 9; that the original's save holds none is *inferred* from ours writing the original's keys).
- ❌ **An inactive plan's `min`**: the original writes a plan's `min` only while it is active, so a played country's theatre - whose powers and wants are made again each midnight and when its area, forces or stance are set - loads with its powers and no `min` until the next midnight. Nothing here reads an inactive plan's `min` (`agents.md`, 7.3; `reversing/findings/FINDINGS-liveread.md`, section 76).
- ❌ **The five ministers' countdowns**, the foreign minister's skipped first work and the production minister's desperation: started again.
- ❌ **The Lua's memory**: Their Finest Hour's scripts keep none between calls, a mod's may (`replay.md`, 7).

**The game's own state that the save leaves out**
- ✅ **The event candidates**: a load leaves every country with none until its month turns, as the original's does (`events.md`, section 1; below, *Script, events and weather*), so no event with a mean time to happen fires where the saved game's would have. **Kept in an enhanced save since 2026-11-26** (the fix `saved_event_candidates`; `savegame.md`, section 21); a faithful game's are still lost.
- ❌ **A relation's fraction**: `value` is written as a whole number.
- ❌ **A lend-lease convoy's own efficiency**: the original neither writes nor reads it, and a convoy read from a save is
  made whole again, so a route a raid has cut delivers in full from the load where the saved game's would have
  regained a hundredth a day (`aid.md`, section 3; `reversing/findings/FINDINGS-definesuse.md`, section 10).
- ❌ **A war's history**: a loaded war has no start, so the Lua's `GetCurrentRunningTimeInMonths` answers 0 (`reversing/findings/FINDINGS-war.md`, C.13).
- ❌ **`last_war`** is stamped the hour of the load for every pair at war, so a peace in the next hundred days leaves a truce the saved game's would not (`war.md`, 4).
- ❌ **The pacts' numbers and the order of their lists**, made again in the save's order - the order the hour's work goes through them in.
- ❌ **The order of each country's units**: the save's tree walked children first, units made in play after (`savegame.md`, 5), and the order of a unit's subordinates.
- ❌ **The order of each country's list of the provinces it controls**: the order it took them in (`supply.md`, section 2), which its areas, its theatres and its strategy's `defend_prov` follow - written nowhere, so a loaded game's lists are in the order of the provinces' numbers. *Inferred* the original's too, its save writing each province's controller and nothing of the list.
- ❌ **A doctrine level at every load**: the original's entry runs every country's shares with nothing to share out, a loaded game's too, so a brigade behind its country in a doctrine is a level nearer after each load (`upkeep.md`, section 5). Not built here for a load.
- ❌ **The order of a country's triggered modifiers**: a running game lists them in the order they came on, and a load tests them again in the file's order - so the list of sources a tooltip writes out is in another order, and nothing else (`modifiers.md`).
- ❌ **A carrier's air group under another fleet than its ship's**: a merge leaves a group aboard with nobody above it, and a ship moved out of its fleet leaves its group under the old one (*Sea*, below); a save writes a group inside its ship and nowhere else, so after a load it is under its ship's fleet - with that fleet's place in the order of battle, and its agent's lists of fleets and air units (`savegame.md`, section 5; where the original's load puts it was not read).
- ❌ **The names**: where each unit type's list of names had got to, and the names of units lost, which stay in use in a running game - so a new unit after a load can be named otherwise (`builders.md`).
- 🟡 ~~**A diplomat on his way** after an offer, and **the offers waiting for an answer**: lost.~~ Kept since 2026-11-14 (`savegame.md`, section 19), as the original keeps them - but for **an added war goal waiting for its answer**, which the original writes as `add_war_goal` and its country loader does not name, so it is lost on a load in the original and here. *Oversight.* Risk: none; a mod's save is unaffected.
- ❌ **The alignment drift's next step**: taken at once after a load (`alignment.md`).
- ❌ **The spies' cursor** in each priority band (`espionage.md`, 4).
- ❌ **The weather's zones**: their cooldowns and headings (`weather.md`); and **each province's front marker**, so the first hour's new fronts are not held back by a front over their home, and **the picture each province shows**, chosen at the first hour - which the AI's choice of leaders reads for a winter trait (`reversing/findings/FINDINGS-weathersim.md`, A.6).
- ❌ **A trade route's inactive state** and the hour it was last found so, which its sticky days count from.
- 🟡 ~~**What each unit has been given to know of it** by spies (`espionage.md`, 7)~~ - kept since 2026-11-14, the save's `country_intel` read and written as the original's (`savegame.md`, section 5); and **a patrol's place on its round and its hours to its next try at a convoy**, and a raider's place on its round - a raider's hours are its `attack`, kept - and **an air unit's hours to its next try at raiding convoys**, which the original's air order neither writes nor reads, so a load starts them at nought and it tries the convoys the first hour it is in its province (`navalmissions.md`, 8.1).
- ❌ **The units queued for removal at the hour's end** - a unit whose last brigade `remove_brigade` named: the queue is not saved, in the original too, so a unit queued by a command run between two hours - a player's answer to an event - is still in the game, brigade and all, after a load (`effects.md`, section 9).
- ❌ **A country's level of each strategic resource**, which only ever rises in play: a load works it out again from the provinces as they stand, so a resource lost before the save is gone after it (`modifiers.md`, 6).
- ❌ **Each port's blockade and each country's share of home ports cut off**: neither is saved, the share is worked out again on entering, and every port comes back unblockaded until its controller is next set - in the original too (`modifiers.md`, 3, step 8).
- ❌ **Each country's radar provinces and each province's last radar level applied**: neither is saved, and entering puts every owned province with radar on its controller's list - so a radar left with an old holder by a plain change of hands moves to the holder of the hour, and one taken off by a fall below a whole level comes back on (`modifiers.md`, 3, step 13; `intelligence.md`, 4). The original's too.
- ❌ **What a province's units ask of its supplies**, nought until the day's pass, which a unit's draw and the AI's line fill read within the day.
- ❌ **What a province held when the supply pass last came to it**: not saved, so a loaded game starts with nothing there, as
  the original's does, and the first midnight after a load carries nothing along a network (`savegame.md`, *Ours*;
  `reversing/findings/FINDINGS-liveread.md`, section 116). A game saved and loaded loses a night of carrying.
- ❌ **Each province's days out of supply** come back one more than saved: entering gives every province its depot again, none being saved, and counts the day once more, so `on_encirclement` and `out_of_supply_days` see a day the saved game's had not (`supply.md`, section 18).
- ❌ **A province's suppression**, made in its own day: nought until the next midnight, which the revolt risk read, an underground's finding read and the first supply pass's loss on ground held from an enemy read in between (`revolt.md`, section 2; `supply.md`, section 8). *Inferred* the original's too: nothing its load does makes it (`reversing/findings/FINDINGS-savegame.md`, A.5).
- ❌ **A country's original capital**, the first capital it declared in play - by losing its declared one or by the `capital` effect - where its government goes back to once it holds it again: the original keeps it in the country's own record of its changes, which no save of its writes, so a load falls back to the declared capital (`war.md`, section 14). The original's too: no reference save holds a country's `history`.
- ❌ **A join_air's target**: a load restores the join's own partner and not the order's, so the AI's air triage finds a loaded join dead and calls it off (`agents.md`; confirmed in the running game).
- ❌ **A unit's last defence product**: what its last battle hour made of its defence, kept when the battle is over and read by the AI's odds of an attack as the toughness it counts, is not in the save, and after a load the unit counts none until a battle works it out again - as the original's, whose constructor zeroes the product (`moving.md`, section 11.4; `reversing/findings/FINDINGS-attackodds.md`, section 7). So an attack the saved game would have made on stale toughness may want another division or fall to the cancel bar after a load.
- ❌ **The IC of a country owning no province**: its new-game set-up worked it out under its history's ministers and nothing works it out again, but the save keeps no IC, so a load works it out without its ministers - Estonia's 8 from 1941 is 7 after a load, and with it the supplies and money it makes and the resources it uses (`ic.md`, section 3). *Ours* on the load's side, for want of anything to read; what the original's load gives such a country was not read.
- ❌ **A battle's surprise**: a battle at sea's submarines and a bombing of ships carry their surprise from round to round on the side, which the original's save does not write, so a surprise still within its rounds is gone after a load (`reversing/findings/FINDINGS-savegame.md`, A.5.1; `savegame.md`, section 20).
- ❌ **The world's last-battle losses**: the six figures the `last_*_losses` conditions and the event text's `$LAND_LOSER_LOSSES$` and its fellows read, set as each land, air and sea battle is decided, are written nowhere. A load starts them at nought; the original's load does not touch them, so they hold whatever the session's last battle left - a game's played before it in the same run included - and before any battle whatever its memory held (`triggers.md`, section 5.2; `reversing/findings/FINDINGS-triggerleaves.md`, section 3.1). An event asking `last_battle_loser_losses` before the next battle answers otherwise after a load.

**Figures made again at the hour of the load**, where a running game keeps those of the last midnight or month until the next: every province's modifier (monthly), the triggered modifiers tested again, each country's war record and `lost_IC` - its battles offensive and defensive, its fleets fighting and its air units bombing, which no save keeps (`triggers.md`, section 5.1) - each country's modifier, IC and most manpower (what it made at home is the save's since 2026-11-15, entering writing nothing to it, `resources.md`, section 3), the officer ratio - which the hour's return of organisation reads - the spare manpower the production minister reads, as the original's entering makes it (since 2026-11-16, `ministers.md`, section 7) - the reinforcement and upgrade shares' needs - which the Lua's spare IC reads - and the men reinforcement needs, counted again by entering's run of the shares with nothing to spend, and the men it used at nought (`upkeep.md`, section 8), and the day's ledgers, at nought until the next day: conversions, what convoys brought, tribute, what units asked - with it the supply slider's need on the screen (`upkeep.md`, section 8; what they drew out of a capital is the usage, which the save keeps, since 2026-11-15), a convoy's transports and escorts wanted and what the land it loads in yields, and each country's transports at the day's sharing, which the production script reads. **A battle's modifiers**: entering makes every battle's again for both sides, of every kind, as the original's does (`reversing/findings/FINDINGS-savegame.md`, A.5.1; `savegame.md`, section 20) - and a battle at sea or a bombing of ships saved before its first round **draws for its surprise** as it is entered and again in that round, so it has two chances where the saved game's had one, and the generator is a draw or more ahead.

**Only shown**, until the next day or month: each party's last change and tallies, the leadership each technology was given, and **a brigade's model**, which a save does not name - shown as the first after a load (whether the original shows it so was not read).
- ❌ **A returning unit's subordinates stay behind.** An expeditionary unit sent home by its date goes home alone, and everything under it is detached and stays with the host, still on loan, each a unit of its own (an annexation and an exile send those home too, one by one; the AI's own command carries the tree). This is wanted changed. `expeditions.md`, 3. *Oversight.* Risk: small; saves hold ownership.

## Script

- ❌ **`create_alliance` checks nothing.** A second one makes a second alliance on the list. `agreements.md`. *Oversight.* Risk: small.
- ❌ **Tribute landing in a harbour is counted nowhere.** The overlord's ledgers leave it out. `stockpile.md`, 7. *Display.* Risk: none.
- ❌ **A unit loaded by `load_oob` keeps experience the file does not give at "none",** and gets no leader. `units.md`. *Oversight.* Risk: small.
- ✅ **A key with no `=` takes the value before it.** **Mended by the fix `key_without_equals`, 2026-11-25, in province histories and unit files; every other file is read as the original reads it.** `manpower 0.66` in a block of pairs is read as `manpower` with the value the pair before it read, and 0.66 is lost: BlackICE's Kiskunhalas gets manpower 5 from the `infra = 5` above it, and Their Finest Hour's Soviet HQ brigades of 1938 and 1939 are named `hq_brigade`. Reading the word after the key as its value would give the line what it plainly says. `clausewitz.md`, 2. *Bug.* Risk: small - eight BlackICE provinces' manpower and four brigades' names change.

## The nuclear bomb

- ❌ **A bomb barely touches land units or air units on the ground.** The drop goes through a settlement that divides brigades' and wings' strength damage by a hundred, so a brigade loses under 1% where a ship is usually sunk. `nukes.md`. *Oversight.* Risk: war balance.
- ❌ **A province keeps its industry until something makes its modifier again.** Every building is flattened and the modifier is not rebuilt. `nukes.md`. *Oversight.* Risk: small.
- ❌ **A greyed row can be the ticked one,** and the window's two dates do nothing on this order. `nukes.md`. *Display.* Risk: none.
- ❌ **One bomb sends the AI's whole strategic force on nuke missions.** The AI asks the stock group by group, but it moves only as an order starts, so every strategic group of a theatre is given a nuke mission and all but the first fly nothing and end at their next hour - a pass of bombing lost. `nukes.md`, section 3; `moving.md`, section 17.4. *Oversight.* Risk: the AI's air war.

## Alerts

- ❌ **A pact's second party cannot break it when the first has nothing on the border.** The balance is nought, and turned round for the second party it is -1 rather than very large, so in the three windows it reads as too few troops. `agreements.md`, 6. *Bug.* Risk: small; AI wars.
- ❌ **The partisans alert is never shown.** Alert 3 is complete, list and all, and nothing puts it up. `alerts.md`. *Dead.* Risk: none.
- ❌ **"More efficient laws" means only earlier in the file.** Any law before the current one whose `allow` holds is offered, better or not. `alerts.md`. *Oversight.* Risk: none.
- ❌ **A pact is listed before it can be broken.** The alert comes up three months before the pact may be broken at all and says so as "may be broken on". `alerts.md`. *Display.* Risk: none.
- ❌ **Icons have no blink or banner.** The banner sits off the screen in the file and its 30-second timer is written and read by nothing. `alerts.md`. *Dead.* Risk: none.

## Land combat

- ❌ **Toughness counted twice.** A technology's toughness is added twice, and a unit's is double the sum of its brigades'. `units.md`, 2; `landcombat.md`, 2; `mapunits.md`, 4. *Bug.* Risk: large; mods are tuned to it.
- ✅ **Technology cannot change repair cost.** **Mended by the fix `tech_repair_cost`, 2026-11-26.** `repair_cost_multiplier` is never added when technologies are summed. `units.md`, 2. *Dead.* Risk: only files that set it.
- ✅ **Two traits do nothing in a land battle.** **Given a meaning by the fixes `trait_fort_defence` and `trait_surprise_chance`, 2026-11-26.** `fort_defence` is reached only for an attacker, and `surprise_chance` is read and thrown away. `landcombat.md`; `leaders.md`, oddity 12. *Dead.* Risk: moderate.
- ❌ ~~**Support attack counts its hours twice,** in its own hour as well as the battle's.~~ **Withdrawn 2026-11-27: not so.** The unit's own hourly count asks for progress along its way, which a support attack never has, so the order's hour is its only count - found when this was to be mended, and nothing is to mend. `landorders.md`, 10.
- ❌ **AI support attack dropped.** The way into a province with no battle goes with the order, so a support attack where nobody fights yet is lost. `landorders.md`, 10. *Oversight.* Risk: AI attacks.
- ✅ **Strategic redeployment restarts its attack delay** every hour on the way, drawing on the generator each time. **Mended by the fix `redeploy_delay_once`, 2026-11-27.** `landorders.md`, 10. *Oversight.* Risk: small; rolls move.
- ❌ **A redeployed brigade never loses organisation in its hour.** `STRAT_REDEP_ORG_LOSS` is taken off the hour's gain and what is left held at nothing, so a division on a strategic redeployment with no supplies keeps all it has where every other unit loses it. `organisation.md`, 3. *Bug.* Risk: small; unsupplied redeployments.
- ❌ **Redeployment offered where impossible.** The row asks nothing about distance or a way, so it is open for a province it cannot reach and the order drops the hour it is given. Confirmed. `landorders.md`, 10. *Display.* Risk: none.
- ✅ **Amphibious defence dead, landing penalty a hundredth off.** **Mended by the fix `amphibious_defence`, 2026-11-27.** `extra_amphibious_defence` is asked of a fleet's summed table, which never holds a ship's, and with no landing craft the penalty is -70.70%. `transport.md`, section 9 and "What looks wrong", 6. The tables are kept on each type since 2026-11-14 and `CLanding.FleetExtraDefence` is the one place to sum the ships' instead. *Dead.* Risk: moderate.
- ❌ **A route is not always the cheapest.** A province is settled the first time a step reaches it, and a cheaper way found later does not replace that. `movement.md`, 3. *Oversight.* Risk: marches and AI paths.
- ❌ **The click and the route ask "at war" two ways.** The click's test of a too-weak or unsupplied unit counts a war nobody declared where it covers the province, the route's does not, and the route asks the supply of a unit aboard a fleet where the click does not - so an order the click lets through can find no way, and the unit keeps its old one without a word. `movement.md`, 1 and 3. *Oversight.* Risk: small.
- ❌ **A sea in no region is the Black Sea where the map lacks one.** Closed waters are asked by each sea's first region against the regions named `black_sea_region`, `baltic_sea_region` and `mediterranean_region`; a sea in no region and a name the map does not have both come out as nothing, so with a mod's `region.txt` missing one of the three, every sea in no region is taken for that sea. And a fleet in a battle may be sent only beside it, by the land unit's test of falling back, which asks for land strength and knows nothing of the enemy's ships. `fleetsandair.md`, 1; `movement.md`, 1. *Oversight.* Risk: none in Their Finest Hour, whose map has all three.
- ❌ **"Arrived" where it was halted.** A unit halted on arrival because it may no longer stand in the province ahead - its holder changed under it - is told it has safely arrived, in the province it never left; and a land unit boarding a fleet whose way ran on and back to the same sea is told twice, the first time naming the shore. `movement.md`, 6. *Display.* Risk: none.
- ❌ **Rebels have no organisation.** Their country has no officers, so a brigade's ceiling is nought after its first hour; experience starts at -1.0. Confirmed. `rebels.md`, 13. *Oversight.* Risk: rebels far stronger.
- ❌ **Shore bombardment asks only the side's first unit's owner, and its cap is a floor taken as it stands.** A side of
  several countries is bombarded by the first one's enemies alone, and a mod whose `SHORE_BOMBARDMENT_CAP` is above
  nothing puts that much on every unit of every land battle, with no fleet anywhere. `landcombat.md`, 4. *Oversight.*
  Risk: none in Their Finest Hour, whose cap is negative.
- ✅ **The attacker picks its tactic with the defender's aggressiveness,** and the defender always with none. **Mended by the fix `tactic_aggressiveness`, 2026-11-27.** One value is written from each side's leader in turn and only the attacker's pick is given it. `landcombat.md`, 11. *Bug.* Risk: small; which tactics come up.

## Sea

- ✅ **An area's harbours keep their order after a bombing.** The original sorts an area's ports by the naval base's working level only when the area's provinces change or a province gets its first naval base level, so a port bombed since - or repaired - keeps its old place, and which harbour a convoy starts from or a tie goes to follows a stale order. The normal engine sorts by the working level whenever the areas are made (ours, `supply.md`, 9). `bombing.md`, 7. *Oversight.* Risk: small.
- ✅ **A technology's `radar_efficiency` does nothing.** **Made to lengthen a radar's reach by the fix `radar_efficiency`, 2026-11-26.** It is loaded, summed for the country and shown in the technology's tooltip, and nothing in the simulation reads it: the radar takes the station's level and `radio_strength`, not this (`reversing/findings/FINDINGS-fleetbasehour.md`, section 4, a static search with a positive control). Their Finest Hour gives it once, BlackICE five times. Making it scale a radar's reach would make the effect mean what it says. `intelligence.md`, 4. *Dead.* Risk: small - what countries with it see.
- ❌ **An interceptor goes out once,** then lies in port under its order. On the "may be bugs" list. `navalmissions.md`, 5 and 12. *Bug.* Risk: moderate.
- ❌ **A sortie from away from base stalls,** given a path that does not begin where the fleet is. `navalmissions.md`, 6 and 12. *Bug.* Risk: small.
- ✅ **Friendly fire always ends on the last ship.** **Mended by the fix `friendly_fire_target`, 2026-11-27.** The walk that picks a friend does not stop at its roll; a raider's fleet of nine lost about a tenth of its strength in a month. `navalcombat.md`, 4; `navalmissions.md`, 12. *Bug.* Risk: moderate.
- ❌ **Reserve ignores priority 4,** and treats an aggressive fleet as a defensive one. `navalmissions.md`, 2 and 12. *Oversight.* Risk: small.
- ❌ **A patrol sinks convoys** every twelve hours, like a raider. **Kept: not a bug, by the user's word of 2026-11-27.** It was on the "may be bugs" list. `navalmissions.md`, 3 and 12. *Oversight.* Risk: moderate.
- ❌ **An escort hesitates an hour** at the first end of its beat, the first time. `navalmissions.md`, 7 and 12. *Oversight.* Risk: negligible.
- ❌ **A convoy's ship sunk outright in a raid costs the convoy nothing.** The count walks the convoy's throwaway fleet as the rounds leave it, and a ship that went under a tenth of a point of strength has left it - so the harder a raid hits, the less it can sink. Rare from a fleet, common enough from the air, where a hit's tenfold strength roll can finish a transport ship. `navalmissions.md`, 8 and 8.1. *Bug.* Risk: moderate; raids from the air sink more.
- ❌ **A raider fought at sea is worth nothing to the strategic warfare.** `STRAT_RAIDER_FOUGHT_IMPACT` is given only in a raid's own battle whose defender raids or patrols, and that defender is always the convoy's fleet under no order. Its sibling for a raider fought in the air, `STRAT_AIR_RAIDER_FOUGHT_IMPACT`, works. `navalmissions.md`, 8.3. *Dead.* Risk: small.
- ✅ **An air raider flies out twice for every try.** **Mended by the fix `air_raid_waits`, 2026-11-27.** It tries the convoys only every twelve hours but leaves its province the hour it arrives - for another of its list, or home where the list has no other - and flies out again at once, so with a base near its water every other sortie finds its hours not yet run and does nothing. `navalmissions.md`, 8.1. *Oversight.* Risk: small.
- ❌ **A patrol tells of the same fleet over and over.** `PATROLTRACK` is news against the fleet held as the hour began, and a patrol lets go of its fleet every hour and must sight it again - so one hour's failed roll makes the next hour's sighting news again, and a patrol beside a fleet it can barely see fills the log with it. And the log line "`$OUR_FLEET$ found $OTHERS$ fleet`" puts the fleet's own name where a country's adjective reads ("found 46th Navy fleet") - the text's, and a mod may mend that alone. `navalmissions.md`, 3.1. *Oversight.* Risk: none.
- ❌ **Ships have no defence.** `sea_defence` and `surface_defence` are read by nothing in the fight; only `hull` protects. `navalcombat.md`, 5. *Dead.* Risk: large.
- ❌ **`reduction_modifier` does nothing.** Read once, as a fleet leaves a battle, and thrown away. `navalmissions.md`, 10. *Dead.* Risk: only if brought to life.
- ❌ **The pride gains experience per reinforcement,** before the watering down. `upkeep.md`, 4. *Bug.* Risk: small.
- ❌ **Pride-sunk dissent define unused.** The dialog's 2 is a literal, and `PRIDE_SUNK_DISSENT_IMPACT` is read by nothing. `brigadecard.md`, 6. *Dead.* Risk: none.
- ❌ **A carrier's room counts wings twice** in the reorganise window. `reorganise.md`, 7. *Bug.* Risk: small.
- ❌ **Carried supplies lost in landing.** A unit's days of supplies go into the sea it leaves; a paratrooper's seven days come from the province it last walked out of. `transport.md`, "What looks wrong", 4 and 14. *Bug.* Risk: small.
- ❌ **The invasion mission mixes units and brigades,** and sends every passenger to the flanks while under them. `transport.md`, "What looks wrong", 8. *Bug.* Risk: landings.
- ❌ **One passenger ends the invasion,** by being refused or having nowhere left. `transport.md`, "What looks wrong", 9. *Oversight.* Risk: small.
- ❌ **Boarding is not checked.** The command puts a unit aboard wherever the two are; only the panel limits it. `transport.md`, "What looks wrong", 1 and 13. *Oversight.* Risk: none in normal play.
- ❌ **Loading several units tests the last alone,** and unloading asks nothing of where the fleets are. `transport.md`, "What looks wrong", 11. *Oversight.* Risk: none.

## Air

- ✅ **A unit once joined is never reinforced by a reserve again.** A unit another is ordered to join is marked so that a reserve's exchange passes over it, and the order's end sets the mark again where it was meant to clear it; a join-fleet order's end marks its fleet too. Nothing saves the mark, so a load clears it. Here the air reserve passes over a unit only while a join towards it stands. `fleetsandair.md`, 5.1. *Bug.* Risk: small.
- ❌ **An air reserve's top-up never runs, and its message has no type.** Besides swapping wings one for one, the reserve was meant to send wings off as a new unit to join a unit short of them; the two counts by type it needs are each made into a copy that is thrown away, so both are always nought and the step is never reached - the same slip leaves every air order's count by type nought in every save. And the day's exchange posts `REINFORCE_AIR`, which `interface/messagetypes.txt` does not declare, so the player is told through no message type. `fleetsandair.md`, 5.1. *Bug.* Risk: small.
- ✅ **A wing's defence does nothing.** **Mended by the fix `air_defence_counts`, 2026-11-27.** Neither its defence nor `air_defence` counts. Confirmed: "Yes thats a bug in the original." `aircombat.md`, 8 and 9. *Bug.* Risk: large.
- ❌ **Nothing defends a bombing target.** No air defence, fort or screen protects it. `bombing.md`, 12. *Bug.* Risk: large.
- ❌ **A worn air superiority unit shuttles,** an hour out and an hour home, and can be fought while out. `aircombat.md`, 3 and 9. *Bug.* Risk: small.
- ❌ **A beaten air unit flies out to sea,** to the water with the most ports around it, not to its base. Whether it is so is not known from play. `aircombat.md`, 8 and 9. *Bug.* Risk: small.
- ❌ **A worn unit at its base cannot be attacked.** `aircombat.md`, 7 and 9. *Oversight.* Risk: moderate.
- ❌ **The intercept scores without a war test.** `aircombat.md`, 5 and 9. *Oversight.* Risk: small.
- ❌ **A leaderless bomber almost never picks ships,** so naval and port strikes wear the port and hardly touch the fleet. `bombing.md`, 12. *Oversight.* Risk: moderate.
- ❌ **A fleet in port fires its air attack three times.** `bombing.md`, 12. *Bug.* Risk: moderate.
- ❌ **At sea, bombers are fired at by their own side.** Nobody holds a sea zone, and the friend test then passes every country that is nobody's puppet, so the bombers' own air attack and their friends' ships count towards the fire at them. `bombing.md`, 8. *Bug.* Risk: moderate; bombing at sea gets dearer.
- ❌ **Two units cannot bomb one province's ground.** The second finds nobody to be against and turns home. `bombing.md`, 12. *Oversight.* Risk: moderate.
- ❌ **A wing with no strategic attack still bombs,** one run an hour. `bombing.md`, 12. *Oversight.* Risk: small.
- ❌ **Installation strike bombs radar only.** `bombing.md`, 12. *Unfinished.* Risk: moderate if widened. **Not to be widened: a mission of its own for a building of the player's choice is asked for in its place, 2026-11-27** (*Asked for*, above).
- ✅ **Interdiction takes the last province of its list** **Mended by the fix `interdiction_target`, 2026-11-27.** its country sees into, whether anybody is there or not. `bombing.md`, 12. *Oversight.* Risk: small.
- ❌ **An air unit in a battle is sent by a right click with nothing asked** - not its range, not the clouds, not the click's test of the province - while one out of a battle may go only where the orders window allows. So a unit in an air battle can be sent beyond its range. `fleetsandair.md`, 4. *Oversight.* Risk: small; only the player's click.
- ❌ **The air orders' priority is never sent,** though the window shows, steps and remembers it. `fleetsandair.md`, "The air window". *Dead.* Risk: only if honoured.
- ❌ **Transport planes wrecked by a drop,** left at a hundredth of their organisation, on an enemy or not. `transport.md`, "What looks wrong", 15. *Oversight.* Risk: small.
- ❌ **Air supply's accounting.** It counts its load before capping by what lies there, picks the last short province and not the neediest, and delivers whether the target is still a friend's or not. `transport.md`, "What looks wrong", 18. *Oversight.* Risk: small.
- ❌ **A sunk carrier's air groups stay under its fleet** in the order of battle, based on an airfield, and **a fleet merged away leaves its carriers' groups aboard with nobody above them** - the carriers having gone to the other fleet, the groups not. `transport.md`, "What looks wrong", 22. *Oversight.* Risk: small; the panel and the browser show them where they are not.

## Leaders

- ❌ **Later history is ignored.** Nothing after a leader's first rank applies, and a dated `skill` does nothing. `leaders.md`, 1, oddities 2 and 8. *Dead.* Risk: historical data would suddenly apply.
- 🟡 **An order of battle's leader goes on anybody,** another country's or the wrong rank included. `leaders.md`, oddity 1. *Oversight.* Risk: small. **Mended in part 2026-11-26**, `oob_leader_ranks`: his own country's land leader takes the unit's rank; another country's still goes on.
- ❌ **One-brigade divisions are given no leader** by the choosing. `leaders.md`, oddity 3. *Oversight.* Risk: small.
- ❌ **`paradrop_mission` never counts** in the choice of a general. `leaders.md`, oddity 4. *Bug.* Risk: negligible. **To be mended by a mod's own file of the choosing's rules, asked for on 2026-11-27** (*Asked for*, above).
- ❌ **`defence_modifier` suits only units slower than 3,** so militia at exactly 3 miss out. `leaders.md`, oddity 5. *Oversight.* Risk: small. **By the same file, asked for on 2026-11-27** (*Asked for*, above).
- ❌ **Command reach falls off a cliff,** from half to nothing at exactly twice the radius. `leaders.md`, 8 and oddity 9. *Bug.* Risk: moderate.
- ❌ **An under-ranked leader loses his chain's bonus,** offence, defence and ground traits, while other traits still reach the unit. `leaders.md`, oddity 11. *Oversight.* Risk: moderate. **Narrowed 2026-11-27**: with the fix `oob_leader_ranks` a file's leader of the unit's own country has the unit's rank, the choosing asks the exact rank and the leader window offers only whom the unit may take, so the rank is short only for another country's leader a file puts on a unit. What stands is the other half of the same gate: a unit with no leader, or one of no skill, gets none of its chain's offence, defence and ground traits - every division of one brigade, the choosing giving those no leader. The candidate is that the chain's traits reach a unit whatever its own leader.
- ❌ **A tactic's `trait` condition ignores the chain.** `leaders.md`, oddity 13. *Oversight.* Risk: small.
- ✅ **`dissent_impact` works on land only.** **Mended by the fix `trait_dissent_impact`, 2026-11-26.** `leaders.md`, 10 and oddity 15. *Dead.* Risk: small.
- ✅ **`seawolf` cannot be earned by raiding** (*inferred*). **Mended by the fix `raid_trait_hours`, 2026-11-26.** `leaders.md`, 11 and oddity 16. *Bug.* Risk: small.
- ✅ **`has_combined_arms = no` holds where `= yes` does.** **Mended by the fix `combined_arms_condition`, 2026-11-27.** `leaders.md`, oddity 17. *Bug.* Risk: conditions change.
- ❌ **`is_winner` holds for both sides** of a battle broken off. `leaders.md`, oddity 18. *Oversight.* Risk: small.
- ❌ **The portrait bypasses the greyed button** (*inferred*), so a leader can be changed in a battle by clicking his picture. `leaders.md`, 12 and oddity 20. *Bug.* Risk: none.
- ❌ **Other countries' leaders can be changed** from their units' panels. `leaders.md`, 12 and oddity 21. *Oversight.* Risk: none.
- ❌ **Promoting or demoting takes the general off his unit,** and the window's buttons go on acting on him. `leaders.md`, 12 and oddity 23. *Oversight.* Risk: small.
- ❌ **The starting stockpile ignores leaders,** being reckoned before they are chosen. `leaders.md`, 5 and oddity 10. *Oversight.* Risk: start-save checks.
- ✅ **Skill 11's progress bar reads past its table.** **Mended by the fix `shown_figures`, 2026-11-27.** `unitpanel.md`, 10.5. *Bug.* Risk: none.
- ❌ **"Remove all" closes the window before it is answered.** `leaders.md`, oddity 25. *Oversight.* Risk: none.
- ✅ **A lent unit's leader button is enabled again** by the routine that disables it. The normal engine already greys it, by choice. `compatibility.md`, entry 23. *Bug.* Risk: none.

## Diplomacy and war

- ❌ **Land in no area is handed over unevenly.** An owned province of one level of infrastructure or none goes to the enemy around it every hour with no unit near: beside an area to the last enemy neighbour, a neutral beside it passed over; with no area beside it, a neutral holding the first neighbour keeps it where it is. `war.md`, 11 and oddity 29. *Oversight.* Risk: small; fronts in the desert, the Arctic and on islands.
- ❌ **Casus belli made in play do nothing.** **Kept, by the user's word of 2026-11-26: the two effects take a tag and no type, and no file of the base game or BlackICE uses them.** Each is of the null type, and the condition answers by the types' `is_valid`. `war.md`, 9 and oddity 6. *Dead.* Risk: war goals.
- ❌ **Casus belli fields unused:** `months`, `truce_months`, `peace_cost_factor`, `penalty_factor`, `prerequisites`, `is_valid_join`, `mutual`, `war_name` and others. `war.md`, 2. *Dead.* Risk: only if brought to life.
- ❌ **`call_ally` is read and does nothing.** **Kept, by the user's word of 2026-11-26 - "a leftover from EU3".** `war.md`, 9. *Dead.* Risk: small.
- ❌ **A widened war is second-class:** no threat, name or limited flag, while the relation hit and influence are still paid. `war.md`, oddity 1. *Oversight.* Risk: small.
- ❌ **Guarantors cannot be asked as allies.** `war.md`, oddity 2. *Oversight.* Risk: small.
- ❌ **A call with `value` unset is accepted for the player** without asking. `war.md`, oddity 4. *Bug.* Risk: none.
- ❌ **A truce counts from the last change of membership,** not a war's end. `war.md`, oddity 7. *Oversight.* Risk: small.
- ❌ **A faction leader joining as defender never leads.** `war.md`, oddity 9. *Oversight.* Risk: small.
- ❌ **A war can outlive its other side,** and `PEACEACCEPT` reaches a player who took no part. `war.md`, oddity 15; see `moving.md`. *Bug.* Risk: AI and war state.
- ❌ **`PEACESELF` and `PEACEDECLINE` mislead.** The first says "negotiating for themselves" and the whole war ends either way; the second's log line is about debt. `war.md`, oddities 16 and 17. *Display.* Risk: none.
- ❌ **Only the first attacker returns land at the peace.** `war.md`, oddity 13. *Oversight.* Risk: small.
- ❌ **A `handles_peace` goal runs everything anyway,** and the country fights on. `war.md`, oddity 23. *Bug.* Risk: moderate.
- ❌ **Annexed land goes to any occupier,** an ally of the annexed included; threat falls by a flat 9999; nothing marks the country dead. `war.md`, oddity 24. *Oversight.* Risk: moderate.
- ❌ **`surrender_inherit` hands control out oddly:** the annexer owns land its enemy holds, the holder is read off areas' first provinces mid-walk so control spreads from one area to the next, a fleet at sea goes to the first candidate wherever it is, and a unit going over leaves its subordinates behind and keeps a dead country's leader. `war.md`, 13 and oddity 30. *Oversight.* Risk: small; BlackICE's eleven uses.
- ❌ **A puppet follows its faction leader, not its overlord;** with no faction it joins no wars. `war.md`, oddity 25. *Oversight.* Risk: moderate.
- ❌ **Surrender progress counts only owned cores.** One owning none is never near surrender. `war.md`, oddity 19. *Oversight.* Risk: large.
- ❌ **Dissolving an alliance costs an unlisted -30** of relation. `agreements.md`, oddity 2. *Oversight.* Risk: small.
- ❌ **Guarantees cannot be revoked by button.** `agreements.md`, oddities 3 and 5. *Unfinished.* Risk: small.
- ❌ **Guarantees end after twenty years** whatever their history says. `agreements.md`, oddity 9. *Oversight.* Risk: small.
- ❌ **Withdrawn access ends both ways,** and `MILACC_CANCEL_RELATION_CHANGE` is read by nothing. `agreements.md`, oddity 4. *Bug.* Risk: small.
- ❌ **Two tests disagree for alliance and embargo:** what greys a button and what decides an offer. `agreements.md`, oddity 6. *Oversight.* Risk: none.
- ❌ **"Unlikely" means never.** A chance of 10 to 30 reads "Unlikely" and is always declined. `agreements.md`, oddity 7. *Display.* Risk: none.
- ❌ **A proposal made while a diplomat travels is lost without a word.** The post throws it away, but the tests the AI's scripts ask before posting - a call to arms', a guarantee's, an influence's, a trade's - do not look for a diplomat, so the AI makes it, posts it and never learns it went nowhere: in July 1941 Germany calls Italy into its war on Mongolia every few days and Italy comes only once an earlier offer's diplomat has arrived. `agreements.md`, 1. *Oversight.* Risk: moderate - when the AI's allies come into its wars.
- ❌ **The null faction's progress overflows.** `factions.md`, 7 and oddity 10. *Bug.* Risk: none.
- ❌ **The join dialog prints the alliance's costs.** The faction's action charges nothing. `factions.md`, oddity 1. *Display.* Risk: none.
- ✅ **Debt is repaid for ever.** **Mended by the fix `debt_repaid`, 2026-11-27.** It is never lowered, and one under 18.250 is never repaid. `aid.md`, 1 and 5. *Bug.* Risk: moderate.
- ✅ **Revoking debt charges the debtor,** and the message names the wrong party. **Mended by the fix `debt_revoke_cost`, 2026-11-27.** `aid.md`, 5. *Bug.* Risk: small.
- ✅ **The lend-lease cap is enforced by nothing;** over it the receiver gets nothing that day. **Mended by the fix `lend_lease_partial`, 2026-11-27.** `aid.md`, 5. *Oversight.* Risk: small.
- ❌ **Sharing theories collapses wholesale.** A new sharer of one theory ends every theory the old one gave. `aid.md`, 5. *Oversight.* Risk: small.
- ❌ **A Retrieve's date disagrees with its delay.** `expeditions.md`, 1. *Display.* Risk: none.
- ❌ **An empty expedition offer.** Clearing a fleet's box leaves Accept up, posting nothing useful. `expeditions.md`, 1. *Bug.* Risk: none.
- ❌ **A new game's history threat is resisted only by the countries set up before the one spreading it** (or mobilised by the history's wars): a later country's ministers' `threat_resistance` is not in its modifier yet. `diplomacy.md`, 5.3. *Ordering artefact.* Risk: small - every later start's threat and effective neutrality.
- ❌ **A later bookmark's history wars are stamped at the defines' start date**, `last_war` and mobilisation alike, as the lobby replays them with its clock there; so no country at war at a later start has its mobilisation bonus. `war.md`, 4. *Oversight.* Risk: small.

## Politics and espionage

- ❌ **No province ever defects.** The original's choice was compiled to an empty loop. `rebels.md`, 12. *Dead.* Risk: rebel outcomes.
- ❌ **Rebels make no demands.** `can_enforce_trigger` and `demands_enforced_effect` are never asked. `rebels.md`, 13. *Unfinished.* Risk: new system.
- ❌ **Risings all but never fire,** a one in 365 on top of the risk's own draw. `rebels.md`, 3. *Oversight.* Risk: large.
- ❌ **Revolt risk never decays** once the war ends. `revolt.md`, "What looks wrong". *Oversight.* Risk: moderate.
- ❌ **Suppression counts a province's own units twice** and reaches two edges, across the sea too. `revolt.md`, "What looks wrong". *Bug.* Risk: moderate.
- ❌ **Winning partisans hand land straight back.** `rebels.md`, "What looks wrong". *Oversight.* Risk: small.
- ❌ **Underground figures unused:** `UNDERGROUND_PARTISAN_STRENGTH` and `local_underground`. `undergrounds.md`, 1. *Dead.* Risk: none.
- ❌ **Organized Revolt uses a different threshold** from the day and the buttons. `undergrounds.md`, "What looks wrong". *Oversight.* Risk: small.
- ✅ **Occupation policy is barely checked,** **Mended by the fix `occupation_policy_checked`, 2026-11-27.** and outlives the war and the peace. `occupation.md`, "What looks wrong". *Oversight.* Risk: small.
- ❌ **A new game's laws read `enemy_ic_ratio` as false while any enemy is set up later** - its IC not worked out yet - so a country at war at a later start stops short of `service_by_requirement` and `total_economic_mobilisation` by where it stands in the countries' order. `politics.md`, 13. *Ordering artefact.* Risk: moderate - every later start's laws, and its units with them.
- ❌ **Spy bands short-change countries.** The cursor never wraps within a day, and a presence in a country that does not exist thins everyone else's share. `espionage.md`, "What looks wrong". *Bug.* Risk: moderate.
- ❌ **Missions at home run every mission,** and threat raised at home is everybody's threat from the country itself. `espionage.md`, "What looks wrong". *Oversight.* Risk: moderate.
- ❌ **`espionage_bonus` helps the wrong way.** The target's bonus lowers the chance of catching spies, its own at home included. `espionage.md`, "What looks wrong". *Bug.* Risk: moderate.
- ❌ **Foreign counter-espionage catches the target's own spies at home.** `espionage.md`, "What looks wrong". *Bug.* Risk: small.
- ❌ **`modify_spies` adds a count it never read,** and `has_hostile_spy_mission` compares with a figure nothing sets. `espionage.md`, 13. *Bug.* Risk: only scripts using them.
- ✅ **Minister intelligence effects do nothing:** **Mended by the fix `minister_intel`, 2026-11-27.** `peace_offmap_intel` and `offmap_political_intel`. `intelligence.md`, 1. *Dead.* Risk: small.
- ❌ **Radar stays with the old holder after a plain change of hands,** a radar bombed below a whole level stops seeing while one built to the same level sees, and the far radar ring is never reached. `intelligence.md`, 4 and "What looks wrong"; `modifiers.md`, 3, step 13. *Bug.* Risk: small.
- ❌ **A port's blockade is worked out only when its controller is set.** A fleet sailing in or out tells it nothing, so a port that never changes hands is never blockaded, and one taken while the enemy lay off it stays blockaded until it changes hands again; `blockaded`, `is_blockaded` and `blockade` all read that. `modifiers.md`, 3, step 8. *Oversight.* Risk: moderate; mods that fill `blockaded` would start to bite.
- ❌ **A country's rebuild asks whether a province is blockaded and does nothing with the answer.** In its walk of the provinces it controls, at war, it tests each owned province of its capital's area for a blockade and fetches the capital twice, and nothing follows. `modifiers.md`, 4, step 7. *Dead.* Risk: none.
- ❌ **A country's own modifier that nothing fills** is added to it in every rebuild, between neutrality's and its event modifiers. `modifiers.md`, 4. *Dead.* Risk: none.
- ❌ **The law dialog's Ok checks nothing.** `politics.md`, 11. *Oversight.* Risk: small; whether the command refuses was not confirmed.
- ✅ **A minister's second type for the same post is dropped without a word**: **Reported for every game since 2026-11-25 - 74 lines of the base game; the first type stands.** the first his block gives is the one he is, and 71 of the base game's lines that give a post again give it another type. `politics.md`, 7. *Oversight.* Risk: small - the base game's own ministers would change; report the line instead.

## Economy and production

- ❌ **A finished production line uses up a name.** As each unit is delivered the line is named for its next one, and the last delivery does it too without asking whether anything is left: a ship or wing line that ends takes the next name of its type's list and moves the list on past it, a division's a numbered brigade name. Nothing gives the name back. `buildqueue.md`, 6. *Bug.* Risk: small - unit names only.
- ❌ **The strategic warfare records count attrition by two different tests.** The player's own brigade's loss counts only where the province it stands in is held by another than its owner; a foreign brigade's loss in the player's province counts whoever holds it, so an enemy's army wearing down in ground it has taken from him still counts on its record. `strategicwarfare.md`, 3; `upkeep.md`, 3a. *Oversight.* Risk: none to the simulation; the records' figures.

- ❌ **The null technology charges budget for nothing.** `notech` costs 125 and takes 1250 of a new game's head start. `research.md`, 9. *Bug.* Risk: start-save checks.
- ✅ **A unit figure of more than three decimals is read ten times or more too large.** **Mended by the fix `unit_figure_decimals`, 2026-11-25.** The unit loader reads every digit after the point as one number of thousandths and pads only where there are fewer than three, so `build_cost_ic = 0.3375` - BlackICE's division-size technologies - is 3.375, and `0.0025` would be 0.025; the loader of modifiers and the rest cut to three as written. Built as the original's (`units.md`, 2). *Bug.* Risk: none in Their Finest Hour, which writes no such figure; three lines of BlackICE.
- ❌ **Building orders are barely checked,** and a new line's size is never capped. `buildinglines.md`, "What looks wrong". *Oversight.* Risk: small.
- ❌ **A line's price ignores its size,** and an order joining a line is not priced again. `buildinglines.md`, "What looks wrong". *Oversight.* Risk: moderate.
- ❌ **The escort rule is enforced by a button only.** `convoylines.md`, "What looks wrong", 1 and 2. *Oversight.* Risk: small.
- ❌ **A trade's cancel cost is shown and never charged.** `trade.md`, 15. *Display.* Risk: small if charged.
- ❌ **A route with no convoy is accepted and dies the next day.** `trade.md`, 3 and 15. *Oversight.* Risk: small.
- ❌ **Trade runs only in the `from` country's day.** One owning no province never trades. `trade.md`, 15. *Oversight.* Risk: small.
- ❌ **A seller is paid money nobody gave.** `trade.md`, 15. *Bug.* Risk: small.
- ❌ **Britain and France trade over land.** Two countries on one continent need no convoy, whatever lies between. `trade.md`, 15. *Oversight.* Risk: moderate.
- ❌ **Depot step 4 is nearly dead,** and may name a province that is not that area's depot. `supply.md`, 5. *Bug.* Risk: breaks the exact match with saves.
- ❌ **Fuel and supplies use different days,** and a share cut by one province's room stays cut for those after. `supply.md`, 12. *Bug.* Risk: large against saves.
- ❌ **A unit on ground that draws on nothing pays the null country for what it draws.** The depot of such ground is province 0, held by the null country, which the friend test takes for a friend of every country with no overlord, both ids being nought: so the unit's country pays as on a friend's depot, up to a quarter over, out of its stockpile into nowhere, booked as traded away and so in its supply need. A puppet pays nothing. BlackICE's Shanxi spends its whole stockpile so in a new game's two days. `supply.md`, 13; `reversing/findings/FINDINGS-supplypass.md`, section 4. *Bug.* Risk: small; a country's units cut off from any depot.
- ❌ **A road asked for more than it can pass empties the stockpile into the provinces.** A province asked for more than a nearer one can pass throws the difference back on the asker - once for it and once more for each nearer province after it - who asks it all again of the next; and every province keeps what was asked of it. So in a long war behind a narrow road the demand swells on its way in, the provinces beside the capital take it all, and the stockpile ends up lying in them: the original's own Japan had 112,729 supplies in its provinces and 527 in its stockpile in May 1940, and Germany here goes from 29,674 to 1,996 in three weeks from the 1941.12.11 bookmark. Nothing is lost, but whatever is paid out of the stockpile goes short. Taking a negative share as nought mends most of it: tried so on 2026-11-15, Germany ends 1941 with 22,826 in its stockpile where the rule leaves 1,996, and Berlin is asked about 900 a day with no day of 8,000. `supply.md`, 12.1; `reversing/findings/FINDINGS-supplypass.md`, section 3. *Bug.* Risk: large against saves of a long war; none at a start.
- ❌ **An idle harbour adds to the supply need.** The day's carriage added to what a country's units asked for counts a held province with a unit in it - and also one with a naval base and no supplies through it today, though nothing there asks for any. `supply.md`, 11; `reversing/findings/FINDINGS-laterstart.md`, section 18. *Oversight*, likely. Risk: the AI's supply share, which reads the need.
- ❌ **Exile manpower replaces the whole sum,** where adding may be meant; not settled. `manpower.md`. *Bug.* Risk: small.
- ❌ **A later start counts a country's months at war from the war's first day, not its own.** The Soviet Union, joining World War II at 1941.6.22, starts 21 months war-weary, with 40 days of stockpile and 0.352 of its most manpower gone. `manpower.md`, 4; `stockpile.md`, 2; `reversing/findings/FINDINGS-laterstart.md`, section 5. *Oversight.* Risk: large against the later start saves.
- ❌ **A new game weighs an occupier's most manpower by the order the countries are declared in.** Land held of a country set up later counts by that country's history law, of one set up earlier by its law brought forward: Japan's 130 Chinese provinces give it nothing at 1941.6.22. `manpower.md`, 4. *Oversight.* Risk: large against the later start saves.
- ❌ **An exile's days of stockpile go to its host's capital,** never to its own pool, and a seat moved afterwards leaves them behind: London holds seven exiles' at 1941.6.22, Amsterdam the Netherlands'. `stockpile.md`, 2. *Oversight.* Risk: large against the later start saves.
- ❌ **An exile hands its day's supplies and fuel to whoever holds its seat, an enemy too.** The hand-over comes in its day before the hour that would move its government to its faction leader's seat, so an exile whose seat has just fallen feeds the enemy holding it for a day. `war.md`, 14; `reversing/findings/FINDINGS-surrender.md`, C.8. *Oversight.* Risk: small.
- ❌ **The goods of a country that does not exist go to whoever holds its capital.** Everything lying in cut-off land is moved to its block's largest harbour each day, and a province's pool is also the stockpile of any country whose capital it is - so a country gone from the map ships its goods to the holder of its old capital. Kept by the decision of 2026-10-10, the save bearing it out. `convoys.md`, 8. *Oversight.* Risk: moderate against saves.
- ❌ **What a country that does not exist made at home grows without end.** Its day's money and supplies are added to the pool and only a country owning land or in exile has it cleared, so its ledger - the Lua's `GetDailyIncome` and `GetDailyBalance`, the save's `home` - shows every day's since it last existed as one day's income; its stockpile gets each day's once. `resources.md`, 3. *Oversight.* Risk: none to the stockpile; breaks the save's `home` for those countries.
- ✅ **A province's yield of fuel reads its holder's `global_money`, and of money its `global_fuel`.** **Mended by the fix `yield_modifiers`, 2026-11-25.** The table of a good's country modifier a province's yield is scaled by has the two the wrong way round, where the province's own table has them right. No province yields either today, so nothing shows it. `resources.md`, 3; `reversing/findings/FINDINGS-supplyconvoys.md`, section 8. *Bug.* Risk: none until a mod gives a province fuel or money to yield.
- ❌ **A change of law leaves the production slider's need at the queue's old prices.** The change prices every line again and counts nothing, so the need stays as last counted until the next midnight or the next order - a new game's first screen shows Germany 13.984 at 1936 where its queue then costs 16.159, the install's Lua having changed its laws after the two days. `buildqueue.md`, 4; `reversing/findings/FINDINGS-production.md`. *Oversight.* Risk: small; the AI's production script reads the need.
- ❌ **A strategic resource once had is kept after its province is lost** - at the best level it ever reached, for as long as the game runs - and **a load forgets it**, so whether a country still has the resource depends on whether a save was loaded since. `modifiers.md`, 6; `reversing/findings/FINDINGS-countrymodifier.md`, section 7. *Oversight.* Risk: moderate; a level would have to be made again each day from nothing.
- ✅ **A minister's technology decay does nothing.** *Done in the enhanced mode, 2026-11-21: the fix `minister_tech_decay` adds the sitting ministers' `decay` to the technologies' (`CResearch.DecayModifier`).* The rebuild of a country's modifier adds every sitting minister's `decay` to each category and then stores the technologies' figure over it, so 24 minister types' decay is thrown away. BlackICE's DLL mends it with `fixMinisterTechDecay`. `research.md`, 6; `reversing/findings/FINDINGS-bicelibsites.md`, section 4. *Bug.* Risk: every country's abilities, and the saves of an executable carrying the patch on disk, which agree with it (`research.md`, 8).
- ❌ **A lend-lease share with nobody to give to wastes its IC.** Only the open production screen puts the slider back to nought; a player who never opens it - for a week, in a reference 1936 game, at 15.5% - loses that IC, and nothing passes it to the other shares. `sliders.md`, 2; `reversing/findings/FINDINGS-lendleaseslider.md`. *Oversight.* Risk: small; the AI's script already zeroes its own.

## AI

- ✅ **The AI's air merge joins what its split takes apart.** **Mended by the fix `air_regroup_agrees`, 2026-11-26.** The
  merge's caps know the original's eight air types alone and the split's last rule every other, so a mod's own type -
  BlackICE's `light_bomber`, its `Flying_boat` - is joined to a group of strategic bombers, interceptors or CAS and
  split from it the hour after, for the whole game. `mechanics/agents.md`, section 5.3. *Oversight*, which the
  original's own eight types never meet. Risk: none seen; fewer orders, and air units that keep their names and leaders.
- ❌ **A dropped headquarters costs officers or not by which routine drops it.** The AI's disband of a headquarters it has no use for keeps the country's officers, as every disband does; the headquarters of a theatre its rebuild of theatres leaves over goes through the end-of-hour removal, which takes them. `units.md`, 9. *Oversight.* Risk: small.
- ❌ **The AI loses multi-level research** from its queue the moment a level finishes. `ministers.md`, 9. *Bug.* Risk: AI research.
- ❌ **The lend-lease figure given to the script is always 0.** `ministers.md`, 9. *Bug.* Risk: AI lend-lease.
- ❌ **A finished carrier air group placed after anything else that day looks at the first fleet alone.** The placing's flag
  of a posting is never cleared between deployments, so the walk for a fleet with an empty carrier stops at the first
  fleet once anything has been posted; a group with no empty carrier is never put on an airfield either, and may wait for
  ever. `ministers.md`, 6. *Bug.* Risk: AI carriers.
- ❌ **`GetRandomUnderGroundTarget` is a fixed pick.** `ministers.md`, 9. *Bug.* Risk: small.
- ❌ **The foreign minister's targets never come off.** `foreignminister.md`, 11. *Oversight.* Risk: AI diplomacy.
- ❌ **An agent with no unit ends the stance pass** for the hour. `agents.md`, 11. *Bug.* Risk: AI land.
- ❌ **A submarine in a mixed fleet always ends alone.** `agents.md`, 11. *Bug.* Risk: AI fleets.
- ❌ **A division over 1000 from a theatre's fronts can never be sent there.** The distance's first best is set against a square, so it caps at 1000, and a capped candidate scores nothing; a theatre with no fronts gets no one. `agents.md`, 10.2. *Bug.* Risk: AI land, where every overseas garrison stays short.
- ❌ **Any AI unit on the land of a country with no faction is lent to it, where its own country has none either.** The test is one shared "no faction". `agents.md`, 10.5. *Oversight.* Risk: AI land; the USA's division in the Philippines goes this way on the first day.
- ❌ **A garrison is rated higher the weaker it is.** `moving.md`, 18. *Bug.* Risk: AI land.
- ❌ **The line's pull can pick the same division twice and leave a position short.** It counts its picks on a count of its own, and nothing refuses a division it has already sent to the position, so a second pick of it books it off the position and back on, orders the move again and uses up the pick: Smolensk HQ's 2390 wanted four on 16 July 1941 and got three. `moving.md`, 7.3, step 7; `reversing/findings/FINDINGS-liveread.md`, section 110. *Bug.* Risk: AI land; the fix is to pass over a division already bound for the position.
- ✅ **What one country's AI sees of an ally's orders depends on thread timing.** The countries' passes run side by side and a unit's destination is written the moment it is sent, so a line counting an ally's divisions sees that hour's orders or not as the threads fall; two runs of one save can differ by it. Ours reads an ally as the hour began, which both July 1941 recordings agree with, but Germany - first on the main thread - as its orders stand, which the recordings agree with too. `moving.md`, 7.3, step 3; `reversing/findings/FINDINGS-liveread.md`, sections 111 and 143. *Bug.* Risk: none for the normal engine, which is already deterministic; an enhanced engine could keep ours on purpose.
- ✅ **The countries' unit AI passes could run side by side, as the original's do, and stay deterministic.** **Built 2026-11-21, for every game** (`agents.md`, section 12). Chosen on
  2026-11-19 as the way to do it, to be taken up once the AI's behaviour is settled: ours runs every country's pass one
  after another, which in a war is about a third of the hour's time. **The plan:**
  - every read of another country's AI fields uses the copy taken as the hour began, as the line's count of an ally
    already does. Those fields are where the AI sent a unit, held back, garrison, staging, sealift target, stranded,
    invasion, agent and promised;
  - the unit pass draws on a generator of its own for each country and hour, as the ministers' Lua already does;
  - each country posts to an outbox of its own, the outboxes joined in the countries' order afterwards;
  - the unit numbers a pass takes are given out again in that order, so they come out as today's.

  Measured that day with eight workers, a probe timing each country's pass:
  - in a 1941 war, 498 ms of a base game day down to about 226, and 776 of BlackICE's down to about 294;
  - in 1936, 126 down to 47 and 220 down to 89;
  - the floor is one country's pass, the Soviet Union's about 97 ms and 151 ms in 1941, so more workers give nothing.

  The orders, ways, plans and joins already go through commands, the caches the pass reads are made again only when
  land changes hands, and the route finder's arrays are safe across threads. *Unfinished.* Risk: AI land, sea and air,
  where a country would no longer see another's same-hour orders; the game parts from today's at the first draw or such
  read, so it needs a `docs/compatibility.md` entry if it is taken into the normal engine. A version keeping today's
  results exactly was weighed and set aside: every draw and every such read would have to wait for the countries before
  it - the draws and numbers alone cost up to a fifth of the gain, the reads more - and a missed read would make the game come apart only now and then.
  `docs/performance.md`, section 2.
- ❌ **The AI odds read a unit last battle figures.** The defence in `CAttackOdds` is the product its last battle left, nought for a unit that has never fought, so a fresh unit counts no toughness at all and a veteran is judged by a battle long over. `moving.md`, 11.4 and 18. *Oversight.* Risk: AI attacks.
- ❌ **The terrain figure's neighbour term re-reads the province's own terrain.** `moving.md`, 18. *Bug.* Risk: AI attacks.
- ❌ **The AI's air sunset ignores longitude.** `moving.md`, 17.4 and 18. *Oversight.* Risk: small.
- ❌ **Multi-role wings alone fly nothing.** `moving.md`, 17.4 and 18. *Oversight.* Risk: AI air.
- ❌ **AI transport planes are never rebased.** `moving.md`, 17.1, 17.3 and 18. *Bug.* Risk: AI air.
- ✅ **Which garrison the AI drops and which it picks hangs on where the heap put its region groups.** The split keeps its blocks of land in a map ordered by the groups' addresses, so two runs of one save can garrison differently: on 17 July 1941 one run sends the German 3. Infanterie-Division from Kirkenes to Oslo and another leaves it. Ours takes the blocks in the order the groups were made. `moving.md`, 7.2 and 19; `reversing/findings/FINDINGS-liveread.md`, section 130. *Bug.* Risk: none for the normal engine, which is already deterministic; an enhanced engine could keep ours on purpose.
- ❌ **A land unit aboard an aeroplane walks off while still aboard.** Nothing in the march asks the plane, so the AI's paradrop planner marches a passenger to its staging base, the plane keeps it on its list and the unit keeps pointing at the plane: the July 1941 save's 1. Fallschirmjäger Division stands in Berlin aboard a transport based at Oslo, and walks there through Sweden. `moving.md`, 9.1; `reversing/findings/FINDINGS-liveread.md`, section 127. *Bug.* Risk: AI paradrops; a fix would unload a passenger given a way, or fly it.
- ❌ **The theatre alignment test counts x twice and never y.** `theatres.md`, 11. *Bug.* Risk: AI theatres.
- ❌ **A rival's distance is halved, not scaled.** `aistrategy.md`, 11. *Oversight.* Risk: breaks the exact match with the start save.
- ❌ **A country's own land held by an enemy is never on its `conquer_prov`.** The list's making asks for a province "owned by the country, or touching its land", and the adder every rule goes through refuses a province the country owns, so the first half is dead and an occupied country - an exile above all - never aims at its own resources: Norway's Vemork, Poland's 2096, China's 5420 at a later start. `aistrategy.md`, 5. *Dead.* Risk: breaks the exact match with the start saves.
- ❌ **Prepared wars are dropped and prepared again.** `aistrategy.md`, 11. *Oversight.* Risk: AI war timing.

## Interface

All of these are risk-free but for what the entry says.

- ❌ **F12's picture of the map shows every province's true controller**, whatever the player knows and with the fog of war on, in a game between machines too - and with Shift every player's land. `screenshots.md`, 3. *Oversight.* Not risk-free between machines: a switch would be the host's.
- ❌ **The two pictures' notices and the wait between two pictures are counted in frames**, fifteen and ten, so both hang on the frame rate. `screenshots.md`, 1. *Oversight.*
- ✅ **A message's text on the map lives by frames, not time** **Mended by the fix `map_text_by_time`, 2026-11-27.**: its time grows by the game's speed and one each drawn frame, paused or not, so it is shorter on a faster machine. `messages.md`, 6. *Oversight.*
- ❌ **A bombing's end has no short account of its damage.** `DAMAGE_SHORT` is given the whole text `DAMAGE` has; the original makes a one-line copy for it and hands over the long one. `messages.md`, 9.2. *Bug.*
- ❌ **A land battle's message says nothing of exhaustion.** `OUREXHAUSTION` and `ENEMYEXHAUSTION` are one constant text, a nought, whatever happened. `messages.md`, 9.1. *Dead rule.*
- ❌ **A long text on the map is measured in lines and drawn in one row**, starting half its widest line left of its point and running off to the right. `messages.md`, 6. *Bug.*
- ❌ **The credits play twice on a second visit.** Each visit reads the file again onto the lines of the visits before, which nothing empties, so the roll is as many times as long as the menu's Credits has been pressed. `frontend.md`, 10. *Oversight.*
- ❌ **A credits file not ending in a line feed loses its last character**, the file's last byte ending a line as a line feed does. `frontend.md`, 10. *Oversight.*
- ✅ **A missing credits file leaves a view nothing leaves** in the original: no window and no Back are made. Here the Back is made. `frontend.md`, 10. *Oversight.*

- ❌ **The chat in play** (`mechanics/chat.md`; `reversing/findings/FINDINGS-ingamechat.md`, section 7).
  `menubar.gui`'s `chat_window`, with its recipient buttons None, Allies, Foes and All, is built and never shown,
  and the rows of players it would tick are named by no code: choosing who a line goes to was planned and
  dropped, and an enhanced chat could have it. A player who leaves a faction is not seen taken out of its channel
  (here he is, at once). Any faction but the three the executable names shares the Neutral channel, so a mod's
  own faction has no channel of its own. A row leaves `chat_list` only after eleven seconds from the last
  arrival or removal, so ten lines take 110 seconds to clear. The lag lines are signed with the host's name, as
  if the host had typed them.

- ❌ **A text's first word too wide for its box leaves the first line empty, and a last word too wide places its line by that word alone.** The font's line breaking tests a word as its space arrives; with nothing laid out yet the long-word break cannot fire, so a first word wider than the box breaks before itself onto a second line; and at the end a last word wider than the box is kept on its line with the line's width for its placing taken as the word's. The first is built (`CFontLineBreak`), the second not, a line here being placed by its own measured width. `interface.md`, 6; `reversing/findings/FINDINGS-textdraw.md`. *Bug.*

- ❌ **A `textBoxType` breaks a word that fills or overruns its box into empty lines and a character to a line.** Its routine (`CTextBoxLineBreak`) breaks before the word as soon as the line reaches the box, with no test of anything laid out, so a first word that fills the box leaves the first line empty - and the space after it, measured from a line count one short of the line, often a second; and the word's own count is never set back after such a break, so a word wider than the box goes on a character to a line. In a one-line box, cut off at its height less its border, that shows nothing at all. Built.

- ❌ **A `textBoxType` with no height drops its format, and one set right ends a pixel short.** Its font places lines by the box's format only where the box is of fixed size or gives both a width and a height; any other box has every line set left at its border, so the game options' `MustRestartTextBox`, written `centre`, is not centred. A line set right ends at the box's width less the border less one. Built as read. `interface.md`, 6; `reversing/findings/FINDINGS-textdraw.md`, section 2.2.1. *Oversight.* `interface.md`, 6; `reversing/findings/FINDINGS-textdraw.md`, section 2.2. *Bug.*

- ❌ **A box of fixed size counts its height less twice its border across, and loses a line at every full one.** The cut (`CFixedSizeCut`) takes the border's x for the height, the y never read; and the character that fills a line does not set the line's count back, so the next starts a line too, and a box of two lines holds one and a character. Built. `interface.md`, 6; `reversing/findings/FINDINGS-textdraw.md`, section 2.4. *Bug.*

- ❌ **A button's caption on a picture of several frames is laid out at a quarter of the whole picture, whatever the number of frames, and never cut.** The arm of the text sprite's routine for a sprite of more than one frame takes a quarter of the picture's width - one frame's only for a sheet of four - and skips the fixed-size cut the one-frame arm makes. No text sprite of the base game or of BlackICE has more than one frame, so nothing shows it. Built (`CButtonCaption.SpriteBox`). `interface.md`, 6; `reversing/findings/FINDINGS-textdraw.md`, section 2.2.2. *Oversight.*

- ❌ **A bombing's row names its goods under each other's words.** The goods whose stockpile the mission bombs are written after "Production of" and those whose production it bombs after "Stockpiles of", so a logistical strike reads "Production of Supplies and Fuel." `combatview.md`, 12. *Bug.*

- ❌ **A port or an airfield under a counter cannot be clicked.** **Kept, by the user's word of 2026-11-25.** A click takes the highest score, and a counter's (at least 1.1) always beats a building picture's (1.0), so an air unit at its base or a fleet in port whose base's picture lies under a land stack is reached only from the air map mode, or by moving the map. The enhanced engine could offer the building where the click falls on its badge outside the counter, or a list of what lies under the click. `mapunits.md`, 9; `reversing/findings/FINDINGS-seaair.md`, 6. *Oversight.*

- ❌ **The map's zoom eases by the frame, not by the second.** **Kept, by the user's word of 2026-11-25, with the wheel's one notch and the texts timed by frames.** Height and look point close 0.197 of the gap each frame, so the zoom is quicker the faster the game draws. A per-second ease of `1 − 0.803^(60·dt)` would feel the same at every frame rate. `mapunits.md`, 7; `reversing/findings/FINDINGS-camerazoom.md`. *Oversight.*
- ❌ **Only the wheel's sign counts, one notch a frame.** A quick turn is one notch. `reversing/findings/FINDINGS-camerazoom.md`, 1. *Oversight.*

- ❌ **Influence's colour never reaches the screen.** `topbar.md`, 7. *Bug.*
- ✅ **Tribute sent to a master shows green with a plus.** **Mended by the fix `shown_figures`, 2026-11-27.** `topbar.md`, 14. *Display.*
- ❌ **What governments in exile hand over has no line in a resource's tooltip.** The change at its head counts their supplies and fuel, and no line under it says where they came from, so the lines do not add up to the change. `topbar.md`, 14. *Display.*
- ❌ **The top bar's and the country screens' pictures are set once, as the game is entered**, so a played country changed in play - the console's `tag` - keeps the first one's: the bar's `topbar_TAG` and the six screens' `country_bg_TAG` over another country's name and figures. Built so since 2026-11-15 (`CSessionCarry.EnteredAs`). The enhanced engine would give them again with the country. `topbar.md`, 1; `console.md`, 3. *Oversight.*
- ❌ **The technology screen's sliders take the production screen's locks:** it reads the first four entries of the production screen's mirror for its own four - research locked where upgrades were - and never writes it, so its own locks last only while the screen stands. It reads them **as it is built, when a game is entered** - so only a loaded save's locks, never one set in play, which the user saw in the original on 2026-11-27; this engine read them at the screen's first opening until then, a regression of ours mended in every game. A fix for the original's part - locks of the screen's own, saved - was built that day and taken out again on his word, the fault he had seen being ours. `sliders.md`, 3. *Bug.*
- ✅ **The technology panel goes stale.** **Mended by the fix `fresh_screens`, 2026-11-25.** Its name, year, difficulty and rows of what the level does are made only when a technology is pressed, so a level finished or a law changed leaves the old ones up; and a one-level technology is described at level 1 for ever, a many-level one a level past its last. `technologyscreen.md`, 7. *Display.*
- ❌ **Money repaid on a debt counts as coming in.** The top bar's colour, the ledger tooltip's change and the Lua's daily balance take `repaid away` away from what goes out, though it is held above nought, so a repayment turns money green. `topbar.md`, 2. *Bug.*
- ✅ **Ordinals past 110 go wrong:** **Mended by the fix `unit_ordinals`, 2026-11-25.** "111st", "112nd", "113rd" - only 11, 12 and 13 are sent to "th". `builders.md`, 8; `units.md`, 3. *Bug.*
- ❌ **An unnamed air group and its wing can be numbered apart:** the wing is named as the file is read and the group after it, so a name later in the file takes a group's number but never a wing's. `units.md`, 3. *Oversight.*
- ✅ **Diplomacy rows go stale:** threat, name and flag. **Mended by the fix `fresh_screens`, 2026-11-25.** `diplomacyscreen.md`, 8. *Display.*
- ❌ **A zero balance reads "surplus"** on a red icon. `diplomacyscreen.md`, 8. *Display.*
- ❌ **The highest-threat tooltip describes the wrong thing.** `diplomacyscreen.md`, 8. *Display.*
- ❌ **The relation breakdown hides the largest cause,** the +2000 of an overlord and a puppet. `diplomacyscreen.md`, 8. *Display.*
- ❌ **`ruler_label` is never filled.** `diplomacyscreen.md`, 8. *Unfinished.*
- ❌ **The intelligence screen's `self_button` does nothing.** `espionagescreen.md`. *Dead.*
- ❌ **Estimates repeat by month,** every target sharing a month's draws. `espionagescreen.md`. *Bug.*
- ❌ **IC is exact though called an estimate,** and the pie and ministers show everything. `espionagescreen.md`. *Display.* Fixing it reveals less.
- ❌ **The spies' tooltip says ten whatever the define.** `espionagescreen.md`. *Display.*
- ✅ **The province view goes stale** until reselected. **Mended by the fix `fresh_screens`, 2026-11-25.** `provinceview.md`. *Display.*
- ❌ **The claims box shows cores,** the owner's among them. `provinceview.md`. *Display.*
- ❌ **Intelligence keys are one out:** `INTEL_2` is "Intel lvl 3". `provinceview.md`. *Display.*
- ❌ **The port tooltip misstates the rule.** `provinceview.md`. *Display.*
- ❌ **The statistics graph section is dead.** `statistics.md`, 2. *Unfinished.* The nation size's yearly history it would draw is kept and saved since 2026-11-14, so a graph of it has its data from the game's first year; income and inflation are never sampled (`statistics.md`, 5).
- ❌ **The victory alert names only the last faction.** `endgame.md`, 6. *Bug.*
- ✅ **Closing the menu leaves the game paused** after opening any of its windows. **Mended by the fix `menu_resumes`, 2026-11-25.** `ingamemenu.md`, 2. *Bug.*
- ❌ **Message settings half-work.** A click on a name does nothing, no tab starts ticked, and Reset leaves the rows. `ingamemenu.md`, 4. *Oversight.*
- 🟡 **A right click with several fleets or air units moves nobody.** **The window it brings up offers several air units what every one may fly, by the fix `air_orders_for_all`, 2026-11-25; fleets were so already. The click still asks and moves nobody, as for one unit.** `selection.md`, 8. *Oversight.*
- ✅ ~~**Attrition is shown a hundred times too small,**~~ and **a terrain's own attrition is left out** of the designer's line. **The first half was no fault and its fix is taken back, 2026-11-27**: attrition is a percentage as it stands - a terrain's `attrition = 2` is 2% a month, a technology's `attrition = -1` a point less, and the day takes the figure times a hundredth of a brigade's strength over thirty (`upkeep.md`) - so the original's plain figure with a `%` is right, and `shown_figures` wrote a hundred times it from 2026-11-25 until then, a level of BlackICE's mountain training as -150%. Found when the technology's text was to be given the same hundredfold. **The second half is mended by the fix `shown_figures`, 2026-11-27**: the designer's terrain summary counts the ground's own attrition in its line, as its attack and defence. `builders.md`; `brigadecard.md`, 6. *Display.*
- ✅ **The positioning penalty lacks its hundredfold.** **Mended by the fix `shown_figures`, 2026-11-25.** `reorganise.md`, 7. *Display.*
- ✅ **Tooltips can leave the screen** at the left or top. **Mended by the fix `tooltips_on_screen`, 2026-11-25.** `tooltips.md`, oddity 2. *Oversight.*
- ✅ **The air map misses updates,** **Mended by the fix `fresh_screens`, 2026-11-27.** and a count of 128 battles hides the icon. `airmap.md`, oddities 1 and 2. *Bug.*
- ❌ **The second-target hint is backwards.** `fleetsandair.md`, "The air window". *Display.*
- ❌ **Script text errors.** REMOVE_SPIES_EFFECT says "Add", SPIES_INFILTRATED says "Grk", about fifty keys show as their names. `scripttext.md`, 5. *Display.* Mods may ship their own.
- ✅ **A fleet's passenger list goes stale.** **Mended by the fix `fresh_screens`, 2026-11-25.** `transport.md`, "What looks wrong", 2. *Display.*
- ❌ **An air unit's `select_land` may be pressed and does nothing**: the panel enables it where paratroopers are aboard, and the click asks first whether the carrier is a fleet. `transport.md`, "What looks wrong", 21. *Bug.*
- ❌ **Effects of nothing are red,** and more encirclement bonus is red. `leaders.md`, oddity 27. *Display.*
- ❌ **The elections line says `YEARS` though it counts months.** `politics.md`, 11. *Display.*
- ✅ **Sliders with nothing needed show a hundred times the IC.** **Mended by the fix `shown_figures`, 2026-11-25.** `sliders.md`. *Display.*
- ❌ **Revolt tooltip mismatches, and its alert is never shown.** `revolt.md`, 7. *Unfinished.*
- ❌ **Offers past the tenth are invisible,** and still decline themselves after a week. `agreements.md`, oddity 11. *Oversight.*
- ✅ **The trade list goes stale** when one route ends as another starts. **Mended by the fix `fresh_screens`, 2026-11-25.** `trade.md`, 15. *Oversight.*
- 🟡 **The organisation bar's hourly regain is not the hour's.** **The roads at sea mended by the fix `shown_figures`, 2026-11-25; a unit given no supplies is still shown regaining nothing.** It counts the roads at sea too, so a fleet at sea is shown a hundredth of what it regains, and shows a unit given no supplies regaining nothing where the hour takes organisation away. `unitpanel.md`, 10.1; `organisation.md`, 3. *Display.*
- ❌ **A base with nothing based there is shown doing a quarter of its work**, where the hour's crowding counts it whole. `unitpanel.md`, 10.2. *Display.*
- ❌ **A model out of a file that leaves a technology out is marked `+*` wherever it shows**, being matched exactly by nothing, though it is the model the brigade was raised as. `units.md`, 5. *Display.*
- ❌ **The Quick Start panel promises each start its own share of the work and gives all four the same.** FE_QS_USA_INFO says "full control over the production, research, politics and intelligence", FE_QS_SOV_INFO "full control over both your military forces and your production", FE_QS_GER_INFO that the AI handles all but warfare; every button's body hands Diplomacy, Production, Technology, Politics and Intelligence to the AI alike, the tech minister on sliders and research. Built as read. `bookmarks.md`, 4; `reversing/findings/FINDINGS-bookmarks.md`, section 1.5. *Oversight.* Risk: none.
- ❌ **A Quick Start ignores the bookmarks and the defines' date range**: its four dates and tags are the executable's, so a mod that changes its bookmarks or its dates still starts Germany in 1939, the United States in 1936, Japan in 1941 and the Soviet Union in 1941 from its own history - BlackICE, with one bookmark, among them - and Japan's 7 December 1941 is no bookmark's date. A Quick Start also begins at hour 1 where a bookmark begins at hour 0, and chooses no victory conditions. Built as read. `bookmarks.md`, 4. *Oversight.* Risk: a mod's history may not reach the dates.
- ❌ **A projection that does not pulse pulses as it fades.** The fade multiplies the pulse's alpha whether or not the type says `pulsating`, so a plain mark with a `fadeout` flickers at the defaults' 0.4 and 3 as it goes. None of the install's three is touched - the two that fade pulse anyway - but a mod's would be. `movement.md`, 7. *Oversight.* Risk: none.

## Script, events and weather

- ❌ **A spent fire-once event blocks its on_action list.** In the base game 620 never fires behind 619, nor 720, 870 or 970 behind theirs. `events.md`, 8. *Bug.* Risk: events that never fired begin firing.
- ❌ **On_action weights are never read.** `events.md`, 8. *Dead.* Risk: event balance.
- ❌ **Province events with a mean time never fire on their own.** `events.md`, 8. *Dead.* Risk: dormant events begin firing.
- ❌ **Political decisions are read and never offered.** `events.md`, 9. *Unfinished.* Risk: content would appear.
- ❌ **`limit` inside a scope is ignored, and the effects beside it run.** BlackICE writes it 66 times (counted 2026-11-27 by `OpenHOI3.SaveCheck script`, its `Quirk` lines; 63 at the first count). Confirmed in the running game on 2026-11-27, by the user's own test. `script.md`, 6. *Bug.* Risk: mods' behaviour changes.
- ❌ **A `<unit type> = N` condition holds only for N of nought or less.** `triggers.md`. *Bug.* Risk: script results.
- ❌ **`lost_IC` is the share held, weighed by a modifier.** It keeps the owned provinces the country **holds** where `lost_national` beside it counts those it does not, and weighs each by its `local_ic` modifier, not its IC; with Their Finest Hour's local modifiers all penalties the figure is never above nought, so `lost_IC = 0.1` never holds. `triggers.md`, 5.1. *Bug.* Risk: script results; nothing in either tree asks it.
- ❌ **A battle side's "lost outright" byte is never set.** A side's defeat test and a decided battle's losses figures each read a byte that only the side's constructor writes, zeroing it, so the immediate loss and the figure's standing-strength, ships and wings terms never apply. `triggers.md`, 5.2. *Dead.* Risk: battles end and figures change.
- ✅ **The south has no season.** **Mended by the fix `southern_seasons`, 2026-11-27.** Latitude is nought everywhere south of the equator. `weather.md`. *Oversight.* Risk: large for southern theatres.
- ❌ **Season 5's doubling is dead** while `TEMPERATURECHANGESPEED` is positive. `weather.md`, "What looks wrong", 3. *Dead.* Risk: small.
- ✅ **A loaded game fires no event until its month turns.** Each country's candidate events are made only by the month's pass, the save keeps none and a load does not make them, so after a load nothing with a mean time to happen fires until the 2nd of the next month, when every candidate has its first roll at once. BlackICE's DLL has the month's making run once after a load with `rebuildCandidates`. `events.md`, 1; `reversing/findings/FINDINGS-events.md`. *Oversight.* Risk: events fire sooner after a load, and the generator moves a draw. **Mended 2026-11-26**, `saved_event_candidates`: the enhanced save keeps the lists.
- ✅ **Decision rows are never re-enabled** **Mended by the fix `fresh_screens`, 2026-11-27.** until the screen is reopened. `events.md`, 12. *Oversight.* Risk: none.

## Music and the loading screen

- ✅ **The next song is the heaviest, not one drawn by weight.** **Mended by the fix `weighted_songs`, 2026-11-27.** A song's chance times a roll of 5 to 9, the most winning, so a factor of 2 beats every song of factor 1 outright and equal weights always go to the song read first. A weighted draw is what `chance` reads as. `music.md`, 2.2. *Bug.* Risk: none.
- ❌ **Five songs or fewer that can win fall silent for good** once each has played, the six chosen last being left out. `music.md`, 2.3. *Oversight.* Risk: none.
- ❌ **The list of the six chosen last outlives a game**, so a new game's first songs depend on the last game's. `music.md`, 2.3. *Oversight.* Risk: none.
- ❌ **The music volume does not stop the picking; the master volume does**, and the end-of-game screen asks the master volume too. `music.md`, 1 and 4. *Oversight.* Risk: none.
- ❌ **Whether the title theme comes round again is a race** between two looks at the track's end in one frame, which as read it nearly always loses. `music.md`, 3. *Bug.* Risk: none.
- ❌ **Every loading picture but the first is let go at the first main menu**, so a game is always entered over the same picture. `loadingscreen.md`, 1. *Dead.* Risk: none.
- ❌ **The loading picture is a square of the screen's width**, its height taken from the width, so a portrait screen gets black bands and every picture's own bands are cut. `loadingscreen.md`, 1. *Bug.* Risk: none.

## The file checksum

- ❌ **The checksum guards little.** Each file's sum is added to one 32-bit figure, so exchanging two files' contents, moving a file between two summed folders or adding an empty file changes nothing; two installs agree by chance far more often than a real hash would allow; and the AI's Lua, the interface and the localisation are not summed at all, though players who differ there play different games. A hash over every file the game reads, by path and content, would guard a multiplayer of the enhanced engine's own. `checksum.md`, 7. *Oversight.* Risk: the letters would no longer be the original's.

## Sound effects

- ❌ **A misspelled `clicksound` silences a button** rather than falling back to `click`: the choice asks whether a name is written, not whether it is a sound, and ten of the original's buttons name `close_window` or `start_game`, which `sound.sfx` has not got. `sound.md`, 5. *Bug.* Risk: none.
- ❌ **A checkbox sounds its `upsound` when it is unticked**, a key that means the button coming up everywhere else. `sound.md`, 5. *Oversight.* Risk: none.
- ❌ **The effects never go below about −50 dB before they stop.** The volume is linear in decibels from a floor of half, so the effects slider's lower half barely changes anything and its last step is a jump to silence. `sound.md`, 4. *Oversight.* Risk: none.
- ❌ **The event window's `message` stacks** where every other sound the game plays is skipped while it plays, so several events at once are heard over each other. `sound.md`, 7. *Oversight.* Risk: none.
- ❌ **A fleet sent out of its range still sounds its move**, the sound coming before the range is asked. `sound.md`, 7. *Oversight.* Risk: none.
- ❌ **`buzzbeep`, `nuke` and `unit_upgraded` are never played**, nor a checkbox's `oversound`, nor `ambient_volume` read. `sound.md`, 5 and 7. *Dead.* Risk: none.

## Save bloat

What the original's save writes that later play does not need, and that grows with the game. Begun on 2026-11-20. The normal engine writes all of it, since its save keeps the original's keys (`savegame.md`, 7 to 10); an enhanced engine's own save could leave it out or write it shorter. Unlike the replay's list above, these are about what a save holds *beyond* what play needs, not what it lacks.

- ✅ ~~**An enhanced game's record of finished battles grows for the whole game**~~ - **packed since 2026-11-25**
  (`mechanics/combatreports.md`, section 3), by the user's word of that day: "we could save the combat reports as
  blobs as well, monthly or yearly, to reduce the filesize", and "keep them exact". A block a year, about 12
  bytes a battle and 18% of what a line a battle came to; a war year of BlackICE is some 20,000 battles and a
  quarter of a MiB. Nothing is gathered into sums and nothing dropped, so the record still grows with the game,
  a fifth as fast. *Not decided*: whether old reports are ever to be gathered or dropped.

- ✅ ~~**A battle's side writes a figure for every unit type**~~ - **the types it has, since 2026-11-26**
  (`mechanics/savegame.md`, section 21): `size`, read by nothing but the battle's end message, is a list as long
  as the unit database - some 1,650 figures a side under BlackICE, about ten kilobytes, nearly all nought. An
  enhanced save writes `openhoi_size` in its place, the types the side has any of by name, every figure exact. **Measured** on BlackICE saved on 10 February 1936 with four land battles being fought (`OpenHOI3.SaveCheck reload`): the eight sides' blocks come to 1,388 bytes where the original's lists would be about 79,000, and no figure of a side differs after the load. The first key of the original's an enhanced save leaves out.

- ❌ **Every sunk ship keeps its whole technology list.** `sunk_ships` holds every ship that has left the game - sunk at sea, finished by bombs, taken out with its fleet, or taken off by `remove_brigade` - and nothing is ever taken off it. Each is written as a ship still in a fleet is: its figures and a `<technology>={level progress}` line for every technology its type has. So the list grows with every loss for the whole game, and a late game with hundreds of sunk ships carries hundreds of technology lists nobody reads. What the game actually reads of a sunk ship is its id, owner, name, type and `sunk_by`: `total_of_ours_sunk` counts owners, `total_sunk_by_us` follows `sunk_by`, and a battle's `sunk_ship`, a ship's list of victims and the ledger page find ships by id and show their names and types. Only a brigade card left open on a ship that has gone shows its technology levels, under "Unit models". Keeping those five keys - and the card saying nothing under "Unit models" for a lost ship - would make each entry a few lines. The cost per ship depends on how many technologies its type has (`units/*.txt`, `technologies/*.txt`), so a mod with many ship technologies pays more. The game keeps the whole ship in memory too (`CSunkShips`), which the same change could trim. `savegame.md`, 7 and 10; `brigadecard.md`. *Oversight.* Risk: none to play; the save is no longer the original's, and a save written so is not known to load in the original.

## Existing departures that could become switches

- ❌ **A released puppet joins its master's supply at once** here, where the original takes some days. `compatibility.md`, entry 9. Risk: event chains timed on it.
